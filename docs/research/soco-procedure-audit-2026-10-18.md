# ViCSI SOCO procedure audit

Research date: 18 October 2026. Scope: current Unity code/configuration and publicly retrievable Philippine PNP procedures, with NIJ/NIST technical references for collection and preservation. This is a read-only training-content audit; no gameplay, scene, dashboard, scoring, upload or production record changes were made. No Unity tests were necessary for this research-only task.

## Authority and currency

I downloaded and extracted the public PNP Operational Procedures (September 2021), section 6.1, printed pp. 132-137 (PDF pp. 161-166), and checked the scene-investigation responsibilities and closeout requirements. [PNP manual](https://akg.pnp.gov.ph/wp-content/uploads/2024/01/POP-Manual-2021.pdf)

The Forensic Group announced a new Administrative and Operations Manual on 11 August 2025. I could verify that announcement but did not obtain its full text. Consequently, current local-unit/instructor confirmation remains necessary before describing the game as fully aligned with the latest PNP manual. [Official announcement](https://fg.pnp.gov.ph/page/2/)

NIJ/NIST publications below are supplementary forensic guidance, with their publication dates retained; they are not Philippine legal mandates.

## What already fits the broad workflow

ViCSI implements FR briefing/Form 1, IOC assessment, a SOCO request/Form 2, scene photography, measurements/sketching, item/sample identification, inventory and a final survey. That overall structure agrees with the public PNP framework. [PNP 2021, section 6.1](https://akg.pnp.gov.ph/wp-content/uploads/2024/01/POP-Manual-2021.pdf)

The game also preserves useful distinctions: the body is documented in place before its transport transition; biological samples remain linked to their source items; and the reasoning question explains that Paul's print establishes handling without establishing the time or purpose. These existing lessons should be retained.

## Findings and recommended priorities

**1. High - storage currently produces a laboratory identification.**

[EvidenceInventory.cs](<C:/Users/Roduf Eleu/My project (1)/Assets/_Project/Scripts/CaseFile/EvidenceInventory.cs:499>) calls MarkMatched when the lifted card is stored. The returned identity is authored scenario data. Teach collection, submission, comparison and reported result as distinct stages; a short, explicitly labelled simulated lab-return/time jump can preserve the current story and gates. Actual fingerprint conclusions involve examination against comparison material. [NIJ Fingerprint Sourcebook, chapter 9](https://www.ojp.gov/pdffiles1/nij/225329.pdf)

This also affects closeout: [SOCOCloseout.cs](<C:/Users/Roduf Eleu/My project (1)/Assets/_Project/Scripts/SceneTurnover/SOCOCloseout.cs:93>) requires a matched result for fingerprint-bearing items. Make clear that this combines scene work and accelerated lab work for the exercise.

**2. High - the knife uses the generic paper evidence bag.**

[EVD-014_KitchenKnife.asset](<C:/Users/Roduf Eleu/My project (1)/Assets/_Project/Data/Evidence/EVD-014_KitchenKnife.asset:295>) points to EvidencePaperBagClosed, also used for the bottle. Represent a secured, protective knife package rather than a loose sharp item in that bag. Protect the blade and remaining traces from movement and damage. [NIST-hosted Trace Materials Crime Scene Investigation Guide, pp. 16-17](https://www.nist.gov/system/files/documents/2020/08/26/CSI_Guide_07302020_ForPublication.pdf)

The existing bag is paper. I did not identify a generic plastic-bag problem in those two item definitions.

**3. High - swab preservation is not explicitly taught.**

[SwabTool.cs](<C:/Users/Roduf Eleu/My project (1)/Assets/_Project/Scripts/Interaction/SwabTool.cs:515>) permits the sample/container/seal sequence without a drying state or approved ventilated-transport instruction. Teach drying and the correct container/transport conditions. Immediate containment is not automatically wrong, but students should not learn that damp biological material can simply be sealed for indefinite storage. [NIJ biological evidence packaging](https://nij.ojp.gov/nij-hosted-online-training-courses/collecting-dna-evidence-at-property-crime-scenes/evidence-collection/biological-evidence-packaging)

**4. High - developed-print photography is not a required recovery step.**

[FingerprintKitTool.cs](<C:/Users/Roduf Eleu/My project (1)/Assets/_Project/Scripts/Interaction/FingerprintKitTool.cs:377>) allows lifting after dusting and the earlier item photograph. That earlier image predates print development. Add or explicitly represent an examination-quality record of the developed impression before lifting, with its location/orientation and scale documented. [NIJ preservation guidance, section 8.6](https://www.ojp.gov/pdffiles1/nij/225328.pdf), [NIJ documentation guidance, section 10.2](https://www.ojp.gov/pdffiles1/nij/225330.pdf)

**5. High - PPE and cross-contamination precautions are not operationally represented.**

The source search found a PPEEquipped event declaration, but no active PPE/glove-change or clean-tool workflow. Explicitly teach appropriate protection and changing/cleaning collection equipment between unrelated items. If the avatar arrives already protected, state that assumption; a full dressing simulation is unnecessary. [NIJ training reference, contamination-control section](https://www.ojp.gov/pdffiles1/nij/200160.pdf)

**6. Medium - the session timeline is not a scene-access register.**

Current logging covers player actions but I found no dedicated record of scene entrants and their entry/exit. Represent the access record or identify the NPC responsible for maintaining it. The PNP manual lists persons-entering-scene documentation among the CSI records. [PNP 2021, section 6.1 and annex JJ](https://akg.pnp.gov.ph/wp-content/uploads/2024/01/POP-Manual-2021.pdf)

**7. Medium - custody tracking is useful but incomplete as a teaching record.**

[EvidenceInventory.cs](<C:/Users/Roduf Eleu/My project (1)/Assets/_Project/Scripts/CaseFile/EvidenceInventory.cs:224>) records source/item IDs, locations, a box ID and transfer ID; session events also have timestamps. That is a useful foundation. Add or represent the releasing and receiving custodians, purpose, package/seal condition and acknowledgement. Maintain custody for derived samples too. [NIST/NIJ preservation handbook, section IV](https://www.nist.gov/system/files/documents/forensics/NIST-IR-7928.pdf)

These technical records can remain behind the simplified educational dashboard. They do not require restoring analytics or changing scoring.

**8. Medium - photography needs clearer coverage and scale instruction.**

The seven route photographs and raw/marked evidence images are useful. [PhotographTool.cs](<C:/Users/Roduf Eleu/My project (1)/Assets/_Project/Scripts/Interaction/PhotographTool.cs:579>) does not explicitly validate photographic scale or a complete overall/midrange/close-up series. Teach those purposes; a numbered tent and a location measurement do not themselves provide a photographic scale. [NIJ training reference, documentation section](https://www.ojp.gov/pdffiles1/nij/200160.pdf)

**9. Medium - closeout/release wording needs reconciliation.**

[SOCOCloseout.cs](<C:/Users/Roduf Eleu/My project (1)/Assets/_Project/Scripts/SceneTurnover/SOCOCloseout.cs:213>) intentionally leaves the cordon secured, while [ProcedureTimeline_1.json](<C:/Users/Roduf Eleu/My project (1)/Assets/Resources/ProcedureTimeline_1.json:45>) describes scene release. Teach the IOC's release authority, the joint final survey and required release documentation. If the scenario ends at SOCO handback while security continues, label that boundary accordingly. The virtual IOC report can remain a simulation. [PNP 2021, section 6.1(a)(3) and (d)](https://akg.pnp.gov.ph/wp-content/uploads/2024/01/POP-Manual-2021.pdf)

**10. Instructor review - blanket chalk and fixed sequence rules.**

All four active item definitions currently require chalk. Treat that uniform requirement as a course-specific choice until validated locally. Older PNP training material refers to marking a victim's position; that does not establish a requirement to draw circles around every bottle, knife or stain. I would not remove body marking solely on the basis of foreign popular descriptions. [Older PNP manual, indexed primary document](https://www.pnpcocpo.com/wp-content/uploads/2015/04/Criminal_Investigation_Manual.pdf)

Likewise, all seven scene photos before every processing action is a scenario rule. Brief students that urgent/perishable evidence can require adjusted priorities. The existing swab-before-powder sequence is sensible for this authored knife, but processing methods depend on the surface, trace and laboratory strategy. [NIJ training reference, prioritization section](https://www.ojp.gov/pdffiles1/nij/200160.pdf)

## Scope clarifications

- The FR dialogue identifies the vendor and passes along an initial account. Witness identification is therefore already represented. The Paul-specific follow-up gate is a scripted lead; avoid teaching it as a real prerequisite for interviewing witnesses.
- FR medical/safety actions can remain outside the playable sequence, but the handover should establish what was checked and anything altered during emergency response. Current dialogue describes a suspected violent death without detailing those actions. [PNP 2021, section 6.1(a)(1)](https://akg.pnp.gov.ph/wp-content/uploads/2024/01/POP-Manual-2021.pdf)
- Formal documents need not become lengthy forms to fill out. Concise reviewed/generated records can teach their purpose. An IOC-to-SOCO evidence transfer document is relevant when the IOC actually collected items; do not manufacture that step where it did not occur.
- Recommended first corrections: explicit simulated lab identification, knife packaging, biological preservation, developed-print documentation and contamination instruction. Preserve internal IDs, procedural non-scoring, the existing deduction rubric and historical compatibility.

The downloaded public manual and extracted research text were moved to a temporary research cache to avoid adding a large source PDF to the project. No code fixes have been applied by this audit.
