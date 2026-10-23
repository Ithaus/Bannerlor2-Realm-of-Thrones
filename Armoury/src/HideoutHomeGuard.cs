using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// m109-crash (Jeff 22.10 23:59:40, wersja 103.5): wywrotka na mapie - NullReferenceException w BanditSpawnCampaignBehavior.OnHomeHideoutChanged
    /// (CrashScribe crash-original-2026-10-22_23-59-40.log; stos: Hideout.OnPartyEntered <- EnterSettlementAction.ApplyForParty <- tik mapy).
    ///
    /// PRZYCZYNA (dekompilacja 1.4.8): Hideout.OnPartyEntered wola BanditPartyComponent.SetHomeHideout(this) dla kazdej bandy frakcji bandyckiej;
    /// SetHomeHideout przy zmianie odpala CampaignEvents.OnHomeHideoutChanged(banda, stara kryjowka), a BanditSpawnCampaignBehavior.OnHomeHideoutChanged
    /// czyta oldHomeHideout.Settlement bez sprawdzenia null. Stara kryjowka null ma kazda banda z BanditPartyComponent.CreateLooterParty (dom = osada,
    /// nie kryjowka). W grze tak powstaja tylko szabrownicy i dezerterzy - ich kultura nie ma osad, wiec AiVisitSettlementBehavior
    /// (CalculateVisitHideoutScoresForBanditParty: MapFaction.Culture.CanHaveSettlement) nie prowadzi ich do kryjowek. U nas tak powstaja bandy kultur
    /// z kryjowkami: najazdy Wolnych Ludzi (FreeFolkRaids, armoury_ffraid), bandy scen prologu i Kroniki (StoryActions.SpawnBand: armoury_prl_*, armoury_kr_*)
    /// - AI gry co godzine wazy je jako gosci kryjowek, a pierwsze wejscie takiej bandy do kryjowki konczy gre.
    ///
    /// ZMIANA (klucz HideoutHomeGuardEnabled; NIE = gra jak w 103.5): prefiks na BanditSpawnCampaignBehavior.OnHomeHideoutChanged - gdy stara kryjowka
    /// jest null, robi to samo co oryginal, ale ze starym domem = osada _relatedSettlement bandy (ta, ktora gra policzyla w MobilePartyCreated):
    /// licznik band starego domu -1, nowego domu (kryjowka) +1, oryginal nie biegnie. Kazdy inny przypadek - oryginal bez zmian. Finalizer polyka
    /// wyjatek z tej metody (licznik, pierwsze 3 do Log.Error) - po nim SetHomeHideout i wejscie do kryjowki koncza sie normalnie (banda ma juz nowy dom).
    /// Jedyny sluchacz zdarzenia w grze i modach to BanditSpawnCampaignBehavior (przeszukane DLL Modules), wiec nikt inny nie dostaje null.
    /// Linie "Dom kryjowki (m109): " - start sesji (latki), pierwsze 5 band szczegolowo (id, klan, kultura, stary dom, kryjowka, zrodlo), doba z wejsciami.
    /// </summary>
    internal static class HideoutHomeGuard
    {
        private const string P = "Dom kryjowki (m109): ";
        private const int DetailMax = 5;

        private static bool _hooked;
        private static string _wired = "-";
        private static AccessTools.FieldRef<BanditSpawnCampaignBehavior, Dictionary<Settlement, int>> _countsRef;
        private static AccessTools.FieldRef<BanditPartyComponent, Settlement> _relatedRef;

        private static int _fixed, _dFixed, _caught, _dCaught, _detail, _stumbles;
        private static readonly Dictionary<string, int> _dSrc = new Dictionary<string, int>();
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static readonly object _lock = new object();

        internal static bool On { get { var s = Settings.Current; return s != null && s.HideoutHomeGuardEnabled; } }

        // ------------------------------------------------------------ wpiecie (ArmouryBehavior.RegisterEvents - jedna linia)
        internal static void RegisterEvents(CampaignBehaviorBase b)
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(b, delegate (CampaignGameStarter s) { try { SessionStart(); } catch (Exception e) { Stumble("SessionStart", e); } });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(b, delegate { try { Daily(); } catch (Exception e) { Stumble("Daily", e); } });
        }

        private static void SessionStart()
        {
            EnsureHooks();
            Log.Info(P + "start sesji | klucz Hideout Home Guard Enabled " + (On ? "tak" : "nie") + " | latki " + _wired
                     + " | licznik band kryjowek gry " + (_countsRef != null ? "tak" : "nie") + ", stary dom bandy " + (_relatedRef != null ? "tak" : "nie")
                     + " | od startu procesu: band bez kryjowki wpuszczonych " + _fixed + ", wyjatkow zlapanych " + _caught + ".");
        }

        private static void EnsureHooks()
        {
            if (_hooked) return;
            _hooked = true;
            var ok = new List<string>(); var miss = new List<string>();
            try { _countsRef = AccessTools.FieldRefAccess<BanditSpawnCampaignBehavior, Dictionary<Settlement, int>>("_banditCountsPerHideout"); }
            catch (Exception e) { _countsRef = null; Stumble("CountsRef", e); }
            try { _relatedRef = AccessTools.FieldRefAccess<BanditPartyComponent, Settlement>("_relatedSettlement"); }
            catch (Exception e) { _relatedRef = null; Stumble("RelatedRef", e); }
            try
            {
                var h = new Harmony("com.jeff.armoury.hideouthomeguard");
                var m = AccessTools.DeclaredMethod(typeof(BanditSpawnCampaignBehavior), "OnHomeHideoutChanged", new[] { typeof(BanditPartyComponent), typeof(Hideout) });
                if (m == null) miss.Add("BanditSpawnCampaignBehavior.OnHomeHideoutChanged");
                else
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(HideoutHomeGuard), nameof(ChangedPrefix)) { priority = Priority.First },
                               finalizer: new HarmonyMethod(typeof(HideoutHomeGuard), nameof(ChangedFinalizer)));
                    ok.Add("BanditSpawnCampaignBehavior.OnHomeHideoutChanged");
                }
            }
            catch (Exception e) { miss.Add("Harmony (" + e.GetType().Name + ")"); Stumble("EnsureHooks", e); }
            _wired = ok.Count + "/" + (ok.Count + miss.Count) + (miss.Count > 0 ? " (BRAK: " + string.Join(", ", miss) + ")" : "");
        }

        // ------------------------------------------------------------ latka
        /// <summary>Prefiks BanditSpawnCampaignBehavior.OnHomeHideoutChanged. false = przypadek bez starej kryjowki obsluzony tutaj (oryginal czytalby null).</summary>
        public static bool ChangedPrefix(BanditSpawnCampaignBehavior __instance, BanditPartyComponent banditPartyComponent, Hideout oldHomeHideout)
        {
            if (oldHomeHideout != null || !On) return true;
            try
            {
                Settlement oldHome = null, newHome = null;
                if (banditPartyComponent != null)
                {
                    if (_relatedRef != null) oldHome = _relatedRef(banditPartyComponent);
                    newHome = banditPartyComponent.HomeSettlement;   // po SetHomeHideout: osada nowej kryjowki
                }
                var counts = (_countsRef != null && __instance != null) ? _countsRef(__instance) : null;
                if (counts != null)
                {
                    int v;
                    // jak oryginal (G BanditSpawnCampaignBehavior.OnHomeHideoutChanged): stary dom -1, nowy +1; klucz null pominiety (slownik rzucilby)
                    if (oldHome != null) { counts.TryGetValue(oldHome, out v); counts[oldHome] = v - 1; }
                    if (newHome != null) { counts.TryGetValue(newHome, out v); counts[newHome] = v + 1; }
                }
                Note(banditPartyComponent, oldHome, newHome, counts != null);
            }
            catch (Exception e) { Stumble("ChangedPrefix", e); }
            return false;
        }

        /// <summary>Finalizer tej samej metody: kazdy inny wyjatek (np. z oryginalu przy dziwnej bandzie) zlapany i policzony - wejscie do kryjowki konczy sie normalnie.</summary>
        public static Exception ChangedFinalizer(Exception __exception, BanditPartyComponent banditPartyComponent, Hideout oldHomeHideout)
        {
            if (__exception == null) return null;
            if (!On) return __exception;
            Interlocked.Increment(ref _caught);
            lock (_lock) _dCaught++;
            if (_caught <= 3 || _caught % 100 == 0)
            {
                string who = "?";
                try { who = Describe(banditPartyComponent) + ", stara kryjowka " + (oldHomeHideout?.Settlement?.StringId ?? "brak"); } catch { }
                try { Log.Error("HideoutHomeGuard.OnHomeHideoutChanged (" + _caught + ". raz; " + who + ")", __exception); } catch { }
            }
            return null;
        }

        // ------------------------------------------------------------ meldunki
        private static void Note(BanditPartyComponent c, Settlement oldHome, Settlement newHome, bool counted)
        {
            Interlocked.Increment(ref _fixed);
            string src = Source(c);
            lock (_lock)
            {
                _dFixed++;
                int n; _dSrc.TryGetValue(src, out n); _dSrc[src] = n + 1;
            }
            if (_detail >= DetailMax) return;
            _detail++;
            Log.Info(P + "banda bez kryjowki weszla do kryjowki (" + _detail + "/" + DetailMax + " szczegolowo) - " + Describe(c)
                     + " | stary dom " + (oldHome != null ? oldHome.StringId : "brak") + " -> kryjowka " + (newHome != null ? newHome.StringId : "brak")
                     + " | zrodlo " + src + " | licznik band kryjowek gry " + (counted ? "poprawiony (stary dom -1, kryjowka +1)" : "niedostepny - bez zmian")
                     + " | gra bez latki: wywrotka (NullReferenceException).");
        }

        private static string Describe(BanditPartyComponent c)
        {
            if (c == null) return "banda null";
            var mp = c.MobileParty;
            var clan = mp != null ? mp.ActualClan : null;
            string cult = null;
            try { cult = mp?.Party?.Culture?.StringId; } catch { }
            return "banda " + (mp?.StringId ?? "?") + " (" + (mp != null ? mp.Name?.ToString() : "?") + "; ludzi " + (mp != null ? mp.MemberRoster.TotalManCount : 0)
                   + ", klan " + (clan?.StringId ?? "brak") + ", kultura " + (cult ?? "?") + ")";
        }

        /// <summary>Kto utworzyl bande - po przedrostku id (nasze bandy) albo klanie (gra i inne mody).</summary>
        private static string Source(BanditPartyComponent c)
        {
            string id = null, clan = null;
            try { id = c?.MobileParty?.StringId; clan = c?.MobileParty?.ActualClan?.StringId; } catch { }
            if (id == null) return "nieznane";
            if (id.StartsWith("armoury_ffraid", StringComparison.Ordinal)) return "najazd Wolnych Ludzi (FreeFolkRaids)";
            if (id.StartsWith("armoury_kr_", StringComparison.Ordinal)) return "banda Kroniki (Chronicle.Bands)";
            if (id.StartsWith("armoury_prl", StringComparison.Ordinal)) return "banda prologu (Prologue/StoryActions)";
            if (id.StartsWith("armoury_", StringComparison.Ordinal)) return "inna banda Armoury (" + Prefix(id) + ")";
            return "gra albo inny mod (klan " + (clan ?? "brak") + ", id " + Prefix(id) + ")";
        }

        private static string Prefix(string id)
        {
            int cut = id.Length;
            for (int i = 0; i < id.Length; i++) if (char.IsDigit(id[i])) { cut = i; break; }
            return id.Substring(0, cut).TrimEnd('_');
        }

        private static void Daily()
        {
            int f, k; string src;
            lock (_lock)
            {
                f = _dFixed; k = _dCaught;
                src = _dSrc.Count == 0 ? "-" : string.Join(", ", _dSrc.OrderByDescending(x => x.Value).Select(x => x.Key + " " + x.Value));
                _dFixed = _dCaught = 0; _dSrc.Clear();
            }
            if (f == 0 && k == 0) return;
            Log.Info(P + "dzien " + ((int)CampaignTime.Now.ToDays).ToString(CultureInfo.InvariantCulture) + " | band bez kryjowki wpuszczonych do kryjowek " + f
                     + " (zrodla: " + src + "), wyjatkow zlapanych " + k + " | od startu procesu " + _fixed + " / " + _caught + ", potkniecia " + _stumbles + ".");
        }

        private static void Stumble(string where, Exception e)
        {
            Interlocked.Increment(ref _stumbles);
            bool first;
            lock (_err) first = _err.Add(where);
            if (first) { try { Log.Error("HideoutHomeGuard." + where, e); } catch { } }
        }
    }
}
