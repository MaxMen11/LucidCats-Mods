# Unified Lucid Cats Modding Structure and Mods

This repo's goal: all the information you need about Lucid Cats modding and mods, a clean architecture for the mods themselves, and a clean setup for anyone who wants to get started.

> AI notice: If you are curious, please read [AI-NOTICE.md](AI-NOTICE.md).

- **Playing with mods** - install and play. Six steps, right below.
- **Making mods** - build your own in plain C#: no Unity editor, no IDE. [MODDING.md](MODDING.md) covers setup to release.


## Playing with mods

1. In Steam, right-click Lucid Cats → Manage → Browse local files. That folder is the game dir.
2. Download `BepInEx_win_x64_5.4.23.5.zip` from the [BepInEx releases](https://github.com/BepInEx/BepInEx/releases) - the x64 **Unity Mono** build, not IL2CPP, not BepInEx 6.
3. Extract the zip into the game dir, next to `LucidCats.exe`.
4. Launch the game once and close it, so BepInEx creates its folders.
5. Download a mod - see [Where to get mods](#where-to-get-mods). You get either a bare `.dll` or a `.zip`:
    - A bare `.dll`: Put it in its own subfolder in `BepInEx/plugins/`, e.g. `BepInEx/plugins/TipJar/TipJar.dll`.
    - A `.zip`: Extract it into the game dir. Merge folders when asked.
    - Both end up as `BepInEx/plugins/<ModName>/<ModName>.dll` (or in `BepInEx/patchers/...`).
6. Launch the game. If a mod doesn't load, look for its name in `<game dir>/BepInEx/LogOutput.log`.

<details><summary>Plugins, patchers, runtimes</summary>

- A **plugin** lives in `BepInEx/plugins/` and runs inside the game.
- A **patcher** lives in `BepInEx/patchers/` and rewrites game assemblies in memory once at startup, before plugins load. Rare.
- A **runtime** is a shared DLL other mods depend on (MapKit so far); the same install as a plugin.
- Settings appear in `BepInEx/config/<mod>.cfg` after the first run; edit the file or use the [Mod Manager](https://github.com/SusoTF/LucidCatsModManager).
A `.zip` with a `BepInEx/` folder inside puts each kind in its right place when you merge folders.
</details>

<details><summary>What is BepInEx, exactly?</summary>

A mod loader. Doorstop (two tiny files) starts before the game and injects BepInEx into Unity's .NET runtime. BepInEx then scans `BepInEx/plugins` for .NET assemblies and starts every plugin class it finds. Nothing is patched on disk: deleting the BepInEx folder restores the vanilla game.
</details>


### Where to get mods

- **This repo** - [Releases](../../releases) has every mod as a `.zip`; sources in [mods/](mods/).
- **Nexus Mods** - the game's mod hub: [nexusmods.com/lucidcats](https://www.nexusmods.com/lucidcats).
- **Lucid Cats Discord** - the official Kolide Studio server. Invite on the [Steam page](https://steamcommunity.com/app/4778540) or [kolidestudio.com](https://kolidestudio.com/games/lucid-cats/).
- **Other GitHub repos** - some mods are developed separately, e.g. the [Bestiary](https://github.com/SusoTF/LucidCatsBestiary).
- **Tools** - the [Lucid Cats Mod Manager](https://github.com/SusoTF/LucidCatsModManager) by SusoTF lists installed mods and their settings.

```
LucidCats-Mods/
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
| [MODDING.md](MODDING.md) | How to mod with this structure, and anything you could need to know about modding, plus code style and conventions. |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Getting your mod into this repo, issues, questions or any help. |
| [TODO.md](TODO.md) | Unfinished parts of this repo, open for help. |


## Philosophy

- Structured after Nix's dendritic pattern: one self-contained folder per mod, discovered by the build — adding a mod touches nothing central.
- Skip boilerplate: a `.csproj` declares only what differs; metadata, references, validation, deploy and ZIP are generated once at the root.
- Unify the mods: same structure, same commands, same output. Plain C#, no Unity editor, no IDE; any OS with the .NET SDK, mingw for the C++, `flake.nix` for Nix specifically.
- One mod, one feature. Shared code becomes a library only with real consumers.
- Harmony is a last resort — the game's own events, components and data come first. Ship nothing the game already provides, never game code or assets.
