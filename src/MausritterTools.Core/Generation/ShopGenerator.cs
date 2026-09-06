using MausritterTools.Core.Data;
using MausritterTools.Core.Model;
using MausritterTools.Core.Randomness;

namespace MausritterTools.Core.Generation;

/// <summary>
/// Populates a settlement with shops, their proprietors and their stock.
/// </summary>
/// <remarks>
/// Mausritter has no shops or services table, so which services exist, how many a settlement
/// supports and how they are gated by size are all house rules defined in
/// <c>wwwroot/data/house/services.json</c>. Each service records the SRD rule it was extrapolated
/// from so the UI can be honest about what is official and what is not.
/// </remarks>
public static class ShopGenerator
{
    /// <summary>Generates every shop in a settlement.</summary>
    public static IReadOnlyList<Shop> Generate(
        GameData data,
        RollContext context,
        int sizeValue,
        bool nearHumanTown)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<ServiceDefinition> available =
        [
            .. data.Services.Services.Where(s => s.IsAvailable(sizeValue, nearHumanTown))
        ];

        if (available.Count == 0)
        {
            return [];
        }

        int wanted = RollShopCount(data, context, sizeValue);
        IReadOnlyList<ServiceDefinition> chosen = ChooseServices(context, available, wanted);

        // Repeated quirks and sign adjectives inside one settlement read as sloppy, so they are
        // de-duplicated as shops are built. This makes a shop's quirk depend on the shops before
        // it, which is a deliberate trade: variety matters more here than absolute per-field
        // independence, and re-rolling an individual shop still behaves correctly.
        HashSet<string> usedQuirks = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> usedSigns = new(StringComparer.OrdinalIgnoreCase);

        List<Shop> shops = new(chosen.Count);
        for (int index = 0; index < chosen.Count; index++)
        {
            shops.Add(BuildShop(data, context, chosen[index], index, usedQuirks, usedSigns));
        }

