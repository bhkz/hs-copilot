using System.Windows.Media;
using HsCopilot.Core.Strategy;

namespace HsCopilot.App;

/// <summary>Contenu de l'overlay, recalculé à chaque mise à jour du conseiller (et chaque 250 ms pendant une analyse).</summary>
public sealed class OverlayViewModel : NotifyBase
{
    private string _headerText = "HS Copilot";
    private Brush _statusColor = Palette.Muted;
    private string _statusText = "Démarrage…";
    private string _actionTitle = "EN ATTENTE";
    private string _actionText = "";
    private Brush _actionColor = Palette.Text;
    private string _verifiedText = "";
    private Brush _verifiedColor = Palette.Good;
    private string _summaryText = "", _planText = "", _whyText = "", _nextText = "", _factsText = "", _footerText = "", _cautionText = "";
    private string? _lethalText;
    private Brush _lethalColor = Palette.Accent;
    private bool _compact, _paused;

    public string HeaderText { get => _headerText; set => Set(ref _headerText, value); }
    public Brush StatusColor { get => _statusColor; set => Set(ref _statusColor, value); }
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }
    public string ActionTitle { get => _actionTitle; set => Set(ref _actionTitle, value); }
    public string ActionText { get => _actionText; set => Set(ref _actionText, value); }
    public Brush ActionColor { get => _actionColor; set => Set(ref _actionColor, value); }
    public string VerifiedText { get => _verifiedText; set => Set(ref _verifiedText, value); }
    public Brush VerifiedColor { get => _verifiedColor; set => Set(ref _verifiedColor, value); }
    public string SummaryText { get => _summaryText; set => Set(ref _summaryText, value); }
    public string PlanText { get => _planText; set { if (Set(ref _planText, value)) Raise(nameof(HasPlan)); } }
    public string WhyText { get => _whyText; set { if (Set(ref _whyText, value)) Raise(nameof(HasWhy)); } }
    public string NextText { get => _nextText; set { if (Set(ref _nextText, value)) Raise(nameof(HasNext)); } }
    public string FactsText { get => _factsText; set => Set(ref _factsText, value); }
    public string FooterText { get => _footerText; set => Set(ref _footerText, value); }
    public string CautionText { get => _cautionText; set => Set(ref _cautionText, value); }
    public string? LethalText { get => _lethalText; set { if (Set(ref _lethalText, value)) Raise(nameof(HasLethal)); } }
    public Brush LethalColor { get => _lethalColor; set => Set(ref _lethalColor, value); }
    public bool HasPlan => PlanText.Length > 0;
    public bool HasWhy => WhyText.Length > 0;
    public bool HasNext => NextText.Length > 0;
    public bool HasLethal => !string.IsNullOrEmpty(LethalText);
    public bool Compact { get => _compact; set { if (Set(ref _compact, value)) Raise(nameof(ShowDetails)); } }
    public bool ShowDetails => !Compact;
    public bool Paused { get => _paused; set { if (Set(ref _paused, value)) Raise(nameof(PauseGlyph)); } }
    public string PauseGlyph => Paused ? "▶" : "⏸";

    public void Apply(AdviceView v)
    {
        StatusText = v.StatusText;
        StatusColor = v.Tone switch { "ok" => Palette.Good, "busy" => Palette.Accent, "warn" => Palette.Warn, "error" => Palette.Bad, _ => Palette.Muted };
        ActionTitle = v.ActionTitle;
        ActionText = v.ActionText;
        ActionColor = v.HasAdvice ? Palette.Text : Palette.Muted;
        VerifiedText = v.VerifiedText;
        VerifiedColor = v.Tone == "ok" ? Palette.Good : Palette.Accent;
        SummaryText = v.SummaryText;
        PlanText = v.PlanText;
        WhyText = v.WhyText;
        NextText = string.Join(Environment.NewLine, v.Next);
        FactsText = string.Join(Environment.NewLine, v.Facts);
        FooterText = v.FooterText;
        CautionText = v.CautionText;
        LethalText = v.LethalText;
        LethalColor = v.LethalTone switch { LethalLevels.Certain => Palette.Good, LethalLevels.Conditional => Palette.Warn, _ => Palette.Accent };
    }
}

/// <summary>Onglet « Conseiller » de la fenêtre principale : détail complet du dernier conseil et journal.</summary>
public sealed class AdvisorPanelViewModel : NotifyBase
{
    private string _keyText = "", _phaseText = "", _detailText = "", _statsText = "", _pingText = "", _effectiveText = "", _hotkeysText = "";
    private Brush _keyColor = Palette.Muted;

