# Unified Lucid Cats Modding Structure (ULCaMoS)

A collection of Lucid Cats mods, tools, and documentation built around a unified approach to its mod development.

> **Status:** Official modding news from devs and body-less references for methods whose implementations are unavailable are still pending. GitHub Actions checks workspace consistency, but automated mod compilation checks for pull requests are not yet configured.

> **AI-NOTICE** If you are curious please look into this file [AI notice](docs/AI-NOTICE.md) how much AI was used at making this, not updated consistenlty (can be behind commits).

This repository aims to provide a central place for Lucid Cats mods, a consistent architecture for developing them, and documentation for anyone who wants to get started.

## Playing with mods

Want to play with mods without getting into the development side? Start here.

### Installation

1. In Steam, right-click **Lucid Cats** → **Manage** → **Browse local files**. This opens the game directory.
2. Download [BepInEx 5.4.23.5](https://github.com/BepInEx/BepInEx/releases), selecting the Windows x64 **Unity Mono** build, not IL2CPP or BepInEx 6. These instructions assume the game uses the 64-bit Unity Mono runtime.
3. Extract the archive into the game directory, alongside `LucidCats.exe`.
4. Launch the game once, then close it. BepInEx should create its required folders.
5. Download a mod from the [releases page](https://github.com/MaxMen11/LucidCats-Mods/releases) or another source listed under [Where to get mods](#where-to-get-mods).
   - **A bare `.dll`:** Put it in its own subfolder under `BepInEx/plugins/`, for example `BepInEx/plugins/TipJar/TipJar.dll`.
   - **A `.zip`:** Extract it into the game directory, preserving the archive's folder structure. Merge folders when prompted.
   - Follow any additional installation instructions supplied by the mod author.
6. Launch the game. If a mod fails to load, check `BepInEx/LogOutput.log` in the game directory.

Only install mod files from sources you trust, as DLLs contain executable code.

<details>
<summary>Plugins, patchers, runtimes, and configuration</summary>

- **Plugin:** A mod loaded by BepInEx, normally installed under `BepInEx/plugins/`.
- **Preloader patcher:** Code installed under `BepInEx/patchers/` that can modify assemblies in memory before normal plugins load. These are less common.
- **Runtime library:** A shared DLL that other mods depend on, such as MapKit. Follow the relevant installation instructions to ensure dependent mods can find it.
- **Configuration:** BepInEx configuration files are generally created under `BepInEx/config/` when the relevant plugin initializes. You can edit supported settings there or use the [Lucid Cats Mod Manager](https://github.com/SusoTF/LucidCatsModManager).

A ZIP containing a `BepInEx/` directory will usually put its contents in the appropriate locations when extracted into the game directory. Check the archive structure before extracting it.

</details>

<details>
<summary>What is BepInEx?</summary>

BepInEx is a mod loader for Unity and other supported .NET-based games. Its loader components start before the game, initialize the framework, and discover compatible plugins.

BepInEx plugins can extend or modify game behavior. Some mods also use preloader patchers to modify assemblies in memory during startup.

A normal BepInEx installation does not require modifying the original game assemblies on disk. However, uninstalling BepInEx completely may require removing additional loader files from the game directory or restoring the original files through Steam.

</details>

## Where to get mods

- **[Nexus Mods](https://www.nexusmods.com/lucidcats/mods):** Browse and download Lucid Cats mods on Nexus Mods.
- **[GitHub Releases](https://github.com/MaxMen11/LucidCats-Mods/releases):** Download packaged mods and available releases from this repository.
- **[Browse mods on GitHub](https://github.com/search?q=lucid+cats&type=repositories):** Explore the repository to discover individual mods, source code, and project files.
- **[Lucid Cats Mod Manager](https://github.com/SusoTF/LucidCatsModManager):** For managing mods in-game.

Check each mod's documentation and release notes for installation instructions, dependencies, compatibility, and known issues.

Always check the release notes for requirements and known issues before installing a mod.

## Making mods with ULCaMoS

Start with the [modding guide](docs/MODDING.md).

- **Getting started:** [C# basics](docs/CSharp-BASICS.md) · [Editor setup](docs/EDITORS.md)
- **Project:** [Philosophy](docs/PHILOSOPHY.md) · [TODO](docs/TODO.md)
- **Contributing and help:** [Contributing](docs/CONTRIBUTING.md) · [Support](docs/SUPPORT.md) · [Security](docs/SECURITY.md)

## License

See [LICENSE](LICENSE).
