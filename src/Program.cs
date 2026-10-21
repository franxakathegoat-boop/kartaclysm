using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace Kartaclysm {
static class Program {
  [STAThread] static void Main(string[] args) {
    ApplicationConfiguration.Initialize();
    // Preview renderer: Kartaclysm.exe --render <outDir> <home|tracks|updates|online|settings|intro>
    if (args.Length >= 3 && args[0] == "--render") { MainForm.RenderOffscreen(args[1], args[2]); return; }
    Application.Run(new MainForm());
  }
}

public class Settings {
  public string GamePath { get; set; }
  public string DolphinPath { get; set; }
  public string PackVersion { get; set; }
  public string Mode { get; set; } = "dolphin";   // "dolphin", "native" (WiiCompiled) or "sd"
  public bool Intro { get; set; } = true;
  public bool Music { get; set; } = true;
  public bool CertGuideShown { get; set; }
  public string PreparedFor { get; set; }
  public string WiiCompiledPath { get; set; }
  static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kartaclysm");
  public static string PackDir => Path.Combine(Dir, "pack");
  public static string DataDir => Dir;
  public static string BackgroundsDir => Path.Combine(Dir, "backgrounds");
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
  public string prepare { get; set; }
  public int wiimmfi_region { get; set; }
  public string wfc_domain { get; set; }   // Kartaclysm's own online server; empty = Wiimmfi
  public string min_launcher { get; set; }
}
public class RiiOption { public string section { get; set; } public string option { get; set; } public int choice { get; set; } }

public class ShotInfo { public string file { get; set; } public string track { get; set; } public string author { get; set; } public string source { get; set; } public string uploader { get; set; } public string page { get; set; } }
public class TrackInfo { public string name { get; set; } public string authors { get; set; } public int? shot { get; set; } }
public class CupInfo { public int n { get; set; } public string name { get; set; } public string series { get; set; } public List<TrackInfo> tracks { get; set; } }
public class AppData { public List<ShotInfo> shots { get; set; } public List<CupInfo> cups { get; set; } }
}
