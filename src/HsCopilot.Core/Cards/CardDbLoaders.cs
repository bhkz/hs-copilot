using System.Text.Json;
using System.Xml;

namespace HsCopilot.Core.Cards;

/// <summary>Chargeur des fichiers CardDefs de HearthSim (ceux que HDT télécharge et met à jour).</summary>
/// <remarks>
/// Emplacement par défaut : <c>%AppData%\HearthstoneDeckTracker\CardDefs\CardDefs.base.xml</c> (valeurs, textes enUS)
/// et <c>CardDefs.frFR.xml</c> (noms et textes français). L'attribut <c>build</c> de la racine identifie la version des données.
/// </remarks>
public static class CardDefsXmlLoader
{
    // Tables issues de HearthDb.Enums (HearthDb.dll livré avec HDT 1.59.6), relevées par réflexion.
    private static readonly Dictionary<int, string> CardTypes = new()
    {
        [1] = "GAME", [2] = "PLAYER", [3] = "HERO", [4] = "MINION", [5] = "SPELL", [6] = "ENCHANTMENT", [7] = "WEAPON",
        [8] = "ITEM", [9] = "TOKEN", [10] = "HERO_POWER", [11] = "BLANK", [39] = "LOCATION",
        [40] = "BATTLEGROUND_QUEST_REWARD", [42] = "BATTLEGROUND_SPELL", [43] = "BATTLEGROUND_ANOMALY", [44] = "BATTLEGROUND_TRINKET",
    };

    private static readonly Dictionary<int, string> Classes = new()
    {
        [1] = "DEATHKNIGHT", [2] = "DRUID", [3] = "HUNTER", [4] = "MAGE", [5] = "PALADIN", [6] = "PRIEST", [7] = "ROGUE",
        [8] = "SHAMAN", [9] = "WARLOCK", [10] = "WARRIOR", [11] = "DREAM", [12] = "NEUTRAL", [13] = "WHIZBANG", [14] = "DEMONHUNTER",
    };

    private static readonly Dictionary<int, string> Races = new()
    {
        [11] = "UNDEAD", [14] = "MURLOC", [15] = "DEMON", [17] = "MECHANICAL", [18] = "ELEMENTAL", [20] = "BEAST", [21] = "TOTEM",
        [23] = "PIRATE", [24] = "DRAGON", [26] = "ALL", [38] = "EGG", [43] = "QUILBOAR", [88] = "HALFORC", [92] = "NAGA",
        [93] = "OLDGOD", [95] = "GRONN",
    };

    private static readonly Dictionary<int, string> Rarities = new()
    {
        [1] = "COMMON", [2] = "FREE", [3] = "RARE", [4] = "EPIC", [5] = "LEGENDARY",
    };

    private static readonly Dictionary<int, string> SpellSchools = new()
    {
        [1] = "ARCANE", [2] = "FIRE", [3] = "FROST", [4] = "NATURE", [5] = "HOLY", [6] = "SHADOW", [7] = "FEL", [8] = "PHYSICAL_COMBAT",
    };

    /// <summary>Tags booléens conservés comme « mécaniques » (mots-clés et capacités utiles au raisonnement).</summary>
    private static readonly HashSet<string> MechanicTags = new(StringComparer.Ordinal)
    {
        "TAUNT", "DIVINE_SHIELD", "CHARGE", "RUSH", "WINDFURY", "MEGA_WINDFURY", "STEALTH", "POISONOUS", "VENOMOUS", "LIFESTEAL",
        "REBORN", "BATTLECRY", "DEATHRATTLE", "DISCOVER", "SECRET", "QUEST", "SIDE_QUEST", "QUESTLINE", "SIGIL", "OBJECTIVE",
        "COMBO", "OVERLOAD", "FREEZE", "SILENCE", "TRADEABLE", "FORGE", "OUTCAST", "INFUSE", "MAGNETIC", "FRENZY", "OVERKILL",
        "SPELLBURST", "HONORABLEKILL", "CORRUPT", "DREDGE", "COLOSSAL", "TITAN", "EXCAVATE", "MINIATURIZE", "GIGANTIFY",
        "ELUSIVE", "CANT_BE_TARGETED_BY_SPELLS", "CANT_BE_TARGETED_BY_HERO_POWERS", "IMMUNE", "DORMANT", "INSPIRE", "ECHO",
        "TWINSPELL", "START_OF_GAME", "AURA", "ENRAGED", "CHOOSE_ONE", "ADAPT", "LIFESTEAL", "REWIND", "KINDRED", "QUICKDRAW",
        "STARSHIP", "STARSHIP_PIECE", "IMBUE", "TOURIST", "DARK_GIFT", "SPELLPOWER", "INVOKE", "MANATHIRST", "FINALE",
        "CLEAVE", "SUPERCHARGE",
    };