        return shops;
    }

    private static int RollShopCount(GameData data, RollContext context, int sizeValue)
    {
        ShopCountRange? range = data.Services.CountForSize(sizeValue);
        if (range is null)
        {
            return 0;
        }

        if (context.TryGetPin("shops/count", out string pinned) &&
            int.TryParse(pinned, out int pinnedCount))
        {
            return Math.Max(0, pinnedCount);
        }

        DiceRoller dice = context.Dice("shops/count");
        return range.Min + dice.NextIndex(range.Max - range.Min + 1);
    }

    /// <summary>
    /// Picks which services are present, weighted, without repeating a service that does not allow
    /// duplicates.
    /// </summary>
    private static IReadOnlyList<ServiceDefinition> ChooseServices(
        RollContext context,
        IReadOnlyList<ServiceDefinition> available,
        int wanted)
    {
        DiceRoller dice = context.Dice("shops/selection");

        List<ServiceDefinition> pool = [.. available];
        List<ServiceDefinition> chosen = [];

        while (chosen.Count < wanted && pool.Count > 0)
        {
            ServiceDefinition service = dice.PickWeighted(pool, s => s.Weight);
            chosen.Add(service);

            if (!service.AllowDuplicates)
            {
                pool.Remove(service);
            }
        }

        // Keep a stable, readable order rather than the order they happened to be drawn in.
        return [.. chosen.OrderBy(s => s.MinSize).ThenBy(s => s.Name, StringComparer.Ordinal)];
    }

    private static Shop BuildShop(
        GameData data,
        RollContext context,
        ServiceDefinition service,
        int index,
        HashSet<string> usedQuirks,
        HashSet<string> usedSigns)
    {
        string path = $"shop/{index}";

        MouseNpc keeper = NpcGenerator.Generate(data, context, $"{path}/keeper");

        string sign = context.TryGetPin($"{path}/sign", out string pinnedSign)
            ? pinnedSign
            : PickDistinctly(
                usedSigns,
                attempt => NameForge.ShopSign(
                    context.Dice(Attempted($"{path}/sign", attempt)), data.Services, service, keeper.FamilyName),
                // Two signs sharing an adjective ("The Stubborn Quench" beside "The Stubborn
                // Pestle") is the collision worth avoiding, so compare on the leading words.
                SignFingerprint);

        string quirk = context.TryGetPin($"{path}/quirk", out string pinnedQuirk)
            ? pinnedQuirk
            : PickUnused(context.Dice($"{path}/quirk"), data.Services.ShopQuirks, usedQuirks);

        int adjustment = RollPriceAdjustment(data, context, service, $"{path}/prices");

        return new Shop
        {
            Id = path,
            Index = index + 1,
            Service = service,
            SignName = sign,
            Quirk = quirk,
            Keeper = keeper,
            PriceAdjustmentPercent = adjustment,
            Stock = BuildStock(data, context, service, adjustment, $"{path}/stock")
        };
    }

    /// <summary>Suffixes a path for a retry, so each attempt draws from a different stream.</summary>
    private static string Attempted(string path, int attempt) =>
        attempt == 0 ? path : $"{path}/retry{attempt}";

    /// <summary>
    /// Picks an entry that has not been used yet by drawing from the unused entries directly.
    /// </summary>
    /// <remarks>
    /// Preferred over retrying a blind roll, which can exhaust its attempts once most of a small
    /// table is spoken for. Falls back to the whole table when a settlement has more shops than
    /// the table has entries.
    /// </remarks>
    private static string PickUnused(DiceRoller dice, IReadOnlyList<string> table, HashSet<string> used)
    {
        if (table.Count == 0)
        {
            return "";
        }

        List<string> available = [.. table.Where(entry => !used.Contains(entry))];
        string picked = dice.Pick(available.Count > 0 ? available : table);

        used.Add(picked);
        return picked;
    }

    /// <summary>
    /// Rolls until the result is one not already used, giving up after several tries.
    /// </summary>
    /// <remarks>
    /// Used for shop signs, which are assembled from a pattern rather than drawn from a fixed
    /// list, so there is no finite pool to subtract from.
    /// </remarks>
    private static string PickDistinctly(
        HashSet<string> used,
        Func<int, string> roll,
        Func<string, string> fingerprint)
    {
        string value = "";

        for (int attempt = 0; attempt < 12; attempt++)
        {
            value = roll(attempt);
            if (used.Add(fingerprint(value)))
            {
                return value;
            }
        }

        return value;
    }

    /// <summary>
    /// Identifies a sign by everything but its final noun, so "The Stubborn Quench" and
    /// "The Stubborn Pestle" are treated as a clash.
    /// </summary>
    private static string SignFingerprint(string sign)
    {
        int lastSpace = sign.LastIndexOf(' ');
        return lastSpace <= 0 ? sign : sign[..lastSpace];
    }

    /// <summary>
    /// Combines the service's baseline price modifier with a per-shop drift, so the same item
    /// costs a little more at one shop than another.
    /// </summary>
    private static int RollPriceAdjustment(
        GameData data,
        RollContext context,
        ServiceDefinition service,
        string path)
    {
        int variance = Math.Abs(data.Services.PriceVariancePercent);
        if (variance == 0)
        {
            return service.PriceModifierPercent;
        }

        DiceRoller dice = context.Dice(path);
        int drift = dice.NextIndex((variance * 2) + 1) - variance;

        return service.PriceModifierPercent + drift;
    }

    private static IReadOnlyList<StockEntry> BuildStock(
        GameData data,
        RollContext context,
        ServiceDefinition service,
        int priceAdjustmentPercent,
        string path)
    {
        if (service.ServiceOnly || service.Stock.Categories.Count == 0)
        {
            return [];
        }

        // The category travels with each item so the card can show the right usage dots without
        // a fragile reverse lookup: a padlock, for instance, appears in two categories.
        List<(GearItem Item, string CategoryId)> pool = [];
        foreach (string categoryId in service.Stock.Categories)
        {
            if (data.Gear.FindCategory(categoryId) is { } category)
            {
                pool.AddRange(category.Items
                    .Where(service.Stock.Allows)
                    .Select(item => (item, category.Id)));
            }
        }

        if (pool.Count == 0)
        {
            return [];
        }

        DiceRoller dice = context.Dice(path);

        int span = Math.Max(0, service.Stock.MaxItems - service.Stock.MinItems);
        int wanted = service.Stock.MinItems + (span == 0 ? 0 : dice.NextIndex(span + 1));

        IReadOnlyList<(GearItem Item, string CategoryId)> items = dice.PickDistinct(pool, wanted);

        List<StockEntry> stock = new(items.Count);
        foreach ((GearItem item, string categoryId) in items.OrderBy(i => i.Item.DisplayName, StringComparer.Ordinal))
        {
            stock.Add(BuildStockEntry(data, dice, service, item, categoryId, priceAdjustmentPercent));
        }

        return stock;
    }

    private static StockEntry BuildStockEntry(
        GameData data,
        DiceRoller dice,
        ServiceDefinition service,
        GearItem item,
        string categoryId,
        int adjustmentPercent)
    {
        // Prices such as "x10p" for silvering and "10%" per repair dot are modifiers rather than
        // amounts, so they are passed through untouched instead of being scaled into nonsense.
        if (!item.HasNumericPrice)
        {
            return new StockEntry
            {
                Item = item,
                CategoryId = categoryId,
                Pips = null,
                PriceText = item.PriceText,
                Quantity = null
            };
        }

        int listed = item.Pips!.Value;
        int adjusted = Math.Max(1, (int)Math.Round(listed * (100 + adjustmentPercent) / 100.0, MidpointRounding.AwayFromZero));

        string priceText = item.PerUnit is not null
            ? $"{adjusted}p per {item.PerUnit}"
            : service.PricesPerHex
                ? $"{adjusted}p per hex"
                : $"{adjusted}p";

        return new StockEntry
        {
            Item = item,
            CategoryId = categoryId,
            Pips = adjusted,
            PriceText = priceText,
            Quantity = RollQuantity(data, dice, service, item, listed)
        };
    }

    /// <summary>
    /// Decides how many of an item a shop has.
    /// </summary>
    /// <remarks>
    /// For a hireling hall the count comes from the SRD's own Number column, which is the rule
    /// that makes skilled help scarce: a torchbearer rolls d6 but a blacksmith rolls d2. Ordinary
    /// goods fall back to stocking cheap things deeply and expensive things thinly, so a shop
    /// reads plausibly rather than offering six sets of heavy armour.
    /// </remarks>
    private static int RollQuantity(
        GameData data,
        DiceRoller dice,
        ServiceDefinition service,
        GearItem item,
        int listedPips)
    {
        if (service.OffersHirelings)
        {
            Hireling? hireling = data.Hirelings.Hirelings.FirstOrDefault(
                h => string.Equals(h.Name, item.Name, StringComparison.OrdinalIgnoreCase));

            if (hireling is not null && DiceExpression.TryRoll(dice, hireling.Number) is { } available)
            {
                return Math.Max(1, available);
            }
        }

        return listedPips switch
        {
            <= 20 => dice.Roll(6),
            <= 100 => dice.Roll(3),
            <= 500 => dice.Roll(2),
            _ => 1
        };
    }
}
