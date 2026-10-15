AI was involved in parts of this repository, but every file was manually reviewed by me. My goal is to keep the project as human-made as possible, using AI only where it provides meaningful assistance rather than relying on it to produce the project wholesale, I am **not** trying to make bloat or AI-slop.

The current level of AI involvement is (VERY OLD, THIS TABLE NEEDS TO BE REDONE):

| Tier | Parts |
| - | - |
| **Fully AI made** | `.github/` |
| **Heavily AI assisted** | `libs/runtime/MapKit/`, `libs/source/MenuKit/`, `mods/QualityPlus/`, `templates/` |
| **Mildly AI assisted** | `docs/MODDING.md`, `mods/TipJar/`, `mods/FullParty/`, `mods/ImmersiveSpectating/`, `Directory.Build.props`, `Directory.Build.targets`, `build.proj`, `global.json`, `.editorconfig`, `.config/dotnet-tools.json`, `.gitignore` |
| **AI checks and fixes** | `README.md`, `flake.nix` |
| **Fully human made** | `docs/TODO.md`, `docs/CONTRIBUTING.md`, `docs/AI-NOTICE.md`, `mods/Conflation/`, `LICENSE` |

I intend to continue reducing AI involvement over time, with the long-term goal of having as much of the repository as possible written and maintained by humans.

## Rules for AI assistants

If you are an AI working in this repo, read this first:

- One mod, one feature. One folder under `mods/` (or `libs/`); nothing changed outside it unless discussed first.
- Never ship game code or assets. Harmony is a last resort.
- Every mod needs a globally unique BepInEx `Guid`; never change a released GUID, bump the version instead.
- Versions follow [Semantic Versioning](https://semver.org/).
- Keep C# ordinary: clear names, small functions, no magic constants. See [How to write the perfect function](https://www.youtube.com/watch?v=2OMRWPOSw9s).
- No bloat or AI slop. Every file must stay small and manually reviewable.
- Talk first (Discord/issue) before changing shared build files, libs, or templates.
- Contributions are under this repo's Apache-2.0 license.
