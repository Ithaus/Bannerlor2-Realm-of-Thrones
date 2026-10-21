using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m97-wioski - WROGIE AKCJE W WIOSCE POBOCZNEJ (Jeff 20.10: "pladrowanie wioski trzeba robic kazda osobno, nie tak, ze robi sie to z glownej; zaplacic - wioska moze okup,
    /// zeby jej nie pladrowac"). Menu "arm_sidevil_hostile" (tylko w wojnie z krolestwem okregu; bez wypowiadania wojny z wioski):
    ///  R1 RABUNEK TEJ WIOSKI ("Raid the village", menu oczekiwania "arm_sidevil_raid" z paskiem): ta sama regula co rabunek osada po osadzie (DistrictRaid/E7/R1), ale
    ///     tylko osady tej wioski i bez zdarzenia rabunku gry (wies gry zostaje Normal):
    ///     - obrona (recenzja 20.10): wioske broni CALA milicja wsi okregu i partie stojace we wsi w wojnie z graczem (RaidTribute.DefOf - ta sama regula sily co okup
    ///       wsi R3-03a; milicja pilnuje calej okolicy - dawniej czesc milicji wedlug ludzi wioski, co dawalo maly oddzial spalic okreg wioska po wiosce bez bitwy);
    ///       mniej zdrowych ludzi niz obroncow - rabunek niemozliwy (bez bitwy: wioska bez bohaterow nie stawia oddzialu - przewaga albo nic, jak okup R3-03a q < 1;
    ///       milicje rozbija sie bitwa przy wsi gry - "Raid the village" w jej menu);
    ///     - praca = ludzie osad tej wioski do pulapu spustoszenia (DevastationMaxPercent x osady - puste), nie wiecej niz Room okregu (Devastation.DistrictPeople);
    ///       tempo R x N na dobe (R = DevastationPerRaiderDay, N = zdrowi ludzie gracza i armii, mnoznik modelu 1, u 1);
    ///     - co godzine gry uderzenie Devastation.StrikePeople (ten sam rdzen: PeopleBook.Move, zabici / las / uchodzcy, spichlerz, prace okregu, pamiec wojny) z
    ///       VillageBook.ForceUid = ta wioska - ksiega pali TYLKO jej osady (bez lancucha do innych wiosek);
    ///     - lup raz na koniec (pelny pasek, "Stop raiding" albo przerwanie - za zrobiona czesc): udzial s = trafieni / ludzie okregu w domu na starcie; zloto s x kiesa
    ///       wsi (SmallGold.PayRaidLoot - ten sam platnik), stado (VillageHerds.OnRaid), towar s z magazynu wsi okregu do taboru gracza (konie i zwierzeta zawsze,
    ///       reszta wedlug udzwigu, czego nie uniesie - spalone; ramka FRaid ksiegi towarow, ArmsBook KRaid) - ta sama arytmetyka co DistrictRaid.Loot;
    ///     - relacja: pan okregu -3, notable -1 (rabunek gry -6 / -3; wioska to czesc okregu); bez licznika wojny gry (to nie rabunek wsi gry).
    ///  R2 OKUP TEJ WIOSKI ("Demand tribute"): regula R3-03a na czesci wioski - obroncy jak wyzej (cala milicja okregu), q = ludzie / obroncy (q &lt; 1 - opor), stawka
    ///     Low + (High - Low) x min(1, (q - 1) / 4), kwota = stawka x kiesa wsi okregu x udzial wioski w ludziach okregu; przelew kiesa wsi -> gracz (GiveGoldAction);
    ///     ochrona (wioska, krolestwo gracza) RaidTributeSpareDays dob: bez rabunku i drugiego okupu tej wioski od krolestwa gracza; relacja pan -1. Okup wsi gry (R3-03a)
    ///     dla krolestwa gracza chroni tez jej wioski poboczne (to okup calej okolicy). Po rabunku tej wioski - 10 dob bez okupu (jak gra po wrogim czynie).
    ///     Recenzja 20.10: ochrona dziala tez poza menu wioski - lancuch rabunku wsi gry (VillageBook.PickFirst/NextFrom, plan kroku DistrictRaid) omija wioski chronione
    ///     przed frakcja napastnika (Spared - migawka _protSnap), a okup wsi gry (RaidTribute.QuoteOf) tej frakcji jest mniejszy o udzial wiosek juz oplaconych (PaidShare).
    /// AI bez zmian (rabuje wies gry - DistrictRaid pali wioski lancuchem, jak dotad; omija tylko wioski po okupie dla jego frakcji). Rabunek wsi gry przez GRACZA pali od
    /// recenzji 20.10 tylko osady wsi gry (DistrictRaid.PlayerMainOnly) - kazda wioske poboczna gracz rabuje osobno, z jej menu.
    /// </summary>
    internal static partial class SideVillageVisit
    {
        private const int HostileMemoryDays = 10;     // po rabunku wioski tyle dob bez okupu (jak gra: _villageLastHostileActionTimeDictionary, 10 dob)
        private const int RelLordRaid = -3, RelNotableRaid = -1, RelLordTribute = -1;
        private const string SaveVer = "sv1";

        private sealed class Raid
        {
            public string Uid, Dist, Name;
            public double Work, Done, Hours, HoursDone, Pend, Hit, Dead, Fled, Home0;
            public int Men, Day, Misses;
            public bool Stop, SlaverSet;   // SlaverSet - niewola napastnika policzona (po wczytaniu - przy pierwszym ticku, recenzja 20.10)
            public Devastation.RaidState Rs;
        }
        // Share - udzial wioski w ludziach okregu w domu przy okupie (okup wsi gry tej frakcji jest o niego mniejszy), Dist - wies okregu (StringId; z zapisu bez pola - z ukladu)
        private sealed class Prot { public string Uid, Fid, Dist; public int Until, Day; public long Gold; public double Share; }

        private static Raid _raid;
        private static readonly List<Prot> _prot = new List<Prot>();
        private static readonly Prot[] NoProt = new Prot[0];
        private static Prot[] _protSnap = NoProt;   // migawka _prot (podmieniana w calosci po zmianie - odczyt w ksiedze wiosek bez alokacji i bez blokad)
        private static readonly Dictionary<string, int> _lastRaid = new Dictionary<string, int>(StringComparer.Ordinal);
        private static int _dRaidStart, _dRaidEnd, _dRaidStop, _dRaidCut, _dTrib, _dStrikes, _dRaidCapped;
        private static double _dRaidHit, _dRaidDead;
        private static long _dRaidGold, _dRaidTake, _dRaidBurn, _dTribGold;
        private static readonly List<string> _dEx = new List<string>();

        internal static bool RaidActive { get { return _raid != null; } }

        private static void RaidReset() { _raid = null; _prot.Clear(); _lastRaid.Clear(); ProtSync(); }

        /// <summary>Migawka ochron od nowa (po okupie, wczytaniu, resecie, na dobe) - wygasle zdjete.</summary>
        private static void ProtSync()
        {
            int today = Today0();
            if (today > 0) _prot.RemoveAll(p => p == null || p.Until <= today);
            System.Threading.Volatile.Write(ref _protSnap, _prot.Count == 0 ? NoProt : _prot.ToArray());
        }

        private static int Today0() { try { return Campaign.Current != null ? Today() : 0; } catch { return 0; } }

        /// <summary>Jest jakas ochrona po okupie wioski (migawka) - VillageBook.RoomFor bez niej konczy od razu.</summary>
        internal static bool AnyProt { get { return System.Threading.Volatile.Read(ref _protSnap).Length > 0; } }

        /// <summary>Ksiega wiosek (lancuch rabunku, plan kroku): wioska uid zaplacila okup frakcji fid i jest chroniona (migawka - bez alokacji).</summary>
        internal static bool Spared(string uid, string fid)
        {
            var a = System.Threading.Volatile.Read(ref _protSnap);
            if (a.Length == 0 || uid == null || fid == null) return false;
            int today = Today0();
            for (int i = 0; i < a.Length; i++) { var p = a[i]; if (p.Until > today && string.Equals(p.Uid, uid, StringComparison.Ordinal) && string.Equals(p.Fid, fid, StringComparison.Ordinal)) return true; }
            return false;
        }

        /// <summary>RaidTribute.QuoteOf (okup wsi gry): suma udzialow wiosek pobocznych tego okregu chronionych okupem przed frakcja f (0..1). Watek kampanii.</summary>
        internal static double PaidShare(Settlement st, IFaction f)
        {
            var a = System.Threading.Volatile.Read(ref _protSnap);
            if (a.Length == 0 || st == null || f == null || st.StringId == null) return 0.0;
            int today = Today0();
            double sum = 0.0;
            for (int i = 0; i < a.Length; i++)
            {
                var p = a[i];
                if (p.Until <= today || !string.Equals(p.Fid, f.StringId, StringComparison.Ordinal)) continue;
                if (p.Dist == null) { var r = Rec(p.Uid); if (r != null) p.Dist = r.VillageId; }   // wpis bez pola okregu - z ukladu wiosek
                if (string.Equals(p.Dist, st.StringId, StringComparison.Ordinal)) sum += Math.Max(0.0, p.Share);
            }
            return Math.Min(1.0, sum);
        }
        private static void RaidDayZero() { _dRaidStart = _dRaidEnd = _dRaidStop = _dRaidCut = _dTrib = _dStrikes = _dRaidCapped = 0; _dRaidHit = _dRaidDead = 0; _dRaidGold = _dRaidTake = _dRaidBurn = _dTribGold = 0; _dEx.Clear(); }
        private static bool RaidDayAny() { return _dRaidStart + _dRaidEnd + _dTrib + _dStrikes > 0 || _raid != null; }

        private static string RaidDayText()
        {
            return "rabunki wiosek: zaczete " + _dRaidStart + ", skonczone " + _dRaidEnd + " (pelne " + (_dRaidEnd - _dRaidStop - _dRaidCut) + ", przerwane przez gracza " + _dRaidStop + ", inne " + _dRaidCut + ")"
                   + ", uderzen " + _dStrikes + " (obcietych do miejsca wioski " + _dRaidCapped + "), trafieni " + _dRaidHit.ToString("0.0", Inv) + " (zabici " + _dRaidDead.ToString("0.0", Inv) + "), lup: zloto " + _dRaidGold + ", sztuki " + _dRaidTake
                   + ", spalone " + _dRaidBurn + (_raid != null ? ", w toku " + _raid.Name + " " + (100.0 * Math.Min(1.0, _raid.HoursDone / Math.Max(0.01, _raid.Hours))).ToString("0", Inv) + "%" : "")
                   + " | okupy wiosek " + _dTrib + " (" + _dTribGold + " zl), ochron " + _prot.Count + (_dEx.Count > 0 ? " | przyklady: " + string.Join("; ", _dEx.ToArray()) : "");
        }

        private static string RaidSessionText()
        {
            return "ochron po okupie " + _prot.Count + ", rabunkow gracza w pamieci " + _lastRaid.Count + ", rabunek w toku " + (_raid != null ? _raid.Name + " (" + _raid.Uid + ")" : "nie");
        }

        private static void Example(string s) { if (_dEx.Count < 3) _dEx.Add(s); }

        // ------------------------------------------------------------ obrona i ochrona
        private static int Healthy(MobileParty p) { try { return p != null && p.MemberRoster != null ? Math.Max(0, p.MemberRoster.TotalHealthyCount) : 0; } catch { return 0; } }

        /// <summary>Zdrowi ludzie gracza i partii armii, ktora prowadzi (te stoja z nim przy wiosce - jak RaidTribute.MenOf).</summary>
        private static int MenNow()
        {
            var mp = MobileParty.MainParty;
            if (mp == null) return 0;
            int n = Healthy(mp);
            try
            {
                var a = mp.Army; var ap = mp.AttachedParties;
                if (a != null && a.LeaderParty == mp && ap != null) for (int i = 0; i < ap.Count; i++) if (ap[i] != null && ap[i] != mp) n += Healthy(ap[i]);
            }
            catch { }
            return n;
        }

        /// <summary>Obroncy wioski (recenzja 20.10): cala milicja wsi okregu i partie we wsi w wojnie z graczem - RaidTribute.DefOf, ta sama regula sily co okup wsi (milicja
        /// pilnuje calej okolicy). share - udzial wioski w ludziach okregu w domu (lup, okup).</summary>
        private static int Defenders(Settlement st, VillageBook.PartView me, double homeAll, out double share)
        {
            share = me != null && homeAll > 0 ? Math.Max(0.0, Math.Min(1.0, me.Home / homeAll)) : 0.0;
            return RaidTribute.DefOf(st, Hero.MainHero != null ? Hero.MainHero.MapFaction : null);
        }

        private static Prot ProtOf(string uid, IFaction f)
        {
            if (uid == null || f == null) return null;
            int today = Today();
            foreach (var p in _prot) if (p.Uid == uid && p.Fid == f.StringId && p.Until > today) return p;
            return null;
        }

        /// <summary>Linia opisu: ochrona po okupie (krolestwo gracza) - wioski albo calej okolicy (okup wsi gry R3-03a).</summary>
        private static string TributeLine(Settlement st)
        {
            try
            {
                var f = Hero.MainHero != null ? Hero.MainHero.MapFaction : null;
                if (f == null) return null;
                var p = ProtOf(_uid, f);
                if (p != null) return "It paid your realm " + p.Gold.ToString("N0", Inv) + " denars of tribute and is spared for " + (p.Until - Today()) + " more days.";
                if (RaidTribute.Spared(st, f, Today())) return st.Name + " paid your realm tribute for all its lands - this village is spared too.";
            }
            catch (Exception e) { Stumble("TributeLine", e); }
            return null;
        }

        /// <summary>Wspolne bramki obu wrogich akcji. why - po angielsku; null = mozna.</summary>
        private static string HostileBlock(Settlement st, VillageBook.PartView me, bool tribute)
        {
            var mp = MobileParty.MainParty;
            var f = Hero.MainHero != null ? Hero.MainHero.MapFaction : null;
            if (!AtWar(st)) return "You are not at war with " + Txt(st.MapFaction != null ? st.MapFaction.Name : null) + ".";
            if (NightRest.EnlistedNow) return "In service your lord decides what the men do.";
            if (mp == null || (mp.Army != null && mp.Army.LeaderParty != mp)) return "You march in another lord's army - he decides.";
            if (!AtVillage()) return "You are not at the village.";
            if (st.Village.VillageState == Village.VillageStates.BeingRaided) return "Other raiders are already at work on these lands.";
            if (me == null) return "Nobody knows this village yet (the village book is not running).";
            if (Burned(me)) return "The village is burned - there is nothing left to take.";
            var p = ProtOf(_uid, f);
            if (p != null) return "It paid your realm tribute - spared for " + (p.Until - Today()) + " more days.";
            if (f != null && RaidTribute.Spared(st, f, Today())) return Txt(st.Name) + " paid your realm tribute for all its lands.";
            if (tribute)
            {
                int d;
                if (_lastRaid.TryGetValue(_uid, out d) && Today() - d < HostileMemoryDays) return "You raided it " + (Today() - d) + " days ago - it will not pay you now.";
            }
            return null;
        }

        // ------------------------------------------------------------ menu
        private static bool HostileCondition(MenuCallbackArgs a)
        {
            a.optionLeaveType = GameMenuOption.LeaveType.HostileAction;
            try
            {
                var st = DistOf(Rec(_uid));
                if (st == null) return false;
                if (!AtWar(st)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}You are not at war with " + Txt(st.MapFaction != null ? st.MapFaction.Name : null) + " - declare war first."); }
                return true;
            }
            catch (Exception e) { Stumble("HostileCondition", e); return false; }
        }

        private static void AddRaidMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenu(HostileMenu, "{=!}{ARM_SIDEVIL_HOSTILE}", HostileInit, GameMenu.MenuOverlayType.None);
            starter.AddGameMenuOption(HostileMenu, "arm_sidevil_raid_opt", "{=!}Raid the village", RaidCondition, RaidStart, false, 0);
            starter.AddGameMenuOption(HostileMenu, "arm_sidevil_tribute", "{=!}Demand tribute", TributeCondition, TributeConsequence, false, 1);
            starter.AddGameMenuOption(HostileMenu, "arm_sidevil_back", "{=!}Forget it",
                delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                delegate (MenuCallbackArgs a) { try { GameMenu.SwitchToMenu(Menu); } catch (Exception e) { Stumble("Back", e); } }, true, 9);
            starter.AddWaitGameMenu(RaidMenu, "{=!}{ARM_SIDEVIL_RAID}", RaidInit, delegate (MenuCallbackArgs a) { return true; }, null, RaidTick,
                GameMenu.MenuAndOptionType.WaitMenuShowProgressAndHoursOption);
            starter.AddGameMenuOption(RaidMenu, "arm_sidevil_raid_stop", "{=!}Stop raiding",
                delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                // z opcji menu oczekiwania nie wolno SwitchToMenu (CLAUDE.md 7) - flaga, tick wraca do menu wioski (wzor HideoutPurge "Call the search off"); zegar ruszamy sami
                delegate (MenuCallbackArgs a)
                {
                    if (_raid != null) _raid.Stop = true;
                    try { Campaign.Current.TimeControlMode = CampaignTimeControlMode.StoppablePlay; a.MenuContext.GameMenu.StartWait(); } catch { }
                }, true, 9);
        }

        private static void HostileInit(MenuCallbackArgs args)
        {
            try
            {
                var r = Rec(_uid); var st = DistOf(r);
                try { string mesh = st != null && st.SettlementComponent != null ? st.SettlementComponent.WaitMeshName : null; args.MenuContext.SetBackgroundMeshName(string.IsNullOrEmpty(mesh) ? "wait_fallback" : mesh); } catch { }
                var sb = new StringBuilder(300);
                if (st == null) sb.Append("There is nothing here.");
                else
                {
                    VillageBook.PartView me; double homeAll, nAll, share;
                    PartOf(st, _uid, out me, out homeAll, out nAll);
                    int def = Defenders(st, me, homeAll, out share);
                    sb.Append("The people of ").Append(NameOf(r)).Append(" watch you from behind their doors. The militia of ").Append(Txt(st.Name)).Append(" - about ").Append(def)
                      .Append(def == 1 ? " man" : " men").Append(" - keeps watch over all its lands and would come to drive you off. You have ").Append(MenNow()).Append(" fit men.");
                    if (def > MenNow()) sb.Append(" Beat the militia at ").Append(Txt(st.Name)).Append(" first, or come with more men.");
                }
                MBTextManager.SetTextVariable("ARM_SIDEVIL_HOSTILE", sb.ToString());
            }
            catch (Exception e) { Stumble("HostileInit", e); }
        }

        /// <summary>Plan rabunku wioski: praca (ludzie osad wioski do pulapu i do Room okregu), godziny (R x N na dobe), ludzie okregu w domu (udzial lupu). why - powod "nie".</summary>
        private static bool RaidPlan(Settlement st, VillageBook.PartView me, double nAll, int men, out double work, out double hours, out double home0, out string why)
        {
            work = hours = home0 = 0; why = null;
            var s = Settings.Current;
            if (s == null || !(s.DevastationPerRaiderDay > 0f)) { why = "Raiding needs the devastation rules (mod settings)."; return false; }
            double home, away, open, reach;
            if (!Devastation.DistrictPeople(st.Village, out home, out away, out open, out reach)) { why = "Raiding needs the devastation rules (mod settings)."; return false; }
            if (me == null || !(nAll > 0)) { why = "Nobody knows this village yet."; return false; }
            double perS = (home + away) / nAll;
            double room = Devastation.MaxShare(s) * me.Nw - me.Br - me.Bf;
            work = Math.Min(Math.Max(0.0, room) * perS, open);
            if (!(work >= 1.0)) { work = 0; why = "There is nothing left to take here."; return false; }
            if (men <= 0) { why = "You have no fit men."; return false; }
            hours = Math.Max(1.0, work / (s.DevastationPerRaiderDay * (double)men) * 24.0);
            home0 = home;
            return true;
        }

        private static bool RaidCondition(MenuCallbackArgs a)
        {
            a.optionLeaveType = GameMenuOption.LeaveType.Raid;
            try
            {
                var r = Rec(_uid); var st = DistOf(r);
                if (st == null) return false;
                VillageBook.PartView me; double homeAll, nAll, share;
                PartOf(st, _uid, out me, out homeAll, out nAll);
                string why = HostileBlock(st, me, false);
                int men = MenNow(), def = Defenders(st, me, homeAll, out share);
                double work = 0, hours = 0, home0 = 0;
                if (why == null && men < Math.Max(1, def)) why = "The militia of " + Txt(st.Name) + " - about " + def + " men - would drive you off. You have " + men + ".";
                if (why == null) RaidPlan(st, me, nAll, men, out work, out hours, out home0, out why);
                if (why != null) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); return true; }
                double s0 = home0 > 0 ? Math.Min(1.0, work / home0) : 0;
                a.Tooltip = new TextObject("{=!}Burn and plunder " + NameOf(r) + " alone - " + Math.Round(work).ToString("N0", Inv) + " people to drive off, about " + FmtHours(hours)
                                           + ". You take its share of the purse and the store of " + Txt(st.Name) + " (about " + Math.Round(100 * s0).ToString("0", Inv) + "%: "
                                           + Math.Round(s0 * Math.Max(0, st.Village.Gold)).ToString("N0", Inv) + " denars). " + Txt(st.Name) + " itself is not raided. Its lord will hate you for it.");
                return true;
            }
            catch (Exception e) { Stumble("RaidCondition", e); return false; }
        }

        private static string FmtHours(double h) { return h < 24 ? Math.Round(h).ToString("0", Inv) + " hours" : (h / 24.0).ToString("0.#", Inv) + " days"; }

        private static void RaidStart(MenuCallbackArgs a)
        {
            try
            {
                var r = Rec(_uid); var st = DistOf(r);
                if (st == null) return;
                VillageBook.PartView me; double homeAll, nAll, share;
                PartOf(st, _uid, out me, out homeAll, out nAll);
                int men = MenNow(), def = Defenders(st, me, homeAll, out share);
                double work, hours, home0; string why = HostileBlock(st, me, false);
                if (why != null || men < Math.Max(1, def) || !RaidPlan(st, me, nAll, men, out work, out hours, out home0, out why)) { Log.Player("You cannot raid " + NameOf(r) + " now.", true); return; }
                bool slaver = false;
                try { slaver = Hero.MainHero.MapFaction != null && LosersFlee.SlaveCulture(Hero.MainHero.MapFaction.Culture); } catch { }
                _raid = new Raid { Uid = _uid, Dist = st.StringId, Name = NameOf(r), Work = work, Hours = hours, Home0 = home0, Men = men, Day = Today(), SlaverSet = true,
                                   Rs = new Devastation.RaidState { Slaver = slaver, MaxMen = men } };
                _dRaidStart++;
                Log.Info(P + "rabunek " + _raid.Name + " (" + _uid + ", okreg " + st.StringId + ") - start: ludzi do przeszukania " + work.ToString("0.0", Inv) + " (osady " + me.Nw.ToString("0.0", Inv)
                         + ", puste " + (me.Br + me.Bf).ToString("0.0", Inv) + "), napastnikow " + men + ", obroncow " + def + " (udzial wioski " + (100 * share).ToString("0.0", Inv) + "%), "
                         + hours.ToString("0.0", Inv) + " h, ludzie okregu w domu " + home0.ToString("0", Inv) + ", kiesa wsi " + st.Village.Gold + ".");
                GameMenu.SwitchToMenu(RaidMenu);   // zwykle menu wioski -> menu oczekiwania (jak sen)
            }
            catch (Exception e) { _raid = null; Stumble("RaidStart", e); }
        }

        [GameMenuInitializationHandler(RaidMenu)]
        public static void RaidSound(MenuCallbackArgs args)
        {
            try
            {
                if (args == null || args.MenuContext == null) return;
                args.MenuContext.SetBackgroundMeshName("wait_raiding_village");
                args.MenuContext.SetAmbientSound("event:/map/ambient/node/settlements/2d/village_raided");
            }
            catch (Exception e) { Stumble("RaidSound", e); }
        }

        private static void RaidInit(MenuCallbackArgs args)
        {
            try
            {
                if (_raid != null)
                {
                    _raid.Misses = 0;
                    if (_raid.Name == null || _raid.Name == "?") _raid.Name = NameOf(Rec(_raid.Uid));
                    MBTextManager.SetTextVariable("ARM_SIDEVIL_RAID", "Your men go through " + _raid.Name + " house by house - driving off the people, taking what can be carried and burning the rest.");
                    args.MenuContext.GameMenu.StartWait();
                    args.MenuContext.GameMenu.SetProgressOfWaitingInMenu((float)Math.Min(1.0, _raid.HoursDone / Math.Max(0.01, _raid.Hours)));
                }
                else
                {
                    MBTextManager.SetTextVariable("ARM_SIDEVIL_RAID", "The raid is over.");
                    args.MenuContext.GameMenu.StartWait();
                }
            }
            catch (Exception e) { Stumble("RaidInit", e); }
        }

        private static void RaidTick(MenuCallbackArgs args, CampaignTime dt)
        {
            try
            {
                var rd = _raid;
                if (rd == null) { GameMenu.ExitToLast(); return; }
                if (rd.Name == null || rd.Name == "?") rd.Name = NameOf(Rec(rd.Uid));   // po wczytaniu (ksiega czytana pozniej niz zapis)   // stan rabunku zgubiony (stary zapis) - na mape (z ticku wolno)
                rd.Misses = 0;
                if (!rd.SlaverSet && rd.Rs != null)
                {
                    rd.SlaverSet = true;
                    try { rd.Rs.Slaver = Hero.MainHero != null && Hero.MainHero.MapFaction != null && LosersFlee.SlaveCulture(Hero.MainHero.MapFaction.Culture); } catch { rd.Rs.Slaver = false; }
                }
                if (rd.Stop) { FinishRaid("przerwany przez gracza (Stop raiding)", 1); GameMenu.SwitchToMenu(Menu); return; }
                double h = Math.Max(0.0, dt.ToHours);
                rd.HoursDone += h; rd.Pend += h;
                bool end = rd.HoursDone >= rd.Hours;
                if (rd.Pend >= 1.0 || end)
                {
                    double ph = rd.Pend;   // godziny od poprzedniego uderzenia - osobodni tego uderzenia (limit zabitych: 1 na zbrojnego na dobe)
                    rd.Pend = 0;
                    double target = rd.Work * Math.Min(1.0, rd.HoursDone / Math.Max(0.01, rd.Hours));
                    double people = target - rd.Done;
                    if (people > 1e-6)
                    {
                        double hit = StrikeChunk(rd, people, Math.Max(ph, 0.05));
                        rd.Done = target;
                        if (!(hit > 1e-6)) end = true;   // nic juz nie trafia (okreg nasycony, ksiega nieczynna) - koniec
                    }
                }
                if (end) { FinishRaid(rd.HoursDone >= rd.Hours ? "pelny" : "nic wiecej do spalenia", 0); GameMenu.SwitchToMenu(Menu); return; }
                args.MenuContext.GameMenu.SetProgressOfWaitingInMenu((float)Math.Min(1.0, rd.HoursDone / Math.Max(0.01, rd.Hours)));
            }
            catch (Exception e) { Stumble("RaidTick", e); }
        }

        /// <summary>Uderzenie: Devastation.StrikePeople (rdzen rabunku osada po osadzie) z VillageBook.ForceUid = ta wioska - ksiega pali tylko jej osady. Recenzja 20.10: uderzenie
        /// nie wieksze niz miejsce wioski TERAZ (osady x ludzie okregu / osady okregu - ta sama miara co ksiega) - ludzie okregu maleja z kazdym uderzeniem i wioska zapelnia sie
        /// przed koncem pracy; bez obciecia ludzie szliby z domow innych wiosek bez palenia. Wioska pelna - 0 (koniec rabunku).</summary>
        private static double StrikeChunk(Raid rd, double people, double hours)
        {
            var st = Settlement.Find(rd.Dist);
            if (st == null || st.Village == null) return 0;
            double room, nb, hh, aw, op, rc;
            if (VillageBook.UidRoom(st.Village, rd.Uid, out room, out nb) && Devastation.DistrictPeople(st.Village, out hh, out aw, out op, out rc))
            {
                double cap = room * (hh + aw) / nb;
                if (cap < people) { people = cap; _dRaidCapped++; }
                if (!(people > 1e-6)) return 0;
            }
            double pd = Math.Max(1e-3, rd.Men * hours / 24.0);
            double dead, fled, refugees, hit;
            VillageBook.ForceUid = rd.Uid;
            try { hit = Devastation.StrikePeople(st.Village, people, pd, rd.Rs, MobileParty.MainParty, out dead, out fled, out refugees); }
            finally { VillageBook.ForceUid = null; }
            if (hit > 0) { rd.Hit += hit; rd.Dead += dead; rd.Fled += fled; _dStrikes++; _dRaidHit += hit; _dRaidDead += dead; }
            return hit;
        }

        /// <summary>Straznik (Tick, 4 razy na sekunde): rabunek w toku, a menu rabunku nie jest otwarte (bitwa, spotkanie, niewola, menu zamkniete) - 3 sprawdzenia z rzedu
        /// (wczytanie w srodku menu oczekiwania - menu wraca chwile po starcie) i rabunek zamkniety za zrobiona czesc.</summary>
        private static void RaidGuard()
        {
            var rd = _raid;
            if (rd == null) return;
            var c = Campaign.Current;
            var mc = c != null ? c.CurrentMenuContext : null;
            string id = mc != null && mc.GameMenu != null ? mc.GameMenu.StringId : null;
            if (id == RaidMenu) { rd.Misses = 0; return; }
            if (++rd.Misses < 8) return;   // 2 s: po wczytaniu menu oczekiwania wraca chwile po pierwszym ticku
            FinishRaid("przerwany (menu " + (id ?? "zamkniete") + ")", 2);
        }

        /// <summary>Koniec rabunku: lup za zrobiona czesc (partia aktywna, nie w niewoli), relacja, pamiec, linia logu, komunikat. how: 0 pelny, 1 gracz, 2 inne.</summary>
        private static void FinishRaid(string why, int how)
        {
            var rd = _raid;
            _raid = null;
            if (rd == null) return;
            if (rd.Name == null || rd.Name == "?") rd.Name = NameOf(Rec(rd.Uid));
            _dRaidEnd++; if (how == 1) _dRaidStop++; else if (how == 2) _dRaidCut++;
            long gold = 0, take = 0, burn = 0; int herd = 0;
            var st = Settlement.Find(rd.Dist);
            try
            {
                var mp = MobileParty.MainParty;
                bool alive = mp != null && mp.IsActive && Hero.MainHero != null && Hero.MainHero.IsAlive && !PlayerCaptivity.IsCaptive;
                double s = rd.Home0 > 0 ? Math.Max(0.0, Math.Min(1.0, rd.Hit / rd.Home0)) : 0.0;
                if (alive && st != null && st.Village != null && s > 0) Loot(st, s, mp, out gold, out take, out burn, out herd);
                if (rd.Hit > 0.5 && st != null) Relations(st, RelLordRaid, RelNotableRaid);
                _lastRaid[rd.Uid] = Today();
                _dRaidGold += gold; _dRaidTake += take; _dRaidBurn += burn;
                VillageBook.PartView me; double h0, n0;
                string state = st != null && PartOf(st, rd.Uid, out me, out h0, out n0) ? (Burned(me) ? "spalona" : "puste " + (me.Br + me.Bf).ToString("0.0", Inv) + " z " + me.Nw.ToString("0.0", Inv) + " osad") : "?";
                Log.Info(P + "rabunek " + rd.Name + " (" + rd.Uid + ", okreg " + rd.Dist + ") - koniec: " + why + " | " + rd.HoursDone.ToString("0.0", Inv) + " z " + rd.Hours.ToString("0.0", Inv)
                         + " h, przeszukani " + rd.Done.ToString("0.0", Inv) + " z " + rd.Work.ToString("0.0", Inv) + ", trafieni " + rd.Hit.ToString("0.0", Inv) + " (zabici " + rd.Dead.ToString("0.0", Inv)
                         + ", las " + rd.Fled.ToString("0.0", Inv) + ") | lup (udzial " + (100.0 * s).ToString("0.00", Inv) + "% ludzi okregu w domu): zloto " + gold + " z kiesy wsi, zwierzeta stada " + herd
                         + ", sztuki do taboru " + take + ", spalone " + burn + " | wioska: " + state + (alive ? "" : " | bez lupu (partia gracza nieaktywna albo w niewoli)") + ".");
                Example(rd.Name + " " + rd.Hit.ToString("0", Inv) + " lud., " + gold + " zl");
                if (alive)
                    Log.Player(rd.Hit > 0.5
                        ? "You raided " + rd.Name + ": " + Math.Round(rd.Hit).ToString("N0", Inv) + " people driven off" + (rd.Dead >= 0.5 ? " (" + Math.Round(rd.Dead).ToString("N0", Inv) + " killed)" : "")
                          + ", " + gold.ToString("N0", Inv) + " denars and " + take.ToString("N0", Inv) + " goods taken" + (burn > 0 ? ", " + burn.ToString("N0", Inv) + " burned" : "") + "."
                        : "Your men found nothing in " + rd.Name + ".", true);
            }
            catch (Exception e) { Stumble("FinishRaid", e); }
        }

        /// <summary>Lup (DistrictRaid.Loot w malym): zloto s x kiesa wsi (SmallGold.PayRaidLoot), stado, towar s z magazynu wsi okregu do taboru gracza w ramce FRaid.</summary>
        private static void Loot(Settlement st, double s, MobileParty mp, out long gold, out long take, out long burn, out int herd)
        {
            gold = take = burn = 0; herd = 0;
            var v = st.Village;
            try { herd = VillageHerds.OnRaid(v, s, mp); } catch (Exception e) { Stumble("Loot(stado)", e); }
            try
            {
                long due = (long)Math.Round(s * Math.Max(0, v.Gold));
                gold = SmallGold.PayRaidLoot(v, Hero.MainHero, due);
            }
            catch (Exception e) { Stumble("Loot(zloto)", e); }
            try
            {
                var roster = st.ItemRoster;
                if (roster == null || roster.Count == 0) return;
                var plan = new List<KeyValuePair<EquipmentElement, int>>();
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0 || el.EquipmentElement.Item == null) continue;
                    int want = MBRandom.RoundRandomized(el.Amount * (float)s);
                    if (want > el.Amount) want = el.Amount;
                    if (want > 0) plan.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, want));
                }
                if (plan.Count == 0) return;
                plan.Sort((x, y) =>
                {
                    bool xa = Walks(x.Key.Item), ya = Walks(y.Key.Item);
                    if (xa != ya) return xa ? -1 : 1;
                    return PerKg(y.Key.Item).CompareTo(PerKg(x.Key.Item));
                });
                double free = 0;
                try { free = Math.Max(0.0, mp.InventoryCapacity - mp.TotalWeightCarried); } catch { free = 0; }
                var mvEl = new List<EquipmentElement>(); var mvN = new List<int>();
                var gf = GoodsLedger.Begin(GoodsLedger.FRaid, st);
                try
                {
                    foreach (var kv in plan)
                    {
                        var el = kv.Key; var it = el.Item; int n = kv.Value;
                        roster.AddToCounts(el, -n);
                        int fit = n;
                        if (!Walks(it))
                        {
                            double w = Math.Max(0f, it.Weight);
                            fit = w > 1e-6 ? (int)Math.Min(n, Math.Floor(free / w)) : n;
                            if (fit < 0) fit = 0;
                            free -= fit * w;
                        }
                        if (fit > 0) { mp.ItemRoster.AddToCounts(el, fit); mvEl.Add(el); mvN.Add(fit); take += fit; }
                        if (n - fit > 0) { burn += n - fit; ArmsBook.Sink(ArmsBook.KRaid, it, n - fit); }
                    }
                }
                finally { GoodsLedger.End(gf); }
                // lista zdarzenia gry PO ramce (jak DistrictRaid.Loot - podsluch ksiegi nie liczy jej jako ruchu), zdjeta zaraz po zdarzeniu
                var got = new ItemRoster();
                for (int k = 0; k < mvEl.Count; k++) got.AddToCounts(mvEl[k], mvN[k]);
                if (got.Count > 0)
                {
                    try { CampaignEventDispatcher.Instance.OnItemsLooted(mp, got); } catch (Exception e) { Stumble("Loot(OnItemsLooted)", e); }
                    for (int k = 0; k < mvEl.Count; k++) got.AddToCounts(mvEl[k], -mvN[k]);
                }
            }
            catch (Exception e) { Stumble("Loot(towar)", e); }
        }

        private static bool Walks(ItemObject it) { return it != null && (it.HasHorseComponent || it.IsAnimal); }
        private static double PerKg(ItemObject it) { return it == null ? 0.0 : it.Value / Math.Max(0.1, (double)it.Weight); }

        private static void Relations(Settlement st, int lordDelta, int notableDelta)
        {
            try
            {
                var me = Hero.MainHero;
                if (me == null || st == null) return;
                if (ReferenceEquals(st.OwnerClan, Clan.PlayerClan)) return;   // wlasna ziemia - bez zmiany (jak gra przy rabunku wsi rodu gracza)
                var lord = st.OwnerClan != null ? st.OwnerClan.Leader : null;
                if (lord != null && lord != me && lordDelta != 0) ChangeRelationAction.ApplyRelationChangeBetweenHeroes(me, lord, lordDelta);
                if (notableDelta != 0)
                {
                    var ns = st.Notables;
                    if (ns != null) for (int i = 0; i < ns.Count; i++) if (ns[i] != null && ns[i] != me) ChangeRelationAction.ApplyRelationChangeBetweenHeroes(me, ns[i], notableDelta);
                }
            }
            catch (Exception e) { Stumble("Relations", e); }
        }

        // ------------------------------------------------------------ R2 okup
        private static double Rate(double q)
        {
            var s = Settings.Current;
            double lo = s != null ? Math.Max(0.0, Math.Min(1.0, s.RaidTributeShareLow)) : 0.10;
            double hi = s != null ? Math.Max(lo, Math.Min(1.0, s.RaidTributeShareHigh)) : 0.20;
            return lo + (hi - lo) * Math.Max(0.0, Math.Min(1.0, (q - 1.0) / 4.0));   // ta sama regula co R3-03a (1:1 - dolna, 5:1 - gorna)
        }

        private static int SpareDays { get { var s = Settings.Current; return s != null ? Math.Max(1, s.RaidTributeSpareDays) : 30; } }

        private static long TributeOf(Settlement st, VillageBook.PartView me, double homeAll, out double rate, out double q, out int def, out int men, out double share)
        {
            men = MenNow();
            def = Defenders(st, me, homeAll, out share);
            q = (double)men / Math.Max(1, def);
            rate = Rate(q);
            if (q < 1.0) return 0;
            return (long)Math.Floor(rate * Math.Max(0, st.Village.Gold) * share);
        }

        private static bool TributeCondition(MenuCallbackArgs a)
        {
            a.optionLeaveType = GameMenuOption.LeaveType.Bribe;
            try
            {
                var r = Rec(_uid); var st = DistOf(r);
                if (st == null || !RaidTribute.On) return false;
                VillageBook.PartView me; double homeAll, nAll;
                PartOf(st, _uid, out me, out homeAll, out nAll);
                string why = HostileBlock(st, me, true);
                double rate = 0, q = 0, share = 0; int def = 0, men = 0; long t = 0;
                if (why == null)
                {
                    t = TributeOf(st, me, homeAll, out rate, out q, out def, out men, out share);
                    if (q < 1.0) why = "The militia of " + Txt(st.Name) + " - about " + def + " men - would rather fight. You have " + men + ".";
                    else if (t <= 0) why = "The village has nothing to pay.";
                }
                if (why != null) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); return true; }
                a.Tooltip = new TextObject("{=!}" + NameOf(r) + " pays " + t.ToString("N0", Inv) + " denars (" + Math.Round(100 * rate).ToString("0", Inv) + "% of its share of the purse of " + Txt(st.Name)
                                           + ": " + Math.Round(100 * share).ToString("0", Inv) + "% of " + st.Village.Gold.ToString("N0", Inv) + "; your " + men + " men against " + def
                                           + " of its militia) and your realm spares it for " + SpareDays + " days: you will not raid it, and raids on " + Txt(st.Name) + " by your realm pass it by.");
                return true;
            }
            catch (Exception e) { Stumble("TributeCondition", e); return false; }
        }

        private static void TributeConsequence(MenuCallbackArgs a)
        {
            try
            {
                var r = Rec(_uid); var st = DistOf(r);
                if (st == null) return;
                VillageBook.PartView me; double homeAll, nAll;
                PartOf(st, _uid, out me, out homeAll, out nAll);
                if (HostileBlock(st, me, true) != null) return;
                double rate, q, share; int def, men;
                long t = TributeOf(st, me, homeAll, out rate, out q, out def, out men, out share);
                if (q < 1.0 || t <= 0 || st.Party == null) return;
                long g0 = Hero.MainHero.Gold, v0 = st.Village.Gold;
                GiveGoldAction.ApplyForPartyToCharacter(st.Party, Hero.MainHero, (int)Math.Min(int.MaxValue, t), false);
                long paid = Hero.MainHero.Gold - g0;
                var f = Hero.MainHero.MapFaction;
                if (f != null)
                {
                    Prot p = null;
                    foreach (var x in _prot) if (x.Uid == _uid && x.Fid == f.StringId) { p = x; break; }
                    if (p == null) { p = new Prot { Uid = _uid, Fid = f.StringId }; _prot.Add(p); }
                    p.Day = Today(); p.Until = Today() + SpareDays; p.Gold = paid; p.Share = share; p.Dist = st.StringId;
                    ProtSync();
                }
                Relations(st, RelLordTribute, 0);
                _dTrib++; _dTribGold += paid;
                Log.Info(P + "okup " + NameOf(r) + " (" + _uid + ", okreg " + st.StringId + "): " + paid + " zl (stawka " + (100 * rate).ToString("0.0", Inv) + "%, udzial wioski " + (100 * share).ToString("0.0", Inv)
                         + "%, kiesa wsi " + v0 + " -> " + st.Village.Gold + ", kontrola: gracz +" + paid + " / wies -" + (v0 - st.Village.Gold) + "), ludzie " + men + " : obroncy " + def
                         + ", ochrona do doby " + (Today() + SpareDays) + " dla " + (f != null ? f.StringId : "-") + ".");
                Example("okup " + NameOf(r) + " " + paid + " zl");
                Log.Player(NameOf(r) + " paid you " + paid.ToString("N0", Inv) + " denars of tribute. Your realm will spare it for " + SpareDays + " days.");
                GameMenu.SwitchToMenu(Menu);   // zwykle menu - wolno
            }
            catch (Exception e) { Stumble("Tribute", e); }
        }

        // ------------------------------------------------------------ zapis arm_sidevil: "sv1|t=uid,fid,do,doba,zloto[,udzial,okreg];...|l=uid,doba;...|r=uid,okreg,praca,zrobione,godziny,minelo,trafieni,dom0,ludzie,doba"
        // (recenzja 20.10: udzial i okreg okupu dopisane na koncu wpisu t - wpis bez nich czytany jak dotad, udzial 0)
        internal static string Export()
        {
            int today = Today();
            var sb = new StringBuilder(64);
            sb.Append(SaveVer).Append("|t=");
            bool first = true;
            foreach (var p in _prot)
            {
                if (p.Until <= today) continue;
                if (!first) sb.Append(';'); first = false;
                sb.Append(p.Uid).Append(',').Append(p.Fid).Append(',').Append(p.Until.ToString(Inv)).Append(',').Append(p.Day.ToString(Inv)).Append(',').Append(p.Gold.ToString(Inv))
                  .Append(',').Append(p.Share.ToString("R", Inv)).Append(',').Append(p.Dist ?? "");
            }
            sb.Append("|l=");
            first = true;
            foreach (var kv in _lastRaid)
            {
                if (today - kv.Value >= HostileMemoryDays) continue;
                if (!first) sb.Append(';'); first = false;
                sb.Append(kv.Key).Append(',').Append(kv.Value.ToString(Inv));
            }
            sb.Append("|r=");
            var rd = _raid;
            if (rd != null)
                sb.Append(rd.Uid).Append(',').Append(rd.Dist).Append(',').Append(rd.Work.ToString("R", Inv)).Append(',').Append(rd.Done.ToString("R", Inv)).Append(',').Append(rd.Hours.ToString("R", Inv))
                  .Append(',').Append(rd.HoursDone.ToString("R", Inv)).Append(',').Append(rd.Hit.ToString("R", Inv)).Append(',').Append(rd.Home0.ToString("R", Inv)).Append(',').Append(rd.Men.ToString(Inv))
                  .Append(',').Append(rd.Day.ToString(Inv));
            string x = sb.ToString();
            return x == SaveVer + "|t=|l=|r=" ? "" : x;
        }

        internal static void Import(string data)
        {
            RaidReset();
            if (string.IsNullOrEmpty(data)) return;
            try
            {
                var f = data.Split('|');
                if (f.Length < 1 || f[0] != SaveVer) { Log.Info(P + "zapis arm_sidevil nieznany (" + data.Length + " znakow) - pominiety."); return; }
                int bad = 0;
                for (int i = 1; i < f.Length; i++)
                {
                    string sec = f[i];
                    if (sec.StartsWith("t=", StringComparison.Ordinal))
                        foreach (var e in sec.Substring(2).Split(';'))
                        {
                            if (e.Length == 0) continue;
                            var a = e.Split(',');
                            int until, day; long gold;
                            if (a.Length < 5 || a[0].Length == 0 || a[1].Length == 0 || !int.TryParse(a[2], NumberStyles.Integer, Inv, out until) || !int.TryParse(a[3], NumberStyles.Integer, Inv, out day)
                                || !long.TryParse(a[4], NumberStyles.Integer, Inv, out gold)) { bad++; continue; }
                            double sh = 0.0;
                            if (a.Length >= 6 && !double.TryParse(a[5], NumberStyles.Float, Inv, out sh)) sh = 0.0;
                            _prot.Add(new Prot { Uid = a[0], Fid = a[1], Until = until, Day = day, Gold = gold, Share = Math.Max(0.0, Math.Min(1.0, sh)),
                                                 Dist = a.Length >= 7 && a[6].Length > 0 ? a[6] : null });
                        }
                    else if (sec.StartsWith("l=", StringComparison.Ordinal))
                        foreach (var e in sec.Substring(2).Split(';'))
                        {
                            if (e.Length == 0) continue;
                            var a = e.Split(',');
                            int day;
                            if (a.Length < 2 || a[0].Length == 0 || !int.TryParse(a[1], NumberStyles.Integer, Inv, out day)) { bad++; continue; }
                            _lastRaid[a[0]] = day;
                        }
                    else if (sec.StartsWith("r=", StringComparison.Ordinal) && sec.Length > 2)
                    {
                        var a = sec.Substring(2).Split(',');
                        double work, done, hours, hd, hit, home0; int men, day;
                        if (a.Length < 10 || !double.TryParse(a[2], NumberStyles.Float, Inv, out work) || !double.TryParse(a[3], NumberStyles.Float, Inv, out done)
                            || !double.TryParse(a[4], NumberStyles.Float, Inv, out hours) || !double.TryParse(a[5], NumberStyles.Float, Inv, out hd) || !double.TryParse(a[6], NumberStyles.Float, Inv, out hit)
                            || !double.TryParse(a[7], NumberStyles.Float, Inv, out home0) || !int.TryParse(a[8], NumberStyles.Integer, Inv, out men) || !int.TryParse(a[9], NumberStyles.Integer, Inv, out day))
                        { bad++; continue; }
                        _raid = new Raid { Uid = a[0], Dist = a[1], Work = work, Done = done, Hours = hours, HoursDone = hd, Hit = hit, Home0 = home0, Men = Math.Max(1, men), Day = day,
                                           Rs = new Devastation.RaidState { MaxMen = men } };
                        _uid = a[0];
                    }
                }
                if (_raid != null) _raid.Name = NameOf(Rec(_raid.Uid));
                System.Threading.Volatile.Write(ref _protSnap, _prot.Count == 0 ? NoProt : _prot.ToArray());   // bez zdejmowania wygaslych - doba przy wczytaniu moze byc jeszcze nieznana
                Log.Info(P + "zapis: ochron " + _prot.Count + ", rabunkow gracza w pamieci " + _lastRaid.Count + ", rabunek w toku " + (_raid != null ? _raid.Uid : "nie") + (bad > 0 ? ", zlych wpisow " + bad + " (pominiete)" : "") + ".");
            }
            catch (Exception e) { RaidReset(); Stumble("Import", e); }
        }
    }
}
