using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// REKRUT GRACZA JAK U AI (E8/T = 263, PLAN 1.12; decyzja Jeffa 08.10: "werbunek gracza jak u AI - rekrut od tieru 2 tylko ze sprzetem kupionym przez
    /// notabla"; projekt etapu 8 v2, krytyka D6: gracz ta sama sciezka RecruitKit co AI, najemnikow regula "tylko kupione" nie tnie).
    ///
    /// Dotad: DTE dawal graczowi przy kazdym werbunku PELNY komplet z niczego (RecruitmentPatch / TavernMercenaryEquipmentPatch /
    /// TownMenuMercenaryEquipmentPatch -> AddStartingEquipmentToPlayerArmory -> zbrojownia druzyny ArmyArmory), a zapis kompletu ochotnika (to, co notabl
    /// naprawde kupil na targu) zostawal sierota w puli - notabl sprzedawal te rzeczy z powrotem na targ (RecruitKit.Reconcile). Gra i BK oglaszaja werbunek
    /// gracza zdarzeniem OnUnitRecruited (bez notabla), wiec sciezka AI (OnTroopRecruited -> RecruitKit.OnRecruited) gracza nie widziala.
    ///
    /// Teraz (klucz PlayerRecruitNoFreeKit, czynny tylko razem z RecruitKit.On):
    ///  - okno werbunku gracza: prefiks First + finalizer na RecruitmentVM.OnDone (przed prefiksem BK, ktory werbuje sam i zwraca false) - koszyk
    ///    (notabl, oddzial) i stan druzyny przed; okno najmu: prefiks First + finalizer na RecruitmentCampaignBehavior.BuyMercenaries (rozmowa w karczmie)
    ///    i buy_mercenaries_on_consequence (menu zaulka) - miasto, typ najemnika i stan druzyny przed;
    ///  - prefiks na DTE AddStartingEquipmentToPlayerArmory: w otwartym oknie komplet DTE dla tieru 2+ nie idzie (tier 1 - jak u AI - komplet DTE = wlasny
    ///    dobytek); poza oknem i przy wylaczonym kluczu DTE jak dotad;
    ///  - finalizer (po wszystkich postfiksach, takze DTE): ilu ludzi naprawde przybylo druzynie (roznica stanu - niezaleznie od tego, czy DTE cokolwiek
    ///    liczyl) i dla kazdego rzeczy ta sama regula co u AI: ochotnik - zapis notabla (kupione + dobytek + dorobek stuleci; zapis zdjety) albo dobytek,
    ///    najemnik z karczmy - dobytek oraz kon i rzad (RecruitKit.MercHorseKit: przy VeteranKeepsHorse kon z puli koni weteranow tej karczmy).
    /// Sztuki ida do zbrojowni druzyny droga DTE (ArmyArmory.AddItemToArmory: czarna lista DTE, prawo unikatow), sztuka kupiona przez notabla zachowuje stan.
    /// Nic nie powstaje: kupione rzeczy notabl zdjal z polki targu i zaplacil za nie, dobytek i dorobek stuleci to reguly wpisow 92 / 79 (jak u AI).
    /// Bez zapisu w SyncData (okno zyje w jednej klatce), Reset z RecruitKit.Reset.
    /// </summary>
    internal static class PlayerRecruit
    {
        private const int Hooks = 4;
        private static MethodInfo _dteAdd;      // DTE ArmyArmory.AddItemToArmory(ItemObject, int)
        private static bool _dteWired;          // prefiks na DTE AddStartingEquipmentToPlayerArmory wpiety - bez niego sciezka nieczynna (inaczej komplet DTE + nasze rzeczy)
        private static int _wired, _stumbles;
        private static long _tMen, _tPcs;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static Win _win;

        /// <summary>Okno jednego werbunku / najmu gracza (prefiks -> finalizer). Publiczne - typ stanu latki Harmony.</summary>
        public sealed class Win
        {
            public Win Prev;
            public bool On, Merc;
            public Settlement Town;
            public readonly List<KeyValuePair<Hero, CharacterObject>> Cart = new List<KeyValuePair<Hero, CharacterObject>>();
            public readonly Dictionary<CharacterObject, int> Before = new Dictionary<CharacterObject, int>();
            public int DteBlocked, DteTier1;
        }

        internal static bool On
        {
            get
            {
                var s = Settings.Current;
                return s != null && s.PlayerRecruitNoFreeKit && RecruitKit.On && _dteWired && _dteAdd != null && QuartermasterLaw.DteArmory() != null;
            }
        }

        internal static void Reset() { _win = null; _stumbles = 0; _tMen = _tPcs = 0; _errSites.Clear(); }

        internal static string State() { return "latki " + _wired + "/" + Hooks + ", sciezka " + (On ? "czynna" : "nieczynna") + ", potkniecia " + _stumbles; }

        /// <summary>Wyjatek w jednym oknie: liczony (linia sesji i linie werbunku), Log.Error raz na miejsce w sesji; nic nie gasnie - nastepne okno biegnie normalnie.</summary>
        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errSites.Add(where)) Log.Error("PlayerRecruit." + where, e); } catch { }
        }

        // ------------------------------------------------------------ okna
        /// <summary>Prefiks (First) RecruitmentVM.OnDone: koszyk (notabl, oddzial) i stan druzyny przed werbunkiem. Bez parametrow zmieniajacych oryginal -
        /// biegnie takze wtedy, gdy inny prefiks (BK) pomija metode gry.</summary>
        public static void VolPre(RecruitmentVM __instance, out Win __state)
        {
            var w = new Win { Prev = _win };
            __state = w; _win = w;
            try
            {
                w.On = On;
                if (!w.On) return;
                var roster = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster : null;
                if (roster == null || __instance == null || __instance.TroopsInCart == null) { w.On = false; return; }
                foreach (var t in __instance.TroopsInCart)
                {
                    if (t == null || t.IsTroopEmpty || t.Character == null) continue;
                    Hero n = null;
                    try { n = t.Owner != null ? t.Owner.OwnerHero : null; } catch { }
                    w.Cart.Add(new KeyValuePair<Hero, CharacterObject>(n, t.Character));
                    if (!w.Before.ContainsKey(t.Character)) w.Before[t.Character] = roster.GetTroopCount(t.Character);
                }
            }
            catch (Exception e) { w.On = false; Stumble("VolPre", e); }
        }

        /// <summary>Prefiks (First) RecruitmentCampaignBehavior.BuyMercenaries / buy_mercenaries_on_consequence: miasto karczmy, typ najemnika i stan druzyny przed
        /// (typ z danych karczmy - jak DTE w menu - oraz rozmowca - jak DTE w rozmowie; zwykle ten sam).</summary>
        public static void MercPre(RecruitmentCampaignBehavior __instance, out Win __state)
        {
            var w = new Win { Prev = _win, Merc = true };
            __state = w; _win = w;
            try
            {
                w.On = On;
                if (!w.On) return;
                var roster = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster : null;
                if (roster == null || __instance == null) { w.On = false; return; }
                var st = Settlement.CurrentSettlement ?? PlayerEncounter.EncounterSettlement;
                w.Town = st;
                CharacterObject a = null, b = null;
                try { if (st != null && st.Town != null) { var md = __instance.GetMercenaryData(st.Town); a = md != null ? md.TroopType : null; } } catch { }
                try { b = CharacterObject.OneToOneConversationCharacter; } catch { }
                if (a != null && !a.IsHero) w.Before[a] = roster.GetTroopCount(a);
                if (b != null && !b.IsHero && !w.Before.ContainsKey(b)) w.Before[b] = roster.GetTroopCount(b);
            }
            catch (Exception e) { w.On = false; Stumble("MercPre", e); }
        }

        /// <summary>Prefiks DTE RecruitmentPatch.AddStartingEquipmentToPlayerArmory(character, troopCount) - jedyne miejsce, w ktorym DTE daje graczowi komplet
        /// rekruta (wolane z trzech latek DTE). W naszym otwartym oknie: tier 2+ bez kompletu DTE (rzeczy da finalizer okna), tier 1 - komplet DTE jak dotad.</summary>
        public static bool DteKitPrefix(CharacterObject __0, int __1)
        {
            try
            {
                var w = _win;
                if (w == null || !w.On || __0 == null || __1 <= 0) return true;
                if (__0.Tier <= 1) { w.DteTier1 += __1; return true; }
                w.DteBlocked += __1;
                return false;
            }
            catch { return true; }
        }

        /// <summary>Finalizer okien (po wszystkich postfiksach, takze po latkach DTE): zamkniecie okna i rozliczenie ludzi, ktorzy naprawde przybyli.</summary>
        public static Exception WinFin(Exception __exception, Win __state)
        {
            try
            {
                if (__state != null)
                {
                    _win = __state.Prev;
                    if (__state.On) Settle(__state);
                }
            }
            catch (Exception e) { Stumble("WinFin", e); }
            return __exception;
        }

        // ------------------------------------------------------------ rozliczenie
        private static void Settle(Win w)
        {
            var roster = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster : null;
            if (roster == null) return;
            int men = 0, rec = 0, tpl = 0, own = 0, pcs = 0, refused = 0, t1 = 0, horsePcs = 0, noCart = 0;
            foreach (var kv in w.Before)
            {
                var troop = kv.Key;
                if (troop == null || troop.IsHero) continue;
                int delta = roster.GetTroopCount(troop) - kv.Value;
                if (delta <= 0) continue;
                if (troop.Tier <= 1) { t1 += delta; continue; }   // tier 1: wlasny dobytek = komplet DTE (regula 3 jak u AI)
                for (int i = 0; i < delta; i++)
                {
                    List<EquipmentElement> items; int kind;
                    try
                    {
                        if (w.Merc) { int hp; items = RecruitKit.PlayerMerc(w.Town, troop, out kind, out hp); horsePcs += hp; }
                        else
                        {
                            Hero n = null; bool found = false;
                            for (int c = 0; c < w.Cart.Count; c++)
                                if (w.Cart[c].Value == troop) { n = w.Cart[c].Key; w.Cart.RemoveAt(c); found = true; break; }
                            if (!found) noCart++;
                            items = RecruitKit.PlayerVolunteer(n, troop, out kind);
                        }
                    }
                    catch (Exception e) { Stumble("Items", e); continue; }
                    men++;
                    if (kind == RecruitKit.PlRecord) rec++; else if (kind == RecruitKit.PlTemplate) tpl++; else own++;
                    foreach (var e in items)
                    {
                        if (e.Item == null) continue;
                        if (AddToStores(e)) pcs++; else refused++;
                    }
                }
            }
            if (men + t1 + w.DteBlocked + w.DteTier1 <= 0) return;
            _tMen += men; _tPcs += pcs;
            int day = 0; try { day = (int)CampaignTime.Now.ToDays; } catch { }
            string where = w.Merc ? "najemnicy z karczmy" + (w.Town != null ? " (" + w.Town.Name + ")" : "") : "ochotnicy od notabli";
            Log.Info("Rekrut gracza (E8/T): dzien " + day + " - " + where + ": ludzi tieru 2+ " + men + " (z zapisu notabla " + rec + ", z tym co ma " + own
                     + ", wzorzec - wylacznik " + tpl + "), do zbrojowni druzyny " + pcs + " szt. (w tym konie i rzedy najemnikow " + horsePcs + "; nieprzyjete przez DTE " + refused
                     + "), komplet DTE zablokowany dla " + w.DteBlocked + " ludzi; tier 1 z wlasnym dobytkiem " + t1 + " (komplet DTE dla " + w.DteTier1
                     + "); ochotnik spoza koszyka " + noCart + "; od startu sesji ludzi " + _tMen + ", szt. " + _tPcs + "; potkniecia " + _stumbles + ".");
            GearFit.PlayerNotice();   // GearFit (noc 15/16.10): jedno zdanie w grze, gdy rekruci oddali na targ rzeczy, ktorych nie uzyja
        }

        /// <summary>Jedna sztuka do zbrojowni druzyny gracza droga DTE (rozpoznanie sztuki, czarna lista DTE, prawo unikatow - UniqueLaw.IntakePlayer);
        /// true tylko, gdy zbrojownia naprawde urosla o te sztuke. Sztuka ze stanem (kupiona przez notabla obita) zachowuje stan.</summary>
        private static bool AddToStores(EquipmentElement e)
        {
            try
            {
                var arm = QuartermasterLaw.DteArmory();
                if (arm == null || _dteAdd == null) return false;
                int before = arm.GetItemNumber(e.Item);
                _dteAdd.Invoke(null, new object[] { e.Item, 1 });
                if (arm.GetItemNumber(e.Item) - before != 1) return false;
                if (e.ItemModifier != null)
                {
                    var plain = new EquipmentElement(e.Item);
                    if (arm.FindIndexOfElement(plain) >= 0) { arm.AddToCounts(plain, -1); arm.AddToCounts(e, 1); }
                }
                return true;
            }
            catch (Exception ex) { Stumble("AddToStores", ex); return false; }
        }

        // ------------------------------------------------------------ latki
        internal static void ApplyAll(Harmony h)
        {
            _wired = 0; _dteWired = false;
            var miss = new List<string>();
            try
            {
                var tArm = AccessTools.TypeByName("DynamicTroopEquipmentReupload.ArmyArmory");
                _dteAdd = tArm != null ? AccessTools.Method(tArm, "AddItemToArmory", new[] { typeof(ItemObject), typeof(int) }) : null;
                var tRec = AccessTools.TypeByName("DynamicTroopEquipmentReupload.Patches.RecruitmentPatch");
                var m = tRec != null ? AccessTools.Method(tRec, "AddStartingEquipmentToPlayerArmory", new[] { typeof(CharacterObject), typeof(int) }) : null;
                if (m != null && _dteAdd != null)
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(PlayerRecruit), nameof(DteKitPrefix)));
                    _dteWired = true; _wired++;
                }
                else miss.Add(m == null ? "DTE AddStartingEquipmentToPlayerArmory" : "DTE ArmyArmory.AddItemToArmory");
            }
            catch (Exception e) { Log.Error("PlayerRecruit.Dte", e); miss.Add("DTE (wyjatek)"); }
            try
            {
                var m = AccessTools.Method(typeof(RecruitmentVM), "OnDone", Type.EmptyTypes);
                if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(PlayerRecruit), nameof(VolPre)) { priority = Priority.First }, finalizer: new HarmonyMethod(typeof(PlayerRecruit), nameof(WinFin))); _wired++; }
                else miss.Add("RecruitmentVM.OnDone");
            }
            catch (Exception e) { Log.Error("PlayerRecruit.OnDone", e); miss.Add("RecruitmentVM.OnDone (wyjatek)"); }
            foreach (var name in new[] { "BuyMercenaries", "buy_mercenaries_on_consequence" })
            {
                try
                {
                    var m = AccessTools.Method(typeof(RecruitmentCampaignBehavior), name, Type.EmptyTypes);
                    if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(PlayerRecruit), nameof(MercPre)) { priority = Priority.First }, finalizer: new HarmonyMethod(typeof(PlayerRecruit), nameof(WinFin))); _wired++; }
                    else miss.Add(name);
                }
                catch (Exception e) { Log.Error("PlayerRecruit." + name, e); miss.Add(name + " (wyjatek)"); }
            }
            Log.Info("Rekrut gracza (E8/T): latki przy starcie gry - wpiete " + _wired + "/" + Hooks + "; BRAK: " + (miss.Count == 0 ? "-" : string.Join(", ", miss.ToArray()))
                     + " | komplet DTE gracza (AddStartingEquipmentToPlayerArmory), okno werbunku (RecruitmentVM.OnDone), najem w rozmowie (BuyMercenaries) i w menu zaulka"
                     + " (buy_mercenaries_on_consequence); bez latki DTE sciezka nieczynna (DTE jak dotad).");
        }
    }
}
