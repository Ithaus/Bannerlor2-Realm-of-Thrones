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
    /// rozdz. 4.2 - 4.3. Liczniki doby wedlug klucza partii (KeyOfParty): krolestwo partii lorda albo zalogi AI (party.MapFaction;
    /// bez krolestwa - "bez_krolestwa"), partie i zalogi rodu gracza osobno ("rod_gracza"), pozostale partie AI (karawany, tabory wsi,
    /// bandy, milicje, patrole) osobno ("inne_partie") - przeglad 175: wiersze krolestw to tylko AI lordowie i AI zalogi.
    ///  - awanse AI na jezdzca (Stables.FilterTargets / PayInHorses): odrzucone z braku konia ("skret w inna droge", gdy zostal
    ///    inny cel, albo "czeka" - takze wedlug krolestwa), przyciete do liczby koni, zatrzymane przy zaplacie, wykonane wedlug
    ///    kategorii konia, konie do zbrojowni / przepadle (175.1), w tym do zbrojowni nietrwalej (DTE jej nie zapisuje albo kasuje
    ///    co dobe), awanse z wolnych koni zbrojowni (175.1-wolne), konie z modyfikatorem;
    ///  - stan doby (petla MountedWage.Daily - jedna petla po partiach): konni / wszyscy w partiach lordow i zalogach AI, czeka na
    ///    konia WEDLUG KATEGORII (gotowi do awansu na jezdzca bez konia tej kategorii, ktorej zada awans: zwykly / bojowy /
    ///    szlachetny - w taborze, a z AiFreeArmoryHorsesFirst takze wolnego w zbrojowni), w tym czekajacy mimo koni innej kategorii,
    ///    konni bez konia w zbrojowni, wolne konie wedlug kategorii (Stables.Balance), khuzait_footman;
    ///  - przecieki wolnych koni (przeglad 175): konie uwolnione, gdy lord zostawia jezdzcow w zalodze (OnTroopGivenToSettlement)
    ///    i gdy jezdzcy dezerteruja (OnTroopsDeserted), konie, ktore przybyly do zbrojowni przy zamianie ROT (echo werbunku -
    ///    komplet wzorca z koniem z RecruitKit, z niczego);
    ///  - zakupy Stajni AI (Stables.OnSettlementEntered): wizyty z potrzeba, kupione (targ / wsie), zloto, nieudane (brak zlota,
    ///    brak koni = pusto / za drogie / polka zarezerwowana), wizyty bez zakupu, choc brakuje koni wlasciwej kategorii (tabor
    ///    ma dosc koni innej kategorii - zakupy sa bez kategorii jak przed 175);
    ///  - naplyw konnych do partii lordow AI: ochotnicy (notabl), najemnicy (karczma), jency (RecruitPrisonersAi), inne -
    ///    prefiks na CampaignEventDispatcher.OnTroopRecruited (przed wszystkimi sluchaczami; echo ROT odciete znacznikiem);
    ///  - zamiany ROT (ROTTroopRecruiter.ExchangeClanTroops, prefiks + postfiks): pieszy -> konny (+) i konny -> pieszy (-),
    ///    plus wedlug zrodla zamienianego (jeniec, najemnik, obcy - inna kultura niz rod, swoj spoza puli), saldo zlota zamian
    ///    (ROT daje / bierze zloto z nikad przy zmianie tieru), straz konia Dothrakow (175.1b, RotHorseGuard);
    ///  - ochotnicy awansowani u notabla na konnego (VolunteerKit): z koniem kupionym / cofnieci.
    /// Linia dnia "Konie AI (175)" (suma swiata) + plik konie-krolestwa.csv (wiersz na klucz na dobe; licznik = doba,
    /// stan = stan doby; nowe kolumny tylko na koncu). Bez zapisu w SyncData - liczniki od startu sesji (autotest to jedna sesja).
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
            CNaplywInne = 40, CKonModDodatni = 41, CWizyty = 42, CKupioneTarg = 43, CKupioneWsie = 44, CStrazPrzepadlo = 45,
            // przeglad 175 (tylko na koncu - skrypty czytaja po nazwie kolumny)
            CCzekaInnaKat = 46, CCzekajaProby = 47, CNieudanePusto = 48, CNieudaneDrogie = 49, CNieudaneRezerwa = 50, CWizytyInnaKat = 51,
            CDoZbrojowniNietrwalej = 52, CWolneZaloga = 53, CWolneDezercja = 54, CKonieEcho = 55, CStrazPieszyFootman = 56,
            CStrazPieszyElita = 57, CStrazBrakPuli = 58, CWolneZwykle = 59, CWolneBojowe = 60, CWolneSzlachetne = 61;
        private const int N = 62;
        private const string Header = "dzien;krolestwo;konni;wszyscy;czeka;odrzucone;skret;przyciete;wykonane;do_zbrojowni;przepadlo;kupione;zloto;"
            + "nieudane_zloto;nieudane_brak;bez_konia;naplyw_ochotnicy;naplyw_najemnicy;naplyw_jency;rot_plus;rot_minus;awanse;wolne_w_zbrojowni;"
            + "awanse_z_wolnych;straz_zbrojownia;straz_tabor;straz_pieszy;straz_t6;rot_plus_jeniec;rot_plus_najemnik;rot_plus_obcy;rot_zloto;"
            + "ochotnik_konny_kupiony;ochotnik_konny_cofniety;footman_dothrakow;kon_modyfikator;"
            + "zatrzymane;czeka_partie;wykonane_zwykly;wykonane_bojowy;wykonane_szlachetny;rot_plus_swoj;naplyw_inne;kon_modyfikator_dodatni;"
            + "wizyty_z_potrzeba;kupione_targ;kupione_wsie;straz_przepadlo;"
            + "czeka_mimo_koni_innej_kategorii;czekaja_proby;nieudane_pusto;nieudane_za_drogie;nieudane_polka_zarezerwowana;"
            + "wizyty_bez_zakupu_konie_innej_kategorii;do_zbrojowni_nietrwalej;wolne_po_jezdzcach_w_zalodze;wolne_po_dezerterach;"
            + "konie_z_echa_rot;straz_pieszy_footman;straz_pieszy_z_elity;straz_pieszego_brak_w_puli;wolne_zwykle;wolne_bojowe;wolne_szlachetne";
        internal const string NoKingdom = "bez_krolestwa", PlayerClanKey = "rod_gracza", OtherPartiesKey = "inne_partie";
        internal const int FailNoGold = 0, FailEmpty = 1, FailTooDear = 2, FailShelfReserved = 3;

        private static readonly Dictionary<string, long[]> _day = new Dictionary<string, long[]>();
        private static long _volKhuzaitBought, _volKhuzaitReverted;   // ochotnicy-jezdzcy u notabli kultury khuzait (linia dnia)
        private static int _stumbles, _daysWritten;
        private static string _patches = "nie wpiete";

        internal static bool On { get { var s = Settings.Current; return s != null && s.Army175Measure; } }

        internal static void Reset()
        {
            _day.Clear(); _volKhuzaitBought = _volKhuzaitReverted = 0; _stumbles = 0; _daysWritten = 0;
            _inRot = 0; _ctxOn = false; _ctxSettlement = null; _ctxSource = null;
            RotHorseGuard.Reset();
        }

        internal static string KeyOf(IFaction f) { var k = f as Kingdom; return k != null && k.StringId != null ? k.StringId : NoKingdom; }

        /// <summary>
        /// Klucz wiersza spisu dla partii (przeglad 175): AI lordowie i AI zalogi - krolestwo; rod gracza (jego partie i zalogi jego
        /// osad) - "rod_gracza"; pozostale partie AI (karawany, tabory wsi, bandy, milicje, patrole) - "inne_partie"; partia osady
        /// (awans po bitwie przy osadzie) - krolestwo osady.
        /// </summary>
        internal static string KeyOfParty(PartyBase p)
        {
            try
            {
                if (p == null) return NoKingdom;
                var mp = p.MobileParty;
                if (mp == null) return KeyOf(p.MapFaction);
                var pc = Clan.PlayerClan;
                if (pc != null)
                {
                    if (mp.ActualClan == pc) return PlayerClanKey;
                    if (mp.IsGarrison)
                    {
                        var st = mp.CurrentSettlement ?? mp.HomeSettlement;
                        if (st != null && st.OwnerClan == pc) return PlayerClanKey;
                    }
                }
                if (!(mp.IsLordParty || mp.IsGarrison)) return OtherPartiesKey;
                return KeyOf(mp.MapFaction);
            }
            catch { return NoKingdom; }
        }

        private static long[] Row(string key)
        {
            long[] r;
            if (!_day.TryGetValue(key ?? NoKingdom, out r)) { r = new long[N]; _day[key ?? NoKingdom] = r; }
            return r;
        }

        internal static void Add(IFaction f, int col, long v) { Add(KeyOf(f), col, v); }

        internal static void Add(string key, int col, long v)
        {
            if (v == 0 || !On) return;
            try { Row(key)[col] += v; } catch { _stumbles++; }
        }

        // ------------------------------------------------------------ awanse (Stables)
        internal static void OnFiltered(PartyBase party, int refused, bool nowEmpty, int trimmed)
        {
            if (!On || party == null) return;
            try
            {
                var r = Row(KeyOfParty(party));
                r[COdrzucone] += refused;
                // PROBY, nie ludzie: FilterTargets idzie co dobe i po bitwie; stan "czeka na konia" liczy petla doby (CountParty)
                if (refused > 0) { if (nowEmpty) r[CCzekajaProby] += refused; else r[CSkret] += refused; }
                r[CPrzyciete] += trimmed;
            }
            catch { _stumbles++; }
        }

        internal static void OnPayRefused(PartyBase party, int need)
        {
            if (!On || party == null) return;
            try { Row(KeyOfParty(party))[CZatrzymane] += need; } catch { _stumbles++; }
        }

        internal static void OnUpgradePaid(PartyBase party, ItemCategory cat, int n, int fromFree, int banked, int lost, int modNeg, int modPos)
        {
            if (!On || party == null || n <= 0) return;
            try
            {
                var r = Row(KeyOfParty(party));
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

        /// <summary>Przeglad 175: konie, ktore weszly do zbrojowni nietrwalej (DTE jej nie zapisuje albo kasuje co dobe - Stables.PersistentArmory).</summary>
        internal static void OnBanked(MobileParty mp, int banked)
        {
            if (!On || mp == null || banked <= 0) return;
            try { if (!Stables.PersistentArmory(mp)) Row(KeyOfParty(mp.Party))[CDoZbrojowniNietrwalej] += banked; } catch { _stumbles++; }
        }

        // ------------------------------------------------------------ zakupy Stajni AI
        internal static void OnLordVisit(MobileParty mp) { if (On && mp != null) Add(KeyOfParty(mp.Party), CWizyty, 1); }
        internal static void OnLordSkipOtherCategory(MobileParty mp) { if (On && mp != null) Add(KeyOfParty(mp.Party), CWizytyInnaKat, 1); }
        internal static void OnLordBuyFailed(MobileParty mp, int kind)
        {
            if (!On || mp == null) return;
            string key = KeyOfParty(mp.Party);
            if (kind == FailNoGold) { Add(key, CNieudaneZloto, 1); return; }
            Add(key, CNieudaneBrak, 1);   // suma trzech ponizej (kolumna z projektu 4.2)
            Add(key, kind == FailTooDear ? CNieudaneDrogie : kind == FailShelfReserved ? CNieudaneRezerwa : CNieudanePusto, 1);
        }
        internal static void OnLordBought(MobileParty mp, int bought, int gold, int fromMarket, int fromVillages)
        {
            if (!On || mp == null) return;
            try
            {
                var r = Row(KeyOfParty(mp.Party));
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

        // ------------------------------------------------------------ przecieki wolnych koni (przeglad 175)
        private static Dictionary<CharacterObject, int> MountedOf(TroopRoster r)
        {
            var d = new Dictionary<CharacterObject, int>();
            if (r == null) return d;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var c = el.Character;
                if (c == null || c.IsHero || !c.IsMounted || el.Number <= 0) continue;
                int n; d.TryGetValue(c, out n); d[c] = n + el.Number;
            }
            return d;
        }

        /// <summary>Ile koni zbrojowni stalo sie wolnych, bo jezdzcy odeszli zywi (wolne teraz minus wolne z nimi jeszcze w partii).</summary>
        private static void Freed(MobileParty mp, TroopRoster gone, int col)
        {
            if (!On || mp == null || mp.IsMainParty || gone == null) return;
            try
            {
                var d = MountedOf(gone);
                if (d.Count == 0) return;
                int after = Stables.Balance(mp).FreeAll;
                int before = Stables.Balance(mp, d).FreeAll;
                if (after > before) Row(KeyOfParty(mp.Party))[col] += after - before;
            }
            catch { _stumbles++; }
        }

        /// <summary>CampaignEvents.OnTroopGivenToSettlementEvent (GarrisonTroopsCampaignBehavior.LeaveTroopsToGarrison - lord zostawia ludzi
        /// w zalodze, takze jezdzcow): jezdziec dalej jezdzi w zalodze, a jego kon zostaje w zbrojowni lorda jako wolny.</summary>
        internal static void OnTroopGiven(Hero giver, Settlement st, TroopRoster roster)
        {
            try { if (giver != null) Freed(giver.PartyBelongedTo, roster, CWolneZaloga); } catch { _stumbles++; }
        }

        /// <summary>CampaignEvents.OnTroopsDesertedEvent (ludzie juz zdjeci z rostera): wedlug 160 kon jest wlasnoscia zolnierza,
        /// a zostaje w zbrojowni jako wolny.</summary>
        internal static void OnTroopsDeserted(MobileParty mp, TroopRoster deserted) { Freed(mp, deserted, CWolneDezercja); }

        // ------------------------------------------------------------ stan doby (z petli MountedWage.Daily)
        internal static void CountParty(MobileParty mp)
        {
            if (mp == null || mp.IsMainParty || !(mp.IsLordParty || mp.IsGarrison)) return;
            try
            {
                var r = Row(KeyOfParty(mp.Party));
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
                // przeglad 175: wolne wedlug kategorii (Stables.Balance - konie kategorii ponad jezdzcow, ktorym ta kategoria przysluguje)
                var bal = Stables.Balance(mp);
                r[CBezKonia] += bal.Unhorsed;
                r[CWolne] += bal.FreeAll;
                r[CWolneZwykle] += bal.Free[0]; r[CWolneBojowe] += bal.Free[1]; r[CWolneSzlachetne] += bal.Free[2];
                var s = Settings.Current;
                if (s != null && s.CavalryNeedsMounts)
                {
                    // przeglad 175: czeka WEDLUG KATEGORII (jak FilterTargets: kon tej kategorii w taborze, a z 175.1-wolne takze wolny
                    // tej kategorii w zbrojowni); kon innej kategorii nie pozwala na awans, wiec nie zmniejsza "czeka"
                    var needK = Stables.NeedByCategory(mp.Party);
                    if (needK[0] + needK[1] + needK[2] > 0)
                    {
                        bool ff = Stables.FreeFirst(mp.Party);
                        int wait = 0, covered = 0;
                        for (int k = 0; k < 3; k++)
                        {
                            if (needK[k] <= 0) continue;
                            int hk = Stables.CountInRoster(mp.Party, Stables.CatOfLevel(k)) + (ff ? bal.Free[k] : 0);
                            wait += Math.Max(0, needK[k] - hk);
                            covered += Math.Min(needK[k], hk);
                        }
                        if (wait > 0)
                        {
                            r[CCzeka] += wait; r[CCzekaPartie]++;
                            int any = Stables.CountAnyMounts(mp.Party) + (ff ? bal.FreeAll : 0);
                            r[CCzekaInnaKat] += Math.Min(wait, Math.Max(0, any - covered));   // czekaja, choc partia ma konie innej kategorii
                        }
                    }
                }
            }
            catch { _stumbles++; }
        }

        // ------------------------------------------------------------ naplyw: werbunek (prefiks na dyspozytorze zdarzenia)
        [ThreadStatic] private static int _inRot;              // wewnatrz ROTTroopRecruiter.ExchangeClanTroops (echo werbunku ROT)
        // sklad9 (175 pkt 19, scalenie z 171): "wewnatrz RecruitPrisonersAi" czyta RecruitSources.InPrisonerRecruit (jedna latka, jedna klasyfikacja);
        // _inRot zostaje - liczy kazda zamiane ROT (takze bez fireEvent), a prefiks i tak jest potrzebny do migawki rostera i strazy konia
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
                int col = RecruitSources.InPrisonerRecruit ? CNaplywJency : __2 != null ? CNaplywOchotnicy : __1 != null ? CNaplywNajemnicy : CNaplywInne;
                Add(KeyOfParty(mp.Party), col, __4);
            }
            catch { _stumbles++; }
        }

        public static Exception RecruitedFinalizer(Exception __exception, object __state)
        {
            var c = __state as Ctx;
            if (c != null) { _ctxOn = c.On; _ctxSettlement = c.St; _ctxSource = c.Src; }
            return __exception;
        }

        // ------------------------------------------------------------ zamiany ROT (ExchangeClanTroops)
        internal sealed class XState
        {
            public Dictionary<CharacterObject, int> Before;
            public int Gold; public bool HasOwner;
            public MobileParty Mp; public string Key; public int Src;   // Src: kolumna rot_plus wedlug zrodla
            public int ArmH = -1;                                        // konie pod siodlo w zbrojowni przed zamiana (echo ROT)
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
            if (RecruitSources.InPrisonerRecruit) return CRotPlusJeniec;
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
                    Mp = mp, Key = KeyOfParty(mp.Party), Src = SourceOf(__0, __2, __4, __5),
                    ArmH = On ? Stables.ArmoryMounts(mp) : -1
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
                    var r = Row(st.Key);
                    r[CRotPlus] += plus; r[CRotMinus] += minus;
                    if (plus > 0) r[st.Src] += plus;
                    if (st.HasOwner && __0 != null) r[CRotZloto] += __0.Gold - st.Gold;
                    // przeglad 175: konie, ktore przybyly do zbrojowni w trakcie zamiany (echo werbunku - komplet wzorca z koniem z RecruitKit)
                    if (st.ArmH >= 0) { int d = Stables.ArmoryMounts(st.Mp) - st.ArmH; if (d > 0) r[CKonieEcho] += d; }
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
            parts.Add("jency wedlug RecruitSources (171)");   // sklad9: bez drugiej latki na RecruitPrisonersAi
            _h = h;
            try { parts.Add(TryRot() ? "zamiany ROT wpiete (straz konia Dothrakow " + (RotHorseGuard.Bound ? "gotowa" : "BEZ puli ROT - spi") + ")"
                                     : "zamiany ROT: BRAK ROTTroopRecruiter.ExchangeClanTroops (proba ponowna przy starcie kampanii)"); }
            catch (Exception e) { parts.Add("zamiany ROT: blad"); Log.Error("HorseCensus.ApplyAll(ROT)", e); }
            _patches = string.Join(", ", parts.ToArray());
            Log.Info("HorseCensus (175.0): spis koni AI - " + _patches + "; wylacznik Army175Measure (teraz " + (On ? "TAK" : "NIE")
                     + "), kon za awans AI do zbrojowni AiUpgradeHorseToArmory (175.1), wolne konie najpierw AiFreeArmoryHorsesFirst (175.1-wolne), straz konia Army175DothrakiHorseGuard (175.1b).");
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

        /// <summary>
        /// Z OnSessionLaunched (przeglad 175, bieg bazowy B0): jedna linia ze STANEM wszystkich czesci 175 po stronie Armoury i znacznikow
        /// CrashScribe 175 (refleksja, CsMarks) - zeby log startu pokazal, czy bieg jest tym, czym ma byc (B0 = sam pomiar: AiUpgradeHorseToArmory,
        /// AiFreeArmoryHorsesFirst i Army175DothrakiHorseGuard NIE, NorthHomeEdgePercent 0). Klucze z kodu, ktorych nie ma w Armoury.json,
        /// maja wartosci domyslne - TAK przy 175.1, 175.1b i 175.4b.
        /// </summary>
        internal static void SessionStart()
        {
            try
            {
                CsMarks.Seal();
                var s = Settings.Current;
                if (s == null) return;
                Func<bool, string> yn = b => b ? "TAK" : "NIE";
                Log.Info("Armie 175 (Armoury) - wylaczniki przy starcie sesji: pomiar Army175Measure " + yn(s.Army175Measure)
                         + " | kon za awans AI do zbrojowni AiUpgradeHorseToArmory " + yn(s.AiUpgradeHorseToArmory)
                         + " (sklad9: takze przy TroopsFightWithOwnKitOnly " + yn(s.TroopsFightWithOwnKitOnly) + " - dziala: " + yn(s.AiUpgradeHorseToArmory || s.TroopsFightWithOwnKitOnly) + ")"
                         + " | wolne konie zbrojowni najpierw AiFreeArmoryHorsesFirst " + yn(s.AiFreeArmoryHorsesFirst)
                         + " | straz konia Dothrakow Army175DothrakiHorseGuard " + yn(s.Army175DothrakiHorseGuard) + " (dziala: " + yn(RotHorseGuard.On)
                         + "; CS 2.1 piesi w puli ROT: " + CsMarks.Describe("DothrakiPoolActive") + ")"
                         + " | przewaga Polnocy NorthHomeEdgePercent " + NorthHomeEdge.Percent + "%"
                         + " | wzorzec bitwy DTE w granicy tieru (CS TierGearApplied): " + CsMarks.Describe("TierGearApplied")
                         + " | Zlota Kompania Army175GoldenBows " + yn(s.Army175GoldenBows)
                         + " | CavalryNeedsMounts " + yn(s.CavalryNeedsMounts) + ", AiBuysMounts " + yn(s.AiBuysMounts) + ".");
            }
            catch (Exception e) { Log.Error("HorseCensus.SessionStart", e); }
        }

        // ------------------------------------------------------------ linia dnia i CSV
        internal static void Daily()
        {
            if (!On) { _day.Clear(); _volKhuzaitBought = _volKhuzaitReverted = 0; return; }
            var inv = CultureInfo.InvariantCulture;
            int day = (int)CampaignTime.Now.ToDays;
            // kazde krolestwo ma wiersz (takze bez zdarzen)
            try { foreach (var k in Kingdom.All) if (k != null && !k.IsEliminated) Row(KeyOf(k)); } catch { }
            var t = new long[N];
            foreach (var kv in _day) for (int i = 0; i < N; i++) t[i] += kv.Value[i];
            var sb = new StringBuilder();
            sb.Append("Konie AI (175): dzien ").Append(day)
              .Append(" | awanse na jezdzca: wykonane ").Append(t[CWykonane]).Append(" (zwykly ").Append(t[CWykZwykly]).Append(", bojowy ").Append(t[CWykBojowy])
              .Append(", szlachetny ").Append(t[CWykSzlachetny]).Append("; do zbrojowni ").Append(t[CDoZbrojowni]).Append(" (w tym nietrwalej ").Append(t[CDoZbrojowniNietrwalej])
              .Append("), przepadlo ").Append(t[CPrzepadlo]).Append(", z wolnych koni zbrojowni ").Append(t[CAwanseZWolnych]).Append(")")
              .Append(" | proby odrzucone z braku konia ").Append(t[COdrzucone]).Append(" (skret ").Append(t[CSkret]).Append(", czekaja ").Append(t[CCzekajaProby])
              .Append("), przyciete ").Append(t[CPrzyciete]).Append(", zatrzymane przy zaplacie ").Append(t[CZatrzymane])
              .Append(" | stan: czeka na konia swojej kategorii ").Append(t[CCzeka]).Append(" ludzi w ").Append(t[CCzekaPartie]).Append(" partiach (w tym mimo koni innej kategorii ")
              .Append(t[CCzekaInnaKat]).Append("), konni bez konia w zbrojowni ")
              .Append(t[CBezKonia]).Append(" z ").Append(t[CKonni]).Append(" konnych (").Append(t[CWszyscy]).Append(" ludzi w partiach lordow i zalogach AI), wolne konie w zbrojowniach ").Append(t[CWolne])
              .Append(" (zwykle ").Append(t[CWolneZwykle]).Append(", bojowe ").Append(t[CWolneBojowe]).Append(", szlachetne ").Append(t[CWolneSzlachetne]).Append(")")
              .Append(" | przecieki wolnych koni: jezdzcy zostawieni w zalodze ").Append(t[CWolneZaloga]).Append(", dezerterzy ").Append(t[CWolneDezercja])
              .Append(", konie z echa werbunku ROT ").Append(t[CKonieEcho])
              .Append(" | zakupy lordow: ").Append(t[CKupione]).Append(" koni za ").Append(t[CZloto]).Append(" (targ ").Append(t[CKupioneTarg]).Append(", wsie ").Append(t[CKupioneWsie])
              .Append("; wizyt z potrzeba ").Append(t[CWizyty]).Append(", bez zakupu przy koniach innej kategorii w taborze ").Append(t[CWizytyInnaKat])
              .Append("), nieudane: brak zlota ").Append(t[CNieudaneZloto]).Append(", brak koni ").Append(t[CNieudaneBrak])
              .Append(" (pusto ").Append(t[CNieudanePusto]).Append(", za drogie ").Append(t[CNieudaneDrogie]).Append(", polka zarezerwowana ").Append(t[CNieudaneRezerwa]).Append(")")
              .Append(" | naplyw konnych do partii lordow: ochotnicy ").Append(t[CNaplywOchotnicy]).Append(", najemnicy ").Append(t[CNaplywNajemnicy]).Append(", jency ").Append(t[CNaplywJency])
              .Append(", inne ").Append(t[CNaplywInne]).Append(", zamiany ROT +").Append(t[CRotPlus]).Append("/-").Append(t[CRotMinus]).Append(" (wszystkie partie AI), awanse ").Append(t[CAwanse])
              .Append(" | ROT +konni wedlug zrodla: jency ").Append(t[CRotPlusJeniec]).Append(", najemnicy ").Append(t[CRotPlusNajemnik]).Append(", obcy ").Append(t[CRotPlusObcy])
              .Append(", swoi spoza puli ").Append(t[CRotPlusSwoj]).Append("; saldo zlota zamian ROT ").Append(t[CRotZloto])
              .Append(" | straz konia Dothrakow (175.1b): ze zbrojowni ").Append(t[CStrazZbrojownia]).Append(", z taboru ").Append(t[CStrazTabor])
              .Append(", na pieszych ").Append(t[CStrazPieszy]).Append(" (w tym na khuzait_footman ").Append(t[CStrazPieszyFootman]).Append(", z linii elity ").Append(t[CStrazPieszyElita])
              .Append("), t6 bez zmiany ").Append(t[CStrazT6]).Append(", pieszego brak w puli ").Append(t[CStrazBrakPuli]).Append(", kon przepadl ").Append(t[CStrazPrzepadlo])
              .Append(" | ochotnicy na konnego u notabli: kupiony kon ").Append(t[COchotnikKupiony]).Append(", cofnieci ").Append(t[COchotnikCofniety])
              .Append(" (notable khuzait ").Append(_volKhuzaitBought).Append("/").Append(_volKhuzaitReverted).Append(")")
              .Append(" | khuzait_footman w partiach i zalogach ").Append(t[CFootmanDothrakow])
              .Append(" | konie z modyfikatorem do zbrojowni: gorsze ").Append(t[CKonModUjemny]).Append(", lepsze ").Append(t[CKonModDodatni])
              .Append(" | wiersze: krolestwa (AI lordowie i zalogi), ").Append(PlayerClanKey).Append(", ").Append(OtherPartiesKey)
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
            _day.Clear(); _volKhuzaitBought = _volKhuzaitReverted = 0;
        }
    }

    /// <summary>
    /// ZNACZNIKI CRASHSCRIBE 175 (przeglad 175): Armoury czyta refleksja pola CrashScribe.Army175, ktore CS ustawia przy wczytaniu
    /// (AfterRegisterSubModuleObjects) - DothrakiPoolActive (2.1: Dothrakowie konni, piesi dopisani do puli ROT) i TierGearApplied (1.1:
    /// wzorce w granicy tieru). Dzieki temu straz konia i sufit wzorca DTE dzialaja dokladnie wtedy, gdy CS zmienil dane przy tym
    /// wczytaniu - nie wedlug biezacego MCM (CS stosuje zmiany "od nastepnego wczytania"). Brak typu albo pola (CS bez 175, bieg B0
    /// na zatwierdzonym CS) = NIE. Punkt styku z drzewem a175cs: nazw tych pol nie zmieniac.
    /// </summary>
    internal static class CsMarks
    {
        private static Type _t;
        private static bool _sealed;
        private static readonly Dictionary<string, FieldInfo> _f = new Dictionary<string, FieldInfo>();

        private static Type T()
        {
            if (_t != null || _sealed) return _t;
            _sealed = true;   // jedno szukanie na proces: zestawy modulow sa zaladowane przed kampania, a pola czyta sie tylko w kampanii
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = null;
                    try { t = asm.GetType("CrashScribe.Army175", false); } catch { }
                    if (t != null) { _t = t; break; }
                }
            }
            catch { }
            return _t;
        }

        /// <summary>Ze startu sesji: szukanie typu (jesli jeszcze nie bylo); brak typu zostaje brakiem.</summary>
        internal static void Seal() { T(); }

        /// <summary>Wartosc pola bool CrashScribe.Army175; null - CS bez 175.</summary>
        internal static bool? Get(string field)
        {
            try
            {
                var t = T();
                if (t == null) return null;
                FieldInfo fi;
                if (!_f.TryGetValue(field, out fi))
                {
                    fi = t.GetField(field, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    _f[field] = fi;
                }
                if (fi == null) return null;
                var v = fi.GetValue(null);
                if (v is bool) return (bool)v;
                return null;
            }
            catch { return null; }
        }

        internal static string Describe(string field)
        {
            var v = Get(field);
            return v == null ? "brak (CrashScribe bez 175)" : v.Value ? "TAK" : "NIE";
        }

        internal static bool DothrakiPoolActive { get { return Get("DothrakiPoolActive") == true; } }
        internal static bool TierGearApplied { get { return Get("TierGearApplied") == true; } }
    }
}
