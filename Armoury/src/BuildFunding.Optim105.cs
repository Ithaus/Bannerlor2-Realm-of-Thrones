using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// m105-optym (OptimBuildWar, domyslnie TAK; NIE = kod jak dotad 1:1): w BuildFunding.Daily "czy krolestwo pana warowni jest w wojnie z innym krolestwem"
    /// (przeglad Kingdom.All z IsAtWarWith - wstrzymanie budowli cywilnych, wpis 86) liczone raz na krolestwo w petli warowni zamiast raz na kazda warownie
    /// z budowa. W tej petli nic nie zmienia stanu wojny (tylko odczyty kolejki budow i WarFront.CivilAllowed); pamiec czyszczona na poczatku i na koncu
    /// petli (wzor m99 KingdomTreasury.WarOf). Optim Verify: liczy tez stara droge, bierze JEJ wynik, roznice do linii "Optym (m105)".
    /// </summary>
    internal static partial class BuildFunding
    {
        private static readonly Dictionary<Kingdom, bool> _bfWar = new Dictionary<Kingdom, bool>();
        private static bool _bfOn, _bfVerify;
        private static int _bfAsk, _bfHit, _bfCmp, _bfDiff;
        private static long _bfCmpSess, _bfDiffSess;

        /// <summary>Poczatek petli warowni BuildFunding.Daily.</summary>
        private static void BfWarBegin(Settings s)
        {
            _bfWar.Clear();
            _bfOn = s != null && s.OptimBuildWar;
            _bfVerify = _bfOn && Optim105.VerifyOn(s);
            _bfAsk = _bfHit = _bfCmp = _bfDiff = 0;
        }

        /// <summary>Stara droga 1:1 (kod sprzed m105 z BuildFunding.Daily): przeglad Kingdom.All, wyjatek - to, co ustalone przed nim.</summary>
        private static bool BfWarOld(Kingdom ck)
        {
            bool warNow = false;
            try { if (ck != null) foreach (var k in Kingdom.All) if (k != ck && !k.IsEliminated && ck.IsAtWarWith(k)) { warNow = true; break; } } catch { }
            return warNow;
        }

        /// <summary>Wojna krolestwa pana warowni: przy OptimBuildWar raz na krolestwo w tej petli, inaczej stara droga. Verify - zwraca wynik starej drogi.</summary>
        private static bool BfWarOf(Kingdom ck)
        {
            if (!_bfOn || ck == null) return BfWarOld(ck);
            _bfAsk++;
            bool w;
            if (_bfWar.TryGetValue(ck, out w)) _bfHit++;
            else { w = BfWarOld(ck); _bfWar[ck] = w; }
            if (_bfVerify)
            {
                bool o = BfWarOld(ck);
                _bfCmp++; _bfCmpSess++;
                if (o != w) { _bfDiff++; _bfDiffSess++; w = o; }
            }
            return w;
        }

        /// <summary>Koniec petli warowni: pamiec puszczona; linia kontrolna tylko przy Optim Verify.</summary>
        private static void BfWarEnd(Settings s)
        {
            _bfWar.Clear();
            try
            {
                if (!_bfVerify || s == null || !s.LogEnabled) return;
                Log.Info("Optym (m105): dzien " + (int)CampaignTime.Now.ToDays + " | kontrola (Optim Verify) BuildFunding.Daily wojna krolestwa: zapytan " + _bfAsk + ", z pamieci " + _bfHit
                         + ", porownan " + _bfCmp + ", roznic " + _bfDiff + " (sesja: porownan " + _bfCmpSess + ", roznic " + _bfDiffSess + ").");
            }
            catch { }
        }

        /// <summary>Start kampanii / wczytanie: liczniki sesji od zera.</summary>
        internal static void BfWarReset() { _bfWar.Clear(); _bfCmpSess = _bfDiffSess = 0; }
    }
}
