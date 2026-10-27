using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Plugins;
using HsCopilot.Shared;
using HdtPlayer = Hearthstone_Deck_Tracker.Hearthstone.Player;
using HdtCore = Hearthstone_Deck_Tracker.API.Core;

namespace HsCopilot.Collector
{
    /// <summary>
    /// Game Collector : plugin HDT qui relaie, sans les interpréter, les lignes GameState du Power.log lues par HDT,
    /// plus quelques annotations (début/fin de partie, deck actif, vue résumée de HDT pour contrôle croisé).
    /// </summary>
    /// <remarks>
    /// Le plugin ne lit pas la mémoire du jeu, n'injecte rien, ne clique rien : il n'utilise que les événements
    /// publics de l'API de plugins HDT. Les données transmises sont celles que HDT lit dans les logs.
    /// </remarks>
    public sealed class CollectorPlugin : IPlugin
    {
        private static readonly Version PluginVersion = new Version(0, 1, 0);
        private static bool _subscribed;
        private static volatile bool _enabled;
        private static PipeClient _client;

        private MenuItem _menuItem;
        private DateTime _lastCheck = DateTime.MinValue;
        private DateTime _lastDeckPoll = DateTime.MinValue;
        private string _lastCheckKey;
        private Guid? _lastDeckId;
        private string _lastDeckKey;

        public string Name => "HS Copilot Collector";
        public string Description => "Transmet les événements de partie (lignes GameState du Power.log lues par HDT) à l'application HS Copilot locale, via un named pipe. Aucune automatisation, aucune lecture mémoire.";
        public string ButtonText => "État de la connexion";
        public string Author => "HS Copilot";
        public Version Version => PluginVersion;
        public MenuItem MenuItem => _menuItem;

        public void OnLoad()
        {
            _enabled = true;
            var hdtVersion = typeof(HdtCore).Assembly.GetName().Version?.ToString() ?? "?";
            if (_client == null)
            {
                _client = new PipeClient(hdtVersion, PluginVersion.ToString());
                _client.Start();
            }
            // Les ActionList de HDT ne permettent pas de se désabonner : abonnement unique, filtré par _enabled.
            if (!_subscribed)
            {
                _subscribed = true;
                LogEvents.OnPowerGamestateLogLine.Add(line => { if (_enabled) _client?.OnLine(line); });
                GameEvents.OnGameStart.Add(() => SendEvent("GameStart", null));
                GameEvents.OnGameEnd.Add(() => SendEvent("GameEnd", null));
                GameEvents.OnInMenu.Add(() => SendEvent("InMenu", null));
                GameEvents.OnTurnStart.Add(p => SendEvent("TurnStart", p.ToString()));
                GameEvents.OnModeChanged.Add(m => SendEvent("ModeChanged", m.ToString()));
                DeckManagerEvents.OnDeckSelected.Add(d => { if (_enabled) _lastDeckId = null; });
            }
            _menuItem = new MenuItem { Header = "HS Copilot : état" };
            _menuItem.Click += (s, e) => ShowStatus();
        }

        public void OnUnload()
        {
            _enabled = false;
            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }
        }

        public void OnButtonPress() => ShowStatus();

        /// <summary>Appelé par HDT environ toutes les 100 ms sur le fil de l'interface.</summary>
        public void OnUpdate()
        {
            if (!_enabled || _client == null || !_client.IsConnected) return;
            var now = DateTime.UtcNow;
            try
            {
                if (now - _lastDeckPoll > TimeSpan.FromSeconds(2))
                {
                    _lastDeckPoll = now;
                    SendDeckIfChanged();
                }
                if (now - _lastCheck > TimeSpan.FromSeconds(1))
                {
                    _lastCheck = now;
                    SendCheckIfChanged();
                }
            }
            catch (Exception)
            {
                // Ne jamais faire échouer HDT : la vue de contrôle est facultative.
            }
        }

        private static void SendEvent(string name, string detail)
        {
            if (!_enabled || _client == null) return;
            _client.Send(new PipeMessage { Type = PipeProtocol.TypeHdtEvent, Event = name, Detail = detail });
        }

        private void SendDeckIfChanged()
        {
            var deck = DeckList.Instance.ActiveDeckVersion ?? DeckList.Instance.ActiveDeck;
            if (deck == null) return;
            var key = deck.DeckId + "|" + string.Join(",", deck.Cards.Select(c => c.Id + "x" + c.Count));
            if (key == _lastDeckKey && _lastDeckId == deck.DeckId) return;
            _lastDeckKey = key;
            _lastDeckId = deck.DeckId;
            var msg = new PipeMessage
            {
                Type = PipeProtocol.TypeDeck,
                Deck = new HdtDeck { Name = deck.Name, DeckId = deck.DeckId.ToString(), Class = deck.Class },
            };
            foreach (var c in deck.Cards) msg.Deck.Cards.Add(new HdtDeckCard { Id = c.Id, Count = c.Count });
            _client.Send(msg);
        }

        private void SendCheckIfChanged()
        {
            var game = HdtCore.Game;
            if (game == null || game.IsInMenu || game.Player == null || game.Opponent == null) return;
            var check = new HdtCheck
            {
                LinesForwarded = _client.LinesForwarded,
                GameTurn = game.GameEntity?.GetTag(GameTag.TURN) ?? 0,
                IsMulliganDone = game.IsMulliganDone,
                Local = BuildPlayer(game.Player),
                Opponent = BuildPlayer(game.Opponent),
            };
            var key = string.Join("|", check.GameTurn, check.Local.HandCount, string.Join(",", check.Local.Hand), string.Join(",", check.Local.Board),
                check.Local.DeckCount, check.Local.HeroHealth, check.Local.HeroArmor, check.Opponent.HandCount, string.Join(",", check.Opponent.Board),
                check.Opponent.DeckCount, check.Opponent.HeroHealth, check.Opponent.HeroArmor, check.Opponent.SecretCount);
            if (key == _lastCheckKey) return;
            _lastCheckKey = key;
            _client.Send(new PipeMessage { Type = PipeProtocol.TypeHdtCheck, Check = check });
        }

        private static HdtPlayerCheck BuildPlayer(HdtPlayer p)
        {
            var hero = p.Hero;
            var result = new HdtPlayerCheck
            {
                PlayerId = p.Id,
                HandCount = p.HandCount,
                DeckCount = p.DeckCount,
                SecretCount = p.Secrets.Count(),
                HeroEntityId = hero?.Id,
                HeroHealth = hero?.Health ?? 0,
                HeroArmor = hero?.GetTag(GameTag.ARMOR) ?? 0,
                WeaponEntityId = p.Board.FirstOrDefault(e => e.IsWeapon)?.Id,
            };
            result.Hand.AddRange(p.Hand.OrderBy(e => e.ZonePosition).Select(e => e.Id));
            result.Board.AddRange(p.Board.Where(e => e.IsMinion || e.IsLocation).OrderBy(e => e.ZonePosition).Select(e => e.Id));
            return result;
        }

        private static void ShowStatus()
        {
            var c = _client;
            var text = c == null
                ? "Plugin inactif."
                : (c.IsConnected ? "Connecté à HS Copilot." : "En attente de l'application HS Copilot (lancez HsCopilot.App).")
                  + "\nLignes de la partie en cours : " + c.LinesForwarded
                  + "\nLignes perdues : " + c.Dropped
                  + (c.LastError == null ? "" : "\nDernière erreur : " + c.LastError);
            MessageBox.Show(text, "HS Copilot Collector", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
