using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// E4/10 (167, plan kodu docs/etap4/PLAN-KODU-E410.md rozdz. 4): KARCZMA TYLKO Z WETERANOW - mechanika karczmy dla konta WETERANI (PeopleBook.Veterans.cs).
    /// L1 - prefiks bool na RecruitmentCampaignBehavior.UpdateCurrentMercenaryTroopAndCount: przy czynnej paczce gra nie dosypuje najemnikow (+10% co dobe) i nie losuje
    /// nowej paczki z niczego (start kampanii idzie przed siewem - gra wypelnia karczmy, to jest pula startowa). L2-L4 - okna najmu (partia AI w ApplyInternal przy
    /// MercenaryFromTavern; gracz w BuyMercenaries i buy_mercenaries_on_consequence). E1 - nasluch CampaignEvents.MercenaryNumberChangedInTown: ubytek nie przez nas = najem
    /// (jedna regula dla AI, karawan i gracza) -> PeopleBook.VetHire (FIFO z klasy pokazanej) -> udzialy do skladu partii (Give FVetHire).
    /// Raz na dobe Sync: w kazdym miescie (poza oblezonymi) klasa z najdluzej czekajacym czlowiekiem, liczba = wszyscy weterani tej klasy w miescie, typ = najemnik
    /// (Occupation.Mercenary - jedynego wynajmuja lordowie AI) z drzewa najemnikow kultury miasta o tym samym "konny/pieszy" i najblizszym tierze (zapas: drzewa wszystkich
    /// kultur); weteran pieszy nigdy jako konny (kon z niczego). Wlasne zmiany karczmy pod flaga _self. Brak L1 albo L2 - paczka nieczynna (karczma jak w grze).
    /// </summary>
    internal static class Veterans
    {
        // ================================================================ okna najmu i flaga "to my"
        [ThreadStatic] private static bool _self;
        [ThreadStatic] private static MobileParty _win;
        [ThreadStatic] private static bool _winOpen;
        public struct WinState { public MobileParty Party; public bool Open; }

        /// <summary>Trwa najem z karczmy w oknie (AI albo gracz) - E4/7 DeducePostfix milknie dla karczmy AI (Z8a).</summary>
        internal static bool InTavernHire { get { return _winOpen && _win != null; } }

        // L1: RecruitmentCampaignBehavior.UpdateCurrentMercenaryTroopAndCount(Town, bool) - przy czynnej paczce bez dosypki i bez losowania z niczego
        public static bool UpdatePrefix()
        {
            try { return !PeopleBook.VetLive; } catch { return true; }
        }

        // L2: RecruitmentCampaignBehavior.ApplyInternal(MobileParty side1Party, Settlement, Hero, CharacterObject, int number, int bitCode, RecruitingDetail detail)
        public static void ApplyPre(MobileParty __0, RecruitmentCampaignBehavior.RecruitingDetail __6, out WinState __state)
        {
            __state = new WinState { Party = _win, Open = _winOpen };
            _win = __6 == RecruitmentCampaignBehavior.RecruitingDetail.MercenaryFromTavern ? __0 : null;
            _winOpen = _win != null;
        }

        public static Exception WinFin(Exception __exception, WinState __state)
        {
            _win = __state.Party; _winOpen = __state.Open;
            return __exception;
        }

        // L3/L4: BuyMercenaries (rozmowa w karczmie), buy_mercenaries_on_consequence (menu zaulka) - partia gracza
        public static void PlayerPre(out WinState __state)
        {
            __state = new WinState { Party = _win, Open = _winOpen };
            _win = MobileParty.MainParty;
            _winOpen = _win != null;
        }

        // ================================================================ E1: najem
        /// <summary>CampaignEvents.MercenaryNumberChangedInTown: ubytek nie przez nas = najem z puli (FIFO z klasy pokazanej) -> udzialy do skladu partii okna.
        /// Przyrost nie przez nas - licznik "przyrost obcy" (oczekiwane 0; nastepna synchronizacja wyrowna do puli). Nigdy nie rzuca (zdarzenie gry w srodku najmu).</summary>
        internal static void OnTavernNumber(Town town, int oldNumber, int newNumber)
        {
            if (_self) return;
            try
            {
                if (town == null || !PeopleBook.VetLive) return;
                if (newNumber > oldNumber) { PeopleBook.VetNoteForeign(newNumber - oldNumber); return; }
                int n = oldNumber - newNumber;
                if (n <= 0) return;
                int key;
                var sh = PeopleBook.VetHire(town, n, out key);
                var party = _winOpen ? _win : null;
                CharacterObject type = null;
                try { var rcb = Rcb(); type = rcb != null ? rcb.GetMercenaryData(town).TroopType : null; } catch { type = null; }
                PeopleBook.VetNoteHire(party, n, type != null && type.IsMounted, type != null ? TierOf(type) : -1, key);
                if (party != null && party.MemberRoster != null) PeopleBook.Give(party.MemberRoster, sh, PeopleBook.FVetHire);
            }
            catch (Exception e) { PeopleBook.VetStumbleExt("OnTavernNumber", e); }
        }

        private static RecruitmentCampaignBehavior Rcb()
        {
            try { return Campaign.Current != null ? Campaign.Current.GetCampaignBehavior<RecruitmentCampaignBehavior>() : null; } catch { return null; }
        }

        // ================================================================ synchronizacja doby
        /// <summary>Raz na dobe (PeopleBook.VetDaily, po powrotach): karczma kazdego miasta = klasa z najdluzej czekajacym czlowiekiem (remis - wiecej ludzi), liczba = cala klasa.
        /// Miasto oblezone - bez zmian (czekaja w srodku). Miasto bez klas - liczba do 0 (typ zostaje, jak po wykupieniu w grze).</summary>
        internal static void Sync()
        {
            var rcb = Rcb();
            if (rcb == null) return;
            foreach (var town in Town.AllTowns)
            {
                try
                {
                    var st = town != null ? town.Settlement : null;
                    if (st == null || st.StringId == null || st.IsUnderSiege) continue;
                    var md = rcb.GetMercenaryData(town);
                    if (md == null) continue;
                    var cls = PeopleBook.VetClassesOf(st.StringId);
                    CharacterObject troop = null; int men = 0, key = -1; bool foot = false;
                    if (cls != null)
                    {
                        cls.Sort((a, b) => a[2] != b[2] ? a[2].CompareTo(b[2]) : b[1].CompareTo(a[1]));
                        foreach (var c in cls)
                        {
                            bool f;
                            var tt = TypeFor(st.Culture, c[0] >= 100, c[0] % 100, out f);
                            if (tt == null) { PeopleBook.VNoType++; continue; }
                            troop = tt; men = c[1]; key = c[0]; foot = f;
                            break;
                        }
                    }
                    if (troop == null)
                    {
                        if (md.Number > 0)
                        {
                            _self = true;
                            try { md.ChangeMercenaryCount(-md.Number); } finally { _self = false; }
                            PeopleBook.VClearTowns++;
                        }
                        PeopleBook.VetSetShown(st.StringId, -1);
                        continue;
                    }
                    if (md.TroopType != troop || md.Number != men)
                    {
                        _self = true;
                        try { md.ChangeMercenaryType(troop, men); } finally { _self = false; }
                    }
                    PeopleBook.VetSetShown(st.StringId, key);
                    PeopleBook.VShownTowns++;
                    if (troop.IsMounted) PeopleBook.VShownHorse++;
                    if (foot) PeopleBook.VFootForHorse++;
                }
                catch (Exception e) { PeopleBook.VetStumbleExt("Sync(miasto)", e); }
            }
        }

        // ================================================================ typ najemnika dla klasy (pamiec na sesje)
        private static readonly Dictionary<string, List<CharacterObject>> _cult = new Dictionary<string, List<CharacterObject>>();
        private static List<CharacterObject> _common;
        private static readonly Dictionary<CharacterObject, int> _tier = new Dictionary<CharacterObject, int>();
        private static readonly Dictionary<string, KeyValuePair<CharacterObject, bool>> _typeCache = new Dictionary<string, KeyValuePair<CharacterObject, bool>>();
        private static List<Town> _towns;
        private static int _townsDay = -1;
        private static readonly List<CharacterObject> NoTypes = new List<CharacterObject>();

        /// <summary>PeopleBook.Reset/Import: pamiec typow i miast od zera (obiekty gry nowej kampanii).</summary>
        internal static void ResetSession()
        {
            _cult.Clear(); _common = null; _tier.Clear(); _typeCache.Clear(); _towns = null; _townsDay = -1;
        }

        /// <summary>Tier oddzialu (CharacterObject.Tier z modelu gry; pamiec na sesje).</summary>
        internal static int TierOf(CharacterObject c)
        {
            if (c == null) return 0;
            int t;
            if (_tier.TryGetValue(c, out t)) return t;
            try { t = c.Tier; } catch { t = 0; }
            _tier[c] = t;
            return t;
        }

        private static bool IsOthersCulture(CultureObject c)
        {
            var id = c != null && c.StringId != null ? c.StringId : "";
            return id == "whitewalker" || id.Contains("walker") || id.Contains("wight");
        }

        /// <summary>Miasto Innych (kultura osady albo frakcji whitewalker) - ludzie tam nie czekaja (wracaja od razu) i nikt tam nie idzie.</summary>
        internal static bool IsOthersTown(Settlement st)
        {
            try
            {
                if (st == null) return false;
                if (IsOthersCulture(st.Culture)) return true;
                var f = st.MapFaction;
                return f != null && IsOthersCulture(f.Culture);
            }
            catch { return false; }
        }

        /// <summary>Najemnicy drzewa kultury: osiagalni przez UpgradeTargets z BasicMercenaryTroops, Occupation.Mercenary, bez bohaterow. Kultura Innych - brak (tylko zapas).</summary>
        private static List<CharacterObject> TreeOf(CultureObject cu)
        {
            if (cu == null || cu.StringId == null) return NoTypes;
            List<CharacterObject> l;
            if (_cult.TryGetValue(cu.StringId, out l)) return l;
            l = new List<CharacterObject>();
            try
            {
                if (!IsOthersCulture(cu) && cu.BasicMercenaryTroops != null)
                {
                    var seen = new HashSet<CharacterObject>();
                    var q = new Queue<CharacterObject>();
                    foreach (var b in cu.BasicMercenaryTroops) if (b != null && seen.Add(b)) q.Enqueue(b);
                    while (q.Count > 0)
                    {
                        var c = q.Dequeue();
                        if (!c.IsHero && c.Occupation == Occupation.Mercenary) l.Add(c);
                        if (c.UpgradeTargets != null) foreach (var u in c.UpgradeTargets) if (u != null && seen.Add(u)) q.Enqueue(u);
                    }
                }
            }
            catch (Exception e) { PeopleBook.VetStumbleExt("TreeOf", e); }
            _cult[cu.StringId] = l;
            return l;
        }

        /// <summary>Zapas wspolny: drzewa najemnikow wszystkich kultur.</summary>
        private static List<CharacterObject> Common()
        {
            if (_common != null) return _common;
            var l = new List<CharacterObject>();
            try
            {
                var seen = new HashSet<CharacterObject>();
                foreach (var cu in MBObjectManager.Instance.GetObjectTypeList<CultureObject>())
                    foreach (var c in TreeOf(cu)) if (seen.Add(c)) l.Add(c);
            }
            catch (Exception e) { PeopleBook.VetStumbleExt("Common", e); }
            _common = l;
            return l;
        }

        private static CharacterObject Pick(List<CharacterObject> l, bool horse, int tier)
        {
            CharacterObject best = null; int bd = int.MaxValue, bt = int.MaxValue;
            foreach (var c in l)
            {
                if (c == null || c.IsMounted != horse) continue;
                int t = TierOf(c), d = Math.Abs(t - tier);
                if (d < bd || (d == bd && t < bt)) { best = c; bd = d; bt = t; }     // remis - nizszy (bez awansu)
            }
            return best;
        }

        /// <summary>Typ najemnika dla klasy (konny, tier) w kulturze miasta: ten sam konny/pieszy, najmniejsza roznica tieru (remis - nizszy); brak w kulturze - zapas wspolny;
        /// konny bez zadnego konnego typu - pieszy typ (footForHorse). Pieszy nigdy jako konny. null - brak typu.</summary>
        internal static CharacterObject TypeFor(CultureObject cu, bool horse, int tier, out bool footForHorse)
        {
            footForHorse = false;
            string k = (cu != null && cu.StringId != null ? cu.StringId : "") + "|" + (horse ? 1 : 0) + "|" + tier;
            KeyValuePair<CharacterObject, bool> hit;
            if (_typeCache.TryGetValue(k, out hit)) { footForHorse = hit.Value; return hit.Key; }
            CharacterObject t = null;
            try
            {
                t = Pick(TreeOf(cu), horse, tier) ?? Pick(Common(), horse, tier);
                if (t == null && horse) { t = Pick(TreeOf(cu), false, tier) ?? Pick(Common(), false, tier); footForHorse = t != null; }
            }
            catch (Exception e) { PeopleBook.VetStumbleExt("TypeFor", e); t = null; }
            _typeCache[k] = new KeyValuePair<CharacterObject, bool>(t, footForHorse);
            return t;
        }

        /// <summary>Miasta (bez miast Innych) - lista raz na dobe.</summary>
        private static List<Town> Towns()
        {
            int day = 0;
            try { day = (int)CampaignTime.Now.ToDays; } catch { }
            if (_towns != null && _townsDay == day) return _towns;
            var l = new List<Town>();
            try { foreach (var t in Town.AllTowns) if (t != null && t.Settlement != null && t.Settlement.StringId != null && !IsOthersTown(t.Settlement)) l.Add(t); }
            catch (Exception e) { PeopleBook.VetStumbleExt("Towns", e); }
            _towns = l; _townsDay = day;
            return l;
        }

        /// <summary>Najblizsze miejscu `pos` miasto: nie oblezone (ludzie nie przechodza przez linie oblezenia - jak W4 166), nie Innych, z typem najemnika dla tej klasy. null - brak.</summary>
        internal static Town TownFor(Vec2 pos, bool horse, int tier)
        {
            Town best = null; float bd = float.MaxValue;
            foreach (var town in Towns())
            {
                try
                {
                    var st = town.Settlement;
                    if (st == null || st.IsUnderSiege) continue;
                    float d = pos.DistanceSquared(st.GetPosition2D);
                    if (d >= bd) continue;
                    bool f;
                    if (TypeFor(st.Culture, horse, tier, out f) == null) continue;
                    bd = d; best = town;
                }
                catch (Exception e) { PeopleBook.VetStumbleExt("TownFor", e); }
            }
            return best;
        }

        /// <summary>Opis typow do linii siewu i wczytania (V1: najnizszy tier zapasu = VeteranMinTier).</summary>
        internal static string TypesNote()
        {
            try
            {
                var cults = new HashSet<CultureObject>();
                foreach (var t in Town.AllTowns) if (t != null && t.Settlement != null && t.Settlement.Culture != null) cults.Add(t.Settlement.Culture);
                int foot = 0, horse = 0;
                foreach (var cu in cults)
                {
                    var l = TreeOf(cu);
                    if (l.Any(c => !c.IsMounted)) foot++;
                    if (l.Any(c => c.IsMounted)) horse++;
                }
                var com = Common();
                Func<bool, string> span = h =>
                {
                    var ts = com.Where(c => c.IsMounted == h).Select(c => TierOf(c)).ToList();
                    return ts.Count > 0 ? ts.Min() + "-" + ts.Max() : "brak";
                };
                return "kultur miast " + cults.Count + " (z wlasnym pieszym " + foot + ", z wlasnym konnym " + horse + "), zapas wspolny: piesi tiery " + span(false) + ", konni tiery " + span(true)
                       + " (" + com.Count + " typow)";
            }
            catch (Exception e) { PeopleBook.VetStumbleExt("TypesNote", e); return "blad"; }
        }

        // ================================================================ wpiecie (SubModuleMain, po PeopleBook.ApplyOrigin; latki wpiete zawsze - U26, dzialaja przy VetLive)
        internal static void ApplyAll(Harmony h)
        {
            var done = new List<string>(); var miss = new List<string>(); var owners = new List<string>();
            Func<MethodInfo, string, HarmonyMethod, HarmonyMethod, bool> wire = (m, label, pre, fin) =>
            {
                try
                {
                    if (m == null) { miss.Add(label); return false; }
                    h.Patch(m, prefix: pre, finalizer: fin);
                    done.Add(label);
                    try
                    {
                        var info = Harmony.GetPatchInfo(m);
                        if (info != null)
                        {
                            var o = info.Owners.Where(x => x != h.Id).Distinct().ToList();
                            owners.Add(m.Name + ": " + (o.Count > 0 ? string.Join(", ", o.ToArray()) : "-"));
                        }
                    }
                    catch { }
                    return true;
                }
                catch (Exception e) { miss.Add(label + " (" + e.GetType().Name + ")"); return false; }
            };
            Func<string, HarmonyMethod> P = n => new HarmonyMethod(typeof(Veterans), n);
            Func<string, int, HarmonyMethod> PP = (n, prio) => new HarmonyMethod(typeof(Veterans), n) { priority = prio };
            var rcbT = typeof(RecruitmentCampaignBehavior);
            bool l1 = wire(AccessTools.Method(rcbT, "UpdateCurrentMercenaryTroopAndCount", new[] { typeof(Town), typeof(bool) }), "karczma bez dosypki z niczego (UpdateCurrentMercenaryTroopAndCount)", P(nameof(UpdatePrefix)), null);
            bool l2 = wire(AccessTools.Method(rcbT, "ApplyInternal"), "okno najmu AI (ApplyInternal)", PP(nameof(ApplyPre), Priority.First), P(nameof(WinFin)));
            int pl = 0;
            if (wire(AccessTools.Method(rcbT, "BuyMercenaries"), "okno najmu gracza: BuyMercenaries", PP(nameof(PlayerPre), Priority.First), P(nameof(WinFin)))) pl++;
            if (wire(AccessTools.Method(rcbT, "buy_mercenaries_on_consequence"), "okno najmu gracza: buy_mercenaries_on_consequence", PP(nameof(PlayerPre), Priority.First), P(nameof(WinFin)))) pl++;
            PeopleBook.VetWired = l1 && l2;
            var s = Settings.Current;
            Log.Info("Weterani (E4/10): wpiete: " + string.Join(", ", done.ToArray()) + " (okno najmu gracza " + pl + "/2), nasluch ubytku karczmy (MercenaryNumberChangedInTown - ArmouryBehavior)"
                     + "; BRAK: " + (miss.Count > 0 ? string.Join(", ", miss.ToArray()) + (PeopleBook.VetWired ? "" : " - PACZKA NIECZYNNA (karczma jak w grze)") : "-")
                     + "; wlasciciele latek celow: " + (owners.Count > 0 ? string.Join("; ", owners.ToArray()) : "-")
                     + "; ustawienia startowe przed MCM (stan gry - linia dzienna): weterani " + (s != null && s.VeteransEnabled ? "TAK" : "NIE")
                     + ", udzial do karczmy " + (s != null ? s.VeteranShareToTavern.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "?")
                     + ", czekanie " + (s != null ? s.VeteranWaitDays : 0) + " dob, od tieru " + (s != null ? s.VeteranMinTier : 0) + ".");
        }
    }
}
