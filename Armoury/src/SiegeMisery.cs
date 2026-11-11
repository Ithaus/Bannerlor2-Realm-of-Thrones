using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 399 (Jeff 11.11 "rob 1" - glod i choroby w oblezeniu): NEDZA OBLEZENIA - trupy pod murami i w murach, zima, ranni, palenie zmarlych.
    ///
    /// UWAGA: licznik trupow to ZNACZNIK SANITARNY, nie ksiega ludzi. Kazdy zmarly jest juz zaksiegowany tam, gdzie umarl (PeopleBook:
    /// FDiePlague zarazy, FDieBattle 391 i bitew, FDieSiege glodu) - tu nie ma drugiego ksiegowania ani konta "ciala". Liczby gasna same
    /// (SiegeCorpseDecayPercent na dobe) i znikaja z koncem oblezenia.
    ///
    /// Rekord na oblezenie (klucz: osada; zgodnosc oblezenia jak 374: |Start - SiegeStartTime.ToHours| &lt;= 0.05):
    ///   trupy POD MURAMI (strona oblegajacych) i W MURACH (strona obroncow) + pola na 400/411 (poczatek glodu, stan obsady, seria dob - na razie
    ///   tylko przechowywane w zapisie, nikt ich nie czyta) + flagi ostrzezen gracza. Od 400 poczatek glodu i stan obsady pisze i czyta glod GL
    ///   (SiegeWalls2.StarveOne przez HungerGet / HungerSet) - te dwa pola zyja takze przy wylaczonym kluczu trupow (rekord zostaje, konta trupow 0). Zapis "arm_siegemisery" (brak klucza w starym zapisie = pusto).
    /// Jedyne wejscie: AddCorpses(osada, wMurach, n, zrodlo). Pisza:
    ///   POD MURAMI - zmarli zarazy oblegajacych (CampFever), zmarli 391 (SiegeWounded373Behavior.LeftToDie391), zabici w bitwie oblezniczej
    ///                z druzyn spoza murow (MapEventParty.DiedInBattle, szeregowi bez Innych);
    ///   W MURACH   - zmarli z glodu GL (SiegeWalls2), zmarli zarazy obroncow, zabici w bitwie z zalogi / milicji / druzyn stojacych w osadzie;
    ///                od 402 takze zgony szeregowych w lochu (zrodlo SrcDungeon - na razie nikt nie pisze).
    /// Czynnik zarazy (CampFever.Strike; DODAWANY, nie mnozony): 1 + ZIMA + RANNI + TRUPY, najwyzej 3.0 -
    ///   TRUPY = min(SiegeCorpseFactorMax - 1, trupy strony / max(100, ludzie strony)) - stad CorpseTerm.
    /// Palenie (SiegeBurnDeadOption): gracz - dwie opcje w menu_siege_strategies (oblegajacy pali POD MURAMI, obronca W MURACH), koszt
    ///   ceil(trupy / SiegeBurnDeadPerHardwood) sztuk "hardwood" z jego jukow, widoczne od 20 trupow; skutek od razu w konsekwencji opcji
    ///   (bez przelaczania menu). AI - raz na dobe wodz obozu (drewno z jukow jego druzyny) i osada (drewno z polki osady) pala od 50 trupow,
    ///   ta sama stawka, ile drewna starczy. Drewno znika ze swiata: GoodsLedger.NotePyre ("stosy pogrzebowe oblezen (399)").
    ///   399-p: AI pali dopiero od 3 dob przed koncem inkubacji zarazy; oboz - drewnem z jukow wszystkich druzyn obozu (nie tylko wodza, bez gracza);
    ///   zamek - z wlasnego skladu; MIASTO - pan kupuje drewno z polki targu po cenie polki (kiesa glowy rodu -> kasa miasta), tylko ponad zapas
    ///   rzemiosla (BuildFunding.WoodReserve); bez nadwyzki albo zlota nie pali. Bitwa odsieczy z obozem (bez MapEventSettlement) tez liczy zabitych.
    /// Oblezenie fabuly ROT (CampFever.Spared): zaraza go nie dotyka, wiec trupow sie nie trzyma (konta zerowane w dobie, bez palenia i ostrzezen).
    /// </summary>
    internal static class SiegeMisery
    {
        internal const string Tag = "Trupy oblezenia";
        internal const int SrcFever = 0, Src391 = 1, SrcBattle = 2, SrcHunger = 3, SrcDungeon = 4, Srcs = 5;
        private const int PlayerBurnMin = 20;   // opcja gracza widoczna od tylu trupow
        private const int AiBurnMin = 50;       // AI pali od tylu trupow; ten sam prog ostrzezenia gracza
        private const int NearDays = 3;         // 399-p: AI pali i prog ostrzega dopiero od tylu dob przed koncem inkubacji zarazy
        private const int FWarnUnder = 1, FWarnWithin = 2, FWarnWinter = 4;
        private const string OptUnder = "arm399_burn_under", OptWithin = "arm399_burn_within";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Rec
        {
            public double Start;                 // SiegeStartTime.ToHours
            public double Under, Within;         // trupy pod murami / w murach
            public double HungerFrom = -1;       // 400: chwila poczatku glodu w dobach kampanii (-1 = brak) - pisze i czyta glod GL (SiegeWalls2)
            public int StartMen = -1;            // 400: stan obsady z poczatku oblezenia (-1 = brak) - pisze i czyta glod GL (SiegeWalls2)
            public int Streak;                   // 411: seria dob zagnilego obozu - tu tylko przechowywane
            public int Flags;                    // ostrzezenia gracza (FWarn*)
            // tylko sesja (linia doby): stan po poprzedniej linii, doszlo wedlug zrodla, spalone od poprzedniej linii
            public double WasU, WasW;
            public readonly int[] AddU = new int[Srcs], AddW = new int[Srcs];
            public double BurnU, BurnW;
            public int WoodU, WoodW;
            public string WhoU, WhoW;
            public string WhyW;                  // 399-p: czemu mury dzis nie palily (null = palily albo nie bylo czego)
        }

        private static readonly Dictionary<string, Rec> _recs = new Dictionary<string, Rec>(StringComparer.Ordinal);
        private static readonly List<string> _tmp = new List<string>();
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static int _stumbles;
        private static ItemObject _wood;
        private static string _hookMenu = "-";
        private static readonly List<MobileParty> _camp = new List<MobileParty>(8);   // 399-p: druzyny obozu przy paleniu AI (tylko watek glowny, doba)

        /// <summary>Pamiec od zera: konstruktor ArmouryBehavior (nowa kampania i kazde wczytanie) oraz Import.</summary>
        internal static void Reset()
        {
            _recs.Clear(); _tmp.Clear(); _err.Clear(); _stumbles = 0; _wood = null;
            _hookMenu = "-"; _camp.Clear();   // 399-p
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("SiegeMisery." + where, e); } catch { }
        }

        private static bool On { get { var s = Settings.Current; return s != null && s.SiegeSicknessEnabled && s.SiegeCorpsesBreedFever; } }   // bez zarazy trupy nic nie znacza

        private static ItemObject Wood()
        {
            if (_wood == null) { try { _wood = MBObjectManager.Instance.GetObject<ItemObject>("hardwood"); } catch { _wood = null; } }
            return _wood;
        }

        private static string Nm(Settlement st) { try { return st != null && st.Name != null ? st.Name.ToString() : "?"; } catch { return "?"; } }

        /// <summary>Rekord trwajacego oblezenia tej osady (inne oblezenie tej samej osady = nowy rekord). null bez oblezenia.</summary>
        private static Rec RecOf(Settlement st, bool create)
        {
            if (st == null || st.StringId == null || st.SiegeEvent == null) return null;
            double start = st.SiegeEvent.SiegeStartTime.ToHours;
            Rec r;
            if (_recs.TryGetValue(st.StringId, out r) && Math.Abs(r.Start - start) <= 0.05) return r;
            if (!create) return null;
            r = new Rec { Start = start };
            _recs[st.StringId] = r;
            return r;
        }

        // ------------------------------------------------------------ wejscie i odczyt

        /// <summary>JEDYNE wejscie licznika: n zmarlych szeregowych po stronie oblegajacych (within = false) albo obroncow (true) trwajacego
        /// oblezenia osady. Bez oblezenia / przy wylaczonym kluczu Siege Corpses Breed Fever - nic. Nie rzuca.</summary>
        internal static void AddCorpses(Settlement st, bool within, int n, int src)
        {
            try
            {
                if (n <= 0 || !On) return;
                var r = RecOf(st, true);
                if (r == null) return;
                if (src < 0 || src >= Srcs) src = SrcBattle;
                if (within) { r.Within += n; r.AddW[src] += n; }
                else { r.Under += n; r.AddU[src] += n; }
            }
            catch (Exception e) { Stumble("AddCorpses", e); }
        }

        /// <summary>Tylko odczyt: trupy jednej strony trwajacego oblezenia (0 bez rekordu).</summary>
        internal static double Corpses(Settlement st, bool within)
        {
            try
            {
                if (!On) return 0;
                var r = RecOf(st, false);
                return r == null ? 0 : (within ? r.Within : r.Under);
            }
            catch (Exception e) { Stumble("Corpses", e); return 0; }
        }

        /// <summary>Czlon TRUPY czynnika zarazy strony: min(SiegeCorpseFactorMax - 1, trupy strony / max(100, ludzie strony)). 0 przy wylaczonym kluczu.</summary>
        internal static float CorpseTerm(Settlement st, bool within, int sideMen)
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return 0f;
                double c = Corpses(st, within);   // 0 przy wylaczonym kluczu
                if (c <= 0) return 0f;
                float max = Math.Max(0f, s.SiegeCorpseFactorMax - 1f);
                return Math.Min(max, (float)(c / Math.Max(100, sideMen)));
            }
            catch (Exception e) { Stumble("CorpseTerm", e); return 0f; }
        }

        /// <summary>400: zapisany poczatek glodu (doby kampanii, -1 = nie gloduje) i stan obsady z poczatku trwajacego oblezenia (-1 = brak).
        /// false bez rekordu. Niezalezne od klucza trupow. Nie rzuca.</summary>
        internal static bool HungerGet(Settlement st, out double hungerFrom, out int startMen)
        {
            hungerFrom = -1; startMen = -1;
            try
            {
                var r = RecOf(st, false);
                if (r == null) return false;
                hungerFrom = r.HungerFrom; startMen = r.StartMen;
                return true;
            }
            catch (Exception e) { Stumble("HungerGet", e); return false; }
        }

        /// <summary>400: glod GL zapisuje poczatek glodu i stan obsady w rekordzie trwajacego oblezenia (rekord powstaje tylko, gdy jest co zapisac).
        /// Niezalezne od klucza trupow. Nie rzuca.</summary>
        internal static void HungerSet(Settlement st, double hungerFrom, int startMen)
        {
            try
            {
                if (double.IsNaN(hungerFrom) || double.IsInfinity(hungerFrom)) hungerFrom = -1;
                var r = RecOf(st, hungerFrom >= 0 || startMen >= 0);
                if (r == null) return;
                r.HungerFrom = hungerFrom; r.StartMen = startMen;
            }
            catch (Exception e) { Stumble("HungerSet", e); }
        }

        /// <summary>CampaignEvents.OnSiegeEventEndedEvent: koniec oblezenia - rekord osady znika. Nie rzuca.</summary>
        internal static void OnSiegeEnded(SiegeEvent ev)
        {
            try
            {
                var st = ev != null ? ev.BesiegedSettlement : null;
                if (st != null && st.StringId != null) _recs.Remove(st.StringId);
            }
            catch (Exception e) { Stumble("OnSiegeEnded", e); }
        }

        // ------------------------------------------------------------ bitwy obleznicze (MapEventEnded)

        /// <summary>CampaignEvents.MapEventEnded: zabici szeregowi (DiedInBattle, bez bohaterow i Innych) bitwy oblezniczej (szturm, wypad, bitwa pod
        /// murami) - z druzyn stojacych w osadzie W MURACH, z pozostalych POD MURAMI. Tylko licznik. Nie rzuca.</summary>
        internal static void OnMapEventEnded(MapEvent me)
        {
            try
            {
                if (me == null || !On) return;
                if (!me.IsSiegeAssault && !me.IsSallyOut && !me.IsSiegeOutside) return;
                var st = me.MapEventSettlement;
                // 399-p: bitwa odsieczy z obozem (MapEvent.StartSiegeOutsideMapEvent) nie ma MapEventSettlement - gra ustawia je tylko, gdy strona jest osada
                // albo stoi w osadzie (MapEvent.Initialize). Osada = oblegana przez pierwsza druzyne bitwy, ktora nalezy do obozu.
                if (st == null && me.IsSiegeOutside) st = CampSettlement(me);
                if (st == null || st.SiegeEvent == null) return;
                int under = 0, within = 0;
                for (int k = 0; k < 2; k++)
                {
                    var side = k == 0 ? me.AttackerSide : me.DefenderSide;
                    if (side == null || side.Parties == null) continue;
                    foreach (var mep in side.Parties)
                    {
                        try
                        {
                            if (mep == null || mep.Party == null) continue;
                            int d = Regulars(mep.DiedInBattle);
                            if (d <= 0) continue;
                            if (Inside(mep.Party, st)) within += d; else under += d;
                        }
                        catch (Exception e) { Stumble("OnMapEventEnded.Party", e); }
                    }
                }
                AddCorpses(st, false, under, SrcBattle);
                AddCorpses(st, true, within, SrcBattle);
            }
            catch (Exception e) { Stumble("OnMapEventEnded", e); }
        }

        /// <summary>399-p: osada oblegana przez pierwsza druzyne bitwy z obozu oblezniczego (najpierw obroncy bitwy - oboz napadniety przez odsiecz). null = brak.</summary>
        private static Settlement CampSettlement(MapEvent me)
        {
            for (int k = 0; k < 2; k++)
            {
                var side = k == 0 ? me.DefenderSide : me.AttackerSide;
                if (side == null || side.Parties == null) continue;
                foreach (var mep in side.Parties)
                {
                    var mp = mep != null && mep.Party != null ? mep.Party.MobileParty : null;
                    if (mp == null || mp.BesiegerCamp == null || mp.BesiegerCamp.SiegeEvent == null) continue;
                    var st = mp.BesiegerCamp.SiegeEvent.BesiegedSettlement;
                    if (st != null) return st;
                }
            }
            return null;
        }

        private static int Regulars(TroopRoster r)
        {
            if (r == null) return 0;
            int n = 0;
            for (int i = 0; i < r.Count; i++)
            {
                var ch = r.GetCharacterAtIndex(i);
                if (ch == null || ch.IsHero || Undead.Character(ch)) continue;
                n += Math.Max(0, r.GetElementNumber(i));
            }
            return n;
        }

        /// <summary>Druzyna strony obroncow murow: sama osada, jej zaloga i milicja, druzyna stojaca w osadzie.</summary>
        private static bool Inside(PartyBase pb, Settlement st)
        {
            if (pb == st.Party) return true;
            var mp = pb.MobileParty;
            if (mp == null) return false;
            if (mp.CurrentSettlement == st) return true;
            return (mp.IsGarrison || mp.IsMilitia) && mp.HomeSettlement == st;
        }

        // ------------------------------------------------------------ doba

        /// <summary>Doba (po CampFever.OnDaily, przed SiegeWalls2.Daily): rekordy bez oblezenia znikaja, konta gasna, AI pali, ostrzezenia gracza, linia.</summary>
        internal static void DailyCorpses()
        {
            try
            {
                if (_recs.Count == 0) return;
                var s = Settings.Current;
                if (s == null) return;
                _tmp.Clear();
                foreach (var kv in _recs) _tmp.Add(kv.Key);
                int day = 0;
                try { day = (int)CampaignTime.Now.ToDays; } catch { }
                for (int i = 0; i < _tmp.Count; i++)
                {
                    string id = _tmp[i];
                    try
                    {
                        Rec r;
                        if (!_recs.TryGetValue(id, out r)) continue;
                        Settlement st = null;
                        try { st = Settlement.Find(id); } catch { st = null; }
                        if (st == null || st.SiegeEvent == null || Math.Abs(st.SiegeEvent.SiegeStartTime.ToHours - r.Start) > 0.05) { _recs.Remove(id); continue; }
                        if (!On)
                        {
                            // 400: klucz trupow wylaczony - konta trupow na zero; rekord zostaje tylko dla pol glodu GL (poczatek glodu, stan obsady)
                            if (r.HungerFrom < 0 && r.StartMen < 0) { _recs.Remove(id); continue; }
                            r.Under = 0; r.Within = 0; ClearDay(r);
                            continue;
                        }
                        DayOf(s, st, r, day);
                    }
                    catch (Exception e) { Stumble("DailyCorpses.Rec", e); }
                }
                _tmp.Clear();
            }
            catch (Exception e) { Stumble("DailyCorpses", e); }
        }

        private static void DayOf(Settings s, Settlement st, Rec r, int day)
        {
            var siege = st.SiegeEvent;
            int addU = 0, addW = 0;
            for (int k = 0; k < Srcs; k++) { addU += r.AddU[k]; addW += r.AddW[k]; }

            // oblezenie fabuly ROT: zaraza go nie dotyka - trupow sie nie trzyma
            if (CampFever.Spared(siege) != null)
            {
                r.Under = 0; r.Within = 0;
                ClearDay(r);
                return;
            }

            // gasniecie. Kolejnosc doby (399-p, swiadomie): CampFever.OnDaily -> tu -> SiegeWalls2.Daily, wiec zmarli zarazy z tej doby gasna raz, zanim wejda
            // do czynnika, a zmarli z glodu (dopisani po nas) wchodza jutro w calosci; "zgaslo" w linii liczy sie takze od "doszlo". Roznica 10% miedzy zrodlami.
            double pct = Math.Min(100.0, Math.Max(0.0, s.SiegeCorpseDecayPercent)) / 100.0;
            double fadeU = r.Under * pct, fadeW = r.Within * pct;
            r.Under -= fadeU; r.Within -= fadeW;
            if (r.Under < 0.5) r.Under = 0;
            if (r.Within < 0.5) r.Within = 0;

            // 399-p: przed inkubacja zarazy trupy nic nie robia (gasna same) - AI nie pali na zapas, gracz nie dostaje ostrzezenia o chorobie, ktorej nie ma;
            // od 3 dob przed koncem inkubacji juz tak (trupy z tych dob waza jeszcze, gdy zaraza wstanie)
            bool near = CampFever.ActiveWithin(st, NearDays);

            // palenie AI
            if (s.SiegeBurnDeadOption && near)
            {
                try { AiBurn(s, st, siege, r); } catch (Exception e) { Stumble("AiBurn", e); }
            }

            // ostrzezenia gracza
            try { Warn(st, siege, r, near); } catch (Exception e) { Stumble("Warn", e); }

            if (r.WasU + r.WasW + addU + addW + r.BurnU + r.BurnW >= 1.0)
                Log.Info(Tag + ": dzien " + day + " | " + st.StringId
                         + " | pod murami " + N(r.WasU) + "/" + addU + " (zaraza " + r.AddU[SrcFever] + ", 391 " + r.AddU[Src391] + ", bitwa " + r.AddU[SrcBattle] + ")/" + N(fadeU)
                         + "/" + N(r.BurnU) + (r.BurnU > 0 ? " (" + r.WhoU + ", drewna " + r.WoodU + ")" : "") + "/" + N(r.Under)
                         + " | w murach " + N(r.WasW) + "/" + addW + " (glod " + r.AddW[SrcHunger] + ", zaraza " + r.AddW[SrcFever] + ", bitwa " + r.AddW[SrcBattle] + ", loch " + r.AddW[SrcDungeon] + ")/" + N(fadeW)
                         + "/" + N(r.BurnW) + (r.BurnW > 0 ? " (" + r.WhoW + ", drewna " + r.WoodW + ")" : "") + "/" + N(r.Within)
                         + " [bylo/doszlo/zgaslo/spalone/jest]"
                         + (r.WhyW != null ? " | mury nie pala: " + r.WhyW : "") + (near ? "" : " | przed inkubacja zarazy - AI nie pali")
                         + (_stumbles > 0 ? " | potkniecia " + _stumbles : "") + ".");
            ClearDay(r);
        }

        private static void ClearDay(Rec r)
        {
            r.WasU = r.Under; r.WasW = r.Within;
            Array.Clear(r.AddU, 0, Srcs); Array.Clear(r.AddW, 0, Srcs);
            r.BurnU = 0; r.BurnW = 0; r.WoodU = 0; r.WoodW = 0; r.WhoU = null; r.WhoW = null; r.WhyW = null;
        }

        private static string N(double v) { return ((int)Math.Round(v)).ToString(Inv); }

        private static int Per(Settings s) { return Math.Max(1, s.SiegeBurnDeadPerHardwood); }

        /// <summary>AI: wodz obozu pali POD MURAMI drewnem z jukow swojej druzyny, osada W MURACH drewnem z polki osady - od AiBurnMin trupow,
        /// ile drewna starczy. Druzyna / osada w bitwie, Inni i strona gracza (decyduje sam) - bez palenia.</summary>
        private static void AiBurn(Settings s, Settlement st, SiegeEvent siege, Rec r)
        {
            var wood = Wood();
            if (wood == null) return;
            int per = Per(s);
            var main = MobileParty.MainParty;

            if (r.Under >= AiBurnMin)
            {
                // 399-p: wodz AI decyduje, ale drewno idzie z jukow CALEGO obozu (najpierw wodza, potem pozostalych druzyn) - bez gracza (pali sam z menu),
                // bez Innych i bez druzyn w bitwie. Kazda druzyna pali swoim drewnem: wlasciciel = platnik.
                var camp = siege.BesiegerCamp;
                var lead = camp != null ? camp.LeaderParty : null;
                if (lead != null && lead != main && lead.MapEvent == null && !Undead.Party(lead))
                {
                    _camp.Clear();
                    _camp.Add(lead);
                    foreach (var pb in camp.GetInvolvedPartiesForEventType())
                    {
                        var mp = pb != null ? pb.MobileParty : null;
                        if (mp != null && mp != main && !_camp.Contains(mp)) _camp.Add(mp);
                    }
                    int usedAll = 0, givers = 0; double burnedAll = 0;
                    for (int i = 0; i < _camp.Count && r.Under >= 0.5; i++)
                    {
                        var mp = _camp[i];
                        try
                        {
                            if (mp.MapEvent != null || mp.ItemRoster == null || Undead.Party(mp)) continue;
                            double burned; int used = Burn(mp.ItemRoster, wood, r.Under, per, out burned);
                            if (used <= 0) continue;
                            r.Under -= burned; if (r.Under < 0.5) r.Under = 0;
                            usedAll += used; burnedAll += burned; givers++;
                        }
                        catch (Exception e) { Stumble("AiBurn.Camp", e); }
                    }
                    _camp.Clear();
                    if (usedAll > 0) { r.BurnU += burnedAll; r.WoodU += usedAll; r.WhoU = "oboz wodza " + lead.StringId + ", druzyn z drewnem " + givers; }
                }
            }

            if (r.Within >= AiBurnMin)
            {
                bool fight = st.Party != null && st.Party.MapEvent != null;
                bool playersOwn = main != null && main.CurrentSettlement == st && st.OwnerClan != null && st.OwnerClan == Clan.PlayerClan;   // gracz-pan w swoich murach decyduje sam
                var gar = st.Town != null ? st.Town.GarrisonParty : null;
                if (!fight && !playersOwn && st.ItemRoster != null && !(gar != null && Undead.Party(gar)))
                    BurnWithin(st, wood, per, r);
            }
        }

        /// <summary>399-p (recenzja, "nic z niczego"): mury pala W MURACH. ZAMEK - drewnem z wlasnego skladu (nie jest targiem; sklad nalezy do zalogi),
        /// jak wodz obozu z jukow. MIASTO - drewno lezy na polce TARGU (towar kupcow, na tej samej polce stoi zapas rzemiosla K1): pan miasta KUPUJE je
        /// po cenie polki (kiesa glowy rodu -> kasa miasta, sztuka po sztuce przez ShelfBuy), tylko nadwyzke ponad zapas rzemiosla
        /// (BuildFunding.WoodReserve - ten sam prog co budowy pana); bez nadwyzki albo bez zlota - nie pali (trupy gasna same).</summary>
        private static void BurnWithin(Settlement st, ItemObject wood, int per, Rec r)
        {
            if (!st.IsTown || st.Town == null)
            {
                double burned; int used = Burn(st.ItemRoster, wood, r.Within, per, out burned);
                if (used > 0) { r.Within -= burned; if (r.Within < 0.5) r.Within = 0; r.BurnW += burned; r.WoodW += used; r.WhoW = "zamek, wlasny sklad"; }
                else r.WhyW = "zamek bez drewna w skladzie";
                return;
            }
            int shelf = st.ItemRoster.GetItemNumber(wood);
            int keep = BuildFunding.WoodReserve(st, wood);
            int spare = shelf - keep;
            if (spare <= 0) { r.WhyW = "bez nadwyzki drewna (polka " + shelf + ", zapas rzemiosla " + keep + ")"; return; }
            var clan = st.OwnerClan;
            var lord = clan != null ? clan.Leader : null;
            if (lord == null || !lord.IsAlive || lord.Gold <= 0) { r.WhyW = "pan bez zlota"; return; }
            int need = (int)Math.Ceiling(r.Within / per);
            var el = new EquipmentElement(wood);
            var town = st.Town;
            int cost, first, last;
            int n = ShelfBuy.Take(st.ItemRoster, el, Math.Min(need, spare), lord.Gold, () => Math.Max(1, town.GetItemPrice(el, null, false)), out cost, out first, out last, 0, false);
            if (n <= 0) { r.WhyW = "pana nie stac (kiesa " + lord.Gold + ")"; return; }
            GoodsLedger.NotePyre(wood, n);
            if (cost > 0)
            {
                lord.ChangeHeroGold(-cost);
                CirculationWindows.NoteHeroGold(lord, -cost);       // glowa poza swiatem - tylko licznik
                town.ChangeGold(cost);
                ClanIncomeBook.NoteOwnPaid(st, clan, cost);         // 110-p (K8): wplata rodu do kasy wlasnego miasta (tylko licznik "wlasne")
                try { TownFolk.Sale(st, cost, TownFolk.MixBuild); } catch { }   // sprzedaz kupcow targu (tylko licznik; drewno = material)
            }
            double done = Math.Min(r.Within, (double)n * per);
            r.Within -= done; if (r.Within < 0.5) r.Within = 0;
            r.BurnW += done; r.WoodW += n;
            r.WhoW = "miasto, pan " + (clan.StringId ?? "?") + " zaplacil targowi " + cost;
            if (n < Math.Min(need, spare)) r.WhyW = "kiesa pana starczyla na " + n + " z " + Math.Min(need, spare);
            if (clan == Clan.PlayerClan)
                Log.Player("Your steward at " + Nm(st) + " bought " + n + " hardwood for " + cost + " denars and burned the dead within the walls.");
        }

        /// <summary>Zdejmuje z rosteru tyle drewna, ile trzeba na te trupy (najwyzej ile jest); zwraca zuzyte sztuki, burned = spalone trupy. Drewno do ksiegi towarow.</summary>
        private static int Burn(ItemRoster roster, ItemObject wood, double corpses, int per, out double burned)
        {
            burned = 0;
            int need = (int)Math.Ceiling(corpses / per);
            int have = roster.GetItemNumber(wood);
            int use = Math.Min(need, have);
            if (use <= 0) return 0;
            roster.AddToCounts(wood, -use);
            GoodsLedger.NotePyre(wood, use);
            burned = Math.Min(corpses, (double)use * per);
            return use;
        }

        private static void Warn(Settlement st, SiegeEvent siege, Rec r, bool near)
        {
            var main = MobileParty.MainParty;
            if (main == null) return;
            bool attacks = siege.BesiegerCamp != null && main.BesiegerCamp == siege.BesiegerCamp;
            bool defends = !attacks && main.CurrentSettlement == st;
            if (!attacks && !defends) return;
            double mine = attacks ? r.Under : r.Within;
            int flag = attacks ? FWarnUnder : FWarnWithin;
            if (near && mine >= AiBurnMin && (r.Flags & flag) == 0)
            {
                r.Flags |= flag;
                Log.Player((int)Math.Round(mine) + " dead lie unburied " + (attacks ? "beneath" : "within") + " the walls of " + Nm(st) + ". They breed sickness.", true);
            }
            if ((r.Flags & FWarnWinter) == 0 && mine >= 1.0 && CampFever.Active(st))
            {
                float w = attacks ? CampFever.WinterTerm(main) : CampFever.WinterTerm(st);
                if (w > 0f)
                {
                    r.Flags |= FWarnWinter;
                    Log.Player("Winter and the unburied dead feed the fever at " + Nm(st) + ".", true);
                }
            }
        }

        // ------------------------------------------------------------ opcje gracza (menu_siege_strategies)

        /// <summary>CampaignEvents.OnAfterSessionLaunchedEvent: dwie opcje palenia zmarlych przed "Leave" w menu_siege_strategies. Nie rzuca.</summary>
        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                var mgr = Campaign.Current != null ? Campaign.Current.GameMenuManager : null;
                var menu = mgr != null ? mgr.GetGameMenu("menu_siege_strategies") : null;
                if (starter == null || menu == null) { _hookMenu = "BRAK (menu_siege_strategies nie znalezione)"; }
                else
                {
                    int idx = -1, i = 0; bool had = false;
                    foreach (var o in menu.MenuOptions)
                    {
                        if (o != null && o.IdString == OptUnder) had = true;
                        if (o != null && o.IdString == "menu_siege_strategies_leave" && idx < 0) idx = i;
                        i++;
                    }
                    if (had) _hookMenu = "dodane (juz byly)";
                    else
                    {
                        // ta sama pozycja dla obu: druga wstawiona przed pierwsza, wiec kolejnosc na ekranie = pod murami, w murach (widac zawsze najwyzej jedna)
                        starter.AddGameMenuOption("menu_siege_strategies", OptWithin, "{=!}{ARM_PYRE_WITHIN}", a => OptCondition(a, true), a => OptConsequence(a, true), false, idx);
                        starter.AddGameMenuOption("menu_siege_strategies", OptUnder, "{=!}{ARM_PYRE_UNDER}", a => OptCondition(a, false), a => OptConsequence(a, false), false, idx);
                        _hookMenu = "dodane (pozycja " + idx + ")";
                    }
                }
            }
            catch (Exception e) { _hookMenu = "BRAK (blad: " + e.Message + ")"; Stumble("AddMenus", e); }
            try
            {
                var s = Settings.Current;
                Log.Info(Tag + " (399): opcje palenia w menu oblezenia " + _hookMenu + " | klucze: trupy " + (s != null && s.SiegeCorpsesBreedFever ? "TAK" : "NIE")
                         + ", palenie " + (s != null && s.SiegeBurnDeadOption ? "TAK" : "NIE") + ", zima " + (s != null ? s.SiegeFeverWinterFactor.ToString("0.00", Inv) : "-")
                         + ", ranni " + (s != null && s.SiegeFeverWoundedCounts ? "TAK" : "NIE") + " | rekordy z zapisu " + _recs.Count + ".");
            }
            catch { }
        }

        /// <summary>Stan opcji gracza: oblezenie, w ktorym gracz stoi po tej stronie, i trupy tej strony. false = opcji nie ma.</summary>
        private static bool PlayerState(bool within, out Settlement st, out Rec r)
        {
            st = null; r = null;
            var s = Settings.Current;
            if (s == null || !s.SiegeBurnDeadOption || !On) return false;
            var main = MobileParty.MainParty;
            if (main == null || main.MapEvent != null) return false;
            var ev = PlayerSiege.PlayerSiegeEvent;
            if (ev == null || ev.BesiegedSettlement == null) return false;
            if ((PlayerSiege.PlayerSide == BattleSideEnum.Defender) != within) return false;
            st = ev.BesiegedSettlement;
            r = RecOf(st, false);
            if (r == null) return false;
            if ((within ? r.Within : r.Under) < PlayerBurnMin) return false;
            return CampFever.Spared(ev) == null;
        }

        private static bool OptCondition(MenuCallbackArgs args, bool within)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Default;
                Settlement st; Rec r;
                if (!PlayerState(within, out st, out r)) return false;
                var s = Settings.Current;
                var wood = Wood();
                int need = (int)Math.Ceiling((within ? r.Within : r.Under) / Per(s));
                MBTextManager.SetTextVariable(within ? "ARM_PYRE_WITHIN" : "ARM_PYRE_UNDER",
                    (within ? "Burn the dead in the yard (" : "Burn the dead beneath the walls (") + need + " hardwood)", false);
                int have = wood != null && MobileParty.MainParty.ItemRoster != null ? MobileParty.MainParty.ItemRoster.GetItemNumber(wood) : 0;
                if (have < need)
                {
                    args.IsEnabled = false;
                    args.Tooltip = new TextObject("{=!}You need " + need + " hardwood to build the pyres.");
                }
                return true;
            }
            catch (Exception e) { Stumble("OptCondition", e); return false; }
        }

        /// <summary>Skutek od razu: drewno z jukow gracza znika, konto strony gracza na zero. Bez przelaczania menu (menu oczekiwania - CLAUDE.md 7).</summary>
        private static void OptConsequence(MenuCallbackArgs args, bool within)
        {
            try
            {
                Settlement st; Rec r;
                if (!PlayerState(within, out st, out r)) return;
                var s = Settings.Current;
                var wood = Wood();
                var roster = MobileParty.MainParty.ItemRoster;
                if (wood == null || roster == null) return;
                double corpses = within ? r.Within : r.Under;
                int need = (int)Math.Ceiling(corpses / Per(s));
                if (roster.GetItemNumber(wood) < need) return;
                double burned; int used = Burn(roster, wood, corpses, Per(s), out burned);
                if (used <= 0) return;
                if (within) { r.Within = 0; r.BurnW += corpses; r.WoodW += used; r.WhoW = "gracz"; }
                else { r.Under = 0; r.BurnU += corpses; r.WoodU += used; r.WhoU = "gracz"; }
                Log.Player("The pyres burn through the night. The stench lifts.");
                Log.Info(Tag + ": gracz spalil " + N(corpses) + " trupow " + (within ? "w murach" : "pod murami") + " " + st.StringId + " za " + used + " drewna.");
            }
            catch (Exception e) { Stumble("OptConsequence", e); }
        }

        // ------------------------------------------------------------ zapis

        private static readonly char[] Bad = { '|', ';', ':' };
        private static string D(double v) { return v.ToString("R", Inv); }
        private static bool P(string s, out double v) { return double.TryParse(s, NumberStyles.Float, Inv, out v); }

        /// <summary>"v1|osada:start:podMurami:wMurach:poczatekGlodu:stanObsady:seria:flagi;..." - tylko rekordy trwajacych oblezen.</summary>
        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(128);
                sb.Append("v1|");
                bool f = true;
                foreach (var kv in _recs)
                {
                    var r = kv.Value;
                    Settlement st = null;
                    try { st = Settlement.Find(kv.Key); } catch { st = null; }
                    if (st == null || st.SiegeEvent == null || Math.Abs(st.SiegeEvent.SiegeStartTime.ToHours - r.Start) > 0.05 || kv.Key.IndexOfAny(Bad) >= 0) continue;
                    if (!f) sb.Append(';'); f = false;
                    sb.Append(kv.Key).Append(':').Append(D(r.Start)).Append(':').Append(D(r.Under)).Append(':').Append(D(r.Within)).Append(':').Append(D(r.HungerFrom))
                      .Append(':').Append(r.StartMen.ToString(Inv)).Append(':').Append(r.Streak.ToString(Inv)).Append(':').Append(r.Flags.ToString(Inv));
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        /// <summary>Wczytanie (brak klucza w starym zapisie = pusto). Rekordy oblezen, ktorych juz nie ma, usuwa pierwsza doba (DailyCorpses) i RecOf.</summary>
        internal static void Import(string data)
        {
            Reset();
            int bad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var f = data.Split('|');
                if (f.Length < 2 || f[0] != "v1") { Log.Info(Tag + ": zapis nieczytelny - od zera."); return; }
                foreach (var p in f[1].Split(';'))
                {
                    if (p.Length == 0) continue;
                    var x = p.Split(':'); double start, u, w, hf; int men, streak, flags;
                    if (x.Length == 8 && x[0].Length > 0 && P(x[1], out start) && P(x[2], out u) && P(x[3], out w) && P(x[4], out hf)
                        && int.TryParse(x[5], NumberStyles.Integer, Inv, out men) && int.TryParse(x[6], NumberStyles.Integer, Inv, out streak) && int.TryParse(x[7], NumberStyles.Integer, Inv, out flags)
                        && u >= 0 && w >= 0 && !double.IsNaN(u) && !double.IsNaN(w) && !double.IsInfinity(u) && !double.IsInfinity(w))
                    {
                        // 400-p: pola glodu z recznie zepsutego zapisu = brak (NaN / nieskonczonosc / ujemne -> -1)
                        if (double.IsNaN(hf) || double.IsInfinity(hf) || hf < 0) hf = -1;
                        if (men < 0) men = -1;
                        var r = new Rec { Start = start, Under = u, Within = w, HungerFrom = hf, StartMen = men, Streak = streak, Flags = flags };
                        r.WasU = u; r.WasW = w;
                        _recs[x[0]] = r;
                    }
                    else bad++;
                }
                if (_recs.Count > 0 || bad > 0)
                    Log.Info(Tag + ": wczytano z zapisu - rekordy trwajacych oblezen (od 400 kazde, takze bez trupow) " + _recs.Count + (bad > 0 ? " (bledne " + bad + ")" : "") + ".");
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
