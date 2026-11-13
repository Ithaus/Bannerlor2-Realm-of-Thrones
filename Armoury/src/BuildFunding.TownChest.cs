using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 482 (klucz BuildTownChestWhenLordBarred; analiza at-220i, sprawa D): po wybuchu powszechnej wojny ok. 95 budow na dobe stalo "bez budzetu doby (R4)" - ok. 114 rodow
    /// z drobnym kredytem Banku (DebtLadder.IsDebtor przy kazdym kredycie) ma w budzecie 166/168 przydzial budow 0 (ClanBudget: "if (b.Debtor) b.Build = 0"), a kasy miast
    /// leza ponad celem (7.7 mln ponad zapas kupcow). REGULA: gdy rod nie ma przydzialu budow (powod R4 "przydzial rodu 0"), budowe WLASNEGO MIASTA placi kasa tego miasta -
    /// tylko z nadwyzki ponad zapas kupcow (TownPurse.Reserve), najblizsza rate firmy DS z zaleglosciami (DsTowns.TdOwe - TdKeep bez podlogi firm) i dlug rady miasta m96 (TownDebt.ArrearsLike).
    /// Budzet doby: najwyzej 1/30 tej nadwyzki i najwyzej przydzial rodu bez kary dluznika (ClanBudget.BuildShareBase); bez podzialu na budowy rodu (count) i bez kolejki
    /// rodu R3-11a (OneBudget) - kazde miasto placi swoja budowe ze swojej kasy. Mury 472 (WallBudget, limit doby) jak dotad. Tylko MIASTA (zamki - CastlePurse, inny
    /// regulator - jak dotad), nie lenna gracza, nie oblezone, nie rody najemne.
    /// ZLOTO (ekonomia zamknieta, nic z niczego): material z targu miasta - kasa osady -> kasa targu (miasto: ta sama kasa, netto 0 zl, towar schodzi z polki);
    /// place - kasa -> kiesa ludu (TownFolk.Earn KBuildLabour); punkty tylko od placy naprawde wyplaconej (482-p), sprzedaz kupcow tylko od obcego targu (482-p). Kiesa pana i jego dlug nietkniete: bez
    /// ChangeHeroGold, bez CirculationWindows.NoteHeroGold i bez ClanIncomeBook.NoteOwnPaid (to nie D rodu), bez zakupu u wytworcow 363 (LordProducerBuy - kiesa pana).
    /// Kazde wywolanie we wlasnym try (licznik potkniec, blad raz na sesje) - bez globalnego wylacznika. Klucz wylaczony - Daily idzie stara droga co do monety.
    /// </summary>
    internal static partial class BuildFunding
    {
        private static int _chN, _chRefused, _chNoBase, _chWall, _chNoMat, _chStumbles;
        private static long _chGold, _chMat, _chMatMoved, _chWage, _chWageOff;
        private static double _chPts;
        private static bool _chErrLogged;

        internal static bool ChestOn { get { var s = Settings.Current; return s != null && s.BuildTownChestWhenLordBarred; } }

        private static void ChestDayStart()
        {
            _chN = _chRefused = _chNoBase = _chWall = _chNoMat = _chStumbles = 0;
            _chGold = _chMat = _chMatMoved = _chWage = _chWageOff = 0; _chPts = 0;
        }

        private static void ChestReset() { ChestDayStart(); _chErrLogged = false; }

        /// <summary>
        /// Daily (pan), osada z powodem "przydzial rodu 0" przy wlaczonym kluczu. true - galaz zalatwila osade (oplacona z kasy miasta albo wstrzymana z powodem po wejsciu:
        /// mury - limit doby, brak materialu); wolajacy idzie do nastepnej osady. false - nic nie ruszone (warunki, brak nadwyzki kasy) - wolajacy idzie stara droga
        /// (R4 "przydzial rodu 0"), a `why` (moze byc null) dopisuje do powodu, czemu kasa miasta nie zaplacila.
        /// </summary>
        private static bool ChestPay(Settlement st, Building b, Clan clan, float ppp, float mcut, Settlement market, Settings s, out string why)
        {
            why = null;
            if (s == null || st == null || !st.IsTown || st.Town == null || b == null || b.BuildingType == null || clan == null || clan == Clan.PlayerClan
                || clan.IsUnderMercenaryService || st.IsUnderSiege || market == null || market.Town == null || market.ItemRoster == null) return false;
            bool touched = false;   // po WallBudget (liczniki murow) - galaz konczy osade sama, bez drugiego przejscia starej drogi
            try
            {
                float baseShare;
                if (!ClanBudget.BuildShareBase(clan, out baseShare) || !(baseShare >= 1f)) { _chNoBase++; why = "; kasa miasta nie placi - rod bez podstawy przydzialu (482)"; return false; }
                var t = st.Town;
                double keep = (double)TownPurse.Reserve(t) + DsTowns.TdOwe(st) + Math.Max(0.0, TownDebt.ArrearsLike(st));   // zapas kupcow + najblizsza rata DS z zaleglosciami + dlug rady m96
                double nad = (double)t.Gold - keep;
                float tb = nad >= 30.0 ? (float)Math.Min((double)baseShare, nad / 30.0) : 0f;
                if (!(tb >= 1f)) { _chRefused++; why = "; kasa miasta bez nadwyzki ponad zapas kupcow, rate DS i dlug rady (482)"; return false; }
                touched = true;
                float preWall = tb;
                if (WallOn) tb = WallBudget(st, b, tb, ppp, market);   // 472: mury - budzet najwyzej na punkty do limitu doby (jak u pana)
                bool wallCut = WallOn && tb < preWall;
                if (tb < 1f) { _chWall++; NoBudgetWhy[st.StringId] = "mury: limit doby (kasa miasta, 482)"; _dNoBudget++; return true; }
                float ms = MBMath.ClampFloat(s.BuildMaterialShare, 0f, 1f);
                float matBudget = tb * ms;
                int matSpent = 0;
                var gfm = GoodsLedger.Begin(GoodsLedger.FBuild, market);
                try
                {
                    matSpent = BuyMaterials(market, matBudget);
                    // jedna najtansza sztuka - najwyzej budzet kasy tej doby (NIE kiesa pana)
                    if (matBudget > 0f && matSpent <= 0) matSpent = BuyOneCheapest(market, Math.Max(0, (int)tb));
                }
                finally { GoodsLedger.End(gfm); }
                if (matBudget > 0f && matSpent <= 0)
                {
                    _chNoMat++;
                    if (wallCut) { _wCutIdle++; NoBudgetWhy[st.StringId] = "mury: limit doby (reszta ponizej ceny sztuki; kasa miasta, 482)"; }
                    else { _stalledNoMat++; NoBudgetWhy[st.StringId] = "brak materialu na targu (albo ponad sufit ceny; kasa miasta, 482)"; }
                    return true;
                }
                float labour = Math.Min(tb - matBudget, matSpent * Math.Max(0f, (1f - s.BuildMaterialShare) / Math.Max(0.01f, s.BuildMaterialShare)));
                int labourI = MBRandom.RoundRandomized(Math.Max(0f, labour));
                labourI = Math.Min(labourI, Math.Max(0, (int)tb - matSpent));   // material i place razem najwyzej budzet kasy (tb <= nadwyzka / 30 < kasa)
                // material: kasa osady -> kasa targu (miasto - ta sama kasa, netto 0 zl; towar zszedl z polki w ramce budow ksiegi towarow)
                int matMoved = 0;
                if (matSpent > 0 && market.Town != t)
                {
                    int had = t.Gold;
                    t.ChangeGold(-matSpent);
                    int took = had - t.Gold;
                    if (took > 0) { market.Town.ChangeGold(took); matMoved = took; }
                }
                // 482-p (recenzja): sprzedaz kupcow (f.Sale - podstawa robocizny handlu "z kasy do ludu" i sprzedazy DS M7) TYLKO gdy obcy targ naprawde dostal zloto;
                // wlasny targ miasta - nikt nie zaplacil (kasa sama sobie), wiec bez wpisu (inaczej kasa placilaby ludowi robocizne od fantomowej sprzedazy)
                try { if (matMoved > 0) TownFolk.Sale(market, matMoved, TownFolk.MixBuild); } catch { }
                // place: kasa -> kiesa ludu (Earn zwraca przyjete; z kasy schodzi tylko to). 482-p (recenzja): reszta placy u pana szla z kiesy pana do kasy - tu kasa
                // placilaby sama sobie (zadne zloto sie nie rusza), wiec punkty i liczniki licza TYLKO place naprawde wyplacone ludowi (folk); B1 wylaczony - same materialy
                int folk = labourI > 0 ? TownFolk.Earn(st, labourI, TownFolk.KBuildLabour) : 0;
                if (folk > 0) t.ChangeGold(-folk);
                try { if (folk > 0) TownFolk.Note(st, folk, TownFolk.KBuildLabour); } catch { }   // E5/0 (195): place budowy (tylko licznik)
                float pts = PointsFor(matSpent, folk, ppp, market);
                if (mcut < 1f) Buildings338Mason.NoteCut(st.Town, mcut, matSpent + folk, pts, false);   // 473-a: linia murarza (tylko licznik)
                _funded[st.Town] = pts;
                if (WallOn) WallPaid(st, b, matSpent, folk, pts);   // 472: liczniki murow i kontrola limitu
                EngPaid(st.Town, pts);   // R3-10b: XP Inzynierii zarzadcy na miejscu i liczniki
                NoBudgetWhy.Remove(st.StringId);
                _chN++; _chGold += matMoved + folk; _chMat += matSpent; _chMatMoved += matMoved; _chWage += folk; _chWageOff += labourI - folk; _chPts += pts;
                return true;
            }
            catch (Exception e)
            {
                _chStumbles++;
                if (!_chErrLogged) { _chErrLogged = true; Log.Error("BuildFunding.ChestPay(" + (st.StringId ?? "?") + ")", e); }   // licznik potkniec, blad raz na sesje (CLAUDE.md 7)
                if (touched) { NoBudgetWhy[st.StringId] = "blad kasy miasta (482)"; _dNoBudget++; }
                return touched;
            }
        }

        /// <summary>Dopisek linii "Budowy oplacone" (przed dopiskiem R3-11a i N5-E - bramka N5-5 czyta koniec linii). Klucz wylaczony - pusty (linia jak dotad).</summary>
        private static string ChestTail()
        {
            if (!ChestOn) return "";
            try
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                return " | 482 kasa miasta za pana bez przydzialu budow (Town Treasury Funds Own Build When Lord Has No Build Share tak): z kasy miasta oplacone " + _chN
                     + " budow, z kasy zeszlo " + _chGold + " zl (material " + _chMat + " zl, z tego do obcych targow " + _chMatMoved + " - reszta wlasny targ, netto 0 zl; place do kies ludu " + _chWage + ", place bez platnika nie liczone " + _chWageOff + "), punktow " + ((long)_chPts).ToString(inv)
                     + "; odmowa - kasa bez nadwyzki ponad zapas, rate DS i dlug rady " + _chRefused + ", rod bez podstawy przydzialu " + _chNoBase
                     + "; po wejsciu wstrzymane: mury limit doby " + _chWall + ", brak materialu " + _chNoMat + "; potkniecia " + _chStumbles + ".";
            }
            catch { return " | 482 kasa miasta: blad linii."; }
        }
    }
}
