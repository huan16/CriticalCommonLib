using ECommons.Throttlers;

namespace CriticalCommonLib.AddonHelper;

internal static class AllaganThrottle{
    internal static bool ThrottleGeneric(int num)
    {
        return FrameThrottler.Throttle("AllaganMarketGenericThrottle", num, false);
    }

    internal static bool ThrottleGeneric(){
        return FrameThrottler.Throttle("AllaganMarketGenericThrottle", 5, false);
    }

    internal static bool RethrottleGeneric(int num)
    {
        return FrameThrottler.Throttle("AllaganMarketGenericThrottle", num, true);
    }

    internal static bool RethrottleGeneric()
    {
        return FrameThrottler.Throttle("AllaganMarketGenericThrottle", 8, true);
    }

    internal static bool RethrottleGeneric(string id, int num, bool isFrame)
    {
        if (isFrame)
        {
            return FrameThrottler.Throttle(id, 8, true);
        }
        else
        {
            return EzThrottler.Throttle(id, num, true);
        }
    }
}