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

ENT_ROOT = os.path.join(REPO, 'Resources', 'Prototypes', '_FinalStand', 'Entities')
DECAL = os.path.join(REPO, 'Resources', 'Prototypes', '_FinalStand', 'Decals', 'hybrisa.yml')

RULES = [
    ('FSPaletteStreet', 'fs-palette-street', 'Street', 40, [
        (r'^FS(Floodlight|Streetlight|EngineerShipLight)', 'Lights', 10),
        (r'^FSOverheadRoadsign', 'Road Signs', 20),
        (r'^FSOverheadBillboard', 'Billboards', 30),
        (r'^FS(PropVehicle|CarPileup)', 'Vehicles', 40),
        (r'^FS\w*Sign(Alt|Fixed)?\d*$', 'Landing Signs', 50),
        (r'^FS(Bench|Planter|Phonebox|BrokenPhonebox)', 'Street Furniture', 60),
        (r'^FS(PropTrash|Trash|Disposal|Condisposal|PropBananaPeel|PropCuppaJoe)', 'Trash', 70),
        (r'^FS(Barrier|DeployableBarrier)', 'Road Barriers', 80),
    ]),
    ('FSPaletteBuilding', 'fs-palette-building', 'Building', 50, [
        (r'^FSWall', 'Walls', 10),
        (r'^FSWindowFrame', 'Window Frames', 20),
        (r'^FSWindow', 'Windows', 30),
        (r'^FSPodDoorButton', 'Door Controls', 40),
        (r'^FSPodDoor', 'Lockdown Doors', 50),
        (r'^FSShutter', 'Shutters', 60),
        (r'^FSElevatorDoor', 'Elevator Doors', 70),
        (r'^FSFence', 'Fences', 80),
        (r'^FSGrille', 'Grilles', 90),
        (r'^FSFogWall', 'Map Boundary', 100),
    ]),
    ('FSPaletteIndustrial', 'fs-palette-industrial', 'Industrial', 60, [
        (r'^FSCatwalk', 'Catwalks', 10),
        (r'^FSPlatform', 'Platforms', 20),
        (r'^FS\w*Stairs', 'Stairs', 30),
        (r'^FSContainer|^FSCrate|^FSStorageContainer', 'Containers', 40),
        (r'^FS\w*Lattice', 'Lattices', 50),
        (r'^FSOverheadPipe|^FSOverheadFuel', 'Overhead Pipes', 60),
        (r'^FSDrill', 'Drills', 70),
        (r'^FS(Monorail|Railroad)', 'Rails', 80),
    ]),
    ('FSPaletteInterior', 'fs-palette-interior', 'Interior', 70, [
        (r'^FS\w*Carpet|^FS\w*Rug', 'Carpets & Rugs', 10),
        (r'^FS\w*Divider', 'Dividers', 20),
    ]),
]

