import csv, os, re, sys, collections

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = sys.argv[1]

ROOTS = [
    ('FSPaletteGround', 'fs-palette-ground', 'Ground', 10, [
        ('ground_road', 'FSPaletteGroundRoad', 'fs-palette-ground-road', 'Road', 10),
        ('ground_walk', 'FSPaletteGroundWalk', 'fs-palette-ground-walk', 'Kerb & Walkway', 20),
        ('ground_paving', 'FSPaletteGroundPaving', 'fs-palette-ground-paving', 'Open Paving', 30),
        ('ground_raw', 'FSPaletteGroundRaw', 'fs-palette-ground-raw', 'Raw Ground', 40),
    ]),
    ('FSPaletteFloors', 'fs-palette-floors', 'Floors', 20, [
        ('floor_metal', 'FSPaletteFloorsMetal', 'fs-palette-floors-metal', 'Metal & Tile', 10),
        ('floor_soft', 'FSPaletteFloorsSoft', 'fs-palette-floors-soft', 'Carpet & Wood', 20),
    ]),
    ('FSPaletteHull', 'fs-palette-hull', 'Hull & Deck', 30, [
        ('hull_engineer', 'FSPaletteHullEngineer', 'fs-palette-hull-engineer', 'Engineer Ship', 10),
        ('hull_deck', 'FSPaletteHullDeck', 'fs-palette-hull-deck', 'Spaceport & Dropship', 20),
    ]),
]

SPLIT = {
    'floor_metal': [
        (r'Beige', 'FSPaletteFloorsMetalBeige', 'fs-palette-floors-metal-beige', 'Beige', 10),
        (r'Black|Grey|Dark', 'FSPaletteFloorsMetalBlack', 'fs-palette-floors-metal-black', 'Black & Grey', 20),
        (r'Blue', 'FSPaletteFloorsMetalBlue', 'fs-palette-floors-metal-blue', 'Blue', 30),
        (r'Brown', 'FSPaletteFloorsMetalBrown', 'fs-palette-floors-metal-brown', 'Brown', 40),
        (r'Green', 'FSPaletteFloorsMetalGreen', 'fs-palette-floors-metal-green', 'Green', 50),
        (r'Red', 'FSPaletteFloorsMetalRed', 'fs-palette-floors-metal-red', 'Red', 60),
        (r'White', 'FSPaletteFloorsMetalWhite', 'fs-palette-floors-metal-white', 'White', 70),
        (r'Yellow|Orange', 'FSPaletteFloorsMetalYellow', 'fs-palette-floors-metal-yellow', 'Yellow & Orange', 80),
    ],
}

rows = list(csv.DictReader(open(os.path.join(HERE, 'manifest.csv'), encoding='utf-8')))
by_group = collections.defaultdict(list)
for r in rows:
    by_group[r['group']].append(r['fs_id'])

blocks = []
labels = {}
unplaced = []


def block(pid, loc, order, root=False, children=(), tiles=()):
    out = ['- type: fsMapPalette', f'  id: {pid}', f'  name: {loc}']
    if root:
        out.append('  root: true')
    out.append(f'  order: {order}')
    if children:
        out.append('  children:')
        out += [f'  - {c}' for c in children]
    if tiles:
        out.append('  tiles:')
        out += [f'  - {t}' for t in tiles]
    blocks.append('\n'.join(out))


for root_id, root_loc, root_label, root_order, cats in ROOTS:
    labels[root_loc] = root_label
    block(root_id, root_loc, root_order, root=True, children=[c[1] for c in cats])

    for group, cid, cloc, clabel, corder in cats:
        labels[cloc] = clabel
        tiles = by_group.get(group, [])

        if group not in SPLIT:
            block(cid, cloc, corder, tiles=tiles)
            continue

        buckets = collections.OrderedDict()
        for pat, sid, sloc, slabel, sorder in SPLIT[group]:
            buckets[sid] = (pat, sloc, slabel, sorder, [])

        for t in tiles:
            body = re.sub(r'^FSFloor(Tile|Soft|Road|Walk|Paving|Ground)|^FSPlating\w*?(?=[A-Z])', '', t)
            for sid, (pat, _, _, _, acc) in buckets.items():
                if re.match(f'(?:{pat})', body):
                    acc.append(t)
                    break
            else:
                unplaced.append(t)

        block(cid, cloc, corder, children=list(buckets.keys()))
        for sid, (_, sloc, slabel, sorder, acc) in buckets.items():
            labels[sloc] = slabel
            block(sid, sloc, sorder, tiles=acc)

if unplaced:
    print('UNPLACED TILES:', unplaced)
    sys.exit(1)

target = os.path.join(REPO, 'Resources', 'Prototypes', '_FinalStand', 'Mapping')
os.makedirs(target, exist_ok=True)
open(os.path.join(target, 'palette.yml'), 'w', encoding='utf-8', newline='\n').write('\n\n'.join(blocks) + '\n')

placed = sum(len(re.findall(r'^  - FS(?:Floor|Plating)', b, re.M)) for b in blocks)
print(f'palette.yml: {len(blocks)} categories, {placed} tile references')

ftl = ['fs-map-palette-title = Final Stand Palette',
       '',
       'cmd-fspalette-desc = Toggle the Final Stand map palette window.',
       'cmd-fspalette-help = Usage: fspalette',
       'cmd-fspalette-denied = You need to be an admin to use the map palette.',
       '']
for loc, label in labels.items():
    ftl.append(f'{loc} = {label}')
open(os.path.join(REPO, 'Resources', 'Locale', 'en-US', '_FinalStand', 'mapping.ftl'),
     'w', encoding='utf-8', newline='\n').write('\n'.join(ftl).rstrip() + '\n')
print(f'mapping.ftl: {len(labels)} category labels')
