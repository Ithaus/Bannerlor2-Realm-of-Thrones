using System;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// P2 (OBIEG KIESY LUDU, docs/kiesa-ludu/PROJEKT-OBIEG-KIESY-LUDU-2026-10-13.md rozdz. 3.1 szczebel 3, 5, 8 wiersz P2; PROJEKT-INWESTYCJE B1 bez trzeciej pana;
    /// decyzja Jeffa 13.10) - STRONA LUDU robot miejskich. Regula i zakup - BuildFunding.FolkWorks; tu tylko kiesy:
    ///  - WorksPurse: ile lud osady moze dzis dac na roboty - gmina (budzet robot z kroku 4 doby ludu, f.GmWorks, najwyzej kiesa gminy) + z kazdej klasy
    ///    FolkWorksShare (2%) nadwyzki TERAZ (ta sama miara co W7: ponad zapas dni po daninie P1, minus oferta na jutro - LevyRoom) + pulap rak
    ///    (FolkWorksHands x glowy wyrobnikow x dniowka; zamek - glowy podzamcza).
    ///  - WorksSpend: zaplata - najpierw gmina, potem klasy proporcjonalnie do swoich czesci; material (kiesy -> kasa targu, sztuki zdejmuje BuildFunding),
    ///    place do kies W 60% / R 40% tego miasta (zamek - podzamcze) jako zarobek okna (podstawa jutrzejszych podatkow) - przelew wewnatrz ludu.
    ///    Ksiega kies: _ctlOut tylko material (place wracaja do ludu tej osady) - kontrola zamknieta kies r = 0.
    ///  - WorksGminaRest: czego budowa nie wziela z budzetu gminy - place robot publicznych W/R po polowie (jak dotad krok 4, bez materialu bez sztuki).
    /// Tylko watek glowny (blok doby). Zapis gry: nic nowego.
    /// </summary>
    internal static partial class TownFolk
    {
        /// <summary>Trasa robot gminy przez BuildFunding (krok 4 doby ludu): klucz FolkBuildsTown i budowy za pieniadze (PaidConstruction).</summary>
        private static bool WorksRoute { get { var s = Settings.Current; return s != null && s.FolkBuildsTown && s.PaidConstruction; } }

        /// <summary>Kiesy na roboty jednej osady tej doby (wypelnia WorksPurse).</summary>
        internal sealed class WorksPay
        {
            public long G;                                  // z kiesy gminy (budzet robot gminy tej doby)
            public readonly long[] C = new long[3];         // z klas W, R, P (zamek: [0] podzamcze) - FolkWorksShare x nadwyzka teraz
            public double Heads;                            // glowy wyrobnikow (zamek: podzamcze) - pulap rak
            public long UsedG;                              // zaplacone z gminy (WorksSpend)
            public readonly long[] UsedC = new long[3];     // zaplacone z klas
            public long Total { get { return G + C[0] + C[1] + C[2]; } }
            public void Clear() { G = 0; C[0] = C[1] = C[2] = 0; Heads = 0; UsedG = 0; UsedC[0] = UsedC[1] = UsedC[2] = 0; }
        }

        private static readonly double[] _wRoom = new double[3];

        /// <summary>P2: kiesy ludu osady na roboty tej doby. false - kiesa nieczynna, wies, zamek bez zaworu 110 albo doba ludu tej osady dzis nie biegla.</summary>
        internal static bool WorksPurse(Settlement st, double share, WorksPay p)
        {
            if (p == null) return false;
            p.Clear();
            try
            {
                if (st == null || !Live) return false;
                Folk f;
                if (!_f.TryGetValue(st, out f) || f == null || f.Kind < 0 || f.Kind > 1) return false;
                if (f.Kind == 1 && !CastleLive) return false;
                if (_liveDay < 0 || f.ADay != _liveDay) return false;
                if (f.Kind == 0) p.G = Math.Max(0, Math.Min(f.GmWorks, f.KG));
                share = share < 0 ? 0 : (share > 1 ? 1 : share);
                if (share > 0 && LevyRoom(st, _wRoom))
                    for (int c = 0; c < 3; c++) p.C[c] = (long)Math.Floor(share * Math.Max(0, _wRoom[c]));
                p.Heads = f.Kind == 1 ? Math.Max(0, CastleHeads(f)) : Math.Max(0, f.HW) * Math.Max(0, f.DR);
                if (double.IsNaN(p.Heads) || double.IsInfinity(p.Heads)) p.Heads = 0;
                return true;
            }
            catch (Exception e) { Stumble("WorksPurse", e); p.Clear(); return false; }
        }

        /// <summary>
        /// P2: zaplata robot - mat (material, juz zdjety z polki targu `market`) + wages (place). Zdejmuje najpierw z gminy, potem z klas proporcjonalnie do
        /// ich czesci (najwyzej czesc klasy i stan kiesy); material -> kasa targu (ChangeGold), place -> kiesy W 60% / R 40% (zamek: podzamcze) jako zarobek.
        /// Zwraca zaplacone razem (mniej niz mat + wages tylko przy rozjezdzie kies - wolajacy liczy kontrole). Nie rzuca.
        /// </summary>
        internal static long WorksSpend(Settlement st, Settlement market, long mat, long wages, WorksPay p)
        {
            try
            {
                if (st == null || p == null || mat < 0 || wages < 0 || mat + wages <= 0) return 0;
                Folk f;
                if (!_f.TryGetValue(st, out f) || f == null) return 0;
                long need = mat + wages, got = 0;
                long g = Math.Min(need, Math.Min(p.G - p.UsedG, Math.Max(0, f.KG)));
                if (g > 0) { f.KG -= g; p.UsedG += g; got += g; }
                long cs = p.C[0] - p.UsedC[0] + p.C[1] - p.UsedC[1] + p.C[2] - p.UsedC[2];
                long rest = need - got;
                if (rest > 0 && cs > 0)
                {
                    long left = rest;
                    for (int c = 0; c < 3 && left > 0; c++)
                    {
                        long room = p.C[c] - p.UsedC[c];
                        if (room <= 0) continue;
                        long x = Math.Min(room, (long)Math.Ceiling((double)rest * room / cs));
                        x = Math.Min(Math.Min(x, left), Math.Max(0, PurseOf(f, c)));
                        if (x <= 0) continue;
                        AddPurse(f, c, -x); p.UsedC[c] += x; got += x; left -= x;
                    }
                    for (int c = 0; c < 3 && left > 0; c++)   // reszta zaokraglen - z klas, ktore jeszcze maja czesc
                    {
                        long x = Math.Min(Math.Min(p.C[c] - p.UsedC[c], left), Math.Max(0, PurseOf(f, c)));
                        if (x <= 0) continue;
                        AddPurse(f, c, -x); p.UsedC[c] += x; got += x; left -= x;
                    }
                }
                // material: z kies do kasy targu (sztuki zdjal wolajacy); place: zarobek W/R tej osady (przelew wewnatrz ludu)
                long m = Math.Min(mat, got), w = got - m;
                if (m > 0 && market != null && market.Town != null) { market.Town.ChangeGold((int)Math.Min(int.MaxValue, m)); _ctlOut += m; f.LDayOut += m; }
                else if (m > 0) { w += m; m = 0; }   // bez targu (nie powinno sie zdarzyc - wolajacy kupil material) - wszystko wraca jako place
                if (w > 0) { f.LDayOut += w; Credit(f, w, KBuildLabour); }
                return got;
            }
            catch (Exception e) { Stumble("WorksSpend", e); return 0; }
        }

        /// <summary>P2: reszta budzetu robot gminy tej doby (budowa jej nie wziela albo nie bylo budowy) - place robot publicznych W/R po polowie, jak krok 4
        /// sprzed P2 bez materialu. Zwraca przelane.</summary>
        internal static long WorksGminaRest(Settlement st, WorksPay p)
        {
            try
            {
                Folk f;
                if (st == null || !_f.TryGetValue(st, out f) || f == null || f.Kind != 0) return 0;
                long used = p != null ? p.UsedG : 0;
                long rest = Math.Min(f.GmWorks - used, Math.Max(0, f.KG));
                f.GmWorks = 0;
                if (rest <= 0) return 0;
                long w1 = rest / 2;
                return Move(f, 3, 0, rest - w1) + Move(f, 3, 1, w1);
            }
            catch (Exception e) { Stumble("WorksGminaRest", e); return 0; }
        }

        /// <summary>P2: budzet robot gminy tej doby jeszcze nie wydany (miasta, ktorych petla robot nie odwiedzila - bez budowy, oblezone) - place robot
        /// publicznych; odwiedzone maja juz GmWorks = 0 (WorksGminaRest).</summary>
        internal static void WorksGminaSweep(ref long moved, ref int places)
        {
            for (int i = 0; i < _fk.Count; i++)
            {
                var f = _fk[i];
                if (f == null || f.Kind != 0 || f.GmWorks <= 0 || f.St == null) continue;
                long x = WorksGminaRest(f.St, null);
                if (x > 0) { moved += x; places++; }
            }
        }
    }
}
