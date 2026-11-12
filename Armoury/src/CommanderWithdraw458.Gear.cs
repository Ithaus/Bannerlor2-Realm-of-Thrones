using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 458-u - sprzet zolnierzy, ktorzy USZLI Z WODZEM (Jeff 12.11: "jak uciekli to nie uciekli nago tylko w sprzecie ktorym walczyli"). Klucz Commander
    /// Withdraws Under Rearguard (NIE - nic sie nie zapisuje, latki wychodza przy pierwszym porownaniu).
    ///
    /// DTE (dekompilacja bad3/dec/dte.cs, DynamicTroopMissionLogic):
    ///  - Spawn (6248): sprzet wystawionego schodzi ze zbrojowni partii (PartyArmories) przy wystawieniu.
    ///  - OnAgentRemoved (2617): zabity / nieprzytomny (stan 3-4) - kazda sztuka (bez rozbitej ciosem i "dodatkowo peknietej") do ItemsToRecover partii
    ///    ofiary, a z DropRate KOPIA do LootedItems partii zabojcy (2770-2800); zbiegly / usuniety (Routed / Deleted) przed koncem misji - kazda sztuka
    ///    (ProcessAgentEquipmentRespectingTemporarySlots: bez slotow tymczasowych, rozbitych tarcz, pustych kolczanow, choragwi) do ItemsToRecover swojej
    ///    partii (2703-2728), do zwyciezcy nic.
    ///  - FinalizeMission (2857-2945): HandlePartyItems (3026) - zwyciezca: ItemsToRecover + LootedItems do zbrojowni; strona bez rozstrzygniecia:
    ///    ItemsToRecover; POKONANY (isDefeated): NIC - caly ItemsToRecover (polegli, ranni, zbiegli) przepada. Potem ReturnEquipmentFromAgents (2911-2943):
    ///    kazdy czynny jeszcze czlowiek partii z rozdzielaczem oddaje sprzet do zbrojowni - takze pokonany.
    ///  - ReturnItemsToDestination (3047): partia AI tylko z rozdzielaczem (Distributors) - ReturnItem -> EveryoneCampaignBehavior.AddItemToPartyArmory (6306, 3992).
    /// Wiec sprzet zbieglych z wodzem (usunietych z pola jako Routed przed koncem misji) ginal: nie szedl do zwyciezcy (nie bylo kopii), nie wracal.
    ///
    /// ZMIANA: prefiks + postfiks DTE OnAgentRemoved - dla zbieglego z wodzem (ten sam warunek co licznik "zbiegli z wodzem" misji 458: wyprowadzany, Routed /
    /// Deleted, morale >= 0.01, szeregowy z przydzialem DTE) roznica ItemsToRecover jego partii = dokladnie sztuki, ktore DTE z niego zapisal. Kon i uprzaz tylko,
    /// gdy z pola zszedl z nim jego WLASNY kon ze spawnu (458-u-p, recenzja U1: rozstrzygane przy OnEndMission 458 - kon nie zabity i jezdziec siedzial na
    /// nim przy usunieciu albo kon zdjety z pola zywy najwyzej 1 s od jezdzca; kolejnosc odpiecia konia przy FadeOut ustala silnik, a cudzy kon dosiadziety
    /// po stracie wlasnego to nie jego sztuka); kon padl / zostal - sztuki nie wracaja (HorseFate liczy padlego jako zgon jezdzca pokonanego, bez zdjecia).
    /// Prefiks HandlePartyItems zapisuje decyzje DTE dla partii (isDefeated).
    /// Na mapie (CapPre, po Settle partii z wodzem) - tylko partia, ktora zostaje z wodzem, i tylko gdy DTE jej nie oddal (isDefeated TAK): sztuki do
    /// zbrojowni ta sama metoda DTE co ReturnItem (AiGear.AddToArmory -> AddItemToPartyArmory). Sprzet rannych, poleglych i pojmanych - bez zmian.
    /// Kontrola w linii: zapisane z agentow = wrocilo + DTE nie przyjal + kon/uprzaz nie zeszly z nim + partie jak w grze + DTE oddal sam + DTE nie rozliczylo.
    /// Zapis porzucony bez linii kontroli (klucz NIE w trakcie, strona nie pokonana, potkniecie CapPre, nowa misja / kampania) - linia "zapis porzucony" (U2).
    /// </summary>
    internal static partial class CommanderWithdraw458Map
    {
        internal sealed class Gear
        {
            public PartyBase Pb;
            public MBGUID Id;
            public bool HasId, Done, Kept;
            public int Dte = -1;   // HandlePartyItems: -1 nie wolane (partia bez rozdzielacza DTE), 0 isDefeated NIE (DTE oddal sam), 1 isDefeated TAK (DTE nie oddal)
            public int Men, MenRec, MenNoRec, MenLate, MenNoAsg;
            public long Pieces, Horses, HorseOff, Back, HorsesBack, Refused, HorseApart;
            public readonly Dictionary<ItemObject, int> Items = new Dictionary<ItemObject, int>();
            public readonly List<HorsePend> Pend = new List<HorsePend>();   // 458-u-p: kon/uprzaz czekaja na rozstrzygniecie przy OnEndMission
        }

        internal sealed class HorsePend
        {
            public Agent Own;
            public float T;
            public bool MountedOwn;
            public ItemObject Horse, Harness;
        }

        internal sealed class GearSnap
        {
            public Gear G;
            public MBGUID Id;
            public Dictionary<ItemObject, int> Before;
            public Agent Own;          // 458-u-p: wlasny kon ze spawnu (zapis 458 OnAgentBuild)
            public bool MountedOwn;    // siedzial na WLASNYM koniu przy usunieciu
            public float T;
            public ItemObject Horse, Harness;
        }

        private static CommanderWithdraw458 _gearLive;   // misja z odwrotem na rozkaz (StartRule) - do OnEndMission
        private static MapEvent _gearMe;
        private static readonly Dictionary<PartyBase, Gear> _gear = new Dictionary<PartyBase, Gear>();
        private static FieldInfo _gRecs, _gRecover, _gEnded, _gAssign;
        private static string _hookGear = "nie wpieta";

        /// <summary>Latki (z TaborSessionStart, raz na proces): DTE DynamicTroopMissionLogic.OnAgentRemoved (prefiks + postfiks) i HandlePartyItems (prefiks);
        /// metody zadeklarowane w DynamicTroopMissionLogic - DeclaredMethod.</summary>
        private static void GearPatch()
        {
            try
            {
                var t = AccessTools.TypeByName("DynamicTroopEquipmentReupload.DynamicTroopMissionLogic");
                var tr = AccessTools.TypeByName("DynamicTroopEquipmentReupload.PartyBattleRecord");
                _gRecs = t != null ? AccessTools.DeclaredField(t, "_partyBattleRecords") : null;
                _gEnded = t != null ? AccessTools.DeclaredField(t, "_isMissionEnded") : null;
                _gAssign = t != null ? AccessTools.DeclaredField(t, "_assignmentByAgent") : null;
                _gRecover = tr != null ? AccessTools.DeclaredField(tr, "ItemsToRecover") : null;
                var rem = t != null ? AccessTools.DeclaredMethod(t, "OnAgentRemoved", new[] { typeof(Agent), typeof(Agent), typeof(AgentState), typeof(KillingBlow) }) : null;
                var hpi = t != null ? AccessTools.DeclaredMethod(t, "HandlePartyItems") : null;
                if (t == null) _hookGear = "DTE nieobecny";
                else if (rem == null || hpi == null || _gRecs == null || _gRecover == null || _gEnded == null || _gAssign == null)
                    _hookGear = "BRAK (" + (rem == null ? "OnAgentRemoved " : "") + (hpi == null ? "HandlePartyItems " : "") + (_gRecs == null ? "_partyBattleRecords " : "")
                                + (_gRecover == null ? "ItemsToRecover " : "") + (_gEnded == null ? "_isMissionEnded " : "") + (_gAssign == null ? "_assignmentByAgent" : "") + ")";
                else
                {
                    _hT.Patch(rem, prefix: new HarmonyMethod(typeof(CommanderWithdraw458Map), nameof(GearRemPre)) { priority = Priority.First },
                              postfix: new HarmonyMethod(typeof(CommanderWithdraw458Map), nameof(GearRemPost)) { priority = Priority.Last });
                    _hT.Patch(hpi, prefix: new HarmonyMethod(typeof(CommanderWithdraw458Map), nameof(GearHandlePre)) { priority = Priority.First });
                    _hookGear = "wpieta";
                }
            }
            catch (Exception e) { _hookGear = "blad " + e.GetType().Name; Log.Error("CommanderWithdraw458Map.GearPatch", e); }
        }

        /// <summary>Misja: regula ruszyla (StartRule) - od teraz zapis sprzetu zbieglych z wodzem tej bitwy.</summary>
        internal static void GearStart(CommanderWithdraw458 live, MapEvent me)
        {
            _gearLive = live;
            _gearMe = me;
            _gear.Clear();
        }

        /// <summary>Misja skonczona (OnEndMission 458): zapis zamkniety, zebrany sprzet czeka na mape.</summary>
        internal static void GearStop(CommanderWithdraw458 live)
        {
            if (!ReferenceEquals(_gearLive, live)) return;
            GearResolve(live);
            _gearLive = null;
        }

        /// <summary>458-u-p (U1): kon i uprzaz zbieglych z wodzem wedlug losu WLASNEGO konia (live - zapis misji; null - misja bez OnEndMission: nie wracaja).</summary>
        private static void GearResolve(CommanderWithdraw458 live)
        {
            foreach (var g in _gear.Values)
            {
                foreach (var hp in g.Pend)
                {
                    int left = 0;
                    try { left = live != null ? live.GearMountLeft(hp.Own, hp.T, hp.MountedOwn) : 0; }
                    catch (Exception e) { left = 0; Stumble("Sprzet (kon)", e); }
                    int n = (hp.Horse != null ? 1 : 0) + (hp.Harness != null ? 1 : 0);
                    if (left == 0) { g.HorseOff += n; continue; }
                    if (left == 2) g.HorseApart++;
                    int k;
                    if (hp.Horse != null)
                    {
                        g.Items.TryGetValue(hp.Horse, out k); g.Items[hp.Horse] = k + 1;
                        if (hp.Horse.ItemType == ItemObject.ItemTypeEnum.Horse) g.Horses++;
                    }
                    if (hp.Harness != null) { g.Items.TryGetValue(hp.Harness, out k); g.Items[hp.Harness] = k + 1; }
                }
                g.Pend.Clear();
            }
        }

        private static void GearClear(string why)
        {
            GearDrop(why);
            _gearLive = null; _gearMe = null; _gear.Clear();
        }

        /// <summary>458-u-p (recenzja U2): zapis sprzetu porzucony bez linii kontroli - jedna linia z powodem (zachowanie jak przed 458-u: sztuki bez zwrotu
        /// przepadaja w rozliczeniu DTE pokonanego; wczesniej juz zwrocone - zostaja).</summary>
        private static void GearDrop(string why)
        {
            if (_gear.Count == 0) return;
            try
            {
                int men = 0, pend = 0;
                long pieces = 0, back = 0, refused = 0, dteSelf = 0;
                foreach (var g in _gear.Values)
                {
                    men += g.Men; pieces += g.Pieces; back += g.Back; refused += g.Refused; pend += g.Pend.Count;
                    if (g.Dte == 0) dteSelf += g.Pieces;   // DTE nie uznal partii za pokonana - oddal ItemsToRecover sam (w tym kon i uprzaz)
                }
                Log.Info(P + "sprzet zbieglych z wodzem - zapis porzucony (" + why + "): zbiegli z wodzem w zapisie " + men + ", sztuk zapisanych przez DTE " + pieces
                         + ", wrocilo dotad " + back + (refused > 0 ? ", DTE nie przyjal " + refused : "") + (pend > 0 ? ", koni nierozstrzygnietych " + pend : "")
                         + ", DTE oddal sam " + dteSelf + ", bez zwrotu " + (pieces - back - refused - dteSelf) + " (jak przed 458-u).");
            }
            catch { }
        }

        /// <summary>Prefiks DTE OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow): zbiegly z wodzem - migawka ItemsToRecover jego partii.</summary>
        public static void GearRemPre(object __instance, Agent __0, AgentState __2, out GearSnap __state)
        {
            __state = null;
            var live = _gearLive;
            if (live == null || __0 == null || __instance == null) return;
            try
            {
                if (__2 != AgentState.Routed && __2 != AgentState.Deleted) return;
                if (!__0.IsHuman || !live.GearEscapee(__0)) return;
                var co = __0.Character as CharacterObject;
                if (co == null || co.IsHero) return;
                float mor;
                try { mor = __0.GetMorale(); } catch { mor = 0f; }
                if (mor < 0.01f) return;   // ten sam prog co Track 458 / SandBox BattleAgentLogic: wypisany w panice - nie zbiegl z wodzem
                var pb = __0.Origin != null ? __0.Origin.BattleCombatant as PartyBase : null;
                if (pb == null) return;
                Gear g;
                if (!_gear.TryGetValue(pb, out g))
                {
                    g = new Gear { Pb = pb };
                    if (pb.MobileParty != null) { g.Id = pb.MobileParty.Id; g.HasId = true; }
                    _gear[pb] = g;
                }
                g.Men++;
                if (!g.HasId) { g.MenNoRec++; return; }
                if ((bool)_gEnded.GetValue(__instance)) { g.MenLate++; return; }   // po rozliczeniu DTE - DTE nic nie zapisuje (sprzet oddal ReturnEquipmentFromAgents)
                var asg = _gAssign.GetValue(__instance) as IDictionary;
                if (asg == null || !asg.Contains(__0)) { g.MenNoAsg++; return; }   // bez przydzialu DTE - sprzet nie ze zbrojowni (zwrot bylby z niczego)
                var eq = __0.SpawnEquipment;
                var own = live.GearOwnMount(__0);
                var cur = __0.MountAgent;
                __state = new GearSnap
                {
                    G = g, Id = g.Id, Before = Recover(__instance, g.Id), Own = own, MountedOwn = own != null && ReferenceEquals(cur, own),
                    T = __0.Mission != null ? __0.Mission.CurrentTime : 0f,
                    Horse = eq != null ? eq[EquipmentIndex.Horse].Item : null, Harness = eq != null ? eq[EquipmentIndex.HorseHarness].Item : null
                };
            }
            catch (Exception e) { __state = null; Stumble("Sprzet (przed DTE)", e); }
        }

        /// <summary>Postfiks DTE OnAgentRemoved: roznica ItemsToRecover = sztuki, ktore DTE zapisal z tego czlowieka.</summary>
        public static void GearRemPost(object __instance, GearSnap __state)
        {
            if (__state == null) return;
            try
            {
                var g = __state.G;
                var after = Recover(__instance, __state.Id);
                var delta = new Dictionary<ItemObject, int>();
                long got = 0;
                foreach (var kv in after)
                {
                    int b;
                    __state.Before.TryGetValue(kv.Key, out b);
                    int d = kv.Value - b;
                    if (d > 0) { delta[kv.Key] = d; got += d; }
                }
                if (got == 0) { g.MenNoRec++; return; }
                g.MenRec++;
                g.Pieces += got;
                // 458-u-p (U1): kon i uprzaz czekaja na los WLASNEGO konia (GearResolve przy OnEndMission); wystawiony bez konia - nie zeszly z nim
                bool hTook = Take1(delta, __state.Horse) > 0, rTook = Take1(delta, __state.Harness) > 0;
                if (hTook || rTook)
                {
                    if (__state.Own == null) g.HorseOff += (hTook ? 1 : 0) + (rTook ? 1 : 0);
                    else g.Pend.Add(new HorsePend { Own = __state.Own, T = __state.T, MountedOwn = __state.MountedOwn, Horse = hTook ? __state.Horse : null, Harness = rTook ? __state.Harness : null });
                }
                foreach (var kv in delta)
                {
                    if (kv.Value <= 0) continue;
                    int n;
                    g.Items.TryGetValue(kv.Key, out n);
                    g.Items[kv.Key] = n + kv.Value;
                    if (kv.Key.ItemType == ItemObject.ItemTypeEnum.Horse) g.Horses += kv.Value;
                }
            }
            catch (Exception e) { Stumble("Sprzet (po DTE)", e); }
        }

        private static int Take1(Dictionary<ItemObject, int> d, ItemObject it)
        {
            int n;
            if (it == null || !d.TryGetValue(it, out n) || n <= 0) return 0;
            d[it] = n - 1;
            return 1;
        }

        private static Dictionary<ItemObject, int> Recover(object logic, MBGUID id)
        {
            var res = new Dictionary<ItemObject, int>();
            var recs = _gRecs.GetValue(logic) as IDictionary;
            object rec = recs != null && recs.Contains(id) ? recs[id] : null;
            var d = rec != null ? _gRecover.GetValue(rec) as ConcurrentDictionary<ItemObject, int> : null;
            if (d == null) return res;
            foreach (var kv in d) if (kv.Key != null && kv.Value > 0) res[kv.Key] = kv.Value;
            return res;
        }

        /// <summary>Prefiks DTE HandlePartyItems(partyId, isPlayerParty, isVictorious, isDefeated): decyzja DTE dla partii ze zbieglymi z wodzem.</summary>
        public static void GearHandlePre(MBGUID __0, bool __3)
        {
            if (_gear.Count == 0) return;
            try
            {
                foreach (var g in _gear.Values) if (g.HasId && g.Id == __0) g.Dte = __3 ? 1 : 0;
            }
            catch (Exception e) { Stumble("Sprzet (HandlePartyItems)", e); }
        }

        /// <summary>CapPre: partia zostaje z wodzem - sprzet jej zbieglych z wodzem do zbrojowni DTE (tylko gdy DTE go nie oddal: isDefeated TAK).</summary>
        private static void GearBack(MapEvent me, PartyBase pb)
        {
            if (pb == null || me == null || !ReferenceEquals(_gearMe, me)) return;
            Gear g;
            if (!_gear.TryGetValue(pb, out g) || g.Done) return;
            if (g.Pend.Count > 0) GearResolve(null);   // misja bez OnEndMission 458 (nie powinno sie zdarzyc) - konie nierozstrzygniete nie wracaja
            g.Done = true;
            g.Kept = true;
            if (g.Dte != 1) return;
            var mp = pb.MobileParty;
            if (mp == null) return;
            foreach (var kv in g.Items)
            {
                if (kv.Key == null || kv.Value <= 0) continue;
                bool ok = false;
                try { ok = AiGear.AddToArmory(mp, kv.Key, kv.Value); }
                catch (Exception e) { ok = false; Stumble("Sprzet (zwrot)", e); }
                if (ok)
                {
                    g.Back += kv.Value;
                    if (kv.Key.ItemType == ItemObject.ItemTypeEnum.Horse) g.HorsesBack += kv.Value;
                }
                else g.Refused += kv.Value;   // DTE AddItemToPartyArmory nie przyjal (czarna lista / nierozpoznana) - jak przy ReturnItem DTE
            }
        }

        /// <summary>Linia "sprzet zbieglych z wodzem wrocil do zbrojowni" z kontrola; zapis tej bitwy zamkniety.</summary>
        private static void GearLine(MapEvent me)
        {
            if (me == null || !ReferenceEquals(_gearMe, me)) return;
            try
            {
                if (_gear.Count == 0) return;
                GearResolve(null);   // nierozstrzygniete (bez OnEndMission 458) - nie wracaja; po GearStop pusto
                int men = 0, menAll = 0, noRec = 0, late = 0, noAsg = 0, parties = 0;
                long pieces = 0, back = 0, horsesBack = 0, refused = 0, horseOff = 0, game = 0, dteSelf = 0, noDte = 0, apart = 0;
                var names = new List<string>();
                foreach (var g in _gear.Values)
                {
                    menAll += g.Men; noRec += g.MenNoRec; late += g.MenLate; noAsg += g.MenNoAsg;
                    pieces += g.Pieces; horseOff += g.HorseOff; apart += g.HorseApart;
                    long rest = g.Pieces - g.HorseOff;
                    if (!g.Kept) { game += rest; names.Add(Name(g.Pb) + ": jak w grze (bez zwrotu) " + rest); continue; }
                    if (g.Dte == 0) { dteSelf += rest; names.Add(Name(g.Pb) + ": DTE oddal sam " + rest); continue; }
                    if (g.Dte != 1) { noDte += rest; names.Add(Name(g.Pb) + ": DTE nie rozliczylo partii " + rest); continue; }
                    parties++;
                    men += g.MenRec;
                    back += g.Back; horsesBack += g.HorsesBack; refused += g.Refused;
                    names.Add(Name(g.Pb) + ": ludzi " + g.MenRec + ", wrocilo " + g.Back + (g.Refused > 0 ? ", DTE nie przyjal " + g.Refused : ""));
                }
                bool ok = pieces == back + refused + horseOff + game + dteSelf + noDte;
                var sb = new StringBuilder(384);
                sb.Append(P).Append("sprzet zbieglych z wodzem wrocil do zbrojowni: ludzi ").Append(men).Append(", sztuk ").Append(back).Append(" (w tym koni ").Append(horsesBack).Append(", kon zdjety z pola osobno do 1 s od jezdzca ").Append(apart).Append(')')
                  .Append(", partii ").Append(parties)
                  .Append(" | kontrola: zapisane przez DTE z agentow ").Append(pieces).Append(" = wrocilo ").Append(back).Append(" + DTE nie przyjal ").Append(refused)
                  .Append(" + kon/uprzaz nie zeszly z nim (wlasny kon padl / zostal / wystawiony pieszo) ").Append(horseOff).Append(" + partie jak w grze (bez zwrotu) ").Append(game)
                  .Append(" + DTE oddal sam ").Append(dteSelf).Append(" + DTE nie rozliczylo partii ").Append(noDte).Append(ok ? " OK" : " NIEZGODNE")
                  .Append(" | zbiegli z wodzem w zapisie: ").Append(menAll).Append(" (bez sztuk w zapisie DTE ").Append(noRec).Append(", po rozliczeniu DTE - sprzet oddal DTE ").Append(late)
                  .Append(", bez przydzialu DTE ").Append(noAsg).Append(')');
                if (names.Count > 0) sb.Append(" | ").Append(string.Join("; ", names.ToArray()));
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Sprzet (linia)", e); }
            finally { _gear.Clear(); _gearMe = null; }
        }
    }
}
