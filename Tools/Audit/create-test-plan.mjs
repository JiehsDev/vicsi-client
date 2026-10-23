import fs from 'node:fs';
const cases = [
 ['Full wife conclusion path','A-20, A-21, C-14','Process body, knife, spatter, phone and gloves; complete motive, blood/print and statement-contradiction relationships; submit board.','Wife result follows from relationships without suspect picker.'],
 ['Misleading brother path','A-21, C-15','Process knife/bottle; connect brother print and argument while leaving exculpatory/contradictory relationships unresolved; submit.','Incorrect/incomplete brother path with evidence-based explanation.'],
 ['Insufficient evidence','A-21, C-16','Submit a relationship set insufficient for either complete case.','Insufficient-evidence result; no invented guilt.'],
 ['Missing prerequisite','B-16, B-19','Attempt collection before photographing and noting evidence.','Action rejected, state unchanged, reason shown and violation logged.'],
 ['Incorrect procedure order','B-01, B-02, B-03, B-04, B-05','Attempt chalk before marker; then photograph, marker, applicable chalk, prepared note and processing in approved order.','Wrong order blocked; approved order succeeds.'],
 ['Duplicate procedure','B-19, D-16','Repeat photo, marker, swab, fingerprint and seal after successful action.','No duplicate collection or changed marker identity; attempts recorded.'],
 ['Incorrect evidence marker','B-02, B-07, B-08','Apply letter to ordinary item, number to body, reused number, zero and invalid label.','Rejected with explanation and event; original evidence state preserved.'],
 ['Body marker behavior','B-08','Photograph body, place A marker, complete applicable tracing and notes.','Letter visible and persisted; numbered pool unaffected.'],
 ['Body non-collection','B-09','Finish body documentation; attempt bagging, collecting and sealing through tool and direct state entry.','Body remains examined in place; no collection status permitted.'],
 ['Blood-swabbing sequence','B-10','Document blood finding; swab using unused swab, deposit correct container, label and seal.','Independent blood finding and container completion recorded in order.'],
 ['Fingerprint-processing sequence','B-11, B-13','Document print; powder, brush, reveal, lift, record and secure; try skipping each step.','Only complete ordered field collection accepted; no laboratory action needed.'],
 ['Fingerprint before packaging','B-12','Attempt to bag/seal parent object before print secured; complete print then retry.','Parent packaging blocked until required finding secured.'],
 ['Separate knife blood finding','B-14, B-15','Photograph and mark knife and its blood finding separately; swab finding and inspect records.','Distinct IDs, marker, notes and processing state; camera targeting remains unambiguous.'],
 ['Prepared-note selection','A-09, B-04','Select valid prepared options, locked option and forged option ID; confirm too few and enough choices.','Only authored unlocked choices and valid count accepted; no free-text typing.'],
 ['Bagging and sealing','A-17, A-18, B-06','Try wrong bag; use correct bag; label; seal; retry collect/seal.','Correct packaging and label required; exactly one sealed record.'],
 ['Score calculation','D-19, D-20, D-22','Replay fixed session using same versioned approved weights twice; vary one metric at a time.','Identical score/grade for same input; all assessment dimensions follow policy.'],
 ['Session-event logging','D-01, D-05, D-08, D-10, D-13','Start session; perform correct, wrong and duplicate actions; complete; inspect JSONL.','Stable ID and monotonic sequence; UTC start/end, tool/marker, attempts and outcomes present.'],
 ['Offline preservation','D-01, D-05','Disconnect network; log events; pause headset; force app exit; restart; also simulate storage write failure.','Persisted prefix survives, retry buffer not cleared on write failure, incomplete tail recoverable.'],
 ['Successful synchronization','E-18, E-20','Upload complete local authenticated session; inspect server record and local acknowledgement.','One canonical validated session and matching derived assessment.'],
 ['Failed upload retry','E-20','Disconnect after send before acknowledgement; restore network and retry.','Local session retained; eventual acknowledgement; no duplicate rows.'],
 ['Duplicate upload prevention','E-18','Send same session concurrently and replay key with changed payload.','Exact replay returns existing result; altered payload rejected.'],
 ['Student restrictions','F-16','Sign in as student A; request student B profile, session and grade via API and direct database client.','All cross-student access denied.'],
 ['Instructor restrictions','F-17','Instructor queries assigned and unassigned classes and their students/sessions.','Only assigned class records accessible.'],
 ['Administrator permissions','F-18','Admin manages authorized profile/role/class/enrollment/scenario/session records; retry as student.','Admin actions allowed and auditable; student escalation denied.'],
 ['Unauthorized API','E-23','Call protected endpoints unauthenticated, expired token, wrong role and manipulated score/session payload.','401/403 or validation rejection; no mutation or secret leakage.'],
 ['Supabase RLS','F-16, F-17, F-18, F-19','Run SQL/API role matrix for anon, two students, two instructors and admin; test read and write.','Constraints and RLS enforce own-record/class/admin boundaries and role immutability.'],
 ['Quest 3S controls','A-03, A-04, A-05, A-19','On physical Quest 3S test move, turn, grab, release, wheel, all tools, UI confirm/cancel, pause/resume.','Reliable controls and feedback without conflicting locomotion.'],
 ['Performance','A-02, A-03','Profile cold load and representative full scene on physical Quest 3S for agreed duration.','Meets researcher-approved frame-rate/loading budget with captured trace.'],
 ['Regression','A-01, A-06, A-11, A-17','Run existing diagnostics and complete previously working scene tool sequence after changes.','Previously valid operations remain valid; deliberate requirement migrations documented.']
];
const correctedIds = {
  'Successful synchronization':'E-15, E-17, E-18, E-19',
  'Failed upload retry':'E-19', 'Duplicate upload prevention':'E-17',
  'Student restrictions':'F-15, F-18', 'Instructor restrictions':'F-16, F-18',
  'Administrator permissions':'F-17, F-18', 'Unauthorized API':'E-15, E-16, E-22',
  'Supabase RLS':'F-15, F-16, F-17, F-18, F-20', 'Score calculation':'D-19, D-20, D-22, D-29'
};
const result=cases.map((c,i)=>({testId:`AT-${String(i+1).padStart(2,'0')}`,name:c[0],requirements:correctedIds[c[0]]??c[1],steps:c[2],expected:c[3],actual:'Not executed end to end; consult component results separately.',result:'NOT RUN',environment:i>=18&&i<=25?'Node/Supabase/Next.js test environment (not present)':i===26||i===27?'Physical Meta Quest 3S (not verified)':'Unity 6000.5.6f1 plus required connected components',tester:'Unassigned for human acceptance',date:'Not executed; plan authored 2026-09-14',defect:'See implementation audit for known gaps',correctiveAction:'Complete corresponding matrix action, execute and attach evidence.'}));
fs.writeFileSync('docs/evidence/acceptance-test-plan.json',JSON.stringify(result,null,2)+'\n');
let md='# ViCSI test report\n\nDate: 2026-09-14. Automated tester: Codex. Human/device acceptance tester: unassigned. A passing component test does not establish a complete VR, security or device requirement. `NOT RUN` is distinct from PASS and FAIL.\n\n## Executed tests\n\nFinal execution summaries and raw artifacts are listed in the engineering report. The acceptance cases below remain unrun end to end even when a related state-boundary test passes.\n\n## Required acceptance cases\n\n';
md+='| ID | Requirement IDs | Test / steps | Expected result | Actual result | Result | Environment | Tester / date | Defect / corrective action |\n|---|---|---|---|---|---|---|---|---|\n';
md+=result.map(r=>'| '+[r.testId,r.requirements,r.name+': '+r.steps,r.expected,r.actual,r.result,r.environment,r.tester+' / '+r.date,r.defect+'; '+r.correctiveAction].map(s=>s.replaceAll('|','\\|')).join(' | ')+' |').join('\n')+'\n';
md+='\n## Reproduction\n\n- `node Tools/Audit/inspect.mjs`: repository scan; source hashes, references and credential-pattern locations only.\n- `dotnet build Tools/EvidenceScorer/EvidenceScorer.csproj --nologo` then `node --test Tools/Audit/scorer.test.mjs`: executable metric regressions.\n- Unity Play mode: add `EvidenceBoundaryTest` and `SessionPersistenceTest` to temporary GameObjects, call `RunAllTests()`, capture returned reports, destroy only test objects. Tests create their own fixtures and restore singleton references. Persistence tests delete only their uniquely named test files.\n- Existing runtime diagnostics: `RequirementEvaluatorTest.RunAllTests()`, `RelationshipStateManagerTest.RunAllTests()`, `ObjectiveStateManagerTest.RunAllTests()`. Story and ending suites require scene state/coroutines; run separately from a fresh Play session.\n- Android build: use both enabled scenes, active Android target, new build output path. Build success is not device acceptance.\n';
fs.writeFileSync('docs/VICSI_TEST_REPORT.md',md);
console.log(`${result.length} acceptance cases documented.`);
