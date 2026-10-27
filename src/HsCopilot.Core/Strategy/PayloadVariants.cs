using System.Text.Json.Nodes;
using HsCopilot.Core.Util;

namespace HsCopilot.Core.Strategy;

/// <summary>Variantes du message pour les comparaisons avant / après (mesure uniquement, jamais en direct).</summary>
public static class PayloadVariants
{
    /// <summary>Message « avant » : sans dons des propositions, sans probabilités de pioche ni tour suivant.</summary>
    public static string WithoutGiftsDeckAndNextTurn(string payload)
    {
        var node = JsonNode.Parse(payload)!.AsObject();
        if (node["localAnalysis"] is JsonObject la) { la.Remove("deck"); la.Remove("nextTurn"); }
        if (node["state"]?["pendingChoice"]?["offered"] is JsonArray offered)
            foreach (var o in offered.OfType<JsonObject>()) o.Remove("darkGift");
        if (node["legalActions"] is JsonArray actions)
            foreach (var x in actions.OfType<JsonObject>())
                if (x["kind"]?.GetValue<string>() == "choose" && x["label"]?.GetValue<string>() is string l && l.Contains(" + ", StringComparison.Ordinal))
                    x["label"] = l[..l.IndexOf(" + ", StringComparison.Ordinal)];
        if (node["state"]?["local"]?["deck"] is JsonObject deck)
        {
            deck.Remove("knownTop");
            if (deck["knownRemaining"] is JsonArray kr) foreach (var k in kr.OfType<JsonObject>()) { k.Remove("type"); k.Remove("races"); }
        }
        return node.ToJsonString(Json.Compact);
    }

}
