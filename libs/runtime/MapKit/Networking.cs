using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Collections;
using Unity.Netcode;

namespace MapKit
{
	internal static class MapCompatibility
	{
		private const string Message = "lucidcats.mapkit.compat.v1";
		private const byte Announce = 1;
		private const byte Relay = 2;
		private const int MaxManifestBytes = 32768;
		private static readonly Dictionary<ulong, string> PeerManifests = new Dictionary<ulong, string>();
		private static NetworkManager network;
		private static bool listening;
		private static bool customMapsAllowed = true;
		private static string lastReason = string.Empty;

		internal static bool CustomMapsAllowed => customMapsAllowed;

		internal static void Tick()
		{
			NetworkManager found = NetworkManager.Singleton;
			if (found != network)
			{
				Unhook();
				if (found != null)
				{
					Hook(found);
				}
			}
			if (network != null && network.IsListening != listening)
			{
				if (network.IsListening)
				{
					StartSession();
				}
				else
				{
					EndSession();
				}
			}
		}

		internal static bool Refresh(bool finalHostCheck)
		{
			Tick();
			bool allowed = Compute(out string reason);
			bool changed = allowed != customMapsAllowed;
			if (changed || finalHostCheck && reason != lastReason)
			{
				MapKitRuntime.Log.LogInfo(allowed
					? "Compatibility confirmed: custom maps enabled."
					: "Custom maps disabled: " + reason);
			}
			customMapsAllowed = allowed;
			lastReason = reason;
			return changed;
		}

		internal static void Invalidate()
		{
			if (network != null && network.IsListening && network.ConnectedClientsIds != null && network.ConnectedClientsIds.Count > 1)
			{
				customMapsAllowed = false;
				lastReason = "waiting for updated manifests";
			}
		}

		internal static void ManifestChanged()
		{
			if (network == null || !listening)
			{
				return;
			}
			PeerManifests[network.LocalClientId] = MapKitRuntime.ManifestText();
			AnnounceLocal();
			MapKitRuntime.RefreshCompatibility(false);
		}

		internal static void Shutdown()
		{
			Unhook();
			customMapsAllowed = true;
			lastReason = string.Empty;
		}

		private static void Hook(NetworkManager manager)
		{
			network = manager;
			manager.OnClientConnectedCallback += Connected;
			manager.OnClientDisconnectCallback += Disconnected;
			if (manager.IsListening)
			{
				StartSession();
			}
		}

		private static void StartSession()
		{
			try
			{
				listening = true;
				PeerManifests.Clear();
				PeerManifests[network.LocalClientId] = MapKitRuntime.ManifestText();
				customMapsAllowed = network.ConnectedClientsIds == null || network.ConnectedClientsIds.Count <= 1;
				lastReason = string.Empty;
				network.CustomMessagingManager?.RegisterNamedMessageHandler(Message, Receive);
				AnnounceLocal();
			}
			catch (Exception e)
			{
				MapKitRuntime.Log.LogError("Network setup failed: " + e);
			}
		}

		private static void EndSession()
		{
			network?.CustomMessagingManager?.UnregisterNamedMessageHandler(Message);
			listening = false;
			PeerManifests.Clear();
			customMapsAllowed = true;
			lastReason = string.Empty;
		}

		private static void Unhook()
		{
			if (network != null)
			{
				if (listening)
				{
					EndSession();
				}
				network.OnClientConnectedCallback -= Connected;
				network.OnClientDisconnectCallback -= Disconnected;
			}
			network = null;
			listening = false;
			PeerManifests.Clear();
		}

		private static void Connected(ulong clientId)
		{
			if (network == null)
			{
				return;
			}
			PeerManifests.Remove(clientId);
			if (clientId == network.LocalClientId)
			{
				PeerManifests[clientId] = MapKitRuntime.ManifestText();
				AnnounceLocal();
			}
			else if (network.IsServer)
			{
				foreach (KeyValuePair<ulong, string> known in PeerManifests)
				{
					SendRelay(clientId, known.Key, known.Value);
				}
			}
			MapKitRuntime.RefreshCompatibility(false);
		}

		private static void Disconnected(ulong clientId)
		{
			PeerManifests.Remove(clientId);
			MapKitRuntime.RefreshCompatibility(false);
		}

