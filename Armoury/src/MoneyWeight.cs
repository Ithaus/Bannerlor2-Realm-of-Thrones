using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 521 WAGA PIENIEDZY (Jeff 14.11: "I PIENIADZE MAJA WAZYC ILE WAZY 179K zlota w tym sa oczywiscie pennies srebrne i zlote"; po propozycji: "1 tak, 2 tak,
    /// 3 lordowie nie woza ze soba 3 milionow maja skarbiec i tam trzymaja zloto lub gdzies").
    ///
    /// (1) Sklad sakiewki: 1 zl = 1 pens (HistoricalPrices). Pens srebrny 1.4 g, grosz (4 pensy) 3.9 g, nobel zloty (80 pensow) 7.8 g. Udzial wartosci
    ///     w sakiewce z MCM (Money Penny / Groat / Noble Percent, domyslnie 10 / 60 / 30, normowane do sumy) -> gramy na zl (domyslnie 0.754 g/zl,
    ///     179 000 zl = 135 kg). Mennica (CrownMint) nie trzyma skladu monety w obiegu (liczniki sztuk tylko z doby) - sklad staly z ustawien.
    /// (2) Waga w udzwigu: postfiks DefaultInventoryCapacityModel.CalculateTotalWeightCarried (DeclaredMethod - pulapka 15.10) dopisuje pozycje "Coin".
    ///     NavalDLC wola model domyslny (BaseModel) - dziala tez na morzu. Predkosc bierze wage z MobileParty.TotalWeightCarried (kara ladunku -0.02 x
    ///     waga/udzwig, przeciazenie -0.4 x nadwyzka/udzwig) - bez wlasnej latki predkosci.
    ///     Gra pamieta wage (MobileParty._totalWeightCarriedCache) do zmiany VersionNo, a zmiana zlota VersionNo nie rusza: tik GODZINOWY liczy kubelek
    ///     floor(kg / MoneyWeightStepKg) i przy zmianie kubelka woła UpdateVersionNo (jak Litter501.Touch). Nic co klatke.
    /// (3) Kto ile niesie: gracz - cale Hero.Gold (sam decyduje, co odklada do skarbca). Partia lorda AI (IsLordParty z wodzem, takze towarzysze gracza) -
    ///     tylko sakiewka robocza min(Gold, LordPurseFlat + LordPurseDays x zold partii); reszta majatku "w skarbcu" - KSIEGOWO (zloto zostaje w Hero.Gold:
    ///     gra placi zold, BK i nasze moduly wydaja z Hero.Gold; przesuniecie zlota AI zmieniloby podatek od nadwyzki, egzekucje Banku i dezercje).
    ///     Zold partii z pamieci odswiezanej raz na dobe (tik godzinowy, partie bez wpisu albo z wpisem starszym niz doba) - model zoldu nie w postfiksie.
    ///     Partie bez wodza (karawany, tabory wsi, bandy, inne) - cala PartyTradeGold przy MoneyWeightPartiesWithoutLord; garnizony i milicja - bez wagi.
    /// (4) Skarbiec gracza (PlayerVaultEnabled): w menu wlasnego miasta albo zamku (rod gracza) wplata / wyplata zlota z sakiewki; w Braavos to samo
    ///     u Banku Zelaznego (przechowanie, bez odsetek - Bank tego zlota nie pozycza). Kiesa per osada, zapis "arm_clanvault". Przelew Hero.ChangeHeroGold
    ///     <-> skarbiec - posiadacz ksiegi pieniadza "skarbce gracza (521)" (MoneyLedger.HVault), suma swiata bez zmian ("nic z niczego").
    ///     Utrata osady: zdobycie (BySiege) - skarbiec do zdobywcy; inna zmiana wlasciciela - do innej twierdzy rodu gracza (pierwsze miasto, potem zamek),
    ///     bez twierdz - do sakiewki gracza. Wylaczony klucz: wplat nie ma, wyplata zostaje (zloto nie utyka).
    ///     Menu: zwykle menu "town"/"castle" (nie menu oczekiwania), wybor kwoty MultiSelectionInquiry, bez SwitchToMenu w opcji.
    /// (5) Linia startu sesji i linia doby w Armoury.log ("Waga pieniedzy (521)"). Bledy liczone (potkniecia), bez globalnego wylacznika.
    /// </summary>
    internal static class MoneyWeight
    {
        private const string P = "Waga pieniedzy (521): ";
        private const string VaultKey = "arm_clanvault";
        internal const string BankKey = "bank";   // skarbiec gracza u Banku Zelaznego (Braavos) - klucz zamiast id osady

        // gramy monety na 1 zl wartosci (1 zl = 1 pens)
        private const double GPenny = 1.4;            // pens srebrny 1.4 g = 1 zl
        private const double GGroat = 3.9 / 4.0;      // grosz 3.9 g = 4 zl
        private const double GNoble = 7.8 / 80.0;     // nobel zloty 7.8 g = 80 zl

        // ------------------------------------------------------------ stan
        private static readonly Dictionary<string, long> _vault = new Dictionary<string, long>();                       // id osady (albo "bank") -> zloto gracza
        private static readonly ConcurrentDictionary<MobileParty, int> _wage = new ConcurrentDictionary<MobileParty, int>();   // zold partii lorda (postfiks czyta rownolegle)
        private static readonly Dictionary<MobileParty, int> _wageDay = new Dictionary<MobileParty, int>();            // doba odczytu zoldu (watek glowny)
        private static readonly Dictionary<MobileParty, int> _bucket = new Dictionary<MobileParty, int>();             // ostatni kubelek wagi (watek glowny)
        private static string _sig = "";                    // podpis ustawien - zmiana = odswiezenie wszystkich partii raz
        private static volatile float _kgPerGold;            // kg na 1 zl (z ustawien, liczone w watku glownym)
        private static volatile bool _on, _noLord;
        private static volatile int _days = 30, _flat = 5000;

        // liczniki (linia doby)
        private static int _post, _postCoin;                 // wywolania postfiksu / z dopisana waga (Interlocked)
        private static int _touch, _wageReads, _stumbles, _parStumbles, _dIn, _dOut, _dInN, _dOutN, _lostSiege, _moved;
        private static long _lostSiegeGold, _movedGold;
        private static int _day = -1;
        private static string _hooks = "nie wpieta";
        private static bool _menus;
        private static readonly HashSet<string> _err = new HashSet<string>();

        private static TextObject _txt;   // leniwie (w kampanii)
        private static TextObject Txt { get { return _txt ?? (_txt = new TextObject("{=!}Coin")); } }

        internal static void Reset()
        {
            _vault.Clear(); _wage.Clear(); _wageDay.Clear(); _bucket.Clear(); _sig = "";
            _post = _postCoin = 0; _touch = _wageReads = _stumbles = _parStumbles = 0; _dIn = _dOut = _dInN = _dOutN = 0;
            _lostSiege = _moved = 0; _lostSiegeGold = _movedGold = 0; _day = -1; _err.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("MoneyWeight." + where, e); } catch { }
        }

        // ------------------------------------------------------------ ustawienia
        /// <summary>kg na 1 zl ze skladu sakiewki (udzial wartosci pensow / groszy / nobli, normowany do sumy; suma 0 - sklad domyslny 10/60/30).</summary>
        internal static double KgPerGold(Settings s)
        {
            double p = Math.Max(0, s.MoneyPennyPercent), g = Math.Max(0, s.MoneyGroatPercent), n = Math.Max(0, s.MoneyNoblePercent);
            double sum = p + g + n;
            if (sum <= 0) { p = 10; g = 60; n = 30; sum = 100; }
            return (p * GPenny + g * GGroat + n * GNoble) / sum / 1000.0;
        }

        /// <summary>Przepisanie ustawien do pol czytanych rownolegle; zmiana podpisu = wszystkie partie raz do przeliczenia. Watek glowny.</summary>
        private static void Pull(Settings s)
        {
            bool on = s != null && s.MoneyWeightEnabled;
            _on = on;
            if (s == null) return;
            _kgPerGold = (float)KgPerGold(s);
            _noLord = s.MoneyWeightPartiesWithoutLord;
            _days = Math.Max(0, s.LordPurseDays);
            _flat = Math.Max(0, s.LordPurseFlat);
            string sig = (on ? "1" : "0") + "|" + _kgPerGold.ToString("0.########", CultureInfo.InvariantCulture) + "|" + (_noLord ? 1 : 0) + "|" + _days + "|" + _flat + "|" + Step(s);
            if (sig == _sig) return;
            bool first = _sig.Length == 0;
            _sig = sig;
            // pierwsze przepisanie (start sesji) albo zmiana w MCM: gra mogla juz zapamietac wage bez monety - wszystkie partie raz do przeliczenia
            int n = 0;
            foreach (var mp in MobileParty.All) { if (mp != null && mp.IsActive) { Touch(mp); n++; } }
            _bucket.Clear();
            Log.Info(P + (first ? "start" : "zmiana ustawien") + " - waga przeliczona w " + n + " partiach (" + sig + ").");
        }

        private static float Step(Settings s) { return Math.Max(0.5f, s.MoneyWeightStepKg); }

        private static void Touch(MobileParty mp) { try { if (mp != null && mp.IsActive) { mp.UpdateVersionNo(); _touch++; } } catch { } }

        // ------------------------------------------------------------ ile niesie partia (watek glowny i rownolegle - tylko odczyt)
        /// <summary>Zloto niesione przez partie (zl). lordPurse = true - sakiewka robocza lorda AI (reszta w skarbcu ksiegowo).</summary>
        internal static long Carried(MobileParty mp, out bool lordPurse, out long whole)
        {
            lordPurse = false; whole = 0;
            if (mp == null) return 0;
            if (mp == MobileParty.MainParty)
            {
                var h = Hero.MainHero;
                whole = h != null ? Math.Max(0, h.Gold) : 0;
                return whole;
            }
            if (mp.IsGarrison || mp.IsMilitia) return 0;
            var lead = mp.LeaderHero;
            if (mp.IsLordParty && lead != null)
            {
                whole = Math.Max(0, lead.Gold);
                int w;
                if (!_wage.TryGetValue(mp, out w)) w = 0;
                long purse = (long)_flat + (long)_days * Math.Max(0, w);
                lordPurse = true;
                return Math.Min(whole, purse);
            }
            if (!_noLord) return 0;
            whole = Math.Max(0, mp.PartyTradeGold);
            return whole;
        }

        // ------------------------------------------------------------ latka
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(DefaultInventoryCapacityModel), "CalculateTotalWeightCarried");
                if (m == null) _hooks = "BRAK CalculateTotalWeightCarried - pieniadze nic nie waza";
                else { h.Patch(m, postfix: new HarmonyMethod(typeof(MoneyWeight), nameof(WeightPostfix))); _hooks = "wpieta (DefaultInventoryCapacityModel.CalculateTotalWeightCarried)"; }
            }
            catch (Exception e) { _hooks = "blad " + e.GetType().Name; Stumble("ApplyAll", e); }
            Log.Info(P + "latka wagi " + _hooks + ".");
        }

        /// <summary>Postfiks wagi niesionej: pozycja "Coin" = zloto niesione x kg/zl. Bywa wolany rownolegle - tylko odczyt.</summary>
        public static void WeightPostfix(MobileParty __0, ref ExplainedNumber __result)
        {
            try
            {
                Interlocked.Increment(ref _post);
                if (!_on || __0 == null) return;
                bool lp; long whole;
                long g = Carried(__0, out lp, out whole);
                if (g <= 0) return;
                float kg = (float)(g * (double)_kgPerGold);
                if (kg <= 0f) return;
                __result.Add(kg, Txt);
                Interlocked.Increment(ref _postCoin);
            }
            catch { Interlocked.Increment(ref _parStumbles); }
        }

        // ------------------------------------------------------------ wpiecie
        internal static void RegisterEvents(CampaignBehaviorBase b)
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(b, delegate { try { Reset(); } catch (Exception e) { Stumble("Reset", e); } });
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(b, delegate (CampaignGameStarter s) { try { SessionStart(s); } catch (Exception e) { Stumble("SessionStart", e); } });
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(b, DayProfile.Hr("MoneyWeight.Hourly", Hourly));
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(b, OnOwner);
        }

        private static void SessionStart(CampaignGameStarter starter)
        {
            var s = Settings.Current;
            Pull(s);
            if (starter != null) { try { AddMenus(starter); _menus = true; } catch (Exception e) { Stumble("AddMenus", e); } }
            string model = "?";
            try { var cm = Campaign.Current != null ? Campaign.Current.Models.InventoryCapacityModel : null; model = cm != null ? cm.GetType().Name : "brak"; } catch { }
            long vt = TotalNow();
            Log.Info(P + "start sesji | klucz " + (s != null && s.MoneyWeightEnabled ? "TAK" : "NIE") + " (Money Weight Enabled)"
                     + (s != null ? " | sklad sakiewki: pensy " + s.MoneyPennyPercent + "% / grosze " + s.MoneyGroatPercent + "% / noble " + s.MoneyNoblePercent + "% wartosci -> "
                                    + (KgPerGold(s) * 1000.0).ToString("0.000", CultureInfo.InvariantCulture) + " kg na 1000 zl (179 000 zl = "
                                    + (KgPerGold(s) * 179000.0).ToString("0.0", CultureInfo.InvariantCulture) + " kg)"
                                    + " | sakiewka lorda AI: " + s.LordPurseFlat + " zl + " + s.LordPurseDays + " dni zoldu (reszta w skarbcu ksiegowo)"
                                    + " | partie bez wodza " + (s.MoneyWeightPartiesWithoutLord ? "TAK" : "NIE") + " | krok odswiezenia " + Step(s).ToString("0.#", CultureInfo.InvariantCulture) + " kg"
                                    + " | skarbiec gracza " + (s.PlayerVaultEnabled ? "TAK" : "NIE") : "")
                     + " | latka " + _hooks + ", model udzwigu " + model + " | menu " + (_menus ? "tak" : "NIE")
                     + " | skarbce gracza: " + _vault.Count(kv => kv.Value > 0) + " miejsc, " + vt + " zl.");
        }

        // ------------------------------------------------------------ tik godzinowy: zold, kubelki, linia doby
        internal static void Hourly()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) return;
                Pull(s);
                int day = (int)CampaignTime.Now.ToDays;
                if (_day < 0) _day = day;
                else if (day != _day) { try { DayLine(s); } catch (Exception e) { Stumble("DayLine", e); } _day = day; }
                if (!_on) return;
                float step = Step(s);
                foreach (var mp in MobileParty.All)
                {
                    try
                    {
                        if (mp == null || !mp.IsActive) continue;
                        if (mp.IsLordParty && mp.LeaderHero != null && mp != MobileParty.MainParty)
                        {
                            int wd;
                            if (!_wageDay.TryGetValue(mp, out wd) || day - wd >= 1)
                            {
                                _wage[mp] = Math.Max(0, mp.TotalWage);
                                _wageDay[mp] = day; _wageReads++;
                            }
                        }
                        bool lp; long whole;
                        long g = Carried(mp, out lp, out whole);
                        int b = g > 0 ? (int)Math.Floor(g * (double)_kgPerGold / step) : 0;
                        int old;
                        if (!_bucket.TryGetValue(mp, out old)) { _bucket[mp] = b; continue; }   // pierwszy odczyt - waga juz z moneta (Pull przeliczyl wszystkie)
                        if (old == b) continue;
                        _bucket[mp] = b;
                        Touch(mp);
                    }
                    catch (Exception e) { Stumble("Hourly(partia)", e); }
                }
            }
            catch (Exception e) { Stumble("Hourly", e); }
        }

        private static void Prune()
        {
            var dead = new List<MobileParty>();
            foreach (var kv in _bucket) if (kv.Key == null || !kv.Key.IsActive) dead.Add(kv.Key);
            foreach (var mp in dead) { if (mp == null) continue; _bucket.Remove(mp); }
            dead.Clear();
            foreach (var kv in _wageDay) if (kv.Key == null || !kv.Key.IsActive || !kv.Key.IsLordParty) dead.Add(kv.Key);
            foreach (var mp in dead) { if (mp == null) continue; _wageDay.Remove(mp); int x; _wage.TryRemove(mp, out x); }
        }

        private static string Kg(double kg) { return kg.ToString("0.0", CultureInfo.InvariantCulture); }

        private static void DayLine(Settings s)
        {
            Prune();
            double k = _kgPerGold;
            var sb = new StringBuilder(P + "doba " + _day + " | klucz " + (_on ? "TAK" : "NIE"));
            var mp = MobileParty.MainParty;
            var hero = Hero.MainHero;
            if (mp != null && hero != null && mp.IsActive)
            {
                long g = Math.Max(0, hero.Gold);
                double coin = _on ? g * k : 0;
                float w = mp.TotalWeightCarried;
                int cap = mp.InventoryCapacity;
                double over = Math.Max(0, w - cap);
                double fromCoin = Math.Min(coin, over);
                // kara predkosci od monety: przeciazenie -0.4 x (czesc nadwyzki z monety)/udzwig, ladunek w granicach -0.02 x (moneta w granicach)/udzwig
                double w0 = Math.Max(0, w - coin);
                double dIn = Math.Min(w, cap) - Math.Min(w0, cap);
                double dOver = Math.Max(0, w - cap) - Math.Max(0, w0 - cap);
                double pen = cap > 0 ? (0.02 * dIn + 0.4 * dOver) / cap : 0;
                sb.Append(" | gracz: ").Append(g).Append(" zl = ").Append(Kg(coin)).Append(" kg; ladunek ").Append(Kg(w)).Append(" / udzwig ").Append(cap).Append(" kg")
                  .Append(over > 0 ? " - PRZECIAZENIE " + Kg(over) + " kg (z monety " + Kg(fromCoin) + ")" : "")
                  .Append("; kara predkosci od monety ok. -").Append((pen * 100).ToString("0.0", CultureInfo.InvariantCulture)).Append("%; predkosc ").Append(mp.Speed.ToString("0.00", CultureInfo.InvariantCulture));
            }
            int lords = 0; long lCar = 0, lWhole = 0, lMax = 0; string lMaxN = "";
            int cars = 0, bands = 0, vils = 0, oth = 0; long cG = 0, bG = 0, vG = 0, oG = 0;
            foreach (var p in MobileParty.All)
            {
                if (p == null || !p.IsActive || p == mp) continue;
                bool lp; long whole;
                long g = Carried(p, out lp, out whole);
                if (lp) { lords++; lCar += g; lWhole += whole; if (g > lMax) { lMax = g; lMaxN = p.LeaderHero != null ? p.LeaderHero.Name.ToString() : "?"; } continue; }
                if (g <= 0) continue;
                if (p.IsCaravan) { cars++; cG += g; } else if (p.IsBandit) { bands++; bG += g; } else if (p.IsVillager) { vils++; vG += g; } else { oth++; oG += g; }
            }
            sb.Append(" | lordowie AI: ").Append(lords).Append(" partii, niosa ").Append(lCar).Append(" zl (").Append(Kg(lCar * k)).Append(" kg, srednio ")
              .Append(Kg(lords > 0 ? lCar * k / lords : 0)).Append(" kg), w skarbcach ksiegowo ").Append(lWhole - lCar).Append(" zl; najciezsza sakiewka ")
              .Append(lMax).Append(" zl (").Append(Kg(lMax * k)).Append(" kg, ").Append(lMaxN).Append(")");
            sb.Append(" | bez wodza: karawany ").Append(cars).Append(" (").Append(cG).Append(" zl, ").Append(Kg(cG * k)).Append(" kg), bandy ").Append(bands).Append(" (").Append(Kg(bG * k))
              .Append(" kg), tabory wsi ").Append(vils).Append(" (").Append(Kg(vG * k)).Append(" kg), inne ").Append(oth).Append(" (").Append(Kg(oG * k)).Append(" kg)");
            sb.Append(" | skarbce gracza ").Append(TotalNow()).Append(" zl w ").Append(_vault.Count(kv => kv.Value > 0)).Append(" miejscach; wplaty ").Append(_dInN).Append(" (").Append(_dIn)
              .Append(" zl), wyplaty ").Append(_dOutN).Append(" (").Append(_dOut).Append(" zl)");
            if (_lostSiege > 0 || _moved > 0) sb.Append(", do zdobywcy ").Append(_lostSiege).Append(" (").Append(_lostSiegeGold).Append(" zl), przeniesione ").Append(_moved).Append(" (").Append(_movedGold).Append(" zl)");
            int post = Interlocked.Exchange(ref _post, 0), pc = Interlocked.Exchange(ref _postCoin, 0), ps = Interlocked.Exchange(ref _parStumbles, 0);
            sb.Append(" | postfiks ").Append(post).Append(" wywolan (z moneta ").Append(pc).Append("), odswiezenia wagi ").Append(_touch).Append(", odczyty zoldu ").Append(_wageReads)
              .Append(" | potkniecia ").Append(_stumbles).Append(", rownolegle ").Append(ps).Append('.');
            Log.Info(sb.ToString());
            _touch = _wageReads = _stumbles = 0; _dIn = _dOut = _dInN = _dOutN = 0; _lostSiege = _moved = 0; _lostSiegeGold = _movedGold = 0;
        }

        // ------------------------------------------------------------ skarbiec gracza
        /// <summary>Posiadacz "skarbce gracza (521)" ksiegi pieniadza - suma wszystkich skarbcow gracza (z bankiem).</summary>
        internal static long TotalNow() { long t = 0; foreach (var kv in _vault) t += kv.Value; return t; }

        private static long VaultOf(string key) { long v; return key != null && _vault.TryGetValue(key, out v) ? v : 0; }

        private static void Put(string key, long d)
        {
            long v = VaultOf(key) + d;
            if (v <= 0) _vault.Remove(key); else _vault[key] = v;
        }

        /// <summary>Klucz skarbca w biezacej osadzie: wlasne miasto / zamek rodu gracza - id osady; Braavos - "bank"; inaczej null.</summary>
        private static string HereKey(out string where)
        {
            where = "";
            var st = Settlement.CurrentSettlement;
            if (st == null) return null;
            if (st.StringId == IronBank.BraavosId) { where = "the Iron Bank"; return BankKey; }
            if ((st.IsTown || st.IsCastle) && st.OwnerClan != null && st.OwnerClan == Clan.PlayerClan) { where = "your treasury in " + st.Name; return st.StringId; }
            return null;
        }

        private static bool VaultKeyOn { get { var s = Settings.Current; return s != null && s.PlayerVaultEnabled; } }

        private static void AddMenus(CampaignGameStarter starter)
        {
            foreach (var menu in new[] { "town", "castle" })
            {
                starter.AddGameMenuOption(menu, "arm_vault_in_" + menu, "{=!}{ARM_VAULT_IN}",
                    delegate (MenuCallbackArgs a)
                    {
                        try
                        {
                            a.optionLeaveType = GameMenuOption.LeaveType.Trade;
                            string where;
                            string key = HereKey(out where);
                            if (key == null || !VaultKeyOn) return false;
                            MBTextManager.SetTextVariable("ARM_VAULT_IN", "Leave coin in " + where + " (kept: " + VaultOf(key) + ")");
                            var h = Hero.MainHero;
                            if (h == null || h.Gold <= 0) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}You carry no coin."); }
                            return true;
                        }
                        catch (Exception e) { Stumble("MenuIn", e); return false; }
                    },
                    delegate { try { DepositDialog(); } catch (Exception e) { Stumble("DepositDialog", e); } }, false, 5);
                starter.AddGameMenuOption(menu, "arm_vault_out_" + menu, "{=!}{ARM_VAULT_OUT}",
                    delegate (MenuCallbackArgs a)
                    {
                        try
                        {
                            a.optionLeaveType = GameMenuOption.LeaveType.Trade;
                            string where;
                            string key = HereKey(out where);
                            if (key == null) return false;
                            long v = VaultOf(key);
                            if (v <= 0) return false;   // nic nie lezy - bez opcji (takze przy wylaczonym kluczu wyplata zostaje)
                            MBTextManager.SetTextVariable("ARM_VAULT_OUT", "Take coin from " + where + " (kept: " + v + ")");
                            return true;
                        }
                        catch (Exception e) { Stumble("MenuOut", e); return false; }
                    },
                    delegate { try { WithdrawDialog(); } catch (Exception e) { Stumble("WithdrawDialog", e); } }, false, 6);
            }
        }

        private static int WageOfPlayer() { try { var mp = MobileParty.MainParty; return mp != null ? Math.Max(0, mp.TotalWage) : 0; } catch { return 0; } }

        private static void DepositDialog()
        {
            string where;
            string key = HereKey(out where);
            var h = Hero.MainHero;
            if (key == null || h == null || !VaultKeyOn) return;
            int have = h.Gold;
            if (have <= 0) return;
            int wage = WageOfPlayer();
            var opts = new List<InquiryElement>();
            var seen = new HashSet<int>();
            int keep10 = have - 10 * wage;
            if (wage > 0 && keep10 > 0 && seen.Add(keep10)) opts.Add(new InquiryElement(keep10, keep10 + " gold (keep ten days' wages: " + (10 * wage) + ")", null));
            foreach (var share in new[] { 0.25f, 0.5f, 0.75f, 1f })
            {
                int amt = (int)(have * share);
                if (amt > 0 && seen.Add(amt)) opts.Add(new InquiryElement(amt, amt + " gold" + (share >= 1f ? " (all)" : ""), null));
            }
            var s = Settings.Current;
            double kgPer = s != null ? KgPerGold(s) : 0;
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "Treasury", "You carry " + have + " gold, about " + Kg(have * kgPer) + " kg of silver and gold. How much will you leave in " + where + "?"
                + " Coin left here is safe from the road but can only be taken back here.", opts, true, 1, 1, "Leave it", "Cancel",
                delegate (List<InquiryElement> sel)
                {
                    try
                    {
                        if (sel == null || sel.Count == 0) return;
                        string w2; string k2 = HereKey(out w2);
                        if (k2 != key) return;
                        int amt = Math.Min((int)sel[0].Identifier, Hero.MainHero.Gold);
                        if (amt <= 0) return;
                        Hero.MainHero.ChangeHeroGold(-amt);
                        Put(key, amt);
                        _dIn += amt; _dInN++;
                        Touch(MobileParty.MainParty);
                        int left = Hero.MainHero.Gold;
                        int w = WageOfPlayer();
                        InformationManager.DisplayMessage(new InformationMessage("You leave " + amt + " gold in " + where + ". Kept there: " + VaultOf(key) + ".", Colors.Green));
                        if (w > 0 && left < 10 * w)
                            InformationManager.DisplayMessage(new InformationMessage("You now carry less than ten days' wages (" + (10 * w) + " gold) - your men must be paid from what you carry.", Colors.Red));
                        Log.Info(P + "gracz wplaca " + amt + " zl do skarbca " + key + " (stan " + VaultOf(key) + ", niesie " + left + " zl, zold " + w + "/d).");
                    }
                    catch (Exception e) { Stumble("Deposit", e); }
                },
                delegate (List<InquiryElement> _) { }), true);
        }

        private static void WithdrawDialog()
        {
            string where;
            string key = HereKey(out where);
            if (key == null) return;
            long v = VaultOf(key);
            if (v <= 0) return;
            int cap = (int)Math.Min(v, int.MaxValue / 2);
            var opts = new List<InquiryElement>();
            var seen = new HashSet<int>();
            foreach (var share in new[] { 0.1f, 0.25f, 0.5f, 1f })
            {
                int amt = (int)(cap * share);
                if (amt > 0 && seen.Add(amt)) opts.Add(new InquiryElement(amt, amt + " gold" + (share >= 1f ? " (all)" : ""), null));
            }
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "Treasury", v + " gold is kept in " + where + ". How much will you take with you?", opts, true, 1, 1, "Take it", "Cancel",
                delegate (List<InquiryElement> sel)
                {
                    try
                    {
                        if (sel == null || sel.Count == 0) return;
                        string w2; string k2 = HereKey(out w2);
                        if (k2 != key) return;
                        int amt = (int)Math.Min((long)(int)sel[0].Identifier, VaultOf(key));
                        if (amt <= 0) return;
                        Put(key, -amt);
                        Hero.MainHero.ChangeHeroGold(amt);
                        _dOut += amt; _dOutN++;
                        Touch(MobileParty.MainParty);
                        InformationManager.DisplayMessage(new InformationMessage("You take " + amt + " gold from " + where + ". Kept there: " + VaultOf(key) + ".", Colors.Green));
                        Log.Info(P + "gracz podejmuje " + amt + " zl ze skarbca " + key + " (stan " + VaultOf(key) + ", niesie " + Hero.MainHero.Gold + " zl).");
                    }
                    catch (Exception e) { Stumble("Withdraw", e); }
                },
                delegate (List<InquiryElement> _) { }), true);
        }

        /// <summary>Zmiana wlasciciela osady ze skarbcem gracza: zdobycie - do zdobywcy; inaczej - do innej twierdzy rodu gracza albo do sakiewki gracza.</summary>
        private static void OnOwner(Settlement st, bool open, Hero newOwner, Hero oldOwner, Hero capturer, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail d)
        {
            try
            {
                if (st == null) return;
                long v = VaultOf(st.StringId);
                if (v <= 0) return;
                if (st.OwnerClan != null && st.OwnerClan == Clan.PlayerClan) return;
                int amt = (int)Math.Min(v, int.MaxValue / 2);
                if (d == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege && capturer != null && capturer.IsAlive && capturer != Hero.MainHero)
                {
                    Put(st.StringId, -amt);
                    capturer.ChangeHeroGold(amt);
                    _lostSiege++; _lostSiegeGold += amt;
                    Log.Player("The treasury of " + st.Name + " has fallen to " + capturer.Name + " - " + amt + " gold kept there is lost to you.", true);
                    Log.Info(P + "skarbiec " + st.StringId + " zdobyty - " + amt + " zl do zdobywcy " + capturer.Name + ".");
                    return;
                }
                Settlement to = null;
                var pc = Clan.PlayerClan;
                if (pc != null && pc.Settlements != null)
                {
                    foreach (var o in pc.Settlements) if (o != null && o != st && o.IsTown) { to = o; break; }
                    if (to == null) foreach (var o in pc.Settlements) if (o != null && o != st && o.IsCastle) { to = o; break; }
                }
                Put(st.StringId, -amt);
                _moved++; _movedGold += amt;
                if (to != null)
                {
                    Put(to.StringId, amt);
                    Log.Player("The " + amt + " gold kept in " + st.Name + " is carried to your treasury in " + to.Name + ".");
                    Log.Info(P + "skarbiec " + st.StringId + " (" + d + ") - " + amt + " zl przeniesione do " + to.StringId + ".");
                }
                else if (Hero.MainHero != null)
                {
                    Hero.MainHero.ChangeHeroGold(amt);
                    Touch(MobileParty.MainParty);
                    Log.Player("The " + amt + " gold kept in " + st.Name + " is brought back to you.");
                    Log.Info(P + "skarbiec " + st.StringId + " (" + d + ") - " + amt + " zl do sakiewki gracza (rod bez twierdz).");
                }
            }
            catch (Exception e) { Stumble("OnOwner", e); }
        }

        // ------------------------------------------------------------ zapis (arm_clanvault: id=zloto;id=zloto)
        internal static string Export()
        {
            var sb = new StringBuilder();
            foreach (var kv in _vault) { if (kv.Value <= 0) continue; if (sb.Length > 0) sb.Append(';'); sb.Append(kv.Key).Append('=').Append(kv.Value.ToString(CultureInfo.InvariantCulture)); }
            return sb.ToString();
        }

        internal static void Import(string data)
        {
            _vault.Clear();
            if (string.IsNullOrEmpty(data)) return;
            foreach (var part in data.Split(';'))
            {
                int i = part.IndexOf('=');
                if (i <= 0) continue;
                long v;
                if (long.TryParse(part.Substring(i + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) && v > 0) _vault[part.Substring(0, i)] = v;
            }
            Log.Info(P + "wczytane skarbce gracza: " + _vault.Count + " miejsc, " + TotalNow() + " zl.");
        }

        internal static void Sync(IDataStore ds)
        {
            string v = ds.IsSaving ? Export() : null;
            SaveText.Sync(ds, VaultKey, ref v);
            if (ds.IsLoading) Import(v);
        }
    }
}
