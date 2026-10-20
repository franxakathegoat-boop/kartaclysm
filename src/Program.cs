using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
  readonly Button play = new() { Text = "PLAY", Font = new Font("Segoe UI Black", 30, FontStyle.Bold), Size = new Size(280, 86), BackColor = Color.FromArgb(255, 106, 0), Cursor = Cursors.Hand, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
  readonly Button settingsBtn = new() { Text = "Settings", Size = new Size(110, 34), BackColor = Color.FromArgb(25, 25, 30), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI Semibold", 10), Cursor = Cursors.Hand };
  readonly Button sdBtn = new() { Text = "Export to SD (Wii)", Size = new Size(160, 34), BackColor = Color.FromArgb(25, 25, 30), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI Semibold", 10), Cursor = Cursors.Hand };
  readonly Label status = new() { AutoSize = false, Height = 30, Dock = DockStyle.Bottom, ForeColor = Color.White, BackColor = Color.FromArgb(14, 10, 18), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 11) };
  readonly ProgressBar bar = new() { Dock = DockStyle.Bottom, Height = 6 };
  Settings s = Settings.Load();
  static readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(30) };

  public MainForm() {
    Text = "Kartaclysm";
    ClientSize = new Size(1000, 620);
    FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
    StartPosition = FormStartPosition.CenterScreen;
    BackColor = Color.Black;
    using (var st = typeof(MainForm).Assembly.GetManifestResourceStream("home-screen.jpg"))
      if (st != null) { BackgroundImage = Image.FromStream(st); BackgroundImageLayout = ImageLayout.Stretch; }
    DoubleBuffered = true;
    play.FlatAppearance.BorderColor = Color.FromArgb(255, 210, 120); play.FlatAppearance.BorderSize = 3; play.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 140, 30);
    foreach (var b in new[] { settingsBtn, sdBtn }) { b.FlatAppearance.BorderColor = Color.FromArgb(255, 106, 0); b.FlatAppearance.BorderSize = 1; }
    Controls.Add(play); Controls.Add(settingsBtn); Controls.Add(sdBtn); Controls.Add(bar); Controls.Add(status);
    Resize += (_, _) => Layout2(); Layout2();
    status.Text = "Ready. Press PLAY.";
    play.Click += async (_, _) => await OnPlay();
    sdBtn.Click += async (_, _) => await ExportSd();
    settingsBtn.Click += (_, _) => { AskPaths(true); };
    http.DefaultRequestHeaders.UserAgent.ParseAdd("Kartaclysm/1.0");
  }
  void Layout2() {
    play.Location = new Point((ClientSize.Width - play.Width) / 2, ClientSize.Height - play.Height - 62);
    settingsBtn.Location = new Point(ClientSize.Width - settingsBtn.Width - 12, ClientSize.Height - settingsBtn.Height - 48);
    sdBtn.Location = new Point(12, ClientSize.Height - sdBtn.Height - 48);
  }

  async Task ExportSd() {
    sdBtn.Enabled = false;
    try {
      try { status.Text = "Checking for updates..."; await UpdatePack(); } catch (Exception ex) { status.Text = "Update failed: " + ex.Message; }
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
      MessageBox.Show("Done! On the Wii: Homebrew Channel > Riivolution > Kartaclysm: Enabled > Launch.", "Export to SD");
      status.Text = "Exported to " + root;
    } catch (Exception ex) { MessageBox.Show(ex.Message, "Error"); }
    finally { sdBtn.Enabled = true; }
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
      if (!AskPaths(false)) { status.Text = "Setup cancelled."; return; }
      Manifest m = null;
      try { status.Text = "Checking for updates..."; m = await UpdatePack(); }
      catch (Exception ex) { status.Text = "Update failed (offline?): " + ex.Message; }
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
      status.Text = $"Downloading {i + 1}/{todo.Count}: {Path.GetFileName(f.path)}";
      var dest = Local(f.path); Directory.CreateDirectory(Path.GetDirectoryName(dest));
      var tmp = dest + ".part";
      using (var resp = await http.GetAsync(f.url, HttpCompletionOption.ResponseHeadersRead)) {
        resp.EnsureSuccessStatusCode();
        using var src = await resp.Content.ReadAsStreamAsync(); using var outp = File.Create(tmp);
        var buf = new byte[81920]; int n;
        while ((n = await src.ReadAsync(buf)) > 0) { await outp.WriteAsync(buf.AsMemory(0, n)); done += n; bar.Value = (int)Math.Min(100, done * 100 / total); }
      }
      if (Sha(tmp) != f.sha256) { File.Delete(tmp); throw new Exception("Bad download: " + f.path); }
      File.Move(tmp, dest, true);
    }
    // remove files no longer in the pack
    var keep = new HashSet<string>(m.files.Select(f => Path.GetFullPath(Local(f.path))), StringComparer.OrdinalIgnoreCase);
    foreach (var f in Directory.GetFiles(Settings.PackDir, "*", SearchOption.AllDirectories))
      if (!keep.Contains(Path.GetFullPath(f))) File.Delete(f);
    File.WriteAllText(Path.Combine(Settings.DataDir, "manifest.json"), json);
    s.PackVersion = m.version; s.Save(); bar.Value = 100;
    status.Text = todo.Count == 0 ? $"Pack v{m.version} up to date." : $"Updated to v{m.version}.";
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
    status.Text = $"Launching Dolphin (pack v{m.version})...";
    Process.Start(new ProcessStartInfo(s.DolphinPath) { ArgumentList = { "-e", jf }, UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(s.DolphinPath) });
  }
}
}
