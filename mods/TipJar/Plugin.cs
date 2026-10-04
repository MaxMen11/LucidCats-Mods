using BepInEx;
using BepInEx.Configuration;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TipJar
{
	[BepInPlugin(ModMetadata.Guid, ModMetadata.Name, ModMetadata.Version)]
	public sealed class Plugin : BaseUnityPlugin
	{
		private const string GameSceneName = "GameScene";

		private static readonly Vector3 JarPosition = new Vector3(-4.14499f, 0.8300774f, -5.876164f);

		internal static ConfigEntry<int> TransferAmount { get; private set; }
		internal static ConfigEntry<int> Capacity { get; private set; }
		internal static ConfigEntry<float> DepositFee { get; private set; }
		internal static ConfigEntry<Key> WithdrawKey { get; private set; }

		private TipJarPrefab prefab;
		private NetworkManager registeredManager;
		private TipJarInteractable jar;

		private void Awake()
		{
			TransferAmount = Config.Bind(
				"Economy",
				"TransferAmount",
				100,
				new ConfigDescription("Credits moved per interaction.", new AcceptableValueRange<int>(1, 1000)));
			Capacity = Config.Bind(
				"Economy",
				"Capacity",
				100000,
				new ConfigDescription("Maximum credits held by the jar.", new AcceptableValueRange<int>(1000, 1000000)));
			DepositFee = Config.Bind(
				"Economy",
				"DepositFee",
				0.1f,
				new ConfigDescription("Extra fraction charged on each deposit.", new AcceptableValueRange<float>(0f, 3f)));
			WithdrawKey = Config.Bind(
				"Controls",
				"WithdrawKey",
				Key.Q,
				"Keyboard key used to withdraw while looking at the jar. Right mouse and gamepad East/B also work.");

			TransferAmount.SettingChanged += HandleHostSettingChanged;
			Capacity.SettingChanged += HandleHostSettingChanged;
			DepositFee.SettingChanged += HandleHostSettingChanged;
			prefab = TipJarPrefab.Create();

			Logger.LogInfo($"{ModMetadata.Name} v{ModMetadata.Version} loaded.");
		}

		private void Update()
		{
			NetworkManager manager = NetworkManager.Singleton;
			if (manager == null)
			{
				return;
			}

			if (manager != registeredManager)
			{
				RegisterWith(manager);
			}

			if (!manager.IsListening || !manager.IsServer || manager.ShutdownInProgress)
			{
				return;
			}
			if (SceneManager.GetActiveScene().name != GameSceneName || jar != null && jar.IsSpawned)
			{
				return;
			}

			if (jar != null)
			{
				Destroy(jar.gameObject);
			}

			NetworkObject instance = prefab.Instantiate(JarPosition, Quaternion.identity);
			jar = instance.GetComponent<TipJarInteractable>();
			instance.Spawn();
			ApplyHostSettings();
			Logger.LogInfo("Spawned the lobby tip jar.");
		}

		private void OnDestroy()
		{
			TransferAmount.SettingChanged -= HandleHostSettingChanged;
			Capacity.SettingChanged -= HandleHostSettingChanged;
			DepositFee.SettingChanged -= HandleHostSettingChanged;

			if (jar != null && jar.IsSpawned && jar.IsServer)
			{
				jar.NetworkObject.Despawn(true);
			}
			jar = null;

			prefab?.Dispose();
			prefab = null;
			registeredManager = null;
		}

		private void RegisterWith(NetworkManager manager)
		{
			prefab.Register(manager);
			registeredManager = manager;
			jar = null;
		}

		private void HandleHostSettingChanged(object sender, System.EventArgs args)
		{
			ApplyHostSettings();
		}

		private void ApplyHostSettings()
		{
			if (jar != null && jar.IsSpawned && jar.IsServer)
			{
				jar.SetHostSettings(TransferAmount.Value, Capacity.Value, DepositFee.Value);
			}
		}
	}
}
