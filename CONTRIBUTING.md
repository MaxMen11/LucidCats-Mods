# Contributing

## Questions

If [MODDING.md](MODDING.md) does not answer a modding question, open an issue or just hit me up. Questions worth answering get their answer added to the docs.

## Publishing your mod here

Mod contributions are welcome, two ways in:

- open a pull request that adds one folder, `mods/<YourMod>/`
- open an issue first if you want a sanity check before building anything

Accepted mods get their own release line (MODDING.md section 5).

Before the pull request, confirm:

- [ ] One folder: `mods/<YourMod>/` with the `.csproj` and sources; nothing changed outside it
- [ ] Globally unique `<Guid>`, not a `com.lucidcats.*` default
- [ ] No game code or assets included
- [ ] Shared code pulled in via `SourceLibs`/`RuntimeLibs`, not copied
- [ ] `dotnet build mods/<YourMod>/<YourMod>.csproj` succeeds and the result follows the MODDING.md code style

## Bugs and ideas

Open an issue. Check [TODO.md](TODO.md) first: it lists what is already known to be unfinished.
