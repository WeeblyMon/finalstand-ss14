import glob, os, sys, collections, json
import yaml
from PIL import Image

SRC = sorted(glob.glob(os.path.join(os.path.dirname(__file__), 'rmc', '*.yml')))
TEX = os.path.join(os.path.dirname(__file__), 'dl', 'tiles')

records = []
for path in SRC:
    with open(path, encoding='utf-8') as fh:
        docs = yaml.safe_load(fh)
    for node in docs:
        if node.get('type') != 'tile':
            continue
        node['_file'] = os.path.basename(path)
        records.append(node)

by_id = {r['id']: r for r in records}


def inherited(pid, key, depth=0):
    r = by_id.get(pid)
    if r is None or depth > 30:
        return None
    if key in r:
        return r[key]
    p = r.get('parent')
    return inherited(p, key, depth + 1) if isinstance(p, str) else None


concrete = [r for r in records if not r.get('abstract')]
abstract = [r for r in records if r.get('abstract')]

print(f'records={len(records)} concrete={len(concrete)} abstract={len(abstract)}')
print('abstract ids:', ', '.join(r['id'] for r in abstract))
print()

keys = collections.Counter()
for r in records:
    for k in r:
        if k not in ('type', '_file'):
            keys[k] += 1
print('keys:', dict(keys))
print()

sizes = collections.defaultdict(list)
missing = []
for r in concrete:
    sp = inherited(r['id'], 'sprite')
    if not sp:
        missing.append(r['id'])
        continue
    base = sp.split('/')[-1]
    fp = os.path.join(TEX, base)
    if not os.path.exists(fp):
        missing.append(f"{r['id']} -> {base} MISSING FILE")
        continue
    w, h = Image.open(fp).size
    var = inherited(r['id'], 'variants') or 1
    sizes[(w, h, var)].append(r['id'])

print('sprite (w,h,variants) -> count   [expect w == 32*variants, h == 32]')
for k in sorted(sizes):
    w, h, var = k
    ok = 'ok' if (w == 32 * var and h == 32) else 'ODD'
    print(f'  {k} {len(sizes[k]):4d}  {ok}')
    if ok == 'ODD':
        for i in sizes[k]:
            print(f'        {i}  sprite={inherited(i,"sprite").split("/")[-1]}')
print()
if missing:
    print('MISSING/NO SPRITE:', missing)
    print()

used = set()
for r in records:
    sp = r.get('sprite')
    if sp:
        used.add(sp.split('/')[-1])
    es = r.get('edgeSprites') or {}
    for v in es.values():
        used.add(v.split('/')[-1])
have = {f for f in os.listdir(TEX)}
print(f'textures referenced={len(used)} downloaded={len(have)}')
print('referenced but not downloaded:', sorted(used - have))
print('downloaded but unreferenced  :', len(have - used))
for f in sorted(have - used):
    print('   unused:', f)

json.dump([{k: v for k, v in r.items() if k != 'type'} for r in records],
          open(os.path.join(os.path.dirname(__file__), 'hybrisa_parsed.json'), 'w'),
          indent=1)
