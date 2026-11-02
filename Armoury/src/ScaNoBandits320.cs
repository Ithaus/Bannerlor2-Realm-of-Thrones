using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// 320b (Jeff 02.11 "i niech korona przestanie liczyc bandytow i Innych jako wroga"). Klucz StrategicAiIgnoresBandits.
    ///
    /// Przyczyna (StrategicCampaignAI145.dll, dekompilacja sca320/a): StrategicCampaignAI145Behavior.CalculateFactionStatus(Kingdom) (private static,
    /// zadeklarowana w tej klasie; wolana z OnDailyTick i co 4 h z OnHourlyTick przed SetFactionStatus/UpdateWarGoal) sumuje EnemyStrength jako
    /// CurrentTotalStrength WSZYSTKICH kingdom.FactionsAtWarWith - takze klanow band (IsBanditFaction, stala wojna z DefaultDiplomacyModel), Innych
    /// (ROTclan_126, kultura whitewalker, is_outlaw) i mniejszych frakcji wyjetych spod prawa (Bractwo bez Choragwi itp.). StrategicFactionStatus.IsExhausted:
    /// wrog > swoi x 1.35 -> ForcePeace / WantsPeace, a StrategicTargetScoreModel tlumi cele oblezen x0.19 i podbija obrone x1.7.
    ///
    /// Zmiana: postfiks na CalculateFactionStatus (DeclaredMethod na klasie deklarujacej, sprawdzony Harmony.GetPatchInfo po wlascicielu) liczy EnemyStrength
    /// od nowa bez band, Innych i frakcji wyjetych spod prawa (krolestwa, klany zbuntowane i inne klany z wojna zostaja). ActiveWars, ThreatenedFortifications,
    /// RaidedVillages bez zmian (decyzja dla Jeffa - polecenie mowi o sile wroga). WantsPeace i IsExhausted SCA przelicza sam (SetFactionStatus po postfiksie).
    /// Klucz na zywo: wylaczony = postfiks nic nie zmienia. Potkniecia liczone, bez globalnego wylacznika. Tylko odczyt cudzych struktur + zapis jednej wlasciwosci.
    /// Linie: "SCA bez band (320b): start sesji ..." (latka, cel) i co dobe "SCA bez band (320b): dzien N ..." (na krolestwo wrog przed/po, cel, wyczerpanie).
    /// </summary>
    internal static class ScaNoBandits320
    {
        private const string HarmonyId = "com.jeff.armoury.scanobandits320";
        internal const string P = "SCA bez band (320b): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Rec
        {
            public string Name;
            public float Before, After, Bandits, Others, Outlaws;
            public int NBandits, NOthers, NOutlaws, NKept;
        }

        private static bool _hookTried, _wired;
        private static string _hookNote = "-";
        private static PropertyInfo _pEnemy;                        // StrategicFactionStatus.EnemyStrength (internal klasa, publiczny setter)
        private static PropertyInfo _pOwn, _pForts, _pThreat, _pWars, _pExh, _pPeace, _pGoal;
        private static MethodInfo _mGetStatus;                      // StrategicAiState.GetFactionStatus(IFaction)
        private static readonly Dictionary<string, Rec> _last = new Dictionary<string, Rec>();   // StringId krolestwa -> ostatnia ocena
        private static int _dEvals, _dChanged, _stumbles, _dStumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        internal static bool On { get { var s = Settings.Current; return s != null && s.StrategicAiIgnoresBandits; } }

        internal static void Reset() { _last.Clear(); _dEvals = _dChanged = _stumbles = _dStumbles = 0; _errSites.Clear(); }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_errSites.Add(where)) Log.Error("ScaNoBandits320." + where + " (potkniecie " + _stumbles + ")", e); } catch { }
        }

        // ------------------------------------------------------------ latka (raz na proces, w kampanii)
        internal static void EnsureHooks()
        {
            if (!_hookTried)
            {
                _hookTried = true;
                try
                {
                    var t = AccessTools.TypeByName("StrategicCampaignAI145.StrategicCampaignAI145Behavior");
                    var m = t != null ? AccessTools.DeclaredMethod(t, "CalculateFactionStatus", new[] { typeof(Kingdom) }) : null;
                    if (t == null) _hookNote = "bez SCA (brak StrategicCampaignAI145.StrategicCampaignAI145Behavior)";
                    else if (m == null) _hookNote = "BRAK (StrategicCampaignAI145Behavior.CalculateFactionStatus(Kingdom))";
                    else
                    {
                        var st = m.ReturnType;
                        _pEnemy = AccessTools.Property(st, "EnemyStrength");
                        _pOwn = AccessTools.Property(st, "OwnedStrength");
                        _pForts = AccessTools.Property(st, "OwnedFortifications");
                        _pThreat = AccessTools.Property(st, "ThreatenedFortifications");
                        _pWars = AccessTools.Property(st, "ActiveWars");
                        _pExh = AccessTools.Property(st, "IsExhausted");
                        _pPeace = AccessTools.Property(st, "WantsPeace");
                        _pGoal = AccessTools.Property(st, "WarGoal");
                        var stt = AccessTools.TypeByName("StrategicCampaignAI145.StrategicAiState");
                        _mGetStatus = stt != null ? AccessTools.DeclaredMethod(stt, "GetFactionStatus", new[] { typeof(IFaction) }) : null;
                        if (_pEnemy == null || _pEnemy.GetSetMethod(true) == null) _hookNote = "BRAK (StrategicFactionStatus.EnemyStrength bez settera)";
                        else
                        {
                            var h = new Harmony(HarmonyId);
                            h.Patch(m, postfix: new HarmonyMethod(typeof(ScaNoBandits320), nameof(CalcPostfix)) { priority = Priority.Normal });
                            int others = 0;
                            var info = Harmony.GetPatchInfo(m);
                            if (info != null)
                            {
                                foreach (var p in info.Postfixes) { if (p == null) continue; if (p.owner == HarmonyId && p.PatchMethod != null && p.PatchMethod.Name == nameof(CalcPostfix)) _wired = true; else others++; }
                                foreach (var p in info.Prefixes) if (p != null && p.owner != HarmonyId) others++;
                                foreach (var p in info.Transpilers) if (p != null && p.owner != HarmonyId) others++;
                                foreach (var p in info.Finalizers) if (p != null && p.owner != HarmonyId) others++;
                            }
                            _hookNote = (_wired ? "wpieta" : "BRAK (latka nie weszla - slad w BKROTLog.txt)") + " (" + m.DeclaringType.FullName + "." + m.Name + "(Kingdom)"
                                        + (others > 0 ? ", cudzych latek " + others : "") + ")";
                        }
                    }
                }
                catch (Exception e) { _hookNote = "BRAK (" + e.GetType().Name + ": " + e.Message + ")"; Stumble("EnsureHooks", e); }
            }
            try
            {
                Log.Info(P + "start sesji - latka " + _hookNote + ", klucz Strategic AI Ignores Bandits " + (On ? "wlaczony" : "WYLACZONY")
                         + " | wrog SCA bez band (IsBanditFaction), Innych (kultura whitewalker) i frakcji wyjetych spod prawa (IsOutlaw); liczba wojen, zagrozone twierdze i wioski bez zmian.");
            }
            catch (Exception e) { Stumble("StartLine", e); }
        }

        /// <summary>Kto NIE jest wrogiem dla SCA: 1 = banda, 2 = Inni, 3 = wyjeci spod prawa, 0 = wrog.</summary>
        internal static int Skip(IFaction f)
        {
            if (f == null) return 3;
            if (f.IsBanditFaction) return 1;
            var c = f as Clan;
            if (c != null && c.Culture != null && c.Culture.StringId == "whitewalker") return 2;
            if (f.IsOutlaw) return 3;
            return 0;
        }

        /// <summary>Postfiks StrategicCampaignAI145Behavior.CalculateFactionStatus: EnemyStrength bez band, Innych i wyjetych spod prawa. Nie rzuca.</summary>
        public static void CalcPostfix(Kingdom kingdom, object __result)
        {
            if (kingdom == null || __result == null || _pEnemy == null || !On) return;
            try
            {
                var r = new Rec { Name = kingdom.Name != null ? kingdom.Name.ToString() : kingdom.StringId };
                try { r.Before = Convert.ToSingle(_pEnemy.GetValue(__result, null), Inv); } catch { r.Before = -1f; }
                float e = 0f;
                foreach (var f in kingdom.FactionsAtWarWith)
                {
                    float v = 0f;
                    try { v = f != null ? f.CurrentTotalStrength : 0f; } catch { v = 0f; }
                    switch (Skip(f))
                    {
                        case 0: e += v; r.NKept++; break;
                        case 1: r.Bandits += v; r.NBandits++; break;
                        case 2: r.Others += v; r.NOthers++; break;
                        default: r.Outlaws += v; r.NOutlaws++; break;
                    }
                }
                r.After = e;
                _pEnemy.SetValue(__result, e, null);
                _last[kingdom.StringId] = r;
                _dEvals++;
                if (Math.Abs(r.Before - e) > 0.5f) _dChanged++;
            }
            catch (Exception ex) { Stumble("CalcPostfix", ex); }
        }

        // ------------------------------------------------------------ doba (tylko log)

        private static string F0(object o) { try { return o == null ? "?" : Convert.ToDouble(o, Inv).ToString("0", Inv); } catch { return "?"; } }
        private static object Get(PropertyInfo p, object o) { try { return p != null && o != null ? p.GetValue(o, null) : null; } catch { return null; } }

        internal static void Daily()
        {
            if (Campaign.Current == null) return;
            var s = Settings.Current;
            try
            {
                if (s != null && s.LogEnabled && (_wired || _hookTried))
                {
                    var sb = new StringBuilder(1200);
                    sb.Append(P).Append("dzien ").Append((int)CampaignTime.Now.ToDays).Append(" | klucz ").Append(On ? "tak" : "nie")
                      .Append(", latka ").Append(_wired ? "wpieta" : "BRAK").Append(", ocen doby ").Append(_dEvals).Append(" (zmienionych ").Append(_dChanged)
                      .Append("), potkniecia doby ").Append(_dStumbles).Append(" (sesja ").Append(_stumbles).Append(")");
                    if (_wired && On)
                    {
                        var parts = new List<string>();
                        foreach (var k in Kingdom.All)
                        {
                            if (k == null || k.IsEliminated) continue;
                            Rec r;
                            if (!_last.TryGetValue(k.StringId, out r)) continue;
                            object st = null;
                            try { st = _mGetStatus != null ? _mGetStatus.Invoke(null, new object[] { k }) : null; } catch (Exception e) { Stumble("Daily.Status", e); }
                            bool exh = Get(_pExh, st) is bool b1 && b1, peace = Get(_pPeace, st) is bool b2 && b2;
                            parts.Add(r.Name + ": wrog " + r.Before.ToString("0", Inv) + " -> " + r.After.ToString("0", Inv)
                                      + " (pominiete: bandy " + r.Bandits.ToString("0", Inv) + " z " + r.NBandits + ", Inni " + r.Others.ToString("0", Inv) + " z " + r.NOthers
                                      + ", wyjeci spod prawa " + r.Outlaws.ToString("0", Inv) + " z " + r.NOutlaws + "; liczeni " + r.NKept + ")"
                                      + ", swoi " + F0(Get(_pOwn, st)) + ", twierdze " + F0(Get(_pForts, st)) + " zagrozone " + F0(Get(_pThreat, st))
                                      + ", wojen " + F0(Get(_pWars, st)) + ", cel " + (Get(_pGoal, st) ?? "?") + ", wyczerpane " + (exh ? "tak" : "nie") + ", chce pokoju " + (peace ? "tak" : "nie"));
                        }
                        sb.Append(" | ").Append(parts.Count > 0 ? string.Join("; ", parts.ToArray()) : "brak ocen SCA");
                    }
                    sb.Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            _dEvals = _dChanged = _dStumbles = 0;
        }
    }
}
