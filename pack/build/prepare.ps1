# Kartaclysm first-run builder. Runs on the player's PC and builds every file that is derived
# from the player's own Mario Kart Wii copy. Nothing from the game is ever uploaded or shipped.
param([Parameter(Mandatory)][string]$Game, [string]$Root = "$env:APPDATA\Kartaclysm", [int]$Region = 0)
$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = 'Tls12'
function Say($p, $m) { Write-Output ("PROGRESS {0} {1}" -f $p, $m) }
$pack = "$Root\pack"; $gen = "$Root\gen"; $tools = "$Root\tools"; $work = "$Root\work"
$szsZip = 'https://szs.wiimm.de/download/szs-v2.42a-r8989-cygwin64.zip'
$witZip = 'https://wit.wiimm.de/download/wit-v3.05a-r8638-cygwin64.zip'
New-Item -ItemType Directory -Force $tools, $work | Out-Null

# 1. Wiimm's tools (free, GPL) - downloaded from their official site
foreach ($z in $szsZip, $witZip) {
  $name = [IO.Path]::GetFileNameWithoutExtension($z)
  if (-not (Test-Path "$tools\$name\bin")) {
    Say 2 "Downloading Wiimm's tools"
    Invoke-WebRequest $z -OutFile "$tools\$name.zip" -UseBasicParsing
    Expand-Archive "$tools\$name.zip" -DestinationPath $tools -Force; Remove-Item "$tools\$name.zip"
  }
}
$szsBin = (Get-ChildItem $tools -Directory -Filter 'szs-*')[0].FullName + '\bin'
$witBin = (Get-ChildItem $tools -Directory -Filter 'wit-*')[0].FullName + '\bin'
$env:PATH = "$szsBin;$witBin;$env:PATH"
function Run($exe, [string[]]$a) { $o = & $exe @a 2>&1; if ($LASTEXITCODE -gt 1) { throw "$exe failed ($LASTEXITCODE): $($o | Select-Object -Last 5 | Out-String)" }; $o }

# 2. Extract only what we need from the player's game
Say 8 'Reading your Mario Kart Wii file'
$x = "$work\game"; if (Test-Path $x) { Remove-Item $x -Recurse -Force }
Run wit.exe @('extract', $Game, $x, '--psel', 'data', '-o', '-q') | Out-Null
$id = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes("$x\sys\boot.bin")[0..5])
if ($id.Substring(0,3) -ne 'RMC') { throw "This is not Mario Kart Wii (game ID $id)" }
$reg = @{ P = 'PAL'; E = 'USA'; J = 'JAP'; K = 'KOR' }[$id.Substring(3,1)]
if (-not $reg) { throw "Unknown region in game ID $id" }

if (Test-Path $gen) { Remove-Item $gen -Recurse -Force }
New-Item -ItemType Directory -Force "$gen\Race\Course", "$gen\Scene\UI", "$gen\rel", "$gen\riivolution" | Out-Null

# 3. Original 32 tracks + 10 arenas + 5 special files, renamed to LE-CODE slot names
Say 14 'Setting up the original tracks'
$orig = 'castle_course farm_course kinoko_course volcano_course factory_course shopping_course boardcross_course truck_course beginner_course senior_course ridgehighway_course treehouse_course koopa_course rainbow_course desert_course water_course old_peach_gc old_mario_gc old_waluigi_gc old_donkey_gc old_falls_ds old_desert_ds old_garden_ds old_town_ds old_mario_sfc old_obake_sfc old_mario_64 old_sherbet_64 old_koopa_64 old_donkey_64 old_koopa_gba old_heyho_gba venice_battle block_battle casino_battle skate_battle sand_battle old_CookieLand_gc old_House_ds old_battle4_sfc old_battle3_gba old_matenro_64'.Split(' ')
$special = @{ 0x36 = 'ring_mission'; 0x37 = 'winningrun_demo'; 0x38 = 'loser_demo'; 0x39 = 'draw_demo'; 0x3a = 'ending_demo' }
$map = @{}; for ($i = 0; $i -lt $orig.Count; $i++) { $map[$i] = $orig[$i] }; foreach ($k in $special.Keys) { $map[$k] = $special[$k] }
$rc = "$x\files\Race\Course"
foreach ($k in $map.Keys) {
  foreach ($sfx in '', '_d') {
    $src = "$rc\$($map[$k])$sfx.szs"; if (Test-Path $src) { Copy-Item $src ("$gen\Race\Course\{0:x3}{1}.szs" -f $k, $sfx) -Force }
  }
}

