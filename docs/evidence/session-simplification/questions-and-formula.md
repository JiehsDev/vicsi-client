# Current questions and scoring audit

Read directly from current Unity assets and source. Scoring rules 1.7.0.

Reasoning = 60 × earned / 24. Evidence Relationships = 25 × max(0, correct relationship weights − unsupported count × 1) / 6. Final Conclusion = 15 × context-dependent credit. Total = clamp(sum, 0, 100). Procedures contribute no points.

## RC-01-PETER — Reasoning

Based on what you know so far, what do you make of Peter leaving the house looking nervous?

- RC-01-PETER-A — **1**: Peter probably did it. Leaving nervously after being with John is enough to make him the main suspect.
- RC-01-PETER-B — **3**: Peter could be involved. His behavior makes him worth looking into, but I need more information.
- RC-01-PETER-C — **4**: I would not treat nervous behavior as proof of guilt. It gives me a reason to ask questions, not a reason to accuse him.
- RC-01-PETER-D — **2**: I don't think Peter matters to the investigation.

Maximum: 4.

## RC-02-SCENE — Reasoning

What can you reasonably conclude from the scene at this point?

- RC-02-SCENE-A — **1**: The knife near John must be the murder weapon.
- RC-02-SCENE-B — **4**: The knife and blood are important, but they still need proper examination before I can say what happened.
- RC-02-SCENE-C — **1**: There isn't enough here to justify further forensic examination.
- RC-02-SCENE-D — **3**: The scene suggests a violent incident, but I still need the physical evidence to understand it.

Maximum: 4.

## RC-03-FINGERPRINT — Reasoning

Paul's fingerprint was recovered from the knife. What does that tell you?

- RC-03-FINGERPRINT-A — **1**: Paul must have used the knife to attack John.
- RC-03-FINGERPRINT-B — **3**: It makes Paul more suspicious, but I still need other evidence before deciding what the print means in this case.
- RC-03-FINGERPRINT-C — **4**: It shows Paul handled the knife at some point, but the print alone doesn't tell me when he touched it or what he did with it.
- RC-03-FINGERPRINT-D — **2**: The fingerprint doesn't matter because Paul knew John.

Maximum: 4.

## RC-04-KNIFE-WOUND — Reasoning

The knife is consistent with John's wound. How should that finding be interpreted?

- RC-04-KNIFE-WOUND-A — **1**: This proves the knife was used to kill John.
- RC-04-KNIFE-WOUND-B — **4**: The knife could be the weapon because its characteristics are consistent with the wound, but that relationship alone does not prove who used it.
- RC-04-KNIFE-WOUND-C — **3**: The knife is probably involved, but I would still want the other findings before relying on it.
- RC-04-KNIFE-WOUND-D — **1**: If the knife fits the wound, whoever touched it must be the attacker.

Maximum: 4.

## RC-05-TIMELINE — Reasoning

The vendor says he saw Paul near John's house around three in the morning. How does that affect Paul's account?

- RC-05-TIMELINE-A — **1**: It proves Paul came back and killed John.
- RC-05-TIMELINE-B — **4**: It makes Paul's story questionable because the vendor's observation conflicts with his claim that he never returned.
- RC-05-TIMELINE-C — **3**: It makes Paul more suspicious, but the vendor still did not see him enter the house or attack John.
- RC-05-TIMELINE-D — **2**: It doesn't matter because the vendor didn't witness the attack.

Maximum: 4.

## RC-06-BLOODSTAIN — Reasoning

What does the bloodstain finding contribute to the case?

- RC-06-BLOODSTAIN-A — **1**: It identifies Paul as the attacker.
- RC-06-BLOODSTAIN-B — **4**: It helps establish where the attack occurred, but it does not identify the attacker by itself.
- RC-06-BLOODSTAIN-C — **3**: It supports the physical reconstruction of what happened near John, but it still needs to be considered with the other evidence.
- RC-06-BLOODSTAIN-D — **2**: The blood only confirms that John was injured, so it adds nothing else.

Maximum: 4.

## REL-bottle-supports-brother — Evidence Relationships

EVD-018 Bottle + the reported drinking gathering
How should this contextual evidence be used?

- REL-bottle-supports-brother-A — **-1**: Handling the bottle proves that a person remained in the house until the attack.
- REL-bottle-supports-brother-B — **-1**: The bottle shows the attack was accidental because the gathering involved drinking.
- REL-bottle-supports-brother-C — **1**: The bottle is consistent with the reported gathering and shared handling. It does not establish who was present during the attack or who committed it.
- REL-bottle-supports-brother-D — **-1**: The bottle establishes the order and time at which each participant handled it.

