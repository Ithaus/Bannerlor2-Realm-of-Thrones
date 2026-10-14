using System;
using System.Globalization;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;

namespace CrashScribe
{
    /// <summary>
    /// OFERTY SLUBU DLA RODU GRACZA NA ROK (decyzja Jeffa 14.10 "sluby - 48 slubow" na rok, zasada "1 dzien = 1 dzien"; rozpoznanie
    /// docs/pytania-2026-10-14/STARZENIE-I-CZAS-ZYCIA.md tabela nr 12 i rozdz. 3 pkt 3).
    /// Gra (MarriageOfferCampaignBehavior.DailyTickClan, sluchacz DailyTickClan - ROT go nie wylacza, wylacza tylko swaty RomanceCampaignBehavior) co dobe dla
    /// kazdego rodu AI: przerwa 7 dob od ostatniej oferty, rzut odleglosci rodu od rodu gracza, potem dla kazdego czlonka rodu gracza i kazdego bohatera
    /// rodu AI rzut "RandomFloat &lt; NpcCoupleMarriageChance" - pierwsze trafienie = oferta na mapie. Szansa jest "na dobe" i dobrana pod rok gry 84 dni;
    /// przy roku Armoury 364 doby ofert na ROK jest wiecej, niz gra przewidziala - do 4.33 x przy malej szansie, mniej przy duzej, bo przerwa 7 dob (nizej) jest
    /// czasem w dobach (pomiaru ofert nie ma w zadnym logu).
    /// REGULA: prefiks (Priority.Last - po prefiksie ROT MarriageCrash, ktory przepuszcza tylko rody z para pasujaca do rodu gracza albo do samego gracza)
    /// rozciaga CALY proces ofert x 1/f, f = 84 / CampaignTime.DaysInYear (rok 364 -> 0.231; rok nie dluzszy niz 84 - f = 1, prefiks nic nie robi):
    ///  - przerwa miedzy ofertami: gra liczy 7 dob od ostatniej oferty JEDNA dla wszystkich rodow (_lastMarriageOfferTime, CanOfferMarriageForClan) i trzyma
    ///    naraz tylko jedna oferte - prefiks odrzuca dobe rodu, gdy od ostatniej oferty minelo mniej niz 7 / f dob (30.3 przy roku 364);
    ///  - poza przerwa doba rodu idzie do rzutow gry z prawdopodobienstwem f (rzut niezalezny od rzutow gry: szansa oferty rodu na dobe = f x szansa gry).
    ///    Ofert na dobe gra daje 1 / (7 + 1 / p) (p - szansa oferty od dowolnego rodu na dobe poza przerwa); po rozciagnieciu 1 / (7 / f + 1 / (f p)) = f x tyle -
    ///    na rok 364 dob dokladnie tyle, ile gra dawala w swoim roku 84 dni, przy kazdym p (recenzja 14.10: sam mnoznik szansy bez przerwy dawal 1.25-3.1 x wiecej).
    /// Reszta gry bez zmian (kto moze dostac oferte, odleglosc rodu, 2 doby na odpowiedz, oferty gracza). Rzut i przerwa tylko, gdy oryginal i tak by biegl
    /// (__runOriginal) - doba rodu odrzucona przez ROT nie zuzywa generatora; doba w przerwie tez nie (bez rzutu).
    /// NIE latka na NpcCoupleMarriageChance: ta sama metoda liczy szanse swatow ROT (MarriageFast), a MarriageFast uznalby obca latke na lancuchu szansy
    /// za powod do zatrzymania reguly "tylko kandydaci z szansa" (MarriageFast.CheckForeign). Sluby AI-AI - hamulec MarriageChanceScale (MarriageFast.cs).
    /// Klucz MarriageOffersPerYear (true; false = oferty jak w grze - prefiks przepuszcza wszystko bez rzutu). Linia startu "Mends: oferty slubu (rok):
    /// PODPIETY / BRAK", linia doby "Oferty slubu (rok): dzien N | ..." (doby rodow, przepuszczone, oferty). Bez zapisu w grze; wyjatek w prefiksie -
    /// doba rodu przepuszczona, licznik potkniec (CLAUDE.md 7).
    /// </summary>
    internal static class MarriageOfferYear
    {
        private const string Tag = "Oferty slubu (rok)";
        private const string OurOwner = "com.jeff.crashscribe";   // ten sam identyfikator Harmony co SubModuleMain.HarmonyId i MarriageFast
        private const double GameYear = 84.0;                     // rok gry: 7 dni x 3 tygodnie x 4 pory (DefaultCampaignTimeModel)
        private const double GameGap = 7.0;                       // gra: doby od ostatniej oferty do nastepnej (CanOfferMarriageForClan)

