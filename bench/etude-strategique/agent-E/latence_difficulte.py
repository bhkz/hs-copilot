"""Latence du premier fragment selon la difficulté apparente de la position (nombre d'actions légales, danger, tour).
Joint chronologie.jsonl et danger-positions.jsonl (830 positions). Usage : python -I -X utf8 latence_difficulte.py"""
import json, os, statistics as st
HERE = os.path.dirname(os.path.abspath(__file__)); ET = os.path.dirname(HERE)
chrono = {}
for l in open(os.path.join(HERE, 'chronologie.jsonl'), encoding='utf-8'):
    r = json.loads(l); chrono[(r['game'][:15], r['logtime'][:12])] = r
snap = {}
for l in open(os.path.join(ET, 'positions-toutes.jsonl'), encoding='utf-8'):
    d = json.loads(l); snap[d['caseId']] = d['snapshot']['lastLogTime'][:12]
info = [json.loads(l) for l in open(os.path.join(ET, 'danger-positions.jsonl'), encoding='utf-8')]
rows = []
for d in info:
    r = chrono.get((d['caseId'].split('/')[1][:15], snap[d['caseId']]))
    if not r or not r.get('traced') or r.get('firstPhase') != 'analyse': continue
    rows.append((d, r))
def med(v): v = [x for x in v if x is not None]; return (f'{st.median(v):.1f} s (n={len(v)})' if v else '-')
def fmt(name, sub):
    adv = [r.get('t_advice') for d, r in sub]
    ok = [r for d, r in sub if r.get('t_advice') is not None and r['think'] is not None and r['t_advice'] <= r['think']]
    think = [r['think'] for d, r in sub]
    print(f"| {name} | {len(sub)} | {med(adv)} | {med(think)} | {len(ok)}/{len(sub)} |")
print('| Strate (positions où le modèle a été appelé) | n | bloc → 1re action affichée | bloc → ton coup | action avant ton coup |')
print('|---|---|---|---|---|')
for lo, hi in [(1, 5), (5, 9), (9, 14), (14, 999)]:
    fmt(f'{lo} à {hi-1} actions légales', [(d, r) for d, r in rows if lo <= d['actions'] < hi])
fmt('danger (marge ≤ 5)', [(d, r) for d, r in rows if d['level'] != 'faible'])
fmt('pas de danger', [(d, r) for d, r in rows if d['level'] == 'faible'])
fmt('1re décision du tour', [(d, r) for d, r in rows if d['decisionInTurn'] == 1])
fmt('2e ou 3e décision', [(d, r) for d, r in rows if d['decisionInTurn'] > 1])
fmt('tours 1 à 4', [(d, r) for d, r in rows if d['turn'] <= 4])
fmt('tours 5 à 8', [(d, r) for d, r in rows if 5 <= d['turn'] <= 8])
fmt('tours 9 et plus', [(d, r) for d, r in rows if d['turn'] >= 9])
