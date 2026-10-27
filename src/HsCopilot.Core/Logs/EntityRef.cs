using System.Text.RegularExpressions;

namespace HsCopilot.Core.Logs;

/// <summary>Référence d'entité telle qu'écrite dans le log.</summary>
/// <remarks>
/// Formes rencontrées : <c>GameEntity</c>, un identifiant numérique (<c>38</c>), un nom de joueur
/// (<c>Joueur1#1001</c>, <c>UNKNOWN HUMAN PLAYER</c>, <c>Aubergiste</c>, ou le nom court <c>Joueur1</c>)
/// et la forme crochetée <c>[entityName=… id=46 zone=HAND zonePos=2 cardId=TIME_045 player=2]</c>.
/// Les champs zone/zonePos/cardId de la forme crochetée reflètent la vue du client au moment de l'écriture,
/// qui peut être en retard sur les données serveur : ils ne servent jamais à modifier l'état.
/// </remarks>
public readonly partial record struct EntityRef(
    int? Id,
    string? PlayerName,
    bool IsGameEntity,
    string? PrintedName,
    string? PrintedCardId,
    string? PrintedZone,
    int? PrintedZonePos,
    int? PrintedPlayer)
{
    public bool IsEmpty => (Id is null || Id == 0) && PlayerName is null && !IsGameEntity;

    [GeneratedRegex(@"^\[entityName=(?<name>.*?) id=(?<id>\d+) zone=(?<zone>\w*) zonePos=(?<pos>-?\d+) cardId=(?<card>\S*) player=(?<player>\d+)\]$")]
    private static partial Regex BracketRegex();

    [GeneratedRegex(@"\bid=(?<id>\d+)\b")]
    private static partial Regex LooseIdRegex();

    public static EntityRef Parse(string? text)
    {
        var s = (text ?? "").Trim();
        if (s.Length == 0) return default;
        if (s == "GameEntity") return new EntityRef(null, null, true, null, null, null, null, null);
        if (int.TryParse(s, out var id)) return new EntityRef(id, null, false, null, null, null, null, null);
        if (s[0] == '[')
        {
            var m = BracketRegex().Match(s);
            if (m.Success)
            {
                return new EntityRef(
                    int.Parse(m.Groups["id"].Value),
                    null,
                    false,
                    m.Groups["name"].Value,
                    m.Groups["card"].Value,
                    m.Groups["zone"].Value,
                    int.Parse(m.Groups["pos"].Value),
                    int.Parse(m.Groups["player"].Value));
            }
            var loose = LooseIdRegex().Match(s);
            if (loose.Success) return new EntityRef(int.Parse(loose.Groups["id"].Value), null, false, null, null, null, null, null);
        }
        return new EntityRef(null, s, false, null, null, null, null, null);
    }
}
