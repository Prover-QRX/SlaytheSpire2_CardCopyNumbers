using CardCopyNumbers;

int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    Console.WriteLine("PASS " + name);
    checks++;
}
var registry = new NumberRegistry<object>();
var scope = new object();
var deck = Enumerable.Range(0, 12).Select(_ => new object()).ToArray();
foreach (var card in deck) registry.Register(card, scope, "Strike");
var combat = new object[12];
for (int i = 0; i < 12; i++)
{
    combat[i] = new object();
    registry.CopyPreview(deck[i], combat[i]);
    Check(registry.Register(combat[i], scope, "Strike", deck[i]).Number == i + 1, "deck identity " + (i + 1));
}
Check(registry.Register(combat[5], scope, "Strike").Number == 6, "moving piles retains identity");
var preview = new object();
registry.CopyPreview(combat[5], preview);
Check(registry.Get(preview)!.Number == 6, "prediction preview retains identity");
var nestedPreview = new object();
registry.CopyPreview(preview, nestedPreview);
Check(registry.Get(nestedPreview)!.Number == 6, "nested prediction retains identity");
Check(registry.Register(new object(), scope, "Strike").Number == 13, "previews do not consume numbers");
Check(registry.Register(preview, scope, "Strike").Number == 14, "real clone gets new identity");
Check(registry.Get(combat[5])!.Number == 6, "clone does not change original");
var infection1 = new object();
var infection2 = new object();
Check(registry.Register(infection1, scope, "Infection").Number == 1, "generated status starts at one");
Check(registry.Register(infection2, scope, "Infection").Number == 2, "generated status increments");
Check(registry.Register(new object(), scope, "Minion Strike").Number == 1, "transformation destination has separate group");
var view = new List<object> {combat[11], infection2, combat[1], combat[9], infection1};
var live = view.ToArray();
registry.SortSameNames(view);
Check(ReferenceEquals(view[0], combat[1]) && ReferenceEquals(view[2], combat[9]) && ReferenceEquals(view[3], combat[11]), "numeric sort 2,10,12 with gaps");
Check(ReferenceEquals(view[1], infection1) && ReferenceEquals(view[4], infection2), "same-name slots sorted independently");
Check(ReferenceEquals(live[0], combat[11]), "live draw order untouched");
var secondPlayer = new object();
Check(registry.Register(new object(), secondPlayer, "Strike").Number == 1, "player numbering isolated");
var history = new NumberRegistry<object>();
var historyScope = new object();
var onlyCard = new object();
var firstEntry = history.Register(onlyCard, historyScope, "Infection");
Check(!history.ShouldShowNumber(firstEntry), "only number one assigned: hidden");
var statusPreview = new object();
history.CopyPreview(onlyCard, statusPreview);
Check(!history.ShouldShowNumber(history.Get(statusPreview)!), "preview of unique card stays hidden");
Check(!history.ShouldShowNumber(firstEntry), "preview does not advance history");
history.Register(new object(), new object(), "Infection");
history.Register(new object(), historyScope, "Wither");
Check(!history.ShouldShowNumber(firstEntry), "other player and other name do not reveal first number");
var nextEntry = history.Register(new object(), historyScope, "Infection");
Check(nextEntry.Number == 2 && history.ShouldShowNumber(nextEntry), "second assigned number visible");
Check(history.ShouldShowNumber(firstEntry), "number one revealed when number two assigned");
Check(history.ShouldShowNumber(history.Get(statusPreview)!), "existing preview follows historical visibility");
// Empty the simulated live piles; historical visibility must stay unchanged.
var liveCards = new List<object> { onlyCard };
liveCards.Clear();
Check(history.ShouldShowNumber(firstEntry), "retired number one stays visible after duplicate history");
Check(history.ShouldShowNumber(nextEntry), "retired number two stays visible");
var thirdEntry = history.Register(new object(), historyScope, "Infection");
Check(thirdEntry.Number == 3 && history.ShouldShowNumber(thirdEntry), "new lone number three is visible and does not reuse retired numbers");
Check(history.Register(onlyCard, historyScope, "Infection").Number == 1, "returning original retains number one");
var newBattleEntry = history.Register(new object(), new object(), "Infection");
Check(newBattleEntry.Number == 1 && !history.ShouldShowNumber(newBattleEntry), "new battle resets allocation and hides first number");
var reload = new NumberRegistry<object>();
var reloadScope = new object();
for (int i = 0; i < 12; i++) Check(reload.Register(new object(), reloadScope, "Strike").Number == i + 1, "deterministic restart " + (i + 1));
Check(registry.Register(combat[5], new object(), "Strike").Number == 1, "new combat resets scope");
Console.WriteLine($"{checks} checks passed.");

