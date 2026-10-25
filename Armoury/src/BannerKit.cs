using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P3 "choragwie" (projekt choragwie.json A-B; klucze StandardBearerKitSafe, FieldStandards, BannerCompaniesEnabled). Jeff 25.10: "niech niosa
    /// sztandary ... choragwie po np. 50 ludzi i kazdy ma dowodce i sztandar".
    ///
    /// A. CHORAZY BEZ DZIURY W ZBROJOWNI (klucz StandardBearerKitSafe). Dowod [K]:
    ///  - gra przy podmianie sprzetu chorazego (BannerBearerLogic.CreateBannerEquipmentForAgent, rozstawienie) i przy spawnie posilkow ze sztandarem
    ///    (Mission.SpawnAgent, mbfull Mission.cs:4239-4256 - na KLONIE ekwipunku) czysci sloty 1-3 i w slot 0 wklada bron zastepcza kultury
    ///    (SandboxBattleBannerBearersModel.GetBannerBearerReplacementWeapon);
    ///  - DTE przy spawnie zdejmuje ze zbrojowni sprzet z przydzialu (SpawnAgentPatch Postfix: Assignment.CreateEquipmentForArmoryConsumption - bez slotow
    ///    tymczasowych), a po bitwie / przy zgonie oddaje sprzet z agent.SpawnEquipment (DynamicTroopMissionLogic.ProcessAgentEquipmentRespectingTemporarySlots,
    ///    pomija IsBannerItem i sloty tymczasowe Assignment.IsTemporarySlot);
    ///  - wynik: bron ze slotow 0-3 przydzialu ginie (zdjeta, nie oddana), a bron zastepcza powstaje z niczego (oddana, nie zdjeta).
    /// Naprawa: w chwili, gdy czlowiek staje sie chorazym (zdarzenie gry BannerBearerLogic.OnBannerBearerAgentUpdated(agent, true) - BannerCompaniesMission,
    /// albo rejestracja DTE posilkow ze sztandarem - GarrisonKit.RegisterPostfix -> OnRegistered), liczymy roznice: sztuki slotow 0-3 przydzialu
    /// (nietymczasowe, czyli zdjete przez DTE), ktorych DTE nie odda z SpawnEquipment, ida na liste "odlozone do taborow"; bron zastepcza w slocie 0, gdy nie jest
    /// sztuka przydzialu, dostaje MarkSlotAsTemporary (DTE jej nie odda ani nie zlupi). Postfiks DTE ProcessAgentEquipmentRespectingTemporarySlots doklada
    /// liste odlozonych do TEGO SAMEGO delegata - raz na czlowieka (flaga Paid): zwrot, gdy przezyje, odzysk albo lup wedlug DTE, gdy padnie. Zdarzenie
    /// (agent, false) - gra cofa podmiane: lista znika, znak slotu 0 wraca do poprzedniego (sztuka tymczasowa m117/m119/sklad7, inaczej "odznaczenie" sztandarem).
    /// Sloty tymczasowe przydzialu (ubranie m117, kij m119, sloty lorda z niczego sklad7) NIE sa "zdjete" - nie trafiaja na liste.
    /// Linia "Choragwie (CH) ekonomia" (postfiks DTE OnEndMission - po FinalizeMission): tozsamosc odlozone == oddane i potwierdzenie znakow tymczasowych.
    ///
    /// B. WIECEJ CHORAZYCH (klucz BannerCompaniesEnabled): postfiks SandboxBattleBannerBearersModel.GetDesiredNumberOfBannerBearersForFormation - gdy gra dala 1,
    /// w bitwie polowej z BannerCompaniesMission zwracamy k = liczba kompanii (BannerCompanies.CompaniesFor, najwyzej 6 - tyle miejsc na sztandar ma
    /// DefaultFormationArrangementModel). Sztandar polowy (FieldStandards) zaklada BannerCompaniesMission w OnDeploymentFinished. B TYLKO gdy A jest czynne
    /// (MoreAllowed) - inaczej dziura rosnie do 6x na formacje. Rozstaw sztandarow nad frontem kompanii (GetBannerBearerPositions) - v1.1, nie zakladany.
    ///
    /// Latki w kampanii (NightHooks.InCampaign, raz na proces), AccessTools.DeclaredMethod na klasie deklarujacej, wejscie sprawdzone Harmony.GetPatchInfo
    /// (pulapka 15.10: finalizer BKROTPatch polyka wyjatki Patch). Wszystko na glownym watku. Wyjatki liczone (RoutHold.Stumble, miejsce "choragiew") - bez
    /// globalnego wylacznika. Stan misji kluczowany referencja misji, czyszczony w postfiksie DTE OnEndMission i przy zmianie misji.
    /// </summary>
    internal static class BannerKit
    {
        private const string P = "Choragwie (CH) ekonomia: ";

        // ---- latki (raz na proces)
        private static bool _tried;
        internal static bool CountHooked;     // GetDesiredNumberOfBannerBearersForFormation
        internal static bool ReturnHooked;    // DTE ProcessAgentEquipmentRespectingTemporarySlots
        internal static bool EndHooked;       // DTE OnEndMission (linia ekonomii)
        internal static bool DtePresent;
        internal static bool RegisterHooked;  // GarrisonKit.RegisterPostfix na DTE RegisterSpawnedAgentAssignment (zrodlo przydzialu agenta)
        private static FieldInfo _fEq;        // DTE Assignment.Equipment (pole publiczne readonly)
        private static string _note = "nie zakladane";

        /// <summary>A jest czynne: bez DTE nie ma czego uszczelniac; z DTE - zwrot wpiety, przydzial agenta znany, oznaczanie tymczasowych dostepne.</summary>
        internal static bool KitReady
        {
            get { return !DtePresent || (ReturnHooked && RegisterHooked && _fEq != null && GarrisonKit._markTemp != null && GarrisonKit._isTemp != null); }
        }

        /// <summary>B wolno (wielu chorazych, sztandar polowy): z DTE tylko przy kluczu StandardBearerKitSafe i czynnym A.</summary>
        internal static bool MoreAllowed(Settings c)
        {
            if (c == null) return false;
            if (!DtePresent) return true;
            return c.StandardBearerKitSafe && KitReady;
        }

        internal static string Note { get { return _note; } }

        internal static string ApplyInCampaign(Harmony h)
        {
            if (_tried) return _note;
            _tried = true;
            if (h == null) { _note = "BRAK instancji Harmony"; return _note; }
            string count = "NIE", ret = "NIE", end = "NIE", reg = "NIE";
            // B: liczba chorazych na formacje
            try
            {
                var tModel = AccessTools.TypeByName("SandBox.SandboxBattleBannerBearersModel");
                var m = tModel != null ? AccessTools.DeclaredMethod(tModel, "GetDesiredNumberOfBannerBearersForFormation", new[] { typeof(Formation) }) : null;
                if (m == null) count = "NIE (brak SandboxBattleBannerBearersModel.GetDesiredNumberOfBannerBearersForFormation)";
                else
                {
                    h.Patch(m, postfix: new HarmonyMethod(typeof(BannerKit), nameof(DesiredPostfix)) { priority = Priority.Last });
                    CountHooked = Has(m, h.Id, nameof(DesiredPostfix));
                    count = CountHooked ? "TAK" : "NIE (latka nie weszla - slad w BKROTLog.txt)";
                }
            }
            catch (Exception e) { CountHooked = false; count = "NIE (" + e.GetType().Name + ")"; RoutHold.Stumble(RoutHold.SiteBanner, "BannerKit.ApplyInCampaign(liczba)", e); }
            // A: zwrot sprzetu odlozonego przez chorazego (DTE)
            try
            {
                var tLogic = QuartermasterLaw.FindType("DynamicTroopEquipmentReupload.DynamicTroopMissionLogic");
                var tAssign = QuartermasterLaw.FindType("DynamicTroopEquipmentReupload.Assignment");
                DtePresent = tLogic != null;
                if (tLogic == null) { ret = "bez DTE (nic do uszczelnienia)"; end = "-"; reg = "-"; }
                else
                {
                    _fEq = tAssign != null ? AccessTools.Field(tAssign, "Equipment") : null;
                    var mProc = AccessTools.DeclaredMethod(tLogic, "ProcessAgentEquipmentRespectingTemporarySlots", new[] { typeof(Agent), typeof(Action<ItemObject>) });
                    if (mProc == null) ret = "NIE (brak DTE ProcessAgentEquipmentRespectingTemporarySlots)";
                    else
                    {
                        h.Patch(mProc, postfix: new HarmonyMethod(typeof(BannerKit), nameof(ReturnPostfix)));
                        ReturnHooked = Has(mProc, h.Id, nameof(ReturnPostfix));
                        ret = ReturnHooked ? "TAK" : "NIE (latka nie weszla - slad w BKROTLog.txt)";
                    }
                    var mEnd = AccessTools.DeclaredMethod(tLogic, "OnEndMission");
                    if (mEnd != null)
                    {
                        h.Patch(mEnd, postfix: new HarmonyMethod(typeof(BannerKit), nameof(EndPostfix)));
                        EndHooked = Has(mEnd, h.Id, nameof(EndPostfix));
                    }
                    end = EndHooked ? "TAK" : "NIE";
                    // przydzial agenta poznajemy z istniejacego postfiksu GarrisonKit.RegisterPostfix (DTE RegisterSpawnedAgentAssignment)
                    var mReg = AccessTools.DeclaredMethod(tLogic, "RegisterSpawnedAgentAssignment");
                    var info = mReg != null ? Harmony.GetPatchInfo(mReg) : null;
                    if (info != null)
                        foreach (var p in info.Postfixes)
                            if (p != null && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(GarrisonKit) && p.PatchMethod.Name == "RegisterPostfix") { RegisterHooked = true; break; }
                    reg = RegisterHooked ? "TAK" : "NIE";
                }
            }
            catch (Exception e) { ReturnHooked = false; ret = "NIE (" + e.GetType().Name + ")"; RoutHold.Stumble(RoutHold.SiteBanner, "BannerKit.ApplyInCampaign(DTE)", e); }
            bool temp = GarrisonKit._markTemp != null && GarrisonKit._isTemp != null && _fEq != null;
            _note = "liczba chorazych " + count + ", zwrot DTE " + ret + " (przydzial agenta z GarrisonKit " + reg + ", sloty tymczasowe " + (temp ? "TAK" : "NIE")
                    + ", linia ekonomii " + end + "), A czynne " + (KitReady ? "TAK" : "NIE - wielu chorazych i sztandary polowe WYLACZONE")
                    + ", rozstaw nad kompaniami v1.1 (nie zakladany)";
            return _note;
        }

        private static bool Has(MethodBase m, string owner, string name)
        {
            var info = Harmony.GetPatchInfo(m);
            if (info == null) return false;
            foreach (var p in info.Postfixes)
                if (p != null && p.owner == owner && p.PatchMethod != null && p.PatchMethod.Name == name) return true;
            return false;
        }

        // ------------------------------------------------------------ stan misji (glowny watek)

        private sealed class Kit
        {
            public object Asg;
            public List<ItemObject> Out = new List<ItemObject>(4);   // odlozone do taborow (zdjete przez DTE, nieoddawane z SpawnEquipment)
            public ItemObject Marked;      // bron zastepcza oznaczona przez nas jako tymczasowa (slot 0)
            public ItemObject PrevTemp0;   // sztuka tymczasowa slotu 0 przydzialu sprzed naszego znaku (m119 kij itd.)
            public ItemObject Banner;      // sztandar chorazego - "odznaczenie" slotu 0 przy cofnieciu (sztandar nigdy nie lezy w slocie 0)
            public bool MarkOk;            // znak przyjety (IsTemporarySlot zaraz po MarkSlotAsTemporary)
            public bool Paid;              // DTE juz rozliczyl tego czlowieka (lista oddana)
            public bool Reinforcement;
        }

        private static WeakReference _m;
        private static Mission _field;                                             // biezaca bitwa polowa z BannerCompaniesMission (postfiks liczby chorazych)
        private static readonly Dictionary<Agent, object> _asg = new Dictionary<Agent, object>();   // agent -> przydzial DTE (z rejestracji)
        private static readonly Dictionary<Agent, Kit> _kit = new Dictionary<Agent, Kit>();
        private static readonly HashSet<Formation> _raised = new HashSet<Formation>();             // formacje, ktorym postfiks podniosl liczbe chorazych
        private static readonly List<ItemObject> _pool = new List<ItemObject>(4);
        // liczniki misji
        private static int _bearers, _bearersDeploy, _bearersReinf, _reverted, _noAsg, _out, _paid, _marked, _markedOk, _markedConfirmed,
                           _sameItem, _noReplacement, _foreign, _stumbles;
        private static string _kind = "?";

        private static void Touch(Mission m)
        {
            if (m == null) return;
            if (_m != null && ReferenceEquals(_m.Target, m)) return;
            if (_m != null && _m.Target != null && Any()) { try { Write("misja bez konca DTE"); } catch { } }
            Clear();
            _m = new WeakReference(m);
            try { _kind = MissionTeams.Kind(m); } catch { _kind = "?"; }
        }

        private static bool Any() { return _bearers + _noAsg + _stumbles > 0; }

        private static void Clear()
        {
            _asg.Clear(); _kit.Clear(); _raised.Clear(); _pool.Clear();
            _bearers = _bearersDeploy = _bearersReinf = _reverted = _noAsg = _out = _paid = _marked = _markedOk = _markedConfirmed = 0;
            _sameItem = _noReplacement = _foreign = _stumbles = 0;
            _kind = "?";
            _m = null;
        }

        /// <summary>BannerCompaniesMission.AfterStart: misja biezaca; field = bitwa polowa z logika sztandarow i spawnem bitwy (postfiks liczby chorazych czynny).</summary>
        internal static void BeginMission(Mission m, bool field)
        {
            Touch(m);
            _field = field ? m : null;
        }

        /// <summary>BannerCompaniesMission.OnEndMission / OnRemoveBehavior: postfiks liczby chorazych juz nie dziala dla tej misji (stan A zostaje do konca DTE).</summary>
        internal static void EndMission(Mission m)
        {
            if (ReferenceEquals(_field, m)) _field = null;
        }

        internal static int RaisedFormations { get { return _raised.Count; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            RoutHold.Stumble(RoutHold.SiteBanner, where, e);
        }

        private static bool IsTemp(object asg, int slot, ItemObject it)
        {
            if (asg == null || it == null || GarrisonKit._isTemp == null) return false;
            return (bool)GarrisonKit._isTemp.Invoke(asg, new object[] { (EquipmentIndex)slot, it });
        }

        private static void MarkTemp(object asg, int slot, ItemObject it)
        {
            GarrisonKit._markTemp.Invoke(asg, new object[] { (EquipmentIndex)slot, it });
        }

        // ------------------------------------------------------------ wejscia

        /// <summary>GarrisonKit.RegisterPostfix (postfiks DTE RegisterSpawnedAgentAssignment, glowny watek, raz na spawn): przydzial agenta do slownika misji;
        /// posilki ze sztandarem (Mission.SpawnAgent juz podmienil sprzet na klonie) - od razu rozliczenie A.</summary>
        internal static void OnRegistered(Agent a, object asg)
        {
            var c = Settings.Current;
            if (a == null || asg == null || c == null || !c.StandardBearerKitSafe || !KitReady || !DtePresent) return;
            try
            {
                Touch(a.Mission ?? Mission.Current);
                _asg[a] = asg;
                if (a.Banner != null) Make(a, true);
            }
            catch (Exception e) { Stumble("BannerKit.OnRegistered", e); }
        }

        /// <summary>BannerCompaniesMission (zdarzenie gry BannerBearerLogic.OnBannerBearerAgentUpdated): true - czlowiek dostal sztandar (podmiana sprzetu),
        /// false - gra cofnela podmiane (przywrocony sprzet z poczatku rozstawienia).</summary>
        internal static void OnBearerUpdated(Agent a, bool bearer)
        {
            var c = Settings.Current;
            if (a == null || c == null || !c.StandardBearerKitSafe || !KitReady || !DtePresent) return;
            try
            {
                Touch(a.Mission ?? Mission.Current);
                if (bearer) Make(a, false);
                else Revert(a);
            }
            catch (Exception e) { Stumble("BannerKit.OnBearerUpdated", e); }
        }

        /// <summary>Rozliczenie A dla chorazego: lista odlozonych od nowa (gra moze podmienic sprzet drugi raz - inny sztandar formacji) i znak slotu 0.</summary>
        private static void Make(Agent a, bool reinforcement)
        {
            object asg;
            if (!_asg.TryGetValue(a, out asg) || asg == null) { _noAsg++; return; }   // czlowiek bez przydzialu DTE (bandyci, karawany) - DTE go nie rozlicza z przydzialu
            var eq = _fEq.GetValue(asg) as Equipment;
            var se = a.SpawnEquipment;
            if (eq == null || se == null) { _noAsg++; return; }
            Kit k;
            bool fresh = !_kit.TryGetValue(a, out k);
            if (fresh)
            {
                k = new Kit { Asg = asg, Reinforcement = reinforcement };
                var a0 = eq[EquipmentIndex.WeaponItemBeginSlot].Item;
                if (a0 != null && IsTemp(asg, 0, a0)) k.PrevTemp0 = a0;
                _kit[a] = k;
                _bearers++;
                if (reinforcement) _bearersReinf++; else _bearersDeploy++;
            }
            else
            {
                if (k.Paid) return;              // DTE juz rozliczyl - nie ruszamy
                _out -= k.Out.Count;             // przeliczenie od nowa
                k.Out.Clear();
            }
            if (a.Banner != null) k.Banner = a.Banner;
            // zdjete przez DTE przy spawnie: sloty 0-3 przydzialu bez slotow tymczasowych (CreateEquipmentForArmoryConsumption). Znak slotu 0 sprzed nas
            // (PrevTemp0) liczy sie jako tymczasowy, nasz wlasny znak (Marked) - nie (sztuka slotu 0 przydzialu o tym samym id byla zdjeta)
            _pool.Clear();
            for (int i = 0; i < 4; i++)
            {
                var it = eq[(EquipmentIndex)i].Item;
                if (it == null) continue;
                bool temp = i == 0 ? (k.PrevTemp0 != null && k.PrevTemp0.StringId == it.StringId) : IsTemp(asg, i, it);
                if (!temp) _pool.Add(it);
            }
            // co DTE odda z SpawnEquipment: sztuki slotow 0-3, ktore nie sa tymczasowe - kazda zdejmuje jedna sztuke z puli; reszta puli = odlozone
            ItemObject fromNothing = null;
            for (int i = 0; i < 4; i++)
            {
                var cur = se[(EquipmentIndex)i].Item;
                if (cur == null || cur.IsBannerItem) continue;
                bool ours = i == 0 && k.Marked != null && k.Marked.StringId == cur.StringId;
                if (!ours && IsTemp(asg, i, cur)) continue;                  // sztuka tymczasowa innych (m117 / m119 / sklad7) - DTE jej nie odda
                int j = IndexOf(_pool, cur);
                if (j >= 0) { _pool.RemoveAt(j); if (ours) Unmark(k, asg); continue; }   // sztuka z przydzialu - DTE ja odda
                if (i == 0) fromNothing = cur;                               // bron zastepcza gry - z niczego
                else _foreign++;                                             // inna sztuka spoza przydzialu w slotach 1-3 (inny mod) - nie nasza sprawa
            }
            if (fromNothing != null)
            {
                if (k.Marked == null || k.Marked.StringId != fromNothing.StringId)
                {
                    bool first = k.Marked == null;
                    MarkTemp(asg, 0, fromNothing);
                    k.Marked = fromNothing;
                    if (first)
                    {
                        _marked++;
                        k.MarkOk = IsTemp(asg, 0, fromNothing);
                        if (k.MarkOk) _markedOk++;
                    }
                }
            }
            else if (fresh)
            {
                var cur0 = se[EquipmentIndex.WeaponItemBeginSlot].Item;
                if (cur0 == null) _noReplacement++;                          // kultura bez broni zastepczej - chorazy z samym sztandarem
                else _sameItem++;                                            // bron zastepcza = sztuka przydzialu (DTE ja odda)
            }
            for (int i = 0; i < _pool.Count; i++) k.Out.Add(_pool[i]);
            _out += k.Out.Count;
            _pool.Clear();
        }

        private static ItemObject _anyBanner;

        /// <summary>Dowolny sztandar do "odznaczenia" slotu 0 (sztandar nigdy nie lezy w slocie 0, wiec znak tymczasowy przestaje pasowac do czegokolwiek).</summary>
        private static ItemObject AnyBanner()
        {
            if (_anyBanner == null)
            {
                try { _anyBanner = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>("campaign_banner_small"); } catch { _anyBanner = null; }
            }
            return _anyBanner;
        }

        private static int IndexOf(List<ItemObject> l, ItemObject it)
        {
            for (int i = 0; i < l.Count; i++) if (l[i] == it || (l[i] != null && it != null && l[i].StringId == it.StringId)) return i;
            return -1;
        }

        /// <summary>Nasz znak slotu 0 przestaje obowiazywac: wraca znak sprzed nas (sztuka tymczasowa przydzialu) albo "odznaczenie" sztandarem.</summary>
        private static void Unmark(Kit k, object asg)
        {
            if (k.Marked == null) return;
            if (k.PrevTemp0 != null) MarkTemp(asg, 0, k.PrevTemp0);
            else
            {
                var u = k.Banner ?? AnyBanner();
                if (u != null) MarkTemp(asg, 0, u);
            }
            k.Marked = null;
            _marked--;
            if (k.MarkOk) { _markedOk--; k.MarkOk = false; }
        }

        private static void Revert(Agent a)
        {
            Kit k;
            if (!_kit.TryGetValue(a, out k) || k.Paid) return;
            _out -= k.Out.Count;
            k.Out.Clear();
            // gra przywrocila sprzet z poczatku rozstawienia - nasz znak slotu 0 (bron zastepcza) nie moze zostac na sztuce przydzialu o tym samym id
            Unmark(k, k.Asg);
            _kit.Remove(a);
            _bearers--;
            if (k.Reinforcement) _bearersReinf--; else _bearersDeploy--;
            _reverted++;
        }

        // ------------------------------------------------------------ latki

        /// <summary>Postfiks SandboxBattleBannerBearersModel.GetDesiredNumberOfBannerBearersForFormation (glowny watek: rozstawienie, nasze SetFormationBanner,
        /// GetMissingBannerCount przy spawnie posilkow). Zmienia tylko 1 na k (zero zostaje zerem) - w bitwie polowej z BannerCompaniesMission i przy czynnym A.</summary>
        public static void DesiredPostfix(Formation __0, ref int __result)
        {
            if (__result != 1 || __0 == null) return;
            try
            {
                var m = _field;
                var c = Settings.Current;
                if (m == null || c == null || !c.BannerCompaniesEnabled || !MoreAllowed(c)) return;
                var t = __0.Team;
                if (t == null || !ReferenceEquals(t.Mission, m) || (int)__0.FormationIndex >= 8) return;
                // recenzja: ta sama populacja co podzial na kompanie (BannerCompaniesMission.Split - czynni ludzie bez bohaterow i umarlych); CountOfUnits liczy
                // takze bohaterow i umarlych (74 szeregowych + 3 bohaterow = 2 chorazych przy 1 kompanii). Najpierw tani prog: szeregowych nie wiecej niz
                // CountOfUnits, a CompaniesFor nie maleje - gdy wszyscy daja 1 kompanie, szeregowi tez (bez przegladu formacji; wolane tez przy kazdym spawnie posilkow)
                if (BannerCompanies.CompaniesFor(__0.CountOfUnits, c.BannerCompanySize) <= 1) return;
                int k = BannerCompanies.CompaniesFor(BannerCompanies.SoldiersOf(__0), c.BannerCompanySize);
                if (k > 1) { __result = k; _raised.Add(__0); }
            }
            catch (Exception e) { Stumble("BannerKit.DesiredPostfix", e); }
        }

        /// <summary>Postfiks DTE DynamicTroopMissionLogic.ProcessAgentEquipmentRespectingTemporarySlots(Agent, Action&lt;ItemObject&gt;) - zwrot (zywi, FinalizeMission),
        /// odzysk / lup (polegli, OnAgentRemoved), zbiegli (Routed / Deleted). Lista odlozonych chorazego idzie tym samym delegatem - raz na czlowieka.</summary>
        public static void ReturnPostfix(Agent __0, Action<ItemObject> __1)
        {
            if (__0 == null || __1 == null || _kit.Count == 0) return;
            Kit k;
            if (!_kit.TryGetValue(__0, out k) || k.Paid) return;
            k.Paid = true;
            try
            {
                if (k.Marked != null && IsTemp(k.Asg, 0, k.Marked)) _markedConfirmed++;
                for (int i = 0; i < k.Out.Count; i++)
                {
                    __1(k.Out[i]);
                    _paid++;
                }
            }
            catch (Exception e) { Stumble("BannerKit.ReturnPostfix", e); }
        }

        /// <summary>Postfiks DTE DynamicTroopMissionLogic.OnEndMission (po FinalizeMission - zwroty policzone): linia ekonomii raz na misje, stan od zera.</summary>
        public static void EndPostfix()
        {
            try { if (_m != null && Any()) Write("koniec misji"); }
            catch (Exception e) { Stumble("BannerKit.EndPostfix", e); }
            Clear();
        }

        private static void Write(string why)
        {
            int alive = 0, aliveOut = 0, deadOut = 0;
            foreach (var kv in _kit)
            {
                if (kv.Value.Paid) continue;
                bool act = false;
                try { act = kv.Key != null && kv.Key.IsActive(); } catch { }
                if (act) { alive++; aliveOut += kv.Value.Out.Count; } else deadOut += kv.Value.Out.Count;
            }
            var sb = new StringBuilder(600);
            sb.Append(P).Append("misja ").Append(_kind).Append(" (").Append(why).Append(") | chorazych ").Append(_bearers)
              .Append(" (rozstawienie ").Append(_bearersDeploy).Append(", posilki ").Append(_bearersReinf).Append("; cofnietych ").Append(_reverted)
              .Append(", bez przydzialu DTE ").Append(_noAsg).Append(')')
              .Append(" | odlozone do taborow ").Append(_out).Append(" szt., oddane przez DTE ").Append(_paid).Append(" szt. (")
              .Append(_out == _paid ? "zgodne" : "NIEZGODNE").Append("; nierozliczeni przez DTE: zywi ").Append(alive).Append(" z ").Append(aliveOut)
              .Append(" szt., inni ").Append(deadOut).Append(" szt.)")
              .Append(" | zastepcze tymczasowe ").Append(_marked).Append(" z ").Append(_bearers).Append(" (znak przyjety ").Append(_markedOk)
              .Append(", potwierdzone przy rozliczeniu DTE ").Append(_markedConfirmed).Append("; zastepcza = sztuka przydzialu ").Append(_sameItem)
              .Append(", bez broni zastepczej ").Append(_noReplacement).Append(", obce sztuki w slotach 1-3 ").Append(_foreign).Append(')')
              .Append(" | liczba chorazych podniesiona w formacjach ").Append(_raised.Count)
              .Append(" | potkniecia ").Append(_stumbles).Append('.');
            Log.Info(sb.ToString());
        }
    }
}
