"""Inventaire des rapports de partie (lecture seule). Usage : python -I -X utf8 inventaire.py"""
import glob, json, os, re
R = os.path.expandvars(r'C:\Users\thoma\AppData\Local\HsCopilot\reports')
rows = []
for f in sorted(glob.glob(os.path.join(R, '*.json'))):
    b = os.path.basename(f)[:-5]
    m = re.match(r'(\d{8})_(\d{6})_(\d{6})-(\d+)', b)
    if not m: continue
    d = json.load(open(f, encoding='utf-8'))
    dup = m.group(2) != m.group(3)
    c = d.get('counters', {})
    rows.append(dict(file=b, dup=dup, gameType=d.get('gameType'), finished=d.get('finished'), result=d.get('result'),
                     sent=c.get('stratégie : appels envoyés', 0), hsid=m.group(4)))
for r in rows: print(r)
