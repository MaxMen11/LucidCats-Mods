# MenuKit

MenuKit is a source library for building game-styled main-menu panels from Lucid Cats' own UI templates. It contains no Harmony patches and adds no runtime dependency.

The main-menu paths and cloning approach were checked against the public [Lucid Cats Bestiary](https://github.com/SusoTF/LucidCatsBestiary) and [Lucid Cats Mod Manager](https://github.com/SusoTF/LucidCatsModManager) implementations. Settings controls are selected from the game's `SettingsMenu` fields, following the Mod Manager's safer template-selection pattern rather than taking an arbitrary control from the scene.

MenuKit has been source-checked and compiled, but not tested in the game. See `NOTICE.md`.

## Example

```csharp
using BepInEx;
using MenuKit;

public sealed class Plugin : BaseUnityPlugin
{
	private float speed = 1f;
	private bool flashy;

	private void Awake()
	{
		Menus.WhenScene("MainMenu", BuildMenu);
	}

	private void BuildMenu()
	{
		Menus.VanillaMenu menu = Menus.NewMenu("Dance Menu");
		if (menu == null)
		{
			return;
		}

		menu.Label("PICK A MOVE");
		menu.Row("Spin", "2 seconds");
		menu.Button("Do the spin", DoSpin);
		menu.Slider("Speed", speed, value => speed = value, 0.5f, 3f);
		menu.Toggle("Flashy", flashy, value => flashy = value);
		menu.Dropdown("Style", new[] { "Calm", "Wild" }, 0, PickStyle);
	}

	private void DoSpin()
	{
	}

	private void PickStyle(int index)
	{
	}
}
```

## API

| Call | Result |
|---|---|
| `Menus.WhenScene(name, action)` | Runs the callback on matching scene loads, or immediately when that scene is already active. Callback failures are isolated and logged. |
| `Menus.TryGetMainMenu(out menu)` | Finds the proven `Canvas/4x3` main-menu root plus its Stats button and panel. |
| `Menus.NewMenu(title)` | Clones the Stats panel and Stats opener, clears the cloned stats content, and returns a handle. |
| `Menus.CloneButton(template, label, action)` | Clones a specific `UiButton`; it never guesses which button the caller intended. |
| `Menus.SetDropdownOptions(dropdown, options, index)` | Replaces options and selects an item without firing the dropdown callback. It does not remove listeners. |

`VanillaMenu` provides `Open`, `Close`, `Toggle`, `Label`, `Row`, `Button`, `Slider`, `Toggle`, and `Dropdown`. Its `Menu` and `Content` properties expose the underlying game component and content transform for custom work.

The control methods clone plain rows referenced by the live `Game.UI.SettingsMenu`. All listeners are removed only from the new clone before the caller's listener is added. MenuKit deliberately does not replace listeners on existing vanilla controls; a consuming mod that changes vanilla behavior must intercept that behavior itself.

## Scope and limits

- The current main-menu structure is version-specific. Missing required templates produce a clear failure instead of a fabricated fallback layout.
- Opening a MenuKit panel closes sibling `HaniUtils.UI.Menu` panels. Existing sibling panels also close it through their public open event. MenuKit does not globally patch `Menu.Open`, so a later mod can still create a panel with its own coordination policy.
- Appearance and layout come from cloned game objects. MenuKit does not claim live-game validation; consuming mods should test every control and supported aspect ratio.
