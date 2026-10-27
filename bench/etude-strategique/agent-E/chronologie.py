"""Chronologie de chaque décision d'action en classé (lecture seule, aucun appel).
Sources : journaux de session HDT (lignes GameState et PowerTaskList), traces du conseiller.
Pour chaque bloc d'options (décision) : heure du bloc (GameState), heure d'exécution du début de ton tour par le client
(PowerTaskList MAIN_ACTION), heure de ton action (SendOption), et, d'après la trace, ce qui était affiché et quand.
Sortie : chronologie.jsonl (une ligne par décision) et un résumé sur la sortie standard.
Usage : python -I -X utf8 chronologie.py"""
import glob, json, os, re, statistics as st
from collections import Counter, defaultdict
from datetime import datetime, timedelta
BASE = r'C:\Users\thoma\AppData\Local\HsCopilot'
OUT = os.path.dirname(os.path.abspath(__file__))
def sec(h): return int(h[:2])*3600+int(h[2:4])*60+int(h[4:6])

# 1. Parties classées retenues (rapport non doublon). Le résultat n'est jamais utilisé pour juger un coup.
games = []
for f in sorted(glob.glob(os.path.join(BASE, 'reports', '*.json'))):
    b = os.path.basename(f)[:-5]
    m = re.match(r'(\d{8})_(\d{6})_(\d{6})-(\d+)', b)
    if not m: continue
    d = json.load(open(f, encoding='utf-8'))
    if d.get('gameType') != 'GT_RANKED' or (sec(m.group(2)) - sec(m.group(3))) % 86400 > 60: continue
    games.append(dict(stem=b, date=m.group(1), time=m.group(2), result=d.get('result'), finished=d.get('finished')))

TL = re.compile(r'^D (\d\d):(\d\d):(\d\d\.\d+) (GameState|PowerTaskList)\.(\w+)\(\) - (.*)$')
def parse_log(path, date, hhmmss):
    base = datetime.strptime(date + hhmmss, '%Y%m%d%H%M%S')
    day0 = base.replace(hour=0, minute=0, second=0, microsecond=0)
    last = None; dayoff = 0
    gs_turn = 0; ptl_turn = 0
    blocks, sends, choices = [], [], []
    gs_main, ptl_main = {}, {}
    for line in open(path, encoding='utf-8', errors='replace'):
        m = TL.match(line)
        if not m: continue
        s = int(m.group(1))*3600 + int(m.group(2))*60 + float(m.group(3))
        if last is not None and s < last - 3600: dayoff += 1
        last = s
        t = day0 + timedelta(days=dayoff, seconds=s)
        stream, fn, rest = m.group(4), m.group(5), m.group(6)
        if 'TAG_CHANGE Entity=GameEntity tag=TURN value=' in rest:
            v = int(re.search(r'value=(\d+)', rest).group(1))
            if stream == 'GameState': gs_turn = v
            else: ptl_turn = v
        elif 'TAG_CHANGE Entity=GameEntity tag=STEP value=MAIN_ACTION' in rest:
            if stream == 'GameState': gs_main.setdefault(gs_turn, t)
            else: ptl_main.setdefault(ptl_turn, t)
        elif stream == 'GameState' and fn == 'DebugPrintOptions' and rest.startswith('id='):
            blocks.append(dict(t=t, turn=gs_turn, id=int(rest[3:].split()[0]), logtime=f"{m.group(1)}:{m.group(2)}:{m.group(3)[:6]}"))
        elif stream == 'GameState' and fn == 'SendOption':
            sends.append(dict(t=t, turn=gs_turn, text=rest))
        elif stream == 'GameState' and fn == 'SendChoices' and rest.startswith('id='):
            choices.append(dict(t=t, turn=gs_turn, text=rest))
    return blocks, sends, choices, gs_main, ptl_main

def utc_to_local(u):
    return datetime.strptime(u[:23].ljust(23, '0'), '%Y-%m-%dT%H:%M:%S.%f') + timedelta(hours=1)  # CET (UTC+1) après le 25/10/2026 3 h

