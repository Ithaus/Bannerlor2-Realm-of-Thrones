using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// R3-06c (noc 20.10, galaz p5-stada; POMYSLY-35 poz. 14, audyt 02 rozdz. 2.2, 2.3 i E pkt 2) - STADA JAKO LUP. Klucz VillageHerdsAsLoot.
    /// Stado wsi to majatek okregu, nie towar w magazynie: dwie czesci - drobne (owce i swinie: sheep, hog, wool) i bydlo (cow, cheese, butter). Stan H w [Floor, 1]
    /// (1 = pelne stado - wsi z pelnym stadem nie ma w slowniku).
    ///  - PRODUKCJA: towary stada x H swojej czesci. Wies pod KS - w StartCalibration.ComputeDistrict (gra, z czynnikami: zaraz po Produce, przed pracami okregu; Plan - nie);
    ///    lancuch gry (wies poza KS, rampa) - w MaterialLaw.ProdPostfix po rekach wojny E4/7 (ten sam wzor czynnik x suma) i w ChainFactor.
    ///  - RABUNEK: DistrictRaid.Loot (rabunek osada po osadzie, AI i gracz) - udzial s (trafieni / ludzie w domu) - stado traci l = min(MaxLoss, TakeMult x s) swojej
    ///    obecnej wielkosci (stado to lup nr 1 - ginie szybciej niz ludzie uciekaja), nie mniej niz do Floor. Ubytek w sztukach gry = ubytek H x stado wsi w sztukach
    ///    (stawka zwierzat przy pelnym stadzie x dni roku / przyrost roczny - ile zwierzat trzeba, zeby co rok dawac taki przychowek). Napastnik pedzi z tego najwyzej
    ///    DrivePerMan sztuk na czlowieka (owce i swinie najpierw, potem bydlo) - do taboru wodza w ramce "produkcja wsi" ksiegi towarow (prawdziwe zwierzeta okregu);
    ///    reszta ubytku - zabite i rozproszone (licznik w linii, bez sztuk). Bramka krainy KS (ks-arm, f26) nie liczy kcal zabranych przez stado i ziemie jako
    ///    "glod z winy KS" (StartCalibration.Stada.cs). Rabunek bez DistrictRaid (klucz rabunku osada po osadzie wylaczony, zdarzenie gry
    ///    VillageLooted bez naszego lupu w ostatnich 3 dobach) - ubytek jak za pelny rabunek gry (s = GameRaidShare), bez zwierzat do taboru (gra daje swoj lup).
    ///  - ODROST: co dobe wies w stanie Normal: H x (1 + r)^(1 / dni roku), r: drobne GrowSmall, bydlo GrowCattle (kalendarz Armoury - rok 364 dni); do 1.
    /// Zapis: klucz arm_herds (SaveText.Sync, "v1|id:drobne:bydlo;..."); brak klucza (stary zapis) = pelne stada. Klucz wylaczony - czynnik 1 i bez ubytku (stan zostaje).
    /// Watki: slownik stada podmieniany w calosci na watku glownym (czytany bez blokad z watkow AI); pamiec stawek i liczniki - tylko watek glowny.
    /// </summary>
    internal static class VillageHerds
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const float Floor = 0.10f;          // najmniej stada (zwierzeta ukryte w lesie i na wyzynach)
        private const double TakeMult = 1.5;        // stado ginie 1.5 x szybciej niz udzial trafionych ludzi
        private const double MaxLoss = 0.90;        // jeden rabunek zabiera najwyzej 90% obecnego stada
        private const double GrowSmall = 0.25;      // przyrost roczny owiec i swin (audyt: owce 20-30%)
        private const double GrowCattle = 0.12;     // przyrost roczny bydla (audyt: 10-15%)
        private const double DrivePerMan = 0.5;     // ile sztuk gry pedzi jeden napastnik (reszta ubytku - zabite i rozproszone)
        private const double GameRaidShare = 0.40;  // rabunek gry bez DistrictRaid: -39.7% hearth za pelny rabunek
        private const int SmallPart = 0, CattlePart = 1;
        private static readonly string[] AnimalIds = { "sheep", "hog", "cow" };   // pamiec stawek: sztuki gry na dobe przy pelnym stadzie

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.VillageHerdsAsLoot; } }

        private static volatile Dictionary<Village, float[]> _herd = new Dictionary<Village, float[]>();
        private static readonly Dictionary<Village, float[]> _rate = new Dictionary<Village, float[]>();   // tylko watek glowny
        private static readonly Dictionary<Village, int> _takeDay = new Dictionary<Village, int>();       // doba ostatniego ubytku z DistrictRaid (tylko watek glowny)
        private static string _pending;
        private static int _mainThread = -1;
        private static ItemObject _sheep, _hog, _cow, _wool, _cheese, _butter;
        private static readonly TextObject _txt = new TextObject("{=!}Armoury: herd of the district (raids)");
        // liczniki doby i sesji
        private static int _dRaids, _dGame, _dDriveS, _dDriveC, _sRaids, _sDrive, _stumbles, _dStumbles, _dZero;
        private static long _dKilled, _sKilled;
        private static string _dWorst; private static float _dWorstLoss;

        internal static void Reset()
        {
            _herd = new Dictionary<Village, float[]>();
            _rate.Clear(); _takeDay.Clear(); _pending = null;
            _sheep = _hog = _cow = _wool = _cheese = _butter = null;
            _dRaids = _dGame = _dDriveS = _dDriveC = _sRaids = _sDrive = _stumbles = _dStumbles = _dZero = 0; _dKilled = _sKilled = 0; _dWorst = null; _dWorstLoss = 0f;
            try { StartCalibration.HerdLandReset(); } catch (Exception e) { Stumble("Reset(bramka)", e); }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            if (_stumbles <= 3) { try { Log.Error("VillageHerds." + where, e); } catch { } }
        }

        private static bool Main { get { return _mainThread >= 0 && System.Threading.Thread.CurrentThread.ManagedThreadId == _mainThread; } }

        private static void EnsureItems()
        {
            if (_sheep != null || MBObjectManager.Instance == null) return;
            var om = MBObjectManager.Instance;
            _hog = om.GetObject<ItemObject>("hog"); _cow = om.GetObject<ItemObject>("cow"); _wool = om.GetObject<ItemObject>("wool");
            _cheese = om.GetObject<ItemObject>("cheese"); _butter = om.GetObject<ItemObject>("butter");
            _sheep = om.GetObject<ItemObject>("sheep");
        }

        /// <summary>Czesc stada towaru (0 drobne, 1 bydlo, -1 towar spoza stada) - po id (watki AI: bez slownika, bez alokacji).</summary>
        private static int PartOf(string id)
        {
            switch (id)
            {
                case "sheep": case "hog": case "wool": return SmallPart;
                case "cow": case "cheese": case "butter": return CattlePart;
                default: return -1;
            }
        }

        /// <summary>Czynnik stada towaru we wsi (1 - pelne stado, towar spoza stada, klucz wylaczony). Sam odczyt - watki AI.</summary>
        internal static float Factor(Village v, ItemObject item)
        {
            if (v == null || item == null) return 1f;
            var h = _herd;
            if (h.Count == 0 || !KeyOn) return 1f;
            float[] a;
            if (!h.TryGetValue(v, out a)) return 1f;
            int p = PartOf(item.StringId);
            return p < 0 ? 1f : a[p];
        }

        // ------------------------------------------------------------ produkcja
        /// <summary>MaterialLaw.ProdPostfix (lancuch gry): po rekach wojny E4/7 - towar stada x H (czynnik x suma). Watek glowny - pamiec stawki przy pelnym stadzie.</summary>
        internal static void ProdStep(Village v, ItemObject item, ref ExplainedNumber r)
        {
            try
            {
                if (v == null || item == null || !KeyOn) return;
                int p = PartOf(item.StringId);
                if (p < 0) return;
                float f = Factor(v, item);
                if (f < 0.9995f)
                {
                    float sum = 1f + r.SumOfFactors;
                    if (sum > 0f) r.AddFactor((f - 1f) * sum, _txt);
                }
                if (Main && v.VillageState == Village.VillageStates.Normal)
                {
                    int k = AnimalIx(item.StringId);
                    if (k >= 0) { float x = r.ResultNumber; if (x > 0f && f > 0.01f) Remember(v, k, x / f); }
                }
            }
            catch (Exception e) { if (Main) Stumble("ProdStep", e); }
        }

        private static int AnimalIx(string id) { return id == "sheep" ? 0 : id == "hog" ? 1 : id == "cow" ? 2 : -1; }

        private static void Remember(Village v, int k, float full)
        {
            float[] a;
            if (!_rate.TryGetValue(v, out a)) { a = new float[3]; _rate[v] = a; }
            a[k] = full;
        }

        /// <summary>StartCalibration.ComputeDistrict (KS, gra - z czynnikami; watek glowny; zaraz po Produce): pamiec stawek zwierzat przy pelnym stadzie (sztuki gry na dobe,
        /// bez zimy i rak - wielkosc stada, nie przychowek sezonu) i towary stada x H swojej czesci (kg okregu - przed tym, co okreg zje sam). Recenzja (drobne): PRZED dodatkiem
        /// prac okregu (DistrictWorks, rodzaj KHerd) - kupione stado prac nie jest stadem wsi (DistrictWorks sam pedzi je przy rabunku i tnie jednostki prac).</summary>
        internal static void KsStep(Village v, double[] prod, double[] kg)
        {
            if (v == null || prod == null || kg == null || !KeyOn) return;
            try
            {
                double sigma = Math.Max(1, DailyLife.KsSigma);
                // prod jest tu jeszcze bez czynnika stada - stawka zwierzat przy PELNYM stadzie (sztuki gry na dobe, jak stawki KS)
                Remember(v, 0, (float)(prod[KsDeliver.ISheep] / (sigma * kg[KsDeliver.ISheep])));
                Remember(v, 1, (float)(prod[KsDeliver.IHog] / (sigma * kg[KsDeliver.IHog])));
                Remember(v, 2, (float)(prod[KsDeliver.ICow] / (sigma * kg[KsDeliver.ICow])));
                var h = _herd; float[] a;
                if (h.Count == 0 || !h.TryGetValue(v, out a)) return;
                double s = a[SmallPart], c = a[CattlePart];
                prod[KsDeliver.ISheep] *= s; prod[KsDeliver.IHog] *= s; prod[KsDeliver.IWool] *= s;
                prod[KsDeliver.ICow] *= c; prod[KsDeliver.ICheese] *= c; prod[KsDeliver.IButter] *= c;
            }
            catch (Exception e) { Stumble("KsStep", e); }
        }

        // ------------------------------------------------------------ rabunek
        /// <summary>DistrictRaid.Loot (na poczatku lupu kroku, PRZED wyjsciem przy udziale 0): ubytek stada za udzial s; zwierzeta do taboru wodza (najwyzej
        /// DrivePerMan na czlowieka partii wodza i doczepionych, bez przekraczania liczby ludzi stadem - kara predkosci gry). Zwraca liczbe pedzonych zwierzat (okienko gracza).
        /// Recenzja (wazne): wies oznaczona jako "rabunek DistrictRaid" ZAWSZE, takze przy s = 0 (okreg wypalony, nikogo w domu) i stadzie na dnie - inaczej zdarzenie gry
        /// VillageLooted po takim rabunku bralo 60% stada drogi zapasowej (OnLooted), choc regula E7/R1 nic nie zabiera.</summary>
        internal static int OnRaid(Village v, double s, MobileParty leader)
        {
            if (v == null || !KeyOn) return 0;
            try
            {
                _takeDay[v] = Today();
                if (!(s > 0)) { _dZero++; return 0; }
                EnsureItems();
                double l = Math.Min(MaxLoss, TakeMult * s);
                float[] lost = Shrink(v, l);
                if (lost == null) return 0;
                _dRaids++; _sRaids++;
                // stado w sztukach gry: stawka przy pelnym stadzie x dni roku / przyrost roczny
                float[] rate; _rate.TryGetValue(v, out rate);
                double year = Math.Max(1, LongYearTimeModel.DaysPerYear(Settings.Current));
                double[] gone = new double[3];
                if (rate != null)
                {
                    gone[0] = lost[SmallPart] * rate[0] * year / GrowSmall;
                    gone[1] = lost[SmallPart] * rate[1] * year / GrowSmall;
                    gone[2] = lost[CattlePart] * rate[2] * year / GrowCattle;
                }
                double cap = DriveCap(leader);
                var items = new[] { _sheep, _hog, _cow };
                int[] drive = new int[3];
                double left = cap, all = 0;
                for (int k = 0; k < 3; k++)
                {
                    all += gone[k];
                    if (items[k] == null || left < 1) continue;
                    int n = (int)Math.Floor(Math.Min(left, gone[k]));
                    if (n > 0) { drive[k] = n; left -= n; }
                }
                int total = drive[0] + drive[1] + drive[2];
                if (total > 0)
                {
                    var gf = GoodsLedger.Begin(GoodsLedger.FVillage, v);   // prawdziwe zwierzeta okregu - w ksiedze jako produkcja wsi (stado -> tabor)
                    try { for (int k = 0; k < 3; k++) if (drive[k] > 0) leader.ItemRoster.AddToCounts(items[k], drive[k]); }
                    finally { GoodsLedger.End(gf); }
                }
                _dDriveS += drive[0] + drive[1]; _dDriveC += drive[2]; _sDrive += total;
                long killed = (long)Math.Round(Math.Max(0.0, all - total));
                _dKilled += killed; _sKilled += killed;
                float worst = Math.Max(lost[SmallPart], lost[CattlePart]);
                if (worst > _dWorstLoss) { _dWorstLoss = worst; _dWorst = v.Settlement != null ? v.Settlement.Name.ToString() : "?"; }
                return total;
            }
            catch (Exception e) { Stumble("OnRaid", e); return 0; }
        }

        /// <summary>Recenzja (drobne): ile zwierzat moze pedzic wodz - DrivePerMan x ludzie partii wodza i doczepionych, ale stado taboru (juczne, zywiec, wolne wierzchowce
        /// ponad pieszych - jak DefaultPartySpeedCalculatingModel) nie wiecej niz ludzie (gra karze predkosc dopiero przy stadzie ponad ludzi). Bez partii - 0.</summary>
        private static double DriveCap(MobileParty leader)
        {
            if (leader == null || leader.ItemRoster == null || leader.MemberRoster == null) return 0.0;
            int men = 0, herd = 0, mounts = 0, foot = 0;
            AddParty(leader, ref men, ref herd, ref mounts, ref foot);
            var att = leader.AttachedParties;
            if (att != null) foreach (var mp in att) if (mp != null && mp != leader) AddParty(mp, ref men, ref herd, ref mounts, ref foot);
            herd += Math.Max(0, mounts - foot);
            return Math.Max(0.0, Math.Min(men * DrivePerMan, men - herd));
        }

        private static void AddParty(MobileParty mp, ref int men, ref int herd, ref int mounts, ref int foot)
        {
            if (mp.MemberRoster != null) men += mp.MemberRoster.TotalManCount;
            var ir = mp.ItemRoster;
            if (ir != null) { herd += ir.NumberOfPackAnimals + ir.NumberOfLivestockAnimals; mounts += ir.NumberOfMounts; }
            if (mp.Party != null) foot += mp.Party.NumberOfMenWithoutHorse;
        }

        /// <summary>Zdarzenie gry VillageLooted: rabunek bez naszego lupu stada w ostatnich 3 dobach (rabunek gry) - ubytek jak za pelny rabunek gry, bez zwierzat.</summary>
        private static void OnLooted(Village v)
        {
            if (v == null || !KeyOn) return;
            try
            {
                int d;
                if (_takeDay.TryGetValue(v, out d) && Today() - d <= 3) return;
                float[] lost = Shrink(v, Math.Min(MaxLoss, TakeMult * GameRaidShare));
                if (lost == null) return;
                _dGame++;
                float[] rate; _rate.TryGetValue(v, out rate);
                if (rate != null)
                {
                    double year = Math.Max(1, LongYearTimeModel.DaysPerYear(Settings.Current));
                    long killed = (long)Math.Round(lost[SmallPart] * (rate[0] + rate[1]) * year / GrowSmall + lost[CattlePart] * rate[2] * year / GrowCattle);
                    _dKilled += killed; _sKilled += killed;
                }
            }
            catch (Exception e) { Stumble("OnLooted", e); }
        }

        /// <summary>Stado wsi x (1 - l), nie mniej niz Floor (nowy slownik - podmiana). Zwraca ubytek H obu czesci; null - nic nie ubylo.</summary>
        private static float[] Shrink(Village v, double l)
        {
            var old = _herd; float[] a;
            float s0 = 1f, c0 = 1f;
            if (old.TryGetValue(v, out a)) { s0 = a[SmallPart]; c0 = a[CattlePart]; }
            float s1 = Math.Max(Floor, (float)(s0 * (1.0 - l))), c1 = Math.Max(Floor, (float)(c0 * (1.0 - l)));
            if (s1 >= s0 && c1 >= c0) return null;
            var nd = new Dictionary<Village, float[]>(old);
            nd[v] = new[] { s1, c1 };
            _herd = nd;
            return new[] { s0 - s1, c0 - c1 };
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }

        // ------------------------------------------------------------ doba: odrost i linia
        internal static void Daily()
        {
            try { Resolve(); } catch (Exception e) { Stumble("Resolve", e); }
            int day = Today() - 1;
            var old = _herd;
            int n = old.Count, grew = 0, full = 0;
            double sumS = 0, sumC = 0; float low = 1f; string lowName = null;
            if (n > 0 && KeyOn)
            {
                double year = Math.Max(1, LongYearTimeModel.DaysPerYear(Settings.Current));
                double gs = Math.Pow(1.0 + GrowSmall, 1.0 / year), gc = Math.Pow(1.0 + GrowCattle, 1.0 / year);
                var nd = new Dictionary<Village, float[]>(n);
                foreach (var kv in old)
                {
                    var v = kv.Key; var a = kv.Value;
                    float s = a[SmallPart], c = a[CattlePart];
                    try
                    {
                        if (v.VillageState == Village.VillageStates.Normal) { s = (float)Math.Min(1.0, s * gs); c = (float)Math.Min(1.0, c * gc); grew++; }
                    }
                    catch (Exception e) { Stumble("Daily(wies)", e); }
                    if (s >= 0.999f && c >= 0.999f) { full++; continue; }
                    nd[v] = new[] { s, c };
                    sumS += s; sumC += c;
                    float m = Math.Min(s, c);
                    if (m < low) { low = m; lowName = v.Settlement != null ? v.Settlement.Name.ToString() : "?"; }
                }
                _herd = nd;
            }
            int left = _herd.Count;
            bool any = _dRaids > 0 || _dGame > 0 || _dZero > 0;
            if (any || LogGate.Lines)
            {
                var sb = new StringBuilder(500);
                sb.Append("Stada (R3-06c): dzien ").Append(day).Append(KeyOn ? "" : " | KLUCZ Village Herds As Loot WYLACZONY (czynnik 1, bez ubytku)")
                  .Append(" | wsie z niepelnym stadem ").Append(left).Append(" (wrocilo do pelnego dzis ").Append(full).Append(", odrastalo ").Append(grew).Append(')');
                if (left > 0) sb.Append(" | srednio drobne (owce, swinie) ").Append((100 * sumS / left).ToString("0", Inv)).Append("%, bydlo ").Append((100 * sumC / left).ToString("0", Inv))
                                .Append("%, najnizej ").Append((100 * low).ToString("0", Inv)).Append("% (").Append(lowName ?? "-").Append(')');
                sb.Append(" | dzis rabunki ze stratami stada ").Append(_dRaids).Append(" (osada po osadzie) + ").Append(_dGame).Append(" (rabunek gry)")
                  .Append(", rabunki osada po osadzie bez lupu (udzial 0 - bez straty stada) ").Append(_dZero)
                  .Append(", zwierzat do taborow ").Append(_dDriveS + _dDriveC).Append(" (owce i swinie ").Append(_dDriveS).Append(", bydlo ").Append(_dDriveC).Append(')')
                  .Append(", zabite i rozproszone ").Append(_dKilled).Append(" szt.");
                if (_dWorst != null) sb.Append(", najwiekszy ubytek ").Append((100 * _dWorstLoss).ToString("0", Inv)).Append(" pkt stada (").Append(_dWorst).Append(')');
                sb.Append(" | sesja: rabunki ").Append(_sRaids).Append(", do taborow ").Append(_sDrive).Append(", zabite i rozproszone ").Append(_sKilled)
                  .Append(" | odrost: drobne ").Append((100 * GrowSmall).ToString("0", Inv)).Append("%/rok, bydlo ").Append((100 * GrowCattle).ToString("0", Inv)).Append("%/rok")
                  .Append(" | wsie ze stawka zwierzat w pamieci ").Append(_rate.Count)
                  .Append(" | bramka krainy KS: ").Append(StartCalibration.HerdLandGateText())
                  .Append(" | potkniecia dzis ").Append(_dStumbles).Append(" (sesja ").Append(_stumbles).Append(").");
                Log.Info(sb.ToString());
            }
            _dRaids = _dGame = _dDriveS = _dDriveC = _dStumbles = _dZero = 0; _dKilled = 0; _dWorst = null; _dWorstLoss = 0f;
            try { StartCalibration.HerdLandGateDayClose(); } catch (Exception e) { Stumble("Daily(bramka)", e); }
        }

        // ------------------------------------------------------------ zapis
        internal static string Export()
        {
            if (_pending != null) return _pending;                      // przed Resolve (zapis tuz po wczytaniu) - napis z zapisu bez zmian
            var sb = new StringBuilder(64 + 24 * _herd.Count);
            sb.Append("v1|");
            foreach (var kv in _herd)
            {
                var st = kv.Key != null ? kv.Key.Settlement : null;
                if (st == null || string.IsNullOrEmpty(st.StringId) || st.StringId.IndexOf(':') >= 0 || st.StringId.IndexOf(';') >= 0) continue;
                sb.Append(st.StringId).Append(':').Append(kv.Value[SmallPart].ToString("0.####", Inv)).Append(':').Append(kv.Value[CattlePart].ToString("0.####", Inv)).Append(';');
            }
            return sb.ToString();
        }

        /// <summary>Wczytanie: stan od nowa, napis odlozony do Resolve (OnSessionLaunched / pierwsza doba).</summary>
        internal static void Import(string s)
        {
            Reset();
            _pending = string.IsNullOrEmpty(s) ? null : s;
        }

        private static void Resolve()
        {
            var s = _pending;
            if (s == null) return;
            _pending = null;
            if (!s.StartsWith("v1|", StringComparison.Ordinal)) return;
            var nd = new Dictionary<Village, float[]>();
            int bad = 0;
            foreach (var e in s.Substring(3).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = e.Split(':');
                float a, b;
                if (p.Length != 3 || !float.TryParse(p[1], NumberStyles.Float, Inv, out a) || !float.TryParse(p[2], NumberStyles.Float, Inv, out b)) { bad++; continue; }
                var st = Settlement.Find(p[0]);
                if (st == null || st.Village == null) { bad++; continue; }
                a = Math.Max(Floor, Math.Min(1f, a)); b = Math.Max(Floor, Math.Min(1f, b));
                if (a >= 0.999f && b >= 0.999f) continue;
                nd[st.Village] = new[] { a, b };
            }
            _herd = nd;
            Log.Info("Stada (R3-06c): z zapisu " + nd.Count + " wsi z niepelnym stadem" + (bad > 0 ? ", pominiete wpisy " + bad : "") + ".");
        }

        // ------------------------------------------------------------ zdarzenia
        internal static void RegisterEvents(CampaignBehaviorBase owner)
        {
            _mainThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(owner, _ => { try { Reset(); } catch (Exception e) { Stumble("Reset", e); } });
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(owner, _ => { try { _mainThread = System.Threading.Thread.CurrentThread.ManagedThreadId; Resolve(); } catch (Exception e) { Stumble("Resolve", e); } });
            CampaignEvents.VillageLooted.AddNonSerializedListener(owner, v => { try { OnLooted(v); } catch (Exception e) { Stumble("OnLooted", e); } });
        }
    }
}
