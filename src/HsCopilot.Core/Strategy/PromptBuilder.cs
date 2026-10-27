using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HsCopilot.Core.Util;

namespace HsCopilot.Core.Strategy;

/// <summary>
/// Construit les instructions (fixes, mises en cache par le fournisseur) et le message de chaque décision.
/// Le message ne contient que le contexte de décision : état observable, hypothèses étiquetées, actions légales.
/// </summary>
public static class PromptBuilder
{
    public const int DefaultHistoryItems = 25;

    public const string BaseInstructions = """
        Tu es un coach Hearthstone expert. Tu conseilles en temps réel un joueur de Guerrier Dragons.
        On te donne, au format JSON :
        - "decisionKind" : action (tour normal), mulligan, ou choice (Découverte, Rembobinage, autre choix proposé par le jeu) ;
        - "state" : l'état observable de la partie (vu par le joueur local, hypothèses étiquetées à part) ;
        - "legalActions" : ce que le jeu accepte MAINTENANT, chacune avec un "id" ;
        - "hypotheses" : des suppositions explicitement incertaines ;
        - "sequenceVerification" : seule la première action est garantie légale ;
        - "localAnalysis" (tour normal) : calcul déterministe local sur l'état visible : létal (niveau, séquence), dégâts
          maximum au visage ce tour, ordre recommandé des préparations, effets non simulés ;
        - "previousAdvice" : ton conseil précédent dans ce même tour (action, suite envisagée), s'il existe ;
        - Découverte avec Sombre don : chaque carte de "pendingChoice.offered" porte son don ("darkGift", lu dans le log) et
          la carte qui en résulte ("darkGift.result") ; compare les trois résultats complets, pas les cartes seules.

        Ta tâche selon decisionKind :
        - action : choisir UNE action immédiate parmi legalActions et renvoyer son id exact dans "actionId" ;
        - mulligan : mettre dans "replaceIds" les id « Remplacer … » des cartes à renvoyer (liste vide pour tout garder) ;
          "actionId" vaut l'id « Garder toute la main » si tu gardes tout, sinon l'id de la carte la plus importante à remplacer ;
        - choice : choisir UNE carte proposée et renvoyer son id dans "actionId".
        Règles impératives :
        1. "actionId", "rejectedAlternativeId" et "replaceIds" ne contiennent que des id présents dans legalActions
           (null pour l'alternative s'il n'y en a pas). Hors mulligan, "replaceIds" est une liste vide.
        2. "summary" : conseil principal en français, 20 mots au plus, qui nomme la carte et la cible.
        3. "why" : la raison principale, 15 mots au plus.
        4. "lethal" : "certain" seulement si localAnalysis.lethal vaut "certain" ; "conditionnel" si un Secret, une carte
           cachée ou un effet aléatoire peut l'empêcher ; "plausible" si cela dépend d'étapes que le calcul ne démontre pas ;
           sinon "aucun". Avec peu de PV face à un Secret inconnu, signale le risque.
        5. "rationale" : raisonnement concis (mana, létal, survie, tempo, synergies du deck, ordre des actions, tour suivant).
        6. "planNextTurn" : une ligne courte pour le tour suivant, sous forme d'intention, pas de certitude.
        7. "followUp" : les actions restantes envisagées ce tour-ci après celle-ci, dans l'ordre, au plus 4 (attaques et pouvoir
           héroïque compris), chacune avec "sourceEntityId" (carte, serviteur, héros ou lieu qui agit) et "targetEntityId"
           (null sans cible), pris dans "state" ; elles ne sont pas vérifiées et seront réévaluées.
        8. Les cartes cachées de l'adversaire (main, deck, Secrets) sont inconnues : ne jamais affirmer ce qu'il possède ;
           raisonner en probabilités et le dire dans "uncertaintyReasons".
        9. Ne jamais présenter ton choix comme optimal ou certain : c'est une recommandation argumentée.
        10. "confidence" reflète ta confiance réelle ; "faible" si plusieurs options se valent ou si l'information manque.
        11. Calcule les dégâts et le mana à partir des valeurs actuelles de "state" (attack, health, cost, mana.available),
            pas des valeurs de base.
        12. Avant de proposer « Finir le tour », vérifie dans legalActions les attaques encore possibles (héros adverse
            compris) et le pouvoir héroïque si du mana reste : propose-les s'ils sont utiles, sinon explique pourquoi.
        13. "localAnalysis" est un calcul exact des mécaniques qu'il simule : ne contredis pas ses dégâts. Si
            localAnalysis.lethal vaut "certain", joue la première étape de sa séquence, sauf raison explicite.
        14. Ordre du tour entier : joue d'abord ce qui augmente l'attaque ou retire une Provocation (Profondeurs Sanguines,
            Forgeron dimensionnel, arme), puis attaque. N'attaque pas avec un serviteur qui peut encore recevoir un bonus
            d'ATQ utile ce tour (localAnalysis.ordering).
        15. Si "previousAdvice" existe et que rien d'imprévu n'est apparu, poursuis son plan ; sinon dis brièvement dans
            "why" ce qui change.
        Réponds uniquement par l'objet JSON demandé.
        """;

