using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace Armoury
{
    /// <summary>
    /// m101-prolog E1b - POKOJ KROLA W PROLOGU i PAKTY Z KROLESTWEM ROBERTA (Jeff 21.10: "Krolestwa zostaja osobno, ale na czas prologu lacze je z krolestwem Roberta
    /// paktami i sojuszami (mod Diplomacy to umie)" - "ok").
    /// (1) POKOJ: do startu opowiesci (Prologue.BeforeStory) prefiks DeclareWarAction.ApplyInternal (ta sama brama, ktora latka ROT ModCompatibility.Diplomacy) nie
    ///     przepuszcza wojny miedzy dwoma krolestwami Westeros (Polnoc, Korona, Dorzecze, Smocza Skala, Krainy Burzy, Reach, Dolina, Zelazne Wyspy, Dorne, Nocna Straz,
    ///     Skagos) - z AI, decyzji krolestwa, BK, Diplomacy, wojen fabularnych ROT i wezwan sojusznikow. Wolni Ludzie (freefolk) poza regula: Nocna Straz kontra
    ///     Wolni Ludzie i najazdy Wolnych Ludzi jak dotad. Poza regula tez: krolestwo, ktorym rzadzi gracz, i wojny z wrogosci gracza / przestepstwa / buntu / zalozenia
    ///     krolestwa (gracz sam je wywoluje; zablokowanie zostawiloby gre w polowie spotkania). Wojny fabularne ROT i tak nie ruszaja przed opowiescia (E1a).
    ///     Klucz ProloguePeace (na zywo).
    /// (2) PAKTY: kazde krolestwo Westeros poza Nocna Straza i Wolnymi Ludzmi zwiazane z krolestwem Roberta (vlandia): SOJUSZ gry (IAllianceCampaignBehavior.StartAlliance,
    ///     koniec przesuniety na dzien po smierci krola - prywatne UpdateAllianceEndTime, tylko przedluza) dla Polnocy (Ned - Reka Krola), Krain Burzy (Renly, brat krola)
    ///     i Dorzecza (Tullyowie, sojusznicy z Rebelii); PAKT O NIEAGRESJI moda Diplomacy (odbiciem: AbstractDiplomaticAction&lt;FormNonAggressionPactAction&gt;.Apply bez kosztow,
    ///     bez pytania gracza, czas do dnia po smierci krola) dla Doliny, Smoczej Skaly, Reach, Dorne, Zelaznych Wysp i Skagos. Brak moda Diplomacy - cicho bez paktow.
    ///     Zakladane po rozstawieniu swiata (OnNewGameCreatedPartialFollowUpEnd) i pilnowane co dobe (wygasly/zerwany - od nowa). Wezwania do wojny miedzy krolem i jego
    ///     sojusznikami z prologu sa w prologu zablokowane (prefiks AllianceCampaignBehavior.StartCallToWarAgreement - nikt nie wciaga sojusznikow np. do wojny z Wolnymi
    ///     Ludzmi; latka 168 CrownCallToWar rozpoznaje brak porozumienia po portfelach). KONIEC: przy smierci Roberta (EndPacts) nasze sojusze zerwane (EndAlliance gry
    ///     przez interfejs), nasze pakty wygaszone (Expire Diplomacy). CrashScribe CanonAlliances przepuszcza sojusze prologu (IsPrologueAlliance - odbiciem).
    ///     Klucz ProloguePacts (co dobe).
    /// </summary>
    internal static partial class Prologue
    {
        internal const string Crown = "vlandia";
        private static readonly string[] AllyIds = { "battania", "stormlands", "riverlands" };
        private static readonly string[] NapIds = { "empire", "dragonstone", "empire_w", "aserai", "sturgia", "skagosi" };
        // krolestwa Westeros pod pokojem krola (bez Wolnych Ludzi) - id z ROT.Misc.ROTKingdoms
        private static readonly HashSet<string> Realms = new HashSet<string>(StringComparer.Ordinal)
        {
            "battania", "vlandia", "riverlands", "dragonstone", "stormlands", "empire_w", "empire", "sturgia", "aserai", "nightswatch", "skagosi"
        };

        // stan (zapis): sojusze i pakty zalozone przez nas, koniec paktow, wojny zablokowane
        private static readonly HashSet<string> _ourAllies = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> _ourNaps = new HashSet<string>(StringComparer.Ordinal);
        private static bool _pactsEnded;
        private static int _warsBlocked, _warsBlockedDay, _callsBlockedDay;
        private static readonly List<string> _blockedToday = new List<string>();

        // Diplomacy (odbiciem)
        private static bool _dipTried;
        private static MethodInfo _mNapApply, _mHasNap, _mExpire;
        private static PropertyInfo _pDipInstance;
        private static MethodInfo _mUpdateEnd;
        private static string _peaceWire = "-";

        private static bool PeaceKey { get { var s = S; return s != null && s.ProloguePeace; } }
        private static bool PactsKey { get { var s = S; return s != null && s.ProloguePacts; } }

        /// <summary>Pokoj krola czynny: kampania z prologiem, klucz, opowiesc jeszcze nie ruszyla.</summary>
        internal static bool PeaceOn { get { return Active && PeaceKey && BeforeStory(); } }

        private static void ResetPeace()
        {
            _ourAllies.Clear(); _ourNaps.Clear(); _pactsEnded = false;
            _warsBlocked = 0; _warsBlockedDay = 0; _callsBlockedDay = 0; _blockedToday.Clear();
        }

        private static string PackPeace()
        {
            return "pa=" + string.Join(",", _ourAllies.ToArray()) + ";pn=" + string.Join(",", _ourNaps.ToArray()) + ";pe=" + (_pactsEnded ? "1" : "0")
                   + ";wb=" + _warsBlocked.ToString(Inv) + ";";
        }

        private static void UnpackPeace(Dictionary<string, string> map)
        {
            ResetPeace();
            string v;
            if (map.TryGetValue("pa", out v)) foreach (var id in v.Split(',')) if (id.Length > 0) _ourAllies.Add(id);
            if (map.TryGetValue("pn", out v)) foreach (var id in v.Split(',')) if (id.Length > 0) _ourNaps.Add(id);
            _pactsEnded = map.TryGetValue("pe", out v) && v == "1";
            _warsBlocked = ParseInt(map, "wb", 0);
        }

        // ---------------------------------------------------------------- latki

        private static string ApplyPeace(Harmony h)
        {
            string w = "BRAK", c = "BRAK";
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(DeclareWarAction), "ApplyInternal");
                if (m != null && h.Patch(m, prefix: new HarmonyMethod(typeof(Prologue), nameof(WarPrefix))) != null) w = "TAK";
            }
            catch (Exception e) { Stumble("ApplyPeace.War", e); }
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(AllianceCampaignBehavior), "StartCallToWarAgreement",
                    new[] { typeof(Kingdom), typeof(Kingdom), typeof(Kingdom), typeof(int), typeof(bool) });
                if (m != null && h.Patch(m, prefix: new HarmonyMethod(typeof(Prologue), nameof(CallToWarPrefix))) != null) c = "TAK";
            }
            catch (Exception e) { Stumble("ApplyPeace.CallToWar", e); }
            try { _mUpdateEnd = AccessTools.DeclaredMethod(typeof(AllianceCampaignBehavior), "UpdateAllianceEndTime", new[] { typeof(Kingdom), typeof(Kingdom), typeof(CampaignTime) }); }
            catch { _mUpdateEnd = null; }
            _peaceWire = "wojny " + w + ", wezwania " + c + ", koniec sojuszu " + (_mUpdateEnd != null ? "TAK" : "BRAK");
            return _peaceWire;
        }

        private static bool IsRealm(IFaction f) { return f != null && f.IsKingdomFaction && f.StringId != null && Realms.Contains(f.StringId); }

        private static bool PlayerRules(IFaction f)
        {
            try { var k = f as Kingdom; return k != null && k.RulingClan != null && k.RulingClan == Clan.PlayerClan; } catch { return false; }
        }

        /// <summary>Prefiks DeclareWarAction.ApplyInternal: w prologu wojna krolestwo Westeros - krolestwo Westeros (bez Wolnych Ludzi) nie wchodzi.</summary>
        public static bool WarPrefix(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
        {
            try
            {
                if (!Active) return true;
                if (declareWarDetail == DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility || declareWarDetail == DeclareWarAction.DeclareWarDetail.CausedByCrimeRatingChange
                    || declareWarDetail == DeclareWarAction.DeclareWarDetail.CausedByRebellion || declareWarDetail == DeclareWarAction.DeclareWarDetail.CausedByKingdomCreation) return true;
                if (!IsRealm(faction1) || !IsRealm(faction2) || faction1 == faction2) return true;
                if (PlayerRules(faction1) || PlayerRules(faction2)) return true;
                if (!PeaceOn) return true;
                _warsBlocked++; _warsBlockedDay++;
                string key = faction1.StringId + "-" + faction2.StringId + " (" + declareWarDetail + ")";
                if (_blockedToday.Count < 8 && !_blockedToday.Contains(key)) _blockedToday.Add(key);
                return false;
            }
            catch (Exception e) { Stumble("WarPrefix", e); return true; }
        }

        /// <summary>Prefiks AllianceCampaignBehavior.StartCallToWarAgreement: w prologu krol i jego sojusznicy z prologu nie wzywaja sie do wojen.</summary>
        public static bool CallToWarPrefix(Kingdom callingKingdom, Kingdom calledKingdom)
        {
            try
            {
                if (!PeaceOn || callingKingdom == null || calledKingdom == null) return true;
                if (!IsPrologueAlliance(callingKingdom, calledKingdom)) return true;
                _callsBlockedDay++;
                return false;
            }
            catch (Exception e) { Stumble("CallToWarPrefix", e); return true; }
        }

        /// <summary>Sojusz z prologu (krol + Polnoc / Krainy Burzy / Dorzecze) w czasie prologu - CrashScribe CanonAlliances czyta to odbiciem.</summary>
        public static bool IsPrologueAlliance(Kingdom a, Kingdom b)
        {
            try
            {
                if (!Active || _pactsEnded || a == null || b == null || !BeforeStory()) return false;
                string x = a.StringId, y = b.StringId;
                if (x == Crown) return Array.IndexOf(AllyIds, y) >= 0;
                if (y == Crown) return Array.IndexOf(AllyIds, x) >= 0;
                return false;
            }
            catch { return false; }
        }

        // ---------------------------------------------------------------- pakty

        private static Kingdom K(string id)
        {
            foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k;
            return null;
        }

        private static bool DipReady()
        {
            if (!_dipTried)
            {
                _dipTried = true;
                try
                {
                    var tAct = AccessTools.TypeByName("Diplomacy.DiplomaticAction.NonAggressionPact.FormNonAggressionPactAction");
                    var tAbs = tAct != null ? tAct.BaseType : null;   // AbstractDiplomaticAction<FormNonAggressionPactAction>
                    if (tAbs != null)
                        _mNapApply = tAbs.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static, null,
                            new[] { typeof(Kingdom), typeof(Kingdom), typeof(bool), typeof(bool), typeof(float?), typeof(bool) }, null);
                    var tMgr = AccessTools.TypeByName("Diplomacy.DiplomaticAction.DiplomaticAgreementManager");
                    if (tMgr != null)
                    {
                        _pDipInstance = tMgr.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                        foreach (var m in tMgr.GetMethods(BindingFlags.Public | BindingFlags.Static))
                            if (m.Name == "HasNonAggressionPact" && m.GetParameters().Length == 3) { _mHasNap = m; break; }
                    }
                    var tAgr = AccessTools.TypeByName("Diplomacy.DiplomaticAction.DiplomaticAgreement");
                    if (tAgr != null) _mExpire = tAgr.GetMethod("Expire", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                }
                catch (Exception e) { Stumble("DipReady", e); }
            }
            if (_mNapApply == null || _mHasNap == null || _pDipInstance == null) return false;
            try { return _pDipInstance.GetValue(null, null) != null; } catch { return false; }
        }

        /// <summary>Pakt o nieagresji Diplomacy miedzy a i b (nie wygasly); null - brak.</summary>
        private static object NapOf(Kingdom a, Kingdom b)
        {
            if (!DipReady()) return null;
            var args = new object[] { a, b, null };
            object r = _mHasNap.Invoke(null, args);
            return r is bool && (bool)r ? args[2] : null;
        }

        private static CampaignTime PactsEndTime()
        {
            // dzien po smierci krola (pakty i tak gasimy sami przy smierci Roberta)
            return Campaign.Current.Models.CampaignTimeModel.CampaignStartTime + CampaignTime.Minutes((KingFallsDay + 1) * 24L * 60L);
        }

        /// <summary>Zaklada brakujace sojusze i pakty prologu (po rozstawieniu swiata i co dobe). Zwraca opis do linii logu.</summary>
        internal static string EnsurePacts(string why)
        {
            if (!Active || _pactsEnded || !PactsKey) return "-";
            double day = PrologueDay();
            if (day >= KingFallsDay) return "-";
            var crown = K(Crown);
            if (crown == null || crown.IsEliminated) return "brak krolestwa korony";
            var beh = Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>();
            var end = PactsEndTime();
            int newA = 0, newN = 0, haveA = 0, haveN = 0;
            var skip = new List<string>();
            if (beh != null)
                foreach (var id in AllyIds)
                {
                    var k = K(id);
                    if (k == null || k.IsEliminated) { skip.Add(id + " (brak)"); continue; }
                    if (crown.IsAtWarWith(k)) { skip.Add(id + " (wojna)"); continue; }
                    try
                    {
                        if (!beh.IsAllyWithKingdom(crown, k)) { beh.StartAlliance(crown, k); newA++; }
                        if (beh.IsAllyWithKingdom(crown, k))
                        {
                            haveA++; _ourAllies.Add(id);
                            if (_mUpdateEnd != null && beh is AllianceCampaignBehavior) _mUpdateEnd.Invoke(beh, new object[] { crown, k, end });
                        }
                    }
                    catch (Exception e) { Stumble("EnsurePacts.Alliance " + id, e); }
                }
            bool dip = DipReady();
            if (dip)
                foreach (var id in NapIds)
                {
                    var k = K(id);
                    if (k == null || k.IsEliminated) { skip.Add(id + " (brak)"); continue; }
                    if (crown.IsAtWarWith(k)) { skip.Add(id + " (wojna)"); continue; }
                    try
                    {
                        if (beh != null && beh.IsAllyWithKingdom(crown, k)) { haveN++; continue; }   // sojusz (np. AI) wystarcza - pakt zbedny
                        if (NapOf(crown, k) == null)
                        {
                            float days = (float)Math.Max(1.0, (end - CampaignTime.Now).ToDays);
                            _mNapApply.Invoke(null, new object[] { crown, k, false, true, (float?)days, false });
                            newN++;
                        }
                        if (NapOf(crown, k) != null) { haveN++; _ourNaps.Add(id); }
                    }
                    catch (Exception e) { Stumble("EnsurePacts.Nap " + id, e); }
                }
            string r = "sojusze " + haveA + "/" + AllyIds.Length + (newA > 0 ? " (nowe " + newA + ")" : "")
                       + ", pakty o nieagresji " + (dip ? haveN + "/" + NapIds.Length + (newN > 0 ? " (nowe " + newN + ")" : "") : "brak moda Diplomacy")
                       + (skip.Count > 0 ? ", pominiete " + string.Join(", ", skip.ToArray()) : "");
            if (newA > 0 || newN > 0) Log.Info(Tag + "pakty z krolestwem Roberta (" + why + "): " + r + ".");
            return r;
        }

        /// <summary>Koniec prologu (smierc krola): nasze sojusze zerwane, nasze pakty wygaszone. Raz.</summary>
        internal static string EndPacts()
        {
            if (_pactsEnded) return "juz zakonczone";
            _pactsEnded = true;
            int a = 0, n = 0;
            var crown = K(Crown);
            if (crown == null) return "brak krolestwa korony";
            var beh = Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>();
            if (beh != null)
                foreach (var id in _ourAllies.ToArray())
                {
                    var k = K(id);
                    try { if (k != null && beh.IsAllyWithKingdom(crown, k)) { beh.EndAlliance(crown, k); a++; } }
                    catch (Exception e) { Stumble("EndPacts.Alliance " + id, e); }
                }
            if (DipReady() && _mExpire != null)
                foreach (var id in _ourNaps.ToArray())
                {
                    var k = K(id);
                    try { var p = k != null ? NapOf(crown, k) : null; if (p != null) { _mExpire.Invoke(p, null); n++; } }
                    catch (Exception e) { Stumble("EndPacts.Nap " + id, e); }
                }
            return "sojusze zerwane " + a + ", pakty wygaszone " + n;
        }

        // ---------------------------------------------------------------- doba

        /// <summary>Wojny miedzy krolestwami Westeros (bez Wolnych Ludzi) - do linii doby (w prologu ma byc 0).</summary>
        private static string RealmWars(out int n)
        {
            n = 0;
            var ks = Kingdom.All.Where(k => k != null && !k.IsEliminated && IsRealm(k)).ToList();
            var l = new List<string>();
            for (int i = 0; i < ks.Count; i++)
                for (int j = i + 1; j < ks.Count; j++)
                    if (ks[i].IsAtWarWith(ks[j])) { n++; if (l.Count < 6) l.Add(ks[i].StringId + "-" + ks[j].StringId); }
            return n == 0 ? "0" : n + " (" + string.Join(", ", l.ToArray()) + ")";
        }

        private static string DailyPeace()
        {
            int n;
            string wars = RealmWars(out n);
            string pacts = EnsurePacts("doba");
            string s = "pokoj krola " + (PeaceOn ? "TAK" : "NIE") + " | wojny Westeros-Westeros (bez Wolnych Ludzi) " + wars
                       + " | zablokowane wypowiedzenia dzis " + _warsBlockedDay + " (razem " + _warsBlocked + ")"
                       + (_blockedToday.Count > 0 ? " [" + string.Join("; ", _blockedToday.ToArray()) + "]" : "")
                       + (_callsBlockedDay > 0 ? " | wezwania do wojny zablokowane " + _callsBlockedDay : "")
                       + " | pakty: " + (_pactsEnded ? "zakonczone" : pacts);
            _warsBlockedDay = 0; _callsBlockedDay = 0; _blockedToday.Clear();
            return s;
        }
    }
}
