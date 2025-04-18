using System;
using Microsoft.Extensions.Hosting;

namespace CriticalCommonLib.MarketBoard
{
    public interface IUniversalis : IHostedService
    {
        public delegate void UniversalisResponseReceivedDelegate(UniversalisApiResponse response);

        public event UniversalisResponseReceivedDelegate? UniversalisResponseReceived;
        int QueuedCount { get; }
        void SetSaleHistoryLimit(int limit);
        void QueuePriceCheck(uint itemId, uint worldId);
        public DateTime? LastFailure { get; }
        public bool TooManyRequests { get; }
    }
}