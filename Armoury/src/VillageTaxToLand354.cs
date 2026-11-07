using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 354 (Jeff 07.11: "Damon Marbrand ... toczymy wojne i jest we wrogiej frakcji ... przeciez nie wyslamy zlota do przeciwnika bo zajale mu zamek ...
    /// Jesli Marband chce kase niech odbije zamek!"). Klucz VillageTaxToLandHolder.
    ///
    /// Przyczyna (BannerKings.dll, dekompilacja): BannerKings.Patches.EconomyPatches.ClanFinancesPatches.VillageIncomePrefix (prefiks BK na
    /// DefaultClanFinanceModel.AddSettlementIncome, zwraca false) przy rozliczeniu rodu (applyWithdrawals) daje podatek kazdej wsi z GetActualVillages(rod),
    /// ktorej tytul de iure ma ktos inny niz glowa rodu, temu posiadaczowi: ApplyWithdrawal(wies, podatek, title.deJure) - licznik wsi w dol, payTo.Gold += podatek,
    /// bez sprawdzenia frakcji; deJure null - licznik w dol i nikt nic nie dostaje (zloto znika). Zdobycie twierdzy (TitleManager.ApplyOwnerChange) zmienia tylko
    /// deFacto, wiec kazda wies zdobytej twierdzy oddawala caly podatek pokonanemu z wrogiego obozu - gracz i wszystkie AI. Sam BK gdzie indziej
    /// (VillageExtensions.GetActualOwner) uznaje posiadacza tytulu za pana wsi tylko, gdy jest w tej samej frakcji co wies - stad wies jest na liscie zdobywcy.
    ///
    /// Zmiana: transpiler na VillageIncomePrefix (DeclaredMethod na klasie deklarujacej, w kampanii - klasa ma statyczne pole new DefaultClanFinanceModel(), pulapka
    /// CLAUDE.md 7). Pierwszy odczyt title.deJure w warunku (title != null &amp;&amp; title.deJure != clan.Leader) &amp; applyWithdrawals zamieniony na Payee(title, wies,
    /// rod, podatek, applyWithdrawals): posiadacz zywy i w tej samej frakcji co wies (regula BK GetActualOwner) - jak dotad on; wrogi, niezywy albo brak - glowa rodu,
    /// wiec warunek falszywy i podatek idzie sciezka pana ziemi (num3 -> "Village Demesnes" -> goldChange -> kiesa rodu), licznik w dol przez ApplyWithdrawal(wies,
    /// podatek) jak przy wsi z wlasnym tytulem. Jeden platnik (licznik wsi), jeden odbiorca, bez podwojnej wyplaty (posiadacz spoza frakcji nie ma tej wsi na swojej
    /// liscie - BK GetVillages bierze Lordship tylko w swojej frakcji). Posiadacz z tej samej frakcji (inny rod, rycerz) - bez zmian.
    /// Podglad (applyWithdrawals = false) - Payee czysty odczyt, liczniki tylko przy wyplacie. Klucz na zywo (wylaczony = jak BK).
    /// Ta sama regula w GoldForecast.VillagesNow (prognoza gracza) i ClanIncomeBook.StableD (ziemia za tytulem 345) przez HolderGets / Redirects.
    /// Linie: "Wies dla trzymajacego (354): start sesji ..." i co dobe "Wies dla trzymajacego (354): dzien N ...". Potkniecia liczone, bez globalnego wylacznika.
    /// </summary>
    internal static class VillageTaxToLand354
    {
        private const string HarmonyId = "com.jeff.armoury.villagetax354";
        internal const string P = "Wies dla trzymajacego (354): ";

        private static bool _tried, _wired;
        private static string _note = "-";
        private static int _matches = -1;          // ile razy wzor IL pasowal w ostatnim przebiegu transpilera (ma byc 1)
        private static int _stumbles, _dStumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static MemberInfo _deJure;         // FeudalTitle.deJure (wlasciwosc albo pole)

        // liczniki doby (tylko przy wyplacie): [0] wrogi posiadacz, [1] bez posiadacza, [2] posiadacz niezywy (354r: tylko spoza frakcji wsi - niezywy z frakcji wsi tu nie trafia,
        // BK GetActualOwner nie sprawdza IsAlive, wiec takiej wsi nie ma na zadnej liscie GetActualVillages)
        private static readonly int[] _nPl = new int[3], _nAi = new int[3];
        private static readonly long[] _gPl = new long[3], _gAi = new long[3];
        private static readonly List<string> _plNames = new List<string>();

        internal static bool On { get { var s = Settings.Current; return s != null && s.VillageTaxToLandHolder; } }
        /// <summary>Regula dziala naprawde: klucz wlaczony i transpiler siedzi.</summary>
        internal static bool Active { get { return _wired && On; } }

        internal static void Reset()
        {
            for (int i = 0; i < 3; i++) { _nPl[i] = _nAi[i] = 0; _gPl[i] = _gAi[i] = 0; }
            _plNames.Clear(); _dStumbles = 0; _stumbles = 0; _errSites.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_errSites.Add(where)) Log.Error("VillageTaxToLand354." + where + " (potkniecie " + _stumbles + ")", e); } catch { }
        }

        /// <summary>Regula BK GetActualOwner: posiadacz tytulu dostaje podatek wsi tylko zywy i w tej samej frakcji co wies.</summary>
        private static bool HolderOk(Hero dj, Village v)
        {
            return dj != null && dj.IsAlive && v != null && v.Settlement != null && dj.MapFaction != null && ReferenceEquals(dj.MapFaction, v.Settlement.MapFaction);
        }

        /// <summary>Dla naszych odwzorowan BK (prognoza, ziemia 345): czy posiadacz tytulu tej wsi naprawde dostaje jej podatek. Bez dzialajacej reguly - jak BK (true).</summary>
        internal static bool HolderGets(Hero dj, Village v)
        {
            if (!Active) return true;
            return HolderOk(dj, v);
        }

        /// <summary>Czy podatek wsi z tytulem (hasTitle), ktory BK dalby posiadaczowi dj, idzie teraz panu ziemi (rodowi z GetActualVillages).</summary>
        internal static bool Redirects(bool hasTitle, Hero dj, Village v)
        {
            return Active && hasTitle && !HolderOk(dj, v);
        }

        private static Hero DeJureOf(object title)
        {
            if (title == null || _deJure == null) return null;
            var p = _deJure as PropertyInfo;
            if (p != null) return p.GetValue(title, null) as Hero;
            var f = _deJure as FieldInfo;
            return f != null ? f.GetValue(title) as Hero : null;
        }

        /// <summary>Wolane z VillageIncomePrefix zamiast title.deJure (pierwszy odczyt, w warunku). Zwraca posiadacza, gdy dostaje podatek; inaczej glowe rodu
        /// (warunek "deJure != clan.Leader" falszywy - sciezka pana ziemi). title nigdy null (BK sprawdza wczesniej).</summary>
        public static Hero Payee(object title, Village v, Clan clan, int amt, bool apply)
        {
            Hero dj = null;
            try
            {
                dj = DeJureOf(title);
                if (!On || clan == null || clan.Leader == null || ReferenceEquals(dj, clan.Leader) || HolderOk(dj, v)) return dj;
                if (apply)
                {
                    int k = dj == null ? 1 : (!dj.IsAlive ? 2 : 0);
                    if (ReferenceEquals(clan, Clan.PlayerClan))
                    {
                        _nPl[k]++; _gPl[k] += amt;
                        if (_plNames.Count < 8) _plNames.Add(SafeName(v) + " " + amt + " (" + (dj == null ? "bez posiadacza" : SafeName(dj) + (k == 2 ? ", niezywy" : ", " + SafeName(dj.MapFaction))) + ")");
                    }
                    else { _nAi[k]++; _gAi[k] += amt; }
                }
                return clan.Leader;
            }
            catch (Exception e) { Stumble("Payee", e); return dj; }   // blad - jak BK
        }

        private static string SafeName(Village v) { try { return v != null && v.Settlement != null && v.Settlement.Name != null ? v.Settlement.Name.ToString() : "?"; } catch { return "?"; } }
        private static string SafeName(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "?"; } catch { return "?"; } }
        private static string SafeName(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "?"; } catch { return "?"; } }

        // ------------------------------------------------------------ latka (raz na proces, w kampanii)
        internal static void EnsureHooks()
        {
            if (!_tried)
            {
                _tried = true;
                try
                {
                    var t = AccessTools.TypeByName("BannerKings.Patches.EconomyPatches+ClanFinancesPatches")
                            ?? AccessTools.TypeByName("BannerKings.Patches.EconomyPatches.ClanFinancesPatches");
                    var m = t != null ? AccessTools.DeclaredMethod(t, "VillageIncomePrefix") : null;
                    var tt = AccessTools.TypeByName("BannerKings.Managers.Titles.FeudalTitle");
                    _deJure = tt != null ? (MemberInfo)AccessTools.DeclaredProperty(tt, "deJure") ?? AccessTools.Property(tt, "deJure") ?? (MemberInfo)AccessTools.Field(tt, "deJure") : null;
                    if (t == null) _note = "bez BK (brak ClanFinancesPatches)";
                    else if (m == null) _note = "BRAK (ClanFinancesPatches.VillageIncomePrefix)";
                    else if (_deJure == null) _note = "BRAK (FeudalTitle.deJure)";
                    else
                    {
                        var h = new Harmony(HarmonyId);
                        h.Patch(m, transpiler: new HarmonyMethod(typeof(VillageTaxToLand354), nameof(Transpiler)));
                        bool ours = false; int others = 0;
                        var info = Harmony.GetPatchInfo(m);
                        if (info != null)
                        {
                            foreach (var p in info.Transpilers) { if (p == null) continue; if (p.owner == HarmonyId) ours = true; else others++; }
                            foreach (var p in info.Prefixes) if (p != null && p.owner != HarmonyId) others++;
                            foreach (var p in info.Postfixes) if (p != null && p.owner != HarmonyId) others++;
                        }
                        _wired = ours && _matches == 1;
                        _note = (_wired ? "wpiety" : (!ours ? "BRAK (latka nie weszla)" : "BRAK (wzor IL, trafien " + _matches + ")"))
                                + " (" + m.DeclaringType.FullName + "." + m.Name + (others > 0 ? ", cudzych latek " + others : "") + ")";
                    }
                }
                catch (Exception e) { _wired = false; _note = "BRAK (wyjatek " + e.GetType().Name + ")"; Stumble("EnsureHooks", e); }
            }
            try
            {
                if (Settings.Current != null && Settings.Current.LogEnabled)
                    Log.Info(P + "start sesji | klucz " + (On ? "tak" : "nie") + ", transpiler podatku wsi BK " + _note
                             + " | regula: posiadacz tytulu wsi dostaje jej podatek tylko zywy i w tej samej frakcji co wies; spoza frakcji (zywy albo niezywy) albo brak - pan ziemi (Village Demesnes); niezywy z frakcji wsi - bez zmian (wies na niczyjej liscie).");
            }
            catch { }
        }

        private static bool Calls(CodeInstruction ci, string name, string declType)
        {
            var mi = ci != null ? ci.operand as MethodInfo : null;
            return mi != null && (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && mi.Name == name
                   && mi.DeclaringType != null && mi.DeclaringType.Name == declType;
        }

        private static CodeInstruction LdFromSt(CodeInstruction st)
        {
            var op = st.opcode;
            if (op == OpCodes.Stloc_0) return new CodeInstruction(OpCodes.Ldloc_0);
            if (op == OpCodes.Stloc_1) return new CodeInstruction(OpCodes.Ldloc_1);
            if (op == OpCodes.Stloc_2) return new CodeInstruction(OpCodes.Ldloc_2);
            if (op == OpCodes.Stloc_3) return new CodeInstruction(OpCodes.Ldloc_3);
            if (op == OpCodes.Stloc_S || op == OpCodes.Stloc) return new CodeInstruction(OpCodes.Ldloc, st.operand);
            return null;
        }

        /// <summary>Wzor (BK 1.4.8): ldloc V; callvirt SettlementComponent.get_Settlement; callvirt TitleManager.GetTitle | call CalculateVillageIncome; stloc A |
        /// ldloc T; callvirt FeudalTitle.get_deJure; ldarg.0; callvirt Clan.get_Leader. Kazdy element dokladnie raz - inaczej instrukcje bez zmian.</summary>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            _matches = 0;
            try
            {
                int iV = -1, nV = 0, iA = -1, nA = 0, iD = -1, nD = 0;
                for (int i = 0; i < code.Count; i++)
                {
                    if (i + 2 < code.Count && code[i].IsLdloc() && Calls(code[i + 1], "get_Settlement", "SettlementComponent") && Calls(code[i + 2], "GetTitle", "TitleManager")) { iV = i; nV++; }
                    if (i + 1 < code.Count && Calls(code[i], "CalculateVillageIncome", "ClanFinancesPatches") && code[i + 1].IsStloc()) { iA = i + 1; nA++; }
                    if (i >= 1 && i + 2 < code.Count && Calls(code[i], "get_deJure", "FeudalTitle") && code[i - 1].IsLdloc() && code[i + 1].opcode == OpCodes.Ldarg_0
                        && Calls(code[i + 2], "get_Leader", "Clan")) { iD = i; nD++; }
                }
                var ldA = iA >= 0 ? LdFromSt(code[iA]) : null;
                if (nV != 1 || nA != 1 || nD != 1 || ldA == null || code[iD].labels.Count > 0 || iV > iD || iA > iD)
                {
                    _matches = nD == 1 ? 0 : nD;
                    return code;
                }
                var ldV = new CodeInstruction(code[iV].opcode, code[iV].operand);
                var payee = AccessTools.DeclaredMethod(typeof(VillageTaxToLand354), nameof(Payee));
                code[iD].opcode = OpCodes.Call; code[iD].operand = payee;
                code.InsertRange(iD, new[] { ldV, new CodeInstruction(OpCodes.Ldarg_0), ldA, new CodeInstruction(OpCodes.Ldarg_2) });
                _matches = 1;
            }
            catch (Exception e) { _matches = -2; Stumble("Transpiler", e); }
            return code;
        }

        // ------------------------------------------------------------ doba (tylko log)
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s != null && s.LogEnabled && _tried)
                {
                    int nP = _nPl[0] + _nPl[1] + _nPl[2], nA = _nAi[0] + _nAi[1] + _nAi[2];
                    long gP = _gPl[0] + _gPl[1] + _gPl[2], gA = _gAi[0] + _gAi[1] + _gAi[2];
                    var sb = new StringBuilder(600);
                    sb.Append(P).Append("dzien ").Append((int)CampaignTime.Now.ToDays).Append(" | klucz ").Append(On ? "tak" : "nie")
                      .Append(", transpiler ").Append(_wired ? "wpiety" : "BRAK")
                      .Append(" | podatek wsi do pana ziemi zamiast posiadacza tytulu (od ostatniej linii, przy rozliczeniu rodow): gracz ").Append(nP).Append(" wsi ").Append(gP).Append(" zl")
                      .Append(" (wrogi posiadacz ").Append(_nPl[0]).Append("/").Append(_gPl[0]).Append(", bez posiadacza ").Append(_nPl[1]).Append("/").Append(_gPl[1])
                      .Append(", niezywy ").Append(_nPl[2]).Append("/").Append(_gPl[2]).Append(")")
                      .Append("; AI ").Append(nA).Append(" wsi ").Append(gA).Append(" zl")
                      .Append(" (wrogi posiadacz ").Append(_nAi[0]).Append("/").Append(_gAi[0]).Append(", bez posiadacza ").Append(_nAi[1]).Append("/").Append(_gAi[1])
                      .Append(", niezywy ").Append(_nAi[2]).Append("/").Append(_gAi[2]).Append(")")
                      .Append(" | platnik: licznik wsi (BK ApplyWithdrawal), odbiorca: rod pana ziemi (Village Demesnes), posiadacz tytulu 0");
                    if (_plNames.Count > 0) sb.Append(" | wsie gracza: ").Append(string.Join("; ", _plNames.ToArray()));
                    sb.Append(" | potkniecia doby ").Append(_dStumbles).Append(" (sesja ").Append(_stumbles).Append(").");
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            for (int i = 0; i < 3; i++) { _nPl[i] = _nAi[i] = 0; _gPl[i] = _gAi[i] = 0; }
            _plNames.Clear(); _dStumbles = 0;
        }
    }
}
