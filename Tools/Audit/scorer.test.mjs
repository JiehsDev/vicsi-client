import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';

// Build Tools/EvidenceScorer first. These exercise the real net8.0 executable.
const dll = path.resolve('Tools/EvidenceScorer/bin/Debug/net8.0/EvidenceScorer.dll');
const truth = {
  scenarioId: 'test-house',
  lifecycleSequence: ['NotFound', 'Found', 'PhotographedRaw', 'ChalkMarked', 'Marked', 'PhotographedFinal', 'Logged', 'ReadyForCollection', 'Collected', 'Sealed', 'Processed'],
  evidence: [
    {evidenceId:'critical-a', relevance:'Critical'}, {evidenceId:'critical-b', relevance:'Critical'},
    {evidenceId:'relevant', relevance:'Relevant'}, {evidenceId:'distractor', relevance:'Distractor'}]
};
const event = (sequenceNumber, eventType, targetId, payload={}) => ({sessionId:'fixture-session',sequenceNumber,timestampMs:sequenceNumber*100,eventType,targetId,payload:Object.entries(payload).map(([key,value])=>({key,value}))});
const run = (events, groundTruth=truth) => {
  const dir=fs.mkdtempSync(path.join(os.tmpdir(),'vicsi-scorer-test-'));
  try {
    fs.writeFileSync(path.join(dir,'input.jsonl'),events.map(e=>JSON.stringify(e)).join('\n')+'\n');
    fs.writeFileSync(path.join(dir,'truth.json'),JSON.stringify(groundTruth));
    const result=spawnSync('dotnet',[dll,path.join(dir,'input.jsonl'),path.join(dir,'truth.json'),path.join(dir,'out.json')],{encoding:'utf8'});
    return {...result, report:fs.existsSync(path.join(dir,'out.json'))?JSON.parse(fs.readFileSync(path.join(dir,'out.json'),'utf8')):null};
  } finally {
    for(const file of ['input.jsonl','truth.json','out.json']) if(fs.existsSync(path.join(dir,file)))fs.unlinkSync(path.join(dir,file));
    fs.rmdirSync(dir);
  }
};
const session = [event(0,'SessionStarted','test-house'),event(1,'EvidenceStatusChanged','critical-a',{status:'Marked'}),event(2,'EvidenceStatusChanged','relevant',{status:'Marked'}),event(3,'NonEvidenceMarked','floor'),event(4,'SessionEnded')];
test('known fixture produces expected recall and precision',()=>{
  const r=run(session); assert.equal(r.status,0,r.stderr);
  // CLI report rounds ratios to four decimal places.
  assert.equal(r.report.criticalRecall,0.5);assert.equal(r.report.relevantRecall,1);assert.equal(r.report.precision,0.6667);assert.equal(r.report.distractorFallRate,0);
});
test('identical input produces identical report',()=>assert.deepEqual(run(session).report,run(session).report));
test('additional marked states do not double-count identified evidence',()=>{
  const r=run([...session,event(5,'EvidenceStatusChanged','critical-a',{status:'Processed'})]);
  assert.equal(r.status,0,r.stderr); assert.equal(r.report.criticalRecall,0.5);assert.equal(r.report.precision,0.6667);
});
test('empty identification denominators are explicit unavailable values',()=>{
  const r=run([event(0,'SessionStarted','test-house'),event(1,'SessionEnded')]);
  assert.equal(r.status,0,r.stderr);assert.equal(r.report.precision,'NaN');
});
test('missing lifecycle export is rejected',()=>{
  const {lifecycleSequence,...oldTruth}=truth; const r=run(session,oldTruth);
  assert.equal(r.status,2);assert.equal(r.report,null);
});
test('empty event log is rejected',()=>{
  const r=run([]); assert.equal(r.status,2);assert.equal(r.report,null);
});
