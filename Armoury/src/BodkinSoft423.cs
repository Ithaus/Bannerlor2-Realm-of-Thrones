using System;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 423b (PROJEKT-423-po-krytyce p. 5, B11): IGLICA W MIEKKI PANCERZ - TRZY CZWARTE ZAMIAST POLOWY.
    ///
    /// Skad polowa (rbm.cs:5951-5954, Utilities.RBMComputeDamage): gdy material pancerza trafionej czesci jest inny niz None i Plate, obrazenia sa
    /// Pierce, a bron to Arrow albo Bolt, RBM liczy pancerz x0.5. Material piersi i brzucha = material pierwszej sztuki typu BodyArmor w slotach
    /// pancerza (ArmorRework.getChestArmorMaterial / getAbdomenArmorMaterial, rbm.cs:190-230). Polowe strojono pod drabine RBM (kolczuga 50-79);
    /// po 423a kolczuga ROT ma 55, wiec prog iglicy = 55 J - iglica 100 lb (101 J) dawalaby 45 HP w kolczuge i 70 w przeszywanice, a longbow 180 lb
    /// zabijal kolczuge jedna strzala (101 HP). Zrodla: przeszywanica 50-80 J, kolczuga 80-120 J.
    ///
    /// REGULA. Wlasny prefiks (Priority.First - przed RBM) na MissionCombatMechanicsHelper.ComputeBlowDamage mnozy pancerz wejsciowy
    /// (AttackInformation.ArmorAmountFloat) przez procent / 50 - po x0.5 RBM wychodzi procent / 100. Ten sam chwyt co lezacy 415 i wypoczety 416
    /// (mnozniki tego samego pola, przemienne; struktura budowana na jedno trafienie, mb_full.cs:58466-58470). Warunki dokladnie jak polowa RBM,
    /// zawezone do tulowia czlowieka: pocisk, bron Arrow / Bolt, Pierce, trafienie w piers albo brzuch, korpus ofiary z materialem innym niz None
    /// i Plate. KAZDY taki korpus (ROT, RBM, gra) - jedna regula na jedno zjawisko. procent = 50 + (BodkinSoftArmourPercent - 50) x sila drabiny;
    /// drabina wylaczona albo klucz 50 = regula RBM nietknieta (prefiks tylko liczy). Progi iglicy po 423a + 423b: ubranie 18 J, przeszywanica 39,
    /// ciezka skora 72, kolczuga 82, luska 93; plyta bez zmian 182.
    ///
    /// Czego nie obejmuje: glowa, szyja, barki, rece, nogi (tam zostaje polowa RBM - inne sztuki, inna skala), konie (419), strzaly szerokie (Cut).
    /// Rzadki przypadek pominiety swiadomie: RBM zamienia typ obrazen na obuch, gdy napastnik trzyma w chwili trafienia bron kuta i cios nie
    /// trafil ostrzem (rbm.cs:3409-3413) - wtedy polowy nie ma, wiec i my nie ruszamy.
    /// Jedna regula dla gracza i AI. Brak RBM = nieczynne (linia logu). Latka w kampanii (NightHooks.InCampaign, raz na proces) - jak 415 / 416.
    /// </summary>
    internal static class BodkinSoft423
    {
        private const float RbmHalfPercent = 50f;

        private static int _patched, _wanted;
        private static string _note = "nie zakladano";
        private static MethodBase _blow;                     // MissionCombatMechanicsHelper.ComputeBlowDamage
        private static bool _rbmBlow;                        // prefiks RBM (z polowa pancerza) jest teraz na ComputeBlowDamage

        // liczniki misji (glowny watek: ComputeBlowDamage i OnScoreHit ida z Mission.GetAttackCollisionResults / RegisterBlow)
        private static int _blows, _changed;
        private static float _armBefore, _armAfter;
        private static Agent _last;                          // ofiara ostatniej iglicy objetej regula - do licznika trafien
        private static int _hits, _hitsDeep;
        private static long _hitHp;

        /// <summary>NightHooks.InCampaign (ArmouryBehavior.OnSessionLaunched, raz na proces). Zwraca opis do linii "Latki nocy (K3): kampania".</summary>
        internal static string ApplyInCampaign(Harmony h)
        {
            if (h == null) { _note = "brak Harmony"; return _note; }
            _patched = 0; _wanted = 0;
            string miss = "";
            Type t = null;
            try { t = AccessTools.TypeByName("RBMCombat.ArmorRework"); } catch { }
            if (t == null)
            {
                _note = "RBM nieobecny (brak RBMCombat.ArmorRework) - regula iglicy nieczynna";
                Log.Info("Iglica w miekki pancerz (423b): " + _note + ".");
                return _note;
            }
            _wanted++;
            MethodInfo blow = null;
            try { blow = AccessTools.DeclaredMethod(typeof(MissionCombatMechanicsHelper), "ComputeBlowDamage"); } catch { }
            _blow = blow;
            if (blow == null) miss += " brak MissionCombatMechanicsHelper.ComputeBlowDamage;";
            else
            {
                try
                {
                    h.Patch(blow, prefix: new HarmonyMethod(typeof(BodkinSoft423), nameof(SoftPrefix)) { priority = Priority.First });
                    if (Owned(blow, h.Id)) _patched++; else miss += " prefiks NIE wszedl;";
                }
                catch (Exception e) { miss += " prefiks BLAD " + e.GetType().Name + ";"; Log.Error("BodkinSoft423.Patch(blow)", e); }
                RefreshRbm();
                if (!_rbmBlow) miss += " ComputeBlowDamage teraz bez prefiksu RBM (modul walki RBM wylaczony?) - czeka;";
            }
            var s = Settings.Current;
            _note = "iglica w miekki pancerz " + _patched + "/" + _wanted + (miss.Length > 0 ? " |" + miss : "");
            Log.Info("Iglica w miekki pancerz (423b): latka raz na proces - " + _note + ". Klucz BodkinSoftArmourPercent " + (s != null ? s.BodkinSoftArmourPercent.ToString() : "?")
                     + ": strzala / belt Pierce w piers albo brzuch czlowieka, korpus inny niz plyta - pancerz x procent / 50 przed prefiksem RBM (RBM x0.5 -> procent / 100);"
                     + " procent = 50 + (klucz - 50) x sila drabiny 423 (ustalana przy wczytaniu zapisu; drabina wylaczona = 50 = regula RBM).");
            return _note;
        }

        /// <summary>RBM zdejmuje i zaklada swoje latki przy zmianie ustawien - sprawdzane przy zakladaniu i na starcie kazdej misji, nie przy ciosie.</summary>
        internal static void RefreshRbm()
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
        }

        private static bool Owned(MethodBase mi, string id)
        {
            try
            {
                var info = Harmony.GetPatchInfo(mi);
                if (info == null) return false;
                foreach (var p in info.Prefixes)
                    if (p != null && p.owner == id && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(BodkinSoft423)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>Procent pancerza liczony dla iglicy w miekki korpus (50 = RBM). Sila drabiny z chwili wczytania zapisu (ArmourLadder423.Strength).</summary>
        internal static float Percent()
        {
            var s = Settings.Current;
            if (s == null) return RbmHalfPercent;
            float key = s.BodkinSoftArmourPercent;
            if (!(key > RbmHalfPercent)) return RbmHalfPercent;
            if (key > 100f) key = 100f;
            return RbmHalfPercent + (key - RbmHalfPercent) * ArmourLadder423.Strength;
        }

        /// <summary>Przed prefiksem RBM na ComputeBlowDamage. Bez alokacji, bez try/catch: same odczyty struktur i pol (glowny watek).
        /// Licznik iglic rosnie takze przy procencie 50 (pomiar "przed").</summary>
        private static void SoftPrefix(ref AttackInformation attackInformation, ref AttackCollisionData attackCollisionData, WeaponComponentData attackerWeapon, DamageTypes damageType)
        {
            if (!_rbmBlow || attackerWeapon == null || damageType != DamageTypes.Pierce || !attackCollisionData.IsMissile) return;
            var wc = attackerWeapon.WeaponClass;
            if (wc != WeaponClass.Arrow && wc != WeaponClass.Bolt) return;
            var part = attackCollisionData.VictimHitBodyPart;
            if (part != BoneBodyPartType.Chest && part != BoneBodyPartType.Abdomen) return;
            var v = attackInformation.VictimAgent;
            if (v == null || !v.IsHuman) return;
            var eq = v.SpawnEquipment;
            if (eq == null) return;
            // material tulowia jak w RBM: pierwsza sztuka typu BodyArmor w slotach pancerza
            var mat = ArmorComponent.ArmorMaterialTypes.None;
            for (EquipmentIndex i = EquipmentIndex.NumAllWeaponSlots; i < EquipmentIndex.ArmorItemEndSlot; i++)
            {
                var it = eq[i].Item;
                if (it != null && it.ItemType == ItemObject.ItemTypeEnum.BodyArmor && it.ArmorComponent != null) { mat = it.ArmorComponent.MaterialType; break; }
            }
            if (mat == ArmorComponent.ArmorMaterialTypes.None || mat == ArmorComponent.ArmorMaterialTypes.Plate) return;
            // RBM zamieni Pierce na obuch (bez polowy), gdy napastnik trzyma bron kuta i cios "nie ostrzem" - nie nasz przypadek
            var a = attackInformation.AttackerAgent;
            if (a != null && attackCollisionData.StrikeType == 0)
            {
                var held = a.WieldedWeapon;
                if (!held.IsEmpty && held.Item != null && held.Item.WeaponDesign != null) return;
            }
            _blows++;
            _last = v;
            float pct = Percent();
            if (!(pct > RbmHalfPercent + 0.01f)) return;                                     // 50 = RBM bez zmian
            float arm = attackInformation.ArmorAmountFloat;
            if (!(arm > 0f)) return;
            float b = arm * (pct / RbmHalfPercent);
            attackInformation.ArmorAmountFloat = b;
            _changed++; _armBefore += arm * 0.5f; _armAfter += b * 0.5f;
        }

        /// <summary>Wolane z ArmourLadder423.Logic.OnScoreHit przy KAZDYM trafieniu (znacznik zdejmowany zawsze). Bez alokacji.</summary>
        internal static void OnHit(Agent affected, bool blocked, int damage)
        {
            var last = _last;
            _last = null;
            if (last == null || blocked || !ReferenceEquals(last, affected)) return;
            _hits++; _hitHp += damage;
            if (damage >= ArmourLadder423.DeepHp) _hitsDeep++;
        }

        internal static void Reset()
        {
            _blows = 0; _changed = 0; _armBefore = 0f; _armAfter = 0f;
            _last = null; _hits = 0; _hitsDeep = 0; _hitHp = 0;
        }

        private static string F1(float v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }

        /// <summary>Czlon linii "Drabina pancerzy (423): koniec misji".</summary>
        internal static string Tail()
        {
            return " | iglica w miekki korpus (423b): latka " + _patched + "/" + _wanted + (_rbmBlow ? "" : " (prefiks RBM NIEOBECNY - nieczynne)")
                   + ", pancerz liczony w " + F1(Percent()) + "% (RBM 50), ciosow objetych " + _blows + ", pancerz zmieniony " + _changed
                   + " (srednio po polowie RBM " + (_changed > 0 ? F1(_armBefore / _changed) + " -> " + F1(_armAfter / _changed) : "-") + ")"
                   + ", trafienia " + _hits + " srednio " + (_hits > 0 ? (_hitHp / _hits).ToString() : "-") + " HP, od " + ArmourLadder423.DeepHp + " HP " + _hitsDeep;
        }
    }
}
