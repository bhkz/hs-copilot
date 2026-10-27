using System.Diagnostics;
using System.Text;
using HsCopilot.Core.Metrics;
using HsCopilot.Core.State;

namespace HsCopilot.Core.Strategy;

public sealed class StrategyOptions
{
    /// <summary>Délai maximal d'un appel au fournisseur ; au-delà, pas de conseil pour cette révision.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(20);
    /// <summary>Conseils en temps réel en partie classée. Désactivé par défaut (voir docs/CONFORMITE.md).</summary>
    public bool AllowRanked { get; init; }
    /// <summary>Durée d'un tour Hearthstone sans corde, utilisée comme budget indicatif « conseil utile ».</summary>
    public TimeSpan TurnBudget { get; init; } = TimeSpan.FromSeconds(75);
    /// <summary>Attente avant l'appel : si l'état change entre-temps (rafale d'options), aucun appel n'est payé.</summary>
    public TimeSpan Debounce { get; init; } = TimeSpan.Zero;
    /// <summary>
    /// Attente avant l'appel pour une décision qui n'est pas la première du tour : quand le joueur enchaîne plusieurs actions
    /// rapidement, aucun appel n'est envoyé pour les états intermédiaires.
    /// </summary>
    public TimeSpan MidTurnDebounce { get; init; } = TimeSpan.Zero;
    /// <summary>
    /// Coût estimé d'un appel annulé après envoi (jetons d'entrée déjà traités). L'API ne renvoie pas d'usage pour un appel
    /// interrompu : cette estimation prudente est ajoutée aux compteurs de budget.
    /// </summary>
    public double CancelledCallCostUsd { get; init; }
    /// <summary>
    /// Analyses automatiques par tour pour les décisions de milieu de tour. Le début du tour (après la pioche), le mulligan,
    /// les choix et la réanalyse manuelle passent outre ; seuls les budgets de coût les limitent.
    /// </summary>
    public int MaxCallsPerTurn { get; init; } = 4;
    /// <summary>Le début du tour, le mulligan et les choix ne sont jamais bloqués par <see cref="MaxCallsPerTurn"/>.</summary>
    public bool PriorityDecisionsBypassTurnCap { get; init; } = true;
    /// <summary>
    /// Un appel annulé parce que la décision est passée consomme-t-il le quota par tour ? Oui par défaut : le rembourser
    /// couvre un peu plus de décisions, mais coûte environ 45 % d'appels en plus (mesuré le 25/10/2026).
    /// </summary>
    public bool CancelledCallsUseTurnCap { get; init; } = true;
    /// <summary>
    /// N'analyser que des états reçus en direct : pendant un rattrapage (lignes en retard, tampon du plugin), aucun appel
    /// n'est envoyé ; la réanalyse manuelle reste possible.
    /// </summary>
    public bool RequireLiveState { get; init; }
    public int MaxCallsPerGame { get; init; } = 60;
    public double MaxCostPerGameUsd { get; init; } = 1.0;
    public double MaxCostPerSessionUsd { get; init; } = 5.0;
    /// <summary>Coût estimé d'un appel, réservé avant l'envoi pour ne jamais dépasser les plafonds.</summary>
    public double ReservePerCallUsd { get; init; }
    /// <summary>Conseiller aussi le mulligan et les choix (Découverte, Rembobinage).</summary>
    public bool AdviseChoices { get; init; } = true;
    /// <summary>Aucun appel quand la seule action possible est de finir le tour.</summary>
    public bool SkipWhenOnlyEndTurn { get; init; } = true;
    /// <summary>Afficher l'action dès qu'elle est lisible dans le flux de réponse, avant la fin de la génération.</summary>
    public bool UseStreaming { get; init; } = true;
    /// <summary>
    /// Ancien décompte (version de la 2e partie réelle) : les appels prioritaires et manuels consomment aussi le quota de milieu
    /// de tour. Mesure avant/après uniquement.
    /// </summary>
    public bool PriorityCallsUseTurnCap { get; init; }
    /// <summary>Létal démontré par le solveur local : affiché aussitôt, sans appel au modèle (gratuit, instantané).</summary>
    public bool LocalLethal { get; init; } = true;
    /// <summary>
    /// Conseil suivi et rien de nouveau (aucune carte piochée ou générée, aucun serviteur adverse apparu) : l'étape suivante
    /// du plan, si elle est légale, est affichée sans nouvel appel.
    /// </summary>
    public bool ContinuePlan { get; init; } = true;
    /// <summary>
    /// Tu joues l'action du début de réponse avant la fin de l'appel, et rien d'imprévu n'apparaît : changement attendu par
    /// le plan, l'appel n'est pas annulé. Sa réponse complète apporte la suite du plan, reprise sans nouvel appel si elle est
    /// légale ; sinon, un nouvel appel part comme avant.
    /// </summary>
    public bool KeepCallWhenPartialPlayed { get; init; } = true;
}

/// <summary>
/// Synchronisation d'un conseil avec l'état : quelle révision a été analysée, ce qu'elle contenait (pioche du tour),
/// quand l'appel est parti et l'âge du conseil à l'affichage.
/// </summary>
public sealed record AdvisorDiagnostics
{
    public int AnalyzedRevision { get; init; }
    /// <summary>Heure (du log) de la dernière ligne de l'état analysé.</summary>
    public string? StateLogTime { get; init; }
    /// <summary>Réception de l'état analysé par le coordinateur.</summary>
    public DateTime StateReceivedUtc { get; init; }
    /// <summary>Cartes arrivées en main pendant ce tour, présentes dans l'état analysé.</summary>
    public List<string> DrawnThisTurn { get; init; } = new();
    public int HandCount { get; init; }
    /// <summary>Envoi effectif au fournisseur, après l'attente anti-rafale (null : aucun appel).</summary>
    public DateTime? SentUtc { get; init; }
    /// <summary>Délai entre la réception de l'état analysé et l'affichage du conseil.</summary>
    public double? AgeMs { get; init; }

    public static AdvisorDiagnostics From(DecisionContext ctx) => new()
    {
        AnalyzedRevision = ctx.Revision,
        StateLogTime = ctx.State.LastLogTime,
        StateReceivedUtc = ctx.CreatedAtUtc,
        DrawnThisTurn = ctx.Kind == DecisionContext.KindMulligan ? new()
            : ctx.State.Local?.Hand.Cards.Where(c => c.EnteredTurn == ctx.GameTurn).Select(c => c.Name ?? c.CardId ?? "carte inconnue").ToList() ?? new(),
        HandCount = ctx.State.Local?.Hand.Cards.Count ?? 0,
    };

    public AdvisorDiagnostics Shown(DateTime nowUtc, DateTime? sentUtc = null) => this with { SentUtc = sentUtc, AgeMs = (nowUtc - StateReceivedUtc).TotalMilliseconds };
}

/// <summary>Raison d'un état sans conseil, pour un titre explicite dans l'overlay.</summary>
public static class AdvisorReasons
{
    public const string Quota = "quota";
    public const string Budget = "budget";
    public const string GameLimit = "limite-partie";
    public const string EndTurnOnly = "fin-de-tour";
    public const string CatchUp = "rattrapage";
    public const string Incomplete = "incomplet";
    public const string Ranked = "classé";
    public const string NoAction = "aucune-action";
    public const string Leak = "fuite";
    public const string OpponentTurn = "tour-adverse";
    public const string GameOver = "fin-de-partie";
    public const string Timeout = "délai";
    public const string ProviderError = "erreur";
}

/// <summary>Origine d'une recommandation affichée.</summary>
public static class AdviceOrigins
{
    public const string Model = "modèle";
    /// <summary>Létal démontré par le solveur local, sans appel.</summary>
    public const string Solver = "solveur";
    /// <summary>Étape suivante du plan précédent, sans appel.</summary>
    public const string Plan = "plan";
}

public static class AdvisorPhases
{
    public const string Idle = "inactif";
    public const string Analyzing = "analyse";
    public const string Partial = "partiel";
    public const string Ready = "prêt";
    public const string Stale = "périmé";
    public const string Skipped = "ignoré";
    public const string Error = "erreur";
    public const string Paused = "pause";
    public const string Disabled = "désactivé";
}

