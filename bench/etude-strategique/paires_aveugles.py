"""Construit le dossier d'évaluation à l'aveugle : pour chaque position, l'état figé (positions-texte) et deux conseils
X et Y issus de deux variantes, dans un ordre tiré au hasard (graine fixe). La correspondance X/Y → variante est écrite à
part (cle-aveugle.json), hors du dossier remis à l'évaluateur. Aucun résultat de partie n'est inclus.
Usage : python -I -X utf8 paires_aveugles.py <run_dir> <varianteA> <varianteB> <dossier_sortie> <fichier_cle>"""
import hashlib, json, os, random, sys

run, va, vb, out, key_path = sys.argv[1:6]
here = os.path.dirname(os.path.abspath(__file__))
results = {}
for l in open(os.path.join(run, 'results.jsonl'), encoding='utf-8'):
    if l.strip():
        r = json.loads(l); results[(r['caseId'], r['variant'])] = r   # dernier résultat de chaque couple
def pid(cid): return 'P' + hashlib.sha256(cid.encode('utf-8')).hexdigest()[:8]
def fmt(r):
    if r is None or r.get('error'): return '  (pas de réponse)'
    lines = [f"  action : {r.get('actionLabel')}", f"  résumé : {r.get('summary')}", f"  raison : {r.get('rationale')}"]
    if r.get('followUp'): lines.append('  suite prévue : ' + ' ; '.join(r['followUp']))
    if r.get('planNextTurn'): lines.append(f"  plan du tour suivant : {r['planNextTurn']}")
    if r.get('risk'): lines.append(f"  risque signalé : {r['risk']}")
    return '\n'.join(lines)
os.makedirs(out, exist_ok=True)
rng = random.Random(77)
key = {}
cases = sorted({c for c, _ in results})
for cid in cases:
    a, b = results.get((cid, va)), results.get((cid, vb))
    if a is None or b is None or a.get('error') or b.get('error'): continue
    p = pid(cid)
    swap = rng.random() < 0.5
    x, y = (b, a) if swap else (a, b)
    key[p] = {'X': vb if swap else va, 'Y': va if swap else vb, 'memeAction': a.get('actionId') == b.get('actionId')}
    state = open(os.path.join(here, 'positions-texte', p + '.txt'), encoding='utf-8').read()
    with open(os.path.join(out, p + '.txt'), 'w', encoding='utf-8') as f:
        f.write(state + '\n\n=== Conseil X ===\n' + fmt(x) + '\n\n=== Conseil Y ===\n' + fmt(y) + '\n')
json.dump(key, open(key_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print(len(key), 'paires écrites ;', sum(1 for k in key.values() if k['memeAction']), 'avec la même action')
