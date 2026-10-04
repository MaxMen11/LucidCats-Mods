using System;
using System.Collections.Generic;
using System.Reflection;
using Game.UI;
using HaniUtils.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MenuKit
{
	public static class Menus
	{
		private static readonly List<KeyValuePair<string, Action>> SceneCallbacks = new List<KeyValuePair<string, Action>>();
		private static bool watchingScenes;

		public static void WhenScene(string sceneName, Action action)
		{
			if (string.IsNullOrWhiteSpace(sceneName))
			{
				throw new ArgumentException("A scene name is required.", nameof(sceneName));
			}
			if (action == null)
			{
				throw new ArgumentNullException(nameof(action));
			}

			SceneCallbacks.Add(new KeyValuePair<string, Action>(sceneName, action));
			if (!watchingScenes)
			{
				watchingScenes = true;
				SceneManager.sceneLoaded += SceneLoaded;
			}
			Scene active = SceneManager.GetActiveScene();
			if (active.IsValid() && active.isLoaded && active.name == sceneName)
			{
				Run("scene '" + sceneName + "'", action);
			}
		}

		public static bool TryGetMainMenu(out MainMenu mainMenu)
		{
			Scene scene = SceneManager.GetActiveScene();
			foreach (GameObject root in scene.GetRootGameObjects())
			{
				if (root.name != "Canvas")
				{
					continue;
				}
				Transform menuRoot = root.transform.Find("4x3");
				Transform statsButton = menuRoot?.Find("Margins/grid/UIButton (stats)");
				Transform statsPanel = menuRoot?.Find("Stats menu");
				if (menuRoot != null && statsButton != null && statsPanel != null)
				{
					UiButton button = statsButton.GetComponent<UiButton>();
					Menu panel = statsPanel.GetComponent<Menu>();
					if (button != null && panel != null)
					{
						mainMenu = new MainMenu(menuRoot, button, panel);
						return true;
					}
				}
			}
			mainMenu = null;
			return false;
		}

		public static VanillaMenu NewMenu(string title)
		{
			if (string.IsNullOrWhiteSpace(title))
			{
				throw new ArgumentException("A menu title is required.", nameof(title));
			}
			if (!TryGetMainMenu(out MainMenu main))
			{
				Debug.LogError("[MenuKit] Canvas/4x3 main-menu templates were not found.");
				return null;
			}

			GameObject holder = new GameObject(title + " Holder");
			holder.SetActive(false);
			GameObject panel = UnityEngine.Object.Instantiate(main.StatsPanel.gameObject, holder.transform, false);
			panel.name = title + " menu";
			foreach (LifetimeStatsDisplay display in panel.GetComponentsInChildren<LifetimeStatsDisplay>(true))
			{
				UnityEngine.Object.DestroyImmediate(display);
			}

			Menu menu = panel.GetComponent<Menu>();
			Transform content = panel.transform.Find("grid");
			Transform header = content?.Find("Text name");
			Transform row = content?.Find("stat display");
			if (menu == null || content == null || header == null || row == null)
			{
				UnityEngine.Object.Destroy(holder);
				Debug.LogError("[MenuKit] The Stats menu did not contain its expected menu, grid, header, and row templates.");
				return null;
			}

			TMP_Text titleText = panel.transform.Find("Text (TMP)")?.GetComponent<TMP_Text>();
			if (titleText != null)
			{
				titleText.text = title;
			}
			TMP_Text headerTemplate = UnityEngine.Object.Instantiate(header.gameObject, panel.transform, false).GetComponent<TMP_Text>();
			headerTemplate.gameObject.name = "Header Template";
			headerTemplate.gameObject.SetActive(false);
			GameObject rowTemplate = UnityEngine.Object.Instantiate(row.gameObject, panel.transform, false);
			rowTemplate.name = "Row Template";
			rowTemplate.SetActive(false);
			while (content.childCount > 0)
			{
				UnityEngine.Object.DestroyImmediate(content.GetChild(0).gameObject);
			}

			panel.transform.SetParent(main.StatsPanel.transform.parent, false);
			panel.transform.SetSiblingIndex(main.StatsPanel.transform.GetSiblingIndex() + 1);
			UnityEngine.Object.Destroy(holder);

			UiButton opener = CloneButton(main.StatsButton, title, null);
			VanillaMenu result = new VanillaMenu(menu, content, headerTemplate, rowTemplate);
			opener.onClick.AddListener(result.Toggle);
			foreach (Transform sibling in panel.transform.parent)
			{
				Menu other = sibling.GetComponent<Menu>();
				if (other != null && other != menu)
				{
					other.OnMenuOpenTriggered += menu.Close;
				}
			}
			return result;
		}

		public static UiButton CloneButton(UiButton template, string label, Action onClick)
		{
			if (template == null)
			{
				throw new ArgumentNullException(nameof(template));
			}
			if (string.IsNullOrWhiteSpace(label))
			{
				throw new ArgumentException("A button label is required.", nameof(label));
			}

			GameObject copy = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent, false);
			copy.name = "UIButton (" + label.ToLowerInvariant() + ")";
			copy.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
			UiButton button = copy.GetComponent<UiButton>();
			Silence(button.onClick);
			TMP_Text text = copy.GetComponentInChildren<TMP_Text>(true);
			if (text != null)
			{
				text.text = label;
			}
			if (onClick != null)
			{
				button.onClick.AddListener(delegate { onClick(); });
			}
			return button;
		}

		public static void SetDropdownOptions(TMP_Dropdown dropdown, IEnumerable<string> options, int selectedIndex)
		{
			if (dropdown == null)
			{
				throw new ArgumentNullException(nameof(dropdown));
			}
			if (options == null)
			{
				throw new ArgumentNullException(nameof(options));
			}
			dropdown.ClearOptions();
			dropdown.AddOptions(new List<string>(options));
			dropdown.SetValueWithoutNotify(Mathf.Clamp(selectedIndex, 0, Math.Max(0, dropdown.options.Count - 1)));
			dropdown.RefreshShownValue();
		}

		private static void SceneLoaded(Scene scene, LoadSceneMode mode)
		{
			foreach (KeyValuePair<string, Action> callback in SceneCallbacks.ToArray())
			{
				if (callback.Key == scene.name)
				{
					Run("scene '" + scene.name + "'", callback.Value);
				}
			}
		}

		private static void Run(string operation, Action action)
		{
			try
			{
				action();
			}
			catch (Exception e)
			{
				Debug.LogError("[MenuKit] " + operation + " failed: " + e);
			}
		}

		private static void Silence(UnityEventBase unityEvent)
		{
			if (unityEvent == null)
			{
				return;
			}
			unityEvent.RemoveAllListeners();
			for (int i = 0; i < unityEvent.GetPersistentEventCount(); i++)
			{
				unityEvent.SetPersistentListenerState(i, UnityEventCallState.Off);
			}
		}

		public sealed class MainMenu
		{
			internal MainMenu(Transform root, UiButton statsButton, Menu statsPanel)
			{
				Root = root;
				StatsButton = statsButton;
				StatsPanel = statsPanel;
			}

			public Transform Root { get; }
			public UiButton StatsButton { get; }
			public Menu StatsPanel { get; }
		}

		public sealed class VanillaMenu
		{
			private static readonly string[] SliderFields = { "mouseSensitivitySlider", "fieldOfViewSlider", "masterVolumeSlider" };
			private static readonly string[] ToggleFields = { "hideCrosshairToggle", "headBobToggle", "vSyncToggle" };
			private static readonly string[] DropdownFields = { "qualityDropdown", "pixelationDropdown", "maxFpsDropdown", "displayModeDropdown" };
			private readonly Transform content;
			private readonly TMP_Text headerTemplate;
			private readonly GameObject rowTemplate;

			internal VanillaMenu(Menu menu, Transform content, TMP_Text headerTemplate, GameObject rowTemplate)
			{
				Menu = menu;
				this.content = content;
				this.headerTemplate = headerTemplate;
				this.rowTemplate = rowTemplate;
			}

			public Menu Menu { get; }
			public Transform Content => content;

			public void Open()
			{
				Transform parent = Menu.transform.parent;
				if (parent != null)
				{
					foreach (Transform sibling in parent)
					{
						Menu other = sibling.GetComponent<Menu>();
						if (other != null && other != Menu && other.IsOpen)
						{
							other.Close();
						}
					}
				}
				if (!Menu.IsOpen)
				{
					Menu.Open();
				}
			}

			public void Close()
			{
				Menu.Close();
			}

			public void Toggle()
			{
				if (Menu.IsOpen)
				{
					Close();
				}
				else
				{
					Open();
				}
			}

			public TMP_Text Label(string text)
			{
				TMP_Text label = UnityEngine.Object.Instantiate(headerTemplate.gameObject, content, false).GetComponent<TMP_Text>();
				label.gameObject.name = "Label";
				label.gameObject.SetActive(true);
				label.text = text;
				return label;
			}

			public GameObject Row(string left, string right)
			{
				GameObject row = NewStatsRow(left);
				SetChildText(row.transform, "Text name", left);
				SetChildText(row.transform, "Text value", right);
				return row;
			}

			public Button Button(string label, Action onClick)
			{
				if (onClick == null)
				{
					throw new ArgumentNullException(nameof(onClick));
				}
				GameObject row = NewStatsRow(label);
				SetChildText(row.transform, "Text name", label);
				SetChildText(row.transform, "Text value", string.Empty);
				Image background = row.GetComponent<Image>();
				if (background != null)
				{
					background.raycastTarget = true;
				}
				Button button = row.AddComponent<Button>();
				button.transition = Selectable.Transition.None;
				button.targetGraphic = background;
				button.onClick.AddListener(delegate { onClick(); });
				return button;
			}

			public Slider Slider(string label, float value, Action<float> onChange, float min, float max)
			{
				if (onChange == null)
				{
					throw new ArgumentNullException(nameof(onChange));
				}
				if (min > max)
				{
					throw new ArgumentException("Slider minimum cannot exceed its maximum.");
				}
				Slider slider = CloneSettingsControl<Slider>(label, SliderFields);
				if (slider == null)
				{
					return null;
				}
				slider.minValue = min;
				slider.maxValue = max;
				slider.SetValueWithoutNotify(value);
				slider.onValueChanged.AddListener(changed => onChange(changed));
				return slider;
			}

			public Toggle Toggle(string label, bool value, Action<bool> onChange)
			{
				if (onChange == null)
				{
					throw new ArgumentNullException(nameof(onChange));
				}
				Toggle toggle = CloneSettingsControl<Toggle>(label, ToggleFields);
				if (toggle == null)
				{
					return null;
				}
				toggle.SetIsOnWithoutNotify(value);
				toggle.onValueChanged.AddListener(changed => onChange(changed));
				return toggle;
			}

			public TMP_Dropdown Dropdown(string label, IEnumerable<string> options, int value, Action<int> onChange)
			{
				if (onChange == null)
				{
					throw new ArgumentNullException(nameof(onChange));
				}
				TMP_Dropdown dropdown = CloneSettingsControl<TMP_Dropdown>(label, DropdownFields);
				if (dropdown == null)
				{
					return null;
				}
				SetDropdownOptions(dropdown, options, value);
				dropdown.onValueChanged.AddListener(changed => onChange(changed));
				return dropdown;
			}

			private GameObject NewStatsRow(string name)
			{
				GameObject row = UnityEngine.Object.Instantiate(rowTemplate, content, false);
				row.name = name;
				row.SetActive(true);
				return row;
			}

			private T CloneSettingsControl<T>(string label, string[] preferredFields) where T : Component
			{
				T template = FindSettingsControl<T>(preferredFields);
				if (template == null)
				{
					Debug.LogError("[MenuKit] No plain Settings-menu " + typeof(T).Name + " row was found.");
					return null;
				}
				Transform sourceRow = template.transform.parent;
				GameObject row = UnityEngine.Object.Instantiate(sourceRow.gameObject, content, false);
				row.name = label;
				row.SetActive(true);
				SetRowLabel(row.transform, label);
				foreach (Slider slider in row.GetComponentsInChildren<Slider>(true))
				{
					Silence(slider.onValueChanged);
				}
				foreach (Toggle toggle in row.GetComponentsInChildren<Toggle>(true))
				{
					Silence(toggle.onValueChanged);
				}
				foreach (TMP_Dropdown dropdown in row.GetComponentsInChildren<TMP_Dropdown>(true))
				{
					Silence(dropdown.onValueChanged);
				}
				return row.GetComponentInChildren<T>(true);
			}

			private static T FindSettingsControl<T>(IEnumerable<string> preferredFields) where T : Component
			{
				SettingsMenu settings = UnityEngine.Object.FindFirstObjectByType<SettingsMenu>(FindObjectsInactive.Include);
				if (settings == null)
				{
					return null;
				}
				const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
				foreach (string name in preferredFields)
				{
					if (typeof(SettingsMenu).GetField(name, flags)?.GetValue(settings) is T preferred && PlainRow(preferred.transform.parent))
					{
						return preferred;
					}
				}
				foreach (FieldInfo field in typeof(SettingsMenu).GetFields(flags))
				{
					if (field.GetValue(settings) is T found && PlainRow(found.transform.parent))
					{
						return found;
					}
				}
				return null;
			}

			private static bool PlainRow(Transform row)
			{
				if (row == null)
				{
					return false;
				}
				foreach (MonoBehaviour behaviour in row.GetComponentsInChildren<MonoBehaviour>(true))
				{
					if (behaviour != null && behaviour.GetType().Assembly.GetName().Name == "Assembly-CSharp")
					{
						return false;
					}
				}
				return true;
			}

			private static void SetRowLabel(Transform row, string value)
			{
				foreach (Transform child in row)
				{
					TMP_Text text = child.GetComponent<TMP_Text>();
					if (text != null && child.name.IndexOf("value", StringComparison.OrdinalIgnoreCase) < 0)
					{
						text.text = value;
						return;
					}
				}
			}

			private static void SetChildText(Transform parent, string name, string value)
			{
				TMP_Text text = parent.Find(name)?.GetComponent<TMP_Text>();
				if (text != null)
				{
					text.text = value;
				}
			}
		}
	}
}