/// <summary>Ce que l'interface doit afficher maintenant. Chaque mise à jour remplace entièrement la précédente.</summary>
public sealed record AdvisorUpdate
{
    /// <summary>Numéro d'ordre attribué sous verrou : une mise à jour plus ancienne qu'une autre déjà affichée est ignorée.</summary>
    public long Seq { get; init; }
    public required string Phase { get; init; }
    public string Message { get; init; } = "";
    /// <summary>Raison d'un état sans conseil (voir <see cref="AdvisorReasons"/>), null sinon.</summary>
    public string? Reason { get; init; }
    public AdvisorDiagnostics? Diagnostics { get; init; }
    /// <summary>
    /// Le serveur a commencé ton tour mais Hearthstone affiche encore le tour adverse (horloge PowerTaskList) : le conseil
    /// porte sur ton vrai tour, pioche comprise, mais n'est pas encore jouable à l'écran.
    /// </summary>
    public bool AwaitingDisplay { get; init; }
    /// <summary>Depuis quand l'affichage est attendu (repli honnête si l'horloge cesse de progresser).</summary>
    public DateTime? AwaitingSinceUtc { get; init; }
    /// <summary>Vrai si l'horloge de l'affichage (PowerTaskList) est suivie dans cette partie.</summary>
    public bool DisplayTracked { get; init; }
    public string? GameId { get; init; }
    public int Revision { get; init; }
    public string? DecisionKind { get; init; }
    /// <summary>Recommandation validée et encore valable (phase prêt).</summary>
    public Recommendation? Recommendation { get; init; }
    /// <summary>Début de réponse en cours de génération (phase partiel) : action déjà contrôlée comme légale.</summary>
    public LegalAction? PartialAction { get; init; }
    public StrategyPartial? Partial { get; init; }
    public LethalAssessment? Lethal { get; init; }
    public IReadOnlyList<QuickFact> Facts { get; init; } = Array.Empty<QuickFact>();
    public DateTime? AnalysisStartedUtc { get; init; }
    public int CallsThisTurn { get; init; }
    /// <summary>Appels automatiques de milieu de tour (les seuls limités par le quota par tour).</summary>
    public int MidTurnCallsThisTurn { get; init; }
    public int MaxCallsPerTurn { get; init; }
    public int CallsThisGame { get; init; }
    public double CostThisGameUsd { get; init; }
    public double CostSessionUsd { get; init; }
    public bool Paused { get; init; }
    public bool Manual { get; init; }
    /// <summary>État observable au moment de la mise à jour (pour l'affichage des cibles), jamais transmis ailleurs.</summary>
    public GameStateSnapshot? State { get; init; }
}

/// <summary>
/// Coordonne les appels au fournisseur de stratégie sans jamais bloquer l'appelant.
/// </summary>
/// <remarks>
/// Déclenchement : mulligan, choix (Découverte, Rembobinage), options fraîches pendant ton tour (début de tour après la pioche,
/// puis après chaque action qui modifie les actions légales), réanalyse manuelle. Pas d'appel si : partie classée (par défaut),
/// état incomplet, pause, seule action « finir le tour », décision équivalente déjà conseillée, plafonds d'appels ou de coût.
/// Séquence d'un appel : contexte construit depuis l'état publié → garde-fou anti-fuite → attente courte (rafales) →
/// appel asynchrone avec délai, en flux si possible → l'état est-il toujours le même ? → validation → publication.
/// Une réponse arrivée après un changement d'état est écartée, jamais affichée comme valable.
/// </remarks>
public sealed class StrategyCoordinator : IAsyncDisposable
{
    private readonly IStrategyProvider _provider;
    private readonly StrategyOptions _options;
    private readonly Func<MetricsRegistry> _metrics;
    private readonly object _lock = new();
    private CancellationTokenSource? _inFlight;
    private Task _inFlightTask = Task.CompletedTask;
    private string? _inFlightKey;
    private string? _inFlightSignature;
    private DecisionContext? _inFlightCtx;
    private DateTime _inFlightStartedUtc;
    private CancellationTokenSource? _partialShownFor;
    /// <summary>Action du début de réponse de l'appel en cours (null tant qu'aucun fragment légal n'est arrivé).</summary>
    private LegalAction? _inFlightPartialAction;
    /// <summary>Tu as joué l'action du début de réponse : l'appel en cours ne conseille plus rien, il apporte la suite du plan.</summary>
    private bool _inFlightCarriesPlan;
    private string? _requestedKey;
    private GameStateSnapshot? _latest;
    private long _turnStartTimestamp;
    private int _turnStartGameTurn = -1;
    private bool _firstAdviceThisTurn, _firstShownThisTurn;
    private string? _gameId;
    private int _callsThisTurn, _callsThisGame, _callsSession, _decisionsThisTurn;
    /// <summary>Appels automatiques de milieu de tour : seuls ceux-là sont limités par <see cref="StrategyOptions.MaxCallsPerTurn"/>.</summary>
    private int _midTurnCallsThisTurn;
    /// <summary>État sur lequel <see cref="_lastRecommendation"/> a été calculée (pour reprendre son plan).</summary>
    private GameStateSnapshot? _lastRecommendationState;
    private AdvisorDiagnostics? _currentDiag;
    private DateTime? _inFlightSentUtc;
    /// <summary>Tour et étape exécutés par le client (PowerTaskList), null tant qu'aucun changement de tour n'a été vu.</summary>
    private int? _visibleTurn;
    private string? _visibleStep;
    private DateTime? _awaitingSinceUtc;
    private int _awaitingTurn = -1;
    /// <summary>Heure du log (GameState) du premier état de chaque tour local : mesure du retard d'affichage.</summary>
    private readonly Dictionary<int, TimeSpan> _serverTurnStart = new();
    private double _costThisGame, _costSession;
    private bool _paused;
    private string? _lastSignature;
    private Recommendation? _lastRecommendation;
    private LethalAssessment? _currentLethal;
    private IReadOnlyList<QuickFact> _currentFacts = Array.Empty<QuickFact>();
    private string? _lastIdleMessage;
    private string? _lastStaleReason;
    private AdvisorUpdate _current = new() { Phase = AdvisorPhases.Idle, Message = "En attente d'une partie." };
    private long _updateSeq;
    private long _lastRaisedSeq;
    private readonly object _raiseLock = new();

    public StrategyCoordinator(IStrategyProvider provider, StrategyOptions? options = null, Func<MetricsRegistry>? metrics = null)
    {
        _provider = provider;
        _options = options ?? new StrategyOptions();
        var own = new MetricsRegistry();
        _metrics = metrics ?? (() => own);
        Board.Changed += OnBoardChanged;
    }

    public AdviceBoard Board { get; } = new();
    public StrategyOptions Options => _options;
    public string ProviderName => _provider.Name;

    /// <summary>Message court à chaque événement notable (ignoré, délai, erreur…).</summary>
    public event Action<string>? Status;

    /// <summary>Nouvel état à afficher. Appelé hors verrou, depuis n'importe quel fil.</summary>
    public event Action<AdvisorUpdate>? Updated;

    public AdvisorUpdate Current { get { lock (_lock) return _current; } }
    public bool Paused { get { lock (_lock) return _paused; } }
    public double CostSessionUsd { get { lock (_lock) return _costSession; } }
    public int CallsSession { get { lock (_lock) return _callsSession; } }

    private static string KeyOf(GameStateSnapshot s) => $"{s.GameId}:{s.Revision}:{s.LegalOptions?.OptionsId}";

    private string? DecisionKey(GameStateSnapshot? s)
    {
        if (s is null) return null;
        if (_options.AdviseChoices && DecisionContextBuilder.IsChoicePoint(s)) return $"{s.GameId}:c{s.PendingChoice!.Id}:{s.Game.ResetCount}";
        if (DecisionContextBuilder.IsDecisionPoint(s)) return KeyOf(s);
        return null;
    }

    /// <summary>À appeler pour chaque état publié. Ne bloque pas : l'appel au fournisseur part sur le pool de fils.</summary>
    public void OnSnapshot(GameStateSnapshot s) => Evaluate(s, manual: false);

    /// <summary>Réanalyse demandée par le joueur : ignore la pause, la limite par tour et la déduplication, pas les budgets.</summary>
    public bool RequestAnalysis()
    {
        GameStateSnapshot? s;
        lock (_lock)
        {
            s = _latest;
            _requestedKey = null;
        }
        if (s is null) return false;
        return Evaluate(s, manual: true);
    }

