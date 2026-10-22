using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
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
    /// m105-kronika K0 (krok B2, plan P6) - WSPOLNE POMOCNIKI SCEN FABULARNYCH. Przeniesione 1:1 (bez zmiany logiki) z prywatnych metod m104-e5:
    ///  - Prologue.World.Clans: BanditClan, BanditsById, SpawnBand (banda z puli wyrzutkow okregu: OutlawLaw.RaidDraw -> CreateLooterParty -> roster -> kiesa 0 ->
    ///    pochodzenie -> ksiega ludzi; nikt z niczego);
    ///  - Prologue.World.Tourney: GtReady (GrandTourney.CanonApi odbiciem - Armoury nie zna DLL GrandTourney, bez nowej zaleznosci).
    /// Prologue wola je przez swoje stare nazwy (cienkie opakowania z wlasnym licznikiem potkniec), Kronika (Chronicle.Bands, akcja tourney) - wprost.
    /// Potkniecia zglasza wolajacy (parametr stumble) - jak dotad kazdy w swoim liczniku.
    /// Nowe w B2: wspolny sufit band fabularnych na region (StoryBandsIn, RegionCap) - bandy scen (najazdy Wolnych Ludzi armoury_ffraid, Ludzie Gory
    /// armoury_prl_mountain, klany gorskie armoury_prl_clan) i Kroniki (armoury_kr_) w okregu OutlawLaw (warownia + jej wsie); okreg bandy = okreg osady domowej
    /// (BanditPartyComponent.RelatedSettlement / HomeSettlement), bez osady - okreg najblizszej warowni. Sufit sprawdza tylko Kronika przed nowa banda (inne sceny
    /// maja wlasne sufity i nie wolaja tej metody - ich logika bez zmian, plan P6).
    /// </summary>
    internal static class StoryActions
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Wspolny sufit band fabularnych w jednym okregu (plan P6) - Kronika nie doklada bandy ponad niego.</summary>
        internal const int RegionCap = 4;

        /// <summary>Przedrostki StringId band scen fabularnych (MobileParty.CreateParty dopisuje numer).</summary>
        internal static readonly string[] StoryPrefixes = { "armoury_ffraid", "armoury_prl_mountain", "armoury_prl_clan", "armoury_kr_" };

        // ---------------------------------------------------------------- bandy (m104-e5 Prologue.World.Clans - przeniesione 1:1)

        internal static Clan BanditClan(string id)
        {
            try { foreach (var c in Clan.BanditFactions) if (c != null && c.StringId == id) return c; } catch { }
            return null;
        }

        internal static Dictionary<string, MobileParty> BanditsById()
        {
            var map = new Dictionary<string, MobileParty>(StringComparer.Ordinal);
            foreach (var mp in MobileParty.AllBanditParties) if (mp != null && mp.StringId != null) map[mp.StringId] = mp;
            return map;
        }

        /// <summary>Banda sceny z ludzi okregu (wzor FreeFolkRaids.Daily: RaidDraw -> CreateLooterParty -> roster -> kiesa 0 -> pochodzenie -> ksiega ludzi).
        /// null - nie powstala (note - powod; ludzie wrocili do puli).</summary>
        internal static MobileParty SpawnBand(string idBase, Clan clan, Settlement home, Settlement region, int want, int min, string name, Banner banner, out string note,
                                              Action<string, Exception> stumble)
        {
            note = "";
            if (!OutlawLaw.On) { note = "prawo wyrzutkow wylaczone (OutlawLawEnabled) - bez puli ludzi"; return null; }
            var org = PeopleBook.OriginLive ? new PeopleBook.Shares() : null;
            TroopRoster roster; float fp, fh;
            int n = OutlawLaw.RaidDraw(region, want, min, clan, org, out roster, out fp, out fh);
            if (n <= 0) { note = "za malo ludzi w okregu " + region.StringId + " (z domow zabrano " + fh.ToString("0.0", Inv) + " - zostaja w puli)"; return null; }
            MobileParty mp = null;
            try
            {
                mp = BanditPartyComponent.CreateLooterParty(idBase, clan, home, false, null, home.GatePosition);
                mp.MemberRoster.Add(roster);
            }
            catch (Exception e) { stumble("SpawnBand(" + idBase + ")", e); }
            if (mp == null || mp.MemberRoster.TotalRegulars == 0)
            {
                OutlawLaw.RaidUndraw(region, roster);
                if (mp != null) { try { DestroyPartyAction.Apply(null, mp); } catch (Exception e) { stumble("SpawnBand(pusta)", e); } }
                note = "banda nie powstala (wyjatek gry) - ludzie wrocili do puli";
                return null;
            }
            int got = mp.MemberRoster.TotalRegulars;
            try
            {
                mp.ActualClan = clan;
                mp.Aggressiveness = 1f - 0.1f * MBRandom.RandomFloat;
                mp.InitializePartyTrade(0);                                  // bez zlota z niczego
                mp.Party.SetCustomName(new TextObject("{=!}" + name));
                if (banner != null) mp.Party.SetCustomBanner(banner);
                mp.Party.SetVisualAsDirty();
            }
            catch (Exception e) { stumble("SpawnBand(ustawienia)", e); }
            if (org != null && org.Total > 0) { try { PeopleBook.Give(mp.MemberRoster, org, PeopleBook.FPoolBand); } catch (Exception e) { stumble("SpawnBand(pochodzenie)", e); } }
            try { PeopleLedger.FixCreatedMen(mp); } catch { }
            note = got + " ludzi (z puli " + fp.ToString("0.0", Inv) + ", zabrani z domow " + Math.Max(0.0, got - fp).ToString("0.0", Inv) + ")";
            return mp;
        }

        // ---------------------------------------------------------------- wspolny sufit band fabularnych na region (B2, plan P6)

        /// <summary>Banda sceny fabularnej (przedrostek StringId z StoryPrefixes).</summary>
        internal static bool IsStoryBand(MobileParty mp)
        {
            string id = mp != null ? mp.StringId : null;
            if (id == null) return false;
            foreach (var p in StoryPrefixes) if (id.StartsWith(p, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>Okreg bandy: okreg OutlawLaw jej osady domowej, bez osady - okreg najblizszej warowni. null - brak.</summary>
        internal static Settlement RegionOfBand(MobileParty mp)
        {
            if (mp == null) return null;
            var home = mp.HomeSettlement;
            if (home != null) return OutlawLaw.RegionFor(home);
            return OutlawLaw.RegionAt(mp.GetPosition2D);
        }

        /// <summary>Bandy scen fabularnych (wszystkie przedrostki) w okregu region - jeden przebieg po bandach mapy, wolane tylko przy narodzinach bandy Kroniki.</summary>
        internal static int StoryBandsIn(Settlement region)
        {
            if (region == null) return 0;
            int n = 0;
            foreach (var mp in MobileParty.AllBanditParties)
            {
                if (mp == null || !mp.IsActive || !IsStoryBand(mp)) continue;
                if (RegionOfBand(mp) == region) n++;
            }
            return n;
        }

        // ---------------------------------------------------------------- GrandTourney (m104-e5 Prologue.World.Tourney - przeniesione 1:1)

        private static bool _gtTried;
        private static MethodInfo _gtProclaim, _gtSetHeroes;

        /// <summary>GrandTourney.CanonApi.ProclaimRoyal(string townId, string[] canonHeroIds) - po GtReady; null bez GrandTourney.</summary>
        internal static MethodInfo GtProclaim { get { return _gtProclaim; } }
        /// <summary>GrandTourney.CanonApi.SetHeroes(string townId, string[] canonHeroIds) - po GtReady; null bez GrandTourney.</summary>
        internal static MethodInfo GtSetHeroes { get { return _gtSetHeroes; } }

        internal static bool GtReady(Action<string, Exception> stumble)
        {
            if (!_gtTried)
            {
                _gtTried = true;
                try
                {
                    var t = AccessTools.TypeByName("GrandTourney.CanonApi");
                    if (t != null)
                    {
                        _gtProclaim = t.GetMethod("ProclaimRoyal", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(string[]) }, null);
                        _gtSetHeroes = t.GetMethod("SetHeroes", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(string[]) }, null);
                    }
                }
                catch (Exception e) { stumble("GtReady", e); }
            }
            return _gtProclaim != null && _gtSetHeroes != null;
        }
    }
}
