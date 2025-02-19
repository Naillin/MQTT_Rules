using System.Runtime.InteropServices;

namespace MQTT_Rules
{
	// Класс для обработки сигналов в Linux
	internal static class UnixSignalHandler
	{
		[DllImport("libc", SetLastError = true)]
		private static extern IntPtr signal(int signum, IntPtr handler);

		public static void Register(Signum signum, Action handler)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				signal((int)signum, Marshal.GetFunctionPointerForDelegate(handler));
			}
		}
	}

	// Перечисление сигналов
	internal enum Signum
	{
		SIGTERM = 15
	}
}