rows = []
for g in games:
    log = os.path.join(BASE, 'sessions', f"{g['date']}_{g['time']}_hdt.log")
    trace = os.path.join(BASE, 'advisor', g['stem'] + '.jsonl')
    if not os.path.exists(log): continue
    blocks, sends, choices, gs_main, ptl_main = parse_log(log, g['date'], g['time'])
    ev = defaultdict(list)
    if os.path.exists(trace):
        for l in open(trace, encoding='utf-8'):
            d = json.loads(l)
            if d.get('stateLogTime'): ev[d['stateLogTime'][:12]].append(d)
    first_of_turn, seen = set(), set()
    for i, b in enumerate(blocks):
        if b['turn'] not in seen: first_of_turn.add(i); seen.add(b['turn'])
    for i, b in enumerate(blocks):
        nxt = blocks[i+1]['t'] if i+1 < len(blocks) else b['t'] + timedelta(hours=1)
        send = next((s for s in sends if b['t'] <= s['t'] < nxt), None)
        choice = next((c for c in choices if b['t'] <= c['t'] < nxt), None)
        r = dict(game=g['stem'], turn=b['turn'], first=i in first_of_turn, t_opt=b['t'].isoformat(), logtime=b['logtime'])
        r['think'] = (send['t'] - b['t']).total_seconds() if send else None
        r['endturn'] = bool(send and 'selectedOption=0 ' in send['text'])
        r['choiceBetween'] = choice is not None
        if i in first_of_turn and b['turn'] in ptl_main:
            r['screenLag'] = (ptl_main[b['turn']] - b['t']).total_seconds()
        evs = ev.get(b['logtime'][:12], [])
        r['traced'] = bool(evs)
        if evs:
            t0 = utc_to_local(evs[0]['utc'])
            r['firstPhase'] = evs[0]['phase']; r['firstOrigin'] = evs[0].get('origin')
            r['firstMessage'] = evs[0].get('message', '')[:60]; r['reason'] = evs[0].get('reason')
            r['kind'] = evs[0].get('kind')
            r['t_first_event'] = (t0 - b['t']).total_seconds()
            adv = next((e for e in evs if e['phase'] in ('partiel', 'prêt') and e.get('action')), None)
            if adv:
                r['advicePhase'] = adv['phase']; r['adviceOrigin'] = adv.get('origin') or 'modèle'
                r['t_advice'] = (utc_to_local(adv['utc']) - b['t']).total_seconds()
                r['adviceAction'] = adv.get('action')
            full = next((e for e in evs if e['phase'] == 'prêt' and e.get('origin') == 'modèle'), None)
            if full: r['t_full'] = (utc_to_local(full['utc']) - b['t']).total_seconds()
            r['facts'] = next((e.get('facts') for e in evs if e.get('facts')), None)
        rows.append(r)

with open(os.path.join(OUT, 'chronologie.jsonl'), 'w', encoding='utf-8') as f:
    for r in rows: f.write(json.dumps(r, ensure_ascii=False) + '\n')

def q(v, p):
    v = sorted(v); return v[min(len(v)-1, int(p*len(v)))] if v else None
def desc(name, v):
    v = [x for x in v if x is not None]
    if not v: print(f'{name}: aucun'); return
    print(f'{name}: n={len(v)} médiane={st.median(v):.2f} P25={q(v,.25):.2f} P75={q(v,.75):.2f} P90={q(v,.9):.2f}')

print('parties :', len(games), '; décisions (blocs d options) :', len(rows), '; tracées :', sum(r['traced'] for r in rows))
acted = [r for r in rows if r['think'] is not None]
desc('temps de réponse du joueur (bloc -> action), toutes', [r['think'] for r in acted])
desc('  1re décision du tour', [r['think'] for r in acted if r['first']])
desc('  milieu de tour', [r['think'] for r in acted if not r['first']])
desc('  milieu de tour, hors fin de tour', [r['think'] for r in acted if not r['first'] and not r['endturn']])
desc('retard écran (PowerTaskList MAIN_ACTION - bloc), 1re décision', [r.get('screenLag') for r in rows if r['first']])
desc('temps de réponse depuis l écran, 1re décision', [r['think'] - max(0, r.get('screenLag') or 0) for r in acted if r['first'] and r.get('screenLag') is not None])
c = Counter()
for r in rows:
    if not r['traced']: c['non tracé'] += 1
    elif 'advicePhase' in r: c[f"{r['advicePhase']} / {r['adviceOrigin']}"] += 1
    else: c[f"sans action affichée ({r['firstPhase']}{' / ' + r['reason'] if r.get('reason') else ''})"] += 1
print('première action affichée par décision :')
for k, v in c.most_common(): print(f'  {v:4d}  {k}')
mod = [r for r in rows if r['traced'] and r.get('firstPhase') == 'analyse' and r['think'] is not None and r.get('kind') == 'action']
before = [r for r in mod if r.get('t_advice') is not None and r['t_advice'] <= r['think']]
print(f"décisions d action avec analyse et coup joué : {len(mod)} ; action affichée avant ton coup : {len(before)} ({len(before)/len(mod):.0%})")
desc('  délai bloc -> 1re action affichée (si avant ton coup)', [r['t_advice'] for r in before])
def window(r):
    if not r['traced'] or r['think'] is None: return None
    if r['firstPhase'] == 'prêt': return 0.0
    end = min(r['think'], r.get('t_advice', 1e9))
    return max(0.0, end - r['t_first_event'])
w = [(r, window(r)) for r in rows if r.get('kind') in (None, 'action')]
desc('fenêtre de visibilité des faits rapides avant ton coup (s), décisions d action', [x for _, x in w])
vals = [x for _, x in w if x is not None]
print('  part des décisions où la fenêtre est nulle :', f"{sum(1 for x in vals if x == 0)/len(vals):.0%}")
