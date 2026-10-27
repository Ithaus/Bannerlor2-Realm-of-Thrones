using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;

namespace Armoury
{
    /// <summary>
    /// M6a-1 (raport roczny 13.10, tabela 6.2 wiersz 17; DECYZJA z pelnymi uprawnieniami): NAJKROTSZA SLUZBA KOMPANII NAJEMNEJ AI. Klucz MercMinService.
    /// Bieg roczny: 11 kompanii, 762 nowe umowy 185, mediana sluzby 3 doby, 149 sluzb jednodniowych, 11 razy prosto do wroga poprzedniego pana; w 351 z 364
    /// dob wszystkie 11 w sluzbie - kompania przechodzi z korony do korony bez przerwy.
    /// Kto zrywa (dekompilacja):
    ///  - GRA, DiplomaticBartersBehavior.DailyTickClan: galaz "najem" (szansa 0.4) dla kompanii W SLUZBIE losuje inne krolestwo i wola
    ///    ConsiderClanJoinAsMercenary (zmiana pana wprost, ApplyByJoinFactionAsMercenary z default(CampaignTime) -> ShouldStayInKingdomUntil = 0);
    ///    galaz "odejscie" wola ConsiderClanLeaveAsMercenary. Obie tylko przy clan.ShouldStayInKingdomUntil.IsPast. Wyjscie z dlugu wladcy
    ///    (ClanVariablesCampaignBehavior.DailyTickClan, DebtToKingdom > 10 000) - tez tylko przy IsPast. Wrogowi obecnego pana gra kompanii nie oddaje
    ///    (clan.MapFaction.IsAtWarWith(kingdom) -> return), ale kompania bez pana (po odejsciu) moze sie od razu najac u wroga poprzedniego.
    ///  - BK: wladca raz w tygodniu (BKMercenaryCareerBehavior.OnClanDailyTickImpl, wynik najmu < 0) -> MercenaryCareer.CheckFiring: zwolnienie albo
    ///    LeaveForEnemy (FireMercenary + ApplyByJoinFactionAsMercenary u WROGA). BK nie patrzy na ShouldStayInKingdomUntil.
    ///  - nasz 185 (MercContract): odejscie po 28 dobach niedoplaty ponad polowe; upadek krolestwa (ApplyByLeaveByKingdomDestruction); Diplomacy - tylko
    ///    rozwiazanie krolestwa buntownikow (SoftlyDestroyKingdomAction). ROT - tylko gracz.
    /// Regula (tylko kompanie AI; gracz-najemnik bez zmian; krolestwo gracza - jego wlasne zwolnienia bez zmian):
    ///  1. Kompania AI w sluzbie krolestwa W WOJNIE nie odchodzi i nie zmienia pana przed uplywem MercMinServiceDays (28) dob od najmu: gra - przez jej
    ///     wlasne pole Clan.ShouldStayInKingdomUntil (= najem + 28 dob; zapisywane przez gre, bez nowego pola), BK - prefiks CheckFiring (wladca nie
    ///     zwalnia). Wyjatki: pracodawca bez wojny z zadnym krolestwem (pokoj - pole zerowane, BK moze zwolnic), niedoplata 185, upadek krolestwa.
    ///  2. Przez MercMinServiceDays dob od odejscia od pana P kompania AI nie najmuje sie u krolestwa w wojnie z P: prefiks ConsiderClanJoinAsMercenary
    ///     (gra) i LeaveForEnemy (BK: zostaje samo zwolnienie FireMercenary, bez przejscia do wroga i bez kar relacji za to przejscie).
    /// Klucz wylaczony = stan 4d33042: pole ShouldStayInKingdomUntil kompanii AI wyzerowane w najblizszej dobie (gra stawia AI zawsze 0), latki przepuszczaja.
    /// Zloto: bez zmian (zwolnienie BK placi jak dotad - wladca -> kompania). Zapis: pan sprzed odejscia i doba (segment 1 klucza "arm_m6a").
    /// Linia "Najemnicy - sluzba (M6a-1)" (licznik doby i sesji).
    /// </summary>
    internal static class MercTenure
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.MercMinService; } }
        private static int Days { get { var s = Settings.Current; return s != null ? Math.Max(1, Math.Min(364, s.MercMinServiceDays)) : 28; } }

        private sealed class Prev { public string Kingdom; public int Day; }
        private static readonly Dictionary<string, Prev> _prev = new Dictionary<string, Prev>();   // id kompanii -> pan sprzed odejscia
        private static readonly Dictionary<string, int> _joined = new Dictionary<string, int>();  // id kompanii -> doba najmu (pamiec; po wczytaniu z LastFactionChangeTime)

        // liczniki doby i sesji (linia M6a-1)
        private static int _dHire, _dEnd, _dShortOk, _dShortBad, _dShortUnknown, _dToEnemy, _dToEnemyPlayer, _dBlockJoin, _dBlockFire, _dBlockEnemyBk, _dArmed, _dFreed;
        private static int _sHire, _sEnd, _sShortBad, _sToEnemy, _sBlockJoin, _sBlockFire, _sBlockEnemyBk;
        private static int _sUnion;   // 272: kompanie przeniesione unia Dorzecza z Polnoca (ta sama sluzba)
        private static long _sServiceDays; private static int _sServiceN;
        private static readonly int[] _dWhy = new int[5];   // krotka sluzba z wyjatkiem: 0 pokoj u pracodawcy, 1 niedoplata 185, 2 upadek krolestwa, 3 zwolnienie BK w pokoju, 4 krolestwo gracza
        private static int _stumbles, _importN = -1, _importBad;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _inBkFiring;
        internal static bool UnpaidLeave;   // MercContract: odejscie po niedoplacie 185 (ustawiane na czas ApplyByLeaveKingdomAsMercenary)
        // recenzja M6a: krotka sluzba bez wyjatku rozstrzygana dopiero w linii doby - Diplomacy SoftlyDestroyKingdomAction wypuszcza kompanie
        // (LeaveAsMercenary) z krolestwa buntownikow, ktore jest jeszcze w wojnie i nie upadlo, a DestroyKingdomAction wola zaraz potem; w chwili
        // zdarzenia nie da sie tego odroznic od zerwania sluzby (falszywe NIE w M6a-1a). Tylko licznik - zachowanie gry bez zmian.
        private static readonly List<string> _pendBad = new List<string>();   // id krolestwa starego pana dla kazdej krotkiej sluzby bez wyjatku z tej doby

        internal static void Reset()
        {
            _prev.Clear(); _joined.Clear(); _pendBad.Clear(); ZeroDay();
            _sHire = _sEnd = _sShortBad = _sToEnemy = _sBlockJoin = _sBlockFire = _sBlockEnemyBk = 0; _sUnion = 0; _sServiceDays = 0; _sServiceN = 0;
            _stumbles = 0; _err.Clear(); _importN = -1; _importBad = 0; _inBkFiring = false; UnpaidLeave = false;
        }

        private static void ZeroDay()
        {
            _dHire = _dEnd = _dShortOk = _dShortBad = _dShortUnknown = _dToEnemy = _dToEnemyPlayer = _dBlockJoin = _dBlockFire = _dBlockEnemyBk = _dArmed = _dFreed = 0;
            Array.Clear(_dWhy, 0, _dWhy.Length);
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("MercTenure." + where, e); } catch { }
        }

        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }

        private static bool AiMerc(Clan c)
        {
            return c != null && c != Clan.PlayerClan && c.IsUnderMercenaryService && !c.IsEliminated && c.Kingdom != null && c.StringId != null;
        }

        /// <summary>Krolestwo w wojnie z jakimkolwiek krolestwem (ta sama regula co zwrot zoldu: KingdomTreasury.AtWar).</summary>
        private static bool AtWar(Kingdom k) { return k != null && !k.IsEliminated && KingdomTreasury.AtWar(k); }

        private static Kingdom KingdomById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k;
            return null;
        }

        /// <summary>Doba najmu kompanii w sluzbie (pamiec, a bez wpisu - pole gry LastFactionChangeTime: kazda zmiana krolestwa je ustawia).</summary>
        private static int JoinedDay(Clan c)
        {
            int d;
            if (c.StringId != null && _joined.TryGetValue(c.StringId, out d)) return d;
            d = (int)c.LastFactionChangeTime.ToDays;
            if (c.StringId != null) _joined[c.StringId] = d;
            return d;
        }

        /// <summary>Kompania AI w sluzbie krolestwa w wojnie i przed uplywem najkrotszej sluzby (regula 1).</summary>
        private static bool Bound(Clan c)
        {
            return On && AiMerc(c) && AtWar(c.Kingdom) && Today - JoinedDay(c) < Days;
        }

        /// <summary>Najem u krolestwa k zabroniony regula 2: przed uplywem Days dob od odejscia od pana P, k w wojnie z P.</summary>
        private static bool EnemyOfPrev(Clan c, Kingdom k, out Prev p)
        {
            p = null;
            if (c == null || k == null || c.StringId == null || !_prev.TryGetValue(c.StringId, out p) || p == null) return false;
            if (Today - p.Day >= Days) return false;
            var pk = KingdomById(p.Kingdom);
            return pk != null && pk != k && !pk.IsEliminated && k.IsAtWarWith(pk);
        }

        // ------------------------------------------------------------ zdarzenie gry: zmiana krolestwa (pomiar zawsze; pole gry - tylko przy kluczu)
        internal static void OnClanChangedKingdom(Clan clan, Kingdom oldK, Kingdom newK, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool show)
        {
            try
            {
                if (clan == null || clan == Clan.PlayerClan || clan.StringId == null) return;
                bool join = detail == ChangeKingdomAction.ChangeKingdomActionDetail.JoinAsMercenary;
                bool leave = detail == ChangeKingdomAction.ChangeKingdomActionDetail.LeaveAsMercenary;
                bool ruin = detail == ChangeKingdomAction.ChangeKingdomActionDetail.LeaveByKingdomDestruction;
                int today = Today, jd;
                bool wasMerc = _joined.TryGetValue(clan.StringId, out jd);
                // 272 (unia Dorzecza z Polnoca, KingInTheNorth.Moving): ta sama sluzba pod nowa korona - bez konca sluzby, bez nowego najmu, doba najmu bez zmian
                // (pole ShouldStayInKingdomUntil przywraca unia z migawki; przeglad doby liczy dalej od pierwszego najmu)
                if (KingInTheNorth.Moving && join && wasMerc) { _sUnion++; return; }
                // koniec sluzby u starego pana: odejscie, zmiana pana wprost (najem z innego krolestwa), upadek krolestwa
                if (oldK != null && oldK != newK && (leave || join || (ruin && wasMerc)))
                {
                    _dEnd++; _sEnd++;
                    if (wasMerc)
                    {
                        int len = today - jd;
                        _sServiceDays += len; _sServiceN++;
                        if (len < Days)
                        {
                            // wyjatki: upadek krolestwa, niedoplata 185, pracodawca bez wojny (zwolnienie BK w pokoju osobno), krolestwo gracza (jego zwolnienie)
                            int why = ruin || oldK.IsEliminated ? 2 : UnpaidLeave ? 1 : !AtWar(oldK) ? (_inBkFiring ? 3 : 0) : oldK.RulingClan == Clan.PlayerClan ? 4 : -1;
                            if (why >= 0) { _dShortOk++; _dWhy[why]++; }
                            else _pendBad.Add(oldK.StringId ?? "");   // recenzja M6a: rozstrzygniecie w linii doby (upadek krolestwa tuz po odejsciu = wyjatek 2)
                        }
                    }
                    else _dShortUnknown++;
                    _prev[clan.StringId] = new Prev { Kingdom = oldK.StringId, Day = today };
                    _joined.Remove(clan.StringId);
                }
                if (join && newK != null && clan.IsUnderMercenaryService)
                {
                    _dHire++; _sHire++;
                    Prev p;
                    if (EnemyOfPrev(clan, newK, out p))
                    {
                        if (newK.Leader == Hero.MainHero) _dToEnemyPlayer++;   // najem przez gracza-krola (jego decyzja) - osobno
                        else { _dToEnemy++; _sToEnemy++; }
                    }
                    _joined[clan.StringId] = today;
                    // regula 1: pole gry od razu przy najmie u krolestwa w wojnie (dalej - przeglad doby)
                    if (On && AtWar(newK))
                    {
                        var until = CampaignTime.Now + CampaignTime.Days(Days);
                        if (clan.ShouldStayInKingdomUntil < until) { clan.ShouldStayInKingdomUntil = until; _dArmed++; }
                    }
                }
            }
            catch (Exception e) { Stumble("OnClanChangedKingdom", e); }
        }

        // ------------------------------------------------------------ przeglad doby: pole ShouldStayInKingdomUntil kompanii AI
        /// <summary>Przeglad pola gry wszystkich kompanii AI w sluzbie (doba, wypowiedzenie wojny, pokoj - wojna i pokoj pracodawcy dzialaja od razu).</summary>
        private static void Sweep(out int inService, out int bound, out int peace)
        {
            int today = Today; inService = 0; bound = 0; peace = 0;
            foreach (var c in Clan.All)
            {
                try
                {
                    if (!AiMerc(c)) continue;
                    inService++;
                    int jd = JoinedDay(c);
                    bool war = AtWar(c.Kingdom);
                    if (On && !war && today - jd < Days) peace++;   // wyjatek "pokoj": pracodawca bez wojny
                    if (On && war && today - jd < Days)
                    {
                        bound++;
                        var until = CampaignTime.Days(jd) + CampaignTime.Days(Days);
                        if (c.ShouldStayInKingdomUntil < until) { c.ShouldStayInKingdomUntil = until; _dArmed++; }
                    }
                    else if (c.ShouldStayInKingdomUntil.IsFuture)
                    {
                        c.ShouldStayInKingdomUntil = CampaignTime.Zero; _dFreed++;   // pokoj u pracodawcy albo klucz wylaczony - jak w grze (AI: 0)
                    }
                }
                catch (Exception e) { Stumble("Sweep(rod)", e); }
            }
        }

        /// <summary>Zdarzenia gry WarDeclared i MakePeace: przeglad od razu (kompania najeta w pokoju jest zwiazana od wypowiedzenia wojny, nie od nastepnej doby).</summary>
        internal static void OnWarDeclared(IFaction a, IFaction b, DeclareWarAction.DeclareWarDetail d) { try { int x, y, z; if (Campaign.Current != null) Sweep(out x, out y, out z); } catch (Exception e) { Stumble("OnWarDeclared", e); } }
        internal static void OnMakePeace(IFaction a, IFaction b, MakePeaceAction.MakePeaceDetail d) { try { int x, y, z; if (Campaign.Current != null) Sweep(out x, out y, out z); } catch (Exception e) { Stumble("OnMakePeace", e); } }

        /// <summary>Klucz: kompania w sluzbie krolestwa w wojnie, przed uplywem Days od najmu - pole = najem + Days; pracodawca bez wojny - pole 0 (wyjatek
        /// "pokoj"). Klucz wylaczony - pole kompanii AI zerowane (stan gry: AI zawsze 0). Wpisy pana sprzed odejscia starsze niz Days - usuwane. Linia doby.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            int today = Today, inService, bound, peace;
            try
            {
                Sweep(out inService, out bound, out peace);
                // recenzja M6a: krotkie sluzby bez wyjatku z tej doby - krolestwo, ktore upadlo po odejsciu kompanii (Diplomacy: krolestwo buntownikow), to wyjatek 2
                foreach (var kid in _pendBad)
                {
                    var ok = KingdomById(kid);
                    if (ok == null || ok.IsEliminated) { _dShortOk++; _dWhy[2]++; }
                    else { _dShortBad++; _sShortBad++; }
                }
                _pendBad.Clear();
                var old = new List<string>();
                foreach (var kv in _prev) if (kv.Value == null || today - kv.Value.Day >= Days) old.Add(kv.Key);
                foreach (var id in old) _prev.Remove(id);
                if (s.LogEnabled)
                    Log.Info("Najemnicy - sluzba (M6a-1): dzien " + today + " | klucz Merc Min Service " + (On ? "tak" : "nie") + ", najkrotsza sluzba " + Days + " dob"
                             + " | kompanii AI w sluzbie " + inService + " (zwiazanych sluzba w wojnie " + bound + ", pracodawca bez wojny - zwolnione z reguly " + peace + ")"
                             + " | dzis: najmy " + _dHire + ", konce sluzby " + _dEnd + " (krotsze niz " + Days + " dob: bez wyjatku " + _dShortBad + ", z wyjatkiem " + _dShortOk
                             + " [pokoj u pracodawcy " + _dWhy[0] + ", niedoplata 185 " + _dWhy[1] + ", upadek krolestwa " + _dWhy[2] + ", zwolnienie BK w pokoju " + _dWhy[3]
                             + ", krolestwo gracza " + _dWhy[4] + "]"
                             + ", bez znanej doby najmu " + _dShortUnknown + ")"
                             + ", najem u wroga poprzedniego pana w " + Days + " dob " + _dToEnemy + " (przez gracza-krola " + _dToEnemyPlayer + ")"
                             + " | wstrzymane: najem gry u wroga poprzedniego pana " + _dBlockJoin + ", zwolnienie BK w sluzbie wojennej " + _dBlockFire
                             + ", przejscie BK do wroga (zostaje samo zwolnienie) " + _dBlockEnemyBk
                             + " | pole gry ShouldStayInKingdomUntil: ustawione " + _dArmed + ", wyzerowane " + _dFreed
                             + " | sesja: najmy " + _sHire + ", konce sluzby " + _sEnd + ", srednia sluzba " + (_sServiceN > 0 ? ((double)_sServiceDays / _sServiceN).ToString("0.0", Inv) : "-")
                             + " dob (z " + _sServiceN + "), krotkie bez wyjatku " + _sShortBad + ", najem u wroga poprzedniego pana " + _sToEnemy + (_sUnion > 0 ? ", przeniesione unia 272 (ta sama sluzba) " + _sUnion : "")
                             + ", wstrzymane (gra / BK zwolnienie / BK do wroga) " + _sBlockJoin + " / " + _sBlockFire + " / " + _sBlockEnemyBk
                             + " | latki " + _wired + (_stumbles > 0 ? " | potkniecia " + _stumbles : "")
                             + (_importN >= 0 ? " | wczytano wpisow " + _importN + " (bledne " + _importBad + ")" : "") + ".");
                _importN = -1;
            }
            catch (Exception e) { Stumble("Daily", e); }
            _pendBad.Clear();
            ZeroDay();   // liczniki od tej linii do nastepnej
        }

        // ------------------------------------------------------------ latki
        /// <summary>Prefiks DiplomaticBartersBehavior.ConsiderClanJoinAsMercenary(Clan, Kingdom): regula 2 (wrog poprzedniego pana) i - na wypadek pola gry
        /// wyzerowanego przez kogos innego - regula 1 dla kompanii w sluzbie. false = gra nie probuje najmu (barter nie startuje).</summary>
        public static bool JoinPrefix(Clan __0, Kingdom __1)
        {
            try
            {
                if (!On || __0 == null || __0 == Clan.PlayerClan || __1 == null) return true;
                Prev p;
                if (EnemyOfPrev(__0, __1, out p) || (__0.IsUnderMercenaryService && __0.Kingdom != __1 && Bound(__0)))
                {
                    _dBlockJoin++; _sBlockJoin++;
                    return false;
                }
                return true;
            }
            catch (Exception e) { Stumble("JoinPrefix", e); return true; }
        }

        private static PropertyInfo _pCareerClan;
        private static MethodInfo _mFire;

        private static Clan CareerClan(object career)
        {
            if (career == null) return null;
            if (_pCareerClan == null) _pCareerClan = AccessTools.Property(career.GetType(), "Clan");
            return _pCareerClan != null ? _pCareerClan.GetValue(career, null) as Clan : null;
        }

        /// <summary>Prefiks BK MercenaryCareer.CheckFiring(Kingdom): wladca AI nie zwalnia kompanii AI w sluzbie wojennej przed uplywem Days dob (regula 1).
        /// Krolestwo gracza i gracz-najemnik - BK jak dotad. Przepuszczone wywolanie znaczy flage (koniec sluzby liczony jako "zwolnienie BK").</summary>
        public static bool FirePrefix(object __instance, Kingdom __0)
        {
            try
            {
                var c = CareerClan(__instance);
                if (!On || c == null || c == Clan.PlayerClan || __0 == null || __0.RulingClan == Clan.PlayerClan || c.Kingdom != __0) { _inBkFiring = true; return true; }
                if (Bound(c)) { _dBlockFire++; _sBlockFire++; return false; }
                _inBkFiring = true;
                return true;
            }
            catch (Exception e) { Stumble("FirePrefix", e); _inBkFiring = true; return true; }
        }

        public static Exception FireFinalizer(Exception __exception) { _inBkFiring = false; return __exception; }

        /// <summary>Prefiks BK MercenaryCareer.LeaveForEnemy(Kingdom original, Kingdom enemy): kompania AI - samo zwolnienie (FireMercenary, jak w BK pierwszy
        /// krok), bez najmu u wroga (regula 2: przejscie od razu = 0 dob od odejscia). Gracz-najemnik - BK jak dotad. Brak metody - BK jak dotad.</summary>
        public static bool EnemyPrefix(object __instance, Kingdom __0, Kingdom __1)
        {
            try
            {
                if (!On || __instance == null) return true;
                var c = CareerClan(__instance);
                if (c == null || c == Clan.PlayerClan) return true;
                if (_mFire == null) _mFire = AccessTools.Method(__instance.GetType(), "FireMercenary", new[] { typeof(Kingdom) });
                if (_mFire == null) return true;
                _mFire.Invoke(__instance, new object[] { __0 });
                _dBlockEnemyBk++; _sBlockEnemyBk++;
                return false;
            }
            catch (Exception e) { Stumble("EnemyPrefix", e); return true; }
        }

        private static bool _hooksTried;
        private static string _wired = "-";
        private static Harmony _harmony;
        internal static void SetHarmony(Harmony h) { _harmony = h; }

        /// <summary>W kampanii (OnSessionLaunched) - przed pierwsza doba klanow; raz na proces. Do tego doby najmu kompanii w sluzbie z pola gry.</summary>
        internal static void EnsureHooks()
        {
            try
            {
                foreach (var c in Clan.All) if (AiMerc(c)) JoinedDay(c);
            }
            catch (Exception e) { Stumble("EnsureHooks(najmy)", e); }
            if (_hooksTried || _harmony == null || Campaign.Current == null) return;
            _hooksTried = true;
            string a = "BRAK", b = "BRAK", c2 = "BRAK";
            try
            {
                var m = AccessTools.Method(typeof(DiplomaticBartersBehavior), "ConsiderClanJoinAsMercenary", new[] { typeof(Clan), typeof(Kingdom) });
                if (m != null) { _harmony.Patch(m, prefix: new HarmonyMethod(typeof(MercTenure), nameof(JoinPrefix))); a = "wpiety"; }
            }
            catch (Exception e) { Log.Error("MercTenure.EnsureHooks(ConsiderClanJoinAsMercenary)", e); }
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Behaviours.Mercenary.MercenaryCareer");
                var m = t != null ? AccessTools.Method(t, "CheckFiring", new[] { typeof(Kingdom) }) : null;
                if (m != null) { _harmony.Patch(m, prefix: new HarmonyMethod(typeof(MercTenure), nameof(FirePrefix)), finalizer: new HarmonyMethod(typeof(MercTenure), nameof(FireFinalizer))); b = "wpiety"; }
                var m2 = t != null ? AccessTools.Method(t, "LeaveForEnemy", new[] { typeof(Kingdom), typeof(Kingdom) }) : null;
                if (m2 != null) { _harmony.Patch(m2, prefix: new HarmonyMethod(typeof(MercTenure), nameof(EnemyPrefix))); c2 = "wpiety"; }
            }
            catch (Exception e) { Log.Error("MercTenure.EnsureHooks(BK MercenaryCareer)", e); }
            _wired = "najem gry " + a + ", zwolnienie BK " + b + ", przejscie BK do wroga " + c2;
            Log.Info("Najemnicy - sluzba (M6a-1): latki - najem gry (DiplomaticBartersBehavior.ConsiderClanJoinAsMercenary) " + a
                     + ", zwolnienie BK (MercenaryCareer.CheckFiring) " + b + ", przejscie BK do wroga (MercenaryCareer.LeaveForEnemy) " + c2
                     + "; odejscie i zmiana pana w grze - pole gry ShouldStayInKingdomUntil; klucz Merc Min Service " + (On ? "tak" : "nie") + ".");
        }

        // ------------------------------------------------------------ zapis (segment 1 klucza "arm_m6a"): "idKompanii:idKrolestwa:doba;..."
        private static readonly char[] Bad = { '|', ';', ':' };

        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(_prev.Count * 32 + 4);
                bool first = true;
                foreach (var kv in _prev)
                {
                    var p = kv.Value;
                    if (p == null || kv.Key.IndexOfAny(Bad) >= 0 || p.Kingdom == null || p.Kingdom.IndexOfAny(Bad) >= 0) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(kv.Key).Append(':').Append(p.Kingdom).Append(':').Append(p.Day.ToString(Inv));
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            _prev.Clear(); _joined.Clear(); _importN = 0; _importBad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                foreach (var p in data.Split(';'))
                {
                    if (p.Length == 0) continue;
                    var x = p.Split(':'); int d;
                    if (x.Length == 3 && x[0].Length > 0 && x[1].Length > 0 && int.TryParse(x[2], NumberStyles.Integer, Inv, out d)) { _prev[x[0]] = new Prev { Kingdom = x[1], Day = d }; _importN++; }
                    else _importBad++;
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
