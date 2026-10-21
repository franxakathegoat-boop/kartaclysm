using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Kartaclysm {
// Wiimmfi in Dolphin needs the Wii SSL certificates (clientca.pem, clientcakey.pem, rootca.pem) in Dolphin's Wii folder.
// Nintendo's update servers are gone, so the working way is Dolphin's offline system update from the player's own disc.
public partial class MainForm {
  static readonly string[] CertFiles = { "clientca.pem", "clientcakey.pem", "rootca.pem" };

  string DolphinUserDir() {
    var env = Environment.GetEnvironmentVariable("DOLPHIN_EMU_USERPATH");
    if (!string.IsNullOrEmpty(env) && Directory.Exists(env)) return env;
    if (!string.IsNullOrEmpty(s.DolphinPath)) {
      var exeDir = Path.GetDirectoryName(s.DolphinPath);
      if (File.Exists(Path.Combine(exeDir, "portable.txt"))) return Path.Combine(exeDir, "User");
    }
    var app = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dolphin Emulator");
    var docs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Dolphin Emulator");
    return Directory.Exists(app) || !Directory.Exists(docs) ? app : docs;
  }

  // read-only check
  bool HasWiiCerts() {
    try { var w = Path.Combine(DolphinUserDir(), "Wii"); return CertFiles.All(f => File.Exists(Path.Combine(w, f))); } catch { return false; }
  }

  const string CertGuideEn = "Online needs a one-time Dolphin setup: the Wii network certificates are missing.\n\n" +
    "1. Click \"Open Dolphin\" below (Kartaclysm adds your Mario Kart Wii folder to Dolphin's game list).\n" +
    "2. In Dolphin, right-click Mario Kart Wii → Perform System Update.\n" +
    "3. Wait until it finishes, then close Dolphin.\n\n" +
    "It installs the system files from your own game disc - no download from Nintendo needed.\n" +
    "First time on Wiimmfi: error 23899 \"Console registered, please wait 7 days\" is normal. Online opens automatically after the 7 days.";
  const string CertGuideIt = "Per l'online serve una configurazione di Dolphin da fare una volta: mancano i certificati di rete Wii.\n\n" +
    "1. Clicca \"Apri Dolphin\" qui sotto (Kartaclysm aggiunge la cartella di Mario Kart Wii alla lista giochi di Dolphin).\n" +
    "2. In Dolphin, clic destro su Mario Kart Wii → Avvia l'aggiornamento di sistema.\n" +
    "3. Aspetta che finisca, poi chiudi Dolphin.\n\n" +
    "Installa i file di sistema dal tuo disco del gioco, senza scaricare nulla da Nintendo.\n" +
    "Primo accesso a Wiimmfi: l'errore 23899 \"Console registrata, attendi 7 giorni\" è normale. L'online si sblocca da solo dopo 7 giorni.";

  // returns true when the player wants to continue to the game
  bool ShowCertGuide(bool beforePlay) {
    var msg = CertGuideEn + "\n\n— — —\n\n" + CertGuideIt + "\n\n" +
      (beforePlay ? "Yes = Open Dolphin for the setup / Apri Dolphin\nNo = Play offline now / Gioca offline ora"
                  : "Yes = Open Dolphin for the setup / Apri Dolphin\nNo = Later / Più tardi");
    var r = MessageBox.Show(this, msg, "Kartaclysm - Online setup / Configurazione online",
      beforePlay ? MessageBoxButtons.YesNoCancel : MessageBoxButtons.YesNo, MessageBoxIcon.Information);
    if (r == DialogResult.Yes) { OpenDolphinForSystemUpdate(); return false; }
    return r == DialogResult.No && beforePlay;
  }

  void OpenDolphinForSystemUpdate() {
    if (!EnsurePaths()) return;
    try { AddGameFolderToDolphin(Path.GetDirectoryName(s.GamePath)); } catch { /* the player can still use File > Open */ }
    StopMusicForGame();
    Process.Start(new ProcessStartInfo(s.DolphinPath) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(s.DolphinPath) });
    SetStatus("In Dolphin: right-click Mario Kart Wii → Perform System Update");
  }

  // adds the folder with the player's original game (it has the update partition) to Dolphin's game list, once
  void AddGameFolderToDolphin(string folder) {
    var ini = Path.Combine(DolphinUserDir(), "Config", "Dolphin.ini");
    Directory.CreateDirectory(Path.GetDirectoryName(ini));
    var lines = File.Exists(ini) ? File.ReadAllLines(ini).ToList() : new System.Collections.Generic.List<string>();
    string norm(string p) => p.Replace('\\', '/').TrimEnd('/');
    var want = norm(folder);
    int gi = lines.FindIndex(l => l.Trim() == "[General]");
    if (gi < 0) { lines.Add("[General]"); gi = lines.Count - 1; }
    int end = lines.FindIndex(gi + 1, l => l.TrimStart().StartsWith("[")); if (end < 0) end = lines.Count;
    int count = 0, countLine = -1;
    for (int i = gi + 1; i < end; i++) {
      var kv = lines[i].Split('=', 2); if (kv.Length < 2) continue;
      var k = kv[0].Trim(); var v = kv[1].Trim();
      if (k.StartsWith("ISOPath") && k != "ISOPaths" && string.Equals(norm(v), want, StringComparison.OrdinalIgnoreCase)) return;
      if (k == "ISOPaths") { int.TryParse(v, out count); countLine = i; }
    }
    lines.Insert(gi + 1, $"ISOPath{count} = {want}");
    if (countLine >= 0) lines[countLine + 1] = $"ISOPaths = {count + 1}"; else lines.Insert(gi + 2, $"ISOPaths = {count + 1}");
    File.WriteAllLines(ini, lines);
  }
}
}
