"""Audit persisted bottle setup against its pre-change scene snapshot."""
import json
import re
from pathlib import Path

root=Path(__file__).resolve().parents[1]
out=root/"docs/evidence/bottle-on-scene"


def records(path):
    return {m[2]:(m[1],m[0]) for m in re.finditer(
        r"^--- !u!(\d+) &(\d+)[^\n]*\n.*?(?=^--- !u!|\Z)",path.read_text(encoding="utf-8-sig"),re.M|re.S)}


old=records(out/"before/CSI_Environment.unity")
new=records(root/"Assets/_Project/Scenes/Scenarios/CSI_Environment.unity")
transforms={}
parents={}
go_for={}
for key,(kind,text) in new.items():
    go=re.search(r"^  m_GameObject: \{fileID: (\d+)\}",text,re.M)
    parent=re.search(r"^  m_Father: \{fileID: (\d+)\}",text,re.M)
    if go: go_for[key]=go[1]
    if parent and go: transforms[go[1]]=key; parents[key]=parent[1]
prefab=next(k for k,(kind,s) in old.items() if kind=="1001" and "value: Evidence_Emptyliquorbottle\n" in s)
bottle=next(k for k,(kind,s) in old.items() if kind=="1" and f"m_PrefabInstance: {{fileID: {prefab}}}" in s)
for key,(kind,text) in new.items():
    if kind=="4" and f"m_PrefabInstance: {{fileID: {prefab}}}" in text:
        transforms[bottle]=key; go_for[key]=bottle


def bottle_member(key):
    kind,text=new[key]
    go=key if kind=="1" else go_for.get(key)
    t=transforms.get(go)
    while t:
        if go_for.get(t)==bottle: return True
        t=parents.get(t)
    return False


changed=[k for k in old.keys()&new.keys() if old[k]!=new[k]]
added=list(new.keys()-old.keys())
removed=list(old.keys()-new.keys())
unexpected=[]
for k in changed:
    if k==prefab: continue
    if bottle_member(k): continue
    text=old[k][1]
    if "Assembly-CSharp::STCSManager" in text or "Assembly-CSharp::FingerprintStationTool" in text: continue
    unexpected.append(k)
added_outside=[k for k in added if not bottle_member(k)]
checks={"no_removed_records":not removed,"changed_records_scoped_to_bottle_station_and_dialogue":not unexpected,
        "new_records_are_bottle_print_children":not added_outside,
        "bottle_prefab_placement_and_visual_overrides_preserved":old[prefab][1].split("    m_RemovedComponents:")[0]==new[prefab][1].split("    m_RemovedComponents:")[0]}
result={"checks":checks,"changed":sorted(changed),"addedCount":len(added),"removed":removed,
        "unexpectedChanged":unexpected,"addedOutsideBottle":added_outside}
(out/"scene-preservation.json").write_text(json.dumps(result,indent=2))
print(json.dumps(result,indent=2))
assert all(checks.values()),"Review unexpected scene changes"
