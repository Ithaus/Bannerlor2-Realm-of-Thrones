using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// UNIKATY KRAZA PO SWIECIE, NIE POWSTAJA I NIE ZNIKAJA (Jeff 04.10: "unikatow ma nie byc na targu - te unikaty maja
    /// postacie i potem mozna je zdobyc od nich"; "jak maja kupic na targu zbroje Brienne, skoro Brienne ja nosi";
    /// "jesli sprzedam, to bedzie gdzies w swiecie - maja byc unikaty monitorowane, kto potem kupil, gdzie jest, aby nie
    /// znikaly; jak sprzedam, moze pojawia sie gdzies indziej, bo ktos sprzeda albo jakis lord kupi"; AI pojmujace: "tak").
    ///  1. Spis unikatow ROT (RotUniques, docs/ROT-UNIKATY.md) dostaje NotMerchandise - sklepy ich nie zaopatruja i warsztaty
    ///     ich nie robia. RAZ na kampanie (flaga w save) zdejmujemy kopie z zaopatrzenia startowego: z polek i bagazy AI -
    ///     oryginal nosi postac. Potem NIC nie znika: unikat sprzedany na targu lezy na polce (do Z16-1c CS czystka polek
    ///     Mends.UniqueWares zjadala przy kazdym wczytaniu sztuki z listy UniqueGear - od Z16-1c pomija te, ktore liczy Is).
    ///     Stal valyrianska (177-2) poza ta czystka - jej kopie lapie spis ValyrianBlades (straznik wedlug stanu startowego).
    ///  2. Zwyczaj wojenny XIV w. (zbroja i kon jenca dla pojmujacego): kto bierze w niewole albo zabija w walce postac, ktora
    ///     NOSI unikat - gracz albo lord AI - dostaje go (gracz do taboru, lord zaklada). Pojmany dostaje zamiennik: najpierw
    ///     z taboru wlasnej partii (gdy jest), dopiero potem zwykly zamiennik kultury (z niczego - liczony w linii doby).
    ///  3. Lord AI w miescie, w ktorym na polce lezy unikat lepszy od tego, co nosi w tym miejscu, kupuje go (placi miastu)
    ///     i zaklada.
    ///  4. Kronika: codziennie spis, gdzie jest kazdy unikat (na kim, w czyim taborze, na jakim targu) - log tylko zmian.
    ///  177-2 (krytyka 177, pkt 3, 7, 16, 24): (a) zakladanie (Wear) NIGDY nie wypycha unikatu ani stali valyrianskiej z rak;
    ///     to, co zdjete albo nie miesci sie, idzie do taboru partii bohatera, a bez partii - na polke miasta jego rodu (dawniej
    ///     przepadalo: dziecko, malzonek bez partii); (b) dziedziczenie stali valyrianskiej po smierci poza walka (wykonawca
    ///     egzekucji, dziedzic, polka) - tylko VS: unikatowe zbroje na nowym wlascicielu zdjalby UniqueLaw.SweepHeroes (MayWear);
    ///     (c) rozbita albo rozwiazana partia oddaje unikaty z taboru zwyciezcy / wlascicielowi / na polke (gra lupi tylko
    ///     !NotMerchandise - MapEvent.LootDefeatedPartyItems - a reszta ginela z partia); (d) przebranie wladcy przy zmianie
    ///     rodu panujacego (NPCEquipmentsCampaignBehavior -> EquipmentHelper.AssignHeroEquipmentFromEquipment nadpisuje 12
    ///     slotow) zostawia unikaty i stal valyrianska w rekach (stad "Truth nigdzie" u zywego Tregara w tescie 120 dob);
    ///     (e) smierc gracza: gra sama przekazuje nastepcy zestaw bojowy i cywilny (HeirSelectionCampaignBehavior) - my dokladamy tylko
    ///     stal valyrianska z zestawu ukrycia, ktorego gra nie przekazuje.
    ///  Recenzja 177: sztuki na bohaterze wedlug ValyrianBlades.Pieces - w rodzie gracza kazdy slot to osobna sztuka (cywilny miecz gracza
    ///     to drugi miecz, nie duplikat), u bohatera spoza rodu gracza zestaw cywilny szablonu ROT powtarza bojowy (jedna sztuka); powody dla
    ///     gracza po angielsku osobno od powodow do logu.
    /// </summary>
    internal static class UniqueSpoils
    {
        private static bool _initDone;
        private static Dictionary<string, string> _last = new Dictionary<string, string>();
        // liczniki doby (177-2) - linia "Unikaty (177)"
        private static int _dInherit, _dDestroyed, _dRestored, _dParked, _dKin, _dShelved, _dStandRoster, _dStandNothing, _dStandNone, _dStandDead, _stumbles;
        private static readonly List<EquipmentElement> _heirStealth = new List<EquipmentElement>();   // smierc gracza: VS z zestawu ukrycia do nastepcy

        internal static void Reset() { _initDone = false; _last = new Dictionary<string, string>(); _common = null; ClearDay(); _stumbles = 0; _heirStealth.Clear(); }
        internal static string Export() { return _initDone ? "init" : ""; }
        internal static void Import(string s) { _initDone = s == "init"; }

        private static void ClearDay() { _dInherit = _dDestroyed = _dRestored = _dParked = _dKin = _dShelved = _dStandRoster = _dStandNothing = _dStandNone = _dStandDead = 0; }

        // wpis 66 (test 16:53): "noble_default" (domyslne nakrycie glowy szlachty, poza handlem w ROT) nosza setki postaci -
        // zasmiecal kronike (218 KB) i byl "zdobywany" przy kazdym pojmaniu. Co nosi wiecej niz UniqueMaxWearers postaci,
        // to stroj, nie unikat - poza kronika i zdobycza (liczone raz na sesje, przy pierwszym spisie).
        // 177-2: stal valyrianska jest unikatem ZAWSZE (skonczony zasob - Vigilance nosza na starcie trzej Hightowerowie).
        private static HashSet<string> _common;
        internal static bool Is(ItemObject it)
        {
            if (it == null || it.StringId == null) return false;
            if (ValyrianBlades.Is(it)) return true;
            if (!RotUniques.Ids.Contains(it.StringId)) return false;
            if (_common == null) BuildCommon();
            return !_common.Contains(it.StringId);
        }

        private static void BuildCommon()
        {
            _common = new HashSet<string>();
            try
            {
                var n = new Dictionary<string, int>();
                foreach (var h in Hero.AllAliveHeroes)
                {
                    var eq = h != null ? h.BattleEquipment : null;
                    if (eq == null) continue;
                    var seen = new HashSet<string>();
                    for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
                    {
                        var el = eq[(EquipmentIndex)i];
                        if (el.IsEmpty || el.Item.StringId == null || !RotUniques.Ids.Contains(el.Item.StringId) || !seen.Add(el.Item.StringId)) continue;
                        int c; n.TryGetValue(el.Item.StringId, out c); n[el.Item.StringId] = c + 1;
                    }
                }
                int max = Math.Max(1, Settings.Current.UniqueMaxWearers);
                foreach (var kv in n) if (kv.Value > max && !ValyrianBlades.IsId(kv.Key)) _common.Add(kv.Key);
                if (_common.Count > 0) Log.Info("UniqueSpoils: nie unikaty (nosi je wiecej niz " + max + " postaci) - " + string.Join(", ", n.Where(kv => kv.Value > max && !ValyrianBlades.IsId(kv.Key)).Select(kv => kv.Key + " x" + kv.Value).ToArray()) + ".");
            }
            catch (Exception e) { Log.Error("UniqueSpoils.BuildCommon", e); }
        }

        internal static void OnSessionLaunched()
        {
            int flagged = 0, bags = 0, shelves = 0;
            try
            {
                var fMerch = HarmonyLib.AccessTools.Field(typeof(ItemObject), "<NotMerchandise>k__BackingField");
                if (fMerch != null)
                    foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                        if (Is(it) && !it.NotMerchandise) { fMerch.SetValue(it, true); flagged++; }
            }
            catch (Exception e) { Log.Error("UniqueSpoils.flag", e); }
            if (!_initDone)
            {
                _initDone = true;
                try
                {
                    foreach (var mp in MobileParty.All)
                        if (mp != null && mp != MobileParty.MainParty && mp.ItemRoster != null) bags += Strip(mp.ItemRoster);
                    foreach (var st in Settlement.All)
                        if (st != null && st.ItemRoster != null) shelves += Strip(st.ItemRoster);
                }
                catch (Exception e) { Log.Error("UniqueSpoils.init", e); }
            }
            Log.Info("UniqueSpoils: unikaty ROT poza zaopatrzeniem sklepow - oznaczone " + flagged + "; kopie startowe zdjete raz na kampanie: z targow " + shelves + ", z bagazy AI " + bags + " szt.");
        }

        /// <summary>Kopie startowe precz (raz na kampanie). 177-2: stal valyrianska NIE - jej nadwyzke ponad stan startowy zamienia
        /// spis ValyrianBlades (liczba sztuk ta sama, stal zwykla); prawdziwa klinga na polce (sprzedana) ma zostac.</summary>
        private static int Strip(ItemRoster roster)
        {
            int off = 0;
            for (int i = roster.Count - 1; i >= 0; i--)
            {
                var el = roster.GetElementCopyAtIndex(i);
                if (el.Amount <= 0 || !Is(el.EquipmentElement.Item) || ValyrianBlades.Is(el.EquipmentElement.Item)) continue;
                roster.AddToCounts(el.EquipmentElement, -el.Amount);
                off += el.Amount;
            }
            return off;
        }

        // ------------------------------------------------------------ zdobycz (pojmanie, smierc w walce)
        internal static void OnPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            try { if (capturer != null) Take(prisoner, capturer.LeaderHero, capturer == PartyBase.MainParty, "pojmanie", false); }
            catch (Exception e) { Log.Error("UniqueSpoils.Prisoner", e); }
        }

        internal static void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool notify)
        {
            try
            {
                if (victim != null && killer != null && detail == KillCharacterAction.KillCharacterActionDetail.DiedInBattle)
                {
                    bool player = killer == Hero.MainHero || killer.PartyBelongedTo == MobileParty.MainParty;
                    Take(victim, player ? Hero.MainHero : (killer.PartyBelongedTo != null && killer.PartyBelongedTo.LeaderHero != null ? killer.PartyBelongedTo.LeaderHero : killer), player, "smierc w walce", true);
                }
            }
            catch (Exception e) { Log.Error("UniqueSpoils.Killed", e); }
            try { Inherit(victim, killer, detail); }
            catch (Exception e) { Log.Error("UniqueSpoils.Inherit", e); }
        }

        /// <summary>death: ofiara ginie (HeroKilledEvent idzie PRZED ChangeState(Dead) - gra 1.4.8 KillCharacterAction.cs:144
        /// vs :227 - wiec from.IsDead jest tu jeszcze false; dlatego flaga z wywolania, nie ze stanu bohatera).
        /// sklad9 (scalenie Z16 + 177, jedna droga): zamiennik ofiary - StandIn 177 (tabor jej partii, potem zwykly zamiennik; zabity - pusty
        /// slot), oba w granicy umiejetnosci ofiary (Z16); zdobywca AI zaklada przez Wear, ktory sam pilnuje sita Z16 (HeroGearRequirements:
        /// czego nie udzwignie - Park, jak wszystko, czego bohater nie trzyma w rekach: tabor jego partii, bez partii - polka miasta rodu).
        /// Z16-5 ClanBag (tabor glowy klanu), "zaklada mimo to" i "zostaje na jencu" zastapione przez Park - nic nie znika.</summary>
        private static void Take(Hero from, Hero to, bool toPlayer, string how, bool death)
        {
            if (from == null || to == null || from == to) return;
            if (from == Hero.MainHero && !Settings.Current.UniqueSpoilsFromPlayer) return;
            var eq = from.BattleEquipment;
            if (eq == null) return;
            var got = new List<string>();
            var overSkill = new List<string>();
            bool req = Settings.Current != null && Settings.Current.HeroGearRequirements;
            for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
            {
                var slot = (EquipmentIndex)i;
                var el = eq[slot];
                if (el.IsEmpty || !Is(el.Item)) continue;
                eq[slot] = StandIn(from, el.Item, death);
                if (ValyrianBlades.Is(el.Item)) ClearTwin(from, el.Item.StringId);   // bohater spoza rodu gracza: blizniak w zestawie cywilnym to ta sama klinga
                if (toPlayer) MobileParty.MainParty.ItemRoster.AddToCounts(el, 1);
                else
                {
                    if (req && to.CharacterObject != null && !ItemReq.MeetsHero(to.CharacterObject, el.Item)) overSkill.Add(el.Item.StringId);   // Wear odlozy (Park)
                    Wear(to, el, null, how);
                }
                got.Add(el.Item.Name.ToString());
            }
            if (got.Count == 0) return;
            string list = string.Join(", ", got.ToArray());
            Log.Info("Kronika unikatow: " + from.Name + " -> " + to.Name + " (" + how + "): " + list
                     + (overSkill.Count > 0 ? " (ponad umiejetnosc - do taboru, bez partii na polke: " + string.Join(", ", overSkill.ToArray()) + ")" : "") + ".");
            if (toPlayer) Log.Player("By the custom of war, the arms of " + from.Name + " are yours: " + list + ".");
            else if (from == Hero.MainHero) Log.Player(to.Name + " takes your arms by the custom of war: " + list + ".", true);
            // cudze zdobycze tylko w kronice (Jeff: za duzo smieci)
        }

        /// <summary>Zamiennik w slocie pojmanego/zabitego (krytyka 177 pkt 28): najpierw najlepsza zwykla sztuka tego samego typu (dla broni
        /// tej samej klasy) z taboru jego wlasnej partii - zdejmowana z taboru; dopiero gdy jej nie ma - zwykly zamiennik kultury
        /// (UniqueLaw.StandInFor, z niczego - jak dotad; pusty slot rozbroilby lorda AI na stale, bo nic go nie dozbraja - liczone
        /// w linii doby, decyzja do Jeffa). Recenzja 177: zabity w walce - pusty slot (martwy nic nie nosi; zwykla sztuka z taboru jego partii
        /// poszlaby na zwloki, czyli w nicosc, a zamiennik z niczego trafilby przy smierci gracza do nastepcy).</summary>
        private static EquipmentElement StandIn(Hero from, ItemObject uniq, bool death)
        {
            if (death) { _dStandDead++; return EquipmentElement.Invalid; }
            try
            {
                // sklad9 (Z16): zamiennik to zakladanie - tylko sztuki, ktore ofiara udzwignie (przy HeroGearRequirements)
                var wearCo = Settings.Current != null && Settings.Current.HeroGearRequirements ? from.CharacterObject : null;
                var party = from.PartyBelongedTo;
                var roster = party != null ? party.ItemRoster : null;
                if (roster != null)
                {
                    var wc = uniq.PrimaryWeapon != null ? uniq.PrimaryWeapon.WeaponClass : WeaponClass.Undefined;
                    int best = -1; ItemObject bestIt = null;
                    for (int i = 0; i < roster.Count; i++)
                    {
                        var e = roster.GetElementCopyAtIndex(i);
                        var it = e.EquipmentElement.Item;
                        if (it == null || e.Amount <= 0 || it.ItemType != uniq.ItemType || Is(it) || LegendaryLaw.IsLegend(it)) continue;
                        if (uniq.HasWeaponComponent && (it.PrimaryWeapon == null || it.PrimaryWeapon.WeaponClass != wc)) continue;
                        if (wearCo != null && !ItemReq.MeetsHero(wearCo, it)) continue;
                        if (best < 0 || it.Tier > bestIt.Tier || (it.Tier == bestIt.Tier && it.Value > bestIt.Value)) { best = i; bestIt = it; }
                    }
                    if (best >= 0)
                    {
                        var el = roster.GetElementCopyAtIndex(best).EquipmentElement;
                        roster.AddToCounts(el, -1);
                        _dStandRoster++;
                        return el;
                    }
                }
                ItemObject stand = null;
                try { stand = UniqueLaw.StandInFor(uniq, from.Culture, from); } catch { }   // Z16: w granicy umiejetnosci ofiary
                if (stand != null) { _dStandNothing++; return new EquipmentElement(stand); }
            }
            catch (Exception e) { if (_stumbles++ < 3) Log.Error("UniqueSpoils.StandIn", e); }
            _dStandNone++;
            return EquipmentElement.Invalid;
        }

        /// <summary>Zdjeto z zestawu bojowego jedna sztuke klingi: u bohatera spoza rodu gracza znika jej blizniak - jeden slot z tym id w zestawie
        /// cywilnym i jeden w zestawie ukrycia (szablon ROT wpisuje te sama klinge w kilka zestawow; to jedna sztuka - ValyrianBlades.Pieces).
        /// Rod gracza: kazdy slot to osobna sztuka (recenzja 177: dawniej kasowalo cywilny miecz gracza) - nic.</summary>
        internal static void ClearTwin(Hero h, string id)
        {
            try
            {
                if (h == null || id == null || ValyrianBlades.Physical(h)) return;
                foreach (var eq in ValyrianBlades.Sets(h))
                {
                    if (ReferenceEquals(eq, h.BattleEquipment)) continue;
                    for (int s = 0; s <= (int)EquipmentIndex.ExtraWeaponSlot; s++)
                    {
                        var it = eq[(EquipmentIndex)s].Item;
                        if (it != null && it.StringId == id) { eq[(EquipmentIndex)s] = EquipmentElement.Invalid; break; }
                    }
                }
            }
            catch { }
        }

        /// <summary>Lord AI zaklada sztuke. 177-2 (krytyka pkt 3 i 16): NIGDY nie wypycha z rak unikatu ani stali valyrianskiej (Longclaw
        /// Jona wypchniety przez luk Ygritte w tescie 120 dob) - gdy kazdy pasujacy slot trzyma unikat, przychodzaca sztuka idzie do
        /// taboru; zdjeta zwykla sztuka tez do taboru. Bez partii (albo partia wlasnie znika - avoid) - na polke miasta rodu.</summary>
        /// sklad9 (Z16, jedna droga dla wszystkich zakladan AI w tym pliku - zdobycz, dziedziczenie, zakup, rozbita partia): przy
        /// HeroGearRequirements bohater zaklada tylko to, co udzwignie (ItemReq.MeetsHero); reszta - Park (tabor jego partii, bez partii -
        /// polka miasta rodu).
        internal static void Wear(Hero h, EquipmentElement el, MobileParty avoid, string why)
        {
            try
            {
                var s0 = Settings.Current;
                if (s0 != null && s0.HeroGearRequirements && h != null && h.CharacterObject != null && el.Item != null
                    && !ItemReq.MeetsHero(h.CharacterObject, el.Item)) { Park(h, el, avoid, why + ", ponad umiejetnosc"); return; }
            }
            catch { }
            var eq = h.BattleEquipment;
            int slot = SlotFor(eq, el.Item);
            if (slot < 0) { Park(h, el, avoid, why); return; }
            var old = eq[(EquipmentIndex)slot];
            eq[(EquipmentIndex)slot] = el;
            if (!old.IsEmpty) Park(h, old, avoid, why);
        }

        /// <summary>Slot dla przychodzacej sztuki albo -1 (wszystkie pasujace sloty trzymaja unikat - nie wypychamy).</summary>
        private static int SlotFor(Equipment eq, ItemObject it)
        {
            int fixedSlot = -1;
            switch (it.ItemType)
            {
                case ItemObject.ItemTypeEnum.HeadArmor: fixedSlot = (int)EquipmentIndex.Head; break;
                case ItemObject.ItemTypeEnum.BodyArmor: fixedSlot = (int)EquipmentIndex.Body; break;
                case ItemObject.ItemTypeEnum.LegArmor: fixedSlot = (int)EquipmentIndex.Leg; break;
                case ItemObject.ItemTypeEnum.HandArmor: fixedSlot = (int)EquipmentIndex.Gloves; break;
                case ItemObject.ItemTypeEnum.Cape: fixedSlot = (int)EquipmentIndex.Cape; break;
                case ItemObject.ItemTypeEnum.HorseHarness: fixedSlot = (int)EquipmentIndex.HorseHarness; break;
            }
            if (fixedSlot >= 0)
            {
                var cur = eq[(EquipmentIndex)fixedSlot];
                return !cur.IsEmpty && Is(cur.Item) ? -1 : fixedSlot;
            }
            // bron: ten sam typ (bez unikatu), potem pusty slot, potem pierwszy slot bez unikatu
            for (int i = 0; i < 4; i++) { var e = eq[(EquipmentIndex)i]; if (!e.IsEmpty && e.Item.ItemType == it.ItemType && !Is(e.Item)) return i; }
            for (int i = 0; i < 4; i++) if (eq[(EquipmentIndex)i].IsEmpty) return i;
            for (int i = 0; i < 4; i++) { var e = eq[(EquipmentIndex)i]; if (!Is(e.Item)) return i; }
            return -1;
        }

        /// <summary>Odlozenie sztuki bohatera: tabor jego partii (nie ta, ktora wlasnie znika), gracz - do taboru gracza, bez partii - polka miasta rodu.</summary>
        private static void Park(Hero h, EquipmentElement el, MobileParty avoid, string why)
        {
            if (el.IsEmpty) return;
            var mp = h != null ? h.PartyBelongedTo : null;
            if (h == Hero.MainHero) mp = MobileParty.MainParty;
            // 177-fix (09.10, test 120 dob kroku B): stal valyrianska nie lezy w taborze partii AI - tabor AI to nie skarbiec (Truth odlozony do taboru
            // Leona Staegone przy pojmaniu Tregara Ormollena zniknal sekunde pozniej, spis: "nigdzie"). Najpierw ktos z rodu, kto ja udzwignie (Z16)
            // i ma slot bez unikatu (glowa rodu pierwsza), potem polka miasta rodu. Gracz i jego rod - jak dotad (tabor gracza / partii rodu gracza).
            if (ValyrianBlades.Is(el.Item) && mp != MobileParty.MainParty && (h == null || h.Clan == null || h.Clan != Clan.PlayerClan))
            {
                if (KinWear(h, el, why)) return;
                Shelve(el, TownFor(h, mp), why + " (" + (h != null ? h.Name.ToString() : "?") + " - stal valyrianska nie do taboru AI)");
                return;
            }
            if (mp != null && mp != avoid && mp.ItemRoster != null) { mp.ItemRoster.AddToCounts(el, 1); _dParked++; return; }
            Shelve(el, TownFor(h, mp), why + " (" + (h != null ? h.Name.ToString() : "?") + " bez partii)");
        }

        /// <summary>177-fix: stal valyrianska bez miejsca w rekach bohatera AI - zaklada ja ktos z jego rodu (zywy, wolny, dorosly, Z16 - udzwignie,
        /// slot bez unikatu), glowa rodu pierwsza. Zdjeta zwykla sztuka idzie przez Park (tabor partii krewnego albo polka). false - nikt.</summary>
        private static bool KinWear(Hero h, EquipmentElement el, string why)
        {
            try
            {
                if (h == null || h.Clan == null || el.Item == null) return false;
                bool req = Settings.Current != null && Settings.Current.HeroGearRequirements;
                Hero best = null; int bestSlot = -1;
                foreach (var k in h.Clan.Heroes)
                {
                    if (k == null || k == h || !k.IsAlive || !k.IsActive || k.IsPrisoner || k.IsChild || k == Hero.MainHero || k.CharacterObject == null) continue;
                    if (req && !ItemReq.MeetsHero(k.CharacterObject, el.Item)) continue;
                    var eq = k.BattleEquipment;
                    if (eq == null) continue;
                    int slot = SlotFor(eq, el.Item);
                    if (slot < 0) continue;
                    if (best == null || k == h.Clan.Leader) { best = k; bestSlot = slot; }
                    if (k == h.Clan.Leader) break;
                }
                if (best == null) return false;
                var beq = best.BattleEquipment;
                var old = beq[(EquipmentIndex)bestSlot];
                beq[(EquipmentIndex)bestSlot] = el;
                _dKin++;
                Log.Info("Kronika unikatow: " + el.Item.StringId + " -> " + best.Name + " (rod " + h.Clan.Name + "; " + why + " - " + h.Name + " jej nie udzwignie).");
                if (!old.IsEmpty) Park(best, old, null, why);
                return true;
            }
            catch (Exception e) { if (_stumbles++ < 3) Log.Error("UniqueSpoils.KinWear", e); return false; }
        }

        /// <summary>Na polke targu miasta (unikat sprzedany albo bez wlasciciela lezy na polce - Jeff 04.10). Bez miasta - nic (log).</summary>
        internal static void Shelve(EquipmentElement el, Settlement town, string why)
        {
            if (el.IsEmpty) return;
            if (town == null || town.ItemRoster == null)
            {
                Log.Error("UniqueSpoils.Shelve", new InvalidOperationException("brak miasta dla " + (el.Item != null ? el.Item.StringId : "?") + " (" + why + ") - sztuka nie ma gdzie lezec"));
                return;
            }
            town.ItemRoster.AddToCounts(el, 1);
            _dShelved++;
            if (Is(el.Item)) Log.Info("Kronika unikatow: " + el.Item.StringId + " na polke " + town.Name + " (" + why + ").");
        }

        /// <summary>Miasto dla sztuki bohatera: siedziba rodu (zamek - najblizsze miasto), potem osada, w ktorej jest / ostatnio byl, potem
        /// najblizsze miasto do jego partii, na koniec pierwsze miasto swiata.</summary>
        internal static Settlement TownFor(Hero h, MobileParty mp)
        {
            try
            {
                Settlement s = null;
                if (h != null && h.Clan != null && h.Clan.HomeSettlement != null) s = h.Clan.HomeSettlement;
                if (s == null && h != null) s = h.CurrentSettlement ?? h.LastKnownClosestSettlement ?? h.HomeSettlement;
                if (s != null && s.IsTown) return s;
                Vec2 p;
                if (s != null) p = s.GetPosition2D;
                else if (mp != null) p = mp.GetPosition2D;
                else if (h != null && h.PartyBelongedToAsPrisoner != null && h.PartyBelongedToAsPrisoner.MobileParty != null) p = h.PartyBelongedToAsPrisoner.MobileParty.GetPosition2D;
                else p = Vec2.Invalid;
                Settlement best = null; float bd = float.MaxValue;
                foreach (var t in Settlement.All)
                {
                    if (t == null || !t.IsTown) continue;
                    if (!p.IsValid) return t;
                    float d = p.DistanceSquared(t.GetPosition2D);
                    if (d < bd) { bd = d; best = t; }
                }
                return best;
            }
            catch { return null; }
        }

        internal static bool Usable(Hero h) { return h != null && h.IsAlive && !h.IsDisabled; }

        /// <summary>Dziedzic (Jeff 31.08 "nie zyja - spadkobiercom"): glowa rodu (gra zmienia ja PRZED zdarzeniem smierci), potem dorosle
        /// dzieci, dzieci, malzonek, rodzenstwo, inny czlonek rodu. Null - nikogo.</summary>
        internal static Hero HeirOf(Hero dead)
        {
            if (dead == null) return null;
            var c = dead.Clan;
            if (c != null && c.Leader != null && c.Leader != dead && Usable(c.Leader)) return c.Leader;
            try
            {
                var kids = dead.Children;
                if (kids != null)
                {
                    foreach (var k in kids) if (k != dead && Usable(k) && !k.IsChild) return k;
                    foreach (var k in kids) if (k != dead && Usable(k)) return k;
                }
            }
            catch { }
            if (Usable(dead.Spouse)) return dead.Spouse;
            try { foreach (var s in dead.Siblings) if (s != dead && Usable(s)) return s; } catch { }
            if (c != null)
                foreach (var m in c.Heroes) if (m != dead && Usable(m) && !m.IsChild) return m;
            return null;
        }

        /// <summary>Przekazanie sztuki bohaterowi: rod gracza (gracz, towarzysze, rodzina) - do taboru gracza z komunikatem; lord AI - zaklada;
        /// nikt - polka miasta (siedziba rodu "near", inaczej miasto najblizsze jego partii albo partii "at"). Recenzja 177: why - powod do logu
        /// (po polsku), playerWhy - powod w komunikacie dla gracza (po angielsku). Zwraca miasto, gdy sztuka poszla na polke.</summary>
        internal static Settlement Give(Hero to, EquipmentElement el, Hero near, MobileParty avoid, string why, string playerWhy, MobileParty at = null)
        {
            if (el.IsEmpty) return null;
            if (Usable(to) && (to == Hero.MainHero || to.Clan == Clan.PlayerClan) && MobileParty.MainParty != null)
            {
                MobileParty.MainParty.ItemRoster.AddToCounts(el, 1);
                Log.Player(el.Item.Name + " comes to you" + (string.IsNullOrEmpty(playerWhy) ? "." : " (" + playerWhy + ")."));
                Log.Info("Kronika unikatow: " + el.Item.StringId + " do taboru gracza (" + why + ").");
                return null;
            }
            if (Usable(to)) { Wear(to, el, avoid, why); return null; }
            var town = TownFor(near, near != null && near.PartyBelongedTo != null ? near.PartyBelongedTo : at);
            Shelve(el, town, why);
            return town;
        }

        /// <summary>177-2 DZIEDZICZENIE stali valyrianskiej (krytyka pkt 7 - tylko VS: unikatowa zbroja na dziedzicu zeszlaby przy wczytaniu
        /// przez UniqueLaw.SweepHeroes). Smierc gracza - gra ma swoja droge (HeirSelectionCampaignBehavior przekazuje caly ekwipunek
        /// nastepcy), wiec tu jej nie dotykamy (inaczej klinga przeszlaby dwa razy).</summary>
        private static void Inherit(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail)
        {
            if (victim == null || victim == Hero.MainHero || !Settings.Current.UniqueInheritance) return;
            // recenzja 177: kazda sztuka (zestaw bojowy, cywilny, ukrycia - ValyrianBlades.Pieces); dawniej druga taka sama klinga szla w nicosc
            var ids = ValyrianBlades.TakePieces(victim, null, int.MaxValue);
            if (ids.Count == 0) return;
            bool execution = detail == KillCharacterAction.KillCharacterActionDetail.Executed || detail == KillCharacterAction.KillCharacterActionDetail.ExecutionAfterMapEvent;
            bool byAxe = execution && Usable(killer);
            var to = byAxe ? killer : HeirOf(victim);
            string how = byAxe ? "egzekucja" : "dziedzictwo";
            string playerWhy = byAxe ? "by the executioner's right over " + victim.Name : "as heir of " + victim.Name;
            Settlement town = null;
            foreach (var el in ids)
            {
                var t = Give(to, el, victim, null, how + " po " + victim.Name, playerWhy);
                if (t != null) town = t;
                _dInherit++;
            }
            string list = string.Join(", ", ids.Select(x => x.Item.StringId).ToArray());
            Log.Info("Kronika unikatow: " + how + " " + victim.Name + " -> " + (to != null ? to.Name.ToString() : "polka " + (town != null ? town.Name.ToString() : "miasta")) + ": " + list + ".");
            try
            {
                // gracz i jego rod dostaja komunikat w Give ("comes to you"); tu tylko klinga zmarlego z rodu gracza, ktora idzie poza rod
                bool toPlayerClan = to != null && Usable(to) && (to == Hero.MainHero || to.Clan == Clan.PlayerClan);
                if (victim.Clan == Clan.PlayerClan && !toPlayerClan)
                    Log.Player(string.Join(", ", ids.Select(x => x.Item.Name.ToString()).ToArray()) + " of " + victim.Name + " passes to "
                               + (to != null && Usable(to) ? to.Name.ToString() : "the market of " + (town != null ? town.Name.ToString() : "a town")) + ".");
            }
            catch { }
        }

        /// <summary>177-2 (krytyka pkt 3 i 16): rozbita albo rozwiazana partia AI. Gra lupi z taboru pokonanych tylko !NotMerchandise, a resztka
        /// ginie z partia (DestroyPartyAction: zdarzenie PRZED RemoveParty - tabor jeszcze jest). Unikaty: zwyciezca-gracz - do taboru
        /// gracza; lord AI - stal valyrianska zaklada, reszta do taboru jego partii; rozwiazanie bez zwyciezcy - wlasciciel (wodz, wlasciciel
        /// partii, glowa rodu), na koniec polka miasta.</summary>
        internal static void OnPartyDestroyed(MobileParty mp, PartyBase destroyer)
        {
            try
            {
                if (mp == null || mp == MobileParty.MainParty || mp.ItemRoster == null || !Settings.Current.UniqueNeverLost) return;
                var roster = mp.ItemRoster;
                List<EquipmentElement> got = null;
                for (int i = roster.Count - 1; i >= 0; i--)
                {
                    var e = roster.GetElementCopyAtIndex(i);
                    if (e.Amount <= 0 || !Is(e.EquipmentElement.Item)) continue;
                    if (got == null) got = new List<EquipmentElement>();
                    for (int n = 0; n < e.Amount; n++) got.Add(e.EquipmentElement);
                    roster.AddToCounts(e.EquipmentElement, -e.Amount);
                }
                if (got == null) return;
                string to;
                if (destroyer == PartyBase.MainParty)
                {
                    foreach (var el in got) MobileParty.MainParty.ItemRoster.AddToCounts(el, 1);
                    Log.Player("You find " + string.Join(", ", got.Select(x => x.Item.Name.ToString()).ToArray()) + " among the baggage of " + mp.Name + ".");
                    to = "gracz";
                }
                else
                {
                    var dmp = destroyer != null ? destroyer.MobileParty : null;
                    Hero lead = dmp != null ? dmp.LeaderHero : null;
                    if (lead == null) lead = mp.LeaderHero;
                    if (lead == null && mp.Party != null) lead = mp.Party.Owner;
                    if (lead == null && mp.ActualClan != null) lead = mp.ActualClan.Leader;
                    to = lead != null ? lead.Name.ToString() : "polka miasta";
                    foreach (var el in got)
                    {
                        // recenzja 177: bez nikogo (lead == null) - miasto najblizsze miejsca, gdzie partia znika (at = mp), nie pierwsze miasto swiata
                        if (ValyrianBlades.Is(el.Item)) Give(lead, el, lead, mp, "tabor rozbitej partii " + mp.Name, "from the baggage of " + mp.Name, mp);
                        else if (dmp != null && dmp != mp && dmp.ItemRoster != null && !dmp.IsMainParty) dmp.ItemRoster.AddToCounts(el, 1);
                        else if (Usable(lead) && lead.PartyBelongedTo != null && lead.PartyBelongedTo != mp && lead.PartyBelongedTo.ItemRoster != null) lead.PartyBelongedTo.ItemRoster.AddToCounts(el, 1);
                        else Shelve(el, TownFor(lead, mp), "tabor rozbitej partii " + mp.Name);
                    }
                }
                _dDestroyed += got.Count;
                Log.Info("Kronika unikatow: partia " + mp.Name + " znika (" + (destroyer != null ? "rozbita przez " + destroyer.Name : "rozwiazana") + ") - unikaty z taboru -> " + to + ": "
                         + string.Join(", ", got.Select(x => x.Item.StringId).ToArray()) + ".");
            }
            catch (Exception e) { Log.Error("UniqueSpoils.OnPartyDestroyed", e); }
        }

        /// <summary>Smierc gracza (krytyka pkt 24 - SPRAWDZONE: gra NIE zostawia ekwipunku na martwym): HeirSelectionCampaignBehavior.
        /// OnBeforePlayerCharacterChanged kopiuje zestaw bojowy I cywilny starego gracza do taboru nastepcy (OnPlayerCharacterChanged dodaje
        /// je do taboru nowej partii gracza), zestawu ukrycia - nie. Recenzja 177: cywilny miecz gracza to osobna sztuka (dawniej kasowany
        /// jako "duplikat" - prawdziwa klinga w nicosc), wiec go nie ruszamy; stal valyrianska z zestawu ukrycia zdejmujemy tu i dajemy
        /// nastepcy po zmianie gracza (OnPlayerChanged) - inaczej zostalaby na zmarlym, poza swiatem.</summary>
        internal static void OnBeforePlayerChanged(Hero oldPlayer, Hero newPlayer)
        {
            try
            {
                _heirStealth.Clear();
                if (oldPlayer == null) return;
                foreach (var eq in ValyrianBlades.Sets(oldPlayer))
                {
                    if (ReferenceEquals(eq, oldPlayer.BattleEquipment) || ReferenceEquals(eq, oldPlayer.CivilianEquipment)) continue;
                    for (int s = 0; s <= (int)EquipmentIndex.ExtraWeaponSlot; s++)
                    {
                        var el = eq[(EquipmentIndex)s];
                        if (el.IsEmpty || !ValyrianBlades.Is(el.Item)) continue;
                        _heirStealth.Add(el);
                        eq[(EquipmentIndex)s] = EquipmentElement.Invalid;
                    }
                }
            }
            catch (Exception e) { Log.Error("UniqueSpoils.OnBeforePlayerChanged", e); }
        }

        /// <summary>Po zmianie gracza: stal valyrianska z zestawu ukrycia poprzednika - do taboru nowej partii gracza (jak reszta spadku w grze).</summary>
        internal static void OnPlayerChanged(Hero oldPlayer, Hero newPlayer, MobileParty newMainParty, bool isMainPartyChanged)
        {
            if (_heirStealth.Count == 0) return;
            try
            {
                var mp = newMainParty ?? MobileParty.MainParty;
                foreach (var el in _heirStealth)
                {
                    if (mp != null && mp.ItemRoster != null) mp.ItemRoster.AddToCounts(el, 1);
                    else Shelve(el, TownFor(newPlayer, null), "spadek po graczu (zestaw ukrycia)");
                }
                Log.Info("Kronika unikatow: spadek po " + (oldPlayer != null ? oldPlayer.Name.ToString() : "graczu") + " - z zestawu ukrycia do taboru nastepcy: "
                         + string.Join(", ", _heirStealth.Select(x => x.Item.StringId).ToArray()) + ".");
            }
            catch (Exception e) { Log.Error("UniqueSpoils.OnPlayerChanged", e); }
            _heirStealth.Clear();
        }

        // ------------------------------------------------------------ przebranie bohatera (zmiana rodu panujacego, dorosniecie)
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(global::Helpers.EquipmentHelper), "AssignHeroEquipmentFromEquipment", new[] { typeof(Hero), typeof(Equipment) });
                if (m == null) { Log.Info("UniqueSpoils: EquipmentHelper.AssignHeroEquipmentFromEquipment nieznaleziona - przebranie wladcy bez ochrony unikatow."); return; }
                h.Patch(m, prefix: new HarmonyMethod(typeof(UniqueSpoils), nameof(AssignPrefix)) { priority = Priority.Last },
                           postfix: new HarmonyMethod(typeof(UniqueSpoils), nameof(AssignPostfix)));
                Log.Info("UniqueSpoils: przebranie bohatera (zmiana rodu panujacego, dorosniecie) zostawia unikaty i stal valyrianska w rekach.");
            }
            catch (Exception e) { Log.Error("UniqueSpoils.ApplyAll", e); }
        }

        /// <summary>Zestaw, ktory gra nadpisuje (jak EquipmentHelper.AssignHeroEquipmentFromEquipment: ukrycia, cywilny albo bojowy).</summary>
        private static Equipment AssignTarget(Hero h, Equipment from)
        {
            return from.IsStealth ? h.StealthEquipment : from.IsCivilian ? h.CivilianEquipment : h.BattleEquipment;
        }

        /// <summary>Prefiks: zapamietaj unikaty i stal valyrianska z nadpisywanego zestawu (bojowy, cywilny albo - recenzja 177 - ukrycia). Priority.Last -
        /// po straznikach innych (CrashScribe DressedOrNot zwraca false przy braku zestawu - wtedy nic sie nie zmienia i postfiks nic nie robi).</summary>
        public static void AssignPrefix(Hero __0, Equipment __1, out List<KeyValuePair<int, EquipmentElement>> __state)
        {
            __state = null;
            try
            {
                if (__0 == null || __1 == null || !Settings.Current.UniqueNeverLost) return;
                var target = AssignTarget(__0, __1);
                if (target == null) return;
                for (int i = 0; i < 12; i++)
                {
                    var el = target[(EquipmentIndex)i];
                    if (el.IsEmpty || !Is(el.Item)) continue;
                    if (__state == null) __state = new List<KeyValuePair<int, EquipmentElement>>();
                    __state.Add(new KeyValuePair<int, EquipmentElement>(i, el));
                }
            }
            catch { __state = null; }
        }

        /// <summary>Postfiks: unikat wraca w ten sam slot; sztuka z szablonu, ktora go zastapila (wlasnie stworzona przez gre), odpada.</summary>
        public static void AssignPostfix(Hero __0, Equipment __1, List<KeyValuePair<int, EquipmentElement>> __state)
        {
            if (__state == null) return;
            try
            {
                var target = AssignTarget(__0, __1);
                int n = 0;
                foreach (var kv in __state)
                {
                    var cur = target[(EquipmentIndex)kv.Key];
                    if (cur.Item == kv.Value.Item && cur.ItemModifier == kv.Value.ItemModifier) continue;
                    target[(EquipmentIndex)kv.Key] = kv.Value;
                    n++;
                }
                if (n > 0)
                {
                    _dRestored += n;
                    Log.Info("Kronika unikatow: " + __0.Name + " przebrany przez gre (" + (__1.IsStealth ? "ukrycia" : __1.IsCivilian ? "cywilny" : "bojowy") + ") - zostaje przy "
                             + string.Join(", ", __state.Select(x => x.Value.Item.StringId).ToArray()) + ".");
                }
            }
            catch (Exception e) { if (_stumbles++ < 3) Log.Error("UniqueSpoils.AssignPostfix", e); }
        }

        // ------------------------------------------------------------ lord AI kupuje unikat z polki
        internal static void OnDailyTickParty(MobileParty mp)
        {
            try
            {
                if (mp == null || mp.IsMainParty || !mp.IsLordParty || mp.LeaderHero == null || mp.CurrentSettlement == null) return;
                var st = mp.CurrentSettlement;
                if (!st.IsTown || st.Town == null || st.ItemRoster == null) return;
                var lord = mp.LeaderHero;
                var shelf = st.ItemRoster;
                for (int i = shelf.Count - 1; i >= 0; i--)
                {
                    var el = shelf.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (el.Amount <= 0 || !Is(it)) continue;
                    // Z16 (Jeff 09.10): kupuje tylko to, co udzwignie - CALY ekwipunek (pancerz: Atletyka), nie tylko RelevantSkill
                    if (Settings.Current != null && Settings.Current.HeroGearRequirements)
                    { if (!ItemReq.MeetsHero(lord.CharacterObject, it)) continue; }
                    else if (it.Difficulty > 0 && it.RelevantSkill != null && lord.GetSkillValue(it.RelevantSkill) < it.Difficulty) continue;
                    if (ValyrianBlades.Is(it) && ValyrianBlades.Holds(lord, it.StringId)) continue;   // recenzja 177: drugiej takiej samej klingi nie kupuje
                    int slot = SlotFor(lord.BattleEquipment, it);
                    if (slot < 0) continue;                                    // kazdy pasujacy slot trzyma unikat
                    var cur = lord.BattleEquipment[(EquipmentIndex)slot];
                    if (!cur.IsEmpty && cur.Item.Effectiveness >= it.Effectiveness) continue;
                    int price = st.Town.MarketData.GetPrice(el.EquipmentElement, mp, false, st.Party);
                    if (price <= 0 || lord.Gold < price * 2) continue;     // nie kupi za ostatnie pieniadze
                    shelf.AddToCounts(el.EquipmentElement, -1);
                    lord.ChangeHeroGold(-price);
                    st.Town.ChangeGold(price);
                    MoneyLedger.Note169(MoneyLedger.N169Other, st, price);   // paczka 169: linia kas (tylko licznik)
                    Wear(lord, el.EquipmentElement, null, "zakup");
                    Log.Info("Kronika unikatow: " + lord.Name + " kupil " + it.StringId + " w " + st.Name + " za " + price + ".");

                    return;
                }
            }
            catch (Exception e) { Log.Error("UniqueSpoils.Buy", e); }
        }

        // ------------------------------------------------------------ kronika
        internal static void Daily()
        {
            try
            {
                var now = new Dictionary<string, string>();
                Action<string, string> add = (id, where) =>
                {
                    string v; now[id] = now.TryGetValue(id, out v) ? v + "; " + where : where;
                };
                foreach (var h in Hero.AllAliveHeroes)
                {
                    if (h == null) continue;
                    var eq = h.BattleEquipment;
                    if (eq == null) continue;
                    for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
                    {
                        var el = eq[(EquipmentIndex)i];
                        if (!el.IsEmpty && Is(el.Item)) add(el.Item.StringId, "nosi " + h.Name);
                    }
                }
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || mp.ItemRoster == null) continue;
                    var r = mp.ItemRoster;
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r.GetElementCopyAtIndex(i);
                        if (el.Amount > 0 && Is(el.EquipmentElement.Item)) add(el.EquipmentElement.Item.StringId, "tabor " + mp.Name + (el.Amount > 1 ? " x" + el.Amount : ""));
                    }
                }
                foreach (var st in Settlement.All)
                {
                    if (st == null || st.ItemRoster == null) continue;
                    var r = st.ItemRoster;
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r.GetElementCopyAtIndex(i);
                        if (el.Amount > 0 && Is(el.EquipmentElement.Item)) add(el.EquipmentElement.Item.StringId, "targ " + st.Name + (el.Amount > 1 ? " x" + el.Amount : ""));
                    }
                }
                // 177-2: stal valyrianska takze w miejscach, ktorych powyzszy spis nie widzi (cywilny zestaw, nieaktywni, schowki, magazyny) -
                // z ostatniego spisu ValyrianBlades; zamiast "nigdzie" - prawdziwe miejsce
                foreach (var kv in ValyrianBlades.ElsewhereFromCensus())
                    if (!now.ContainsKey(kv.Key)) now[kv.Key] = kv.Value;
                var changes = new List<string>();
                foreach (var kv in now)
                {
                    string was;
                    if (!_last.TryGetValue(kv.Key, out was) || was != kv.Value) changes.Add(kv.Key + ": " + kv.Value);
                }
                foreach (var kv in _last) if (!now.ContainsKey(kv.Key)) changes.Add(kv.Key + ": nigdzie (zniszczony/stracony z bohaterem?)");
                bool first = _last.Count == 0;
                _last = now;
                if (changes.Count > 0)
                    Log.Info("Kronika unikatow: dzien " + (int)CampaignTime.Now.ToDays + (first ? " - stan swiata (" + now.Count + " unikatow w obiegu): " : " - zmiany: ") + string.Join(" | ", changes.ToArray()) + ".");
                if (_dInherit + _dDestroyed + _dRestored + _dParked + _dShelved + _dStandRoster + _dStandNothing + _dStandNone + _dStandDead > 0)
                    Log.Info("Unikaty (177): dzien " + (int)CampaignTime.Now.ToDays + " - dziedziczenie stali valyrianskiej " + _dInherit + ", unikaty z taborow znikajacych partii " + _dDestroyed
                             + ", zostawione w rekach przy przebraniu przez gre " + _dRestored + ", odlozone do taboru " + _dParked + ", stal valyrianska do krewnego " + _dKin + ", na polke miasta " + _dShelved
                             + "; zamiennik pojmanego/zabitego: z jego taboru " + _dStandRoster + ", z niczego (zwykly zamiennik kultury) " + _dStandNothing + ", brak (pusty slot) " + _dStandNone + "; zabity w walce - pusty slot " + _dStandDead + ".");
            }
            catch (Exception e) { Log.Error("UniqueSpoils.Daily", e); }
            ClearDay();
        }
    }
}
