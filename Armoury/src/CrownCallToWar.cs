using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;

namespace Armoury
{
    /// <summary>
    /// 168 DODATEK - WEZWANIE SOJUSZNIKA DO WOJNY PLACI KORONA (decyzja Jeffa 10.10 "tak, korona"; zamyka czesc zrodla zaliczki gry OBIEG-1).
    /// Gra (AllianceCampaignBehavior.StartCallToWarAgreement): przy porozumieniu portfel wezwania krolestwa wzywajacego idzie w minus o cene (dlug jego rodow),
    /// a portfel wezwanego w plus o te sama cene od razu - rody wezwanego dostaja ja z niczego (AddIncomeFromCallToWarAgrements), a rody wzywajacego splacaja
    /// swoj udzial w nicosc (AddExpensesForCallToWarAgreements, przy braku - DebtToKingdom i portfel uznany w calosci). Cena = dzienna stawka x 42 doby sluzby.
    /// Przy CrownPaysCallToWar (i 165):
    ///  - rody wzywajacego nie placa nic (prefiks AddExpensesForCallToWarAgreements - AI i gracz);
    ///  - wezwany nie dostaje ceny z gory (postfiks StartCallToWarAgreement cofa plus portfela wezwanego); portfel wzywajacego zostaje dlugiem korony;
    ///  - codziennie w kolejnosci 165 (po kontraktach 185, przed zwrotem) skarbiec wzywajacego placi z reszty wplywow dnia czesc dnia = cena / 42 (do konca ceny):
    ///    skarbiec -> portfel wezwanego (gra rozdziela go na jego rody jak dotad) - prawdziwy platnik;
    ///  - gdy wplywow dnia nie starcza na czesc dnia - porozumienie konczy sie (EndCallToWarAgreement gry) i sojusznik wychodzi z tej wojny (MakePeaceAction,
    ///    gdy nie trzyma go w niej inne wezwanie); reszta ceny skreslona (nikt jej nie dostal);
    ///  - porozumienie skonczone przez gre (termin, pokoj) - reszta ceny skreslona.
    /// Wylaczone: porozumienia w toku wracaja do gry (portfel wezwanego dostaje niezaplacona reszte jak przy starcie gry). Zapis: "arm_ctw168".
    /// </summary>
    internal static class CrownCallToWar
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Agr { public string Calling = "", Called = "", Against = ""; public long Total, Daily, Left; public int Day; }
        private static readonly List<Agr> _agr = new List<Agr>();

        internal static long LastPaid, LastEnded, LastUnpaid; internal static int LastEndedN;
        private static long _dPaid, _dEnded, _dUnpaid, _dCancelled, _dLegacy, _dUndone; private static int _dEndedN, _dPaidN, _dPeaceN, _dNewN;
        private static int _stumbles, _importN = -1, _importBad;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _ending;   // nasze EndCallToWarAgreement - postfiks nie liczy drugi raz

        internal static bool On { get { var s = Settings.Current; return s != null && s.CrownPaysCallToWar && CrownIncome.On; } }

