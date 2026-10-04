using System;
using Game.Interaction;
using Game.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace TipJar
{
	internal sealed class TipJarInteractable : BaseInteractable
	{
		private const float GlassRadius = 0.06f;
		private const float GlassHeight = 0.12f;
		private const float LidRadius = 0.062f;
		private const float LidThickness = 0.008f;
		private const float CoinsRadius = 0.058f;
		private const float MaxCoinsHeight = GlassHeight * 0.9f;
		private const string TransferMessage = ModMetadata.Guid + ".transfer.v2";

		private static readonly Color GlassColor = new Color(0.3f, 0.4f, 0.6f, 0.3f);
		private static readonly Color LidColor = new Color(0.2f, 0.3f, 0.5f);
		private static readonly Color CoinsColor = new Color(0.7f, 0.7f, 0.2f);

		private readonly NetworkVariable<int> balance = new NetworkVariable<int>(0);
		private readonly NetworkVariable<int> amount = new NetworkVariable<int>(Plugin.TransferAmount.Value);
		private readonly NetworkVariable<int> limit = new NetworkVariable<int>(Plugin.Capacity.Value);
		private readonly NetworkVariable<float> fee = new NetworkVariable<float>(Plugin.DepositFee.Value);

		private Transform coins;
		private Renderer coinsRenderer;
		private Collider jarCollider;
		private Renderer[] visualRenderers;
		private bool hidden;

		public static GameObject BuildPrefab()
		{
			Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
			if (shader == null)
			{
				throw new InvalidOperationException("No supported lit shader is available for the tip jar.");
			}

			var root = new GameObject("TipJar");
			root.layer = LayerMask.NameToLayer("Interactable");
			root.AddComponent<NetworkObject>();

			var collider = root.AddComponent<CapsuleCollider>();
			collider.height = GlassHeight;
			collider.radius = GlassRadius;
			collider.center = new Vector3(0f, GlassHeight * 0.5f, 0f);

			AddCylinder(root.transform, "Glass", GlassRadius, GlassHeight, 0f, GlassColor, shader, true);
			AddCylinder(root.transform, "Lid", LidRadius, LidThickness, GlassHeight, LidColor, shader, false);
			AddCylinder(root.transform, "Coins", CoinsRadius, 0f, 0f, CoinsColor, shader, false);
			root.AddComponent<TipJarInteractable>();
			return root;
		}

		internal void SetHostSettings(int transferAmount, int capacity, float feeRate)
		{
			amount.Value = Mathf.Max(1, transferAmount);
			limit.Value = Mathf.Max(amount.Value, capacity);
			fee.Value = Mathf.Max(0f, feeRate);
		}

		protected override void OnAwake()
		{
			coins = transform.Find("Coins");
			coinsRenderer = coins.GetComponent<Renderer>();
			jarCollider = GetComponent<Collider>();
			visualRenderers = GetComponentsInChildren<Renderer>();
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			if (IsServer)
			{
				SetInteractHoldDuration(0f);
				NetworkManager.CustomMessagingManager.RegisterNamedMessageHandler(TransferMessage, ReceiveTransfer);
			}

			balance.OnValueChanged += HandleBalanceChanged;
			amount.OnValueChanged += HandleAmountOrCapacityChanged;
			limit.OnValueChanged += HandleAmountOrCapacityChanged;
			fee.OnValueChanged += HandleFeeChanged;
			UpdateCoins(balance.Value);
		}

		public override void OnNetworkDespawn()
		{
			if (IsServer && NetworkManager != null)
			{
				NetworkManager.CustomMessagingManager.UnregisterNamedMessageHandler(TransferMessage);
			}

			balance.OnValueChanged -= HandleBalanceChanged;
			amount.OnValueChanged -= HandleAmountOrCapacityChanged;
			limit.OnValueChanged -= HandleAmountOrCapacityChanged;
			fee.OnValueChanged -= HandleFeeChanged;
			base.OnNetworkDespawn();
		}

		protected override string GetIdleText(PlayerManager player)
		{
			string deposit = fee.Value > 0f
				? $"{amount.Value} @{Mathf.RoundToInt(fee.Value * 100f)}%"
				: amount.Value.ToString();
			return $"Tip {deposit}\n[RMB/{Plugin.WithdrawKey.Value}] Take {amount.Value}";
		}

		protected override void OnInteract(PlayerManager player)
		{
			RequestTransfer(player, true);
		}

		private void Update()
		{
			PlayerManager player = LocalPlayerRegistry.HasInstance ? LocalPlayerRegistry.Instance.Current : null;
			bool shouldHide = player != null && player.SleepManager != null && player.SleepManager.IsDreaming;
			if (shouldHide != hidden)
			{
				hidden = shouldHide;
				jarCollider.enabled = !hidden;
				foreach (Renderer renderer in visualRenderers)
				{
					renderer.enabled = !hidden;
				}
				coinsRenderer.enabled = !hidden && balance.Value > 0;
			}

			if (player == null || player.InteractionManager == null || player.InteractionManager.Hovered != this)
			{
				return;
			}

			Mouse mouse = Mouse.current;
			Keyboard keyboard = Keyboard.current;
			Gamepad gamepad = Gamepad.current;
			bool withdraw = mouse != null && mouse.rightButton.wasPressedThisFrame
				|| keyboard != null && keyboard[Plugin.WithdrawKey.Value].wasPressedThisFrame
				|| gamepad != null && gamepad.buttonEast.wasPressedThisFrame;
			if (withdraw)
			{
				RequestTransfer(player, false);
			}
		}

		private void RequestTransfer(PlayerManager player, bool deposit)
		{
			if (IsServer)
			{
				ApplyTransfer(player, deposit);
				return;
			}

			using (var writer = new FastBufferWriter(sizeof(bool), Allocator.Temp))
			{
				writer.WriteValueSafe(deposit);
				NetworkManager.CustomMessagingManager.SendNamedMessage(
					TransferMessage,
					NetworkManager.ServerClientId,
					writer,
					NetworkDelivery.Reliable);
			}
		}

		private void ReceiveTransfer(ulong requesterId, FastBufferReader reader)
		{
			reader.ReadValueSafe(out bool deposit);
			NetworkObject playerObject = NetworkManager.SpawnManager.GetPlayerNetworkObject(requesterId);
			PlayerManager player = playerObject != null ? playerObject.GetComponent<PlayerManager>() : null;
			ApplyTransfer(player, deposit);
		}

		private void ApplyTransfer(PlayerManager player, bool deposit)
		{
			if (!CanTransfer(player))
			{
				return;
			}

			int transferAmount = amount.Value;
			if (deposit)
			{
				int cost = transferAmount + Mathf.RoundToInt(transferAmount * fee.Value);
				if (balance.Value > limit.Value - transferAmount || !player.Valuables.CanAfford(cost))
				{
					return;
				}

				player.Valuables.Credits.Value -= cost;
				balance.Value += transferAmount;
				return;
			}

			if (balance.Value < transferAmount)
			{
				return;
			}
			balance.Value -= transferAmount;
			player.Valuables.Credits.Value += transferAmount;
		}

		private bool CanTransfer(PlayerManager player)
		{
			if (!IsServer || player == null || player.Valuables == null || player.InteractionManager == null)
			{
				return false;
			}

			float range = player.InteractionManager.interactionRange;
			Vector3 nearestPoint = jarCollider.ClosestPoint(player.Position);
			return range > 0f && (nearestPoint - player.Position).sqrMagnitude <= range * range;
		}

		private void HandleBalanceChanged(int previous, int current)
		{
			UpdateCoins(current);
		}

		private void HandleAmountOrCapacityChanged(int previous, int current)
		{
			RaisePromptChanged();
			UpdateCoins(balance.Value);
		}

		private void HandleFeeChanged(float previous, float current)
		{
			RaisePromptChanged();
		}

		private void UpdateCoins(int currentBalance)
		{
			coinsRenderer.enabled = currentBalance > 0 && !hidden;
			if (currentBalance <= 0)
			{
				return;
			}

			float capacity = Mathf.Max(amount.Value, limit.Value);
			float fill = Mathf.Lerp(0.1f, 1f, Mathf.Clamp01(currentBalance / capacity));
			float height = fill * MaxCoinsHeight;
			coins.localScale = new Vector3(CoinsRadius * 2f, height * 0.5f, CoinsRadius * 2f);
			coins.localPosition = new Vector3(0f, height * 0.5f, 0f);
		}

		private static void AddCylinder(
			Transform parent,
			string name,
			float radius,
			float height,
			float bottom,
			Color color,
			Shader shader,
			bool transparent)
		{
			GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			cylinder.name = name;
			Collider primitiveCollider = cylinder.GetComponent<Collider>();
			primitiveCollider.enabled = false;
			Destroy(primitiveCollider);
			cylinder.transform.SetParent(parent, false);
			cylinder.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
			cylinder.transform.localPosition = new Vector3(0f, bottom + height * 0.5f, 0f);

			var material = new Material(shader) { color = color };
			if (transparent && material.HasProperty("_Surface"))
			{
				material.SetFloat("_Surface", 1f);
				material.SetOverrideTag("RenderType", "Transparent");
				material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
				material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
				material.SetInt("_ZWrite", 0);
				material.renderQueue = (int)RenderQueue.Transparent;
				material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
			}
			cylinder.GetComponent<Renderer>().sharedMaterial = material;
		}

		protected override void __initializeVariables()
		{
			balance.Initialize(this);
			NetworkVariableFields.Add(balance);
			amount.Initialize(this);
			NetworkVariableFields.Add(amount);
			limit.Initialize(this);
			NetworkVariableFields.Add(limit);
			fee.Initialize(this);
			NetworkVariableFields.Add(fee);
			base.__initializeVariables();
		}
	}
}
