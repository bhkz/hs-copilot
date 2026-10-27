namespace HsCopilot.Core.Engine;

/// <summary>État interne d'une entité, reconstruit à partir des lignes GameState.</summary>
/// <remarks>
/// Cet objet contient tout ce que le client a reçu, y compris des données qui ne doivent pas
/// sortir du moteur (par exemple un identifiant de carte adverse transmis dans un bloc de reset).
/// La seule voie de sortie vers l'extérieur est <see cref="State.GameStateProjector"/>, qui applique
/// les règles de visibilité.
/// </remarks>
public sealed class EntityState
{
    internal readonly Dictionary<string, string> Tags = new(StringComparer.Ordinal);

    public EntityState(int id) => Id = id;

    public int Id { get; }

    /// <summary>Identifiant de carte actuellement connu du client ("" si caché).</summary>
    public string CardId { get; internal set; } = "";

    public IReadOnlyDictionary<string, string> AllTags => Tags;

    // --- Provenance de l'information -------------------------------------------------------

    /// <summary>Dernier identifiant de carte observé alors que l'entité était publique (zone publique ou révélée).</summary>
    public string? PublicCardId { get; internal set; }
    /// <summary>Premier identifiant observé publiquement (utile si l'entité a ensuite été transformée).</summary>
    public string? FirstPublicCardId { get; internal set; }
    /// <summary>Raison de la publicité : PLAY, GRAVEYARD, REMOVEDFROMGAME, REVEALED, SECRET_NON_SECRET.</summary>
    public string? PublicReason { get; internal set; }
    public int? PublicAtRevision { get; internal set; }
    /// <summary>Vrai si l'entité a été transformée dans une zone cachée après avoir été vue : l'identité publique peut être périmée.</summary>
    public bool PublicMayBeStale { get; internal set; }
    /// <summary>Dernier identifiant non vide connu du client (même s'il a été masqué ensuite, par ex. carte remise au deck au mulligan).</summary>
    public string? LastKnownCardId { get; internal set; }

    // --- Cycle de vie ----------------------------------------------------------------------

    /// <summary>Zone au moment de la création de l'entité.</summary>
    public string? InitialZone { get; internal set; }
    /// <summary>Propriétaire au moment de la création (une carte peut changer de camp : vol, « jouable des deux côtés »…).</summary>
    public int InitialController { get; internal set; }
    /// <summary>Créée pendant la mise en place (avant le début du mulligan) : fait partie du deck ou de la main de départ.</summary>
    public bool CreatedDuringSetup { get; internal set; }
    public int CreatedTurn { get; internal set; }
    public int CreatedRevision { get; internal set; }
    public int ZoneEnteredTurn { get; internal set; }
    public int LastChangedRevision { get; internal set; }
    public bool WasTransformed { get; internal set; }
    /// <summary>Entité absente de l'état renvoyé par le serveur lors d'un reset (Rembobinage) : elle n'existe plus.</summary>
    public bool Orphaned { get; internal set; }
    /// <summary>Entité référencée avant d'avoir été créée (partie rejointe en cours ou lignes manquantes).</summary>
    public bool Placeholder { get; internal set; }

    // --- Accès aux tags --------------------------------------------------------------------

    public string? GetTag(string tag) => Tags.TryGetValue(tag, out var v) ? v : null;

    public int GetInt(string tag) =>
        Tags.TryGetValue(tag, out var v) && int.TryParse(v, out var i) ? i : 0;

    public bool Flag(string tag) => GetInt(tag) != 0;

    public string Zone => GetTag("ZONE") ?? "INVALID";
    public int Controller => GetInt("CONTROLLER");
    public string CardType => GetTag("CARDTYPE") ?? "INVALID";
    public int ZonePosition => GetInt("ZONE_POSITION");
    public bool HasCardId => CardId.Length > 0;

    public override string ToString() => $"#{Id} {CardId} {Zone} p{Controller}";
}
