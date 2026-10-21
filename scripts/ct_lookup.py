#!/usr/bin/env python3
"""Look up the pack's hosted (non CT-archive) tracks in Wiimm's CT archive. Runs on GitHub Actions
(.github/workflows/ct-lookup.yml), which can reach ct.wiimm.de. Prints one block per track; read it in the job log."""
import html, json, re, sys, time, urllib.parse, urllib.request
UA = {'User-Agent': 'Kartaclysm-track-lookup (github.com/franxakathegoat-boop/kartaclysm)'}
def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60) as r: return r.read().decode('utf-8', 'replace')
def text(h): return re.sub(r'[ \t]+', ' ', html.unescape(re.sub(r'<[^>]+>', ' ', re.sub(r'(?s)<(script|style).*?</\1>', '', h))))
if '--discover' in sys.argv:
    h = get('https://ct.wiimm.de/')
    print('FORMS:', re.findall(r'(?s)<form.*?</form>', h)[:3])
    print('LINKS:', sorted(set(re.findall(r'href="([^"]+)"', h)))[:80])
    sys.exit()
