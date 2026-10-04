using System;
using System.Collections.Generic;
using Game.Levels;
using HarmonyLib;
using HaniUtils.RoomGen;
using Unity.AI.Navigation;
using UnityEngine;

namespace MapKit
{
	internal sealed class MapHooks
	{
		private readonly Dictionary<GenerationPhase, List<Action<GenerationContext>>> hooks = new Dictionary<GenerationPhase, List<Action<GenerationContext>>>();

		internal void Add(GenerationPhase phase, Action<GenerationContext> action)
		{
			if (!hooks.TryGetValue(phase, out List<Action<GenerationContext>> actions))
			{
				actions = new List<Action<GenerationContext>>();
				hooks.Add(phase, actions);
			}
			actions.Add(action);
		}

		internal void Invoke(GenerationPhase phase, GenerationContext context, string providerId)
		{
			if (!hooks.TryGetValue(phase, out List<Action<GenerationContext>> actions))
			{
				return;
			}
			foreach (Action<GenerationContext> action in actions)
			{
				try
				{
					action(context);
				}
				catch (Exception e)
				{
					MapKitRuntime.Log.LogError("[" + providerId + "] " + phase + " hook failed: " + e);
				}
			}
		}

		internal void Clear()
		{
			hooks.Clear();
		}
	}

	internal static class GenerationRuntime
	{
		private static RoomLevelGenerator subscribedGenerator;
		private static bool afterRooms;
		private static bool beforeNavMesh;

		internal static GenerationContext Current { get; private set; }
		internal static MapEntry CurrentMap { get; private set; }

		internal static void Initialize()
		{
			LevelManager.OnLevelGenerated -= Generated;
			LevelManager.OnLevelGenerated += Generated;
			LevelManager.OnLevelCleared -= Cleared;
			LevelManager.OnLevelCleared += Cleared;
		}

		internal static void Begin(LevelConfig level, int seed, RoomLevelGenerator generator)
		{
			DetachGenerator();
			if (!MapKitRuntime.TryMap(level, out MapEntry map))
			{
				Current = null;
				CurrentMap = null;
				return;
			}
			CurrentMap = map;
			Current = new GenerationContext(map, seed, generator);
			afterRooms = false;
			beforeNavMesh = false;
			subscribedGenerator = generator;
			if (subscribedGenerator != null)
			{
				subscribedGenerator.OnGenerationComplete += GeneratorComplete;
			}
			Invoke(GenerationPhase.BeforeGeneration);
		}

		internal static void BeforeNavMeshBuild()
		{
			if (Current == null)
			{
				return;
			}
			EnsureAfterRooms();
			if (!beforeNavMesh)
			{
				beforeNavMesh = true;
				Invoke(GenerationPhase.BeforeNavMesh);
			}
		}

		internal static void Reset()
		{
			DetachGenerator();
			LevelManager.OnLevelGenerated -= Generated;
			LevelManager.OnLevelCleared -= Cleared;
			Current = null;
			CurrentMap = null;
		}

		private static void GeneratorComplete(RoomLevelGenerator generator)
		{
			EnsureAfterRooms();
		}

		private static void Generated()
		{
			if (Current == null)
			{
				return;
			}
			EnsureAfterRooms();
			Invoke(GenerationPhase.AfterGeneration);
		}

		private static void Cleared()
		{
			if (Current != null)
			{
				Invoke(GenerationPhase.LevelCleared);
			}
			DetachGenerator();
			Current = null;
			CurrentMap = null;
		}

		private static void EnsureAfterRooms()
		{
			if (Current == null || afterRooms)
			{
				return;
			}
			afterRooms = true;
			Invoke(GenerationPhase.AfterRoomsPlaced);
		}

		private static void Invoke(GenerationPhase phase)
		{
			CurrentMap.Provider.Hooks.Invoke(phase, Current, CurrentMap.Provider.Id);
		}

		private static void DetachGenerator()
		{
			if (subscribedGenerator != null)
			{
				subscribedGenerator.OnGenerationComplete -= GeneratorComplete;
			}
			subscribedGenerator = null;
		}
	}

	[HarmonyPatch(typeof(LevelGenerationCoordinator), nameof(LevelGenerationCoordinator.GenerateForAllClientsRoutine))]
	internal static class HostSelectionPatch
	{
		[HarmonyPrefix]
		private static void Prefix()
		{
			MapKitRuntime.RefreshCompatibility(true);
		}
	}

	[HarmonyPatch(typeof(LevelManager), nameof(LevelManager.GenerateLevelRoutine))]
	internal static class GenerationStartPatch
	{
		[HarmonyPrefix]
		private static void Prefix(LevelManager __instance, int levelGenConfigIndex, int seed)
		{
			LevelConfig[] levels = MapKitRuntime.Registry?.LevelGenConfigs;
			LevelConfig level = levels != null && levelGenConfigIndex >= 0 && levelGenConfigIndex < levels.Length ? levels[levelGenConfigIndex] : null;
			GenerationRuntime.Begin(level, seed, __instance.procGen);
		}
	}

	[HarmonyPatch(typeof(NavMeshSurface), nameof(NavMeshSurface.BuildNavMesh))]
	internal static class BeforeNavMeshPatch
	{
		[HarmonyPrefix]
		private static void Prefix()
		{
			GenerationRuntime.BeforeNavMeshBuild();
		}
	}

	[HarmonyPatch(typeof(GeneratedRoom), nameof(GeneratedRoom.GetSpawnPoint))]
	internal static class SpawnPointPatch
	{
		[HarmonyPostfix]
		private static void Postfix(GeneratedRoom __instance, ref Transform __result)
		{
			MapKitSpawnPoints points = __instance.GetComponent<MapKitSpawnPoints>();
			if (points == null || points.Points == null || points.Points.Length < 2 || GenerationRuntime.Current == null)
			{
				return;
			}
			System.Random random = GenerationRuntime.Current.RngForRoom(__instance, "spawn");
			Transform selected = points.Points[random.Next(points.Points.Length)];
			if (selected != null)
			{
				__result = selected;
			}
		}
	}

	internal static class StableHash
	{
		internal static int Combine(int seed, string first, string second)
		{
			unchecked
			{
				uint hash = (uint)seed ^ 2166136261u;
				Add(ref hash, first);
				Add(ref hash, second);
				return (int)hash;
			}
		}

		private static void Add(ref uint hash, string value)
		{
			foreach (char character in value ?? string.Empty)
			{
				hash ^= character;
				hash *= 16777619u;
			}
		}
	}
}
