namespace HsCopilot.Core.Logs;

/// <summary>Découpage des instructions "DebugPrintPower" en éléments typés.</summary>
/// <remarks>
/// Le découpage se fait par repères fixes (" tag=", " value=", " Entity=", …) plutôt que par une grande
/// expression régulière, car les noms d'entités contiennent des espaces et parfois des crochets
/// (<c>UNKNOWN ENTITY [cardType=INVALID]</c>).
/// </remarks>
public static class PowerStatements
{
    public readonly record struct TagChange(EntityRef Entity, string Tag, string Value, bool DefChange);
    public readonly record struct TagLine(string Tag, string Value);
    public readonly record struct EntityHeader(EntityRef Entity, string CardId);
    public readonly record struct HideEntity(EntityRef Entity, string Tag, string Value);
    public readonly record struct BlockStart(string BlockType, EntityRef Entity, int EffectIndex, EntityRef Target, int SubOption, string? TriggerKeyword);

    /// <summary><c>tag=ZONE value=HAND</c></summary>
    public static bool TryTagLine(string body, out TagLine result)
    {
        result = default;
        if (!body.StartsWith("tag=", StringComparison.Ordinal)) return false;
        var v = body.IndexOf(" value=", StringComparison.Ordinal);
        if (v < 0) return false;
        var tag = body[4..v];
        var value = FirstToken(body[(v + 7)..]);
        result = new TagLine(tag, value);
        return true;
    }

    /// <summary><c>TAG_CHANGE Entity=&lt;ref&gt; tag=T value=V [DEF CHANGE]</c></summary>
    public static bool TryTagChange(string body, out TagChange result)
    {
        result = default;
        const string head = "TAG_CHANGE Entity=";
        if (!body.StartsWith(head, StringComparison.Ordinal)) return false;
        var t = body.LastIndexOf(" tag=", StringComparison.Ordinal);
        if (t < head.Length) return false;
        var v = body.IndexOf(" value=", t, StringComparison.Ordinal);
        if (v < 0) return false;
        var entity = EntityRef.Parse(body[head.Length..t]);
        var tag = body[(t + 5)..v];
        var valuePart = body[(v + 7)..];
        var value = FirstToken(valuePart);
        var def = valuePart.Contains("DEF CHANGE", StringComparison.Ordinal);
        result = new TagChange(entity, tag, value, def);
        return true;
    }

    /// <summary><c>FULL_ENTITY - Creating ID=N CardID=C</c></summary>
    public static bool TryFullEntityCreating(string body, out int id, out string cardId)
    {
        id = 0; cardId = "";
        const string head = "FULL_ENTITY - Creating ID=";
        if (!body.StartsWith(head, StringComparison.Ordinal)) return false;
        var c = body.IndexOf(" CardID=", StringComparison.Ordinal);
        var idText = c < 0 ? body[head.Length..] : body[head.Length..c];
        if (!int.TryParse(idText.Trim(), out id)) return false;
        cardId = c < 0 ? "" : FirstToken(body[(c + 8)..]);
        return true;
    }

    /// <summary>
    /// <c>FULL_ENTITY - Updating &lt;ref&gt; CardID=C</c>, <c>SHOW_ENTITY - Updating Entity=&lt;ref&gt; CardID=C</c>,
    /// <c>CHANGE_ENTITY - Updating Entity=&lt;ref&gt; CardID=C</c>.
    /// </summary>
    public static bool TryUpdatingHeader(string body, string head, out EntityHeader result)
    {
        result = default;
        if (!body.StartsWith(head, StringComparison.Ordinal)) return false;
        var c = body.LastIndexOf(" CardID=", StringComparison.Ordinal);
        if (c < head.Length) return false;
        var entity = EntityRef.Parse(body[head.Length..c]);
        var cardId = FirstToken(body[(c + 8)..]);
        result = new EntityHeader(entity, cardId);
        return true;
    }

