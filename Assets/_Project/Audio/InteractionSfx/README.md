# ViCSI interaction audio

Edit clip assignments and master category gains in `Assets/Resources/InteractionSfxLibrary.asset`. The saved CSI_Environment scene contains an `Interaction SFX` object with a 2D UI source and eight spatial voices. Existing camera, flashlight, chalk, evidence-bag, numbered-tent and swab components retain their own editable clip/source fields.

The scene-setup menu `Tools > ViCSI > Scene Setup > Install Interaction Sound Effects` restores the selected defaults and saves a scene backup. Use it deliberately: normal Inspector changes do not require re-running it.

Recorder selection and confirmation sounds are neutral and must remain independent of statement points. Footsteps follow real travel and stop for menus, height-only motion and teleports. The chalk recording plays only on guide contact and stops for menus/holstering. No zipper, door or cabinet clips are installed.

Sources, original recordings, acquisition pages, license copies and SHA-256 hashes: `docs/licenses/interaction-sfx-2026-10-22/manifest.json`. Kenney and Freesound clips use CC0; Mixkit files use its Sound Effects Free License. No attribution screen is required under the checked sound licenses. Keep these provenance records when replacing clips; do not assume every free sound on a provider's site has the same license.

Freesound clips were obtained as openly offered HQ previews; Mixkit clips are its original WAV downloads. The imported mono WAV excerpts are trimmed and have short fades; Unity uses 44.1 kHz Vorbis with preload/decompression for these short cues. See `docs/validation/interaction-sfx-2026-10-22.md` for coverage and validation limits.
