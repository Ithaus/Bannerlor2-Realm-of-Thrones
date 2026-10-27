using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;

namespace Armoury
{
    /// <summary>
    /// 264 - PROBA AUTOTESTU (tylko DLL autotestu CrashScribe - LogGate.Mode 1/2 - i plik Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-pokoj264.flag;
    /// plik tylko czytany; w zwyklej grze nic nie robi). Na kopii zapisu autotestu, nigdy na zapisach Jeffa. Pierwsza godzina sesji (od 2. godziny) poza bitwa:
    ///  T1 ApplyPeace Diplomacy (odbiciem) Dorzecze -> korona: wojna dalej, blokada policzona (bez okna gracza, bez reparacji);
    ///  T2 vlandia.AddDecision(MakePeaceKingdomDecision(rod korony, Dorzecze)): brak czekajacej decyzji, wplywy wnioskodawcy bez zmian, wojna dalej;
    ///  T3 MakePeaceKingdomDecision(rod Dorzecza, korona).IsAllowed() = false;
    ///  T4 tabela prawdy Locked (Krwawe Gody 0/1/-1/2, Robb martwy, po koncu kanonu ROT wymusza / nie, wesele ROT w toku, gracz rzadzi, gracz rzadzi + zmeczenie
    ///     bez wyboru, klucz, kampania, krolestwo zniszczone - recenzja 264) i Blocks tej chwili (obie kolejnosci TAK, para spoza tabeli NIE);
    ///  T5 tabela prawdy PeaceHold.Decide i bieg: pokoj ApplyByKingdomDecision pary wymuszonej ROT spoza tabeli (Essos) - wynik po wznowieniu: "odwolane",
    ///     nigdy "uwolnione" (czekanie na ROT - INFO, zalezy od kolejnosci dob ROT i Armoury);
    ///  T6 MakePeaceAction.Apply(korona, Dorzecze) (droga Kroniki, celowo bez blokady): m104 widzi WZNOWIONA, M6a-3 "odwolane";
    ///  T7 linie diagnostyki 4 wladcow z kazdym polem.
    /// T5 i T6 czekaja najdluzej TestWaitHours godzin (24 + 48 + zapas). Linie "Pokoj kanonu (264): test - Tn ... - wynik TAK|NIE|POMINIETY."
    /// </summary>
    internal static class CanonPeaceLockTest
    {
        internal const string AutoFlagName = "armoury-pokoj264.flag";
        private const int TestWaitHours = 80;
        private static bool _done, _flag, _live;
        private static int _flagDay = -1, _hours;
        private static long _liveHour;
        private static Kingdom _eA, _eB, _crown, _river;
        private static int _resumed0;
        private static bool _t5Done, _t6Done;

        internal static void Reset() { _done = false; _flag = false; _live = false; _flagDay = -1; _hours = 0; _eA = _eB = _crown = _river = null; _t5Done = _t6Done = false; }

        private static string P { get { return CanonPeaceLock.Tag + "test - "; } }

