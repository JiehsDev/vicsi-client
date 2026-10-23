"""Author the additive v1 relationship review; preserve existing IDs and weights."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
QUESTIONS = {
    "REL-paul-print-handled-knife": (
        "Paul's fingerprint + EVD-014 Kitchen Knife\nWhat does this relationship support?", 1, [
            "Paul's print identifies him as the person who used the knife during the attack.",
            "Paul handled the knife at some point, but the print alone does not establish when, why, or whether he used it during the attack.",
            "The print shows Paul handled the knife only during the earlier gathering and excludes later handling.",
            "Because the print cannot be dated, it provides no evidence that Paul handled the knife."]),
    "REL-knife-consistent-wound": (
        "EVD-014 Kitchen Knife + John's injury\nWhat can be concluded from their consistency?", 2, [
            "The injury proves this particular knife was used and excludes every other sharp object.",
            "The injury and knife together identify Paul as the person who inflicted the wound.",
            "The knife is consistent with John's injury and could have caused it, but this relationship alone does not identify who used it.",
            "A compatible injury establishes the attack sequence and the time at which the knife was used."]),
    "REL-paul-timeline-contradiction": (
        "Paul's statement + the vendor's observation around 3:00 AM\nHow should these accounts be interpreted?", 0, [
            "The vendor's observation contradicts Paul's claim that he went home around midnight and did not return. The vendor did not witness the attack.",
            "The observation confirms Paul's claim because it occurred after the earlier gathering.",
            "The observation proves Paul attacked John at exactly 3:00 AM.",
            "The accounts cannot be compared unless the vendor also saw what happened inside the house."]),
    "REL-spatter-consistent-reconstruction": (
        "EVD-015 Blood Spatter + the attack area\nWhat does the bloodstain evidence support?", 1, [
            "The blood pattern independently identifies the attacker and establishes intent.",
            "The bloodstain evidence supports reconstruction of the attack area but does not independently identify the attacker.",
            "The pattern establishes one exact sequence of movements and excludes all other sequences.",
            "The pattern proves the knife was held by Paul when the blood was deposited."]),
    "REL-bottle-supports-brother": (
        "EVD-018 Bottle + the reported drinking gathering\nHow should this contextual evidence be used?", 2, [
            "Handling the bottle proves that a person remained in the house until the attack.",
            "The bottle shows the attack was accidental because the gathering involved drinking.",
            "The bottle is consistent with the reported gathering and shared handling. It does not establish who was present during the attack or who committed it.",
            "The bottle establishes the order and time at which each participant handled it."]),
    "REL-peter-departure-supported": (
        "Peter's departure account + the vendor's observation around 2:00 AM\nWhat is supported by the two accounts?", 0, [
            "The vendor broadly corroborates Peter leaving around 2:00 AM, but did not witness the attack or establish everything Peter did afterward.",
            "The observation establishes Peter's location for the rest of the night and rules out every possible involvement.",
            "Leaving around 2:00 AM proves Peter left because he had attacked John.",
            "The accounts are contradictory because the vendor did not observe what happened inside the house."])
}

if __name__ == "__main__":
    path = ROOT / "Assets/Resources/DeductionScoring_1_7_0.json"
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    for rule in data["relationships"]:
        prompt, correct, choices = QUESTIONS[rule["id"]]
        rule["reviewPrompt"] = prompt
        rule["interpretations"] = [dict(id=rule["id"] + "-" + chr(65+i), text=text) for i, text in enumerate(choices)]
        rule["correctInterpretationId"] = rule["interpretations"][correct]["id"]
    text = json.dumps(data, indent=2, ensure_ascii=False) + "\n"
    path.write_text(text, encoding="utf-8")
    mirror = Path.home() / "Desktop/vicsi-dashboard/src/lib/scoring/data/csiEnvironment.v1_7.json"
    mirror.write_text(text, encoding="utf-8")
    print("Authored six reviews; preserved scoring version, IDs and 60/25/15 weights.")
