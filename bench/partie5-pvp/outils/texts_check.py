"""Chiffres cités dans les textes du modèle (résumé du conseil, étape de plan) contre les statistiques reconstruites au moment
de l'affichage. Seuls les motifs sans ambiguïté sont vérifiés : « <serviteur> à N ATQ », « <serviteur> à N PV » et
« pour N dégâts » sur une attaque (comparé à l'attaque de l'attaquant).
Usage : python texts_check.py dump.jsonl trace1.jsonl [...]
"""
import json, re, sys

dump = [json.loads(l) for l in open(sys.argv[1], encoding='utf-8') if l.strip()]
for d in dump: d['t'] = d['time'][:12]
stat = re.compile(r"(?:le |la |l’|l')?([A-ZÀ-Ý][\w’' -]+?) à (\d+) (ATQ|PV)")
dmg = re.compile(r"pour (\d+) dégâts?")
attacker = re.compile(r"^Attaquer avec (.+?) →")
tot = {'modèle': [0, 0], 'plan': [0, 0]}
for path in sys.argv[2:]:
    rows = [json.loads(l) for l in open(path, encoding='utf-8') if l.strip()]
    seen = set()
    for r in rows:
        if r.get('phase') != 'prêt' or r.get('origin') not in tot or not r.get('stateLogTime'): continue
        if r['revision'] in seen: continue
        seen.add(r['revision'])
        cand = [d for d in dump if d['game'] == r.get('gameId') and d['t'] >= r['stateLogTime'][:12]]
        if not cand: continue
        d = cand[0]
        board = d['local']['board'] + d['opponent']['board']
        text = r.get('summary') or ''
        checks = []
        for m in stat.finditer(text):
            name, n, kind = m.group(1).strip(), int(m.group(2)), m.group(3)
            ms = [x for x in board if x['name'] and (x['name'] == name or x['name'].startswith(name) or name.endswith(x['name']))]
            if ms: checks.append((f'{name} à {n} {kind}', any((x['attack'] if kind == 'ATQ' else x['health']) == n for x in ms), [(x['attack'], x['health']) for x in ms]))
        a = attacker.search(r.get('action') or '')
        for m in dmg.finditer(text):
            if not a: continue
            ms = [x for x in d['local']['board'] if x['name'] == a.group(1)]
            hero = d['local']['hero']
            vals = [x['attack'] for x in ms] + ([hero['attack']] if a.group(1) == 'ton héros' or 'Garrosh' in a.group(1) else [])
            if vals: checks.append((f'pour {m.group(1)} dégâts', int(m.group(1)) in vals, vals))
        for what, ok, actual in checks:
            tot[r['origin']][0] += 1
            if not ok:
                tot[r['origin']][1] += 1
                print('ÉCART', r['origin'], path[-30:-6], r['revision'], '|', r.get('action'), '|', text, '| cité :', what, '| réel :', actual)
for k, (c, b) in tot.items():
    print(f'{k} : {c} chiffre(s) vérifié(s), {b} faux au moment de l’affichage')
