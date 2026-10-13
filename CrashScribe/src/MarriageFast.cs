using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace CrashScribe
{
    /// <summary>
    /// SWATY ROT SZYBCIEJ (Z.3 pozycja 1, 13.10; rozpoznanie docs/zakonczenie/ROZPOZNANIE-Z3-CUDZY-KOD-2026-10-13.md).
    /// Wynik gry ma zostac TEN SAM: te same pary, w tej samej dobie, ten sam ciag losowy co do wywolania.
    ///
    /// Oryginal (ROT 8.1.8, ROTRelationshipsBehavior.CheckNPCMarriages, sluchacz DailyTickClan; 448 bajtow IL):
    ///   dla kazdego lorda rodu z CanMarry(): kandydaci = _unmarriedHeroes.Where(h => model.IsCoupleSuitableForMarriage(lord, h));
    ///   GetRandomElementInefficiently (gra): IsEmpty (do pierwszego trafienia) + Count (cala lista) + ElementAt(MBRandom.RandomInt(Count))
    ///   - warunek pary liczony ok. 1.6 raza na kandydata; potem NpcCoupleMarriageChance, MBRandom.RandomFloat tylko gdy szansa > 0,
    ///   kontrola zalotow (stan >= 3) i MarriageAction.Apply + break.
    /// Lista _unmarriedHeroes: wszyscy zywi z CanMarry() spoza rodu gracza, budowana raz na dobe (DailyTick) - BK nie pyta o malzonka,
    /// wiec sa na niej takze zonaci i kazdy z nich sam szuka pary, choc szansa slubu wynosi u niego zawsze 0.
    ///
    /// Nasza droga (prefiks, oryginal pominiety) wola TE SAME metody modelu z tymi samymi argumentami, tylko rzadziej:
    ///  (a) jedno przejscie: trafienia do listy w kolejnosci _unmarriedHeroes, potem MBRandom.RandomInt(liczba) i element o tym numerze
    ///      - to samo wywolanie generatora i ten sam element co ElementAt (warunek pary jest czysta funkcja stanu - nizej);
    ///  (b) podzial wedlug plci: para tej samej plci to zawsze "nie" (DefaultMarriageModel.IsCoupleSuitableForMarriage; ROTMarriageModel
    ///      konczy na _previousModel, BK tej metody nie nadpisuje) - takich kandydatow nie pytamy wcale;
    ///  (c) lord z zyjacym malzonkiem (Spouse != null i IsAlive - wtedy BK HasPrimarySpouse zwraca true bez wolania czegokolwiek, a
    ///      BKMarriageModel.NpcCoupleMarriageChance daje 0 przez IsMarriageAdequate = -10000): oryginal losuje kandydata (jeden krok
    ///      generatora, gdy jest ktokolwiek), liczy szanse 0 i idzie dalej BEZ RandomFloat. My szukamy tylko pierwszego trafienia i
    ///      zuzywamy jeden krok (MBRandom.RandomInt(1); MBFastRandom.Next(int) to zawsze dokladnie jeden XorShift, bez wzgledu na argument).
    /// Reszta (szansa, RandomFloat, kontrola zalotow, MarriageAction.Apply, break) - przepisana 1:1.
    ///
    /// Dlaczego warunek pary mozna liczyc rzadziej (sprawdzone w dekompilacji gry 1.4.8, ROT, BK Redux, BKROTPatch):
    ///  - nie losuje i nie zmienia stanu gry: ROTMarriageModel (rody, wojna, wiek, wielkie rody, kultura, odleglosc domow, Mur) ->
    ///    DefaultMarriageModel (rody, plec, pokrewienstwo, Romance.GetCourtedHeroInOtherClan x2, CanMarry x2 -> model + pytanie do
    ///    zadan i spraw: same odczyty);
    ///  - jedyny slad: Hero.HomeSettlement liczy sie przy pierwszym odczycie (pole _homeSettlement, [CachedData] - puste po wczytaniu,
    ///    po zmianie rodu). Dlatego skroty (b) i (c) dotycza tylko bohaterow z policzonym domem: kandydat z pustym polem (tej samej
    ///    plci, a po skrocie (c) kazdy niezapytany) jest oceniany jak w oryginale, lord z pustym polem przeglada cala liste;
    ///  - Armoury RaceLaw.CouplePostfix (postfiks na obu poziomach modelu) zmienia tylko "tak" na "nie" dla roznych ras i zapisuje pare
    ///    do zbioru do linii "Rasy: dzien" - przy skrocie (c) ta linia nie liczy juz par, w ktorych swat ma malzonka (nie mogly sie pobrac).
    /// Podzial wedlug plci i spis pustych domow to jedno tanie przejscie po liscie (2 odczyty pola na bohatera) na wywolanie rodu, nie
    /// raz na dobe: Hero.IsFemale ma publiczny setter, a pole domu gra zeruje miedzy rodami (rody tykaja rozlozone na cala dobe).
    ///
    /// Straze (jedna linia "Mends: swaty ROT (Z.3): PODPIETY / BRAK" przy starcie sesji; gdy cos nie pasuje - liczy oryginal ROT):
    ///  dlugosci IL znanych metod (inna = inna logika), model na wierzchu ROTMarriageModel, pod nim pochodna BKMarriageModel z
    ///  IsCoupleSuitableForMarriage z gry; obce latki Harmony na lancuchu oceny pary (poza RaceLaw) albo obcy prefiks bool / transpiler
    ///  na samej metodzie ROT - nasza droga stoi; obca latka na lancuchu szansy (np. AIInfluence: prefiks DefaultMarriageModel
    ///  .NpcCoupleMarriageChance) - wylaczony tylko skrot (c). Obce latki sprawdzane przy pierwszym wywolaniu sesji i raz na dobe.
    /// Wyjatek w naszej drodze: stan generatora cofniety do wejscia, potkniecie policzone, ten rod liczy oryginal (latka zostaje).
    /// MarriageAction.Apply wolane poza ta ochrona (jak w oryginale) i przez delegata - metoda ma 9 bajtow IL, wolana wprost
    /// zostalaby wklejona w nasz kod i ominela ewentualna cudza latke.
    ///
    /// BIEG CIENIA (MarriageFastShadow): nasza droga liczy "co by wybrala" (bez slubu), stan generatora wraca do wejscia i decyduje
    /// oryginal ROT. Porownanie: para (albo brak) i stan generatora w chwili decyzji - u oryginalu lapany na wejsciu
    /// MarriageAction.ApplyInternal (albo na koncu metody, gdy slubu nie ma). Ten sam stan po tym samym starcie = ta sama liczba krokow.
    /// Linia doby: "Swaty ROT (Z.3): klanow N, par stara droga A, nowa B, roznic R, czas stara X ms, nowa Y ms; ...".
    /// Wylaczniki: MarriageFast (true; false = oryginal ROT bez latki), MarriageFastShadow (false) w ModuleData/CrashScribe.settings.xml.
    /// Bez zapisu w grze.
    /// </summary>
    internal static class MarriageFast
    {
        private const string Tag = "Swaty ROT (Z.3)";
        private const string OurOwner = "com.jeff.crashscribe";   // ten sam identyfikator Harmony co SubModuleMain.HarmonyId

        // dlugosci IL znanych metod (ROT 8.1.8, BK Redux, gra 1.4.8) - inna dlugosc = inna logika = straz nie puszcza
        private const int IlCheck = 448;       // ROT ROTRelationshipsBehavior.CheckNPCMarriages
        private const int IlRotCouple = 475;   // ROT ROTMarriageModel.IsCoupleSuitableForMarriage
        private const int IlDefCouple = 148;   // gra DefaultMarriageModel.IsCoupleSuitableForMarriage
        private const int IlPickRandom = 36;   // gra Extensions.GetRandomElementInefficiently
        private const int IlRotChance = 19;    // ROT ROTMarriageModel.NpcCoupleMarriageChance
        private const int IlBkChance = 48;     // BK BKMarriageModel.NpcCoupleMarriageChance
        private const int IlBkAdequate = 453;  // BK BKMarriageModel.IsMarriageAdequate
        private const int IlBkSpouse = 48;     // BK HeroExtensions.HasPrimarySpouse
        private const int IlRngNext = 73;      // gra MBFastRandom.Next(int)

        private static readonly Type[] H2 = { typeof(Hero), typeof(Hero) };

        // ---- dostep do cudzego kodu (raz na proces) ----
        private static Harmony _h;
        private static bool _resolved, _patched, _shadowPatched;
        private static string _resolveFail;
        private static MethodInfo _mCheck;
        private static FieldInfo _fList;                                 // ROTRelationshipsBehavior._unmarriedHeroes
        private static Func<Game, MBFastRandom> _getRng;                 // Game.RandomGenerator (internal) - ten sam generator co MBRandom
        private static AccessTools.FieldRef<MBFastRandom, uint> _rx, _ry, _rz, _rw;
        private static AccessTools.FieldRef<Hero, Settlement> _home;     // Hero._homeSettlement (pamiec HomeSettlement)
        private static Action<Hero, Hero, bool> _apply;                  // MarriageAction.Apply

        // ---- stan sesji kampanii ----
        private static bool _ready;            // straze przeszly - prefiks pracuje
        private static bool _shadow;           // bieg cienia w tej sesji
        private static bool _spouseModelOk;    // lancuch szansy zgodny ze znanym
        private static bool _spouseOk;         // skrot dla zonatych czynny (model + brak obcych latek)
        private static bool _standDown;        // obca latka na lancuchu - liczy oryginal
        private static string _standWhy;
        private static bool _patchCheckDue;
        private static MethodBase[] _chainCouple, _chainChance;

        // ---- bufory (jeden watek kampanii; _busy pilnuje ponownego wejscia) ----
        private static bool _busy;
        private static readonly List<Hero> _lords = new List<Hero>(16);
        private static readonly List<Hero> _males = new List<Hero>(1024);
        private static readonly List<Hero> _females = new List<Hero>(1024);
        private static readonly List<Hero> _nullHomes = new List<Hero>();
        private static readonly List<Hero> _cand = new List<Hero>(512);

        // ---- liczniki doby i sesji ----
        private static int _dayClans, _dayNewPairs, _dayOldPairs, _dayDiff, _dayBothThrew;
        private static int _dayLords, _daySpouse, _dayFull, _dayNullMax;
        private static long _dayEvals, _dayNewTicks, _dayOldTicks;
        private static int _totClans, _totDiff, _diffLines;
        private static int _stumbles, _stumblesDay;

        private struct Rng
        {
            public uint X, Y, Z, W;
            public bool Same(Rng o) { return X == o.X && Y == o.Y && Z == o.Z && W == o.W; }
        }

        // ---- rekord biegu cienia: miedzy naszym prefiksem a koncem oryginalu ----
        private static bool _shOpen, _shNewThrew, _shOldSeen;
        private static Clan _shClan;
        private static Hero _shNewH1, _shNewPick, _shOldH1, _shOldPick;
        private static Rng _shNewRng, _shOldRng;
        private static long _shT0;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_stumbles == 1) { try { Scribe.Report("CrashScribe", e, where, null); } catch { } }
        }

        private static int IlLen(MethodBase m)
        {
            try
            {
                var b = m != null ? m.GetMethodBody() : null;
                return b != null ? b.GetILAsByteArray().Length : -1;
            }
            catch { return -1; }
        }

        /// <summary>Implementacja metody wirtualnej, ktora naprawde wykona sie dla typu t (DeclaringType = klasa, ktora ja nadpisala).</summary>
        private static MethodInfo Impl(Type t, string name, Type[] args)
        {
            try { return t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args, null); }
            catch { return null; }
        }

        private static Rng Snap(MBFastRandom r)
        {
            Rng s; s.X = _rx(r); s.Y = _ry(r); s.Z = _rz(r); s.W = _rw(r);
            return s;
        }

        private static void Restore(MBFastRandom r, Rng s)
        {
            _rx(r) = s.X; _ry(r) = s.Y; _rz(r) = s.Z; _rw(r) = s.W;
        }

        // ------------------------------------------------------------ wpinanie i straze

        /// <summary>Typy, pola i metody cudzego kodu - raz na proces. Zwraca powod braku albo null.</summary>
        private static string Resolve()
        {
            var tBeh = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTRelationshipsBehavior");
            if (tBeh == null) return "ROT nieobecny - brak ROTRelationshipsBehavior";
            _mCheck = AccessTools.DeclaredMethod(tBeh, "CheckNPCMarriages", new[] { typeof(Clan) });
            if (_mCheck == null) return "brak metody ROTRelationshipsBehavior.CheckNPCMarriages(Clan)";
            int il = IlLen(_mCheck);
            if (il != IlCheck) return "inna wersja ROT - CheckNPCMarriages ma " + il + " bajtow IL, znana " + IlCheck;
            _fList = AccessTools.DeclaredField(tBeh, "_unmarriedHeroes");
            if (_fList == null || _fList.FieldType != typeof(List<Hero>)) return "brak pola ROTRelationshipsBehavior._unmarriedHeroes (List<Hero>)";

            il = IlLen(AccessTools.DeclaredMethod(typeof(TaleWorlds.Core.Extensions), "GetRandomElementInefficiently"));
            if (il != IlPickRandom) return "inna wersja gry - Extensions.GetRandomElementInefficiently ma " + il + " bajtow IL, znana " + IlPickRandom;

            var getter = AccessTools.PropertyGetter(typeof(Game), "RandomGenerator");
            if (getter == null || getter.ReturnType != typeof(MBFastRandom)) return "brak Game.RandomGenerator";
            _getRng = (Func<Game, MBFastRandom>)Delegate.CreateDelegate(typeof(Func<Game, MBFastRandom>), getter);
            foreach (var n in new[] { "_x", "_y", "_z", "_w" })
            {
                var f = AccessTools.DeclaredField(typeof(MBFastRandom), n);
                if (f == null || f.FieldType != typeof(uint)) return "brak pola MBFastRandom." + n;
            }
            _rx = AccessTools.FieldRefAccess<MBFastRandom, uint>("_x");
            _ry = AccessTools.FieldRefAccess<MBFastRandom, uint>("_y");
            _rz = AccessTools.FieldRefAccess<MBFastRandom, uint>("_z");
            _rw = AccessTools.FieldRefAccess<MBFastRandom, uint>("_w");

            var fHome = AccessTools.DeclaredField(typeof(Hero), "_homeSettlement");
            if (fHome == null || fHome.FieldType != typeof(Settlement)) return "brak pola Hero._homeSettlement";
            _home = AccessTools.FieldRefAccess<Hero, Settlement>("_homeSettlement");

            var mApply = AccessTools.DeclaredMethod(typeof(MarriageAction), "Apply", new[] { typeof(Hero), typeof(Hero), typeof(bool) });
            if (mApply == null) return "brak MarriageAction.Apply(Hero, Hero, bool)";
            _apply = (Action<Hero, Hero, bool>)Delegate.CreateDelegate(typeof(Action<Hero, Hero, bool>), mApply);
            return null;
        }

        /// <summary>
        /// Model malzenstw tej kampanii: znany lancuch ROT > BK > gra. Zwraca powod, dla ktorego cala latka stoi, albo null;
        /// spouseWhy = powod, dla ktorego nie dziala sam skrot dla zonatych.
        /// </summary>
        private static string CheckModel(out string spouseWhy, out string desc)
        {
            spouseWhy = null; desc = "?";
            _chainCouple = null; _chainChance = null;
            var mm = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.MarriageModel : null;
            if (mm == null) return "brak modelu malzenstw w kampanii";
            var tTop = mm.GetType();
            if (tTop.FullName != "ROT.Models.ROTMarriageModel") return "model malzenstw na wierzchu to " + tTop.FullName + ", znany ROT.Models.ROTMarriageModel";
            var fPrev = AccessTools.DeclaredField(tTop, "_previousModel");
            var prev = fPrev != null ? fPrev.GetValue(mm) as MarriageModel : null;
            if (prev == null) return "brak modelu pod ROTMarriageModel (_previousModel)";
            var tPrev = prev.GetType();
            var tBk = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKMarriageModel");
            if (tBk == null || !tBk.IsAssignableFrom(tPrev)) return "model pod ROT to " + tPrev.FullName + ", znany: pochodna BannerKings BKMarriageModel";
            desc = tTop.Name + " > " + tPrev.Name;

            var chain = new List<MethodBase>();
            var mRotCouple = Impl(tTop, "IsCoupleSuitableForMarriage", H2);
            if (mRotCouple == null || mRotCouple.DeclaringType != tTop || IlLen(mRotCouple) != IlRotCouple)
                return "inna wersja ROT - ROTMarriageModel.IsCoupleSuitableForMarriage ma " + IlLen(mRotCouple) + " bajtow IL, znana " + IlRotCouple;
            var mDefCouple = Impl(tPrev, "IsCoupleSuitableForMarriage", H2);
            if (mDefCouple == null || mDefCouple.DeclaringType != typeof(DefaultMarriageModel))
                return "model " + tPrev.Name + " ma wlasne IsCoupleSuitableForMarriage (" + (mDefCouple != null && mDefCouple.DeclaringType != null ? mDefCouple.DeclaringType.FullName : "?") + ") - para tej samej plci nie jest juz pewnym 'nie'";
            if (IlLen(mDefCouple) != IlDefCouple)
                return "inna wersja gry - DefaultMarriageModel.IsCoupleSuitableForMarriage ma " + IlLen(mDefCouple) + " bajtow IL, znana " + IlDefCouple;
            chain.Add(mRotCouple); chain.Add(mDefCouple);
            // pozostale metody modelu wolane przy ocenie pary i przez Hero.CanMarry - do kontroli obcych latek
            var more = new[]
            {
                new KeyValuePair<string, Type[]>("ShouldNpcMarriageBetweenClansBeAllowed", new[] { typeof(Clan), typeof(Clan) }),
                new KeyValuePair<string, Type[]>("IsClanSuitableForMarriage", new[] { typeof(Clan) }),
                new KeyValuePair<string, Type[]>("IsSuitableForMarriage", new[] { typeof(Hero) }),
            };
            foreach (var kv in more)
            {
                var a = Impl(tTop, kv.Key, kv.Value);
                var b = Impl(tPrev, kv.Key, kv.Value);
                if (a == null || b == null) return "brak metody modelu " + kv.Key;
                chain.Add(a); chain.Add(b);
            }
            var mCan = AccessTools.DeclaredMethod(typeof(Hero), "CanMarry", Type.EmptyTypes);
            if (mCan == null) return "brak Hero.CanMarry()";
            chain.Add(mCan);
            _chainCouple = chain.ToArray();

            // skrot dla zonatych: szansa slubu lorda z zyjacym malzonkiem musi byc zerem bez losowania
            var sc = new List<MethodBase>();
            try
            {
                var mRotChance = Impl(tTop, "NpcCoupleMarriageChance", H2);
                var mBkChance = Impl(tPrev, "NpcCoupleMarriageChance", H2);
                var mDefChance = AccessTools.DeclaredMethod(typeof(DefaultMarriageModel), "NpcCoupleMarriageChance", H2);
                var mAdequate = Impl(tPrev, "IsMarriageAdequate", new[] { typeof(Hero), typeof(Hero), typeof(bool), typeof(bool) });
                var tExt = AccessTools.TypeByName("BannerKings.Utils.Extensions.HeroExtensions");
                var mSpouse = tExt != null ? AccessTools.DeclaredMethod(tExt, "HasPrimarySpouse", new[] { typeof(Hero) }) : null;
                var mNext = AccessTools.DeclaredMethod(typeof(MBFastRandom), "Next", new[] { typeof(int) });
                if (mRotChance == null || mRotChance.DeclaringType != tTop || IlLen(mRotChance) != IlRotChance)
                    spouseWhy = "ROTMarriageModel.NpcCoupleMarriageChance inne niz znane";
                else if (mBkChance == null || mBkChance.DeclaringType != tBk || IlLen(mBkChance) != IlBkChance)
                    spouseWhy = "NpcCoupleMarriageChance modelu BK inne niz znane (" + (mBkChance != null && mBkChance.DeclaringType != null ? mBkChance.DeclaringType.Name : "?") + ", " + IlLen(mBkChance) + " bajtow IL)";
                else if (mDefChance == null)
                    spouseWhy = "brak DefaultMarriageModel.NpcCoupleMarriageChance";
                else if (mAdequate == null || mAdequate.DeclaringType != tBk || IlLen(mAdequate) != IlBkAdequate)
                    spouseWhy = "BKMarriageModel.IsMarriageAdequate inne niz znane (" + IlLen(mAdequate) + " bajtow IL)";
                else if (mSpouse == null || IlLen(mSpouse) != IlBkSpouse)
                    spouseWhy = "BK HasPrimarySpouse inne niz znane (" + IlLen(mSpouse) + " bajtow IL)";
                else if (mNext == null || IlLen(mNext) != IlRngNext)
                    spouseWhy = "MBFastRandom.Next(int) inne niz znane (" + IlLen(mNext) + " bajtow IL)";
                else
                {
                    sc.Add(mRotChance); sc.Add(mBkChance); sc.Add(mDefChance); sc.Add(mAdequate); sc.Add(mSpouse);
                }
            }
            catch (Exception e) { spouseWhy = "wyjatek przy sprawdzaniu lancucha szansy (" + e.GetType().Name + ")"; }
            _chainChance = sc.ToArray();
            return null;
        }

        private static string Who(Patch p)
        {
            try
            {
                var pm = p.PatchMethod;
                return p.owner + (pm != null && pm.DeclaringType != null ? " (" + pm.DeclaringType.Name + "." + pm.Name + ")" : "");
            }
            catch { return "?"; }
        }

        /// <summary>Pierwsza obca latka na metodzie (kazdy rodzaj) albo null; raceLawOk przepuszcza postfiks Armoury RaceLaw.CouplePostfix.</summary>
        private static string Foreign(MethodBase m, bool raceLawOk)
        {
            var info = Harmony.GetPatchInfo(m);
            if (info == null) return null;
            for (int k = 0; k < 4; k++)
            {
                var set = k == 0 ? info.Prefixes : k == 1 ? info.Postfixes : k == 2 ? info.Transpilers : info.Finalizers;
                if (set == null) continue;
                foreach (var p in set)
                {
                    if (p == null || p.owner == OurOwner) continue;
                    var pm = p.PatchMethod;
                    if (raceLawOk && k == 1 && pm != null && pm.DeclaringType != null && pm.DeclaringType.FullName == "Armoury.RaceLaw" && pm.Name == "CouplePostfix") continue;
                    return Who(p) + " na " + (m.DeclaringType != null ? m.DeclaringType.Name + "." : "") + m.Name;
                }
            }
            return null;
        }

        /// <summary>
        /// Obce latki Harmony (inne mody wpinaja sie takze po nas): na samej metodzie ROT grozny jest tylko prefiks z wynikiem bool
        /// i transpiler (zmieniaja bieg oryginalu; postfiksy i prefiksy-obserwatorzy biegna takze przy naszej drodze); na lancuchu
        /// oceny pary kazda obca latka poza RaceLaw zatrzymuje nasza droge; na lancuchu szansy - tylko skrot dla zonatych.
        /// </summary>
        private static void CheckForeign()
        {
            string stand = null, spouse = null;
            try
            {
                var info = Harmony.GetPatchInfo(_mCheck);
                if (info != null)
                {
                    if (info.Prefixes != null)
                        foreach (var p in info.Prefixes)
                            if (p != null && p.owner != OurOwner && p.PatchMethod != null && p.PatchMethod.ReturnType == typeof(bool)) { stand = "prefiks " + Who(p) + " na CheckNPCMarriages"; break; }
                    if (stand == null && info.Transpilers != null)
                        foreach (var p in info.Transpilers)
                            if (p != null && p.owner != OurOwner) { stand = "transpiler " + Who(p) + " na CheckNPCMarriages"; break; }
                }
                if (stand == null && _chainCouple != null)
                    foreach (var m in _chainCouple) { stand = Foreign(m, true); if (stand != null) break; }
                if (_chainChance != null)
                    foreach (var m in _chainChance) { spouse = Foreign(m, false); if (spouse != null) break; }
            }
            catch (Exception e) { Stumble("MarriageFast.CheckForeign", e); stand = "nie udalo sie sprawdzic obcych latek"; }

            if (stand != _standWhy)
            {
                _standDown = stand != null; _standWhy = stand;
                Scribe.Line(Tag + ": " + (stand != null
                    ? "obca latka - " + stand + " - nasza droga stoi, swaty liczy oryginal ROT."
                    : "obcej latki juz nie ma - nasza droga znow czynna."));
            }
            bool sp = _spouseModelOk && spouse == null;
            if (sp != _spouseOk)
            {
                _spouseOk = sp;
                Scribe.Line(Tag + ": skrot dla zonatych " + (sp ? "znow czynny." : "wylaczony - obca latka - " + spouse + " - reszta drogi bez zmian."));
            }
        }

        /// <summary>Start sesji kampanii (OnSessionLaunched - modele juz sa): straze, latka (raz na proces), linia startu.</summary>
        internal static void OnSession()
        {
            try
            {
                _ready = false; _shadow = false; _spouseOk = false; _spouseModelOk = false; _standDown = false; _standWhy = null; _patchCheckDue = true;
                bool fast = Config.MarriageFast, shadow = Config.MarriageFastShadow;
                if (!fast && !shadow) { Say(false, "MarriageFast = false"); return; }
                if (!_resolved) { _resolved = true; _resolveFail = Resolve(); }
                if (_resolveFail != null) { Say(false, _resolveFail); return; }
                string spouseWhy, desc;
                string why = CheckModel(out spouseWhy, out desc);
                if (why != null) { Say(false, why); return; }

                if (_h == null) _h = new Harmony(OurOwner);
                if (!_patched)
                {
                    _h.Patch(_mCheck, prefix: new HarmonyMethod(typeof(MarriageFast), nameof(Prefix)));
                    _patched = true;
                }
                if (shadow && !_shadowPatched)
                {
                    var mInternal = AccessTools.DeclaredMethod(typeof(MarriageAction), "ApplyInternal", new[] { typeof(Hero), typeof(Hero), typeof(bool) });
                    if (mInternal == null) { Say(false, "bieg cienia: brak MarriageAction.ApplyInternal(Hero, Hero, bool)"); return; }
                    _h.Patch(mInternal, prefix: new HarmonyMethod(typeof(MarriageFast), nameof(ShadowApplyWatch)));
                    _h.Patch(_mCheck, postfix: new HarmonyMethod(typeof(MarriageFast), nameof(ShadowPostfix)));
                    _shadowPatched = true;
                }
                _shadow = shadow;
                _spouseModelOk = spouseWhy == null;
                _spouseOk = _spouseModelOk;
                _ready = true;
                Say(true, "ROT ROTRelationshipsBehavior.CheckNPCMarriages; model " + desc + "; "
                          + (shadow ? "BIEG CIENIA - decyduje oryginal ROT, nasza droga tylko liczy; linia doby '" + Tag + ": klanow ..., roznic ...'"
                                    : "decyduje nasza droga: te same pary i ten sam ciag losowy, jedno przejscie po kandydatach przeciwnej plci")
                          + "; skrot dla zonatych: " + (spouseWhy == null ? "tak" : "nie - " + spouseWhy));
            }
            catch (Exception e)
            {
                _ready = false;
                Stumble("MarriageFast.OnSession", e);
                Say(false, "wyjatek przy wpinaniu " + e.GetType().Name + " - szczegoly w raporcie");
            }
        }

        private static void Say(bool ok, string what)
        {
            try { Scribe.Line("Mends: swaty ROT (Z.3): " + (ok ? "PODPIETY (" + what + ")" : "BRAK (" + what + ") - zostaje oryginal ROT")); } catch { }
        }

        // ------------------------------------------------------------ nasza droga

        /// <summary>Prefiks ROTRelationshipsBehavior.CheckNPCMarriages(Clan). false = oryginal pominiety (zdecydowala nasza droga).</summary>
        public static bool Prefix(object __instance, Clan __0)
        {
            if (!_ready || _busy) return true;
            MBFastRandom rng; List<Hero> list; MarriageModel model; Rng s0;
            try
            {
                if (_patchCheckDue) { _patchCheckDue = false; CheckForeign(); }
                if (_standDown) return true;
                if (_shOpen) ShadowOrphan();
                var game = Game.Current;
                rng = game != null ? _getRng(game) : null;
                list = _fList.GetValue(__instance) as List<Hero>;
                model = Campaign.Current.Models.MarriageModel;
                if (rng == null || list == null || model == null) { _stumbles++; _stumblesDay++; return true; }
                s0 = Snap(rng);
            }
            catch (Exception e) { Stumble("MarriageFast.Prefix (wejscie)", e); return true; }

            Hero h1 = null, pick = null;
            bool decided = false;
            Exception err = null;
            long t0 = Stopwatch.GetTimestamp();
            try { _busy = true; decided = Decide(model, list, __0, out h1, out pick); }
            catch (Exception e) { err = e; }
            finally { _busy = false; }
            long dt = Stopwatch.GetTimestamp() - t0;

            _dayClans++; _totClans++;
            if (err != null)
            {
                // nic nie zostalo zmienione poza generatorem: cofamy go i oddajemy ten rod oryginalowi (zrobi dokladnie to, co bez latki)
                try { Restore(rng, s0); } catch { }
                Stumble("MarriageFast.Decide", err);
                if (_shadow) ShadowOpen(__0, null, null, default(Rng), true);
                return true;
            }
            _dayNewTicks += dt;
            if (decided) _dayNewPairs++;

            if (_shadow)
            {
                // bieg cienia: nasza droga tylko policzyla - generator wraca do wejscia, decyduje oryginal
                Rng sNew;
                try { sNew = Snap(rng); Restore(rng, s0); }
                catch (Exception e) { Stumble("MarriageFast.Prefix (cien)", e); return true; }
                ShadowOpen(__0, h1, pick, sNew, false);
                return true;
            }
            if (decided) _apply(h1, pick, true);   // poza ochrona, jak w oryginale: wyjatek slubu leci dalej tak samo
            return false;
        }

        /// <summary>
        /// Decyzja rodu bez slubu: kto z kim (albo nikt). Zuzywa generator dokladnie jak oryginal - te same wywolania MBRandom,
        /// w tej samej kolejnosci, z tymi samymi argumentami (wyjatek: krok zonatego, gdzie argument nie ma znaczenia dla stanu).
        /// </summary>
        private static bool Decide(MarriageModel model, List<Hero> list, Clan clan, out Hero h1, out Hero pick)
        {
            h1 = null; pick = null;
            if (clan == Clan.PlayerClan || !model.IsClanSuitableForMarriage(clan)) return false;
            _lords.Clear();
            _lords.AddRange(clan.AliveLords);   // kopia, jak ToList() w oryginale
            bool split = false;
            for (int li = 0; li < _lords.Count; li++)
            {
                Hero hero1 = _lords[li];
                if (!hero1.CanMarry()) continue;
                if (!split) { Split(list); split = true; }
                _dayLords++;
                Hero x;
                if (!Scan(model, list, hero1, out x)) continue;
                float num = model.NpcCoupleMarriageChance(hero1, x);
                if (!(num > 0f) || !(MBRandom.RandomFloat < num)) continue;
                bool courting = false;
                List<Romance.RomanticState> states = Romance.RomanticStateList;
                for (int i = 0; i < states.Count; i++)
                {
                    Romance.RomanticState rs = states[i];
                    if ((int)rs.Level >= 3 && (rs.Person1 == hero1 || rs.Person2 == hero1 || rs.Person1 == x || rs.Person2 == x)) { courting = true; break; }
                }
                if (courting) continue;
                h1 = hero1; pick = x;
                return true;
            }
            return false;
        }

        /// <summary>Jedno tanie przejscie po liscie ROT na wywolanie rodu: podzial wedlug plci (kolejnosc listy zachowana) i spis
        /// bohaterow z pusta pamiecia domu (dla nich ocena pary ma skutek uboczny - nie wolno jej pominac).</summary>
        private static void Split(List<Hero> list)
        {
            _males.Clear(); _females.Clear(); _nullHomes.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                Hero h = list[i];
                if (h.IsFemale) _females.Add(h); else _males.Add(h);
                if (_home(h) == null) _nullHomes.Add(h);
            }
            if (_nullHomes.Count > _dayNullMax) _dayNullMax = _nullHomes.Count;
        }

        /// <summary>
        /// Kandydat dla lorda. true = x wylosowany jak w oryginale (ten sam krok generatora, ten sam element); false = dalej bez
        /// liczenia szansy: nie ma kandydata (bez losowania, jak w oryginale) albo lord ma zyjacego malzonka (jeden krok generatora
        /// zuzyty, szansa i tak wynosi 0, wiec oryginal tez nie robi nic wiecej).
        /// </summary>
        private static bool Scan(MarriageModel model, List<Hero> list, Hero hero1, out Hero x)
        {
            x = null;
            _cand.Clear();
            if (_home(hero1) == null)
            {
                // pamiec domu lorda pusta: kazda ocena pary moze ja wypelnic - cala lista w kolejnosci, jak Count() w oryginale
                _dayFull++;
                for (int i = 0; i < list.Count; i++)
                {
                    Hero h = list[i];
                    _dayEvals++;
                    if (model.IsCoupleSuitableForMarriage(hero1, h)) _cand.Add(h);
                }
            }
            else
            {
                bool female = hero1.IsFemale;
                List<Hero> other = female ? _males : _females;
                Hero spouse = _spouseOk ? hero1.Spouse : null;
                bool quick = spouse != null && spouse.IsAlive;   // lord z zyjacym malzonkiem: szansa slubu zawsze 0
                bool any = false;
                if (quick)
                {
                    _daySpouse++;
                    for (int i = 0; i < other.Count; i++)
                    {
                        _dayEvals++;
                        if (model.IsCoupleSuitableForMarriage(hero1, other[i])) { any = true; break; }
                    }
                }
                else
                {
                    for (int i = 0; i < other.Count; i++)
                    {
                        Hero h = other[i];
                        _dayEvals++;
                        if (model.IsCoupleSuitableForMarriage(hero1, h)) _cand.Add(h);
                    }
                }
                // bohaterowie z pusta pamiecia domu, ktorych skroty by nie zapytaly: oryginal ocenia cala liste, a ocena wypelnia te
                // pamiec - robimy to samo (ta sama plec zawsze; przeciwna tylko po skrocie zonatego - przy pelnym przejsciu juz byla).
                // Ponowna ocena tej samej pary niczego nie zmienia (oryginal tez liczy ja do trzech razy).
                for (int i = 0; i < _nullHomes.Count; i++)
                {
                    Hero h = _nullHomes[i];
                    if (_home(h) != null) continue;
                    bool same = h.IsFemale == female;
                    if (!same && !quick) continue;
                    _dayEvals++;
                    if (!model.IsCoupleSuitableForMarriage(hero1, h)) continue;
                    if (same) throw new InvalidOperationException("model uznal pare tej samej plci za dobra - podzial wedlug plci nie jest rownowazny");
                    any = true;
                }
                if (quick)
                {
                    if (any) MBRandom.RandomInt(1);   // krok, ktory oryginal zuzywa na wybor kandydata (szansa 0 - RandomFloat nie pada)
                    return false;
                }
            }
            if (_cand.Count == 0) return false;
            x = _cand[MBRandom.RandomInt(_cand.Count)];
            return true;
        }

        // ------------------------------------------------------------ bieg cienia

        private static void ShadowOpen(Clan clan, Hero h1, Hero pick, Rng sNew, bool threw)
        {
            _shOpen = true; _shClan = clan; _shNewH1 = h1; _shNewPick = pick; _shNewRng = sNew; _shNewThrew = threw;
            _shOldSeen = false; _shOldH1 = null; _shOldPick = null;
            _shT0 = Stopwatch.GetTimestamp();
        }

        /// <summary>Prefiks-obserwator MarriageAction.ApplyInternal (tylko bieg cienia): decyzja oryginalu i stan generatora w tej chwili.</summary>
        public static void ShadowApplyWatch(Hero __0, Hero __1)
        {
            if (!_shOpen || _shOldSeen) return;
            try
            {
                _dayOldTicks += Stopwatch.GetTimestamp() - _shT0;
                _shOldSeen = true; _shOldH1 = __0; _shOldPick = __1;
                var game = Game.Current;
                var rng = game != null ? _getRng(game) : null;
                _shOldRng = rng != null ? Snap(rng) : default(Rng);
            }
            catch (Exception e) { Stumble("MarriageFast.ShadowApplyWatch", e); }
        }

        /// <summary>Postfiks CheckNPCMarriages (tylko bieg cienia): porownanie obu drog dla tego rodu.</summary>
        public static void ShadowPostfix()
        {
            if (!_shOpen) return;
            try
            {
                bool same;
                if (_shOldSeen)
                {
                    _dayOldPairs++;
                    same = !_shNewThrew && _shNewH1 == _shOldH1 && _shNewPick == _shOldPick && _shNewRng.Same(_shOldRng);
                }
                else
                {
                    _dayOldTicks += Stopwatch.GetTimestamp() - _shT0;
                    var game = Game.Current;
                    var rng = game != null ? _getRng(game) : null;
                    same = !_shNewThrew && _shNewH1 == null && rng != null && _shNewRng.Same(Snap(rng));
                }
                if (!same) ShadowDiff(_shOldSeen ? Pair(_shOldH1, _shOldPick) : "bez slubu");
            }
            catch (Exception e) { Stumble("MarriageFast.ShadowPostfix", e); }
            ShadowClose();
        }

        /// <summary>Oryginal nie doszedl do konca (wyjatek) - rekord zamykany przy nastepnym wywolaniu albo linii doby.</summary>
        private static void ShadowOrphan()
        {
            try
            {
                if (_shNewThrew) _dayBothThrew++;
                else ShadowDiff("wyjatek oryginalu");
            }
            catch { }
            ShadowClose();
        }

        private static void ShadowClose()
        {
            _shOpen = false; _shClan = null; _shNewH1 = null; _shNewPick = null; _shOldH1 = null; _shOldPick = null;
        }

        private static void ShadowDiff(string oldWhat)
        {
            _dayDiff++; _totDiff++;
            if (_diffLines >= 5) return;
            _diffLines++;
            Scribe.Line(Tag + ": ROZNICA w rodzie " + Name(_shClan) + " - stara droga: " + oldWhat + ", nowa: "
                        + (_shNewThrew ? "wyjatek" : _shNewH1 != null ? Pair(_shNewH1, _shNewPick) : "bez slubu")
                        + "; stan losowy w chwili decyzji " + (_shOldSeen && !_shNewThrew && _shNewRng.Same(_shOldRng) ? "zgodny" : "niezgodny albo nieporownany")
                        + " (linia " + _diffLines + " z 5 w sesji).");
        }

        private static string Pair(Hero a, Hero b) { return "slub " + Name(a) + " + " + Name(b); }

        private static string Name(Hero h)
        {
            try { return h == null ? "?" : (h.Name != null ? h.Name.ToString() : h.StringId); } catch { return "?"; }
        }

        private static string Name(Clan c)
        {
            try { return c == null ? "?" : (c.Name != null ? c.Name.ToString() : c.StringId); } catch { return "?"; }
        }

        // ------------------------------------------------------------ linia doby

        private static int Day()
        {
            try { return (int)CampaignTime.Now.ToDays; } catch { return 0; }
        }

        /// <summary>Raz na dobe (DailyTick): linia z licznikami od poprzedniej, obce latki do ponownego sprawdzenia.</summary>
        internal static void DailyLine()
        {
            try
            {
                _patchCheckDue = true;
                if (_shOpen) ShadowOrphan();
                if (_ready && (_dayClans > 0 || _stumblesDay > 0))
                {
                    var ci = CultureInfo.InvariantCulture;
                    double ms = 1000.0 / Stopwatch.Frequency;
                    string work = "lordow szukajacych " + _dayLords + " (z zyjacym malzonkiem - skrot " + _daySpouse + ", cala lista " + _dayFull
                                  + "), ocen pary " + _dayEvals + ", bohaterow bez pamieci domu najwyzej " + _dayNullMax
                                  + (_stumblesDay > 0 ? ", POTKNIECIA " + _stumblesDay + " (razem " + _stumbles + ", pierwsze w raporcie; rod z potknieciem liczy oryginal ROT od tego samego stanu losowego)" : "");
                    if (_shadow)
                        Scribe.Line(Tag + ": klanow " + _dayClans + ", par stara droga " + _dayOldPairs + ", nowa " + _dayNewPairs + ", roznic " + _dayDiff
                                    + ", czas stara " + (_dayOldTicks * ms).ToString("0.0", ci) + " ms, nowa " + (_dayNewTicks * ms).ToString("0.0", ci) + " ms"
                                    + "; dzien " + Day() + ", " + work + (_dayBothThrew > 0 ? ", wyjatek w obu drogach " + _dayBothThrew : "")
                                    + "; od wczytania: klanow " + _totClans + ", roznic " + _totDiff + ".");
                    else
                        Scribe.Line(Tag + ": dzien " + Day() + " - klanow " + _dayClans + ", slubow " + _dayNewPairs + ", czas " + (_dayNewTicks * ms).ToString("0.0", ci)
                                    + " ms; " + work + ".");
                }
            }
            catch (Exception e) { Stumble("MarriageFast.DailyLine", e); }
            _dayClans = _dayNewPairs = _dayOldPairs = _dayDiff = _dayBothThrew = 0;
            _dayLords = _daySpouse = _dayFull = _dayNullMax = 0;
            _dayEvals = _dayNewTicks = _dayOldTicks = 0;
            _stumblesDay = 0;
        }

        /// <summary>Nowa gra / wczytanie: prefiks stoi do OnSessionLaunched, liczniki od zera (latka Harmony zostaje - raz na proces).</summary>
        internal static void ResetSession()
        {
            _ready = false; _shadow = false; _spouseOk = false; _spouseModelOk = false; _standDown = false; _standWhy = null; _patchCheckDue = true;
            _busy = false;
            ShadowClose();
            _lords.Clear(); _males.Clear(); _females.Clear(); _nullHomes.Clear(); _cand.Clear();
            _dayClans = _dayNewPairs = _dayOldPairs = _dayDiff = _dayBothThrew = 0;
            _dayLords = _daySpouse = _dayFull = _dayNullMax = 0;
            _dayEvals = _dayNewTicks = _dayOldTicks = 0;
            _totClans = _totDiff = _diffLines = 0;
            _stumblesDay = 0;
        }
    }

    /// <summary>Start sesji (straze i latka) oraz linia doby "Swaty ROT (Z.3)" - bez zapisu w grze.</summary>
    internal sealed class MarriageFastBehavior : CampaignBehaviorBase
    {
        public MarriageFastBehavior() { MarriageFast.ResetSession(); }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, delegate (CampaignGameStarter s) { MarriageFast.OnSession(); });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, MarriageFast.DailyLine);
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
