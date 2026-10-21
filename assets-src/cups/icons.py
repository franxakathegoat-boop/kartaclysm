# Kartaclysm cup icons v4: one 3D object per cup from Microsoft's Fluent Emoji (MIT, fluent/LICENSE), styled like
# Mario Kart Wii's own cup icons (fills the tile, dark outline, soft shadow). Usage: python3 assets-src/cups/icons.py [out.png]
# Sheet layout (LE-CODE, 128x128 per icon): 0-1 arrows, 2-9 the 8 original cups, 10.. one icon per custom cup, then a spare.
# Slots 0-9 are placeholders: prepare.ps1 replaces them on the player's PC with the real Mario Kart icons that
# Wiimm's tools carry (wimgt ':CUP-IMAGE=arrows,swapped'), so no Nintendo art is in this repo.
import json, os, sys
from PIL import Image, ImageEnhance, ImageFilter
S = 128
ICONS = {'Shockwave': 'collision', 'Riptide': 'shark', 'Inferno': 'fire', 'Avalanche': 'snow-capped_mountain',
  'Rift': 'crystal_ball', 'Monsoon': 'umbrella_with_rain_drops', 'Mirage': 'cactus', 'Tremor': 'hammer', 'Undertow': 'octopus',
  'Sandstorm': 'hourglass_done', 'Wildfire': 'phoenix_bird', 'Blizzard': 'snowflake', 'Tempest': 'ring_buoy', 'Maelstrom': 'cyclone',
  'Eclipse': 'new_moon_face', 'Cyclone': 'tornado', 'Aftershock': 'bomb', 'Downpour': 'cloud_with_rain', 'Meteor': 'comet',
  'Supernova': 'sparkles', 'Typhoon': 'sailboat', 'Faultline': 'brick', 'Tsunami': 'water_wave', 'Landslide': 'rock',
  'Firestorm': 'dragon_face', 'Whiteout': 'snowman', 'Overload': 'battery', 'Singularity': 'magnet', 'Nebula': 'ringed_planet',
  'Hailstorm': 'ice', 'Thunderhead': 'cloud_with_lightning', 'Glitch': 'alien_monster', 'Kartaclysm': 'globe_showing_europe-africa',
  'Eruption': 'volcano'}
SPARE = 'trophy'
def icon(name, fill=0.88, outline=0.02):
  W = S * 4  # drawn at 4x, scaled down at the end
  im = Image.open(os.path.join('assets-src/cups/fluent', name + '_3d.png')).convert('RGBA'); im = im.crop(im.getbbox())
  k = W * fill / max(im.size); im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
  rgb = ImageEnhance.Contrast(ImageEnhance.Color(im.convert('RGB')).enhance(1.18)).enhance(1.06); rgb.putalpha(im.getchannel('A')); im = rgb
  art = Image.new('RGBA', (W, W), (0, 0, 0, 0)); art.alpha_composite(im, ((W - im.width) // 2, (W - im.height) // 2))
  r = round(W * outline); a = art.getchannel('A').point(lambda v: 255 if v > 40 else 0)
  ring = a.filter(ImageFilter.GaussianBlur(r * .6)).point(lambda v: 255 if v > 8 else 0).filter(ImageFilter.MaxFilter(2 * (r // 2) + 1)).filter(ImageFilter.GaussianBlur(1.5))
  out = Image.new('RGBA', (W, W), (0, 0, 0, 0))
  sh = Image.new('RGBA', (W, W), (0, 0, 0, 0)); sh.putalpha(ring.point(lambda v: v * .4).filter(ImageFilter.GaussianBlur(W * .02))); out.alpha_composite(sh, (0, round(W * .022)))
  ol = Image.new('RGBA', (W, W), (24, 16, 36, 0)); ol.putalpha(ring.point(lambda v: v * .92)); out.alpha_composite(ol)
  out.alpha_composite(art)
  return out.resize((S, S), Image.LANCZOS)
if __name__ == '__main__':
  os.chdir(os.path.dirname(os.path.abspath(__file__)) + '/../..')
  cups = []
  for t in json.load(open('pack/build/tracks.json')):
    n = t['cupname'].replace(' Cup', '')
    if n not in cups: cups.append(n)
  missing = [n for n in cups if n not in ICONS]
  if missing: sys.exit('no icon for cup(s): ' + ', '.join(missing))
  v1 = Image.open('assets-src/cups/cup-icons-v1.png').convert('RGBA'); tile = lambda i: v1.crop((0, i * S, S, i * S + S))
  out = Image.new('RGBA', (S, (11 + len(cups)) * S), (0, 0, 0, 0))
  # placeholder arrows + original cups, in LE-CODE's column order: Wii 1, Retro 1, Wii 2, Retro 2, ...
  for i, j in enumerate([0, 1, 2, 6, 3, 7, 4, 8, 5, 9]): out.paste(tile(j), (0, i * S))
  for i, n in enumerate(cups + [None]): out.paste(icon(ICONS[n] if n else SPARE), (0, (10 + i) * S))
  out.save(sys.argv[1] if len(sys.argv) > 1 else 'pack/build/cup-icons.png'); print(len(cups), 'cups')
