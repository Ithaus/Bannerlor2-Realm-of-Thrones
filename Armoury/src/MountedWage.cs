using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// KONNY BIERZE WIEKSZY ZOLD (paczka 160, decyzja Jeffa 08.10: "albo najemnik ma konia, wtedy jest konny, albo przychodzi bez konia,
    /// wtedy jest pieszy ... jak ma konia, to chce wiekszy zold pewnie"; wylacznik MountedWagePremium, suwak MountedWageFactor).
    ///
    /// Sprawdzone w kodzie (1.4.8, czynny model BKROTPatch.Models.BKROTPartyWageModel : BannerKings BKPartyWageModel : DefaultPartyWageModel):
    /// zold jednostki to DefaultPartyWageModel.GetCharacterWage - tylko tier (1, 2, 3, 5, 8, 12, 17, 23) i najemnik x1.5; BK i BKROTPatch
    /// go nie nadpisuja (BK liczy zold partii z CharacterObject.TroopWage = GetCharacterWage, razy BaseWage 1.25 z MCM BK, kultura,
    /// garnizon -50%, perki), ROT (ROTPartyWageModel - nieczynny) zmienia tylko Unsullied i Innych. Konny bierze tyle samo co pieszy
    /// tego samego tieru - jedyna roznica w BK to perk CataphractEquites (-10% zoldu za konnych). Historycznie konny ok. x2 (Anglia,
    /// kampania Crecy-Calais 1346-47, rachunki Garderoby: lucznik konny 6 d, pieszy 3 d, hobelar 6 d, piechur walijski 2 d, man-at-arms
    /// 12 d, rycerz 24 d - A. Ayton, Knights and Warhorses, 1994; M. Prestwich, Armies and Warfare in the Middle Ages, 1996).
    ///
    /// Teraz: postfiks na GetCharacterWage kazdego modelu zoldu (licznik zagniezdzenia - liczymy raz, na zewnatrz): jednostka konna
    /// (IsMounted, nie bohater) - zold x MountedWageFactor (domyslnie 1.5: t4 8 -> 12 d = man-at-arms, t6 17 -> 26 d ~ rycerz 24 d;
    /// x2 dawalby t4 16, t6 34 - ponad historie, bo piesi od tieru 3 sa w grze juz oplacani ponad historie, a 86% jednostek konnych ROT
    /// to tiery 4-6). Ta sama liczba idzie wszedzie, gdzie gra i mody czytaja zold jednostki: zold partii (BK), ekran druzyny, budzet
    /// zoldu AI (werbunek, jency), cena werbunku "dni zoldu" (RecruitCost), koszt awansu, limit zoldu i dezercja.
    /// Tylko partie, ktorych zold idzie do ludzi (SoldierPay: partia rodu -> sakiewki ludzi, garnizon -> kasa osady): partie rodow
    /// (takze gracz) i garnizony. Karawana (zold w nicosc jak w grze) i inne partie - bez premii: kontekst partii z GetTotalWage
    /// (zold partii) i z RecruitmentCampaignBehavior.CheckRecruiting (werbunek AI - karawana placi dni zoldu bez premii).
    ///
    /// Kiedy latamy (pulapka z SoldierPay): GetCharacterWage i CheckRecruiting - przy starcie gry (ciala bez pol statycznych; te same
    /// klasy co latki RecruitCost); GetTotalWage modeli (BK, BKROTPatch czytaja statyczne singletony BK) - dopiero w kampanii, przy
    /// OnSessionLaunched (EnsureContextHooks), raz na klase. Linia dnia "Zold konnych (160)": sklad wojska (konni / wszyscy) i premia
    /// wedle stawek jednostek - tylko log.
    /// </summary>
    internal static class MountedWage
    {
        [ThreadStatic] private static int _depth;
        [ThreadStatic] private static MobileParty _party;   // partia, ktorej zold (GetTotalWage) albo werbunek (CheckRecruiting) liczymy; null = poza nimi
        [ThreadStatic] private static bool _raw;            // linia dnia: stawka jednostki bez premii

        private static Harmony _harmony;
        private static readonly HashSet<Type> _totalHooked = new HashSet<Type>();
        private static int _wageModels, _recruitHook;

        internal static bool On { get { var s = Settings.Current; return s != null && s.MountedWagePremium && Factor(s) > 1f; } }

        internal static float Factor(Settings s) { return s == null ? 1f : Math.Max(1f, Math.Min(8f, s.MountedWageFactor)); }

        /// <summary>Czy partia dostaje premie konnego: partia rodu (takze gracz) albo garnizon; null (poza zoldem partii i werbunkiem AI) - tak.</summary>
        internal static bool PartyPays(MobileParty p) { return p == null || p.IsLordParty || p.IsGarrison; }

        /// <summary>Zold jednostki z premia konnego (raw - zold z modelu gry).</summary>
        internal static int Apply(int raw, CharacterObject c, MobileParty p)
        {
            if (raw <= 0 || c == null || c.IsHero || !c.IsMounted || !PartyPays(p)) return raw;
            var s = Settings.Current;
            if (s == null || !s.MountedWagePremium) return raw;
            float f = Factor(s);
            if (f <= 1f) return raw;
            return Math.Max(raw, (int)Math.Round(raw * f, MidpointRounding.AwayFromZero));
        }

        // ------------------------------------------------------------ GetCharacterWage (kazdy model zoldu)
        public static void WagePrefix() { _depth++; }
        public static Exception WageFinalizer(Exception __exception) { if (_depth > 0) _depth--; return __exception; }
        public static void WagePostfix(CharacterObject __0, ref int __result)
        {
            if (_depth > 1 || _raw) return;
            try { __result = Apply(__result, __0, _party); }
            catch { }
        }

        // ------------------------------------------------------------ kontekst partii: GetTotalWage (zold partii) i CheckRecruiting (werbunek AI)
        public static void PartyPrefix(MobileParty __0, out MobileParty __state) { __state = _party; _party = __0; }
        public static Exception PartyFinalizer(Exception __exception, MobileParty __state) { _party = __state; return __exception; }

        /// <summary>Zold jednostki z modelu gry bez premii (linia dnia).</summary>
        private static int Raw(PartyWageModel m, CharacterObject c)
        {
            bool was = _raw; _raw = true;
            try { return m.GetCharacterWage(c); }
            finally { _raw = was; }
        }

        internal static void ApplyAll(Harmony h)
        {
            _harmony = h;
            int n = 0;
            var seen = new HashSet<Type>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                {
                    try
                    {
                        if (t == null || t.IsAbstract || !typeof(PartyWageModel).IsAssignableFrom(t)) continue;
                        var m = t.GetMethod("GetCharacterWage", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(CharacterObject) }, null);
                        if (m == null || m.DeclaringType != t || m.IsAbstract || seen.Contains(t)) continue;
                        seen.Add(t);
                        h.Patch(m, prefix: new HarmonyMethod(typeof(MountedWage), nameof(WagePrefix)) { priority = Priority.First },
                                   postfix: new HarmonyMethod(typeof(MountedWage), nameof(WagePostfix)) { priority = Priority.Last },
                                   finalizer: new HarmonyMethod(typeof(MountedWage), nameof(WageFinalizer)));
                        n++;
                    }
                    catch (Exception e) { Log.Error("MountedWage.ApplyAll(" + (t != null ? t.FullName : "?") + ")", e); }
                }
            }
            _wageModels = n;
            try
            {
                var cr = AccessTools.Method(typeof(RecruitmentCampaignBehavior), "CheckRecruiting", new[] { typeof(MobileParty), typeof(Settlement) });
                if (cr != null)
                {
                    h.Patch(cr, prefix: new HarmonyMethod(typeof(MountedWage), nameof(PartyPrefix)), finalizer: new HarmonyMethod(typeof(MountedWage), nameof(PartyFinalizer)));
                    _recruitHook = 1;
                }
            }
            catch (Exception e) { Log.Error("MountedWage.CheckRecruiting", e); }
            var s = Settings.Current;
            Log.Info("MountedWage: premia zoldu konnego (paczka 160) " + (On ? "CZYNNA x" + Factor(s).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "WYLACZONA")
                     + " - zold jednostki wpiety w " + n + " modelach zoldu (GetCharacterWage), werbunek AI (CheckRecruiting) " + (_recruitHook == 1 ? "wpiety" : "BRAK")
                     + "; zold partii (GetTotalWage - karawany bez premii) dojdzie przy starcie kampanii.");
        }

        /// <summary>W kampanii (OnSessionLaunched): kontekst partii na GetTotalWage kazdej implementacji modelu zoldu - raz na klase.</summary>
        internal static void EnsureContextHooks()
        {
            var h = _harmony;
            if (h == null) return;
            int now = 0;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                {
                    try
                    {
                        if (t == null || t.IsAbstract || !typeof(PartyWageModel).IsAssignableFrom(t) || _totalHooked.Contains(t)) continue;
                        var m = t.GetMethod("GetTotalWage", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null,
                                            new[] { typeof(MobileParty), typeof(TroopRoster), typeof(bool) }, null);
                        if (m == null || m.IsAbstract) continue;
                        _totalHooked.Add(t);
                        h.Patch(m, prefix: new HarmonyMethod(typeof(MountedWage), nameof(PartyPrefix)) { priority = Priority.First },
                                   finalizer: new HarmonyMethod(typeof(MountedWage), nameof(PartyFinalizer)));
                        now++;
                    }
                    catch (Exception e) { Log.Error("MountedWage.EnsureContextHooks(" + (t != null ? t.FullName : "?") + ")", e); }
                }
            }
            if (now > 0 || _totalHooked.Count == 0)
            {
                string active = "?";
                try { active = Campaign.Current.Models.PartyWageModel.GetType().FullName; } catch { }
                Log.Info("MountedWage: zold partii (GetTotalWage) wpiety w " + _totalHooked.Count + " modelach (teraz " + now + "), model czynny " + active
                         + " - premia konnego w partiach rodow i garnizonach, karawany i inne partie bez premii.");
            }
        }

        // ------------------------------------------------------------ linia dnia "Zold konnych (160)" (tylko log)
        private sealed class Row { public int Parties; public long Men, Mounted, All0, All1, Mnt0, Mnt1; }

        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            var model = Campaign.Current.Models.PartyWageModel;
            if (model == null) return;
            var lords = new Row(); var player = new Row(); var gar = new Row(); var car = new Row();
            var cache = new Dictionary<CharacterObject, int>();
            foreach (var mp in MobileParty.All)
            {
                if (mp == null || !mp.IsActive) continue;
                Row r = mp.IsMainParty ? player : mp.IsLordParty ? lords : mp.IsGarrison ? gar : mp.IsCaravan ? car : null;
                if (r == null) continue;
                var roster = mp.MemberRoster;
                if (roster == null) continue;
                r.Parties++;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var c = el.Character;
                    if (c == null || c.IsHero || el.Number <= 0) continue;
                    int raw;
                    if (!cache.TryGetValue(c, out raw)) { raw = Raw(model, c); cache[c] = raw; }
                    int paid = Apply(raw, c, mp);
                    long n = el.Number;
                    r.Men += n; r.All0 += n * raw; r.All1 += n * paid;
                    if (c.IsMounted) { r.Mounted += n; r.Mnt0 += n * raw; r.Mnt1 += n * paid; }
                }
            }
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            Func<string, Row, bool, string> F = (name, r, prem) =>
            {
                string t = name + " (" + r.Parties + "): konnych " + r.Mounted + " z " + r.Men + " ludzi (" + (r.Men > 0 ? (100.0 * r.Mounted / r.Men).ToString("0.0", inv) : "0") + "%)";
                t += ", stawki dzienne: konni " + r.Mnt1 + (prem ? " (bez premii " + r.Mnt0 + ")" : "") + " z " + r.All1;
                if (prem) t += " (bez premii " + r.All0 + ", +" + (r.All0 > 0 ? (100.0 * (r.All1 - r.All0) / r.All0).ToString("0.0", inv) : "0") + "%)";
                else t += " (udzial konnych " + (r.All0 > 0 ? (100.0 * r.Mnt0 / r.All0).ToString("0.0", inv) : "0") + "%, bez premii)";
                return t;
            };
            Log.Info("Zold konnych (160, premia " + (On ? "CZYNNA x" + Factor(s).ToString("0.00", inv) : "WYLACZONA") + "): dzien " + (int)CampaignTime.Now.ToDays
                     + " | " + F("partie rodow AI", lords, true) + " | " + F("gracz", player, true) + " | " + F("garnizony", gar, true) + " | " + F("karawany", car, false)
                     + " | stawki jednostek z modelu zoldu (bez mnoznikow BK - BaseWage, kultura, garnizon -50%, perki - one mnoza zold partii po rowno, wiec procent premii jest ten sam)"
                     + "; latki: zold jednostki " + _wageModels + " modeli, zold partii " + _totalHooked.Count + ", werbunek AI " + _recruitHook + "/1.");
        }
    }
}
