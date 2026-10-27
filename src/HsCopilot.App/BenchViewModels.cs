using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using HsCopilot.Core.Evaluation;
using HsCopilot.Core.State;
using HsCopilot.Core.Strategy;

namespace HsCopilot.App;

public abstract class NotifyBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    protected void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Un cas dans la liste de gauche.</summary>
public sealed class BenchCaseVm : NotifyBase
{
    public required DecisionCase Case { get; init; }
    public required DecisionContext Context { get; init; }
    public required IReadOnlyList<BenchResult> Results { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }

    public string Title => $"Tour {Case.TurnNumber} · décision {Case.DecisionInTurn}" + (Ranked ? " · classée" : " · IA") + $" · {Case.OpponentClass}";
    public string Subtitle => Case.CaseId;
    public bool Ranked => Case.GameType == "GT_RANKED";
    public bool Lethal => Tags.Contains("létal-attaques");
    public bool HasDubious => Results.Any(r => r.Flags.Any(f => f.Severity == "douteux"));
    public bool HasErrors => Results.Any(r => r.Error is not null || !r.Accepted);
    public bool DisagreesWithActual => Results.Any(r => r.Agreement is "différent" or "même-type");
    public bool VariantsDisagree => Results.Where(r => r.Legal).Select(r => r.ActionId).Distinct().Count() > 1;

    private int _annotated;
    public int Annotated { get => _annotated; set { if (Set(ref _annotated, value)) Raise(nameof(Badges)); } }

    public string Badges
    {
        get
        {
            var parts = new List<string>();
            var dubious = Results.Count(r => r.Flags.Any(f => f.Severity == "douteux"));
            if (dubious > 0) parts.Add($"douteux {dubious}");
            var differ = Results.Count(r => r.Agreement is "différent" or "même-type");
            if (Case.Actual?.ActionId is not null) parts.Add($"différent de ton coup {differ}/{Results.Count(r => r.Legal)}");
            if (VariantsDisagree) parts.Add("variantes divisées");
            if (Lethal) parts.Add("létal");
            if (Results.Any(r => r.Error is not null)) parts.Add("erreur");
            if (Annotated > 0) parts.Add($"annoté {Annotated}");
            return string.Join(" · ", parts);
        }
    }

    public Brush BadgeColor => HasDubious ? Palette.Bad : DisagreesWithActual ? Palette.Warn : Palette.Muted;
}

public sealed class LegalActionVm
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public string Marks { get; init; } = "";
    public bool IsActual { get; init; }
    public bool IsChosen { get; init; }
    public Brush Color => IsActual ? Palette.Good : IsChosen ? Palette.Accent : Palette.Text;
    public System.Windows.FontWeight Weight => IsActual || IsChosen ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal;
}

/// <summary>Une réponse (ou ton coup réel) avec son annotation.</summary>
public sealed class AnswerVm : NotifyBase
{
    private readonly Action<AnswerVm> _changed;
    private string? _verdict;
    private string _note = "";

    public AnswerVm(Action<AnswerVm> changed, string? verdict, string? note)
    {
        _changed = changed;
        _verdict = verdict;
        _note = note ?? "";
    }

