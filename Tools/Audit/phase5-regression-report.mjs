import fs from 'node:fs';
import path from 'node:path';
const root='docs/evidence/phase5-last-drink';
const batches=[{batch:1,suites:['FirstResponderTurnoverTest','DialogueHistoryTest','StoryContentTest','ObjectiveStateManagerTest']},...JSON.parse(fs.readFileSync('Tools/phase5-batches.json','utf8'))];
function walk(dir){return fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(path.join(dir,e.name)):[path.join(dir,e.name)]);}
const files=walk(root).filter(f=>f.endsWith('.txt') && f.includes('unity-final'));
const suites=batches.flatMap(batch=>batch.suites.map(name=>{
 const candidates=files.filter(f=>path.basename(f)===name+'.txt').sort((a,b)=>fs.statSync(b).mtimeMs-fs.statSync(a).mtimeMs);
 if(!candidates.length)return {batch:batch.batch,name,status:'PENDING',passChecks:0,failChecks:0};
 const file=candidates[0],text=fs.readFileSync(file,'utf8');
 const passChecks=(text.match(/^\s*PASS:/gm)||[]).length,failures=(text.match(/^\s*FAIL:.*$/gm)||[]).map(s=>s.trim());
 const skipped=/SKIPPED|\(.*skipped:/i.test(text);
 const status=failures.length||!/=== PASS/.test(text)?'FAIL':skipped?'PARTIAL':'PASS';
 const log=file.replace(/\.txt$/,'.log');
 return {batch:batch.batch,name,status,passChecks,failChecks:failures.length,failures,file:file.replaceAll('\\','/'),
  ...(name==='GreyboxFlowTest'?{lifecycleItems:3,note:'This diagnostic reports full lifecycle items instead of PASS-prefixed assertions.'}:{}),
  ...(fs.existsSync(log)?{compileErrors:(fs.readFileSync(log,'utf8').match(/error CS\d+/g)||[]).length}:{} )};
}));
const result={generatedAt:new Date().toISOString(),method:'Controlled scene/domain resets and Play exit between suites; graphics-enabled fresh batch-mode Editor processes with maximum two suites after identifying the Null graphics renderer crash.',
 suites,totals:{suites:suites.length,passed:suites.filter(s=>s.status==='PASS').length,failed:suites.filter(s=>s.status==='FAIL').length,partial:suites.filter(s=>s.status==='PARTIAL').length,pending:suites.filter(s=>s.status==='PENDING').length,passChecks:suites.reduce((n,s)=>n+s.passChecks,0),failChecks:suites.reduce((n,s)=>n+s.failChecks,0)}};
fs.writeFileSync(root+'/regression.json',JSON.stringify(result,null,2)+'\n');
console.log(JSON.stringify(result.totals));
for(const s of suites.filter(s=>s.status!=='PASS'&&s.status!=='PENDING'))console.log(s.name+': '+s.status+' '+s.failures?.join(' | '));
