using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
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
    ///    obejmuje tez nasze pojmanie, wiec ksiegi widza je jak pojmanie gry): partie "z wodzem" = partia wodza + partie, ktorych pan uszedl z nim albo
    ///    (zdrowy) nie wszedl na pole - posilki czekajace. Te partie wyjmujemy z listy gry i rozliczamy sami TAK SAMO jak gra (te same szanse, ten sam rzut
    ///    odbiorcy, CanTroopBeTakenPrisoner, bohaterowie jak w grze), ale tylko ludzi, ktorzy NIE zeszli z wodzem: na element skladu WSZYSCY ranni (z pola i
    ///    sprzed bitwy - decyzja 2B "wygrany grabi rannych"), zdrowi do niewoli = zdrowi wystawieni, ktorzy wrocili do skladu (zeszli z pola bez wodza albo stali
    ///    na koniec), minus zbiegli z wodzem. Zostaja: zbiegli z wodzem i posilki niewprowadzone (zdrowi skladu minus wystawieni, ktorzy wrocili), bohaterowie
    ///    zbiegli z wodzem / zdrowi niewprowadzeni. Pozostale partie strony - gra (ich zbiegli tez).
    ///    458-p: przed rozliczeniem kontrola licznikow misji z licznikami gry (MapEventParty.DiedInBattle / WoundedInBattle / RoutedInBattle na postac);
    ///    rozjazd = partia do listy gry (rozlicza ja gra), bez zgadywania.
    ///    458-s: RBM (RBMAI.AiModule.SpawningPatches.SetRoutedPatch, prefiks PlayerEncounter.CheckIfBattleShouldContinueAfterBattleMission, decout/full/rbmai.cs
    ///    12650-12676) po KAZDEJ wygranej gracza w polu wola MapEventSide.OnTroopWounded na wszystkich deskryptorach pokonanej strony (GetAllTroops =
    ///    gotowi + przydzieleni do misji, takze zabici / ranni / wypisani, cs 114243); MapEventParty.OnTroopWounded (cs 113444) dopisuje WoundedInBattle +1
    ///    i WoundTroop w skladzie (AddToCountsAtIndex przycina rannych do liczby - cs 96536), wiec zdrowi skladu (zbiegli z wodzem, posilki) staja sie rannymi.
    ///    Dlatego migawka PRZED RBM (nasz prefiks tej samej metody, Priority.First): sklad (liczba / ranni) i liczniki gry na postac; kontrola licznikow
    ///    porownuje misje z migawka, a rozliczenie partii z wodzem bierze rannych z migawki (ranni dopisani przez RBM w tej partii cofnieci - zostaja zdrowi).
    ///    Partie jak w grze - bez zmian (RBM dziala na nie jak dotad).
    ///  - MapEventEnded (nasluch ArmouryBehavior): partie "z wodzem" (ruchome, nie przypiete do armii) odchodza jak strona z RetreatingSide w grze
    ///    (AiMilitaryBehavior.OnMapEventEnded, cs 220459): TeleportPartyToOutSideOfEncounterRadius + Ai.CalculateFleePosition od wodza zwyciezcow + SetMoveGoToPoint.
    ///    RetreatingSide NIE ustawiamy (ratowalby caly sklad, wylaczal lup DTE i drogi H3 / 312 / A17).
    /// Kontrola: na partie przed = po + do niewoli + bez odbiorcy (jak w grze) + bohaterowie wyjeci; niezgodnosc liczona. Potkniecie na partie - ta partia wraca
    /// do listy gry (rozliczy ja gra), bez globalnego wylacznika.
    /// </summary>
    internal static partial class CommanderWithdraw458Map
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
            // 458-s: migawka przed RBM SetRoutedPatch - na postac: [0] liczba w skladzie, [1] ranni w skladzie, [2] DiedInBattle, [3] WoundedInBattle, [4] RoutedInBattle
            public Dictionary<PartyBase, Dictionary<CharacterObject, int[]>> Pre;
        }

        private static Pending _p;                 // czeka na pojmanie tej bitwy (jedna bitwa gracza naraz)
        private static Pending _move;              // czeka na MapEventEnded tej bitwy
        private static List<MobileParty> _moveParties;
        private static string _hook = "nie wpieta";
        private static string _hookPre = "nie wpieta";   // 458-s: migawka przed RBM (prefiks PlayerEncounter.CheckIfBattleShouldContinueAfterBattleMission)
        private static int _stumbles, _errs;
        private static bool _changed;   // 458-p: Settle zaczal zmieniac sklad (potkniecie potem nie oddaje partii grze)

        internal static void Set(Pending p) { _p = p; _move = null; _moveParties = null; _tab = null; }

        /// <summary>458-p (recenzja B9): nowa kampania / wczytanie (konstruktor ArmouryBehavior) - stan i liczniki od zera.</summary>
        internal static void Reset() { _p = null; _move = null; _moveParties = null; _stumbles = 0; _errs = 0; _tab = null; _tabHidden = null; GearClear(); }

        /// <summary>Nowa misja: stare oczekiwanie (bitwa trwala dalej - ContinueBattle - albo pojmanie sie nie odbylo) znika.</summary>
        internal static void Clear()
        {
            if (_p != null) { try { Log.Info(P + "oczekiwanie bez pojmania (bitwa trwa dalej albo nowa misja) - porzucone, nic nie zmieniono."); } catch { } }
            _p = null; _move = null; _moveParties = null; _tab = null;   // 458-t: zbior taboru poprzedniej bitwy tez
            GearClear();   // 458-u: sprzet zbieglych poprzedniej bitwy (nowa misja - konstruktor 458, przed StartRule)
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
            try
            {
                // 458-s: metoda prywatna zadeklarowana w PlayerEncounter (cs 99447) - DeclaredMethod; Priority.First = przed RBM SetRoutedPatch (prefiks bez priorytetu)
                var m2 = AccessTools.DeclaredMethod(typeof(PlayerEncounter), "CheckIfBattleShouldContinueAfterBattleMission");
                if (m2 == null) _hookPre = "BRAK PlayerEncounter.CheckIfBattleShouldContinueAfterBattleMission";
                else
                {
                    h.Patch(m2, prefix: new HarmonyMethod(typeof(CommanderWithdraw458Map), nameof(ContPre)) { priority = Priority.First });
                    _hookPre = "wpieta";
                }
            }
            catch (Exception e) { _hookPre = "blad " + e.GetType().Name; Log.Error("CommanderWithdraw458Map.Apply (migawka)", e); }
            try { Log.Info("Odwrot wodza (458): latka rozliczenia - " + _hook + " (prefiks MapEvent.CaptureDefeatedPartyMembers, Priority.Last; dziala tylko po misji z odwrotem wodza); migawka przed RBM - " + _hookPre + " (prefiks PlayerEncounter.CheckIfBattleShouldContinueAfterBattleMission, Priority.First)."); } catch { }
        }

        internal static string Hook { get { return _hook; } }

        /// <summary>458-s: prefiks PlayerEncounter.CheckIfBattleShouldContinueAfterBattleMission (Priority.First, przed RBM SetRoutedPatch): migawka skladu i licznikow
        /// gry partii strony wodza, raz na oczekiwanie (DoWait moze wolac metode wiecej razy - RBM dopisuje przy kazdym). Tylko odczyt.</summary>
        public static void ContPre()
        {
            var p = _p;
            if (p == null || p.Pre != null) return;
            try
            {
                MapEvent me = null;
                try { me = PlayerEncounter.Battle; } catch { me = null; }
                if (me == null || !ReferenceEquals(me, p.Me)) return;
                var side = me.GetMapEventSide(p.Side);
                if (side == null || side.Parties == null) return;
                var pre = new Dictionary<PartyBase, Dictionary<CharacterObject, int[]>>();
                foreach (var mep in side.Parties)
                {
                    var pb = mep != null ? mep.Party : null;
                    if (pb == null || pre.ContainsKey(pb)) continue;
                    var d = new Dictionary<CharacterObject, int[]>();
                    var r = pb.MemberRoster;
                    for (int i = 0; i < r.Count; i++)
                    {
                        var e = r.GetElementCopyAtIndex(i);
                        var c = e.Character;
                        if (c == null || c.IsHero || e.Number <= 0) continue;
                        int[] v;
                        if (!d.TryGetValue(c, out v)) { v = new int[5]; d[c] = v; }
                        v[0] += e.Number; v[1] += Math.Min(e.WoundedNumber, e.Number);
                    }
                    AddGame(d, mep.DiedInBattle, 2, 5);
                    AddGame(d, mep.WoundedInBattle, 3, 5);
                    AddGame(d, mep.RoutedInBattle, 4, 5);
                    pre[pb] = d;
                }
                p.Pre = pre;
            }
            catch (Exception e) { Stumble("Migawka", e); }
        }

        /// <summary>Prefiks CaptureDefeatedPartyMembers(winnerParties, defeatedParties): partie "z wodzem" rozliczone tutaj, lista gry bez nich.</summary>
        public static void CapPre(MapEvent __instance, MBReadOnlyList<MapEventParty> __0, ref MBReadOnlyList<MapEventParty> __1)
        {
            var p = _p;
            if (p == null || !ReferenceEquals(p.Me, __instance)) return;
            _p = null;
            try
            {
                var cfg = Settings.Current;
                if (cfg == null || !cfg.CommanderWithdrawsUnderRearguard) { Log.Info(P + "klucz Commander Withdraws Under Rearguard NIE - oczekiwanie porzucone, pojmanie jak w grze."); return; }   // recenzja A4
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
                    GearLine(__instance);   // 458-u: sprzet zbieglych - partie jak w grze (bez zwrotu)
                    return;
                }
                MBList<KeyValuePair<MapEventParty, float>> wch = null, hch = null;
                Campaign.Current.Models.BattleRewardModel.GetCaptureMemberChancesForWinnerParties(__instance, __0, out wch, out hch);
                var sb = new StringBuilder(512);
                sb.Append(P).Append("wodz ").Append(p.Cmd).Append(" uszedl - partie z wodzem ").Append(keep.Count).Append(", jak w grze ").Append(rest.Count)
                  .Append(" | migawka przed RBM ").Append(p.Pre != null ? "TAK (partii " + p.Pre.Count + ")" : "BRAK (" + _hookPre + ") - kontrola na licznikach biezacych");
                var moved = new List<MobileParty>();
                int tBefore = 0, tAfter = 0, tTaken = 0, tLost = 0, tHero = 0, tBad = 0, tGame = 0;
                foreach (var mep in keep)
                {
                    var pb = mep.Party;
                    _changed = false;
                    try
                    {
                        string bad = GameCheck(p, mep);
                        if (bad != null)
                        {
                            tGame++;
                            rest.Add(mep);
                            TaborDrop(pb);   // 458-t: zbrojownia DTE i migawka Spoils jak w grze
                            sb.Append(" || ").Append(Name(pb)).Append(": liczniki misji niezgodne z gra (").Append(bad).Append(") - rozliczy gra");
                            continue;
                        }
                        int before, after, taken, lost, heroOut;
                        string line = Settle(__instance, p, pb, wch, hch, out before, out after, out taken, out lost, out heroOut) + RbmNote(p, mep);
                        bool ok = before == after + taken + lost + heroOut;
                        if (!ok) tBad++;
                        tBefore += before; tAfter += after; tTaken += taken; tLost += lost; tHero += heroOut;
                        sb.Append(" || ").Append(line).Append(" | kontrola: przed ").Append(before).Append(" = po ").Append(after).Append(" + do niewoli ").Append(taken)
                          .Append(" + bez odbiorcy ").Append(lost).Append(" + bohaterowie wyjeci ").Append(heroOut).Append(ok ? " OK" : " NIEZGODNE");
                        if (pb.IsMobile && pb.MobileParty != null) moved.Add(pb.MobileParty);
                        GearBack(__instance, pb);   // 458-u: sprzet zbieglych z wodzem do zbrojowni DTE tej partii
                    }
                    catch (Exception e)
                    {
                        Stumble("Settle " + Name(pb), e);
                        if (!_changed)
                        {
                            rest.Add(mep);   // potkniecie przed zmiana skladu - te partie rozliczy gra
                            TaborDrop(pb);   // 458-t
                            sb.Append(" || ").Append(Name(pb)).Append(": potkniecie przed zmianami - rozliczy gra");
                        }
                        else
                        {
                            // recenzja B5: sklad juz zmieniony - oddanie grze wzieloby ludzi drugi raz; partia zostaje jak jest (linia mowi to wprost)
                            tBad++;
                            if (pb.IsMobile && pb.MobileParty != null) moved.Add(pb.MobileParty);
                            GearBack(__instance, pb);   // 458-u: partia zostaje (zbiegli w niej) - ich sprzet tez
                            sb.Append(" || ").Append(Name(pb)).Append(": potkniecie PO zmianach skladu - partia zostaje jak jest (nie oddana grze, bez podwojenia)");
                        }
                    }
                }
                __1 = rest;
                _move = p;
                _moveParties = moved;
                if (restNames.Count > 0) sb.Append(" || jak w grze: ").Append(string.Join(", ", restNames.ToArray()));
                sb.Append(" || razem: przed ").Append(tBefore).Append(", zostaje ").Append(tAfter).Append(", do niewoli ").Append(tTaken).Append(", bez odbiorcy ").Append(tLost)
                  .Append(", bohaterowie wyjeci ").Append(tHero).Append(", zbiegli z wodzem w partiach jak w grze ").Append(restEsc)
                  .Append(" | niezgodnych partii ").Append(tBad).Append(", oddanych grze po kontroli licznikow ").Append(tGame).Append(" | potkniecia ").Append(_stumbles).Append('.');
                Log.Info(sb.ToString());
                GearLine(__instance);   // 458-u
            }
            catch (Exception e) { Stumble("CapPre", e); }
        }

        /// <summary>Partia uchodzi z wodzem: partia wodza albo jej pan zszedl z pola z wodzem / zdrowy nie wszedl na pole (posilki; i zyje, nie jest jencem).</summary>
        private static bool WithCommander(Pending p, PartyBase pb)
        {
            if (ReferenceEquals(pb, p.CmdParty)) return true;
            var h = pb.LeaderHero;
            if (h == null || !h.IsAlive || h.IsPrisoner) return false;
            return HeroStays(p, h);
        }

        /// <summary>Bohater zostaje: zszedl z pola z wodzem albo ZDROWY nie wszedl na pole (posilki czekajace - decyzja 3). Ranny sprzed bitwy nie byl
        /// wystawiony, ale posilkami nie jest - idzie jak w grze (recenzja B1).</summary>
        private static bool HeroStays(Pending p, Hero h)
        {
            return p.Kept.Contains(h) || (!p.Spawned.Contains(h) && !h.IsWounded);
        }

        /// <summary>458-p (recenzja B3): liczniki misji (zabici / ranni z pola / wypisani na postac) == liczniki gry tej partii (MapEventParty.DiedInBattle /
        /// WoundedInBattle / RoutedInBattle). null = zgodne; inaczej opis rozjazdu (np. wczesniejsza runda tej samej bitwy albo agent bez licznika).</summary>
        private static string GameCheck(Pending p, MapEventParty mep)
        {
            var g = new Dictionary<CharacterObject, int[]>();
            var pre = PreOf(p, mep.Party);
            int bad = 0;
            string first = null;
            if (pre != null)
            {
                // 458-s: liczniki gry z migawki przed RBM SetRoutedPatch (te same pola gry, przed dopisaniem przez RBM); sklad teraz = sklad z migawki
                // (liczba na postac rowna, rannych nie mniej - RBM tylko rani), inaczej ktos inny zmienil sklad miedzy migawka a pojmaniem - partia do gry
                foreach (var kv in pre)
                {
                    var v = kv.Value;
                    if (v[2] + v[3] + v[4] > 0) g[kv.Key] = new int[] { v[2], v[3], v[4] };
                }
                var now = new Dictionary<CharacterObject, int[]>();
                var r = mep.Party.MemberRoster;
                for (int i = 0; i < r.Count; i++)
                {
                    var e = r.GetElementCopyAtIndex(i);
                    var c = e.Character;
                    if (c == null || c.IsHero || e.Number <= 0) continue;
                    int[] v;
                    if (!now.TryGetValue(c, out v)) { v = new int[2]; now[c] = v; }
                    v[0] += e.Number; v[1] += Math.Min(e.WoundedNumber, e.Number);
                }
                foreach (var kv in now)
                {
                    int[] q;
                    pre.TryGetValue(kv.Key, out q);
                    int n0 = q != null ? q[0] : 0, w0 = q != null ? q[1] : 0;
                    if (kv.Value[0] != n0 || kv.Value[1] < w0)
                    {
                        bad++;
                        if (first == null) first = (kv.Key.Name != null ? kv.Key.Name.ToString() : "?") + ": sklad przy migawce " + n0 + " (rannych " + w0 + "), teraz " + kv.Value[0] + " (rannych " + kv.Value[1] + ")";
                    }
                }
                foreach (var kv in pre)
                {
                    if (now.ContainsKey(kv.Key) || kv.Value[0] <= 0) continue;
                    bad++;
                    if (first == null) first = (kv.Key.Name != null ? kv.Key.Name.ToString() : "?") + ": sklad przy migawce " + kv.Value[0] + ", teraz 0";
                }
                // 458-s-p (recenzja 2): dowod, ze po migawce przybylo TYLKO rannych (RBM) - biezace DiedInBattle i RoutedInBattle rowne migawce,
                // WoundedInBattle nie mniejsze; inaczej ktos zabil/wypisal po migawce - partia do gry
                var cur = new Dictionary<CharacterObject, int[]>();
                AddGame(cur, mep.DiedInBattle, 0, 3);
                AddGame(cur, mep.WoundedInBattle, 1, 3);
                AddGame(cur, mep.RoutedInBattle, 2, 3);
                foreach (var kv in cur)
                {
                    int[] q;
                    pre.TryGetValue(kv.Key, out q);
                    int d0 = q != null ? q[2] : 0, w0 = q != null ? q[3] : 0, r0 = q != null ? q[4] : 0;
                    if (kv.Value[0] != d0 || kv.Value[2] != r0 || kv.Value[1] < w0)
                    {
                        bad++;
                        if (first == null) first = (kv.Key.Name != null ? kv.Key.Name.ToString() : "?") + ": licznik gry przy migawce " + d0 + "/" + w0 + "/" + r0 + ", teraz " + kv.Value[0] + "/" + kv.Value[1] + "/" + kv.Value[2];
                    }
                }
                foreach (var kv in pre)
                {
                    if (cur.ContainsKey(kv.Key) || kv.Value[2] + kv.Value[3] + kv.Value[4] == 0) continue;
                    bad++;
                    if (first == null) first = (kv.Key.Name != null ? kv.Key.Name.ToString() : "?") + ": licznik gry przy migawce " + kv.Value[2] + "/" + kv.Value[3] + "/" + kv.Value[4] + ", teraz 0/0/0";
                }
            }
            else
            {
                AddGame(g, mep.DiedInBattle, 0, 3);
                AddGame(g, mep.WoundedInBattle, 1, 3);
                AddGame(g, mep.RoutedInBattle, 2, 3);
            }
            Dictionary<CharacterObject, int[]> d;
            if (p.Cnt == null || !p.Cnt.TryGetValue(mep.Party, out d)) d = new Dictionary<CharacterObject, int[]>();
            foreach (var kv in g)
            {
                int[] k;
                d.TryGetValue(kv.Key, out k);
                int ck = k != null ? k[CK] : 0, cw = k != null ? k[CWS] : 0, cr = k != null ? k[CRm] : 0;
                if (ck != kv.Value[0] || cw != kv.Value[1] || cr != kv.Value[2])
                {
                    bad++;
                    if (first == null) first = (kv.Key.Name != null ? kv.Key.Name.ToString() : "?") + ": gra " + kv.Value[0] + "/" + kv.Value[1] + "/" + kv.Value[2] + ", misja " + ck + "/" + cw + "/" + cr;
                }
            }
            foreach (var kv in d)
            {
                if (g.ContainsKey(kv.Key)) continue;
                if (kv.Value[CK] + kv.Value[CWS] + kv.Value[CRm] == 0) continue;
                bad++;
                if (first == null) first = (kv.Key.Name != null ? kv.Key.Name.ToString() : "?") + ": gra 0/0/0, misja " + kv.Value[CK] + "/" + kv.Value[CWS] + "/" + kv.Value[CRm];
            }
            return bad == 0 ? null : "postaci " + bad + ", np. " + first + " (zabici/ranni/wypisani)" + (pre != null ? " [gra = migawka przed RBM]" : " [gra = liczniki biezace]");
        }

        private static Dictionary<CharacterObject, int[]> PreOf(Pending p, PartyBase pb)
        {
            Dictionary<CharacterObject, int[]> d;
            if (p.Pre != null && pb != null && p.Pre.TryGetValue(pb, out d)) return d;
            return null;
        }

        /// <summary>458-s: ile rannych dopisano tej partii po migawce (RBM SetRoutedPatch): licznik gry WoundedInBattle (zostaje - lup cial gry, cs 112447,
        /// juz go uzyl przed pojmaniem, o ile nie wyciety przez BattlefieldLaw.CutCasualtyLootPrefix; czyta go tez perk BattleHardened, cs 169110) i ranni w skladzie (cofnieci w Settle).</summary>
        private static string RbmNote(Pending p, MapEventParty mep)
        {
            var pre = PreOf(p, mep.Party);
            if (pre == null) return "";
            int w0 = 0, w1 = 0;
            foreach (var kv in pre) w0 += kv.Value[3];
            var r = mep.WoundedInBattle;
            if (r != null) for (int i = 0; i < r.Count; i++) { var e = r.GetElementCopyAtIndex(i); if (e.Character != null && !e.Character.IsHero && e.Number > 0) w1 += e.Number; }
            return " | licznik gry WoundedInBattle: przy migawce " + w0 + ", przy pojmaniu " + w1 + " (dopisane po misji " + (w1 - w0) + " - RBM SetRoutedPatch; licznik bez zmian)";
        }

        private static void AddGame(Dictionary<CharacterObject, int[]> g, TaleWorlds.CampaignSystem.Roster.TroopRoster r, int slot, int size)
        {
            if (r == null) return;
            for (int i = 0; i < r.Count; i++)
            {
                var e = r.GetElementCopyAtIndex(i);
                var c = e.Character;
                if (c == null || c.IsHero || e.Number <= 0) continue;
                int[] v;
                if (!g.TryGetValue(c, out v)) { v = new int[size]; g[c] = v; }
                v[slot] += e.Number;
            }
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
            int keepEsc = 0, keepRes = 0, capW = 0, capH = 0, skew = 0, heroKept = 0, heroTaken = 0, heroFled = 0, rbmW = 0;
            var pre = PreOf(p, pb);
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
                    if (HeroStays(p, h)) heroKept++;
                    else heroes.Add(h);
                    continue;
                }
                var k = Get(p, pb, c);
                int hs = k != null ? k[CHS] : 0, es = k != null ? k[CE] : 0;
                int curW = Math.Min(e.WoundedNumber, e.Number);
                int wn = curW;
                int[] q;
                // 458-s: ranni z migawki przed RBM (GameCheck sprawdzil: liczba jak w migawce, rannych nie mniej) - dopisani przez RBM to zdrowi skladu
                if (pre != null && pre.TryGetValue(c, out q)) wn = Math.Max(0, Math.Min(q[1], curW));
                rbmW += curW - wn;
                int hn = e.Number - wn;
                int cw = wn;   // 458-p (recenzja B2): wszyscy ranni do zwyciezcy - z pola i sprzed bitwy (decyzja 2B: zostaja TYLKO ci, co zeszli z wodzem + posilki)
                int ch = Math.Max(0, Math.Min(hn, hs - es));
                if (hs > hn) skew++;
                int ke = Math.Min(es, hn - ch);   // recenzja B12: zbiegli w skladzie po odjeciu bioracych do niewoli
                keepEsc += ke;
                keepRes += Math.Max(0, hn - ch - ke);
                if (cw + ch == 0 && curW == 0) continue;   // 458-s: element z rannymi od RBM tez w planie - ranni wyzerowani (zostaja sami zdrowi)
                planC.Add(c); planH.Add(ch); planW.Add(cw);
                capW += cw; capH += ch;
            }
            // szeregowi do niewoli (jak gra: kazdy osobno, odbiorca z rzutu; nie-jeniec albo brak odbiorcy - znika jak w grze)
            // 458-p (recenzja B5): najpierw zdjecie ze skladu, potem rozdanie - kazdy czlowiek osobno (potkniecie = "bez odbiorcy", jak brak odbiorcy w grze);
            // nikt nie jest naraz w skladzie i u zwyciezcy
            for (int i = 0; i < planC.Count; i++)
            {
                var c = planC[i];
                int ch = planH[i], cw = planW[i];
                bool can = model.CanTroopBeTakenPrisoner(c);
                int idx = r.FindIndexOfTroop(c);
                if (idx < 0) { skew++; continue; }
                _changed = true;
                // 458-s: wszyscy prawdziwi ranni (cw) ida do zwyciezcy, wiec zostajacy sa zdrowi - ranni elementu do zera (cofa tez rannych dopisanych przez RBM;
                // bez migawki curW = cw, czyli jak dotad -cw)
                var en = r.GetElementCopyAtIndex(idx);
                r.AddToCountsAtIndex(idx, -(ch + cw), -Math.Min(en.WoundedNumber, en.Number), 0, false);
                for (int j = 0; j < cw; j++)
                {
                    try
                    {
                        var to = can ? Pick(wch) : null;
                        var recv = to != null ? to.RosterToReceiveLootPrisoners : null;
                        if (recv != null) { recv.AddToCounts(c, 1, false, 1); taken++; } else lost++;
                    }
                    catch (Exception e) { lost++; Stumble("Jeniec ranny", e); }
                }
                for (int j = 0; j < ch; j++)
                {
                    try
                    {
                        var to = can ? Pick(hch) : null;
                        var recv = to != null ? to.RosterToReceiveLootPrisoners : null;
                        if (recv != null) { recv.AddToCounts(c, 1); taken++; } else lost++;
                    }
                    catch (Exception e) { lost++; Stumble("Jeniec zdrowy", e); }
                }
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
                    _changed = true;
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
                   + " | zostaje: zbiegli z wodzem " + keepEsc + ", posilki niewprowadzone " + keepRes + ", bohaterowie " + heroKept
                   + " | do niewoli wg gry: ranni (z pola i sprzed bitwy) " + capW + ", zdrowi na polu / zeszli bez wodza " + capH + " (wzieci " + taken + ", bez odbiorcy " + lost + ")"
                   + " | bohaterowie bez wodza: pojmani " + heroTaken + ", zbiegli (fugitive) " + heroFled
                   + " | po " + after + (skew > 0 ? " | elementow z liczbami misji ponad sklad " + skew : "")
                   + (pre != null ? " | ranni w skladzie dopisani przez RBM po misji (cofnieci - to zdrowi) " + rbmW : "");
        }

        /// <summary>MapEventEnded (ArmouryBehavior): partie z wodzem odchodza od zwyciezcy jak strona RetreatingSide w grze (AiMilitaryBehavior.OnMapEventEnded).</summary>
        internal static void OnMapEventEnded(MapEvent me)
        {
            // 458-t: linia taboru tu tylko bez okna DTE (z oknem pisze ja finalizer DTE - sluchacz DTE biegnie po naszym)
            if (_tab != null && me != null && ReferenceEquals(_tab.Me, me) && _hookTabDte != "wpieta") TabLine("MapEventEnded, bez okna DTE");
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