		private static void AnnounceLocal()
		{
			if (network == null || !network.IsListening || network.CustomMessagingManager == null)
			{
				return;
			}
			string manifest = MapKitRuntime.ManifestText();
			PeerManifests[network.LocalClientId] = manifest;
			if (network.IsServer)
			{
				foreach (ulong peer in network.ConnectedClientsIds)
				{
					if (peer != network.LocalClientId)
					{
						SendRelay(peer, network.LocalClientId, manifest);
					}
				}
			}
			else
			{
				SendAnnounce(NetworkManager.ServerClientId, manifest);
			}
		}

		private static void SendAnnounce(ulong target, string manifest)
		{
			using (FastBufferWriter writer = WriterFor(manifest, 1))
			{
				writer.WriteValueSafe(Announce);
				writer.WriteValueSafe(manifest, true);
				network.CustomMessagingManager.SendNamedMessage(Message, target, writer, NetworkDelivery.ReliableSequenced);
			}
		}

		private static void SendRelay(ulong target, ulong origin, string manifest)
		{
			using (FastBufferWriter writer = WriterFor(manifest, 9))
			{
				writer.WriteValueSafe(Relay);
				writer.WriteValueSafe(origin);
				writer.WriteValueSafe(manifest, true);
				network.CustomMessagingManager.SendNamedMessage(Message, target, writer, NetworkDelivery.ReliableSequenced);
			}
		}

		private static FastBufferWriter WriterFor(string manifest, int overhead)
		{
			int bytes = Encoding.UTF8.GetByteCount(manifest);
			if (bytes > MaxManifestBytes)
			{
				throw new InvalidOperationException("Map manifest exceeds " + MaxManifestBytes + " bytes.");
			}
			return new FastBufferWriter(bytes + overhead + 8, Allocator.Temp);
		}

		private static void Receive(ulong sender, FastBufferReader reader)
		{
			try
			{
				if (network == null || reader.Length > MaxManifestBytes + 32)
				{
					return;
				}
				reader.ReadValueSafe(out byte kind);
				if (kind == Announce && network.IsServer && sender != network.LocalClientId)
				{
					reader.ReadValueSafe(out string manifest, true);
					if (!ValidManifest(manifest))
					{
						return;
					}
					PeerManifests[sender] = manifest;
					foreach (ulong peer in network.ConnectedClientsIds)
					{
						if (peer != network.LocalClientId && peer != sender)
						{
							SendRelay(peer, sender, manifest);
						}
					}
				}
				else if (kind == Relay && !network.IsServer && sender == NetworkManager.ServerClientId)
				{
					reader.ReadValueSafe(out ulong origin);
					reader.ReadValueSafe(out string manifest, true);
					if (ValidManifest(manifest))
					{
						PeerManifests[origin] = manifest;
					}
				}
				MapKitRuntime.RefreshCompatibility(false);
			}
			catch (Exception e)
			{
				MapKitRuntime.Log.LogWarning("Ignored malformed compatibility message: " + e.Message);
			}
		}

		private static bool Compute(out string reason)
		{
			if (network == null || !network.IsListening || network.ConnectedClientsIds == null || network.ConnectedClientsIds.Count <= 1)
			{
				reason = "solo session";
				return true;
			}
			string local = MapKitRuntime.ManifestText();
			foreach (ulong peer in network.ConnectedClientsIds)
			{
				if (peer == network.LocalClientId)
				{
					continue;
				}
				if (!PeerManifests.TryGetValue(peer, out string remote))
				{
					reason = "waiting for player " + peer + " to report its MapKit manifest";
					return false;
				}
				if (!string.Equals(local, remote, StringComparison.Ordinal))
				{
					reason = Difference(local, remote, peer);
					return false;
				}
			}
			reason = "all connected players match";
			return true;
		}

		private static bool ValidManifest(string manifest)
		{
			return manifest != null
				&& manifest.All(character => character <= 127)
				&& Encoding.UTF8.GetByteCount(manifest) <= MaxManifestBytes
				&& manifest.StartsWith("api=", StringComparison.Ordinal);
		}

		private static string Difference(string local, string remote, ulong peer)
		{
			string[] ours = local.Split('|');
			string[] theirs = remote.Split('|');
			int count = Math.Max(ours.Length, theirs.Length);
			for (int i = 0; i < count; i++)
			{
				string left = i < ours.Length ? ours[i] : "<missing>";
				string right = i < theirs.Length ? theirs[i] : "<missing>";
				if (!string.Equals(left, right, StringComparison.Ordinal))
				{
					return "player " + peer + " differs at '" + left + "' versus '" + right + "'";
				}
			}
			return "player " + peer + " has a different manifest";
		}
	}

}
