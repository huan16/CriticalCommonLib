using System.Collections.Generic;
using Newtonsoft.Json;

namespace CriticalCommonLib.MarketBoard
{
    public class MultiRequest
    {
        public string[] itemIDs { internal get; set; }
        public Dictionary<string, UniversalisApiResponse> items { internal get; set; }
    }

    public class UniversalisApiResponse
    {
        public uint itemID { get; set; }
        public uint worldID { get; set; }
        public long lastUploadTime { get; set; }
        [JsonProperty]
        public Listing[] listings { get; set; }
        [JsonProperty]
        public RecentHistory[] recentHistory { get; set; }
        
        // Price averages
        public float currentAveragePrice { get; set; }
        public float currentAveragePriceNQ { get; set; }
        public float currentAveragePriceHQ { get; set; }
        public float averagePrice { get; set; }
        public float averagePriceNQ { get; set; }
        public float averagePriceHQ { get; set; }
        
        // Min/max prices
        public float minPrice { get; set; }
        public float minPriceNQ { get; set; }
        public float minPriceHQ { get; set; }
        public float maxPrice { get; set; }
        public float maxPriceNQ { get; set; }
        public float maxPriceHQ { get; set; }
        
        // Sales velocity
        public float regularSaleVelocity { get; set; }
        public float nqSaleVelocity { get; set; }
        public float hqSaleVelocity { get; set; }
        
        // Histograms
        [JsonProperty]
        public Dictionary<string, int> stackSizeHistogram { get; set; }
        [JsonProperty]
        public Dictionary<string, int> stackSizeHistogramNQ { get; set; }
        [JsonProperty]
        public Dictionary<string, int> stackSizeHistogramHQ { get; set; }
        
        // Additional information
        public string worldName { get; set; }
        public int listingsCount { get; set; }
        public int recentHistoryCount { get; set; }
        public int unitsForSale { get; set; }
        public int unitsSold { get; set; }
        public bool hasData { get; set; }

        public UniversalisApiResponse()
        {
        }
        
        // 拷贝构造函数
        public UniversalisApiResponse(UniversalisApiResponse source)
        {
            itemID = source.itemID;
            worldID = source.worldID;
            lastUploadTime = source.lastUploadTime;
            listings = source.listings;
            recentHistory = source.recentHistory;
            currentAveragePrice = source.currentAveragePrice;
            currentAveragePriceNQ = source.currentAveragePriceNQ;
            currentAveragePriceHQ = source.currentAveragePriceHQ;
            averagePrice = source.averagePrice;
            averagePriceNQ = source.averagePriceNQ;
            averagePriceHQ = source.averagePriceHQ;
            minPrice = source.minPrice;
            minPriceNQ = source.minPriceNQ;
            minPriceHQ = source.minPriceHQ;
            maxPrice = source.maxPrice;
            maxPriceNQ = source.maxPriceNQ;
            maxPriceHQ = source.maxPriceHQ;
            regularSaleVelocity = source.regularSaleVelocity;
            nqSaleVelocity = source.nqSaleVelocity;
            hqSaleVelocity = source.hqSaleVelocity;
            stackSizeHistogram = source.stackSizeHistogram;
            stackSizeHistogramNQ = source.stackSizeHistogramNQ;
            stackSizeHistogramHQ = source.stackSizeHistogramHQ;
            worldName = source.worldName;
            listingsCount = source.listingsCount;
            recentHistoryCount = source.recentHistoryCount;
            unitsForSale = source.unitsForSale;
            unitsSold = source.unitsSold;
            hasData = source.hasData;
        }
    }


    public class Stacksizehistogram
    {
        public int _1 { get; set; }
    }

    public class Stacksizehistogramnq
    {
        public int _1 { get; set; }
    }

    public class Stacksizehistogramhq
    {
        public int _1 { get; set; }
    }

    public class Listing
    {

        public int lastReviewTime { get; set; }

        public int pricePerUnit { get; set; }

        public int quantity { get; set; }

        public int stainID { get; set; }

        public string creatorName { get; set; }

        public object creatorID { get; set; }

        public bool hq { get; set; }

        public bool isCrafted { get; set; }

        public object listingID { get; set; }

        public object[] materia { get; set; }

        public bool onMannequin { get; set; }

        public int retainerCity { get; set; }

        public string retainerID { get; set; }

        public string retainerName { get; set; }

        public string sellerID { get; set; }

        public int total { get; set; }
        public int tax { get; set; }
    }

    public class RecentHistory
    {
        public bool hq { get; set; }
        public int pricePerUnit { get; set; }
        public int quantity { get; set; }
        public long timestamp { get; set; }
        public bool onMannequin { get; set; }
        public string buyerName { get; set; }
        public int total { get; set; }
    }
}