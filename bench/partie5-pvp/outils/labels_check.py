"""Libellés affichés (trace du conseiller) contre les statistiques reconstruites au même instant (dump du moteur, vérifié
indépendamment). Rapprochement par l'heure de l'état analysé : le point de décision qui suit immédiatement cet état.
Usage : python labels_check.py dump.jsonl trace1.jsonl [trace2.jsonl ...]
"""
import json, re, sys

dump = [json.loads(l) for l in open(sys.argv[1], encoding='utf-8') if l.strip()]
for d in dump: d['t'] = d['time'][:12]
minion = re.compile(r'→ (.+?) (\d+)/(\d+)$')
hero = re.compile(r'→ (.+?) \((\d+) PV(?: \+ (\d+) armure)?\)$')
checked = bad = skipped = 0
for path in sys.argv[2:]:
    rows = [json.loads(l) for l in open(path, encoding='utf-8') if l.strip()]
    seen = set()
    for r in rows:
        if r.get('phase') not in ('partiel', 'prêt') or not r.get('action') or not r.get('stateLogTime'): continue
        key = (r['revision'], r['action'])
        if key in seen: continue
        seen.add(key)
        t = r['stateLogTime'][:12]
        cand = [d for d in dump if d['game'] == r.get('gameId') and d['t'] >= t]
        if not cand: skipped += 1; continue
        d = cand[0]
        mm, mh = minion.search(r['action']), hero.search(r['action'])
        if mm:
            name, a, h = mm.group(1), int(mm.group(2)), int(mm.group(3))
            name = name.replace(' (allié)', '')
            ms = [m for s in ('local', 'opponent') for m in d[s]['board'] if m['name'] == name]
            if not ms: skipped += 1; continue
            checked += 1
            if not any(m['attack'] == a and m['health'] == h for m in ms):
                bad += 1
                print('ÉCART', path[-30:-6], r['revision'], r['phase'], r.get('origin'), '| état', t, 'point', d['t'], '|', r['action'], '| reconstruit :', [(m['attack'], m['health']) for m in ms])
        elif mh:
            name, hp, ar = mh.group(1), int(mh.group(2)), int(mh.group(3) or 0)
            hs = [d[s]['hero'] for s in ('local', 'opponent') if d[s]['hero']]
            checked += 1
            if not any(x['health'] == hp and x['armor'] == ar for x in hs):
                bad += 1
                print('ÉCART', path[-30:-6], r['revision'], r['phase'], '|', r['action'], '| reconstruit :', [(x['health'], x['armor']) for x in hs])
print(f'libellés comparés : {checked} ; écarts : {bad} ; non rapprochés : {skipped}')
