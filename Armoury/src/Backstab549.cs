using System;
using System.Globalization;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 549 (Jeff 15.11): CIOS W PLECY - cios wrecz czlowieka w czlowieka z tylu zadaje x BackstabDamageMult (2.0) obrazen i pomija
    /// BackstabArmourPen (25%) pancerza ofiary. Jedna regula dla wszystkich: gracz bije AI, AI bije gracza, AI bije AI, pieszo i konno.
    /// Pociski poza regula (Jeff napisal "cios"; pociski z tylu i tak pada masowo na uciekajacych).
    ///
    /// Cel: MissionCombatMechanicsHelper.ComputeBlowDamage (prywatna statyczna, zadeklarowana w tej klasie - DeclaredMethod), ten sam MethodInfo co 415 / 416 / 417 / 423b.
    ///  - prefiks Priority.First (przed RBM OverrideDamageCalc.Prefix, ktory czyta ArmorAmountFloat na wejsciu): ArmorAmountFloat x (1 - pen/100);
    ///    bez RBM dziala tak samo - oryginal gry czyta to samo pole;
    ///  - postfiks Priority.Last (biegnie takze, gdy prefiks RBM zwrocil false): gotowe inflictedDamage x mnoznik (NIE magnitude - RBM odejmuje
    ///    pancerz nieliniowo, x2 sily dalby x3-x5 obrazen); absorbedByArmor = max(0, surowe - po), suma stala dopoki po <= surowe.
    ///
    /// "Z tylu": d = napastnik - ofiara (plasko), cos = dot(d, kierunek tulowia ofiary) / |d|; tyl gdy cos <= BannerCompanies.RearCos (-0.5),
    /// czyli napastnik ponad 120 st. od kierunku tulowia. UWAGA: celowo VictimAgentMovementDirection (= GetMovementDirection, TULOW), a nie
    /// LookDirection jak w 448 - gracz rozgladajacy sie Altem obraca glowe, nie tulow. Nie "ujednolicac" z 448.
    ///
    /// Pominiete: pociski, upadek, szarza konia, ciosy alternatywne (kopniak, tarcza), cios na tarcze, swoj w swojego (tylny szereg wloczni),
    /// tarcza na plecach (RBM / Perk506 D9), lezacy w akcji Fall (zjawisko 415), sneak attack gry (RBM instakill).
    ///
    /// Watki: OnHit bywa z dowolnego watku (283), wiec znacznik [ThreadStatic], liczniki Interlocked, wyjatek tylko zapisany (volatile)
    /// i zgloszony RoutHold.Stumble na glownym watku na koncu misji. Bez globalnego wylacznika - wyjatek liczony, nastepny cios liczony dalej.
    /// Latki w kampanii (NightHooks.InCampaign, raz na proces), nigdy w SubModuleMain.
    /// </summary>
    internal static class Backstab549
    {
        private const float MinDist = 0.05f;

        private static int _patched, _wanted;
        private static string _note = "nie zakladano";
        /// <summary>Choc jedna latka 549 siedzi w procesie - SubModuleMain dodaje wtedy Logic w kazdej misji (jak 415-p).</summary>
        internal static bool Patched { get { return _patched > 0; } }

        [ThreadStatic] private static bool _mark;           // prefiks uznal cios za cios w plecy - postfiks mnozy obrazenia

        // liczniki misji (Interlocked; sumy jako long)
        private static int _melee, _rear, _rearOnPlayer, _rearByPlayer, _skipFriend, _skipBackShield, _skipProne, _skipSneak, _stum, _armN;
        private static long _hpB, _hpA, _armB10, _armA10;     // pancerz x10 (zaokraglony)
        private static volatile Exception _lastErr;

        /// <summary>NightHooks.InCampaign (raz na proces). Zwraca opis do linii "Latki nocy (K3): kampania".</summary>
        internal static string ApplyInCampaign(Harmony h)
        {
            if (h == null) { _note = "brak Harmony"; return _note; }
            _patched = 0; _wanted = 2;
            string miss = "";
            MethodInfo blow = null;
            try { blow = AccessTools.DeclaredMethod(typeof(MissionCombatMechanicsHelper), "ComputeBlowDamage"); } catch { }
            if (blow == null) miss += " brak MissionCombatMechanicsHelper.ComputeBlowDamage;";
            else
            {
                try
                {
                    h.Patch(blow, prefix: new HarmonyMethod(typeof(Backstab549), nameof(BackPrefix)) { priority = Priority.First },
                                  postfix: new HarmonyMethod(typeof(Backstab549), nameof(BackPostfix)) { priority = Priority.Last });
                    if (Owned(blow, h.Id, true)) _patched++; else miss += " prefiks NIE wszedl;";
                    if (Owned(blow, h.Id, false)) _patched++; else miss += " postfiks NIE wszedl;";
                }
                catch (Exception e) { miss += " BLAD " + e.GetType().Name + ";"; Log.Error("Backstab549.Patch", e); }
            }
            _note = (_patched == _wanted ? "wpiete " : "NIE wpiete: ") + _patched + "/" + _wanted + (miss.Length > 0 ? " |" + miss : "");
            var s = Settings.Current;
            Log.Info("Cios z tylu (549): latka raz na proces - " + _note + ". Klucz BackstabEnabled " + (s != null && s.BackstabEnabled ? "TAK" : "NIE")
                     + ", obrazenia x" + F(Mult(s)) + ", przebicie " + ((int)Pen(s)) + "% (cios wrecz czlowiek-czlowiek, napastnik ponad 120 st. od tulowia ofiary).");
            return _note;
        }

        private static bool Owned(MethodBase mi, string id, bool prefix)
        {
            try
            {
                var info = Harmony.GetPatchInfo(mi);
                if (info == null) return false;
                foreach (var p in prefix ? info.Prefixes : info.Postfixes)
                    if (p != null && p.owner == id && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(Backstab549)) return true;
            }
            catch { }
            return false;
        }

        private static float Mult(Settings s)
        {
            float m = s != null ? s.BackstabDamageMult : 2f;
            if (!(m >= 1f)) m = 1f; else if (m > 3f) m = 3f;
            return m;
        }

        private static float Pen(Settings s)
        {
            float p = s != null ? s.BackstabArmourPen : 25f;
            if (!(p >= 0f)) p = 0f; else if (p > 75f) p = 75f;
            return p;
        }

        private static string F(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        private static string F1(double v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }

        /// <summary>Przed RBM. Zmienia tylko ArmorAmountFloat (struktura budowana na jedno trafienie - jak 415 / 416 / 423b).</summary>
        private static void BackPrefix(ref AttackInformation attackInformation, ref AttackCollisionData attackCollisionData, WeaponComponentData attackerWeapon)
        {
            _mark = false;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.BackstabEnabled) return;
                if (attackCollisionData.IsMissile || attackCollisionData.IsFallDamage || attackCollisionData.IsHorseCharge
                    || attackCollisionData.IsAlternativeAttack || attackCollisionData.AttackBlockedWithShield) return;
                if (attackerWeapon == null) return;
                var v = attackInformation.VictimAgent;
                if (v == null || attackInformation.AttackerAgent == null) return;
                if (!attackInformation.IsVictimAgentHuman || !attackInformation.IsAttackerAgentHuman || attackInformation.IsVictimAgentSameWithAttackerAgent) return;
                Interlocked.Increment(ref _melee);
                if (attackInformation.IsFriendlyFire) { Interlocked.Increment(ref _skipFriend); return; }
                if (attackCollisionData.CollidedWithShieldOnBack) { Interlocked.Increment(ref _skipBackShield); return; }

                // test tylu - tani rachunek przed jedynym wolaniem silnika; tulow (MovementDirection), nie glowa (LookDirection) - patrz opis klasy
                Vec2 d = (attackInformation.AttackerAgentPosition - attackInformation.VictimAgentPosition).AsVec2;
                float dl = d.Length;
                if (!(dl >= MinDist)) return;
                Vec2 dir = attackInformation.VictimAgentMovementDirection;
                float fl = dir.Length;
                if (!(fl > 0.001f)) return;
                float cos = (d.x * dir.x + d.y * dir.y) / (dl * fl);
                if (!(cos <= BannerCompanies.RearCos)) return;

                if (v.GetCurrentActionType(0) == Agent.ActionCodeType.Fall) { Interlocked.Increment(ref _skipProne); return; }   // lezacy = 415

                float pen = Pen(s);
                float a = attackInformation.ArmorAmountFloat;
                if (a > 0f && pen > 0f)
                {
                    float b = a * (1f - pen / 100f);
                    attackInformation.ArmorAmountFloat = b;
                    Interlocked.Increment(ref _armN);
                    Interlocked.Add(ref _armB10, (long)(a * 10f + 0.5f));
                    Interlocked.Add(ref _armA10, (long)(b * 10f + 0.5f));
                }
                _mark = true;
            }
            catch (Exception e) { _mark = false; Interlocked.Increment(ref _stum); _lastErr = e; }
        }

        /// <summary>Po RBM i po grze (Priority.Last). Mnozy gotowe obrazenia ciosu oznaczonego przez prefiks.</summary>
        private static void BackPostfix(ref AttackInformation attackInformation, ref int inflictedDamage, ref int absorbedByArmor, bool isSneakAttack)
        {
            if (!_mark) return;
            _mark = false;
            try
            {
                if (isSneakAttack) { Interlocked.Increment(ref _skipSneak); return; }   // kryjowka: RBM instakill 200
                float m = Mult(Settings.Current);
                int before = inflictedDamage, raw = before + Math.Max(0, absorbedByArmor);
                int after = Math.Min(2000, (int)(before * m + 0.5f));
                inflictedDamage = after;
                absorbedByArmor = Math.Max(0, raw - after);
                Interlocked.Increment(ref _rear); Interlocked.Add(ref _hpB, before); Interlocked.Add(ref _hpA, after);
                if (attackInformation.IsVictimPlayer) Interlocked.Increment(ref _rearOnPlayer);
                else if (attackInformation.IsAttackerPlayer) Interlocked.Increment(ref _rearByPlayer);
            }
            catch (Exception e) { Interlocked.Increment(ref _stum); _lastErr = e; }
        }

        private static void Reset()
        {
            Interlocked.Exchange(ref _melee, 0); Interlocked.Exchange(ref _rear, 0); Interlocked.Exchange(ref _rearOnPlayer, 0); Interlocked.Exchange(ref _rearByPlayer, 0);
            Interlocked.Exchange(ref _skipFriend, 0); Interlocked.Exchange(ref _skipBackShield, 0); Interlocked.Exchange(ref _skipProne, 0); Interlocked.Exchange(ref _skipSneak, 0);
            Interlocked.Exchange(ref _stum, 0); Interlocked.Exchange(ref _armN, 0);
            Interlocked.Exchange(ref _hpB, 0L); Interlocked.Exchange(ref _hpA, 0L); Interlocked.Exchange(ref _armB10, 0L); Interlocked.Exchange(ref _armA10, 0L);
            _lastErr = null;
        }

        private static string Avg(long sum, int n, double scale) { return n > 0 ? F1(sum / scale / n) : "-"; }

        private static void Flush(Mission m)
        {
            int melee = Volatile.Read(ref _melee), stum = Volatile.Read(ref _stum);
            var err = _lastErr;
            if (err != null) RoutHold.Stumble(RoutHold.SiteBanner, "Backstab549.Blow (" + stum + " razy w misji)", err);
            if (melee == 0 && stum == 0) { Reset(); return; }          // misja bez ciosow wrecz czlowiek-czlowiek - bez linii
            var s = Settings.Current;
            string kind = "?";
            try { kind = MissionTeams.Kind(m); } catch { }
            int rear = Volatile.Read(ref _rear), armN = Volatile.Read(ref _armN);
            string pct = melee > 0 ? ((int)Math.Round(100.0 * rear / melee)).ToString(CultureInfo.InvariantCulture) : "-";
            Log.Info("Cios z tylu (549): koniec misji (" + kind + ") | klucz " + (s != null && s.BackstabEnabled ? "TAK" : "NIE") + ", x" + F(Mult(s)) + ", przebicie " + ((int)Pen(s)) + "%"
                     + " | ciosy wrecz czlowiek-czlowiek " + melee + ", z tylu " + rear + " (" + pct + "%), obrazenia srednio " + Avg(Interlocked.Read(ref _hpB), rear, 1.0)
                     + " -> " + Avg(Interlocked.Read(ref _hpA), rear, 1.0) + ", pancerz srednio " + Avg(Interlocked.Read(ref _armB10), armN, 10.0) + " -> " + Avg(Interlocked.Read(ref _armA10), armN, 10.0)
                     + " | w gracza z tylu " + Volatile.Read(ref _rearOnPlayer) + ", gracza z tylu " + Volatile.Read(ref _rearByPlayer)
                     + " | pominiete: swoj " + Volatile.Read(ref _skipFriend) + ", tarcza na plecach " + Volatile.Read(ref _skipBackShield) + ", lezacy " + Volatile.Read(ref _skipProne)
                     + ", sneak " + Volatile.Read(ref _skipSneak) + " | potkniecia " + stum + " | latki " + _patched + "/" + _wanted + ".");
            Reset();
        }

        /// <summary>Liczniki misji: zerowanie na starcie, linia sumy i zgloszenie potkniec (glowny watek) na koncu.</summary>
        internal sealed class Logic : MissionLogic
        {
            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            public override void OnBehaviorInitialize()
            {
                base.OnBehaviorInitialize();
                Reset();
            }

            protected override void OnEndMission()
            {
                try { Flush(Mission); } catch (Exception e) { Log.Error("Backstab549.Flush", e); }
                base.OnEndMission();
            }
        }
    }
}
