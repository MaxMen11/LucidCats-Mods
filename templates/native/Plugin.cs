using System;
using System.IO;
using System.Runtime.InteropServices;
using BepInEx;

namespace ModName
{
	[BepInPlugin(ModMetadata.Guid, ModMetadata.Name, ModMetadata.Version)]
	public sealed partial class Plugin : BaseUnityPlugin
	{
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void LogFn(IntPtr message);

		[DllImport("kernel32", CharSet = CharSet.Unicode)]
		private static extern IntPtr LoadLibraryW(string path);

		[DllImport("ModNameNative", CallingConvention = CallingConvention.Cdecl)]
		private static extern void mod_init(LogFn log);

		[DllImport("ModNameNative", CallingConvention = CallingConvention.Cdecl)]
		private static extern void mod_update();

		[DllImport("ModNameNative", CallingConvention = CallingConvention.Cdecl)]
		private static extern void mod_shutdown();

		private static readonly LogFn NativeLogCallback = NativeLog;
		private static Plugin instance;
		private bool loaded;

		private static void NativeLog(IntPtr message)
		{
			instance?.Logger.LogInfo(Marshal.PtrToStringAnsi(message));
		}

		private void Awake()
		{
			instance = this;
			string directory = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
			if (LoadLibraryW(Path.Combine(directory, "ModNameNative.dll")) == IntPtr.Zero)
			{
				Logger.LogError("ModNameNative.dll could not be loaded.");
				return;
			}

			loaded = true;
			mod_init(NativeLogCallback);
			Logger.LogInfo($"{ModMetadata.Name} v{ModMetadata.Version} loaded.");
		}

		private void Update()
		{
			if (loaded)
			{
				mod_update();
			}
		}

		private void OnDestroy()
		{
			if (loaded)
			{
				mod_shutdown();
			}
		}
	}
}
