using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// R3-03c - ZAKLADNICY I WYCHOWANKOWIE (POMYSLY-35 nr 5, Jeff 20.10 "tak", fala 2; audyt 10 Z17, 04 N2, 11-trybut:74). Klucz WardsOfPeace.
    ///  - KIEDY: pokoj dwoch krolestw (prefiks MakePeaceAction.ApplyInternal - jedyna droga pokoju gry, takze Diplomacy AcceptPeace -> ApplyByKingdomDecision).
    ///    Przegrany ustalany PRZED pokojem, bo FactionManager.SetNeutral zeruje statystyki lacznika (StanceLink.ResetStats), a Diplomacy czysci zmeczenie wojna
    ///    w zdarzeniu MakePeace (WarExhaustionBehavior.ClearWarExhaustion). Kolejnosc: (1) Diplomacy WarExhaustionManager.GetWarResult - Loss jednej strony przy
    ///    zwyciestwie drugiej (Tie = remis, bez wychowanka); (2) postep wojny gry DiplomacyModel.GetWarProgressScore obu stron - roznica >= 75 (ten sam prog, ktorego
    ///    gra uzywa przy trybucie i ryzyku pokoju); (3) trybut dzienny pokoju (placi przegrany). Brak - remis.
    ///    Wyjatki: wojna fabularna ROT (ROTStorylineWars.IsWarForced - ten sam warunek, ktorym ROT EnforceWars wznawia wojne) - nic; pokoj, ktory nie przetrwal
    ///    doby (24 h czasu kampanii, jak PeaceHold M6a-3: ROT wznawia wojny tej samej doby) - nic.
    ///  - KTO: rod panujacy przegranego krolestwa, bohater 10-20 lat (wiek gry; BK nie zmienia AgeModel - sprawdzone w dekompilacji, wiec dolna granica 10),
    ///    rodu (Occupation Lord), zywy, nie glowa rodu, nie gracz, nie jeniec, bez partii (ani dowodca, ani czlonek), nie namiestnik, bez malzonka, stan Active albo
    ///    NotSpawned (dzieci gry), nie bedacy juz wychowankiem; rod musi miec jeszcze innego zywego czlonka-rodu poza glowa (nie jedyny dziedzic). Najmlodszy
    ///    z kandydatow (jak Theon). Jeden wychowanek na pokoj; rod, ktory juz ma wychowanka u tego zwyciezcy - nie wysyla drugiego.
    ///  - DOKAD: siedziba rodu panujacego zwyciezcy (HomeSettlement, gdy to miasto/zamek krolestwa zwyciezcy; inaczej miasto, potem zamek rodu; potem miasto
    ///    krolestwa). TeleportHeroAction.ApplyImmediateTeleportToSettlement. Rod, slub, dziedziczenie BK - bez zmian (bohater zostaje w swoim rodzie).
    ///  - POBYT: 2 lata kalendarza gry (CampaignTime.DaysInYear - Armoury Calendar, rok 364 dni). Sluchacze zdarzen gry (bez Harmony): CanHeroLeadParty,
    ///    CanMoveToSettlement (gra nie przenosi bezczynnych doroslych do innej osady - HeroSpawnCampaignBehavior.CanHeroMoveToAnotherSettlement),
    ///    CanBeGovernorOrHavePartyRole - false dla wychowanka. Postfiks HeroSpawnCampaignBehavior.GetBestAvailableCommander: druga petla gry (rody AI) nie pyta
    ///    CanLeadParty - gdy wybrala wychowanka, ten sam wybor bez wychowankow. Codziennie: poza siedziba (bez partii i niewoli) - z powrotem; siedziba w obcych
    ///    rekach - nowa siedziba zwyciezcy.
    ///  - WOJNA: zdarzenie WarDeclared pary (krolestwo domu, krolestwo zwyciezcy): wypowiada dom - wychowanek jencem zwyciezcy (TakePrisonerAction.Apply,
    ///    loch twierdzy, w ktorej jest); wypowiada zwyciezca albo wojna fabularna ROT - wraca wolny. Zmiana wykonana w najblizszej godzinie (nie w srodku zdarzenia).
    ///    Wojna bez zdarzenia (np. po wczytaniu) - wraca wolny.
    ///  - KONIEC: po 2 latach powrot do siedziby wlasnego rodu, relacja wychowanek - glowa rodu gospodarza +10, glowy obu rodow +5. Smierc, slub, zmiana rodu,
    ///    niewola u kogos innego, partia - stan sie czysci (bez ruchu). Wychowanek zostal glowa rodu, krolestwo upadlo albo rod zmienil krolestwo - powrot bez relacji.
    ///    Klucz wylaczony - wszyscy wracaja do domu (bez relacji), nowych nie ma.
    ///  - AI RZADZIEJ ZRYWA POKOJ: postfiks BK BKDiplomacyModel.GetScoreOfDeclaringWar (wersja ExplainedNumber - na niej stoja: model gry przez nadpisanie float
    ///    x10, BKWarBarterable i decyzje wojny BK) - krolestwo, ktorego krew jest wychowankiem u celu, ocenia wojne nizej o 300 na wychowanka (najwyzej 2; jednostka
    ///    BK num3 >= 600 - polowa), z linia "Kin held as ward" w wyjasnieniu BK. Brak BK - tylko zamiana w jenca (linia startu mowi BRAK).
    ///  - GRACZ: tak samo (dziecko gracza tez moze pojechac, wychowanek u gracza stoi w jego siedzibie do rozmowy); krotki komunikat EN przy wyjezdzie, powrocie,
    ///    zamianie w jenca i powrocie wolnym, gdy dotyczy krolestwa albo rodu gracza.
    ///  - Zloto: brak (zadnego przeplywu - nic do ksiegi Obieg).
    /// Watki: sluchacze Can* i postfiks oceny wojny moga biec z watkow AI - czytaja tylko migawki (_set, _pairs) podmieniane w calosci na watku glownym; bez alokacji
    /// (TextObject tylko przy wyjasnieniu BK dla ekranu). Zapis: SaveText "arm_wards" ("w1|pokoje|wychowankowie|liczniki"); brak klucza = brak wychowankow.
    /// Linie: "Wychowankowie (R3-03c): start sesji", zdarzenia "... dzien N | pokoj: / wyjazd: / powrot: / jeniec: / koniec:" i linia doby "... dzien N | klucz".
    /// </summary>
    internal static class PeaceWards
    {
        private const string HarmonyId = "com.jeff.armoury";   // ten sam identyfikator co SubModuleMain.HarmonyId
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double WaitHours = 24.0;
        private const float MinAge = 10f, MaxAge = 20f;
        private const int StayYears = 2;
        private const float WarPenalty = 300f;
        private const int PenaltyMaxWards = 2;
        private const int RelWard = 10, RelLeaders = 5;
        private const float ProgressGap = 75f;
        private const string P = "Wychowankowie (R3-03c): ";

        internal static bool On { get { var s = Settings.Current; return s != null && s.WardsOfPeace; } }

        private sealed class Peace { public string Loser, Winner, Src; public long Hour; }
        private sealed class Ward
        {
            public string HeroId, HomeClan, HomeK, HostClan, HostK, Seat;
            public int Start, End, Act;   // Act: 0 pobyt, 1 jeniec (wojne wypowiedzial dom), 2 wolny (wypowiedzial zwyciezca), 3 wolny (wojna fabularna ROT)
            public Hero H; public Clan HC, XC; public Kingdom HK, XK;
        }
        private static readonly List<Peace> _peaces = new List<Peace>();
        private static readonly List<Ward> _wards = new List<Ward>();
        private static bool _resolved;

        // migawki dla watkow AI (podmieniane w calosci na watku glownym)
        private struct KPair { public Kingdom Home, Host; }
        private static volatile HashSet<Hero> _set = new HashSet<Hero>();
        private static volatile KPair[] _pairs = new KPair[0];

        // liczniki doby
        private static int _dPeace, _dForced, _dTie, _dByDip, _dByProg, _dByTrib, _dLasting, _dShort, _dGoneK;
        private static int _dSent, _dNoCand, _dNoSeat, _dAlready, _dNoClan, _dTpFail;
        private static int _dEnd, _dFreeHost, _dFreeStory, _dFreeOther, _dCaptured, _dCapFail, _dLeader, _dKChange, _dKeyOff;
        private static int _dDead, _dWed, _dPrison, _dParty, _dClanCh, _dBack, _dNewSeat, _dCmdSwap;
        private static int _dPen;   // Interlocked (watki AI)
        // od poczatku kampanii (zapis)
        private static int _tSent, _tRet, _tCap, _tFree, _tDrop;
        private static int _stumbles, _penStumbles, _importN = -1, _importBad;
        private static readonly HashSet<string> _err = new HashSet<string>();

        // latki i odbicia (raz na proces)
        private static bool _hooksTried;
        private static string _wired = "-";
        private static MethodInfo _mScore;            // HeroSpawnCampaignBehavior.GetHeroPartyCommandScore(Hero) (private)
        private static PropertyInfo _pDipInst;        // Diplomacy WarExhaustionManager.Instance
        private static MethodInfo _mDipResult;        // Diplomacy WarExhaustionManager.GetWarResult(Kingdom, Kingdom)
        private static FieldInfo _fRotWars;           // ROT SubModule.StorylineWars
        private static MethodInfo _mRotForced;        // ROT ROTStorylineWars.IsWarForced(IFaction, IFaction)

        internal static void Reset()
        {
            _peaces.Clear(); _wards.Clear(); _resolved = false;
            _set = new HashSet<Hero>(); _pairs = new KPair[0];
            ZeroDay();
            _tSent = _tRet = _tCap = _tFree = _tDrop = 0;
            _stumbles = 0; _penStumbles = 0; _importN = -1; _importBad = 0; _err.Clear();
        }

        private static void ZeroDay()
        {
            _dPeace = _dForced = _dTie = _dByDip = _dByProg = _dByTrib = _dLasting = _dShort = _dGoneK = 0;
            _dSent = _dNoCand = _dNoSeat = _dAlready = _dNoClan = _dTpFail = 0;
            _dEnd = _dFreeHost = _dFreeStory = _dFreeOther = _dCaptured = _dCapFail = _dLeader = _dKChange = _dKeyOff = 0;
            _dDead = _dWed = _dPrison = _dParty = _dClanCh = _dBack = _dNewSeat = _dCmdSwap = 0;
            Interlocked.Exchange(ref _dPen, 0);
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("PeaceWards." + where, e); } catch { }
        }

        private static string Name(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Name(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Name(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "-"; } catch { return "?"; } }
        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }
        private static bool LogOn { get { var s = Settings.Current; return s != null && s.LogEnabled; } }

        private static Kingdom KingdomById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k;
            return null;
        }

        private static T Find<T>(string id) where T : TaleWorlds.ObjectSystem.MBObjectBase
        {
            try { return string.IsNullOrEmpty(id) || Campaign.Current == null ? null : Campaign.Current.CampaignObjectManager.Find<T>(id); }
            catch { return null; }
        }

        // ------------------------------------------------------------ rejestracja zdarzen (ArmouryBehavior.RegisterEvents)
        internal static void RegisterEvents(CampaignBehaviorBase owner)
        {
            CampaignEvents.WarDeclared.AddNonSerializedListener(owner, OnWarDeclared);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(owner, Hourly);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(owner, Daily);
            CampaignEvents.CanHeroLeadPartyEvent.AddNonSerializedListener(owner, OnCanBlock);
            CampaignEvents.CanMoveToSettlementEvent.AddNonSerializedListener(owner, OnCanBlock);
            CampaignEvents.CanBeGovernorOrHavePartyRoleEvent.AddNonSerializedListener(owner, OnCanBlock);
        }

        /// <summary>CanHeroLeadParty / CanMoveToSettlement / CanBeGovernorOrHavePartyRole: wychowanek - false. Tylko odczyt migawki (watki AI), bez alokacji.</summary>
        private static void OnCanBlock(Hero hero, ref bool result)
        {
            if (!result || hero == null) return;
            var s = _set;
            if (s.Count > 0 && s.Contains(hero)) result = false;
        }

        // ------------------------------------------------------------ start sesji: latki (raz na proces), odbicia, linia
        internal static void OnSessionLaunched()
        {
            ZeroDay();
            if (!_hooksTried)
            {
                _hooksTried = true;
                var parts = new List<string>();
                Harmony h = null;
                try { h = new Harmony(HarmonyId); } catch (Exception e) { Stumble("Harmony", e); }
                // pokoj gry
                try
                {
                    var m = AccessTools.DeclaredMethod(typeof(MakePeaceAction), "ApplyInternal",
                        new[] { typeof(IFaction), typeof(IFaction), typeof(int), typeof(int), typeof(MakePeaceAction.MakePeaceDetail) });
                    if (m != null && h != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(PeaceWards), nameof(PeacePrefix))); parts.Add("pokoj (MakePeaceAction.ApplyInternal) wpiety"); }
                    else parts.Add("BRAK MakePeaceAction.ApplyInternal - wychowankow nie bedzie");
                }
                catch (Exception e) { Stumble("PeacePatch", e); parts.Add("pokoj - blad latki"); }
                // dowodca partii rodu AI
                try
                {
                    var m = AccessTools.DeclaredMethod(typeof(HeroSpawnCampaignBehavior), "GetBestAvailableCommander", new[] { typeof(Clan) });
                    _mScore = AccessTools.DeclaredMethod(typeof(HeroSpawnCampaignBehavior), "GetHeroPartyCommandScore", new[] { typeof(Hero) });
                    if (m != null && _mScore != null && h != null) { h.Patch(m, postfix: new HarmonyMethod(typeof(PeaceWards), nameof(CommanderPostfix))); parts.Add("dowodca partii (GetBestAvailableCommander) wpiety"); }
                    else parts.Add("BRAK GetBestAvailableCommander / GetHeroPartyCommandScore - dorosly wychowanek rodu AI moze dostac partie (wtedy stan sie czysci)");
                }
                catch (Exception e) { Stumble("CommanderPatch", e); parts.Add("dowodca partii - blad latki"); }
                // ocena wojny BK
                try
                {
                    var mm = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.DiplomacyModel : null;
                    MethodInfo target = null;
                    Type rt = mm != null ? mm.GetType() : null;
                    for (var t = rt; t != null && target == null; t = t.BaseType) target = WarScoreMethod(t);
                    if (target == null) { var bk = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKDiplomacyModel"); if (bk != null) target = WarScoreMethod(bk); }
                    if (target != null && h != null)
                    {
                        h.Patch(target, postfix: new HarmonyMethod(typeof(PeaceWards), nameof(WarScorePostfix)));
                        parts.Add("ocena wojny BK (" + target.DeclaringType.FullName + ".GetScoreOfDeclaringWar, model gry " + (rt != null ? rt.FullName : "brak") + ") wpieta");
                    }
                    else parts.Add("BRAK oceny wojny BK (model gry " + (rt != null ? rt.FullName : "brak") + ") - AI zrywa pokoj jak dotad, dziala tylko zamiana w jenca");
                }
                catch (Exception e) { Stumble("WarScorePatch", e); parts.Add("ocena wojny - blad latki"); }
                // Diplomacy i ROT (odbicia)
                try
                {
                    var tw = AccessTools.TypeByName("Diplomacy.WarExhaustion.WarExhaustionManager");
                    if (tw != null)
                    {
                        _pDipInst = AccessTools.Property(tw, "Instance");
                        _mDipResult = AccessTools.Method(tw, "GetWarResult", new[] { typeof(Kingdom), typeof(Kingdom) });
                    }
                    parts.Add(_pDipInst != null && _mDipResult != null ? "wynik wojny Diplomacy jest" : "BRAK wyniku wojny Diplomacy (przegrany z postepu wojny gry i trybutu)");
                }
                catch (Exception e) { Stumble("DipReflect", e); }
                try
                {
                    var tWars = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTStorylineWars");
                    var tSub = tWars != null ? tWars.Assembly.GetType("ROT.SubModule") : null;
                    _fRotWars = tSub != null ? AccessTools.Field(tSub, "StorylineWars") : null;
                    _mRotForced = tWars != null ? AccessTools.Method(tWars, "IsWarForced", new[] { typeof(IFaction), typeof(IFaction) }) : null;
                    if (_mRotForced != null && _mRotForced.ReturnType != typeof(bool)) _mRotForced = null;
                    parts.Add(_fRotWars != null && _mRotForced != null ? "wojny fabularne ROT (IsWarForced) jest" : "BRAK ROT IsWarForced (wojny fabularne lapie tylko doba czekania)");
                }
                catch (Exception e) { Stumble("RotReflect", e); }
                _wired = string.Join(", ", parts.ToArray());
            }
            Resolve();
            try
            {
                Log.Info(P + "start sesji | klucz Wards Of Peace " + (On ? "tak" : "nie") + " | latki: " + _wired + " | wychowankow " + _wards.Count + ", pokojow czeka " + _peaces.Count
                         + (_importN >= 0 ? " | wczytano " + _importN + " (bledne " + _importBad + ")" : "") + ".");
            }
            catch { }
        }

        private static MethodInfo WarScoreMethod(Type t)
        {
            foreach (var m in AccessTools.GetDeclaredMethods(t))
            {
                if (m.Name != "GetScoreOfDeclaringWar" || m.ReturnType != typeof(ExplainedNumber) || m.IsAbstract) continue;
                var ps = m.GetParameters();
                if (ps.Length == 6 && ps[5].ParameterType == typeof(bool)) return m;
            }
            return null;
        }

        // ------------------------------------------------------------ odczyty Diplomacy / ROT
        private static int DipResult(Kingdom a, Kingdom b)
        {
            if (_pDipInst == null || _mDipResult == null) return -1;
            object inst = _pDipInst.GetValue(null, null);
            if (inst == null) return -1;
            object r = _mDipResult.Invoke(inst, new object[] { a, b });
            return r != null ? Convert.ToInt32(r, Inv) : -1;   // None 0, Loss 1, Tie 2, Pyrrhic 3, Close 4, Victory 5
        }

        private static bool IsForced(Kingdom a, Kingdom b)
        {
            try
            {
                if (_fRotWars == null || _mRotForced == null || a == null || b == null) return false;
                object wars = _fRotWars.GetValue(null);
                if (wars == null) return false;
                object r = _mRotForced.Invoke(wars, new object[] { a, b });
                return r is bool && (bool)r;
            }
            catch (Exception e) { Stumble("IsForced", e); return false; }
        }

        // ------------------------------------------------------------ pokoj: przegrany przed SetNeutral
        /// <summary>Prefiks MakePeaceAction.ApplyInternal(faction1, faction2, dailyTributeFrom1To2, duration, detail) - nic nie zmienia, tylko zapamietuje
        /// przegranego; rozstrzygniecie po 24 h (Hourly).</summary>
        public static void PeacePrefix(IFaction __0, IFaction __1, int __2)
        {
            if (!On || __0 == null || __1 == null) return;
            try
            {
                if (!__0.IsKingdomFaction || !__1.IsKingdomFaction) return;
                var a = (Kingdom)__0; var b = (Kingdom)__1;
                if (a == b || a.IsEliminated || b.IsEliminated || !a.IsAtWarWith(b)) return;
                if (a.StringId == null || b.StringId == null || a.StringId.IndexOfAny(Bad) >= 0 || b.StringId.IndexOfAny(Bad) >= 0) return;
                _dPeace++;
                if (IsForced(a, b))
                {
                    _dForced++;
                    if (LogOn) Log.Info(P + "dzien " + Today + " | pokoj: " + Name(a) + " - " + Name(b) + " - wojna fabularna ROT, bez wychowanka.");
                    return;
                }
                Kingdom loser = null; string src = null; bool tie = false;
                int ra = -1, rb = -1;
                try { ra = DipResult(a, b); rb = DipResult(b, a); } catch (Exception e) { Stumble("DipResult", e); }
                if (ra == 2 || rb == 2) tie = true;
                else if (ra == 1 && rb >= 3) { loser = a; src = "D"; }
                else if (rb == 1 && ra >= 3) { loser = b; src = "D"; }
                float pa = 0, pb = 0;
                if (loser == null && !tie)
                {
                    try
                    {
                        var dm = Campaign.Current.Models.DiplomacyModel;
                        pa = dm.GetWarProgressScore(a, b).ResultNumber;
                        pb = dm.GetWarProgressScore(b, a).ResultNumber;
                        if (Math.Abs(pa - pb) >= ProgressGap) { loser = pa < pb ? a : b; src = "P"; }
                    }
                    catch (Exception e) { Stumble("WarProgress", e); }
                }
                if (loser == null && !tie && __2 != 0) { loser = __2 > 0 ? a : b; src = "T"; }
                if (loser == null)
                {
                    _dTie++;
                    if (LogOn) Log.Info(P + "dzien " + Today + " | pokoj: " + Name(a) + " - " + Name(b) + " - remis (Diplomacy " + ra + "/" + rb + ", postep wojny "
                                        + pa.ToString("0", Inv) + "/" + pb.ToString("0", Inv) + ", trybut " + __2 + "), bez wychowanka.");
                    return;
                }
                if (src == "D") _dByDip++; else if (src == "P") _dByProg++; else _dByTrib++;
                var winner = loser == a ? b : a;
                _peaces.RemoveAll(x => (x.Loser == a.StringId && x.Winner == b.StringId) || (x.Loser == b.StringId && x.Winner == a.StringId));
                _peaces.Add(new Peace { Loser = loser.StringId, Winner = winner.StringId, Src = src, Hour = (long)Math.Floor(CampaignTime.Now.ToHours) });
                if (LogOn)
                    Log.Info(P + "dzien " + Today + " | pokoj: " + Name(loser) + " przegralo z " + Name(winner) + " (wedlug " + SrcName(src) + "; Diplomacy " + ra + "/" + rb
                             + ", postep wojny " + pa.ToString("0", Inv) + "/" + pb.ToString("0", Inv) + ", trybut " + __2 + ") - wychowanek pojedzie, jesli pokoj przetrwa "
                             + WaitHours.ToString("0", Inv) + " h.");
            }
            catch (Exception e) { Stumble("PeacePrefix", e); }
        }

        private static string SrcName(string s) { return s == "D" ? "wyniku wojny Diplomacy" : s == "P" ? "postepu wojny gry" : s == "T" ? "trybutu" : "?"; }

        // ------------------------------------------------------------ co godzine: pokoje po dobie, zmiany po wojnie
        private static void Hourly()
        {
            if (Campaign.Current == null || (_peaces.Count == 0 && _wards.Count == 0)) return;
            try
            {
                Resolve();
                if (_peaces.Count > 0)
                {
                    long now = (long)Math.Floor(CampaignTime.Now.ToHours);
                    for (int i = 0; i < _peaces.Count; i++)
                    {
                        var p = _peaces[i];
                        if (now - p.Hour < (long)WaitHours) continue;
                        _peaces.RemoveAt(i); i--;
                        try { Settle(p); } catch (Exception e) { Stumble("Settle", e); }
                    }
                }
                bool any = false;
                for (int i = _wards.Count - 1; i >= 0; i--)
                {
                    var w = _wards[i];
                    if (w.Act == 0) continue;
                    any = true;
                    try { DoAct(w); } catch (Exception e) { Stumble("DoAct", e); _wards.Remove(w); _tDrop++; }
                }
                if (any) Rebuild();
            }
            catch (Exception e) { Stumble("Hourly", e); }
        }

        private static void Settle(Peace p)
        {
            var lk = KingdomById(p.Loser); var wk = KingdomById(p.Winner);
            if (lk == null || wk == null || lk.IsEliminated || wk.IsEliminated) { _dGoneK++; return; }
            if (lk.IsAtWarWith(wk))
            {
                _dShort++;
                if (LogOn) Log.Info(P + "dzien " + Today + " | pokoj: " + Name(lk) + " - " + Name(wk) + " znow w wojnie przed uplywem doby - bez wychowanka.");
                return;
            }
            _dLasting++;
            if (!On) return;
            var lc = lk.RulingClan; var wc = wk.RulingClan;
            if (lc == null || wc == null || lc == wc || lc.IsEliminated || wc.IsEliminated || lc.Leader == null) { _dNoClan++; return; }
            foreach (var w in _wards) if (w.HC == lc && w.XK == wk) { _dAlready++; return; }
            var seat = SeatOf(wk);
            if (seat == null) { _dNoSeat++; if (LogOn) Log.Info(P + "dzien " + Today + " | wyjazd: brak siedziby zwyciezcy " + Name(wk) + " - bez wychowanka."); return; }
            var h = PickWard(lc);
            if (h == null) { _dNoCand++; if (LogOn) Log.Info(P + "dzien " + Today + " | wyjazd: rod " + Name(lc) + " (" + Name(lk) + ") nie ma kandydata (10-20 lat, wolny, nie jedyny dziedzic) - bez wychowanka."); return; }
            bool wasNotSpawned = h.HeroState == Hero.CharacterStates.NotSpawned;
            TeleportHeroAction.ApplyImmediateTeleportToSettlement(h, seat);
            if (h.CurrentSettlement != seat) { _dTpFail++; if (LogOn) Log.Info(P + "dzien " + Today + " | wyjazd: " + Name(h) + " - przeniesienie do " + Name(seat) + " nie wyszlo, bez wychowanka."); return; }
            int start = Today;
            int diy = 364; try { diy = Math.Max(1, CampaignTime.DaysInYear); } catch { }
            var ward = new Ward
            {
                HeroId = h.StringId, HomeClan = lc.StringId, HomeK = lk.StringId, HostClan = wc.StringId, HostK = wk.StringId, Seat = seat.StringId,
                Start = start, End = start + StayYears * diy, H = h, HC = lc, XC = wc, HK = lk, XK = wk
            };
            _wards.Add(ward);
            Rebuild();
            _dSent++; _tSent++;
            if (LogOn)
                Log.Info(P + "dzien " + Today + " | wyjazd: " + Name(h) + " (rod " + Name(lc) + ", wiek " + h.Age.ToString("0", Inv) + (wasNotSpawned ? ", dziecko dotad bez miejsca na mapie" : "")
                         + ") z " + Name(lk) + " do " + Name(seat) + " (rod " + Name(wc) + ", " + Name(wk) + "), powrot dnia " + ward.End + " (" + StayYears + " lata po " + diy + " dni); przegrany wedlug " + SrcName(p.Src) + ".");
            if (PlayerInvolved(ward)) Msg(Name(h) + " of " + Name(lc) + " goes to " + Name(seat) + " as a ward.");
        }

        private static bool Fort(Settlement s, Kingdom k) { return s != null && (s.IsTown || s.IsCastle) && (object)s.MapFaction == k; }

        private static Settlement SeatOf(Kingdom k)
        {
            var c = k != null ? k.RulingClan : null;
            if (c == null) return null;
            var hs = c.HomeSettlement;
            if (Fort(hs, k)) return hs;
            foreach (var t in c.Fiefs) if (t != null && t.IsTown && Fort(t.Settlement, k)) return t.Settlement;
            foreach (var t in c.Fiefs) if (t != null && Fort(t.Settlement, k)) return t.Settlement;
            foreach (var t in k.Fiefs) if (t != null && t.IsTown && Fort(t.Settlement, k)) return t.Settlement;
            return null;
        }

        private static bool IsWard(Hero h) { foreach (var w in _wards) if (w.H == h) return true; return false; }

        private static Hero PickWard(Clan c)
        {
            Hero best = null; int heirs = 0;
            foreach (var h in c.Heroes)
            {
                if (h == null || !h.IsAlive || h == c.Leader || h.CharacterObject == null || h.CharacterObject.Occupation != Occupation.Lord) continue;
                heirs++;
                if (h == Hero.MainHero || h.IsPrisoner || h.PartyBelongedTo != null || h.PartyBelongedToAsPrisoner != null || h.GovernorOf != null || h.Spouse != null) continue;
                if (h.HeroState != Hero.CharacterStates.Active && h.HeroState != Hero.CharacterStates.NotSpawned) continue;
                if (h.IsNotable || h.IsWanderer || h.Age < MinAge || h.Age > MaxAge || IsWard(h)) continue;
                if (best == null || h.Age < best.Age || (h.Age == best.Age && string.CompareOrdinal(h.StringId, best.StringId) < 0)) best = h;
            }
            return heirs >= 2 ? best : null;   // nie jedyny dziedzic: poza glowa jeszcze ktos zywy z rodu
        }

        private static Settlement HomeOf(Ward w)
        {
            var h = w.H; var c = w.HC != null ? w.HC : (h != null ? h.Clan : null);
            IFaction mf = h != null ? h.MapFaction : null;
            Func<Settlement, bool> ok = s => s != null && (s.IsTown || s.IsCastle) && (mf == null || s.MapFaction == null || !s.MapFaction.IsAtWarWith(mf));
            if (c != null)
            {
                if (ok(c.HomeSettlement)) return c.HomeSettlement;
                foreach (var t in c.Fiefs) if (t != null && ok(t.Settlement)) return t.Settlement;
            }
            if (mf != null) foreach (var t in mf.Fiefs) if (t != null && ok(t.Settlement)) return t.Settlement;
            return null;
        }

        private static bool PlayerInvolved(Ward w)
        {
            try
            {
                var pc = Clan.PlayerClan; var pf = Hero.MainHero != null ? Hero.MainHero.MapFaction : null;
                return (pc != null && (w.HC == pc || w.XC == pc)) || (pf != null && ((object)w.HK == pf || (object)w.XK == pf));
            }
            catch { return false; }
        }

        private static void Msg(string t)
        {
            try { InformationManager.DisplayMessage(new InformationMessage(t, Color.FromUint(0xFFE8C77Au))); } catch { }
        }

        /// <summary>Powrot do siedziby wlasnego rodu (bez partii i niewoli); rel - relacje konca pobytu; free - wolny po wojnie (licznik i komunikat). Wpis znika.</summary>
        private static void ReturnHome(Ward w, string why, bool rel, bool free = false)
        {
            var h = w.H;
            var seat = Settlement.Find(w.Seat);
            Settlement home = null;
            if (h != null && h.IsAlive && !h.IsPrisoner && h.PartyBelongedTo == null)
            {
                home = HomeOf(w);
                if (home != null && h.CurrentSettlement != home) TeleportHeroAction.ApplyImmediateTeleportToSettlement(h, home);
            }
            if (rel && h != null)
            {
                var hl = w.XC != null ? w.XC.Leader : null; var ml = w.HC != null ? w.HC.Leader : null;
                try { if (hl != null && hl.IsAlive && hl != h) ChangeRelationAction.ApplyRelationChangeBetweenHeroes(h, hl, RelWard, false); } catch (Exception e) { Stumble("RelWard", e); }
                try { if (hl != null && ml != null && hl.IsAlive && ml.IsAlive && hl != ml && ml != h) ChangeRelationAction.ApplyRelationChangeBetweenHeroes(ml, hl, RelLeaders, false); } catch (Exception e) { Stumble("RelLeaders", e); }
            }
            _wards.Remove(w); if (free) _tFree++; else _tRet++;
            if (LogOn)
                Log.Info(P + "dzien " + Today + " | powrot: " + Name(h) + " (rod " + Name(w.HC) + ") z " + Name(seat) + " do " + (home != null ? Name(home) : "- (bez przeniesienia)") + " - " + why
                         + (rel ? "; relacja z glowa rodu " + Name(w.XC) + " +" + RelWard + ", glowy rodow +" + RelLeaders : "") + ".");
            if (PlayerInvolved(w)) Msg(free ? Name(h) + " leaves " + Name(seat) + " and goes home free." : Name(h) + ", a ward at " + Name(seat) + ", returns home.");
        }

        private static void Drop(Ward w, string why)
        {
            _wards.Remove(w); _tDrop++;
            if (LogOn) Log.Info(P + "dzien " + Today + " | koniec: " + Name(w.H) + " (rod " + Name(w.HC) + ", siedziba " + Name(Settlement.Find(w.Seat)) + ") - " + why + ", stan wyczyszczony.");
        }

        // ------------------------------------------------------------ wojna pary: decyzja w zdarzeniu, wykonanie w najblizszej godzinie
        private static void OnWarDeclared(IFaction f1, IFaction f2, DeclareWarAction.DeclareWarDetail detail)
        {
            if (_wards.Count == 0 || f1 == null || f2 == null) return;
            try
            {
                Resolve();
                var a = f1.MapFaction; var b = f2.MapFaction;
                bool changed = false;
                foreach (var w in _wards)
                {
                    if (w.Act != 0 || w.HK == null || w.XK == null) continue;
                    bool homeDecl = (object)a == w.HK && (object)b == w.XK, hostDecl = (object)a == w.XK && (object)b == w.HK;
                    if (!homeDecl && !hostDecl) continue;
                    w.Act = IsForced(w.HK, w.XK) ? 3 : homeDecl ? 1 : 2;
                    changed = true;
                    if (LogOn) Log.Info(P + "dzien " + Today + " | wojna: " + Name(f1) + " wypowiada wojne " + Name(f2) + " (" + detail + ") - wychowanek " + Name(w.H) + ": "
                                        + (w.Act == 1 ? "stanie sie jencem " + Name(w.XK) : w.Act == 3 ? "wojna fabularna ROT - wraca wolny" : "wraca wolny") + ".");
                }
                if (changed) Rebuild();
            }
            catch (Exception e) { Stumble("OnWarDeclared", e); }
        }

        private static void DoAct(Ward w)
        {
            var h = w.H;
            if (h == null || !h.IsAlive) { Drop(w, "nie zyje"); _dDead++; return; }
            if (w.Act == 1)
            {
                var at = h.CurrentSettlement;
                if (!h.IsPrisoner && h.PartyBelongedTo == null && at != null && at.Party != null && w.XK != null && (object)at.MapFaction == w.XK)
                {
                    TakePrisonerAction.Apply(at.Party, h);
                    if (h.IsPrisoner)
                    {
                        _wards.Remove(w); _dCaptured++; _tCap++;
                        if (LogOn) Log.Info(P + "dzien " + Today + " | jeniec: " + Name(h) + " (rod " + Name(w.HC) + ") - " + Name(w.HK) + " zerwalo pokoj, wychowanek w lochu " + Name(at) + " (" + Name(w.XK) + ").");
                        if (PlayerInvolved(w)) Msg(Name(h) + ", a ward at " + Name(at) + ", is now a prisoner.");
                        return;
                    }
                }
                _dCapFail++;
                Drop(w, "zamiana w jenca niemozliwa (poza twierdza zwyciezcy, w partii albo juz w niewoli)");
                return;
            }
            if (w.Act == 3) _dFreeStory++; else _dFreeHost++;
            ReturnHome(w, w.Act == 3 ? "wojna fabularna ROT miedzy domem a zwyciezca, wolny" : Name(w.XK) + " wypowiedzialo wojne, wolny", false, true);
        }

        // ------------------------------------------------------------ doba: stan wychowankow, linia
        private static void Daily()
        {
            if (Campaign.Current == null) return;
            try
            {
                Resolve();
                int today = Today;
                if (!On && (_wards.Count > 0 || _peaces.Count > 0))
                {
                    _peaces.Clear();
                    foreach (var w in _wards.ToArray()) { _dKeyOff++; try { ReturnHome(w, "klucz Wards Of Peace wylaczony", false); } catch (Exception e) { Stumble("KeyOff", e); _wards.Remove(w); } }
                }
                foreach (var w in _wards.ToArray())
                {
                    if (w.Act != 0) continue;
                    try { Check(w, today); } catch (Exception e) { Stumble("Check", e); }
                }
                Rebuild();
                Report(today);
            }
            catch (Exception e) { Stumble("Daily", e); }
            ZeroDay();
        }

        private static void Check(Ward w, int today)
        {
            var h = w.H;
            if (h == null) { Drop(w, "bohater nie znaleziony"); return; }
            if (!h.IsAlive) { _dDead++; Drop(w, "nie zyje"); return; }
            if (w.HC == null || h.Clan != w.HC) { _dClanCh++; Drop(w, "zmiana rodu (" + Name(h.Clan) + ")"); return; }
            if (h.Spouse != null) { _dWed++; Drop(w, "slub z " + Name(h.Spouse)); return; }
            if (h.IsPrisoner) { _dPrison++; Drop(w, "w niewoli"); return; }
            if (h.PartyBelongedTo != null) { _dParty++; Drop(w, "w druzynie " + (h.PartyBelongedTo.Name != null ? h.PartyBelongedTo.Name.ToString() : "?")); return; }
            if (w.HC.Leader == h) { _dLeader++; ReturnHome(w, "zostal glowa rodu", false); return; }
            if (w.HK == null || w.XK == null || w.HK.IsEliminated || w.XK.IsEliminated || w.HC.Kingdom != w.HK || w.HC.IsEliminated)
            { _dKChange++; ReturnHome(w, "upadek krolestwa albo rod poza krolestwem z chwili pokoju", false); return; }
            if (w.HK.IsAtWarWith(w.XK)) { _dFreeOther++; ReturnHome(w, "wojna domu ze zwyciezca bez zdarzenia - wolny", false, true); return; }
            if (today >= w.End) { _dEnd++; ReturnHome(w, "koniec pobytu (" + StayYears + " lata)", true); return; }
            var seat = Settlement.Find(w.Seat);
            if (!Fort(seat, w.XK))
            {
                var ns = SeatOf(w.XK);
                if (ns == null) { _dKChange++; ReturnHome(w, "zwyciezca bez siedziby", false); return; }
                w.Seat = ns.StringId; seat = ns; _dNewSeat++;
                if (LogOn) Log.Info(P + "dzien " + today + " | siedziba: " + Name(h) + " przeniesiony do nowej siedziby " + Name(ns) + " (" + Name(w.XK) + ").");
            }
            if (h.CurrentSettlement != seat)
            {
                TeleportHeroAction.ApplyImmediateTeleportToSettlement(h, seat);
                _dBack++;
            }
        }

        // ------------------------------------------------------------ AI: dowodca partii bez wychowanka
        /// <summary>Postfiks HeroSpawnCampaignBehavior.GetBestAvailableCommander(Clan): gdy gra wybrala wychowanka (druga petla rodow AI nie pyta CanLeadParty) -
        /// ten sam wybor gry bez wychowankow; blad - null (tej doby bez nowej partii).</summary>
        public static void CommanderPostfix(object __instance, Clan __0, ref Hero __result)
        {
            var set = _set;
            if (__result == null || set.Count == 0 || !set.Contains(__result) || __0 == null) return;
            try
            {
                int age = Campaign.Current.Models.AgeModel.HeroComesOfAge;
                Hero best = null; float top = 0f;
                for (int pass = 0; pass < 2 && best == null; pass++)
                {
                    if (pass == 1 && __0 == Clan.PlayerClan) break;
                    foreach (var h in __0.Heroes)
                    {
                        if (h == null || set.Contains(h)) continue;
                        if (!(h.IsActive && h.IsAlive && h.PartyBelongedTo == null && h.PartyBelongedToAsPrisoner == null && h.Age > age && h.CharacterObject != null && h.CharacterObject.Occupation == Occupation.Lord)) continue;
                        if (pass == 0 && !h.CanLeadParty()) continue;
                        float sc = (float)_mScore.Invoke(__instance, new object[] { h });
                        if (sc > top) { top = sc; best = h; }
                    }
                }
                __result = best;
                _dCmdSwap++;
            }
            catch (Exception e) { __result = null; Stumble("CommanderPostfix", e); }
        }

        // ------------------------------------------------------------ AI: ocena wojny BK (watki AI - tylko migawka)
        /// <summary>Postfiks BKDiplomacyModel.GetScoreOfDeclaringWar(declares, declared, evaluating, out reason, casusBelli, explanations) - krew krolestwa
        /// wypowiadajacego u celu jako wychowanek: -300 na wychowanka (najwyzej 2).</summary>
        public static void WarScorePostfix(IFaction __0, IFaction __1, bool __5, ref ExplainedNumber __result)
        {
            var pairs = _pairs;
            if (pairs.Length == 0 || __0 == null || __1 == null) return;
            try
            {
                int n = 0;
                for (int i = 0; i < pairs.Length; i++) if ((object)pairs[i].Home == __0 && (object)pairs[i].Host == __1) n++;
                if (n == 0) return;
                if (n > PenaltyMaxWards) n = PenaltyMaxWards;
                __result.Add(-WarPenalty * n, __5 ? new TextObject("{=!}Kin held as ward by " + Name(__1)) : null);
                Interlocked.Increment(ref _dPen);
            }
            catch { Interlocked.Increment(ref _penStumbles); }
        }

        // ------------------------------------------------------------ migawki
        private static void Rebuild()
        {
            var set = new HashSet<Hero>();
            var pairs = new List<KPair>();
            foreach (var w in _wards)
            {
                if (w.H == null) continue;
                set.Add(w.H);
                if (w.Act == 0 && w.HK != null && w.XK != null) pairs.Add(new KPair { Home = w.HK, Host = w.XK });
            }
            _set = set; _pairs = pairs.ToArray();
        }

        private static void Resolve()
        {
            if (_resolved || Campaign.Current == null) return;
            _resolved = true;
            foreach (var w in _wards)
            {
                try
                {
                    w.H = Find<Hero>(w.HeroId); w.HC = Find<Clan>(w.HomeClan); w.XC = Find<Clan>(w.HostClan);
                    w.HK = KingdomById(w.HomeK); w.XK = KingdomById(w.HostK);
                }
                catch (Exception e) { Stumble("Resolve", e); }
            }
            Rebuild();
        }

        // ------------------------------------------------------------ linia doby
        private static void Report(int today)
        {
            if (!LogOn) return;
            int atPlayer = 0, fromPlayer = 0;
            var pc = Clan.PlayerClan;
            var list = new List<string>();
            foreach (var w in _wards)
            {
                if (pc != null && w.XC == pc) atPlayer++;
                if (pc != null && w.HC == pc) fromPlayer++;
                if (list.Count < 12) list.Add(Name(w.H) + " (" + Name(w.HC) + " -> " + Name(Settlement.Find(w.Seat)) + ", do dnia " + w.End + ")");
            }
            var sb = new StringBuilder(900);
            sb.Append(P).Append("dzien ").Append(today).Append(" | klucz Wards Of Peace ").Append(On ? "tak" : "nie")
              .Append(" | aktywni ").Append(_wards.Count).Append(" (u gracza ").Append(atPlayer).Append(", z rodu gracza ").Append(fromPlayer).Append(')')
              .Append(" | dzis pokoje krolestw ").Append(_dPeace).Append(": wojna fabularna ROT ").Append(_dForced).Append(", remis ").Append(_dTie)
              .Append(", przegrany wedlug Diplomacy ").Append(_dByDip).Append(" / postepu wojny ").Append(_dByProg).Append(" / trybutu ").Append(_dByTrib)
              .Append(" | po dobie: pokoj trwa ").Append(_dLasting).Append(", znow wojna ").Append(_dShort).Append(", krolestwo upadlo ").Append(_dGoneK)
              .Append(" | wyjazdy ").Append(_dSent).Append(" (bez kandydata ").Append(_dNoCand).Append(", bez siedziby ").Append(_dNoSeat).Append(", rod juz ma wychowanka u zwyciezcy ")
              .Append(_dAlready).Append(", bez rodu panujacego ").Append(_dNoClan).Append(", przeniesienie nieudane ").Append(_dTpFail).Append(')')
              .Append(" | powroty: koniec pobytu ").Append(_dEnd).Append(", wolni po wojnie zwyciezcy ").Append(_dFreeHost).Append(", wolni po wojnie fabularnej ").Append(_dFreeStory)
              .Append(", wolni po wojnie bez zdarzenia ").Append(_dFreeOther).Append(", glowa rodu ").Append(_dLeader).Append(", zmiana krolestwa / siedziby ").Append(_dKChange)
              .Append(", klucz wylaczony ").Append(_dKeyOff)
              .Append(" | zamiany w jencow ").Append(_dCaptured).Append(" (niemozliwe ").Append(_dCapFail).Append(')')
              .Append(" | wyczyszczone: smierc ").Append(_dDead).Append(", slub ").Append(_dWed).Append(", niewola ").Append(_dPrison).Append(", druzyna ").Append(_dParty)
              .Append(", zmiana rodu ").Append(_dClanCh)
              .Append(" | przywroceni do siedziby ").Append(_dBack).Append(", nowa siedziba ").Append(_dNewSeat)
              .Append(" | AI: oceny wojny z kara ").Append(Volatile.Read(ref _dPen)).Append(", podmiany dowodcy partii ").Append(_dCmdSwap)
              .Append(" | od poczatku: wyjazdy ").Append(_tSent).Append(", powroty ").Append(_tRet).Append(", jency ").Append(_tCap).Append(", wolni po wojnie ").Append(_tFree)
              .Append(", wyczyszczone ").Append(_tDrop)
              .Append(" | pokojow czeka ").Append(_peaces.Count)
              .Append(_stumbles > 0 || _penStumbles > 0 ? " | potkniecia " + _stumbles + " (ocena wojny " + _penStumbles + ")" : "")
              .Append(_importN >= 0 ? " | wczytano " + _importN + " (bledne " + _importBad + ")" : "")
              .Append(" | lista: ").Append(list.Count > 0 ? string.Join(" ; ", list.ToArray()) : "-").Append('.');
            Log.Info(sb.ToString());
            _importN = -1;
        }

        // ------------------------------------------------------------ zapis "w1|pokoje|wychowankowie|liczniki"
        private static readonly char[] Bad = { '|', ';', ':', ',' };
        private static bool Ok(string s) { return !string.IsNullOrEmpty(s) && s.IndexOfAny(Bad) < 0; }

        internal static string Export()
        {
            try
            {
                if (_peaces.Count == 0 && _wards.Count == 0 && _tSent == 0 && _tRet == 0 && _tCap == 0 && _tFree == 0 && _tDrop == 0) return null;
                var sb = new StringBuilder(64 + _wards.Count * 120 + _peaces.Count * 40);
                sb.Append("w1|");
                bool first = true;
                foreach (var p in _peaces)
                {
                    if (!Ok(p.Loser) || !Ok(p.Winner)) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(p.Loser).Append(':').Append(p.Winner).Append(':').Append(p.Hour.ToString(Inv)).Append(':').Append(Ok(p.Src) ? p.Src : "?");
                }
                sb.Append('|');
                first = true;
                foreach (var w in _wards)
                {
                    if (!Ok(w.HeroId) || !Ok(w.HomeClan) || !Ok(w.HomeK) || !Ok(w.HostClan) || !Ok(w.HostK) || !Ok(w.Seat)) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(w.HeroId).Append(':').Append(w.HomeClan).Append(':').Append(w.HomeK).Append(':').Append(w.HostClan).Append(':').Append(w.HostK).Append(':').Append(w.Seat)
                      .Append(':').Append(w.Start.ToString(Inv)).Append(':').Append(w.End.ToString(Inv)).Append(':').Append(w.Act.ToString(Inv));
                }
                sb.Append('|').Append(_tSent.ToString(Inv)).Append(',').Append(_tRet.ToString(Inv)).Append(',').Append(_tCap.ToString(Inv)).Append(',').Append(_tFree.ToString(Inv)).Append(',').Append(_tDrop.ToString(Inv));
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return null; }
        }

        internal static void Import(string data)
        {
            _peaces.Clear(); _wards.Clear(); _resolved = false; _importN = 0; _importBad = 0;
            _tSent = _tRet = _tCap = _tFree = _tDrop = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var seg = data.Split('|');
                if (seg.Length < 4 || seg[0] != "w1") { _importBad++; return; }
                foreach (var e in seg[1].Split(';'))
                {
                    if (e.Length == 0) continue;
                    var x = e.Split(':'); long hr;
                    if (x.Length == 4 && Ok(x[0]) && Ok(x[1]) && long.TryParse(x[2], NumberStyles.Integer, Inv, out hr)) { _peaces.Add(new Peace { Loser = x[0], Winner = x[1], Hour = hr, Src = x[3] }); _importN++; }
                    else _importBad++;
                }
                foreach (var e in seg[2].Split(';'))
                {
                    if (e.Length == 0) continue;
                    var x = e.Split(':'); int st, en, act;
                    if (x.Length == 9 && Ok(x[0]) && Ok(x[1]) && Ok(x[2]) && Ok(x[3]) && Ok(x[4]) && Ok(x[5])
                        && int.TryParse(x[6], NumberStyles.Integer, Inv, out st) && int.TryParse(x[7], NumberStyles.Integer, Inv, out en) && int.TryParse(x[8], NumberStyles.Integer, Inv, out act))
                    {
                        _wards.Add(new Ward { HeroId = x[0], HomeClan = x[1], HomeK = x[2], HostClan = x[3], HostK = x[4], Seat = x[5], Start = st, End = en, Act = act < 0 || act > 3 ? 0 : act });
                        _importN++;
                    }
                    else _importBad++;
                }
                var t = seg[3].Split(',');
                if (t.Length == 5)
                {
                    int.TryParse(t[0], NumberStyles.Integer, Inv, out _tSent); int.TryParse(t[1], NumberStyles.Integer, Inv, out _tRet);
                    int.TryParse(t[2], NumberStyles.Integer, Inv, out _tCap); int.TryParse(t[3], NumberStyles.Integer, Inv, out _tFree);
                    int.TryParse(t[4], NumberStyles.Integer, Inv, out _tDrop);
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
