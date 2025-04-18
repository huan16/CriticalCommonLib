using System;
using System.Linq;
using System.Collections.Generic;
using CriticalCommonLib.MarketBoard;
using Dalamud.Game.Network.Structures;
using System.Linq.Expressions;

namespace CriticalCommonLib.MarketBoard;

public class MarketPricing : UniversalisApiResponse
{
    // 静态字典缓存属性访问器
    private static readonly Dictionary<string, 
        (Func<UniversalisApiResponse, object?> Getter, Action<MarketPricing, object?> Setter)> _propertyCache = new();

    // 静态构造函数初始化属性缓存
    static MarketPricing()
    {
        var universalisType = typeof(UniversalisApiResponse);
        foreach (var property in universalisType.GetProperties())
        {
            if (property.CanRead && property.CanWrite)
            {
                // 创建 getter 表达式
                var getterParam = Expression.Parameter(universalisType);
                var getterBody = Expression.Property(getterParam, property);
                var getter = Expression.Lambda<Func<UniversalisApiResponse, object?>>(
                    Expression.Convert(getterBody, typeof(object)),
                    getterParam).Compile();

                // 创建 setter 表达式
                var setterParam1 = Expression.Parameter(typeof(MarketPricing));
                var setterParam2 = Expression.Parameter(typeof(object));
                var setterBody = Expression.Call(
                    Expression.Convert(setterParam1, universalisType),
                    property.SetMethod,
                    Expression.Convert(setterParam2, property.PropertyType));
                var setter = Expression.Lambda<Action<MarketPricing, object?>>(setterBody, setterParam1, setterParam2).Compile();

                _propertyCache[property.Name] = (getter, setter);
            }
        }
    }

    public float MBMaxHQPrice { get; set; }
    public float MBMinHQPrice { get; set; }
    public float MBAvgHQPrice { get; set; }
    public float MBMaxNQPrice { get; set; }
    public float MBMinNQPrice { get; set; }
    public float MBAvgNQPrice { get; set; }
    public DateTime MBLastUpdate { get; set; } = DateTime.MinValue;
    public List<IMarketBoardItemListing> offerings { get; set; } = new List<IMarketBoardItemListing>();
    
    public DateTime UniversalisLastUpdate { get; set; } = DateTime.MinValue;
    public int Available { get; set; } = 0;

    // 从UniversalisApiResponse推荐的价格
    public uint? UniversalisRecdPrice { get; set; } = null;
    public uint? UniversalisRecdNQPrice { get; set; } = null;
    public uint? UniversalisRecdHQPrice { get; set; } = null;
    // 从交易板推荐的价格
    public uint? MBRecdPrice { get; set; } = null;
    public uint? MBRecdNQPrice { get; set; } = null;
    public uint? MBRecdHQPrice { get; set; } = null;

    // 创建空的市场价格对象
    public MarketPricing() : base()
    {
    }

    // 基于 UniversalisApiResponse 创建市场价格对象
    public MarketPricing(UniversalisApiResponse source) : base(source)
    {
        Available = listings?.Length ?? 0;
    }

    // 使用预缓存的属性访问器更新属性
    public void UpdateFromUniversalisResponse(UniversalisApiResponse source)
    {
        // 使用预缓存的访问器复制所有基类属性
        foreach (var (propertyName, (getter, setter)) in _propertyCache)
        {
            var value = getter(source);
            setter(this, value);
        }
        
        // 更新 MarketPricing 特有的属性
        Available = source.listings?.Length ?? 0;
        UniversalisLastUpdate = DateTime.Now;
    }
    
    // 从游戏市场板更新数据
    public void UpdateFromGameMB(List<IMarketBoardItemListing> currentOfferings)
    {
        MBLastUpdate = DateTime.Now;
        offerings = currentOfferings;
    }
}