        private static string FlagPath()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), AutoFlagName);
        }

        private static bool Wanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;   // tylko DLL autotestu CrashScribe
            int today = (int)CampaignTime.Now.ToDays;
            if (_flagDay != today)
            {
                _flagDay = today;
                try { _flag = File.Exists(FlagPath()); } catch { _flag = false; }
            }
            return _flag;
        }

        /// <summary>Dopisek linii startu 264 (tylko DLL autotestu; w grze pusty).</summary>
        internal static string StartText()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return "";
            _flagDay = -1;
            return " | proba autotestu: flaga " + AutoFlagName + " " + (Wanted() ? "TAK" : "NIE");
        }

        private static string T(bool b) { return b ? "TAK" : "NIE"; }

        private static void Line(string text, bool? ok) { Log.Info(P + text + " - wynik " + (ok == null ? "POMINIETY" : ok.Value ? "TAK" : "NIE") + "."); }

        /// <summary>Co godzine (z CanonWarWatch.Hourly w ArmouryBehavior): w zwyklej grze Wanted wraca od razu.</summary>
        internal static void Hourly()
        {
            if (_done && !_live) return;
            _hours++;
            if (!_done)
            {
                if (_hours < 2 || !Wanted()) return;
                var mp = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty;
                if (mp != null && (mp.MapEvent != null || mp.SiegeEvent != null)) return;
                _done = true;
                try { Run(); } catch (Exception e) { CanonPeaceLock.Stumble("Test.Run", e); Line("T0 bieg przerwany (" + e.GetType().Name + ")", false); }
                return;
            }
            try { Live(); } catch (Exception e) { _live = false; CanonPeaceLock.Stumble("Test.Live", e); Line("T5 bieg przerwany razem z T6 (" + e.GetType().Name + ")", false); }
        }

        private static void Run()
        {
            _crown = Prologue.KingdomById(Prologue.Crown);
            _river = Prologue.KingdomById(Prologue.RiverId);
            bool ready = _crown != null && _river != null && !_crown.IsEliminated && !_river.IsEliminated && _crown.IsAtWarWith(_river) && CanonPeaceLock.Blocks(_river, _crown);
            string skip = ready ? null : "korona-Dorzecze nie w wojnie albo blokada nieczynna (klucz, kampania, gracz rzadzi, Krwawe Gody)";
            // T1
            if (skip != null) Line("T1 ApplyPeace Diplomacy Dorzecze -> korona (" + skip + ")", null);
            else if (CanonPeaceLock.DipApplyPeace == null) Line("T1 ApplyPeace Diplomacy (latka H1 nie wpieta - brak Diplomacy)", null);
            else
            {
                CanonPeaceLock.TestArmed = true; CanonPeaceLock.TestHits = 0;
                try { CanonPeaceLock.DipApplyPeace.Invoke(null, new object[] { _river, _crown, null, false, false, false }); }
                finally { CanonPeaceLock.TestArmed = false; }
                bool war = _crown.IsAtWarWith(_river);
                Line("T1 ApplyPeace Diplomacy Dorzecze -> korona: wojna dalej " + T(war) + ", blokad " + CanonPeaceLock.TestHits, war && CanonPeaceLock.TestHits >= 1);
            }
            // T2
            if (skip != null || _crown.RulingClan == null) Line("T2 AddDecision MakePeaceKingdomDecision korona -> Dorzecze (" + (skip ?? "brak rodu korony") + ")", null);
            else
            {
                var clan = _crown.RulingClan;
                float inf0 = clan.Influence;
                CanonPeaceLock.TestArmed = true; CanonPeaceLock.TestHits = 0;
                try { _crown.AddDecision(new MakePeaceKingdomDecision(clan, _river), false); }
                finally { CanonPeaceLock.TestArmed = false; }
                int pending = 0;
                foreach (var d in _crown.UnresolvedDecisions) { var mpd = d as MakePeaceKingdomDecision; if (mpd != null && mpd.FactionToMakePeaceWith == _river) pending++; }
                bool war = _crown.IsAtWarWith(_river);
                bool infOk = Math.Abs(clan.Influence - inf0) < 0.01f;
                Line("T2 AddDecision korona -> Dorzecze: czekajace decyzje " + pending + ", wplywy " + inf0.ToString("0.0") + " -> " + clan.Influence.ToString("0.0") + ", wojna dalej " + T(war)
                     + ", blokad " + CanonPeaceLock.TestHits, pending == 0 && infOk && war && CanonPeaceLock.TestHits >= 1);
            }
            // T3
            if (skip != null || _river.RulingClan == null) Line("T3 IsAllowed (" + (skip ?? "brak rodu Dorzecza") + ")", null);
            else
            {
                bool allowed = new MakePeaceKingdomDecision(_river.RulingClan, _crown).IsAllowed();
                Line("T3 MakePeaceKingdomDecision Dorzecze -> korona IsAllowed " + T(allowed), !allowed);
            }
            // T4
            Line("T4 tabela prawdy Locked i Blocks tej chwili: " + TruthLocked(), TruthLockedOk() && (skip != null || (CanonPeaceLock.Blocks(_crown, _river) && !NonRuleBlocks())));
            // T5a
            bool dec = PeaceHold.Decide(23, false, true) == PeaceHold.ActWait && PeaceHold.Decide(24, true, true) == PeaceHold.ActKeep && PeaceHold.Decide(24, false, true) == PeaceHold.ActWaitRot
                       && PeaceHold.Decide(71, false, true) == PeaceHold.ActWaitRot && PeaceHold.Decide(72, false, true) == PeaceHold.ActRelease && PeaceHold.Decide(24, false, false) == PeaceHold.ActRelease
                       && PeaceHold.Decide(50, true, false) == PeaceHold.ActKeep;
            Line("T5a tabela prawdy PeaceHold.Decide (24 h, w wojnie, ROT wymusza, 72 h)", dec);
            // T7
            var lines = CanonWarWatch.TestRulerLines();
            int full = 0;
            foreach (var l in lines)
                if (l.Contains(" | partia: ") && l.Contains(" | AI: ") && l.Contains(" | armia: ") && l.Contains(" | dzis: bitwy polowe ") && l.Contains(" | mysl AI (mysli ")
                    && l.Contains(" | dlaczego stoi: ") && l.Contains(" | SCA: ")) full++;
            Line("T7 diagnostyka wladcow: linii " + lines.Count + ", z kazdym polem " + full, lines.Count == 4 && full == 4);
            // T5 / T6 bieg
            _eA = _eB = null;
            foreach (var a in Kingdom.All)
            {
                if (_eA != null) break;
                if (a == null || a.IsEliminated || Excluded(a)) continue;
                foreach (var b in Kingdom.All)
                {
                    if (b == null || b == a || b.IsEliminated || Excluded(b) || !a.IsAtWarWith(b)) continue;
                    if (!Prologue.RotWarForced(a, b) || CanonPeaceLock.Blocks(a, b)) continue;
                    _eA = a; _eB = b; break;
                }
            }
            if (_eA == null) { Line("T5 bieg (brak pary w wojnie wymuszonej przez ROT spoza tabeli)", null); _t5Done = true; }
            else
            {
                MakePeaceAction.ApplyByKingdomDecision(_eA, _eB, 0, 0);
                Log.Info(P + "T5 start: pokoj " + _eA.StringId + "-" + _eB.StringId + " (ApplyByKingdomDecision, ROT wymusza), w wojnie po pokoju " + T(_eA.IsAtWarWith(_eB)) + ".");
            }
            if (skip != null) { Line("T6 bieg (" + skip + ")", null); _t6Done = true; }
            else
            {
                _resumed0 = Prologue.RiverResumedSeen;
                CanonPeaceLock.TestDirect = true;
                try { MakePeaceAction.Apply(_crown, _river); }
                finally { CanonPeaceLock.TestDirect = false; }
                Log.Info(P + "T6 start: pokoj korona-Dorzecze (MakePeaceAction.Apply - droga Kroniki, bez blokady), w wojnie po pokoju " + T(_crown.IsAtWarWith(_river)) + ".");
            }
            _live = !(_t5Done && _t6Done);
            _liveHour = (long)Math.Floor(CampaignTime.Now.ToHours);
        }

        private static bool Excluded(Kingdom k)
        {
            string id = k.StringId;
            return id == Prologue.Crown || id == Prologue.RiverId || id == Prologue.NorthId || Prologue.RulesRealm(k) || (Clan.PlayerClan != null && Clan.PlayerClan.Kingdom == k);
        }

        private static bool NonRuleBlocks()
        {
            foreach (var k in Kingdom.All)
                if (k != null && !k.IsEliminated && k.StringId != Prologue.Crown && k.StringId != Prologue.RiverId && k.StringId != Prologue.NorthId && _crown != null
                    && CanonPeaceLock.Blocks(_crown, k)) return true;
            return false;
        }

        private static bool L(bool key, bool canon, bool pr, bool el, int rw, bool dead, bool forced = false)
        { string w; return CanonPeaceLock.Locked(key, canon, pr, el, rw, dead, forced, out w); }

        // gracz rzadzi i droga bez wyboru (zmeczenie wojna Diplomacy - okno tylko OK / odmowa wylaczona) - jak w CanonPeaceLock.State
        private static bool LWeary(int rw, bool dead, bool forced) { return L(true, true, CanonPeaceLock.PlayerChoice(true, true), false, rw, dead, forced); }

        private static bool TruthLockedOk()
        {
            // recenzja 264 (1): RedWeddingEvent 1 (zapowiedziane, ROT czeka 14 dni) - blokada TRWA; koniec dopiero stan 2 albo smierc Robba
            // recenzja 264 (3): po koncu kanonu blokada trwa, dopoki ROT wymusza wojne; stan 1 + Robb martwy (ExecuteRobb ROT) - otwarte zawsze
            // recenzja 264 (2): gracz rzadzi - pokoj jego wyborem (otwarte), ale zmeczenie wojna bez wyboru zablokowane jak u AI
            return L(true, true, false, false, 0, false) && L(true, true, false, false, -1, false) && L(true, true, false, false, 1, false) && !L(true, true, false, false, 2, false)
                   && L(true, true, false, false, 2, false, true) && !L(true, true, false, false, 0, true) && L(true, true, false, false, 0, true, true)
                   && !L(true, true, false, false, -1, true) && !L(true, true, false, false, 1, true, true) && !L(true, true, false, false, 2, true)
                   && !L(true, true, CanonPeaceLock.PlayerChoice(true, false), false, 0, false) && LWeary(0, false, false) && LWeary(2, false, true) && !LWeary(2, true, false)
                   && !L(false, true, false, false, 0, false) && !L(true, false, false, false, 0, false) && !L(true, true, false, true, 0, false);
        }

        private static string TruthLocked()
        {
            return "RW0 " + T(L(true, true, false, false, 0, false)) + ", RW-1 " + T(L(true, true, false, false, -1, false)) + ", RW1 " + T(L(true, true, false, false, 1, false))
                   + ", RW2 " + T(L(true, true, false, false, 2, false)) + ", RW2 ROT wymusza " + T(L(true, true, false, false, 2, false, true))
                   + ", Robb martwy " + T(L(true, true, false, false, 0, true)) + ", Robb martwy ROT wymusza " + T(L(true, true, false, false, 0, true, true))
                   + ", RW1 Robb martwy ROT wymusza " + T(L(true, true, false, false, 1, true, true))
                   + ", gracz rzadzi " + T(L(true, true, CanonPeaceLock.PlayerChoice(true, false), false, 0, false)) + ", gracz rzadzi zmeczenie bez wyboru " + T(LWeary(0, false, false))
                   + ", klucz NIE " + T(L(false, true, false, false, 0, false)) + ", klasyczna " + T(L(true, false, false, false, 0, false)) + ", zniszczone " + T(L(true, true, false, true, 0, false));
        }

        private static void Live()
        {
            if (!_live) return;
            long el = (long)Math.Floor(CampaignTime.Now.ToHours) - _liveHour;
            if (!_t5Done && _eA != null)
            {
                string o = PeaceHold.Outcome(_eA.StringId, _eB.StringId);
                if (o == "odwolane" || o == "uwolnione" || o == "upadlo")
                {
                    _t5Done = true;
                    Line("T5 bieg " + _eA.StringId + "-" + _eB.StringId + ": po " + el + " h stan M6a-3 '" + o + "', w wojnie " + T(_eA.IsAtWarWith(_eB)) + ", czekanie na ROT "
                         + T(PeaceHold.RotWaitSeen(_eA.StringId, _eB.StringId)), o == "odwolane");
                }
                else if (el > TestWaitHours) { _t5Done = true; Line("T5 bieg: po " + el + " h stan M6a-3 '" + (o ?? "-") + "' bez rozstrzygniecia", false); }
            }
            if (!_t6Done && _crown != null)
            {
                string o = PeaceHold.Outcome(_crown.StringId, _river.StringId);
                bool res = Prologue.RiverResumedSeen > _resumed0;
                bool hold = PeaceHold.On;
                if ((o == "odwolane" || o == "uwolnione" || (!hold && o == null)) && res)
                {
                    _t6Done = true;
                    Line("T6 bieg korona-Dorzecze: po " + el + " h m104 WZNOWIONA " + T(res) + ", stan M6a-3 '" + (o ?? "-") + "', w wojnie " + T(_crown.IsAtWarWith(_river)),
                         (o == "odwolane" || !hold) && res);
                }
                else if (el > TestWaitHours)
                {
                    _t6Done = true;
                    Line("T6 bieg: po " + el + " h m104 WZNOWIONA " + T(res) + ", stan M6a-3 '" + (o ?? "-") + "'", false);
                }
            }
            if (_t5Done && _t6Done) _live = false;
        }
    }
}
