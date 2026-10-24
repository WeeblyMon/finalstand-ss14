import json, os, re, csv, collections, shutil, sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = sys.argv[1]
TEX = os.path.join(HERE, 'dl', 'tiles')

records = json.load(open(os.path.join(HERE, 'hybrisa_parsed.json')))
by_id = {r['id']: r for r in records}


def inherited(pid, key, depth=0):
    r = by_id.get(pid)
    if r is None or depth > 30:
        return None
    if key in r:
        return r[key]
    p = r.get('parent')
    return inherited(p, key, depth + 1) if isinstance(p, str) else None


def chain(pid, depth=0):
    r = by_id.get(pid)
    out = [pid]
    if r is None or depth > 30:
        return out
    p = r.get('parent')
    if isinstance(p, str):
        out += chain(p, depth + 1)
    return out


GROUPS = {
    'ground_road':   dict(label='Ground/Road',   idp='FSFloorRoad',       loc='fs-tile-road',     out='hybrisa_ground.yml',  dir='Road',     tpl='road {b}'),
    'ground_walk':   dict(label='Ground/Walk',   idp='FSFloorWalk',       loc='fs-tile-walk',     out='hybrisa_ground.yml',  dir='Walk',     tpl='{b}'),
    'ground_paving': dict(label='Ground/Paving', idp='FSFloorPaving',     loc='fs-tile-paving',   out='hybrisa_ground.yml',  dir='Paving',   tpl='{b}'),
    'ground_raw':    dict(label='Ground/Raw',    idp='FSFloorGround',     loc='fs-tile-ground',   out='hybrisa_ground.yml',  dir='Ground',   tpl='{b}'),
    'floor_metal':   dict(label='Floors/Metal',  idp='FSFloorTile',       loc='fs-tile-floor',    out='hybrisa_floors.yml',  dir='Floor',    tpl='{b} floor'),
    'floor_soft':    dict(label='Floors/Soft',   idp='FSFloorSoft',       loc='fs-tile-soft',     out='hybrisa_floors.yml',  dir='Soft',     tpl='{b}'),
    'hull_engineer': dict(label='Hull/Engineer', idp='FSPlatingEngineer', loc='fs-tile-engineer', out='hybrisa_plating.yml', dir='Engineer', tpl='engineer {b}'),
    'hull_deck':     dict(label='Hull/Deck',     idp='FSPlatingDeck',     loc='fs-tile-deck',     out='hybrisa_plating.yml', dir='Deck',     tpl='{b}'),
}

ORDER = ['ground_road', 'ground_walk', 'ground_paving', 'ground_raw',
         'floor_metal', 'floor_soft', 'hull_engineer', 'hull_deck']


def classify(s):
    if 'EngineerShip' in s:
        return 'hull_engineer'
    if 'Dropship' in s or 'SpacePort' in s:
        return 'hull_deck'
    if 'Sidewalk' in s:
        return 'ground_walk'
    if 'Asphalt' in s:
        return 'ground_road'
    if re.search(r'Dirt|Shale|Underground', s):
        return 'ground_raw'
    if re.search(r'Cement|Ramp|Stripe|Emblem', s):
        return 'ground_paving'
    if re.search(r'Carpet|Wood|Dingy|Rug', s):
        return 'floor_soft'
    return 'floor_metal'


REWRITE = {
    'ground_road': [('Asphalt', '')],
    'ground_walk': [('AsphaltSidewalk', 'Kerb'), ('Sidewalk', 'Sidewalk')],
    'hull_engineer': [('EngineerShip', '')],
    'hull_deck': [('Blue', '')],
}
STRIP_LEAD = ['RMCPlanetHybrisa', 'RMCFloorHybrisa']
DIRS = [('SouthEast', 'SE'), ('SouthWest', 'SW'), ('NorthEast', 'NE'), ('NorthWest', 'NW'),
        ('South', 'S'), ('North', 'N'), ('East', 'E'), ('West', 'W')]
DIR_WORD = {'N': 'north', 'S': 'south', 'E': 'east', 'W': 'west',
            'NE': 'north-east', 'NW': 'north-west', 'SE': 'south-east', 'SW': 'south-west'}

SKIP = {'RMCPlanetHybrisaUndergroundWeedable', 'RMCPlanetHybrisaDirtPlating'}

