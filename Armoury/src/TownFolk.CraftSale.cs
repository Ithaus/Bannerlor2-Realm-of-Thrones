using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// N4 (noc 16.10, galaz noc-ekonomia cz. 2; raport audytu ekonomii 15.10 rozdz. 3.1, tabela problemow 11) - DOPLATA DO 35% TYLKO OD TOWARU SPRZEDANEGO Z POLKI.
    /// Klucz FolkCraftWageOnSale (wylaczony = jak przed paczka: wartosc wyrobu liczona przy wykonaniu, TownFolk.OnItemProduced).
    ///  - DOWOD: kasy miast placily ludowi 87-103 tys. zl/dobe doplaty do TownFolkCraftWageShare (35%) wartosci KAZDEGO wyrobu (TownFolk.Live.cs TownLive krok 2,
    ///    "doplata do 35%"), takze broni, ktora lezy na zapchanej polce (2 580 sztuk broni na dobe, zlom 135; test 4 lata: polki 68 -> 264 tys., potem warsztaty
    ///    staja "brak zlota"). Lud nie ma czego kupic - zloto z kas miast zostaje w kiesie.
    ///  - REGULA (kolejka wedlug koszyka w kazdym miescie): wyrob warsztatu miasta, ktory NAPRAWDE trafil na polke, idzie do puli "wykonane, niesprzedane"
    ///    koszyka (towar - wedlug przedmiotu, uzbrojenie - rodzaj i tier) z wartoscia po cenie targu w chwili wykonania. Raz na dobe (TownFolk.Daily, przed doba ludu)
    ///    liczymy sztuki koszyka na polce: gdy jest ich mniej niz w puli, roznica zeszla z polki - jej wartosc z puli (srednia) wchodzi do f.ProdV, od ktorego kasa
    ///    miasta placi doplate do 35% (ta sama kasa, ten sam wzor, te same place cyklu odliczone). Zejscie z polki na zlom z nadmiaru (174, ArmsScrap) liczy sie
    ///    najpierw i bez doplaty. Pula nie rosnie ponad polke. W PRZYBLIZENIU: wyroby warsztatow licza sie jako NAJNOWSZE na polce - towar spoza warsztatow
    ///    (lup, dostawy karawan) w tym samym koszyku schodzi w rachunku pierwszy, wiec doplata wychodzi raczej nizsza niz wyzsza (zloto zostaje w kasie miasta).
    ///  - POPRAWKA PO RECENZJI (noc 16.10): do puli tylko sztuki, ktore naprawde trafily na polke. Zdarzenie gry OnItemProduced przychodzi przy BK dwa razy
    ///    na jeden wyrob: prefiks BK ProduceOutputPrefix (wewnatrz ProduceAnOutputToTown) z liczba sztuk, ktore dopisal, potem gra drugi raz zbiorczo
    ///    (cykl notabla WorkshopsCampaignBehavior :769 - 1 na wywolanie; cykl gracza :671 - wywolania do miasta + do magazynu gracza). Do tego rzemieslnicy BK
    ///    (ArtisanInputs.OutPost) zdejmuja z polki czesc sztuk BK po zdarzeniu. Teraz: zdarzenie z wnetrza ProduceAnOutputToTown - do puli (BK, wyroby uboczne
    ///    E8/B); gra dopisala sztuke sama (oryginal biegl, __runOriginal) - 1 do puli wprost z postfiksu; zdarzenie zbiorcze gry po wywolaniu - pominiete
    ///    (cykl notabla: tyle wywolan, ile ich bylo - nie wedlug liczby sztuk BK; cykl gracza: kazde zdarzenie poza ProduceAnOutputToTown, bo magazyn gracza
    ///    nie jest polka); sztuki zdjete przez rzemieslnikow BK - z puli bez doplaty (CsDrop). Wlasne zdarzenia warsztatow zbrojnych (WorkshopLaw), strzelarzy,
    ///    rzemiosla miasta - jak dotad (prawdziwe sztuki). Kontrola: przyrost polki w kazdym wywolaniu ProduceAnOutputToTown (wszystkie modyfikatory) wobec
    ///    sztuk dopisanych do puli w tym wywolaniu - linia NE "kontrola polki", prog NE-17b (ponad przyrost 0).
    ///  - Zloto: platnik i odbiorca jak dotad (kasa miasta -> kiesa ludu W70/R30); zmienia sie tylko CHWILA i PODSTAWA (sprzedane zamiast wykonane).
    ///  - Zapis arm_craft_sale (pula wedlug miasta i koszyka); brak klucza (stary zapis, wlaczenie w trakcie) - pula od zera: towar na polce sprzed wlaczenia traktowany
    ///    jak starszy (byl juz oplacony przy wykonaniu). Zamki bez zmian (doplata liczona tylko w miastach).
    /// Wyjatek per miasto - licznik potkniec (CLAUDE.md 7), reszta liczy sie dalej.
    /// </summary>
    internal static partial class TownFolk
    {
        private sealed class CsPool { public int U; public double V; public int Gone; }

        private static readonly Dictionary<Settlement, Dictionary<string, CsPool>> _cs = new Dictionary<Settlement, Dictionary<string, CsPool>>();
        private static Dictionary<Settlement, double> _csMade = new Dictionary<Settlement, double>(), _csMadeDay = new Dictionary<Settlement, double>();
        private static readonly CultureInfo CsInv = CultureInfo.InvariantCulture;
        /// <summary>N4 (linia NE, doba): wartosc wyrobow miast wykonanych (stara podstawa) i zeszlych z polki z puli (nowa), sztuki, zlom bez doplaty, pula na koniec,
        /// doplata zaplacona i wedlug starej reguly (po przycieciu do kasy), potkniecia.</summary>
        internal static double N4MadeV, N4SoldV, N4PoolV; internal static long N4SoldN, N4GoneN, N4PoolN, N4TopUp, N4TopUpOld; internal static int N4Towns, N4Stumbles;

        // poprawka po recenzji (noc 16.10): sztuki-widma. Stan wywolania ProduceAnOutputToTown i cyklu warsztatu (tylko watek glowny - jak OnItemProduced).
        private static int _csOut, _csPlayer;                 // glebokosc ProduceAnOutputToTown i cyklu warsztatu GRACZA (TickOneProductionCycleForPlayerWorkshop)
        private static Settlement _csSt;                      // miasto biezacego wywolania (null = nie mierzone: klucz, zamek, wywolanie zagniezdzone)
        private static ItemObject _csItem;                    // wyrob z argumentu gry (przed podmiana kruszcu przez BK w kopalni)
        private static int _csShelf0, _csCallAdd;             // polka tego wyrobu przed wywolaniem (wszystkie modyfikatory); sztuki dopisane do puli w wywolaniu
        private static Settlement _csExpSt;                   // cykl notabla: miasto oczekiwanych zdarzen zbiorczych gry
        private static readonly Dictionary<ItemObject, int> _csExp = new Dictionary<ItemObject, int>();   // wyrob -> zdarzen zbiorczych gry do pominiecia (wywolan)
        private static long _wDup, _wGame, _wDrop, _wChkIn, _wChkShelf, _wChkOver, _wStale;   // okno od ostatniej doby ludu (jak _csMade)
        /// <summary>N4 (linia NE, doba): dubli zdarzen gry pominietych (szt.), sztuk dopisanych przez gre bez BK, zdjetych przez rzemieslnikow BK, kontrola polki
        /// (do puli w wywolaniach / przyrost polek / ponad przyrost), oczekiwan bez zdarzenia (na koniec cyklu). N4Wired - latki wpiete (bez nich regula jak przed poprawka).</summary>
        internal static long N4DupN, N4GameN, N4DropN, N4ChkIn, N4ChkShelf, N4ChkOver, N4StaleN;
        internal static bool N4Wired; internal static string N4Hook = "nie wpiete";

        private static bool CraftSaleOn { get { var s = Settings.Current; return s != null && s.FolkCraftWageOnSale; } }

        private static string CsKey(ItemObject it)
        {
            if (it == null) return null;
            if (ArmsLeaks.ArmsPiece(it)) return "a" + ((int)it.ItemType).ToString(CsInv) + "." + Math.Max(1, Math.Min(6, (int)it.Tier + 1)).ToString(CsInv);
            return it.StringId != null ? "i" + it.StringId : null;
        }

        private static void ResetCraftSale()
        {
            _cs.Clear(); _csMade.Clear(); _csMadeDay.Clear();
            CsDayZero(); N4PoolV = 0; N4PoolN = 0; N4Stumbles = 0;
            _csOut = _csPlayer = 0; _csSt = null; _csItem = null; _csCallAdd = 0; _csExp.Clear(); _csExpSt = null;
            _wDup = _wGame = _wDrop = _wChkIn = _wChkShelf = _wChkOver = _wStale = 0;
        }

        private static void CsDayZero()
        {
            N4MadeV = N4SoldV = 0; N4SoldN = N4GoneN = N4TopUp = N4TopUpOld = 0; N4Towns = 0;   // N4Stumbles zeruje linia NE (NightEcon)
            N4DupN = N4GameN = N4DropN = N4ChkIn = N4ChkShelf = N4ChkOver = N4StaleN = 0;
        }

        /// <summary>Sztuki wyrobu na polce (wszystkie modyfikatory - BK dopisuje bez modyfikatora, gra z modyfikatorem argumentu).</summary>
        private static int CsShelf(ItemRoster r, ItemObject it)
        {
            int n = 0;
            if (r == null || it == null) return 0;
            for (int i = 0; i < r.Count; i++) if (r.GetItemAtIndex(i) == it) n += r.GetElementNumber(i);
            return n;
        }

        private static void CsPut(Settlement st, string k, ItemObject item, int n, double v)
        {
            Dictionary<string, CsPool> m;
            if (!_cs.TryGetValue(st, out m)) { m = new Dictionary<string, CsPool>(); _cs[st] = m; }
            CsPool p;
            if (!m.TryGetValue(k, out p)) { p = new CsPool(); m[k] = p; }
            p.U += n; p.V += Math.Max(0, v);
            if (_csOut > 0 && st == _csSt && item == _csItem) _csCallAdd += n;
        }

        /// <summary>Cykl notabla: ile z count sztuk zdarzenia to zdarzenie zbiorcze gry po wywolaniu ProduceAnOutputToTown (pominiete - sztuka juz policzona).</summary>
        private static int CsTakeExp(Settlement st, ItemObject item, int count)
        {
            if (_csExp.Count == 0 || st != _csExpSt) return 0;
            int e;
            if (!_csExp.TryGetValue(item, out e) || e <= 0) return 0;
            int t = Math.Min(e, count);
            if (e - t > 0) _csExp[item] = e - t; else _csExp.Remove(item);
            return t;
        }

        /// <summary>OnItemProduced (miasto, klucz wlaczony): wyrob do puli koszyka; wartosc wykonana do porownania ze stara regula (kazde zdarzenie, z dublami gry -
        /// tak placila stara regula). Zwraca false - klucz wylaczony / nie miasto (stara droga f.ProdV).</summary>
        private static bool CsAdd(Settlement st, ItemObject item, int count, double value)
        {
            if (!CraftSaleOn || st == null || !st.IsTown) return false;
            try
            {
                string k = CsKey(item);
                if (k == null) return false;
                double d; _csMade.TryGetValue(st, out d); _csMade[st] = d + Math.Max(0, value);
                int real = count;
                if (N4Wired && _csOut <= 0)
                {
                    // zdarzenie poza ProduceAnOutputToTown: w cyklu gracza - zbiorcze gry (miasto + magazyn gracza; warsztat gracza sam nic nie dopisuje,
                    // WorkshopLaw.PlayerCyclePrefix zdarzen nie wysyla); w cyklu notabla - zbiorcze gry tyle razy, ile bylo wywolan; reszta - prawdziwe sztuki
                    int dup = _csPlayer > 0 ? count : CsTakeExp(st, item, count);
                    real -= dup; _wDup += dup;
                }
                if (real > 0) CsPut(st, k, item, real, Math.Max(0, value) * real / count);
                return true;
            }
            catch (Exception e) { N4Stumbles++; Stumble("CsAdd", e); return false; }
        }

        /// <summary>ArmsScrap (zlom z nadmiaru 174): sztuki zdjete z polki bez sprzedazy - przy liczeniu doby schodza z puli najpierw i bez doplaty. Nie rzuca.</summary>
        internal static void ShelfGone(Settlement st, ItemObject item, int n)
        {
            try
            {
                if (n <= 0 || st == null || _cs.Count == 0) return;
                Dictionary<string, CsPool> m; CsPool p;
                string k = CsKey(item);
                if (k == null || !_cs.TryGetValue(st, out m) || !m.TryGetValue(k, out p)) return;
                p.Gone += n;
            }
            catch (Exception e) { N4Stumbles++; Stumble("ShelfGone", e); }
        }

        /// <summary>ArtisanInputs.OutPost (poprawka po recenzji): rzemieslnicy BK zdjeli z polki n sztuk, ktore prefiks BK dopisal (i zglosil zdarzeniem) -
        /// schodza z puli od razu, bez doplaty (nigdy nie byly na sprzedaz). Nie rzuca.</summary>
        internal static void CsDrop(Settlement st, ItemObject item, int n)
        {
            try
            {
                if (n <= 0 || st == null || item == null || !CraftSaleOn || _cs.Count == 0) return;
                Dictionary<string, CsPool> m; CsPool p;
                string k = CsKey(item);
                if (k == null || !_cs.TryGetValue(st, out m) || !m.TryGetValue(k, out p) || p.U <= 0) return;
                int t = Math.Min(n, p.U);
                double unit = p.V / p.U;
                p.U -= t; p.V = Math.Max(0, p.V - unit * t);
                _wDrop += t;
                if (_csOut > 0 && st == _csSt && item == _csItem) _csCallAdd -= t;
            }
            catch (Exception e) { N4Stumbles++; Stumble("CsDrop", e); }
        }

        // ------------------------------------------------------------ latki (poprawka po recenzji; wpinane zawsze, klucz czytany na zywo)
        /// <summary>Prefiks Priority.First WorkshopsCampaignBehavior.ProduceAnOutputToTown - przed prefiksem BK (Normal) i przed WorkshopTrade.OutPrefix
        /// (First, wczesniej wpiety): polka wyrobu przed wywolaniem.</summary>
        public static void CsOutPre(EquipmentElement __0, Workshop __1)
        {
            _csOut++;
            try
            {
                if (_csOut != 1) return;
                _csSt = null; _csItem = null; _csCallAdd = 0; _csShelf0 = -1;
                if (!CraftSaleOn || !Run || OffMain()) return;
                var st = __1 != null ? __1.Settlement : null;
                if (st == null || !st.IsTown || st.Town == null || st.Town.Owner == null || __0.Item == null) return;
                _csSt = st; _csItem = __0.Item;
                _csShelf0 = CsShelf(st.Town.Owner.ItemRoster, __0.Item);
            }
            catch (Exception e) { N4Stumbles++; Stumble("CsOutPre", e); }
        }

        /// <summary>Postfiks Priority.First (przed WorkshopTrade.OutPostfix -> ArtisanInputs.OutPost): oryginal gry biegl (BK nie przejal wywolania) - gra
        /// dopisala 1 sztuke bez zdarzenia z wnetrza, wiec 1 do puli wprost; w cyklu notabla zdarzenie zbiorcze gry za to wywolanie - do pominiecia.</summary>
        public static void CsOutPost(bool __runOriginal)
        {
            try
            {
                if (_csOut != 1 || _csSt == null || _csItem == null) return;
                if (_csPlayer <= 0)
                {
                    if (_csExpSt != _csSt) { foreach (var v in _csExp.Values) _wStale += v; _csExp.Clear(); _csExpSt = _csSt; }
                    int e; _csExp.TryGetValue(_csItem, out e); _csExp[_csItem] = e + 1;
                }
                if (__runOriginal)
                {
                    string k = CsKey(_csItem);
                    if (k != null) { CsPut(_csSt, k, _csItem, 1, Math.Max(0, _csSt.Town.MarketData.GetPrice(_csItem))); _wGame++; }
                }
            }
            catch (Exception e) { N4Stumbles++; Stumble("CsOutPost", e); }
        }

        /// <summary>Finalizer ProduceAnOutputToTown (biegnie po wszystkich postfiksach, takze po wyjatku): kontrola polki - sztuki dopisane do puli w wywolaniu
        /// wobec przyrostu polki tego wyrobu (po zdjeciu nadwyzki BK i wyrobach ubocznych).</summary>
        public static void CsOutFin()
        {
            try
            {
                if (_csOut == 1 && _csSt != null && _csItem != null && _csShelf0 >= 0)
                {
                    int d = CsShelf(_csSt.Town.Owner.ItemRoster, _csItem) - _csShelf0;
                    _wChkIn += _csCallAdd; _wChkShelf += d;
                    if (_csCallAdd > Math.Max(0, d)) _wChkOver += _csCallAdd - Math.Max(0, d);
                }
            }
            catch (Exception e) { N4Stumbles++; Stumble("CsOutFin", e); }
            finally
            {
                if (_csOut > 0) _csOut--;
                if (_csOut == 0) { _csSt = null; _csItem = null; _csCallAdd = 0; }
            }
        }

        public static void CsPlayerPre() { _csPlayer++; }
        public static void CsPlayerFin() { if (_csPlayer > 0) _csPlayer--; }

        /// <summary>305b (PlayerArmsAsFirm): firma WorkshopLaw w cyklu warsztatu gracza wysyla OnItemProduced za kazda sztuke sama (jak w cyklu notabla) - na czas
        /// firmy cykl nie liczy sie jako cykl gracza (zdarzenie to prawdziwa sztuka, nie zbiorcze zdarzenie gry). Zwraca glebokosc do CsPlayerResume.</summary>
        internal static int CsPlayerSuspend() { int p = _csPlayer; _csPlayer = 0; return p; }
        internal static void CsPlayerResume(int p) { _csPlayer = Math.Max(0, p); }

        /// <summary>Finalizer cyklu notabla: oczekiwania bez zdarzenia zbiorczego (gra zawsze je wysyla po wywolaniu - licznik powinien stac na 0) czyszczone.</summary>
        public static void CsNotableFin()
        {
            try { if (_csExp.Count > 0) { foreach (var v in _csExp.Values) _wStale += v; _csExp.Clear(); } _csExpSt = null; }
            catch { }
        }

        /// <summary>Menu glowne (SubModuleMain, po WorkshopTrade.ApplyAll i TownFolk.ApplyAll): wpiecie latek poprawki sztuk-widm. Kolejnosc: najpierw metoda wolana
        /// (ProduceAnOutputToTown - wpieta juz wczesniej przez BK i WorkshopTrade), potem cykle, ktore ja wolaja. Bez kompletu - regula jak przed poprawka (N4Wired false).</summary>
        internal static void ApplyCraftSale(Harmony h)
        {
            var parts = new List<string>();
            bool outOk = false, notOk = false, plOk = false;
            try
            {
                var beh = typeof(WorkshopsCampaignBehavior);
                var mo = AccessTools.DeclaredMethod(beh, "ProduceAnOutputToTown");
                if (mo != null)
                {
                    h.Patch(mo, prefix: new HarmonyMethod(typeof(TownFolk), nameof(CsOutPre)) { priority = Priority.First },
                               postfix: new HarmonyMethod(typeof(TownFolk), nameof(CsOutPost)) { priority = Priority.First },
                               finalizer: new HarmonyMethod(typeof(TownFolk), nameof(CsOutFin)));
                    outOk = true;
                }
                parts.Add(outOk ? "wyrob do miasta (ProduceAnOutputToTown)" : "BRAK ProduceAnOutputToTown");
                var mn = AccessTools.DeclaredMethod(beh, "TickOneProductionCycleForNotableWorkshop");
                if (mn != null) { h.Patch(mn, finalizer: new HarmonyMethod(typeof(TownFolk), nameof(CsNotableFin))); notOk = true; }
                parts.Add(notOk ? "cykl notabla" : "BRAK cyklu notabla");
                var mp = AccessTools.DeclaredMethod(beh, "TickOneProductionCycleForPlayerWorkshop");
                if (mp != null) { h.Patch(mp, prefix: new HarmonyMethod(typeof(TownFolk), nameof(CsPlayerPre)) { priority = Priority.First }, finalizer: new HarmonyMethod(typeof(TownFolk), nameof(CsPlayerFin))); plOk = true; }
                parts.Add(plOk ? "cykl gracza" : "BRAK cyklu gracza");
            }
            catch (Exception e) { parts.Add("wyjatek " + e.GetType().Name); Log.Error("TownFolk.ApplyCraftSale", e); }
            N4Wired = outOk && notOk && plOk;
            N4Hook = (N4Wired ? "wpiete" : "NIEPELNE") + " (" + string.Join(", ", parts.ToArray()) + ")";
            Log.Info("Doplata 35% od sprzedanego (N4, poprawka po recenzji): latki " + N4Hook + " - do puli tylko sztuki, ktore trafily na polke: zdarzenie z wnetrza"
                     + " ProduceAnOutputToTown (BK) do puli, sztuka gry bez BK z postfiksu, zdarzenie zbiorcze gry pominiete, sztuki zdjete przez rzemieslnikow BK z puli;"
                     + (N4Wired ? "" : " BEZ KOMPLETU LATEK pula liczy zdarzenia jak przed poprawka;") + " klucz Folk Craft Wage On Sale czytany na zywo.");
        }

        /// <summary>
        /// TownFolk.Daily (przed doba ludu i modelem na sucho): sztuki koszykow puli na polce kazdego miasta; zeszle z polki ponad zlom - wartosc z puli do f.ProdV
        /// (podstawa doplaty do 35%). Wartosc wykonana okna do porownania (TownLive). Klucz wylaczony - pula czyszczona (wroci od zera). Nie rzuca.
        /// </summary>
        private static void CraftSaleDay()
        {
            CsDayZero();
            var t = _csMadeDay; _csMadeDay = _csMade; _csMade = t; _csMade.Clear();
            foreach (var kv in _csMadeDay) { N4MadeV += kv.Value; }
            N4DupN = _wDup; N4GameN = _wGame; N4DropN = _wDrop; N4ChkIn = _wChkIn; N4ChkShelf = _wChkShelf; N4ChkOver = _wChkOver; N4StaleN = _wStale;
            _wDup = _wGame = _wDrop = _wChkIn = _wChkShelf = _wChkOver = _wStale = 0;
            _csOut = _csPlayer = 0; _csSt = null; _csItem = null; _csCallAdd = 0; _csExp.Clear(); _csExpSt = null;   // doba ludu biegnie poza cyklami warsztatow
            if (!CraftSaleOn) { _cs.Clear(); _csMadeDay.Clear(); N4PoolV = 0; N4PoolN = 0; return; }
            double poolV = 0; long poolN = 0;
            var empty = new List<Settlement>();
            var drop = new List<string>();
            var count = new Dictionary<string, int>();
            foreach (var kv in _cs)
            {
                var st = kv.Key;
                try
                {
                    var roster = st != null ? st.ItemRoster : null;
                    if (roster == null || st.Town == null) { empty.Add(st); continue; }
                    count.Clear();
                    for (int i = 0; i < roster.Count; i++)
                    {
                        var el = roster.GetElementCopyAtIndex(i);
                        if (el.Amount <= 0) continue;
                        string k = CsKey(el.EquipmentElement.Item);
                        if (k == null || !kv.Value.ContainsKey(k)) continue;
                        int c0; count.TryGetValue(k, out c0); count[k] = c0 + el.Amount;
                    }
                    Folk f; _f.TryGetValue(st, out f);
                    double soldV = 0; long soldN = 0;
                    drop.Clear();
                    foreach (var b in kv.Value)
                    {
                        var p = b.Value;
                        int c; count.TryGetValue(b.Key, out c);
                        if (p.U > c)
                        {
                            int left = p.U - c;
                            int gone = Math.Min(left, Math.Max(0, p.Gone));
                            int sold = left - gone;
                            double unit = p.U > 0 ? p.V / p.U : 0;
                            soldV += unit * sold; soldN += sold; N4GoneN += gone;
                            p.V = Math.Max(0, p.V - unit * left); p.U = c;
                        }
                        p.Gone = 0;
                        if (p.U <= 0) drop.Add(b.Key);
                        else { poolV += p.V; poolN += p.U; }
                    }
                    foreach (var k in drop) kv.Value.Remove(k);
                    if (kv.Value.Count == 0) empty.Add(st);
                    if (soldV > 0)
                    {
                        if (f != null && f.Kind == 0) f.ProdV += soldV;
                        N4SoldV += soldV; N4SoldN += soldN; N4Towns++;
                    }
                }
                catch (Exception e) { N4Stumbles++; Stumble("CraftSaleDay", e); }
            }
            foreach (var st in empty) _cs.Remove(st);
            N4PoolV = poolV; N4PoolN = poolN;   // wartosc wykonana okna zostaje w _csMadeDay do TownLive tej doby (porownanie)
        }

        /// <summary>N1(a) (linia "Pompy - sztuczne przelewy (nekon)"): przelewy z kas miast do ludu tej doby kiesy czynnej (-1 - doba ludu dzis nie biegla).</summary>
        internal static long LastLabourTowns { get { var d = _ld; return d != null && _liveDay == (int)CampaignTime.Now.ToDays - 1 ? d.LabT : -1; } }
        internal static long LastTurnTowns { get { var d = _ld; return d != null && _liveDay == (int)CampaignTime.Now.ToDays - 1 ? d.TurnT : -1; } }
        internal static long LastTopUpTowns { get { var d = _ld; return d != null && _liveDay == (int)CampaignTime.Now.ToDays - 1 ? d.TopT : -1; } }
        internal static long LastLabourCastles { get { var d = _ld; return d != null && _liveDay == (int)CampaignTime.Now.ToDays - 1 ? d.LabC : -1; } }

        /// <summary>TownLive: wartosc wyrobow miasta wykonanych w oknie (stara podstawa doplaty) - tylko porownanie.</summary>
        private static double CsMadeOf(Settlement st)
        {
            double v;
            return st != null && _csMadeDay.TryGetValue(st, out v) ? v : 0;
        }

        // ============================================================ zapis arm_craft_sale
        /// <summary>"cs1;S,miasto;B,koszyk,sztuki,wartosc;..." - pusty: nic do zapisania.</summary>
        internal static string ExportCraftSale()
        {
            try
            {
                if (_cs.Count == 0) return "";
                var sb = new StringBuilder(4096);
                sb.Append("cs1");
                foreach (var kv in _cs)
                {
                    if (kv.Key == null || kv.Key.StringId == null || kv.Value.Count == 0) continue;
                    sb.Append(";S,").Append(kv.Key.StringId);
                    foreach (var b in kv.Value)
                    {
                        if (b.Value.U <= 0 || b.Key.IndexOf(',') >= 0 || b.Key.IndexOf(';') >= 0) continue;
                        sb.Append(";B,").Append(b.Key).Append(',').Append(b.Value.U.ToString(CsInv)).Append(',').Append(Math.Round(b.Value.V).ToString("0", CsInv));
                    }
                }
                return sb.ToString();
            }
            catch (Exception e) { Log.Error("TownFolk.ExportCraftSale", e); return ""; }
        }

        internal static void ImportCraftSale(string data)
        {
            try
            {
                _cs.Clear();
                if (string.IsNullOrEmpty(data) || !data.StartsWith("cs1", StringComparison.Ordinal)) return;
                Dictionary<string, CsPool> cur = null;
                foreach (var rec in data.Split(';'))
                {
                    var f = rec.Split(',');
                    if (f.Length >= 2 && f[0] == "S")
                    {
                        cur = null;
                        var st = Settlement.Find(f[1]);
                        if (st == null || !st.IsTown) continue;
                        if (!_cs.TryGetValue(st, out cur)) { cur = new Dictionary<string, CsPool>(); _cs[st] = cur; }
                    }
                    else if (f.Length >= 4 && f[0] == "B" && cur != null)
                    {
                        int u; double v;
                        if (int.TryParse(f[2], NumberStyles.Integer, CsInv, out u) && u > 0 && double.TryParse(f[3], NumberStyles.Float, CsInv, out v) && v >= 0 && !double.IsInfinity(v))
                            cur[f[1]] = new CsPool { U = u, V = v };
                    }
                }
                var empty = new List<Settlement>();
                foreach (var kv in _cs) if (kv.Value.Count == 0) empty.Add(kv.Key);
                foreach (var st in empty) _cs.Remove(st);
            }
            catch (Exception e) { Log.Error("TownFolk.ImportCraftSale", e); }
        }
    }
}
