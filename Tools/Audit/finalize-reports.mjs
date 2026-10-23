import fs from 'node:fs';
const date='2026-09-14';
const suites=[
 ['UT-01','EvidenceBoundaryTest','B-07, B-08, B-09, B-16, B-19','unity-boundary-persistence.txt',28],
 ['UT-02','SessionPersistenceTest','D-01, D-05, D-08','unity-boundary-persistence.txt',12],
 ['UT-03','RequirementEvaluatorTest','B-19, C-20','unity-component-regressions.txt',24],
 ['UT-04','RelationshipStateManagerTest','C-19, C-20, D-27','unity-component-regressions.txt',14],
 ['UT-05','ObjectiveStateManagerTest','C-19, C-20','unity-component-regressions.txt',13],
 ['UT-06','StoryContentTest','A-21, C-08, C-09, C-10, C-11, C-12, C-13, C-14, C-15, C-16','unity-story-content.txt',33]
];
const tests=suites.map(([id,name,requirements,file,expectedCount])=>{
  const raw=fs.readFileSync('docs/evidence/'+file,'utf8');
  const body=raw.split('=== '+name+' ===')[1]?.split(/=== (?:PASS|FAIL)/)[0];
  if(!body)throw new Error('Missing suite result: '+name);
  const passed=(body.match(/^\s*PASS:/gm)??[]).length;
  const failed=(body.match(/^\s*FAIL:/gm)??[]).length;
  if(passed!==expectedCount || failed)throw new Error('Unexpected test outcome: '+name);
  return {id,name,requirements,steps:'Run '+name+'.RunAllTests() in Play mode; fixture actions are checked in under Assets/_Project/Scripts/Testing.',expected:`${expectedCount} checks pass`,actual:`${passed} passed, ${failed} failed`,result:'PASS',environment:'Windows Unity 6000.5.6f1 Editor Play mode; not Quest hardware',tester:'Codex',date,defect:name==='StoryContentTest'?'Legacy state-driving/picker path, not required board acceptance':name==='RelationshipStateManagerTest'?'Legacy locked-selection behavior still asserted':'None in tested scope',correctiveAction:'Preserve regression; execute outstanding acceptance tests',artifact:'docs/evidence/'+file};
});
// Windows PowerShell Tee-Object writes UTF-16LE; normalize the saved transcript.
const tapBytes=fs.readFileSync('docs/evidence/scorer-tests.tap');
const tap=tapBytes.subarray(0,2).equals(Buffer.from([0xff,0xfe]))
  ? tapBytes.toString('utf16le').replace(/^\uFEFF/,'') : tapBytes.toString('utf8');
fs.writeFileSync('docs/evidence/scorer-tests.tap',tap);
if(!tap.includes('# pass 6') || !tap.includes('# fail 0'))throw new Error('CLI suite did not pass');
tests.push({id:'CLI-01',name:'Offline scorer fixtures',requirements:'D-19, D-22, D-23',steps:'Build Tools/EvidenceScorer, then node --test Tools/Audit/scorer.test.mjs',expected:'6 metric/validation fixtures pass',actual:'6 passed, 0 failed',result:'PASS',environment:'Windows; Node test runner; .NET 8.0.10 runtime, net8.0 assembly built with SDK 10.0.302',tester:'Codex',date,defect:'First run: two rounding expectation failures; product uses documented four-decimal rounding',correctiveAction:'Corrected expected 2/3 to 0.6667; rerun passed; scorer unchanged',artifact:'docs/evidence/scorer-tests.tap'});
fs.writeFileSync('docs/evidence/executed-tests.json',JSON.stringify(tests,null,2)+'\n');
let report=fs.readFileSync('docs/VICSI_TEST_REPORT.md','utf8');
const a=report.indexOf('## Executed tests'),b=report.indexOf('## Required acceptance cases');
let content='## Executed tests\n\nFinal run: **124 Unity assertions + 6 CLI tests = 130 passed, 0 failed.** Legacy greybox flow also passed inside StoryContentTest; its internal checks are not double-counted. All results below are dated 2026-09-14, tester Codex. Source fixture methods define exact steps; complete machine-readable records include environment, expected/actual, defect, corrective action and artifact in `docs/evidence/executed-tests.json`.\n\n';
content+='| Test ID | Requirements | Steps | Expected / actual | Result | Environment | Defect / corrective action | Evidence |\n|---|---|---|---|---|---|---|---|\n';
content+=tests.map(t=>'| '+[t.id,t.requirements,t.steps,t.expected+' / '+t.actual,t.result,t.environment,t.defect+'; '+t.correctiveAction,t.artifact].map(s=>s.replaceAll('|','\\|')).join(' | ')+' |').join('\n');
content+='\n\nInitial CLI run: 4 passed, 2 failed due to unrounded precision expected by the new tests. The implementation intentionally rounds to four decimal places (`Program.Ratio`); corrected expectations and reran, obtaining 6/6. These were test defects, not unreported product failures. Expected blocked-action warnings and the simulated filesystem failure are part of the boundary/persistence fixtures.\n\nThe following acceptance cases remain unrun end to end. Component success does not override the known absence of a usable deduction board, independent findings, backend, dashboards or device evidence.\n\n';
report=report.slice(0,a)+content+report.slice(b);fs.writeFileSync('docs/VICSI_TEST_REPORT.md',report);
const requirements=JSON.parse(fs.readFileSync('docs/evidence/requirements.json','utf8'));
const ids=new Set(requirements.map(r=>r.id));
for(const t of [...tests,...JSON.parse(fs.readFileSync('docs/evidence/acceptance-test-plan.json','utf8'))])
  for(const id of t.requirements.split(',').map(s=>s.trim()))if(!ids.has(id))throw new Error('Unknown requirement reference: '+id);
console.log(JSON.stringify({executedSuites:tests.length,unityAssertions:124,cliTests:6,failed:0,requirementReferences:'valid'}));
