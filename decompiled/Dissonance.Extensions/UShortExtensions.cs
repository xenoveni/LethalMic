namespace Dissonance.Extensions;

internal static class UShortExtensions
{
	internal static int WrappedDelta2(this ushort a, ushort b)
	{
		return a.WrappedDelta(b, 2);
	}

	internal static int WrappedDelta7(this ushort a, ushort b)
	{
		return a.WrappedDelta(b, 7);
	}

	internal static int WrappedDelta16(this ushort a, ushort b)
	{
		return a.WrappedDelta(b, 16);
	}

	private static int WrappedDelta(this ushort a, ushort b, int bits)
	{
		int num = (1 << bits) - 1;
		int num2 = b - a;
		long num3 = (uint)num2 & num;
		int result = (int)num3;
		if (((uint)num2 & (1 << bits - 1)) != 0L)
		{
			result = -(int)(num - num3 + 1);
		}
		return result;
	}
}
