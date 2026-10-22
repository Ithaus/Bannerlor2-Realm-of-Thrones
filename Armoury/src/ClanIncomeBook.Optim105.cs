using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// m105-optym (OptimStableDelegates, domyslnie TAK; NIE = kod jak dotad 1:1): ClanIncomeBook.StableBegin (poczatek D stalego) wolal BK przez odbicie
    /// (MethodInfo.Invoke z new object[] i pakowaniem bool) ok. 570 razy dla rodow (GetActualVillages) i 2-3 razy na kazda wies (IsSettlementPopulated,
    /// podatek wsi BK, tytul wsi). Teraz te same metody przez delegaty zbudowane raz na proces (to samo wywolanie: ten sam obiekt, te same argumenty,
    /// wywolanie wirtualne jak Invoke). Delegatu nie da sie zbudowac (inny podpis BK) - odbicie jak dotad. Optim Verify: wszystkie cztery liczone obiema
    /// drogami - gra bierze stara (liczona druga, wiec ostatni zapis BK jak dotad), roznice do linii "Optym (m105)". Podatek wsi BK dwa razy jest bezpieczny:
    /// TownFolk.VillageTaxPostfix wisi na przeciazeniu (Village, int), nie na (Village, bool, bool); skutki uboczne BK (GetCouncil raz, DueWage = wynik x stala
    /// pozycji, polityka domyslna raz) sa powtarzalne, bez MBRandom; osada bez danych BK tu nie trafia (IsSettlementPopulated przed podatkiem).
    /// </summary>
    internal static partial class ClanIncomeBook
    {
        private static bool _cdOn, _cdVerify;
        private static Func<Clan, List<Village>> _cdActual;
        private static Func<object, Settlement, bool> _cdPop;
        private static Func<object, Village, bool, bool, ExplainedNumber> _cdVTax;
        private static Func<object, Settlement, object> _cdTitle;
        private static MethodInfo _cdActM, _cdPopM, _cdVTaxM, _cdTitleM;   // MethodInfo, z ktorych zbudowano delegaty (po ResolveBk - nowe = budowa od nowa)
        private static int _cdFast, _cdCmp, _cdDiff;
        private static long _cdCmpSess, _cdDiffSess;
        private static string _cdDiffNote;

        /// <summary>Poczatek StableBeginBody (po ResolveBk): klucz tej doby i delegaty.</summary>
        private static void CdBegin()
        {
            _cdFast = _cdCmp = _cdDiff = 0; _cdDiffNote = null;
            var s = Settings.Current;
            _cdOn = s != null && s.OptimStableDelegates;
            _cdVerify = _cdOn && s.OptimVerify;
            if (!_cdOn) return;
            try
            {
                if (!ReferenceEquals(_cdActM, _mActual))
                {
                    _cdActM = _mActual; _cdActual = null;
                    if (_mActual != null && _mActual.IsStatic && _mActual.ReturnType == typeof(List<Village>))
                        _cdActual = (Func<Clan, List<Village>>)Delegate.CreateDelegate(typeof(Func<Clan, List<Village>>), _mActual);
                }
                if (!ReferenceEquals(_cdPopM, _mPopulated)) { _cdPopM = _mPopulated; _cdPop = PerfFast.M1<Settlement, bool>(_mPopulated); }
                if (!ReferenceEquals(_cdTitleM, _mGetTitle)) { _cdTitleM = _mGetTitle; _cdTitle = PerfFast.M1<Settlement, object>(_mGetTitle); }
                if (!ReferenceEquals(_cdVTaxM, _mVTax)) { _cdVTaxM = _mVTax; _cdVTax = VTaxDelegate(_mVTax); }
            }
            catch (Exception e) { _cdActual = null; _cdPop = null; _cdTitle = null; _cdVTax = null; Stumble("Optim105(delegaty)", e); }
        }

        /// <summary>Metoda instancji (Village, bool, bool) -&gt; ExplainedNumber jako delegat (callvirt jak MethodInfo.Invoke); inny podpis - null.</summary>
        private static Func<object, Village, bool, bool, ExplainedNumber> VTaxDelegate(MethodInfo m)
        {
            if (m == null || m.IsStatic || m.ReturnType != typeof(ExplainedNumber)) return null;
            var ps = m.GetParameters();
            if (ps.Length != 3 || ps[0].ParameterType != typeof(Village) || ps[1].ParameterType != typeof(bool) || ps[2].ParameterType != typeof(bool)) return null;
            var o = Expression.Parameter(typeof(object), "o");
            var v = Expression.Parameter(typeof(Village), "v");
            var a = Expression.Parameter(typeof(bool), "a");
            var b = Expression.Parameter(typeof(bool), "b");
            return Expression.Lambda<Func<object, Village, bool, bool, ExplainedNumber>>(Expression.Call(Expression.Convert(o, m.DeclaringType), m, v, a, b), o, v, a, b).Compile();
        }

        /// <summary>BK GetActualVillages(c) - jak _mActual.Invoke(null, new object[] { c }).</summary>
        private static IEnumerable CdActual(Clan c)
        {
            if (!_cdOn || _cdActual == null) return _mActual.Invoke(null, new object[] { c }) as IEnumerable;
            _cdFast++;
            var l = _cdActual(c);
            if (!_cdVerify) return l;
            var old = _mActual.Invoke(null, new object[] { c }) as IEnumerable;
            bool same = SameSeq(l, old);
            CdNote(same, "wsie z tytulu rodu " + (c != null ? c.StringId : "?"));
            return old;
        }

        /// <summary>BK IsSettlementPopulated(st) - jak (bool)_mPopulated.Invoke(popMgr, new object[] { st }).</summary>
        private static bool CdPopulated(object popMgr, Settlement st)
        {
            if (!_cdOn || _cdPop == null) return (bool)_mPopulated.Invoke(popMgr, new object[] { st });
            _cdFast++;
            bool f = _cdPop(popMgr, st);
            if (!_cdVerify) return f;
            bool o = (bool)_mPopulated.Invoke(popMgr, new object[] { st });
            CdNote(f == o, "dane BK osady " + (st != null ? st.StringId : "?"));
            return o;
        }

        /// <summary>Podatek wsi BK - jak (ExplainedNumber)_mVTax.Invoke(tax, new object[] { v, false, false }); Verify - obie drogi, gra bierze stara (liczona druga).</summary>
        private static ExplainedNumber CdVTax(object tax, Village v)
        {
            if (!_cdOn || _cdVTax == null) return (ExplainedNumber)_mVTax.Invoke(tax, new object[] { v, false, false });
            _cdFast++;
            var f = _cdVTax(tax, v, false, false);
            if (!_cdVerify) return f;
            var o = (ExplainedNumber)_mVTax.Invoke(tax, new object[] { v, false, false });   // ostatni zapis DueWage - stara droga
            CdNote(f.ResultNumber == o.ResultNumber, "podatek wsi " + (v != null ? v.StringId : "?"));
            return o;
        }

        /// <summary>BK TitleManager.GetTitle(st) - jak _mGetTitle.Invoke(titleMgr, new object[] { st }).</summary>
        private static object CdTitle(object titleMgr, Settlement st)
        {
            if (!_cdOn || _cdTitle == null) return _mGetTitle.Invoke(titleMgr, new object[] { st });
            _cdFast++;
            var f = _cdTitle(titleMgr, st);
            if (!_cdVerify) return f;
            var o = _mGetTitle.Invoke(titleMgr, new object[] { st });
            CdNote(ReferenceEquals(f, o), "tytul osady " + (st != null ? st.StringId : "?"));
            return o;
        }

        private static bool SameSeq(IEnumerable a, IEnumerable b)
        {
            if (a == null || b == null) return a == null && b == null;
            var ea = a.GetEnumerator(); var eb = b.GetEnumerator();
            while (true)
            {
                bool na = ea.MoveNext(), nb = eb.MoveNext();
                if (na != nb) return false;
                if (!na) return true;
                if (!ReferenceEquals(ea.Current, eb.Current)) return false;
            }
        }

        private static void CdNote(bool same, string what)
        {
            _cdCmp++; _cdCmpSess++;
            if (same) return;
            _cdDiff++; _cdDiffSess++;
            if (_cdDiffNote == null) _cdDiffNote = what;
        }

        /// <summary>Koniec StableBeginBody: linia doby (przy LogEnabled i kluczu).</summary>
        private static void CdEnd()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.LogEnabled || !s.OptimStableDelegates) return;
                Log.Info("Optym (m105): dzien " + (int)CampaignTime.Now.ToDays + " | ClanIncomeBook.StableBegin wywolania BK przez delegaty: " + _cdFast
                         + " (delegaty: wsie z tytulu " + (_cdActual != null ? "tak" : "BRAK") + ", dane osady " + (_cdPop != null ? "tak" : "BRAK")
                         + ", podatek wsi " + (_cdVTax != null ? "tak" : "BRAK") + ", tytul " + (_cdTitle != null ? "tak" : "BRAK") + ")"
                         + (_cdVerify ? " | kontrola (Optim Verify) delegaty BK: porownan " + _cdCmp + ", roznic " + _cdDiff + " (sesja: porownan " + _cdCmpSess + ", roznic " + _cdDiffSess + ")"
                                        + (_cdDiffNote != null ? "; pierwsza: " + _cdDiffNote : "") : "") + ".");
            }
            catch { }
        }

        /// <summary>Start kampanii / wczytanie: liczniki sesji od zera.</summary>
        internal static void CdReset() { _cdCmpSess = _cdDiffSess = 0; }
    }
}
