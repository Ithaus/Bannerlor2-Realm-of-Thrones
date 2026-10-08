using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;

namespace Armoury
{
    /// <summary>
    /// KLAN NAJEMNIKOW SPOILS TYLKO Z PRAWDZIWYCH ZOLNIERZY (Jeff 07.10: "tylko prawdziwi" - koniec 20 z niczego).
    /// Dekompilacja RealisticLoot.dll 1.8.4 (Spoils of War) + gry 1.4.8 - skad klan najemnikow gracza bierze ludzi:
    ///  1. SubClanBehavior.SpawnLordParty(przywodca, ile, nazwa) - jedyne miejsce SPOILS, ktore dodaje zolnierzy: tworzy druzyne
    ///     lorda (LordPartyComponent.CreateLordParty) i dosypuje kultury przywodcy do "ile + 1" (AddToCounts, 2/5 rekrutow, 2/5
    ///     awansu, reszta drugiego awansu). Wolaja ja: zalozenie (20), odnowienie (20), nowa druzyna (20), RespawnMissingParties
    ///     co dobe (przywodca bez druzyny - 20, kapitan - 10) i powrot przywodcy z niewoli (max(5, 20/2) = 10).
    ///  2. LordPartyComponent.InitializationArgs.InitializeLordPartyProperties (gra) - kazda nowa druzyna lorda spoza rodu gracza
    ///     dostaje sklad z szablonu kultury (PartySizeLimitModel.FindAppropriateInitialRosterForMobileParty; ROT/BEE/NavalDLC tylko
    ///     przekazuja do gry). Szablony lordow ROT maja 53-56 ludzi (prawie wszystkie stosy min = max) - to one, nie "20" Spoils, sa dzis druzyna klanu
    ///     (Spoils dosypuje tylko do 20 + 1, wiec przy 54 z szablonu nie doklada nic). Ta sama droga idzie gra
    ///     (HeroSpawnCampaignBehavior.ConsiderSpawningLordParties - co dobe druzyna dla bohatera rodu bez druzyny), takze dla
    ///     klanu Spoils (przywodca i kapitanowie maja Occupation.Lord).
    ///  3. RecruitmentCampaignBehavior.HourlyTickParty - druzyna lorda poza osada, gdy jej strona (MapFaction) to mniejsza frakcja
    ///     (klan Spoils po zwolnieniu z krolestwa): 3-7 "ochotnikow z mapy" (ApplyInternal, VolunteerFromMap) bez osady i bez
    ///     notabla, zaplata GiveGoldAction(przywodca, nikt) - ludzie z niczego, zloto do nikad.
    ///  4. BK BKClanBehavior.ConvertTroopsMercenaries (co dobe, DailyClanTickImpl) - klan typu "najemnicy" (Spoils ustawia
    ///     IsClanTypeMercenary) W KROLESTWIE: kazdy wpis skladu spoza szablonu klanu - TAKZE BOHATER (przywodca, kapitan) - zamieniany
    ///     1:1 na zolnierza szablonu (albo BasicTroop) z szansa 1: przywodca wypada z wlasnej druzyny (druzyna zostaje bez wodza,
    ///     LeaderHero = null), a w jego miejsce staje zolnierz z niczego; prawdziwi rekruci zmieniaja typ (po jednym na wpis na dobe).
    ///     Sprawdzone na prawdziwym BannerKings.dll z gry (recenzja, proba sekcja 13). Galaz poza krolestwem (+3-4 z szablonu) martwa
    ///     (wolajacy wychodzi przy Kingdom == null).
    /// Prawdziwe drogi (bez zmian): ochotnicy notabli i najemnicy z karczmy (ApplyInternal z osada - BK zdejmuje czlowieka z klasy
    /// ludnosci osady, LevyGold placi notablowi / miastu, PeopleLedger liczy w "zwerbowani"), wcielanie jencow (prawdziwi ludzie).
    /// ZMIANA (wlacznik SpoilsClanRealSoldiers): druzyna klanu powstaje z samym przywodca (szablon zdjety w chwili powstania, dosypka
    /// Spoils = 0, wszystko inne w druzynie stworzonej przez Spoils przed chwila zdjete), ochotnikow z mapy klan nie dostaje, zamiany
    /// BK (4.) dla klanu Spoils nie ma (przywodca zostaje w druzynie, zwerbowani zostaja soba); rosnie
    /// tylko z prawdziwego werbunku za pieniadze przywodcy. To, co gracz placi Spoils za zalozenie / odnowienie / nowa druzyne, a
    /// Spoils przelewa tylko w czesci (1/2, 1/2, 1/4), idzie w calosci do kiesy klanu (przywodca / nowy kapitan) - pieniadze za
    /// ludzi trafiaja do tego, kto ich werbuje; gdy nie ma komu - wracaja do gracza.
    /// Klan Spoils = Clan o StringId "rl_merc_..." (Spoils: Clan.CreateClan("rl_merc_" + CampaignTime.Now.ElapsedDaysUntilNow) - to zawsze
    /// "rl_merc_0", gra dokleja numer, gdy zajete; takze klany rozwiazane) albo
    /// biezacy SubClanBehavior._subClan. Bez Spoils nic sie nie wpina. Stan: tylko liczniki do linii dnia (bez zapisu).
    /// </summary>
    internal static class SpoilsCompany
    {
        private const string ClanPrefix = "rl_merc_";

