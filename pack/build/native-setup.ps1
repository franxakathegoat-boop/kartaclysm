# Kartaclysm NATIVE (WiiCompiled) builder. Runs on the player's PC after prepare.ps1.
# Downloads the official WiiCompiled setup (GPL, github.com/patchzyy/Wiicompiled) and the Kartaclysm fork
# changes, extracts the needed files from the player's OWN game, and compiles Kartaclysm natively.
# Nothing built here may be shared: the result contains code translated from the player's game.
param([Parameter(Mandatory)][string]$Game, [string]$Root = "$env:APPDATA\Kartaclysm", [switch]$Force)
$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = 'Tls12'
function Say($p, $m) { Write-Output ("PROGRESS {0} {1}" -f $p, $m) }
$SetupUrl = 'https://github.com/patchzyy/Wiicompiled/releases/download/v0.2.36/WiiCompiled-Setup.exe'
$ForkCommit = '43a68e6'
$ForkUrl = "https://codeload.github.com/franxakathegoat-boop/wiicompiled-kartaclysm/zip/$ForkCommit"
$ForkFiles = 'Launcher/LocalBuild.ps1','projects/mkwii/recomp.yml','runtime/CMakeLists.txt','runtime/cmake/PublicProducts.cmake','runtime/include/runtime_config.h','runtime/include/runtime_product.h','runtime/src/hle/net/network_core.cpp','runtime/src/hle/storage/dvd.cpp','runtime/src/hle/storage/riivolution.cpp','runtime/src/main.cpp','runtime/src/product/kartaclysm_product.cpp','runtime/src/product/retro_rewind_product.cpp'
$pack = "$Root\pack"; $gen = "$Root\gen"; $nat = "$Root\native"; $dl = "$nat\downloads"
$payload = "$nat\payload"; $ws = "$payload\BuildWorkspace"; $tk = "$payload\Toolkit"
$pk = "$ws\PulsarPacks\completed\Kartaclysm\Kartaclysm"; $out = "$nat\game"
New-Item -ItemType Directory -Force $nat, $dl | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (-not (Test-Path "$gen\Race\Course")) { throw 'Run the normal Kartaclysm setup first (gen\Race\Course is missing).' }
$free = (Get-PSDrive ([IO.Path]::GetPathRoot($Root).Substring(0,1))).Free
if (-not (Test-Path "$out\portable.txt") -and $free -lt 20GB) { throw ("Native mode needs about 20 GB free on {0} (only {1:N1} GB free)." -f [IO.Path]::GetPathRoot($Root), ($free/1GB)) }

# 1. official WiiCompiled setup -> Toolkit + BuildWorkspace (payload appended to the exe, trailer 'MKWCPAY1')
if (-not (Test-Path "$payload\payload-manifest.json")) {
  Say 1 'Downloading WiiCompiled (official setup, ~390 MB)'
  $exe = "$dl\WiiCompiled-Setup.exe"
  if (-not (Test-Path $exe) -or (Get-Item $exe).Length -lt 300MB) { Invoke-WebRequest $SetupUrl -OutFile "$exe.part" -UseBasicParsing; Move-Item "$exe.part" $exe -Force }
  Say 4 'Unpacking WiiCompiled tools'
  $f = [IO.File]::OpenRead($exe)
  try {
    $f.Seek(-24, 'End') | Out-Null; $br = New-Object IO.BinaryReader($f)
    if ([Text.Encoding]::ASCII.GetString($br.ReadBytes(8)) -ne 'MKWCPAY1') { throw 'Unexpected WiiCompiled setup format' }
    $off = $br.ReadInt64(); $len = $br.ReadInt64(); $f.Seek($off, 'Begin') | Out-Null
    $o = [IO.File]::Create("$dl\payload.zip"); $buf = New-Object byte[] (8MB); $left = $len
    while ($left -gt 0) { $n = $f.Read($buf, 0, [Math]::Min($buf.Length, $left)); if ($n -le 0) { throw 'Truncated setup file' }; $o.Write($buf, 0, $n); $left -= $n }
    $o.Close()
  } finally { $f.Close() }
  if (Test-Path $payload) { Remove-Item $payload -Recurse -Force }
  [IO.Compression.ZipFile]::ExtractToDirectory("$dl\payload.zip", $payload); Remove-Item "$dl\payload.zip"
  Remove-Item "$nat\fork.stamp" -EA 0
}