BODY_FIX = [
    (r'Emblem64A$', 'EmblemNW'), (r'Emblem64B$', 'EmblemNE'),
    (r'Emblem64C$', 'EmblemSW'), (r'Emblem64D$', 'EmblemSE'),
    (r'^Underground$', 'Underfloor'),
    (r'^LinesRed$', 'HazardRed'), (r'^LinesYellow$', 'HazardYellow'),
    (r'^SpacePort', 'Spaceport'),
    (r'^GreenTile$', 'Green'),
    (r'^BlueTileCheck$', 'BlueCheck'), (r'^BlueTile$', 'Blue'),
    (r'^GreenDingy$', 'GreenCarpetGrimy'),
    (r'RedMetalCuppaJoe$', 'RedMetalCafe'),
    (r'^(\d+)$', 'Floor' + chr(92) + '1'),
]

OVERRIDE_NAME = {
    'RMCFloorHybrisaEngineerShip': 'engineer ship deck',
    'RMCFloorHybrisaEngineerShipPlating': 'engineer ship underplating',
}


def body_of(rid, group):
    s = rid
    for p in STRIP_LEAD:
        if s.startswith(p):
            s = s[len(p):]
            break
    for w, repl in REWRITE.get(group, []):
        if s.startswith(w):
            s = repl + s[len(w):]
            break
    for lng, short in DIRS:
        s = re.sub(lng + r'(?=$|[A-Z0-9])', short, s)
    for pat, repl in BODY_FIX:
        s = re.sub(pat, repl, s)
    return s


def words(s):
    s = re.sub(r'([a-z0-9])([A-Z])', r'\1 \2', s)
    s = re.sub(r'([A-Z]+)([A-Z][a-z])', r'\1 \2', s)
    s = re.sub(r'([A-Za-z])(\d)', r'\1 \2', s)
    return [w for w in s.split() if w]


def slug_of(body):
    s = re.sub(r'([a-z0-9])([A-Z])', r'\1-\2', body)
    s = re.sub(r'([A-Za-z])(\d)', r'\1-\2', s)
    return re.sub(r'[^a-z0-9]+', '-', s.lower()).strip('-')


def fs_base(rid, group):
    ch = chain(rid)
    if 'RMCFloorHybrisaWoodBase' in ch:
        return 'FSTileBaseWood'
    if 'CMPlanetBase' in ch:
        return 'FSTileBaseGroundRaw' if group == 'ground_raw' else 'FSTileBaseGroundPaved'
    return 'FSTileBaseFloor'


rows = []
for r in records:
    if r.get('abstract') or r['id'] in SKIP:
        continue
    rid = r['id']
    g = classify(rid)
    G = GROUPS[g]
    body = body_of(rid, g)
    fs_id = G['idp'] + body
    ws = [DIR_WORD.get(w.upper(), w.lower()) for w in words(body)]
    human = re.sub(r'\s+', ' ', G['tpl'].format(b=' '.join(ws))).strip()
    human = OVERRIDE_NAME.get(rid, human)
    sl = slug_of(body)
    loc = f"{G['loc']}-{sl}" if sl else G['loc']
    src = inherited(rid, 'sprite').split('/')[-1]
    dst = f"{G['dir']}/{sl or G['dir'].lower()}.png"
    rows.append(dict(rmc_id=rid, group=g, group_label=G['label'], fs_id=fs_id, loc=loc,
                     human=human, base=fs_base(rid, g), tex_src=src, tex_dst=dst,
                     outfile=G['out'],
                     variants=inherited(rid, 'variants'),
                     placement=inherited(rid, 'placementVariants'),
                     edge=r.get('edgeSprites') or inherited(rid, 'edgeSprites'),
                     edgeprio=r.get('edgeSpritePriority', inherited(rid, 'edgeSpritePriority'))))

for key, fld in (('fs_id', 'fs_id'), ('human', 'human'), ('loc', 'loc'), ('tex_dst', 'tex_dst')):
    d = [k for k, v in collections.Counter(r[fld] for r in rows).items() if v > 1]
    if d:
        print('FATAL duplicate', fld, d)
        sys.exit(1)
print(f'{len(rows)} tiles, no duplicates in fs_id / human / loc / tex_dst')

EDGE_DST = {'layer1_single_edge.png': 'Ground/edge-single.png',
            'layer1_double_edge.png': 'Ground/edge-double.png'}

