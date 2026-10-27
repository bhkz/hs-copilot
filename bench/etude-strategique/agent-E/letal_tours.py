"""Tours de victoire (rétrospectif : issue des parties, utilisée seulement pour repérer le tour final) :
niveau du solveur actuel à la 1re décision du tour, et ce qu'ajoute la recherche d'échanges (echanges.jsonl).
Usage : python -I -X utf8 letal_tours.py"""
import glob, json, os, re
from collections import Counter
HERE = os.path.dirname(os.path.abspath(__file__)); ET = os.path.dirname(HERE)
R = r'C:\Users\thoma\AppData\Local\HsCopilot\reports'
def sec(h): return int(h[:2])*3600+int(h[2:4])*60+int(h[4:6])
won = {}
for f in glob.glob(os.path.join(R, '*.json')):
    b = os.path.basename(f)[:-5]; m = re.match(r'(\d{8})_(\d{6})_(\d{6})-(\d+)', b)
    if not m: continue
    d = json.load(open(f, encoding='utf-8'))
    if d.get('gameType') == 'GT_RANKED' and (sec(m.group(2)) - sec(m.group(3))) % 86400 <= 60 and d.get('result') == 'WON':
        won[m.group(1) + '_' + m.group(2)] = True
pos = {}
for l in open(os.path.join(ET, 'positions-toutes.jsonl'), encoding='utf-8'):
    d = json.loads(l); g = d['caseId'].split('/')[1][:15]
    pos[d['caseId']] = (g, d['gameTurn'], d['decisionInTurn'])
ech = {json.loads(l)['caseId']: json.loads(l) for l in open(os.path.join(HERE, 'echanges.jsonl'), encoding='utf-8')}
last = {}
for cid, (g, gt, k) in pos.items():
    last[g] = max(last.get(g, 0), gt)
c = Counter(); rows = []
for g in sorted(won):
    if g not in last: continue
    turn = last[g]
    decs = sorted([(k, cid) for cid, (gg, gt, k) in pos.items() if gg == g and gt == turn])
    if not decs: continue
    first = ech[decs[0][1]]
    levels = [ech[cid]['level'] for k, cid in decs]
    trade = first.get('tradeLethal')
    cat = ('certain dès la 1re décision' if first['level'] == 'certain'
           else f"1re décision « {first['level']} », échanges : {trade or 'rien'}")
    c[cat] += 1
    rows.append((g, first['turn'], levels, cat, first.get('tradeSteps')))
for r in rows: print(r[0], 'tour', r[1], r[2], '->', r[3], r[4] or '')
print()
for k, v in c.most_common(): print(f'{v:3d}  {k}')
print('parties gagnées couvertes :', len(rows))
