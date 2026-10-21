using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// m105-optym (OptimSutlerNearest, domyslnie TAK; NIE = kod jak dotad 1:1): najblizsze miasto i wies markietanow (Sutlers.One) z tablic doby zamiast
    /// przegladu Town.AllTowns / Village.All z odczytem polozenia, stanu i frakcji kazdej osady na kazda z ok. 420 partii. Tablice budowane raz na poczatku
    /// petli partii Sutlers.Daily w tej samej kolejnosci co Town.AllTowns / Village.All, z tym samym filtrem (miasto: Settlement.IsTown; wies: stan Normal),
    /// to samo scisle porownanie odleglosci (d &gt;= najlepsza - dalej) i ten sam test wojny dopiero dla blizszej - przy remisie wygrywa ta sama osada.
    /// W petli partii nic nie zmienia polozenia osad, stanu wsi ani frakcji (zloto, liczniki, odziez z targu miasta). Rzad markietanow gracza (CampSutlers)
    /// zostaje na starej drodze (Sutlers.NearestTown / NearestVillage). Optim Verify: liczy tez stara droge, bierze JEJ wynik, roznice do linii "Optym (m105)".
    /// </summary>
    internal static class SutlerNear
    {
        private static bool _on, _verify;
        private static Settlement[] _tSt = new Settlement[0], _vSt = new Settlement[0];
        private static Vec2[] _tPos = new Vec2[0], _vPos = new Vec2[0];
        private static IFaction[] _tFac = new IFaction[0], _vFac = new IFaction[0];
        private static int _tN, _vN;
        private static int _askT, _askV, _cmp, _diff, _err;
        private static long _cmpSess, _diffSess;
        private static string _diffNote;

        /// <summary>Poczatek petli partii Sutlers.Daily: klucz tej doby i tablice. Wyjatek - stara droga na te dobe (potkniecie).</summary>
        internal static void Begin(Settings s)
        {
            _on = false; _verify = false; _askT = _askV = _cmp = _diff = _err = 0; _diffNote = null; _tN = _vN = 0;
            if (s == null || !s.OptimSutlerNearest) return;
            try
            {
                foreach (var t in Town.AllTowns)
                {
                    var st = t != null ? t.Settlement : null;
                    if (st == null || !st.IsTown) continue;
                    Put(ref _tSt, ref _tPos, ref _tFac, _tN++, st);
                }
                foreach (var v in Village.All)
                {
                    var st = v != null ? v.Settlement : null;
                    if (st == null || v.VillageState != Village.VillageStates.Normal) continue;
                    Put(ref _vSt, ref _vPos, ref _vFac, _vN++, st);
                }
                _on = true;
                _verify = s.OptimVerify;
            }
            catch (Exception e) { _on = false; _tN = _vN = 0; Stumble("tablice", e); }
        }

        private static void Put(ref Settlement[] st, ref Vec2[] pos, ref IFaction[] fac, int i, Settlement s)
        {
            if (i >= st.Length)
            {
                int n = Math.Max(16, st.Length * 2);
                Array.Resize(ref st, n); Array.Resize(ref pos, n); Array.Resize(ref fac, n);
            }
            st[i] = s; pos[i] = s.GetPosition2D; fac[i] = s.MapFaction;
        }

        /// <summary>Koniec petli partii: tablice puste (bez osad poprzedniej kampanii do nastepnej doby); linia doby przy LogEnabled.</summary>
        internal static void End(Settings s, int today)
        {
            bool was = _on;
            _on = false;
            Array.Clear(_tSt, 0, _tSt.Length); Array.Clear(_vSt, 0, _vSt.Length); Array.Clear(_tFac, 0, _tFac.Length); Array.Clear(_vFac, 0, _vFac.Length);
            int tn = _tN, vn = _vN; _tN = _vN = 0;
            try
            {
                if (s == null || !s.LogEnabled || !s.OptimSutlerNearest) return;
                Log.Info("Optym (m105): dzien " + today + " | Sutlers.Daily najblizsze osady: " + (was ? "z tablic doby (" + tn + " miast, " + vn + " wsi w stanie Normal), zapytan: miasta " + _askT + ", wsie " + _askV
                         + (_verify ? " | kontrola (Optim Verify) najblizsze osady: porownan " + _cmp + ", roznic " + _diff + " (sesja: porownan " + _cmpSess + ", roznic " + _diffSess + ")"
                                      + (_diffNote != null ? "; pierwsza: " + _diffNote : "") : "")
                                 : "stara droga (tablice nie powstaly)")
                         + " | potkniecia " + _err + ".");
            }
            catch { }
        }

        /// <summary>Najblizsze miasto (jak Sutlers.NearestTown).</summary>
        internal static Settlement NearTown(Vec2 p, IFaction faction)
        {
            if (!_on) return Sutlers.NearestTown(p, faction);
            _askT++;
            Settlement best = null; float bd = float.MaxValue;
            for (int i = 0; i < _tN; i++)
            {
                float d = p.DistanceSquared(_tPos[i]);
                if (d >= bd) continue;
                var mf = _tFac[i];
                if (faction != null && mf != null && FactionManager.IsAtWarAgainstFaction(faction, mf)) continue;
                bd = d; best = _tSt[i];
            }
            if (_verify) best = Check(best, Sutlers.NearestTown(p, faction), "miasto");
            return best;
        }

        /// <summary>Najblizsza wies w stanie Normal (jak Sutlers.NearestVillage).</summary>
        internal static Settlement NearVillage(Vec2 p, IFaction faction)
        {
            if (!_on) return Sutlers.NearestVillage(p, faction);
            _askV++;
            Settlement best = null; float bd = float.MaxValue;
            for (int i = 0; i < _vN; i++)
            {
                float d = p.DistanceSquared(_vPos[i]);
                if (d >= bd) continue;
                var mf = _vFac[i];
                if (faction != null && mf != null && FactionManager.IsAtWarAgainstFaction(faction, mf)) continue;
                bd = d; best = _vSt[i];
            }
            if (_verify) best = Check(best, Sutlers.NearestVillage(p, faction), "wies");
            return best;
        }

        /// <summary>Verify: wynik starej drogi wygrywa, roznica do licznika.</summary>
        private static Settlement Check(Settlement fast, Settlement old, string what)
        {
            _cmp++; _cmpSess++;
            if (!ReferenceEquals(fast, old))
            {
                _diff++; _diffSess++;
                if (_diffNote == null) _diffNote = what + " " + (fast != null ? fast.StringId : "brak") + " zamiast " + (old != null ? old.StringId : "brak");
            }
            return old;
        }

        private static readonly HashSet<string> _errLogged = new HashSet<string>();
        private static void Stumble(string site, Exception e)
        {
            _err++;
            try { if (_errLogged.Add(site)) Log.Error("SutlerNear(" + site + ")", e); } catch { }
        }

        /// <summary>Start kampanii / wczytanie: liczniki sesji od zera.</summary>
        internal static void Reset() { _cmpSess = _diffSess = 0; _errLogged.Clear(); _on = false; }
    }
}
