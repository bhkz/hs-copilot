using HsCopilot.Core.State;

namespace HsCopilot.Core.Strategy;

/// <summary>Construit le contexte de décision à partir d'un état publié (aucun accès au moteur : seulement l'observable).</summary>
public static class DecisionContextBuilder
{
    /// <summary>Un point de décision : tour du joueur local, options du serveur à jour, partie en cours.</summary>
    public static bool IsDecisionPoint(GameStateSnapshot s) =>
        s.LegalOptions is { Fresh: true } && s.Game.ActivePlayer == "local" && !s.Game.GameOver && s.PendingChoice is null;

    /// <summary>Un choix proposé au joueur local (mulligan, Découverte, Rembobinage…) avec au moins une carte visible.</summary>
    public static bool IsChoicePoint(GameStateSnapshot s) =>
        s.PendingChoice is { Offered.Count: > 0 } c && !s.Game.GameOver && c.Offered.Any(o => o.CardId is not null);

    public static DecisionContext? Build(GameStateSnapshot s)
    {
        if (s.LegalOptions is not { Fresh: true } options) return null;
        return new DecisionContext
        {
            GameId = s.GameId,
            Revision = s.Revision,
            OptionsId = options.OptionsId,
            GameTurn = s.Game.GameTurn,
            ResetCount = s.Game.ResetCount,
            HistorySeq = s.History.Count == 0 ? 0 : s.History[^1].Seq,
            State = s,
            Hypotheses = Hypotheses(s),
            LegalActions = BuildLegalActions(s, options),
            OpponentInformationHidden = OpponentHidden(s),
        };
    }

    /// <summary>
    /// Contexte d'un choix en attente. Mulligan : une action « Remplacer » par carte et « Garder toute la main » ; le modèle
    /// liste les cartes à remplacer dans replaceIds. Autre choix : une action « Choisir » par carte proposée.
    /// </summary>
    public static DecisionContext? BuildChoice(GameStateSnapshot s)
    {
        if (!IsChoicePoint(s)) return null;
        var choice = s.PendingChoice!;
        var mulligan = choice.ChoiceType == "MULLIGAN";
        var actions = new List<LegalAction>();
        var index = 0;
        foreach (var o in choice.Offered)
        {
            // La pièce est proposée au mulligan du second joueur mais ne peut pas être remplacée.
            if (mulligan && HsCopilot.Core.Cards.CardRules.IsCoin(o.CardId)) { index++; continue; }
            var name = o.Name ?? (o.CardId is null ? "carte inconnue" : o.CardId);
            actions.Add(new LegalAction
            {
                Id = $"c{choice.Id}.{o.EntityId}",
                OptionsId = choice.Id,
                OptionIndex = index++,
                Kind = mulligan ? "mulligan-replace" : "choose",
                SourceEntityId = o.EntityId,
                SourceCardId = o.CardId,
                SourceName = o.Name,
                ManaCost = o.Cost,
                Label = mulligan ? $"Remplacer {name}" : o.DarkGift is { } gift ? $"Choisir {name} + {gift.Name ?? "Sombre don"}" : $"Choisir {name}",
            });
        }
        if (mulligan)
            actions.Add(new LegalAction { Id = $"c{choice.Id}.garder", OptionsId = choice.Id, OptionIndex = index, Kind = "mulligan-keep", Label = "Garder toute la main" });
        return new DecisionContext
        {
            GameId = s.GameId,
            Revision = s.Revision,
            OptionsId = choice.Id,
            GameTurn = s.Game.GameTurn,
            ResetCount = s.Game.ResetCount,
            HistorySeq = s.History.Count == 0 ? 0 : s.History[^1].Seq,
            State = s,
            Kind = mulligan ? DecisionContext.KindMulligan : DecisionContext.KindChoice,
            ChoiceType = choice.ChoiceType,
            ChoiceSourceName = choice.SourceName,
            Hypotheses = Hypotheses(s),
            LegalActions = actions,
            OpponentInformationHidden = OpponentHidden(s),
            SequenceVerification = mulligan
                ? "mulligan : les cartes proposées sont celles affichées par le jeu ; replaceIds liste celles à remplacer"
                : "choix : seules les cartes proposées par le jeu peuvent être choisies",
        };
    }

