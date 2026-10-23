import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
const web='C:/Users/Roduf Eleu/Desktop/vicsi-dashboard';
const require=createRequire(web+'/package.json'),yaml=require('js-yaml');
const out='docs/evidence/session-simplification';fs.mkdirSync(out,{recursive:true});
const rules=JSON.parse(fs.readFileSync('Assets/Resources/DeductionScoring_1_7_0.json','utf8'));
const asset=yaml.load(fs.readFileSync('Assets/Resources/ReasoningCheckpoints_LastDrink.asset','utf8').replace(/^%.*\r?\n/gm,'').replace(/^--- !u!.*\r?\n/gm,'')).MonoBehaviour;
const questions=asset.checkpoints.map(q=>({id:q.id,category:'Reasoning',dimension:q.category,prompt:q.prompt,options:q.responses.map(r=>({id:r.id,text:r.text,weight:r.weight})),maximum:4}));
questions.push(...rules.relationships.map(q=>({id:q.id,category:'Evidence Relationships',prompt:q.reviewPrompt,options:q.interpretations.map(r=>({id:r.id,text:r.text,weight:r.id===q.correctInterpretationId?q.weight:-rules.unsupportedRelationshipPenalty})),maximum:q.weight})));
const conclusions=['Conclusion_BROTHER-RESPONSIBLE','Conclusion_PETER-ACCUSATION','Conclusion_INCONCLUSIVE'].map(name=>yaml.load(fs.readFileSync('Assets/_Project/Data/Conclusions/'+name+'.asset','utf8').replace(/^%.*\r?\n/gm,'').replace(/^--- !u!.*\r?\n/gm,'')).MonoBehaviour);
const audit={rules,questions,conclusions:conclusions.map(c=>({id:c.conclusionId,text:c.displayName+'\n'+c.description})),events:[]};
function walk(p){for(const e of fs.readdirSync(p,{withFileTypes:true})){const f=path.join(p,e.name);if(e.isDirectory())walk(f);else if(f.endsWith('.cs')&&!f.includes('Testing')){const text=fs.readFileSync(f,'utf8');for(const m of text.matchAll(/LogEvent\(\s*SessionEventType\.(\w+)/g))audit.events.push({type:m[1],file:f.replaceAll('\\','/'),line:text.slice(0,m.index).split('\n').length});}}}
walk('Assets/_Project/Scripts');
fs.writeFileSync(out+'/current-code-audit.json',JSON.stringify(audit,null,2));
let md='# Current questions and scoring audit\n\nRead directly from current Unity assets and source. Scoring rules '+rules.scoringRulesVersion+'.\n\n';
md+='Reasoning = 60 × earned / 24. Evidence Relationships = 25 × max(0, correct relationship weights − unsupported count × 1) / 6. Final Conclusion = 15 × context-dependent credit. Total = clamp(sum, 0, 100). Procedures contribute no points.\n\n';
for(const q of questions){md+='## '+q.id+' — '+q.category+'\n\n'+q.prompt+'\n\n';for(const a of q.options)md+='- '+a.id+' — **'+a.weight+'**: '+a.text+'\n';md+='\nMaximum: '+q.maximum+'.\n\n';}
md+='## Final Conclusion\n\nActual picker prompt: “Submit your conclusion:”. Options are dynamically filtered by EndingManager.GetSelectableCaseConclusions; Inconclusive is hidden once a supported case exists. Snapshot the options actually offered, rather than fabricating three choices for every attempt.\n\n';
for(const c of audit.conclusions)md+='- '+c.id+': '+c.text+'\n';
md+='\nCredits (maximum 1): supported Paul 1; premature Paul 0.25; Peter 0; Inconclusive with insufficient support 1; Inconclusive despite full support 0.25. Each credit is multiplied by 15.\n\n';
md+='HYP-01/HYP-03 and legacy report-line correctness remain workflow/training decisions, outside the current deduction formula. HYP-02 uses the integrated RC-03 question; do not count it twice.\n';
fs.writeFileSync(out+'/questions-and-formula.md',md);
console.log(JSON.stringify({reasoning:asset.checkpoints.length,relationships:rules.relationships.length,events:audit.events.length,questions:out+'/questions-and-formula.md'}));
