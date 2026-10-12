using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// E6/M2 (208, PLAN 6.10 (d); projekt etapu 6 v2 rozdz. 3 "E6/M2", krytyka KA K1-11; D 09.10 I/J "Dorne - piaskowe rumaki") - WIELBLADY TYLKO W QARTHU.
    /// Gra: 4 stadniny desert_horse_ranch Dorne (i te same w Lys, Myr, Tyrosh, Volantis, Zatoce) daja rumaki RAZEM z wielbladami (camel 0.3, war_camel 0.08,
    /// pack_camel 0.3 - G DefaultVillageTypes.cs:229-237), a Qarth (jedyny z jezdzcami na wielbladach) ma tylko steppe_horse_ranch bez wielbladow.
    /// Regula (ta sama suma sztuk, nic z niczego): poza Qarthem stadnina nie rodzi wielbladow - ich waga przechodzi na konia TEJ SAMEJ kategorii w tej
    /// stadninie (camel -> kon zwykly aserai_horse, war_camel -> bojowy t2, pack_camel -> juczny sumpter_horse; brak takiej kategorii - kon o najwiekszej
    /// wadze); stadniny Qarthu rodza wielblady w wadze stadniny pustynnej kosztem konia tej samej kategorii (khuzait_horse, t2_khuzait_horse, sumpter_horse).
    /// Okna: lista produkcji wsi BK (postfiks GetProductions - MineralOnce.Postfix, jedna latka) zdejmuje/dopisuje wpisy i przenosi wagi; wynik modelu
    /// (MaterialLaw.ProdPostfix - aktywny model wagi z listy nie czyta, liczy z VillageType.Productions): kon-gospodarz x (w + zdjete)/w poza Qarthem albo
    /// x (w - oddane)/w w Qarthu, a wielblad Qarthu (model daje 0 - nie ma go w typie wsi) dostaje baze = surowy wynik modelu gospodarza x oddane/w (wywolanie
    /// wewnetrzne - nasz postfiks pomija je, _depth > 1), dalej te same mnozniki co kazdy towar wsi. Mnoznik tylko dla wsi, ktorej liste naprawde przerobiono
    /// (jak MineralOnce.Times). TYLKO NOWA KAMPANIA (NK): flaga "arm_e6_camels" w zapisie; stary zapis - stadniny jak w grze. MountLaw, LegendaryLaw, Stables -
    /// bez zmian w tej paczce (MountLaw :171 "wielblad tylko qartheen" i koniec kasowania egzotycznych z polek - nastepna sesja etapu 6).
    /// </summary>
    internal static class CamelLaw
    {
        internal static bool NewCampaign;
        private static readonly object Gate = new object();
        private static readonly TextObject _txt = new TextObject("{=!}Armoury: camels of Qarth");

        private sealed class Plan
        {
            public readonly Dictionary<ItemObject, float> Factor = new Dictionary<ItemObject, float>();                     // kon-gospodarz -> mnoznik wyniku
            public readonly Dictionary<ItemObject, KeyValuePair<ItemObject, float>> FromHost = new Dictionary<ItemObject, KeyValuePair<ItemObject, float>>();   // wielblad Qarthu -> (gospodarz, waga)
            public readonly Dictionary<ItemObject, float> HostW = new Dictionary<ItemObject, float>();
            public readonly Dictionary<ItemObject, ItemObject> MoveTo = new Dictionary<ItemObject, ItemObject>();             // wielblad poza Qarthem -> gospodarz
            public bool Qarth;
        }

        private static readonly Dictionary<VillageType, Plan> _out = new Dictionary<VillageType, Plan>();
        private static readonly Dictionary<VillageType, Plan> _in = new Dictionary<VillageType, Plan>();
        private static readonly Dictionary<Village, bool> _qarthOf = new Dictionary<Village, bool>();
        private static readonly HashSet<Village> _filtered = new HashSet<Village>();
        private static List<KeyValuePair<ItemObject, float>> _camelW;
        private static bool _camelWDone;
        private static double _qarthBase;
        private static int _lists, _stumbles;

        internal static void Reset()
        {
            lock (Gate) { _out.Clear(); _in.Clear(); _qarthOf.Clear(); _filtered.Clear(); _camelW = null; _camelWDone = false; _qarthBase = 0; _lists = 0; }
            _stumbles = 0; NewCampaign = false;
        }

        internal static string Export() { return NewCampaign ? "nk" : ""; }
        internal static void Import(string s) { NewCampaign = s == "nk"; }
        internal static void OnNewGame() { NewCampaign = true; }

        /// <summary>Klucz, nowa kampania i wpieta lista produkcji BK (bez niej wielblad zostalby w produkcji, a kon dostalby jego wage - nic z niczego). Recenzja T4-E6: takze MaterialLaw.ProdPostfix w modelach produkcji (MaterialLaw.ProdModels &gt; 0) - bez niego lista traci wielblady, a mnoznika gospodarza nie ma czym oddac (sztuki w nicosc), jak czesc mineralow MineralOnce._patched.</summary>
        internal static bool On { get { var s = Settings.Current; return s != null && s.SandSteedsInDorne && NewCampaign && MineralOnce.Wired && MaterialLaw.ProdModels > 0; } }

        private static void Stumble(string where, Exception e)
        {
            int k;
            lock (Gate) k = ++_stumbles;
            if (k <= 3) Log.Error("CamelLaw." + where, e);
        }

        private static bool IsHorse(ItemObject it) { return it != null && it.ItemType == ItemObject.ItemTypeEnum.Horse && MountLaw.FamilyOf(it) == MountLaw.Fam.Horse; }
        private static bool IsCamel(ItemObject it) { return it != null && MountLaw.FamilyOf(it) == MountLaw.Fam.Camel; }

        private static bool Qarth(Village v)
        {
            bool q;
            lock (Gate) { if (_qarthOf.TryGetValue(v, out q)) return q; }
            q = PeopleBook.CultureAtCalibration(v.Settlement) == "qartheen";
            lock (Gate) _qarthOf[v] = q;
            return q;
        }

        /// <summary>Gospodarz: kon tej samej kategorii w typie wsi o najwiekszej wadze; brak - kon o najwiekszej wadze.</summary>
        private static ItemObject HostIn(VillageType t, ItemObject camel, out float w)
        {
            ItemObject best = null, any = null; float bw = 0f, aw = 0f;
            foreach (var p in t.Productions)
            {
                if (!IsHorse(p.Item1) || p.Item2 <= 0f) continue;
                if (p.Item1.ItemCategory == camel.ItemCategory && p.Item2 > bw) { best = p.Item1; bw = p.Item2; }
                if (p.Item2 > aw) { any = p.Item1; aw = p.Item2; }
            }
            if (best != null) { w = bw; return best; }
            w = aw; return any;
        }

        private static List<KeyValuePair<ItemObject, float>> CamelWeights()
        {
            if (_camelWDone) return _camelW;
            List<KeyValuePair<ItemObject, float>> res = null;
            foreach (var v in Village.All)
            {
                if (v == null || v.VillageType == null) continue;
                var l = new List<KeyValuePair<ItemObject, float>>();
                foreach (var p in v.VillageType.Productions) if (IsCamel(p.Item1) && p.Item2 > 0f) l.Add(new KeyValuePair<ItemObject, float>(p.Item1, p.Item2));
                if (l.Count > 0) { res = l; break; }
            }
            _camelW = res; _camelWDone = true;
            return res;
        }

        private static Plan PlanFor(Village v)
        {
            if (v == null || v.VillageType == null || v.Settlement == null) return null;
            var t = v.VillageType;
            bool q = Qarth(v);
            Plan p;
            lock (Gate)
            {
                var cache = q ? _in : _out;
                if (cache.TryGetValue(t, out p)) return p;
                p = null;
                bool hasCamel = false, hasHorse = false;
                foreach (var e in t.Productions) { if (IsCamel(e.Item1) && e.Item2 > 0f) hasCamel = true; if (IsHorse(e.Item1) && e.Item2 > 0f) hasHorse = true; }
                if (!q && hasCamel)
                {
                    p = new Plan();
                    foreach (var e in t.Productions)
                    {
                        if (!IsCamel(e.Item1) || e.Item2 <= 0f) continue;
                        float hw; var host = HostIn(t, e.Item1, out hw);
                        if (host == null || hw <= 0f) continue;                  // stadnina bez konia - wielblad zostaje (nie ma dokad przeniesc)
                        p.MoveTo[e.Item1] = host; p.HostW[host] = hw;
                        float f; p.Factor.TryGetValue(host, out f); p.Factor[host] = (f > 0f ? f : 1f) + e.Item2 / hw;
                    }
                    if (p.MoveTo.Count == 0) p = null;
                }
                else if (q && !hasCamel && hasHorse)
                {
                    var cw = CamelWeights();
                    if (cw != null)
                    {
                        p = new Plan { Qarth = true };
                        var left = new Dictionary<ItemObject, float>();
                        foreach (var c in cw)
                        {
                            float hw; var host = HostIn(t, c.Key, out hw);
                            if (host == null || hw <= 0f) continue;
                            float rem; if (!left.TryGetValue(host, out rem)) rem = hw;
                            float take = Math.Min(c.Value, 0.5f * rem);           // gospodarz nie schodzi ponizej polowy swojej wagi
                            if (take <= 0f) continue;
                            left[host] = rem - take;
                            p.FromHost[c.Key] = new KeyValuePair<ItemObject, float>(host, take); p.HostW[host] = hw;
                        }
                        foreach (var kv in left) p.Factor[kv.Key] = kv.Value / p.HostW[kv.Key];
                        if (p.FromHost.Count == 0) p = null;
                    }
                }
                cache[t] = p;
            }
            return p;
        }

        /// <summary>Z MineralOnce.Postfix (lista produkcji wsi BK): poza Qarthem bez wielbladow (waga na gospodarza), w Qarthu wielblady kosztem gospodarza.
        /// Nowa lista tylko, gdy cos zmienia; inaczej ta sama.</summary>
        internal static List<(ItemObject, float)> ListFilter(Village v, List<(ItemObject, float)> list)
        {
            if (!On || list == null) return list;
            var p = PlanFor(v);
            if (p == null) { lock (Gate) _filtered.Remove(v); return list; }
            var res = new List<(ItemObject, float)>(list.Count + 3);
            if (!p.Qarth)
            {
                var add = new Dictionary<ItemObject, float>();
                var hosts = new HashSet<ItemObject>();
                foreach (var e in list) if (e.Item1 != null && p.HostW.ContainsKey(e.Item1)) hosts.Add(e.Item1);
                if (hosts.Count < p.HostW.Count) { lock (Gate) _filtered.Remove(v); return list; }   // lista bez ktoregos gospodarza - nie ruszamy (mnoznik planu zaklada wszystkich)
                foreach (var e in list)
                {
                    ItemObject host;
                    if (e.Item1 != null && p.MoveTo.TryGetValue(e.Item1, out host)) { float a; add.TryGetValue(host, out a); add[host] = a + e.Item2; continue; }
                    res.Add(e);
                }
                for (int i = 0; i < res.Count; i++) { float a; if (res[i].Item1 != null && add.TryGetValue(res[i].Item1, out a)) { res[i] = (res[i].Item1, res[i].Item2 + a); add.Remove(res[i].Item1); } }
            }
            else
            {
                var take = new Dictionary<ItemObject, float>();
                foreach (var kv in p.FromHost) { float a; take.TryGetValue(kv.Value.Key, out a); take[kv.Value.Key] = a + kv.Value.Value; }
                foreach (var e in list)
                {
                    float a;
                    if (e.Item1 != null && take.TryGetValue(e.Item1, out a)) res.Add((e.Item1, Math.Max(0f, e.Item2 - a)));
                    else if (!IsCamel(e.Item1)) res.Add(e);
                }
                foreach (var kv in p.FromHost) res.Add((kv.Key, kv.Value.Value));
            }
            lock (Gate) { if (_filtered.Add(v)) _lists++; }
            return res;
        }

        /// <summary>MineralOnce.Postfix po potknieciu: lista tej wsi zostaje z BK (z wielbladami) - mnoznik gospodarza musi zniknac.</summary>
        internal static void Forget(Village v) { if (v == null) return; lock (Gate) _filtered.Remove(v); }

        private static Plan Live(Village v)
        {
            if (!On || v == null) return null;
            lock (Gate) { if (!_filtered.Contains(v)) return null; }
            return PlanFor(v);
        }

        /// <summary>MaterialLaw.ProdPostfix: mnoznik wyniku konia-gospodarza (1 dla reszty i dla wsi bez przerobionej listy).</summary>
        internal static float HostFactor(Village v, ItemObject item)
        {
            try
            {
                if (item == null || item.ItemType != ItemObject.ItemTypeEnum.Horse) return 1f;
                var p = Live(v);
                float f;
                if (p == null || !p.Factor.TryGetValue(item, out f)) return 1f;
                return f;
            }
            catch (Exception e) { Stumble("HostFactor", e); return 1f; }
        }

        /// <summary>MaterialLaw.ProdPostfix, przed mnoznikami: wielblad stadniny Qarthu - baza = surowy wynik modelu gospodarza x waga oddana / waga gospodarza.</summary>
        internal static void QarthBase(Village v, ItemObject item, ref ExplainedNumber r)
        {
            try
            {
                if (!IsCamel(item)) return;
                var p = Live(v);
                KeyValuePair<ItemObject, float> fh;
                if (p == null || !p.Qarth || !p.FromHost.TryGetValue(item, out fh)) return;
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.VillageProductionCalculatorModel : null;
                float hw;
                if (model == null || !p.HostW.TryGetValue(fh.Key, out hw) || hw <= 0f) return;
                float raw = model.CalculateDailyProductionAmount(v, fh.Key).ResultNumber;   // wywolanie wewnetrzne: nasz postfiks je pomija (_depth > 1)
                float x = raw * fh.Value / hw;
                if (!(x > 0f)) return;
                float sum = 1f + r.SumOfFactors;
                r.Add(sum > 0.01f ? x / sum : x, _txt);
                lock (Gate) _qarthBase += x;
            }
            catch (Exception e) { Stumble("QarthBase", e); }
        }

        /// <summary>Z ArmouryBehavior.OnDailyTick: linia "Wierzchowce kanonu (208)" (stan regul; produkcje w sztukach liczy linia "Konie (E6/0)").</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || !s.SandSteedsInDorne) return;
            int lists, out_, in_; double qb;
            lock (Gate)
            {
                lists = _filtered.Count; qb = _qarthBase; _qarthBase = 0;
                out_ = 0; foreach (var p in _out.Values) if (p != null) out_++;
                in_ = 0; foreach (var p in _in.Values) if (p != null) in_++;
            }
            int day = (int)CampaignTime.Now.ToDays;
            if (!NewCampaign) { if (day % 28 == 0) Log.Info("Wierzchowce kanonu (208): dzien " + day + " | stary zapis (bez flagi nowej kampanii) - stadniny jak w grze."); return; }
            Log.Info("Wierzchowce kanonu (208): dzien " + day + " | " + (MineralOnce.Wired && MaterialLaw.ProdModels > 0 ? "" : MineralOnce.Wired ? "BRAK MaterialLaw w modelach produkcji wsi - NIECZYNNE | " : "BRAK listy produkcji BK - NIECZYNNE | ") + "stadniny z przerobiona lista " + lists
                     + " (typy stadnin pustynnych poza Qarthem " + out_ + ", stadnin Qarthu " + in_ + ") | wielblady Qarthu z modelu " + qb.ToString("0.0", CultureInfo.InvariantCulture)
                     + "/d | sztuki wedlug krainy - linia \"Konie (E6/0)\" (wielblady poza Qarthem ma byc 0)" + (_stumbles > 0 ? " | potkniecia " + _stumbles : "") + ".");
        }
    }
}
