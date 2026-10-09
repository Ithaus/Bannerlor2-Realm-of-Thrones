using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace CrashScribe
{
    /// <summary>
    /// INNI BEZ DOSYPKI Z NICZEGO (T2b noc 08/09.10; Jeff 08.10 "Nieumarli za szybko rosna"; decyzja Jeffa 07.10 H2
    /// "Inni tylko z poleglych", kanon: jency Innych wstaja jako trupy).
    ///
    /// Skad Inni dzis rosna (pomiar 05.10 w STAN-PRAC i audyt docs/audyt-2026-10-09/06-nieumarli.md, dekompilacja ROT 8.1.8):
    ///  1. ROT.CampaignBehaviors.ROTOthersCampaignBehavior.OnMobilePartyCreated(MobileParty party) (MobilePartyCreated):
    ///     kazda nowa partia klanu Innych (ActualClan == ROTClans.WhiteWalkers) dostaje AddToCounts(_wight, 100).
    ///     Narodziny bandy = 53 z szablonu lorda + 100 stad + wodz = 154. Z NICZEGO.
    ///  2. ROTOthersCampaignBehavior.OnDailyTickParty(MobileParty party) (DailyTickPartyEvent): banda lorda Innych (nie gracz)
    ///     z wolnym miejscem > 2 dostaje AddToCounts(_wight, 2) co dobe - do pelnego limitu (ok. 488). Z NICZEGO.
    ///  3. Gra: RecruitmentCampaignBehavior.HourlyTickParty - druzyna lorda mniejszej frakcji poza osada (Inni nie maja krolestwa)
    ///     z szansa 5% na godzine bierze 3-7 BasicTroop "ochotnikow z mapy" (ApplyInternal z RecruitingDetail.VolunteerFromMap,
    ///     bez osady i bez notabla; zloto przywodcy idzie do nikogo). Ok. 10 dziennie. Z NICZEGO.
    ///  ZOSTAJE (wedlug kanonu): nekromancja ROT po bitwie (OnMapEventEnded - z pokonanych i z jencow, ktorych ROT kasuje)
    ///  oraz odbici jency. ZOSTAJE TEZ (wersja minimalna, dalej "z niczego" do zamkniecia z ksiega ludzi 108): szablon 53
    ///  i wodz nowej bandy (gra: HeroSpawnCampaignBehavior / LordPartyComponent) oraz rajd ROT (OnRaidCompleted: Hearth / 2
    ///  trupow; dla rodu bez krolestwa praktycznie nieosiagalny - audyt 05.10).
    ///
    /// Latki (prefiksy Harmony):
    ///  - OnMobilePartyCreated: partia klanu ROTClans.WhiteWalkers (StringId ROTclan_126 - ten sam warunek co ROT:1301)
    ///    z _wight != null - oryginal nie biegnie (licznik +100, osobno bandy lordow i inne partie klanu: bandyci z kryjowek,
    ///    partie niestandardowe). Cudzy prefiks BKROTPatch (ROTOthersCampaignBehaviorNullPatch: _wight jeszcze null)
    ///    zostaje - przy _wight == null przepuszczamy i nic nie liczymy.
    ///    WYJATEK - BILANS OTWARCIA (Jeff 07.10, STAN-PRAC pytanie 2: "616 dawnych umarlych na start - TAK"; projekt R2,
    ///    REGULY-KRAIN-I-DLUGU 4.2 A pkt 1): w pierwszych OthersStartDowryDays dobach kampanii (domyslnie 2) +100 przechodzi
    ///    i liczy sie osobno jako "bilans otwarcia" - 4 bandy startu po 154 = 616, inaczej Zew (cel 520) i Pochod (prog 500)
    ///    nie mialyby z czego ruszyc. Do czasu puli cial (R4). W kampanii starszej niz te doby wyjatek nie dziala.
    ///  - OnDailyTickParty: partia Innych - oryginal nie biegnie (licznik +2, gdy warunek ROT bylby spelniony).
    ///  - RecruitmentCampaignBehavior.ApplyInternal (nie GetRecruitVolunteerFromMap - jednolinijkowa, JIT moze ja wkleic
    ///    w HourlyTickParty i latka by nie dzialala): VolunteerFromMap dla klanu Innych - nic nie zachodzi (ani ludzi, ani
    ///    zlota do nikad, ani zdarzenia OnTroopRecruited). Ta sama metoda ma prefiks Armoury SpoilsCompany (tylko klan Spoils)
    ///    i postfiksy BK (settlement == null - nic) i Armoury LevyGold (tylko VolunteerFromIndividual / MercenaryFromTavern).
    ///    Ochotnicy od notabli w osadach Innych (VolunteerFromIndividual) - bez zmian (prawdziwi ludzie z osady).
    /// Wylaczniki w ModuleData/CrashScribe.settings.xml: OthersNoFreeGrowth (calosc) i po jednym na droge.
    /// Bez zapisu w grze; bandy, ktore juz sa w polu, zachowuja swoich ludzi.
    /// </summary>
    internal static class OthersGrowth
    {
        // liczniki od ostatniej linii dobowej
        private static int _births, _birthWights, _birthsOther;   // _births = bandy lordow, _birthsOther = inne partie klanu Innych
        private static int _openTimes;                             // bilans otwarcia (+100 przepuszczone na starcie kampanii)
        private static int _dailyTimes, _dailyWights;
        private static int _mapTimes, _mapMen;
        // od wczytania (sesja kampanii)
        private static long _totalWights;
        private static int _stumbles, _stumblesDay;

        private static bool IsOthers(Clan c)
        {
            return c != null && c.Culture != null && c.Culture.StringId == "whitewalker";   // to samo co ROT Extensions.IsWhiteWalker
        }

        private const string OthersClanId = "ROTclan_126";   // ROT ROTClans.WhiteWalkers => GetClanByID("ROTclan_126")
        private static System.Reflection.FieldInfo _wightField;   // ROTOthersCampaignBehavior._wight (BKROTPatch: null = ROT nic nie da)

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_stumbles == 1) { try { Scribe.Report("CrashScribe", e, where, null); } catch { } }
        }

        internal static void Install(Harmony harmony)
        {
            try
            {
                if (!Config.OthersNoFreeGrowth)
                {
                    Scribe.Line("Inni bez dosypki: wylaczone (OthersNoFreeGrowth = false) - Inni rosna jak w ROT (154 przy narodzinach, +2 dziennie, ochotnicy z mapy).");
                    return;
                }
                var parts = new System.Collections.Generic.List<string>();
                int hooked = 0;

                var tOth = Type.GetType("ROT.CampaignBehaviors.ROTOthersCampaignBehavior, ROT");
                if (tOth == null) Scribe.Line("Inni bez dosypki: ROT nieobecny (brak ROTOthersCampaignBehavior) - narodziny i +2 dziennie bez latki.");

                // 1. +100 przy narodzinach bandy
                if (!Config.OthersNoBirthDowry) parts.Add("narodziny +100 zostawione (OthersNoBirthDowry = false)");
                else
                {
                    var m = tOth != null ? AccessTools.Method(tOth, "OnMobilePartyCreated", new[] { typeof(MobileParty) }) : null;
                    if (m == null) parts.Add("narodziny +100 NIEZNALEZIONE");
                    else
                    {
                        _wightField = AccessTools.Field(tOth, "_wight");
                        try
                        {
                            harmony.Patch(m, prefix: new HarmonyMethod(typeof(OthersGrowth), nameof(BirthPrefix))); hooked++;
                            parts.Add("narodziny +100 wpiete" + (Config.OthersStartDowryDays > 0
                                ? " (bilans otwarcia: +100 przepuszczone w pierwszych " + Config.OthersStartDowryDays + " dobach kampanii)"
                                : " (bez bilansu otwarcia, OthersStartDowryDays = 0)"));
                        }
                        catch (Exception e) { parts.Add("narodziny +100 BLAD"); Stumble("OthersGrowth.Install(birth)", e); }
                    }
                }

                // 2. +2 trupy dziennie na bande
                if (!Config.OthersNoDailyWights) parts.Add("+2 dziennie zostawione (OthersNoDailyWights = false)");
                else
                {
                    var m = tOth != null ? AccessTools.Method(tOth, "OnDailyTickParty", new[] { typeof(MobileParty) }) : null;
                    if (m == null) parts.Add("+2 dziennie NIEZNALEZIONE");
                    else
                    {
                        try { harmony.Patch(m, prefix: new HarmonyMethod(typeof(OthersGrowth), nameof(DailyPrefix))); hooked++; parts.Add("+2 dziennie wpiete"); }
                        catch (Exception e) { parts.Add("+2 dziennie BLAD"); Stumble("OthersGrowth.Install(daily)", e); }
                    }
                }

                // 3. ochotnicy z mapy
                if (!Config.OthersNoMapVolunteers) parts.Add("ochotnicy z mapy zostawieni (OthersNoMapVolunteers = false)");
                else
                {
                    var m = AccessTools.Method(typeof(RecruitmentCampaignBehavior), "ApplyInternal");
                    if (m == null) parts.Add("ochotnicy z mapy NIEZNALEZIONE");
                    else
                    {
                        try { harmony.Patch(m, prefix: new HarmonyMethod(typeof(OthersGrowth), nameof(MapVolunteerPrefix))); hooked++; parts.Add("ochotnicy z mapy wpieci"); }
                        catch (Exception e) { parts.Add("ochotnicy z mapy BLAD"); Stumble("OthersGrowth.Install(map)", e); }
                    }
                }

                Scribe.Line("Inni bez dosypki: " + string.Join(", ", parts.ToArray()) + " - wpiete " + hooked + "/3. Zostaje: nekromancja z poleglych i odbici jency"
                            + " (kanon), szablon 53 + wodz nowej bandy (dalej z niczego - do zamkniecia z ksiega ludzi 108).");
            }
            catch (Exception e) { Stumble("OthersGrowth.Install", e); }
        }

        /// <summary>Prefiks ROTOthersCampaignBehavior.OnMobilePartyCreated: nowa partia Innych bez +100 trupow
        /// (poza bilansem otwarcia w pierwszych dobach kampanii).</summary>
        public static bool BirthPrefix(object __instance, MobileParty __0)
        {
            try
            {
                if (!Config.OthersNoFreeGrowth || !Config.OthersNoBirthDowry) return true;
                // tylko gdy ROT naprawde by dosypal: klan dokladnie ROTClans.WhiteWalkers i _wight juz jest
                if (__0 == null || __0.ActualClan == null || __0.ActualClan.StringId != OthersClanId) return true;
                if (_wightField != null && __instance != null && _wightField.GetValue(__instance) == null) return true;
                if (Config.OthersStartDowryDays > 0)
                {
                    float elapsed = Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow;
                    if (elapsed < Config.OthersStartDowryDays) { _openTimes++; return true; }   // bilans otwarcia (Jeff 07.10: 616 na start)
                }
                if (__0.IsLordParty) _births++; else _birthsOther++;
                _birthWights += 100; _totalWights += 100;
                return false;
            }
            catch (Exception e) { Stumble("OthersGrowth.Birth", e); return true; }
        }

        /// <summary>Prefiks ROTOthersCampaignBehavior.OnDailyTickParty: banda Innych bez +2 trupow na dobe.</summary>
        public static bool DailyPrefix(MobileParty __0)
        {
            try
            {
                if (!Config.OthersNoFreeGrowth || !Config.OthersNoDailyWights) return true;
                if (__0 == null || !IsOthers(__0.ActualClan)) return true;
                // licznik: tylko gdy ROT naprawde by dosypal (ten sam warunek co ROT:1309)
                if (!__0.IsMainParty && __0.IsLordParty && __0.Party != null
                    && (1f - __0.PartySizeRatio) * (float)__0.Party.PartySizeLimit > 2f)
                { _dailyTimes++; _dailyWights += 2; _totalWights += 2; }
                return false;
            }
            catch (Exception e) { Stumble("OthersGrowth.Daily", e); return true; }
        }

        /// <summary>Prefiks RecruitmentCampaignBehavior.ApplyInternal(druzyna, osada, notabl, typ, ile, miejsce, rodzaj):
        /// "ochotnik z mapy" dla klanu Innych nie zachodzi.</summary>
        public static bool MapVolunteerPrefix(MobileParty __0, int __4, RecruitmentCampaignBehavior.RecruitingDetail __6)
        {
            try
            {
                if (__6 != RecruitmentCampaignBehavior.RecruitingDetail.VolunteerFromMap) return true;
                if (!Config.OthersNoFreeGrowth || !Config.OthersNoMapVolunteers) return true;
                if (__0 == null || __0.IsMainParty || !IsOthers(__0.ActualClan)) return true;
                int n = Math.Max(0, __4);
                _mapTimes++; _mapMen += n; _totalWights += n;
                return false;
            }
            catch (Exception e) { Stumble("OthersGrowth.MapVolunteer", e); return true; }
        }

        /// <summary>Raz na dobe: ile trupow nie dosypano (od poprzedniej linii).</summary>
        internal static void DailyLine()
        {
            try
            {
                if (!Config.OthersNoFreeGrowth) return;
                int day = 0;
                try { day = (int)(CampaignTime.Now - Campaign.Current.Models.CampaignTimeModel.CampaignStartTime).ToDays; } catch { }
                int sum = _birthWights + _dailyWights + _mapMen;
                Scribe.Line("Inni bez dosypki: dzien " + day + " - nie dosypano " + sum + " trupow (narodziny: bandy lordow " + _births
                            + " + inne partie Innych " + _birthsOther + " = " + (_births + _birthsOther) + " x100 = " + _birthWights
                            + "; +2 dziennie: " + _dailyTimes + " band = " + _dailyWights + "; ochotnicy z mapy: " + _mapTimes + " razy = " + _mapMen
                            + " ludzi); od wczytania razem " + _totalWights + "."
                            + (_openTimes > 0 ? " Bilans otwarcia (przepuszczone, nie wliczone): " + _openTimes + " x100 = " + (_openTimes * 100) + "." : "")
                            + (_stumblesDay > 0 ? " Potkniecia dzis " + _stumblesDay + " (razem " + _stumbles + ", pierwsze w raporcie)." : ""));
            }
            catch (Exception e) { Stumble("OthersGrowth.DailyLine", e); }
            _births = _birthWights = _birthsOther = _openTimes = _dailyTimes = _dailyWights = _mapTimes = _mapMen = 0;
            _stumblesDay = 0;
        }

        /// <summary>Nowa gra / wczytanie: liczniki od zera (latki zostaja - zakladane raz przy starcie gry).</summary>
        internal static void ResetSession()
        {
            _births = _birthWights = _birthsOther = _openTimes = _dailyTimes = _dailyWights = _mapTimes = _mapMen = 0;
            _totalWights = 0; _stumblesDay = 0;
        }
    }

    /// <summary>Linia dobowa "Inni bez dosypki" (bez zapisu w grze).</summary>
    internal sealed class OthersGrowthBehavior : CampaignBehaviorBase
    {
        // liczniki od zera przy kazdym starcie gry (OnGameStart) - PRZED narodzinami 4 band nowej kampanii,
        // ktore ROT robi jeszcze przed OnSessionLaunched; dzieki temu pierwsza linia dobowa pokazuje tez je
        public OthersGrowthBehavior() { OthersGrowth.ResetSession(); }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OthersGrowth.DailyLine);
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
