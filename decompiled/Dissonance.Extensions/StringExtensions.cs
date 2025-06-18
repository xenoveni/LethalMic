using JetBrains.Annotations;

namespace Dissonance.Extensions;

internal static class StringExtensions
{
	public static int GetFnvHashCode([CanBeNull] this string str)
	{
		if (str == null)
		{
			return 0;
		}
		uint num = 2166136261u;
		foreach (char num2 in str)
		{
			byte b = (byte)((int)num2 >> 8);
			byte b2 = (byte)num2;
			num ^= b;
			num *= 16777619;
			num ^= b2;
			num *= 16777619;
		}
		return (int)num;
	}
}
