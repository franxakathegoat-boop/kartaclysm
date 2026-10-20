# Kartaclysm
Windows launcher for a Mario Kart Wii custom retro-track pack. Download `Kartaclysm.exe` from Releases and double-click.
You need your OWN Mario Kart Wii game file and Dolphin. No Nintendo files are distributed here.
- Launcher release: `git tag launcher-vX && git push --tags` (GitHub Actions builds the exe)
- Pack update: put files in `pack/`, run `python3 scripts/publish_pack.py <version>`
- Real Wii (softmod): the pack layout is SD-root ready (`riivolution/Kartaclysm.xml` + `Kartaclysm/`). Use the launcher's "Export to SD (Wii)" button, or copy the contents of `pack/` to the SD root, then Homebrew Channel > Riivolution > enable "Pack" > Launch.

## Art pipeline (v1.4)

`python3 art4/build4.py` regenerates the logo, wordmark, symbol and app.ico in src/Assets. Previews: `Kartaclysm.exe --render <dir> home|tracks|updates|online|settings|intro`.
