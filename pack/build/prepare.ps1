# Kartaclysm first-run builder. Runs on the player's PC and builds every file that is derived
# from the player's own Mario Kart Wii copy. Nothing from the game is ever uploaded or shipped.
param([Parameter(Mandatory)][string]$Game, [string]$Root = "$env:APPDATA\Kartaclysm", [int]$Region = 0, [string]$Wfc = '')
$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = 'Tls12'
# Online server: Kartaclysm's own server (pack.json "wfc_domain") or, when that is empty, Wiimmfi. Read from the
# manifest the launcher just downloaded, so launchers that do not pass -Wfc build the right game too.
if (-not $Wfc) { try { $Wfc = [string](Get-Content "$Root\manifest.json" -Raw | ConvertFrom-Json).wfc_domain } catch { $Wfc = '' } }
if ($Wfc -and $Wfc -notmatch '^[a-z0-9.-]{1,16}$') { throw "Invalid server domain in the pack: $Wfc" }
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

# 4b. Never leave a track slot empty: picking an empty slot freezes the game (online too, for everyone in the
#     room). A custom track that is not built here and not in the pack yet uses, for now, the original track with
#     the same property (LE-CODE slot of each original track, see `wlect dump`).
$propSlot = @{ T11 = 0x08; T12 = 0x01; T13 = 0x02; T14 = 0x04; T21 = 0x00; T22 = 0x05; T23 = 0x06; T24 = 0x07
  T31 = 0x09; T32 = 0x0f; T33 = 0x0b; T34 = 0x03; T41 = 0x0e; T42 = 0x0a; T43 = 0x0c; T44 = 0x0d
  T51 = 0x10; T52 = 0x14; T53 = 0x19; T54 = 0x1a; T61 = 0x1b; T62 = 0x1f; T63 = 0x17; T64 = 0x12
  T71 = 0x15; T72 = 0x1e; T73 = 0x1d; T74 = 0x11; T81 = 0x18; T82 = 0x16; T83 = 0x13; T84 = 0x1c }
$standIn = @(foreach ($t in $list) {
  if ((Test-Path "$gen\Race\Course\$($t.slotfile)") -or (Test-Path "$pack\Kartaclysm\Race\Course\$($t.slotfile)")) { continue }
  Copy-Item ("$gen\Race\Course\{0:x3}.szs" -f $propSlot[$t.prop]) "$gen\Race\Course\$($t.slotfile)"; $t.name
})
if ($standIn.Count) { Write-Warning ("{0} tracks are not in this pack yet and use an original track for now: {1}" -f $standIn.Count, ($standIn -join ', ')) }

