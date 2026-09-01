using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;

namespace CombatSolver;

internal static class PotionUsePolicy
{
    public const decimal AmbergrisMinimumHpSavedFraction = 0.40m;

    public static int RequiredHpSaved(int potionCount)
        => potionCount * SolverWeights.PotionMinimumHpSaved;

    public static int AdditionalRequiredUseStrategicHpCost(int strategicHpCost)
        => Math.Max(0, strategicHpCost - SolverWeights.PotionMinimumHpSaved);

    /// <summary>
    /// Potions the solver must be clearly rewarded for spending, because their effect is hard to replace.
    /// </summary>
    /// <remarks>
    /// Ambergris is deliberately absent: <see cref="MeetsAmbergrisRestriction"/> already holds it to a far higher
    /// bar (a fraction of maximum HP), and that calculation is written against the baseline cost.
    /// </remarks>
    private static readonly HashSet<string> HighValuePotionIds =
    [
        "GLOWWATER_POTION",
        "SWIFT_POTION",
        "GAMBLERS_BREW",
        "DUPLICATOR",
        "OROBIC_ACID",
        "POT_OF_GHOULS",
    ];

    private static readonly HashSet<string> ElevatedValuePotionIds =
    [
        "DISTILLED_CHAOS",
        "CLARITY",
        "RADIANT_TINCTURE",
        "CURE_ALL",
        "LIQUID_MEMORIES",
        "BOTTLED_POTENTIAL",
        "TOUCH_OF_INSANITY",
    ];

    /// <summary>
    /// The HP a route must save to justify spending this potion. Token potions are free to spend; everything else
    /// costs at least the baseline, and the two value tiers cost more so a cheap potion is spent before a scarce one.
    /// </summary>
    public static int StrategicHpCost(PotionModel potion)
    {
        if (potion.Rarity == PotionRarity.Token)
            return 0;
        string id = potion.Id.Entry;
        if (HighValuePotionIds.Contains(id))
            return SolverWeights.PotionHighValueHpSaved;
        if (ElevatedValuePotionIds.Contains(id))
            return SolverWeights.PotionElevatedValueHpSaved;
        return SolverWeights.PotionMinimumHpSaved;
    }

    public static bool RequiresOpeningUse(PotionModel potion)
        => potion is DexterityPotion
            or FocusPotion
            or FyshOil
            or LiquidBronze
            or MazalethsGift
            or PotionOfCapacity
            or SoldiersStew
            or StrengthPotion;

    public static int StrategicHpCost(string potionId)
    {
        PotionModel potion = ModelDb.AllPotions.Single(candidate =>
            candidate.Id.Entry.Equals(potionId, StringComparison.Ordinal));
        return StrategicHpCost(potion);
    }

    public static int HpSaved(int potionFreeHpDeficit, int potionRouteHpDeficit)
        => Math.Max(0, potionFreeHpDeficit - potionRouteHpDeficit);

    public static int AmbergrisRequiredHpSaved(int maximumHp)
        => (int)Math.Ceiling(maximumHp * AmbergrisMinimumHpSavedFraction);

    public static int EffectiveStrategicHpCost(
        int strategicHpCost,
        int ambergrisCount,
        int maximumHp)
        => strategicHpCost + ambergrisCount
            * (AmbergrisRequiredHpSaved(maximumHp) - SolverWeights.PotionMinimumHpSaved);

    public static bool MeetsAmbergrisRestriction(
        bool hasPotionFreeBaseline,
        int ambergrisCount,
        int strategicHpCost,
        int maximumHp,
        int potionFreePlayerHp,
        int potionRoutePlayerHp)
    {
        if (ambergrisCount == 0)
            return true;
        if (!hasPotionFreeBaseline)
            return false;
        int required = EffectiveStrategicHpCost(strategicHpCost, ambergrisCount, maximumHp);
        return Math.Max(0, potionRoutePlayerHp - potionFreePlayerHp) >= required;
    }

    public static bool IsEligible(
        SolverPotionPolicy policy,
        int potionCount,
        int automaticPotionCount,
        int strategicHpCost,
        bool potionFreeWon,
        int potionFreeHpDeficit,
        bool anyRouteWon,
        bool potionRouteWon,
        int potionRouteHpDeficit)
        => policy switch
        {
            SolverPotionPolicy.Disabled => potionCount == automaticPotionCount,
            SolverPotionPolicy.RequireAtLeastOne => potionCount > 0
                && (!anyRouteWon || potionRouteWon)
                && (potionCount == 1
                    || !potionFreeWon
                    || HpSaved(potionFreeHpDeficit, potionRouteHpDeficit)
                        >= AdditionalRequiredUseStrategicHpCost(strategicHpCost)),
            SolverPotionPolicy.Smart => potionCount == 0
                || potionRouteWon && !potionFreeWon
                || HpSaved(potionFreeHpDeficit, potionRouteHpDeficit) >= strategicHpCost,
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };
}

internal readonly record struct PotionFreePolicyBaseline(
    bool Won,
    int HpDeficit,
    int PlayerHp);

internal sealed class PotionPolicyUnsatisfiedException(string message) : InvalidOperationException(message);