    public void SetPaused(bool paused)
    {
        GameStateSnapshot? latest;
        AdvisorUpdate? update = null;
        lock (_lock)
        {
            if (_paused == paused) return;
            _paused = paused;
            latest = _latest;
            _requestedKey = null;
            if (paused)
            {
                if (_inFlight is not null) CancelInFlightLocked("stratégie : appels annulés (pause)");
                update = Make(AdvisorPhases.Paused, "Conseiller en pause : aucun appel automatique (Ctrl+Alt+R pour une analyse ponctuelle).", latest);
            }
        }
        if (update is not null) Raise(update);
        else if (latest is not null) Evaluate(latest, manual: false);
    }

    private bool Evaluate(GameStateSnapshot s, bool manual)
    {
        Board.OnSnapshot(s);
        AdvisorUpdate? update = null;
        Recommendation? reuse = null;
        AdvisorUpdate? reuseUpdate = null;
        var started = false;
        lock (_lock)
        {
            _latest = s;
            if (s.GameId != _gameId)
            {
                _gameId = s.GameId;
                _callsThisGame = 0;
                _costThisGame = 0;
                _lastSignature = null;
                _lastRecommendation = null;
                _lastRecommendationState = null;
                _serverTurnStart.Clear();
                _awaitingTurn = -1;
                _awaitingSinceUtc = null;
            }
            // Premier joueur : le serveur lui donne la main au tour 1 dès le mulligan ; son tour ne commence qu'après.
            var localTurn = s.Game.ActivePlayer == "local" && s.Game.MulliganDone;
            if (localTurn && !_serverTurnStart.ContainsKey(s.Game.GameTurn))
                _serverTurnStart[s.Game.GameTurn] = s.LastLogTimeOfDay;
            if (localTurn && s.Game.GameTurn != _turnStartGameTurn)
            {
                _turnStartGameTurn = s.Game.GameTurn;
                _turnStartTimestamp = Stopwatch.GetTimestamp();
                _firstAdviceThisTurn = false;
                _firstShownThisTurn = false;
                _callsThisTurn = 0;
                _midTurnCallsThisTurn = 0;
                _decisionsThisTurn = 0;
            }

            var key = DecisionKey(s);
            if (key is null)
            {
                if (_inFlight is not null)
                {
                    if (_options.KeepCallWhenPartialPlayed && PlayedAsAdvisedLocked(s))
                    {
                        // Changement attendu : tu as joué l'action du début de réponse. La réponse complète apportera la suite.
                        if (!_inFlightCarriesPlan) _metrics().Increment("stratégie : appels conservés (début de réponse joué)");
                        _inFlightCarriesPlan = true;
                    }
                    else
                    {
                        // La décision analysée est passée (autre action, choix fait, fin de tour) : sa réponse serait périmée.
                        CancelInFlightLocked(_inFlightCarriesPlan ? "stratégie : appels annulés (changement imprévu)" : "stratégie : appels annulés (décision passée)");
                    }
                }
                if (!manual && Board.Current is null) update = IdleUpdate(s);
                goto Done;
            }
            if (!manual && key == _requestedKey) goto Done;
            _requestedKey = key;
            _lastIdleMessage = null;
            _lastStaleReason = null;
            _decisionsThisTurn++;

            if (s.Uncertainty.Incomplete) { update = SkipLocked(s, "état incomplet : aucun conseil", AdvisorPhases.Skipped, AdvisorReasons.Incomplete); goto Done; }
            if (s.Game.GameType == "GT_RANKED" && !_options.AllowRanked)
            {
                update = SkipLocked(s, "conseils en temps réel désactivés en partie classée", AdvisorPhases.Disabled, AdvisorReasons.Ranked);
                goto Done;
            }
            if (_paused && !manual) { update = Make(AdvisorPhases.Paused, "Conseiller en pause.", s); goto Done; }
            if (_options.RequireLiveState && !s.LiveTimestamps && !manual)
            {
                update = SkipLocked(s, "rattrapage du flux en cours : analyse au prochain état reçu en direct", AdvisorPhases.Skipped, AdvisorReasons.CatchUp);
                goto Done;
            }

            var choice = _options.AdviseChoices && DecisionContextBuilder.IsChoicePoint(s);
            var ctx = choice ? DecisionContextBuilder.BuildChoice(s) : DecisionContextBuilder.Build(s);
            if (ctx is null || ctx.LegalActions.Count == 0) { update = SkipLocked(s, "aucune action légale", AdvisorPhases.Skipped, AdvisorReasons.NoAction); goto Done; }
            _currentLethal = QuickAnalysis.AssessLethal(ctx);
            _currentFacts = QuickAnalysis.Analyze(ctx, _currentLethal);
            _currentDiag = AdvisorDiagnostics.From(ctx);
            if (!manual && _options.SkipWhenOnlyEndTurn && ctx.Kind == DecisionContext.KindAction && ctx.LegalActions.All(a => a.Kind == "end-turn"))
            {
                update = SkipLocked(s, "seule action possible : finir le tour", AdvisorPhases.Skipped, AdvisorReasons.EndTurnOnly);
                goto Done;
            }
            // Létal démontré par le solveur : affiché tout de suite, sans appel (la réanalyse manuelle interroge quand même le modèle).
            if (!manual && _options.LocalLethal && LocalLethalRecommendation(ctx, _currentLethal) is { } lethalRec)
            {
                if (_inFlight is not null) CancelInFlightLocked("stratégie : appels annulés (létal démontré)");
                _metrics().Increment("stratégie : létaux démontrés affichés sans appel");
                reuse = lethalRec;
                reuseUpdate = ReadyUpdateLocked(lethalRec) with { Diagnostics = _currentDiag.Shown(DateTime.UtcNow) };
                _lastRecommendation = lethalRec;
                _lastRecommendationState = s;
                _lastSignature = Signature(ctx);
                goto Done;
            }
            // Conseil suivi, rien de nouveau : étape suivante du plan, sans appel.
            if (!manual && _options.ContinuePlan && ctx.Kind == DecisionContext.KindAction && ContinuePlanLocked(ctx) is { } planRec)
            {
                if (_inFlight is not null) CancelInFlightLocked("stratégie : appels annulés (suite du plan)");
                _metrics().Increment("stratégie : suites de plan affichées sans appel");
                reuse = planRec;
                reuseUpdate = ReadyUpdateLocked(planRec) with { Diagnostics = _currentDiag.Shown(DateTime.UtcNow) };
                _lastRecommendation = planRec;
                _lastRecommendationState = s;
                _lastSignature = Signature(ctx);
                goto Done;
            }
            // Suite du plan attendue de l'appel en cours : pas de nouvel appel tant que rien d'imprévu n'est apparu.
            if (!manual && _inFlight is not null && _inFlightCarriesPlan)
            {
                if (ctx.Kind == DecisionContext.KindAction && PlayedAsAdvisedLocked(s))
                {
                    _metrics().Increment("stratégie : décisions en attente de la suite du plan");
                    update = Make(AdvisorPhases.Analyzing, "Coup conseillé joué : suite du plan attendue de l'analyse en cours…", s) with
                    {
                        DecisionKind = ctx.Kind,
                        AnalysisStartedUtc = _inFlightStartedUtc,
                    };
                    goto Done;
                }
                CancelInFlightLocked("stratégie : appels annulés (changement imprévu)");
            }
            if (ctx.Kind == DecisionContext.KindAction)
            {
                ctx.LocalAnalysis = QuickAnalysis.LocalAnalysisPayload(ctx, _currentLethal);
                ctx.PreviousAdvice = PreviousAdviceLocked(ctx);
            }
            var leaks = PayloadLeakGuard.FindLeaks(ctx.ToPayloadJson(), s.ForbiddenCardIds);
            if (leaks.Count > 0)
            {
                _metrics().Increment("stratégie : envois bloqués (fuite)");
                update = SkipLocked(s, $"envoi bloqué : le message contiendrait une carte cachée ({string.Join(", ", leaks)})", AdvisorPhases.Error, AdvisorReasons.Leak);
                goto Done;
            }

            var signature = Signature(ctx);
            if (!manual && _lastRecommendation is not null && signature == _lastSignature && Board.Current is null
                && _lastRecommendation.RebindTo(ctx) is { } rebound)
            {
                // Options renvoyées à l'identique sous un nouvel identifiant : même conseil, sans nouvel appel.
                reuse = rebound;
                reuseUpdate = ReadyUpdateLocked(rebound);
                _lastRecommendation = rebound;
                _metrics().Increment("stratégie : conseils réutilisés sans appel");
                goto Done;
            }
            if (!manual && _inFlight is not null && signature == _inFlightSignature)
            {
                // Même décision que l'appel en cours : on le laisse finir et on rattachera sa réponse à ce nouvel état.
                _inFlightKey = key;
                _inFlightCtx = ctx;
                goto Done;
            }
            // Début de tour, mulligan et choix : prioritaires, ils ne consomment pas le quota de milieu de tour.
            var priority = (ctx.Kind != DecisionContext.KindAction || _decisionsThisTurn == 1) && _options.PriorityDecisionsBypassTurnCap;
            var countsTowardCap = _options.PriorityCallsUseTurnCap || (!manual && !priority);
            if (!manual && !priority && _midTurnCallsThisTurn >= _options.MaxCallsPerTurn)
            {
                update = SkipLocked(s, $"{_midTurnCallsThisTurn} analyses automatiques de milieu de tour déjà faites : Ctrl+Alt+R pour réanalyser ; l'analyse rapide ci-dessous reste à jour", AdvisorPhases.Skipped, AdvisorReasons.Quota);
                goto Done;
            }
            if (_callsThisGame >= _options.MaxCallsPerGame)
            {
                update = SkipLocked(s, $"limite de {_options.MaxCallsPerGame} analyses pour cette partie atteinte", AdvisorPhases.Skipped, AdvisorReasons.GameLimit);
                goto Done;
            }
            if (_costThisGame + _options.ReservePerCallUsd > _options.MaxCostPerGameUsd)
            {
                update = SkipLocked(s, $"budget de la partie atteint ({_costThisGame:0.000} $ sur {_options.MaxCostPerGameUsd:0.00} $)", AdvisorPhases.Skipped, AdvisorReasons.Budget);
                goto Done;
            }
            if (_costSession + _options.ReservePerCallUsd > _options.MaxCostPerSessionUsd)
            {
                update = SkipLocked(s, $"budget de la session atteint ({_costSession:0.000} $ sur {_options.MaxCostPerSessionUsd:0.00} $)", AdvisorPhases.Skipped, AdvisorReasons.Budget);
                goto Done;
            }

            if (_inFlight is not null) CancelInFlightLocked("stratégie : appels annulés (état modifié)");
            var cts = new CancellationTokenSource();
            _inFlight = cts;
            _inFlightKey = key;
            _inFlightSignature = signature;
            _inFlightCtx = ctx;
            _inFlightStartedUtc = DateTime.UtcNow;
            _inFlightSentUtc = null;
            _inFlightPartialAction = null;
            _inFlightCarriesPlan = false;
            var decisionTimestamp = Stopwatch.GetTimestamp();
            _metrics().Increment("stratégie : points de décision");
            if (manual) _metrics().Increment("stratégie : réanalyses manuelles");
            var debounce = ctx.Kind == DecisionContext.KindAction && _decisionsThisTurn > 1 && !manual
                ? (_options.MidTurnDebounce > _options.Debounce ? _options.MidTurnDebounce : _options.Debounce)
                : manual ? TimeSpan.Zero : _options.Debounce;
            _inFlightTask = Task.Run(() => RunAsync(ctx, cts, decisionTimestamp, debounce, countsTowardCap));
            started = true;
            update = Make(AdvisorPhases.Analyzing, manual ? "Réanalyse demandée…" : "Analyse en cours…", s) with
            {
                DecisionKind = ctx.Kind,
                AnalysisStartedUtc = _inFlightStartedUtc,
                Manual = manual,
            };
        }
    Done:
        if (reuse is not null) PublishChecked(reuse, reuseUpdate!, s);
        if (update is not null) Raise(update);
        return started;
    }

