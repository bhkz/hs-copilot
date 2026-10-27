"""Pour chaque position de danger (marge <= 5) et chaque position avec Nozdormu en main : quand l'alerte serait visible,
et combien de temps avant ton coup, avec l'affichage actuel (AdviceDisplay : les faits ne sont montrés que pendant
l'analyse ou sans conseil). Lit danger-positions.jsonl, positions-toutes.jsonl et chronologie.jsonl.
Usage : python -I -X utf8 danger_fenetre.py"""
import json, os
HERE = os.path.dirname(os.path.abspath(__file__)); ET = os.path.dirname(HERE)
chrono = {}
for l in open(os.path.join(HERE, 'chronologie.jsonl'), encoding='utf-8'):
    r = json.loads(l); chrono[(r['game'][:15], r['logtime'][:12])] = r
snap = {}
for l in open(os.path.join(ET, 'positions-toutes.jsonl'), encoding='utf-8'):
    d = json.loads(l); snap[d['caseId']] = d['snapshot']['lastLogTime'][:12]
info = [json.loads(l) for l in open(os.path.join(ET, 'danger-positions.jsonl'), encoding='utf-8')]
def window(r):
    if r is None or not r.get('traced') or r['think'] is None: return None
    if r['firstPhase'] == 'prêt': return 0.0
    end = min(r['think'], r.get('t_advice', 1e9))
    return max(0.0, end - r['t_first_event'])
print('| Position | Tour | Niveau | Marge | Décision | 1er affichage | Fenêtre d alerte avant ton coup | Ton coup après le bloc | Coup joué |')
print('|---|---|---|---|---|---|---|---|---|')
stats = []
for d in info:
    noz = any('Nozdormu' in g for g in d['gifts'])
    if d['level'] == 'faible' and not noz: continue
    key = (d['caseId'].split('/')[1][:15], snap[d['caseId']])
    r = chrono.get(key)
    w = window(r)
    first = '?' if r is None or not r.get('traced') else (r['firstPhase'] + ('/' + r['adviceOrigin'] if r.get('firstPhase') == 'prêt' else ''))
    adv = '' if r is None or r.get('t_advice') is None else f" ; action affichée à {r['t_advice']:.1f} s ({r['advicePhase']})"
    print(f"| {d['caseId'].split('/')[1]} | {d['turn']} | {d['level']}{' · Nozdormu' if noz else ''} | {d['margin']} | {d['decisionInTurn']} | {first}{adv} | {'' if w is None else f'{w:.1f} s'} | {'' if r is None or r['think'] is None else f"{r['think']:.1f} s"} | {d['played']} |")
    if d['level'] != 'faible': stats.append(w)
v = [x for x in stats if x is not None]
print()
print('positions de danger (marge <= 5) :', len(stats), '; fenêtre nulle :', sum(1 for x in v if x == 0), '; < 2 s :', sum(1 for x in v if x < 2), '; médiane :', sorted(v)[len(v)//2] if v else None)
