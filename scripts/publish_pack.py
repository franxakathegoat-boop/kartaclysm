#!/usr/bin/env python3
"""Publish a pack update. Usage: python3 scripts/publish_pack.py 1.0.1 [--force]
Uploads every file under pack/ to the rolling 'pack' release (only changed ones) + manifest.json.
NEVER put Nintendo game files in pack/ - only fan-made tracks + riivolution xml."""
import hashlib, json, os, shutil, subprocess, sys, tempfile
REPO = "franxakathegoat-boop/kartaclysm"; TAG = "pack"
root = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "pack")
args = [a for a in sys.argv[1:] if a != "--force"]; ver = args[0]
# refuse to publish a pack that cannot work (--force skips this): the lecode-*.bin cups must match
# kartaclysm-ctdef.txt. Hosted (non-CT) tracks missing from pack/Kartaclysm/Race/Course are only reported:
# prepare.ps1 puts the original track with the same property in their slots.
missing = [t["name"] for t in json.load(open(os.path.join(root, "build", "tracks.json")))
           if not t.get("ct") and not os.path.exists(os.path.join(root, "Kartaclysm", "Race", "Course", t["slotfile"]))]
if missing: print(f"note: {len(missing)} hosted tracks are not in pack/Kartaclysm/Race/Course and play as original tracks: " + ", ".join(missing))
problems = []
if shutil.which("wlect"):
    for reg in ("PAL", "USA", "JAP", "KOR"):
        b = os.path.join(root, "Kartaclysm", "rel", f"lecode-{reg}.bin")
        with tempfile.TemporaryDirectory() as tmp:
            c = shutil.copy(b, tmp)
            subprocess.run(["wlect", "patch", c, "--le-define", os.path.join(root, "build", "kartaclysm-ctdef.txt"), "-o", "-q"], check=True)
            if subprocess.run(["wlect", "bin-diff", "d", b, c], capture_output=True).returncode:
                problems.append(f"lecode-{reg}.bin is stale: run wlect patch pack/Kartaclysm/rel/lecode-*.bin --le-define pack/build/kartaclysm-ctdef.txt")
else: print("warning: wlect (Wiimms SZS Tools) not found, LE-CODE binaries not checked against kartaclysm-ctdef.txt")
if problems and "--force" not in sys.argv: sys.exit("not publishing:\n  " + "\n  ".join(problems))
# the launcher rebuilds when gen/stamp.json "pack" != manifest version, so the stamp must always match
open(os.path.join(root, "build", "stamp.txt"), "w").write(ver + "\n")
def asset(rel): return rel.replace("/", "__")
subprocess.run(["gh","release","view",TAG,"-R",REPO],capture_output=True).returncode and \
  subprocess.run(["gh","release","create",TAG,"-R",REPO,"--title","Pack content","--notes","Rolling pack files used by the launcher.","--latest=false"],check=True)
old = {}
try:
    old = {f["path"]: f["sha256"] for f in json.loads(subprocess.run(["gh","release","download",TAG,"-R",REPO,"-p","manifest.json","-O","-"],capture_output=True,check=True).stdout)["files"]}
except Exception: pass
files = []
for d, _, fs in os.walk(root):
    for n in sorted(fs):
        p = os.path.join(d, n); rel = os.path.relpath(p, root).replace(os.sep, "/")
        h = hashlib.sha256(open(p,"rb").read()).hexdigest()
        files.append({"path": rel, "sha256": h, "size": os.path.getsize(p),
                      "url": f"https://github.com/{REPO}/releases/download/{TAG}/{asset(rel)}"})
        if old.get(rel) != h:
            tmp = f"/tmp/{asset(rel)}"; subprocess.run(["cp", p, tmp], check=True)
            subprocess.run(["gh","release","upload",TAG,tmp,"-R",REPO,"--clobber"],check=True); print("uploaded", rel)
cfg = json.load(open(os.path.join(root, "..", "pack.json")))
man = {"version": ver, "files": files, **cfg}
open("/tmp/manifest.json","w").write(json.dumps(man, indent=1))
subprocess.run(["gh","release","upload",TAG,"/tmp/manifest.json","-R",REPO,"--clobber"],check=True)
print("published pack", ver, len(files), "files")
