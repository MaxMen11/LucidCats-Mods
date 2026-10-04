# Modding Lucid Cats

How modding works in this workspace, from setup to release. Installing mods as a player is in the [README](README.md); getting your own mod into the repo is in [CONTRIBUTING.md](CONTRIBUTING.md).

## Contents

1. Modding setup (once per machine)
2. First mod
3. Reading the game's code
4. Libraries
5. Releases
6. Going deeper: patchers, C++
7. Troubleshooting

Code style and conventions are at the end.

## 1. Modding setup (once per machine, ~10 minutes)

Install the .NET 8 SDK with the system package manager:

| Manager | Command |
|---|---|
| winget | `winget install Microsoft.DotNet.SDK.8` |
| scoop | `scoop install dotnet-sdk` |
| choco | `choco install dotnet-sdk` |
| brew | `brew install --cask dotnet-sdk` |
| apt | `sudo apt install dotnet-sdk-8.0` |
| dnf | `sudo dnf install dotnet-sdk-8.0` |
| pacman | `sudo pacman -S dotnet-sdk` |
| zypper | `sudo zypper install dotnet-sdk-8.0` |
| apk | `sudo apk add dotnet8-sdk` |
| snap | `sudo snap install dotnet-sdk --classic --channel 8.0/stable` |
| nix | `nix develop` (uses the included `flake.nix`) |
| any OS | `curl -sSL https://dot.net/v1/dotnet-install.sh \| bash -s -- --channel 8.0` |

Get this repo: `git clone`, or Code → Download ZIP → extract.

Open a terminal in the repo folder. On Windows, open the folder in Explorer, click the address bar, type `cmd`, press Enter. On Linux or macOS, `cd` into the folder.

Point the build at the game dir (the folder with `LucidCats.exe`), one of:

- per command: add `-p:LucidCatsDir="C:\Games\Lucid Cats"` with the real path
- permanent, Windows: `setx LUCIDCATS_DIR "C:\Games\Lucid Cats"`, then reopen the terminal
- permanent, Linux/macOS: `export LUCIDCATS_DIR="/path/to/Lucid Cats"` in the shell profile
- or write it into `Directory.Build.props`

Sanity check:

```
dotnet build build.proj
```

Expected output: `Build succeeded`. With no mods yet, this only proves the toolchain works.

<details><summary>How the build finds everything</summary>

`Directory.Build.props` gives every project the same compiler settings, metadata defaults, game references and output paths. `Directory.Build.targets` resolves libraries, generates BepInEx metadata, validates, packages and deploys. `build.proj` finds every mod automatically and builds its runtime dependencies in order.

</details>

## 2. First mod

```
dotnet new install ./templates
dotnet new lcmod -n HelloCats -o mods/HelloCats
```

Two files:

| File | What it is |
|---|---|
| `mods/HelloCats/HelloCats.csproj` | Only metadata and libraries that differ from defaults. |
| `mods/HelloCats/Plugin.cs` | The mod. It logs "HelloCats v1.0.0 loaded." at startup. |

### The project file

The `.csproj` describes the mod to the build. A normal one stays small:

```xml
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<Guid>io.github.author.lucidcats.hellocats</Guid>
		<Name>Hello Cats</Name>
		<Version>1.0.0</Version>
		<Description>What the mod does.</Description>
		<Company>Your name or team</Company>
		<SourceLibs>MenuKit</SourceLibs>
		<RuntimeLibs>MapKit</RuntimeLibs>
	</PropertyGroup>
</Project>
```

Every field is optional, but replace the development GUID before a public release:

| Property | Default / effect |
|---|---|
| `Guid` | `com.lucidcats.<project>`; permanent BepInEx identity, so set a globally unique value before release |
| `Name` | project filename; displayed by BepInEx and the Mod Manager |
| `Version` | `1.0.0`; used by BepInEx, dependencies and DLL metadata |
| `Description` | empty; displayed by the Mod Manager |
| `Company` | project name; author or team displayed by the Mod Manager |
| `SourceLibs` | none; source helpers compiled into this DLL |
| `RuntimeLibs` | none; shared DLLs built first and declared as BepInEx dependencies |

The project filename also defaults the DLL name and namespace. Rare standard properties such as `AssemblyName`, `RootNamespace`, `FileVersion` and `InformationalVersion` can still override those defaults when genuinely needed.

The root build files supply the framework, compiler settings, game references, output paths, validation, deployment and ZIP packaging. They also generate `ModMetadata.Guid`, `Name` and `Version`, so these values are not repeated in `Plugin.cs`.

Build (drop the flag if `LUCIDCATS_DIR` is set):

```
dotnet build mods/HelloCats/HelloCats.csproj
```

Expected: `Build succeeded`, the mod copied into `<game dir>/BepInEx/plugins/HelloCats/`, and a shareable `dist/mods/HelloCats.zip` written. Launch the game and check the BepInEx console (or `<game dir>/BepInEx/LogOutput.log`) for the log line.