    private static bool OpponentHidden(GameStateSnapshot s) =>
        (s.Opponent?.Hand.Cards.Any(c => c.CardId is null) ?? false) || (s.Opponent?.Secrets.Any(x => !x.Known) ?? false) || (s.Opponent?.Deck.Count ?? 0) > 0;

    private static List<Hypothesis> Hypotheses(GameStateSnapshot s)
    {
        var hypotheses = new List<Hypothesis>();
        if (s.Opponent is not { } opp) return hypotheses;
        foreach (var c in opp.Hand.Cards)
        {
            if (c.Hypothesis is not null)
                hypotheses.Add(new Hypothesis { Subject = $"main adverse, position {c.Position}", Statement = c.Hypothesis, Basis = "règle du jeu" });
            else if (c.Knowledge is Visibility.PublicSeen or Visibility.PublicSeenStale)
                hypotheses.Add(new Hypothesis
                {
                    Subject = $"main adverse, position {c.Position}",
                    Statement = $"probablement {c.Name}",
                    Basis = c.Knowledge == Visibility.PublicSeenStale ? "vue publiquement, transformée depuis" : "vue publiquement avant de revenir en main",
                });
        }
        foreach (var secret in opp.Secrets.Where(x => !x.Known))
            hypotheses.Add(new Hypothesis { Subject = $"Secret adverse #{secret.EntityId}", Statement = $"Secret inconnu de classe {secret.CardClass ?? "?"}", Basis = "identité cachée" });
        return hypotheses;
    }

    public static List<LegalAction> BuildLegalActions(GameStateSnapshot s, LegalOptionsView options)
    {
        var costs = s.Local?.Hand.Cards.Where(c => c.Cost is not null).ToDictionary(c => c.EntityId, c => c.Cost) ?? new();
        var result = new List<LegalAction>();
        foreach (var o in options.Playable)
        {
            int? cost = o.EntityId is int eid && costs.TryGetValue(eid, out var c) ? c : null;
            if (o.SubOptions.Count > 0)
            {
                foreach (var sub in o.SubOptions)
                {
                    if (sub.Targets.Count == 0) result.Add(Make(options.OptionsId, o, sub, null, cost));
                    else foreach (var t in sub.Targets) result.Add(Make(options.OptionsId, o, sub, t, cost));
                }
            }
            else if (o.Targets.Count > 0)
            {
                foreach (var t in o.Targets) result.Add(Make(options.OptionsId, o, null, t, cost));
            }
            else
            {
                result.Add(Make(options.OptionsId, o, null, null, cost));
            }
        }
        if (options.CanEndTurn)
            result.Add(new LegalAction { Id = $"o{options.OptionsId}.fin", OptionsId = options.OptionsId, OptionIndex = 0, Kind = "end-turn", Label = "Finir le tour" });
        return result;
    }

    private static LegalAction Make(int optionsId, LegalOption o, LegalOption? sub, OptionTargetView? t, int? cost)
    {
        var id = $"o{optionsId}.{o.Index}" + (sub is null ? "" : $".s{sub.Index}") + (t is null ? "" : $".t{t.EntityId}");
        var verb = o.Kind switch
        {
            "play" => "Jouer",
            "attack" => "Attaquer avec",
            "hero-power" => "Pouvoir héroïque",
            "location" => "Activer",
            _ => o.Kind,
        };
        var label = $"{verb} {o.Name ?? "#" + o.EntityId}"
                    + (sub is null ? "" : $" ({sub.Name ?? "choix " + sub.Index})")
                    + (t is null ? "" : $" → {t.Name ?? "#" + t.EntityId}{(t.Side == "local" ? " (allié)" : "")}");
        return new LegalAction
        {
            Id = id,
            OptionsId = optionsId,
            OptionIndex = o.Index,
            SubOptionIndex = sub?.Index,
            TargetEntityId = t?.EntityId,
            Kind = o.Kind,
            SourceEntityId = o.EntityId,
            SourceCardId = o.CardId,
            SourceName = o.Name,
            SubOptionName = sub?.Name,
            TargetCardId = t?.CardId,
            TargetName = t?.Name,
            TargetSide = t?.Side,
            ManaCost = cost,
            Label = label,
        };
    }
}
