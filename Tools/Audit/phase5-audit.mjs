import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { createRequire } from 'node:module';
const keyPath='Assets/_Project/Data/Scenarios/GroundTruth_CSI_Environment.json';
const key=JSON.parse(fs.readFileSync(keyPath,'utf8'));
const checks=[];
function check(name,fn){try{fn();checks.push({name,pass:true});}catch(e){checks.push({name,pass:false,error:e.message});}}
const read=p=>fs.readFileSync(p,'utf8');
const base='Assets/_Project/Data';
const backend='C:/Users/Roduf Eleu/Desktop/vicsi-dashboard';
const yaml=createRequire(import.meta.url)(backend+'/node_modules/js-yaml');
check('dialogue catalog is valid YAML',()=>yaml.load(read('Assets/Resources/ViCSIDialogueCatalog.asset').replace(/^%.*$/gm,'').replace(/^---.*$/gm,'---')));
check('versions',()=>{assert.equal(key.groundTruthVersion,'1.2.0');assert.equal(key.scenarioVersion,'1.1.0');assert.match(read(base+'/Assessment/Scenario_CSI_Environment.asset'),/scoringRulesVersion: 1\.5\.0/);});
check('collectible roster',()=>assert.deepEqual(key.evidence.map(e=>e.evidenceId),['EVD-014','EVD-015','EVD-018']));
for(const file of fs.readdirSync(base+'/Evidence').filter(f=>f.endsWith('.asset'))){
 const text=read(base+'/Evidence/'+file),id=text.match(/^  evidenceId: (.+)$/m)?.[1].trim();
 if(!['BODY-A','EVD-014','EVD-015','EVD-018'].includes(id))continue;
 const options=[...text.matchAll(/^  - id: (.+)\r?\n([\s\S]*?)(?=^  - id:|^  icon:)/gm)].map(m=>({id:m[1].trim(),isAccurate:/^    isAccurate: 1\r?$/m.test(m[2])}));
 check(id+' report-line key',()=>assert.deepEqual(key.evidenceReportLines.find(e=>e.evidenceId===id)?.reportLineOptions,options));
}
const hypotheses=read(base+'/Scenarios/HypothesisCheckpoints_CSI_Environment.asset');
check('HYP-01/02/03 answer IDs and correctness',()=>{
 for(const h of key.hypothesisCheckpoints){assert.ok(hypotheses.includes(h.id));assert.equal(h.reasoningOptions.filter(o=>o.isCorrect).length,1);for(const o of h.reasoningOptions){assert.ok(hypotheses.includes('id: '+o.id));const section=hypotheses.split('id: '+o.id)[1];assert.equal(/^      isCorrect: 1\r?$/m.test(section.split(/^    - id:/m)[0]),o.isCorrect,o.id);}}
});
check('five relationship IDs/weights are authored and wired',()=>{
 const scene=read('Assets/_Project/Scenes/Scenarios/CSI_Environment.unity');
 for(const r of key.relationships){const file=base+'/Relationships/CSI_Environment/'+r.relationshipId+'.asset';assert.match(read(file),/isCorrect: 1/);const guid=read(file+'.meta').match(/guid: (\w+)/)[1];assert.ok(scene.includes(guid));assert.equal(Number(read(file).match(/^  scoreWeight: (.+)$/m)[1]),r.scoreWeight);}
});
check('supported conclusion/ending and negative outcomes',()=>{const endings=read(base+'/Ending/Endings_CSI_Environment.asset');for(const id of [key.conclusion.correctConclusionId,key.conclusion.correctEndingId,'conclusion-peter','ending-peter-unsupported','ending-premature-submission','ending-inconclusive'])assert.ok(endings.includes(id));});
const patterns=/wife|affair|gloves|retired phone|accidental[ -]fall|Brother.s Fingerprint.*False Solution/i;
const sweep=[];
function walk(dir){return fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.name==='_Archive'?[]:e.isDirectory()?walk(path.join(dir,e.name)):[path.join(dir,e.name)]);}
const contentFiles=[...walk(base).filter(f=>f.endsWith('.asset')),'Assets/Resources/ViCSIDialogueCatalog.asset','Assets/_Project/Scenes/Scenarios/CSI_Environment.unity'];
for(const file of contentFiles){const lines=read(file).split(/\r?\n/);for(let i=0;i<lines.length;i++){const line=lines[i];if(!patterns.test(line))continue;if(line.includes('The injury is not consistent with an ordinary accidental fall.'))continue;if(/^\s*(?:- )?(?:id|theoryId|reportLineId|lineId|relationshipId|findingId|sourceId|targetId|m_Name):/.test(line))continue;sweep.push({file:file.replaceAll('\\','/'),line:i+1,text:line.trim()});}}
check('active player-facing retired-content sweep',()=>assert.deepEqual(sweep,[]));
const archive=base+'/_Archive/RemovedFromCSI_Environment/Scenarios/GroundTruth_CSI_Environment_1.1.0.json';
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
check('historical Unity 1.1 archive unchanged',()=>assert.equal(hash(archive),'4548f848eaade98ade1547ace03877769e002a34cd97019c4d69e64c3657a02c'));
const historical=Object.fromEntries([
 'src/lib/scoring/data/csiEnvironment.groundTruth.v1_1.ts',
 'src/lib/scoring/data/csiEnvironment.v1.ts',
 'src/lib/scoring/data/csiEnvironment.v1_5.ts'
].map(file=>[file,crypto.createHash('sha256').update(execFileSync('git',['-C',backend,'show','HEAD:'+file])).digest('hex')]));
check('historical server keys/rules unchanged',()=>{for(const [file,expected]of Object.entries(historical))assert.equal(hash(path.join(backend,file)),expected,file);});
const report={generatedAt:new Date().toISOString(),checks,activeContentCandidates:sweep,archive:{file:archive,sha256:hash(archive),evidence:JSON.parse(read(archive)).evidence.map(e=>e.evidenceId)},bodyPolicy:'BODY-A examined in place: report lines are classified; collectible recall roster remains three items.',backendRepository:backend};
fs.mkdirSync('docs/evidence/phase5-last-drink',{recursive:true});
fs.writeFileSync('docs/evidence/phase5-last-drink/audit.json',JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify(report,null,2));
if(checks.some(c=>!c.pass))process.exitCode=1;
