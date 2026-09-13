using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace CardCopyNumbers;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    public static void Initialize() => new Harmony("local.card-copy-numbers").PatchAll();
}

internal static class Numbers
{
    internal static readonly NumberRegistry<CardModel> Registry = new();
    private static readonly ConditionalWeakTable<PlayerCombatState, object> Seeded = new();

    // The un-upgraded localized name groups e.g. different characters' Strikes together.
    private static string Group(CardModel card) => card.TitleLocString.GetFormattedText();

    internal static void Register(CardModel card)
    {
        if (card.IsCanonical || card.Owner?.PlayerCombatState is not { } scope) return;
        if (!Seeded.TryGetValue(scope, out _))
        {
            Seeded.Add(scope, new object());
            foreach (var deckCard in card.Owner.Deck.Cards)
                Registry.Register(deckCard, scope, Group(deckCard));
        }
        Registry.Register(card, scope, Group(card), card.DeckVersion);
    }

    internal static NumberRegistry<CardModel>.Entry? VisibleEntry(CardModel card)
    {
        if (card.IsCanonical) return null;
        var entry = Registry.Get(card);
        return entry is not null && card.Owner?.PlayerCombatState is { } scope
            && ReferenceEquals(scope, entry.Scope) && Registry.ShouldShowNumber(entry)
            ? entry : null;
    }
}

[HarmonyPatch(typeof(CardPile), nameof(CardPile.AddInternal))]
internal static class RegisterRealCardPatch
{
    // Before CardAdded/ContentsChanged handlers can render the new card.
    private static void Prefix(CardPile __instance, CardModel card)
    {
        if (__instance.IsCombatPile) Numbers.Register(card);
    }

    private static void Postfix(CardPile __instance, CardModel card)
    {
        if (__instance.IsCombatPile) VisibleTitles.RefreshGroup(card);
    }
}

[HarmonyPatch(typeof(CardPile), nameof(CardPile.RemoveInternal))]
internal static class RemovedCardPatch
{
    private static void Postfix(CardPile __instance, CardModel card)
    {
        if (__instance.IsCombatPile) VisibleTitles.RefreshGroup(card);
    }
}

internal static class VisibleTitles
{
    private static readonly List<WeakReference<NCard>> Nodes = new();
    private static readonly ConditionalWeakTable<NCard, object> Known = new();

    internal static void Track(NCard node)
    {
        if (Known.TryGetValue(node, out _)) return;
        Known.Add(node, new object());
        Nodes.Add(new WeakReference<NCard>(node));
    }

    internal static void RefreshGroup(CardModel card)
    {
        var changed = Numbers.Registry.Get(card);
        if (changed is null) return;
        for (int i = Nodes.Count - 1; i >= 0; i--)
        {
            if (!Nodes[i].TryGetTarget(out var node) || !Godot.GodotObject.IsInstanceValid(node))
            {
                Nodes.RemoveAt(i);
                continue;
            }
            if (!node.IsInsideTree() || node.IsQueuedForDeletion() || node.Model is not { } model) continue;
            var entry = Numbers.Registry.Get(model);
            if (entry is not null && ReferenceEquals(entry.Scope, changed.Scope) && entry.Group == changed.Group)
                // Read final pile contents after a remove/add move has completed.
                // Refresh only the title: full UpdateVisuals mutates preview variables.
                node.CallDeferred(NCard.MethodName.UpdateTitleLabel);
        }
    }
}

[HarmonyPatch(typeof(NCard), "UpdateTitleLabel")]
internal static class TrackVisibleCardPatch
{
    private static void Postfix(NCard __instance) => VisibleTitles.Track(__instance);
}

[HarmonyPatch(typeof(AbstractModel), nameof(AbstractModel.MutableClone))]
internal static class PreviewPatch
{
    private static void Postfix(AbstractModel __instance, AbstractModel __result)
    {
        if (__instance is CardModel source && __result is CardModel preview)
            Numbers.Registry.CopyPreview(source, preview);
    }
}

[HarmonyPatch]
internal static class TitlePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.Title));
        yield return AccessTools.PropertyGetter(typeof(MegaCrit.Sts2.Core.Models.Cards.Wither), nameof(CardModel.Title));
    }

    private static void Postfix(CardModel __instance, MethodBase __originalMethod, ref string __result)
    {
        // Wither calls base.Title then appends its fake upgrade level.
        if (__instance is MegaCrit.Sts2.Core.Models.Cards.Wither && __originalMethod.DeclaringType == typeof(CardModel)) return;
        if (Numbers.VisibleEntry(__instance) is { } entry)
            __result += " #" + entry.Number;
    }
}

[HarmonyPatch(typeof(NCardPileScreen), "OnPileContentsChanged")]
internal static class OrdinaryPileOrderPatch
{
    // Only replaces the vanilla screen's call. RandomForeseer's prefix skips this
    // body and calls NCardGrid directly, preserving its true/predicted draw order.
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var target = AccessTools.Method(typeof(NCardGrid), nameof(NCardGrid.SetCards));
        var replacement = AccessTools.Method(typeof(OrdinaryPileOrderPatch), nameof(SetCards));
        int replaced = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(target))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                replaced++;
            }
            yield return instruction;
        }
        if (replaced != 1) throw new InvalidOperationException("CardCopyNumbers: incompatible pile screen; expected one SetCards call.");
    }

    internal static void SetCards(NCardGrid grid, IReadOnlyList<CardModel> cardsToDisplay,
        PileType pileType, List<SortingOrders> sortingPriority, Task? taskToWaitOn)
    {
        var display = cardsToDisplay.ToList();
        Numbers.Registry.SortSameNames(display);
        grid.SetCards(display, pileType, sortingPriority, taskToWaitOn);
    }
}
