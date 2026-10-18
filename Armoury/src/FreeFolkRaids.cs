using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// r4-mur (decyzja Jeffa 17.10, STAN-PRAC ### 72: "glod pcha Wolnych Ludzi do najazdow na poludnie") - GLOD WOLNYCH LUDZI -> NAJAZDY:
    ///  - INDEKS GLODU okregu Wolnych Ludzi (krolestwo "freefolk"; tylko wsie z KS zastosowanym): H = (1 - pokrycie z pola) x waga zapasu; pokrycie = plon doby / ludzie
    ///    w domu (migawka Famine), waga zapasu = 1 przy spichlerzu <= 180 dni, maleje liniowo do 0.5 przy pelnym spichlerzu (sufit Famine). Swiat Wolnych Ludzi -
    ///    srednia wazona ludzmi.
    ///  - NOWA BANDA: najwyzej jedna na dobe, z szansa H x FreeFolkRaidDailyChance, dopoki w polu jest mniej niz FreeFolkRaidMaxParties band (twardy sufit - CLAUDE.md:
    ///    setki obiektow = CTD); okreg zrodla losowany wagi H x ludzie, wies zrodla - najwiekszy brak (ludzie x (1 - pokrycie)); ludzi FreeFolkRaidMinMen + H x (Max - Min),
    ///    najwyzej FreeFolkRaidMaxMen. CEL: wies Strazy albo Polnocy z droga ladowa od wsi zrodla (pamiec drog gry < 50000 - prog BK), losowo z 8 najblizszych
    ///    (waga 1 / droga) - przez Mur na poludnie. Brak drogi - banda nie rusza (licznik), nikogo nie zabrano.
    ///  - LUDZIE (nie z niczego): OutlawLaw.RaidDraw - najpierw prosci z puli wyrzutkow okregu zrodla (wrocili z poprzednich najazdow albo poszli w las z biedy),
    ///    brak - z domow wsi okregu (PeopleBook.Move DOM -> WYRZUTKI, przyczyna "bieda i wojna"); narodziny bandy jak RosterPostfix (pula -> banda, licznik "narodziny
    ///    band"), banda = najnizszy lekki bandyta klanu "wildlings" (Wildling Renegades), udzialy pochodzenia = okreg zrodla. Banda bez zlota z niczego (kiesa 0),
    ///    bez jedzenia z niczego (bandy nie jedza). Bandy najazdu nie werbuja (OutlawLaw.Daily pomija je) - rozmiar nie rosnie ponad sufit; nadmiar (np. scalenie
    ///    HideoutPurge) zdejmowany co dobe do puli okregu zrodla.
    ///  - WYMARSZ (recenzja r4-mur: klan bandycki jest w stalej wojnie z kazdym krolestwem, DefaultDiplomacyModel.GetShallowDiplomaticStance - takze z Wolnymi Ludzmi):
    ///    banda idzie do bramy wsi celu rozkazem "idz do punktu" BEZ nowych decyzji AI (MobilePartyAi.GetBehaviors pomija wtedy inicjatywe - nie napada po drodze
    ///    taborow i karawan wlasnych wsi Wolnych Ludzi). U celu (<= MarchArrive jednostek od bramy albo po ReturnMaxDays dobach marszu) - patrol; doby najazdu licza
    ///    sie od dojscia (FreeFolkRaidDays: "ile dob banda rabuje na poludniu"). Banda rozbita w marszu (ponad polowa ludzi) od razu wraca.
    ///  - NAJAZD: banda bandycka (BanditPartyComponent.CreateLooterParty) z domem = wies celu - AI band patroluje wokol niej i napada tabory wsi, karawany i slabsze
    ///    partie (jak looterzy gry); lup sprzedaje paserowi (OutlawLaw) jak kazda banda. Po FreeFolkRaidDays dobach (albo gdy stracila ponad polowe ludzi) wraca:
    ///    rozkaz "idz do punktu" przy bramie wsi zrodla, bez nowych decyzji AI. W domu (najwyzej 30 dob drogi): bohaterowie-jency uciekaja, zloto bandy do kiesy wsi
    ///    zrodla, towar do jej magazynu, ludzie i jency do puli wyrzutkow okregu zrodla ("rozwiazane bandy" - stamtad wracaja do domow tempem puli albo ida w nastepny
    ///    najazd), banda rozwiazana (DestroyPartyAction bez sprawcy). Polegli i pojmani w drodze - straty (jak kazdej bandy).
    ///  - ZAPIS: arm_mur_najazdy (bandy najazdu, liczniki od poczatku); stary zapis bez klucza = brak band. Starszy DLL widzi zwykle bandy Wildling Renegades.
    ///    Flaga "bez nowych decyzji" (MobilePartyAi.DoNotMakeNewDecisions to [SaveableProperty(11)] - idzie do zapisu gry) NIE trafia do zapisu: OnBeforeSave ja zdejmuje,
    ///    OnSaveOver przywraca, po wczytaniu wraca w pierwszej godzinie (Hourly) - recenzja r4-mur: banda bez wpisu w arm_mur_najazdy (starszy DLL po cofnieciu,
    ///    straznik rozmiaru, blad odczytu) nie stoi zamrozona na zawsze u bram, tylko zostaje zwykla banda.
    ///  - SWOI NIE BIJA SWOICH (fix-mur, klucz FreeFolkRaidKinPeace; fin83 NMUR-32 "doszly 0 z 13": bitwy.log - 10 band rozbili lordowie Wolnych Ludzi na ich wlasnej
    ///    ziemi (Varamyr Sixskins, Tormund, Torvi, Frenya, Ookpik, Great Walrus), 3 - Straz u Muru (Rast, Karl Tanner); droga ladowa byla - "bez drogi 0", pamiec drog gry
    ///    = SandBoxNavigationCache.GetPathDistanceBetweenAIFaces po siatce ladu): banda najazdu nalezy do klanu bandyckiego, ktory gra trzyma w wojnie z kazdym
    ///    krolestwem, wiec AI lordow Wolnych Ludzi (MobilePartyAi -> GetBestInitiativeBehavior -> ShouldConsiderAttacking) rzucala sie na nia jak na obcych zbojow.
    ///    Postfiks DefaultMobilePartyAIModel.ShouldConsiderAttacking: partia frakcji Wolnych Ludzi nie atakuje bandy najazdu, a banda najazdu - partii Wolnych Ludzi
    ///    (NavalDLC wola ten model jako BaseModel). Straz i Polnoc dalej ich lapia (zatrzymane u Muru - linia "cel:"). Wylaczony klucz albo brak band - wynik gry bez zmian.
    /// KONTROLA (linia "Najazdy Wolnych Ludzi (r4-mur)"): od poczatku wyruszylo + przybyli = wrocili + straty + przyciete + puszczone bez zarzadu + w polu;
    /// wyruszylo = z puli + z domow. Cel (fix-mur, od wczytania wersji fix-mur): doszly + zatrzymane w marszu (zginely: Straz / Polnoc / Wolni Ludzie / inni;
    /// zawrocily rozbite) + marsz ponad 30 dob + w marszu teraz <= wyruszylo (reszta: puszczone bez zarzadu w marszu i bandy wyslane przed fix-mur); "zginely po
    /// dojsciu lub w powrocie" osobno (banda zawrocona rozbita, ktora potem zginie, jest w obu).
    /// Wylaczone (klucz albo KS) - nowych band nie ma, bandy w polu wracaja do domu; bez band w polu nic nie biegnie.
    /// </summary>
    internal static class FreeFolkRaids
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string ClanId = "wildlings";        // ROT spclans.xml: "Wildling Renegades" (is_bandit, kultura wildlings, kryjowki wight_hideout_*)
        private const float ArriveDist = 4f;              // jednostki mapy od bramy wsi zrodla = w domu
        private const int ReturnMaxDays = 30;             // najdluzsza droga powrotu - potem banda rozwiazana tam, gdzie stoi (ludzie i tak do puli okregu zrodla); tez najdluzszy marsz do celu
        private const float MarchArrive = 6f;             // jednostki mapy od bramy wsi celu = banda doszla (wymarsz -> patrol)
        private const double StoreFloor = 0.5;            // waga zapasu przy pelnym spichlerzu
        private const double LowDays = 180.0;             // dni spichlerza, ponizej ktorych waga zapasu = 1
        private const int TargetNearest = 8;              // cel losowany z tylu najblizszych (droga) wsi Strazy / Polnocy
        private const int TargetPrefilter = 24;           // tyle najblizszych w linii prostej sprawdzamy w pamieci drog
        private const int MaxImportChars = 200000;
        private const int MaxEntries = 64;

        // Phase: 0 = najazd (patrol wokol wsi celu, AI gry), 1 = powrot (idz do bramy wsi zrodla, bez nowych decyzji), 2 = wymarsz (idz do bramy wsi celu, bez nowych decyzji).
        // Day: doba wymarszu (faza 2), od dojscia do celu - doba poczatku najazdu (faza 0). P - partia (bez zapisu; po wczytaniu z MobileParty.AllBanditParties).
        private sealed class Raid { public string Id, Src, Region, Target; public int Day, Men0, Phase, BackDay, Last; public MobileParty P; public string Killer; }   // Killer - strona, ktora rozbila bande (zdarzenie zniszczenia; bez zapisu)
        private static readonly List<Raid> _raids = new List<Raid>();
        private static readonly HashSet<MobileParty> _owned = new HashSet<MobileParty>();
        // od poczatku kampanii (w zapisie)
        private static long _cStarted, _cReturned, _cLost, _cGained, _cTrim, _cLeft; private static int _nStarted, _nReturned, _nLost;   // _cLeft - bandy puszczone bez zarzadu (zwykle bandy w polu)
        private static double _cFromHome, _cFromPool, _cHomeLeft;     // z domow (zabrani dla najazdu), z puli prostych; z domow, ale zostali w puli (za malo do bandy)
        private static long _cGold, _cItems;
        private static int _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static bool _offLogged;
        private static Clan _clan; private static bool _clanTried;
        private static bool _refreeze;                    // po wczytaniu: flaga "bez nowych decyzji" band w marszu / powrocie do przywrocenia w pierwszej godzinie (Hourly)
        private static int _thawed;                       // ile band zdjal OnBeforeSave (OnSaveOver przywraca)
        // fix-mur: swoi nie bija swoich - migawka band najazdu dla latki AI (czytana z watkow AI; nigdy nie zmieniana po publikacji - podmieniana w calosci)
        private static volatile HashSet<MobileParty> _kin = new HashSet<MobileParty>();
        private static volatile bool _kinOn;              // klucz FreeFolkRaidKinPeace i co najmniej jedna banda w migawce
        private static bool _kinDirty;                    // po wczytaniu - migawka w pierwszej godzinie (Hourly)
        private static int _kinVeto, _kinVetoAll;         // odmowy ataku: od ostatniej doby (Interlocked - watki AI) / od wczytania
        private static string _kinHook = "nie wpieta";
        // fix-mur: wynik marszu od poczatku (osobny klucz zapisu arm_mur_cel): doszly do celu, marsz ponad ReturnMaxDays, zginely w marszu z rak Strazy / Polnocy /
        // Wolnych Ludzi / innych (bez bitwy, inne frakcje), zawrocily rozbite w marszu, zginely po dojsciu albo w powrocie
        private static int _nReached, _nMarchCap, _nKillW, _nKillN, _nKillF, _nKillO, _nTurnMarch, _nDiedAfter;

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.FreeFolkHungerRaids; } }

        /// <summary>OutlawLaw.Daily (werbunek band): banda najazdu nie werbuje (twardy sufit ludzi). Pusta lista - zawsze false (stara sciezka).</summary>
        internal static bool Owns(MobileParty p) { return p != null && _owned.Count > 0 && _owned.Contains(p); }

        internal static void Reset()
        {
            _raids.Clear(); _owned.Clear();
            _cStarted = _cReturned = _cLost = _cGained = _cTrim = _cLeft = 0; _nStarted = _nReturned = _nLost = 0; _cFromHome = _cFromPool = _cHomeLeft = 0.0; _cGold = _cItems = 0;
            _stumbles = 0; _errSites.Clear(); _offLogged = false; _clan = null; _clanTried = false; _refreeze = false; _thawed = 0;
            _kin = new HashSet<MobileParty>(); _kinOn = false; _kinDirty = false; _kinVeto = 0; _kinVetoAll = 0;
            _nReached = _nMarchCap = _nKillW = _nKillN = _nKillF = _nKillO = _nTurnMarch = _nDiedAfter = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errSites.Add(where)) { try { Log.Error("FreeFolkRaids." + where, e); } catch { } }
        }

        private static bool BadId(string id) { return string.IsNullOrEmpty(id) || id.IndexOf('|') >= 0 || id.IndexOf(',') >= 0 || id.IndexOf(';') >= 0; }
        private static double Fin(double x) { return double.IsNaN(x) || double.IsInfinity(x) || x < 0.0 ? 0.0 : x; }

        // ------------------------------------------------------------ zapis (arm_mur_najazdy)
        // "1|wyruszylo;wrocili;straty;przybyli;przyciete;band wyruszylo;band wrocilo;band zginelo;z domow;z puli;z domow w puli;zloto;towar;puszczone|id,wies,okreg,cel,doba,ludzi,faza,doba powrotu,ostatnio;..."
        internal static string Export()
        {
            try
            {
                if (_raids.Count == 0 && _nStarted == 0) return "";
                var sb = new StringBuilder(128 + _raids.Count * 80);
                sb.Append("1|").Append(_cStarted.ToString(Inv)).Append(';').Append(_cReturned.ToString(Inv)).Append(';').Append(_cLost.ToString(Inv)).Append(';')
                  .Append(_cGained.ToString(Inv)).Append(';').Append(_cTrim.ToString(Inv)).Append(';').Append(_nStarted.ToString(Inv)).Append(';')
                  .Append(_nReturned.ToString(Inv)).Append(';').Append(_nLost.ToString(Inv)).Append(';').Append(Fin(_cFromHome).ToString("R", Inv)).Append(';')
                  .Append(Fin(_cFromPool).ToString("R", Inv)).Append(';').Append(Fin(_cHomeLeft).ToString("R", Inv)).Append(';').Append(_cGold.ToString(Inv)).Append(';')
                  .Append(_cItems.ToString(Inv)).Append(';').Append(_cLeft.ToString(Inv)).Append('|');
                bool first = true; int n = 0;
                foreach (var r in _raids)
                {
                    if (BadId(r.Id) || BadId(r.Src) || BadId(r.Region) || BadId(r.Target)) continue;
                    if (++n > MaxEntries) break;
                    if (!first) sb.Append(';');
                    sb.Append(r.Id).Append(',').Append(r.Src).Append(',').Append(r.Region).Append(',').Append(r.Target).Append(',').Append(r.Day.ToString(Inv)).Append(',')
                      .Append(r.Men0.ToString(Inv)).Append(',').Append(r.Phase.ToString(Inv)).Append(',').Append(r.BackDay.ToString(Inv)).Append(',').Append(r.Last.ToString(Inv));
                    first = false;
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string s)
        {
            _raids.Clear(); _owned.Clear();
            _cStarted = _cReturned = _cLost = _cGained = _cTrim = _cLeft = 0; _nStarted = _nReturned = _nLost = 0; _cFromHome = _cFromPool = _cHomeLeft = 0.0; _cGold = _cItems = 0;
            _nReached = _nMarchCap = _nKillW = _nKillN = _nKillF = _nKillO = _nTurnMarch = _nDiedAfter = 0;
            _kin = new HashSet<MobileParty>(); _kinOn = false; _kinDirty = false;
            if (string.IsNullOrEmpty(s)) return;
            if (s.Length > MaxImportChars) { try { Log.Info("Najazdy Wolnych Ludzi (r4-mur): zapis ponad " + MaxImportChars + " znakow - pominiety (bandy zostaja zwyklymi bandami)."); } catch { } return; }
            try
            {
                var p = s.Split('|');
                if (p.Length < 2 || p[0] != "1") return;
                var c = p[1].Split(';');
                _cStarted = L(c, 0); _cReturned = L(c, 1); _cLost = L(c, 2); _cGained = L(c, 3); _cTrim = L(c, 4);
                _nStarted = (int)L(c, 5); _nReturned = (int)L(c, 6); _nLost = (int)L(c, 7);
                _cFromHome = D(c, 8); _cFromPool = D(c, 9); _cHomeLeft = D(c, 10); _cGold = L(c, 11); _cItems = L(c, 12); _cLeft = L(c, 13);
                if (p.Length > 2 && p[2].Length > 0)
                    foreach (var item in p[2].Split(';'))
                    {
                        try
                        {
                            var a = item.Split(',');
                            if (a.Length < 9 || BadId(a[0]) || BadId(a[1]) || BadId(a[2]) || BadId(a[3])) continue;
                            if (_raids.Count >= MaxEntries) break;
                            _raids.Add(new Raid { Id = a[0], Src = a[1], Region = a[2], Target = a[3], Day = I(a, 4), Men0 = I(a, 5), Phase = I(a, 6), BackDay = I(a, 7), Last = I(a, 8) });
                        }
                        catch (Exception e) { Stumble("Import(banda)", e); }
                    }
            }
            catch (Exception e) { Stumble("Import", e); _raids.Clear(); }
            _refreeze = _raids.Any(r => r.Phase != 0);       // zapis nie niesie flagi "bez nowych decyzji" (BeforeSave) - wraca w pierwszej godzinie (Hourly)
            _kinDirty = _raids.Count > 0;                      // fix-mur: migawka band dla latki AI w pierwszej godzinie (partie znane dopiero po wczytaniu)
        }

        // ------------------------------------------------------------ zapis wyniku marszu (fix-mur, arm_mur_cel - wola ArmouryBehavior.SyncData PO arm_mur_najazdy)
        // "1|doszly;marsz ponad;zginely w marszu: Straz;Polnoc;Wolni Ludzie;inni;zawrocily rozbite;zginely po dojsciu" (pusty - same zera; brak klucza - zera)
        internal static string ExportGoal()
        {
            try
            {
                if (_nReached + _nMarchCap + _nKillW + _nKillN + _nKillF + _nKillO + _nTurnMarch + _nDiedAfter <= 0) return "";
                return "1|" + string.Join(";", new[] { _nReached, _nMarchCap, _nKillW, _nKillN, _nKillF, _nKillO, _nTurnMarch, _nDiedAfter }.Select(x => x.ToString(Inv)));
            }
            catch (Exception e) { Stumble("ExportGoal", e); return ""; }
        }

        internal static void ImportGoal(string s)
        {
            _nReached = _nMarchCap = _nKillW = _nKillN = _nKillF = _nKillO = _nTurnMarch = _nDiedAfter = 0;
            if (string.IsNullOrEmpty(s) || s.Length > 1000) return;                         // straznik rozmiaru (8 liczb)
            try
            {
                var p = s.Split('|');
                if (p.Length < 2 || p[0] != "1") return;                                    // inny format - od zera
                var c = p[1].Split(';');
                _nReached = (int)Math.Min(int.MaxValue, L(c, 0)); _nMarchCap = (int)Math.Min(int.MaxValue, L(c, 1)); _nKillW = (int)Math.Min(int.MaxValue, L(c, 2));
                _nKillN = (int)Math.Min(int.MaxValue, L(c, 3)); _nKillF = (int)Math.Min(int.MaxValue, L(c, 4)); _nKillO = (int)Math.Min(int.MaxValue, L(c, 5));
                _nTurnMarch = (int)Math.Min(int.MaxValue, L(c, 6)); _nDiedAfter = (int)Math.Min(int.MaxValue, L(c, 7));
            }
            catch (Exception e) { Stumble("ImportGoal", e); }
        }

        private static long L(string[] a, int i) { long x; return a != null && i < a.Length && long.TryParse(a[i], NumberStyles.Integer, Inv, out x) && x > 0 ? x : 0; }
        private static int I(string[] a, int i) { int x; return a != null && i < a.Length && int.TryParse(a[i], NumberStyles.Integer, Inv, out x) ? x : 0; }
        private static double D(string[] a, int i) { double x; return a != null && i < a.Length && double.TryParse(a[i], NumberStyles.Float, Inv, out x) ? Fin(x) : 0.0; }

        // ------------------------------------------------------------ pomocnicze
        private static Clan RaidClan()
        {
            if (_clanTried) return _clan;
            _clanTried = true;
            try { foreach (var c in Clan.BanditFactions) if (c != null && c.StringId == ClanId) { _clan = c; break; } }
            catch (Exception e) { Stumble("RaidClan", e); }
            return _clan;
        }

        private static Settlement Find(string id)
        {
            if (id == null) return null;
            try { return Settlement.Find(id); } catch { return null; }
        }

        private static bool IsFreeFolk(Settlement n) { var f = n != null ? n.MapFaction : null; return f != null && f.StringId == CrownGifts.FreeFolkId; }

        // ------------------------------------------------------------ flaga "bez nowych decyzji" (marsz i powrot) - nigdy w zapisie gry
        /// <summary>Banda idzie do punktu bez nowych decyzji AI (bez napadow i ucieczek po drodze). force - rozkaz zawsze (zmiana fazy); inaczej tylko gdy zgubiony.</summary>
        private static void Freeze(MobileParty mp, CampaignVec2 to, bool force)
        {
            if (!mp.Ai.DoNotMakeNewDecisions) mp.Ai.SetDoNotMakeNewDecisions(true);
            if (force || mp.DefaultBehavior != AiBehavior.GoToPoint) mp.SetMoveGoToPoint(to, MobileParty.NavigationType.Default);
        }

        /// <summary>Banda puszczona jako zwykla banda (bez zarzadu): AI gry wraca i mysli od razu (jak MaterialOrders.Release).</summary>
        private static void Release(MobileParty mp)
        {
            try { mp.Ai.SetDoNotMakeNewDecisions(false); mp.Ai.RethinkAtNextHourlyTick = true; } catch { }
        }

        private static Dictionary<string, MobileParty> BanditMap()
        {
            var map = new Dictionary<string, MobileParty>(StringComparer.Ordinal);
            foreach (var mp in MobileParty.AllBanditParties) if (mp != null && mp.StringId != null) map[mp.StringId] = mp;
            return map;
        }

        private static MobileParty PartyOf(Raid r, ref Dictionary<string, MobileParty> map)
        {
            var mp = r.P;
            if (mp == null || mp.StringId != r.Id)
            {
                if (map == null) map = BanditMap();
                map.TryGetValue(r.Id, out mp);
                r.P = mp;
            }
            return mp != null && mp.IsActive ? mp : null;
        }

        /// <summary>CampaignEvents.OnBeforeSaveEvent: zdejmuje flage "bez nowych decyzji" z band w marszu i powrocie - gra zapisuje ja w partii
        /// (MobilePartyAi.DoNotMakeNewDecisions, [SaveableProperty(11)]); bez wpisu arm_mur_najazdy (starszy DLL, straznik, blad) banda stalaby zamrozona na zawsze.</summary>
        internal static void BeforeSave()
        {
            _thawed = 0;
            if (_raids.Count == 0) return;
            Dictionary<string, MobileParty> map = null;
            foreach (var r in _raids)
            {
                try
                {
                    if (r.Phase == 0) continue;
                    var mp = PartyOf(r, ref map);
                    if (mp == null || mp.Ai == null || !mp.Ai.DoNotMakeNewDecisions) continue;
                    mp.Ai.SetDoNotMakeNewDecisions(false);
                    _thawed++;
                }
                catch (Exception e) { Stumble("BeforeSave", e); }
            }
        }

        /// <summary>CampaignEvents.OnSaveOverEvent (takze po nieudanym zapisie): flaga i rozkaz wracaja bandom w marszu i powrocie.</summary>
        internal static void SaveOver()
        {
            if (_thawed == 0) return;
            _thawed = 0;
            Refreeze();
        }

        /// <summary>CampaignEvents.HourlyTickEvent: po wczytaniu (Import z bandami w marszu / powrocie) flaga i rozkaz wracaja w pierwszej godzinie. Inaczej nic.</summary>
        internal static void Hourly()
        {
            if (_kinDirty)
            {
                // fix-mur: po wczytaniu - bandy najazdu w migawce latki AI "swoi nie bija swoich" (zanim lordowie Wolnych Ludzi zdaza je zaatakowac)
                _kinDirty = false;
                try { Dictionary<string, MobileParty> map = null; foreach (var r in _raids) PartyOf(r, ref map); RebuildKin(Settings.Current); }
                catch (Exception e) { Stumble("Hourly(swoi)", e); }
            }
            if (!_refreeze) return;
            _refreeze = false;
            Refreeze();
        }

        private static void Refreeze()
        {
            if (_raids.Count == 0) return;
            Dictionary<string, MobileParty> map = null;
            foreach (var r in _raids)
            {
                try
                {
                    if (r.Phase == 0) continue;
                    var mp = PartyOf(r, ref map);
                    if (mp == null || mp.Ai == null) continue;
                    var to = Find(r.Phase == 1 ? r.Src : r.Target);
                    if (to == null) continue;                                    // Daily rozstrzygnie (wies zniknela - banda puszczona)
                    if (mp.MapEvent != null || mp.SiegeEvent != null) { if (!mp.Ai.DoNotMakeNewDecisions) mp.Ai.SetDoNotMakeNewDecisions(true); continue; }
                    Freeze(mp, to.GatePosition, false);
                }
                catch (Exception e) { Stumble("Refreeze", e); }
            }
        }

        // ------------------------------------------------------------ doba
        private sealed class RegHunger { public Settlement N; public double Home, Harvest, Food, Cap, H; public Village Worst; public double WorstW; }

        /// <summary>ArmouryBehavior.OnDailyTick, po Famine.Daily (migawka spichlerzy) i PRZED OutlawLaw.Daily (linia puli liczy dzisiejsze narodziny i rozwiazania band).</summary>
        internal static void Daily()
        {
            long t0 = Stopwatch.GetTimestamp();
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            int today = (int)CampaignTime.Now.ToDays, day = today - 1;
            bool on = s.FreeFolkHungerRaids && StartCalibration.Applied && Famine.On;
            if (!on && _raids.Count == 0)
            {
                _owned.Clear();
                if (_kinOn || _kin.Count > 0) { _kin = new HashSet<MobileParty>(); _kinOn = false; }   // fix-mur: bez band - latka AI bez pracy
                if (!_offLogged && s.LogEnabled)
                {
                    _offLogged = true;
                    string why = !s.FreeFolkHungerRaids ? "klucz Free Folk Hunger Raids wylaczony" : (!StartCalibration.Applied ? "kalibracja startu (KS) nie zastosowana" : "spichlerze (Famine) wylaczone");
                    try { Log.Info("Najazdy Wolnych Ludzi (r4-mur): dzien " + day + " | WYLACZONE (" + why + ") - bez band w polu nic nie biegnie; od poczatku: wyruszylo " + _cStarted + " ludzi w " + _nStarted + " bandach, wrocilo " + _cReturned + ", straty " + _cLost + "."); } catch { }
                }
                return;
            }
            _offLogged = false;
            int maxP = Math.Max(0, Math.Min(MaxEntries, s.FreeFolkRaidMaxParties)), maxMen = Math.Max(1, s.FreeFolkRaidMaxMen), minMen = Math.Max(1, Math.Min(maxMen, s.FreeFolkRaidMinMen));
            int dLoss = 0, dGain = 0, dTrim = 0, dRetN = 0, dRetMen = 0, dLostN = 0, dLostMen = 0, dLeft = 0, dTurn = 0; long dGold = 0, dItems = 0;

            // 1. bandy w polu: straty, powrot, dom
            var map = _raids.Count > 0 ? BanditMap() : new Dictionary<string, MobileParty>(StringComparer.Ordinal);
            int dArrive = 0, dMarchCap = 0;
            _owned.Clear();
            foreach (var r in _raids.ToList())
            {
                try
                {
                    MobileParty mp;
                    if (!map.TryGetValue(r.Id, out mp) || mp == null || !mp.IsActive)
                    {
                        _cLost += r.Last; dLostMen += r.Last; dLostN++; _nLost++;
                        // fix-mur: kto zatrzymal bande (zdarzenie zniszczenia: strona zwyciezcy; brak - inni / bez bitwy)
                        if (r.Phase == 2)
                        {
                            if (r.Killer == "w") _nKillW++; else if (r.Killer == "n") _nKillN++; else if (r.Killer == "f") _nKillF++; else _nKillO++;
                        }
                        else _nDiedAfter++;
                        _raids.Remove(r);
                        continue;
                    }
                    r.P = mp;
                    _owned.Add(mp);
                    int men = mp.MemberRoster.TotalRegulars;
                    if (men < r.Last) { dLoss += r.Last - men; _cLost += r.Last - men; }
                    else if (men > r.Last) { dGain += men - r.Last; _cGained += men - r.Last; }
                    r.Last = men;
                    var src = Find(r.Src); var region = Find(r.Region);
                    if (men > maxMen && mp.MapEvent == null && region != null)
                    {
                        int cut = Trim(mp, region, men - maxMen);
                        dTrim += cut; _cTrim += cut; r.Last = mp.MemberRoster.TotalRegulars;
                    }
                    if (src == null || src.Village == null || region == null)
                    {
                        // wies zrodla zniknela z gry - banda zostaje zwykla banda (bez zarzadu), ludzie dalej w polu jak kazdej bandy
                        _cLeft += r.Last; dLeft++; _owned.Remove(mp); _raids.Remove(r);
                        Release(mp);
                        continue;
                    }
                    if (r.Phase == 2)
                    {
                        // wymarsz: do bramy wsi celu bez nowych decyzji (nie napada po drodze wlasnych wsi Wolnych Ludzi); u celu - patrol (AI band gry)
                        if (mp.MapEvent != null || mp.SiegeEvent != null) continue;
                        var tgt = Find(r.Target);
                        bool beatenM = r.Last == 0 || (r.Men0 > 0 && r.Last * 2 < r.Men0);
                        if (beatenM)
                        {
                            Freeze(mp, src.GatePosition, true);
                            r.Phase = 1; r.BackDay = today; dTurn++; _nTurnMarch++;      // fix-mur: zatrzymana w marszu (rozbita, wraca)
                            continue;
                        }
                        bool arrived = tgt != null && mp.GetPosition2D.Distance(tgt.GatePosition.ToVec2()) <= MarchArrive;
                        if (arrived || tgt == null || today - r.Day >= ReturnMaxDays)
                        {
                            mp.Ai.SetDoNotMakeNewDecisions(false);
                            if (tgt != null) mp.SetMovePatrolAroundSettlement(tgt, MobileParty.NavigationType.Default, false);
                            else mp.Ai.RethinkAtNextHourlyTick = true;
                            r.Phase = 0; r.Day = today;                               // doby najazdu od dojscia do celu
                            if (arrived) { dArrive++; _nReached++; } else { dMarchCap++; _nMarchCap++; }   // marsz ponad ReturnMaxDays (albo cel zniknal) - patrol tam, gdzie stoi (AI band gry)
                        }
                        else Freeze(mp, tgt.GatePosition, false);                     // flaga i rozkaz (po wczytaniu / zapisie / bitwie)
                        continue;
                    }
                    if (r.Phase == 0)
                    {
                        bool timeUp = today - r.Day >= Math.Max(1, s.FreeFolkRaidDays);
                        bool beaten = r.Men0 > 0 && r.Last * 2 < r.Men0;
                        if ((timeUp || beaten) && mp.MapEvent == null && mp.SiegeEvent == null)
                        {
                            Freeze(mp, src.GatePosition, true);
                            r.Phase = 1; r.BackDay = today; dTurn++;
                        }
                        continue;
                    }
                    if (mp.MapEvent != null || mp.SiegeEvent != null) continue;
                    float dist = mp.GetPosition2D.Distance(src.GatePosition.ToVec2());
                    if (dist <= ArriveDist || today - r.BackDay >= ReturnMaxDays || r.Last == 0)
                    {
                        if (mp.IsCurrentlyUsedByAQuest)
                        {
                            // banda w zadaniu gracza - nie rozwiazujemy (zadanie by sie zepsulo); zostaje zwykla banda w polu
                            _cLeft += r.Last; dLeft++; _owned.Remove(mp); _raids.Remove(r);
                            Release(mp);
                            continue;
                        }
                        long g, it;
                        int back = Home(mp, src, region, out g, out it);
                        if (back < 0) { _cLeft += r.Last; dLeft++; _owned.Remove(mp); _raids.Remove(r); continue; }   // prawo wyrzutkow wylaczone - banda zostaje w polu
                        _cReturned += back; dRetMen += back; dRetN++; _nReturned++; dGold += g; dItems += it; _cGold += g; _cItems += it;
                        _owned.Remove(mp); _raids.Remove(r);
                        continue;
                    }
                    Freeze(mp, src.GatePosition, false);                              // flaga i rozkaz (po wczytaniu / zapisie / bitwie)
                }
                catch (Exception e) { Stumble("Daily(banda)", e); }
            }

            // 2. indeks glodu Wolnych Ludzi (migawka Famine tej doby; wsie z KS zastosowanym)
            var regs = new List<RegHunger>();
            double hHome = 0, hHarv = 0, hFood = 0, hSum = 0;
            foreach (var n in OutlawLaw.RegionNodes())
            {
                try
                {
                    if (n == null || !IsFreeFolk(n) || n.BoundVillages == null || n.BoundVillages.Count == 0) continue;
                    var rh = new RegHunger { N = n };
                    foreach (var v in n.BoundVillages)
                    {
                        double home, harv, food, cap; float u; int d;
                        if (!StartCalibration.AppliedFor(v) || !Famine.DayOf(v, out home, out harv, out food, out cap, out u, out d) || d != today || !(home > 0.0)) continue;
                        rh.Home += home; rh.Harvest += harv; rh.Food += food; rh.Cap += cap;
                        double wv = home * Math.Max(0.0, 1.0 - harv / home);
                        if (v.VillageState == Village.VillageStates.Normal && (rh.Worst == null || wv > rh.WorstW)) { rh.Worst = v; rh.WorstW = wv; }
                    }
                    if (!(rh.Home > 0.0)) continue;
                    double cov = Math.Min(1.0, rh.Harvest / rh.Home), days = rh.Food / rh.Home, full = rh.Cap / rh.Home;
                    double wgt = full > LowDays ? Math.Max(StoreFloor, Math.Min(1.0, (full - days) / (full - LowDays))) : 1.0;
                    if (days <= LowDays) wgt = 1.0;
                    rh.H = Math.Max(0.0, Math.Min(1.0, (1.0 - cov) * wgt));
                    regs.Add(rh);
                    hHome += rh.Home; hHarv += rh.Harvest; hFood += rh.Food; hSum += rh.H * rh.Home;
                }
                catch (Exception e) { Stumble("Daily(glod)", e); }
            }
            double H = hHome > 0 ? hSum / hHome : 0.0;
            double chance = Math.Max(0.0, Math.Min(1.0, H * Math.Max(0f, s.FreeFolkRaidDailyChance)));

            // 3. nowa banda (najwyzej jedna na dobe; twardy sufit band)
            int spawnN = 0, spawnMen = 0, noRoad = 0, tooFew = 0, capHit = 0; double fromPoolD = 0, fromHomeD = 0; string spawnNote = "";
            bool outlaw = OutlawLaw.On;
            var clan = RaidClan();
            if (on && outlaw && clan != null && H > 0.0)
            {
                if (_raids.Count >= maxP) capHit = 1;
                else if (MBRandom.RandomFloat < chance)
                {
                    try
                    {
                        RegHunger pick = null; double tw = 0;
                        foreach (var rh in regs) if (rh.H > 0.0 && rh.Worst != null && !rh.N.IsUnderSiege) tw += rh.H * rh.Home;
                        double roll = MBRandom.RandomFloat * tw;
                        foreach (var rh in regs)
                        {
                            if (!(rh.H > 0.0) || rh.Worst == null || rh.N.IsUnderSiege) continue;
                            roll -= rh.H * rh.Home; pick = rh;
                            if (roll <= 0.0) break;
                        }
                        if (pick != null)
                        {
                            var srcV = pick.Worst;
                            var target = PickTarget(srcV);
                            if (target == null) noRoad++;
                            else
                            {
                                int want = Math.Max(minMen, Math.Min(maxMen, minMen + (int)Math.Round(pick.H * (maxMen - minMen))));
                                var org = PeopleBook.OriginLive ? new PeopleBook.Shares() : null;
                                TroopRoster roster; float fp, fh;
                                int n = OutlawLaw.RaidDraw(pick.N, want, minMen, clan, org, out roster, out fp, out fh);
                                fromHomeD += fh;
                                if (n <= 0) { tooFew++; _cHomeLeft += fh; }
                                else
                                {
                                    double homeUsed = Math.Max(0.0, n - fp);
                                    _cHomeLeft += Math.Max(0.0, fh - homeUsed);
                                    MobileParty mp = null;
                                    try
                                    {
                                        mp = BanditPartyComponent.CreateLooterParty("armoury_ffraid", clan, target, false, null, srcV.Settlement.GatePosition);
                                        mp.MemberRoster.Add(roster);
                                    }
                                    catch (Exception e) { Stumble("Daily(utworzenie bandy)", e); }
                                    if (mp == null || mp.MemberRoster.TotalRegulars < n)
                                    {
                                        // banda nie powstala (wyjatek gry) - ludzie wracaja do puli okregu (nikt nie znika); pusta partia rozwiazana
                                        if (mp == null || mp.MemberRoster.TotalRegulars == 0)
                                        {
                                            OutlawLaw.RaidUndraw(pick.N, roster);           // ludzie z powrotem do puli, narodziny dnia cofniete (recenzja r4-mur: nie "inne" w linii puli)
                                            _cHomeLeft += homeUsed; tooFew++;
                                            if (mp != null) { try { DestroyPartyAction.Apply(null, mp); } catch (Exception e) { Stumble("Daily(pusta banda)", e); } }
                                            mp = null;
                                        }
                                    }
                                    if (mp != null)
                                    {
                                        int got = mp.MemberRoster.TotalRegulars;
                                        _raids.Add(new Raid { Id = mp.StringId, Src = srcV.Settlement.StringId, Region = pick.N.StringId, Target = target.StringId, Day = today, Men0 = got, Phase = 2, BackDay = 0, Last = got, P = mp });
                                        _owned.Add(mp);
                                        spawnN++; spawnMen += got; fromPoolD += fp;
                                        _cStarted += got; _nStarted++; _cFromPool += fp; _cFromHome += homeUsed;
                                        spawnNote = ", " + srcV.Settlement.Name + " -> " + target.Name;
                                        try
                                        {
                                            mp.ActualClan = clan;
                                            mp.Aggressiveness = 1f - 0.1f * MBRandom.RandomFloat;
                                            mp.InitializePartyTrade(0);                      // bez zlota z niczego
                                            mp.Party.SetVisualAsDirty();
                                        }
                                        catch (Exception e) { Stumble("Daily(ustawienia bandy)", e); }
                                        if (org != null && org.Total > 0) { try { PeopleBook.Give(mp.MemberRoster, org, PeopleBook.FPoolBand); } catch { } }
                                        try { PeopleLedger.FixCreatedMen(mp); } catch { }   // sklad dopisany po zdarzeniu utworzenia (ksiega ludzi E4/2)
                                        try { Freeze(mp, target.GatePosition, true); } catch (Exception e) { Stumble("Daily(rozkaz)", e); }   // wymarsz bez napadow po drodze; u celu - patrol
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e) { Stumble("Daily(nowa banda)", e); }
                }
            }

            try { RebuildKin(s); } catch (Exception e) { Stumble("Daily(swoi)", e); }   // fix-mur: bandy najazdu (takze nowa) w migawce latki AI
            int veto = Interlocked.Exchange(ref _kinVeto, 0); _kinVetoAll += veto;
            if (!s.LogEnabled) return;
            try
            {
                int active = 0, activeMen = 0, biggest = 0, back = 0, march = 0;
                foreach (var r in _raids) { active++; activeMen += r.Last; if (r.Last > biggest) biggest = r.Last; if (r.Phase == 1) back++; else if (r.Phase == 2) march++; }
                long ctl = _cStarted + _cGained - _cReturned - _cLost - _cTrim - _cLeft - activeMen;
                var sb = new StringBuilder(1400);
                sb.Append("Najazdy Wolnych Ludzi (r4-mur): dzien ").Append(day).Append(on ? " | CZYNNE" : " | WYLACZONE (bandy w polu wracaja)");
                if (on && !outlaw) sb.Append(" [bez prawa wyrzutkow - nowe bandy nie ruszaja]");
                if (on && clan == null) sb.Append(" [brak klanu wildlings]");
                sb.Append(" | glod: indeks ").Append(F(H, "0.000")).Append(" (pokrycie z pola ").Append(F(hHome > 0 ? hHarv / hHome : 0, "0.000")).Append(", spichlerze ")
                  .Append(F(hHome > 0 ? hFood / hHome : 0, "0")).Append(" dni, okregi ").Append(regs.Count).Append(", ludzie ").Append(F(hHome, "0")).Append(')');
                sb.Append(" | szansa ").Append(F(chance, "0.000")).Append(", nowe dzis ").Append(spawnN).Append(" (").Append(spawnMen).Append(" ludzi: z puli ").Append(F(fromPoolD, "0.0"))
                  .Append(", zabrani z domow ").Append(F(fromHomeD, "0.0")).Append(spawnNote).Append(')');
                sb.Append(" | w polu ").Append(active).Append('/').Append(maxP).Append(" band (wraca ").Append(back).Append(", w marszu ").Append(march).Append(", doszly dzis ").Append(dArrive).Append(dMarchCap > 0 ? ", marsz ponad " + ReturnMaxDays + " dob " + dMarchCap : "").Append("), ludzi ").Append(activeMen).Append(", najwieksza ").Append(biggest).Append('/').Append(maxMen)
                  .Append(capHit > 0 ? " [sufit band]" : "");
                sb.Append(" | dzis: straty ").Append(dLoss).Append(", przybyli ").Append(dGain).Append(", przyciete ").Append(dTrim).Append(", zawrocily ").Append(dTurn)
                  .Append(", wrocilo ").Append(dRetN).Append(" band (").Append(dRetMen).Append(" ludzi, lup ").Append(dGold).Append(" zl i ").Append(dItems).Append(" szt. do wsi)")
                  .Append(", zginelo ").Append(dLostN).Append(" band (").Append(dLostMen).Append(" ludzi)").Append(dLeft > 0 ? ", bez zarzadu " + dLeft : "");
                sb.Append(" | od poczatku: wyruszylo ").Append(_cStarted).Append(" + przybyli ").Append(_cGained).Append(" = wrocili ").Append(_cReturned).Append(" + straty ").Append(_cLost)
                  .Append(" + przyciete ").Append(_cTrim).Append(" + puszczone bez zarzadu ").Append(_cLeft).Append(" + w polu ").Append(activeMen).Append(" (reszta ").Append(ctl).Append(')');
                sb.Append(" | ludzie najazdow: z puli ").Append(F(_cFromPool, "0.0")).Append(" + z domow ").Append(F(_cFromHome, "0.0")).Append(" = wyruszylo ").Append(_cStarted)
                  .Append(" (reszta ").Append(F(_cStarted - _cFromPool - _cFromHome, "0.0")).Append("); zabrani z domow, a zostali w puli ").Append(F(_cHomeLeft, "0.0"));
                sb.Append(" | band: wyruszylo ").Append(_nStarted).Append(", wrocilo ").Append(_nReturned).Append(", zginelo ").Append(_nLost).Append("; lup do wsi ").Append(_cGold).Append(" zl, ").Append(_cItems).Append(" szt.");
                sb.Append(" | bez drogi ").Append(noRoad).Append(", za malo ludzi ").Append(tooFew);
                // fix-mur: wynik marszu od poczatku (doszly / zatrzymane i przez kogo / marsz ponad 30 dob) i latka "swoi nie bija swoich"
                int blocked = _nKillW + _nKillN + _nKillF + _nKillO + _nTurnMarch;
                sb.Append(" | cel: doszly ").Append(_nReached).Append(" z ").Append(_nStarted).Append(" band (dzis ").Append(dArrive).Append("), zatrzymane w marszu ").Append(blocked)
                  .Append(" (zginely: Straz ").Append(_nKillW).Append(", Polnoc ").Append(_nKillN).Append(", Wolni Ludzie ").Append(_nKillF).Append(", inni ").Append(_nKillO)
                  .Append("; zawrocily rozbite ").Append(_nTurnMarch).Append("), marsz ponad ").Append(ReturnMaxDays).Append(" dob ").Append(_nMarchCap)
                  .Append(", w marszu teraz ").Append(march).Append(", zginely po dojsciu lub w powrocie ").Append(_nDiedAfter);
                sb.Append(" | swoi nie bija swoich: ").Append(s.FreeFolkRaidKinPeace ? "tak" : "nie").Append(" (latka ").Append(_kinHook).Append(", band w migawce ").Append(_kin.Count)
                  .Append("), odmowy ataku dzis ").Append(veto).Append(" (od wczytania ").Append(_kinVetoAll).Append(')');
                sb.Append(" | potkniecia ").Append(_stumbles);
                sb.Append(" | czas ").Append(((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.00", Inv)).Append(" ms.");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily(linia)", e); }
        }

        // ------------------------------------------------------------ fix-mur: swoi nie bija swoich (latka AI) i kto zatrzymal bande
        /// <summary>SubModuleMain (menu glowne, jak inne latki): postfiks DefaultMobilePartyAIModel.ShouldConsiderAttacking (metoda zadeklarowana w tej klasie -
        /// AccessTools.DeclaredMethod; klasa bez pol statycznych - bez konstruktora statycznego przy kompilacji latki). Czynny modelem jest NavalDLCMobilePartyAIModel,
        /// ktory wola BaseModel.ShouldConsiderAttacking (dekompilacja NavalDLC) - latka dziala dla obu. Zadna inna paczka ani mod tej metody nie latka.</summary>
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(DefaultMobilePartyAIModel), "ShouldConsiderAttacking", new[] { typeof(MobileParty), typeof(MobileParty) });
                if (m != null) { h.Patch(m, postfix: new HarmonyMethod(typeof(FreeFolkRaids), nameof(KinPost)) { priority = Priority.Last }); _kinHook = "wpieta"; }
                else _kinHook = "BRAK ShouldConsiderAttacking";
            }
            catch (Exception e) { _kinHook = "blad " + e.GetType().Name; Log.Error("FreeFolkRaids.ApplyAll", e); }
            try { Log.Info("Najazdy Wolnych Ludzi (fix-mur): latka 'Wolni Ludzie nie bija wlasnych band najazdu' " + _kinHook + " (postfiks DefaultMobilePartyAIModel.ShouldConsiderAttacking; dziala przy kluczu Free Folk Raid Kin Peace i bandach najazdu w polu)."); } catch { }
        }

        /// <summary>Postfiks ShouldConsiderAttacking (watki AI): partia frakcji Wolnych Ludzi nie atakuje bandy najazdu i banda najazdu nie atakuje partii Wolnych Ludzi.
        /// Tylko odczyt niezmiennej migawki (_kin); poza nia - wynik gry bez zmian. Nie rzuca.</summary>
        public static void KinPost(MobileParty party, MobileParty targetParty, ref bool __result)
        {
            if (!__result || !_kinOn) return;
            try
            {
                var k = _kin;
                if (k == null || k.Count == 0 || party == null || targetParty == null) return;
                bool tIn = k.Contains(targetParty), pIn = k.Contains(party);
                if (tIn == pIn) return;                                    // zadna albo obie (ten sam klan - i tak nie wrogowie)
                var other = tIn ? party : targetParty;
                var f = other.MapFaction;
                if (f == null || f.StringId != CrownGifts.FreeFolkId) return;
                __result = false;
                Interlocked.Increment(ref _kinVeto);
            }
            catch { }
        }

        private static void RebuildKin(Settings s)
        {
            bool on = s != null && s.FreeFolkRaidKinPeace;
            var set = new HashSet<MobileParty>();
            if (on) foreach (var r in _raids) if (r.P != null && r.P.IsActive) set.Add(r.P);
            _kin = set;                                                     // publikacja calej migawki (watki AI czytaja stara albo nowa - nigdy w trakcie zmiany)
            _kinOn = on && set.Count > 0;
        }

        /// <summary>CampaignEvents.MobilePartyDestroyed: kto rozbil bande najazdu (strona zwyciezcy - MapEventSide.HandleMapEventEndForPartyInternal wola
        /// DestroyPartyAction.Apply(lider strony przeciwnej, banda)). Tylko zapis strony do linii "cel:"; bez band - nic.</summary>
        internal static void OnPartyDestroyed(MobileParty party, PartyBase destroyer)
        {
            if (_raids.Count == 0 || party == null) return;
            try
            {
                foreach (var r in _raids)
                {
                    if (r.P != party && (r.Id == null || r.Id != party.StringId)) continue;
                    var f = destroyer != null ? destroyer.MapFaction : null;
                    r.Killer = f == null ? "o" : f.StringId == CrownGifts.WatchId ? "w" : f.StringId == CrownGifts.NorthId ? "n" : f.StringId == CrownGifts.FreeFolkId ? "f" : "o";
                    break;
                }
            }
            catch (Exception e) { Stumble("OnPartyDestroyed", e); }
        }

        /// <summary>Cel najazdu: wies Strazy albo Polnocy (stan Normal) z droga ladowa od wsi zrodla - z TargetPrefilter najblizszych w linii prostej TargetNearest
        /// najblizszych droga, losowo z waga 1 / droga. null - brak drogi przez Mur.</summary>
        private static Settlement PickTarget(Village src)
        {
            var from = src.Settlement;
            var p = from.GetPosition2D;
            var cand = new List<KeyValuePair<Settlement, float>>();
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsVillage || st.Village == null || st.Village.VillageState != Village.VillageStates.Normal) continue;
                var f = st.MapFaction;
                if (f == null || (f.StringId != CrownGifts.WatchId && f.StringId != CrownGifts.NorthId)) continue;
                cand.Add(new KeyValuePair<Settlement, float>(st, p.DistanceSquared(st.GetPosition2D)));
            }
            var near = cand.OrderBy(c => c.Value).Take(TargetPrefilter).ToList();
            var road = new List<KeyValuePair<Settlement, float>>();
            var m = Campaign.Current.Models.MapDistanceModel;
            foreach (var c in near)
            {
                try
                {
                    float d = m.GetDistance(from, c.Key, false, false, MobileParty.NavigationType.Default);
                    if (d > 0f && d < CartTownExit.BkLimit) road.Add(new KeyValuePair<Settlement, float>(c.Key, d));
                }
                catch (Exception e) { Stumble("PickTarget", e); }
            }
            var top = road.OrderBy(c => c.Value).Take(TargetNearest).ToList();
            if (top.Count == 0) return null;
            double tw = 0; foreach (var c in top) tw += 1.0 / Math.Max(1f, c.Value);
            double roll = MBRandom.RandomFloat * tw;
            foreach (var c in top)
            {
                roll -= 1.0 / Math.Max(1f, c.Value);
                if (roll <= 0.0) return c.Key;
            }
            return top[top.Count - 1].Key;
        }

        /// <summary>Nadmiar ludzi bandy ponad sufit (np. scalenie band przy kryjowce) - do puli okregu zrodla w swoim typie, z udzialami pochodzenia. Zwraca, ilu zdjeto.</summary>
        private static int Trim(MobileParty mp, Settlement region, int extra)
        {
            int cut = 0;
            var r = mp.MemberRoster;
            for (int i = r.Count - 1; i >= 0 && cut < extra; i--)
            {
                var e = r.GetElementCopyAtIndex(i);
                if (e.Character == null || e.Character.IsHero || e.Number <= 0) continue;
                int n = Math.Min(extra - cut, e.Number);
                try { if (PeopleBook.OriginLive) PeopleBook.PoolShares(region, PeopleBook.Take(r, n, PeopleBook.FBandDisband)); } catch { }
                int wounded = Math.Min(e.WoundedNumber, n);
                r.AddToCountsAtIndex(i, -n, -wounded);
                OutlawLaw.RaidTrimAdd(region, e.Character, n);               // naplyw "rozwiazane bandy" linii puli (recenzja r4-mur: nie reszta "inne")
                cut += n;
            }
            return cut;
        }

        /// <summary>Banda w domu: bohaterowie-jency uciekaja, zloto do kiesy wsi zrodla, towar do jej magazynu, banda rozwiazana (OutlawLaw.RaidDisband -
        /// DestroyPartyAction bez sprawcy; ludzie i jency do puli okregu zrodla ta sama droga co kazda rozwiazana banda). Zwraca ludzi skladu; -1 - prawo
        /// wyrzutkow wylaczone (banda zostaje w polu, bez zarzadu).</summary>
        private static int Home(MobileParty mp, Settlement src, Settlement region, out long gold, out long items)
        {
            gold = 0; items = 0;
            if (!OutlawLaw.On) { Release(mp); return -1; }
            var pr = mp.PrisonRoster;
            for (int i = pr.Count - 1; i >= 0; i--)
            {
                var e = pr.GetElementCopyAtIndex(i);
                if (e.Character != null && e.Character.IsHero && e.Character.HeroObject != null)
                {
                    try { EndCaptivityAction.ApplyByEscape(e.Character.HeroObject, null, false); } catch (Exception ex) { Stumble("Home(jeniec)", ex); }
                }
            }
            var v = src.Village;
            try
            {
                if (mp.IsPartyTradeActive && mp.PartyTradeGold > 0)
                {
                    gold = mp.PartyTradeGold;
                    v.ChangeGold(mp.PartyTradeGold); mp.PartyTradeGold = 0;
                    MoneyLedger.Note169(MoneyLedger.N169PurseGone, src, (int)gold);   // kiesa wsi poza GiveGoldAction - "sakiewki rozbitych partii" w "Przeplywy osad (przyczyny)" (tylko licznik)
                }
            }
            catch (Exception e) { Stumble("Home(zloto)", e); }
            try
            {
                var ir = mp.ItemRoster; var to = src.ItemRoster;
                for (int i = ir.Count - 1; i >= 0; i--)
                {
                    var el = ir.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0 || el.EquipmentElement.Item == null) continue;
                    to.AddToCounts(el.EquipmentElement, el.Amount);
                    ir.AddToCounts(el.EquipmentElement, -el.Amount);
                    items += el.Amount;
                }
            }
            catch (Exception e) { Stumble("Home(towar)", e); }
            return OutlawLaw.RaidDisband(region, mp);                         // rozwiazanie: ludzie i jency do puli okregu zrodla (OnPartyDestroyed), -1 bez prawa wyrzutkow
        }

        private static string F(double x, string f) { return (double.IsNaN(x) || double.IsInfinity(x) ? 0.0 : x).ToString(f, Inv); }
    }
}
