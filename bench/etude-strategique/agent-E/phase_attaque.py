"""Phase d'attaque jouée contre toutes les phases d'attaque possibles (aucun appel, information publique seulement).

Pour chaque tour classé dont la fin ne contient plus que des attaques (et éventuellement le pouvoir héroïque ou la fin
du tour), on part de l'état à la première de ces attaques (cas-tous.jsonl, construit par `bench build --per-turn 99`)
et on énumère TOUTES les suites d'attaques légales (Provocation, Ruée, Furie des vents, Bouclier divin, Toxicité,
riposte, Grommash blessé). Chaque fin de phase est résumée par un vecteur :
  F dégâts au visage, T dégâts visibles adverses restants au prochain tour (serviteurs + arme), A ATQ totale de tes
  serviteurs survivants, P tes PV + armure, H PV totaux de tes serviteurs, W coups d'arme utilisés (durabilité).
La ligne jouée est « strictement dominée » si une autre ligne fait au moins aussi bien sur F, T, A et P et mieux sur l'un.
Positions écartées (non simulables ici) : Secret adverse, Râle d'agonie, Réincarnation, déclencheurs sur dégâts, attaque
ou mort, auras, Gel, effets « Une fois que / Chaque fois que » sur un serviteur impliqué.
C'est une borne BASSE des inefficacités tactiques : la dominance ignore la valeur des cartes, le tempo et la main adverse.
Usage : python -I -X utf8 phase_attaque.py"""
import json, os, re
from collections import defaultdict
HERE = os.path.dirname(os.path.abspath(__file__))
cases = [json.loads(l) for l in open(os.path.join(HERE, 'cas-tous.jsonl'), encoding='utf-8')]
turns = defaultdict(list)
for c in cases: turns[(c['source'], c['gameTurn'])].append(c)
UNSAFE = re.compile(r"Râle d|Réincarnation|subi|subit|attaqu|meurt|mort|Une fois que|Chaque fois que|Vos autres|adjacent|Gèle|Gel |Insensible|à la place|survit|Dégâts des sorts", re.I)
SAFE_TEXT = re.compile(r"^À la fin de votre tour|^Au début de votre tour", re.I)

def kind_of(c):
    a = c.get('actual') or {}
    idx = a.get('optionIndex')
    if idx == 0: return 'end-turn', None, None
    for o in c['snapshot']['legalOptions']['playable']:
        if o['index'] == idx: return o['kind'], o.get('entityId'), a.get('targetEntityId')
    return None, None, None

def unsafe_minion(m):
    t = m.get('text') or ''
    kw = set(m.get('keywords') or [])
    if kw & {'DEATHRATTLE', 'REBORN', 'AURA', 'FREEZE', 'IMMUNE', 'STEALTH'}: return True
    return bool(UNSAFE.search(t)) and not SAFE_TEXT.search(t)

