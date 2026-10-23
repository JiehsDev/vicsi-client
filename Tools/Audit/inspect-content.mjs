import fs from 'node:fs';
const scenePath='Assets/_Project/Scenes/Main.unity';
const scene=fs.readFileSync(scenePath,'utf8');
const docs=new Map([...scene.matchAll(/^--- !u!(\d+) &(-?\d+)[^\n]*\n([\s\S]*?)(?=^--- !u!|$(?![\s\S]))/gm)].map(m=>[m[2],{type:m[1],body:m[3]}]));
const objects=new Map();const parents=new Map();
for(const [id,d] of docs){
  if(d.type==='1')objects.set(id,{name:d.body.match(/^  m_Name: (.*)$/m)?.[1]?.trim(),active:d.body.match(/^  m_IsActive: (\d)/m)?.[1]==='1'});
  if(['4','224'].includes(d.type))parents.set(id,{gameObject:d.body.match(/m_GameObject: \{fileID: (\d+)/)?.[1],parent:d.body.match(/m_Father: \{fileID: (\d+)/)?.[1]});
}
function hierarchy(go){
  const nodes=[];let active=true;
  while(go){const o=objects.get(go);if(!o)break;nodes.unshift(o.name);active&&=o.active;
    const t=[...parents.values()].find(p=>p.gameObject===go);go=parents.get(t?.parent)?.gameObject;
  }return {path:nodes.join('/'),activeInHierarchy:active};
}
const missingCallbacks=[];
for(const [id,d] of docs){
  if(!d.body.includes('m_MethodName: ShowRoleSelect'))continue;
  const go=d.body.match(/m_GameObject: \{fileID: (\d+)/)?.[1];
  missingCallbacks.push({scene:scenePath,componentId:id,...hierarchy(go),missingMethod:'UIManager.ShowRoleSelect',replacementCandidate:'UIManager.OnStartPressed (verify button intent)'});
}
const truthPath='Assets/_Project/Data/Scenarios/GroundTruth_CSI_Environment.json';
const truth=JSON.parse(fs.readFileSync(truthPath,'utf8'));
const staleAnswerKeys=[];
for(const name of fs.readdirSync('Assets/_Project/Data/Evidence').filter(f=>f.endsWith('.asset'))){
  const file='Assets/_Project/Data/Evidence/'+name;const text=fs.readFileSync(file,'utf8');
  const evidenceId=text.match(/^  evidenceId: (.*)$/m)?.[1]?.trim();
  const examinedInPlace=/^  examinedInPlace: 1/m.test(text);
  const authored=[...text.matchAll(/^  - id: (.*)$/gm)].map(m=>m[1].trim());
  const exported=truth.evidenceReportLines?.find(e=>e.evidenceId===evidenceId)?.reportLineOptions.map(o=>o.id)??[];
  const missing=authored.filter(id=>!exported.includes(id));
  const obsolete=exported.filter(id=>!authored.includes(id));
  if(missing.length||obsolete.length)staleAnswerKeys.push({file,evidenceId,examinedInPlace,missingFromExport:missing,obsoleteInExport:obsolete});
}
const report={generatedAt:new Date().toISOString(),scope:'Targeted Main-scene legacy UnityEvent and report-line answer-key comparison',missingCallbacks,groundTruthPath:truthPath,groundTruthGeneratedAt:truth.generatedAt,staleAnswerKeys};
fs.writeFileSync('docs/evidence/content-consistency.json',JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify(report,null,2));
