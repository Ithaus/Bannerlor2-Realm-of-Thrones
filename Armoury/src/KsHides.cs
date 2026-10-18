using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// f4-ksiega-i-skory (noc 17/18.10, koncowy przeglad ekonomii, pakiet P1 "Ksiega i skory") - SKORY Z UBOJU W MIESCIE WEDLUG TABELI SWIATA.
    /// Dowod (fin86, Armoury-2026-10-17_23-22-37.log, d109235): ksiega "Towary: hides" - rzemieslnicy BK 494 skor z 44 krow (x2), 79 swin (x1) i 327 owiec (x1),
    /// a tabela swiata KS (KsTab.HideOfLive: krowa 0.08, swinia 0.05, owca 0.10 kg skory na kg zywca; zywiec 200 / 120 / 40 kg, skora 10 kg) daje 1.6 / 0.6 / 0.4
    /// skory na sztuke = ok. 249 - miejska owca byla skorowana 2.5 raza "grubiej" niz ta sama owca we wsi KS (StartCalibration.Deliver: skory wlasnego uboju)
    /// i w przetworach miast ZC5 (SideRatio). Dwie reguly na jedno zjawisko; zapas skor rosl +223..+270 szt./d (38 tys. po 160 dobach), garbarze stali.
    /// Regula (klucz Ks Hides By Table, tylko przy KS zastosowanym i Ks Real Recipes - KsRecipes.HidesActive): kazda linia uboju (wsad: krowa, swinia, owca)
    /// z wyrobem skory ma ceil(skory na cykl) wywolan skory (KsRecipes.Ensure), a skora na wywolanie = skory na cykl / wywolania (<= 1) idzie do licznika OSADY;
    /// pelna sztuka wychodzi, gdy licznik dojdzie do 1, ulamek zostaje w pamieci (zapis arm_hide_frac, jak welna z uboju arm_wool_frac). Jeden licznik na osade
    /// dla wszystkich linii uboju (skora to skora). Dwie drogi tego samego licznika:
    ///  - rzemieslnicy BK ("artisans", ArtisanInputs.OutPost): cel wywolania = pelne sztuki z licznika po dopisaniu (cykle x skora na wywolanie); nadwyzka BK
    ///    zdjeta z polki ze zwrotem zaplaty BK do kasy miasta (ta sama droga co kazda sztuka bez wsadu), brak - Byproduct E8/B (kasa miasta placi tylko z nadwyzki);
    ///  - warsztaty nieukryte (rzeznik BK, takze gracza) - postfiks WorkshopsCampaignBehavior.GetItemsToProduce: z listy wyrobow cyklu zdjete pozycje skory ponad
    ///    pelne sztuki licznika (sztuki nie powstaja, nikt za nie nie placi); utarg cyklu w progu zysku gry mniejszy o oczekiwanie z tabeli (pozycje gry - skory
    ///    na cykl) x cena skory, niezalezne od licznika (recenzja); licznik zapisany dopiero po udanym cyklu (WorkshopTrade.CyclePostfix -> CycleEnd), cena
    ///    sprawiedliwa liczy udzial z prawdziwej liczby sztuk (CutFor).
    /// Tozsamosc towaru (linia doby "Skory z uboju (KS-S)"): nalezne wedlug tabeli = pelne sztuki + przyrost licznikow osad + zaokraglenia (roznica 0). Zloto:
    /// zwrot BK miedzy kapitalem rzemieslnikow i kasa miasta (roznica 0), u rzeznikow zadne zloto sie nie rusza. Bez gnicia skor (decyzja Jeffa nie zapadla).
    /// Poza regula (KS niezastosowany, klucz wylaczony) - stara droga co do linii: ArtisanTarget zwraca -1, ItemsPost nic nie zmienia.
    /// </summary>
    internal static class KsHides
    {
        private const float Eps = 0.0001f, Min = 0.001f, MaxFrac = 64f;
        private const int MaxSaveEntries = 4096;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // osada (StringId) -> ulamek skory (zapis arm_hide_frac)
        private static readonly Dictionary<string, float> _frac = new Dictionary<string, float>(StringComparer.Ordinal);

        // cykl warsztatu nieukrytego w toku (GetItemsToProduce -> CyclePostfix): licznik zapisany tylko po udanym cyklu
        private static Workshop _pW;
        private static string _pKey;
        private static float _pOld;
        private static double _pDue, _pRest;
        private static int _pKind, _pKeep, _pCut;
        private static long _pIncome, _pCutVal;   // obnizka progu zysku gry (oczekiwanie z tabeli) i wartosc pozycji zdjetych w tym cyklu (log)

        // liczniki doby (linia "Skory z uboju (KS-S)")
        private static readonly double[] _dDue = new double[3];
        private static double _dFracDelta, _dRound;
        private static int _dArtCalls, _dArtWhole, _dArtCut, _dArtShort, _dArtShortBy, _dArtShortUnsold;
        private static long _dArtRefund;
        private static int _dShopCycles, _dShopWhole, _dShopCut, _dShopFail, _dStumbles;
        private static long _dShopIncome, _dShopCutVal;
        private static readonly HashSet<string> _errOnce = new HashSet<string>();
        private static string _wire = "nie wpieta";

        /// <summary>Nowa gra / wczytanie (z ArtisanInputs.Reset, czyli z konstruktora ArmouryBehavior) - liczniki osad oddaje SyncData (arm_hide_frac).
        /// Recenzja: stan receptur KS (KsRecipes, statyczny na proces) od nowa - nowa kampania ma nowe obiekty typow warsztatow (MBObjectManager.Init przy kazdej
        /// grze), a stary stan zostawial regule skor "czynna" na listach poprzedniej kampanii i blokowal wlaczenie receptur KS do konca sesji.</summary>
        internal static void Reset()
        {
            _frac.Clear();
            _pW = null; _pKey = null;
            _errOnce.Clear();
            ClearDay();
            KsRecipes.CampaignReset();   // wlasny try; przy kluczach paczki NIE i bez stanu reguly skor - nic (stara droga KsRecipes co do linii)
        }

        private static void ClearDay()
        {
            Array.Clear(_dDue, 0, 3);
            _dFracDelta = _dRound = 0;
            _dArtCalls = _dArtWhole = _dArtCut = _dArtShort = _dArtShortBy = _dArtShortUnsold = 0;
            _dArtRefund = 0;
            _dShopCycles = _dShopWhole = _dShopCut = _dShopFail = 0;
            _dShopIncome = _dShopCutVal = 0;
            _dStumbles = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _dStumbles++;
            if (_errOnce.Add(where)) Log.Error(where, e);
        }

        /// <summary>Licznik osady: dopisuje inc (ulamek skory), zwraca pelne sztuki, ulamek zostaje (ponizej 0.001 obciety). Tozsamosc dokladna:
        /// inc = pelne + (zapisane - bylo) + zaokraglenie.</summary>
        private static int Take(string key, double inc)
        {
            float f0;
            _frac.TryGetValue(key, out f0);
            if (!(f0 > 0f)) f0 = 0f;
            double acc = f0 + inc;
            int whole = (int)Math.Floor(acc + Eps);
            if (whole < 0) whole = 0;
            double rest = acc - whole;
            float stored = rest < Min ? 0f : (float)Math.Min(rest, MaxFrac);
            if (stored <= 0f) _frac.Remove(key); else _frac[key] = stored;
            _dFracDelta += stored - f0;
            _dRound += rest - stored;
            return whole;
        }

        // ------------------------------------------------------------ rzemieslnicy BK (ArtisanInputs.OutPost)
        /// <summary>Wywolanie wyrobu "hides" w linii uboju pod regula: cel wywolania = pelne sztuki z licznika osady po dopisaniu cykle x skora na wywolanie.
        /// -1 = nie ta regula (inny wyrob, linia bez skory, regula nieczynna) - ArtisanInputs idzie stara droga. Zmienia licznik (raz na wywolanie). Nie rzuca.</summary>
        internal static int ArtisanTarget(Settlement st, object outs, ItemObject item, int cycles)
        {
            try
            {
                if (st == null || item == null || cycles <= 0 || item.ItemCategory == null || item.ItemCategory.StringId != "hides") return -1;
                KsRecipes.HideLine h;
                if (!KsRecipes.TryHideLine(outs, out h)) return -1;
                double inc = (double)cycles * h.PerCall;
                int whole = Take(st.StringId, inc);
                _dDue[h.Kind] += inc;
                _dArtCalls++;
                _dArtWhole += whole;
                return whole;
            }
            catch (Exception e) { Stumble("KsHides.ArtisanTarget", e); return -1; }
        }

        internal static void NoteArtisanCut(int n) { if (n > 0) _dArtCut += n; }
        internal static void NoteArtisanRefund(long gold) { if (gold > 0) _dArtRefund += gold; }

        /// <summary>BK dopisal mniej skor, niz wynosi cel z licznika: missing - brak, by - dopisane drogi E8/B (Byproduct), unsold - E8/B bez zaplaty kasy miasta.</summary>
        internal static void NoteArtisanShort(int missing, int by, int unsold)
        {
            if (missing <= 0) return;
            _dArtShort += missing; _dArtShortBy += Math.Max(0, by); _dArtShortUnsold += Math.Max(0, unsold);
        }

        // ------------------------------------------------------------ warsztaty nieukryte (rzeznik BK): postfiks GetItemsToProduce
        /// <summary>Postfiks WorkshopsCampaignBehavior.GetItemsToProduce: w linii uboju pod regula zostaje tyle pozycji skory, ile pelnych sztuk z licznika osady po
        /// dopisaniu skor tego cyklu (skory na cykl z tabeli); reszta zdjeta z listy (od konca). Utarg cyklu (income - prog zysku gry i kasa miasta) mniejszy o
        /// OCZEKIWANIE z tabeli: (pozycje skory gry - skory na cykl) x cena skory (ta sama wycena co gra: Town.GetItemPrice(sztuka, null, true)) - nie o cene
        /// sztuk zdjetych w tym cyklu (recenzja: prog zalezny od licznika osady, ktory po cyklu bez skutku stoi, mogl zatrzymac linie z cienka marza na stale).
        /// Licznik osady zapisuje dopiero CycleEnd po udanym cyklu. Ukryci rzemieslnicy - nie tu (ArtisanInputs, cel z liczby cykli). Nie rzuca.</summary>
        public static void ItemsPost(WorkshopType.Production production, Workshop workshop, List<EquipmentElement> __result, ref int income)
        {
            _pW = null;   // cykl bez CyclePostfix (wyjatek w oryginale) - stary stan przepada, licznik bez zmian
            try
            {
                if (__result == null || __result.Count == 0 || workshop == null || workshop.WorkshopType == null || workshop.WorkshopType.IsHidden) return;
                KsRecipes.HideLine h;
                if (!KsRecipes.TryHideLine(production.Outputs, out h)) return;
                var st = workshop.Settlement;
                var town = st != null ? st.Town : null;
                if (town == null) return;
                int pos = 0;
                long priceSum = 0;   // cena wszystkich pozycji skory z listy gry (te same sztuki, ktore gra wliczyla w income)
                for (int i = 0; i < __result.Count; i++)
                {
                    if (!IsHide(__result[i])) continue;
                    pos++;
                    try { priceSum += town.GetItemPrice(__result[i], null, true); }
                    catch (Exception e) { Stumble("KsHides.ItemsPost(cena)", e); }
                }
                if (pos == 0) return;
                float f0;
                _frac.TryGetValue(st.StringId, out f0);
                if (!(f0 > 0f)) f0 = 0f;
                double acc = f0 + h.PerCycle;
                int whole = (int)Math.Floor(acc + Eps);
                if (whole < 0) whole = 0;
                int keep = Math.Min(whole, pos);
                int cut = 0;
                for (int i = __result.Count - 1; i >= 0 && pos - cut > keep; i--)
                {
                    if (!IsHide(__result[i])) continue;
                    __result.RemoveAt(i);
                    cut++;
                }
                // prog zysku gry: oczekiwana strata z tabeli (pozycje gry - skory na cykl) x srednia cena skory - stala dla linii przy danej cenie, bez licznika osady
                double avg = (double)priceSum / pos;
                long expLoss = (long)Math.Round(Math.Max(0.0, pos - (double)h.PerCycle) * avg, MidpointRounding.AwayFromZero);
                if (expLoss > 0) income = (int)Math.Max(int.MinValue, (long)income - expLoss);
                _pW = workshop; _pKey = st.StringId; _pOld = f0; _pDue = h.PerCycle; _pRest = acc - (pos - cut); _pKind = h.Kind;
                _pKeep = pos - cut; _pCut = cut; _pIncome = expLoss;
                _pCutVal = (long)Math.Round(cut * avg, MidpointRounding.AwayFromZero);
            }
            catch (Exception e) { _pW = null; Stumble("KsHides.ItemsPost", e); }
        }

        private static bool IsHide(EquipmentElement e)
        {
            var it = e.Item;
            return it != null && it.ItemCategory != null && it.ItemCategory.StringId == "hides";
        }

        /// <summary>WorkshopTrade.CyclePostfix (przed cena sprawiedliwa): pozycje skory zdjete z listy wyrobow tego cyklu (0 - nie ten warsztat / nic).</summary>
        internal static int CutFor(Workshop w) { return _pW != null && ReferenceEquals(_pW, w) ? _pCut : 0; }

        /// <summary>WorkshopTrade.CyclePostfix: udany cykl (ok) - licznik osady zapisany (ulamek po skorach tego cyklu); cykl bez skutku (prog zysku gry, kapital) -
        /// zwierze nie ubite, licznik bez zmian. Zawsze czysci stan cyklu. Nie rzuca.</summary>
        internal static void CycleEnd(Workshop w, bool ok)
        {
            if (_pW == null) return;
            try
            {
                if (!ReferenceEquals(_pW, w)) return;
                if (!ok) { _dShopFail++; return; }   // cykl nieudany z dowolnej przyczyny gry (prog zysku, kapital, kasa miasta) - licznik bez zmian
                float stored = _pRest < Min ? 0f : (float)Math.Min(_pRest, MaxFrac);
                if (stored <= 0f) _frac.Remove(_pKey); else _frac[_pKey] = stored;
                _dFracDelta += stored - _pOld;
                _dRound += _pRest - stored;
                _dDue[_pKind] += _pDue;
                _dShopCycles++; _dShopWhole += _pKeep; _dShopCut += _pCut; _dShopIncome += _pIncome; _dShopCutVal += _pCutVal;
            }
            catch (Exception e) { Stumble("KsHides.CycleEnd", e); }
            finally { _pW = null; _pKey = null; }
        }

        /// <summary>SubModuleMain zaraz po GoodsPaths.ApplyEarly - PRZED WorkshopTrade.ApplyAll (latki cyklu warsztatu kompiluja wolana metode, latka musi byc
        /// wczesniej). Metoda zadeklarowana w WorkshopsCampaignBehavior (DeclaredMethod). Wlasny try, linia startu.</summary>
        internal static void ApplyEarly(Harmony h)
        {
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(WorkshopsCampaignBehavior), "GetItemsToProduce");
                if (m == null) _wire = "BRAK GetItemsToProduce - rzeznicy bez reguly (rzemieslnicy BK z regula)";
                else { h.Patch(m, postfix: new HarmonyMethod(typeof(KsHides), nameof(ItemsPost))); _wire = "wpieta"; }
            }
            catch (Exception e) { _wire = "blad " + e.GetType().Name + " - rzeznicy bez reguly"; Log.Error("KsHides.ApplyEarly", e); }
            Log.Info("Skory z uboju (KS-S): latka listy wyrobow cyklu warsztatow (GetItemsToProduce - rzeznicy) " + _wire
                     + "; regula czynna tylko przy KS zastosowanym z Ks Real Recipes i Ks Hides By Table (klucze czytane na zywo, linia doby 'Skory z uboju (KS-S)').");
        }

        // ------------------------------------------------------------ zapis (WorkshopTradeBehavior.SyncData, klucz arm_hide_frac)
        /// <summary>"osada=ulamek;..." - tylko ulamki >= 0.001, najwyzej MaxSaveEntries wpisow (osad jest ok. 200 - straznik rozmiaru).</summary>
        internal static string Export()
        {
            var sb = new StringBuilder();
            int n = 0;
            foreach (var kv in _frac)
            {
                if (!(kv.Value >= Min) || string.IsNullOrEmpty(kv.Key) || kv.Key.IndexOf(';') >= 0 || kv.Key.IndexOf('=') >= 0) continue;
                if (++n > MaxSaveEntries) break;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(kv.Key).Append('=').Append(kv.Value.ToString("R", Inv));
            }
            return sb.ToString();
        }

        /// <summary>Wczytanie: stary zapis bez klucza (null) = puste liczniki; wpisy bez nazwy, liczby niepoprawne i ponizej 0.001 pominiete; najwyzej MaxFrac.</summary>
        internal static void Import(string s)
        {
            _frac.Clear();
            if (string.IsNullOrEmpty(s)) return;
            int n = 0;
            foreach (var p in s.Split(';'))
            {
                if (++n > MaxSaveEntries) break;
                var a = p.Split('=');
                if (a.Length != 2 || a[0].Length == 0) continue;
                float v;
                if (!float.TryParse(a[1], NumberStyles.Float, Inv, out v)) continue;
                if (float.IsNaN(v) || float.IsInfinity(v) || v < Min) continue;
                _frac[a[0]] = Math.Min(v, MaxFrac);
            }
        }

        // ------------------------------------------------------------ linia doby
        /// <summary>Z ArtisanInputs.Daily (DailyTickEvent): jedna linia, gdy regula czynna albo cos dzis policzyla. Nie rzuca.</summary>
        internal static void Daily()
        {
            try
            {
                bool act = KsRecipes.HidesActive;
                if (!act && _dArtCalls == 0 && _dShopCycles == 0 && _dShopFail == 0) return;
                int day = (int)CampaignTime.Now.ToDays - 1;
                double due = _dDue[0] + _dDue[1] + _dDue[2];
                int whole = _dArtWhole + _dShopWhole;
                double diff = due - whole - _dFracDelta - _dRound;
                double mem = 0;
                foreach (var v in _frac.Values) mem += v;
                var sb = new StringBuilder();
                sb.Append("Skory z uboju (KS-S): dzien ").Append(day)
                  .Append(" | Ks Hides By Table ").Append(act ? "[WL]" : "[wyl]")
                  .Append(", ksiega z aktualnych receptur (Ks Ledger Current Recipes) ").Append(KsRecipes.LedgerKeyOn ? "[WL]" : "[wyl]")
                  .Append(" | skory na sztuke zywca: krowa ").Append(KsRecipes.PerHead(0).ToString("0.00", Inv))
                  .Append(", swinia ").Append(KsRecipes.PerHead(1).ToString("0.00", Inv))
                  .Append(", owca ").Append(KsRecipes.PerHead(2).ToString("0.00", Inv))
                  .Append(" | nalezne wedlug tabeli swiata ").Append(due.ToString("0.00", Inv)).Append(" szt. [");
                string[] nm = { "krowy", "swinie", "owce" };
                for (int k = 0; k < 3; k++)
                {
                    float ph = KsRecipes.PerHead(k);
                    double heads = ph > 0f ? _dDue[k] / ph : 0;
                    if (k > 0) sb.Append(", ");
                    sb.Append(nm[k]).Append(' ').Append(Math.Round(heads).ToString("0", Inv)).Append(" -> ").Append(_dDue[k].ToString("0.00", Inv));
                }
                sb.Append("] | pelne sztuki ").Append(whole)
                  .Append(" (rzemieslnicy BK ").Append(_dArtWhole).Append(" z ").Append(_dArtCalls).Append(" wywolan, rzeznicy ").Append(_dShopWhole).Append(" z ").Append(_dShopCycles).Append(" cykli)")
                  .Append(" | kontrola towaru: nalezne ").Append(due.ToString("0.00", Inv)).Append(" = pelne ").Append(whole)
                  .Append(" + przyrost licznikow osad ").Append(_dFracDelta.ToString("0.00", Inv)).Append(" + zaokraglenia ").Append(_dRound.ToString("0.000", Inv))
                  .Append(" (roznica ").Append(diff.ToString("0.000", Inv)).Append(")")
                  .Append(" | liczniki osad: ").Append(_frac.Count).Append(" osad razem ").Append(mem.ToString("0.00", Inv)).Append(" szt.")
                  .Append(" | rzemieslnicy BK: ponad tabele zdjete z polki ").Append(_dArtCut).Append(" szt., zwrot zaplaty BK do kas miast +").Append(_dArtRefund)
                  .Append(" zl (kapital rzemieslnikow -").Append(_dArtRefund).Append(", kasy miast +").Append(_dArtRefund).Append(", roznica 0)")
                  .Append("; brak sztuk BK wobec celu ").Append(_dArtShort).Append(" szt. (dopisane E8/B ").Append(_dArtShortBy)
                  .Append(", niesprzedane ").Append(_dArtShortUnsold).Append(", bez E8/B ").Append(Math.Max(0, _dArtShort - _dArtShortBy - _dArtShortUnsold)).Append(")")
                  .Append(" | rzeznicy: pozycji skory zdjetych z listy wyrobow ").Append(_dShopCut).Append(" warte ").Append(_dShopCutVal)
                  .Append(" zl (sztuki nie powstaly - zloto 0), utarg cyklu w progu zysku mniej ").Append(_dShopIncome)
                  .Append(" zl (oczekiwanie z tabeli: pozycje gry - skory na cykl), nieudanych cykli linii uboju ze skora ").Append(_dShopFail)
                  .Append(" (prog zysku gry, kapital albo kasa miasta - licznik bez zmian)")
                  .Append(" | latka rzeznikow ").Append(_wire)
                  .Append(" | potkniecia ").Append(_dStumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("KsHides.Daily", e); }
            finally { ClearDay(); }
        }
    }
}
