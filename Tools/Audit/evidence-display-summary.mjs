import fs from 'node:fs';
import assert from 'node:assert/strict';
const root='docs/evidence/evidence-display';
const readText=file=>{
 const data=fs.readFileSync(file);
 return (data[0]===0xff&&data[1]===0xfe?data.subarray(2).toString('utf16le'):data.toString('utf8').replace(/^\uFEFF/,''))
  .replaceAll('\r','');
};
const paths=[
 'gameplay-final/EvidenceDisplayTest',
 'markers-final/EvidenceTentPickupTest','markers-final/EvidenceTraceMarkerTest',
 'gameplay-final-2/EvidenceProcedurePanelTest','gameplay-final-2/InventoryUILayoutTest',
 'gameplay-final-4/BottleFingerprintProcedureTest','gameplay-final-4/ReasoningCheckpointTest',
 'gameplay-final-4/EvidenceRelationshipsReviewTest','gameplay-final-4/AssessmentUploadServiceTest','gameplay-final-4/SOCOCloseoutTest',
 'gameplay-final-5/CustodyPassTest','gameplay-final-5/EducationalSessionRecordTest','gameplay-final-5/MeasurementToolTest',
 'gameplay-final-6/SceneRelationalMeasurementTest','gameplay-final-7/CrimeSceneSketchTest',
];
const unity=paths.map(path=>{
 const report=readText(`${root}/${path}.txt`);
 assert.match(report,/=== PASS/);assert.doesNotMatch(report,/^\s*FAIL:/m);
 const log=`${root}/${path}.log`;
 const compilerErrors=fs.existsSync(log)?[...fs.readFileSync(log,'utf8').matchAll(/\berror CS\d+:/g)].length:0;
 assert.equal(compilerErrors,0,path);
 return {suite:path.split('/').at(-1),passed:[...report.matchAll(/^\s*PASS:/gm)].length,failed:0,report:`${root}/${path}.txt`,compilerErrors};
});
const tap=file=>{
 const text=readText(`${root}/${file}`);
 const count=name=>Number(text.match(new RegExp(`^# ${name} (\\d+)$`,'m'))?.[1]??NaN);
 const value={file:`${root}/${file}`,tests:count('tests'),passed:count('pass'),failed:count('fail'),skipped:count('skipped')};
 assert.equal(value.failed,0);return value;
};
const web={sessionApiDisplay:tap('fresh-upload-api-tests.txt'),dashboard:tap('dashboard-regression.txt'),
 scoring:tap('scoring-regression.txt'),freshBottleWire:tap('bottle-wire-tests.txt'),freshCustodyWire:tap('custody-wire-tests.txt'),browser:tap('browser-tests-final.txt')};
for(const result of [web.sessionApiDisplay,web.dashboard,web.freshBottleWire,web.freshCustodyWire,web.browser])assert.equal(result.skipped,0);
const xr=[...fs.readFileSync('Assets/XR/XRGeneralSettingsPerBuildTarget.asset','utf8').matchAll(/m_InitManagerOnStart: (\d+)/g)].map(m=>Number(m[1]));
assert.ok(xr.length>=2&&xr.every(v=>v===1));
assert.equal(fs.existsSync('Tools/phase5-file-request.json'),false);
assert.equal(fs.existsSync('Temp/phase5-request.json'),false);
const summary={unity,unityAssertions:unity.reduce((n,s)=>n+s.passed,0),web,
 checks:{typescript:'passed',scopedEslint:'passed',productionNextBuild:'passed',xrStartupSettings:xr,pendingFileRequest:false},
 preservation:JSON.parse(fs.readFileSync(`${root}/preservation-result.json`,'utf8')),
 notes:[
  'Accepted reports only; earlier failures, test-only compilation correction and interrupted attempts are retained separately.',
  'Two optional fixtures skipped in the general scoring run were subsequently run with fresh actual Unity bottle/custody payloads; both dedicated wire suites passed without skips.',
  'Headset/production database acceptance was not performed. No commit, push, deployment, seed or production write.',
  'Existing diagnostics refresh shared local capture files; task copies are listed in capture-preservation.json.',
 ]};
fs.writeFileSync(`${root}/summary.json`,JSON.stringify(summary,null,2));
console.log(JSON.stringify({unityAssertions:summary.unityAssertions,suites:unity.length,failed:0,web},null,2));
