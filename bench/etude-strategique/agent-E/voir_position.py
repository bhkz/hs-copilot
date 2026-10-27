"""Affiche l'état public d'une position figée (positions-toutes.jsonl). Usage : python -I -X utf8 voir_position.py <suffixe caseId>..."""
import json, os, sys
ET = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.environ.get('CAS', os.path.join(ET, 'positions-toutes.jsonl'))
want = sys.argv[1:]
for l in open(SRC, encoding='utf-8'):
    d = json.loads(l)
    if not any(d['caseId'].endswith(w) for w in want): continue
    s = d['snapshot']; me = s['local']; op = s['opponent']
    print('=' * 100); print(d['caseId'], 'tour', d['turnNumber'], 'décision', d['decisionInTurn'], s['game']['formatType'], 'contre', op.get('heroClass'))
    h = me['hero']; oh = op['hero']
    print(f"TOI : {h['health']} PV + {h.get('armor',0)} armure, ATQ héros {h.get('attack')}, arme {me.get('weapon',{}).get('name') if me.get('weapon') else '-'} ; mana {me['mana']['available']}/{me['mana']['max']}")
    for m in me['board']: print(f"   [{m['entityId']}] {m['name']} {m.get('attack')}/{m.get('health')} {m.get('keywords')} {('justPlayed' if m.get('state',{}).get('justPlayed') else '')} att={m.get('state',{}).get('attacksThisTurn')} | {(m.get('text') or '')[:90]}")
    print('   main :', ' ; '.join(f"{c.get('name')} ({c.get('cost')})" for c in me['hand']['cards']))
    print(f"ADVERSAIRE : {oh['health']} PV + {oh.get('armor',0)} armure ; main {op['hand']['count']} ; deck {op['deck']['count']} ; Secrets {len(op.get('secrets',[]))} ; arme {op.get('weapon',{}).get('name') if op.get('weapon') else '-'}")
    for m in op['board']: print(f"   [{m['entityId']}] {m['name']} {m.get('attack')}/{m.get('health')} {m.get('keywords')} | {(m.get('text') or '')[:110]}")
    print('   options :')
    for o in s['legalOptions']['playable']:
        print(f"     {o['kind']} {o.get('name')} -> {[t.get('name') for t in o.get('targets',[])]}")
    print('   coup joué :', (d.get('actual') or {}).get('label'))