# 4. Custom tracks hosted in Wiimm's CT archive: download WBZ and rebuild SZS with the player's own game files
Run wszst.exe @('autoadd', "$x\files\Race\Course", '-q') | Out-Null
foreach ($f in Get-ChildItem "$gen\Race\Course" -Filter *.szs) { Run wszst.exe @('autoadd', $f.FullName, '-q') | Out-Null }
$list = Get-Content "$pack\build\tracks.json" -Raw | ConvertFrom-Json
$cache = "$Root\cache"; New-Item -ItemType Directory -Force $cache | Out-Null
$ct = @($list | Where-Object { $_.ct })
Add-Type -ReferencedAssemblies System.Net.Http -TypeDefinition @'
using System; using System.IO; using System.Linq; using System.Net.Http; using System.Threading; using System.Threading.Tasks;
public static class Fetch {
  public static string All(string[] urls, string[] files, int par) {
    var http = new HttpClient(); http.Timeout = TimeSpan.FromSeconds(120);
    http.DefaultRequestHeaders.UserAgent.ParseAdd("Kartaclysm-Launcher/1.5");
    var gate = new SemaphoreSlim(par); string err = null;
    var tasks = urls.Select((u, i) => Task.Run(async () => {
      await gate.WaitAsync();
      try {
        for (int t = 1; ; t++) {
          string fail = null;
          try { var b = await http.GetByteArrayAsync(u); if (b.Length < 1000 || b[0] != (byte)'W' || b[1] != (byte)'B' || b[2] != (byte)'Z') throw new Exception("not a track file (404)"); File.WriteAllBytes(files[i] + ".part", b); File.Copy(files[i] + ".part", files[i], true); File.Delete(files[i] + ".part"); await Task.Delay(1500); return; }
          catch (Exception e) { fail = e.Message; }
          if (t >= 8 || (fail.Contains("404") && u.Contains("github.com"))) { err = u + ": " + fail; return; }
          await Task.Delay(fail.Contains("429") ? 30000 * t : 3000 * t);
        }
      } finally { gate.Release(); }
    })).ToArray();
    Task.WaitAll(tasks); return err;
  }
}
'@
$need = @($ct | Where-Object { -not (Test-Path "$cache\$($_.ct).wbz") })
if ($need.Count) {
  Say 20 "Downloading $($need.Count) tracks (Kartaclysm mirror)"
  # Kartaclysm mirror first (GitHub, no rate limit); Wiimm's archive (rate-limited, 429) only as fallback
  $null = [Fetch]::All([string[]]($need | ForEach-Object { "https://github.com/franxakathegoat-boop/kartaclysm/releases/download/tracks/$($_.ct).wbz" }), [string[]]($need | ForEach-Object { "$cache\$($_.ct).wbz" }), 4)
  $need = @($ct | Where-Object { -not (Test-Path "$cache\$($_.ct).wbz") })
  if ($need.Count) {
    Say 30 "Downloading $($need.Count) tracks from Wiimm's CT archive"
    $e = [Fetch]::All([string[]]($need | ForEach-Object { "https://ct.wiimm.de/d/$($_.ct)" }), [string[]]($need | ForEach-Object { "$cache\$($_.ct).wbz" }), 1)
    if ($e) { throw "Track download failed: $e" }
  }
}
$n = 0
foreach ($t in $ct) {
  $n++; Say (40 + [int](42 * $n / $ct.Count)) "Building track $n/$($ct.Count): $($t.name)"
  Run wszst.exe @('compress', '--szs', "$cache\$($t.ct).wbz", '--dest', "$gen\Race\Course\$($t.slotfile)", '-o', '-q') | Out-Null
}

