using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 335 OCHRONA WODZA - MAPA (Jeff 06.11: "zblizam sie do Tywina, to inni, jesli sa wokol niego, nie uciekaja, to glowa rodu, tylko beda go bronic za wszelka cene,
    /// szczegolnie ze maja przewage liczebna"). Klucz LordsDefendThreatenedLord.
    /// Przyczyna (log Jeffa Armoury-2026-11-06_11-51-21, Tywin lord_4_1 12:39; dekompilacja DefaultMobilePartyAIModel.GetBestInitiativeBehavior 1.4.8): gra nie zna
    /// "obrony sojuszniczego lorda" - kazda partia co AiCheckInterval ocenia wroga sama (przewaga lokalna: wlasna sila + sojusznicy w 2R od wroga z waga b), a
    /// wolniejsza od wroga dostaje wynik ataku 0 (num5 = 0), chyba ze wrog jest w 2.5 jedn.; sojusznik liczy sie do przewagi przy celu = gracz tylko, gdy
    /// ShouldConsiderAttacking(sojusznik, gracz) - a 223 / p9 daja "nie zaczepiaj" slabym R1-R3 (Tywin, Melwyn, Lancel) - grupa nigdy nie widziala swojej przewagi;
    /// ucieczka do sojusznika (GetNearbyPartyToFlee) tylko do jednego silniejszego w 11.25 jedn. Tywin uciekal sam w pole.
    ///
    /// Regula (migawka w watku glownym co AiCheckInterval = 0.25 h, CampaignEvents.TickEvent; watki AI czytaja opublikowany slownik):
    ///  - CHRONIONY P: partia lorda AI (nie gracz i jego rod, krolestwo, nie Straz / umarli / partie prologu), poza bitwa, osada, oblezeniem i morzem, sama albo wodz armii;
    ///  - NAPASTNIK X: wrogi (wojna) gracz albo partia lorda / wodz armii, poza bitwa, osada i oblezeniem, w 6R od P (gra: dalej niz R x 2 x 3 wroga nie liczy), ktory
    ///    go goni (krotki albo dlugi cel = P; gracz zawsze - jego zamiaru gra nie zna) albo przed ktorym P ucieka; sila X (z armia) wieksza od sily P - inaczej P sam
    ///    da rade i decyduje gra; najsilniejszy taki X;
    ///  - OBRONCY: partie lordow tego samego krolestwa (nie gracz i jego rod, poza bitwa, osada, oblezeniem, morzem; sama albo wodz armii z cala armia) w
    ///    FleeToNearbySettlementRadius od P dla wladcy / nastepcy / glowy rodu / znanego lorda (223 R1-R3) i wodza armii, FleeToNearbyPartyRadius dla reszty (promienie
    ///    modelu AI gry - "glowa rodu bardziej"), ktore dojda do P w DrMaxMarchHours godzin marszu (ten sam limit, z ktorym m97 przyprowadza posilki do bitwy:
    ///    trasa 1.1 x linia prosta, predkosc partii / 1.2 jedn./h);
    ///  - SILA: grupa = P + obroncy (EstimatedStrength z p9, armia od wodza); napastnik = X + jego sojusznicy (partie lordow strony X) w 2R od X - jak gra liczy
    ///    przewage lokalna; obrona, gdy grupa >= napastnik: gra atakuje przy przewadze > 1 (CalculateInitiativeScoresForEnemy: 0.5 x (1 + a) > 1 / a), wiec margines 1;
    ///    grupa slabsza - bez zmian (ucieczka jak gra; 223 prowadzi slabego do twierdzy);
    ///  - CO ROBIA (postfiks GetBestInitiativeBehavior czynnego modelu, PatchTarget.OfActive - NavalDLC wola model bazowy): obronca - EngageParty na X (wynik >= 1.01,
    ///    ponad prog GetBehaviors), chyba ze ucieka przed KIMS INNYM (inne zagrozenie - decyzja gry); P - EscortParty do najblizszego obroncy, a w R od niego
    ///    EngageParty na X razem z nimi; obronca przypisany do jednego P (najwyzsza ranga, potem najblizszy); zakaz zaczepiania 223 (LordEscort.AttackPostfix) i
    ///    p9 (ArmsFirst.AttackPostfix) nie obowiazuje wobec tego X - to samo przywraca liczenie tych partii jako sojusznikow w przewadze lokalnej gry (flag7).
    /// Recenzja 335 (335b):
    ///  - sila napastnika liczona z sojusznikami w 2R ZANIM odrzucimy pare ("P sam da rade" = P >= X z sojusznikami);
    ///  - przydzial obroncow: najpierw wszystkie pary, potem obroncy do par (ranga, odleglosc; chroniony nigdy nie jest obronca), sila grupy tylko z przypisanych;
    ///    para lacznie slabsza odpada (po jednej, najslabsza pierwsza) i jej obroncy wracaja do puli pozostalych par;
    ///  - BITWA CHRONIONEGO: P w bitwie polowej (bez osady, nie na morzu) z napastnikiem z poprzedniej migawki albo P po stronie broniacej, a napastnik = wodz strony
    ///    atakujacej - para trwa (sila stron bitwy), obroncy EngageParty na X = dolaczenie do bitwy po stronie P ("bronia go za wszelka cene" od chwili starcia);
    ///  - P jedzie tylko do obroncy, do ktorego droga nie prowadzi przez X (kat P-obronca / P-X > 60 st.) - bez takiego decyzja gry (ucieczka), obroncy i tak ida;
    ///  - obronca dalej niz 2R od P i dalej od X niz P najpierw EscortParty do P (zbieraja sie przy nim, nie wpadaja na X po jednym), blizej - EngageParty na X;
    ///  - latka na typ czynnego modelu kazdej kampanii (inny model w nowej kampanii - nowa latka).
    /// Linie: "Ochrona wodza (335): mapa - ..." (przyklady, najwyzej 10 na dobe) i "Ochrona wodza (335): mapa dzien N ..." (doba).
    /// </summary>
    internal static partial class LordEscort
    {
        private sealed class G335
        {
            internal int Kind;           // 1 chroniony, 2 obronca
            internal int Rank;
            internal MobileParty X, P, Near;
            internal bool Battle;        // chroniony w bitwie polowej z napastnikiem - obroncy dolaczaja
        }

        private sealed class Pair335
        {
            internal MobileParty P, X;
            internal int Rank, RKind, Rk;
            internal bool Big, Army, Battle, Dropped;
            internal float Sp, Foe, Group;
            internal string Key, Hid;
            internal readonly List<KeyValuePair<MobileParty, float>> Cands = new List<KeyValuePair<MobileParty, float>>();
            internal readonly List<MobileParty> Defs = new List<MobileParty>();
        }

        private struct Edge335 { internal int I; internal MobileParty D; internal float Dist; internal int Rk; }

        private static volatile Dictionary<MobileParty, G335> _g335 = new Dictionary<MobileParty, G335>();
        private static volatile bool _g335On;
        private static double _g335Last = -1;
        private static string _g335Patch = "nie wpieta";
        private static System.Reflection.MethodInfo _g335M;
        private static readonly HashSet<System.Reflection.MethodBase> _g335Patched = new HashSet<System.Reflection.MethodBase>();

        // watki AI (Interlocked)
        private static int _gvDef, _gvDefAlready, _gvProtEscort, _gvProtEngage, _gvOther, _gvAtk, _gvDefEscort, _gvProtNoSafe;
        // doba (watek glowny)
        private static readonly HashSet<string> _gdPairs = new HashSet<string>(StringComparer.Ordinal), _gdWin = new HashSet<string>(StringComparer.Ordinal),
                                                _gdBattle = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<MBGUID> _gdBig = new HashSet<MBGUID>(), _gdArmyP = new HashSet<MBGUID>(), _gdDefenders = new HashSet<MBGUID>(),
                                                _gdToBattle = new HashSet<MBGUID>(), _gdJoined = new HashSet<MBGUID>();
        private static readonly HashSet<string> _gdProt = new HashSet<string>(StringComparer.Ordinal);   // bohaterowie chronieni grupa (id - pojmanie)
        private static int _gdPlayer, _gdLose, _gdLoseBig, _gdNoDef, _gdSnaps, _gdEx, _gdStum, _gdCapt, _gdCaptProt, _gdNoSafe;
        private static float _gdRatioSum;
        private static int _gdRatioN;

        private static void G335Stumble(string where, Exception e)
        {
            _gdStum++;
            Stumble(where, e);
        }

        /// <summary>334e (obrona pustych ziem): partia jest teraz chroniona albo obronca w migawce 335 - nie dostaje przydzialu obrony wsi.</summary>
        internal static bool Guard335Busy(MobileParty mp)
        {
            var d = _g335;
            return mp != null && _g335On && d != null && d.ContainsKey(mp);
        }

        internal static void Guard335Reset()
        {
            _g335 = new Dictionary<MobileParty, G335>();
            _g335On = false;
            _g335Last = -1;
            Guard335ClearDay();
        }

        private static void Guard335ClearDay()
        {
            Interlocked.Exchange(ref _gvDef, 0); Interlocked.Exchange(ref _gvDefAlready, 0); Interlocked.Exchange(ref _gvProtEscort, 0);
            Interlocked.Exchange(ref _gvProtEngage, 0); Interlocked.Exchange(ref _gvOther, 0); Interlocked.Exchange(ref _gvAtk, 0);
            Interlocked.Exchange(ref _gvDefEscort, 0); Interlocked.Exchange(ref _gvProtNoSafe, 0);
            _gdPairs.Clear(); _gdWin.Clear(); _gdBattle.Clear(); _gdBig.Clear(); _gdArmyP.Clear(); _gdDefenders.Clear(); _gdProt.Clear(); _gdToBattle.Clear(); _gdJoined.Clear();
            _gdPlayer = _gdLose = _gdLoseBig = _gdNoDef = _gdSnaps = _gdEx = _gdStum = _gdCapt = _gdCaptProt = _gdNoSafe = 0;
            _gdRatioSum = 0f; _gdRatioN = 0;
        }

        // ------------------------------------------------------------ migawka (watek glowny)

        /// <summary>CampaignEvents.TickEvent: co AiCheckInterval godzin gry nowa migawka par chroniony - napastnik i obroncow.</summary>
        internal static void Guard335Tick()
        {
            var s = Settings.Current;
            bool on = s != null && s.LordsDefendThreatenedLord && Campaign.Current != null && _g335M != null;
            if (!on)
            {
                if (_g335On || _g335.Count > 0) { _g335On = false; _g335 = new Dictionary<MobileParty, G335>(); }
                return;
            }
            double h = CampaignTime.Now.ToHours;
            float step = 0.25f;
            try { step = Math.Max(0.05f, Campaign.Current.Models.MobilePartyAIModel.AiCheckInterval); } catch { }
            if (_g335Last >= 0 && h >= _g335Last && h - _g335Last < step) return;
            _g335Last = h;
            try { Build335(s); _g335On = true; }
            catch (Exception e) { G335Stumble("Guard335.Build", e); }
        }

        /// <summary>Bitwa polowa na mapie bez osady (nie oblezenie, nie morze, nie najazd).</summary>
        private static bool FieldEvent(TaleWorlds.CampaignSystem.MapEvents.MapEvent me)
        {
            try { return me != null && me.IsFieldBattle && me.MapEventSettlement == null && !me.IsNavalMapEvent; }
            catch { return false; }
        }

        /// <summary>Suma EstimatedStrength partii strony bitwy (armia - kazda partia osobno, bez podwojnego liczenia).</summary>
        private static float StrSide(TaleWorlds.CampaignSystem.MapEvents.MapEventSide side)
        {
            float s = 0f;
            if (side == null) return 0f;
            var ps = side.Parties;
            for (int i = 0; ps != null && i < ps.Count; i++)
            {
                try { var pb = ps[i].Party; if (pb != null) s += pb.EstimatedStrength; } catch { }
            }
            return s;
        }

        private static bool Protectable(MobileParty p, bool battle = false)
        {
            if (p == null || !p.IsActive || !p.IsLordParty || p.IsMainParty || p.IsDisbanding) return false;
            if (p.CurrentSettlement != null || p.BesiegedSettlement != null || p.IsCurrentlyAtSea) return false;
            if (p.MapEvent != null && !(battle && FieldEvent(p.MapEvent))) return false;
            if (p.AttachedTo != null || (p.Army != null && p.Army.LeaderParty != p)) return false;
            var h = p.LeaderHero;
            if (h == null || !h.IsAlive) return false;
            var clan = p.ActualClan;
            if (clan == null || clan == Clan.PlayerClan || clan.IsBanditFaction) return false;
            var k = p.MapFaction as Kingdom;
            if (k == null || k.IsEliminated) return false;
            if (CrownGifts.IsWatchParty(p) || Undead.Party(p) || Prologue.Scripted(p)) return false;
            return true;
        }

        private static bool Attacker(MobileParty t, MobileParty p)
        {
            if (t == null || t == p || !t.IsActive || t.IsCurrentlyAtSea != p.IsCurrentlyAtSea) return false;
            if (t.CurrentSettlement != null || t.MapEvent != null || t.BesiegedSettlement != null) return false;
            if (!(t.IsLordParty || t.IsMainParty)) return false;
            if (t.AttachedTo != null || (t.Army != null && t.Army.LeaderParty != t)) return false;
            if (PrisonerTerms384.PactBlocks(p, t)) return false;   // 386: pakt o nieagresji (384) - obroncy nie wymuszaja ataku na druzyne gracza / partie rodu
            return Hostile(p.MapFaction, t) && !t.IsBandit;
        }

        private static bool Chasing(MobileParty t, MobileParty p)
        {
            if (t.ShortTermTargetParty == p || t.TargetParty == p) return true;
            if (t.IsMainParty)
            {
                // gracz bez klikniecia na P: jedzie w jego strone (cel ruchu w 45 st. od kierunku na P) albo jest juz w 2R
                Vec2 me = t.GetPosition2D, to = p.GetPosition2D - me;
                float R = 3f;
                try { R = Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius; } catch { }
                if (to.LengthSquared <= 4f * R * R) return true;
                Vec2 go = Vec2.Invalid;
                try { go = t.TargetPosition.ToVec2() - me; } catch { go = Vec2.Invalid; }
                if (!t.IsMoving || !go.IsValid || go.LengthSquared < 0.01f || to.LengthSquared < 0.0001f) return false;
                return Vec2.DotProduct(go.Normalized(), to.Normalized()) >= 0.7071f;
            }
            return p.ShortTermTargetParty == t && p.IsFleeing();
        }

        private static float Hours(MobileParty d, float dist)
        {
            float sp = 0f;
            try { sp = d.Speed; } catch { sp = 0f; }
            float uph = Math.Max(1f, sp) / 1.2f;                                   // m97 DrMarchArrival: predkosc / 1.2 = jedn. na godzine, najmniej MinimumSpeed 1
            return dist * 1.1f / uph;                                              // trasa 1.1 x linia prosta (m97 RouteFactor)
        }

        private static void Build335(Settings s)
        {
            var models = Campaign.Current.Models;
            float R = models.EncounterModel.GetEncounterJoiningRadius;
            var aim = models.MobilePartyAIModel;
            float threatR = R * 6f, bigR = aim.FleeToNearbySettlementRadius, lowR = aim.FleeToNearbyPartyRadius;
            float maxH = MBMath.ClampFloat(s.DrMaxMarchHours, 0.25f, 24f);
            var old = _g335;
            var lords = MobileParty.AllLordParties;
            _gdSnaps++;
            // obroncy z poprzedniej migawki, ktorzy dolaczyli do bitwy chronionego
            foreach (var kv in old)
            {
                try
                {
                    var g = kv.Value;
                    if (g.Kind != 2 || g.P == null || kv.Key == null) continue;
                    var me = g.P.MapEvent;
                    if (me != null && kv.Key.MapEvent == me) _gdJoined.Add(kv.Key.Id);
                }
                catch { }
            }
            // 1) pary chroniony - napastnik, kandydaci na obroncow
            var pairs = new List<Pair335>();
            for (int i = 0; i < lords.Count; i++)
            {
                var p = lords[i];
                try
                {
                    if (!Protectable(p, true)) continue;
                    Vec2 pp = p.GetPosition2D;
                    MobileParty x = null;
                    float sp, foe;
                    bool battle = p.MapEvent != null;
                    if (battle)
                    {
                        // bitwa chronionego: napastnik z poprzedniej migawki w tej samej bitwie albo wodz strony atakujacej (P broni sie)
                        var me = p.MapEvent;
                        G335 prev;
                        if (old.TryGetValue(p, out prev) && prev.Kind == 1 && prev.X != null && prev.X.MapEvent == me) x = prev.X;
                        if (x == null && p.MapEventSide == me.DefenderSide)
                        {
                            var lp = me.AttackerSide != null ? me.AttackerSide.LeaderParty : null;
                            var lm = lp != null ? lp.MobileParty : null;
                            if (lm != null && lm.IsActive && (lm.IsLordParty || lm.IsMainParty) && !lm.IsBandit && Hostile(p.MapFaction, lm)) x = lm;
                        }
                        if (x == null || x.MapEventSide == null || p.MapEventSide == null || x.MapEventSide == p.MapEventSide) continue;
                        sp = StrSide(p.MapEventSide);
                        foe = StrSide(x.MapEventSide);
                    }
                    else
                    {
                        float sx = 0f;
                        var data = MobileParty.StartFindingLocatablesAroundPosition(pp, threatR);
                        for (var t = MobileParty.FindNextLocatable(ref data); t != null; t = MobileParty.FindNextLocatable(ref data))
                        {
                            if (!Attacker(t, p) || pp.Distance(t.GetPosition2D) > threatR || !Chasing(t, p)) continue;
                            float st = Str(t);
                            if (st <= sx) continue;
                            sx = st; x = t;
                        }
                        if (x == null) continue;
                        // napastnik z sojusznikami w 2R (gra: num6 + num7 x 0.9 - liczymy w pelni, bez wagi odleglosci)
                        foe = sx;
                        Vec2 xp = x.GetPosition2D;
                        data = MobileParty.StartFindingLocatablesAroundPosition(xp, 2f * R);
                        for (var t = MobileParty.FindNextLocatable(ref data); t != null; t = MobileParty.FindNextLocatable(ref data))
                        {
                            if (t == x || (x.Army != null && t.Army == x.Army) || !Attacker(t, p) || xp.Distance(t.GetPosition2D) > 2f * R) continue;
                            foe += Str(t);
                        }
                        sp = Str(p);                                               // sila P dopiero przy napastniku (EstimatedStrength nie dla kazdego lorda)
                    }
                    if (foe <= sp) continue;                                       // sam da rade (z sojusznikami napastnika) - decyduje gra
                    int kind;
                    int rank = RankOf(p.LeaderHero, p.ActualClan, out kind);
                    bool army = p.Army != null && p.Army.LeaderParty == p;
                    bool big = rank <= 3 || army;
                    var pr = new Pair335 { P = p, X = x, Rank = rank, RKind = kind, Rk = big ? Math.Min(rank, 3) : 4, Big = big, Army = army, Battle = battle, Sp = sp, Foe = foe };
                    pr.Hid = p.LeaderHero.StringId ?? "?";
                    pr.Key = pr.Hid + "|" + (x.StringId ?? "?");
                    _gdPairs.Add(pr.Key);
                    if (big) _gdBig.Add(p.Id);
                    if (army) _gdArmyP.Add(p.Id);
                    if (x.IsMainParty) _gdPlayer++;
                    float defR = big ? bigR : lowR;
                    var dd = MobileParty.StartFindingLocatablesAroundPosition(pp, defR);
                    for (var d = MobileParty.FindNextLocatable(ref dd); d != null; d = MobileParty.FindNextLocatable(ref dd))
                    {
                        if (d == p || d.MapFaction != p.MapFaction || !Protectable(d)) continue;
                        float dist = pp.Distance(d.GetPosition2D);
                        if (dist > defR || Hours(d, dist) > maxH) continue;
                        pr.Cands.Add(new KeyValuePair<MobileParty, float>(d, dist));
                    }
                    pairs.Add(pr);
                }
                catch (Exception e) { G335Stumble("Guard335.Party", e); }
            }
            var map = new Dictionary<MobileParty, G335>();
            if (pairs.Count > 0)
            {
                try { Assign335(pairs, map, R); }
                catch (Exception e) { G335Stumble("Guard335.Assign", e); }
            }
            _g335 = map;
        }

        /// <summary>2) Obroncy do par (ranga, odleglosc; chroniony nie jest obronca), sila grupy tylko z przypisanych; para lacznie slabsza odpada (najslabsza
        /// pierwsza), jej obroncy wracaja do pozostalych. 3) Wpisy migawki, najblizszy bezpieczny obronca, liczniki doby, przyklady.</summary>
        private static void Assign335(List<Pair335> pairs, Dictionary<MobileParty, G335> map, float R)
        {
            var prot = new HashSet<MobileParty>();
            for (int i = 0; i < pairs.Count; i++) prot.Add(pairs[i].P);
            var edges = new List<Edge335>();
            var str = new Dictionary<MobileParty, float>();
            for (int i = 0; i < pairs.Count; i++)
            {
                var c = pairs[i].Cands;
                for (int j = 0; j < c.Count; j++)
                {
                    if (prot.Contains(c[j].Key)) continue;
                    edges.Add(new Edge335 { I = i, D = c[j].Key, Dist = c[j].Value, Rk = pairs[i].Rk });
                    if (!str.ContainsKey(c[j].Key)) str[c[j].Key] = Str(c[j].Key);
                }
            }
            edges.Sort((a, b) => a.Rk != b.Rk ? a.Rk.CompareTo(b.Rk) : a.Dist.CompareTo(b.Dist));
            var taken = new HashSet<MobileParty>();
            for (int iter = 0; iter <= pairs.Count; iter++)
            {
                for (int i = 0; i < pairs.Count; i++) { if (pairs[i].Dropped) continue; pairs[i].Defs.Clear(); pairs[i].Group = pairs[i].Sp; }
                taken.Clear();
                for (int k = 0; k < edges.Count; k++)
                {
                    var e = edges[k];
                    var pr = pairs[e.I];
                    if (pr.Dropped || taken.Contains(e.D)) continue;
                    taken.Add(e.D);
                    pr.Defs.Add(e.D);
                    pr.Group += str[e.D];
                }
                Pair335 worst = null;
                float wr = float.MaxValue;
                for (int i = 0; i < pairs.Count; i++)
                {
                    var pr = pairs[i];
                    if (pr.Dropped) continue;
                    if (pr.Defs.Count > 0 && pr.Group >= pr.Foe) continue;
                    float r = pr.Defs.Count == 0 ? -1f : pr.Group / Math.Max(1f, pr.Foe);
                    if (r < wr) { wr = r; worst = pr; }
                }
                if (worst == null) break;
                worst.Dropped = true;                                              // jej obroncy wracaja do pozostalych par w nastepnym obiegu
            }
            for (int i = 0; i < pairs.Count; i++)
            {
                var pr = pairs[i];
                try
                {
                    if (pr.Defs.Count == 0) { _gdNoDef++; continue; }
                    _gdRatioSum += pr.Foe > 0f ? pr.Group / pr.Foe : 0f; _gdRatioN++;
                    if (pr.Dropped)
                    {
                        _gdLose++;
                        if (pr.Big) _gdLoseBig++;
                        continue;                                                  // lacznie slabsi - ucieczka jak gra
                    }
                    var p = pr.P;
                    var x = pr.X;
                    Vec2 pp = p.GetPosition2D, toX = x.GetPosition2D - pp;
                    float lx = toX.Length;
                    // najblizszy obronca, do ktorego droga nie prowadzi przez X (kat > 60 st.; w R - juz razem)
                    MobileParty near = null;
                    float nd = float.MaxValue;
                    for (int j = 0; j < pr.Defs.Count; j++)
                    {
                        var d = pr.Defs[j];
                        Vec2 v = d.GetPosition2D - pp;
                        float dist = v.Length;
                        bool safe = dist <= R || lx < 0.01f || Vec2.DotProduct(v * (1f / dist), toX * (1f / lx)) < 0.5f;
                        if (safe && dist < nd) { nd = dist; near = d; }
                    }
                    if (near == null && !pr.Battle) _gdNoSafe++;
                    bool fresh = _gdWin.Add(pr.Key);
                    _gdProt.Add(pr.Hid);
                    if (pr.Battle) _gdBattle.Add(pr.Key);
                    map[p] = new G335 { Kind = 1, Rank = pr.Rk, X = x, P = p, Near = near, Battle = pr.Battle };
                    for (int j = 0; j < pr.Defs.Count; j++)
                    {
                        var d = pr.Defs[j];
                        map[d] = new G335 { Kind = 2, Rank = pr.Rk, X = x, P = p, Battle = pr.Battle };
                        _gdDefenders.Add(d.Id);
                        if (pr.Battle) _gdToBattle.Add(d.Id);
                    }
                    if (fresh && _gdEx < MaxExamples) { _gdEx++; Example335(p, x, pr.Rank, pr.RKind, pr.Army, pr.Sp, pr.Foe, pr.Group, pr.Defs, near, near != null ? nd : -1f, pr.Battle); }
                }
                catch (Exception e) { G335Stumble("Guard335.Pair", e); }
            }
        }

        private static void Example335(MobileParty p, MobileParty x, int rank, int kind, bool army, float sp, float foe, float group, List<MobileParty> defs, MobileParty near, float nd, bool battle)
        {
            try
            {
                var sb = new StringBuilder(400);
                sb.Append("Ochrona wodza (335): mapa - ").Append(Hn(p.LeaderHero)).Append(" (").Append(RankName(rank, kind)).Append(army ? ", wodz armii" : "").Append(", sila ").Append(F0(sp))
                  .Append(battle ? ") w bitwie z " : ") zagrozony przez ").Append(x.IsMainParty ? "gracza" : Nm(x)).Append(battle ? " (sila strony " : " (sila z sojusznikami w 2R ").Append(F0(foe)).Append(") | obroncow ").Append(defs.Count).Append(": ");
                for (int i = 0; i < defs.Count && i < 5; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(Hn(defs[i].LeaderHero)).Append(" ").Append(F0(Str(defs[i])));
                }
                if (defs.Count > 5) sb.Append(", ...");
                sb.Append(" | sila grupy ").Append(F0(group)).Append(" >= ").Append(F0(foe)).Append(battle ? " - obroncy dolaczaja do bitwy" : " - bronia go razem").Append(", najblizszy bezpieczny ")
                  .Append(near != null ? Hn(near.LeaderHero) : "-").Append(" w ").Append(nd >= 0f ? F1(nd) : "-").Append(" jedn.");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { G335Stumble("Guard335.Example", e); }
        }

        // ------------------------------------------------------------ watki AI

        /// <summary>Postfiks GetBestInitiativeBehavior czynnego modelu AI partii (watki AI): obronca atakuje napastnika, chroniony jedzie do obroncy / atakuje z nimi. Nie rzuca.</summary>
        public static void Init335Postfix(MobileParty __0, ref AiBehavior __1, ref MobileParty __2, ref float __3)
        {
            if (!_g335On || __0 == null) return;
            try
            {
                var map = _g335;
                G335 g;
                if (map.Count == 0 || !map.TryGetValue(__0, out g)) return;
                var x = g.X;
                if (x == null || !x.IsActive || x.CurrentSettlement != null || x.IsCurrentlyAtSea != __0.IsCurrentlyAtSea) return;
                if (__2 != null && __2 != x && __3 > 1f && MobileParty.IsFleeBehavior(__1)) { Interlocked.Increment(ref _gvOther); return; }   // inne zagrozenie - decyzja gry
                float R = 3f;
                try { R = Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius; } catch { }
                if (g.Kind == 2)
                {
                    // chroniony w bitwie - EngageParty na X = dolaczenie do bitwy po stronie P; poza bitwa obronca daleko za P (dalej niz 2R od P i dalej od X niz P)
                    // najpierw do P - zbieraja sie przy nim, nie wpadaja na X po jednym
                    var p = g.P;
                    if (p != null && p.IsActive && p.MapEvent == null && !g.Battle)
                    {
                        Vec2 me = __0.GetPosition2D, pp = p.GetPosition2D, xp = x.GetPosition2D;
                        if (me.Distance(pp) > 2f * R && me.Distance(xp) > pp.Distance(xp))
                        {
                            if (__1 == AiBehavior.EscortParty && __2 == p && __3 > 1f) { Interlocked.Increment(ref _gvDefAlready); return; }
                            __1 = AiBehavior.EscortParty;
                            __2 = p;
                            __3 = Math.Max(__3, 1f) + 0.01f;
                            Interlocked.Increment(ref _gvDefEscort);
                            return;
                        }
                    }
                    if (__1 == AiBehavior.EngageParty && __2 == x && __3 > 1f) { Interlocked.Increment(ref _gvDefAlready); return; }
                    __1 = AiBehavior.EngageParty;
                    __2 = x;
                    __3 = Math.Max(__3, 1f) + 0.01f;
                    Interlocked.Increment(ref _gvDef);
                    return;
                }
                var near = g.Near;
                if (near == null) { Interlocked.Increment(ref _gvProtNoSafe); return; }   // brak obroncy z droga omijajaca X - decyzja gry (ucieczka)
                if (!near.IsActive || near.CurrentSettlement != null || near.MapEvent != null) return;
                if (__0.GetPosition2D.Distance(near.GetPosition2D) <= R)
                {
                    __1 = AiBehavior.EngageParty;
                    __2 = x;
                    Interlocked.Increment(ref _gvProtEngage);
                }
                else
                {
                    __1 = AiBehavior.EscortParty;
                    __2 = near;
                    Interlocked.Increment(ref _gvProtEscort);
                }
                __3 = Math.Max(__3, 1f) + 0.01f;
            }
            catch { }
        }

        /// <summary>LordEscort.AttackPostfix / ArmsFirst.AttackPostfix (watki AI): zakaz zaczepiania nie obowiazuje obroncy i chronionego wobec ich napastnika.</summary>
        internal static bool GuardAllowsAttack(MobileParty p, MobileParty t)
        {
            if (!_g335On || p == null || t == null) return false;
            try
            {
                var map = _g335;
                G335 g;
                if (map.Count == 0 || !map.TryGetValue(p, out g) || g.X == null) return false;
                if (t != g.X && !(t.Army != null && t.Army == g.X.Army)) return false;
                Interlocked.Increment(ref _gvAtk);
                return true;
            }
            catch { return false; }
        }

        // ------------------------------------------------------------ pojmanie, doba, start

        /// <summary>LordEscort.OnPrisonerTaken: pojmany lord byl w tej dobie zagrozony (para) / chroniony przez grupe.</summary>
        internal static void Guard335Captured(Hero h)
        {
            try
            {
                if (h == null || h.StringId == null) return;
                string pre = h.StringId + "|";
                foreach (var k in _gdPairs) { if (k.StartsWith(pre, StringComparison.Ordinal)) { _gdCapt++; break; } }
                if (_gdProt.Contains(h.StringId)) _gdCaptProt++;
            }
            catch { }
        }

        internal static void Guard335Daily()
        {
            var s = Settings.Current;
            try
            {
                int day = (int)Math.Floor(CampaignTime.Now.ToDays);
                Log.Info("Ochrona wodza (335): mapa dzien " + day + " | klucz " + (s != null && s.LordsDefendThreatenedLord ? "TAK" : "NIE") + ", latka " + _g335Patch
                         + " | migawek " + _gdSnaps + " | zagrozonych par (lord - silniejszy napastnik w 6R) " + _gdPairs.Count + " (wladcy / glowy rodow / znani / wodzowie armii "
                         + _gdBig.Count + ", wodzowie armii " + _gdArmyP.Count + ", napastnik gracz (migawki) " + _gdPlayer + ") | bez obroncow w zasiegu (migawki) " + _gdNoDef
                         + " | lacznie slabsi - ucieczka jak gra (migawki) " + _gdLose + " (w tym ranga wysoka " + _gdLoseBig + "), sr. sila grupy / napastnika "
                         + (_gdRatioN > 0 ? F2(_gdRatioSum / _gdRatioN) : "-") + " | bronia razem: par " + _gdWin.Count + ", chronionych " + _gdProt.Count + ", obroncow " + _gdDefenders.Count
                         + " | decyzje AI: obronca atakuje napastnika " + Volatile.Read(ref _gvDef) + " (juz atakowal " + Volatile.Read(ref _gvDefAlready) + "), obronca do chronionego " + Volatile.Read(ref _gvDefEscort)
                         + ", chroniony do obroncy " + Volatile.Read(ref _gvProtEscort) + ", chroniony atakuje z nimi " + Volatile.Read(ref _gvProtEngage) + ", inne zagrozenie (decyzja gry) "
                         + Volatile.Read(ref _gvOther) + ", chroniony bez bezpiecznej drogi do obroncy (decyzja gry) " + Volatile.Read(ref _gvProtNoSafe) + " (migawki " + _gdNoSafe + ")"
                         + " | bitwa chronionego: par " + _gdBattle.Count + ", obroncow wezwanych " + _gdToBattle.Count + ", dolaczylo " + _gdJoined.Count
                         + " | wyjatki zakazu zaczepiania 223/p9 " + Volatile.Read(ref _gvAtk) + " | pojmani z par " + _gdCapt + " (chronieni grupa " + _gdCaptProt + ") | przykladow "
                         + _gdEx + " | potkniecia 335 " + _gdStum + ".");
            }
            catch (Exception e) { Stumble("Guard335.Daily", e); }
            Guard335ClearDay();
        }

        internal static void Guard335Events(TaleWorlds.CampaignSystem.CampaignBehaviorBase owner)
        {
            CampaignEvents.TickEvent.AddNonSerializedListener(owner, delegate (float dt) { Guard335Tick(); });
        }

        /// <summary>SessionStart 223: postfiks GetBestInitiativeBehavior czynnego modelu AI partii (raz na proces), linia startu.</summary>
        internal static void Guard335SessionStart()
        {
            try
            {
                // recenzja 335: latka na typ czynnego modelu KAZDEJ kampanii (inna kampania - inny model, np. bez NavalDLC)
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.MobilePartyAIModel : null;
                var m = PatchTarget.OfActive(model, "GetBestInitiativeBehavior",
                    new[] { typeof(MobileParty), typeof(AiBehavior).MakeByRefType(), typeof(MobileParty).MakeByRefType(), typeof(float).MakeByRefType(), typeof(Vec2).MakeByRefType() },
                    "ochrona wodza: decyzja chwili AI partii (335)");
                if (m == null) { _g335M = null; _g335Patch = "BRAK (GetBestInitiativeBehavior)"; }
                else if (_g335Patched.Contains(m)) { _g335M = m; _g335Patch = "TAK (juz wczesniej w tym procesie, " + (m.DeclaringType != null ? m.DeclaringType.Name : "?") + ")"; }
                else
                {
                    if (_h == null) _h = new Harmony("com.jeff.armoury.lordescort223");
                    _h.Patch(m, postfix: new HarmonyMethod(typeof(LordEscort), nameof(Init335Postfix)) { priority = Priority.Last });
                    _g335Patched.Add(m);
                    _g335M = m;
                    _g335Patch = "TAK (" + (m.DeclaringType != null ? m.DeclaringType.Name : "?") + ")";
                }
            }
            catch (Exception e) { _g335Patch = "wyjatek " + e.GetType().Name; Stumble("Guard335.Patch", e); }
            try
            {
                var s = Settings.Current;
                var models = Campaign.Current.Models;
                float R = models.EncounterModel.GetEncounterJoiningRadius;
                Log.Info("Ochrona wodza (335): mapa start sesji | klucz Lords Defend Threatened Lord " + (s != null && s.LordsDefendThreatenedLord ? "TAK" : "NIE") + " | latka " + _g335Patch
                         + " | migawka co " + F2(models.MobilePartyAIModel.AiCheckInterval) + " h | napastnik w " + F1(R * 6f) + " jedn. (6R), obroncy w "
                         + F1(models.MobilePartyAIModel.FleeToNearbySettlementRadius) + " (ranga 1-3, wodz armii) / " + F1(models.MobilePartyAIModel.FleeToNearbyPartyRadius)
                         + " jedn. i " + F1(s != null ? s.DrMaxMarchHours : 2f) + " h marszu | sojusznicy napastnika w " + F1(2f * R) + " jedn. | obrona przy sile grupy >= napastnika.");
            }
            catch (Exception e) { Stumble("Guard335.Start", e); }
        }
    }
}
