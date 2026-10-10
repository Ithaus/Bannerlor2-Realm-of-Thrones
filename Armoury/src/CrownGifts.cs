using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Armoury
{
    /// <summary>
    /// PACZKA 182 - DARY MIEDZY KORONAMI I STRAZ BEZ ZOLDU (projekt etapu 2, krok C1; PLAN 2.4, 2.5). Wylaczniki CrownGifts i WatchUnpaid.
    ///
    ///  - POLNOC -> NOCNA STRAZ: GiftNorthToWatchShare (25%) wplywow dnia skarbca Polnocy (165: wplywy dnia z 1/360 zapasu).
    ///  - WOLNE MIASTA -> DOTHRAKOWIE: kazde Wolne Miasto GiftFreeCitiesToDothrakiShare (10%) swoich wplywow dnia ([D] 03:45 nr 12 = Q4a).
    ///  - Dar idzie przez skarbiec odbiorcy TEGO SAMEGO DNIA do glow rodow odbiorcy wedlug STALYCH wag (Z8 - nigdy wedlug liczby ludzi):
    ///    Straz - twierdza (miasto albo zamek) 1, wies 0.25, rod bez lenna 0.5; Dothrakowie - rowno na kazdy rod khalasaru. Liczy sie do ich D
    ///    (czesc "korona" D stalego). Reszta z zaokraglen zostaje w skarbcu odbiorcy (nic nie znika).
    ///  - STRAZ BEZ ZOLDU (WatchUnpaid, [D] 07.10): zold jednostek w partiach i zalogach Strazy = 0 (MountedWage.WagePostfix w kontekscie zoldu
    ///    partii); liczebnosc trzyma pulap 166 w ludziach (ClanBudget) - dlatego WatchUnpaid dziala tylko z ClanBudgetEnabled.
    /// Kolejnosc: po CrownIncome.Begin, przed ratami reparacji (2.0b). Platnik -> odbiorca: skarbiec Polnocy / Wolnych Miast -> glowy rodow Strazy /
    /// Dothrakow. Id krolestw ROT (sprawdzone w balans-krolestw.csv biegu e2b120 i w budzet-rodow.csv): Polnoc "battania", Straz "nightswatch",
    /// Dothrakowie "khuzait"; Wolne Miasta "bravos" (Braavos), "volantis", "pentos", "myr", "lys", "tyrosh", "norvos", "qohor", "nord" (Lorath -
    /// kultura "nord", PopulationLaw). Qarth nie jest Wolnym Miastem; bunty (new_kingdom*) nie placa.
    /// </summary>
    internal static class CrownGifts
    {
        internal const string NorthId = "battania", WatchId = "nightswatch", DothrakiId = "khuzait";
        internal static readonly string[] FreeCityIds = { "bravos", "volantis", "pentos", "myr", "lys", "tyrosh", "norvos", "qohor", "nord" };
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.CrownGifts && CrownIncome.On; } }

        /// <summary>Straz bez zoldu: wymaga pulapu w ludziach z budzetu rodu (166) - bez niego werbunek Strazy nie mialby hamulca.</summary>
        internal static bool WatchUnpaidOn { get { var s = Settings.Current; return s != null && s.WatchUnpaid && s.ClanBudgetEnabled; } }

        internal static bool IsWatch(IFaction f) { return f != null && f.StringId == WatchId; }

        /// <summary>Partia Strazy (partia lorda rodu Strazy, zaloga osady Strazy, takze gracz w Strazy).</summary>
        internal static bool IsWatchParty(MobileParty mp)
        {
            try { return mp != null && (mp.IsLordParty || mp.IsGarrison || mp.IsMainParty) && IsWatch(mp.MapFaction); }
            catch { return false; }
        }

        // liczniki doby (linia i "Obieg")
        internal static long LastNorth, LastFree, LastToWatch, LastToDothraki, LastKept;
        internal static int LastWatchClans, LastDothrakiClans;
        private static string _lastDetail = "-";
        private static int _stumbles;
        private static bool _errLogged, _idsLogged;

        internal static void Reset() { ZeroLast(); _stumbles = 0; _errLogged = false; _idsLogged = false; _lastDetail = "-"; }
        internal static void ZeroLast() { LastNorth = LastFree = LastToWatch = LastToDothraki = LastKept = 0; LastWatchClans = LastDothrakiClans = 0; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errLogged) return;
            _errLogged = true;
            try { Log.Error("CrownGifts." + where, e); } catch { }
        }

        private static Kingdom Find(string id)
        {
            foreach (var k in Kingdom.All) if (k != null && !k.IsEliminated && k.StringId == id) return k;
            return null;
        }

        private static bool Eligible(Clan c)
        {
            return c != null && !c.IsEliminated && c.Leader != null && c.Leader.IsAlive && !c.IsUnderMercenaryService && !c.IsBanditFaction
                   && (c.StringId == null || !c.StringId.StartsWith("bk_courtiers_", StringComparison.Ordinal)) && !ClanIncomeBook.IsUndeadClan(c);
        }

        /// <summary>Waga rodu Strazy: twierdza (miasto, zamek) 1, wies 0.25, rod bez lenna 0.5 (stale - Z8).</summary>
        private static double WatchWeight(Clan c)
        {
            double w = 0; bool any = false;
            var sts = c.Settlements;
            if (sts != null)
                for (int i = 0; i < sts.Count; i++)
                {
                    var st = sts[i];
                    if (st == null) continue;
                    if (st.IsTown || st.IsCastle) { w += 1.0; any = true; }
                    else if (st.IsVillage) { w += 0.25; any = true; }
                }
            return any ? w : 0.5;
        }

        /// <summary>Dar `gift` ze skarbca dawcy przez skarbiec odbiorcy do glow jego rodow wedlug wag; zwraca to, co zeszlo ze skarbca dawcy
        /// (0 - nie ma komu dac), `given` - to, co doszlo do rodow (reszta z zaokraglen zostaje w skarbcu odbiorcy).</summary>
        private static long Give(Kingdom from, Kingdom to, long gift, bool watch, out long given, out int clans)
        {
            clans = 0; given = 0;
            if (gift <= 0 || from == null || to == null) return 0;
            var list = new List<Clan>(); var wts = new List<double>(); double sum = 0;
            foreach (var c in to.Clans)
            {
                if (!Eligible(c)) continue;
                double w = watch ? WatchWeight(c) : 1.0;
                if (w <= 0) continue;
                list.Add(c); wts.Add(w); sum += w;
            }
            if (list.Count == 0 || sum <= 0) return 0;            // nie ma komu dac - dar nie wychodzi ze skarbca dawcy
            int g = (int)Math.Min(int.MaxValue, gift);
            from.KingdomBudgetWallet -= g;                       // dawca -> skarbiec odbiorcy (ten sam dzien dalej do rodow)
            to.KingdomBudgetWallet += g;
            for (int i = 0; i < list.Count; i++)
            {
                int x = (int)(g * wts[i] / sum);
                if (x <= 0) continue;
                var h = list[i].Leader;
                to.KingdomBudgetWallet -= x;                     // najpierw skarbiec, potem glowa - dostaje dokladnie tyle, ile zeszlo
                h.ChangeHeroGold(x);
                CirculationWindows.NoteHeroGold(h, x);           // paczka 169b: glowa poza swiatem - zloto wyszlo ze swiata (tylko licznik)
                ClanIncomeBook.NoteInflow(h, x, ClanIncomeBook.KCrownLevies);   // D rodu odbiorcy: czesc "korona" (dar z korony)
                given += x; clans++;
            }
            LastKept += g - given;                               // reszta z zaokraglen zostaje w skarbcu odbiorcy
            return g;
        }

        /// <summary>Raz na dobe, po CrownIncome.Begin (wplywy dnia), przed ratami reparacji. Kazdy dawca we wlasnym try.</summary>
        internal static void Daily()
        {
            ZeroLast();
            var s = Settings.Current;
            if (s == null || Campaign.Current == null || !On) return;
            try
            {
                var north = Find(NorthId); var watch = Find(WatchId); var dothraki = Find(DothrakiId);
                if (!_idsLogged)
                {
                    _idsLogged = true;
                    var found = new List<string>();
                    foreach (var id in FreeCityIds) if (Find(id) != null) found.Add(id);
                    Log.Info("Dary koron (182): krolestwa - Polnoc (" + NorthId + ") " + (north != null ? north.Name.ToString() : "BRAK") + ", Straz (" + WatchId + ") "
                             + (watch != null ? watch.Name.ToString() : "BRAK") + ", Dothrakowie (" + DothrakiId + ") " + (dothraki != null ? dothraki.Name.ToString() : "BRAK")
                             + ", Wolne Miasta " + found.Count + " z " + FreeCityIds.Length + " (" + string.Join(", ", found.ToArray()) + ").");
                }
                var det = new List<string>();
                float nShare = Math.Max(0f, Math.Min(1f, s.GiftNorthToWatchShare)), fShare = Math.Max(0f, Math.Min(1f, s.GiftFreeCitiesToDothrakiShare));
                if (north != null && watch != null && nShare > 0f)
                {
                    try
                    {
                        var d = CrownIncome.DayOf(north);
                        if (d != null)
                        {
                            long gift = Math.Min((long)(nShare * d.Spend), CrownIncome.LeftFor(north));
                            int n; long got; long out1 = Give(north, watch, gift, true, out got, out n);
                            if (out1 > 0)
                            {
                                CrownIncome.Spent(north, out1); d.GiftOut += out1;
                                var dw = CrownIncome.DayOf(watch); if (dw != null) dw.GiftIn += got;
                                LastNorth = out1; LastToWatch = got; LastWatchClans = n;
                                det.Add(north.Name + " -> " + watch.Name + " " + got + " (" + (nShare * 100f).ToString("0", Inv) + "% z " + d.Spend + ", rodow " + n + ")");
                            }
                        }
                    }
                    catch (Exception e) { Stumble("Daily(Polnoc)", e); }
                }
                if (dothraki != null && fShare > 0f)
                    foreach (var id in FreeCityIds)
                    {
                        try
                        {
                            var fc = Find(id);
                            if (fc == null || fc == dothraki) continue;
                            var d = CrownIncome.DayOf(fc);
                            if (d == null) continue;
                            long gift = Math.Min((long)(fShare * d.Spend), CrownIncome.LeftFor(fc));
                            int n; long got; long out1 = Give(fc, dothraki, gift, false, out got, out n);
                            if (out1 <= 0) continue;
                            CrownIncome.Spent(fc, out1); d.GiftOut += out1;
                            var dd = CrownIncome.DayOf(dothraki); if (dd != null) dd.GiftIn += got;
                            LastFree += out1; LastToDothraki += got; LastDothrakiClans = n;
                            det.Add(fc.Name + " " + got);
                        }
                        catch (Exception e) { Stumble("Daily(" + id + ")", e); }
                    }
                _lastDetail = det.Count > 0 ? string.Join(", ", det.ToArray()) : "-";
                if (s.LogEnabled)
                    Log.Info("Dary koron (182): dzien " + (int)CampaignTime.Now.ToDays
                             + " | Polnoc -> Straz " + LastToWatch + " zl do " + LastWatchClans + " rodow (wagi stale: twierdza 1, wies 0.25, rod bez lenna 0.5)"
                             + " | Wolne Miasta -> Dothrakowie " + LastToDothraki + " zl do " + LastDothrakiClans + " rodow (rowno na rod)"
                             + " | ze skarbcow dawcow zeszlo " + (LastNorth + LastFree) + ", do rodow doszlo " + (LastToWatch + LastToDothraki) + ", w skarbcach odbiorcow zostalo (zaokraglenia) " + LastKept
                             + " | szczegoly: " + _lastDetail
                             + " | Straz bez zoldu (WatchUnpaid): " + (WatchUnpaidOn ? "TAK" : "NIE") + (_stumbles > 0 ? " | potkniecia " + _stumbles : "") + ".");
            }
            catch (Exception e) { Stumble("Daily", e); }
        }
    }
}
