using BepInEx;
using BepInEx.Configuration;
using Game;
using HaniUtils.Steam.Netcode;
using HarmonyLib;
using Unity.Netcode;

namespace FullParty
{
	[BepInPlugin(ModMetadata.Guid, ModMetadata.Name, ModMetadata.Version)]
	public sealed class Plugin : BaseUnityPlugin
	{
		private const int SteamLobbyLimit = 250;
		internal static ConfigEntry<int> MaxPlayers;
		internal static ConfigEntry<int> PlayersPerBed;
		internal static ConfigEntry<bool> JoinAnytime;

		private void Awake()
		{
			MaxPlayers = Config.Bind("Host", "maxPlayers", 8, new ConfigDescription(
				"Players allowed in the lobby, including the host.",
				new AcceptableValueRange<int>(1, SteamLobbyLimit)));
			PlayersPerBed = Config.Bind("Host", "oneBedHolds", 0, new ConfigDescription(
				"Players each bed holds. 0 uses maxPlayers.",
				new AcceptableValueRange<int>(0, SteamLobbyLimit)));
			JoinAnytime = Config.Bind("Host", "joinAnytime", true,
				"Keep the Steam lobby joinable during a run. Steam privacy and lobby membership checks still apply.");

			new Harmony(ModMetadata.Guid).PatchAll();
			Logger.LogInfo($"{ModMetadata.Name} v{ModMetadata.Version} loaded.");
		}
	}

	[HarmonyPatch(typeof(SteamLobbyManager), nameof(SteamLobbyManager.StartHost))]
	internal static class LobbySizePatch
	{
		private static readonly AccessTools.FieldRef<SteamLobbyManager, int> MaxLobbyMembers =
			AccessTools.FieldRefAccess<SteamLobbyManager, int>("maxLobbyMembers");

		[HarmonyPrefix]
		private static void Prefix(SteamLobbyManager __instance)
		{
			MaxLobbyMembers(__instance) = Plugin.MaxPlayers.Value;
		}
	}

	[HarmonyPatch(typeof(SteamGameEventsManager), "HandleGameStarted")]
	internal static class JoinAnytimePatch
	{
		[HarmonyPostfix]
		private static void Postfix()
		{
			if (!Plugin.JoinAnytime.Value || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
			{
				return;
			}
			SteamLobbyManager lobby = SteamLobbyManager.Instance;
			if (lobby != null)
			{
				lobby.SetLobbyJoinable(true);
				lobby.RestoreConnectPresence();
			}
		}
	}
}