That is the whole loop: edit `Plugin.cs` → build → launch. A `private void Update()` method runs every frame. Calling classes from the game's own code (next section) is where modding starts.

### Showing up in the Mod Manager

SusoTF's [Lucid Cats Mod Manager](https://github.com/SusoTF/LucidCatsModManager) lists every installed BepInEx 5 mod with its details, so the `.csproj` fields are worth filling in:

| The manager shows | Where it comes from |
|---|---|
| Name, version | `<Name>` and `<Version>` in the `.csproj`, generated into `[BepInPlugin]` |
| Author | `<Company>` in the `.csproj` |
| Description | `<Description>` in the `.csproj` |

The manager also renders each mod's settings with the game's own controls: `bool` settings become toggles, `float` settings with a range become sliders, enum settings and `AcceptableValueList` become dropdowns. The optional hints of the Configuration Manager mod (`DispName`, `Order`, `Browsable`, `ReadOnly`) are supported.

<details><summary>What the skeleton means</summary>

The project filename supplies the default name, namespace and DLL name; the version defaults to `1.0.0`. Set `Guid` to a globally unique permanent value before release. The build generates `ModMetadata`, so name, GUID and version are never repeated in C#. `BaseUnityPlugin` is BepInEx's plugin base; `Awake`, `Update` and `OnGUI` are Unity messages.

</details>

## 3. Reading the game's code

```
dotnet tool restore
dotnet ilspycmd -p -o decompiled "<game dir>/LucidCats_Data/Managed/Assembly-CSharp.dll"
```

`decompiled/` then holds the whole game as readable C#. Good starting points: `MainMenuController.cs`, `LevelConfig.cs`, the `Game.UI/` folder. The same command works on the `HaniUtils.*` framework modules. Reading and learning is free; shipping game code or assets inside a mod is never OK. `decompiled/` is gitignored.

