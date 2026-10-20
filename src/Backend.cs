using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kartaclysm {
public partial class MainForm {
  const string ManifestUrl = "https://github.com/franxakathegoat-boop/kartaclysm/releases/download/pack/manifest.json";
  static readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(30) };
  string status = "Ready to race";
  double progress = 0; bool showBar = false; bool busy = false;

  void SetStatus(string t) { status = t; Invalidate(); }

  // ---------------- online status ----------------
  public class SvcStatus { public string Name, Host, Url, Desc; public int State = 0; public long Ms; public string Detail = "Not checked yet"; public DateTime When; }
  readonly List<SvcStatus> services = new() {
    new SvcStatus { Name = "WIIMMFI", Host = "wiimmfi.de", Url = "https://wiimmfi.de/", Desc = "Custom-track online server (LE-CODE packs)" },
    new SvcStatus { Name = "RETRO WFC", Host = "rwfc.net", Url = "https://rwfc.net/", Desc = "Retro Rewind's online server" },
    new SvcStatus { Name = "PACK SERVER", Host = "github.com", Url = ManifestUrl, Desc = "Kartaclysm updates (GitHub Releases)" },
  };
  bool checking = false;
  async Task CheckOnline() {
    if (checking) return; checking = true; Invalidate();
    try {
      await Task.WhenAll(services.Select(async sv => {
        sv.State = 0; sv.Detail = "Checking…";
        var sw = Stopwatch.StartNew();
        try {
          var ips = await Dns.GetHostAddressesAsync(sv.Host);
          if (ips.Length == 0) throw new Exception("DNS: no address");
          using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
          using var req = new HttpRequestMessage(HttpMethod.Get, sv.Url);
          using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
          sv.Ms = sw.ElapsedMilliseconds;
          int code = (int)resp.StatusCode;
          if (code < 500) { sv.State = 1; sv.Detail = $"Online · {sv.Ms} ms"; } else { sv.State = 2; sv.Detail = $"Server error {code}"; }
        } catch (System.Net.Sockets.SocketException) { sv.State = 3; sv.Detail = "DNS lookup failed (blocked or offline?)"; }
        catch (Exception ex) { sv.State = 3; sv.Detail = ex is TaskCanceledException ? "Timed out" : "Unreachable: " + ex.GetType().Name.Replace("Exception", ""); }
        sv.When = DateTime.Now; Invalidate();
      }));
    } finally { checking = false; Invalidate(); }
  }

  // ---------------- pack update / play / export ----------------
  async Task ExportSd() {
    if (busy) return; busy = true;
    try {
      try { SetStatus("Checking for updates..."); await UpdatePack(); } catch (Exception ex) { SetStatus("Update failed: " + ex.Message); }
      if (!Directory.Exists(Settings.PackDir)) { MessageBox.Show(this, "Pack not downloaded yet.", "Kartaclysm"); return; }
      using var d = new FolderBrowserDialog { Description = "Choose your Wii SD card (the drive root, e.g. E:\\)" };
      if (d.ShowDialog(this) != DialogResult.OK) return;
      var root = Path.GetPathRoot(d.SelectedPath);
      if (MessageBox.Show(this, $"Copy the Kartaclysm pack to {root} ?", "Export to SD", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
      await Task.Run(() => {
        foreach (var f in Directory.GetFiles(Settings.PackDir, "*", SearchOption.AllDirectories)) {
          var dest = Path.Combine(root, Path.GetRelativePath(Settings.PackDir, f));
          Directory.CreateDirectory(Path.GetDirectoryName(dest)); File.Copy(f, dest, true);
        }
      });
      MessageBox.Show(this, "Done! On the Wii: Homebrew Channel > Riivolution > Kartaclysm: Pack = Enabled > Launch.", "Export to SD");
      SetStatus("Exported to " + root);
    } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error"); }
    finally { busy = false; Invalidate(); }
  }

  bool AskGame() {
    MessageBox.Show(this, "Select YOUR Mario Kart Wii game file (.iso, .wbfs or .rvz).", "Kartaclysm setup");
    using var d = new OpenFileDialog { Filter = "Wii game (*.iso;*.wbfs;*.rvz;*.ciso;*.gcz)|*.iso;*.wbfs;*.rvz;*.ciso;*.gcz|All files|*.*" };
    if (d.ShowDialog(this) != DialogResult.OK) return false;
    s.GamePath = d.FileName; s.Save(); Invalidate(); return true;
  }
  bool AskDolphin() {
    MessageBox.Show(this, "Select Dolphin.exe (download it free from dolphin-emu.org if you don't have it).", "Kartaclysm setup");
    using var d = new OpenFileDialog { Filter = "Dolphin.exe|Dolphin.exe|Programs|*.exe" };
    if (d.ShowDialog(this) != DialogResult.OK) return false;
    s.DolphinPath = d.FileName; s.Save(); Invalidate(); return true;
  }
  bool EnsurePaths() {
    if (string.IsNullOrEmpty(s.GamePath) || !File.Exists(s.GamePath)) { if (!AskGame()) return false; }
    if (string.IsNullOrEmpty(s.DolphinPath) || !File.Exists(s.DolphinPath)) {
      var guess = new[] { @"C:\Program Files\Dolphin\Dolphin.exe", @"C:\Program Files\Dolphin-x64\Dolphin.exe" }.FirstOrDefault(File.Exists);
      if (guess != null) { s.DolphinPath = guess; s.Save(); } else if (!AskDolphin()) return false;
    }
    return true;
  }

  async Task OnPlay() {
    if (s.Mode == "sd") { await ExportSd(); return; }
    if (busy) return; busy = true; Invalidate();
    try {
      if (!EnsurePaths()) { SetStatus("Setup cancelled"); return; }
      Manifest m = null;
      try { SetStatus("Checking for updates..."); m = await UpdatePack(); }
      catch (Exception ex) { SetStatus("Update failed (offline?): " + ex.Message); }
      if (m == null) {
        var cached = Path.Combine(Settings.DataDir, "manifest.json");
        if (!File.Exists(cached)) { MessageBox.Show(this, "Could not download the pack. Check your internet and try again.", "Kartaclysm"); return; }
        m = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(cached));
      }
      Launch(m);
    } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error"); }
    finally { busy = false; Invalidate(); }
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
    var keep = new HashSet<string>(m.files.Select(f => Path.GetFullPath(Local(f.path))), StringComparer.OrdinalIgnoreCase);
    foreach (var f in Directory.GetFiles(Settings.PackDir, "*", SearchOption.AllDirectories))
      if (!keep.Contains(Path.GetFullPath(f))) File.Delete(f);
    File.WriteAllText(Path.Combine(Settings.DataDir, "manifest.json"), json);
    s.PackVersion = m.version; s.Save(); progress = 1; showBar = true;
    SetStatus(todo.Count == 0 ? $"Pack v{m.version} is up to date" : $"Updated to pack v{m.version}");
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
