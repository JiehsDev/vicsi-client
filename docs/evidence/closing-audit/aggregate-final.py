from pathlib import Path
import re,json,collections
root=Path('docs/evidence/ioc-soco-phase5/closing-audit');rows=[]
for p in root.rglob('*.txt'):
 s=p.read_text(encoding='utf-8-sig');batch=p.relative_to(root).parts[0];rows.append({'batch':batch,'run':p.parent.name,'suite':p.stem,'passed':len(re.findall(r'^\s*PASS:',s,re.M)),'failed':len(re.findall(r'^\s*FAIL:',s,re.M)),'suitePass':'=== PASS' in s,'file':str(p),'mtime':p.stat().st_mtime})
latest={}
for r in sorted(rows,key=lambda r:r['mtime']):latest[(r['suite'],'WrongOrder' if 'wrong-order' in r['batch'] else 'Normal')]=r
final=list(latest.values());batches={}
for x in final:
 b=x['batch'][0] if x['batch'].startswith('C') else x['batch'];d=batches.setdefault(b,{'suites':set(),'configs':0,'passed':0,'failed':0});d['suites'].add(x['suite']);d['configs']+=1;d['passed']+=x['passed'];d['failed']+=x['failed']
for b,d in batches.items():d['suites']=len(d['suites'])
r={'attempts':len(rows),'distinctSuites':len(set(x['suite'] for x in rows)),'configurations':len(final),'allAttemptsPassedChecks':sum(x['passed'] for x in rows),'allAttemptsFailedChecks':sum(x['failed'] for x in rows),'finalPassedChecks':sum(x['passed'] for x in final),'finalFailedChecks':sum(x['failed'] for x in final),'finalFailedSuites':[x['suite'] for x in final if not x['suitePass']],'batches':batches,'reports':rows,'finalReports':final}
Path('docs/evidence/closing-audit/results-final.json').write_text(json.dumps(r,indent=2));print(json.dumps({k:v for k,v in r.items() if k not in ['reports','finalReports']}))
