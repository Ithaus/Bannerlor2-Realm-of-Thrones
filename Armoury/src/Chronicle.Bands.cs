using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    internal static partial class Chronicle
    {
        /// <summary>Najwyzej tyle band Kroniki naraz na mapie (plan F MaxBands, prog KW-14).</summary>
        internal const int MaxBands = 12;

        /// <summary>NoticeBoard (listy goncze kapitana strazy): banda Kroniki z kolumna list = 0 nie dostaje listu gonczego (FORMAT 6). Bez band Kroniki - false.</summary>
        internal static bool NoBounty(MobileParty mp)
        {
            try { return Actions.BandNoBounty(mp); }
            catch { return false; }
        }

        internal static partial class Actions
        {
            // ================================================================ m105-kronika K0 krok B2 - BANDY KRONIKI (KRONIKA-FORMAT 6, plan P6, G "banda ...", H KW-14)
            // band:bNNN - banda z sekcji #banda przy osadzie z kolumny osada, LUDZIE Z PULI wyrzutkow okregu (StoryActions.SpawnBand = wzor m104-e5: OutlawLaw.RaidDraw,
            // najpierw pula, potem domy wsi okregu - nikt z niczego; za malo ludzi - banda nie powstaje, ludzie zostaja w puli), kiesa 0, pochodzenie i ksiega ludzi.
            // Klan: bandycki z Clan.BanditFactions (looters, sea_raiders, wildlings, desert_bandits ...); klan spoza band (brave_companions, faith_militant,
            // sons_of_the_harpy - w ROT pomniejsze frakcje, nie bandy) - banda pod klanem looters z nazwa z kolumny herszt (CreateLooterParty tylko dla klanow
            // bandyckich - linia mowi o zamianie). Sufity: MaxBands band Kroniki naraz i wspolny sufit band fabularnych okregu (StoryActions.RegionCap - najazdy Wolnych
            // Ludzi, sceny E5 prologu i Kronika). Ta sama banda (bNNN) dwa razy naraz - nie. Zachowanie: rabunek - AI band gry wokol osady domowej (napada podroznych,
            // tabory, karawany), patrol - spokojniejsza (Aggressiveness 0.5) z rozkazem patrolu wokol osady, oboz - prawie bez zaczepek (0.1) wokol osady.
            // Zycie: po dni dniach banda wraca do domow ta sama droga co najazdy Wolnych Ludzi (FreeFolkRaids.Home: jency-bohaterowie uciekaja, zloto do kiesy wsi,
            // towar do jej magazynu, ludzie do puli okregu) - wies = osada domowa albo pierwsza wies warowni; w bitwie - nastepna doba; w zadaniu gracza - zostaje zwykla banda.
            // Rozbita przez GRACZA (strona wygrana bitwy z partia gracza, MapEventEnded) - raz skutki po_rozbiciu (zwykla droga akcji, kontekst bNNN) i zadania "defeat".
            // Zniknela bez gracza - linia, zadania "defeat" nieudane. List gonczy: NoticeBoard.Watch obejmuje kazda bande sam; list = 0 - Chronicle.NoBounty wyklucza.
            // Zapis "kb" (w arm_kronika). Partia po wczytaniu - po StringId (gra zapisuje bande jak kazda inna).

            private sealed class BandRow
            {
                public string Id = "", Party = "", Src = "", Home = "", Region = "";
                public int Day, Until, Men0;
                public bool Notice, Routed;       // Routed - rozbita przez gracza (po_rozbiciu juz wykonane)
                public MobileParty P;             // sesja (bez zapisu)
            }

            private static readonly List<BandRow> _bands = new List<BandRow>();

            internal static int BandCount { get { return _bands.Count; } }

            private static void BandsReset() { _bands.Clear(); }

            private static void BandStumble(string where, Exception e) { Stumble("Bands." + where, e); }

            private static MobileParty BandParty(BandRow r, ref Dictionary<string, MobileParty> map)
            {
                var mp = r.P;
                if (mp == null || mp.StringId != r.Party)
                {
                    if (map == null) map = StoryActions.BanditsById();
                    map.TryGetValue(r.Party, out mp);
                    r.P = mp;
                }
                return mp != null && mp.IsActive ? mp : null;
            }

            /// <summary>Zywa partia bandy Kroniki bNNN (null - nie ma jej na mapie).</summary>
            internal static MobileParty BandLive(string bandId)
            {
                if (_bands.Count == 0 || string.IsNullOrEmpty(bandId)) return null;
                Dictionary<string, MobileParty> map = null;
                foreach (var r in _bands) if (r.Id == bandId) { var mp = BandParty(r, ref map); if (mp != null) return mp; }
                return null;
            }

            internal static bool BandNoBounty(MobileParty mp)
            {
                if (_bands.Count == 0 || mp == null || mp.StringId == null || !mp.StringId.StartsWith("armoury_kr_", StringComparison.Ordinal)) return false;
                foreach (var r in _bands) if (r.Party == mp.StringId) return !r.Notice;
                return false;
            }

            /// <summary>band:&lt;bNNN&gt; - banda z #banda (opis bloku wyzej). Linia "banda bNNN 'nazwa' (id partii) | ludzi ... | region r: band fabularnych m/sufit".</summary>
            private static Res Band(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                if (a.Count != 1 || a[0].Length == 0) { detail = "band:<bNNN> - zle argumenty"; return Res.Skip; }
                var def = Data.Band(a[0]);
                if (def == null) { detail = "brak bandy " + a[0] + " w #banda (paczka wylaczona albo zly wiersz)"; return Res.Skip; }
                Dictionary<string, MobileParty> map = null;
                int alive = 0;
                foreach (var r in _bands)
                {
                    var p = BandParty(r, ref map);
                    if (p == null) continue;
                    alive++;
                    if (r.Id == def.Id) { detail = "banda " + def.Id + " juz na mapie (" + r.Party + ", od dnia " + r.Day + ") - bez drugiej"; return Res.Done; }
                }
                if (alive >= MaxBands) { detail = "sufit " + MaxBands + " band Kroniki naraz (na mapie " + alive + ")"; return Res.Skip; }
                var home = Terms.FindSettlement(def.Settlement, c.Tok);
                if (home == null) { detail = "osada " + def.Settlement + " nie istnieje w grze"; return Res.Skip; }
                var region = OutlawLaw.RegionFor(home);
                if (region == null) { detail = "brak okregu (OutlawLaw) dla osady " + home.StringId; return Res.Skip; }
                if (!OutlawLaw.On) { detail = "prawo wyrzutkow wylaczone (OutlawLawEnabled) - bez puli ludzi, banda z niczego nie powstaje"; return Res.Skip; }
                int inRegion = StoryActions.StoryBandsIn(region);
                if (inRegion >= StoryActions.RegionCap)
                { detail = "sufit band fabularnych okregu " + region.StringId + " (" + inRegion + "/" + StoryActions.RegionCap + ": najazdy, sceny prologu, Kronika)"; return Res.Skip; }
                var clan = StoryActions.BanditClan(def.Clan);
                string clanNote = "";
                if (clan == null)
                {
                    clan = StoryActions.BanditClan("looters");
                    clanNote = " (klan " + def.Clan + " poza klanami bandyckimi gry - banda pod klanem looters, nazwa z kolumny herszt)";
                }
                if (clan == null) { detail = "brak klanu bandyckiego " + def.Clan + " i zapasowego looters"; return Res.Skip; }
                string name = TextOf(def.Leader, null);
                if (string.IsNullOrEmpty(name)) name = Data.Plain(def.Leader);
                int want = def.Min + MBRandom.RandomInt(def.Max - def.Min + 1);
                string note;
                var mp = StoryActions.SpawnBand("armoury_kr_" + def.Id, clan, home, region, want, def.Min, name, null, out note, BandStumble);
                if (mp == null) { detail = "banda " + def.Id + " nie powstala (chciana " + want + ", min " + def.Min + ") - " + note; return Res.Skip; }
                try
                {
                    if (def.Behavior == "patrol") { mp.Aggressiveness = 0.5f; mp.SetMovePatrolAroundSettlement(home, MobileParty.NavigationType.Default, false); }
                    else if (def.Behavior == "oboz") { mp.Aggressiveness = 0.1f; mp.SetMovePatrolAroundSettlement(home, MobileParty.NavigationType.Default, false); }
                }
                catch (Exception e) { BandStumble("Zachowanie(" + def.Id + ")", e); }
                int day = Terms.Day();
                var row = new BandRow
                {
                    Id = def.Id, Party = mp.StringId, Src = c.Label, Home = home.StringId, Region = region.StringId, Day = day, Until = day + def.Days,
                    Men0 = mp.MemberRoster.TotalRegulars, Notice = def.Notice, P = mp
                };
                _bands.Add(row);
                Log.Info(Tag + "banda " + def.Id + " '" + name + "' (" + mp.StringId + ") | ludzi " + note + " | klan " + clan.StringId + clanNote + " | przy " + home.StringId
                         + " | region " + region.StringId + ": band fabularnych " + (inRegion + 1) + "/" + StoryActions.RegionCap + " | zachowanie " + def.Behavior
                         + " | do dnia " + row.Until + " (" + def.Days + " dni) | list gonczy " + YN(def.Notice) + " | bandy Kroniki " + (alive + 1) + "/" + MaxBands + " | " + c.Label + ".");
                detail = mp.StringId + ", " + row.Men0 + " ludzi z puli okregu " + region.StringId + ", do dnia " + row.Until;
                return Res.Done;
            }

            /// <summary>Doba (WorldDaily): banda zniknela - wpis usuniety (linia; bez gracza - zadania defeat nieudane); koniec terminu - powrot do domow
            /// (FreeFolkRaids.Home), w bitwie - nastepna doba, w zadaniu gracza - zwykla banda.</summary>
            private static void BandsDaily(int day)
            {
                if (_bands.Count == 0) return;
                Dictionary<string, MobileParty> map = null;
                for (int i = 0; i < _bands.Count; i++)
                {
                    var r = _bands[i];
                    try
                    {
                        var mp = BandParty(r, ref map);
                        if (mp == null)
                        {
                            _bands.RemoveAt(i); i--;
                            Log.Info(Tag + "banda " + r.Id + " (" + r.Party + ") zniknela z mapy - " + (r.Routed ? "rozbita przez gracza (po_rozbiciu juz wykonane)" : "rozbita bez gracza albo rozwiazana przez gre")
                                     + " (od dnia " + r.Day + ", " + r.Src + ").");
                            if (!r.Routed) TasksBandGone(r.Id, "banda " + r.Id + " zniknela bez gracza");
                            continue;
                        }
                        if (day < r.Until) continue;
                        if (mp.MapEvent != null || mp.SiegeEvent != null) continue;   // w bitwie - nastepna doba
                        _bands.RemoveAt(i); i--;
                        if (mp.IsCurrentlyUsedByAQuest)
                        {
                            Log.Info(Tag + "banda " + r.Id + " (" + r.Party + ") koniec terminu - zostaje zwykla banda (w zadaniu gracza gry).");
                            if (!r.Routed) TasksBandGone(r.Id, "banda " + r.Id + " - koniec terminu");
                            continue;
                        }
                        var home = Terms.FindSettlement(r.Home, null);
                        var region = Terms.FindSettlement(r.Region, null);
                        Settlement vil = home != null && home.IsVillage ? home : null;
                        if (vil == null && home != null && home.BoundVillages != null) foreach (var v in home.BoundVillages) if (v != null && v.Settlement != null) { vil = v.Settlement; break; }
                        if (vil == null && region != null && region.BoundVillages != null) foreach (var v in region.BoundVillages) if (v != null && v.Settlement != null) { vil = v.Settlement; break; }
                        if (vil == null || vil.Village == null || region == null)
                        {
                            Log.Info(Tag + "banda " + r.Id + " (" + r.Party + ") koniec terminu - zostaje zwykla banda (brak wsi powrotu przy " + r.Home + ").");
                            if (!r.Routed) TasksBandGone(r.Id, "banda " + r.Id + " - koniec terminu");
                            continue;
                        }
                        int men = mp.MemberRoster.TotalRegulars;
                        long g, it;
                        int back = FreeFolkRaids.Home(mp, vil, region, out g, out it);
                        if (back < 0) Log.Info(Tag + "banda " + r.Id + " (" + r.Party + ") koniec terminu - zostaje zwykla banda (prawo wyrzutkow wylaczone).");
                        else Log.Info(Tag + "banda " + r.Id + " (" + r.Party + ") koniec terminu (dzien " + day + ") - wraca do domow: " + back + " ludzi (z " + men + ") do puli okregu "
                                      + region.StringId + ", lup " + g + " zl i " + it + " szt. do wsi " + vil.StringId + ".");
                        if (!r.Routed) TasksBandGone(r.Id, "banda " + r.Id + " - koniec terminu (rozwiazana)");
                    }
                    catch (Exception e) { BandStumble("Daily(" + r.Id + ")", e); }
                }
            }

            /// <summary>CampaignEvents.MapEventEnded (watek gry, strony bitwy jeszcze pelne - jak FreeFolkRaids.OnMapEventEnded): banda Kroniki po stronie przegranej,
            /// partia gracza po stronie wygranej - rozbita przez gracza: raz skutki po_rozbiciu i zadania defeat. Bez band Kroniki - od razu wyjscie.</summary>
            internal static void OnMapEventEnded(MapEvent me)
            {
                if (_bands.Count == 0 || me == null) return;
                try
                {
                    var win = me.WinningSide;
                    if (win == BattleSideEnum.None) return;
                    var lose = win == BattleSideEnum.Attacker ? me.DefenderSide : me.AttackerSide;
                    var wins = win == BattleSideEnum.Attacker ? me.AttackerSide : me.DefenderSide;
                    if (lose == null || wins == null || lose.Parties == null || wins.Parties == null) return;
                    List<BandRow> hit = null;
                    foreach (var mep in lose.Parties)
                    {
                        var mp = mep != null && mep.Party != null ? mep.Party.MobileParty : null;
                        if (mp == null || mp.StringId == null) continue;
                        foreach (var r in _bands) if (!r.Routed && r.Party == mp.StringId) { (hit ?? (hit = new List<BandRow>())).Add(r); break; }
                    }
                    if (hit == null) return;
                    bool player = false;
                    foreach (var mep in wins.Parties) if (mep != null && mep.Party == PartyBase.MainParty) { player = true; break; }
                    foreach (var r in hit)
                    {
                        if (!player)
                        {
                            var lead = me.GetLeaderParty(win);
                            Log.Info(Tag + "banda " + r.Id + " (" + r.Party + ") przegrala bitwe bez gracza (wygral " + (lead != null && lead.Name != null ? lead.Name.ToString() : "-") + ") - po_rozbiciu nie.");
                            continue;
                        }
                        r.Routed = true;
                        var def = Data.Band(r.Id);
                        string name = def != null ? TextOf(def.Leader, null) : r.Id;
                        Log.Info(Tag + "banda " + r.Id + " '" + name + "' (" + r.Party + ") ROZBITA przez gracza (dzien " + Terms.Day() + ", od dnia " + r.Day + ") | po_rozbiciu "
                                 + (def != null ? def.AfterRout.Length + " akcji" : "brak definicji") + ".");
                        try { if (!string.IsNullOrEmpty(name)) Log.Player(name + " is broken. Word of it will spread."); } catch { }
                        if (def != null && def.AfterRout.Length > 0) Run(new Ctx { Id = r.Id, RowKey = "" }, def.AfterRout);
                        TasksBandDefeated(r.Id);
                    }
                }
                catch (Exception e) { BandStumble("OnMapEventEnded", e); }
            }

            /// <summary>Zapis "kb id partia zrodlo osada okreg dzien do ludzi list rozbita".</summary>
            private static void BandsExport(StringBuilder sb)
            {
                foreach (var r in _bands)
                    sb.Append("kb\t").Append(r.Id).Append('\t').Append(r.Party).Append('\t').Append(r.Src.Replace('\t', ' ')).Append('\t').Append(r.Home).Append('\t').Append(r.Region)
                      .Append('\t').Append(r.Day.ToString(Inv)).Append('\t').Append(r.Until.ToString(Inv)).Append('\t').Append(r.Men0.ToString(Inv)).Append('\t')
                      .Append(r.Notice ? '1' : '0').Append('\t').Append(r.Routed ? '1' : '0').Append('\n');
            }

            private static void BandsImport(string s)
            {
                foreach (var raw in s.Split('\n'))
                {
                    var p = raw.TrimEnd('\r').Split('\t');
                    if (p[0] != "kb" || p.Length < 11 || p[1].Length == 0 || p[2].Length == 0) continue;
                    int d, u, m;
                    if (!int.TryParse(p[6], NumberStyles.AllowLeadingSign, Inv, out d) || !int.TryParse(p[7], NumberStyles.AllowLeadingSign, Inv, out u)
                        || !int.TryParse(p[8], NumberStyles.Integer, Inv, out m)) continue;
                    if (_bands.Count >= MaxBands * 2) break;
                    _bands.Add(new BandRow { Id = p[1], Party = p[2], Src = p[3], Home = p[4], Region = p[5], Day = d, Until = u, Men0 = m, Notice = p[9] == "1", Routed = p[10] == "1" });
                }
            }
        }
    }
}
