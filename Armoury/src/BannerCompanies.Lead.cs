using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 318c (Jeff 02.11: "panikowanie jednostek za szybkie: jesli dowodca zyje ... panika powinna byc obnizona, dopiero jak sztandary przepadna, dowodcy zgina
    /// ... panika moze sie nasilac, ale tez im wyzszy tier tym rzadziej panikuje - elitarne jednostki prawie nigdy, t6"). Bitwa 24 (19:33-19:40): 7 zalaman
    /// wroga wylacznie z udzialu strat (B=1.5 caly czas), T6 1/2, sr. szansa rzutu 307b 0.696; do tego odskok szarzy RBM (26 ludzi, 261 s) liczony jako strata.
    /// Jedna regula w BannerCompaniesMission.Roll: p = szansa x F x M x B x T(tier) x L(dowodztwo), limit 0.9 - zadnych innych mnoznikow tieru w rzucie.
    ///  T (klucz BannerTierSteadiness): tabela TierTable (T1 0.8, T2 0.667 .. T7 0.04; nie wyzej niz stare 1/(0.25T+1)) zamiast odpornosci R = 0.25T+1 / 0.5T+1, liniowo miedzy tierami sredniego tieru;
    ///    ta sama tabela dla paniki z bolu szeregowych (FieldCraft.RollPain, klucz PainBreakTierSteadiness) - bohaterowie bez mnoznika.
    ///  L (klucz BannerLeaderHold): czynny bohater strony (Team.Side) - zyje, nie nieprzytomny, nie zlamany (IsPanicked / zbior RoutHold; samo IsRunningAway nie - odskok RBM),
    ///    nie w odwrocie rannego 143 ani w wycofaniu rannego bohatera 149 (HeroCaution.WoundedWithdrawing); chwilowe wycofanie samotnego 149 i odskok RBM nie
    ///    odbieraja dowodztwa. Wlasny dowodca-bohater kompanii 0.5; bohater w tym samym szyku albo w 60 m od srodka kompanii 0.65; gdzie indziej 0.8; zaden 1.0.
    ///    x1.3 tylko przy faktycznym upadku sztandaru (LastFall i !StandardUp - nie brak chorazego, nie chorazy przeniesiony z szykiem), poza samym rzutem upadku.
    ///  Zdarzenie strony: wodz druzyny (Team.GeneralAgent) zabity albo nieprzytomny - kompanie jego druzyny; ostatni bohater strony zszedl z pola (kazdy w _heroDown) - kompanie strony;
    ///    raz wstrzas BannerShockCommanderLost i raz rzut F 1.5 (zrodlo 6) w kazdej niezlamanej kompanii (bez kompanii, ktora dostala juz rzut CommanderLost
    ///    za tego samego czlowieka). Kazde zdarzenie raz na bitwe (wodz - raz na druzyne, ostatni bohater - raz na strone; w 2 s po zdarzeniu wodza bez drugiego).
    ///  Szereg (klucz BannerRanksIgnoreKnockback): InRanksLive nie liczy chwilowego odwrotu komponentu (szarza RBM bez IsPanicked i bez RoutHold) jako straty -
    ///    ta sama definicja co AnyAbleLater; ranni w odwrocie 143 dalej poza szeregiem.
    /// Linie: "Choragwie (318c): zalamanie ..." przy kazdym zalamaniu (najwyzej BreakLinesMax), "Choragwie (318c): upadek dowodztwa strony ..." przy zdarzeniu,
    /// segment "dowodztwo i tier (318c)" w "Choragwie (CH) koniec" (przed potknieciami). Bez Harmony, bez globalnego wylacznika (wyjatki - BannerCompanies.Stumble).
    /// </summary>
    internal static partial class BannerCompanies
    {
        // indeks = tier (0 nieuzywany - tier 0 liczony jak 1)
        // poprawka recenzji 318c: niski tier nie wyzej niz stary wzor 309 1/(0.25T+1) (T1 0.8, T2 0.667, T3 0.571) - tabela Jeffa od T4
        internal static readonly float[] TierTable = { 0.8f, 0.8f, 0.667f, 0.55f, 0.35f, 0.18f, 0.06f, 0.04f };
        internal const float LeadOwn = 0.5f, LeadNear = 0.65f, LeadFar = 0.8f, LeadNone = 1f, LeadFallMul = 1.3f, LeadNearRange = 60f, SideEventF = 1.5f;
        internal const int BreakLinesMax = 80;
        internal static readonly string[] LeadNames = { "wlasny dowodca-bohater", "bohater strony w szyku lub w 60 m", "bohater strony dalej", "brak bohatera strony" };

        /// <summary>318c: mnoznik tieru (srednia tieru - liniowo miedzy wierszami tabeli, ponizej 1 jak 1, od 7 jak 7).</summary>
        internal static float TierHold(float t)
        {
            if (!(t > 1f)) return TierTable[1];
            if (t >= 7f) return TierTable[7];
            int i = (int)Math.Floor(t);
            float f = t - i;
            float v = TierTable[i] + (TierTable[i + 1] - TierTable[i]) * f;
            float old = 1f / (0.25f * t + 1f);   // miedzy T1 a T2 linia jest nad krzywa starego wzoru (T1.5: 0.733 > 0.727) - nie wyzej niz dawniej
            return v < old ? v : old;
        }

        // ---- liczniki 318c (zerowane w ResetCounters)
        internal static readonly int[] LeadRolls = new int[4], LeadBreaks = new int[4];
        internal static int FallMulRolls, SideEvtGeneral, SideEvtLast, SideEvtCompanies, SideEvtRolls, SideEvtBreaks, ReboundKept, BreakLines, BreakLinesCut, TLRolls;
        internal static float TSum, LSum;
        internal static int PainTierRolls, PainHeroRolls;
        internal static float PainTierSum;
        internal static readonly int[] PainTierN = new int[8], PainTierBroke = new int[8];

        internal static void Reset318()
        {
            Array.Clear(LeadRolls, 0, LeadRolls.Length); Array.Clear(LeadBreaks, 0, LeadBreaks.Length);
            FallMulRolls = SideEvtGeneral = SideEvtLast = SideEvtCompanies = SideEvtRolls = SideEvtBreaks = ReboundKept = BreakLines = BreakLinesCut = TLRolls = 0;
            TSum = LSum = 0f;
            PainTierRolls = PainHeroRolls = 0; PainTierSum = 0f;
            Array.Clear(PainTierN, 0, PainTierN.Length); Array.Clear(PainTierBroke, 0, PainTierBroke.Length);
        }

        /// <summary>318c: segment linii "Choragwie (CH) koniec" (bez znaku '|' w srodku segmentu poza separatorem na poczatku).</summary>
        internal static void Append318(StringBuilder sb, Settings c)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            sb.Append(" | dowodztwo i tier (318c): tier ").Append(c != null && c.BannerTierSteadiness ? "TAK" : "NIE")
              .Append(", dowodztwo ").Append(c != null && c.BannerLeaderHold ? "TAK" : "NIE")
              .Append(", odskok RBM w szeregu ").Append(c != null && c.BannerRanksIgnoreKnockback ? "TAK" : "NIE")
              .Append(", bol wedlug tieru ").Append(c != null && c.PainBreakTierSteadiness ? "TAK" : "NIE")
              .Append("; rzutow ").Append(TLRolls).Append(" (sr. T ").Append(TLRolls > 0 ? (TSum / TLRolls).ToString("0.000", inv) : "-")
              .Append(", sr. L ").Append(TLRolls > 0 ? (LSum / TLRolls).ToString("0.00", inv) : "-").Append(')')
              .Append("; zalaman/rzutow wedlug dowodztwa: wlasny bohater ").Append(LeadBreaks[0]).Append('/').Append(LeadRolls[0])
              .Append(", bohater blisko ").Append(LeadBreaks[1]).Append('/').Append(LeadRolls[1])
              .Append(", bohater dalej ").Append(LeadBreaks[2]).Append('/').Append(LeadRolls[2])
              .Append(", bez bohatera ").Append(LeadBreaks[3]).Append('/').Append(LeadRolls[3])
              .Append("; sztandar upadly x1.3 w rzutach ").Append(FallMulRolls)
              .Append("; zdarzenia strony ").Append(SideEvtGeneral + SideEvtLast).Append(" (wodz ").Append(SideEvtGeneral).Append(", ostatni bohater ").Append(SideEvtLast)
              .Append("; kompanii ").Append(SideEvtCompanies).Append(", rzutow ").Append(SideEvtRolls).Append(", zalaman ").Append(SideEvtBreaks).Append(')')
              .Append("; odskok RBM liczony w szeregu przy rzutach ").Append(ReboundKept)
              .Append("; linie zalaman ").Append(BreakLines).Append(" (ponad limit ").Append(BreakLinesCut).Append(')')
              .Append("; bol: rzutow szeregowych ").Append(PainTierRolls).Append(" (sr. mnoznik tieru ")
              .Append(PainTierRolls > 0 ? (PainTierSum / PainTierRolls).ToString("0.000", inv) : "-").Append("), bohaterow bez mnoznika ").Append(PainHeroRolls)
              .Append(", zlamani/rzutow wedlug tieru ");   // Tn:a/b - nie wzor T1 a/b, ktory CH-4 sumuje z calej linii
            for (int t = 1; t <= 7; t++)
            {
                if (t > 1) sb.Append(", ");
                sb.Append('T').Append(t).Append(':').Append(PainTierBroke[t]).Append('/').Append(PainTierN[t]);
            }
        }
    }

    internal sealed partial class BannerCompaniesMission
    {
        // 318c: bohaterowie misji (spis przy rozstawieniu i co 3 s w przegladzie), zeszli z pola (OnAgentRemoved - kazdy stan), wodzowie druzyn (co takt)
        private readonly List<Agent> _heroes = new List<Agent>();
        private readonly List<int> _heroSide = new List<int>();   // strona bohatera przy spisie (SideIx) - rownolegle do _heroes
        private readonly HashSet<Agent> _heroSet = new HashSet<Agent>();
        private readonly HashSet<Agent> _heroDown = new HashSet<Agent>();
        private readonly Dictionary<Team, Agent> _gen = new Dictionary<Team, Agent>();
        private readonly HashSet<Team> _genDone = new HashSet<Team>();
        private readonly bool[] _sideHad = new bool[2], _sideLastDone = new bool[2];
        private readonly float[] _sideEvtAt = { -100f, -100f };
        private readonly string[] _sideLastName = new string[2];

        private void LeadClear()
        {
            _heroes.Clear(); _heroSide.Clear(); _heroSet.Clear(); _heroDown.Clear(); _gen.Clear(); _genDone.Clear();
            for (int i = 0; i < 2; i++) { _sideHad[i] = false; _sideLastDone[i] = false; _sideEvtAt[i] = -100f; _sideLastName[i] = null; }
        }

        private static int SideIx(Team t)
        {
            if (t == null) return -1;
            if (t.Side == BattleSideEnum.Defender) return 0;
            if (t.Side == BattleSideEnum.Attacker) return 1;
            return -1;
        }

        /// <summary>318c: nowi bohaterowie w spisie (rozstawienie, przeglad co 3 s - posilki).</summary>
        private void HeroScan(Mission m)
        {
            if (m == null) return;
            var agents = m.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                var a = agents[i];
                if (a == null || !a.IsHuman || !a.IsHero || !a.IsActive() || _heroDown.Contains(a)) continue;
                if (_heroSet.Add(a)) { _heroes.Add(a); _heroSide.Add(SideIx(a.Team)); }
            }
        }

        /// <summary>318c: bohater dowodzi - czynny, nie zszedl, nie zlamany (IsPanicked) (RoutHold), nie w odwrocie rannego 143 ani w wycofaniu rannego 149.
        /// Chwilowy odwrot komponentu (szarza RBM) i wycofanie samotnego 149 dowodztwa nie odbieraja. Wyjatek = nie dowodzi.</summary>
        private bool Leads(Agent h)
        {
            if (h == null || _heroDown.Contains(h)) return false;
            try
            {
                // poprawka recenzji 318c: samo IsRunningAway (odskok RBM, natywny Retreat) nie odbiera dowodztwa - zlamanie to IsPanicked / RoutHold,
                // ranny w odwrocie to 143 (k.Withdrawing) i 149 (HeroCaution.WoundedWithdrawing)
                if (!h.IsActive()) return false;
                var cai = h.CommonAIComponent;
                if (cai != null && cai.IsPanicked) return false;
                var k = RoutHold.Live;
                if (k != null && (k.Knows(h) || k.Withdrawing(h))) return false;
                if (HeroCaution.WoundedWithdrawing(h)) return false;
                return true;
            }
            catch { return false; }
        }

        /// <summary>318c: mnoznik dowodztwa L kompanii (bez mnoznika sztandaru) i koszyk 0..3 (BannerCompanies.LeadNames).</summary>
        private float Leadership(Company co, out int bucket)
        {
            bucket = 3;
            if (co == null || co.T == null) return BannerCompanies.LeadNone;
            if (co.Commander != null && co.CmdHero && !co.CmdDown && Leads(co.Commander)) { bucket = 0; return BannerCompanies.LeadOwn; }
            Vec2 cen = Vec2.Zero;
            int n = 0;
            for (int i = 0; i < co.Men.Count; i++)
            {
                var a = co.Men[i];
                if (a == null || !a.IsActive()) continue;
                cen += a.Position.AsVec2; n++;
            }
            if (n > 0) cen = cen * (1f / n);
            var side = co.T.Side;
            bool far = false;
            float r2 = BannerCompanies.LeadNearRange * BannerCompanies.LeadNearRange;
            for (int i = 0; i < _heroes.Count; i++)
            {
                var h = _heroes[i];
                if (h == null || h.Team == null || h.Team.Side != side || !Leads(h)) continue;
                if (ReferenceEquals(h.Formation, co.F) || (n > 0 && h.Position.AsVec2.DistanceSquared(cen) <= r2)) { bucket = 1; return BannerCompanies.LeadNear; }
                far = true;
            }
            if (far) { bucket = 2; return BannerCompanies.LeadFar; }
            return BannerCompanies.LeadNone;
        }

        /// <summary>318c (co takt 0.5 s): wodzowie druzyn i zdarzenie "ostatni czynny bohater strony zszedl" (raz na strone; bez drugiego w 2 s po zdarzeniu wodza).</summary>
        private void TickLead(float now, Settings c, Mission m)
        {
            foreach (var t in m.Teams)
            {
                if (!MissionTeams.Valid(t)) continue;
                var g = t.GeneralAgent;
                if (g != null && g.IsActive()) _gen[t] = g;
            }
            if (!c.BannerLeaderHold) return;
            for (int s = 0; s < 2; s++)
            {
                if (_sideLastDone[s]) continue;
                int cnt = 0;
                string last = null;
                for (int i = 0; i < _heroes.Count; i++)
                {
                    var h = _heroes[i];
                    if (h == null || SideIx(h.Team) != s || !Leads(h)) continue;
                    cnt++;
                    try { last = h.Name; } catch { last = "?"; }
                }
                if (cnt > 0)
                {
                    _sideHad[s] = true;
                    _sideLastName[s] = cnt == 1 ? last : null;
                    continue;
                }
                if (!_sideHad[s]) continue;
                // poprawka recenzji 318c: zdarzenie tylko gdy kazdy bohater tej strony naprawde zszedl (_heroDown: zdjety z pola, nieprzytomny, zabity),
                // nie przy chwilowym cnt == 0 (odwrot rannego 143/149, chwilowa panika)
                bool allDown = true;
                for (int i = 0; i < _heroes.Count; i++)
                    if (_heroSide[i] == s && !_heroDown.Contains(_heroes[i])) { allDown = false; break; }
                if (!allDown) continue;
                _sideLastDone[s] = true;
                if (now - _sideEvtAt[s] <= 2f) continue;   // ostatni byl wodzem - zdarzenie wodza juz bylo
                BannerCompanies.SideEvtLast++;
                SideEvent(s, null, "ostatni czynny bohater strony zszedl (" + (_sideLastName[s] ?? "?") + ")", null, now, c);
            }
        }

        /// <summary>318c (OnAgentRemoved, po zdarzeniach kompanii): bohater zszedl z pola; wodz druzyny zabity albo nieprzytomny - zdarzenie strony (raz na druzyne).</summary>
        private void HeroRemoved(Agent a, AgentState st, float now, Settings c)
        {
            if (a == null || !a.IsHero) return;
            _heroDown.Add(a);
            if (!c.BannerLeaderHold || (st != AgentState.Killed && st != AgentState.Unconscious)) return;
            var t = a.Team;
            if (t == null) return;
            Agent g;
            bool gen = ReferenceEquals(t.GeneralAgent, a) || (_gen.TryGetValue(t, out g) && ReferenceEquals(g, a));
            if (!gen || !_genDone.Add(t)) return;
            int s = SideIx(t);
            if (s < 0) return;
            string who = "?";
            try { who = a.Name ?? "?"; } catch { who = "?"; }
            BannerCompanies.SideEvtGeneral++;
            SideEvent(s, t, "wodz " + who + (st == AgentState.Killed ? " zabity" : " nieprzytomny"), a, now, c);
        }

        /// <summary>318c: zdarzenie strony - raz wstrzas BannerShockCommanderLost i raz rzut F 1.5 (zrodlo 6) w kazdej niezlamanej kompanii strony
        /// (only != null - tylko kompanie tej druzyny); bez kompanii, ktorej dowodca to ten sam czlowiek (rzut CommanderLost juz byl).</summary>
        private void SideEvent(int s, Team only, string why, Agent fallen, float now, Settings c)
        {
            _sideEvtAt[s] = now;
            int comps = 0, rolls = 0, breaks = 0;
            bool player = false;
            for (int i = 0; i < _all.Count; i++)
            {
                var co = _all[i];
                if (co.Broken || co.T == null || SideIx(co.T) != s) continue;
                if (only != null && !ReferenceEquals(co.T, only)) continue;
                if (fallen != null && ReferenceEquals(co.Commander, fallen)) continue;
                comps++;
                if (PlayerSide(co)) player = true;
                try
                {
                    Shock(co, -c.BannerShockCommanderLost);
                    if (Roll(co, BannerCompanies.SideEventF, c, now, false, -1f, 6)) rolls++;
                    if (co.Broken) breaks++;
                }
                catch (Exception e) { BannerCompanies.Stumble("BannerCompaniesMission.SideEvent", e); }
            }
            BannerCompanies.SideEvtCompanies += comps; BannerCompanies.SideEvtRolls += rolls; BannerCompanies.SideEvtBreaks += breaks;
            if (player) Msg(fallen != null ? "Your army's commander has fallen - the ranks waver!" : "No lord is left standing to lead - the ranks waver!", now, c, true);
            try
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                Log.Info("Choragwie (318c): upadek dowodztwa strony | strona " + (s == 0 ? "obroncy" : "atakujacy") + (player ? " (gracza)" : " (wroga)") + " | " + why
                         + " | " + (now - _deployedAt).ToString("0", inv) + " s po rozstawieniu | kompanii " + comps + ", rzutow " + rolls + ", zalaman " + breaks + ".");
            }
            catch (Exception e) { BannerCompanies.Stumble("BannerCompaniesMission.SideEventLine", e); }
        }

        /// <summary>318c: linia przy kazdym zalamaniu (najwyzej BreakLinesMax na misje).</summary>
        private void BreakLine(Company co, float now, int src, float share, float tier, float p, float tf, int bucket, float lf, bool fell, float b)
        {
            if (BannerCompanies.BreakLines >= BannerCompanies.BreakLinesMax) { BannerCompanies.BreakLinesCut++; return; }
            BannerCompanies.BreakLines++;
            try
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                string side = co.T != null && co.T.IsPlayerTeam ? "druzyna gracza" : PlayerSide(co) ? "sojusznik gracza" : "wrog";
                string srcName = src == 1 ? "sztandar" : src == 2 ? "dowodca" : src == 3 ? "udzial strat" : src == 4 ? "straty 305c / polowa" : src == 5 ? "1/6 strat" : src == 6 ? "zdarzenie strony" : "inne";
                Log.Info("Choragwie (318c): zalamanie | " + NameOf(co) + " (szyk " + ((int)co.F.FormationIndex + 1).ToString(inv) + ", kompania " + (co.Index + 1).ToString(inv)
                         + ") | strona " + side + " | " + (now - _deployedAt).ToString("0", inv) + " s po rozstawieniu | zrodlo " + srcName
                         + " | strata " + (share >= 0f ? share.ToString("0.00", inv) : "-") + " | tier " + tier.ToString("0.00", inv) + " (T x" + tf.ToString("0.000", inv) + ")"
                         + " | dowodztwo " + (bucket >= 0 && bucket < 4 ? BannerCompanies.LeadNames[bucket] : "-") + " (L x" + lf.ToString("0.00", inv) + ")"
                         + " | sztandar upadl " + (fell ? "TAK" : "NIE") + " | stan bitwy x" + b.ToString("0.00", inv) + " | p " + p.ToString("0.000", inv) + ".");
            }
            catch (Exception e) { BannerCompanies.Stumble("BannerCompaniesMission.BreakLine", e); }
        }
    }
}
