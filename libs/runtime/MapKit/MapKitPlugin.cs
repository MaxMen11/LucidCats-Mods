using BepInEx;
using HarmonyLib;

namespace MapKit
{
	[BepInPlugin(ModMetadata.Guid, ModMetadata.Name, ModMetadata.Version)]
	public sealed class MapKitPlugin : BaseUnityPlugin
	{
		private Harmony harmony;

		private void Awake()
		{
			harmony = new Harmony(ModMetadata.Guid);
			MapKitRuntime.Initialize(Logger, Config, harmony);
			Logger.LogInfo($"{ModMetadata.Name} v{ModMetadata.Version} loaded.");
		}

		private void OnDestroy()
		{
			MapKitRuntime.Shutdown();
			harmony?.UnpatchSelf();
		}
	}
}
