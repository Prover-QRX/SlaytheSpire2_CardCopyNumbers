using System.Runtime.CompilerServices;

namespace CardCopyNumbers;

// No game state or RNG is mutated. Scope and card keys are weak references.
internal sealed class NumberRegistry<T> where T : class
{
    internal sealed record Entry(object Scope, string Group, int Number, bool Actual);
    private readonly ConditionalWeakTable<T, Entry> entries = new();
    private readonly ConditionalWeakTable<object, Dictionary<string, int>> counters = new();

    public Entry? Get(T card) => entries.TryGetValue(card, out var entry) ? entry : null;

    public bool ShouldShowNumber(Entry entry)
    {
        // Historical allocation, never current pile population. Retired numbers
        // remain reserved, and preview copies do not advance this counter.
        return entry.Number > 1 || (counters.TryGetValue(entry.Scope, out var counts)
            && counts.TryGetValue(entry.Group, out int highestAssigned) && highestAssigned > 1);
    }

    public Entry Register(T card, object scope, string group, T? deckCard = null)
    {
        var old = Get(card);
        if (old is { Actual: true } && ReferenceEquals(old.Scope, scope)) return old;
        var counts = counters.GetOrCreateValue(scope);
        var deck = deckCard is null ? null : Get(deckCard);
        int number;
        if (deck is not null && ReferenceEquals(deck.Scope, scope) && deck.Group == group)
            number = deck.Number;
        else
        {
            counts.TryGetValue(group, out number);
            counts[group] = ++number;
        }
        var result = new Entry(scope, group, number, true);
        entries.AddOrUpdate(card, result);
        return result;
    }

    public void CopyPreview(T source, T preview)
    {
        if (Get(source) is { } entry)
            entries.AddOrUpdate(preview, entry with { Actual = false });
    }

    public void SortSameNames(List<T> cards)
    {
        var groups = new Dictionary<(object, string), List<int>>();
        for (int i = 0; i < cards.Count; i++)
        {
            if (Get(cards[i]) is not { } entry) continue;
            var key = (entry.Scope, entry.Group);
            if (!groups.TryGetValue(key, out var positions)) groups[key] = positions = new();
            positions.Add(i);
        }
        foreach (var positions in groups.Values)
        {
            var ordered = positions.Select(i => cards[i]).OrderBy(c => Get(c)!.Number).ToArray();
            for (int i = 0; i < positions.Count; i++) cards[positions[i]] = ordered[i];
        }
    }
}
