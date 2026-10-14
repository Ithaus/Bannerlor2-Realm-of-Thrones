using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// A17 (poz. 10, PLAN 4.18) - POJEDYNEK Z NOCNYM KROLEM: WSTAJA TYLKO POLEGLI TEJ BITWY. Klucz OthersDuelFromFallen (wylaczony = 79a8d89).
    ///
    /// ROT (ROT.CampaignBehaviors.ROTDuelsBehavior, dekompilacja): MapEventStarted gracz vs partia Nocnego Krola - LoadNKBattle kopiuje sklad partii
    /// (NKTroopCopy = MemberRoster.GetTroopRoster().ToMBList(), :241-258); MapEventEnded tej bitwy: gracz nie przegral, pojedynek byl (nkDuelMissionTriggered),
    /// a Nocny Krol nie pokonany (nkDefeated false) - gdy partia NK pusta, ROT dodaje jej CALA kopie skladu (MemberRoster.Add, :186-197) i wznawia bitwe
    /// (TickEvent: StartBattleAction) - cala armia wightow z niczego. Menu ROT: "The Night King has gotten back up and is raising his wights".
    ///
    /// REGULA (kanon ROT: wstaja polegli z pola): prefiks First na MapEvent.FinalizeEventAux (przed nasluchami MapEventEnded, w tym ROT) - dla bitwy NKBattle
    /// w tej samej sytuacji, w ktorej ROT wskrzesza, kopia skladu (bez bohaterow) przycieta proporcjonalnie do liczby poleglych tej bitwy (DiedInBattle obu
    /// stron, szeregowi - ludzie gracza i wighty NK); bohaterowie kopii bez zmian. Gdy poleglych co najmniej tyle co kopii - bez zmian. Nocny Krol, pojedynek
    /// i ponowna bitwa bez zmian (fabuly ROT nie ruszamy). Zapis jak (a) nekromancji E4/2: polegli -> wstali, linia "Inni z poleglych (E4/2)" pozycja (g).
    /// Polegli tej bitwy sa juz w ksiedze pochodzenia jako polegli (PeopleBook.OnMapEventEnded - FDieBattle); wighty bez slownika (Inni). Sciezka tylko gracza.
    /// </summary>
    internal static class OthersDuel
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.OthersDuelFromFallen; } }

        private static FieldInfo _fInst, _fBattle, _fParty, _fCopy, _fTrig, _fDef;
        private static bool _wired;
        private static int _n, _nCut, _stumbles;
        private static long _fallen, _had, _risen;
        private static bool _err;

        internal static void Reset() { _n = _nCut = _stumbles = 0; _fallen = _had = _risen = 0; _err = false; }

        internal static void ApplyAll(Harmony h)
        {
            if (_wired || h == null) return;
            var t = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTDuelsBehavior");
            if (t != null)
            {
                _fInst = AccessTools.Field(t, "Instance"); _fBattle = AccessTools.Field(t, "NKBattle"); _fParty = AccessTools.Field(t, "NKParty");
                _fCopy = AccessTools.Field(t, "NKTroopCopy"); _fTrig = AccessTools.Field(t, "nkDuelMissionTriggered"); _fDef = AccessTools.Field(t, "nkDefeated");
            }
            var m = AccessTools.Method(typeof(MapEvent), "FinalizeEventAux");
            bool ok = t != null && m != null && _fInst != null && _fInst.IsStatic && _fBattle != null && _fParty != null && _fCopy != null && _fTrig != null && _fDef != null
                      && _fCopy.FieldType == typeof(MBList<TroopRosterElement>) && _fTrig.FieldType == typeof(bool) && _fDef.FieldType == typeof(bool);
            if (!ok)
            {
                Log.Info("OthersDuel (A17): BRAK ROT ROTDuelsBehavior albo jego pol / MapEvent.FinalizeEventAux - pojedynek z Nocnym Krolem jak w ROT (cala kopia armii).");
                return;
            }
            h.Patch(m, prefix: new HarmonyMethod(typeof(OthersDuel), nameof(Pre)) { priority = Priority.First });
            _wired = true;
            Log.Info("OthersDuel (A17): pojedynek z Nocnym Krolem - wstaja tylko polegli tej bitwy; latka wpieta (klucz Others Duel From Fallen czytany na zywo).");
        }

        public static void Pre(MapEvent __instance)
        {
            if (!_wired || __instance == null || !On) return;
            try
            {
                if (__instance.IsFinalized) return;
                var inst = _fInst.GetValue(null);
                if (inst == null || !ReferenceEquals(_fBattle.GetValue(inst), __instance)) return;
                // ten sam warunek co ROT MapEventEnded: gracz przegral - smierc gracza (bez wskrzeszenia); inaczej wskrzeszenie tylko po pojedynku z niepokonanym NK
                var main = PartyBase.MainParty;
                var ws = __instance.WinningSide;
                if (main == null || (ws != BattleSideEnum.None && main.Side != ws)) return;
                if (!(bool)_fTrig.GetValue(inst) || (bool)_fDef.GetValue(inst)) return;
                var copy = _fCopy.GetValue(inst) as MBList<TroopRosterElement>;
                if (copy == null) return;
                int fallen = 0;
                foreach (var side in new[] { __instance.AttackerSide, __instance.DefenderSide })
                {
                    if (side == null) continue;
                    foreach (var mep in side.Parties)
                    {
                        var d = mep != null ? mep.DiedInBattle : null;
                        if (d == null) continue;
                        for (int i = 0; i < d.Count; i++)
                        {
                            var e = d.GetElementCopyAtIndex(i);
                            if (e.Character != null && !e.Character.IsHero && e.Number > 0) fallen += e.Number;
                        }
                    }
                }
                int had = 0;
                foreach (var e in copy) if (e.Character != null && !e.Character.IsHero && e.Number > 0) had += e.Number;
                var nk = _fParty.GetValue(inst) as PartyBase;
                bool empty = nk != null && nk.NumberOfAllMembers == 0;
                _n++; _fallen += fallen; _had += had;
                if (had <= fallen)
                {
                    _risen += had;
                    Log.Info("Pojedynek z Nocnym Krolem (A17): polegli tej bitwy " + fallen + ", kopia armii ROT " + had + " - bez przyciecia (wstaje cala kopia); partia NK pusta: " + (empty ? "tak" : "nie") + ".");
                    return;
                }
                // przyciecie proporcjonalne do poleglych (reszta z zaokraglen do typow z najwiekszym ulamkiem); bohaterowie bez zmian
                var regs = new List<int>();
                var xs = new List<int>();
                var fr = new List<double>();
                int given = 0;
                for (int i = 0; i < copy.Count; i++)
                {
                    var e = copy[i];
                    if (e.Character == null || e.Character.IsHero || e.Number <= 0) continue;
                    double q = (double)e.Number * fallen / had;
                    int x = (int)Math.Floor(q);
                    regs.Add(i); xs.Add(x); fr.Add(q - x); given += x;
                }
                var order = new List<int>();
                for (int k = 0; k < regs.Count; k++) order.Add(k);
                order.Sort((a, b) => fr[b].CompareTo(fr[a]));
                foreach (var k in order) { if (given >= fallen) break; if (xs[k] < copy[regs[k]].Number) { xs[k]++; given++; } }
                var res = new MBList<TroopRosterElement>();
                int ri = 0;
                for (int i = 0; i < copy.Count; i++)
                {
                    var e = copy[i];
                    if (ri < regs.Count && regs[ri] == i)
                    {
                        int x = xs[ri++];
                        if (x <= 0) continue;
                        var e2 = e;
                        e2.Xp = e.Number > 0 ? (int)((long)e.Xp * x / e.Number) : 0;
                        e2.WoundedNumber = Math.Min(e.WoundedNumber, x);
                        e2.Number = x;
                        res.Add(e2);
                    }
                    else if (e.Character != null && e.Character.IsHero) res.Add(e);
                }
                _fCopy.SetValue(inst, res);
                _nCut++; _risen += fallen;
                Log.Info("Pojedynek z Nocnym Krolem (A17): polegli tej bitwy " + fallen + " (obie strony, szeregowi) - wstaje tylu z kopii armii ROT " + had
                         + " (przycieto " + (had - fallen) + "); partia NK pusta: " + (empty ? "tak - ROT doda przycieta kopie" : "nie - ROT nic nie doda") + ".");
            }
            catch (Exception e) { _stumbles++; if (!_err) { _err = true; Log.Error("OthersDuel.Pre", e); } }
        }

        /// <summary>Pozycja (g) linii "Inni z poleglych (E4/2)" (OthersRise.Daily) - czyta i zeruje liczniki doby.</summary>
        internal static string DayText()
        {
            string s = "pojedynek z Nocnym Krolem (A17): " + (!_wired ? "BRAK LATKI" : On ? "z poleglych" : "WYLACZONE (Others Duel From Fallen - cala kopia ROT)")
                       + ", pojedynkow przegranych przez gracza " + _n + ", polegli tej bitwy " + _fallen + " -> wstalo " + _risen + " (kopia ROT " + _had + ", przycieto " + (_had - _risen) + " w " + _nCut + ")"
                       + (_stumbles > 0 ? ", potkniecia " + _stumbles : "");
            _n = _nCut = _stumbles = 0; _fallen = _had = _risen = 0;
            return s;
        }
    }
}
