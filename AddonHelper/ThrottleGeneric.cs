using ECommons.Throttlers;

namespace CriticalCommonLib.AddonHelper;

internal static class AllaganThrottle{
    internal static bool ThrottleGeneric(int num) => FrameThrottler.Throttle("AllaganMarketGenericThrottle", num, false);

    internal static bool ThrottleGeneric() => FrameThrottler.Throttle("AllaganMarketGenericThrottle", 5, false);

    internal static void RethrottleGeneric(int num)
    {
        FrameThrottler.Throttle("AllaganMarketGenericThrottle", num, true);
    }

    internal static void RethrottleGeneric()
    {
        FrameThrottler.Throttle("AllaganMarketGenericThrottle", 8, true);
    }
}