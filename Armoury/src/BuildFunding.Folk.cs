using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// P2 (OBIEG KIESY LUDU, docs/kiesa-ludu/PROJEKT-OBIEG-KIESY-LUDU-2026-10-13.md rozdz. 3.1 szczebel 3, 3.2, 5, 6, 7, 8 wiersz P2; PROJEKT-INWESTYCJE B1 BEZ
    /// trzeciej pana; decyzja Jeffa 13.10 z doprecyzowaniem) - LUD BUDUJE MIASTO: drugi platnik budow. Klucz FolkBuildsTown (wylaczony = stan sprzed P2 co do monety:
    /// roboty gminy "na niby" w kroku 4 doby ludu, bez sufitu ceny materialu).
    ///  - CO LUD BUDUJE (Jeff): tylko budowle SLUZACE MIASTU - mury (fortyfikacje miasta i zamku), spichlerz, targ, port i przystan, warsztaty, magazyn, drogi,
    ///    kopalnie, wodociagi, sad, teatr, rzemieslnicy i podobne (FolkPays). Budowle wojskowe i pana (koszary, plac cwiczen, straznica, warsztat oblezniczy,
    ///    urzad kasztelana, urzad podatkowy, koszary druzyny BK, zbrojownia BK, stadnina koni bojowych BK) placi pan jak dotad. Gdy czolo kolejki jest wojskowe,
    ///    lud doklada do najblizszej "swojej" budowli w kolejce; w osadzie AI (kolejka gry ma jedna budowle) - do swojej budowli najblizszej konca poziomu.
    ///  - SKAD: gmina (budzet robot z akcyzy - 2/3 z 7% jej kiesy, krok 4 doby ludu) najpierw, potem kazda klasa FolkWorksShare (2%) nadwyzki ponad zapas dni
    ///    po daninie P1 i ponad oferte na jutro (TownFolk.WorksPurse). Zamek - podzamcze. Wojna NIE wstrzymuje robot ludu (ich pieniadz, ich miasto), oblezenie tak.
    ///  - GRANICE: material z polki targu (miasto - swoj, zamek - najblizsze miasto, jak pan), sztuka najwyzej BuildMaterialPriceCap x wartosc (sufit takze dla pana),
    ///    narzedzia lud zostawia na polce 5 sztuk (wsie i warsztaty), drewno 50 (dymarki i warsztaty), sztuki do 5 zl w wozach po 10 (jedna wycena na woz);
    ///    place najwyzej 3 x material
    ///    (BuildMaterialShare 25%) i najwyzej FolkWorksHands x glowy wyrobnikow x dniowka (pulap rak); punkty najwyzej reszta biezacego poziomu (z punktami pana
    ///    tej doby). Brak budowy, materialu albo nadwyzki - bez robot, pieniadz zostaje. Czego budowa nie wziela z budzetu gminy - place robot publicznych W/R.
    ///  - PUNKTY: ta sama funkcja co u pana (PointsFor; cena punktu cywilnego / wojskowego - mury 48). Czolo kolejki - moc budowy tej doby (_folk obok _funded,
    ///    PowerPostfix - pozycja "Town works (paid by the townsfolk)"; lenna gracza bez PaidConstructionPlayer - dopisane do mocy gry). Budowla dalej w kolejce
    ///    albo poza nia - postep wprost (poziom: Building.LevelUp jak gra, wpis wychodzi z kolejki).
    ///  - Platnik -> odbiorca: kiesy ludu -> kasa targu (material, sztuki zdjete przez ShelfBuy.Take - nic bez towaru), kiesy ludu -> kiesy W 60% / R 40% tej osady
    ///    (place, zarobek okna). Zadnej trzeciej dla pana (decyzja Jeffa); farma gminy 1/3 jak dotad.
    /// Linia doby "Roboty ludu (P2): dzien N". Zapis gry: nic nowego. Kazda osada we wlasnym try, licznik potkniec.
    /// </summary>
    internal static partial class BuildFunding
    {
        private static readonly string[] LordOnly = { "building_settlement_siege_workshop", "building_settlement_tax_office", "building_castle_retinue", "bk_building_armory", "bk_building_castle_studs" };
        private static readonly string[] WallIds = { "building_settlement_fortifications", "building_castle_fortifications" };
        private const int ToolsKeep = 5, WoodKeep = 50, WagonN = 10, WagonPrice = 5;   // WoodKeep: drewno dla dymarek i warsztatow (DECYZJA - CHANGELOG P2)
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly Dictionary<Town, float> _folk = new Dictionary<Town, float>();   // punkty ludu na czolo kolejki - moc budowy tej doby (PowerPostfix)
        private static TextObject _folkTxt;
        private sealed class WorkRec { public long Mat, Wage, FromG; public float Pts; public string Bld; public int Why; public int Where; public int Day; }
        private static readonly Dictionary<Settlement, WorkRec> _rec = new Dictionary<Settlement, WorkRec>();
        private static readonly TownFolk.WorksPay _pay = new TownFolk.WorksPay();
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static int _stumbles;
        // licznik doby (linia)
        private static int _capLord, _capFolk;   // sztuki pominiete przez sufit ceny (pan / lud) - liczone przy przegladzie towaru
        private const int WhyBuilt = 0, WhyNone = 1, WhySiege = 2, WhyNoMat = 3, WhyNoCoin = 4, WhyLevel = 5, WhyMilOnly = 6, WhyOff = 7;

        internal static bool FolkOn { get { var s = Settings.Current; return s != null && s.FolkBuildsTown && s.PaidConstruction; } }

        private static void FolkStumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("BuildFunding.Folk." + where, e); } catch { }
        }

        /// <summary>Budowla, za ktora placi lud (Jeff 13.10: sluzaca miastu): mury i budowle cywilne; bez wojskowych i budowli pana (LordOnly). Projekt dzienny - nie.</summary>
        internal static bool FolkPays(BuildingType bt)
        {
            if (bt == null || bt.IsDailyProject) return false;
            string id = bt.StringId ?? "";
            if (Array.IndexOf(WallIds, id) >= 0) return true;
            if (Array.IndexOf(LordOnly, id) >= 0) return false;
            return !bt.IsMilitaryProject;
        }

        /// <summary>Sufit ceny sztuki materialu (P2, obaj platnicy): BuildMaterialPriceCap x wartosc; 0 - bez sufitu (klucz wylaczony, sufit 0).</summary>
        private static int CapOf(ItemObject it)
        {
            var s = Settings.Current;
            if (s == null || it == null || !s.FolkBuildsTown || !(s.BuildMaterialPriceCap > 0f)) return 0;
            return Math.Max(1, (int)Math.Ceiling(s.BuildMaterialPriceCap * Math.Max(1, it.Value)));
        }

        /// <summary>Budowla, do ktorej lud doklada dzis: czolo kolejki (swoje), pierwsza swoja dalej w kolejce (czolo wojskowe), a w osadzie AI z kolejka bez
        /// swojej - swoja budowla najblizsza konca poziomu (przy remisie nizszy poziom). where: 0 czolo, 1 dalej w kolejce, 2 poza kolejka. null - nic.</summary>
        private static Building FolkTarget(Town t, bool ai, out int where)
        {
            where = -1;
            var q = t.BuildingsInProgress;
            if (q == null || q.Count == 0) return null;   // pusta kolejka - lud nie buduje (projekt rozdz. 6)
            int i = 0;
            foreach (var b in q)
            {
                if (b != null && b.BuildingType != null && b.CurrentLevel < 3 && FolkPays(b.BuildingType)) { where = i == 0 ? 0 : 1; return b; }
                i++;
            }
            if (!ai || t.Buildings == null) return null;
            Building best = null; double key = -1;
            foreach (var b in t.Buildings)
            {
                if (b == null || b.BuildingType == null || b.CurrentLevel >= 3 || !FolkPays(b.BuildingType) || q.Contains(b)) continue;
                int cost = b.GetConstructionCost();
                if (cost <= 0) continue;
                double k = b.BuildingProgress / cost + (3 - b.CurrentLevel) * 1e-3;
                if (k > key) { key = k; best = b; }
            }
            if (best != null) where = 2;
            return best;
        }

        /// <summary>Material ludu z polki targu: przejscia jak BuyMaterials pana, sufit ceny, narzedzia do 5 sztuk na polce, sztuki do 5 zl w wozach po 10.</summary>
        private static int FolkBuy(Settlement market, double budget, int[] pieces)
        {
            if (market == null || market.Town == null || market.ItemRoster == null || budget < 1) return 0;
            int spent = 0;
            var roster = market.ItemRoster;
            for (int pass = 0; pass < 3 && spent < budget; pass++)
                for (int k = 0; k < MatIds.Length; k++)
                {
                    var it = MBObjectManager.Instance.GetObject<ItemObject>(MatIds[k]);
                    if (it == null) continue;
                    int have = roster.GetItemNumber(it);
                    if (MatIds[k] == "tools") have -= ToolsKeep;   // narzedzia: lud zostawia na polce 5 sztuk (projekt B rozdz. 8 - wsie i warsztaty)
                    else if (MatIds[k] == "hardwood") have -= WoodKeep;   // drewno: lud zostawia na polce 50 sztuk - dymarki (zelazo) i warsztaty kupuja po nim (bieg 120 dob: stos swiata 9 dni zuzycia)
                    if (have <= 0) continue;
                    int price = Math.Max(1, market.Town.GetItemPrice(it, null, false));
                    int cap = CapOf(it);
                    if (cap > 0 && price > cap) { if (pass == 0) _capFolk++; continue; }
                    int n = Math.Min(have, (int)((budget - spent) / price / (3 - pass)) + (pass == 2 ? 0 : 1));
                    n = Math.Min(n, (int)((budget - spent) / price));
                    if (n <= 0) continue;
                    var mel = new EquipmentElement(it);
                    var town = market.Town;
                    Func<int> pf;
                    if (price <= WagonPrice)
                    {
                        int cnt = 0, cur = price;   // woz po 10: jedna wycena na 10 sztuk
                        pf = () => { if (++cnt % WagonN == 0) cur = Math.Max(1, town.GetItemPrice(mel, null, false)); return cur; };
                    }
                    else pf = () => Math.Max(1, town.GetItemPrice(mel, null, false));
                    int cost, first, last;
                    n = ShelfBuy.Take(roster, mel, n, (int)(budget - spent), pf, out cost, out first, out last, price, true, cap);
                    if (n <= 0) continue;
                    OreLedger.NoteBuild(it, n);   // ksiega rudy i drewna: pozycja "budowy" (tylko licznik)
                    pieces[k] += n; spent += cost;
                    if (spent >= budget) break;
                }
            return spent;
        }

        /// <summary>Postep budowli poza czolem kolejki: punkty wprost; pelny poziom - Building.LevelUp (jak gra: postep - koszt, zdarzenie zmiany poziomu), na
        /// poziomie 3 postep = koszt (BuildingHelper.CheckIfBuildingIsComplete), wpis wychodzi z kolejki (reszta kolejki w tej samej kolejnosci).</summary>
        private static bool AddProgress(Town t, Building b, float pts)
        {
            b.BuildingProgress += pts;
            int cost = b.GetConstructionCost();
            if (cost <= 0 || b.BuildingProgress < cost || b.CurrentLevel >= 3) return false;
            b.LevelUp();
            if (b.CurrentLevel >= 3) b.BuildingProgress = b.GetConstructionCost();
            var q = t.BuildingsInProgress;
            if (q != null && q.Contains(b))
            {
                var keep = new List<Building>(q);
                keep.Remove(b);
                q.Clear();
                foreach (var x in keep) q.Enqueue(x);
            }
            return true;
        }

        /// <summary>P2: roboty ludu - z BuildFunding.Daily PO budowach pana (pan pierwszy na targu), w ramce ksiegi towarow "budowy" i pozycji MBuild ksiegi pieniadza.</summary>
        private static void FolkWorks()
        {
            long t0 = Stopwatch.GetTimestamp();
            var s = Settings.Current;
            int day = (int)CampaignTime.Now.ToDays;
            _folk.Clear(); _rec.Clear();
            int towns = 0, castles = 0, built = 0, atHead = 0, behind = 0, offQ = 0, done = 0;
            int nNone = 0, nSiege = 0, nNoMat = 0, nNoCoin = 0, nLevel = 0, nMil = 0, nHands = 0, nOff = 0;
            long fromG = 0, fromC = 0, mat = 0, wage = 0, unpaid = 0, valSum = 0, gmRest = 0; int gmRestN = 0;
            double pts = 0;
            var pieces = new int[MatIds.Length];
            var doneNames = new List<string>();
            bool on = FolkOn;
            if (on)
            {
                double share = Math.Max(0, Math.Min(1, s.FolkWorksShare)), hands = Math.Max(0, Math.Min(1, s.FolkWorksHands)), wageDay = Math.Max(0, s.WorkshopWagePerDay);
                double ms = Math.Max(0.01, Math.Min(1.0, s.BuildMaterialShare)), r = (1 - ms) / ms;
                foreach (var st in Settlement.All)
                {
                    if (st == null || st.Town == null || (!st.IsTown && !st.IsCastle) || st.OwnerClan == null) continue;
                    var t = st.Town;
                    var rec = new WorkRec { Day = day, Why = WhyNone, Where = -1 };
                    try
                    {
                        if (!TownFolk.WorksPurse(st, share, _pay)) { rec.Why = WhyOff; nOff++; continue; }
                        _rec[st] = rec;
                        if (st.IsUnderSiege) { rec.Why = WhySiege; nSiege++; continue; }
                        int where;
                        var b = FolkTarget(t, st.OwnerClan != Clan.PlayerClan, out where);
                        if (b == null)
                        {
                            var q = t.BuildingsInProgress;
                            bool mil = q != null && q.Count > 0;
                            rec.Why = mil ? WhyMilOnly : WhyNone; if (mil) nMil++; else nNone++;
                            continue;
                        }
                        rec.Bld = b.Name != null ? b.Name.ToString() : b.BuildingType.StringId; rec.Where = where;
                        long B = _pay.Total;
                        if (B < 1) { rec.Why = WhyNoCoin; nNoCoin++; continue; }
                        var market = st.IsTown ? st : NearestTown(st);
                        double idx = WageIdx(market);
                        double ppp = b.BuildingType.IsMilitaryProject ? Math.Max(1f, s.BuildPencePerPointMilitary) : Math.Max(1f, s.BuildPencePerPointCivil);
                        double H = hands * _pay.Heads * wageDay * idx;
                        double rem = b.GetConstructionCost() - b.BuildingProgress;
                        if (where == 0) { float cp = 0f; try { cp = t.Construction; } catch { cp = 0f; } rem -= Math.Max(0f, cp); }   // czolo: punkty pana (i mocy gry) tej doby
                        if (rem < 1) { rec.Why = WhyLevel; nLevel++; continue; }
                        double mFor = rem * ppp / (1 + r / idx);
                        double mT = Math.Min(B * ms, Math.Min(r > 0 ? H / r : double.MaxValue, mFor));
                        if (r > 0 && H / r <= Math.Min(B * ms, mFor)) nHands++;
                        if (mT < 1) { rec.Why = H < 1 ? WhyNoCoin : WhyLevel; if (H < 1) nNoCoin++; else nLevel++; continue; }
                        int m = FolkBuy(market, mT, pieces);
                        if (m <= 0) { rec.Why = WhyNoMat; nNoMat++; continue; }
                        double w = Math.Min(Math.Min(r * m, H), Math.Min(B - m, Math.Max(0, (rem * ppp - m) * idx)));
                        long wl = Math.Max(0, (long)Math.Floor(w));
                        long got = TownFolk.WorksSpend(st, market, m, wl, _pay);
                        if (got < m) unpaid += m - got;   // kontrola: material bez zaplaty (oczekiwane 0)
                        long wPaid = Math.Max(0, got - m);
                        int mPaid = (int)Math.Min(m, got);
                        float p = PointsFor(mPaid, (int)Math.Min(int.MaxValue, wPaid), (float)ppp, market);
                        if (market != null && market.Town != null) { try { TownFolk.Sale(market, mPaid, TownFolk.MixBuild); } catch { } }   // material - sprzedaz kupcow (podstawa robocizny)
                        rec.Mat = mPaid; rec.Wage = wPaid; rec.FromG = _pay.UsedG; rec.Pts = p; rec.Why = WhyBuilt;
                        built++; if (st.IsTown) towns++; else castles++;
                        mat += mPaid; wage += wPaid; fromG += _pay.UsedG; fromC += _pay.UsedC[0] + _pay.UsedC[1] + _pay.UsedC[2]; pts += p;
                        if (where == 0) { atHead++; float x; _folk.TryGetValue(t, out x); _folk[t] = x + p; }
                        else
                        {
                            if (where == 1) behind++; else offQ++;
                            if (AddProgress(t, b, p))
                            {
                                done++;
                                if (st.OwnerClan == Clan.PlayerClan)
                                    Log.Player("The " + (st.IsTown ? "townsfolk" : "castle folk") + " of " + st.Name + " have finished the " + rec.Bld + " (level " + b.CurrentLevel + ") at their own cost.");
                                if (doneNames.Count < 6) doneNames.Add(st.Name + ": " + rec.Bld + " poz. " + b.CurrentLevel);
                            }
                        }
                    }
                    catch (Exception e) { FolkStumble("FolkWorks(osada)", e); }
                    finally
                    {
                        try { long x = TownFolk.WorksGminaRest(st, _pay); if (x > 0) { gmRest += x; gmRestN++; } } catch (Exception e) { FolkStumble("FolkWorks(gmina)", e); }
                        _pay.Clear();
                    }
                }
            }
            try { TownFolk.WorksGminaSweep(ref gmRest, ref gmRestN); } catch (Exception e) { FolkStumble("FolkWorks(sweep)", e); }
            for (int k = 0; k < MatIds.Length; k++)
            {
                var it = MBObjectManager.Instance.GetObject<ItemObject>(MatIds[k]);
                if (it != null) valSum += (long)pieces[k] * Math.Max(1, it.Value);
            }
            if (s == null || !s.LogEnabled) { _capLord = _capFolk = 0; _stumbles = 0; return; }
            try
            {
                var sb = new StringBuilder(1200);
                sb.Append("Roboty ludu (P2): dzien ").Append(day).Append(" | wylacznik Folk Builds Town ").Append(s.FolkBuildsTown ? "tak" : "nie (roboty gminy jak dotad, bez sufitu ceny)")
                  .Append(", Paid Construction ").Append(s.PaidConstruction ? "tak" : "nie (P2 nieczynne)");
                if (on)
                    sb.Append(" | czesc nadwyzki ").Append((100 * s.FolkWorksShare).ToString("0.0", Inv)).Append("%/d, rece ").Append((100 * s.FolkWorksHands).ToString("0.0", Inv))
                      .Append("% wyrobnikow x ").Append(s.WorkshopWagePerDay.ToString("0.#", Inv)).Append(" zl, sufit ceny materialu x").Append(s.BuildMaterialPriceCap.ToString("0.0#", Inv));
                sb.Append(" | osad z robota ").Append(built).Append(" (miasta ").Append(towns).Append(", zamki ").Append(castles).Append("; czolo kolejki ").Append(atHead)
                  .Append(", za wojskowa w kolejce ").Append(behind).Append(", poza kolejka AI ").Append(offQ).Append(')')
                  .Append(" | bez robot: bez budowy ").Append(nNone).Append(", tylko wojskowe w kolejce (gracz) ").Append(nMil).Append(", oblezone ").Append(nSiege)
                  .Append(", bez materialu ").Append(nNoMat).Append(", bez nadwyzki ").Append(nNoCoin).Append(", poziom oplacony ").Append(nLevel)
                  .Append(", kiesa nieczynna ").Append(nOff).Append("; na pulapie rak ").Append(nHands)
                  .Append(" | zaplacone ").Append(mat + wage).Append(" (z gmin ").Append(fromG).Append(", z kies klas i podzamczy ").Append(fromC).Append(')')
                  .Append(" | material ").Append(mat).Append(" zl za ");
                for (int k = 0; k < MatIds.Length; k++) sb.Append(k > 0 ? "/" : "").Append(pieces[k]);
                sb.Append(" szt. (").Append(string.Join("/", MatIds)).Append("), cena / wartosc ").Append(valSum > 0 ? ((double)mat / valSum).ToString("0.00", Inv) : "-")
                  .Append(" | place ").Append(wage).Append(" | punkty ").Append(pts.ToString("0", Inv)).Append(", poziomy ukonczone wprost (poza czolem) ").Append(done)
                  .Append(doneNames.Count > 0 ? " [" + string.Join("; ", doneNames.ToArray()) + "]" : "")
                  .Append(" | gmina - reszta budzetu robot na place robot publicznych ").Append(gmRest).Append(" w ").Append(gmRestN).Append(" miastach")
                  .Append(" | sufit ceny - towar pominiety: pan ").Append(_capLord).Append(", lud ").Append(_capFolk)
                  .Append(" | kontrola: material bez zaplaty ").Append(unpaid)
                  .Append(" | czas ").Append(((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.00", Inv)).Append(" ms | potkniecia ").Append(_stumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { FolkStumble("FolkWorks(linia)", e); }
            _capLord = _capFolk = 0; _stumbles = 0;
        }

        /// <summary>P2: zdanie "Works: ..." w oknie "People of this land" (liczby ostatniej doby osady; null - bez zdania).</summary>
        internal static string WorksLine(Settlement st)
        {
            try
            {
                WorkRec r;
                if (st == null || !FolkOn || !_rec.TryGetValue(st, out r) || r == null || (int)CampaignTime.Now.ToDays - r.Day > 1) return null;
                string who = st.IsTown ? "townsfolk" : "castle folk";
                switch (r.Why)
                {
                    case WhyBuilt:
                        return "Works: the " + who + " put " + (r.Mat + r.Wage).ToString("N0", Inv) + " coins into the " + r.Bld + " today - " + r.Mat.ToString("N0", Inv)
                               + " for timber and stone, " + r.Wage.ToString("N0", Inv) + " in wages" + (r.FromG > 0 ? " (" + r.FromG.ToString("N0", Inv) + " of it from the town's common purse)" : "") + ".";
                    case WhyNoMat: return "Works: the " + who + " have coin to build with, but the market has no timber or stone they can afford.";
                    case WhyNoCoin: return "Works: the " + who + " have nothing to spare for building.";
                    case WhySiege: return "Works: stopped - the " + (st.IsTown ? "town" : "castle") + " is under siege.";
                    case WhyLevel: return "Works: the present stage of the " + (r.Bld ?? "building") + " is already paid for today.";
                    case WhyMilOnly: return "Works: only the lord's military works wait in line - the " + who + " pay only for what serves the " + (st.IsTown ? "town" : "castle") + ".";
                    case WhyNone: return "Works: nothing is waiting to be built here.";
                    default: return null;
                }
            }
            catch { return null; }
        }
    }
}
