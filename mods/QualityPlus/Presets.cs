using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using Game.UI;
using UnityEngine.Rendering.Universal;

namespace QualityPlus
{
	internal enum SettingTarget { Meta, Pipeline, Quality, Level }

	internal class Setting
	{
		internal readonly string Key;
		internal readonly SettingTarget Target;

		private readonly Func<ConfigFile, string, object, ConfigEntryBase> bind;

		private Setting(string key, SettingTarget target, Func<ConfigFile, string, object, ConfigEntryBase> binder)
		{
			Key = key;
			Target = target;
			bind = binder;
		}

		internal ConfigEntryBase Bind(ConfigFile file, string section, object instead = null) => bind(file, section, instead);

		private static Setting Make<T>(string key, SettingTarget target, T fallback, AcceptableValueBase limits, string info) =>
			new Setting(key, target, (file, section, instead) =>
				file.Bind(section, key, instead is T given ? given : fallback, new ConfigDescription(info, limits)));

		private static readonly AcceptableValueList<int> Maps = new AcceptableValueList<int>(256, 512, 1024, 2048, 4096, 8192);
		private static readonly AcceptableValueList<int> Cookies = new AcceptableValueList<int>(256, 512, 1024, 2048, 4096);

		internal static readonly Setting[] All =
		{
			Make("order", SettingTarget.Meta, 0, new AcceptableValueRange<int>(-50, 50), "Place in the Quality dropdown. Vanilla Low, Medium and High are 0, 1 and 2. Add a preset by adding a new [Section] with at least one setting."),
			Make("base", SettingTarget.Meta, GraphicsQuality.Low, null, "Vanilla preset this one starts from. Anything not set below is inherited from it, untouched."),

			Make("mainLightShadows", SettingTarget.Pipeline, true, null, "Whether the main light casts shadows. Vanilla on."),
			Make("shadowCascades", SettingTarget.Pipeline, 2, new AcceptableValueRange<int>(0, 4), "Slices the main light's shadow into more, sharper steps. 0 switches them off. Vanilla 1 / 2 / 3."),
			Make("shadowDistance", SettingTarget.Pipeline, 35f, new AcceptableValueRange<float>(0f, 200f), "Metres from the camera that still get shadows. 0 turns shadows off. Vanilla 25 / 35 / 35."),
			Make("mainShadowmapResolution", SettingTarget.Pipeline, 1024, Maps, "Texture size for the main light's shadow, the biggest shadow cost. Vanilla 1024."),
			Make("additionalShadowmapResolution", SettingTarget.Pipeline, 2048, Maps, "Texture size shared by all other shadow casting lights. Vanilla 2048 / 4096."),
			Make("cookieResolution", SettingTarget.Pipeline, 1024, Cookies, "Texture size for light cookies, the masks that shape a light. Vanilla 1024 / 2048 / 4096."),
			Make("additionalLightShadows", SettingTarget.Pipeline, false, null, "Whether lights other than the main one cast shadows. Vanilla off / on / on."),
			Make("softShadows", SettingTarget.Pipeline, false, null, "Smooths shadow edges instead of leaving them hard. Vanilla off / on / on."),
			Make("softShadowQuality", SettingTarget.Pipeline, SoftShadowQuality.Low, null, "How much work the softening does. Vanilla Low / Medium."),
			Make("renderScale", SettingTarget.Pipeline, 1f, new AcceptableValueRange<float>(0.1f, 2f), "Fraction of window size the world is drawn at. Below 1 also set upscaler to FSR. Vanilla 1."),
			Make("upscaler", SettingTarget.Pipeline, UpscalingFilterSelection.Auto, null, "Filter used to scale the picture back up. Vanilla Point, which Unity ignores while post processing runs."),
			Make("msaa", SettingTarget.Pipeline, 1, new AcceptableValueList<int>(1, 2, 4, 8), "Multisample anti aliasing samples. Vanilla 1, meaning off."),

			Make("lodBias", SettingTarget.Quality, 1f, new AcceptableValueRange<float>(0.1f, 3f), "How stubbornly distant models keep their detailed version. Vanilla 1 / 1.5 / 2."),

			Make("shadowHops", SettingTarget.Level, 2, new AcceptableValueRange<int>(0, 10), "How many rooms away lights still cast shadows. 0 is your room only. Vanilla 2."),
			Make("lightHops", SettingTarget.Level, 3, new AcceptableValueRange<int>(0, 10), "How many rooms away lights stay switched on. Vanilla 3."),
			Make("lightBeams", SettingTarget.Level, true, null, "Volumetric light beams, the cones of dusty light around lamps. Vanilla on."),
			Make("ssgiResolution", SettingTarget.Level, 1f, new AcceptableValueRange<float>(0f, 1f), "Resolution of screen space global illumination, the bounced light. 0.5 saves a lot, 0 switches it off. Vanilla 1.")
		};

		internal static Setting Find(string key) => All.FirstOrDefault(setting => setting.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
	}

	internal class Preset
	{
		internal static List<Preset> All = new List<Preset>();

		internal string Name;

		private int defaultOrder;
		private readonly Dictionary<string, ConfigEntryBase> entries = new Dictionary<string, ConfigEntryBase>(StringComparer.OrdinalIgnoreCase);

		internal int Order => Get("order", out int order) ? order : defaultOrder;

		internal GraphicsQuality Base => Get("base", out GraphicsQuality quality) ? quality : Inherited(defaultOrder);

		internal bool Touched => entries.Keys.Any(key => Setting.Find(key).Target != SettingTarget.Meta);

