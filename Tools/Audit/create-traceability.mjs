import fs from 'node:fs';
// Generate stable IDs in specification order. Keep the request outside the repository.
const request = fs.readFileSync(process.argv[2], 'utf8').replaceAll('\r\n', '\n');
const section = request.split('## Functional Requirements')[1].split('## Implementation Order')[0];
const groups = [...section.matchAll(/### ([A-G])\. ([^\n]+)\n([\s\S]*?)(?=### [A-G]\.|$)/g)];
const esc = s => s.replaceAll('|', '\\|').trim();
const rows=[];
for (const [,letter,title,body] of groups) {
  let n=0;
  for (const line of body.split('\n')) {
    const m=line.match(/^\s*(?:\d+\.|\*)\s+(.+)$/); if(!m) continue;
    const text=m[1]; const id=`${letter}-${String(++n).padStart(2,'0')}`;
    let status='Partial', source='', problem='', action='', evidence='Source inspection only; acceptance unrun';
    if(letter==='A') {
      const sources=['RoleSystem/RoleSceneLoader.cs','Scenes/Scenarios/CSI_Environment.unity','RoleSystem/LocomotionSuspender.cs','Interaction/EvidenceGrabGate.cs','RoleSystem/ToolWheelController.cs','Interaction/PhotographTool.cs','Interaction/EvidenceTentTool.cs; Interaction/LetterTentTool.cs','Interaction/ChalkMarkTool.cs','UI/RecorderReportUI.cs','CaseFile/EvidenceStateManager.cs','Interaction/SwabTool.cs','Interaction/FingerprintKitTool.cs','Interaction/BrushTipReceiver.cs','Interaction/UVRevealable.cs','Interaction/FingerprintTapeTool.cs','Interaction/FingerprintStationTool.cs','Interaction/EvidenceBagTool.cs','Interaction/EvidenceBagReceiver.cs','Feedback/InteractionFeedback.cs','Deduction/EvidenceBoardController.cs','Ending/EndingManager.cs','Deduction/CompletionScreenUI.cs'];
      source=sources[n-1]; problem='Player/device acceptance not demonstrated'; action='Test actual scene controls and outcome on Quest 3S';
      if(n===1)source='UI/UIManager.cs: OnStartPressed; Assets/_Project/Scenes/Main.unity; Logging/SessionLogger.cs';
      if(n===20){status='Missing';problem='Controller code exists but no board component in active scene';action='Author and wire board UI and relationship nodes';}
      if(n===21){problem='Separate suspect picker determines submitted conclusion';action='Resolve conclusion from completed evidence relationships';}
      if(n===22){problem='Ending text exists; complete assessed result interface absent';action='Implement configured scoring, grade, errors, duration and completion view';}
      if([10,11,12,13,14,15,16,17,18].includes(n)){problem='Legacy processing order/finding flags differ from approved specification; player acceptance unrun';action='Migrate type-specific finding workflow and test complete sequence';}
    } else if(letter==='B') {
      source='CaseFile/EvidenceStateManager.cs; ProceduralGate/ProceduralGateValidator.cs';
      problem='Legacy sequence or tool-only validation does not prove approved rule';action='Enforce rule at state boundary and cover valid, duplicate and invalid transitions';
      if(/separate|own evidence record/.test(text)){problem='Findings are parent flags, without separate identity/marker';action='Add finding records and unambiguous targeting; migrate scene and answer key';}
      if(/laboratory|Fingerprint work/.test(text)){problem='Lab stub and post-seal fingerprint workflow remain';action='Separate field procedure from authored analytical results and enforce before packaging';}
    } else if(letter==='C') {
      source='Data/Evidence/; Data/Relationships/; Data/Ending/Endings_CSI_Environment.asset; Scripts/Ending/EndingManager.cs';
      problem='Authored narrative/state logic exists; board unavailable and conclusion uses picker';action='Wire relationship-driven deduction and run wife/brother/insufficient player paths';
      if(/separate suspect|must not manually/.test(text)){problem='EndingManager explicitly displays wife/brother/inconclusive picker';action='Remove suspect-selection screen; derive result from submitted relationships';}
    } else if(letter==='D') {
      source='Scripts/Logging/SessionLogger.cs; Scripts/Logging/SessionEvent.cs; Tools/EvidenceScorer/Program.cs';
      problem='Partial event/metric coverage; no complete approved assessment';action='Add required metadata/outcome coverage and reproducible configurable assessment';
      if(/Student ID|Attempt number|Grade|Completion status/.test(text)){status='Missing';problem='No verified persistence/assessment implementation for this field';}
      if(/start and completion time/.test(text)){problem='Elapsed clock only at baseline; UTC lifecycle fields absent';}
    } else {
      status='Missing';source='Repository inventory: no relevant application manifest, route or migration';
      problem=letter==='E'?'Node API absent':letter==='F'?'Supabase migrations and RLS absent; remote state unverified':'Next.js application and role dashboards absent';
      action=letter==='E'?'Implement authenticated validated API after stable session/schema contracts; add authorization/idempotency tests':letter==='F'?'Create constraints, indexes and role-scoped RLS migrations; execute local and deployed database tests':'Implement role dashboard against secured API; test own-record/class/admin access';
      evidence='No executable implementation found; acceptance unrun';
    }
    rows.push({id,section:title.trim(),description:text,status,source,tests:evidence,problem,action});
  }
}
// Normative prose requirements outside the source bullet lists also need IDs.
for (const [letter,description,source,problem,action] of [
  ['C','Verify and implement one indoor house murder scenario.','Assets/_Project/Scenes/Scenarios/CSI_Environment.unity','Scene and content exist; complete approved player path and device acceptance outstanding','Migrate the evidence workflow and verify the complete indoor scenario on Quest 3S'],
  ['D','Unapproved final scoring weights remain configurable and explicitly require stakeholder approval.','Assets/_Project/Scripts/Deduction/DeductionScorer.cs; Tools/EvidenceScorer/Program.cs','Legacy deduction weights .5/.3/.2 are hardcoded; no approved complete grading policy','Introduce versioned configurable assessment policy; do not present provisional values as official'],
  ['F','Create migrations, indexes, foreign keys, validation constraints and Row-Level Security policies.','Repository inventory','No Supabase migrations or policies found','Author and execute schema and role-matrix tests after stable data contract'],
  ['G','Clearly label unfinished dashboard components and remove placeholder data before completion.','Repository inventory','No dashboard application found','Build real role dashboards; explicitly label incomplete development UI and verify production has no placeholder data']
]) {
  const id=letter+'-'+String(rows.filter(r=>r.id.startsWith(letter+'-')).length+1).padStart(2,'0');
  rows.push({id,section:letter,description,status:['F','G'].includes(letter)?'Missing':'Partial',source,tests:'Acceptance unrun',problem,action});
}
const verified = {
  'B-07':['EvidenceBoundaryTest: ordinary letter rejected, positive number applied, zero/negative/reused numbers rejected','Numbered marker invariant verified at mutation boundary; Quest placement acceptance remains separate'],
  'B-08':['EvidenceBoundaryTest: numbered/blank/numeric body labels rejected, A applied, duplicate safe; StoryContentTest documents actual BODY-A with letter A','Letter marker invariant verified; physical controller acceptance remains separate'],
  'B-09':['EvidenceBoundaryTest: body remains Logged; readiness, collection and sealing rejected','Body non-collection invariant verified at state/query boundary'],
  'D-01':['SessionPersistenceTest: persisted events carry consistent generated session ID and sequence','Local session ID persistence verified; authenticated upload remains separate']
};
for (const r of rows) {
  if(verified[r.id]) { r.status='Implemented';r.tests=verified[r.id][0];r.problem=verified[r.id][1];r.action='Preserve regression coverage; execute remaining integrated/device acceptance cases'; }
  else if(r.id.startsWith('B-')) r.tests='EvidenceBoundaryTest: 28/28 component checks; approved full procedure order acceptance unrun';
  else if(r.id.startsWith('C-')) r.tests='StoryContentTest: 33/33 checks plus legacy Greybox flow PASS; direct state/picker tests, not board/device acceptance';
  else if(r.id.startsWith('D-')) r.tests='SessionPersistenceTest 12/12; CLI metric fixtures 6/6; full assessment/auth/upload acceptance unrun';
  if(r.id==='D-05') {r.problem='UTC startedAtUtc/endedAtUtc now persisted; training-completed versus interruption status remains undefined';r.action='Record explicit training completion and interruption metadata in versioned session contract';}
  if(['D-19','D-22'].includes(r.id)) {r.problem+='; ground-truth export omits authored phone-message-affair and deliberately excludes body notes';r.action+='; regenerate export and model body documentation assessment';}
}
rows.sort((a,b)=>a.id.localeCompare(b.id));
if (rows.length < 100) throw new Error('Specification parsing incomplete; refusing to write matrix.');
fs.mkdirSync('docs/evidence',{recursive:true});
fs.writeFileSync('docs/evidence/requirements.json',JSON.stringify(rows,null,2)+'\n');
let md='# ViCSI requirements traceability matrix\n\nDate: 2026-09-14. IDs preserve specification order within sections A–G, including evidence roster and access requirements. Status is conservative: source presence alone never earns Implemented. Partial tests do not constitute full requirement acceptance. All '+rows.length+' enumerated/bulleted functional requirements are tracked. Scoring approval and all delivery/testing obligations are additionally tracked in the engineering and test reports.\n\n';
md+='| ID | Requirement | Status | Actual source / object | Test evidence | Problem | Required action |\n|---|---|---|---|---|---|---|\n';
md+=rows.map(r=>'| '+[r.id,r.description,r.status,r.source,r.tests,r.problem,r.action].map(esc).join(' | ')+' |').join('\n');
md+='\n\nSource paths refer to `Assets/_Project/Scripts/` unless starting with `Scenes/`, `Data/`, `Scripts/` (relative to `Assets/_Project/`) or `Tools/` (repository root). See `VICSI_TEST_REPORT.md` for executed component tests and pending acceptance cases. Status totals and acceptance completion must be recalculated from the final matrix, not from code counts.\n';
fs.writeFileSync('docs/VICSI_REQUIREMENTS_TRACEABILITY_MATRIX.md',md);
console.log(JSON.stringify({requirements:rows.length,statuses:rows.reduce((a,r)=>(a[r.status]=(a[r.status]??0)+1,a),{})}));
