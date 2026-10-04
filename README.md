# Lucid Cats Mods & Modding Unified

> AI-notice: this Repo's README and some parts were made with some help of generic-ai.

A workspace and tutorial for building BepInEx mods for Lucid Cats. Mods are plain C# files built with the .NET SDK: no Unity editor, no IDE. Lucid Cats is a plain Mono build (Unity 6000.3.11f1), so its game code decompiles to fully readable C#.

```
GameMods/
├── mods/           one folder per mod
├── libs/
│   ├── source/     helpers compiled into requesting mods
│   └── runtime/    shared BepInEx dependencies built once
├── templates/      dotnet new stubs for mods, runtimes, patchers and C++
└── dist/
    ├── mods/       built mod folders and ZIPs
    └── runtime/    built runtime folders and ZIPs
```

## Docs

| File | What it covers |
|---|---|
| [MODDING.md](MODDING.md) | Setup, first mod, reading the game's code, libraries, releases, patchers and C++, troubleshooting, code style and conventions. |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Publishing your mod in this repo, issues, questions. |
| [TODO.md](TODO.md) | Unfinished parts of the repo. |

## Playing with mods (players)

1. In Steam, right-click Lucid Cats → Manage → Browse local files. That folder is the game dir.
2. Download `BepInEx_win_x64_5.4.23.5.zip` (x64, Unity Mono, not IL2CPP, not BepInEx 6) from <https://github.com/BepInEx/BepInEx/releases> and extract it into the game dir, next to `LucidCats.exe`.
3. Launch the game once and close it, so BepInEx creates its folders.
4. Download a mod `.zip` (or the bundle with every mod) from the [Releases](../../releases) page and extract it into the game dir. Merge folders when asked.

<details><summary>What is BepInEx, exactly?</summary>

A mod loader. Doorstop (two tiny files) starts before the game and injects BepInEx into Unity's .NET runtime. BepInEx then scans `BepInEx/plugins` for .NET assemblies and starts every plugin class it finds. Nothing is patched on disk: deleting the BepInEx folder restores the vanilla game.

</details>

## Philosophy

- Grown like the Nix dendritic pattern: every mod folder is a self-contained module the workspace discovers automatically; adding one never edits anything central.
- Easy to use: a mod is two plain C# files, no Unity editor, no IDE; one command builds, deploys and zips it.
- Cross-platform: any OS with the .NET SDK builds Windows mods, C++ cross-compiles via mingw, and `flake.nix` pins the whole toolchain.
- A mod's `.csproj` holds only what differs from the defaults; the root build files do the repetitive work (metadata, references, validation, deploy, ZIP) exactly once.
- One mod, one feature; shared code becomes a library only when that makes real consumers simpler.
- Harmony is a last resort: prefer the game's own events, components, data and config binding.
- Ship nothing the game already provides, and never game code or assets.
