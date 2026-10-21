#!/usr/bin/env python3
"""Find Wiimm CT archive IDs for the pack's hosted (non CT-archive) tracks. ct.wiimm.de refuses connections from
GitHub Actions, so the IDs come from the Custom Mario Kart wiki (mkwiiki.org), whose track pages link to the archive.
Runs on GitHub Actions (.github/workflows/ct-lookup.yml); prints one JSON line per track in the job log."""
import html, json, re, sys, time, urllib.parse, urllib.request
UA = {'User-Agent': 'Kartaclysm-track-lookup (github.com/franxakathegoat-boop/kartaclysm)'}
def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60) as r: return r.read().decode('utf-8', 'replace')
def text(h): return re.sub(r'[ \t]+', ' ', html.unescape(re.sub(r'<[^>]+>', ' ', re.sub(r'(?s)<(script|style).*?</\1>', '', h))))
if '--probe' in sys.argv:
    import socket
    for host in ('ct.wiimm.de', 'wiimm.de', 'szs.wiimm.de', 'ct.wiimm.de'):
        try: print(host, 'resolves to', sorted({a[4][0] for a in socket.getaddrinfo(host, 443)}))
        except Exception as e: print(host, 'DNS error', e)
    for url in ('https://ct.wiimm.de/', 'http://ct.wiimm.de/', 'https://wiimm.de/', 'https://szs.wiimm.de/', 'https://ct.wiimm.de/i/10780', 'https://mkwiiki.org/wiki/Wii_U_Water_Park', 'https://wiki.tockdom.com/wiki/Wii_U_Water_Park'):
        try: print(url, '->', len(get(url)), 'bytes OK')
        except Exception as e: print(url, '->', repr(e)[:160])
        time.sleep(2)
    sys.exit()
if '--discover' in sys.argv:
    h = get('https://ct.wiimm.de/')
    print('FORMS:', re.findall(r'(?s)<form.*?</form>', h)[:3])
    print('LINKS:', sorted(set(re.findall(r'href="([^"]+)"', h)))[:80])
    sys.exit()

WIKI = 'https://mkwiiki.org'
def links(h): return [(html.unescape(u), text(t).strip()) for u, t in re.findall(r'(?s)<a [^>]*href="([^"]+)"[^>]*>(.*?)</a>', h)]
def page(title):
    try: return get(WIKI + '/wiki/' + urllib.parse.quote(title.replace(' ', '_')))
    except Exception as e: return ''
def ct_ids(h): return sorted({int(i) for i in re.findall(r'ct\.wiimm\.de/(?:i/|d/|\?t=|\?id=)(\d+)', h)})
for t in json.load(open('pack/build/tracks.json')):
    if t.get('ct'): continue
    names = [a.strip().lower() for a in t['authors'].split(',')]
    main = page(t['name']); time.sleep(1.5)
    subs = sorted({u for u, _ in links(main) if u.startswith('/wiki/' + urllib.parse.quote(t['name'].replace(' ', '_'))) and '(' in u})
    found = []
    for u in subs:
        title = urllib.parse.unquote(u[len('/wiki/'):]).replace('_', ' ')
        h = page(title); time.sleep(1.5)
        body = text(h).lower()
        found.append({'page': title, 'author_match': [a for a in names if a in body], 'ct': ct_ids(h),
                      'other': sorted({x for x, _ in links(h) if re.search(r'mediafire|drive\.google|mega\.nz|gamebanana|dropbox|github\.com/.+/releases|icedrive', x)})[:6]})
    print(json.dumps({'slot': t['slotfile'], 'name': t['name'], 'authors': t['authors'], 'main_page': bool(main), 'main_ct': ct_ids(main), 'versions': found}), flush=True)
