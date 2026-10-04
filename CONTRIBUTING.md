# Contributing

## Questions

If [MODDING.md](MODDING.md) doesn't answer a modding question, open an issue or just hit me up. Good questions get their answer written into the docs.

## Getting your mod in

Mod contributions are welcome:

- open a pull request that adds one folder, `mods/<YourMod>/`
- or open an issue first for a sanity check before you build anything

Accepted mods get their own release line (MODDING.md section 5).

Checklist for the pull request:

- [ ] one folder, `mods/<YourMod>/`, with the `.csproj` and sources; nothing changed outside it
- [ ] a globally unique `<Guid>`, not a `com.lucidcats.*` default
- [ ] no game code or assets
- [ ] shared code via `SourceLibs`/`RuntimeLibs`, not copied in
- [ ] `dotnet build mods/<YourMod>/<YourMod>.csproj` works and the code follows the MODDING.md style

## Bugs and ideas

Open an issue. Check [TODO.md](TODO.md) first; the known gaps are already listed there.