    /// <summary>
    /// Annule l'appel en cours et l'oublie aussitôt : sa réponse éventuelle sera écartée (elle ne correspond plus à rien) et
    /// un nouvel appel ne le recomptera pas. Le jeton est libéré par RunAsync.
    /// </summary>
    private void CancelInFlightLocked(string metric)
    {
        if (_inFlight is null) return;
        _inFlight.Cancel();
        _metrics().Increment(metric);
        _inFlight = null;
        _inFlightSignature = null;
        _inFlightKey = null;
        _inFlightCtx = null;
        _inFlightPartialAction = null;
        _inFlightCarriesPlan = false;
    }

    /// <summary>Changement attendu par le plan pour l'appel en cours : voir <see cref="UnexpectedChange"/>.</summary>
    private bool PlayedAsAdvisedLocked(GameStateSnapshot s) =>
        _inFlightCtx is { } ctx && _inFlightPartialAction is { } advised && UnexpectedChange(ctx, advised, s) is null;

    /// <summary>
    /// Changement attendu par le plan (null) : depuis l'état analysé, tu as joué une seule action, celle du début de réponse,
    /// et rien de nouveau n'est apparu (<see cref="NothingNew"/>). Sinon, la raison du changement imprévu : autre action,
    /// pioche ou carte générée, serviteur ou Secret apparu, Secret déclenché, choix à faire, fin du tour.
    /// </summary>
    public static string? UnexpectedChange(DecisionContext analysed, LegalAction advised, GameStateSnapshot now)
    {
        if (analysed.Kind != DecisionContext.KindAction) return "pas une décision d'action";
        if (now.GameId != analysed.GameId || now.Game.ResetCount != analysed.ResetCount) return "partie différente ou rembobinée";
        if (now.Game.GameOver) return "partie terminée";
        if (now.Game.GameTurn != analysed.GameTurn || now.Game.ActivePlayer != "local") return "fin du tour";
        if (now.PendingChoice is not null) return "choix à faire";
        var played = now.History.Where(h => h.Seq > analysed.HistorySeq && h.Player == "local" && h.Kind is "play" or "attack" or "hero-power" or "location").ToList();
        if (played.Count == 0) return "aucune action jouée";
        if (played.Count > 1) return "plusieurs actions jouées";
        if (played[0].SourceEntityId != advised.SourceEntityId || (advised.TargetEntityId is int t && played[0].TargetEntityId != t))
            return "autre action que le début de réponse";
        if (!NothingNew(analysed.State, now)) return "information nouvelle (carte en main, serviteur, Secret ou arme)";
        if (UnplannedEffect(analysed.State, now, advised) is { } effect) return "effet non prévu : " + effect;
        return null;
    }

    /// <summary>
    /// Effet que le plan ne connaissait pas : la suite peut rester légale sans rester pertinente. Seules l'entité qui agit et
    /// sa cible (et ton héros pour un pouvoir héroïque) peuvent changer librement. Ailleurs, sont imprévus :
    /// <list type="bullet">
    /// <item>un serviteur blessé, mort ou renvoyé, ou dont un statut change (Gel, Bouclier divin, Provocation, Silence…) ;</item>
    /// <item>le héros adverse blessé, soigné, cuirassé, gelé ou insensible (le létal en dépend) ;</item>
    /// <item>ton héros blessé ou qui perd de l'armure ;</item>
    /// <item>une attaque ou des PV maximum modifiés sans qu'aucun serviteur concerné par l'action n'arrive ni ne parte.</item>
    /// </list>
    /// Acceptés : ton héros soigné ou cuirassé (Wyrm du pic venteux : « vous gagnez 5 points d'armure ») et une aura qui
    /// apparaît ou disparaît avec le serviteur joué ou tué (Oracle sinistrécaille). Null si rien d'imprévu.
    /// </summary>
    public static string? UnplannedEffect(GameStateSnapshot before, GameStateSnapshot now, LegalAction played)
    {
        var involved = new HashSet<int>();
        if (played.SourceEntityId is int source) involved.Add(source);
        if (played.TargetEntityId is int target) involved.Add(target);
        if (played.Kind == "hero-power" && before.Local?.Hero is { } own) involved.Add(own.EntityId);
        // Aura possible : un serviteur concerné par l'action arrive sur le plateau ou le quitte.
        var boardBefore = (before.Local?.Board ?? new()).Concat(before.Opponent?.Board ?? new()).Select(m => m.EntityId).ToHashSet();
        var boardNow = (now.Local?.Board ?? new()).Concat(now.Opponent?.Board ?? new()).Select(m => m.EntityId).ToHashSet();
        var auraChange = involved.Any(id => boardBefore.Contains(id) != boardNow.Contains(id));

        if (before.Local?.Hero is { } lb && now.Local?.Hero is { } ln && !involved.Contains(lb.EntityId)
            && (ln.Health < lb.Health || ln.Armor < lb.Armor || lb.Frozen != ln.Frozen || lb.Immune != ln.Immune))
            return $"ton héros a changé ({lb.Health} PV + {lb.Armor} → {ln.Health} PV + {ln.Armor})";
        if (before.Opponent?.Hero is { } ob && now.Opponent?.Hero is { } on && !involved.Contains(ob.EntityId)
            && (ob.Health != on.Health || ob.Armor != on.Armor || ob.Frozen != on.Frozen || ob.Immune != on.Immune))
            return $"le héros adverse a changé ({ob.Health} PV + {ob.Armor} → {on.Health} PV + {on.Armor})";
        foreach (var m in (before.Local?.Board ?? new()).Concat(before.Opponent?.Board ?? new()))
        {
            if (involved.Contains(m.EntityId)) continue;
            var after = (now.Local?.Board ?? new()).Concat(now.Opponent?.Board ?? new()).FirstOrDefault(x => x.EntityId == m.EntityId);
            if (after is null) return $"{m.Name} a quitté le plateau";
            if (after.Damage != m.Damage) return $"{m.Name} a subi ou perdu des dégâts ({m.Attack}/{m.Health} → {after.Attack}/{after.Health})";
            if (!after.Keywords.ToHashSet().SetEquals(m.Keywords)) return $"{m.Name} a changé de statut";
            if ((after.Attack != m.Attack || after.MaxHealth != m.MaxHealth) && !auraChange)
                return $"{m.Name} a changé ({m.Attack}/{m.Health} → {after.Attack}/{after.Health})";
        }
        return null;
    }

