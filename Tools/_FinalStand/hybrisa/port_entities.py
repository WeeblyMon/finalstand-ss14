import os, re, sys, glob, collections

DROP_COMPONENTS = {
    'RMCDropshipBlocked', 'RMCMesonsNonviewable', 'RMCWallExplosionDeletable',
    'RMCFusionCell', 'RMCFusionReactor', 'RMCPodDoor', 'RMCDoorButton',
    'XenoMapTracked', 'XenoToggleChargingDamage', 'XenoCrusherChargable',
    'DamageOnXenoScissors', 'DestroyOnXenoPierceScissor', 'VehicleSmashable',
    'Corrodible', 'DeleteOnExplosion', 'MinimapColor', 'Fog',
    'DestroyedByExplosionType', 'RMCReactorPoweredLight', 'PreventAttackLightOff',
    'RMCPowerReceiver', 'RMCOpenOnAlertLevel',
    'ImmuneToUnarmed', 'InsertBlacklist', 'InteractedBlacklist',
    'MeleeReceivedMultiplier', 'ProjectileDamageReceived', 'DamageMultiplierFlags',
}

PARENT_REMAP = {
    'CMBaseStructureCorrodible': 'BaseStructure',
    'CMBaseStructure': 'BaseStructure',
    'CMBaseWallInvincible': 'BaseWall',
    'CMWallMetal': 'WallSolid',
    'RMCBaseWallReinforced': 'WallReinforced',
    'CMBaseWindowIndestructible': 'BaseWindowStructure',
    'CMBaseWindow': 'BaseWindowStructure',
}

REF_REMAP = {
    'CMSheetMetal1': 'SheetSteel1',
    'CMSheetGlass1': 'SheetGlass1',
    'CMShardGlass': 'ShardGlass',
    'CMRodMetal': 'PartRodMetal1',
    'CMTileItemWood': 'FloorTileItemWood',
    'CMTileItemSteel': 'FloorTileItemSteel',
    'RMCCable': 'Cable',
}

ENUM_REMAP = {
    'enum.DisposalUnitVisualLayers.Unanchored': 'enum.DisposalUnitVisualLayers.Base',
    'enum.DisposalUnitVisualLayers.BaseCharging': 'enum.DisposalUnitVisualLayers.OverlayCharging',
    'enum.DisposalUnitVisualLayers.OverlayFlush': 'enum.DisposalUnitVisualLayers.OverlayFlushing',
    'enum.DisposalUnitVisualLayers.OverlayReady': 'enum.DisposalUnitVisualLayers.OverlayEngaged',
}


def strip_components(text):
    lines = text.splitlines()
    out, i = [], 0
    while i < len(lines):
        line = lines[i]
        m = re.match(r'^(\s*)- type: (\w+)\s*$', line)
        if m and m.group(2) in DROP_COMPONENTS:
            indent = len(m.group(1))
            i += 1
            while i < len(lines):
                nxt = lines[i]
                if nxt.strip() == '' or len(nxt) - len(nxt.lstrip()) <= indent:
                    break
                i += 1
            continue
        out.append(line)
        i += 1
    return '\n'.join(out) + '\n'


def split_damage(text):
    lines = text.splitlines()
    out, i = [], 0
    while i < len(lines):
        line = lines[i]
        m = re.match(r'^(\s*)- type: Damageable\s*$', line)
        if not m:
            out.append(line)
            i += 1
            continue
        indent = m.group(1)
        body, j = [], i + 1
        while j < len(lines):
            nxt = lines[j]
            if nxt.strip() == '' or len(nxt) - len(nxt.lstrip()) <= len(indent):
                break
            body.append(nxt)
            j += 1
        container = [b for b in body if 'damageContainer:' in b]
        rest = [b for b in body if 'damageContainer:' not in b]
        out.append(line)
        out.extend(rest)
        if container:
            out.append(f'{indent}- type: Injurable')
            out.extend(container)
        i = j
    return '\n'.join(out) + '\n'


def strip_fields(text):
    return re.sub(r'^\s*boundingBox: .*\n', '', text, flags=re.M)


def fix_bounds(text):
    def repl(m):
        l, b, r, t = (float(x) for x in m.groups())
        l, r = min(l, r), max(l, r)
        b, t = min(b, t), max(b, t)
        return f'bounds: "{l:g},{b:g},{r:g},{t:g}"'

    return re.sub(r'bounds: "(-?[\d.]+),\s*(-?[\d.]+),\s*(-?[\d.]+),\s*(-?[\d.]+)"', repl, text)


def convert(text):
    text = strip_components(text)
    text = split_damage(text)
    text = fix_bounds(text)
    text = strip_fields(text)
    text = text.replace('damageContainer: StructuralMarine', 'damageContainer: StructuralInorganic')
    text = text.replace('damageModifierSet: StructuralMarine', 'damageModifierSet: StructuralMetallic')
    for old, new in ENUM_REMAP.items():
        text = text.replace(old, new)

    for old, new in PARENT_REMAP.items():
        text = re.sub(rf'^(\s*parent: ){old}\s*$', rf'\1{new}', text, flags=re.M)
        text = re.sub(rf'^(\s*- ){old}\s*$', rf'\1{new}', text, flags=re.M)
    for old, new in REF_REMAP.items():
        text = re.sub(rf'\b{old}\b', new, text)

    text = re.sub(r'^(\s*id: )(?:RMC|CM)', r'\1FS', text, flags=re.M)
    text = re.sub(r'^(\s*parent: )(?:RMC|CM)', r'\1FS', text, flags=re.M)
    text = re.sub(r'^(\s*- )(?:RMC|CM)([A-Z]\w*)\s*$', r'\1FS\2', text, flags=re.M)
    text = re.sub(r'\b(?:RMC|CM)([A-Z]\w*)\b', r'FS\1', text)
    text = text.replace('_RMC14/', '_FinalStand/')
    return text


if __name__ == '__main__':
    src_dir, out_dir = sys.argv[1], sys.argv[2]
    os.makedirs(out_dir, exist_ok=True)
    totals = collections.Counter()
    for path in sorted(glob.glob(os.path.join(src_dir, '*.yml'))):
        text = open(path, encoding='utf-8-sig').read()
        new = convert(text)
        leftover = sorted({m for m in re.findall(r'\b(?:CM|RMC)[A-Za-z0-9]*', new)})
        name = os.path.basename(path)
        if leftover:
            print(f'  {name}: LEFTOVER {leftover}')
        hp = [int(h) for h in re.findall(r'damage: (\d+)', new)]
        loud = [h for h in hp if h > 1000]
        ids = len(re.findall(r'^  id: ', new, re.M))
        totals['ids'] += ids
        print(f'  {name:46s} ids={ids:4d}' + (f'  HIGH HP {loud}' if loud else ''))
        open(os.path.join(out_dir, name), 'w', encoding='utf-8', newline='\n').write(new)
    print(f'total ids: {totals["ids"]}')
