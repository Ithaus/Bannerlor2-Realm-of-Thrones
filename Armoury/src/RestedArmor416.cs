using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 416 (audyt RBM 11.11, C3): ZDJECIE PREMII x1.3 PANCERZA U WYPOCZETEGO.
    ///
    /// Skad premia (rbm.cs:3543-3562, RBMCombat OverrideDamageCalc.Prefix na MissionCombatMechanicsHelper.ComputeBlowDamage): gdy RBMConfig.postureEnabled
    /// i staminaEnabled, a ofiara ma postawe w RBMAI.AgentStances.values, pancerz (lokalna "num") mnozony jest przez
    ///   0.3 + max(0.7, stamina / maxStamina)   = x1.0 przy pasku do 70%, rosnie liniowo do x1.3 przy pelnym.
    /// Liczba siedzi w srodku prefiksu RBM (dwie stale w jednym wyrazeniu) - nie ma osobnej metody do postfiksu. Zamiast podmiany IL: wlasny prefiks
    /// (Priority.First - przed RBM) na te sama metode silnika dzieli pancerz wejsciowy (AttackInformation.ArmorAmountFloat) przez TEN SAM mnoznik,
    /// policzony z tych samych pol postawy - po mnozeniu RBM wychodzi pancerz bez premii. Ten sam chwyt co lezacy w 415 (NeckGuard415.PronePrefix);
    /// oba prefiksy mnoza to samo pole, kolejnosc bez znaczenia. Struktura budowana na jedno trafienie (mb_full.cs:58466-58470), RBM czyta ja raz
    /// (rbm.cs:3352) - zmiana nie wycieka. Postawy RBM nie dotykamy (zadnego zapisu staminy).
    ///
    /// Czego to nie obejmuje (RBM podmienia pancerz PO naszym prefiksie, rbm.cs:3456-3494): trafienie pod pache (pancerz = korpus.arm + plaszcz.arm)
    /// i w twarz przy helmie z wlasnym body_armor - tam premia zostaje; twarz bez helmu (x0.15) i helm bez body_armor (0) wychodza poprawnie.
    /// Obrazenia od upadku: RBM nie liczy pancerza (num = 0) - pomijamy. Sila ciosu atakujacego (x0.85-1.0 od jego staminy) zostaje - to nie premia.
    ///
    /// Ostatnia czesc linii konca misji: progi przebicia RBM dla strzal, beltow i oszczepow odczytane z pamieci RBM (RBMConfig.weaponTypesFactors) -
    /// kontrola, czy tools/rbm_config_combat.py (A1, A2: config.xml w Documents) jest wgrany i czy RBM nie wrocil do wartosci fabrycznych.
    ///
    /// Jedna regula dla gracza i AI. Brak RBM = nieczynne (linia logu). Latka w kampanii (NightHooks.InCampaign, raz na proces) - jak 413 / 415.
    /// </summary>
    internal static class RestedArmor416
    {
        private const float RestedFloor = 0.7f;              // stale RBM: 0.3 + max(0.7, stamina / maxStamina)
        private const float RestedBase = 0.3f;

        private static int _patched, _wanted;
        private static string _note = "nie zakladano";
        private static MethodBase _blow;                     // MissionCombatMechanicsHelper.ComputeBlowDamage
        private static bool _rbmBlow;                        // prefiks RBM (z premia) jest teraz na ComputeBlowDamage
        private static bool _rbmStaminaOn;                   // RBMConfig.postureEnabled && staminaEnabled (odczyt na starcie misji)
        private static FieldInfo _fValues, _fPosture, _fStaminaOn, _fFactors;
        private static IDictionary _stances;                 // RBMAI.AgentStances.values (Dictionary<Agent, Stance>) - odczyt przez indeksator, bez alokacji
        private static AccessTools.FieldRef<object, float> _rStamina, _rMaxStamina;

        // liczniki misji (glowny watek: ComputeBlowDamage i OnScoreHit ida z Mission.GetAttackCollisionResults / RegisterBlow)
        private static int _blows, _rested, _changed;
        private static float _factorSum, _armBefore, _armAfter;
        private static Agent _lastVictim;                    // ofiara ostatniego ciosu z postawa RBM - do licznika trafien
        private static bool _lastRested;
        private static int _hRested, _hTired, _hRestedHeavy, _hTiredHeavy;
        private static float _hpRested, _hpTired;

        /// <summary>NightHooks.InCampaign (ArmouryBehavior.OnSessionLaunched, raz na proces). Zwraca opis do linii "Latki nocy (K3): kampania".</summary>
        internal static string ApplyInCampaign(Harmony h)
        {
            if (h == null) { _note = "brak Harmony"; return _note; }
            _patched = 0; _wanted = 0;
            string miss = "";
            Type tStances = null, tStance = null, tCfg = null, tCombat = null;
            try { tCombat = AccessTools.TypeByName("RBMCombat.ArmorRework"); } catch { }
            try { tStances = AccessTools.TypeByName("RBMAI.AgentStances"); } catch { }
            try { tStance = AccessTools.TypeByName("RBMAI.Stance"); } catch { }
            try { tCfg = AccessTools.TypeByName("RBMConfig.RBMConfig"); } catch { }
            if (tCombat == null || tStances == null || tStance == null || tCfg == null)
            {
                _note = "RBM nieobecny (brak RBMCombat.ArmorRework / RBMAI.AgentStances / RBMAI.Stance / RBMConfig.RBMConfig) - premia wypoczetego nietknieta";
                Log.Info("Wypoczety (416): " + _note + ".");
                return _note;
            }

            _wanted++;
            try
            {
                _fValues = AccessTools.DeclaredField(tStances, "values");
                _fPosture = AccessTools.DeclaredField(tCfg, "postureEnabled");
                _fStaminaOn = AccessTools.DeclaredField(tCfg, "staminaEnabled");
                _fFactors = AccessTools.DeclaredField(tCfg, "weaponTypesFactors");
                var fs = AccessTools.DeclaredField(tStance, "stamina");
                var fm = AccessTools.DeclaredField(tStance, "maxStamina");
                if (fs != null && fm != null && fs.FieldType == typeof(float) && fm.FieldType == typeof(float) && !fs.IsStatic && !fm.IsStatic)
                {
                    _rStamina = AccessTools.FieldRefAccess<object, float>(fs);
                    _rMaxStamina = AccessTools.FieldRefAccess<object, float>(fm);
                }
            }
            catch (Exception e) { Log.Error("RestedArmor416.Fields", e); }
            bool fieldsOk = _fValues != null && _fValues.IsStatic && typeof(IDictionary).IsAssignableFrom(_fValues.FieldType)
                            && _fPosture != null && _fPosture.IsStatic && _fPosture.FieldType == typeof(bool)
                            && _fStaminaOn != null && _fStaminaOn.IsStatic && _fStaminaOn.FieldType == typeof(bool)
                            && _rStamina != null && _rMaxStamina != null;
            MethodInfo blow = null;
            try { blow = AccessTools.DeclaredMethod(typeof(MissionCombatMechanicsHelper), "ComputeBlowDamage"); } catch { }
            _blow = blow;
            if (!fieldsOk) miss += " pola RBM nie pasuja (AgentStances.values / Stance.stamina, maxStamina / RBMConfig.postureEnabled, staminaEnabled) - latka NIE zalozona;";
            else if (blow == null) miss += " brak MissionCombatMechanicsHelper.ComputeBlowDamage;";
            else
            {
                try
                {
                    h.Patch(blow, prefix: new HarmonyMethod(typeof(RestedArmor416), nameof(RestedPrefix)) { priority = Priority.First });
                    if (Owned(blow, h.Id)) _patched++; else miss += " prefiks NIE wszedl;";
                }
                catch (Exception e) { miss += " prefiks BLAD " + e.GetType().Name + ";"; Log.Error("RestedArmor416.Patch(blow)", e); }
                Refresh();
                if (!_rbmBlow) miss += " ComputeBlowDamage teraz bez prefiksu RBM (modul walki RBM wylaczony?) - czeka;";
            }

            var s = Settings.Current;
            _note = "premia wypoczetego " + _patched + "/" + _wanted + (miss.Length > 0 ? " |" + miss : "");
            Log.Info("Wypoczety (416): latka raz na proces - " + _note + ". Klucz RestedArmorBonusRemoved " + (s != null && s.RestedArmorBonusRemoved ? "TAK" : "NIE")
                     + ": pancerz ofiary / (0.3 + max(0.7, stamina / maxStamina)) przed prefiksem RBM - premia x1.0-1.3 znika; postura i stamina RBM "
                     + (_rbmStaminaOn ? "wlaczone" : "WYLACZONE (premii nie ma - nic do zdjecia)") + " | progi RBM w pamieci: " + Thresholds() + ".");
            return _note;
        }

        /// <summary>Stan RBM czytany poza goraca sciezka: przy zakladaniu i na starcie kazdej misji (RBM zdejmuje i zaklada latki oraz czyta
        /// config.xml przy starcie gry i po swoim ekranie ustawien - nie w trakcie bitwy).</summary>
        private static void Refresh()
        {
            bool on = false;
            try
            {
                var info = _blow != null ? Harmony.GetPatchInfo(_blow) : null;
                if (info != null)
                    foreach (var p in info.Prefixes)
                        if (p != null && p.PatchMethod != null && p.PatchMethod.DeclaringType != null
                            && (p.PatchMethod.DeclaringType.FullName ?? "").StartsWith("RBMCombat.", StringComparison.Ordinal)) { on = true; break; }
            }
            catch { }
            _rbmBlow = on;
            bool st = false;
            IDictionary d = null;
            try
            {
                if (_fPosture != null && _fStaminaOn != null) st = (bool)_fPosture.GetValue(null) && (bool)_fStaminaOn.GetValue(null);
                if (_fValues != null) d = _fValues.GetValue(null) as IDictionary;
            }
            catch { }
            _rbmStaminaOn = st;
            _stances = d;
        }

        /// <summary>Progi przebicia RBM z pamieci (lista RBMConfig.weaponTypesFactors) - tylko do logu, poza goraca sciezka.</summary>
        internal static string Thresholds(bool withTarget = true)
        {
            try
            {
                var list = _fFactors != null ? _fFactors.GetValue(null) as IEnumerable : null;
                if (list == null) return "brak listy";
                string a = "?", b = "?", j = "?";
                foreach (var o in list)
                {
                    if (o == null) continue;
                    var t = o.GetType();
                    string name = AccessTools.Field(t, "weaponType")?.GetValue(o) as string;
                    if (name == "Arrow") a = Num(o, t, "ExtraArmorThresholdFactorCut");
                    else if (name == "Bolt") b = Num(o, t, "ExtraArmorThresholdFactorCut");
                    else if (name == "Javelin") j = Num(o, t, "ExtraArmorThresholdFactorPierce");
                }
                if (!withTarget) return "Arrow ciecie " + a + ", Bolt ciecie " + b + ", Javelin klucie " + j + " (fabryczne RBM 2.60 / 2.60 / 3.00)";   // 423: linia startu drabiny
                return "Arrow ciecie " + a + " (cel 2.00), Bolt ciecie " + b + " (cel 2.00), Javelin klucie " + j + " (cel 2.00)";
            }
            catch (Exception e) { return "odczyt nieudany (" + e.GetType().Name + ")"; }
        }

        private static string Num(object o, Type t, string field)
        {
            var f = AccessTools.Field(t, field);
            return f != null && f.FieldType == typeof(float) ? F((float)f.GetValue(o)) : "?";
        }

        private static string F(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        private static string F1(float v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }

        private static bool Owned(MethodBase mi, string id)
        {
            try
            {
                var info = Harmony.GetPatchInfo(mi);
                if (info == null) return false;
                foreach (var p in info.Prefixes)
                    if (p != null && p.owner == id && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(RestedArmor416)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>Przed prefiksem RBM na ComputeBlowDamage. Warunki premii jak w RBM: postura i stamina wlaczone, ofiara ma postawe. Bez alokacji
        /// (indeksator IDictionary na Dictionary - samo wyszukanie; pola przez FieldRef). Liczniki rosna takze przy kluczu NIE (pomiar "przed").
        /// Bez try/catch: same odczyty pol i wyszukanie w slowniku (glowny watek, ten sam slownik RBM czyta chwile pozniej w swoim prefiksie).</summary>
        private static void RestedPrefix(ref AttackInformation attackInformation, ref AttackCollisionData attackCollisionData)
        {
            if (!_rbmBlow || !_rbmStaminaOn) return;
            var d = _stances;
            var v = attackInformation.VictimAgent;
            if (d == null || v == null || attackCollisionData.IsFallDamage) return;
            object st = d[v];                                                                // Dictionary: klucz nie null = samo wyszukanie, brak wpisu = null
            if (st == null) return;
            float max = _rMaxStamina(st);
            if (!(max > 0f)) return;
            float ratio = _rStamina(st) / max;
            _blows++;
            _lastVictim = v;
            _lastRested = ratio > RestedFloor;
            if (!_lastRested) return;                                                        // do 70% paska RBM mnozy x1.0 - nic do zdjecia
            float f = RestedBase + ratio;                                                    // = 0.3 + max(0.7, ratio)
            if (!(f > 1f) || f > 2f) return;                                                 // NaN albo stamina ponad pule - nie ruszamy
            _rested++; _factorSum += f;
            var s = Settings.Current;
            if (s == null || !s.RestedArmorBonusRemoved) return;
            float a = attackInformation.ArmorAmountFloat;
            if (!(a > 0f)) return;
            float b = a / f;
            attackInformation.ArmorAmountFloat = b;
            _changed++; _armBefore += a * f; _armAfter += a;
        }

        private static void Reset()
        {
            _blows = 0; _rested = 0; _changed = 0;
            _factorSum = 0f; _armBefore = 0f; _armAfter = 0f;
            _lastVictim = null; _lastRested = false;
            _hRested = 0; _hTired = 0; _hRestedHeavy = 0; _hTiredHeavy = 0;
            _hpRested = 0f; _hpTired = 0f;
        }

        private static string Avg(float sum, int n) { return n > 0 ? F1(sum / n) : "-"; }

        private static void Flush(Mission m)
        {
            if (_blows == 0) { Reset(); return; }                                            // misja bez ciosow w ludzi z postawa RBM - bez linii
            var s = Settings.Current;
            string kind = "?";
            try { kind = MissionTeams.Kind(m); } catch { }
            Log.Info("Wypoczety (416): koniec misji (" + kind + ") | klucz " + (s != null && s.RestedArmorBonusRemoved ? "TAK" : "NIE")
                     + " | latka " + _patched + "/" + _wanted + (_rbmBlow ? "" : " (prefiks RBM na ComputeBlowDamage NIEOBECNY - nieczynne)")
                     + " | ciosy w cele z postawa RBM " + _blows + ", w wypoczetych (pasek > 70%) " + _rested
                     + " (sredni mnoznik RBM x" + (_rested > 0 ? F(_factorSum / _rested) : "-") + "), premia zdjeta " + _changed
                     + " (pancerz po RBM srednio " + Avg(_armBefore, _changed) + " -> " + Avg(_armAfter, _changed) + ")"
                     + " | TRAFIENIA (nie zablokowane): w wypoczetych " + _hRested + " srednio " + Avg(_hpRested, _hRested) + " HP, od 60 HP " + _hRestedHeavy
                     + "; w zmeczonych (pasek do 70%) " + _hTired + " srednio " + Avg(_hpTired, _hTired) + " HP, od 60 HP " + _hTiredHeavy
                     + " | progi RBM w pamieci: " + Thresholds() + ".");
            Reset();
        }

        /// <summary>Liczniki misji: zerowanie i odczyt stanu RBM na starcie, trafienia wedlug stanu paska ofiary, linia sumy na koncu.
        /// Dziala takze przy kluczu NIE - pomiar "przed".</summary>
        internal sealed class Logic : MissionLogic
        {
            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            public override void OnBehaviorInitialize()
            {
                base.OnBehaviorInitialize();
                Reset();
                Refresh();
            }

            public override void OnScoreHit(Agent affectedAgent, Agent affectorAgent, WeaponComponentData attackerWeapon, bool isBlocked, bool isSiegeEngineHit,
                in Blow blow, in AttackCollisionData collisionData, float damagedHp, float hitDistance, float shotDifficulty)
            {
                // bez alokacji i bez wywolan silnika; ofiara = ta, ktora prefiks policzyl przy tym ciosie (znacznik zdejmowany przy kazdym trafieniu)
                var last = _lastVictim;
                bool rested = _lastRested;
                _lastVictim = null;
                if (last == null || isBlocked || !ReferenceEquals(last, affectedAgent) || collisionData.IsFallDamage) return;
                if (rested) { _hRested++; _hpRested += damagedHp; if (damagedHp >= 60f) _hRestedHeavy++; }
                else { _hTired++; _hpTired += damagedHp; if (damagedHp >= 60f) _hTiredHeavy++; }
            }

            protected override void OnEndMission()
            {
                try { Flush(Mission); } catch (Exception e) { Log.Error("RestedArmor416.Flush", e); }
                base.OnEndMission();
            }
        }
    }
}
