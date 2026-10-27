using System.Globalization;
using System.Text.RegularExpressions;
using HsCopilot.Core.State;

namespace HsCopilot.Core.Strategy;

/// <summary>
/// Probabilités de pioche calculées sur le contenu connu du deck (liste HDT moins les cartes d'origine sorties, plus les
/// cartes ajoutées connues). Exactes quand toutes les cartes du deck sont identifiées et que la liste est cohérente ; sinon
/// annoncées comme estimations. L'ordre du deck n'est jamais supposé, sauf une position rendue publique par un effet
/// (Sombre don Beaux rêves : dessus du deck).
/// </summary>
public static class DeckOdds
{
    /// <summary>« vous piochez un serviteur », « vous piochez un serviteur aléatoire », « piochez un Dragon »…</summary>
    private static readonly Regex DrawFiltered = new(@"piochez un (serviteur|Dragon|sort)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    /// <summary>Pioche soumise à une condition (« Si vous avez en main un serviteur coûtant (5) ou plus, vous piochez… »).</summary>
    private static readonly Regex ConditionBeforeDraw = new(@"\bsi vous [^.]*piochez", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public sealed record Entry(string Name, int Count, string? Type, bool Dragon, int? Cost);

    /// <summary>Contenu connu du deck, cartes ajoutées comprises, et nombre de cartes inconnues.</summary>
    public static (List<Entry> Known, int Unknown, bool Exact) Content(DeckView d)
    {
        var known = d.KnownRemaining.Select(c => new Entry(c.Name ?? c.CardId, c.Count, c.Type, c.Races?.Contains("DRAGON") == true, c.Cost)).ToList();
        foreach (var g in d.KnownAdded.GroupBy(c => c.CardId ?? "?"))
            known.Add(new Entry(g.First().Name ?? g.Key, g.Count(), g.First().Type, false, g.First().Cost));
        var unknown = Math.Max(0, d.Count - known.Sum(e => e.Count));
        var exact = d.DecklistKnown && d.Warning is null && unknown == 0;
        return (known, unknown, exact);
    }

    /// <summary>
    /// Objet joint à « localAnalysis.deck » ; null si le deck est inconnu. Avec <paramref name="onlyIfRelevant"/>, seulement
    /// quand une pioche filtrée est en main, que le dessus du deck est connu ou que le deck est presque vide : la comparaison
    /// sur de vraies décisions (docs/SOMBRES-DONS-ET-DECK.md) n'a montré aucun usage des probabilités générales par le modèle.
    /// </summary>
    public static object? Payload(DecisionContext ctx, bool onlyIfRelevant = false)
    {
        var me = ctx.State.Local;
        if (me is null) return null;
        var d = me.Deck;
        if (d.Count == 0) return new { count = 0, note = "deck vide : chaque pioche inflige des dégâts de fatigue" };
        if (!d.DecklistKnown && d.KnownAdded.Count == 0) return null;
        var (known, unknown, exact) = Content(d);
        var n = d.Count;
        string P(int k) => $"{k}/{n} ({100.0 * k / n:0} %)";
        var basis = exact ? "exact : toutes les cartes du deck sont identifiées, ordre inconnu"
            : $"estimation : {unknown} carte(s) du deck non identifiée(s)" + (d.Warning is null ? "" : " ; liste peut-être différente du deck joué");

        object nextDraw;
        if (d.KnownTop.FirstOrDefault() is { } top)
            nextDraw = $"connue : {top.Name} (placée sur le dessus du deck), sauf si le deck a été mélangé depuis";
        else
            nextDraw = new
            {
                dragon = P(known.Where(e => e.Dragon).Sum(e => e.Count)),
                minion = P(known.Where(e => e.Type == "MINION").Sum(e => e.Count)),
                spell = P(known.Where(e => e.Type == "SPELL").Sum(e => e.Count)),
                minionCost5Plus = P(known.Where(e => e.Type == "MINION" && e.Cost >= 5).Sum(e => e.Count)),
            };

        // Effets de pioche filtrée présents en main : résultats possibles et probabilité de chacun.
        var filtered = new List<object>();
        foreach (var card in me.Hand.Cards.Where(c => c.Text is not null && DrawFiltered.IsMatch(c.Text)).GroupBy(c => c.CardId).Select(g => g.First()))
        {
            var kind = DrawFiltered.Match(card.Text!).Groups[1].Value.ToLowerInvariant();
            var pool = known.Where(e => kind switch { "serviteur" => e.Type == "MINION", "dragon" => e.Dragon, _ => e.Type == "SPELL" }).ToList();
            var total = pool.Sum(e => e.Count);
            var rewind = card.Text!.Contains("Rembobinage", StringComparison.OrdinalIgnoreCase);
            // Résultats regroupés par probabilité : « 2/19 (11 %, 20 % avec le Rembobinage) : Wyrm, Glissedrake… ».
            var outcomes = pool.GroupBy(e => e.Count).OrderByDescending(g => g.Key).Select(g =>
            {
                var p = (double)g.Key / total;
                var odds = $"{g.Key}/{total} chacun ({p.ToString("P0", Fr)}" + (rewind ? $", {(1 - (1 - p) * (1 - p)).ToString("P0", Fr)} avec le second tirage du Rembobinage)" : ")");
                return $"{odds} : {string.Join(", ", g.OrderByDescending(e => e.Cost).Select(e => e.Name))}";
            }).ToList();
            // La condition du client (POWERED_UP) n'a de sens que pour une pioche conditionnelle (« Si vous avez… »).
            var conditional = ConditionBeforeDraw.IsMatch(card.Text!);
            filtered.Add(new
            {
                card = card.Name,
                draws = kind switch { "serviteur" => "un serviteur du deck, au hasard parmi les serviteurs", "dragon" => "un Dragon du deck, au hasard", _ => "un sort du deck, au hasard" },
                condition = !conditional ? null : card.PoweredUp switch { true => "condition remplie selon le jeu", false => "condition NON remplie selon le jeu : pas de pioche", _ => null },
                candidates = total,
                outcomes = total == 0 ? new List<string> { "aucune carte de ce type dans le deck connu : la pioche échoue" } : outcomes,
                basis = exact ? "exact" : $"estimation : {unknown} carte(s) inconnue(s) non comptée(s)",
            });
        }

        if (onlyIfRelevant && filtered.Count == 0 && d.KnownTop.Count == 0 && n > 5) return null;
        return new
        {
            count = n,
            basis,
            nextDraw,
            filteredDraws = filtered.Count == 0 ? null : filtered,
        };
    }
}