def setup(c):
    s = c['snapshot']; me = s['local']; op = s['opponent']
    if op.get('secrets'): return None, 'Secret adverse'
    opp_hero = op['hero']['entityId']; my_hero = me['hero']['entityId']
    att = {}
    for o in s['legalOptions']['playable']:
        if o['kind'] == 'attack': att[o['entityId']] = [t['entityId'] for t in o.get('targets', [])]
    minions = {m['entityId']: m for m in me['board'] if m.get('type') == 'MINION'}
    opps = {m['entityId']: m for m in op['board'] if m.get('type') == 'MINION'}
    for m in list(opps.values()):
        if unsafe_minion(m): return None, f"adverse non simulable : {m['name']}"
    for eid in att:
        if eid in minions and unsafe_minion(minions[eid]): return None, f"attaquant non simulable : {minions[eid]['name']}"
    mine = []
    for eid, m in minions.items():
        kw = set(m.get('keywords') or [])
        wf = 4 if 'MEGA_WINDFURY' in kw else 2 if 'WINDFURY' in kw else 1
        left = 0 if eid not in att else max(1, wf - (m.get('state', {}).get('attacksThisTurn') or 0))
        can_face = eid in att and (opp_hero in att[eid] or not any('TAUNT' in (x.get('keywords') or []) for x in opps.values()))
        rush_only = bool(m.get('state', {}).get('justPlayed')) and 'RUSH' in kw and 'CHARGE' not in kw
        if 'ne peut pas attaquer les héros' in (m.get('text') or ''): rush_only = True
        mine.append(dict(id=eid, atk=m['attack'], hp=m['health'], shield='DIVINE_SHIELD' in kw, poison=bool(kw & {'POISONOUS', 'VENOMOUS'}),
                         left=left, face=not rush_only, grom=(m.get('cardId') == 'CORE_EX1_414' and m['health'] >= m.get('maxHealth', m['health']))))
    hero = me['hero']
    hero_att = dict(id=my_hero, atk=hero.get('attack') or 0, left=1 if my_hero in att else 0, hp=hero['health'] + (hero.get('armor') or 0))
    foes = []
    for eid, m in opps.items():
        kw = set(m.get('keywords') or [])
        n = 4 if 'MEGA_WINDFURY' in kw else 2 if 'WINDFURY' in kw else 1
        cant = (m.get('state') or {}).get('frozen') or (m.get('state') or {}).get('dormant') or (m.get('state') or {}).get('cantAttack') or 'FROZEN' in kw
        foes.append(dict(id=eid, atk=m['attack'], hp=m['health'], shield='DIVINE_SHIELD' in kw, poison=bool(kw & {'POISONOUS', 'VENOMOUS'}),
                         taunt='TAUNT' in kw and 'STEALTH' not in kw, mult=0 if cant else n, elusive=False))
    w = op.get('weapon')
    weapon = (w.get('attack') or 0) if w and (w.get('durability') or 0) > 0 and not op['hero'].get('frozen') else 0
    return dict(mine=mine, hero=hero_att, foes=foes, oppHp=op['hero']['health'] + (op['hero'].get('armor') or 0), weapon=weapon, legal=att), None

def key(st):
    return (tuple((m['id'], m['atk'], m['hp'], m['shield'], m['left']) for m in st['mine']),
            (st['hero']['left'], st['hero']['hp'], st['hero'].get('swings', 0)), tuple((f['id'], f['hp'], f['shield']) for f in st['foes']), st['face'])

def copy(st):
    return dict(mine=[dict(m) for m in st['mine']], hero=dict(st['hero']), foes=[dict(f) for f in st['foes']], face=st['face'])

def moves(st, legal, opp_hero_target='H'):
    taunts = [f for f in st['foes'] if f['taunt']]
    for m in st['mine']:
        if m['left'] <= 0: continue
        allowed = set(legal.get(m['id'], []))
        for f in st['foes']:
            if taunts and not f['taunt']: continue
            if f['id'] in allowed or not allowed: yield (m['id'], f['id'])
        if not taunts and m['face']: yield (m['id'], opp_hero_target)
    h = st['hero']
    if h['left'] > 0 and h['atk'] > 0:
        for f in st['foes']:
            if taunts and not f['taunt']: continue
            yield (h['id'], f['id'])
        if not taunts: yield (h['id'], opp_hero_target)

def apply(st, src, tgt, hero_id):
    n = copy(st)
    if src == hero_id:
        a = n['hero']; a['left'] -= 1; atk = a['atk']; a['swings'] = a.get('swings', 0) + 1
        if tgt == 'H': n['face'] += atk; return n
        f = next(x for x in n['foes'] if x['id'] == tgt)
        if f['shield']: f['shield'] = False
        else: f['hp'] -= atk
        a['hp'] -= f['atk']
        n['foes'] = [x for x in n['foes'] if x['hp'] > 0]
        return n
    a = next(x for x in n['mine'] if x['id'] == src); a['left'] -= 1
    if tgt == 'H': n['face'] += a['atk']; return n
    f = next(x for x in n['foes'] if x['id'] == tgt)
    if f['shield']: f['shield'] = False
    else: f['hp'] = 0 if a['poison'] else f['hp'] - a['atk']
    if f['atk'] > 0:
        if a['shield']: a['shield'] = False
        else:
            a['hp'] = 0 if f['poison'] else a['hp'] - f['atk']
            if a['grom'] and a['hp'] > 0: a['atk'] += 6; a['grom'] = False
    n['foes'] = [x for x in n['foes'] if x['hp'] > 0]
    n['mine'] = [x for x in n['mine'] if x['hp'] > 0]
    return n

