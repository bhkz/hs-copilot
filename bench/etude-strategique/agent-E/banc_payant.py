"""Relecture du banc payant run-danger (aucun appel) : paires, accords, jetons de raisonnement, latence, et
comparaison au conseil affiché en partie (trace, 1re action affichée pour le même bloc d'options).
Usage : python -I -X utf8 banc_payant.py"""
import json, os, statistics as st
from collections import defaultdict
HERE = os.path.dirname(os.path.abspath(__file__)); ET = os.path.dirname(HERE)
res = [json.loads(l) for l in open(os.path.join(ET, 'run-danger', 'results.jsonl'), encoding='utf-8')]
by = defaultdict(dict)
for r in res: by[r['caseId']][r['variant']] = r
V0, V1 = 'gpt-6.1-sol:medium+local', 'gpt-6.1-sol:medium+local+danger'
ok = lambda r: r is not None and r.get('accepted') and r.get('actionId')
pairs = [(c, v[V0], v[V1]) for c, v in by.items() if ok(v.get(V0)) and ok(v.get(V1))]
print('réponses acceptées :', sum(1 for r in res if ok(r)), '/', len(res), '; paires complètes :', len(pairs))
print('même 1re action dans les paires :', sum(a['actionId'] == b['actionId'] for _, a, b in pairs))
for name, key in [('jetons de raisonnement', 'reasoningTokens'), ('jetons de sortie', 'outputTokens'), ('latence (ms)', 'latencyMs'), ('coût', 'costUsd')]:
    for v in (V0, V1):
        xs = [float(r[key]) for r in res if ok(r) and r['variant'] == v and r.get(key) is not None]
        if xs: print(f'  {name} {v.split(":")[1]} : médiane {st.median(xs):.4g}, moyenne {st.mean(xs):.4g}, n={len(xs)}')
print('accord avec ton coup réel (étiquette) :')
for v in (V0, V1):
    xs = [r for r in res if ok(r) and r['variant'] == v]
    print(f"  {v.split(':')[1]} : exact {sum(r.get('agreement') == 'exact' for r in xs)}/{len(xs)}")
# Conseil affiché en partie : chronologie (même bloc d'options).
chrono = {}
for l in open(os.path.join(HERE, 'chronologie.jsonl'), encoding='utf-8'):
    r = json.loads(l); chrono[(r['game'][:15], r['logtime'][:12])] = r
snap = {}
for l in open(os.path.join(ET, 'positions-toutes.jsonl'), encoding='utf-8'):
    d = json.loads(l); snap[d['caseId']] = d['snapshot']['lastLogTime'][:12]
print('comparaison au conseil affiché en partie (1re action affichée pour ce bloc) :')
for c, v in sorted(by.items()):
    live = chrono.get((c.split('/')[1][:15], snap[c]))
    la = (live or {}).get('adviceAction'); lo = (live or {}).get('adviceOrigin')
    a0 = v.get(V0, {}).get('actionLabel') if ok(v.get(V0)) else None
    a1 = v.get(V1, {}).get('actionLabel') if ok(v.get(V1)) else None
    if la is None: continue
    norm = lambda x: (x or '').split(' → ')[0] + ((' → ' + x.split(' → ')[1].split(' ')[0]) if x and ' → ' in x else '')
    print(f"  {c.split('/')[1]} | partie ({lo}) : {la[:55]} | V0 : {(a0 or '-')[:45]} | V1 : {(a1 or '-')[:45]} | V0=partie : {norm(a0) == norm(la) if a0 else '-'} | V1=partie : {norm(a1) == norm(la) if a1 else '-'}")
