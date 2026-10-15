# Philosophy

ULCaMoS (Unified Lucid Cats Modding Structure) philosophy, consolidated.

## Core ideas

- **One folder per mod.** Adding a mod touches nothing central; the build discovers it automatically.
- **No boilerplate.** A `.csproj` declares only what differs; metadata, references, validation, deploy, and ZIP packaging are generated once at the root.
- **Plain C#.** No Unity editor, no IDE required. Any OS with the .NET 8 SDK works.
- **Unified output.** Same structure, same commands, same output for every mod.

## Mod rules

- **One mod, one feature.** Reused code becomes a library only when sharing is genuinely simpler than keeping it local.
- **Harmony is a last resort.** Use the game's own events, components, data, and config binding first. When a patch is needed, prefer `Postfix`, then `Prefix`, then `Transpiler`, and patch the smallest possible part.
- **No game code or assets in a release.** Reading and learning from decompiled game code is free; shipping it is never OK.
- **No magic constants or paths.** Give authored values meaningful names; derive measurements from runtime relationships; take reference paths from the shared build properties (`LucidCatsDir`).
- **Keep C# ordinary.** `var` when the type is obvious, pattern matching when it helps, `string.Empty` over `""`, narrowest useful access, tabs for indentation, logs only for errors or significant lifecycle events.
- **Small functions.** See [How to write the perfect function](https://www.youtube.com/watch?v=2OMRWPOSw9s).
- **Talk first** before changing shared build files, libraries, or templates (Discord or issue).
- **Versions follow [Semantic Versioning](https://semver.org/).** Never change a released BepInEx GUID; bump the version instead.

## Releases

Seven release lines, each versioning independently: workspace snapshot, bare template, template + libraries, bundle (`all-mods`), one mod, one runtime, one source library. Mods and runtimes are built locally (they need the game DLLs); their zips are committed under `dist/`. Bump `Version` in the `.csproj` to ship again.

## AI involvement

AI was used in parts of this repository, but every file was manually reviewed. See `AI-NOTICE.md` for the tier breakdown.
