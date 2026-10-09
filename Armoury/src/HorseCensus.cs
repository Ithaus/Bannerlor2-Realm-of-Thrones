using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// SPIS KONI AI - paczka 175.0 (decyzja Jeffa 09.10 pkt 2: "rycerz z koniem" - najpierw POMIAR, decyzja po pomiarze).
    /// Tylko liczy i pisze log; niczego w grze nie zmienia (wylacznik Army175Measure). Projekt: docs/PROJEKT-175-ARMIE-2026-10-09.md
    /// rozdz. 4.2 - 4.3. Liczniki doby wedlug krolestwa (party.MapFaction; partie bez krolestwa - "bez_krolestwa"):
    ///  - awanse AI na jezdzca (Stables.FilterTargets / PayInHorses): odrzucone z braku konia ("skret w inna droge", gdy zostal
    ///    inny cel, albo "czeka"), przyciete do liczby koni, zatrzymane przy zaplacie, wykonane wedlug kategorii konia, konie do
    ///    zbrojowni / przepadle (175.1), awanse z wolnych koni zbrojowni, konie z modyfikatorem;
    ///  - stan doby (petla MountedWage.Daily - jedna petla po partiach): konni / wszyscy w partiach lordow i zalogach AI, czeka na
    ///    konia (gotowi do awansu na jezdzca bez konia w taborze i wolnego w zbrojowni), konni bez konia w zbrojowni, wolne konie,
    ///    khuzait_footman (piesi Dothrakowie po 2.1 tylko ubywaja);
    ///  - zakupy Stajni AI (Stables.OnSettlementEntered): wizyty z potrzeba, kupione (targ / wsie), zloto, nieudane (brak zlota,
    ///    brak koni);
    ///  - naplyw konnych do partii lordow AI: ochotnicy (notabl), najemnicy (karczma), jency (RecruitPrisonersAi), inne -
    ///    prefiks na CampaignEventDispatcher.OnTroopRecruited (przed wszystkimi sluchaczami; echo ROT odciete znacznikiem);
    ///  - zamiany ROT (ROTTroopRecruiter.ExchangeClanTroops, prefiks + postfiks): pieszy -> konny (+) i konny -> pieszy (-),
    ///    plus wedlug zrodla zamienianego (jeniec, najemnik, obcy - inna kultura niz rod, swoj spoza puli), saldo zlota zamian
    ///    (ROT daje / bierze zloto z nikad przy zmianie tieru), straz konia Dothrakow (175.1b, RotHorseGuard);
    ///  - ochotnicy awansowani u notabla na konnego (VolunteerKit): z koniem kupionym / cofnieci.
    /// Linia dnia "Konie AI (175)" (suma swiata) + plik konie-krolestwa.csv (wiersz na krolestwo na dobe; licznik = doba,
    /// stan = stan doby). Bez zapisu w SyncData - liczniki od startu sesji (autotest to jedna sesja).
    /// </summary>
    internal static class HorseCensus
    {
        // kolumny wiersza krolestwa = kolejnosc w naglowku CSV (po "dzien;krolestwo")
        internal const int CKonni = 0, CWszyscy = 1, CCzeka = 2, COdrzucone = 3, CSkret = 4, CPrzyciete = 5, CWykonane = 6,
            CDoZbrojowni = 7, CPrzepadlo = 8, CKupione = 9, CZloto = 10, CNieudaneZloto = 11, CNieudaneBrak = 12, CBezKonia = 13,
            CNaplywOchotnicy = 14, CNaplywNajemnicy = 15, CNaplywJency = 16, CRotPlus = 17, CRotMinus = 18, CAwanse = 19,
            CWolne = 20, CAwanseZWolnych = 21, CStrazZbrojownia = 22, CStrazTabor = 23, CStrazPieszy = 24, CStrazT6 = 25,
            CRotPlusJeniec = 26, CRotPlusNajemnik = 27, CRotPlusObcy = 28, CRotZloto = 29, COchotnikKupiony = 30,
            COchotnikCofniety = 31, CFootmanDothrakow = 32, CKonModUjemny = 33,
            // dopisane za lista projektu (rozdz. 4.2 - te liczby sa w linii dnia)
            CZatrzymane = 34, CCzekaPartie = 35, CWykZwykly = 36, CWykBojowy = 37, CWykSzlachetny = 38, CRotPlusSwoj = 39,
            CNaplywInne = 40, CKonModDodatni = 41, CWizyty = 42, CKupioneTarg = 43, CKupioneWsie = 44, CStrazPrzepadlo = 45;
        private const int N = 46;
        private const string Header = "dzien;krolestwo;konni;wszyscy;czeka;odrzucone;skret;przyciete;wykonane;do_zbrojowni;przepadlo;kupione;zloto;"
            + "nieudane_zloto;nieudane_brak;bez_konia;naplyw_ochotnicy;naplyw_najemnicy;naplyw_jency;rot_plus;rot_minus;awanse;wolne_w_zbrojowni;"
            + "awanse_z_wolnych;straz_zbrojownia;straz_tabor;straz_pieszy;straz_t6;rot_plus_jeniec;rot_plus_najemnik;rot_plus_obcy;rot_zloto;"
            + "ochotnik_konny_kupiony;ochotnik_konny_cofniety;footman_dothrakow;kon_modyfikator;"
            + "zatrzymane;czeka_partie;wykonane_zwykly;wykonane_bojowy;wykonane_szlachetny;rot_plus_swoj;naplyw_inne;kon_modyfikator_dodatni;"
            + "wizyty_z_potrzeba;kupione_targ;kupione_wsie;straz_przepadlo";
        internal const string NoKingdom = "bez_krolestwa";

        private static readonly Dictionary<string, long[]> _day = new Dictionary<string, long[]>();
        private static long _volKhuzaitBought, _volKhuzaitReverted;   // ochotnicy-jezdzcy u notabli kultury khuzait (linia dnia)
        private static int _stumbles, _daysWritten;
        private static string _patches = "nie wpiete";

        internal static bool On { get { var s = Settings.Current; return s != null && s.Army175Measure; } }

        internal static void Reset()
        {
            _day.Clear(); _volKhuzaitBought = _volKhuzaitReverted = 0; _stumbles = 0; _daysWritten = 0;
            _inRot = 0; _inPrisoner = 0; _ctxOn = false; _ctxSettlement = null; _ctxSource = null;
            RotHorseGuard.Reset();
        }

        internal static string KeyOf(IFaction f) { var k = f as Kingdom; return k != null && k.StringId != null ? k.StringId : NoKingdom; }

        private static long[] Row(IFaction f)
        {
            string key = KeyOf(f);
            long[] r;
            if (!_day.TryGetValue(key, out r)) { r = new long[N]; _day[key] = r; }
            return r;
        }

        internal static void Add(IFaction f, int col, long v)
        {
            if (v == 0 || !On) return;
            try { Row(f)[col] += v; } catch { _stumbles++; }
        }

        private static IFaction FacOf(PartyBase p) { try { return p != null ? p.MapFaction : null; } catch { return null; } }

        // ------------------------------------------------------------ awanse (Stables)
        internal static void OnFiltered(PartyBase party, int refused, bool nowEmpty, int trimmed)
        {
            if (!On || party == null) return;
            try
            {
                var r = Row(FacOf(party));
                r[COdrzucone] += refused;
                // PROBY, nie ludzie: FilterTargets idzie co dobe i po bitwie; stan "czeka na konia" liczy petla doby (CountParty)
                if (refused > 0) { if (nowEmpty) _waitTries += refused; else r[CSkret] += refused; }
                r[CPrzyciete] += trimmed;
            }
            catch { _stumbles++; }
        }
        private static long _waitTries;   // proby "czeka" (lista celow pusta) - suma swiata do linii dnia

        internal static void OnPayRefused(PartyBase party, int need)
        {
            if (!On || party == null) return;
            try { Row(FacOf(party))[CZatrzymane] += need; } catch { _stumbles++; }
        }

        internal static void OnUpgradePaid(PartyBase party, ItemCategory cat, int n, int fromFree, int banked, int lost, int modNeg, int modPos)
        {
            if (!On || party == null || n <= 0) return;
            try
            {
                var r = Row(FacOf(party));
                r[CWykonane] += n;
                if (cat == DefaultItemCategories.NobleHorse) r[CWykSzlachetny] += n;
                else if (cat == DefaultItemCategories.WarHorse) r[CWykBojowy] += n;
                else r[CWykZwykly] += n;
                r[CAwanseZWolnych] += fromFree;
                r[CDoZbrojowni] += banked;
                r[CPrzepadlo] += lost;
                r[CKonModUjemny] += modNeg;
                r[CKonModDodatni] += modPos;
                var mp = party.MobileParty;
                if (mp != null && mp.IsLordParty && !mp.IsMainParty) r[CAwanse] += n;   // naplyw konnych do partii lordow: awanse
            }
            catch { _stumbles++; }
        }

        // ------------------------------------------------------------ zakupy Stajni AI
        internal static void OnLordVisit(MobileParty mp) { if (On && mp != null) Add(mp.MapFaction, CWizyty, 1); }
        internal static void OnLordBuyFailed(MobileParty mp, bool noGold) { if (On && mp != null) Add(mp.MapFaction, noGold ? CNieudaneZloto : CNieudaneBrak, 1); }
        internal static void OnLordBought(MobileParty mp, int bought, int gold, int fromMarket, int fromVillages)
        {
            if (!On || mp == null) return;
            try
            {
                var r = Row(mp.MapFaction);
                r[CKupione] += bought; r[CZloto] += gold; r[CKupioneTarg] += fromMarket; r[CKupioneWsie] += fromVillages;
            }
            catch { _stumbles++; }
        }

        // ------------------------------------------------------------ ochotnicy u notabli (VolunteerKit)
        internal static void OnVolunteerRider(Settlement st, Hero notable, bool bought)
        {
            if (!On) return;
            try
            {
                Add(st != null ? st.MapFaction : null, bought ? COchotnikKupiony : COchotnikCofniety, 1);
                if (notable != null && notable.Culture != null && notable.Culture.StringId == "khuzait")
                { if (bought) _volKhuzaitBought++; else _volKhuzaitReverted++; }
            }
            catch { _stumbles++; }
        }

        // ------------------------------------------------------------ stan doby (z petli MountedWage.Daily)
        internal static void CountParty(MobileParty mp)
        {
            if (mp == null || mp.IsMainParty || !(mp.IsLordParty || mp.IsGarrison)) return;
            try
            {
                var r = Row(mp.MapFaction);
                var roster = mp.MemberRoster;
                int men = 0, mounted = 0, footmen = 0;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var c = el.Character;
                    if (c == null || c.IsHero || el.Number <= 0) continue;
                    men += el.Number;
                    if (c.IsMounted) mounted += el.Number;
                    if (c.StringId == "khuzait_footman") footmen += el.Number;
                }
                r[CKonni] += mounted; r[CWszyscy] += men; r[CFootmanDothrakow] += footmen;
                int horses = Stables.ArmoryMounts(mp);
                r[CBezKonia] += Math.Max(0, mounted - horses);
                int free = Math.Max(0, horses - mounted);
                r[CWolne] += free;
                var s = Settings.Current;
                if (s != null && s.CavalryNeedsMounts)
                {
                    int need = Stables.NeedForUpgrades(mp.Party);
                    if (need > 0)
                    {
                        int have = Stables.CountAnyMounts(mp.Party) + (Stables.AiFix(mp.Party) ? free : 0);
                        int wait = need - have;
                        if (wait > 0) { r[CCzeka] += wait; r[CCzekaPartie]++; }
                    }
                }
            }
            catch { _stumbles++; }
        }

        // ------------------------------------------------------------ naplyw: werbunek (prefiks na dyspozytorze zdarzenia)
        [ThreadStatic] private static int _inRot;              // wewnatrz ROTTroopRecruiter.ExchangeClanTroops (echo werbunku ROT)
        [ThreadStatic] private static int _inPrisoner;         // wewnatrz RecruitPrisonersCampaignBehavior.RecruitPrisonersAi
        [ThreadStatic] private static bool _ctxOn;             // trwa zdarzenie werbunku (zewnetrzne) - jego miejsce i zrodlo
        [ThreadStatic] private static Settlement _ctxSettlement;
        [ThreadStatic] private static Hero _ctxSource;

        private sealed class Ctx { public bool On; public Settlement St; public Hero Src; }

        public static void RecruitedPrefix(Hero __0, Settlement __1, Hero __2, CharacterObject __3, int __4, out object __state)
        {
            __state = new Ctx { On = _ctxOn, St = _ctxSettlement, Src = _ctxSource };
            _ctxOn = true; _ctxSettlement = __1; _ctxSource = __2;
            if (_inRot > 0 || !On) return;                     // echo zamiany ROT - to nie nowy czlowiek
            try
            {
                if (__3 == null || __3.IsHero || !__3.IsMounted || __4 <= 0 || __0 == null) return;
                var mp = __0.PartyBelongedTo;
                if (mp == null || mp.IsMainParty || !mp.IsLordParty) return;
                int col = _inPrisoner > 0 ? CNaplywJency : __2 != null ? CNaplywOchotnicy : __1 != null ? CNaplywNajemnicy : CNaplywInne;
                Add(mp.MapFaction, col, __4);
            }
            catch { _stumbles++; }
        }

        public static Exception RecruitedFinalizer(Exception __exception, object __state)
        {
            var c = __state as Ctx;
            if (c != null) { _ctxOn = c.On; _ctxSettlement = c.St; _ctxSource = c.Src; }
            return __exception;
        }

        public static void PrisonerPrefix() { _inPrisoner++; }
        public static Exception PrisonerFinalizer(Exception __exception) { if (_inPrisoner > 0) _inPrisoner--; return __exception; }

        // ------------------------------------------------------------ zamiany ROT (ExchangeClanTroops)
        internal sealed class XState
        {
            public Dictionary<CharacterObject, int> Before;
            public int Gold; public bool HasOwner;
            public MobileParty Mp; public IFaction Fac; public int Src;   // Src: kolumna rot_plus wedlug zrodla
        }

        private static Dictionary<CharacterObject, int> Snapshot(TroopRoster r)
        {
            var d = new Dictionary<CharacterObject, int>();
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.Character == null || el.Character.IsHero) continue;
                int n; d.TryGetValue(el.Character, out n); d[el.Character] = n + el.Number;
            }
            return d;
        }

        /// <summary>Zrodlo czlowieka zamienianego przez ROT: jeniec, najemnik, obcy (inna kultura niz rod), swoj spoza puli.</summary>
        private static int SourceOf(Hero owner, CharacterObject troop, bool fireEvent, Settlement settlement)
        {
            if (_inPrisoner > 0) return CRotPlusJeniec;
            Clan cl = null;
            try { cl = settlement != null ? settlement.OwnerClan : owner != null ? owner.Clan : null; } catch { }
            var cult = cl != null ? cl.Culture : null;
            bool foreign = cult != null && troop.Culture != null && troop.Culture != cult;
            if (fireEvent && _ctxOn)
            {
                if (_ctxSource != null) return foreign ? CRotPlusObcy : CRotPlusSwoj;
                if (_ctxSettlement != null) return CRotPlusNajemnik;
            }
            if (troop.Occupation == Occupation.Mercenary) return CRotPlusNajemnik;
            return foreign ? CRotPlusObcy : CRotPlusSwoj;
        }

        public static void ExchangePrefix(Hero __0, TroopRoster __1, CharacterObject __2, int __3, bool __4, Settlement __5, out object __state)
        {
            __state = null;
            _inRot++;
            try
            {
                if ((!On && !RotHorseGuard.On) || __1 == null || __2 == null || __2.IsHero || __3 <= 0) return;
                MobileParty mp = null;
                if (__5 != null) { if (__5.Town != null) mp = __5.Town.GarrisonParty; }
                else if (__0 != null) mp = __0.PartyBelongedTo;
                if (mp == null) return;
                __state = new XState
                {
                    Before = Snapshot(__1), HasOwner = __0 != null, Gold = __0 != null ? __0.Gold : 0,
                    Mp = mp, Fac = mp.MapFaction, Src = SourceOf(__0, __2, __4, __5)
                };
            }
            catch { _stumbles++; __state = null; }
        }

        public static void ExchangePostfix(object __instance, Hero __0, TroopRoster __1, CharacterObject __2, Settlement __5, object __state)
        {
            var st = __state as XState;   // stan jako object - bez typow wewnetrznych w sygnaturze latki
            if (st == null) return;
            try
            {
                var after = Snapshot(__1);
                var added = new List<KeyValuePair<CharacterObject, int>>();
                foreach (var kv in after)
                {
                    if (kv.Key == __2) continue;
                    int b; st.Before.TryGetValue(kv.Key, out b);
                    if (kv.Value > b) added.Add(new KeyValuePair<CharacterObject, int>(kv.Key, kv.Value - b));
                }
                int plus = 0, minus = 0;
                foreach (var a in added)
                {
                    if (a.Key.IsMounted && !__2.IsMounted) plus += a.Value;
                    else if (!a.Key.IsMounted && __2.IsMounted) minus += a.Value;
                }
                if (On)
                {
                    var r = Row(st.Fac);
                    r[CRotPlus] += plus; r[CRotMinus] += minus;
                    if (plus > 0) r[st.Src] += plus;
                    if (st.HasOwner && __0 != null) r[CRotZloto] += __0.Gold - st.Gold;
                }
                if (plus > 0 && RotHorseGuard.On) RotHorseGuard.Apply(__instance, __0, __1, __2, __5, st.Mp, added, plus);
            }
            catch (Exception e) { _stumbles++; if (_stumbles <= 3) Log.Error("HorseCensus.ExchangePostfix", e); }
        }

        public static Exception ExchangeFinalizer(Exception __exception) { if (_inRot > 0) _inRot--; return __exception; }

        internal static void ApplyAll(Harmony h)
        {
            var parts = new List<string>();
            try
            {
                var disp = AccessTools.Method(typeof(CampaignEventDispatcher), "OnTroopRecruited",
                    new[] { typeof(Hero), typeof(Settlement), typeof(Hero), typeof(CharacterObject), typeof(int) });
                if (disp != null)
                {
                    h.Patch(disp, prefix: new HarmonyMethod(typeof(HorseCensus), nameof(RecruitedPrefix)) { priority = Priority.First },
                                  finalizer: new HarmonyMethod(typeof(HorseCensus), nameof(RecruitedFinalizer)));
                    parts.Add("werbunek wpiety");
                }
                else parts.Add("werbunek: BRAK CampaignEventDispatcher.OnTroopRecruited");
            }
            catch (Exception e) { parts.Add("werbunek: blad"); Log.Error("HorseCensus.ApplyAll(werbunek)", e); }
            try
            {
                var t = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.RecruitPrisonersCampaignBehavior");
                var m = t != null ? AccessTools.Method(t, "RecruitPrisonersAi") : null;
                if (m != null)
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(HorseCensus), nameof(PrisonerPrefix)),
                               finalizer: new HarmonyMethod(typeof(HorseCensus), nameof(PrisonerFinalizer)));
                    parts.Add("jency wpieci");
                }
                else parts.Add("jency: BRAK RecruitPrisonersAi");
            }
            catch (Exception e) { parts.Add("jency: blad"); Log.Error("HorseCensus.ApplyAll(jency)", e); }
            _h = h;
            try { parts.Add(TryRot() ? "zamiany ROT wpiete (straz konia Dothrakow " + (RotHorseGuard.Bound ? "gotowa" : "BEZ puli ROT - spi") + ")"
                                     : "zamiany ROT: BRAK ROTTroopRecruiter.ExchangeClanTroops (proba ponowna przy starcie kampanii)"); }
            catch (Exception e) { parts.Add("zamiany ROT: blad"); Log.Error("HorseCensus.ApplyAll(ROT)", e); }
            _patches = string.Join(", ", parts.ToArray());
            Log.Info("HorseCensus (175.0): spis koni AI - " + _patches + "; wylacznik Army175Measure (teraz " + (On ? "TAK" : "NIE")
                     + "), kon za awans AI do zbrojowni AiUpgradeHorseToArmory (175.1), straz konia Army175DothrakiHorseGuard (175.1b).");
        }

        private static Harmony _h;
        private static bool _rotHooked;

        /// <summary>Latka na ROTTroopRecruiter.ExchangeClanTroops (raz na proces; jak 171 RecruitSources - typ ROT moze jeszcze nie byc
        /// widoczny przy starcie, wtedy druga proba z OnSessionLaunched).</summary>
        private static bool TryRot()
        {
            if (_rotHooked || _h == null) return _rotHooked;
            var rot = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTTroopRecruiter");
            if (rot == null) return false;
            MethodInfo x = null;
            foreach (var m in AccessTools.GetDeclaredMethods(rot))
            {
                if (m.Name != "ExchangeClanTroops") continue;
                var ps = m.GetParameters();
                if (ps.Length == 6 && ps[0].ParameterType == typeof(Hero) && ps[1].ParameterType == typeof(TroopRoster)
                    && ps[2].ParameterType == typeof(CharacterObject) && ps[3].ParameterType == typeof(int)
                    && ps[4].ParameterType == typeof(bool) && ps[5].ParameterType == typeof(Settlement)) { x = m; break; }
            }
            if (x == null) return false;
            _h.Patch(x, prefix: new HarmonyMethod(typeof(HorseCensus), nameof(ExchangePrefix)),
                        postfix: new HarmonyMethod(typeof(HorseCensus), nameof(ExchangePostfix)),
                        finalizer: new HarmonyMethod(typeof(HorseCensus), nameof(ExchangeFinalizer)));
            RotHorseGuard.Bind(rot);
            _rotHooked = true;
            return true;
        }

        /// <summary>Z OnSessionLaunched: gdy typu ROT nie bylo przy starcie (kolejnosc ladowania) - jedna proba wpiecia latki ROT.</summary>
        internal static void ApplyLate()
        {
            if (_h == null || _rotHooked) return;
            bool ok = false;
            try { ok = TryRot(); } catch (Exception e) { Log.Error("HorseCensus.ApplyLate", e); }
            Log.Info("HorseCensus (175.0): zamiany ROT przy starcie kampanii " + (ok ? "wpiete (straz konia Dothrakow " + (RotHorseGuard.Bound ? "gotowa" : "BEZ puli ROT - spi") + ")"
                     : "BRAK ROTTroopRecruiter.ExchangeClanTroops - spis bez zamian ROT, straz konia spi") + ".");
        }

        // ------------------------------------------------------------ linia dnia i CSV
        internal static void Daily()
        {
            if (!On) { _day.Clear(); _waitTries = 0; _volKhuzaitBought = _volKhuzaitReverted = 0; return; }
            var inv = CultureInfo.InvariantCulture;
            int day = (int)CampaignTime.Now.ToDays;
            // kazde krolestwo ma wiersz (takze bez zdarzen)
            try { foreach (var k in Kingdom.All) if (k != null && !k.IsEliminated) Row(k); } catch { }
            var t = new long[N];
            foreach (var kv in _day) for (int i = 0; i < N; i++) t[i] += kv.Value[i];
            var sb = new StringBuilder();
            sb.Append("Konie AI (175): dzien ").Append(day)
              .Append(" | awanse na jezdzca: wykonane ").Append(t[CWykonane]).Append(" (zwykly ").Append(t[CWykZwykly]).Append(", bojowy ").Append(t[CWykBojowy])
              .Append(", szlachetny ").Append(t[CWykSzlachetny]).Append("; do zbrojowni ").Append(t[CDoZbrojowni]).Append(", przepadlo ").Append(t[CPrzepadlo])
              .Append(", z wolnych koni zbrojowni ").Append(t[CAwanseZWolnych]).Append(")")
              .Append(" | proby odrzucone z braku konia ").Append(t[COdrzucone]).Append(" (skret ").Append(t[CSkret]).Append(", czekaja ").Append(_waitTries)
              .Append("), przyciete ").Append(t[CPrzyciete]).Append(", zatrzymane przy zaplacie ").Append(t[CZatrzymane])
              .Append(" | stan: czeka na konia ").Append(t[CCzeka]).Append(" ludzi w ").Append(t[CCzekaPartie]).Append(" partiach, konni bez konia w zbrojowni ")
              .Append(t[CBezKonia]).Append(" z ").Append(t[CKonni]).Append(" konnych (").Append(t[CWszyscy]).Append(" ludzi w partiach lordow i zalogach AI), wolne konie w zbrojowniach ").Append(t[CWolne])
              .Append(" | zakupy lordow: ").Append(t[CKupione]).Append(" koni za ").Append(t[CZloto]).Append(" (targ ").Append(t[CKupioneTarg]).Append(", wsie ").Append(t[CKupioneWsie])
              .Append("; wizyt z potrzeba ").Append(t[CWizyty]).Append("), nieudane: brak zlota ").Append(t[CNieudaneZloto]).Append(", brak koni ").Append(t[CNieudaneBrak])
              .Append(" | naplyw konnych do partii lordow: ochotnicy ").Append(t[CNaplywOchotnicy]).Append(", najemnicy ").Append(t[CNaplywNajemnicy]).Append(", jency ").Append(t[CNaplywJency])
              .Append(", inne ").Append(t[CNaplywInne]).Append(", zamiany ROT +").Append(t[CRotPlus]).Append("/-").Append(t[CRotMinus]).Append(" (wszystkie partie AI), awanse ").Append(t[CAwanse])
              .Append(" | ROT +konni wedlug zrodla: jency ").Append(t[CRotPlusJeniec]).Append(", najemnicy ").Append(t[CRotPlusNajemnik]).Append(", obcy ").Append(t[CRotPlusObcy])
              .Append(", swoi spoza puli ").Append(t[CRotPlusSwoj]).Append("; saldo zlota zamian ROT ").Append(t[CRotZloto])
              .Append(" | straz konia Dothrakow (175.1b): ze zbrojowni ").Append(t[CStrazZbrojownia]).Append(", z taboru ").Append(t[CStrazTabor])
              .Append(", na pieszych ").Append(t[CStrazPieszy]).Append(", t6 bez zmiany ").Append(t[CStrazT6]).Append(", kon przepadl ").Append(t[CStrazPrzepadlo])
              .Append(" | ochotnicy na konnego u notabli: kupiony kon ").Append(t[COchotnikKupiony]).Append(", cofnieci ").Append(t[COchotnikCofniety])
              .Append(" (notable khuzait ").Append(_volKhuzaitBought).Append("/").Append(_volKhuzaitReverted).Append(")")
              .Append(" | khuzait_footman w partiach i zalogach ").Append(t[CFootmanDothrakow])
              .Append(" | konie z modyfikatorem do zbrojowni: gorsze ").Append(t[CKonModUjemny]).Append(", lepsze ").Append(t[CKonModDodatni])
              .Append(" | potkniecia ").Append(_stumbles).Append('.');
            Log.Info(sb.ToString());
            try
            {
                var rows = new StringBuilder();
                var keys = new List<string>(_day.Keys); keys.Sort(StringComparer.Ordinal);
                foreach (var k in keys)
                {
                    var r = _day[k];
                    rows.Append(day).Append(';').Append(k);
                    for (int i = 0; i < N; i++) rows.Append(';').Append(r[i].ToString(inv));
                    rows.Append(Environment.NewLine);
                }
                if (Log.Csv("konie-krolestwa.csv", Header, rows.ToString()) != null) _daysWritten++;
            }
            catch { _stumbles++; }
            _day.Clear(); _waitTries = 0; _volKhuzaitBought = _volKhuzaitReverted = 0;
        }
    }
}