    public static CardDatabase Load(string basePath, string? localePath, string locale = "frFR")
    {
        var cards = new Dictionary<string, Builder>(StringComparer.Ordinal);
        var build = ReadEntities(basePath, cards, isLocaleFile: false, locale);
        if (localePath is not null && File.Exists(localePath))
            ReadEntities(localePath, cards, isLocaleFile: true, locale);
        return new CardDatabase(cards.Values.Select(b => b.ToCardInfo()), build ?? "unknown", locale, basePath);
    }

    private sealed class Builder
    {
        public required string Id;
        public int Dbf;
        public string? Name, NameEn, Text, TextEn, Type, Class, Rarity, Set, SpellSchool;
        public int? Cost, Attack, Health, Durability;
        public bool Collectible;
        public readonly List<string> Races = new();
        public readonly List<string> Mechanics = new();

        public CardInfo ToCardInfo() => new()
        {
            Id = Id,
            DbfId = Dbf,
            Name = Name ?? NameEn,
            NameEn = NameEn,
            Text = Text ?? TextEn,
            Type = Type,
            Cost = Cost,
            Attack = Attack,
            Health = Type == "WEAPON" ? null : Health,
            Durability = Type == "WEAPON" ? Durability ?? Health : Durability,
            CardClass = Class,
            Races = Races.Count > 0 ? Races : null,
            Rarity = Rarity,
            Set = Set,
            Collectible = Collectible,
            Mechanics = Mechanics.Count > 0 ? Mechanics.Distinct().ToList() : null,
            SpellSchool = SpellSchool,
        };
    }

    private static string? ReadEntities(string path, Dictionary<string, Builder> cards, bool isLocaleFile, string locale)
    {
        string? build = null;
        var settings = new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true, DtdProcessing = DtdProcessing.Prohibit };
        using var reader = XmlReader.Create(path, settings);
        Builder? current = null;
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element) continue;
            switch (reader.Name)
            {
                case "CardDefs":
                    build = reader.GetAttribute("build");
                    break;
                case "Entity":
                {
                    var id = reader.GetAttribute("CardID");
                    if (string.IsNullOrEmpty(id)) { current = null; break; }
                    if (!cards.TryGetValue(id, out current))
                    {
                        if (isLocaleFile) { current = null; break; }
                        current = new Builder { Id = id };
                        cards[id] = current;
                    }
                    if (int.TryParse(reader.GetAttribute("ID"), out var dbf)) current.Dbf = dbf;
                    break;
                }
                case "Tag" when current is not null:
                    ReadTag(reader, current, locale);
                    break;
            }
        }
        return build;
    }

    private static void ReadTag(XmlReader reader, Builder b, string locale)
    {
        var name = reader.GetAttribute("name") ?? "";
        var type = reader.GetAttribute("type");
        if (type == "LocString")
        {
            if (name is not ("CARDNAME" or "CARDTEXT")) { reader.Skip(); return; }
            string? en = null, loc = null;
            using (var sub = reader.ReadSubtree())
            {
                while (sub.Read())
                {
                    if (sub.NodeType != XmlNodeType.Element) continue;
                    if (sub.Name == "enUS") en = sub.ReadElementContentAsString();
                    else if (sub.Name == locale) loc = sub.ReadElementContentAsString();
                }
            }
            if (name == "CARDNAME") { if (en is not null) b.NameEn = en; if (loc is not null) b.Name = loc; }
            else { if (en is not null) b.TextEn = en; if (loc is not null) b.Text = loc; }
            return;
        }
        if (!int.TryParse(reader.GetAttribute("value"), out var v)) return;
        switch (name)
        {
            case "COST": b.Cost = v; break;
            case "ATK": b.Attack = v; break;
            case "HEALTH": b.Health = v; break;
            case "DURABILITY": b.Durability = v; break;
            case "CARDTYPE": b.Type = CardTypes.GetValueOrDefault(v, v.ToString()); break;
            case "CLASS": b.Class = Classes.GetValueOrDefault(v, v.ToString()); break;
            case "CARDRACE": if (Races.TryGetValue(v, out var r)) b.Races.Insert(0, r); break;
            case "RARITY": b.Rarity = Rarities.GetValueOrDefault(v, v.ToString()); break;
            case "CARD_SET": b.Set = v.ToString(); break;
            case "COLLECTIBLE": b.Collectible = v == 1; break;
            case "SPELL_SCHOOL": b.SpellSchool = SpellSchools.GetValueOrDefault(v, v.ToString()); break;
            default:
                if (v == 1 && MechanicTags.Contains(name)) b.Mechanics.Add(name);
                break;
        }
    }
}

