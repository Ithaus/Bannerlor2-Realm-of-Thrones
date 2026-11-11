using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 422 (audyt RBM 11.11, C7 + decyzja Jeffa 3; rbm-mapa-rany-i-nasze.md "Uproszczenie nr 2"): RANA BOHATERA ZALEZNA OD CIOSU.
    ///
    /// Jak bylo (HeroWounds 401 / 403): stopien rany to czysty rzut Grave 8% (+ wiek) / Serious 20% / reszta Light, a typ okaleczenia - rzut z tabeli
    /// 40 / 25 / 20 / 15. Nie liczylo sie, czym i w co bohater dostal.
    ///
    /// Skad cios - BEZ LATKI HARMONY. Kolejnosc w silniku (mb_full.cs): Agent.HandleBlow (15808) odejmuje HP i wola Mission.OnAgentHit -> OnScoreHit
    /// kazdego zachowania (57614); przy HP &lt; 1 Die -> Mission.OnAgentRemoved (54990): zachowania, a potem affectedAgent.OnRemove (55025) ->
    /// Origin.OnAgentRemoved(Health) (15537) -> PartyGroupAgentOrigin / SimpleAgentOrigin: Hero.HitPoints = max(1, HP agenta) (cs_full.cs:224472,
    /// 224622) -> seter HP wola CampaignEvents.HeroWounded (cs_full.cs:34190) -> HeroWounds.OnHeroWounded. Wszystko w jednym watku i jednym wywolaniu,
    /// w trakcie misji. Bohater, ktory dotrwal do konca misji z HP pod progiem rany, przechodzi te sama droge w Mission.EndMissionInternal (56573:
    /// najpierw OnEndMission zachowan, potem agent.OnRemove) - dlatego znaczniki NIE sa czyszczone w OnEndMission, tylko w OnMissionStateFinalized
    /// i na starcie nastepnej misji; HeroWounds bierze znacznik tylko, gdy Mission.Current != null (symulacja = stary rzut).
    ///
    /// Logic.OnScoreHit zapamietuje OSTATNI cios, ktory zabral bohaterowi HP: typ obrazen, grupe czesci ciala, glebokosc (nadwyzka obrazen ponad
    /// reszte HP / polowa limitu HP - jak 421; 0 dla ciosu, ktory nie powalil) i czy powalil. HeroWounds.OnHeroWounded mnozy nim szanse:
    ///
    ///   GRAVE   x TYP (ciecie / klucie 1.05, obuch 0.70) x CZESC (glowa / szyja 1.50, tulow i cios bez czesci ciala 1.10, barki 0.70,
    ///             reka 0.30, noga 0.30) x GLEBOKOSC (0.80 + 0.50 x glebokosc: 0.80 ledwie powalil ... 1.30 gleboko)
    ///   SERIOUS x TYP (ciecie / klucie 0.975, obuch 1.15 - obuch lamie kosci, czesciej rani niz zabija) x CZESC (noga 1.10, reszta 1.00)
    ///             x GLEBOKOSC (0.90 + 0.25 x glebokosc)
    ///
    /// SREDNIA zostaje (rachunek w CHANGELOG 422; ZALOZENIE rozkladu ciosow powalajacych bohatera: glowa / szyja 35%, tulow 25%, barki 15%, rece 10%,
    /// nogi 15%; obuch 15%; srednia glebokosc 0.4): Grave x0.98 x0.9975 x1.00 = x0.98, Serious x1.015 x1.001 x1.00 = x1.02. Faktyczna srednia
    /// "szansa gry -> po regule" jest w linii doby "Rany (HW)" (czlon 422).
    /// Okaleczenie (HeroFate.TryMaim, przesilenie): czesc ciala z wiersza ksiegi zaweza typ - glowa / szyja: blizna 60 / oko 40; reka: dlon;
    /// barki: dlon 60 / blizna 40; noga: kulawizna; tulow / brak: stara tabela. Liczba okaleczen bez zmian (Hero Wounds Maim Percent).
    ///
    /// Goraca sciezka: OnScoreHit - dla nie-bohatera jedno porownanie i jedno wywolanie wirtualne (Agent.IsHero); dla bohatera rzutowanie i zapis
    /// do slownika (klucz juz jest = bez alokacji; nowy bohater = jeden wpis, slownik z zapasem 64). Bez try/catch, bez wywolan silnika.
    /// Jedna regula dla gracza i AI. Klucz Hero Wounds By Blow NIE = HeroWounds nie pyta o znacznik (stan sprzed 422).
    /// </summary>
    internal static class HeroBlow422
    {
        internal const int P = 5;   // glowa / szyja, tulow, barki, rece, nogi (jak BlowFate421)
        internal const float GraveCapPercent = 30f;   // sufit szansy Grave po regule (stary bohater + gleboki cios w glowe)

        private struct Mark
        {
            public int Type, Part;     // typ: 0 ciecie, 1 klucie, 2 obuch; czesc: -1 brak, 0..4
            public float Depth;        // 0..1
            public bool Felled;        // po tym ciosie HP < 1
        }

        private static readonly Dictionary<Hero, Mark> _marks = new Dictionary<Hero, Mark>(64);

        // liczniki misji
        private static int _hits, _felled, _taken;
        private static readonly int[] _felledPart = new int[P + 1];   // [P] = bez czesci ciala
        private static int _felledBlunt;

        internal static void Clear() { _marks.Clear(); }

        private static void ResetCounters()
        {
            _hits = 0; _felled = 0; _taken = 0; _felledBlunt = 0;
            Array.Clear(_felledPart, 0, _felledPart.Length);
        }

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

        internal static string PartName(int part)
        {
            switch (part)
            {
                case 0: return "glowa / szyja";
                case 1: return "tulow";
                case 2: return "bark";
                case 3: return "reka";
                case 4: return "noga";
                default: return "bez czesci ciala";
            }
        }

        internal static string TypeName(int type) { return type == 2 ? "obuch" : type == 1 ? "klucie" : "ciecie"; }

        private static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }

        /// <summary>Mnoznik szansy rany Grave (typ 0-2, grupa czesci ciala -1..4, glebokosc 0..1).</summary>
        internal static float GraveFactor(int type, int part, float depth)
        {
            float f = type == 2 ? 0.70f : 1.05f;
            if (part == 0) f *= 1.50f;
            else if (part == 2) f *= 0.70f;
            else if (part == 3 || part == 4) f *= 0.30f;
            else f *= 1.10f;                                   // tulow i cios bez czesci ciala (upadek)
            return f * (0.80f + 0.50f * Clamp01(depth));
        }

        /// <summary>Mnoznik szansy rany Serious.</summary>
        internal static float SeriousFactor(int type, int part, float depth)
        {
            float f = type == 2 ? 1.15f : 0.975f;
            if (part == 4) f *= 1.10f;
            return f * (0.90f + 0.25f * Clamp01(depth));
        }

        /// <summary>HeroWounds.OnHeroWounded (w misji): ostatni cios, ktory zabral bohaterowi HP w tej misji. Znacznik jednorazowy.</summary>
        internal static bool Take(Hero h, out int type, out int part, out float depth, out bool felled)
        {
            Mark m;
            if (h == null || _marks.Count == 0 || !_marks.TryGetValue(h, out m)) { type = 0; part = -1; depth = 0f; felled = false; return false; }
            _marks.Remove(h);
            _taken++;
            type = m.Type; part = m.Part; depth = m.Depth; felled = m.Felled;
            return true;
        }

        private static void Flush(Mission m)
        {
            if (_hits == 0) { ResetCounters(); return; }
            string kind = "?";
            try { kind = MissionTeams.Kind(m); } catch { }
            var s = Settings.Current;
            Log.Info("Rany wg ciosu (422): koniec misji (" + kind + ") | klucz Hero Wounds By Blow " + (s != null && s.HeroWoundsByBlowEnabled ? "TAK" : "NIE")
                     + " | ciosy w bohaterow (zabraly HP) " + _hits + ", powalajace " + _felled + ": glowa i szyja " + _felledPart[0] + ", tulow " + _felledPart[1]
                     + ", barki " + _felledPart[2] + ", rece " + _felledPart[3] + ", nogi " + _felledPart[4] + ", bez czesci ciala " + _felledPart[P]
                     + "; obuchem " + _felledBlunt + " | znaczniki wziete przez ksiege ran do tej chwili " + _taken + ", czekaja " + _marks.Count
                     + " (bohater z HP pod progiem rany na koniec misji bierze swoj po tej linii; reszta przepada).");
            ResetCounters();
        }

        internal sealed class Logic : MissionLogic
        {
            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            public override void OnBehaviorInitialize()
            {
                base.OnBehaviorInitialize();
                Clear();
                ResetCounters();
            }

            public override void OnScoreHit(Agent affectedAgent, Agent affectorAgent, WeaponComponentData attackerWeapon, bool isBlocked, bool isSiegeEngineHit,
                in Blow blow, in AttackCollisionData collisionData, float damagedHp, float hitDistance, float shotDifficulty)
            {
                if (affectedAgent == null || !(damagedHp > 0f) || !affectedAgent.IsHero) return;
                var co = affectedAgent.Character as CharacterObject;
                var h = co != null ? co.HeroObject : null;
                if (h == null) return;
                var part = blow.VictimBodyPart;
                if (part == BoneBodyPartType.None) part = collisionData.VictimHitBodyPart;
                Mark m;
                m.Type = blow.DamageType == DamageTypes.Blunt ? 2 : (blow.DamageType == DamageTypes.Pierce ? 1 : 0);
                m.Part = PartIx(part);
                m.Felled = affectedAgent.Health < 1f;                                   // Agent.Health to pole (mb_full.cs:11862)
                float limit = affectedAgent.HealthLimit;
                float over = blow.InflictedDamage - damagedHp;
                m.Depth = m.Felled && limit > 1f && over > 0f ? Clamp01(over / (limit * 0.5f)) : 0f;
                _marks[h] = m;
                _hits++;
                if (m.Felled)
                {
                    _felled++;
                    _felledPart[m.Part >= 0 ? m.Part : P]++;
                    if (m.Type == 2) _felledBlunt++;
                }
            }

            protected override void OnEndMission()
            {
                // UWAGA: bez Clear() - silnik zdejmuje stojacych agentow (i ustawia HP bohaterow) PO tym wywolaniu (Mission.EndMissionInternal)
                try { Flush(Mission); } catch (Exception e) { Log.Error("HeroBlow422.Flush", e); }
                base.OnEndMission();
            }

            public override void OnMissionStateFinalized()
            {
                Clear();
                base.OnMissionStateFinalized();
            }
        }
    }
}
