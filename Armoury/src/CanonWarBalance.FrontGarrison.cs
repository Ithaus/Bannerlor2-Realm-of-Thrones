using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 326c-E - MINIMALNA ZALOGA TWIERDZ FRONTU SLABSZEJ STRONY (Jeff 05.11: "zamki na froncie slabszej strony dostana minimalna zaloge; werbunek z okolicznych wsi
    /// i z kasy rodu, nic z niczego"). Klucz CanonWarBalanceFrontGarrison; dziala przy czynnej regule 274 z dzwigniami (jest slabsza strona).
    ///
    /// DLACZEGO: log 04/05.11 - zalogi Korony razem 6.1k wobec 18.7k Polnocy; Clegane's Keep padl ze zaloga 340 + milicja 235 (szturm 5862 ludzi), Deep Den 130 + 107.
    /// Gra werbuje do zalogi 1 ochotnika na dobe (DefaultSettlementGarrisonModel.GetMaximumDailyAutoRecruitmentCount = 1), choc ochotnicy u notabli czekaja.
    ///
    /// REGULA (raz na dobe, CanonWarBalance.Daily): twierdza frontu slabszej strony (274: w 55 jedn. od twierdzy drugiej strony), nie gracza, nie oblegana, z zagrozeniem -
    /// armia drugiej strony albo jej wolne druzyny w 55 jedn. (FrontRadius, SCA). Prog = ludzie zagrozenia / przewaga murow, gdzie przewaga murow z linii szturmu
    /// SiegeWalls (SZ, "przewaga murow" przy murach poziomu 2): zamek 5.5 (Clegane's Keep 109349), miasto 3.91 (Ten Towers 109314) - zaloga + milicja na tym progu
    /// sprawia, ze szturm tej armii kosztuje ja tyle co obrone. Ponizej progu - dodatkowe wywolania werbunku zalogi GRY (GarrisonRecruitmentCampaignBehavior.
    /// TickAutoRecruitmentGarrisonChange, odbiciem), az do progu albo gdy wywolanie nikogo nie doda: ochotnicy ze slotow notabli miasta i jego wsi (prawdziwi ludzie -
    /// ksiega ludzi A5, PeopleBook), oplata werbunku z kasy rodu pana (AutoRecruitmentExpenses -> E5 GarrisonRecruitFee do sprzedajacego), zold z limitu zoldu zalogi
    /// rodu (ClanBudget) - gra sama tnie werbunek limitem zoldu, pulapem zalogi i liczba ochotnikow. Warunek gry CanSettlementAutoRecruit (autowerbunek wlaczony,
    /// jedzenie na plusie, bez oblezenia i bitwy) - jak u gry. Nic nie powstaje z niczego.
    /// Recenzja 326c (R1-5, R1-8, R2-1..6, R3-5): prog najwyzej pulap zalogi gry (PartySizeLimit); bez twierdz w krainie z hamulcem wolnych rak (FreeHands - jak K8);
    /// bez rodu z pusta kiesa glowy; E bierze najwyzej polowe ochotnikow dostepnych zalodze tej doby (reszta zostaje dla druzyn lordow - dzwignia werbunku B);
    /// przed kazdym wywolaniem warunek gry CanSettlementAutoRecruit i jedzenie miasta co najmniej +1 na dobe (zaloga je - nie wolno zaglodzic twierdzy);
    /// stop przy pierwszym rekrucie "na podlodze" (ksiega ludzi: rekrut bez zdjecia z ludnosci - nic z niczego) i gdy oplata werbunku w kolejce rodu
    /// (AutoRecruitmentExpenses) + 7 dob zoldu nowych ludzi przekroczy kiese glowy; wywolania E poza licznikami A5/K8 (ArmsFromPeople.FrontGarrisonCall).
    /// Linia "Rownowaga wojny (274): zaloga frontu (326c)" - raz na dobe (suma, powody przerwania), przyklady do 4 twierdz.
    /// </summary>
    internal static partial class CanonWarBalance
    {
        private const float WallEdgeCastle = 5.5f;   // SZ: przewaga murow zamku (Clegane's Keep 109349, mury poziom 2)
        private const float WallEdgeTown = 3.91f;    // SZ: przewaga murow miasta (Ten Towers 109314, mury poziom 2)
        private const int FrontGarrisonMaxCalls = 200;   // bezpiecznik petli na twierdze i dobe (gra konczy wczesniej - ochotnicy, zold, pulap)
        private const float FrontGarrisonFoodMin = 1f;    // jedzenie miasta na dobe przed kolejnym wywolaniem (gra: > 0; zapas na zjedzenie nowych ludzi)
        private const int FrontGarrisonWageDays = 7;      // zold nowych ludzi na tydzien musi byc w kiesie glowy razem z kolejka oplat werbunku

        private static MethodInfo _miAutoRec, _miCanAuto;
        private static bool _fgReflected;
        private static string _wFrontGar = "-";

        private static bool FrontGarrisonOn { get { var s = Settings.Current; return s != null && s.CanonWarBalance && s.CanonWarBalanceFrontGarrison; } }

        private static void ReflectFrontGarrison()
        {
            if (_fgReflected) return;
            _fgReflected = true;
            try
            {
                var t = typeof(GarrisonRecruitmentCampaignBehavior);
                _miAutoRec = AccessTools.Method(t, "TickAutoRecruitmentGarrisonChange", new[] { typeof(Town) });
                _miCanAuto = AccessTools.Method(t, "CanSettlementAutoRecruit", new[] { typeof(Settlement) });
                _wFrontGar = _miAutoRec != null && _miCanAuto != null ? "TAK" : "BRAK (" + (_miAutoRec == null ? "TickAutoRecruitmentGarrisonChange " : "") + (_miCanAuto == null ? "CanSettlementAutoRecruit" : "") + ")";
            }
            catch (Exception e) { _wFrontGar = "BLAD (" + e.GetType().Name + ")"; Stumble("ReflectFrontGarrison", e); }
        }

        /// <summary>326c-E (autotest): prog zalogi = ceil(ludzie zagrozenia / przewaga murow); zamek 5.5, miasto 3.91.</summary>
        internal static int FrontGarrisonNeed(int threatMen, bool castle)
        {
            if (threatMen <= 0) return 0;
            return (int)Math.Ceiling(threatMen / (castle ? WallEdgeCastle : WallEdgeTown));
        }

        /// <summary>Ludzie zagrozenia twierdzy: najwieksza armia drugiej strony (wodz + doczepieni) albo suma wolnych druzyn lordow drugiej strony w 55 jedn.</summary>
        private static int ThreatMen(Settlement st, int side)
        {
            float r2 = FrontRadius * FrontRadius;
            var pos = st.GetPosition2D;
            int bestArmy = 0, free = 0;
            var all = MobileParty.AllLordParties;
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (p == null || !p.IsActive || p.AttachedTo != null) continue;
                int ps = SideOf(p.MapFaction);
                if (ps == 0 || ps == side) continue;
                if (st.MapFaction == null || p.MapFaction == null || !p.MapFaction.IsAtWarWith(st.MapFaction)) continue;
                if (p.GetPosition2D.DistanceSquared(pos) > r2) continue;
                if (p.Army != null && p.Army.LeaderParty == p)
                {
                    int men = p.MemberRoster.TotalManCount;
                    var att = p.AttachedParties;
                    for (int j = 0; j < att.Count; j++) if (att[j] != null) men += att[j].MemberRoster.TotalManCount;
                    if (men > bestArmy) bestArmy = men;
                }
                else if (p.Army == null || p.Army.LeaderParty == p) free += p.MemberRoster.TotalManCount;   // R2-6: czlonek armii (niedoczepiony) - w sile swojej armii
            }
            return Math.Max(bestArmy, free);
        }

        /// <summary>Ochotnicy dostepni zalodze tej doby - te same sloty co gra (GarrisonRecruitmentCampaignBehavior.RepopulateVolunteerListCache): notable miasta
        /// i jego wsi w stanie Normal, indeks do MaximumIndexGarrisonCanRecruitFromHero. Nie rzuca (blad - 0).</summary>
        private static int GarrisonVolunteers(Settlement st)
        {
            int n = 0;
            try
            {
                var vm = Campaign.Current.Models.VolunteerModel;
                foreach (var h in st.Notables) n += Slots(vm, st, h);
                foreach (var v in st.BoundVillages)
                {
                    if (v == null || v.VillageState != Village.VillageStates.Normal || v.Settlement == null) continue;
                    foreach (var h in v.Settlement.Notables) n += Slots(vm, st, h);
                }
            }
            catch { return 0; }
            return n;
        }

        private static int Slots(VolunteerModel vm, Settlement st, Hero h)
        {
            if (h == null || !h.IsAlive || h.VolunteerTypes == null) return 0;
            int max = Math.Min(vm.MaximumIndexGarrisonCanRecruitFromHero(st, h), h.VolunteerTypes.Length), n = 0;
            for (int i = 0; i < max; i++) if (h.VolunteerTypes[i] != null) n++;
            return n;
        }

        /// <summary>Raz na dobe z Daily (po Step, przy dzwigniach): werbunek zalog twierdz frontu slabszej strony do progu. Potkniecia per twierdza. Nie rzuca.</summary>
        private static void FrontGarrisonDaily(int today)
        {
            if (!FrontGarrisonOn || !_levers || _weak == 0) return;
            ReflectFrontGarrison();
            if (_miAutoRec == null || _miCanAuto == null) return;
            var beh = Campaign.Current.GetCampaignBehavior<GarrisonRecruitmentCampaignBehavior>();
            if (beh == null) return;
            int forts = 0, threatened = 0, below = 0, blocked = 0, men = 0, helped = 0, stuck = 0;
            int braked = 0, noGold = 0, stopFood = 0, stopFloor = 0, stopGold = 0, stopHalf = 0;
            var ex = new List<string>();
            var list = _frontOwn[_weak];
            var args = new object[1];
            for (int i = 0; i < list.Count; i++)
            {
                var st = list[i];
                try
                {
                    if (st == null || st.Town == null || !st.IsFortification || st.OwnerClan == null || st.OwnerClan == Clan.PlayerClan || st.IsUnderSiege) continue;
                    if (SideOf(st.MapFaction) != _weak) continue;
                    forts++;
                    int threat = ThreatMen(st, _weak);
                    if (threat <= 0) continue;
                    threatened++;
                    int need = FrontGarrisonNeed(threat, st.IsCastle);
                    var g0 = st.Town.GarrisonParty;
                    if (g0 != null && g0.Party != null && g0.Party.PartySizeLimit > 0 && need > g0.Party.PartySizeLimit) need = g0.Party.PartySizeLimit;   // R3-5: pulap zalogi gry
                    int gar = g0 != null ? g0.MemberRoster.TotalManCount : 0;
                    int mil = (int)st.Town.Militia;
                    if (gar + mil >= need || gar >= need) continue;
                    below++;
                    if (FreeHands.Braked(st)) { braked++; continue; }                     // R1-5 / R2-2: kraina bez wolnych rak nie oddaje ludzi ponad tempo gry
                    var clan = st.OwnerClan;
                    if (clan.Leader == null || clan.Gold <= 0) { noGold++; continue; }     // R1-8 / R2-4: pusta kiesa glowy - werbunek szedlby na kredyt bez pokrycia
                    args[0] = st;
                    if (!(bool)_miCanAuto.Invoke(beh, args)) { blocked++; continue; }
                    int half = GarrisonVolunteers(st) / 2;                                 // R3-5: polowa ochotnikow zostaje dla druzyn lordow
                    int wage0 = g0 != null ? g0.TotalWage : 0;
                    int got = 0;
                    string why = null;
                    for (int c = 0; c < FrontGarrisonMaxCalls && gar + mil + got < need; c++)
                    {
                        if (got >= half) { why = "polowa"; stopHalf++; break; }
                        args[0] = st;
                        if (c > 0 && !(bool)_miCanAuto.Invoke(beh, args)) { why = "gra"; break; }
                        if (st.Town.FoodChange < FrontGarrisonFoodMin) { why = "jedzenie"; stopFood++; break; }   // R2-1: zaloga je - bilans jedzenia na plusie
                        var g = st.Town.GarrisonParty;
                        int before = g != null ? g.MemberRoster.TotalManCount : 0;
                        int floor0 = PeopleBook.FloorCallsNow;
                        args[0] = st.Town;
                        ArmsFromPeople.FrontGarrisonCall = true;
                        try { _miAutoRec.Invoke(beh, args); }
                        finally { ArmsFromPeople.FrontGarrisonCall = false; }
                        g = st.Town.GarrisonParty;
                        int d = (g != null ? g.MemberRoster.TotalManCount : 0) - before;
                        if (d > 0) got += d;
                        if (PeopleBook.FloorCallsNow > floor0) { why = "podloga"; stopFloor++; break; }         // R2-3: rekrut bez ludzi w ksiedze - stop
                        if (d <= 0) break;
                        long wageAdd = Math.Max(0, (g != null ? g.TotalWage : 0) - wage0);
                        if ((long)clan.AutoRecruitmentExpenses + FrontGarrisonWageDays * wageAdd > clan.Gold) { why = "kasa"; stopGold++; break; }   // R2-4
                    }
                    if (got > 0) { helped++; men += got; } else stuck++;
                    if (ex.Count < 4)
                        ex.Add(Nm(st) + " " + gar + "+" + mil + " -> +" + got + " (prog " + need + " z " + threat + " ludzi wroga / " + (st.IsCastle ? WallEdgeCastle : WallEdgeTown).ToString("0.##", Inv)
                               + ", ochotnikow do wziecia " + half + (why != null ? ", stop: " + why : "") + ")");
                }
                catch (Exception e) { Stumble("FrontGarrisonDaily", e); }
            }
            if (Say("zaloga326"))
                Log.Info(Tag + "zaloga frontu (326c) | dzien " + today + " | strona " + SideName(_weak) + " (slabsza) | odbicia gry " + _wFrontGar + " | twierdze frontu " + forts + ", z wrogiem w "
                         + F0(FrontRadius) + " jedn. " + threatened + ", ponizej progu " + below + " (hamulec wolnych rak " + braked + ", pusta kiesa rodu " + noGold
                         + ", autowerbunek gry niedostepny " + blocked + ", bez nikogo do wziecia " + stuck + ")"
                         + " | dobrani do zalog " + men + " ludzi w " + helped + " twierdzach (ochotnicy notabli miasta i wsi, oplata i zold z kasy rodu pana)"
                         + " | przerwane: polowa ochotnikow dla lordow " + stopHalf + ", jedzenie " + stopFood + ", podloga ludzi " + stopFloor + ", kasa rodu " + stopGold
                         + (ex.Count > 0 ? " | " + string.Join(", ", ex.ToArray()) : "") + ".");
        }
    }
}
