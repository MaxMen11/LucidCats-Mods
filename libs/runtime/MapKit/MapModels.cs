using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using HaniUtils.RoomGen;
using UnityEngine;

namespace MapKit
{
	public enum GenerationPhase
	{
		BeforeGeneration,
		AfterRoomsPlaced,
		BeforeNavMesh,
		AfterGeneration,
		LevelCleared
	}

	public enum ColliderMode
	{
		Solid,
		Trigger,
		None
	}

	public sealed class MapBuilder
	{
		private readonly MapProvider provider;
		private readonly List<MapEntry> levels = new List<MapEntry>();

		internal MapBuilder(MapProvider provider, MapContext context)
		{
			this.provider = provider;
			Context = context;
		}

		public MapContext Context { get; }

		public void AddLevel(string id, LevelConfig level)
		{
			if (!MapIds.ValidPart(id))
			{
				throw new ArgumentException("A level ID may contain only letters, numbers, dots, underscores, and hyphens.", nameof(id));
			}
			if (level == null)
			{
				throw new ArgumentNullException(nameof(level));
			}
			levels.Add(new MapEntry(provider, provider.Id + "/" + id, level));
		}

		public void Unavailable(string reason)
		{
			if (string.IsNullOrWhiteSpace(reason))
			{
				throw new ArgumentException("An unavailable map needs a reason.", nameof(reason));
			}
			throw new MapUnavailableException(reason);
		}

		public void On(GenerationPhase phase, Action<GenerationContext> action)
		{
			if (action == null)
			{
				throw new ArgumentNullException(nameof(action));
			}
			provider.Hooks.Add(phase, action);
		}

		internal IReadOnlyList<MapEntry> Finish()
		{
			if (levels.Count == 0)
			{
				throw new InvalidOperationException("The provider did not add a level.");
			}
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			foreach (MapEntry level in levels)
			{
				if (!ids.Add(level.Id))
				{
					throw new InvalidOperationException("Duplicate level ID '" + level.Id + "'.");
				}
				MapValidation.ThrowIfInvalid(level.Level, level.Id);
			}
			return levels.AsReadOnly();
		}
	}

	public sealed class MapContext
	{
		private readonly ReadOnlyCollection<LevelConfig> existingLevels;
		private readonly ReadOnlyCollection<GeneratedRoom> existingRooms;

		internal MapContext(LevelConfig[] levels, MapGameAdapter adapter)
		{
			Adapter = adapter;
			existingLevels = Array.AsReadOnly(levels ?? Array.Empty<LevelConfig>());
			List<GeneratedRoom> rooms = new List<GeneratedRoom>();
			foreach (LevelConfig level in existingLevels)
			{
				GeneratedRoom[] prefabs = level?.procGenConfig?.RoomPrefabs;
				if (prefabs == null)
				{
					continue;
				}
				foreach (GeneratedRoom room in prefabs)
				{
					if (room != null && !rooms.Contains(room))
					{
						rooms.Add(room);
					}
				}
			}
			existingRooms = rooms.AsReadOnly();
			Metrics = adapter.Metrics;
			Layers = adapter.Layers;
			Materials = adapter.Materials;
			Prefabs = adapter.Prefabs;
		}

		internal MapGameAdapter Adapter { get; }

		public IReadOnlyList<LevelConfig> ExistingLevels => existingLevels;

		public IReadOnlyList<GeneratedRoom> ExistingRooms => existingRooms;

		public LevelConfig DoorSource
		{
			get
			{
				foreach (LevelConfig level in existingLevels)
				{
					RoomPoolConfig pool = level == null ? null : level.procGenConfig;
					if (pool != null && (pool.ConnectedDoorPrefab != null || pool.SealedDoorPrefab != null || pool.ExitDoorPrefab != null))
					{
						return level;
					}
				}
				return existingLevels.Count > 0 ? existingLevels[0] : null;
			}
		}

		public MapMetrics Metrics { get; }

		public MapLayers Layers { get; }

		public MapMaterials Materials { get; }

		public MapPrefabs Prefabs { get; }
	}

	public sealed class MapMetrics
	{
		internal MapMetrics(float doorwayWidth, float doorwayHeight, bool hasDoorwaySize, Bounds exitDoorBounds, bool hasExitDoorBounds)
		{
			DoorwayWidth = doorwayWidth;
			DoorwayHeight = doorwayHeight;
			HasDoorwaySize = hasDoorwaySize;
			ExitDoorBounds = exitDoorBounds;
			HasExitDoorBounds = hasExitDoorBounds;
		}

		public float DoorwayWidth { get; }

		public float DoorwayHeight { get; }

		public bool HasDoorwaySize { get; }

		public Bounds ExitDoorBounds { get; }

		public bool HasExitDoorBounds { get; }
	}

	public sealed class MapLayers
	{
		internal MapLayers(int solid, int roomBounds)
		{
			Solid = solid;
			RoomBounds = roomBounds;
		}

		public int Solid { get; }

		public int RoomBounds { get; }
	}

	public sealed class GenerationContext
	{
		internal GenerationContext(MapEntry map, int seed, RoomLevelGenerator generator)
		{
			MapId = map.Id;
			Level = map.Level;
			Seed = seed;
			Generator = generator;
		}

		public string MapId { get; }

		public LevelConfig Level { get; }

		public int Seed { get; }

		public RoomLevelGenerator Generator { get; internal set; }

		public IReadOnlyList<GeneratedRoom> Rooms => Generator == null ? Array.Empty<GeneratedRoom>() : Generator.PlacedRooms;

		public IReadOnlyList<Doorway> ConnectedDoors => Generator == null ? Array.Empty<Doorway>() : Generator.ConnectedDoors;

		public IReadOnlyList<Doorway> OpenDoors => Generator == null ? Array.Empty<Doorway>() : Generator.OpenDoors;

		public System.Random Rng(string salt)
		{
			return new System.Random(StableHash.Combine(Seed, MapId, salt));
		}

		public System.Random RngForRoom(GeneratedRoom room, string salt)
		{
			int index = Generator == null || room == null ? -1 : Generator.PlacedRooms.IndexOf(room);
			return new System.Random(StableHash.Combine(Seed, MapId, (room == null ? string.Empty : room.name) + ":" + index + ":" + salt));
		}
	}

	internal sealed class MapUnavailableException : Exception
	{
		internal MapUnavailableException(string message) : base(message)
		{
		}
	}
}
