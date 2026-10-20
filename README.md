# Kartaclysm
Windows launcher for a Mario Kart Wii custom retro-track pack. Download `Kartaclysm.exe` from Releases and double-click.
You need your OWN Mario Kart Wii game file and Dolphin. No Nintendo files are distributed here.
- Launcher release: `git tag launcher-vX && git push --tags` (GitHub Actions builds the exe)
- Pack update: put files in `pack/`, run `python3 scripts/publish_pack.py <version>`
- Real Wii (softmod): the pack layout is SD-root ready (`riivolution/Kartaclysm.xml` + `Kartaclysm/`). Use the launcher's "Export to SD (Wii)" button, or copy the contents of `pack/` to the SD root, then Homebrew Channel > Riivolution > enable "Pack" > Launch.

## Art pipeline (v1.3+)
`cd art && python3 build_assets.py` regenerates logo, emblem, icon, background and intro overlays into src/Assets.
`python3 render.py main out.png [hover] [progress]` and `python3 render.py intro out.mp4` render previews using the same layout/animation math as src/Program.cs.
