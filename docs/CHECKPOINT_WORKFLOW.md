# Accepted source checkpoint: 2026-10-26

| Edition | Frozen tag | Stable branch | Ongoing work |
| --- | --- | --- | --- |
| Research | `good-research-2026-10-26` | `stable/research` | `develop/research` |
| Competitive | `good-competitive-2026-10-26` | `stable/competitive` | `develop/competitive` |

The tags permanently identify today's accepted source state. Later development does not change these snapshots. The stable branches can advance only when the user explicitly accepts and requests promotion of a later version; the dated tags remain available afterward. Earlier research and competitive checkpoint tags also remain intact.

Work in the matching development branch (or a feature branch created from it). The two existing Unity project folders remain available and continue to keep the editions separate. Both projects are left on their development branches after this checkpoint. The editor Build Edition selector builds the checked-out source in its configured project folder: it does not automatically build a frozen tag.

To inspect or build today's accepted state, save current scenes, stop Play Mode, commit or stash any newer uncommitted work, then use the matching project terminal:

```powershell
git switch stable/research
# Or, inside the competitive project:
git switch stable/competitive
```

If the stable branch has since advanced, create a temporary restore branch from the dated tag:

```powershell
git switch -c restore/research-2026-10-26 good-research-2026-10-26
# Or use the competitive tag in its own project.
```

Return to `develop/research` or `develop/competitive` before making further improvements. Avoid hard resets: these commands preserve development commits and require saving uncommitted work first.

For an explicitly user-approved promotion, first run the checks appropriate to the changed gameplay, review the diff and verify the intended headset build. Then, for example:

```powershell
git switch stable/research
$env:VICSI_APPROVED_PROMOTION = '1'
try {
    git merge --no-ff develop/research
} finally {
    Remove-Item Env:VICSI_APPROVED_PROMOTION -ErrorAction SilentlyContinue
}
# Push the reviewed stable branch and create a NEW dated release tag.
# Keep good-research-2026-10-26 unchanged.
git switch develop/research
```

`Tools/Git/Install-StableGuards.ps1` installs local pre-commit and pre-merge-commit guards. They reject accidental stable-branch commits/merges and preserve existing Git LFS hooks. Run this installer in a new clone to enable the same local guard. These hooks are local checks, not GitHub branch-protection settings. `AGENTS.md` also records the user's promotion rule for future agents.

Source commits contain authored scenes, code, small decor assets and text validation reports. Downloadable Asset Store dependencies remain excluded from Git; restore them using `ASSET_STORE_SETUP.md` if opening a fresh clone. Existing local packs remain in both workspaces.

This checkpoint contains the latest lobby layout, paintings, framed flag, interactive chair, build-edition selector and vendor label adjustment. Research also includes the register layout and duplicate satisfaction-question fix. Validation scope is recorded in the linked evidence folders under `docs/evidence`.

This is a source checkpoint, not a newly certified headset release. The latest lobby changes have editor validation, and no new APK is built by this checkpoint operation. Older local APKs are copied to `C:/Users/Roduf Eleu/ViCSI Checkpoints/2026-10-26/apks` with a manifest so future builds cannot overwrite that fallback. Unrelated competitive editor preferences are preserved locally rather than included in the source checkpoint.
