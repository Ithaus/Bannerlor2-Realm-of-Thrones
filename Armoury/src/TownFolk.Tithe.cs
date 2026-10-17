using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// E5/C3 - DZIESIECINA OD RAZU (decyzja Jeffa 12.10 10:15, STAN-PRAC; projekt etapu 5 R21, C3; tempo nadwyzki R11/R13 - decyzja (3)).
    ///  - Wies: TitheVillageShare (5%) utargu taborow okna (f.VHanded - ta sama definicja utargu co ksiega, NoteVillageReturn) z czesci wsi - najwyzej stan
    ///    kiesy wsi, raz na dobe w bloku TownFolk.Daily (migawka M18 ksiegi), przed miastami. Niezaplacone przepada (bez dlugu).
    ///  - Lud miast: od zarobku okna (ta sama podstawa co podatek pana, krok 1 TownLive) W 1% / R 2% / P 3%, najwyzej stan kiesy klasy.
    ///  - Kaplan osady = zywy duchowny BK w slowniku Religion.clergy dowolnej religii (odczyt refleksja raz na dobe). NIE Religion.GetClergyman - ten
    ///    tworzy bohatera, gdy kaplana brak (BK wola go sam w ReligionData.Update dla wiary panujacej). Kaplan jest posrednikiem: zloto nie siedzi w jego kiesie.
    ///  - Z kaplanem: 50% jalmuzna (kiesa W miasta / zostaje w kiesie wsi), 50% budowa septow: 75% place ludu (Credit KBuildLabour W60/R40; wies - lud
    ///    miasta targowego: murarze i ciesle), 25% material do kasy miasta (tego samego / targowego wsi) jako sprzedaz kupcow MixBuild (podstawa robocizny
    ///    tej doby; jak material robot gminy - bez zdejmowania towaru z polki).
    ///  - Bez kaplana (odbiorca zapasowy, nic nie znika): miasto - do kasy miasta; wies - zostaje w kiesie wsi. Wies bez miasta targowego (albo targ-zamek przy
    ///    zaworze zamku wylaczonym) - czesc budowy zostaje w kiesie wsi.
    ///  - Odczyt BK nieudany (brak typow albo wyjatek doby) - dziesiecina tej doby pominieta (licznik w linii), bez globalnego wylacznika.
    /// </summary>
    internal static partial class TownFolk
    {
        private const double TitheW = 0.01, TitheR = 0.02, TitheP = 0.03;    // R21: od zarobku ludu W / R / P
        private const double TitheAlms = 0.5, TitheWage = 0.75;              // polowa jalmuzna; z budowy 75% place, 25% material

        private static int _relInit;                                         // 0 nie sprawdzone, 1 typy BK znalezione, 2 brak
        private static string _relWhy = "nie sprawdzone";
        private static PropertyInfo _relCfgInst, _relMgr, _clHero;
        private static MethodInfo _relGet;
        private static FieldInfo _relClergy;
        private static HashSet<Settlement> _priests;                         // osady z zywym kaplanem BK - ta doba (null = dziesiecina tej doby pominieta)
        private static int _priestFail, _priestFailSess;

        private static void ResetTithe() { _priests = null; _priestFail = 0; _priestFailSess = 0; }

        /// <summary>Tempo wydawania nadwyzki ponad zapas (lud miast W/R/P i wsie) - FolkSurplusSpendShare, przyciete 0..1.</summary>
        private static double SpendRate(Settings s) { return s != null ? Unit(s.FolkSurplusSpendShare) : SurplusRate; }
        private static double TitheVillage(Settings s) { return s != null ? Math.Max(0f, Math.Min(0.2f, s.TitheVillageShare)) : 0; }
        private static bool TitheKey { get { var s = Settings.Current; return s != null && s.TempleTithe; } }

        private static string TitheBkState() { if (_relInit == 0) InitRel(); return _relWhy; }

        /// <summary>Typy BK (refleksja, raz na sesje): BannerKingsConfig.Instance.ReligionsManager.GetReligions(), Religion.clergy, Clergyman.Hero.</summary>
        private static void InitRel()
        {
            try
            {
                var tCfg = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                var tRel = AccessTools.TypeByName("BannerKings.Managers.Institutions.Religions.Religion");
                var tCl = AccessTools.TypeByName("BannerKings.Managers.Institutions.Religions.Clergyman");
                _relCfgInst = tCfg != null ? AccessTools.Property(tCfg, "Instance") : null;
                _relMgr = tCfg != null ? AccessTools.Property(tCfg, "ReligionsManager") : null;
                _relGet = _relMgr != null ? AccessTools.Method(_relMgr.PropertyType, "GetReligions", Type.EmptyTypes) : null;
                _relClergy = tRel != null ? AccessTools.Field(tRel, "clergy") : null;
                _clHero = tCl != null ? AccessTools.Property(tCl, "Hero") : null;
                string miss = _relCfgInst == null || _relMgr == null ? "BannerKingsConfig.Instance/ReligionsManager" : _relGet == null ? "ReligionsManager.GetReligions"
                            : _relClergy == null ? "Religion.clergy" : _clHero == null ? "Clergyman.Hero" : null;
                _relInit = miss == null ? 1 : 2;
                _relWhy = miss == null ? "odczyt ok (Religion.clergy)" : "BRAK " + miss + " - dziesiecina nie biegnie";
            }
            catch (Exception e) { _relInit = 2; _relWhy = "BRAK (blad: " + e.Message + ") - dziesiecina nie biegnie"; }
        }

        /// <summary>Poczatek doby: zbior osad z zywym kaplanem BK (null - dziesiecina tej doby pominieta: klucz wylaczony, brak typow BK albo wyjatek odczytu).</summary>
        private static void TitheBegin()
        {
            _priests = null;
            if (!TitheKey) return;
            if (_relInit == 0) InitRel();
            if (_relInit != 1) return;
            try
            {
                var cfg = _relCfgInst.GetValue(null, null);
                var mgr = cfg != null ? _relMgr.GetValue(cfg, null) : null;
                var rels = mgr != null ? _relGet.Invoke(mgr, null) as IEnumerable : null;
                if (rels == null) { _priestFail++; _priestFailSess++; return; }
                var set = new HashSet<Settlement>();
                foreach (var r in rels)
                {
                    var dict = r != null ? _relClergy.GetValue(r) as IDictionary : null;
                    if (dict == null) continue;
                    foreach (DictionaryEntry e in dict)
                    {
                        var st = e.Key as Settlement;
                        if (st == null || e.Value == null) continue;
                        var h = _clHero.GetValue(e.Value, null) as Hero;
                        if (h != null && h.IsAlive) set.Add(st);
                    }
                }
                _priests = set;
            }
            catch (Exception e) { _priests = null; _priestFail++; _priestFailSess++; Stumble("TitheBegin(odczyt BK)", e); }
        }

        /// <summary>Zdejmuje z kiesy klasy `c` dziesiecine od zarobku `earned` x stawka - najwyzej stan kiesy. Zwraca zdjete.</summary>
        private static long TakeTithe(Folk f, int c, long earned, double rate)
        {
            if (earned <= 0) return 0;
            long x = Math.Min((long)Math.Round(earned * rate), Math.Max(0, PurseOf(f, c)));
            if (x <= 0) return 0;
            AddPurse(f, c, -x);
            return x;
        }

        /// <summary>Krok 1b TownLive: dziesiecina ludu miasta od zarobku okna (po podatkach pana i korony). Przelewy wewnatrz ludu (jalmuzna, place) bez kontroli
        /// zamknietej; material i brak kaplana - kiesa -> kasa miasta (_ctlOut).</summary>
        private static void TownTithe(Folk f, Town town, long eW, long eR, long eP, LDay d)
        {
            if (_priests == null) return;
            long t0 = TakeTithe(f, 0, eW, TitheW), t1 = TakeTithe(f, 1, eR, TitheR), t2 = TakeTithe(f, 2, eP, TitheP), t = t0 + t1 + t2;
            if (t <= 0) return;
            d.TiW += t0; d.TiR += t1; d.TiP += t2;
            if (!_priests.Contains(f.St))
            {
                town.ChangeGold((int)Math.Min(int.MaxValue, t));
                _ctlOut += t; f.LDayOut += t; d.TiNoPriestT += t; d.TiNoPriestN++;
                return;
            }
            d.TiPriestN++;
            long alms = (long)Math.Round(t * TitheAlms), build = t - alms, wage = (long)Math.Round(build * TitheWage), mat = build - wage;
            f.KW += alms; d.TiAlmsT += alms;                                 // jalmuzna - dar, nie zarobek (bez okna podatku)
            if (wage > 0) { Credit(f, wage, KBuildLabour); f.LDayIn -= wage; d.TiWage += wage; }   // place budowy septu - zarobek (podatek jutro), przelew wewnatrz ludu
            if (mat > 0)
            {
                town.ChangeGold((int)Math.Min(int.MaxValue, mat));
                f.Sale[MixBuild] += mat; _ctlOut += mat; f.LDayOut += mat; d.TiMat += mat;
            }
        }

        /// <summary>Przed miastami: dziesiecina wsi z utargu okna (f.VHanded). Jalmuzna zostaje w kiesie wsi; budowa septu - kiesa wsi -> lud (place) i kasa
        /// (material) miasta targowego. Bez kaplana - zostaje w kiesie wsi.</summary>
        private static void VillageTithe(LDay d)
        {
            if (_priests == null) return;
            double share = TitheVillage(Settings.Current);
            if (!(share > 0)) return;
            foreach (var f in _f.Values)
            {
                if (f.Kind != 2 || !(f.VHanded > 0)) continue;
                if (DsVillage.CartDuesOn(f.St)) continue;                     // DS (F1): dziesiecina wsi DS zdjeta juz przy wozie (TitheVillageAtCart)
                try
                {
                    var vil = f.St.Village;
                    if (vil == null) continue;
                    long due = (long)Math.Round(f.VHanded * share);
                    if (due <= 0) continue;
                    d.TiVDue += due;
                    long x = Math.Min(due, Math.Max(0, vil.Gold));
                    if (x <= 0) continue;
                    d.TiV += x; d.TiVN++;
                    if (!_priests.Contains(f.St)) { d.TiNoPriestV += x; d.TiNoPriestN++; continue; }
                    d.TiPriestN++;
                    long alms = (long)Math.Round(x * TitheAlms), build = x - alms;
                    d.TiAlmsV += alms;
                    var mk = vil.TradeBound ?? vil.Bound;
                    // recenzja: TryGetValue jak w Villages - Get() dopisuje do _f, a tu iterujemy _f.Values (brak wpisu targu = wyjatek MoveNext, reszta wsi bez dziesieciny)
                    Folk mf = null;
                    if (mk != null && mk.Town != null) _f.TryGetValue(mk, out mf);
                    if (mf == null || mf.Kind > 1 || (mf.Kind == 1 && !CastleLive) || build <= 0) { d.TiNoMarket += Math.Max(0, build); continue; }
                    int had = vil.Gold;
                    vil.ChangeGold(-(int)build);
                    long got = Math.Max(0, had - vil.Gold);
                    f.ZcTiV = got; f.ZcTiDay = _liveDay;                          // ZC0 (DailyLife): dziesiecina, ktora wyszla z kiesy wsi tej doby - tylko licznik
                    if (got < build) d.TiNoMarket += build - got;                  // czego kiesa nie oddala - zostalo w niej (linia: suma = przez kaplanow)
                    if (got <= 0) continue;
                    long wage = (long)Math.Round(got * TitheWage), mat = got - wage;
                    if (wage > 0) { Credit(mf, wage, KBuildLabour); _ctlIn += wage; d.TiWage += wage; }   // kiesa wsi -> kiesa ludu miasta targowego
                    if (mat > 0)
                    {
                        mk.Town.ChangeGold((int)mat);
                        mf.Sale[MixBuild] += mat; d.TiMat += mat;
                    }
                }
                catch (Exception e) { Stumble("VillageTithe(wies)", e); }
            }
        }

        /// <summary>
        /// DS (F1, DsVillage.DuesAtCart): dziesiecina wsi PRZY WOZIE - TitheVillageShare oddanego utargu `handed`, najwyzej czesc wsi `part` (przyrost kiesy w oknie
        /// powrotu). Ta sama regula co VillageTithe (kaplani z ostatniej doby ludu - _priests): polowa jalmuzna zostaje w kiesie wsi, budowa septow - kiesa wsi -> place
        /// ludu miasta targowego (75%, KBuildLabour) i material do jego kasy (25%, sprzedaz MixBuild); bez kaplana albo bez miasta targowego - zostaje w kiesie.
        /// Zwraca dziesiecine nalezna (zdjeta albo zostawiona); state: 0 klucz/brak, 1 bez kaplana, 2 bez miasta targowego, 3 zaplacona, 4 lista kaplanow doby nieznana
        /// (pominieta); fromPurse - zeszlo z kiesy wsi, received - przybylo odbiorcom (zmierzone; kontrola DS). Poza blokiem doby - przelewy liczone w oknie ludu
        /// (jak Earn: _ctlIn), material w "Przeplywy osad (przyczyny)" (N169Dues). Nie rzuca.
        /// </summary>
        internal static long TitheVillageAtCart(Village vil, int handed, long part, out long alms, out long wage, out long mat, out long fromPurse, out long received, out int state)
        {
            alms = wage = mat = fromPurse = received = 0; state = 0;
            try
            {
                if (vil == null || vil.Settlement == null || handed <= 0 || part <= 0 || !Live || OffMain() || !TitheKey) return 0;
                if (_priests == null) { state = 4; return 0; }
                double share = TitheVillage(Settings.Current);
                if (!(share > 0)) return 0;
                long x = Math.Min((long)Math.Round(handed * share), part);
                if (x <= 0) return 0;
                var st = vil.Settlement;
                if (!_priests.Contains(st)) { state = 1; return x; }
                alms = (long)Math.Round(x * TitheAlms);
                long build = x - alms;
                var mk = vil.TradeBound ?? vil.Bound;
                Folk mf = null;
                if (mk != null && mk.Town != null) _f.TryGetValue(mk, out mf);
                if (mf == null || mf.Kind > 1 || (mf.Kind == 1 && !CastleLive) || build <= 0) { state = 2; return x; }
                int had = vil.Gold;
                vil.ChangeGold(-(int)Math.Min(int.MaxValue, build));
                long got = Math.Max(0, (long)had - vil.Gold);
                if (got <= 0) { state = 2; return x; }
                state = 3; fromPurse = got;
                wage = (long)Math.Round(got * TitheWage); mat = got - wage;
                if (wage > 0)
                {
                    long k0 = mf.KW + mf.KR + mf.KP;
                    Credit(mf, wage, KBuildLabour); _ctlIn += wage;                 // kiesa wsi -> kiesa ludu miasta targowego
                    received += mf.KW + mf.KR + mf.KP - k0;                        // kontrola DS: odbiorca zmierzony
                }
                if (mat > 0)
                {
                    int t0 = mk.Town.Gold;
                    mk.Town.ChangeGold((int)mat);
                    received += (long)mk.Town.Gold - t0;
                    mf.Sale[MixBuild] += mat;
                    MoneyLedger.Note169(MoneyLedger.N169Dues, mk, (int)mat);   // kasa miasta targowego poza tickiem - nazwane w "Przeplywy osad (przyczyny)"
                }
                Folk vf;                                                            // TryGetValue, nie Get - bez dopisywania do _f poza doba ludu
                if (_f.TryGetValue(st, out vf) && vf != null) vf.DsTiCart += got;  // ZC0: dziesiecina, ktora wyszla z kiesy wsi (do ZcTiV w DailyLive)
                return x;
            }
            catch (Exception e) { Stumble("TitheVillageAtCart", e); return 0; }
        }

        /// <summary>DailyLive (przed VillageTithe): ZC0 - dziesiecina wsi DS zdjeta przy wozach od ostatniej doby ludu jako "dziesiecina tej doby" (ZcVillageTithe).</summary>
        private static void DsCartTitheZc()
        {
            foreach (var f in _f.Values)
            {
                if (f.Kind != 2 || f.DsTiCart <= 0) continue;
                f.ZcTiV = f.DsTiCart; f.ZcTiDay = _liveDay; f.DsTiCart = 0;
            }
        }

        /// <summary>Linia dobowa "Dziesiecina (E5/C3)" (null - klucz wylaczony).</summary>
        private static string TitheLine(int day, LDay d)
        {
            if (!TitheKey) return null;
            long town = d.TiW + d.TiR + d.TiP, all = d.TiV + town, viaPriest = all - d.TiNoPriestT - d.TiNoPriestV;
            return "Dziesiecina (E5/C3): dzien " + day + (_priests == null ? " | POMINIETA tej doby (" + _relWhy + "; nieudane odczyty kaplanow BK " + _priestFail + ", w sesji " + _priestFailSess + ")" : "")
                   + " | razem " + all + " = wsie " + d.TiV + " (" + d.TiVN + " wsi; nalezne " + d.TiVDue + " = " + F2(TitheVillage(Settings.Current)) + " utargu okna, niezaplacone " + Math.Max(0, d.TiVDue - d.TiV)
                   + ") + lud miast " + town + " (W " + d.TiW + " / R " + d.TiR + " / P " + d.TiP + ")"
                   + " | przez kaplanow " + viaPriest + " (osad " + d.TiPriestN + "): jalmuzna " + (d.TiAlmsT + d.TiAlmsV) + " (kiesy W miast " + d.TiAlmsT + ", kiesy wsi " + d.TiAlmsV
                   + "), budowa septow " + (d.TiWage + d.TiMat + d.TiNoMarket) + ": place ludu " + d.TiWage + ", material do kas miast " + d.TiMat + ", wsie bez miasta targowego (zostalo w kiesie) " + d.TiNoMarket
                   + " | bez kaplana " + (d.TiNoPriestT + d.TiNoPriestV) + " (osad " + d.TiNoPriestN + "): miasta do kasy " + d.TiNoPriestT + ", wsie - zostalo w kiesie " + d.TiNoPriestV
                   + " | kaplani BK: osad z zywym kaplanem " + (_priests != null ? _priests.Count.ToString() : "-") + ".";
        }
    }
}
