using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// m113-walka2 faza 2 (24.10, Jeff po fazie 1 w 106 - turniej, arena i pojedynek trudniejsze: walka w polu ma byc trudniejsza): ROZSZERZENIA BITWY.
    ///
    /// Dowod [K]: w bitwie bohater (lord, towarzysz, przeciwnik-bohater) szedl regula weteranow z tierem z poziomu (TierOf: lord 25 = T4), czyli
    /// RBM-owy zolw (atak po decyzji 0.15, decyzja o ataku 0.5, trzymanie zamachu 1 s) jak szeregowy; VeteranAi mnozyl takze AIDecideOnAttackChance
    /// (szanse porzucenia zamachu - profil DuelAi obniza ja z umiejetnoscia) przez agresje tieru, wiec T6 x1.20 czesciej rezygnowal z ataku;
    /// zasady postury pojedynku (roznica umiejetnosci) dzialaly tylko w szrankach.
    /// Zakres (po recenzji): BattleSense.InBattle - rodzaj "bitwa" (kazda misja przy bitwie gracza na mapie: pole, szturm i obrona murow, wypad,
    /// najazd) i "kryjowka"; klucze dzialaja we wszystkich tych misjach, nie tylko w polu.
    ///
    /// Klucze (kazdy osobno, domyslnie TAK):
    ///  - DuelistsInBattles: bohater AI od poziomu 0.40 dostaje profil DuelAi (runda 0) - DuelAi.ApplyField z VeteranAi.Postfix; ponizej - weterani;
    ///    po recenzji przy VeteransByTier poziom co najmniej prog tieru weteranow (DuelAi.TierMin), konny - atak po decyzji co najmniej 0.30 x agresja;
    ///  - HeroTierFromSkill: bohater bez profilu - tier = max(TierOf(poziom), clamp(1 + (najlepsza - 40) / 30, 1, 7)) (VeteranAi.SkillTier);
    ///  - VeteranAggressionFix: AIDecideOnAttackChance bez mnozenia w gore od T4 (T1-T3 jak w 106: x0.80 / x0.88 / x0.96);
    ///  - DuelSkillGapInBattles: hak A DuelGuard (roznica umiejetnosci, waga DuelSkillGapWeight) w kontaktach gracza - bez DuelPerfectDefence;
    ///    granice bitwy 0.75-1.25 (DuelGuard.FieldGapMul), ta sama regula w obie strony (24.10: recenzja chciala tylko przeciw graczowi - odrzucone, jedna regula);
    ///  - DuelistBurstsInBattles: zrywy DuelCraft (otwarcie / nacisk) dla bohaterow AI - zgloszenia tylko z obrony bohatera (FieldTrack), tick
    ///    DuelCraft z BattleSense.OnMissionTick, najwyzej 8 UpdateAgentStats na klatke (liczba ludzi ograniczona do bohaterow); po recenzji
    ///    w bitwie przerwa nacisku 6 s, po otwarciu 2 s, otwarcie w trakcie otwarcia odrzucone (DuelCraft.Field*Cooldown).
    /// Linie: "Bitwa (m113-walka2) tabela" (start sesji) i "Bitwa (m113-walka2): misja ..." (koniec bitwy, BattleSense.Clean).
    /// Liczniki pod RoutHold.Gate (postfiks SetAiRelatedProperties bywa wolany z ticku asynchronicznego), zerowane w Begin (BattleSense.EarlyStart);
    /// bez alokacji poza HashSet roznych ludzi (jak VeteranAi).
    /// </summary>
    internal static class FieldDuel
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // rodzaje zdarzen hakow B (NoteBreak)
        internal const int BrPlayerAtt = 0, BrPlayerDef = 1, BrAiAttVsPlayer = 2, BrAiDefVsPlayer = 3, BrHeroDef = 4, BrOtherAtt = 5, HeroDefence = 6;
        private const int Events = 7;

        internal static bool AnyKey(Settings c)
        {
            return c != null && (c.DuelistsInBattles || c.HeroTierFromSkill || c.VeteranAggressionFix || c.DuelSkillGapInBattles || c.DuelistBurstsInBattles);
        }

        /// <summary>Czy w bitwie potrzebne haki postury DuelGuard (A: roznica umiejetnosci, B: zrywy i liczniki).</summary>
        internal static bool HooksWanted(Settings c)
        {
            return c != null && ((c.DuelSkillGapInBattles && c.DuelSkillGapWeight > 0f) || BurstsOn(c));
        }

        internal static bool BurstsOn(Settings c)
        {
            return c != null && c.DuelistsInBattles && c.DuelistBurstsInBattles;
        }

        // ---- liczniki misji (pod RoutHold.Gate)
        private static readonly HashSet<int> _heroes = new HashSet<int>();
        private static readonly HashSet<int>[] _heroBands = { new HashSet<int>(), new HashSet<int>(), new HashSet<int>() };
        private static int _heroCalls, _heroBurstCalls;
        private static double _hLvl, _hAoD, _hDoA;
        private static readonly int[] _tCalls = new int[8];
        private static readonly double[] _tAoD = new double[8], _tDoA = new double[8];
        private static readonly HashSet<int> _vetHeroes = new HashSet<int>(), _raised = new HashSet<int>();
        private static int _gapDef, _gapAtt;
        private static double _gapDefSum, _gapAttSum;
        private static readonly int[] _ev = new int[Events];

        /// <summary>Poczatek bitwy (BattleSense.Classify, glowny watek): liczniki od zera, zrywy i pamiec hakow od nowa.</summary>
        internal static void Begin(Mission m)
        {
            lock (RoutHold.Gate)
            {
                _heroes.Clear();
                for (int i = 0; i < _heroBands.Length; i++) _heroBands[i].Clear();
                _heroCalls = _heroBurstCalls = 0;
                _hLvl = _hAoD = _hDoA = 0.0;
                Array.Clear(_tCalls, 0, _tCalls.Length);
                Array.Clear(_tAoD, 0, _tAoD.Length);
                Array.Clear(_tDoA, 0, _tDoA.Length);
                _vetHeroes.Clear(); _raised.Clear();
                _gapDef = _gapAtt = 0;
                _gapDefSum = _gapAttSum = 0.0;
                Array.Clear(_ev, 0, _ev.Length);
            }
            DuelCraft.Reset();
            DuelGuard.BeginField();
        }

        /// <summary>Koniec bitwy (BattleSense.Clean, po Write): zrywy bez agentow w statykach.</summary>
        internal static void End()
        {
            DuelCraft.Reset();
            DuelGuard.EndField();
        }

        /// <summary>DuelAi.ApplyField (dowolny watek): bohater z profilem. aod / doa - profil przed zrywem.</summary>
        internal static void NoteHero(Mission m, int id, int s, float lvl, float aod, float doa, int burst)
        {
            lock (RoutHold.Gate)
            {
                RoutHold.EnsureLocked(m);
                _heroes.Add(id);
                _heroBands[s < 140 ? 0 : (s < 180 ? 1 : 2)].Add(id);   // po recenzji: profil wymaga s >= 100 / trudnosc - pasmo <100 bylo zawsze puste
                _heroCalls++;
                if (burst != DuelCraft.None) _heroBurstCalls++;
                _hLvl += lvl; _hAoD += aod; _hDoA += doa;
            }
        }

        /// <summary>VeteranAi.Postfix (pod RoutHold.Gate): wartosci po regule weteranow wedlug tieru.</summary>
        internal static void NoteTierLocked(int id, int tier, float aod, float doa, bool hero, bool raised)
        {
            int t = tier < 1 ? 1 : (tier > 7 ? 7 : tier);
            _tCalls[t]++;
            _tAoD[t] += aod;
            _tDoA[t] += doa;
            if (hero) _vetHeroes.Add(id);
            if (raised) _raised.Add(id);
        }

        /// <summary>Hak A (glowny watek): kontakt gracza z roznica umiejetnosci. playerSide - strona tego haka to gracz (hak obroncy: gracz broni sie,
        /// hak atakujacego: gracz atakuje); defSide - hak calculateDefenderPostureDamage; f - mnoznik obrazen postury gracza. Strona przeciwnika
        /// tego samego ciosu nie liczy sie drugi raz.</summary>
        internal static void NoteGap(bool playerSide, bool defSide, float f)
        {
            if (!playerSide) return;
            lock (RoutHold.Gate)
            {
                if (defSide) { _gapDef++; _gapDefSum += f; }
                else { _gapAtt++; _gapAttSum += f; }
            }
        }

        internal static void NoteBreak(int kind)
        {
            if (kind < 0 || kind >= Events) return;
            lock (RoutHold.Gate) { _ev[kind]++; }
        }

        private static string F(double v) { return v.ToString("0.00", Inv); }

        /// <summary>Koniec bitwy (BattleSense.Clean): linia "Bitwa (m113-walka2): misja ...".</summary>
        internal static void Write(Mission m, string kind)
        {
            var c = Settings.Current;
            if (!AnyKey(c)) return;
            var sb = new StringBuilder(1400);
            float dm = VeteranAi.DiffMulNow();
            float mins = m != null ? m.CurrentTime / 60f : 0f;
            sb.Append("Bitwa (m113-walka2): misja ").Append(kind).Append(" (Duelists In Battles ").Append(c.DuelistsInBattles ? "TAK" : "NIE")
              .Append(", Hero Tier From Skill ").Append(c.HeroTierFromSkill ? "TAK" : "NIE").Append(", Veteran Aggression Fix ").Append(c.VeteranAggressionFix ? "TAK" : "NIE")
              .Append(", Duel Skill Gap In Battles ").Append(c.DuelSkillGapInBattles ? "TAK" : "NIE").Append(" waga ").Append(F(DuelGuard.Weight(c)))
              .Append(", Duelist Bursts In Battles ").Append(c.DuelistBurstsInBattles ? "TAK" : "NIE").Append("; latka weteranow ").Append(VeteranAi.Patched ? "TAK" : "NIE")
              .Append(", trudnosc x").Append(F(dm)).Append(", misja ").Append(mins.ToString("0.0", Inv)).Append(" min)");
            lock (RoutHold.Gate)
            {
                RoutHold.EnsureLocked(m);
                sb.Append(" | bohaterowie z profilem DuelAi ").Append(_heroes.Count).Append(" (umiejetnosc do 139 ").Append(_heroBands[0].Count).Append(", 140-179 ")
                  .Append(_heroBands[1].Count).Append(", 180+ ").Append(_heroBands[2].Count).Append("; wywolan ").Append(_heroCalls).Append(", w zrywie ").Append(_heroBurstCalls)
                  .Append(" (").Append(_heroCalls > 0 ? (100.0 * _heroBurstCalls / _heroCalls).ToString("0", Inv) : "-").Append("%, ponad 25% - zrywy za czesto)");
                if (_heroCalls > 0)
                    sb.Append("; srednio poziom ").Append(F(_hLvl / _heroCalls)).Append(", AoD ").Append(F(_hAoD / _heroCalls)).Append(", DoA ").Append(F(_hDoA / _heroCalls));
                sb.Append(") | bohaterowie regula weteranow ").Append(_vetHeroes.Count).Append(" (tier z umiejetnosci wyzszy ").Append(_raised.Count).Append(')')
                  .Append(" | weterani srednio AoD/DoA:");
                for (int t = 1; t <= 7; t++)
                {
                    sb.Append(t > 1 ? "," : "").Append(" T").Append(t).Append(' ');
                    if (_tCalls[t] == 0) { sb.Append('-'); continue; }
                    sb.Append(F(_tAoD[t] / _tCalls[t])).Append('/').Append(F(_tDoA[t] / _tCalls[t])).Append(" (").Append(_tCalls[t]).Append(')');
                }
                sb.Append(" | kontakty gracza z roznica umiejetnosci: gracz obronca ").Append(_gapDef).Append(" (garda gracza srednio x")
                  .Append(_gapDef > 0 ? F(_gapDefSum / _gapDef) : "-").Append("), gracz atakujacy ").Append(_gapAtt).Append(" (x")
                  .Append(_gapAtt > 0 ? F(_gapAttSum / _gapAtt) : "-").Append(')')
                  .Append(" | postura peka: gracz atakujacy ").Append(_ev[BrPlayerAtt]).Append(", gracz obronca ").Append(_ev[BrPlayerDef])
                  .Append("; AI przeciw graczowi atakujacy ").Append(_ev[BrAiAttVsPlayer]).Append(", obronca ").Append(_ev[BrAiDefVsPlayer])
                  .Append("; bohater AI obronca ").Append(_ev[BrHeroDef]).Append(", atakujacy na bohaterze ").Append(_ev[BrOtherAtt])
                  .Append(" (obron bohaterow w hakach ").Append(_ev[HeroDefence]).Append(')')
                  .Append(" | zrywy bohaterow: otwarcie ").Append(DuelCraft.StartedOpening).Append(", nacisk ").Append(DuelCraft.StartedPressed)
                  .Append(" (w przerwie ").Append(DuelCraft.Cooled).Append(", kolejka pelna ").Append(DuelCraft.Dropped).Append(", odswiezen ").Append(DuelCraft.Refreshed).Append(')')
                  .Append(" | haki ").Append(DuelGuard.OrderLocked()).Append(", czas hakow ").Append(DuelGuard.HookMs().ToString("0.0", Inv)).Append(" ms")
                  .Append(" | potkniecia weteran ").Append(RoutHold.StumblesLocked(RoutHold.SiteVeteran)).Append(", pojedynek ").Append(RoutHold.StumblesLocked(RoutHold.SiteDuel)).Append('.');
            }
            Log.Info(sb.ToString());
        }

        /// <summary>Linia "Bitwa (m113-walka2) tabela" (start sesji, PO McmSettings.Apply): klucze, profil bohatera przy umiejetnosci 100..250, tier
        /// z umiejetnosci, agresja weteranow, przyklad roznicy umiejetnosci, zrywy, stan hakow.</summary>
        internal static void SessionStart()
        {
            var c = Settings.Current;
            if (c == null) return;
            float dm = VeteranAi.DiffMulNow();
            var sb = new StringBuilder(1800);
            sb.Append("Bitwa (m113-walka2) tabela: Duelists In Battles ").Append(c.DuelistsInBattles ? "TAK" : "NIE").Append(" (latka SetAiRelatedProperties ")
              .Append(VeteranAi.Patched ? "TAK" : "NIE").Append("), trudnosc x").Append(F(dm)).Append(", bohater AI od poziomu ").Append(F(DuelAi.Floor))
              .Append(" profil DuelAi runda 0, AoD/DoA/AoP/Par/Hold bez tarczy | z tarcza:");
            int[] skills = { 100, 140, 180, 210, 250 };
            for (int i = 0; i < skills.Length; i++)
            {
                float lvl = DuelAi.Level(skills[i], dm, 0, c);
                sb.Append(' ').Append(skills[i]).Append("->L").Append(F(lvl));
                if (lvl < DuelAi.Floor) { sb.Append(" weterani;"); continue; }
                var a = DuelAi.ShapeOf(lvl, false, false, 1f);
                var b = DuelAi.ShapeOf(lvl, true, false, 1f);
                sb.Append(' ').Append(F(a.AoD)).Append('/').Append(F(a.DoA)).Append('/').Append(F(a.AoP)).Append('/').Append(F(a.Parry)).Append('/').Append(F(a.Hold))
                  .Append(" | ").Append(F(b.AoD)).Append('/').Append(F(b.DoA)).Append('/').Append(F(b.AoP)).Append('/').Append(F(b.Parry)).Append('/').Append(F(b.Hold)).Append(';');
            }
            // po recenzji: minimum z reguly weteranow - lord poziomu 35 (T6) z najlepsza umiejetnoscia 120
            {
                float l0 = DuelAi.Level(120, dm, 0, c);
                int t35 = Math.Max((int)Math.Ceiling((35 - 5) / 5.0), c.HeroTierFromSkill ? VeteranAi.SkillTier(120) : 1);
                float l1 = c.VeteransByTier ? Math.Max(l0, 0.05f + 0.13f * (t35 - 1)) : l0;
                sb.Append(" | minimum tieru (Veterans By Tier ").Append(c.VeteransByTier ? "TAK" : "NIE").Append("): lord poziom 35 (T").Append(t35).Append("), umiejetnosc 120: L ")
                  .Append(F(l0)).Append(l0 < DuelAi.Floor ? " (ponizej profilu - weterani)" : "").Append(" -> ").Append(F(l1)).Append(" (losowy kierunek obrony ")
                  .Append(F(1f - l0 * l0 * l0)).Append(" -> ").Append(F(1f - l1 * l1 * l1)).Append(", parowanie w ataku ").Append(F(l0)).Append(" -> ").Append(F(l1))
                  .Append("), konny T5/T6/T7 atak po decyzji co najmniej ").Append(F(0.30f * 1.12f)).Append('/').Append(F(0.30f * 1.20f)).Append('/').Append(F(0.30f * 1.28f));
            }
            sb.Append(" | Hero Tier From Skill ").Append(c.HeroTierFromSkill ? "TAK" : "NIE").Append(": najlepsza");
            int[] best = { 39, 40, 70, 100, 130, 160, 190, 220, 300 };
            for (int i = 0; i < best.Length; i++) sb.Append(i > 0 ? "," : "").Append(' ').Append(best[i]).Append(" T").Append(VeteranAi.SkillTier(best[i]));
            sb.Append(" (bohater bez profilu; tier = wyzszy z poziomu i z umiejetnosci) | Veteran Aggression Fix ").Append(c.VeteranAggressionFix ? "TAK" : "NIE")
              .Append(" (RBM AoD 0.15 / DoA 0.50 przy pelnej staminie):");
            for (int t = 1; t <= 7; t++)
            {
                float agg = 0.8f + 0.08f * (t - 1);
                float aod = Math.Min(0.6f, Math.Max(0.05f, 0.15f * agg));
                float doa = c.VeteranAggressionFix && agg >= 1f ? 0.5f : Math.Min(1f, 0.5f * agg);   // po recenzji: T1-T3 jak w 106
                sb.Append(t > 1 ? "," : "").Append(" T").Append(t).Append(' ').Append(F(aod)).Append('/').Append(F(doa));
            }
            float w = DuelGuard.Weight(c);
            // po recenzji: gracz 24 z tarcza na T6 180 z tarcza (pula postury T6 z SoldierPostureByTier) i gracz 220 z tarcza na rekrucie 50 z tarcza
            float gA = (180f + 20f - 24f) / 250f, gD = (24f + 20f - 180f) / 250f, gR = (50f + 20f - 220f) / 250f, gP = (220f + 20f - 50f) / 250f;
            float fFoe = DuelGuard.FieldGapMul(1f - w * gA, false), fOwnAtt = DuelGuard.FieldGapMul(1f + w * gA, true), fOwnDef = DuelGuard.FieldGapMul(1f - w * gD, true);
            float pool = c.SoldierPostureByTier ? BattleWind.SoldierMul(6) : 1f;
            sb.Append(" | Duel Skill Gap In Battles ").Append(c.DuelSkillGapInBattles ? "TAK" : "NIE").Append(" (waga ").Append(F(w))
              .Append(", w obie strony, granice ").Append(F(DuelGuard.FieldGapLo)).Append('-').Append(F(DuelGuard.FieldGapHi))
              .Append(", bez Perfect Defence; gracz 24 z tarcza na T6 180 z tarcza: garda T6 x").Append(F(fFoe)).Append(" przy puli postury T6 x").Append(F(pool))
              .Append(" = ").Append(F(pool / fFoe)).Append(" raza wiecej ciosow niz RBM, koszt ciosu gracza x").Append(F(fOwnAtt)).Append(", garda gracza pod ciosem T6 x").Append(F(fOwnDef))
              .Append("; gracz 220 na rekrucie 50: garda rekruta x").Append(F(DuelGuard.FieldGapMul(1f - w * gR, false))).Append(", garda gracza x")
              .Append(F(DuelGuard.FieldGapMul(1f - w * gP, true))).Append(") | Duelist Bursts In Battles ").Append(c.DuelistBurstsInBattles ? "TAK" : "NIE")
              .Append(" (tylko bohaterowie AI z profilem: otwarcie ").Append(F(DuelCraft.OpeningSeconds)).Append(" s od L").Append(F(DuelAi.Floor)).Append(", nacisk ")
              .Append(F(DuelCraft.PressedSeconds)).Append(" s od L").Append(F(DuelAi.PressedFrom)).Append(", przerwa nacisku w bitwie ").Append(F(DuelCraft.FieldPressedCooldown))
              .Append(" s, po otwarciu ").Append(F(DuelCraft.FieldOpeningCooldown)).Append(" s, najwyzej ").Append(DuelCraft.PerTick).Append(" odswiezen na klatke) | zakres: bitwa (pole, mury, wypad, najazd) i kryjowka | haki bitwy ")
              .Append(HooksWanted(c) ? "potrzebne" : "niepotrzebne").Append(": ").Append(DuelGuard.HookState());
            if (BattleSense.DuelTestFlagFile())
                sb.Append(" | UWAGA: lezy CrashScribe/armoury-pojedynek-test.flag - w autotescie bitwy ida jako pojedynek, linii 'Bitwa (m113-walka2): misja' nie bedzie");
            sb.Append('.');
            Log.Info(sb.ToString());
        }

        private static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }
    }
}