        private static Type _tSub;
        private static PropertyInfo _pInstance;
        private static FieldInfo _fSubClan, _fLeader;
        private static bool _present;
        private static Clan _clanCache;                    // biezacy klan Spoils (odswiezany linia dnia i opakowaniami)
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();

        internal static bool On { get { var s = Settings.Current; return s != null && s.SpoilsClanRealSoldiers; } }

        // liczniki od poprzedniej linii dnia; [0] zablokowane (wlacznik TAK), [1] przepuszczone (wlacznik NIE - dane z niczego)
        private static readonly int[] _tplParties = new int[2], _tplMen = new int[2], _topup = new int[2], _mapTimes = new int[2], _mapMen = new int[2], _purse = new int[2];
        private static int _spawnSpoils, _spawnGame, _extraMen, _ships, _purseEvents, _refund, _recNotable, _recTavern, _recCost;
        private static int _bkSkipped, _bkLet, _bkHeroesOut;   // zamiana BK: zablokowana / przepuszczona (wlacznik NIE) i ilu bohaterow wypadlo przy tym z druzyn
        private static int _stumbles, _stumblesDay;
        private static int _lastMen = -1;
        private static string _lastClanId;

        // wywolania zagniezdzone: SpawnLordParty (szablon w srodku to droga Spoils) i opakowania z komunikatem dla gracza
        [ThreadStatic] private static int _inSpawn;
        [ThreadStatic] private static int _wrap;
        private static string _pending;                    // komunikat z SpawnLordParty czekajacy na opakowanie ({GOLD} = kiesa _pendingHero przy wypisaniu)
        private static Hero _pendingHero;
        private static MobileParty _lastTplParty;          // ostatnia druzyna, ktorej szablon liczyl TemplatePostfix
        private static int _lastTplMen, _lastTplLeft;      // ilu ludzi dal jej szablon i ilu z nich w niej zostalo (wlacznik NIE)

        /// <summary>Nowa gra albo wczytanie (wolane z SpoilsSeal.Reset - konstruktor ArmouryBehavior): liczniki od zera.</summary>
        internal static void Reset()
        {
            ClearDay();
            _stumbles = 0; _lastMen = -1; _lastClanId = null; _clanCache = null;
            _inSpawn = 0; _wrap = 0; _pending = null; _pendingHero = null; _lastTplParty = null; _lastTplMen = _lastTplLeft = 0;
        }

        private static void ClearDay()
        {
            Array.Clear(_tplParties, 0, 2); Array.Clear(_tplMen, 0, 2); Array.Clear(_topup, 0, 2);
            Array.Clear(_mapTimes, 0, 2); Array.Clear(_mapMen, 0, 2); Array.Clear(_purse, 0, 2);
            _spawnSpoils = _spawnGame = _extraMen = _ships = _purseEvents = _refund = _recNotable = _recTavern = _recCost = 0;
            _bkSkipped = _bkLet = _bkHeroesOut = 0;
            _stumblesDay = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_stumbles <= 3) Log.Error(where, e);
        }

        private static int MainGold() { var h = Hero.MainHero; return h != null ? h.Gold : 0; }

        private static object Sub() { try { return _pInstance != null ? _pInstance.GetValue(null, null) : null; } catch { return null; } }

        private static Clan CurrentClan()
        {
            var s = Sub();
            try { var c = s != null && _fSubClan != null ? _fSubClan.GetValue(s) as Clan : null; if (c != null) _clanCache = c; return c; } catch { return null; }
        }

        private static Hero LeaderOf(object sub) { try { return sub != null && _fLeader != null ? _fLeader.GetValue(sub) as Hero : null; } catch { return null; } }

        /// <summary>Klan najemnikow Spoils (biezacy albo dawny, rozwiazany - kazdy "rl_merc_...").</summary>
        internal static bool IsSpoilsClan(Clan c)
        {
            if (c == null || !_present) return false;
            try
            {
                var id = c.StringId;
                if (id != null && id.StartsWith(ClanPrefix, StringComparison.Ordinal)) return true;
                return c == _clanCache;
            }
            catch { return false; }
        }

