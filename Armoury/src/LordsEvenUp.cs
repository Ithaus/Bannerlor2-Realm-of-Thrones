using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// PACZKA E5/B3 (Z15-5, PLAN 5.7) - WYROWNANIE DLA PANOW ZAMKOW (projekt etapu 5 v2 docs\etap5\PROJEKT-ETAP5-KIESA-LUDU-KASY-MIAST-2026-10-11.md
    /// rozdz. 2 R4 i R16, rozdz. 3 "E5/B3", rozdz. 5.2 pkt 9 (K3); audyt-2026-10-09\15-BILANS-PANOW-ZAMKOW.md Z15-5 i rozdz. 3.5; decyzja Jeffa 09.10 04:25 E
    /// "wyrownanie dla panow zamkow (1/2 nadwyzki, renty), takze u gracza - TAK").
    /// Kiesa ludu (B1) z panem 35% utargu wsi (Z15-0) przenosi pieniadze chlopow do kas miast: bez wyrownania panowie samych zamkow traca 14% ludzi
    /// w wojnie i 32% w pokoju, z wyrownaniem 1% / 4% [R A15 3.5]. Wyrownanie z tych samych pieniedzy, przez korone - zadnego nowego przeplywu zlota:
    ///  R4  zawor kasy miasta (A1, TownPurse.Valve): pan TownRentLordShareWithFolk (1/2) zamiast TownRentLordShare (2/3), korona reszte. Jedna funkcja
    ///      TownPurse.LordShare - zawor i ciecie K3 zwrotu korony (KingdomTreasury.WageRefund) biora ten sam udzial (tyle naprawde wraca panu).
    ///  R16 renty korony (180, CrownRents.Weight): stale wagi lenn miasto CrownRentWeightTownWithFolk (2), zamek CrownRentWeightCastleWithFolk (1.5),
    ///      wies CrownRentWeightVillage (0.25 - ten sam klucz z wyrownaniem i bez) zamiast 3 / 1 / 0.25; nigdy wedlug zalogi (Z8).
    /// Czynne tylko z kluczem CastleLordsEvenUp i przy czynnej kiesie ludu (v2 K3 - wyrownanie kompensuje skutek kiesy); inaczej udzial i wagi z kluczy sprzed
    /// paczki. Wylaczone = stan sprzed paczki w tej samej dobie (zawor i renty licza w tym samym bloku dobowym, stanu nie ma). Wartosci z wyrownaniem sa we
    /// WLASNYCH kluczach, nie w zmianie domyslnych TownRentLordShare / CrownRentWeight*: jeden klucz nie utrzyma dwoch wartosci (z kiesa i bez), a nowe klucze
    /// omijaja pulapke MCM (CLAUDE.md 7 - Armoury.json nadpisuje domyslne). Gracz - ta sama regula (zawor jego miast, renta jego rodu).
    /// Zloto: te same przelewy w innych proporcjach - zawor: kasa miasta -> pan (GiveGoldAction) i kasa -> skarbiec (pozycja ksiegi M13 MTownCrown);
    /// renty: skarbiec -> glowa rodu (blok korony MCrown). MoneyLedger bez nowych indeksow.
    /// Linia dzienna "Wyrownanie panow zamkow (E5/B3)" po rentach 180 (dzien gry): stan, wartosci czynne, zawor i renty dnia wedlug rodzaju rodu (tylko odczyt).
    /// </summary>
    internal static class LordsEvenUp
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();

        /// <summary>Konstruktor ArmouryBehavior (nowa kampania i kazde wczytanie): licznik potkniec i zapamietane bledy. Innego stanu nie ma.</summary>
        internal static void Reset() { _stumbles = 0; _err.Clear(); }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("LordsEvenUp." + where, e); } catch { }
        }

        /// <summary>Hak B1 (v2 K3): kiesa ludu czynna. Do B1 zawsze false - kiesy ludu nie ma (wyrownanie = stan sprzed paczki); B1 zamienia na
        /// TownFolk.Live - jeden przelacznik kiesy z PLAN-KODU-E5B1 pkt 1 (TownFolkLive i TownFolkMode >= 1 i A1 cofa zakupy, TownPurse.NoMint), NIE na
        /// sam warunek z ustawien: bez A1 kiesy nie ma, a wyrownanie byloby czynne (K3). Ta sama funkcja w KingdomTreasury.WtFolkLive i CourtFull.FolkShare.</summary>
        private static bool FolkLive { get { return false; } }

        private static bool LiveIn(Settings s) { return s != null && s.CastleLordsEvenUp && FolkLive; }

        /// <summary>Wyrownanie czynne: klucz CastleLordsEvenUp i kiesa ludu czynna.</summary>
        internal static bool Live { get { return LiveIn(Settings.Current); } }

        /// <summary>Udzial pana w zaworze kasy miasta rodu w krolestwie (0..1): z wyrownaniem TownRentLordShareWithFolk, bez - TownRentLordShare.
        /// Miasto bez krolestwa (calosc panu) rozstrzyga wolajacy (TownPurse.LordShare).</summary>
        internal static float TownLordShare(Settings s)
        {
            if (s == null) return 1f;
            return ValveSplit.Unit(LiveIn(s) ? s.TownRentLordShareWithFolk : s.TownRentLordShare);
        }

        private static float W(float v) { return float.IsNaN(v) || v <= 0f ? 0f : v; }

        /// <summary>Waga renty korony za miasto: z wyrownaniem CrownRentWeightTownWithFolk (2), bez - CrownRentWeightTown (3).</summary>
        internal static float WeightTown(Settings s) { return s == null ? 0f : W(LiveIn(s) ? s.CrownRentWeightTownWithFolk : s.CrownRentWeightTown); }

        /// <summary>Waga renty korony za zamek: z wyrownaniem CrownRentWeightCastleWithFolk (1.5), bez - CrownRentWeightCastle (1).</summary>
        internal static float WeightCastle(Settings s) { return s == null ? 0f : W(LiveIn(s) ? s.CrownRentWeightCastleWithFolk : s.CrownRentWeightCastle); }

        /// <summary>Waga renty korony za wies: CrownRentWeightVillage (0.25) - z wyrownaniem i bez (Z15-5 zostawia wies bez zmian).</summary>
        internal static float WeightVillage(Settings s) { return s == null ? 0f : W(s.CrownRentWeightVillage); }

        private static string Pct(double part, double whole) { return whole > 0 ? (100.0 * part / whole).ToString("0.0", Inv) : "0.0"; }
        private static string Wt(float v) { return v.ToString("0.##", Inv); }

        // ------------------------------------------------------------ raz na dobe, po CrownRents.Daily (wiersze rent dnia czynne) - tylko linia, zlota nie rusza
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null || !s.LogEnabled) return;
            try
            {
                int today = (int)CampaignTime.Now.ToDays;
                bool key = s.CastleLordsEvenUp, folk = FolkLive, live = key && folk;
                float lordNow = TownLordShare(s);
                float lordWith = ValveSplit.Unit(s.TownRentLordShareWithFolk), lordWithout = ValveSplit.Unit(s.TownRentLordShare);
                // renty dnia wedlug rodzaju rodu (jak budzet-rodow.csv: rod z miastem "miasto", sam zamek "zamek", gracz osobno)
                long rTotal = 0, rCastle = 0, rTown = 0, rPlayer = 0;
                int nCastle = 0, nTown = 0, pCastle = 0, pTown = 0;
                double wCastle = 0, wTown = 0, wPlayer = 0;
                foreach (var c in Clan.All)
                {
                    try
                    {
                        long paid;
                        if (c == null || !CrownRents.RowOf(c, out paid)) continue;   // rod bez wiersza 180 (bez lenna, najemnik, poza krolestwem, 180 wylaczone)
                        double w = CrownRents.Weight(c, s);
                        rTotal += paid;
                        if (c == Clan.PlayerClan) { rPlayer += paid; wPlayer += w; continue; }
                        int towns = 0, castles = 0;
                        var fiefs = c.Fiefs;
                        if (fiefs != null)
                            for (int i = 0; i < fiefs.Count; i++)
                            {
                                var f = fiefs[i];
                                if (f == null) continue;
                                if (f.IsTown) towns++; else if (f.IsCastle) castles++;
                            }
                        if (towns > 0) { nTown++; wTown += w; rTown += paid; if (paid > 0) pTown++; }
                        else if (castles > 0) { nCastle++; wCastle += w; rCastle += paid; if (paid > 0) pCastle++; }
                    }
                    catch (Exception e) { Stumble("Daily(rod)", e); }
                }
                double wAll = wCastle + wTown + wPlayer;
                bool valve = TownPurse.ValveLive;
                long vLord = TownPurse.LastLordPaid, vBaseK = TownPurse.LastLordBaseK, vCrown = TownPurse.LastCrownPaid;
                int stum = _stumbles; _stumbles = 0;
                var sb = new StringBuilder(1024);
                sb.Append("Wyrownanie panow zamkow (E5/B3): dzien ").Append(today)
                  .Append(" | wylacznik Castle Lords Even Up ").Append(key ? "tak" : "nie")
                  .Append(", kiesa ludu (B1) ").Append(folk ? "czynna" : "nieczynna")
                  .Append(" -> wyrownanie ").Append(live ? "CZYNNE" : "NIECZYNNE (udzial zaworu i wagi rent sprzed B3 - K3: wyrownanie tylko z czynna kiesa ludu)")
                  .Append(" | zawor kas miast (A1 ").Append(valve ? "czynny" : "nieczynny - zawor nie biegnie").Append("): panu ").Append((100.0 * lordNow).ToString("0.0", Inv))
                  .Append("%, koronie ").Append((100.0 * (1f - lordNow)).ToString("0.0", Inv)).Append("% (z wyrownaniem ").Append((100.0 * lordWith).ToString("0.0", Inv))
                  .Append("%, bez ").Append((100.0 * lordWithout).ToString("0.0", Inv)).Append("%; miasto rodu bez krolestwa - calosc panu)")
                  .Append(" | wagi rent korony (180 ").Append(CrownRents.On ? "czynne" : "nieczynne").Append("): miasto ").Append(Wt(WeightTown(s)))
                  .Append(", zamek ").Append(Wt(WeightCastle(s))).Append(", wies ").Append(Wt(WeightVillage(s)))
                  .Append(" (z wyrownaniem ").Append(Wt(W(s.CrownRentWeightTownWithFolk))).Append(" / ").Append(Wt(W(s.CrownRentWeightCastleWithFolk))).Append(" / ").Append(Wt(W(s.CrownRentWeightVillage)))
                  .Append(", bez ").Append(Wt(W(s.CrownRentWeightTown))).Append(" / ").Append(Wt(W(s.CrownRentWeightCastle))).Append(" / ").Append(Wt(W(s.CrownRentWeightVillage))).Append(')')
                  .Append(" | zawor dzis: panom ").Append(vLord).Append(" (w krolestwach przed dekretem ").Append(vBaseK).Append(") + koronie ").Append(vCrown)
                  .Append(" - udzial korony ").Append(vBaseK + vCrown > 0 ? ((double)vCrown / (vBaseK + vCrown)).ToString("0.000", Inv) : "-")
                  .Append(" | renty korony dzis ").Append(rTotal).Append(": rody samych zamkow ").Append(rCastle).Append(" (rodow ").Append(nCastle).Append(", z renta ").Append(pCastle)
                  .Append("; wag ").Append(Pct(wCastle, wAll)).Append("%, rent ").Append(Pct(rCastle, rTotal)).Append("%)")
                  .Append(", rody z miastem ").Append(rTown).Append(" (rodow ").Append(nTown).Append(", z renta ").Append(pTown)
                  .Append("; wag ").Append(Pct(wTown, wAll)).Append("%, rent ").Append(Pct(rTown, rTotal)).Append("%)")
                  .Append(", rod gracza ").Append(rPlayer).Append(" (wag ").Append(Pct(wPlayer, wAll)).Append("%)")
                  .Append(" | gracz: zawor (czesc pana) ").Append(TownPurse.LastPlayerPaid).Append(", renta ").Append(rPlayer)
                  .Append(" | potkniecia ").Append(stum).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
        }
    }
}