    private AdvisorUpdate? IdleUpdate(GameStateSnapshot s)
    {
        var reason = s.Game.GameOver ? AdvisorReasons.GameOver : s.Game.ActivePlayer == "opponent" ? AdvisorReasons.OpponentTurn : null;
        var message = s.Game.GameOver ? "Partie terminée."
            : s.Game.ActivePlayer == "opponent" ? "Tour adverse : en attente de ton tour."
            : s.Game.ActivePlayer == "local" ? (_lastStaleReason is null ? "En attente des actions possibles…" : $"Conseil retiré : {_lastStaleReason}. En attente des nouvelles actions possibles…")
            : "En attente d'une décision.";
        if (message == _lastIdleMessage) return null;
        _lastIdleMessage = message;
        _currentFacts = Array.Empty<QuickFact>();
        _currentLethal = null;
        _currentDiag = null;
        return Make(AdvisorPhases.Idle, message, s) with { Reason = reason };
    }

    private AdvisorUpdate SkipLocked(GameStateSnapshot s, string why, string phase, string? reason = null)
    {
        _metrics().Increment("stratégie : points sans conseil");
        Board.Record("skipped", why, s.Revision);
        Status?.Invoke(why);
        return Make(phase, why, s) with { Reason = reason };
    }

    /// <summary>Recommandation locale pour un létal démontré : première étape légale maintenant, suite calculée.</summary>
    public static Recommendation? LocalLethalRecommendation(DecisionContext ctx, LethalAssessment? lethal)
    {
        if (ctx.Kind != DecisionContext.KindAction || lethal is not { Level: LethalLevels.Certain, Solver: { } solver }) return null;
        if (solver.Sequence.Count == 0 || solver.Sequence[0].Action is not { } first) return null;
        if (ctx.LegalActions.All(a => a.Id != first.Id)) return null;
        return new Recommendation
        {
            Kind = DecisionContext.KindAction,
            GameId = ctx.GameId,
            Revision = ctx.Revision,
            OptionsId = ctx.OptionsId,
            GameTurn = ctx.GameTurn,
            ResetCountAtCreation = ctx.ResetCount,
            HistorySeqAtCreation = ctx.HistorySeq,
            Action = first,
            Uncertainty = UncertaintyLevel.Faible,
            UncertaintyReasons = new() { "calcul local sur l'état visible, aucun Secret adverse ; effets non simulés exclus" },
            Summary = $"Létal démontré : {solver.Sequence[0].Label}, puis la suite.",
            Why = $"{solver.MaxFaceDamage} dégâts démontrés contre {solver.Needed} PV",
            Lethal = LethalLevels.Certain,
            LethalNote = lethal.Label,
            FollowUp = solver.Sequence.Skip(1).Select(x => new PlannedStep { Description = x.Label, SourceEntityId = x.SourceEntityId, TargetEntityId = x.TargetEntityId, Status = "calculé" }).ToList(),
            Rationale = "Solveur local : " + solver.SequenceText,
            Provider = "solveur local",
            Origin = AdviceOrigins.Solver,
            LatencyMs = solver.ElapsedMs,
            CostUsd = 0,
        };
    }

    /// <summary>
    /// Étape suivante du dernier conseil, sans appel : ce conseil a été suivi (une seule action jouée depuis, la sienne),
    /// rien de nouveau n'est apparu (main, plateau adverse, Secrets, arme adverse) et l'étape existe dans les options du serveur.
    /// </summary>
    private Recommendation? ContinuePlanLocked(DecisionContext ctx)
    {
        var prev = _lastRecommendation;
        var before = _lastRecommendationState;
        var s = ctx.State;
        if (prev is null || before is null || prev.Kind != DecisionContext.KindAction || prev.GameId != ctx.GameId
            || prev.GameTurn != ctx.GameTurn || prev.ResetCountAtCreation != ctx.ResetCount || prev.FollowUp.Count == 0) return null;
        var played = s.History.Where(h => h.Seq > prev.HistorySeqAtCreation && h.Player == "local" && h.Kind is "play" or "attack" or "hero-power" or "location").ToList();
        if (played.Count != 1 || played[0].SourceEntityId != prev.Action.SourceEntityId
            || (prev.Action.TargetEntityId is int t && played[0].TargetEntityId != t)) return null;
        if (!NothingNew(before, s) || UnplannedEffect(before, s, prev.Action) is not null) return null;
        var step = prev.FollowUp[0];
        if (step.SourceEntityId is not int source) return null;
        var next = ctx.LegalActions.FirstOrDefault(a => a.Kind != "end-turn" && a.SourceEntityId == source
            && (step.TargetEntityId is int target ? a.TargetEntityId == target : a.TargetEntityId is null));
        if (next is null) return null;
        var output = new StrategyOutput
        {
            ActionId = next.Id,
            Summary = step.Description,
            Why = prev.Why,
            Lethal = prev.Lethal,
            Confidence = prev.Uncertainty switch { UncertaintyLevel.Faible => "élevée", UncertaintyLevel.Moyenne => "moyenne", _ => "faible" },
            PlanNextTurn = prev.PlanNextTurn,
            Rationale = prev.Rationale,
            Risk = prev.Risk,
            FollowUp = prev.FollowUp.Skip(1).Select(f => new FollowUpStep { Text = f.Description, SourceEntityId = f.SourceEntityId, TargetEntityId = f.TargetEntityId }).ToList(),
        };
        var outcome = RecommendationValidator.Validate(output, ctx, prev.Provider, prev.Model, 0, 0, _currentLethal);
        if (outcome.Recommendation is not { } rec) return null;
        rec.Origin = prev.Origin == AdviceOrigins.Solver ? AdviceOrigins.Solver : AdviceOrigins.Plan;
        return rec;
    }

    /// <summary>Aucune information nouvelle entre deux états du même tour (seule la carte jouée a quitté la main).</summary>
    public static bool NothingNew(GameStateSnapshot before, GameStateSnapshot now)
    {
        var handBefore = before.Local?.Hand.Cards.Select(c => c.EntityId).ToHashSet() ?? new();
        if (now.Local?.Hand.Cards.Any(c => !handBefore.Contains(c.EntityId)) == true) return false;
        var oppBefore = before.Opponent?.Board.Select(m => m.EntityId).ToHashSet() ?? new();
        if (now.Opponent?.Board.Any(m => !oppBefore.Contains(m.EntityId)) == true) return false;
        var localBefore = (before.Local?.Board.Select(m => m.EntityId) ?? Array.Empty<int>()).Concat(before.Local?.Hand.Cards.Select(c => c.EntityId) ?? Array.Empty<int>()).ToHashSet();
        if (now.Local?.Board.Any(m => !localBefore.Contains(m.EntityId)) == true) return false;
        // Secret posé, déclenché ou révélé : imprévu.
        if ((now.Opponent?.Secrets.Count ?? 0) != (before.Opponent?.Secrets.Count ?? 0)) return false;
        if (now.Opponent?.Hand.Count != before.Opponent?.Hand.Count) return false;
        if (now.Opponent?.Weapon?.EntityId != before.Opponent?.Weapon?.EntityId) return false;
        if (now.Local?.Weapon?.EntityId != before.Local?.Weapon?.EntityId) return false;
        return true;
    }

