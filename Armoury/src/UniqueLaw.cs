using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// PRAWO UNIKATOW (Jeff 16.09: "Ramsey Armour jest tylko jeden i nosi go Ramsey,
    /// a polowa wojska biega w Ramsey armour... pozamieniaj na odpowiednie
    /// historyczne i podobnej jakosci lub minimalnie mniejszej").
    ///
    /// SKAD KOPIE (dekompilacja DTE 1.4.7, 16.09): EveryoneCampaignBehavior
    /// .AllocateRandomEquipmentToPartyArmory co dzien dosypuje KAZDEJ partii AI
    /// losowy sprzet z puli Cache.GetItemsByTypeTierAndCulture, filtrowanej
    /// wylacznie po tierze i kulturze - a ramsay_* ma culture=battania (Polnoc),
    /// hound_* i mountain_* vlandia (Westerlands), brienne_/renly_ stormlands.
    /// Kazda partia Polnocy dostawala wiec pancerz Ramsaya, kazda z Westerlands
    /// rekawice Ogara i zbroje Gory. Po bitwie magazyn pokonanego jest lupem
    /// zwyciezcy (GetAllLootItems -> AddItemToArmory / AddItemToPartyArmory) -
    /// stad 9 par rekawic Ramsaya na polkach Jeffa (Armoury.log 15.09 14:32).
    /// Straze Mends zdejmowaly unikaty z przydzialu, ale kopie lezaly dalej i wracaly
    /// nastepnym lupem; ramsay_armor przechodzil przez furtke "nauczonego wzoru",
    /// a zestawu Gory nie bylo na zadnej liscie.
    ///
    /// Cztery ostrza tego prawa:
    /// (1) NABOR - prefix na DTE ArmyArmory.AddItemToArmory (gracz) i
    ///     EveryoneCampaignBehavior.AddItemToPartyArmory (AI): unikat wchodzacy do
    ///     magazynu zamienia sie W DRZWIACH na zamiennik (StandInFor): ten sam typ,
    ///     tier nie wyzszy, kultura wlasciciela magazynu; gdy w tej kulturze nic nie
    ///     ma - bezkulturowy, potem z tej samej strony Waskiego Morza. Korony
    ///     zamiennika nie maja (regalia, patrz Uniques) - znikaja.
    /// (2) SWEEP przy wczytaniu: magazyn DTE gracza, magazyny partii AI, tabor
    ///     gracza, tabory AI - kazda kopia -> zamiennik, liczba sztuk bez zmian.
    /// (3) BOHATEROWIE, gracz tez: unikat na kims, kto nie jest wlascicielem,
    ///     schodzi na rzecz zamiennika w kulturze bohatera. Wlasciciel = ma ten
    ///     item we WLASNYM szablonie (ROT: Sandor Clegane = ROTwanderer3 z hound_*,
    ///     Brienne = ROTwanderer9, Ramsay z ramsay_*...) ALBO nazwisko z listy
    ///     Owners (Mends.NamesakeGear ubiera Ramsaya, Cersei, Stannisa... w rzeczy,
    ///     ktorych szablon moze nie miec).
    /// (4) Targow miast NIE tykamy - Mends.UniqueWares czysci je co sesje, a
    ///     relikwie z NamesakeGear leza tam CELOWO po jednej sztuce.
    /// Lista unikatow: UniqueGear (= Mends.UniquePrefixes + zestaw Gory).
    /// </summary>
    internal sealed class UniqueLaw : CampaignBehaviorBase
    {
        private static readonly Dictionary<string, ItemObject> Repl = new Dictionary<string, ItemObject>();
        private static int _inPlayer, _inAi, _inGone;      // nabor od ostatniego raportu dziennego
        private bool _aiRedressed;                         // 16.09: jednorazowe przebranie zamiennikow "bez kultury" (patrz RedressAiArmories)

        // DTE kluczuje magazyny MBGUID partii (MobileParty.Id). PULAPKA (16.09, pierwsze
        // wczytanie prawa): MBObjectManager.Instance.GetObject(MBGUID) NIE zna partii
        // kampanii (te zyja w CampaignObjectManager) - 337 partii dostalo zamienniki
        // "bez kultury". DTE sam szuka przez Campaign.Current.MobileParties (FindActiveParty),
        // robimy to samo, ze slownikiem odswiezanym przy chybieniu.
        private static Dictionary<MBGUID, MobileParty> _byId;

        private static MobileParty FindParty(MBGUID id)
        {
            try
            {
                MobileParty mp;
                if (_byId != null && _byId.TryGetValue(id, out mp)) return mp;
                var all = MobileParty.All;
                if (all == null) return null;
                if (_byId == null || _byId.Count != all.Count)
                {
                    _byId = new Dictionary<MBGUID, MobileParty>();
                    foreach (var p in all) if (p != null) _byId[p.Id] = p;
                }
                return _byId.TryGetValue(id, out mp) ? mp : null;
            }
            catch { return null; }
        }

        // Wlasciciele wedle nazwiska - odbicie Mends.NamesakeGear + wedrowcy ROT.
        // Klucz = poczatek id, wartosc = fragmenty imienia rozdzielone '|'.
        // Szablon wlasny sprawdzany jest ZAWSZE i PIERWSZY - ta lista to
        // tylko rezerwa dla sztuk, ktore Mends dopisuje bohaterom w locie.
        private static readonly string[][] Owners = {
            new[] { "ramsay_", "Ramsay" },
            new[] { "cersei_", "Cersei" },
            new[] { "stannis_", "Stannis" },
            new[] { "baratheon_crown", "Stannis|Robert Baratheon" },
            new[] { "dany_", "Daenerys" },
            new[] { "joffrey_crown", "Joffrey|Tommen" },
            new[] { "renly_", "Renly" },
            new[] { "euron_crown", "Euron" },
            new[] { "nightking_", "Night King" },
            new[] { "hound_", "Sandor" },
            new[] { "houndskull", "Sandor" },
            new[] { "brienne_", "Brienne" },
            new[] { "tyrion_", "Tyrion" },
            new[] { "varys_", "Varys" },
            new[] { "baelish_", "Baelish" },
            new[] { "bull_helmet", "Gendry" },
            new[] { "melisandre_", "Melisandre|Kinvara" },
            new[] { "mountain_", "Gregor" },
            new[] { "robb_crown", "Robb" }
        };

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSession);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDay);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("armouryUniqueLawAiRedress", ref _aiRedressed);
        }

        private void OnSession(CampaignGameStarter starter)
        {
            try
            {
                if (!Settings.Current.UniqueGearLawEnabled) { Log.Info("UniqueLaw: wylaczone w ustawieniach."); return; }
                var pc = PlayerCulture();
                int g1, g2, g3 = 0, s3 = 0;
                int s1 = SweepRoster(DteArmory(), pc, out g1);
                int s2 = SweepRoster(MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null, pc, out g2);
                int aiBags = 0;
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || mp == MobileParty.MainParty || mp.ItemRoster == null) continue;
                    int g;
                    int s = SweepRoster(mp.ItemRoster, PartyCulture(mp), out g);
                    if (s > 0 || g > 0) aiBags++;
                    s3 += s; g3 += g;
                }
                Log.Info("UniqueLaw (wczytanie): magazyn DTE gracza - " + s1 + " szt. zamienionych, " + g1 + " znikly; tabor gracza - "
                         + s2 + " zamienionych, " + g2 + " znikly; tabory AI - " + s3 + " zamienionych, " + g3 + " znikly w " + aiBags + " partiach.");
                if (s1 + s2 > 0)
                    Log.Player("The quartermaster exchanged " + (s1 + s2) + " pieces of named heroes' gear for honest equipment of your own people.");
                else if (g1 + g2 > 0)
                    Log.Player("The quartermaster struck " + (g1 + g2) + " pieces of named heroes' regalia from your stores - they belong to their owners.");
                SweepAiArmories("wczytanie");
                if (!_aiRedressed) { RedressAiArmories(); _aiRedressed = true; }
                SweepHeroes();
            }
            catch (Exception e) { Log.Error("UniqueLaw.OnSession", e); }
        }

        private void OnDay()
        {
            try
            {
                if (_inPlayer == 0 && _inAi == 0 && _inGone == 0) return;
                Log.Info("UniqueLaw (dzien): nabor do magazynow DTE - u gracza " + _inPlayer + " szt. unikatow zamienionych na zamienniki, u AI "
                         + _inAi + ", bez zamiennika (znikly) " + _inGone + ".");
                _inPlayer = 0; _inAi = 0; _inGone = 0;
            }
            catch { }
        }

        // ---------------------------------------------------------------- zamiennik

        private static bool IsDead(ItemObject it)
        {
            try
            {
                var cu = it.Culture != null ? (it.Culture.StringId ?? "") : "";
                if (cu == "wights" || cu == "whitewalker") return true;
                var id = it.StringId ?? "";
                return id.StartsWith("ice_", StringComparison.Ordinal) || id.StartsWith("wight_", StringComparison.Ordinal)
                    || id == "nightking_blade" || id == "white_walker_saddle";
            }
            catch { return false; }
        }

        /// <summary>Zamiennik unikatu w danej kulturze: ten sam typ (dla broni
        /// tez klasa), tier nie wyzszy, nie unikat / legenda / regalia / sprzet
        /// umarlych / NotMerchandise (rzadkosci lordow). Kolejnosc: kultura
        /// wlasciciela, bezkulturowy, ta sama strona Waskiego Morza; w obrebie
        /// tego najwyzszy tier, potem najwiecej pancerza, potem cena. Druga
        /// strona morza NIGDY (Polnoc nie dostanie plyty z Volantis). Cache na
        /// sesje - pary (unikat, kultura) jest kilkadziesiat.</summary>
        internal static ItemObject StandInFor(ItemObject uniq, BasicCultureObject cul)
        {
            if (uniq == null) return null;
            string cid = cul != null && cul.StringId != null ? cul.StringId : "-";
            string key = uniq.StringId + "|" + cid;
            lock (Repl)
            {
                ItemObject cached;
                if (Repl.TryGetValue(key, out cached)) return cached;
            }
            ItemObject best = null;
            long bestScore = long.MinValue;
            try
            {
                string uid = uniq.StringId ?? "";
                if (uid.EndsWith("_crown", StringComparison.Ordinal)) { Repl[key] = null; return null; }
                bool essos = MountLaw.IsEssosCulture(cid);
                bool female = (uniq.ItemFlags & ItemFlags.NotUsableByMale) != 0;
                var wc = uniq.PrimaryWeapon != null ? uniq.PrimaryWeapon.WeaponClass : WeaponClass.Undefined;
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || it == uniq || it.StringId == null) continue;
                    if (it.ItemType != uniq.ItemType) continue;
                    if (UniqueGear.Is(it) || LegendaryLaw.IsLegend(it) || IsDead(it) || it.NotMerchandise) continue;
                    if (it.StringId.EndsWith("_crown", StringComparison.Ordinal)) continue;
                    if (it.Tier > uniq.Tier) continue;
                    if (it.IsCivilian != uniq.IsCivilian) continue;
                    if (((it.ItemFlags & ItemFlags.NotUsableByMale) != 0) != female) continue;
                    if (uniq.HasWeaponComponent)
                    {
                        if (it.PrimaryWeapon == null || it.PrimaryWeapon.WeaponClass != wc) continue;
                    }
                    else if (it.HasWeaponComponent) continue;
                    string ic = it.Culture != null ? it.Culture.StringId : null;
                    int cs;
                    if (string.IsNullOrEmpty(ic) || ic == "neutral_culture") cs = 1;
                    else if (ic == cid) cs = 2;
                    else if (MountLaw.IsEssosCulture(ic) == essos) cs = 0;
                    else continue;
                    int armour = 0;
                    var ac = it.ArmorComponent;
                    if (ac != null) armour = ac.HeadArmor + ac.BodyArmor + ac.LegArmor + ac.ArmArmor;
                    long score = cs * 100000000000L + (long)(int)it.Tier * 1000000000L + (long)armour * 100000L + Math.Min(it.Value, 99999);
                    if (score > bestScore) { bestScore = score; best = it; }
                }
            }
            catch (Exception e) { Log.Error("UniqueLaw.StandInFor", e); }
            lock (Repl) { Repl[key] = best; }
            Log.Info("UniqueLaw: zamiennik dla " + uniq.StringId + " (" + cid + ") -> "
                     + (best != null
                        ? best.StringId + " (t" + ((int)best.Tier + 1) + ", " + (best.Culture != null ? best.Culture.StringId : "bezkulturowy") + ")"
                        : "BRAK - sztuka znika") + ".");
            return best;
        }

        // ---------------------------------------------------------------- kultury

        private static BasicCultureObject PlayerCulture()
        {
            try { return Hero.MainHero != null ? Hero.MainHero.Culture : null; } catch { return null; }
        }

        private static BasicCultureObject PartyCulture(MobileParty mp)
        {
            try
            {
                if (mp == null) return null;
                if (mp.Party != null && mp.Party.Culture != null) return mp.Party.Culture;
                if (mp.LeaderHero != null && mp.LeaderHero.Culture != null) return mp.LeaderHero.Culture;
                if (mp.ActualClan != null && mp.ActualClan.Culture != null) return mp.ActualClan.Culture;
            }
            catch { }
            return null;
        }

        // ---------------------------------------------------------------- sweepy

        /// <summary>Kazda kopia unikatu w rosterze -> zamiennik (ta sama liczba
        /// sztuk, bez modyfikatora - modyfikator plyty na skorze bylby bez sensu).</summary>
        private static int SweepRoster(ItemRoster roster, BasicCultureObject cul, out int gone)
        {
            gone = 0;
            int swapped = 0;
            if (roster == null) return 0;
            try
            {
                for (int i = roster.Count - 1; i >= 0; i--)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0 || !UniqueGear.Is(it)) continue;
                    int n = el.Amount;
                    roster.AddToCounts(el.EquipmentElement, -n);
                    var sub = StandInFor(it, cul);
                    if (sub != null) { roster.AddToCounts(sub, n); swapped += n; }
                    else gone += n;
                }
            }
            catch (Exception e) { Log.Error("UniqueLaw.SweepRoster", e); }
            return swapped;
        }

        private static ItemRoster DteArmory()
        {
            try
            {
                var t = AccessTools.TypeByName("DynamicTroopEquipmentReupload.ArmyArmory");
                var f = t != null ? AccessTools.Field(t, "Armory") : null;
                return f != null ? f.GetValue(null) as ItemRoster : null;
            }
            catch { return null; }
        }

        /// <summary>Wirtualne magazyny partii AI (DTE PartyArmories: MBGUID ->
        /// slownik item -> liczba) - kopie unikatow na zamienniki w kulturze partii.</summary>
        private static void SweepAiArmories(string why)
        {
            try
            {
                var t = AccessTools.TypeByName("DynamicTroopEquipmentReupload.EveryoneCampaignBehavior");
                var f = t != null ? AccessTools.Field(t, "PartyArmories") : null;
                var map = f != null ? f.GetValue(null) as System.Collections.IDictionary : null;
                if (map == null) return;
                int swapped = 0, gone = 0, parties = 0;
                foreach (System.Collections.DictionaryEntry e in map)
                {
                    var inner = e.Value as System.Collections.IDictionary;
                    if (inner == null) continue;
                    List<object> keys = null;
                    foreach (System.Collections.DictionaryEntry kv in inner)
                    {
                        var it = kv.Key as ItemObject;
                        if (it == null || !UniqueGear.Is(it)) continue;
                        if (keys == null) keys = new List<object>();
                        keys.Add(kv.Key);
                    }
                    if (keys == null) continue;
                    parties++;
                    BasicCultureObject cul = null;
                    try { if (e.Key is MBGUID) cul = PartyCulture(FindParty((MBGUID)e.Key)); } catch { }
                    foreach (var k in keys)
                    {
                        int n = 0;
                        try { n = Convert.ToInt32(inner[k]); } catch { }
                        inner.Remove(k);
                        if (n <= 0) continue;
                        var sub = StandInFor((ItemObject)k, cul);
                        if (sub == null) { gone += n; continue; }
                        int have = 0;
                        try { if (inner.Contains(sub)) have = Convert.ToInt32(inner[sub]); } catch { }
                        inner[sub] = have + n;
                        swapped += n;
                    }
                }
                if (swapped > 0 || gone > 0)
                    Log.Info("UniqueLaw: magazyny AI (" + why + ") - " + swapped + " szt. unikatow zamienionych na zamienniki, "
                             + gone + " znikly, w " + parties + " partiach.");
            }
            catch (Exception e) { Log.Error("UniqueLaw.SweepAiArmories", e); }
        }

        /// <summary>JEDNORAZOWO (16.09): pierwsze wczytanie prawa nie rozpoznalo partii AI
        /// (patrz FindParty) i 11134 sztuk w 337 magazynach dostalo zamienniki bez kultury
        /// (bandit_hybrid_*, tacky_bandit, arryn_chausses, padded_vambrace). Tu: w kazdym
        /// magazynie AI z rozpoznana kultura te wlasnie id (zbior = StandInFor(unikat, null)
        /// dla kazdego unikatu) ida jeszcze raz przez StandInFor z kultura partii; podmiana
        /// TYLKO gdy wynik jest w kulturze partii (inaczej nic sie nie zyskuje). Skutek
        /// uboczny: zwykle bandyckie sztuki tych id w magazynach lordow tez staja sie
        /// rodowe - na plus. Flaga w save, wiec raz na kampanie.</summary>
        private static void RedressAiArmories()
        {
            try
            {
                var neutral = new HashSet<ItemObject>();
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || !UniqueGear.Is(it)) continue;
                    var s = StandInFor(it, null);
                    if (s != null) neutral.Add(s);
                }
                if (neutral.Count == 0) return;
                var t = AccessTools.TypeByName("DynamicTroopEquipmentReupload.EveryoneCampaignBehavior");
                var f = t != null ? AccessTools.Field(t, "PartyArmories") : null;
                var map = f != null ? f.GetValue(null) as System.Collections.IDictionary : null;
                if (map == null) return;
                int swapped = 0, parties = 0, noCulture = 0;
                foreach (System.Collections.DictionaryEntry e in map)
                {
                    var inner = e.Value as System.Collections.IDictionary;
                    if (inner == null) continue;
                    BasicCultureObject cul = null;
                    try { if (e.Key is MBGUID) cul = PartyCulture(FindParty((MBGUID)e.Key)); } catch { }
                    if (cul == null || cul.StringId == null) { noCulture++; continue; }
                    List<object> keys = null;
                    foreach (System.Collections.DictionaryEntry kv in inner)
                    {
                        var it = kv.Key as ItemObject;
                        if (it == null || !neutral.Contains(it)) continue;
                        if (keys == null) keys = new List<object>();
                        keys.Add(kv.Key);
                    }
                    if (keys == null) continue;
                    bool touched = false;
                    foreach (var k in keys)
                    {
                        var it = (ItemObject)k;
                        var sub = StandInFor(it, cul);
                        if (sub == null || sub == it || sub.Culture == null || sub.Culture.StringId != cul.StringId) continue;
                        int n = 0;
                        try { n = Convert.ToInt32(inner[k]); } catch { }
                        inner.Remove(k);
                        if (n <= 0) continue;
                        int have = 0;
                        try { if (inner.Contains(sub)) have = Convert.ToInt32(inner[sub]); } catch { }
                        inner[sub] = have + n;
                        swapped += n; touched = true;
                    }
                    if (touched) parties++;
                }
                Log.Info("UniqueLaw: przebranie magazynow AI po pierwszym wczytaniu - " + swapped + " szt. w " + parties
                         + " partiach z ubran bez kultury na sprzet rodu" + (noCulture > 0 ? " (" + noCulture + " magazynow bez rozpoznanej partii - pominiete)" : "") + ".");
            }
            catch (Exception e) { Log.Error("UniqueLaw.RedressAiArmories", e); }
        }

        private static bool Has(Equipment eq, ItemObject it)
        {
            try
            {
                for (int s = 0; s < 12; s++)
                    if (eq[(EquipmentIndex)s].Item == it) return true;
            }
            catch { }
            return false;
        }

        // Szablon XML postaci: BasicCharacterObject._equipmentRoster. UWAGA: dla
        // bohatera CharacterObject.BattleEquipments zwraca jego BIEZACY ekwipunek
        // (HeroObject.BattleEquipment), nie szablon - sprawdzone w dekompilacji 16.09;
        // przez to "czy ma we wzorcu" byloby zawsze prawda. Wedrowiec stworzony
        // z szablonu dziedziczy ten sam roster (CharacterObject.CreateFrom).
        private static readonly System.Reflection.FieldInfo FRoster = AccessTools.Field(typeof(BasicCharacterObject), "_equipmentRoster");

        private static bool InTemplate(CharacterObject co, ItemObject it)
        {
            try
            {
                var ro = FRoster != null && co != null ? FRoster.GetValue(co) as MBEquipmentRoster : null;
                var all = ro != null ? ro.AllEquipments : null;
                if (all == null) return false;
                foreach (var eq in all) if (eq != null && Has(eq, it)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>Czy ten bohater ma prawo do tego unikatu: item we wlasnym
        /// szablonie XML (wedrowcy ROT, lordowie z XML) albo nazwisko z Owners.</summary>
        private static bool MayWear(Hero h, ItemObject it)
        {
            try
            {
                if (InTemplate(h.CharacterObject, it)) return true;
                string name = h.Name != null ? h.Name.ToString() : "";
                string id = it.StringId ?? "";
                for (int i = 0; i < Owners.Length; i++)
                {
                    if (!id.StartsWith(Owners[i][0], StringComparison.Ordinal)) continue;
                    foreach (var part in Owners[i][1].Split('|'))
                        if (part.Length > 0 && name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>Bohaterowie (gracz tez): unikat nie na wlascicielu schodzi
        /// na rzecz zamiennika w kulturze bohatera. Sloty 0-3 i 5-9 (sztandar 4,
        /// kon i uprzaz poza prawem).</summary>
        private static void SweepHeroes()
        {
            try
            {
                int swapped = 0, gone = 0, heroes = 0, player = 0;
                foreach (var h in Hero.AllAliveHeroes)
                {
                    if (h == null) continue;
                    bool touched = false;
                    foreach (var eq in new[] { h.BattleEquipment, h.CivilianEquipment })
                    {
                        if (eq == null) continue;
                        for (int s = 0; s <= 9; s++)
                        {
                            if (s == 4) continue;
                            ItemObject it;
                            try { it = eq[(EquipmentIndex)s].Item; } catch { continue; }
                            if (it == null || !UniqueGear.Is(it) || MayWear(h, it)) continue;
                            var sub = StandInFor(it, h.Culture);
                            eq[(EquipmentIndex)s] = sub != null ? new EquipmentElement(sub) : default(EquipmentElement);
                            if (sub != null) swapped++; else gone++;
                            touched = true;
                            Log.Info("UniqueLaw: " + h.Name + " (" + (h.Culture != null ? h.Culture.StringId : "?") + ") zdejmuje "
                                     + it.StringId + " -> " + (sub != null ? sub.StringId : "nic") + ".");
                            if (h == Hero.MainHero)
                            {
                                player++;
                                Log.Player(it.Name + " belongs to its owner alone - "
                                           + (sub != null ? "you wear " + sub.Name + " instead." : "it is gone from your back."), sub == null);
                            }
                        }
                    }
                    if (touched) heroes++;
                }
                if (swapped > 0 || gone > 0)
                    Log.Info("UniqueLaw: bohaterowie - " + swapped + " sztuk zamienionych, " + gone + " zdjetych bez zamiennika, u "
                             + heroes + " osob (w tym gracz: " + player + " szt.).");
            }
            catch (Exception e) { Log.Error("UniqueLaw.SweepHeroes", e); }
        }

        // ---------------------------------------------------------------- nabor DTE

        /// <summary>Prefix na DTE ArmyArmory.AddItemToArmory(ItemObject item, int count):
        /// unikat w drzwiach magazynu gracza zamienia sie w zamiennik (kultura gracza).</summary>
        public static bool IntakePlayer(ref ItemObject __0, int __1)
        {
            try
            {
                if (__0 == null || !Settings.Current.UniqueGearLawEnabled || !UniqueGear.Is(__0)) return true;
                var was = __0;
                var sub = StandInFor(was, PlayerCulture());
                int n = Math.Max(1, __1);
                if (sub == null)
                {
                    _inGone += n;
                    Log.Player(was.Name + " x" + n + " is not yours to keep - struck from the stores.", true);
                    return false;
                }
                _inPlayer += n;
                __0 = sub;
                Log.Player(was.Name + " x" + n + " is not yours to keep - the quartermaster took " + sub.Name + " in its place.");
                return true;
            }
            catch { return true; }
        }

        /// <summary>Prefix na DTE EveryoneCampaignBehavior.AddItemToPartyArmory(MBGUID partyId,
        /// ItemObject item, int count): to samo dla magazynow AI (kultura partii).
        /// Tedy idzie i codzienny los DTE, i lup z bitwy - jedno miejsce, cala strumien.</summary>
        public static bool IntakeAi(MBGUID __0, ref ItemObject __1, int __2)
        {
            try
            {
                if (__1 == null || !Settings.Current.UniqueGearLawEnabled || !UniqueGear.Is(__1)) return true;
                var sub = StandInFor(__1, PartyCulture(FindParty(__0)));
                int n = Math.Max(1, __2);
                if (sub == null) { _inGone += n; return false; }
                _inAi += n;
                __1 = sub;
                return true;
            }
            catch { return true; }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var tA = AccessTools.TypeByName("DynamicTroopEquipmentReupload.ArmyArmory");
                var tE = AccessTools.TypeByName("DynamicTroopEquipmentReupload.EveryoneCampaignBehavior");
                var mA = tA != null ? AccessTools.Method(tA, "AddItemToArmory") : null;
                var mE = tE != null ? AccessTools.Method(tE, "AddItemToPartyArmory") : null;
                if (mA != null) h.Patch(mA, prefix: new HarmonyMethod(typeof(UniqueLaw), "IntakePlayer"));
                if (mE != null) h.Patch(mE, prefix: new HarmonyMethod(typeof(UniqueLaw), "IntakeAi"));
                Log.Info("UniqueLaw: nabor do magazynow DTE " + (mA != null ? "gracza" : "gracza BRAK") + " / " + (mE != null ? "AI" : "AI BRAK")
                         + " - unikaty imienne zamieniaja sie w drzwiach na zamienniki wlasnej kultury.");
            }
            catch (Exception e) { Log.Error("UniqueLaw.ApplyAll", e); }
        }
    }
}
