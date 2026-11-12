using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 476-a (Jeff 13.11: "Mines rob od razu i tylko tam gdzie jest zloze, jesli nie ma budynku mines to nie ma wydobycia bo nie ma mines, i jak powstanie mines to
    /// mozna w mine inwestowac im wiekszy poziom mines tym wiecej osob moze pracowac w kopalni i wiecej mozna inwestowac w ten region"; "Castle mines ... gornicy i zasoby
    /// jakie ma dana okolica jesli okolica nie ma zasobow to nie mozna budowac"; "warhorse studs rob od razu") - ZAKLADY PRZY ZASOBACH OKOLICY.
    /// MAPA ZASOBOW: okolica twierdzy = jej wsie (Town.Villages): towary z VillageType (PrimaryProduction + Productions). Zloze = mineral z tabeli gornikow
    /// (Buildings338Ports.GornikKg: zelazo, srebro, sol, glina, wapien, marmur, ruda zlota); pastwisko koni = wies z koniem (kategoria Horse/WarHorse) albo
    /// pastwiska BK okolicy &gt;= 20000 (limit stadniny BK = pastwiska / 10000).
    /// ZASADA (jeden mechanizm "zaklad"): (1) bez budynku nie ma produkcji zakladu (kopalnia twierdzy: BK RunMines w miescie, nasz tick w zamku - tylko przy poziomie
    /// &gt; 0, jak dotad); (2) budynek tylko przy zasobie okolicy - AI nie wybiera go bez zasobu (postfiks GetNextBuilding 473-b6, ta sama droga), a gdy mimo to stoi
    /// (gracz, zapis sprzed 476) - nie produkuje: miasto - prefiks na BK RunMines pomija miasto bez zloza; zamek - urobek tylko mineralow zloza okolicy;
    /// (3) poziom = miejsca pracy i pulap nakladu: kopalnia - sztuki na dobe = poziom (BK; gornicy z wolnych rak osady, placa z ceny sprzedazy - jak dotad), stadnina -
    /// szansa przychowku 2% x poziom na dobe (dotad 2% stale - poziom podnosil tylko limit) i limit pastwiska / 10000 + 2 x poziom; kon z konia (bez konia z niczego).
    /// Gracz: w swoich lennach widzi te budowle jak w BK (lista BK nie jest ruszana - wczytanie zerowaloby poziomy, 473-b6), ale bez zasobu nie daja produkcji.
    /// Klucz Buildings338Works (TAK); NIE - co do bitu jak przed (AI, kopalnie i stadniny bez zmian). Linia startu: mapa zasobow; linia doby: "Budynki (338) zaklady".
    /// </summary>
    internal static class Buildings338Works
    {
        internal const string TownMinesId = "building_town_mines", StudsId = Buildings338Studs.StudsId;
        internal static bool On { get { var s = Settings.Current; return s != null && s.Buildings338 && s.Buildings338Works; } }

        private static readonly HashSet<string> MineralIds = new HashSet<string>(StringComparer.Ordinal) { "iron", "silver", "salt", "clay", "limestone", "marble", "gold_ore" };
        private sealed class Res { public int Day; public readonly HashSet<string> Items = new HashSet<string>(StringComparer.Ordinal); public bool Horses; }
        private static readonly Dictionary<Town, Res> _res = new Dictionary<Town, Res>();
        private static bool _minesHooked;
        private static int _aiBlocked, _townMinesSkipped, _castleOreFiltered, _stumbles;

        internal static void SessionStart()
        {
            _res.Clear(); _aiBlocked = _townMinesSkipped = _castleOreFiltered = _stumbles = 0;
            if (!_minesHooked)
            {
                var bkb = AccessTools.TypeByName("BannerKings.Behaviours.BKBuildingsBehavior");
                var m = bkb != null ? AccessTools.DeclaredMethod(bkb, "RunMines", new[] { typeof(Town) }) : null;
                if (m != null) { Buildings338.H.Patch(m, prefix: new HarmonyMethod(typeof(Buildings338Works), nameof(RunMinesPrefix)) { priority = Priority.First }); _minesHooked = true; }
            }
            Buildings338.Hook("zaklady (476): BK RunMines miast " + (_minesHooked ? "pod zlozem okolicy" : "BRAK"));
            if (!On || Campaign.Current == null) return;
            try
            {
                int towns = 0, withOre = 0, castles = 0, castlesOre = 0, horses = 0;
                var byMin = new SortedDictionary<string, int>(StringComparer.Ordinal);
                foreach (var t in Town.AllTowns) { towns++; var r = Get(t); if (Deposit(r)) { withOre++; Count(byMin, r); } }
                foreach (var t in Town.AllCastles) { castles++; var r = Get(t); if (Deposit(r)) { castlesOre++; Count(byMin, r); } if (HasPasture(t)) horses++; }
                var sb = new StringBuilder(400);
                foreach (var kv in byMin) sb.Append(sb.Length > 0 ? ", " : "").Append(kv.Key).Append(' ').Append(kv.Value);
                Log.Info("Budynki (338) zaklady (476): start sesji | klucz TAK | mapa zasobow okolic: miasta ze zlozem " + withOre + " z " + towns + ", zamki ze zlozem " + castlesOre + " z " + castles
                         + " (twierdz wg mineralu: " + (sb.Length > 0 ? sb.ToString() : "-") + "), zamki z pastwiskiem koni " + horses + " | kopalnia i stadnina tylko przy zasobie okolicy");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        private static void Count(SortedDictionary<string, int> d, Res r)
        {
            foreach (var id in r.Items) if (MineralIds.Contains(id)) { int n; d.TryGetValue(id, out n); d[id] = n + 1; }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3) { try { Log.Error("Buildings338Works." + where, e); } catch { } }
        }

        /// <summary>Zasoby okolicy twierdzy (wsie: PrimaryProduction + Productions), pamiec na dobe.</summary>
        private static Res Get(Town t)
        {
            int day = (int)CampaignTime.Now.ToDays;
            Res r;
            if (_res.TryGetValue(t, out r) && r.Day == day) return r;
            if (r == null) { r = new Res(); _res[t] = r; } else { r.Items.Clear(); r.Horses = false; }
            r.Day = day;
            try
            {
                if (t != null && t.Villages != null)
                    foreach (var v in t.Villages)
                    {
                        var vt = v != null ? v.VillageType : null;
                        if (vt == null) continue;
                        Add(r, vt.PrimaryProduction);
                        if (vt.Productions != null) foreach (var p in vt.Productions) Add(r, p.Item1);
                    }
            }
            catch (Exception e) { Stumble("Get", e); }
            return r;
        }

        private static void Add(Res r, ItemObject it)
        {
            if (it == null || it.StringId == null) return;
            r.Items.Add(it.StringId);
            if (it.ItemCategory == DefaultItemCategories.Horse || it.ItemCategory == DefaultItemCategories.WarHorse) r.Horses = true;
        }

        private static bool Deposit(Res r) { foreach (var id in r.Items) if (MineralIds.Contains(id)) return true; return false; }

        internal static bool HasDeposit(Town t) { return t != null && Deposit(Get(t)); }

        /// <summary>Mineral jest w zlozu okolicy twierdzy.</summary>
        internal static bool InDeposit(Town t, ItemObject it) { return t != null && it != null && it.StringId != null && Get(t).Items.Contains(it.StringId); }

        internal static bool HasPasture(Town t)
        {
            if (t == null) return false;
            if (Get(t).Horses) return true;
            return Buildings338.Pasture(Buildings338.PopData(t.Settlement)) >= 20000f;
        }

        /// <summary>AI (osada nie gracza) nie wybiera zakladu bez zasobu okolicy (postfiks GetNextBuilding 473-b6 i kolejka).</summary>
        internal static bool AiBlocks(Town t, BuildingType bt)
        {
            if (!On || t == null || bt == null || t.Settlement == null || t.Settlement.OwnerClan == Clan.PlayerClan) return false;
            try
            {
                var id = bt.StringId;
                bool b = false;
                if (id == TownMinesId || id == Buildings338Ports.CastleMinesId) b = !HasDeposit(t);
                else if (id == StudsId) b = !HasPasture(t);
                if (b) _aiBlocked++;
                return b;
            }
            catch (Exception e) { Stumble("AiBlocks", e); return false; }
        }

        /// <summary>BK RunMines (tick miast): miasto bez zloza okolicy nie kopie (zamki - wlasny tick 338, filtr w Buildings338Ports.MineCastle).</summary>
        public static bool RunMinesPrefix(Town town)
        {
            try
            {
                if (!On || town == null || town.IsCastle) return true;
                if (Buildings338.Level(town, TownMinesId) <= 0 && Buildings338.Level(town, Buildings338Ports.CastleMinesId) <= 0) return true;
                if (HasDeposit(town)) return true;
                _townMinesSkipped++;
                return false;
            }
            catch (Exception e) { Stumble("RunMinesPrefix", e); return true; }
        }

        internal static void NoteCastleOreFiltered() { _castleOreFiltered++; }

        /// <summary>Stadnina: szansa przychowku na dobe (476: 2% x poziom; NIE - 2% jak dotad).</summary>
        internal static float StudsChance(int lvl) { return On ? 0.02f * Math.Max(1, lvl) : 0.02f; }

        /// <summary>Stadnina bez pastwiska okolicy nie hoduje (476).</summary>
        internal static bool StudsNoPasture(Town t) { return On && !HasPasture(t); }

        internal static void Daily(int day)
        {
            if (!On) return;
            Log.Info("Budynki (338) zaklady (476): dzien " + day + " | AI pominelo zaklad bez zasobu " + _aiBlocked + ", miasta z kopalnia bez zloza (bez wydobycia) " + _townMinesSkipped
                     + ", losowan rudy zamkow spoza zloza okolicy (pominiete) " + _castleOreFiltered + " | bledy " + _stumbles);
            _aiBlocked = _townMinesSkipped = _castleOreFiltered = 0;
        }
    }
}
