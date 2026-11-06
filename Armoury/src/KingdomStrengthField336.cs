using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 336 (Jeff 06.11: "sile krolestw pokazuj bez garnizonow - Polnoc nie ma 44k zolnierzy w polu; garnizony, jak zostana wyjete z garnizonow do armii,
    /// to wtedy sa liczone. Liczy sie armia w polu."). Klucz KingdomStrengthFieldOnly336.
    ///
    /// Przyczyna (dekompilacja TaleWorlds.CampaignSystem): Clan.UpdateCurrentStrength (co godzine, KingdomManager.HourlyTickClan) = suma EstimatedStrength
    /// partii wojennych rodu (_warPartyComponentsCache) PLUS EstimatedStrength garnizonow jego twierdz; Kingdom.CurrentTotalStrength = suma po rodach.
    /// Log Jeffa 06.11 (55 dob): The North 42.8-47.4 tys. pokazane, z czego 19.8-21.0 tys. to garnizony (partie lordow 25.4 tys.).
    ///
    /// Zmiana TYLKO W UI: transpiler na metodach ViewModeli, ktore pokazuja sile - kazde wywolanie get_CurrentTotalStrength (IFaction / Kingdom / Clan)
    /// zamienione na FieldOf(...) tego samego typu (suma EstimatedStrength partii wojennych rodow bez garnizonow i milicji, bez partii rozwiazywanych
    /// i nieaktywnych - jak strona krolestwa w encyklopedii). Podpis paska ("Total Strength" / "Strength" / "Clan Strength") dostaje dopisek " (in field)".
    /// Wlasnosci, ktore czyta AI (Clan/Kingdom.CurrentTotalStrength, UpdateCurrentStrength, modele dyplomacji, SCA, Diplomacy, BK) - BEZ ZMIAN.
    /// Miejsca: Krolestwo / Dyplomacja (wojna, rozejm), Krolestwo / Decyzje (wojna, pokoj, sojusz, umowa handlowa, wezwanie do wojny x2, wybor krola),
    /// encyklopedia (lista krolestw i rodow - wartosc i sortowanie, strona rodu), podpowiedz rodu na mapie, Diplomacy: okno szczegolow wojny (postfiks
    /// DetailWarVM.RefreshValues - konstruktor DetailWarStatsVM jest maly i moglby byc wkompilowany w wywolujacego).
    /// Klucz na zywo: wylaczony = FieldOf zwraca stara wartosc, podpis bez dopisku. Potkniecia liczone, bez globalnego wylacznika.
    /// Linie: "Sila krolestw w polu (336): start sesji ..." (metody i podmiany) i co dobe "Sila krolestw w polu (336): dzien N ..." (pole vs stara).
    /// </summary>
    internal static class KingdomStrengthField336
    {
        private const string HarmonyId = "com.jeff.armoury.strengthfield336";
        internal const string P = "Sila krolestw w polu (336): ";
        internal const string Suffix = " (in field)";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static bool _hookTried;
        private static string _hookNote = "-";
        private static int _methodsOk, _methodsWant, _stumbles, _dStumbles, _dCalls;
        private static readonly List<string> _missing = new List<string>();
        // metoda -> (podmian odczytu sily, dopiskow podpisu) z ostatniego biegu transpilera (Harmony powtarza transpilery przy kazdej nowej latce na celu)
        private static readonly Dictionary<string, int[]> _perMethod = new Dictionary<string, int[]>(StringComparer.Ordinal);
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static PropertyInfo _pDwStats, _pDwOpp, _pDsTotal, _pDsLabel;
        private static FieldInfo _fDwOpp;
        private static bool _dipWired;

        private static readonly MethodInfo MFaction = AccessTools.Method(typeof(KingdomStrengthField336), nameof(FieldOfFaction));
        private static readonly MethodInfo MKingdom = AccessTools.Method(typeof(KingdomStrengthField336), nameof(FieldOfKingdom));
        private static readonly MethodInfo MClan = AccessTools.Method(typeof(KingdomStrengthField336), nameof(FieldOfClan));
        private static readonly MethodInfo MLabel = AccessTools.Method(typeof(KingdomStrengthField336), nameof(Label));

        internal static bool On { get { var s = Settings.Current; return s != null && s.KingdomStrengthFieldOnly336; } }

        internal static void Reset() { _dStumbles = 0; _dCalls = 0; _errSites.Clear(); }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_errSites.Add(where)) Log.Error("KingdomStrengthField336." + where + " (potkniecie " + _stumbles + ")", e); } catch { }
        }

        // ------------------------------------------------------------ sila w polu (ten sam model sily co gra: PartyBase.EstimatedStrength)
        /// <summary>Sila partii wojennych rodu bez garnizonow (garnizon nie jest partia wojenna, milicja nie jest partia) - bez partii pustych,
        /// nieaktywnych i rozwiazywanych.</summary>
        internal static float FieldRaw(Clan c)
        {
            if (c == null) return 0f;
            float sum = 0f;
            var list = c.WarPartyComponents;
            if (list == null) return 0f;
            for (int i = 0; i < list.Count; i++)
            {
                var mp = list[i] != null ? list[i].MobileParty : null;
                if (mp == null || !mp.IsActive || mp.IsDisbanding || mp.Party == null) continue;
                sum += mp.Party.EstimatedStrength;
            }
            return sum;
        }

        internal static float FieldRaw(Kingdom k)
        {
            if (k == null) return 0f;
            float sum = 0f;
            var cl = k.Clans;
            for (int i = 0; i < cl.Count; i++) sum += FieldRaw(cl[i]);
            return sum;
        }

        internal static float GarrisonRaw(Kingdom k)
        {
            float sum = 0f;
            foreach (var c in k.Clans)
                foreach (Town t in c.Fiefs)
                    if (t != null && t.GarrisonParty != null && t.GarrisonParty.Party != null) sum += t.GarrisonParty.Party.EstimatedStrength;
            return sum;
        }

        // wolane z podmienionych metod UI (sygnatura = obiekt na stosie -> float, jak getter)
        public static float FieldOfClan(Clan c)
        {
            if (c == null) throw new NullReferenceException();   // jak oryginalny callvirt na null
            if (!On) return c.CurrentTotalStrength;
            try { _dCalls++; return FieldRaw(c); }
            catch (Exception e) { Stumble("FieldOfClan", e); return c.CurrentTotalStrength; }
        }

        public static float FieldOfKingdom(Kingdom k)
        {
            if (k == null) throw new NullReferenceException();
            if (!On) return k.CurrentTotalStrength;
            try { _dCalls++; return FieldRaw(k); }
            catch (Exception e) { Stumble("FieldOfKingdom", e); return k.CurrentTotalStrength; }
        }

        public static float FieldOfFaction(IFaction f)
        {
            var k = f as Kingdom;
            if (k != null) return FieldOfKingdom(k);
            var c = f as Clan;
            if (c != null) return FieldOfClan(c);
            return f.CurrentTotalStrength;
        }

        /// <summary>Podpis paska / wiersza: przy kluczu nowy TextObject z dopiskiem (oryginal z GameTexts jest wspolny - nie zmieniamy go).</summary>
        public static TextObject Label(TextObject t)
        {
            if (t == null || !On) return t;
            try { return new TextObject("{=!}" + t.ToString() + Suffix); }
            catch (Exception e) { Stumble("Label", e); return t; }
        }

        // ------------------------------------------------------------ transpiler
        private static bool IsStrengthGetter(MethodInfo mi)
        {
            return mi != null && mi.Name == "get_CurrentTotalStrength"
                   && (mi.DeclaringType == typeof(IFaction) || mi.DeclaringType == typeof(Kingdom) || mi.DeclaringType == typeof(Clan));
        }

        private static bool HasStrengthCall(MethodBase m)
        {
            try
            {
                if (m.IsAbstract || m.GetMethodBody() == null) return false;
                foreach (var ci in PatchProcessor.GetOriginalInstructions(m))
                    if ((ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && IsStrengthGetter(ci.operand as MethodInfo)) return true;
            }
            catch (Exception e) { Stumble("HasStrengthCall." + m.Name, e); }
            return false;
        }

        private static bool LabelId(string s)
        {
            return s == "str_total_strength" || s == "str_strength" || (s != null && s.StartsWith("{=ODEnkg0o}", StringComparison.Ordinal));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var list = instructions.ToList();
            int swaps = 0, labels = 0;
            try
            {
                var outp = new List<CodeInstruction>(list.Count + 4);
                for (int i = 0; i < list.Count; i++)
                {
                    var ci = list[i];
                    var mi = ci.operand as MethodInfo;
                    if ((ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && IsStrengthGetter(mi))
                    {
                        ci.opcode = OpCodes.Call;
                        ci.operand = mi.DeclaringType == typeof(Kingdom) ? MKingdom : mi.DeclaringType == typeof(Clan) ? MClan : MFaction;
                        swaps++;
                        outp.Add(ci);
                        continue;
                    }
                    outp.Add(ci);
                    // podpis: GameTexts.FindText("str_total_strength" / "str_strength", null) albo new TextObject("{=ODEnkg0o}Clan Strength", null)
                    bool find = (ci.opcode == OpCodes.Call && mi != null && mi.Name == "FindText" && mi.DeclaringType != null && mi.DeclaringType.Name == "GameTexts")
                                || (ci.opcode == OpCodes.Newobj && ci.operand is ConstructorInfo ctor && ctor.DeclaringType == typeof(TextObject));
                    if (!find) continue;
                    for (int j = i - 1; j >= 0 && j >= i - 3; j--)
                    {
                        if (list[j].opcode == OpCodes.Ldstr)
                        {
                            if (LabelId(list[j].operand as string)) { outp.Add(new CodeInstruction(OpCodes.Call, MLabel)); labels++; }
                            break;
                        }
                    }
                }
                lock (_perMethod) _perMethod[Name(original)] = new[] { swaps, labels };
                return outp;
            }
            catch (Exception e)
            {
                Stumble("Transpiler." + (original != null ? original.Name : "?"), e);
                return list;
            }
        }

        private static string Name(MethodBase m)
        {
            if (m == null) return "?";
            var t = m.DeclaringType;
            string tn = t == null ? "?" : (t.DeclaringType != null ? t.DeclaringType.Name + "+" : "") + t.Name;
            return tn + "." + m.Name;
        }

        // ------------------------------------------------------------ Diplomacy: okno szczegolow wojny
        public static void DetailWarPostfix(object __instance)
        {
            if (!On || __instance == null) return;
            try
            {
                var own = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
                var opp = _fDwOpp != null ? _fDwOpp.GetValue(__instance) as Kingdom : null;
                Fix(_pDwStats.GetValue(__instance, null), own);
                Fix(_pDwOpp.GetValue(__instance, null), opp);
            }
            catch (Exception e) { Stumble("DetailWarPostfix", e); }
        }

        private static void Fix(object stats, Kingdom k)
        {
            if (stats == null || k == null) return;
            _dCalls++;
            _pDsTotal.SetValue(stats, Convert.ToInt32(Math.Round(FieldRaw(k))), null);
            if (_pDsLabel != null)
            {
                var s = _pDsLabel.GetValue(stats, null) as string;
                if (s != null && !s.EndsWith(Suffix, StringComparison.Ordinal)) _pDsLabel.SetValue(stats, s + Suffix, null);
            }
        }

        // ------------------------------------------------------------ latki (raz na proces, w kampanii)
        private static int PatchAll(Harmony h, Type t, string label, int want, bool nested)
        {
            _methodsWant += want;
            if (t == null) { _missing.Add(label + " (brak typu)"); return 0; }
            var cands = new List<MethodBase>();
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var types = new List<Type> { t };
            if (nested) types.AddRange(t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic));
            foreach (var tt in types)
            {
                cands.AddRange(tt.GetMethods(F).Where(m => !m.IsGenericMethodDefinition));
                cands.AddRange(tt.GetConstructors(F));
            }
            int n = 0;
            foreach (var m in cands)
            {
                if (!HasStrengthCall(m)) continue;
                try
                {
                    h.Patch(m, transpiler: new HarmonyMethod(typeof(KingdomStrengthField336), nameof(Transpiler)));
                    bool ok = false;
                    var info = Harmony.GetPatchInfo(m);
                    if (info != null) foreach (var p in info.Transpilers) if (p != null && p.owner == HarmonyId) ok = true;
                    if (ok) n++; else _missing.Add(Name(m) + " (latka nie weszla)");
                }
                catch (Exception e) { _missing.Add(Name(m) + " (" + e.GetType().Name + ")"); Stumble("Patch." + m.Name, e); }
            }
            if (n < want) _missing.Add(label + " " + n + "/" + want);
            _methodsOk += Math.Min(n, want);
            return n;
        }

        internal static void EnsureHooks()
        {
            if (!_hookTried)
            {
                _hookTried = true;
                try
                {
                    var h = new Harmony(HarmonyId);
                    const string Vm = "TaleWorlds.CampaignSystem.ViewModelCollection.";
                    // Krolestwo / Dyplomacja
                    PatchAll(h, AccessTools.TypeByName(Vm + "KingdomManagement.Diplomacy.KingdomWarItemVM"), "KingdomWarItemVM", 1, false);
                    PatchAll(h, AccessTools.TypeByName(Vm + "KingdomManagement.Diplomacy.KingdomTruceItemVM"), "KingdomTruceItemVM", 1, false);
                    // Krolestwo / Decyzje
                    foreach (var n in new[] { "DeclareWarDecisionItemVM", "MakePeaceDecisionItemVM", "StartAllianceDecisionItemVM", "TradeAgreementDecisionItemVM",
                                              "AcceptingCallToWarAgreementDecisionItemVM", "ProposeCallToWarAgreementDecisionItemVM", "KingSelectionDecisionItemVM" })
                        PatchAll(h, AccessTools.TypeByName(Vm + "KingdomManagement.Decisions.ItemTypes." + n), n, 1, false);
                    // Encyklopedia: listy (wartosc i sortowanie - lambda w zagniezdzonej <>c) i strona rodu
                    var fp = AccessTools.TypeByName("TaleWorlds.CampaignSystem.Encyclopedia.Pages.DefaultEncyclopediaFactionPage");
                    var cp = AccessTools.TypeByName("TaleWorlds.CampaignSystem.Encyclopedia.Pages.DefaultEncyclopediaClanPage");
                    PatchAll(h, fp != null ? AccessTools.Inner(fp, "EncyclopediaListKingdomTotalStrengthComparer") : null, "EncyclopediaListKingdomTotalStrengthComparer", 2, true);
                    PatchAll(h, cp != null ? AccessTools.Inner(cp, "EncyclopediaListClanStrengthComparer") : null, "EncyclopediaListClanStrengthComparer", 2, true);
                    PatchAll(h, AccessTools.TypeByName(Vm + "Encyclopedia.Pages.EncyclopediaClanPageVM"), "EncyclopediaClanPageVM", 1, false);
                    // Mapa: podpowiedz rodu
                    PatchAll(h, AccessTools.TypeByName(Vm + "TooltipRefresherCollection"), "TooltipRefresherCollection", 1, false);
                    // Diplomacy: okno szczegolow wojny
                    var dw = AccessTools.TypeByName("Diplomacy.ViewModel.DetailWarVM");
                    var ds = AccessTools.TypeByName("Diplomacy.ViewModel.DetailWarStatsVM");
                    string dip;
                    if (dw == null || ds == null) dip = "bez Diplomacy";
                    else
                    {
                        _methodsWant++;
                        var m = AccessTools.DeclaredMethod(dw, "RefreshValues", Type.EmptyTypes);
                        _pDwStats = AccessTools.Property(dw, "Stats"); _pDwOpp = AccessTools.Property(dw, "OpponentStats"); _fDwOpp = AccessTools.Field(dw, "_opposingKingdom");
                        _pDsTotal = AccessTools.Property(ds, "TotalStrength"); _pDsLabel = AccessTools.Property(ds, "TotalStrengthLabel");
                        if (m == null || _pDwStats == null || _pDwOpp == null || _fDwOpp == null || _pDsTotal == null || _pDsTotal.GetSetMethod(true) == null)
                        { dip = "BRAK (DetailWarVM.RefreshValues / pola)"; _missing.Add("DetailWarVM"); }
                        else
                        {
                            h.Patch(m, postfix: new HarmonyMethod(typeof(KingdomStrengthField336), nameof(DetailWarPostfix)));
                            var info = Harmony.GetPatchInfo(m);
                            if (info != null) foreach (var p in info.Postfixes) if (p != null && p.owner == HarmonyId) _dipWired = true;
                            if (_dipWired) _methodsOk++; else _missing.Add("DetailWarVM.RefreshValues (latka nie weszla)");
                            dip = _dipWired ? "wpieta" : "BRAK";
                        }
                    }
                    _hookNote = "Diplomacy DetailWarVM " + dip;
                }
                catch (Exception e) { _hookNote = "BRAK (" + e.GetType().Name + ": " + e.Message + ")"; Stumble("EnsureHooks", e); }
            }
            try
            {
                var sb = new StringBuilder(800);
                int swaps = 0, labels = 0;
                lock (_perMethod) foreach (var kv in _perMethod) { swaps += kv.Value[0]; labels += kv.Value[1]; }
                sb.Append(P).Append("start sesji | klucz Kingdom Strength In Field Only ").Append(On ? "TAK" : "NIE")
                  .Append(" | latki metod ").Append(_methodsOk).Append("/").Append(_methodsWant)
                  .Append(", podmian odczytu sily ").Append(swaps).Append(", podpisow ").Append(labels)
                  .Append(", ").Append(_hookNote)
                  .Append(" | brak: ").Append(_missing.Count == 0 ? "-" : string.Join("; ", _missing))
                  .Append(" | metody: ");
                lock (_perMethod) sb.Append(string.Join(", ", _perMethod.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + " " + kv.Value[0] + "/" + kv.Value[1])));
                sb.Append(" | AI bez zmian (Clan/Kingdom.CurrentTotalStrength nietkniete).");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("StartLine", e); }
        }

        // ------------------------------------------------------------ linia doby
        internal static void Daily()
        {
            if (Campaign.Current == null) return;
            var s = Settings.Current;
            if (s == null || !s.LogEnabled) { _dCalls = 0; _dStumbles = 0; return; }
            try
            {
                var rows = new List<string>();
                int nk = 0, below = 0, notBelow = 0;
                foreach (var k in Kingdom.All.Where(x => x != null && !x.IsEliminated && x.Clans.Count > 0).OrderByDescending(x => FieldRaw(x)))
                {
                    float field = FieldRaw(k), old = k.CurrentTotalStrength, gar = GarrisonRaw(k);
                    nk++;
                    if (field < old) below++;
                    else if (gar > 0f) notBelow++;
                    rows.Add(k.Name + " pole " + ((int)Math.Round(field)).ToString(Inv) + " / stara " + ((int)Math.Round(old)).ToString(Inv)
                             + " (garnizony " + ((int)Math.Round(gar)).ToString(Inv) + ", reszta " + ((int)Math.Round(old - field - gar)).ToString(Inv) + ")");
                }
                Log.Info(P + "dzien " + (int)CampaignTime.Now.ToDays + " | klucz " + (On ? "TAK" : "NIE") + ", latki metod " + _methodsOk + "/" + _methodsWant
                         + " | krolestw " + nk + ", w polu < stara " + below + ", nie mniej przy garnizonach " + notBelow
                         + " | " + string.Join(" | ", rows)
                         + " | odczytow UI doby " + _dCalls + " | potkniecia 336 " + _dStumbles + " (sesja " + _stumbles + ").");
            }
            catch (Exception e) { Stumble("Daily", e); }
            _dCalls = 0; _dStumbles = 0;
        }
    }
}