Maximum: 1.

## REL-knife-consistent-wound — Evidence Relationships

EVD-014 Kitchen Knife + John's injury
What can be concluded from their consistency?

- REL-knife-consistent-wound-A — **-1**: The injury proves this particular knife was used and excludes every other sharp object.
- REL-knife-consistent-wound-B — **-1**: The injury and knife together identify Paul as the person who inflicted the wound.
- REL-knife-consistent-wound-C — **1**: The knife is consistent with John's injury and could have caused it, but this relationship alone does not identify who used it.
- REL-knife-consistent-wound-D — **-1**: A compatible injury establishes the attack sequence and the time at which the knife was used.

Maximum: 1.

## REL-paul-print-handled-knife — Evidence Relationships

Paul's fingerprint + EVD-014 Kitchen Knife
What does this relationship support?

- REL-paul-print-handled-knife-A — **-1**: Paul's print identifies him as the person who used the knife during the attack.
- REL-paul-print-handled-knife-B — **1**: Paul handled the knife at some point, but the print alone does not establish when, why, or whether he used it during the attack.
- REL-paul-print-handled-knife-C — **-1**: The print shows Paul handled the knife only during the earlier gathering and excludes later handling.
- REL-paul-print-handled-knife-D — **-1**: Because the print cannot be dated, it provides no evidence that Paul handled the knife.

Maximum: 1.

## REL-paul-timeline-contradiction — Evidence Relationships

Paul's statement + the vendor's observation around 3:00 AM
How should these accounts be interpreted?

- REL-paul-timeline-contradiction-A — **1**: The vendor's observation contradicts Paul's claim that he went home around midnight and did not return. The vendor did not witness the attack.
- REL-paul-timeline-contradiction-B — **-1**: The observation confirms Paul's claim because it occurred after the earlier gathering.
- REL-paul-timeline-contradiction-C — **-1**: The observation proves Paul attacked John at exactly 3:00 AM.
- REL-paul-timeline-contradiction-D — **-1**: The accounts cannot be compared unless the vendor also saw what happened inside the house.

Maximum: 1.

## REL-peter-departure-supported — Evidence Relationships

Peter's departure account + the vendor's observation around 2:00 AM
What is supported by the two accounts?

- REL-peter-departure-supported-A — **1**: The vendor broadly corroborates Peter leaving around 2:00 AM, but did not witness the attack or establish everything Peter did afterward.
- REL-peter-departure-supported-B — **-1**: The observation establishes Peter's location for the rest of the night and rules out every possible involvement.
- REL-peter-departure-supported-C — **-1**: Leaving around 2:00 AM proves Peter left because he had attacked John.
- REL-peter-departure-supported-D — **-1**: The accounts are contradictory because the vendor did not observe what happened inside the house.

Maximum: 1.

## REL-spatter-consistent-reconstruction — Evidence Relationships

EVD-015 Blood Spatter + the attack area
What does the bloodstain evidence support?

- REL-spatter-consistent-reconstruction-A — **-1**: The blood pattern independently identifies the attacker and establishes intent.
- REL-spatter-consistent-reconstruction-B — **1**: The bloodstain evidence supports reconstruction of the attack area but does not independently identify the attacker.
- REL-spatter-consistent-reconstruction-C — **-1**: The pattern establishes one exact sequence of movements and excludes all other sequences.
- REL-spatter-consistent-reconstruction-D — **-1**: The pattern proves the knife was held by Paul when the blood was deposited.

Maximum: 1.

## Final Conclusion

Actual picker prompt: “Submit your conclusion:”. Options are dynamically filtered by EndingManager.GetSelectableCaseConclusions; Inconclusive is hidden once a supported case exists. Snapshot the options actually offered, rather than fabricating three choices for every attempt.

- conclusion-brother: Paul
Paul is a reasonable person of interest, but an accusation must rest on the combined physical evidence and timeline contradiction rather than his fingerprint alone.
- conclusion-peter: Peter
Peter's late departure and uneasy appearance justify further investigation, but an accusation still requires evidence connecting him to the attack.
- CONCLUSION-INCONCLUSIVE: Inconclusive
The current findings do not provide enough consistent evidence to identify the responsible person with confidence. Leaving the case unresolved is more defensible than making an unsupported accusation.

Credits (maximum 1): supported Paul 1; premature Paul 0.25; Peter 0; Inconclusive with insufficient support 1; Inconclusive despite full support 0.25. Each credit is multiplied by 15.

HYP-01/HYP-03 and legacy report-line correctness remain workflow/training decisions, outside the current deduction formula. HYP-02 uses the integrated RC-03 question; do not count it twice.
