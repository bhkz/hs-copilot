// Fichier partagé entre le plugin HDT (.NET Framework 4.7.2) et l'application (.NET 10).
// Il ne doit utiliser que des API disponibles sur les deux plateformes.
#nullable enable
using System.Collections.Generic;

namespace HsCopilot.Shared
{
    /// <summary>Constantes du protocole local plugin HDT -> application compagnon.</summary>
    /// <remarks>
    /// Transport : named pipe Windows local, messages JSON délimités par des sauts de ligne (NDJSON), UTF-8.
    /// L'application compagnon est le serveur ; le plugin se connecte comme client et se reconnecte seul.
    /// </remarks>
    public static class PipeProtocol
    {
        public const int Version = 1;
        public const string PipeName = "HsCopilot.Collector.v1";

        public const string TypeHello = "hello";
        /// <summary>Lignes brutes "GameState.*" du Power.log telles que HDT les a lues.</summary>
        public const string TypeLines = "lines";
        /// <summary>Événement de haut niveau HDT (annotation, jamais source de vérité).</summary>
        public const string TypeHdtEvent = "hdtEvent";
        /// <summary>Vue résumée de HDT, utilisée uniquement pour le contrôle croisé.</summary>
        public const string TypeHdtCheck = "hdtCheck";
        /// <summary>Deck actif sélectionné dans HDT (liste de cartes du joueur local).</summary>
        public const string TypeDeck = "deck";
        /// <summary>Le plugin signale que des lignes ont été perdues (file pleine).</summary>
        public const string TypeGap = "gap";
        public const string TypeHeartbeat = "heartbeat";
    }

    /// <summary>Message unique du protocole ; seuls les champs pertinents pour <see cref="Type"/> sont remplis.</summary>
    public sealed class PipeMessage
    {
        public string Type { get; set; } = "";
        public long Seq { get; set; }
        /// <summary>Horodatage d'envoi ISO-8601 (horloge du plugin).</summary>
        public string? SentAt { get; set; }

        // hello
        public int? Protocol { get; set; }
        public string? Source { get; set; }
        public string? HdtVersion { get; set; }
        public string? PluginVersion { get; set; }

        // lines
        public List<string>? Lines { get; set; }
        /// <summary>Vrai si les lignes proviennent du tampon de la partie en cours renvoyé à la connexion.</summary>
        public bool? Backlog { get; set; }

        // hdtEvent
        public string? Event { get; set; }
        public string? Detail { get; set; }

        // hdtCheck
        public HdtCheck? Check { get; set; }

        // deck
        public HdtDeck? Deck { get; set; }

        // gap
        public long? Dropped { get; set; }
    }

    public sealed class HdtCheck
    {
        /// <summary>Nombre de lignes GameState transmises par le plugin pour la partie en cours au moment du relevé.</summary>
        public long LinesForwarded { get; set; }
        public int GameTurn { get; set; }
        public bool IsMulliganDone { get; set; }
        public HdtPlayerCheck Local { get; set; } = new HdtPlayerCheck();
        public HdtPlayerCheck Opponent { get; set; } = new HdtPlayerCheck();
    }

    public sealed class HdtPlayerCheck
    {
        public int PlayerId { get; set; }
        public List<int> Hand { get; set; } = new List<int>();
        public List<int> Board { get; set; } = new List<int>();
        public int HandCount { get; set; }
        public int DeckCount { get; set; }
        public int SecretCount { get; set; }
        public int? HeroEntityId { get; set; }
        public int HeroHealth { get; set; }
        public int HeroArmor { get; set; }
        public int? WeaponEntityId { get; set; }
    }

    public sealed class HdtDeck
    {
        public string? Name { get; set; }
        public string? DeckId { get; set; }
        public string? Class { get; set; }
        public List<HdtDeckCard> Cards { get; set; } = new List<HdtDeckCard>();
    }

    public sealed class HdtDeckCard
    {
        public string Id { get; set; } = "";
        public int Count { get; set; }
    }
}
