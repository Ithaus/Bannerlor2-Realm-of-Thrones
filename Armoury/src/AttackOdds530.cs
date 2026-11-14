using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 530 (Jeff 14.11: "AI wpadlo 92 ludzmi specjalnie w armie 23 ludzi jak obok mieli blackwood armie z 192 ludzmi ... czy AI jest glupie"; regula 25.10
    /// "AI walczy o zycie, mysli realnie"): AI lord przed atakiem liczy WSZYSTKICH, ktorzy stana do bitwy, i nie atakuje bez przewagi.
    /// Przyczyna (dekompilacja DefaultMobilePartyAIModel.GetBestInitiativeBehavior): localAdvantage gry liczy sojusznikow celu TYLKO z tej samej MapFaction
    /// (mobileParty4.MapFaction == mobileParty2.MapFaction) - sojusznicze krolestwo (Riverlands przy celu z Polnocy) nie istnieje dla napastnika, a z bliska
    /// atak x100 (num5). Armia sojusznika w zasiegu inicjatywy (R x 3 = 9) i tak dochodzi do bitwy. Do tego promien liczenia sojusznikow celu maleje do 1R,
    /// gdy napastnik stoi przy celu.
    /// Regula: sila strony celu E = cel (armia od wodza) + kazda partia polowa w wojnie z napastnikiem i NIE w wojnie z celem (jego frakcja albo sojusznik),
    /// w promieniu 3R od celu (waga 1 do 2R, liniowo do 0 przy 3R; uczestnicy bitwy celu waga 1). Sila wlasna O = napastnik (armia od wodza) + partie w wojnie
    /// z celem i nie w wojnie z napastnikiem, tak samo wokol celu (tam bedzie bitwa). Miara jak w grze: EstimatedStrength (z p9 - uzbrojenie).
    /// Pomijamy: partie w innej bitwie, oblegajacych, garnizony i milicje (gra ich nie liczy, nie wychodza w pole), w osadzie, Aggressiveness &lt;= 0.01
    /// (karawany, chlopi - nie dolaczaja; poza uczestnikami bitwy celu), doczepionych do armii (licza sie przez wodza), inny teren (morze / lad), tratwy.
    /// Atak tylko przy O / E &gt;= AiAttackEdgePercent (130). Inaczej:
    ///  (a) INICJATYWA - postfiks VeryLow DefaultMobilePartyAIModel.CalculateInitiativeScoresForEnemy (watki AI, co 0.25 h; po 500-b Low, przed 335 Last):
    ///      atak &gt;= 1 (gra by zaczepila) -&gt; min(atak, 0.99) = bez zaczepki (MobilePartyAi bierze inicjatywe tylko &gt; 1). Atak &lt; 1 (500-b juz przyciela) - bez zmian,
    ///      nic nie liczymy drugi raz. Dolaczanie do toczacej sie bitwy ta sama miara (swoi z bitwy po naszej stronie, cala strona celu po jego).
    ///  (b) MYSL - postfiks 52 CampaignEventDispatcher.AiHourlyTick (po 500-b 55): opcje GoAroundParty / EngageParty (poscig AiEngagePartyBehavior - gra liczy
    ///      tam sile samego celu) o ocenie &gt; 0 -&gt; -1 (jak wety ArmsFirst / 500-b).
    /// Wyjatki: (1) tylko bitwa w polu - cel oblegajacy / w osadzie / w bitwie o osade (rabunek, szturm, oblezenie) zostaje grze, BesiegedRelief i 500-b E1
    /// (gra liczy tam caly oboz oblegajacych; obrona osad to osobna regula, nie dublujemy); (2) obrona osady wlasnego rodu (ArmsFirst.DefendsOwn - rod broni
    /// swego, jak w p9 i 500-b); (3) obronca / chroniony 335 (LordEscort.GuardAllowsAttack - ratuje swojego lorda); (4) cel ucieka i O / E &gt;= 1 - poscig za
    /// uciekajacym nie szuka walki; (5) cel - banda: prog 1 (bandy nie maja sojusznikow-krolestw, gra liczy je dobrze); (6) tylko partie lordow AI (bez gracza,
    /// band, karawan, patroli).
    /// Koszt: wyszukiwanie wokol celu (jak gra w tych samych watkach) TYLKO dla ataku &gt;= 1; wynik w pamieci na pare (napastnik, cel) przez 0.25 h,
    /// slownik podmieniany co godzine (watek glowny). Watki AI: odczyty (jak gra), liczniki Interlocked, przyklady pod lockiem (najwyzej 3 na dobe).
    /// Bez wizerunkow, nic co klatke, bledy liczone (bez globalnego wylacznika).
    /// </summary>
    internal static class AttackOdds530
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string Tag = "Przewaga lokalna (530): ";
        private const double CacheHours = 0.25;   // wynik pary wazny tyle godzin (jeden takt inicjatywy)
        private const int ExMax = 3;              // przykladow w linii doby

        private struct Odds { internal double At; internal float Own, Foe, FoeSelf, FoeAllies; internal MobileParty Ally; }

        private sealed class Ex
        {
            internal MobileParty A, T, Ally; internal float Own, Foe, FoeSelf, FoeAllies; internal string Where;
        }

        private static volatile ConcurrentDictionary<(MobileParty, MobileParty), Odds> _cache = new ConcurrentDictionary<(MobileParty, MobileParty), Odds>();
        private static readonly object ExGate = new object();
        private static readonly List<Ex> _ex = new List<Ex>(ExMax);
        private static readonly List<(AIBehaviorData, float)> _copy = new List<(AIBehaviorData, float)>(32);

        private static Harmony _h;
        private static string _hInit = "nie wpieta", _hThink = "nie wpieta";
        private static int _day = -1;
        private static int _iVeto, _iKeep, _iFlee, _iBand, _iOk, _iCalc, _iHit, _iOwnSide, _tVeto, _tKeep, _tOk, _sInit, _sThink, _sCalc;

        internal static void Reset()
        {
            _cache = new ConcurrentDictionary<(MobileParty, MobileParty), Odds>();
            lock (ExGate) _ex.Clear();
            _copy.Clear(); _day = -1;
            ClearDay();
            _sInit = _sThink = _sCalc = 0;
        }

        private static void ClearDay()
        {
            Interlocked.Exchange(ref _iVeto, 0); Interlocked.Exchange(ref _iKeep, 0); Interlocked.Exchange(ref _iFlee, 0); Interlocked.Exchange(ref _iBand, 0);
            Interlocked.Exchange(ref _iOk, 0); Interlocked.Exchange(ref _iCalc, 0); Interlocked.Exchange(ref _iHit, 0); Interlocked.Exchange(ref _iOwnSide, 0);
            _tVeto = _tKeep = _tOk = 0;
            lock (ExGate) _ex.Clear();
        }

        private static void Stumble(ref int counter, string where, Exception e)
        {
            int n = Interlocked.Increment(ref counter);
            try { if (n <= 3 || n % 100 == 0) Log.Error("AttackOdds530." + where + " (potkniecie " + n + ")", e); } catch { }
        }

        private static float Edge(Settings s) { return Math.Max(100, Math.Min(300, s.AiAttackEdgePercent)) / 100f; }

        private static float Str(MobileParty p)
        {
            var a = p.Army;
            return a != null && a.LeaderParty == p ? a.EstimatedStrength : p.Party.EstimatedStrength;
        }

        // ------------------------------------------------------------ zakres reguly
        /// <summary>Napastnik i cel w zakresie reguly: partia lorda AI (nie gracz) przeciw partii w polu (bez oblezen, osad i bitew o osade).</summary>
        private static bool InScope(MobileParty a, MobileParty t)
        {
            if (a == null || t == null || a.IsMainParty || !a.IsLordParty || a.MapEvent != null) return false;
            var army = a.Army;
            if (army != null && army.LeaderParty != a && a.AttachedTo != null) return false;   // doczepiony - decyduje wodz
            if (t.BesiegerCamp != null || t.BesiegedSettlement != null || t.SiegeEvent != null || t.CurrentSettlement != null) return false;
            var me = t.MapEvent;
            if (me != null && me.MapEventSettlement != null) return false;
            return true;
        }

        /// <summary>Wyjatki (2) i (3): obrona osady rodu, obronca / chroniony 335.</summary>
        private static bool Exempt(MobileParty a, MobileParty t)
        {
            return ArmsFirst.DefendsOwn(a, t) || LordEscort.GuardAllowsAttack(a, t);
        }

        // ------------------------------------------------------------ liczenie sil
        /// <summary>Sily obu stron wokol celu (opis klasy). Dowolny watek: tylko odczyty, jak gra w GetBestInitiativeBehavior. Wynik w pamieci 0.25 h.</summary>
        private static Odds Count(MobileParty a, MobileParty t)
        {
            double now = CampaignTime.Now.ToHours;
            var cache = _cache;
            var key = (a, t);
            Odds o;
            if (cache.TryGetValue(key, out o) && now - o.At >= 0 && now - o.At < CacheHours) { Interlocked.Increment(ref _iHit); return o; }
            Interlocked.Increment(ref _iCalc);
            o = new Odds { At = now };
            IFaction fa = a.MapFaction, ft = t.MapFaction;
            float r = Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius;
            float full = 2f * r, far = 3f * r;
            Vec2 tp = t.GetPosition2D;
            bool sea = t.IsCurrentlyAtSea;
            var tme = t.MapEvent;
            o.FoeSelf = Str(t);
            o.Own = Str(a);
            float allies = 0f, best = 0f;
            var data = MobileParty.StartFindingLocatablesAroundPosition(tp, far);
            for (var p = MobileParty.FindNextLocatable(ref data); p != null; p = MobileParty.FindNextLocatable(ref data))
            {
                if (p == a || p == t || !p.IsActive || p.AttachedTo != null) continue;
                if (a.Army != null && a.Army.LeaderParty == a && p.Army == a.Army) continue;   // armia napastnika - juz w Army.EstimatedStrength
                if (t.Army != null && t.Army.LeaderParty == t && p.Army == t.Army) continue;   // armia celu - juz w sile celu
                bool inBattle = tme != null && p.MapEvent == tme;
                if (p.MapEvent != null && !inBattle) continue;
                if (p.IsGarrison || p.IsMilitia || p.CurrentSettlement != null || p.BesiegerCamp != null || p.BesiegedSettlement != null) continue;
                if (p.IsCurrentlyAtSea != sea || p.IsInRaftState) continue;
                if (!inBattle && !(p.Aggressiveness > 0.01f)) continue;
                var fp = p.MapFaction;
                if (fp == null) continue;
                bool warA = FactionManager.IsAtWarAgainstFaction(fp, fa), warT = FactionManager.IsAtWarAgainstFaction(fp, ft);
                if (warA == warT) continue;   // neutralni albo wrogowie obu (bandy wobec lordow) - nie stana po zadnej stronie
                float w;
                if (inBattle) w = 1f;
                else
                {
                    float d = p.GetPosition2D.Distance(tp);
                    if (d >= far) continue;
                    w = d <= full ? 1f : 1f - (d - full) / (far - full);
                }
                float s = Str(p) * w;
                if (warA) { allies += s; if (s > best) { best = s; o.Ally = p; } }
                else o.Own += s;
            }
            o.FoeAllies = allies;
            o.Foe = o.FoeSelf + allies;
            if (cache.Count < 20000) cache[key] = o;
            return o;
        }

        private static void Note(MobileParty a, MobileParty t, in Odds o, string where)
        {
            lock (ExGate)
            {
                var e = new Ex { A = a, T = t, Ally = o.Ally, Own = o.Own, Foe = o.Foe, FoeSelf = o.FoeSelf, FoeAllies = o.FoeAllies, Where = where };
                if (_ex.Count < ExMax) { _ex.Add(e); return; }
                // pierwszenstwo maja przyklady z sojusznikami celu (przypadek Jeffa) - zastepuja przyklad bez sojusznikow
                if (!(o.FoeAllies > 0f)) return;
                for (int i = 0; i < _ex.Count; i++) if (!(_ex[i].FoeAllies > 0f)) { _ex[i] = e; return; }
            }
        }

        /// <summary>Decyzja: true = atak dozwolony. Liczniki w kind (0 inicjatywa, 1 mysl).</summary>
        private static bool Allowed(MobileParty a, MobileParty t, Settings s, int kind)
        {
            var o = Count(a, t);
            float q = o.Own / Math.Max(0.01f, o.Foe);
            float edge = t.IsBandit ? 1f : Edge(s);
            if (q >= edge) { if (kind == 0) Interlocked.Increment(ref _iOk); else _tOk++; return true; }
            if (q >= 1f && t.IsFleeing()) { if (kind == 0) Interlocked.Increment(ref _iFlee); else _tOk++; return true; }
            if (t.IsBandit && kind == 0) Interlocked.Increment(ref _iBand);
            if (o.FoeAllies > 0f && kind == 0) Interlocked.Increment(ref _iOwnSide);
            Note(a, t, in o, kind == 0 ? "na widoku" : "poscig z mysli");
            return false;
        }

        // ------------------------------------------------------------ (a) inicjatywa (watki AI)
        /// <summary>Postfiks VeryLow DefaultMobilePartyAIModel.CalculateInitiativeScoresForEnemy(mobileParty, enemyParty, out avoidScore, out attackScore, ...). Nie rzuca.</summary>
        public static void InitPostfix(MobileParty __0, MobileParty __1, ref float __3)
        {
            if (!(__3 >= 1f)) return;   // gra nie zaczepi (albo 500-b juz przyciela) - nic nie liczymy
            try
            {
                var s = Settings.Current;
                if (s == null || !s.AiAttackOdds || !InScope(__0, __1)) return;
                if (Exempt(__0, __1)) { Interlocked.Increment(ref _iKeep); return; }
                if (!Allowed(__0, __1, s, 0)) { __3 = Math.Min(__3, 0.99f); Interlocked.Increment(ref _iVeto); }
            }
            catch (Exception e) { Stumble(ref _sInit, "InitPostfix", e); }
        }

        // ------------------------------------------------------------ (b) mysl (watek glowny)
        /// <summary>Postfiks 52 CampaignEventDispatcher.AiHourlyTick: poscig (GoAroundParty / EngageParty) bez przewagi -&gt; -1. Nie rzuca.</summary>
        public static void ThinkPostfix(MobileParty party, PartyThinkParams partyThinkParams)
        {
            if (party == null || partyThinkParams == null || party.IsMainParty || !party.IsLordParty) return;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.AiAttackOdds || partyThinkParams.DoNotChangeBehavior) return;
                var scores = partyThinkParams.AIBehaviorScores;
                if (scores == null || scores.Count == 0) return;
                bool any = false;
                for (int i = 0; i < scores.Count && !any; i++)
                {
                    var b = scores[i].Item1.AiBehavior;
                    if ((b == AiBehavior.GoAroundParty || b == AiBehavior.EngageParty) && scores[i].Item2 > 0f && scores[i].Item1.Party is MobileParty) any = true;
                }
                if (!any) return;
                _copy.Clear();
                for (int i = 0; i < scores.Count; i++) _copy.Add(scores[i]);
                for (int i = 0; i < _copy.Count; i++)
                {
                    var d = _copy[i].Item1;
                    if (!(_copy[i].Item2 > 0f) || (d.AiBehavior != AiBehavior.GoAroundParty && d.AiBehavior != AiBehavior.EngageParty)) continue;
                    var t = d.Party as MobileParty;
                    if (t == null || !InScope(party, t)) continue;
                    if (Exempt(party, t)) { _tKeep++; continue; }
                    if (!Allowed(party, t, s, 1)) { partyThinkParams.SetBehaviorScore(in d, -1f); _tVeto++; }
                }
                _copy.Clear();
            }
            catch (Exception e) { _copy.Clear(); Stumble(ref _sThink, "ThinkPostfix", e); }
        }

        // ------------------------------------------------------------ godzina, start, linia doby
        /// <summary>Watek glowny, co godzine: nowy slownik pamieci (stary zbiera GC), linia doby przy zmianie dnia.</summary>
        internal static void Hourly()
        {
            var s = Settings.Current;
            if (s == null) return;
            _cache = new ConcurrentDictionary<(MobileParty, MobileParty), Odds>();
            int day = (int)CampaignTime.Now.ToDays;
            if (_day < 0) _day = day;
            else if (day != _day) { DayLine(s); ClearDay(); _day = day; }
        }

        /// <summary>OnSessionLaunched (raz na proces): dwie latki; linia startu. Nie rzuca.</summary>
        internal static void SessionStart()
        {
            if (_h == null)
            {
                _h = new Harmony("com.jeff.armoury.attackodds530");
                var me = typeof(AttackOdds530);
                try
                {
                    var m = AccessTools.DeclaredMethod(typeof(DefaultMobilePartyAIModel), "CalculateInitiativeScoresForEnemy");
                    var ps = m != null ? m.GetParameters() : null;
                    if (m == null) _hInit = "BRAK (DefaultMobilePartyAIModel.CalculateInitiativeScoresForEnemy)";
                    else if (ps.Length < 4 || ps[0].ParameterType != typeof(MobileParty) || ps[1].ParameterType != typeof(MobileParty)
                             || ps[3].ParameterType != typeof(float).MakeByRefType())
                        _hInit = "BRAK (inna sygnatura)";
                    else { _h.Patch(m, postfix: new HarmonyMethod(me, nameof(InitPostfix)) { priority = Priority.VeryLow }); _hInit = "TAK (VeryLow)"; }
                }
                catch (Exception e) { _hInit = "wyjatek " + e.GetType().Name; Stumble(ref _sInit, "SessionStart.Init", e); }
                try
                {
                    var m = AccessTools.DeclaredMethod(typeof(CampaignEventDispatcher), "AiHourlyTick", new[] { typeof(MobileParty), typeof(PartyThinkParams) });
                    if (m == null) _hThink = "BRAK (CampaignEventDispatcher.AiHourlyTick)";
                    else { _h.Patch(m, postfix: new HarmonyMethod(me, nameof(ThinkPostfix)) { priority = 52 }); _hThink = "TAK (52)"; }
                }
                catch (Exception e) { _hThink = "wyjatek " + e.GetType().Name; Stumble(ref _sThink, "SessionStart.Think", e); }
            }
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                Log.Info(Tag + (s.AiAttackOdds ? "TAK" : "NIE") + " (atak lorda AI tylko przy sile wlasnej z sojusznikami >= " + Edge(s).ToString("F2", Inv)
                         + " x sila celu z sojusznikami, takze z innych krolestw, w promieniu dolaczenia do bitwy; bandy - prog 1, uciekajacy - prog 1;"
                         + " bez oblezen, osad i bitew o osade; obrona osady rodu i 335 bez zmian) | latki: inicjatywa " + _hInit + ", mysl (poscig) " + _hThink + ".");
            }
            catch (Exception e) { Stumble(ref _sCalc, "SessionStart", e); }
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

        private static void DayLine(Settings s)
        {
            try
            {
                int veto = Volatile.Read(ref _iVeto), keep = Volatile.Read(ref _iKeep), ok = Volatile.Read(ref _iOk), flee = Volatile.Read(ref _iFlee);
                int stumbles = _sInit + _sThink + _sCalc;
                if (!s.AiAttackOdds && veto + _tVeto + stumbles == 0) return;
                var sb = new System.Text.StringBuilder(512);
                sb.Append(Tag).Append("doba ").Append(_day)
                  .Append(" | inicjatywa: zaczepki wstrzymane ").Append(veto).Append(" (z tego cel z sojusznikami w poblizu ").Append(Volatile.Read(ref _iOwnSide))
                  .Append(", cel-banda ").Append(Volatile.Read(ref _iBand)).Append("), z przewaga ").Append(ok).Append(", poscig za uciekajacym ").Append(flee)
                  .Append(", wyjatki (obrona osady rodu / 335) ").Append(keep)
                  .Append(" | mysl: poscigi -1 ").Append(_tVeto).Append(", z przewaga ").Append(_tOk).Append(", wyjatki ").Append(_tKeep)
                  .Append(" | liczenia ").Append(Volatile.Read(ref _iCalc)).Append(", z pamieci ").Append(Volatile.Read(ref _iHit));
                lock (ExGate)
                {
                    if (_ex.Count > 0)
                    {
                        sb.Append(" | przyklady:");
                        foreach (var e in _ex)
                        {
                            sb.Append(" [").Append(e.Where).Append("] ").Append(Nm(e.A)).Append(" (wlasne z sojusznikami ").Append(e.Own.ToString("F0", Inv))
                              .Append(") -> ").Append(Nm(e.T)).Append(" (").Append(e.FoeSelf.ToString("F0", Inv)).Append(" + sojusznicy ").Append(e.FoeAllies.ToString("F0", Inv));
                            if (e.Ally != null) sb.Append(", najwiekszy ").Append(Nm(e.Ally));
                            sb.Append(" = ").Append(e.Foe.ToString("F0", Inv)).Append("), stosunek ").Append((e.Own / Math.Max(0.01f, e.Foe)).ToString("F2", Inv)).Append(";");
                        }
                    }
                }
                sb.Append(" | potkniecia: inicjatywa ").Append(_sInit).Append(", mysl ").Append(_sThink).Append(", inne ").Append(_sCalc).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble(ref _sCalc, "DayLine", e); }
        }
    }
}