# 5. Menus: LE-CODE cup scrolling, cup icons with our cup names, track names
Say 84 'Building menus and cup icons'
# Track names go into Common.bmg only (T11 exists only there): Menu.bmg entries would shadow them.
# LE-COPY puts the player's own original track names into the LE-CODE range; the BMG file path is
# relative because the '?MID=' condition syntax does not accept 'C:' paths.
Push-Location "$pack\build"
try { Run wszst.exe @('patch', "$x\files\Scene\UI\*.szs", '--le-menu', '--cup-icons', 'cup-icons.png', '--links', '--patch-bmg', 'LE-COPY?T11', '--patch-bmg', 'OVERWRITE?T11=tracks.bmg.txt', '-D', "$gen\Scene\UI\", '-o', '-q') | Out-Null } finally { Pop-Location }

# 6. main.dol (LE-CODE starter, Wiimmfi) + StaticR.rel (Wiimmfi + Kartaclysm region)
Say 90 'Patching game code'
$opt = @('--add-lecode'); if ($Region -gt 0) { $opt += @('--wiimmfi', '--region', "$Region") }
Copy-Item "$x\sys\main.dol" "$work\main.dol" -Force; Copy-Item "$x\files\rel\StaticR.rel" "$gen\rel\StaticR.rel" -Force
Run wstrt.exe (@('patch', "$work\main.dol", "$gen\rel\StaticR.rel") + $opt + @('-q')) | Out-Null

# main.dol cannot be swapped by Riivolution, so turn the differences into memory patches
Add-Type -TypeDefinition @'
using System; using System.Collections.Generic; using System.Text;
public static class DolDiff {
  static uint U(byte[] b, int o) { return (uint)(b[o] << 24 | b[o+1] << 16 | b[o+2] << 8 | b[o+3]); }
  static Dictionary<uint, byte> Map(byte[] d) {
    var m = new Dictionary<uint, byte>();
    for (int i = 0; i < 18; i++) { uint off = U(d, i*4), addr = U(d, 0x48 + i*4), size = U(d, 0x90 + i*4);
      for (uint k = 0; k < size; k++) m[addr + k] = d[off + k]; }
    return m;
  }
  public static string Patches(byte[] a, byte[] b) {
    var A = Map(a); var B = Map(b); var keys = new List<uint>();
    foreach (var kv in B) { byte o; if (!A.TryGetValue(kv.Key, out o) || o != kv.Value) keys.Add(kv.Key); }
    keys.Sort(); var sb = new StringBuilder(); int i = 0;
    while (i < keys.Count) { int j = i; while (j + 1 < keys.Count && keys[j+1] == keys[j] + 1 && j - i < 255) j++;
      while ((j - i + 1) % 4 != 0) { keys.Insert(j + 1, keys[j] + 1); j++; }
      var v = new StringBuilder(); for (int k = i; k <= j; k++) v.Append(B.ContainsKey(keys[k]) ? B[keys[k]].ToString("X2") : "00");
      sb.AppendFormat("    <memory offset=\"0x{0:X8}\" value=\"{1}\" />\n", keys[i], v); i = j + 1; }
    return sb.ToString();
  }
}
'@
$mem = [DolDiff]::Patches([IO.File]::ReadAllBytes("$x\sys\main.dol"), [IO.File]::ReadAllBytes("$work\main.dol"))

# 7. Riivolution XML (root = SD card or Dolphin root folder; everything lives in /Kartaclysm)
$xml = @"
<wiidisc version="1">
  <id game="RMC"/>
  <options>
    <section name="Kartaclysm">
      <option name="Pack" default="1">
        <choice name="Enabled"><patch id="Kartaclysm"/></choice>
      </option>
    </section>
  </options>
  <patch id="Kartaclysm" root="/Kartaclysm">
    <folder external="/gen/Race/Course" disc="/Race/Course" create="true"/>
    <folder external="/pack/Kartaclysm/Race/Course" disc="/Race/Course" create="true"/>
    <folder external="/gen/Scene/UI" disc="/Scene/UI"/>
    <file external="/gen/rel/StaticR.rel" disc="/rel/StaticR.rel"/>
    <file external="/pack/Kartaclysm/rel/lecode-$reg.bin" disc="/rel/lecode-$reg.bin" create="true"/>
$mem  </patch>
</wiidisc>
"@
Set-Content "$gen\riivolution\Kartaclysm.xml" $xml -Encoding UTF8

# 8. Dolphin / USB-loader image. LE-CODE's starter is a new main.dol section with a new entry
# point, which Riivolution memory patches cannot express, so the playable game is a patched copy
# of the player's own disc (new game ID RMC?KC keeps the original save untouched).
Say 92 'Building your Kartaclysm game file'
Copy-Item "$gen\Race\Course\*" "$x\files\Race\Course\" -Force
if (Test-Path "$pack\Kartaclysm\Race\Course") { Copy-Item "$pack\Kartaclysm\Race\Course\*" "$x\files\Race\Course\" -Force }
Copy-Item "$gen\Scene\UI\*" "$x\files\Scene\UI\" -Force
Copy-Item "$gen\rel\StaticR.rel" "$x\files\rel\StaticR.rel" -Force
Copy-Item "$pack\Kartaclysm\rel\lecode-$reg.bin" "$x\files\rel\" -Force
Copy-Item "$work\main.dol" "$x\sys\main.dol" -Force
Run wit.exe @('copy', $x, "$gen\Kartaclysm.wbfs", '--wbfs', '--id', '....KC', '--name', 'Kartaclysm', '-o', '-q') | Out-Null
@{ game = $id; region = $Region; pack = (Get-Content "$pack\build\stamp.txt" -Raw).Trim(); built = (Get-Date).ToString('s') } | ConvertTo-Json | Set-Content "$gen\stamp.json"
Remove-Item $work -Recurse -Force
Say 100 'Ready'
