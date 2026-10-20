#!/usr/bin/env python3
"""Publish a pack update. Usage: python3 scripts/publish_pack.py 1.0.1
Uploads every file under pack/ to the rolling 'pack' release (only changed ones) + manifest.json.
NEVER put Nintendo game files in pack/ - only fan-made tracks + riivolution xml."""
import hashlib, json, os, subprocess, sys
REPO = "franxakathegoat-boop/mario-kart-nitro-mogger"; TAG = "pack"
root = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "pack")
ver = sys.argv[1]
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