    /// <summary>Instructions complètes : règles + repères stratégiques du deck (fichier configurable).</summary>
    public static string BuildInstructions(string? strategyNotes) =>
        string.IsNullOrWhiteSpace(strategyNotes) ? BaseInstructions : BaseInstructions + "\n\nRepères stratégiques du deck (orientations, pas des règles absolues) :\n" + strategyNotes;

    /// <summary>Charge le fichier de stratégie (config/strategy/guerrier-dragons.md par défaut).</summary>
    public static string? LoadStrategyNotes(string? path = null)
    {
        var candidates = new[]
        {
            path,
            Environment.GetEnvironmentVariable("HSCOPILOT_STRATEGY_FILE"),
            Path.Combine(AppContext.BaseDirectory, "config", "strategy", "guerrier-dragons.md"),
            Path.Combine(Environment.CurrentDirectory, "config", "strategy", "guerrier-dragons.md"),
        };
        foreach (var c in candidates)
            if (!string.IsNullOrWhiteSpace(c) && File.Exists(c)) return File.ReadAllText(c);
        return null;
    }

    /// <summary>
    /// Message de décision : le contexte sérialisé, avec un historique réduit aux dernières actions et sans champs internes
    /// (révisions par entité, horodatages) qui n'apportent rien au raisonnement.
    /// </summary>
    public static string BuildUserPayload(DecisionContext ctx, int historyItems = DefaultHistoryItems)
    {
        var node = JsonNode.Parse(ctx.ToPayloadJson())!.AsObject();
        if (node["state"] is JsonObject state)
        {
            if (state["history"] is JsonArray history && history.Count > historyItems)
            {
                var keep = history.Skip(history.Count - historyItems).Select(h => h?.DeepClone()).ToList();
                state["history"] = new JsonArray(keep.ToArray());
            }
            // legalOptions est redondant avec legalActions (plus lisible) ; horodatages et révisions par entité inutiles au raisonnement.
            foreach (var key in new[] { "lastLogTime", "linesProcessed", "openBlocks", "source", "schema", "legalOptions" }) state.Remove(key);
            RemoveKeysRecursive(state, "updatedAtRevision");
            // Type et races des cartes restantes : utiles au calcul local des probabilités, pas au modèle (mesuré : +400 à
            // +600 caractères par message sans usage observé).
            if (state["local"]?["deck"]?["knownRemaining"] is JsonArray remaining)
                foreach (var c in remaining.OfType<JsonObject>()) { c.Remove("type"); c.Remove("races"); }
            if (state["history"] is JsonArray trimmed)
                foreach (var h in trimmed.OfType<JsonObject>()) h.Remove("time");
        }
        return node.ToJsonString(Json.Compact);
    }

    private static void RemoveKeysRecursive(JsonNode? node, string key)
    {
        switch (node)
        {
            case JsonObject obj:
                obj.Remove(key);
                foreach (var child in obj.Select(kv => kv.Value).ToList()) RemoveKeysRecursive(child, key);
                break;
            case JsonArray arr:
                foreach (var child in arr) RemoveKeysRecursive(child, key);
                break;
        }
    }

    /// <summary>Empreinte courte d'un texte (pour tracer la version du prompt dans les résultats du banc d'essai).</summary>
    public static string Fingerprint(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12].ToLowerInvariant();
}
