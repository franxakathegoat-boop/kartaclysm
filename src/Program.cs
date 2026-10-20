using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kartaclysm {
static class Program {
  [STAThread] static void Main() { ApplicationConfiguration.Initialize(); Application.Run(new MainForm()); }
}

public class Settings {
  public string GamePath { get; set; }
  public string DolphinPath { get; set; }
  public string PackVersion { get; set; }
  static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kartaclysm");
  public static string PackDir => Path.Combine(Dir, "pack");
  public static string DataDir => Dir;
  static string FilePath => Path.Combine(Dir, "settings.json");
  public static Settings Load() { try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings(); } catch { return new Settings(); } }
  public void Save() { Directory.CreateDirectory(Dir); File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
}

public class ManifestFile { public string path { get; set; } public string sha256 { get; set; } public long size { get; set; } public string url { get; set; } }
public class Manifest {
  public string version { get; set; }
  public List<ManifestFile> files { get; set; } = new();
  public string riivolution_xml { get; set; }
  public List<RiiOption> options { get; set; } = new();
}
public class RiiOption { public string section { get; set; } public string option { get; set; } public int choice { get; set; } }

public class MainForm : Form {
  const string ManifestUrl = "https://github.com/franxakathegoat-boop/kartaclysm/releases/download/pack/manifest.json";
  const string LauncherVersion = "1.3.0";
  // ======== layout & animation constants (mirrored exactly by art/render.py) ========
  const float WIN_W = 1000, WIN_H = 620;
  // logo.png meta (art/build_assets.py -> Assets/meta.json)
  const float LOGO_W = 1200, LOGO_H = 528, EMB_X = -9.402f, EMB_Y = -106.392f, EMB_S = 643.299f, TYRE_CX = 0.34f, TYRE_CY = 0.66f;
  static readonly RectangleF MainLogo = new(6, -30, 650, 650 * LOGO_H / LOGO_W);
  static readonly RectangleF IntroLogo = new(90, (WIN_H - 820 * LOGO_H / LOGO_W) / 2, 820, 820 * LOGO_H / LOGO_W);
  const float T_IMPACT = 0.75f, T_OUT = 2.75f, T_OUT_LEN = 0.65f, T_UI = 3.15f, T_END = 3.6f;
  static readonly Rectangle PlayR = new(712, 524, 262, 66);
  static readonly Rectangle SdR = new(718, 20, 140, 36);
  static readonly Rectangle SetR = new(868, 20, 106, 36);
  static readonly Rectangle BarR = new(28, 568, 600, 8);
  const int StatusY = 538, VersionY = 586;
  // ===================================================================================
  class Btn { public Rectangle R; public string Text; public bool Hover, Down, Enabled = true; public Func<Task> Click; }
  readonly Btn play = new() { R = PlayR, Text = "PLAY" }, sd = new() { R = SdR, Text = "EXPORT TO SD" }, set = new() { R = SetR, Text = "SETTINGS" };
  readonly List<Btn> btns;
  string status = "Ready to race";
  double progress = 0; bool showBar = false;
  readonly Image bg, logo, emblem, cracks, glowImg;
  readonly System.Drawing.Text.PrivateFontCollection pfc = new();
  FontFamily fBold, fSemi;
  Settings s = Settings.Load();
  static readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(30) };
  readonly Stopwatch clock = Stopwatch.StartNew();
  readonly System.Windows.Forms.Timer anim = new() { Interval = 15 };
  bool intro = true; float introSkipAt = -1;
  struct Spark { public float vx, vy, life; }
  readonly Spark[] sparks;
  struct Ember { public float x0, y0, sp, sz, ph; }
  readonly Ember[] embers;

  // deterministic RNG shared with the Python preview renderer
  static uint lcg = 1337;
  static float Rnd() { lcg = (uint)((lcg * 1103515245UL + 12345UL) % 2147483648UL); return lcg / 2147483648f; }

  public MainForm() {
    Text = "Kartaclysm";
    try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
    ClientSize = new Size((int)WIN_W, (int)WIN_H);
    FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
    StartPosition = FormStartPosition.CenterScreen; BackColor = Color.Black;
    SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    var asm = typeof(MainForm).Assembly;
    Image Res(string n) { using var st = asm.GetManifestResourceStream(n); return new Bitmap(Image.FromStream(st)); }
    bg = Res("background.jpg"); logo = Res("logo.png"); emblem = Res("emblem.png"); cracks = Res("cracks.png"); glowImg = Res("glow.png");
    foreach (var n in new[] { "bold.ttf", "semi.ttf" }) {
      var tmp = Path.Combine(Path.GetTempPath(), "kartaclysm-" + n);
      using (var st = asm.GetManifestResourceStream(n)) using (var o = File.Create(tmp)) st.CopyTo(o);
      pfc.AddFontFile(tmp);
    }
    fBold = pfc.Families.FirstOrDefault(f => f.Name == "Rajdhani") ?? pfc.Families[0];
    fSemi = pfc.Families.FirstOrDefault(f => f.Name.Contains("SemiBold")) ?? fBold;
    lcg = 1337; sparks = new Spark[170];
    for (int i = 0; i < sparks.Length; i++) {
      float ang = Rnd() * 6.2831853f, spd = 180 + Rnd() * 620, life = 0.6f + Rnd() * 0.9f;
      sparks[i] = new Spark { vx = MathF.Cos(ang) * spd, vy = MathF.Sin(ang) * spd * 0.75f - 120, life = life };
    }
    embers = new Ember[70];
    for (int i = 0; i < embers.Length; i++) embers[i] = new Ember { x0 = Rnd() * WIN_W, y0 = Rnd() * WIN_H, sp = 25 + Rnd() * 70, sz = 1.2f + Rnd() * 2.4f, ph = Rnd() * 6.2831853f };
    play.Click = OnPlay; sd.Click = ExportSd; set.Click = () => { AskPaths(true); return Task.CompletedTask; };
    btns = new() { play, sd, set };
    http.DefaultRequestHeaders.UserAgent.ParseAdd("Kartaclysm/" + LauncherVersion);
    anim.Tick += (_, _) => { if (intro && IntroT() >= T_END) intro = false; Invalidate(); };
    anim.Start();
    MouseMove += (_, e) => { if (intro) return; bool ch = false; foreach (var b in btns) { var h = b.R.Contains(e.Location); if (h != b.Hover) { b.Hover = h; ch = true; } } Cursor = btns.Any(b => b.Hover && b.Enabled) ? Cursors.Hand : Cursors.Default; if (ch) Invalidate(); };
    MouseLeave += (_, _) => { foreach (var b in btns) { b.Hover = b.Down = false; } Invalidate(); };
    MouseDown += (_, e) => { if (intro) { intro = false; Invalidate(); return; } foreach (var b in btns) if (b.Enabled && b.R.Contains(e.Location)) b.Down = true; Invalidate(); };
    KeyDown += (_, _) => { intro = false; Invalidate(); };
    MouseUp += async (_, e) => {
      var hit = btns.FirstOrDefault(b => b.Down && b.R.Contains(e.Location));
      foreach (var b in btns) b.Down = false; Invalidate();
      if (hit != null && hit.Enabled) { foreach (var b in btns) b.Enabled = false; Invalidate(); try { await hit.Click(); } finally { foreach (var b in btns) b.Enabled = true; Invalidate(); } }
    };
  }
  static Font MkFont(FontFamily fam, float px, FontStyle st) {
    foreach (var s2 in new[] { st, FontStyle.Bold, FontStyle.Regular })
      if (fam != null && fam.IsStyleAvailable(s2)) return new Font(fam, px, s2, GraphicsUnit.Pixel);
    return new Font("Segoe UI", px * 0.8f, FontStyle.Bold, GraphicsUnit.Pixel);
  }
  float IntroT() => (float)clock.Elapsed.TotalSeconds;
  float Time() => (float)clock.Elapsed.TotalSeconds;
  void SetStatus(string t) { status = t; Invalidate(); }

  static GraphicsPath Round(RectangleF r, float rad) {
    var p = new GraphicsPath(); float d = rad * 2;
    p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
    p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
  }
  static void DrawA(Graphics g, Image img, RectangleF r, float a) {
    if (a <= 0.003f) return;
    var pts = new[] { new PointF(r.Left, r.Top), new PointF(r.Right, r.Top), new PointF(r.Left, r.Bottom) };
    if (a >= 0.997f) { g.DrawImage(img, pts); return; }
    using var ia = new System.Drawing.Imaging.ImageAttributes();
    ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = a });
    g.DrawImage(img, pts, new RectangleF(0, 0, img.Width, img.Height), GraphicsUnit.Pixel, ia);
  }
  static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
  static float Smooth(float v) { v = Clamp01(v); return v * v * (3 - 2 * v); }
  static RectangleF Lerp(RectangleF a, RectangleF b, float t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Width + (b.Width - a.Width) * t, a.Height + (b.Height - a.Height) * t);
  static RectangleF EmblemRect(RectangleF L) { float k = L.Width / LOGO_W; return new(L.X + EMB_X * k, L.Y + EMB_Y * k, EMB_S * k, EMB_S * k); }
  static PointF TyreCenter(RectangleF L) { var e = EmblemRect(L); return new(e.X + TYRE_CX * e.Width, e.Y + TYRE_CY * e.Height); }

  void DrawEmbers(Graphics g, float t, float alphaMul) {
    foreach (var m in embers) {
      float x = m.x0 + 14 * MathF.Sin(t * 1.3f + m.ph) + 10 * t;
      float y = m.y0 - m.sp * t; y = ((y % (WIN_H + 20)) + (WIN_H + 20)) % (WIN_H + 20) - 10;
      x = ((x % WIN_W) + WIN_W) % WIN_W;
      int a = (int)(alphaMul * Clamp01(0.55f + 0.45f * MathF.Sin(t * 3 + m.ph)) * 230);
      if (a <= 2) continue;
      using var br = new SolidBrush(Color.FromArgb(a / 3, 255, 90, 10));
      g.FillEllipse(br, x - m.sz * 1.8f, y - m.sz * 1.8f, m.sz * 3.6f, m.sz * 3.6f);
      using var br2 = new SolidBrush(Color.FromArgb(a, 255, 190, 90));
      g.FillEllipse(br2, x - m.sz / 2, y - m.sz / 2, m.sz, m.sz);
    }
  }

  protected override void OnPaint(PaintEventArgs e) {
    var g = e.Graphics;
    g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
    if (intro) { DrawIntro(g, IntroT()); return; }
    g.DrawImage(bg, 0, 0, WIN_W, WIN_H);
    DrawEmbers(g, Time(), 0.8f);
    DrawA(g, logo, MainLogo, 1);
    DrawUI(g, Time());
  }

  void DrawIntro(Graphics g, float t) {
    g.Clear(Color.Black);
    var P = TyreCenter(IntroLogo);
    float dt = t - T_IMPACT;
    float sx = 0, sy = 0;
    if (dt > 0 && dt < 0.7f) { float k = 16 * MathF.Exp(-6 * dt); sx = k * MathF.Sin(53 * dt); sy = k * 0.7f * MathF.Cos(41 * dt); }
    var st = g.Save(); g.TranslateTransform(sx, sy);
    float bgA = dt <= 0 ? 0 : MathF.Min(0.35f, dt * 2);
    if (t > T_OUT) bgA = 0.35f + 0.65f * Smooth((t - T_OUT) / T_OUT_LEN);
    DrawA(g, bg, new RectangleF(0, 0, WIN_W, WIN_H), bgA);
    DrawEmbers(g, t, Clamp01(t / 0.5f));
    {
      float u = Clamp01(t / T_IMPACT);
      float gs = dt <= 0 ? 120 + 520 * u * u : 640 + 200 * dt;
      float ga = dt <= 0 ? MathF.Pow(u, 1.5f) * 0.9f : 0.9f * Clamp01(1 - dt / 1.6f);
      DrawA(g, glowImg, new RectangleF(P.X - gs / 2, P.Y - gs / 2, gs, gs), ga);
    }
    if (dt <= 0) {
      float u = Clamp01(t / T_IMPACT), ee = MathF.Pow(u, 1.6f);
      var fin = EmblemRect(IntroLogo); float fcx = fin.X + fin.Width / 2, fcy = fin.Y + fin.Height / 2;
      float size = fin.Width * (0.45f + 0.55f * ee);
      float cx = 1150 + (fcx - 1150) * ee, cy = -180 + (fcy + 180) * ee;
      DrawA(g, emblem, new RectangleF(cx - size / 2, cy - size / 2, size, size), Clamp01(u * 3));
    } else {
      float crackA = Clamp01(dt / 0.08f) * (t < T_OUT ? 1 : Clamp01(1 - (t - T_OUT) / 0.5f));
      DrawA(g, cracks, new RectangleF(P.X - 500, P.Y - 310, 1000, 620), crackA);
      for (int i = 0; i < 2; i++) {
        float d = dt - 0.08f * i; if (d <= 0 || d >= 0.9f) continue;
        float q = d / 0.9f, r = 40 + 720 * (1 - MathF.Pow(1 - q, 3)), w = MathF.Max(1, 26 * (1 - q));
        int a = (int)(MathF.Pow(1 - q, 1.5f) * (i == 0 ? 230 : 160));
        using var pen = new Pen(i == 0 ? Color.FromArgb(a, 255, 150, 40) : Color.FromArgb(a, 255, 240, 210), w);
        g.DrawEllipse(pen, P.X - r, P.Y - r, 2 * r, 2 * r);
      }
      const float c = 1.8f, grav = 420;
      foreach (var p in sparks) {
        if (dt >= p.life) continue;
        float f = dt / p.life, ex = (1 - MathF.Exp(-c * dt)) / c;
        float x = P.X + p.vx * ex, y = P.Y + p.vy * ex + 0.5f * grav * dt * dt;
        float vx = p.vx * MathF.Exp(-c * dt), vy = p.vy * MathF.Exp(-c * dt) + grav * dt;
        var col = f < 0.25f ? Color.FromArgb((int)(255 * (1 - f)), 255, 240, 180) : f < 0.6f ? Color.FromArgb((int)(255 * (1 - f)), 255, 150, 40) : Color.FromArgb((int)(255 * (1 - f)), 220, 60, 20);
        using var pen = new Pen(col, f < 0.5f ? 2.5f : 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pen, x, y, x - vx * 0.03f, y - vy * 0.03f);
      }
      var L = IntroLogo; float la = Clamp01(dt / 0.06f);
      if (t > T_OUT) L = Lerp(IntroLogo, MainLogo, Smooth((t - T_OUT) / T_OUT_LEN));
      else { float sc = 1 + 0.35f * MathF.Exp(-10 * dt) * MathF.Cos(12 * dt); float cx = L.X + L.Width / 2, cy = L.Y + L.Height / 2; L = new RectangleF(cx - L.Width * sc / 2, cy - L.Height * sc / 2, L.Width * sc, L.Height * sc); }
      DrawA(g, logo, L, la);
    }
    g.Restore(st);
    if (dt > 0 && dt < 0.3f) { int a = (int)(MathF.Pow(1 - dt / 0.3f, 2) * 255); using var br = new SolidBrush(Color.FromArgb(a, 255, 220, 170)); g.FillRectangle(br, 0, 0, WIN_W, WIN_H); }
    if (t > T_UI) {
      using var layer = new Bitmap((int)WIN_W, (int)WIN_H, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
      using (var lg = Graphics.FromImage(layer)) { lg.SmoothingMode = SmoothingMode.AntiAlias; lg.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias; DrawUI(lg, t); }
      DrawA(g, layer, new RectangleF(0, 0, WIN_W, WIN_H), Clamp01((t - T_UI) / (T_END - T_UI)));
    }
  }

  void DrawUI(Graphics g, float time) {
    // PLAY glow (pulses gently while idle)
    var pr = (RectangleF)play.R; if (play.Down) pr.Offset(0, 2);
    float pulse = 0.5f + 0.5f * MathF.Sin(time * 3.8f);
    int glowA = !play.Enabled ? 0 : play.Hover ? 46 : (int)(22 + 14 * pulse);
    for (int i = 16; i >= 1; i--) using (var gp = Round(RectangleF.Inflate(pr, i * 1.7f, i * 1.7f), 14 + i * 1.7f)) using (var br = new SolidBrush(Color.FromArgb(Math.Max(0, glowA - i * 2), 255, 80, 0))) g.FillPath(br, gp);
    Color c1 = Color.FromArgb(255, 176, 48), c2 = Color.FromArgb(214, 34, 8);
    if (play.Hover) { c1 = Color.FromArgb(255, 206, 90); c2 = Color.FromArgb(240, 60, 12); }
    if (play.Down) { c1 = Color.FromArgb(225, 120, 20); c2 = Color.FromArgb(170, 24, 4); }
    if (!play.Enabled) { c1 = Color.FromArgb(110, 96, 90); c2 = Color.FromArgb(64, 54, 52); }
    using (var gp = Round(pr, 14)) {
      using (var br = new LinearGradientBrush(new RectangleF(pr.X, pr.Y - 1, pr.Width, pr.Height + 2), c1, c2, 90f)) g.FillPath(br, gp);
      var gloss = new RectangleF(pr.X + 3, pr.Y + 3, pr.Width - 6, pr.Height * 0.45f);
      using (var gp2 = Round(gloss, 11)) using (var br = new LinearGradientBrush(new RectangleF(gloss.X, gloss.Y - 1, gloss.Width, gloss.Height + 2), Color.FromArgb(100, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillPath(br, gp2);
      using (var pen = new Pen(Color.FromArgb(220, 255, 214, 140), 1.5f)) g.DrawPath(pen, gp);
    }
    using (var f = MkFont(fBold, 34, FontStyle.Bold)) {
      var label = play.Enabled ? "PLAY" : "LOADING";
      var sz = g.MeasureString(label, f, PointF.Empty, StringFormat.GenericTypographic); float tri = 22, gap = 14, tw = tri + gap + sz.Width;
      float x0 = pr.X + (pr.Width - tw) / 2, cy = pr.Y + pr.Height / 2;
      using var sh = new SolidBrush(Color.FromArgb(110, 90, 10, 0));
      g.FillPolygon(sh, new[] { new PointF(x0 + 1, cy - 12 + 2), new PointF(x0 + tri + 1, cy + 2), new PointF(x0 + 1, cy + 12 + 2) });
      g.FillPolygon(Brushes.White, new[] { new PointF(x0, cy - 12), new PointF(x0 + tri, cy), new PointF(x0, cy + 12) });
      g.DrawString(label, f, sh, x0 + tri + gap + 1, cy - sz.Height / 2 + 2, StringFormat.GenericTypographic);
      g.DrawString(label, f, Brushes.White, x0 + tri + gap, cy - sz.Height / 2, StringFormat.GenericTypographic);
    }
    // secondary pills
    foreach (var b in new[] { sd, set }) {
      var r = (RectangleF)b.R; if (b.Down) r.Offset(0, 1);
      using var gp = Round(r, r.Height / 2);
      using (var br = new SolidBrush(b.Hover && b.Enabled ? Color.FromArgb(190, 230, 70, 0) : Color.FromArgb(170, 14, 6, 8))) g.FillPath(br, gp);
      using (var pen = new Pen(b.Hover ? Color.FromArgb(255, 255, 190, 110) : Color.FromArgb(150, 255, 120, 40), 1.2f)) g.DrawPath(pen, gp);
      using var f = MkFont(fBold, 15, FontStyle.Bold);
      var sz = g.MeasureString(b.Text, f, PointF.Empty, StringFormat.GenericTypographic);
      g.DrawString(b.Text, f, b.Enabled ? Brushes.White : Brushes.Gray, r.X + (r.Width - sz.Width) / 2, r.Y + (r.Height - sz.Height) / 2, StringFormat.GenericTypographic);
    }
    // status + progress + version
    using (var f = MkFont(fBold, 18, FontStyle.Bold)) g.DrawString(status.ToUpperInvariant(), f, Brushes.White, BarR.X, StatusY, StringFormat.GenericTypographic);
    using (var gp = Round(BarR, 4)) using (var br = new SolidBrush(Color.FromArgb(50, 255, 255, 255))) g.FillPath(br, gp);
    if (showBar && progress > 0) {
      var fr = new RectangleF(BarR.X, BarR.Y, Math.Max(8, (float)(BarR.Width * progress)), BarR.Height);
      using (var gp = Round(RectangleF.Inflate(fr, 3, 3), 7)) using (var br = new SolidBrush(Color.FromArgb(60, 255, 90, 0))) g.FillPath(br, gp);
      using (var gp = Round(fr, 4)) using (var br = new LinearGradientBrush(new RectangleF(BarR.X - 1, BarR.Y, BarR.Width + 2, BarR.Height), Color.FromArgb(210, 30, 8), Color.FromArgb(255, 210, 70), 0f)) g.FillPath(br, gp);
    }
    using (var f = MkFont(fSemi, 14, FontStyle.Regular)) using (var br = new SolidBrush(Color.FromArgb(180, 210, 180, 170))) {
      var pv = string.IsNullOrEmpty(s.PackVersion) ? "not installed" : "v" + s.PackVersion;
      g.DrawString($"LAUNCHER v{LauncherVersion}   •   PACK {pv.ToUpperInvariant()}", f, br, BarR.X, VersionY, StringFormat.GenericTypographic);
    }
  }

  async Task ExportSd() {
    try {
      try { SetStatus("Checking for updates..."); await UpdatePack(); } catch (Exception ex) { SetStatus("Update failed: " + ex.Message); }
      if (!Directory.Exists(Settings.PackDir)) { MessageBox.Show("Pack not downloaded yet."); return; }
      using var d = new FolderBrowserDialog { Description = "Choose your Wii SD card (the drive root, e.g. E:\\)" };
      if (d.ShowDialog(this) != DialogResult.OK) return;
      var root = Path.GetPathRoot(d.SelectedPath);
      if (MessageBox.Show($"Copy the pack to {root} ?", "Export to SD", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
      await Task.Run(() => {
        foreach (var f in Directory.GetFiles(Settings.PackDir, "*", SearchOption.AllDirectories)) {
          var dest = Path.Combine(root, Path.GetRelativePath(Settings.PackDir, f));
          Directory.CreateDirectory(Path.GetDirectoryName(dest)); File.Copy(f, dest, true);
        }
      });
      MessageBox.Show("Done! On the Wii: Homebrew Channel > Riivolution > Kartaclysm: Pack = Enabled > Launch.", "Export to SD");
      SetStatus("Exported to " + root);
    } catch (Exception ex) { MessageBox.Show(ex.Message, "Error"); }
  }

  bool AskPaths(bool force) {
    if (force || string.IsNullOrEmpty(s.GamePath) || !File.Exists(s.GamePath)) {
      MessageBox.Show("Select YOUR Mario Kart Wii game file (.iso, .wbfs or .rvz).", "First setup");
      using var d = new OpenFileDialog { Filter = "Wii game (*.iso;*.wbfs;*.rvz;*.ciso;*.gcz)|*.iso;*.wbfs;*.rvz;*.ciso;*.gcz|All files|*.*" };
      if (d.ShowDialog(this) != DialogResult.OK) return false;
      s.GamePath = d.FileName;
    }
    if (force || string.IsNullOrEmpty(s.DolphinPath) || !File.Exists(s.DolphinPath)) {
      var guess = new[] { @"C:\Program Files\Dolphin\Dolphin.exe", @"C:\Program Files\Dolphin-x64\Dolphin.exe" }.FirstOrDefault(File.Exists);
      if (guess != null && !force) s.DolphinPath = guess;
      else {
        MessageBox.Show("Select Dolphin.exe (download it free from dolphin-emu.org if you don't have it).", "First setup");
        using var d = new OpenFileDialog { Filter = "Dolphin.exe|Dolphin.exe|Programs|*.exe" };
        if (d.ShowDialog(this) != DialogResult.OK) return false;
        s.DolphinPath = d.FileName;
      }
    }
    s.Save(); return true;
  }

  async Task OnPlay() {
    play.Enabled = false;
    try {
      if (!AskPaths(false)) { SetStatus("Setup cancelled."); return; }
      Manifest m = null;
      try { SetStatus("Checking for updates..."); m = await UpdatePack(); }
      catch (Exception ex) { SetStatus("Update failed (offline?): " + ex.Message); }
      if (m == null) {
        var cached = Path.Combine(Settings.DataDir, "manifest.json");
        if (!File.Exists(cached)) { MessageBox.Show("Could not download the pack. Check your internet and try again."); return; }
        m = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(cached));
      }
      Launch(m);
    } catch (Exception ex) { MessageBox.Show(ex.ToString(), "Error"); }
    finally { play.Enabled = true; }
  }

  static string Sha(string f) { using var st = File.OpenRead(f); return Convert.ToHexString(SHA256.HashData(st)).ToLowerInvariant(); }

  async Task<Manifest> UpdatePack() {
    var json = await http.GetStringAsync(ManifestUrl + "?t=" + DateTime.UtcNow.Ticks);
    var m = JsonSerializer.Deserialize<Manifest>(json);
    Directory.CreateDirectory(Settings.PackDir);
    var todo = m.files.Where(f => { var p = Local(f.path); return !File.Exists(p) || new FileInfo(p).Length != f.size || Sha(p) != f.sha256; }).ToList();
    long total = Math.Max(1, todo.Sum(f => f.size)), done = 0;
    for (int i = 0; i < todo.Count; i++) {
      var f = todo[i];
      SetStatus($"Downloading {i + 1}/{todo.Count}: {Path.GetFileName(f.path)}");
      var dest = Local(f.path); Directory.CreateDirectory(Path.GetDirectoryName(dest));
      var tmp = dest + ".part";
      using (var resp = await http.GetAsync(f.url, HttpCompletionOption.ResponseHeadersRead)) {
        resp.EnsureSuccessStatusCode();
        using var src = await resp.Content.ReadAsStreamAsync(); using var outp = File.Create(tmp);
        var buf = new byte[81920]; int n;
        while ((n = await src.ReadAsync(buf)) > 0) { await outp.WriteAsync(buf.AsMemory(0, n)); done += n; progress = (double)done / total; showBar = true; Invalidate(); }
      }
      if (Sha(tmp) != f.sha256) { File.Delete(tmp); throw new Exception("Bad download: " + f.path); }
      File.Move(tmp, dest, true);
    }
    // remove files no longer in the pack
    var keep = new HashSet<string>(m.files.Select(f => Path.GetFullPath(Local(f.path))), StringComparer.OrdinalIgnoreCase);
    foreach (var f in Directory.GetFiles(Settings.PackDir, "*", SearchOption.AllDirectories))
      if (!keep.Contains(Path.GetFullPath(f))) File.Delete(f);
    File.WriteAllText(Path.Combine(Settings.DataDir, "manifest.json"), json);
    s.PackVersion = m.version; s.Save(); progress = 1;
    SetStatus(todo.Count == 0 ? $"Pack v{m.version} up to date" : $"Updated to v{m.version}");
    return m;
  }
  static string Local(string rel) => Path.Combine(Settings.PackDir, rel.Replace('/', Path.DirectorySeparatorChar));

  void Launch(Manifest m) {
    var desc = new Dictionary<string, object> {
      ["type"] = "dolphin-game-mod-descriptor", ["version"] = 1, ["base-file"] = s.GamePath,
      ["display-name"] = "Kartaclysm",
      ["riivolution"] = new Dictionary<string, object> { ["patches"] = new[] { new Dictionary<string, object> {
        ["xml"] = Local(m.riivolution_xml), ["root"] = Settings.PackDir,
        ["options"] = m.options.Select(o => new Dictionary<string, object> { ["section-name"] = o.section, ["option-name"] = o.option, ["choice"] = o.choice }).ToArray() } } }
    };
    var jf = Path.Combine(Settings.DataDir, "Kartaclysm.json");
    File.WriteAllText(jf, JsonSerializer.Serialize(desc, new JsonSerializerOptions { WriteIndented = true }));
    SetStatus($"Launching Dolphin (pack v{m.version})...");
    Process.Start(new ProcessStartInfo(s.DolphinPath) { ArgumentList = { "-e", jf }, UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(s.DolphinPath) });
  }
}
}
