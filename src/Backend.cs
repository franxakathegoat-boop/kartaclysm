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
  public class SvcStatus { public string Name, Host, Desc; public int Port; public int State = 0; public long Ms; public string Detail = "Not checked yet"; public DateTime When; }
  readonly List<SvcStatus> services = new() {
    new SvcStatus { Name = "WIIMMFI LOGIN", Host = "naswii.wiimmfi.de", Port = 443, Desc = "Wiimmfi sign-in server (nas)" },
    new SvcStatus { Name = "WIIMMFI ROOMS", Host = "gpcm.gs.wiimmfi.de", Port = 29900, Desc = "Wiimmfi matchmaking (gpcm)" },
    new SvcStatus { Name = "PACK SERVER", Host = "github.com", Port = 443, Desc = "Kartaclysm updates (GitHub Releases)" },
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
          if (ips.Length == 0) throw new System.Net.Sockets.SocketException();
          using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
          using var tcp = new System.Net.Sockets.TcpClient();
          await tcp.ConnectAsync(ips, sv.Port, cts.Token);
          sv.Ms = sw.ElapsedMilliseconds; sv.State = 1; sv.Detail = $"Reachable · {sv.Ms} ms";
        } catch (OperationCanceledException) { sv.State = 3; sv.Detail = "Timed out (port " + sv.Port + " blocked?)"; }
        catch (System.Net.Sockets.SocketException ex) { sv.State = 3; sv.Detail = ex.SocketErrorCode == System.Net.Sockets.SocketError.HostNotFound ? "DNS lookup failed" : "Connection refused / blocked"; }
        catch (Exception ex) { sv.State = 3; sv.Detail = "Unreachable: " + ex.GetType().Name.Replace("Exception", ""); }
        sv.When = DateTime.Now; Invalidate();
      }));
    } finally { checking = false; Invalidate(); }
  }
  int WiimmfiRegion => lastManifest?.wiimmfi_region ?? 0;
  Manifest lastManifest = LoadCachedManifest();
  static Manifest LoadCachedManifest() { try { return JsonSerializer.Deserialize<Manifest>(File.ReadAllText(Path.Combine(Settings.DataDir, "manifest.json"))); } catch { return null; } }

  // ---------------- first-run build (runs prepare.ps1 from the pack on this PC) ----------------
  static string GenDir => Path.Combine(Settings.DataDir, "gen");
  static string GameImage => Path.Combine(GenDir, "Kartaclysm.wbfs");
  bool NeedsPrepare(Manifest m) {
    if (string.IsNullOrEmpty(m.prepare)) return false;
    try {
      var st = JsonDocument.Parse(File.ReadAllText(Path.Combine(GenDir, "stamp.json"))).RootElement;
      return st.GetProperty("pack").GetString() != m.version || st.GetProperty("region").GetInt32() != m.wiimmfi_region
        || !File.Exists(GameImage) || s.PreparedFor != s.GamePath;
    } catch { return true; }
  }
  async Task Prepare(Manifest m) {
    SetStatus("First-time setup: building Kartaclysm from your game (a few minutes)...");
    await RunScript(Local(m.prepare), new[] { "-Game", s.GamePath, "-Root", Settings.DataDir, "-Region", m.wiimmfi_region.ToString() }, "prepare-error.log");
    if (!File.Exists(GameImage)) throw new Exception("Setup finished but the Kartaclysm game file is missing.");
    s.PreparedFor = s.GamePath; s.Save();
  }
  // runs a pack script; "PROGRESS <pct> <text>" lines drive the bar, everything is logged, errors are shown
  async Task<List<string>> RunScript(string script, string[] args, string errLog) {
    var psi = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var a in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script }.Concat(args)) psi.ArgumentList.Add(a);
    var err = new System.Text.StringBuilder(); var outl = new List<string>();
    using var p = Process.Start(psi);
    p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (err) err.AppendLine(e.Data); };
    p.BeginErrorReadLine();
    string line;
    while ((line = await p.StandardOutput.ReadLineAsync()) != null) {
      outl.Add(line);
      if (line.StartsWith("PROGRESS ")) {
        var parts = line.Split(' ', 3);
        if (int.TryParse(parts[1], out var pc)) { progress = pc / 100.0; showBar = true; }
        SetStatus("Setup: " + (parts.Length > 2 ? parts[2] : ""));
      }
    }
    await p.WaitForExitAsync();
    if (p.ExitCode != 0) {
      var path = Path.Combine(Settings.DataDir, errLog);
      File.WriteAllText(path, string.Join("\n", outl) + "\n--- errors ---\n" + err);
      throw new Exception("Setup failed. Details: " + path + "\n" + string.Join("\n", err.ToString().Split('\n').Take(8)));
    }
    return outl;
  }
  async Task<Manifest> UpdateAndPrepare() {
    Manifest m = null;
    try { SetStatus("Checking for updates..."); m = await UpdatePack(); }
    catch (Exception ex) { SetStatus("Update failed (offline?): " + ex.Message); }
    m ??= LoadCachedManifest();
    if (m == null) throw new Exception("Could not download the pack. Check your internet and try again.");
    lastManifest = m;
    if (NeedsPrepare(m)) await Prepare(m);
    return m;
  }

  // ---------------- pack update / play / export ----------------
  async Task ExportSd() {
    if (busy) return; busy = true; Invalidate();
    try {
      if (string.IsNullOrEmpty(s.GamePath) || !File.Exists(s.GamePath)) { if (!AskGame()) return; }
      await UpdateAndPrepare();
      using var d = new FolderBrowserDialog { Description = "Choose the USB drive or SD card your USB loader uses (the drive root, e.g. E:\\)" };
      if (d.ShowDialog(this) != DialogResult.OK) return;
      var root = Path.GetPathRoot(d.SelectedPath);
      var dest = Path.Combine(root, "wbfs", "Kartaclysm [RMCPKC]", "RMCPKC.wbfs");
      if (MessageBox.Show(this, $"Copy Kartaclysm (about {new FileInfo(GameImage).Length >> 30} GB) to\n{dest} ?", "Copy to USB / SD", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
      Directory.CreateDirectory(Path.GetDirectoryName(dest));
      SetStatus("Copying the game file...");
      await Task.Run(() => {
        using var src = File.OpenRead(GameImage); using var outp = File.Create(dest + ".part");
        var buf = new byte[1 << 20]; int n; long done = 0, total = src.Length;
        while ((n = src.Read(buf, 0, buf.Length)) > 0) { outp.Write(buf, 0, n); done += n; progress = (double)done / total; showBar = true; BeginInvoke((Action)Invalidate); }
      });
      File.Move(dest + ".part", dest, true);
      MessageBox.Show(this, "Done! Put the drive in your Wii and start Kartaclysm from your USB loader (USB Loader GX, WiiFlow...).", "Copy to USB / SD");
      SetStatus("Copied to " + root);
    } catch (Exception ex) { SetStatus("Copy failed"); MessageBox.Show(this, ex.Message, "Error"); }
    finally { busy = false; showBar = false; Invalidate(); }
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
    if (s.Mode == "native") { await PlayNative(); return; }
    if (busy) return; busy = true; Invalidate();
    try {
      if (!EnsurePaths()) { SetStatus("Setup cancelled"); return; }
      var m = await UpdateAndPrepare();
      // Wiimmfi needs Dolphin's Wii certificates: guide the player once (offline play still works without them)
      if (!HasWiiCerts() && !s.CertGuideShown) { s.CertGuideShown = true; s.Save(); if (!ShowCertGuide(true)) { SetStatus("Online setup: finish it in Dolphin, then press PLAY"); return; } }
      Launch(m);
    } catch (Exception ex) { SetStatus("Setup failed - see the message"); showBar = false; MessageBox.Show(this, ex.Message, "Kartaclysm"); }
    finally { busy = false; Invalidate(); }
  }

  // ---------------- native mode (WiiCompiled Kartaclysm build) ----------------
  async Task PlayNative() {
    if (busy) return; busy = true; Invalidate();
    try {
      if (string.IsNullOrEmpty(s.GamePath) || !File.Exists(s.GamePath)) { if (!AskGame()) { SetStatus("Setup cancelled"); return; } }
      var m = await UpdateAndPrepare();
      var native = Local("build/native-setup.ps1");
      if (!File.Exists(native)) throw new Exception("This pack version has no native mode yet. Press PLAY again after the next update.");
      SetStatus("Native setup: preparing WiiCompiled (first time takes about an hour)...");
      var outl = await RunScript(native, new[] { "-Game", s.GamePath, "-Root", Settings.DataDir }, "native-error.log");
      var exe = outl.LastOrDefault(l => l.StartsWith("EXE "))?.Substring(4).Trim();
      if (exe == null || !File.Exists(exe)) throw new Exception("Native build finished but the game exe was not found.");
      s.WiiCompiledPath = exe; s.Save();
      SetStatus($"Launching Kartaclysm natively (pack v{m.version})...");
      StopMusicForGame(); Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) });
      showBar = false;
    } catch (Exception ex) { SetStatus("Native setup failed - see the message"); showBar = false; MessageBox.Show(this, ex.Message, "Kartaclysm"); }
    finally { busy = false; Invalidate(); }
  }

  // ---------------- launcher music (original Kartaclysm theme, CC0) ----------------
  System.Media.SoundPlayer music;
  void UpdateMusic() {
    try {
      if (s.Music && fixedTime == null) { if (music == null) { music = new System.Media.SoundPlayer(GetType().Assembly.GetManifestResourceStream("music.wav")); music.Load(); } music.PlayLooping(); }
      else music?.Stop();
    } catch { }
  }
  void StopMusicForGame() { try { music?.Stop(); } catch { } }

  // ---------------- launcher self-update (launcher-vX.Y.Z GitHub releases) ----------------
  async Task SelfUpdate() {
    try {
      var exe = Environment.ProcessPath; if (exe == null) return;
      try { File.Delete(exe + ".old"); } catch { }
      // launchers before 1.5 left a Riivolution descriptor that boots the plain game; never use it
      try { File.Delete(Path.Combine(Settings.DataDir, "Kartaclysm.json")); } catch { }
      using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/franxakathegoat-boop/kartaclysm/releases?per_page=30");
      req.Headers.UserAgent.ParseAdd("Kartaclysm-Launcher/" + LauncherVersion);
      using var resp = await http.SendAsync(req); if (!resp.IsSuccessStatusCode) return;
      using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
      Version cur = Version.Parse(LauncherVersion), best = cur; string url = null;
      foreach (var r in doc.RootElement.EnumerateArray()) {
        var tag = r.GetProperty("tag_name").GetString() ?? "";
        if (!tag.StartsWith("launcher-v") || r.GetProperty("draft").GetBoolean()) continue;
        if (!Version.TryParse(tag.Substring(10), out var v) || v <= best) continue;
        foreach (var a in r.GetProperty("assets").EnumerateArray())
          if (a.GetProperty("name").GetString() == "Kartaclysm.exe") { best = v; url = a.GetProperty("browser_download_url").GetString(); }
      }
      if (url == null) return;
      SetStatus($"Updating the launcher to v{best}...");
      var tmp = exe + ".new";
      using (var src = await http.GetStreamAsync(url)) using (var outp = File.Create(tmp)) await src.CopyToAsync(outp);
      if (new FileInfo(tmp).Length < 1_000_000) { File.Delete(tmp); return; }
      File.Move(exe, exe + ".old", true); File.Move(tmp, exe, true);
      StopMusicForGame(); Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) });
      Application.Exit();
    } catch (Exception ex) { SetStatus("Launcher update check failed: " + ex.Message); }
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
    // the playable game is the image prepare.ps1 built from the player's own disc (LE-CODE needs a patched main.dol)
    if (!File.Exists(GameImage)) throw new Exception("The Kartaclysm game file is missing. Press PLAY again to rebuild it.");
    SetStatus($"Launching Dolphin (pack v{m.version})...");
    var psi = new ProcessStartInfo(s.DolphinPath) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(s.DolphinPath) };
    // smoother play on weaker PCs: compile shaders in the background and pre-build them before the game starts
    foreach (var a in new[] { "-C", "GFX.Settings.ShaderCompilationMode=2", "-C", "GFX.Settings.WaitForShadersBeforeStarting=True", "-e", GameImage }) psi.ArgumentList.Add(a);
    StopMusicForGame(); Process.Start(psi);
    showBar = false;
  }
}
}
