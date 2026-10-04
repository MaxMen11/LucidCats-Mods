using System;
using System.Collections.Generic;
using HaniUtils.EnvSettings;
using HaniUtils.RoomGen;
using UnityEngine;

namespace MapKit
{
	public static class Maps
	{
		public const int ApiVersion = 1;

		public static string Manifest => MapKitRuntime.ManifestText();

		public static bool CustomMapsEnabled => MapCompatibility.CustomMapsAllowed;

		public static void DumpManifest()
		{
			MapKitRuntime.Log?.LogInfo("Manifest: " + Manifest);
		}

		public static void Register(string id, string version, Action<MapBuilder> build)
		{
			MapKitRuntime.Register(id, version, null, build);
		}

		public static void Register(string id, string version, string contentHash, Action<MapBuilder> build)
		{
			MapKitRuntime.Register(id, version, contentHash, build);
		}

		public static LevelConfig Level(string name, RoomPoolConfig pool, EnvironmentSettings environment = null, bool cullRoomLights = true)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				throw new ArgumentException("A level name is required.", nameof(name));
			}
			if (pool == null)
			{
				throw new ArgumentNullException(nameof(pool));
			}
			LevelConfig level = ScriptableObject.CreateInstance<LevelConfig>();
			level.name = name;
			level.procGenConfig = pool;
			level.environmentSettings = environment;
			level.cullRoomLights = cullRoomLights;
			MapBuildScope.Track(level);
			return level;
		}

		public static RoomPoolConfig Pool(params GeneratedRoom[] rooms)
		{
			return Pool(null, rooms);
		}

		public static RoomPoolConfig Pool(LevelConfig borrowDoorsFrom, params GeneratedRoom[] rooms)
		{
			RoomPoolConfig pool = ScriptableObject.CreateInstance<RoomPoolConfig>();
			pool.RoomPrefabs = rooms ?? Array.Empty<GeneratedRoom>();
			RoomPoolConfig source = borrowDoorsFrom == null ? null : borrowDoorsFrom.procGenConfig;
			if (source != null)
			{
				pool.ConnectedDoorPrefab = source.ConnectedDoorPrefab;
				pool.SealedDoorPrefab = source.SealedDoorPrefab;
				pool.ExitDoorPrefab = source.ExitDoorPrefab;
			}
			MapBuildScope.Track(pool);
			return pool;
		}

		public static RoomPoolConfig Remix(params LevelConfig[] sources)
		{
			List<GeneratedRoom> rooms = new List<GeneratedRoom>();
			RoomPoolConfig mixed = ScriptableObject.CreateInstance<RoomPoolConfig>();
			foreach (LevelConfig source in sources ?? Array.Empty<LevelConfig>())
			{
				RoomPoolConfig pool = source == null ? null : source.procGenConfig;
				if (pool?.RoomPrefabs == null)
				{
					continue;
				}
				foreach (GeneratedRoom room in pool.RoomPrefabs)
				{
					if (room != null && !rooms.Contains(room))
					{
						rooms.Add(room);
					}
				}
				mixed.StartRoomOverride = mixed.StartRoomOverride != null ? mixed.StartRoomOverride : pool.StartRoomOverride;
				mixed.ConnectedDoorPrefab = mixed.ConnectedDoorPrefab != null ? mixed.ConnectedDoorPrefab : pool.ConnectedDoorPrefab;
				mixed.SealedDoorPrefab = mixed.SealedDoorPrefab != null ? mixed.SealedDoorPrefab : pool.SealedDoorPrefab;
				mixed.ExitDoorPrefab = mixed.ExitDoorPrefab != null ? mixed.ExitDoorPrefab : pool.ExitDoorPrefab;
			}
			mixed.RoomPrefabs = rooms.ToArray();
			MapBuildScope.Track(mixed);
			return mixed;
		}

		public static EnvironmentSettings EnvironmentFrom(LevelConfig source)
		{
			EnvironmentSettings original = source == null ? null : source.environmentSettings;
			if (original == null)
			{
				return Environment();
			}
			return Environment(
				original.enableFog,
				original.fogColor,
				original.fogDensity,
				original.fogMode,
				original.ambientLight,
				original.skyboxMaterial);
		}

		public static EnvironmentSettings Environment(bool fog = true, Color? fogColor = null, float fogDensity = 0.01f, FogMode fogMode = FogMode.ExponentialSquared, Color? ambientLight = null, Material skyboxMaterial = null)
		{
			if (!float.IsFinite(fogDensity) || fogDensity < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(fogDensity));
			}
			EnvironmentSettings settings = ScriptableObject.CreateInstance<EnvironmentSettings>();
			settings.enableFog = fog;
			settings.fogColor = fogColor ?? Color.gray;
			settings.fogDensity = fogDensity;
			settings.fogMode = fogMode;
			settings.ambientLight = ambientLight ?? Color.black;
			settings.skyboxMaterial = skyboxMaterial;
			MapBuildScope.Track(settings);
			return settings;
		}

		public static RoomBuilder Room(string name, float width, float height, float depth)
		{
			RoomBuilder room = Room(name, height);
			room.Floor(0f, 0f, width, depth);
			float halfWidth = width * 0.5f;
			float halfDepth = depth * 0.5f;
			room.Wall(-halfWidth, -halfDepth, halfWidth, -halfDepth);
			room.Wall(halfWidth, -halfDepth, halfWidth, halfDepth);
			room.Wall(halfWidth, halfDepth, -halfWidth, halfDepth);
			room.Wall(-halfWidth, halfDepth, -halfWidth, -halfDepth);
			return room;
		}

		public static RoomBuilder Room(string name, float height = 3f)
		{
			MapBuildScope scope = MapBuildScope.Require();
			return new RoomBuilder(scope, name, height);
		}
	}
}