    public string KeyText { get => _keyText; set => Set(ref _keyText, value); }
    public Brush KeyColor { get => _keyColor; set => Set(ref _keyColor, value); }
    public string EffectiveText { get => _effectiveText; set => Set(ref _effectiveText, value); }
    public string PingText { get => _pingText; set => Set(ref _pingText, value); }
    public string PhaseText { get => _phaseText; set => Set(ref _phaseText, value); }
    public string DetailText { get => _detailText; set => Set(ref _detailText, value); }
    public string StatsText { get => _statsText; set => Set(ref _statsText, value); }
    public string HotkeysText { get => _hotkeysText; set => Set(ref _hotkeysText, value); }
    public System.Collections.ObjectModel.ObservableCollection<string> Journal { get; } = new();

    /// <summary>Synchronisation du conseil avec l'état : révision analysée, pioche, envoi, âge.</summary>
    public static string DiagnosticLine(AdvisorDiagnostics d) =>
        $"Synchronisation : révision analysée {d.AnalyzedRevision} (log {d.StateLogTime ?? "?"}, reçue à {d.StateReceivedUtc.ToLocalTime():HH:mm:ss.f})"
        + $" · pioche du tour dans l'état : {(d.DrawnThisTurn.Count == 0 ? "aucune" : string.Join(", ", d.DrawnThisTurn))} · {d.HandCount} carte(s) en main"
        + (d.SentUtc is DateTime sent ? $" · envoi {sent.ToLocalTime():HH:mm:ss.f}" : " · sans appel")
        + (d.AgeMs is double age ? $" · âge du conseil à l'affichage {age / 1000:0.0} s" : "");

    public void Apply(AdvisorUpdate u)
    {
        PhaseText = $"{u.Phase} · {u.Message}";
        StatsText = $"Ce tour : {u.CallsThisTurn} analyse(s), dont {u.MidTurnCallsThisTurn}/{u.MaxCallsPerTurn} automatiques de milieu de tour · cette partie : {u.CallsThisGame} analyse(s), {u.CostThisGameUsd:0.000} $ · session : {u.CostSessionUsd:0.000} $";
        if (u.Recommendation is { } r && u.Phase == AdvisorPhases.Ready)
        {
            var lines = new List<string>
            {
                $"Action : {AdviceDisplay.Label(r.Action, u.State)}",
                $"Conseil : {r.Summary}",
            };
            if (r.Why is not null) lines.Add($"Pourquoi : {r.Why}");
            if (r.PlanNextTurn is not null) lines.Add($"Tour suivant : {r.PlanNextTurn}");
            if (r.ReplaceActions.Count > 0) lines.Add("Mulligan, remplacer : " + string.Join(", ", r.ReplaceActions.Select(a => a.SourceName)));
            lines.Add($"Létal : {r.Lethal}" + (r.LethalNote is null ? "" : $" ({r.LethalNote})"));
            lines.Add($"Raisonnement : {r.Rationale}");
            if (r.RejectedAlternative is not null) lines.Add($"Alternative écartée : {r.RejectedAlternative.Label} : {r.RejectedAlternativeReason}");
            if (r.Risk is not null) lines.Add($"Risque : {r.Risk}");
            if (r.FollowUp.Count > 0) lines.Add("Suite envisagée (non vérifiée) : " + string.Join(" | ", r.FollowUp.Select(f => f.Description)));
            if (r.UncertaintyReasons.Count > 0) lines.Add("Incertitudes : " + string.Join(" ; ", r.UncertaintyReasons));
            if (r.Caution is not null) lines.Add(r.Caution);
            if (r.Warnings.Count > 0) lines.Add("Avertissements du validateur : " + string.Join(" ; ", r.Warnings));
            lines.Add($"Garantie : {r.Verification}");
            lines.Add($"Fournisseur : {r.Provider} · {r.LatencyMs / 1000:0.0} s · {r.CostUsd ?? 0:0.0000} $ · origine : {r.Origin}");
            if (u.Diagnostics is { } d) lines.Add(DiagnosticLine(d));
            DetailText = string.Join(Environment.NewLine, lines);
        }
        else if (u.Diagnostics is { } diag && u.Phase is AdvisorPhases.Analyzing or AdvisorPhases.Skipped)
        {
            DetailText = $"{u.Message}{Environment.NewLine}{DiagnosticLine(diag)}"
                         + (u.Facts.Count > 0 ? Environment.NewLine + "Analyse rapide : " + string.Join(" | ", u.Facts.Select(f => f.Text)) : "");
        }
        var entry = $"{DateTime.Now:HH:mm:ss} {u.Phase} : {(u.Recommendation is { } rr && u.Phase == AdvisorPhases.Ready ? rr.Action.Label : u.PartialAction?.Label ?? u.Message)}";
        if (Journal.Count == 0 || Journal[0] != entry) Journal.Insert(0, entry);
        while (Journal.Count > 80) Journal.RemoveAt(Journal.Count - 1);
    }
}
