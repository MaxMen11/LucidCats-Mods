using BepInEx;
using BepInEx.Logging;
using Game.UI;
using HarmonyLib;
using UnityEngine;

namespace QualityPlus
{
	[BepInPlugin(ModMetadata.Guid, ModMetadata.Name, ModMetadata.Version)]
	public sealed class Plugin : BaseUnityPlugin
	{
		internal const string PresetKey = "qualityplus.preset";
		internal static ManualLogSource Log;

		private void Awake()
		{
			Log = Logger;
			Preset.Load(Config);

			Harmony harmony = new Harmony(ModMetadata.Guid);
			harmony.PatchAll(typeof(QualityMenu));
			harmony.PatchAll(typeof(Quality));

			GameSettings.OnGraphicsQualityChanged += delegate { Quality.Reapply(); };
			GameSettings.OnRenderScaleChanged += delegate { Quality.Reapply(); };
			QualitySettings.activeQualityLevelChanged += delegate { Quality.Reapply(); };
			Config.SettingChanged += delegate { Preset.Sort(); Quality.Reapply(); };
			Config.ConfigReloaded += delegate { Preset.Sort(); Quality.Reapply(); };

			Quality.Choose(PlayerPrefs.GetString(PresetKey, string.Empty));
			Logger.LogInfo($"{ModMetadata.Name} v{ModMetadata.Version} loaded.");
		}
	}
}
