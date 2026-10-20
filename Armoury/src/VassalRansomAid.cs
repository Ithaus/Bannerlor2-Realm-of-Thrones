using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// R3-01d - POMOC WASALI NA OKUP PANA (POMYSLY-35 nr 2, Jeff 20.10 "tak"; audyt 04 N1, audyt 05 D4 WARIANT 1 - bez podatku nadzwyczajnego, ktory Jeff
    /// odlozyl razem z kasami miast). Klucz VassalRansomAid (+ VassalRansomAidShare). Magna Carta kl. 12: "rozsadna pomoc" wasali na okup ciala pana.
    ///  - KIEDY: okup 178 (Ransom178.Execute - AI-AI i kurier) glowy rodu AI albo krola AI, gdy gotowka rodziny (krola - skarbca ponad rezerwe) nie pokrywa
    ///    calego okupu, czyli zostaje dlug okupu (po pulapie dlugu 364 D - wasale doplacaja do tego, co rod naprawde bylby winien). Zwykly lord (nie glowa) - bez pomocy.
    ///  - KTO DAJE: rody krolestwa jenca z lennami (miasto = 2, zamek = 1), bez rodu jenca, bez rodu krola (pan nie jest wasalem), bez rodu gracza (BK pyta
    ///    gracza sam - RansomDuty), bez najemnikow; rod musi miec budzet 166 (ClanBudget.ChestOf: nie dluznik, nie w zajeciu, nie Straz bez zoldu) i zloto
    ///    rodziny (dzis, kiesy glowy i doroslych) ponad rezerwe R.
    ///  - ILE: brak B dzielony wedlug lenn miedzy rody, ktore moga dac: udzial_i = B x lenna_i / suma lenn; dar_i = min(udzial_i, VassalRansomAidShare x (G_i - R_i)).
    ///    Jedno przejscie (bez dzielenia reszty od nowa) - czego wasale nie dadza, zostaje dlugiem okupu jak dotad.
    ///  - SKAD ZLOTO: kiesy rodziny wasala (glowa, potem dorosli lordowie; kazdy najwyzej do podlogi FamilyPurseFloor) -> ta sama droga co gotowka okupu
    ///    (Ransom178.Pay: glowa rodu porywacza albo skarbiec korony zdobywcy; 1/9 korony porywacza liczy sie od calej gotowki).
    /// Platnik -> odbiorca: kiesy wasali -> odbiorca okupu. Zadnego zlota z niczego. Bez zapisu (same liczniki doby). Linia doby "Pomoc wasali (R3-01d)".
    /// </summary>
    internal static class VassalRansomAid
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.VassalRansomAid && ClanBudget.On; } }

        // liczniki doby
        private static int _dNeedN, _dAidN, _dNoneN, _dPayersN, _dPoorN, _stumbles;
        private static long _dNeed, _dAid;
        private static string _dMax; private static long _dMaxV;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static readonly List<Clan> _cl = new List<Clan>();
        private static readonly List<double> _w = new List<double>(), _cap = new List<double>();

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("VassalRansomAid." + where, e); } catch { }
        }

        private static string Name(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Name(Clan c) { try { return c != null && c.Name != null ? c.Name.ToString() : "-"; } catch { return "?"; } }

        /// <summary>Lenna rodu: miasto 2, zamek 1.</summary>
        private static int FiefWeight(Clan c)
        {
            int w = 0;
            var f = c.Fiefs;
            if (f == null) return 0;
            for (int i = 0; i < f.Count; i++) { var t = f[i]; if (t == null) continue; w += t.IsTown ? 2 : 1; }
            return w;
        }

        /// <summary>
        /// Zloto z kies rodziny AI: glowa, potem dorosli lordowie rodu - kazdy najwyzej do podlogi floor; skip - nie placi (panna przy posagu). Zwraca zebrane
        /// (zdjete z kies). Wspolne z posagiem (AiDowry).
        /// </summary>
        internal static long TakeFromFamily(Clan c, long want, long floor, Hero skip = null)
        {
            long got = 0;
            if (c == null || want <= 0) return 0;
            var lead = c.Leader;
            if (lead != null && lead != skip && lead.IsAlive && lead != Hero.MainHero) got += TakeFrom(lead, want - got, floor);
            if (got >= want) return got;
            foreach (var h in c.Heroes)
            {
                if (got >= want) break;
                if (h == null || h == lead || h == skip || h == Hero.MainHero || !h.IsAlive || h.IsChild || h.Clan != c || !h.IsLord) continue;
                got += TakeFrom(h, want - got, floor);
            }
            return got;
        }

        private static long TakeFrom(Hero h, long want, long floor)
        {
            long x = Math.Min(want, Math.Max(0, (long)h.Gold - Math.Max(0, floor)));
            if (x <= 0) return 0;
            h.ChangeHeroGold(-(int)Math.Min(int.MaxValue, x));
            CirculationWindows.NoteHeroGold(h, -x);
            return x;
        }

        /// <summary>
        /// Ransom178.Execute: brak gotowki "shortfall" na okup jenca (glowa rodu albo krol). Zbiera dary wasali (zdjete z ich kies) i zwraca ich sume - wolajacy
        /// doklada ja do gotowki dla odbiorcy okupu i odejmuje od dlugu. 0 - bez pomocy (klucz, nie glowa, gracz, brak wasali z nadwyzka).
        /// </summary>
        internal static long Help(Hero captive, Clan captorClan, long shortfall)
        {
            if (shortfall <= 0 || captive == null) return 0;
            try
            {
                if (!On) return 0;
                var clan = captive.Clan;
                if (clan == null || clan == Clan.PlayerClan || clan.Leader != captive || clan.IsUnderMercenaryService) return 0;
                var k = clan.Kingdom;
                if (k == null || k.IsEliminated) return 0;
                var s = Settings.Current;
                double share = Math.Max(0f, Math.Min(1f, s.VassalRansomAidShare));
                if (share <= 0) return 0;
                _dNeedN++; _dNeed += shortfall;

                _cl.Clear(); _w.Clear(); _cap.Clear();
                double sumW = 0;
                int poor = 0;
                var ruling = k.RulingClan;
                foreach (var c in k.Clans)
                {
                    if (c == null || c == clan || c == ruling || c == captorClan || c == Clan.PlayerClan || c.IsEliminated || c.IsUnderMercenaryService) continue;
                    int w = FiefWeight(c);
                    if (w <= 0) continue;
                    double G, R; bool war;
                    if (!ClanBudget.ChestOf(c, out G, out R, out war)) { poor++; continue; }
                    double live = ClanIncomeBook.FamilyGoldOf(c);
                    double above = Math.Min(G, live) - R;   // budzet z rana albo kiesy dzis - mniejsze (nic ponad to, co rod naprawde ma)
                    if (above <= 0) { poor++; continue; }
                    _cl.Add(c); _w.Add(w); _cap.Add(share * above);
                    sumW += w;
                }
                _dPoorN += poor;
                if (_cl.Count == 0 || sumW <= 0) { _dNoneN++; return 0; }

                long floor = Math.Max(0, s.FamilyPurseFloor), total = 0;
                int payers = 0;
                string topName = null; long topV = 0;
                var parts = new StringBuilder(256);
                for (int i = 0; i < _cl.Count; i++)
                {
                    long want = (long)Math.Min(shortfall * _w[i] / sumW, _cap[i]);
                    if (want <= 0) continue;
                    long got = TakeFromFamily(_cl[i], want, floor);
                    if (got <= 0) continue;
                    total += got; payers++;
                    if (got > topV) { topV = got; topName = Name(_cl[i]); }
                    if (payers <= 8) parts.Append(payers > 1 ? ", " : "").Append(Name(_cl[i])).Append(' ').Append(got);
                }
                _cl.Clear(); _w.Clear(); _cap.Clear();
                if (total <= 0) { _dNoneN++; return 0; }
                if (total > shortfall) total = shortfall;   // zaokraglenia - nigdy wiecej niz brak (nadmiaru i tak nie ma: suma udzialow = brak)
                _dAidN++; _dAid += total; _dPayersN += payers;
                if (total > _dMaxV) { _dMaxV = total; _dMax = Name(clan) + " (" + Name(captive) + ") " + total + " od " + payers + " rodow"; }
                Log.Info("Pomoc wasali (R3-01d): " + Name(captive) + " (" + (k.Leader == captive ? "krol" : "glowa rodu") + ", " + Name(clan) + ", " + k.Name + ") - brak gotowki na okup "
                         + shortfall + " zl; wasale dali " + total + " zl (" + payers + " rodow: " + parts + (payers > 8 ? ", ..." : "") + "), zostaje dlug " + (shortfall - total) + ".");
                return total;
            }
            catch (Exception e) { Stumble("Help", e); _cl.Clear(); _w.Clear(); _cap.Clear(); return 0; }
        }

        /// <summary>Linia doby "Pomoc wasali (R3-01d)" (po linii "Okupy (178)"); zeruje liczniki.</summary>
        internal static void Report()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.LogEnabled || Campaign.Current == null) return;
                if (!On && _dNeedN == 0) return;
                Log.Info("Pomoc wasali (R3-01d): dzien " + (int)CampaignTime.Now.ToDays + " | okupow glow rodow i krolow AI z brakiem gotowki " + _dNeedN + " (brak razem " + _dNeed + " zl)"
                         + " | z pomoca wasali " + _dAidN + ": wasale dali razem " + _dAid + " zl (placacych rodow razem " + _dPayersN + "; najwieksza: " + (_dMax ?? "-") + ")"
                         + " | bez pomocy (zaden wasal z lennami nie ma nadwyzki ponad rezerwe) " + _dNoneN + "; wasali pominietych - bez budzetu, dluznik albo ponizej rezerwy " + _dPoorN
                         + (On ? "" : " (pomoc wasali wylaczona)") + (_stumbles > 0 ? " | potkniecia " + _stumbles : "") + ".");
            }
            catch (Exception e) { Stumble("Report", e); }
            finally { _dNeedN = _dAidN = _dNoneN = _dPayersN = _dPoorN = 0; _dNeed = _dAid = 0; _dMax = null; _dMaxV = 0; }
        }
    }
}
