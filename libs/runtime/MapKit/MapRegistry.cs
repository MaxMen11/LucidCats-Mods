using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace MapKit
{
	internal sealed class MapProvider
	{
		internal readonly string Id;
		internal readonly string Version;
		internal readonly string ContentHash;
		internal readonly Action<MapBuilder> Build;
		internal readonly MapHooks Hooks = new MapHooks();
		internal MapBuildScope Scope;
		internal IReadOnlyList<MapEntry> Levels = Array.Empty<MapEntry>();

		internal MapProvider(string id, string version, string contentHash, Action<MapBuilder> build)
		{
			Id = id;
			Version = version;
			ContentHash = contentHash;
			Build = build;
		}
	}

	internal sealed class MapEntry
	{
		internal MapEntry(MapProvider provider, string id, LevelConfig level)
		{
			Provider = provider;
			Id = id;
			Level = level;
		}

		internal MapProvider Provider { get; }
		internal string Id { get; }
		internal LevelConfig Level { get; }
	}

	internal static class MapKitRuntime
	{
		private static readonly SortedDictionary<string, MapProvider> Providers = new SortedDictionary<string, MapProvider>(StringComparer.Ordinal);
		private static readonly List<MapEntry> Maps = new List<MapEntry>();
		private static readonly Dictionary<LevelConfig, MapEntry> ByLevel = new Dictionary<LevelConfig, MapEntry>();
		private static ManualLogSource log;
		private static ConfigEntry<bool> logManifest;
		private static GameObject host;
		private static LevelConfigsRegistry registry;
		private static LevelConfig[] baseLevels = Array.Empty<LevelConfig>();
		private static MapGameAdapter adapter;
		private static bool initialized;
		private static bool rebuilding;

		internal static ManualLogSource Log => log;
		internal static LevelConfigsRegistry Registry => registry;

		internal static void Initialize(ManualLogSource logger, ConfigFile config, Harmony harmony)
		{
			if (initialized)
			{
				return;
			}
			initialized = true;
			log = logger;
			logManifest = config.Bind("Diagnostics", "LogManifest", false, "Log the ordered custom-map manifest whenever it changes.");
			host = new GameObject("MapKit Runtime");
			host.hideFlags = HideFlags.HideAndDontSave;
			UnityEngine.Object.DontDestroyOnLoad(host);
			host.AddComponent<MapKitRunner>();
			GenerationRuntime.Initialize();
			Type[] patches =
			{
				typeof(HostSelectionPatch),
				typeof(GenerationStartPatch),
				typeof(BeforeNavMeshPatch),
				typeof(SpawnPointPatch)
			};
			foreach (Type patch in patches)
			{
				try
				{
					harmony.CreateClassProcessor(patch).Patch();
				}
				catch (Exception e)
				{
					log.LogError("Patch '" + patch.Name + "' failed: " + e);
				}
			}
		}

		internal static void Register(string id, string version, string contentHash, Action<MapBuilder> build)
		{
			if (!MapIds.Valid(id))
			{
				throw new ArgumentException("A stable reverse-domain map provider ID is required.", nameof(id));
			}
			if (string.IsNullOrWhiteSpace(version) || version.Split('.').Length < 2 || !System.Version.TryParse(version, out _))
			{
				throw new ArgumentException("A numeric version with at least two parts is required.", nameof(version));
			}
			if (build == null)
			{
				throw new ArgumentNullException(nameof(build));
			}
			if (Providers.ContainsKey(id))
			{
				throw new InvalidOperationException("Map provider '" + id + "' is already registered.");
			}
			string hash = string.IsNullOrWhiteSpace(contentHash) ? ContentHash.For(build.Method.DeclaringType?.Assembly) : NormalizeHash(contentHash);
			Providers.Add(id, new MapProvider(id, version, hash, build));
			if (registry != null)
			{
				Rebuild(registry);
			}
			MapCompatibility.ManifestChanged();
		}

		internal static void Tick()
		{
			MapCompatibility.Tick();
			if (LevelConfigsRegistry.HasInstance)
			{
				LevelConfigsRegistry found = LevelConfigsRegistry.Instance;
				if (found != registry)
				{
					Rebuild(found);
				}
				else
				{
					SyncRegistry();
				}
			}
			else
			{
				registry = null;
				baseLevels = Array.Empty<LevelConfig>();
			}
			RefreshCompatibility(false);
		}

		internal static void Rebuild(LevelConfigsRegistry target)
		{
			if (target == null || rebuilding)
			{
				return;
			}
			rebuilding = true;
			try
			{
				LevelConfig[] existing = target.LevelGenConfigs ?? Array.Empty<LevelConfig>();
				HashSet<LevelConfig> oldMaps = new HashSet<LevelConfig>(Maps.Select(map => map.Level));
				baseLevels = existing.Where(level => level == null || !oldMaps.Contains(level)).ToArray();
				DestroyBuilds();
				adapter?.Dispose();
				adapter = MapGameAdapter.Bind(baseLevels, log);
				MapContext context = new MapContext(baseLevels, adapter);
				HashSet<string> mapIds = new HashSet<string>(StringComparer.Ordinal);
				foreach (MapProvider provider in Providers.Values)
				{
					BuildProvider(provider, context, mapIds);
				}
				Maps.Sort((left, right) => string.Compare(left.Id, right.Id, StringComparison.Ordinal));
				ByLevel.Clear();
				foreach (MapEntry map in Maps)
				{
					ByLevel.Add(map.Level, map);
				}
				registry = target;
				MapCompatibility.Invalidate();
				Apply(oldMaps);
				MapCompatibility.ManifestChanged();
				if (logManifest.Value)
				{
					log.LogInfo("Manifest: " + ManifestText());
				}
			}
			finally
			{
				rebuilding = false;
			}
		}

		private static void BuildProvider(MapProvider provider, MapContext context, HashSet<string> mapIds)
		{
			provider.Hooks.Clear();
			provider.Scope = new MapBuildScope(provider, context);
			try
			{
				using (provider.Scope.Enter())
				{
					MapBuilder builder = new MapBuilder(provider, context);
					provider.Build(builder);
					provider.Levels = builder.Finish();
				}
				foreach (MapEntry map in provider.Levels)
				{
					if (mapIds.Contains(map.Id))
					{
						throw new InvalidOperationException("Duplicate map ID '" + map.Id + "'.");
					}
				}
				foreach (MapEntry map in provider.Levels)
				{
					mapIds.Add(map.Id);
					Maps.Add(map);
				}
			}
			catch (MapUnavailableException e)
			{
				DisableProvider(provider);
				log.LogWarning("[" + provider.Id + "] unavailable: " + e.Message);
			}
			catch (Exception e)
			{
				DisableProvider(provider);
				log.LogError("[" + provider.Id + "] disabled: " + e);
			}
		}

		private static void DisableProvider(MapProvider provider)
		{
			provider.Levels = Array.Empty<MapEntry>();
			provider.Scope?.Dispose();
			provider.Scope = null;
		}

		internal static void RefreshCompatibility(bool finalHostCheck)
		{
			bool changed = MapCompatibility.Refresh(finalHostCheck);
			if (changed)
			{
				Apply();
			}
		}

		private static void SyncRegistry()
		{
			LevelConfig[] current = registry.LevelGenConfigs ?? Array.Empty<LevelConfig>();
			HashSet<LevelConfig> owned = new HashSet<LevelConfig>(Maps.Select(map => map.Level));
			LevelConfig[] observedBase = current.Where(level => level == null || !owned.Contains(level)).ToArray();
			List<LevelConfig> desired = new List<LevelConfig>(observedBase);
			if (MapCompatibility.CustomMapsAllowed)
			{
				desired.AddRange(Maps.Select(map => map.Level));
			}
			bool baseChanged = !baseLevels.SequenceEqual(observedBase);
			if (baseChanged)
			{
				baseLevels = observedBase;
			}
			if (!current.SequenceEqual(desired))
			{
				registry.LevelGenConfigs = desired.ToArray();
				registry.Reshuffle();
			}
			if (baseChanged)
			{
				MapCompatibility.ManifestChanged();
			}
		}

		private static void Apply(IEnumerable<LevelConfig> retired = null)
		{
			if (registry == null)
			{
				return;
			}
			HashSet<LevelConfig> owned = new HashSet<LevelConfig>(Maps.Select(map => map.Level));
			if (retired != null)
			{
				owned.UnionWith(retired);
			}
			LevelConfig[] current = registry.LevelGenConfigs ?? Array.Empty<LevelConfig>();
			baseLevels = current.Where(level => level == null || !owned.Contains(level)).ToArray();
			List<LevelConfig> desired = new List<LevelConfig>(baseLevels);
			if (MapCompatibility.CustomMapsAllowed)
			{
				desired.AddRange(Maps.Select(map => map.Level));
			}
			if (current.SequenceEqual(desired))
			{
				return;
			}
			registry.LevelGenConfigs = desired.ToArray();
			registry.Reshuffle();
		}

		internal static bool TryMap(LevelConfig level, out MapEntry map)
		{
			map = null;
			return level != null && ByLevel.TryGetValue(level, out map);
		}

		internal static string ManifestText()
		{
			StringBuilder text = new StringBuilder("api=").Append(global::MapKit.Maps.ApiVersion);
			foreach (LevelConfig level in baseLevels)
			{
				string name = level == null ? string.Empty : level.name;
				text.Append("|base:").Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(name)));
			}
			foreach (MapProvider provider in Providers.Values)
			{
				text.Append('|').Append(provider.Id).Append('@').Append(provider.Version).Append('#').Append(provider.ContentHash);
			}
			foreach (MapEntry map in Maps)
			{
				text.Append('|').Append(map.Id);
			}
			return text.ToString();
		}

		internal static void Shutdown()
		{
			if (!initialized)
			{
				return;
			}
			GenerationRuntime.Reset();
			MapCompatibility.Shutdown();
			DestroyBuilds();
			adapter?.Dispose();
			if (host != null)
			{
				Destroy(host);
			}
			host = null;
			registry = null;
			adapter = null;
			Providers.Clear();
			baseLevels = Array.Empty<LevelConfig>();
			initialized = false;
		}

		private static void DestroyBuilds()
		{
			Maps.Clear();
			ByLevel.Clear();
			foreach (MapProvider provider in Providers.Values)
			{
				provider.Levels = Array.Empty<MapEntry>();
				provider.Scope?.Dispose();
				provider.Scope = null;
			}
		}

		internal static void Destroy(UnityEngine.Object value)
		{
			if (value == null)
			{
				return;
			}
			if (Application.isPlaying)
			{
				UnityEngine.Object.Destroy(value);
			}
			else
			{
				UnityEngine.Object.DestroyImmediate(value);
			}
		}

		private static string NormalizeHash(string value)
		{
			string hash = value.Trim().ToLowerInvariant();
			if (hash.StartsWith("sha256:", StringComparison.Ordinal))
			{
				hash = hash.Substring(7);
			}
			if (hash.Length != 64 || hash.Any(c => !Uri.IsHexDigit(c)))
			{
				throw new ArgumentException("Content hash must be a SHA-256 hex string.", nameof(value));
			}
			return hash;
		}
	}

	internal sealed class MapKitRunner : MonoBehaviour
	{
		private float nextTick;

		private void Update()
		{
			if (Time.unscaledTime < nextTick)
			{
				return;
			}
			nextTick = Time.unscaledTime + 0.2f;
			MapKitRuntime.Tick();
		}
	}

	internal static class MapIds
	{
		internal static bool Valid(string value)
		{
			return ValidPart(value) && value.Contains(".");
		}

		internal static bool ValidPart(string value)
		{
			return !string.IsNullOrWhiteSpace(value) && value.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-');
		}
	}

	internal static class ContentHash
	{
		internal static string For(Assembly assembly)
		{
			if (assembly == null)
			{
				throw new InvalidOperationException("The map provider assembly could not be identified.");
			}
			try
			{
				using (FileStream stream = File.OpenRead(assembly.Location))
				using (SHA256 sha = SHA256.Create())
				{
					return Hex(sha.ComputeHash(stream));
				}
			}
			catch (Exception e)
			{
				throw new InvalidOperationException("Could not hash map provider assembly '" + assembly.FullName + "'.", e);
			}
		}

		private static string Hex(byte[] bytes)
		{
			StringBuilder text = new StringBuilder(bytes.Length * 2);
			foreach (byte value in bytes)
			{
				text.Append(value.ToString("x2"));
			}
			return text.ToString();
		}
	}
}
