using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// ZB - ZBROJOWNIA GRACZA, poprawki z przegladu 16.10 (STAN-PRAC 46, sciezki misji i UI, wf_d9056776-3fb; zrobione w nocy 17/18.10). Szesc kluczy MCM,
    /// kazdy wylaczony = kod jak przed paczka:
    ///  ZB-1 ArmouryBookPerModifier - ksiega wkladow gracza per id + modyfikator (rozbicie w ArmouryBehavior, przydzial SwapMath.AllocateOwn z keyBookOf,
    ///       porzadek QuartermasterLaw.PurgeUnusable ustawia rozbicie na Target). Dotad per id, a czesc gracza szla na NAJGORSZY egzemplarz - przy dwoch
    ///       egzemplarzach tego samego id gracz po nastepnym porzadku dostawal gorszy.
    ///  ZB-2 ArmouryListSpareHorsesWhenKept - przy ArmouryShowsOnlyYours i bez PlayerMenSellSpareHorses konie zapasu ludzi byly schowane na zawsze (ani na liscie,
    ///       ani sprzedane - MenPurse.PlayerSurplusPlan zwraca null dla koni); teraz zostaja na liscie okna (do kupienia od ludzi, jak przy widoku pelnym).
    ///  ZB-3 SurplusUsableAnyGroup - plan nadwyzek (MenPurse.PlayerSurplusPlan / AiSurplusPlan, musztra) pytal o "ktos udzwignie" tylko najzdolniejsza grupe
    ///       (SwapMath.Usable: group[0]); przy GearFit (siodlo, kultura) sztuka innej grupy szla do kupca jako "nikt nie udzwignie" - teraz kazda grupa.
    ///  ZB-4 GearFitStoreSubNoLegends (CrashScribe Mends.StoreSub) - zamiennik obcej sztuki z puli bez legend i bez sprzetu olbrzymow (poza olbrzymami).
    ///  ZB-5 GearFitRealmFootInHideoutSiege (CrashScribe RealmWard + GearFit.FloorOne) - w kryjowce i oblezeniu jezdziec walczy pieszo; dotad oceniany wedlug
    ///       konia, ktorego DTE i tak przydziela (DTE: _missionAllowsMountEquipment = tylko nie-morska), wiec nie dostawal broni "nie z siodla".
    ///  ZB-6 NightPromptSkipWhenRested (NightRest.OnHourly) - pytanie "Night Falls" nie wyskakuje, gdy ludzie juz wyspali baze doby (np. po snie w menu).
    /// Linia doby "Zbrojownia gracza (ZB): dzien N" (tylko log) i linia startu sesji z samosprawdzeniem. Bez stanu w zapisie (stan ZB-1 trzyma ArmouryBehavior).
    /// </summary>
    internal static class ArmouryZb
    {
        // liczniki doby (zerowane po linii doby; nowa kampania w tym samym procesie - zerowane przy zmianie instancji zachowania)
        private static int _dPurge, _dSetPcs, _dRest, _dKeyIn, _dKeyOut, _dClip, _dGhost, _dModeReset;
        private static int _dHorseOpens, _dHorseListed, _dSurSaved, _dSurSavedAi, _dPromptSkip, _dStumble;
        private static int _migIds = -1, _migPcs;   // migracja starego zapisu (raz) - do linii doby; -1 = nie bylo
        private static string _firstStumble;
        private static object _inst;

        private static Settings S { get { return Settings.Current; } }
        internal static bool HorsesListedOn { get { var s = S; return s != null && s.ArmouryListSpareHorsesWhenKept && !s.PlayerMenSellSpareHorses; } }
        internal static bool AnyGroupOn { get { var s = S; return s != null && s.SurplusUsableAnyGroup; } }
        internal static bool FootOn { get { var s = S; return s != null && s.GearFitRealmFootInHideoutSiege; } }
        internal static bool PromptSkipOn { get { var s = S; return s != null && s.NightPromptSkipWhenRested; } }

        private static void Touch()
        {
            var cur = ArmouryBehavior.Instance;
            if (ReferenceEquals(cur, _inst)) return;
            _inst = cur;
            _dPurge = _dSetPcs = _dRest = _dKeyIn = _dKeyOut = _dClip = _dGhost = _dModeReset = 0;
            _dHorseOpens = _dHorseListed = _dSurSaved = _dSurSavedAi = _dPromptSkip = _dStumble = 0;
            _migIds = -1; _migPcs = 0; _firstStumble = null;
        }

        // ------------------------------------------------------------ liczniki (wolane z ArmouryBehavior, QuartermasterLaw, MenPurse, NightRest)
        internal static void NoteKeyMove(int n, bool deposit) { Touch(); if (deposit) _dKeyIn += n; else _dKeyOut += n; }
        internal static void NoteClip(int n) { if (n <= 0) return; Touch(); _dClip += n; }
        internal static void NoteGhostKey(int n) { Touch(); _dGhost += n; }
        internal static void NoteModeReset(int keys) { Touch(); _dModeReset++; Log.Info("Zbrojownia (ZB-1): klucz Armoury Book Per Modifier przelaczony w trakcie gry - rozbicie ksiegi (" + keys + " kluczy) od nowa, migracja z ksiegi per id przy najblizszym porzadku."); }
        internal static void NoteHorses(int listed) { Touch(); _dHorseOpens++; _dHorseListed += listed; }
        internal static void NotePromptSkip() { Touch(); _dPromptSkip++; }

        /// <summary>ZB-1: porzadek z rozbiciem - ustawione sztuki (Target) i sztuki przypisane z reszty ksiegi id (bez rozbicia).</summary>
        internal static void NotePurge(int setPcs, int rest) { Touch(); _dSetPcs += setPcs; _dRest += rest; }
        internal static void NotePurgeRun() { Touch(); _dPurge++; }

        /// <summary>ZB-1: migracja starego zapisu - raz, przy pierwszym porzadku z rozbiciem po wczytaniu zapisu bez klucza.</summary>
        internal static void NoteMigration(int ids, int pcs)
        {
            Touch();
            _migIds = ids; _migPcs = pcs;
            Log.Info("Zbrojownia (ZB-1): migracja ksiegi per id -> per egzemplarz (zapis bez rozbicia): id " + ids + ", szt. " + pcs
                     + " rozpisane na egzemplarze dokladnie tak, jak lezaly po ostatnim porzadku (najgorsze egzemplarze id - bez zmiany wlasnosci); odtad kazdy wklad ze swoim stanem.");
        }

        internal static void Stumble(string where, Exception e)
        {
            Touch();
            _dStumble++;
            if (_firstStumble == null) { _firstStumble = where; try { Log.Error("ArmouryZb." + where, e); } catch { } }
        }

        /// <summary>ZB-3: "ktos udzwignie" do SwapMath.SurplusPlan - przy kluczu kazda grupa (UsableAny, licznik uratowanych sztuk), inaczej jak dotad (group[0]).</summary>
        internal static Func<SwapMath.Piece, bool> SurplusUsable(int[] men, Func<int, SwapMath.Piece, bool> meets, bool ai)
        {
            if (!AnyGroupOn) return p => SwapMath.Usable(men, meets, p);
            var groups = SwapMath.DistinctGroups(men);
            return p =>
            {
                bool saved;
                bool ok = SwapMath.UsableAny(groups, meets, p, out saved);
                if (saved && p.MenFree > 0) { Touch(); if (ai) _dSurSavedAi += p.MenFree; else _dSurSaved += p.MenFree; }
                return ok;
            };
        }

        // ------------------------------------------------------------ ZB-5: misja pieszo (kryjowka, zasadzka w kryjowce, oblezenie)
        // Ta sama regula co DTE Assignment.FillEmptySlots (flag3: HideoutMissionController / HideoutAmbushMissionController / MissionSiegeEnginesLogic - tam DTE nie
        // dopelnia konia ze wzorca); gra w oblezeniu stawia wojsko bez koni (spawnWithHorses: false). Kopia w CrashScribe Mends.FootMission - TRZYMAC W ZGODZIE.
        private static WeakReference _footM;
        private static bool _footV;
        private static readonly object _footLock = new object();

        internal static bool FootMission(Mission m)
        {
            if (m == null) return false;
            lock (_footLock)
            {
                if (_footM != null && ReferenceEquals(_footM.Target, m)) return _footV;
            }
            bool foot = false;
            try
            {
                foreach (var b in m.MissionBehaviors)
                {
                    if (b == null) continue;
                    string n = b.GetType().Name;
                    if (n == "HideoutMissionController" || n == "HideoutAmbushMissionController" || n == "MissionSiegeEnginesLogic") { foot = true; break; }
                }
            }
            catch { foot = false; }
            lock (_footLock) { _footM = new WeakReference(m); _footV = foot; }
            return foot;
        }

        // ------------------------------------------------------------ linie
        /// <summary>Start sesji (po McmSettings.Apply): klucze i samosprawdzenie ZB-1 (dwa egzemplarze tego samego id, czesc gracza na lepszym) i ZB-3 (sztuka tylko dla
        /// drugiej grupy) na liczbach bez typow gry.</summary>
        internal static void SessionStart()
        {
            var s = S;
            if (s == null) return;
            Touch();
            // ZB-1: X|fine (1 szt., gracza), X| (1 szt., zapas ludzi), ksiega X = 1
            var fine = new SwapMath.Piece { Id = "test_x", Mod = "fine", Tier = 3, Power = 30, Quality = 1.2f, Total = 1 };
            var plain = new SwapMath.Piece { Id = "test_x", Mod = "", Tier = 3, Power = 30, Quality = 1f, Total = 1 };
            var l1 = new List<SwapMath.Piece> { fine, plain };
            SwapMath.AllocateOwn(l1, id => 1, k => k == "test_x|fine" ? 1 : 0);
            string got = fine.Own == 1 && plain.Own == 0 ? "fine" : plain.Own == 1 ? "zwykly" : "?";
            var fine0 = new SwapMath.Piece { Id = "test_x", Mod = "fine", Tier = 3, Power = 30, Quality = 1.2f, Total = 1 };
            var plain0 = new SwapMath.Piece { Id = "test_x", Mod = "", Tier = 3, Power = 30, Quality = 1f, Total = 1 };
            SwapMath.AllocateOwn(new List<SwapMath.Piece> { fine0, plain0 }, id => 1);
            string got0 = fine0.Own == 1 ? "fine" : plain0.Own == 1 ? "zwykly" : "?";
            // reszta bez rozbicia: ksiega 2, rozbicie fine 1 -> fine 1 + zwykly 1
            var f2 = new SwapMath.Piece { Id = "test_x", Mod = "fine", Tier = 3, Power = 30, Quality = 1.2f, Total = 1 };
            var p2 = new SwapMath.Piece { Id = "test_x", Mod = "", Tier = 3, Power = 30, Quality = 1f, Total = 2 };
            int rest = SwapMath.AllocateOwn(new List<SwapMath.Piece> { f2, p2 }, id => 2, k => k == "test_x|fine" ? 1 : 0);
            bool restOk = f2.Own == 1 && p2.Own == 1 && rest == 1;
            // ZB-3: grupy 0 (najzdolniejsza) i 1; sztuke udzwignie tylko grupa 1
            var men = new[] { 0, 0, 1 };
            Func<int, SwapMath.Piece, bool> meets = (g, p) => g == 1;
            var pc = new SwapMath.Piece { Id = "test_lance", Total = 1 };
            bool saved; bool any = SwapMath.UsableAny(SwapMath.DistinctGroups(men), meets, pc, out saved);
            bool old = SwapMath.Usable(men, meets, pc);
            Log.Info("Zbrojownia gracza (ZB): start sesji | ksiega per modyfikator (Armoury Book Per Modifier) " + (s.ArmouryBookPerModifier ? "TAK" : "NIE")
                     + " | konie zapasu ludzi na liscie (Armoury List Spare Horses When Kept) " + (s.ArmouryListSpareHorsesWhenKept ? "TAK" : "NIE")
                     + " (czynne " + (HorsesListedOn && PlayerArmoryView.OnlyYours ? "TAK" : "NIE - wymaga widoku tylko Twoje i wylaczonej sprzedazy koni") + ")"
                     + " | nadwyzki - kazda grupa (Surplus Usable Any Group) " + (s.SurplusUsableAnyGroup ? "TAK" : "NIE")
                     + " | zamiennik bez legend i olbrzymow (Gear Fit Store Sub No Legends) " + (s.GearFitStoreSubNoLegends ? "TAK" : "NIE")
                     + " | jezdziec pieszo w kryjowce i oblezeniu (Gear Fit Realm Foot In Hideout Siege) " + (s.GearFitRealmFootInHideoutSiege ? "TAK" : "NIE")
                     + " | bez pytania o nocleg po wyspaniu (Night Prompt Skip When Rested) " + (s.NightPromptSkipWhenRested ? "TAK" : "NIE")
                     + " | samosprawdzenie ksiegi: czesc gracza na " + got + " (oczekiwane fine; regula sprzed ZB-1: " + got0 + "), reszta bez rozbicia " + (restOk ? "tak" : "NIE")
                     + " | samosprawdzenie nadwyzek: sztuka tylko dla drugiej grupy uzyteczna " + (any && saved ? "tak" : "NIE") + " (regula sprzed ZB-3: " + (old ? "tak" : "nie") + ").");
        }

        /// <summary>Linia doby (tylko log). Tozsamosci do sprawdzarki (grupa nzbroj): szt. ksiegi = szt. w rozbiciu + reszta bez rozbicia; rozbicie ponad ksiege = 0.
        /// Same etykiety wlasnosci - sztuki i pieniadze bez zmian (ksiega per id zmienia sie jak przed paczka).</summary>
        internal static void Daily()
        {
            var s = S;
            if (s == null) return;
            Touch();
            int ids, pcs, keys, modPcs, rest, over;
            ArmouryBehavior.BookModStats(out ids, out pcs, out keys, out modPcs, out rest, out over);
            int day = 0;
            try { day = (int)CampaignTime.Now.ToDays; } catch { }
            string mig = _migIds >= 0 ? "id " + _migIds + " szt. " + _migPcs : "nie bylo";
            Log.Info("Zbrojownia gracza (ZB): dzien " + day.ToString(CultureInfo.InvariantCulture)
                     + " | ksiega per modyfikator " + (s.ArmouryBookPerModifier ? "TAK" : "NIE") + ": ksiega id " + ids + " szt. " + pcs + ", rozbicie kluczy " + keys + " szt. " + modPcs
                     + ", reszta bez rozbicia (najgorsze egzemplarze) " + rest + ", rozbicie ponad ksiege " + over
                     + " | porzadki z rozbiciem " + _dPurge + ", ustawione szt. " + _dSetPcs + ", z reszty bez rozbicia " + _dRest + ", migracja " + mig
                     + " | wklady ze stanem " + _dKeyIn + ", wyjecia ze stanem " + _dKeyOut + ", roszczenia zdjete (ponad ksiege) " + _dClip + ", przyciete do polek " + _dGhost
                     + ", przelaczenia klucza " + _dModeReset
                     + " | konie zapasu ludzi na liscie " + (HorsesListedOn ? "TAK" : "NIE") + ": otwarc " + _dHorseOpens + ", koni " + _dHorseListed
                     + " | nadwyzki - kazda grupa " + (AnyGroupOn ? "TAK" : "NIE") + ": uratowane od 'nikt nie udzwignie' gracz " + _dSurSaved + ", AI " + _dSurSavedAi
                     + " | pytanie o nocleg po wyspaniu " + (PromptSkipOn ? "TAK" : "NIE") + ": pominiete " + _dPromptSkip
                     + " | potkniecia " + _dStumble + (_firstStumble != null ? " (pierwsze " + _firstStumble + ")" : "") + ".");
            _dPurge = _dSetPcs = _dRest = _dKeyIn = _dKeyOut = _dClip = _dGhost = _dModeReset = 0;
            _dHorseOpens = _dHorseListed = _dSurSaved = _dSurSavedAi = _dPromptSkip = _dStumble = 0;
            _migIds = -1; _migPcs = 0; _firstStumble = null;
        }
    }
}
