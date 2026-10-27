using HsCopilot.Core.Engine;

namespace HsCopilot.Core.State;

/// <summary>Règles de visibilité : quelle identité de carte le joueur local est autorisé à connaître.</summary>
/// <remarks>
/// Le Power.log contient parfois des identifiants que l'interface du jeu ne montre pas (par exemple dans le bloc
/// de reset d'un Rembobinage). Ces règles ne se fient donc pas à la simple présence d'un identifiant :
/// <list type="bullet">
/// <item>Entité du joueur local : tout ce que le client connaît.</item>
/// <item>Entité adverse en PLAY, GRAVEYARD ou REMOVEDFROMGAME : identité affichée publiquement.</item>
/// <item>Entité adverse en SECRET : cachée, sauf élément public non-Secret (Quête, Sigil, Objectif) établi par le moteur.</item>
/// <item>Entité adverse en HAND, DECK, SETASIDE : seulement une identité vue publiquement auparavant (carte renvoyée en main,
/// révélation de début de partie…), signalée comme telle.</item>
/// <item>Joueur local inconnu : toutes les entités sont traitées comme adverses (aucune fuite possible).</item>
/// </list>
/// </remarks>
public static class Visibility
{
    public const string Own = "own";
    public const string Public = "public";
    public const string PublicSeen = "public-seen";
    public const string PublicSeenStale = "public-seen-stale";
    public const string Hidden = "hidden";

    public static bool IsLocal(EntityState e, int? localPlayerId) => localPlayerId is int lp && e.Controller == lp;

    public static (string? CardId, string Knowledge) VisibleCardId(EntityState e, int? localPlayerId)
    {
        if (IsLocal(e, localPlayerId))
            return e.CardId.Length > 0 ? (e.CardId, Own) : (null, Hidden);

        switch (e.Zone)
        {
            case "PLAY":
            case "GRAVEYARD":
            case "REMOVEDFROMGAME":
                return e.CardId.Length > 0 ? (e.CardId, Public) : (null, Hidden);
            case "SECRET":
                return e.CardId.Length > 0 && e.PublicCardId == e.CardId && e.PublicReason is "SECRET_ZONE_PUBLIC" or "REVEALED"
                    ? (e.CardId, Public)
                    : (null, Hidden);
            default:
                if (e.PublicCardId is not null) return (e.PublicCardId, e.PublicMayBeStale ? PublicSeenStale : PublicSeen);
                return (null, Hidden);
        }
    }

    /// <summary>Identité à afficher pour une action passée : la carte telle qu'elle a été vue au moment où elle est devenue publique.</summary>
    public static string? HistoryCardId(EntityState e, int? localPlayerId)
    {
        if (IsLocal(e, localPlayerId)) return e.CardId.Length > 0 ? e.CardId : e.LastKnownCardId;
        return e.FirstPublicCardId;
    }
}
