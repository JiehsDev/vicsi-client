import fs from 'node:fs';
import crypto from 'node:crypto';

const scenePath = 'Assets/_Project/Scenes/Scenarios/CSI_Environment.unity';
const priorPath = 'docs/evidence/arrival-entry/CSI_Environment.unity.before';
const read = path => fs.readFileSync(path, 'utf8').replace(/\r\n/g, '\n');
const hash = path => crypto.createHash('sha256').update(fs.readFileSync(path)).digest('hex');
function records(source) {
  const result = new Map();
  for (const match of source.matchAll(/^--- !u!(\d+) &(-?\d+)(?: stripped)?\n([\s\S]*?)(?=^--- !u!|$(?![\s\S]))/gm))
    result.set(match[2], { type: match[1], body: match[3] });
  return result;
}
const before = records(read(priorPath)), after = records(read(scenePath));
const added = [...after.keys()].filter(id => !before.has(id));
const removed = [...before.keys()].filter(id => !after.has(id));
const changed = [...before.keys()].filter(id => after.has(id) && before.get(id).body !== after.get(id).body);
const allowedChanges = new Set(['1705414296', '1311940560', '1311940559', '121411599']);
const result = {
  scene: scenePath, comparison: priorPath, beforeSha256: hash(priorPath), afterSha256: hash(scenePath),
  recordsBefore: before.size, recordsAfter: after.size, addedRecords: added.length,
  removedRecords: removed, changedExisting: changed.map(id => ({ id, type: before.get(id).type })),
  addedGameObjects: added.filter(id => after.get(id).type === '1').map(id =>
    after.get(id).body.match(/  m_Name: (.*)/)?.[1]),
  allowedExistingChangesOnly: changed.length === 4 && changed.every(id => allowedChanges.has(id)),
  sourceHashes: Object.fromEntries([
    scenePath, 'Assets/Resources/ArrivalPPEStation.prefab', 'Assets/Resources/ViCSIDialogueCatalog.asset',
    'Assets/_Project/Scripts/SceneTurnover/ArrivalPPEStation.cs', 'Assets/_Project/Scripts/SceneTurnover/SceneCordon.cs',
    'Assets/_Project/Scripts/SceneTurnover/FirstResponderTurnover.cs', 'Assets/_Project/Scripts/SceneTurnover/SceneTurnover.cs',
    'Assets/_Project/Scripts/SceneTurnover/IOCAssessmentGuide.cs', 'Assets/_Project/Scripts/SceneTurnover/IOCAssessmentInteraction.cs',
  ].map(path => [path, hash(path)])),
};
result.passed = result.allowedExistingChangesOnly && removed.length === 0 && added.length === 15
  && result.addedGameObjects.includes('Controlled Entry Point Sign');
fs.writeFileSync('docs/evidence/arrival-ppe/scene-preservation.json', JSON.stringify(result, null, 2) + '\n');
console.log(JSON.stringify({ passed: result.passed, changedExisting: changed.length, added: added.length, removed: removed.length }));
if (!result.passed) process.exitCode = 1;
