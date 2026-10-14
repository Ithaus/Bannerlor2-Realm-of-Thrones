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
    /// przy roku Armoury 364 doby ofert na ROK jest ok. 4.33 x wiecej, niz gra przewidziala (sam rok gracza - pomiaru ofert nie ma w zadnym logu).
    /// REGULA: prefiks (Priority.Last - po prefiksie ROT MarriageCrash, ktory przepuszcza tylko rody z para pasujaca do rodu gracza) przepuszcza dobe rodu
    /// z prawdopodobienstwem f = 84 / CampaignTime.DaysInYear (rok 364 -> 0.231; rok nie dluzszy niz 84 - f = 1, bez rzutu). Rzut jest niezalezny od rzutow
    /// gry, wiec szansa oferty rodu na dobe = f x szansa gry dokladnie, a ofert na rok tyle, ile gra dawala w swoim roku. Przerwa 7 dob (czas trwania w dobach)
    /// i reszta gry bez zmian. Rzut tylko, gdy oryginal i tak by biegl (__runOriginal) - doba rodu odrzucona przez ROT nie zuzywa generatora.
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

        private static Harmony _h;
        private static bool _tried, _patched;
        private static string _fail;
        private static bool _on;                 // regula czynna w tej sesji (klucz i latka)
        private static float _f = 1f;            // mnoznik doby rodu (84 / rok)
        private static int _year;                // rok kalendarza tej sesji (doby)
        // liczniki doby i sesji (watek kampanii)
        private static int _dayAsked, _dayPassed, _dayOffers, _totAsked, _totPassed, _totOffers, _stumbles, _stumblesDay;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_stumbles == 1) { try { Scribe.Report("CrashScribe", e, where, null); } catch { } }
        }

        private static string Num(double v) { return v.ToString("0.000", CultureInfo.InvariantCulture); }

        /// <summary>Start sesji kampanii (OnSessionLaunched): latka raz na proces, mnoznik z roku kalendarza tej sesji, linia startu.</summary>
        internal static void OnSession()
        {
            _on = false; _f = 1f; _year = 0;
            try
            {
                if (!_tried)
                {
                    _tried = true;
                    var m = AccessTools.DeclaredMethod(typeof(MarriageOfferCampaignBehavior), "DailyTickClan", new[] { typeof(Clan) });
                    var mOffer = AccessTools.DeclaredMethod(typeof(MarriageOfferCampaignBehavior), "CreateMarriageOffer", new[] { typeof(Hero), typeof(Hero) });
                    if (m == null) _fail = "brak MarriageOfferCampaignBehavior.DailyTickClan(Clan)";
                    else
                    {
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
                _on = true;
                bool beh = Campaign.Current != null && Campaign.Current.GetCampaignBehavior<MarriageOfferCampaignBehavior>() != null;
                Say(true, "MarriageOfferCampaignBehavior.DailyTickClan, Priority.Last; rok " + _year + " dob, doba rodu przechodzi z prawdopodobienstwem "
                          + Num(_f) + " = 84 / rok" + (_f >= 1f ? " (rok nie dluzszy niz 84 - bez rzutu, jak w grze)" : "")
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

        /// <summary>Prefiks MarriageOfferCampaignBehavior.DailyTickClan(Clan): doba rodu idzie do rzutow gry z prawdopodobienstwem 84 / rok.</summary>
        public static bool Prefix(Clan __0, bool __runOriginal)
        {
            if (!__runOriginal) return false;   // wczesniejszy prefiks (ROT MarriageCrash) juz zatrzymal oryginal - bez rzutu
            if (!_on || !(_f < 1f)) return true;
            try
            {
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
                                + " | doby rodow z para dla rodu gracza (po ROT) " + _dayAsked + ", przepuszczone " + _dayPassed
                                + " | oferty dzis " + _dayOffers + " | od wczytania: doby rodow " + _totAsked + ", przepuszczone " + _totPassed + ", oferty " + _totOffers
                                + (_stumblesDay > 0 ? " | POTKNIECIA " + _stumblesDay + " (razem " + _stumbles + ")" : "") + ".");
                }
            }
            catch (Exception e) { Stumble("MarriageOfferYear.DailyLine", e); }
            _dayAsked = _dayPassed = _dayOffers = 0; _stumblesDay = 0;
        }

        /// <summary>Nowa gra / wczytanie: regula stoi do OnSessionLaunched, liczniki od zera (latka zostaje - raz na proces).</summary>
        internal static void ResetSession()
        {
            _on = false; _f = 1f; _year = 0;
            _dayAsked = _dayPassed = _dayOffers = 0; _totAsked = _totPassed = _totOffers = 0; _stumblesDay = 0;
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
