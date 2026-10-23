const fs=require('fs'), assert=require('assert');
const yaml=require('C:/Users/Roduf Eleu/Desktop/vicsi-dashboard/node_modules/js-yaml');
const checks=[];
function check(name,fn){try{fn();checks.push({name,pass:true});}catch(e){checks.push({name,pass:false,error:e.message});}}
// Unity managed-reference IDs exceed JS's safe integer range; preserve exact keys.
function load(p){return yaml.load(fs.readFileSync(p,'utf8').replace(/^%.*$/gm,'').replace(/^---.*$/gm,'---').replace(/(\brid:\s*)(\d+)/g,'$1"$2"')).MonoBehaviour;}
const entries=load('Assets/Resources/ViCSIDialogueCatalog.asset').entries;
const get=id=>entries.find(e=>e.id===id);
check('Catalog: unique IDs, no empty texts',()=>{assert.equal(new Set(entries.map(e=>e.id)).size,entries.length);assert(entries.every(e=>e.text&&e.text.trim()));});
check('Exact vendor question and answer',()=>{assert.equal(get('vendor-followup-anyone-else').text,"Did you see anyone else near John's house?");assert.equal(get('statement-vendor-paul-3am').text,"Yeah, now that you mention it, I remember seeing Paul walking near John's house sometime around three in the morning. I didn't see where he went after that, but I recognized him.");});
check('Original Paul statement',()=>assert(entries.some(e=>e.text.includes('I went straight home. I did not come back after that.'))));
const rel=load('Assets/_Project/Data/Relationships/CSI_Environment/REL-paul-timeline-contradiction.asset');
check('Timeline relationship preserves observation limits',()=>{assert.equal(rel.sourceId,'statement_paul_timeline');assert.equal(rel.targetId,'statement_vendor_paul_3am');assert.equal(rel.relationshipType,'Contradicts');assert.equal(rel.isCorrect,1);assert(rel.explanation.includes('did not see Paul enter'));assert(rel.explanation.includes('does not prove guilt'));});
const req=load('Assets/_Project/Data/Requirements/REQ_BrotherCaseSupported.asset');
const defs=new Map(req.references.RefIds.map(r=>[r.rid,r.data]));const leaves=req.root.children.map(r=>defs.get(r.rid));
check('Supported Paul: ALL nine exact leaves, no shortcuts',()=>{assert.equal(req.root.kind,1);assert.equal(req.root.operatorType,0);assert.equal(leaves.length,9);assert(leaves.every(r=>r.kind===0));assert.deepEqual(leaves.filter(r=>r.requirementType===12).map(r=>r.findingId).sort(),['FIND-BODY-SHARP-WOUND','FIND-KNIFE-WOUND-CONSISTENT','FIND-KNIFE-PRINT-BROTHER','FIND-SPATTER-RECONSTRUCTION'].sort());assert.deepEqual(leaves.filter(r=>r.requirementType===11).map(r=>r.relationshipId).sort(),['REL-knife-consistent-wound','REL-spatter-consistent-reconstruction','REL-paul-print-handled-knife','REL-paul-timeline-contradiction'].sort());assert.equal(leaves.filter(r=>r.requirementType===21)[0].evidenceId,'statement_vendor_paul_3am');});
const files=['Assets/Resources/ViCSIDialogueCatalog.asset','Assets/_Project/Data/Ending/Endings_CSI_Environment.asset','Assets/_Project/Data/Scenarios/HypothesisCheckpoints_CSI_Environment.asset'];
for(const d of ['Evidence','Conclusions','Findings']){const dir='Assets/_Project/Data/'+d;if(fs.existsSync(dir))for(const f of fs.readdirSync(dir))if(f.endsWith('.asset'))files.push(dir+'/'+f);}
const hits=[];const stale=/\bwife\b|\baffair\b|\bgloves\b|retired phone|Brother.s Fingerprint.{0,8}False Solution|Form\s*4.{0,60}(?:SOCO request|request.*SOCO)/i;
function scan(v,p,key=''){if(typeof v==='string'&&/(text|description|displayName|endingText|explanation|title)$/i.test(key)&&stale.test(v))hits.push({file:p,field:key,text:v});else if(Array.isArray(v))v.forEach(x=>scan(x,p,key));else if(v&&typeof v==='object')Object.entries(v).forEach(([k,x])=>scan(x,p,k));}
for(const p of files)scan(load(p),p);
check('Active player-facing content sweep (ordinary fall wording permitted)',()=>assert.deepEqual(hits,[]));
check('HYP-03 and ending include strengthened reasoning',()=>{assert(fs.readFileSync(files[2],'utf8').includes('contradicts his no-return account'));const end=fs.readFileSync(files[1],'utf8');assert(end.includes('3:00 AM'));assert(end.includes('primary suspect'));});
check('Presentation: no evidence mutation, physics creation or scene scans',()=>{for(const n of ['NPCPresentation','SubtleTapeMotion']){const s=fs.readFileSync('Assets/_Project/Scripts/SceneTurnover/'+n+'.cs','utf8');assert(!/FindObject|FindAnyObject|FindFirstObject|FindObjects|GameObject.Find|EvidenceStateManager|Rigidbody|AddComponent<Light|MarkFound|MarkProcessed/.test(s));}});
check('Assessment builder and DTO unchanged since checkpoint',()=>{for(const p of ['Assets/_Project/Scripts/Assessment/AssessmentSessionUploadDto.cs','Assets/_Project/Scripts/Assessment/AssessmentSessionBuilder.cs'])assert.equal(require('child_process').execFileSync('git',['diff','4b7573870c85a539270014369109832fb54781d0','--',p],{encoding:'utf8'}),'');});
fs.writeFileSync('docs/evidence/closing-audit/static-audit.json',JSON.stringify({checks,hits,supportedPaulLeaves:leaves.map(r=>({type:r.requirementType,id:r.findingId||r.relationshipId||r.evidenceId}))},null,2));
console.log(JSON.stringify({passed:checks.filter(c=>c.pass).length,failed:checks.filter(c=>!c.pass).length,failures:checks.filter(c=>!c.pass)},null,2));
