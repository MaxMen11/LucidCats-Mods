using BepInEx;

namespace RuntimeName
{
	[BepInPlugin(ModMetadata.Guid, ModMetadata.Name, ModMetadata.Version)]
	public sealed class RuntimeNamePlugin : BaseUnityPlugin
	{
		private void Awake()
		{
			Logger.LogInfo($"{ModMetadata.Name} v{ModMetadata.Version} loaded.");
		}
	}
}
