"""Sélection reproductible des positions du benchmark (graine fixe), à partir de :
- positions-toutes.jsonl (cas figés de `hscopilot bench build`, sans cartes cachées) ;
- danger-positions.jsonl (résumé par cas de `hscopilot threat-audit`).
Sorties : positions-benchmark.jsonl (cas retenus), positions-benchmark.md (index par catégorie, SANS résultat de partie),
positions-retro.json (résultat des parties et coup joué : réservé aux analyses rétrospectives).
Usage : python -I -X utf8 selection.py <dossier des rapports>"""
import glob, json, os, random, sys

here = os.path.dirname(os.path.abspath(__file__))
reports = {}
for f in glob.glob(os.path.join(sys.argv[1], '*.json')):
    r = json.load(open(f, encoding='utf-8'))
    if r.get('finished'): reports[r['gameId']] = r.get('result')
cases = {}
for l in open(os.path.join(here, 'positions-toutes.jsonl'), encoding='utf-8'):
    if l.strip():
        c = json.loads(l); cases[c['caseId']] = l.rstrip('\n')
info = [json.loads(l) for l in open(os.path.join(here, 'danger-positions.jsonl'), encoding='utf-8') if l.strip()]
rng = random.Random(20261026)
chosen = {}  # caseId -> catégorie

def first_per_turn(rows):
    best = {}
    for r in rows:
        k = (r['gameId'], r['turn'])
        if k not in best or r['decisionInTurn'] < best[k]['decisionInTurn']: best[k] = r
    return list(best.values())

# 1. Danger : premier point de décision de chaque tour où le danger visible est élevé ou létal.
for r in first_per_turn([r for r in info if r['level'] != 'faible']):
    chosen[r['caseId']] = 'danger : ' + r['level']
# 2. Ton létal certain sous danger : la pression est-elle conservée ?
for r in [r for r in info if r['lethal'] and r['level'] != 'faible']:
    chosen[r['caseId']] = 'létal sous danger'
# 3. Cadeau à l'adversaire chiffrable (mise à 10 cristaux) : un tour par partie.
noz = first_per_turn([r for r in info if any('Nozdormu' in g for g in r['gifts'])])
for r in sorted(noz, key=lambda r: r['caseId'])[:2]:
    chosen.setdefault(r['caseId'], 'cadeau à l’adversaire')
# 4. Témoins : danger faible, pas de létal, au moins 4 actions, début de tour ; strates début / milieu / fin et formats.
pool = [r for r in info if r['level'] == 'faible' and not r['lethal'] and r['actions'] >= 4 and r['decisionInTurn'] == 1 and r['caseId'] not in chosen]
for fmt, phase, n in [('FT_WILD', range(2, 5), 1), ('FT_WILD', range(5, 30), 1), ('FT_STANDARD', range(2, 5), 1), ('FT_STANDARD', range(5, 8), 2), ('FT_STANDARD', range(8, 30), 1)]:
    sub = [r for r in pool if r['turn'] in phase and (fmt == 'FT_WILD') == (r['caseId'] < 'sessions/20261025_2317')]
    for r in rng.sample(sub, min(n, len(sub))): chosen[r['caseId']] = 'témoin'
# 5. Létal certain sans danger : vérifier qu'aucune variante ne le perd.
lethals = first_per_turn([r for r in info if r['lethal'] and r['level'] == 'faible' and r['caseId'] not in chosen])
for r in rng.sample(lethals, 3): chosen[r['caseId']] = 'létal'

with open(os.path.join(here, 'positions-benchmark.jsonl'), 'w', encoding='utf-8') as f:
    for cid in sorted(chosen): f.write(cases[cid] + '\n')
byid = {r['caseId']: r for r in info}
with open(os.path.join(here, 'positions-benchmark.md'), 'w', encoding='utf-8') as f:
    f.write('# Positions du benchmark (figées, sans résultat de partie)\n\n| Cas | Catégorie | Tour | Danger visible / PV | Actions |\n|---|---|---|---|---|\n')
    for cid in sorted(chosen):
        r = byid[cid]; f.write(f"| {cid} | {chosen[cid]} | {r['turn']} | {r['visible']} / {r['hp']} | {r['actions']} |\n")
retro = {cid: {'categorie': chosen[cid], 'resultatPartie': reports.get(byid[cid]['gameId']), 'coupJoue': byid[cid]['played']} for cid in sorted(chosen)}
json.dump(retro, open(os.path.join(here, 'positions-retro.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
from collections import Counter
print(len(chosen), 'positions :', dict(Counter(chosen.values())))
