using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 590 MARTWE POLE CZLONKA ARMII (Jeff 16.11: "AI jak jedzie wrogi lord i ma jakies zadanie ... jedzie na slepo i widzi ze moje sily sa wieksze ale i tak
    /// jedzie bez sensu przed siebie ... powinno byc ze widzi moje sily i wycofuje sie i caly czas ma w glowie ze ma dojechac do X"; regula 25.10 "AI walczy o zycie").
    /// Klucz ArmyMemberSeesOwnStrength.
    /// Przyczyna (dekompilacja DefaultMobilePartyAIModel.GetBestInitiativeBehavior 1.4.8, ok. linii 192-209; audyt wf_826d0f68): partia w armii, ale jeszcze nie
    /// doczepiona (jedzie do wodza: Army != null, AttachedTo == null, nie wodz), pomija KAZDEGO wroga, ktorego sila (jego armia albo partia) jest mniejsza od
    /// Army.EstimatedStrength - czyli od wodza i juz doczepionych, nawet gdy ta armia stoi pol mapy dalej. Taki wrog nie istnieje dla niej: ani atak, ani
    /// ucieczka - GoToPoint do wodza prosto na niego. Dowod: Myles Mooton 109347 (137 ludzi, sila 133, EscortParty -> Edmure Tully, armia ok. 7339; gracz 459 -
    /// L16:19246), Addam Marbrand 109168 (wcielony przez 223 do armii Tywina 8944 minute przed spotkaniem - L20:8144). Nasze 223 (LordEscort "do armii") wciela
    /// zagrozonych lordow do armii z 60 jedn. - 38 razy w sesji 16-31-45 - i kazdy z nich do doczepienia ma to martwe pole.
    /// Regula (postfiks GetBestInitiativeBehavior czynnego modelu - PatchTarget.OfActive, NavalDLC wola baze; Priority.Low, czyli PRZED 335 Last - obrona wodza
    /// ma ostatnie slowo): tylko lord AI (nie armia gracza) z Army != null, AttachedTo == null, Army.LeaderParty != party, poza bitwa, osada i morzem, gdy gra
    /// sama juz nie ucieka (wynik ucieczki &gt; 1):
    ///  - przegladamy wrogow w 3R (szukanie jak gra: R x 3, R = EncounterModel.GetEncounterJoiningRadius), ktorych gra POMINELA linia "slabszy od armii"
    ///    (sila wroga - jego armia albo partia - &lt; Army.EstimatedStrength); pozostalych gra ocenila sama - nie liczymy drugi raz;
    ///  - wrog: partia w polu w wojnie z nami (nie banda - gra i tak pomija bandy czlonkom armii, nie w osadzie, bitwie, obozie oblezenia, tratwie, na morzu,
    ///    nie doczepiona - liczy sie przez wodza, nie ShouldBeIgnored); bramka ucieczki modelu ShouldConsiderAvoiding (z postfiksem 386 AvoidPact - pakt
    ///    rodu gracza wygrywa: przed partia objeta paktem nie ucieka, chyba ze ta naprawde wystepuje przeciw niej - decyzja 386);
    ///  - sila wlasna = partia + sojusznicy (ta sama MapFaction, agresja &gt; 0.01, nie gracz, nie doczepieni, poza bitwa / obozem oblezenia / garnizonem /
    ///    milicja) w 2R od wroga z waga b jak gra (num3 / num8) + wodz z doczepionymi (Army.EstimatedStrength) z waga 1 TYLKO gdy wodz blizej niz 2R od partii,
    ///    dalej wodz nie liczy sie wcale (recenzja 590); sojusznik przeciw graczowi tylko z waniliowym warunkiem ShouldConsiderAttacking (bez naszych
    ///    wet - model nie jest wolany, jak w 589);
    ///  - sila wroga = wrog (armia od wodza) + 0.9 x jego sojusznicy (ta sama MapFaction co wrog, agresja &gt; 0.01) w 2R od niego z waga b - jak gra (num6/num7);
    ///  - przewaga lokalna a = wlasna / wroga; ucieczka gdy a &lt; 1 (gra: atak 0 ponizej 1) z wynikiem wedlug wzoru gry (CalculateInitiativeScoresForEnemy):
    ///    0.9434 x num20 x num18 x clamp(1/a, 0.05, 3), num18 = 4 - d / num17 dla d &lt; 4 x num17, inaczej 1; num17 = 1.2R x 0.7 x (1 + AvoidInitiative) / 2
    ///    (0.6 przy celu wlasnego poscigu); num20 = 1 dla lorda, inaczej AvoidInitiative (czlonkowie armii maja z gry 0.33);
    ///  - najlepszy taki wynik &gt; 1 i wiekszy od wyniku gry -&gt; FleeToPoint od tego wroga z wlasnym averageEnemyVec (osiem kierunkow, wzor gry z linii 414-437;
    ///    statycznej listy MobilePartyAi.DangerousPartiesAndTheirVecs NIE ruszamy - wspolna dla watkow AI); rozkaz strategiczny (DefaultBehavior EscortParty -&gt;
    ///    wodz) zostaje - gra wraca do niego, gdy zagrozenie minie ("ma w glowie X").
    /// Bez zmian: bandy, karawany, wodzowie armii, doczepieni, partie poza armia (gra liczy je dobrze), oblegajacy osade wodza (gra: linia 194), sen i obowiazkowy
    /// odpoczynek (NightRest/NightMarch trzymaja AI wylaczone albo na Hold - inicjatywa nie biegnie). 335 (Last) moze potem nadpisac (obronca / chroniony).
    /// 223 (LordEscort.ThinkPostfix "w armii bezpieczny"): decyzja - strategicznie nie ruszane (patrz komentarz tam), ochrona w drodze do wodza jest tutaj.
    /// Watki AI: tylko odczyty jak gra, liczniki Interlocked, przyklady pod lockiem (najwyzej 3 na dobe), bez wizerunkow; bledy liczone (bez globalnego wylacznika).
    /// Linie: "Martwe pole armii (590): start sesji ..." i "Martwe pole armii (590): doba N ...".
    /// </summary>
    internal static class ArmyBlind590
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string Tag = "Martwe pole armii (590): ";
        private const int ExMax = 3;
        private const int MaxFoes = 8;
        private const float RDef = 3f;

        private sealed class Ex
        {
            internal MobileParty Who, Foe, Leader;   // recenzja 590: referencje - nazwy (TextObject) dopiero w linii doby, w watku glownym
            internal float D, Own, FoeStr, ArmyStr, FoeRaw, LeadD, Avoid, Game;
            internal bool J223;
        }

        private static Harmony _h;
        private static string _hPatch = "nie wpieta";
        private static readonly HashSet<System.Reflection.MethodBase> _patched = new HashSet<System.Reflection.MethodBase>();
        private static int _day = -1;
        private static int _iScope, _iSeen, _iWeaker, _iPact, _iFlee, _iFleePlayer, _iFlee223, _iGameFlee, _iLow, _sInit, _sOther;
        private static ConcurrentDictionary<MobileParty, byte> _dayLords = new ConcurrentDictionary<MobileParty, byte>();
        private static ConcurrentDictionary<MobileParty, byte> _joined223 = new ConcurrentDictionary<MobileParty, byte>();
        private static readonly object ExGate = new object();
        private static readonly List<Ex> _ex = new List<Ex>(ExMax);
        private static readonly HashSet<MobileParty> _exLords = new HashSet<MobileParty>();

        internal static void Reset()
        {
            _day = -1;
            ClearDay();
            _joined223 = new ConcurrentDictionary<MobileParty, byte>();
            _sInit = _sOther = 0;
        }

        private static void ClearDay()
        {
            Interlocked.Exchange(ref _iScope, 0); Interlocked.Exchange(ref _iSeen, 0); Interlocked.Exchange(ref _iWeaker, 0); Interlocked.Exchange(ref _iPact, 0);
            Interlocked.Exchange(ref _iFlee, 0); Interlocked.Exchange(ref _iFleePlayer, 0); Interlocked.Exchange(ref _iFlee223, 0); Interlocked.Exchange(ref _iGameFlee, 0);
            Interlocked.Exchange(ref _iLow, 0);
            _dayLords = new ConcurrentDictionary<MobileParty, byte>();
            lock (ExGate) { _ex.Clear(); _exLords.Clear(); }
        }

        private static void Stumble(ref int counter, string where, Exception e)
        {
            int n = Interlocked.Increment(ref counter);
            try { if (n <= 3 || n % 100 == 0) Log.Error("ArmyBlind590." + where + " (potkniecie " + n + ")", e); } catch { }
        }

        /// <summary>LordEscort 223 "do armii" (watek glowny): partia wcielona przez 223 - licznik ucieczek 590 "z tego wcieleni przez 223".</summary>
        internal static void NoteJoined223(MobileParty p)
        {
            try { if (p != null) _joined223[p] = 1; } catch { }
        }

        private static float R
        {
            get { try { var m = Campaign.Current?.Models?.EncounterModel; float r = m != null ? m.GetEncounterJoiningRadius : RDef; return r > 0f ? r : RDef; } catch { return RDef; } }
        }

        /// <summary>Sila jak w grze: wodz albo doczepiony - Army.EstimatedStrength, inaczej partia.</summary>
        private static float Str(MobileParty p)
        {
            var a = p.Army;
            return a != null && (p.AttachedTo != null || a.LeaderParty == p) ? a.EstimatedStrength : p.Party.EstimatedStrength;
        }

        /// <summary>Waga sojusznika wzgledem wroga jak gra (num3 / num8 / b): 0 gdy poza zasiegiem.</summary>
        private static float W(float distToFoe, float num3, float r)
        {
            if (distToFoe > 2f * r) return 0f;
            if (!(distToFoe < r * num3)) return 0f;
            if (distToFoe < r) return 1f;
            return Math.Min(1f, 1f - (distToFoe - r) / (r * (num3 - 1f)));
        }

        /// <summary>Waniliowy ShouldConsiderAttacking(ally, gracz) bez naszych wet (jak 589 - model nie wolany, liczniki wet nie rosna).</summary>
        private static bool VanillaAttacksPlayer(MobileParty ally)
        {
            var mp = MobileParty.MainParty;
            if (mp == null || mp.ShouldBeIgnored || !ally.Ai.DoNotAttackMainPartyUntil.IsPast) return false;
            if (ally.IsCurrentlyAtSea != mp.IsCurrentlyAtSea) return false;
            return MobilePartyHelper.CanPartyAttackWithCurrentMorale(ally);
        }

        /// <summary>Zakres reguly: niedoczepiony czlonek armii AI (nie wodz, nie armia gracza), lord, poza bitwa, osada i morzem.</summary>
        private static bool InScope(MobileParty p, out Army army, out MobileParty lead)
        {
            army = null; lead = null;
            if (p == null || p.IsMainParty || !p.IsLordParty || p.LeaderHero == null || !p.IsActive) return false;
            army = p.Army;
            if (army == null || p.AttachedTo != null) return false;
            lead = army.LeaderParty;
            if (lead == null || lead == p || lead.IsMainParty) return false;
            if (p.MapEvent != null || p.CurrentSettlement != null || p.BesiegerCamp != null || p.IsCurrentlyAtSea || p.IsInRaftState) return false;
            return true;
        }

        /// <summary>Wrog, ktorego gra mogla rozwazac, ale czlonek armii go POMINAL (linia 205: sila wroga &lt; Army.EstimatedStrength). Filtry jak gra.</summary>
        private static bool Candidate(MobileParty p, MobileParty e, IFaction fp, float armyStr, out float raw)
        {
            raw = 0f;
            if (e == p || !e.IsActive || e.ShouldBeIgnored || e.IsGarrison || e.IsMilitia || e.IsBandit) return false;
            if (e.CurrentSettlement != null || e.MapEvent != null || e.BesiegerCamp != null || e.BesiegedSettlement != null) return false;
            if (e.IsCurrentlyAtSea || e.IsInRaftState) return false;
            if (e.Army != null && e.Army.LeaderParty != e && e.AttachedTo != null) return false;   // doczepiony - liczy sie przez wodza
            var fe = e.MapFaction;
            if (fe == null || !FactionManager.IsAtWarAgainstFaction(fe, fp)) return false;
            if (e.IsFleeing() && e.ShortTermTargetParty == p.Army.LeaderParty) return false;   // gra: ucieka do naszego wodza - pominiety slusznie
            raw = e.Army != null ? e.Army.EstimatedStrength : e.Party.EstimatedStrength;      // dokladnie miara gry z linii 205
            return raw < armyStr;   // nie pominiety - gra ocenila go sama
        }

        // ------------------------------------------------------------ watki AI
        /// <summary>Postfiks Priority.Low GetBestInitiativeBehavior czynnego modelu (watki AI, co AiCheckInterval). Przed 335 (Last). Nie rzuca.</summary>
        public static void InitPostfix(MobileParty __0, ref AiBehavior __1, ref MobileParty __2, ref float __3, ref Vec2 __4)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.ArmyMemberSeesOwnStrength) return;
                Army army; MobileParty lead;
                if (!InScope(__0, out army, out lead)) return;
                Interlocked.Increment(ref _iScope);
                if (__3 > 1f && MobileParty.IsFleeBehavior(__1)) { Interlocked.Increment(ref _iGameFlee); return; }   // gra juz ucieka - jej decyzja
                var p = __0;
                var fp = p.MapFaction;
                if (fp == null) return;
                float r = R;
                float armyStr = army.EstimatedStrength;
                Vec2 pp = p.GetPosition2D;
                Vec2 lp = lead.GetPosition2D;
                float leadD = pp.Distance(lp);
                bool leadNear = leadD < 2f * r && (lead.MapEvent == null) && !lead.IsCurrentlyAtSea;
                var model = Campaign.Current.Models.MobilePartyAIModel;
                float avoidInit = p.Ai.AvoidInitiative;
                float r12 = r * 1.2f;

                MobileParty best = null; float bestAvoid = 0f, bestD = 0f, bestOwn = 0f, bestFoe = 0f, bestRaw = 0f;
                float[] fs = null; Vec2[] fv = null; int fn = 0;

                var data = MobileParty.StartFindingLocatablesAroundPosition(pp, r * 3f);
                for (var e = MobileParty.FindNextLocatable(ref data); e != null; e = MobileParty.FindNextLocatable(ref data))
                {
                    float raw;
                    if (!Candidate(p, e, fp, armyStr, out raw)) continue;
                    Vec2 ep = e.GetPosition2D;
                    float d = pp.Distance(ep);
                    if (d >= r * 3f) continue;   // gra: num2 >= R x 2 x 3 odrzuca (promien szukania 3R)
                    // recenzja 590: gra (linia ok. 170) pomija wroga, do ktorego nie ma drogi (rzeka, morze) - tak samo, ta sama funkcja i limit
                    float dn, lr;
                    if (!DistanceHelper.FindClosestDistanceFromMobilePartyToMobileParty(p, e, p.NavigationCapability, r * 2f * 10f, out dn, out lr)) continue;
                    Interlocked.Increment(ref _iSeen);
                    if (!model.ShouldConsiderAvoiding(p, e)) { Interlocked.Increment(ref _iPact); continue; }   // 386 AvoidPact (pakt rodu gracza) i bramki gry

                    // sily obu stron jak gra (num4 / num6 + num7), wokol wroga
                    float num3 = 1f + Math.Max(0f, (d - 1f) / ((r - 1f) * 2f));
                    if (num3 > 2f) num3 = 2f;
                    float own = p.Party.EstimatedStrength + 0.01f;
                    float foe = 0.01f + Str(e);
                    float foeAllies = 0f;
                    var fe = e.MapFaction;
                    bool vsPlayer = e.IsMainParty;
                    // wodz i doczepieni: TYLKO gdy wodz blizej niz 2R od partii (waga 1); dalej - nie liczy sie wcale (recenzja 590: wodz stojacy
                    // za wrogiem na drodze czeka na zbiorke i nie idzie na pomoc - to jest wlasnie przypadek Mylesa i Marbranda)
                    if (leadNear && (!vsPlayer || VanillaAttacksPlayer(lead))) own += armyStr;
                    var d2 = MobileParty.StartFindingLocatablesAroundPosition(pp, r * 3f);
                    for (var a = MobileParty.FindNextLocatable(ref d2); a != null; a = MobileParty.FindNextLocatable(ref d2))
                    {
                        if (a == p || a == e || a == lead || !a.IsActive || a.AttachedTo != null) continue;
                        if (a.MapEvent != null || a.IsCurrentlyAtSea || a.IsInRaftState) continue;
                        if (a.IsGarrison || a.IsMilitia || a.BesiegerCamp != null || a.BesiegedSettlement != null) continue;
                        if (a.CurrentSettlement != null && a.CurrentSettlement.SiegeEvent != null) continue;
                        if (!(a.Aggressiveness > 0.01f)) continue;
                        var fa = a.MapFaction;
                        if (fa == null) continue;
                        bool mine = fa == fp && !a.IsMainParty;
                        bool theirs = fa == fe;
                        if (!mine && !theirs) continue;
                        float w = W(a.GetPosition2D.Distance(ep), num3, r);
                        if (w <= 0f) continue;
                        float sa = Str(a) * w;
                        if (theirs) foeAllies += sa;
                        else if (!vsPlayer || VanillaAttacksPlayer(a)) own += sa;
                    }
                    foe += foeAllies * 0.9f;
                    float adv = own / foe;
                    if (!(adv < 1f)) { Interlocked.Increment(ref _iWeaker); continue; }   // nie slabszy - gra i tak nie ucieka (atak liczy gra wobec tego wroga - nie)
                    // wynik ucieczki - wzor CalculateInitiativeScoresForEnemy
                    float num16 = 0.7f;
                    if ((p.DefaultBehavior == AiBehavior.EngageParty || p.DefaultBehavior == AiBehavior.GoAroundParty) && p.TargetParty == e) num16 = 0.6f;
                    float num17 = r12 * num16 * (1f + avoidInit) / 2f;
                    float num18 = d < num17 * 4f ? 4f - d / (num17 + 1E-05f) : 1f;
                    float num20 = e.IsLordParty && e.LeaderHero != null && e.LeaderHero.IsLord ? 1f : avoidInit;
                    float num4 = MBMath.ClampFloat(MBMath.ClampFloat(1f / Math.Max(adv, 0.0001f), 0.05f, 3f), 0.05f, 3f);
                    float avoid = 0.9433963f * num20 * num18 * num4;
                    if (avoid > 1f)
                    {
                        if (fs == null) { fs = new float[MaxFoes]; fv = new Vec2[MaxFoes]; }
                        if (fn < MaxFoes) { fs[fn] = avoid; fv[fn] = (ep - pp).Normalized(); fn++; }
                    }
                    else Interlocked.Increment(ref _iLow);
                    if (avoid > bestAvoid) { bestAvoid = avoid; best = e; bestD = d; bestOwn = own; bestFoe = foe; bestRaw = raw; }
                }
                if (best == null || !(bestAvoid > 1f) || !(bestAvoid > __3) || fn == 0) return;

                // averageEnemyVec - osiem kierunkow, wzor gry
                Vec2 vec = Vec2.Zero; float top = 0f;
                for (int i = 0; i < 8; i++)
                {
                    var v = new Vec2(TaleWorlds.Library.MathF.Sin(i / 8f * (float)Math.PI * 2f), TaleWorlds.Library.MathF.Cos(i / 8f * (float)Math.PI * 2f));
                    float sum = 0f;
                    for (int j = 0; j < fn; j++)
                    {
                        float q = fv[j].DistanceSquared(v);
                        if (q > 1f) q = 1f + (q - 1f) * 0.5f;
                        sum += q * fs[j];
                    }
                    if (sum > top) { vec = -v; top = sum; }
                }
                float game = __3;
                __1 = AiBehavior.FleeToPoint;
                __2 = best;
                __3 = bestAvoid;
                __4 = vec;
                Interlocked.Increment(ref _iFlee);
                if (best.IsMainParty) Interlocked.Increment(ref _iFleePlayer);
                bool j223 = _joined223.ContainsKey(p);
                if (j223) Interlocked.Increment(ref _iFlee223);
                if (_dayLords.Count < 5000) _dayLords[p] = 1;
                Note(p, best, lead, bestD, bestOwn, bestFoe, armyStr, bestRaw, leadD, bestAvoid, game, j223);
            }
            catch (Exception e) { Stumble(ref _sInit, "InitPostfix", e); }
        }

        private static void Note(MobileParty p, MobileParty e, MobileParty lead, float d, float own, float foe, float armyStr, float raw, float leadD, float avoid, float game, bool j223)
        {
            lock (ExGate)
            {
                if (_ex.Count >= ExMax || !_exLords.Add(p)) return;
                _ex.Add(new Ex { Who = p, Foe = e, Leader = lead, D = d, Own = own, FoeStr = foe, ArmyStr = armyStr, FoeRaw = raw,
                                 LeadD = leadD, Avoid = avoid, Game = game, J223 = j223 });
            }
        }

        private static string Nm(MobileParty p)
        {
            if (p == null) return "?";
            try
            {
                var h = p.LeaderHero;
                string n = h != null ? h.Name.ToString() : p.Name.ToString();
                var f = p.MapFaction;
                return f != null ? n + " [" + f.Name + "]" : n;
            }
            catch { return "?"; }
        }

        private static string F0(float v) { return v.ToString("F0", Inv); }
        private static string F1(float v) { return v.ToString("F1", Inv); }
        private static string F2(float v) { return v.ToString("F2", Inv); }

        // ------------------------------------------------------------ watek glowny
        /// <summary>Co godzine (watek glowny): sprzatanie listy wcielonych przez 223, linia doby przy zmianie dnia.</summary>
        internal static void Hourly()
        {
            try
            {
                var j = _joined223;
                if (j.Count > 0)
                {
                    List<MobileParty> drop = null;
                    foreach (var kv in j)
                    {
                        var p = kv.Key;
                        if (p == null || !p.IsActive || p.Army == null || p.AttachedTo != null) { if (drop == null) drop = new List<MobileParty>(); drop.Add(p); }
                    }
                    if (drop != null) { byte b; foreach (var p in drop) j.TryRemove(p, out b); }
                }
            }
            catch (Exception e) { Stumble(ref _sOther, "Hourly.Joined", e); }
            var s = Settings.Current;
            if (s == null) return;
            int day = (int)CampaignTime.Now.ToDays;
            if (_day < 0) _day = day;
            else if (day != _day) { DayLine(s); ClearDay(); _day = day; }
        }

        private static void DayLine(Settings s)
        {
            try
            {
                int flee = Volatile.Read(ref _iFlee);
                int st = _sInit + _sOther;
                if (!s.ArmyMemberSeesOwnStrength && flee + st == 0) return;
                var sb = new StringBuilder(768);
                sb.Append(Tag).Append("doba ").Append(_day).Append(" | klucz ").Append(s.ArmyMemberSeesOwnStrength ? "TAK" : "NIE")
                  .Append(" | ocen niedoczepionych czlonkow armii ").Append(Volatile.Read(ref _iScope))
                  .Append(" (gra juz uciekala ").Append(Volatile.Read(ref _iGameFlee)).Append(")")
                  .Append(" | wrogow w 3R pominietych przez gre (slabszy od calej armii) ").Append(Volatile.Read(ref _iSeen))
                  .Append(": bez ucieczki wg modelu/paktu 386 ").Append(Volatile.Read(ref _iPact))
                  .Append(", czlonek z sojusznikami nie slabszy ").Append(Volatile.Read(ref _iWeaker))
                  .Append(", slabszy, ale wynik ucieczki <= 1 (daleko) ").Append(Volatile.Read(ref _iLow))
                  .Append(" | 590 ZMIENIL INICJATYWE (ucieczka) ").Append(flee).Append(" razy, lordow ").Append(_dayLords.Count)
                  .Append(", przed graczem ").Append(Volatile.Read(ref _iFleePlayer))
                  .Append(", z tego wcieleni do armii przez 223 ").Append(Volatile.Read(ref _iFlee223))
                  .Append(" | potkniecia ").Append(st).Append(".");
                lock (ExGate)
                {
                    for (int i = 0; i < _ex.Count; i++)
                    {
                        var x = _ex[i];
                        sb.Append(" | przyklad: ").Append(Nm(x.Who)).Append(" (do wodza ").Append(Nm(x.Leader)).Append(" ").Append(F1(x.LeadD)).Append(" jedn.")
                          .Append(x.J223 ? ", wcielony przez 223" : "").Append(") ucieka przed ").Append(x.Foe != null && x.Foe.IsMainParty ? "GRACZ" : Nm(x.Foe)).Append(" z ").Append(F1(x.D))
                          .Append(" jedn. - sily: swoi w poblizu ").Append(F0(x.Own)).Append(" / wrog z sojusznikami ").Append(F0(x.FoeStr))
                          .Append(" (gra porownywala wroga ").Append(F0(x.FoeRaw)).Append(" z cala armia ").Append(F0(x.ArmyStr)).Append(")")
                          .Append(", wynik ucieczki ").Append(F2(x.Avoid)).Append(" (gra ").Append(F2(x.Game)).Append(")");
                    }
                }
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble(ref _sOther, "DayLine", e); }
        }

        /// <summary>OnSessionLaunched: postfiks na typie czynnego modelu kazdej kampanii (raz na typ w procesie), linia startu. Nie rzuca.</summary>
        internal static void SessionStart()
        {
            try
            {
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.MobilePartyAIModel : null;
                var m = PatchTarget.OfActive(model, "GetBestInitiativeBehavior",
                    new[] { typeof(MobileParty), typeof(AiBehavior).MakeByRefType(), typeof(MobileParty).MakeByRefType(), typeof(float).MakeByRefType(), typeof(Vec2).MakeByRefType() },
                    "martwe pole czlonka armii (590)");
                if (m == null) _hPatch = "BRAK (GetBestInitiativeBehavior)";
                else if (_patched.Contains(m)) _hPatch = "TAK (juz wczesniej w tym procesie, " + (m.DeclaringType != null ? m.DeclaringType.Name : "?") + ")";
                else
                {
                    if (_h == null) _h = new Harmony("com.jeff.armoury.armyblind590");
                    _h.Patch(m, postfix: new HarmonyMethod(typeof(ArmyBlind590), nameof(InitPostfix)) { priority = Priority.Low });
                    _patched.Add(m);
                    _hPatch = "TAK (Low, przed 335 Last; " + (m.DeclaringType != null ? m.DeclaringType.Name : "?") + ")";
                }
            }
            catch (Exception e) { _hPatch = "wyjatek " + e.GetType().Name; Stumble(ref _sOther, "SessionStart.Patch", e); }
            try
            {
                var s = Settings.Current;
                float r = R;
                Log.Info(Tag + "start sesji | klucz Army Member Sees Own Strength " + (s != null && s.ArmyMemberSeesOwnStrength ? "TAK" : "NIE") + " | latka " + _hPatch
                         + " | niedoczepiony czlonek armii AI ocenia wroga w " + F1(3f * r) + " jedn. (3R) swoja sila z sojusznikami w " + F1(2f * r)
                         + " jedn. od wroga, wodz z armia tylko w " + F1(2f * r) + " jedn. od niego; ucieczka wzorem gry, rozkaz do wodza zostaje.");
            }
            catch (Exception e) { Stumble(ref _sOther, "SessionStart", e); }
        }
    }
}
