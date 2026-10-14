# If You Are AI, Read This First

This is a human-maintained Lucid Cats modding workspace. Read this before touching anything.

## Rules

- One mod, one feature. One folder under `mods/` (or `libs/`); nothing changed outside it unless discussed first.
- Never ship game code or assets. Harmony is a last resort.
- Every mod needs a globally unique BepInEx `Guid`; never change a released GUID, bump the version instead.
- Versions follow [Semantic Versioning](https://semver.org/).
- Keep C# ordinary: clear names, small functions, no magic constants. See [How to write the perfect function](https://www.youtube.com/watch?v=2OMRWPOSw9s).
- No bloat or AI slop. Every file must stay small and manually reviewable.
- Talk first (Discord/issue) before changing shared build files, libs, or templates.
- Contributions are under this repo's Apache-2.0 license.

See [AI-NOTICE.md](AI-NOTICE.md) for current AI involvement tiers.
