using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using Game.Levels;
using HaniUtils.Gameplay;
using HaniUtils.RoomGen;
using Unity.Netcode;
using UnityEngine;

namespace MapKit
{
	internal sealed class MapGameAdapter
	{
		private MapGameAdapter(MapMetrics metrics, MapLayers layers, MapMaterials materials, MapPrefabs prefabs, FieldInfo roomName, FieldInfo spawnPoint, FieldInfo zoneSize, FieldInfo zoneOffset, FieldInfo zoneWeight)
		{
			Metrics = metrics;
			Layers = layers;
			Materials = materials;
			Prefabs = prefabs;
			RoomName = roomName;
			SpawnPoint = spawnPoint;
			ZoneSize = zoneSize;
			ZoneOffset = zoneOffset;
			ZoneWeight = zoneWeight;
		}

		internal MapMetrics Metrics { get; }
		internal MapLayers Layers { get; }
		internal MapMaterials Materials { get; }
		internal MapPrefabs Prefabs { get; }
		internal FieldInfo RoomName { get; }
		internal FieldInfo SpawnPoint { get; }
		internal FieldInfo ZoneSize { get; }
		internal FieldInfo ZoneOffset { get; }
		internal FieldInfo ZoneWeight { get; }

		internal void Dispose()
		{
			Materials.Dispose();
		}

		internal static MapGameAdapter Bind(LevelConfig[] levels, ManualLogSource log)
		{
			LevelConfig source = levels.FirstOrDefault(level =>
			{
				RoomPoolConfig candidate = level == null ? null : level.procGenConfig;
				return candidate?.RoomPrefabs?.Any(room => room != null) == true
					&& (candidate.ConnectedDoorPrefab != null || candidate.ExitDoorPrefab != null);
			}) ?? levels.FirstOrDefault(level => level?.procGenConfig?.RoomPrefabs?.Any(room => room != null) == true);
			RoomPoolConfig pool = source == null ? null : source.procGenConfig;
			GeneratedRoom sample = pool?.RoomPrefabs?.FirstOrDefault(room => room != null);
			GameObject door = pool?.ConnectedDoorPrefab != null ? pool.ConnectedDoorPrefab : pool?.ExitDoorPrefab;
			Bounds doorBounds = door == null ? default : LocalBounds(door, door.transform);
			bool hasDoorwaySize = doorBounds.size.x > 0.1f && doorBounds.size.y > 0.1f;
			float doorWidth = hasDoorwaySize ? doorBounds.size.x : float.NaN;
			float doorHeight = hasDoorwaySize ? doorBounds.size.y : float.NaN;
			Bounds exitBounds = pool?.ExitDoorPrefab == null ? default : LocalBounds(pool.ExitDoorPrefab, pool.ExitDoorPrefab.transform);
			Collider solid = sample?.GetComponentsInChildren<Collider>(true).FirstOrDefault(collider => !collider.isTrigger && collider.GetComponent<Renderer>() != null);
			int solidLayer = solid == null ? 0 : solid.gameObject.layer;
			BoxCollider sampleBounds = sample?.Bounds != null ? sample.Bounds : sample?.GetComponentInChildren<BoxCollider>(true);
			int namedBounds = LayerMask.NameToLayer("RoomBounds");
			int boundsLayer = namedBounds >= 0 ? namedBounds : sampleBounds == null ? solidLayer : sampleBounds.gameObject.layer;
			Material sampleMaterial = sample?.GetComponentsInChildren<Renderer>(true).Select(renderer => renderer.sharedMaterial).FirstOrDefault(material => material != null);
			MapMetrics metrics = new MapMetrics(doorWidth, doorHeight, hasDoorwaySize, exitBounds, exitBounds.size.x > 0.1f && exitBounds.size.y > 0.1f);
			MapLayers layers = new MapLayers(solidLayer, boundsLayer);
			MapMaterials materials = new MapMaterials(sampleMaterial);
			MapPrefabs prefabs = new MapPrefabs();
			FieldInfo roomName = typeof(Room).GetField("roomName", BindingFlags.NonPublic | BindingFlags.Instance);
			FieldInfo spawnPoint = typeof(GeneratedRoom).GetField("SpawnPoint", BindingFlags.NonPublic | BindingFlags.Instance);
			FieldInfo zoneSize = typeof(ValuableSpawnZone).GetField("zoneSize", BindingFlags.NonPublic | BindingFlags.Instance);
			FieldInfo zoneOffset = typeof(ValuableSpawnZone).GetField("yOffset", BindingFlags.NonPublic | BindingFlags.Instance);
			FieldInfo zoneWeight = typeof(ValuableSpawnZone).GetField("weight", BindingFlags.NonPublic | BindingFlags.Instance);
			List<string> missing = new List<string>();
			if (roomName == null)
			{
				missing.Add("Room.roomName");
			}
			if (spawnPoint == null)
			{
				missing.Add("GeneratedRoom.SpawnPoint");
			}
			if (zoneSize == null)
			{
				missing.Add("ValuableSpawnZone.zoneSize");
			}
			if (zoneOffset == null)
			{
				missing.Add("ValuableSpawnZone.yOffset");
			}
			if (zoneWeight == null)
			{
				missing.Add("ValuableSpawnZone.weight");
			}
			if (missing.Count > 0)
			{
				log.LogError("Game bindings unavailable: " + string.Join(", ", missing) + ". Features using them will fail validation instead of silently misbehaving.");
			}
			string doorway = hasDoorwaySize ? $"{doorWidth:0.##} x {doorHeight:0.##} m" : "not measured";
			log.LogInfo($"Game adapter: doorway {doorway}, solid layer {solidLayer}, bounds layer {boundsLayer}.");
			return new MapGameAdapter(metrics, layers, materials, prefabs, roomName, spawnPoint, zoneSize, zoneOffset, zoneWeight);
		}

