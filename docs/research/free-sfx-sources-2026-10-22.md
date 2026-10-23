# Free sound effects for ViCSI

License research checked 22 October 2026 against publisher/license pages. This document records the original shortlist. The user subsequently authorized downloading and wiring the sounds, excluding zipper, door and cabinet sounds; implementation, source-format details, coverage and accepted validation are recorded in [the implementation report](../validation/interaction-sfx-2026-10-22.md). Physical headset listening remains outstanding.

## Recommendation

Start with Kenney's CC0 Interface Sounds/UI Audio and Impact Sounds, add the individually verified Freesound CC0 recordings below for physical tools, and use Mixkit's free sound effects for writing, packaging and switches. This offers broad interaction coverage without requiring creator credits. Specialty glove, swab and fingerprint-brush recordings still need an exact clip selection or an original recording.

No provider or license can guarantee that nobody will bring a claim. CC0 does not warrant ownership or clear other people's trademark, privacy or publicity rights. The practical safeguard is an appropriate license from the original source plus retained evidence of the download and terms, rather than relying on the words "free" or "royalty-free" alone. [CC0 explanation](https://creativecommons.org/publicdomain/zero/1.0/).

## Verified licensing

| Source | Commercial game use | Creator credit required? | Relevant conditions |
| --- | --- | --- | --- |
| [Kenney Interface Sounds](https://kenney.nl/assets/interface-sounds), [UI Audio](https://kenney.nl/assets/ui-audio), [Impact Sounds](https://kenney.nl/assets/impact-sounds) | Yes, CC0 | No | Kenney explicitly confirms commercial use and no attribution in its [support page](https://kenney.nl/support). Its logo is reserved for official projects; do not imply endorsement. |
| Individually listed Freesound CC0 clips | Yes, CC0 | No | Check each sound's actual license; this approval does not extend to all Freesound clips. The original downloads shown below require login. |
| [Mixkit free sound effects](https://mixkit.co/free-sound-effects/) | Yes | No | The [sound-effects license](https://mixkit.co/license/modal/sfxFree/) explicitly includes video games. Use free sound-effect entries, observing the [site terms](https://mixkit.co/terms/), including restrictions on standalone stock redistribution. |
| [Sonniss GDC bundles](https://sonniss.com/gameaudiogdc/) - optional specialty library | Yes | No | [Current bundle license](https://sonniss.com/gdc-bundle-license/) version 2.0, effective 27 August 2026: permits incorporation into finished games, forbids supplying the recordings as an SFX/asset library and forbids AI training/development uses described in the license. The version on the download date governs, even if an older copy is bundled inside the archive. No individual clip from this collection was selected in this research. |

Mixkit's music has a different license: its [music page](https://mixkit.co/free-stock-music/) excludes video-game use. That restriction does not apply to its separately licensed sound effects. Do not treat this SFX recommendation as approval for Mixkit background music.

## Interaction shortlist

Entries are candidates for auditioning; suggested uses/adaptations are our sound-design judgments, not claims that a recording was made with a forensic tool.

| ViCSI interaction | Specific candidate / resource | License | Selection note |
| --- | --- | --- | --- |
| Tool/utility wheel, Settings, Recorder select/deselect/confirm, inventory, album and review controls | [Kenney Interface Sounds](https://kenney.nl/assets/interface-sounds), [UI Audio](https://kenney.nl/assets/ui-audio) | CC0 | Pick quiet neutral clicks for ordinary actions. Recorder selection/confirmation sounds must not reveal whether an answer is supported. |
| Photography shutter | [Camera Shutter - roachpowder](https://freesound.org/s/170229/) | CC0 | A 0.295-second mono camera recording; candidate for the existing shutter hook. Alternative: "Camera shutter click" on [Mixkit Click](https://mixkit.co/free-sound-effects/click/). |
| Flashlight/UV beam toggle | "On or off light switch tap" on [Mixkit Click](https://mixkit.co/free-sound-effects/click/) | Mixkit SFX Free | Candidate for the existing beam-click hook. |
| Measuring tape extend/retract | [Tape Measure_1.wav - NinjaSharkStudios](https://freesound.org/people/NinjaSharkStudios/sounds/565501/) | CC0 | 2.332 seconds; audition/cut separate extension/retraction portions if suitable. |
| Body/evidence chalk marking | [writing - chalk - center 01.wav - Anthousai](https://freesound.org/people/Anthousai/sounds/398491/) | CC0 | 6.095-second chalkboard recording; adapt a short section to marking contact and stop when contact stops. Surface realism needs auditioning. |
| Access-register signing, Recorder writing, sketch annotations | "Signing on a paper", "Writing scribble on paper", "Writing pencil" on [Mixkit Paper](https://mixkit.co/free-sound-effects/paper/) | Mixkit SFX Free | Match pen/pencil character to the visible tool; do not play repeated writing for every highlighted option. |
| Paper records/album page movement and paper evidence packaging | "Page turn single", "Paper crinkle", "Papers being moved or wrinkling" on [Mixkit Paper](https://mixkit.co/free-sound-effects/paper/) | Mixkit SFX Free | Candidate short movement/fold sounds; not a source of automatic procedure-completion feedback. |
| Fingerprint lifting tape / sealing tape | "Duct tape bandage" on [Mixkit Paper](https://mixkit.co/free-sound-effects/paper/) | Mixkit SFX Free | Audition and trim a suitable peel/tear section; a substitute texture rather than a verified fingerprint-tape recording. |
| Evidence-bag or body-bag zipper, where the visible closure is a zipper | [Zipper Zipping Slow.wav - bmacphail](https://freesound.org/people/bmacphail/sounds/569540/) | CC0 | 2.465-second mono recording. The uploader requests project comments but explicitly says use is allowed regardless; this is not a mandatory attribution condition. |
| Container lids / small plastic closures | "Plastic bubble click" on [Mixkit Click](https://mixkit.co/free-sound-effects/click/) | Mixkit SFX Free | Audition for a restrained closure click; a candidate substitute, not a claimed recording of the game's container. |
| Evidence tent placement, evidence-box deposits, small object set-downs and footsteps | [Kenney Impact Sounds](https://kenney.nl/assets/impact-sounds) | CC0 | 130-file impact/foley pack; select material-appropriate contacts after inspecting/listening to the archive. Avoid presenting a breaking sound as ordinary handling of intact evidence. |
| Doors, cabinets and drying-cabinet hardware | [Mixkit Doors](https://mixkit.co/free-sound-effects/doors/) | Mixkit SFX Free | Select normal opening/closing/latch sounds after auditioning; match door size/material and animation timing. |

## Remaining specialty selections

No exact clip has yet been verified for glove donning/removal, swab-on-surface contact or gentle fingerprint powder brushing. These can be recorded originally with the actual materials or sourced as additional individually verified CC0 clips. The Sonniss GDC collection is an additional search option under its own license. Do not assume a generic wet/scrape sound accurately represents these procedures.

## Current integration points observed

- `Feedback/InteractionFeedback.cs`: confirm/blocked clip slots and generated fallback tones.
- `Interaction/PhotographTool.cs`: shutter clip/source.
- `Interaction/FlashlightTool.cs`: beam toggle clip/source.
- `Interaction/ChalkMarkTool.cs`: chalk-contact looping clip/source.
- `Interaction/EvidenceBagTool.cs`: insertion clip.
- `Interaction/EvidenceTentTool.cs`: placing clip.
- `Interaction/SwabTool.cs`: swab contact and container-close clips.

These are existing hooks, not proof that all have authored clips assigned. Additional footsteps, furniture, tape, packaging and supply sounds need an implementation audit before integration. This research made no runtime changes.

## Internal license evidence for eventual imports

Keep an internal manifest of imported filename, original title/creator, original source URL, source license URL/version, acquisition date, original-file SHA-256 and edits. Keep the downloaded archive's license plus a dated copy of the live license/item page. These records are internal provenance; they do not require an in-game credit screen for the no-attribution sources above. Retain the original recording alongside edited derivatives. Do not put proprietary-library raw recordings in a public source/asset download without checking redistribution permission.

Exclude CC BY clips from this no-credit shortlist, and exclude CC BY-NC clips from a commercial-use shortlist. For example, Freesound's [writing-chalk-short-01.wav by newagesoup](https://freesound.org/people/newagesoup/sounds/377842/) is Attribution 4.0 and therefore was not selected, despite matching the sound search.