For one visual introduction to the full Unity Mono workflow, watch [Lethal Company — How to write your own mod from scratch!](https://www.youtube.com/watch?v=4Q7Zp5K2ywI). It demonstrates inspecting managed game code, choosing a method, applying a Harmony patch, building, and testing a BepInEx plugin. Use it for the investigation and patching concepts; use this repository's `lcmod` template and build commands instead of the video's manual project setup.

<details><summary>Why is it this readable?</summary>

Mono Unity games ship their C# as .NET assemblies; ilspycmd turns them back into nearly source identical C#, and a mod references those same DLLs to compile against the real game types. IL2CPP games compile to native code instead: that is the world of dumpers and C++, and not this game.

</details>

## 4. Libraries

Libraries are selected by name in a mod's `.csproj`:

```xml
<PropertyGroup>
	<SourceLibs>MenuKit</SourceLibs>
	<RuntimeLibs>MapKit</RuntimeLibs>
</PropertyGroup>
```

| Kind | Location | Behavior |
|---|---|---|
| Source library | `libs/source/<Name>/` | Its C# is compiled into the mod; no extra player download. |
| Runtime library | `libs/runtime/<Name>/` | Builds one shared BepInEx DLL; the build adds the project reference and runtime dependency. |

Names are separated with `;`. Unknown and duplicate names fail with a direct error. Each library has its own short README with its API and examples.

MapKit is a runtime because all map mods must share one registry and network handler. MenuKit is source because its helpers are local to each mod. Runtime libraries produce their own ZIP under `dist/runtime/` and are installed once by players.

## 5. Releases

Everything ships from this one repo as seven release lines. Each is a tag pattern, and each versions itself independently:

| Line | Tag | What's in the release | Who it's for |
|---|---|---|---|
| Workspace snapshot | `v1.2.0` | source of the whole repo at that point (auto-generated) | pinning a known-good state |
| Bare template | `template-v1.0.0` | the folder skeleton + `dotnet new` templates, no libraries | starting a fresh setup |
| Template + libraries | `template-full-v1.0.0` | skeleton + MapKit + MenuKit | same, libraries included |
| Bundle | `all-mods-v1.0.0` | one zip, every mod and required runtime merged | players wanting everything |
| One mod | `MyMod-v1.0.0` | `BepInEx/plugins/MyMod/MyMod.dll` (+ native DLL when used) | players picking mods |
| One runtime | `MapKit-v1.0.0` | `dist/runtime/MapKit.zip` | players and dependent mods |
| One source library | `MenuKit-v1.2.0` | its source folder under `libs/source/` | modders adding a source library |

Prefixed releases never take the workspace snapshot's Latest badge. The Releases search accepts filters such as `tag:MapKit` and `tag:template`. The names `template`, `template-full`, and `all-mods` are reserved.

Snapshot, template, and source-library tags publish automatically. Mods, runtime libraries, and bundles are released locally because compiling them requires the game DLLs.

To publish a mod of your own in this repo, see [CONTRIBUTING.md](CONTRIBUTING.md).

## 6. Going deeper

**Patcher**: `dotnet new lcpatcher -n MyPatcher -o mods/MyPatcher`. A patcher modifies game DLLs at load time with Mono.Cecil; `TargetDLLs` lists the assemblies to patch. Output lands in `BepInEx/patchers`; zip and deploy paths follow automatically.

**C++**: `dotnet new lcnative -n MyMod -o mods/MyMod`. The mod's code goes in `native/mod.cpp`; CMake builds it into `MyModNative.dll`, and a generated shim (never edited) loads it and forwards `Update` plus a `Log` callback. Needed tools: `cmake`, `make` and a Windows x64 compiler. Visual Studio (MSVC) on Windows, `sudo apt install cmake make mingw-w64` on Debian/Ubuntu, `brew install cmake mingw-w64` on macOS, or `nix develop .#native`. C++ reaches below the managed world (engine hooks, raw memory, native libraries, GC free loops); game logic stays easier in C#, and a native crash takes the whole game down.

**Every dotnet command:**

| What | Command |
|---|---|
| Build one mod | `dotnet build mods/<ModName>/<ModName>.csproj` |
| Build every mod | `dotnet build build.proj` |
| Clean everything | `dotnet clean build.proj` |
| Install templates (once per machine) | `dotnet new install ./templates` |
| Scaffold a plugin | `dotnet new lcmod -n MyMod -o mods/MyMod` |
| Scaffold a runtime | `dotnet new lcruntime -n MyRuntime -o libs/runtime/MyRuntime` |
| Scaffold a patcher | `dotnet new lcpatcher -n MyPatcher -o mods/MyPatcher` |
| Scaffold a C++ mod | `dotnet new lcnative -n MyMod -o mods/MyMod` |
| Install tools (once per machine) | `dotnet tool restore` |
| Decompile the game | `dotnet ilspycmd -p -o decompiled "<game dir>/LucidCats_Data/Managed/Assembly-CSharp.dll"` |

## 7. Troubleshooting

| Symptom | Fix |
|---|---|
| `Point LucidCatsDir at your Lucid Cats game folder` | The build cannot find the game; check the `-p:` flag, `LUCIDCATS_DIR`, or `Directory.Build.props` (section 1). |
| `A compatible .NET SDK was not found` or `Requested SDK version: 8.0.100` | Install the .NET 8 SDK or a newer SDK permitted by `global.json`. Run `dotnet --list-sdks` to confirm that an SDK, rather than only a runtime, is installed. |
| Mod builds but never loads in game | BepInEx not installed, or wrong build: `BepInEx_win_x64_5.4.23.5` (Mono x64) is the right one. Check `BepInEx/LogOutput.log`. |
| Windows blocks the downloaded files (dll "access denied" / refuses to load) | Windows marks files from the internet. In PowerShell, `cd` into the game dir and run: `Get-ChildItem -Recurse \| Unblock-File` |
| `CS0246: MapKit` or `MenuKit` not found | Add MapKit to `RuntimeLibs` or MenuKit to `SourceLibs` in that mod's `.csproj`. |
| Build errors mention `net46` / MSB3644 | Keep `netstandard2.1` (the templates already do). |
| C++ mod build fails, or its zip is missing the native dll | `cmake`, `make`, or the mingw compiler not installed: see section 6. |
| Plugin GUID collisions | Change one mod's `Guid` before releasing. |

## Code style

Harmony is a last resort, not a first tool: use the game's events, components, data and config binding first, because every patch is a hook into game internals that an update can break. When a patch is necessary, prefer `Postfix`, then `Prefix`, then `Transpiler`, and patch the smallest possible part. MenuKit uses no Harmony; MapKit keeps its few patches at the lifecycle boundaries where before-or-after timing matters.

No magic constants and no magic paths: give authored values meaningful names, derive measurements and positions from runtime relationships when possible, and take reference paths from the shared build properties (`LucidCatsDir`) rather than a hardcoded disk location.

Modularity means one mod does one feature, methods represent meaningful behavior, and shared code becomes a kit only when the abstraction makes real consumers simpler. Prefer direct code, clear names and comments only where the reason is not self-evident. For a great 47 minute talk on exactly this way of writing functions, watch [How to write the perfect function](https://www.youtube.com/watch?v=2OMRWPOSw9s).

Keep ordinary C# ordinary: use `var` when the type is obvious, pattern matching when it improves clarity, `string.Empty` instead of `""`, the narrowest useful access, tabs for indentation, and logs only for errors or significant lifecycle events.

## Conventions

A mod's folder, project, DLL and namespace share one short feature name (`TipJar`, not `LucidCatsTipJar`); its display name may use spaces. The `lc` prefix is reserved for template commands where it prevents collisions. Never change a released BepInEx GUID; bump the single version in the `.csproj` instead, and keep Harmony IDs unique and concise.

One mod, one feature. Reused code belongs under `libs/source/` or `libs/runtime/` only when sharing it is genuinely simpler than keeping it local. References are `Private="false"`, so a mod's ZIP does not ship DLLs the game already provides. Never include game code or assets in a release.
