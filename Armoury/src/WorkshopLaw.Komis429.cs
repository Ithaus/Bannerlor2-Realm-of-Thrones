using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 429 (audyt ekonomii 11.11, A5; decyzja Jeffa 2 "tak") - BRON W KOMIS. Klucz ArmsConsign (NIE = kod jak przed 429: gotowa sztuka czeka w warsztacie).
    ///  - DOWOD: linia "Warsztaty: dzien" - "brak zlota/kupca" 1049-1214 cykli na dobe przez cale sesje (gotowa sztuka czeka, bo wolna kasa miasta - kasa minus
    ///    zapas kupcow - jest nizsza od ceny sztuki albo warsztat nie ma kapitalu na place), a panowie chca wydac ok. 112 tys. zl/d i 94% zostaje ("bez towaru").
    ///  - REGULA: gotowa sztuka, ktorej miasto nie moze dzis kupic, idzie na polke miasta W KOMIS: zadne zloto nie rusza, warsztat ma zapis (sztuka, cena umowiona
    ///    = dzisiejsza cena kupca, place cyklu). Raz na dobe w kazdym miescie (przed pierwszym cyklem linii zbrojnej tego miasta, w tym samym miejscu doby, co
    ///    dotychczasowa sprzedaz warsztat -> miasto) liczymy sztuki tego przedmiotu na polce: gdy jest ich mniej niz zapisow, roznica zeszla z polki (kupil ja
    ///    lord, zaloga, notabl, gracz, karawana - zloto kupujacego jest juz w kasie miasta). Za kazda taka sztuke kasa miasta placi warsztatowi min(cena umowiona,
    ///    dzisiejsza cena kupca) - prowizja kupca zostaje w kasie jak dotad (WorkshopSellShare) - a warsztat z tej zaplaty placi rzemieslnikom place cyklu
    ///    (TownFolk.Earn, ta sama droga co dotad). Zaplata nie czeka na "nadwyzke ponad zapas kupcow" (to pieniadz kupujacego, nie kupcow), ale nigdy nie
    ///    sciaga kasy ponizej zera: gdy kasa jest nizsza od naleznosci, zapis czeka jako "sprzedane, niezaplacone" i idzie pierwszy nastepnej doby.
    ///  - W PRZYBLIZENIU (to samo co TownFolk.CraftSale N4): sztuki w komisie licza sie jako OSTATNIE na polce - wlasny towar miasta tego samego przedmiotu
    ///    schodzi w rachunku pierwszy, wiec warsztat dostaje zaplate raczej pozniej niz wczesniej (zloto zostaje w kasie miasta, nigdy odwrotnie).
    ///  - ZABEZPIECZENIA: limit zapisow na miasto i koszyk (rodzaj x tier; ArmsConsignPerBasket) i na miasto (ArmsConsignPerTown) - ponad limit sztuka czeka
    ///    w warsztacie jak dotad; warsztat placi za surowiec z gory, wiec jego kapital sam hamuje liczbe sztuk w komisie; sztuka na polce obniza cene koszyka
    ///    (SupplyDemand) i bramka zysku nowych sztuk dziala jak dotad; zaplata nie wyzsza niz dzisiejsza cena kupca (spadek cen niesie warsztat, nie kasa).
    ///  - ZLOM (ArmsScrap, 174): sztuki zdjete z polki na zlom licza sie najpierw (jak w N4) - warsztat dostaje za nie cene wyprzedazy (nizej), nie pelna.
    ///  - NIESPRZEDANA: po ArmsConsignUnsoldDays dobach kupiec przejmuje sztuke za ArmsConsignUnsoldPricePercent % dzisiejszej ceny kupca (historycznie stare
    ///    kolczugi szly za 7-14% nowej); sztuka zostaje na polce jako wlasny towar miasta (dalej rzadzi nia zlom z nadmiaru 174) - towar nie znika.
    ///  - KSIEGI: towar - sztuka trafia na polke ta sama droga co dotad (OnItemProduced, Measure174b.NoteArrival, ArmsBook), nic wiecej sie nie rusza;
    ///    zloto - kasa miasta -> kapital warsztatu -> (place) kiesa ludu i kasa miasta, te same wpisy MoneyLedger.NShop i TownFolk.KCraftPaid co przy
    ///    zwyklej sprzedazy. Linia doby "Komis broni (429)" z trzema kontrolami zamkniecia (sztuki, naleznosci, zloto - kazda ma dac 0).
    ///  - ZAPIS: klucz "arm_komis429" (SaveText.Sync). Brak klucza (stary zapis) - stan pusty. Starsza DLL klucz pomija (sztuki zostaja na polkach jako
    ///    towar miasta, warsztaty nie dostana zaplaty - nic nie powstaje z niczego). Warsztat rozpoznawany po osadzie i miejscu na liscie miasta.
    ///  - Warsztat GRACZA (305b, PlayerFirm) poza regula - jak dotad (odlozone). Wyjatek per miasto / per zapis - licznik potkniec, reszta liczy sie dalej.
    /// </summary>
    internal static partial class WorkshopLaw
    {
        private sealed class KLot
        {
            public Workshop W; public ItemObject Item; public int Rev, Wages, Day;
            public bool Gone;      // zeszla z polki (albo przejeta) - czeka tylko na zaplate Due
            public int Due;        // naleznosc po zejsciu z polki
        }

        private static readonly Dictionary<Town, List<KLot>> _kLots = new Dictionary<Town, List<KLot>>();
        private static readonly Dictionary<Town, int> _kDay = new Dictionary<Town, int>();                                   // doba ostatniego rozliczenia miasta
        private static readonly Dictionary<Town, Dictionary<ItemObject, int>> _kScrap = new Dictionary<Town, Dictionary<ItemObject, int>>();   // zlom od ostatniego rozliczenia
        private static string _kPending;
        private static bool _kErrLogged;
        // liczniki doby (zerowane w KomisFlush)
        private static int _kIn, _kInTown, _kInWage, _kInBoth, _kCapBasket, _kCapTown, _kSold, _kScrapN, _kExpired, _kPaidN, _kNoPayee, _kStumbles, _kTownsDone, _kFree;
        private static long _kInRev, _kInWages, _kPaid, _kWagesPaid, _kFolk, _kPriceCut, _kDueNew, _kNoPayeeGold, _kGoldCheck;
        // stan na poczatku doby (do kontroli zamkniecia)
        private static long _kShelf0, _kDueN0, _kDueG0;

        internal static bool KomisOn { get { var s = Settings.Current; return s != null && s.ArmsConsign; } }
        private static int KCapBasket { get { var s = Settings.Current; return s == null ? 6 : Math.Max(1, Math.Min(50, s.ArmsConsignPerBasket)); } }
        private static int KCapTown { get { var s = Settings.Current; return s == null ? 80 : Math.Max(1, Math.Min(1000, s.ArmsConsignPerTown)); } }
        private static int KUnsoldDays { get { var s = Settings.Current; return s == null ? 91 : Math.Max(7, Math.Min(728, s.ArmsConsignUnsoldDays)); } }
        private static float KUnsoldShare { get { var s = Settings.Current; return s == null ? 0.10f : MBMath.ClampFloat(s.ArmsConsignUnsoldPricePercent / 100f, 0f, 1f); } }

        private static void KomisReset()
        {
            _kLots.Clear(); _kDay.Clear(); _kScrap.Clear(); _kPending = null; _kErrLogged = false;
            KomisZero();
            _kShelf0 = _kDueN0 = _kDueG0 = 0;
        }

        private static void KomisZero()
        {
            _kIn = _kInTown = _kInWage = _kInBoth = _kCapBasket = _kCapTown = _kSold = _kScrapN = _kExpired = _kPaidN = _kNoPayee = _kStumbles = _kTownsDone = _kFree = 0;
            _kInRev = _kInWages = _kPaid = _kWagesPaid = _kFolk = _kPriceCut = _kDueNew = _kNoPayeeGold = _kGoldCheck = 0;
        }

        private static void KStumble(string where, Exception e)
        {
            _kStumbles++;
            if (!_kErrLogged) { _kErrLogged = true; Log.Error("WorkshopLaw.Komis429." + where, e); }
        }

        /// <summary>Stan teraz: zapisy na polce (sztuki), sprzedane niezaplacone (sztuki, zloto), cena umowiona sztuk na polce, miasta ze stanem, najstarsza doba.</summary>
        private static void KState(out long shelf, out long dueN, out long dueG, out long shelfRev, out int towns, out int oldest)
        {
            shelf = dueN = dueG = shelfRev = 0; towns = 0; oldest = int.MaxValue;
            foreach (var kv in _kLots)
            {
                if (kv.Value == null || kv.Value.Count == 0) continue;
                towns++;
                foreach (var l in kv.Value)
                {
                    if (l.Gone) { dueN++; dueG += l.Due; }
                    else { shelf++; shelfRev += l.Rev; if (l.Day < oldest) oldest = l.Day; }
                }
            }
        }

        // ------------------------------------------------------------ przyjecie sztuki w komis (z CyclePrefix)
        /// <summary>Czy gotowa sztuka moze isc na polke w komis: klucz, nie warsztat gracza, limity miasta i koszyka. Sam odczyt i liczniki odmowy.</summary>
        private static bool KomisAccept(Workshop workshop, Town town, ItemObject it)
        {
            try
            {
                if (!KomisOn || _plFirmCall || workshop == null || town == null || it == null) return false;
                if (_kPending != null) KomisResolve("pierwsza sztuka przed startem sesji");
                List<KLot> list;
                if (!_kLots.TryGetValue(town, out list) || list == null) return true;
                int b = BasketOf(it), inB = 0, inT = 0;
                foreach (var l in list) { if (l.Gone) continue; inT++; if (BasketOf(l.Item) == b) inB++; }
                if (inB >= KCapBasket) { _kCapBasket++; return false; }
                if (inT >= KCapTown) { _kCapTown++; return false; }
                return true;
            }
            catch (Exception e) { KStumble("Accept", e); return false; }
        }

        /// <summary>Zapis sztuki oddanej w komis (sztuka juz na polce). Nie rzuca.</summary>
        private static void KomisAdd(Workshop workshop, Town town, ItemObject it, int rev, int wages, int day, bool townShort, bool wageShort)
        {
            try
            {
                List<KLot> list;
                if (!_kLots.TryGetValue(town, out list) || list == null) { list = new List<KLot>(); _kLots[town] = list; }
                list.Add(new KLot { W = workshop, Item = it, Rev = Math.Max(0, rev), Wages = Math.Max(0, wages), Day = day });
                _kIn++; _kInRev += Math.Max(0, rev); _kInWages += Math.Max(0, wages);
                if (townShort && wageShort) _kInBoth++; else if (townShort) _kInTown++; else _kInWage++;
            }
            catch (Exception e) { KStumble("Add", e); }
        }

        /// <summary>ArmsScrap: n sztuk przedmiotu zdjetych z polki miasta na zlom (bez sprzedazy) - przy rozliczeniu licza sie najpierw, po cenie wyprzedazy. Nie rzuca.</summary>
        internal static void KomisScrapped(Town town, ItemObject it, int n)
        {
            try
            {
                List<KLot> list;
                if (n <= 0 || town == null || it == null || !_kLots.TryGetValue(town, out list) || list == null || list.Count == 0) return;
                Dictionary<ItemObject, int> d;
                if (!_kScrap.TryGetValue(town, out d)) { d = new Dictionary<ItemObject, int>(); _kScrap[town] = d; }
                int v; d.TryGetValue(it, out v); d[it] = v + n;
            }
            catch (Exception e) { KStumble("Scrapped", e); }
        }

        // ------------------------------------------------------------ rozliczenie miasta (raz na dobe, z CyclePrefix - przed pierwszym cyklem linii zbrojnej miasta)
        private static void KomisSettle(Town town, int day)
        {
            if (_kPending != null) KomisResolve("pierwszy cykl przed startem sesji");
            if (_kLots.Count == 0 || town == null) return;
            List<KLot> list;
            if (!_kLots.TryGetValue(town, out list) || list == null || list.Count == 0) return;
            int last;
            if (_kDay.TryGetValue(town, out last) && last == day) return;
            _kDay[town] = day;
            _kTownsDone++;
            try
            {
                var shelf = town.Owner != null ? town.Owner.ItemRoster : null;
                if (shelf == null) return;
                // zapisy na polce wedlug przedmiotu
                var want = new Dictionary<ItemObject, int>();
                foreach (var l in list) { if (l.Gone || l.Item == null) continue; int k; want.TryGetValue(l.Item, out k); want[l.Item] = k + 1; }
                if (want.Count > 0)
                {
                    // sztuki tych przedmiotow na polce (wszystkie stany; GetItemNumber gry liczy tylko pierwszy stos)
                    var have = new Dictionary<ItemObject, int>();
                    for (int i = 0; i < shelf.Count; i++)
                    {
                        var el = shelf.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (it == null || el.Amount <= 0 || !want.ContainsKey(it)) continue;
                        int h; have.TryGetValue(it, out h); have[it] = h + el.Amount;
                    }
                    Dictionary<ItemObject, int> scr;
                    _kScrap.TryGetValue(town, out scr);
                    int unsoldDays = KUnsoldDays; float unsoldShare = KUnsoldShare;
                    foreach (var kv in want)
                    {
                        try
                        {
                            int h; have.TryGetValue(kv.Key, out h);
                            int gone = kv.Value - h;
                            int scrapN = 0;
                            if (gone > 0 && scr != null) { int sc; if (scr.TryGetValue(kv.Key, out sc)) scrapN = Math.Min(gone, sc); }
                            bool anyOld = false;
                            if (gone <= 0)
                            {
                                foreach (var l in list) if (!l.Gone && l.Item == kv.Key && day - l.Day >= unsoldDays) { anyOld = true; break; }
                                if (!anyOld) continue;
                            }
                            int now = Math.Max(0, MBRandom.RoundRandomized(Revenue(town, kv.Key)));   // dzisiejsza cena kupca (polka juz bez sprzedanych sztuk)
                            int cheap = (int)Math.Floor(now * unsoldShare);
                            // najstarsze zapisy pierwsze: zlom, potem sprzedane, potem przeterminowane
                            foreach (var l in list)
                            {
                                if (l.Gone || l.Item != kv.Key) continue;
                                if (gone > 0)
                                {
                                    gone--;
                                    if (scrapN > 0) { scrapN--; l.Due = Math.Min(l.Rev, cheap); _kScrapN++; }
                                    else { l.Due = Math.Min(l.Rev, now); _kSold++; if (now < l.Rev) _kPriceCut += l.Rev - now; }
                                    l.Gone = true; _kDueNew += l.Due;
                                }
                                else if (day - l.Day >= unsoldDays)
                                {
                                    l.Due = Math.Min(l.Rev, cheap); l.Gone = true; _kExpired++; _kDueNew += l.Due;   // sztuka zostaje na polce jako towar miasta
                                }
                            }
                        }
                        catch (Exception e) { KStumble("Settle(przedmiot)", e); }
                    }
                }
                if (_kScrap.Count > 0) _kScrap.Remove(town);
                // wyplata: najstarsze pierwsze, dopoki kasa miasta starcza na cala naleznosc
                var st = town.Settlement;
                for (int i = 0; i < list.Count; i++)
                {
                    var l = list[i];
                    if (!l.Gone) continue;
                    try
                    {
                        if (l.Due <= 0) { _kFree++; list.RemoveAt(i--); continue; }
                        if (l.W == null || l.W.Settlement != st) { _kNoPayee++; _kNoPayeeGold += l.Due; list.RemoveAt(i--); continue; }   // warsztatu juz nie ma - naleznosc zostaje w kasie miasta
                        if (town.Gold < l.Due) break;   // kolejka: starsza naleznosc przed mlodsza
                        int pay = l.Due, wages = Math.Min(l.Wages, pay);
                        long g0 = town.Gold, c0 = l.W.Capital;
                        town.ChangeGold(-pay);
                        l.W.ChangeGold(pay);
                        int folk = 0;
                        if (wages > 0)
                        {
                            l.W.ChangeGold(-wages);
                            folk = TownFolk.Earn(st, wages, TownFolk.KCraftPaid);   // place cyklu do kiesy ludu W70/R30 - jak przy zwyklej sprzedazy
                            town.ChangeGold(wages - folk);                           // place wydane w miescie
                            MoneyLedger.Note(MoneyLedger.NShop, st, wages - folk);
                        }
                        MoneyLedger.Note(MoneyLedger.NShop, st, -pay);
                        if (wages > 0) { try { TownFolk.Note(st, wages, TownFolk.KCraftPaid); } catch { } }
                        _kPaidN++; _kPaid += pay; _kWagesPaid += wages; _kFolk += folk;
                        _kGoldCheck += (town.Gold - g0) + (l.W.Capital - c0) + folk;   // kasa miasta + kapital warsztatu + kiesa ludu = 0
                        list.RemoveAt(i--);
                    }
                    catch (Exception e) { KStumble("Settle(wyplata)", e); }
                }
                if (list.Count == 0) { _kLots.Remove(town); _kDay.Remove(town); }
            }
            catch (Exception e) { KStumble("Settle", e); }
        }

        // ------------------------------------------------------------ linia doby (z Flush - doba poprzednia)
        private static void KomisFlush(int day)
        {
            try
            {
                long shelf, dueN, dueG, shelfRev; int towns, oldest;
                KState(out shelf, out dueN, out dueG, out shelfRev, out towns, out oldest);
                bool on = KomisOn;
                if (on || shelf + dueN > 0 || _kIn + _kPaidN + _kSold + _kScrapN + _kExpired > 0)
                {
                    long cPieces = _kShelf0 + _kIn - _kSold - _kScrapN - _kExpired - shelf;
                    long goneN = (long)_kSold + _kScrapN + _kExpired;
                    long cDueN = _kDueN0 + goneN - _kPaidN - _kNoPayee - _kFree - dueN;
                    long cDueG = _kDueG0 + _kDueNew - _kPaid - _kNoPayeeGold - dueG;
                    var sb = new StringBuilder();
                    sb.Append("Komis broni (429): dzien ").Append(day).Append(" - klucz ").Append(on ? "TAK" : "NIE (nowych sztuk nie przyjmuje, stare rozlicza)")
                      .Append(" | na polke w komis ").Append(_kIn).Append(" szt. (cena umowiona ").Append(_kInRev).Append(" zl, place odroczone ").Append(_kInWages)
                      .Append(" zl; powod: kasa miasta ").Append(_kInTown).Append(", kapital na place ").Append(_kInWage).Append(", oba ").Append(_kInBoth)
                      .Append(") | odmowy - sztuka czeka w warsztacie jak dotad: limit koszyka ").Append(KCapBasket).Append(" - ").Append(_kCapBasket).Append(" cykli, limit miasta ")
                      .Append(KCapTown).Append(" - ").Append(_kCapTown).Append(" cykli | zeszlo z polki: sprzedane ").Append(_kSold).Append(", zlom (cena wyprzedazy) ").Append(_kScrapN)
                      .Append(", niesprzedane po ").Append(KUnsoldDays).Append(" dobach przejete przez kupca za ").Append((KUnsoldShare * 100f).ToString("0", CultureInfo.InvariantCulture))
                      .Append("% ").Append(_kExpired).Append(" | wyplacono warsztatom ").Append(_kPaid).Append(" zl za ").Append(_kPaidN).Append(" szt. (z tego place rzemieslnikow ")
                      .Append(_kWagesPaid).Append(" zl, w tym wprost do kiesy ludu ").Append(_kFolk).Append(" zl; cena dnia nizsza od umowionej o ").Append(_kPriceCut)
                      .Append(" zl - zostalo w kasach) | sprzedane, czeka na zaplate (kasa miasta ponizej naleznosci) ").Append(dueN).Append(" szt. / ").Append(dueG)
                      .Append(" zl; bez zaplaty (naleznosc 0) ").Append(_kFree).Append(", bez odbiorcy (warsztatu nie ma - zostaje w kasie miasta) ").Append(_kNoPayee).Append(" szt. / ")
                      .Append(_kNoPayeeGold).Append(" zl | stan: w komisie na polkach ").Append(shelf).Append(" szt. w ").Append(towns).Append(" miastach (cena umowiona ").Append(shelfRev)
                      .Append(" zl, najstarsza ").Append(shelf > 0 && oldest != int.MaxValue ? (day - oldest).ToString(CultureInfo.InvariantCulture) : "0").Append(" dob), miast rozliczonych dzis ")
                      .Append(_kTownsDone).Append(" | KONTROLA sztuk (start ").Append(_kShelf0).Append(" + przyjete - sprzedane - zlom - przejete - stan): roznica ").Append(cPieces)
                      .Append("; KONTROLA naleznosci (start ").Append(_kDueN0).Append(" szt. / ").Append(_kDueG0).Append(" zl + nowe - wyplacone - bez odbiorcy - stan): roznica ")
                      .Append(cDueN).Append(" szt. / ").Append(cDueG).Append(" zl; KONTROLA zlota wyplat (kasy miast + kapital warsztatow + kiesy ludu): roznica ").Append(_kGoldCheck)
                      .Append(" | potkniecia ").Append(_kStumbles).Append('.');
                    Log.Info(sb.ToString());
                }
                KomisZero();
                _kShelf0 = shelf; _kDueN0 = dueN; _kDueG0 = dueG;
            }
            catch (Exception e) { KStumble("Flush", e); }
        }

        // ------------------------------------------------------------ zapis gry: klucz arm_komis429
        // Rekord (~): osada|miejsce warsztatu na liscie miasta|przedmiot|cena umowiona|place|doba|zeszla 0/1|naleznosc
        internal static string KomisExport()
        {
            try
            {
                if (_kPending != null) KomisResolve("zapis przed startem sesji");
                var sb = new StringBuilder();
                int n = 0, skip = 0;
                var inv = CultureInfo.InvariantCulture;
                foreach (var kv in _kLots)
                {
                    var t = kv.Key;
                    if (t == null || t.Settlement == null || kv.Value == null) continue;
                    string sid = t.Settlement.StringId;
                    foreach (var l in kv.Value)
                    {
                        if (l.Item == null || string.IsNullOrEmpty(sid) || string.IsNullOrEmpty(l.Item.StringId) || l.Item.StringId.IndexOf('|') >= 0 || l.Item.StringId.IndexOf('~') >= 0) { skip++; continue; }
                        int ix = -1;
                        try { if (l.W != null && t.Workshops != null) ix = Array.IndexOf(t.Workshops, l.W); } catch { }
                        sb.Append(sid).Append('|').Append(ix.ToString(inv)).Append('|').Append(l.Item.StringId).Append('|').Append(l.Rev.ToString(inv)).Append('|')
                          .Append(l.Wages.ToString(inv)).Append('|').Append(l.Day.ToString(inv)).Append('|').Append(l.Gone ? '1' : '0').Append('|').Append(l.Due.ToString(inv)).Append('~');
                        n++;
                    }
                }
                if (n + skip > 0) Log.Info("Komis broni (zapis 429): zapis gry - zapisow " + n + " (pominietych " + skip + "), " + sb.Length + " znakow.");
                return sb.ToString();
            }
            catch (Exception e) { Log.Error("WorkshopLaw.KomisExport", e); return ""; }
        }

        /// <summary>Z SyncData: tylko zapamietanie - rozwiazanie w KomisResolve (OnSessionLaunched albo pierwsze uzycie).</summary>
        internal static void KomisImport(string s)
        {
            _kLots.Clear(); _kDay.Clear(); _kScrap.Clear();
            _kShelf0 = _kDueN0 = _kDueG0 = 0;
            _kPending = string.IsNullOrEmpty(s) ? null : s;
        }

        internal static void KomisResolve(string why)
        {
            var s = _kPending;
            _kPending = null;
            if (string.IsNullOrEmpty(s)) return;
            int ok = 0, bad = 0, noShop = 0;
            var om = MBObjectManager.Instance;
            var inv = CultureInfo.InvariantCulture;
            foreach (var rec in s.Split('~'))
            {
                if (rec.Length == 0) continue;
                try
                {
                    var a = rec.Split('|');
                    if (a.Length < 8) { bad++; continue; }
                    Settlement st = null; ItemObject it = null;
                    try { st = om.GetObject<Settlement>(a[0]); } catch { }
                    try { it = om.GetObject<ItemObject>(a[2]); } catch { }
                    var town = st != null ? st.Town : null;
                    int ix, rev, wages, day, due;
                    if (town == null || it == null || !int.TryParse(a[1], NumberStyles.Integer, inv, out ix) || !int.TryParse(a[3], NumberStyles.Integer, inv, out rev)
                        || !int.TryParse(a[4], NumberStyles.Integer, inv, out wages) || !int.TryParse(a[5], NumberStyles.Integer, inv, out day)
                        || !int.TryParse(a[7], NumberStyles.Integer, inv, out due)) { bad++; continue; }
                    Workshop w = town.Workshops != null && ix >= 0 && ix < town.Workshops.Length ? town.Workshops[ix] : null;
                    if (w == null) noShop++;
                    List<KLot> list;
                    if (!_kLots.TryGetValue(town, out list) || list == null) { list = new List<KLot>(); _kLots[town] = list; }
                    list.Add(new KLot { W = w, Item = it, Rev = Math.Max(0, rev), Wages = Math.Max(0, wages), Day = day, Gone = a[6] == "1", Due = Math.Max(0, due) });
                    ok++;
                }
                catch (Exception e) { bad++; KStumble("Resolve", e); }
            }
            long shelf, dueN, dueG, shelfRev; int towns, oldest;
            KState(out shelf, out dueN, out dueG, out shelfRev, out towns, out oldest);
            _kShelf0 = shelf; _kDueN0 = dueN; _kDueG0 = dueG;
            Log.Info("Komis broni (zapis 429): " + why + " - wczytano zapisow " + ok + " (na polkach " + shelf + " szt. za " + shelfRev + " zl w " + towns + " miastach, sprzedane niezaplacone "
                     + dueN + " szt. / " + dueG + " zl), bez warsztatu (naleznosc zostanie w kasie miasta) " + noShop + ", nieczytelnych " + bad + ".");
        }
    }
}
