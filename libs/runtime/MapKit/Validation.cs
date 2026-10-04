using System;
using System.Collections.Generic;
using System.Linq;
using HaniUtils.RoomGen;

namespace MapKit
{
	internal static class MapValidation
	{
		internal static void ThrowIfInvalid(LevelConfig level, string id)
		{
			List<string> errors = new List<string>();
			List<string> warnings = new List<string>();
			Validate(level, errors, warnings);
			foreach (string warning in warnings)
			{
				MapKitRuntime.Log.LogWarning("[" + id + "] " + warning);
			}
			if (errors.Count > 0)
			{
				throw new InvalidOperationException("Map '" + id + "' is invalid:\n- " + string.Join("\n- ", errors));
			}
		}

		private static void Validate(LevelConfig level, List<string> errors, List<string> warnings)
		{
			if (level == null)
			{
				errors.Add("Level config is null.");
				return;
			}
			if (string.IsNullOrWhiteSpace(level.name))
			{
				errors.Add("Level display name is empty.");
			}
			RoomPoolConfig pool = level.procGenConfig;
			if (pool == null)
			{
				errors.Add("Room pool is null.");
				return;
			}
			GeneratedRoom[] rooms = pool.RoomPrefabs;
			if (rooms == null || rooms.Length == 0)
			{
				errors.Add("Room pool is empty.");
				return;
			}
			if (rooms.Any(room => room == null))
			{
				warnings.Add("Room pool contains a null room; the game will ignore it.");
			}
			if (rooms.Where(room => room != null).Distinct().Count() != rooms.Count(room => room != null))
			{
				errors.Add("Room pool contains a duplicate room reference.");
			}
			GeneratedRoom[] present = rooms.Where(room => room != null).ToArray();
			if (pool.StartRoomOverride == null && !present.Any(room => room.EffectiveMaxCount > 0))
			{
				errors.Add("No room can be selected as the start room.");
			}
			else if (pool.StartRoomOverride == null && !present.Any(room => room.CanBeStart && room.EffectiveMaxCount > 0))
			{
				warnings.Add("No room is marked as a start room; the game will choose from all placeable rooms.");
			}
			if (!present.Any(room => room.CanBeSpawnRoom))
			{
				warnings.Add("No room is eligible as a player spawn room.");
			}
			if (pool.ExitDoorPrefab != null && !present.Any(room => room.CanContainExit || room.ExitDoorway != null))
			{
				warnings.Add("An exit prefab exists, but no room is exit-eligible; the game will fall back to an arbitrary dead end.");
			}
			foreach (GeneratedRoom room in present)
			{
				ValidateRoom(room, errors);
			}
		}

		private static void ValidateRoom(GeneratedRoom room, List<string> errors)
		{
			if (room.Bounds == null)
			{
				errors.Add("Room '" + room.name + "' has no bounds collider.");
			}
			else if (room.Bounds.size.x <= 0f || room.Bounds.size.y <= 0f || room.Bounds.size.z <= 0f)
			{
				errors.Add("Room '" + room.name + "' has nonpositive bounds.");
			}
			if (room.SpawnWeight < 0f || float.IsNaN(room.SpawnWeight) || float.IsInfinity(room.SpawnWeight))
			{
				errors.Add("Room '" + room.name + "' has an invalid spawn weight.");
			}
			if (room.MinCount < 0 || room.MaxCount < -1 || room.MaxCount >= 0 && room.MinCount > room.MaxCount)
			{
				errors.Add("Room '" + room.name + "' has an invalid min/max count.");
			}
		}
	}
}
