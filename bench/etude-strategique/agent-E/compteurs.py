"""Recalcul des compteurs des rapports de parties classées (lecture seule).
Doublon = rapport dont l'heure de fichier dépasse de plus de 60 s l'heure de début de partie (renvoi au démarrage d'une session).
Usage : python -I -X utf8 compteurs.py"""
import glob, json, os, re, statistics as st
from collections import Counter, defaultdict
R = os.path.expandvars(r'C:\Users\thoma\AppData\Local\HsCopilot\reports')
def sec(h): return int(h[:2])*3600+int(h[2:4])*60+int(h[4:6])
keep = []
for f in sorted(glob.glob(os.path.join(R, '*.json'))):
    b = os.path.basename(f)[:-5]
    m = re.match(r'(\d{8})_(\d{6})_(\d{6})-(\d+)', b)
    if not m: continue
    d = json.load(open(f, encoding='utf-8'))
    dup = (sec(m.group(2)) - sec(m.group(3))) % 86400 > 60
    if d.get('gameType') != 'GT_RANKED' or dup: continue
    keep.append((b, m.group(4), d))
print('rapports classés non doublons :', len(keep))
games = defaultdict(list)
for b, hs, d in keep: games[hs].append((b, d))
res = Counter()
for hs, lst in games.items():
    fin = [d for b, d in lst if d.get('finished')]
    res[fin[-1].get('result') if fin else None] += 1
print('parties distinctes :', len(games), dict(res))
tot = Counter(); amt = Counter()
for b, hs, d in keep:
    for k, v in d.get('counters', {}).items(): tot[k] += v
    for k, v in d.get('amounts', {}).items(): amt[k] += v
for k in sorted(tot): print(f'  {tot[k]:6d}  {k}')
for k in sorted(amt): print(f'  {amt[k]:10.3f}  {k}')
# médianes des parties (p50 par partie, puis médiane)
lat = defaultdict(list)
for b, hs, d in keep:
    for L in d.get('latencies', []):
        lat[L['name']].append((L['p50'], L['count'], L.get('p95')))
for name, v in sorted(lat.items()):
    p50s = [x[0] for x in v if x[1] > 0]
    print(f"  {name}: médiane des p50 = {st.median(p50s)/1000:.2f} s sur {len(p50s)} rapports ; total n = {sum(x[1] for x in v)}")
