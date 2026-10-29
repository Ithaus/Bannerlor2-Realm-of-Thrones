using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 297b (29.10, Jeff "napraw pozostale bledy i pusc test 90"): ESKORTA MILICJI WSI DLA WOZU POD BANDAMI i linia kopaln rudy.
    /// Dowod (autotest 137a, m113/logi/137a-probyN): zatkane wsie (zapas &gt;= 1.5 W - VillageGoodProductionCampaignBehavior.TickProductions nie wola TickGoodProduction)
    /// 40-75 z 571, z nich 24-56 z wozem "w domu, gotowy do wyjazdu" srednio 20-40 dob od wyjazdu; jedyna latka na ThinkAboutSendingItemToTown w calym folderze Modules
    /// to nasz prefiks m107 K1 (CartGuard.ThinkPrefix), wiec te wozy to wsie wstrzymane (30-55, 13-23 ponad 30 dob). Wstrzymanie zdejmuje dopiero brak zagrozenia, a bandy
    /// z domem w tej wsi kraza wokol niej (VillagePatrolDistanceAsDays 0.25 przy promieniu zagrozenia 9 jedn. - DefaultMobilePartyAIModel.GetBestInitiativeBehavior):
    /// 51 z 51 przykladow to Broken Men z wynikiem ucieczki 3-11 (Crowgrave 84 doby, ta sama banda). Kopalnie zelaza zatkane 1-4 z 26 (Tylemos w domu 60 dob), wydobycie
    /// ok. 27 lup/d ponizej modelu przez caly bieg.
    /// Gra (dekompilacja 1.4.8): ucieczka wozu - avoidScore = 0.943 x AvoidInitiative x (1..4 wedlug odleglosci) x num4, num4 = 1 / przewaga przy przewadze &lt; 1, a 0.05
    /// przy przewadze &gt;= 1 (wtedy wynik &lt;= 0.19, bez ucieczki); przewaga = sila wozu i sojusznikow / sila wroga (CalculateInitiativeScoresForEnemy; partie milicji
    /// i zalogi petla sojusznikow pomija - IsGarrison || IsMilitia: continue, wiec ruch ludzi miedzy milicja a wozem nie zmienia sily sojusznikow). Wiesniacy maja
    /// Aggressiveness 0 - silny woz nie atakuje (attackScore 0), tylko jedzie.
    /// Zmiana (klucz VillageCartMilitiaEscort, przy Village Cart Flee Unload i Village Cart Stuck Guard): wies daje wozowi tylu ludzi swojej milicji, ilu trzeba, by model
    /// AI gry nie widzial zagrozenia, a sila wozu byla co najmniej sila bandy (przewaga &gt;= 1 - num4 = 0.05 w kazdej odleglosci); liczba szukana modelem gry (podwajanie,
    /// potem polowienie - kazdy krok to ten sam GetBestInitiativeBehavior co m107). Kiedy: (1) HoldCheck - wies wstrzymana, zagrozenie trwa, magazyn &gt;= W: eskorta
    /// i zdjecie wstrzymania (gra laduje i wysyla woz jak zwykle); (2) CartGuard.Home - woz z jukami i rozkazem trzymany w domu ucieczka: eskorta od razu (bez 3 dob
    /// czekania i rozladunku); (3) woz w domu z eskorta bez jukow: magazyn &gt;= W - eskorta liczona od nowa (zdjeta, gdy zagrozenia nie ma), magazyn &lt; W - ludzie
    /// wracaja do milicji. Woz z jukami i rozkazem bez ucieczki - eskorta jedzie bez zmian. Za malo milicji (albo brak czynnej partii milicji) - nic nie zostaje przy
    /// wozie, wies czeka jak dotad. Bitwa wozu albo wsi, wies poza Normal, tratwa - bez ruchu ludzi tej doby. Klucz wylaczony - eskorty w domu wracaja do milicji,
    /// nowych nie ma (jak m107).
    /// Recenzja 297b (29.10) - TE SAMI LUDZIE: eskorta to prawdziwe elementy rosteru partii milicji wsi (MilitiaPartyComponent.MobileParty.MemberRoster), zdrowi,
    /// z kazdego rodzaju milicji proporcjonalnie (najwieksze reszty; weterani tez), przenoszone AddToCounts na obu rosterach - nie setter Settlement.Militia
    /// (dekompilacja 1.4.8: RemoveMilitiasFromParty zdejmuje RoundRandomized z kazdego elementu bez dopelnienia - mniej niz trzeba, a AddMilitiasToParty losuje
    /// weteranow wedlug CalculateVeteranMilitiaSpawnChance - z eskorty zwyklych wracaliby weterani). Powrot: ci sami ludzie do rosteru milicji, ranni jako ranni.
    /// Bez czynnej partii milicji (spalona wies, partia w bitwie) eskorta czeka przy wozie (liczona w wozach), az partia milicji wroci. W partii milicji zostaje
    /// co najmniej 1 czlowiek.
    /// Ksiega pochodzenia (PeopleBook, E4/7): wyjscie - PeopleBook.Take(roster milicji, n) (slownik milicji maleje o tylu, ilu ubylo z rosteru - uzgodnienie doby nie widzi
    /// "w nicosc"), udzialy w pamieci sesji przy wozie; powrot - PeopleBook.Give(roster milicji, udzialy z pamieci; po wczytaniu, bez pamieci - region wsi wozu, tak jak
    /// ksiega widzi ludzi taboru: Take z rosteru wiesniakow daje region ich wsi). Rownanie regionu: ludzie w wozie licza sie w TABORZE regionu wsi (RegionNow:
    /// Men(roster wiesniakow)), w pamieci udzialow - poza SLUZBA, wiec suma bez zmian. Polegli w drodze - DieCarts (POLEGLI regionu wsi), ich udzialy z pamieci przepadaja
    /// przy powrocie (licznik linii). Kontrola linii: zmiana rosteru milicji przy kazdym przeniesieniu wobec ludzi wydanych / wroconych (roznica 0).
    /// Zamknieta ekonomia: towar i zloto jak u kazdego wozu (sprzedaz w miescie za kase miasta). Wiesniacy nie jedza (DefaultMobilePartyFoodConsumptionModel.DoesPartyConsumeFood:
    /// IsVillager - nie), wiec eskorta nie zjada zapasow. Rozpoznanie eskorty po rodzaju wojska (milicja dowolnej kultury w partii wiesniakow - szablony wiesniakow SandBox
    /// i ROT jej nie maja), bez zapisu: po wczytaniu ludzie wracaja tak samo.
    /// Linie doby (LogGate.Lines): "Eskorta wozow (297b)", "Kopalnie rudy (297b)" (kopalnie zelaza i srebra: zatkane wedlug stanu wozu, zapas rudy wsi, przyklady) i co 5 dob
    /// "Miasta bez rudy a kopalnie (297b)" (miasto bez rudy na polce: najblizsza kopalnia zelaza ladem, zasieg wozu wsi, cena rudy w miescie i w miescie targowym kopalni).
    /// Wolane tylko z CartGuard.Daily (doba kampanii, watek glowny, poza tickiem AI i poza oknami milicji E4/8 - jak HoldCheck m107).
    /// </summary>
    internal static class CartEscort
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const int ExMax = 6;
        private const int NoOreEvery = 5;                                    // linia miast bez rudy co 5 dob (jak przyklady m107)
        private const double Eps = 1e-9;

        internal static bool On { get { var s = Settings.Current; return s != null && s.VillageCartMilitiaEscort && s.VillageCartFleeUnload && s.VillageCartStuckGuard; } }

        private static HashSet<CharacterObject> _mil;                       // milicja wszystkich kultur (bez chlopow kultur)
        private static readonly Dictionary<MobileParty, int> _setDay = new Dictionary<MobileParty, int>();   // woz -> doba nadania eskorty (bez drugiego liczenia tej doby)
        private static readonly Dictionary<MobileParty, PeopleBook.Shares> _sh = new Dictionary<MobileParty, PeopleBook.Shares>();   // woz -> udzialy pochodzenia eskorty (sesja)
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static long _taken, _back;                                   // sesja: ludzie milicji do wozow / z wozow
        private static int _outPrev = -1;                                    // ludzie w eskortach przy ostatniej linii (-1 - po wczytaniu)
        private static ItemObject _iron, _silver;
        private static bool _itemsSought;

        // liczniki doby
        private static int _tryHold, _tryFlee, _tryRe, _okHold, _okFlee, _okRe, _menHold, _menFlee, _menRe, _calm, _weak, _noMil, _evals, _backCarts, _backMen, _stumbles, _waitNoParty;
        private static long _takenDay, _backDay, _ctlOut, _ctlBack;          // _ctl*: zmiana rosteru milicji przy przeniesieniach (kontrola)
        private static double _bkTake, _bkMem, _bkReg, _bkLost;              // ksiega pochodzenia doby: zdjete z milicji, oddane z pamieci / regionem wsi, przepadle z eskortami

        private sealed class El { public CharacterObject C; public int H; }   // element puli milicji: rodzaj i zdrowi

        internal static void Reset()
        {
            _mil = null; _setDay.Clear(); _sh.Clear(); _errSites.Clear(); _taken = _back = 0; _outPrev = -1; _iron = _silver = null; _itemsSought = false;
            ClearDay();
        }

        private static void ClearDay()
        {
            _tryHold = _tryFlee = _tryRe = _okHold = _okFlee = _okRe = _menHold = _menFlee = _menRe = _calm = _weak = _noMil = _evals = _backCarts = _backMen = _stumbles = _waitNoParty = 0;
            _takenDay = _backDay = _ctlOut = _ctlBack = 0;
            _bkTake = _bkMem = _bkReg = _bkLost = 0.0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errSites.Add(where)) Log.Error(where, e);
        }

        private static int Today() { try { return (int)CampaignTime.Now.ToDays; } catch { return 0; } }

        // ------------------------------------------------------------ rozpoznanie eskorty
        private static HashSet<CharacterObject> Mil()
        {
            if (_mil != null) return _mil;
            var set = new HashSet<CharacterObject>();
            var vil = new HashSet<CharacterObject>();
            try
            {
                foreach (var cu in MBObjectManager.Instance.GetObjectTypeList<CultureObject>())
                {
                    if (cu == null) continue;
                    if (cu.Villager != null) vil.Add(cu.Villager);
                    if (cu.MeleeMilitiaTroop != null) set.Add(cu.MeleeMilitiaTroop);
                    if (cu.RangedMilitiaTroop != null) set.Add(cu.RangedMilitiaTroop);
                    if (cu.MeleeEliteMilitiaTroop != null) set.Add(cu.MeleeEliteMilitiaTroop);
                    if (cu.RangedEliteMilitiaTroop != null) set.Add(cu.RangedEliteMilitiaTroop);
                }
                set.ExceptWith(vil);                                         // chlop kultury nigdy nie jest eskorta (woz z niego sie sklada)
            }
            catch (Exception e) { Stumble("CartEscort.Mil", e); }
            _mil = set;
            return set;
        }

        /// <summary>Ludzie eskorty w wozie (zdrowi i ranni).</summary>
        internal static int Count(MobileParty cart)
        {
            int n = 0;
            try
            {
                var r = cart != null ? cart.MemberRoster : null;
                if (r == null) return 0;
                var mil = Mil();
                if (mil.Count == 0) return 0;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Character != null && !el.Character.IsHero && el.Number > 0 && mil.Contains(el.Character)) n += el.Number;
                }
            }
            catch (Exception e) { Stumble("CartEscort.Count", e); }
            return n;
        }

        /// <summary>Czynna partia milicji osady poza bitwa (null - brak: eskorta nie wychodzi i nie wraca).</summary>
        private static MobileParty MilParty(Settlement st)
        {
            var mc = st != null ? st.MilitiaPartyComponent : null;
            var mp = mc != null ? mc.MobileParty : null;
            return mp != null && mp.IsActive && mp.MemberRoster != null && mp.MapEvent == null ? mp : null;
        }

        private static string RegKey(Settlement home)
        {
            try { var reg = home != null ? OutlawLaw.RegionFor(home) : null; return reg != null && reg.StringId != null ? reg.StringId : ""; }
            catch { return ""; }
        }

        /// <summary>Ksiega pochodzenia przy powrocie n ludzi do rosteru milicji mr: udzialy z pamieci wozu (najwyzej tyle, ile w pamieci), reszta - region wsi wozu;
        /// po calym powrocie reszta pamieci (polegli i wzieci w drodze) przepada (licznik).</summary>
        private static void BookBack(MobileParty cart, Settlement home, TroopRoster mr, int n)
        {
            PeopleBook.Shares s0;
            _sh.TryGetValue(cart, out s0);
            double known = s0 != null ? Math.Min(n, s0.Total) : 0.0;
            var give = known > Eps ? s0.Cut(known) : new PeopleBook.Shares();
            double rest = n - known;
            if (rest > Eps) { give.Add(RegKey(home), rest); _bkReg += rest; }
            _bkMem += known;
            PeopleBook.Give(mr, give, -1);
            if (s0 != null) { if (s0.Total > Eps) _bkLost += s0.Total; _sh.Remove(cart); }
        }

        /// <summary>Wszyscy ludzie eskorty z wozu do rosteru partii milicji wsi domowej - ci sami ludzie, ranni jako ranni. Zwraca liczbe ludzi; -1 - brak czynnej
        /// partii milicji (eskorta zostaje przy wozie).</summary>
        private static int Detach(MobileParty cart, Village v)
        {
            var r = cart.MemberRoster; var st = v.Settlement;
            if (r == null || st == null) return 0;
            var mil = Mil();
            var list = new List<TroopRosterElement>();
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.Character != null && !el.Character.IsHero && el.Number > 0 && mil.Contains(el.Character)) list.Add(el);
            }
            if (list.Count == 0)
            {
                PeopleBook.Shares s0;
                if (_sh.TryGetValue(cart, out s0)) { if (s0.Total > Eps) _bkLost += s0.Total; _sh.Remove(cart); }   // eskorta zginela cala w drodze
                return 0;
            }
            var mp = MilParty(st);
            if (mp == null) { _waitNoParty++; return -1; }
            var mr = mp.MemberRoster;
            int m0 = mr.TotalManCount, n = 0;
            foreach (var el in list)
            {
                r.AddToCounts(el.Character, -el.Number, false, -el.WoundedNumber);
                mr.AddToCounts(el.Character, el.Number, false, el.WoundedNumber);
                n += el.Number;
            }
            _ctlBack += mr.TotalManCount - m0;
            if (n > 0) { _back += n; _backDay += n; BookBack(cart, st, mr, n); }
            return n;
        }

        // ------------------------------------------------------------ zagrozenie (model AI gry)
        private static float Str(MobileParty p)
        {
            if (p == null) return 0f;
            try { return p.Army != null && (p.AttachedTo != null || p.Army.LeaderParty == p) ? p.Army.EstimatedStrength : p.Party.EstimatedStrength; } catch { return 0f; }
        }

        /// <summary>To samo zapytanie co m107 HoldCheck: wynik inicjatywy &gt; 1 i zachowanie ucieczki.</summary>
        private static bool Threat(MobileParty cart, MobilePartyAIModel model, out MobileParty foe, out float sc)
        {
            AiBehavior b; Vec2 av;
            _evals++;
            model.GetBestInitiativeBehavior(cart, out b, out foe, out sc, out av);
            return sc > 1f && MobileParty.IsFleeBehavior(b);
        }

        /// <summary>Zdrowi ludzie milicji, ktorych eskorta moze wziac (rodzaj rozpoznawany przez Count / Detach, bez bohaterow i wightow).</summary>
        private static List<El> Pool(TroopRoster mr, out int healthy)
        {
            healthy = 0;
            var mil = Mil();
            var pool = new List<El>();
            for (int i = 0; i < mr.Count; i++)
            {
                var el = mr.GetElementCopyAtIndex(i);
                var c = el.Character;
                if (c == null || c.IsHero || !mil.Contains(c) || PeopleBook.IsWight(c)) continue;
                int h = el.Number - el.WoundedNumber;
                if (h <= 0) continue;
                pool.Add(new El { C = c, H = h });
                healthy += h;
            }
            return pool;
        }

        /// <summary>Sklad n ludzi z puli proporcjonalnie do zdrowych kazdego rodzaju (najwieksze reszty, remis - kolejnosc rosteru).</summary>
        private static int[] Targets(List<El> pool, int all, int n)
        {
            var t = new int[pool.Count];
            if (n <= 0 || all <= 0) return t;
            var rem = new double[pool.Count];
            int sum = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                double x = (double)pool[i].H * n / all;
                t[i] = Math.Min(pool[i].H, (int)Math.Floor(x));
                rem[i] = x - t[i];
                sum += t[i];
            }
            while (sum < n)
            {
                int bi = -1;
                for (int i = 0; i < pool.Count; i++) if (t[i] < pool[i].H && (bi < 0 || rem[i] > rem[bi])) bi = i;
                if (bi < 0) break;
                t[bi]++; rem[bi] = -1.0; sum++;
            }
            return t;
        }

        /// <summary>Ustawia eskorte wozu na sklad n ludzi z puli: ci sami ludzie z rosteru milicji do wozu albo z powrotem (cur - stan przeniesionych).</summary>
        private static void SetTo(TroopRoster cart, TroopRoster mr, List<El> pool, int all, int[] cur, int n)
        {
            var t = Targets(pool, all, n);
            for (int i = 0; i < pool.Count; i++)
            {
                int d = t[i] - cur[i];
                if (d == 0) continue;
                mr.AddToCounts(pool[i].C, -d, false, 0, 0, false);
                cart.AddToCounts(pool[i].C, d);
                cur[i] = t[i];
            }
        }

        /// <summary>Eskorta wozu w domu (woz w osadzie wsi, bez bitwy - sprawdza wolajacy): dotychczasowa eskorta wraca do milicji, potem zagrozenie modelem gry;
        /// brak zagrozenia - true, men 0; jest - najmniejsza liczba zdrowych ludzi milicji (podwajanie od 1 do calej puli, potem polowienie), przy ktorej model nie widzi
        /// zagrozenia, a sila wozu &gt;= sila bandy, ktora go trzymala; znaleziona - ci ludzie z rosteru milicji do wozu (ksiega: Take), true; nie - nic przy wozie, false.
        /// Eskorta, ktora nie mogla wrocic (brak czynnej partii milicji) - false bez zmian.
        /// why: 0 wstrzymanie m107, 1 ucieczka zaladowanego wozu, 2 odswiezenie eskorty w domu.</summary>
        internal static bool TryEscort(MobileParty cart, Village v, MobilePartyAIModel model, int why, out int men)
        {
            men = 0;
            if (why == 0) _tryHold++; else if (why == 1) _tryFlee++; else _tryRe++;
            var st = v.Settlement; var r = cart.MemberRoster;
            int had = Detach(cart, v);
            if (had < 0) { _noMil++; return false; }                        // eskorta czeka przy wozie na partie milicji
            MobileParty foe; float sc;
            if (!Threat(cart, model, out foe, out sc))
            {
                _calm++;
                if (had > 0) { _backCarts++; _backMen += had; }
                _setDay.Remove(cart);
                return true;
            }
            var mp = MilParty(st);
            int healthy = 0;
            var pool = mp != null ? Pool(mp.MemberRoster, out healthy) : null;
            int avail = mp != null ? Math.Min(healthy, mp.MemberRoster.TotalManCount - 1) : 0;   // co najmniej 1 czlowiek zostaje w partii milicji
            if (pool == null || pool.Count == 0 || avail < 1)
            {
                _noMil++;
                if (had > 0) { _backCarts++; _backMen += had; }
                _setDay.Remove(cart);
                return false;
            }
            var mr = mp.MemberRoster;
            float need = Str(foe);
            int m0 = mr.TotalManCount;
            var cur = new int[pool.Count];
            int lo = 0, hi = -1, n = 1;
            try
            {
                while (true)
                {
                    SetTo(r, mr, pool, healthy, cur, n);
                    MobileParty f2; float s2;
                    if (!Threat(cart, model, out f2, out s2) && cart.Party.EstimatedStrength >= need) { hi = n; break; }
                    lo = n;
                    if (n >= avail) break;
                    n = Math.Min(avail, n * 2);
                }
                if (hi > 0)
                {
                    while (hi - lo > 1)
                    {
                        int mid = (lo + hi) / 2;
                        SetTo(r, mr, pool, healthy, cur, mid);
                        MobileParty f3; float s3;
                        if (!Threat(cart, model, out f3, out s3) && cart.Party.EstimatedStrength >= need) hi = mid; else lo = mid;
                    }
                    SetTo(r, mr, pool, healthy, cur, hi);
                }
                else SetTo(r, mr, pool, healthy, cur, 0);
            }
            catch
            {
                try { SetTo(r, mr, pool, healthy, cur, 0); } catch { }      // nic z niczego: przy wyjatku ludzie wracaja do milicji sprzed szukania
                try { mr.RemoveZeroCounts(); } catch { }
                throw;
            }
            mr.RemoveZeroCounts();
            if (hi <= 0)
            {
                _weak++;
                if (had > 0) { _backCarts++; _backMen += had; }
                _setDay.Remove(cart);
                return false;
            }
            _ctlOut += m0 - mr.TotalManCount;
            // ksiega pochodzenia: slownik milicji maleje o tylu, ilu ubylo z rosteru; udzialy przy wozie (pamiec sesji) do powrotu
            var sh = PeopleBook.Take(mr, hi, -1);
            PeopleBook.Shares s0;
            if (!_sh.TryGetValue(cart, out s0)) { s0 = new PeopleBook.Shares(); _sh[cart] = s0; }
            s0.AddAll(sh);
            _bkTake += hi;
            _taken += hi; _takenDay += hi;
            men = hi;
            if (why == 0) { _okHold++; _menHold += hi; } else if (why == 1) { _okFlee++; _menFlee += hi; } else { _okRe++; _menRe += hi; }
            _setDay[cart] = Today();
            return true;
        }

        private static int Store(Settlement home)
        {
            int n = 0;
            var r = home != null ? home.ItemRoster : null;
            if (r != null) for (int i = 0; i < r.Count; i++) n += r.GetElementNumber(i);
            return n;
        }

        /// <summary>CartGuard.HoldCheck: wies wstrzymana, woz w domu, zagrozenie trwa. Magazyn &gt;= W (gra chce wyslac woz) - eskorta; true = wstrzymanie do zdjecia.</summary>
        internal static bool HoldStep(MobileParty cart, Village v, MobilePartyAIModel model)
        {
            if (!On || model == null) return false;
            try
            {
                var home = v.Settlement;
                if (v.VillageState != Village.VillageStates.Normal || cart.MapEvent != null || cart.IsInRaftState || (home.Party != null && home.Party.MapEvent != null)) return false;
                if (Store(home) < v.GetWarehouseCapacity()) return false;
                int men;
                return TryEscort(cart, v, model, 0, out men);
            }
            catch (Exception e) { Stumble("CartEscort.HoldStep", e); return false; }
        }

        private static void Back(MobileParty cart, Village v)
        {
            int b = Detach(cart, v);
            if (b < 0) return;                                               // brak partii milicji - eskorta czeka przy wozie
            _backCarts++; _backMen += b; _setDay.Remove(cart);
        }

        /// <summary>CartGuard.Home (woz w domu, przed logika ucieczki m107): lo - juki i rozkaz jazdy. true - woz dostal eskorte pod ucieczka (wolajacy konczy epizod).</summary>
        internal static bool HomeStep(MobileParty cart, Village v, Settlement home, bool lo)
        {
            try
            {
                if (v.VillageState != Village.VillageStates.Normal || cart.MapEvent != null || cart.IsInRaftState || (home.Party != null && home.Party.MapEvent != null)) return false;
                int have = Count(cart);
                if (!On)
                {
                    if (have > 0) Back(cart, v);                             // klucz wylaczony: eskorta w domu wraca do milicji
                    return false;
                }
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.MobilePartyAIModel : null;
                if (model == null) return false;
                int day; bool fresh = _setDay.TryGetValue(cart, out day) && day == Today();
                if (lo && cart.ShortTermTargetSettlement == home)
                {
                    if (fresh) return true;                                  // eskorta nadana dzis (HoldCheck) - AI gry ruszy woz w nastepnym ticku
                    int men;
                    return TryEscort(cart, v, model, 1, out men) && men > 0;
                }
                if (have == 0 || lo || fresh) return false;                 // bez eskorty; woz z jukami i rozkazem jedzie z eskorta bez zmian
                if (Store(home) >= v.GetWarehouseCapacity()) { int m2; TryEscort(cart, v, model, 2, out m2); }
                else Back(cart, v);
                return false;
            }
            catch (Exception e) { Stumble("CartEscort.HomeStep", e); return false; }
        }

        /// <summary>CartGuard.Daily przy kluczu straznika wylaczonym: eskorty wozow w domu (bez bitwy) wracaja do milicji.</summary>
        internal static void Sweep()
        {
            foreach (var v in Village.All)
            {
                try
                {
                    var cart = v != null && v.VillagerPartyComponent != null ? v.VillagerPartyComponent.MobileParty : null;
                    if (cart == null || !cart.IsActive || v.Settlement == null || cart.CurrentSettlement != v.Settlement || cart.MapEvent != null) continue;
                    if (v.Settlement.Party != null && v.Settlement.Party.MapEvent != null) continue;
                    if (Count(cart) > 0) Back(cart, v);
                }
                catch (Exception e) { Stumble("CartEscort.Sweep", e); }
            }
        }

        /// <summary>Pamiec sesji: woz nieczynny (rozbity, rozwiazany) - udzialy eskorty przepadaja (licznik), doba nadania zdjeta.</summary>
        private static void Tidy()
        {
            List<MobileParty> gone = null;
            foreach (var kv in _sh) if (kv.Key == null || !kv.Key.IsActive) (gone ?? (gone = new List<MobileParty>())).Add(kv.Key);
            if (gone != null) foreach (var mp in gone) { _bkLost += _sh[mp].Total; _sh.Remove(mp); }
            List<MobileParty> g2 = null;
            foreach (var kv in _setDay) if (kv.Key == null || !kv.Key.IsActive) (g2 ?? (g2 = new List<MobileParty>())).Add(kv.Key);
            if (g2 != null) foreach (var mp in g2) _setDay.Remove(mp);
        }

        // ------------------------------------------------------------ linie doby
        private sealed class MineSum
        {
            public int N, Clog, Held, HomeReady, Road, NoCart, NotNormal, Escorted, Ore; public double Fill;
            public List<string> Ex = new List<string>();
        }

        private struct MineAt { public Village V; public int Ore, W; }

        private static void Mines(Village v, MobileParty cart, MineSum m, ItemObject ore, List<MineAt> list)
        {
            var home = v.Settlement;
            int store = Store(home), W = Math.Max(1, v.GetWarehouseCapacity());
            int ore1 = ore != null && home.ItemRoster != null ? home.ItemRoster.GetItemNumber(ore) : 0;
            m.N++; m.Ore += ore1; m.Fill += (double)store / W;
            if (list != null) list.Add(new MineAt { V = v, Ore = ore1, W = W });
            bool active = cart != null && cart.IsActive;
            if (active && Count(cart) > 0) m.Escorted++;
            if (store < 1.5f * W) return;
            m.Clog++;
            int held = CartGuard.HeldSince(v);
            string what;
            if (v.VillageState != Village.VillageStates.Normal) { m.NotNormal++; what = "wies " + v.VillageState; }
            else if (!active) { m.NoCart++; what = "brak wozu"; }
            else if (cart.CurrentSettlement == home && held >= 0) { m.Held++; what = "wstrzymana m107 od " + (Today() - held) + " dob"; }
            else if (cart.CurrentSettlement == home) { m.HomeReady++; what = "woz w domu"; }
            else { m.Road++; what = "woz poza domem" + (cart.CurrentSettlement != null ? " (" + (cart.CurrentSettlement.StringId ?? "?") + ")" : ""); }
            if (m.Ex.Count < ExMax)
                m.Ex.Add((home.Name != null ? home.Name.ToString() : "?") + " [" + (home.StringId ?? "?") + "] zapas/W " + ((double)store / W).ToString("0.00", Inv) + " (" + store + "/" + W
                         + "), ruda " + ore1 + ", milicja " + home.Militia.ToString("0", Inv) + ", " + what);
        }

        private static string MineText(string name, MineSum m)
        {
            return name + ": kopaln " + m.N + ", zatkanych (zapas >= 1.5 W) " + m.Clog + " [wstrzymane m107 " + m.Held + ", woz w domu " + m.HomeReady + ", woz poza domem " + m.Road
                   + ", brak wozu " + m.NoCart + ", wies poza Normal " + m.NotNormal + "], ruda w magazynach " + m.Ore + ", zapas/W srednio " + (m.N > 0 ? (m.Fill / m.N).ToString("0.00", Inv) : "-")
                   + ", wozy z eskorta " + m.Escorted + (m.Ex.Count > 0 ? "; zatkane: " + string.Join("; ", m.Ex) : "");
        }

        private static string SN(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString().Replace(';', ' ') : "?"; } catch { return "?"; } }

        private static Settlement MarketOf(Village v)
        {
            var b = v.Bound;
            if (b != null && b.IsTown) return b;
            var tb = v.TradeBound;
            return tb != null && tb.IsTown ? tb : null;
        }

        /// <summary>Linia co 5 dob (recenzja 297b, uwaga 2 - dowod zamiast twierdzenia): kazde miasto bez rudy na polce (MarketData zelaza 0 - ta sama miara co licznik
        /// "do miasta bez rudy" wyboru miasta wozow) - najblizsza kopalnia zelaza droga ladowa (MapDistanceModel, jak MarketCarts.Dist), czy w zasiegu wyboru miasta wozow wsi
        /// (MarketCarts.CartRange; barki rzeczne pominiete), wojna miedzy frakcjami, ruda w magazynie kopalni, cena rudy za pierwsza sztuke w tym miescie i w miescie
        /// targowym kopalni (GetItemPrice przy sprzedazy). Tylko odczyt.</summary>
        private static void NoOreLine(int day, List<MineAt> mines)
        {
            var s = Settings.Current;
            var cat = DefaultItemCategories.Iron;
            if (s == null || cat == null || _iron == null) return;
            var dm = Campaign.Current.Models.MapDistanceModel;
            float range = MarketCarts.CartRange(s);
            int k = 0, inR = 0, outR = 0, noRoad = 0, war = 0;
            double pSum = 0, pMine = 0; int pN = 0, pmN = 0;
            var ex = new List<string>();
            foreach (var t in Town.AllTowns)
            {
                try
                {
                    var ts = t != null ? t.Settlement : null;
                    if (ts == null || t.MarketData == null || t.MarketData.GetItemCountOfCategory(cat) > 0) continue;
                    k++;
                    int p = t.GetItemPrice(_iron, null, true);
                    pSum += p; pN++;
                    MineAt best = default(MineAt); float bd = float.MaxValue; bool found = false;
                    foreach (var m in mines)
                    {
                        var ms = m.V.Settlement;
                        if (ms == null) continue;
                        float d = dm.GetDistance(ms, ts, false, false, MobileParty.NavigationType.Default);
                        if (CartGuard.NoLandRoad(d)) continue;
                        if (d < bd) { bd = d; best = m; found = true; }
                    }
                    string what;
                    if (!found) { noRoad++; what = "bez drogi ladowej do kopalni"; }
                    else
                    {
                        var ms = best.V.Settlement;
                        var fa = ms.MapFaction; var fb = ts.MapFaction;
                        bool atWar = fa != null && fb != null && fa != fb && fa.IsAtWarWith(fb);
                        bool r = range <= 0f || bd <= range;
                        if (atWar) war++; else if (r) inR++; else outR++;
                        var mk = MarketOf(best.V);
                        int pm = mk != null && mk.Town != null ? mk.Town.GetItemPrice(_iron, null, true) : -1;
                        if (pm >= 0) { pMine += pm; pmN++; }
                        what = SN(ms) + " " + bd.ToString("0", Inv) + (r ? " (w zasiegu)" : " (poza zasiegiem)") + (atWar ? " w wojnie" : "") + ", ruda " + best.Ore + "/W " + best.W
                               + ", cena " + p + " d wobec " + (pm >= 0 ? pm + " d (" + SN(mk) + ")" : "-");
                    }
                    if (ex.Count < 25) ex.Add(SN(ts) + ": " + (found ? what : what + ", cena " + p + " d"));
                }
                catch (Exception e) { Stumble("CartEscort.NoOreLine(miasto)", e); }
            }
            Log.Info("Miasta bez rudy a kopalnie (297b): dzien " + day + " | miast bez rudy na polce " + k + ", kopalni zelaza " + mines.Count + ", zasieg wozu wsi "
                     + (range > 0f ? range.ToString("0", Inv) : "bez limitu") + " | najblizsza kopalnia ladem: w zasiegu " + inR + ", poza zasiegiem " + outR + ", bez drogi ladowej "
                     + noRoad + ", w wojnie " + war + " | cena rudy za pierwsza sztuke: miasta bez rudy srednio " + (pN > 0 ? (pSum / pN).ToString("0", Inv) : "-")
                     + " d, miasta targowe ich kopalni " + (pmN > 0 ? (pMine / pmN).ToString("0", Inv) : "-") + " d" + (ex.Count > 0 ? " | " + string.Join("; ", ex) : "") + ".");
        }

        /// <summary>CartGuard.Daily (po straznku): ksiega eskort i linie doby (LogGate.Lines). quiet - wszystkie klucze W2 wylaczone: eskorty w domu wracaja do milicji
        /// (tylko gdy jakies byly albo po wczytaniu), bez linii.</summary>
        internal static void Daily(int day, bool guardOn, bool quiet)
        {
            try
            {
                if (quiet)
                {
                    if (_outPrev != 0)
                    {
                        Sweep();
                        int left = 0;
                        foreach (var v in Village.All)
                        {
                            var cart = v != null && v.VillagerPartyComponent != null ? v.VillagerPartyComponent.MobileParty : null;
                            if (cart != null && cart.IsActive) left += Count(cart);
                        }
                        _outPrev = left;
                    }
                    Tidy();
                    ClearDay();
                    return;
                }
                if (!guardOn) Sweep();
                Tidy();
                if (!_itemsSought)
                {
                    _itemsSought = true;
                    try { var om = MBObjectManager.Instance; if (om != null) { _iron = om.GetObject<ItemObject>("iron"); _silver = om.GetObject<ItemObject>("silver"); } } catch { }
                }
                bool noOre = LogGate.Lines && day % NoOreEvery == 0;
                var feList = noOre ? new List<MineAt>() : null;
                int out0 = 0, carts = 0, atHome = 0, onRoad = 0, inTown = 0, elsewhere = 0, menHome = 0, menRoad = 0, menTown = 0;
                var fe = new MineSum(); var ag = new MineSum();
                foreach (var v in Village.All)
                {
                    try
                    {
                        if (v == null || v.Settlement == null) continue;
                        var cart = v.VillagerPartyComponent != null ? v.VillagerPartyComponent.MobileParty : null;
                        if (cart != null && cart.IsActive)
                        {
                            int n = Count(cart);
                            if (n > 0)
                            {
                                out0 += n; carts++;
                                var at = cart.CurrentSettlement;
                                if (at == v.Settlement) { atHome++; menHome += n; }
                                else if (at == null) { onRoad++; menRoad += n; }
                                else if (at.IsTown) { inTown++; menTown += n; }
                                else elsewhere++;
                            }
                        }
                        string tid = v.VillageType != null ? v.VillageType.StringId : null;
                        if (tid == "iron_mine") Mines(v, cart, fe, _iron, feList);
                        else if (tid == "silver_mine") Mines(v, cart, ag, _silver, null);
                    }
                    catch (Exception e) { Stumble("CartEscort.Daily(wies)", e); }
                }
                long lost = _outPrev >= 0 ? _outPrev + _takenDay - _backDay - out0 : -1;
                double memMen = 0; foreach (var x in _sh.Values) memMen += x.Total;
                long ctlDiff = (_ctlOut - _takenDay) + (_ctlBack - _backDay);
                if (LogGate.Lines)
                {
                    var sb = new StringBuilder(1600);
                    sb.Append("Eskorta wozow (297b): dzien ").Append(day).Append(" | klucz Village Cart Militia Escort ").Append(On ? "TAK" : "NIE").Append(guardOn ? "" : " (straznik wylaczony - eskorty w domu wracaja)")
                      .Append(" | proby: wstrzymane m107 ").Append(_tryHold).Append(" (z eskorta ").Append(_okHold).Append(", ludzi ").Append(_menHold).Append("), ucieczka z jukami ").Append(_tryFlee)
                      .Append(" (z eskorta ").Append(_okFlee).Append(", ludzi ").Append(_menFlee).Append("), odswiezenie w domu ").Append(_tryRe).Append(" (z eskorta ").Append(_okRe).Append(", ludzi ").Append(_menRe)
                      .Append("); bez zagrozenia ").Append(_calm).Append(", za malo milicji ").Append(_weak).Append(", bez milicji albo wojska kultury ").Append(_noMil).Append(", ocen modelu ").Append(_evals)
                      .Append(" | milicja: wydana ").Append(_takenDay).Append(", wrocila ").Append(_backDay).Append(" (wozow ").Append(_backCarts).Append(", ludzi ").Append(_backMen).Append(")")
                      .Append(" | teraz w eskortach ").Append(out0).Append(" ludzi w ").Append(carts).Append(" wozach (w domu ").Append(atHome).Append(" / ").Append(menHome)
                      .Append(", w drodze ").Append(onRoad).Append(" / ").Append(menRoad).Append(", w miescie ").Append(inTown).Append(" / ").Append(menTown).Append(", gdzie indziej ").Append(elsewhere).Append(")")
                      .Append(" | ksiega: wczoraj ").Append(_outPrev >= 0 ? _outPrev.ToString(Inv) : "-").Append(" + wydana - wrocila - dzis = straty ").Append(lost >= 0 ? lost.ToString(Inv) : "od jutra")
                      .Append(" (sesja: wydana ").Append(_taken).Append(", wrocila ").Append(_back).Append(")")
                      .Append(" | kontrola przenosin: roster milicji -").Append(_ctlOut).Append(" / +").Append(_ctlBack).Append(" wobec wydana / wrocila, roznica ").Append(ctlDiff)
                      .Append(", czeka przy wozie bez partii milicji ").Append(_waitNoParty)
                      .Append(" | pochodzenie: zdjete z milicji ").Append(_bkTake.ToString("0", Inv)).Append(", oddane milicji ").Append((_bkMem + _bkReg).ToString("0", Inv))
                      .Append(" (z pamieci ").Append(_bkMem.ToString("0", Inv)).Append(", region wsi ").Append(_bkReg.ToString("0", Inv)).Append("), przepadle z eskortami ").Append(_bkLost.ToString("0", Inv))
                      .Append(", w pamieci ").Append(memMen.ToString("0", Inv)).Append(" ludzi w ").Append(_sh.Count).Append(" wozach")
                      .Append(" | potkniecia ").Append(_stumbles).Append('.');
                    Log.Info(sb.ToString());
                    Log.Info("Kopalnie rudy (297b): dzien " + day + " | " + MineText("zelazo", fe) + " | " + MineText("srebro", ag) + ".");
                    if (noOre) { try { NoOreLine(day, feList); } catch (Exception e) { Stumble("CartEscort.NoOreLine", e); } }
                }
                _outPrev = out0;
            }
            catch (Exception e) { Stumble("CartEscort.Daily", e); }
            ClearDay();
        }
    }
}
