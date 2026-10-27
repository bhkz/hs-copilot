"""Relecture indépendante des tags bruts (GameState) et comparaison avec les statistiques projetées par le moteur.

Usage : python stats_check.py dump.jsonl journal1 [journal2 ...]
Aucune ligne du moteur n'est réutilisée : seuls FULL_ENTITY / SHOW_ENTITY / CHANGE_ENTITY / TAG_CHANGE / HIDE_ENTITY sont lus.
"""
import json, re, sys

dump_path, journals = sys.argv[1], sys.argv[2:]
lines = []
for j in journals:
    with open(j, encoding='utf-8', errors='replace') as f:
        lines.extend(l.rstrip('\n') for l in f)

dump = [json.loads(l) for l in open(dump_path, encoding='utf-8') if l.strip()]
wanted = {d['line']: d for d in dump}

GS = ' GameState.DebugPrintPower() - '
ref_id = re.compile(r'\bid=(\d+)\b')
tags = {}          # entité -> {tag: valeur}
current = None     # entité dont les lignes « tag=… » suivent

def ent_of(ref):
    ref = ref.strip()
    if ref.startswith('['):
        m = ref_id.search(ref)
        return int(m.group(1)) if m else None
    if ref.isdigit():
        return int(ref)
    return None   # GameEntity, nom de joueur : sans intérêt ici

def val(v):
    try: return int(v)
    except ValueError: return v

def body_of(l):
    i = l.find(GS)
    return None if i < 0 else l[i + len(GS):].strip()

def stats_now():
    out = {}
    for e, t in tags.items():
        if t.get('ZONE') == 'PLAY' and t.get('CARDTYPE') in ('MINION', 'HERO'):
            hp = t.get('HEALTH', 0); dmg = t.get('DAMAGE', 0)
            out[e] = dict(type=t.get('CARDTYPE'), attack=t.get('ATK', 0), health=(hp if isinstance(hp, int) else 0) - (dmg if isinstance(dmg, int) else 0),
                          maxHealth=hp, damage=dmg, armor=t.get('ARMOR', 0))
    return out

checked = mism = 0
examples = []
by_game = {}
for i, l in enumerate(lines):
    if i in wanted:
        d = wanted[i]
        now = stats_now()
        g = by_game.setdefault(d['game'], [0, 0])
        for side in ('local', 'opponent'):
            v = d[side]
            items = [('serviteur', m['id'], m) for m in v['board']]
            if v['hero']: items.append(('héros', v['hero']['id'], v['hero']))
            for kind, eid, m in items:
                ref = now.get(eid)
                checked += 1; g[0] += 1
                keys = ['attack', 'health', 'maxHealth'] if kind == 'serviteur' else ['health', 'armor']
                if ref is None or any(ref[k] != m[k] for k in keys):
                    mism += 1; g[1] += 1
                    if len(examples) < 25:
                        examples.append((d['game'], d['revision'], d['time'][:10], kind, eid, m.get('name'), {k: m[k] for k in keys}, None if ref is None else {k: ref[k] for k in keys}))
    b = body_of(l)
    if b is None:
        continue
    if b.startswith('CREATE_GAME'):
        tags.clear(); current = None; continue
    m = re.match(r'FULL_ENTITY - Creating ID=(\d+) CardID=(\S*)', b)
    if m:
        current = int(m.group(1)); tags[current] = {}; continue
    m = re.match(r'FULL_ENTITY - Updating (\[.*?\]) CardID=(\S*)', b)
    if m:
        current = ent_of(m.group(1))
        if current is not None: tags[current] = {}       # entité complète : jeu de tags entier
        continue
    m = re.match(r'(SHOW_ENTITY|CHANGE_ENTITY) - Updating Entity=(\[.*?\]|\S+) CardID=(\S*)', b)
    if m:
        current = ent_of(m.group(2))
        if current is not None: tags.setdefault(current, {})
        continue
    m = re.match(r'tag=(\S+) value=(.*)$', b)
    if m and current is not None:
        tags[current][m.group(1)] = val(m.group(2).strip()); continue
    m = re.match(r'TAG_CHANGE Entity=(\[.*?\]|\S+) tag=(\S+) value=(\S*)', b)
    if m:
        e = ent_of(m.group(1))
        if e is not None:
            tags.setdefault(e, {})[m.group(2)] = val(m.group(3))
        current = None; continue
    m = re.match(r'HIDE_ENTITY - Entity=(\[.*?\]|\S+) tag=(\S+) value=(\S*)', b)
    if m:
        e = ent_of(m.group(1))
        if e is not None: tags.setdefault(e, {})[m.group(2)] = val(m.group(3))
        current = None; continue
    if not b.startswith('tag='):
        current = None

print(f'points de décision : {len(dump)} ; valeurs comparées : {checked} ; écarts : {mism}')
for gid, (c, mm) in by_game.items():
    print(f'  partie {gid} : {c} comparaisons, {mm} écart(s)')
for e in examples:
    print('  ÉCART', e)
