using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 458-r - strona mapy reguly "odwrot wodza pod oslona tylnej strazy" (klucz Commander Withdraws Under Rearguard; misja: CommanderWithdraw458.cs).
    ///
    /// GRA (dekompilacja 1.4.8, decout/cs_full.cs): po wygranej gracza PlayerEncounter.DoWait -> PrepareResults -> MapEvent.CalculateAndCommitMapEventResults
    /// (112158): lup cial, przedmiotow, jencow, statkow, potem CaptureDefeatedPartyMembers (112702) - kazda pokonana partia z listy defeatedParties traci WSZYSTKICH
    /// szeregowych (zdrowi i ranni do lochu zwyciezcow wedlug GetCaptureMemberChancesForWinnerParties, kto nie trafi do odbiorcy albo nie moze byc jencem - znika),
    /// a bohater: przywodca RemovePartyLeader, CanBecomePrisoner -> TakePrisonerAction, kto zostal - MakeHeroFugitiveAction. Lista defeatedParties jest kopia
    /// (ToMBList) przekazana jako argument - prefiks moze ja podmienic. Partia pokonana ze zdrowymi w skladzie gra NIE kasuje (MapEventSide.HandleMapEventEnd,
    /// warunek opisany w DoomedPartyMen.GameDestroys: zero zdrowych albo pusta), dostaje kare morale i ForceDefaultBehaviorUpdate (ApplyMoraleAndBehavior...).
    ///
    /// REGULA (tylko bitwa, ktora misja 458 oznaczyla: Pending z OnEndMission - wodz wroga gracza uszedl z pola, gracz wygral):
    ///  - prefiks MapEvent.CaptureDefeatedPartyMembers (Priority.Last - po migawkach PeopleBook (First), 312/Measure40, 385d; okno pojmania PeopleBook / 312
    ///    obejmuje tez nasze pojmanie, wiec ksiegi widza je jak pojmanie gry): partie "z wodzem" = partia wodza + partie, ktorych pan uszedl z nim albo nie
    ///    wszedl na pole. Te partie wyjmujemy z listy gry i rozliczamy sami TAK SAMO jak gra (te same szanse, ten sam rzut odbiorcy, CanTroopBeTakenPrisoner,
    ///    bohaterowie jak w grze), ale tylko ludzi, ktorzy NIE zeszli z wodzem: na element skladu ranni z pola = min(ranni, ranni misji), zdrowi do niewoli =
    ///    zdrowi wystawieni, ktorzy wrocili do skladu (zeszli z pola z morale >= 0.01 albo stali na koniec), minus zbiegli z wodzem. Zostaja: zbiegli z wodzem,
    ///    posilki niewprowadzone (zdrowi skladu minus wystawieni, ktorzy wrocili) i ranni sprzed bitwy. Pozostale partie strony - gra (ich zbiegli tez).
    ///  - MapEventEnded (nasluch ArmouryBehavior): partie "z wodzem" (ruchome, nie przypiete do armii) odchodza jak strona z RetreatingSide w grze
    ///    (AiMilitaryBehavior.OnMapEventEnded, cs 220459): TeleportPartyToOutSideOfEncounterRadius + Ai.CalculateFleePosition od wodza zwyciezcow + SetMoveGoToPoint.
    ///    RetreatingSide NIE ustawiamy (ratowalby caly sklad, wylaczal lup DTE i drogi H3 / 312 / A17).
    /// Kontrola: na partie przed = po + do niewoli + bez odbiorcy (jak w grze) + bohaterowie wyjeci; niezgodnosc liczona. Potkniecie na partie - ta partia wraca
    /// do listy gry (rozliczy ja gra), bez globalnego wylacznika.
    /// </summary>
    internal static class CommanderWithdraw458Map
    {
        private const string P = "Odwrot wodza (458) mapa: ";
        // liczniki misji na (partia, postac) - indeksy wspolne z CommanderWithdraw458
        internal const int CS = 0, CHS = 1, CWS = 2, CE = 3, CRm = 4, CK = 5, CEnd = 6, CN = 7;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal sealed class Pending
        {
            public MapEvent Me;
            public BattleSideEnum Side;
            public PartyBase CmdParty;
            public Hero CmdHero;
            public string Cmd = "?";
            public Dictionary<PartyBase, Dictionary<CharacterObject, int[]>> Cnt;
            public HashSet<Hero> Spawned, Kept;
        }

        private static Pending _p;                 // czeka na pojmanie tej bitwy (jedna bitwa gracza naraz)
        private static Pending _move;              // czeka na MapEventEnded tej bitwy
        private static List<MobileParty> _moveParties;
        private static string _hook = "nie wpieta";
        private static int _stumbles, _errs;

        internal static void Set(Pending p) { _p = p; _move = null; _moveParties = null; }

        /// <summary>Nowa misja: stare oczekiwanie (bitwa trwala dalej - ContinueBattle - albo pojmanie sie nie odbylo) znika.</summary>
        internal static void Clear()
        {
            if (_p != null) { try { Log.Info(P + "oczekiwanie bez pojmania (bitwa trwa dalej albo nowa misja) - porzucone, nic nie zmieniono."); } catch { } }
            _p = null; _move = null; _moveParties = null;
        }

        /// <summary>SubModuleMain (menu glowne, raz na proces): prefiks MapEvent.CaptureDefeatedPartyMembers (metoda zadeklarowana w MapEvent - DeclaredMethod).
        /// Bez oczekiwania z misji 458 prefiks wychodzi przy pierwszym porownaniu.</summary>
        internal static void Apply(Harmony h)
        {
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(MapEvent), "CaptureDefeatedPartyMembers");
                if (m == null) _hook = "BRAK MapEvent.CaptureDefeatedPartyMembers";
                else
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(CommanderWithdraw458Map), nameof(CapPre)) { priority = Priority.Last });
                    _hook = "wpieta";
                }
            }
            catch (Exception e) { _hook = "blad " + e.GetType().Name; Log.Error("CommanderWithdraw458Map.Apply", e); }
            try { Log.Info("Odwrot wodza (458): latka rozliczenia - " + _hook + " (prefiks MapEvent.CaptureDefeatedPartyMembers, Priority.Last; dziala tylko po misji z odwrotem wodza)."); } catch { }
        }

        internal static string Hook { get { return _hook; } }

        /// <summary>Prefiks CaptureDefeatedPartyMembers(winnerParties, defeatedParties): partie "z wodzem" rozliczone tutaj, lista gry bez nich.</summary>
        public static void CapPre(MapEvent __instance, MBReadOnlyList<MapEventParty> __0, ref MBReadOnlyList<MapEventParty> __1)
        {
            var p = _p;
            if (p == null || !ReferenceEquals(p.Me, __instance)) return;
            _p = null;
            try
            {
                if (__instance.DefeatedSide != p.Side || __1 == null || __0 == null)
                {
                    Log.Info(P + "strona wodza " + p.Side + " nie jest pokonana (pokonana " + __instance.DefeatedSide + ") - pojmanie jak w grze.");
                    return;
                }
                var keep = new List<MapEventParty>();
                var rest = new MBList<MapEventParty>();
                var restNames = new List<string>();
                int restEsc = 0;
                foreach (var mep in __1)
                {
                    var pb = mep != null ? mep.Party : null;
                    if (pb != null && WithCommander(p, pb)) keep.Add(mep);
                    else
                    {
                        rest.Add(mep);
                        if (pb != null)
                        {
                            int esc = SumEsc(p, pb);
                            restEsc += esc;
                            restNames.Add(Name(pb) + (esc > 0 ? " (zbiegli z wodzem " + esc + " - pan nie uszedl, do niewoli jak w grze)" : ""));
                        }
                    }
                }
                if (keep.Count == 0)
                {
                    Log.Info(P + "zadna partia strony nie uszla z wodzem " + p.Cmd + " - pojmanie jak w grze.");
                    return;
                }
                MBList<KeyValuePair<MapEventParty, float>> wch = null, hch = null;
                Campaign.Current.Models.BattleRewardModel.GetCaptureMemberChancesForWinnerParties(__instance, __0, out wch, out hch);
                var sb = new StringBuilder(512);
                sb.Append(P).Append("wodz ").Append(p.Cmd).Append(" uszedl - partie z wodzem ").Append(keep.Count).Append(", jak w grze ").Append(rest.Count);
                var moved = new List<MobileParty>();
                int tBefore = 0, tAfter = 0, tTaken = 0, tLost = 0, tHero = 0, tBad = 0;
                foreach (var mep in keep)
                {
                    var pb = mep.Party;
                    try
                    {
                        int before, after, taken, lost, heroOut;
                        string line = Settle(__instance, p, pb, wch, hch, out before, out after, out taken, out lost, out heroOut);
                        bool ok = before == after + taken + lost + heroOut;
                        if (!ok) tBad++;
                        tBefore += before; tAfter += after; tTaken += taken; tLost += lost; tHero += heroOut;
                        sb.Append(" || ").Append(line).Append(" | kontrola: przed ").Append(before).Append(" = po ").Append(after).Append(" + do niewoli ").Append(taken)
                          .Append(" + bez odbiorcy ").Append(lost).Append(" + bohaterowie wyjeci ").Append(heroOut).Append(ok ? " OK" : " NIEZGODNE");
                        if (pb.IsMobile && pb.MobileParty != null) moved.Add(pb.MobileParty);
                    }
                    catch (Exception e)
                    {
                        Stumble("Settle " + Name(pb), e);
                        rest.Add(mep);   // potkniecie - te partie rozliczy gra (to, co juz przeniesione, przeszlo zwyklymi AddToCounts)
                        sb.Append(" || ").Append(Name(pb)).Append(": potkniecie - rozliczy gra");
                    }
                }
                __1 = rest;
                _move = p;
                _moveParties = moved;
                if (restNames.Count > 0) sb.Append(" || jak w grze: ").Append(string.Join(", ", restNames.ToArray()));
                sb.Append(" || razem: przed ").Append(tBefore).Append(", zostaje ").Append(tAfter).Append(", do niewoli ").Append(tTaken).Append(", bez odbiorcy ").Append(tLost)
                  .Append(", bohaterowie wyjeci ").Append(tHero).Append(", zbiegli z wodzem w partiach jak w grze ").Append(restEsc)
                  .Append(" | niezgodnych partii ").Append(tBad).Append(" | potkniecia ").Append(_stumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("CapPre", e); }
        }

        /// <summary>Partia uchodzi z wodzem: partia wodza albo jej pan zszedl z pola z wodzem / nie wszedl na pole (i zyje, nie jest jencem).</summary>
        private static bool WithCommander(Pending p, PartyBase pb)
        {
            if (ReferenceEquals(pb, p.CmdParty)) return true;
            var h = pb.LeaderHero;
            if (h == null || !h.IsAlive || h.IsPrisoner) return false;
            return p.Kept.Contains(h) || !p.Spawned.Contains(h);
        }

        private static int[] Get(Pending p, PartyBase pb, CharacterObject c)
        {
            Dictionary<CharacterObject, int[]> d;
            int[] k;
            if (p.Cnt != null && p.Cnt.TryGetValue(pb, out d) && d.TryGetValue(c, out k)) return k;
            return null;
        }

        private static int SumEsc(Pending p, PartyBase pb)
        {
            Dictionary<CharacterObject, int[]> d;
            int n = 0;
            if (p.Cnt != null && p.Cnt.TryGetValue(pb, out d)) foreach (var kv in d) n += kv.Value[CE];
            return n;
        }

        private static MapEventParty Pick(MBList<KeyValuePair<MapEventParty, float>> ch)
        {
            // ta sama droga co MapEvent.FindWinnerPartyToGetCurrentLootObjectBasedOnChances (cs 112580): suma szans < 1 = czasem nikt
            if (ch == null || ch.Count == 0) return null;
            float r = MBRandom.RandomFloat;
            foreach (var kv in ch)
            {
                r -= kv.Value;
                if (r <= 0f) return kv.Key;
            }
            return null;
        }

        /// <summary>Rozliczenie jednej partii z wodzem: plan na elementach skladu, potem zmiany zwyklymi AddToCounts / akcjami gry.</summary>
        private static string Settle(MapEvent me, Pending p, PartyBase pb, MBList<KeyValuePair<MapEventParty, float>> wch, MBList<KeyValuePair<MapEventParty, float>> hch,
                                     out int before, out int after, out int taken, out int lost, out int heroOut)
        {
            taken = lost = heroOut = 0;
            var r = pb.MemberRoster;
            before = r.TotalManCount;
            var model = Campaign.Current.Models.BattleRewardModel;
            int keepEsc = 0, keepRes = 0, keepW0 = 0, capW = 0, capH = 0, skew = 0, heroKept = 0, heroTaken = 0, heroFled = 0;
            // plan (bez zmian)
            var planC = new List<CharacterObject>();
            var planH = new List<int>();
            var planW = new List<int>();
            var heroes = new List<Hero>();
            for (int i = 0; i < r.Count; i++)
            {
                var e = r.GetElementCopyAtIndex(i);
                var c = e.Character;
                if (c == null || e.Number <= 0) continue;
                if (c.IsHero)
                {
                    var h = c.HeroObject;
                    if (h == null) continue;
                    if (p.Kept.Contains(h) || !p.Spawned.Contains(h)) heroKept++;
                    else heroes.Add(h);
                    continue;
                }
                var k = Get(p, pb, c);
                int hs = k != null ? k[CHS] : 0, ws = k != null ? k[CWS] : 0, es = k != null ? k[CE] : 0;
                int wn = Math.Min(e.WoundedNumber, e.Number), hn = e.Number - wn;
                int cw = Math.Min(wn, ws);
                int ch = Math.Max(0, Math.Min(hn, hs - es));
                if (hs > hn || ws > wn) skew++;
                keepEsc += Math.Min(es, hn);
                keepRes += Math.Max(0, hn - ch - Math.Min(es, hn));
                keepW0 += wn - cw;
                if (cw + ch == 0) continue;
                planC.Add(c); planH.Add(ch); planW.Add(cw);
                capW += cw; capH += ch;
            }
            // szeregowi do niewoli (jak gra: kazdy osobno, odbiorca z rzutu; nie-jeniec albo brak odbiorcy - znika jak w grze)
            for (int i = 0; i < planC.Count; i++)
            {
                var c = planC[i];
                int ch = planH[i], cw = planW[i];
                bool can = model.CanTroopBeTakenPrisoner(c);
                for (int j = 0; j < cw; j++)
                {
                    var to = can ? Pick(wch) : null;
                    var recv = to != null ? to.RosterToReceiveLootPrisoners : null;
                    if (recv != null) { recv.AddToCounts(c, 1, false, 1); taken++; } else lost++;
                }
                for (int j = 0; j < ch; j++)
                {
                    var to = can ? Pick(hch) : null;
                    var recv = to != null ? to.RosterToReceiveLootPrisoners : null;
                    if (recv != null) { recv.AddToCounts(c, 1); taken++; } else lost++;
                }
                int idx = r.FindIndexOfTroop(c);
                if (idx >= 0) r.AddToCountsAtIndex(idx, -(ch + cw), -cw, 0, false);
            }
            // bohaterowie, ktorzy nie uszli z wodzem (ranni z pola, zostali): jak gra (CaptureDefeatedPartyMembers, cs 112722-112750)
            foreach (var h in heroes)
            {
                try
                {
                    var c = h.CharacterObject;
                    int idx = r.FindIndexOfTroop(c);
                    if (idx < 0) continue;
                    int n0 = r.TotalManCount;
                    if (h.DeathMark != KillCharacterAction.KillCharacterActionDetail.DiedInBattle && h.DeathMark != KillCharacterAction.KillCharacterActionDetail.DiedInLabor && h.Occupation != Occupation.Special)
                    {
                        if (pb.IsMobile && pb.LeaderHero == h) pb.MobileParty.RemovePartyLeader();
                        if (h.CanBecomePrisoner())
                        {
                            var to = Pick(h.IsWounded ? wch : hch);
                            var recv = to != null ? to.RosterToReceiveLootPrisoners : null;
                            if (recv != null)
                            {
                                var owner = Owner(recv);
                                if (owner != null) TakePrisonerAction.Apply(owner, h);
                                else { recv.AddToCounts(c, 1); idx = r.FindIndexOfTroop(c); if (idx >= 0) r.AddToCountsAtIndex(idx, -1, 0, 0, false); }
                                heroTaken++;
                            }
                        }
                    }
                    idx = r.FindIndexOfTroop(c);
                    if (idx >= 0 && r.GetElementCopyAtIndex(idx).Number > 0 && h.DeathMark == KillCharacterAction.KillCharacterActionDetail.None)
                    {
                        MakeHeroFugitiveAction.Apply(h);
                        heroFled++;
                    }
                    heroOut += Math.Max(0, n0 - r.TotalManCount);
                }
                catch (Exception e) { Stumble("Bohater " + (h.Name != null ? h.Name.ToString() : "?"), e); }
            }
            r.RemoveZeroCounts();
            after = r.TotalManCount;
            return Name(pb) + (ReferenceEquals(pb, p.CmdParty) ? " [wodz]" : "") + ": przed " + before
                   + " | zostaje: zbiegli z wodzem " + keepEsc + ", posilki niewprowadzone " + keepRes + ", ranni sprzed bitwy " + keepW0 + ", bohaterowie " + heroKept
                   + " | do niewoli wg gry: ranni z pola " + capW + ", zdrowi na polu / zeszli bez wodza " + capH + " (wzieci " + taken + ", bez odbiorcy " + lost + ")"
                   + " | bohaterowie bez wodza: pojmani " + heroTaken + ", zbiegli (fugitive) " + heroFled
                   + " | po " + after + (skew > 0 ? " | elementow z liczbami misji ponad sklad " + skew : "");
        }

        /// <summary>MapEventEnded (ArmouryBehavior): partie z wodzem odchodza od zwyciezcy jak strona RetreatingSide w grze (AiMilitaryBehavior.OnMapEventEnded).</summary>
        internal static void OnMapEventEnded(MapEvent me)
        {
            var p = _move;
            if (p == null || me == null || !ReferenceEquals(p.Me, me)) return;
            var list = _moveParties;
            _move = null; _moveParties = null;
            int ok = 0, skip = 0;
            try
            {
                MobileParty from = null;
                Vec2 foe = me.Position.ToVec2();
                try
                {
                    var win = me.GetMapEventSide(me.WinningSide);
                    var lp = win != null ? win.LeaderParty : null;
                    from = lp != null ? lp.MobileParty : null;
                    if (from != null) foe = from.Position.ToVec2();
                }
                catch { from = null; }
                var tp = AccessTools.DeclaredMethod(typeof(MobileParty), "TeleportPartyToOutSideOfEncounterRadius");
                if (list != null)
                {
                    foreach (var mp in list)
                    {
                        try
                        {
                            if (mp == null || !mp.IsActive || mp.IsMainParty || mp.AttachedTo != null) { skip++; continue; }   // MapEvent jeszcze ustawiony (HandleMapEventEnd po nasluchach) - gra w AiMilitaryBehavior tez tego nie sprawdza
                            if (tp != null) tp.Invoke(mp, null);
                            CampaignVec2 pt;
                            mp.Ai.CalculateFleePosition(out pt, from, foe - mp.Position.ToVec2());
                            mp.SetMoveGoToPoint(pt, mp.IsCurrentlyAtSea ? MobileParty.NavigationType.Naval : MobileParty.NavigationType.Default);
                            ok++;
                        }
                        catch (Exception e) { skip++; Stumble("Odejscie", e); }
                    }
                }
                Log.Info(P + "po bitwie odchodzi od zwyciezcy (jak RetreatingSide w grze: poza promien spotkania + punkt ucieczki): partii " + ok + ", pominietych (przypiete do armii / nieczynne) " + skip
                         + (tp == null ? " | BRAK TeleportPartyToOutSideOfEncounterRadius - tylko punkt ucieczki" : "") + ".");
            }
            catch (Exception e) { Stumble("MapEventEnded", e); }
        }

        private static MethodInfo _ownerGet;

        /// <summary>TroopRoster.OwnerParty jest internal - odczyt odbiciem (tylko na mapie, przy pojmaniu bohatera).</summary>
        private static PartyBase Owner(TroopRoster r)
        {
            if (_ownerGet == null) _ownerGet = AccessTools.PropertyGetter(typeof(TroopRoster), "OwnerParty");
            return _ownerGet != null ? _ownerGet.Invoke(r, null) as PartyBase : null;
        }

        private static string Name(PartyBase pb)
        {
            try { return pb != null && pb.Name != null ? pb.Name.ToString() : "?"; } catch { return "?"; }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errs >= 3) return;
            _errs++;
            try { Log.Error("CommanderWithdraw458Map." + where, e); } catch { }
        }
    }
}
