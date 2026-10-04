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
