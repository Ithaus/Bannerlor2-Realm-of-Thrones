using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 145 (25.10, fala "AI walczy o zycie", karta syntezy "140 Ranny walczy ostroznie (20-50% HP)"; Jeff 25.10 ok. 13:30: "przeciwnik zachowuje sie jak
    /// prawdziwy zywy czlowiek, nie chce dac sie zabic i zrobi wszystko w obszarze jego mozliwosci i skilli aby przetrwac, walczy o zycie!"): RANNY WALCZY
    /// OSTROZNIE (klucz WoundedFightWarily). Cialo i wola osobno: FieldCraft spowalnia cialo rannego (ruch i zamach od WoundedBelowPercent), 145 zmienia
    /// tylko jego decyzje - nie rzuca sie pierwszy, czesciej porzuca zamach na rzecz obrony, czesciej blokuje i zaslania sie tarcza.
    ///
    /// Dowod [K]:
    ///  - RBM (RBMAI AgentAi.cs:95-131, galaz "postura, bez vanillaCombatAi") daje decyzje bez wzgledu na zdrowie: atak po decyzji 0.15 (z tarcza
    ///    clamp(0.3n, 0.1, 0.15), konno 0.3), porzucenie zamachu 0.5 x zmeczenie, blok clamp(2n, 0.3, 1), obrona tarcza 1; gra
    ///    (AgentStatCalculateModel.SetAiRelatedProperties:165-229) tez nie czyta Health. Ranny z 25% HP atakuje tak samo jak zdrowy.
    ///  - kierunek "ostroznie" wedlug wzorow gry z Agent.Defensiveness (szarza 0, stanie w szyku 1, stanie w murze tarcz 2 - MovementOrder:422,
    ///    ArrangementOrder:474): AIAttackOnDecideChance = 0.1 x max x (3 - D) - W DOL z obrona; AIDecideOnAttackChance = 0.5 x D - W GORE z obrona
    ///    (szansa porzucenia zamachu na rzecz obrony - to samo ustalil m113 VeteranAggressionFix); AiDefendWithShieldDecisionChanceValue =
    ///    min(2, 0.5 + n + 0.6 x (n + D)) - W GORE. Dlatego AIDecideOnAttackChance idzie w gore (x (2 - f)), nie w dol jak w karcie (odstepstwo w CHANGELOG).
    ///  - odswiezanie: RBM StanceLogic.OnMissionTick (StanceLogic.cs:1916-1933) co 3 s wola Agent.UpdateAgentStats() kazdemu czynnemu czlowiekowi, gdy
    ///    postureEnabled i AllowAiTicking -> UpdateAgentProperties (Agent.cs:1834-1840, 3658-3664) -> model (Artem -> SandboxAgentStatCalculateModel.
    ///    UpdateHumanStats:1107) -> SetAiRelatedProperties -> RBM -> VeteranAi.Postfix. Brama VeteranAi.RbmBranchOn wymaga tej samej postury, wiec spadek HP
    ///    dociera najpozniej po 3 s - bez wlasnego UpdateAgentProperties. RBM i gra PRZYPISUJA te pola przy kazdym wywolaniu - nasze mnozenie sie nie kumuluje.
    ///
    /// Zasada (wolane z VeteranAi.Postfix: po regule weteranow i po licznikach FieldDuel - srednie tierow m113 zostaja bez rannych):
    ///  - kto: czlowiek AI (nie gracz, nie umarly) w bitwie (BattleSense.InBattle) przy galezi RBM z postura, obie strony, takze ludzie gracza;
    ///    NIE bohater (decyzja Jeffa D4 "ostrozni pozycyjnie" - krok 149: wodz i lordowie cofaja sie za szyk, ich profil pojedynku DuelistsInBattles i zrywy
    ///    DuelCraft zostaja - inaczej 145 oslabialby lorda na polu dwa razy); PANIKA pominieta (przelacznik 139, wariant z dowolnego watku
    ///    SurvivalMode.PanicAnyThread - postfiks bywa wolany z ticku asynchronicznego);
    ///  - hp = Health / HealthLimit (ta sama miara co FieldCraft i BrokenMen), prog rannego = WoundedBelowPercent (50, ten sam co kara FieldCraft - jedna
    ///    granica "ranny"): f = 0.4 + 0.6 x hp / prog (50% -> 1.0, 35% -> 0.82, 20% -> 0.64, 10% -> 0.52); ponizej progu Wounded Flee (143: odwrot z bronia
    ///    i postoj "tylko obrona" w trybie ROZSADEK) ten sam wzor dalej w dol;
    ///  - atak po decyzji x f (clamp weteranow 0.05-0.6, nigdy w gore); porzucenie zamachu x (2 - f) (clamp 0-1, nigdy w dol); blok: luka do 1
    ///    mniejsza o (1 - f) (blok + (1 - blok) x (1 - f), w clampie RBM 0.3-1); obrona tarcza x (2 - f) (clamp gry 2, nigdy w dol);
    ///  - klucz NIE: ta sama ocena na sucho (linia pokazuje wartosci przed i to, co dalby klucz TAK), pola bez zmian.
    /// Wydajnosc: jedno wywolanie na czlowieka co 3 s; bez LINQ; alokacja tylko w zbiorach roznych ludzi i slowniku odstepow. Liczniki pod RoutHold.Gate
    /// (zerowane w RoutHold.Ensure); wyjatek liczony (RoutHold.Stumble, miejsce "ostroznosc"), funkcja dalej dziala.
    /// Linia "ranni ostrozni (145)" po linii 143 (RoutHold.Write).
    /// </summary>
    internal static class WoundedWary
    {
        internal const float Floor = 0.4f;           // f przy 0 HP (karta: 0.4 + 0.6 x HP / prog)
        internal const float LongGap = 4f;           // odstep wywolan u rannego dluzszy niz takt RBM (3 s) z zapasem - do linii
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---- liczniki misji (pod RoutHold.Gate; zerowane w RoutHold.Ensure)
        private static bool _seen, _key;
        private static float _thr = 0.5f;
        private static readonly HashSet<int> People = new HashSet<int>(), Mine = new HashSet<int>(), Deep = new HashSet<int>();
        private static readonly HashSet<int> Mounted = new HashSet<int>(), Shielded = new HashSet<int>();
        private static readonly HashSet<int> HeroSkip = new HashSet<int>(), PanicFlag = new HashSet<int>(), PanicRout = new HashSet<int>();
        private static int _calls, _atCap, _shdN;
        private static double _fSum, _hpSum, _aodB, _aodA, _doaB, _doaA, _blkB, _blkA, _shdB, _shdA;
        private static float _fMin = 1f;
        private static int _hN, _hShdN;
        private static double _hAod, _hDoa, _hBlk, _hShd;
        private static readonly Dictionary<int, float> Last = new Dictionary<int, float>();
        private static double _gapSum;
        private static int _gapN, _gapLong;
        private static float _gapMax;
        private static string _sample;

        internal static void ResetCounters()
        {
            _seen = _key = false;
            _thr = 0.5f;
            People.Clear(); Mine.Clear(); Deep.Clear(); Mounted.Clear(); Shielded.Clear();
            HeroSkip.Clear(); PanicFlag.Clear(); PanicRout.Clear();
            _calls = _atCap = _shdN = 0;
            _fSum = _hpSum = _aodB = _aodA = _doaB = _doaA = _blkB = _blkA = _shdB = _shdA = 0.0;
            _fMin = 1f;
            _hN = _hShdN = 0;
            _hAod = _hDoa = _hBlk = _hShd = 0.0;
            Last.Clear();
            _gapSum = 0.0;
            _gapN = _gapLong = 0;
            _gapMax = 0f;
            _sample = null;
        }

        private static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }

        /// <summary>Prog rannego z FieldCraft (WoundedBelowPercent) jako ulamek; 0 = zasada nieczynna (prog ponizej 1%).</summary>
        internal static float Threshold(Settings c)
        {
            if (c == null) return 0.5f;
            float t = c.WoundedBelowPercent / 100f;
            return t > 0.01f ? Clamp(t, 0f, 1f) : 0f;
        }

        /// <summary>f(HP) = 0.4 + 0.6 x hp / prog dla hp ponizej progu; 1 = bez zmian.</summary>
        internal static float Factor(float hp, float thr)
        {
            if (thr <= 0f || hp >= thr) return 1f;
            if (hp < 0f) hp = 0f;
            return Floor + (1f - Floor) * (hp / thr);
        }

        /// <summary>Ranny bohater AI (VeteranAi.Postfix, przed profilem pojedynku) - poza zasada (krok 149); tylko licznik do linii.</summary>
        internal static void Hero(Agent agent, Mission m, Settings c)
        {
            try
            {
                float lim = agent.HealthLimit;
                float hp = lim > 0f ? agent.Health / lim : 1f;
                if (hp >= Threshold(c)) return;
                lock (RoutHold.Gate) { RoutHold.EnsureLocked(m); _seen = true; HeroSkip.Add(agent.Index); }
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteWary, "WoundedWary.Hero", e); }
        }

        /// <summary>Szeregowy AI w bitwie (VeteranAi.Postfix po regule weteranow; parametr to ten sam obiekt co agent.AgentDrivenProperties).
        /// Zdrowy - tylko tlo do linii; ranny w ROZSADKU - ostroznosc (klucz TAK) albo pomiar na sucho (NIE); ranny w PANICE - bez zmian, licznik.</summary>
        internal static void Apply(Agent agent, AgentDrivenProperties p, Mission m, Settings c)
        {
            try
            {
                bool key = c.WoundedFightWarily;
                float thr = Threshold(c);
                float lim = agent.HealthLimit;
                float hp = lim > 0f ? agent.Health / lim : 1f;
                bool shield = VeteranAi.HasShield(agent);
                float aodB = p.AIAttackOnDecideChance, doaB = p.AIDecideOnAttackChance, blkB = p.AIBlockOnDecideAbility, shdB = p.AiDefendWithShieldDecisionChanceValue;
                if (thr <= 0f || hp >= thr)
                {
                    // zdrowy (albo zasada bez progu) - tlo do porownania (srednie z wywolan)
                    lock (RoutHold.Gate)
                    {
                        RoutHold.EnsureLocked(m);
                        _seen = true; _key = key; _thr = thr;
                        _hN++; _hAod += aodB; _hDoa += doaB; _hBlk += blkB;
                        if (shield) { _hShdN++; _hShd += shdB; }
                    }
                    return;
                }
                int id = agent.Index;
                // przelacznik 139: PANIKA - bez rozsadku (flagi komponentu albo zbior RoutHold; wariant z dowolnego watku)
                int panic = SurvivalMode.PanicAnyThread(agent);
                if (panic != 0)
                {
                    lock (RoutHold.Gate)
                    {
                        RoutHold.EnsureLocked(m);
                        _seen = true; _key = key; _thr = thr;
                        if (panic == 1) PanicFlag.Add(id); else PanicRout.Add(id);
                    }
                    return;
                }
                float f = Factor(hp, thr);
                float aodA = Math.Min(aodB, Math.Max(0.05f, Math.Min(0.6f, aodB * f)));
                float doaA = Math.Max(doaB, Clamp(doaB * (2f - f), 0f, 1f));
                float blkA = blkB >= 1f ? blkB : Math.Max(blkB, Math.Min(1f, blkB + (1f - blkB) * (1f - f)));
                float shdA = Math.Max(shdB, Math.Min(2f, shdB * (2f - f)));
                if (key)
                {
                    p.AIAttackOnDecideChance = aodA;
                    p.AIDecideOnAttackChance = doaA;
                    p.AIBlockOnDecideAbility = blkA;
                    p.AiDefendWithShieldDecisionChanceValue = shdA;
                }
                bool mine = false, mounted = false;
                try { var t = agent.Team; mine = t != null && t.IsPlayerAlly; } catch { mine = false; }
                try { mounted = agent.HasMount; } catch { mounted = false; }
                bool deep = c.WoundedFleePercent > 0f && hp < c.WoundedFleePercent / 100f;
                float now = 0f;
                try { now = m.CurrentTime; } catch { now = 0f; }
                string sample = null;
                if (_sample == null)                     // odczyt bez blokady - najwyzej dwie probki, zapis pod blokada
                    sample = "HP " + (hp * 100f).ToString("0", Inv) + "%, f " + f.ToString("0.00", Inv) + ": atak " + aodB.ToString("0.000", Inv) + "->" + aodA.ToString("0.000", Inv)
                             + ", porzucenie " + doaB.ToString("0.00", Inv) + "->" + doaA.ToString("0.00", Inv) + ", blok " + blkB.ToString("0.00", Inv) + "->" + blkA.ToString("0.00", Inv)
                             + ", tarcza " + shdB.ToString("0.00", Inv) + "->" + shdA.ToString("0.00", Inv) + (shield ? " (w rece)" : " (bez)");
                lock (RoutHold.Gate)
                {
                    RoutHold.EnsureLocked(m);
                    _seen = true; _key = key; _thr = thr;
                    People.Add(id);
                    if (mine) Mine.Add(id);
                    if (deep) Deep.Add(id);
                    if (mounted) Mounted.Add(id);
                    if (shield) Shielded.Add(id);
                    _calls++;
                    _fSum += f; _hpSum += hp;
                    if (f < _fMin) _fMin = f;
                    _aodB += aodB; _aodA += aodA; _doaB += doaB; _doaA += doaA; _blkB += blkB; _blkA += blkA;
                    if (blkB >= 0.999f) _atCap++;
                    if (shield) { _shdN++; _shdB += shdB; _shdA += shdA; }
                    float prev;
                    if (Last.TryGetValue(id, out prev) && now > prev)
                    {
                        float gap = now - prev;
                        _gapSum += gap; _gapN++;
                        if (gap > _gapMax) _gapMax = gap;
                        if (gap > LongGap) _gapLong++;
                    }
                    Last[id] = now;
                    if (sample != null && _sample == null) _sample = sample;
                }
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteWary, "WoundedWary.Apply", e); }
        }

        private static string Avg(double s, int n, string fmt) { return n > 0 ? (s / n).ToString(fmt, Inv) : "-"; }

        /// <summary>Linia "ranni ostrozni (145)" - RoutHold.Write (wolajacy trzyma Gate), po linii 143. Bitwa z wywolaniami postfiksu albo klucz TAK
        /// w bitwie / kryjowce (wtedy z powodem, czemu nieczynne).</summary>
        internal static void Write(Settings c, string kind)
        {
            bool keyNow = c != null && c.WoundedFightWarily;
            bool battle = kind == "bitwa" || kind == "kryjowka";
            if (!_seen && !(keyNow && battle)) return;
            bool key = _seen ? _key : keyNow;
            var sb = new StringBuilder(1400);
            sb.Append("ranni ostrozni (145): misja ").Append(kind).Append(", klucz ").Append(key ? "TAK" : "NIE");
            float thr = _seen ? _thr : Threshold(c);
            string thrS = (thr * 100f).ToString("0", Inv);
            if (!_seen)
            {
                sb.Append(" (nieczynne: ").Append(!VeteranAi.Patched
                    ? "latka SetAiRelatedProperties niewpieta - RBM nieobecny albo przy starcie gry wszystkie klucze latki NIE (Veterans By Tier, Duel AI, Duelists In Battles, Wounded Fight Warily)"
                    : "brak wywolan postfiksu w bitwie - RBM bez postury albo vanillaCombatAi (bez odswiezania co 3 s)").Append(')');
            }
            else if (key)
                sb.Append(" (ponizej ").Append(thrS).Append("% HP - prog kary FieldCraft: atak po decyzji x f, porzucenie zamachu i obrona tarcza x (2 - f), blok blizej 1; f = ")
                  .Append(Floor.ToString("0.0", Inv)).Append(" + ").Append((1f - Floor).ToString("0.0", Inv)).Append(" x HP / ").Append(thr.ToString("0.00", Inv))
                  .Append("; bez bohaterow (krok 149) i bez PANIKI (139); odswiezanie RBM co 3 s)");
            else
                sb.Append(" (tylko pomiar - pola bez zmian; '->' to wartosc, ktora dalby klucz TAK)");
            sb.Append(" | latka VeteranAi ").Append(VeteranAi.Patched ? "TAK" : "NIE").Append(", po RBM ").Append(VeteranAi.OrderLocked);
            sb.Append(" | rannych ostroznych ").Append(People.Count).Append(" (strona gracza ").Append(Mine.Count).Append(" / wroga ").Append(People.Count - Mine.Count)
              .Append("; konnych ").Append(Mounted.Count).Append(", z tarcza w rece ").Append(Shielded.Count)
              .Append("; ponizej progu Wounded Flee ").Append(c != null ? c.WoundedFleePercent.ToString("0", Inv) : "?").Append("% ").Append(Deep.Count)
              .Append(" - odwrot 143 / postoj albo bez celu odwrotu)")
              .Append(" | wywolan z ostroznoscia ").Append(_calls).Append(": sr. HP ").Append(_calls > 0 ? (100.0 * _hpSum / _calls).ToString("0", Inv) + "%" : "-")
              .Append(", sr. f ").Append(Avg(_fSum, _calls, "0.00")).Append(" (najmniej ").Append(_calls > 0 ? _fMin.ToString("0.00", Inv) : "-").Append(')')
              .Append("; odstep wywolan u rannego sr. ").Append(Avg(_gapSum, _gapN, "0.0")).Append(" s (").Append(_gapN).Append(" odstepow), najdluzej ")
              .Append(_gapN > 0 ? _gapMax.ToString("0.0", Inv) : "-").Append(" s, dluzej niz ").Append(LongGap.ToString("0", Inv)).Append(" s ").Append(_gapLong)
              .Append(" | atak po decyzji (AIAttackOnDecideChance): ranni ").Append(Avg(_aodB, _calls, "0.000")).Append(" -> ").Append(Avg(_aodA, _calls, "0.000"))
              .Append(", zdrowi ").Append(Avg(_hAod, _hN, "0.000"))
              .Append(" | porzucenie zamachu (AIDecideOnAttackChance): ranni ").Append(Avg(_doaB, _calls, "0.00")).Append(" -> ").Append(Avg(_doaA, _calls, "0.00"))
              .Append(", zdrowi ").Append(Avg(_hDoa, _hN, "0.00"))
              .Append(" | blok (AIBlockOnDecideAbility): ranni ").Append(Avg(_blkB, _calls, "0.00")).Append(" -> ").Append(Avg(_blkA, _calls, "0.00"))
              .Append(" (juz 1.0 w ").Append(_atCap).Append(" wywolaniach), zdrowi ").Append(Avg(_hBlk, _hN, "0.00"))
              .Append(" | obrona tarcza (AiDefendWithShieldDecisionChanceValue, tarcza w rece): ranni ").Append(Avg(_shdB, _shdN, "0.00")).Append(" -> ")
              .Append(Avg(_shdA, _shdN, "0.00")).Append(" (").Append(_shdN).Append(" wywolan), zdrowi ").Append(Avg(_hShd, _hShdN, "0.00")).Append(" (").Append(_hShdN).Append(')')
              .Append(" | wywolan zdrowych ").Append(_hN)
              .Append(" | pominieci ranni: bohaterowie ").Append(HeroSkip.Count).Append(" (krok 149 - ostroznosc pozycyjna, profil pojedynku zostaje), PANIKA ")
              .Append(PanicFlag.Count + PanicRout.Count).Append(" (IsPanicked / IsRetreating ").Append(PanicFlag.Count).Append(", zbior RoutHold ").Append(PanicRout.Count).Append(')')
              .Append(" | probka: ").Append(_sample ?? "-")
              .Append(" | potkniecia ostroznosc ").Append(RoutHold.StumblesLocked(RoutHold.SiteWary)).Append('.');
            Log.Info(sb.ToString());
        }
    }
}
