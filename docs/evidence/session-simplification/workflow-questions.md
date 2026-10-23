# Unscored workflow checkpoint inventory

Source: `Assets/_Project/Data/Scenarios/HypothesisCheckpoints_CSI_Environment.asset`, `HypothesisCheckpointManager`, and `FinalDeductionAssessment`. These workflow choices do not contribute numerical weights to scoring rules 1.7.0. Correctness flags below are training flags, not deduction points. HYP-01 and HYP-03 remain active. HYP-02 is replaced in the current interaction by scored RC-03, whose actual choices and weights are in the scored inventory. Its old template is listed only to distinguish legacy content from what is assessed.

## HYP-01 — active, unscored

What do you think you can reasonably say about the case so far?

Initial choices (all have deduction contribution 0):

- `theory-accidental-fall`: Peter is responsible because the vendor saw him leave.
- `theory-homicide`: Paul is responsible because he argued with John earlier.
- `theory-undetermined`: John was attacked, but there is not enough reliable evidence yet to identify the attacker.
- `theory-alcohol-explanation`: The drinking explains what happened to John.

Justification choices (all have deduction contribution 0):

- `hyp01-peter-departure-proves-guilt`: Peter's later departure and uneasy appearance establish that he attacked John. (training correctness: false)
- `hyp01-paul-argument-proves-guilt`: Paul's earlier argument with John is enough to identify him as the attacker. (training correctness: false)
- `hyp01-evidence-not-yet-developed`: Peter's timing and behavior give us questions to ask, not a conclusion. Paul still needs looking at too; we need to see what the physical findings support. (training correctness: true)
- `hyp01-drinking-explains-attack`: Earlier drinking is enough to explain the injury without further investigation. (training correctness: false)

## HYP-02 — legacy template, integrated RC-03 replaces it

What can Paul's fingerprint on the knife tell us?

Initial choices (all have deduction contribution 0):

- `theory-accidental-fall`: Paul used the knife during the attack.
- `theory-homicide`: Paul handled the knife at some point.
- `theory-undetermined`: Paul was the last person to touch the knife.
- `theory-knife-ownership`: The knife belongs to Paul.

Justification choices (all have deduction contribution 0):

- `hyp02-print-proves-attacker`: Paul's print proves he used the knife during the attack. (training correctness: false)
- `hyp02-bottle-intoxication`: Paul's earlier drinking means the print can be treated as proof of his involvement. (training correctness: false)
- `hyp02-print-needs-reconstruction`: Paul handled the knife at some point, but the print doesn't tell us when or why. We can't treat contact as proof that he attacked John. (training correctness: true)
- `hyp02-knife-routine-use`: Because the knife came from John's kitchen, the fingerprint has no investigative value. (training correctness: false)

## HYP-03 — active, unscored

How do the findings fit together at this point?

Initial choices (all have deduction contribution 0):

- `theory-accidental-fall`: Peter is responsible because the vendor saw him leave.
- `theory-homicide`: Paul is responsible because his fingerprint was found on the knife.
- `theory-undetermined`: Physical findings and any independently discovered timeline contradictions must be considered together before responsibility can be assigned.
- `theory-bottle-presence`: The liquor bottle establishes who remained at the scene.

Justification choices (all have deduction contribution 0):

- `hyp03-knife-print-implicates-brother`: Paul's fingerprint on the knife is enough on its own to identify him as the attacker. (training correctness: false)
- `hyp03-wound-alone`: John's wound identifies the kind of injury, so it also identifies who was responsible. (training correctness: false)
- `hyp03-combined-physical-evidence`: We need to weigh the wound, knife fit, contact and blood pattern alongside checked accounts. To support Paul, we also need a discovered observation that conflicts with his no-return claim, linked to that claim on the board. Contact or a sighting alone isn't proof of guilt. (training correctness: true)
- `hyp03-bottle-premature-closure`: Paul's print on the bottle places him at the house during the attack. (training correctness: false)
- `hyp03-no-contradicting-evidence`: Peter's departure and Paul's statement settle the case without needing the physical evidence. (training correctness: false)