        /// <summary>Druzyna, ktora bohater prowadzi (czynna) - null, gdy zadnej.</summary>
        private static MobileParty LedBy(Hero h)
        {
            try
            {
                var p = h != null ? h.PartyBelongedTo : null;
                return p != null && p.IsActive && p.LeaderHero == h ? p : null;
            }
            catch { return null; }
        }

        private static int ShipCount(MobileParty p) { try { var s = p != null ? p.Ships : null; return s != null ? s.Count : 0; } catch { return 0; } }

        private static Dictionary<CharacterObject, int> Regulars(TroopRoster r)
        {
            var d = new Dictionary<CharacterObject, int>();
            if (r == null) return d;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.Character == null || el.Character.IsHero || el.Number <= 0) continue;
                int v; d.TryGetValue(el.Character, out v); d[el.Character] = v + el.Number;
            }
            return d;
        }

        /// <summary>Ludzie (bez bohaterow) w druzynie ponad stan "before" (null = ponad zero); remove = zdejmij ich z druzyny.
        /// Zwraca ich liczbe. Liczone z kopii skladu (Regulars), potem zdejmowane typ po typie - zmiana rostera nie psuje petli.</summary>
        private static int StripNew(MobileParty p, Dictionary<CharacterObject, int> before, bool remove)
        {
            var r = p != null ? p.MemberRoster : null;
            if (r == null) return 0;
            var now = Regulars(r);
            int total = 0;
            foreach (var kv in now)
            {
                int was = 0;
                if (before != null) before.TryGetValue(kv.Key, out was);
                int n = kv.Value - was;
                if (n <= 0) continue;
                total += n;
                if (remove) r.AddToCounts(kv.Key, -n);
            }
            return total;
        }

        private static string Nm(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "?"; } catch { return "?"; } }
        private static string Nm(Clan c) { try { return c != null && c.Name != null ? c.Name.ToString() : "?"; } catch { return "?"; } }

        private static void Say(string text)
        {
            Log.Player(text);
            Log.Info("SpoilsCompany: komunikat dla gracza: " + text);
        }

        // ------------------------------------------------------------ 2. szablon przy powstaniu druzyny lorda (gra)

        /// <summary>Prefiks InitializeLordPartyProperties(druzyna, wlasciciel): przy klanie Spoils zapamietuje sklad (ludzie bez
        /// bohaterow, zwykle pusto) i statki przed wypelnieniem z szablonu. Inne klany - nic (jedno porownanie).</summary>
        public static void TemplatePrefix(MobileParty __0, Hero __1, out object __state)
        {
            __state = null;
            try
            {
                if (__0 == null || __1 == null || !IsSpoilsClan(__1.Clan)) return;
                __state = new object[] { Regulars(__0.MemberRoster), ShipCount(__0) };
            }
            catch (Exception e) { Stumble("SpoilsCompany.Template", e); }
        }

        /// <summary>Postfiks: ludzie, ktorych gra wlozyla z szablonu kultury - przy wlaczniku zdjeci (druzyna zostaje z przywodca);
        /// statki z szablonu tylko liczone (szablony lordow ROT ich nie maja).</summary>
        public static void TemplatePostfix(MobileParty __0, Hero __1, object __state)
        {
            var st = __state as object[];
            if (st == null) return;
            try
            {
                bool on = On;
                int men = StripNew(__0, (Dictionary<CharacterObject, int>)st[0], on);
                int b = on ? 0 : 1;
                _tplParties[b]++; _tplMen[b] += men;
                _ships += Math.Max(0, ShipCount(__0) - (int)st[1]);
                _lastTplParty = __0; _lastTplMen = men; _lastTplLeft = on ? 0 : men;
                if (_inSpawn == 0)
                {
                    _spawnGame++;
                    if (on && men > 0 && __1.Clan == CurrentClan())      // komunikat tylko dla biezacego klanu (dawne, rozwiazane - tylko log)
                        Say("Armoury: " + Nm(__1) + " of \"" + Nm(__1.Clan) + "\" takes the field again with no soldiers out of thin air - "
                            + men + " men of a ready-made warband were not handed out; the company recruits real men with its own gold.");
                }
            }
            catch (Exception e) { Stumble("SpoilsCompany.Template", e); }
        }

        // ------------------------------------------------------------ 1. dosypka Spoils (SubClanBehavior.SpawnLordParty)

        /// <summary>Prefiks SpawnLordParty(przywodca, ile, nazwa): zapamietuje druzyne, ktora przywodca juz prowadzi (Spoils wtedy nic
        /// nie tworzy), i przy wlaczniku ustawia "ile" na 0 - Spoils nie dosypuje (liczy ile + 1 - stan, a stan to sam przywodca).</summary>
        public static void SpawnPrefix(Hero __0, ref int __1, out object __state)
        {
            __state = null;
            _inSpawn++;
            try
            {
                __state = new object[] { __1, LedBy(__0) };
                if (On && __1 > 0) __1 = 0;
            }
            catch (Exception e) { Stumble("SpoilsCompany.Spawn", e); }
        }

        /// <summary>Postfiks: gdy Spoils stworzyl druzyne - przy wlaczniku zdejmuje z niej wszystkich ludzi bez bohaterow, ktorzy jeszcze
        /// w niej sa (druzyna sprzed chwili, kazdy czlowiek w niej pochodzi z niczego; po szablonie i dosypce 0 zwykle nikt), i zostawia
        /// komunikat dla gracza. Przy wylaczonym liczy dosypke Spoils (ludzie ponad to, co dal szablon).</summary>
        public static void SpawnPostfix(Hero __0, object __state)
        {
            var st = __state as object[];
            if (st == null || __0 == null) return;
            try
            {
                var p = LedBy(__0);
                if (p == null || p == st[1] as MobileParty) return;
                _spawnSpoils++;
                int want = Math.Max(0, (int)st[0]);
                bool on = On;
                int regs = StripNew(p, null, false);
                bool mine = p == _lastTplParty;
                int tpl = mine ? _lastTplMen : 0, tplLeft = mine ? _lastTplLeft : 0;
                int extra = Math.Max(0, regs - tplLeft);
                if (on)
                {
                    int topup = Math.Max(0, want - tpl);           // Spoils dosypuje do "ile + 1" ponad to, co dal szablon
                    _topup[0] += topup;
                    if (extra > 0) { StripNew(p, null, true); _extraMen += extra; }
                    string msg = Nm(__0) + " of \"" + Nm(__0.Clan) + "\" takes the field alone - no soldiers out of thin air (" + (tpl + topup + extra)
                                 + " men would have appeared from nowhere); the company recruits real men from notables and taverns with its own gold ({GOLD} den.).";
                    _pending = msg; _pendingHero = __0;
                    if (_wrap == 0) Flush(null);
                }
                else _topup[1] += extra;
            }
            catch (Exception e) { Stumble("SpoilsCompany.Spawn", e); }
        }

        public static Exception SpawnFinalizer(Exception __exception) { if (_inSpawn > 0) _inSpawn--; return __exception; }

        // ------------------------------------------------------------ 3. ochotnicy z mapy; prawdziwy werbunek (licznik)

        /// <summary>Prefiks RecruitmentCampaignBehavior.ApplyInternal(druzyna, osada, notabl, typ, ile, miejsce, rodzaj): "ochotnik z mapy"
        /// (bez osady, bez notabla) dla klanu Spoils przy wlaczniku nie zachodzi - ani czlowieka, ani zaplaty do nikad, ani zdarzenia.</summary>
        public static bool RecruitPrefix(MobileParty __0, int __4, object __6)
        {
            try
            {
                if (__0 == null || __6 == null || !IsSpoilsClan(__0.ActualClan) || __6.ToString() != "VolunteerFromMap") return true;
                int b = On ? 0 : 1;
                _mapTimes[b]++; _mapMen[b] += Math.Max(0, __4);
                return b == 1;
            }
            catch (Exception e) { Stumble("SpoilsCompany.Recruit", e); return true; }
        }

        /// <summary>Postfiks: prawdziwy werbunek klanu Spoils (od notabla, z karczmy) - tylko licznik i koszt dla linii dnia.</summary>
        public static void RecruitPostfix(MobileParty __0, CharacterObject __3, int __4, object __6)
        {
            try
            {
                if (__0 == null || __6 == null || !IsSpoilsClan(__0.ActualClan)) return;
                string d = __6.ToString();
                int n = Math.Max(1, __4);
                if (d == "VolunteerFromIndividual") _recNotable += n;
                else if (d == "MercenaryFromTavern") _recTavern += n;
                else return;
                if (__3 != null) _recCost += Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(__3, __0.LeaderHero).RoundedResultNumber * n;
            }
            catch (Exception e) { Stumble("SpoilsCompany.Recruit", e); }
        }

        // ------------------------------------------------------------ 4. zaplata gracza za ludzi -> kiesa klanu; komunikaty

        private static Clan ClanOf(object sub) { try { return sub != null && _fSubClan != null ? _fSubClan.GetValue(sub) as Clan : null; } catch { return null; } }

        /// <summary>Stan opakowania: zloto gracza, bohater, ktorego wzrost zlota mierzymy (przelew Spoils), jego zloto przed metoda,
        /// bohaterowie klanu przed metoda (nowa druzyna: kapitan to ten, ktorego przedtem nie bylo).</summary>
        private static object[] PayState(Hero measured, Clan heroesOf)
        {
            HashSet<Hero> set = null;
            if (heroesOf != null)
            {
                set = new HashSet<Hero>();
                if (heroesOf.Heroes != null) foreach (var h in heroesOf.Heroes) if (h != null) set.Add(h);
            }
            return new object[] { MainGold(), measured, measured != null ? measured.Gold : 0, set };
        }

        /// <summary>Zalozenie (CreateMercenaryClan(przywodca, nazwa, towarzysz)): mierzony i odbiorca = przywodca, o ile Spoils doprowadzil
        /// zalozenie do konca (SubClanBehavior._leader == przywodca); inaczej reszta wraca do gracza.</summary>
        public static void FoundPrefix(Hero __0, out object __state) { _wrap++; _pending = null; __state = null; try { __state = PayState(__0, null); } catch (Exception e) { Stumble("SpoilsCompany.Found", e); } }
        public static void FoundPostfix(object __instance, Hero __0, object __state)
        {
            try
            {
                bool done = __0 != null && __0.IsAlive && LeaderOf(__instance) == __0;
                if (done) _clanCache = ClanOf(__instance) ?? _clanCache;
                Settle(__state as object[], __0, done ? __0 : null, "found");
            }
            catch (Exception e) { Stumble("SpoilsCompany.Found", e); }
        }

        /// <summary>Odnowienie (OnReformConsequence): mierzony i odbiorca = przywodca po metodzie (dawny albo nowy); bez przywodcy - zwrot.</summary>
        public static void ReformPrefix(object __instance, out object __state) { _wrap++; _pending = null; __state = null; try { __state = PayState(LeaderOf(__instance), null); } catch (Exception e) { Stumble("SpoilsCompany.Reform", e); } }
        public static void ReformPostfix(object __instance, object __state)
        {
            try { var l = LeaderOf(__instance); Settle(__state as object[], l, l != null && l.IsAlive ? l : null, "reform"); }
            catch (Exception e) { Stumble("SpoilsCompany.Reform", e); }
        }

        /// <summary>Nowa druzyna (CreateExtraParty(koszt)): mierzony i odbiorca = nowy kapitan (bohater klanu, ktorego przed metoda nie bylo).</summary>
        public static void ExtraPrefix(object __instance, out object __state) { _wrap++; _pending = null; __state = null; try { __state = PayState(null, ClanOf(__instance)); } catch (Exception e) { Stumble("SpoilsCompany.Extra", e); } }
        public static void ExtraPostfix(object __instance, object __state)
        {
            try
            {
                var st = __state as object[];
                Hero cap = null;
                var set = st != null ? st[3] as HashSet<Hero> : null;
                var c = ClanOf(__instance);
                if (set != null && c != null && c.Heroes != null)
                    foreach (var h in c.Heroes) if (h != null && !set.Contains(h)) { cap = h; break; }
                Settle(st, cap, cap != null && cap.IsAlive ? cap : null, "extra");
            }
            catch (Exception e) { Stumble("SpoilsCompany.Extra", e); }
        }

        /// <summary>Powrot przywodcy z niewoli (OnHeroPrisonerReleased): tylko komunikat po komunikacie Spoils.</summary>
        public static void ReleasedPrefix() { _wrap++; _pending = null; }
        public static void ReleasedPostfix() { try { Flush(null); } catch (Exception e) { Stumble("SpoilsCompany.Released", e); } }

        public static Exception WrapFinalizer(Exception __exception) { if (_wrap > 0) _wrap--; return __exception; }

        private static void Flush(string extra)
        {
            string msg = _pending;
            if (msg != null) msg = msg.Replace("{GOLD}", (_pendingHero != null ? _pendingHero.Gold : 0).ToString());
            _pending = null; _pendingHero = null;
            if (msg == null && extra == null) return;
            Say("Armoury: " + (msg ?? "") + (msg != null && extra != null ? " " : "") + (extra ?? ""));
        }

        /// <summary>Ile gracz zaplacil w tym wywolaniu (spadek jego zlota) minus to, co Spoils przelal mierzonemu bohaterowi (wzrost jego
        /// zlota; nowy bohater - cale jego zloto) = czesc, ktora u Spoils przepada. Przy wlaczniku idzie do odbiorcy, a bez odbiorcy
        /// (Spoils nie doprowadzil tworzenia do konca) wraca do gracza; to, co Spoils juz przelal, zostaje, gdzie jest; przy wylaczonym
        /// tylko licznik.</summary>
        private static void Settle(object[] st, Hero measured, Hero to, string kind)
        {
            if (st == null) { Flush(null); return; }
            int paid = (int)st[0] - MainGold();
            if (paid <= 0) { Flush(null); return; }
            int got = measured == null ? 0 : measured.Gold - (measured == st[1] as Hero ? (int)st[2] : 0);
            int rest = paid - Math.Max(0, got);
            if (rest <= 0) { Flush(null); return; }
            if (!On) { _purse[1] += rest; _purseEvents++; Flush(null); return; }
            if (to != null)
            {
                to.ChangeHeroGold(rest);
                _purse[0] += rest; _purseEvents++;
                Log.Info("SpoilsCompany: " + kind + " - gracz zaplacil " + paid + " zl, Spoils przelal " + got + ", reszta " + rest + " zl do kiesy " + Nm(to) + " (zamiast przepasc).");
                Flush("All " + paid + " den. you paid went to " + Nm(to) + " of \"" + Nm(to.Clan) + "\" to recruit with (purse now " + to.Gold + " den.); Spoils of War would have kept "
                      + rest + " den. of it for nothing.");
            }
            else
            {
                var me = Hero.MainHero;
                if (me != null) me.ChangeHeroGold(rest);
                _refund += rest;
                Log.Info("SpoilsCompany: " + kind + " - nie powstal nikt, kto by werbowal; zwrot graczowi " + rest + " zl.");
                Flush("Nobody took up the command - " + rest + " den. were returned to you.");
            }
        }

        // ------------------------------------------------------------ 5. BK: zamiana ludzi klanu najemnikow (BKClanBehavior.ConvertTroopsMercenaries)

        /// <summary>Prefiks BK ConvertTroopsMercenaries(klan): przy wlaczniku dla klanu Spoils nie zachodzi - przywodca zostaje w swojej
        /// druzynie, zwerbowani zostaja tymi, kogo zwerbowano, zolnierz z niczego nie powstaje. Inne klany i wlacznik NIE - BK jak dotad
        /// (przy NIE tylko pomiar: bohaterowie w druzynach klanu przed zamiana).</summary>
        public static bool BkConvertPrefix(Clan __0, out int __state)
        {
            __state = -1;
            try
            {
                if (__0 == null || !IsSpoilsClan(__0)) return true;
                if (On) { _bkSkipped++; return false; }
                __state = HeroesInParties(__0);
            }
            catch (Exception e) { Stumble("SpoilsCompany.BkConvert", e); }
            return true;
        }

        /// <summary>Postfiks (tylko wlacznik NIE): ilu bohaterow BK wyrzucil z druzyn klanu Spoils (kazdy zastapiony zolnierzem z niczego).</summary>
        public static void BkConvertPostfix(Clan __0, int __state)
        {
            if (__state < 0) return;
            try { _bkLet++; _bkHeroesOut += Math.Max(0, __state - HeroesInParties(__0)); }
            catch (Exception e) { Stumble("SpoilsCompany.BkConvert", e); }
        }

        private static int HeroesInParties(Clan c)
        {
            int n = 0;
            var w = c != null ? c.WarPartyComponents : null;
            if (w == null) return 0;
            foreach (var wp in w)
            {
                var mp = wp != null ? wp.MobileParty : null;
                if (mp != null && mp.MemberRoster != null) n += mp.MemberRoster.TotalHeroes;
            }
            return n;
        }

        // ------------------------------------------------------------ linia dnia

        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTick, po SpoilsSeal): stan klanu i co zablokowano / przepuszczono od poprzedniej
        /// linii. Tylko log.</summary>
        internal static void Daily()
        {
            if (!_present) return;
            try
            {
                bool on = On;
                int day = (int)CampaignTime.Now.ToDays - 1;
                var sb = new StringBuilder();
                sb.Append("Spoils - klan najemnikow: dzien ").Append(day).Append(" | wlacznik Spoils Clan Real Soldiers: ")
                  .Append(on ? "TAK - tylko prawdziwi zolnierze" : "NIE - Spoils i gra jak dotad, tu tylko pomiar");
                var sub = Sub();
                var clan = CurrentClan();
                int men = -1;
                if (clan == null) sb.Append(" | klanu brak (nie zalozony albo rozwiazany)");
                else
                {
                    var leader = LeaderOf(sub);
                    int parties = 0, regs = 0; men = 0;
                    foreach (var mp in MobileParty.All)
                    {
                        if (mp == null || !mp.IsActive || mp.ActualClan != clan || mp.MemberRoster == null) continue;
                        parties++; men += mp.MemberRoster.TotalManCount; regs += mp.MemberRoster.TotalRegulars;
                    }
                    string state = leader == null ? "brak" : !leader.IsAlive ? "martwy" : leader.IsPrisoner ? "w niewoli" : LedBy(leader) != null ? "prowadzi druzyne" : "bez druzyny";
                    sb.Append(" | klan \"").Append(Nm(clan)).Append("\" (").Append(clan.StringId).Append("): przywodca ").Append(Nm(leader)).Append(" (").Append(state)
                      .Append("), druzyn ").Append(parties).Append(", ludzi ").Append(men).Append(" (bez bohaterow ").Append(regs);
                    if (_lastMen >= 0 && _lastClanId == clan.StringId) sb.Append("; zmiana ").Append(men - _lastMen >= 0 ? "+" : "").Append(men - _lastMen);
                    sb.Append("), skarbiec (kiesa przywodcy) ").Append(leader != null ? leader.Gold : 0).Append(" zl");
                }
                int others = 0, otherMen = 0;
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || !mp.IsActive || mp.ActualClan == null || mp.ActualClan == clan || !IsSpoilsClan(mp.ActualClan)) continue;
                    others++; otherMen += mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0;
                }
                if (others > 0) sb.Append("; dawne klany Spoils: druzyn ").Append(others).Append(", ludzi ").Append(otherMen);
                sb.Append(" | od poprzedniej linii: nowe druzyny klanu ").Append(_spawnSpoils + _spawnGame).Append(" (przez Spoils ").Append(_spawnSpoils)
                  .Append(", przez gre ").Append(_spawnGame).Append(')');
                sb.Append("; ZABLOKOWANO z niczego: sklad z szablonu ").Append(_tplMen[0]).Append(" ludzi w ").Append(_tplParties[0]).Append(" druzynach, dosypka Spoils ponad szablon ")
                  .Append(_topup[0]).Append(", inne w druzynie Spoils sprzed chwili ").Append(_extraMen).Append(", ochotnicy z mapy ")
                  .Append(_mapTimes[0]).Append(" razy / ").Append(_mapMen[0]).Append(" ludzi, zamiana BK (przywodca za zolnierza z niczego) ").Append(_bkSkipped).Append(" razy");
                sb.Append("; DALO Z NICZEGO (wlacznik NIE): szablon ").Append(_tplMen[1]).Append(" ludzi w ").Append(_tplParties[1]).Append(" druzynach, dosypka Spoils ")
                  .Append(_topup[1]).Append(", ochotnicy z mapy ").Append(_mapTimes[1]).Append(" razy / ").Append(_mapMen[1]).Append(" ludzi, zamiana BK ").Append(_bkLet)
                  .Append(" razy (bohaterow wyrzuconych z druzyn, kazdy za zolnierza z niczego: ").Append(_bkHeroesOut).Append(')');
                sb.Append("; statki z szablonu ").Append(_ships).Append(" (zostaja - szablony lordow ROT statkow nie maja)");
                sb.Append("; prawdziwy werbunek klanu: od notabli ").Append(_recNotable).Append(", z karczmy ").Append(_recTavern).Append(" (koszt ").Append(_recCost).Append(" zl z kiesy klanu)");
                sb.Append("; zaplata gracza za ludzi: do kiesy klanu ").Append(_purse[0]).Append(" zl, przepadlo u Spoils (wlacznik NIE) ").Append(_purse[1])
                  .Append(" zl (zdarzen ").Append(_purseEvents).Append("), zwrot graczowi ").Append(_refund).Append(" zl");
                if (men >= 0 && _lastMen >= 0 && clan != null && _lastClanId == clan.StringId)
                {
                    int known = _recNotable + _recTavern + _tplMen[1] + _topup[1] + _mapMen[1];
                    sb.Append("; reszta zmiany ludzi (jency wcieleni, polegli, dezercja, rozbici, przywodcy wracajacy do druzyn) ").Append(men - _lastMen - known);
                }
                sb.Append(" | potkniecia dzis ").Append(_stumblesDay).Append(" (od wczytania ").Append(_stumbles).Append(").");
                Log.Info(sb.ToString());
                _lastMen = men; _lastClanId = clan != null ? clan.StringId : null;
            }
            catch (Exception e) { Log.Error("SpoilsCompany.Daily", e); }
            finally { ClearDay(); }
        }

        // ------------------------------------------------------------ wpiecie

        private static void Wire(Harmony h, MethodBase m, string label, string prefix, string postfix, string finalizer)
        {
            if (m == null) { _missing.Add(label); return; }
            h.Patch(m, prefix: prefix != null ? new HarmonyMethod(typeof(SpoilsCompany), prefix) : null,
                       postfix: postfix != null ? new HarmonyMethod(typeof(SpoilsCompany), postfix) : null,
                       finalizer: finalizer != null ? new HarmonyMethod(typeof(SpoilsCompany), finalizer) : null);
            _wired.Add(label);
        }

        private static MethodInfo Exact(Type t, string name, params Type[] args)
        {
            if (t == null) return null;
            var m = AccessTools.Method(t, name, args);
            return m != null && m.DeclaringType == t ? m : null;
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                _wired.Clear(); _missing.Clear();
                _tSub = QuartermasterLaw.FindType("RealisticLoot.Behaviors.SubClanBehavior");
                if (_tSub == null) { Log.Info("SpoilsCompany: Spoils of War (klan najemnikow) nieobecny - nic do zrobienia."); return; }
                _present = true;
                _pInstance = AccessTools.Property(_tSub, "Instance");
                _fSubClan = AccessTools.Field(_tSub, "_subClan");
                _fLeader = AccessTools.Field(_tSub, "_leader");
                if (_pInstance == null || _fSubClan == null || _fLeader == null) _missing.Add("pola SubClanBehavior (Instance/_subClan/_leader) - linia dnia bez stanu klanu");

                Wire(h, Exact(typeof(LordPartyComponent.InitializationArgs), "InitializeLordPartyProperties", typeof(MobileParty), typeof(Hero)),
                     "sklad z szablonu przy powstaniu druzyny (gra)", "TemplatePrefix", "TemplatePostfix", null);
                Wire(h, Exact(_tSub, "SpawnLordParty", typeof(Hero), typeof(int), typeof(string)),
                     "dosypka Spoils (SpawnLordParty)", "SpawnPrefix", "SpawnPostfix", "SpawnFinalizer");
                var ai = AccessTools.Method(typeof(RecruitmentCampaignBehavior), "ApplyInternal");
                var ps = ai != null ? ai.GetParameters() : null;
                if (ps == null || ps.Length != 7 || ps[0].ParameterType != typeof(MobileParty) || ps[3].ParameterType != typeof(CharacterObject) || ps[4].ParameterType != typeof(int) || !ps[6].ParameterType.IsEnum) ai = null;
                Wire(h, ai, "ochotnicy z mapy / licznik werbunku (ApplyInternal)", "RecruitPrefix", "RecruitPostfix", null);
                Wire(h, Exact(_tSub, "CreateMercenaryClan", typeof(Hero), typeof(string), typeof(bool)), "zalozenie - zaplata do kiesy klanu", "FoundPrefix", "FoundPostfix", "WrapFinalizer");
                Wire(h, Exact(_tSub, "OnReformConsequence", typeof(MenuCallbackArgs)), "odnowienie - zaplata do kiesy klanu", "ReformPrefix", "ReformPostfix", "WrapFinalizer");
                Wire(h, Exact(_tSub, "CreateExtraParty", typeof(int)), "nowa druzyna - zaplata do kiesy kapitana", "ExtraPrefix", "ExtraPostfix", "WrapFinalizer");
                var rel = _tSub.GetMethod("OnHeroPrisonerReleased", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                Wire(h, rel, "powrot z niewoli - komunikat", "ReleasedPrefix", "ReleasedPostfix", "WrapFinalizer");
                // BK laduje sie przed Armoury (SubModule.xml: BannerKings.Redux LoadBeforeThis); bez BK nie ma czego latac (nie "BRAK")
                var tBk = QuartermasterLaw.FindType("BannerKings.Behaviours.BKClanBehavior");
                if (tBk != null)
                    Wire(h, Exact(tBk, "ConvertTroopsMercenaries", typeof(Clan)), "zamiana BK przywodcy i ludzi klanu (ConvertTroopsMercenaries)", "BkConvertPrefix", "BkConvertPostfix", null);
                else Log.Info("SpoilsCompany: BannerKings nieobecny - zamiany BK nie ma, nic do latania.");

                Log.Info("SpoilsCompany: Spoils of War - klan najemnikow tylko z prawdziwych zolnierzy (wlacznik Spoils Clan Real Soldiers "
                         + (On ? "TAK" : "NIE - tylko pomiar") + "); wpiete: " + string.Join(", ", _wired.ToArray())
                         + (_missing.Count > 0 ? " | BRAK (te drogi BEZ ZMIAN - sprawdzic dekompilacje): " + string.Join(", ", _missing.ToArray()) : " | wszystkie drogi wpiete")
                         + "; liczby - linia dnia \"Spoils - klan najemnikow\".");
            }
            catch (Exception e) { Log.Error("SpoilsCompany.ApplyAll", e); }
        }
    }
}
