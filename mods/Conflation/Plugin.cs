using System.Linq;
using BepInEx;
using HaniUtils.RoomGen;
using MapKit;
using UnityEngine;

namespace Conflation
{
	[BepInPlugin(ModMetadata.Guid, ModMetadata.Name, ModMetadata.Version)]
	public sealed partial class Plugin : BaseUnityPlugin
	{
		private void Awake()
		{
			Maps.Register(ModMetadata.Guid, ModMetadata.Version, map =>
			{
				LevelConfig[] vanilla = map.Context.ExistingLevels.ToArray();

				LevelConfig conflation = Maps.Level(
					"Conflation",
					Maps.Remix(vanilla),
					Maps.Environment(
						fogColor: new Color(0.02f, 0.02f, 0.032f),
						fogDensity: 0.035f,
						ambientLight: new Color(0.05f, 0.055f, 0.07f)
					)
				);

				map.AddLevel("main", conflation);
			});
			Logger.LogInfo($"{ModMetadata.Name} v{ModMetadata.Version} loaded.");
		}
	}
}
