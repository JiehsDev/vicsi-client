# Research baseline — 2026-10-23

Git tag: `research-baseline-2026-10-23`.

This checkpoint preserves the current Unity research edition before competitive-edition work begins. No competitive rules, rankings, timers or edition-selection menu have been added.

Unity version: **6000.5.6f1**. Primary scenes: `Assets/_Project/Scenes/Main.unity` and `Assets/_Project/Scenes/Scenarios/CSI_Environment.unity`.

The published checkpoint includes authored assets, project configuration, tools and text validation reports. Unchanged downloadable Asset Store binaries and validation screenshots are kept locally. Generated builds, raw editor logs, local credentials and validation scene backups are excluded. Git LFS is required to restore custom binary assets. Follow [ASSET_STORE_SETUP.md](ASSET_STORE_SETUP.md) to import the excluded packages and restore their GUIDs and importer settings.

The complete pre-exclusion state, including screenshots and Asset Store binaries, is preserved locally on `archive/research-complete-2026-10-23` and tag `research-complete-local-2026-10-23`. These references are intentionally not pushed. The published baseline consolidates the previously unpushed checkpoints while retaining all application changes.

Restore a separate research working branch from this checkpoint:

```sh
git fetch origin --tags
git switch -c research-session research-baseline-2026-10-23
git lfs pull
```

Then restore the Asset Store dependencies using the setup guide above.

Keep competitive development on a separate branch and build. Do not move the baseline tag or mix competitive sessions into research exports.

Recent recorder changes use catalog version 2 while preserving version 1 for saved records. The paired dashboard must support both catalogs; that compatibility work exists in the separate local dashboard workspace and is not deployed by this Unity repository checkpoint. This checkpoint does not create or certify a new playable build. Validation details remain under `docs/validation/`.
