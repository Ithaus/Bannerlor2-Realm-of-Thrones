using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// m99-optym (noc 20/21.10, PLAN-OPTYMALIZACJA-NOC pkt 1): te same wyniki, mniej pracy na przelomie doby - wylacznik OptimDailyCache (domyslnie TAK;
    /// NIE = kod jak dotad 1:1), tryb kontrolny OptimVerify (domyslnie NIE): liczy obie drogi, bierze wynik starej i liczy roznice (linia "Optym (m99)").
    /// (1) KingdomTreasury.Daily: "czy krolestwo rodu jest w wojnie z innym krolestwem" liczone raz na krolestwo w petli rodow zamiast raz na rod
    ///     (dotad kazdy z ok. 540 rodow przegladal Kingdom.All). W petli nic nie zmienia stanu wojny (ChangeHeroGold, KingdomBudgetWallet, liczniki,
    ///     model finansow bez pobrania) - ta sama odpowiedz dla kazdego rodu tego krolestwa; pamiec czyszczona na poczatku kazdej doby.
    /// </summary>
    internal static partial class KingdomTreasury
    {
        private static readonly Dictionary<Kingdom, bool> _optWar = new Dictionary<Kingdom, bool>();
        private static bool _optOn, _optVerify;
        // liczniki doby (linia kontrolna) i sesji
        private static int _optWarAsk, _optWarHit, _optWarCmp, _optWarDiff;
        private static long _optWarDiffSess, _optWarCmpSess;

        /// <summary>Poczatek petli rodow KingdomTreasury.Daily: pamiec doby od zera, klucze z ustawien tej doby.</summary>
        private static void OptBegin(Settings s)
        {
            _optWar.Clear();
            _optOn = s != null && s.OptimDailyCache;
            _optVerify = _optOn && s.OptimVerify;
            _optWarAsk = _optWarHit = _optWarCmp = _optWarDiff = 0;
        }

        /// <summary>Stara droga 1:1 (kod sprzed m99 z KingdomTreasury.Daily): przeglad Kingdom.All, wyjatek - falsz albo to, co ustalone przed nim.</summary>
        private static bool WarOld(Kingdom ck)
        {
            bool war = false;
            try { foreach (var k in Kingdom.All) if (k != ck && !k.IsEliminated && ck.IsAtWarWith(k)) { war = true; break; } } catch { }
            return war;
        }

        /// <summary>Wojna krolestwa rodu: przy OptimDailyCache raz na krolestwo na dobe (pamiec), inaczej stara droga. OptimVerify - liczy tez stara droge
        /// i zwraca JEJ wynik (gra jak przy kluczu NIE), roznica tylko do licznika.</summary>
        private static bool WarOf(Kingdom ck)
        {
            if (!_optOn || ck == null) return WarOld(ck);
            _optWarAsk++;
            bool w;
            if (_optWar.TryGetValue(ck, out w)) _optWarHit++;
            else { w = WarOld(ck); _optWar[ck] = w; }
            if (_optVerify)
            {
                bool o = WarOld(ck);
                _optWarCmp++; _optWarCmpSess++;
                if (o != w) { _optWarDiff++; _optWarDiffSess++; w = o; }
            }
            return w;
        }

        /// <summary>Koniec KingdomTreasury.Daily: pamiec doby puszczona (recenzja m99: bez przytrzymania krolestw do nastepnej doby / innej kampanii),
        /// linia kontrolna tylko przy OptimVerify (bez niej log jak dotad).</summary>
        private static void OptEnd(Settings s)
        {
            try
            {
                _optWar.Clear();   // WarOf wolany tylko w petli rodow (po OptBegin) - po petli pamiec niepotrzebna
                if (!_optVerify || s == null || !s.LogEnabled) return;
                Log.Info("Optym (m99): dzien " + (int)CampaignTime.Now.ToDays + " | kontrola (Optim Verify) KingdomTreasury.Daily wojna krolestwa: zapytan " + _optWarAsk
                         + ", z pamieci " + _optWarHit + ", porownan " + _optWarCmp + ", roznic " + _optWarDiff
                         + " (sesja: porownan " + _optWarCmpSess + ", roznic " + _optWarDiffSess + ").");
            }
            catch { }
        }

        /// <summary>Linia startu kampanii: ktore optymalizacje m99 sa wlaczone.</summary>
        internal static void OptimStartLine()
        {
            var s = Settings.Current;
            if (s == null) return;
            _optWar.Clear(); _optWarDiffSess = 0; _optWarCmpSess = 0;   // recenzja m99: nowa kampania / wczytanie - bez krolestw poprzedniej sesji
            try { KtSkipReset(); Optim105.StartLine(); } catch (Exception e) { Log.Error("Optim105.StartLine", e); }   // m105-optym: liczniki sesji i linia "Optym (m105): start"
            Log.Info("Optym (m99): start | Optim Daily Cache " + (s.OptimDailyCache ? "TAK" : "NIE") + ", Optim Verify " + (s.OptimDailyCache && s.OptimVerify ? "TAK" : "NIE")
                     + " | wlaczone: " + (s.OptimDailyCache ? "KingdomTreasury.Daily - wojna krolestwa raz na krolestwo na dobe (zamiast przegladu Kingdom.All na kazdy rod)" : "brak (kod jak dotad)")
                     + " | bez zmian (wynik zalezalby od kolejnosci albo skutkow ubocznych modelu finansow - docs/noc-1920/m99-optym-CHANGELOG.md): ClanIncomeBook.Daily, SupplyDemand.DailyTrade, Sutlers.Daily, model dochodu w KingdomTreasury.Daily.");
        }
    }
}
