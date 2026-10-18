using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// inwest (noc 17/18.10) - PRACE OKREGU LORDOW AI (projekt B13, etap 4). Jeff 16.10: "ok na razie nie inwestuja" - klucz District Works Ai WYLACZONY; kod gotowy do testu
    /// stabilnosci (projekt TESTS 4: prace AI <= 0.5% rocznej produkcji wsi swiata na 28 dob, produkcja wsi swiata <= +3%, udzial kies panow +-1 p.p., kiesy wsi +-5%).
    /// Regula ta sama co dla gracza (DistrictWorks.Buy - te same drogi pieniedzy), decyzja z biezacego zysku:
    ///  - co 28 dob kazda glowa rodu z lennami patrzy na swoje okregi (wsie przy swoich warowniach);
    ///  - kiesa >= Reserve Days zoldu druzyny + Reserve Flat; kwota = min(Surplus Share x nadwyzka, miejsce do wypelnienia, przy ktorym ostatnia moneta daje Min Return);
    ///  - czesc stoi (bez ognia), wies Normal; nastepna moneta >= Min Return rocznie przy dzisiejszej porze roku (zima tnie) i regulatorze;
    ///  - tempo swiata <= World Pace rocznej produkcji wsi swiata na 28 dob.
    /// Odbudowa przez AI (RebuildsToo) tylko przy tym kluczu. Wyjatki per rod liczone.
    /// </summary>
    internal static class DistrictWorksAi
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal static bool On { get { var s = Settings.Current; return s != null && s.DistrictWorksAi && s.DistrictWorks; } }
        internal static bool RebuildsToo { get { return On; } }
        private static int _last = -1, _st, _dBuys, _dClans, _dLooked; private static long _dGold; private static double _worldYear, _pace;

        internal static void Reset() { _last = -1; _st = 0; _dBuys = _dClans = _dLooked = 0; _dGold = 0; _worldYear = 0; _pace = 0; }

        internal static string DayText()
        {
            if (!On) return "AI: WYLACZONE (District Works Ai - Jeff 16.10)";
            string t = "AI: zakupow " + _dBuys + " (" + _dGold + " zl), rodow ogladanych " + _dClans + ", okregow " + _dLooked + ", tempo swiata " + (100 * _pace).ToString("0.000", Inv) + "% rocznej produkcji wsi (" + Math.Round(_worldYear) + " zl/rok)";
            _dBuys = _dClans = _dLooked = 0; _dGold = 0;
            return t;
        }

        internal static void Daily(int today)
        {
            if (!On) return;
            if (_last >= 0 && today - _last < 28) return;
            _last = today;
            try
            {
                var s = Settings.Current;
                float minRet = Math.Max(0.01f, Math.Min(2f, s.DistrictWorksAiMinReturn));
                float surplusShare = Math.Max(0f, Math.Min(1f, s.DistrictWorksAiSurplusShare));
                int resDays = Math.Max(0, s.DistrictWorksAiReserveDays), resFlat = Math.Max(0, s.DistrictWorksAiReserveFlat);
                float pace = Math.Max(0f, Math.Min(0.1f, s.DistrictWorksAiWorldPace));
                // roczna produkcja wsi swiata (wartosc dnia okregow x 364) - raz na 28 dob
                double world = 0;
                var parts = new List<VillageCrafts.PartCraft>();
                foreach (var v in Village.All) { bool ks; if (v != null && VillageCrafts.PartsOf(v, parts, out ks)) foreach (var p in parts) world += p.ValDay; }
                _worldYear = world * 364;
                double budget = pace * _worldYear, spent = 0;
                foreach (var clan in Clan.All)
                {
                    if (spent >= budget) break;
                    try
                    {
                        if (clan == null || clan == Clan.PlayerClan || clan.IsEliminated || clan.Leader == null || !clan.Leader.IsAlive || clan.Fiefs == null || clan.Fiefs.Count == 0) continue;
                        var leader = clan.Leader;
                        int wages = 0; try { var pp = leader.PartyBelongedTo; wages = pp != null ? Math.Max(0, pp.TotalWage) : 0; } catch { wages = 0; }
                        long reserve = (long)resDays * wages + resFlat;
                        long surplus = leader.Gold - reserve;
                        if (surplus <= 0) continue;
                        _dClans++;
                        double want = surplusShare * surplus;
                        foreach (var f in clan.Fiefs)
                        {
                            if (want < 200 || spent >= budget) break;
                            if (f == null || f.Settlement == null) continue;
                            foreach (var v in f.Settlement.BoundVillages)
                            {
                                if (want < 200 || spent >= budget) break;
                                if (v == null || v.VillageState != Village.VillageStates.Normal) continue;
                                _dLooked++;
                                var qs = new List<DistrictWorks.Quote>();
                                if (!DistrictWorks.QuotesOf(v, qs, clan)) continue;
                                DistrictWorks.Quote best = null; double bestRet = 0;
                                foreach (var q in qs)
                                {
                                    if (!q.Allowed || q.State != VillageBook.SStanding || !(q.KitPer > 0) || !(q.S > 0)) continue;
                                    double x = Math.Min(1.0, q.UEff / q.S);
                                    double ret = s.DistrictWorksShare * q.H * q.Bmax * 2 * (1 - x) * (1 - q.Winter);
                                    if (ret >= minRet && ret > bestRet) { bestRet = ret; best = q; }
                                }
                                if (best == null) continue;
                                double xs = 1.0 - minRet / Math.Max(1e-6, 2.0 * s.DistrictWorksShare * best.H * best.Bmax * Math.Max(0.05, 1 - best.Winter));
                                double room = Math.Max(0, (Math.Min(1.0, xs) * best.S - best.UEff) * best.KitPer);
                                int amount = (int)Math.Floor(Math.Min(Math.Min(want, room), Math.Min(best.Room, budget - spent)));
                                if (amount < Math.Max(200, s.DistrictWorksMinGold)) continue;
                                var res = DistrictWorks.Buy(leader, clan, v, best.Uid, amount, 0, true);
                                if (!res.Ok) continue;
                                _dBuys++; _dGold += res.Paid; spent += res.Paid; want -= res.Paid;
                            }
                        }
                    }
                    catch (Exception e) { _st++; if (_st <= 3) Log.Error("DistrictWorksAi.Daily(rod)", e); }
                }
                _pace = _worldYear > 0 ? spent / _worldYear : 0;
            }
            catch (Exception e) { _st++; if (_st <= 3) Log.Error("DistrictWorksAi.Daily", e); }
        }
    }
}
