## Summary

## Checklist

- [ ] builds: `dotnet build mods/<ModName>/<ModName>.csproj` works
- [ ] tested in a running game
- [ ] a globally unique `<Guid>`, not a `com.lucidcats.*` default
- [ ] no game code or assets
- [ ] shared code via `SourceLibs`/`RuntimeLibs`, not copied in
- [ ] follows [PHILOSOPHY.md](../docs/PHILOSOPHY.md) and the [MODDING.md](../docs/MODDING.md) code style
- [ ] version follows [semver](https://semver.org/); a released `<Guid>` never changes