def vec(st, weapon):
    T = sum(f['atk'] * f['mult'] for f in st['foes']) + weapon
    A = sum(m['atk'] for m in st['mine'])
    H = sum(m['hp'] for m in st['mine'])
    return (st['face'], -T, A, st['hero']['hp'], H, -st['hero'].get('swings', 0))

def enumerate_ends(st0, legal, hero_id, limit=200000):
    seen, ends, stack = set(), {}, [st0]
    while stack and len(seen) < limit:
        st = stack.pop()
        k = key(st)
        if k in seen: continue
        seen.add(k); ends[k] = st
        for src, tgt in moves(st, legal):
            stack.append(apply(st, src, tgt, hero_id))
    return list(ends.values()), len(seen) >= limit

out, stats = [], defaultdict(int)
for (src, gt), cs in sorted(turns.items()):
    cs = sorted(cs, key=lambda c: c['decisionInTurn'])
    acts = [kind_of(c) for c in cs]
    # début de la phase d'attaque : premier cas après lequel il n'y a plus que des attaques (pouvoir héroïque et fin de tour tolérés)
    start = None
    for i in range(len(cs)):
        rest = acts[i:]
        if rest and rest[0][0] == 'attack' and all(k in ('attack', 'hero-power', 'end-turn') for k, _, _ in rest):
            start = i; break
    if start is None: continue
    played = [(s_, t_) for k, s_, t_ in acts[start:] if k == 'attack']
    if not played: continue
    stats['phases'] += 1
    c = cs[start]
    st0, why = setup(c)
    if st0 is None: stats['écartée : ' + why.split(' :')[0]] += 1; continue
    hero_id = c['snapshot']['local']['hero']['entityId']; opp_hero = c['snapshot']['opponent']['hero']['entityId']
    st = dict(mine=st0['mine'], hero=st0['hero'], foes=st0['foes'], face=0)
    ok = True
    for s_, t_ in played:
        tgt = 'H' if t_ == opp_hero else t_
        legal_now = list(moves(st, st0['legal']))
        if (s_, tgt) not in legal_now: ok = False; break
        st = apply(st, s_, tgt, hero_id)
    if not ok: stats['écartée : ligne jouée non rejouable'] += 1; continue
    ends, truncated = enumerate_ends(dict(mine=st0['mine'], hero=st0['hero'], foes=st0['foes'], face=0), st0['legal'], hero_id)
    if truncated: stats['écartée : énumération tronquée'] += 1; continue
    stats['analysées'] += 1
    vp = vec(st, st0['weapon'])
    lethal_played = st['face'] >= st0['oppHp']
    if lethal_played: stats['ligne jouée létale'] += 1; continue
    lethal_alt = [e for e in ends if e['face'] >= st0['oppHp']]
    dom = [e for e in ends if all(a >= b for a, b in zip(vec(e, st0['weapon']), vp)) and vec(e, st0['weapon']) != vp]
    rec = dict(caseId=c['caseId'], turn=c['turnNumber'], played=[(cs[start + i]['actual'] or {}).get('label') for i in range(len(cs) - start)],
               vecPlayed=vp, lines=len(ends), lethalAvailable=bool(lethal_alt))
    if lethal_alt: stats['létal par attaques disponible mais pas joué'] += 1
    if dom:
        best = max(dom, key=lambda e: tuple(x - y for x, y in zip(vec(e, st0['weapon']), vp)))
        rec['dominatedBy'] = vec(best, st0['weapon'])
        rec['gain'] = [x - y for x, y in zip(vec(best, st0['weapon']), vp)]
        stats['ligne jouée strictement dominée'] += 1
    out.append(rec)
with open(os.path.join(HERE, 'phase_attaque.jsonl'), 'w', encoding='utf-8') as f:
    for r in out: f.write(json.dumps(r, ensure_ascii=False) + '\n')
for k, v in sorted(stats.items()): print(f'{v:5d}  {k}')
print('gains (F visage, -T menace, A ton ATQ, P tes PV, H PV de tes serviteurs, -W coups d arme) des lignes dominantes :')
for r in out:
    if 'gain' in r: print(' ', r['caseId'].split('/')[1], 'tour', r['turn'], 'joué', r['vecPlayed'], 'gain', r['gain'], '|', ' ; '.join(x or '?' for x in r['played'])[:160])
