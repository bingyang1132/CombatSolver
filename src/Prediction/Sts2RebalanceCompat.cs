using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;
using CombatSolver.Engine.InCombat.Simulation;

namespace CombatSolver;

/// <summary>
/// LOCAL BUILD ONLY. Mirrors the card reworks that the Sts2RebalanceBeta mod installs by replacing vanilla
/// <see cref="CardModel.OnPlay"/> bodies with Harmony prefixes.
/// </summary>
/// <remarks>
/// The engine cannot see those replacements: bespoke mirrors are keyed on the vanilla card type and
/// <see cref="CardOnPlayInferrer"/> reads the original, unpatched IL by design. Everything the mod expresses as
/// canonical data (energy cost, dynamic vars, keywords, rarity) is already followed by the mirrors, which read
/// live values, so only replaced behavior is restated here.
///
/// This file is not upstreamable and must not grow beyond a straight transcription of the mod's replacements.
/// Keep every deviation from a stock CombatSolver build inside this file plus the four tagged call sites, so
/// rebasing onto a new upstream release stays a mechanical merge.
/// </remarks>
internal static class Sts2RebalanceCompat
{
    private const string ModAssemblyName = "Sts2RebalanceBeta";

    /// <summary>Borrowed Time applies this much Doom to its owner in the reworked card.</summary>
    private const int BorrowedTimeDoom = 3;

    /// <summary>Withering Presence triggers every this many cards in the reworked encounter.</summary>
    public const int AeonglassCardsBeforeWither = 4;

    public static bool IsActive { get; } = AppDomain.CurrentDomain.GetAssemblies()
        .Any(assembly => string.Equals(
            assembly.GetName().Name,
            ModAssemblyName,
            StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Restates the parts of a reworked OnPlay that the stock mirrors leave out. Runs after the generic and
    /// bespoke mirrors, so it only ever adds effects.
    /// </summary>
    public static void CompleteCardOnPlay(
        CombatPredictionSimulator simulator,
        SimulatedCombatState combat,
        PredictedCard playedCard,
        Creature? target)
    {
        if (!IsActive)
            return;

        CardModel card = playedCard.Preview;
        Creature owner = card.Owner.Creature;
        switch (card)
        {
            // "Apply 3 Doom to yourself. Gain 1(2) Energy." The energy is already correct because the mod moved
            // it onto CanonicalVars, and ExtraCost is pinned to 0 so the stock BorrowedTimePower line is inert.
            case BorrowedTime:
                combat.Apply<DoomPower>(owner, BorrowedTimeDoom, owner);
                break;

            // "Deal 5(7) damage. If you lost HP this turn, draw 1 card." Damage is already correct because the
            // mod pins Repeat to 1, so only the draw is missing.
            case Spite when combat.HasLostHpThisTurn(owner):
                simulator.Draw(card.Owner, 1);
                break;

            // "Gain 1 Energy. Draw 1(2) cards." Stock only gains the energy on 0.111.0.
            case Fuel:
                simulator.Draw(card.Owner, card.DynamicVars.Cards.IntValue);
                break;

            // "Gain 1(2) Stars. Draw 2 cards." / "Deal damage. Draw 2(3) cards."
            // Both moved the draw from next turn to now; the stock next-turn Power is unregistered in
            // CardEffectSpecRegistry for this build.
            case Glow:
            case GuidingStar:
                simulator.Draw(card.Owner, card.DynamicVars.Cards.IntValue);
                break;
        }
    }

    /// <summary>
    /// "Exhaust your hand. Draw that many cards." Stock generates that many random cards instead.
    /// </summary>
    /// <remarks>Rocket Punch is handled inline in AfterCardGeneratedForCombatMirrors: the mod restores the
    /// pre-0.110.0 "cost becomes 0 until played" instead of 0.110.0's "cost -1".</remarks>
    public static void StokeOnPlay(Stoke card, CardOnPlayMirrorContext context)
    {
        List<PredictedCard> hand = context.OwnerState.Hand.Cards.ToList();
        foreach (PredictedCard handCard in hand)
        {
            context.Simulator.Exhaust(handCard);
        }
        context.Simulator.Draw(card.Owner, hand.Count);
    }
}
