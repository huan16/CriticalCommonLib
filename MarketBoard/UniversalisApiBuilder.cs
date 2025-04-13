using System;
using System.Collections.Generic;
using System.Linq;

namespace CriticalCommonLib.MarketBoard;

/// <summary>
/// Universalis API 请求构建器，提供链式方法配置请求参数
/// </summary>
public class UniversalisApiBuilder
{
    private const int MaxItems = 50;
    private const string DefaultUserAgent = "DalamudPlugin/1.0";
    
    private readonly List<uint> _itemIds = new();
    private readonly Dictionary<string, string> _headers = new();
    private readonly Dictionary<string, string> _queryParams = new();
    
    /// <summary>
    /// 初始化请求构建器
    /// </summary>
    /// <param name="worldDcRegion">服务器/数据中心/地区名称或ID</param>
    public UniversalisApiBuilder(string worldDcRegion)
    {
        if (string.IsNullOrWhiteSpace(worldDcRegion))
            throw new ArgumentException("区域名称不能为空", nameof(worldDcRegion));
        
        WorldDcRegion = Uri.EscapeDataString(worldDcRegion.Trim());
    }

    /// <summary>
    /// 区域路径参数（已编码）
    /// </summary>
    public string WorldDcRegion { get; }

    /// <summary>
    /// 添加单个物品ID
    /// </summary>
    public UniversalisApiBuilder AddItemId(uint itemId)
    {
        if (_itemIds.Count >= MaxItems)
            throw new InvalidOperationException($"最多只能添加 {MaxItems} 个物品ID");
        
        _itemIds.Add(itemId);
        return this;
    }

    /// <summary>
    /// 添加多个物品ID
    /// </summary>
    public UniversalisApiBuilder AddItemIds(IEnumerable<uint> itemIds)
    {
        var ids = itemIds.ToArray();
        if (_itemIds.Count + ids.Length > MaxItems)
            throw new InvalidOperationException($"超过最大数量限制 {MaxItems}");
        
        _itemIds.AddRange(ids);
        return this;
    }

    /// <summary>
    /// 设置返回的出售列表数量
    /// </summary>
    public UniversalisApiBuilder WithListings(int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        _queryParams["listings"] = count.ToString();
        return this;
    }

    /// <summary>
    /// 设置返回的历史记录数量
    /// </summary>
    public UniversalisApiBuilder WithEntries(int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        _queryParams["entries"] = count.ToString();
        return this;
    }

    /// <summary>
    /// 过滤高品质物品
    /// </summary>
    public UniversalisApiBuilder WithHq(bool hqOnly)
    {
        _queryParams["hq"] = hqOnly.ToString().ToLower();
        return this;
    }

    /// <summary>
    /// 设置统计数据时间范围（毫秒）
    /// </summary>
    public UniversalisApiBuilder WithStatsWithin(long milliseconds)
    {
        if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
        _queryParams["statsWithin"] = milliseconds.ToString();
        return this;
    }

    /// <summary>
    /// 设置历史记录时间范围（秒）
    /// </summary>
    public UniversalisApiBuilder WithEntriesWithin(long seconds)
    {
        if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        _queryParams["entriesWithin"] = seconds.ToString();
        return this;
    }

    /// <summary>
    /// 设置返回字段白名单
    /// </summary>
    public UniversalisApiBuilder WithFields(params string[] fields)
    {
        _queryParams["fields"] = string.Join(",", fields.Where(f => !string.IsNullOrWhiteSpace(f)));
        return this;
    }

    /// <summary>
    /// 设置自定义 User-Agent
    /// </summary>
    public UniversalisApiBuilder WithUserAgent(string userAgent)
    {
        _headers["User-Agent"] = userAgent;
        return this;
    }

    /// <summary>
    /// 构建最终请求
    /// </summary>
    /// <returns>包含完整URL和请求头的元组</returns>
    public (string Url, Dictionary<string, string> Headers) Build()
    {
        if (_itemIds.Count == 0)
            throw new InvalidOperationException("至少需要指定一个物品ID");
        
        // 构造物品ID路径部分
        var itemIdsSegment = string.Join(",", _itemIds.Distinct());
        
        // 构造查询参数
        var queryString = string.Join("&", _queryParams
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        
        // 构造完整URL
        var url = $"https://universalis.app/api/v2/{WorldDcRegion}/{itemIdsSegment}";
        if (!string.IsNullOrEmpty(queryString))
            url += $"?{queryString}";
        
        // 设置默认User-Agent
        if (!_headers.ContainsKey("User-Agent"))
            _headers["User-Agent"] = DefaultUserAgent;

        return (url, new Dictionary<string, string>(_headers));
    }
}