    /// <summary>Conseil précédent du même tour, joint à l'appel pour que le modèle reste cohérent avec son plan.</summary>
    private object? PreviousAdviceLocked(DecisionContext ctx)
    {
        var prev = _lastRecommendation;
        if (prev is null || prev.Kind != DecisionContext.KindAction || prev.GameId != ctx.GameId || prev.GameTurn != ctx.GameTurn) return null;
        var played = ctx.State.History.Where(h => h.Seq > prev.HistorySeqAtCreation && h.Player == "local" && h.Kind is "play" or "attack" or "hero-power" or "location").ToList();
        var followed = played.Any(h => h.SourceEntityId == prev.Action.SourceEntityId && (prev.Action.TargetEntityId is null || h.TargetEntityId == prev.Action.TargetEntityId));
        return new
        {
            action = prev.Action.Label,
            summary = prev.Summary,
            followUp = prev.FollowUp.Select(f => f.Description).ToList(),
            followed,
        };
    }

    private AdvisorUpdate Make(string phase, string message, GameStateSnapshot? s) => new()
    {
        Seq = Interlocked.Increment(ref _updateSeq),
        Phase = phase,
        Message = message,
        GameId = s?.GameId,
        Revision = s?.Revision ?? 0,
        State = s,
        Lethal = _currentLethal,
        Facts = _currentFacts,
        CallsThisTurn = _callsThisTurn,
        MidTurnCallsThisTurn = _midTurnCallsThisTurn,
        MaxCallsPerTurn = _options.MaxCallsPerTurn,
        CallsThisGame = _callsThisGame,
        CostThisGameUsd = _costThisGame,
        CostSessionUsd = _costSession,
        Paused = _paused,
        Diagnostics = _currentDiag,
        AwaitingDisplay = AwaitingDisplayLocked(s),
        AwaitingSinceUtc = AwaitingDisplayLocked(s) ? _awaitingSinceUtc : null,
        DisplayTracked = _visibleTurn is not null,
    };

    /// <summary>
    /// Ton tour a commencé côté serveur mais le client ne l'a pas encore exécuté (affiché). Le mulligan et les états hors de
    /// ton tour ne sont jamais concernés ; sans horloge de l'affichage, rien n'est retenu.
    /// </summary>
    private bool AwaitingDisplayLocked(GameStateSnapshot? s)
    {
        if (s is null || _visibleTurn is null || !s.Game.MulliganDone || s.Game.ActivePlayer != "local") return false;
        var awaiting = !HsCopilot.Core.Session.VisualClock.ShowsTurn(_visibleTurn, _visibleStep, s.Game.GameTurn);
        if (awaiting && _awaitingTurn != s.Game.GameTurn)
        {
            _awaitingTurn = s.Game.GameTurn;
            _awaitingSinceUtc = DateTime.UtcNow;
        }
        return awaiting;
    }

    /// <summary>
    /// Nouveau tour ou nouvelle étape exécutés par le client (PowerTaskList). Quand ton tour devient visible, la mise à jour
    /// affichée est republiée : le conseil, déjà calculé sur ce tour, devient « ACTION IMMÉDIATE ».
    /// </summary>
    public void OnVisualProgress(int? turn, string? step, TimeSpan logTime)
    {
        AdvisorUpdate? republish = null;
        lock (_lock)
        {
            var wasAwaiting = _current.AwaitingDisplay;
            _visibleTurn = turn;
            _visibleStep = step;
            if (turn is int t && step == "MAIN_ACTION" && _serverTurnStart.TryGetValue(t, out var server))
            {
                var lag = (logTime - server).TotalMilliseconds;
                if (lag < -60_000) lag += TimeSpan.FromDays(1).TotalMilliseconds;
                if (lag >= 0) _metrics().Record(MetricNames.TurnServerToScreen, lag);
            }
            if (wasAwaiting && !AwaitingDisplayLocked(_current.State ?? _latest))
            {
                _metrics().Increment("affichage : conseils retenus jusqu'à l'affichage du tour");
                var visibleFor = _awaitingSinceUtc is DateTime since ? $" (affiché par Hearthstone {(DateTime.UtcNow - since).TotalSeconds:0.0} s après le début de ton tour côté serveur)" : "";
                republish = _current with
                {
                    Seq = Interlocked.Increment(ref _updateSeq),
                    AwaitingDisplay = false,
                    AwaitingSinceUtc = null,
                    DisplayTracked = true,
                    Message = _current.Phase == AdvisorPhases.Ready ? $"Ton tour est affiché : conseil valable maintenant{visibleFor}." : _current.Message,
                };
            }
        }
        if (republish is not null) Raise(republish);
    }

    /// <summary>À appeler sous verrou, au moment où la recommandation est reconnue valable.</summary>
    private AdvisorUpdate ReadyUpdateLocked(Recommendation rec)
    {
        var reused = rec.Reused ? " (repris sans nouvel appel)" : "";
        return Make(AdvisorPhases.Ready, $"Conseil pour la révision {rec.Revision}, {rec.LatencyMs / 1000:0.0} s{reused}.", _latest) with
        {
            Recommendation = rec,
            DecisionKind = rec.Kind,
            Revision = rec.Revision,
        };
    }

    /// <summary>
    /// Transmet une mise à jour à l'interface, dans l'ordre : une mise à jour calculée avant une autre déjà transmise
    /// (par exemple un conseil validé juste avant qu'une nouvelle décision ne commence) est ignorée.
    /// </summary>
    private void Raise(AdvisorUpdate? update)
    {
        if (update is null) return;
        lock (_raiseLock)
        {
            if (update.Seq != 0 && update.Seq <= _lastRaisedSeq) return;
            if (update.Seq != 0) _lastRaisedSeq = update.Seq;
            lock (_lock) _current = update;
            Updated?.Invoke(update);
        }
    }

    /// <summary>Publie la recommandation, puis la retire aussitôt si l'état a changé pendant la publication.</summary>
    private void PublishChecked(Recommendation rec, AdvisorUpdate ready, GameStateSnapshot? checkedAgainst)
    {
        Board.Publish(rec);
        Raise(ready);
        GameStateSnapshot? now;
        lock (_lock) now = _latest;
        if (now is not null && !ReferenceEquals(now, checkedAgainst)) Board.OnSnapshot(now);
    }

    private void OnBoardChanged(Recommendation? rec, string? reason)
    {
        if (rec is not null || reason is null) return;
        AdvisorUpdate update;
        lock (_lock)
        {
            _lastStaleReason = reason;
            _lastIdleMessage = null;
            // L'analyse rapide de la décision précédente ne vaut plus pour le nouvel état.
            _currentFacts = Array.Empty<QuickFact>();
            _currentLethal = null;
            _currentDiag = null;
            update = Make(AdvisorPhases.Stale, $"Conseil retiré : {reason}.", _latest);
        }
        Raise(update);
    }

    /// <summary>L'appel envoyé n'a servi à rien (décision passée) : il rend son quota par tour, pas son coût.</summary>
    private void RefundTurnQuota(int gameTurn)
    {
        if (_options.CancelledCallsUseTurnCap) return;
        lock (_lock)
        {
            if (gameTurn == _turnStartGameTurn && _midTurnCallsThisTurn > 0) _midTurnCallsThisTurn--;
        }
    }

    private async Task RunAsync(DecisionContext ctx, CancellationTokenSource cts, long decisionTimestamp, TimeSpan debounce, bool countsTowardCap)
    {
        try
        {
            await RunCoreAsync(ctx, cts, decisionTimestamp, debounce, countsTowardCap).ConfigureAwait(false);
        }
        finally
        {
            GameStateSnapshot? reevaluate = null;
            lock (_lock)
            {
                if (ReferenceEquals(_inFlight, cts))
                {
                    // Appel conservé pour la suite du plan, terminé (réponse, erreur ou délai) : la décision en attente est
                    // évaluée maintenant, avec la suite si elle est légale, sinon par un nouvel appel.
                    if (_inFlightCarriesPlan && !cts.IsCancellationRequested)
                    {
                        reevaluate = _latest;
                        _requestedKey = null;
                        _inFlightKey = null;
                        _inFlightCtx = null;
                    }
                    _inFlight = null;
                    _inFlightSignature = null;
                    _inFlightPartialAction = null;
                    _inFlightCarriesPlan = false;
                }
            }
            cts.Dispose();
            if (reevaluate is not null) Evaluate(reevaluate, manual: false);
        }
    }

