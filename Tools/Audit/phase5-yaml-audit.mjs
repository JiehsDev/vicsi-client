import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
const yaml=createRequire(import.meta.url)('C:/Users/Roduf Eleu/Desktop/vicsi-dashboard/node_modules/js-yaml');
function walk(dir){return fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.name==='_Archive'?[]:e.isDirectory()?walk(path.join(dir,e.name)):[path.join(dir,e.name)]);}
const files=[...walk('Assets/_Project/Data').filter(f=>f.endsWith('.asset')),'Assets/Resources/ViCSIDialogueCatalog.asset'];
const failures=[];
for(const file of files){try{yaml.loadAll(fs.readFileSync(file,'utf8').replace(/^%.*$/gm,'').replace(/^---.*$/gm,'---'));}catch(error){failures.push({file,message:error.message});}}
const result={filesChecked:files.length,failed:failures.length,failures};
fs.writeFileSync('docs/evidence/phase5-last-drink/yaml-audit.json',JSON.stringify(result,null,2)+'\n');
console.log(JSON.stringify(result,null,2));
if(failures.length)process.exitCode=1;