    /// <summary><c>HIDE_ENTITY - Entity=&lt;ref&gt; tag=ZONE value=DECK</c></summary>
    public static bool TryHideEntity(string body, out HideEntity result)
    {
        result = default;
        const string head = "HIDE_ENTITY - Entity=";
        if (!body.StartsWith(head, StringComparison.Ordinal)) return false;
        var t = body.LastIndexOf(" tag=", StringComparison.Ordinal);
        if (t < head.Length) return false;
        var v = body.IndexOf(" value=", t, StringComparison.Ordinal);
        if (v < 0) return false;
        result = new HideEntity(EntityRef.Parse(body[head.Length..t]), body[(t + 5)..v], FirstToken(body[(v + 7)..]));
        return true;
    }

    /// <summary>
    /// <c>BLOCK_START BlockType=PLAY Entity=&lt;ref&gt; EffectCardId=… EffectIndex=0 Target=&lt;ref|0&gt; SubOption=-1 [TriggerKeyword=K]</c>.
    /// Certains blocs (GAME_RESET) n'ont pas de SubOption ; tous les champs après BlockType sont facultatifs.
    /// </summary>
    public static bool TryBlockStart(string body, out BlockStart result)
    {
        result = default;
        const string head = "BLOCK_START BlockType=";
        if (!body.StartsWith(head, StringComparison.Ordinal)) return false;
        var markers = new[] { " Entity=", " EffectCardId=", " EffectIndex=", " Target=", " SubOption=", " TriggerKeyword=" };
        var positions = new int[markers.Length];
        var searchFrom = head.Length;
        for (int i = 0; i < markers.Length; i++)
        {
            var p = body.IndexOf(markers[i], searchFrom, StringComparison.Ordinal);
            positions[i] = p;
            if (p >= 0) searchFrom = p + markers[i].Length;
        }

        string Segment(int i)
        {
            var p = positions[i];
            if (p < 0) return "";
            var start = p + markers[i].Length;
            var end = body.Length;
            for (int j = i + 1; j < markers.Length; j++)
            {
                if (positions[j] >= 0) { end = positions[j]; break; }
            }
            return body[start..end].Trim();
        }

        var typeEnd = positions.Where(p => p >= 0).DefaultIfEmpty(body.Length).Min();
        var blockType = body[head.Length..typeEnd].Trim();
        var entity = EntityRef.Parse(Segment(0));
        var effectIndex = int.TryParse(Segment(2), out var ei) ? ei : -1;
        var target = EntityRef.Parse(Segment(3));
        var subOption = int.TryParse(Segment(4), out var so) ? so : -1;
        var kw = positions[5] >= 0 ? Segment(5) : null;
        result = new BlockStart(blockType, entity, effectIndex, target, subOption, kw);
        return true;
    }

    /// <summary>Lit une valeur "clé=valeur" dans une ligne d'en-tête (par ex. <c>id=3 Player=Nom TaskList=153 ChoiceType=GENERAL</c>).</summary>
    /// <remarks>La valeur s'arrête au prochain " Clé=" connu ; les noms de joueur peuvent contenir des espaces.</remarks>
    public static string? ReadField(string body, string key, params string[] followingKeys)
    {
        var marker = key + "=";
        var p = body.StartsWith(marker, StringComparison.Ordinal) ? 0 : body.IndexOf(" " + marker, StringComparison.Ordinal);
        if (p < 0) return null;
        var start = p == 0 && body.StartsWith(marker, StringComparison.Ordinal) ? marker.Length : p + 1 + marker.Length;
        var end = body.Length;
        foreach (var k in followingKeys)
        {
            var q = body.IndexOf(" " + k + "=", start, StringComparison.Ordinal);
            if (q >= 0 && q < end) end = q;
        }
        if (followingKeys.Length == 0)
        {
            var sp = body.IndexOf(' ', start);
            if (sp >= 0) end = sp;
        }
        return body[start..end].Trim();
    }

    public static string FirstToken(string s)
    {
        s = s.TrimStart();
        var sp = s.IndexOf(' ');
        return sp < 0 ? s : s[..sp];
    }
}
