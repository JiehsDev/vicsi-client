# Restore downloadable Asset Store dependencies

The source checkpoint omits unchanged binary files from the packages below. Their editable materials, prefabs, scripts, shaders and project-specific changes remain tracked. The complete original local checkpoint is preserved on `archive/research-complete-2026-10-23` and tag `research-complete-local-2026-10-23`; those archive references are not pushed.

| Package | Publisher |
| --- | --- |
| Furniture Mega Pack - Free | dlgames |
| Hand Painted Seamless Grass Texture Vol - 3 | Innovana Games |
| Cartoon FX Remaster Free | Jean Moreno |
| Furnished Cabin | Johnny Kasapi |
| HDRP NYC-like City Buildings Set PBR | MARCINS ASSETS |
| Free Sci-Fi Office Pack | Terresquall |
| UI Samples | Unity Technologies |

These names and publishers come from the locally cached Unity Asset Store packages. Exact content hashes, original paths, GUIDs and importer settings are recorded in [ASSET_STORE_DEPENDENCIES.json](ASSET_STORE_DEPENDENCIES.json). No version number or currently available download is assumed: use a package revision whose files pass the validator. Retain the original cached `.unitypackage` archives for exact research restoration.

## Fresh checkout

1. Check out `research-baseline-2026-10-23` and run `git lfs pull` for the custom assets.
2. Open with Unity **6000.5.6f1** and import the packages above through Package Manager / My Assets, or import the original cached `.unitypackage` archives. Keep their original destination paths and GUIDs.
3. Package imports can overwrite the project's edited prefabs and materials. On this **fresh checkout only**, restore the tracked project files with `git restore --source=HEAD --worktree -- Assets`.
4. Close Unity, then restore recorded importer settings and validate:

   ```sh
   python Tools/ValidateAssetStoreDependencies.py --restore-import-settings
   ```

5. Reopen Unity. All dependencies must report verified with zero missing files and zero mismatches. Recheck without changing settings using `python Tools/ValidateAssetStoreDependencies.py`.

Existing workspaces keep their downloaded files on disk. The exclusion changes Git tracking, not the playable local scene. Files that could not be verified as unchanged package content remain tracked. This does not purge assets from previously published Git history.

Validation screenshots remain in the complete local archive; GitHub retains the source and text validation reports. Some report image links therefore refer to local-only evidence. Do not push the archive references when publishing the lean source checkpoint.
