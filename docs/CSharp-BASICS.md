# C# basics for Lucid Cats modding

This is completely the basics of C# for making a mod here. I recommend watching the videos or reading the sources at the bottom alongside it.

## The twelve lines a mod is

This is the whole `Plugin.cs` that `dotnet new lcmod` gives you:

```csharp
using BepInEx;

namespace ModName
{
	[BepInPlugin(ModMetadata.Guid, ModMetadata.Name, ModMetadata.Version)]
	public sealed partial class Plugin : BaseUnityPlugin
	{
		private void Awake()
		{
			Logger.LogInfo($"{ModMetadata.Name} v{ModMetadata.Version} loaded.");
		}
	}
}
```

| Line | What it means |
|---|---|
| `using BepInEx;` | "I will use names from this library without spelling out the full path." |
| `namespace ModName` | A folder for names, so your `Plugin` never collides with another mod's `Plugin`. The build defaults it to the project name. |
| `[BepInPlugin(...)]` | An attribute: a label BepInEx reads to register the mod. GUID, name and version come from the `.csproj` via generated `ModMetadata` constants, so nothing is written twice. |
| `public sealed class Plugin : BaseUnityPlugin` | A class (a blueprint for objects) named `Plugin`. `: BaseUnityPlugin` means it *inherits* every BepInEx plugin ability; `sealed` means nothing may inherit *from* it; `partial` means the build may append generated parts to it. |
| `private void Awake()` | A method only this class can call (`private`), returning nothing (`void`). `Awake` is a *Unity message*: Unity calls it automatically when the plugin loads. |
| `Logger.LogInfo($"...");` | BepInEx's built-in logger. The message appears in the console and in `BepInEx/LogOutput.log`. |

From here, everything you add is one of the five patterns below.

## Pattern 1: variables and types

A variable is a named box with a type. The types mods use constantly:

```csharp
string author = "Max";        // text
int capacity = 100;           // whole numbers
float fee = 0.05f;            // decimals; the f marks float
bool enabled = true;          // true / false
```

For collections: `List<TipEntry> entries = new();` grows like an array you can `.Add()` to. `var` lets the compiler figure the type out when it is obvious from the right side.

## Pattern 2: methods and if/else

```csharp
private int EntryFee(int amount)
{
	if (amount <= 0)
	{
		return 0;
	}
	return amount / 20;
}
```

`return` hands a value back and exits. `if`/`else` pick a branch. Naming: methods and classes in this repo use `PascalCase`, variables use `camelCase`. Exception: `private static readonly` often stays PascalCase (`GlassColor` in TipJar's interactable).

## Pattern 3: foreach over a collection

```csharp
foreach (var preset in presets)
{
	Logger.LogInfo($"preset {preset.Name}");
}
```

Runs the block once per item. Real example in the repo: `mods/QualityPlus/Presets.cs`.

## Pattern 4: Unity messages

Methods Unity calls for you at fixed moments; write the body, never call them yourself:

| Message | Runs |
|---|---|
| `Awake()` | once, when the plugin loads (setup goes here) |
| `Start()` | once, before the first frame |
| `Update()` | every frame; keep it cheap |
| `OnGUI()` | each GUI frame, sometimes several times per frame (layout, repaint, input); keep it cheap too |

## Pattern 5: configuration with BepInEx

One line per setting; BepInEx creates and reloads `BepInEx/config/<your guid>.cfg`:

```csharp
private void Awake()
{
	var capacity = Config.Bind("General", "Capacity", 100, "How many credits fit in the jar.");
	var fee = Config.Bind("General", "DepositFee", 5,
		new ConfigDescription("Percent per deposit", new AcceptableValueRange<int>(0, 50)));
	Logger.LogInfo($"capacity {capacity.Value}, fee {fee.Value}");
}
```

Typed, clamped, documented, and the in-game Mod Manager renders the same entries as sliders and toggles. Real usage in this repo: `mods/TipJar/Plugin.cs`.

## Reading unfamiliar syntax

| Looks scary | Just means |
|---|---|
| `$"v{version}"` | String interpolation: evaluate `{...}` and splice into the text. |
| `partial` | The class continues in another (generated) file. |
| `sealed` | No class may inherit this one. |
| `var x = GetThing();` | Same type as the call returns; do not guess. |
| `static` | Belongs to the class itself, one shared copy, no instance needed. |

## When the build complains

`dotnet build` errors read like `mods/Foo/Plugin.cs(12,18): error CS0246: The type or namespace name 'X' could not be found`. File, line, column, a `CS` code, a message. The three you will actually meet:

| Code | Cause |
|---|---|
| CS0246 | Missing `using …` at the top, or a library not referenced (see MODDING.md section 4). |
| CS0103 | Typo or wrong capitalization; C# names are case-sensitive. |
| CS0029 / CS0266 | Type mismatch, e.g. assigning a `float` into an `int`; convert explicitly. |

## Sources

| What | Where |
|---|---|
| Official C# tour and tutorials | https://learn.microsoft.com/en-us/dotnet/csharp/tour-of-csharp/ |
| *C# in 100 Seconds* (2 minute overview) | https://www.youtube.com/watch?v=ravLFzIguCM |
| The full Unity mod walkthrough | [*Lethal Company - How to write your own mod from scratch!*](https://www.youtube.com/watch?v=4Q7Zp5K2ywI) (MrMiinxx) |
| BepInEx documentation | https://docs.bepinex.dev/ |
| Harmony patching documentation | https://harmony.pardeike.net/ |
| Reading unfamiliar game code | MODDING.md section 3 (decompile with ilspycmd) |
