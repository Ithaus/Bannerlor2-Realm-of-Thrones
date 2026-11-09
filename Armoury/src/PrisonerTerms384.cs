using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 384 (Jeff 09.11: "nie sprzedam ich, lub wymienie ich za odstapienie od oblezenia i nie atakowanie moich ziem, albo inne warunki rozejmu";
    /// "daj warunki w parly ze moge ziemie lub moge rozmawiac z dowodca oblezenia zeby odstapil od oblezenia za jencow, lub przepuscil mnie na slowo honoru
    /// lub inne elementy ktore mozna bylo dogadac paktujac z lordem przeciwnej strony i majac np. jencow zloto etc."). PERTRAKTACJE Z WODZEM OBLEZENIA.
    /// Klucz PrisonerExchangeForTerms, dlugosc rozejmu / paktu PrisonerExchangePactDays (60 dob = 1/6 roku kalendarza Armoury 364 - ta sama miara co
    /// SiegeRansomTruceDays okupu, jedna miara rozejmu).
    ///
    /// Skad rozmowa: ta sama co 374 (gracz w oblezonej twierdzy, "Request a parley" gry -> rozmowa z wodzem obozu; warunki jak SiegeSurrender374.AskCond).
    /// Linia w hero_main_options (priorytet 104, obok 374): ZADANIE (co gracz chce) -> OFERTA (co daje: jency-lordowie frakcji wodza z lochow rodu
    /// gracza i z jego druzyny - 8 najcenniejszych osobno i "wszyscy", zloto 5 000 / 20 000, zamek albo miasto rodu gracza - do 3, bez oblezonych)
    /// -> ocena wodza -> zgoda / kontroferta (najwyzej 2 rundy: dodaj zloto albo jeszcze jednego jenca) / odmowa (3 doby przerwy). Bez okien - same linie
    /// rozmowy (zadnego okna nad misja rozmowy, zadnego SwitchToMenu).
    /// ZADANIA:
    ///  L - odstapienie: oboz zwiniety droga gry (SiegeRansom.Lift384 = RemoveAllSiegeParties w takcie kampanii, jak okup), rozejm tej osady N dob dla frakcji
    ///      i rodow obozu (SiegeRansom.Truce384 - ta sama tablica, latka celu i nasluch zlamania co okup);
    ///  A - pakt: L + frakcja i rody obozu nie celuja w osady i wsie rodu gracza N dob (ScorePact w SiegeRansom.ScorePost: oblezenie i najazd -> wynik 0)
    ///      i ich druzyny nie atakuja druzyny gracza (SetDoNotAttackMainParty 25 h co dobe);
    ///  P - swobodne przejscie: gracz z druzyna wychodzi (menu arm384_pass po rozmowie - SetNextMenu jak 374/gra przy SafePassageBarterable, potem jak gry
    ///      "Leave": FinalizePlayerSiege, LeaveSettlement, Finish), twierdza zostaje z zaloga, oboz nie atakuje gracza 48 h.
    ///  Rozejm miedzy krolestwami - linia widoczna, nieczynna z powodem (gracz nie jest wladca; wladca - pokoj zawiera sie na dworze, nie pod murami).
    /// OCENA (denary): V = jency (Ransom178.PriceFor; rod wladcy/nastepca x3, krewni i rod wodza x1.5; relacja wodza z jencem 0.5-1.5; jency w lochu OBLEZONEJ
    ///   twierdzy x (1 - 0.7 p) - wodz i tak ich uwolni, gdy zdobedzie mury; Mercy +10%/poziom) + zloto + ziemia (SettlementValueModel dla rodu wodza);
    ///   Honor > 0: +5%/poziom. C = L: p x wartosc osady x (1 + 0.15 Valor); P: p x (okup gracza + 0.3 wartosci wojskowej druzyny) + 2000; A: L + (N/60) x 0.1 x
    ///   wartosc pozostalych twierdz gracza; relacja wodza z graczem x (1 - rel/200). p = szansa zdobycia (sila obozu do murow R, odparte szturmy i straty
    ///   z 374, jedzenie, 0.05-0.95). Zgoda: V >= C x prog (Calculating > 0: 1.0, < 0: 0.9, inaczej 0.95); kontroferta od 0.7 progu (Calculating > 0: 0.8).
    /// ZDRADA (losowana przy zgodzie, SiegeSurrender374.BetrayChance - tylko Honor < 0): L/A - oboz odchodzi i bierze jencow, ale rozejmu / paktu nie
    ///   dotrzymuje (wpisy tylko pilnowane); powrot w czasie umowy = linia ZDRADA, klamca (lista 374), relacja -30, honorowe glowy rodow -5 x Honor.
    ///   P - jency i zloto oddane przy bramie, potem pojmanie gracza i towarzyszy, szeregowi do lochu (jak RunBetray 374, bez zmiany pana twierdzy).
    /// ZLAMANIE PRZEZ GRACZA (atak na druzyne / najazd / oblezenie frakcji z umowa w czasie umowy): wpisy tej umowy zdjete (takze rozejm osady),
    ///   relacja z przysiegajacym -20 i z wladca jego frakcji -10, Honor gracza -500 xp (TraitLevelingHelper.OnIncidentResolved).
    /// WYKONANIE tylko w takcie kampanii (bez misji i rozmowy; L/A przy braku bitwy przy obozie, najwyzej 1 doba czekania; P po wyjsciu gracza na mape):
    ///   najpierw sprawdzenie (jency nadal u rodu gracza, zloto, ziemia wolna od oblezenia), dla L/A najpierw zwiniecie obozu (nie wyszlo = bez zaplaty),
    ///   potem jency wolni (EndCaptivityAction.ApplyByReleasedByChoice - jak gra przy uwolnieniu z lochu; bez urazy 367d, bo uraza liczy okup i rokowania),
    ///   zloto GiveGoldAction gracz -> wodz (platnik i odbiorca), ziemia ChangeOwnerOfSettlementAction.ApplyByBarter (jak FiefBarterable; bez roszczenia 369 -
    ///   ApplyByBarter to nie BySiege; wladca gracza -10 relacji za oddanie ziemi korony).
    /// Zapis "arm_k384": kolejka, oferta w menu, wpisy umow (klucz, zasieg, do doby, przysiegajacy, zdrada), przerwy odmow. Log 'Wymiana jencow (384): ...'.
    /// Potkniecia liczone (log pierwszego na miejsce), nic sie nie gasi.
    /// </summary>
    internal static class PrisonerTerms384
    {
        internal const string Tag = "Wymiana jencow (384)";
        private const string MenuId = "arm384_pass";
        private const int AskPriority = 104;
        private const int ShowPris = 8, ShowFiefs = 3, MaxRounds = 2;
        private const double CooldownDays = 3.0;
        private const int NoAttackHours = 48, PactNoAttackHours = 25;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.PrisonerExchangeForTerms; } }
        private static int Days { get { var s = Settings.Current; return s != null ? Math.Max(10, Math.Min(364, s.PrisonerExchangePactDays)) : 60; } }

        private sealed class Deal
        {
            public string St = "", Lord = ""; public double Start; public char Want; public List<string> Pris = new List<string>(); public int Gold;
            public List<string> Fiefs = new List<string>(); public bool Betray; public double P, Day, V, C; public int Days;
        }
        /// <summary>Wpis umowy: Key - frakcja albo "c:"+rod; Scope - "*" (wszystkie ziemie rodu gracza) albo StringId osady (rozejm osady); Betray - tylko pilnowany.</summary>
        private sealed class Entry { public string Key = "", Scope = "", Lord = ""; public int Until; public bool Betray; }
        private sealed class Cool { public double Start, End; }
        private sealed class Ev { public double V, Vp, Vg, Vf, C, PTake, Thr, Lo; public char Why = 'T'; public string Parts = "-"; }

        private static readonly List<Deal> _queue = new List<Deal>();
        private static readonly List<Entry> _entries = new List<Entry>();
        private static readonly Dictionary<string, Cool> _cool = new Dictionary<string, Cool>();
        private static Deal _offer;          // P: zgoda w rozmowie, czeka na opcje menu arm384_pass
        private static bool _armed;
        private static int _stumbles, _dZero, _dNoAttack;
        private static readonly HashSet<string> _err = new HashSet<string>();

        // stan rozmowy
        private static Hero _talk;
        private static Settlement _st;
        private static char _want;
        private static readonly List<Hero> _pris = new List<Hero>();
        private static readonly Dictionary<Hero, double> _worth = new Dictionary<Hero, double>();
        private static readonly Dictionary<Hero, bool> _inSt = new Dictionary<Hero, bool>();
        private static readonly HashSet<Hero> _sel = new HashSet<Hero>();
        private static readonly List<Settlement> _fiefs = new List<Settlement>();
        private static readonly HashSet<Settlement> _selF = new HashSet<Settlement>();
        private static int _gold, _round, _ask;
        private static Hero _askHero;
        private static Ev _ev;
        private static char _verdict;

        internal static void Reset()
        {
            _queue.Clear(); _entries.Clear(); _cool.Clear(); _offer = null; _armed = false; _stumbles = 0; _dZero = 0; _dNoAttack = 0; _err.Clear();
            ClearTalk();
        }

        private static void ClearTalk()
        {
            _talk = null; _st = null; _want = '\0'; _pris.Clear(); _worth.Clear(); _inSt.Clear(); _sel.Clear(); _fiefs.Clear(); _selF.Clear();
            _gold = 0; _round = 0; _ask = 0; _askHero = null; _ev = null; _verdict = '\0';
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("PrisonerTerms384." + where, e); } catch { }
        }

        private static string Name(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Name(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "-"; } catch { return "?"; } }
        private static string F(double v) { return v.ToString("0.#", Inv); }
        private static string N(double v) { return Math.Round(v).ToString("N0", Inv); }
        private static double Today { get { return CampaignTime.Now.ToDays; } }
        private static int Trait(Hero h, TraitObject t) { return SiegeSurrender374.Trait(h, t); }
        private static string WantName(char w) { return w == 'L' ? "odstapienie" : w == 'P' ? "swobodne przejscie" : w == 'A' ? "odstapienie + pakt" : "-"; }

        /// <summary>374: czy w toku jest umowa 384 o te osade (oferta w menu albo kolejka) - 374 wtedy nie proponuje poddania.</summary>
        internal static bool Pending(Settlement st)
        {
            if (st == null) return false;
            if (_offer != null && _offer.St == st.StringId) return true;
            foreach (var d in _queue) if (d.St == st.StringId) return true;
            return false;
        }

        // ------------------------------------------------------------ rozmowa
        internal static void AddDialogs(CampaignGameStarter starter)
        {
            try
            {
                if (starter == null) return;
                starter.AddPlayerLine("arm384_ask", "hero_main_options", "arm384_open", "{=!}{ARM384_ASK}", AskCond, Begin, AskPriority, AskClick);
                starter.AddDialogLine("arm384_open", "arm384_open", "arm384_want", "{=!}{ARM384_OPEN}", OpenCond, null, 200);
                starter.AddPlayerLine("arm384_w_lift", "arm384_want", "arm384_wfor", "{=!}{ARM384_WL}", WantLCond, () => SetWant('L'), 112);
                starter.AddPlayerLine("arm384_w_pact", "arm384_want", "arm384_wfor", "{=!}{ARM384_WA}", WantACond, () => SetWant('A'), 111);
                starter.AddPlayerLine("arm384_w_pass", "arm384_want", "arm384_wfor", "{=!}{ARM384_WP}", WantPCond, () => SetWant('P'), 110);
                starter.AddPlayerLine("arm384_w_peace", "arm384_want", "lord_pretalk", "{=!}A truce between our realms.", null, ClearTalk, 109, PeaceClick);
                starter.AddPlayerLine("arm384_w_back", "arm384_want", "lord_pretalk", "{=!}Never mind.", null, ClearTalk, 100);
                starter.AddDialogLine("arm384_wfor", "arm384_wfor", "arm384_offer", "{=!}{ARM384_WFOR}", WforCond, null, 200);
                for (int k = 0; k < ShowPris; k++)
                {
                    int i = k;
                    starter.AddPlayerLine("arm384_p" + i, "arm384_offer", "arm384_ack", "{=!}{ARM384_P" + i + "}", () => PCond(i), () => PToggle(i), 130 - i);
                }
                starter.AddPlayerLine("arm384_pall", "arm384_offer", "arm384_ack", "{=!}{ARM384_PALL}", PAllCond, PAllToggle, 121);
                starter.AddPlayerLine("arm384_g5", "arm384_offer", "arm384_ack", "{=!}Add 5,000 denars.", () => GoldCond(5000), () => { _gold += 5000; }, 120);
                starter.AddPlayerLine("arm384_g20", "arm384_offer", "arm384_ack", "{=!}Add 20,000 denars.", () => GoldCond(20000), () => { _gold += 20000; }, 119);
                starter.AddPlayerLine("arm384_g0", "arm384_offer", "arm384_ack", "{=!}{ARM384_G0}", G0Cond, () => { _gold = 0; }, 118);
                for (int k = 0; k < ShowFiefs; k++)
                {
                    int i = k;
                    starter.AddPlayerLine("arm384_f" + i, "arm384_offer", "arm384_ack", "{=!}{ARM384_F" + i + "}", () => FCond(i), () => FToggle(i), 117 - i);
                }
                starter.AddPlayerLine("arm384_done", "arm384_offer", "arm384_answer", "{=!}That is my offer. What say you?", DoneCond, Decide, 105);
                starter.AddPlayerLine("arm384_o_back", "arm384_offer", "lord_pretalk", "{=!}Never mind.", null, ClearTalk, 100);
                starter.AddDialogLine("arm384_ack", "arm384_ack", "arm384_offer", "{=!}{ARM384_SUM}", SumCond, null, 200);
                starter.AddDialogLine("arm384_yes", "arm384_answer", "close_window", "{=!}{ARM384_YES}", () => _verdict == 'Y', YesCons, 200);
                starter.AddDialogLine("arm384_ctr", "arm384_answer", "arm384_counter", "{=!}{ARM384_CTR}", () => _verdict == 'K', null, 199);
                starter.AddDialogLine("arm384_no", "arm384_answer", "lord_pretalk", "{=!}{ARM384_NO}", () => _verdict != 'Y' && _verdict != 'K', NoCons, 198);
                starter.AddPlayerLine("arm384_c_gold", "arm384_counter", "arm384_answer", "{=!}{ARM384_CG}", CGoldCond, () => { _gold += _ask; Decide(); }, 112);
                starter.AddPlayerLine("arm384_c_hero", "arm384_counter", "arm384_answer", "{=!}{ARM384_CH}", CHeroCond, () => { if (_askHero != null) _sel.Add(_askHero); Decide(); }, 111);
                starter.AddPlayerLine("arm384_c_more", "arm384_counter", "arm384_offer", "{=!}Let me weigh my offer again.", null, null, 110);
                starter.AddPlayerLine("arm384_c_no", "arm384_counter", "lord_pretalk", "{=!}No. That is my last word.", null, LastWord, 100);
                var s = Settings.Current;
                Log.Info(Tag + ": rozmowa wpieta (hero_main_options, priorytet " + AskPriority + ") | klucz " + (On ? "TAK" : "NIE") + ", rozejm / pakt " + Days + " dob"
                         + " | kolejka " + _queue.Count + (_offer != null ? ", oferta w menu" : "") + ", wpisy umow " + _entries.Count + ", przerwy " + _cool.Count + ".");
            }
            catch (Exception e) { Stumble("AddDialogs", e); }
        }

        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                if (starter == null) return;
                starter.AddGameMenu(MenuId, "{=!}{ARM384_MENU}", MenuInit, GameMenu.MenuOverlayType.None);
                starter.AddGameMenuOption(MenuId, "arm384_ride", "{=!}Ride out under his word.", RideCond, RideCons, true);
            }
            catch (Exception e) { Stumble("AddMenus", e); }
        }

        private static string CoolKey(Settlement st, Hero h) { return st.StringId + "|" + h.StringId; }

        private static double CoolLeft(Settlement st, Hero h)
        {
            Cool c;
            if (st == null || h == null || st.SiegeEvent == null || !_cool.TryGetValue(CoolKey(st, h), out c)) return 0;
            if (Math.Abs(c.Start - st.SiegeEvent.SiegeStartTime.ToHours) > 0.05) return 0;
            double left = c.End - Today;
            return left > 0 ? left : 0;
        }

        /// <summary>Warunki rozmowy jak 374 (SiegeSurrender374.AskCond): gracz w oblezonej twierdzy (swojej albo jako wodz obrony), rozmowca to wodz obozu, wojna, bez bitwy.</summary>
        private static bool Parley(out Hero h, out Settlement st)
        {
            h = Hero.OneToOneConversationHero; st = null;
            var me = Hero.MainHero;
            var mp = MobileParty.MainParty;
            if (h == null || me == null || mp == null || h == me || !h.IsAlive || !h.IsLord || h.IsPrisoner || h.StringId == null) return false;
            st = mp.CurrentSettlement;
            if (st == null || !st.IsFortification || st.Town == null || st.SiegeEvent == null || mp.BesiegerCamp != null) return false;
            if (Settlement.CurrentSettlement != st) return false;
            var ev = st.SiegeEvent;
            if (!SiegeSurrender374.IsCampLeader(ev, h) || SiegeSurrender374.Excluded(h)) return false;
            if (me.MapFaction == null || !FactionManager.IsAtWarAgainstFaction(h.MapFaction, me.MapFaction)) return false;
            if ((st.Party != null && st.Party.MapEvent != null) || mp.MapEvent != null) return false;
            if (mp.Army != null && mp.Army.LeaderParty != mp) return false;
            Hero def = null;
            try { def = Campaign.Current.Models.EncounterModel.GetLeaderOfSiegeEvent(ev, BattleSideEnum.Defender); } catch { }
            if (def != me && st.OwnerClan != Clan.PlayerClan) return false;
            return true;
        }

        private static bool AskCond()
        {
            try
            {
                if (!On) return false;
                ExpireOffer();
                if (_offer != null) return false;
                Hero h; Settlement st;
                if (!Parley(out h, out st)) return false;
                if (Pending(st) || SiegeSurrender374.Pending(st)) return false;
                int n = CountPrisoners(h);
                MBTextManager.SetTextVariable("ARM384_ASK", n > 0 ? "I hold lords of your realm in my dungeons. Let us talk terms." : "Let us talk terms - what would it take?");
                return true;
            }
            catch (Exception e) { Stumble("AskCond", e); return false; }
        }

        private static bool AskClick(out TextObject why)
        {
            why = null;
            try
            {
                var h = Hero.OneToOneConversationHero;
                var st = MobileParty.MainParty != null ? MobileParty.MainParty.CurrentSettlement : null;
                double cl = CoolLeft(st, h);
                if (cl > 0) { int n = (int)Math.Ceiling(cl); why = new TextObject("{=!}He has turned your terms down already. You may try again in " + (n <= 1 ? "1 day." : n.ToString(Inv) + " days.")); return false; }
                return true;
            }
            catch (Exception e) { Stumble("AskClick", e); return true; }
        }

        private static bool PeaceClick(out TextObject why)
        {
            try
            {
                var me = Hero.MainHero;
                var k = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
                if (k != null && k.Leader != null && k.Leader != me)
                    why = new TextObject("{=!}Only " + Name(k.Leader) + " can make peace for " + k.Name + ". You can bargain only for yourself, your men and your lands.");
                else
                    why = new TextObject("{=!}Peace between realms is sworn at court, not before a siege camp. Here you can bargain only for this siege, your men and your lands.");
            }
            catch (Exception e) { Stumble("PeaceClick", e); why = new TextObject("{=!}Not here."); }
            return false;
        }

        /// <summary>Jency-bohaterowie frakcji wodza u rodu gracza: lochy twierdz rodu gracza i druzyna gracza. inSt - w lochu oblezonej osady.</summary>
        private static List<Hero> Prisoners(Hero lord, Settlement besieged, Dictionary<Hero, bool> inSt)
        {
            var list = new List<Hero>();
            var seen = new HashSet<Hero>();
            var f = lord != null ? lord.MapFaction : null;
            if (f == null) return list;
            Action<TaleWorlds.CampaignSystem.Roster.TroopRoster, bool> scan = (r, here) =>
            {
                if (r == null) return;
                for (int i = 0; i < r.Count; i++)
                {
                    var c = r.GetCharacterAtIndex(i);
                    var h = c != null && c.IsHero ? c.HeroObject : null;
                    if (h == null || !h.IsAlive || !h.IsPrisoner || h.MapFaction != f || h == Hero.MainHero || !seen.Add(h)) continue;
                    list.Add(h);
                    if (inSt != null) inSt[h] = here;
                }
            };
            try { scan(PartyBase.MainParty != null ? PartyBase.MainParty.PrisonRoster : null, false); } catch (Exception e) { Stumble("Prisoners.Party", e); }
            try
            {
                var pc = Clan.PlayerClan;
                if (pc != null)
                    foreach (var s in pc.Settlements)
                        if (s != null && s.IsFortification && s.Party != null) scan(s.Party.PrisonRoster, s == besieged);
            }
            catch (Exception e) { Stumble("Prisoners.Dungeons", e); }
            return list;
        }

        private static int CountPrisoners(Hero lord) { return Prisoners(lord, null, null).Count; }

        /// <summary>Jeniec nadal u rodu gracza (druzyna gracza albo loch twierdzy rodu gracza).</summary>
        private static bool Held(Hero h)
        {
            if (h == null || !h.IsAlive || !h.IsPrisoner) return false;
            var pb = h.PartyBelongedToAsPrisoner;
            if (pb == null) return false;
            if (pb == PartyBase.MainParty) return true;
            return pb.IsSettlement && pb.Settlement != null && pb.Settlement.OwnerClan == Clan.PlayerClan;
        }

        private static bool Kin(Hero a, Hero b)
        {
            try
            {
                if (a == null || b == null) return false;
                if (a.Clan != null && a.Clan == b.Clan) return true;
                return a.Father == b || a.Mother == b || b.Father == a || b.Mother == a || a.Spouse == b
                       || (a.Father != null && a.Father == b.Father) || (a.Mother != null && a.Mother == b.Mother);
            }
            catch { return false; }
        }

        /// <summary>Wartosc jenca dla wodza (denary, bez rabatu oblezonej twierdzy i Mercy - te w Evaluate).</summary>
        private static double WorthOf(Hero lord, Hero h)
        {
            double w = Ransom178.PriceFor(h);
            if (w <= 0) { try { w = Campaign.Current.Models.RansomValueCalculationModel.PrisonerRansomValue(h.CharacterObject, null); } catch { w = 1000; } }
            if (Ransom178.IsRulerOrHeir(h)) w *= 3.0;
            else if (Kin(lord, h)) w *= 1.5;
            int rel = 0; try { rel = lord.GetRelation(h); } catch { }
            w *= Math.Max(0.5, Math.Min(1.5, 1.0 + rel / 100.0));
            return Math.Max(0, w);
        }

        private static double FiefValue(Settlement s, Hero lord)
        {
            try { return Math.Max(0, Campaign.Current.Models.SettlementValueModel.CalculateSettlementValueForFaction(s, lord.Clan)); }
            catch { return 0; }
        }

        private static void Begin()
        {
            try
            {
                ClearTalk();
                Hero h; Settlement st;
                if (!Parley(out h, out st)) return;
                _talk = h; _st = st;
                foreach (var p in Prisoners(h, st, _inSt)) { _pris.Add(p); _worth[p] = WorthOf(h, p); }
                _pris.Sort((a, b) => _worth[b].CompareTo(_worth[a]));
                var pc = Clan.PlayerClan;
                if (pc != null)
                    foreach (var s in pc.Settlements)
                        if (s != null && s.IsFortification && s != st && s.SiegeEvent == null && s.Party != null && s.Party.MapEvent == null) _fiefs.Add(s);
                _fiefs.Sort((a, b) => FiefValue(a, h).CompareTo(FiefValue(b, h)));
                if (_fiefs.Count > ShowFiefs) _fiefs.RemoveRange(ShowFiefs, _fiefs.Count - ShowFiefs);
            }
            catch (Exception e) { Stumble("Begin", e); }
        }

        private static bool OpenCond()
        {
            try
            {
                var sb = new StringBuilder("Speak, then. What would you have of me");
                sb.Append(_pris.Count > 0 ? ", and what are my lords worth to you?" : "?");
                int honor = _talk != null ? Trait(_talk, DefaultTraits.Honor) : 0;
                if (_talk != null && (SiegeSurrender374.IsLiar(_talk) || honor < 0)) sb.Append(" (Men say his word is worth little.)");
                else if (honor > 0) sb.Append(" (Men say he keeps his word.)");
                MBTextManager.SetTextVariable("ARM384_OPEN", sb.ToString());
            }
            catch (Exception e) { Stumble("OpenCond", e); MBTextManager.SetTextVariable("ARM384_OPEN", "Speak."); }
            return true;
        }

        private static bool WantLCond()
        {
            if (_talk == null || _st == null) return false;
            MBTextManager.SetTextVariable("ARM384_WL", "Lift the siege and march away, and swear not to come back to " + Name(_st) + " for " + Days + " days.");
            return true;
        }

        private static bool WantACond()
        {
            if (_talk == null || _st == null) return false;
            MBTextManager.SetTextVariable("ARM384_WA", "Lift the siege, and swear that your realm will not raise a hand against my lands or my men for " + Days + " days.");
            return true;
        }

        private static bool WantPCond()
        {
            if (_talk == null || _st == null) return false;
            MBTextManager.SetTextVariable("ARM384_WP", "Let my men and me ride out of " + Name(_st) + " unharmed, on your word of honour. The keep and its garrison stay.");
            return true;
        }

        private static void SetWant(char w) { _want = w; _round = 0; _verdict = '\0'; }

        private static bool WforCond()
        {
            string t = _want == 'P' ? "You ask me to let the prize walk out of the trap. What would you give for it?"
                     : _want == 'A' ? "A long oath for a whole realm. What would you give for it?"
                     : "You ask me to give up a siege half won. What would you give for it?";
            MBTextManager.SetTextVariable("ARM384_WFOR", t);
            return true;
        }

        private static string Rank(Hero h)
        {
            try
            {
                if (h.MapFaction is Kingdom k && k.Leader == h) return "the king";
                if (Ransom178.IsRulerOrHeir(h)) return "of the royal house";
                if (_talk != null && Kin(_talk, h)) return "your kin";
                if (h.Clan != null && h.Clan.Leader == h) return "head of " + h.Clan.Name;
                return h.Clan != null ? "of " + h.Clan.Name : "lord";
            }
            catch { return "lord"; }
        }

        private static bool PCond(int i)
        {
            try
            {
                if (i >= _pris.Count) return false;
                var h = _pris[i];
                bool on = _sel.Contains(h);
                MBTextManager.SetTextVariable("ARM384_P" + i, (on ? "Take back " : "Offer ") + Name(h) + " (" + Rank(h) + ", worth some " + N(_worth[h]) + " denars to you" + (_inSt.ContainsKey(h) && _inSt[h] ? ", held here" : "") + ").");
                return true;
            }
            catch (Exception e) { Stumble("PCond", e); return false; }
        }

        private static void PToggle(int i)
        {
            if (i >= _pris.Count) return;
            var h = _pris[i];
            if (!_sel.Remove(h)) _sel.Add(h);
        }

        private static bool PAllCond()
        {
            if (_pris.Count == 0) return false;
            bool all = _sel.Count == _pris.Count;
            double sum = 0; foreach (var h in _pris) sum += _worth[h];
            MBTextManager.SetTextVariable("ARM384_PALL", all ? "Take back all the lords." : "Offer all the lords of your realm I hold (" + _pris.Count + ", worth some " + N(sum) + " denars).");
            return true;
        }

        private static void PAllToggle()
        {
            if (_sel.Count == _pris.Count) _sel.Clear();
            else foreach (var h in _pris) _sel.Add(h);
        }

        private static bool GoldCond(int add) { var me = Hero.MainHero; return me != null && me.Gold >= _gold + add; }

        private static bool G0Cond()
        {
            if (_gold <= 0) return false;
            MBTextManager.SetTextVariable("ARM384_G0", "Take back the gold (" + N(_gold) + " denars).");
            return true;
        }

        private static bool FCond(int i)
        {
            if (i >= _fiefs.Count || _talk == null) return false;
            var s = _fiefs[i];
            bool on = _selF.Contains(s);
            MBTextManager.SetTextVariable("ARM384_F" + i, (on ? "Take back " : "Offer ") + Name(s) + (s.IsTown ? " (town" : " (castle") + ", worth some " + N(FiefValue(s, _talk)) + " to you" + (on ? ")." : "; its dungeon goes with it)."));
            return true;
        }

        private static void FToggle(int i)
        {
            if (i >= _fiefs.Count) return;
            var s = _fiefs[i];
            if (!_selF.Remove(s)) _selF.Add(s);
        }

        private static bool DoneCond() { return _talk != null && _st != null && _want != '\0' && (_sel.Count > 0 || _gold > 0 || _selF.Count > 0); }

        private static string OfferText()
        {
            var parts = new List<string>();
            if (_sel.Count > 0) parts.Add(_sel.Count == 1 ? Name(First(_sel)) : _sel.Count + " lords");
            if (_gold > 0) parts.Add(N(_gold) + " denars");
            foreach (var s in _selF) parts.Add(Name(s));
            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "nothing";
        }

        private static Hero First(HashSet<Hero> s) { foreach (var h in s) return h; return null; }

        private static bool SumCond()
        {
            try
            {
                var e = _talk != null && _st != null && _st.SiegeEvent != null ? Evaluate(_talk, _st, _want) : null;
                string lean = e == null ? "" : e.V >= e.C * e.Thr ? " (He looks at it long. It may be enough.)" : e.V >= e.C * e.Lo ? " (He frowns. It is close.)" : " (He shakes his head. It is far from enough.)";
                MBTextManager.SetTextVariable("ARM384_SUM", "On the table: " + OfferText() + "." + lean);
            }
            catch (Exception ex) { Stumble("SumCond", ex); MBTextManager.SetTextVariable("ARM384_SUM", "Go on."); }
            return true;
        }

        // ------------------------------------------------------------ ocena
        private static double PTake(Settlement st, out string parts)
        {
            double camp, walls; SiegeSurrender374.Strength(st, out camp, out walls);
            int held, lost; SiegeSurrender374.Assaults(st, out held, out lost);
            int menCamp = 0;
            try
            {
                var ev = st.SiegeEvent;
                if (ev != null && ev.BesiegerCamp != null)
                    foreach (var pb in ev.BesiegerCamp.GetInvolvedPartiesForEventType()) if (pb != null && pb.MemberRoster != null) menCamp += pb.MemberRoster.TotalManCount;
            }
            catch { }
            double food = 999;
            try { float ch = st.Town.FoodChange; if (ch < 0) food = st.Town.FoodStocks / -ch; } catch { }
            double R = walls > 1 ? camp / walls : 99;
            double lostFrac = Math.Min(1.0, lost / Math.Max(1.0, menCamp));
            double p = 0.5 + 0.15 * Math.Log(Math.Max(0.01, R / 4.0), 2) - 0.10 * held - 0.5 * lostFrac + (food < 14 ? 0.15 : food > 60 ? -0.10 : 0);
            p = Math.Max(0.05, Math.Min(0.95, p));
            parts = "p " + p.ToString("0.00", Inv) + " (R " + R.ToString("0.0", Inv) + ", odparte " + held + ", straty " + lost + "/" + menCamp + ", jedzenie " + (food >= 999 ? "bez ubytku" : F(food) + " d") + ")";
            return p;
        }

        private static Ev Evaluate(Hero lord, Settlement st, char want)
        {
            var e = new Ev();
            var me = Hero.MainHero;
            string pp;
            e.PTake = PTake(st, out pp);
            int honor = Trait(lord, DefaultTraits.Honor), calc = Trait(lord, DefaultTraits.Calculating), mercy = Trait(lord, DefaultTraits.Mercy), valor = Trait(lord, DefaultTraits.Valor);
            // oferta
            foreach (var h in _sel)
            {
                double w; if (!_worth.TryGetValue(h, out w)) continue;
                bool here; if (_inSt.TryGetValue(h, out here) && here) w *= 1.0 - 0.7 * e.PTake;   // i tak ich uwolni, gdy zdobedzie mury
                e.Vp += w;
            }
            e.Vp *= Math.Max(0.7, 1.0 + 0.1 * mercy);
            e.Vg = _gold;
            foreach (var s in _selF) e.Vf += FiefValue(s, lord);
            e.V = (e.Vp + e.Vg + e.Vf) * (honor > 0 ? 1.0 + 0.05 * honor : 1.0);
            // koszt zadania
            double stVal = FiefValue(st, lord);
            double cLift = e.PTake * stVal * Math.Max(0.5, 1.0 + 0.15 * valor);
            double c;
            string cp;
            if (want == 'P')
            {
                double ransom = Ransom178.PriceFor(me), mil = 0;
                try { mil = Campaign.Current.Models.ValuationModel.GetMilitaryValueOfParty(MobileParty.MainParty); } catch { }
                c = e.PTake * (ransom + 0.3 * mil) + 2000;
                cp = "przejscie: p x (okup gracza " + N(ransom) + " + 0.3 x wojsko " + N(mil) + ") + 2000";
            }
            else
            {
                c = cLift;
                cp = "odstapienie: p x osada " + N(stVal) + " x Valor";
                if (want == 'A')
                {
                    double other = 0;
                    var pc = Clan.PlayerClan;
                    if (pc != null) foreach (var s in pc.Settlements) if (s != null && s.IsFortification && s != st) other += FiefValue(s, lord);
                    double add = (Days / 60.0) * 0.1 * other;
                    c += add;
                    cp += " + pakt " + N(add) + " (pozostale twierdze gracza " + N(other) + ", " + Days + " d)";
                }
            }
            int rel = 0; try { rel = lord.GetRelation(me); } catch { }
            c *= 1.0 - Math.Max(-50, Math.Min(50, rel)) / 200.0;
            e.C = Math.Max(1000, c);
            e.Thr = calc > 0 ? 1.0 : calc < 0 ? 0.9 : 0.95;
            e.Lo = calc > 0 ? 0.8 : 0.7;
            e.Why = e.PTake >= 0.8 ? 'S' : (mercy < 0 || valor > 0) ? 'C' : 'T';
            e.Parts = pp + " | oferta: jency " + N(e.Vp) + " (" + _sel.Count + ", Mercy " + mercy + "), zloto " + N(e.Vg) + ", ziemia " + N(e.Vf) + " (" + _selF.Count + "), Honor " + honor
                      + " -> V " + N(e.V) + " | koszt: " + cp + ", relacja " + rel + " -> C " + N(e.C) + " | prog " + e.Thr.ToString("0.00", Inv) + " (Calc " + calc + "), kontroferta od " + e.Lo.ToString("0.00", Inv) + ", Valor " + valor;
            return e;
        }

        private static void Decide()
        {
            try
            {
                var h = _talk; var st = _st;
                _askHero = null; _ask = 0;
                if (h == null || st == null || st.SiegeEvent == null || _want == '\0') { _verdict = 'N'; MBTextManager.SetTextVariable("ARM384_NO", "Not now."); return; }
                _round++;
                _ev = Evaluate(h, st, _want);
                double need = _ev.C * _ev.Thr;
                if (_ev.V >= need)
                {
                    _verdict = 'Y';
                    string yes = _want == 'P' ? "Go, then. Leave my lords at the gate, and no man of mine will lay a hand on you. You have my word."
                               : _want == 'A' ? "So be it. Free my lords, and we march at dawn - and for " + Days + " days my realm will leave your lands and your men in peace. You have my word."
                               : "So be it. Free my lords, and we march at dawn. For " + Days + " days no banner of mine comes back to " + Name(st) + ". You have my word.";
                    MBTextManager.SetTextVariable("ARM384_YES", yes);
                    return;
                }
                if (_ev.V >= _ev.C * _ev.Lo && _round < MaxRounds)
                {
                    double gap = need - _ev.V;
                    int honor = Trait(h, DefaultTraits.Honor);
                    double mul = honor > 0 ? 1.0 + 0.05 * honor : 1.0;
                    _ask = (int)(Math.Ceiling(gap / mul / 500.0) * 500);
                    // jeszcze jeden jeniec: najtanszy, ktory sam domyka roznice
                    double best = double.MaxValue;
                    foreach (var p in _pris)
                    {
                        if (_sel.Contains(p)) continue;
                        double w = _worth[p];
                        bool here; if (_inSt.TryGetValue(p, out here) && here) w *= 1.0 - 0.7 * _ev.PTake;
                        w *= Math.Max(0.7, 1.0 + 0.1 * Trait(h, DefaultTraits.Mercy)) * mul;
                        if (w >= gap && w < best) { best = w; _askHero = p; }
                    }
                    _verdict = 'K';
                    var sb = new StringBuilder("Not enough. ");
                    sb.Append("Add " + N(_ask) + " denars and we have a deal.");
                    if (_askHero != null) sb.Append(" Or give me " + Name(_askHero) + " as well.");
                    MBTextManager.SetTextVariable("ARM384_CTR", sb.ToString());
                    LogDecision(h, st, _ev, "KONTROFERTA runda " + _round + " (+" + _ask + " zlota" + (_askHero != null ? " albo jeniec " + Name(_askHero) : "") + ")", 0);
                    return;
                }
                _verdict = 'N';
                string no = _ev.Why == 'S' ? "Why bargain for what I will take? When your walls fall, your dungeon opens to me."
                          : _ev.Why == 'C' ? "You insult me. I will have my lords back and your banner in the mud besides."
                          : "Not for so little. Think on it, and we may speak again.";
                MBTextManager.SetTextVariable("ARM384_NO", no);
                Refuse(h, st, "ODMOWA (runda " + _round + ")");
            }
            catch (Exception e) { Stumble("Decide", e); _verdict = 'N'; MBTextManager.SetTextVariable("ARM384_NO", "Not now."); }
        }

        private static void Refuse(Hero h, Settlement st, string verdict)
        {
            try
            {
                if (h != null && st != null && st.SiegeEvent != null) _cool[CoolKey(st, h)] = new Cool { Start = st.SiegeEvent.SiegeStartTime.ToHours, End = Today + CooldownDays };
                if (_ev != null && h != null && st != null) LogDecision(h, st, _ev, verdict + ", przerwa " + F(CooldownDays) + " d", 0);
            }
            catch (Exception e) { Stumble("Refuse", e); }
        }

        private static bool CGoldCond()
        {
            var me = Hero.MainHero;
            if (_ask <= 0 || me == null || me.Gold < _gold + _ask) return false;
            MBTextManager.SetTextVariable("ARM384_CG", "Agreed - " + N(_ask) + " denars more.");
            return true;
        }

        private static bool CHeroCond()
        {
            if (_askHero == null) return false;
            MBTextManager.SetTextVariable("ARM384_CH", "Then take " + Name(_askHero) + " as well.");
            return true;
        }

        private static void LastWord()
        {
            Refuse(_talk, _st, "ODMOWA (gracz nie dolozyl)");
            try { Log.Player(Name(_talk) + " will not take your terms. You may try again in " + CooldownDays.ToString(Inv) + " days.", true); } catch { }
            ClearTalk();
        }

        private static void NoCons()
        {
            try { Log.Player(Name(_talk) + " refuses your terms. You may try again in " + CooldownDays.ToString(Inv) + " days.", true); } catch { }
            ClearTalk();
        }

        private static void YesCons()
        {
            try
            {
                var h = _talk; var st = _st;
                if (h != null && st != null && st.SiegeEvent != null && _ev != null)
                {
                    double p = SiegeSurrender374.BetrayChance(h);
                    bool betray = p > 0 && MBRandom.RandomFloat < p;
                    var d = new Deal { St = st.StringId, Lord = h.StringId, Start = st.SiegeEvent.SiegeStartTime.ToHours, Want = _want, Gold = _gold, Betray = betray, P = p, Day = Today, V = _ev.V, C = _ev.C, Days = Days };
                    foreach (var x in _sel) if (x.StringId != null) d.Pris.Add(x.StringId);
                    foreach (var s in _selF) if (s.StringId != null) d.Fiefs.Add(s.StringId);
                    if (_want == 'P')
                    {
                        _offer = d;
                        // ta sama droga co gra po udanym barterze wolnego przejscia (SafePassageBarterable.Apply) i 374: nastepne menu po rozmowie, bez SwitchToMenu
                        Campaign.Current.GameMenuManager.SetNextMenu(MenuId);
                    }
                    else { _queue.Add(d); _armed = false; }
                    LogDecision(h, st, _ev, "ZGODA (" + WantName(_want) + ") | zdrada " + F(p * 100) + "%" + (betray ? " -> ZLAMIE SLOWO" : " -> dotrzyma"), p);
                }
            }
            catch (Exception e) { Stumble("YesCons", e); _offer = null; }
            ClearTalk();
        }

        private static void LogDecision(Hero h, Settlement st, Ev e, string verdict, double p)
        {
            try
            {
                int rel = 0; try { rel = h.GetRelation(Hero.MainHero); } catch { }
                var names = new List<string>();
                foreach (var x in _sel) { if (names.Count >= 12) { names.Add("..."); break; } names.Add(Name(x)); }
                Log.Info(Tag + ": dzien " + F(Today) + " | " + Name(st) + " [" + st.StringId + "] | wodz " + Name(h) + " (Honor " + Trait(h, DefaultTraits.Honor) + ", Calc " + Trait(h, DefaultTraits.Calculating)
                         + ", Mercy " + Trait(h, DefaultTraits.Mercy) + ", Valor " + Trait(h, DefaultTraits.Valor) + ", rel " + rel + (SiegeSurrender374.IsLiar(h) ? ", znany klamca" : "") + ")"
                         + " | zadanie " + WantName(_want) + " (" + Days + " d) | oferta: jency " + _sel.Count + " [" + string.Join(", ", names.ToArray()) + "], zloto " + _gold + ", ziemia " + _selF.Count
                         + " | " + e.Parts + " | wynik V/C " + (e.C > 0 ? (e.V / e.C).ToString("0.00", Inv) : "-") + " -> " + verdict + ".");
            }
            catch (Exception ex) { Stumble("LogDecision", ex); }
        }

        // ------------------------------------------------------------ menu po rozmowie (P - swobodne przejscie)
        private static void ExpireOffer()
        {
            var o = _offer;
            if (o == null) return;
            try
            {
                if (Mission.Current != null) return;
                var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
                if (cm != null && (cm.IsConversationInProgress || cm.IsConversationFlowActive)) return;
                var ctx = Campaign.Current != null ? Campaign.Current.CurrentMenuContext : null;
                string menu = ctx != null && ctx.GameMenu != null ? ctx.GameMenu.StringId : null;
                if (menu == MenuId) return;
                var st = Settlement.Find(o.St);
                bool siegeGone = st == null || st.SiegeEvent == null || Math.Abs(st.SiegeEvent.SiegeStartTime.ToHours - o.Start) > 0.05;
                double age = Today - o.Day;
                if (!siegeGone && age <= 0.1) return;
                _offer = null;
                Log.Info(Tag + ": dzien " + F(Today) + " | " + (st != null ? Name(st) : o.St) + " - oferta przejscia PRZEPADLA (menu " + MenuId + " nie otwarte; czynne menu " + (menu ?? "-") + ").");
            }
            catch (Exception e) { Stumble("ExpireOffer", e); }
        }

        private static void MenuInit(MenuCallbackArgs args)
        {
            try
            {
                var st = _offer != null ? Settlement.Find(_offer.St) : MobileParty.MainParty.CurrentSettlement;
                var lord = _offer != null ? Hero.Find(_offer.Lord) : null;
                MBTextManager.SetTextVariable("ARM384_MENU", "A postern of " + Name(st) + " opens. " + (lord != null ? Name(lord) : "The besiegers' commander") + " waits beyond it for the lords you promised him.");
                string mesh = st != null && st.SettlementComponent != null ? st.SettlementComponent.WaitMeshName : null;
                args.MenuContext.SetBackgroundMeshName(string.IsNullOrEmpty(mesh) ? "wait_fallback" : mesh);
            }
            catch (Exception e) { Stumble("MenuInit", e); }
        }

        private static bool RideCond(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Leave;
            return true;
        }

        private static void RideCons(MenuCallbackArgs args)
        {
            var o = _offer;
            _offer = null;
            try
            {
                var st = o != null ? Settlement.Find(o.St) : null;
                var ev = st != null ? st.SiegeEvent : null;
                int n = 0;
                if (ev != null && ev.BesiegerCamp != null)
                    foreach (var pb in ev.BesiegerCamp.GetInvolvedPartiesForEventType())
                    {
                        var mp = pb != null ? pb.MobileParty : null;
                        if (mp == null || mp.Ai == null) continue;
                        mp.Ai.SetDoNotAttackMainParty(NoAttackHours); n++;
                    }
                if (o != null) { _queue.Add(o); _armed = false; }
                Log.Info(Tag + ": dzien " + F(Today) + " | " + Name(st) + " - gracz wychodzi na slowo honoru | oboz nie atakuje " + n + " partii przez " + NoAttackHours + " h"
                         + (o == null ? " | BRAK umowy (wczytanie bez oferty?) - samo wyjscie" : " | wymiana w kolejce (wykonanie w takcie mapy)") + ".");
            }
            catch (Exception e) { Stumble("RideCons", e); }
            try
            {
                var main = MobileParty.MainParty;
                if (main.BesiegerCamp == null && PlayerEncounter.Current != null && main.CurrentSettlement != null)
                {
                    if (main.Army != null) main.Army = null;
                    PlayerSiege.FinalizePlayerSiege();
                    PlayerEncounter.LeaveSettlement();
                    PlayerEncounter.Finish();
                }
                else GameMenu.ExitToLast();
            }
            catch (Exception e) { Stumble("RideCons.Leave", e); }
        }

        // ------------------------------------------------------------ wykonanie (takt kampanii)
        internal static void Tick()
        {
            if (_offer != null) ExpireOffer();
            if (_queue.Count == 0) return;
            try
            {
                if (Campaign.Current == null || Mission.Current != null) return;
                var cm = Campaign.Current.ConversationManager;
                if (cm != null && (cm.IsConversationInProgress || cm.IsConversationFlowActive)) return;
                var mp = MobileParty.MainParty;
                if (mp == null || mp.MapEvent != null || Hero.MainHero.IsPrisoner) return;
                var d = _queue[0];
                if (d.Want == 'P')
                {
                    if (PlayerEncounter.Current != null || mp.CurrentSettlement != null) return;   // dopiero za brama
                }
                else
                {
                    var st0 = Settlement.Find(d.St);
                    if (SiegeSurrender374.CampBusy(st0) && Today - d.Day < 1.0) return;   // bitwa przy obozie - czekamy (najwyzej doba)
                }
                if (!_armed) { _armed = true; return; }
                _queue.RemoveAt(0);   // najpierw z kolejki - wyjatek nie powtorzy umowy
                _armed = false;
                Run(d);
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        private static void Drop(Deal d, Settlement st, string en, string pl)
        {
            Log.Info(Tag + ": dzien " + F(Today) + " | " + (st != null ? Name(st) : d.St) + " - umowa PRZEPADLA (" + WantName(d.Want) + "): " + pl + ".");
            if (!string.IsNullOrEmpty(en)) Log.Player(en, true);
        }

        /// <summary>Czy oferta nadal da sie oddac: jency u rodu gracza, zloto, ziemia rodu gracza bez oblezenia i bitwy. why - powod po polsku.</summary>
        private static bool Payable(Deal d, out string why)
        {
            why = null;
            var me = Hero.MainHero;
            foreach (var id in d.Pris) { var h = Hero.Find(id); if (!Held(h)) { why = "jeniec " + (h != null ? Name(h) : id) + " nie jest juz u rodu gracza"; return false; } }
            if (d.Gold > 0 && me.Gold < d.Gold) { why = "za malo zlota gracza (" + me.Gold + " < " + d.Gold + ")"; return false; }
            foreach (var id in d.Fiefs)
            {
                var s = Settlement.Find(id);
                if (s == null || s.OwnerClan != Clan.PlayerClan || s.SiegeEvent != null || (s.Party != null && s.Party.MapEvent != null)) { why = "ziemia " + (s != null ? Name(s) : id) + " nie do oddania"; return false; }
            }
            return true;
        }

        /// <summary>Zaplata: jency wolni (z wyboru gracza), zloto gracz -> wodz, ziemia przez barter gry. Zwraca opis do logu.</summary>
        private static string Pay(Deal d, Hero lord)
        {
            var me = Hero.MainHero;
            int freed = 0, fails = 0;
            var names = new List<string>();
            foreach (var id in d.Pris)
            {
                var h = Hero.Find(id);
                if (!Held(h)) { fails++; continue; }
                try { EndCaptivityAction.ApplyByReleasedByChoice(h, me); freed++; if (names.Count < 20) names.Add(Name(h)); }
                catch (Exception e) { fails++; Stumble("Pay.Free", e); }
            }
            int gold = 0;
            if (d.Gold > 0)
            {
                try { int g = Math.Min(d.Gold, me.Gold); GiveGoldAction.ApplyBetweenCharacters(me, lord, g, true); gold = g; }
                catch (Exception e) { Stumble("Pay.Gold", e); }
            }
            var ceded = new List<string>();
            foreach (var id in d.Fiefs)
            {
                var s = Settlement.Find(id);
                if (s == null || s.OwnerClan != Clan.PlayerClan) continue;
                try
                {
                    bool crown = Clan.PlayerClan.Kingdom != null && s.MapFaction == Clan.PlayerClan.Kingdom;
                    ChangeOwnerOfSettlementAction.ApplyByBarter(lord, s);
                    ceded.Add(Name(s));
                    var ruler = crown ? Clan.PlayerClan.Kingdom.Leader : null;
                    if (ruler != null && ruler != me && ruler.IsAlive) ChangeRelationAction.ApplyPlayerRelation(ruler, -10);
                }
                catch (Exception e) { Stumble("Pay.Fief", e); }
            }
            return "jency wolni " + freed + (fails > 0 ? " (nieudane " + fails + ")" : "") + (names.Count > 0 ? " [" + string.Join(", ", names.ToArray()) + "]" : "")
                   + ", zloto " + gold + " (gracz -> " + Name(lord) + "), ziemia " + (ceded.Count > 0 ? string.Join(", ", ceded.ToArray()) + " (barter, wladca gracza -10)" : "-");
        }

        private static void Run(Deal d)
        {
            Settlement st = null;
            try
            {
                st = Settlement.Find(d.St);
                var lord = Hero.Find(d.Lord);
                if (Today - d.Day > 3.0) { Drop(d, st, "The terms agreed at " + Name(st) + " have lapsed.", "umowa przedawniona (ponad 3 doby)"); return; }
                if (lord == null || !lord.IsAlive || lord.IsPrisoner) { Drop(d, st, "The lord who gave you his word at " + Name(st) + " can no longer keep it.", "przysiegajacy nie zyje albo w niewoli"); return; }
                string why;
                if (!Payable(d, out why)) { Drop(d, st, "You can no longer pay what you promised at " + Name(st) + ". The terms are void.", why); return; }
                if (d.Want == 'P') { RunPass(d, st, lord); return; }
                var ev = st != null ? st.SiegeEvent : null;
                if (st == null || ev == null || Math.Abs(ev.SiegeStartTime.ToHours - d.Start) > 0.05) { Drop(d, st, null, "oblezenie skonczylo sie przed wymiana - nic nie oddane"); return; }
                if (!FactionManager.IsAtWarAgainstFaction(lord.MapFaction, st.MapFaction)) { Drop(d, st, null, "pokoj miedzy stronami - nic nie oddane"); return; }
                if (SiegeSurrender374.CampBusy(st)) { Drop(d, st, "The terms agreed at " + Name(st) + " have lapsed amid the fighting.", "bitwa przy obozie dluzej niz doba"); return; }
                var keys = SiegeRansom.CampKeys384(ev.BesiegerCamp);   // przed zwinieciem
                string lift;
                if (!SiegeRansom.Lift384(st, out lift)) { Drop(d, st, "The besiegers of " + Name(st) + " could not break camp. Nothing was handed over.", "oboz nie zwiniety (" + lift + ") - bez zaplaty"); return; }
                string paid = Pay(d, lord);
                int until = (int)Today + d.Days;
                int turned = 0;
                if (!d.Betray) turned = SiegeRansom.Truce384(st, keys, until);
                foreach (var k in keys) _entries.Add(new Entry { Key = k, Scope = st.StringId, Until = until, Lord = lord.StringId, Betray = d.Betray });
                if (d.Want == 'A')
                {
                    foreach (var k in keys) _entries.Add(new Entry { Key = k, Scope = "*", Until = until, Lord = lord.StringId, Betray = d.Betray });
                    if (!d.Betray) NoAttack();
                }
                if (!d.Betray) { try { ChangeRelationAction.ApplyPlayerRelation(lord, 5); } catch (Exception e) { Stumble("Run.Rel", e); } }
                Log.Info(Tag + ": dzien " + F(Today) + " | " + Name(st) + " [" + st.StringId + "] WYKONANE: " + WantName(d.Want) + " | wodz " + Name(lord) + " | " + lift + " | " + paid
                         + " | " + (d.Betray ? "ZDRADA wylosowana (p " + F(d.P * 100) + "%): rozejm NIE wpisany do latki - tylko pilnowany" : "rozejm osady do dnia " + until + " [" + string.Join(", ", keys.ToArray()) + "], zawrocone w drodze " + turned)
                         + (d.Want == 'A' ? " | pakt ziem rodu gracza do dnia " + until + (d.Betray ? " (tylko pilnowany)" : "") : "") + (d.Betray ? "" : " | relacja wodza +5") + ".");
                Log.Player(Name(lord) + " lifts the siege of " + Name(st) + " and takes back his lords. "
                           + (d.Want == 'A' ? "His realm swears to leave your lands and your men in peace for " + d.Days + " days." : "He swears not to return for " + d.Days + " days."));
            }
            catch (Exception e) { Stumble("Run", e); }
        }

        private static void RunPass(Deal d, Settlement st, Hero lord)
        {
            var me = Hero.MainHero;
            var main = MobileParty.MainParty;
            var ev = st != null ? st.SiegeEvent : null;
            MobileParty cap = null;
            if (d.Betray && ev != null && ev.BesiegerCamp != null)
            {
                var lp = lord.PartyBelongedTo;
                if (lp != null && lp.IsActive && lp.LeaderHero == lord && ev.BesiegerCamp.HasInvolvedPartyForEventType(lp.Party)) cap = lp;
                else { var l = ev.BesiegerCamp.LeaderParty; if (l != null && l.IsActive && l.LeaderHero != null) cap = l; }
            }
            string paid = Pay(d, lord);
            if (cap == null)
            {
                try { ChangeRelationAction.ApplyPlayerRelation(lord, 5); } catch (Exception e) { Stumble("RunPass.Rel", e); }
                Log.Info(Tag + ": dzien " + F(Today) + " | " + Name(st) + " WYKONANE: swobodne przejscie | wodz " + Name(lord) + " dotrzymal slowa" + (d.Betray ? " (zdrada wylosowana, ale oboz juz nie stoi)" : "")
                         + " | " + paid + " | twierdza zostaje u " + (st != null && st.OwnerClan != null ? st.OwnerClan.Name.ToString() : "-") + " | relacja wodza +5.");
                Log.Player("You ride out of " + Name(st) + " and leave " + Name(lord) + "'s lords at the gate. He keeps his word.");
                return;
            }
            // zdrada: jak RunBetray 374 bez zmiany pana twierdzy - szeregowi do lochu, jency-bohaterowie gracza wolni, towarzysze i gracz w niewoli
            int men = 0, comp = 0, freed = 0;
            bool taken = false;
            try
            {
                men = SiegeSurrender374.ToPrison(main.MemberRoster, cap.Party.PrisonRoster);
                var pr = main.PrisonRoster;
                for (int i = pr.Count - 1; i >= 0; i--)
                {
                    var c = pr.GetCharacterAtIndex(i);
                    if (c == null || !c.IsHero || c.HeroObject == null) continue;
                    try { EndCaptivityAction.ApplyByReleasedAfterBattle(c.HeroObject); freed++; } catch (Exception e) { Stumble("RunPass.Free", e); }
                }
                var heroes = new List<Hero>();
                var r = main.MemberRoster;
                for (int i = 0; i < r.Count; i++) { var c = r.GetCharacterAtIndex(i); if (c != null && c.IsHero && c.HeroObject != null && c.HeroObject != me) heroes.Add(c.HeroObject); }
                foreach (var h in heroes) { try { TakePrisonerAction.Apply(cap.Party, h); comp++; } catch (Exception e) { Stumble("RunPass.Companion", e); } }
                TakePrisonerAction.Apply(cap.Party, me); taken = true;
            }
            catch (Exception e) { Stumble("RunPass.Betray", e); }
            int punished = Punish(lord);
            Log.Info(Tag + ": dzien " + F(Today) + " | " + Name(st) + " ZDRADA przy przejsciu: wodz " + Name(lord) + " lamie slowo | " + paid + " | gracz " + (taken ? "W NIEWOLI" : "NIE pojmany (blad)")
                     + " u " + (cap.Name != null ? cap.Name.ToString() : "-") + ", towarzysze " + comp + ", szeregowi do lochu " + men + ", jency-bohaterowie gracza wolni " + freed
                     + " | twierdza zostaje z zaloga | kara relacji " + punished + " glow rodow | klamca zapisany (374).");
            Log.Player(Name(lord) + " breaks his word! His men take your lords at the gate - and then fall on you.", true);
        }

        /// <summary>Zdrajca: relacja z graczem -30, honorowe glowy rodow -5 x Honor wobec niego, lista klamcow 374. Zwraca ile glow rodow.</summary>
        private static int Punish(Hero lord)
        {
            int punished = 0;
            try
            {
                ChangeRelationAction.ApplyPlayerRelation(lord, -30);
                foreach (var c in Clan.All)
                {
                    if (c == null || c.IsEliminated || c == lord.Clan || c == Clan.PlayerClan || c.IsBanditFaction) continue;
                    var l = c.Leader;
                    if (l == null || !l.IsAlive || l == lord) continue;
                    int hon = Trait(l, DefaultTraits.Honor);
                    if (hon <= 0) continue;
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(l, lord, -5 * hon, false);
                    punished++;
                }
                SiegeSurrender374.MarkLiar(lord);
            }
            catch (Exception e) { Stumble("Punish", e); }
            return punished;
        }

        // ------------------------------------------------------------ pakt: cele AI, druzyna gracza, zlamania
        private static bool Match(Entry e, IFaction f, Clan c)
        {
            if (f != null && f.StringId != null && e.Key == f.StringId) return true;
            return c != null && c.StringId != null && e.Key == "c:" + c.StringId;
        }

        /// <summary>Postfiks celu AI (z SiegeRansom.ScorePost): pakt - oblezenie twierdzy i najazd na wies rodu gracza ma wynik 0 dla frakcji / rodow z paktem.</summary>
        internal static void ScorePact(Settlement st, Army.ArmyTypes type, MobileParty party, ref float result)
        {
            if (_entries.Count == 0 || result <= 0f) return;
            try
            {
                if (!On || st == null || party == null) return;
                if (type != Army.ArmyTypes.Besieger && type != Army.ArmyTypes.Raider) return;
                if (st.OwnerClan != Clan.PlayerClan) return;
                int today = (int)Today;
                foreach (var e in _entries)
                {
                    if (e.Betray || e.Scope != "*" || e.Until <= today) continue;
                    if (Match(e, party.MapFaction, party.ActualClan)) { result = 0f; _dZero++; return; }
                }
            }
            catch (Exception e) { Stumble("ScorePact", e); }
        }

        /// <summary>Druzyny frakcji / rodow z paktem nie atakuja druzyny gracza (SetDoNotAttackMainParty 25 h - odswiezane co dobe).</summary>
        private static void NoAttack()
        {
            try
            {
                int today = (int)Today;
                foreach (var mp in MobileParty.AllLordParties)
                {
                    if (mp == null || !mp.IsActive || mp.IsMainParty || mp.Ai == null) continue;
                    foreach (var e in _entries)
                    {
                        if (e.Betray || e.Scope != "*" || e.Until <= today) continue;
                        if (Match(e, mp.MapFaction, mp.ActualClan)) { mp.Ai.SetDoNotAttackMainParty(PactNoAttackHours); _dNoAttack++; break; }
                    }
                }
            }
            catch (Exception e) { Stumble("NoAttack", e); }
        }

        private static List<Entry> Covering(IFaction f, Clan c, Settlement st)
        {
            var list = new List<Entry>();
            int today = (int)Today;
            foreach (var e in _entries)
            {
                if (e.Until <= today || !Match(e, f, c)) continue;
                if (e.Scope == "*" || (st != null && e.Scope == st.StringId)) list.Add(e);
            }
            return list;
        }

        /// <summary>Nasluch OnSiegeEventStarted: oblezenie twierdzy rodu gracza wbrew umowie (zdrada / mimo latki) albo gracz oblega frakcje z umowa.</summary>
        internal static void OnSiegeStarted(SiegeEvent ev)
        {
            if (_entries.Count == 0) return;
            try
            {
                if (ev == null || ev.BesiegedSettlement == null || ev.BesiegerCamp == null) return;
                var st = ev.BesiegedSettlement;
                var lp = ev.BesiegerCamp.LeaderParty;
                if (lp == null) return;
                if (lp.IsMainParty) { PlayerBroke(st.MapFaction, st.OwnerClan, "oblezenie " + Name(st)); return; }
                if (st.OwnerClan != Clan.PlayerClan) return;
                AiBroke(Covering(lp.MapFaction, lp.ActualClan, st), "oblezenie " + Name(st) + " przez " + (lp.LeaderHero != null ? Name(lp.LeaderHero) : lp.StringId));
            }
            catch (Exception e) { Stumble("OnSiegeStarted", e); }
        }

        /// <summary>Nasluch MapEventStarted: gracz atakuje druzyne / wies frakcji z umowa, albo AI z umowa atakuje druzyne gracza / wies rodu gracza.</summary>
        internal static void OnMapEventStarted(MapEvent me, PartyBase a, PartyBase d)
        {
            if (_entries.Count == 0) return;
            try
            {
                if (a == null || d == null) return;
                if (a == PartyBase.MainParty) { PlayerBroke(d.MapFaction, d.MobileParty != null ? d.MobileParty.ActualClan : (d.Settlement != null ? d.Settlement.OwnerClan : null), "atak na " + d.Name); return; }
                bool onMe = d == PartyBase.MainParty || (d.Settlement != null && d.Settlement.OwnerClan == Clan.PlayerClan);
                if (!onMe) return;
                var mp = a.MobileParty;
                var list = Covering(a.MapFaction, mp != null ? mp.ActualClan : null, null);
                list.RemoveAll(e => e.Scope != "*");
                AiBroke(list, "atak " + a.Name + " na " + d.Name);
            }
            catch (Exception e) { Stumble("OnMapEventStarted", e); }
        }

        private static void AiBroke(List<Entry> list, string what)
        {
            if (list == null || list.Count == 0) return;
            var lords = new HashSet<string>();
            bool betray = false;
            foreach (var e in list) { lords.Add(e.Lord); if (e.Betray) betray = true; }
            if (!betray)
            {
                Log.Info(Tag + ": dzien " + F(Today) + " | UMOWA NARUSZONA przez AI mimo latki celu (" + what + ") - wpisy " + list.Count + "; brak kary (AI nie lamie umowy z wlasnej woli - droga poza modelem celu).");
                return;
            }
            var names = new List<string>();
            foreach (var id in lords)
            {
                var h = Hero.Find(id);
                if (h == null || !h.IsAlive) continue;
                int n = Punish(h);
                names.Add(Name(h) + " (kara " + n + " glow rodow)");
            }
            _entries.RemoveAll(e => e.Betray && lords.Contains(e.Lord));
            Log.Info(Tag + ": dzien " + F(Today) + " | ZDRADA umowy: " + what + " | przysiegajacy " + string.Join(", ", names.ToArray()) + " - klamca, relacja -30, wpisy zdjete.");
            Log.Player("The enemy breaks the oath sworn for your prisoners: " + what.Replace("oblezenie", "siege of").Replace("atak", "attack").Replace(" na ", " on ").Replace(" przez ", " by ") + ".", true);
        }

        private static void PlayerBroke(IFaction f, Clan c, string what)
        {
            var list = new List<Entry>();
            int today = (int)Today;
            foreach (var e in _entries) if (e.Until > today && Match(e, f, c)) list.Add(e);
            if (list.Count == 0) return;
            var lords = new HashSet<string>();
            foreach (var e in list) lords.Add(e.Lord);
            // wszystkie wpisy tych umow (takze innych kluczy) - umowa zerwana w calosci
            var gone = new List<Entry>();
            foreach (var e in _entries) if (lords.Contains(e.Lord)) gone.Add(e);
            foreach (var e in gone)
            {
                _entries.Remove(e);
                if (e.Scope != "*" && !e.Betray) SiegeRansom.EndTruce384(Settlement.Find(e.Scope), e.Key);
            }
            var rel = new List<string>();
            try
            {
                foreach (var id in lords)
                {
                    var h = Hero.Find(id);
                    if (h != null && h.IsAlive) { ChangeRelationAction.ApplyPlayerRelation(h, -20); rel.Add(Name(h) + " -20"); }
                }
                var ruler = f != null ? f.Leader : null;
                if (ruler != null && ruler.IsAlive && ruler != Hero.MainHero) { ChangeRelationAction.ApplyPlayerRelation(ruler, -10); rel.Add(Name(ruler) + " -10"); }
                TraitLevelingHelper.OnIncidentResolved(DefaultTraits.Honor, -500);
                rel.Add("Honor gracza -500 xp");
            }
            catch (Exception e) { Stumble("PlayerBroke", e); }
            Log.Info(Tag + ": dzien " + F(Today) + " | GRACZ LAMIE UMOWE (" + what + ") | wpisy zdjete " + gone.Count + " | " + string.Join(", ", rel.ToArray()) + ".");
            Log.Player("You break the oath sworn over the prisoners. Men will remember it.", true);
        }

        /// <summary>Doba: wygasle wpisy precz, pakt - odswiezenie SetDoNotAttackMainParty, linia stanu (gdy sa wpisy albo cos sie zdarzylo).</summary>
        internal static void Daily()
        {
            try
            {
                int today = (int)Today;
                int before = _entries.Count;
                _entries.RemoveAll(e => e.Until <= today);
                var dead = new List<string>();
                foreach (var kv in _cool) if (kv.Value.End <= Today) dead.Add(kv.Key);
                foreach (var k in dead) _cool.Remove(k);
                if (_entries.Count > 0 && On) NoAttack();
                if (_entries.Count > 0 || before > 0 || _dZero > 0 || _queue.Count > 0)
                {
                    int pact = 0, st = 0, bet = 0;
                    foreach (var e in _entries) { if (e.Betray) bet++; else if (e.Scope == "*") pact++; else st++; }
                    Log.Info(Tag + ": doba " + today + " | wpisy umow " + _entries.Count + " (pakt ziem " + pact + ", rozejm osady " + st + ", pilnowane zdrady " + bet + "; wygaslo " + (before - _entries.Count) + ")"
                             + " | cele AI wyzerowane paktem " + _dZero + " | druzyny bez ataku na gracza " + _dNoAttack + " | kolejka " + _queue.Count + " | potkniecia " + _stumbles + ".");
                }
                _dZero = 0; _dNoAttack = 0;
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        // ------------------------------------------------------------ zapis ("arm_k384")
        private static readonly char[] Bad = { '|', ';', ':', ',' };
        private static string D(double v) { return v.ToString("R", Inv); }
        private static bool P(string s, out double v) { return double.TryParse(s, NumberStyles.Float, Inv, out v); }
        private static bool Ok(string s) { return s != null && s.IndexOfAny(Bad) < 0; }

        /// <summary>"v1|Q:st:wodz:start:zadanie:jency,..:zloto:ziemie,..:zdrada:p:doba:V:C:dni;...(O - oferta w menu)|E:klucz:zasieg:do:wodz:zdrada;...|C:st:wodz:start:koniec;..."</summary>
        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(256);
                sb.Append("v1|");
                bool f = true;
                var all = new List<Deal>(_queue);
                if (_offer != null) all.Add(_offer);
                foreach (var d in all)
                {
                    if (!Ok(d.St) || !Ok(d.Lord)) continue;
                    var pr = d.Pris.FindAll(Ok); var fi = d.Fiefs.FindAll(Ok);
                    if (!f) sb.Append(';'); f = false;
                    sb.Append(ReferenceEquals(d, _offer) ? 'O' : 'Q').Append(':').Append(d.St).Append(':').Append(d.Lord).Append(':').Append(D(d.Start)).Append(':').Append(d.Want)
                      .Append(':').Append(string.Join(",", pr.ToArray())).Append(':').Append(d.Gold.ToString(Inv)).Append(':').Append(string.Join(",", fi.ToArray()))
                      .Append(':').Append(d.Betray ? '1' : '0').Append(':').Append(D(d.P)).Append(':').Append(D(d.Day)).Append(':').Append(D(d.V)).Append(':').Append(D(d.C)).Append(':').Append(d.Days.ToString(Inv));
                }
                sb.Append('|'); f = true;
                int today = (int)Today;
                foreach (var e in _entries)
                {
                    if (e.Until <= today || !Ok(e.Key.Replace("c:", "c_")) || !Ok(e.Scope) || !Ok(e.Lord)) continue;
                    if (!f) sb.Append(';'); f = false;
                    sb.Append(e.Key.Replace(":", "~")).Append(':').Append(e.Scope).Append(':').Append(e.Until.ToString(Inv)).Append(':').Append(e.Lord).Append(':').Append(e.Betray ? '1' : '0');
                }
                sb.Append('|'); f = true;
                foreach (var kv in _cool)
                {
                    if (kv.Value.End <= Today) continue;
                    var x = kv.Key.Split('|');
                    if (x.Length != 2 || !Ok(x[0]) || !Ok(x[1])) continue;
                    if (!f) sb.Append(';'); f = false;
                    sb.Append(x[0]).Append(':').Append(x[1]).Append(':').Append(D(kv.Value.Start)).Append(':').Append(D(kv.Value.End));
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            Reset();
            int bad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var f = data.Split('|');
                if (f.Length < 4 || f[0] != "v1") { Log.Info(Tag + ": zapis nieczytelny - od zera."); return; }
                foreach (var p in f[1].Split(';'))
                {
                    if (p.Length == 0) continue;
                    var x = p.Split(':'); double s, pp, dy, v, c; int g, dd;
                    if (x.Length == 14 && (x[0] == "Q" || x[0] == "O") && P(x[3], out s) && x[4].Length == 1 && int.TryParse(x[6], NumberStyles.Integer, Inv, out g)
                        && P(x[9], out pp) && P(x[10], out dy) && P(x[11], out v) && P(x[12], out c) && int.TryParse(x[13], NumberStyles.Integer, Inv, out dd))
                    {
                        var d = new Deal { St = x[1], Lord = x[2], Start = s, Want = x[4][0], Gold = g, Betray = x[8] == "1", P = pp, Day = dy, V = v, C = c, Days = dd };
                        foreach (var id in x[5].Split(',')) if (id.Length > 0) d.Pris.Add(id);
                        foreach (var id in x[7].Split(',')) if (id.Length > 0) d.Fiefs.Add(id);
                        if (x[0] == "O") _offer = d; else _queue.Add(d);
                    }
                    else bad++;
                }
                foreach (var p in f[2].Split(';'))
                {
                    if (p.Length == 0) continue;
                    var x = p.Split(':'); int u;
                    if (x.Length == 5 && int.TryParse(x[2], NumberStyles.Integer, Inv, out u)) _entries.Add(new Entry { Key = x[0].Replace("~", ":"), Scope = x[1], Until = u, Lord = x[3], Betray = x[4] == "1" });
                    else bad++;
                }
                foreach (var p in f[3].Split(';'))
                {
                    if (p.Length == 0) continue;
                    var x = p.Split(':'); double s, e;
                    if (x.Length == 4 && P(x[2], out s) && P(x[3], out e)) _cool[x[0] + "|" + x[1]] = new Cool { Start = s, End = e }; else bad++;
                }
                if (_queue.Count + _entries.Count + _cool.Count > 0 || _offer != null || bad > 0)
                    Log.Info(Tag + ": wczytano z zapisu - kolejka " + _queue.Count + (_offer != null ? ", oferta w menu" : "") + ", wpisy umow " + _entries.Count + ", przerwy " + _cool.Count + (bad > 0 ? " (bledne " + bad + ")" : "") + ".");
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
