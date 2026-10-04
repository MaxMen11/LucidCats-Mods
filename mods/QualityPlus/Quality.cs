using System;
using System.Collections.Generic;
using Game.Levels;
using Game.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VLB;
using Object = UnityEngine.Object;

namespace QualityPlus
{
	[HarmonyPatch]
	internal static class Quality
	{
		private static readonly AccessTools.FieldRef<LevelLightCuller, int> ShadowHops = AccessTools.FieldRefAccess<LevelLightCuller, int>("shadowHops");
		private static readonly AccessTools.FieldRef<LevelLightCuller, int> LightHops = AccessTools.FieldRefAccess<LevelLightCuller, int>("lightHops");

		private static readonly Dictionary<Object, Action> Restorations = new Dictionary<Object, Action>();

		private static UniversalRenderPipelineAsset pipelineCopy;
		private static RenderPipelineAsset stockPipeline;
		private static Preset currentPreset;
		private static string selectedPreset = string.Empty;
		private static float stockLodBias;
		private static bool lodChanged, changingQuality;
		private static int qualityLevel;

		internal static string Current => Preset.IndexOf(selectedPreset) >= 0 ? selectedPreset : GameSettings.GraphicsQuality.ToString();

		internal static void Choose(string name) => Choose(Preset.IndexOf(name));

		internal static void Choose(int index)
		{
			if (index < 0 || index >= Preset.All.Count)
				return;

			Undo();

			Preset preset = Preset.All[index];
			selectedPreset = preset.Name;
			currentPreset = preset;
			PlayerPrefs.SetString(Plugin.PresetKey, preset.Name);
			PlayerPrefs.Save();

			// Fires OnGraphicsQualityChanged, which would land back in Reapply before the copy exists.
			changingQuality = true;
			GameSettings.GraphicsQuality = preset.Base;
			changingQuality = false;

			Install(preset);
			Apply();
		}

		internal static void Reapply()
		{
			int index = changingQuality ? -1 : Preset.IndexOf(selectedPreset);

			if (index < 0)
				return;

			if (Preset.All[index].Touched)
				Choose(index);
			else if (Preset.All[index].Base != GameSettings.GraphicsQuality)
				selectedPreset = GameSettings.GraphicsQuality.ToString();
		}

		private static void Install(Preset preset)
		{
			qualityLevel = QualitySettings.GetQualityLevel();

			if (preset.Get("lodBias", out float bias))
			{
				stockLodBias = QualitySettings.lodBias;
				lodChanged = true;
				QualitySettings.lodBias = bias;
			}

			if (!preset.Touches(SettingTarget.Pipeline) || !(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset stock))
				return;

			stockPipeline = QualitySettings.renderPipeline;
			pipelineCopy = Object.Instantiate(stock);
			pipelineCopy.hideFlags = HideFlags.HideAndDontSave;

			Write(preset, pipelineCopy);
			QualitySettings.renderPipeline = pipelineCopy;
		}

		private static void Undo()
		{
			if (pipelineCopy != null || lodChanged)
			{
				// A pipeline asset and a LOD bias belong to the quality level that was active when they were set.
				changingQuality = true;
				int now = QualitySettings.GetQualityLevel();
				QualitySettings.SetQualityLevel(qualityLevel, false);

				if (pipelineCopy != null)
				{
					QualitySettings.renderPipeline = stockPipeline;
					Object.Destroy(pipelineCopy);
					pipelineCopy = null;
					stockPipeline = null;
				}

				if (lodChanged)
				{
					QualitySettings.lodBias = stockLodBias;
					lodChanged = false;
				}

				QualitySettings.SetQualityLevel(now, false);
				changingQuality = false;
			}

			foreach (KeyValuePair<Object, Action> entry in Restorations)
				if (entry.Key != null)
					entry.Value();

			Restorations.Clear();
			currentPreset = null;
		}

		private static void Write(Preset preset, UniversalRenderPipelineAsset copy)
		{
			if (preset.Get("mainLightShadows", out bool mainShadows)) Set(copy, "m_MainLightShadowsSupported", mainShadows);
			if (preset.Get("shadowCascades", out int cascades))
			{
				Set(copy, "m_MainLightShadowsSupported", cascades > 0);
				copy.shadowCascadeCount = Math.Max(1, cascades);
			}
			if (preset.Get("shadowDistance", out float distance)) copy.shadowDistance = distance;
			if (preset.Get("mainShadowmapResolution", out int main)) copy.mainLightShadowmapResolution = main;
			if (preset.Get("additionalShadowmapResolution", out int extra)) copy.additionalLightsShadowmapResolution = extra;
			if (preset.Get("cookieResolution", out int cookie)) Set(copy, "m_AdditionalLightsCookieResolution", (LightCookieResolution)cookie);
			if (preset.Get("additionalLightShadows", out bool extraShadows)) Set(copy, "m_AdditionalLightShadowsSupported", extraShadows);
			if (preset.Get("softShadows", out bool soft)) Set(copy, "m_SoftShadowsSupported", soft);
			if (preset.Get("softShadowQuality", out SoftShadowQuality quality)) Set(copy, "m_SoftShadowQuality", quality);
			if (preset.Get("renderScale", out float scale)) copy.renderScale = scale;
			if (preset.Get("upscaler", out UpscalingFilterSelection upscaler)) copy.upscalingFilter = upscaler;
			if (preset.Get("msaa", out int msaa)) copy.msaaSampleCount = msaa;
		}

