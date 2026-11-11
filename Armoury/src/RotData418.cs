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
    /// 418 (audyt RBM 11.11, B3 + B6): TARCZE ROT W SKALI 4-6 + UBRANIA BEZ PANCERZA ZBROI. Sama korekta danych w pamieci, raz na start sesji
    /// (wzor: Uniques.Apply - korony; CrashScribe Mends.ArmorSanity). Bez edycji XML, bez latek Harmony.
    ///
    /// B3. ROTassets.xml daje tarczom body_armor ALBO 1 (25 sztuk), ALBO 7-9 (26 sztuk), jedna ma 4. RBM liczy obrazenia tarczy z pancerz x 10
    /// i progiem przebicia pancerz x 10 x 5 (RBMComputeBlowDamageOnShield, rbm.cs:3614; WeaponTypeDamage, rbm.cs:6042): przy pancerzu 1 prog = 50,
    /// wiec kazdy topor przebija (topor 2h z premia na tarcze: 197 pkt na cios, tarcza 647 HP pada po 4 ciosach), od pancerza 2 w gore nic juz nie
    /// przebija i zostaje sam uraz tepy (pancerz 9: 33 pkt, 20 ciosow drewno / 51 metal). Tarcze RBM maja drewno 4-6, stal 9-15. Regula: pancerz
    /// tarczy ROT przyciety do przedzialu [RotShieldArmorMin, RotShieldArmorMax] (domyslnie 4-6): 1 -> 4, 7-9 -> 6. HP tarcz bez zmian.
    ///
    /// B6. Tabela wyjatkow po id: ubranie o wadze sukni z pancerzem kolczugi / plyty (kryterium: zbroja ROT z pancerzem tulowia od 80 przy co
    /// najmniej 20 pkt na kg; do tego wlosy Daenerys - "helm" 150 przy 0.2 kg). Wartosci po zmianie = szlachecka suknia ROT z tkaniny
    /// (andal_noble_dress*: 14 / 4 / 4), dla sukni z elementami zbroi Cersei = skorzany stroj dworski ROT (cersei_red_dress, ned_clothes: 44 / 17 / 17;
    /// drabina 423 mapuje potem obie tak samo, 44 -> 15), dla wlosow = 418-p: poziom noble_default, niewidzialnego helmu 125 / 62, ktory nosi kazdy lord
    /// ROT (wartosci czytane z tej sztuki przy starcie) - dopoki nie ma decyzji o noble_default, Daenerys nie jest jedynym bohaterem z gola glowa
    /// (w 418 bylo 10 / 5 jak korony w Uniques). Tylko w dol (min z obecna wartoscia). material_type bez zmian.
    ///
    /// SWIADOMIE POMINIETE (decyzja Jeffa 11.11 "4. ok zostawiamy"): Dothrakowie i Nieskalani - patrz SkippedByDecision. Pominiete z innych
    /// powodow (do osobnej decyzji): Inni (nightking_armor, nightking_armor2, whitewalker_armor, nightking_spikes - nadnaturalni), noble_default
    /// (niewidzialny helm 125 szlachty ROT - 134 uzycia we wzorcach lordow; zdjecie = kazdy lord z gola glowa), valyrian_surcoat_master (kolczuga
    /// 207 na wszystkim przy 2 kg - rynsztunek wedrowca Maegora, nie suknia), stroje dworskie 44 (XML 20 x 2.2 - masowa drabina skor ROT, sprawa B5).
    ///
    /// TIERY I CENY BEZ ZMIAN: ItemObject.Tierf NIE jest zapamietany - kazde czytanie liczy go modelem gry z biezacych pol (RBM: tarcza
    /// ((HP - 400) x 0.005 + pancerz x 0.2) x (dlugosc / 60) + 1; zbroja body x 0.05 + leg x 0.035 + arm x 0.025 - 1). Bez zabezpieczenia tarcza
    /// 1 -> 4 przy dlugosci 95 skoczylaby o 0.95 tieru, a suknia Daenerys spadla z 6 na 1 - a od tieru zalezy u nas Recipes.Grade (55 miejsc),
    /// wymog umiejetnosci, dobor sprzetu wojsk, koszyki AI. Dlatego PRZED zmiana pola czytamy Tierf i wpisujemy go do ItemObject.TierfOverride
    /// (wartosc + 1; ten sam mechanizm co atrybut XML tier_override i co 175b w CrashScribe) - tier zostaje co do ulamka (override dziala od 1.0,
    /// wiec Tierf ujemny - w tych listach nie wystepuje - bylby przyciety do 0; calkowity Tier i tak ten sam). Cena gry (Value) jest
    /// zapamietana przy wczytaniu, kategoria towaru tez. Bez settera TierfOverride sztuka NIE jest ruszana (linia logu).
    ///
    /// Kolejnosc (418-p, sprostowanie): MbEvent.AddNonSerializedListener dopisuje nasluch na POCZATEK listy, a Invoke idzie od glowy
    /// (cs_full.cs:22405-22424) - zachowanie zarejestrowane POZNIEJ biegnie WCZESNIEJ. 418 jest rejestrowane po ArmouryBehavior, wiec jego
    /// OnSessionLaunched biegnie PRZED ArmouryBehavior.OnSessionLaunched (ArmsPricing.Build liczy juz po 418), a nie po nim, jak mowil opis 418.
    /// Dlatego miejsca, ktore licza koszt / material / zuzycie z pol pancerza sztuk z tabeli ubran, biora wartosci SPRZED 418 z ksiegi (BookBody /
    /// BookHead / BookSum): HeroDress382.CivilBody (klasa "zbroja czy stroj" - stroje Varysa, Tyriona i Baelisha zostaja zbroja jak przed 418, 382c
    /// ich nie przebiera, 389 nie doposaza), ArmouryBehavior.ArmorPool (pula zuzycia), Recipes.ArmourUnits (material naprawy i przetopu),
    /// ArmsPricing.HelmKgPerArmor / HelmCostWeight (dany_hair to "helm plytowy"). Tarcze: wycena i receptury ida z wagi i zamrozonego tieru.
    /// Wzgledem CrashScribe (ArmorSanity) kolejnosc obojetna: tarcz ArmorSanity nie dotyka, a wszystkie sztuki z tabeli ubran sa NotMerchandise
    /// (ArmorSanity je pomija); tier zamrozony, wiec grupy (typ, tier) maja ten sam sklad. Obiekty przedmiotow sa nowe w kazdej grze - klucz NIE dziala
    /// od nastepnego wczytania; gdyby te same obiekty przyszly drugi raz, ubrania najpierw wracaja do wartosci z ksiegi (NIE = dane ROT).
    /// Regula nie zna wlasciciela: gracz i AI jednakowo.
    /// </summary>
    internal sealed class RotData418 : CampaignBehaviorBase
    {
        // wszystkie tarcze ROT-Content/ROTassets.xml poza Nieskalanymi (zrzut 11.11: 50 sztuk; w nawiasie body_armor z XML)
        private static readonly string[] ShieldIds =
        {
            // pancerz 1 (23)
            "arryn_shield", "baratheon_shield", "baratheon_shield2", "bolton_kite_shield", "bolton_oval_shield", "bolton_shield", "braavosi_shield",
            "bronze_shield", "dayne_shield", "dragonstone_shield", "golden_company_shield", "golden_company_shield2", "greyjoy_shield",
            "lannister_kite_shield", "lyseni_shield", "mormont_shield", "myrish_shield", "sarnori_shield", "spiked_shield", "targaryen_shield2",
            "targaryen_shield3", "tyrell_shield", "tyroshi_shield",
            // pancerz 7-9 (26; lannister_shield 8, targ_shield 7, reszta 9)
            "grafton_shield", "ibb_shield", "ibb_shield2", "lannister_shield", "lion_shield", "lorathi_shield", "lorathi_shield2", "mallister_shield",
            "manderly_heater_shield", "manderly_shield", "martell_shield", "pentoshi_shield", "pentoshi_shield2", "qarth_shield", "qarth_shield2",
            "qohorik_shield", "qohorik_shield2", "skagosi_shield", "skagosi_shield2", "stark_shield", "targ_shield", "thenn_shield", "westerling_shield",
            "wildling_shield2", "yiti_shield", "yiti_shield2",
            // pancerz 4 (1) - juz w skali, na liscie dla pomiaru
            "wildling_shield1",
        };

        // id | typ | glowa | tulow | rece | nogi [| sztuka-wzor]  (wartosci docelowe; stosowane jako min z obecna; 418-p: gdy sztuka-wzor istnieje,
        // wartosci docelowe czytane sa z niej, a liczby z wiersza sa zapasem na brak wzoru)
        private static readonly string[] ClothRows =
        {
            "dany_dress|B|0|14|4|4",
            "cersei_dress|B|0|14|4|4",
            "margaery_dress|B|0|14|4|4",
            "melisandre_dress|B|0|14|4|4",
            "varys_clothes|B|0|14|4|4",
            "tyrion_clothes|B|0|14|4|4",
            "baelish_clothes|B|0|14|4|4",
            "cersei_armor|B|0|44|17|17",
            "dany_hair|H|10|5|0|0|noble_default",
        };

        // decyzja Jeffa 11.11 (pytanie 4): Dothrakowie i Nieskalani bez zmian - do linii logu, nigdy nie ruszane
        internal static readonly string[] SkippedByDecision =
        {
            "dothraki_armor", "dothraki_armor2", "dothraki_armor3", "dothraki_armor4", "dothraki_armor5", "dothraki_armor6", "dothraki_armor8",
            "dothraki_hair", "dothraki_boots", "unsullied_armor", "unsullied_soldier_armor", "unsullied_helmet", "unsullied_pikeman_helmet",
            "unsullied_boots", "unsullied_shield", "unsullied_pikeman_shield",
        };

        private const byte ClassRaised = 1, ClassLowered = 2, ClassKept = 3;

        // stan sesji (czyszczony w Apply): klasa tarczy ROT i sztuki z tabeli ubran - do licznikow misji
        private static readonly Dictionary<ItemObject, byte> ShieldClass = new Dictionary<ItemObject, byte>();
        private static readonly HashSet<ItemObject> ClothItems = new HashSet<ItemObject>();
        private static bool _shieldsOn, _clothOn;
        private static int _lo = 4, _hi = 6;

        // 418-p: ksiega - sztuka z tabeli ubran zmieniona przez 418 -> pola sprzed zmiany (glowa, tulow, rece, nogi); podmieniana w calosci (czytaja ja
        // watki wyceny). Pusta przy kluczu NIE i dla sztuk nieruszonych - wtedy wartosc biezaca = dane ROT.
        private static Dictionary<ItemObject, int[]> _orig = new Dictionary<ItemObject, int[]>();

        /// <summary>418-p: pancerz tulowia sprzed 418 (klasa stroju w HeroDress382.CivilBody). Sztuka spoza ksiegi: wartosc podana.</summary>
        internal static int BookBody(ItemObject it, int live)
        {
            int[] o;
            var d = _orig;
            return d.Count > 0 && it != null && d.TryGetValue(it, out o) ? o[1] : live;
        }

        /// <summary>418-p: pancerz glowy sprzed 418 (koszt helmu plytowego - dany_hair). Sztuka spoza ksiegi: wartosc podana.</summary>
        internal static int BookHead(ItemObject it, int live)
        {
            int[] o;
            var d = _orig;
            return d.Count > 0 && it != null && d.TryGetValue(it, out o) ? o[0] : live;
        }

        /// <summary>418-p: suma czterech pol sprzed 418 (pula zuzycia, jednostki materialu). Sztuka spoza ksiegi: suma podana.</summary>
        internal static int BookSum(ItemObject it, int live)
        {
            int[] o;
            var d = _orig;
            return d.Count > 0 && it != null && d.TryGetValue(it, out o) ? o[0] + o[1] + o[2] + o[3] : live;
        }

        // liczniki misji (glowny watek: OnMeleeHit i OnScoreHit wola Mission.MeleeHitCallback / RegisterBlow)
        private static readonly int[] HitN = new int[4];
        private static readonly long[] HitDmg = new long[4];
        private static int _broken, _clothHits, _clothMax;
        private static int _clothOwners, _clothElse;      // 418-p: bohaterowie ze sztuka z tabeli w zestawie bojowym / z tego weszli do misji bez niej
        private static float _clothHp;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => Apply());
        }

        public override void SyncData(IDataStore dataStore) { }

        private static void Apply()
        {
            ShieldClass.Clear();
            ClothItems.Clear();
            _shieldsOn = false; _clothOn = false;
            var old = _orig;
            var neu = new Dictionary<ItemObject, int[]>();
            bool done = false;
            try
            {
                var c = Settings.Current;
                if (c == null) return;      // ksiega zostaje stara (pola nieruszone - dalej prawdziwa)
                var getOv = AccessTools.PropertyGetter(typeof(ItemObject), "TierfOverride");
                var setOv = AccessTools.PropertySetter(typeof(ItemObject), "TierfOverride");
                bool model = false;
                try { model = Game.Current != null && Game.Current.BasicModels != null && Game.Current.BasicModels.ItemValueModel != null; } catch { }
                bool canFreeze = getOv != null && setOv != null && model;
                Shields(c, getOv, setOv, canFreeze);
                Clothes(c, getOv, setOv, canFreeze, old, neu);
                done = true;
                Log.Info("Dane ROT (418): swiadomie pominiete (decyzja Jeffa 11.11 - Dothrakowie i Nieskalani bez zmian): " + string.Join(", ", SkippedByDecision) + ".");
            }
            catch (Exception e) { Log.Error("RotData418.Apply", e); }
            finally
            {
                // 418-p: po wyjatku ksiega nie gubi sztuk, ktorych pola moga byc dalej obnizone (wpisy nowe maja pierwszenstwo)
                if (!done && old != null)
                    foreach (var kv in old) if (!neu.ContainsKey(kv.Key)) neu[kv.Key] = kv.Value;
                if (done || neu.Count > 0) _orig = neu;
            }
        }

        /// <summary>Tier sprzed zmiany wpisany do TierfOverride (wartosc + 1), o ile sztuka nie ma juz wlasnego. Zwraca false, gdy sie nie udalo.</summary>
        private static bool Freeze(ItemObject it, float tierfBefore, MethodInfo getOv, MethodInfo setOv)
        {
            try
            {
                float cur = (float)getOv.Invoke(it, null);
                if (cur >= 1f) return true;                       // tier_override z XML albo 175b - tier i tak nie zalezy od pol
                setOv.Invoke(it, new object[] { Math.Max(0f, tierfBefore) + 1f });
                return true;
            }
            catch { return false; }
        }

        private static void Shields(Settings c, MethodInfo getOv, MethodInfo setOv, bool canFreeze)
        {
            _lo = Math.Max(0, Math.Min(c.RotShieldArmorMin, c.RotShieldArmorMax));
            _hi = Math.Max(_lo, Math.Max(c.RotShieldArmorMin, c.RotShieldArmorMax));
            var fArmor = AccessTools.Field(typeof(WeaponComponentData), "<BodyArmor>k__BackingField");
            string off = !c.RotShieldArmorScaleEnabled ? "klucz ROT Shield Armour Scale NIE - pancerz tarcz ROT jak w danych (1 albo 7-9)"
                       : fArmor == null ? "brak pola WeaponComponentData.BodyArmor (inna wersja gry) - bez zmian"
                       : !canFreeze ? "brak ItemObject.TierfOverride albo modelu wartosci - bez zamrozenia tieru nie ruszam (tier tarczy liczy sie z pancerza)" : null;
            _shieldsOn = off == null;

            int raised = 0, lowered = 0, kept = 0, missing = 0, odd = 0, tierMoved = 0;
            var sbUp = new StringBuilder(); var sbDown = new StringBuilder(); var sbMiss = new StringBuilder();
            foreach (var id in ShieldIds)
            {
                try
                {
                    var it = MBObjectManager.Instance.GetObject<ItemObject>(id);
                    if (it == null) { missing++; sbMiss.Append(sbMiss.Length > 0 ? ", " : "").Append(id); continue; }
                    var wc = it.WeaponComponent;
                    if (it.ItemType != ItemObject.ItemTypeEnum.Shield || wc == null || wc.Weapons == null || wc.Weapons.Count == 0 || !wc.Weapons[0].IsShield)
                    { odd++; continue; }
                    int a0 = wc.Weapons[0].BodyArmor;
                    int a1 = a0 < _lo ? _lo : (a0 > _hi ? _hi : a0);
                    if (a1 == a0) { kept++; ShieldClass[it] = ClassKept; continue; }
                    // klasa takze przy kluczu NIE - pomiar "przed" w linii konca misji idzie w te same rubryki
                    if (!_shieldsOn) { ShieldClass[it] = a1 > a0 ? ClassRaised : ClassLowered; if (a1 > a0) raised++; else lowered++; continue; }
                    float tf = it.Tierf; int g0 = (int)it.Tier;
                    if (!Freeze(it, tf, getOv, setOv)) { odd++; continue; }
                    for (int i = 0; i < wc.Weapons.Count; i++)
                        if (wc.Weapons[i] != null && wc.Weapons[i].IsShield) fArmor.SetValue(wc.Weapons[i], a1);
                    if ((int)it.Tier != g0) tierMoved++;
                    if (a1 > a0) { raised++; ShieldClass[it] = ClassRaised; sbUp.Append(sbUp.Length > 0 ? ", " : "").Append(id).Append(' ').Append(a0).Append("->").Append(a1); }
                    else { lowered++; ShieldClass[it] = ClassLowered; sbDown.Append(sbDown.Length > 0 ? ", " : "").Append(id).Append(' ').Append(a0).Append("->").Append(a1); }
                }
                catch (Exception e) { odd++; Log.Error("RotData418.Shields " + id, e); }
            }
            if (!_shieldsOn)
            {
                Log.Info("Dane ROT (418): tarcze - " + off + " | poza skala " + _lo + "-" + _hi + ": ponizej " + raised + ", powyzej " + lowered + ", w skali " + kept
                         + ", brak w grze " + missing + " (tylko pomiar w linii konca misji).");
                return;
            }
            Log.Info("Dane ROT (418): tarcze - skala " + _lo + "-" + _hi + " | podniesione " + raised + ", obnizone " + lowered + ", juz w skali " + kept
                     + ", brak w grze " + missing + (missing > 0 ? " (" + sbMiss + ")" : "") + ", pominiete (nie tarcza / blad) " + odd
                     + " | tier zamrozony (TierfOverride), zmienionych tierow " + tierMoved + " | HP tarcz, cena i kategoria bez zmian.");
            if (raised > 0) Log.Info("Dane ROT (418): tarcze podniesione: " + sbUp + ".");
            if (lowered > 0) Log.Info("Dane ROT (418): tarcze obnizone: " + sbDown + ".");
        }

        private static void Clothes(Settings c, MethodInfo getOv, MethodInfo setOv, bool canFreeze, Dictionary<ItemObject, int[]> old, Dictionary<ItemObject, int[]> neu)
        {
            var fHead = AccessTools.Field(typeof(ArmorComponent), "<HeadArmor>k__BackingField");
            var fBody = AccessTools.Field(typeof(ArmorComponent), "<BodyArmor>k__BackingField");
            var fArm = AccessTools.Field(typeof(ArmorComponent), "<ArmArmor>k__BackingField");
            var fLeg = AccessTools.Field(typeof(ArmorComponent), "<LegArmor>k__BackingField");
            bool fields = fHead != null && fBody != null && fArm != null && fLeg != null;
            // 418-p: te same obiekty drugi raz w tym procesie - najpierw wartosci sprzed 418 (NIE = dane ROT; ksiega nie zapisuje wartosci juz obnizonych)
            int restored = 0;
            if (old != null && old.Count > 0)
                foreach (var kv in old)
                {
                    try
                    {
                        var ac = kv.Key != null ? kv.Key.ArmorComponent : null;
                        if (ac == null) continue;
                        if (!fields) { neu[kv.Key] = kv.Value; continue; }      // nie da sie przywrocic - wpis zostaje
                        fHead.SetValue(ac, kv.Value[0]); fBody.SetValue(ac, kv.Value[1]); fArm.SetValue(ac, kv.Value[2]); fLeg.SetValue(ac, kv.Value[3]);
                        restored++;
                    }
                    catch { }
                }
            // zbior sztuk takze przy kluczu NIE - pomiar "przed" (trafienia w noszacych) w linii konca misji
            foreach (var row in ClothRows)
            {
                try { var it0 = MBObjectManager.Instance.GetObject<ItemObject>(row.Substring(0, row.IndexOf('|'))); if (it0 != null && it0.ArmorComponent != null) ClothItems.Add(it0); }
                catch { }
            }
            if (!c.ClothingArmorExceptionsEnabled)
            {
                Log.Info("Dane ROT (418): ubrania - klucz Dresses Are Not Plate NIE, suknie i stroje imienne jak w danych ROT (tylko pomiar w linii konca misji)"
                         + (restored > 0 ? "; przywrocone do danych ROT " + restored : "") + ".");
                return;
            }
            if (!fields)
            { Log.Info("Dane ROT (418): ubrania - brak pol ArmorComponent (inna wersja gry), bez zmian."); return; }
            if (!canFreeze) { Log.Info("Dane ROT (418): ubrania - brak ItemObject.TierfOverride albo modelu wartosci; bez zamrozenia tieru nie ruszam."); return; }
            _clothOn = true;

            int changed = 0, same = 0, missing = 0, odd = 0, tierMoved = 0;
            var sb = new StringBuilder();
            foreach (var row in ClothRows)
            {
                string id = row;
                try
                {
                    var p = row.Split('|');
                    id = p[0];
                    var want = p[1] == "H" ? ItemObject.ItemTypeEnum.HeadArmor : ItemObject.ItemTypeEnum.BodyArmor;
                    int th = int.Parse(p[2]), tb = int.Parse(p[3]), ta = int.Parse(p[4]), tl = int.Parse(p[5]);
                    if (p.Length > 6)
                    {
                        // 418-p: wartosci docelowe ze sztuki-wzoru (dany_hair = noble_default, niewidzialny helm lordow ROT)
                        var model = MBObjectManager.Instance.GetObject<ItemObject>(p[6]);
                        var ma = model != null && model.ItemType == want ? model.ArmorComponent : null;
                        if (ma != null) { th = ma.HeadArmor; tb = ma.BodyArmor; ta = ma.ArmArmor; tl = ma.LegArmor; sb.Append(sb.Length > 0 ? ", " : "").Append("[wzor ").Append(p[6]).Append(']'); }
                    }
                    var it = MBObjectManager.Instance.GetObject<ItemObject>(id);
                    if (it == null) { missing++; continue; }
                    var a = it.ArmorComponent;
                    if (a == null || it.ItemType != want) { odd++; continue; }
                    int h0 = a.HeadArmor, b0 = a.BodyArmor, a0 = a.ArmArmor, l0 = a.LegArmor;
                    int h1 = Math.Min(h0, th), b1 = Math.Min(b0, tb), a1 = Math.Min(a0, ta), l1 = Math.Min(l0, tl);
                    if (h1 == h0 && b1 == b0 && a1 == a0 && l1 == l0) { same++; continue; }
                    float tf = it.Tierf; int g0 = (int)it.Tier;
                    if (!Freeze(it, tf, getOv, setOv)) { odd++; continue; }
                    neu[it] = new[] { h0, b0, a0, l0 };      // 418-p: ksiega sprzed zmiany
                    fHead.SetValue(a, h1); fBody.SetValue(a, b1); fArm.SetValue(a, a1); fLeg.SetValue(a, l1);
                    if ((int)it.Tier != g0) tierMoved++;
                    changed++;
                    sb.Append(sb.Length > 0 ? ", " : "").Append(id).Append(' ')
                      .Append(h0).Append('/').Append(b0).Append('/').Append(a0).Append('/').Append(l0).Append(" -> ")
                      .Append(h1).Append('/').Append(b1).Append('/').Append(a1).Append('/').Append(l1);
                }
                catch (Exception e) { odd++; Log.Error("RotData418.Clothes " + id, e); }
            }
            Log.Info("Dane ROT (418): ubrania - zmienione " + changed + ", juz nizej " + same + ", brak w grze " + missing + ", pominiete (inny typ / blad) " + odd
                     + (restored > 0 ? ", najpierw przywrocone do danych ROT " + restored : "")
                     + " | tier zamrozony (TierfOverride), zmienionych tierow " + tierMoved
                     + " | 418-p: klasa stroju (382c / 389), pula zuzycia, material naprawy i koszt helmu licza z wartosci sprzed zmiany (ksiega " + neu.Count + " sztuk)"
                     + " | glowa/tulow/rece/nogi: " + (changed > 0 ? sb.ToString() : "-") + ".");
        }

        private static void Reset()
        {
            for (int i = 0; i < 4; i++) { HitN[i] = 0; HitDmg[i] = 0; }
            _broken = 0; _clothHits = 0; _clothMax = 0; _clothHp = 0f;
            _clothOwners = 0; _clothElse = 0;
        }

        private static string Avg(int i) { return HitN[i] > 0 ? (HitDmg[i] / HitN[i]).ToString() : "-"; }

        private static void Flush(Mission m)
        {
            int all = HitN[0] + HitN[1] + HitN[2] + HitN[3];
            if (all == 0 && _clothHits == 0 && _clothOwners == 0) { Reset(); return; }      // misja bez ciosu w tarcze i bez bohatera ze sztuka z tabeli - bez linii
            string kind = "?";
            try { kind = MissionTeams.Kind(m); } catch { }
            Log.Info("Tarcze i ubrania ROT (418): koniec misji (" + kind + ") | skala tarcz " + (_shieldsOn ? "TAK " + _lo + "-" + _hi : "NIE")
                     + " | ciosy wrecz przyjete na tarcze (liczba / suma obrazen tarczy / srednio na cios): ROT z pancerzem 1 - " + HitN[ClassRaised] + " / " + HitDmg[ClassRaised] + " / " + Avg(ClassRaised)
                     + "; ROT z pancerzem 7-9 - " + HitN[ClassLowered] + " / " + HitDmg[ClassLowered] + " / " + Avg(ClassLowered)
                     + "; ROT w skali - " + HitN[ClassKept] + " / " + HitDmg[ClassKept] + " / " + Avg(ClassKept)
                     + "; inne tarcze i nierozpoznane - " + HitN[0] + " / " + HitDmg[0] + " / " + Avg(0)
                     + " | tarcze rozbite ciosem wrecz " + _broken
                     + " | ubrania z tabeli " + (_clothOn ? "TAK" : "NIE") + ": trafienia w noszacych " + _clothHits
                     + (_clothHits > 0 ? ", srednio " + (_clothHp / _clothHits).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " HP, najwiecej " + _clothMax : "")
                     + "; bohaterowie ze sztuka z tabeli w zestawie bojowym " + _clothOwners + ", z tego weszli do misji bez niej " + _clothElse
                     + " (przebrani przez 382c albo misja cywilna - wtedy 418 ich w tej misji nie dotyczy).");
            Reset();
        }

        /// <summary>Liczniki misji: zerowanie na starcie, ciosy wrecz w tarcze wedlug klasy tarczy ROT, trafienia w noszacych ubrania z tabeli, linia sumy
        /// na koncu. Dziala takze przy kluczach NIE (pomiar "przed" w tych samych rubrykach). Bez alokacji, bez try/catch.</summary>
        internal sealed class Logic : MissionLogic
        {
            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            public override void OnBehaviorInitialize()
            {
                base.OnBehaviorInitialize();
                Reset();
            }

            /// <summary>418-p: pomiar zasiegu B6 - bohater, ktory ma sztuke z tabeli w zestawie bojowym, a wszedl do misji w czyms innym (382c daje
            /// bohaterom AI zbroje zamiast sukni; w misji cywilnej zestaw jest cywilny). Raz na agenta, bez alokacji.</summary>
            public override void OnAgentBuild(Agent agent, Banner banner)
            {
                if (ClothItems.Count == 0 || agent == null || !agent.IsHuman || !agent.IsHero) return;
                var co = agent.Character as CharacterObject;
                var hero = co != null ? co.HeroObject : null;
                var be = hero != null ? hero.BattleEquipment : null;
                if (be == null) return;
                var b = be[EquipmentIndex.Body].Item; var h = be[EquipmentIndex.Head].Item;
                if ((b == null || !ClothItems.Contains(b)) && (h == null || !ClothItems.Contains(h))) return;
                _clothOwners++;
                var se = agent.SpawnEquipment;
                if (se == null) { _clothElse++; return; }
                b = se[EquipmentIndex.Body].Item; h = se[EquipmentIndex.Head].Item;
                if ((b == null || !ClothItems.Contains(b)) && (h == null || !ClothItems.Contains(h))) _clothElse++;
            }

            public override void OnMeleeHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)
            {
                if (!collisionData.AttackBlockedWithShield || victim == null) return;
                if (collisionData.IsShieldBroken) _broken++;
                int cls = 0;
                // rozbita tarcza moze juz nie byc w rece - wtedy cios idzie do "inne i nierozpoznane"
                var slot = victim.GetOffhandWieldedItemIndex();
                if (slot != EquipmentIndex.None)
                {
                    var it = victim.Equipment[slot].Item;
                    byte b;
                    if (it != null && ShieldClass.TryGetValue(it, out b)) cls = b;
                }
                HitN[cls]++;
                HitDmg[cls] += collisionData.InflictedDamage;
            }

            public override void OnScoreHit(Agent affectedAgent, Agent affectorAgent, WeaponComponentData attackerWeapon, bool isBlocked, bool isSiegeEngineHit,
                in Blow blow, in AttackCollisionData collisionData, float damagedHp, float hitDistance, float shotDifficulty)
            {
                if (ClothItems.Count == 0 || isBlocked || affectedAgent == null || !affectedAgent.IsHuman || !affectedAgent.IsHero) return;
                var eq = affectedAgent.SpawnEquipment;
                if (eq == null) return;
                var body = eq[EquipmentIndex.Body].Item;
                var head = eq[EquipmentIndex.Head].Item;
                if ((body == null || !ClothItems.Contains(body)) && (head == null || !ClothItems.Contains(head))) return;
                _clothHits++;
                _clothHp += damagedHp;
                if ((int)damagedHp > _clothMax) _clothMax = (int)damagedHp;
            }

            protected override void OnEndMission()
            {
                try { Flush(Mission); } catch (Exception e) { Log.Error("RotData418.Flush", e); }
                base.OnEndMission();
            }
        }
    }
}
