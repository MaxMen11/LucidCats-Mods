using System;
using Game.UI;
using HarmonyLib;
using MenuKit;
using TMPro;

namespace QualityPlus
{
	[HarmonyPatch]
	internal static class QualityMenu
	{
		private static readonly AccessTools.FieldRef<SettingsMenu, TMP_Dropdown> Dropdown =
			AccessTools.FieldRefAccess<SettingsMenu, TMP_Dropdown>("qualityDropdown");

		[HarmonyPatch(typeof(SettingsMenu), "Awake")]
		[HarmonyPostfix]
		private static void Build(SettingsMenu __instance)
		{
			Fill(Dropdown(__instance));
		}

		[HarmonyPatch(typeof(SettingsMenu), "SyncFromSettings")]
		[HarmonyPostfix]
		private static void Sync(SettingsMenu __instance)
		{
			Fill(Dropdown(__instance));
		}

		[HarmonyPatch(typeof(SettingsMenu), "OnQualityDropdown")]
		[HarmonyPrefix]
		private static bool Pick(int index)
		{
			if (index >= 0 && index < Preset.All.Count)
			{
				Quality.Choose(Preset.All[index].Name);
			}
			return false;
		}

		private static void Fill(TMP_Dropdown dropdown)
		{
			if (dropdown != null)
			{
				Menus.SetDropdownOptions(
					dropdown,
					Preset.All.ConvertAll(preset => preset.Name),
					Math.Max(0, Preset.IndexOf(Quality.Current)));
			}
		}
	}
}
