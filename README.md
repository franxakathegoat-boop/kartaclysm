# Mario Kart Nitro Mogger
Windows launcher for a Mario Kart Wii custom retro-track pack. Download `MarioKartNitroMogger.exe` from Releases and double-click.
You need your OWN Mario Kart Wii game file and Dolphin. No Nintendo files are distributed here.
- Launcher release: `git tag launcher-vX && git push --tags` (GitHub Actions builds the exe)
- Pack update: put files in `pack/`, run `python3 scripts/publish_pack.py <version>`
- Real Wii (softmod): the pack layout is SD-root ready (`riivolution/NitroMogger.xml` + `NitroMogger/`). Use the launcher's "Export to SD (Wii)" button, or copy the contents of `pack/` to the SD root, then Homebrew Channel > Riivolution > enable "Pack" > Launch.
