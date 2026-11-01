using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 313c (01.11, Jeff: "im wiecej mam umiejetnosci walki tym mniej staminy zuzywa"; raport rozp313/stamina-raport.md + krytyka, POPRAWIONA PROPOZYCJA):
    /// wprawa w broni bialej a koszt staminy RBM. RBM liczy koszt przy KOLIZJI wrecz w prywatnych statycznych
    /// RBMAI.StanceLogic+CreateMeleeBlowPatch.calculateAttackerStaminaLoss / calculateDefenderStaminaLoss (5 parametrow: obronca, atakujacy,
    /// ref AttackCollisionData, MeleeHitType [3], isUnarmedAttack [4]; RBMAI.cs:8970, 9041). Trafienie w cialo (atakujacy) - stale 35 / 49 bez skilla,
    /// bloki i parady obu stron - x (1 - s/1000). Regula (gracz, lordowie, szeregowi obu stron jednakowo): M(s) = max(dno, U x (1 - P/100)^(s/100)),
    /// s = skill broni trzymanej w reku liczony jak RBM (GetRelevantSkillFromWeaponClass(WieldedWeapon), takze przy bloku tarcza).
    /// Atakujacy AgentHit: koszt x M; bloki / parady (MeleeHitType 2..7) obu stron: koszt x M / (1 - s/1000) - zastepuje liniowy czynnik RBM (skill nie liczy sie
    /// podwojnie). Bez zmian: przyjety cios w cialo (obronca AgentHit - bol), walka bez broni i pusta reka (RBM liczy z Atletyki), pociski, luk / kusza / rzut
    /// (RBM juz liczy skill w strzale), pula, odnowa, bieg. Kara zbroi RBM (x (1 + kg/50)) mnozy sie dalej po naszym wyniku.
    /// Pomiar (linia "Wprawa (WeaponEase)" na koniec misji) dziala takze przy kluczu NIE - "bylby". Wyjatek: licznik potkniec, wynik RBM nietkniety,
    /// bez globalnego wylacznika.
    /// </summary>
    internal static class WeaponEase
    {
        private static MethodInfo _mAtt, _mDef;
        private static bool _okAtt, _okDef;
        private static string _why = "nie sprawdzane";

        // liczniki misji (glowny watek - CreateMeleeBlow); misja zmieniona -> zero (misja bez FieldCraft nie pisze linii)
        private static Mission _mis;
        private static int _nAtt, _nDef, _stumbles, _stumblesSession;
        private static double _sumRbm, _sumRule;
        private static readonly int[] _bN = new int[4];
        private static readonly double[] _bS = new double[4], _bM = new double[4], _bRbm = new double[4], _bRule = new double[4];
        private static int _plN, _plSkillLast = -1;
        private static double _plS, _plM, _plRbm, _plRule;

        private static bool HitTypeEnum(Type t) { return t != null && t.IsEnum && t.Name == "MeleeHitType"; }

        private static bool Sig(MethodInfo mi)
        {
            if (mi == null || !mi.IsStatic || mi.ReturnType != typeof(float)) return false;
            var p = mi.GetParameters();
            return p.Length == 5 && p[0].ParameterType == typeof(Agent) && p[1].ParameterType == typeof(Agent)
                   && p[2].ParameterType.IsByRef && p[2].ParameterType.GetElementType() == typeof(AttackCollisionData)
                   && HitTypeEnum(p[3].ParameterType) && p[4].ParameterType == typeof(bool);
        }

        private static MethodInfo FindDeclared(Type t, string name)
        {
            if (t == null) return null;
            MethodInfo first = null;
            foreach (var mi in t.GetMethods(AccessTools.allDeclared))
            {
                if (mi == null || mi.Name != name) continue;
                if (Sig(mi)) return mi;
                if (first == null) first = mi;
            }
            return first;
        }

        private static bool Owned(MethodBase target, string id)
        {
            try
            {
                var info = Harmony.GetPatchInfo(target);
                if (info == null) return false;
                foreach (var p in info.Postfixes) if (p != null && p.owner == id) return true;
            }
            catch { }
            return false;
        }

        /// <summary>Wpiecie w menu glownym (SubModuleMain obok DuelGuard). Postfiksy zawsze, gdy RBM obecny - pomiar takze przy kluczu NIE.</summary>
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var tLogic = QuartermasterLaw.FindType("RBMAI.StanceLogic");
                var inner = tLogic != null ? AccessTools.Inner(tLogic, "CreateMeleeBlowPatch") : null;
                if (tLogic == null) _why = "RBM nieobecny";
                else if (inner == null) _why = "brak StanceLogic+CreateMeleeBlowPatch";
                else
                {
                    _mAtt = FindDeclared(inner, "calculateAttackerStaminaLoss");
                    _mDef = FindDeclared(inner, "calculateDefenderStaminaLoss");
                    if (!Sig(_mAtt) || !Sig(_mDef)) _why = "sygnatura calculate*StaminaLoss nie pasuje (5 parametrow, [3] MeleeHitType, [4] bool)";
                    else
                    {
                        try
                        {
                            h.Patch(_mAtt, postfix: new HarmonyMethod(typeof(WeaponEase), nameof(AttPostfix)));
                            _okAtt = Owned(_mAtt, h.Id);
                        }
                        catch (Exception e) { Log.Error("WeaponEase.ApplyAll atakujacy", e); }
                        try
                        {
                            h.Patch(_mDef, postfix: new HarmonyMethod(typeof(WeaponEase), nameof(DefPostfix)));
                            _okDef = Owned(_mDef, h.Id);
                        }
                        catch (Exception e) { Log.Error("WeaponEase.ApplyAll obronca", e); }
                        _why = "sygnatura OK";
                    }
                }
            }
            catch (Exception e) { _why = "wyjatek " + e.GetType().Name; Log.Error("WeaponEase.ApplyAll", e); }
            finally
            {
                var c = Settings.Current;
                Log.Info("Wprawa (WeaponEase): latki atakujacy/obronca wpiete " + (_okAtt ? "TAK" : "NIE") + "/" + (_okDef ? "TAK" : "NIE") + " (" + _why + ")"
                         + " | klucz Weapon Skill Eases Stamina " + (c != null && c.WeaponSkillEasesStamina ? "TAK" : "NIE (tylko pomiar: bylby)")
                         + " | regula " + RuleText(c) + " | bron biala: trafienie atakujacego x M, bloki i parady obu stron x M zamiast (1 - s/1000) RBM;"
                         + " bez zmian: przyjety cios, bez broni, pociski, luk / kusza / rzut (RBM liczy skill w strzale)");
            }
        }

        private static string RuleText(Settings c)
        {
            if (c == null) return "-";
            return "U " + Untrained(c).ToString("0.00", CultureInfo.InvariantCulture) + ", -" + SavingPct(c).ToString("0.#", CultureInfo.InvariantCulture)
                   + "%/100 pkt, dno " + Floor(c).ToString("0.00", CultureInfo.InvariantCulture);
        }

        // zakresy jak RANGES w tools/gen_mcm.py
        private static float Untrained(Settings c) { return Math.Max(1f, Math.Min(1.5f, c.WeaponSkillStaminaUntrained)); }
        private static float SavingPct(Settings c) { return Math.Max(0f, Math.Min(40f, c.WeaponSkillStaminaSavingPer100)); }
        private static float Floor(Settings c) { return Math.Max(0.25f, Math.Min(1f, c.WeaponSkillStaminaFloor)); }

        /// <summary>M(s) = max(dno, U x (1 - P/100)^(s/100)).</summary>
        internal static float M(int s, Settings c)
        {
            double m = Untrained(c) * Math.Pow(1.0 - SavingPct(c) / 100.0, Math.Max(0, s) / 100.0);
            return (float)Math.Max(Floor(c), m);
        }

        private static void MissionCheck()
        {
            var m = Mission.Current;
            if (ReferenceEquals(m, _mis)) return;
            _mis = m;
            Reset();
        }

        internal static void Reset()
        {
            _nAtt = _nDef = _stumbles = 0;
            _sumRbm = _sumRule = 0;
            for (int i = 0; i < 4; i++) { _bN[i] = 0; _bS[i] = _bM[i] = _bRbm[i] = _bRule[i] = 0; }
            _plN = 0; _plSkillLast = -1;
            _plS = _plM = _plRbm = _plRule = 0;
        }

        /// <summary>Skill broni w reku jak RBM; -1 = pusta reka / bez uzycia (RBM liczy wtedy Atletyke - nie ruszamy).</summary>
        private static int WeaponSkill(Agent a)
        {
            if (a == null) return -1;
            var w = a.WieldedWeapon;
            if (w.IsEmpty || w.CurrentUsageItem == null) return -1;
            var sk = WeaponComponentData.GetRelevantSkillFromWeaponClass(w.CurrentUsageItem.WeaponClass);
            if (sk == null) return -1;
            return MissionGameModels.Current.AgentStatCalculateModel.GetEffectiveSkill(a, sk);
        }

        private static void Note(Agent a, int s, float m, float rbm, float rule, bool att)
        {
            if (att) _nAtt++; else _nDef++;
            _sumRbm += rbm; _sumRule += rule;
            int b = s < 100 ? 0 : s < 200 ? 1 : s < 300 ? 2 : 3;
            _bN[b]++; _bS[b] += s; _bM[b] += m; _bRbm[b] += rbm; _bRule[b] += rule;
            if (a != null && a.IsMainAgent)
            {
                _plN++; _plS += s; _plM += m; _plRbm += rbm; _plRule += rule; _plSkillLast = s;
            }
        }

        private static void Stumble(Exception e)
        {
            _stumbles++; _stumblesSession++;
            if (_stumblesSession <= 3) Log.Error("WeaponEase (potkniecie " + _stumblesSession + ")", e);
        }

        // atakujacy: __args = (defenderAgent, attackerAgent, ref collisionData, meleeHitType, isUnarmedAttack)
        private static void AttPostfix(ref float __result, object[] __args)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || __args == null || __args.Length < 5) return;
                MissionCheck();
                if ((bool)__args[4]) return;                        // bez broni - RBM z Atletyki
                var a = __args[1] as Agent;
                int ht = Convert.ToInt32(__args[3], CultureInfo.InvariantCulture);
                if (ht < 1 || ht > 7) return;                       // None / inne - bez zmian
                int s = WeaponSkill(a);
                if (s < 0) return;                                  // pusta reka - RBM z Atletyki
                float m = M(s, c);
                float rbm = __result, rule;
                if (ht == 1) rule = rbm * m;                        // trafienie w cialo: 35 / 49 bez skilla w RBM
                else
                {
                    float lin = 1f - s * 0.001f;                    // RBM: 1 - s/1000 (bez max)
                    if (lin < 0.1f) return;
                    rule = rbm * m / lin;
                }
                Note(a, s, m, rbm, rule, true);
                if (c.WeaponSkillEasesStamina) __result = rule;
            }
            catch (Exception e) { Stumble(e); }
        }

        // obronca: przyjety cios w cialo (AgentHit) - bol, bez zmian
        private static void DefPostfix(ref float __result, object[] __args)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || __args == null || __args.Length < 5) return;
                MissionCheck();
                if ((bool)__args[4]) return;
                var a = __args[0] as Agent;
                int ht = Convert.ToInt32(__args[3], CultureInfo.InvariantCulture);
                if (ht < 2 || ht > 7) return;
                int s = WeaponSkill(a);
                if (s < 0) return;
                float lin = Math.Max(0f, 1f - s * 0.001f);          // RBM obronca: max(0, 1 - s/1000)
                if (lin < 0.1f) return;
                float m = M(s, c);
                float rbm = __result, rule = rbm * m / lin;
                Note(a, s, m, rbm, rule, false);
                if (c.WeaponSkillEasesStamina) __result = rule;
            }
            catch (Exception e) { Stumble(e); }
        }

        private static string F(double v, string fmt) { return v.ToString(fmt, CultureInfo.InvariantCulture); }

        /// <summary>Linia "Wprawa (WeaponEase)" raz na koniec misji (FieldCraft.OnEndMission), potem zero. Bez kolizji w tej misji - nic.</summary>
        internal static void Flush(Mission m)
        {
            try
            {
                if (!ReferenceEquals(m, _mis) || _nAtt + _nDef == 0) { if (ReferenceEquals(m, _mis)) Reset(); return; }
                var c = Settings.Current;
                bool on = c != null && c.WeaponSkillEasesStamina;
                var sb = new StringBuilder(512);
                sb.Append("Wprawa (WeaponEase): misja | klucz ").Append(on ? "TAK" : "NIE (pomiar: bylby)")
                  .Append(", latki ").Append(_okAtt && _mAtt != null && Owned(_mAtt, VeteranAi.HarmonyId) ? "TAK" : "NIE").Append("/")
                  .Append(_okDef && _mDef != null && Owned(_mDef, VeteranAi.HarmonyId) ? "TAK" : "NIE")
                  .Append(" | regula ").Append(RuleText(c))
                  .Append(" | kolizji: atak ").Append(_nAtt).Append(", obrona ").Append(_nDef)
                  .Append(" | koszt RBM bez zbroi ").Append(F(_sumRbm, "0")).Append(on ? " -> po regule " : " -> bylby ").Append(F(_sumRule, "0"))
                  .Append(" (x").Append(F(_sumRbm > 0.01 ? _sumRule / _sumRbm : 1.0, "0.00")).Append(")")
                  .Append(" | koszyki skilla broni 0-99/100-199/200-299/300+:");
                string[] names = { "0-99", "100-199", "200-299", "300+" };
                for (int i = 0; i < 4; i++)
                {
                    sb.Append(i == 0 ? " " : "; ").Append(names[i]).Append(" n ").Append(_bN[i]);
                    if (_bN[i] > 0)
                        sb.Append(", sr. skill ").Append(F(_bS[i] / _bN[i], "0")).Append(", sr. M ").Append(F(_bM[i] / _bN[i], "0.00"))
                          .Append(", koszt x").Append(F(_bRbm[i] > 0.01 ? _bRule[i] / _bRbm[i] : 1.0, "0.00"));
                }
                sb.Append(" | gracz: ");
                if (_plN > 0)
                    sb.Append("kolizji ").Append(_plN).Append(", sr. skill broni ").Append(F(_plS / _plN, "0")).Append(" (ostatni ").Append(_plSkillLast)
                      .Append("), sr. M ").Append(F(_plM / _plN, "0.00")).Append(", koszt ").Append(F(_plRbm, "0")).Append(" -> ").Append(F(_plRule, "0"))
                      .Append(" (").Append(on ? "oszczedzone " : "bylby oszczedzone ").Append(F(_plRbm - _plRule, "0")).Append(" pkt)");
                else sb.Append("-");
                sb.Append(" | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumblesSession).Append(")");
                Log.Info(sb.ToString());
                Reset();
            }
            catch (Exception e) { Log.Error("WeaponEase.Flush", e); }
        }
    }
}
