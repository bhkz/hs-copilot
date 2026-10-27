"""Détail de la chronologie (lit chronologie.jsonl). Usage : python -I -X utf8 chrono_detail.py"""
import json, os, statistics as st
from collections import Counter
OUT = os.path.dirname(os.path.abspath(__file__))
rows = [json.loads(l) for l in open(os.path.join(OUT, 'chronologie.jsonl'), encoding='utf-8')]
def q(v, p):
    v = sorted(v); return v[min(len(v)-1, int(p*len(v)))] if v else None
def desc(name, v):
    v = [x for x in v if x is not None]
    if not v: print(f'{name}: aucun'); return
    print(f'{name}: n={len(v)} médiane={st.median(v):.2f} P10={q(v,.1):.2f} P25={q(v,.25):.2f} P75={q(v,.75):.2f} P90={q(v,.9):.2f}')
act = [r for r in rows if r.get('kind') in (None, 'action') and r['think'] is not None and r['traced']]
for label, sub in [('1re décision du tour', [r for r in act if r['first']]), ('milieu de tour', [r for r in act if not r['first']])]:
    print('==', label, len(sub))
    c = Counter()
    for r in sub:
        if r.get('advicePhase') and r['t_advice'] <= r['think']: c['action affichée avant ton coup : ' + r['advicePhase'] + '/' + r['adviceOrigin']] += 1
        elif r.get('advicePhase'): c['action affichée APRÈS ton coup'] += 1
        elif r['firstPhase'] == 'ignoré': c['ignoré : ' + str(r.get('reason'))] += 1
        else: c['aucune action affichée (' + r['firstPhase'] + ')'] += 1
    for k, v in c.most_common(): print(f'  {v:4d} {k}')
    called = [r for r in sub if r['firstPhase'] == 'analyse']
    ok = [r for r in called if r.get('t_advice') is not None and r['t_advice'] <= r['think']]
    print(f'  appels au modèle : {len(called)} ; action affichée avant ton coup : {len(ok)} ({len(ok)/max(1,len(called)):.0%})')
    desc('  délai bloc -> 1re action affichée (appels)', [r['t_advice'] for r in ok])
    desc('  ton coup - 1re action affichée (appels)', [r['think'] - r['t_advice'] for r in ok])
    desc('  ton temps de réponse quand aucune action n a précédé ton coup (appels)', [r['think'] for r in called if r not in ok])
    desc('  ton temps de réponse sur suite de plan', [r['think'] for r in sub if r.get('adviceOrigin') == 'plan'])
    desc('  ton temps de réponse sur létal du solveur', [r['think'] for r in sub if r.get('adviceOrigin') == 'solveur'])
# Le joueur attend-il le conseil ? Écart coup - affichage, par tranche.
ok = [r for r in act if r['firstPhase'] == 'analyse' and r.get('t_advice') is not None and r['t_advice'] <= r['think']]
gaps = [r['think'] - r['t_advice'] for r in ok]
for lo, hi in [(0, 1), (1, 2), (2, 4), (4, 8), (8, 1e9)]:
    print(f'  écart coup - action affichée dans [{lo},{hi}) s : {sum(lo <= g < hi for g in gaps)}')
# Coup joué = action affichée ?
same = sum(1 for r in ok if r.get('adviceAction'))
print('décisions avec action affichée avant le coup :', len(ok))
