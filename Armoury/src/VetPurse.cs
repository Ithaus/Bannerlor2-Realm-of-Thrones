using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// E5/B6 (paczka 201, projekt etapu 5 v2 rozdz. 2 R19, rozdz. 3 "E5/B6"; RD D5) - PIENIADZE WETERANA I NAJMU. Wylacznik VeteranPurseFollowsMan.
    /// Regula "sakiewka idzie z czlowiekiem" (KL 1.3 "docelowo sakiewka najetego"; OB 6.2 "do kasy miasta karczmy" odrzucone - najmuje sie prawdziwego czlowieka):
    ///  1. Zwolnieni 166 (ClanBudget.ReleaseFrom): czesc sakiewki partii z odchodzacymi (proporcjonalnie do ludzi - jak dotad) dzieli sie wedlug tego, dokad poszli:
    ///     ci, ktorzy staneli w karczmie (E4/10, PeopleBook.VetCommit), niosa swoja czesc do PULI PIENIEDZY WETERANOW miasta karczmy; reszta - do kiesy wsi jak dotad.
    ///  2. Pula miasta jest wydawana w miescie karczmy jak "zycie" (R9): co dobe SutlerDailyShare (10%) do kasy miasta; gdy w miescie nie ma juz weteranow
    ///     (wszyscy najeci albo wrocili do domu po VeteranWaitDays) - cala reszta naraz (wydali przy wyjezdzie, jak MenPurse.OnLeft). Miasto Innych - najblizsze
    ///     miasto ludzi. Miasto z zapisu, ktorego nie ma w swiecie - zloto zostaje w puli jako "osierocone" (posiadacz, nic w nicosc; oczekiwane 0).
    ///  3. Najem weterana (Veterans.OnTavernNumber - AI, karawany, gracz): najeci zabieraja swoj udzial w puli miasta (pula x najeci / weterani miasta przed najmem)
    ///     do sakiewki partii, ktora ich najela. Bez sakiewki ludzi (MenPurseEnabled) albo bez okna najmu - udzial zostaje w puli (wydany w miescie).
    ///  4. Zaplata za najem z karczmy -> sakiewka najetych (partia, ktora najela), nie kasa miasta: AI i karawany w LevyGold (oplata bez konia z polki - za konia
    ///     miasto dostaje dalej to, co kupujacy za niego zaplacil, 157), gracz - prefiks/postfiks na BuyMercenaries i buy_mercenaries_on_consequence (gra kasuje
    ///     zaplate gracza GiveGoldAction do nikogo - dotad zloto w nicosc poza czescia za konia 157). Jedna regula dla gracza i AI; wymaga RecruitGoldToSeller
    ///     (bez niej zaplata AI znika jak w grze) i MenPurseEnabled.
    /// Hak B1 (KL 1.3: uslugi 75% kupcy / 25% lud): wydatek puli idzie dzis caly do kasy miasta (jak markietani); B1 dzieli go w tym samym miejscu (Spend, przy wplacie).
    /// Weterani nie sa juz ludzmi rodu: bez licznikow "wlasne" D stalego (169c) i K3 zwrotu korony (to wydatki ludzi rodu w jego miescie).
    /// Ksiega pieniadza: pula = posiadacz "sakiewki weteranow w karczmach (E5/B6)" (MoneyLedger.HVetPurse = H37); wydatek puli w naszym ticku = pozycja
    /// MoneyLedger.MVetPurse (M33); sakiewka zwolnionych do puli - przelew sakiewka -> pula (posiadacze, poza kasami osad); najem - pula / zaplata -> sakiewka.
    /// Zapis: "arm_vetpurse" (id miasta = zloto). Wylaczony klucz: nowe wejscia nie powstaja, cala pula wydana w miastach karczm tej samej doby (stan sprzed paczki).
    /// Wyjatek per obiekt - licznik potkniec (bez globalnego wylacznika).
    /// </summary>
    internal static class VetPurse
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.VeteranPurseFollowsMan; } }
        /// <summary>Zaplata za najem do sakiewki najetych: klucz, sakiewka ludzi i zloto werbunku do sprzedajacego (bez RecruitGoldToSeller zaplata AI znika jak w grze).</summary>
        internal static bool FeeOn { get { var s = Settings.Current; return s != null && s.VeteranPurseFollowsMan && s.RecruitGoldToSeller && MenPurse.On; } }

        // ------------------------------------------------------------ stan (zapis arm_vetpurse)
        private static readonly Dictionary<string, int> _gold = new Dictionary<string, int>();   // id miasta karczmy -> zloto puli weteranow

        // ------------------------------------------------------------ liczniki od linii do linii (bez zapisu)
        private static long _p0;                                       // pula przy poprzedniej linii (albo z zapisu)
        private static long _dRelIn, _dRelTook; private static int _dRelN, _dRelMenTav, _dRelMenGone;
        private static long _dHireOut, _dHireLord, _dHireCar, _dHirePlayer, _dHireStay; private static int _dHireMen, _dHireStayMen;
        private static long _dFeeLord, _dFeeCar, _dFeePlayer, _dFeeTown, _dHorseTown; private static int _dFeeN;
        private static long _dSpent, _dDrain, _dLiquid, _dOthers; private static int _dSpentTowns, _dDrainN, _dOthersN;
        private static float _dRate;
        private static double _dMs;
        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static int _wiredPlayer;

        /// <summary>Konstruktor ArmouryBehavior (nowa kampania i kazde wczytanie) - pula pusta przed SyncData, liczniki, potkniecia.</summary>
        internal static void Reset()
        {
            _gold.Clear(); _p0 = 0; _stumbles = 0; _err.Clear();
            ClearDay();
        }

        private static void ClearDay()
        {
            _dRelIn = _dRelTook = 0; _dRelN = _dRelMenTav = _dRelMenGone = 0;
            _dHireOut = _dHireLord = _dHireCar = _dHirePlayer = _dHireStay = 0; _dHireMen = _dHireStayMen = 0;
            _dFeeLord = _dFeeCar = _dFeePlayer = _dFeeTown = _dHorseTown = 0; _dFeeN = 0;
            _dSpent = _dDrain = _dLiquid = _dOthers = 0; _dSpentTowns = _dDrainN = _dOthersN = 0;
            _dRate = 0f; _dMs = 0; _stumblesDay = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            try { if (_err.Add(where)) Log.Error("VetPurse." + where, e); } catch { }
        }

        private static bool BadId(string id)
        {
            if (string.IsNullOrEmpty(id)) return true;
            foreach (char ch in id) if (ch == ';' || ch == '=') return true;
            return false;
        }

        // ------------------------------------------------------------ zapis
        internal static string Export()
        {
            var sb = new StringBuilder(64 + _gold.Count * 24);
            foreach (var kv in _gold)
            {
                if (kv.Value <= 0 || BadId(kv.Key)) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(kv.Key).Append('=').Append(kv.Value.ToString(Inv));
            }
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            _gold.Clear();
            if (!string.IsNullOrEmpty(s))
                foreach (var p in s.Split(';'))
                {
                    int eq = p.LastIndexOf('=');
                    if (eq <= 0) continue;
                    int v;
                    if (!int.TryParse(p.Substring(eq + 1), NumberStyles.Integer, Inv, out v) || v <= 0) continue;
                    string id = p.Substring(0, eq);
                    int old; _gold.TryGetValue(id, out old);
                    _gold[id] = (int)Math.Min(int.MaxValue, (long)old + v);
                }
            _p0 = TotalNow();
        }

        /// <summary>Posiadacz "sakiewki weteranow w karczmach (E5/B6)" w ksiedze pieniadza (MoneyLedger.ReadHolders) - cala pula z osieroconymi.</summary>
        internal static long TotalNow() { long t = 0; foreach (var v in _gold.Values) t += v; return t; }

        private static void Put(string id, long amount)
        {
            if (amount <= 0 || id == null) return;
            int old; _gold.TryGetValue(id, out old);
            _gold[id] = (int)Math.Min(int.MaxValue, old + amount);
        }

        // ------------------------------------------------------------ 1. zwolnieni 166
        /// <summary>
        /// ClanBudget.ReleaseFrom: `took` = czesc sakiewki partii zdjeta z odchodzacymi (`gone` ludzi - takze ci, ktorzy staneli w karczmie); `tav` - miasta karczm
        /// i ilu ludzi w nich stanelo w tym zwolnieniu (E4/10). Zwraca, ile zlota poszlo do pul weteranow (0..took) - reszte wolajacy wplaca do kiesy wsi jak dotad.
        /// Udzial weterana = took x ludzie w karczmie / odeszli (ta sama regula proporcji co dotad: sakiewka za ludzmi). Nigdy nie rzuca; przy wyjatku 0
        /// (wszystko do wsi) - wplaty do pul dopiero po policzeniu calego podzialu (bez polowicznego stanu).
        /// </summary>
        internal static int FromRelease(int took, int gone, List<KeyValuePair<Settlement, int>> tav)
        {
            if (took <= 0 || gone <= 0) return 0;
            try
            {
                int men = 0;
                if (tav != null) foreach (var kv in tav) if (kv.Key != null && kv.Value > 0) men += kv.Value;
                if (men > gone) men = gone;
                _dRelN++; _dRelTook += took; _dRelMenGone += gone; _dRelMenTav += men;
                if (!KeyOn || men <= 0) return 0;
                long toVet = (long)took * men / gone;
                if (toVet <= 0) return 0;
                // podzial na miasta (proporcjonalnie do ludzi; reszta zaokraglen - miastu z najwieksza liczba ludzi)
                var plan = new List<KeyValuePair<string, long>>(tav.Count);
                long sum = 0; int big = -1, bigN = 0;
                foreach (var kv in tav)
                {
                    if (kv.Key == null || kv.Value <= 0 || kv.Key.StringId == null) continue;
                    long a = toVet * kv.Value / men;
                    plan.Add(new KeyValuePair<string, long>(kv.Key.StringId, a));
                    sum += a;
                    if (kv.Value > bigN) { bigN = kv.Value; big = plan.Count - 1; }
                }
                if (plan.Count == 0) return 0;
                if (sum < toVet && big >= 0) { plan[big] = new KeyValuePair<string, long>(plan[big].Key, plan[big].Value + (toVet - sum)); sum = toVet; }
                // ---- wplaty (tylko slownik) - po policzeniu calego podzialu
                foreach (var kv in plan) Put(kv.Key, kv.Value);
                _dRelIn += sum;
                return (int)Math.Min(took, sum);
            }
            catch (Exception e) { Stumble("FromRelease", e); return 0; }
        }

        // ------------------------------------------------------------ 3. najem weterana: udzial w puli do sakiewki partii
        /// <summary>
        /// Veterans.OnTavernNumber (po PeopleBook.VetHire): najeci = weterani miasta przed najmem (`menBefore`) - po najmie; ich udzial w puli (pula x najeci /
        /// menBefore) idzie do sakiewki partii z okna najmu. Bez okna, bez sakiewki ludzi albo przy wylaczonym kluczu - zostaje w puli. Nigdy nie rzuca.
        /// </summary>
        internal static void OnHire(Settlement town, MobileParty party, int menBefore)
        {
            try
            {
                if (town == null || town.StringId == null || menBefore <= 0) return;
                int g; if (!_gold.TryGetValue(town.StringId, out g) || g <= 0) return;
                int m1, o1;
                int after = PeopleBook.VetInTown(town, out m1, out o1);
                int hired = menBefore - Math.Max(0, after);
                if (hired <= 0) return;
                if (hired > menBefore) hired = menBefore;
                int share = (int)((long)g * hired / menBefore);
                if (share <= 0) return;
                if (party == null || !KeyOn || !MenPurse.On) { _dHireStay += share; _dHireStayMen += hired; return; }
                // ---- od zdjecia z puli do wplaty tylko slownik i MenPurse.Add
                if (g - share > 0) _gold[town.StringId] = g - share; else _gold.Remove(town.StringId);
                MenPurse.Add(party, share);
                try
                {
                    _dHireOut += share; _dHireMen += hired;
                    if (party.IsMainParty) _dHirePlayer += share; else if (party.IsCaravan) _dHireCar += share; else _dHireLord += share;
                }
                catch { }
            }
            catch (Exception e) { Stumble("OnHire", e); }
        }

        // ------------------------------------------------------------ 4. zaplata za najem -> sakiewka najetych
        /// <summary>
        /// LevyGold (AI i karawany, MercenaryFromTavern): `fee` = oplata za najem bez konia z polki, `horseTown` = za konia z polki (zostaje miastu - 157).
        /// Zwraca, ile poszlo do sakiewki partii (0 = wolajacy wplaca do kasy miasta jak dotad). Nigdy nie rzuca.
        /// </summary>
        internal static int HireFee(MobileParty party, int fee, int horseTown)
        {
            try { if (horseTown > 0) _dHorseTown += horseTown; } catch { }
            if (fee <= 0) return 0;
            if (party == null || !FeeOn) { try { _dFeeTown += fee; } catch { } return 0; }
            try { MenPurse.Add(party, fee); }
            catch (Exception e) { Stumble("HireFee", e); try { _dFeeTown += fee; } catch { } return 0; }
            try
            {
                _dFeeN++;
                if (party.IsMainParty) _dFeePlayer += fee; else if (party.IsCaravan) _dFeeCar += fee; else _dFeeLord += fee;
            }
            catch { }
            return fee;
        }

        /// <summary>Okno zakupu najemnikow przez gracza: kiesa gracza i kasa miasta przed (gra zdejmuje zaplate z kiesy GiveGoldAction do nikogo).</summary>
        public struct PlayerFee { public bool Open; public int Gold0, Town0; public Settlement Town; }

        /// <summary>Prefiks (First) RecruitmentCampaignBehavior.BuyMercenaries / buy_mercenaries_on_consequence: migawka kiesy gracza i kasy miasta karczmy.</summary>
        public static void PlayerBuyPre(out PlayerFee __state)
        {
            __state = default(PlayerFee);
            try
            {
                if (!FeeOn || Hero.MainHero == null || MobileParty.MainParty == null) return;
                var st = Settlement.CurrentSettlement ?? TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.EncounterSettlement;   // ta sama osada co RecruitCost.PlayerBuyPrefix
                if (st == null || st.Town == null) return;
                __state = new PlayerFee { Open = true, Gold0 = Hero.MainHero.Gold, Town0 = st.Town.Gold, Town = st };
            }
            catch { __state = default(PlayerFee); }
        }

        /// <summary>
        /// Postfiks (Last - po RecruitCost.PlayerBuyPostfix: kon z polki do kasy miasta, zwrot doplaty za brakujace konie): to, co naprawde zeszlo z kiesy gracza
        /// (po zwrocie), minus to, co dostala kasa miasta (konie z polki) = oplata, ktora gra skasowala do nikogo -> sakiewka ludzi gracza (najeci); ksiega
        /// pieniadza - NoteLevyBack (gra skasowala ja przez GiveGoldAction). Nigdy nie rzuca.
        /// </summary>
        public static void PlayerBuyPost(PlayerFee __state)
        {
            if (!__state.Open) return;
            try
            {
                var hero = Hero.MainHero; var main = MobileParty.MainParty;
                if (hero == null || main == null || __state.Town == null || __state.Town.Town == null) return;
                long net = (long)__state.Gold0 - hero.Gold;
                long town = Math.Max(0L, (long)__state.Town.Town.Gold - __state.Town0);
                long fee = net - town;
                if (fee <= 0) return;
                int f = (int)Math.Min(int.MaxValue, fee);
                int put = HireFee(main, f, 0);
                if (put <= 0) return;
                MoneyLedger.NoteLevyBack(put);                 // ksiega pieniadza: gra skasowala te zaplate gracza przez GiveGoldAction (tylko licznik)
                try { Log.Player("The " + put + " denars of hire stay with the men you hired - they go into your men's purse."); } catch { }
            }
            catch (Exception e) { Stumble("PlayerBuyPost", e); }
        }

        // ------------------------------------------------------------ 2. doba: wydatek puli w miastach karczm
        /// <summary>
        /// ArmouryBehavior, nasz tick dobowy: po markietanach (MSutlers), przed budzetem rodow (166 dopisuje dzis nowe sakiewki); pozycja ksiegi MVetPurse.
        /// Kazde miasto puli: SutlerDailyShare (co najmniej 1 zl) do kasy miasta; miasto bez weteranow - cala pula; klucz wylaczony - cala pula (likwidacja);
        /// miasto Innych - najblizsze miasto ludzi; miasta nie ma w swiecie - osierocone (bez ruchu).
        /// </summary>
        internal static void Daily()
        {
            var sw = Stopwatch.StartNew();
            var s = Settings.Current;
            float rate = s != null ? MBMath.ClampFloat(s.SutlerDailyShare, 0f, 1f) : 0f;
            _dRate = rate;
            if (Campaign.Current == null || s == null || _gold.Count == 0) { sw.Stop(); _dMs += sw.Elapsed.TotalMilliseconds; return; }
            bool on = KeyOn;
            foreach (var id in _gold.Keys.ToList())
            {
                try
                {
                    int g; if (!_gold.TryGetValue(id, out g)) continue;
                    if (g <= 0) { _gold.Remove(id); continue; }
                    Settlement st = null;
                    try { st = Settlement.Find(id); } catch { st = null; }
                    if (st == null || st.Town == null) continue;                    // osierocone - zostaje w puli (linia)
                    int m1, o1;
                    int men = PeopleBook.VetInTown(st, out m1, out o1);
                    bool drain = !on || men <= 0;
                    int x = drain ? g : (rate > 0f ? Math.Max(1, Math.Min(g, (int)(g * (double)rate))) : 0);
                    if (x <= 0) continue;
                    var rec = st;
                    bool others = Veterans.IsOthersTown(st);
                    if (others) { var h = NearestHumanTown(st.GetPosition2D); if (h != null) rec = h; }
                    if (rec == null || rec.Town == null) continue;
                    // ---- od zdjecia z puli do wplaty tylko slownik i ChangeGold
                    if (g - x > 0) _gold[id] = g - x; else _gold.Remove(id);
                    rec.Town.ChangeGold(x);
                    Spend(rec, x);
                    _dSpent += x; _dSpentTowns++;
                    if (!on) _dLiquid += x; else if (men <= 0) { _dDrain += x; _dDrainN++; }
                    if (others && !ReferenceEquals(rec, st)) { _dOthers += x; _dOthersN++; }
                }
                catch (Exception e) { Stumble("Daily(miasto)", e); }   // wyjatek jednego miasta - licznik, reszta biegnie dalej
            }
            sw.Stop();
            _dMs += sw.Elapsed.TotalMilliseconds;
        }

        /// <summary>Liczniki wydatku w kasie miasta (zloto juz wplacone): tarcza zoldu (A1 wylaczone - jak "zycie" przy wyjezdzie) i E5/0 kiesa ludu na sucho ("zycie").
        /// Hak B1: tu B1 podzieli wplate 75% kupcy / 25% lud (TownFolk.Earn).</summary>
        private static void Spend(Settlement town, int x)
        {
            try { SoldierPay.Hold(town, x); } catch { }
            try { TownFolk.Note(town, x, TownFolk.KLife); } catch { }   // E5/0 (195): weterani w kasie miasta - "zycie" jak MenPurse.OnLeft (tylko licznik)
        }

        /// <summary>Najblizsze miasto, ktore nie jest miastem Innych (Veterans.IsOthersTown).</summary>
        private static Settlement NearestHumanTown(Vec2 p)
        {
            Settlement best = null; float bd = float.MaxValue;
            foreach (var t in Town.AllTowns)
            {
                var st = t != null ? t.Settlement : null;
                if (st == null || !st.IsTown) continue;
                float d = p.DistanceSquared(st.GetPosition2D);
                if (d >= bd || Veterans.IsOthersTown(st)) continue;
                bd = d; best = st;
            }
            return best;
        }

        // ------------------------------------------------------------ linia (po budzecie rodow - ta sama doba co "Budzet rodow (166)")
        internal static void Line()
        {
            var s = Settings.Current;
            try
            {
                if (s != null && s.LogEnabled && Campaign.Current != null) Log.Info(BuildLine(s));
            }
            catch (Exception e) { Stumble("Line", e); }
            _p0 = TotalNow();
            ClearDay();
        }

        private static string BuildLine(Settings s)
        {
            int today = (int)CampaignTime.Now.ToDays;
            long pool = 0, orphan = 0; int towns = 0, orphanN = 0, menAll = 0, emptyN = 0; long emptyGold = 0;
            foreach (var kv in _gold)
            {
                if (kv.Value <= 0) continue;
                pool += kv.Value;
                Settlement st = null;
                try { st = Settlement.Find(kv.Key); } catch { st = null; }
                if (st == null || st.Town == null) { orphan += kv.Value; orphanN++; continue; }
                towns++;
                int m1, o1, men = 0;
                try { men = PeopleBook.VetInTown(st, out m1, out o1); } catch { men = 0; }
                menAll += men;
                if (men <= 0) { emptyN++; emptyGold += kv.Value; }
            }
            int vetWorld = 0; try { vetWorld = PeopleBook.VetWorld; } catch { }
            bool on = KeyOn, fee = FeeOn;
            var sb = new StringBuilder(1400);
            sb.Append("Weterani - pieniadze (E5/B6): dzien ").Append(today)
              .Append(on ? (fee ? " | CZYNNE" : " | CZYNNE bez zaplaty za najem do sakiewek (" + (!MenPurse.On ? "Men Purse Enabled" : "Recruit Gold To Seller") + " wylaczone)")
                         : " | WYLACZONE (Veteran Purse Follows Man) - sakiewki zwolnionych do wsi, zaplata za najem do kas miast, pula wydana w miastach karczm");
            sb.Append(" | pula sakiewek weteranow ").Append(pool).Append(" zl w ").Append(towns).Append(" miastach (weteranow w tych miastach ").Append(menAll)
              .Append(", w karczmach razem ").Append(vetWorld).Append(menAll > 0 ? "; " + (pool - orphan) / Math.Max(1, menAll) + " zl na weterana" : "").Append(')')
              .Append(", osierocone (miasto z zapisu nie istnieje) ").Append(orphan).Append(" zl w ").Append(orphanN)
              .Append(", pule bez weteranow po dobie ").Append(emptyN).Append(" (").Append(emptyGold).Append(" zl)");
            double gp = _dRelTook > 0 ? 100.0 * _dRelIn / _dRelTook : 0.0, mp = _dRelMenGone > 0 ? 100.0 * _dRelMenTav / _dRelMenGone : 0.0;
            sb.Append(" | wejscia: sakiewki zwolnionych 166 ").Append(_dRelTook).Append(" zl w ").Append(_dRelN).Append(" zwolnieniach (ludzi ").Append(_dRelMenGone)
              .Append(", w karczmach ").Append(_dRelMenTav).Append(" = ").Append(mp.ToString("0.0", Inv)).Append("%) -> do puli ").Append(_dRelIn)
              .Append(" = ").Append(gp.ToString("0.0", Inv)).Append("%, do kies wsi ").Append(_dRelTook - _dRelIn);
            sb.Append(" | najem z puli: do sakiewek najetych ").Append(_dHireOut).Append(" zl (ludzi ").Append(_dHireMen).Append("; lordowie ").Append(_dHireLord)
              .Append(", karawany ").Append(_dHireCar).Append(", gracz ").Append(_dHirePlayer).Append("), zostalo w puli (bez okna albo sakiewki) ").Append(_dHireStay)
              .Append(" (ludzi ").Append(_dHireStayMen).Append(')');
            sb.Append(" | zaplata za najem: do sakiewek najetych ").Append(_dFeeLord + _dFeeCar + _dFeePlayer).Append(" zl w ").Append(_dFeeN).Append(" najmach (lordowie ").Append(_dFeeLord)
              .Append(", karawany ").Append(_dFeeCar).Append(", gracz ").Append(_dFeePlayer).Append("), oplata do kas miast ").Append(_dFeeTown)
              .Append(", za konie z polki do kas miast ").Append(_dHorseTown);
            sb.Append(" | wydane w miastach karczm (zycie, stawka ").Append((_dRate * 100f).ToString("0.0", Inv)).Append("%) ").Append(_dSpent).Append(" zl w ").Append(_dSpentTowns)
              .Append(" miastach, w tym pule bez weteranow (wyjazd ostatnich) ").Append(_dDrain).Append(" w ").Append(_dDrainN)
              .Append(", likwidacja przy wylaczonym kluczu ").Append(_dLiquid).Append(", miasto Innych -> najblizsze miasto ludzi ").Append(_dOthers).Append(" w ").Append(_dOthersN);
            long diff = pool - (_p0 + _dRelIn - _dHireOut - _dSpent);
            sb.Append(" | kontrola: pula wczoraj ").Append(_p0).Append(" + wejscia ").Append(_dRelIn).Append(" - najem ").Append(_dHireOut).Append(" - wydane ").Append(_dSpent)
              .Append(" = ").Append(pool).Append(" (roznica ").Append(diff).Append(')');
            sb.Append(" | potkniecia ").Append(_stumblesDay).Append(" (razem ").Append(_stumbles).Append(')')
              .Append(" | czas ").Append(_dMs.ToString("0.00", Inv)).Append(" ms.");
            return sb.ToString();
        }

        // ------------------------------------------------------------ wpiecie (SubModuleMain, po Veterans.ApplyAll i RecruitCost.ApplyAll)
        internal static void ApplyAll(Harmony h)
        {
            var done = new List<string>(); var miss = new List<string>(); var owners = new List<string>();
            foreach (var name in new[] { "BuyMercenaries", "buy_mercenaries_on_consequence" })
            {
                try
                {
                    var m = AccessTools.Method(typeof(RecruitmentCampaignBehavior), name, Type.EmptyTypes);
                    if (m == null) { miss.Add(name); continue; }
                    h.Patch(m, prefix: new HarmonyMethod(typeof(VetPurse), nameof(PlayerBuyPre)) { priority = Priority.First },
                               postfix: new HarmonyMethod(typeof(VetPurse), nameof(PlayerBuyPost)) { priority = Priority.Last });
                    done.Add(name);
                    try
                    {
                        var info = Harmony.GetPatchInfo(m);
                        if (info != null)
                        {
                            var o = info.Owners.Where(x => x != h.Id).Distinct().ToList();
                            owners.Add(name + ": " + (o.Count > 0 ? string.Join(", ", o.ToArray()) : "-"));
                        }
                    }
                    catch { }
                }
                catch (Exception e) { miss.Add(name + " (" + e.GetType().Name + ")"); }
            }
            _wiredPlayer = done.Count;
            var s = Settings.Current;
            Log.Info("VetPurse (E5/B6): pieniadze weterana i najmu - wpiete: zaplata gracza za najem do sakiewki najetych (" + (done.Count > 0 ? string.Join(", ", done.ToArray()) : "-") + ") "
                     + _wiredPlayer + "/2; zwolnieni 166 (ClanBudget.ReleaseFrom), najem AI i karawan (LevyGold), udzial w puli przy najmie (Veterans.OnTavernNumber) - wywolania wprost"
                     + "; BRAK: " + (miss.Count > 0 ? string.Join(", ", miss.ToArray()) : "-")
                     + "; wlasciciele latek celow: " + (owners.Count > 0 ? string.Join("; ", owners.ToArray()) : "-")
                     + "; ustawienia startowe przed MCM (stan gry - linia dzienna): Veteran Purse Follows Man " + (s != null && s.VeteranPurseFollowsMan ? "TAK" : "NIE")
                     + ", Men Purse Enabled " + (s != null && s.MenPurseEnabled ? "TAK" : "NIE") + ", Recruit Gold To Seller " + (s != null && s.RecruitGoldToSeller ? "TAK" : "NIE")
                     + ", stawka zycia puli (Sutler Daily Share) " + (s != null ? s.SutlerDailyShare.ToString("0.00", Inv) : "?") + ".");
        }
    }
}
