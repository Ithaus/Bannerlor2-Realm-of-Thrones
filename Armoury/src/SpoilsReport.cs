using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P5 "okno-lupu" (Jeff 25.10: "po bitwie i po oblezeniu i po turnieju, powinien wyskakiwac popup z dokladna iloscia lootu, ile zlota, ile zlota
    /// w sprzecie ... w klimacie"; projekt noc/projekty/okno-lupu.json, krytyk K11/K16; klucze SpoilsWindowAfterBattle, SpoilsWindowAfterSiege,
    /// SpoilsWindowAfterTourney, SpoilsWindowMinWorth).
    /// ZASADA (jedna dla bitwy i oblezenia; "nic z niczego" - TYLKO ODCZYT, zadna latka nie zmienia wyniku ani nie zwraca false): raport sklada sie z RAMEK.
    /// Ramka = roznica stanu prawdziwych posiadaczy przed i po kroku, w ktorym czas kampanii stoi: kiesa gracza (Hero.MainHero.Gold), sakiewka ludzi gracza
    /// (MenPurse.Get), sakwy (MainParty.ItemRoster - sztuka wedlug przedmiotu i stanu), loch (MainParty.PrisonRoster), slawa i wplywy rodu gracza.
    /// Ramki sie NIE zagniezdzaja (licznik glebokosci: wewnetrzna ramka nic nie liczy, gdy zewnetrzna jest otwarta) - zadna sztuka ani zlotowka nie liczy sie dwa razy.
    /// Etykiety zlota biora istniejace liczby (PlunderedGold - GoldLost gracza z MapEventParty, kiesy rozbitych BeatenPurse pkt 3 i karawan EnemyCaravans,
    /// wyniki CitySack, skrzynia zalogi, okup SiegeRansom); czego etykieta nie tlumaczy - wiersz reszty "Other" (recenzja P5 (3): takze w bitwie - ramka A obejmuje
    /// OnPlayerBattleEnd i wszystkie nasluchy MapEventEnded, wiec reszta to nie zawsze kiesy pokonanych): suma czesci = roznica kiesy.
    /// Korona (recenzja P5 (2)): roznica podstawy prawa trzecich gracza w Ransom178 przed i po ramce (PendingClaim - ten sam wzor co ThirdsSettle o swicie);
    /// obejmuje lup z kies wodzow (BattlePlunder), kiesy rozbitych (BeatenPurse pkt 3), sakwe do sakiewek ludzi (MenPurse), ekran lupu gry (ScreenLootPrefix),
    /// skrzynie zalogi i czesc grabiezy (CitySack) - tylko odczyt. Czesc lupu z kies (BeatenPurse.SplitPostfix) gra liczy w MapEvent.OnBattleWon, PRZED ramka A -
    /// jej przyrost podaje NoteCrownEarly, a ramka A tej samej bitwy go dolicza.
    /// Gdy w ramce ruszyl sie czas kampanii (CampaignTime) - znacznik "czas plynal": zostaja tylko etykiety, bez reszty i bez korony (linia logu mowi to wprost).
    /// RAMKI v1:
    ///  A pole (bitwa i szturm): prefiks First PlayerEncounter.DoApplyMapEventResults -> finalizer Last PlayerEncounter.DoEnd (lup gry, jency, ekran jencow,
    ///    ekran lupu gry w oblezeniu/kryjowce/morzu, CommitGoldChanges, FinalizeEvent: BeatenPurse pkt 3, MenPurse.OnPartyDestroyed, nastepstwa oblezenia AI).
    ///    Etykieta: prefiks Last MapEventParty.CommitGoldChanges (po oknie ksiegi O14 First, BattlePlunder Normal, ShipsToPort181) - Party == MainParty.
    ///    Bez DoEnd (przegrana, niewola) - ramke zamyka nastepna bitwa albo straznik AppTick (bez spotkania i bitwy).
    ///  S ekrany Spoils: prefiks OpenLootSelectionScreen / OpenBaggageLootScreen -> postfiks Last OnLootScreenClosed / OnBaggageLootScreenClosed (po latkach SpoilsSeal);
    ///    recenzja P5 (4): sakwy RAZEM z rynsztunkiem bohaterow druzyny (bojowy, cywilny, skrytu - jak InventoryLogic.PartyEquipment): sztuka zalozona na bohatera
    ///    prosto z listy trofeow jest lupem, stara sztuka zdjeta do sakw - nie. Kontrola: suma sztuk wobec _takenCount Spoils (Spoils liczy tylko sakwy po przedmiocie,
    ///    my po przedmiocie i stanie; sztuka zalozona z listy zgadza sie liczba, wlasny rynsztunek zdjety do sakw - "NIE": Spoils liczy go jako trofeum).
    ///  L Leave Spoils: prefiks First OnCompleteLeaveConsequence (PRZED SpoilsSeal.DrillLeavePrefix) - reszta _lootScreenRoster = "Left on the field" + ramka
    ///    przez cala metode (monety Spoils, dzis 0 przez SpoilsSeal) -> finalizer; OnBaggageDoneConsequence - ramka (zloto taboru, dzis 0).
    ///  F sakwy: BattlefieldLaw.FlushShareToBaggage / FlushMountsToBaggage - ramka tylko, gdy zadna inna nie jest otwarta (godzina bez ekranu, Send troops).
    ///  G oblezenie: prefiks First / finalizer Last CampaignEventDispatcher.OnSiegeAftermathApplied (wszystkie nasluchy: CitySack L2/L7, wyplata gry) - tylko,
    ///    gdy gracz atakuje, jest w partyContributions albo w armii atakujacego. Etykiety: CitySack.Messages (NoteSack), CitySack.Capture (skrzynia zalogi).
    ///  R okup oblezenia (SiegeRansom O1/O3): bez ramki - liczby SiegeRansom (ToLead, ToMen, korona) do osobnego raportu "The Ransom of X".
    ///  T turniej (noc P11, 125 - SpoilsReport.Tourney.cs): od OnPlayerJoinedTournamentEvent do wyjscia z szranek (po misji, mapa, GrandTourney bez kolejki
    ///    wykupu, bez zapytania); etykiety z zakresow roznicy kiesy wokol wrapperow PlayerFees8, sakiewki mieszczan i metod GrandTourney (odbicie, Harmony w kampanii).
    /// KIEDY GOTOWY: bitwa polowa - po FinishCollection Spoils (faza Spoils None), szturm wygrany - po ramce G tej osady (najwyzej 12 h kampanii), reszta po DoEnd;
    /// recenzja P5 (1): raport bitwy czeka tez, dopoki dzialka gracza lezy w kolejce BattlefieldLaw (SharePending - bitwa bez ekranu Spoils: zaciezny ROT,
    /// Spoils wyszedl z OnMapEventEnded wczesniej) - sakwy przychodza z terminu ArmouryBehavior._lootFlushDue (+1 h, sprawdzany w HourlyTick, wiec najpozniej
    /// 2 h po bitwie): ramka F zwalnia czekanie, straznik - pusta kolejka albo 2.5 h kampanii. Przegrana, remis, odwrot - bez okna (linia logu). Nowa bitwa przed pokazaniem okna dopisuje sie do tego samego raportu ("Spoils of 2 Battles").
    /// Okno pokazuje SpoilsWindow (bramka Prologue.SceneGate itd.). Wszystko watek glowny; potkniecia liczone, bez globalnego wylacznika. Raport w pamieci, bez zapisu
    /// (wczytanie zapisu w trakcie zbierania Spoils = brak raportu; Reset w konstruktorze ArmouryBehavior i OnGameEnd).
    /// GRACZ vs AI: tylko bitwy gracza (IsPlayerMapEvent), nastepstwa z jego udzialem; AI nic nie widzi i niczego nie traci.
    /// </summary>
    internal static partial class SpoilsReport
    {
        internal const string Tag = "Okno lupu (117): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal const int KBattle = 1, KSiege = 2, KRansom = 3, KTourney = 4;
        internal const string LPlunder = "Plunder from the beaten";
        internal const string LPurses = "Purses and chests of the beaten";
        internal const string LField = "Coin found on the field";
        internal const string LSack = "Your part of the sack";
        internal const string LChest = "The broken garrison's chest";
        internal const string LRansom = "Your part of the ransom";
        internal const string LOther = "Other";

        // ------------------------------------------------------------ model raportu

        internal sealed class Gain
        {
            public EquipmentElement E;
            public int N;
        }

        internal sealed class Rep
        {
            public int Id;
            public int Kind = KBattle;
            public int Battles;
            public string Place = "", Enemy = "", Date = "";
            public Settlement Town;               // osada szturmu / okupu
            public int SackKind = -1;             // CitySack.KMercy / KPillage / KDevastate (-1 - bez nastepstw)
            public Settlement Near;               // 150b: osada miejsca bitwy (MapEventSettlement albo najblizsza) - region obrazka okna
            public string Foe = "";               // 150b: "undead" / "wild" / "" - wrog pierwszej bitwy raportu (obrazek okna)
            public bool Sea;                      // 150b: bitwa na morzu (MapEvent.IsNavalMapEvent) - obrazek okna
            public readonly List<string> CoinLabels = new List<string>();
            public readonly List<long> CoinAmounts = new List<long>();
            public long Men;                      // do sakiewek ludzi gracza (MenPurse)
            public long Crown;                    // korona upomni sie o (ramki: roznica Ransom178.PendingClaim; okup: liczby SiegeRansom - przypis)
            public readonly Dictionary<string, Gain> Gear = new Dictionary<string, Gain>(StringComparer.Ordinal);
            public int LeftN; public long LeftW;
            public readonly Dictionary<CharacterObject, int> Troops = new Dictionary<CharacterObject, int>();
            public readonly List<Hero> Lords = new List<Hero>();
            public int Fallen, Wounded;
            public float Renown, Influence;
            public bool TimeMoved;
            public string WaitFor = "";           // "Spoils" / "oblezenie" / "sakwy" / "" (gotowy, gdy brak ramki)
            public double WaitUntil;              // dzien kampanii - koniec czekania na ramke G albo na sakwy
            public bool ShareWaitDone;            // czekanie na sakwy juz bylo dla tej bitwy (bez ponownego wejscia po terminie; nowa bitwa raportu - od nowa)
            public bool Ready, Shown, Done;
            public double ReadyDay;
            public long ReadyTicks;
            public int Given;                     // sztuki oddane z sakw w ramkach (ujemna roznica) - tylko log
            public int SN, SpN; public long SW, SpW;   // ekran trofeow: nasze sztuki i wartosc, Spoils _takenCount/_takenValue
            public readonly StringBuilder Lines = new StringBuilder();   // segmenty linii logu ramek
            public TInfo T;                       // noc P11: turniej (szranki, mistrz, nagroda, oddany rynsztunek) - tylko KTourney
            public bool Test;                     // noc P11: raport proby autotestu (linia wyniku przy Finish)

            internal long CoinTotal { get { long s = 0; foreach (var v in CoinAmounts) s += v; return s; } }
            internal long GearWorth
            {
                get { long s = 0; foreach (var g in Gear.Values) s += (long)Math.Max(0, g.E.ItemValue) * g.N; return s; }
            }
            internal int GearCount { get { int s = 0; foreach (var g in Gear.Values) s += g.N; return s; } }
            internal int TroopCount { get { int s = 0; foreach (var v in Troops.Values) s += v; return s; } }
            internal long RansomWorth;            // policzone przy zamknieciu ramki jencow (wycena modelu / Ransom178)
            internal bool AnythingWon { get { return CoinTotal > 0 || Men > 0 || GearCount > 0 || TroopCount > 0 || Lords.Count > 0; } }
            /// <summary>noc P11: turniej - okno takze przy samych stratach (zaklady, wykup konia i zbroi), bo gracz chce wiedziec, co zostalo.</summary>
            internal bool AnythingTourney
            {
                get
                {
                    foreach (var v in CoinAmounts) if (v != 0) return true;
                    return GearCount > 0 || Math.Abs(Renown) >= 0.5f || Math.Abs(Influence) >= 0.5f || (T != null && T.LostN > 0);
                }
            }

            internal void AddCoin(string label, long v)
            {
                if (v == 0 || string.IsNullOrEmpty(label)) return;
                int i = CoinLabels.IndexOf(label);
                if (i < 0) { CoinLabels.Add(label); CoinAmounts.Add(v); }
                else CoinAmounts[i] += v;
            }
        }

        private static readonly List<Rep> _reps = new List<Rep>();
        private static int _nextId;

        // liczniki sesji (linia raportu)
        private static int _sShown, _sMerged, _sSkipped, _sFallback, _sLost, _stumbles;
        private static readonly Dictionary<string, int> _skipWhy = new Dictionary<string, int>(StringComparer.Ordinal);

        internal static int Stumbles { get { return _stumbles; } }

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 8) Log.Error("SpoilsReport." + where + " (potkniecie " + _stumbles + ")", e);
        }

        internal static bool BattleOn { get { var s = Settings.Current; return s != null && s.SpoilsWindowAfterBattle; } }
        internal static bool SiegeOn { get { var s = Settings.Current; return s != null && s.SpoilsWindowAfterSiege; } }
        private static bool AnyOn { get { return BattleOn || SiegeOn; } }

        /// <summary>Pierwszy raport gotowy do pokazania (SpoilsWindow). null - nic nie czeka.</summary>
        internal static Rep NextReady()
        {
            for (int i = 0; i < _reps.Count; i++) if (_reps[i].Ready && !_reps[i].Shown && !_reps[i].Done) return _reps[i];
            return null;
        }

        internal static bool AnyReady { get { return NextReady() != null; } }

        // ------------------------------------------------------------ ramki

        internal sealed class Snap
        {
            public string Name;
            public long Gold, Men;
            public Dictionary<string, Gain> Bag;
            public Dictionary<CharacterObject, int> Pris;
            public HashSet<Hero> PrisHeroes;
            public float Renown, Influence;
            public double Day;
            public readonly List<string> Labels = new List<string>();
            public readonly List<long> Amounts = new List<long>();
            public long Crown0;                   // Ransom178.PendingClaim gracza przy otwarciu ramki (roznica = korona z tej ramki)
            public long CrownEarly;               // ramka A: korona z czesci lupu z kies zapisana juz w MapEvent.OnBattleWon (BeatenPurse.SplitPostfix) - Crown0 o nia nizej
            public long CrownSaid;                // korona wedlug CitySack (NoteSack) - tylko kontrola w linii logu
            // kontekst ramki A
            public MapEvent Me;
            public bool Won, Siege, SiegeApplied, Attacker;
            public Settlement Town;
            public int SackKind = -1;
            public string Place = "", Enemy = "", Date = "";
            public Settlement Near;               // 150b: jak Rep.Near / Foe / Sea
            public string Foe = "";
            public bool Sea;
            public int Fallen, Wounded;
            public long OpenedTicks;
        }

        private static Snap _frame;   // jedyna liczaca ramka (najbardziej zewnetrzna)
        private static int _depth;

        private static Snap TakeSnap(string name)
        {
            var s = new Snap { Name = name, OpenedTicks = System.Diagnostics.Stopwatch.GetTimestamp() };
            var me = Hero.MainHero; var mp = MobileParty.MainParty; var cl = Clan.PlayerClan;
            s.Gold = CurGold();   // noc P11: kiesa gracza (w probie autotestu turnieju - kiesa probna, bez ruszania prawdziwej)
            s.Men = mp != null ? MenPurse.Get(mp) : 0;
            s.Bag = BagOf(mp);
            if (WornCounts(name)) AddWorn(mp, s.Bag);   // recenzja P5 (4): tylko ekrany Spoils (w ramce A gra i zuzycie zmieniaja stan rynsztunku)
            s.Pris = new Dictionary<CharacterObject, int>();
            s.PrisHeroes = new HashSet<Hero>();
            ReadPrison(mp, s.Pris, s.PrisHeroes);
            s.Renown = cl != null ? cl.Renown : 0f;
            s.Influence = cl != null ? cl.Influence : 0f;
            s.Crown0 = Ransom178.PendingClaim(me);
            s.Day = CampaignTime.Now.ToDays;
            return s;
        }

        /// <summary>Ramki ekranow Spoils (S, S-tabor): rynsztunek bohaterow liczy sie razem z sakwami (gracz moze zalozyc trofeum prosto z listy).</summary>
        private static bool WornCounts(string name) { return name == "S" || name == "S-tabor"; }

        /// <summary>Recenzja P5 (4): rynsztunek bohaterow druzyny gracza - te same trzy zestawy, ktore ekran ekwipunku zmienia (InventoryLogic.PartyEquipment:
        /// FirstBattleEquipment, FirstCivilianEquipment, FirstStealthEquipment). Zestaw wspolny (domyslny zestaw skrytu bez wlasnego) liczony raz.</summary>
        private static void AddWorn(MobileParty mp, Dictionary<string, Gain> d)
        {
            var mr = mp != null ? mp.MemberRoster : null;
            if (mr == null) return;
            var seen = new List<Equipment>();
            for (int i = 0; i < mr.Count; i++)
            {
                var ch = mr.GetCharacterAtIndex(i);
                if (ch == null || !ch.IsHero || ch.HeroObject == null) continue;
                AddSet(ch.FirstBattleEquipment, d, seen);
                AddSet(ch.FirstCivilianEquipment, d, seen);
                AddSet(ch.FirstStealthEquipment, d, seen);
            }
        }

        private static void AddSet(Equipment eq, Dictionary<string, Gain> d, List<Equipment> seen)
        {
            if (eq == null) return;
            for (int i = 0; i < seen.Count; i++) if (ReferenceEquals(seen[i], eq)) return;
            seen.Add(eq);
            for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
            {
                var el = eq[i];
                if (el.IsEmpty || el.Item == null) continue;
                string k = KeyOf(el);
                if (k == null) continue;
                Gain g;
                if (d.TryGetValue(k, out g)) g.N++;
                else d[k] = new Gain { E = el, N = 1 };
            }
        }

        private static string KeyOf(EquipmentElement e)
        {
            var it = e.Item;
            if (it == null) return null;
            var mod = e.ItemModifier;
            return mod != null ? it.StringId + "|" + mod.StringId : it.StringId;
        }

        private static Dictionary<string, Gain> BagOf(MobileParty mp)
        {
            var d = new Dictionary<string, Gain>(StringComparer.Ordinal);
            var r = mp != null ? mp.ItemRoster : null;
            if (r == null) return d;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.Amount <= 0) continue;
                string k = KeyOf(el.EquipmentElement);
                if (k == null) continue;
                Gain g;
                if (d.TryGetValue(k, out g)) g.N += el.Amount;
                else d[k] = new Gain { E = el.EquipmentElement, N = el.Amount };
            }
            return d;
        }

        private static void ReadPrison(MobileParty mp, Dictionary<CharacterObject, int> troops, HashSet<Hero> heroes)
        {
            var pr = mp != null ? mp.PrisonRoster : null;
            if (pr == null) return;
            for (int i = 0; i < pr.Count; i++)
            {
                var el = pr.GetElementCopyAtIndex(i);
                var ch = el.Character;
                if (ch == null || el.Number <= 0) continue;
                if (ch.IsHero) { if (ch.HeroObject != null) heroes.Add(ch.HeroObject); continue; }
                int n; troops.TryGetValue(ch, out n); troops[ch] = n + el.Number;
            }
        }

        /// <summary>Otwarcie ramki: zwraca ramke liczaca albo null (inna ramka juz otwarta - wewnetrzna nic nie liczy). Kazde Open MUSI miec Close (finalizer).</summary>
        private static Snap Open(string name)
        {
            _depth++;
            if (_depth > 1) return null;
            try { _frame = TakeSnap(name); }
            catch (Exception e) { Stumble("Open " + name, e); _frame = null; }
            return _frame;
        }

        /// <summary>Zamkniecie ramki (s - to, co zwrocilo Open). Zwraca roznice tylko dla ramki liczacej.</summary>
        private static Delta Close(Snap s)
        {
            if (_depth > 0) _depth--;
            if (s == null) return null;
            if (!ReferenceEquals(_frame, s)) return null;   // ramka zamknieta juz przez straznika
            _frame = null;
            _depth = 0;
            try { return DiffOf(s); }
            catch (Exception e) { Stumble("Close " + s.Name, e); return null; }
        }

        private sealed class Delta
        {
            public Snap S;
            public long Gold, Men;
            public readonly List<Gain> Gains = new List<Gain>();
            public int Given;
            public readonly Dictionary<CharacterObject, int> Troops = new Dictionary<CharacterObject, int>();
            public readonly List<Hero> Lords = new List<Hero>();
            public float Renown, Influence;
            public bool TimeMoved;
            public long Crown;                    // przyrost podstawy korony gracza (Ransom178.PendingClaim); czas plynal - 0 (rozliczenie o swicie mogloby wejsc w srodek)
        }

        private static Delta DiffOf(Snap s)
        {
            var now = TakeSnap(s.Name);
            var d = new Delta { S = s };
            d.Gold = now.Gold - s.Gold;
            d.Men = now.Men - s.Men;
            foreach (var kv in now.Bag)
            {
                Gain was; int n0 = s.Bag.TryGetValue(kv.Key, out was) ? was.N : 0;
                int diff = kv.Value.N - n0;
                if (diff > 0) d.Gains.Add(new Gain { E = kv.Value.E, N = diff });
                else if (diff < 0) d.Given -= diff;
            }
            foreach (var kv in s.Bag) if (!now.Bag.ContainsKey(kv.Key)) d.Given += kv.Value.N;
            foreach (var kv in now.Pris)
            {
                int n0; s.Pris.TryGetValue(kv.Key, out n0);
                if (kv.Value > n0) d.Troops[kv.Key] = kv.Value - n0;
            }
            foreach (var h in now.PrisHeroes) if (!s.PrisHeroes.Contains(h)) d.Lords.Add(h);
            d.Renown = now.Renown - s.Renown;
            d.Influence = now.Influence - s.Influence;
            d.TimeMoved = now.Day - s.Day > 0.0005;   // ok. 43 s kampanii
            d.Crown = d.TimeMoved ? 0 : Math.Max(0, now.Crown0 - s.Crown0);
            return d;
        }

        /// <summary>Etykieta czesci zlota w otwartej ramce liczacej (bez ramki - nic). amount: kwota do kiesy gracza.</summary>
        internal static void NoteCoin(string label, long amount)
        {
            try   // wolane ze srodka przelewow (CitySack.Capture) - nigdy nie rzuca, zeby nie przerwac wyplaty
            {
                var f = _frame;
                if (f == null || amount == 0 || string.IsNullOrEmpty(label)) return;
                int i = f.Labels.IndexOf(label);
                if (i < 0) { f.Labels.Add(label); f.Amounts.Add(amount); }
                else f.Amounts[i] += amount;
            }
            catch (Exception e) { Stumble("NoteCoin", e); }
        }

        /// <summary>Wplyw roznicy ramki na raport: zloto z etykietami + reszta, sakiewki ludzi, sprzet, jency, slawa. Zwraca opis do linii logu.</summary>
        private static string Apply(Rep r, Delta d, string restLabel)
        {
            var f = d.S;
            long labelled = 0;
            for (int i = 0; i < f.Labels.Count; i++) { r.AddCoin(f.Labels[i], f.Amounts[i]); labelled += f.Amounts[i]; }
            long rest = d.Gold - labelled;
            if (!d.TimeMoved && rest != 0) r.AddCoin(rest > 0 ? restLabel : LOther, rest);
            else if (d.TimeMoved) r.TimeMoved = true;
            r.Men += d.Men;
            r.Crown += d.Crown;
            int n = 0; long w = 0;
            foreach (var g in d.Gains)
            {
                string k = KeyOf(g.E);
                if (k == null) continue;
                Gain had;
                if (r.Gear.TryGetValue(k, out had)) had.N += g.N;
                else r.Gear[k] = new Gain { E = g.E, N = g.N };
                n += g.N; w += (long)Math.Max(0, g.E.ItemValue) * g.N;
            }
            r.Given += d.Given;
            int t = 0;
            foreach (var kv in d.Troops) { int c; r.Troops.TryGetValue(kv.Key, out c); r.Troops[kv.Key] = c + kv.Value; t += kv.Value; }
            foreach (var h in d.Lords) if (!r.Lords.Contains(h)) r.Lords.Add(h);
            if (t > 0 || d.Lords.Count > 0) r.RansomWorth = RansomOf(r);
            r.Renown += d.Renown; r.Influence += d.Influence;
            var sb = new StringBuilder();
            sb.Append("zloto ").Append(Sg(d.Gold)).Append(" = ");
            for (int i = 0; i < f.Labels.Count; i++) sb.Append(Short(f.Labels[i])).Append(' ').Append(Sg(f.Amounts[i])).Append(" + ");
            sb.Append(d.TimeMoved ? "reszta pominieta (czas plynal)" : "reszta " + Sg(rest));
            sb.Append(", czas stal ").Append(d.TimeMoved ? "NIE" : "TAK");
            sb.Append(", sakiewka ludzi ").Append(Sg(d.Men)).Append(", sztuk ").Append(n).Append(" za ").Append(w.ToString(Inv));
            if (d.Given > 0) sb.Append(" (oddane z sakw ").Append(d.Given).Append(')');
            sb.Append(", jency ").Append(t).Append(" (lordow ").Append(d.Lords.Count);
            if (d.Lords.Count > 0) { sb.Append(": "); for (int i = 0; i < d.Lords.Count; i++) { if (i > 0) sb.Append(", "); sb.Append(Nm(d.Lords[i])); } }
            sb.Append("), slawa ").Append(Sg((long)Math.Round(d.Renown))).Append(" wplywy ").Append(Sg((long)Math.Round(d.Influence)));
            sb.Append(", korona ").Append(d.TimeMoved ? "pominieta (czas plynal)" : Sg(d.Crown));
            if (f.CrownEarly > 0) sb.Append(" (z tego z kies przed ramka ").Append(f.CrownEarly.ToString(Inv)).Append(')');
            if (f.CrownSaid > 0) sb.Append(" (wedlug CitySack ").Append(f.CrownSaid.ToString(Inv)).Append(')');
            return sb.ToString();
        }

        private static string Short(string label)
        {
            switch (label)
            {
                case LPlunder: return "lup pokonanych";
                case LPurses: return "kiesy/skrzynie";
                case LField: return "monety z pola";
                case LSack: return "czesc grabiezy";
                case LChest: return "skrzynia zalogi";
                case LRansom: return "czesc okupu";
                // noc P11 (turniej)
                case LTPrize: return "nagroda TXP";
                case LTHonours: return "premie meczu";
                case LTBetStake: return "zaklady postawione";
                case LTBetWon: return "zaklady wygrane";
                case LTPrizeCoin: return "nagroda w monecie";
                case LTFolk: return "sakiewka mieszczan";
                case LTChampion: return "sakiewka mistrza GT";
                case LTTakings: return "utarg gospodarza GT";
                case LTRansomIn: return "wykup od pokonanych GT";
                case LTRansomOut: return "wykup gracza GT";
                default: return label;
            }
        }

        private static string Sg(long v) { return (v >= 0 ? "+" : "") + v.ToString(Inv); }

        internal static string Nm(Hero h)
        {
            try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "-"; }
        }

        internal static string Nm(Settlement s)
        {
            try { return s != null && s.Name != null ? s.Name.ToString() : "-"; } catch { return "-"; }
        }

        /// <summary>Raport, do ktorego dopisuje sie nowa bitwa/oblezenie: ostatni niepokazany bitewny albo oblezenia; inaczej nowy.</summary>
        private static Rep Target(int kind, bool create)
        {
            for (int i = _reps.Count - 1; i >= 0; i--)
            {
                var r = _reps[i];
                if (r.Shown || r.Done) continue;
                if (r.Kind == KBattle || r.Kind == KSiege) return r;
            }
            if (!create) return null;
            var n = new Rep { Id = ++_nextId, Kind = kind };
            _reps.Add(n);
            return n;
        }

        // ------------------------------------------------------------ ramka A (pole)

        private static FieldInfo _fPeMapEvent;
        private static MapEvent _earlyMe;
        private static long _earlyCrown;

        /// <summary>Recenzja P5 (2): BeatenPurse.SplitPostfix (postfiks CalculatePlunderedAndLostGoldAmounts - MapEvent.OnBattleWon, PRZED ramka A) - przyrost
        /// Ransom178.PendingClaim gracza z czesci jego lupu, ktora placa kiesy pokonanych. Ramka A tej samej bitwy dolicza go do korony (tylko odczyt).</summary>
        internal static void NoteCrownEarly(MapEvent me, long crown)
        {
            try
            {
                if (me == null || crown <= 0) return;
                if (!ReferenceEquals(_earlyMe, me)) { _earlyMe = me; _earlyCrown = 0; }
                _earlyCrown += crown;
            }
            catch (Exception e) { Stumble("NoteCrownEarly", e); }
        }

        /// <summary>Prefiks First PlayerEncounter.DoApplyMapEventResults - otwarcie ramki A (tylko odczyt).</summary>
        public static void ApplyResultsPrefix(PlayerEncounter __instance)
        {
            try
            {
                if (!AnyOn) return;
                if (_frame != null) Abandon("nowa bitwa (DoApplyMapEventResults) przy otwartej ramce " + _frame.Name);
                var me = MapEventOf(__instance);
                if (me == null || !me.IsPlayerMapEvent) return;
                var s = Open("A");
                if (s == null) return;
                s.Me = me;
                // recenzja P5 (2): czesc lupu gracza z kies pokonanych trafila do podstawy korony juz w OnBattleWon (przed ta ramka) - liczy sie do korony tej bitwy
                if (ReferenceEquals(_earlyMe, me)) { s.CrownEarly = _earlyCrown; s.Crown0 -= _earlyCrown; }
                _earlyMe = null; _earlyCrown = 0;
                s.Siege = me.IsSiegeAssault || me.IsSallyOut;
                s.Town = me.MapEventSettlement;
                s.Date = CampaignTime.Now.ToString();
                try
                {
                    var side = PartyBase.MainParty.Side;
                    s.Attacker = side == BattleSideEnum.Attacker;
                    var mine = me.GetMapEventSide(side);
                    var foe = me.GetMapEventSide(side == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker);
                    var lp = foe != null ? foe.LeaderParty : null;
                    s.Enemy = lp != null && lp.Name != null ? lp.Name.ToString() : "";
                    // 150b: wrog i morze do obrazka okna (SpoilsWindow.Picture) - Inni przed dzikimi; kultura strony = kultura frakcji lidera
                    try
                    {
                        s.Sea = me.IsNavalMapEvent;
                        if (lp != null && lp.MobileParty != null && Undead.Party(lp.MobileParty)) s.Foe = "undead";
                        else
                        {
                            string fc = lp != null && lp.MapFaction != null && lp.MapFaction.Culture != null ? lp.MapFaction.Culture.StringId : null;
                            if (fc == "freefolk" || fc == "wildlings") s.Foe = "wild";
                        }
                    }
                    catch (Exception e) { Stumble("ApplyResultsPrefix.foe", e); }
                    if (mine != null)
                        foreach (var p in mine.Parties)
                            if (p != null && p.Party == PartyBase.MainParty)
                            {
                                s.Fallen = p.DiedInBattle != null ? p.DiedInBattle.TotalManCount : 0;
                                s.Wounded = p.WoundedInBattle != null ? p.WoundedInBattle.TotalManCount : 0;
                            }
                }
                catch (Exception e) { Stumble("ApplyResultsPrefix.side", e); }
                try
                {
                    var st = me.MapEventSettlement ?? (Hero.MainHero != null ? Hero.MainHero.LastKnownClosestSettlement : null);
                    s.Place = Nm(st);
                    s.Near = st;
                }
                catch { s.Place = "-"; }
            }
            catch (Exception e) { Stumble("ApplyResultsPrefix", e); }
        }

        /// <summary>Postfiks DoApplyMapEventResults - wynik bitwy (wygrana strony gracza).</summary>
        public static void ApplyResultsPostfix(PlayerEncounter __instance)
        {
            try
            {
                var f = _frame;
                if (f == null || f.Name != "A" || f.Me == null) return;
                f.Won = f.Me.WinningSide == PartyBase.MainParty.Side && f.Me.WinningSide != BattleSideEnum.None;
            }
            catch (Exception e) { Stumble("ApplyResultsPostfix", e); }
        }

        /// <summary>Prefiks Last MapEventParty.CommitGoldChanges - etykieta lupu z kiesy pokonanych dla partii gracza (getter: zaciag ROT = 0). Tylko odczyt.</summary>
        public static void CommitGoldPrefix(MapEventParty __instance)
        {
            try
            {
                var f = _frame;
                if (f == null || f.Name != "A" || __instance == null || __instance.Party != PartyBase.MainParty) return;
                var lh = __instance.Party.LeaderHero;
                if (lh == null || lh != Hero.MainHero) return;   // bez wodza-gracza zloto idzie do kiesy partii (PartyTradeGold), nie do kiesy gracza
                long net = (long)__instance.PlunderedGold - __instance.GoldLost;
                NoteCoin(LPlunder, net);
            }
            catch (Exception e) { Stumble("CommitGoldPrefix", e); }
        }

        /// <summary>Finalizer Last PlayerEncounter.DoEnd - zamkniecie ramki A po Finish -> FinalizeBattle -> FinalizeEvent. Wyjatek gry przechodzi bez zmian.</summary>
        public static Exception DoEndFinalizer(Exception __exception)
        {
            try
            {
                var f = _frame;
                if (f != null && f.Name == "A") FinishA(f, __exception != null ? "DoEnd z wyjatkiem " + __exception.GetType().Name : "DoEnd");
            }
            catch (Exception e) { Stumble("DoEndFinalizer", e); }
            return __exception;
        }

        private static void FinishA(Snap f, string how)
        {
            var d = Close(f);
            _depth = 0;
            if (d == null) return;
            if (!f.Won)
            {
                _sLost++;
                Log.Info(Tag + "bitwa bez okna (przegrana, remis albo odwrot) | " + how + " | " + Apply(new Rep(), d, LOther) + " | " + Counters() + ".");
                return;
            }
            bool siegeKind = (f.Me != null && f.Me.IsSiegeAssault && f.Attacker) || f.SiegeApplied;   // obrona i wypad - okno bitwy (klucz Battle)
            var r = Target(siegeKind ? KSiege : KBattle, true);
            if (r.Battles > 0) _sMerged++;
            r.Battles++;
            if (siegeKind && r.Kind == KBattle) r.Kind = KSiege;
            if (string.IsNullOrEmpty(r.Place)) { r.Place = f.Place; r.Enemy = f.Enemy; r.Date = f.Date; r.Near = f.Near; r.Foe = f.Foe; r.Sea = f.Sea; }
            if (f.Town != null) r.Town = f.Town;
            if (f.SackKind >= 0) r.SackKind = f.SackKind;
            r.Fallen += f.Fallen; r.Wounded += f.Wounded;
            r.ShareWaitDone = false;   // nowa bitwa raportu - jej dzialka moze jeszcze lezec w kolejce (TryReady)
            // recenzja P5 (3): reszta "Other" - ramka A obejmuje OnPlayerBattleEnd i wszystkie nasluchy MapEventEnded (zadania, cudze mody); kiesy pokonanych maja wlasna etykiete
            string a = Apply(r, d, LOther);
            r.Lines.Append(" | A").Append(r.Battles > 1 ? "#" + r.Battles : "").Append(" (").Append(how).Append("): ").Append(a)
             .Append(", polegli ").Append(f.Fallen).Append(" ranni ").Append(f.Wounded);
            r.Ready = false;
            // na co czekac: szturm wygrany przez atakujacego gracza - ramka G tej osady; bitwa polowa z Spoils w toku - FinishCollection; reszta - gotowy
            // (dzialka gracza w kolejce BattlefieldLaw - TryReady dopisuje czekanie na sakwy, jedna regula dla kazdej drogi do gotowosci)
            if (f.Me != null && f.Me.IsSiegeAssault && !f.SiegeApplied && f.Attacker && f.Town != null)
            {
                r.WaitFor = "oblezenie";
                r.WaitUntil = CampaignTime.Now.ToDays + 0.5;
            }
            else if (SpoilsBusy())
            {
                r.WaitFor = "Spoils";
            }
            else r.WaitFor = "";
            TryReady(r, "po DoEnd");
        }

        /// <summary>Ramka bez zamkniecia (cudzy wyjatek, przegrana bez DoEnd): A - zamkniecie jak po DoEnd; inna - zamkniecie bez wplywu na raport (linia logu).</summary>
        private static void Abandon(string why)
        {
            var f = _frame;
            if (f == null) { _depth = 0; return; }
            if (f.Name == "A") { FinishA(f, why); return; }
            if (f.Name == "T") { TClose(why); }   // noc P11: turniej przerwany nowa bitwa albo nowym turniejem - raport z tego, co juz bylo
            else if (f.Name.StartsWith("S", StringComparison.Ordinal)) { SClose(why); }
            else if (f.Name.StartsWith("L", StringComparison.Ordinal)) { LClose(why); }
            else if (f.Name == "G") { GClose(why); }
            if (_frame != null) { Log.Info(Tag + "ramka " + _frame.Name + " porzucona bez wplywu na raport (" + why + ")."); _frame = null; }
            _depth = 0;
            _sOpen = _lOpen = _gOpen = false; _sFrame = _lFrame = _gFrame = null;
        }

        private static MapEvent MapEventOf(PlayerEncounter pe)
        {
            try
            {
                if (pe == null) return null;
                if (_fPeMapEvent == null) _fPeMapEvent = AccessTools.Field(typeof(PlayerEncounter), "_mapEvent");
                var me = _fPeMapEvent != null ? _fPeMapEvent.GetValue(pe) as MapEvent : null;
                return me ?? PlayerEncounter.Battle;
            }
            catch (Exception e) { Stumble("MapEventOf", e); return null; }
        }

        // ------------------------------------------------------------ ramki S i L (Spoils)

        private static Type _tLoot;
        private static PropertyInfo _pLootInstance;
        private static FieldInfo _fPhase, _fLootRoster, _fTakenCount, _fTakenValue, _fBagCount, _fBagValue;
        private static Snap _sFrame;      // ramka ekranu trofeow/taboru (zwrot Open - moze byc null przy zagniezdzeniu)
        private static bool _sOpen;
        private static bool _sBaggage;
        private static int _sMissing;
        private static Snap _lFrame;
        private static bool _lOpen;

        private static object LootInstance()
        {
            try { return _pLootInstance != null ? _pLootInstance.GetValue(null, null) : null; } catch { return null; }
        }

        /// <summary>Faza Spoils (CollectionPhase: 0 None, 1 PostBattle, 2 Collecting, 3 Complete, 4/5 tabor); -1 - Spoils nieobecny / nieczytelny.</summary>
        internal static int SpoilsPhase()
        {
            try
            {
                var inst = LootInstance();
                if (inst == null || _fPhase == null) return -1;
                return Convert.ToInt32(_fPhase.GetValue(inst), Inv);
            }
            catch { return -1; }
        }

        internal static bool SpoilsBusy() { int p = SpoilsPhase(); return p > 0; }

        public static void LootScreenOpenPrefix() { SOpen(false); }
        public static void BaggageScreenOpenPrefix() { SOpen(true); }

        private static void SOpen(bool baggage)
        {
            try
            {
                if (!AnyOn || Target(KBattle, false) == null) return;
                if (_sOpen) SClose("ekran otwarty ponownie bez zamkniecia");
                _sOpen = true; _sBaggage = baggage; _sMissing = 0;
                _sFrame = Open(baggage ? "S-tabor" : "S");
            }
            catch (Exception e) { Stumble("SOpen", e); }
        }

        public static void LootScreenClosedPostfix() { SClose(null); }
        public static void BaggageScreenClosedPostfix() { SClose(null); }

        private static void SClose(string forced)
        {
            try
            {
                if (!_sOpen) return;
                _sOpen = false;
                var s = _sFrame; _sFrame = null;
                var d = Close(s);
                if (d == null) return;
                var r = Target(KBattle, false);
                if (r == null) return;
                string a = Apply(r, d, LOther);
                int n = 0; long w = 0;
                foreach (var g in d.Gains) { n += g.N; w += (long)Math.Max(0, g.E.ItemValue) * g.N; }
                if (!_sBaggage) { r.SN += n; r.SW += w; }
                int spN = -1; long spW = -1;
                var inst = LootInstance();
                try
                {
                    if (inst != null && !_sBaggage && _fTakenCount != null) { spN = Convert.ToInt32(_fTakenCount.GetValue(inst), Inv); spW = _fTakenValue != null ? Convert.ToInt64(_fTakenValue.GetValue(inst), Inv) : -1; }
                    if (inst != null && _sBaggage && _fBagCount != null) { spN = Convert.ToInt32(_fBagCount.GetValue(inst), Inv); spW = _fBagValue != null ? Convert.ToInt64(_fBagValue.GetValue(inst), Inv) : -1; }
                }
                catch { }
                if (!_sBaggage && spN >= 0) { r.SpN = spN; r.SpW = spW; }
                r.Lines.Append(_sBaggage ? " | S tabor: " : " | S trofea: ").Append(n).Append(" szt. za ").Append(w.ToString(Inv))
                 .Append(" (Spoils razem ").Append(spN).Append('/').Append(spW).Append(_sBaggage ? "" : " - sztuki zgodne " + (r.SN == spN ? "TAK" : "NIE")).Append(')')
                 .Append(forced != null ? " [" + forced + "]" : "").Append("; ").Append(a);
            }
            catch (Exception e) { Stumble("SClose", e); }
        }

        /// <summary>Prefiks First OnCompleteLeaveConsequence (PRZED SpoilsSeal.DrillLeavePrefix): reszta ekranu trofeow = "Left on the field"; ramka L do finalizera.</summary>
        public static void LeavePrefix()
        {
            try
            {
                if (!AnyOn) return;
                var r = Target(KBattle, false);
                if (r == null) return;
                var inst = LootInstance();
                var roster = inst != null && _fLootRoster != null ? _fLootRoster.GetValue(inst) as ItemRoster : null;
                if (roster != null)
                {
                    int n = 0; long w = 0;
                    for (int i = 0; i < roster.Count; i++)
                    {
                        var el = roster.GetElementCopyAtIndex(i);
                        if (el.EquipmentElement.Item == null || el.Amount <= 0) continue;
                        n += el.Amount; w += (long)Math.Max(0, el.EquipmentElement.ItemValue) * el.Amount;
                    }
                    r.LeftN += n; r.LeftW += w;
                }
                if (_lOpen) LClose("Leave ponownie bez zamkniecia");
                _lOpen = true;
                _lFrame = Open("L");
            }
            catch (Exception e) { Stumble("LeavePrefix", e); }
        }

        public static void BaggageDonePrefix()
        {
            try
            {
                if (!AnyOn || Target(KBattle, false) == null) return;
                if (_lOpen) LClose("Done ponownie bez zamkniecia");
                _lOpen = true;
                _lFrame = Open("L-tabor");
            }
            catch (Exception e) { Stumble("BaggageDonePrefix", e); }
        }

        public static Exception LeaveFinalizer(Exception __exception)
        {
            LClose(__exception != null ? "wyjatek Spoils " + __exception.GetType().Name : null);
            return __exception;
        }

        private static void LClose(string forced)
        {
            try
            {
                if (!_lOpen) return;
                _lOpen = false;
                var s = _lFrame; _lFrame = null;
                var d = Close(s);
                var r = Target(KBattle, false);
                if (d != null && r != null)
                {
                    string a = Apply(r, d, LField);
                    r.Lines.Append(" | L").Append(s != null && s.Name == "L-tabor" ? " tabor" : "").Append(": zostawione na polu ").Append(r.LeftN).Append(" szt. za ")
                     .Append(r.LeftW.ToString(Inv)).Append(forced != null ? " [" + forced + "]" : "").Append("; ").Append(a);
                }
                if (r != null) TryReady(r, "po Leave");
            }
            catch (Exception e) { Stumble("LClose", e); }
        }

        /// <summary>Postfiks FinishCollection (jedyny koniec przeplywu Spoils, takze "Leave the battlefield" bez zbierania): raport gotowy, gdy zadna ramka nie jest otwarta.</summary>
        public static void FinishCollectionPostfix()
        {
            try
            {
                var r = Target(KBattle, false);
                if (r == null) return;
                if (r.WaitFor != "Spoils" && r.SN == 0 && r.LeftN == 0) return;   // raport bez przeplywu Spoils (oblezenie, kryjowka) - nie jego koniec
                if (r.WaitFor == "Spoils") r.WaitFor = "";
                r.Lines.Append(" | Spoils FinishCollection");
                TryReady(r, "po FinishCollection");
            }
            catch (Exception e) { Stumble("FinishCollectionPostfix", e); }
        }

        // ------------------------------------------------------------ ramka F (sakwy BattlefieldLaw)

        internal sealed class FToken { internal Snap S; internal bool Counted; }

        /// <summary>BattlefieldLaw.FlushShareToBaggage / FlushMountsToBaggage: otwarcie ramki F (null - klucze wylaczone). Para z CloseF w finally.</summary>
        internal static FToken OpenF()
        {
            try
            {
                if (!AnyOn) return null;
                return new FToken { S = Open("F"), Counted = true };
            }
            catch (Exception e) { Stumble("OpenF", e); return null; }
        }

        internal static void CloseF(FToken t, string why)
        {
            if (t == null || !t.Counted) return;
            try
            {
                var d = Close(t.S);
                if (d == null) return;
                var r = Target(KBattle, false);
                int n = 0; long w = 0;
                foreach (var g in d.Gains) { n += g.N; w += (long)Math.Max(0, g.E.ItemValue) * g.N; }
                if (r == null)
                {
                    if (n > 0) Log.Info(Tag + "sakwy bez raportu (" + why + "): " + n + " szt. za " + w.ToString(Inv) + " - okno juz pokazane albo bitwa bez okna; sztuki sa w sakwach.");
                    return;
                }
                string a = Apply(r, d, LOther);
                r.Lines.Append(" | F sakwy (").Append(why).Append("): ").Append(n).Append(" szt. za ").Append(w.ToString(Inv)).Append("; ").Append(a);
                if (r.WaitFor == "sakwy") { r.WaitFor = ""; r.ShareWaitDone = true; r.Lines.Append(" | sakwy przyszly (koniec czekania)"); }   // recenzja P5 (1)
                TryReady(r, "po sakwach");
            }
            catch (Exception e) { Stumble("CloseF", e); }
        }

        // ------------------------------------------------------------ ramka G (nastepstwa oblezenia)

        private static Snap _gFrame;
        private static bool _gOpen, _gNested;
        private static Settlement _gTown;
        private static int _gKind;

        /// <summary>Prefiks First CampaignEventDispatcher.OnSiegeAftermathApplied - ramka G (wszystkie nasluchy nastepstw), tylko z udzialem gracza.</summary>
        public static void AftermathPrefix(MobileParty attackerParty, Settlement settlement, SiegeAftermathAction.SiegeAftermath aftermathType, Dictionary<MobileParty, float> partyContributions)
        {
            try
            {
                if (!AnyOn) return;
                var main = MobileParty.MainParty;
                if (main == null || settlement == null) return;
                bool mine = attackerParty == main || (partyContributions != null && partyContributions.ContainsKey(main))
                            || (attackerParty != null && attackerParty.Army != null && main.Army != null && attackerParty.Army == main.Army);
                if (!mine) return;
                if (_gOpen) GClose("nastepstwa ponownie bez zamkniecia");
                _gOpen = true; _gTown = settlement; _gKind = CitySack.KindOf(aftermathType);
                _gNested = _frame != null;
                if (_gNested) { _frame.SiegeApplied = true; _frame.SackKind = _gKind; if (_frame.Town == null) _frame.Town = settlement; }
                _gFrame = Open("G");
            }
            catch (Exception e) { Stumble("AftermathPrefix", e); }
        }

        public static Exception AftermathFinalizer(Exception __exception)
        {
            GClose(__exception != null ? "wyjatek " + __exception.GetType().Name : null);
            return __exception;
        }

        private static void GClose(string forced)
        {
            try
            {
                if (!_gOpen) return;
                _gOpen = false;
                var s = _gFrame; _gFrame = null;
                var d = Close(s);
                if (_gNested) return;   // w ramce A (oblezenie AI w FinalizeEvent) - liczy A
                if (d == null) return;
                Rep r = null;
                for (int i = _reps.Count - 1; i >= 0; i--)
                {
                    var x = _reps[i];
                    if (!x.Shown && !x.Done && x.WaitFor == "oblezenie" && x.Town == _gTown) { r = x; break; }
                }
                if (r == null)
                {
                    r = new Rep { Id = ++_nextId, Kind = KSiege, Town = _gTown, Place = Nm(_gTown), Date = CampaignTime.Now.ToString() };
                    _reps.Add(r);
                }
                r.Kind = KSiege;
                r.SackKind = _gKind;
                r.WaitFor = "";
                string a = Apply(r, d, LOther);
                r.Lines.Append(" | G ").Append(Nm(_gTown)).Append(' ').Append(_gKind == CitySack.KMercy ? "laska" : _gKind == CitySack.KPillage ? "grabiez" : "spustoszenie")
                 .Append(forced != null ? " [" + forced + "]" : "").Append(": ").Append(a);
                TryReady(r, "po nastepstwach oblezenia");
            }
            catch (Exception e) { Stumble("GClose", e); }
        }

        /// <summary>CitySack.Messages (tylko odczyt, gdy gracz bierze udzial): czesc gracza w lupie miasta jako etykieta; korona - kontrola w logu (liczy ja ramka). Bez otwartej ramki - nic.</summary>
        internal static void NoteSack(long playerLead, long playerCrown)
        {
            try
            {
                if (_frame == null) return;
                NoteCoin(LSack, playerLead);
                // recenzja P5 (2): korone liczy roznica Ransom178.PendingClaim w ramce (czesc gracza idzie tam przez Ransom178.NoteThird - CitySack.Coin);
                // liczba CitySack zostaje tylko kontrola w linii logu (bez drugiego doliczenia)
                if (playerCrown > 0) _frame.CrownSaid += playerCrown;
            }
            catch (Exception e) { Stumble("NoteSack", e); }
        }

        /// <summary>SiegeRansom (O1 gracz w obozie, O3 gracz-oblegajacy): okup wziety - osobny raport z liczb SiegeRansom (ToLead do kiesy, ToMen do sakiewek, korona).</summary>
        internal static void NoteRansom(Settlement st, long pool, long men, long lead, long crown, bool besieger)
        {
            try
            {
                if (!SiegeOn || (men <= 0 && lead <= 0)) return;
                var r = new Rep { Id = ++_nextId, Kind = KRansom, Town = st, Place = Nm(st), Date = CampaignTime.Now.ToString(), Battles = 0 };
                r.AddCoin(LRansom, lead);
                r.Men = men;
                r.Crown = crown;
                r.Lines.Append(" | R okup ").Append(Nm(st)).Append(besieger ? " (gracz oblegal)" : " (gracz w obozie)").Append(": pula ").Append(pool.ToString(Inv))
                 .Append(", do kiesy ").Append(lead.ToString(Inv)).Append(", sakiewki ludzi ").Append(men.ToString(Inv)).Append(", korona ").Append(crown.ToString(Inv));
                _reps.Add(r);
                TryReady(r, "okup");
            }
            catch (Exception e) { Stumble("NoteRansom", e); }
        }

        // ------------------------------------------------------------ gotowosc i straznik

        /// <summary>Recenzja P5 (1): termin czekania na sakwy - ArmouryBehavior._lootFlushDue = koniec bitwy + 1 h, sprawdzany w HourlyTick (CampaignTime.Now &gt; termin),
        /// wiec wysypka przychodzi najpozniej 2 h kampanii po bitwie; 0.5 h zapasu.</summary>
        private const double ShareWaitHours = 2.5;

        private static bool ShareQueued() { try { return BattlefieldLaw.SharePending(); } catch { return false; } }

        private static void TryReady(Rep r, string how)
        {
            if (r == null || r.Ready || r.Done) return;
            if (_frame != null || _depth > 0) return;
            if (r.WaitFor == "Spoils" && !SpoilsBusy()) r.WaitFor = "";
            // recenzja P5 (1): raport bitwy nie jest gotowy, dopoki dzialka gracza lezy w kolejce BattlefieldLaw (bitwa bez ekranu Spoils: zaciezny ROT -
            // SoldierStandsDown, Spoils wyszedl z OnMapEventEnded wczesniej) - inaczej okno pokazaloby sie przed sakwami (Show zatrzymuje czas), a ramka F
            // po oknie trafilaby w "sakwy bez raportu"; zwalnia CloseF albo straznik (pusta kolejka, ShareWaitHours)
            if (r.WaitFor == "" && !r.ShareWaitDone && r.Battles > 0 && r.Kind != KRansom && ShareQueued())
            {
                r.WaitFor = "sakwy";
                r.WaitUntil = CampaignTime.Now.ToDays + ShareWaitHours / 24.0;
                r.Lines.Append(" | czeka na sakwy (dzialka w kolejce BattlefieldLaw, ").Append(how).Append(')');
                return;
            }
            if (r.WaitFor != "") return;
            r.Ready = true;
            r.ReadyDay = CampaignTime.Now.ToDays;
            r.ReadyTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            r.Lines.Append(" | gotowy ").Append(how);
        }

        /// <summary>SpoilsWindow.AppTick (4 razy na sekunde, w kampanii): ramka bez zamkniecia (przegrana, niewola, cudzy wyjatek), czekanie na Spoils/oblezenie, raporty przeterminowane.</summary>
        internal static void Watch()
        {
            try
            {
                if (_reps.Count == 0 && _frame == null && _depth == 0) return;
                var c = Campaign.Current;
                if (c == null) return;
                var mp = MobileParty.MainParty;
                bool battle = mp != null && mp.MapEvent != null;
                bool encounter = PlayerEncounter.Current != null;
                if (_frame != null && _frame.Name == "T") TWatch();   // noc P11: ramka turnieju - zamkniecie po wyjsciu z szranek
                else if (_frame != null && _frame.Name == "A" && !battle && !encounter)
                    Abandon("straznik - ramka bez DoEnd (bez spotkania i bitwy)");
                else if (_frame != null && _frame.Name.StartsWith("S", StringComparison.Ordinal))
                {
                    // dwa kolejne sprawdzenia (0,5 s) bez ekranu ekwipunku na stosie - ekran zamkniety bez wywolania Spoils (cudzy wyjatek)
                    if (InventoryOnStack()) _sMissing = 0;
                    else if (++_sMissing >= 2) { _sMissing = 0; SClose("straznik - ekranu Spoils juz nie ma"); }
                }
                else if (_frame == null && _depth > 0 && !_sOpen && !_lOpen && !_gOpen) _depth = 0;   // licznik bez ramki (wyjatek miedzy Open i Close)
                double day = CampaignTime.Now.ToDays;
                for (int i = _reps.Count - 1; i >= 0; i--)
                {
                    var r = _reps[i];
                    if (r.Done) { _reps.RemoveAt(i); continue; }
                    if (r.Shown) continue;
                    if (!r.Ready)
                    {
                        if (r.WaitFor == "Spoils" && !SpoilsBusy()) { r.WaitFor = ""; r.Lines.Append(" | Spoils bez FinishCollection (straznik)"); }
                        if (r.WaitFor == "oblezenie" && (day > r.WaitUntil || (!encounter && mp != null && mp.CurrentSettlement == null)))
                        {
                            r.WaitFor = "";
                            r.Lines.Append(" | bez nastepstw oblezenia (").Append(day > r.WaitUntil ? "12 h kampanii" : "gracz poza osada").Append(')');
                        }
                        if (r.WaitFor == "sakwy")
                        {
                            // recenzja P5 (1): kolejka oprozniona bez ramki F (przegrana kolejna bitwa - DropShare) albo termin minal - okno bez sakw
                            bool pend = ShareQueued();
                            if (!pend || day > r.WaitUntil)
                            {
                                r.WaitFor = ""; r.ShareWaitDone = true;
                                r.Lines.Append(pend ? " | sakwy nie przyszly w " + ShareWaitHours.ToString("0.0", Inv) + " h kampanii (straznik - okno bez nich)"
                                                    : " | kolejka lupow pusta bez ramki F (straznik)");
                            }
                        }
                        TryReady(r, "straznik");
                    }
                    else if (day - r.ReadyDay > 3.0) Finish(r, "pominiete", "przeterminowany (3 dni kampanii bez spokojnej mapy)", null);
                }
            }
            catch (Exception e) { Stumble("Watch", e); }
        }

        private static bool InventoryOnStack()
        {
            try
            {
                var gsm = TaleWorlds.Core.GameStateManager.Current;
                if (gsm == null) return false;
                foreach (var g in gsm.GameStates) if (g is TaleWorlds.CampaignSystem.GameState.InventoryState) return true;
            }
            catch { }
            return false;
        }

        // ------------------------------------------------------------ wycena

        /// <summary>Okup jencow raportu: zolnierz - RansomValueCalculationModel.PrisonerRansomValue(ch, gracz) (te same wywolania co sprzedaz u brokera; latka FairRansom RC),
        /// lord - Ransom178.PriceOf przy wlaczonym okupie wedlug majatku, inaczej model. Wartosc, nie zloto w kiesie ("about").</summary>
        private static long RansomOf(Rep r)
        {
            long sum = 0;
            try
            {
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.RansomValueCalculationModel : null;
                var me = Hero.MainHero;
                if (model == null) return 0;
                foreach (var kv in r.Troops) sum += (long)kv.Value * Math.Max(0, model.PrisonerRansomValue(kv.Key, me));
                foreach (var h in r.Lords)
                {
                    long v = 0;
                    try
                    {
                        if (Ransom178.On) { bool bc; double D; v = Ransom178.PriceOf(h, out bc, out D); }
                        if (v <= 0 && h.CharacterObject != null) v = model.PrisonerRansomValue(h.CharacterObject, me);
                    }
                    catch (Exception e) { Stumble("RansomOf.lord", e); }
                    sum += Math.Max(0, v);
                }
            }
            catch (Exception e) { Stumble("RansomOf", e); }
            return sum;
        }

        internal static long Worth(Rep r) { return Math.Max(0, r.CoinTotal) + Math.Max(0, r.Men) + r.GearWorth + r.RansomWorth; }

        // ------------------------------------------------------------ koniec raportu (SpoilsWindow)

        /// <summary>Koniec raportu: pokazane / pominiete (powod) / zapas. Jedna linia logu z ramkami i licznikami sesji.</summary>
        internal static void Finish(Rep r, string outcome, string why, string window)
        {
            if (r == null || r.Done) return;
            r.Done = true;
            try
            {
                if (outcome == "pokazane") _sShown++;
                else if (outcome == "pominiete")
                {
                    _sSkipped++;
                    string k = why ?? "-";
                    int p = k.IndexOf(" (", StringComparison.Ordinal);
                    if (p > 0) k = k.Substring(0, p);
                    int n; _skipWhy.TryGetValue(k, out n); _skipWhy[k] = n + 1;
                }
                if (window != null && window.StartsWith("zapas", StringComparison.Ordinal)) _sFallback++;
                var sb = new StringBuilder(512);
                sb.Append(Tag).Append(KindName(r.Kind)).Append(' ').Append(r.Id).Append(" (bitew ").Append(r.Battles).Append(") ").Append(Nm(r.Town) != "-" ? Nm(r.Town) : r.Place)
                  .Append(r.Lines.ToString())
                  .Append(" | razem: zloto ").Append(r.CoinTotal.ToString(Inv)).Append(", sakiewki ludzi ").Append(r.Men.ToString(Inv)).Append(", sprzet ").Append(r.GearCount)
                  .Append(" szt. za ").Append(r.GearWorth.ToString(Inv)).Append(", jency ").Append(r.TroopCount).Append(" + lordow ").Append(r.Lords.Count)
                  .Append(" okup ~").Append(r.RansomWorth.ToString(Inv)).Append(", korona ").Append(r.Crown.ToString(Inv))
                  .Append(r.TimeMoved ? ", czas plynal TAK" : "")
                  .Append(" | ").Append(outcome).Append(why != null ? " (" + why + ")" : "")
                  .Append(window != null ? " | okno: " + window : "")
                  .Append(" | ").Append(Counters()).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Finish", e); }
            if (r.Test) TestVerdict(r, outcome, why, window);   // noc P11: proba autotestu okna turnieju - linia wyniku
        }

        internal static string KindName(int k)
        {
            return k == KSiege ? "oblezenie" : k == KRansom ? "okup" : k == KTourney ? "turniej" : "bitwa";
        }

        private static string Counters()
        {
            var sb = new StringBuilder();
            sb.Append("sesja: pokazane ").Append(_sShown).Append(", polaczone ").Append(_sMerged).Append(", pominiete ").Append(_sSkipped);
            if (_skipWhy.Count > 0)
            {
                sb.Append(" (");
                bool first = true;
                foreach (var kv in _skipWhy) { if (!first) sb.Append(", "); first = false; sb.Append(kv.Key).Append(' ').Append(kv.Value); }
                sb.Append(')');
            }
            sb.Append(", bez okna (przegrana) ").Append(_sLost).Append(", zapas ").Append(_sFallback).Append(", potkniecia ").Append(_stumbles + SpoilsWindow.Stumbles);
            return sb.ToString();
        }

        // ------------------------------------------------------------ kontrakt P0

        internal static void Reset()
        {
            _reps.Clear();
            _frame = null; _depth = 0;
            _sOpen = _lOpen = _gOpen = _gNested = false; _sFrame = _lFrame = _gFrame = null; _gTown = null;
            _earlyMe = null; _earlyCrown = 0;
            TReset();   // noc P11: ramka turnieju, zakresy kiesy, proba autotestu (proba - raz na sesje gry, nie na kampanie)
        }

        // turniej (ramka T) - noc P11 (125): SpoilsReport.Tourney.cs (OnTourneyJoined / OnTourneyFinished / OnTourneyEliminated)

        // ------------------------------------------------------------ latki (BattlefieldLaw.ApplyAll, start gry)

        private static int _wired, _wanted;
        private static readonly List<string> _wiredNames = new List<string>();
        private static readonly List<string> _missing = new List<string>();

        private static void Wire(Harmony h, Type t, string name, string prefix, string postfix, string finalizer, int prio, string label)
        {
            _wanted++;
            try
            {
                if (t == null) { _missing.Add(label + " (brak typu)"); return; }
                var m = AccessTools.DeclaredMethod(t, name);
                if (m == null) { _missing.Add(label + " (brak metody)"); return; }
                HarmonyMethod pre = prefix != null ? new HarmonyMethod(typeof(SpoilsReport), prefix) { priority = prio } : null;
                HarmonyMethod post = postfix != null ? new HarmonyMethod(typeof(SpoilsReport), postfix) { priority = prio } : null;
                HarmonyMethod fin = finalizer != null ? new HarmonyMethod(typeof(SpoilsReport), finalizer) { priority = Priority.Last } : null;
                h.Patch(m, prefix: pre, postfix: post, finalizer: fin);
                // pulapka 15.10: finalizer BKROTPatch polyka bledy Harmony - liczymy tylko to, co GetPatchInfo naprawde pokazuje na celu
                bool okPre = prefix == null || Has(m, h.Id, prefix, 'p');
                bool okPost = postfix == null || Has(m, h.Id, postfix, 'o');
                bool okFin = finalizer == null || Has(m, h.Id, finalizer, 'f');
                if (okPre && okPost && okFin)
                {
                    _wired++;
                    _wiredNames.Add(label);
                    return;
                }
                // czesciowo wpieta para (np. prefiks bez finalizera) zostawilaby otwarta ramke - zdejmij to, co weszlo
                foreach (var n in new[] { prefix, postfix, finalizer })
                {
                    if (n == null) continue;
                    var pm = AccessTools.DeclaredMethod(typeof(SpoilsReport), n);
                    if (pm == null) continue;
                    try { h.Unpatch(m, pm); } catch { }
                }
                _missing.Add(label + " (latka nie weszla" + (okPre ? "" : " - prefiks") + (okPost ? "" : " - postfiks") + (okFin ? "" : " - finalizer")
                             + " - slad w BKROTPatch/BKROTLog.txt)");
            }
            catch (Exception e) { _missing.Add(label + " (wyjatek " + e.GetType().Name + ")"); Log.Error("SpoilsReport.Wire " + label, e); }
        }

        /// <summary>Harmony.GetPatchInfo: nasza latka danego rodzaju (p/o/f) naprawde na celu.</summary>
        private static bool Has(MethodBase target, string owner, string patchName, char kind)
        {
            var pi = Harmony.GetPatchInfo(target);
            if (pi == null) return false;
            IEnumerable<Patch> list = kind == 'p' ? pi.Prefixes : kind == 'o' ? pi.Postfixes : pi.Finalizers;
            if (list == null) return false;
            foreach (var p in list)
                if (p != null && p.owner == owner && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(SpoilsReport) && p.PatchMethod.Name == patchName) return true;
            return false;
        }

        /// <summary>Latki ramek (tylko odczyt) - z BattlefieldLaw.ApplyAll (start gry, obok LootTally); kazda osobno, brak = BRAK w linii startu.</summary>
        internal static void Apply(Harmony h)
        {
            try
            {
                _harmony = h;   // noc P11: latki GrandTourney (zakresy kiesy turnieju) wpinane dopiero w kampanii - EnsureGtWired
                var pe = typeof(PlayerEncounter);
                Wire(h, pe, "DoApplyMapEventResults", nameof(ApplyResultsPrefix), nameof(ApplyResultsPostfix), null, Priority.First, "DoApplyMapEventResults");
                Wire(h, typeof(MapEventParty), "CommitGoldChanges", nameof(CommitGoldPrefix), null, null, Priority.Last, "CommitGoldChanges");
                Wire(h, pe, "DoEnd", null, null, nameof(DoEndFinalizer), Priority.Last, "DoEnd");
                Wire(h, typeof(CampaignEventDispatcher), "OnSiegeAftermathApplied", nameof(AftermathPrefix), null, nameof(AftermathFinalizer), Priority.First, "OnSiegeAftermathApplied");
                int game = _wired;
                _tLoot = Type.GetType("RealisticLoot.Behaviors.LootCollectionBehavior, RealisticLoot");
                int sp0 = _wired;
                if (_tLoot != null)
                {
                    _pLootInstance = AccessTools.Property(_tLoot, "Instance");
                    _fPhase = AccessTools.Field(_tLoot, "_phase");
                    _fLootRoster = AccessTools.Field(_tLoot, "_lootScreenRoster");
                    _fTakenCount = AccessTools.Field(_tLoot, "_takenCount");
                    _fTakenValue = AccessTools.Field(_tLoot, "_takenValue");
                    _fBagCount = AccessTools.Field(_tLoot, "_baggageTakenCount");
                    _fBagValue = AccessTools.Field(_tLoot, "_baggageTakenValue");
                    Wire(h, _tLoot, "OpenLootSelectionScreen", nameof(LootScreenOpenPrefix), null, null, Priority.First, "OpenLootSelectionScreen");
                    Wire(h, _tLoot, "OnLootScreenClosed", null, nameof(LootScreenClosedPostfix), null, Priority.Last, "OnLootScreenClosed");
                    Wire(h, _tLoot, "OpenBaggageLootScreen", nameof(BaggageScreenOpenPrefix), null, null, Priority.First, "OpenBaggageLootScreen");
                    Wire(h, _tLoot, "OnBaggageLootScreenClosed", null, nameof(BaggageScreenClosedPostfix), null, Priority.Last, "OnBaggageLootScreenClosed");
                    Wire(h, _tLoot, "OnCompleteLeaveConsequence", nameof(LeavePrefix), null, nameof(LeaveFinalizer), Priority.First, "OnCompleteLeaveConsequence");
                    Wire(h, _tLoot, "OnBaggageDoneConsequence", nameof(BaggageDonePrefix), null, nameof(LeaveFinalizer), Priority.First, "OnBaggageDoneConsequence");
                    Wire(h, _tLoot, "FinishCollection", null, nameof(FinishCollectionPostfix), null, Priority.Last, "FinishCollection");
                }
                int sp = _wired - sp0;
                string fields = _tLoot == null ? "Spoils BRAK" : ("pola Spoils " + ((_pLootInstance != null ? 1 : 0) + (_fPhase != null ? 1 : 0) + (_fLootRoster != null ? 1 : 0)
                                + (_fTakenCount != null ? 1 : 0) + (_fTakenValue != null ? 1 : 0) + (_fBagCount != null ? 1 : 0) + (_fBagValue != null ? 1 : 0)) + "/7");
                string foreign = Foreign(h, pe, "DoEnd") + "; DoApplyMapEventResults: " + Foreign(h, pe, "DoApplyMapEventResults");
                var s = Settings.Current;
                Log.Info(Tag + "start | klucze Battle " + YN(s.SpoilsWindowAfterBattle) + ", Siege " + YN(s.SpoilsWindowAfterSiege) + ", Tourney " + YN(s.SpoilsWindowAfterTourney)
                         + " (turniej - ramka T, latki GrandTourney w kampanii: linia 'Okno lupu (125)'), MinWorth " + s.SpoilsWindowMinWorth + " | latki " + _wired + "/" + _wanted + ": gra " + game + "/4, Spoils " + sp + "/7 ("
                         + string.Join(", ", _wiredNames.ToArray()) + ")" + (_missing.Count > 0 ? " | BRAK: " + string.Join(", ", _missing.ToArray()) : "") + " | " + fields
                         + " | prefab ArmSpoilsWindow " + (SpoilsWindow.PrefabFileExists() ? "TAK" : "BRAK (okno przez zapas StoryWindow/ShowInquiry)")
                         + " | GT Busy " + (SpoilsWindow.GtKnown() ? "TAK" : "BRAK") + " | obce latki DoEnd: " + foreign + ".");
            }
            catch (Exception e) { Log.Error("SpoilsReport.Apply", e); }
        }

        private static string YN(bool b) { return b ? "TAK" : "NIE"; }

        private static string Foreign(Harmony h, Type t, string name)
        {
            try
            {
                var m = AccessTools.DeclaredMethod(t, name);
                var info = m != null ? Harmony.GetPatchInfo(m) : null;
                if (info == null) return "-";
                var owners = new List<string>();
                foreach (var o in info.Owners) if (o != h.Id && !owners.Contains(o)) owners.Add(o);
                return owners.Count == 0 ? "brak" : string.Join(", ", owners.ToArray());
            }
            catch { return "?"; }
        }
    }
}
