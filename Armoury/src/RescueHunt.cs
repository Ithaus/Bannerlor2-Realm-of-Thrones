using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 222 - ODBICIE POJMANEGO LORDA (Jeff 26.10 ok. 22:15: "zlapalem przywodce rodu Tywina Lannistera i co kurwa teraz, Lannisterowie ruszyliby natychmiast
    /// mnie schwytac aby odbic lorda, nie wiem kiedy by sie dowiedzieli ... nie od razu ale po jakims czasie zaczna mnie tropic"). Klucze RescueHunt*.
    ///
    /// DOTAD (dekompilacja 1.4.8, ROT AIThinkPatch, nasze latki): Kruki (R3-05c) opozniaja tylko czat gracza; AI nie dostaje zadnej wiesci o pojmaniu i nikt
    /// nie rusza za graczem. Poscig gry (AiEngagePartyBehavior, GoAroundParty) dziala tylko w pol dnia marszu od wlasnych osad i w 22.5 jedn. od celu.
    ///
    /// REGULA (jeniec lorda krolestwa u gracza: druzyna gracza, druzyna rodu gracza albo loch osady rodu gracza; bez Innych, bez jencow sceny prologu E5 i Neda):
    ///  - Ranga: T3 wladca krolestwa albo jego bliscy (malzonek, dzieci, rodzice, rodzenstwo) z rodu panujacego; T2 glowa rodu albo jej bliscy; T1 inny lord.
    ///  - Wiesc (bez wszechwiedzy): z miejsca pojmania jezdziec do najblizszej twierdzy krolestwa jenca (dalej niz 40 jedn. - Ravens.NearRange; 2.5 jedn./h =
    ///    kruk Ravens.SpeedPerHour / 2.5 - "kruk = ok. 2.5 x dzien jazdy"), stamtad kruk do siedziby rodu (T3: siedziby wladcy) - Ravens.EtaHours (3 h + d / 6.25,
    ///    najwyzej 12 dob). 222c: albo - gdy szybciej - jezdziec do najblizszej NIEOBLEGANEJ twierdzy (zamek albo miasto) DOWOLNEJ frakcji, ktorej maester
    ///    wysyla kruka do siedziby (te same liczby); tez dla lochu; lowy z zapisu dostaja min(stary plan, nowy plan) (Recalc w Resolve).
    ///    Kazda druzyna wie w chwili: wiesc w siedzibie + EtaHours(siedziba -> druzyna) (do 40 jedn. od siedziby - od razu, jak Ravens).
    ///    Kurier z okupem (RansomOfferCampaignBehavior / Ransom178.ConsiderPrefix) nie przychodzi, zanim rod nie wie (HoldCourier).
    ///  - Slady (ostatnie znane miejsce, najwyzej 8): druzyna lorda krolestwa jenca widzi cel (odleglosc &lt;= jej SeeingRange; sama wie od razu, inni - jezdziec
    ///    2.5 jedn./h); osada krolestwa widzi cel (w niej albo w 12 jedn. dniem / 6 noca - podstawa widzenia gry; wiesc wzorem wiesci o pojmaniu); loch osady.
    ///    Slad starszy niz 72 h (trzy dni jazdy - ostatnie pasmo Rumours.Days) - zimny trop, druzyna wraca do ocen gry.
    ///  - Lowcy: druzyny lordow krolestwa jenca w wojnie z celem, ktore juz wiedza i znaja slad: T1 - rod jenca; T2 - rod + rody krolestwa, ktorych glowa jest
    ///    przyjacielem jenca albo glowy jego rodu (IsFriend gry, bez najemnikow); T3 - cale krolestwo. Bez: glodujacych, niegotowych (ArmsFirst.Unready p9),
    ///    druzyn sceny prologu, w bitwie, oblegajacych inna osade, czlonkow cudzej armii (wodz armii liczy cala armie, jak OB5 Contrib). Zasieg (linia prosta):
    ///    rod i T3 - 8.02 doby marszu (zasieg gry dla oblezonej twierdzy), pomocnicy T2 - 4.01 doby.
    ///  - Potrzeba = 1.5 x sila celu (Army?.EstimatedStrength ?? Party.EstimatedStrength - jak gra; ponad 1.5 x gra nie daje premii w ocenie poscigu).
    ///    Wybor: najblizsi do sladu, az suma sily &gt;= potrzeby (bez nadmiaru - wzor OB5). Za malo wszystkich - wodz lowow (glowa rodu, T3 wladca) dostaje opcje
    ///    "zbierz armie" (PatrolAroundPoint + WillGatherArmy przy twierdzy krolestwa najblizszej sladu -> armia Defender gry), gdy gra dala mu druzyny do
    ///    wezwania i razem maja potrzebe; inaczej czekaja (okup).
    ///  - Oceny (postfiks 150 CampaignEventDispatcher.AiHourlyTick - po OB5/WatchLaw/ReaverLaw (200), przed ArmsFirst/RaidTribute (100)): B = max(najlepsza inna
    ///    opcja, 0.5); P = 1.2 rod (ArmsFirst.Edge), 1.1 pomocnik, 1.3 T3. Widzi cel i sila lowcow w 22.5 jedn. od celu &gt;= potrzeby - GoAroundParty(cel)
    ///    P x B + 0.01 x ocena gry; widzi, ale za slabi - nic (gra unika sama); nie widzi - PatrolAroundPoint(slad) P x B; loch - BesiegeSettlement(loch) P x B
    ///    i RaidSettlement wsi lochu 1.1 x B (tylko opcje dane przez gre). Bez podbicia: obrona zagrozonej osady wlasnego rodu w opcjach, odsiecz oblezonej
    ///    twierdzy krolestwa jako najlepsza opcja (OB5), wodz armii w zbiorce (DoNotChangeBehavior).
    ///  - Koniec: uwolnienie (okup, ucieczka, bitwa, wypuszczenie, wykup, pokoj), smierc, jeniec juz nie u gracza, rod po stronie gracza / bez krolestwa, 24 h
    ///    pokoju (okno PeaceHold M6a-3).
    /// WATKI: postfiks czyta tylko migawke rozkazow (slownik podmieniany w calosci raz na godzine - jak RaidTribute), liczniki Interlocked. Reszta - watek kampanii.
    /// ZAPIS: klucz arm_odbicie (v1; lowy i slady); brak klucza = brak lowow. Linie "Odbicie (222): ...". Potkniecia liczone, bez globalnego wylacznika.
    /// </summary>
    internal static partial class RescueHunt
    {
        internal const string P = "Odbicie (222): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // liczby z modeli gry i moda (opis w naglowku) - zadnej nowej stalej rozgrywki
        private const float NearRange = 40f;                  // Ravens.NearRange: pol dnia jazdy - widac/slychac od razu
        private const float RiderPerHour = 2.5f;              // Ravens.SpeedPerHour 6.25 / 2.5 (kruk = ok. 2.5 x dzien jazdy)
        private const float NeedMult = 1.5f;                  // AiEngagePartyBehavior: przewaga ponad 1.5 x cel bez premii (num18)
        private const double ColdHours = 72.0;                // trzy dni jazdy (Rumours.Days, ostatnie pasmo < 160)
        private const double PeaceEndHours = 24.0;            // PeaceHold M6a-3 - wojna wypowiadana od nowa tego samego dnia nie konczy lowow
        private const double MarkMsgHours = 12.0;             // komunikat "marked your column" najwyzej co pol doby na lowy
        private const double SightLogHours = 6.0;             // linia "widziany" najwyzej co 6 h na lowy (tylko log)
        // recenzja 222: slady trzymane wedlug wieku (ColdHours), nie liczby - kruk/jezdziec do dalekich lowcow idzie do kilkudziesieciu godzin;
        // najwyzej jeden slad na godzine (Look co godzine) -> do 73 sladow; MaxSights to tylko bezpiecznik (trzy doby + doba zapasu na loch)
        private const int MaxSights = (int)ColdHours + 24;
        private const double ReachOwnDays = 8.02, ReachHelpDays = 4.01;   // AiMilitaryBehavior.GetDistanceScoreForDefending (oblezona twierdza / zwykle)
        private const float FloorBase = 0.5f;                 // OB5: najnizsza podstawa B
        private const float POwn = 1.2f, PHelp = 1.1f, PRealm = 1.3f;   // ArmsFirst.Edge; OB5 zamek i miasto
        private const float ArmyEdge = 1.05f;                 // OB5 KingArmyEdge
        private const float RaidEdge = 1.1f;
        private const float EngageMul = 45f;                  // AiEngagePartyBehavior: NeededMaximumLandDistanceForEncounteringMobileParty x 45

        private const int TOne = 1, TTwo = 2, TThree = 3;
        private static readonly string[] TierName = { "-", "T1 lord", "T2 glowa rodu", "T3 wladca" };
        private const int CMain = 0, CParty = 1, CDungeon = 2;
        private static readonly string[] CaptorName = { "gracz", "druzyna rodu gracza", "loch" };
        private const int SParty = 0, SSettlement = 1, SDungeon = 2, SCapture = 3;
        private static readonly string[] SightName = { "druzyna", "osada", "loch", "pojmanie" };
        private const int ONone = 0, OChase = 1, OPatrol = 2, OArmy = 3, OSiege = 4;

        // ------------------------------------------------------------ stan
        private sealed class Sight
        {
            public double H; public float X, Y; public int Kind;
            public double FH; public float FX, FY;          // wiesc ze sladu: godzina w twierdzy (albo u obserwatora) i jej polozenie
            public MobileParty By;                          // druzyna, ktora widziala (tylko sesja)
            public Vec2 Pos { get { return new Vec2(X, Y); } }
        }

        private sealed class Hunt
        {
            public Hero Captive; public Clan Clan; public Kingdom K; public int Tier;
            public int CaptorKind; public string CaptorId = "";
            public double StartH; public float X, Y;
            public string SeatName = ""; public float SX, SY; public double SeatH;
            public bool Told;
            public string DungeonId = ""; public double DungeonH = -1.0; public bool DungeonTold;
            public int Armies, Battles, Wins; public float TopStr;
            public readonly List<Sight> Sights = new List<Sight>();
            // sesja
            public double PeaceH = -1.0, MarkH = -1000.0, SightLogH = -1000.0;
            public string Mode = "";
            public readonly HashSet<MobileParty> Elig = new HashSet<MobileParty>();
            public MapEvent BattleMe; public int BattleSide = -1; public double BattleEndH = -1.0;
            public Vec2 Seat { get { return new Vec2(SX, SY); } }
        }

        private sealed class Order
        {
            public Hunt H; public int Kind; public MobileParty Target; public Vec2 Pos; public Settlement Fort; public Settlement Dungeon;
            public float P, Need, Own;
            public double OfferedH = -10.0;
        }

        private static readonly Dictionary<Hero, Hunt> _hunts = new Dictionary<Hero, Hunt>();
        private static readonly Dictionary<MobileParty, Order> Empty = new Dictionary<MobileParty, Order>();
        private static Dictionary<MobileParty, Order> _snap = Empty;
        private static readonly List<string[]> _pendingH = new List<string[]>();
        private static readonly List<string[]> _pendingS = new List<string[]>();
        private static int _importN = -1, _importBad;
        private static double _lastHour = -1.0;
        private static double _hourNowH = -1.0, _hourPrevH = -1.0;   // NowH tego i poprzedniego przegladu (T4: wiesc przy pierwszym przegladzie po planie)
        private static int _sessionDays;
        private static bool _adopted;   // Adopt raz na sesje

        // liczniki doby (watek kampanii) i mysli AI (Interlocked)
        private static int _dNew, _dEnded, _dSkipped, _dCourier, _dRethink, _dSight, _dSightCap, _dMarkMsg, _dStumbles, _stumbles, _dSkipTrip, _dSkipHeld;
        private static int _dRaise, _dAdd, _dPatrol, _dArmyOffer, _dArmyNoCall, _dArmyShort, _dSiege, _dRaid, _dSkipRelief, _dSkipOwn, _dSkipDnc, _dArmies;
        private static readonly Dictionary<string, int> _dSkipWhy = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly HashSet<string> _err = new HashSet<string>(StringComparer.Ordinal);

        // latka
        private static bool _patched; private static string _missing; private static MethodBase _target;

        private static Settings S { get { return Settings.Current; } }
        internal static bool On { get { var s = S; return s != null && s.RescueHunt; } }
        private static bool RealmOn { get { var s = S; return s != null && s.RescueHuntRealm; } }
        private static bool TellOn { get { var s = S; return s != null && s.RescueHuntTellPlayer; } }
        private static double NowH { get { return CampaignTime.Now.ToHours; } }
        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }

        private static string When()
        {
            double h = NowH;
            return "dzien " + ((int)Math.Floor(h / 24.0)).ToString(Inv) + " godz " + ((int)Math.Floor(h - Math.Floor(h / 24.0) * 24.0)).ToString(Inv);
        }

        private static string F0(double v) { return v.ToString("0", Inv); }
        private static string F1(double v) { return v.ToString("0.0", Inv); }
        private static string F2(double v) { return v.ToString("0.00", Inv); }
        private static string Nm(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(MobileParty m) { try { return m != null && m.Name != null ? m.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s) sb.Append(ch == '\t' || ch == '\n' || ch == '\r' || ch == '|' ? ' ' : ch);
            return sb.ToString();
        }

        /// <summary>Nazwa rodu dla gracza: ROT nazywa rody "House X" - bez podwojnego "House".</summary>
        private static string House(Clan c)
        {
            string n = Nm(c);
            return n.StartsWith("House ", StringComparison.Ordinal) ? n : "House " + n;
        }

        internal static void Reset()
        {
            _hunts.Clear(); Volatile.Write(ref _snap, Empty); _pendingH.Clear(); _pendingS.Clear();
            _importN = -1; _importBad = 0; _lastHour = -1.0; _hourNowH = _hourPrevH = -1.0; _sessionDays = 0; _stumbles = 0; _adopted = false; _forts = null;
            lock (_err) _err.Clear();
            ClearDay();
            TestReset();
        }

        private static void ClearDay()
        {
            _dNew = _dEnded = _dSkipped = _dCourier = _dRethink = _dSight = _dSightCap = _dMarkMsg = _dStumbles = _dSkipTrip = _dSkipHeld = 0;
            Interlocked.Exchange(ref _dRaise, 0); Interlocked.Exchange(ref _dAdd, 0); Interlocked.Exchange(ref _dPatrol, 0);
            Interlocked.Exchange(ref _dArmyOffer, 0); Interlocked.Exchange(ref _dArmyNoCall, 0); Interlocked.Exchange(ref _dArmyShort, 0);
            Interlocked.Exchange(ref _dSiege, 0); Interlocked.Exchange(ref _dRaid, 0); Interlocked.Exchange(ref _dSkipRelief, 0);
            Interlocked.Exchange(ref _dSkipOwn, 0); Interlocked.Exchange(ref _dSkipDnc, 0);
            _dArmies = 0;
            _dSkipWhy.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            Interlocked.Increment(ref _stumbles); Interlocked.Increment(ref _dStumbles);
            bool first;
            lock (_err) first = _err.Add(where);
            if (first) { try { Log.Error("RescueHunt." + where, e); } catch { } }
        }

        // ------------------------------------------------------------ wpiecie
        internal static void ApplyAll(Harmony h)
        {
            if (h == null || _patched) return;
            try
            {
                var m = AccessTools.Method(typeof(CampaignEventDispatcher), "AiHourlyTick", new[] { typeof(MobileParty), typeof(PartyThinkParams) });
                _target = m;
                if (m == null) { _missing = "CampaignEventDispatcher.AiHourlyTick"; return; }
                // 150: po OB5 / WatchLaw / ReaverLaw (Low = 200), przed ArmsFirst p9 i RaidTribute (VeryLow = 100) - niegotowa partia i tak dostaje -1 od p9
                h.Patch(m, postfix: new HarmonyMethod(typeof(RescueHunt), nameof(ThinkPostfix)) { priority = 150 });
                _patched = true;
            }
            catch (Exception e) { _missing = "CampaignEventDispatcher.AiHourlyTick (" + e.GetType().Name + ")"; Stumble("ApplyAll", e); }
        }

        internal static void RegisterEvents(CampaignBehaviorBase b)
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(b, delegate (CampaignGameStarter s) { try { SessionStart(); } catch (Exception e) { Stumble("SessionStart", e); } });
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(b, DayProfile.Hr("RescueHunt.Hourly", delegate { try { Hourly(); } catch (Exception e) { Stumble("Hourly", e); } }));
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(b, delegate { try { Daily(); } catch (Exception e) { Stumble("Daily", e); } });
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(b, delegate (PartyBase captor, Hero h) { try { OnTaken(captor, h); } catch (Exception e) { Stumble("OnTaken", e); } });
            CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(b, delegate (Hero h, PartyBase p, IFaction f, EndCaptivityDetail d, bool n) { try { OnReleased(h, p, d); } catch (Exception e) { Stumble("OnReleased", e); } });
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(b, delegate (Hero v, Hero k, KillCharacterAction.KillCharacterActionDetail d, bool n) { try { OnKilled(v); } catch (Exception e) { Stumble("OnKilled", e); } });
            CampaignEvents.MapEventStarted.AddNonSerializedListener(b, delegate (MapEvent me, PartyBase a, PartyBase d) { try { OnBattleStart(me, a, d); } catch (Exception e) { Stumble("OnBattleStart", e); } });
            CampaignEvents.MapEventEnded.AddNonSerializedListener(b, delegate (MapEvent me) { try { OnBattleEnd(me); } catch (Exception e) { Stumble("OnBattleEnd", e); } });
            CampaignEvents.ArmyCreated.AddNonSerializedListener(b, delegate (Army a) { try { OnArmyCreated(a); } catch (Exception e) { Stumble("OnArmyCreated", e); } });
        }

        private static string Owners(MethodBase m)
        {
            try
            {
                if (m == null) return "-";
                var info = Harmony.GetPatchInfo(m);
                if (info == null) return "-";
                var o = info.Owners.Distinct().ToList();
                return o.Count > 0 ? string.Join("/", o.ToArray()) : "-";
            }
            catch { return "?"; }
        }

        /// <summary>Kurier 178: czy prefiks Ransom178.ConsiderPrefix siedzi na RansomOfferCampaignBehavior.ConsiderRansomPrisoner (tam stoi bramka wiesci).</summary>
        private static string CourierWired()
        {
            try
            {
                var m = AccessTools.Method(typeof(RansomOfferCampaignBehavior), "ConsiderRansomPrisoner", new[] { typeof(Hero) });
                if (m == null) return "BRAK (metoda gry)";
                var info = Harmony.GetPatchInfo(m);
                if (info != null) foreach (var p in info.Prefixes) if (p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(Ransom178)) return "TAK";
                return "NIE (prefiks 178 nie wpiety - gra wysyla kuriera bez bramki)";
            }
            catch { return "?"; }
        }

        private static void SessionStart()
        {
            Resolve();
            _sessionDays = 0;
            Log.Info(P + "start sesji | klucze Rescue Hunt " + (On ? "tak" : "nie") + ", Rescue Hunt Realm " + (RealmOn ? "tak" : "nie") + ", Rescue Hunt Tell Player " + (TellOn ? "tak" : "nie")
                     + " | latka AiHourlyTick 150 " + (_patched ? "TAK" : "BRAK (" + (_missing ?? "?") + ")") + " (wlasciciele " + Owners(_target) + ")"
                     + " | kurier 178 " + CourierWired()
                     + " | lowy z zapisu " + (_importN >= 0 ? _importN + " (bledne " + _importBad + ")" : "0 (brak klucza arm_odbicie - nowa kampania albo stary zapis)")
                     + " | jezdziec " + F1(RiderPerHour) + " jedn./h, od razu do " + F0(NearRange) + " jedn., potrzeba " + F1(NeedMult) + " x sila celu, trop zimny po " + F0(ColdHours)
                     + " h, zasieg " + F2(ReachOwnDays) + "/" + F2(ReachHelpDays) + " dob marszu.");
        }

        // ------------------------------------------------------------ kto jest czyj
        private static bool PlayerHolds(PartyBase pb)
        {
            if (pb == null) return false;
            if (pb == PartyBase.MainParty) return true;
            if (pb.IsSettlement) return pb.Settlement != null && pb.Settlement.OwnerClan == Clan.PlayerClan;
            var mp = pb.MobileParty;
            return mp != null && (mp.IsMainParty || (mp.ActualClan != null && mp.ActualClan == Clan.PlayerClan));
        }

        private static bool Kin(Hero a, Hero b)
        {
            if (a == null || b == null) return false;
            if (a == b) return true;
            if (a.Spouse == b || b.Spouse == a || a.Father == b || a.Mother == b || b.Father == a || b.Mother == a) return true;
            if (a.Father != null && a.Father == b.Father) return true;
            if (a.Mother != null && a.Mother == b.Mother) return true;
            return false;
        }

        private static int TierOf(Hero h, Kingdom k)
        {
            var ruler = k != null ? k.Leader : null;
            if (ruler != null && (h == ruler || (h.Clan != null && h.Clan == k.RulingClan && Kin(h, ruler)))) return TThree;
            var cl = h.Clan != null ? h.Clan.Leader : null;
            if (cl != null && Kin(h, cl)) return TTwo;
            return TOne;
        }

        /// <summary>Powod, dla ktorego pojmanie nie otwiera lowow (null - otwiera).</summary>
        private static string WhyNot(PartyBase captor, Hero h)
        {
            if (!PlayerHolds(captor)) return "nie u gracza";
            if (h == Hero.MainHero || !h.IsLord) return "nie lord";
            var c = h.Clan;
            if (c == null || c == Clan.PlayerClan || c.IsEliminated) return "rod gracza albo bez rodu";
            var k = c.Kingdom;
            if (k == null || !c.MapFaction.IsKingdomFaction) return "rod bez krolestwa";
            if (Hero.MainHero != null && Hero.MainHero.MapFaction == k) return "krolestwo gracza";
            try { if (ClanIncomeBook.IsUndeadClan(c) || Undead.Character(h.CharacterObject)) return "Inni"; } catch { }
            try { if (Prologue.HeldByScene(h) != null || Prologue.NedHeld(h)) return "jeniec sceny prologu (E5)"; } catch { }
            return null;
        }

        // ------------------------------------------------------------ wiesc
        private static Settlement NearestFort(Kingdom k, Vec2 p)
        {
            if (k == null) return null;
            Settlement best = null; float bd = float.MaxValue;
            var fs = k.Fiefs;
            for (int i = 0; i < fs.Count; i++)
            {
                var st = fs[i] != null ? fs[i].Settlement : null;
                if (st == null || !st.IsFortification) continue;
                float d = st.GetPosition2D.Distance(p);
                if (d < bd) { bd = d; best = st; }
            }
            return best;
        }

        /// <summary>Wiesc z miejsca p do punktu q: jezdziec do najblizszej twierdzy krolestwa (powyzej NearRange), potem kruk (Ravens.EtaHours; twierdza = cel
        /// albo cel do NearRange od twierdzy - bez kruka). Bez twierdz - kruk prosto z p. Wynik: godziny; d1/h1 - jazda, d2/h2 - kruk.</summary>
        private static double NewsPlan(Vec2 p, Kingdom k, Vec2 q, out Settlement f, out float d1, out double h1, out float d2, out double h2)
        {
            f = NearestFort(k, p);
            Vec2 from = f != null ? f.GetPosition2D : p;
            d1 = f != null ? p.Distance(from) : 0f;
            h1 = d1 <= NearRange ? 0.0 : d1 / RiderPerHour;
            d2 = from.Distance(q);
            h2 = d2 <= NearRange ? 0.0 : Ravens.EtaHours(d2);
            return h1 + h2;
        }

        /// <summary>Druzyna w q dostaje wiesc z siedziby: do NearRange od razu, dalej kruk (Ravens.EtaHours).</summary>
        private static double FromSeat(Vec2 seat, Vec2 q)
        {
            float d = seat.Distance(q);
            return d <= NearRange ? 0.0 : Ravens.EtaHours(d);
        }

        // 222c (autotest 127a: Tywin wziety na Skagos, najblizsza twierdza jego krolestwa Duskendale 665 jedn. - wiesc po 13 dobach): maester jest w kazdym
        // zamku i miescie, a porywacz sam wysyla kruka z zadaniem okupu - wiesc idzie tez przez najblizsza twierdze DOWOLNEJ frakcji, ktora nie jest oblegana
        // (z oblezonej kruk nie wyleci). Te same liczby co droga przez twierdze krolestwa: jezdziec RiderPerHour powyzej NearRange, kruk Ravens.EtaHours.
        // Kruki R3-05c nie wysylaja kruka do siedziby jenca (ich kolejka to tylko czat gracza; pojmanie przez gracza idzie na czat od razu) - wspolne sa liczby.
        private static List<Settlement> _forts;

        private static Settlement NearestOpenFort(Vec2 p)
        {
            if (_forts == null)
            {
                var l = new List<Settlement>();
                foreach (var s in Settlement.All) if (s != null && s.IsFortification) l.Add(s);
                _forts = l;
            }
            Settlement best = null; float bd = float.MaxValue;
            for (int i = 0; i < _forts.Count; i++)
            {
                var st = _forts[i];
                if (st.SiegeEvent != null) continue;
                float d = st.GetPosition2D.Distance(p);
                if (d < bd) { bd = d; best = st; }
            }
            return best;
        }

        /// <summary>222c: wiesc z miejsca p do siedziby q - szybsza z dwoch drog: (a) jezdziec do najblizszej nieoblezonej twierdzy dowolnej frakcji i kruk jej
        /// maestra, (b) NewsPlan (twierdza krolestwa jenca - jak dotad). Remis - (b). any = wybrana (a); alt - opis drugiej drogi do linii logu.</summary>
        private static double SeatNews(Vec2 p, Kingdom k, Vec2 q, out Settlement f, out float d1, out double h1, out float d2, out double h2, out bool any, out string alt)
        {
            Settlement fb; float b1, b2; double g1, g2;
            double etaB = NewsPlan(p, k, q, out fb, out b1, out g1, out b2, out g2);
            var fa = NearestOpenFort(p);
            double etaA = double.MaxValue, e1 = 0.0, e2 = 0.0; float a1 = 0f, a2 = 0f;
            if (fa != null)
            {
                var fp = fa.GetPosition2D;
                a1 = p.Distance(fp); e1 = a1 <= NearRange ? 0.0 : a1 / RiderPerHour;
                a2 = fp.Distance(q); e2 = a2 <= NearRange ? 0.0 : Ravens.EtaHours(a2);
                etaA = e1 + e2;
            }
            any = fa != null && etaA < etaB;
            if (any)
            {
                f = fa; d1 = a1; h1 = e1; d2 = a2; h2 = e2;
                alt = "przez twierdze krolestwa jenca " + (fb != null ? Nm(fb) : "brak") + " bylaby za " + F1(etaB) + " h";
                return etaA;
            }
            f = fb; d1 = b1; h1 = g1; d2 = b2; h2 = g2;
            alt = fa != null ? "najblizsza wolna twierdza " + Nm(fa) + " (frakcja " + Nm(fa.MapFaction) + ") bylaby za " + F1(etaA) + " h" : "brak wolnej twierdzy";
            return etaB;
        }

        private static string Route(Settlement f, bool any, string alt)
        {
            string s = any ? "kruk z najblizszej twierdzy " + Nm(f) + " (frakcja " + Nm(f != null ? f.MapFaction : null) + ")"
                           : f != null ? "twierdza krolestwa jenca " + Nm(f) : "kruk z miejsca (krolestwo bez twierdz)";
            return s + ", " + alt;
        }

        private static double NewsAt(Hunt t, Vec2 q) { return t.SeatH + FromSeat(t.Seat, q); }

        private static bool SeatOf(Hunt t, Vec2 fallback, out Vec2 pos, out string name)
        {
            pos = fallback; name = "miejsce pojmania";
            Settlement st = null;
            try
            {
                if (t.Tier == TThree)
                {
                    var l = t.K.Leader;
                    st = (l != null ? l.HomeSettlement : null) ?? t.K.FactionMidSettlement ?? t.K.InitialHomeSettlement;
                }
                else st = t.Clan.HomeSettlement;
            }
            catch { st = null; }
            if (st != null) { pos = st.GetPosition2D; name = Nm(st); return true; }
            // bez siedziby: najsilniejszy wolny lord rodu (poza jencem)
            MobileParty best = null; float bs = -1f;
            foreach (var h in t.Clan.Heroes)
            {
                var mp = h != null && h != t.Captive && h.IsAlive && !h.IsPrisoner ? h.PartyBelongedTo : null;
                if (mp == null || !mp.IsActive) continue;
                float s = mp.Party.EstimatedStrength;
                if (s > bs) { bs = s; best = mp; }
            }
            if (best != null) { pos = best.GetPosition2D; name = "druzyna " + Nm(best); return true; }
            return false;
        }

        private static string NearName(Vec2 p)
        {
            try
            {
                Settlement best = null; float bd = float.MaxValue;
                var data = Settlement.StartFindingLocatablesAroundPosition(p, 60f);
                for (var s = Settlement.FindNextLocatable(ref data); s != null; s = Settlement.FindNextLocatable(ref data))
                {
                    float d = s.GetPosition2D.Distance(p);
                    if (d < bd) { bd = d; best = s; }
                }
                return best != null ? Nm(best) : "-";
            }
            catch { return "?"; }
        }

        // ------------------------------------------------------------ zdarzenia
        private static void OnTaken(PartyBase captor, Hero h)
        {
            if (!On || h == null || captor == null || Campaign.Current == null) return;
            if (_hunts.ContainsKey(h)) return;   // przeniesienie (np. do lochu) - przeglad godzinny
            if (!PlayerHolds(captor)) return;
            string why = WhyNot(captor, h);
            if (why != null)
            {
                _dSkipped++; int c; _dSkipWhy.TryGetValue(why, out c); _dSkipWhy[why] = c + 1;
                if (h.IsLord) Log.Info(P + "pojmanie bez lowow | " + When() + " | " + Nm(h) + " rodu " + Nm(h.Clan) + " - " + why + ".");
                return;
            }
            Open(captor, h, false);
        }

        /// <summary>Raz na sesje (pierwsza godzina przy kluczu): lordowie, ktorych gracz trzymal juz przed wgraniem 222 (albo przy kluczu NIE), dostaja lowy jak przy
        /// pojmaniu - poczatek niewoli z Hero.CaptivityStartTime, miejsce = gdzie jeniec jest teraz, wiesc najwczesniej teraz (w Jeffa kampanii: Tywin z 26.10).</summary>
        private static void Adopt()
        {
            int n = 0;
            foreach (var h in Hero.AllAliveHeroes)
            {
                try
                {
                    if (h == null || !h.IsPrisoner || _hunts.ContainsKey(h)) continue;
                    var pb = h.PartyBelongedToAsPrisoner;
                    if (!PlayerHolds(pb) || WhyNot(pb, h) != null) continue;
                    Open(pb, h, true);
                    n++;
                }
                catch (Exception e) { Stumble("Adopt", e); }
            }
            if (n > 0) Log.Info(P + "przejete przy starcie sesji | " + When() + " | jency gracza bez lowow: " + n + ".");
        }

        /// <summary>Otwiera lowy (watek kampanii). adopted - jeniec trzymany juz wczesniej (Adopt): poczatek z CaptivityStartTime, wiesc najwczesniej teraz.</summary>
        private static Hunt Open(PartyBase captor, Hero h, bool adopted)
        {
            var c = h.Clan; var k = c.Kingdom;
            double now = NowH, start = now;
            if (adopted)
            {
                try { double cs = h.CaptivityStartTime.ToHours; if (cs > 0.0 && cs <= now) start = cs; } catch { }
            }
            var t = new Hunt { Captive = h, Clan = c, K = k, Tier = TierOf(h, k), StartH = start };
            t.CaptorKind = captor == PartyBase.MainParty ? CMain : captor.IsSettlement ? CDungeon : CParty;
            t.CaptorId = captor.IsSettlement ? (captor.Settlement != null ? captor.Settlement.StringId : "") : (captor.MobileParty != null ? captor.MobileParty.StringId : "");
            Vec2 at = captor.IsSettlement ? captor.Settlement.GetPosition2D : captor.MobileParty.GetPosition2D;
            t.X = at.X; t.Y = at.Y;
            Vec2 seat; string seatName;
            SeatOf(t, at, out seat, out seatName);
            t.SX = seat.X; t.SY = seat.Y; t.SeatName = Clean(seatName);
            Settlement f; float d1, d2; double h1, h2; bool any; string alt;
            double eta = SeatNews(at, k, seat, out f, out d1, out h1, out d2, out h2, out any, out alt);
            t.SeatH = Math.Max(t.StartH + eta, now);
            if (captor.IsSettlement) { t.DungeonId = captor.Settlement.StringId; t.DungeonH = t.SeatH; }
            // slad pojmania - znany z wiescia (twierdza = siedziba, godzina = wiesc w siedzibie); przejety jeniec - miejsce z tej chwili
            t.Sights.Add(new Sight { H = adopted ? now : t.StartH, X = at.X, Y = at.Y, Kind = SCapture, FH = t.SeatH, FX = seat.X, FY = seat.Y });
            _hunts[h] = t;
            _dNew++;
            string by = t.CaptorKind == CMain ? "gracz" : t.CaptorKind == CParty ? "druzyna rodu gracza " + Nm(captor.MobileParty) : "loch " + Nm(captor.Settlement);
            if (adopted) by += " (przejete: w niewoli od " + F1(now - t.StartH) + " h)";
            Log.Info(P + "pojmanie | " + When() + " | " + Nm(h) + " (" + TierName[t.Tier] + ") rodu " + Nm(c) + " krolestwa " + Nm(k) + " przez " + by
                     + " | przy " + NearName(at) + " | twierdza " + (f != null ? Nm(f) : "brak") + " " + F1(d1) + " jedn. (konno " + F1(h1) + " h)"
                     + " | kruk do " + t.SeatName + " " + F1(d2) + " jedn. (" + F1(h2) + " h) | dotrze za " + F1(t.SeatH - now) + " h (godzina gry " + F1(t.SeatH) + ")"
                     + " | droga wiesci: " + Route(f, any, alt) + ".");
            return t;
        }

        private static void OnReleased(Hero h, PartyBase party, EndCaptivityDetail d)
        {
            Hunt t;
            if (h == null || !_hunts.TryGetValue(h, out t)) return;
            string why;
            switch (d)
            {
                case EndCaptivityDetail.Ransom: why = "okup"; break;
                case EndCaptivityDetail.ReleasedAfterEscape: why = "ucieczka"; break;
                case EndCaptivityDetail.ReleasedAfterBattle:
                    why = t.BattleMe != null && (t.BattleEndH < 0 || NowH - t.BattleEndH <= 1.0) ? "odbity" : "po bitwie"; break;
                case EndCaptivityDetail.ReleasedByChoice: why = "wypuszczony"; break;
                case EndCaptivityDetail.ReleasedByCompensation: why = "wykup"; break;
                case EndCaptivityDetail.ReleasedAfterPeace: why = "pokoj"; break;
                case EndCaptivityDetail.Death: why = "smierc"; break;
                default: why = d.ToString(); break;
            }
            Finish(t, why);
        }

        private static void OnKilled(Hero v)
        {
            Hunt t;
            if (v != null && _hunts.TryGetValue(v, out t)) Finish(t, "smierc");
        }

        private static MobileParty TargetParty(Hunt t)
        {
            var pb = t.Captive != null ? t.Captive.PartyBelongedToAsPrisoner : null;
            var mp = pb != null && pb.IsMobile ? pb.MobileParty : null;
            if (mp != null && mp.Army != null && mp.Army.LeaderParty != null) mp = mp.Army.LeaderParty;
            return mp;
        }

        private static bool InSide(MobileParty tp, PartyBase pb)
        {
            if (tp == null || pb == null) return false;
            if (pb == tp.Party) return true;
            var mp = pb.MobileParty;
            return mp != null && mp.Army != null && mp.Army.LeaderParty == tp;
        }

        private static void OnBattleStart(MapEvent me, PartyBase a, PartyBase d)
        {
            if (me == null || _hunts.Count == 0) return;
            foreach (var kv in _hunts)
            {
                var t = kv.Value;
                if (!t.Told) continue;
                var tp = TargetParty(t);
                if (tp == null) continue;
                PartyBase other = InSide(tp, a) ? d : InSide(tp, d) ? a : null;
                if (other == null || other.MobileParty == null || !other.MobileParty.IsLordParty || other.MapFaction != t.K) continue;
                t.Battles++; t.BattleMe = me; t.BattleEndH = -1.0;
                try { t.BattleSide = other.MapEventSide != null ? (int)other.MapEventSide.MissionSide : -1; } catch { t.BattleSide = -1; }
                Log.Info(P + "bitwa | " + When() + " | " + Nm(t.Captive) + " | lowcy " + Nm(other.MobileParty) + " (sila " + F0(Contrib(other.MobileParty)) + ") z " + Nm(tp) + ".");
            }
        }

        private static void OnBattleEnd(MapEvent me)
        {
            if (me == null || _hunts.Count == 0) return;
            foreach (var kv in _hunts)
            {
                var t = kv.Value;
                if (t.BattleMe != me) continue;
                t.BattleEndH = NowH;
                bool won = t.BattleSide >= 0 && (int)me.WinningSide == t.BattleSide;
                if (won) t.Wins++;
                Log.Info(P + "bitwa koniec | " + When() + " | " + Nm(t.Captive) + " | wygrali " + (won ? "lowcy" : "nie lowcy") + ".");
            }
        }

        private static void OnArmyCreated(Army a)
        {
            if (a == null || a.LeaderParty == null) return;
            var snap = Volatile.Read(ref _snap);
            Order o;
            if (!snap.TryGetValue(a.LeaderParty, out o) || o.Kind != OArmy || NowH - o.OfferedH > 2.0) return;
            o.H.Armies++; _dArmies++;
            Log.Info(P + "armia zalozona | " + When() + " | " + Nm(o.H.Captive) + " | wodz " + Nm(a.LeaderParty.LeaderHero) + " | zbiorka " + Nm(o.Fort) + " | sila z wezwanymi "
                     + F0(Contrib(a.LeaderParty)) + " / potrzeba " + F0(o.Need) + ".");
        }

        private static void Finish(Hunt t, string why)
        {
            if (t == null || t.Captive == null || !_hunts.Remove(t.Captive)) return;
            _dEnded++;
            // druzyny z rozkazem tych lowow mysla od nowa (bez starego celu) - migawka bez nich
            try
            {
                var old = Volatile.Read(ref _snap);
                if (old.Count > 0)
                {
                    var nw = new Dictionary<MobileParty, Order>(old.Count);
                    foreach (var kv in old)
                    {
                        if (kv.Value.H == t) { if (kv.Key.IsActive) { kv.Key.Ai.RethinkAtNextHourlyTick = true; _dRethink++; } }
                        else nw[kv.Key] = kv.Value;
                    }
                    Volatile.Write(ref _snap, nw.Count > 0 ? nw : Empty);
                }
            }
            catch (Exception e) { Stumble("Finish.Rethink", e); }
            Log.Info(P + "koniec | " + When() + " | " + Nm(t.Captive) + " - " + why + " | dob " + F1((NowH - t.StartH) / 24.0)
                     + " | wiesc " + (t.Told ? "doszla" : "nie doszla") + " | bitwy lowcow z graczem " + t.Battles + " (wygrane " + t.Wins + ") | armie " + t.Armies
                     + " | najwieksza sila " + F0(t.TopStr) + ".");
            TestEnded(t.Captive, why);
        }

        // ------------------------------------------------------------ bramka kuriera (Ransom178.ConsiderPrefix - pierwsza linia)
        /// <summary>true = kurier z okupem jeszcze nie moze przyjsc (rod jenca nie wie o pojmaniu). Nie rzuca.</summary>
        internal static bool HoldCourier(Hero h)
        {
            try
            {
                if (h == null || _hunts.Count == 0 || !On || Campaign.Current == null) return false;
                Hunt t;
                if (!_hunts.TryGetValue(h, out t)) return false;
                double now = NowH;
                if (now >= t.SeatH) return false;
                _dCourier++;
                Log.Info(P + "kurier wstrzymany | " + When() + " | " + Nm(h) + " (rod jeszcze nie wie, wiesc za " + F1(t.SeatH - now) + " h).");
                return true;
            }
            catch (Exception e) { Stumble("HoldCourier", e); return false; }
        }

        // ------------------------------------------------------------ sila
        private static float Contrib(MobileParty mp)
        {
            try
            {
                var a = mp.Army;
                if (a == null || a.LeaderParty != mp) return mp.Party.EstimatedStrength;
                float s = 0f; bool lead = false;
                var ps = a.Parties;
                for (int i = 0; i < ps.Count; i++)
                {
                    var p = ps[i];
                    if (p == null || p.Party == null) continue;
                    if (ReferenceEquals(p, mp)) lead = true;
                    s += p.Party.EstimatedStrength;
                }
                if (!lead) s += mp.Party.EstimatedStrength;
                return s;
            }
            catch { return 0f; }
        }

        private static float TargetStrength(MobileParty tp)
        {
            try { return tp.Army != null ? tp.Army.EstimatedStrength : tp.Party.EstimatedStrength; }
            catch { return 0f; }
        }

        private static float Defenders(Settlement x)
        {
            float s = 0f;
            try { foreach (var mp in x.Parties) if (mp != null && mp.Party != null) s += mp.Party.EstimatedStrength; } catch { }
            return Math.Max(1f, s);
        }

        // ------------------------------------------------------------ slady
        private static void AddSight(Hunt t, Sight s)
        {
            var l = t.Sights;
            var last = l.Count > 0 ? l[l.Count - 1] : null;
            // ta sama godzina: nowszy slad zastepuje poprzedni, ale nigdy pojmania ani lochu
            if (last != null && Math.Floor(last.H) == Math.Floor(s.H) && last.Kind != SCapture && last.Kind != SDungeon) l[l.Count - 1] = s;
            else l.Add(s);
            // recenzja 222: usuwane tylko slady starsze niz ColdHours (Hunters i tak ich nie bierze) - dalecy lowcy dostaja slad, gdy dojdzie kruk/jezdziec
            l.RemoveAll(x => s.H - x.H > ColdHours);
            while (l.Count > MaxSights) { _dSightCap++; l.RemoveAt(0); }
        }

        /// <summary>Kiedy druzyna w q dowie sie o sladzie s.</summary>
        private static double KnownAt(Sight s, MobileParty mp, Vec2 q)
        {
            if (s.Kind == SParty) return s.By == mp ? s.H : s.H + s.Pos.Distance(q) / RiderPerHour;
            return s.FH + FromSeat(new Vec2(s.FX, s.FY), q);
        }

        /// <summary>Godzinny przeglad widzenia celu (druzyna lorda albo osada krolestwa jenca).</summary>
        private static void Look(Hunt t, MobileParty tp, double now)
        {
            if (tp == null || tp.IsCurrentlyAtSea) return;   // na morzu - brak sladu (trop ginie)
            var pos = tp.GetPosition2D;
            var vm = Campaign.Current.Models.MapVisibilityModel;
            float max = vm.MaximumSeeingRange();
            MobileParty by = null; float bd = float.MaxValue;
            var data = MobileParty.StartFindingLocatablesAroundPosition(pos, max);
            for (var mp = MobileParty.FindNextLocatable(ref data); mp != null; mp = MobileParty.FindNextLocatable(ref data))
            {
                if (mp == tp || !mp.IsActive || !mp.IsLordParty || mp.MapFaction != t.K || mp.CurrentSettlement != null) continue;
                float d = mp.GetPosition2D.Distance(pos);
                if (d >= bd || d > mp.SeeingRange) continue;
                bd = d; by = mp;
            }
            Sight s = null; Settlement sst = null;
            if (by != null) s = new Sight { H = now, X = pos.X, Y = pos.Y, Kind = SParty, FH = now, FX = pos.X, FY = pos.Y, By = by };
            else
            {
                if (tp.CurrentSettlement != null && tp.CurrentSettlement.MapFaction == t.K) sst = tp.CurrentSettlement;
                else
                {
                    float r = vm.GetPartySeeingRangeBase(tp);   // 12 dniem / 6 noca - podstawa widzenia gry
                    var sd = Settlement.StartFindingLocatablesAroundPosition(pos, r);
                    float best = float.MaxValue;
                    for (var st = Settlement.FindNextLocatable(ref sd); st != null; st = Settlement.FindNextLocatable(ref sd))
                    {
                        if (st.MapFaction != t.K || !(st.IsTown || st.IsCastle || st.IsVillage)) continue;
                        float d = st.GetPosition2D.Distance(pos);
                        if (d <= r && d < best) { best = d; sst = st; }
                    }
                }
                if (sst != null)
                {
                    Settlement f; float d1, d2; double h1, h2;
                    var fpos = NearestFort(t.K, pos);
                    NewsPlan(pos, t.K, fpos != null ? fpos.GetPosition2D : pos, out f, out d1, out h1, out d2, out h2);
                    s = new Sight { H = now, X = pos.X, Y = pos.Y, Kind = SSettlement, FH = now + h1, FX = f != null ? f.GetPosition2D.X : pos.X, FY = f != null ? f.GetPosition2D.Y : pos.Y };
                }
            }
            if (s == null) return;
            AddSight(t, s);
            _dSight++;
            bool column = tp == MobileParty.MainParty || (MobileParty.MainParty != null && MobileParty.MainParty.Army != null && MobileParty.MainParty.Army.LeaderParty == tp);
            if (by != null && t.Told && column && TellOn && now - t.MarkH >= MarkMsgHours)
            {
                t.MarkH = now; _dMarkMsg++;
                Show("Riders of " + House(by.ActualClan) + " have marked your column.");
            }
            if (now - t.SightLogH >= SightLogHours)
            {
                t.SightLogH = now;
                Log.Info(P + "widziany | " + When() + " | " + Nm(t.Captive) + " | przez " + (by != null ? "druzyna " + Nm(by) + " (" + F1(bd) + " jedn.)" : "osada " + Nm(sst))
                         + " przy " + NearName(pos) + " | wiesc " + (t.Told ? "doszla" : "jeszcze nie doszla") + " | znane w twierdzy za " + F1(s.FH - now) + " h.");
            }
        }

        private static void Show(string text)
        {
            try
            {
                uint color = Campaign.Current.Models.DiplomacyModel.GetNotificationColor(ChatNotificationType.PlayerClanNegative);
                InformationManager.DisplayMessage(new InformationMessage(text, Color.FromUint(color)));
            }
            catch (Exception e) { Stumble("Show", e); }
        }

        // ------------------------------------------------------------ godzina
        private sealed class Cand { public MobileParty Mp; public float Str, D; public Vec2 Pos, Kp; public bool Own, Sees; public double Age; }
        private static readonly List<Cand> _cands = new List<Cand>();
        private static readonly List<Hunt> _tmpHunts = new List<Hunt>();
        private static readonly List<KeyValuePair<Hunt, string>> _tmpEnd = new List<KeyValuePair<Hunt, string>>();

        private static void Hourly()
        {
            if (Campaign.Current == null) return;
            double now = Math.Floor(NowH);
            if (now == _lastHour) return;
            _lastHour = now;
            _hourPrevH = _hourNowH; _hourNowH = NowH;
            Resolve();
            if (On && !_adopted) { _adopted = true; try { Adopt(); } catch (Exception e) { Stumble("Adopt", e); } }
            var old = Volatile.Read(ref _snap);
            var nw = new Dictionary<MobileParty, Order>();
            if (On && _hunts.Count > 0)
            {
                _tmpHunts.Clear(); _tmpHunts.AddRange(_hunts.Values); _tmpEnd.Clear();
                foreach (var t in _tmpHunts)
                {
                    try { Step(t, NowH, nw); }
                    catch (Exception e) { Stumble("Step", e); }
                }
                foreach (var kv in _tmpEnd) Finish(kv.Key, kv.Value);
                _tmpEnd.Clear(); _tmpHunts.Clear();
            }
            // nowa mysl: nowy albo zmieniony rozkaz, rozkaz cofniety
            try
            {
                foreach (var kv in nw)
                {
                    Order o;
                    if (!old.TryGetValue(kv.Key, out o) || Changed(o, kv.Value)) Rethink(kv.Key);
                    else kv.Value.OfferedH = o.OfferedH;
                }
                foreach (var kv in old) if (!nw.ContainsKey(kv.Key)) Rethink(kv.Key);
            }
            catch (Exception e) { Stumble("Rethink", e); }
            Volatile.Write(ref _snap, nw.Count > 0 ? nw : Empty);
            try { TestHour(); } catch (Exception e) { Stumble("TestHour", e); }
        }

        private static void Rethink(MobileParty mp)
        {
            if (mp == null || !mp.IsActive || mp.Ai == null) return;
            mp.Ai.RethinkAtNextHourlyTick = true;
            _dRethink++;
        }

        private static bool Changed(Order a, Order b)
        {
            if (a.Kind != b.Kind || a.H != b.H || a.Target != b.Target || a.Fort != b.Fort || a.Dungeon != b.Dungeon) return true;
            return a.Kind == OPatrol && a.Pos.Distance(b.Pos) > 1f;
        }

        private static void End(Hunt t, string why) { _tmpEnd.Add(new KeyValuePair<Hunt, string>(t, why)); }

        /// <summary>Kolumna gracza (druzyna gracza albo wodz jego armii); gracz w niewoli - null.</summary>
        private static MobileParty PlayerColumn()
        {
            var mp = MobileParty.MainParty;
            if (mp == null || Hero.MainHero == null || Hero.MainHero.IsPrisoner || !mp.IsActive) return null;
            if (mp.Army != null && mp.Army.LeaderParty != null) mp = mp.Army.LeaderParty;
            return mp;
        }

        private static void Step(Hunt t, double now, Dictionary<MobileParty, Order> nw)
        {
            var h = t.Captive;
            if (h == null || !h.IsAlive) { End(t, "smierc"); return; }
            if (!h.IsPrisoner || !PlayerHolds(h.PartyBelongedToAsPrisoner)) { End(t, "nie u gracza"); return; }
            var c = t.Clan;
            if (c == null || c.IsEliminated || c == Clan.PlayerClan || c.Kingdom == null) { End(t, "zmiana strony"); return; }
            if (Hero.MainHero != null && Hero.MainHero.MapFaction == c.Kingdom) { End(t, "zmiana strony"); return; }
            if (c.Kingdom != t.K)
            {
                Log.Info(P + "zmiana krolestwa | " + When() + " | " + Nm(h) + " | rod " + Nm(c) + ": " + Nm(t.K) + " -> " + Nm(c.Kingdom) + ".");
                t.K = c.Kingdom;
            }
            var pb = h.PartyBelongedToAsPrisoner;
            Settlement x = pb.IsSettlement ? pb.Settlement : null;
            // jeniec w lochu, o ktorym rod jeszcze nie wie - lowcy dalej tropia kolumne gracza (sadza, ze to ona go wiezie)
            MobileParty tp = x == null ? TargetParty(t) : (t.DungeonTold && t.DungeonId == x.StringId ? null : PlayerColumn());
            // loch
            if (x != null && t.DungeonId != x.StringId)
            {
                // 222c: o lochu tez szybsza z dwoch drog (zwykle kruk maestra samej twierdzy lochu, gdy nie jest oblegana)
                Settlement f; float d1, d2; double h1, h2; bool any; string alt;
                double eta = SeatNews(x.GetPosition2D, t.K, t.Seat, out f, out d1, out h1, out d2, out h2, out any, out alt);
                t.DungeonId = x.StringId; t.DungeonH = Math.Max(now + eta, t.SeatH); t.DungeonTold = false;
                var xp = x.GetPosition2D;
                AddSight(t, new Sight { H = now, X = xp.X, Y = xp.Y, Kind = SDungeon, FH = t.DungeonH, FX = t.SX, FY = t.SY });
                Log.Info(P + "loch | " + When() + " | " + Nm(h) + " w lochu " + Nm(x) + " | wiesc do " + t.SeatName + " za " + F1(t.DungeonH - now) + " h (godzina gry " + F1(t.DungeonH) + ")"
                         + " | droga wiesci: " + Route(f, any, alt) + ".");
            }
            else if (x == null && t.DungeonId.Length > 0)
            {
                Log.Info(P + "z lochu | " + When() + " | " + Nm(h) + " znow w druzynie " + Nm(tp) + ".");
                t.DungeonId = ""; t.DungeonH = -1.0; t.DungeonTold = false;
            }
            IFaction tf = x != null ? x.MapFaction : (tp != null ? tp.MapFaction : null);
            bool war = tf != null && tf.IsAtWarWith(t.K);
            if (tp != null) Look(t, tp, now);
            bool arrived = false;
            if (now >= t.SeatH && !t.Told) { t.Told = true; arrived = true; }
            if (!war)
            {
                if (t.PeaceH < 0) t.PeaceH = now;
                if (arrived) Log.Info(P + "wiesc doszla | " + When() + " | rod " + Nm(c) + " wie o " + Nm(h) + " po " + F1(now - t.StartH) + " h (godzina gry " + F1(now) + ", plan " + F1(t.SeatH) + ") | bez wojny - bez poscigu.");
                if (now - t.PeaceH >= PeaceEndHours) End(t, "pokoj");
                return;
            }
            t.PeaceH = -1.0;
            if (now < t.SeatH) return;
            if (arrived && TellOn)
            {
                string pr = h.IsFemale ? "her" : "him";
                if (t.Tier == TThree)
                    Show(Nm(t.K) + " has learned that you hold " + Nm(h) + (t.K.Leader == h ? ", their liege" : " of their ruling house") + ". The lords of the realm ride to free " + pr + ".");
                else Show(House(c) + " has learned that you hold " + Nm(h) + ". Their lords ride to free " + pr + ".");
            }
            if (x != null && t.DungeonH >= 0 && now >= t.DungeonH && !t.DungeonTold)
            {
                t.DungeonTold = true;
                if (TellOn) Show(House(c) + " has learned that " + Nm(h) + " is held at " + Nm(x) + ".");
                Log.Info(P + "loch -> cel " + Nm(x) + " | " + When() + " | " + Nm(h) + " | rod " + Nm(c) + " wie o lochu.");
            }
            bool xKnown = x != null && t.DungeonTold;
            Hunters(t, xKnown ? null : tp, xKnown ? x : null, now, nw, arrived);
        }

        private static bool Friendly(Clan mc, Hunt t)
        {
            if (mc == null || mc.IsUnderMercenaryService) return false;
            var l = mc.Leader;
            if (l == null) return false;
            if (l.IsFriend(t.Captive)) return true;
            var cl = t.Clan.Leader;
            return cl != null && cl != t.Captive && l.IsFriend(cl);
        }

        private static void Hunters(Hunt t, MobileParty tp, Settlement x, double now, Dictionary<MobileParty, Order> nw, bool arrived)
        {
            _cands.Clear(); t.Elig.Clear();
            float march = Campaign.Current.EstimatedAverageLordPartySpeed * 24f;
            float engage = Campaign.Current.Models.EncounterModel.NeededMaximumLandDistanceForEncounteringMobileParty * EngageMul;
            Vec2 tpos = tp != null ? tp.GetPosition2D : (x != null ? x.GetPosition2D : Vec2.Zero);
            bool tVisible = tp != null && tp.CurrentSettlement == null && !tp.IsCurrentlyAtSea;
            float max = Campaign.Current.Models.MapVisibilityModel.MaximumSeeingRange();
            IFaction tf = x != null ? x.MapFaction : tp != null ? tp.MapFaction : null;
            int notYet = 0, cold = 0, far = 0, skip = 0, trip = 0, held = 0;
            var wpcs = t.K.WarPartyComponents;
            for (int i = 0; i < wpcs.Count; i++)
            {
                var mp = wpcs[i] != null ? wpcs[i].MobileParty : null;
                if (mp == null || !mp.IsActive || !mp.IsLordParty || mp.IsMainParty || mp.LeaderHero == null) continue;
                var mc = mp.ActualClan;
                if (mc == null) continue;
                bool own = mc == t.Clan;
                if (!own && !(RealmOn && (t.Tier == TThree || (t.Tier == TTwo && Friendly(mc, t))))) continue;
                if (mp.Army != null && mp.Army.LeaderParty != mp) continue;
                if (tf == null || mp.MapFaction == null || !mp.MapFaction.IsAtWarWith(tf)) continue;
                if (mp.MapEvent != null || (mp.CurrentSettlement != null && mp.CurrentSettlement.SiegeEvent != null)) { skip++; continue; }
                if (mp.BesiegedSettlement != null && mp.BesiegedSettlement != x) { skip++; continue; }
                if (Undead.Party(mp) || Prologue.Scripted(mp) || mp.Party.IsStarving || ArmsFirst.Unready(mp)) { skip++; continue; }
                // recenzja 222: wyjazd po pierwsza bron 159c (CapThink po tym postfiksie zawsze go przebija) - nie wybierac i nie liczyc sily, idzie nastepny
                if (ArmsFirst.CapTripActive(mp)) { skip++; trip++; _dSkipTrip++; continue; }
                // scalenie 222 + 223: lord wstrzymany przez 223 (slaby albo narazony - nie zaczepia, jedzie do armii / twierdzy) - nie wybierac i nie liczyc sily
                if (LordEscort.HeldBack(mp)) { skip++; held++; _dSkipHeld++; continue; }
                var q = mp.GetPosition2D;
                if (now < NewsAt(t, q)) { notYet++; continue; }
                // recenzja 222: o lochu kazdy dowiaduje sie wiescia z siedziby (DungeonH + kruk), nie razem z siedziba - kto jeszcze nie wie, nie oblega
                if (x != null && now < t.DungeonH + FromSeat(t.Seat, q)) { notYet++; continue; }
                Vec2 kp; double age = 0;
                if (x != null) kp = x.GetPosition2D;
                else
                {
                    Sight best = null;
                    for (int j = t.Sights.Count - 1; j >= 0; j--)
                    {
                        var s = t.Sights[j];
                        if (now - s.H > ColdHours) continue;
                        if (KnownAt(s, mp, q) > now) continue;
                        if (best == null || s.H > best.H) best = s;
                    }
                    if (best == null) { cold++; continue; }
                    kp = best.Pos; age = now - best.H;
                }
                float d = q.Distance(kp);
                double reach = (own || t.Tier == TThree ? ReachOwnDays : ReachHelpDays) * march;
                if (d > reach) { far++; continue; }
                bool sees = false;
                if (tVisible)
                {
                    float dt = q.Distance(tpos);
                    sees = dt <= max && dt <= mp.SeeingRange;
                }
                _cands.Add(new Cand { Mp = mp, Str = Contrib(mp), D = d, Pos = q, Kp = kp, Own = own, Sees = sees, Age = age });
                t.Elig.Add(mp);
            }
            float tStr = x != null ? Defenders(x) : (tp != null ? TargetStrength(tp) : 0f);
            float need = NeedMult * tStr;
            float total = 0f;
            foreach (var cd in _cands) total += cd.Str;
            _cands.Sort(delegate (Cand a, Cand b) { return a.D.CompareTo(b.D); });
            float sel = 0f; int nSel = 0;
            string mode;
            int nChase = 0, nPatrol = 0, nSiege = 0;
            if (x == null && tp == null) mode = "brak celu";   // gracz w niewoli, jeniec w lochu, o ktorym rod nie wie - bez rozkazow
            else if (_cands.Count == 0) mode = cold > 0 ? "trop zimny" : "brak lowcow";
            else if (total < need)
            {
                // za slabi wszyscy - wodz lowow zwoluje armie (gdy gra da mu druzyny), inaczej czekaja
                Cand lead = null;
                Hero want = t.Tier == TThree ? t.K.Leader : t.Clan.Leader;
                foreach (var cd in _cands) if (cd.Mp.Army == null && cd.Mp.LeaderHero == want && want != t.Captive) { lead = cd; break; }
                if (lead == null)
                {
                    Clan lc = t.Tier == TThree ? t.K.RulingClan : t.Clan;
                    foreach (var cd in _cands) if (cd.Mp.Army == null && cd.Mp.ActualClan == lc && (lead == null || cd.Str > lead.Str)) lead = cd;
                }
                if (lead != null && x == null)
                {
                    var fort = NearestFort(t.K, lead.Kp);
                    if (fort != null)
                    {
                        nw[lead.Mp] = new Order { H = t, Kind = OArmy, Fort = fort, P = PFor(t, lead.Own), Need = need, Own = lead.Str };
                        mode = "armia";
                    }
                    else mode = "za slabi - czekaja";
                }
                else mode = x != null ? "loch - za slabi" : "za slabi - czekaja";
            }
            else
            {
                // najblizsi az do potrzeby (bez nadmiaru)
                float nearT = 0f;
                if (tVisible) foreach (var cd in _cands) if (cd.Pos.Distance(tpos) <= engage) nearT += cd.Str;
                foreach (var cd in _cands)
                {
                    if (sel >= need) break;
                    sel += cd.Str; nSel++;
                    float p = PFor(t, cd.Own);
                    if (x != null) { nw[cd.Mp] = new Order { H = t, Kind = OSiege, Dungeon = x, P = p, Need = need }; nSiege++; }
                    else if (cd.Sees) { if (nearT >= need) { nw[cd.Mp] = new Order { H = t, Kind = OChase, Target = tp, P = p, Need = need }; nChase++; } }
                    else { nw[cd.Mp] = new Order { H = t, Kind = OPatrol, Pos = cd.Kp, P = p, Need = need }; nPatrol++; }
                }
                mode = nSiege > 0 ? "oblezenie lochu" : nChase > 0 ? "poscig" : nPatrol > 0 ? "szuka" : "widza - za slabi blisko";
                if (sel > t.TopStr) t.TopStr = sel;
            }
            if (arrived)
            {
                string last = "-";
                if (t.Sights.Count > 0) { var ls = t.Sights[t.Sights.Count - 1]; last = NearName(ls.Pos) + " " + F0(now - ls.H) + " h temu (" + SightName[ls.Kind] + ")"; }
                Log.Info(P + "wiesc doszla | " + When() + " | rod " + Nm(t.Clan) + " wie o " + Nm(t.Captive) + " po " + F1(now - t.StartH) + " h (godzina gry " + F1(now) + ", plan " + F1(t.SeatH) + ")"
                         + " | wybrani " + nSel + " sila " + F0(sel) + " / potrzeba " + F0(need) + " (1.5 x " + F0(tStr) + ") | lowcow " + _cands.Count + " sila " + F0(total)
                         + " (jeszcze nie wiedza " + notYet + ", zimny trop " + cold + ", za daleko " + far + ", pominieci " + skip + " w tym wyjazd po bron " + trip + ", wstrzymani 223 " + held + ") | zasieg " + F2(ReachOwnDays) + "/" + F2(ReachHelpDays)
                         + " dob | ostatnio widziany " + last + ".");
            }
            if (mode != t.Mode)
            {
                t.Mode = mode;
                Log.Info(P + mode + " | " + When() + " | " + Nm(t.Captive) + " | wybrani " + nSel + " sila " + F0(sel) + " / potrzeba " + F0(need) + " | lowcow " + _cands.Count + " sila " + F0(total)
                         + " | cel " + (x != null ? "loch " + Nm(x) : Nm(tp)) + " (sila " + F0(tStr) + ") | rozkazy: poscig " + nChase + ", szukanie " + nPatrol + ", oblezenie " + nSiege
                         + (mode == "armia" ? ", armia 1" : "") + " | nie wiedza " + notYet + ", zimny trop " + cold + ", za daleko " + far + ", pominieci " + skip + " (wyjazd po bron " + trip + ", wstrzymani 223 " + held + ").");
            }
            _cands.Clear();
        }

        private static float PFor(Hunt t, bool own) { return t.Tier == TThree ? PRealm : own ? POwn : PHelp; }

        // ------------------------------------------------------------ postfiks mysli AI
        /// <summary>Postfiks 150 CampaignEventDispatcher.AiHourlyTick: rozkaz lowow z migawki (regula w opisie klasy). Czyta tylko migawke. Nie rzuca.</summary>
        public static void ThinkPostfix(MobileParty party, PartyThinkParams partyThinkParams)
        {
            var snap = Volatile.Read(ref _snap);
            if (snap.Count == 0 || party == null || partyThinkParams == null) return;
            Order o;
            if (!snap.TryGetValue(party, out o)) return;
            try
            {
                if (!On) return;
                if (partyThinkParams.DoNotChangeBehavior) { Interlocked.Increment(ref _dSkipDnc); return; }
                if (party.MapEvent != null || (party.CurrentSettlement != null && party.CurrentSettlement.SiegeEvent != null)) return;
                if (party.Army != null && party.Army.LeaderParty != party) return;
                var scores = partyThinkParams.AIBehaviorScores;
                if (scores == null) return;
                var clan = party.ActualClan; var f = party.MapFaction;
                float other = 0f, top = float.MinValue; bool topRelief = false, ownThreat = false;
                for (int i = 0; i < scores.Count; i++)
                {
                    var d = scores[i].Item1; float v = scores[i].Item2;
                    bool relief = false;
                    if (d.AiBehavior == AiBehavior.DefendSettlement)
                    {
                        var st = d.Party as Settlement;
                        if (st != null)
                        {
                            bool threat = st.SiegeEvent != null || (st.Party != null && st.Party.MapEvent != null);
                            if (threat && clan != null && st.OwnerClan == clan) ownThreat = true;
                            // recenzja 222: armia odsieczy wladcy OB5 (DefendSettlement + WillGatherArmy, KingArmyEdge) tez jest odsiecza - gra sama takiej opcji nie daje
                            relief = st.SiegeEvent != null && st.MapFaction == f;
                        }
                    }
                    if (v > top) { top = v; topRelief = relief; }
                    if (!Ours(o, d) && v > other) other = v;
                }
                if (ownThreat) { Interlocked.Increment(ref _dSkipOwn); return; }
                if (topRelief) { Interlocked.Increment(ref _dSkipRelief); return; }
                float b = Math.Max(other, FloorBase);
                switch (o.Kind)
                {
                    case OChase: Chase(party, partyThinkParams, o, b); break;
                    case OPatrol:
                        {
                            if (party.IsCurrentlyAtSea) return;
                            var a = new AIBehaviorData(new CampaignVec2(o.Pos, true), AiBehavior.PatrolAroundPoint, MobileParty.NavigationType.Default, false, false, false);
                            var item = (a, o.P * b);
                            partyThinkParams.AddBehaviorScore(in item);
                            Interlocked.Increment(ref _dPatrol);
                            break;
                        }
                    case OArmy:
                        {
                            if (party.Army != null || o.Fort == null) return;
                            var mem = partyThinkParams.PossibleArmyMembersUponArmyCreation;
                            if (mem == null || mem.Count == 0) { Interlocked.Increment(ref _dArmyNoCall); return; }
                            float s = o.Own;
                            for (int i = 0; i < mem.Count; i++) if (mem[i] != null && mem[i].Party != null) s += mem[i].Party.EstimatedStrength;
                            if (s < o.Need) { Interlocked.Increment(ref _dArmyShort); return; }
                            var a = new AIBehaviorData(o.Fort, AiBehavior.PatrolAroundPoint, MobileParty.NavigationType.Default, true, false, false);
                            var item = (a, o.P * b * ArmyEdge);
                            partyThinkParams.AddBehaviorScore(in item);
                            o.OfferedH = NowH;
                            Interlocked.Increment(ref _dArmyOffer);
                            break;
                        }
                    case OSiege:
                        for (int i = 0; i < scores.Count; i++)
                        {
                            var d = scores[i].Item1; float v = scores[i].Item2;
                            if (v <= -1f) continue;   // cudza blokada (RaidTribute, p9, Straz) - bez zmian
                            float nv;
                            if (d.AiBehavior == AiBehavior.BesiegeSettlement && ReferenceEquals(d.Party, o.Dungeon)) nv = o.P * b + 0.01f * Math.Max(0f, v);
                            else if (d.AiBehavior == AiBehavior.RaidSettlement && IsBoundVillage(d.Party as Settlement, o.Dungeon)) nv = RaidEdge * b;
                            else continue;
                            if (nv <= v) continue;
                            partyThinkParams.SetBehaviorScore(in d, nv);
                            if (d.AiBehavior == AiBehavior.BesiegeSettlement) Interlocked.Increment(ref _dSiege); else Interlocked.Increment(ref _dRaid);
                        }
                        break;
                }
            }
            catch (Exception e) { Stumble("ThinkPostfix", e); }
        }

        private static bool IsBoundVillage(Settlement v, Settlement x)
        {
            return v != null && x != null && v.IsVillage && v.Village != null && v.Village.Bound == x;
        }

        private static bool Ours(Order o, in AIBehaviorData d)
        {
            switch (o.Kind)
            {
                case OChase: return d.AiBehavior == AiBehavior.GoAroundParty && ReferenceEquals(d.Party, o.Target);
                case OSiege: return (d.AiBehavior == AiBehavior.BesiegeSettlement && ReferenceEquals(d.Party, o.Dungeon)) || (d.AiBehavior == AiBehavior.RaidSettlement && IsBoundVillage(d.Party as Settlement, o.Dungeon));
                default: return false;
            }
        }

        private static void Chase(MobileParty party, PartyThinkParams pt, Order o, float b)
        {
            var tgt = o.Target;
            if (tgt == null || !tgt.IsActive) return;
            var scores = pt.AIBehaviorScores;
            for (int i = 0; i < scores.Count; i++)
            {
                var d = scores[i].Item1;
                if (d.AiBehavior != AiBehavior.GoAroundParty || d.WillGatherArmy || !ReferenceEquals(d.Party, tgt)) continue;
                float v = scores[i].Item2;
                if (v <= -1f) return;   // cudza blokada - bez zmian
                float nv = o.P * b + 0.01f * Math.Max(0f, v);
                if (nv > v) { pt.SetBehaviorScore(in d, nv); Interlocked.Increment(ref _dRaise); }
                return;
            }
            MobileParty.NavigationType nav; float dist;
            AiHelper.GetBestNavigationTypeAndDistanceOfMobilePartyForMobileParty(party, tgt, out nav, out dist);
            if (nav == MobileParty.NavigationType.None) return;
            var a = new AIBehaviorData(tgt, AiBehavior.GoAroundParty, nav, false, false, false);
            var item = (a, o.P * b);
            pt.AddBehaviorScore(in item);
            Interlocked.Increment(ref _dAdd);
        }

        // ------------------------------------------------------------ doba
        private static void Daily()
        {
            _sessionDays++;
            try
            {
                int pre = 0, chase = 0, look = 0, army = 0, weak = 0, dun = 0, other = 0;
                double now = NowH;
                foreach (var kv in _hunts)
                {
                    var t = kv.Value;
                    if (now < t.SeatH) pre++;
                    else if (t.Mode == "poscig") chase++;
                    else if (t.Mode == "szuka") look++;
                    else if (t.Mode == "armia") army++;
                    else if (t.Mode.StartsWith("za slabi", StringComparison.Ordinal) || t.Mode == "loch - za slabi") weak++;
                    else if (t.Mode == "oblezenie lochu") dun++;
                    else other++;
                }
                var sk = new StringBuilder();
                foreach (var kv in _dSkipWhy) { if (sk.Length > 0) sk.Append(", "); sk.Append(kv.Key).Append(' ').Append(kv.Value); }
                Log.Info(P + "dzien " + Today + " | " + (On ? "CZYNNE" : "WYLACZONE (Rescue Hunt NIE)") + " | lowy czynne " + _hunts.Count + " (przed wiescia " + pre + ", poscig " + chase + ", szuka " + look
                         + ", armia " + army + ", za slabi " + weak + ", oblezenie lochu " + dun + ", inne " + other + ")"
                         + " | dzis: nowe " + _dNew + ", skonczone " + _dEnded + ", pojmania bez lowow " + _dSkipped + (sk.Length > 0 ? " (" + sk + ")" : "")
                         + " | rozkazy w mysli AI: poscig podbity " + _dRaise + ", poscig dodany " + _dAdd + ", szukanie " + _dPatrol + ", armia " + _dArmyOffer + " (bez druzyn do wezwania "
                         + _dArmyNoCall + ", za slabi z wezwanymi " + _dArmyShort + ", zalozone " + _dArmies + "), oblezenie lochu " + _dSiege + ", najazd wsi lochu " + _dRaid
                         + " | pominiete: odsiecz OB5 " + _dSkipRelief + ", obrona osady wlasnego rodu " + _dSkipOwn + ", zbiorka armii " + _dSkipDnc + ", wyjazd po bron (druzynogodziny) " + _dSkipTrip + ", wstrzymani przez 223 (druzynogodziny) " + _dSkipHeld
                         + " | slady " + _dSight + " (bezpiecznik " + _dSightCap + ")" + ", komunikaty 'marked your column' " + _dMarkMsg + ", wstrzymani kurierzy " + _dCourier + ", przemyslenia " + _dRethink
                         + " | kurier 178 " + CourierWired() + " | potkniecia " + _dStumbles + " (sesja " + _stumbles + ").");
            }
            catch (Exception e) { Stumble("Daily.Line", e); }
            ClearDay();
            try { TestStep(Today); } catch (Exception e) { Stumble("TestStep", e); }
        }

        // ------------------------------------------------------------ zapis
        internal static string Export()
        {
            try
            {
                if (_hunts.Count == 0 && _pendingH.Count == 0) return "";
                var sb = new StringBuilder(256 + _hunts.Count * 400);
                sb.Append("v1");
                foreach (var kv in _hunts)
                {
                    var t = kv.Value;
                    sb.Append("\nh\t").Append(t.Captive.StringId).Append('\t').Append(t.Clan != null ? t.Clan.StringId : "").Append('\t').Append(t.K != null ? t.K.StringId : "")
                      .Append('\t').Append(t.Tier.ToString(Inv)).Append('\t').Append(t.CaptorKind.ToString(Inv)).Append('\t').Append(Clean(t.CaptorId))
                      .Append('\t').Append(t.StartH.ToString("R", Inv)).Append('\t').Append(t.X.ToString("R", Inv)).Append('\t').Append(t.Y.ToString("R", Inv))
                      .Append('\t').Append(Clean(t.SeatName)).Append('\t').Append(t.SX.ToString("R", Inv)).Append('\t').Append(t.SY.ToString("R", Inv)).Append('\t').Append(t.SeatH.ToString("R", Inv))
                      .Append('\t').Append(t.Told ? "1" : "0").Append('\t').Append(Clean(t.DungeonId)).Append('\t').Append(t.DungeonH.ToString("R", Inv)).Append('\t').Append(t.DungeonTold ? "1" : "0")
                      .Append('\t').Append(t.Armies.ToString(Inv)).Append('\t').Append(t.Battles.ToString(Inv)).Append('\t').Append(t.Wins.ToString(Inv)).Append('\t').Append(t.TopStr.ToString("R", Inv));
                    foreach (var s in t.Sights)
                    {
                        sb.Append("\ns\t").Append(t.Captive.StringId).Append('\t').Append(s.H.ToString("R", Inv)).Append('\t').Append(s.X.ToString("R", Inv)).Append('\t').Append(s.Y.ToString("R", Inv))
                          .Append('\t').Append(s.Kind.ToString(Inv)).Append('\t').Append(s.FH.ToString("R", Inv)).Append('\t').Append(s.FX.ToString("R", Inv)).Append('\t').Append(s.FY.ToString("R", Inv));
                    }
                }
                // nierozpoznane jeszcze wiersze (wczytanie przed startem sesji) - bez zmian
                foreach (var r in _pendingH) sb.Append('\n').Append(string.Join("\t", r));
                foreach (var r in _pendingS) sb.Append('\n').Append(string.Join("\t", r));
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            // recenzja 222: brak klucza (stary zapis) = null -> _importN zostaje -1 ("brak klucza"); zapis 222 bez lowow ma "" -> 0
            _hunts.Clear(); _pendingH.Clear(); _pendingS.Clear(); _importN = data == null ? -1 : 0; _importBad = 0;
            Volatile.Write(ref _snap, Empty);
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var lines = data.Split('\n');
                if (lines.Length == 0 || lines[0].Trim() != "v1") { _importBad++; return; }
                for (int i = 1; i < lines.Length; i++)
                {
                    var l = lines[i].TrimEnd('\r');
                    if (l.Length == 0) continue;
                    var f = l.Split('\t');
                    if (f[0] == "h" && f.Length >= 22) { _pendingH.Add(f); _importN++; }
                    else if (f[0] == "s" && f.Length >= 9) _pendingS.Add(f);
                    else _importBad++;
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }

        private static double D(string s) { return double.Parse(s, NumberStyles.Float, Inv); }
        private static float Fl(string s) { return float.Parse(s, NumberStyles.Float, Inv); }
        private static int I(string s) { return int.Parse(s, NumberStyles.Integer, Inv); }

        /// <summary>Wiersze z zapisu -> lowy (bohaterowie po StringId; po wczytaniu, w starcie sesji albo w pierwszej godzinie). Nierozpoznane - liczone i porzucone.</summary>
        private static void Resolve()
        {
            if (_pendingH.Count == 0 && _pendingS.Count == 0) return;
            if (Campaign.Current == null) return;
            var map = new Dictionary<string, Hunt>(StringComparer.Ordinal);
            foreach (var f in _pendingH)
            {
                try
                {
                    var h = Hero.Find(f[1]);
                    var c = Clan.FindFirst(x => x.StringId == f[2]);
                    var k = Kingdom.All.FirstOrDefault(x => x.StringId == f[3]);
                    if (h == null || c == null || !h.IsAlive || !h.IsPrisoner) { _importBad++; continue; }
                    var t = new Hunt
                    {
                        Captive = h, Clan = c, K = k ?? c.Kingdom, Tier = I(f[4]), CaptorKind = I(f[5]), CaptorId = f[6], StartH = D(f[7]), X = Fl(f[8]), Y = Fl(f[9]),
                        SeatName = f[10], SX = Fl(f[11]), SY = Fl(f[12]), SeatH = D(f[13]), Told = f[14] == "1", DungeonId = f[15], DungeonH = D(f[16]), DungeonTold = f[17] == "1",
                        Armies = I(f[18]), Battles = I(f[19]), Wins = I(f[20]), TopStr = Fl(f[21])
                    };
                    if (t.K == null) { _importBad++; continue; }
                    _hunts[h] = t; map[f[1]] = t;
                }
                catch { _importBad++; }
            }
            foreach (var f in _pendingS)
            {
                try
                {
                    Hunt t;
                    if (!map.TryGetValue(f[1], out t)) continue;
                    t.Sights.Add(new Sight { H = D(f[2]), X = Fl(f[3]), Y = Fl(f[4]), Kind = I(f[5]), FH = D(f[6]), FX = Fl(f[7]), FY = Fl(f[8]) });
                }
                catch { _importBad++; }
            }
            _pendingH.Clear(); _pendingS.Clear();
            // 222c: lowy z zapisu 222/222b dostaja wczesniejszy plan wiesci (najblizsza twierdza dowolnej frakcji) - min(stary plan, nowy plan)
            foreach (var t in map.Values)
            {
                try { Recalc(t); } catch (Exception e) { Stumble("Recalc", e); }
            }
        }

        /// <summary>222c: plan wiesci lowow z zapisu liczony od nowa z miejsca pojmania (X, Y), siedziby i poczatku niewoli - tylko gdy wiesc jeszcze nie doszla
        /// i nowy plan wczesniejszy; najwczesniej teraz (jak Open). Slad pojmania i loch z tym samym planem przesuwane razem. Ten sam plan - bez zmian
        /// (zapis tam i z powrotem T3 identyczny).</summary>
        private static void Recalc(Hunt t)
        {
            if (t == null || t.Told || t.K == null) return;
            Settlement f; float d1, d2; double h1, h2; bool any; string alt;
            double eta = SeatNews(new Vec2(t.X, t.Y), t.K, t.Seat, out f, out d1, out h1, out d2, out h2, out any, out alt);
            double now = NowH, old = t.SeatH;
            double nh = Math.Max(t.StartH + eta, now);
            if (nh >= old - 0.01) return;
            t.SeatH = nh;
            foreach (var s in t.Sights) if ((s.Kind == SCapture || s.Kind == SDungeon) && Math.Abs(s.FH - old) < 1e-6) s.FH = nh;
            if (t.DungeonH >= 0.0 && Math.Abs(t.DungeonH - old) < 1e-6) t.DungeonH = nh;
            Log.Info(P + "wiesc przeliczona (222c) | " + When() + " | " + Nm(t.Captive) + " | dotrze za " + F1(nh - now) + " h (godzina gry " + F1(nh) + ", poprzednio " + F1(old) + ")"
                     + " | droga wiesci: " + Route(f, any, alt) + ".");
        }
    }
}