# 5. Menus: LE-CODE cup scrolling, cup icons with our cup names, track names
Say 84 'Building menus and cup icons'
# The 8 original cups get the real Mario Kart icons that Wiimm's tools carry (slots 0-9 of cup-icons.png are placeholders)
$icons = 'cup-icons.png'
try { Run wimgt.exe @('copy', "$pack\build\cup-icons.png", '-d', "$work\cup-icons.png", '--patch', 'T.L.CP=:CUP-IMAGE=arrows,swapped', '-o', '-q') | Out-Null; $icons = "$work\cup-icons.png" }
catch { Write-Warning "Original cup icons not added: $($_.Exception.Message)" }
# Track names go into Common.bmg only (T11 exists only there): Menu.bmg entries would shadow them.
# LE-COPY puts the player's own original track names into the LE-CODE range; the BMG file path is
# relative because the '?MID=' condition syntax does not accept 'C:' paths.
Push-Location "$pack\build"
try { Run wszst.exe @('patch', "$x\files\Scene\UI\*.szs", '--le-menu', '--cup-icons', $icons, '--links', '--patch-bmg', 'LE-COPY?T11', '--patch-bmg', 'OVERWRITE?T11=tracks.bmg.txt', '-D', "$gen\Scene\UI\", '-o', '-q') | Out-Null } finally { Pop-Location }

# 5b. Kartaclysm title screen (our own art, pack/build/title) in every Title*.szs that has the title textures
$tmap = @{ 'tt_title_screen_mario0' = 'title_bg'; 'tt_title_screen_mario0_bokeboke' = 'title_bg_blur'; 'tt_title_screen_title_rogo' = 'title_logo'; 'tt_title_screen_title_rogo_bokeboke' = 'title_logo_blur'; 'tt_title_screen_title_rogo_tm_only' = 'title_logo'
  # the menu backdrop picks one of these at random: all show the Kartaclysm picture
  'tt_title_screen_mario' = 'title_bg'; 'tt_title_screen_mario_bokeboke' = 'title_bg_blur'; 'tt_title_screen_mario2' = 'title_bg'; 'tt_title_screen_mario2_bokeboke' = 'title_bg_blur'
  'tt_title_screen_luigi' = 'title_bg'; 'tt_title_screen_luigi_bokeboke' = 'title_bg_blur'; 'tt_title_screen_peachi' = 'title_bg'; 'tt_title_screen_peachi_bokeboke' = 'title_bg_blur'
  'tt_title_screen_koopa' = 'title_bg'; 'tt_title_screen_koopa_bokeboke' = 'title_bg_blur' }
foreach ($orig in Get-ChildItem "$x\files\Scene\UI" -Filter 'Title*.szs') {
  if (-not (Test-Path "$gen\Scene\UI\$($orig.Name)")) { Copy-Item $orig.FullName "$gen\Scene\UI\" }
  $ts = Get-Item "$gen\Scene\UI\$($orig.Name)"
  $td = "$work\title_$($ts.BaseName)"; if (Test-Path $td) { Remove-Item $td -Recurse -Force }
  Run wszst.exe @('extract', $ts.FullName, '-d', $td, '-o', '-q') | Out-Null
  $hit = $false
  foreach ($tpl in Get-ChildItem $td -Recurse -Filter 'tt_title_screen_*.tpl') {
    $png = $tmap[$tpl.BaseName]; if (-not $png) { continue }
    $info = (& wimgt.exe list -l $tpl.FullName 2>&1 | Out-String)
    $fmt = if ($info -match 'CMPR') { 'CMPR' } elseif ($info -match 'RGB5A3') { 'RGB5A3' } elseif ($info -match 'RGBA32') { 'RGBA32' } else { 'RGB565' }
    $size = if ($info -match '(\d+)\*(\d+)') { "$($matches[1])x$($matches[2])" } else { $null }
    $srcPng = "$pack\build\title\$png.png"
    if ($size) { Add-Type -AssemblyName System.Drawing; $w,$h = $size.Split('x'); $im = [Drawing.Image]::FromFile($srcPng); $bm = New-Object Drawing.Bitmap $im, ([int]$w), ([int]$h); $im.Dispose(); $srcPng = "$td\$png-$size.png"; $bm.Save($srcPng); $bm.Dispose() }
    Run wimgt.exe @('encode', $srcPng, '-x', "TPL.$fmt", '-d', $tpl.FullName, '-o', '-q') | Out-Null; $hit = $true
  }
  # Full-screen title: the art bleeds 120 units past every edge (assets-src/title/title.py), so grow the
  # 832x140 logo strip to 832x380 and the 832x316 picture to 832x556 (centered, so the animation still lines up)
  if ($hit) { foreach ($ly in Get-ChildItem $td -Recurse -Filter '*.brlyt') {
    $lb = [IO.File]::ReadAllBytes($ly.FullName); $chg = $false
    for ($i = 0; $i -lt $lb.Length - 0x4c; $i++) {
      if ($lb[$i] -ne 0x70 -or $lb[$i+1] -ne 0x69 -or $lb[$i+2] -ne 0x63 -or $lb[$i+3] -ne 0x31) { continue }
      $wd = [BitConverter]::ToString($lb, $i+0x44, 8)
      if ($wd -eq '44-50-00-00-43-0C-00-00') { $lb[$i+0x48]=0x43; $lb[$i+0x49]=0xBE; $chg = $true }
      elseif ($wd -eq '44-50-00-00-43-9E-00-00') { $lb[$i+0x48]=0x44; $lb[$i+0x49]=0x0B; $chg = $true }
    }
    if ($chg) { [IO.File]::WriteAllBytes($ly.FullName, $lb) } } }
  if ($hit) { Get-ChildItem $td -Filter '*.png' | Remove-Item; Run wszst.exe @('create', $td, '-d', $ts.FullName, '-o', '-q') | Out-Null }
  Remove-Item $td -Recurse -Force
}

# 6. main.dol (LE-CODE starter + online patch) + StaticR.rel + LE-CODE binary
Say 90 'Patching game code'
$opt = @('--add-lecode')
Copy-Item "$pack\Kartaclysm\rel\lecode-$reg.bin" "$gen\rel\" -Force
if ($Wfc) {
  # Own server (WiiLink WFC): the stage0 patch for this region, built by the server itself. A cached copy
  # keeps rebuilds working while the server is down.
  $s0name = "RMC$($id.Substring(3,1))D00.txt"; $s0 = "$cache\wfc-$Wfc-$s0name"
  try { Invoke-WebRequest "https://$Wfc/client/$s0name" -OutFile "$s0.part" -UseBasicParsing; Move-Item "$s0.part" $s0 -Force }
  catch { if (-not (Test-Path $s0)) { throw "Could not download the online patch from $Wfc (server down?): $($_.Exception.Message)" } }
  if ([string](Get-Content $s0 -Raw) -notmatch '^\s*([0-9A-Fa-f]{8}\s+[0-9A-Fa-f]{8}\s*)+$') { Remove-Item $s0; throw "The online patch from $Wfc is not a Gecko code list" }
  # every player has the same tracks on our server, so LE-CODE's Worldwide button is turned on
  Set-Content "$work\worldwide.lpar" "#LE-LPAR`r`n[LECODE-PARAMETERS]`r`nBT-WORLDWIDE`t= LE`$ENABLE`r`nVS-WORLDWIDE`t= LE`$ENABLE" -Encoding ASCII
  Run wlect.exe @('patch', "$gen\rel\lecode-$reg.bin", '--lpar', "$work\worldwide.lpar", '-o', '-q') | Out-Null
} else {
  # Wiimmfi (Nintendo WFC is gone); own Kartaclysm region once Wiimm assigns it
  $opt += '--wiimmfi'; if ($Region -gt 0) { $opt += @('--region', "$Region") }
}
Copy-Item "$x\sys\main.dol" "$work\main.dol" -Force; Copy-Item "$x\files\rel\StaticR.rel" "$gen\rel\StaticR.rel" -Force
Run wstrt.exe (@('patch', "$work\main.dol", "$gen\rel\StaticR.rel") + $opt + @('-q')) | Out-Null
# 'Unlock Everything Without Save' (PAL 0x80549974 = li r3,1, the same patch Pulsar ships): the unlock check
# always answers yes. Written straight into StaticR.rel (loaded at 0x805102E0), so no cheat handler is needed
# and the save file is never touched.
if ($reg -eq 'PAL') {
  $rp = "$gen\rel\StaticR.rel"; $rb = [IO.File]::ReadAllBytes($rp); $o = 0x80549974 - 0x805102E0
  if ($rb[$o] -eq 0x54 -and $rb[$o+1] -eq 0x03 -and $rb[$o+2] -eq 0x0F -and $rb[$o+3] -eq 0xFE) {
    $rb[$o] = 0x38; $rb[$o+1] = 0x60; $rb[$o+2] = 0x00; $rb[$o+3] = 0x01; [IO.File]::WriteAllBytes($rp, $rb)
  } elseif (-not ($rb[$o] -eq 0x38 -and $rb[$o+1] -eq 0x60)) { Write-Warning 'Unlock patch skipped: unexpected StaticR.rel' }
}

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
  static int Off(byte[] d, uint addr) {
    for (int i = 0; i < 18; i++) { uint off = U(d, i*4), a = U(d, 0x48 + i*4), size = U(d, 0x90 + i*4);
      if (size > 0 && addr >= a && addr - a < size) return (int)(off + addr - a); }
    throw new Exception("Online patch address 0x" + addr.ToString("X8") + " is not in main.dol");
  }
  // Own server: write the WiiLink WFC stage0 Gecko codes into main.dol once (like WiiLink's Riivolution patches)
  // instead of running them with a code handler. Stage0 uses 04 (write), 06 (string), C6 (branch),
  // 22 (if not equal) and E2 (endif); anything else is refused. Every write must land on bytes that the
  // LE-CODE patch left as in the player's original main.dol.
  public static void ApplyGecko(byte[] orig, byte[] dol, string codes) {
    var w = new List<uint>();
    foreach (var t in codes.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) w.Add(Convert.ToUInt32(t, 16));
    var skip = new Stack<bool>(); var mine = new Dictionary<uint, bool>();
    for (int i = 0; i + 1 < w.Count; ) {
      uint a = w[i], v = w[i+1], type = a >> 24, addr = 0x80000000 | (a & 0x01FFFFFF); i += 2;
      bool on = !skip.Contains(true); byte[] data;
      if (type == 0x04) data = new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };
      else if (type == 0x06) {
        data = new byte[v]; for (int k = 0; k < v; k++) data[k] = (byte)(w[i + k / 4] >> (24 - 8 * (k % 4)));
        i += (int)((v + 7) / 8 * 2);
      }
      else if (type == 0xC6) { uint b = 0x48000000 | ((v - addr) & 0x03FFFFFC); data = new[] { (byte)(b >> 24), (byte)(b >> 16), (byte)(b >> 8), (byte)b }; }
      else if (type == 0x22) {
        if ((a & 1) != 0 && skip.Count > 0) skip.Pop();
        addr &= 0xFFFFFFFE; int o = Off(dol, addr);
        skip.Push(!on || U(dol, o) == v); continue;
      }
      else if (type == 0xE2 && v == 0) { for (uint n = a & 0xFF; n > 0 && skip.Count > 0; n--) skip.Pop(); continue; }
      else throw new Exception("Unsupported code in the online patch: " + a.ToString("X8") + " " + v.ToString("X8"));
      if (!on) continue;
      for (int k = 0; k < data.Length; k++) {
        uint m = addr + (uint)k; int fo = Off(dol, m);
        if (!mine.ContainsKey(m) && dol[fo] != orig[Off(orig, m)]) throw new Exception("The online patch conflicts with LE-CODE at 0x" + m.ToString("X8"));
        dol[fo] = data[k]; mine[m] = true;
      }
    }
  }
}
'@
if ($Wfc) {
  $md = [IO.File]::ReadAllBytes("$work\main.dol")
  [DolDiff]::ApplyGecko([IO.File]::ReadAllBytes("$x\sys\main.dol"), $md, (Get-Content $s0 -Raw)); [IO.File]::WriteAllBytes("$work\main.dol", $md)
}
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
    <file external="/gen/rel/lecode-$reg.bin" disc="/rel/lecode-$reg.bin" create="true"/>
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
Copy-Item "$gen\rel\lecode-$reg.bin" "$x\files\rel\" -Force
Copy-Item "$work\main.dol" "$x\sys\main.dol" -Force
Run wit.exe @('copy', $x, "$gen\Kartaclysm.wbfs", '--wbfs', '--id', '....KC', '--name', 'Kartaclysm', '-o', '-q') | Out-Null
@{ game = $id; region = $Region; wfc = $Wfc; pack = (Get-Content "$pack\build\stamp.txt" -Raw).Trim(); built = (Get-Date).ToString('s') } | ConvertTo-Json | Set-Content "$gen\stamp.json"
Remove-Item $work -Recurse -Force
Say 100 'Ready'
