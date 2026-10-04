using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapKit
{
	internal sealed class MapBuildScope : IDisposable
	{
		[ThreadStatic]
		private static MapBuildScope active;

		private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
		private MapBuildScope previous;
		private bool disposed;

		internal MapBuildScope(MapProvider provider, MapContext context)
		{
			Provider = provider;
			Context = context;
			GameObject holder = new GameObject(provider.Id + " Templates");
			holder.SetActive(false);
			holder.hideFlags = HideFlags.HideAndDontSave;
			holder.transform.position = new Vector3(0f, -1000f, 0f);
			UnityEngine.Object.DontDestroyOnLoad(holder);
			Holder = holder.transform;
			owned.Add(holder);
		}

		internal MapProvider Provider { get; }

		internal MapContext Context { get; }

		internal MapGameAdapter Adapter => Context.Adapter;

		internal Transform Holder { get; }

		internal static MapBuildScope Require()
		{
			if (active == null)
			{
				throw new InvalidOperationException("Map construction must run inside MapKit.Register's build callback.");
			}
			return active;
		}

		internal static void Track(UnityEngine.Object value)
		{
			if (value != null)
			{
				Require().owned.Add(value);
			}
		}

		internal IDisposable Enter()
		{
			previous = active;
			active = this;
			return new Exit(this);
		}

		public void Dispose()
		{
			if (disposed)
			{
				return;
			}
			disposed = true;
			for (int i = owned.Count - 1; i >= 0; i--)
			{
				MapKitRuntime.Destroy(owned[i]);
			}
			owned.Clear();
		}

		private sealed class Exit : IDisposable
		{
			private MapBuildScope scope;

			internal Exit(MapBuildScope scope)
			{
				this.scope = scope;
			}

			public void Dispose()
			{
				if (scope == null)
				{
					return;
				}
				active = scope.previous;
				scope.previous = null;
				scope = null;
			}
		}
	}
}
