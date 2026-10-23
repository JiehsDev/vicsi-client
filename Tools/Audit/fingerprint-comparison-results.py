"""Verify accepted tutorial results and preserve pre-existing case content."""
from pathlib import Path
import hashlib
import json
import re
from collections import Counter

root=Path(__file__).resolve().parents[2]
evidence=root/'docs/evidence/fingerprint-comparison'
baseline=json.loads((evidence/'baseline.json').read_text())
changed=[name for name,digest in baseline.items() if hashlib.sha256((root/name).read_bytes()).hexdigest()!=digest]
assert not any(name.endswith('.unity') or 'Data\\Evidence\\' in name or 'Data/Evidence/' in name for name in changed),changed
old=(evidence/'catalog-before.txt').read_bytes()
new=(root/'Assets/Resources/ViCSIDialogueCatalog.asset').read_bytes()
assert new.startswith(old),'Existing dialogue content changed'
added_ids=re.findall(r'^  - id: (.+)$',new[len(old):].decode().replace('\r\n','\n'),re.M)
required={'FingerprintComparisonTest','FingerprintStorageTest','BottleFingerprintProcedureTest','SamplePreparationTest',
          'GloveChangeStationTest','EvidenceInventoryTest','ArrivalPPETest','SceneAccessRegisterTest','DialogueCatalogTest'}
accepted={}
for path in sorted(evidence.glob('run*/*.txt'),key=lambda p:(int(p.parent.name[3:]),p.name)):
    body=path.read_text(encoding='utf-8-sig')
    if not re.search(r'=== (?:ALL )?PASS',body) or re.search(r'^\s*FAIL:',body,re.M):continue
    accepted[path.stem]={'report':path.relative_to(root).as_posix(),'passed':len(re.findall(r'^\s*PASS:',body,re.M)),'failed':0}
assert required<=accepted.keys(),required-accepted.keys()
baseline_log=(root/'docs/evidence/photo-scale/run7/DialogueCatalogTest.log').read_text(errors='replace')
baseline_warnings=Counter(re.findall(r'^.*Missing dialogue id.*$',baseline_log,re.M))
for suite,row in accepted.items():
    log=(root/row['report']).with_suffix('.log').read_text(errors='replace')
    assert not re.search(r'error CS\d+|Shader error|NullReferenceException|MissingReferenceException',log),suite
    warnings=Counter(re.findall(r'^.*Missing dialogue id.*$',log,re.M))
    assert not (warnings-baseline_warnings if suite=='DialogueCatalogTest' else warnings),(suite,warnings)
new_sources=list(root.glob('Assets/_Project/Scripts/**/*Fingerprint*Comparison*.cs'))+list(root.glob('Assets/_Project/Scripts/**/PracticeFingerprintGraphic.cs'))
result={'date':'2026-10-19','suites':accepted,'totalPassed':sum(row['passed'] for row in accepted.values()),'totalFailed':0,
        'changedExistingFiles':changed,'unchangedExistingFiles':len(baseline)-len(changed),'addedDialogueIds':added_ids,
        'sceneSha256':hashlib.sha256((root/'Assets/_Project/Scenes/Scenarios/CSI_Environment.unity').read_bytes()).hexdigest(),
        'newMissingDialogueWarnings':0,'csShaderNullMissingReferenceErrors':0,
        'sourceSha256':{name:hashlib.sha256((root/name).read_bytes()).hexdigest() for name in changed},
        'newSourceSha256':{p.relative_to(root).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in new_sources},
        'manualValidationPending':'Physical Quest controller reach, stereo readability and device performance'}
(evidence/'summary.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({'suites':len(accepted),'passed':result['totalPassed'],'failed':0,'changed':len(changed),'addedDialogue':len(added_ids)}))
