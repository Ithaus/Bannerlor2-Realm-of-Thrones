using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 326a - FLOTY WEDLUG KROLESTW (Jeff 05.11: "jak to mozliwe, ze Robb zebral flote i poplynal na wyspy i zdobyl port ... skad polnoc ma flote?").
    /// PRZYCZYNA (dekompilacja NavalDLC 1.4.8): ShipTradeCampaignBehavior.DailyTickClan dla KAZDEGO rodu AI: ConsiderPurchasingShip z szansa 0.5 na dobe kupuje statek
    /// w porcie swojego krolestwa (bez patrzenia na liczbe idealna - jedyna bramka to cena < 20% kiesy rodu), a liczba idealna
    /// NavalDLCClanShipOwnershipModel.GetIdealShipNumberForClan = limit partii rodu x 3 - bez kultury i krainy. Sprzedaz tylko powyzej idealu, szansa 0.1, i tylko
    /// statek, bez ktorego sklad druzyny ma WYZSZA ocene (TryGetShipToSell) - w praktyce nadmiar zostaje. W zapisie Jeffa (dzien 109348) 286 partii ma statki,
    /// rody Polnocy tez; IslandRoads (IslandRoadsFix) puszcza partie ze statkami droga "lad i morze" - tak lordowie Polnocy doplyneli na Zelazne Wyspy.
    /// ZMIANA (liczby wedlug ksiazek - decyzja Claude, Jeff nie chce pytan o parametry; klucz NavalRealms):
    ///  (1) postfiks GetIdealShipNumberForClan: ideal = podloga(ideal gry x mnoznik krolestwa rodu) - Zelazne Wyspy x2 (Zelazna Flota), Dragonstone x1.5 (Velaryon,
    ///      Celtigar), Braavos / Volantis / Wyspy Letnie x1.5, Lys / Tyrosh x1.25, korona (Krolewska Przystan, Lannisport), Reach (Redwyne), Myr, Pentos, Lorath, Qarth,
    ///      Ibben, Yi Ti, Aegon (Zlota Kompania przyplynela statkami) x1, Daenerys x0.75, Stormlands i Vale (Gulltown) x0.6, Dorne x0.5, Skagos x0.4, Dorzecze i Nocna
    ///      Straz (Eastwatch) x0.2, Norvos / Qohor / Sarnor (srodladowe) x0.1, Polnoc, Wolni Ludzie i Dothrakowie (khuzait) 0. Polnoc: wyjatek rod Manderly
    ///      (White Harbor - clan_battania_4 albo siedziba town_B3) x0.5. Rod bez krolestwa (najemnicy, mniejsze frakcje) - bez zmian.
    ///  (2) prefiks ConsiderPurchasingShip: rod ze statkami >= idealu (liczba jak u gry - statki partii wojennych rodu) nie kupuje (gra kupowala bez patrzenia na ideal).
    ///  (3) nadmiar (raz na dobe, najwyzej 1 statek na rod; krolestwo bez floty - mnoznik 0 - do 3 i zlozenie w porcie juz po LayUpDaysNoFleet dobach): partia rodu na ladzie (nie na morzu, nie w przejsciu lad-morze, nie na tratwie, nie w bitwie ani
    ///      oblezeniu, z wodzem), z ktorej jest droga LADOWA do siedziby rodu (bez tego - na wyspie - statki zostaja, inaczej utknelaby), sprzedaje najtanszy statek
    ///      portowi (miasto ze stocznia, nieoblezone, nie w wojnie z rodem, droga ladowa; najpierw porty wlasnego krolestwa, potem najblizsze) droga gry
    ///      ChangeShipOwnerAction.ApplyByTrade - zaplata i bramki 181 (port placi z nadwyzki kasy, port pelny / bez nadwyzki nie kupuje; zadnego zlota z niczego przy
    ///      wlaczonym Ship Trade Through Port). Najwyzej 3 porty na probe. Gdy zaden port nie kupi przez LayUpDays dob z rzedu, a nadmiar jest skutkiem naszego
    ///      mnoznika (ideal 326a < ideal gry) - statek zostaje zlozony w najblizszym porcie bez zaplaty (ApplyByTransferring - jak rozdzial statkow gry; zloto sie
    ///      nie pojawia, rod traci wartosc statku; stocznia NavalDLC skasuje nadmiar ponad swoja liczbe idealna - jak dzis).
    /// Linia dobowa "Floty krolestw (326a)". Wylaczony klucz - gra jak dotad, linia tylko mierzy. Kazdy wyjatek per obiekt liczony (Stumble), nic nie gasnie.
    /// PO RECENZJI (05.11):
    ///  - "wyspa" liczona wzgledem ladu stalego krolestwa, nie tylko siedziby: partia jest odcieta (statki zostaja), gdy nie ma drogi ladowej do siedziby rodu
    ///    ALBO do siedziby rodu panujacego krolestwa, ALBO siedziba rodu to miasto bez innego miasta w zasiegu ladu (IslandRoads - Mormont Keep na Wyspie Niedzwiedziej);
    ///  - zakup: rod z odcieta partia bez statkow kupuje mimo idealu (gra daje statek partii z najslabszym skladem - tej bez statkow); lord przewieziony armia
    ///    Manderly'ego na wyspe i tam zostawiony moze wrocic;
    ///  - partia nie sprzedaje statku w armii, przy wyplywaniu z portu (StartTransitionNextFrameToExitFromPort), w drodze do portu (IsTargetingPort) ani gdy jej trasa idzie
    ///    morzem (DesiredAiNavigationType Naval / All);
    ///  - mnoznik > 0 daje co najmniej 1 statek (Nocna Straz, rody Dorzecza - podloga 3 x 0.2 = 0);
    ///  - Ship Trade Through Port (181) wylaczony: bez sprzedazy (ApplyByTrade placilby z niczego) - nadmiar z naszego mnoznika od razu zlozony w porcie bez zaplaty;
    ///  - doby bez kupca w zapisie (arm_k326a) - wczytanie gry nie zeruje licznika.
    /// </summary>
    internal static class NavalRealms
    {
        private const string Tag = "Floty krolestw (326a): ";
        internal const int LayUpDays = 10;
        internal const int LayUpDaysNoFleet = 3;   // krolestwo bez floty (mnoznik 0): po 3 dobach bez kupca statek zlozony w porcie
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.NavalRealms; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        private static readonly Dictionary<string, float> Factor = new Dictionary<string, float>(StringComparer.Ordinal)
        {
            { "sturgia", 2.0f },      // Zelazne Wyspy - Zelazna Flota
            { "dragonstone", 1.5f },  // Stannis: Velaryon, Celtigar, Bar Emmon
            { "bravos", 1.5f }, { "volantis", 1.5f }, { "summer", 1.5f },
            { "lys", 1.25f }, { "tyrosh", 1.25f },
            { "vlandia", 1.0f },      // korona: flota krolewska i Lannisport
            { "empire_w", 1.0f },     // Reach: Redwyne z Arbor
            { "myr", 1.0f }, { "pentos", 1.0f }, { "nord", 1.0f }, { "qarth", 1.0f }, { "ibb", 1.0f }, { "yiti", 1.0f },
            { "valyrian", 1.0f },     // Aegon - Zlota Kompania
            { "empire_s", 0.75f },    // Daenerys
            { "stormlands", 0.6f }, { "empire", 0.6f },   // Stormlands, Vale (Gulltown)
            { "aserai", 0.5f },       // Dorne
            { "skagosi", 0.4f },
            { "riverlands", 0.2f }, { "nightswatch", 0.2f },   // Dorzecze - rzeki; Straz - Eastwatch
            { "norvos", 0.1f }, { "qohor", 0.1f }, { "sarnor", 0.1f },
            { "battania", 0f },       // Polnoc - bez floty (wyjatek Manderly)
            { "freefolk", 0f }, { "khuzait", 0f },
        };
        private const string ManderlyId = "clan_battania_4", WhiteHarborId = "town_B3";
        private const float ManderlyFactor = 0.5f;

        // ---- latki
        private static bool _wIdeal, _wBuy;
        private static readonly List<string> _miss = new List<string>();
        private static bool _startLogged;

        // ---- doba
        private static int _blocked, _sold, _laid, _rejected, _cut, _noPort, _busy, _stumbles;
        private static long _soldGold;
        private static readonly List<string> _ex = new List<string>();
        private static readonly Dictionary<string, int> _fail = new Dictionary<string, int>(StringComparer.Ordinal);   // rod -> doby z rzedu bez kupca (zapis arm_k326a)
        private static int _strandBuy;
        private static readonly HashSet<string> _errSites = new HashSet<string>(StringComparer.Ordinal);

        internal static void Reset()
        {
            _blocked = _sold = _laid = _rejected = _cut = _noPort = _busy = _stumbles = _strandBuy = 0; _soldGold = 0; _ex.Clear(); _fail.Clear(); _startLogged = false;
        }

        internal static string Export()
        {
            var sb = new StringBuilder(64);
            sb.Append("nr1|");
            bool first = true;
            foreach (var kv in _fail) { if (!first) sb.Append(';'); first = false; sb.Append(kv.Key).Append('=').Append(kv.Value.ToString(Inv)); }
            return sb.ToString();
        }

        internal static void Import(string v)
        {
            _fail.Clear();
            if (string.IsNullOrEmpty(v) || !v.StartsWith("nr1|", StringComparison.Ordinal)) return;
            try
            {
                foreach (var e in v.Substring(4).Split(';'))
                {
                    int i = e.IndexOf('='); int x;
                    if (i > 0 && int.TryParse(e.Substring(i + 1), NumberStyles.Integer, Inv, out x)) _fail[e.Substring(0, i)] = x;
                }
            }
            catch (Exception e) { _fail.Clear(); Stumble("Import", e); }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errSites.Add(where)) Log.Error("NavalRealms." + where, e); } catch { }
        }

        private static string Nm(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "-"; } catch { return "?"; } }

        /// <summary>Mnoznik idealu rodu; -1 = bez zmian (rod bez krolestwa albo krolestwo spoza tabeli).</summary>
        internal static float FactorOf(Clan c)
        {
            if (c == null) return -1f;
            var k = c.Kingdom;
            if (k == null) return -1f;
            float f;
            if (!Factor.TryGetValue(k.StringId, out f)) return -1f;
            if (k.StringId == "battania")
            {
                bool manderly = c.StringId == ManderlyId;
                try { if (!manderly && c.HomeSettlement != null && c.HomeSettlement.StringId == WhiteHarborId) manderly = true; } catch { }
                if (manderly) f = ManderlyFactor;
            }
            return f;
        }

        internal static int IdealFor(Clan c, int gameIdeal)
        {
            float f = FactorOf(c);
            if (f < 0f) return gameIdeal;
            int v = Math.Max(0, (int)Math.Floor(gameIdeal * f + 1e-4f));
            if (f > 0f && gameIdeal > 0 && v < 1) v = 1;   // po recenzji: mnoznik > 0 to co najmniej jeden statek
            return v;
        }

        private static int GameIdeal(Clan c)
        {
            try { return Campaign.Current.Models.ClanTierModel.GetPartyLimitForTier(c, c.Tier) * 3; }   // ten sam wzor co NavalDLCClanShipOwnershipModel
            catch { return 0; }
        }

        internal static int ShipsOf(Clan c)
        {
            int n = 0;
            var w = c.WarPartyComponents;
            if (w == null) return 0;
            for (int i = 0; i < w.Count; i++) { var mp = w[i] != null ? w[i].MobileParty : null; if (mp != null && mp.Ships != null) n += mp.Ships.Count; }
            return n;
        }

        // ============================================================ latki
        internal static void ApplyAll(Harmony h)
        {
            _miss.Clear();
            Type tModel = null, tTrade = null;
            try { tModel = AccessTools.TypeByName("NavalDLC.GameComponents.NavalDLCClanShipOwnershipModel"); tTrade = AccessTools.TypeByName("NavalDLC.CampaignBehaviors.ShipTradeCampaignBehavior"); } catch { }
            _wIdeal = Patch(h, "ideal rodu", tModel, "GetIdealShipNumberForClan", null, nameof(IdealPost));
            _wBuy = Patch(h, "zakup rodu", tTrade, "ConsiderPurchasingShip", nameof(BuyPrefix), null);
            Log.Info(Tag + "latki - ideal rodu " + (_wIdeal ? "tak" : "NIE") + ", zakup rodu " + (_wBuy ? "tak" : "NIE") + (_miss.Count > 0 ? "; BRAK: " + string.Join("; ", _miss.ToArray()) : "")
                     + ". Klucz Naval Realms " + (On ? "WLACZONY" : "wylaczony") + " (stan z chwili wpiecia).");
        }

        private static bool Patch(Harmony h, string label, Type t, string method, string pre, string post)
        {
            try
            {
                if (t == null) { _miss.Add(label + " (brak typu)"); return false; }
                var m = AccessTools.DeclaredMethod(t, method);
                if (m == null) { _miss.Add(label + " (brak metody " + method + ")"); return false; }
                h.Patch(m, prefix: pre != null ? new HarmonyMethod(typeof(NavalRealms), pre) : null, postfix: post != null ? new HarmonyMethod(typeof(NavalRealms), post) : null);
                return true;
            }
            catch (Exception e) { _miss.Add(label + " (blad: " + e.Message + ")"); Log.Error("NavalRealms.ApplyAll(" + label + ")", e); return false; }
        }

        public static void IdealPost(Clan __0, ref int __result)
        {
            try { if (On && __0 != null && __0 != Clan.PlayerClan) __result = IdealFor(__0, __result); }
            catch (Exception e) { Stumble("IdealPost", e); }
        }

        public static bool BuyPrefix(Clan __0)
        {
            try
            {
                if (!On || !Live || __0 == null || __0 == Clan.PlayerClan) return true;
                if (FactorOf(__0) < 0f) return true;
                if (ShipsOf(__0) >= IdealFor(__0, GameIdeal(__0)))
                {
                    if (HasStrandedEmpty(__0)) { _strandBuy++; return true; }   // po recenzji: partia odcieta od ladu stalego bez statkow - zakup dozwolony
                    _blocked++; return false;
                }
            }
            catch (Exception e) { Stumble("BuyPrefix", e); }
            return true;
        }

        // ============================================================ doba
        private static bool PartyOk(MobileParty mp)
        {
            return mp != null && mp.IsActive && !mp.IsDisbanding && !mp.IsMainParty && mp.LeaderHero != null && mp.MapEvent == null && mp.SiegeEvent == null
                   && mp.BesiegerCamp == null && !mp.IsCurrentlyAtSea && !mp.IsTransitionInProgress && !mp.IsInRaftState && !mp.IsCurrentlyUsedByAQuest
                   && mp.Army == null && !mp.StartTransitionNextFrameToExitFromPort && !mp.IsTargetingPort
                   && mp.DesiredAiNavigationType != MobileParty.NavigationType.Naval && mp.DesiredAiNavigationType != MobileParty.NavigationType.All
                   && mp.Ships != null && mp.Ships.Count > 0;
        }

        /// <summary>
        /// Partia odcieta od ladu stalego krolestwa (wyspa): brak drogi ladowej do siedziby rodu albo do siedziby rodu panujacego, albo siedziba rodu to miasto
        /// bez innego miasta w zasiegu ladu (IslandRoads). Bez statkow nie wrocilaby. Nie rzuca (blad - odcieta, statki zostaja).
        /// </summary>
        private static bool Stranded(MobileParty mp, Clan c)
        {
            try
            {
                var home = c.HomeSettlement;
                if (home == null || LandDist(mp, home) < 0f) return true;
                if (home.IsTown && IslandRoads.LonelyTown(home)) return true;
                var k = c.Kingdom;
                var core = k != null && k.RulingClan != null ? k.RulingClan.HomeSettlement : null;
                if (core != null && core != home && LandDist(mp, core) < 0f) return true;
                return false;
            }
            catch (Exception e) { Stumble("Stranded", e); return true; }
        }

        /// <summary>Rod ma partie wojenna bez statkow odcieta od ladu stalego (zakup mimo idealu). Nie rzuca.</summary>
        private static bool HasStrandedEmpty(Clan c)
        {
            try
            {
                foreach (var w in c.WarPartyComponents)
                {
                    var mp = w != null ? w.MobileParty : null;
                    if (mp == null || !mp.IsActive || mp.LeaderHero == null || mp.IsCurrentlyAtSea || mp.MapEvent != null || mp.Army != null) continue;
                    if (mp.Ships != null && mp.Ships.Count > 0) continue;
                    if (Stranded(mp, c)) return true;
                }
            }
            catch (Exception e) { Stumble("HasStrandedEmpty", e); }
            return false;
        }

        private static float LandDist(MobileParty p, Settlement s)
        {
            try
            {
                float lr;
                float d = Campaign.Current.Models.MapDistanceModel.GetDistance(p, s, false, MobileParty.NavigationType.Default, out lr);
                return d >= 0f && d < CartTownExit.BkLimit ? d : -1f;
            }
            catch { return -1f; }
        }

        private struct Port { public Town T; public float D; public bool Own; }

        private static List<Port> Ports(MobileParty p, Clan c)
        {
            var l = new List<Port>();
            foreach (var t in Town.AllTowns)
            {
                try
                {
                    if (t == null || t.Settlement == null || !t.IsTown || t.IsUnderSiege || t.Settlement.Party == null) continue;
                    if (t.MapFaction != null && c.MapFaction != null && t.MapFaction.IsAtWarWith(c.MapFaction)) continue;
                    if (ShipsToPort181.ShipyardLevelOf(t) <= 0) continue;
                    float d = LandDist(p, t.Settlement);
                    if (d < 0f) continue;
                    l.Add(new Port { T = t, D = d, Own = t.MapFaction == c.MapFaction });
                }
                catch (Exception e) { Stumble("Ports", e); }
            }
            l.Sort((a, b) => a.Own != b.Own ? (a.Own ? -1 : 1) : a.D.CompareTo(b.D));
            return l;
        }

        private static Ship Cheapest(MobileParty p)
        {
            Ship best = null; float bv = float.MaxValue;
            foreach (var sh in p.Ships)
            {
                if (sh == null || !sh.IsTradeable || sh.IsUsedByQuest) continue;
                float v;
                try { v = Campaign.Current.Models.ShipCostModel.GetShipTradeValue(sh, p.Party, null); } catch { v = 0f; }
                if (v < bv) { bv = v; best = sh; }
            }
            return best;
        }

        /// <summary>Jeden statek nadmiaru rodu. Wynik: 1 sprzedany, 2 zlozony, 0 nic.</summary>
        private static int Dispose(Clan c, int gameIdeal, int ideal, float fc)
        {
            var home = c.HomeSettlement;
            if (home == null) { _cut++; return 0; }
            if (HasStrandedEmpty(c)) { _cut++; return 0; }   // rod czeka na statek dla partii odcietej - bez sprzedazy (zakup gry moze trafic do innej partii; bez kupna-sprzedazy w kolko)
            MobileParty pick = null; int pickN = 0; bool anyCut = false, anyBusy = false;
            foreach (var w in c.WarPartyComponents)
            {
                var mp = w != null ? w.MobileParty : null;
                if (mp == null || mp.Ships == null || mp.Ships.Count == 0) continue;
                if (!PartyOk(mp)) { anyBusy = true; continue; }
                if (Stranded(mp, c)) { anyCut = true; continue; }   // na wyspie (bez drogi ladowej do domu albo do ladu stalego krolestwa) - statki zostaja
                if (mp.Ships.Count > pickN) { pick = mp; pickN = mp.Ships.Count; }
            }
            if (pick == null) { if (anyCut) _cut++; else if (anyBusy) _busy++; return 0; }
            var ship = Cheapest(pick);
            if (ship == null) { _busy++; return 0; }
            var ports = Ports(pick, c);
            if (ports.Count == 0) { _noPort++; return 0; }
            // po recenzji: bez 181 ApplyByTrade placi glowie rodu z niczego - nie sprzedajemy; nadmiar z naszego mnoznika od razu zlozony w porcie
            bool trade = ShipsToPort181.TradeOn;
            int tries = trade ? Math.Min(3, ports.Count) : 0;
            for (int i = 0; i < tries; i++)
            {
                var pt = ports[i];
                long g0 = c.Leader != null ? c.Leader.Gold : 0;
                try { ChangeShipOwnerAction.ApplyByTrade(pt.T.Settlement.Party, ship); }
                catch (Exception e) { Stumble("Sell", e); }
                if (ship.Owner == pt.T.Settlement.Party)
                {
                    long g1 = c.Leader != null ? c.Leader.Gold : 0;
                    _sold++; _soldGold += Math.Max(0L, g1 - g0); _fail.Remove(c.StringId);
                    if (_ex.Count < 8) _ex.Add(c.Name + " (" + Nm(c.Kingdom) + ") -> " + Nm(pt.T.Settlement) + " sprzedany");
                    return 1;
                }
                _rejected++;
            }
            int f = 0;
            if (trade) { _fail.TryGetValue(c.StringId, out f); _fail[c.StringId] = ++f; }
            if ((!trade || f >= (fc <= 0f ? LayUpDaysNoFleet : LayUpDays)) && ideal < gameIdeal)
            {
                var pt = ports[0];
                try { ChangeShipOwnerAction.ApplyByTransferring(pt.T.Settlement.Party, ship); }
                catch (Exception e) { Stumble("LayUp", e); }
                if (ship.Owner == pt.T.Settlement.Party)
                {
                    _laid++; _fail.Remove(c.StringId);
                    if (_ex.Count < 8) _ex.Add(c.Name + " (" + Nm(c.Kingdom) + ") -> " + Nm(pt.T.Settlement) + " zlozony bez zaplaty " + (trade ? "po " + f + " dobach" : "(181 wylaczony - bez sprzedazy)"));
                    return 2;
                }
            }
            return 0;
        }

        private sealed class KRow { public string Name; public int Clans, Ships, Ideal, GameIdeal, Over; }

        /// <summary>DailyTickEvent (NavalRealmsBehavior). Nie rzuca.</summary>
        internal static void Daily()
        {
            if (!Live) return;
            try
            {
                if (!_startLogged)
                {
                    _startLogged = true;
                    Log.Info(Tag + "start sesji | model idealu NavalDLC: " + ModelName() + " (latka na NavalDLCClanShipOwnershipModel - inny model = mnoznik nie dziala na sprzedaz gry, zakup i nadmiar dalej tak).");
                }
                bool on = On;
                var rows = new Dictionary<string, KRow>(StringComparer.Ordinal);
                foreach (var c in Clan.All)
                {
                    try
                    {
                        if (c == null || c.IsEliminated || c.IsBanditFaction || c == Clan.PlayerClan || c.Kingdom == null) continue;
                        int ships = ShipsOf(c);
                        int gi = GameIdeal(c);
                        int id = IdealFor(c, gi);
                        KRow r;
                        if (!rows.TryGetValue(c.Kingdom.StringId, out r)) { r = new KRow { Name = Nm(c.Kingdom) }; rows[c.Kingdom.StringId] = r; }
                        r.Clans++; r.Ships += ships; r.Ideal += id; r.GameIdeal += gi;
                        if (ships > id) r.Over += ships - id;
                        float fc = FactorOf(c);
                        if (on && ships > id && fc >= 0f)
                        {
                            // krolestwo bez floty (mnoznik 0 - Polnoc, Wolni Ludzie, Dothrakowie): do 3 statkow na dobe, zeby flota nie plywala jeszcze tygodniami
                            int max = Math.Min(ships - id, fc <= 0f ? 3 : 1);
                            for (int i = 0; i < max; i++) if (Dispose(c, gi, id, fc) == 0) break;
                        }
                    }
                    catch (Exception e) { Stumble("Clan", e); }
                }
                var l = new List<KRow>(rows.Values);
                l.Sort((a, b) => b.Ships.CompareTo(a.Ships));
                var sb = new StringBuilder(1500);
                sb.Append(Tag).Append("dzien ").Append((int)CampaignTime.Now.ToDays).Append(" | klucz ").Append(on ? "TAK" : "NIE (tylko pomiar)")
                  .Append(" | dzis: zakupy zablokowane (rod >= ideal) ").Append(_blocked).Append(", zakupy dozwolone partii odcietej bez statkow ").Append(_strandBuy).Append(", sprzedane portom ").Append(_sold)
                  .Append(" (zloto glow rodow +").Append(_soldGold.ToString(Inv)).Append(" z kas portow 181), zlozone w porcie bez zaplaty ").Append(_laid)
                  .Append(", odmowy portow ").Append(_rejected).Append(", rody z nadmiarem odciete od domu (wyspa - statki zostaja) ").Append(_cut)
                  .Append(", bez portu w zasiegu ladu ").Append(_noPort).Append(", wszystkie partie zajete ").Append(_busy)
                  .Append(" | krolestwa (statki / ideal 326a / ideal gry / nadmiar): ");
                int k = 0;
                foreach (var r in l)
                {
                    if (r.Ships == 0 && r.Ideal == 0) continue;
                    if (k++ > 0) sb.Append("; ");
                    sb.Append(r.Name).Append(' ').Append(r.Ships).Append('/').Append(r.Ideal).Append('/').Append(r.GameIdeal).Append('/').Append(r.Over);
                }
                if (_ex.Count > 0) sb.Append(" | przyklady: ").Append(string.Join("; ", _ex.ToArray()));
                sb.Append(" | potkniecia ").Append(_stumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            _blocked = _sold = _laid = _rejected = _cut = _noPort = _busy = _strandBuy = 0; _soldGold = 0; _ex.Clear();
        }

        private static string ModelName()
        {
            try
            {
                var t = AccessTools.TypeByName("NavalDLC.NavalDLCManager");
                var inst = t != null ? AccessTools.Property(t, "Instance").GetValue(null, null) : null;
                var gm = inst != null ? AccessTools.Property(inst.GetType(), "GameModels").GetValue(inst, null) : null;
                var m = gm != null ? AccessTools.Property(gm.GetType(), "ClanShipOwnershipModel").GetValue(gm, null) : null;
                return m != null ? m.GetType().FullName : "brak";
            }
            catch (Exception e) { return "blad odczytu (" + e.GetType().Name + ")"; }
        }
    }

    /// <summary>326a: doba flot krolestw (zapis arm_k326a - doby bez kupca).</summary>
    internal sealed class NavalRealmsBehavior : CampaignBehaviorBase
    {
        public NavalRealmsBehavior() { NavalRealms.Reset(); }
        public override void RegisterEvents() { CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, NavalRealms.Daily); }
        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                string v = dataStore.IsSaving ? NavalRealms.Export() : null;
                SaveText.Sync(dataStore, "arm_k326a", ref v);
                if (dataStore.IsLoading) NavalRealms.Import(v);
            }
            catch (Exception e) { Log.Error("SyncData.arm_k326a", e); }
        }
    }
}
