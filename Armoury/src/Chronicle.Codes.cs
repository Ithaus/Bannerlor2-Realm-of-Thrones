using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    internal static partial class Chronicle
    {
        internal static partial class Actions
        {
            // ================================================================ m105-kronika K0 krok B2 - UCHWYTY kod:<nazwa> (KRONIKA-FORMAT 12, kronika_check KODY)
            // Jeden switch po nazwie (RunCode). Uchwyt czyta stan gry i wybor (Ctx.Opt = kr:NNN.w=x), nie tekst okna; sam sprawdza klucz ChronicleWorldChanges
            // (NIE - tylko czesc R albo nic, z linia). Jedna linia "kod <nazwa> | okno NNN.w[war] | ...liczby... | OK/POMINIETA (powod)"; wyjatek - POMINIETA
            // z powodem i potkniecie (okno i reszta skutkow ida dalej). Nieznany uchwyt - POMINIETA "brak uchwytu".
            //  test        (000.1, okno) - tylko linia, bez zmian w swiecie.
            //  zaraza      (076.2, okno) - Blada klacz pod Meereen (town_K1): (2) partie w promieniu ok. 30 jednostek (oblegajacy, obroncy, gracz) - 10-20% zdrowych
            //              zolnierzy kazdej -> ranni (jeden rzut dla wszystkich, bez smierci, umarli nie choruja), (3) bezpieczenstwo town_K1 -5. (1) ludzie miasta 8-12%
            //              przez ksiege ludzi z przyczyna "choroba" - BEZ ZMIAN: ksiega ludzi (PeopleBook) nie ma przyczyny ani drogi miasto -> pula regionu dla Kroniki
            //              (ta sama sytuacja co akcja pop, B1) - linia mowi, ilu ludzi ma miasto.
            //  dzwony      (115.1, okno) - Krolewska Przystan plonie: (3) lup rodu zdobywcy - 30% kasy miasta -> glowa rodu-wlasciciela (zwykla akcja gold - platnik
            //              kasa miasta, linia "zloto"). (1) ludzie 25-35% z przyczyna "pozar" - BEZ ZMIAN (jak wyzej); (2) dobrobyt i zabudowa przez Devastation - BEZ
            //              ZMIAN: Devastation Armoury dziala tylko na wsie (lupienie miasta robi CitySack przy zdobyciu), a wprost Town.Prosperity gryzie sie z DailyLife.
            //  wielka_rada (117.1/117.1a, kazda opcja) - glos gracza a Bran, b gracz, c Jon, d bez glosu: relacje gracza z glowami rodow krolestw Westeros (kolumna
            //              gracz okna 117.1 - termy p:vassal:k) a +3, b -5, c -2, d 0 (czesc R - takze przy World Changes NIE). LEKKA: wynik Bran, bez flag i zmian
            //              wladcow. PELNA (klucz Full Versions TAK): glosy glow rodow = slawa rodu dla kandydata z najwyzsza relacja (Bran zawsze, premia kanonu x1.25;
            //              gracz tylko przy glosie b; Jon tylko zywy i przy glosie c), glos gracza = slawa jego rodu; wynik w linii i flaga k117_wynik_bran|gracz|jon.
            //              Korona bez zmian (wylacznik straznika Prologue.Crown i decyzja K10 - FORMAT 12, plan P5).

            private static readonly string[] CouncilCandidates = { "bran", "gracz", "jon" };

            internal static Res RunCode(string name, Ctx c, out string detail)
            {
                detail = null;
                string line;
                Res r;
                try
                {
                    switch (name)
                    {
                        case "test": r = Res.Done; line = "proba silnika, bez zmian w swiecie | OK"; break;
                        case "zaraza": r = CodePlague(c, out line); break;
                        case "dzwony": r = CodeBells(c, out line); break;
                        case "wielka_rada": r = CodeCouncil(c, out line); break;
                        default: r = Res.Skip; line = "POMINIETA (brak uchwytu w tej wersji silnika)"; break;
                    }
                }
                catch (Exception e) { Stumble("Codes(" + name + ")", e); r = Res.Skip; line = "POMINIETA (wyjatek " + e.GetType().Name + ")"; }
                Log.Info(Tag + "kod " + name + " | okno " + c.Label + " | " + line + ".");
                string inner = line.StartsWith("POMINIETA (", StringComparison.Ordinal) && line.EndsWith(")", StringComparison.Ordinal) ? line.Substring(11, line.Length - 12) : line;
                detail = r == Res.Done ? "uchwyt " + name : (name.Length == 0 || line.StartsWith("POMINIETA (brak uchwytu", StringComparison.Ordinal) ? "brak uchwytu " + name : "uchwyt " + name + " - " + inner);
                return r;
            }

            /// <summary>kod:zaraza (076.2) - opis bloku wyzej.</summary>
            private static Res CodePlague(Ctx c, out string line)
            {
                var st = Terms.FindSettlement("town_K1", null);
                if (st == null || st.Town == null) { line = "POMINIETA (brak miasta town_K1)"; return Res.Skip; }
                if (!KeyWorld) { line = "POMINIETA (klucz Chronicle World Changes NIE - uchwyt zmienia swiat)"; return Res.Skip; }
                float people = 0f;
                try { people = PopulationLaw.PeopleOf(st); } catch (Exception e) { Stumble("Codes.zaraza(ludzie)", e); }
                float roll = 0.10f + 0.10f * MBRandom.RandomFloat;   // ten sam rzut dla gracza i AI (FORMAT 12)
                var pos = st.GetPosition2D;
                int parties = 0, healthy = 0, wounded = 0;
                bool playerHit = false;
                foreach (var mp in MobileParty.All)
                {
                    try
                    {
                        if (mp == null || !mp.IsActive || mp.MemberRoster == null || mp.MemberRoster.Count == 0) continue;
                        if (mp.CurrentSettlement != st && mp.GetPosition2D.Distance(pos) > 30f) continue;
                        if (InBattle(mp)) continue;   // recenzja K0 (styk-api R5): partia w toczacej sie bitwie walczy lista z chwili wejscia - bez rannych w trakcie
                        var ro = mp.MemberRoster;
                        int got = 0;
                        for (int i = 0; i < ro.Count; i++)
                        {
                            var el = ro.GetElementCopyAtIndex(i);
                            if (el.Character == null || el.Character.IsHero || PeopleBook.IsWight(el.Character)) continue;
                            int h = el.Number - el.WoundedNumber;
                            if (h <= 0) continue;
                            healthy += h;
                            int w = (int)Math.Round(h * roll);
                            if (w <= 0) continue;
                            ro.AddToCountsAtIndex(i, 0, w);
                            got += w;
                        }
                        if (got <= 0) continue;
                        parties++; wounded += got;
                        if (mp == MobileParty.MainParty) playerHit = true;
                    }
                    catch (Exception e) { Stumble("Codes.zaraza(partia)", e); }
                }
                float s0 = st.Town.Security;
                st.Town.Security = s0 - 5f;
                if (playerHit) { try { Log.Player("The pale mare spreads through your camp before Meereen. Many of your men are too sick to fight."); } catch { } }
                line = "Meereen " + st.StringId + ": ludzie ok. " + people.ToString("0", Inv) + " bez zmian (ksiega ludzi bez przyczyny choroba - jak pop) | ranni: partii " + parties
                       + " w promieniu 30, rannych " + wounded + " z " + healthy + " zdrowych (rzut " + (roll * 100f).ToString("0", Inv) + "%)" + (playerHit ? ", takze gracz" : "")
                       + " | bezpieczenstwo " + s0.ToString("0.0", Inv) + " -> " + st.Town.Security.ToString("0.0", Inv) + " | OK";
                return Res.Done;
            }

            /// <summary>kod:dzwony (115.1) - opis bloku wyzej. Lup przez zwykla akcje gold (platnik kasa miasta, linia "zloto").</summary>
            private static Res CodeBells(Ctx c, out string line)
            {
                var st = Terms.FindSettlement("town_EW4", null);
                if (st == null || st.Town == null) { line = "POMINIETA (brak miasta town_EW4)"; return Res.Skip; }
                if (!KeyWorld) { line = "POMINIETA (klucz Chronicle World Changes NIE - uchwyt zmienia swiat)"; return Res.Skip; }
                float people = 0f;
                try { people = PopulationLaw.PeopleOf(st); } catch (Exception e) { Stumble("Codes.dzwony(ludzie)", e); }
                var owner = st.OwnerClan;
                var lead = owner != null ? owner.Leader : null;
                string loot;
                Res r = Res.Done;
                if (lead == null || !lead.IsAlive) { loot = "lup POMINIETY (rod-wlasciciel " + (owner != null ? owner.StringId : "-") + " bez zywej glowy)"; r = Res.Skip; }
                else
                {
                    string gd;
                    var gr = Gold(c, new List<string> { "@kasa:town_EW4", lead.StringId, "30%" }, out gd);
                    loot = "lup 30% kasy -> glowa rodu " + owner.StringId + " " + HeroName(lead) + ": " + (gr == Res.Done ? "OK (" + gd + ")" : "POMINIETY (" + gd + ")");
                    if (gr != Res.Done) r = Res.Skip;
                }
                line = "Krolewska Przystan " + st.StringId + " (wlasciciel " + (owner != null ? owner.StringId : "-") + ") | (1) ludzie ok. " + people.ToString("0", Inv)
                       + " bez zmian (ksiega ludzi bez przyczyny pozar) | (2) dobrobyt i zabudowa bez zmian (Devastation tylko dla wsi; lup miasta - CitySack przy zdobyciu) | (3) " + loot
                       + " | " + (r == Res.Done ? "OK" : "POMINIETA (lup nie wszedl)");
                return r;
            }

            /// <summary>Krolestwa Westeros z kolumny gracz okna (termy p:vassal:k).</summary>
            private static List<Kingdom> CouncilKingdoms(Ctx c)
            {
                var l = new List<Kingdom>();
                string wid = c.Id;
                int h = wid.IndexOf('#');
                if (h >= 0) wid = wid.Substring(0, h);
                var w = Data.Window(wid);
                string expr = w != null ? w.Main.Player : "";
                foreach (var part in expr.Split(new[] { '|', '&' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string t = part.Trim();
                    if (!t.StartsWith("p:vassal:", StringComparison.Ordinal)) continue;
                    var k = Terms.FindKingdom(t.Substring(9), null);
                    if (k != null && !k.IsEliminated && !l.Contains(k)) l.Add(k);
                }
                return l;
            }

            /// <summary>kod:wielka_rada (117.1 i 117.1a, kazda opcja) - opis bloku wyzej.</summary>
            private static Res CodeCouncil(Ctx c, out string line)
            {
                char vote = c.Opt;
                if (vote < 'a' || vote > 'd') { line = "POMINIETA (uchwyt tylko w skutkach opcji - brak glosu kr:117.1=x)"; return Res.Skip; }
                var me = Hero.MainHero;
                if (me == null) { line = "POMINIETA (brak gracza)"; return Res.Skip; }
                var ks = CouncilKingdoms(c);
                var heads = new List<Hero>();
                foreach (var k in ks)
                    foreach (var cl in k.Clans)
                        if (cl != null && !cl.IsEliminated && !cl.IsUnderMercenaryService && cl.Leader != null && cl.Leader.IsAlive && cl.Leader != me && !heads.Contains(cl.Leader)) heads.Add(cl.Leader);
                int d = vote == 'a' ? 3 : vote == 'b' ? -5 : vote == 'c' ? -2 : 0;
                if (d != 0) foreach (var hd in heads) { try { ChangeRelationAction.ApplyPlayerRelation(hd, d, false, false); } catch (Exception e) { Stumble("Codes.wielka_rada(relacja)", e); } }
                string cand = vote == 'a' ? "Bran" : vote == 'b' ? "gracz" : vote == 'c' ? "Jon" : "bez glosu";
                var sb = new StringBuilder(300);
                sb.Append("glos gracza ").Append(vote).Append(" (").Append(cand).Append(") | krolestwa ").Append(ks.Count).Append(", glowy rodow ").Append(heads.Count)
                  .Append(", relacja z graczem ").Append(d >= 0 ? "+" : "").Append(d);
                if (!KeyFull)
                {
                    sb.Append(" | LEKKA: wynik Bran (jak w tekscie okna), bez zmian wladcow, rodow i krolestw | OK");
                    line = sb.ToString();
                    return Res.Done;
                }
                // PELNA: glosy wedlug slawy rodu i relacji z kandydatem
                var bran = Terms.FindHero("lord_5_7", null);
                var jon = Terms.FindHero("lord_5_17", null);
                var heroes = new Hero[] { bran != null && bran.IsAlive ? bran : null, vote == 'b' ? me : null, vote == 'c' && jon != null && jon.IsAlive ? jon : null };
                var score = new double[3];
                int voters = 0;
                foreach (var hd in heads)
                {
                    int best = -1, bestRel = int.MinValue;
                    for (int i = 0; i < 3; i++)
                    {
                        if (heroes[i] == null || heroes[i] == hd) continue;
                        int rel = hd.GetRelation(heroes[i]);
                        if (rel > bestRel) { bestRel = rel; best = i; }
                    }
                    if (best < 0) continue;
                    double wgt = hd.Clan != null ? Math.Max(0f, hd.Clan.Renown) : 0.0;
                    score[best] += best == 0 ? wgt * 1.25 : wgt;
                    voters++;
                }
                int mine = vote == 'a' ? 0 : vote == 'b' ? 1 : vote == 'c' ? 2 : -1;
                if (mine >= 0 && heroes[mine] != null && Clan.PlayerClan != null) score[mine] += mine == 0 ? Clan.PlayerClan.Renown * 1.25 : Clan.PlayerClan.Renown;
                int win = -1;
                for (int i = 0; i < 3; i++) if (heroes[i] != null && (win < 0 || score[i] > score[win])) win = i;
                sb.Append(" | PELNA: glosujacych ").Append(voters).Append(", Bran ").Append(score[0].ToString("0", Inv)).Append(heroes[0] == null ? " (nie zyje)" : "")
                  .Append(", gracz ").Append(score[1].ToString("0", Inv)).Append(heroes[1] == null ? " (bez kandydatury)" : "")
                  .Append(", Jon ").Append(score[2].ToString("0", Inv)).Append(heroes[2] == null ? " (bez kandydatury)" : "");
                if (win < 0) { sb.Append(" -> brak kandydata | POMINIETA (Bran nie zyje, brak innej kandydatury)"); line = sb.ToString(); return Res.Skip; }
                string flag = "k117_wynik_" + CouncilCandidates[win];
                State.SetFlag(flag);
                sb.Append(" -> wynik ").Append(CouncilCandidates[win]).Append(", flaga ").Append(flag)
                  .Append("; korona bez zmian (wylacznik straznika Prologue.Crown i decyzja K10 - FORMAT 12, plan P5) | OK");
                line = sb.ToString();
                return Res.Done;
            }
        }
    }
}