# 2. Kartaclysm fork changes (Kartaclysm product, Wiimmfi mode, kartaclysm build profile)
if ((Get-Content "$nat\fork.stamp" -EA 0) -ne $ForkCommit) {
  Say 8 'Adding the Kartaclysm changes to WiiCompiled'
  Invoke-WebRequest $ForkUrl -OutFile "$dl\fork.zip" -UseBasicParsing
  $z = [IO.Compression.ZipFile]::OpenRead("$dl\fork.zip")
  try {
    foreach ($rel in $ForkFiles) {
      $e = $z.Entries | Where-Object { $_.FullName -match ('^[^/]+/' + [regex]::Escape($rel) + '$') }
      if (-not $e) { throw "Fork file missing: $rel" }
      $dest = if ($rel -eq 'Launcher/LocalBuild.ps1') { "$ws\LocalBuild.ps1" } else { Join-Path $ws ($rel -replace '/', '\') }
      New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
      [IO.Compression.ZipFileExtensions]::ExtractToFile($e, $dest, $true)
    }
  } finally { $z.Dispose() }
  Remove-Item "$dl\fork.zip"; Set-Content "$nat\fork.stamp" $ForkCommit
}

# 3. the player's own game: full DATA folder (dvd_root) + main.dol/StaticR.rel for the translator
$wit = (Get-ChildItem "$Root\tools" -Directory -Filter 'wit-*')[0].FullName + '\bin\wit.exe'
$env:PATH = (Split-Path $wit) + ";$env:PATH"
if (-not (Test-Path "$nat\DATA\sys\main.dol")) {
  Say 10 'Extracting your Mario Kart Wii (a few minutes)'
  if (Test-Path "$nat\DATA") { Remove-Item "$nat\DATA" -Recurse -Force }
  $o = & $wit extract $Game "$nat\DATA" --psel data -o -q 2>&1; if ($LASTEXITCODE -gt 1) { throw "wit extract failed: $($o | Select-Object -Last 3 | Out-String)" }
}
$expect = '80d18895b39c63bd80f457398bfcbb91b7d16ac116a41a88967e954080155b05'
if ((Get-FileHash "$nat\DATA\sys\main.dol").Hash.ToLower() -ne $expect) { throw 'WiiCompiled needs a clean, unmodified PAL (RMCP01) Mario Kart Wii. Your game file does not match.' }
New-Item -ItemType Directory -Force "$ws\Assets" | Out-Null
Copy-Item "$nat\DATA\sys\main.dol", "$nat\DATA\files\rel\StaticR.rel" "$ws\Assets\" -Force

# 3b. Kartaclysm title screen: the title files prepare.ps1 built go into native's own DATA copy
foreach ($t in Get-ChildItem "$gen\Scene\UI" -Filter 'Title*.szs') { Copy-Item $t.FullName "$nat\DATA\files\Scene\UI\" -Force }

# 4. Kartaclysm Pulsar pack (code/config from the pack, tracks from the player's own built files)
Say 14 'Setting up the Kartaclysm pack'
if (Test-Path $pk) { Remove-Item $pk -Recurse -Force }
New-Item -ItemType Directory -Force (Split-Path $pk) | Out-Null
[IO.Compression.ZipFile]::ExtractToDirectory("$pack\native\kartaclysm-pulsar.zip", (Split-Path $pk))
New-Item -ItemType Directory -Force "$pk\Tracks" | Out-Null
foreach ($l in Get-Content "$pk\track-map.txt") {
  $i, $f = $l.Split(' '); $src = "$gen\Race\Course\$f"
  if (-not (Test-Path $src)) { $src = "$pack\Kartaclysm\Race\Course\$f" }
  if (-not (Test-Path $src)) { throw "Track file $f is missing; press PLAY in Dolphin mode once to rebuild" }
  Copy-Item $src "$pk\Tracks\$i.szs" -Force
}

# 5. compile (only when the code changed); progress from ninja's [n/m]
$codeSha = (Get-FileHash "$pk\Binaries\Code.pul").Hash
$want = "$ForkCommit $codeSha"
$exeOut = Get-ChildItem $out -Filter *.exe -Recurse -EA 0 | Where-Object { $_.Name -match 'Kartaclysm' } | Select-Object -First 1
if ($Force -or -not $exeOut -or (Get-Content "$nat\build.stamp" -EA 0) -ne $want) {
  Say 16 'Compiling Kartaclysm for your PC (first time: about 1 hour)'
  $log = "$Root\native-build.log"
  $psi = New-Object Diagnostics.ProcessStartInfo 'powershell.exe'
  $psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$ws\LocalBuild.ps1`" -Workspace `"$ws`" -Toolkit `"$tk`" -Profile kartaclysm -OutputDirectory `"$out`" -KartaclysmPackageDirectory `"$pk`""
  $psi.UseShellExecute = $false; $psi.CreateNoWindow = $true; $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true
  $p = [Diagnostics.Process]::Start($psi)
  $errTask = $p.StandardError.ReadToEndAsync()
  $w = [IO.StreamWriter]::new($log, $false)
  try {
    while (($line = $p.StandardOutput.ReadLine()) -ne $null) {
      $w.WriteLine($line); $w.Flush()
      if ($line -match '^\[(\d+)/(\d+)\]') { Say (30 + [int](68 * [int]$matches[1] / [int]$matches[2])) "Compiling $($matches[1])/$($matches[2])" }
      elseif ($line -match '^MKWCBUILD:STEP:\S+ (.+)$') { Say 18 $matches[1] }
    }
    $p.WaitForExit(); $w.WriteLine($errTask.Result)
  } finally { $w.Close() }
  if ($p.ExitCode -ne 0) { throw "WiiCompiled build failed (exit $($p.ExitCode)). Log: $log`n$((Get-Content $log -Tail 8) -join "`n")" }
  Set-Content "$nat\build.stamp" $want
  $exeOut = Get-ChildItem $out -Filter *.exe -Recurse | Where-Object { $_.Name -match 'Kartaclysm' } | Select-Object -First 1
  if (-not $exeOut) { throw "Build finished but no Kartaclysm exe was found in $out" }
}

# 6. portable config next to the game, so a normal WiiCompiled / Retro Rewind install is not touched
Set-Content (Join-Path $exeOut.DirectoryName 'portable.txt') 'Kartaclysm portable install'
$ud = Join-Path $exeOut.DirectoryName 'UserData'; New-Item -ItemType Directory -Force $ud | Out-Null
$cfg = Join-Path $ud 'Config.toml'
function TomlStr($v) { '"' + ($v -replace '\\', '\\') + '"' }
$txt = if (Test-Path $cfg) { [IO.File]::ReadAllText($cfg) } else { '' }
if ($txt -match '^\[\r?\n\]') { $txt = '' }   # file broken by native-setup 1.1.2
$txt = [regex]::Replace($txt, '(?m)^\s*(dvd_root|pulsar_pack_root)\s*=.*\r?\n?', '')
$ins = "dvd_root = $(TomlStr "$nat\DATA")`r`npulsar_pack_root = $(TomlStr $pk)`r`n"
if ($txt -match '(?m)^\s*\[paths\]\s*$') { $txt = ([regex]'(?m)^\s*\[paths\]\s*\r?\n').Replace($txt, [Text.RegularExpressions.MatchEvaluator]{ param($mm) $mm.Value + $ins }, 1) }
else { $txt = $txt.TrimEnd() + "`r`n`r`n[paths]`r`n" + $ins }
[IO.File]::WriteAllText($cfg, $txt.TrimStart(), (New-Object Text.UTF8Encoding($false)))
Write-Output "EXE $($exeOut.FullName)"
Say 100 'Native Kartaclysm ready'