        private static Harmony _h;
        private static bool _tried, _patched;
        private static string _fail;
        private static bool _on;                 // regula czynna w tej sesji (klucz i latka)
        private static float _f = 1f;            // mnoznik doby rodu (84 / rok)
        private static double _gap = GameGap;    // przerwa miedzy ofertami w dobach (7 / f)
        private static int _year;                // rok kalendarza tej sesji (doby)
        private static AccessTools.FieldRef<MarriageOfferCampaignBehavior, CampaignTime> _last;   // MarriageOfferCampaignBehavior._lastMarriageOfferTime
        // liczniki doby i sesji (watek kampanii): doby rodow po ROT, w przerwie (bez rzutu), rzucone, przepuszczone, oferty
        private static int _dayAll, _dayGap, _dayAsked, _dayPassed, _dayOffers, _totAll, _totGap, _totAsked, _totPassed, _totOffers, _stumbles, _stumblesDay;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_stumbles == 1) { try { Scribe.Report("CrashScribe", e, where, null); } catch { } }
        }

        private static string Num(double v) { return v.ToString("0.000", CultureInfo.InvariantCulture); }

        /// <summary>Start sesji kampanii (OnSessionLaunched): latka raz na proces, mnoznik z roku kalendarza tej sesji, linia startu.</summary>
        internal static void OnSession()
        {
            _on = false; _f = 1f; _gap = GameGap; _year = 0;
            try
            {
                if (!_tried)
                {
                    _tried = true;
                    var m = AccessTools.DeclaredMethod(typeof(MarriageOfferCampaignBehavior), "DailyTickClan", new[] { typeof(Clan) });
                    var mOffer = AccessTools.DeclaredMethod(typeof(MarriageOfferCampaignBehavior), "CreateMarriageOffer", new[] { typeof(Hero), typeof(Hero) });
                    var fLast = AccessTools.DeclaredField(typeof(MarriageOfferCampaignBehavior), "_lastMarriageOfferTime");
                    if (m == null) _fail = "brak MarriageOfferCampaignBehavior.DailyTickClan(Clan)";
                    else if (fLast == null || fLast.FieldType != typeof(CampaignTime)) _fail = "brak pola MarriageOfferCampaignBehavior._lastMarriageOfferTime (CampaignTime)";
                    else
                    {
                        _last = AccessTools.FieldRefAccess<MarriageOfferCampaignBehavior, CampaignTime>("_lastMarriageOfferTime");
                        if (_h == null) _h = new Harmony(OurOwner);
                        _h.Patch(m, prefix: new HarmonyMethod(typeof(MarriageOfferYear), nameof(Prefix)) { priority = Priority.Last });
                        if (mOffer != null) _h.Patch(mOffer, postfix: new HarmonyMethod(typeof(MarriageOfferYear), nameof(OfferPostfix)));   // tylko licznik ofert
                        _patched = true;
                    }
                }
                try { _year = CampaignTime.DaysInYear; } catch { _year = 0; }
                if (!Config.MarriageOffersPerYear) { Say(false, "MarriageOffersPerYear = false - oferty jak w grze"); return; }
                if (!_patched) { Say(false, (_fail ?? "latka nie zalozona") + " - oferty jak w grze"); return; }
                _f = _year > GameYear ? (float)(GameYear / _year) : 1f;
                _gap = GameGap / _f;
                _on = true;
                bool beh = Campaign.Current != null && Campaign.Current.GetCampaignBehavior<MarriageOfferCampaignBehavior>() != null;
                Say(true, "MarriageOfferCampaignBehavior.DailyTickClan, Priority.Last; rok " + _year + " dob, doba rodu przechodzi z prawdopodobienstwem "
                          + Num(_f) + " = 84 / rok, przerwa miedzy ofertami " + _gap.ToString("0.0", CultureInfo.InvariantCulture) + " dob (gra 7)"
                          + (_f >= 1f ? " (rok nie dluzszy niz 84 - bez rzutu i bez dluzszej przerwy, jak w grze)" : "")
                          + (beh ? "" : "; UWAGA: w kampanii brak zachowania MarriageOfferCampaignBehavior - ofert nie bedzie wcale"));
            }
            catch (Exception e)
            {
                _on = false;
                Stumble("MarriageOfferYear.OnSession", e);
                Say(false, "wyjatek przy wpinaniu " + e.GetType().Name + " - oferty jak w grze");
            }
        }

        private static void Say(bool ok, string what)
        {
            try { Scribe.Line("Mends: oferty slubu (rok): " + (ok ? "PODPIETY (" : "BRAK (") + what + ")."); } catch { }
        }

        /// <summary>Prefiks MarriageOfferCampaignBehavior.DailyTickClan(Clan): poza przerwa 7 / f dob od ostatniej oferty doba rodu idzie do rzutow gry
        /// z prawdopodobienstwem f = 84 / rok.</summary>
        public static bool Prefix(MarriageOfferCampaignBehavior __instance, Clan __0, bool __runOriginal)
        {
            if (!__runOriginal) return false;   // wczesniejszy prefiks (ROT MarriageCrash) juz zatrzymal oryginal - bez rzutu
            if (!_on || !(_f < 1f)) return true;
            try
            {
                _dayAll++; _totAll++;
                // przerwa miedzy ofertami jedna dla wszystkich rodow (pole gry) - rozciagnieta jak caly rok; w przerwie bez rzutu
                if (__instance != null && _last != null && _last(__instance).ElapsedDaysUntilNow < _gap) { _dayGap++; _totGap++; return false; }
                _dayAsked++; _totAsked++;
                if (MBRandom.RandomFloat < _f) { _dayPassed++; _totPassed++; return true; }
                return false;
            }
            catch (Exception e) { Stumble("MarriageOfferYear.Prefix", e); return true; }
        }

        /// <summary>Postfiks MarriageOfferCampaignBehavior.CreateMarriageOffer - tylko licznik ofert (linia doby).</summary>
        public static void OfferPostfix()
        {
            _dayOffers++; _totOffers++;
        }

        /// <summary>Raz na dobe (DailyTick): linia z licznikami od poprzedniej.</summary>
        internal static void DailyLine()
        {
            try
            {
                if (_patched)
                {
                    int day = 0;
                    try { day = (int)CampaignTime.Now.ToDays; } catch { }
                    Scribe.Line(Tag + ": dzien " + day + " | " + (_on ? "regula CZYNNA, mnoznik doby rodu " + Num(_f) + " (84 / " + _year + ")"
                                                                      : "regula NIECZYNNA (" + (Config.MarriageOffersPerYear ? "brak wpiecia" : "MarriageOffersPerYear = false") + ") - jak w grze")
                                + (_on ? ", przerwa miedzy ofertami " + _gap.ToString("0.0", CultureInfo.InvariantCulture) + " dob (gra 7)" : "")
                                + " | doby rodow przepuszczone przez ROT " + _dayAll + ": w przerwie " + _dayGap + ", rzucone " + _dayAsked + ", przepuszczone " + _dayPassed
                                + " | oferty dzis " + _dayOffers + " | od wczytania: doby rodow " + _totAll + ", w przerwie " + _totGap + ", rzucone " + _totAsked
                                + ", przepuszczone " + _totPassed + ", oferty " + _totOffers
                                + (_stumblesDay > 0 ? " | POTKNIECIA " + _stumblesDay + " (razem " + _stumbles + ")" : "") + ".");
                }
            }
            catch (Exception e) { Stumble("MarriageOfferYear.DailyLine", e); }
            _dayAll = _dayGap = _dayAsked = _dayPassed = _dayOffers = 0; _stumblesDay = 0;
        }

        /// <summary>Nowa gra / wczytanie: regula stoi do OnSessionLaunched, liczniki od zera (latka zostaje - raz na proces).</summary>
        internal static void ResetSession()
        {
            _on = false; _f = 1f; _gap = GameGap; _year = 0;
            _dayAll = _dayGap = _dayAsked = _dayPassed = _dayOffers = 0; _totAll = _totGap = _totAsked = _totPassed = _totOffers = 0; _stumblesDay = 0;
        }
    }

    /// <summary>Start sesji (latka i mnoznik) oraz linia doby "Oferty slubu (rok)" - bez zapisu w grze.</summary>
    internal sealed class MarriageOfferYearBehavior : CampaignBehaviorBase
    {
        public MarriageOfferYearBehavior() { MarriageOfferYear.ResetSession(); }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, delegate (CampaignGameStarter s) { MarriageOfferYear.OnSession(); });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, MarriageOfferYear.DailyLine);
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