DECAL_RULES = [
    (r'^FSDecalHybrisa(Road|Red|Half)', 'Road Lines', 10),
    (r'^FSDecalHybrisa(Z|Stop|Checkpoint)', 'Crossings', 20),
    (r'^FSDecalHybrisa(Warning|Worker)', 'Hazard Markings', 30),
    (r'^FSDecalHybrisaEngi', 'Industrial', 40),
    (r'^FSDecalHybrisaStone', 'Stone & Rubble', 50),
    (r'^FSDecalHybrisaTrash', 'Litter', 60),
    (r'^FSDecalHybrisa(WEYA|Casino)', 'Branding', 70),
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
    m = re.match(r'^FS(?:Carpet|Rug)([A-Za-z]+?)\d*$', eid)
    if m:
        return m.group(1)
    m = re.match(r'^FSContainer([A-Za-z]+?)\d*$', eid)
    if m:
        return m.group(1)
    m = re.match(r'^FS([A-Za-z]+?)\d*$', eid)
    return m.group(1) if m else None


entities, decals = [], []
for path in sorted(glob.glob(os.path.join(ENT_ROOT, '**', 'hybrisa_*.yml'), recursive=True)) + \
        [os.path.join(ENT_ROOT, 'Structures', 'Walls', 'fog_wall.yml')]:
    if not os.path.exists(path):
        continue
    for e in yaml.safe_load(open(path, encoding='utf-8')):
        if e.get('type') == 'Tag' or e.get('abstract'):
            continue
        entities.append(e['id'])
for d in yaml.safe_load(open(DECAL, encoding='utf-8')):
    if not d.get('abstract'):
        decals.append(d['id'])

print(f'entities={len(entities)} decals={len(decals)}')

blocks, labels, placed = [], {}, set()


def block(pid, loc, order, root=False, children=(), entities_=(), decals_=()):
    out = ['- type: fsMapPalette', f'  id: {pid}', f'  name: {loc}']
    if root:
        out.append('  root: true')
    out.append(f'  order: {order}')
    if children:
        out.append('  children:')
        out += [f'  - {c}' for c in children]
    if entities_:
        out.append('  entities:')
        out += [f'  - {e}' for e in entities_]
    if decals_:
        out.append('  decals:')
        out += [f'  - {e}' for e in decals_]
    blocks.append('\n'.join(out))


def emit(cid, cloc, clabel, corder, members, kind):
    labels[cloc] = clabel
    if len(members) <= MAX_LEAVES:
        block(cid, cloc, corder, **{kind: sorted(members)})
        return
    groups = collections.OrderedDict()
    for m in sorted(members):
        groups.setdefault(split_key(m) or 'Other', []).append(m)

    singletons = sum(1 for v in groups.values() if len(v) < 2)
    if len(groups) > 20 or singletons * 2 > len(groups):
        block(cid, cloc, corder, **{kind: sorted(members)})
        return

    kids = []
    for n, (k, v) in enumerate(sorted(groups.items()), start=1):
        kids.append((f'{cid}{k}', f'{cloc}-{k.lower()}', k, n * 10, v))
    block(cid, cloc, corder, children=[k[0] for k in kids])
    for sid, sloc, slabel, sorder, v in kids:
        labels[sloc] = slabel
        block(sid, sloc, sorder, **{kind: sorted(v)})


for root_id, root_loc, root_label, root_order, cats in RULES:
    labels[root_loc] = root_label
    ids = []
    for i, (pat, clabel, corder) in enumerate(cats):
        cid = f'{root_id}{clabel.split()[0].replace("&", "")}{i}'
        ids.append((cid, pat, clabel, corder))
    block(root_id, root_loc, root_order, root=True, children=[i[0] for i in ids])
    for cid, pat, clabel, corder in ids:
        members = [e for e in entities if re.match(pat, e) and e not in placed]
        placed |= set(members)
        emit(cid, f'{root_id.lower()}-{corder}', clabel, corder, members, 'entities_')

labels['fs-palette-decals'] = 'Decals'
dec_ids = [(f'FSPaletteDecals{i}', pat, lab, order) for i, (pat, lab, order) in enumerate(DECAL_RULES)]
dplaced = set()
dec_groups = []
for cid, pat, lab, order in dec_ids:
    members = [d for d in decals if re.match(pat, d) and d not in dplaced]
    if not members:
        continue
    dplaced |= set(members)
    dec_groups.append((cid, f'fs-palette-decals-{order}', lab, order, members))
rest = [d for d in decals if d not in dplaced]
if rest:
    dec_groups.append(('FSPaletteDecalsOther', 'fs-palette-decals-other', 'Other', 999, rest))
block('FSPaletteDecals', 'fs-palette-decals', 80, root=True,
      children=[g[0] for g in dec_groups])
for cid, cloc, lab, order, members in dec_groups:
    emit(cid, cloc, lab, order, members, 'decals_')

unplaced = [e for e in entities if e not in placed]
if unplaced:
    print(f'UNPLACED ({len(unplaced)}):', unplaced[:25])
    sys.exit(1)

target = os.path.join(REPO, 'Resources', 'Prototypes', '_FinalStand', 'Mapping', 'palette_entities.yml')
open(target, 'w', encoding='utf-8', newline='\n').write('\n\n'.join(blocks) + '\n')
print(f'palette_entities.yml: {len(blocks)} categories, {len(placed)} entities, {len(decals)} decals')

ftl = os.path.join(REPO, 'Resources', 'Locale', 'en-US', '_FinalStand', 'mapping.ftl')
base = [l for l in open(ftl, encoding='utf-8').read().splitlines()
        if l.startswith(('fs-map-palette', 'cmd-fspalette', 'fs-palette-ground', 'fs-palette-floors', 'fs-palette-hull')) or not l.strip()]
lines = [l for l in base if l.strip()]
lines += [f'{loc} = {lab}' for loc, lab in labels.items() if not any(l.startswith(f'{loc} =') for l in lines)]
open(ftl, 'w', encoding='utf-8', newline='\n').write('\n'.join(lines) + '\n')
print(f'mapping.ftl: {len(lines)} lines')
