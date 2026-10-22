using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m104-e5 pkt 5 - KLANY GORSKIE DOLINY od dnia 118 (klucz PrologueHillClans; projekt pkt 2: "klany gorskie napadaja na Gorskim Trakcie"). W ROT nie ma Burned Men,
    /// Milk Snakes ani Black Ears; klan bandycki "hilltribesmen" (Hill Tribesmen, kultura hilltribesmen - oddzialy z ROT) - bandy tego klanu z nazwami klanow z ksiazki
    /// (Painted Dogs, Burned Men, Milk Snakes, Black Ears; Party.SetCustomName; Stone Crows to w ROT frakcja Shaggi, nie banda - recenzja m104). Wzor spawnu i ksiegi ludzi - FreeFolkRaids (rozpoznanie e5:B): ludzie z OutlawLaw.RaidDraw
    /// (najpierw pula wyrzutkow okregu, potem domy wsi okregu - "bieda i wojna"; nikt z niczego), BanditPartyComponent.CreateLooterParty z domem = wies na Gorskim Trakcie
    /// (wsie Orlego Gniazda i Krwawej Bramy) - AI band gry patroluje wokol niej i napada podroznych (tabory, karawany, slabsze partie), PeopleBook.Give (pochodzenie)
    /// i PeopleLedger.FixCreatedMen (ksiega ludzi). Bez zlota z niczego (kiesa 0), bez zamrazania AI - to zwykle bandy.
    /// LIMIT: najwyzej ClanMax band naraz z tej sceny; w dniu 118 do ClanMax, potem uzupelnienie po jednej co ClanRespawnDays dni, do dnia ClanUntil. Po prologu (188)
    /// bandy zostaja zwyklymi bandami (bez zarzadu - jak kazda banda klanu). Prawo wyrzutkow wylaczone (OutlawLaw) - bez ludzi, bez band (linia).
    /// </summary>
    internal static partial class Prologue
    {
        private const string HillClanId = "hilltribesmen";
        // recenzja m104: bez "Stone Crows" - w ROT to pomniejsza frakcja (spclans.xml stone_crows, dom town_EN2; Shagga - wiersze kanonu p05/p06/m08, scena 16), nie banda
        private static readonly string[] ClanNames = { "Painted Dogs", "Burned Men", "Milk Snakes", "Black Ears" };
        private static readonly string[] ClanHomes = { "village_EN1_2", "castle_village_EN2_1", "castle_village_EN2_2", "village_EN1_1", "village_EN1_3" };   // Stoneyhead, Durlston, Shady Glen, Hollow's Creek, Monton
        private const int ClanFrom = 118, ClanUntil = 186, ClanMax = 4, ClanRespawnDays = 3, ClanMinMen = 12, ClanMaxMen = 32;

        // stan (zapis): bandy sceny w polu (StringId), od poczatku: band / ludzi / zniszczonych, ostatnie narodziny (dzien prologu), koniec sceny
        private static readonly List<string> _kgIds = new List<string>();
        private static int _kgSpawned, _kgMen, _kgLost, _kgLast = -100, _kgName;
        private static bool _kgDone;
        private static string _kgWhy = "", _kgNote = "";

        private static void ResetClans()
        {
            _kgIds.Clear(); _kgSpawned = _kgMen = _kgLost = _kgName = 0; _kgLast = -100; _kgDone = false; _kgWhy = _kgNote = "";
        }

        private static string PackClans()
        {
            return "wkg=" + _kgSpawned.ToString(Inv) + "," + _kgMen.ToString(Inv) + "," + _kgLost.ToString(Inv) + "," + _kgLast.ToString(Inv) + "," + _kgName.ToString(Inv) + ","
                   + (_kgDone ? "1" : "0") + ";wki=" + Ids(_kgIds) + ";";
        }

        private static void UnpackClans(Dictionary<string, string> map)
        {
            ResetClans();
            string v;
            if (map.TryGetValue("wkg", out v))
            {
                var a = v.Split(',');
                _kgSpawned = Int(a, 0, 0); _kgMen = Int(a, 1, 0); _kgLost = Int(a, 2, 0); _kgLast = Int(a, 3, -100); _kgName = Int(a, 4, 0); _kgDone = Int(a, 5, 0) == 1;
            }
            _kgIds.AddRange(IdList(map, "wki"));
        }

        // m105-kronika B2 (plan P6): pomocniki band przeniesione 1:1 do StoryActions (wspolne z akcja band: Kroniki) - tu cienkie opakowania, logika bez zmian
        private static Clan BanditClan(string id) { return StoryActions.BanditClan(id); }

        private static Dictionary<string, MobileParty> BanditsById() { return StoryActions.BanditsById(); }

        /// <summary>Banda sceny z ludzi okregu (StoryActions.SpawnBand - wzor FreeFolkRaids.Daily); potkniecia w liczniku scen prologu (WStumble).
        /// null - nie powstala (note - powod; ludzie wrocili do puli).</summary>
        private static MobileParty SpawnBand(string idBase, Clan clan, Settlement home, Settlement region, int want, int min, string name, Banner banner, out string note)
        {
            return StoryActions.SpawnBand(idBase, clan, home, region, want, min, name, banner, out note, WStumble);
        }

        private static void ClansStep(double day, bool daily)
        {
            if (!daily || _kgDone) return;
            var s = S;
            if (day >= PrologueDays)
            {
                if (_kgIds.Count > 0 || _kgSpawned > 0)
                    Log.Info(WTag + "klany gorskie: koniec prologu - " + _kgIds.Count + " band zostaje zwyklymi bandami (od poczatku " + _kgSpawned + " band, " + _kgMen + " ludzi, zniszczone " + _kgLost + ").");
                _kgDone = true; _kgIds.Clear();
                return;
            }
            // bandy sceny w polu (zniszczone - z listy)
            var map = _kgIds.Count > 0 ? BanditsById() : null;
            foreach (var id in _kgIds.ToList())
            {
                MobileParty mp;
                if (map == null || !map.TryGetValue(id, out mp) || mp == null || !mp.IsActive) { _kgIds.Remove(id); _kgLost++; }
            }
            if (s == null || !s.PrologueHillClans || day < ClanFrom || day >= ClanUntil) return;
            if (_wLegacy && _kgSpawned == 0 && day > ClanFrom + 3) { _kgDone = true; _kgWhy = "zapis sprzed E5"; Log.Info(WTag + "klany gorskie POMINIETE - zapis sprzed E5 (dzien prologu " + D(day) + ")."); return; }
            int want = _kgIds.Count >= ClanMax ? 0 : (_kgSpawned == 0 ? ClanMax - _kgIds.Count : ((int)day - _kgLast >= ClanRespawnDays ? 1 : 0));
            if (want <= 0) return;
            var clan = BanditClan(HillClanId);
            if (clan == null) { _kgNote = "brak klanu " + HillClanId; return; }
            for (int i = 0; i < want; i++)
            {
                var home = PickClanHome();
                var region = home != null ? OutlawLaw.RegionFor(home) : null;
                if (home == null || region == null) { _kgNote = "brak wsi na Gorskim Trakcie"; break; }
                string name = ClanNames[_kgName % ClanNames.Length], note;
                var mp = SpawnBand("armoury_prl_clan", clan, home, region, ClanMinMen + MBRandom.RandomInt(ClanMaxMen - ClanMinMen + 1), ClanMinMen, name, null, out note);
                _kgNote = note;
                if (mp == null) { Log.Info(WTag + "klany gorskie: banda '" + name + "' przy " + home.StringId + " NIE powstala - " + note + " (dzien prologu " + D(day) + ")."); break; }
                _kgIds.Add(mp.StringId); _kgSpawned++; _kgMen += mp.MemberRoster.TotalRegulars; _kgLast = (int)day; _kgName++;
                Log.Info(WTag + "klany gorskie: nowa banda '" + name + "' (" + mp.StringId + ", klan " + HillClanId + ") przy " + home.StringId + " (okreg " + region.StringId + ") - " + note
                         + " | w polu " + _kgIds.Count + "/" + ClanMax + " (dzien prologu " + D(day) + ").");
            }
        }

        /// <summary>Wies na Gorskim Trakcie z najmniejsza liczba band sceny wokol (dom bandy - AI band patroluje wokol niej).</summary>
        private static Settlement PickClanHome()
        {
            var map = BanditsById();
            Settlement best = null; int bestN = int.MaxValue;
            foreach (var id in ClanHomes)
            {
                var st = Settlement.Find(id);
                if (st == null || !st.IsVillage) continue;
                int n = 0;
                foreach (var bid in _kgIds) { MobileParty mp; if (map.TryGetValue(bid, out mp) && mp.HomeSettlement == st) n++; }
                if (n < bestN) { best = st; bestN = n; }
            }
            return best;
        }

        private static string ClansLine()
        {
            int men = 0, alive = 0;
            if (_kgIds.Count > 0)
            {
                var map = BanditsById();
                foreach (var id in _kgIds) { MobileParty mp; if (map.TryGetValue(id, out mp) && mp.IsActive) { alive++; men += mp.MemberRoster.TotalManCount; } }
            }
            return "klany gorskie: " + (_kgDone ? "koniec" + (_kgWhy.Length > 0 ? " (" + _kgWhy + ")" : "") : "w polu " + alive + "/" + ClanMax + " (ludzi " + men + ")")
                   + ", od poczatku " + _kgSpawned + " band (" + _kgMen + " ludzi), zniszczone " + _kgLost + (_kgLast >= 0 ? ", ostatnia d" + _kgLast : "")
                   + (_kgNote.Length > 0 ? ", " + _kgNote : "");
        }
    }
}
