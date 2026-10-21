#!/usr/bin/env python3
"""Convert a WiiLink-WFC stage0 Gecko code (wfc-patcher-wii patch/build/RMCPD00.txt) into
 (1) a binary .gct for `wstrt patch --add-section` (LE-CODE/Dolphin build) and
 (2) Riivolution <memory> patches for the Pulsar build.
Supports the code types the stage0 patch uses: 04 (32-bit write), 06 (string write),
C6 (branch), 22 (if 32-bit equal) ... E2 (endif). Usage: wfc_gecko_convert.py RMCPD00.txt OUTDIR"""
import sys, struct, pathlib

src, out = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
words = [l.split() for l in src.read_text().splitlines() if l.strip() and not l.startswith(('*', '$', '#'))]
words = [(int(a, 16), int(b, 16)) for a, b in words]
out.mkdir(parents=True, exist_ok=True)
gct = b'\x00\xD0\xC0\xDE\x00\xD0\xC0\xDE' + b''.join(struct.pack('>II', a, b) for a, b in words) + b'\xF0' + b'\x00' * 7
(out / 'kartaclysm-wfc.gct').write_bytes(gct)

patches, cond, i = [], None, 0
while i < len(words):
    a, b = words[i]; t = a >> 24 & 0xFE; addr = 0x80000000 | (a & 0x01FFFFFF); i += 1
    if t == 0x04:
        patches.append([addr, struct.pack('>I', b)])
    elif t == 0x06:
        n = b; nl = (n + 7) // 8; data = b''.join(struct.pack('>II', *w) for w in words[i:i + nl])[:n]; i += nl
        patches.append([addr, data])
    elif t == 0xC6:  # write `b target` at addr
        patches.append([addr, struct.pack('>I', 0x48000000 | ((b - addr) & 0x03FFFFFC))])
    elif t == 0x22:
        cond = (addr, b); continue
    elif t == 0xE2:
        cond = None; continue
    else:
        sys.exit(f'unsupported gecko code type {t:02X}')
    if cond and cond[0] == addr:  # guard the patch that overwrites the checked word
        patches[-1].append(struct.pack('>I', cond[1]))
xml = ['<!-- Kartaclysm WFC stage0 (generated from %s; do not edit) -->' % src.name]
for p in patches:
    orig = f' original="{p[2].hex().upper()}"' if len(p) > 2 else ''
    xml.append(f'<memory offset="0x{p[0]:08X}" value="{p[1].hex().upper()}"{orig} />')
(out / 'kartaclysm-wfc-riivo.xml').write_text('\n'.join(xml) + '\n')
print(f'{len(words)} gecko lines -> {len(gct)} byte gct, {len(patches)} riivolution patches')
