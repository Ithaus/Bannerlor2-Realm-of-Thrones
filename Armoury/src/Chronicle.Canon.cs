using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    internal static partial class Chronicle
    {
        internal static partial class Actions
        {
            // ================================================================ m105-kronika K0 krok B2 - AKCJE KANONU, TYLKO-PELNE, TURNIEJ, ZBIORKA (KRONIKA-FORMAT 8, 11, 15)
            // clan/court/lead/prisoner/free/wander/die/marry/wed - ta sama droga co wiersze #move (CanonCompanions.ApplyAction: postac gracza, ukryta, jeniec, bitwa -
            // jak #move). Dodatkowo z Kroniki: postac z wierszami #move (i druga strona marry/wed) - POMINIETA (zmiany stron tylko przez pare #para, FORMAT 8);
            // die: lista chronionych przed wydarzeniem ROT - POMINIETA (FORMAT 15); glowa rodu albo wladca W CHWILI AKCJI z wiersza lekka/obie - POMINIETA "tylko
            // pelna" (FORMAT 11; z wiersza pelna - smierc, dziedzica wybiera gra).
            // succeed:umierajacy:dziedzic - tylko pelna (wiersz pelna i klucz Chronicle Full Versions TAK), wyjatki lekkie: wydarzenia 42, 49, 52, 80. Wzor Prologue.End:
            // dziedzic (zywy, z rodu umierajacego) zostaje glowa rodu PRZED smiercia (ChangeClanLeaderAction.ApplyWithSelectedNewLeader - wladca krolestwa rodu
            // panujacego idzie za glowa rodu, bez losowania dziedzica i glosowania o tron), potem smierc bez komunikatu jak die (KillCharacterAction.ApplyByMurder).
            // own/ruler/leader/alliance/truce - tylko pelna. alliance - zawsze POMINIETA z powodem (Kronika nie wola StartAlliance: straznik Prologue.Crown pilnuje
            // sojuszu korona-Reach i paktow Dorne, a sojusz spoza listy CanonAlliances CrashScribe zerwalby sie przy wczytaniu - plan P5, decyzja K9/K10).
            // truce - pokoj (MakePeaceAction), gdy ROT nie wymusza wojny tej pary (Prologue.RotWarForced) i gracz nie rzadzi zadna strona.
            // tourney:miasto - turniej kanonu przez GrandTourney (CanonApi.ProclaimRoyal odbiciem - jak Turniej Reki prologu, bez oplat gracza); bez GrandTourney -
            // POMINIETA; turniej juz jest w miescie - OK bez nowego ("gdy nie ma innego"); oblezenie - CZEKA.
            // muster:krolestwo:osada:dni - jednorazowo, miekko: wolne druzyny lordow krolestwa (bez armii, bitwy, oblezenia, morza, scen prologu, rodu gracza) dostaja
            // raz rozkaz "idz do osady" (AI gry moze go zmienic przy nastepnej mysli); rejestr zbiorki (zapis "km") tylko do linii konca: ile druzyn dotarlo.

            private static readonly HashSet<int> SucceedLightOk = new HashSet<int> { 42, 49, 52, 80 };   // FORMAT 11 / kronika_check SUCCEED_LIGHT_OK
            private const float MusterArrive = 8f;   // jednostki mapy od bramy osady zbiorki = na miejscu (linia konca)

            private static Res CanonClan(Ctx c, List<string> a, out string detail) { return CanonAct(c, a, "clan", 2, out detail); }
            private static Res CanonCourt(Ctx c, List<string> a, out string detail) { return CanonAct(c, a, "court", 2, out detail); }
            private static Res CanonLead(Ctx c, List<string> a, out string detail) { return CanonAct(c, a, "lead", 1, out detail); }
            private static Res CanonPrisoner(Ctx c, List<string> a, out string detail) { return CanonAct(c, a, "prisoner", 2, out detail); }
            private static Res CanonFree(Ctx c, List<string> a, out string detail) { return CanonAct(c, a, "free", 2, out detail); }
            private static Res CanonWander(Ctx c, List<string> a, out string detail) { return CanonAct(c, a, "wander", 2, out detail); }
            private static Res CanonDie(Ctx c, List<string> a, out string detail) { return CanonAct(c, a, "die", 1, out detail); }
            private static Res CanonMarry(Ctx c, List<string> a, out string detail) { return CanonAct(c, a, "marry", 2, out detail); }
            private static Res CanonWed(Ctx c, List<string> a, out string detail) { return CanonAct(c, a, "wed", 2, out detail); }

            /// <summary>Akcja kanonu z Kroniki przez CanonCompanions.ApplyAction (opis bloku wyzej). Argument: rod (clan/court/free), rod albo osada (prisoner),
            /// kultura (wander), bohater (marry/wed - tokeny rozwiazane do id, CanonCompanions szuka po id).</summary>
            private static Res CanonAct(Ctx c, List<string> a, string act, int argc, out string detail)
            {
                detail = null;
                if (a.Count != argc || a[0].Length == 0 || (argc == 2 && a[1].Length == 0))
                { detail = act + (argc == 2 ? ":<bohater>:<arg>" : ":<bohater>") + " - zle argumenty"; return Res.Skip; }
                var h = Terms.FindHero(a[0], c.Tok);
                if (h == null) { detail = "bohater " + a[0] + " nie istnieje w grze"; return Res.Skip; }
                if (CanonCompanions.HasMoveRows(h)) { detail = HeroName(h) + " ma wiersze #move - zmiany stron tylko przez CanonCompanions (para #para, FORMAT 8)"; return Res.Skip; }
                string arg = argc == 2 ? a[1] : "";
                if (act == "marry" || act == "wed")
                {
                    var o = Terms.FindHero(arg, c.Tok);
                    if (o == null) { detail = "bohater " + arg + " nie istnieje w grze"; return Res.Skip; }
                    if (CanonCompanions.HasMoveRows(o)) { detail = HeroName(o) + " ma wiersze #move - zmiana tylko przez CanonCompanions (FORMAT 8)"; return Res.Skip; }
                    arg = o.StringId;
                }
                else if (act == "clan" || act == "court" || act == "free")
                {
                    var cl = Terms.FindClan(arg, c.Tok);
                    if (cl == null) { detail = "rod " + arg + " nie istnieje w grze"; return Res.Skip; }
                    arg = cl.StringId;
                }
                else if (act == "prisoner")
                {
                    var st = Terms.FindSettlement(arg, c.Tok);
                    if (st != null) arg = st.StringId;
                    else
                    {
                        var cl = Terms.FindClan(arg, c.Tok);
                        if (cl == null) { detail = "miejsce niewoli " + arg + " - ani osada, ani rod w grze"; return Res.Skip; }
                        arg = cl.StringId;
                    }
                }
                if (act == "die" && h.IsAlive)
                {
                    if ((h.IsClanLeader || h.IsKingdomLeader) && RowVersion(c) != "pelna")
                    {
                        detail = HeroName(h) + " jest teraz " + (h.IsKingdomLeader ? "wladca" : "glowa rodu " + (h.Clan != null ? h.Clan.StringId : "?")) + " - smierc glowy rodu tylko pelna (FORMAT 11; wiersz "
                                 + (RowVersion(c).Length > 0 ? RowVersion(c) : "bez wiersza") + ")";
                        return Res.Skip;
                    }
                    string need;
                    if (CanonCompanions.GuardState(h, out need) == 2) { detail = HeroName(h) + " - lista chronionych, jeszcze przed " + need + " (FORMAT 15)"; return Res.Skip; }
                }
                string why;
                var r = FromCanon(CanonCompanions.ApplyAction(h, act, arg, "Kronika " + c.Label, out why));
                detail = HeroName(h) + ": " + why;
                return r;
            }

            /// <summary>null - akcja tylko-pelna wolna (wiersz pelna i klucz TAK); inaczej powod POMINIETA.</summary>
            private static string FullOnlyWhy(Ctx c, string act)
            {
                string ver = RowVersion(c);
                if (ver != "pelna") return act + " - tylko pelna (FORMAT 11): wiersz " + (ver.Length > 0 ? "wersja " + ver : "bez wiersza okna");
                if (!KeyFull) return act + " - tylko pelna: klucz Chronicle Full Versions NIE";
                return null;
            }

            private static bool Busy(Hero h)
            {
                var p = h != null ? h.PartyBelongedTo : null;
                return p != null && (p.MapEvent != null || p.SiegeEvent != null);
            }

            /// <summary>succeed:&lt;umierajacy&gt;:&lt;dziedzic&gt; - dziedzic glowa rodu (i wladca), potem smierc (opis bloku wyzej).</summary>
            private static Res Succeed(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                if (a.Count != 2 || a[0].Length == 0 || a[1].Length == 0) { detail = "succeed:<umierajacy>:<dziedzic> - zle argumenty"; return Res.Skip; }
                int ev = EventNo(c.Id);
                if (!SucceedLightOk.Contains(ev)) { string fw = FullOnlyWhy(c, "succeed"); if (fw != null) { detail = fw + " (wyjatki lekkie: 42, 49, 52, 80)"; return Res.Skip; } }
                var d = LiveHero(a[0], c, out detail); if (d == null) return Res.Skip;
                var heir = LiveHero(a[1], c, out detail); if (heir == null) return Res.Skip;
                if (d == heir) { detail = "umierajacy = dziedzic"; return Res.Skip; }
                if (CanonCompanions.HasMoveRows(d)) { detail = HeroName(d) + " ma wiersze #move - smierc tylko przez CanonCompanions (FORMAT 8)"; return Res.Skip; }
                if (CanonCompanions.HasMoveRows(heir)) { detail = HeroName(heir) + " ma wiersze #move - zmiana tylko przez CanonCompanions (FORMAT 8)"; return Res.Skip; }
                string ps = CanonCompanions.PlayerSideOf(d);
                if (ps != null) { detail = HeroName(d) + ": " + ps; return Res.Skip; }
                ps = CanonCompanions.PlayerSideOf(heir);
                if (ps != null) { detail = "dziedzic " + HeroName(heir) + ": " + ps; return Res.Skip; }
                if (d.IsDisabled || CanonCompanions.IsHidden(d)) { detail = HeroName(d) + " ukryty albo wylaczony - bez sukcesji"; return Res.Skip; }
                if (heir.IsDisabled || CanonCompanions.IsHidden(heir)) { detail = "dziedzic " + HeroName(heir) + " ukryty albo wylaczony"; return Res.Skip; }
                string need;
                if (CanonCompanions.GuardState(d, out need) == 2) { detail = HeroName(d) + " - lista chronionych, jeszcze przed " + need + " (FORMAT 15)"; return Res.Skip; }
                var clan = d.Clan;
                if (clan == null || clan.IsEliminated) { detail = HeroName(d) + " bez rodu"; return Res.Skip; }
                if (heir.Clan != clan) { detail = "dziedzic " + HeroName(heir) + " poza rodem " + clan.StringId + " (rod " + (heir.Clan != null ? heir.Clan.StringId : "-") + ")"; return Res.Skip; }
                if (Busy(d) || Busy(heir)) { detail = "umierajacy albo dziedzic w bitwie albo oblezeniu"; return Res.Wait; }
                var k = clan.Kingdom;
                string ruler0 = k != null && k.Leader != null ? HeroName(k.Leader) : "-", lead0 = clan.Leader != null ? HeroName(clan.Leader) : "-", how;
                if (clan.Leader == d)
                {
                    ChangeClanLeaderAction.ApplyWithSelectedNewLeader(clan, heir);
                    how = clan.Leader == heir ? "dziedzic glowa rodu " + clan.StringId + " (" + lead0 + " -> " + HeroName(heir) + ")" : "zmiana glowy rodu NIE weszla (glowa " + (clan.Leader != null ? HeroName(clan.Leader) : "-") + ")";
                }
                else how = "umierajacy nie jest glowa rodu (glowa " + lead0 + ") - bez zmiany glowy";
                KillCharacterAction.ApplyByMurder(d, null, false);   // jak die: smierc bez komunikatu (w bitwie - znak smierci)
                string ruler1 = k != null && k.Leader != null ? HeroName(k.Leader) : "-";
                detail = how + " | " + HeroName(d) + (d.IsDead ? " nie zyje" : " znak smierci - umrze po bitwie") + (k != null ? " | wladca " + k.StringId + ": " + ruler0 + " -> " + ruler1 : "")
                         + (SucceedLightOk.Contains(ev) ? " (wyjatek lekki " + ev + ")" : "");
                return Res.Done;
            }

            /// <summary>own:&lt;osada&gt;:&lt;rod&gt; - wlasciciel miasta/zamku (tylko pelna). Osada gracza i rod gracza - POMINIETA; oblezenie - CZEKA.</summary>
            private static Res Own(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                if (a.Count != 2) { detail = "own:<osada>:<rod> - zle argumenty"; return Res.Skip; }
                string fw = FullOnlyWhy(c, "own"); if (fw != null) { detail = fw; return Res.Skip; }
                var st = Terms.FindSettlement(a[0], c.Tok);
                if (st == null) { detail = "osada " + a[0] + " nie istnieje w grze"; return Res.Skip; }
                if (!st.IsFortification || st.Town == null) { detail = "own tylko dla miasta albo zamku (" + st.StringId + ")"; return Res.Skip; }
                var cl = Terms.FindClan(a[1], c.Tok);
                if (cl == null || cl.IsEliminated) { detail = "rod " + a[1] + (cl == null ? " nie istnieje w grze" : " wyeliminowany"); return Res.Skip; }
                if (cl.Leader == null || !cl.Leader.IsAlive) { detail = "rod " + cl.StringId + " bez zywej glowy"; return Res.Skip; }
                if (st.OwnerClan == cl) { detail = "osada " + st.StringId + " juz nalezy do " + cl.StringId + " - bez zmian"; return Res.Done; }
                if (st.OwnerClan == Clan.PlayerClan) { detail = "osada " + st.StringId + " nalezy do gracza - Kronika jej nie zabiera"; return Res.Skip; }
                if (cl == Clan.PlayerClan) { detail = "rod gracza - Kronika nie daje osad graczowi"; return Res.Skip; }
                if (st.SiegeEvent != null || (st.Party != null && st.Party.MapEvent != null)) { detail = "osada " + st.StringId + " oblezona albo w bitwie"; return Res.Wait; }
                string before = st.OwnerClan != null ? st.OwnerClan.StringId : "-";
                ChangeOwnerOfSettlementAction.ApplyByDefault(cl.Leader, st);
                detail = "osada " + st.StringId + ": " + before + " -> " + (st.OwnerClan != null ? st.OwnerClan.StringId : "-") + " (krolestwo " + (st.MapFaction != null ? st.MapFaction.StringId : "-") + ")";
                return st.OwnerClan == cl ? Res.Done : Res.Skip;
            }

            /// <summary>ruler:&lt;krolestwo&gt;:&lt;rod&gt; - rod rzadzacy (tylko pelna; rod w krolestwie, bez gracza).</summary>
            private static Res Ruler(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                if (a.Count != 2) { detail = "ruler:<krolestwo>:<rod> - zle argumenty"; return Res.Skip; }
                string fw = FullOnlyWhy(c, "ruler"); if (fw != null) { detail = fw; return Res.Skip; }
                var k = Terms.FindKingdom(a[0], c.Tok);
                if (k == null || k.IsEliminated) { detail = "krolestwo " + a[0] + (k == null ? " nie istnieje w grze" : " wyeliminowane"); return Res.Skip; }
                var cl = Terms.FindClan(a[1], c.Tok);
                if (cl == null || cl.IsEliminated) { detail = "rod " + a[1] + " nie istnieje w grze albo wyeliminowany"; return Res.Skip; }
                if (cl.Kingdom != k) { detail = "rod " + cl.StringId + " poza krolestwem " + k.StringId; return Res.Skip; }
                if (k.RulingClan == cl) { detail = "rod " + cl.StringId + " juz rzadzi " + k.StringId + " - bez zmian"; return Res.Done; }
                if (k.RulingClan == Clan.PlayerClan || cl == Clan.PlayerClan) { detail = "gracz rzadzi albo mialby rzadzic " + k.StringId + " - Kronika nie zmienia"; return Res.Skip; }
                string before = k.RulingClan != null ? k.RulingClan.StringId : "-";
                ChangeRulingClanAction.Apply(k, cl);
                detail = "krolestwo " + k.StringId + ": rod rzadzacy " + before + " -> " + (k.RulingClan != null ? k.RulingClan.StringId : "-");
                return k.RulingClan == cl ? Res.Done : Res.Skip;
            }

            /// <summary>leader:&lt;rod&gt;:&lt;bohater&gt; - glowa rodu (tylko pelna; bohater z rodu, bez rodu gracza).</summary>
            private static Res Leader(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                if (a.Count != 2) { detail = "leader:<rod>:<bohater> - zle argumenty"; return Res.Skip; }
                string fw = FullOnlyWhy(c, "leader"); if (fw != null) { detail = fw; return Res.Skip; }
                var cl = Terms.FindClan(a[0], c.Tok);
                if (cl == null || cl.IsEliminated) { detail = "rod " + a[0] + " nie istnieje w grze albo wyeliminowany"; return Res.Skip; }
                if (cl == Clan.PlayerClan) { detail = "rod gracza - Kronika nie zmienia jego glowy"; return Res.Skip; }
                var h = LiveHero(a[1], c, out detail); if (h == null) return Res.Skip;
                if (h.Clan != cl) { detail = HeroName(h) + " poza rodem " + cl.StringId; return Res.Skip; }
                if (h.IsDisabled || CanonCompanions.IsHidden(h)) { detail = HeroName(h) + " ukryty albo wylaczony"; return Res.Skip; }
                if (cl.Leader == h) { detail = HeroName(h) + " juz glowa rodu " + cl.StringId + " - bez zmian"; return Res.Done; }
                string before = cl.Leader != null ? HeroName(cl.Leader) : "-";
                ChangeClanLeaderAction.ApplyWithSelectedNewLeader(cl, h);
                detail = "rod " + cl.StringId + ": glowa " + before + " -> " + (cl.Leader != null ? HeroName(cl.Leader) : "-");
                return cl.Leader == h ? Res.Done : Res.Skip;
            }

            /// <summary>alliance:&lt;k1&gt;:&lt;k2&gt; - zawsze POMINIETA z powodem (plan P5, opis bloku wyzej); kontrola argumentow i wersji jak inne tylko-pelne.</summary>
            private static Res Alliance(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                if (a.Count != 2) { detail = "alliance:<k1>:<k2> - zle argumenty"; return Res.Skip; }
                string fw = FullOnlyWhy(c, "alliance"); if (fw != null) { detail = fw; return Res.Skip; }
                bool nv;
                bool reach = false, dorne = false;
                try { reach = Prologue.CrownGuardHolds("reach", out nv); dorne = Prologue.CrownGuardHolds("dorne", out nv); } catch (Exception e) { Stumble("Actions.Alliance(straznik)", e); }
                if (reach || dorne)
                    detail = "straznik Prologue.Crown pilnuje " + (reach ? "sojuszu korona-Reach" : "") + (reach && dorne ? " i " : "") + (dorne ? "paktow Dorne" : "") + " - Kronika nie zawiera sojuszy (plan P5)";
                else
                    detail = "Kronika nie wola StartAlliance - sojusz " + a[0] + "-" + a[1] + " spoza listy CanonAlliances (CrashScribe) zerwany przy wczytaniu; wylacznik straznika i wpis - decyzja K9/K10 (plan P5)";
                return Res.Skip;
            }

            /// <summary>truce:&lt;k1&gt;:&lt;k2&gt; - pokoj (tylko pelna): obie strony w grze i w wojnie, bez wojny wymuszonej przez ROT, gracz nie rzadzi zadna strona.</summary>
            private static Res Truce(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                if (a.Count != 2) { detail = "truce:<k1>:<k2> - zle argumenty"; return Res.Skip; }
                string fw = FullOnlyWhy(c, "truce"); if (fw != null) { detail = fw; return Res.Skip; }
                var k1 = Terms.FindKingdom(a[0], c.Tok);
                var k2 = Terms.FindKingdom(a[1], c.Tok);
                if (k1 == null || k1.IsEliminated) { detail = "krolestwo " + a[0] + " nie istnieje w grze albo wyeliminowane"; return Res.Skip; }
                if (k2 == null || k2.IsEliminated) { detail = "krolestwo " + a[1] + " nie istnieje w grze albo wyeliminowane"; return Res.Skip; }
                if (k1 == k2) { detail = "to samo krolestwo"; return Res.Skip; }
                if (!FactionManager.IsAtWarAgainstFaction(k1, k2)) { detail = k1.StringId + " i " + k2.StringId + " nie sa w wojnie - bez zmian"; return Res.Done; }
                var pk = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
                if (pk != null && (pk == k1 || pk == k2) && pk.RulingClan == Clan.PlayerClan) { detail = "gracz rzadzi " + pk.StringId + " - o pokoju decyduje gracz"; return Res.Skip; }
                bool forced = false;
                try { forced = Prologue.RotWarForced(k1, k2); } catch (Exception e) { Stumble("Actions.Truce(ROT)", e); }
                if (forced) { detail = "wojna fabularna ROT " + k1.StringId + "-" + k2.StringId + " wymuszona - Kronika nie zmienia wojen ROT (FORMAT 11)"; return Res.Skip; }
                MakePeaceAction.Apply(k1, k2);
                bool war = FactionManager.IsAtWarAgainstFaction(k1, k2);
                detail = k1.StringId + " - " + k2.StringId + ": " + (war ? "pokoj NIE wszedl (inny mod albo gra)" : "pokoj");
                return war ? Res.Skip : Res.Done;
            }

            /// <summary>tourney:&lt;miasto&gt; - turniej kanonu przez GrandTourney (opis bloku wyzej).</summary>
            private static Res Tourney(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                if (a.Count != 1 || a[0].Length == 0) { detail = "tourney:<miasto> - zle argumenty"; return Res.Skip; }
                var st = Terms.FindSettlement(a[0], c.Tok);
                if (st == null) { detail = "osada " + a[0] + " nie istnieje w grze"; return Res.Skip; }
                if (!st.IsTown || st.Town == null) { detail = "turniej tylko w miescie (" + st.StringId + ")"; return Res.Skip; }
                var mgr = Campaign.Current != null ? Campaign.Current.TournamentManager : null;
                if (mgr == null) { detail = "brak menedzera turniejow"; return Res.Skip; }
                if (mgr.GetTournamentGame(st.Town) != null) { detail = "turniej juz jest w " + st.StringId + " - bez nowego (FORMAT 11: gdy nie ma innego)"; return Res.Done; }
                if (st.SiegeEvent != null || st.Town.InRebelliousState) { detail = "miasto " + st.StringId + " oblezone albo w buncie"; return Res.Wait; }
                if (!StoryActions.GtReady((w, e) => Stumble("Actions.Tourney." + w, e)) || StoryActions.GtProclaim == null)
                { detail = "brak GrandTourney (CanonApi) - turniej Kroniki tylko przez GrandTourney, bez nowej zaleznosci Armoury"; return Res.Skip; }
                string r;
                try { r = Convert.ToString(StoryActions.GtProclaim.Invoke(null, new object[] { st.StringId, new string[0] }), Inv); }
                catch (TargetInvocationException e) { r = "BLAD " + (e.InnerException ?? e).GetType().Name; Stumble("Actions.Tourney", e.InnerException ?? e); }
                bool ok = mgr.GetTournamentGame(st.Town) != null;
                detail = "GrandTourney w " + st.StringId + ": " + r + (ok ? "" : " - turniej nie powstal");
                if (ok) { try { Log.Player("A great tourney is proclaimed at " + (st.Name != null ? st.Name.ToString() : st.StringId) + ". Lords gather, and any knight may ride."); } catch { } }
                return ok ? Res.Done : Res.Skip;
            }

            // ---------------------------------------------------------------- zbiorka (muster)

            private sealed class MusterRow
            {
                public string K = "", St = "", Src = "";
                public int Day, Until;
                public readonly List<string> Parties = new List<string>();
            }

            private static readonly List<MusterRow> _musters = new List<MusterRow>();

            internal static int MusterCount { get { return _musters.Count; } }

            private static void MusterReset() { _musters.Clear(); }

            /// <summary>muster:&lt;krolestwo&gt;:&lt;osada&gt;:&lt;dni 1-60&gt; - jednorazowy, miekki rozkaz zbiorki (opis bloku wyzej).</summary>
            private static Res Muster(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                int days;
                if (a.Count != 3 || !Num(a[2], 1, 60, out days)) { detail = "muster:<krolestwo>:<osada>:<dni 1-60> - zle argumenty"; return Res.Skip; }
                var k = Terms.FindKingdom(a[0], c.Tok);
                if (k == null || k.IsEliminated) { detail = "krolestwo " + a[0] + (k == null ? " nie istnieje w grze" : " wyeliminowane"); return Res.Skip; }
                var st = Terms.FindSettlement(a[1], c.Tok);
                if (st == null) { detail = "osada " + a[1] + " nie istnieje w grze"; return Res.Skip; }
                if (st.SiegeEvent != null) { detail = "osada zbiorki " + st.StringId + " oblezona"; return Res.Skip; }
                var row = new MusterRow { K = k.StringId, St = st.StringId, Src = c.Label, Day = Terms.Day() };
                row.Until = row.Day + days;
                int lords = 0, busy = 0;
                foreach (var w in k.WarPartyComponents)
                {
                    var mp = w != null ? w.MobileParty : null;
                    if (mp == null || !mp.IsActive || !mp.IsLordParty || mp == MobileParty.MainParty) continue;
                    var lh = mp.LeaderHero;
                    if (lh == null || lh.Clan == Clan.PlayerClan || lh.IsPrisoner) continue;
                    lords++;
                    if (mp.Army != null || mp.MapEvent != null || mp.SiegeEvent != null || mp.IsCurrentlyAtSea || mp.IsDisbanding || mp.CurrentSettlement == st || Prologue.Scripted(mp)) { busy++; continue; }
                    try { mp.SetMoveGoToSettlement(st, MobileParty.NavigationType.Default, false); row.Parties.Add(mp.StringId); }
                    catch (Exception e) { Stumble("Actions.Muster(" + mp.StringId + ")", e); }
                }
                if (row.Parties.Count == 0) { detail = "brak wolnych druzyn lordow " + k.StringId + " (druzyn " + lords + ", zajete " + busy + ": armie, bitwy, oblezenia, morze)"; return Res.Skip; }
                for (int i = _musters.Count - 1; i >= 0; i--)
                    if (_musters[i].K == row.K) { Log.Info(Tag + "zbiorka " + _musters[i].K + " -> " + _musters[i].St + " (" + _musters[i].Src + ") zastapiona nowa (" + c.Label + ")."); _musters.RemoveAt(i); }
                _musters.Add(row);
                detail = "krolestwo " + k.StringId + " -> " + st.StringId + ": rozkaz zbiorki dla " + row.Parties.Count + " druzyn (z " + lords + ", zajete " + busy + "), jednorazowo - AI moze zmienic, linia konca dzien " + row.Until;
                return Res.Done;
            }

            /// <summary>Doba (WorldDaily): koniec zbiorki - linia "zbiorka ... koniec: na miejscu a z n".</summary>
            private static void MusterDaily(int day)
            {
                if (_musters.Count == 0) return;
                for (int i = 0; i < _musters.Count; i++)
                {
                    var r = _musters[i];
                    if (day < r.Until) continue;
                    _musters.RemoveAt(i); i--;
                    try
                    {
                        var st = Terms.FindSettlement(r.St, null);
                        int at = 0, gone = 0;
                        var ids = new HashSet<string>(r.Parties, StringComparer.Ordinal);
                        var seen = new HashSet<string>(StringComparer.Ordinal);
                        foreach (var mp in MobileParty.AllLordParties)
                        {
                            if (mp == null || mp.StringId == null || !ids.Contains(mp.StringId) || !mp.IsActive) continue;
                            seen.Add(mp.StringId);
                            if (st != null && (mp.CurrentSettlement == st || mp.GetPosition2D.Distance(st.GatePosition.ToVec2()) <= MusterArrive)) at++;
                        }
                        gone = r.Parties.Count - seen.Count;
                        Log.Info(Tag + "zbiorka " + r.K + " -> " + r.St + " (" + r.Src + ", od dnia " + r.Day + ") koniec: na miejscu " + at + " z " + r.Parties.Count + " druzyn"
                                 + (gone > 0 ? ", bez druzyny " + gone : "") + " (rozkaz jednorazowy - reszta wedlug AI gry).");
                    }
                    catch (Exception e) { Stumble("Actions.MusterDaily(" + r.K + ")", e); }
                }
            }

            /// <summary>Zapis "km krolestwo osada zrodlo dzien do druzyny(,)".</summary>
            private static void MusterExport(StringBuilder sb)
            {
                foreach (var r in _musters)
                    sb.Append("km\t").Append(r.K).Append('\t').Append(r.St).Append('\t').Append(r.Src.Replace('\t', ' ')).Append('\t').Append(r.Day.ToString(Inv)).Append('\t')
                      .Append(r.Until.ToString(Inv)).Append('\t').Append(string.Join(",", r.Parties.ToArray())).Append('\n');
            }

            private static void MusterImport(string s)
            {
                foreach (var raw in s.Split('\n'))
                {
                    var p = raw.TrimEnd('\r').Split('\t');
                    if (p[0] != "km" || p.Length < 7 || p[1].Length == 0 || p[2].Length == 0) continue;
                    int d, u;
                    if (!int.TryParse(p[4], NumberStyles.AllowLeadingSign, Inv, out d) || !int.TryParse(p[5], NumberStyles.AllowLeadingSign, Inv, out u)) continue;
                    if (_musters.Count >= 32) break;
                    var r = new MusterRow { K = p[1], St = p[2], Src = p[3], Day = d, Until = u };
                    foreach (var id in p[6].Split(',')) if (id.Length > 0 && r.Parties.Count < 200) r.Parties.Add(id);
                    _musters.Add(r);
                }
            }
        }
    }
}
