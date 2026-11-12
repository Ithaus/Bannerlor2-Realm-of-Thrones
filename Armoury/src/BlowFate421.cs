using System;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using SandBox.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 421 (audyt RBM 11.11, C6 + decyzja Jeffa 3): SMIERC CZY RANA ZALEZNA OD CIOSU - tylko zolnierze, tylko bitwy rozgrywane (misja).
    ///
    /// Jak jest (sb_killed.cs, healing.cs:61-115, rbm.cs:1968-2025, mb_full.cs:56977-57015): gdy HP czlowieka spada do zera, silnik wola
    /// Mission.GetAgentState -> AgentDecideKilledOrUnconsciousModel.GetAgentStateProbability (czynny: SandboxAgentDecideKilledOrUnconsciousModel;
    /// ROT, BK, BKROT, RBM i NavalDLC go nie podmieniaja) = 1 - PartyHealingModel.GetSurvivalChance(partia, postac, typ obrazen, flaga broni, wrog).
    /// GetSurvivalChance u Jeffa: BKROT (+0.03 za blogoslawienstwa) -> lancuch BkrotChains -> Default z prefiksem RBM, ktory przepisuje metode BEZ
    /// warunku "obuch nie zabija". Wynik: smierc = 1 / (1 + premia chirurga + poziom x 0.02 [+ perk]) - rekrut bez chirurga 89%, obuch jak ostrze;
    /// czesc ciala i glebokosc rany nie licza sie wcale. Ta sama metoda GetSurvivalChance sluzy SYMULACJOM (cs_full.cs:114621, 114639) - dlatego
    /// NIE ruszamy jej, tylko metode modelu MISJI, ktora wola ja raz na powalonego: postfiks mnozy gotowa szanse smierci (poziom, chirurg, perki
    /// i blogoslawienstwa zostaja w niej jako czynniki, nic nie jest liczone drugi raz), symulacje zostaja jak sa. Armoury nie ma innej latki na
    /// szanse smierci zolnierza (BkrotChains tylko przywraca lancuch modeli; HeroWounds / StoryGuard / HeroFate to bohaterowie - tu pomijani).
    ///
    /// Skad cios: Agent.HandleBlow (mb_full.cs:15808-15871) odejmuje HP, wola Mission.OnAgentHit (-> OnScoreHit kazdego zachowania, z damagedHp =
    /// ile HP cios faktycznie zabral), a zaraz potem, w tym samym watku, Die -> silnik -> Mission.GetAgentState. Zachowanie Logic zapamietuje w
    /// OnScoreHit cios, po ktorym czlowiek ma HP 0 (typ, czesc ciala, nadwyzka = obrazenia - zabrane HP, limit HP); postfiks bierze go tylko, gdy
    /// to ta sama ofiara, i zdejmuje. Zgon bez takiego ciosu (krwawienie 313 przez Agent.Die, kara za granice pola, skrypty) = szansa gry bez zmian.
    /// Pola znacznika sa [ThreadStatic] (cios i rzut ida jednym watkiem; liczniki zwykle - przyblizone, gdyby trafienie przyszlo z watku pobocznego).
    ///
    /// Regula (jedna dla gracza i AI):  smierc = szansa gry x TYP x CZESC CIALA x GLEBOKOSC
    ///   TYP: ciecie / klucie 1.0, obuch BlowDeathBluntFactor (0.35)
    ///   CZESC: glowa / szyja 1.0, piers / brzuch (i cios bez czesci ciala, np. upadek) BlowDeathTorsoFactor (0.60),
    ///          barki = srednia tulowia i konczyny, reka / noga BlowDeathLimbFactor (0.15)
    ///   GLEBOKOSC: BlowDeathShallowFactor (0.50) dla lekkiego ciosu, ktory ledwie zabral reszte HP, rosnie liniowo do 1.0. Miara glebokosci (421-p,
    ///          Depth) = wieksza z dwoch: nadwyzka obrazen ponad zabrane HP / polowa limitu HP oraz cale obrazenia ciosu / limit HP. Sama nadwyzka
    ///          liczyla najciezszy cios (zdrowy czlowiek powalony jednym uderzeniem za 105) jako plytki, a dobicie rannego jako glebokie.
    /// Wszystkie czynniki &lt;= 1, wiec regula tylko obniza smiertelnosc (wiecej rannych, mniej zabitych). Bohaterowie: bez zmian (wczesny powrot).
    /// Postfiks: bez alokacji, bez try/catch, bez wywolan silnika (same pola agenta); wolany raz na powalonego, nie na trafienie.
    /// Latka w kampanii (NightHooks.InCampaign, raz na proces) - jak 413 / 415.
    /// </summary>
    internal static class BlowFate421
    {
        private const int T = 3;   // ciecie, klucie, obuch
        private const int P = 5;   // glowa / szyja, tulow, barki, rece, nogi

        private static int _patched, _wanted;
        private static string _note = "nie zakladano";
        private static string _model = "?";                  // czynny model misji (sprawdzany na starcie misji)
        private static bool _modelOk = true;

        // znacznik ciosu, po ktorym ofiara ma HP 0 (ten sam watek co rzut silnika)
        [ThreadStatic] private static Agent _victim;
        [ThreadStatic] private static int _vType, _vPart;
        [ThreadStatic] private static float _vDepth;         // 0..1: Depth()
        [ThreadStatic] private static float _vTime;          // 421-p: czas misji w chwili ciosu - rzut bierze znacznik tylko z tej samej klatki
        // ofiara ostatniego rzutu z zapisanym ciosem - do rozkladu w OnAgentRemoved (KillingBlow nie wystarcza: Blow zrobiony przez new Blow(id),
        // np. zgon z krwawienia 313, ma VictimBodyPart = 0 = Head)
        [ThreadStatic] private static Agent _rolled;
        [ThreadStatic] private static int _rolledIx;

        // liczniki misji
        private static readonly int[] _killed = new int[T * P], _wounded = new int[T * P];
        private static int _rolls, _changed, _noBlow, _heroes, _edge, _stum;
        private static int _full, _stale, _synth;            // 421-p: rzuty z szansa gry 1 (czlowiek bez partii), znaczniki z innej klatki, sztuczne ciosy od siebie
        private static float _sumBase, _sumNew;
        private static int _otherKilled, _otherWounded;      // zolnierze zabici / ranni bez zapisanego ciosu albo bez czesci ciala (krwawienie, upadek)

        /// <summary>NightHooks.InCampaign (ArmouryBehavior.OnSessionLaunched, raz na proces). Zwraca opis do linii "Latki nocy (K3): kampania".</summary>
        internal static string ApplyInCampaign(Harmony h)
        {
            if (h == null) { _note = "brak Harmony"; return _note; }
            _patched = 0; _wanted = 1;
            string miss = "";
            MethodInfo mi = null;
            // klasa DEKLARUJACA (pulapka 15.10): SandboxAgentDecideKilledOrUnconsciousModel sama nadpisuje metode abstrakcyjna; wywolanie wirtualne
            // z Mission.GetAgentState - JIT jej nie wkleja
            try
            {
                mi = AccessTools.DeclaredMethod(typeof(SandboxAgentDecideKilledOrUnconsciousModel), "GetAgentStateProbability",
                    new[] { typeof(Agent), typeof(Agent), typeof(DamageTypes), typeof(WeaponFlags), typeof(float).MakeByRefType() });
            }
            catch { }
            if (mi == null || mi.ReturnType != typeof(float)) miss += " brak SandboxAgentDecideKilledOrUnconsciousModel.GetAgentStateProbability;";
            else
            {
                try
                {
                    h.Patch(mi, postfix: new HarmonyMethod(typeof(BlowFate421), nameof(ProbPostfix)) { priority = Priority.Last });
                    if (Owned(mi, h.Id)) _patched++; else miss += " postfiks NIE wszedl;";
                }
                catch (Exception e) { miss += " BLAD " + e.GetType().Name + ";"; Log.Error("BlowFate421.Patch", e); }
            }
            var s = Settings.Current;
            _note = "szansa smierci powalonego " + _patched + "/" + _wanted + (miss.Length > 0 ? " |" + miss : "");
            Log.Info("Smierc czy rana (421): latka raz na proces - " + _note + ". Klucz BlowDecidesDeathEnabled " + (s != null && s.BlowDecidesDeathEnabled ? "TAK" : "NIE")
                     + ": smierc zolnierza = szansa gry x typ (obuch " + F(s != null ? s.BlowDeathBluntFactor : 0f) + ") x czesc ciala (glowa / szyja 1.00, tulow "
                     + F(s != null ? s.BlowDeathTorsoFactor : 0f) + ", barki " + F(s != null ? ShoulderF(s) : 0f) + ", reka / noga " + F(s != null ? s.BlowDeathLimbFactor : 0f)
                     + ") x glebokosc (" + F(s != null ? s.BlowDeathShallowFactor : 0f) + " - 1.00); bohaterowie i symulacje bez zmian.");
            return _note;
        }

        private static bool Owned(MethodBase mi, string id)
        {
            try
            {
                var info = Harmony.GetPatchInfo(mi);
                if (info == null) return false;
                foreach (var p in info.Postfixes)
                    if (p != null && p.owner == id && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(BlowFate421)) return true;
            }
            catch { }
            return false;
        }

        private static string F(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }

        private static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }

        private static float ShoulderF(Settings s) { return (Clamp01(s.BlowDeathTorsoFactor) + Clamp01(s.BlowDeathLimbFactor)) * 0.5f; }

        private static int TypeIx(DamageTypes t) { return t == DamageTypes.Blunt ? 2 : (t == DamageTypes.Pierce ? 1 : 0); }

        /// <summary>Grupa czesci ciala: 0 glowa / szyja, 1 tulow, 2 barki, 3 rece, 4 nogi; -1 = brak (upadek, cios bez kosci).</summary>
        private static int PartIx(BoneBodyPartType p)
        {
            switch (p)
            {
                case BoneBodyPartType.Head:
                case BoneBodyPartType.Neck: return 0;
                case BoneBodyPartType.Chest:
                case BoneBodyPartType.Abdomen: return 1;
                case BoneBodyPartType.ShoulderLeft:
                case BoneBodyPartType.ShoulderRight: return 2;
                case BoneBodyPartType.ArmLeft:
                case BoneBodyPartType.ArmRight: return 3;
                case BoneBodyPartType.Legs: return 4;
                default: return -1;
            }
        }

        /// <summary>Glebokosc ciosu 0..1 (421-p; wspolna z HeroBlow422): wieksza z dwoch miar - nadwyzka obrazen ponad zabrane HP / polowa limitu HP
        /// (dobicie rannego ciosem duzo ciezszym niz trzeba) oraz cale obrazenia ciosu / limit HP (zdrowy czlowiek powalony jednym uderzeniem).</summary>
        internal static float Depth(float inflicted, float damagedHp, float limit)
        {
            if (!(limit > 1f) || !(inflicted > 0f)) return 0f;
            float a = (inflicted - damagedHp) / (limit * 0.5f), b = inflicted / limit;
            return Clamp01(a > b ? a : b);
        }

        /// <summary>Mnoznik szansy smierci dla ciosu (typ 0-2, grupa czesci ciala -1..4, glebokosc 0..1). Wszystkie czynniki przyciete do 0..1.</summary>
        internal static float Factor(Settings s, int type, int part, float depth)
        {
            float f = type == 2 ? Clamp01(s.BlowDeathBluntFactor) : 1f;
            if (part == 0) { }
            else if (part == 2) f *= ShoulderF(s);
            else if (part == 3 || part == 4) f *= Clamp01(s.BlowDeathLimbFactor);
            else f *= Clamp01(s.BlowDeathTorsoFactor);                                   // tulow i cios bez czesci ciala
            float sh = Clamp01(s.BlowDeathShallowFactor);
            return f * (sh + (1f - sh) * Clamp01(depth));
        }

        /// <summary>Po SandboxAgentDecideKilledOrUnconsciousModel.GetAgentStateProbability(napastnik, ofiara, typ, flagi, out chirurg): __result = szansa smierci.
        /// Tylko zolnierz (nie bohater), tylko gdy w tej samej klatce OnScoreHit zapisal cios, ktory zabral mu reszte HP, i tylko szansa z przedzialu (0, 1]
        /// (0 = obuch w grze bez RBM - zostaje; 1 = czlowiek bez partii, np. bojka w miescie: RBM daje mu przezycie 0 - 421-p: ta sama regula co w polu).</summary>
        private static void ProbPostfix(Agent __1, ref float __result)
        {
            var v = _victim;
            _victim = null;                                                             // znacznik zawsze jednorazowy
            if (__1 == null || !__1.IsHuman) return;
            if (__1.IsHero) { _heroes++; return; }
            if (v == null || !ReferenceEquals(v, __1)) { _noBlow++; return; }
            // 421-p: znacznik tylko z tej samej klatki (cios i rzut ida jednym wywolaniem HandleBlow); stary znacznik na zywym agencie (HP podniesione
            // przez inne zachowanie po ciosie) nie przechodzi na jego pozniejszy zgon z krwawienia
            var mc = Mission.Current;
            if (mc == null || mc.CurrentTime != _vTime) { _noBlow++; _stale++; return; }
            _rolled = __1; _rolledIx = _vPart >= 0 ? _vType * P + _vPart : -1;
            float p0 = __result;
            if (!(p0 > 0f) || p0 > 1f) { _edge++; return; }
            _rolls++; _sumBase += p0;
            if (p0 >= 1f) _full++;
            var s = Settings.Current;
            if (s == null || !s.BlowDecidesDeathEnabled) { _sumNew += p0; return; }
            float p = p0 * Factor(s, _vType, _vPart, _vDepth);
            if (!(p >= 0f)) { _stum++; _sumNew += p0; return; }                          // NaN z ustawien - szansa gry zostaje
            if (p > p0) p = p0;
            _sumNew += p;
            if (p < p0) _changed++;
            __result = p;
        }

        // 436: suma misji tej sesji (bitwy w polu i kryjowki) - tylko odczyt w SimFate436, czyszczona razem z kampania (SimFate436.Reset)
        internal static int SessMissions, SessKilled, SessWounded, SessRolls;
        internal static float SessBase, SessNew;
        internal static void ResetSession() { SessMissions = 0; SessKilled = 0; SessWounded = 0; SessRolls = 0; SessBase = 0f; SessNew = 0f; }

        private static void Reset()
        {
            _victim = null; _rolled = null;
            Array.Clear(_killed, 0, _killed.Length);
            Array.Clear(_wounded, 0, _wounded.Length);
            _rolls = 0; _changed = 0; _noBlow = 0; _heroes = 0; _edge = 0; _stum = 0; _full = 0; _stale = 0; _synth = 0;
            _sumBase = 0f; _sumNew = 0f; _otherKilled = 0; _otherWounded = 0;
        }

        private static void CheckModel()
        {
            _model = "?"; _modelOk = true;
            try
            {
                var gm = MissionGameModels.Current != null ? MissionGameModels.Current.AgentDecideKilledOrUnconsciousModel : null;
                if (gm == null) { _model = "brak"; _modelOk = false; return; }
                var t = gm.GetType();
                _model = t.Name;
                // model, ktory nie dziedziczy po Sandbox: postfiks biegnie tylko, gdy ten model wola Sandbox przez BaseModel (tak robi
                // StoryModeAgentDecideKilledOrUnconsciousModel) - o dzialaniu rozstrzyga pole RZUTY w linii konca misji, nie ten znacznik
                _modelOk = typeof(SandboxAgentDecideKilledOrUnconsciousModel).IsAssignableFrom(t);
            }
            catch { _modelOk = false; }
        }

        private static string Pair(int k, int w) { return k + "/" + w; }

        private static string Ratio(int k, int w)
        {
            if (k <= 0) return w > 0 ? "sami ranni" : "-";
            return ((float)w / k).ToString("0.0", CultureInfo.InvariantCulture) + ":1";
        }

        private static void Flush(Mission m)
        {
            int k = _otherKilled, w = _otherWounded;
            for (int i = 0; i < T * P; i++) { k += _killed[i]; w += _wounded[i]; }
            if (k + w == 0 && _rolls == 0) { Reset(); return; }                           // misja bez powalonych zolnierzy - bez linii
            var s = Settings.Current;
            string kind = "?";
            try { kind = MissionTeams.Kind(m); } catch { }
            var kt = new int[T]; var wt = new int[T]; var kp = new int[P]; var wp = new int[P];
            for (int t = 0; t < T; t++)
                for (int p = 0; p < P; p++) { kt[t] += _killed[t * P + p]; wt[t] += _wounded[t * P + p]; kp[p] += _killed[t * P + p]; wp[p] += _wounded[t * P + p]; }
            // 436: suma sesji do porownania z symulacja (linia doby "Smierc czy rana w symulacji (436)") - tylko bitwy w polu i kryjowki
            // (arena / turniej: ludzie bez partii z szansa gry 1.00)
            if (kind == "bitwa" || kind == "kryjowka") { SessMissions++; SessKilled += k; SessWounded += w; SessRolls += _rolls; SessBase += _sumBase; SessNew += _sumNew; }
            Log.Info("Smierc czy rana (421): koniec misji (" + kind + ") | klucz " + (s != null && s.BlowDecidesDeathEnabled ? "TAK" : "NIE")
                     + " obuch " + F(s != null ? s.BlowDeathBluntFactor : 0f) + " tulow " + F(s != null ? s.BlowDeathTorsoFactor : 0f)
                     + " konczyna " + F(s != null ? s.BlowDeathLimbFactor : 0f) + " plytki " + F(s != null ? s.BlowDeathShallowFactor : 0f)
                     + " | latka " + _patched + "/" + _wanted + ", model " + _model + (_modelOk ? "" : " (typ nie-Sandbox: regula dziala, jesli RZUTY z ciosem > 0)")
                     + " | ZOLNIERZE powaleni " + (k + w) + ": zabici " + k + ", ranni " + w + ", ranni do zabitych " + Ratio(k, w)
                     + " | zabici/ranni wg TYPU: ciecie " + Pair(kt[0], wt[0]) + ", klucie " + Pair(kt[1], wt[1]) + ", obuch " + Pair(kt[2], wt[2])
                     + " | wg CZESCI CIALA: glowa i szyja " + Pair(kp[0], wp[0]) + ", tulow " + Pair(kp[1], wp[1]) + ", barki " + Pair(kp[2], wp[2])
                     + ", rece " + Pair(kp[3], wp[3]) + ", nogi " + Pair(kp[4], wp[4]) + ", bez zapisanego ciosu / czesci ciala (krwawienie, upadek) " + Pair(_otherKilled, _otherWounded)
                     + " | RZUTY z ciosem " + _rolls + ": srednia szansa smierci gry " + (_rolls > 0 ? F(_sumBase / _rolls) : "-") + " -> po regule "
                     + (_rolls > 0 ? F(_sumNew / _rolls) : "-") + ", obnizonych " + _changed + ", w tym z szansa gry 1.00 (czlowiek bez partii) " + _full
                     + " | bez zapisanego ciosu " + _noBlow + " (w tym znacznik z innej klatki " + _stale + "), szansa 0 (bez zmian) " + _edge
                     + ", bohaterowie (pominieci) " + _heroes + ", sztuczny cios od siebie liczony bez czesci ciala " + _synth
                     + (_stum > 0 ? ", potkniecia " + _stum : "") + ".");
            Reset();
        }

        /// <summary>Zapis ciosu, po ktorym czlowiek ma HP 0 (dla postfiksu), i rozklad zabitych / rannych zolnierzy wedlug typu ciosu i czesci ciala
        /// (takze przy kluczu NIE - pomiar "przed"). Sciezka trafien: dwa porownania i odczyt HP, bez alokacji, bez try/catch.</summary>
        internal sealed class Logic : MissionLogic
        {
            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            public override void OnBehaviorInitialize()
            {
                base.OnBehaviorInitialize();
                Reset();
                CheckModel();
            }

            public override void OnScoreHit(Agent affectedAgent, Agent affectorAgent, WeaponComponentData attackerWeapon, bool isBlocked, bool isSiegeEngineHit,
                in Blow blow, in AttackCollisionData collisionData, float damagedHp, float hitDistance, float shotDifficulty)
            {
                if (affectedAgent == null || !affectedAgent.IsHuman || affectedAgent.Health >= 1f) return;   // Agent.Health: wlasciwosc z prostym getterem pola _health (mb_full.cs:11862-11867)
                var part = blow.VictimBodyPart;
                if (part == BoneBodyPartType.None) part = collisionData.VictimHitBodyPart;
                _vType = TypeIx(blow.DamageType);
                // 421-p: sztuczny cios od siebie bez energii (Artem KnockBackAnimation: new Blow(agent.Index), InflictedDamage 1, BaseMagnitude 0 - ten sam
                // warunek co FieldCraft 313d) ma VictimBodyPart = 0 = Head z default(Blow) - liczony jako cios bez czesci ciala, nie jako glowa
                if (blow.OwnerId == affectedAgent.Index && blow.BaseMagnitude <= 0f) { _vPart = -1; _synth++; }
                else _vPart = PartIx(part);
                _vDepth = Depth(blow.InflictedDamage, damagedHp, affectedAgent.HealthLimit);
                var mc = Mission.Current;
                _vTime = mc != null ? mc.CurrentTime : -1f;
                _victim = affectedAgent;
            }

            public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
            {
                bool dead = agentState == AgentState.Killed;
                if (!dead && agentState != AgentState.Unconscious) return;
                if (affectedAgent == null || !affectedAgent.IsHuman || affectedAgent.IsHero) return;
                // silnik usuwa agenta w tym samym wywolaniu Die, zaraz po rzucie; brak pary = zgon bez zapisanego ciosu (krwawienie, skrypt)
                int i = ReferenceEquals(_rolled, affectedAgent) ? _rolledIx : -1;
                _rolled = null;
                if (i < 0) { if (dead) _otherKilled++; else _otherWounded++; return; }
                if (dead) _killed[i]++; else _wounded[i]++;
            }

            protected override void OnEndMission()
            {
                try { Flush(Mission); } catch (Exception e) { Log.Error("BlowFate421.Flush", e); }
                base.OnEndMission();
            }
        }
    }
}
