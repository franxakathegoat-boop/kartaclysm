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
  const string LauncherVersion = "1.2.0";
  // ---- layout (mirrored exactly by scripts/render_preview.py) ----
  static readonly Rectangle PlayR = new(712, 524, 262, 66);
  static readonly Rectangle SdR = new(718, 20, 140, 36);
  static readonly Rectangle SetR = new(868, 20, 106, 36);
  static readonly Rectangle BarR = new(28, 568, 600, 8);
  const int StatusY = 538, VersionY = 586;
  class Btn { public Rectangle R; public string Text; public bool Hover, Down, Enabled = true; public Func<Task> Click; }
  readonly Btn play = new() { R = PlayR, Text = "PLAY" }, sd = new() { R = SdR, Text = "EXPORT TO SD" }, set = new() { R = SetR, Text = "SETTINGS" };
  readonly List<Btn> btns;
  string status = "Ready to race";
  double progress = 0; bool showBar = false;
  readonly Image bg;
  readonly System.Drawing.Text.PrivateFontCollection pfc = new();
  FontFamily fBold, fSemi;
  Settings s = Settings.Load();
  static readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(30) };

  public MainForm() {
    Text = "Kartaclysm";
    try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
    ClientSize = new Size(1000, 620);
    FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
    StartPosition = FormStartPosition.CenterScreen; BackColor = Color.Black;
    SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    var asm = typeof(MainForm).Assembly;
    using (var st = asm.GetManifestResourceStream("home-screen.jpg")) bg = Image.FromStream(st);
    foreach (var n in new[] { "bold.ttf", "semi.ttf" }) {
      var tmp = Path.Combine(Path.GetTempPath(), "kartaclysm-" + n);
      using (var st = asm.GetManifestResourceStream(n)) using (var o = File.Create(tmp)) st.CopyTo(o);
      pfc.AddFontFile(tmp);
    }
    fBold = pfc.Families.FirstOrDefault(f => f.IsStyleAvailable(FontStyle.Bold)) ?? pfc.Families[0];
    fSemi = pfc.Families.FirstOrDefault(f => f.Name.Contains("SemiBold")) ?? fBold;
    play.Click = OnPlay; sd.Click = ExportSd; set.Click = () => { AskPaths(true); return Task.CompletedTask; };
    btns = new() { play, sd, set };
    http.DefaultRequestHeaders.UserAgent.ParseAdd("Kartaclysm/" + LauncherVersion);
    MouseMove += (_, e) => { bool ch = false; foreach (var b in btns) { var h = b.R.Contains(e.Location); if (h != b.Hover) { b.Hover = h; ch = true; } } Cursor = btns.Any(b => b.Hover && b.Enabled) ? Cursors.Hand : Cursors.Default; if (ch) Invalidate(); };
    MouseLeave += (_, _) => { foreach (var b in btns) { b.Hover = b.Down = false; } Invalidate(); };
    MouseDown += (_, e) => { foreach (var b in btns) if (b.Enabled && b.R.Contains(e.Location)) b.Down = true; Invalidate(); };
    MouseUp += async (_, e) => {
      var hit = btns.FirstOrDefault(b => b.Down && b.R.Contains(e.Location));
      foreach (var b in btns) b.Down = false; Invalidate();
      if (hit != null && hit.Enabled) { foreach (var b in btns) b.Enabled = false; Invalidate(); try { await hit.Click(); } finally { foreach (var b in btns) b.Enabled = true; Invalidate(); } }
    };
  }
  void SetStatus(string t) { status = t; Invalidate(); }

  static GraphicsPath Round(RectangleF r, float rad) {
    var p = new GraphicsPath(); float d = rad * 2;
    p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
    p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
  }

  protected override void OnPaint(PaintEventArgs e) {
    var g = e.Graphics;
    g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.DrawImage(bg, 0, 0, 1000, 620);
    // PLAY glow
    var pr = (RectangleF)play.R; if (play.Down) pr.Offset(0, 2);
    int glowA = !play.Enabled ? 0 : play.Hover ? 44 : 28;
    for (int i = 14; i >= 1; i--) using (var gp = Round(RectangleF.Inflate(pr, i * 1.6f, i * 1.6f), 14 + i * 1.6f)) using (var br = new SolidBrush(Color.FromArgb(Math.Max(0, glowA - i * 2), 255, 110, 0))) g.FillPath(br, gp);
    Color c1 = Color.FromArgb(255, 196, 60), c2 = Color.FromArgb(255, 92, 0);
    if (play.Hover) { c1 = Color.FromArgb(255, 216, 100); c2 = Color.FromArgb(255, 120, 10); }
    if (play.Down) { c1 = Color.FromArgb(235, 140, 20); c2 = Color.FromArgb(205, 60, 0); }
    if (!play.Enabled) { c1 = Color.FromArgb(120, 110, 100); c2 = Color.FromArgb(80, 72, 66); }
    using (var gp = Round(pr, 14)) {
      using (var br = new LinearGradientBrush(pr, c1, c2, 90f)) g.FillPath(br, gp);
      var gloss = new RectangleF(pr.X + 3, pr.Y + 3, pr.Width - 6, pr.Height * 0.45f);
      using (var gp2 = Round(gloss, 11)) using (var br = new LinearGradientBrush(gloss, Color.FromArgb(90, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillPath(br, gp2);
      using (var pen = new Pen(Color.FromArgb(200, 255, 236, 180), 1.5f)) g.DrawPath(pen, gp);
    }
    using (var f = new Font(fBold, 34, FontStyle.Bold, GraphicsUnit.Pixel)) {
      var label = play.Enabled ? "PLAY" : "LOADING";
      var sz = g.MeasureString(label, f); float tri = 22, gap = 14, tw = tri + gap + sz.Width;
      float x0 = pr.X + (pr.Width - tw) / 2, cy = pr.Y + pr.Height / 2;
      using var sh = new SolidBrush(Color.FromArgb(90, 80, 20, 0));
      g.FillPolygon(sh, new[] { new PointF(x0 + 1, cy - 12 + 2), new PointF(x0 + tri + 1, cy + 2), new PointF(x0 + 1, cy + 12 + 2) });
      g.FillPolygon(Brushes.White, new[] { new PointF(x0, cy - 12), new PointF(x0 + tri, cy), new PointF(x0, cy + 12) });
      g.DrawString(label, f, sh, x0 + tri + gap + 1, cy - sz.Height / 2 + 2);
      g.DrawString(label, f, Brushes.White, x0 + tri + gap, cy - sz.Height / 2);
    }
    // secondary pills
    foreach (var b in new[] { sd, set }) {
      var r = (RectangleF)b.R; if (b.Down) r.Offset(0, 1);
      using var gp = Round(r, r.Height / 2);
      using (var br = new SolidBrush(b.Hover && b.Enabled ? Color.FromArgb(150, 255, 100, 0) : Color.FromArgb(150, 12, 10, 20))) g.FillPath(br, gp);
      using (var pen = new Pen(b.Hover ? Color.FromArgb(255, 255, 170, 80) : Color.FromArgb(90, 255, 255, 255), 1.2f)) g.DrawPath(pen, gp);
      using var f = new Font(fBold, 15, FontStyle.Bold, GraphicsUnit.Pixel);
      using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
      g.DrawString(b.Text, f, b.Enabled ? Brushes.White : Brushes.Gray, r, sf);
    }
    // status + progress
    using (var f = new Font(fBold, 18, FontStyle.Bold, GraphicsUnit.Pixel)) g.DrawString(status.ToUpperInvariant(), f, Brushes.White, BarR.X - 3, StatusY);
    using (var gp = Round(BarR, 4)) using (var br = new SolidBrush(Color.FromArgb(55, 255, 255, 255))) g.FillPath(br, gp);
    if (showBar && progress > 0) {
      var fr = new RectangleF(BarR.X, BarR.Y, Math.Max(8, (float)(BarR.Width * progress)), BarR.Height);
      using (var gp = Round(RectangleF.Inflate(fr, 3, 3), 7)) using (var br = new SolidBrush(Color.FromArgb(50, 255, 120, 0))) g.FillPath(br, gp);
      using (var gp = Round(fr, 4)) using (var br = new LinearGradientBrush(new RectangleF(BarR.X, BarR.Y, BarR.Width, BarR.Height), Color.FromArgb(255, 70, 0), Color.FromArgb(255, 210, 60), 0f)) g.FillPath(br, gp);
    }
    using (var f = new Font(fSemi, 14, FontStyle.Regular, GraphicsUnit.Pixel)) using (var br = new SolidBrush(Color.FromArgb(170, 190, 185, 205))) {
      var pv = string.IsNullOrEmpty(s.PackVersion) ? "not installed" : "v" + s.PackVersion;
      g.DrawString($"LAUNCHER v{LauncherVersion}   •   PACK {pv.ToUpperInvariant()}", f, br, BarR.X - 2, VersionY);
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
