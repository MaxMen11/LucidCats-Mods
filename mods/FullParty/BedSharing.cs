using System;
using System.Collections.Generic;
using Game;
using Game.Nights;
using Game.Player;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace FullParty
{
	[HarmonyPatch]
	internal static class BedSharing
	{
		private static readonly AccessTools.FieldRef<Bed, NetworkVariable<ulong>> Slot0 =
			AccessTools.FieldRefAccess<Bed, NetworkVariable<ulong>>("slot0");
		private static readonly AccessTools.FieldRef<Bed, NetworkVariable<ulong>> Slot1 =
			AccessTools.FieldRefAccess<Bed, NetworkVariable<ulong>>("slot1");
		private static readonly AccessTools.FieldRef<Bed, float> SlotSpacing =
			AccessTools.FieldRefAccess<Bed, float>("slotSpacing");
		private static readonly Action<PlayerSleepManager, NetworkObjectReference> GetUp =
			(Action<PlayerSleepManager, NetworkObjectReference>)Delegate.CreateDelegate(
				typeof(Action<PlayerSleepManager, NetworkObjectReference>),
				AccessTools.Method(typeof(PlayerSleepManager), "GetUpFromBedRpc"));
		private static readonly Dictionary<Bed, List<ulong>> Occupants = new Dictionary<Bed, List<ulong>>();
		private static Bed localBed;

		private static int Capacity => Plugin.PlayersPerBed.Value == 0
			? Plugin.MaxPlayers.Value
			: Plugin.PlayersPerBed.Value;

		[HarmonyPatch(typeof(Bed), nameof(Bed.FindOccupiedBy))]
		[HarmonyPrefix]
		private static bool FindOccupied(ulong clientId, ref Bed __result)
		{
			if (localBed != null && NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
			{
				__result = localBed;
				return false;
			}
			foreach (KeyValuePair<Bed, List<ulong>> bed in Occupants)
			{
				if (bed.Value.Contains(clientId))
				{
					__result = bed.Key;
					return false;
				}
			}
			return true;
		}

		[HarmonyPatch(typeof(Bed), nameof(Bed.Contains))]
		[HarmonyPrefix]
		private static bool Contains(Bed __instance, ulong clientId, ref bool __result)
		{
			__result = clientId != ulong.MaxValue
				&& Occupants.TryGetValue(__instance, out List<ulong> occupants)
				&& occupants.Contains(clientId);
			return false;
		}

		[HarmonyPatch(typeof(Bed), nameof(Bed.GetSlotOf))]
		[HarmonyPrefix]
		private static bool GetSlot(Bed __instance, ulong clientId, ref int __result)
		{
			__result = clientId != ulong.MaxValue && Occupants.TryGetValue(__instance, out List<ulong> occupants)
				? occupants.IndexOf(clientId)
				: -1;
			if (__result < 0 && localBed == __instance && NetworkManager.Singleton != null
				&& clientId == NetworkManager.Singleton.LocalClientId)
			{
				__result = 0;
			}
			return false;
		}

		[HarmonyPatch(typeof(Bed), nameof(Bed.OccupantCount), MethodType.Getter)]
		[HarmonyPrefix]
		private static bool GetCount(Bed __instance, ref int __result)
		{
			__result = Occupants.TryGetValue(__instance, out List<ulong> occupants) ? occupants.Count : 0;
			return false;
		}

		[HarmonyPatch(typeof(Bed), nameof(Bed.IsFull), MethodType.Getter)]
		[HarmonyPrefix]
		private static bool GetIsFull(Bed __instance, ref bool __result)
		{
			__result = __instance.IsServer
				&& Occupants.TryGetValue(__instance, out List<ulong> occupants)
				&& occupants.Count >= Capacity;
			return false;
		}

		[HarmonyPatch(typeof(Bed), nameof(Bed.AssignOccupant))]
		[HarmonyPrefix]
		private static bool Assign(Bed __instance, ulong clientId, ref int __result)
		{
			if (!__instance.IsServer)
			{
				__result = -1;
				return false;
			}
			if (!Occupants.TryGetValue(__instance, out List<ulong> occupants))
			{
				occupants = new List<ulong>();
				Occupants.Add(__instance, occupants);
			}
			__result = occupants.IndexOf(clientId);
			if (__result < 0 && occupants.Count < Capacity)
			{
				occupants.Add(clientId);
				__result = occupants.Count - 1;
				UpdateVanillaSlots(__instance, occupants);
			}
			return false;
		}

		[HarmonyPatch(typeof(Bed), nameof(Bed.ClearOccupant))]
		[HarmonyPrefix]
		private static bool Clear(Bed __instance, ulong clientId)
		{
			if (__instance.IsServer && Occupants.TryGetValue(__instance, out List<ulong> occupants)
				&& occupants.Remove(clientId))
			{
				UpdateVanillaSlots(__instance, occupants);
			}
			return false;
		}

		private static void UpdateVanillaSlots(Bed bed, List<ulong> occupants)
		{
			Slot0(bed).Value = occupants.Count > 0 ? occupants[0] : ulong.MaxValue;
			Slot1(bed).Value = occupants.Count >= Capacity
				? occupants[Math.Min(1, occupants.Count - 1)]
				: ulong.MaxValue;
		}

		[HarmonyPatch(typeof(PlayerSleepManager), "EnterBedMode")]
		[HarmonyPostfix]
		private static void EnterBed(Bed bed)
		{
			localBed = bed;
		}

		[HarmonyPatch(typeof(PlayerSleepManager), "GetUpFromBedRpc")]
		[HarmonyPostfix]
		private static void LeaveBed()
		{
			localBed = null;
		}

		[HarmonyPatch(typeof(PlayerSleepManager), "OnPlayerEnteredDream")]
		[HarmonyPrefix]
		private static void EnterDream(PlayerSleepManager __instance, ulong clientId)
		{
			if (__instance.IsOwner && __instance.OwnerClientId == clientId && localBed != null)
			{
				localBed.DeactivateCamera();
				localBed.ClearLookTarget();
				localBed = null;
			}
		}

		[HarmonyPatch(typeof(PlayerRoomSpawnAssigner), "AssignAndTeleportAll")]
		[HarmonyPrefix]
		private static void ReleaseBedCameras()
		{
			NetworkManager network = NetworkManager.Singleton;
			if (network == null || !network.IsServer)
			{
				return;
			}
			foreach (ulong clientId in network.ConnectedClientsIds)
			{
				Bed bed = Bed.FindOccupiedBy(clientId);
				if (bed != null && network.ConnectedClients.TryGetValue(clientId, out NetworkClient client)
					&& client.PlayerObject != null && client.PlayerObject.TryGetComponent(out PlayerManager player))
				{
					GetUp(player.SleepManager, bed.NetworkObject);
				}
			}
		}

		[HarmonyPatch(typeof(PlayerSleepManager), nameof(PlayerSleepManager.OnNetworkDespawn))]
		[HarmonyPrefix]
		private static void RemoveDisconnectedPlayer(PlayerSleepManager __instance)
		{
			if (__instance.IsServer)
			{
				Bed.FindOccupiedBy(__instance.OwnerClientId)?.ClearOccupant(__instance.OwnerClientId);
			}
		}

		[HarmonyPatch(typeof(Bed), nameof(Bed.OnNetworkDespawn))]
		[HarmonyPrefix]
		private static void RemoveBed(Bed __instance)
		{
			if (localBed == __instance)
			{
				localBed = null;
			}
			Occupants.Remove(__instance);
		}

		[HarmonyPatch(typeof(Bed), "SlotLocalOffset")]
		[HarmonyPrefix]
		private static bool PlaceSleeper(Bed __instance, int slot, ref Vector3 __result)
		{
			float spacing = SlotSpacing(__instance);
			__result = Vector3.right * (spacing * ((slot & 1) - 0.5f))
				+ Vector3.up * (spacing * (slot / 2));
			return false;
		}
	}
}
