using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// Prawo pola bitwy. Naprawia potok lupow miedzy Dynamic Troop Equipment
    /// a Spoils of War (RealisticLoot):
    ///
    /// 1. W bitwach STOCZONYCH OSOBISCIE lupem rzadzi DTE - zabici zostawiaja
    ///    faktyczny ekwipunek (czesc trafiona ciosem smiertelnym przepada),
    ///    wojsko czerpie z Armoury, a dzialka gracza wg wkladu w bitwe trafia
    ///    na ekran lupow. Spoils of War dotad WYCINAL ten ekran (prefix na
    ///    PlayerEncounter.DoLootInventory) i podstawial wlasna loterie -
    ///    nasz postfix na jego prefixie kaze mu ustapic.
    /// 2. Przedmioty z ekranu lupow wygranej, stoczonej bitwy dostaja stan
    ///    (modyfikatory zuzycia Spoils of War) - uszkodzone, ale z wartoscia.
    /// 3. W bitwach AUTOMATYCZNYCH Spoils zostaje, ale bez zaszytych mnoznikow
    ///    tieru (T1 x0.18 itd.) - reszte bramek zdjeto configiem.
    ///
    /// Wszystko przez refleksje - dziala tez, gdy ktoregos z tych modow nie ma.
    /// </summary>
    internal static class BattlefieldLaw
    {
        private static PropertyInfo _vanillaHandledSetter;
        private static MethodInfo _applyDamage;
        private static bool _rlPresent;
        // PRAWO ZACHOWANIA LUPU aktywne: vanilla loteria szablonowa za
        // poleglych/rannych wycieta w bitwach osobistych (DTE + jency
        // placa fizycznie) - czytane przez ArmouryBehavior przy jencach
        internal static bool CasualtyLootCut;

        // dzialka gracza wyjeta z magazynu DTE, czekajaca na ekran lupow
        private static readonly ItemRoster ShareQueue = new ItemRoster();
        private static FieldInfo _dteRecords;      // DynamicTroopMissionLogic._partyBattleRecords
        private static FieldInfo _dteLootedItems;  // PartyBattleRecord.LootedItems
        private static FieldInfo _dteArmory;       // ArmyArmory.Armory (ItemRoster)

        // wraki: czesci rozbite ciosem smiertelnym (DTE je wyrzucal - my je zbieramy)
        private static readonly List<ItemObject> Wrecks = new List<ItemObject>();
        private static ItemObject _lastPick;
        private static MethodInfo _rlEnsureCache;      // EquipmentDamageModel.EnsureCacheInitialized
        private static FieldInfo _rlHeavy;             // EquipmentDamageModel._lootedHeavy
        private static FieldInfo _rlHeavyMax;          // EquipmentDamageModel._lootedHeavyMax
        private static MethodInfo _rlRegisterLucky;    // RealisticLootModel.RegisterLuckyElement
        // m109-lup: RegisterLuckyElement niezaleznie od WreckSalvageEnabled (konie wracajace na liste trofeow - ReturnMounts) i stan wpiecia do linii
        // startu LootTally ("Lup bitwy (m109): start")
        private static MethodInfo _lReg;
        internal static bool MountReturnWired, MountFinalWired, SpareHarnessWired;
        // m114-lup: getter Spoils EnablePrioritySystem odpowiada false (NoSoldierSplit) - drugi podzial listy trofeow wylaczony
        internal static bool SoldierSplitWired;
        internal static bool LuckyFound { get { return _lReg != null; } }
        /// <summary>m110-stan: latki lupu cialo po ciele wpiete (DTE OnAgentRemoved - zabojca i ofiara, PartyBattleRecord.AddLootedItem - sztuka) - linia startu stanow.</summary>
        internal static int LootWiredCount { get { return _lootWired; } }
        // m110-stan (LootStateFromWounds): sprawne sztuki lupu z obrazen wysypane do sakw (FlushShareToBaggage, FlushMountsToBaggage) - WearTheLoot nie daje im
        // loterii stanu (kryjowka: sakwy przed WearTheLoot); czyszczone przy odczycie i na poczatku rozliczenia nastepnej bitwy
        private static readonly Dictionary<ItemObject, int> _soundFlushed = new Dictionary<ItemObject, int>();

        /// <summary>m110-stan: sprawne sztuki B wysypane do sakw od ostatniego odczytu (przedmiot -> ile) - zwraca kopie i czysci; null - brak.</summary>
        internal static Dictionary<ItemObject, int> TakeSoundFlushed()
        {
            if (_soundFlushed.Count == 0) return null;
            var r = new Dictionary<ItemObject, int>(_soundFlushed);
            _soundFlushed.Clear();
            return r;
        }

        // m110-stan (LootStateFromWounds, LootWear): stan lupu z obrazen - czesci zbroi z cial wrogow powalonych przez druzyne gracza.
        // Pule czesci z lupu (LootTag): przedmiot -> dane czesci z cial powalonych przez Ciebie i towarzyszy / przez zolnierzy (pod lockiem _lootHero);
        // dane czesci rownolegle do Wrecks (cios - QueueWreck, pekniete - AddWreck; null = bez ksiegi). Sztuki B w kolejce lupu: z nowym stanem (_bQueued -
        // znacznik lucky Spoils, odjete przy zwrocie), ich oryginaly ze zbrojowni wojska (_bOrig - wracaja przy zwrocie), sprawne bez modyfikatora (_bSound -
        // sakwy bez loterii stanu). Zyja od AfterDtePartyItems do oddania kolejki (lista trofeow, sakwy, zwrot, ekran gry); bez zapisu, czyszczone w LReset.
        [ThreadStatic] private static Agent _lootVictim;   // ofiara w biegu DTE OnAgentRemoved (prefiks ustawia, finalizer czysci)
        private static readonly Dictionary<ItemObject, List<LootWear.PieceData>> _pdHero = new Dictionary<ItemObject, List<LootWear.PieceData>>();
        private static readonly Dictionary<ItemObject, List<LootWear.PieceData>> _pdTroop = new Dictionary<ItemObject, List<LootWear.PieceData>>();
        private static readonly List<LootWear.PieceData> WreckData = new List<LootWear.PieceData>();
        private static readonly ItemRoster _bQueued = new ItemRoster();
        private static readonly ItemRoster _bOrig = new ItemRoster();
        private static readonly Dictionary<ItemObject, int> _bSound = new Dictionary<ItemObject, int>();
        private static bool _bSoundHeld;   // AfterGenerateLoot zostawil _bSound dla przetrzymanych koni i uprzezy - finalizer Spoils czysci

        /// <summary>m110-stan: pule czesci z lupu zdjete (pod lockiem) - do zwrotu drogi "zostaje u wojska".</summary>
        private static List<LootWear.PieceData> TakePoolsLocked()
        {
            if (_pdHero.Count == 0 && _pdTroop.Count == 0) return null;
            var r = new List<LootWear.PieceData>();
            foreach (var l in _pdHero.Values) r.AddRange(l);
            foreach (var l in _pdTroop.Values) r.AddRange(l);
            _pdHero.Clear(); _pdTroop.Clear();
            return r;
        }

        /// <summary>m110-stan: reszta pul (sztuki zostaja w zbrojowni wojska bez stanu, jak dotad) - droga "zostaje u wojska".</summary>
        private static void DropPools()
        {
            List<LootWear.PieceData> l;
            lock (_lootHero) l = TakePoolsLocked();
            if (l != null) foreach (var pd in l) LootWear.Route(pd, LootWear.RZost);
        }

        /// <summary>m110-stan: kolejka oddana albo wyczyszczona - ewidencja sztuk B od nowa.</summary>
        private static void ClearB()
        {
            _bQueued.Clear(); _bOrig.Clear(); _bSound.Clear(); _bSoundHeld = false;
        }

        private static void ClearWrecks() { Wrecks.Clear(); WreckData.Clear(); }

        /// <summary>m110-stan: sztuka B do kolejki lupu (dzialka) ze stanem z obrazen; orig - element zdjety ze zbrojowni wojska (wraca przy zwrocie kolejki).</summary>
        private static void QueueB(ItemObject it, int st, ItemModifier soundMod, EquipmentElement? orig)
        {
            var mod = st <= GearState.Sound ? soundMod : GearState.LootMod(it, st);
            var ne = new EquipmentElement(it, mod);
            LootTally.Queued(it, 1, LootTally.Dzialka);   // m109-lup: pomiar (przed dopisaniem)
            ShareQueue.AddToCounts(ne, 1);
            _bQueued.AddToCounts(ne, 1);
            if (orig.HasValue) _bOrig.AddToCounts(orig.Value, 1);
            if (mod == null) { int v; _bSound.TryGetValue(it, out v); _bSound[it] = v + 1; }
        }

        /// <summary>m114-lup (LootWeaponsKeepCondition, Jeff 24.10 "ja wszystkich sam zabijam, to wszystko powinno byc moje"): bron, tarcza, amunicja -
        /// stan z ciala (modyfikator sztuki), bez loterii stanu Spoils i sakw; ciosy w cialo zuzywaja zbroje (ksiega m110), nie bron.</summary>
        internal static bool KeepsBodyState(ItemObject it)
        {
            return it != null && it.HasWeaponComponent && !it.HasArmorComponent && !it.IsMountable && Settings.Current.LootWeaponsKeepCondition;
        }

        /// <summary>m114-lup: bron z kolejki lupu idacej na liste trofeow (element ze stanem -> sztuk), bez sztuk B (te oznacza petla m110 - bez dubli).</summary>
        private static ItemRoster WeaponShare(ItemRoster queue)
        {
            var r = new ItemRoster();
            for (int i = 0; i < queue.Count; i++)
            {
                var el = queue.GetElementCopyAtIndex(i);
                if (el.Amount <= 0 || !KeepsBodyState(el.EquipmentElement.Item)) continue;
                int bi = _bQueued.FindIndexOfElement(el.EquipmentElement);
                int n = el.Amount - (bi >= 0 ? Math.Max(0, _bQueued.GetElementNumber(bi)) : 0);
                if (n > 0) r.AddToCounts(el.EquipmentElement, n);
            }
            return r;
        }

        /// <summary>m114-lup: znacznik lucky Spoils (RegisterLuckyElement - ApplyDamageToLoot zostawia sztuke 1:1, jak zbroje z ksiega m110) dla broni z kolejki,
        /// ktora zostala na liscie trofeow po przesianiu - tyle sztuk, ile lezy na liscie (bez znacznikow bez pokrycia). ok - oznaczone, need - do oznaczenia,
        /// mod - z nich z modyfikatorem (stan inny niz sprawny, zachowany).</summary>
        private static void RegisterWeaponsLucky(ItemRoster list, ItemRoster wq, ref int ok, ref int need, ref int mod)
        {
            for (int i = 0; i < wq.Count; i++)
            {
                var el = wq.GetElementCopyAtIndex(i);
                int li = list.FindIndexOfElement(el.EquipmentElement);
                int n = Math.Min(el.Amount, li >= 0 ? list.GetElementNumber(li) : 0);
                if (n <= 0) continue;
                need += n;
                if (el.EquipmentElement.ItemModifier != null) mod += n;
                if (_lReg == null) continue;
                for (int k = 0; k < n; k++)
                {
                    try { _lReg.Invoke(null, new object[] { el.EquipmentElement }); ok++; }
                    catch (Exception e) { MountStumble("AfterGenerateLoot(lucky m114)", e); break; }
                }
            }
        }

        /// <summary>m110-stan: sprawna sztuka B tego przedmiotu wysypywana do sakw - bez loterii stanu (true) i zapamietana dla WearTheLoot.</summary>
        private static bool TakeBSound(ItemObject it)
        {
            int v;
            if (it == null || !_bSound.TryGetValue(it, out v) || v <= 0) return false;
            _bSound[it] = v - 1;
            int f; _soundFlushed.TryGetValue(it, out f); _soundFlushed[it] = f + 1;
            return true;
        }

        private static LootWear.PieceData TakePiece(List<LootWear.PieceData> ph, List<LootWear.PieceData> pt)
        {
            if (ph != null && ph.Count > 0) { var x = ph[0]; ph.RemoveAt(0); return x; }
            if (pt != null && pt.Count > 0) { int i = TaleWorlds.Core.MBRandom.RandomInt(pt.Count); var x = pt[i]; pt.RemoveAt(i); return x; }
            return null;
        }

        /// <summary>m110-stan (i): podzial wrakow wedlug stanu z obrazen. Czesc z ksiega: zniszczona przepada; wrak (ponizej 10%) zostaje wrakiem (caly dla gracza,
        /// Jeff 12.10); lepsza czesc - jak reszta lupu z tego ciala: powalony przez Ciebie albo towarzysza - do kolejki w calosci, przez zolnierza - trzecia kapitana
        /// na przedmiot (WholeShare), reszta do zbrojowni wojska ze swoim stanem (recenzja R1/R4: dotad bez stanu - wartosc z niczego). Bez zbrojowni - jak
        /// dotad (wrak). Czesci bez ksiegi i juz podzielone - bez zmian. reg - zwykly lup partii gracza z rekordu DTE (cialo po ciele): czesci zolnierza dziela
        /// sie jednym zaokragleniem z tym lupem (recenzja R4); null - osobno jak dotad.</summary>
        private static void SplitWrecks(ItemRoster armory, int third, int pct, Dictionary<ItemObject, int> reg)
        {
            if (Wrecks.Count == 0) return;
            var keepW = new List<ItemObject>();
            var keepD = new List<LootWear.PieceData>();
            var troop = new Dictionary<ItemObject, List<LootWear.PieceData>>();
            for (int i = 0; i < Wrecks.Count; i++)
            {
                var it = Wrecks[i];
                var pd = i < WreckData.Count ? WreckData[i] : null;
                if (pd == null || pd.Split || it == null) { keepW.Add(it); keepD.Add(pd); continue; }
                pd.Split = true;
                if (pd.Destroyed) { LootWear.Route(pd, LootWear.RZn); continue; }
                if (pd.State == GearState.Wrecked || armory == null) { keepW.Add(it); keepD.Add(pd); LootWear.Route(pd, LootWear.RWrak); continue; }
                if (pd.Kind == 1) { QueueB(it, pd.State, null, null); LootWear.Route(pd, LootWear.RTy); continue; }
                List<LootWear.PieceData> l;
                if (!troop.TryGetValue(it, out l)) { l = new List<LootWear.PieceData>(); troop[it] = l; }
                l.Add(pd);
            }
            foreach (var kv in troop)
            {
                var l = kv.Value;
                for (int i = l.Count - 1; i > 0; i--) { int j = TaleWorlds.Core.MBRandom.RandomInt(i + 1); var x = l[i]; l[i] = l[j]; l[j] = x; }
                int sh, rc;
                if (reg != null && reg.TryGetValue(kv.Key, out rc))
                {
                    // recenzja R4: jedno zaokraglenie na przedmiot - dzialka z (zwykly lup + te czesci) minus dzialka samego zwyklego lupu (ta sama regula
                    // WholeShare, ktora potem liczy zwykla dzialke ponizej); pojedyncza czesc obok zwyklego lupu nie ginie juz w zaokragleniu do zera
                    int hk, tk;
                    lock (_lootHero) { _lootHero.TryGetValue(kv.Key, out hk); _lootTroop.TryGetValue(kv.Key, out tk); }
                    int h0 = hk, t0 = tk, h1 = hk, t1 = tk + l.Count;
                    sh = WholeShare(rc + l.Count, ref h1, ref t1, third, pct) - WholeShare(rc, ref h0, ref t0, third, pct);
                    sh = Math.Max(0, Math.Min(l.Count, sh));
                }
                else
                {
                    int h = 0, tr = l.Count;
                    sh = WholeShare(l.Count, ref h, ref tr, third, pct);
                }
                for (int i = 0; i < l.Count; i++)
                {
                    if (i < sh) { QueueB(kv.Key, l[i].State, null, null); LootWear.Route(l[i], LootWear.RDz); }
                    else
                    {
                        // recenzja R1 (kod) / R4: do zbrojowni wojska ze swoim stanem (zbrojownia trzyma modyfikatory - WearTheTroops, ShareWithState)
                        var wm = l[i].State > GearState.Sound ? GearState.ModFor(kv.Key, l[i].State) : null;
                        armory.AddToCounts(new EquipmentElement(kv.Key, wm), 1);
                        LootWear.Route(l[i], LootWear.RZbr);
                    }
                }
            }
            Wrecks.Clear(); Wrecks.AddRange(keepW);
            WreckData.Clear(); WreckData.AddRange(keepD);
        }

        /// <summary>m110-stan (ii): dzialka jednego przedmiotu ze stanem z obrazen - najpierw sztuki sprawne ze zbrojowni, kazda dostaje dane czesci (najpierw
        /// pula "Ty", potem losowo z puli "zolnierz"); zniszczona - zdjeta ze zbrojowni, nie trafia do kolejki; inna - do kolejki ze stanem gorszym z (stan
        /// z obrazen, stan zdjetej sztuki), sprawna zachowuje swoj modyfikator; sztuka bez danych - jak dotad. Zwraca liczbe sztuk w kolejce, left - niezdjete.</summary>
        private static int ShareWithState(ItemRoster armory, ItemObject item, int share, List<LootWear.PieceData> ph, List<LootWear.PieceData> pt, out int left)
        {
            left = share;
            int moved = 0;
            for (int pass = 0; pass < 2 && left > 0; pass++)
                for (int i = armory.Count - 1; i >= 0 && left > 0; i--)
                {
                    var el = armory.GetElementCopyAtIndex(i);
                    if (el.EquipmentElement.Item != item || el.Amount <= 0) continue;
                    int sEl = GearState.Of(el.EquipmentElement.ItemModifier);
                    bool sound = sEl == GearState.Sound;
                    if ((pass == 0) != sound) continue;
                    int t = Math.Min(left, el.Amount);
                    for (int n = 0; n < t; n++)
                    {
                        var pd = TakePiece(ph, pt);
                        armory.AddToCounts(el.EquipmentElement, -1);
                        left--;
                        if (pd == null)
                        {
                            LootTally.Queued(item, 1, LootTally.Dzialka);   // m109-lup: pomiar (przed dopisaniem)
                            ShareQueue.AddToCounts(el.EquipmentElement, 1);
                            moved++;
                            LootWear.NoBook(false);
                            continue;
                        }
                        if (pd.Destroyed) { LootWear.Route(pd, LootWear.RZn); continue; }
                        QueueB(item, Math.Max(pd.State, Math.Max(GearState.Sound, sEl)), el.EquipmentElement.ItemModifier, el.EquipmentElement);
                        moved++;
                        LootWear.Route(pd, pd.Kind == 1 ? LootWear.RTy : LootWear.RDz);
                    }
                }
            return moved;
        }
        /// <summary>m109-lup: kolejka lupu do odczytu przez pomiar LootTally (tylko czyta).</summary>
        internal static ItemRoster QueueRoster { get { return ShareQueue; } }
        // m109-lup (klucz FallenRiderHorsesLooted): konie i uprzaz z listy trofeow przetrzymane na czas jednego wywolania Spoils
        // LootCollectionBehavior.OnMapEventEnded - Spoils (:899-941) zdejmowal kazda sztuke IsMountable || HorseHarness do taboru (_horsesFromLoot),
        // ktory u nas jest zamkniety (NoBaggageTrain), i przepadaly. Wracaja w prefiksie ApplyDamageToLoot (:944, zaraz po petli Spoils), resztka
        // (wyjatek w Spoils) - finalizer do sakw
        private static readonly ItemRoster MountHold = new ItemRoster();
        private static bool _mountArmed;      // MountHold napelniony w tym wywolaniu Spoils
        private static bool _screenThisCall;  // AfterGenerateLoot podmienil liste w tym wywolaniu Spoils - finalizer zamyka pomiar bitwy
        private static int _mountStumbles;
        // m109-lup (recenzja L2): kopie koni z lupu tej misji, ktore zostaly w zbrojowni wojska po zdjeciu dzialki (count - zdjete) - HorseFate.TakeKill
        // zdejmuje padlego konia ze zbrojowni najwyzej tyle razy, potem z kolejki; dotad TakeOne bral dowolna sztuke tego id, takze konia gracza, a kopia
        // padlego zostawala w kolejce. Liczone w AfterDtePartyItems, czyszczone w HorseFate.ApplyMission (finally), nowej misji i LReset
        private static readonly Dictionary<ItemObject, int> _armLeft = new Dictionary<ItemObject, int>();
        private static bool _armLeftKnown;    // dzialka tej misji policzona (inaczej -1 - zbrojownia bez limitu, jak dotad)

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { var t = asm.GetType(fullName); if (t != null) return t; } catch { }
            }
            return null;
        }

        // czy ostatnio konczona bitwa byla stoczona osobiscie (nie symulacja)
        private static bool RealFoughtBattle()
        {
            try
            {
                var ev = MapEvent.PlayerMapEvent ?? PlayerEncounter.Battle;
                if (ev == null) return false;
                return !ev.IsPlayerSimulation;
            }
            catch { return false; }
        }

        private static bool RotEnlisted()
        {
            try
            {
                var sub = Type.GetType("ROT.SubModule, ROT");
                if (sub == null) return false;
                var f = sub.GetField("EnlistmentBehavior", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var beh = f != null ? f.GetValue(null) : null;
                if (beh == null) return false;
                var fi = beh.GetType().GetField("IsEnlisted", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var v = fi != null ? fi.GetValue(beh) : null;
                return v is bool && (bool)v;
            }
            catch { return false; }
        }

        /// <summary>
        /// Prefix na RealisticLoot.LootCollectionBehavior.OnMapEventEnded.
        /// Gdy sluzysz w armii lorda (ROT enlisted), caly potok Spoils - rytual
        /// zbierania, setki przedmiotow, zloto z cial - NIE odpala sie dla Ciebie.
        /// Pole sprzata armia; szeregowiec zostaje w szeregu.
        /// </summary>
        internal static bool SoldierStandsDown(MapEvent mapEvent)
        {
            try
            {
                if (!Settings.Current.EnlistedSoldierNoLooting) return true;
                if (!RotEnlisted()) return true;
                if (mapEvent != null && mapEvent.IsPlayerMapEvent
                    && mapEvent.WinningSide == mapEvent.PlayerSide)
                {
                    TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(
                        "The quartermasters strip the field. A soldier keeps his place in the line.",
                        TaleWorlds.Library.Colors.Yellow));
                    Log.Info("BattlefieldLaw: bitwa armii wygrana w sluzbie - lup zostaje przy armii.");
                }
                return false;
            }
            catch { return true; }
        }

        internal static void ApplyAll(Harmony h)
        {
            // noc 25.10 P5: okno lupu - latki ramek raportu (tylko odczyt: DoApplyMapEventResults, CommitGoldChanges, DoEnd, OnSiegeAftermathApplied, ekrany Spoils);
            // PRZED wylacznikiem prawa pola bitwy (okno ma wlasne klucze Spoils Window After ...), wlasny try - wyjatek nie rusza reszty prawa pola bitwy
            try { SpoilsReport.Apply(h); } catch (Exception e) { Log.Error("BattlefieldLaw.ApplyAll (okno lupu P5)", e); }
            if (!Settings.Current.BattlefieldLawEnabled) { Log.Info("BattlefieldLaw: wylaczone w ustawieniach."); return; }
            try
            {
                var skipType = Type.GetType("RealisticLoot.Patches.SkipVanillaLootPatch, RealisticLoot");
                var modelType = Type.GetType("RealisticLoot.Models.RealisticLootModel, RealisticLoot");
                var dmgType = Type.GetType("RealisticLoot.Models.EquipmentDamageModel, RealisticLoot");
                _rlPresent = skipType != null;

                if (modelType != null)
                {
                    if (Settings.Current.SimBattleFullDrop)
                    {
                        var tierMult = AccessTools.Method(modelType, "GetTierDropMultiplier");
                        if (tierMult != null)
                        {
                            h.Patch(tierMult, postfix: new HarmonyMethod(typeof(BattlefieldLaw), "FullTierMultiplier"));
                            Log.Info("BattlefieldLaw: bitwy automatyczne bez mnoznikow tieru.");
                        }
                    }
                    // rytual Spoils (godziny zbierania, oboz, zloto z cial) zostaje;
                    // podmieniamy tylko ZAWARTOSC jego ekranu na prawdziwy lup z DTE
                    var gen = AccessTools.Method(modelType, "GenerateLootFromUnitList");
                    if (gen != null)
                    {
                        h.Patch(gen, postfix: new HarmonyMethod(typeof(BattlefieldLaw), "AfterGenerateLoot"));
                        Log.Info("BattlefieldLaw: ekran Spoils dostaje faktyczny lup z pola (DTE), loteria za burta.");
                    }
                }
                else Log.Info("BattlefieldLaw: RealisticLoot nieobecny - lup DTE plynie ekranem vanilla.");

                // zaciezny zolnierz w cudzej armii NIE rozbiera calego pola bitwy:
                // rytual Spoils (godziny, lup, zloto z cial) nalezy do dowodcy, nie szeregowca
                var collType = Type.GetType("RealisticLoot.Behaviors.LootCollectionBehavior, RealisticLoot");
                if (collType != null)
                {
                    var omee = AccessTools.Method(collType, "OnMapEventEnded");
                    if (omee != null)
                    {
                        h.Patch(omee, prefix: new HarmonyMethod(typeof(BattlefieldLaw), "SoldierStandsDown"));
                        Log.Info("BattlefieldLaw: zaciezny nie lupi pola bitwy armii (kwatermistrz to robi).");
                        // m109-lup: finalizer - konie i uprzaz przetrzymane, ktorych Spoils nie odebral (wyjatek), do sakw; zamkniecie pomiaru bitwy
                        try
                        {
                            h.Patch(omee, finalizer: new HarmonyMethod(typeof(BattlefieldLaw), nameof(MountsFinalizer)));
                            MountFinalWired = true;
                        }
                        catch (Exception e) { Log.Error("BattlefieldLaw.ApplyAll (m109 finalizer)", e); }
                    }
                }

                // TABOR SPOILS WYCIETY NA STALE (Jeff 30.08: "robimy to").
                // Spoils robi WLASNY, drugi ekran bagazy ze zdjecia z POCZATKU
                // bitwy - rownolegle do vanillowego podzialu wozow wg wkladu:
                // te same rzeczy wpadaly dwa razy, bez ogladania na sojusznikow.
                // Checkbox w MCM wraca przy kazdym presecie/resecie (kazdy
                // preset ma ON), wiec gasimy u zrodla: getter ustawienia
                // odpowiada false, caly kod taboru Spoils spi.
                var mcmType = Type.GetType("RealisticLoot.Settings.MCMSettings, RealisticLoot");
                // m114-lup (recenzja): kazda latka gettera Spoils we wlasnym try - wyjatek tutaj nie moze wylaczyc reszty prawa pola bitwy (dzialka DTE, E8/A, Z2, LootTally)
                try
                {
                    var gBag = mcmType != null ? AccessTools.PropertyGetter(mcmType, "EnableBaggageTrain") : null;
                    if (gBag != null)
                    {
                        h.Patch(gBag, postfix: new HarmonyMethod(typeof(BattlefieldLaw), "NoBaggageTrain"));
                        Log.Info("BattlefieldLaw: tabor Spoils (Baggage Train) wyciety na stale - wozy dzieli vanilla wg wkladu.");
                    }
                }
                catch (Exception e) { Log.Error("BattlefieldLaw.ApplyAll (EnableBaggageTrain)", e); }

                // m114-lup (Jeff 24.10 17:40: "dostaje tylko zbroje i zero broni... wszystkich zabijam sam, wiec wszystko powinno byc moje. Cos dzieli lup zle";
                // 17:55: "bron byla potem w ekwipunku, ale nie na ekranie lupu - jakby przeniesiona przed ekranem"). Spoils dzieli liste trofeow DRUGI raz
                // (LootCollectionBehavior.BuildLootScreenRoster, EnablePrioritySystem, bez MCM, kazdy preset ON): przy choc jednym zolnierzu w druzynie
                // na ekran idzie 30% sztuk od najdrozszych (zbroje), reszta (tania bron bandytow) po cichu do sakw. Dzialke gracza liczy juz BattlefieldLaw
                // wg powalonych, wiec getter odpowiada false - cala lista trofeow na ekran. Jedyny odczyt gettera to ten podzial (_isSoloParty nietkniete:
                // kara rannych, czas szukania, komunikat solo). Klucz SpoilsSoldierSplitOff.
                try
                {
                    var gPrio = mcmType != null ? AccessTools.PropertyGetter(mcmType, "EnablePrioritySystem") : null;
                    if (gPrio != null)
                    {
                        h.Patch(gPrio, postfix: new HarmonyMethod(typeof(BattlefieldLaw), nameof(NoSoldierSplit)));
                        SoldierSplitWired = true;
                        Log.Info("BattlefieldLaw: drugi podzial Spoils (Priority: 30% na ekran, reszta po cichu do sakw) wylaczony - dzialke liczy BattlefieldLaw wg powalonych.");
                    }
                    else Log.Info("BattlefieldLaw: UWAGA brak EnablePrioritySystem w Spoils - drugi podzial NIE wylaczony.");
                }
                catch (Exception e) { Log.Error("BattlefieldLaw.ApplyAll (m114 EnablePrioritySystem)", e); }

                if (dmgType != null) _applyDamage = AccessTools.Method(dmgType, "ApplyDamageToLoot");
                // m109-lup: konie i uprzaz przetrzymane w AfterGenerateLoot wracaja na liste trofeow (prefiks; Spoils wola ApplyDamageToLoot tylko w :944)
                if (dmgType != null)
                {
                    try
                    {
                        var adl = AccessTools.DeclaredMethod(dmgType, "ApplyDamageToLoot", new[] { typeof(ItemRoster), typeof(float) });
                        if (adl != null)
                        {
                            h.Patch(adl, prefix: new HarmonyMethod(typeof(BattlefieldLaw), nameof(ReturnMounts)));
                            MountReturnWired = true;
                        }
                    }
                    catch (Exception e) { Log.Error("BattlefieldLaw.ApplyAll (m109 ApplyDamageToLoot)", e); }
                }
                var dispatcher = AccessTools.Method(typeof(CampaignEventDispatcher), "OnCollectLootItems");
                if (dispatcher != null)
                {
                    h.Patch(dispatcher, postfix: new HarmonyMethod(typeof(BattlefieldLaw), "AfterCollectLoot"));
                    Log.Info("BattlefieldLaw: lupy z ekranu dostaja stan bojowy (zuzycie Spoils)."
                             + (_applyDamage == null ? " (Spoils nieobecny - bez zuzycia)" : ""));
                }

                // wpis 82: lup wedlug tego, kto powalil - liczymy powalonych wrogow (gracz/towarzysz vs zolnierz)
                {
                    var missionK = FindType("DynamicTroopEquipmentReupload.DynamicTroopMissionLogic");
                    var removedK = missionK != null ? AccessTools.Method(missionK, "OnAgentRemoved") : null;
                    if (removedK != null)
                    {
                        h.Patch(removedK, postfix: new HarmonyMethod(typeof(BattlefieldLaw), nameof(CountKill)));
                        Log.Info("BattlefieldLaw: dzialka gracza wedlug tego, kto powalil wroga (Ty i towarzysze 100%, zolnierze 1/3 dla kapitana).");
                    }
                }

                // wraki: podgladamy, ktora czesc DTE uznal za rozbita ciosem, i zamiast
                // pozwolic jej zniknac, kladziemy ja na ekranie lupow jako zlom do naprawy
                if (Settings.Current.WreckSalvageEnabled)
                {
                    var selector = FindType("DynamicTroopEquipmentReupload.ArmorSelector");
                    var mission = FindType("DynamicTroopEquipmentReupload.DynamicTroopMissionLogic");
                    var pickM = selector != null ? AccessTools.Method(selector, "GetRandomArmorByBodyPart") : null;
                    var removedM = mission != null ? AccessTools.Method(mission, "OnAgentRemoved") : null;
                    if (pickM != null && removedM != null)
                    {
                        h.Patch(pickM, postfix: new HarmonyMethod(typeof(BattlefieldLaw), "RecordStruckPiece"));
                        h.Patch(removedM, postfix: new HarmonyMethod(typeof(BattlefieldLaw), "QueueWreck"));
                        Log.Info("BattlefieldLaw: czesci rozbite ciosem wracaja jako wraki do naprawy.");
                    }
                    if (modelType != null)
                    {
                        _rlRegisterLucky = AccessTools.Method(modelType, "RegisterLuckyElement");
                        if (dmgType != null)
                        {
                            _rlEnsureCache = AccessTools.Method(dmgType, "EnsureCacheInitialized");
                            _rlHeavy = AccessTools.Field(dmgType, "_lootedHeavy");
                            _rlHeavyMax = AccessTools.Field(dmgType, "_lootedHeavyMax");
                        }
                    }
                }

                // PRAWO ZACHOWANIA LUPU (Jeff 30.08: "nie mozemy dostac NIC
                // ponad to, co ma wroga armia"). W bitwie STOCZONEJ OSOBISCIE
                // sprzet wroga placi sie fizycznie: zabity - DTE zdejmuje go
                // ze sceny do magazynu partii ZABOJCY; ranny wziety do niewoli -
                // obszukanie jenca; ranny, ktory uciekl - nic (uciekl w swoim).
                // Vanilla ROWNOLEGLE generowala DRUGI komplet z SZABLONOW za
                // kazdego zabitego I rannego pokonanych (LootDefeatedParty
                // Casualties) - czysta inflacja materii. Wycinamy ja w bitwach
                // osobistych; symulacje ida po staremu.
                var mCas = AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyCasualties");
                if (mCas != null)
                {
                    h.Patch(mCas, prefix: new HarmonyMethod(typeof(BattlefieldLaw), "CutCasualtyLootPrefix"));
                    Log.Info("BattlefieldLaw: prawo zachowania lupu - szablonowa loteria za poleglych wycieta w bitwach osobistych.");
                }

                // dzialka gracza z tego, co DTE sciagnal z zabitych
                var dteMission = FindType("DynamicTroopEquipmentReupload.DynamicTroopMissionLogic");
                var dteRecord = FindType("DynamicTroopEquipmentReupload.PartyBattleRecord");
                var dteArmoryType = FindType("DynamicTroopEquipmentReupload.ArmyArmory");
                if (dteMission != null && dteRecord != null && dteArmoryType != null)
                {
                    _dteRecords = AccessTools.Field(dteMission, "_partyBattleRecords");
                    _dteLootedItems = AccessTools.Field(dteRecord, "LootedItems");
                    _dteArmory = AccessTools.Field(dteArmoryType, "Armory");
                    var handle = AccessTools.Method(dteMission, "HandlePartyItems");
                    if (handle != null && _dteRecords != null && _dteLootedItems != null && _dteArmory != null)
                    {
                        h.Patch(handle, postfix: new HarmonyMethod(typeof(BattlefieldLaw), "AfterDtePartyItems"));
                        // prawo zachowania lupu dziala tylko, gdy DTE realnie
                        // placi za zabitych - bez DTE vanilla loteria zostaje
                        CasualtyLootCut = mCas != null;
                        Log.Info("BattlefieldLaw: dzialka gracza z lupow DTE trafia na ekran (solo 100%, z druzyna "
                                 + Settings.Current.PlayerLootSharePercent + "%) + drugi podzial Spoils: "
                                 + (SoldierSplitWired && Settings.Current.SpoilsSoldierSplitOff ? "WYLACZONY" : "WLACZONY") + "."   // m114-lup
                                 + (CasualtyLootCut ? " Jency z bitew ida pod obszukanie (placa fizycznie za rannych)." : ""));
                        // E8/A (250, odpowiedz Jeffa 12.10 nr 1): lup cialo po ciele - kto powalil wlasciciela sztuki dopisywanej do lupu (latki wpiete zawsze,
                        // klucz OwnKillsLootWhole czytany w LootTag i AfterDtePartyItems; bez latek dzialka liczy sie srednia z puli jak dotad)
                        MethodInfo removedL = null, addL = null;
                        try
                        {
                            removedL = AccessTools.Method(dteMission, "OnAgentRemoved");
                            addL = AccessTools.Method(dteRecord, "AddLootedItem", new[] { typeof(ItemObject) });
                            if (removedL != null && addL != null)
                            {
                                h.Patch(removedL, prefix: new HarmonyMethod(typeof(BattlefieldLaw), nameof(LootKillPrefix)),
                                        finalizer: new HarmonyMethod(typeof(BattlefieldLaw), nameof(LootKillFinalizer)));
                                _lootWired++;
                                h.Patch(addL, prefix: new HarmonyMethod(typeof(BattlefieldLaw), nameof(LootTag)));
                                _lootWired++;
                            }
                        }
                        catch (Exception e) { Log.Error("BattlefieldLaw.ApplyAll (E8/A)", e); }
                        Log.Info("Misja gracza (E8/A): start - lup z wlasnych zabojstw w calosci [" + (Settings.Current.OwnKillsLootWhole ? "wl" : "wyl")
                                 + "], latki wpiete " + _lootWired + "/2 (DTE DynamicTroopMissionLogic.OnAgentRemoved - zabojca, PartyBattleRecord.AddLootedItem - sztuka)"
                                 + (_lootWired == 2 ? "" : " | nie znaleziono: " + (removedL == null ? "OnAgentRemoved " : "") + (addL == null ? "AddLootedItem(ItemObject) " : "")
                                                           + (removedL != null && addL != null ? "(wyjatek przy wpinaniu)" : "")) + ".");
                    }
                    else Log.Info("BattlefieldLaw: nie znalazlem wnetrza DTE - dzialka gracza nieaktywna.");
                }
                else Log.Info("BattlefieldLaw: DTE nieobecny - dzialka gracza nieaktywna.");

                // m109-lup Z2 (klucz RiderBlowSparesHarness, czytany w biegu): cios w jezdzca nie rozbija ladrow konia - prefiks selektora DTE dostaje
                // liste zbroi bez uprzezy (DTE GetAgentArmors bierze sloty 5-11, a ciezka uprzaz ma body_armor wieksze niz kolczuga jezdzca)
                try
                {
                    var selZ2 = FindType("DynamicTroopEquipmentReupload.ArmorSelector");
                    var pickZ2 = selZ2 != null ? AccessTools.DeclaredMethod(selZ2, "GetRandomArmorByBodyPart") : null;
                    if (pickZ2 != null)
                    {
                        h.Patch(pickZ2, prefix: new HarmonyMethod(typeof(BattlefieldLaw), nameof(SpareHarness)));
                        SpareHarnessWired = true;
                    }
                }
                catch (Exception e) { Log.Error("BattlefieldLaw.ApplyAll (m109 Z2)", e); }

                // m109-lup: pomiar lupu bitwy wedlug rodzaju i hak autotestu (LootTally) - latki wpiete zawsze, tylko czytaja
                if (modelType != null) _lReg = AccessTools.Method(modelType, "RegisterLuckyElement");
                LootTally.ApplyAll(h);
                LootTally.ApplyScreen(h);   // m114-lup: pomiar ekranu trofeow Spoils (lista -> ekran -> sakwy)
            }
            catch (Exception e) { Log.Error("BattlefieldLaw.ApplyAll", e); }
        }

        /// <summary>
        /// Ekran Spoils po stoczonej bitwie: zamiast wylosowanych ochlapow -
        /// dokladnie to, co lezalo na polu (dzialka gracza z DTE + udzial w bagazach).
        /// Zloto z cial, godziny zbierania i oboz licza sie w Spoils jak dotad;
        /// zuzycie tez nalozy Spoils (robi to po tej metodzie).
        /// </summary>
        public static void AfterGenerateLoot(ref ItemRoster __result)
        {
            try
            {
                ScreenIsRealLoot = false;   // E8/L (252): do nastepnej podmiany - lista trofeow Spoils to loteria (z niczego)
                if (!Settings.Current.BattlefieldLawEnabled) return;
                // 177-2 (krytyka pkt 2): loteria Spoils losuje z PRAWDZIWEGO zestawu pokonanego bohatera (GetRandomBattleEquipment) i dodaje KOPIE -
                // legenda i stal valyrianska z loterii to zawsze kopia (klinge bohatera bierze, jesli w ogole, zwyczaj wojenny - UniqueSpoils.Take);
                // w bitwie stoczonej osobiscie to samo robi CleanseTrash nizej, w symulacji dotad nic
                if (__result != null && Settings.Current.NoConjuredLegends) StripLegendCopies(__result);
                if (RotEnlisted()) { LootTally.QueueOut(ShareQueue, LootTally.Przepadlo); ShareQueue.Clear(); ClearWrecks(); ClearB(); LootWear.Close("zaciezny - lup zostaje przy armii", 0, 0); return; }
                bool sim = false;
                if (!RealFoughtBattle())
                {
                    // E8/L (252, PLAN 8.6c): "Send troops" wygrane przez gracza - jedna droga lupu. Za tych samych poleglych placi juz DTE (lup z cial do
                    // zbrojowni druzyny, dzialka kapitana w kolejce - HorseFate.SimShare), wiec loteria Spoils schodzi z ekranu jak w bitwie stoczonej
                    sim = SimOneLoot(MapEvent.PlayerMapEvent ?? PlayerEncounter.Battle);
                    if (!sim) return;                    // pozostale symulacje: zostaje loteria Spoils (odbramkowana)
                }
                if (__result == null) __result = new ItemRoster();
                if (sim)
                {
                    int lot = 0;
                    for (int i = 0; i < __result.Count; i++) { var le = __result.GetElementCopyAtIndex(i); if (le.Amount > 0) lot += le.Amount; }
                    _simCutSpoils += lot; _simCutBattles++;
                    Log.Info("BattlefieldLaw (E8/L): Send troops - loteria Spoils pominieta (" + lot + " szt. z niczego), na ekran idzie dzialka kapitana z lupu DTE ("
                             + ShareQueue.Count + " pozycji w kolejce" + (ShareQueue.Count == 0 ? " - kolejka pusta: dzialka 0 albo DTE rozlicza po Spoils, wtedy trafi do sakw po godzinie" : "") + ").");
                }
                // AUDYT LUPOW (Jeff 29.08: "czy nie ma podwojnego otrzymywania?"):
                // realna bitwa NIGDY nie zostawia vanillowej loterii na ekranie -
                // nawet gdy kolejka jest pusta (np. flush godzinowy juz oddal
                // dzialke do sakw; loteria na wierzchu bylaby DRUGA nagroda)
                __result.Clear();
                ScreenIsRealLoot = true;    // E8/L (252): od teraz na liscie trofeow sa tylko prawdziwe sztuki z pola (albo nic)
                int wOk = Settings.Current.LootWeaponsKeepCondition ? 0 : -1, wNeed = 0, wMod = 0;   // m114-lup: bron bez loterii stanu (linia m110)
                if (ShareQueue.Count == 0 && Wrecks.Count == 0) { ClearB(); LootWear.Close("lista trofeow Spoils", 0, 0, wOk, 0, 0); ScreenDone(); return; }
                for (int i = 0; i < ShareQueue.Count; i++)
                {
                    var el = ShareQueue.GetElementCopyAtIndex(i);
                    __result.AddToCounts(el.EquipmentElement, el.Amount);
                    LootTally.ToList(el.EquipmentElement.Item, el.Amount);   // m109-lup: pomiar (kolejka -> lista trofeow)
                }
                // m110-stan: sztuki ze stanem z obrazen chronione przed loteria stanu Spoils (ApplyDamageToLoot zachowuje zarejestrowane 1:1; ClearLuckyItems
                // biegl na poczatku GenerateLootFromUnitList, przed tym postfiksem) - takze sprawne (klucz "id|")
                int bOk = 0, bNeed = 0;
                for (int i = 0; i < _bQueued.Count; i++)
                {
                    var el = _bQueued.GetElementCopyAtIndex(i);
                    if (el.EquipmentElement.Item == null || el.Amount <= 0) continue;
                    bNeed += el.Amount;
                    if (_lReg == null) continue;
                    for (int n = 0; n < el.Amount; n++)
                    {
                        try { _lReg.Invoke(null, new object[] { el.EquipmentElement }); bOk++; }
                        catch (Exception e) { MountStumble("AfterGenerateLoot(lucky m110)", e); break; }
                    }
                }
                // m114-lup (LootWeaponsKeepCondition): bron, tarcze i amunicja z kolejki - ten sam znacznik lucky Spoils co zbroja wyzej (RegisterLuckyElement),
                // nadany po przesianiu listy (RegisterWeaponsLucky nizej); ewidencja przed wyczyszczeniem _bQueued (sztuki B pominiete)
                ItemRoster wq = null;
                if (wOk >= 0)
                {
                    try { wq = WeaponShare(ShareQueue); }
                    catch (Exception e) { MountStumble("AfterGenerateLoot(lucky m114)", e); }
                }
                _bQueued.Clear(); _bOrig.Clear();
                ShareQueue.Clear(); _simPending = false;   // recenzja E8/L: kolejka oddana na ekran
                LootTally.WrecksIn(Wrecks);
                int wrecks = AppendWrecks(__result);
                CleanseDragons(__result);
                CleanseTrash(__result);
                LootTally.Sifted(__result);
                if (wq != null)
                {
                    try { RegisterWeaponsLucky(__result, wq, ref wOk, ref wNeed, ref wMod); }
                    catch (Exception e) { MountStumble("AfterGenerateLoot(lucky m114)", e); }
                }
                Log.Info("BattlefieldLaw: ekran Spoils podmieniony na " + __result.Count + " pozycji prawdziwego lupu"
                         + (wrecks > 0 ? " (w tym " + wrecks + " wrakow przed przesianiem)" : "") + ".");
                // m109-lup (FallenRiderHorsesLooted): konie i uprzaz poza zasiegiem petli Spoils, ktora zabralaby je do zamknietego taboru - wracaja
                // w prefiksie ApplyDamageToLoot tego samego wywolania (ReturnMounts); tylko przy obu latkach (inaczej jak dotad)
                bool hold = Settings.Current.FallenRiderHorsesLooted && MountReturnWired && MountFinalWired;
                if (hold) HoldMounts(__result);
                // m110-stan: sprawne sztuki B zostaja w ewidencji tylko dla przetrzymanych koni i uprzezy (rezerwa do sakw w finalizerze), reszta - lista trofeow
                if (_bSound.Count > 0)
                {
                    var keep = new Dictionary<ItemObject, int>();
                    if (hold)
                        for (int i = 0; i < MountHold.Count; i++)
                        {
                            var it = MountHold.GetElementCopyAtIndex(i).EquipmentElement.Item;
                            int v;
                            if (it != null && _bSound.TryGetValue(it, out v) && v > 0) keep[it] = v;
                        }
                    _bSound.Clear();
                    foreach (var kv in keep) _bSound[kv.Key] = kv.Value;
                    _bSoundHeld = _bSound.Count > 0;
                }
                LootTally.OnList(__result, hold);
                LootWear.Close("lista trofeow Spoils", bOk, bNeed, wOk, wNeed, wMod);
                ScreenDone();
            }
            catch (Exception e) { Log.Error("AfterGenerateLoot", e); }
        }

        /// <summary>m109-lup: lista trofeow Spoils z prawdziwym lupem gotowa - pomiar bitwy zamyka finalizer Spoils (po oddaniu koni), bez niego od razu.</summary>
        private static void ScreenDone()
        {
            if (MountFinalWired) _screenThisCall = true;
            else LootTally.Close("lista trofeow Spoils");
        }

        /// <summary>m109-lup: konie (IsMountable) i uprzaz z listy trofeow do MountHold - ten sam warunek, co petla Spoils OnMapEventEnded :907.</summary>
        private static void HoldMounts(ItemRoster list)
        {
            try
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var el = list.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0) continue;
                    if (!it.IsMountable && it.ItemType != ItemObject.ItemTypeEnum.HorseHarness) continue;
                    list.AddToCounts(el.EquipmentElement, -el.Amount);
                    MountHold.AddToCounts(el.EquipmentElement, el.Amount);
                    LootTally.Held(it, el.Amount);
                }
                _mountArmed = MountHold.Count > 0;
            }
            catch (Exception e) { MountStumble("HoldMounts", e); _mountArmed = MountHold.Count > 0; }
        }

        private static void MountStumble(string where, Exception e)
        {
            _mountStumbles++;
            if (_mountStumbles <= 3 || _mountStumbles % 50 == 0) Log.Error("BattlefieldLaw." + where, e);
        }

        /// <summary>m109-lup: prefiks Spoils EquipmentDamageModel.ApplyDamageToLoot(lootRoster, ratio) - przetrzymane konie i uprzaz wracaja na liste
        /// trofeow. Kon dostaje znacznik "lucky" Spoils (RegisterLuckyElement - ApplyDamageToLoot zostawia go 1:1, bez losowej kulawizny), uprzaz zuzycie
        /// Spoils jak kazda zbroja (wrak uprzezy byl juz zarejestrowany w AppendWrecks). Sztuka schodzi z MountHold dopiero po dopisaniu - bez dubli.</summary>
        public static void ReturnMounts(ItemRoster __0)
        {
            if (!_mountArmed) return;
            try
            {
                if (__0 == null) return;   // finalizer wysypie do sakw
                var els = new List<ItemRosterElement>();
                for (int i = 0; i < MountHold.Count; i++) els.Add(MountHold.GetElementCopyAtIndex(i));
                foreach (var el in els)
                {
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0) continue;
                    if (it.IsMountable && _lReg != null)
                        for (int n = 0; n < el.Amount; n++)
                        {
                            try { _lReg.Invoke(null, new object[] { el.EquipmentElement }); }
                            catch (Exception e) { MountStumble("ReturnMounts(lucky)", e); break; }
                        }
                    __0.AddToCounts(el.EquipmentElement, el.Amount);
                    MountHold.AddToCounts(el.EquipmentElement, -el.Amount);
                    LootTally.Returned(it, el.Amount);
                }
                _mountArmed = MountHold.Count > 0;
            }
            catch (Exception e) { MountStumble("ReturnMounts", e); }
        }

        /// <summary>m109-lup: finalizer Spoils LootCollectionBehavior.OnMapEventEnded - co z MountHold nie wrocilo na liste (wyjatek w Spoils), do sakw;
        /// potem zamkniecie pomiaru bitwy. Wyjatek Spoils przechodzi dalej bez zmian.</summary>
        public static Exception MountsFinalizer(Exception __exception)
        {
            try
            {
                if (MountHold.Count > 0) FlushMountsToBaggage(__exception != null ? "wyjatek w Spoils" : "Spoils nie oddal listy");
                _mountArmed = false;
                if (_bSoundHeld) { _bSound.Clear(); _bSoundHeld = false; }   // m110-stan: ewidencja sprawnych B dla przetrzymanych - tylko w tym wywolaniu Spoils
                if (_screenThisCall) { _screenThisCall = false; LootTally.Close("lista trofeow Spoils"); }
            }
            catch (Exception e) { MountStumble("MountsFinalizer", e); }
            return __exception;
        }

        /// <summary>m109-lup: rezerwa - konie bez zmian, uprzaz ze stanem bojowym (jak FlushShareToBaggage) do sakw gracza. Zwraca liczbe sztuk.</summary>
        private static int FlushMountsToBaggage(string why)
        {
            // noc 25.10 P5: ramka F okna lupu (jw.; z FlushShareToBaggage zagniezdzona - liczy ramka zewnetrzna)
            var spoilsF = SpoilsReport.OpenF();
            try { return FlushMountsToBaggageCore(why); }
            finally { SpoilsReport.CloseF(spoilsF, why); }
        }

        private static int FlushMountsToBaggageCore(string why)
        {
            int moved = 0;
            _mountArmed = false;   // przed wysypaniem - prefiks ReturnMounts nie dubluje
            try
            {
                var bag = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
                if (bag == null || MountHold.Count == 0) return 0;
                var els = new List<ItemRosterElement>();
                for (int i = 0; i < MountHold.Count; i++) els.Add(MountHold.GetElementCopyAtIndex(i));
                foreach (var el in els)
                {
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0) continue;
                    for (int n = 0; n < el.Amount; n++)
                    {
                        var mod = el.EquipmentElement.ItemModifier;
                        if (mod == null && !TakeBSound(it)) mod = ArmouryBehavior.PickWornModifier(it);   // m110-stan: sprawna sztuka B zostaje sprawna
                        bag.AddToCounts(new EquipmentElement(it, mod), 1);
                    }
                    MountHold.AddToCounts(el.EquipmentElement, -el.Amount);
                    LootTally.Reserve(it, el.Amount);
                    moved += el.Amount;
                }
                if (moved > 0)
                {
                    Log.Info("BattlefieldLaw (m109): konie i uprzaz powalonych do sakw (" + moved + " szt.) - " + why + ".");
                    Log.Player("The horses and harness of the fallen are led to your baggage: " + moved + ".", false);
                }
            }
            catch (Exception e) { MountStumble("FlushMountsToBaggage", e); }
            return moved;
        }

        /// <summary>m109-lup (recenzja L2, HorseFate.TakeKill): ile kopii tego konia z lupu tej misji zostalo w zbrojowni wojska po zdjeciu dzialki;
        /// -1 - dzialka tej misji nie liczona (zbrojownia bez limitu, jak dotad).</summary>
        internal static int ArmoryCopiesLeft(ItemObject it)
        {
            if (!_armLeftKnown || it == null) return -1;
            int n;
            return _armLeft.TryGetValue(it, out n) ? n : 0;
        }

        /// <summary>m109-lup (recenzja L2): padly kon zdjety ze zbrojowni wojska - jedna kopia mniej.</summary>
        internal static void UseArmoryCopy(ItemObject it)
        {
            int n;
            if (it != null && _armLeft.TryGetValue(it, out n) && n > 0) _armLeft[it] = n - 1;
        }

        internal static void ClearArmoryCopies() { _armLeft.Clear(); _armLeftKnown = false; }

        /// <summary>m109-lup (HorseFate.TakeKill): padly kon jezdzca powalonego przez druzyne gracza schodzi z kolejki lupu - jedna sztuka, najpierw bez stanu.</summary>
        internal static bool TakeQueuedHorse(ItemObject it)
        {
            try
            {
                if (it == null) return false;
                for (int pass = 0; pass < 2; pass++)
                    for (int i = 0; i < ShareQueue.Count; i++)
                    {
                        var el = ShareQueue.GetElementCopyAtIndex(i);
                        if (el.EquipmentElement.Item != it || el.Amount <= 0) continue;
                        if (pass == 0 && el.EquipmentElement.ItemModifier != null) continue;
                        ShareQueue.AddToCounts(el.EquipmentElement, -1);
                        return true;
                    }
            }
            catch (Exception e) { MountStumble("TakeQueuedHorse", e); }
            return false;
        }

        private static void StripLegendCopies(ItemRoster roster)
        {
            int n = 0;
            var ids = new List<string>();
            for (int i = roster.Count - 1; i >= 0; i--)
            {
                var el = roster.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || !(LegendaryLaw.IsLegend(it) || ValyrianBlades.Is(it))) continue;
                roster.AddToCounts(el.EquipmentElement, -el.Amount);
                n += el.Amount; ids.Add(it.StringId);
            }
            if (n > 0) Log.Info("BattlefieldLaw: lup Spoils bez kopii legend - " + n + " szt. (" + string.Join(", ", ids.ToArray()) + ") - klinga zostaje przy bohaterze albo idzie zwyczajem wojennym.");
        }

        /// <summary>
        /// SMIEC I LEGENDA NIE LEZA W WORKACH (Jeff 28.08). Smiec: sprzet zbity
        /// ponizej LootMinConditionPercent wartosci jest ZNISZCZONY - nie ma
        /// czego niesc (tnie tez zalew wrakow na 3%). Legenda: nazwane klingi
        /// ROT (Brightroar, Widow's Wail... value 100k+, po kilkadziesiat sztuk
        /// u elitarnych jednostek) nie moga lezec masowo w lupach - "unikat"
        /// ma byc unikatem.
        /// </summary>
        private static void CleanseTrash(ItemRoster roster)
        {
            try
            {
                if (roster == null) return;
                var s = Settings.Current;
                int trash = 0, legends = 0;
                for (int i = roster.Count - 1; i >= 0; i--)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0) continue;
                    // SLON NIE LEZY W WORKACH (Jeff 29.08: "w bitwie nie bylo
                    // sloni, a po pladrowaniu mam slonie - wywal!"): pokonani
                    // wozili slonie w bagazach; bojowe slonie Zlotej Kompanii
                    // zyja w szablonach i ich nie ruszamy
                    if (it.StringId != null && (it.StringId == "elephant" || it.StringId.StartsWith("rot_elephant")))
                    {
                        roster.AddToCounts(el.EquipmentElement, -el.Amount);
                        trash += el.Amount;
                        LootTally.SiftOut(it, el.Amount, LootTally.SEgz);   // m109-lup (recenzja L4): slon - egzotyk
                        continue;
                    }
                    if (LegendaryLaw.IsLegend(it))
                    {
                        roster.AddToCounts(el.EquipmentElement, -el.Amount);
                        legends += el.Amount;
                        LootTally.SiftOut(it, el.Amount, LootTally.SLeg);   // m109-lup (recenzja L4)
                        continue;
                    }
                    // sprzet olbrzymow (Jeff 14.09): czlowiek go nie uzywa, wiec
                    // nie leci do worka - chyba ze gracz prowadzi olbrzymow
                    if ((GiantGear.Is(it) && !GiantGear.PartyHasGiants(MobileParty.MainParty))
                        || (MountLaw.IsExotic(it) && !MountLaw.AllowedForParty(MobileParty.MainParty, it)))
                    {
                        roster.AddToCounts(el.EquipmentElement, -el.Amount);
                        legends += el.Amount;
                        LootTally.SiftOut(it, el.Amount, LootTally.SEgz);   // m109-lup (recenzja L4): olbrzymy i egzotyki
                        continue;
                    }
                    if (s.LootMinConditionPercent > 0)
                    {
                        var mod = el.EquipmentElement.ItemModifier;
                        // m110-stan (recenzja R7): przy stanach - caly przedzial stanu na progu albo nizej (wrak 0.05 nie ginie juz przy progu 5-9)
                        if (mod != null && (GearState.On ? GearState.BelowDestroy(mod, s.LootMinConditionPercent)
                                                         : mod.PriceMultiplier * 100f <= s.LootMinConditionPercent + 0.01f))
                        {
                            roster.AddToCounts(el.EquipmentElement, -el.Amount);
                            trash += el.Amount;
                            LootTally.SiftOut(it, el.Amount, LootTally.SZnisz);   // m109-lup (recenzja L4)
                        }
                    }
                }
                if (trash > 0 || legends > 0)
                    Log.Info("BattlefieldLaw: lup przesiany - " + trash + " szt. zniszczonych (<=" +
                             s.LootMinConditionPercent + "%) i " + legends + " legendarnych klng odpadlo.");
            }
            catch (Exception e) { Log.Error("CleanseTrash", e); }
        }

        /// <summary>
        /// SMOK TO NIE CHABETA (Jeff 28.08: "jakim cudem mam trzy smoki
        /// w ekwipunku?!"). ROT daje jednostkom Targaryenow wierzchowce
        /// dragon_* (Type=Horse), a gra wrzuca mounty pokonanych do lupu jak
        /// kazdego konia - is_merchandise=false przed tym nie chroni. Smoki
        /// sa fabularne: wycinamy je z KAZDEGO lupu bitewnego.
        /// </summary>
        private static void CleanseDragons(ItemRoster roster)
        {
            try
            {
                if (roster == null) return;
                int cut = 0;
                for (int i = roster.Count - 1; i >= 0; i--)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || it.StringId == null) continue;
                    if (!it.StringId.StartsWith("dragon_")) continue;
                    if (it.ItemType != ItemObject.ItemTypeEnum.Horse) continue;
                    roster.AddToCounts(el.EquipmentElement, -el.Amount);
                    cut += el.Amount;
                    LootTally.SiftOut(it, el.Amount, LootTally.SSmok);   // m109-lup (recenzja L4): przyczyna przesiania
                }
                if (cut > 0) Log.Info("BattlefieldLaw: " + cut + " smokow wycietych z lupu - smoki nie leza w workach.");
            }
            catch (Exception e) { Log.Error("CleanseDragons", e); }
        }

        /// <summary>Dzialka gracza z tego, co druzyna sciagnela z zabitych (DTE) - wyjeta z magazynu, do kolejki na ekran.</summary>
        public static void AfterDtePartyItems(object __instance, object partyId, bool isPlayerParty, bool isVictorious)
        {
            try
            {
                if (isPlayerParty) ClearArmoryCopies();   // m109-lup (recenzja L2): kopie koni w zbrojowni liczone od nowa dla tej misji
                if (isPlayerParty) _soundFlushed.Clear();  // m110-stan: nowe rozliczenie - sprawne B poprzedniej bitwy juz nie czekaja na WearTheLoot
                if (!Settings.Current.BattlefieldLawEnabled || !isPlayerParty || !isVictorious) { if (isPlayerParty) DropPools(); return; }
                if (RotEnlisted()) { DropPools(); return; }
                int pct = PlayerSharePercent();
                // E8/A (250, odpowiedz Jeffa 1): cialo po ciele - sztuki z cial powalonych przez Ciebie i towarzyszy w calosci, przez zolnierzy trzecia
                // kapitana, bez znanego zabojcy (lup morski z losowania DTE) srednia z puli jak dotad; brak znacznikow z tej misji = sama srednia (kod sprzed)
                bool whole = false;
                int third = 0, wHero = 0, wTroop = 0, wUnk = 0, wAvg = 0, wNew = 0;
                try
                {
                    third = Math.Max(0, Math.Min(100, Settings.Current.PlayerLootSharePercent));
                    lock (_lootHero)
                        whole = Settings.Current.OwnKillsLootWhole && ReferenceEquals(Mission.Current, _lootMission) && _lootHero.Count + _lootTroop.Count > 0;
                }
                catch { whole = false; }
                var armory = _dteArmory.GetValue(null) as ItemRoster;
                // m110-stan (i): czesci z ciosu i pekniete wedlug stanu z obrazen - przed dzialka (bez zbrojowni - jak dotad, wrak)
                bool bOn = LootWear.On;
                if (bOn)
                {
                    try
                    {
                        // recenzja R4: przy lupie cialo po ciele czesci zolnierza dziela sie jednym zaokragleniem ze zwyklym lupem tego przedmiotu
                        var reg = whole && armory != null ? PlayerLootCounts(__instance, partyId) : null;
                        SplitWrecks(armory, third, pct, reg);
                    }
                    catch (Exception e) { MountStumble("SplitWrecks", e); }
                }
                if (pct <= 0 && !whole) { DropPools(); return; }

                if (armory == null) { DropPools(); return; }

                // rekord bitewny partii gracza - jedyny z isPlayerParty
                var records = _dteRecords.GetValue(__instance) as System.Collections.IEnumerable;
                if (records == null) { DropPools(); return; }   // m110-stan (recenzja R5): pule czesci nie zostaja na nastepna bitwe
                int moved = 0;
                foreach (var entry in records)
                {
                    var et = entry.GetType();
                    var key = et.GetProperty("Key").GetValue(entry, null);
                    if (key == null || partyId == null || !key.Equals(partyId)) continue;   // tylko rekord partii gracza
                    var looted = _dteLootedItems.GetValue(et.GetProperty("Value").GetValue(entry, null)) as System.Collections.IEnumerable;
                    if (looted == null) continue;
                    _armLeftKnown = true;   // m109-lup (recenzja L2): rekord gracza liczony - kopie koni w zbrojowni ponizej
                    foreach (var kv in looted)
                    {
                        var kt = kv.GetType();
                        var item = kt.GetProperty("Key").GetValue(kv, null) as ItemObject;
                        int count = (int)kt.GetProperty("Value").GetValue(kv, null);
                        if (item == null || count <= 0) continue;
                        int share = Math.Min(count, (int)Math.Round(count * pct / 100.0, MidpointRounding.AwayFromZero));
                        if (whole)
                        {
                            int hk, tk;
                            lock (_lootHero) { _lootHero.TryGetValue(item, out hk); _lootTroop.TryGetValue(item, out tk); }
                            wAvg += share;
                            share = WholeShare(count, ref hk, ref tk, third, pct);
                            wHero += hk; wTroop += tk; wUnk += count - hk - tk; wNew += share;
                        }
                        if (share <= 0) { if (item.IsMountable) _armLeft[item] = count; continue; }   // m109-lup (L2): wszystkie kopie zostaja w zbrojowni
                        // m110-stan (ii): przedmiot z danymi czesci z cial (LootTag) - dzialka ze stanem z obrazen
                        List<LootWear.PieceData> ph = null, pt = null;
                        if (bOn)
                            lock (_lootHero)
                            {
                                if (_pdHero.TryGetValue(item, out ph)) _pdHero.Remove(item);
                                if (_pdTroop.TryGetValue(item, out pt)) _pdTroop.Remove(item);
                            }
                        if ((ph != null && ph.Count > 0) || (pt != null && pt.Count > 0))
                        {
                            int leftB;
                            try { moved += ShareWithState(armory, item, share, ph, pt, out leftB); }
                            catch (Exception e) { leftB = 0; MountStumble("ShareWithState", e); }
                            if (ph != null) foreach (var x in ph) LootWear.Route(x, LootWear.RZost);
                            if (pt != null) foreach (var x in pt) LootWear.Route(x, LootWear.RZost);
                            if (item.IsMountable) _armLeft[item] = count - (share - leftB);
                            continue;
                        }
                        // audyt pelny W1: zdejmujemy po wszystkich elementach tej sztuki RAZEM z ich stanem -
                        // wczesniej AddToCounts(item, -take) celowal w element bez modyfikatora, a kolejka i tak
                        // dostawala "take" (towar z niczego, gdy w magazynie byly tylko sztuki zuzyte)
                        int left = share;
                        for (int i = armory.Count - 1; i >= 0 && left > 0; i--)
                        {
                            var el = armory.GetElementCopyAtIndex(i);
                            if (el.EquipmentElement.Item != item || el.Amount <= 0) continue;
                            int t = Math.Min(left, el.Amount);
                            armory.AddToCounts(el.EquipmentElement, -t);
                            LootTally.Queued(item, t, LootTally.Dzialka);   // m109-lup: pomiar (przed dopisaniem - otwarcie bitwy liczy kolejke jako 'wczesniej')
                            ShareQueue.AddToCounts(el.EquipmentElement, t);
                            left -= t; moved += t;
                        }
                        if (item.IsMountable) _armLeft[item] = count - (share - left);   // m109-lup (L2): kopie tej misji, ktore zostaly w zbrojowni
                    }
                    break;   // rekord partii gracza znaleziony i przetworzony
                }
                if (moved > 0) Log.Info("BattlefieldLaw: " + moved + " szt. (" + (whole ? "cialo po ciele, srednia z puli " : "") + pct + "%) z magazynu wojska czeka na ekran lupow.");
                if (whole)
                {
                    Log.Info("BattlefieldLaw (E8/A): lup cialo po ciele - z cial powalonych przez Ciebie i towarzyszy " + wHero + " szt. w calosci (powalonych wrogow "
                             + (ReferenceEquals(Mission.Current, _killMission) ? _killsHero : 0) + "), przez zolnierzy " + wTroop + " szt. x " + third
                             + "%, bez znanego zabojcy " + wUnk + " szt. x " + pct + "% | dzialka gracza " + wNew + " szt. (zdjete z magazynu " + moved
                             + "), srednia z puli dalaby " + wAvg + " szt. | potkniecia (sesja) " + _lootStumbles + ".");
                    lock (_lootHero) { _lootHero.Clear(); _lootTroop.Clear(); _lootMission = null; }
                }
                DropPools();   // m110-stan: reszta pul - sztuki zostaja u wojska (bez stanu, jak dotad)
            }
            catch (Exception e) { Log.Error("AfterDtePartyItems", e); try { DropPools(); } catch { } }   // m110-stan (recenzja R5): po wyjatku pule tez puszczone
        }

        /// <summary>m110-stan (recenzja R4): zwykly lup partii gracza z rekordu DTE (przedmiot -> sztuk) - tylko odczyt, ta sama droga co petla dzialki
        /// w AfterDtePartyItems; null - brak rekordu.</summary>
        private static Dictionary<ItemObject, int> PlayerLootCounts(object inst, object partyId)
        {
            var records = _dteRecords.GetValue(inst) as System.Collections.IEnumerable;
            if (records == null || partyId == null) return null;
            foreach (var entry in records)
            {
                var et = entry.GetType();
                var key = et.GetProperty("Key").GetValue(entry, null);
                if (key == null || !key.Equals(partyId)) continue;
                var looted = _dteLootedItems.GetValue(et.GetProperty("Value").GetValue(entry, null)) as System.Collections.IEnumerable;
                if (looted == null) return null;
                var r = new Dictionary<ItemObject, int>();
                foreach (var kv in looted)
                {
                    var kt = kv.GetType();
                    var item = kt.GetProperty("Key").GetValue(kv, null) as ItemObject;
                    int count = (int)kt.GetProperty("Value").GetValue(kv, null);
                    if (item != null && count > 0) r[item] = count;
                }
                return r;
            }
            return null;
        }

        // E8/A (250; odpowiedz Jeffa 12.10 nr 1 "to, co ja zabilem, idzie do mnie"): LUP CIALO PO CIELE. Wpis 82 liczyl dzialke gracza jako SREDNIA z puli
        // (Ty i towarzysze 100%, zolnierze trzecia) i bral ja po rowno z kazdej pozycji lupu - przy sredniej ponizej 50% pojedyncza sztuka z ciala, ktore
        // powaliles sam, zaokraglala sie do zera i zostawala w zbrojowni ludzi. Teraz kazda sztuka dopisywana przez DTE do lupu partii gracza pamieta, kto
        // powalil jej wlasciciela: Ty albo towarzysz - cala Twoja; zolnierz - trzecia kapitana (PlayerLootSharePercent); bez znanego zabojcy (lup morski
        // z losowania przy rozliczeniu) - srednia z puli jak dotad. Zabojca jak w CountKill (jezdziec za konia). Klucz OwnKillsLootWhole.
        [ThreadStatic] private static Agent _lootKiller;        // zabojca w biegu DTE OnAgentRemoved (prefiks ustawia, finalizer czysci)
        [ThreadStatic] private static int _lootKind;            // 0 poza OnAgentRemoved, -1 jeszcze nie policzone, 1 Ty / towarzysz, 2 zolnierz partii gracza, 3 inni
        private static object _lootMission;
        private static readonly Dictionary<ItemObject, int> _lootHero = new Dictionary<ItemObject, int>();    // sztuki z cial powalonych przez gracza i towarzyszy
        private static readonly Dictionary<ItemObject, int> _lootTroop = new Dictionary<ItemObject, int>();   // ... przez zolnierzy partii gracza
        private static int _lootWired, _lootStumbles;

        /// <summary>Dzialka gracza z jednej pozycji lupu (count sztuk): hero sztuk z cial powalonych przez gracza i towarzyszy - w calosci; troop sztuk z cial
        /// powalonych przez zolnierzy - third procent; reszta (bez znanego zabojcy) - pct procent (srednia z puli). Ulamek zaokraglany jak dotad (od zera),
        /// razem dla czesci zolnierskiej i nieznanej; hero i troop przyciete do count.</summary>
        internal static int WholeShare(int count, ref int hero, ref int troop, int third, int pct)
        {
            if (count <= 0) { hero = 0; troop = 0; return 0; }
            hero = Math.Max(0, Math.Min(count, hero));
            troop = Math.Max(0, Math.Min(count - hero, troop));
            int unk = count - hero - troop;
            return Math.Min(count, hero + (int)Math.Round((troop * third + unk * pct) / 100.0, MidpointRounding.AwayFromZero));
        }

        public static void LootKillPrefix(Agent __0, Agent __1) { _lootKiller = __1; _lootVictim = __0; _lootKind = -1; }   // m110-stan: takze ofiara (DTE OnAgentRemoved: __0 ofiara, __1 zabojca)

        public static void LootKillFinalizer() { _lootKiller = null; _lootVictim = null; _lootKind = 0; }

        /// <summary>Prefiks DTE PartyBattleRecord.AddLootedItem(ItemObject): sztuka idzie do lupu partii zabojcy - zapamietaj, czy powalil bohater partii
        /// gracza, czy jej zolnierz (partia zabojcy = partia gracza, wiec to jej rekord). Tylko licznik; nie rzuca.</summary>
        public static void LootTag(ItemObject __0)
        {
            try
            {
                if (_lootKind == 0 || __0 == null) return;
                var s = Settings.Current;
                if (s == null || !s.BattlefieldLawEnabled) return;
                bool own = s.OwnKillsLootWhole;
                bool st = LootWear.On;   // m110-stan (LootStateFromWounds): stan czesci z obrazen do puli wedlug zabojcy
                if (!own && !st) return;
                if (_lootKind < 0) _lootKind = KillerKind(_lootKiller);
                if (_lootKind != 1 && _lootKind != 2) return;
                var m = Mission.Current;
                var pd = st ? LootWear.Piece(_lootVictim, __0, -1, LootWear.CLup, _lootKind, LootWear.WhoOf(_lootKind, _lootKiller)) : null;
                List<LootWear.PieceData> stale = null;
                lock (_lootHero)
                {
                    if (!ReferenceEquals(m, _lootMission)) { _lootMission = m; _lootHero.Clear(); _lootTroop.Clear(); stale = TakePoolsLocked(); }
                    if (own)
                    {
                        var d = _lootKind == 1 ? _lootHero : _lootTroop;
                        int n; d.TryGetValue(__0, out n); d[__0] = n + 1;
                    }
                    if (pd != null)
                    {
                        var dd = _lootKind == 1 ? _pdHero : _pdTroop;
                        List<LootWear.PieceData> l;
                        if (!dd.TryGetValue(__0, out l)) { l = new List<LootWear.PieceData>(); dd[__0] = l; }
                        l.Add(pd);
                    }
                }
                if (stale != null) foreach (var x in stale) LootWear.Route(x, LootWear.RZost);
            }
            catch { _lootStumbles++; }
        }

        private static int KillerKind(Agent a)
        {
            try
            {
                if (a == null) return 3;
                if (a.IsMount && a.RiderAgent != null) a = a.RiderAgent;   // stratowany przez konia - liczy sie jezdziec (jak CountKill)
                if (a == Agent.Main) return 1;
                if (!IsMainPartyAgent(a)) return 3;                           // sojusznicy i wrogowie - ich lup, ich rekord
                return a.IsHero ? 1 : 2;
            }
            catch { return 3; }
        }

        // wpis 82 (Jeff 05.10: "a jak jestem sam z 1 zolnierzem, a zabilem wszystkich ja?"): historycznie lup nalezal do
        // tego, kto go zdobyl; kapitan bral trzecia tylko z tego, co zdobyli jego ludzie (umowy Edwarda III). Liczymy wrogow
        // powalonych (zabici i ogluszeni) przez partie gracza: gracz i towarzysze (bohaterowie partii) -> 100% gracza,
        // zolnierze -> 1/3 gracza (PlayerLootSharePercent), 2/3 ludzi. Bitwa bez misji (symulacja) - stala trzecia.
        private static object _killMission;
        private static int _killsHero, _killsTroop;

        public static void CountKill(Agent affectedAgent, Agent affectorAgent, AgentState agentState)
        {
            try
            {
                var m = Mission.Current;
                if (m == null) return;
                if (!ReferenceEquals(m, _killMission)) { _killMission = m; _killsHero = 0; _killsTroop = 0; }
                if (agentState != AgentState.Killed && agentState != AgentState.Unconscious) return;
                if (affectedAgent == null || !affectedAgent.IsHuman || affectorAgent == null) return;
                // NM: powalony bez waznej druzyny (Team.Invalid) - pominiety i liczony; obie druzyny wazne przed natywnym IsEnemy
                if (MissionTeams.Skip(affectedAgent, MissionTeams.Kill) || !MissionTeams.Enemies(affectedAgent.Team, m.PlayerTeam)) return;
                var a = affectorAgent;
                if (a.IsMount && a.RiderAgent != null) a = a.RiderAgent;   // stratowany przez konia - liczy sie jezdziec
                if (a == Agent.Main) { _killsHero++; return; }
                if (!IsMainPartyAgent(a)) return;                             // sojusznicy - ich lup
                if (a.IsHero) _killsHero++; else _killsTroop++;
            }
            catch (Exception e) { MissionTeams.Stumble(MissionTeams.Kill, "BattlefieldLaw.CountKill", e); }   // NM: dotad cichy catch gubil zliczenie
        }

        private static int PlayerSharePercent()
        {
            try
            {
                int third = Math.Max(0, Math.Min(100, Settings.Current.PlayerLootSharePercent));
                if (ReferenceEquals(Mission.Current, _killMission) && _killsHero + _killsTroop > 0)
                {
                    float pct = (_killsHero * 100f + _killsTroop * third) / (_killsHero + _killsTroop);
                    int r = Math.Max(0, Math.Min(100, (int)Math.Round(pct)));
                    Log.Info("BattlefieldLaw: powaleni przez partie gracza - Ty i towarzysze " + _killsHero + ", zolnierze " + _killsTroop
                             + " -> dzialka gracza " + r + "% (zolnierskie " + third + "%).");
                    return r;
                }
                int troops = 0;
                var roster = MobileParty.MainParty.MemberRoster;
                if (roster != null)
                    for (int i = 0; i < roster.Count; i++)
                    {
                        var el = roster.GetElementCopyAtIndex(i);
                        if (el.Character != null && !el.Character.IsHero) troops += el.Number;
                    }
                if (troops == 0) return 100;   // sam jestes druzyna - wszystko twoje
                return Math.Max(0, Math.Min(100, Settings.Current.PlayerLootSharePercent));
            }
            catch { return Settings.Current.PlayerLootSharePercent; }
        }

        /// <summary>
        /// Udzial w bagazach pokonanych (liczy go DTE na ekran vanilla) przenosimy
        /// do kolejki - trafi na ekran Spoils razem z dzialka z pola. Gdy Spoils
        /// nieobecny, kolejka jedzie prosto na ekran vanilla ze zuzyciem.
        /// </summary>
        public static void AfterCollectLoot(PartyBase winnerParty, ItemRoster gainedLoots)
        {
            try
            {
                if (!Settings.Current.BattlefieldLawEnabled) return;
                if (winnerParty == null || winnerParty != PartyBase.MainParty) return;
                if (gainedLoots == null) return;
                if (!RealFoughtBattle() || RotEnlisted())
                {
                    // recenzja E8/L: nieodebrana dzialka z poprzedniego "Send troops" (do godziny w kolejce) zostaje - dotad to czyszczenie wyrzucalo ja w nicosc
                    if (SimShareWaits())
                    {
                        Log.Info("BattlefieldLaw (E8/L): rozliczenie nastepnej bitwy bez misji - kolejka lupow nie jest czyszczona, czeka w niej dzialka z poprzedniego Send troops ("
                                 + ShareQueue.Count + " pozycji); odbierze ja ekran lupow albo sakwy po godzinie.");
                        return;
                    }
                    LootTally.QueueOut(ShareQueue, LootTally.Przepadlo);   // m109-lup: pomiar
                    ShareQueue.Clear(); ClearWrecks(); ClearB(); LootWear.Close("przepadlo - rozliczenie bitwy bez misji", 0, 0); return;
                }

                if (_rlPresent)
                {
                    // Spoils obsluzy ekran - zabieramy udzial w bagazach do kolejki
                    if (gainedLoots.Count > 0)
                    {
                        for (int i = 0; i < gainedLoots.Count; i++)
                        {
                            var el = gainedLoots.GetElementCopyAtIndex(i);
                            LootTally.Queued(el.EquipmentElement.Item, el.Amount, LootTally.Bagaz);   // m109-lup: pomiar (przed dopisaniem)
                            ShareQueue.AddToCounts(el.EquipmentElement, el.Amount);
                        }
                        gainedLoots.Clear();
                    }
                    return;
                }

                // bez Spoils: dzialka gracza na ekran vanilla, ze zuzyciem jesli mamy czym
                if (ShareQueue.Count > 0)
                {
                    for (int i = 0; i < ShareQueue.Count; i++)
                    {
                        var el = ShareQueue.GetElementCopyAtIndex(i);
                        gainedLoots.AddToCounts(el.EquipmentElement, el.Amount);
                    }
                    LootTally.QueueOut(ShareQueue, LootTally.Lista);   // m109-lup: pomiar
                    ShareQueue.Clear(); _simPending = false;   // recenzja E8/L: kolejka oddana na ekran gry
                }
                ClearB();   // m110-stan: bez Spoils nikt nie losuje stanu - sztuki B maja arm_* (GearState.LootMod)
                AppendWrecks(gainedLoots);
                CleanseDragons(gainedLoots);
                CleanseTrash(gainedLoots);
                LootTally.Close("ekran gry");   // m109-lup: bez Spoils lista trofeow to ekran gry
                LootWear.Close("ekran gry", 0, 0);
                if (!Settings.Current.LootArrivesBattleWorn || _applyDamage == null || gainedLoots.Count == 0) return;
                var damaged = _applyDamage.Invoke(null, new object[] { gainedLoots, 0f }) as ItemRoster;
                if (damaged == null || ReferenceEquals(damaged, gainedLoots)) return;
                gainedLoots.Clear();
                for (int i = 0; i < damaged.Count; i++)
                {
                    var el = damaged.GetElementCopyAtIndex(i);
                    gainedLoots.AddToCounts(el.EquipmentElement, el.Amount);
                }
            }
            catch (Exception e) { Log.Error("AfterCollectLoot", e); }
        }

        /// <summary>
        /// Prefix na MapEvent.LootDefeatedPartyCasualties: w bitwie osobistej
        /// z DTE szablonowa loteria NIE biegnie (dla zadnej partii - sojusznicy
        /// AI dostaja swoje fizycznie przez rekordy DTE). Symulacje i bitwy
        /// bez DTE: vanilla po staremu.
        /// </summary>
        public static bool CutCasualtyLootPrefix(MapEvent __instance)
        {
            try
            {
                if (!CasualtyLootCut || !Settings.Current.BattlefieldLawEnabled) return true;
                // E8/L (252, PLAN 8.6c): "Send troops" wygrane przez gracza - za poleglych i rannych pokonanych placi DTE (DistributePlayerSimulationLoot:
                // prawdziwy sprzet do zbrojowni zwyciezcow), wiec drugi komplet z szablonow nie powstaje - dla zadnej partii tej bitwy, jak w bitwie stoczonej
                if (SimOneLoot(__instance))
                {
                    _simCutGame++;
                    Log.Info("BattlefieldLaw (E8/L): Send troops - szablonowa loteria gry za poleglych/rannych pominieta (placi lup DTE z cial).");
                    return false;
                }
                if (!RealFoughtBattle()) return true;
                Log.Info("BattlefieldLaw: szablonowa loteria za poleglych/rannych pominieta (placi scena DTE i jency).");
                return false;
            }
            catch { return true; }
        }

        // ------------------------------------------------------------------ E8/L (252, PLAN 8.6c): "Send troops" - jedna droga lupu
        private static int _simCutSpoils, _simCutBattles, _simCutGame, _lStumbles;
        internal static int LStumbles { get { return _lStumbles; } }
        /// <summary>Lista trofeow Spoils ostatniej bitwy to prawdziwy lup z pola (podmieniona w AfterGenerateLoot), nie loteria Spoils - tylko taka
        /// reszta zostawiona przy "Leave" idzie na polke warowni (SpoilsSeal.RemnantPostfix); loteria z niczego przepada jak dotad.</summary>
        internal static bool ScreenIsRealLoot;
        /// <summary>Recenzja E8/L: w kolejce lezy nieodebrana dzialka z "Send troops" (sztuki zdjete juz ze zbrojowni wojska). Po oblezeniu i bitwie morskiej
        /// ekran Spoils nie przychodzi, wiec dzialka czeka do godziny na termin "do sakw" - a rozliczenie NASTEPNEJ bitwy bez misji czyscilo kolejke
        /// (AfterCollectLoot) i te sztuki szly w nicosc (np. wypad zalogi odparty przez "Send troops", zaraz potem szturm przez "Send troops"). Dopoki
        /// flaga stoi, tamto czyszczenie jest pomijane - kolejke odbierze ekran lupow albo termin godzinny. Gasnie tam, gdzie kolejka jest oddawana.</summary>
        private static bool _simPending;

        /// <summary>Czy w kolejce czeka nieodebrana dzialka z "Send troops" (wtedy rozliczenie nastepnej bitwy bez misji kolejki nie czysci).</summary>
        private static bool SimShareWaits()
        {
            if (_simPending && ShareQueue.Count == 0) _simPending = false;
            return _simPending;
        }

        /// <summary>Nowa gra albo wczytanie (HorseFate.Reset z konstruktora ArmouryBehavior): liczniki E8/L od zera. Recenzja E8/L: takze kolejka lupow
        /// (statyczna, bez zapisu) - dzialka czekajaca na ekran w jednej kampanii nie moze wyjsc na ekranie lupow innej (od E8/L czeka w niej do godziny
        /// po kazdym "Send troops" bez ekranu Spoils).</summary>
        internal static void LReset()
        {
            _simCutSpoils = _simCutBattles = _simCutGame = _lStumbles = 0; ScreenIsRealLoot = false;
            _simPending = false;
            try { LootTally.Reset(); } catch { }   // m109-lup: PRZED czyszczeniem kolejki (otwarta bitwa zamknieta, "zostaje" = kolejka)
            try { LootWear.Reset(); } catch { }    // m110-stan: otwarta bitwa stanu lupu zamknieta, ksiega misji od zera
            try { ShareQueue.Clear(); ClearWrecks(); MountHold.Clear(); ClearArmoryCopies(); ClearB(); _soundFlushed.Clear(); lock (_lootHero) TakePoolsLocked(); } catch { }
            _mountArmed = false; _screenThisCall = false;
        }

        /// <summary>m109-lup: rodzaj zabojcy dla pomiaru LootTally - 1 Ty albo towarzysz (kon zamieniony na jezdzca), 2 zolnierz partii gracza, 3 inni.</summary>
        internal static int KillerKindOf(Agent a) { return KillerKind(a); }

        /// <summary>Liczniki doby do linii "Lup i dary (E8/L)" (HorseFate.LLine) - zwraca tekst i zeruje.</summary>
        internal static string TakeSimCut()
        {
            string r = "loteria Spoils pominieta w " + _simCutBattles + " bitwach (" + _simCutSpoils + " szt. z niczego), szablonowa loteria gry pominieta " + _simCutGame + " razy";
            _simCutSpoils = _simCutBattles = _simCutGame = 0;
            return r;
        }

        /// <summary>
        /// Czy to bitwa gracza rozstrzygnieta przez "Send troops" i wygrana, za ktora DTE zaplaci lupem z poleglych (dekompilacja DTE v1.4.7
        /// EveryoneCampaignBehavior.OnMapEventEnded :444-464 - te same warunki: zwyciestwo, nie odwrot, nie rozejscie sie okretow, nie kryjowka wyslana
        /// rozkazem). Tylko wtedy tniemy loterie (Spoils i szablonowa gry) - inaczej za poleglych nie zaplacilby nikt. Sluga w cudzej armii (ROT) - jak dotad.
        /// </summary>
        internal static bool SimOneLoot(MapEvent me)
        {
            try
            {
                var s = Settings.Current;
                if (me == null || s == null || !s.SendTroopsOneLoot || !s.BattlefieldLawEnabled) return false;
                if (!CasualtyLootCut || _dteArmory == null || !HorseFate.DteWindowWired) return false;
                if (!me.IsPlayerMapEvent || !me.IsPlayerSimulation) return false;
                if (me.BattleState != BattleState.AttackerVictory && me.BattleState != BattleState.DefenderVictory) return false;
                if (me.WinningSide != me.PlayerSide || me.EndedByRetreat) return false;
                var pe = PlayerEncounter.Current;
                if (pe != null && (pe.IsNavalEncounterFinishedWithDisengage || pe.ForceHideoutSendTroops)) return false;
                return !RotEnlisted();
            }
            catch { _lStumbles++; return false; }
        }

        /// <summary>Dzialka kapitana w bitwie bez misji: stala trzecia (PlayerLootSharePercent - nie wiadomo, kto kogo powalil); sam w druzynie - wszystko.</summary>
        private static int SimSharePercent()
        {
            int third = Math.Max(0, Math.Min(100, Settings.Current.PlayerLootSharePercent));
            int troops = 0;
            var roster = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster : null;
            if (roster != null)
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.Character != null && !el.Character.IsHero) troops += el.Number;
                }
            return troops == 0 ? 100 : third;
        }

        /// <summary>
        /// HorseFate.SimShare (postfiks okna DTE): gained = przybytek zbrojowni druzyny gracza z lupu "Send troops" (przedmiot -> ile). Dzialka kapitana
        /// z kazdej pozycji (zaokraglenie jak w bitwie stoczonej - od zera) schodzi ze zbrojowni do kolejki na ekran lupow: najpierw sztuki bez stanu (te
        /// dopisal przed chwila DTE), potem pozostale tej samej nazwy. Kolejke odbiera ekran Spoils (bitwa polowa) albo po godzinie sakwy gracza
        /// (ArmouryBehavior.LootFlushIn - nasz sluchacz MapEventEnded biegnie PRZED oknem DTE i terminu sam by nie ustawil). Zwraca liczbe sztuk w kolejce.
        /// </summary>
        internal static int QueueSimShare(Dictionary<ItemObject, int> gained, int total)
        {
            int moved = 0, pct = 0;
            try
            {
                var armory = _dteArmory != null ? _dteArmory.GetValue(null) as ItemRoster : null;
                if (armory == null || gained == null || total <= 0)
                {
                    Log.Info("BattlefieldLaw (E8/L): Send troops - DTE nie dopisal lupu do zbrojowni wojska (0 szt.), dzialki nie ma.");
                    return 0;
                }
                pct = SimSharePercent();
                LootTally.SimBody(gained);   // m109-lup: pomiar
                foreach (var kv in gained)
                {
                    if (kv.Key == null || kv.Value <= 0) continue;
                    int left = Math.Min(kv.Value, (int)Math.Round(kv.Value * pct / 100.0, MidpointRounding.AwayFromZero));
                    for (int pass = 0; pass < 2 && left > 0; pass++)
                        for (int i = armory.Count - 1; i >= 0 && left > 0; i--)
                        {
                            var el = armory.GetElementCopyAtIndex(i);
                            if (el.EquipmentElement.Item != kv.Key || el.Amount <= 0) continue;
                            if (pass == 0 && el.EquipmentElement.ItemModifier != null) continue;
                            int n = Math.Min(left, el.Amount);
                            armory.AddToCounts(el.EquipmentElement, -n);
                            LootTally.Queued(kv.Key, n, LootTally.Send);   // m109-lup: pomiar (przed dopisaniem)
                            ShareQueue.AddToCounts(el.EquipmentElement, n);
                            left -= n; moved += n;
                        }
                }
                Log.Info("BattlefieldLaw (E8/L): Send troops - lup DTE z poleglych " + total + " szt. w zbrojowni wojska, dzialka kapitana " + pct + "% = " + moved
                         + " szt. w kolejce lupow - ekran Spoils albo sakwy (reszta zostaje ludziom); loterie Spoils i gry za tych samych poleglych pominiete.");
            }
            catch (Exception e) { _lStumbles++; Log.Error("QueueSimShare", e); }
            // recenzja E8/L: poza try - takze gdy wyjatek przerwal zdejmowanie w polowie, sztuki juz zdjete maja termin i ochrone przed czyszczeniem kolejki
            if (moved > 0) { _simPending = true; ArmouryBehavior.LootFlushIn(1f); }
            return moved;
        }

        /// <summary>
        /// Recenzja E8/L (HorseFate.SimShare, zaraz po QueueSimShare): ekran lupow Spoils przychodzi tylko po bitwie polowej na ladzie (dekompilacja Spoils
        /// 1.8.4: SkipVanillaLootPatch i LootCollectionBehavior.OnMapEventEnded - oblezenie i bitwe morska oddaje grze, a ekran gry byl PRZED koncem bitwy).
        /// Po takiej bitwie dzialka idzie do sakw od razu, a nie po godzinie: kolejka jest statyczna i bez zapisu, wiec zapis i wczytanie w tej godzinie
        /// (w zdobytym zamku czas stoi) gubily sztuki zdjete juz ze zbrojowni wojska. Po bitwie polowej bez zmian - ekran Spoils albo termin godzinny.
        /// </summary>
        internal static void SimShareNoScreen(MapEvent me)
        {
            try
            {
                if (!SimShareWaits()) return;
                if (_rlPresent && me != null && me.IsFieldBattle && !me.IsNavalMapEvent) return;
                FlushShareToBaggage("Send troops - ekran lupow Spoils po tej bitwie nie przychodzi (oblezenie, morze)");
            }
            catch (Exception e) { _lStumbles++; Log.Error("SimShareNoScreen", e); }
        }

        /// <summary>Czy agent nalezy do partii gracza (po Origin, przez refleksje -
        /// typy originow roznia sie miedzy trybami misji).</summary>
        internal static bool IsMainPartyAgent(Agent agent)
        {
            try
            {
                var origin = agent != null ? agent.Origin : null;
                if (origin == null) return false;
                var p = origin.GetType().GetProperty("Party");
                var party = p != null ? p.GetValue(origin, null) as PartyBase : null;
                if (party != null) return party == PartyBase.MainParty;
                var b = origin.GetType().GetProperty("BattleCombatant");
                return b != null && ReferenceEquals(b.GetValue(origin, null), PartyBase.MainParty);
            }
            catch { return false; }
        }

        /// <summary>Tabor Spoils zawsze OFF - wozy pokonanych dzieli vanilla
        /// wg wkladu, drugi ekran bagazy to byl dubel materii.</summary>
        public static void NoBaggageTrain(ref bool __result)
        {
            try { if (Settings.Current.BattlefieldLawEnabled) __result = false; } catch { }
        }

        /// <summary>m114-lup: drugi podzial Spoils (Priority) zawsze OFF przy prawie pola bitwy - cala lista trofeow na ekran, nic po cichu do sakw.
        /// Bez ScreenIsRealLoot: LReset czysci ja przy wczytaniu, a zbieranie Spoils i BuildLootScreenRoster ida pozniej.</summary>
        public static void NoSoldierSplit(ref bool __result)
        {
            try { if (Settings.Current.BattlefieldLawEnabled && Settings.Current.SpoilsSoldierSplitOff) __result = false; } catch { }
        }

        /// <summary>Symulacje: trup mial sprzet, to sprzet jest - bez zaszytego x0.10-0.58.</summary>
        public static void FullTierMultiplier(ref float __result)
        {
            try { if (Settings.Current.BattlefieldLawEnabled && Settings.Current.SimBattleFullDrop) __result = 1f; }
            catch { }
        }

        // ------------------------------------------------------------------ wraki

        /// <summary>m109-lup Z2 (RiderBlowSparesHarness): prefiks DTE ArmorSelector.GetRandomArmorByBodyPart(armors, bodyPart) - lista bez uprzezy konia
        /// (kopia; oryginal DTE uzywa dalej do "dodatkowo peknietej", ktora i tak pomija uprzaz). Cios rozbija czesc zbroi czlowieka albo nic, gdy ten nie
        /// ma zbroi na trafionej czesci ciala. Ta sama liczba sztuk; wrakiem staje sie zbroja jezdzca, ladry przychodza cale.</summary>
        public static void SpareHarness(ref List<ItemObject> __0)
        {
            try
            {
                var s = Settings.Current;
                if (__0 == null || s == null || !s.RiderBlowSparesHarness || !s.BattlefieldLawEnabled) return;
                bool any = false;
                foreach (var it in __0) if (it != null && it.ItemType == ItemObject.ItemTypeEnum.HorseHarness) { any = true; break; }
                if (!any) return;
                var copy = new List<ItemObject>(__0.Count);
                foreach (var it in __0) if (it != null && it.ItemType != ItemObject.ItemTypeEnum.HorseHarness) copy.Add(it);
                __0 = copy;
            }
            catch (Exception e) { MountStumble("SpareHarness", e); }
        }

        /// <summary>Postfix na selektorze DTE - zapamietuje, ktora czesc uznano za rozbita.</summary>
        public static void RecordStruckPiece(object __result)
        {
            try { _lastPick = __result as ItemObject; } catch { _lastPick = null; }
        }

        /// <summary>Po usunieciu agenta: rozbita czesc idzie do worka z wrakami -
        /// ale TYLKO gdy zabojca byl z PARTII GRACZA (Jeff 30.08: przy
        /// sojusznikach zbieralismy wraki z CALEGO pola, takze z cudzych
        /// zabojstw - kazdemu jego lup).</summary>
        public static void QueueWreck(Agent affectedAgent, Agent affectorAgent)
        {
            try
            {
                // m117 (zapas): wlasne ubranie cywilne agenta to nie sztuka zbrojowni - nie ma z niego wraka (latka Civvies.ArmorsPostfix i tak
                // wyjmuje je z listy zbroi DTE; tu tylko, gdyby dla tego agenta nie zadzialala)
                if (_lastPick != null && Civvies.IsWorn(affectedAgent, _lastPick)) { Civvies.NoteWreckStopped(); _lastPick = null; }
                // Jeff 04.10 ("ma byc historycznie"): rozbita czesc WLASNEGO poleglego tez zostaje na polu jako wrak -
                // kto utrzyma pole, ten ja zbiera (przegrana: DropShare czysci worek). Wczesniej znikala.
                if (_lastPick != null && Settings.Current.WreckSalvageEnabled &&
                    Settings.Current.BattlefieldLawEnabled && Wrecks.Count < 400 &&
                    (IsMainPartyAgent(affectorAgent) || IsMainPartyAgent(affectedAgent)))
                {
                    // m110-stan (LootStateFromWounds): czesc rozbita ciosem wroga powalonego przez druzyne gracza - stan z obrazen (nie z automatu wrak)
                    LootWear.PieceData pd = null;
                    if (LootWear.On)
                    {
                        int k = KillerKind(affectorAgent);
                        if (k == 1 || k == 2)
                        {
                            pd = LootWear.Piece(affectedAgent, _lastPick, -1, LootWear.CCios, k, LootWear.WhoOf(k, affectorAgent));
                            if (pd == null && affectedAgent != null && Mission.Current != null && MissionTeams.Enemies(affectedAgent.Team, Mission.Current.PlayerTeam)) LootWear.NoBook(true);
                        }
                    }
                    Wrecks.Add(_lastPick);
                    WreckData.Add(pd);
                }
            }
            catch { }
            _lastPick = null;
        }

        /// <summary>m109-lup Z1 (ExtraBrokenArmorWrecks, wolane z LootTally.FelledPost): "dodatkowo peknieta" czesc zbroi wroga powalonego przez druzyne
        /// gracza do worka z wrakami - te same bramki co QueueWreck (klucze, limit 400) i tylko, gdy znamy stan wraka (modyfikator Spoils wpiety przy starcie
        /// z WreckSalvageEnabled) - bez niego wrak przyszedlby jako cala sztuka. DTE tej sztuki nie dopisuje nigdzie (AddItemToRecover ja pomija), wiec wrak
        /// to jedyna kopia. true - dodany.</summary>
        internal static bool AddWreck(ItemObject it) { return AddWreck(it, null); }

        /// <summary>m110-stan: jak AddWreck - z danymi czesci (stan z obrazen; null - bez ksiegi) rownolegle do Wrecks.</summary>
        internal static bool AddWreck(ItemObject it, LootWear.PieceData pd)
        {
            try
            {
                var s = Settings.Current;
                if (it == null || s == null || !s.WreckSalvageEnabled || !s.BattlefieldLawEnabled || Wrecks.Count >= 400) return false;
                if (_rlHeavy == null && _rlHeavyMax == null) return false;
                Wrecks.Add(it);
                WreckData.Add(pd);
                return true;
            }
            catch (Exception e) { MountStumble("AddWreck", e); return false; }
        }

        /// <summary>Najciezszy modyfikator zuzycia Spoils - stan wraka.</summary>
        private static ItemModifier WreckModifier()
        {
            try
            {
                if (_rlEnsureCache != null) _rlEnsureCache.Invoke(null, null);
                // audyt 04.10: wrak dostawal heavy_max (3%) i prog zniszczenia (Jeff 28.08: "<=3% = zniszczone") kasowal
                // KAZDY wrak - funkcja byla martwa (w logach 8/8 "zostaly na polu"). Wrak = ciezko uszkodzony (8%), do przetopu.
                // wpis 97: cena lupu = stan - wrakiem jest "Mangled" (10%); "Battered" (25%) to juz zwykle zuzycie
                var m = _rlHeavyMax != null ? _rlHeavyMax.GetValue(null) as ItemModifier : null;
                if (m == null && _rlHeavy != null) m = _rlHeavy.GetValue(null) as ItemModifier;
                return m;
            }
            catch { return null; }
        }

        /// <summary>Dosypuje wraki do rostera (z modyfikatorem i ochrona przed ponownym losowaniem zuzycia).</summary>
        /// <summary>
        /// Pole opuszczone - kolejka lupu idzie do kosza. Zwraca, ile sztuk
        /// przepadlo (dzialka z magazynu wraca tam, skad ja wzielismy).
        /// </summary>
        internal static int DropShare()
        {
            int n = 0;
            try
            {
                n = ShareQueue.Count + Wrecks.Count;
                // to, co wyjelismy z magazynu wojska na ekran lupow, wraca na polke
                var armory = _dteArmory != null ? _dteArmory.GetValue(null) as ItemRoster : null;
                if (armory != null)
                {
                    // m110-stan: sztuki B wracaja jako ich oryginaly ze zbrojowni (bez stanu z obrazen); B z podzialu wrakow przepadaja jak wraki
                    var bq = new Dictionary<string, int>();
                    for (int i = 0; i < _bQueued.Count; i++)
                    {
                        var be = _bQueued.GetElementCopyAtIndex(i);
                        if (be.EquipmentElement.Item == null || be.Amount <= 0) continue;
                        string k = be.EquipmentElement.Item.StringId + "|" + (be.EquipmentElement.ItemModifier != null ? be.EquipmentElement.ItemModifier.StringId : "");
                        int v; bq.TryGetValue(k, out v); bq[k] = v + be.Amount;
                    }
                    for (int i = 0; i < ShareQueue.Count; i++)
                    {
                        var el = ShareQueue.GetElementCopyAtIndex(i);
                        int amt = el.Amount;
                        if (bq.Count > 0 && el.EquipmentElement.Item != null)
                        {
                            string k = el.EquipmentElement.Item.StringId + "|" + (el.EquipmentElement.ItemModifier != null ? el.EquipmentElement.ItemModifier.StringId : "");
                            int v;
                            if (bq.TryGetValue(k, out v) && v > 0) { int b = Math.Min(v, amt); amt -= b; bq[k] = v - b; }
                        }
                        if (amt > 0) armory.AddToCounts(el.EquipmentElement, amt);
                    }
                    for (int i = 0; i < _bOrig.Count; i++)
                    {
                        var oe = _bOrig.GetElementCopyAtIndex(i);
                        if (oe.EquipmentElement.Item != null && oe.Amount > 0) armory.AddToCounts(oe.EquipmentElement, oe.Amount);
                    }
                    LootTally.QueueOut(ShareQueue, LootTally.Zwrot);   // m109-lup: pomiar
                    // m109-lup: siatka bezpieczenstwa - przetrzymane konie i uprzaz (zdjete z kolejki, czyli ze zbrojowni wojska) wracaja tam samo
                    for (int i = 0; i < MountHold.Count; i++)
                    {
                        var el = MountHold.GetElementCopyAtIndex(i);
                        armory.AddToCounts(el.EquipmentElement, el.Amount);
                        LootTally.Reserve(el.EquipmentElement.Item, el.Amount);
                    }
                    MountHold.Clear(); _mountArmed = false;
                }
                else LootTally.QueueOut(ShareQueue, LootTally.Przepadlo);
                ShareQueue.Clear(); _simPending = false;   // recenzja E8/L: kolejka wrocila do zbrojowni wojska
                ClearWrecks(); ClearB();
            }
            catch (Exception e) { Log.Error("DropShare", e); }
            LootTally.Close("zwrot do zbrojowni wojska");   // m109-lup: pole przegrane albo opuszczone
            LootWear.Close("zwrot do zbrojowni wojska", 0, 0);   // m110-stan
            return n;
        }

        /// <summary>Czy dzialka gracza wciaz lezy w kolejce (ekran lupow nie przyszedl).</summary>
        internal static bool SharePending()
        {
            try { return ShareQueue.Count > 0 || Wrecks.Count > 0 || MountHold.Count > 0; } catch { return false; }   // m109-lup: takze przetrzymane konie
        }

        /// <summary>
        /// KRYJOWKI I INNE BITWY BEZ EKRANU LUPOW. Po zwyklej bitwie dzialka
        /// gracza czeka w kolejce na ekran Spoils - ale po zdobyciu kryjowki
        /// zaden ekran nie przychodzi i lupy wisialy w prozni (60 sztuk u Jeffa).
        /// Ta metoda wysypuje kolejke PROSTO DO SAKW: kazda sztuka dostaje stan
        /// bojowy, wraki jada jak wraki. Wolana od razu po kryjowce i godzine
        /// po kazdej bitwie, ktorej ekran sie nie upomnial.
        /// </summary>
        internal static int FlushShareToBaggage(string why)
        {
            // noc 25.10 P5: ramka F okna lupu wokol calego przelewu (tylko odczyt sakw przed i po; w ramce bitwy/Spoils nic nie liczy - liczy ramka zewnetrzna)
            var spoilsF = SpoilsReport.OpenF();
            try { return FlushShareToBaggageCore(why); }
            finally { SpoilsReport.CloseF(spoilsF, why); }
        }

        private static int FlushShareToBaggageCore(string why)
        {
            int moved = 0;
            int wOk = -1, wMod = 0;   // m114-lup: bron bez loterii stanu (linia m110)
            try
            {
                var bag = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
                if (bag == null) return 0;
                LootTally.QueueOut(ShareQueue, LootTally.Sakwy);   // m109-lup: pomiar
                if (Settings.Current.LootWeaponsKeepCondition) wOk = 0;
                for (int i = 0; i < ShareQueue.Count; i++)
                {
                    var el = ShareQueue.GetElementCopyAtIndex(i);
                    var item = el.EquipmentElement.Item;
                    if (item == null) continue;
                    for (int n = 0; n < el.Amount; n++)
                    {
                        var mod = el.EquipmentElement.ItemModifier;
                        if (wOk >= 0 && KeepsBodyState(item))
                        {
                            // m114-lup: bron w stanie z ciala - bez PickWornModifier; sprawna zapamietana, zeby WearTheLoot (kryjowka) tez jej nie losowal
                            wOk++;
                            if (mod != null) wMod++;
                            else { int f; _soundFlushed.TryGetValue(item, out f); _soundFlushed[item] = f + 1; }
                        }
                        else if (mod == null && !TakeBSound(item)) mod = ArmouryBehavior.PickWornModifier(item);   // m110-stan: sprawna sztuka B zostaje sprawna
                        bag.AddToCounts(new EquipmentElement(item, mod), 1);
                        moved++;
                    }
                }
                ShareQueue.Clear(); _simPending = false;   // recenzja E8/L: kolejka oddana do sakw
                moved += AppendWrecks(bag);
                if (MountHold.Count > 0) moved += FlushMountsToBaggage(why);   // m109-lup: siatka bezpieczenstwa (MountHold zyje tylko w wywolaniu Spoils)
                if (moved > 0)
                {
                    Log.Info("BattlefieldLaw: kolejka lupow wysypana do sakw (" + moved + " szt.) - " + why + ".");
                    Log.Player("The spoils are packed into your baggage: " + moved + " pieces of gear.", true);
                }
            }
            catch (Exception e) { Log.Error("FlushShareToBaggage", e); }
            ClearB();   // m110-stan: kolejka oddana (sprawne B wysypane - _soundFlushed dla WearTheLoot)
            LootTally.Close("sakwy - " + why);   // m109-lup: pomiar bitwy zamkniety
            LootWear.Close("sakwy - " + why, 0, 0, wOk, Math.Max(0, wOk), wMod);
            return moved;
        }

        private static int AppendWrecks(ItemRoster roster)
        {
            int n = 0;
            try
            {
                if (Wrecks.Count == 0) return 0;
                var mod = WreckModifier();
                // wraki zbite ponizej progu zniszczenia w ogole nie wchodza
                // (Jeff 28.08: "<=3% = zniszczone, nie pojawia sie w loocie")
                // m110-stan (recenzja R7): przy stanach wrak (0.05) ginie dopiero przy progu 10 i wiecej (jak 104 przy cenie 0.10); czesci z ksiega obrazen
                // zniszczone wedlug d juz w SplitWrecks (LootWear) - tu tylko wrak bez wiedzy o wytrzymalosci
                if (mod != null && Settings.Current.LootMinConditionPercent > 0
                    && (GearState.On ? GearState.BelowDestroy(mod, Settings.Current.LootMinConditionPercent)
                                     : mod.PriceMultiplier * 100f <= Settings.Current.LootMinConditionPercent + 0.01f))
                {
                    int junked = Wrecks.Count;
                    foreach (var w in Wrecks) LootTally.SiftOut(w, 1, LootTally.SWrak);   // m109-lup (recenzja L4): przyczyna przesiania
                    ClearWrecks();
                    Log.Info("BattlefieldLaw: " + junked + " wrakow ponizej progu zniszczenia - zostaly na polu.");
                    return 0;
                }
                foreach (var item in Wrecks)
                {
                    if (item == null) continue;
                    var el = new EquipmentElement(item, mod);
                    if (mod != null && _rlRegisterLucky != null)
                    {
                        // Spoils przy nakladaniu zuzycia zachowuje "zarejestrowane" elementy 1:1 -
                        // dzieki temu wrak zostaje wrakiem, a nie dostaje losowych 40-85%
                        try { _rlRegisterLucky.Invoke(null, new object[] { el }); } catch { }
                    }
                    roster.AddToCounts(el, 1);
                    n++;
                }
                ClearWrecks();
            }
            catch (Exception e) { Log.Error("AppendWrecks", e); }
            return n;
        }
    }
}
