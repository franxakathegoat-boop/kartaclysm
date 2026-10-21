# Kartaclysm
Windows launcher for a Mario Kart Wii custom retro-track pack. Download `Kartaclysm.exe` from Releases and double-click.
You need your OWN Mario Kart Wii game file and Dolphin. No Nintendo files are distributed here.
- Launcher release: `git tag launcher-vX && git push --tags` (GitHub Actions builds the exe)
- Pack update: put files in `pack/`, run `python3 scripts/publish_pack.py <version>`
- Real Wii (softmod): the pack layout is SD-root ready (`riivolution/Kartaclysm.xml` + `Kartaclysm/`). Use the launcher's "Export to SD (Wii)" button, or copy the contents of `pack/` to the SD root, then Homebrew Channel > Riivolution > enable "Pack" > Launch.

## Art pipeline (v1.4)

`python3 art4/build4.py` regenerates the logo, wordmark, symbol and app.ico in src/Assets. Previews: `Kartaclysm.exe --render <dir> home|tracks|updates|online|settings|intro`.

## The pack (v1.0.x)
- 34 named cups (136 custom tracks) plus the 8 original cups, powered by LE-CODE (build 40).
- Nothing from the game is shipped. On the first PLAY the launcher runs `pack/build/prepare.ps1` on your PC. It downloads Wiimm's tools, fetches 105 tracks from Wiimm's CT archive (via the Kartaclysm GitHub mirror, with Wiimm's site as fallback), rebuilds them with your own game files, and writes `%APPDATA%\Kartaclysm\gen\Kartaclysm.wbfs` (game ID RMCPKC, so your normal save is untouched). Dolphin then starts that file.
- The other 31 tracks are hosted fan releases. Track sources and authors are listed in [CREDITS.md](CREDITS.md).
- Real Wii: choose REAL WII · USB in Settings; PLAY copies the WBFS to `wbfs/Kartaclysm [RMCPKC]/` for USB Loader GX or WiiFlow.
- Online: Wiimmfi, with its own Kartaclysm region once Wiimm assigns one (`wiimmfi_region` in pack.json; 0 = offline).

## Music

Launcher music: **Stalemate by KzX (Kayoszx)**, newgrounds.com/audio/listen/482872 — used with the author's permission ("Go for it, just leave some credit."). Turn it off in Settings.
