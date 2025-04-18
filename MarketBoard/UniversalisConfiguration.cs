namespace CriticalCommonLib.MarketBoard;

public class UniversalisConfiguration
{
    public long StatsWithin { get; set; } = 4 * 604800000L;
    public int ListingsCount { get; set; } = 30;
    public int EntrisCount { get; set; } = 30;
    public long EntrisWithin { get; set; } = 4 * 604800000L;
    public string[]? ExcludeFields { get; set; } = null;
}