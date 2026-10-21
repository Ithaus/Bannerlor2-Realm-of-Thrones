using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// m104-e5 pkt 6 - LUDZIE GORY W DORZECZU od dnia 155 (klucz PrologueMountainRaiders; projekt pkt 2: "grabiezcy Lannisterow w Dorzeczu (spalone wsie)").
    /// Bez wojny krolestw: banda klanu bandyckiego "robber_knights" (Robber Knights - klan bandycki jest w stalej wojnie z kazdym krolestwem: DefaultDiplomacyModel.
    /// GetShallowDiplomaticStance, wiec rabunek wsi Dorzecza idzie zwykla mechanika gry - EncounterManager: ShortTermBehavior RaidSettlement + IsAtWarAgainstFaction;
    /// precedens gry: bandy dezerterow w zadaniu ExtortionByDeserters), nazwa "The Mountain's Men", sztandar Lannisterow (Party.SetCustomBanner - rod clan_vlandia_1).
    /// Ludzie (nikt z niczego): OutlawLaw.RaidDraw z okregu wsi zrodla - wies twierdzy rodu Clegane (clan_vlandia_5) najblizszej Riverrun (w ROT Harrenhal), wzor
    /// FreeFolkRaids. Dwie bandy (RaidBands), kazda: rabuj najblizsza wies Dorzecza w stanie Normal (nie cel drugiej bandy, nie spalona przez nas w ostatnich RaidRecentDays
    /// dniach), po spaleniu - nastepna. Partia sceny (bez nowych decyzji - nie ucieka i nie zaczepia po drodze); w marszu do pierwszej wsi ukryta przed AI (ziemie
    /// Clegane - nie bija sie z wlasnymi), potem widoczna (lordowie Dorzecza, Beric i gracz moga ja rozbic). Straty rabunku ksieguje gra i Armoury jak kazdy rabunek
    /// (DistrictRaid: ludzie, spichlerz, towar; zlota nie ma - SmallGold.PayRaidLoot wymaga wodza-bohatera).
    /// KONIEC (dzien 188): kazda banda w domu jak banda najazdu Wolnych Ludzi (FreeFolkRaids.Home: bohaterowie-jency uciekaja, lup do wsi zrodla - zloto do jej kiesy,
    /// towar do magazynu - ludzie do puli okregu zrodla, banda rozwiazana). Klucz wylaczony w trakcie - bandy zostaja zwyklymi bandami (bez rozwiazania).
    /// </summary>
    internal static partial class Prologue
    {
        private const string MScene = "Gora";
        private const string RaiderClanId = "robber_knights", CleganeClanId = "clan_vlandia_5", LannisterClanId = "clan_vlandia_1", RiverlandsId = "riverlands", RiverrunId = "town_V2";
        private const string RaiderName = "The Mountain's Men";
        private const int RaidFrom = 155, RaidBands = 2, RaidMinMen = 20, RaidMaxMen = 50, RaidRecentDays = 20, RaidSpawnLate = 5;

        private sealed class Raider
        {
            public string Id, Src, Region, Target;
            public bool Raiding, Marched;
        }

        // stan (zapis)
        private static readonly List<Raider> _mm = new List<Raider>();
        private static readonly Dictionary<string, int> _mmRecent = new Dictionary<string, int>(StringComparer.Ordinal);   // wies -> dzien prologu spalenia przez nas
        private static readonly List<string> _mmBurnedNames = new List<string>();
        private static int _mmSpawned, _mmMen, _mmLost, _mmBurned, _mmHome, _mmBack;
        private static bool _mmStarted, _mmDone;
        private static string _mmWhy = "", _mmNote = "", _mmKiller = "";
        private static readonly List<string> _mmKillerIds = new List<string>();   // bohaterowie-wodzowie, ktorzy rozbili bande (Beric liczy swoje)

        private static void ResetRaiders()
        {
            _mm.Clear(); _mmRecent.Clear(); _mmBurnedNames.Clear();
            _mmSpawned = _mmMen = _mmLost = _mmBurned = _mmHome = _mmBack = 0; _mmStarted = _mmDone = false; _mmWhy = _mmNote = _mmKiller = ""; _mmKillerIds.Clear();
        }

        private static string PackRaiders()
        {
            var b = _mm.Select(r => r.Id + ":" + r.Src + ":" + r.Region + ":" + (r.Target ?? "-") + ":" + (r.Raiding ? "1" : "0") + (r.Marched ? "1" : "0"));
            var rec = _mmRecent.Select(kv => kv.Key + ":" + kv.Value.ToString(Inv));
            return "wmm=" + string.Join(",", new[] { _mmSpawned, _mmMen, _mmLost, _mmBurned, _mmHome, _mmBack, _mmStarted ? 1 : 0, _mmDone ? 1 : 0 }.Select(x => x.ToString(Inv)))
                   + ";wmb=" + string.Join(".", b.ToArray()) + ";wmr=" + string.Join(".", rec.ToArray()) + ";wmn=" + Clean(string.Join("|", _mmBurnedNames.ToArray()))
                   + ";wmk=" + Clean(_mmKiller) + ";wmw=" + Ids(_mmKillerIds) + ";";
        }

        private static void UnpackRaiders(Dictionary<string, string> map)
        {
            ResetRaiders();
            string v;
            if (map.TryGetValue("wmm", out v))
            {
                var a = v.Split(',');
                _mmSpawned = Int(a, 0, 0); _mmMen = Int(a, 1, 0); _mmLost = Int(a, 2, 0); _mmBurned = Int(a, 3, 0); _mmHome = Int(a, 4, 0); _mmBack = Int(a, 5, 0);
                _mmStarted = Int(a, 6, 0) == 1; _mmDone = Int(a, 7, 0) == 1;
            }
            foreach (var e in IdList(map, "wmb"))
            {
                var a = e.Split(':');
                if (a.Length < 5) continue;
                _mm.Add(new Raider { Id = a[0], Src = a[1], Region = a[2], Target = a[3] == "-" ? null : a[3], Raiding = a[4].Length > 0 && a[4][0] == '1', Marched = a[4].Length > 1 && a[4][1] == '1' });
            }
            foreach (var e in IdList(map, "wmr")) { var a = e.Split(':'); int d; if (a.Length == 2 && int.TryParse(a[1], System.Globalization.NumberStyles.Integer, Inv, out d)) _mmRecent[a[0]] = d; }
            if (map.TryGetValue("wmn", out v)) foreach (var n in Unesc(v).Split('|')) if (n.Length > 0) _mmBurnedNames.Add(n);
            if (map.TryGetValue("wmk", out v)) _mmKiller = Unesc(v);
            _mmKillerIds.AddRange(IdList(map, "wmw"));
        }

        /// <summary>Wies zrodla Ludzi Gory: wies twierdzy rodu Clegane najblizszej Riverrun (w ROT Harrenhal; zapas - Clegane's Keep).</summary>
        private static Settlement RaiderSource()
        {
            var clan = C(CleganeClanId);
            var rr = Settlement.Find(RiverrunId);
            if (clan == null || rr == null) return null;
            Settlement best = null; float bd = float.MaxValue;
            foreach (var t in clan.Fiefs)
            {
                var st = t != null ? t.Settlement : null;
                if (st == null || st.BoundVillages == null) continue;
                foreach (var v in st.BoundVillages)
                {
                    if (v == null || v.Settlement == null) continue;
                    float d = v.Settlement.GetPosition2D.Distance(rr.GetPosition2D);
                    if (d < bd) { bd = d; best = v.Settlement; }
                }
            }
            return best;
        }

        private static Settlement PickRaidTarget(MobileParty mp, Raider self, double day)
        {
            Settlement best = null; float bd = float.MaxValue;
            var taken = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in _mm) if (r != self && r.Target != null) taken.Add(r.Target);
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsVillage || st.Village == null || st.Village.VillageState != Village.VillageStates.Normal) continue;
                if (st.MapFaction == null || st.MapFaction.StringId != RiverlandsId || st.SettlementHitPoints <= 0.001f || st.Party.MapEvent != null) continue;
                if (taken.Contains(st.StringId)) continue;
                int when;
                if (_mmRecent.TryGetValue(st.StringId, out when) && day - when < RaidRecentDays) continue;
                float d = mp.GetPosition2D.Distance(st.GetPosition2D);
                if (d < bd) { bd = d; best = st; }
            }
            return best;
        }

        private static void RaidersStep(double day, bool daily)
        {
            if (_mmDone) return;
            var s = S;
            bool key = s != null && s.PrologueMountainRaiders;
            if (key && daily && !_mmStarted && day >= RaidFrom && day < PrologueDays) RaidersSpawn(day);
            if (_mm.Count == 0)
            {
                if (_mmStarted && day >= PrologueDays) _mmDone = true;
                return;
            }
            var map = BanditsById();
            foreach (var r in _mm.ToList())
            {
                MobileParty mp;
                if (!map.TryGetValue(r.Id, out mp) || mp == null || !mp.IsActive)
                {
                    _mm.Remove(r); _mmLost++;
                    Log.Info(WTag + "ludzie Gory: banda " + r.Id + " ROZBITA albo rozwiazana" + (_mmKiller.Length > 0 ? " (" + _mmKiller + ")" : "") + " (dzien prologu " + D(day) + ").");
                    continue;
                }
                if (!key) continue;   // klucz wylaczony - zwykla banda (WorldStep puszcza ja z rejestru), bez rozwiazania na koniec
                if (day >= PrologueDays)
                {
                    if (mp.MapEvent != null || mp.SiegeEvent != null) { Control(MScene, mp, KGo, mp.CurrentSettlement, null, false); continue; }
                    RaiderHome(r, mp, day);
                    continue;
                }
                try { RaiderTick(r, mp, day); } catch (Exception e) { WStumble("RaiderTick(" + r.Id + ")", e); }
            }
            if (_mm.Count == 0 && day >= PrologueDays) _mmDone = true;
        }

        private static void RaidersSpawn(double day)
        {
            if (_wLegacy && day > RaidFrom + 3) { _mmStarted = true; _mmDone = true; _mmWhy = "zapis sprzed E5"; Log.Info(WTag + "ludzie Gory POMINIECI - zapis sprzed E5 (dzien prologu " + D(day) + ")."); return; }
            var clan = BanditClan(RaiderClanId);
            var src = RaiderSource();
            var region = src != null ? OutlawLaw.RegionFor(src) : null;
            var lann = C(LannisterClanId);
            if (clan == null || src == null || region == null)
            {
                _mmNote = clan == null ? "brak klanu " + RaiderClanId : "brak wsi rodu Clegane (" + CleganeClanId + ")";
                if (day >= RaidFrom + RaidSpawnLate) { _mmStarted = true; _mmDone = true; _mmWhy = _mmNote; Log.Info(WTag + "ludzie Gory POMINIECI - " + _mmNote + "."); }
                return;
            }
            int made = 0;
            for (int i = _mm.Count; i < RaidBands; i++)
            {
                string note;
                var mp = SpawnBand("armoury_prl_mountain", clan, src, region, RaidMinMen + MBRandom.RandomInt(RaidMaxMen - RaidMinMen + 1), RaidMinMen, RaiderName, lann != null ? lann.Banner : null, out note);
                _mmNote = note;
                if (mp == null) { Log.Info(WTag + "ludzie Gory: banda przy " + src.StringId + " NIE powstala - " + note + " (dzien prologu " + D(day) + ")."); break; }
                var r = new Raider { Id = mp.StringId, Src = src.StringId, Region = region.StringId };
                _mm.Add(r); _mmSpawned++; _mmMen += mp.MemberRoster.TotalRegulars; made++;
                var tgt = PickRaidTarget(mp, r, day);
                r.Target = tgt != null ? tgt.StringId : null;
                Log.Info(WTag + "ludzie Gory: nowa banda '" + RaiderName + "' (" + mp.StringId + ", klan " + RaiderClanId + ", sztandar " + (lann != null ? "Lannister" : "klanu") + ") przy "
                         + src.StringId + " (okreg " + region.StringId + ") - " + note + " | pierwszy cel " + (r.Target ?? "brak") + " (dzien prologu " + D(day) + ").");
            }
            if (_mm.Count >= RaidBands || day >= RaidFrom + RaidSpawnLate) _mmStarted = true;
        }

        private static void RaiderTick(Raider r, MobileParty mp, double day)
        {
            var me = mp.MapEvent;
            if (me != null)
            {
                if (me.IsRaid && me.MapEventSettlement != null && me.MapEventSettlement.StringId == r.Target) { r.Raiding = true; r.Marched = true; }
                Control(MScene, mp, KRaid, Settlement.Find(r.Target ?? ""), null, false);
                return;
            }
            var tv = r.Target != null ? Settlement.Find(r.Target) : null;
            if (tv != null && tv.Village != null && tv.Village.VillageState == Village.VillageStates.Looted && r.Raiding)
            {
                _mmBurned++; _mmRecent[tv.StringId] = (int)day;
                _mmBurnedNames.Add(tv.Name.ToString() + " d" + ((int)day).ToString(Inv));
                while (_mmBurnedNames.Count > 8) _mmBurnedNames.RemoveAt(0);
                Log.Info(WTag + "ludzie Gory: SPALONA wies " + tv.Name + " (" + tv.StringId + ", " + (tv.MapFaction != null ? tv.MapFaction.StringId : "-") + ") przez " + r.Id + " ("
                         + mp.MemberRoster.TotalHealthyCount + " zdrowych) | spalonych od poczatku " + _mmBurned + " (dzien prologu " + D(day) + ").");
                tv = null;
            }
            bool bad = tv == null || tv.Village == null || tv.MapFaction == null || tv.MapFaction.StringId != RiverlandsId
                       || (tv.Village.VillageState != Village.VillageStates.Normal && !(tv.Village.VillageState == Village.VillageStates.BeingRaided && r.Raiding));
            if (bad)
            {
                r.Raiding = false;
                var nt = PickRaidTarget(mp, r, day);
                string was = r.Target;
                r.Target = nt != null ? nt.StringId : null;
                if (r.Target != was) Log.Info(WTag + "ludzie Gory: " + r.Id + " - nowy cel " + (r.Target ?? "BRAK (zadnej wsi Dorzecza w stanie Normal)") + " (" + Pos(mp, nt) + ", dzien prologu " + D(day) + ").");
                tv = nt;
            }
            if (tv == null) return;   // bez celu - zwykla banda do czasu, az ktoras wies wroci do stanu Normal (WorldStep puszcza ja z rejestru)
            Control(MScene, mp, KRaid, tv, null, !r.Marched);
        }

        private static void RaiderHome(Raider r, MobileParty mp, double day)
        {
            var src = Settlement.Find(r.Src); var region = Settlement.Find(r.Region);
            _mm.Remove(r);
            if (src == null || src.Village == null || region == null || mp.IsCurrentlyUsedByAQuest)
            {
                Uncontrol(mp);
                Log.Info(WTag + "ludzie Gory: koniec prologu - " + r.Id + " zostaje zwykla banda (" + (mp.IsCurrentlyUsedByAQuest ? "w zadaniu gracza" : "brak wsi zrodla") + ").");
                return;
            }
            Uncontrol(mp);
            long g, it;
            int men = mp.MemberRoster.TotalRegulars;
            int back = FreeFolkRaids.Home(mp, src, region, out g, out it);
            if (back < 0) { Log.Info(WTag + "ludzie Gory: koniec prologu - " + r.Id + " zostaje zwykla banda (prawo wyrzutkow wylaczone)."); return; }
            _mmHome++; _mmBack += back;
            Log.Info(WTag + "ludzie Gory: koniec prologu - " + r.Id + " wraca do domow: " + back + " ludzi do puli okregu " + region.StringId + ", lup " + g + " zl i " + it + " szt. do "
                     + src.StringId + " (dzien prologu " + D(day) + ").");
        }

        /// <summary>CampaignEvents.MobilePartyDestroyed: kto rozbil bande Ludzi Gory (linia; Beric liczy swoje zwyciestwa).</summary>
        internal static void OnWorldPartyDestroyed(MobileParty party, PartyBase destroyer)
        {
            try
            {
                if (!Active || party == null || _mm.Count == 0 || destroyer == null) return;
                if (!_mm.Any(r => r.Id == party.StringId)) return;
                var lead = destroyer.LeaderHero;
                _mmKiller = "rozbili: " + (lead != null ? WhoS(lead) : destroyer.Name.ToString()) + " (" + (destroyer.MapFaction != null ? destroyer.MapFaction.StringId : "-") + ")";
                if (lead != null) _mmKillerIds.Add(lead.StringId);
            }
            catch (Exception e) { WStumble("OnWorldPartyDestroyed", e); }
        }

        private static string RaidersLine()
        {
            var parts = new List<string>();
            if (_mm.Count > 0)
            {
                var map = BanditsById();
                foreach (var r in _mm)
                {
                    MobileParty mp;
                    if (!map.TryGetValue(r.Id, out mp) || !mp.IsActive) continue;
                    parts.Add(r.Id + " (" + mp.MemberRoster.TotalManCount + " ludzi, cel " + (r.Target ?? "-") + (r.Raiding ? " - rabunek" : "") + ", " + Pos(mp, r.Target != null ? Settlement.Find(r.Target) : null) + ")");
                }
            }
            return "ludzie Gory: " + (_mmDone ? "koniec" + (_mmWhy.Length > 0 ? " (" + _mmWhy + ")" : "") : !_mmStarted ? "przed (od d" + RaidFrom + ")" : "w polu " + parts.Count + "/" + RaidBands)
                   + (parts.Count > 0 ? " [" + string.Join("; ", parts.ToArray()) + "]" : "") + ", od poczatku " + _mmSpawned + " band (" + _mmMen + " ludzi), spalone wsie " + _mmBurned
                   + (_mmBurnedNames.Count > 0 ? " (" + string.Join(", ", _mmBurnedNames.ToArray()) + ")" : "") + ", rozbite " + _mmLost + (_mmKiller.Length > 0 ? " (ostatnio " + _mmKiller + ")" : "")
                   + ", wrocily do domow " + _mmHome + " (" + _mmBack + " ludzi)" + (_mmNote.Length > 0 && !_mmStarted ? ", " + _mmNote : "");
        }
    }
}
