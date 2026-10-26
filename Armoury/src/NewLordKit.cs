using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 159 - "POZYCZKA + MNIEJSZE WOJSKO" (Jeff 26.10 ok. 00:10: "dopadlem w ostatniej bitwie znowu armie bez wyposazenia czemu przeciwnik biega z armia bez
    /// sprzetu?!"; decyzja "Pozyczka + mniejsze": lord najpierw kupuje, co moze - z pozyczka Banku Zelaznego do limitu - i nie bierze ludzi, ktorych nie ubierze;
    /// regula 12.10: bez sztucznych doplat, brak pieniedzy = pozyczka albo mniejsze wojsko, nic z niczego). Sledztwo (nadzy-raport): gra odtwarza pobitego albo
    /// wykupionego lorda nowa druzyna z szablonu klanu (HeroSpawnCampaignBehavior.SpawnLordParty -> LordPartyComponent.InitializeLordPartyProperties ->
    /// InitializeMobilePartyAroundPosition), E4/8 bierze tych ludzi z wolnych rak wsi bez dobytku, LevyGold.PartyCreatedPrefix blokuje darmowy komplet DTE,
    /// a sklad7b (TroopsFightWithOwnKitOnly) puszcza ich w bitwie gracza w tunice z kijem; p9 tylko wstrzymuje ich atak, dozbrajanie jest wolniejsze niz
    /// naplyw nagich druzyn (20/23/10 na dobe, ok. 25% partii lordow ponizej 50% gotowosci).
    ///
    /// (1) NOWA DRUZYNA (klucz NewLordPartyArmedMenOnly; ArmsFromPeople.SpawnPost - droga E4/8, przed rachunkiem szablonu i poborem z DOM): Plan -
    ///     miasto = najblizsze wazne miasto wyjazdu p9 (ArmsFirst.NearestTown: przyjazne, bez oblezenia, droga ladowa, AiArmsTripMaxDays, ta sama strona Muru);
    ///     wycena kompletow ludzi szablonu na JEGO polce (AiGear.QuoteUnits - filtry i kolejnosc BuyLoop, bez zakupu); fundusz = zloto wodza ponad AiGearGoldReserve,
    ///     najwyzej przydzial sprzetu rodu 166 (GearCapAny) plus nadwyzka kies rodziny ponad rezerwe wojny R (NeedOf), plus miejsce pozyczki Banku (LoanRoom - zasady
    ///     drabiny 168: DebtLadder.CanBorrow, IronBank.Limit - dlug, wolny kapital ponad BankFreeCapitalFloor). Ludzie wybierani po jednym (najwyzsze pokrycie
    ///     kompletu na tej polce, potem najtanszy): bierze czlowieka tylko, gdy dostanie co najmniej AiArmsReadyPercent slotow wzorca i jego komplet (x zapas
    ///     Margin) miesci sie w funduszu. Reszta szablonu zostaje we wsi (zdjeta przed poborem - z ksiegi ludzi nic nie schodzi). Brak miasta w zasiegu - nikt.
    ///     Sztuki NIE przenosza sie: druzyna jedzie do tego miasta (ArmsFirst.TripTown - miasto planu; CanAfford przez AiGear.BudgetPreview widzi fundusz planu)
    ///     i kupuje z polki ta sama droga AiGear (VisitBudget w TryBuyCore): przy pierwszej wizycie w miescie budzet = zloto ponad rezerwe (jak wyzej) + sakiewka
    ///     ludzi + pozyczka na brak wobec wyceny TEJ polki (IronBank.Lend - prawdziwa pozyczka z oplata i oprocentowaniem, splata drabiny 168; zloto Banku ->
    ///     glowa rodu -> wodz partii przelewem GiveGoldAction), limit sztuk wizyty = sztuki wyceny. Placi wodz, zloto do kasy miasta (AiGear) - nic z niczego.
    ///     Plan wazny 2 x AiArmsTripMaxDays + 2 doby; bez wizyty - wygasa (bez pozyczki). Plan i zegary w zapisie (arm_159_kit).
    /// (2) LUDZIE BEZ SPRZETU DO DOMU (klucz UnarmedMenSentHome): niegotowa partia lorda AI (ArmedStrength Low), ktora nie moze sie dozbroic - ArmsFirst.ThinkPostfix
    ///     zapisuje (NoteStuck) od kiedy i czemu nie ma wyjazdu (nie stac / brak miasta / nie stac w miescie / 2 wizyty bez postepu) - po UnarmedMenSentHomeDays
    ///     dobach (albo zwolniona z czekania p9) odsyla ludzi bez zbroi albo bez broni: zostaje ceil(min(pokrycie korpusu, pokrycie broni) x ludzi) (miara
    ///     GarrisonKit.FillPercent per typ, wszyscy ludzie), reszta proporcjonalnie do skladu do domu droga zwolnionych 166 (PeopleBook.Take, ClanBudget.ToVillagePop
    ///     z przyczyna M166 - ludnosc BK wsi rodu, znani pochodzeniem do DOM swojego regionu, weterani E4/10). Wies bez danych BK - nikt nie odchodzi (licznik).
    ///     Sakiewki ludzi i zbrojownia zostaja w partii (jak przepelnienie W4c). Bez: armii, bitwy, oblezenia, morza, rozwiazywanej partii, rodu gracza, Strazy, umarlych.
    /// (3) p9 bez zmian. P0: linia "Bitwa gracza (159 P0)" (kazda bitwa gracza - stan kazdej partii lorda AI) i linia doby "Nowe druzyny i sprzet (159)" (plany,
    ///     pozyczki, zakupy, odeslani, spis partii bez wpisu DTE i z R = 0 wedlug wieku partii). Potkniecia liczone, bez wylacznikow.
    /// </summary>
    internal static class NewLordKit
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double Margin = 1.10;      // zapas wyceny: zakup ShelfBuy wycenia kazda sztuke od nowa (cena rosnie z ubytkiem polki), wycena - cena pierwszej sztuki
        private const int RepeatDays = 14;       // ponowne odeslanie tej samej partii w tylu dobach - licznik "w kolko" (werbunek -> odeslanie)
        private static readonly string[] WhyCodes = { ArmsFirst.WhyPoor, ArmsFirst.WhyNoTown, ArmsFirst.WhyNoBudgetTown, ArmsFirst.WhyNoGain, "?" };

        private sealed class KitPlan
        {
            internal MobileParty P; internal Settlement Town; internal string TownId;
            internal int Men, Template, Pieces, Day; internal long Cost, Own, Loan; internal bool Done;
        }
        private sealed class Stuck { internal double SinceH; internal int Why; }

        private static readonly Dictionary<MobileParty, KitPlan> _plans = new Dictionary<MobileParty, KitPlan>();
        private static readonly Dictionary<string, KitPlan> _savedPlans = new Dictionary<string, KitPlan>(StringComparer.Ordinal);
        private static readonly Dictionary<MobileParty, Stuck> _stuck = new Dictionary<MobileParty, Stuck>();
        private static readonly Dictionary<string, Stuck> _savedStuck = new Dictionary<string, Stuck>(StringComparer.Ordinal);
        private static readonly Dictionary<MobileParty, int> _born = new Dictionary<MobileParty, int>();
        private static readonly Dictionary<string, int> _savedBorn = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<MobileParty, int> _lastHome = new Dictionary<MobileParty, int>();
        private static WeakReference _lastBattle;
        private static int _importN = -1, _importBad;

        // liczniki doby (linia "Nowe druzyny i sprzet (159)")
        private static int _dPlanParties, _dOff, _dStart, _dLate, _dNoGear, _dNoTown, _dZero, _dStumbles, _stumbles;
        private static long _dTemplate, _dTaken, _dNoTownMen, _dPlanPieces, _dPlanCost, _dPlanOwn, _dPlanLoan;
        private static int _dVisits, _dVisitEmpty, _dLoans, _dExpired, _dDestroyed;
        private static long _dLoanSum, _dVisitSpent, _dVisitPieces, _dLoanLeft, _dVisitOwn;
        private static readonly Dictionary<string, int> _dLoanNo = new Dictionary<string, int>(StringComparer.Ordinal);
        private static int _dHomeParties, _dHomeNothing, _dHomeRepeat, _dHomeSkipBusy, _dHomeSkipPlayer, _dHomeSkipOther;
        private static long _dHomeMen, _dHomeWounded, _dHomeNoVillage;
        private static readonly List<string> _dEx = new List<string>();
        private static readonly HashSet<string> _err = new HashSet<string>();

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_err.Add(where)) Log.Error("NewLordKit." + where, e); } catch { }
        }

        private static readonly int[] _dExN = new int[3];   // przyklady na rodzaj: 0 plan, 1 wizyta, 2 do domu (po 2)
        private const int ExPlan = 0, ExVisit = 1, ExHome = 2;
        private static void Ex(int kind, string s) { if (kind >= 0 && kind < _dExN.Length && _dExN[kind] < 2) { _dExN[kind]++; _dEx.Add(s); } }

        internal static void Reset()
        {
            _plans.Clear(); _savedPlans.Clear(); _stuck.Clear(); _savedStuck.Clear(); _born.Clear(); _savedBorn.Clear(); _lastHome.Clear();
            _lastBattle = null; _importN = -1; _importBad = 0; _stumbles = 0; _err.Clear();
            ClearDay();
        }

        private static void ClearDay()
        {
            _dPlanParties = _dOff = _dStart = _dLate = _dNoGear = _dNoTown = _dZero = _dStumbles = 0;
            _dTemplate = _dTaken = _dNoTownMen = _dPlanPieces = _dPlanCost = _dPlanOwn = _dPlanLoan = 0;
            _dVisits = _dVisitEmpty = _dLoans = _dExpired = _dDestroyed = 0;
            _dLoanSum = _dVisitSpent = _dVisitPieces = _dLoanLeft = _dVisitOwn = 0;
            _dLoanNo.Clear();
            _dHomeParties = _dHomeNothing = _dHomeRepeat = _dHomeSkipBusy = _dHomeSkipPlayer = _dHomeSkipOther = 0;
            _dHomeMen = _dHomeWounded = _dHomeNoVillage = 0;
            _dEx.Clear(); Array.Clear(_dExN, 0, _dExN.Length);
            AiGear.QuoteStumbles = 0;
        }

        private static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
        internal static int HomeDays(Settings s) { return Clamp(s.UnarmedMenSentHomeDays, 1, 30); }
        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }
        private static string Nm(MobileParty mp) { try { return mp != null && mp.Name != null ? mp.Name.ToString() : "?"; } catch { return "?"; } }
        private static string Nm(Settlement st) { try { return st != null && st.Name != null ? st.Name.ToString() : "-"; } catch { return "?"; } }

        // ------------------------------------------------------------ zdarzenia
        internal static void RegisterEvents(CampaignBehaviorBase owner)
        {
            CampaignEvents.MobilePartyCreated.AddNonSerializedListener(owner, OnCreated);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(owner, OnDestroyed);
            CampaignEvents.MapEventStarted.AddNonSerializedListener(owner, OnMapEventStarted);
        }

        private static void OnCreated(MobileParty mp)
        {
            try { if (mp != null && mp.IsLordParty && !mp.IsMainParty) _born[mp] = Today; } catch (Exception e) { Stumble("OnCreated", e); }
        }

        private static void OnDestroyed(MobileParty mp, PartyBase pb)
        {
            try
            {
                if (mp == null) return;
                KitPlan p;
                if (_plans.TryGetValue(mp, out p)) { if (!p.Done) _dDestroyed++; _plans.Remove(mp); }
                _stuck.Remove(mp); _born.Remove(mp); _lastHome.Remove(mp);
            }
            catch (Exception e) { Stumble("OnDestroyed", e); }
        }

        private static void OnMapEventStarted(MapEvent me, PartyBase a, PartyBase d)
        {
            try { if (me != null && (a == PartyBase.MainParty || d == PartyBase.MainParty)) BattleLine(me, "start spotkania"); } catch (Exception e) { Stumble("OnMapEventStarted", e); }
        }

        // ------------------------------------------------------------ fundusz i pozyczka
        /// <summary>Zloto wodza na sprzet nowych ludzi: ponad AiGearGoldReserve, najwyzej przydzial sprzetu rodu 166 (takze przed pierwszym budzetem sesji - GearCapAny)
        /// plus nadwyzka kies rodziny ponad rezerwe wojny R (NeedOf). Rod bez budzetu - cale zloto ponad rezerwe. Tylko odczyt.</summary>
        private static int OwnFund(Hero lord, Settings s)
        {
            if (lord == null || !lord.IsAlive) return 0;
            int above = Math.Max(0, lord.Gold - Math.Max(0, s.AiGearGoldReserve));
            var c = lord.Clan;
            int gear;
            if (c == null || !ClanBudget.GearCapAny(c, out gear)) return above;
            double cap = Math.Max(0, gear), G, R, D; int streak;
            if (ClanBudget.NeedOf(c, out G, out R, out D, out streak) && G > R) cap += G - R;
            return (int)Math.Min(above, Math.Min(int.MaxValue, cap));
        }

        /// <summary>Miejsce pozyczki Banku na sprzet - zasady drabiny 168 (DebtLadder.CanBorrow: bez zajecia, zaleglosci, karencji, bankructwa, korony w zaleglosci, wolny
        /// kapital Banku), IronBank.Limit (przy drabinie DebtLadder.Limit) minus dlug, najwyzej wolny kapital ponad BankFreeCapitalFloor. Tylko rody krolestw (jak
        /// Bank przed drabina), bez gracza i mniejszych frakcji. why - powod 0 (do linii). Tylko odczyt.</summary>
        private static int LoanRoom(Clan c, out string why)
        {
            why = null;
            var s = Settings.Current;
            if (s == null || !s.NewLordPartyKitLoan) { why = "pozyczka wylaczona (MCM)"; return 0; }
            if (!IronBank.On || !DebtLadder.On) { why = "Bank albo drabina dlugu wylaczone"; return 0; }
            if (c == null || c == Clan.PlayerClan || c.Leader == null || c.Kingdom == null || c.IsMinorFaction || c.IsEliminated) { why = "rod poza krolestwem (Bank pozycza rodom krolestw)"; return 0; }
            string w;
            if (!DebtLadder.CanBorrow(c, out w))
            {
                var d0 = IronBank.DebtOf(c, false);
                if (DebtLadder.IsSeized(c)) why = "rod w zajeciu";
                else if (DebtLadder.StageOf(c) == "zaleglosc") why = "zaleglosc rat";
                else if (d0 != null && d0.Defaulted) why = "bankrut Banku";
                else if (CrownBorrow.CrownInArrears(c)) why = "korona w zaleglosci w Banku";
                else if (IronBank.FreeCapital < Math.Max(0, s.BankFreeCapitalFloor)) why = "Bank ponizej progu wolnego kapitalu";
                else why = "karencja po zajeciu";
                return 0;
            }
            var d = IronBank.DebtOf(c, false);
            double room = IronBank.Limit(c) - (d != null ? d.Principal : 0);
            double free = IronBank.FreeCapital - Math.Max(0, s.BankFreeCapitalFloor);
            if (room < 1) { why = "na limicie Banku"; return 0; }
            if (free < 1) { why = "Bank ponizej progu wolnego kapitalu"; return 0; }
            return (int)Math.Max(0, Math.Min(int.MaxValue, Math.Min(room, free)));
        }

        /// <summary>Pozyczka na brak: kapital Banku -> kiesa glowy rodu (IronBank.Lend - oplata, oprocentowanie, splata drabiny 168), potem glowa -> wodz partii
        /// (GiveGoldAction, gdy wodz nie jest glowa). Zwraca zloto, ktore dotarlo do wodza.</summary>
        private static int Borrow(Clan c, Hero lord, int amount, out string why)
        {
            int room = LoanRoom(c, out why);
            int ask = Math.Min(amount, room);
            if (ask <= 0) return 0;
            int got = IronBank.Lend(c, ask, "sprzet nowej druzyny (159)");
            if (got <= 0) { why = "Bank nie wyplacil (limit albo kapital)"; return 0; }
            if (lord == null || lord == c.Leader) return got;
            int before = lord.Gold;
            try { GiveGoldAction.ApplyBetweenCharacters(c.Leader, lord, got, true); } catch (Exception e) { Stumble("Borrow(przelew)", e); }
            return Math.Max(0, lord.Gold - before);
        }

        private static void NoLoan(string why) { if (string.IsNullOrEmpty(why)) return; int v; _dLoanNo.TryGetValue(why, out v); _dLoanNo[why] = v + 1; }

        // ------------------------------------------------------------ (1) plan przy tworzeniu (ArmsFromPeople.SpawnPost, E4/8)
        private sealed class Kind { internal CharacterObject Ch; internal int Count, Taken; internal List<int> Buckets; }

        /// <summary>
        /// Wolane z ArmsFromPeople.SpawnPost (droga E4/8, przed rachunkiem szablonu): ile ludzi kazdego typu z dopisanych przez gre wziac. null - bez zmian (klucz
        /// wylaczony, start gry, okno nowej gry ColdStart, zakupy AI wylaczone, rod gracza, umarli). Pusty slownik - nikt (brak miasta w zasiegu albo nic nie da
        /// sie ubrac). Partia nie ma jeszcze Id (CampaignObjectManager.AddMobileParty po komponencie) - plan po obiekcie partii.
        /// </summary>
        internal static Dictionary<CharacterObject, int> Plan(MobileParty mp, Hero owner, Dictionary<CharacterObject, int> added)
        {
            var s = Settings.Current;
            if (s == null || added == null || mp == null || owner == null) return null;
            if (!s.NewLordPartyArmedMenOnly) { _dOff++; return null; }
            if (Campaign.Current == null || !Campaign.Current.GameStarted) { _dStart++; return null; }   // nowa gra - swiat dostaje komplet DTE (LevyGold)
            if (mp.IsMainParty || owner.Clan == Clan.PlayerClan || Undead.Party(mp)) return null;
            if (ColdStart.LateWindowFor(mp)) { _dLate++; return null; }   // okno nowej gry: dorobek stuleci (ColdStart.LateLord)
            if (!AiGear.On) { _dNoGear++; return null; }                   // nikt nie kupuje - jak dotad
            var keep = new Dictionary<CharacterObject, int>();
            int template = 0;
            foreach (var v in added.Values) template += Math.Max(0, v);
            if (template <= 0) return null;
            _dPlanParties++; _dTemplate += template;
            var lord = mp.LeaderHero ?? owner;
            bool failed;
            var town = ArmsFirst.NearestTown(mp, out failed);
            if (failed) { _dPlanParties--; _dTemplate -= template; _dStumbles++; _stumbles++; return null; }   // wyjatek przy szukaniu miasta - sklad bez zmian (nie "brak miasta")
            if (town == null)
            {
                _dNoTown++; _dNoTownMen += template;
                Ex(ExPlan, Nm(mp) + ": szablon " + template + " -> 0 (brak miasta w zasiegu " + ArmsFirst.TripDays(s) + " dob)");
                return keep;
            }
            bool oneUp = s.MenUpgradeOneTierUp;
            var kinds = new List<Kind>();
            var need = new Dictionary<int, int>();
            var lifters = new Dictionary<int, List<CharacterObject>>();
            foreach (var kv in added)
            {
                var ch = kv.Key;
                if (ch == null || kv.Value <= 0) continue;
                if (ch.IsHero || PeopleBook.IsWight(ch)) { keep[ch] = kv.Value; continue; }   // bez kompletu z polki - jak dotad
                var b = AiGear.KitBuckets(ch, oneUp);
                kinds.Add(new Kind { Ch = ch, Count = kv.Value, Buckets = b });
                foreach (var k in b)
                {
                    int n; need.TryGetValue(k, out n); need[k] = n + kv.Value;
                    List<CharacterObject> l;
                    if (!lifters.TryGetValue(k, out l)) lifters[k] = l = new List<CharacterObject>();
                    if (!l.Contains(ch)) l.Add(ch);
                }
            }
            var units = AiGear.QuoteUnits(town, mp, need, lifters);
            if (units == null) { _dPlanParties--; _dTemplate -= template; _dStumbles++; _stumbles++; return null; }   // wycena nieudana - sklad bez zmian
            int own = OwnFund(lord, s);
            string why;
            int room = LoanRoom(lord.Clan, out why);
            double fund = (double)own + room;
            double T = ArmedStrength.Ready(s) / 100.0;
            var pos = new Dictionary<int, int>();
            var local = new Dictionary<int, int>();
            long spent = 0; int pieces = 0, taken = 0;
            while (true)
            {
                int best = -1; double bf = -1; long bc = long.MaxValue;
                for (int i = 0; i < kinds.Count; i++)
                {
                    var kd = kinds[i];
                    if (kd.Taken >= kd.Count) continue;
                    int filled; long cost;
                    Eval(kd.Buckets, units, pos, local, false, out filled, out cost);
                    double fill = kd.Buckets.Count > 0 ? filled / (double)kd.Buckets.Count : 1.0;
                    if (fill + 1e-9 < T || (spent + cost) * Margin > fund + 1e-6) continue;
                    if (fill > bf + 1e-9 || (Math.Abs(fill - bf) <= 1e-9 && cost < bc)) { best = i; bf = fill; bc = cost; }
                }
                if (best < 0) break;
                int f2; long c2;
                Eval(kinds[best].Buckets, units, pos, local, true, out f2, out c2);
                kinds[best].Taken++; taken++; spent += c2; pieces += f2;
            }
            foreach (var kd in kinds) keep[kd.Ch] = kd.Taken;
            _dTaken += taken;
            long plannedLoan = Math.Max(0, Math.Min(room, (long)Math.Ceiling(spent * Margin) - own));
            if (taken > 0 && pieces > 0)
            {
                _plans[mp] = new KitPlan { P = mp, Town = town, TownId = town.StringId, Men = taken, Template = template, Pieces = pieces, Day = Today, Cost = spent, Own = own, Loan = plannedLoan };
                _dPlanPieces += pieces; _dPlanCost += spent; _dPlanOwn += Math.Min(own, (long)Math.Ceiling(spent * Margin)); _dPlanLoan += plannedLoan;
            }
            if (taken <= 0) _dZero++;
            if (room <= 0) NoLoan(why);
            Ex(ExPlan, Nm(mp) + ": szablon " + template + " -> " + taken + " (miasto " + Nm(town) + ", wycena " + pieces + " szt. za " + spent + " zl, wlasne " + own
               + ", miejsce pozyczki " + room + (room <= 0 && why != null ? " - " + why : "") + ")");
            return keep;
        }

        /// <summary>Pokrycie kompletu jednego czlowieka na polce: ile jego koszykow ma jeszcze sztuke w wycenie (pos - zuzyte przez wzietych) i ich koszt;
        /// take - zuzyj. local - pamiec robocza (powtorzony koszyk u jednego czlowieka).</summary>
        private static void Eval(List<int> buckets, Dictionary<int, List<int>> units, Dictionary<int, int> pos, Dictionary<int, int> local, bool take, out int filled, out long cost)
        {
            filled = 0; cost = 0;
            local.Clear();
            foreach (var b in buckets)
            {
                List<int> u;
                if (!units.TryGetValue(b, out u)) continue;
                int p0; pos.TryGetValue(b, out p0);
                int l0; local.TryGetValue(b, out l0);
                int at = p0 + l0;
                if (at >= u.Count) continue;
                filled++; cost += u[at];
                local[b] = l0 + 1;
            }
            if (!take) return;
            foreach (var kv in local) { int p0; pos.TryGetValue(kv.Key, out p0); pos[kv.Key] = p0 + kv.Value; }
        }

        // ------------------------------------------------------------ (1) wizyta w miescie (AiGear.TryBuyCore, BudgetPreview, ArmsFirst.TripTown)
        private static KitPlan Get(MobileParty mp)
        {
            if (mp == null) return null;
            KitPlan p;
            if (_plans.TryGetValue(mp, out p)) return p;
            if (_savedPlans.Count == 0) return null;
            string id = mp.StringId;
            if (id == null || !_savedPlans.TryGetValue(id, out p)) return null;
            _savedPlans.Remove(id);
            p.P = mp;
            if (p.Town == null && p.TownId != null) { try { p.Town = Settlement.Find(p.TownId); } catch { } }
            _plans[mp] = p;
            return p;
        }

        /// <summary>Nowa druzyna z planem jeszcze bez wizyty w miescie (klucz wlaczony).</summary>
        internal static bool Active(MobileParty mp)
        {
            try { var s = Settings.Current; if (s == null || !s.NewLordPartyArmedMenOnly) return false; var p = Get(mp); return p != null && !p.Done; }
            catch (Exception e) { Stumble("Active", e); return false; }
        }

        /// <summary>ArmsFirst.TripTown: miasto, w ktorym wyceniono komplet (null - bez planu).</summary>
        internal static Settlement PlanTown(MobileParty mp) { return Active(mp) ? Get(mp).Town : null; }

        /// <summary>ArmsFirst.CanAfford: prog "stac go" dla nowej druzyny z planem - wycena planu x Margin (0 - bez planu).</summary>
        internal static int PlanNeed(MobileParty mp)
        {
            try { if (!Active(mp)) return 0; var p = Get(mp); return (int)Math.Min(int.MaxValue, Math.Max(1L, (long)Math.Ceiling(p.Cost * Margin))); }
            catch (Exception e) { Stumble("PlanNeed", e); return 0; }
        }

        /// <summary>AiGear.BudgetPreview (ArmsFirst.CanAfford): zloto ponad rezerwe i miejsce pozyczki - bez brania pozyczki.</summary>
        internal static int PreviewFund(MobileParty mp, Hero lord, Settings s)
        {
            try { string why; return (int)Math.Min(int.MaxValue, (long)OwnFund(lord, s) + LoanRoom(lord != null ? lord.Clan : null, out why)); }
            catch (Exception e) { Stumble("PreviewFund", e); return 0; }
        }

        /// <summary>
        /// AiGear.TryBuyCore (lord AI w miescie, przed petla zakupow): budzet wizyty z planem - braki zbrojowni wycenione na tej polce (QuoteUnits) x Margin;
        /// zloto ponad rezerwe (OwnFund) i sakiewka ludzi, brak - pozyczka Banku (Borrow). -1 - bez planu, nie miasto albo polka bez sztuk (zwykly zakup; plan
        /// zostaje do nastepnego miasta albo wygasniecia). pieces - limit sztuk wizyty (sztuki wyceny).
        /// </summary>
        internal static int VisitBudget(MobileParty mp, Settlement st, Hero lord, Settings s, int budget0, int purse, out int pieces)
        {
            pieces = 0;
            try
            {
                var p = Get(mp);
                if (p == null || p.Done || st == null || !st.IsTown || lord == null) return -1;
                var all = AiGear.Armories();
                Dictionary<ItemObject, int> arm = null;
                if (all != null) all.TryGetValue(mp.Id, out arm);
                var lifters = new Dictionary<int, List<CharacterObject>>();
                var need = AiGear.Deficit(mp, arm, null, lifters);
                var units = AiGear.QuoteUnits(st, mp, need, lifters);
                if (units == null) return -1;   // wycena nieudana - zwykly zakup, plan czeka
                long est = 0;
                foreach (var l in units.Values) { pieces += l.Count; foreach (var u in l) est += u; }
                if (pieces <= 0) { _dVisitEmpty++; return -1; }
                p.Done = true;
                _dVisits++;
                int own = OwnFund(lord, s);
                long want = (long)Math.Ceiling(est * Margin);
                long shortfall = want - own - Math.Max(0, purse);
                int got = 0; string why = null;
                if (shortfall > 0) got = Borrow(lord.Clan, lord, (int)Math.Min(int.MaxValue, shortfall), out why);
                if (got > 0) { _dLoans++; _dLoanSum += got; }
                else if (shortfall > 0) NoLoan(why);
                p.Loan = got; p.Own = own; p.Cost = est;
                _dVisitOwn += Math.Min(own, want);
                return (int)Math.Min(int.MaxValue, Math.Min(want, (long)own + Math.Max(0, purse) + got));
            }
            catch (Exception e) { Stumble("VisitBudget", e); return -1; }
        }

        /// <summary>AiGear.TryBuyCore po zakupach wizyty z planem: liczniki (wydane, sztuki, reszta pozyczki w kiesie wodza) i koniec planu.</summary>
        internal static void VisitDone(MobileParty mp, Settlement st, int spent, int pieces, int budget)
        {
            try
            {
                KitPlan p;
                if (mp == null || !_plans.TryGetValue(mp, out p)) return;
                _dVisitSpent += Math.Max(0, spent); _dVisitPieces += Math.Max(0, pieces);
                long left = Math.Max(0L, Math.Min(p.Loan, (long)budget - spent));
                _dLoanLeft += left;
                Ex(ExVisit, Nm(mp) + " w " + Nm(st) + ": kupione " + pieces + " szt. za " + spent + " zl (wycena " + p.Cost + ", wlasne " + p.Own + ", pozyczka " + p.Loan
                   + (left > 0 ? ", z pozyczki zostalo w kiesie " + left : "") + "; ludzi " + p.Men + " z szablonu " + p.Template + ")");
                _plans.Remove(mp);
            }
            catch (Exception e) { Stumble("VisitDone", e); }
        }

        // ------------------------------------------------------------ (2) niegotowa partia, ktora nie moze sie dozbroic
        /// <summary>ArmsFirst.ThinkPostfix (watek glowny, co godzine dla niegotowych): why != null - mysl nie dala wyjazdu (od kiedy - pierwsza taka mysl, takze
        /// z zapisu); null - jest wyjazd (koniec stania).</summary>
        internal static void NoteStuck(MobileParty mp, string why)
        {
            try
            {
                if (mp == null) return;
                if (why == null) { _stuck.Remove(mp); return; }
                int code = Array.IndexOf(WhyCodes, why); if (code < 0) code = WhyCodes.Length - 1;
                Stuck st;
                if (_stuck.TryGetValue(mp, out st)) { st.Why = code; return; }
                st = new Stuck { SinceH = CampaignTime.Now.ToHours, Why = code };
                Stuck sv;
                if (_savedStuck.Count > 0 && mp.StringId != null && _savedStuck.TryGetValue(mp.StringId, out sv)) { _savedStuck.Remove(mp.StringId); st.SinceH = Math.Min(st.SinceH, sv.SinceH); }
                _stuck[mp] = st;
            }
            catch (Exception e) { Stumble("NoteStuck", e); }
        }

        private static bool IsWeapon(ItemObject.ItemTypeEnum t)
        {
            return t == ItemObject.ItemTypeEnum.OneHandedWeapon || t == ItemObject.ItemTypeEnum.TwoHandedWeapon || t == ItemObject.ItemTypeEnum.Polearm
                   || t == ItemObject.ItemTypeEnum.Bow || t == ItemObject.ItemTypeEnum.Crossbow || t == ItemObject.ItemTypeEnum.Thrown;
        }

        /// <summary>Odsyla do domu ludzi bez zbroi albo bez broni (opis klasy, (2)). Zwraca odeslanych.</summary>
        private static int SendHome(MobileParty mp, string why, double days)
        {
            var all = AiGear.Armories();
            Dictionary<ItemObject, int> arm = null;
            if (all != null) all.TryGetValue(mp.Id, out arm);
            var per = new List<int[]>();
            int needS, usedS;
            int r0 = GarrisonKit.FillPercent(mp, arm, null, false, out needS, out usedS, per);
            long bm = 0, bu = 0, wm = 0, wu = 0;
            foreach (var x in per)
            {
                var t = (ItemObject.ItemTypeEnum)x[0];
                if (t == ItemObject.ItemTypeEnum.BodyArmor) { bm += x[1]; bu += x[2]; }
                else if (IsWeapon(t)) { wm += x[1]; wu += x[2]; }
            }
            double cb = bm > 0 ? bu / (double)bm : 1.0, cw = wm > 0 ? wu / (double)wm : 1.0;
            double f = Math.Max(0.0, Math.Min(1.0, Math.Min(cb, cw)));
            var r = mp.MemberRoster;
            var reg = new Dictionary<CharacterObject, int>();
            int men = 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var ch = el.Character;
                if (ch == null || ch.IsHero || el.Number <= 0 || PeopleBook.IsWight(ch) || Undead.Character(ch)) continue;
                int v; reg.TryGetValue(ch, out v); reg[ch] = v + el.Number; men += el.Number;
            }
            int keepN = (int)Math.Ceiling(f * men - 1e-9);
            int go = men - keepN;
            if (go <= 0) { _dHomeNothing++; return 0; }
            var village = ClanBudget.OverflowVillage(mp);
            if (village == null) { _dHomeNoVillage += go; return 0; }   // bez wsi z danymi BK nikt nie odchodzi (nikt nie znika - Z6)
            int gone = 0, wounded = 0;
            foreach (var kv in ArmsFromPeople.Split(reg, go))
            {
                var ch = kv.Key;
                int idx = r.FindIndexOfTroop(ch);
                if (idx < 0 || kv.Value <= 0) continue;
                int num = r.GetElementNumber(idx), wnd = r.GetElementWoundedNumber(idx);
                int n = Math.Min(kv.Value, num);
                if (n <= 0) continue;
                int w = wnd > 0 ? Math.Min(wnd, (int)Math.Round(n * (double)wnd / num)) : 0;
                // E4/7: udzialy pochodzenia tuz PRZED dopisaniem do wsi, roster po (jak ClanBudget.ReleaseFrom; wies nie przyjela - Untake, ludzie zostaja)
                PeopleBook.Shares org = null;
                try { if (PeopleBook.OriginLive) org = PeopleBook.Take(r, n, PeopleBook.FRelease166); } catch (Exception e) { Stumble("SendHome.Take", e); org = null; }
                bool ok = false;
                try { ok = ClanBudget.ToVillagePop(village, ch, n, PeopleBook.M166, org, mp); } catch (Exception e) { Stumble("SendHome.ToVillagePop", e); ok = false; }
                if (!ok)
                {
                    try { if (org != null) PeopleBook.Untake(r, org, PeopleBook.FRelease166); } catch (Exception e) { Stumble("SendHome.Untake", e); }
                    _dHomeNoVillage += n;
                    continue;
                }
                r.AddToCounts(ch, -n, false, -w);
                gone += n; wounded += w;
            }
            if (gone <= 0) return 0;
            _dHomeParties++; _dHomeMen += gone; _dHomeWounded += wounded;
            int last;
            if (_lastHome.TryGetValue(mp, out last) && Today - last <= RepeatDays) _dHomeRepeat++;
            _lastHome[mp] = Today;
            try { ArmedStrength.MarkDirty(mp); } catch { }
            Ex(ExHome, Nm(mp) + ": do domu " + gone + " z " + men + " (" + why + " od " + days.ToString("0.0", Inv) + " dob; zbroja " + (int)Math.Round(cb * 100) + "%, bron "
               + (int)Math.Round(cw * 100) + "%, R " + r0 + "%; wies " + Nm(village) + ")");
            return gone;
        }

        // ------------------------------------------------------------ doba
        private static int AgeBucket(MobileParty mp, int today)
        {
            int d;
            if (_born.TryGetValue(mp, out d) || (mp.StringId != null && _savedBorn.TryGetValue(mp.StringId, out d)))
            {
                int a = today - d;
                return a < 1 ? 0 : a < 3 ? 1 : a < 7 ? 2 : 3;
            }
            return 4;
        }

        /// <summary>Globalna doba (ArmouryBehavior, po ArmedStrength.Daily): pamiec z zapisu do partii, wygasle plany, stanie, regula (2), spis P0 i linia doby.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) { ClearDay(); return; }
            int today = Today;
            double now = CampaignTime.Now.ToHours;
            int lords = 0, noEntry = 0, r0 = 0, stuckNow = 0, stuckShort = 0;
            var byAge = new int[5, 3];
            var whyN = new int[WhyCodes.Length];
            var worst = new List<KeyValuePair<MobileParty, int>>();   // P0 doby: niegotowe poza armia (R) - 3 najgorsze opisane jak w bitwie gracza
            try
            {
                var all = AiGear.Armories();
                // (a) doba powstania z zapisu i wygasle plany
                var lordList = new List<MobileParty>();
                foreach (var mp in MobileParty.AllLordParties)
                {
                    if (mp == null || !mp.IsActive || mp.IsMainParty) continue;
                    lordList.Add(mp);
                    int d;
                    if (_savedBorn.Count > 0 && mp.StringId != null && _savedBorn.TryGetValue(mp.StringId, out d) && !_born.ContainsKey(mp)) _born[mp] = d;
                    Get(mp);   // plan z zapisu do partii (gdy byl)
                    Stuck sv;
                    if (_savedStuck.Count > 0 && mp.StringId != null && _savedStuck.TryGetValue(mp.StringId, out sv) && !_stuck.ContainsKey(mp)) _stuck[mp] = sv;   // zegar z zapisu
                }
                _savedBorn.Clear(); _savedPlans.Clear(); _savedStuck.Clear();   // reszta z zapisu: partii juz nie ma
                int maxAge = 2 * ArmsFirst.TripDays(s) + 2;
                List<MobileParty> rm = null;
                foreach (var kv in _plans)
                {
                    var p = kv.Value; var mp = kv.Key;
                    if (mp == null || !mp.IsActive) { if (rm == null) rm = new List<MobileParty>(); rm.Add(mp); _dDestroyed++; continue; }
                    if (p.Done || today - p.Day > maxAge) { if (rm == null) rm = new List<MobileParty>(); rm.Add(mp); if (!p.Done) _dExpired++; }
                }
                if (rm != null) foreach (var mp in rm) _plans.Remove(mp);
                // (b) stanie: tylko partie dalej niegotowe, poza armia i czynne
                List<MobileParty> rs = null;
                foreach (var kv in _stuck)
                {
                    var mp = kv.Key;
                    var info = mp != null && mp.IsActive ? ArmedStrength.InfoOf(mp) : null;
                    // po wczytaniu p9 liczy partie kolejka (najwyzej 400 na godzine) - zegar partii jeszcze nie policzonej zostaje
                    bool gone = mp == null || !mp.IsActive || mp.Army != null || (ArmedStrength.Computed(mp) && (info == null || !info.Low));
                    if (gone) { if (rs == null) rs = new List<MobileParty>(); rs.Add(mp); }
                }
                if (rs != null) foreach (var mp in rs) _stuck.Remove(mp);
                // (c) spis P0 i regula (2)
                bool home = s.UnarmedMenSentHome && GarrisonKit.OwnKitOn;
                double wait = HomeDays(s) * 24.0;
                foreach (var mp in lordList)
                {
                    try
                    {
                        if (!mp.IsActive || !GarrisonKit.LordRule(mp)) continue;
                        lords++;
                        int ab = AgeBucket(mp, today);
                        bool hasEntry = all != null && all.ContainsKey(mp.Id);
                        var info = ArmedStrength.InfoOf(mp);
                        bool zero = !hasEntry || (info != null && info.R == 0);
                        byAge[ab, 0]++;
                        if (!hasEntry) { noEntry++; byAge[ab, 1]++; }
                        if (zero) { r0++; byAge[ab, 2]++; }
                        if (info == null || !info.Low) continue;
                        if (mp.Army == null && mp.ActualClan != Clan.PlayerClan) worst.Add(new KeyValuePair<MobileParty, int>(mp, info.R));
                        Stuck st;
                        bool isStuck = _stuck.TryGetValue(mp, out st);
                        if (isStuck) { stuckNow++; whyN[Math.Max(0, Math.Min(WhyCodes.Length - 1, st.Why))]++; }
                        bool due = isStuck && now - st.SinceH >= wait;
                        if (!due && info.Free && !isStuck)
                        {
                            // zwolniona z czekania p9 (AiArmsWaitMaxDays bez dozbrojenia) - do domu tylko, gdy dalej jej nie stac (ArmsFirst.CanAfford, jak powrot do dozbrajania)
                            int fb, fn;
                            due = !ArmsFirst.CanAfford(mp, info.Short, null, out fb, out fn);
                        }
                        if (!due) { if (isStuck) stuckShort++; continue; }
                        if (!home) continue;
                        if (mp.ActualClan == Clan.PlayerClan) { _dHomeSkipPlayer++; continue; }
                        if (CrownGifts.IsWatchParty(mp) || Undead.Party(mp)) { _dHomeSkipOther++; continue; }
                        if (mp.Army != null || mp.MapEvent != null || mp.SiegeEvent != null || mp.BesiegedSettlement != null || mp.IsCurrentlyAtSea || mp.IsDisbanding
                            || mp.LeaderHero == null || (mp.CurrentSettlement != null && mp.CurrentSettlement.IsUnderSiege))
                        { _dHomeSkipBusy++; continue; }
                        string why = info.Free ? "zwolniona z czekania p9" : WhyCodes[Math.Max(0, Math.Min(WhyCodes.Length - 1, st.Why))];
                        double days = isStuck ? (now - st.SinceH) / 24.0 : ArmedStrength.WaitDaysOf(mp);
                        int gone = SendHome(mp, why, days);
                        if (gone > 0 || isStuck) _stuck.Remove(mp);   // od nowa: po odeslaniu (albo gdy nie bylo kogo) zegar liczy sie znowu przy nastepnym braku
                    }
                    catch (Exception e) { Stumble("Daily(partia)", e); }
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            string p0 = "";
            try
            {
                worst.Sort((a, b) => a.Value != b.Value ? a.Value.CompareTo(b.Value) : b.Key.MemberRoster.TotalManCount.CompareTo(a.Key.MemberRoster.TotalManCount));
                var ds = new List<string>();
                for (int i = 0; i < worst.Count && i < 3; i++) { var mp = worst[i].Key; if (mp != null && mp.IsActive) ds.Add(Describe(mp, "doba")); }
                p0 = ds.Count > 0 ? string.Join(" || ", ds.ToArray()) : "brak";
            }
            catch (Exception e) { Stumble("Daily(P0)", e); }
            try { Line(s, today, lords, noEntry, r0, byAge, stuckNow, stuckShort, whyN, p0); } catch (Exception e) { Stumble("Line", e); }
            ClearDay();
        }

        private static string Age3(int[,] a, int i) { return a[i, 0] + "/" + a[i, 1] + "/" + a[i, 2]; }

        private static void Line(Settings s, int today, int lords, int noEntry, int r0, int[,] byAge, int stuckNow, int stuckShort, int[] whyN, string p0)
        {
            var sb = new StringBuilder(1800);
            sb.Append("Nowe druzyny i sprzet (159): dzien ").Append(today)
              .Append(" | klucze: New Lord Party Armed Men Only ").Append(s.NewLordPartyArmedMenOnly ? "TAK" : "NIE")
              .Append(", New Lord Party Kit Loan ").Append(s.NewLordPartyKitLoan ? "TAK" : "NIE")
              .Append(", Unarmed Men Sent Home ").Append(s.UnarmedMenSentHome ? "TAK po " + HomeDays(s) + " dobach" : "NIE")
              .Append(GarrisonKit.OwnKitOn ? "" : " (TroopsFightWithOwnKitOnly NIE - regula 2 nieczynna)");
            sb.Append(" | (1) NOWE DRUZYNY z planem: ").Append(_dPlanParties).Append(" partii, szablon ").Append(_dTemplate).Append(" ludzi -> wzieci ").Append(_dTaken)
              .Append(", zostali we wsi ").Append(_dTemplate - _dTaken).Append(_dTemplate > 0 ? " (" + (100.0 * (_dTemplate - _dTaken) / _dTemplate).ToString("0", Inv) + "%)" : "")
              .Append(", bez miasta w zasiegu ").Append(_dNoTown).Append(" partii (").Append(_dNoTownMen).Append(" ludzi), bez nikogo ").Append(_dZero)
              .Append("; wycena planow ").Append(_dPlanPieces).Append(" szt. za ").Append(_dPlanCost).Append(" zl (x").Append(Margin.ToString("0.00", Inv))
              .Append(": wlasne ").Append(_dPlanOwn).Append(", pozyczka planowana ").Append(_dPlanLoan).Append(")")
              .Append("; bez planu: klucz wylaczony ").Append(_dOff).Append(", start gry ").Append(_dStart).Append(", okno nowej gry (ColdStart) ").Append(_dLate)
              .Append(", zakupy AI wylaczone ").Append(_dNoGear)
              .Append(" | WIZYTY z planem: ").Append(_dVisits).Append(" (polka bez sztuk - zwykly zakup, plan czeka ").Append(_dVisitEmpty).Append(")")
              .Append(", kupione ").Append(_dVisitPieces).Append(" szt. za ").Append(_dVisitSpent).Append(" zl (wodz -> kasy miast), z tego wlasne do ").Append(_dVisitOwn)
              .Append("; POZYCZKI Banku ").Append(_dLoans).Append(" na ").Append(_dLoanSum).Append(" zl (Bank -> glowa rodu -> wodz; drabina 168 splaca), z pozyczki zostalo w kiesach ").Append(_dLoanLeft)
              .Append("; bez pozyczki (przy planie albo przy wizycie, wedlug powodu): ");
            if (_dLoanNo.Count == 0) sb.Append("-");
            else { bool f = true; foreach (var kv in _dLoanNo) { if (!f) sb.Append(", "); f = false; sb.Append(kv.Key).Append(' ').Append(kv.Value); } }
            sb.Append("; plany wygasle bez wizyty ").Append(_dExpired).Append(", partie zniszczone przed wizyta ").Append(_dDestroyed).Append(", plany w toku ").Append(_plans.Count + _savedPlans.Count);
            sb.Append(" | (2) DO DOMU (ludzie bez zbroi albo broni): partii ").Append(_dHomeParties).Append(", ludzi ").Append(_dHomeMen).Append(" (rannych ").Append(_dHomeWounded).Append(")")
              .Append(", wies bez danych BK - zostali ").Append(_dHomeNoVillage).Append(", nic do odeslania (maja zbroje i bron) ").Append(_dHomeNothing)
              .Append(", ta sama partia znowu w ").Append(RepeatDays).Append(" dobach ").Append(_dHomeRepeat)
              .Append("; pominiete: armia/bitwa/oblezenie/morze/rozwiazywana ").Append(_dHomeSkipBusy).Append(", rod gracza ").Append(_dHomeSkipPlayer).Append(", Straz/umarli ").Append(_dHomeSkipOther)
              .Append("; stoja (nie moga sie dozbroic) ").Append(stuckNow).Append(" partii, krocej niz ").Append(HomeDays(s)).Append(" dob ").Append(stuckShort).Append(" [");
            for (int i = 0; i < WhyCodes.Length; i++) { if (i > 0) sb.Append(", "); sb.Append(WhyCodes[i]).Append(' ').Append(whyN[i]); }
            sb.Append("]");
            sb.Append(" | SPIS (P0): partie lordow AI ").Append(lords).Append(", bez wpisu DTE ").Append(noEntry).Append(", R = 0 (z brakiem wpisu) ").Append(r0)
              .Append("; wedlug wieku partii (wszystkie/bez wpisu/R=0): < 1 doby ").Append(Age3(byAge, 0)).Append(", 1-3 ").Append(Age3(byAge, 1)).Append(", 3-7 ").Append(Age3(byAge, 2))
              .Append(", 7+ ").Append(Age3(byAge, 3)).Append(", sprzed wczytania (wiek nieznany) ").Append(Age3(byAge, 4));
            sb.Append(" | P0 niegotowe poza armia - 3 najnizsze R (stan po regule 2): ").Append(p0);
            sb.Append(" | przyklady: ").Append(_dEx.Count > 0 ? string.Join("; ", _dEx.ToArray()) : "brak");
            sb.Append(" | zapis: wczytane ").Append(_importN < 0 ? "brak klucza" : _importN.ToString()).Append(_importBad > 0 ? " (bledne " + _importBad + ")" : "")
              .Append(" | potkniecia ").Append(_dStumbles).Append(" (sesja ").Append(_stumbles).Append(", wycena ").Append(AiGear.QuoteStumbles).Append(")").Append('.');
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ P0: bitwa gracza
        /// <summary>Raz na bitwe gracza (start spotkania z gracza i misja DTE - ktore pierwsze): stan kazdej partii lorda AI tej bitwy. Tylko log, nie rzuca.</summary>
        internal static void BattleLine(MapEvent me, string where)
        {
            try
            {
                if (me == null) return;
                var last = _lastBattle != null ? _lastBattle.Target : null;
                if (ReferenceEquals(last, me)) return;
                _lastBattle = new WeakReference(me);
                var parts = new List<string>();
                BattleSideEnum my = BattleSideEnum.None;
                try { my = PartyBase.MainParty != null ? PartyBase.MainParty.Side : BattleSideEnum.None; } catch { }
                foreach (var pb in me.InvolvedParties)
                {
                    var mp = pb != null ? pb.MobileParty : null;
                    if (mp == null || mp.IsMainParty || !mp.IsLordParty) continue;
                    try { parts.Add(Describe(mp, pb.Side == my ? "sojusznik" : "wrog")); }
                    catch (Exception e) { Stumble("BattleLine(partia)", e); }
                }
                if (parts.Count == 0) return;
                Log.Info("Bitwa gracza (159 P0, " + where + "): partie lordow AI " + parts.Count + " || " + string.Join(" || ", parts.ToArray()) + ".");
            }
            catch (Exception e) { Stumble("BattleLine", e); }
        }

        private static string Describe(MobileParty mp, string side)
        {
            var s = Settings.Current;
            var all = AiGear.Armories();
            Dictionary<ItemObject, int> arm = null;
            bool entry = all != null && all.TryGetValue(mp.Id, out arm);
            int needS, usedS;
            int r = GarrisonKit.FillPercent(mp, arm, null, true, out needS, out usedS);
            var info = ArmedStrength.InfoOf(mp);
            string state = info == null ? "nie niegotowa (albo niepoliczona)" : info.Low ? (info.Free ? "NIEGOTOWA, zwolniona z czekania" : "NIEGOTOWA") : "gotowa (F " + info.F.ToString("0.00", Inv) + ")";
            double w = ArmedStrength.WaitDaysOf(mp);
            int b, need;
            bool can = ArmsFirst.CanAfford(mp, info != null ? info.Short : 0, null, out b, out need);
            Stuck st;
            string stuck = _stuck.TryGetValue(mp, out st) ? WhyCodes[Math.Max(0, Math.Min(WhyCodes.Length - 1, st.Why))] + " od " + ((CampaignTime.Now.ToHours - st.SinceH) / 24.0).ToString("0.0", Inv) + " dob" : "-";
            var lord = mp.LeaderHero;
            int d;
            string born = _born.TryGetValue(mp, out d) || (mp.StringId != null && _savedBorn.TryGetValue(mp.StringId, out d))
                          ? "dzien " + d + " (wiek " + (Today - d) + " dob)" : "sprzed wczytania";
            string ai = mp.DefaultBehavior + (mp.TargetSettlement != null ? " -> " + Nm(mp.TargetSettlement) : mp.TargetParty != null ? " -> " + Nm(mp.TargetParty) : "") + ", krotko " + mp.ShortTermBehavior;
            var p = Get(mp);
            string plan = p != null && !p.Done ? "miasto " + Nm(p.Town) + ", ludzi " + p.Men + " z " + p.Template + ", wycena " + p.Cost + " zl" : "-";
            return Nm(mp) + " [" + side + "] ludzi " + mp.MemberRoster.TotalManCount + " (rannych " + mp.MemberRoster.TotalWounded + ")"
                   + " | R teraz " + r + "% (" + usedS + "/" + needS + " slotow), wpis DTE " + (entry ? "TAK" : "NIE") + ", p9: " + state
                   + (w >= 0 ? ", czeka " + w.ToString("0.0", Inv) + " dob" : "")
                   + " | cel p9 " + ArmsFirst.TargetName(mp) + ", stoi: " + stuck + " | zloto wodza " + (lord != null ? lord.Gold.ToString(Inv) : "-")
                   + ", budzet AiGear " + b + " z potrzebnych " + need + (can ? "" : " - nie stac")
                   + " | w armii " + (mp.Army != null ? "TAK (" + Nm(mp.Army.LeaderParty) + ")" : "NIE") + " | powstala " + born + " | AI " + ai + " | plan 159: " + plan;
        }

        // ------------------------------------------------------------ zapis (arm_159_kit)
        private static readonly char[] Bad = { ',', ';', '|' };
        private static bool IdOk(string id) { return !string.IsNullOrEmpty(id) && id.IndexOfAny(Bad) < 0; }

        /// <summary>"1|plany|stanie|doby powstania": plan "partia,miasto,ludzi,szablon,sztuk,wycena,wlasne,pozyczka,dzien"; stanie "partia,odH,kod"; powstanie
        /// "partia,dzien". Razem z wpisami z zapisu jeszcze nie przypietymi do partii.</summary>
        internal static string Export()
        {
            var sb = new StringBuilder("1|");
            bool first = true;
            foreach (var kv in _plans)
            {
                var p = kv.Value; var id = kv.Key != null ? kv.Key.StringId : null;
                if (p.Done || !IdOk(id) || (p.TownId != null && !IdOk(p.TownId))) continue;
                if (!first) sb.Append(';'); first = false;
                AppendPlan(sb, id, p);
            }
            foreach (var kv in _savedPlans)
            {
                if (!IdOk(kv.Key)) continue;
                if (!first) sb.Append(';'); first = false;
                AppendPlan(sb, kv.Key, kv.Value);
            }
            sb.Append('|');
            first = true;
            foreach (var kv in _stuck)
            {
                var id = kv.Key != null ? kv.Key.StringId : null;
                if (!IdOk(id)) continue;
                if (!first) sb.Append(';'); first = false;
                sb.Append(id).Append(',').Append(kv.Value.SinceH.ToString("0.##", Inv)).Append(',').Append(kv.Value.Why);
            }
            foreach (var kv in _savedStuck)
            {
                if (!IdOk(kv.Key)) continue;
                if (!first) sb.Append(';'); first = false;
                sb.Append(kv.Key).Append(',').Append(kv.Value.SinceH.ToString("0.##", Inv)).Append(',').Append(kv.Value.Why);
            }
            sb.Append('|');
            first = true;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in _born)
            {
                var id = kv.Key != null && kv.Key.IsActive ? kv.Key.StringId : null;
                if (!IdOk(id) || !seen.Add(id)) continue;
                if (!first) sb.Append(';'); first = false;
                sb.Append(id).Append(',').Append(kv.Value);
            }
            foreach (var kv in _savedBorn)
            {
                if (!IdOk(kv.Key) || !seen.Add(kv.Key)) continue;
                if (!first) sb.Append(';'); first = false;
                sb.Append(kv.Key).Append(',').Append(kv.Value);
            }
            return sb.ToString();
        }

        private static void AppendPlan(StringBuilder sb, string id, KitPlan p)
        {
            sb.Append(id).Append(',').Append(p.TownId ?? "").Append(',').Append(p.Men).Append(',').Append(p.Template).Append(',').Append(p.Pieces).Append(',')
              .Append(p.Cost).Append(',').Append(p.Own).Append(',').Append(p.Loan).Append(',').Append(p.Day);
        }

        /// <summary>Wczytanie (po Reset z konstruktora zachowania): brak klucza albo zly format - pusto (plany i zegary od nowa).</summary>
        internal static void Import(string v)
        {
            _savedPlans.Clear(); _savedStuck.Clear(); _savedBorn.Clear(); _importN = -1; _importBad = 0;
            if (string.IsNullOrEmpty(v) || !v.StartsWith("1|", StringComparison.Ordinal)) return;
            _importN = 0;
            var sec = v.Substring(2).Split('|');
            try
            {
                if (sec.Length > 0)
                    foreach (var row in sec[0].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var c = row.Split(',');
                        int men, tpl, pcs, day; long cost, own, loan;
                        if (c.Length < 9 || c[0].Length == 0 || !int.TryParse(c[2], NumberStyles.Integer, Inv, out men) || !int.TryParse(c[3], NumberStyles.Integer, Inv, out tpl)
                            || !int.TryParse(c[4], NumberStyles.Integer, Inv, out pcs) || !long.TryParse(c[5], NumberStyles.Integer, Inv, out cost)
                            || !long.TryParse(c[6], NumberStyles.Integer, Inv, out own) || !long.TryParse(c[7], NumberStyles.Integer, Inv, out loan)
                            || !int.TryParse(c[8], NumberStyles.Integer, Inv, out day)) { _importBad++; continue; }
                        _savedPlans[c[0]] = new KitPlan { TownId = c[1].Length > 0 ? c[1] : null, Men = men, Template = tpl, Pieces = pcs, Cost = cost, Own = own, Loan = loan, Day = day };
                        _importN++;
                    }
                if (sec.Length > 1)
                    foreach (var row in sec[1].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var c = row.Split(',');
                        double h; int why;
                        if (c.Length < 3 || c[0].Length == 0 || !double.TryParse(c[1], NumberStyles.Float, Inv, out h) || !int.TryParse(c[2], NumberStyles.Integer, Inv, out why) || h < 0)
                        { _importBad++; continue; }
                        _savedStuck[c[0]] = new Stuck { SinceH = h, Why = Math.Max(0, Math.Min(WhyCodes.Length - 1, why)) };
                        _importN++;
                    }
                if (sec.Length > 2)
                    foreach (var row in sec[2].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var c = row.Split(',');
                        int day;
                        if (c.Length < 2 || c[0].Length == 0 || !int.TryParse(c[1], NumberStyles.Integer, Inv, out day)) { _importBad++; continue; }
                        _savedBorn[c[0]] = day;
                        _importN++;
                    }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
