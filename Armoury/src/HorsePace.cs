using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 311d KONIE A TEMPO NA MAPIE (Jeff 31.10: "armia ktora nie jest konnica porusza sie szybciej i powinno to byc uzaleznione od jakosci koni, tanie konie
    /// jeszcze nieco wolniej ... na lepszych koniach jezdza nieco szybciej, dajmy roznice realne"; rozpoznanie rozp311k/konie-raport.md rozdz. 3 + krytyka
    /// konie-krytyk.md rozdz. C). Trzy kroki, kazdy z wlasnym kluczem (niezalezne od MarchPaceEnabled / MarchPaceAiToo), jedna latka - MarchPace.SpeedPostfix
    /// (Priority.Last na kazdym zadeklarowanym CalculateFinalSpeed), wolane PRZED sufitem kolumny, wlasny bezpiecznik SpeedDepth.OutermostFinal:
    ///  (1) HorsesArmouryCarryFootmen: wolne konie zbrojowni DTE (HorseState.FreeOf - lordowie AI, takze partie AI w armii gracza) niosa piechure takze
    ///      we wspolczynniku gry "Footmen on horses" (gra liczy tylko luzaki z taboru): wpis "Footmen on horses (armoury)" +0.15 x dodatkowo niesionych / N,
    ///      tylko ponad to, co gra juz policzyla (min(piesi, luzaki + wolne) - min(piesi, luzaki)), w mokro -30% jak gra.
    ///  (2) HorseQualitySpeed: d = clamp(Slope x (q / Ref - 1), -MaxPenalty, +MaxBonus), q = HorseComponent.Speed x modyfikator (kulawy 0.7; stosunek 0.5-1.1).
    ///      Bohater (takze gracz) - wlasny kon z Equipment z modyfikatorem; szeregowy jezdziec - kon, ktory naprawde ma: AI ze stanu koni partii (HorseState,
    ///      przydzial jak Stables.Balance, w stopniu najlepsze najpierw), druzyna gracza ze zbrojowni DTE (ArmyArmory) przy TroopsFightWithOwnKitOnly,
    ///      w reszcie przypadkow kon wzorca; piechurzy na luzakach - najlepsze luzaki (tabor + wolne konie zbrojowni wedlug kluczy); juczne nie wchodza.
    ///      Wpisy "Horse quality: riders" / "Horse quality: spare horses" = suma d / N, w mokro -30%. Sufit kolumny MarchPace: sytuacja 1 (sama jazda)
    ///      x (1 + srednie d jezdzcow), sytuacja 2 (piechota na luzakach) x (1 + srednie d luzakow pod sufitem) - w Cap() i Explain() tak samo (CapMul).
    ///  (3) RidersWithoutHorseWalk: szeregowy jezdziec bez konia w zbrojowni (HorseState.Unhorsed, przydzial gracza z ArmyArmory) - tylko przy
    ///      TroopsFightWithOwnKitOnly (bez tej reguly DTE daje mu konia wzorca) - nie jest jazda: wpis "Riders without horses" -0.3 x n / N (zdejmuje
    ///      "Cavalry" gry, w mokro jak gra -30%), a w kolumnie MarchPace idzie pieszo (WalkOf - do piechoty; luzak go niesie jak piechure, wtedy
    ///      dostaje "Footmen on horses (riders without horses)" / "(armoury)" +0.15).
    /// PAMIEC: rekord partii (z doczepionymi partiami armii) w ConcurrentDictionary podmieniany w calosci; klucz = wersje rosterow ludzi i taboru partii
    /// i doczepionych, tozsamosc stanu koni partii (HorseState.State - nowy obiekt po dobie i odswiezeniu), doba, a dla gracza godzina (zbrojownia DTE i jego kon nie zmieniaja wersji rosterow);
    /// podpis ustawien osobno. Mokro liczone przy kazdym wywolaniu (jak gra). Potkniecia liczone (wyjatek = wynik bez zmian dla tego wywolania), bez wylacznika.
    /// </summary>
    internal static class HorsePace
    {
        internal struct Lot { public int Lv; public float Q; public int N; }

        internal sealed class Rec
        {
            public int Sig, Key;
            public int Men, FootV, MountsV, Free, Att;                 // jak gra / CountColumn (z doczepionymi)
            public int Riders, RidersArm, RidersTpl, RidersHero;        // konni z koniem; zrodlo jakosci: zbrojownia, wzorzec, bohater
            public double RidersD, RidersQ;                             // suma d i szybkosci koni jezdzcow
            public int Extra; public bool ExtraFree;                    // krok 1: piechurzy niesieni ponad gre; czy przez wolne konie zbrojowni
            public int SpareF; public double SpareFD, SpareFQ;          // krok 2: luzaki pod piechurami we wspolczynniku (najlepsze)
            public int SpareC; public double SpareCD;                   // krok 2: luzaki pod piechurami w suficie kolumny (pula K0)
            public int Walk, Unhorsed, RegRiders;                       // krok 3: jezdzcy bez konia idacy pieszo / bez konia w ogole / konni szeregowi
            public float FDry, FHq;                                     // suma wspolczynnikow 311d na sucho (bez mokra); w tym jakosc koni (kontrola: w [-kara, +premia])
        }

        private struct P { public float Ref, Slope, Bon, Pen; }

        private static ConcurrentDictionary<MobileParty, Rec> _rec = new ConcurrentDictionary<MobileParty, Rec>();
        private struct Tpl { public float Q; public bool Plain; }
        private static readonly ConcurrentDictionary<CharacterObject, Tpl> _tpl = new ConcurrentDictionary<CharacterObject, Tpl>();
        [ThreadStatic] private static bool _dry;                        // probka "bez 311d" w linii doby
        [ThreadStatic] private static List<Lot> _tmpA, _tmpB, _tmpC;
        // jedno wywolanie MarchPace.SpeedPostfix: rekord i mokro raz na wywolanie (Apply, WalkOf i CapMul z ColumnPostfix czytaja te sama pamiec watku)
        [ThreadStatic] private static bool _mOn, _mHave;
        [ThreadStatic] private static MobileParty _mMp;
        [ThreadStatic] private static Rec _mRec;
        [ThreadStatic] private static float _mWet;

        // wpisy rozpiski tworzone dopiero w kampanii (klasa jest dotykana przy starcie gry z MarchPace.ApplyAll - bez TextObject w konstruktorze statycznym)
        private static TextObject _txArm, _txArmWalk, _txRiders, _txSpare, _txWalk;
        private static TextObject Tx(ref TextObject t, string s) { return t ?? (t = new TextObject(s)); }
        private static TextObject TxArm { get { return Tx(ref _txArm, "{=!}Footmen on horses (armoury)"); } }
        private static TextObject TxArmWalk { get { return Tx(ref _txArmWalk, "{=!}Footmen on horses (riders without horses)"); } }
        private static TextObject TxRiders { get { return Tx(ref _txRiders, "{=!}Horse quality: riders"); } }
        private static TextObject TxSpare { get { return Tx(ref _txSpare, "{=!}Horse quality: spare horses"); } }
        private static TextObject TxWalk { get { return Tx(ref _txWalk, "{=!}Riders without horses"); } }

        // liczniki doby (tylko log; bez blokad poza potknieciami jak HorseState)
        private static long _calls, _sampled, _ticks, _builds, _buildTicks, _dBuilds, _dBuildTicks;
        [ThreadStatic] private static bool _inDaily;                    // przeliczenia w linii doby liczone osobno (nie goraca sciezka)
        private static long _capN, _capUp, _capDown; private static double _capBefore, _capAfter;
        private static int _stumbles, _stumblesDay;
        private static readonly object Gate = new object();
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static void Reset()
        {
            _rec = new ConcurrentDictionary<MobileParty, Rec>();
            _arm = new ConcurrentDictionary<MobileParty, ArmQ>();
            _tpl.Clear();
            ResetDay();
            _stumbles = 0;
            lock (Gate) _errSites.Clear();
        }

        private static void ResetDay()
        {
            _calls = _sampled = _ticks = 0;
            Interlocked.Exchange(ref _builds, 0); Interlocked.Exchange(ref _buildTicks, 0); _dBuilds = _dBuildTicks = 0;
            _capN = _capUp = _capDown = 0; _capBefore = _capAfter = 0;
            _stumblesDay = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            Interlocked.Increment(ref _stumbles); Interlocked.Increment(ref _stumblesDay);
            bool first; lock (Gate) first = _errSites.Add(where);
            if (first) Log.Error("HorsePace." + where, e);
        }

        internal static bool WantsState(Settings s) { return s != null && (s.HorsesArmouryCarryFootmen || s.HorseQualitySpeed || s.RidersWithoutHorseWalk); }

        internal static bool AnyOn(Settings s) { return WantsState(s); }

        // ------------------------------------------------------------ jakosc konia

        /// <summary>Szybkosc konia z modyfikatorem (ItemModifier.ModifyMountSpeed; stosunek przyciety do 0.5-1.1); 0 = nie kon.</summary>
        internal static float Q(ItemObject it, ItemModifier md)
        {
            if (it == null || !it.HasHorseComponent) return 0f;
            var hc = it.HorseComponent;
            int sp = hc != null ? hc.Speed : 0;
            if (sp <= 0) return 0f;
            if (md == null) return sp;
            float r = md.ModifyMountSpeed(sp) / (float)sp;
            return sp * MBMath.ClampFloat(r, 0.5f, 1.1f);
        }

        private static P Params(Settings s)
        {
            return new P
            {
                Ref = Math.Max(30, Math.Min(90, s.HorseQualityRefSpeed)),
                Slope = Math.Max(0, Math.Min(200, s.HorseQualitySlopePercent)) / 100f,
                Bon = Math.Max(0, Math.Min(50, s.HorseQualityMaxBonusPercent)) / 100f,
                Pen = Math.Max(0, Math.Min(50, s.HorseQualityMaxPenaltyPercent)) / 100f
            };
        }

        private static float D(float q, P p)
        {
            if (q <= 0f) return 0f;
            return MBMath.ClampFloat(p.Slope * (q / p.Ref - 1f), -p.Pen, p.Bon);
        }

        /// <summary>Kon wzorca szeregowego (CharacterObject.Equipment - pierwszy zestaw bitewny) i czy to zwykly kon pod siodlo (Stables.PlainRider), pamiec na sesje.</summary>
        private static Tpl TemplateOf(CharacterObject ch)
        {
            Tpl t;
            if (_tpl.TryGetValue(ch, out t)) return t;
            t = new Tpl();
            try { var eq = ch.Equipment; if (eq != null) { var ee = eq[EquipmentIndex.Horse]; t.Q = Q(ee.Item, ee.ItemModifier); } }
            catch { t.Q = 0f; }
            t.Plain = Stables.PlainRider(ch);
            _tpl[ch] = t;
            return t;
        }

        // ------------------------------------------------------------ przydzial koni zbrojowni (jak Stables.Balance)

        private static int Take(List<Lot> l, int k, List<Lot> ride)
        {
            int got = 0;
            for (int i = 0; i < l.Count && k > 0; i++)
            {
                var x = l[i];
                if (x.N <= 0) continue;
                int t = Math.Min(k, x.N);
                x.N -= t; l[i] = x; k -= t; got += t;
                if (ride != null) ride.Add(new Lot { Lv = x.Lv, Q = x.Q, N = t });
            }
            return got;
        }

        private static int ByQDesc(Lot a, Lot b) { return b.Q.CompareTo(a.Q); }

        /// <summary>Przydzial jak Stables.Balance (rycerz: bojowy, szlachetny; reszta: zwykly, bojowy, szlachetny; rycerz bez bojowego - zwykly), w stopniu
        /// najlepsze najpierw. Zwraca jezdzcow bez konia; ride / spare - konie pod jezdzcami / wolne.</summary>
        internal static int Assign(List<Lot> lots, int low, int high, List<Lot> ride, List<Lot> spare)
        {
            var lv = new[] { new List<Lot>(), new List<Lot>(), new List<Lot>() };
            foreach (var x in lots) if (x.N > 0 && x.Lv >= 0 && x.Lv < 3) lv[x.Lv].Add(x);
            for (int i = 0; i < 3; i++) lv[i].Sort(ByQDesc);
            int rh = Math.Max(0, high), rl = Math.Max(0, low);
            rh -= Take(lv[1], rh, ride);
            rh -= Take(lv[2], rh, ride);
            for (int i = 0; i < 3; i++) rl -= Take(lv[i], rl, ride);
            rh -= Take(lv[0], rh, ride);
            if (spare != null) for (int i = 0; i < 3; i++) foreach (var x in lv[i]) if (x.N > 0) spare.Add(x);
            return rh + rl;
        }

        /// <summary>Konie zbrojowni AI z szybkoscia - pod jezdzcami i wolne; liczone raz na stan koni partii (HorseState.State podmieniany w calosci w dobie
        /// i przy odswiezeniu - nowa referencja = liczymy od nowa). Koszt w liczniku przeliczen HorsePace, nie w "stan doby" HorseState.</summary>
        internal sealed class ArmQ
        {
            public HorseState.State Src;
            public Lot[] Ride, Spare;
            public int Ridden, SpareAll, Unhorsed;   // Unhorsed - jezdzcy wzorca na zwyklym koniu bez konia w zbrojowni (jak Balance plainOnly)
        }

        private static ConcurrentDictionary<MobileParty, ArmQ> _arm = new ConcurrentDictionary<MobileParty, ArmQ>();

        private static ArmQ ArmOf(MobileParty x, HorseState.State st, int low, int high)
        {
            ArmQ a;
            if (_arm.TryGetValue(x, out a) && a != null && ReferenceEquals(a.Src, st)) return a;
            var all = AiGear.Armories();
            Dictionary<ItemObject, int> arm;
            a = new ArmQ { Src = st };
            if (all != null && all.TryGetValue(x.Id, out arm) && arm != null) Fill(a, arm, low, high);
            _arm[x] = a;   // podmiana w calosci
            return a;
        }

        /// <summary>Przydzial koni zbrojowni DTE (bez modyfikatorow - slownik DTE) jezdzcom wzorca na zwyklym koniu (plainOnly, jak Remount: wielblady i slonie
        /// nie biora koni i nie sa "bez konia" - pelny Balance liczy ich do Unhorsed, gdy brak koni pod siodlo).</summary>
        private static void Fill(ArmQ a, Dictionary<ItemObject, int> arm, int low, int high)
        {
            var lots = new List<Lot>();
            foreach (var kv in arm)
            {
                if (kv.Key == null || kv.Value <= 0 || !Stables.IsPlainMount(kv.Key)) continue;
                int lv = Stables.Level(kv.Key.ItemCategory);
                if (lv < 0) continue;
                lots.Add(new Lot { Lv = lv, Q = Q(kv.Key, null), N = kv.Value });
            }
            var ride = new List<Lot>(); var spare = new List<Lot>();
            int un = Assign(lots, low, high, ride, spare);
            spare.Sort(ByQDesc);
            int r = 0, f = 0;
            foreach (var x in ride) r += x.N;
            foreach (var x in spare) f += x.N;
            a.Ride = ride.ToArray(); a.Spare = spare.ToArray(); a.Ridden = r; a.SpareAll = f; a.Unhorsed = un;
        }

        // ------------------------------------------------------------ rekord partii

        private static int Sig(Settings s, P p)
        {
            int k = (s.HorsesArmouryCarryFootmen ? 1 : 0) | (s.HorseQualitySpeed ? 2 : 0) | (s.RidersWithoutHorseWalk ? 4 : 0)
                  | (s.SpareHorsesCarryFootmen ? 8 : 0) | (GarrisonKit.OwnKitOn ? 16 : 0);
            unchecked
            {
                int h = k;
                h = h * 397 + (int)(p.Ref * 100f);
                h = h * 397 + (int)(p.Slope * 1000f);
                h = h * 397 + (int)(p.Bon * 1000f);
                h = h * 397 + (int)(p.Pen * 1000f);
                return h;
            }
        }

        /// <summary>Tozsamosc stanu koni partii (nowy obiekt po dobie i odswiezeniu); 0 - bez stanu.</summary>
        private static int StateId(MobileParty mp)
        {
            var st = HorseState.Of(mp);
            return st != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(st) : 0;
        }

        private static int KeyOf(MobileParty mp)
        {
            unchecked
            {
                int h = (int)CampaignTime.Now.ToDays;
                if (mp.IsMainParty) h = h * 31 + (int)CampaignTime.Now.ToHours;
                h = h * 397 + StateId(mp);
                h = h * 397 + (mp.MemberRoster != null ? mp.MemberRoster.VersionNo : 0);
                h = h * 397 + (mp.ItemRoster != null ? mp.ItemRoster.VersionNo : 0);
                var att = mp.AttachedParties;
                if (att != null)
                {
                    h = h * 397 + att.Count;
                    for (int i = 0; i < att.Count; i++)
                    {
                        var ap = att[i];
                        if (ap == null) continue;
                        h = h * 397 + StateId(ap);
                        h = h * 397 + (ap.MemberRoster != null ? ap.MemberRoster.VersionNo : 0);
                        h = h * 397 + (ap.ItemRoster != null ? ap.ItemRoster.VersionNo : 0);
                    }
                }
                return h;
            }
        }

        private static List<Lot> Tmp(ref List<Lot> l) { if (l == null) l = new List<Lot>(64); else l.Clear(); return l; }

        private static void AddParty(MobileParty x, Rec r, P p, bool own, List<Lot> tabor, List<Lot> free)
        {
            var mr = x.MemberRoster;
            if (mr == null) return;
            r.Men += mr.TotalManCount;
            r.FootV += x.Party != null ? x.Party.NumberOfMenWithoutHorse : 0;
            var ir = x.ItemRoster;
            if (ir != null)
            {
                r.MountsV += ir.NumberOfMounts;
                for (int i = 0; i < ir.Count; i++)
                {
                    var el = ir[i];
                    var it = el.EquipmentElement.Item;
                    if (it == null || !it.HasHorseComponent || el.Amount <= 0) continue;
                    var hc = it.HorseComponent;
                    if (hc == null || !hc.IsMount) continue;   // juczne poza luzakami (jak NumberOfMounts gry)
                    tabor.Add(new Lot { Lv = -1, Q = Q(it, el.EquipmentElement.ItemModifier), N = el.Amount });
                }
            }
            var st = HorseState.Of(x);
            if (st != null) r.Free += st.FreeAll;   // ta sama liczba co K0 w kolumnie (MarchPace.CountColumn)
            // jezdzcy: bohaterowie z wlasnym koniem, szeregowi wedlug zrodla; jezdzcy wielblada, slonia itd. (wzorzec bez zwyklego konia) - zawsze kon wzorca
            int regular = 0, low = 0, high = 0;
            double tplD = 0, tplQ = 0;
            var c = Settings.Current;
            int highFrom = c != null ? Math.Min(c.WarHorseFromTier, c.NobleHorseFromTier) : 4;
            for (int i = 0; i < mr.Count; i++)
            {
                var ch = mr.GetCharacterAtIndex(i);
                int n = mr.GetElementNumber(i);
                if (ch == null || n <= 0 || !ch.IsMounted) continue;
                if (ch.IsHero)
                {
                    float hq = 0f;
                    try { var eq = ch.Equipment; if (eq != null) { var ee = eq[EquipmentIndex.Horse]; hq = Q(ee.Item, ee.ItemModifier); } } catch { hq = 0f; }
                    r.Riders += n; r.RidersHero += n; r.RidersD += n * D(hq, p); r.RidersQ += n * hq;
                    continue;
                }
                var tp = TemplateOf(ch);
                r.RegRiders += n;
                if (!tp.Plain) { r.Riders += n; r.RidersTpl += n; r.RidersD += n * D(tp.Q, p); r.RidersQ += n * tp.Q; continue; }
                regular += n;
                if (ch.Tier >= highFrom) high += n; else low += n;
                tplD += n * D(tp.Q, p); tplQ += n * tp.Q;
            }
            ArmQ aq = st != null ? ArmOf(x, st, low, high) : null;
            if (aq != null && aq.Spare != null) free.AddRange(aq.Spare);   // wolne konie zbrojowni z szybkoscia (najlepsze najpierw)
            if (regular <= 0) return;
            Lot[] ride = null; int ridden = 0, un = 0;
            if (aq != null)
            {
                ride = aq.Ride; ridden = aq.Ridden;
                un = Math.Min(Math.Max(0, aq.Unhorsed), regular);
            }
            else if (x.IsMainParty && own)
            {
                var arm = HorseFate.PlayerArmory();
                if (arm != null)
                {
                    var lots = new List<Lot>();
                    for (int i = 0; i < arm.Count; i++)
                    {
                        var el = arm[i];
                        var it = el.EquipmentElement.Item;
                        if (it == null || el.Amount <= 0 || !Stables.IsPlainMount(it)) continue;
                        int lv = Stables.Level(it.ItemCategory);
                        if (lv < 0) continue;
                        lots.Add(new Lot { Lv = lv, Q = Q(it, el.EquipmentElement.ItemModifier), N = el.Amount });
                    }
                    var rl = new List<Lot>();
                    un = Math.Min(Assign(lots, low, high, rl, null), regular);
                    ride = rl.ToArray();
                    foreach (var y in rl) ridden += y.N;
                    if (ridden <= 0) ride = null;
                }
            }
            r.Unhorsed += un;
            int horsed = regular - un;
            if (ride != null && ridden > 0 && horsed > 0)
            {
                // konie pod jezdzcami wedlug przydzialu; sklad mogl sie zmienic od pieczatki - skala do jezdzcow z koniem teraz
                double sd = 0, sq = 0; int sn = 0;
                foreach (var y in ride) { sd += y.N * D(y.Q, p); sq += y.N * y.Q; sn += y.N; }
                double k = horsed / (double)Math.Max(1, sn);
                r.Riders += horsed; r.RidersArm += horsed; r.RidersD += sd * k; r.RidersQ += sq * k;
            }
            else if (horsed > 0)
            {
                double k = horsed / (double)regular;
                r.Riders += horsed; r.RidersTpl += horsed; r.RidersD += tplD * k; r.RidersQ += tplQ * k;
            }
            if (un > 0)
            {
                if (own) r.Walk += un;   // krok 3 (klucz w Build): bez konia - pieszo
                else { double k = un / (double)regular; r.Riders += un; r.RidersTpl += un; r.RidersD += tplD * k; r.RidersQ += tplQ * k; }   // bez reguly wlasnego sprzetu DTE daje konia wzorca
            }
        }

        /// <summary>Najlepsze n luzakow z puli (tabor + wolne konie zbrojowni, gdy withFree): suma d i szybkosci.</summary>
        private static int Best(List<Lot> tabor, List<Lot> free, bool withFree, int n, P p, out double sd, out double sq)
        {
            sd = 0; sq = 0;
            if (n <= 0) return 0;
            var all = Tmp(ref _tmpC);
            all.AddRange(tabor);
            if (withFree) all.AddRange(free);
            all.Sort(ByQDesc);
            int got = 0;
            for (int i = 0; i < all.Count && got < n; i++)
            {
                int t = Math.Min(n - got, all[i].N);
                if (t <= 0) continue;
                sd += t * D(all[i].Q, p); sq += t * all[i].Q; got += t;
            }
            return got;
        }

        private static Rec Build(MobileParty mp, Settings s, P p, int sig, int key)
        {
            long t0 = Stopwatch.GetTimestamp();
            var r = new Rec { Sig = sig, Key = key };
            bool own = GarrisonKit.OwnKitOn;
            var tabor = Tmp(ref _tmpA); var free = Tmp(ref _tmpB);
            AddParty(mp, r, p, own, tabor, free);
            var att = mp.AttachedParties;
            if (att != null)
                for (int i = 0; i < att.Count; i++) { var ap = att[i]; if (ap != null) { AddParty(ap, r, p, own, tabor, free); r.Att++; } }
            if (!s.RidersWithoutHorseWalk) r.Walk = 0;   // krok 3 wylaczony: bez konia - dalej jazda gry (bez wpisu jakosci: konia nie ma)
            int footEff = r.FootV + r.Walk;
            int carriedV = Math.Min(r.FootV, r.MountsV);
            bool k1 = s.HorsesArmouryCarryFootmen;
            int carriedAll = Math.Min(footEff, r.MountsV + (k1 ? r.Free : 0));
            r.Extra = Math.Max(0, carriedAll - carriedV);
            r.ExtraFree = k1 && carriedAll > Math.Min(footEff, r.MountsV);
            // jakosc liczona zawsze (linia doby pokazuje d takze przy wylaczonym kluczu); wpisy i sufit - tylko przy HorseQualitySpeed
            r.SpareF = Best(tabor, free, k1, carriedAll, p, out r.SpareFD, out r.SpareFQ);
            int capFoot = Math.Min(footEff, r.MountsV + (s.SpareHorsesCarryFootmen ? r.Free : 0));
            double cq;
            r.SpareC = Best(tabor, free, s.SpareHorsesCarryFootmen, capFoot, p, out r.SpareCD, out cq);
            if (r.Men > 0)
            {
                r.FHq = s.HorseQualitySpeed ? (float)((r.RidersD + r.SpareFD) / r.Men) : 0f;
                r.FDry = (float)((0.15 * r.Extra - 0.3 * r.Walk) / r.Men) + r.FHq;
            }
            if (_inDaily) { _dBuilds++; _dBuildTicks += Stopwatch.GetTimestamp() - t0; }
            else { Interlocked.Increment(ref _builds); Interlocked.Add(ref _buildTicks, Stopwatch.GetTimestamp() - t0); }
            return r;
        }

        private static bool InScope(MobileParty mp)
        {
            return mp != null && mp.MemberRoster != null && !mp.IsCurrentlyAtSea && !mp.IsVillager && !mp.IsCaravan && !mp.IsGarrison && !mp.IsMilitia;
        }

        /// <summary>MarchPace.SpeedPostfix: poczatek / koniec jednego wywolania (pamiec watku - rekord i mokro liczone raz).</summary>
        internal static void Begin(MobileParty mp) { _mOn = true; _mHave = false; _mMp = mp; _mRec = null; _mWet = -1f; }
        internal static void End() { _mOn = false; _mHave = false; _mMp = null; _mRec = null; }

        private static Rec GetFast(MobileParty mp)
        {
            if (_mOn && ReferenceEquals(mp, _mMp))
            {
                if (!_mHave) { _mRec = Get(mp); _mHave = true; }
                return _mRec;
            }
            return Get(mp);
        }

        /// <summary>Rekord partii z pamieci (null - klucze wylaczone, partia poza regula, pusta, potkniecie).</summary>
        internal static Rec Get(MobileParty mp)
        {
            var s = Settings.Current;
            if (_dry || !AnyOn(s) || !InScope(mp)) return null;
            try
            {
                var p = Params(s);
                int sig = Sig(s, p), key = KeyOf(mp);
                var d = _rec;
                Rec r;
                if (d.TryGetValue(mp, out r) && r != null && r.Sig == sig && r.Key == key) return r.Men > 0 ? r : null;
                r = Build(mp, s, p, sig, key);
                d[mp] = r;   // podmiana w calosci
                return r.Men > 0 ? r : null;
            }
            catch (Exception e) { Stumble("Get", e); return null; }
        }

        private static float WetMul(MobileParty mp)
        {
            if (_mOn && ReferenceEquals(mp, _mMp))
            {
                if (_mWet < 0f) _mWet = WetNow(mp);
                return _mWet;
            }
            return WetNow(mp);
        }

        private static float WetNow(MobileParty mp)
        {
            try
            {
                return Campaign.Current.Models.MapWeatherModel.GetWeatherEffectOnTerrainForPosition(mp.Position.ToVec2())
                       == MapWeatherModel.WeatherEventEffectOnTerrain.Wet ? 0.7f : 1f;
            }
            catch { return 1f; }
        }

        // ------------------------------------------------------------ MarchPace: wspolczynniki, sufit, pieszo

        /// <summary>MarchPace.SpeedPostfix PRZED sufitem kolumny: wpisy krokow 1-3 (AddFactor - sumuja sie z "Cavalry" gry w jednym ExplainedNumber).</summary>
        internal static void Apply(MobileParty mp, ref ExplainedNumber result)
        {
            if (_dry) return;
            var s = Settings.Current;
            if (!AnyOn(s) || !InScope(mp)) return;
            if (!SpeedDepth.OutermostFinal) return;
            long t = (++_calls & 15) == 1 ? Stopwatch.GetTimestamp() : 0L;
            try
            {
                var r = GetFast(mp);
                if (r == null) return;
                float n = r.Men;
                bool any = r.Extra > 0 || r.Walk > 0 || (s.HorseQualitySpeed && (r.Riders > 0 || r.SpareF > 0));
                if (!any) return;
                float wm = WetMul(mp);
                if (r.Extra > 0) result.AddFactor(0.15f * r.Extra / n * wm, r.ExtraFree ? TxArm : TxArmWalk);
                if (s.HorseQualitySpeed)
                {
                    if (r.Riders > 0 && Math.Abs(r.RidersD) > 1e-6) result.AddFactor((float)(r.RidersD / n) * wm, TxRiders);
                    if (r.SpareF > 0 && Math.Abs(r.SpareFD) > 1e-6) result.AddFactor((float)(r.SpareFD / n) * wm, TxSpare);
                }
                if (r.Walk > 0) result.AddFactor(-0.3f * r.Walk / n * wm, TxWalk);
            }
            catch (Exception e) { Stumble("Apply", e); }
            finally { if (t != 0) { _ticks += Stopwatch.GetTimestamp() - t; _sampled++; } }
        }

        /// <summary>Jezdzcy bez konia idacy pieszo (krok 3) - MarchPace.CountColumn dolicza do piechoty (Cap i Explain).</summary>
        internal static int WalkOf(MobileParty mp)
        {
            var s = Settings.Current;
            if (s == null || !s.RidersWithoutHorseWalk) return 0;
            var r = GetFast(mp);
            return r != null ? r.Walk : 0;
        }

        /// <summary>Mnozniki sufitu kolumny (krok 2): sytuacja 1 - (1 + srednie d jezdzcow), sytuacja 2 - (1 + srednie d luzakow pod sufitem); w mokro -30%.
        /// Te same w Cap() z ColumnPostfix i z Explain() (okno "Review the army").</summary>
        internal static void CapMul(MobileParty mp, out float rider, out float foot)
        {
            rider = 1f; foot = 1f;
            var s = Settings.Current;
            if (s == null || !s.HorseQualitySpeed) return;
            try
            {
                var r = GetFast(mp);
                if (r == null) return;
                float wm = (r.Riders > 0 || r.SpareC > 0) ? WetMul(mp) : 1f;
                if (r.Riders > 0) rider = 1f + (float)(r.RidersD / r.Riders) * wm;
                if (r.SpareC > 0) foot = 1f + (float)(r.SpareCD / r.SpareC) * wm;
            }
            catch (Exception e) { Stumble("CapMul", e); rider = 1f; foot = 1f; }
        }

        /// <summary>Sufit kolumny bez kroku 2 i z nim (jednostki mapy) w tym samym wywolaniu - linia doby.</summary>
        internal static void NoteCap(float before, float after)
        {
            _capN++; _capBefore += before; _capAfter += after;
            if (after > before + 0.001f) _capUp++; else if (after < before - 0.001f) _capDown++;
        }

        // ------------------------------------------------------------ start sesji i doba

        private static string F2(double v) { return v.ToString("0.00", Inv); }
        private static string F3(double v) { return v.ToString("0.000", Inv); }
        private static string Pc(double v) { return (v >= 0 ? "+" : "") + (v * 100.0).ToString("0.0", Inv) + "%"; }
        private static double Ms(long ticks) { return ticks * 1000.0 / Stopwatch.Frequency; }

        private static readonly string[] CatName = { "horse", "war_horse", "noble_horse", "juczne", "egzotyczne" };

        internal static void OnSessionLaunched()
        {
            Reset();
            var s = Settings.Current;
            try
            {
                var lists = new List<int>[5];
                for (int i = 0; i < 5; i++) lists[i] = new List<int>();
                var items = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObjectTypeList<ItemObject>() : null;
                if (items != null)
                    foreach (var it in items)
                    {
                        if (it == null || !it.HasHorseComponent || it.HorseComponent == null) continue;
                        int c = HorseBook.Cat(it);
                        if (c < 0 || c > 4) continue;
                        if (c != 3 && !it.HorseComponent.IsMount) continue;
                        lists[c].Add(it.HorseComponent.Speed);
                    }
                var p = s != null ? Params(s) : new P { Ref = 56, Slope = 0.6f, Bon = 0.1f, Pen = 0.15f };
                var sb = new StringBuilder(600);
                sb.Append("Konie a tempo (HQ): start sesji | klucze: zbrojownia niesie piechure (HorsesArmouryCarryFootmen) ").Append(s != null && s.HorsesArmouryCarryFootmen ? "TAK" : "NIE")
                  .Append(", jakosc konia (HorseQualitySpeed) ").Append(s != null && s.HorseQualitySpeed ? "TAK" : "NIE")
                  .Append(", jezdzcy bez konia pieszo (RidersWithoutHorseWalk) ").Append(s != null && s.RidersWithoutHorseWalk ? "TAK" : "NIE")
                  .Append(" (TroopsFightWithOwnKitOnly ").Append(GarrisonKit.OwnKitOn ? "TAK" : "NIE").Append(")")
                  .Append(" | wzor: Ref ").Append(F2(p.Ref)).Append(", nachylenie ").Append(F2(p.Slope)).Append(", premia do ").Append(Pc(p.Bon)).Append(", kara do ").Append(Pc(-p.Pen))
                  .Append(" | szybkosc koni wczytanych (HorseComponent.Speed): ");
                for (int c = 0; c < 5; c++)
                {
                    var l = lists[c];
                    l.Sort();
                    if (c > 0) sb.Append("; ");
                    sb.Append(CatName[c]).Append(' ').Append(l.Count);
                    if (l.Count > 0)
                    {
                        int med = l[l.Count / 2];
                        sb.Append(" (min ").Append(l[0]).Append(", mediana ").Append(med).Append(", max ").Append(l[l.Count - 1]);
                        if (c != 3) sb.Append(", d mediany ").Append(Pc(D(med, p)));
                        sb.Append(')');
                    }
                }
                sb.Append(" | latka: MarchPace.SpeedPostfix (modeli ").Append(MapRoads.SpeedModels).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("OnSessionLaunched", e); }
        }

        private sealed class Grp { public int N; public double F, D, Spd; }

        /// <summary>Probka "teraz / bez 311d" - model predkosci wprost (jak MapRoads), drugi raz z wylaczonymi krokami 311d w tym watku.</summary>
        private static void Sample(MobileParty mp, out float now, out float without)
        {
            now = without = -1f;
            var m = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.PartySpeedCalculatingModel : null;
            if (m == null) return;
            now = m.CalculateFinalSpeed(mp, m.CalculateBaseSpeed(mp, false, 0, 0)).ResultNumber;
            _dry = true;
            try { without = m.CalculateFinalSpeed(mp, m.CalculateBaseSpeed(mp, false, 0, 0)).ResultNumber; }
            finally { _dry = false; }
        }

        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTick po HorseState.Daily - stan koni swiezy): linia "Konie a tempo (HQ)", porzadek pamieci.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            long tl = Stopwatch.GetTimestamp();
            _inDaily = true;
            try
            {
                int day = (int)CampaignTime.Now.ToDays - 1;
                // grupy: jazda (>= 70% konnych z koniem) tania / srednia / dobra, piechota na luzakach, pieszo
                var g = new Grp[5]; for (int i = 0; i < 5; i++) g[i] = new Grp();
                int parties = 0, riders = 0, rArm = 0, rTpl = 0, rHero = 0, spare = 0, extraP = 0, extraN = 0, unhorsed = 0, walk = 0, regRiders = 0;
                double rD = 0, rQ = 0, sD = 0, sQ = 0, extraF = 0, fMax = 0, hqMin = 0, hqMax = 0;
                int mism = 0, withState = 0;
                var cand = new List<KeyValuePair<float, MobileParty>>();
                bool on = AnyOn(s);
                foreach (var mp in MobileParty.All)
                {
                    try
                    {
                        if (mp == null || !mp.IsActive || !InScope(mp) || mp.AttachedTo != null) continue;
                        if (!on) continue;
                        var r = Get(mp);
                        if (r == null) continue;
                        var st = HorseState.Of(mp);
                        ArmQ aq;
                        if (st != null && _arm.TryGetValue(mp, out aq) && aq != null && ReferenceEquals(aq.Src, st)) { withState++; if (aq.SpareAll != st.FreeAll) mism++; }
                        parties++;
                        riders += r.Riders; rArm += r.RidersArm; rTpl += r.RidersTpl; rHero += r.RidersHero; rD += r.RidersD; rQ += r.RidersQ;
                        spare += r.SpareF; sD += r.SpareFD; sQ += r.SpareFQ;
                        unhorsed += r.Unhorsed; walk += r.Walk; regRiders += r.RegRiders;
                        if (r.Extra > 0 && !mp.IsMainParty) { extraP++; extraN += r.Extra; extraF += 0.15 * r.Extra / r.Men; }
                        fMax = Math.Max(fMax, Math.Abs(r.FDry));
                        hqMin = Math.Min(hqMin, r.FHq); hqMax = Math.Max(hqMax, r.FHq);
                        int footEff = r.FootV + r.Walk;
                        int carried = Math.Min(footEff, r.MountsV + (s.SpareHorsesCarryFootmen ? r.Free : 0));
                        int gi;
                        if (r.Riders >= 0.7 * r.Men)
                        {
                            double md = r.Riders > 0 ? r.RidersD / r.Riders : 0;
                            gi = md < -0.03 ? 0 : md > 0.03 ? 2 : 1;
                        }
                        else gi = footEff > carried ? 4 : 3;
                        var G = g[gi];
                        G.N++; G.F += r.FDry;
                        G.D += gi <= 2 ? (r.Riders > 0 ? r.RidersD / r.Riders : 0) : (r.SpareF > 0 ? r.SpareFD / r.SpareF : 0);
                        float spd = 0f; try { spd = mp.Speed; } catch { spd = 0f; }
                        G.Spd += spd;
                        if (Math.Abs(r.FDry) > 0.001f) cand.Add(new KeyValuePair<float, MobileParty>(Math.Abs(r.FDry), mp));
                    }
                    catch (Exception e) { Stumble("Daily(partia)", e); }
                }
                // probki: gracz (jesli w regule) + 4 partie AI o najwiekszym |F|
                cand.Sort((a, b) => b.Key.CompareTo(a.Key));
                var pick = new List<MobileParty>();
                var main = MobileParty.MainParty;
                if (on && main != null && main.IsActive && InScope(main) && main.AttachedTo == null) pick.Add(main);
                foreach (var kv in cand) { if (pick.Count >= 5) break; if (kv.Value != main) pick.Add(kv.Value); }
                var samples = new List<string>();
                foreach (var mp in pick)
                {
                    try
                    {
                        float now, without; Sample(mp, out now, out without);
                        var r = Get(mp);
                        float mr, mf; CapMul(mp, out mr, out mf);
                        samples.Add("'" + (mp.Name != null ? mp.Name.ToString() : mp.StringId) + "' " + (r != null ? r.Men : 0) + " ludzi, F " + Pc(r != null ? r.FDry : 0)
                                    + ": teraz " + F2(now) + " / bez 311d " + F2(without) + ", mnoznik sufitu jazda x" + F3(mr) + " luzaki x" + F3(mf));
                    }
                    catch (Exception e) { Stumble("Daily(probka)", e); }
                }
                // porzadek pamieci: partie nieczynne
                foreach (var kv in _rec) if (kv.Key == null || !kv.Key.IsActive) { Rec x; _rec.TryRemove(kv.Key, out x); }
                foreach (var kv in _arm) if (kv.Key == null || !kv.Key.IsActive) { ArmQ x; _arm.TryRemove(kv.Key, out x); }

                var sb = new StringBuilder(1400);
                sb.Append("Konie a tempo (HQ): dzien ").Append(day)
                  .Append(" | klucze: zbrojownia niesie piechure ").Append(s != null && s.HorsesArmouryCarryFootmen ? "TAK" : "NIE")
                  .Append(", jakosc konia ").Append(s != null && s.HorseQualitySpeed ? "TAK" : "NIE")
                  .Append(", jezdzcy bez konia pieszo ").Append(s != null && s.RidersWithoutHorseWalk ? "TAK" : "NIE")
                  .Append(" (TroopsFightWithOwnKitOnly ").Append(GarrisonKit.OwnKitOn ? "TAK" : "NIE").Append(")");
                string[] gn = { "jazda tania", "jazda srednia", "jazda dobra", "piechota na luzakach", "pieszo" };
                sb.Append(" | partie marszu ").Append(parties).Append(":");
                for (int i = 0; i < 5; i++)
                {
                    var G = g[i];
                    sb.Append(i == 0 ? " " : "; ").Append(gn[i]).Append(' ').Append(G.N);
                    if (G.N > 0) sb.Append(" (d ").Append(Pc(G.D / G.N)).Append(", F ").Append(Pc(G.F / G.N)).Append(", predkosc ").Append(F2(G.Spd / G.N)).Append(')');
                }
                sb.Append(" | jezdzcy z koniem ").Append(riders).Append(" (zbrojownia ").Append(rArm).Append(", wzorzec ").Append(rTpl).Append(", bohaterowie ").Append(rHero)
                  .Append("), d srednio ").Append(Pc(riders > 0 ? rD / riders : 0)).Append(", szybkosc konia srednio ").Append(F2(riders > 0 ? rQ / riders : 0))
                  .Append(" | luzaki pod piechura ").Append(spare).Append(", d srednio ").Append(Pc(spare > 0 ? sD / spare : 0)).Append(", szybkosc srednio ").Append(F2(spare > 0 ? sQ / spare : 0))
                  .Append(" | zbrojownia AI niesie piechure (krok 1): partii ").Append(extraP).Append(", piechurow ").Append(extraN).Append(", wspolczynnik srednio ").Append(Pc(extraP > 0 ? extraF / extraP : 0))
                  .Append(" | jezdzcy bez konia (krok 3): ").Append(unhorsed).Append(" z ").Append(regRiders).Append(" konnych szeregowych, pieszo ").Append(walk)
                  .Append(" | sufit kolumny (krok 2): wywolan ").Append(_capN).Append(", srednio przed ").Append(_capN > 0 ? F2(_capBefore / _capN) : "-")
                  .Append(" / po ").Append(_capN > 0 ? F2(_capAfter / _capN) : "-").Append(", wyzszy ").Append(_capUp).Append(", nizszy ").Append(_capDown)
                  .Append(" | probki: ").Append(samples.Count > 0 ? string.Join("; ", samples) : "-")
                  .Append(" | kontrola: |F| najwiecej ").Append(F3(fMax)).Append(", jakosc koni F od ").Append(F3(hqMin)).Append(" do ").Append(F3(hqMax))
                  .Append(" (granice -").Append(F3(s != null ? Params(s).Pen : 0)).Append(" / +").Append(F3(s != null ? Params(s).Bon : 0)).Append(")")
                  .Append(", stany koni 311d ").Append(withState).Append(" (wolne 311d inne niz Balance - jezdzcy egzotyczni: ").Append(mism).Append(')');
                // goraca sciezka = Apply (probka co 16. wywolanie, w niej przeliczenia rekordu); przeliczenia na mapie mierzone kazde (czesc z nich jest w probce Apply)
                double applyMs = _sampled > 0 ? Ms(_ticks) * ((double)_calls / _sampled) : 0.0;
                double buildMs = Ms(Interlocked.Read(ref _buildTicks));
                sb.Append(" | koszt: goraca sciezka ").Append(F2(applyMs)).Append(" ms/").Append(_calls).Append(" wywolan (prog 2 ms/dobe), przeliczen rekordu na mapie ")
                  .Append(Interlocked.Read(ref _builds)).Append(" (").Append(F2(buildMs)).Append(" ms), w linii doby ").Append(_dBuilds).Append(" (").Append(F2(Ms(_dBuildTicks))).Append(" ms)");
                sb.Append(" | potkniecia dzis ").Append(Volatile.Read(ref _stumblesDay)).Append(", od startu sesji ").Append(Volatile.Read(ref _stumbles));
                lock (Gate) { if (_errSites.Count > 0) sb.Append(" (").Append(string.Join(", ", _errSites)).Append(')'); }
                sb.Append(" | linia ").Append(Ms(Stopwatch.GetTimestamp() - tl).ToString("0.0", Inv)).Append(" ms.");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { _inDaily = false; }
            ResetDay();
        }
    }
}
