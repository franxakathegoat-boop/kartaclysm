using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kartaclysm {
public partial class MainForm : Form {
  public const string LauncherVersion = "1.11.0";
  public const int W = 1200, H = 720, SIDE = 236;
  // theme
  static readonly Color Cyan = Color.FromArgb(0, 229, 255), Blue = Color.FromArgb(60, 110, 255), Magenta = Color.FromArgb(255, 46, 147);
  static readonly Color TextC = Color.FromArgb(236, 242, 255), Muted = Color.FromArgb(146, 158, 186), Dim = Color.FromArgb(96, 106, 132);
  static readonly Color Ok = Color.FromArgb(60, 230, 140), Warn = Color.FromArgb(255, 190, 60), Bad = Color.FromArgb(255, 70, 90);

  enum Page { Home, Tracks, Updates, Online, Settings }
  Page page = Page.Home, prevPage = Page.Home; float pageSwitchAt = -10;

  Settings s = Settings.Load();
  readonly AppData data;
  readonly List<Bitmap> shots = new(), shotsBlur = new();
  readonly List<string> shotCaption = new();
  readonly Image logoSide, logoFull, wordmark, symbol;
  readonly PrivateFontCollection pfc = new();
  FontFamily fBold, fSemi, fMed;
  readonly Dictionary<(FontFamily, float), Font> fonts = new();

  readonly Stopwatch clock = Stopwatch.StartNew();
  float? fixedTime;               // used by the offscreen renderer
  float Now => fixedTime ?? (float)clock.Elapsed.TotalSeconds;
  readonly System.Windows.Forms.Timer anim = new() { Interval = 16 };
  bool intro;
  float ui = 1f;   // uniform UI scale for small screens
  Point Unscale(Point p) => ui == 1f ? p : new Point((int)(p.X / ui), (int)(p.Y / ui));
  float introStart = 0;

  // slideshow
  int shot = 0, prevShot = 0; float shotAt = 0; const float SHOT_LEN = 7f, SHOT_FADE = 1.2f;

  // hit regions rebuilt every frame
  class Hit { public RectangleF R; public string Id; public Action Click; }
  List<Hit> hits = new();
  Point mouse = new(-1, -1); string downId;
  float tracksScroll = 0, tracksScrollTarget = 0; string trackFilter = "ALL";
  string hoverForced;             // offscreen renderer hover simulation

  public MainForm(bool offscreen = false) {
    Text = "Kartaclysm";
    try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
    // fit small / high-DPI-scaled screens (e.g. 1536x864 at 125%): shrink the whole UI uniformly
    try {
      var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
      ui = Math.Min(1f, Math.Min((wa.Width - 24f) / W, (wa.Height - 48f) / H));
    } catch { ui = 1f; }
    ClientSize = new Size((int)Math.Round(W * ui), (int)Math.Round(H * ui));
    FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
    StartPosition = FormStartPosition.CenterScreen; BackColor = Color.Black;
    SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    var asm = typeof(MainForm).Assembly;
    Stream Res(string n) => asm.GetManifestResourceStream(n) ?? throw new Exception("Missing resource " + n);
    Bitmap Img(string n) { using var st = Res(n); using var i = Image.FromStream(st); return new Bitmap(i); }
    using (var st = Res("data.json")) data = JsonSerializer.Deserialize<AppData>(st);
    logoSide = Img("logo_side.png"); logoFull = Img("logo_full.png"); wordmark = Img("wordmark.png"); symbol = Img("symbol.png");
    foreach (var n in new[] { "bold.ttf", "semi.ttf", "med.ttf" }) {
      var tmp = Path.Combine(Path.GetTempPath(), "kartaclysm14-" + n);
      try { using var st = Res(n); using var o = File.Create(tmp); st.CopyTo(o); } catch (IOException) { }
      pfc.AddFontFile(tmp);
    }
    FontFamily Fam(string key) => pfc.Families.FirstOrDefault(f => f.Name.Replace(" ", "").Contains(key)) ;
    fBold = pfc.Families.FirstOrDefault(f => f.Name.Replace(" ", "") == "ChakraPetch") ?? Fam("Bold") ?? FontFamily.GenericSansSerif;
    fSemi = Fam("SemiBold") ?? fBold; fMed = Fam("Medium") ?? fBold;
    // screenshots: user's own folder first (…\AppData\Roaming\Kartaclysm\backgrounds), then built-in
    try {
      if (Directory.Exists(Settings.BackgroundsDir))
        foreach (var f in Directory.GetFiles(Settings.BackgroundsDir).Where(f => new[] { ".jpg", ".jpeg", ".png" }.Contains(Path.GetExtension(f).ToLowerInvariant())).OrderBy(f => f).Take(12)) {
          using var i = Image.FromFile(f); AddShot(new Bitmap(i), Path.GetFileNameWithoutExtension(f).ToUpperInvariant(), "");
        }
    } catch { }
    foreach (var si in data.shots) AddShot(Img("shots/" + si.file), si.track.ToUpperInvariant(), "TRACK BY " + si.author.ToUpperInvariant());
    if (shots.Count == 0) { var b = new Bitmap(W, H); using (var g = Graphics.FromImage(b)) g.Clear(Color.FromArgb(10, 14, 24)); AddShot(b, "", ""); }

    intro = s.Intro && !offscreen;
    if (offscreen) return;
    anim.Tick += (_, _) => Tick();
    anim.Start();
    MouseMove += (_, e) => { mouse = Unscale(e.Location); Cursor = HitAt(mouse) != null ? Cursors.Hand : Cursors.Default; };
    MouseLeave += (_, _) => { mouse = new Point(-1, -1); };
    MouseDown += (_, e) => { if (intro) { intro = false; return; } downId = HitAt(Unscale(e.Location))?.Id; };
    MouseUp += (_, e) => { var h = HitAt(Unscale(e.Location)); if (h != null && h.Id == downId) h.Click?.Invoke(); downId = null; };
    MouseWheel += (_, e) => { if (page == Page.Tracks) tracksScrollTarget = Math.Max(0, Math.Min(TracksMaxScroll(), tracksScrollTarget - e.Delta * 0.6f)); };
    KeyPreview = true; KeyDown += (_, _) => intro = false;
    Shown += async (_, _) => { UpdateMusic(); await SelfUpdate(); await CheckOnline(); };
  }
  void AddShot(Bitmap b, string cap, string by) {
    // cover-fit to 1.12x window for slow zoom; blurred copy for glass panels
    float sc = Math.Max(W * 1.12f / b.Width, H * 1.12f / b.Height);
    int w = (int)(b.Width * sc), h = (int)(b.Height * sc);
    var big = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
    using (var g = Graphics.FromImage(big)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(b, 0, 0, w, h); }
    shots.Add(big);
    var small = new Bitmap(W / 14, H / 14);
    using (var g = Graphics.FromImage(small)) { g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.DrawImage(big, new Rectangle(0, 0, small.Width, small.Height), (w - W) / 2, (h - H) / 2, W, H, GraphicsUnit.Pixel); }
    var blur = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
    using (var g = Graphics.FromImage(blur)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(small, new Rectangle(-8, -8, W + 16, H + 16)); }
    shotsBlur.Add(blur); shotCaption.Add(cap + "|" + by);
    if (b != big) b.Dispose(); small.Dispose();
  }

  void Tick() {
    float t = Now;
    if (intro && t - introStart >= INTRO_END) intro = false;
    if (!intro && t - shotAt > SHOT_LEN && page == Page.Home) NextShot(1);
    tracksScroll += (tracksScrollTarget - tracksScroll) * 0.22f;
    Invalidate();
  }
  void NextShot(int d) { prevShot = shot; shot = (shot + d + shots.Count) % shots.Count; shotAt = Now; }
  void GoShot(int i) { if (i == shot) return; prevShot = shot; shot = i; shotAt = Now; }
  void Go(Page p) { if (p == page) return; prevPage = page; page = p; pageSwitchAt = Now; if (p == Page.Online && services.All(x => x.State == 0)) _ = CheckOnline(); }

  Hit HitAt(Point p) { for (int i = hits.Count - 1; i >= 0; i--) if (hits[i].R.Contains(p)) return hits[i]; return null; }
  bool Hover(string id) => hoverForced == id || (!intro && HitAt(mouse)?.Id == id);
  bool Pressed(string id) => downId == id && Hover(id);
  void AddHit(RectangleF r, string id, Action a) => hits.Add(new Hit { R = r, Id = id, Click = a });

  // ---------------- drawing helpers ----------------
  Font F(FontFamily fam, float px) {
    if (!fonts.TryGetValue((fam, px), out var f)) {
      var st = fam.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular : FontStyle.Bold;
      f = new Font(fam, px, st, GraphicsUnit.Pixel); fonts[(fam, px)] = f;
    }
    return f;
  }
  static readonly StringFormat TF = MkTF();
  static StringFormat MkTF() { var f = (StringFormat)StringFormat.GenericTypographic.Clone(); f.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap; return f; }
  SizeF Measure(Graphics g, string t, Font f) => g.MeasureString(t, f, PointF.Empty, TF);
  void Txt(Graphics g, string t, Font f, Color c, float x, float y) { using var b = new SolidBrush(c); g.DrawString(t, f, b, x, y, TF); }
  // letter-spaced text, returns width
  float Spaced(Graphics g, string t, Font f, Color c, float x, float y, float sp, bool draw = true) {
    float x0 = x; using var b = new SolidBrush(c);
    foreach (var ch in t) { var s = ch.ToString(); if (draw) g.DrawString(s, f, b, x, y, TF); x += Measure(g, s, f).Width + sp; }
    return x - x0 - sp;
  }
  string Ellipsis(Graphics g, string t, Font f, float maxW) {
    if (Measure(g, t, f).Width <= maxW) return t;
    while (t.Length > 1 && Measure(g, t + "…", f).Width > maxW) t = t[..^1];
    return t + "…";
  }
  List<string> Wrap(Graphics g, string t, Font f, float maxW) {
    var lines = new List<string>(); var cur = "";
    foreach (var w in t.Split(' ')) { var tryS = cur.Length == 0 ? w : cur + " " + w; if (Measure(g, tryS, f).Width > maxW && cur.Length > 0) { lines.Add(cur); cur = w; } else cur = tryS; }
    if (cur.Length > 0) lines.Add(cur); return lines;
  }
  static GraphicsPath Round(RectangleF r, float rad) {
    var p = new GraphicsPath(); rad = Math.Min(rad, Math.Min(r.Width, r.Height) / 2); float d = rad * 2;
    if (d <= 0.5f) { p.AddRectangle(r); return p; }
    p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
    p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
  }
  static void DrawA(Graphics g, Image img, RectangleF dst, float a, RectangleF? src = null) {
    if (a <= 0.003f) return;
    var s = src ?? new RectangleF(0, 0, img.Width, img.Height);
    var pts = new[] { new PointF(dst.Left, dst.Top), new PointF(dst.Right, dst.Top), new PointF(dst.Left, dst.Bottom) };
    if (a >= 0.997f) { g.DrawImage(img, pts, s, GraphicsUnit.Pixel); return; }
    using var ia = new ImageAttributes(); ia.SetColorMatrix(new ColorMatrix { Matrix33 = a });
    g.DrawImage(img, pts, s, GraphicsUnit.Pixel, ia);
  }
  static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
  static float Smooth(float v) { v = Clamp01(v); return v * v * (3 - 2 * v); }
  static float EaseOut(float v) { v = Clamp01(v); return 1 - (1 - v) * (1 - v) * (1 - v); }
  static Color A(Color c, int a) => Color.FromArgb(Math.Max(0, Math.Min(255, a)), c.R, c.G, c.B);
  static Color Mix(Color a, Color b, float t) => Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

  // frosted glass panel: blurred background + tint + hairline border + top sheen
  void Glass(Graphics g, RectangleF r, float rad, int tint = 150, bool border = true, Color? tintCol = null) {
    using var p = Round(r, rad);
    var st = g.Save(); g.SetClip(p, CombineMode.Intersect);
    DrawA(g, shotsBlur[shot], new RectangleF(0, 0, W, H), 1);
    float fa = ShotFade(); if (fa < 1) DrawA(g, shotsBlur[prevShot], new RectangleF(0, 0, W, H), 1 - fa);
    using (var b = new SolidBrush(A(tintCol ?? Color.FromArgb(10, 14, 26), tint))) g.FillRectangle(b, r);
    using (var lg = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), Color.FromArgb(22, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillRectangle(lg, r);
    g.Restore(st);
    if (border) using (var pen = new Pen(Color.FromArgb(34, 255, 255, 255), 1f)) g.DrawPath(pen, p);
  }
  float ShotFade() => Clamp01((Now - shotAt) / SHOT_FADE);

  void DrawBackground(Graphics g, float t) {
    void One(int i, float a, float since) {
      var img = shots[i]; float z = 1 + 0.07f * Smooth(since / (SHOT_LEN + SHOT_FADE));
      float vw = W * 1.12f / z, vh = H * 1.12f / z;
      float pan = (i % 2 == 0 ? 1 : -1) * 0.25f * Smooth(since / (SHOT_LEN + SHOT_FADE));
      float sx = (img.Width - vw) / 2 + pan * (img.Width - vw) / 2, sy = (img.Height - vh) / 2;
      float k = img.Width / (W * 1.12f);
      DrawA(g, img, new RectangleF(-W * 0.06f, -H * 0.06f, W * 1.12f, H * 1.12f), a, new RectangleF(sx + (vw - vw) / 2, sy, vw * 1f, vh * 1f));
    }
    float fa = ShotFade();
    One(shot, 1, t - shotAt);
    if (fa < 1) One(prevShot, 1 - fa, t - shotAt + SHOT_LEN);
    // cinematic grade: left darkening for readability, bottom fade, vignette, cool tint
    using (var lg = new LinearGradientBrush(new RectangleF(0, 0, W, H), Color.FromArgb(235, 6, 8, 16), Color.FromArgb(40, 6, 8, 16), 0f)) {
      lg.InterpolationColors = new ColorBlend { Colors = new[] { Color.FromArgb(240, 5, 7, 14), Color.FromArgb(200, 5, 7, 14), Color.FromArgb(110, 5, 7, 14), Color.FromArgb(60, 5, 7, 14) }, Positions = new[] { 0f, 0.35f, 0.62f, 1f } };
      g.FillRectangle(lg, 0, 0, W, H);
    }
    using (var lg = new LinearGradientBrush(new RectangleF(0, H * 0.55f, W, H * 0.45f + 1), Color.FromArgb(0, 5, 7, 14), Color.FromArgb(230, 5, 7, 14), 90f)) g.FillRectangle(lg, 0, H * 0.55f + 1, W, H * 0.45f);
    using (var lg = new LinearGradientBrush(new RectangleF(0, 0, W, 140), Color.FromArgb(150, 5, 7, 14), Color.FromArgb(0, 5, 7, 14), 90f)) g.FillRectangle(lg, 0, 0, W, 139);
    // subtle cyan/magenta light bleed
    using (var gp = new GraphicsPath()) {
      gp.AddEllipse(W - 520, -260, 900, 620);
      using var pb = new PathGradientBrush(gp) { CenterColor = Color.FromArgb(34, Magenta), SurroundColors = new[] { Color.FromArgb(0, Magenta) } };
      g.FillPath(pb, gp);
    }
    using (var gp = new GraphicsPath()) {
      gp.AddEllipse(-200, H - 300, 900, 560);
      using var pb = new PathGradientBrush(gp) { CenterColor = Color.FromArgb(30, Cyan), SurroundColors = new[] { Color.FromArgb(0, Cyan) } };
      g.FillPath(pb, gp);
    }
  }

  // ---------------- icons ----------------
  void Icon_(Graphics g, string name, float x, float y, Color c, float sw = 1.8f) {
    using var pen = new Pen(c, sw) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
    using var br = new SolidBrush(c);
    switch (name) {
      case "home":
        g.DrawLines(pen, new[] { new PointF(x + 2, y + 9), new PointF(x + 10, y + 2), new PointF(x + 18, y + 9) });
        g.DrawLines(pen, new[] { new PointF(x + 4, y + 8), new PointF(x + 4, y + 18), new PointF(x + 16, y + 18), new PointF(x + 16, y + 8) });
        g.DrawRectangle(pen, x + 8.5f, y + 12, 3, 6); break;
      case "tracks":
        g.DrawLine(pen, x + 3, y + 2, x + 3, y + 19);
        for (int r = 0; r < 3; r++) for (int c2 = 0; c2 < 4; c2++) if ((r + c2) % 2 == 0) g.FillRectangle(br, x + 4 + c2 * 3.5f, y + 3 + r * 3.5f, 3.5f, 3.5f);
        g.DrawRectangle(pen, x + 4, y + 3, 14, 10.5f); break;
      case "updates":
        g.DrawArc(pen, x + 2, y + 2, 16, 16, -60, 290);
        g.DrawLines(pen, new[] { new PointF(x + 15, y + 1), new PointF(x + 15.5f, y + 5.5f), new PointF(x + 11, y + 6) });
        g.DrawLines(pen, new[] { new PointF(x + 10, y + 6), new PointF(x + 10, y + 10.5f), new PointF(x + 13, y + 12.5f) }); break;
      case "online":
        for (int i = 0; i < 4; i++) { float h = 4 + i * 4; g.FillRectangle(br, x + 2 + i * 4.6f, y + 19 - h, 2.8f, h); } break;
      case "settings":
        g.DrawEllipse(pen, x + 6.5f, y + 6.5f, 7, 7);
        for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; g.DrawLine(pen, x + 10 + (float)Math.Cos(a) * 6.5f, y + 10 + (float)Math.Sin(a) * 6.5f, x + 10 + (float)Math.Cos(a) * 9f, y + 10 + (float)Math.Sin(a) * 9f); }
        g.DrawEllipse(pen, x + 3.5f, y + 3.5f, 13, 13); break;
      case "play":
        g.FillPolygon(br, new[] { new PointF(x + 3, y + 1), new PointF(x + 19, y + 10), new PointF(x + 3, y + 19) }); break;
      case "sd":
        g.DrawLines(pen, new[] { new PointF(x + 4, y + 2), new PointF(x + 13, y + 2), new PointF(x + 17, y + 6), new PointF(x + 17, y + 19), new PointF(x + 4, y + 19), new PointF(x + 4, y + 2) });
        for (int i = 0; i < 3; i++) g.DrawLine(pen, x + 7 + i * 3, y + 5, x + 7 + i * 3, y + 8); break;
      case "grid":
        for (int r = 0; r < 2; r++) for (int c2 = 0; c2 < 2; c2++) g.DrawRectangle(pen, x + 2 + c2 * 9, y + 2 + r * 9, 7, 7); break;
      case "refresh":
        g.DrawArc(pen, x + 3, y + 3, 14, 14, 20, 300);
        g.DrawLines(pen, new[] { new PointF(x + 17.5f, y + 3), new PointF(x + 17.5f, y + 8), new PointF(x + 12.5f, y + 8) }); break;
      case "folder":
        g.DrawLines(pen, new[] { new PointF(x + 2, y + 17), new PointF(x + 2, y + 4), new PointF(x + 8, y + 4), new PointF(x + 10, y + 6.5f), new PointF(x + 18, y + 6.5f), new PointF(x + 18, y + 17), new PointF(x + 2, y + 17) }); break;
    }
  }

  // ---------------- buttons ----------------
  void PrimaryButton(Graphics g, RectangleF r, string id, string label, string icon, Action click, bool enabled = true) {
    bool hv = enabled && Hover(id), pr = enabled && Pressed(id);
    if (pr) r.Offset(0, 2);
    float t = Now; float pulse = 0.5f + 0.5f * MathF.Sin(t * 2.6f);
    int glow = !enabled ? 0 : hv ? 44 : (int)(18 + 12 * pulse);
    for (int i = 14; i >= 1; i--) using (var gp = Round(RectangleF.Inflate(r, i * 1.8f, i * 1.8f), 16 + i * 1.8f)) using (var b = new SolidBrush(Color.FromArgb(Math.Max(0, glow - i * 2), 0, 200, 255))) g.FillPath(b, gp);
    using (var gp = Round(r, 14)) {
      Color c1 = enabled ? (hv ? Color.FromArgb(70, 245, 255) : Cyan) : Color.FromArgb(70, 80, 100);
      Color c2 = enabled ? (hv ? Color.FromArgb(90, 140, 255) : Blue) : Color.FromArgb(50, 56, 72);
      if (pr) { c1 = Color.FromArgb(0, 190, 220); c2 = Color.FromArgb(40, 85, 220); }
      using (var lg = new LinearGradientBrush(new RectangleF(r.X - 1, r.Y, r.Width + 2, r.Height), c1, c2, 0f)) g.FillPath(lg, gp);
      // sheen sweep
      float sw = ((t * 0.45f) % 1.6f) - 0.3f;
      if (enabled) {
        var st = g.Save(); g.SetClip(gp, CombineMode.Intersect);
        float sx = r.X + sw * r.Width;
        using (var sb = new LinearGradientBrush(new RectangleF(sx - 60, r.Y, 120, r.Height), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 0f)) {
          sb.InterpolationColors = new ColorBlend { Colors = new[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) }, Positions = new[] { 0f, 0.5f, 1f } };
          using var m = new Matrix(); m.Shear(-0.4f, 0); 
          g.FillRectangle(sb, sx - 60, r.Y, 120, r.Height);
        }
        using (var lg = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height * 0.5f + 2), Color.FromArgb(60, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillRectangle(lg, r.X, r.Y, r.Width, r.Height * 0.5f);
        g.Restore(st);
      }
      using (var pen = new Pen(Color.FromArgb(enabled ? 150 : 40, 255, 255, 255), 1f)) g.DrawPath(pen, gp);
    }
    var f = F(fBold, 26); var sz = Measure(g, label, f);
    float iw = icon != null ? 26 : 0, tw = iw + sz.Width, x0 = r.X + (r.Width - tw) / 2, cy = r.Y + r.Height / 2;
    var tc = enabled ? Color.FromArgb(4, 14, 30) : Color.FromArgb(150, 160, 180);
    if (icon != null) Icon_(g, icon, x0 - 2, cy - 10, tc);
    Spaced(g, label, f, tc, x0 + iw, cy - sz.Height / 2 - 1, 2.5f);
    if (enabled) AddHit(r, id, click);
  }
  void GhostButton(Graphics g, RectangleF r, string id, string label, string icon, Action click, float fontPx = 15, bool active = false) {
    bool hv = Hover(id), pr = Pressed(id); if (pr) r.Offset(0, 1);
    Glass(g, r, r.Height > 44 ? 14 : r.Height / 2, hv ? 120 : 150, false, hv || active ? Color.FromArgb(10, 40, 60) : null);
    using (var gp = Round(r, r.Height > 44 ? 14 : r.Height / 2)) using (var pen = new Pen(hv || active ? A(Cyan, 200) : Color.FromArgb(60, 255, 255, 255), hv || active ? 1.4f : 1f)) g.DrawPath(pen, gp);
    var f = F(fBold, fontPx); var sz = Measure(g, label, f); float sp = 1.5f; float lw = Spaced(g, label, f, TextC, 0, 0, sp, false);
    float iw = icon != null ? 26 : 0, x0 = r.X + (r.Width - (iw + lw)) / 2, cy = r.Y + r.Height / 2;
    if (icon != null) Icon_(g, icon, x0 - 2, cy - 10, hv || active ? Cyan : TextC);
    Spaced(g, label, f, TextC, x0 + iw, cy - sz.Height / 2 - 1, sp);
    AddHit(r, id, click);
  }
  void Chip(Graphics g, float x, float y, string label, Color dot, out float w) {
    var f = F(fBold, 12); float lw = Spaced(g, label, f, TextC, 0, 0, 1.6f, false); w = lw + 34;
    var r = new RectangleF(x, y, w, 28); Glass(g, r, 14, 140);
    using (var b = new SolidBrush(dot)) g.FillEllipse(b, x + 12, y + 11, 6, 6);
    using (var b = new SolidBrush(A(dot, 60))) g.FillEllipse(b, x + 9, y + 8, 12, 12);
    Spaced(g, label, f, TextC, x + 24, y + 7, 1.6f);
  }
  void ProgressBar(Graphics g, RectangleF r, float p) {
    using (var gp = Round(r, r.Height / 2)) using (var b = new SolidBrush(Color.FromArgb(40, 255, 255, 255))) g.FillPath(b, gp);
    if (p <= 0) return;
    var fr = new RectangleF(r.X, r.Y, Math.Max(r.Height, r.Width * p), r.Height);
    using (var gp = Round(RectangleF.Inflate(fr, 3, 3), r.Height)) using (var b = new SolidBrush(Color.FromArgb(50, 0, 200, 255))) g.FillPath(b, gp);
    using (var gp = Round(fr, r.Height / 2)) using (var lg = new LinearGradientBrush(new RectangleF(r.X - 1, r.Y, r.Width + 2, r.Height), Cyan, Magenta, 0f)) g.FillPath(lg, gp);
  }

  // ---------------- sidebar ----------------
  static readonly (Page p, string label, string icon)[] Nav = {
    (Page.Home, "HOME", "home"), (Page.Tracks, "TRACKS", "tracks"), (Page.Updates, "UPDATES", "updates"), (Page.Online, "ONLINE", "online"), (Page.Settings, "SETTINGS", "settings") };
  void DrawSidebar(Graphics g) {
    var r = new RectangleF(0, 0, SIDE, H);
    Glass(g, r, 0, 185, false);
    using (var lg = new LinearGradientBrush(new RectangleF(SIDE - 1, 0, 1, H), A(Cyan, 0), A(Cyan, 90), 90f)) {
      lg.InterpolationColors = new ColorBlend { Colors = new[] { A(Cyan, 0), A(Cyan, 110), A(Magenta, 90), A(Magenta, 0) }, Positions = new[] { 0f, 0.35f, 0.7f, 1f } };
      g.FillRectangle(lg, SIDE - 1, 0, 1, H);
    }
    DrawA(g, logoSide, new RectangleF(22, 34, 192, 192f * logoSide.Height / logoSide.Width), 1);
    Spaced(g, "MENU", F(fBold, 11), Dim, 28, 116, 3);
    float y = 140;
    for (int i = 0; i < Nav.Length; i++) {
      var (p, label, icon) = Nav[i]; var item = new RectangleF(14, y, SIDE - 28, 46); string id = "nav" + i;
      bool act = page == p, hv = Hover(id);
      if (act || hv) {
        using (var gp = Round(item, 10)) using (var lg = new LinearGradientBrush(new RectangleF(item.X - 1, item.Y, item.Width + 2, item.Height), act ? A(Cyan, 46) : Color.FromArgb(18, 255, 255, 255), A(Cyan, 0), 0f)) g.FillPath(lg, gp);
      }
      if (act) {
        using (var b = new SolidBrush(Cyan)) g.FillRectangle(b, item.X, item.Y + 12, 3, item.Height - 24);
        for (int k = 1; k <= 6; k++) using (var b = new SolidBrush(A(Cyan, 26 - k * 4))) g.FillRectangle(b, item.X, item.Y + 12 - k, 3 + k * 2, item.Height - 24 + 2 * k);
      }
      var c = act ? Cyan : hv ? TextC : Muted;
      Icon_(g, icon, item.X + 20, item.Y + 13, c);
      Spaced(g, label, F(fBold, 15), act ? TextC : c, item.X + 54, item.Y + 14, 2.2f);
      AddHit(item, id, () => Go(p));
      y += 52;
    }
    // pack status card
    var card = new RectangleF(16, H - 128, SIDE - 32, 104);
    Glass(g, card, 12, 120);
    Spaced(g, "PACK", F(fBold, 11), Dim, card.X + 16, card.Y + 14, 2.5f);
    var pv = string.IsNullOrEmpty(s.PackVersion) ? "NOT INSTALLED" : "v" + s.PackVersion;
    Txt(g, pv, F(fBold, 20), TextC, card.X + 16, card.Y + 30);
    var dotc = string.IsNullOrEmpty(s.PackVersion) ? Warn : Ok;
    using (var b = new SolidBrush(dotc)) g.FillEllipse(b, card.X + 16, card.Y + 64, 7, 7);
    Txt(g, string.IsNullOrEmpty(s.PackVersion) ? "Installs on first PLAY" : "Auto-updates on PLAY", F(fMed, 12), Muted, card.X + 30, card.Y + 60);
    Txt(g, "LAUNCHER v" + LauncherVersion, F(fSemi, 11), Dim, card.X + 16, card.Y + 80);
  }

  // ---------------- frame ----------------
  protected override void OnPaint(PaintEventArgs e) { RenderFrame(e.Graphics); }
  void Quality(Graphics g) {
    g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBilinear;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; g.CompositingQuality = CompositingQuality.HighSpeed;
  }
  Bitmap pageLayer;
  void RenderFrame(Graphics g) {
    Quality(g);
    if (ui != 1f) { g.ScaleTransform(ui, ui); g.InterpolationMode = InterpolationMode.HighQualityBilinear; }
    float t = Now;
    if (intro) { DrawIntro(g, t - introStart); return; }
    DrawMain(g, t, 1f);
  }
  void DrawMain(Graphics g, float t, float uiAlpha) {
    hits = new List<Hit>();
    DrawBackground(g, t);
    // page with transition (fade + slide)
    float k = EaseOut((t - pageSwitchAt) / 0.35f);
    pageLayer ??= new Bitmap(W, H, PixelFormat.Format32bppPArgb);
    if (k < 1) {
      using (var lg = Graphics.FromImage(pageLayer)) { lg.Clear(Color.Transparent); Quality(lg); var saved = hits; hits = new List<Hit>(); DrawPage(lg, prevPage); hits = saved; }
      DrawA(g, pageLayer, new RectangleF(-24 * k, 0, W, H), (1 - k) * uiAlpha);
    }
    using (var lg = Graphics.FromImage(pageLayer)) { lg.Clear(Color.Transparent); Quality(lg); DrawPage(lg, page); }
    DrawA(g, pageLayer, new RectangleF(24 * (1 - k), 0, W, H), k * uiAlpha);
    using (var lg = Graphics.FromImage(pageLayer)) { lg.Clear(Color.Transparent); Quality(lg); DrawSidebar(lg); }
    DrawA(g, pageLayer, new RectangleF(0, 0, W, H), uiAlpha);
  }

  // ---------------- offscreen preview renderer ----------------
  public static void RenderOffscreen(string outDir, string what) {
    Directory.CreateDirectory(outDir);
    var f = new MainForm(true);
    f.s = new Settings { PackVersion = "0.2.0", GamePath = @"D:\Games\Mario Kart Wii (PAL).rvz", DolphinPath = @"C:\Program Files\Dolphin\Dolphin.exe" };
    foreach (var sv in f.services) { sv.State = 1; sv.Ms = sv.Name.StartsWith("WIIMMFI L") ? 48 : sv.Name.StartsWith("WIIMMFI R") ? 52 : 33; sv.Detail = $"Reachable · {sv.Ms} ms"; sv.When = DateTime.Now; }
    void Save(string name) {
      using var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
      using (var g = Graphics.FromImage(bmp)) f.RenderFrame(g);
      bmp.Save(Path.Combine(outDir, name + ".png"), ImageFormat.Png);
    }
    if (what == "intro") {
      f.intro = true; f.introStart = 0;
      for (int i = 0; i < (int)((INTRO_END + 0.8f) * 30); i++) { f.fixedTime = i / 30f; f.intro = f.fixedTime < INTRO_END; f.shotAt = INTRO_END - 0.01f; Save($"f{i:0000}"); }
      return;
    }
    var p = what switch { "tracks" => Page.Tracks, "updates" => Page.Updates, "online" => Page.Online, "settings" => Page.Settings, _ => Page.Home };
    f.page = p; f.pageSwitchAt = -10; f.fixedTime = 4.2f; f.shotAt = 0;
    if (what == "home-hover") { f.hoverForced = "play"; f.showBar = true; f.progress = 0.62; f.status = "Downloading 3/7: n64_rainbow_road.szs"; }
    f.RenderFrame(Graphics.FromImage(new Bitmap(1, 1))); // build hit list / layout once
    Save(what);
  }
}
}
