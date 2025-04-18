using System.Collections.Generic;
using Dalamud.Game.Network.Structures;

namespace CriticalCommonLib.Services.Mediator;

public record PluginLoadedMessage : MessageBase;
public record ConfigurationModifiedMessage() : MessageBase;

public record MarketCacheUpdatedMessage(uint itemId, uint worldId) : MessageBase;
public record MarketPricingUpdatedMessage(uint itemId, uint worldId) : MessageBase;
public record MarketRequestItemUpdateMessage(uint itemId) : MessageBase;
public record MarketRequestItemWorldUpdateMessage(uint itemId, uint worldId) : MessageBase;

public record MarketBoardOfferingsProcessedMessage(
    IReadOnlyList<IMarketBoardItemListing> Listings
) : MessageBase;
