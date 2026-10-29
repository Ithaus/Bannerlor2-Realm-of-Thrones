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
    /// przy przewadze &gt;= 1 (wtedy wynik &lt;= 0.19, bez ucieczki); przewaga = sila wozu i sojusznikow / sila wroga (CalculateInitiativeScoresForEnemy). Wiesniacy maja
    /// Aggressiveness 0 - silny woz nie atakuje (attackScore 0), tylko jedzie.
    /// Zmiana (klucz VillageCartMilitiaEscort, przy Village Cart Flee Unload i Village Cart Stuck Guard): wies daje wozowi tylu ludzi swojej milicji (Settlement.Militia,
    /// wojsko kultury wsi MeleeMilitiaTroop / RangedMilitiaTroop, 2/3 i 1/3 jak SideVillageVisit), ilu trzeba, by model AI gry nie widzial zagrozenia, a sila wozu byla
    /// co najmniej sila bandy (przewaga &gt;= 1 - num4 = 0.05 w kazdej odleglosci); liczba szukana modelem gry (podwajanie, potem polowienie - kazdy krok to ten sam
    /// GetBestInitiativeBehavior co m107). Kiedy: (1) HoldCheck - wies wstrzymana, zagrozenie trwa, magazyn &gt;= W: eskorta i zdjecie wstrzymania (gra laduje i wysyla
    /// woz jak zwykle); (2) CartGuard.Home - woz z jukami i rozkazem trzymany w domu ucieczka: eskorta od razu (bez 3 dob czekania i rozladunku); (3) woz w domu z eskorta
    /// bez jukow: magazyn &gt;= W - eskorta liczona od nowa (zdjeta, gdy zagrozenia nie ma), magazyn &lt; W - ludzie wracaja do milicji. Woz z jukami i rozkazem bez ucieczki
    /// - eskorta jedzie bez zmian. Za malo milicji (albo wojska kultury brak) - nic nie zostaje przy wozie, wies czeka jak dotad. Bitwa wozu albo wsi, wies poza Normal,
    /// tratwa - bez ruchu ludzi tej doby. Klucz wylaczony - eskorty w domu wracaja do milicji, nowych nie ma (jak m107).
    /// Zamknieta ekonomia: ludzie milicji tej samej wsi do wozu i z powrotem (polegli i wzieci w niewole w drodze przepadaja jak milicja w bitwie - ksiega linii:
    /// w eskortach wczoraj + wydani - wroceni - dzis = straty &gt;= 0); towar i zloto jak u kazdego wozu (sprzedaz w miescie za kase miasta). Wiesniacy nie jedza
    /// (DefaultMobilePartyFoodConsumptionModel.DoesPartyConsumeFood: IsVillager - nie), wiec eskorta nie zjada zapasow. Rozpoznanie eskorty po rodzaju wojska
    /// (milicja dowolnej kultury w partii wiesniakow - szablony wiesniakow SandBox i ROT jej nie maja), bez zapisu: po wczytaniu ludzie wracaja tak samo.
    /// Linie doby (LogGate.Lines): "Eskorta wozow (297b)" i "Kopalnie rudy (297b)" (kopalnie zelaza i srebra: zatkane wedlug stanu wozu, zapas rudy wsi, przyklady).
    /// Wolane tylko z CartGuard.Daily (doba kampanii, watek glowny, poza tickiem AI - jak HoldCheck m107).
    /// </summary>
    internal static class CartEscort
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const int ExMax = 6;

        internal static bool On { get { var s = Settings.Current; return s != null && s.VillageCartMilitiaEscort && s.VillageCartFleeUnload && s.VillageCartStuckGuard; } }

        private static HashSet<CharacterObject> _mil;                       // milicja wszystkich kultur (bez chlopow kultur)
        private static readonly Dictionary<MobileParty, int> _setDay = new Dictionary<MobileParty, int>();   // woz -> doba nadania eskorty (bez drugiego liczenia tej doby)
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static long _taken, _back;                                   // sesja: ludzie milicji do wozow / z wozow
        private static int _outPrev = -1;                                    // ludzie w eskortach przy ostatniej linii (-1 - po wczytaniu)
        private static ItemObject _iron, _silver;
        private static bool _itemsSought;

        // liczniki doby
        private static int _tryHold, _tryFlee, _tryRe, _okHold, _okFlee, _okRe, _menHold, _menFlee, _menRe, _calm, _weak, _noMil, _evals, _backCarts, _backMen, _stumbles;
        private static long _takenDay, _backDay;

        internal static void Reset()
        {
            _mil = null; _setDay.Clear(); _errSites.Clear(); _taken = _back = 0; _outPrev = -1; _iron = _silver = null; _itemsSought = false;
            ClearDay();
        }

        private static void ClearDay()
        {
            _tryHold = _tryFlee = _tryRe = _okHold = _okFlee = _okRe = _menHold = _menFlee = _menRe = _calm = _weak = _noMil = _evals = _backCarts = _backMen = _stumbles = 0;
            _takenDay = _backDay = 0;
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
                    if (el.Character != null && el.Number > 0 && mil.Contains(el.Character)) n += el.Number;
                }
            }
            catch (Exception e) { Stumble("CartEscort.Count", e); }
            return n;
        }

        /// <summary>Wszyscy ludzie eskorty z wozu do milicji wsi domowej (ranni tez - milicja nie ma rannych). Zwraca liczbe ludzi.</summary>
        private static int Detach(MobileParty cart, Village v)
        {
            var r = cart.MemberRoster; var st = v.Settlement;
            if (r == null || st == null) return 0;
            var mil = Mil();
            var list = new List<TroopRosterElement>();
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.Character != null && el.Number > 0 && mil.Contains(el.Character)) list.Add(el);
            }
            int n = 0;
            foreach (var el in list)
            {
                r.AddToCounts(el.Character, -el.Number, false, -el.WoundedNumber);
                n += el.Number;
            }
            if (n > 0) { st.Militia += n; _back += n; _backDay += n; }
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

        /// <summary>Sklad eskorty n ludzi: 2/3 wlocznicy, 1/3 lucznicy (bez jednego rodzaju - caly drugi). Ustawia juki wozu na dokladnie ten sklad wzgledem cm / cr.</summary>
        private static void SetTo(TroopRoster r, CharacterObject mel, CharacterObject rng, int n, ref int cm, ref int cr)
        {
            int wr = mel != null && rng != null ? n / 3 : (mel == null ? n : 0);
            int wm = n - wr;
            if (mel != null && wm != cm) { r.AddToCounts(mel, wm - cm); cm = wm; }
            if (rng != null && wr != cr) { r.AddToCounts(rng, wr - cr); cr = wr; }
        }

        /// <summary>Eskorta wozu w domu (woz w osadzie wsi, bez bitwy - sprawdza wolajacy): dotychczasowa eskorta wraca do milicji, potem zagrozenie modelem gry;
        /// brak zagrozenia - true, men 0; jest - najmniejsza liczba ludzi milicji (podwajanie od 1 do calej milicji, potem polowienie), przy ktorej model nie widzi
        /// zagrozenia, a sila wozu &gt;= sila bandy, ktora go trzymala; znaleziona - ludzie z milicji do wozu, true; nie - nic przy wozie, false.
        /// why: 0 wstrzymanie m107, 1 ucieczka zaladowanego wozu, 2 odswiezenie eskorty w domu.</summary>
        internal static bool TryEscort(MobileParty cart, Village v, MobilePartyAIModel model, int why, out int men)
        {
            men = 0;
            if (why == 0) _tryHold++; else if (why == 1) _tryFlee++; else _tryRe++;
            var st = v.Settlement; var r = cart.MemberRoster;
            int had = Detach(cart, v);
            MobileParty foe; float sc;
            if (!Threat(cart, model, out foe, out sc))
            {
                _calm++;
                if (had > 0) { _backCarts++; _backMen += had; }
                _setDay.Remove(cart);
                return true;
            }
            var cu = st.Culture;
            CharacterObject mel = cu != null ? cu.MeleeMilitiaTroop : null, rng = cu != null ? cu.RangedMilitiaTroop : null;
            var mil = Mil();
            if (mel != null && !mil.Contains(mel)) mel = null;               // tylko wojsko, ktore Count / Detach rozpozna (inaczej ludzie nie wrociliby do milicji)
            if (rng != null && !mil.Contains(rng)) rng = null;
            int avail = (int)Math.Floor(st.Militia);
            if ((mel == null && rng == null) || avail < 1)
            {
                _noMil++;
                if (had > 0) { _backCarts++; _backMen += had; }
                _setDay.Remove(cart);
                return false;
            }
            float need = Str(foe);
            int cm = 0, cr = 0, lo = 0, hi = -1, n = 1;
            try
            {
                while (true)
                {
                    SetTo(r, mel, rng, n, ref cm, ref cr);
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
                        SetTo(r, mel, rng, mid, ref cm, ref cr);
                        MobileParty f3; float s3;
                        if (!Threat(cart, model, out f3, out s3) && cart.Party.EstimatedStrength >= need) hi = mid; else lo = mid;
                    }
                    SetTo(r, mel, rng, hi, ref cm, ref cr);
                }
                else SetTo(r, mel, rng, 0, ref cm, ref cr);
            }
            catch
            {
                try { SetTo(r, mel, rng, 0, ref cm, ref cr); } catch { }    // nic z niczego: przy wyjatku juki wracaja do skladu sprzed szukania
                throw;
            }
            if (hi <= 0)
            {
                _weak++;
                if (had > 0) { _backCarts++; _backMen += had; }
                _setDay.Remove(cart);
                return false;
            }
            st.Militia -= hi;
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

        /// <summary>CartGuard.Home (woz w domu, przed logika ucieczki m107): lo - juki i rozkaz jazdy. true - woz dostal eskorte pod ucieczka (wolajacy konczy epizod).</summary>
        internal static bool HomeStep(MobileParty cart, Village v, Settlement home, bool lo)
        {
            try
            {
                if (v.VillageState != Village.VillageStates.Normal || cart.MapEvent != null || cart.IsInRaftState || (home.Party != null && home.Party.MapEvent != null)) return false;
                int have = Count(cart);
                if (!On)
                {
                    if (have > 0) { int b = Detach(cart, v); _backCarts++; _backMen += b; _setDay.Remove(cart); }   // klucz wylaczony: eskorta w domu wraca do milicji
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
                else { int b = Detach(cart, v); _backCarts++; _backMen += b; _setDay.Remove(cart); }
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
                    if (Count(cart) > 0) { int b = Detach(cart, v); _backCarts++; _backMen += b; _setDay.Remove(cart); }
                }
                catch (Exception e) { Stumble("CartEscort.Sweep", e); }
            }
        }

        // ------------------------------------------------------------ linie doby
        private sealed class MineSum
        {
            public int N, Clog, Held, HomeReady, Road, NoCart, NotNormal, Escorted, Ore; public double Fill;
            public List<string> Ex = new List<string>();
        }

        private static void Mines(Village v, MobileParty cart, MineSum m, ItemObject ore)
        {
            var home = v.Settlement;
            int store = Store(home), W = Math.Max(1, v.GetWarehouseCapacity());
            int ore1 = ore != null && home.ItemRoster != null ? home.ItemRoster.GetItemNumber(ore) : 0;
            m.N++; m.Ore += ore1; m.Fill += (double)store / W;
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

        /// <summary>CartGuard.Daily (po straznku): ksiega eskort i linie doby (LogGate.Lines).</summary>
        internal static void Daily(int day, bool guardOn)
        {
            try
            {
                if (!guardOn) Sweep();
                if (!_itemsSought)
                {
                    _itemsSought = true;
                    try { var om = MBObjectManager.Instance; if (om != null) { _iron = om.GetObject<ItemObject>("iron"); _silver = om.GetObject<ItemObject>("silver"); } } catch { }
                }
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
                        if (tid == "iron_mine") Mines(v, cart, fe, _iron);
                        else if (tid == "silver_mine") Mines(v, cart, ag, _silver);
                    }
                    catch (Exception e) { Stumble("CartEscort.Daily(wies)", e); }
                }
                long lost = _outPrev >= 0 ? _outPrev + _takenDay - _backDay - out0 : -1;
                if (LogGate.Lines)
                {
                    var sb = new StringBuilder(1200);
                    sb.Append("Eskorta wozow (297b): dzien ").Append(day).Append(" | klucz Village Cart Militia Escort ").Append(On ? "TAK" : "NIE").Append(guardOn ? "" : " (straznik wylaczony - eskorty w domu wracaja)")
                      .Append(" | proby: wstrzymane m107 ").Append(_tryHold).Append(" (z eskorta ").Append(_okHold).Append(", ludzi ").Append(_menHold).Append("), ucieczka z jukami ").Append(_tryFlee)
                      .Append(" (z eskorta ").Append(_okFlee).Append(", ludzi ").Append(_menFlee).Append("), odswiezenie w domu ").Append(_tryRe).Append(" (z eskorta ").Append(_okRe).Append(", ludzi ").Append(_menRe)
                      .Append("); bez zagrozenia ").Append(_calm).Append(", za malo milicji ").Append(_weak).Append(", bez milicji albo wojska kultury ").Append(_noMil).Append(", ocen modelu ").Append(_evals)
                      .Append(" | milicja: wydana ").Append(_takenDay).Append(", wrocila ").Append(_backDay).Append(" (wozow ").Append(_backCarts).Append(", ludzi ").Append(_backMen).Append(")")
                      .Append(" | teraz w eskortach ").Append(out0).Append(" ludzi w ").Append(carts).Append(" wozach (w domu ").Append(atHome).Append(" / ").Append(menHome)
                      .Append(", w drodze ").Append(onRoad).Append(" / ").Append(menRoad).Append(", w miescie ").Append(inTown).Append(" / ").Append(menTown).Append(", gdzie indziej ").Append(elsewhere).Append(")")
                      .Append(" | ksiega: wczoraj ").Append(_outPrev >= 0 ? _outPrev.ToString(Inv) : "-").Append(" + wydana - wrocila - dzis = straty ").Append(lost >= 0 ? lost.ToString(Inv) : "od jutra")
                      .Append(" (sesja: wydana ").Append(_taken).Append(", wrocila ").Append(_back).Append(") | potkniecia ").Append(_stumbles).Append('.');
                    Log.Info(sb.ToString());
                    Log.Info("Kopalnie rudy (297b): dzien " + day + " | " + MineText("zelazo", fe) + " | " + MineText("srebro", ag) + ".");
                }
                _outPrev = out0;
            }
            catch (Exception e) { Stumble("CartEscort.Daily", e); }
            ClearDay();
        }
    }
}
