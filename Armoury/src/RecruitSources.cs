using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 171 ZRODLA REKRUTOW (docs/paczki/171-zbrojenie-zalog.md, A1, A5, B2). Skad przychodzi czlowiek, ktorego werbunek ogloszono:
    ///  - A1 echo ROT: ROTTroopRecruiter.ExchangeClanTroops(..., fireEvent: true) zamienia zwerbowanego X na czlowieka rodu Y w tej samej
    ///    druzynie i oglasza werbunek DRUGI raz (OnTroopRecruited(lord, null, null, Y, n)). To ten sam czlowiek - komplet dostal w zdarzeniu
    ///    pierwszym. Licznik glebokosci (prefiks + finalizer), nie prywatne pole ROT (_firingEvent ustawiane bez try/finally).
    ///  - B2 jeniec: RecruitPrisonersCampaignBehavior.RecruitPrisonersAi wola OnTroopRecruited(lord, null, null, ...) synchronicznie -
    ///    licznik glebokosci; obszukany jeniec przychodzi z niczym (jego sprzet wzial zwyciezca w lupie).
    ///  - A5 autowerbunek zalogi: GarrisonRecruitmentCampaignBehavior.TickAutoRecruitmentGarrisonChange bierze ochotnika z puli BEZ zdarzenia;
    ///    migawka pul (prefiks) i roznica (postfiks) - komplet z zapisu notabla idzie do zbrojowni zalogi (RecruitKit.OnGarrisonTook).
    /// Flagi wpiecia latek na caly proces (Reset jest raz na kampanie, latki Harmony zyja caly proces): podwojnie wpiety postfiks A5
    /// wkladalby kazdy komplet dwa razy.
    /// </summary>
    internal static class RecruitSources
    {
        private static Harmony _h;
        private static bool _applied, _rotHooked, _prisonHooked, _garrisonAutoHooked;
        private static int _rotDepth, _prisonerDepth;
        private static int _stumbles, _errDay = -1;
        private static readonly HashSet<string> _errWhere = new HashSet<string>();

        internal static void Reset() { _rotDepth = 0; _prisonerDepth = 0; _stumbles = 0; _errDay = -1; _errWhere.Clear(); }

        /// <summary>Potkniecia doby (do linii "Pule ochotnikow (171)") - zerowane przy odczycie.</summary>
        internal static int TakeStumbles() { int n = _stumbles; _stumbles = 0; return n; }

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try
            {
                int d = (int)CampaignTime.Now.ToDays;
                if (d != _errDay) { _errDay = d; _errWhere.Clear(); }
                if (_errWhere.Add(where)) Log.Error("RecruitSources." + where, e);
            }
            catch { }
        }

        /// <summary>Echo werbunku ROT: ten sam czlowiek (zamiana oznaki), zrodlo i osada null, w trakcie ExchangeClanTroops z fireEvent.</summary>
        internal static bool IsRotEcho(Settlement settlement, Hero source)
        {
            var s = Settings.Current;
            return s != null && s.RotSwapSameMan && _rotDepth > 0 && settlement == null && source == null;
        }

        internal static bool InPrisonerRecruit { get { return _prisonerDepth > 0; } }

        // ------------------------------------------------------------ A1: ROT ExchangeClanTroops
        public static void RotExchangePrefix(bool fireEvent, out bool __state) { __state = fireEvent; if (fireEvent) _rotDepth++; }
        public static Exception RotExchangeFinalizer(Exception __exception, bool __state) { if (__state && _rotDepth > 0) _rotDepth--; return __exception; }

        // ------------------------------------------------------------ B2: jency
        public static void PrisonPrefix() { _prisonerDepth++; }
        public static Exception PrisonFinalizer(Exception __exception) { if (_prisonerDepth > 0) _prisonerDepth--; return __exception; }

        // ------------------------------------------------------------ A5: autowerbunek zalogi
        public static void GarrisonAutoPrefix(Town __0, out Dictionary<Hero, CharacterObject[]> __state)
        {
            __state = null;
            try
            {
                var s = Settings.Current;
                if (__0 == null || __0.Settlement == null || s == null || !s.GarrisonRecruitKeepsKit || !RecruitKit.On || AiGear.Armories() == null) return;
                var st = __0.Settlement;
                var map = new Dictionary<Hero, CharacterObject[]>();
                // ta sama lista notabli, z ktorej gra buduje _volunteerListCache (osada i jej wsie)
                foreach (var n in st.Notables)
                    if (n != null && n.VolunteerTypes != null && !map.ContainsKey(n)) map[n] = (CharacterObject[])n.VolunteerTypes.Clone();
                if (st.BoundVillages != null)
                    foreach (var v in st.BoundVillages)
                    {
                        if (v == null || v.Settlement == null) continue;
                        foreach (var n in v.Settlement.Notables)
                            if (n != null && n.VolunteerTypes != null && !map.ContainsKey(n)) map[n] = (CharacterObject[])n.VolunteerTypes.Clone();
                    }
                __state = map;
            }
            catch (Exception e) { __state = null; Stumble("GarrisonAutoPrefix", e); }
        }

        public static void GarrisonAutoPostfix(Town __0, Dictionary<Hero, CharacterObject[]> __state)
        {
            if (__state == null || __0 == null) return;
            try
            {
                var garrison = __0.GarrisonParty;   // gra tworzy zaloge w tej metodzie, gdy jej nie bylo
                foreach (var kv in __state)
                {
                    var n = kv.Key; var before = kv.Value; var after = n != null ? n.VolunteerTypes : null;
                    if (after == null || before == null) continue;
                    int len = Math.Min(before.Length, after.Length);
                    for (int i = 0; i < len; i++)
                    {
                        // gra zeruje slot dokladnie przy przejsciu ochotnika do zalogi
                        if (before[i] == null || after[i] != null) continue;
                        try { RecruitKit.OnGarrisonTook(n, before[i], garrison, __0.Settlement); }
                        catch (Exception e) { Stumble("OnGarrisonTook", e); }
                    }
                }
            }
            catch (Exception e) { Stumble("GarrisonAutoPostfix", e); }
        }

        // ------------------------------------------------------------ wpiecie
        private static bool TryRot()
        {
            if (_rotHooked || _h == null) return _rotHooked;
            var t = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTTroopRecruiter");
            if (t == null) return false;
            var m = AccessTools.Method(t, "ExchangeClanTroops", new[] { typeof(Hero), typeof(TroopRoster), typeof(CharacterObject), typeof(int), typeof(bool), typeof(Settlement) });
            if (m == null) return false;
            _h.Patch(m, prefix: new HarmonyMethod(typeof(RecruitSources), nameof(RotExchangePrefix)),
                        finalizer: new HarmonyMethod(typeof(RecruitSources), nameof(RotExchangeFinalizer)));
            _rotHooked = true;
            return true;
        }

        internal static void ApplyAll(Harmony h)
        {
            if (_applied || h == null) return;
            _applied = true;
            _h = h;
            try { TryRot(); } catch (Exception e) { Log.Error("RecruitSources.ApplyAll(ROT)", e); }
            try
            {
                if (!_prisonHooked)
                {
                    var m = AccessTools.Method(typeof(RecruitPrisonersCampaignBehavior), "RecruitPrisonersAi", new[] { typeof(MobileParty), typeof(CharacterObject), typeof(int), typeof(int) });
                    if (m != null)
                    {
                        h.Patch(m, prefix: new HarmonyMethod(typeof(RecruitSources), nameof(PrisonPrefix)), finalizer: new HarmonyMethod(typeof(RecruitSources), nameof(PrisonFinalizer)));
                        _prisonHooked = true;
                    }
                }
            }
            catch (Exception e) { Log.Error("RecruitSources.ApplyAll(jency)", e); }
            try
            {
                if (!_garrisonAutoHooked)
                {
                    var m = AccessTools.Method(typeof(GarrisonRecruitmentCampaignBehavior), "TickAutoRecruitmentGarrisonChange", new[] { typeof(Town) });
                    if (m != null)
                    {
                        h.Patch(m, prefix: new HarmonyMethod(typeof(RecruitSources), nameof(GarrisonAutoPrefix)), postfix: new HarmonyMethod(typeof(RecruitSources), nameof(GarrisonAutoPostfix)));
                        _garrisonAutoHooked = true;
                    }
                }
            }
            catch (Exception e) { Log.Error("RecruitSources.ApplyAll(autowerbunek)", e); }
            Log.Info("RecruitSources: echo werbunku ROT (ten sam czlowiek) " + (_rotHooked ? "wpiete" : "BRAK ROTTroopRecruiter.ExchangeClanTroops (proba ponowna przy starcie kampanii)")
                     + "; jency " + (_prisonHooked ? "wpiete" : "BRAK") + "; autowerbunek zalog " + (_garrisonAutoHooked ? "wpiety" : "BRAK") + ".");
        }

        /// <summary>Z OnSessionLaunched: gdy typu ROT nie bylo przy starcie (kolejnosc ladowania) - jedna proba wpiecia WYLACZNIE latki ROT.</summary>
        internal static void ApplyLate()
        {
            if (_h == null || _rotHooked) return;
            bool ok = false;
            try { ok = TryRot(); } catch (Exception e) { Log.Error("RecruitSources.ApplyLate", e); }
            Log.Info("RecruitSources: echo werbunku ROT przy starcie kampanii " + (ok ? "wpiete" : "BRAK ROTTroopRecruiter.ExchangeClanTroops - echo liczone jako ochotnik bez zrodla (dobytek, nie wzorzec)") + ".");
        }
    }
}
