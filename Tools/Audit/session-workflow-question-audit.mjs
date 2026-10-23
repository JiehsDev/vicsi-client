import fs from 'node:fs';
import {createRequire} from 'node:module';
const require=createRequire('C:/Users/Roduf Eleu/Desktop/vicsi-dashboard/package.json');
const yaml=require('js-yaml');
const source='Assets/_Project/Data/Scenarios/HypothesisCheckpoints_CSI_Environment.asset';
const data=yaml.load(fs.readFileSync(source,'utf8').replace(/^%.*\r?\n/gm,'').replace(/^--- !u!.*\r?\n/gm,'')).MonoBehaviour;
let md='# Unscored workflow checkpoint inventory\n\n';
md+='Source: `'+source+'`, `HypothesisCheckpointManager`, and `FinalDeductionAssessment`. These workflow choices do not contribute numerical weights to scoring rules 1.7.0. Correctness flags below are training flags, not deduction points. HYP-01 and HYP-03 remain active. HYP-02 is replaced in the current interaction by scored RC-03, whose actual choices and weights are in the scored inventory. Its old template is listed only to distinguish legacy content from what is assessed.\n\n';
for(const q of data.checkpoints){
 md+='## '+q.id+(q.id==='HYP-02'?' — legacy template, integrated RC-03 replaces it':' — active, unscored')+'\n\n'+q.promptText+'\n\n';
 md+='Initial choices (all have deduction contribution 0):\n\n';
 for(const a of q.options)md+='- `'+a.id+'`: '+a.text+'\n';
 md+='\nJustification choices (all have deduction contribution 0):\n\n';
 for(const a of q.reasoningOptions)md+='- `'+a.id+'`: '+a.text+' (training correctness: '+Boolean(a.isCorrect)+')\n';
 md+='\n';
}
fs.writeFileSync('docs/evidence/session-simplification/workflow-questions.md',md);
console.log('Wrote unscored workflow question inventory.');
