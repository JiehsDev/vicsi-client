import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';

// Read-only repository inspection. Reports contain locations, never secret values.
const root = process.cwd();
const walk = (dir, skip = new Set()) => fs.existsSync(dir)
  ? fs.readdirSync(dir, { withFileTypes: true }).flatMap(e => skip.has(e.name) ? [] :
    e.isDirectory() ? walk(path.join(dir, e.name), skip) : [path.join(dir, e.name).replaceAll('\\', '/')]) : [];
const excluded = new Set(['.git', 'Library', 'Temp', 'Logs', 'obj', 'bin', 'node_modules', '.next', 'Builds', 'build', '.utmp']);
const files = walk('.', excluded);
const source = files.filter(f => /^(Assets\/(?:_Project|Editor)\/.*\.cs|Tools\/.*\.(?:cs|csproj)|Packages\/.*\.json|ProjectSettings\/.*\.asset)$/.test(f));
const inventory = source.map(file => {
  const bytes = fs.readFileSync(file); const text = bytes.toString('utf8');
  return { file, bytes: bytes.length, sha256: crypto.createHash('sha256').update(bytes).digest('hex'),
    lines: text.split('\n').length,
    types: [...text.matchAll(/\b(?:class|enum|struct)\s+(\w+)/g)].map(m => m[1]) };
});
const metaFiles = [...files.filter(f => f.endsWith('.meta')), ...walk('Library/PackageCache').filter(f => f.endsWith('.meta'))];
const guids = new Map(); const duplicateGuids = [];
for (const file of metaFiles) {
  const guid = fs.readFileSync(file, 'utf8').match(/^guid: ([a-f0-9]{32})/m)?.[1];
  if (guid) { if (guids.has(guid)) duplicateGuids.push({guid, files: [guids.get(guid), file]}); else guids.set(guid, file.slice(0, -5)); }
}
const assets = files.filter(f => f.startsWith('Assets/_Project/') && /\.(unity|prefab|asset)$/.test(f));
const unresolvedReferences = []; const scenes = [];
for (const file of assets) {
  const text = fs.readFileSync(file, 'utf8');
  const refs = [...new Set([...text.matchAll(/guid: ([a-f0-9]{32})/g)].map(m => m[1]))];
  for (const guid of refs) if (!guids.has(guid) && !/^0{16}/.test(guid)) unresolvedReferences.push({file, guid});
  if (file.endsWith('.unity') || file.endsWith('.prefab')) scenes.push({file,
    projectScripts: [...new Set([...text.matchAll(/m_Script: \{fileID: \d+, guid: ([a-f0-9]{32})/g)]
      .map(m => guids.get(m[1])).filter(f => f?.startsWith('Assets/_Project/')))].sort(),
    nullScripts: [...text.matchAll(/m_Script: \{fileID: 0\}/g)].length });
}
const evidence = files.filter(f => /^Assets\/_Project\/Data\/Evidence\/.*\.asset$/.test(f)).map(file => {
  const text = fs.readFileSync(file, 'utf8'); const fields = {};
  for (const key of ['evidenceId','displayName','requiresFingerprinting','fingerprintBeforeCollection','requiresSwabbing','collectedBySwab','examinedInPlace','minimumLinesRequired']) fields[key] = text.match(new RegExp(`^  ${key}: (.*)$`, 'm'))?.[1] ?? null;
  return { file, ...fields };
});
const markers = []; const credentialCandidates = [];
for (const file of files.filter(f => /\.(cs|json|ya?ml|sql|[cm]?js|tsx?|md|env)$/.test(f) && !f.startsWith('Assets/Samples/') && !f.startsWith('docs/evidence/'))) {
  const text = fs.readFileSync(file, 'utf8');
  text.split('\n').forEach((line, i) => {
    if (/^(Assets\/_Project|Tools\/EvidenceScorer)/.test(file) && /\b(TODO|FIXME|STUBBED|placeholder|mock)\b/i.test(line)) markers.push({file, line: i+1});
    if (/(?:sb_secret_[A-Za-z0-9_-]{12,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|(?:service_role_key|supabase_service_role_key)\s*[:=]\s*["'][^"']{16,})/i.test(line)) credentialCandidates.push({file, line: i+1});
  });
}
const report = {generatedAt: new Date().toISOString(), root, scope: 'Filesystem inventory and pattern inspection; not execution or exhaustive credential detection.',
  fileCount: files.length, inventory, scenes, evidence, unresolvedReferences, duplicateGuids, markers, credentialCandidates,
  applicationManifests: files.filter(f => /(?:package\.json|\.sql|\.csproj)$/.test(f) && !f.startsWith('Assets/')),
  inputActions: files.filter(f => f.endsWith('.inputactions'))};
fs.mkdirSync('docs/evidence', {recursive: true});
fs.writeFileSync('docs/evidence/repository-inspection.json', JSON.stringify(report, null, 2)+'\n');
if (!fs.existsSync('docs/evidence/git-status-before.txt')) fs.writeFileSync('docs/evidence/git-status-before.txt', execFileSync('git', ['status','--short'], {encoding:'utf8'}));
console.log(JSON.stringify({files: files.length, inspectedSources: inventory.length, scenesAndPrefabs: scenes.length, evidence: evidence.map(e=>e.evidenceId), unresolvedReferences, duplicateGuids: duplicateGuids.length, credentialCandidateLocations: credentialCandidates}, null, 2));