TEXROOT = os.path.join(REPO, 'Resources', 'Textures', '_FinalStand', 'Tiles')
copied = 0
for r in rows:
    d = os.path.join(TEXROOT, r['tex_dst'].replace('/', os.sep))
    os.makedirs(os.path.dirname(d), exist_ok=True)
    shutil.copyfile(os.path.join(TEX, r['tex_src']), d)
    copied += 1
for s, d in EDGE_DST.items():
    p = os.path.join(TEXROOT, d.replace('/', os.sep))
    os.makedirs(os.path.dirname(p), exist_ok=True)
    shutil.copyfile(os.path.join(TEX, s), p)
    copied += 1
print(f'copied {copied} textures')

BASE_YML = """- type: tile
  abstract: true
  id: FSTileBaseFloor
  baseTurf: Plating
  isSubfloor: false
  deconstructTools: [ Prying ]
  footstepSounds:
    collection: FootstepFloor
  itemDrop: FloorTileItemSteel
  heatCapacity: 10000

- type: tile
  abstract: true
  parent: FSTileBaseFloor
  id: FSTileBaseWood
  footstepSounds:
    collection: FootstepWood
  barestepSounds:
    collection: BarestepWood
  itemDrop: FloorTileItemWood

- type: tile
  abstract: true
  id: FSTileBaseGroundPaved
  isSubfloor: true
  deconstructTools: [ ]
  footstepSounds:
    collection: FootstepFloor
  heatCapacity: 10000
  weather: true
  indestructible: true

- type: tile
  abstract: true
  id: FSTileBaseGroundRaw
  isSubfloor: true
  deconstructTools: [ ]
  footstepSounds:
    collection: FootstepGrass
  heatCapacity: 10000
  weather: true
  indestructible: true
"""

PROTOROOT = os.path.join(REPO, 'Resources', 'Prototypes', '_FinalStand', 'Tiles')
os.makedirs(PROTOROOT, exist_ok=True)
open(os.path.join(PROTOROOT, 'base.yml'), 'w', encoding='utf-8', newline='\n').write(BASE_YML)

buckets = collections.OrderedDict()
for g in ORDER:
    buckets.setdefault(GROUPS[g]['out'], []).extend([r for r in rows if r['group'] == g])

for out, items in buckets.items():
    parts = []
    last = None
    for r in items:
        if r['group_label'] != last:
            last = r['group_label']
        b = [f"- type: tile",
             f"  parent: {r['base']}",
             f"  id: {r['fs_id']}",
             f"  name: {r['loc']}",
             f"  sprite: /Textures/_FinalStand/Tiles/{r['tex_dst']}"]
        if r['variants']:
            b.append(f"  variants: {r['variants']}")
        if r['placement']:
            b.append("  placementVariants:")
            for v in r['placement']:
                b.append(f"  - {v}")
        if r['edgeprio'] is not None:
            b.append(f"  edgeSpritePriority: {r['edgeprio']}")
        if r['edge']:
            b.append("  edgeSprites:")
            for k, v in r['edge'].items():
                b.append(f"    {k}: /Textures/_FinalStand/Tiles/{EDGE_DST[v.split('/')[-1]]}")
        parts.append('\n'.join(b))
    open(os.path.join(PROTOROOT, out), 'w', encoding='utf-8', newline='\n').write('\n\n'.join(parts) + '\n')
    print(f'  {out}: {len(items)} tiles')

ftl = []
for g in ORDER:
    for r in [x for x in rows if x['group'] == g]:
        ftl.append(f"{r['loc']} = {r['human']}")
    ftl.append('')
open(os.path.join(REPO, 'Resources', 'Locale', 'en-US', '_FinalStand', 'tiles.ftl'),
     'w', encoding='utf-8', newline='\n').write('\n'.join(ftl).rstrip() + '\n')
print(f'wrote tiles.ftl with {len(rows)} strings')

TOOLS = os.path.join(REPO, 'Tools', '_FinalStand', 'hybrisa')
os.makedirs(TOOLS, exist_ok=True)
with open(os.path.join(TOOLS, 'manifest.csv'), 'w', newline='', encoding='utf-8') as fh:
    w = csv.DictWriter(fh, fieldnames=['rmc_id', 'group', 'group_label', 'fs_id', 'loc',
                                       'human', 'base', 'tex_src', 'tex_dst', 'outfile'])
    w.writeheader()
    for r in rows:
        w.writerow({k: r[k] for k in w.fieldnames})
print('wrote manifest.csv')
