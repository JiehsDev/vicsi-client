from pathlib import Path
import re,json
root=Path('docs/evidence/ioc-soco-phase5/closing-audit')
rows=[]
for p in sorted(root.rglob('*.txt')):
 s=p.read_text(encoding='utf-8-sig');rows.append({'batch':p.relative_to(root).parts[0],'run':p.parent.name,'suite':p.stem,'passed':len(re.findall(r'^\s*PASS:',s,re.M)),'failed':len(re.findall(r'^\s*FAIL:',s,re.M)),'suitePass':'=== PASS' in s,'file':str(p)})
r={'reports':rows,'reportsCount':len(rows),'distinctSuites':len(set(x['suite'] for x in rows)),'passed':sum(x['passed'] for x in rows),'failed':sum(x['failed'] for x in rows)}
Path('docs/evidence/closing-audit/results.json').write_text(json.dumps(r,indent=2))
print(json.dumps({k:v for k,v in r.items() if k!='reports'}))
