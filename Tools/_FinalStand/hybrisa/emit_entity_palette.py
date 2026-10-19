import os, re, sys, glob, collections
import yaml

REPO = sys.argv[1]


def anytag(loader, suffix, node):
    if isinstance(node, yaml.MappingNode):
        return loader.construct_mapping(node, deep=True)
    if isinstance(node, yaml.SequenceNode):
        return loader.construct_sequence(node, deep=True)
    return loader.construct_scalar(node)


yaml.SafeLoader.add_multi_constructor('!type:', anytag)

SOURCES = [
    'Entities/Structures/hybrisa_lights.yml',
    'Entities/Structures/hybrisa_poddoors.yml',
    'Entities/Structures/hybrisa_road_signs.yml',
    'Entities/Structures/hybrisa_prop_vehicles.yml',
    'Entities/Structures/Walls/hybrisa_walls.yml',
    'Entities/Structures/Walls/fog_wall.yml',
    'Entities/Structures/Windows/hybrisa_windows.yml',
]

ROOTS = [
    ('FSPaletteStreet', 'fs-palette-street', 'Street', 40, [
        (r'^FS(Floodlight|Streetlight|EngineerShipLight)', 'FSPaletteStreetLights',
         'fs-palette-street-lights', 'Lights', 10),
        (r'^FSOverheadRoadsign', 'FSPaletteStreetSignage',
         'fs-palette-street-signage', 'Road Signs', 20),
        (r'^FSOverheadBillboard', 'FSPaletteStreetBillboards',
         'fs-palette-street-billboards', 'Billboards', 30),
        (r'^FS(PropVehicle|CarPileup)', 'FSPaletteStreetVehicles',
         'fs-palette-street-vehicles', 'Vehicles', 40),
    ]),
    ('FSPaletteBuilding', 'fs-palette-building', 'Building', 50, [
        (r'^FSWall', 'FSPaletteBuildingWalls', 'fs-palette-building-walls', 'Walls', 10),
        (r'^FSWindowFrame', 'FSPaletteBuildingFrames', 'fs-palette-building-frames', 'Window Frames', 20),
        (r'^FSWindow', 'FSPaletteBuildingWindows', 'fs-palette-building-windows', 'Windows', 30),
        (r'^FSPodDoor', 'FSPaletteBuildingDoors', 'fs-palette-building-doors', 'Lockdown Doors', 40),
        (r'^FSFogWall', 'FSPaletteBuildingBoundary', 'fs-palette-building-boundary', 'Map Boundary', 50),
    ]),
]

MAX_LEAVES = 40


def split_key(eid):
    m = re.match(r'^FSPropVehicleMonoSupron(.+)$', eid)
    if m:
        k = re.sub(r'Flipped$', '', m.group(1))
        k = re.sub(r'Damaged\d+$', '', k)
        k = re.sub(r'Destroyed$', '', k)
        return k or 'Plain'
    m = re.match(r'^FSOverheadBillboard([A-Za-z]+?)\d*$', eid)
    if m:
        return m.group(1)
    if re.match(r'^FSOverheadBillboard\d+$', eid):
        return 'Standard'
    return None


ids = []
for rel in SOURCES:
    p = os.path.join(REPO, 'Resources', 'Prototypes', '_FinalStand', rel)
    for e in yaml.safe_load(open(p, encoding='utf-8')):
        if e.get('abstract'):
            continue
        ids.append(e['id'])

print(f'placeable entities found: {len(ids)}')

blocks = []
labels = {}
placed = set()


def block(pid, loc, order, root=False, children=(), entities=()):
    out = ['- type: fsMapPalette', f'  id: {pid}', f'  name: {loc}']
    if root:
        out.append('  root: true')
    out.append(f'  order: {order}')
    if children:
        out.append('  children:')
        out += [f'  - {c}' for c in children]
    if entities:
        out.append('  entities:')
        out += [f'  - {e}' for e in entities]
    blocks.append('\n'.join(out))


for root_id, root_loc, root_label, root_order, cats in ROOTS:
    labels[root_loc] = root_label
    block(root_id, root_loc, root_order, root=True, children=[c[1] for c in cats])

    for pat, cid, cloc, clabel, corder in cats:
        labels[cloc] = clabel
        members = [i for i in ids if re.match(pat, i) and i not in placed]
        placed |= set(members)
        members.sort()

        if len(members) <= MAX_LEAVES:
            block(cid, cloc, corder, entities=members)
            continue

        groups = collections.OrderedDict()
        for i in members:
            groups.setdefault(split_key(i) or 'Other', []).append(i)

        kids = []
        for n, (k, v) in enumerate(sorted(groups.items()), start=1):
            sid = f'{cid}{k}'
            sloc = f'{cloc}-{k.lower()}'
            labels[sloc] = k
            kids.append((sid, sloc, n * 10, v))
        block(cid, cloc, corder, children=[k[0] for k in kids])
        for sid, sloc, sorder, v in kids:
            block(sid, sloc, sorder, entities=sorted(v))

unplaced = [i for i in ids if i not in placed]
if unplaced:
    print('UNPLACED:', unplaced)
    sys.exit(1)

target = os.path.join(REPO, 'Resources', 'Prototypes', '_FinalStand', 'Mapping', 'palette_entities.yml')
open(target, 'w', encoding='utf-8', newline='\n').write('\n\n'.join(blocks) + '\n')
print(f'palette_entities.yml: {len(blocks)} categories, {len(placed)} entity references')

ftl = os.path.join(REPO, 'Resources', 'Locale', 'en-US', '_FinalStand', 'mapping.ftl')
existing = open(ftl, encoding='utf-8').read().rstrip('\n')
add = [f'{loc} = {label}' for loc, label in labels.items() if f'{loc} =' not in existing]
if add:
    open(ftl, 'w', encoding='utf-8', newline='\n').write(existing + '\n\n' + '\n'.join(add) + '\n')
print(f'mapping.ftl: +{len(add)} labels')
