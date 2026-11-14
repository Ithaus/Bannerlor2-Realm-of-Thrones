using System;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using SandBox.GameComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 506 (perki, audyt 14.11 punkty 1, 2, 21, 26, 27, 34; decyzje Jeffa docs/audyty-2026-11-14/PERKI-DECYZJE.md): PANCERZ I TARCZE Z PERKOW PO RBM.
    ///
    /// SKAD PROBLEM (dekompilacja): perki pancerza i oslony tarczy dzialaja w grze przez AgentDrivenProperties liczone w
    /// SandboxAgentStatCalculateModel (UpdateHorseStats :1196-1214 ToughSteed; SetPerkAndBannerEffectsOnAgent :1476-1487 ArrowCatcher / ShieldWall,
    /// :1519-1555 DauntlessSteed / IgnorePain). RBM:
    ///  - podmienia Agent.GetBaseArmorEffectivenessForBodyPart (prefiks ArmorRework.ChangeBodyPartArmor, false) na
    ///    ArmorRework.GetBaseArmorEffectivenessForBodyPartRBM - pancerz wprost z SpawnEquipment; ArmorHead/Torso/Arms/Legs agenta nikt juz nie czyta,
    ///    wiec +pancerz z perkow przepada (rbm ArmorRework.cs:13-17, 767-830);
    ///  - postfiks na SandboxAgentStatCalculateModel.UpdateHumanStats ustawia KAZDEMU AttributeShieldMissileCollisionBodySizeAdder = 0.01
    ///    (rbm DamageRework.cs:922-930; gra: 0.3 + perki) - oslona z ArrowCatcher i ShieldWall przepada;
    ///  - prefiks na Mission.MissileHitCallback przepisuje trafienie w tarcze na plecach na blok (rbm RangedRework.cs:1446-1500) - rzut 75% perka Pavise
    ///    (SandboxAgentApplyDamageModel.cs:31-34) jest zbedny poza pociskami, ktore RBM przepuszcza (przebijajace / Impale).
    ///
    /// NOWE EFEKTY (Jeff 14.11):
    ///  1  ArrowCatcher - NIE oslona ("oslona tarczy jest taka jak tarcza daje"). Osobisty: tarcza +ArrowCatcherShieldDurability (20%) wytrzymalosci
    ///     w bitwie i -ArrowCatcherShieldWearCut (25%) zuzycia Armoury; dowodca: tarcze ludzi w jego szyku +ArrowCatcherCaptainDurability (10%).
    ///  2  ShieldWall (dowodca) - TYLKO w szyku sciany tarcz (ArrangementOrderEnum.ShieldWall = 5, jak gra :1480): oslona przed pociskami
    ///     +ShieldWallPerkCover do wartosci RBM (postfiks PO RBM na UpdateHumanStats). ArrangementOrder.OnApply wola agent.UpdateAgentProperties dla
    ///     kazdego czlonka szyku (TaleWorlds.MountAndBlade ArrangementOrder.cs:126-143), wiec zmiana szyku przelicza oslone od razu.
    ///  21 Pavise - tarcza na PLECACH (AttackCollisionData.CollidedWithShieldOnBack) +PaviseBackShieldDurability (30%) wytrzymalosci,
    ///     -PaviseBackShieldWearCut (25%) zuzycia; efekt gubernatora (balista) i rzut 75% gry zostaja.
    ///  26 ToughSteed, 27 DauntlessSteed, 34 IgnorePain - te same liczby co gra, doliczone PO RBM (postfiks na GetBaseArmorEffectivenessForBodyPartRBM):
    ///     czlowiek: (pancerz + dowodca DauntlessSteed 5 konnemu / IgnorePain 5 pieszemu) x (1 + IgnorePain 10% pieszego); kon: (pancerz + dowodca
    ///     jezdzca ToughSteed 10) x (1 + ToughSteed 20% jezdzca). Dowodca = kapitan szyku, nie on sam (jak gra). Wartosci z PerkObject (nie wpisane).
    ///
    /// WYTRZYMALOSC TARCZY = obrazenia tarczy / mnoznik (postfiks na MissionCombatMechanicsHelper.ComputeBlowDamageOnShield, Priority.Low - PRZED
    /// ShieldGuard 414 (Priority.Last), ktory potem tnie strzaly do 1%). Mnozniki sumowane: 1 + osobisty + dowodca + Pavise. Silnik zaraz potem
    /// robi AbsorbedByArmor = InflictedDamage (MissionCombatMechanicsHelper.cs:208-209), a FieldCraft liczy zuzycie tarczy z tej liczby - dlatego
    /// WearFactor oddaje mnoznik wytrzymalosci (zuzycie Armoury bez podwojnego liczenia) i dopiero mnozy przez ciecia perkow: zuzycie = dokladnie
    /// -25% (ArrowCatcher) / -25% (Pavise na plecach), a dowodca ArrowCatcher zuzycia nie rusza.
    ///
    /// Bez RBM: pancerz i ShieldWall nieczynne (gra liczy je sama), wytrzymalosc tarcz dziala. Jedna regula dla gracza i AI.
    /// Latki w kampanii (NightHooks.InCampaign, raz na proces), po 415 - postfiks pancerza tej samej waznosci (Priority.Last) biegnie po NeckGuard415
    /// (kolejnosc zakladania), wiec premia perka idzie na pancerz juz z szyja / barkami - jak w grze (perk na koncu).
    /// Perki czytane z obiektow kampanii odswiezanych na starcie kazdej misji (Logic) - poza kampania (bitwa wlasna) nieczynne.
    /// </summary>
    internal static class Perk506
    {
        private const int ShieldWallEnum = 5;                // ArrangementOrder.ArrangementOrderEnum.ShieldWall

        private static int _patched, _wanted;
        private static string _note = "nie zakladano";
        private static bool _armorPatched, _shieldPatched, _statsPatched;
        private static MethodBase _stats;                    // SandboxAgentStatCalculateModel.UpdateHumanStats
        private static bool _rbmStats;                       // postfiks RBM (0.01) jest teraz na UpdateHumanStats

        /// <summary>Choc jedna latka 506 siedzi w procesie - SubModuleMain dodaje wtedy Logic w kazdej misji (odswiezenie perkow / zerowanie).</summary>
        internal static bool Patched { get { return _patched > 0; } }

        // perki kampanii (nowe obiekty w kazdej grze) - odswiezane w Logic.OnBehaviorInitialize; null = nieczynne
        private static PerkObject _arrowCatcher, _shieldWall, _pavise, _toughSteed, _dauntless, _ignorePain;

        // liczniki misji (zwykle pola; metoda pancerza bywa wolana z watku pobocznego - wtedy przyblizone)
        private static int _armHuman, _armHorse;
        private static float _armHumanBefore, _armHumanAfter, _armHorseBefore, _armHorseAfter;
        private static int _shN, _shAc, _shCap, _shPav;
        private static long _shIn, _shOut;
        private static int _swSet, _wearN;
        private static float _wearSum;
        private static int _errs, _errsLogged;

        /// <summary>NightHooks.InCampaign (ArmouryBehavior.OnSessionLaunched, raz na proces). Zwraca opis do linii "Latki nocy (K3): kampania".</summary>
        internal static string ApplyInCampaign(Harmony h)
        {
            if (h == null) { _note = "brak Harmony"; return _note; }
            _patched = 0; _wanted = 0;
            string miss = "";
            RefreshPerks();

            // 26 / 27 / 34: pancerz czesci ciala RBM
            Type tArmor = null;
            try { tArmor = AccessTools.TypeByName("RBMCombat.ArmorRework"); } catch { }
            if (tArmor == null) miss += " RBM nieobecny - pancerz z perkow liczy gra;";
            else
            {
                _wanted++;
                MethodInfo armor = null;
                try { armor = AccessTools.DeclaredMethod(tArmor, "GetBaseArmorEffectivenessForBodyPartRBM", new[] { typeof(Agent), typeof(BoneBodyPartType) }); } catch { }
                if (armor == null || !armor.IsStatic || armor.ReturnType != typeof(float)) miss += " brak ArmorRework.GetBaseArmorEffectivenessForBodyPartRBM(Agent, BoneBodyPartType);";
                else
                {
                    try
                    {
                        h.Patch(armor, postfix: new HarmonyMethod(typeof(Perk506), nameof(ArmorPostfix)) { priority = Priority.Last });
                        _armorPatched = Owned(armor, h.Id, false);
                        if (_armorPatched) _patched++; else miss += " pancerz NIE wszedl;";
                    }
                    catch (Exception e) { miss += " pancerz BLAD " + e.GetType().Name + ";"; Log.Error("Perk506.Patch(armor)", e); }
                }
            }

            // 1 / 21: wytrzymalosc tarczy w bitwie (metoda silnika - dziala z RBM i bez)
            _wanted++;
            MethodInfo shield = null;
            try { shield = AccessTools.DeclaredMethod(typeof(MissionCombatMechanicsHelper), "ComputeBlowDamageOnShield"); } catch { }
            if (shield == null) miss += " brak MissionCombatMechanicsHelper.ComputeBlowDamageOnShield;";
            else
            {
                try
                {
                    h.Patch(shield, postfix: new HarmonyMethod(typeof(Perk506), nameof(ShieldPostfix)) { priority = Priority.Low });
                    _shieldPatched = Owned(shield, h.Id, false);
                    if (_shieldPatched) _patched++; else miss += " tarcza NIE weszla;";
                }
                catch (Exception e) { miss += " tarcza BLAD " + e.GetType().Name + ";"; Log.Error("Perk506.Patch(shield)", e); }
            }

            // 2: ShieldWall po RBM (postfiks po postfiksie RBM na UpdateHumanStats)
            MethodInfo stats = null;
            try { stats = AccessTools.DeclaredMethod(typeof(SandboxAgentStatCalculateModel), "UpdateHumanStats"); } catch { }
            _stats = stats;
            RefreshRbm();
            if (stats == null) miss += " brak SandboxAgentStatCalculateModel.UpdateHumanStats;";
            else if (!_rbmStats) miss += " UpdateHumanStats bez postfiksu RBM (oslone tarcz liczy gra, ShieldWall dziala tam sam) - ShieldWall nie zakladany;";
            else
            {
                _wanted++;
                try
                {
                    h.Patch(stats, postfix: new HarmonyMethod(typeof(Perk506), nameof(StatsPostfix)) { priority = Priority.Last });
                    _statsPatched = Owned(stats, h.Id, false);
                    if (_statsPatched) _patched++; else miss += " ShieldWall NIE wszedl;";
                }
                catch (Exception e) { miss += " ShieldWall BLAD " + e.GetType().Name + ";"; Log.Error("Perk506.Patch(stats)", e); }
            }

            var s = Settings.Current;
            _note = "pancerz i tarcze z perkow " + _patched + "/" + _wanted + (miss.Length > 0 ? " |" + miss : "");
            Log.Info("Perki pancerza i tarcz (506): latka raz na proces - " + _note
                     + ". Klucz PerkArmourAfterRbm " + (s != null && s.PerkArmourAfterRbm ? "TAK" : "NIE")
                     + ": IgnorePain x" + F(1f + Bonus(_ignorePain, true)) + " pieszy, dowodca +" + F(Bonus(_ignorePain, false)) + " pieszym / DauntlessSteed +" + F(Bonus(_dauntless, false))
                     + " konnym, kon: dowodca ToughSteed +" + F(Bonus(_toughSteed, false)) + ", jezdziec x" + F(1f + Bonus(_toughSteed, true))
                     + " | klucz PerkShieldsAfterRbm " + (s != null && s.PerkShieldsAfterRbm ? "TAK" : "NIE")
                     + ": ArrowCatcher tarcza +" + Pct(s != null ? s.ArrowCatcherShieldDurability : 0f) + " wytrzymalosci, -" + Pct(s != null ? s.ArrowCatcherShieldWearCut : 0f)
                     + " zuzycia, dowodca +" + Pct(s != null ? s.ArrowCatcherCaptainDurability : 0f) + "; Pavise tarcza na plecach +" + Pct(s != null ? s.PaviseBackShieldDurability : 0f)
                     + ", -" + Pct(s != null ? s.PaviseBackShieldWearCut : 0f) + " zuzycia; ShieldWall (dowodca, tylko sciana tarcz) oslona +" + F(s != null ? s.ShieldWallPerkCover : 0f)
                     + " (RBM 0.01, gra 0.30).");
            return _note;
        }

        /// <summary>Perki kampanii - raz przy zakladaniu i na starcie kazdej misji. Poza kampania null (nieczynne).</summary>
        private static void RefreshPerks()
        {
            PerkObject ac = null, sw = null, pv = null, ts = null, ds = null, ip = null;
            try
            {
                if (Campaign.Current != null)
                {
                    ac = DefaultPerks.OneHanded.ArrowCatcher;
                    sw = DefaultPerks.OneHanded.ShieldWall;
                    pv = DefaultPerks.Crossbow.Pavise;
                    ts = DefaultPerks.Riding.ToughSteed;
                    ds = DefaultPerks.Riding.DauntlessSteed;
                    ip = DefaultPerks.Athletics.IgnorePain;
                }
            }
            catch (Exception e) { Count("Perk506.RefreshPerks", e); }
            _arrowCatcher = ac; _shieldWall = sw; _pavise = pv; _toughSteed = ts; _dauntless = ds; _ignorePain = ip;
        }

        /// <summary>Czy postfiks RBM (oslona 0.01) jest na UpdateHumanStats - przy zakladaniu i na starcie misji (RBM zdejmuje / zaklada latki po swoich ustawieniach).</summary>
        private static void RefreshRbm()
        {
            bool on = false;
            try
            {
                var info = _stats != null ? Harmony.GetPatchInfo(_stats) : null;
                if (info != null)
                    foreach (var p in info.Postfixes)
                        if (p != null && p.PatchMethod != null && p.PatchMethod.DeclaringType != null
                            && (p.PatchMethod.DeclaringType.FullName ?? "").StartsWith("RBMCombat.", StringComparison.Ordinal)) { on = true; break; }
            }
            catch { }
            _rbmStats = on;
        }

        private static float Bonus(PerkObject p, bool primary)
        {
            if (p == null) return 0f;
            return primary ? p.PrimaryBonus : p.SecondaryBonus;
        }

        private static string F(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        private static string F1(float v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }
        private static string Pct(float v) { return (v * 100f).ToString("0", CultureInfo.InvariantCulture) + "%"; }

        private static bool Owned(MethodBase mi, string id, bool prefix)
        {
            try
            {
                var info = Harmony.GetPatchInfo(mi);
                if (info == null) return false;
                foreach (var p in prefix ? info.Prefixes : info.Postfixes)
                    if (p != null && p.owner == id && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(Perk506)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>Potkniecie liczone (bez wylacznika); pierwsze trzy w misji z pelnym wyjatkiem w logu.</summary>
        private static void Count(string where, Exception e)
        {
            _errs++;
            if (_errsLogged < 3) { _errsLogged++; Log.Error(where, e); }
        }

        /// <summary>Kapitan szyku agenta jako postac kampanii; null, gdy brak albo to on sam (jak gra: SetPerkAndBannerEffectsOnAgent :1265-1280).</summary>
        private static CharacterObject CaptainOf(Agent a)
        {
            var f = a.Formation;
            var c = f != null ? f.Captain : null;
            if (c == null || c == a) return null;
            return c.Character as CharacterObject;
        }

        // ---------------- 26 / 27 / 34: pancerz po RBM ----------------

        /// <summary>Po RBM GetBaseArmorEffectivenessForBodyPartRBM (i po NeckGuard415). Formula gry: (pancerz + premia dowodcy) x (1 + premia osobista).
        /// Bez alokacji; same odczyty perkow (slownik bohatera) - zdarzenia (cios, postawa RBMAI, obnizka stanu RBM), nie klatka.</summary>
        private static void ArmorPostfix(Agent __0, BoneBodyPartType __1, ref float __result)
        {
            try
            {
                if (__0 == null || __1 == BoneBodyPartType.None) return;
                var ip = _ignorePain;
                if (ip == null) return;                                                      // poza kampania
                var s = Settings.Current;
                if (s == null || !s.PerkArmourAfterRbm) return;
                float flat = 0f, pct = 0f;
                if (__0.IsHuman)
                {
                    bool mounted = __0.HasMount;
                    var cap = CaptainOf(__0);
                    if (cap != null)
                    {
                        if (mounted) { var ds = _dauntless; if (ds != null && cap.GetPerkValue(ds)) flat += ds.SecondaryBonus; }
                        else if (cap.GetPerkValue(ip)) flat += ip.SecondaryBonus;
                    }
                    if (!mounted)
                    {
                        var self = __0.Character as CharacterObject;
                        if (self != null && self.GetPerkValue(ip)) pct += ip.PrimaryBonus;
                    }
                    if (flat == 0f && pct == 0f) return;
                    float was = __result;
                    float now = (was + flat) * (1f + pct);
                    if (now < 0f) now = 0f;
                    __result = now;
                    _armHuman++; _armHumanBefore += was; _armHumanAfter += now;
                }
                else
                {
                    var rider = __0.RiderAgent;
                    var ts = _toughSteed;
                    if (rider == null || ts == null) return;                                 // kon bez jezdzca - gra tez nic (UpdateHorseStats :1170)
                    var cap = CaptainOf(rider);
                    if (cap != null && cap.GetPerkValue(ts)) flat += ts.SecondaryBonus;
                    var rc = rider.Character as CharacterObject;
                    if (rc != null && rc.GetPerkValue(ts)) pct += ts.PrimaryBonus;
                    if (flat == 0f && pct == 0f) return;
                    float was = __result;
                    float now = (was + flat) * (1f + pct);
                    if (now < 0f) now = 0f;
                    __result = now;
                    _armHorse++; _armHorseBefore += was; _armHorseAfter += now;
                }
            }
            catch (Exception e) { Count("Perk506.ArmorPostfix", e); }
        }

        // ---------------- 1 / 21: wytrzymalosc i zuzycie tarczy ----------------

        /// <summary>Mnoznik wytrzymalosci tarczy ofiary (1 = bez perkow). onBack = trafienie w tarcze na plecach (Pavise).</summary>
        private static float Durability(Settings s, Agent v, bool onBack, out bool ac, out bool cap, out bool pav)
        {
            ac = false; cap = false; pav = false;
            if (s == null || !s.PerkShieldsAfterRbm || v == null || !v.IsHuman) return 1f;
            var acp = _arrowCatcher;
            if (acp == null) return 1f;
            float m = 1f;
            var self = v.Character as CharacterObject;
            if (self != null && self.GetPerkValue(acp)) { ac = true; m += Math.Max(0f, s.ArrowCatcherShieldDurability); }
            var c = CaptainOf(v);
            if (c != null && c.GetPerkValue(acp)) { cap = true; m += Math.Max(0f, s.ArrowCatcherCaptainDurability); }
            var pv = _pavise;
            if (onBack && pv != null && self != null && self.GetPerkValue(pv)) { pav = true; m += Math.Max(0f, s.PaviseBackShieldDurability); }
            return m;
        }

        /// <summary>Postfiks na ComputeBlowDamageOnShield (po prefiksie RBM / ciele gry, przed ShieldGuard 414): obrazenia tarczy / mnoznik wytrzymalosci.</summary>
        private static void ShieldPostfix(ref AttackInformation attackInformation, ref AttackCollisionData attackCollisionData, ref int inflictedDamage)
        {
            try
            {
                if (inflictedDamage <= 0) return;
                bool ac, cap, pav;
                float m = Durability(Settings.Current, attackInformation.VictimAgent, attackCollisionData.CollidedWithShieldOnBack, out ac, out cap, out pav);
                if (!(m > 1f)) return;
                int was = inflictedDamage;
                int now = (int)(was / m + 0.5f);
                if (now < 1) now = 1;                                                       // trafienie zostaje trafieniem (jak ShieldGuard - bez zera z dzielenia)
                if (now > was) now = was;
                inflictedDamage = now;
                _shN++; _shIn += was; _shOut += now;
                if (ac) _shAc++;
                if (cap) _shCap++;
                if (pav) _shPav++;
            }
            catch (Exception e) { Count("Perk506.ShieldPostfix", e); }
        }

        /// <summary>FieldCraft.OnAgentHit (zuzycie tarczy w ksiedze bohatera): mnoznik do surowego zuzycia z obrazen tarczy. Oddaje mnoznik wytrzymalosci
        /// (obrazenia przyszly juz podzielone przez ShieldPostfix - bez tego dowodca ArrowCatcher cialby zuzycie, a osobisty liczylby sie dwa razy)
        /// i mnozy przez ciecia perkow: ArrowCatcher -25%, Pavise na plecach -25%. 1 = bez zmian.</summary>
        internal static float ShieldWearFactor(Agent victim, bool onBack)
        {
            try
            {
                var s = Settings.Current;
                bool ac, cap, pav;
                float m = Durability(s, victim, onBack, out ac, out cap, out pav);
                if (!(m > 1f)) return 1f;
                float f = _shieldPatched ? m : 1f;
                if (ac) f *= 1f - Math.Max(0f, Math.Min(0.9f, s.ArrowCatcherShieldWearCut));
                if (pav) f *= 1f - Math.Max(0f, Math.Min(0.9f, s.PaviseBackShieldWearCut));
                _wearN++; _wearSum += f;
                return f;
            }
            catch (Exception e) { Count("Perk506.ShieldWearFactor", e); return 1f; }
        }

        // ---------------- 2: ShieldWall po RBM ----------------

        /// <summary>Po postfiksie RBM na UpdateHumanStats (ten ustawia oslone 0.01 kazdemu). Warunki gry: tarcza w lewej rece, kapitan szyku (nie on sam)
        /// z ShieldWall, szyk w scianie tarcz. Przeliczane przy kazdej zmianie szyku (ArrangementOrder.OnApply -> UpdateAgentProperties), nie co klatke.</summary>
        private static void StatsPostfix(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            try
            {
                if (!_rbmStats || agent == null || agentDrivenProperties == null) return;
                var sw = _shieldWall;
                if (sw == null) return;
                var s = Settings.Current;
                if (s == null || !s.PerkShieldsAfterRbm || !(s.ShieldWallPerkCover > 0f)) return;
                var f = agent.Formation;
                if (f == null || (int)f.ArrangementOrder.OrderEnum != ShieldWallEnum) return;
                var off = agent.GetOffhandWieldedItemIndex();
                if (off == EquipmentIndex.None) return;
                var w = agent.Equipment[off].CurrentUsageItem;
                if (w == null || !w.IsShield) return;
                var cap = CaptainOf(agent);
                if (cap == null || !cap.GetPerkValue(sw)) return;
                agentDrivenProperties.AttributeShieldMissileCollisionBodySizeAdder += Math.Min(0.3f, s.ShieldWallPerkCover);
                _swSet++;
            }
            catch (Exception e) { Count("Perk506.StatsPostfix", e); }
        }

        // ---------------- liczniki misji ----------------

        private static void Reset()
        {
            _armHuman = 0; _armHorse = 0; _armHumanBefore = 0f; _armHumanAfter = 0f; _armHorseBefore = 0f; _armHorseAfter = 0f;
            _shN = 0; _shAc = 0; _shCap = 0; _shPav = 0; _shIn = 0; _shOut = 0;
            _swSet = 0; _wearN = 0; _wearSum = 0f;
            _errs = 0; _errsLogged = 0;
        }

        private static string Avg(float sum, int n) { return n > 0 ? F1(sum / n) : "-"; }

        private static void Flush(Mission m)
        {
            if (_armHuman == 0 && _armHorse == 0 && _shN == 0 && _swSet == 0 && _wearN == 0 && _errs == 0) { Reset(); return; }
            string kind = "?";
            try { kind = MissionTeams.Kind(m); } catch { }
            Log.Info("Perki pancerza i tarcz (506): koniec misji (" + kind + ") | latki " + _patched + "/" + _wanted
                     + " | pancerz z perkow: ludzie " + _armHuman + " odczytow (srednio " + Avg(_armHumanBefore, _armHuman) + " -> " + Avg(_armHumanAfter, _armHuman)
                     + "), konie " + _armHorse + " (" + Avg(_armHorseBefore, _armHorse) + " -> " + Avg(_armHorseAfter, _armHorse) + ")"
                     + " | tarcze: ciosy z ulga " + _shN + " (ArrowCatcher osobisty " + _shAc + ", dowodcy " + _shCap + ", Pavise na plecach " + _shPav
                     + "; obrazenia tarcz " + _shIn + " -> " + _shOut + ")"
                     + " | zuzycie tarcz bohaterow z perkiem: " + _wearN + " wpisow (sredni mnoznik x" + (_wearN > 0 ? F(_wearSum / _wearN) : "-") + ")"
                     + " | ShieldWall: przeliczen oslony w scianie tarcz " + _swSet + (_rbmStats ? "" : " (postfiks RBM nieobecny - nieczynne)")
                     + " | potkniecia " + _errs + ".");
            Reset();
        }

        /// <summary>Odswiezenie perkow i stanu RBM na starcie misji, linia sumy na koncu. Dodawana takze poza kampania, gdy latka siedzi w procesie
        /// (perki wtedy null - wszystko nieczynne).</summary>
        internal sealed class Logic : MissionLogic
        {
            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            public override void OnBehaviorInitialize()
            {
                base.OnBehaviorInitialize();
                Reset();
                RefreshPerks();
                RefreshRbm();
            }

            protected override void OnEndMission()
            {
                try { Flush(Mission); } catch (Exception e) { Log.Error("Perk506.Flush", e); }
                base.OnEndMission();
            }
        }
    }
}
