# Contributing

Everything in this repo is open for help: mods, docs, libraries, templates, build files.

But I cannot promise quick replies.

## Talk first

Anything is accepted: Discord, an issue, a finished pull request; but talking with me first is strongly recommended, especially before bigger work.

Read [PHILOSOPHY.md](PHILOSOPHY.md) first; it is short and it is what every review is measured against.

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
- [ ] follows [PHILOSOPHY.md](PHILOSOPHY.md) and the [MODDING.md](MODDING.md) code style
- [ ] version follows [semver](https://semver.org/); a released `<Guid>` never changes

Accepted mods get their own release line (MODDING.md, section 5). Releases are automated from `dist/` on push to `main` (see `.github/workflows/release.yml`); manual tagging is only needed for snapshots, templates and source libraries.

## Everything else

- **Docs and TODO** - PR whenever.
- **Libraries, templates, build files** - they affect every mod, so discuss first (Discord or issue).
- **Philosophy** - open for discussion too, but it changes rarely and only with a reason.

## Bugs and ideas

Open an issue. Check [TODO.md](TODO.md) first - the known gaps are already listed.

## License

Contributions come under this repo's [Apache-2.0](../LICENSE) license.