    public required string CaseId { get; init; }
    public required string Variant { get; init; }
    public required string Title { get; init; }
    public required string Action { get; init; }
    public string Meta { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Rationale { get; init; } = "";
    public string Alternative { get; init; } = "";
    public string Plan { get; init; } = "";
    public string FollowUp { get; init; } = "";
    public string Uncertainty { get; init; } = "";
    public string Flags { get; init; } = "";
    public string Error { get; init; } = "";
    public Brush FlagColor { get; init; } = Palette.Warn;
    public Brush TitleColor { get; init; } = Palette.Accent;

    public string? Verdict
    {
        get => _verdict;
        set
        {
            if (!Set(ref _verdict, value)) return;
            Raise(nameof(IsGood)); Raise(nameof(IsDebatable)); Raise(nameof(IsBad));
            _changed(this);
        }
    }

    public string Note
    {
        get => _note;
        set { if (Set(ref _note, value ?? "")) _changed(this); }
    }

    public bool IsGood { get => Verdict == "bon"; set { if (value) Verdict = "bon"; } }
    public bool IsDebatable { get => Verdict == "discutable"; set { if (value) Verdict = "discutable"; } }
    public bool IsBad { get => Verdict == "mauvais"; set { if (value) Verdict = "mauvais"; } }

    public static AnswerVm FromResult(BenchResult r, DecisionContext ctx, Annotation? ann, Action<AnswerVm> changed)
    {
        var inv = CultureInfo.GetCultureInfo("fr-FR");
        var meta = r.Error is not null
            ? $"{r.LatencyMs / 1000:0.0} s · {r.CostUsd?.ToString("0.0000", inv) ?? "?"} $"
            : $"{Agreement(r.Agreement)} · confiance {r.Confidence} · {r.LatencyMs / 1000:0.0} s · entrée {r.InputTokens} (cache {r.CachedInputTokens}) · sortie {r.OutputTokens} (raisonnement {r.ReasoningTokens}) · {r.CostUsd?.ToString("0.0000", inv) ?? "?"} $";
        return new AnswerVm(changed, ann?.Verdict, ann?.Note)
        {
            CaseId = r.CaseId,
            Variant = r.Variant,
            Title = r.Variant,
            Action = r.Error is not null ? "Pas de réponse" : r.ActionLabel ?? "?",
            Meta = meta,
            Summary = r.Summary ?? "",
            Rationale = r.Rationale ?? "",
            Alternative = r.RejectedAlternativeLabel is null ? "" : $"Alternative écartée : {r.RejectedAlternativeLabel} : {r.RejectedAlternativeReason}",
            Plan = string.IsNullOrWhiteSpace(r.PlanNextTurn) ? "" : $"Tour suivant : {r.PlanNextTurn}",
            FollowUp = r.FollowUp.Count == 0 ? "" : "Suite envisagée (non vérifiée) : " + string.Join(" | ", r.FollowUp),
            Uncertainty = r.UncertaintyReasons.Count == 0 ? "" : "Incertitudes : " + string.Join(" ; ", r.UncertaintyReasons),
            Flags = string.Join(Environment.NewLine, r.Flags.Select(f => $"[{f.Severity}] {f.Code} : {f.Message}")
                .Concat(r.Accepted || r.Error is not null ? Array.Empty<string>() : r.ValidationReasons.Select(x => "[rejetée] " + x))
                .Concat(r.Warnings.Select(w => "[avertissement] " + w))),
            Error = r.Error ?? "",
            FlagColor = r.Flags.Any(f => f.Severity == "douteux") || !r.Accepted ? Palette.Bad : Palette.Warn,
            TitleColor = r.Agreement == "exact" ? Palette.Good : Palette.Accent,
        };
    }

    public static AnswerVm FromActual(DecisionCase c, DecisionContext ctx, Annotation? ann, Action<AnswerVm> changed)
    {
        var played = c.Actual?.ActionId is string id ? ctx.LegalActions.FirstOrDefault(a => a.Id == id) : null;
        var flags = played is null ? new List<DubiousFlag>() : DubiousMoveDetector.Analyze(ctx, played);
        return new AnswerVm(changed, ann?.Verdict, ann?.Note)
        {
            CaseId = c.CaseId,
            Variant = AnnotationStore.RealMove,
            Title = "Ton coup réel (étiquette, jamais montré au modèle)",
            Action = played?.Label ?? c.Actual?.Note ?? "coup inconnu (aucune action envoyée avant le bloc d'options suivant)",
            Flags = string.Join(Environment.NewLine, flags.Select(f => $"[{f.Severity}] {f.Code} : {f.Message}")),
            FlagColor = flags.Any(f => f.Severity == "douteux") ? Palette.Bad : Palette.Warn,
            TitleColor = Palette.Good,
        };
    }

    private static string Agreement(string? a) => a switch
    {
        "exact" => "identique à ton coup",
        "même-carte" => "même carte, autre cible",
        "même-type" => "même type d'action",
        "différent" => "différent de ton coup",
        "sans-réponse" => "sans réponse",
        _ => "ton coup inconnu",
    };
}

public static class Palette
{
    public static readonly Brush Text = Freeze(new SolidColorBrush(Color.FromRgb(0xE6, 0xE9, 0xEF)));
    public static readonly Brush Muted = Freeze(new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xB2)));
    public static readonly Brush Accent = Freeze(new SolidColorBrush(Color.FromRgb(0xE2, 0xB6, 0x5B)));
    public static readonly Brush Good = Freeze(new SolidColorBrush(Color.FromRgb(0x6C, 0xC4, 0x87)));
    public static readonly Brush Warn = Freeze(new SolidColorBrush(Color.FromRgb(0xE8, 0xB0, 0x4B)));
    public static readonly Brush Bad = Freeze(new SolidColorBrush(Color.FromRgb(0xE2, 0x6D, 0x6D)));

    private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
}

/// <summary>Chargement d'un dossier d'exécution pour la fenêtre d'examen.</summary>
public sealed class BenchSession
{
    public required string Directory { get; init; }
    public required List<BenchCaseVm> Cases { get; init; }
    public required AnnotationStore Annotations { get; init; }
    public required List<string> Variants { get; init; }
    public BenchRunManifest? Manifest { get; init; }
    public double TotalCost { get; init; }

    public string AnnotationsPath => Path.Combine(Directory, BenchRunner.AnnotationsFile);

    public static BenchSession Load(string dir)
    {
        var cases = DatasetBuilder.Load(Path.Combine(dir, BenchRunner.CasesFile));
        var all = BenchRunner.LoadResults(Path.Combine(dir, BenchRunner.ResultsFile));
        var annotations = AnnotationStore.Load(Path.Combine(dir, BenchRunner.AnnotationsFile));
        var summary = BenchSummary.Compute(cases, all, annotations, BenchRunner.LoadManifest(dir));
        // Les signalements et accords sont recalculés par la synthèse ; on les reprend ici pour rester cohérent avec le rapport.
        var latest = BenchRunner.Latest(all);
        var vms = new List<BenchCaseVm>();
        foreach (var c in cases)
        {
            var ctx = BenchRunner.BuildContext(c);
            var results = latest.Where(r => r.CaseId == c.CaseId).Select(r => BenchSummary.Recheck(r, c, ctx)).OrderBy(r => r.Variant, StringComparer.Ordinal).ToList();
            vms.Add(new BenchCaseVm
            {
                Case = c,
                Context = ctx,
                Results = results,
                Tags = CaseTagger.Tags(ctx, c.DecisionInTurn),
                Annotated = annotations.Items.Count(a => a.CaseId == c.CaseId),
            });
        }
        return new BenchSession
        {
            Directory = dir,
            Cases = vms,
            Annotations = annotations,
            Variants = summary.Variants.Select(v => v.Variant).ToList(),
            Manifest = BenchRunner.LoadManifest(dir),
            TotalCost = summary.TotalCostUsd,
        };
    }

    public static string RenderState(DecisionContext ctx) => StateText.Render(ctx.State, 12);
}
