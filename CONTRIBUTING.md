# Contributing

Everything in this repo is open for help: mods, docs, libraries, templates, build files.

But I cannot promise quick replies.

## Talk first

Anything is accepted: Discord, an issue, a finished pull request; but talking with me first is strongly recommended, especially before bigger work.

- Discord: `maxmen11`
- Matrix: coming later
- Email: maybe coming later
- or open an issue

## Getting a mod in

One folder, `mods/<YourMod>/`, with the `.csproj` and sources; nothing changed outside it. The pull request:

- [ ] builds: `dotnet build mods/<YourMod>/<YourMod>.csproj` works
- [ ] tested in a running game
- [ ] a globally unique `<Guid>`, not a `com.lucidcats.*` default
- [ ] no game code or assets
- [ ] shared code via `SourceLibs`/`RuntimeLibs`, not copied in
- [ ] follows the [MODDING.md](MODDING.md) style

Accepted mods get their own release line (MODDING.md, section 5). Auto-releases don't work yet, so releases are manual for now.

## Everything else

- **Docs and TODO** - PR whenever.
- **Libraries, templates, build files** - they affect every mod, so discuss first (Discord or issue).

## Bugs and ideas

Open an issue. Check [TODO.md](TODO.md) first - the known gaps are already listed.

## License

Contributions come under this repo's [Apache-2.0](LICENSE) license.
