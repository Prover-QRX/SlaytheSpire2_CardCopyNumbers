using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;

var gameData = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    var path = Path.Combine(gameData, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
Probe.Run();

static class Probe
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Run()
    {
        new Harmony("card-copy-numbers.test-localization").Patch(
            AccessTools.Method(typeof(LocString), nameof(LocString.GetFormattedText)),
            prefix: new HarmonyMethod(typeof(Probe), nameof(Localize)));
        CardCopyNumbers.ModEntry.Initialize();
        var patches = Harmony.GetAllPatchedMethods().Where(m => Harmony.GetPatchInfo(m)?.Owners.Contains("local.card-copy-numbers") == true).ToArray();
        foreach (var method in patches) Console.WriteLine("PATCH OK " + method.DeclaringType!.Name + "." + method.Name);
        if (patches.Length != 7) throw new Exception("Expected seven patches.");
        var overrides = typeof(CardModel).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(CardModel)))
            .Select(t => t.GetProperty("Title", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(p => p is not null).ToArray();
        foreach (var property in overrides) Console.WriteLine("TITLE OVERRIDE " + property!.DeclaringType!.FullName);
        Console.WriteLine("Actual game assembly Harmony patch installation passed.");
        TestTitles();
    }

    private static bool Localize(LocString __instance, ref string __result)
    {
        __result = __instance.LocEntryKey;
        return false;
    }

    // Use actual game model getters and production registration, with localization
    // replaced by fixture strings (the console has no Godot localization assets).
    private static void TestTitles()
    {
        var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        var state = (PlayerCombatState)RuntimeHelpers.GetUninitializedObject(typeof(PlayerCombatState));
        AccessTools.Field(typeof(Player), "<PlayerCombatState>k__BackingField").SetValue(player, state);
        AccessTools.Field(typeof(Player), "<Deck>k__BackingField").SetValue(player, new CardPile(PileType.Deck));
        var hand = new CardPile(PileType.Hand);
        var draw = new CardPile(PileType.Draw);
        var discard = new CardPile(PileType.Discard);
        var exhaust = new CardPile(PileType.Exhaust);
        AccessTools.Field(typeof(PlayerCombatState), "_piles").SetValue(state, new[] { hand, draw, discard, exhaust });
        List<CardModel> Contents(CardPile pile) => (List<CardModel>)AccessTools.Field(typeof(CardPile), "_cards").GetValue(pile)!;
        var numbers = typeof(CardCopyNumbers.ModEntry).Assembly.GetType("CardCopyNumbers.Numbers")!;
        var register = AccessTools.Method(numbers, "Register");
        T Card<T>(string name) where T : CardModel
        {
            var card = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
            AccessTools.Field(typeof(AbstractModel), "<IsMutable>k__BackingField").SetValue(card, true);
            AccessTools.Field(typeof(CardModel), "_owner").SetValue(card, player);
            AccessTools.Field(typeof(CardModel), "_titleLocString").SetValue(card, new LocString("fixture", name));
            return card;
        }
        void Check(string actual, string expected)
        {
            if (actual != expected) throw new Exception($"Expected {expected}, got {actual}");
            Console.WriteLine("TITLE OK " + actual);
        }
        var first = Card<Wither>("凋萎");
        var second = Card<Wither>("凋萎");
        register.Invoke(null, new object[] { first });
        Contents(hand).Add(first);
        Check(first.Title, "凋萎");
        register.Invoke(null, new object[] { second });
        Contents(draw).Add(second);
        Check(first.Title, "凋萎 #1");
        Check(second.Title, "凋萎 #2");
        AccessTools.Field(typeof(Wither), "_fakeUpgradeLevel").SetValue(second, 2);
        Check(second.Title, "凋萎+2 #2");
        register.Invoke(null, new object[] { second });
        Check(second.Title, "凋萎+2 #2");
        var preview = Card<Wither>("凋萎");
        var registry = AccessTools.Field(numbers, "Registry").GetValue(null)!;
        AccessTools.Method(registry.GetType(), "CopyPreview").Invoke(registry, new object[] { second, preview });
        Check(preview.Title, "凋萎 #2");
        register.Invoke(null, new object[] { preview });
        Contents(draw).Add(preview);
        Check(preview.Title, "凋萎 #3");
        var unrelated = Card<Wither>("感染");
        register.Invoke(null, new object[] { unrelated });
        Contents(discard).Add(unrelated);
        Check(unrelated.Title, "感染");
        Contents(hand).Remove(first);
        Contents(exhaust).Add(first);
        Check(second.Title, "凋萎+2 #2");
        Contents(exhaust).Remove(first);
        Contents(draw).Remove(preview);
        Check(second.Title, "凋萎+2 #2");
        Contents(draw).Remove(second);
        Check(first.Title, "凋萎 #1");
        var fourth = Card<Wither>("凋萎");
        register.Invoke(null, new object[] { fourth });
        Contents(draw).Add(fourth);
        Check(fourth.Title, "凋萎 #4");
        Contents(discard).Add(first);
        Check(second.Title, "凋萎+2 #2");
        Check(first.Title, "凋萎 #1");
        AccessTools.Field(typeof(Player), "<PlayerCombatState>k__BackingField").SetValue(player, null);
        Check(first.Title, "凋萎");
    }
}