		private static void Apply()
		{
			foreach (LevelLightCuller culler in Object.FindObjectsByType<LevelLightCuller>(FindObjectsInactive.Include, FindObjectsSortMode.None))
				Tune(culler);

			foreach (Volume volume in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
				Bounce(volume);

			foreach (VolumetricLightBeamAbstractBase beam in Object.FindObjectsByType<VolumetricLightBeamAbstractBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
				Beam(beam);
		}

		[HarmonyPatch(typeof(VolumetricLightBeamSD), "OnEnable"), HarmonyPostfix]
		private static void BeamSD(VolumetricLightBeamAbstractBase __instance) => Beam(__instance);

		[HarmonyPatch(typeof(VolumetricLightBeamHD), "OnEnable"), HarmonyPostfix]
		private static void BeamHD(VolumetricLightBeamAbstractBase __instance) => Beam(__instance);

		private static void Beam(VolumetricLightBeamAbstractBase beam)
		{
			if (currentPreset == null || beam == null || Restorations.ContainsKey(beam) || !currentPreset.Get("lightBeams", out bool lit) || lit)
				return;

			bool was = beam.enabled;
			Restorations[beam] = delegate { beam.enabled = was; };
			beam.enabled = false;
		}

		// Runs on every level, which is also the moment to write the values back onto the
		// copy, because the game reapplies its own render scale without saying so.
		[HarmonyPatch(typeof(LevelLightCuller), "OnEnable"), HarmonyPostfix]
		private static void Tune(LevelLightCuller __instance)
		{
			if (currentPreset == null || __instance == null)
				return;

			if (pipelineCopy != null)
				Write(currentPreset, pipelineCopy);

			if (Restorations.ContainsKey(__instance))
				return;

			int shadowWas = ShadowHops(__instance), lightWas = LightHops(__instance);

			Restorations[__instance] = delegate
			{
				ShadowHops(__instance) = shadowWas;
				LightHops(__instance) = lightWas;
			};

			if (currentPreset.Get("shadowHops", out int shadow))
				ShadowHops(__instance) = shadow;

			if (currentPreset.Get("lightHops", out int light))
				LightHops(__instance) = light;
		}

		[HarmonyPatch(typeof(Volume), "OnEnable"), HarmonyPostfix]
		private static void Bounce(Volume __instance)
		{
			if (currentPreset != null && currentPreset.Get("ssgiResolution", out float scale) && __instance.sharedProfile != null && __instance.sharedProfile.TryGet(out ScreenSpaceGlobalIlluminationVolume bounced))
				Tune(bounced, scale);
		}

		private static void Tune(ScreenSpaceGlobalIlluminationVolume bounced, float scale)
		{
			if (!Restorations.ContainsKey(bounced))
			{
				bool fullWas = bounced.fullResolutionSS.value, fullSetWas = bounced.fullResolutionSS.overrideState;
				float scaleWas = bounced.resolutionScaleSS.value;
				bool scaleSetWas = bounced.resolutionScaleSS.overrideState, activeWas = bounced.active;

				Restorations[bounced] = delegate
				{
					bounced.fullResolutionSS.value = fullWas;
					bounced.fullResolutionSS.overrideState = fullSetWas;
					bounced.resolutionScaleSS.value = scaleWas;
					bounced.resolutionScaleSS.overrideState = scaleSetWas;
					bounced.active = activeWas;
				};
			}

			bounced.active = scale > 0f;
			bounced.fullResolutionSS.value = scale >= 1f;
			bounced.fullResolutionSS.overrideState = true;
			bounced.resolutionScaleSS.value = Mathf.Max(0.1f, scale);
			bounced.resolutionScaleSS.overrideState = true;
		}

		private static void Set(object asset, string field, object value)
		{
			System.Reflection.FieldInfo target = AccessTools.Field(asset.GetType(), field);

			if (target == null)
				Plugin.Log.LogError($"{asset.GetType().Name} has no field {field}, this game build needs a newer QualityPlus.");
			else
				target.SetValue(asset, value);
		}
	}
}
