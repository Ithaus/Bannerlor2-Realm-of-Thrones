using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// 423a (audyt RBM 11.11, B5 + decyzja Jeffa 1 "rob balans zbroi i broni"): DRABINA PANCERZY ROT W SKALI RBM. Sama korekta danych w pamieci,
    /// raz na start sesji (wzor: HorseBarding419). Bez edycji XML, bez latek Harmony.
    ///
    /// SKAD LICZBY. Lacznik ROT z RBM (rot_all.cs:17505-17510) mnozy przy wczytaniu XML pancerz korpusow ROT: skora x2.2, kolczuga x2.1, plyta x2.4.
    /// W grze: ubranie T1 33, przeszywanica 61, ciezka skora 83, kolczuga 88, plyta 120 - a wojska RBM (Calradia) maja 10 / 22-32 / 50 / 50-79 / 99.
    /// Prog ciecia RBM = pancerz x 5 / sqrt(wspolczynnik broni): przeszywanicy 61 nie tnie zaden miecz (prog 233), wlocznia T2 daje 15 HP;
    /// kazdy od T1 do T6 pada po 10-13 cieciach - lekka piechota prawie tak twarda jak rycerz.
    ///
    /// REGULA. nowa = min(stara, round(stara + sila x (f_material(stara) - stara))); f odcinkami liniowa, monotoniczna, tylko w dol (wezly nizej;
    /// kontrola: tools/rot_armour_ladder.py). Funkcja materialu i wartosci - waga nie wchodzi. Rusza TYLKO pole BodyArmor korpusow ROT z listy
    /// (ArmourLadder423.Ids.cs, 337 sztuk: Leather 171, Chainmail 51, Plate 115); rece i nogi korpusu, helmy, rekawice, buty, naramienniki,
    /// tarcze, konie - bez zmian. Mediany: skora 61 -> 26, kolczuga 88 -> 55, plyta 120 -> 91.
    ///
    /// POZA TABELA (w zbiorze do pomiaru, liczby nietkniete): 7 sukien i strojow z tabeli 418 (juz 14), Inni i olbrzym, valyrian_surcoat_master;
    /// Dothrakowie i Nieskalani pod kluczem ArmourLadderDothrakiUnsullied (TAK = ida tabela i zachowuja MIEJSCE w drabinie: 77 -> 39, 99 -> 58;
    /// NIE = te same liczby co dzis - wtedy Nieskalany w 6 kg jest rowny plycie rycerskiej). To pytanie czeka na Jeffa przed wgraniem.
    ///
    /// KSIEGA (kontrola calosci). Tier zamrozony przez TierfOverride PRZED zmiana pola (jak 418 / 419); cena gry (Value) i Effectiveness sa zapisane
    /// przy wczytaniu XML. Wszystko, co u nas liczy z pancerza korpusu rzeczy inne niz walka, bierze wartosc SPRZED mapowania (BookBody / BookSum /
    /// BookModified): jednostki materialu (Recipes.ArmourUnits), pula zuzycia (ArmouryBehavior.ArmorPool), zamienniki unikatow (UniqueLaw),
    /// awanse band (OutlawLaw), granica stroju cywilnego (Civvies, HeroDress382), prog "ciezki korpus dusi oddech" (BattleWind) i podloga barkow
    /// 415 (NeckGuard415: min(korpus teraz, udzial x korpus ksiegowy)). Na zywo, swiadomie: RBM, RBMAI, HeroHp, podpowiedzi i karta przedmiotu
    /// (gracz widzi prawdziwe liczby), Spoils of War, DTE.
    ///
    /// CHWILA: OnAfterSessionLaunched - po wszystkich OnSessionLaunched (ArmorSanity i TierGearCheck CrashScribe, 418, 419); ROT slucha tam tylko
    /// smokow i krukow. Obiekty przedmiotow sa nowe w kazdej grze; gdyby ta sama sztuka przyszla drugi raz, Apply najpierw przywraca wartosc sprzed
    /// mapowania (slownik ksiegi), wiec NIE = dokladnie dane ROT, a zmiana suwaka nie sklada sie z poprzednia. Regula nie zna wlasciciela.
    /// </summary>
    internal sealed partial class ArmourLadder423 : CampaignBehaviorBase
    {
        // wezly (stara, nowa) - PROJEKT-423-po-krytyce p. 3; ponizej pierwszego liniowo do zera, powyzej ostatniego nachylenie ostatniego odcinka
        private static readonly int[] KnotsLeather =
        {
            0, 0, 19, 8, 33, 12, 44, 15, 55, 20, 61, 26, 66, 29, 70, 32, 74, 36, 77, 39, 79, 42, 81, 45, 83, 48, 85, 50, 88, 52, 92, 54, 99, 58, 132, 72, 176, 90,
        };
        private static readonly int[] KnotsMail = { 0, 0, 42, 30, 52, 36, 58, 40, 71, 46, 79, 50, 88, 55, 92, 58, 98, 62, 104, 66, 113, 72, 207, 130 };
        private static readonly int[] KnotsPlate = { 0, 0, 72, 55, 96, 72, 100, 75, 105, 79, 110, 83, 115, 87, 120, 91, 127, 96, 132, 100, 136, 103, 160, 120 };

        // 418: suknie i stroje imienne juz sprowadzone do 14 / 4 / 4 (RotData418.ClothRows) - nie mapowac drugi raz
        private static readonly string[] Dresses418 = { "dany_dress", "cersei_dress", "margaery_dress", "melisandre_dress", "varys_clothes", "tyrion_clothes", "baelish_clothes" };
        // Inni, olbrzym, mistrzowska oponcza valyrianska - poza tabela jak w 418 (osobna decyzja)
        private static readonly string[] OthersKept = { "nightking_armor", "nightking_armor2", "whitewalker_armor", "whitewalker_armor2", "giant_garb", "valyrian_surcoat_master" };
        // decyzja Jeffa 4 (Dothrakowie i Nieskalani) - pod kluczem ArmourLadderDothrakiUnsullied
        private static readonly string[] DothrakiUnsullied =
        {
            "dothraki_armor", "dothraki_armor2", "dothraki_armor3", "dothraki_armor4", "dothraki_armor5", "dothraki_armor6", "dothraki_armor8",
            "unsullied_armor", "unsullied_soldier_armor",
        };

        internal const int ClassCount = 6, KindCount = 6, DeepHp = 25;
        private static readonly string[] ClassName = { "bez korpusu / tkanina / spoza zbioru", "ubranie (skora < 50)", "przeszywanica (skora 50-69)", "ciezka skora (70+)", "kolczuga", "plyta" };
        private static readonly string[] KindName = { "ciecie", "klucie", "obuch", "iglica", "strzala szeroka", "inny pocisk" };

        // stan sesji. Ksiega: sztuka ze zbioru -> BodyArmor sprzed mapowania (16 bitow) | klasa korpusu << 16; podmieniana w calosci (czytaja ja watki wyceny)
        private static Dictionary<ItemObject, int> _book = new Dictionary<ItemObject, int>();
        private static bool _on;
        private static float _strength = 1f;
        private static int _changed;

        // liczniki misji (glowny watek): [klasa korpusu ofiary x rodzaj ciosu] - trafienia w piers / brzuch ludzi
        private static readonly int[] HitN = new int[ClassCount * KindCount];
        private static readonly long[] HitHp = new long[ClassCount * KindCount];
        private static readonly int[] HitDeep = new int[ClassCount * KindCount];
        private static readonly int[] Men = new int[ClassCount];
        private static readonly int[] Killed = new int[ClassCount];
        private static readonly int[] Wounded = new int[ClassCount];

        /// <summary>Czy drabina teraz zmienia dane (klucz TAK, sila > 0, pola gry pasuja).</summary>
        internal static bool Active { get { return _on; } }
        internal static float Strength { get { return _on ? _strength : 0f; } }

        public override void RegisterEvents()
        {
            CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, _ => Apply());
        }

        public override void SyncData(IDataStore dataStore) { }

        /// <summary>Pancerz tulowia korpusu sprzed mapowania. Sztuka spoza zbioru: wartosc biezaca.</summary>
        internal static int BookBody(ItemObject it, int live)
        {
            int v;
            var d = _book;
            return d.Count > 0 && it != null && d.TryGetValue(it, out v) ? (v & 0xFFFF) : live;
        }

        /// <summary>Suma czterech pol z tulowiem sprzed mapowania - do jednostek materialu, puli zuzycia, granic stroju. Sztuka spoza zbioru: suma biezaca.</summary>
        internal static int BookSum(ItemObject it, int liveSum)
        {
            int v;
            var d = _book;
            if (d.Count == 0 || it == null || !d.TryGetValue(it, out v)) return liveSum;
            var a = it.ArmorComponent;
            return a == null ? liveSum : liveSum - a.BodyArmor + (v & 0xFFFF);
        }

        /// <summary>Pancerz tulowia noszonej sztuki z modyfikatorem, liczony od wartosci sprzed mapowania - ta sama droga co EquipmentElement
        /// .GetModifiedBodyArmor (ItemModifier.ModifyArmor z prefiksem RBM i naszym postfiksem stanu), wiec wynik jest dokladnie taki jak przed 423.
        /// Sztuka spoza zbioru albo niezmieniona: wartosc biezaca. Bez alokacji.</summary>
        internal static float BookModified(EquipmentElement el)
        {
            var it = el.Item;
            if (it == null) return 0f;
            int v;
            var d = _book;
            var a = it.ArmorComponent;
            if (d.Count == 0 || a == null || !d.TryGetValue(it, out v)) return el.GetModifiedBodyArmor();
            int orig = v & 0xFFFF;
            if (orig <= 0 || orig == a.BodyArmor) return el.GetModifiedBodyArmor();
            var m = el.ItemModifier;
            return m != null ? m.ModifyArmor(orig) : orig;
        }

        internal static bool InSet(ItemObject it)
        {
            var d = _book;
            return d.Count > 0 && it != null && d.ContainsKey(it);
        }

        /// <summary>Klasa korpusu z wartosci KSIEGOWEJ i materialu (te same rubryki przy kluczu TAK i NIE): 0 brak / tkanina / spoza zbioru,
        /// 1 skora &lt; 50, 2 skora 50-69, 3 skora 70+, 4 kolczuga, 5 plyta.</summary>
        internal static int ClassOf(ItemObject it)
        {
            int v;
            var d = _book;
            return d.Count > 0 && it != null && d.TryGetValue(it, out v) ? (v >> 16) : 0;
        }

        private static int ClassFor(ArmorComponent.ArmorMaterialTypes mat, int body)
        {
            switch (mat)
            {
                case ArmorComponent.ArmorMaterialTypes.Leather: return body < 50 ? 1 : body < 70 ? 2 : 3;
                case ArmorComponent.ArmorMaterialTypes.Chainmail: return 4;
                case ArmorComponent.ArmorMaterialTypes.Plate: return 5;
                default: return 0;
            }
        }

        private static int[] KnotsOf(ArmorComponent.ArmorMaterialTypes mat)
        {
            switch (mat)
            {
                case ArmorComponent.ArmorMaterialTypes.Leather: return KnotsLeather;
                case ArmorComponent.ArmorMaterialTypes.Chainmail: return KnotsMail;
                case ArmorComponent.ArmorMaterialTypes.Plate: return KnotsPlate;
                default: return null;
            }
        }

        /// <summary>Wartosc docelowa tabeli (sila 1) - odcinkami liniowa.</summary>
        private static double Target(int[] k, int v)
        {
            if (v <= 0) return v;
            int n = k.Length;
            for (int i = 2; i < n; i += 2)
                if (v <= k[i]) return k[i - 1] + (k[i + 1] - k[i - 1]) * (double)(v - k[i - 2]) / (k[i] - k[i - 2]);
            return k[n - 1] + (k[n - 1] - k[n - 3]) / (double)(k[n - 2] - k[n - 4]) * (v - k[n - 2]);
        }

        internal static int Map(ArmorComponent.ArmorMaterialTypes mat, int v, float strength)
        {
            var k = KnotsOf(mat);
            if (k == null || v <= 0) return v;
            double s = strength < 0f ? 0.0 : strength > 1f ? 1.0 : strength;
            int n = (int)Math.Round(v + s * (Target(k, v) - v));
            if (n < 1) n = 1;
            return n < v ? n : v;
        }

        private static int Median(List<int> l)
        {
            if (l.Count == 0) return 0;
            l.Sort();
            return l[l.Count / 2];
        }

        private static string F(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }

        private static void Apply()
        {
            var old = _book;
            var neu = new Dictionary<ItemObject, int>();
            _on = false; _changed = 0;
            try
            {
                var c = Settings.Current;
                if (c == null) { _book = neu; return; }
                float s = c.ArmourLadderStrength;
                if (!(s >= 0f)) s = 0f; else if (s > 1f) s = 1f;
                _strength = s;
                bool doth = c.ArmourLadderDothrakiUnsullied;
                var fBody = AccessTools.Field(typeof(ArmorComponent), "<BodyArmor>k__BackingField");
                var getOv = AccessTools.PropertyGetter(typeof(ItemObject), "TierfOverride");
                var setOv = AccessTools.PropertySetter(typeof(ItemObject), "TierfOverride");
                bool field = fBody != null && fBody.FieldType == typeof(int);
                bool model = false;
                try { model = Game.Current != null && Game.Current.BasicModels != null && Game.Current.BasicModels.ItemValueModel != null; } catch { }
                string off = !c.ArmourLadderEnabled ? "klucz ROT Body Armour In RBM Scale NIE - korpusy jak w danych ROT"
                           : !(s > 0f) ? "sila 0 (Armour Ladder Strength) - korpusy jak w danych ROT"
                           : !field ? "brak pola ArmorComponent.BodyArmor (inna wersja gry) - bez zmian"
                           : getOv == null || setOv == null || !model ? "brak ItemObject.TierfOverride albo modelu wartosci - bez zamrozenia tieru nie ruszam (tier korpusu liczy sie z pancerza)" : null;
                _on = off == null;

                var dress = new HashSet<string>(Dresses418);
                var others = new HashSet<string>(OthersKept);
                var dothSet = new HashSet<string>(DothrakiUnsullied);
                int same = 0, missing = 0, odd = 0, tierMoved = 0, restored = 0, shown = 0;
                var cnt = new int[3];
                var before = new[] { new List<int>(), new List<int>(), new List<int>() };
                var after = new[] { new List<int>(), new List<int>(), new List<int>() };
                var sbMiss = new StringBuilder(); var sbEx = new StringBuilder(); var sbKept = new StringBuilder(); var sbDoth = new StringBuilder();
                foreach (var id in Ids)
                {
                    try
                    {
                        var it = MBObjectManager.Instance.GetObject<ItemObject>(id);
                        if (it == null) { missing++; if (missing <= 12) sbMiss.Append(sbMiss.Length > 0 ? ", " : "").Append(id); continue; }
                        var a = it.ArmorComponent;
                        if (it.ItemType != ItemObject.ItemTypeEnum.BodyArmor || a == null) { odd++; continue; }
                        var mat = a.MaterialType;
                        if (KnotsOf(mat) == null) { odd++; continue; }                       // inny mod zmienil material - nie nasza tabela
                        // ta sama sztuka drugi raz w tym procesie: najpierw wartosc sprzed mapowania (NIE = dane ROT, suwak sie nie sklada)
                        int ov;
                        if (field && old != null && old.TryGetValue(it, out ov) && (ov & 0xFFFF) != a.BodyArmor) { fBody.SetValue(a, ov & 0xFFFF); restored++; }
                        int b0 = a.BodyArmor;
                        if (b0 < 0 || b0 > 0xFFFF) { odd++; continue; }
                        neu[it] = b0 | (ClassFor(mat, b0) << 16);
                        int mi = mat == ArmorComponent.ArmorMaterialTypes.Leather ? 0 : mat == ArmorComponent.ArmorMaterialTypes.Chainmail ? 1 : 2;
                        cnt[mi]++; before[mi].Add(b0);
                        int b1 = b0;
                        bool isDoth = dothSet.Contains(id);
                        if (dress.Contains(id) || others.Contains(id))
                            sbKept.Append(sbKept.Length > 0 ? ", " : "").Append(id).Append(' ').Append(b0);
                        else if (isDoth && !doth)
                            sbDoth.Append(sbDoth.Length > 0 ? ", " : "").Append(id).Append(' ').Append(b0);
                        else if (_on)
                        {
                            b1 = Map(mat, b0, s);
                            if (b1 == b0) same++;
                            else
                            {
                                float tf = it.Tierf; int g0 = (int)it.Tier;
                                if (!Freeze(it, tf, getOv, setOv)) { odd++; b1 = b0; }
                                else
                                {
                                    fBody.SetValue(a, b1);
                                    if ((int)it.Tier != g0) tierMoved++;
                                    _changed++;
                                    if (isDoth) sbDoth.Append(sbDoth.Length > 0 ? ", " : "").Append(id).Append(' ').Append(b0).Append(" -> ").Append(b1);
                                    else if (id == "stark_armor" || id == "lannister_armor" || id == "nightswatch_armor2" || id == "cersei_armor" || (shown < 2 && mi == 1))
                                    {
                                        if (mi == 1) shown++;
                                        sbEx.Append(sbEx.Length > 0 ? ", " : "").Append(id).Append(' ').Append(b0).Append(" -> ").Append(b1);
                                    }
                                }
                            }
                        }
                        after[mi].Add(b1);
                    }
                    catch (Exception e) { odd++; Log.Error("ArmourLadder423.Apply " + id, e); }
                }
                _book = neu;
                string thr;
                try { thr = RestedArmor416.Thresholds(false); } catch { thr = "odczyt nieudany"; }
                string med = "skora " + cnt[0] + " (mediana " + Median(before[0]) + " -> " + Median(after[0]) + "), kolczuga " + cnt[1] + " (" + Median(before[1]) + " -> " + Median(after[1])
                             + "), plyta " + cnt[2] + " (" + Median(before[2]) + " -> " + Median(after[2]) + ")";
                Log.Info("Drabina pancerzy ROT (423): " + (_on ? "klucz TAK, sila " + F(s) : off) + " | korpusy ROT z listy " + Ids.Length + ": " + med
                         + " | zmienione " + _changed + ", bez zmiany (juz w skali) " + same + ", brak w grze " + missing + (missing > 0 ? " (" + sbMiss + (missing > 12 ? ", ..." : "") + ")" : "")
                         + ", pominiete (inny typ / material / blad) " + odd + (restored > 0 ? ", przywrocone do danych ROT " + restored : "")
                         + " | tier zamrozony (TierfOverride), zmienionych tierow " + tierMoved
                         + " | poza tabela (418, Inni, olbrzym): " + (sbKept.Length > 0 ? sbKept.ToString() : "-")
                         + " | Dothrakowie i Nieskalani (klucz " + (doth ? "TAK - tabela" : "NIE - bez zmian") + "): " + (sbDoth.Length > 0 ? sbDoth.ToString() : "-")
                         + " | przyklady: " + (sbEx.Length > 0 ? sbEx.ToString() : "-")
                         + " | receptury, pula zuzycia, zamienniki unikatow, awanse band, granice stroju, prog oddechu i podloga barkow 415 licza z wartosci sprzed mapowania; cena gry bez zmian"
                         + " | progi RBM w pamieci: " + thr + ".");
            }
            catch (Exception e) { _book = neu; Log.Error("ArmourLadder423.Apply", e); }
        }

        /// <summary>Tier sprzed zmiany wpisany do TierfOverride (wartosc + 1), o ile sztuka nie ma juz wlasnego. Zwraca false, gdy sie nie udalo.</summary>
        private static bool Freeze(ItemObject it, float tierfBefore, MethodInfo getOv, MethodInfo setOv)
        {
            try
            {
                float cur = (float)getOv.Invoke(it, null);
                if (cur >= 1f) return true;                       // tier_override z XML albo wczesniejsze zamrozenie (418) - tier i tak nie zalezy od pol
                setOv.Invoke(it, new object[] { Math.Max(0f, tierfBefore) + 1f });
                return true;
            }
            catch { return false; }
        }

        private static void Reset()
        {
            for (int i = 0; i < HitN.Length; i++) { HitN[i] = 0; HitHp[i] = 0; HitDeep[i] = 0; }
            for (int i = 0; i < ClassCount; i++) { Men[i] = 0; Killed[i] = 0; Wounded[i] = 0; }
            BodkinSoft423.Reset();
        }

        private static void Flush(Mission m)
        {
            int all = 0;
            for (int i = 0; i < HitN.Length; i++) all += HitN[i];
            if (all == 0) { Reset(); return; }                                              // misja bez trafien w tulow ludzi - bez linii
            string kind = "?";
            try { kind = MissionTeams.Kind(m); } catch { }
            var sb = new StringBuilder(900);
            sb.Append("Drabina pancerzy (423): koniec misji (").Append(kind).Append(") | drabina ").Append(_on ? "TAK sila " + F(_strength) + " (zmienionych sztuk " + _changed + ")" : "NIE")
              .Append(" | klasy korpusu wedlug wartosci sprzed mapowania | ludzie w misji / zabici / ranni:");
            for (int c = 0; c < ClassCount; c++)
                sb.Append(c > 0 ? ";" : "").Append(' ').Append(ClassName[c]).Append(' ').Append(Men[c]).Append(" / ").Append(Killed[c]).Append(" / ").Append(Wounded[c]);
            sb.Append(" | TRAFIENIA w piers i brzuch, nie zablokowane (liczba / srednio HP / udzial ciosow od ").Append(DeepHp).Append(" HP):");
            for (int c = 0; c < ClassCount; c++)
            {
                int inClass = 0;
                for (int k = 0; k < KindCount; k++) inClass += HitN[c * KindCount + k];
                if (inClass == 0) continue;
                sb.Append(" [").Append(ClassName[c]).Append(']');
                for (int k = 0; k < KindCount; k++)
                {
                    int i = c * KindCount + k;
                    if (HitN[i] == 0) continue;
                    sb.Append(' ').Append(KindName[k]).Append(' ').Append(HitN[i]).Append(" / ").Append(HitHp[i] / HitN[i]).Append(" / ").Append(HitDeep[i] * 100 / HitN[i]).Append("%;");
                }
            }
            sb.Append(BodkinSoft423.Tail()).Append('.');
            Log.Info(sb.ToString());
            Reset();
        }

        private static int ClassOfAgent(Agent a)
        {
            var d = _book;
            if (d.Count == 0) return 0;
            var eq = a.SpawnEquipment;
            if (eq == null) return 0;
            var it = eq[EquipmentIndex.Body].Item;
            int v;
            return it != null && d.TryGetValue(it, out v) ? (v >> 16) : 0;
        }

        /// <summary>Liczniki misji: ludzie wedlug klasy korpusu, trafienia w piers / brzuch wedlug klasy ofiary i rodzaju ciosu, zabici i ranni wedlug
        /// klasy; linia sumy na koncu. Dziala takze przy kluczu NIE (pomiar "przed" w tych samych rubrykach). Bez alokacji, bez try/catch w sciezce trafien.</summary>
        internal sealed class Logic : MissionLogic
        {
            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            public override void OnBehaviorInitialize()
            {
                base.OnBehaviorInitialize();
                Reset();
                BodkinSoft423.RefreshRbm();
            }

            public override void OnAgentBuild(Agent agent, Banner banner)
            {
                if (agent == null || !agent.IsHuman) return;
                Men[ClassOfAgent(agent)]++;
            }

            public override void OnScoreHit(Agent affectedAgent, Agent affectorAgent, WeaponComponentData attackerWeapon, bool isBlocked, bool isSiegeEngineHit,
                in Blow blow, in AttackCollisionData collisionData, float damagedHp, float hitDistance, float shotDifficulty)
            {
                BodkinSoft423.OnHit(affectedAgent, isBlocked, blow.InflictedDamage);   // 423b: znacznik iglicy objetej regula - zdejmowany przy kazdym trafieniu
                if (isBlocked || affectedAgent == null || !affectedAgent.IsHuman || collisionData.IsFallDamage) return;
                var part = collisionData.VictimHitBodyPart;
                if (part != BoneBodyPartType.Chest && part != BoneBodyPartType.Abdomen) return;
                int k;
                var dt = blow.DamageType;
                if (blow.IsMissile)
                {
                    k = 5;
                    if (attackerWeapon != null && (attackerWeapon.WeaponClass == WeaponClass.Arrow || attackerWeapon.WeaponClass == WeaponClass.Bolt))
                        k = dt == DamageTypes.Pierce ? 3 : (dt == DamageTypes.Cut ? 4 : 5);
                }
                else k = dt == DamageTypes.Cut ? 0 : (dt == DamageTypes.Pierce ? 1 : 2);
                int i = ClassOfAgent(affectedAgent) * KindCount + k;
                int dmg = blow.InflictedDamage;
                HitN[i]++;
                HitHp[i] += dmg;
                if (dmg >= DeepHp) HitDeep[i]++;
            }

            public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
            {
                if (affectedAgent == null || !affectedAgent.IsHuman) return;
                if (agentState == AgentState.Killed) Killed[ClassOfAgent(affectedAgent)]++;
                else if (agentState == AgentState.Unconscious) Wounded[ClassOfAgent(affectedAgent)]++;
            }

            protected override void OnEndMission()
            {
                try { Flush(Mission); } catch (Exception e) { Log.Error("ArmourLadder423.Flush", e); }
                base.OnEndMission();
            }
        }
    }
}