		internal static Bounds LocalBounds(GameObject part, Transform root)
		{
			bool hasBounds = false;
			Bounds result = default;
			foreach (MeshFilter filter in part.GetComponentsInChildren<MeshFilter>(true))
			{
				Add(filter.sharedMesh, filter.transform);
			}
			foreach (SkinnedMeshRenderer renderer in part.GetComponentsInChildren<SkinnedMeshRenderer>(true))
			{
				Add(renderer.sharedMesh, renderer.transform);
			}
			return result;

			void Add(Mesh mesh, Transform owner)
			{
				if (mesh == null)
				{
					return;
				}
				Matrix4x4 matrix = root.worldToLocalMatrix * owner.localToWorldMatrix;
				Bounds bounds = mesh.bounds;
				for (int corner = 0; corner < 8; corner++)
				{
					Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
					Vector3 local = matrix.MultiplyPoint3x4(point);
					if (!hasBounds)
					{
						result = new Bounds(local, Vector3.zero);
						hasBounds = true;
					}
					else
					{
						result.Encapsulate(local);
					}
				}
			}
		}
	}

	public sealed class MapMaterials
	{
		private readonly Material sample;
		private readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

		internal MapMaterials(Material sample)
		{
			this.sample = sample;
		}

		public Material Create(Color color, Color? emission = null)
		{
			Color glow = emission ?? Color.black;
			string key = ColorKey(color) + ":" + ColorKey(glow);
			if (cache.TryGetValue(key, out Material found) && found != null)
			{
				return found;
			}
			Material material;
			if (sample != null)
			{
				material = new Material(sample);
			}
			else
			{
				Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
				if (shader == null)
				{
					throw new InvalidOperationException("No compatible surface shader was found.");
				}
				material = new Material(shader);
			}
			material.name = "MapKit Surface";
			if (material.HasProperty("_BaseColor"))
			{
				material.SetColor("_BaseColor", color);
			}
			if (material.HasProperty("_Color"))
			{
				material.SetColor("_Color", color);
			}
			if (glow != Color.black && material.HasProperty("_EmissionColor"))
			{
				material.EnableKeyword("_EMISSION");
				material.SetColor("_EmissionColor", glow);
			}
			cache[key] = material;
			return material;
		}

		private static string ColorKey(Color value)
		{
			return value.r.ToString("R", CultureInfo.InvariantCulture) + ","
				+ value.g.ToString("R", CultureInfo.InvariantCulture) + ","
				+ value.b.ToString("R", CultureInfo.InvariantCulture) + ","
				+ value.a.ToString("R", CultureInfo.InvariantCulture);
		}

		internal void Dispose()
		{
			foreach (Material material in cache.Values)
			{
				MapKitRuntime.Destroy(material);
			}
			cache.Clear();
		}
	}

	public sealed class MapPrefabs
	{
		public GameObject CloneDecoration(GameObject source, Transform parent = null)
		{
			if (source == null)
			{
				throw new ArgumentNullException(nameof(source));
			}
			MapBuildScope scope = MapBuildScope.Require();
			GameObject clone = UnityEngine.Object.Instantiate(source, parent == null ? scope.Holder : parent, false);
			clone.name = source.name;
			foreach (Component component in clone.GetComponentsInChildren<Component>(true))
			{
				if (component == null || component is Transform || !UnsafeDecorationComponent(component))
				{
					continue;
				}
				UnityEngine.Object.DestroyImmediate(component);
			}
			return clone;
		}

		private static bool UnsafeDecorationComponent(Component component)
		{
			return component is NetworkBehaviour
				|| component is NetworkObject
				|| component is Room
				|| component is Doorway
				|| component is ValuableSpawnZone
				|| component is LevelPropSpawnPoint
				|| component is Rigidbody
				|| component is Joint;
		}
	}
}
