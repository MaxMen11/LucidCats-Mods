using System;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TipJar
{
	internal sealed class TipJarPrefab : INetworkPrefabInstanceHandler, IDisposable
	{
		private const string StableId = ModMetadata.Guid + ".prefab.v2";

		private static readonly FieldInfo GlobalObjectIdHashField = typeof(NetworkObject).GetField(
			"GlobalObjectIdHash",
			BindingFlags.Instance | BindingFlags.NonPublic);

		private NetworkManager registeredManager;

		private TipJarPrefab(GameObject source)
		{
			Source = source;
		}

		public GameObject Source { get; }

		public static TipJarPrefab Create()
		{
			if (GlobalObjectIdHashField == null)
			{
				throw new MissingFieldException(typeof(NetworkObject).FullName, "GlobalObjectIdHash");
			}

			GameObject source = TipJarInteractable.BuildPrefab();
			source.SetActive(false);
			Object.DontDestroyOnLoad(source);
			GlobalObjectIdHashField.SetValue(source.GetComponent<NetworkObject>(), StableHash(StableId));
			return new TipJarPrefab(source);
		}

		public void Register(NetworkManager manager)
		{
			if (manager == registeredManager)
			{
				return;
			}

			Unregister();
			manager.AddNetworkPrefab(Source);
			manager.PrefabHandler.AddHandler(Source, this);
			registeredManager = manager;
		}

		public NetworkObject Instantiate(Vector3 position, Quaternion rotation)
		{
			GameObject clone = Object.Instantiate(Source, position, rotation);
			clone.SetActive(true);
			return clone.GetComponent<NetworkObject>();
		}

		public void Dispose()
		{
			Unregister();
			if (Source == null)
			{
				return;
			}

			foreach (Renderer renderer in Source.GetComponentsInChildren<Renderer>())
			{
				Object.Destroy(renderer.sharedMaterial);
			}
			Object.Destroy(Source);
		}

		NetworkObject INetworkPrefabInstanceHandler.Instantiate(
			ulong ownerClientId,
			Vector3 position,
			Quaternion rotation)
		{
			return Instantiate(position, rotation);
		}

		void INetworkPrefabInstanceHandler.Destroy(NetworkObject networkObject)
		{
			if (networkObject != null)
			{
				Object.Destroy(networkObject.gameObject);
			}
		}

		private void Unregister()
		{
			NetworkManager manager = registeredManager;
			registeredManager = null;
			if (manager == null)
			{
				return;
			}

			manager.PrefabHandler.RemoveHandler(Source);
			manager.RemoveNetworkPrefab(Source);
		}

		private static uint StableHash(string value)
		{
			unchecked
			{
				uint hash = 2166136261u;
				foreach (char character in value)
				{
					hash ^= character;
					hash *= 16777619u;
				}
				return hash == 0u ? 1u : hash;
			}
		}
	}
}
