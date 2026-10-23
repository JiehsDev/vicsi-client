# Step 2 plan: crime scene security and access register

Status: implemented and batch validated on 2026-10-19: 452 PASS / 0 FAIL across 11 suites. Physical Quest acceptance remains pending. See [validation report](../validation/scene-security-2026-10-19.md). Follows the completed arrival-glove exercise; the following is the approved implementation scope.

## Student encounter order

1. Receive the existing First Responder briefing. Add a short explanation that the First Responder maintains the controlled entrance and access register, so investigators know who entered, why, and when. Explain that limiting access helps preserve the scene and reduce contamination.
2. Put on gloves through the existing station. Its completion guidance now points to the register rather than announcing unrestricted entry.
3. Approach an access-register clipboard beside the First Responder, outside the cordon. A opens a short review; A confirms check-in with the First Responder, B cancels. Use the existing identified student information, current simulated role and phase-derived purpose; require no keyboard entry or extra personal information.
4. See registration confirmed, then enter through the controlled entrance. Actual entry time is recorded on crossing; confirmation alone leaves the visit marked Awaiting entry.
5. Continue the existing IOC assessment and later investigation. The register remains available for review. Actual exits and re-entries, including a vendor trip, update the student's visit history without repeatedly filling out the form while the same authorization remains valid.

Suggested briefing: "I will maintain the controlled entrance and access register. After putting on gloves, check in with me before entering. We record who enters, their role and purpose, and when they enter and leave. Keeping access limited helps protect the scene from disturbance and contamination."

## Register presentation

Show a compact, scrollable list with Name, Role, Purpose, Time in, Time out and Status. Display elapsed session time consistently with the educational timeline. Distinguish Awaiting entry, Inside, Exited, and Present at scenario end. The panel identifies the First Responder as the register keeper and the source of entry authorization.

Use the existing padded, flexible-height panel and controller/pointer conventions. Protect held A against opening and confirming at once. Cancelling, walking away or opening unrelated modal UI must not authorize entry. Keep the clipboard/register interaction separate from the gloves' already-completed interaction so two nearby actions cannot consume the same press.

## Entry rules and record accuracy

- Entry requires briefing, gloves and confirmed registration. Before-briefing, missing-gloves and missing-registration messages identify the next required action in that order. Reuse the existing SDK-aware head/capsule return and keep tape/posts secured and visible.
- Registration is authorization; an access visit is an actual observed crossing. Rejected crossings that are returned outside create no successful-entry record. Confirmation repeated without a crossing creates no duplicate visit.
- Use one consistent perimeter transition definition, with hysteresis/settling to prevent repeated entries when a headset or capsule hovers at the boundary. Moving between the yard, porch and kitchen must not create additional visits within the same secured area.
- Close a visit on actual exit and open a new visit on actual re-entry. Reset and lobby/new-session loading clear the register with the rest of the investigation.
- The student plays IOC and later SOCO. Keep one student identity and annotate the existing role handover. The scripted SOCO-arrival reposition must be identified as a simulation transition, not invented physical entry/exit or a second person. A fresh registration is unnecessary solely because the role label changes.
- Existing NPCs are already on scene when play starts. Do not invent their entry times, medical-team visits, historical scene entrants or signatures. Identify the First Responder's responsibility in the briefing without fabricating a pre-exercise register.
- Ending while still inside leaves a clearly labelled Present at scenario end record. Administrative closeout/scene-release events must not fabricate a physical exit or remove the cordon.

## Integration

Add a session-local access-register owner and a runtime-installed entrance interaction. SceneCordon reads its authorization alongside the existing PPE state. IOC observation guards and navigation wait for registration, preserving the original assessment sequence after check-in.

The existing SceneLogSigned enum can represent confirmed registration. Existing SceneEntered is currently emitted by RoleSceneLoader as a scene-load event, so it must not be mistaken for a physical perimeter crossing. Choose distinct, append-only access-visit events as needed, preserving existing enum ordinals and historical semantics. Internal IDs remain technical; normal register prose uses readable labels.

Keep this procedural and non-scored. Do not change the deduction rubric or add a compulsory administrative question. Store the session register locally with the existing session records. Dashboard presentation and transport of access visits require their own explicit integration decision; do not claim a local action-log row is a complete remotely stored register.

Later development presets prepare authorization inside the existing isolated sandbox, just as they prepare gloves. Normal Play must require the actual check-in interaction.

## Acceptance checks

Exercise the complete briefing, gloves, check-in and first-entry flow with real simulated controller input. Cover cancel, walking away, held buttons, wrong phase, other modal UI, missing prerequisites, and smooth/room-scale/SDK-teleport crossing attempts. Verify exactly one visit per genuine crossing, no visits for denied attempts, stable boundary behavior, vendor exit/re-entry, one student identity across the IOC/SOCO handover, truthful session-end status and restart reset.

Verify rendered long-name rows, scrolling and readable VR placement. Re-run the existing arrival/PPE, FR handover, IOC/SOCO foundation, relevant guide, vendor and development-preset checks. Preserve scene evidence, photography and measurement geometry. Physical Quest interaction, readability and boundary comfort remain manual acceptance.

## Source anchors

- Existing entry/PPE gate: Assets/_Project/Scripts/SceneTurnover/SceneCordon.cs.
- FR briefing: FirstResponderTurnover.cs and Assets/Resources/ViCSIDialogueCatalog.asset.
- Phase/role ownership: SceneTurnover.cs; scripted arrival reposition: SOCOWorkflowInteraction.AcknowledgeArrival.
- Existing scene-load logging: Assets/_Project/Scripts/RoleSystem/RoleSceneLoader.cs.
- Audit finding: docs/research/soco-procedure-audit-2026-10-18.md, scene-access-register finding.
