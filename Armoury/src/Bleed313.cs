using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 313d (01.11, paczka "krwawienie i ranni", raport rozp313/krwawienie-raport.md + krytyka - "POPRAWIONA PROPOZYCJA"; Jeff: "nie zawsze powinno byc krwawienie,
    /// tylko zalezne od atletyki, szansy na krwawienie i glebokosci rany"; "jesli ma malo zdrowia ... jest po prostu ranny i nie biega jak glupi tylko wolno
    /// wycofuje sie").
    ///  - Krok 0 (BleedDeathLog): pomiar - zgony z krwawienia (zabici / nieprzytomni, z kredytem, w panice / w odwrocie 143, HP na starcie krwawienia). Takze przy
    ///    starej regule (BleedFromWounds NIE) - porownanie przed i po.
    ///  - Krok A (BleedFromWounds): rzut przy trafieniu (FieldCraft.OnAgentHit -> kolejka pod zamkiem, bo OnAgentHit bywa wolany z ticku asynchronicznego -
    ///    BattleGore.cs:35, HorsePursuit.cs:48; zdejmuje ja tick FieldCraft na glownym watku). Szansa = BleedChanceDeepWound x typ x czesc ciala x glebokosc x pancerz
    ///    x kondycja (najwyzej 95%); strata calkowita = BleedLossShare x obrazenia x kondycja, tempo maleje wykladniczo (stala BleedClotSeconds), ustaje ponizej
    ///    0.05 HP/s. Rany sie sumuja; pelne HP (leczenie autotestu, RBM) zeruje krwawienie. Zgon z krwawienia zalicza sie temu, kto zadal rane (gdy jeszcze zyje).
    ///  - Krok B: rzut odwrotu 143 (WoundedFleeByChance, BrokenMen) i rzut paniki z bolu (PainBreakByChance, FieldCraft) - raz na swieza rane, nie co takt.
    ///  - Kustykanie (WoundedLimp): pieszy AI w odwrocie 143 albo z HP &lt;= WoundedLimpBelowHp (takze w panice) - limit predkosci = krok x WoundedLimpWalkShare.
    ///    Limit wpisuje FieldCraft co 0.2 s (glowny watek) i latka AdjustSpeedLimit (MarchToContact.PostSpeed, watki robocze - tablica jak AiBreath.CapOf), bo gra
    ///    przepisuje limit szyku co ok. 0.5 s (HumanAIComponent.ParallelUpdateFormationMovement) i przy natywnym odwrocie (OnRetreating -> AdjustSpeedLimit(-1)).
    ///    Artem (ArtemsLivelyAnimationsAgentStatCalculateModel: ponizej HPThreshold MaxSpeedMultiplier x0.35 noga / x0.7 brzuch i glowa) mnozy MAKSIMUM agenta;
    ///    nasz limit jest bezwzgledny (m/s, isMultiplier false) - silnik bierze mniejsze z dwoch, kary sie nie mnoza (krok 1.6 m/s x 0.7 = ok. 1.1 m/s).
    /// Wszystkie liczniki - glowny watek; kolejka i tablica capu - jak opisano. Wyjatek na czlowieka liczony (potkniecia w linii), funkcja zostaje wlaczona.
    /// </summary>
    internal static class Bleed313
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const float StopRate = 0.05f;       // HP/s - ponizej krwawienie ustaje (krzepniecie)
        internal const float WithdrawBleedFactor = 2f;   // krytyka: odwrot 143 - krwawiacy wycofuje sie dwa razy chetniej
        private const int QueueMax = 4096;
        private const float LimpFallbackWalk = 1.6f;      // m/s, gdy WalkSpeedCached nieznane

        // ------------------------------------------------------------ kolejka trafien (OnAgentHit -> tick FieldCraft)

        internal struct Hit
        {
            public Agent V;
            public int By;              // indeks zadajacego (-1 = nikt)
            public Agent ByA;           // recenzja 313d: zadajacy (affectorAgent z OnAgentHit, gdy jego Index == OwnerId) - kredyt za zgon tylko temu samemu czlowiekowi
            public float Dmg, Abs;      // obrazenia po pancerzu, pochloniete przez pancerz
            public DamageTypes Type;
            public BoneBodyPartType Part;
            public bool Missile;
        }

        private static readonly object _q = new object();
        private static List<Hit> _in = new List<Hit>(64), _spare = new List<Hit>(64);
        private static int _lost;   // pod _q

        internal static void Enqueue(Hit h)
        {
            lock (_q)
            {
                if (_in.Count < QueueMax) _in.Add(h);
                else _lost++;
            }
        }

        /// <summary>Glowny watek: zabiera kolejke (wolajacy przetwarza ja i czysci przed nastepnym Take).</summary>
        internal static List<Hit> Take()
        {
            lock (_q)
            {
                var t = _in;
                _in = _spare;
                _spare = t;
                return t;
            }
        }

        /// <summary>Recenzja 313d: kolejka oprozniona pod zamkiem (koniec misji - bez referencji do ludzi miedzy misjami).</summary>
        internal static void ClearQueue()
        {
            lock (_q) { _in.Clear(); _spare.Clear(); }
        }

        // ------------------------------------------------------------ wzory

        /// <summary>Kondycja z Atletyki: 1 - per100 x (Atl - 100) / 100, obciete 0.5..1.25 (Atl 30: x1.18, 100: x1, 200: x0.75, 300+: x0.5).</summary>
        internal static float Cond(float ath, Settings c)
        {
            float per = Clamp(c.BleedAthleticsPer100, 0f, 0.5f);
            return Clamp(1f - per * (ath - 100f) / 100f, 0.5f, 1.25f);
        }

        internal static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }

        /// <summary>Szansa krwawienia rany (0..0.95) i glebokosc W (0..1; 0 = plytsza niz BleedMinWoundShare).</summary>
        internal static float Chance(Hit h, float maxHp, float cond, Settings c, out float w)
        {
            w = 0f;
            if (maxHp <= 0f || h.Dmg <= 0f) return 0f;
            float lo = Clamp(c.BleedMinWoundShare, 0f, 0.5f);
            float hi = Math.Max(lo + 0.01f, Clamp(c.BleedFullWoundShare, 0.2f, 1f));
            float d = h.Dmg / maxHp;
            w = Clamp((d - lo) / (hi - lo), 0f, 1f);
            if (w <= 0f) return 0f;
            float type;
            if (h.Type == DamageTypes.Blunt) type = Clamp(c.BleedBluntFactor, 0f, 1f);
            else if (h.Type == DamageTypes.Pierce || h.Missile) type = Clamp(c.BleedPierceFactor, 0f, 1.5f);
            else type = 1f;
            float part;
            switch (h.Part)
            {
                case BoneBodyPartType.Head:
                case BoneBodyPartType.Neck: part = 1.2f; break;
                case BoneBodyPartType.ShoulderLeft:
                case BoneBodyPartType.ShoulderRight:
                case BoneBodyPartType.ArmLeft:
                case BoneBodyPartType.ArmRight: part = 0.8f; break;
                default: part = 1f; break;   // tulow i nogi (tetnica udowa)
            }
            float armour = 1f;
            if (h.Abs > 0f) armour = 1f - Clamp(c.BleedArmourFactor, 0f, 1f) * h.Abs / (h.Abs + h.Dmg);
            return Clamp(Clamp(c.BleedChanceDeepWound, 0f, 1f) * type * part * w * armour * cond, 0f, 0.95f);
        }

        /// <summary>Strata calkowita rany (HP) - przed podzialem na tempo.</summary>
        internal static float Loss(float dmg, float cond, Settings c)
        {
            return Clamp(c.BleedLossShare, 0f, 1.5f) * dmg * cond;
        }

        internal static float ClotSeconds(Settings c) { return Clamp(c.BleedClotSeconds, 5f, 120f); }

        /// <summary>Mnoznik morale: 1.5 - morale/100 (morale agenta 0..100), obciete 0.5..1.5.</summary>
        internal static float MoraleMul(float morale) { return Clamp(1.5f - morale / 100f, 0.5f, 1.5f); }

        internal static int PartGroup(BoneBodyPartType p)
        {
            switch (p)
            {
                case BoneBodyPartType.Head:
                case BoneBodyPartType.Neck: return 0;
                case BoneBodyPartType.ShoulderLeft:
                case BoneBodyPartType.ShoulderRight:
                case BoneBodyPartType.ArmLeft:
                case BoneBodyPartType.ArmRight: return 2;
                case BoneBodyPartType.Legs: return 3;
                default: return 1;
            }
        }

        internal static int TypeGroup(Hit h)
        {
            if (h.Type == DamageTypes.Blunt) return 2;
            if (h.Type == DamageTypes.Pierce || h.Missile) return 1;
            return 0;
        }

        // ------------------------------------------------------------ kustykanie (cap predkosci, czytany z watkow roboczych)

        private const int Size = 16384;
        private static readonly float[] _cap = new float[Size];
        private static readonly Agent[] _owner = new Agent[Size];
        private static volatile bool _capsAny;

        /// <summary>Cap kustykania czlowieka w m/s (0 = brak). Dowolny watek, tylko odczyt.</summary>
        internal static float LimpCapOf(Agent a)
        {
            if (!_capsAny || a == null) return 0f;
            int i = a.Index;
            if (i < 0 || i >= Size) return 0f;
            if (!ReferenceEquals(Volatile.Read(ref _owner[i]), a)) return 0f;
            float v = Volatile.Read(ref _cap[i]);
            if (v <= 0f || !ReferenceEquals(Volatile.Read(ref _owner[i]), a)) return 0f;
            return v;
        }

        /// <summary>Glowny watek: wpis capu (v &gt; 0) albo zdjecie (v &lt;= 0).</summary>
        internal static void SetLimp(Agent a, float v)
        {
            if (a == null) return;
            int i = a.Index;
            if (i < 0 || i >= Size) return;
            if (v <= 0f)
            {
                if (ReferenceEquals(_owner[i], a)) Volatile.Write(ref _cap[i], 0f);
                return;
            }
            _capsAny = true;
            if (ReferenceEquals(_owner[i], a)) { Volatile.Write(ref _cap[i], v); return; }
            Volatile.Write(ref _cap[i], 0f);             // najpierw zero, potem wlasciciel, potem wartosc (jak AiBreath.SetCap)
            Volatile.Write(ref _owner[i], a);
            Volatile.Write(ref _cap[i], v);
        }

        internal static void ClearLimp()
        {
            _capsAny = false;
            for (int i = 0; i < Size; i++) { Volatile.Write(ref _cap[i], 0f); Volatile.Write(ref _owner[i], null); }
        }

        /// <summary>Predkosc kustykania pieszego w m/s: krok (WalkSpeedCached gry) x WoundedLimpWalkShare.</summary>
        internal static float LimpSpeed(Agent a, Settings c)
        {
            float walk = 0f;
            try { walk = a.WalkSpeedCached; } catch { walk = 0f; }
            if (!(walk > 0.5f) || walk > 4f) walk = LimpFallbackWalk;
            return Math.Max(0.4f, walk * Clamp(c.WoundedLimpWalkShare, 0.3f, 1f));
        }

        /// <summary>Latka AdjustSpeedLimit (watki robocze): pieszy z capem kustykania - limit = min(cap, to, co wpisala gra / RBM). true = wpisane (wolajacy konczy).</summary>
        internal static bool PostSpeedLimp(Agent agent, float desiredSpeed, bool limitIsMultiplier)
        {
            float lc = LimpCapOf(agent);
            if (lc <= 0f || agent.MountAgent != null) return false;
            float lim = lc;
            try
            {
                if (desiredSpeed >= 0f)
                {
                    float cur = desiredSpeed;
                    if (limitIsMultiplier) cur = desiredSpeed * agent.GetMaximumForwardUnlimitedSpeed();
                    if (cur > 0.3f && cur < lim) lim = cur;
                }
                agent.SetMaximumSpeedLimit(lim, false);
                Interlocked.Increment(ref _limpPost);
            }
            catch { Interlocked.Increment(ref _limpPostErr); }
            return true;
        }

        // ------------------------------------------------------------ liczniki misji (glowny watek; _limpPost* - Interlocked)

        internal static int Hits, Shallow, Bleeds, Stopped, BleedingAtEnd, PlayerBleeds;
        internal static readonly int[] ByType = new int[3], ByPart = new int[4];
        internal static double PSum, LSum, AthSum;
        internal static int DeathKilled, DeathUncon, DeathCredit, DeathPanic, DeathWithdraw, DeathOld;
        internal static int DieFailed;   // recenzja 314x: Agent.Die z krwawienia zawiodl (sesja - bez zerowania; pelny wpis pierwszych 3)
        internal static int DeathMissile;   // 313y: zgony z krwawienia po ranie od pocisku, wliczone do DeathByRangedCount druzyny
        internal static double DeathHp0Sum;
        internal static int WdRolls, WdYes, WdNo, WdForced;
        internal static int PainRolls, PainBroke, PainBrokeBleeding, PainResist;
        internal static double PainMoraleSum;
        // recenzja 313d: zbiory po Agent (nie po Index - indeksy wracaja do posilkow), czyszczone w Begin i po linii (EndMission)
        internal static readonly HashSet<Agent> LimpMen = new HashSet<Agent>(), LimpWithdraw = new HashSet<Agent>(), LimpPanic = new HashSet<Agent>();
        internal static readonly HashSet<Agent> LimpHero = new HashSet<Agent>();   // 313y: ranny bohater AI w wycofaniu HeroCaution
        internal const float LimpHystHp = 5f;   // 313y: histereza progu WoundedLimpBelowHp (RBM leczy ok. +1 HP / 10 s)
        internal static int LimpWrites;
        // recenzja 313d (pomiar): predkosc kustykajacych (agent.Velocity) po co najmniej LimpSettle s z capem - czy limit naprawde trzyma; postoj w miejscu po
        // limicie drogi 143 u kustykajacego (RoutKeeper)
        internal const float LimpSettle = 1f;
        internal static int LimpSpeedN, LimpFast, LimpHoldInPlace;
        internal static double LimpSpeedSum;
        internal static float LimpSpeedMax;
        private static int _limpPost, _limpPostErr;
        private static int _stum, _stumSession;
        private static bool _battle;

        internal static void Begin()
        {
            Hits = Shallow = Bleeds = Stopped = BleedingAtEnd = PlayerBleeds = 0;
            Array.Clear(ByType, 0, ByType.Length);
            Array.Clear(ByPart, 0, ByPart.Length);
            PSum = LSum = AthSum = 0.0;
            DeathKilled = DeathUncon = DeathCredit = DeathPanic = DeathWithdraw = DeathOld = 0;
            DeathMissile = 0;
            DeathHp0Sum = 0.0;
            WdRolls = WdYes = WdNo = WdForced = 0;
            PainRolls = PainBroke = PainBrokeBleeding = PainResist = 0;
            PainMoraleSum = 0.0;
            LimpMen.Clear(); LimpWithdraw.Clear(); LimpPanic.Clear(); LimpHero.Clear();
            LimpWrites = 0;
            LimpSpeedN = LimpFast = LimpHoldInPlace = 0;
            LimpSpeedSum = 0.0;
            LimpSpeedMax = 0f;
            Interlocked.Exchange(ref _limpPost, 0);
            Interlocked.Exchange(ref _limpPostErr, 0);
            _stum = 0;
            _battle = false;
            lock (_q) { _in.Clear(); _spare.Clear(); _lost = 0; }
            ClearLimp();
        }

        internal static void NoteBattle() { _battle = true; }

        /// <summary>Recenzja 313d: koniec misji (po linii) - kolejka i zbiory ludzi oproznione, bez referencji do agentow do nastepnej misji.</summary>
        internal static void EndMission()
        {
            ClearQueue();
            LimpMen.Clear(); LimpWithdraw.Clear(); LimpPanic.Clear(); LimpHero.Clear();
        }

        /// <summary>Recenzja 313d: limit drogi odwrotu 143 (s) - kustykajacy idzie ok. 1.1 m/s, wiec 30 s (WalkMax) to ok. 33 m, a cel bywa do 80 m (sztandar).
        /// Przy capie kustykania: dystans do celu przy pierwszym wyslaniu / cap x 1.3, nie mniej niz walkMax, najwyzej LimpWalkMaxCap.</summary>
        internal const float LimpWalkMaxCap = 90f;
        internal static float WalkLimit(Agent a, float dist0, float walkMax)
        {
            float lc = LimpCapOf(a);
            if (lc <= 0f || !(dist0 > 0f)) return walkMax;
            return Clamp(dist0 / lc * 1.3f, walkMax, Math.Max(walkMax, LimpWalkMaxCap));
        }

        /// <summary>Wyjatek na czlowieka: licznik misji i sesji, pelny slad tylko 3 razy w sesji. Funkcja zostaje wlaczona.</summary>
        internal static void Stumble(string where, Exception e)
        {
            try
            {
                _stum++;
                _stumSession++;
                if (_stumSession <= 3) Log.Error("Bleed313." + where, e);
            }
            catch { }
        }

        /// <summary>Linia "Krwawienie (313d)" raz na misje (FieldCraft.OnEndMission). Bez linii, gdy misja poza bitwa i nic sie nie wydarzylo.</summary>
        internal static void Line(Settings c)
        {
            if (c == null || !c.BleedDeathLog) return;
            int lost;
            lock (_q) { lost = _lost; }
            int deaths = DeathKilled + DeathUncon;
            bool any = Hits + Bleeds + deaths + WdRolls + WdForced + PainRolls + LimpMen.Count + _stum + lost > 0;
            if (!_battle && !any) return;
            var sb = new StringBuilder(900);
            sb.Append("Krwawienie (313d): misja ").Append(_battle ? "bitwa" : "poza bitwa")
              .Append(" | klucze: Bleed From Wounds ").Append(c.BleedFromWounds ? "TAK" : "NIE")
              .Append(", Bleed Affects Player ").Append(c.BleedAffectsPlayer ? "TAK" : "NIE")
              .Append(", Wounded Flee By Chance ").Append(c.WoundedFleeByChance ? "TAK" : "NIE")
              .Append(", Pain Break By Chance ").Append(c.PainBreakByChance ? "TAK" : "NIE")
              .Append(", Wounded Limp ").Append(c.WoundedLimp ? "TAK" : "NIE")
              .Append(" | trafien ocenionych ").Append(Hits).Append(" (plytszych niz prog ").Append(Shallow).Append(')')
              .Append(" | krwawien ").Append(Bleeds).Append(" (ciete ").Append(ByType[0]).Append(" / klute i strzaly ").Append(ByType[1]).Append(" / obuch ").Append(ByType[2])
              .Append("; glowa ").Append(ByPart[0]).Append(" / tulow ").Append(ByPart[1]).Append(" / rece ").Append(ByPart[2]).Append(" / nogi ").Append(ByPart[3]).Append(')');
            if (Bleeds > 0)
                sb.Append(", sr. szansa ").Append((PSum * 100.0 / Bleeds).ToString("0", Inv)).Append("%, sr. utrata ").Append((LSum / Bleeds).ToString("0.0", Inv))
                  .Append(" HP, sr. Atletyka ").Append((AthSum / Bleeds).ToString("0", Inv));
            else sb.Append(", sr. szansa -, sr. utrata -, sr. Atletyka -");
            sb.Append(" | ustalo ").Append(Stopped).Append(", na koniec krwawi ").Append(BleedingAtEnd)
              .Append(" | zgony z krwawienia ").Append(deaths).Append(" (zabici ").Append(DeathKilled).Append(" / nieprzytomni ").Append(DeathUncon)
              .Append("; z kredytem ").Append(DeathCredit).Append("; od pocisku ").Append(DeathMissile).Append("; w panice ").Append(DeathPanic).Append(", w odwrocie ").Append(DeathWithdraw)
              .Append("; stara regula ").Append(DeathOld).Append("; sr. HP na starcie krwawienia ")
              .Append(deaths > 0 ? (DeathHp0Sum / deaths).ToString("0.0", Inv) : "-").Append(')')
              .Append(" | odwrot 143: rzutow ").Append(WdRolls).Append(" (wycofal sie ").Append(WdYes).Append(" / zostal ").Append(WdNo)
              .Append("), pewny od ").Append(c.WoundedLimpBelowHp).Append(" HP ").Append(WdForced)
              .Append(" | bol: rzutow ").Append(PainRolls).Append(" (zlamani ").Append(PainBroke).Append(": krwawiacy ").Append(PainBrokeBleeding)
              .Append(" / nie ").Append(PainBroke - PainBrokeBleeding).Append("; oparli sie ").Append(PainResist).Append("), sr. morale ")
              .Append(PainRolls > 0 ? (PainMoraleSum / PainRolls).ToString("0", Inv) : "-")
              .Append(" | kustykanie: ludzi ").Append(LimpMen.Count).Append(" (w odwrocie ").Append(LimpWithdraw.Count).Append(", w panice ").Append(LimpPanic.Count)
              .Append(", ranny bohater ").Append(LimpHero.Count).Append("), zapisow limitu ").Append(LimpWrites).Append(", w latce ").Append(Volatile.Read(ref _limpPost))
              .Append(", pomiarow predkosci ").Append(LimpSpeedN).Append(" (sr. ")
              .Append(LimpSpeedN > 0 ? (LimpSpeedSum / LimpSpeedN).ToString("0.00", Inv) : "-").Append(" m/s, maks ")
              .Append(LimpSpeedN > 0 ? LimpSpeedMax.ToString("0.00", Inv) : "-").Append(", szybciej niz limit ").Append(LimpFast)
              .Append("), postoj po limicie drogi ").Append(LimpHoldInPlace)
              .Append(" | gracz krwawil ").Append(PlayerBleeds)
              .Append(" | kolejka: zgubione ").Append(lost)
              .Append(" | potkniecia ").Append(_stum + Volatile.Read(ref _limpPostErr)).Append('.');
            Log.Info(sb.ToString());
        }
    }
}