        internal static void Reset() { _agr.Clear(); ZeroDay(); LastPaid = LastEnded = LastUnpaid = 0; LastEndedN = 0; _stumbles = 0; _err.Clear(); _importN = -1; _importBad = 0; _ending = false; }
        private static void ZeroDay() { _dPaid = _dEnded = _dUnpaid = _dCancelled = _dLegacy = _dUndone = 0; _dEndedN = _dPaidN = _dPeaceN = _dNewN = 0; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("CrownCallToWar." + where, e); } catch { }
        }

        private static Kingdom KingdomById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k;
            return null;
        }

        private static float Days() { try { return Math.Max(1f, (float)Campaign.Current.Models.AllianceModel.MaxDurationOfWarParticipation.ToDays); } catch { return 42f; } }

        // ------------------------------------------------------------ latki gry (w kampanii - pulapka konstruktora statycznego modelu finansow)
        private static Harmony _harmony;
        private static bool _hooksTried;
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        internal static void SetHarmony(Harmony h) { _harmony = h; }

        internal static void EnsureHooks()
        {
            if (_hooksTried || _harmony == null || Campaign.Current == null) return;
            _hooksTried = true;
            var fin = AccessTools.Method(typeof(DefaultClanFinanceModel), "AddExpensesForCallToWarAgreements", new[] { typeof(Clan), typeof(TaleWorlds.CampaignSystem.ExplainedNumber).MakeByRefType(), typeof(bool) });
            Wire("rody nie placa wezwania (AddExpensesForCallToWarAgreements)", fin, nameof(ClanSharePrefix), null);
            Wire("porozumienie bez ceny z gory (StartCallToWarAgreement)", AccessTools.Method(typeof(AllianceCampaignBehavior), "StartCallToWarAgreement",
                 new[] { typeof(Kingdom), typeof(Kingdom), typeof(Kingdom), typeof(int), typeof(bool) }), nameof(StartPrefix), nameof(StartPostfix));
            Wire("koniec porozumienia (EndCallToWarAgreement)", AccessTools.Method(typeof(AllianceCampaignBehavior), "EndCallToWarAgreement",
                 new[] { typeof(Kingdom), typeof(Kingdom), typeof(Kingdom) }), null, nameof(EndPostfix));
            Log.Info("Wezwania do wojny (168): latki - " + (_wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "-") + "; BRAK: " + (_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-") + ".");
        }

        private static void Wire(string label, MethodBase m, string pre, string post)
        {
            try
            {
                if (m == null) { _missing.Add(label); return; }
                _harmony.Patch(m, prefix: pre != null ? new HarmonyMethod(typeof(CrownCallToWar), pre) { priority = Priority.First } : null,
                                  postfix: post != null ? new HarmonyMethod(typeof(CrownCallToWar), post) { priority = Priority.Last } : null);
                _wired.Add(label);
            }
            catch (Exception e) { _missing.Add(label + " (blad: " + e.Message + ")"); }
        }

        /// <summary>Rody krolestwa wzywajacego nie placa ceny wezwania - placi skarbiec (Daily).</summary>
        public static bool ClanSharePrefix() { try { return !On; } catch { return true; } }

        public struct StartState { public int Called, Calling; }

        public static void StartPrefix(Kingdom __0, Kingdom __1, out StartState __state)
        {
            __state = new StartState { Called = int.MinValue };
            try { if (On && __0 != null && __1 != null) __state = new StartState { Called = __1.CallToWarWallet, Calling = __0.CallToWarWallet }; } catch { }
        }

        public static void StartPostfix(Kingdom __0, Kingdom __1, Kingdom __2, int __3, bool __4, StartState __state)
        {
            try
            {
                if (__state.Called == int.MinValue || __4 || __0 == null || __1 == null || __2 == null || __3 <= 0 || !On) return;   // gracz placacy z kiesy - prawdziwy platnik, jak w grze
                if (__1.CallToWarWallet - __state.Called != __3 || __state.Calling - __0.CallToWarWallet != __3) return;          // porozumienie nie powstalo
                __1.CallToWarWallet -= __3;   // wezwany nie dostaje ceny z niczego z gory - dostaje to, co skarbiec wzywajacego naprawde zaplaci
                long daily = (long)Math.Ceiling(__3 / Days());
                _agr.Add(new Agr { Calling = __0.StringId, Called = __1.StringId, Against = __2.StringId, Total = __3, Daily = Math.Max(1, daily), Left = __3, Day = (int)CampaignTime.Now.ToDays });
                _dNewN++;
                Log.Info("Wezwania do wojny (168): " + __0.Name + " wzywa " + __1.Name + " przeciw " + __2.Name + " - cena " + __3 + " zl placi skarbiec " + __0.Name
                         + " z wplywow dnia po " + Math.Max(1, daily) + " zl przez " + Days().ToString("0", Inv) + " dob; rody nie placa.");
            }
            catch (Exception e) { Stumble("StartPostfix", e); }
        }

        /// <summary>Porozumienie skonczone (gra: termin, pokoj; albo my - brak wplywow): reszta ceny skreslona z portfela wzywajacego.</summary>
        public static void EndPostfix(Kingdom __0, Kingdom __1, Kingdom __2)
        {
            try
            {
                if (__0 == null || __1 == null || __2 == null) return;
                for (int i = _agr.Count - 1; i >= 0; i--)
                {
                    var a = _agr[i];
                    if (a.Calling != __0.StringId || a.Called != __1.StringId || a.Against != __2.StringId) continue;
                    if (a.Left > 0) { __0.CallToWarWallet = (int)Math.Min(0L, (long)__0.CallToWarWallet + a.Left); if (!_ending) _dCancelled += a.Left; }
                    _agr.RemoveAt(i);
                    break;
                }
            }
            catch (Exception e) { Stumble("EndPostfix", e); }
        }

        // ------------------------------------------------------------ raz na dobe (krok korony 165: po kontraktach 185, przed zwrotem)
        internal static void Daily()
        {
            ZeroDay();
            if (Campaign.Current == null) return;
            if (!On)
            {
                // wylaczone: porozumienia w toku wracaja do gry - wezwany dostaje niezaplacona reszte (jak przy starcie gry), rody wzywajacego splacaja portfel
                foreach (var a in _agr) { try { var called = KingdomById(a.Called); if (called != null && a.Left > 0) { called.CallToWarWallet += (int)Math.Min(int.MaxValue, a.Left); _dUndone += a.Left; } } catch { } }
                _agr.Clear();
                return;
            }
            try
            {
                // portfel wzywajacego ponizej dlugu z naszych porozumien = dlug rodow sprzed paczki (wezwany dostal go juz z niczego) - skreslony, nikt go nie placi
                var owed = new Dictionary<string, long>();
                foreach (var a in _agr) { long v; owed.TryGetValue(a.Calling, out v); owed[a.Calling] = v + a.Left; }
                foreach (var k in Kingdom.All)
                {
                    if (k == null || k.IsEliminated || k.StringId == null) continue;
                    long o; owed.TryGetValue(k.StringId, out o);
                    if (k.CallToWarWallet < -o) { long x = -o - k.CallToWarWallet; k.CallToWarWallet += (int)x; _dLegacy += x; }
                }
                var beh = Campaign.Current.GetCampaignBehavior<AllianceCampaignBehavior>();
                foreach (var a in _agr.ToArray())
                {
                    try
                    {
                        var calling = KingdomById(a.Calling); var called = KingdomById(a.Called); var against = KingdomById(a.Against);
                        if (calling == null || called == null || against == null || calling.IsEliminated || called.IsEliminated || against.IsEliminated)
                        {
                            if (calling != null && a.Left > 0) { calling.CallToWarWallet = (int)Math.Min(0L, (long)calling.CallToWarWallet + a.Left); _dCancelled += a.Left; }
                            _agr.Remove(a);
                            continue;
                        }
                        long due = Math.Min(a.Left, a.Daily);
                        if (due <= 0) { _agr.Remove(a); continue; }
                        var kd = CrownIncome.DayOf(calling);
                        if (kd == null) continue;
                        long avail = CrownIncome.LeftFor(calling);
                        if (avail >= due)
                        {
                            Pay(calling, called, a, due, kd);
                            if (a.Left <= 0) _agr.Remove(a);
                            continue;
                        }
                        // skarbca nie stac na czesc dnia - porozumienie konczy sie, sojusznik wychodzi z wojny
                        if (beh != null)
                        {
                            long left = a.Left;
                            _ending = true;
                            try { beh.EndCallToWarAgreement(calling, called, against); }
                            finally { _ending = false; }
                            _agr.Remove(a);   // na wypadek, gdyby postfiks nie znalazl wpisu
                            _dEnded += left; _dEndedN++;
                            bool peace = false;
                            Kingdom other;
                            if (called.IsAtWarWith(against) && !beh.IsAtWarByCallToWarAgreement(called, against, out other))
                            {
                                try { MakePeaceAction.Apply(called, against); peace = true; _dPeaceN++; } catch (Exception e) { Stumble("MakePeace", e); }
                            }
                            Log.Info("Wezwania do wojny (168): skarbiec " + calling.Name + " nie ma z wplywow dnia " + due + " zl (zostalo " + avail + ") - porozumienie z " + called.Name
                                     + " przeciw " + against.Name + " zerwane, reszta ceny " + left + " skreslona" + (peace ? ", " + called.Name + " wychodzi z wojny (pokoj)" : ", wojna trwa z innych powodow") + ".");
                            if (Clan.PlayerClan != null && (Clan.PlayerClan.Kingdom == calling || Clan.PlayerClan.Kingdom == called))
                                Log.Player(calling.Name + " can no longer pay " + called.Name + " for its war against " + against.Name + ". The agreement is ended" + (peace ? " and " + called.Name + " makes peace." : "."), true);
                        }
                        else
                        {
                            // bez zachowania gry - zaplata proporcjonalna, niedoplata w liczniku (bez zlota z niczego, bez dlugu rodow)
                            if (avail > 0) Pay(calling, called, a, avail, kd);
                            _dUnpaid += due - Math.Max(0, avail);
                            if (a.Left <= 0) _agr.Remove(a);
                        }
                    }
                    catch (Exception e) { Stumble("Daily(porozumienie)", e); }
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally
            {
                LastPaid = _dPaid; LastEnded = _dEnded; LastEndedN = _dEndedN; LastUnpaid = _dUnpaid;
                try { Line(); } catch (Exception e) { Stumble("Line", e); }
            }
        }

        private static void Pay(Kingdom calling, Kingdom called, Agr a, long x, CrownIncome.KDay kd)
        {
            int ix = (int)Math.Min(int.MaxValue, Math.Min(x, (long)Math.Max(0, calling.KingdomBudgetWallet)));
            if (ix <= 0) return;
            calling.KingdomBudgetWallet -= ix;    // skarbiec wzywajacego
            calling.CallToWarWallet += ix;        // dlug korony za wezwanie maleje
            called.CallToWarWallet += ix;         // portfel wezwanego - gra rozdziela go na jego rody (AddIncomeFromCallToWarAgrements)
            a.Left -= ix;
            CrownIncome.Spent(calling, ix);
            kd.CallToWar += ix;
            _dPaid += ix; _dPaidN++;
        }

        private static void Line()
        {
            var s = Settings.Current;
            if (s == null || !s.LogEnabled || !On) return;
            if (_agr.Count == 0 && _dPaid + _dEnded + _dCancelled + _dLegacy + _dUndone == 0 && _dNewN == 0 && _importN < 0) return;
            long left = 0; foreach (var a in _agr) left += a.Left;
            var sb = new StringBuilder(400);
            sb.Append("Wezwania do wojny (168): dzien ").Append((int)CampaignTime.Now.ToDays)
              .Append(" | porozumien w toku ").Append(_agr.Count).Append(" (nowe dzis ").Append(_dNewN).Append("), reszta cen do zaplaty przez korony ").Append(left)
              .Append(" | zaplacone dzis ze skarbcow wzywajacych do portfeli wezwanych ").Append(_dPaid).Append(" (").Append(_dPaidN).Append(" czesci dnia)")
              .Append(" | zerwane z braku wplywow dnia ").Append(_dEndedN).Append(" (reszta ceny skreslona ").Append(_dEnded).Append(", pokoj sojusznika ").Append(_dPeaceN).Append(")")
              .Append(", niedoplata (bez zachowania gry) ").Append(_dUnpaid)
              .Append(" | skreslone: koniec porozumienia przez gre ").Append(_dCancelled).Append(", dlug portfela sprzed paczki ").Append(_dLegacy)
              .Append(" | rody nic nie placa (latki: ").Append(_wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "-").Append("; BRAK: ").Append(_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-").Append(')')
              .Append(_stumbles > 0 ? " | potkniecia " + _stumbles : "").Append('.');
            if (_importN >= 0) { sb.Append(" Wczytano z zapisu: porozumien ").Append(_importN).Append(" (bledne ").Append(_importBad).Append(")."); _importN = -1; }
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ zapis (SaveText, "arm_ctw168")
        private static readonly char[] Bad = { '|', ';', ',' };

        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(8 + _agr.Count * 64);
                sb.Append("v1|");
                bool first = true;
                foreach (var a in _agr)
                {
                    if (a.Calling.IndexOfAny(Bad) >= 0 || a.Called.IndexOfAny(Bad) >= 0 || a.Against.IndexOfAny(Bad) >= 0 || a.Left <= 0) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(a.Calling).Append(',').Append(a.Called).Append(',').Append(a.Against).Append(',').Append(a.Total.ToString(Inv)).Append(',').Append(a.Daily.ToString(Inv))
                      .Append(',').Append(a.Left.ToString(Inv)).Append(',').Append(a.Day.ToString(Inv));
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            _agr.Clear(); _importN = 0; _importBad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var f = data.Split('|');
                if (f.Length < 2 || f[0] != "v1") { _importBad++; return; }
                if (f[1].Length == 0) return;
                foreach (var rec in f[1].Split(';'))
                {
                    var x = rec.Split(','); long t, d, l; int day;
                    if (x.Length == 7 && long.TryParse(x[3], NumberStyles.Integer, Inv, out t) && long.TryParse(x[4], NumberStyles.Integer, Inv, out d) && long.TryParse(x[5], NumberStyles.Integer, Inv, out l)
                        && int.TryParse(x[6], NumberStyles.Integer, Inv, out day) && l > 0)
                    { _agr.Add(new Agr { Calling = x[0], Called = x[1], Against = x[2], Total = t, Daily = Math.Max(1, d), Left = l, Day = day }); _importN++; }
                    else _importBad++;
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
