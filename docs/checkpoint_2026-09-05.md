# After333 checkpoint - 2026-09-05

## Purpose

Preserve the accumulated working tree, including user-authored scenes, artwork,
animations, card data, runtime changes, server code, migrations and tests.
This is a preservation checkpoint, not a release certification.

## Included Work

- Battle rules, new cards, Shielder migration and combat log changes.
- Shop scene and pixel UI work across startup, collection, draft and deck building.
- Card artwork, animation sources and Unity asset metadata.
- Account-backed PvP/PvE server changes and migration 0011.
- Latest Shaolin_1st_Disciple base attack: 13; HP: 33.
- The server .csproj, previously excluded by the Unity-wide ignore pattern.

## Git Cleanup

Server bin/obj, TempBuild and Unity recovery scenes are removed from Git tracking
only. Their local files are retained. Build caches, credentials and signing keys
are excluded from future commits. No history is rewritten and no remote push is
performed.

## Recovery

The checkpoint is committed on a codex/checkpoint-* branch. A standalone Git
bundle is created under Backups after committing. To inspect a separate copy:

```powershell
git clone -b <checkpoint-branch> <absolute-path-to-bundle> <new-empty-directory>
```

The bundle contains Git history and committed files. It does not contain the
PostgreSQL database, ignored credentials, local build caches or unsaved Unity
Editor changes. Existing local files are not a substitute for an off-device
backup. Unity EditMode tests are not rerun by this preservation operation.
