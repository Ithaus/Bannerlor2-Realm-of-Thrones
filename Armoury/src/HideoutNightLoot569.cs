using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 569 (C) - KRYJOWKA NOCA BEZ TOWARU Z NICZEGO (Jeff 15.11, "ABCDE tak rob", audyt kryjowek pkt C). Klucz HideoutNightNoFreeLoot.
    /// Gra (HideoutCampaignBehavior.OnCollectLootItems, sluchacz CampaignEvents.OnCollectLootsItemsEvent): gdy gracz rozbije kryjowke NOCA (nie "wyslij
    /// wojsko"), doklada do jego lupu towar z niczego do celu min(max(zloto lupu x populacja x 135, najtanszy towar ~350), 4750) - najpierw sztuki z listy
    /// towarow handlowych (_potentialLootItems, do 5 pozycji w lupie), potem kopie sztuk juz lezacych w lupie (petla do-while). Kazda dolozona sztuka
    /// jest BEZ modyfikatora (AddToCounts(ItemObject, 1)). Nikt za nia nie placi - lamie zamknieta ekonomie ("nic z niczego").
    /// Latka: prefiks zapamietuje liczbe sztuk bez modyfikatora per przedmiot w rosterze lupu, postfiks (Priority.Last - po postfiksie Wyceny20.HidePost,
    /// ktory tylko loguje doklad gry) zdejmuje to, czego przybylo - dokladnie dolozone sztuki, nic z prawdziwego lupu (prawdziwy lup jest w rosterze
    /// PRZED wywolaniem tej metody, inne mody/sluchacze nie dzialaja w jej wnetrzu). Metoda gry biegnie jak dotad (zeruje _initialHideoutPopulation,
    /// transpiler Wyceny20 D zostaje), tylko jej doklad schodzi. Zloto: gra niczego nie dosypuje (PlunderedGold to prawdziwe zloto bitwy) - bez zmian.
    /// Latka W KAMPANII (OnSessionLaunched, CLAUDE.md 7), raz na proces, tylko przy wlaczonym kluczu; postfiks sprawdza klucz przy kazdym wywolaniu
    /// (wylaczony = jak w grze). Cel przez AccessTools.DeclaredMethod (pulapka 15.10). Wyjatek = licznik potkniec, bez wylacznika.
    /// Log: start sesji "Kryjowka noca (569)", zdarzenie "Kryjowka noca (569): zablokowano ... szt. ... d".
    /// </summary>
    internal sealed class HideoutNightLoot569 : CampaignBehaviorBase
    {
        private static Harmony _h;
        private static bool _hooked;
        private static string _status = "";
        private static int _stumbles, _blockedTimes, _lines;
        private static long _blockedPieces, _blockedValue;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        internal static bool On { get { var s = Settings.Current; return s != null && s.HideoutNightNoFreeLoot; } }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, st =>
            {
                try { SessionStart(); } catch (Exception e) { Stumble("SessionStart", e); }
            });
        }

        public override void SyncData(IDataStore dataStore) { }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errSites.Add(where)) Log.Error("HideoutNightLoot569." + where, e); } catch { }
        }

        private static void SessionStart()
        {
            const string K = "Kryjowka noca (569, klucz Hideout Night No Free Loot): ";
            if (!On)
            {
                Log.Info(K + "WYLACZONE (MCM)" + (_hooked ? " - latka wpieta wczesniej, nieczynna" : " - nic nie wpiete") + ", towar z niczego po nocnym ataku na kryjowke jak w grze.");
                return;
            }
            if (!_hooked)
            {
                try
                {
                    var m = AccessTools.DeclaredMethod(typeof(HideoutCampaignBehavior), "OnCollectLootItems", new[] { typeof(PartyBase), typeof(ItemRoster) });
                    if (m == null) _status = "BRAK HideoutCampaignBehavior.OnCollectLootItems - nie wpiete";
                    else
                    {
                        if (_h == null) _h = new Harmony("com.jeff.armoury.hideoutnight569");
                        _h.Patch(m, prefix: new HarmonyMethod(typeof(HideoutNightLoot569), nameof(Pre)),
                                 postfix: new HarmonyMethod(typeof(HideoutNightLoot569), nameof(Post)) { priority = Priority.Last });
                        _hooked = true;
                        _status = "wpiete (prefiks + postfiks Priority.Last na HideoutCampaignBehavior.OnCollectLootItems)";
                    }
                }
                catch (Exception e) { _status = "BLAD wpiecia (" + e.GetType().Name + ")"; Stumble("Hook", e); }
            }
            Log.Info(K + "CZYNNE - " + _status + "; dotad zablokowano " + _blockedTimes + " razy, " + _blockedPieces + " szt., " + _blockedValue + " d, potkniec " + _stumbles + ".");
        }

        /// <summary>Liczba sztuk BEZ modyfikatora per przedmiot (tylko takie doklada gra).</summary>
        private static Dictionary<ItemObject, int> Plain(ItemRoster r)
        {
            var d = new Dictionary<ItemObject, int>();
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || el.EquipmentElement.ItemModifier != null) continue;
                int n;
                d.TryGetValue(it, out n);
                d[it] = n + el.Amount;
            }
            return d;
        }

        public static void Pre(PartyBase __0, ItemRoster __1, out Dictionary<ItemObject, int> __state)
        {
            __state = null;
            try
            {
                if (!On || __0 == null || __1 == null || __0 != PartyBase.MainParty) return;   // gra doklada tylko gracza
                __state = Plain(__1);
            }
            catch (Exception e) { __state = null; Stumble("Pre", e); }
        }

        public static void Post(ItemRoster __1, Dictionary<ItemObject, int> __state)
        {
            if (__state == null || __1 == null) return;
            try
            {
                var now = Plain(__1);
                long pieces = 0, value = 0;
                var parts = new List<string>();
                foreach (var kv in now)
                {
                    int before;
                    __state.TryGetValue(kv.Key, out before);
                    int extra = kv.Value - before;
                    if (extra <= 0) continue;
                    __1.AddToCounts(new EquipmentElement(kv.Key), -extra);
                    pieces += extra;
                    value += (long)extra * kv.Key.Value;
                    if (parts.Count < 8) parts.Add(kv.Key.StringId + " x" + extra);
                }
                if (pieces <= 0) return;
                _blockedTimes++; _blockedPieces += pieces; _blockedValue += value;
                if (_lines < 50)
                {
                    _lines++;
                    Log.Info("Kryjowka noca (569): zablokowano towar z niczego po nocnym ataku na kryjowke - " + pieces + " szt., wartosc dzis " + value + " d ("
                             + string.Join(", ", parts.ToArray()) + (parts.Count >= 8 ? ", ..." : "") + "); razem w procesie " + _blockedTimes + " razy, "
                             + _blockedPieces + " szt., " + _blockedValue + " d.");
                }
            }
            catch (Exception e) { Stumble("Post", e); }
        }
    }
}
