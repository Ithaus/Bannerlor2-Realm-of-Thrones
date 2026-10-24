using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace Armoury
{
    /// <summary>
    /// m116-fabula - PETLA OBLEZENIA (Jeff 24.10, raport z gry: "Koniec oblezenia (KO) ... ROT_castle18 (Cape Wrath) ... oblezenie 0.1 dob | przyczyna: odejscie z obozu
    /// (zmiana celu - ostatnia mysl: GoToSettlement castle_village_ES4_2 ...)" ponad 60 razy, a do tego kruk "Cape Wrath has been besieged by Stormlands" za kazdym razem).
    /// Wodz armii Krain Burzy stawia oboz, godzine pozniej jego mysl (BesiegeSettlement tej osady wcale nie ma w liscie - "oblezenie -") wybiera wies obok, gra zwija
    /// oboz (BesiegerCamp.CheckBesiegerPartiesAndMakeThemLeave), a nastepna mysl znowu wybiera oblezenie.
    ///  - TRZYMANIE SWIEZEGO OBLEZENIA (klucz SiegeAiHoldsFreshSiege; postfiks Last ThinkPost - ten sam co F2): druzyna AI w obozie (wodz obozu albo samodzielna
    ///    druzyna - nie dolaczona do armii), oblezenie mlodsze niz FreshHours, poza bitwa i morzem, bez DoNotChangeBehavior, skryptu i Innych, jedzenie druzyny
    ///    (z armia) na co najmniej FreshFoodDays (suma Food / -FoodChange), bez przewazajacej odsieczy w drodze (OB5: zebrana >= wymagana), a najlepszy cel mysli
    ///    nie jest obrona twierdzy wlasnej frakcji -> wpis BesiegeSettlement tej osady dostaje max(1.05 x najlepszy + 0.01, HoldFloor) (jak F2), wiec gra zostaje.
    ///  - ZAKAZ POWTORKI (klucz SiegeAiRetryCooldown): oblezenie zwiniete przed uplywem doby, bez zdobycia, bez pokonania oblegajacych, bez przerwanego szturmu
    ///    i bez odparc -> frakcja oblegajaca nie oblega tej osady przez RetryBanDays (wynik BesiegeSettlement 0 w mysli druzyn poza obozem). Pamiec sesji.
    /// Diagnostyka: mysl wodza (Remember) z celem druzyny, armia i jedzeniem z Food/FoodChange; linia "Oblezenia 3 (doba)" z licznikami. Potkniecia - Stumble3.
    /// </summary>
    internal static partial class SiegeWalls2
    {
        internal static bool FreshOn { get { var s = Settings.Current; return s != null && s.SiegeAiHoldsFreshSiege; } }
        internal static bool RetryOn { get { var s = Settings.Current; return s != null && s.SiegeAiRetryCooldown; } }
        private const double FreshHours = 48.0;
        private const float FreshFoodDays = 1.5f;
        private const double RetryShortDays = 1.0;   // oblezenie krotsze niz doba ...
        private const double RetryBanDays = 3.0;     // ... = ta frakcja nie wraca pod te osade przez 3 dni

        private static readonly Dictionary<SiegeEvent, string> _besFac = new Dictionary<SiegeEvent, string>();   // frakcja oblegajaca (oboz bywa zwiniety przed zdarzeniem konca)
        private static readonly HashSet<SiegeEvent> _freshSaved = new HashSet<SiegeEvent>();                      // oblezenia, ktore trzymanie zatrzymalo dzis
        private static readonly Dictionary<string, double> _ban = new Dictionary<string, double>(StringComparer.Ordinal);   // "frakcja|osada" -> dzien konca zakazu
        private static int _dFreshH, _dFreshDefend, _dFreshFood, _dFreshRelief, _dBanNew, _dBanHit;

        /// <summary>Jedzenie druzyny (wodz armii - cala armia) w dniach: suma Food / -suma FoodChange; bez ubytku - 999.</summary>
        internal static float FoodDays(MobileParty lp)
        {
            if (lp == null) return -1f;
            float food = 0f, change = 0f;
            if (lp.Army != null && lp.Army.LeaderParty == lp)
            {
                foreach (var mp in lp.Army.Parties) if (mp != null && mp.IsActive) { food += mp.Food; change += mp.FoodChange; }
            }
            else { food = lp.Food; change = lp.FoodChange; }
            if (change > -0.0001f) return 999f;
            return Math.Max(0f, food) / -change;
        }

        private static string FacOf(MobileParty mp) { return mp != null && mp.MapFaction != null ? mp.MapFaction.StringId : null; }

        /// <summary>Z SiegeWalls2.OnSiegeStarted i z mysli wodza: frakcja oblegajaca tego oblezenia.</summary>
        private static void NoteBesieger(SiegeEvent ev, MobileParty lp)
        {
            string f = FacOf(lp);
            if (ev != null && f != null) _besFac[ev] = f;
        }

        /// <summary>Druzyna moze byc trzymana pod swiezym oblezeniem (szybkie warunki - bez petli po ocenach).</summary>
        private static bool FreshCandidate(MobileParty party, BesiegerCamp camp, SiegeEvent ev, Settlement st, PartyThinkParams pt)
        {
            if (!FreshOn || party.IsMainParty || party.AttachedTo != null) return false;
            if (party.Army != null && party.Army.LeaderParty != party) return false;
            if (st.Town == null || !st.IsFortification) return false;
            if (ev.SiegeStartTime.ElapsedHoursUntilNow >= FreshHours) return false;
            if (party.MapEvent != null || party.IsCurrentlyAtSea || pt.DoNotChangeBehavior || Scripted(party) || Und(party, st)) return false;
            return true;
        }

        /// <summary>Trzymanie swiezego oblezenia: true - wynik oblezenia podniesiony ponad najlepszy cel. bi/bd/b0 - najlepszy wpis, si - wpis oblezenia tej osady.</summary>
        private static bool HoldFresh(MobileParty party, SiegeEvent ev, Settlement st, PartyThinkParams pt, int bi, AIBehaviorData bd, float b0, int si)
        {
            if (bi < 0) return false;
            if (ReferenceEquals(bd.Party, st) && (bd.AiBehavior == AiBehavior.BesiegeSettlement || bd.AiBehavior == AiBehavior.AssaultSettlement)) return false;   // gra i tak zostaje
            if (bd.AiBehavior == AiBehavior.DefendSettlement)
            {
                var ds = bd.Party as Settlement;
                if (ds != null && ds.IsFortification && ds.MapFaction == party.MapFaction) { _dFreshDefend++; return false; }   // obrona wlasnej twierdzy - pierwsza
            }
            if (FoodDays(party) < FreshFoodDays) { _dFreshFood++; return false; }   // glod - gra prowadzi po jedzenie
            if (BesiegedRelief.ReliefReady(ev)) { _dFreshRelief++; return false; }  // przewazajaca odsiecz w drodze - odwrot gry
            float v = Math.Max(b0 * 1.05f + 0.01f, HoldFloor);
            if (si >= 0) { var d = pt.AIBehaviorScores[si].Item1; pt.SetBehaviorScore(in d, v); }
            else pt.AddBehaviorScore((new AIBehaviorData(st, AiBehavior.BesiegeSettlement, MobileParty.NavigationType.Default, false, false, false), v));
            _dFreshH++;
            _freshSaved.Add(ev);
            return true;
        }

        /// <summary>Zakaz powtorki w mysli druzyny poza obozem: wpisy BesiegeSettlement osad zakazanych dla jej frakcji - wynik 0. Nie rzuca.</summary>
        private static void ApplyBan(MobileParty party, PartyThinkParams pt)
        {
            try
            {
                if (!RetryOn || party.IsMainParty || pt.DoNotChangeBehavior) return;
                string f = FacOf(party);
                if (f == null) return;
                var scores = pt.AIBehaviorScores;
                if (scores == null) return;
                double now = NowD;
                List<int> hit = null;
                for (int i = 0; i < scores.Count; i++)
                {
                    var e = scores[i];
                    if (e.Item1.AiBehavior != AiBehavior.BesiegeSettlement || e.Item2 <= 0f) continue;
                    var s = e.Item1.Party as Settlement;
                    double until;
                    if (s == null || !_ban.TryGetValue(f + "|" + s.StringId, out until) || until <= now) continue;
                    if (hit == null) hit = new List<int>();
                    hit.Add(i);
                }
                if (hit == null) return;
                foreach (int i in hit) { var d = scores[i].Item1; pt.SetBehaviorScore(in d, 0f); _dBanHit++; }
            }
            catch (Exception e) { Stumble3("ApplyBan", e); }
        }

        /// <summary>Z SiegeWalls.OnSiegeEnded (przed EndLine): krotkie oblezenie bez zdobycia i bez bitwy - zakaz powtorki dla frakcji oblegajacej. Nie rzuca.</summary>
        internal static void NoteSiegeEndForBan(SiegeEvent ev, int repels)
        {
            try
            {
                if (ev == null) return;
                string f = null;
                var camp = ev.BesiegerCamp;
                var lp = camp != null ? camp.LeaderParty : null;
                if (lp != null) f = FacOf(lp);
                if (f == null) _besFac.TryGetValue(ev, out f);
                _besFac.Remove(ev);
                _freshSaved.Remove(ev);
                if (!RetryOn || f == null) return;
                var st = ev.BesiegedSettlement;
                if (st == null || st.Town == null || !st.IsFortification) return;
                if (lp != null && lp.IsMainParty) return;
                if (MobileParty.MainParty != null && camp != null && MobileParty.MainParty.BesiegerCamp == camp) return;
                if (ev.SiegeStartTime.ElapsedDaysUntilNow >= RetryShortDays) return;
                if (repels > 0 || CapturedNow(st) || BeatenNow(ev)) return;
                double t0;
                if (_abortAt.TryGetValue(st, out t0) && Math.Abs(NowD - t0) < 0.02) return;   // przerwany szturm - byla bitwa
                if (st.Party != null && st.Party.MapEvent != null) return;
                bool fresh = !_ban.ContainsKey(f + "|" + st.StringId);
                _ban[f + "|" + st.StringId] = NowD + RetryBanDays;
                _dBanNew++;
                if (LogOn && fresh)
                    Log.Info("Oblezenia 3 (m116): zakaz powtorki - " + f + " nie oblega " + st.StringId + " (" + Nm(st) + ") przez " + F(RetryBanDays, "0") + " dni (oblezenie "
                             + F(ev.SiegeStartTime.ElapsedDaysUntilNow, "0.00") + " doby, bez zdobycia i bez bitwy).");
            }
            catch (Exception e) { Stumble3("NoteSiegeEndForBan", e); }
        }

        private static int BanActive()
        {
            int n = 0; double now = NowD;
            foreach (var v in _ban.Values) if (v > now) n++;
            return n;
        }

        /// <summary>Z Daily3: porzadek pamieci (zakazy po terminie, frakcje oblezen, ktorych juz nie ma) i tekst do linii doby.</summary>
        private static string HoldDayText()
        {
            double now = NowD;
            if (_ban.Count > 0)
            {
                List<string> old = null;
                foreach (var kv in _ban) if (kv.Value <= now) { if (old == null) old = new List<string>(); old.Add(kv.Key); }
                if (old != null) foreach (var k in old) _ban.Remove(k);
            }
            if (_besFac.Count > 0)
            {
                List<SiegeEvent> dead = null;
                foreach (var kv in _besFac) if (kv.Key == null || kv.Key.BesiegedSettlement == null || kv.Key.BesiegedSettlement.SiegeEvent != kv.Key) { if (dead == null) dead = new List<SiegeEvent>(); dead.Add(kv.Key); }
                if (dead != null) foreach (var e in dead) _besFac.Remove(e);
            }
            return " | m116: swieze trzymane " + _dFreshH + " h (oblezen " + _freshSaved.Count + "; nie: obrona wlasnej twierdzy " + _dFreshDefend + ", jedzenie < "
                   + F(FreshFoodDays, "0.0") + " doby " + _dFreshFood + ", odsiecz przewazajaca " + _dFreshRelief + "), klucz " + (FreshOn ? "tak" : "nie")
                   + " | zakazy powtorki: nowe " + _dBanNew + " (aktywne " + BanActive() + "), zablokowane mysli " + _dBanHit + ", klucz " + (RetryOn ? "tak" : "nie");
        }

        private static void ZeroHoldDay()
        {
            _dFreshH = _dFreshDefend = _dFreshFood = _dFreshRelief = _dBanNew = _dBanHit = 0;
            _freshSaved.Clear();
        }

        private static void ResetHold()
        {
            _besFac.Clear(); _ban.Clear();
            ZeroHoldDay();
        }
    }
}
