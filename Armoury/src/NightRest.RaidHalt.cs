using System;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 577 (Jeff 16.11: "czemu jak pale wioske to mam no sleep, to jest bez sensu co mam przerwac??" - wybrany "pierwszy wariant"): NOCNY POSTOJ PRZY RABUNKU.
    /// Rabunek wsi gry, ktory prowadzi gracz (sam albo jako wodz armii - AttackerSide.LeaderParty == druzyna gracza), w nocy obozu gracza (BedNight - godziny obozu
    /// swiata, przy 21-3 godziny 21:00-2:59; ta sama noc, w ktorej staje oboz i pada pytanie o nocleg) stoi: kolumna spi przy wsi, a godziny licza sie w ksiedze snu
    /// jak postoj w obozie (nie jako praca RaidCountsAsWork). O swicie (koniec godzin obozu) rabunek idzie dalej sam.
    ///  - JAK STOI: MapEvent rabunku zostaje (wies BeingRaided, menu raiding_village otwarte, pasek = 1 - SettlementHitPoints stoi, bo punkty sie nie zmieniaja).
    ///    Postep robi tylko RaidEventComponent.Update w fazie lupienia (DefenderSide.TroopCount == 0): gra - CalculateHitDamage, uderzenia, zloto i towar; E7/R1 -
    ///    DistrictRaid.UpdatePrefix (Prog, uderzenia, spichlerz, LootDays). Prefiks DistrictRaid (bool, First) przy RaidHaltHolds zwraca false PRZED postepem
    ///    (po wykonaniu wyboru z okienka E7/R2 - "End the raid" w nocy dziala od reki), a prefiksy void Devastation.RaidPrefix i SmallGold.RaidPre wychodza
    ///    od razu (bez osobodni i pomiarow). Bez SwitchToMenu, bez konczenia MapEvent, bez ruszania wizerunkow mapy.
    ///  - W NOCY BEZ OCHRONY: walka (milicja, odsiecz dolaczona do zdarzenia - DefenderSide.TroopCount > 0) biegnie jak w grze - RaidHaltHolds jest falszywe poza faza lupienia.
    ///  - KONIEC BEZ WZNOWIENIA: rabunek skonczony (End Raiding, bitwa, koniec zdarzenia), klucz zdjety, ksiega snu wylaczona - postoj znika, nic sie nie wznawia.
    ///  - NIE STAJE: marsz forsowny (ForcedMarch.Active - kolumna i tak nie spi), Nieumarli, gracz nie jest wodzem rabunku (cudza armia - regula AI), latka kroku
    ///    rabunku DistrictRaid niewpieta (DistrictRaid.UpdWired). Wioski poboczne (SideVillageVisit, wlasne menu bez MapEvent) - poza 577.
    ///  - AI bez zmian: lord AI rabuje cala dobe, a ksiega AI liczy postoj przy wsi jako odpoczynek (bez RaidCountsAsWork).
    /// Tylko pamiec (wczytanie w nocy - postoj od nowa w pierwszej klatce). Wyjatek liczony; po MaxHaltStumbles postoj zdjety (rabunek idzie), funkcja dalej czynna.
    /// </summary>
    internal static partial class NightRest
    {
        private const int MaxHaltStumbles = 3;
        private static RaidEventComponent _haltComp;     // rabunek gracza stojacy na noc (null = brak postoju)
        private static string _haltName;
        private static double _haltFrom = -1;             // CampaignTime.ToHours poczatku postoju
        private static float _haltHp0;                    // punkty wsi przy postoju (pasek) - do linii wznowienia (kontrola: pasek stal)
        private static int _haltStumbles, _haltErrLogged;
        private static bool _haltOff;                     // recenzja 577: po MaxHaltStumbles postoj zdjety do switu (bez ponownego stawania i komunikatu co klatke)
        // liczniki doby od switu gracza (linia switu NocnyMarsz): godziny ksiegi z postojem, odpoczynek z nich, postoje, wznowienia, konce bez wznowienia
        private static int _haltTicksDay, _haltStartsDay, _haltResumesDay, _haltEndsDay;
        private static float _haltRestDay;
        // recenzja 577: liczniki doby sprzed zerowania o swicie - tick switu bywa przed klatka wznowienia, wtedy linia wznowienia/konca podaje noc z nich
        private static int _haltTicksPrev;
        private static float _haltRestPrev;

        /// <summary>577: postoj nocny przy rabunku trwa (tick godzinowy ksiegi gracza).</summary>
        internal static bool RaidHaltNow { get { return _haltComp != null; } }

        /// <summary>577: ten rabunek stoi na noc w fazie lupienia - prefiksy kroku rabunku (DistrictRaid, Devastation, SmallGold) nie robia postepu.
        /// Wolane z RaidEventComponent.Update kazdego rabunku swiata - sam odczyt, bez alokacji.</summary>
        internal static bool RaidHaltHolds(RaidEventComponent comp)
        {
            var hc = _haltComp;
            if (hc == null || comp == null || !ReferenceEquals(comp, hc)) return false;
            var def = comp.DefenderSide;
            return def != null && def.TroopCount == 0;
        }

        /// <summary>577: rabunek wsi gry, ktory prowadzi druzyna gracza (sama albo jako wodz armii); null - brak.</summary>
        private static RaidEventComponent PlayerLedRaid()
        {
            var mp = MobileParty.MainParty;
            var me = mp != null ? mp.MapEvent : null;
            if (me == null || !me.IsRaid) return null;
            var att = me.AttackerSide;
            if (att == null || att.LeaderParty == null || att.LeaderParty != mp.Party) return null;
            return me.Component as RaidEventComponent;
        }

        private static void ResetRaidHalt()
        {
            _haltComp = null; _haltName = null; _haltFrom = -1; _haltHp0 = 0f; _haltStumbles = 0; _haltErrLogged = 0; _haltOff = false;
            _haltTicksPrev = 0; _haltRestPrev = 0f;
            _haltTicksDay = _haltStartsDay = _haltResumesDay = _haltEndsDay = 0; _haltRestDay = 0f;
        }

        private static void RaidHaltDawnReset()
        {
            _haltTicksPrev = _haltTicksDay; _haltRestPrev = _haltRestDay; _haltOff = false;
            _haltTicksDay = _haltStartsDay = _haltResumesDay = _haltEndsDay = 0; _haltRestDay = 0f;
        }

        private static string HaltPct(RaidEventComponent c)
        {
            try { var st = c.MapEventSettlement; return st != null ? ((1f - st.SettlementHitPoints) * 100f).ToString("0", CultureInfo.InvariantCulture) + "%" : "?"; }
            catch { return "?"; }
        }

        /// <summary>577: klatka kampanii (OnTickCore) - poczatek postoju o zmierzchu, wznowienie o swicie, koniec bez wznowienia. Szybka sciezka bez postoju i bez klucza.</summary>
        private static void RaidHaltFrame(Settings s)
        {
            if (_haltComp == null && !s.RaidNightHalt) return;
            try
            {
                if (Campaign.Current == null) { _haltComp = null; return; }
                var cur = PlayerLedRaid();
                double now = CampaignTime.Now.ToHours;
                bool on = s.RaidNightHalt && s.NightRestEnabled && DistrictRaid.UpdWired;
                if (_haltComp != null)
                {
                    if (!ReferenceEquals(cur, _haltComp)) { EndHalt("rabunek skonczony (End Raiding, bitwa albo koniec zdarzenia) - bez wznowienia", now); return; }
                    if (!on) { EndHalt("klucz Raid Night Halt albo ksiega snu wylaczone - rabunek idzie dalej od reki", now); return; }
                }
                if (cur == null || !on) return;
                bool night = BedNight(CampaignTime.Now.GetHourOfDay);
                if (_haltComp == null)
                {
                    if (!night) { _haltOff = false; return; }
                    if (_haltOff || ForcedMarch.Active) return;
                    var def = cur.DefenderSide;
                    if (def == null || def.TroopCount > 0) return;                     // walka z milicja albo odsiecza - gra
                    var mp = MobileParty.MainParty;
                    if (Undead.Party(mp)) return;                                       // umarli nie spia
                    var st = cur.MapEventSettlement;
                    _haltComp = cur; _haltFrom = now; _haltName = st != null ? st.Name.ToString() : "?"; _haltHp0 = st != null ? st.SettlementHitPoints : 0f;
                    _haltStartsDay++;
                    try { var me = cur.MapEvent; if (me != null) me.WasEverInLootingPhase = true; } catch { }   // jak gra przy pierwszym kroku lupienia (RaidEventComponent.Update)
                    Msg(PartyWords.Pick("Night falls - the men stop raiding " + _haltName + " and sleep by the village. The raid goes on at dawn.",
                                        "Night falls - you stop raiding " + _haltName + " and sleep by the village. The raid goes on at dawn.",
                                        "Night falls - you and your companions stop raiding " + _haltName + " and sleep by the village. The raid goes on at dawn."), Colors.White);
                    int parties = 1;
                    try { var a = mp.Army; if (a != null && a.LeaderParty == mp && a.Parties != null) parties = a.Parties.Count; } catch { }
                    Log.Info("Nocleg: gracz - postoj nocny przy rabunku (577, Raid Night Halt): " + _haltName + " o " + Hm(CampaignTime.Now.CurrentHourInDay)
                             + ", pasek rabunku " + HaltPct(cur) + (parties > 1 ? ", wodz armii (" + parties + " partii)" : "") + " - postep rabunku stoi do switu "
                             + PlayerDawn + ":00, godziny licza sie jak sen w obozie (nie Raid Counts As Work); odpoczynek doby " + F1(_restTonight) + " h, dlug " + Debt
                             + ", E7/R1 " + (DistrictRaid.Owns(cur) ? "prowadzi rabunek" : "nie prowadzi (rabunek gry)") + ".");
                }
                else if (!night)
                {
                    string name = _haltName; double from = _haltFrom; float hp0 = _haltHp0; string pct = HaltPct(cur);
                    float hp1 = 0f; try { var st = cur.MapEventSettlement; hp1 = st != null ? st.SettlementHitPoints : 0f; } catch { }
                    _haltComp = null; _haltResumesDay++;
                    Msg(PartyWords.Pick("Dawn - the men take up the raid on " + name + " again.",
                                        "Dawn - you take up the raid on " + name + " again.",
                                        "Dawn - you and your companions take up the raid on " + name + " again."), Colors.White);
                    Log.Info("Nocleg: gracz - wznowienie rabunku o swicie (577): " + name + " o " + Hm(CampaignTime.Now.CurrentHourInDay) + ", postoj " + F1((float)(now - from))
                             + " h, pasek " + pct + (Math.Abs(hp1 - hp0) > 1e-4f ? " (ZMIENIL SIE w postoju o " + ((hp0 - hp1) * 100f).ToString("0.0", CultureInfo.InvariantCulture) + " pp)" : " (stal)")
                             + "; " + HaltNightCounts() + "; odpoczynek doby " + F1(_restTonight)
                             + " h, dlug " + Debt + ".");
                }
            }
            catch (Exception e)
            {
                _haltStumbles++;
                if (_haltErrLogged++ < 3) Log.Error("NightRest.RaidHaltFrame", e);
                if (_haltStumbles >= MaxHaltStumbles)
                {
                    // recenzja 577: zdjety do switu (_haltOff) - inaczej nastepna klatka nocy stanelaby od nowa z komunikatem; potkniecia liczone od nowa nastepnej nocy
                    _haltStumbles = 0;
                    if (!_haltOff) { _haltOff = true; Log.Info("Nocleg: gracz - postoj nocny przy rabunku zdjety do switu po " + MaxHaltStumbles + " potknieciach (rabunek idzie dalej)."); }
                    _haltComp = null;
                }
            }
        }

        private static void EndHalt(string why, double now)
        {
            string name = _haltName; double from = _haltFrom;
            _haltComp = null; _haltEndsDay++;
            Log.Info("Nocleg: gracz - postoj nocny przy rabunku (577) zakonczony: " + (name ?? "?") + " - " + why + "; postoj " + F1((float)(now - from)) + " h, " + HaltNightCounts() + ".");
        }

        /// <summary>recenzja 577: godziny ksiegi z postojem i odpoczynek z nich tej nocy - po zerowaniu o swicie (tick switu przed klatka) z licznikow sprzed zerowania.</summary>
        private static string HaltNightCounts()
        {
            int t = _haltTicksDay; float r = _haltRestDay; bool prev = false;
            if (t == 0 && _haltTicksPrev > 0) { t = _haltTicksPrev; r = _haltRestPrev; prev = true; }
            return "godziny ksiegi z postojem tej nocy " + t + ", odpoczynek z nich " + F1(r) + " h" + (prev ? " (doba juz rozliczona o swicie)" : "");
        }
    }
}
