using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace Armoury
{
    /// <summary>
    /// PACZKA 185 - KONTRAKT NAJEMNIKA AI (projekt etapu 2, krok C1; PLAN 2.10, D-6). Wylacznik MercContractEnabled (czynny z 165 - kontrakt placony
    /// z wplywow dnia korony).
    ///  - W dniu najmu (pierwsza doba w sluzbie u tej korony) kontrakt K = MercContractFactor (1.3) x dzienny zold kompanii (zold 1.0 + jedzenie ok. 0.15
    ///    + sprzet ok. 0.15) i ludzie z umowy = ludzie kompanii. W pokoju "w oczekiwaniu": MercPeaceShare (polowa) K i polowa ludzi.
    ///  - Przeglad co MercReviewDays (28) tylko w dol: kompania ma mniej niz MercReviewFloor (75%) ludzi z umowy -> K i ludzie do stanu faktycznego.
    ///  - Pulap 166 najemnika = zold ludzi z umowy (ClanBudget; bez udzialu "dwor"). Bez zwrotu 50% (KingdomTreasury - jak dotad).
    ///  - Placi skarbiec pracodawcy z wplywow dnia, w kolejnosci 165 (po ratach reparacji, przed zwrotem); gdy nie starcza - proporcjonalnie, niedoplata
    ///    przepada; niedoplata > 50% przez MercUnpaidLeaveDays (28) dob z rzedu - kompania odchodzi ze sluzby.
    ///  - Gra dla AI w sluzbie nie placi nic z siebie (MercGameContractAiOff): "za tier" (Tier x 120 z niczego) = 0 i kontrakt gry (AddMercenaryIncome:
    ///    wplyw x mnoznik z MercenaryWallet, ktory splacaja wasale) = 0 - bez dopisku do MercenaryWallet, wiec wasale placa tylko za kontrakt gracza.
    ///    GRACZ-najemnik zostaje na kontrakcie gry (prawdziwy platnik - wasale).
    /// Platnik -> odbiorca: skarbiec pracodawcy -> glowa rodu najemnego (D: czesc "kontrakt"). Zapis umow: SaveText "arm_merc185".
    /// </summary>
    internal static class MercContract
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.MercContractEnabled && CrownIncome.On; } }
        private static bool GameOff { get { var s = Settings.Current; return s != null && s.MercGameContractAiOff && On; } }

        private sealed class Deal { public string Kingdom; public long K, Wage; public int Men, Day, Review, Unpaid; }
        private static readonly Dictionary<string, Deal> _deals = new Dictionary<string, Deal>();   // id rodu -> umowa

        // liczniki doby
        internal static long LastDue, LastPaid, LastTierOff, LastGameOff;
        internal static int LastClans, LastNew, LastCut, LastLeft;
        private static long _dTierOff, _dGameOff;
        private static int _stumbles, _importN = -1, _importBad;
        private static readonly HashSet<string> _err = new HashSet<string>();

        internal static void Reset() { _deals.Clear(); ZeroLast(); _dTierOff = _dGameOff = 0; _stumbles = 0; _err.Clear(); _importN = -1; _importBad = 0; }
        internal static void ZeroLast() { LastDue = LastPaid = LastTierOff = LastGameOff = 0; LastClans = LastNew = LastCut = LastLeft = 0; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("MercContract." + where, e); } catch { }
        }

        private static bool AiMerc(Clan c)
        {
            return c != null && c != Clan.PlayerClan && c.IsUnderMercenaryService && !c.IsEliminated && c.Kingdom != null && c.Leader != null && c.Leader.IsAlive && c.StringId != null;
        }

        private static void Company(Clan c, out long wage, out int men)
        {
            wage = 0; men = 0;
            var wps = c.WarPartyComponents;
            if (wps == null) return;
            for (int i = 0; i < wps.Count; i++)
            {
                var mp = wps[i] != null ? wps[i].MobileParty : null;
                if (mp == null || !mp.IsActive) continue;
                wage += Math.Max(0, mp.TotalWage);
                if (mp.MemberRoster != null) men += mp.MemberRoster.TotalRegulars;
            }
        }

        /// <summary>166: pulap zoldu kompanii w sluzbie = zold ludzi z umowy (w pokoju polowa); false - brak umowy.</summary>
        internal static bool CapOf(Clan c, bool war, out double cap)
        {
            cap = 0;
            Deal d;
            if (!On || c == null || c.StringId == null || !_deals.TryGetValue(c.StringId, out d) || c.Kingdom == null || d.Kingdom != c.Kingdom.StringId) return false;
            var s = Settings.Current;
            cap = d.Wage * (war ? 1.0 : Math.Max(0f, Math.Min(1f, s.MercPeaceShare)));
            return true;
        }

        // ------------------------------------------------------------ raz na dobe (krok korony po ratach reparacji, przed zwrotem)
        internal static void Daily()
        {
            ZeroLast();
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            if (!On) { return; }
            try
            {
                int today = (int)CampaignTime.Now.ToDays;
                float factor = Math.Max(0f, s.MercContractFactor), peace = Math.Max(0f, Math.Min(1f, s.MercPeaceShare));
                int reviewDays = Math.Max(1, s.MercReviewDays), leaveDays = Math.Max(1, s.MercUnpaidLeaveDays);
                float floor = Math.Max(0f, Math.Min(1f, s.MercReviewFloor));
                var byKingdom = new Dictionary<Kingdom, List<KeyValuePair<Clan, long>>>();
                var seen = new HashSet<string>();
                long menNow = 0, menDeal = 0;
                foreach (var c in Clan.All)
                {
                    try
                    {
                        if (!AiMerc(c)) continue;
                        seen.Add(c.StringId);
                        long wage; int men; Company(c, out wage, out men);
                        Deal d;
                        if (!_deals.TryGetValue(c.StringId, out d) || d.Kingdom != c.Kingdom.StringId)
                        {
                            // dzien najmu: kontrakt z dzisiejszego zoldu kompanii (zold + jedzenie + sprzet)
                            d = new Deal { Kingdom = c.Kingdom.StringId, Wage = wage, K = (long)Math.Round(factor * wage), Men = men, Day = today, Review = today };
                            _deals[c.StringId] = d; LastNew++;
                            Log.Info("Kontrakty najemnikow (185): " + c.Name + " w sluzbie " + c.Kingdom.Name + " - kontrakt " + d.K + " zl dziennie w wojnie (" + factor.ToString("0.00", Inv)
                                     + " x zold " + wage + "), ludzi z umowy " + men + "; w pokoju polowa.");
                        }
                        else if (today - d.Review >= reviewDays)
                        {
                            d.Review = today;
                            if (d.Men > 0 && men < floor * d.Men)
                            {
                                // przeglad tylko w dol: kontrakt i ludzie do stanu faktycznego
                                d.K = (long)Math.Round((double)d.K * men / d.Men); d.Wage = (long)Math.Round((double)d.Wage * men / d.Men); d.Men = men; LastCut++;
                            }
                        }
                        bool war = KingdomTreasury.AtWar(c.Kingdom);
                        long due = war ? d.K : (long)(d.K * peace);
                        menNow += men; menDeal += war ? d.Men : (long)(d.Men * peace);
                        if (due <= 0) continue;
                        List<KeyValuePair<Clan, long>> list;
                        if (!byKingdom.TryGetValue(c.Kingdom, out list)) { list = new List<KeyValuePair<Clan, long>>(); byKingdom[c.Kingdom] = list; }
                        list.Add(new KeyValuePair<Clan, long>(c, due));
                        LastClans++;
                    }
                    catch (Exception e) { Stumble("Daily(rod)", e); }
                }
                // umowy rodow, ktore nie sa juz w sluzbie - wygasaja
                var gone = new List<string>();
                foreach (var kv in _deals) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
                foreach (var id in gone) _deals.Remove(id);
                // zaplata: skarbiec -> glowa rodu najemnego, z reszty wplywow dnia, proporcjonalnie
                var leave = new List<Clan>();
                foreach (var kk in byKingdom)
                {
                    var k = kk.Key; var list = kk.Value;
                    try
                    {
                        var due = new int[list.Count];
                        for (int i = 0; i < list.Count; i++) due[i] = (int)Math.Min(int.MaxValue, list[i].Value);
                        long have = CrownIncome.LeftFor(k);
                        var give = KingdomTreasury.Split(due, have);
                        long paidK = 0, dueK = 0;
                        for (int i = 0; i < list.Count; i++)
                        {
                            var c = list[i].Key; var h = c.Leader;
                            dueK += due[i];
                            if (give[i] > 0)
                            {
                                k.KingdomBudgetWallet -= give[i];          // najpierw skarbiec, potem glowa - dostaje dokladnie tyle, ile zeszlo
                                h.ChangeHeroGold(give[i]);
                                CirculationWindows.NoteHeroGold(h, give[i]);   // paczka 169b: glowa poza swiatem - zloto wyszlo ze swiata (tylko licznik)
                                ClanIncomeBook.NoteInflow(h, give[i], ClanIncomeBook.KContract);   // D rodu: czesc "kontrakt"
                                paidK += give[i];
                            }
                            Deal d;
                            if (_deals.TryGetValue(c.StringId, out d))
                            {
                                d.Unpaid = give[i] * 2 < due[i] ? d.Unpaid + 1 : 0;   // niedoplata > 50%
                                if (d.Unpaid >= leaveDays) leave.Add(c);
                            }
                        }
                        CrownIncome.Spent(k, paidK);
                        var cd = CrownIncome.DayOf(k); if (cd != null) cd.Contract += paidK;
                        LastDue += dueK; LastPaid += paidK;
                    }
                    catch (Exception e) { Stumble("Daily(krolestwo)", e); }
                }
                foreach (var c in leave)
                {
                    try
                    {
                        var k = c.Kingdom;
                        _deals.Remove(c.StringId);
                        ChangeKingdomAction.ApplyByLeaveKingdomAsMercenary(c, false);
                        LastLeft++;
                        Log.Info("Kontrakty najemnikow (185): " + c.Name + " odchodzi ze sluzby " + (k != null ? k.Name.ToString() : "?") + " - korona nie placila ponad polowy kontraktu przez " + leaveDays + " dob.");
                    }
                    catch (Exception e) { Stumble("Daily(odejscie)", e); }
                }
                LastTierOff = _dTierOff; LastGameOff = _dGameOff; _dTierOff = 0; _dGameOff = 0;
                long wallet = 0; foreach (var k in Kingdom.All) if (k != null && !k.IsEliminated) wallet += k.MercenaryWallet;
                if (s.LogEnabled)
                    Log.Info("Kontrakty najemnikow (185): dzien " + today + " | kompanii AI w sluzbie " + LastClans + " (nowe umowy " + LastNew + ", przeglad w dol " + LastCut + ", odeszly po niedoplacie " + LastLeft + ")"
                             + " | kontrakty nalezne " + LastDue + ", zaplacone z wplywow dnia " + LastPaid + ", niedoplata " + (LastDue - LastPaid)
                             + " | ludzie najemnikow AI " + menNow + " / z umowy (w pokoju polowa) " + menDeal
                             + " | gra dla AI w sluzbie: \"za tier\" wylaczone " + LastTierOff + " zl, kontrakt gry wylaczony " + LastGameOff + " zl (od wczoraj; AI dostaje 0 z gry)"
                             + " | MercenaryWallet krolestw razem " + wallet + " (zmienia sie tylko o kontrakt gracza)"
                             + (_stumbles > 0 ? " | potkniecia " + _stumbles : "")
                             + (_importN >= 0 ? " | wczytano umow " + _importN + " (bledne " + _importBad + ")" : "") + ".");
                _importN = -1;
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        // ------------------------------------------------------------ gra: AI w sluzbie nie dostaje nic z siebie (w kampanii - pulapka konstruktora statycznego)
        /// <summary>Prefiks DefaultClanFinanceModel.AddMercenaryIncome: AI w sluzbie - bez wplywu i bez zmiany MercenaryWallet (gracz bez zmian).</summary>
        public static bool MercIncomePrefix(Clan __0, bool __2)
        {
            try
            {
                if (__0 == null || __0 == Clan.PlayerClan || !__0.IsUnderMercenaryService || !GameOff) return true;
                if (__2 && __0.Kingdom != null && __0.Leader != null)
                {
                    try { _dGameOff += (long)Math.Ceiling(__0.Influence * (1f / Campaign.Current.Models.ClanFinanceModel.RevenueSmoothenFraction())) * __0.MercenaryAwardMultiplier; } catch { }
                }
                return false;
            }
            catch (Exception e) { Stumble("MercIncomePrefix", e); return true; }
        }

        /// <summary>Czy "za tier" tego rodu jest dzis zdejmowany (ksiega obiegu nie liczy go wtedy jako zrodla z niczego) - ten sam warunek co TierPostfix.</summary>
        internal static bool CancelsTier(Clan c)
        {
            try { return _tierWired && c != null && c != Clan.PlayerClan && !c.IsEliminated && c.IsUnderMercenaryService && GameOff && c.Fiefs != null && c.Fiefs.Count == 0; }
            catch { return false; }
        }
        private static bool _tierWired;

        /// <summary>Postfiks DefaultClanFinanceModel.CalculateClanIncomeInternal: "za tier" AI w sluzbie bez lenn (Tier x (80 + 40)) zdjete tym samym wpisem bez opisu.</summary>
        public static void TierPostfix(Clan __0, ref ExplainedNumber __1, bool __2)
        {
            try
            {
                var c = __0;
                if (c == null || c == Clan.PlayerClan || c.IsEliminated || !c.IsUnderMercenaryService || !GameOff) return;
                if (c.Fiefs == null || c.Fiefs.Count != 0) return;   // ten sam warunek co w grze (rod bez lenn)
                int x = c.Tier * (80 + 40);
                if (x == 0) return;
                __1.Add(-x);
                if (__2) _dTierOff += x;
            }
            catch (Exception e) { Stumble("TierPostfix", e); }
        }

        private static bool _hooksTried;
        private static Harmony _harmony;
        internal static void SetHarmony(Harmony h) { _harmony = h; }

        internal static void EnsureHooks()
        {
            if (_hooksTried || _harmony == null || Campaign.Current == null) return;
            _hooksTried = true;
            string a = "BRAK", b = "BRAK";
            try
            {
                var m = AccessTools.Method(typeof(DefaultClanFinanceModel), "AddMercenaryIncome");
                if (m != null) { _harmony.Patch(m, prefix: new HarmonyMethod(typeof(MercContract), nameof(MercIncomePrefix))); a = "wpiety"; }
            }
            catch (Exception e) { Log.Error("MercContract.EnsureHooks(AddMercenaryIncome)", e); }
            try
            {
                var m = AccessTools.Method(typeof(DefaultClanFinanceModel), "CalculateClanIncomeInternal");
                if (m != null) { _harmony.Patch(m, postfix: new HarmonyMethod(typeof(MercContract), nameof(TierPostfix))); b = "wpiety"; _tierWired = true; }
            }
            catch (Exception e) { Log.Error("MercContract.EnsureHooks(CalculateClanIncomeInternal)", e); }
            Log.Info("Kontrakty najemnikow (185): kontrakt gry dla AI (AddMercenaryIncome) " + a + ", \"za tier\" AI (CalculateClanIncomeInternal) " + b
                     + "; wylaczniki Merc Contract Enabled (czynny z Crown Current Income) i Merc Game Contract Ai Off.");
        }

        // ------------------------------------------------------------ zapis (SaveText, "arm_merc185")
        /// <summary>"v1|idRodu:idKrolestwa:K:zold:ludzie:doba:przeglad:niedoplata;..."</summary>
        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(_deals.Count * 48 + 4);
                sb.Append("v1|");
                bool first = true;
                foreach (var kv in _deals)
                {
                    var d = kv.Value;
                    if (kv.Key.IndexOfAny(Bad) >= 0 || d.Kingdom == null || d.Kingdom.IndexOfAny(Bad) >= 0) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(kv.Key).Append(':').Append(d.Kingdom).Append(':').Append(d.K.ToString(Inv)).Append(':').Append(d.Wage.ToString(Inv)).Append(':').Append(d.Men.ToString(Inv))
                      .Append(':').Append(d.Day.ToString(Inv)).Append(':').Append(d.Review.ToString(Inv)).Append(':').Append(d.Unpaid.ToString(Inv));
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }
        private static readonly char[] Bad = { '|', ';', ':' };

        internal static void Import(string data)
        {
            _deals.Clear(); _importN = 0; _importBad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var f = data.Split('|');
                if (f.Length < 2 || f[0] != "v1") { _importBad++; return; }
                if (f[1].Length == 0) return;
                foreach (var p in f[1].Split(';'))
                {
                    var x = p.Split(':'); long k, w; int men, day, rev, un;
                    if (x.Length == 8 && x[0].Length > 0 && x[1].Length > 0 && long.TryParse(x[2], NumberStyles.Integer, Inv, out k) && long.TryParse(x[3], NumberStyles.Integer, Inv, out w)
                        && int.TryParse(x[4], NumberStyles.Integer, Inv, out men) && int.TryParse(x[5], NumberStyles.Integer, Inv, out day)
                        && int.TryParse(x[6], NumberStyles.Integer, Inv, out rev) && int.TryParse(x[7], NumberStyles.Integer, Inv, out un))
                    { _deals[x[0]] = new Deal { Kingdom = x[1], K = k, Wage = w, Men = men, Day = day, Review = rev, Unpaid = un }; _importN++; }
                    else _importBad++;
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
