const fs = require('node:fs');
const { execFileSync } = require('node:child_process');
const file = 'Assets/_Project/Scripts/SceneTurnover/IOCAssessmentInteraction.cs';
const before = execFileSync('git', ['show', `HEAD:${file}`], { encoding: 'utf8' }).replace(/\r/g, '');
const after = fs.readFileSync(file, 'utf8').replace(/\r/g, '');
function body(source, name) {
  const start = source.indexOf(`public ${name}`);
  const open = source.indexOf('{', start);
  let depth = 1, end = open + 1;
  while (depth && end < source.length) { if (source[end] === '{') depth++; if (source[end] === '}') depth--; end++; }
  return source.slice(open, end);
}
const results = {
  observationTargetSelectionUnchanged: body(before, 'string ObservationAtGaze()') === body(after, 'string ObservationAtGaze()'),
  observationRecordingUnchanged: body(before, 'bool ObserveAtGaze()') === body(after, 'bool ObserveAtGaze()'),
  sevenChecklistPredicatesUnchanged: JSON.stringify(before.match(/^\s*Row\(text,.*$/gm)) === JSON.stringify(after.match(/^\s*Row\(text,.*$/gm)),
};
fs.writeFileSync('docs/evidence/ioc-ui-ergonomics/logic-compare.json', JSON.stringify(results, null, 2) + '\n');
console.log(JSON.stringify(results));
if (Object.values(results).some(value => !value)) process.exitCode = 1;
