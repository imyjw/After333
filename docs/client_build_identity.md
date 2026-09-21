# After333 client build identity

Unity's normal **Build** and **Build And Run** automatically prepare a new identity before packaging the player. The server compatibility version remains a separate setting.

## Included information

- Build ID: UTC date/time plus a random suffix, for example 20260913-010203-a1b2c3d4.
- Build time in UTC, application version, target, build options and Unity version.
- Git commit and working-tree state (clean, modified or unavailable).
- SHA256 of saved code/data/scenes/import settings under Assets, plus Packages and ProjectSettings.

The source hash includes paths and file contents (including .meta files), in a deterministic order. It excludes media file contents (images, audio, video, source fonts, Aseprite and PDF) and the generated ClientBuild.json/.meta. Media import .meta files remain included; unique Build IDs distinguish builds even when only excluded media changes. It identifies a saved source snapshot, not the compiled binary: build profiles, unsaved scenes, later build callbacks, tools and package-cache edits can affect output. Commit information alone never implies a clean build. A copied project inside another Git checkout or a machine without Git reports unavailable and still calculates the source hash.

## Where to find it

- In the app, open the gear menu. The footer shows the app version, build ID and UTC build time.
- Player startup logs include the full metadata as After333 client build.
- Managed Unity log messages receive an [After333 build=...] prefix. Exceptions retain the original exception object/type/stack and receive a preceding build-context line.
- Successful builds also write a companion JSON file next to the output, such as After333.apk.build.json. The same metadata is embedded in the app. A companion-file write error produces a warning without invalidating an otherwise successful player build.

No absolute workstation paths, file names from Git status, account identifiers or credentials are written into the metadata. The metadata is diagnostic only; it is not an authentication or reward-authority token.

## Generated asset and limitations

Assets/Project333/Resources/Project333/ClientBuild.json and its .meta are generated and Git-ignored. Every normal Unity player build overwrites the JSON and imports it synchronously. Failure to generate/import valid metadata fails the build instead of packaging an older identity. A failed/cancelled build may leave the last generated file on disk; the next build overwrites it.

Unity Editor Play Mode explicitly displays Unity Editor / 빌드 전, ignoring any previous player metadata. It does not install the player log wrapper. Existing APKs retain their existing identity until the user builds and updates them.

Scripts Only Build is rejected because it cannot reliably package a changed Resources asset; use normal Build or Build And Run. Rebuilding an already-exported Gradle/Xcode project without returning through Unity retains the export's identity.

This change does not increment Android versionCode, change the display/application version, alter the server compatibility contract, or freeze card definitions per run. Android store release numbering remains a separate release task.

## Implementation

- Runtime: Assets/Project333/Runtime/Presentation/Project333ClientBuildInfo.cs
- Build hook: Assets/Project333/Editor/Project333ClientBuildProcessor.cs
- UI: Assets/Project333/Runtime/Presentation/Settings/GlobalSettingsMenuController.cs

Validation is recorded in TempBuild/ClientBuildIdentity_20260913. Final APK packaging and physical-device display verification require the user's next normal Unity build and install.
