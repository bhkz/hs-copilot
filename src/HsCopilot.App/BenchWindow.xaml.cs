using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using HsCopilot.Core.Evaluation;
using Microsoft.Win32;

namespace HsCopilot.App;

/// <summary>Examen des décisions une par une : état, actions légales, coup réel, réponses de chaque variante, annotations.</summary>
public partial class BenchWindow : Window
{
    private static readonly (string Label, Func<BenchCaseVm, bool> Predicate)[] Filters =
    {
        ("Tous les cas", _ => true),
        ("Signalés « douteux »", c => c.HasDubious),
        ("Différents de ton coup", c => c.DisagreesWithActual),
        ("Variantes en désaccord", c => c.VariantsDisagree),
        ("Létal disponible", c => c.Lethal),
        ("Parties classées", c => c.Ranked),
        ("Erreurs ou réponses rejetées", c => c.HasErrors),
        ("Non annotés", c => c.Annotated == 0),
    };

    private BenchSession? _session;
    private BenchCaseVm? _current;

    public BenchWindow(string? runDir)
    {
        InitializeComponent();
        var area = SystemParameters.WorkArea;
        Width = Math.Min(Width, area.Width - 40);
        Height = Math.Min(Height, area.Height - 40);
        foreach (var f in Filters) FilterCombo.Items.Add(f.Label);
        FilterCombo.SelectedIndex = 0;
        if (runDir is not null) Loaded += (_, _) => LoadSession(runDir);
    }

    private void LoadSession(string dir)
    {
        try
        {
            _session = BenchSession.Load(dir);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Impossible de charger {dir} : {ex.Message}", "Banc d'essai", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var m = _session.Manifest;
        TitleText.Text = $"Banc d'essai — {Path.GetFileName(Path.TrimEndingDirectorySeparator(dir))}";
        InfoText.Text = $"{_session.Cases.Count} cas · variantes : {string.Join(", ", _session.Variants)} · coût mesuré {_session.TotalCost.ToString("0.000", CultureInfo.GetCultureInfo("fr-FR"))} $" +
                        (m is null ? "" : $" · instructions {m.InstructionsFingerprint} · stratégie {m.StrategyFingerprint}") +
                        $" · annotations : {_session.Annotations.Items.Count}";
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (_session is null) return;
        var predicate = Filters[Math.Max(0, FilterCombo.SelectedIndex)].Predicate;
        var keep = _current;
        CaseList.ItemsSource = _session.Cases.Where(predicate).ToList();
        CaseList.SelectedItem = keep is not null && CaseList.Items.Contains(keep) ? keep : CaseList.Items.Count > 0 ? CaseList.Items[0] : null;
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilter();

    private void Case_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (CaseList.SelectedItem is not BenchCaseVm vm || _session is null) return;
        _current = vm;
        var c = vm.Case;
        CaseHeader.Text = $"{vm.Title} — {string.Join(", ", vm.Tags)}";
        StateBox.Text = BenchSession.RenderState(vm.Context);
        StateBox.ScrollToHome();

        ActionList.ItemsSource = vm.Context.LegalActions.Select(a =>
        {
            var chosenBy = vm.Results.Where(r => r.ActionId == a.Id).Select(r => r.Variant).ToList();
            var isActual = a.Id == c.Actual?.ActionId;
            var marks = new List<string>();
            if (isActual) marks.Add("← ton coup");
            if (chosenBy.Count > 0) marks.Add("← " + string.Join(", ", chosenBy));
            return new LegalActionVm { Id = a.Id, Label = a.Label, Marks = string.Join("  ", marks), IsActual = isActual, IsChosen = chosenBy.Count > 0 };
        }).ToList();

        var answers = new List<AnswerVm> { AnswerVm.FromActual(c, vm.Context, _session.Annotations.Get(c.CaseId, AnnotationStore.RealMove), OnAnnotationChanged) };
        answers.AddRange(vm.Results.Select(r => AnswerVm.FromResult(r, vm.Context, _session.Annotations.Get(c.CaseId, r.Variant), OnAnnotationChanged)));
        AnswerList.ItemsSource = answers;
    }

    private void OnAnnotationChanged(AnswerVm answer)
    {
        if (_session is null) return;
        var verdict = answer.Verdict;
        var note = string.IsNullOrWhiteSpace(answer.Note) ? null : answer.Note.Trim();
        if (verdict is null && note is not null)
        {
            StatusText.Text = "Choisis Bon, Discutable ou Mauvais pour enregistrer la note.";
            return;
        }
        _session.Annotations.Set(answer.CaseId, answer.Variant, verdict, note);
        try
        {
            _session.Annotations.Save(_session.AnnotationsPath);
            StatusText.Text = $"Annotation enregistrée ({answer.Variant} : {verdict ?? "effacée"}) dans {_session.AnnotationsPath}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Échec de l'enregistrement des annotations : {ex.Message}";
        }
        var vm = _session.Cases.FirstOrDefault(x => x.Case.CaseId == answer.CaseId);
        if (vm is not null) vm.Annotated = _session.Annotations.Items.Count(a => a.CaseId == answer.CaseId);
    }

    private void ClearVerdict_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is AnswerVm answer)
        {
            answer.Verdict = null;
            // Rafraîchit les boutons radio du groupe.
            Case_Selected(this, null!);
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Dossier d'exécution du banc d'essai (contient results.jsonl)" };
        if (dialog.ShowDialog(this) == true) LoadSession(dialog.FolderName);
    }

    private void Report_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        try
        {
            var (_, md) = BenchReport.Write(_session.Directory);
            StatusText.Text = $"Synthèse écrite : {md}";
            Process.Start(new ProcessStartInfo(md) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Synthèse", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
