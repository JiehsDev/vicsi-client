# Checkpoint and development workflow

The user asked to preserve the accepted 2026-10-26 state and isolate future changes until they explicitly choose to merge them.

- Research workspace: `C:/Users/Roduf Eleu/My project (1)`, development branch `develop/research`, stable branch `stable/research`, immutable checkpoint tag `good-research-2026-10-26`.
- Competitive workspace: `C:/Users/Roduf Eleu/ViCSI Competitive`, development branch `develop/competitive`, stable branch `stable/competitive`, immutable checkpoint tag `good-competitive-2026-10-26`.
- Before future file edits, check the active branch. Work on the matching development branch or a feature branch based on it. Do not edit, commit, reset or force-push stable branches or move/delete the good checkpoint tags.
- Promote development into its matching stable branch only when the user explicitly asks to merge or promote that version. A request for routine improvements does not authorize promotion. Once promotion is authorized, proceed without requesting the same permission again.
- Keep the editions separate. Port shared fixes deliberately; do not merge the two editions' branches into one another automatically.
- Existing local Git guards reject commits and merges on `stable/*` unless `VICSI_APPROVED_PROMOTION=1` is set for an explicitly authorized promotion. The original good tags must remain unchanged even after a later promotion.
- Preserve excluded downloadable Asset Store assets locally. Do not add large dependency packs, APKs, raw logs, validation screenshots, secrets or generated Unity folders to source commits. See `docs/ASSET_STORE_SETUP.md`.
- The 2026-10-26 checkpoint preserves source and authored assets; it does not claim that the latest changes have been rebuilt or fully tested on a headset. Existing APK backups are older artifacts, identified separately.

See `docs/CHECKPOINT_WORKFLOW.md` for restore and promotion instructions.