/// <summary>Chargeur au format HearthstoneJSON (<c>https://api.hearthstonejson.com/v1/&lt;build&gt;/frFR/cards.json</c>) et du cache local.</summary>
public static class HearthstoneJsonLoader
{
    private sealed class CacheFile
    {
        public string Build { get; set; } = "unknown";
        public string Locale { get; set; } = "frFR";
        public string? Origin { get; set; }
        public List<CardInfo> Cards { get; set; } = new();
    }

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Charge un fichier HearthstoneJSON (tableau de cartes) ou un cache HsCopilot (objet avec <c>cards</c>).</summary>
    public static CardDatabase Load(string path, string? build = null, string locale = "frFR")
    {
        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);
        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            var cache = doc.RootElement.Deserialize<CacheFile>(Options) ?? new CacheFile();
            return new CardDatabase(cache.Cards, cache.Build, cache.Locale, path);
        }
        var list = new List<CardInfo>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var id = el.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (string.IsNullOrEmpty(id)) continue;
            List<string>? races = null;
            if (el.TryGetProperty("races", out var rs) && rs.ValueKind == JsonValueKind.Array)
                races = rs.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList();
            else if (el.TryGetProperty("race", out var r1) && r1.ValueKind == JsonValueKind.String)
                races = new List<string> { r1.GetString()! };
            list.Add(new CardInfo
            {
                Id = id,
                DbfId = Int(el, "dbfId") ?? 0,
                Name = Str(el, "name"),
                Text = Str(el, "text"),
                Type = Str(el, "type"),
                Cost = Int(el, "cost"),
                Attack = Int(el, "attack"),
                Health = Int(el, "health"),
                Durability = Int(el, "durability"),
                CardClass = Str(el, "cardClass"),
                Races = races,
                Rarity = Str(el, "rarity"),
                Set = Str(el, "set"),
                Collectible = el.TryGetProperty("collectible", out var col) && col.ValueKind == JsonValueKind.True,
                Mechanics = el.TryGetProperty("mechanics", out var me) && me.ValueKind == JsonValueKind.Array
                    ? me.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList()
                    : null,
                SpellSchool = Str(el, "spellSchool"),
            });
        }
        return new CardDatabase(list, build ?? "hearthstonejson", locale, path);
    }

    public static void SaveCache(CardDatabase db, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var cache = new CacheFile { Build = db.Build, Locale = db.Locale, Origin = db.Source, Cards = db.All.OrderBy(c => c.Id, StringComparer.Ordinal).ToList() };
        var tmp = path + ".tmp";
        using (var fs = File.Create(tmp))
            JsonSerializer.Serialize(fs, cache, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        File.Move(tmp, path, overwrite: true);
    }

    private static string? Str(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;
}

/// <summary>Localise et charge la meilleure base disponible, avec mise en cache JSON par build.</summary>
public static class CardDbLocator
{
    public static string DefaultHdtCardDefsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HearthstoneDeckTracker", "CardDefs");

    public static string DefaultCacheDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HsCopilot", "carddb");

    /// <summary>
    /// Ordre : fichier explicite (HearthstoneJSON ou cache) → CardDefs de HDT (via cache par build) → base vide.
    /// </summary>
    public static (CardDatabase Db, string Message) LoadBest(string? explicitPath = null, string? hdtCardDefsDir = null, string? cacheDir = null, string locale = "frFR")
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                if (!File.Exists(explicitPath)) return (CardDatabase.Empty, $"Base de cartes introuvable : {explicitPath}");
                var db = explicitPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                    ? CardDefsXmlLoader.Load(explicitPath, null, locale)
                    : HearthstoneJsonLoader.Load(explicitPath, null, locale);
                return (db, $"{db.Count} cartes chargées depuis {explicitPath} (build {db.Build})");
            }

            var dir = hdtCardDefsDir ?? DefaultHdtCardDefsDir;
            var basePath = Path.Combine(dir, "CardDefs.base.xml");
            if (File.Exists(basePath))
            {
                var build = ReadBuild(basePath) ?? "unknown";
                var cachePath = Path.Combine(cacheDir ?? DefaultCacheDir, $"cards.{build}.{locale}.json");
                if (File.Exists(cachePath))
                {
                    var cached = HearthstoneJsonLoader.Load(cachePath);
                    return (cached, $"{cached.Count} cartes (cache build {cached.Build}) depuis {cachePath}");
                }
                var db = CardDefsXmlLoader.Load(basePath, Path.Combine(dir, $"CardDefs.{locale}.xml"), locale);
                try { HearthstoneJsonLoader.SaveCache(db, cachePath); } catch { /* le cache est facultatif */ }
                return (db, $"{db.Count} cartes chargées depuis les CardDefs HDT (build {db.Build})");
            }
            return (CardDatabase.Empty, "Aucune base de cartes trouvée (CardDefs HDT absent et aucun fichier configuré).");
        }
        catch (Exception ex)
        {
            return (CardDatabase.Empty, $"Erreur de chargement de la base de cartes : {ex.Message}");
        }
    }

    public static string? ReadBuild(string cardDefsPath)
    {
        using var reader = XmlReader.Create(cardDefsPath, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        while (reader.Read())
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "CardDefs")
                return reader.GetAttribute("build");
        return null;
    }
}
