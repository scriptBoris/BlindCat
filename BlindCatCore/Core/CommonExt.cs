using System.Collections;

namespace BlindCatCore.Core;

public static class CommonExt
{
    public static T Limitation<T>(this T self, T min, T max) where T : IComparable<T>
    {
        int checkMin = self.CompareTo(min);
        if (checkMin < 0)
            return min;

        int checkMax = self.CompareTo(max);
        if (checkMax > 0)
            return max;

        return self;
    }
}