		internal bool Touches(SettingTarget target) => entries.Keys.Any(key => Setting.Find(key).Target == target);

		internal bool Get<T>(string key, out T value)
		{
			value = default;

			if (!entries.TryGetValue(key, out ConfigEntryBase entry) || !(entry.BoxedValue is T stored))
				return false;

			value = stored;
			return true;
		}

		internal static int IndexOf(string name) => All.FindIndex(preset => preset.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

		internal static void Sort() => All = All.OrderBy(preset => preset.Order).ThenBy(preset => preset.Name).ToList();

		internal static void Load(ConfigFile file)
		{
			bool saving = file.SaveOnConfigSet;
			file.SaveOnConfigSet = false;

			All = new List<Preset>();

			foreach (KeyValuePair<string, List<string>> section in Scan(file))
			{
				Preset preset = Meta(file, section.Key);

				foreach (string key in section.Value)
				{
					Setting setting = Setting.Find(key);

					if (setting == null)
						Plugin.Log.LogWarning($"[{preset.Name}] {key} is not a QualityPlus setting, leaving it alone.");
					else if (setting.Target != SettingTarget.Meta)
						preset.entries[setting.Key] = setting.Bind(file, preset.Name);
				}

				All.Add(preset);
			}

			foreach (GraphicsQuality stock in Enum.GetValues(typeof(GraphicsQuality)))
				if (IndexOf(stock.ToString()) < 0)
					All.Add(Meta(file, stock.ToString()));

			Sort();

			file.Save();
			file.SaveOnConfigSet = saving;
		}

		private static Preset Meta(ConfigFile file, string name)
		{
			Preset preset = new Preset { Name = name, defaultOrder = Slot(name) };
			preset.entries["order"] = Setting.Find("order").Bind(file, name, preset.defaultOrder);
			preset.entries["base"] = Setting.Find("base").Bind(file, name, Inherited(preset.Order));
			return preset;
		}

		// BepInEx only lists settings that are already bound and keeps unbound lines private,
		// so the file is read as text first to learn which sections and keys exist.
		private static Dictionary<string, List<string>> Scan(ConfigFile file)
		{
			string path = file.ConfigFilePath;
			Dictionary<string, List<string>> sections = File.Exists(path) ? Read(File.ReadAllLines(path)) : new Dictionary<string, List<string>>();

			if (sections.Count > 0)
				return sections;

			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllLines(path, Example);
			file.Reload();
			return Read(Example);
		}

		private static Dictionary<string, List<string>> Read(string[] lines)
		{
			Dictionary<string, List<string>> sections = new Dictionary<string, List<string>>();
			List<string> current = null;

			foreach (string line in lines.Select(raw => raw.Trim()).Where(line => line.Length > 0 && line[0] != '#' && line[0] != ';'))
			{
				if (line[0] == '[' && line[line.Length - 1] == ']')
				{
					string name = line.Substring(1, line.Length - 2).Trim();

					if (name.Length > 0 && !sections.TryGetValue(name, out current))
						sections[name] = current = new List<string>();
				}
				else if (current != null && line.IndexOf('=') > 0)
					current.Add(line.Substring(0, line.IndexOf('=')).Trim());
			}

			return sections;
		}

		private static int Slot(string name) => Enum.TryParse(name, true, out GraphicsQuality stock) ? (int)stock : 3;

		private static GraphicsQuality Inherited(int order) => order <= 0 ? GraphicsQuality.Low : order == 1 ? GraphicsQuality.Medium : GraphicsQuality.High;

		private static readonly string[] Example =
		{
			"[Ultra]", "order = 4", "renderScale = 1.25", "upscaler = FSR", "msaa = 2", "lodBias = 3", "shadowCascades = 4",
			"shadowDistance = 75", "mainShadowmapResolution = 4096", "additionalShadowmapResolution = 4096",
			"cookieResolution = 4096", "additionalLightShadows = true", "softShadows = true",
			"softShadowQuality = High", "shadowHops = 4", "lightHops = 5", "ssgiResolution = 1",
			"[VHigh]", "order = 3", "lodBias = 2.5", "shadowCascades = 4", "shadowDistance = 50",
			"mainShadowmapResolution = 2048", "additionalShadowmapResolution = 4096", "cookieResolution = 2048",
			"additionalLightShadows = true", "softShadows = true", "softShadowQuality = High",
			"shadowHops = 3", "lightHops = 4", "ssgiResolution = 1",
			"[VLow]", "order = -1", "lodBias = 0.8", "shadowCascades = 1", "shadowDistance = 15", "mainShadowmapResolution = 512",
			"shadowHops = 1", "lightHops = 2", "ssgiResolution = 0.5",
			"[Minimal]", "order = -2", "lodBias = 0.3", "shadowCascades = 1", "shadowDistance = 8",
			"mainShadowmapResolution = 256", "additionalShadowmapResolution = 256", "cookieResolution = 256",
			"additionalLightShadows = false", "softShadows = false", "shadowHops = 0", "lightHops = 1",
			"lightBeams = true", "ssgiResolution = 0",
			"[None]", "order = -3", "renderScale = 0.5", "upscaler = Linear", "msaa = 1", "lodBias = 0.1",
			"shadowCascades = 0", "shadowDistance = 0", "mainShadowmapResolution = 256",
			"additionalShadowmapResolution = 256", "cookieResolution = 256", "additionalLightShadows = false",
			"softShadows = false", "shadowHops = 0", "lightHops = 0", "lightBeams = false", "ssgiResolution = 0"
		};
	}
}
