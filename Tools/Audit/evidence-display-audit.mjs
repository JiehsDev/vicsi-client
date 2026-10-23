import fs from 'node:fs';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
const output='docs/evidence/evidence-display';
const web='C:/Users/Roduf Eleu/Desktop/vicsi-dashboard';
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const before=JSON.parse(fs.readFileSync(`${output}/preservation-before.json`,'utf8'));
const changed=Object.entries(before).filter(([file,sha])=>hash(file)!==sha).map(([file])=>file);
assert.deepEqual(changed,[], 'No serialized scene, evidence, prerequisites, relationship or scoring asset may change');
const unity=JSON.parse(fs.readFileSync('Assets/Resources/EvidenceDisplayNames_1.json','utf8'));
assert.deepEqual(unity,JSON.parse(fs.readFileSync(`${web}/src/lib/data/evidenceDisplay.v1.json`,'utf8')));
assert.deepEqual(unity.entries.map(e=>[e.id,e.number,e.label]),[
 ['BODY-A',0,'Body A – John'],['EVD-014',1,'Evidence 1 – Kitchen Knife'],
 ['EVD-015',2,'Evidence 2 – Blood Spatter'],['EVD-018',3,'Evidence 3 – Empty Liquor Bottle']]);
const result={unchangedSerializedFiles:Object.keys(before).length,changedSerializedFiles:changed,
 sharedMappingIdentical:true,mapping:unity.entries,
 preservedReferences:['EVD-014','EVD-015','EVD-018','BODY-A','REL-paul-timeline-contradiction','statement_vendor_paul_3am'],
 note:'SHA256 comparison against this task start, not Git HEAD; existing unrelated edits preserved. No database writes.'};
fs.writeFileSync(`${output}/preservation-result.json`,JSON.stringify(result,null,2));
console.log(JSON.stringify(result,null,2));
