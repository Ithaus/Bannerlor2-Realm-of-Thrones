using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P6 "friendly fire" (projekt noc/projekty/friendly-fire.txt; klucze FriendlyFireEnabled, FriendlyFireRangedPercent, FriendlyFireMeleePercent,
    /// FriendlyFireChargePercent). Jeff 25.10: "i dodaj friendly fire po obu stronach", "cios wrecz nie tylko ostrzaly".
    /// Pociski, wrecz i szarza konia rania swoich - jedna regula gracz/AI; pole, oblezenie, wypad, kryjowka, morze (nie turniej, arena, bojki w miescie).
    ///
    /// GRA DZIS (SP, dekompilacja Mission.cs 1.4.8, md5 TaleWorlds.MountAndBlade.dll = libs):
    ///  - wrecz: MeleeHitCallback -> CancelsDamageAndBlocksAttackBecauseOfNonEnemyCase (w SP zawsze prawda dla swojego) = cios anulowany, atakujacy ogluszony 0.4 s;
    ///    kon swojego: anulowany tylko, gdy natywne attacker.IsFriendOf(kon) da prawde (galaz num && flag zwraca true bez IsHuman, Mission.cs:6297-6311) -
    ///    czy silnik uznaje konia (bez SetTeam) za swojego, z C# nie wiadomo;
    ///  - pociski: MissileHitCallback - gracz rani swoich w pelni, AI w swojego i w konia swojego anulowane (porownanie attacker.Controller == AI, 2 miejsca);
    ///    pocisk z fizyka (MissileWithPhysics) i obszar (MissileAreaDamageCallback) razi zawsze;
    ///  - szarza: ChargeDamageCallback wychodzi przy celu nie-wrogu (IsFriendlyFireAllowedForChargeDamage bez serwera MP = false).
    /// NASZE (latki wpiete raz na proces w SubModuleMain obok ChargeTemperPatch, przed JIT wolajacych - tylko przy kluczu FriendlyFireEnabled wlaczonym
    /// przy starcie gry (plik MCM Armoury.json, bez wpisu Settings.Current); wylaczony przy starcie = zadna latka, kod trafien gry nietkniety;
    /// wylaczenie w sesji zamyka bramke od nastepnej misji, IL zostaje do ponownego uruchomienia gry):
    ///  (1) postfiks AgentApplyDamageModel.CalculateDamage (klasa bazowa, niewirtualna - jedyny wolajacy GetAttackCollisionResults): trafienie swojego
    ///      (IsFriendlyFire, a dla szarzy i pociskow tez ta sama strona przez jezdzca) x procent z klucza (wrecz / pociski / szarza);
    ///  (2) prefiks + finalizer MissileHitCallback: [ThreadStatic] znacznik "jestesmy w trafieniu pocisku" i "pocisk machiny" (Missile.MissionObjectToIgnore,
    ///      jak isSiegeEngineHit w Mission.OnAgentHit) - machiny i obszar (garnki ognia) zostaja przy zasadzie gry (v1);
    ///  (3) transpilery (cele AccessTools.DeclaredMethod(typeof(Mission), ...), kontrola liczby dopasowan, inna liczba = galaz zdjeta i linia startu):
    ///      a) MeleeHitCallback: po call CancelsDamage... wynik przez Melee(bool, Mission, attacker, victim);
    ///      b) MissileHitCallback: po kazdym z 2 callvirt Agent::get_Controller wynik przez MissileCtl(ctl, Mission, attacker, victim, miejsce);
    ///      c) ChargeDamageCallback: po call IsFriendlyFireAllowedForChargeDamage() wynik przez Charge(bool, Mission, attacker, victim).
    ///      Transpilery wchodza tylko, gdy wszedl postfiks skali (1) - bez niego swoi dostawaliby pelne obrazenia; pociski dodatkowo wymagaja (2).
    /// Bramka misji (Logic.EarlyStart, raz): klucz, kampania, MapEvent.PlayerMapEvent, CombatType Combat, tryb nie turniej / pojedynek, !ForceNoFriendlyFire;
    /// w kazdym trafieniu jeszcze raz ForceNoFriendlyFire i tryb (zasadzka kryjowki po rozstrzygnieciu, pojedynek z hersztem). "Swoj" = obie druzyny wazne,
    /// ta sama strona (Side != None) i IsFriendOf; neutralni anulowani jak w grze. Procent 0 = zasada gry dla wszystkich (wrecz i szarza: cios bez obrazen;
    /// pociski: takze strzaly gracza w swoich anulowane).
    /// Watki: callbacki trafien to MBCallback(..., false) (watek glowny), ale i tak tylko statyki / [ThreadStatic], liczniki Interlocked, bez I/O
    /// i bez Settings.Current (kopia do statykow w EarlyStart). Wyjatek w pomocniczej zwraca wartosc gry i jest liczony - bez globalnego wylacznika.
    /// Linie: start "FriendlyFire: ..." (raz na proces), "FF (rodzaj): ..." raz na bitwe gracza (OnEndMission / OnRemoveBehavior / nastepna misja).
    /// </summary>
    internal static class FriendlyFire
    {
        // ------------------------------------------------------------ stan latek (proces)
        private static bool _dmgOk, _preOk, _meleeOk, _missOk, _chargeOk;
        private static int _meleeMatch = -1, _missMatch = -1, _chargeMatch = -1;   // dopasowania celu w ostatnim przebiegu transpilera
        private static bool _meleeShape, _missShape, _chargeShape;                 // ksztalt IL zgodny z dekompilacja (ldarg przed get_Controller, brak etykiety za wstawka)
        private static bool _offAtStart;                                           // klucz wylaczony przy starcie gry - nic nie wpiete (wlaczenie po restarcie)

        // ------------------------------------------------------------ bramka misji
        private static volatile bool _active;          // bramka otwarta w misji _mref
        private static WeakReference _mref;            // misja ostatniego Begin
        private static volatile int _rangedPct = 100, _meleePct = 50, _chargePct = 50;
        private static string _kind = "-";
        private static string _why;                    // dlaczego bramka zamknieta (null = otwarta)
        private static bool _inBattle;                 // bitwa gracza na mapie (linia FF takze przy zamknietej bramce)
        private static bool _written = true;           // linia tej misji juz w logu (start: nic do zapisu)
        private static bool _wasActive;

        [ThreadStatic] private static int _missileCtx;   // 0 poza MissileHitCallback, 1 zwykly pocisk, 2 pocisk machiny (zasada gry)

        // ------------------------------------------------------------ liczniki misji (Interlocked)
        private static int _meleeAi, _meleePl, _missAi, _missPl, _chargeAi, _chargePl;   // trafienia swojego ze skala (postfiks)
        private static int _openMelee, _openMissAi, _openCharge, _cancelMiss;            // bramki gry otwarte przez nas / pociski anulowane przy 0%
        private static int _siegeCancel, _siegeHit;                                      // machiny: AI w swojego anulowane (gra), trafienia swojego bez skali
        private static long _hpBefore10, _hpAfter10;                                     // HP od swoich x10 przed / po skali
        private static int _humanHits, _ffHumanHits, _heroHits, _playerHits;
        private static int _killedAtt, _koAtt, _killedDef, _koDef, _heroDown;
        private static int _stum, _stumProcess;
        private static string _stumWhere;

        // ------------------------------------------------------------ wpiecie (SubModuleMain.OnBeforeInitialModuleScreenSetAsRoot)

        internal static void ApplyAll(Harmony h)
        {
            if (h == null) { Log.Info("FriendlyFire: NIE wpiete - brak instancji Harmony."); return; }
            // recenzja P6: klucz wylaczony przy starcie gry = zadna latka (transpilery, prefiks, finalizer, postfiks) - Jeff wylacza w MCM i po restarcie
            // kod trafien gry jest caly; MCM wchodzi do Settings dopiero w sesji, wiec tu plik MCM (jak MapSpeedUi.ButtonsAtStart)
            string src;
            if (!EnabledAtStart(out src))
            {
                _offAtStart = true;
                Log.Info("FriendlyFire: NIE wpiete - klucz Friendly Fire wylaczony przy starcie gry (" + src + "); kod trafien gry bez zmian,"
                         + " wlaczenie w MCM dziala po ponownym uruchomieniu gry.");
                return;
            }
            int miss = 0;
            var parts = new StringBuilder(300);

            // (1) skala obrazen - bez niej zadna galaz nie wchodzi
            var dmgPost = Own(nameof(DamagePost));
            MethodInfo dmg = null;
            try
            {
                dmg = AccessTools.DeclaredMethod(typeof(AgentApplyDamageModel), "CalculateDamage");
                if (dmg != null && dmgPost != null)
                {
                    h.Patch(dmg, postfix: new HarmonyMethod(dmgPost));
                    _dmgOk = Has(dmg, h.Id, dmgPost, 'o');
                }
            }
            catch (Exception e) { _dmgOk = false; Log.Error("FriendlyFire.ApplyAll (obrazenia)", e); }
            parts.Append("obrazenia ").Append(_dmgOk ? "1/1" : (dmg == null ? "NIE (brak CalculateDamage)" : "NIE"));
            if (!_dmgOk) miss++;

            // (2) znacznik pocisku / machiny
            var pre = Own(nameof(MissilePre));
            var fin = Own(nameof(MissileFin));
            MethodInfo missT = null;
            try
            {
                missT = AccessTools.DeclaredMethod(typeof(Mission), "MissileHitCallback");
                if (_dmgOk && missT != null && pre != null && fin != null)
                {
                    h.Patch(missT, prefix: new HarmonyMethod(pre), finalizer: new HarmonyMethod(fin));
                    _preOk = Has(missT, h.Id, pre, 'p') && Has(missT, h.Id, fin, 'f');
                }
            }
            catch (Exception e) { _preOk = false; Log.Error("FriendlyFire.ApplyAll (machiny)", e); }
            parts.Append(", machiny ").Append(_preOk ? "1/1" : !_dmgOk ? "pominiete" : "NIE");
            if (!_preOk) miss++;

            // (3a) wrecz
            _meleeOk = Transpile(h, "MeleeHitCallback", nameof(MeleeIl), _dmgOk, () => _meleeMatch == 1 && _meleeShape);
            parts.Append(", wrecz ").Append(State(_meleeOk, _dmgOk ? null : "brak latki skali", _meleeMatch, 1, _meleeShape));
            if (!_meleeOk) miss++;
            // (3b) pociski
            _missOk = Transpile(h, "MissileHitCallback", nameof(MissileIl), _dmgOk && _preOk, () => _missMatch == 2 && _missShape);
            parts.Append(", pociski ").Append(State(_missOk, !_dmgOk ? "brak latki skali" : !_preOk ? "brak znacznika machin" : null, _missMatch, 2, _missShape));
            if (!_missOk) miss++;
            // (3c) szarza
            _chargeOk = Transpile(h, "ChargeDamageCallback", nameof(ChargeIl), _dmgOk, () => _chargeMatch == 1 && _chargeShape);
            parts.Append(", szarza ").Append(State(_chargeOk, _dmgOk ? null : "brak latki skali", _chargeMatch, 1, _chargeShape));
            if (!_chargeOk) miss++;

            var s = Settings.Current;
            string head = miss == 0 ? "wpiete" : (_meleeOk || _missOk || _chargeOk) ? "CZESCIOWO wpiete" : "NIE wpiete";
            Log.Info("FriendlyFire: " + head + " - " + parts + " | latki, ktore nie weszly: " + miss
                     + " | klucz Friendly Fire przy starcie: " + src
                     + " | procenty z modulu: pociski " + (s != null ? Clamp(s.FriendlyFireRangedPercent) : 100) + "%, wrecz " + (s != null ? Clamp(s.FriendlyFireMeleePercent) : 50)
                     + "%, szarza " + (s != null ? Clamp(s.FriendlyFireChargePercent) : 50) + "% (czytane na poczatku kazdej misji); machiny i obszar - zasada gry.");
        }

        /// <summary>Recenzja P6: FriendlyFireEnabled przy starcie gry (OnBeforeInitialModuleScreenSetAsRoot - MCM wchodzi do Settings dopiero w sesji,
        /// McmSettings.Apply). Plik MCM (Documents\Mount and Blade II Bannerlord\Configs\ModSettings\Global\Armoury\Armoury.json, wpis "FriendlyFireEnabled" -
        /// to on wygrywa w sesji), bez wpisu albo pliku - Settings.Current (Armoury.settings.xml albo domyslne z kodu: TAK). Tylko odczyt. Nie rzuca.</summary>
        private static bool EnabledAtStart(out string src)
        {
            bool on = Settings.Current == null || Settings.Current.FriendlyFireEnabled;
            src = "ustawienia modulu / domyslne: " + (on ? "TAK" : "NIE");
            try
            {
                string doc = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(doc)) return on;
                string f = System.IO.Path.Combine(doc, "Mount and Blade II Bannerlord", "Configs", "ModSettings", "Global", "Armoury", "Armoury.json");
                if (!System.IO.File.Exists(f)) return on;
                var mm = System.Text.RegularExpressions.Regex.Match(System.IO.File.ReadAllText(f), "\"FriendlyFireEnabled\"\\s*:\\s*(true|false)",
                                                                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!mm.Success) { src = "plik MCM bez wpisu, " + src; return on; }
                on = string.Equals(mm.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
                src = "plik MCM: " + (on ? "TAK" : "NIE");
            }
            catch (Exception e) { src = "plik MCM nieczytelny (" + e.GetType().Name + "), " + src; }
            return on;
        }

        private static string State(bool ok, string skipped, int match, int want, bool shape)
        {
            if (ok) return want + "/" + want;
            if (skipped != null) return "pominiete (" + skipped + ")";
            return "NIE (dopasowan " + match + "/" + want + (shape ? "" : ", inny ksztalt IL") + " - zdjete)";
        }

        /// <summary>Transpiler na metodzie Mission; zly wynik (liczba dopasowan / ksztalt) - zdjety tym samym MethodInfo.</summary>
        private static bool Transpile(Harmony h, string target, string il, bool allowed, Func<bool> good)
        {
            if (!allowed) return false;
            MethodInfo t = null, tr = Own(il);
            bool patched = false;
            try
            {
                t = AccessTools.DeclaredMethod(typeof(Mission), target);
                if (t == null || tr == null) return false;
                h.Patch(t, transpiler: new HarmonyMethod(tr));
                patched = true;
                if (good() && Has(t, h.Id, tr, 't')) return true;
            }
            catch (Exception e) { Log.Error("FriendlyFire.ApplyAll (" + target + ")", e); }
            if (patched)
            {
                try { h.Unpatch(t, tr); } catch (Exception e) { Log.Error("FriendlyFire.ApplyAll (zdjecie " + target + ")", e); }
            }
            return false;
        }

        private static MethodInfo Own(string name)
        {
            return typeof(FriendlyFire).GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        }

        /// <summary>Harmony.GetPatchInfo: nasza latka naprawde na celu (pulapka 15.10 - finalizer BKROTPatch polyka bledy Harmony).</summary>
        private static bool Has(MethodBase target, string owner, MethodInfo patch, char kind)
        {
            var pi = Harmony.GetPatchInfo(target);
            if (pi == null) return false;
            IEnumerable<Patch> list = kind == 'o' ? pi.Postfixes : kind == 'p' ? pi.Prefixes : kind == 'f' ? pi.Finalizers : pi.Transpilers;
            if (list == null) return false;
            foreach (var p in list) if (p != null && p.owner == owner && p.PatchMethod == patch) return true;
            return false;
        }

        // ------------------------------------------------------------ transpilery

        private static int ArgIndex(MethodBase m, string name)
        {
            if (m == null) return -1;
            var ps = m.GetParameters();
            for (int i = 0; i < ps.Length; i++)
                if (ps[i].Name == name && ps[i].ParameterType == typeof(Agent)) return i + (m.IsStatic ? 0 : 1);
            return -1;
        }

        private static CodeInstruction Ldarg(int i)
        {
            switch (i)
            {
                case 0: return new CodeInstruction(OpCodes.Ldarg_0);
                case 1: return new CodeInstruction(OpCodes.Ldarg_1);
                case 2: return new CodeInstruction(OpCodes.Ldarg_2);
                case 3: return new CodeInstruction(OpCodes.Ldarg_3);
            }
            return i < 256 ? new CodeInstruction(OpCodes.Ldarg_S, (byte)i) : new CodeInstruction(OpCodes.Ldarg, (short)i);
        }

        /// <summary>MeleeHitCallback: jedyne call CancelsDamageAndBlocksAttackBecauseOfNonEnemyCase (Mission.il IL_0071) -> wynik przez Melee.</summary>
        public static IEnumerable<CodeInstruction> MeleeIl(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var list = new List<CodeInstruction>(instructions);
            _meleeShape = false;
            try
            {
                var target = AccessTools.DeclaredMethod(typeof(Mission), "CancelsDamageAndBlocksAttackBecauseOfNonEnemyCase");
                var helper = Own(nameof(Melee));
                int ia = ArgIndex(original, "attacker"), iv = ArgIndex(original, "victim");
                int n = 0, at = -1;
                if (target != null) for (int i = 0; i < list.Count; i++) if (list[i].Calls(target)) { n++; at = i; }
                _meleeMatch = n;
                if (n != 1 || helper == null || ia <= 0 || iv <= 0 || at + 1 >= list.Count || list[at + 1].labels.Count > 0) return list;
                _meleeShape = true;
                list.InsertRange(at + 1, new[] { Ldarg(0), Ldarg(ia), Ldarg(iv), new CodeInstruction(OpCodes.Call, helper) });
            }
            catch (Exception e) { _meleeShape = false; Log.Error("FriendlyFire.MeleeIl", e); return new List<CodeInstruction>(instructions); }
            return list;
        }

        /// <summary>MissileHitCallback: oba callvirt Agent::get_Controller (IL_0319 swoj, IL_0371 kon swojego; oba po ldarg attacker) -> wynik przez MissileCtl.</summary>
        public static IEnumerable<CodeInstruction> MissileIl(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var src = new List<CodeInstruction>(instructions);
            var list = new List<CodeInstruction>(src);
            _missShape = false;
            try
            {
                var target = AccessTools.DeclaredPropertyGetter(typeof(Agent), "Controller");
                var helper = Own(nameof(MissileCtl));
                int ia = ArgIndex(original, "attacker"), iv = ArgIndex(original, "victim");
                var at = new List<int>();
                if (target != null) for (int i = 0; i < list.Count; i++) if (list[i].Calls(target)) at.Add(i);
                _missMatch = at.Count;
                if (at.Count != 2 || helper == null || ia <= 0 || iv <= 0) return src;
                foreach (int i in at)
                    if (i == 0 || !list[i - 1].IsLdarg(ia) || i + 1 >= list.Count || list[i + 1].labels.Count > 0) return src;
                _missShape = true;
                for (int k = at.Count - 1; k >= 0; k--)   // od konca - indeksy wczesniejszych miejsc bez zmian
                    list.InsertRange(at[k] + 1, new[] { Ldarg(0), Ldarg(ia), Ldarg(iv), new CodeInstruction(OpCodes.Ldc_I4, k + 1), new CodeInstruction(OpCodes.Call, helper) });
            }
            catch (Exception e) { _missShape = false; Log.Error("FriendlyFire.MissileIl", e); return src; }
            return list;
        }

        /// <summary>ChargeDamageCallback: jedyne call IsFriendlyFireAllowedForChargeDamage (IL_0020) -> wynik przez Charge.</summary>
        public static IEnumerable<CodeInstruction> ChargeIl(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var list = new List<CodeInstruction>(instructions);
            _chargeShape = false;
            try
            {
                var target = AccessTools.DeclaredMethod(typeof(Mission), "IsFriendlyFireAllowedForChargeDamage");
                var helper = Own(nameof(Charge));
                int ia = ArgIndex(original, "attacker"), iv = ArgIndex(original, "victim");
                int n = 0, at = -1;
                if (target != null) for (int i = 0; i < list.Count; i++) if (list[i].Calls(target)) { n++; at = i; }
                _chargeMatch = n;
                if (n != 1 || helper == null || ia <= 0 || iv <= 0 || at + 1 >= list.Count || list[at + 1].labels.Count > 0) return list;
                _chargeShape = true;
                list.InsertRange(at + 1, new[] { Ldarg(0), Ldarg(ia), Ldarg(iv), new CodeInstruction(OpCodes.Call, helper) });
            }
            catch (Exception e) { _chargeShape = false; Log.Error("FriendlyFire.ChargeIl", e); return new List<CodeInstruction>(instructions); }
            return list;
        }

        // ------------------------------------------------------------ pomocnicze wolane z kodu gry (zwracaja wartosc gry przy watpliwosci)

        private static bool Open(Mission m)
        {
            if (!_active || m == null) return false;
            var r = _mref;
            return r != null && ReferenceEquals(r.Target, m);
        }

        /// <summary>Zasada gry zostaje: pojedynek z hersztem / zasadzka kryjowki po rozstrzygnieciu (ForceNoFriendlyFire), rozmowa, przerywnik, turniej.</summary>
        private static bool Blocked(Mission m)
        {
            if (m.ForceNoFriendlyFire) return true;
            var mode = m.Mode;
            return mode == MissionMode.Duel || mode == MissionMode.Tournament || mode == MissionMode.Conversation || mode == MissionMode.CutScene
                   || m.CombatType != Mission.MissionCombatType.Combat;
        }

        private static Agent Rider(Agent a)
        {
            if (a != null && a.IsMount && a.RiderAgent != null) return a.RiderAgent;
            return a;
        }

        /// <summary>Swoj: kon liczy sie jako jego jezdziec; obie druzyny wazne (przed natywnym IsFriend - HideoutAlarm.cs:49), ta sama strona (nie None),
        /// agentCheck - jeszcze IsFriendOf agentow (w trafieniu; przy zejsciu agenta tylko druzyny).</summary>
        private static bool OwnSide(Agent a, Agent v, bool agentCheck)
        {
            a = Rider(a);
            v = Rider(v);
            if (a == null || v == null || ReferenceEquals(a, v)) return false;
            var ta = a.Team;
            var tv = v.Team;
            if (ta == null || tv == null || !ta.IsValid || !tv.IsValid) return false;
            if (ta.Side == BattleSideEnum.None || ta.Side != tv.Side) return false;
            return agentCheck ? a.IsFriendOf(v) : ta.IsFriendOf(tv);
        }

        /// <summary>MeleeHitCallback: cancel = wynik CancelsDamageAndBlocksAttackBecauseOfNonEnemyCase. Swoj przy otwartej bramce - cios dochodzi
        /// (obrazenia skaluje postfiks), wrecz 0% - zasada gry.</summary>
        public static bool Melee(bool cancel, Mission m, Agent attacker, Agent victim)
        {
            if (!cancel || !_active) return cancel;
            try
            {
                if (!Open(m) || _meleePct <= 0 || Blocked(m)) return cancel;
                if (!OwnSide(attacker, victim, true)) return cancel;   // neutralni - jak w grze
                Interlocked.Increment(ref _openMelee);
                return false;
            }
            catch (Exception e) { Stumble("Melee", e); return cancel; }
        }

        /// <summary>MissileHitCallback, miejsce 1 (swoj) i 2 (kon swojego): attacker.Controller porownywany z AI. Swoj przy otwartej bramce - AI jak gracz
        /// (trafienie dochodzi); pociski 0% - AI dla kazdego (anulowane, takze strzaly gracza); machina - zasada gry.</summary>
        public static AgentControllerType MissileCtl(AgentControllerType ctl, Mission m, Agent attacker, Agent victim, int site)
        {
            if (!_active) return ctl;
            try
            {
                if (!Open(m) || Blocked(m)) return ctl;
                if (!OwnSide(attacker, victim, true)) return ctl;
                if (_missileCtx != 1)
                {
                    if (_missileCtx == 2 && ctl == AgentControllerType.AI) Interlocked.Increment(ref _siegeCancel);
                    return ctl;
                }
                if (_rangedPct <= 0) { Interlocked.Increment(ref _cancelMiss); return AgentControllerType.AI; }
                if (ctl != AgentControllerType.AI) return ctl;
                Interlocked.Increment(ref _openMissAi);
                return AgentControllerType.None;
            }
            catch (Exception e) { Stumble("MissileCtl" + site, e); return ctl; }
        }

        /// <summary>ChargeDamageCallback: allowed = IsFriendlyFireAllowedForChargeDamage() (w SP false). Swoj przy otwartej bramce - szarza rani
        /// (obrazenia skaluje postfiks), szarza 0% - zasada gry.</summary>
        public static bool Charge(bool allowed, Mission m, Agent attacker, Agent victim)
        {
            if (allowed || !_active) return allowed;
            try
            {
                if (!Open(m) || _chargePct <= 0 || Blocked(m)) return allowed;
                if (!OwnSide(attacker, victim, true)) return allowed;
                Interlocked.Increment(ref _openCharge);
                return true;
            }
            catch (Exception e) { Stumble("Charge", e); return allowed; }
        }

        /// <summary>Prefiks MissileHitCallback: znacznik trafienia pociskiem; pocisk machiny (MissionObjectToIgnore != null - jak isSiegeEngineHit) = 2.</summary>
        public static void MissilePre(Dictionary<int, Mission.Missile> ____missilesDictionary, ref AttackCollisionData collisionData)
        {
            int ctx = 1;
            if (_active)
            {
                try
                {
                    Mission.Missile mi;
                    if (____missilesDictionary != null && ____missilesDictionary.TryGetValue(collisionData.AffectorWeaponSlotOrMissileIndex, out mi)
                        && mi != null && mi.MissionObjectToIgnore != null) ctx = 2;
                }
                catch (Exception e) { Stumble("MissilePre", e); ctx = 2; }   // nie wiadomo, co to - zasada gry
            }
            _missileCtx = ctx;
        }

        /// <summary>Finalizer MissileHitCallback: znacznik zdjety (takze po wyjatku - void, wyjatek idzie dalej bez zmian).</summary>
        public static void MissileFin()
        {
            _missileCtx = 0;
        }

        /// <summary>Postfiks AgentApplyDamageModel.CalculateDamage: obrazenia swojego x procent (wrecz / pociski / szarza); liczniki misji.
        /// Upadek, samookaleczenie, cel-obiekt, machina i obszar (poza MissileHitCallback) - bez zmian.</summary>
        public static void DamagePost(in AttackInformation attackInformation, in AttackCollisionData collisionData, ref float __result)
        {
            if (!_active) return;
            try
            {
                var v = attackInformation.VictimAgent;
                var a = attackInformation.AttackerAgent;
                if (v == null || a == null || ReferenceEquals(v, a) || collisionData.IsFallDamage) return;
                if (!Open(v.Mission)) return;
                bool human = v.IsHuman;
                if (human) Interlocked.Increment(ref _humanHits);
                bool charge = collisionData.IsHorseCharge;
                bool missile = !charge && collisionData.IsMissile;
                bool ff = attackInformation.IsFriendlyFire;
                // szarza: atakujacy to kon (IsFriendOf konia nieznane); pocisk w konia swojego (miejsce 2) - liczy sie jezdziec. Wrecz w konia swojego
                // (recenzja P6): zalezy od natywnego IsFriendOf konia - gdy silnik uznaje go za swojego, gra dzis cios anuluje, a Melee() (OwnSide przez jezdzca)
                // go przepuszcza i tu wchodzi skala wrecz (domyslnie 50%; takze kon bohatera - HorseDeathPermanent, FieldCraft.cs:186-215); gdy nie uznaje -
                // gra rani go i dzis w pelni (ff false, bez zmian). Ta sama regula co pocisk w konia swojego; v1 bez zmiany kodu.
                if (!ff && (charge || missile)) ff = OwnSide(a, v, true);
                if (!ff) return;
                var r = Rider(a);
                if (ReferenceEquals(r, Rider(v))) return;
                if (missile && _missileCtx != 1)
                {
                    if (_missileCtx == 2) Interlocked.Increment(ref _siegeHit);
                    return;
                }
                int pct = charge ? _chargePct : missile ? _rangedPct : _meleePct;
                float before = __result;
                float after = pct >= 100 ? before : Math.Max(0f, before * pct * 0.01f);
                __result = after;
                bool pl = r != null && (r.IsMainAgent || r.IsPlayerControlled);
                if (charge) { if (pl) Interlocked.Increment(ref _chargePl); else Interlocked.Increment(ref _chargeAi); }
                else if (missile) { if (pl) Interlocked.Increment(ref _missPl); else Interlocked.Increment(ref _missAi); }
                else { if (pl) Interlocked.Increment(ref _meleePl); else Interlocked.Increment(ref _meleeAi); }
                Interlocked.Add(ref _hpBefore10, (long)Math.Round(before * 10f));
                Interlocked.Add(ref _hpAfter10, (long)Math.Round(after * 10f));
                if (human) Interlocked.Increment(ref _ffHumanHits);
                if (v.IsHero) Interlocked.Increment(ref _heroHits);
                if (v.IsMainAgent) Interlocked.Increment(ref _playerHits);
            }
            catch (Exception e) { Stumble("CalculateDamage", e); }
        }

        private static void Stumble(string where, Exception e)
        {
            Interlocked.Increment(ref _stum);
            Interlocked.Increment(ref _stumProcess);
            if (_stumWhere == null)
                Interlocked.CompareExchange(ref _stumWhere, where + ": " + (e != null ? e.GetType().Name + " " + e.Message : "?"), null);
        }

        private static int Clamp(int p) { return p < 0 ? 0 : p > 100 ? 100 : p; }

        // ------------------------------------------------------------ misja (watek glowny)

        private static void Reset()
        {
            _meleeAi = _meleePl = _missAi = _missPl = _chargeAi = _chargePl = 0;
            _openMelee = _openMissAi = _openCharge = _cancelMiss = 0;
            _siegeCancel = _siegeHit = 0;
            Interlocked.Exchange(ref _hpBefore10, 0);
            Interlocked.Exchange(ref _hpAfter10, 0);
            _humanHits = _ffHumanHits = _heroHits = _playerHits = 0;
            _killedAtt = _koAtt = _killedDef = _koDef = _heroDown = 0;
            _stum = 0;
            _stumWhere = null;
        }

        private static string KindOf(Mission m)
        {
            try
            {
                if (HideoutAlarm.IsHideout(m)) return "kryjowka";
                if (m.IsNavalBattle || m.IsNavalRaidBattle) return "morze";
                if (m.IsSiegeBattle) return "oblezenie";
                if (m.IsSallyOutBattle) return "wypad";
                if (m.IsFieldBattle) return "pole";
            }
            catch { }
            return "inna";
        }

        private static void Begin(Mission m)
        {
            Flush("bez konca poprzedniej misji");
            _active = false;
            Reset();
            var s = Settings.Current;
            _rangedPct = s != null ? Clamp(s.FriendlyFireRangedPercent) : 100;
            _meleePct = s != null ? Clamp(s.FriendlyFireMeleePercent) : 50;
            _chargePct = s != null ? Clamp(s.FriendlyFireChargePercent) : 50;
            _mref = new WeakReference(m);
            _kind = KindOf(m);
            _inBattle = false;
            try { _inBattle = Campaign.Current != null && MapEvent.PlayerMapEvent != null; } catch { _inBattle = false; }
            string why = null;
            if (s == null || !s.FriendlyFireEnabled) why = "klucz Friendly Fire wylaczony";
            else if (_offAtStart) why = "klucz byl wylaczony przy starcie gry - latki wejda po ponownym uruchomieniu gry";
            else if (!(_meleeOk || _missOk || _chargeOk)) why = "latki nie wpiete (linia FriendlyFire przy starcie)";
            else if (!_inBattle) why = "nie bitwa gracza na mapie";
            else if (m.CombatType != Mission.MissionCombatType.Combat) why = "walka cwiczebna";
            else if (m.Mode == MissionMode.Tournament || m.Mode == MissionMode.Duel) why = "turniej / pojedynek";
            else if (m.ForceNoFriendlyFire) why = "gra wylaczyla friendly fire w tej misji (ForceNoFriendlyFire)";
            _why = why;
            _written = false;
            _wasActive = why == null;
            _active = why == null;
        }

        private static void Removed(Mission m, Agent affected, Agent affector, AgentState state)
        {
            if (!Open(m)) return;
            if (state != AgentState.Killed && state != AgentState.Unconscious) return;
            if (affected == null || affector == null || !affected.IsHuman) return;
            if (!OwnSide(affector, affected, false)) return;
            var side = affected.Team.Side;
            bool killed = state == AgentState.Killed;
            if (side == BattleSideEnum.Attacker) { if (killed) _killedAtt++; else _koAtt++; }
            else if (side == BattleSideEnum.Defender) { if (killed) _killedDef++; else _koDef++; }
            if (affected.IsHero) _heroDown++;
        }

        private static void End(Mission m, string how)
        {
            var r = _mref;
            if (r == null || !ReferenceEquals(r.Target, m)) return;
            Flush(how);
        }

        /// <summary>Linia "FF" raz na misje: bitwa gracza (otwarta albo zamknieta bramka - z powodem); inne misje (miasto, karczma) bez linii.</summary>
        private static void Flush(string how)
        {
            if (_written) return;
            _written = true;
            _active = false;
            if (!_wasActive && !_inBattle) return;
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(700);
            sb.Append("FF (").Append(_kind).Append("): ");
            if (!_wasActive)
            {
                sb.Append("zamkniete - ").Append(_why ?? "?").Append(" | koniec: ").Append(how).Append('.');
                Log.Info(sb.ToString());
                return;
            }
            int melee = _meleeAi + _meleePl, miss = _missAi + _missPl, charge = _chargeAi + _chargePl;
            sb.Append("klucze pociski ").Append(_rangedPct).Append("%, wrecz ").Append(_meleePct).Append("%, szarza ").Append(_chargePct).Append('%');
            sb.Append(" | trafienia swoich: wrecz ").Append(melee).Append(" (AI ").Append(_meleeAi).Append(", gracz ").Append(_meleePl).Append(')')
              .Append(", pociski ").Append(miss).Append(" (AI ").Append(_missAi).Append(", gracz ").Append(_missPl).Append(')')
              .Append(", szarza ").Append(charge).Append(" (AI ").Append(_chargeAi).Append(", gracz ").Append(_chargePl).Append(')');
            sb.Append(" | bramki gry otwarte: wrecz ").Append(_openMelee).Append(", pociski AI ").Append(_openMissAi).Append(", szarza ").Append(_openCharge)
              .Append("; pociski anulowane przy 0% ").Append(_cancelMiss);
            sb.Append(" | machiny (zasada gry): AI w swoich anulowane ").Append(_siegeCancel).Append(", trafienia swoich ").Append(_siegeHit);
            sb.Append(" | HP od swoich przed/po skali ").Append((Interlocked.Read(ref _hpBefore10) / 10.0).ToString("0", inv))
              .Append('/').Append((Interlocked.Read(ref _hpAfter10) / 10.0).ToString("0", inv));
            sb.Append(" | powaleni przez swoich (zabici/ogluszeni): atak ").Append(_killedAtt).Append('/').Append(_koAtt)
              .Append(", obrona ").Append(_killedDef).Append('/').Append(_koDef).Append(", w tym bohaterowie ").Append(_heroDown);
            sb.Append(" | trafieni bohaterowie ").Append(_heroHits).Append(" (w tym Ty ").Append(_playerHits).Append(')');
            double share = _humanHits > 0 ? 100.0 * _ffHumanHits / _humanHits : 0.0;
            sb.Append(" | udzial FF w trafieniach ludzi ").Append(share.ToString("0.0", inv)).Append("% (").Append(_ffHumanHits).Append('/').Append(_humanHits).Append(')');
            sb.Append(" | potkniecia ").Append(_stum);
            if (_stum > 0 && _stumWhere != null) sb.Append(" (pierwsze: ").Append(_stumWhere).Append(')');
            sb.Append(" (sesja ").Append(_stumProcess).Append(") | koniec: ").Append(how).Append('.');
            Log.Info(sb.ToString());
        }

        /// <summary>noc 25.10 P6: bramka misji friendly fire (dodawana w SubModuleMain.OnMissionBehaviorInitialize przy kluczu i kampanii).</summary>
        internal sealed class Logic : MissionBehavior
        {
            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            public override void EarlyStart()
            {
                try { Begin(Mission); }
                catch (Exception e) { _active = false; Stumble("Begin", e); }
                base.EarlyStart();
            }

            public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
            {
                if (_active)
                {
                    try { Removed(Mission, affectedAgent, affectorAgent, agentState); }
                    catch (Exception e) { Stumble("OnAgentRemoved", e); }
                }
                base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
            }

            protected override void OnEndMission()
            {
                try { End(Mission, "koniec misji"); } catch (Exception e) { Log.Error("FriendlyFire.OnEndMission", e); }
                base.OnEndMission();
            }

            public override void OnRemoveBehavior()
            {
                try { End(Mission, "zdjete zachowanie"); } catch (Exception e) { Log.Error("FriendlyFire.OnRemoveBehavior", e); }
                base.OnRemoveBehavior();
            }
        }
    }
}
