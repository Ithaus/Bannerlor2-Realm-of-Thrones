using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 419 (audyt RBM 11.11, B4 + decyzja Jeffa 2 "tak maja byc do przebicia"): SUFIT PANCERZA LADROW PLYTOWYCH ROT. Sama korekta danych w pamieci,
    /// raz na start sesji (wzor: RotData418). Bez edycji XML, bez latek Harmony.
    ///
    /// SKAD LICZBY. ROTassets.xml daje ladrom plytowym body_armor 65-80 przy 140-145 kg; lacznik ROT z RBM (ROT.HarmonyPatches.ModCompatibility.RBM
    /// .DeserializeArmorComponent, rot_all.cs:17517) rozklada to na glowa x1.2 / tulow x1.1 / szyja x0.6 / nogi x1.0 - w grze 90 / 82 / 45 / 75 (rot_horse_armor*).
    /// RBM (ArmorRework.GetBaseArmorEffectivenessForBodyPartRBM, rbm.cs:459) dodaje koniowi 10 do kazdego pola i liczy piers = (nogi + tulow) / 2 = 88.5,
    /// barki = tulow = 92, szyja = 55, glowa = 100. Material Plate nie dostaje ulgi x0.5 dla grotow przebijajacych (RBMComputeDamage, rbm.cs:5951), wiec
    /// prog przebicia piersi = 88.5 x 2 = 177 J - a najmocniejszy luk ROT (180 lb, bodkin 90 g) niesie 142 J z 20 m. Kazda strzala = 3-7 HP z 340.
    /// Ladry RBM maja tulow 0-50 (mediana 10) przy 8-30 kg.
    ///
    /// REGULA. Kazde pole ladrow z listy przyciete do sufitu (RotBardingArmorCap, domyslnie 45): tulow, szyja i nogi do sufitu, glowa do sufitu x1.2
    /// (naczolek zostaje najgrubszy - proporcja ROT). Tylko w dol. 90 / 82 / 45 / 75 -> 54 / 45 / 45 / 45: piers 55 (prog bodkina 110 J, strzaly szerokiej
    /// 143 J), szyja bez zmian. Bodkin 150 lb w piers konia w szarzy (8 m/s) z 20 m: 7 -> 54 HP (stojacego 5 -> 18); 100 lb: 5 -> 11; strzala szeroka 100-180 lb dalej 3-8 HP.
    /// Waga (szybkosc i szarza konia), material, HP koni - bez zmian.
    ///
    /// EKONOMIA BEZ ZMIAN (kontrola calosci): z pol pancerza rzedu licza u nas na zywo trzy miejsca - ArmsPricing.CostWeight (waga do kosztu = 0.4 kg x pancerz
    /// konia: wartosc, kwit kuzni, warsztat, przetop, material naprawy; cache kosztu sprawdza ja co przebieg), Recipes.ArmourUnits (suma pol) i
    /// ArmouryBehavior.ArmorPool (pula zuzycia). Wszystkie trzy biora dla sztuk z listy wartosci SPRZED sufitu (BookBody / BookSum) - koszt wykonania
    /// ladrow nie zalezy od tego, jak RBM liczy przebicie. Tier zamrozony przez TierfOverride jak w 418 (RBM: tulow x 0.02 + nogi x 0.04 + szyja x 0.02 +
    /// glowa x 0.02 - 1; bez zamrozenia t6 spadlby do t3). Cena gry (Value) zapamietana przy wczytaniu.
    ///
    /// POMINIETE SWIADOMIE: dothraki_horse_armor i dothraki_saddle (decyzja Jeffa 4 - Dothrakowie bez zmian; we wzorcach wojsk 0 uzyc), pancerze sloni
    /// (rot_elephant_armor1 / 2 - 132 i 110; inne zwierze, osobna decyzja), siodla z naczolkiem (tulow 38 - ponizej sufitu), kolczugi konskie Starkow
    /// (material Chainmail - bodkin i tak liczy polowe pancerza: 17-51 HP), kropierze skorzane, siodlo smoka.
    ///
    /// Obiekty przedmiotow sa nowe w kazdej grze, wiec klucz dziala od nastepnego wczytania; gdyby jednak ta sama sztuka przyszla drugi raz, Apply najpierw
    /// przywraca jej wartosci sprzed sufitu (slownik Orig), wiec NIE = dokladnie dane ROT, a zmiana suwaka nie sklada sie z poprzednia.
    /// Regula nie zna wlasciciela: gracz i AI jednakowo.
    /// </summary>
    internal sealed class HorseBarding419 : CampaignBehaviorBase
    {
        // ladry plytowe ROT-Content/ROTassets.xml (zrzut 11.11: material Plate, 140-145 kg, body_armor XML 65-80) - 45 sztuk
        private static readonly string[] BardingIds =
        {
            "kingsguard_horse_armor", "horse_armor",
            "rot_horse_mail", "rot_horse_mail2", "rot_horse_mail3", "rot_horse_mail4", "rot_horse_mail5",
            "rot_horse_armor1", "rot_horse_armor2", "rot_horse_armor3", "rot_horse_armor4", "rot_horse_armor5", "rot_horse_armor6", "rot_horse_armor8",
            "rot_horse_armor9", "rot_horse_armor10", "rot_horse_armor11", "rot_horse_armor12", "rot_horse_armor13", "rot_horse_armor17", "rot_horse_armor18",
            "rot_horse_armor19", "rot_horse_armor20", "rot_horse_armor21", "rot_horse_armor22", "rot_horse_armor23", "rot_horse_armor25", "rot_horse_armor26",
            "rot_horse_armor27", "rot_horse_armor28", "rot_horse_armor29", "rot_horse_armor30", "rot_horse_armor31", "rot_horse_armor32",
            "ghiscari_horse_armor", "braavosi_horse_armor", "lorathi_horse_armor", "pentoshi_horse_armor", "norvoshi_horse_armor", "qohorik_horse_armor",
            "myrish_horse_armor", "tyroshi_horse_armor", "lyseni_horse_armor", "volantene_horse_armor", "wildling_horse_armor",
        };

        // do linii logu, nigdy nie ruszane
        private static readonly string[] SkippedByDecision = { "dothraki_horse_armor", "dothraki_saddle", "rot_elephant_armor1", "rot_elephant_armor2" };

        internal const int CapMin = 31, CapMax = 90;      // ponizej 31 lader wpadalby w klase "kropierz" cennika (PriceList8.CaparisonMaxArmor = 30)
        private const float HeadShare = 1.2f;             // glowa = sufit x 1.2 (proporcja lacznika ROT: glowa 1.2, tulow 1.1 bazy XML)

        // stan sesji. Orig: sztuka z listy -> pola sprzed sufitu (glowa, tulow, szyja, nogi); podmieniany w calosci (czytaja go watki wyceny)
        private static Dictionary<ItemObject, int[]> _orig = new Dictionary<ItemObject, int[]>();
        private static bool _on;
        private static int _cap = 45, _changed;

        // liczniki misji (glowny watek). Grupa: 0 = kon w ladrach z listy, 1 = inny wierzchowiec. Rodzaj: 0 = strzala / belt przebijajacy (bodkin),
        // 1 = strzala / belt tnacy (szeroki), 2 = inny pocisk
        private static readonly int[] HitN = new int[6];
        private static readonly long[] HitDmg = new long[6];
        private static readonly int[] HitMax = new int[6];
        private static int _chestN; private static long _chestDmg;
        private static int _mountsBarded, _mountsOther, _killedBarded, _killedOther;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => Apply());
        }

        public override void SyncData(IDataStore dataStore) { }

        /// <summary>Pancerz konia sprzed sufitu - do wyceny i kwitu (ArmsPricing.CostWeight). Sztuka spoza listy: wartosc biezaca.</summary>
        internal static int BookBody(ItemObject it, int live)
        {
            int[] o;
            var d = _orig;
            return d.Count > 0 && it != null && d.TryGetValue(it, out o) ? o[1] : live;
        }

        /// <summary>Suma czterech pol sprzed sufitu - do jednostek materialu i puli zuzycia. Sztuka spoza listy: wartosc biezaca.</summary>
        internal static int BookSum(ItemObject it, int live)
        {
            int[] o;
            var d = _orig;
            return d.Count > 0 && it != null && d.TryGetValue(it, out o) ? o[0] + o[1] + o[2] + o[3] : live;
        }

        private static void Apply()
        {
            var old = _orig;
            var neu = new Dictionary<ItemObject, int[]>();
            _on = false; _changed = 0;
            try
            {
                var c = Settings.Current;
                if (c == null) { _orig = neu; return; }
                _cap = Math.Max(CapMin, Math.Min(CapMax, c.RotBardingArmorCap));
                int capHead = (int)Math.Round(_cap * HeadShare);
                var fHead = AccessTools.Field(typeof(ArmorComponent), "<HeadArmor>k__BackingField");
                var fBody = AccessTools.Field(typeof(ArmorComponent), "<BodyArmor>k__BackingField");
                var fArm = AccessTools.Field(typeof(ArmorComponent), "<ArmArmor>k__BackingField");
                var fLeg = AccessTools.Field(typeof(ArmorComponent), "<LegArmor>k__BackingField");
                var getOv = AccessTools.PropertyGetter(typeof(ItemObject), "TierfOverride");
                var setOv = AccessTools.PropertySetter(typeof(ItemObject), "TierfOverride");
                bool fields = fHead != null && fBody != null && fArm != null && fLeg != null;
                bool model = false;
                try { model = Game.Current != null && Game.Current.BasicModels != null && Game.Current.BasicModels.ItemValueModel != null; } catch { }
                string off = !c.RotBardingCapEnabled ? "klucz ROT Plate Barding Can Be Pierced NIE - ladry plytowe jak w danych ROT"
                           : !fields ? "brak pol ArmorComponent (inna wersja gry) - bez zmian"
                           : getOv == null || setOv == null || !model ? "brak ItemObject.TierfOverride albo modelu wartosci - bez zamrozenia tieru nie ruszam (tier rzedu liczy sie z pancerza)" : null;
                _on = off == null;

                int over = 0, under = 0, missing = 0, odd = 0, tierMoved = 0, restored = 0;
                var sb = new StringBuilder(); var sbMiss = new StringBuilder();
                foreach (var id in BardingIds)
                {
                    try
                    {
                        var it = MBObjectManager.Instance.GetObject<ItemObject>(id);
                        if (it == null) { missing++; sbMiss.Append(sbMiss.Length > 0 ? ", " : "").Append(id); continue; }
                        var a = it.ArmorComponent;
                        if (it.ItemType != ItemObject.ItemTypeEnum.HorseHarness || a == null || a.MaterialType != ArmorComponent.ArmorMaterialTypes.Plate) { odd++; continue; }
                        // ta sama sztuka drugi raz w tym procesie: najpierw wartosci sprzed sufitu (NIE = dane ROT, suwak sie nie sklada)
                        int[] o;
                        if (fields && old != null && old.TryGetValue(it, out o))
                        { fHead.SetValue(a, o[0]); fBody.SetValue(a, o[1]); fArm.SetValue(a, o[2]); fLeg.SetValue(a, o[3]); restored++; }
                        int h0 = a.HeadArmor, b0 = a.BodyArmor, a0 = a.ArmArmor, l0 = a.LegArmor;
                        int h1 = Math.Min(h0, capHead), b1 = Math.Min(b0, _cap), a1 = Math.Min(a0, _cap), l1 = Math.Min(l0, _cap);
                        if (h1 == h0 && b1 == b0 && a1 == a0 && l1 == l0) { under++; neu[it] = new[] { h0, b0, a0, l0 }; continue; }
                        over++;
                        // sztuka w zbiorze takze przy kluczu NIE - pomiar "przed" w linii konca misji idzie w te same rubryki (ksiega = wartosci biezace)
                        neu[it] = new[] { h0, b0, a0, l0 };
                        if (!_on) continue;
                        float tf = it.Tierf; int g0 = (int)it.Tier;
                        if (!Freeze(it, tf, getOv, setOv)) { odd++; over--; continue; }
                        fHead.SetValue(a, h1); fBody.SetValue(a, b1); fArm.SetValue(a, a1); fLeg.SetValue(a, l1);
                        if ((int)it.Tier != g0) tierMoved++;
                        _changed++;
                        if (_changed <= 4)
                            sb.Append(sb.Length > 0 ? ", " : "").Append(id).Append(' ')
                              .Append(h0).Append('/').Append(b0).Append('/').Append(a0).Append('/').Append(l0).Append(" -> ")
                              .Append(h1).Append('/').Append(b1).Append('/').Append(a1).Append('/').Append(l1);
                    }
                    catch (Exception e) { odd++; Log.Error("HorseBarding419.Apply " + id, e); }
                }
                _orig = neu;
                if (!_on)
                    Log.Info("Ladry ROT (419): " + off + " | ladrow plytowych z listy powyzej sufitu " + _cap + ": " + over + ", w sufcie " + under + ", brak w grze " + missing
                             + ", pominiete (inny typ / material) " + odd + (restored > 0 ? ", przywrocone do danych ROT " + restored : "") + " (tylko pomiar w linii konca misji).");
                else
                    Log.Info("Ladry ROT (419): sufit pancerza ladrow plytowych " + _cap + " (glowa " + capHead + ") | obnizone " + _changed + ", juz w sufcie " + under
                             + ", brak w grze " + missing + (missing > 0 ? " (" + sbMiss + ")" : "") + ", pominiete (inny typ / material / blad) " + odd
                             + " | tier zamrozony (TierfOverride), zmienionych tierow " + tierMoved
                             + " | wycena, kwit kuzni i pula zuzycia licza z wartosci sprzed sufitu; waga, cena gry i HP koni bez zmian"
                             + " | glowa/tulow/szyja/nogi: " + (_changed > 0 ? sb + (_changed > 4 ? ", ..." : "") : "-")
                             + " | swiadomie pominiete: " + string.Join(", ", SkippedByDecision) + ".");
            }
            catch (Exception e) { _orig = neu; Log.Error("HorseBarding419.Apply", e); }
        }

        /// <summary>Tier sprzed zmiany wpisany do TierfOverride (wartosc + 1), o ile sztuka nie ma juz wlasnego. Zwraca false, gdy sie nie udalo.</summary>
        private static bool Freeze(ItemObject it, float tierfBefore, MethodInfo getOv, MethodInfo setOv)
        {
            try
            {
                float cur = (float)getOv.Invoke(it, null);
                if (cur >= 1f) return true;                       // tier_override z XML albo wczesniejsze zamrozenie - tier i tak nie zalezy od pol
                setOv.Invoke(it, new object[] { Math.Max(0f, tierfBefore) + 1f });
                return true;
            }
            catch { return false; }
        }

        private static void Reset()
        {
            for (int i = 0; i < 6; i++) { HitN[i] = 0; HitDmg[i] = 0; HitMax[i] = 0; }
            _chestN = 0; _chestDmg = 0;
            _mountsBarded = 0; _mountsOther = 0; _killedBarded = 0; _killedOther = 0;
        }

        private static string Cell(int i)
        {
            return HitN[i] + " / " + (HitN[i] > 0 ? (HitDmg[i] / HitN[i]).ToString() : "-") + " / " + HitMax[i];
        }

        private static void Flush(Mission m)
        {
            if (_mountsBarded + _mountsOther == 0) return;      // misja bez wierzchowcow - bez linii
            string kind = "?";
            try { kind = MissionTeams.Kind(m); } catch { }
            Log.Info("Ladry (419): koniec misji (" + kind + ") | sufit " + (_on ? "TAK " + _cap + " (obnizonych sztuk " + _changed + ")" : "NIE")
                     + " | wierzchowce w misji: w ladrach plytowych ROT " + _mountsBarded + ", pozostale " + _mountsOther
                     + " | pociski w konie w ladrach (trafienia / srednio HP / najwiecej): grot przebijajacy - " + Cell(0)
                     + (_chestN > 0 ? " (w piers " + _chestN + ", srednio " + (_chestDmg / _chestN) + ")" : "")
                     + "; grot szeroki - " + Cell(1) + "; inne pociski - " + Cell(2)
                     + " | w pozostale wierzchowce: grot przebijajacy - " + Cell(3) + "; grot szeroki - " + Cell(4) + "; inne pociski - " + Cell(5)
                     + " | zabite pociskiem: w ladrach " + _killedBarded + ", pozostale " + _killedOther + ".");
            Reset();
        }

        /// <summary>0 = wierzchowiec w ladrach z listy (takze przy kluczu NIE - pomiar "przed"), 1 = inny. Bez alokacji.</summary>
        private static int Group(Agent mount)
        {
            var d = _orig;
            if (d.Count == 0) return 1;
            var eq = mount.SpawnEquipment;
            if (eq == null) return 1;
            var it = eq[EquipmentIndex.HorseHarness].Item;
            return it != null && d.ContainsKey(it) ? 0 : 1;
        }

        /// <summary>Liczniki misji: wierzchowce w ladrach, trafienia pociskow w konie wedlug grotu, konie zabite pociskiem; linia sumy na koncu.
        /// Dziala takze przy kluczu NIE. Bez alokacji, bez try/catch w sciezce trafien.</summary>
        internal sealed class Logic : MissionLogic
        {
            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            public override void OnBehaviorInitialize()
            {
                base.OnBehaviorInitialize();
                Reset();
            }

            public override void OnAgentBuild(Agent agent, Banner banner)
            {
                if (agent == null || agent.IsHuman) return;
                if (Group(agent) == 0) _mountsBarded++; else _mountsOther++;
            }

            public override void OnScoreHit(Agent affectedAgent, Agent affectorAgent, WeaponComponentData attackerWeapon, bool isBlocked, bool isSiegeEngineHit,
                in Blow blow, in AttackCollisionData collisionData, float damagedHp, float hitDistance, float shotDifficulty)
            {
                if (affectedAgent == null || affectedAgent.IsHuman || !blow.IsMissile) return;
                int k = 2;
                if (attackerWeapon != null && (attackerWeapon.WeaponClass == WeaponClass.Arrow || attackerWeapon.WeaponClass == WeaponClass.Bolt))
                    k = blow.DamageType == DamageTypes.Pierce ? 0 : (blow.DamageType == DamageTypes.Cut ? 1 : 2);
                int g = Group(affectedAgent);
                int i = g * 3 + k;
                int dmg = blow.InflictedDamage;
                HitN[i]++;
                HitDmg[i] += dmg;
                if (dmg > HitMax[i]) HitMax[i] = dmg;
                if (i == 0 && blow.VictimBodyPart == BoneBodyPartType.Chest) { _chestN++; _chestDmg += dmg; }
            }

            public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
            {
                if (affectedAgent == null || affectedAgent.IsHuman || agentState != AgentState.Killed || !blow.IsMissile) return;
                if (Group(affectedAgent) == 0) _killedBarded++; else _killedOther++;
            }

            protected override void OnEndMission()
            {
                try { Flush(Mission); } catch (Exception e) { Log.Error("HorseBarding419.Flush", e); }
                base.OnEndMission();
            }
        }
    }
}
