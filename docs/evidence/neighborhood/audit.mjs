import fs from 'node:fs';
const base='docs/evidence/neighborhood/';
const parse=p=>new Map([...fs.readFileSync(p,'utf8').replace(/\r\n/g,'\n').matchAll(/^--- !u!(\d+) &(-?\d+)[^\n]*\n([\s\S]*?)(?=^--- !u!|$(?![\s\S]))/gm)].map(m=>[m[2],{type:m[1],body:m[3]}]));
const before=parse(base+'before.unity');
const after=parse('Assets/_Project/Scenes/Scenarios/CSI_Environment.unity');
const removed=[],changed=[];
for(const [id,r] of before){if(!after.has(id))removed.push(id);else if(r.body!==after.get(id).body&&r.type!=='1660057539')changed.push({id,type:r.type});}
const report={existingRecords:before.size,addedRecords:[...after.keys()].filter(id=>!before.has(id)).length,removed,changedExistingRecords:changed,pass:removed.length===0&&changed.length===0};
fs.writeFileSync(base+'preservation.json',JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify(report,null,2));
if(!report.pass)process.exitCode=1;