    private async Task RunCoreAsync(DecisionContext ctx, CancellationTokenSource cts, long decisionTimestamp, TimeSpan debounce, bool countsTowardCap)
    {
        if (debounce > TimeSpan.Zero)
        {
            try { await Task.Delay(debounce, cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
        lock (_lock)
        {
            if (!ReferenceEquals(_inFlight, cts) || cts.IsCancellationRequested) return;
            _callsThisTurn++;
            if (countsTowardCap) _midTurnCallsThisTurn++;
            _callsThisGame++;
            _callsSession++;
            _inFlightSentUtc = DateTime.UtcNow;
        }
        _metrics().Increment("stratégie : appels envoyés");

        using var timeout = new CancellationTokenSource(_options.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, timeout.Token);
        var callStart = Stopwatch.GetTimestamp();
        StrategyResult result;
        try
        {
            result = _options.UseStreaming && _provider is IStreamingStrategyProvider streaming
                ? await streaming.ProposeStreamingAsync(ctx, p => OnPartial(ctx, cts, p, decisionTimestamp), linked.Token).ConfigureAwait(false)
                : await _provider.ProposeAsync(ctx, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cts.IsCancellationRequested)
        {
            _metrics().Increment("stratégie : délais dépassés");
            Board.Record("timeout", $"pas de réponse en {_options.Timeout.TotalSeconds:0} s (révision {ctx.Revision})", ctx.Revision);
            Status?.Invoke("délai dépassé : aucun conseil pour cette situation");
            RaiseIfCurrent(cts, AdvisorPhases.Error, $"Pas de réponse en {_options.Timeout.TotalSeconds:0} s : aucun conseil pour cette situation (Ctrl+Alt+R pour réessayer).", AdvisorReasons.Timeout);
            return;
        }
        catch (OperationCanceledException)
        {
            if (countsTowardCap) RefundTurnQuota(ctx.GameTurn);
            // Remplacé par un état plus récent ou décision passée. L'appel était parti : on compte son coût estimé.
            if (_options.CancelledCallCostUsd > 0)
            {
                _metrics().AddAmount("stratégie : coût estimé des appels annulés (USD)", _options.CancelledCallCostUsd);
                lock (_lock)
                {
                    _costSession += _options.CancelledCallCostUsd;
                    if (ctx.GameId == _gameId) _costThisGame += _options.CancelledCallCostUsd;
                }
            }
            return;
        }
        catch (Exception ex)
        {
            _metrics().Increment("stratégie : erreurs");
            Board.Record("error", ex.Message, ctx.Revision);
            Status?.Invoke("erreur du fournisseur : " + ex.Message);
            RaiseIfCurrent(cts, AdvisorPhases.Error, "Erreur du fournisseur : " + ex.Message, AdvisorReasons.ProviderError);
            return;
        }
        var callMs = Stopwatch.GetElapsedTime(callStart).TotalMilliseconds;
        _metrics().Record(MetricNames.StrategyCall, callMs);
        if (result.FirstTokenMs is double ft) _metrics().Record(MetricNames.StrategyFirstToken, ft);
        if (result.CostUsd is double cost)
        {
            _metrics().AddAmount("stratégie : coût API (USD)", cost);
            lock (_lock)
            {
                _costSession += cost;
                if (ctx.GameId == _gameId) _costThisGame += cost;
            }
        }
        _metrics().AddAmount("stratégie : jetons envoyés", result.InputTokens);
        _metrics().AddAmount("stratégie : jetons reçus", result.OutputTokens);

        DecisionContext target;
        bool carriesPlan;
        lock (_lock)
        {
            carriesPlan = !cts.IsCancellationRequested && ReferenceEquals(_inFlight, cts) && _inFlightCarriesPlan && _inFlightCtx is not null;
            target = _inFlightCtx ?? ctx;
        }
        if (carriesPlan)
        {
            // Tu as déjà joué l'action conseillée : la réponse n'est pas affichée, elle devient le plan en cours. RunAsync
            // évalue ensuite la décision en attente (suite du plan si elle est légale, sinon nouvel appel).
            var planned = result.Output is null ? null : RecommendationValidator.Validate(result.Output, ctx, _provider.Name, result.Model, callMs, result.CostUsd);
            var planRec = planned is { Accepted: true, Recommendation: { } r0 } ? (ReferenceEquals(target, ctx) ? r0 : r0.RebindTo(target)) : null;
            lock (_lock)
            {
                if (planRec is not null && ReferenceEquals(_inFlight, cts) && _inFlightCarriesPlan)
                {
                    _lastRecommendation = planRec;
                    _lastRecommendationState = target.State;
                    _lastSignature = Signature(target);
                    _metrics().Increment(planRec.FollowUp.Count > 0 ? "stratégie : suites de plan reçues après le coup joué" : "stratégie : réponses sans suite après le coup joué");
                }
            }
            return;
        }
        lock (_lock)
        {
            if (cts.IsCancellationRequested || !ReferenceEquals(_inFlight, cts) || DecisionKey(_latest) != _inFlightKey || _inFlightCtx is null)
            {
                _metrics().Increment("stratégie : réponses périmées écartées");
                Board.Record("discarded", $"réponse pour la révision {ctx.Revision} arrivée après un changement d'état", ctx.Revision);
                return;
            }
            target = _inFlightCtx;
        }
        if (result.Output is null)
        {
            _metrics().Increment("stratégie : erreurs");
            Board.Record("error", result.Error ?? "réponse vide", ctx.Revision);
            RaiseIfCurrent(cts, AdvisorPhases.Error, "Réponse inutilisable : " + (result.Error ?? "vide"));
            return;
        }
        LethalAssessment? lethalNow;
        lock (_lock) lethalNow = ReferenceEquals(_inFlightCtx, ctx) ? _currentLethal : null;
        var outcome = RecommendationValidator.Validate(result.Output, ctx, _provider.Name, result.Model, callMs, result.CostUsd, lethalNow);
        if (!outcome.Accepted || outcome.Recommendation is null)
        {
            _metrics().Increment("stratégie : actions proposées illégales ou invalides");
            Board.Record("rejected", string.Join(" ; ", outcome.Reasons), ctx.Revision);
            Status?.Invoke("réponse rejetée : " + string.Join(" ; ", outcome.Reasons));
            RaiseIfCurrent(cts, AdvisorPhases.Error, "Réponse rejetée par le validateur : " + string.Join(" ; ", outcome.Reasons));
            return;
        }
        var rec = ReferenceEquals(target, ctx) ? outcome.Recommendation : outcome.Recommendation.RebindTo(target);
        if (rec is null)
        {
            _metrics().Increment("stratégie : réponses périmées écartées");
            return;
        }
        AdvisorUpdate ready;
        GameStateSnapshot? checkedAgainst;
        lock (_lock)
        {
            // Dernière vérification juste avant publication.
            if (!ReferenceEquals(_inFlight, cts) || DecisionKey(_latest) != _inFlightKey)
            {
                _metrics().Increment("stratégie : réponses périmées écartées");
                return;
            }
            checkedAgainst = _latest;
            _metrics().Increment("stratégie : actions proposées légales");
            var elapsed = Stopwatch.GetElapsedTime(decisionTimestamp).TotalMilliseconds;
            _metrics().Record(MetricNames.DecisionToAdvice, elapsed);
            if (!_firstAdviceThisTurn && _turnStartTimestamp != 0 && ctx.Kind == DecisionContext.KindAction)
            {
                _firstAdviceThisTurn = true;
                var sinceTurn = Stopwatch.GetElapsedTime(_turnStartTimestamp);
                _metrics().Record(MetricNames.TurnStartToAdvice, sinceTurn.TotalMilliseconds);
                _metrics().Increment(sinceTurn <= _options.TurnBudget ? "stratégie : premier conseil dans le budget du tour" : "stratégie : premier conseil hors budget");
            }
            MarkShownLocked(ctx, decisionTimestamp);
            _lastSignature = Signature(target);
            _lastRecommendation = rec;
            _lastRecommendationState = target.State;
            ready = ReadyUpdateLocked(rec) with { Diagnostics = AdvisorDiagnostics.From(ctx).Shown(DateTime.UtcNow, _inFlightSentUtc) };
        }
        PublishChecked(rec, ready, checkedAgainst);
    }

    /// <summary>Premier affichage (partiel ou final) pour cette décision et ce tour.</summary>
    private void MarkShownLocked(DecisionContext ctx, long decisionTimestamp)
    {
        if (ReferenceEquals(_partialShownFor, _inFlight)) return;
        _partialShownFor = _inFlight;
        _metrics().Record(MetricNames.DecisionToFirstShown, Stopwatch.GetElapsedTime(decisionTimestamp).TotalMilliseconds);
        if (!_firstShownThisTurn && _turnStartTimestamp != 0 && ctx.Kind == DecisionContext.KindAction)
        {
            _firstShownThisTurn = true;
            _metrics().Record(MetricNames.TurnStartToFirstShown, Stopwatch.GetElapsedTime(_turnStartTimestamp).TotalMilliseconds);
        }
    }

    private void OnPartial(DecisionContext ctx, CancellationTokenSource cts, StrategyPartial partial, long decisionTimestamp)
    {
        if (ctx.Kind == DecisionContext.KindMulligan) return; // les cartes à remplacer arrivent en fin de réponse
        AdvisorUpdate update;
        lock (_lock)
        {
            if (cts.IsCancellationRequested || !ReferenceEquals(_inFlight, cts) || DecisionKey(_latest) != _inFlightKey) return;
            var action = ctx.LegalActions.FirstOrDefault(a => a.Id == partial.ActionId);
            if (action is null) return; // action inventée : rien n'est affiché, la validation finale la rejettera
            _inFlightPartialAction = action;
            MarkShownLocked(ctx, decisionTimestamp);
            update = Make(AdvisorPhases.Partial, $"Réponse en cours ({partial.ElapsedMs / 1000:0.0} s) : action légale, conseil complet dans quelques secondes.", _latest) with
            {
                PartialAction = action,
                Partial = partial,
                DecisionKind = ctx.Kind,
                AnalysisStartedUtc = _inFlightStartedUtc,
            };
        }
        Raise(update);
    }

    private void RaiseIfCurrent(CancellationTokenSource cts, string phase, string message, string? reason = AdvisorReasons.ProviderError)
    {
        AdvisorUpdate? update = null;
        lock (_lock)
        {
            if (ReferenceEquals(_inFlight, cts) && DecisionKey(_latest) == _inFlightKey) update = Make(phase, message, _latest) with { Reason = reason };
        }
        Raise(update);
    }

    /// <summary>Empreinte d'une décision : mêmes actions possibles, même main, même plateau, mêmes PV.</summary>
    public static string Signature(DecisionContext ctx)
    {
        var sb = new StringBuilder();
        sb.Append(ctx.Kind).Append('|').Append(ctx.GameId).Append('|').Append(ctx.GameTurn).Append('|').Append(ctx.ResetCount).Append('|');
        foreach (var a in ctx.LegalActions.OrderBy(a => a.Kind, StringComparer.Ordinal).ThenBy(a => a.SourceEntityId).ThenBy(a => a.SubOptionIndex).ThenBy(a => a.TargetEntityId))
            sb.Append(a.Kind).Append(':').Append(a.SourceEntityId).Append(':').Append(a.SubOptionIndex).Append(':').Append(a.TargetEntityId).Append(':').Append(a.ManaCost).Append(';');
        var s = ctx.State;
        if (ctx.Kind != DecisionContext.KindAction) sb.Append("choix:").Append(ctx.OptionsId).Append('|');
        sb.Append("mana:").Append(s.Local?.Mana.Available).Append('|');
        foreach (var c in s.Local?.Hand.Cards ?? new()) sb.Append(c.EntityId).Append(':').Append(c.Cost).Append(',');
        foreach (var side in new[] { s.Local, s.Opponent })
        {
            sb.Append('|').Append(side?.Hero?.Health).Append('/').Append(side?.Hero?.Armor).Append('/').Append(side?.Hero?.Attack).Append('/').Append(side?.Secrets.Count);
            foreach (var m in side?.Board ?? new()) sb.Append(m.EntityId).Append(':').Append(m.Attack).Append(':').Append(m.Health).Append(':').Append(m.Keywords.Count).Append(',');
        }
        return PromptBuilder.Fingerprint(sb.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        Task pending;
        lock (_lock)
        {
            _inFlight?.Cancel();
            pending = _inFlightTask;
        }
        try { await pending.ConfigureAwait(false); } catch { /* arrêt */ }
    }
}

/// <summary>
/// Fournisseur de diagnostic, sans aucune stratégie : renvoie la première action légale (hors fin de tour).
/// Sert uniquement à mesurer et tester la chaîne (latences, invalidation, légalité) ; jamais affiché comme conseil.
/// </summary>
public sealed class DiagnosticFirstActionProvider : IStrategyProvider
{
    private readonly TimeSpan _delay;

    public DiagnosticFirstActionProvider(TimeSpan? delay = null) => _delay = delay ?? TimeSpan.Zero;

    public string Name => "diagnostic (première action légale, aucune stratégie)";

    public async Task<StrategyResult> ProposeAsync(DecisionContext context, CancellationToken cancellationToken)
    {
        if (_delay > TimeSpan.Zero) await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
        var action = context.LegalActions.FirstOrDefault(a => a.Kind is not ("end-turn" or "mulligan-keep")) ?? context.LegalActions[0];
        return new StrategyResult
        {
            Model = "aucun",
            Output = new StrategyOutput
            {
                ActionId = action.Id,
                Confidence = "faible",
                Summary = $"Diagnostic : {action.Label}",
                Why = "fournisseur de diagnostic, aucune stratégie",
                Rationale = "Fournisseur de diagnostic : aucune évaluation stratégique.",
            },
        };
    }
}

/// <summary>
/// Fournisseur simulé pour mesurer la chaîne sans coût : même comportement que le diagnostic, avec une latence tirée
/// autour d'une moyenne et un début de réponse transmis à mi-parcours (comme un flux réel). Aucune stratégie.
/// </summary>
public sealed class SimulatedLatencyProvider : IStrategyProvider, IStreamingStrategyProvider
{
    private readonly TimeSpan _mean;
    private readonly TimeSpan _jitter;
    private readonly double _partialFraction;
    private readonly Random _random;
    private readonly object _lock = new();

    public SimulatedLatencyProvider(TimeSpan mean, TimeSpan jitter, double partialFraction = 0.45, int seed = 42)
    {
        _mean = mean;
        _jitter = jitter;
        _partialFraction = partialFraction;
        _random = new Random(seed);
    }

    public string Name => $"simulé ({_mean.TotalSeconds:0.#} s ± {_jitter.TotalSeconds:0.#} s, aucune stratégie)";

    private TimeSpan NextDelay()
    {
        lock (_lock) return _mean + TimeSpan.FromMilliseconds((_random.NextDouble() * 2 - 1) * _jitter.TotalMilliseconds);
    }

    public Task<StrategyResult> ProposeAsync(DecisionContext context, CancellationToken cancellationToken) => ProposeStreamingAsync(context, _ => { }, cancellationToken);

    public async Task<StrategyResult> ProposeStreamingAsync(DecisionContext context, Action<StrategyPartial> onPartial, CancellationToken cancellationToken)
    {
        var delay = NextDelay();
        var sw = Stopwatch.StartNew();
        var action = context.LegalActions.FirstOrDefault(a => a.Kind is not ("end-turn" or "mulligan-keep")) ?? context.LegalActions[0];
        await Task.Delay(delay * _partialFraction, cancellationToken).ConfigureAwait(false);
        onPartial(new StrategyPartial { ActionId = action.Id, Summary = $"Simulation : {action.Label}", ElapsedMs = sw.Elapsed.TotalMilliseconds });
        await Task.Delay(delay * (1 - _partialFraction), cancellationToken).ConfigureAwait(false);
        return new StrategyResult
        {
            Model = "simulé",
            LatencyMs = sw.Elapsed.TotalMilliseconds,
            CostUsd = 0,
            Output = new StrategyOutput
            {
                ActionId = action.Id,
                Confidence = "faible",
                Summary = $"Simulation : {action.Label}",
                Why = "latence simulée, aucune stratégie",
                PlanNextTurn = "Simulation : aucun plan réel, mode démo.",
                FollowUp = new() { "Simulation : étape suivante non vérifiée" },
                Rationale = "simulation",
            },
        };
    }
}
