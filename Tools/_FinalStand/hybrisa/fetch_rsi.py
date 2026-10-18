import json, os, sys, urllib.request

RAW = 'https://raw.githubusercontent.com/RMC-14/RMC-14'


def get(url):
    with urllib.request.urlopen(url) as r:
        return r.read()


def fetch(ref, src, dst):
    os.makedirs(dst, exist_ok=True)
    meta = get(f'{RAW}/{ref}/{src}/meta.json')
    with open(os.path.join(dst, 'meta.json'), 'wb') as fh:
        fh.write(meta)
    states = [s['name'] for s in json.loads(meta.decode('utf-8-sig'))['states']]
    for s in states:
        with open(os.path.join(dst, f'{s}.png'), 'wb') as fh:
            fh.write(get(f'{RAW}/{ref}/{src}/{s}.png'))
    return states


if __name__ == '__main__':
    ref, src, dst = sys.argv[1], sys.argv[2], sys.argv[3]
    states = fetch(ref, src, dst)
    print(f'{dst}: {len(states)} states')
