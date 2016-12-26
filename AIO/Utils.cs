using System;
using System.IO;
using TShockAPI;

namespace AIO
{
	public static class Utils
	{
		private static string filepath = Path.Combine("tshock", "logs");

		public static string GetPath()
		{
			return Path.Combine(filepath, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
		}

		public static string Specifier(this bool silent)
		{
			if (silent)
				return TShock.Config.CommandSilentSpecifier;
			else
				return TShock.Config.CommandSpecifier;
		}
	}
}
