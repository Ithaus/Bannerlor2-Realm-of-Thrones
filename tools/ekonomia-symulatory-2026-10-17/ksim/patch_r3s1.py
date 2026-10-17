# KS-R3 sim round 1 fixes -> n-ks C# (Core.cs step 8 market correction, R3.cs controller, KsFoodCarts.cs NKS-22 counters). Exact replacements, LF kept.
import os, sys
N = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n-ks/Armoury/src'


def patch(fn, reps):
    p = os.path.join(N, fn)
    raw = open(p, 'rb').read()
    crlf = b'\r\n' in raw
    s = raw.decode('utf-8')
    if crlf: s = s.replace('\r\n', '\n')
    for a, b in reps:
        if s.count(a) != 1:
            print('ANCHOR NOT UNIQUE/FOUND in', fn, ':', a[:90].replace('\n', ' | '), 'count', s.count(a)); sys.exit(1)
        s = s.replace(a, b, 1)
    if crlf: s = s.replace('\n', '\r\n')
    open(p, 'wb').write(s.encode('utf-8'))
    print('patched', fn, len(reps), 'crlf' if crlf else 'lf')


# ---------------------------------------------------------------- Core.cs: G '-' takes back only home work above the village's own unpaid basket
patch('StartCalibration.Core.cs', [
('''                    // poprawka okregu G (bez nawijania): + wedlug udzialu w towarach w nadmiarze, - wedlug wartosci pracy domowej; G w [-HwV, HomeMax - HwV]
                    Array.Clear(sumSur, 0, NM); Array.Clear(sumHv, 0, NM);
                    foreach (var v in vs) { int m = v.D.Market == 0 ? 0 : 1; sumSur[m] += v.Sur; sumHv[m] += HomeOf(v.D) * v.V0; }''',
'''                    // poprawka okregu G (bez nawijania): + wedlug udzialu w towarach w nadmiarze, - wedlug pracy domowej DODANEJ przez '+'; G w [0, HomeMax - HwV]
                    // KS-R3 (port symulatora 17.10, runda 1): brak rynku (G '-') oddaje rynkowi tylko prace domowa ponad wlasny niezaplacony koszyk okregu (G > 0);
                    // praca domowa na wlasny koszyk (HwV) zostaje - wies najpierw zyje z wlasnej ziemi i rak, brak targu pokazuja ceny (dotad G do -HwV: rynek Essos
                    // zabieral rece pracy domowej wsiom, ktorych nie stac na koszyk - Plan bez pokrycia Essos 15.8% kcal, z ta zasada 3.3%)
                    Array.Clear(sumSur, 0, NM); Array.Clear(sumHv, 0, NM);
                    foreach (var v in vs) { int m = v.D.Market == 0 ? 0 : 1; sumSur[m] += v.Sur; sumHv[m] += Math.Max(0.0, v.G) * v.V0; }'''),
('''                        else if (e < 0 && sumHv[m] > 0) dG = 0.5 * e * HomeOf(v.D) / sumHv[m];
                        double g0 = v.G;
                        v.G = Math.Max(-v.HwV, Math.Min(inp.HomeMax - v.HwV, v.G + dG));''',
'''                        else if (e < 0 && sumHv[m] > 0) dG = 0.5 * e * Math.Max(0.0, v.G) / sumHv[m];
                        double g0 = v.G;
                        v.G = Math.Max(0.0, Math.Min(inp.HomeMax - v.HwV, v.G + dG));'''),
('''                bool stuck = rel < 0 && M.HwLeft[m] < 0.05 * Math.Abs(M.Excess[m]);      // praca domowa rynku (wartosc) mniejsza niz 5% braku - nie ma czego oddac''',
'''                bool stuck = rel < 0 && M.HwLeft[m] < 0.05 * Math.Abs(M.Excess[m]);      // praca domowa do oddania (G > 0, wartosc) mniejsza niz 5% braku - nie ma czego oddac'''),
])

# ---------------------------------------------------------------- R3.cs: controller averages restart at the end of the demand ramp, step and gap 10%
patch('StartCalibration.R3.cs', [
('''            Array.Clear(_c28, 0, _c28.Length); Array.Clear(_u28, 0, _u28.Length); _cN = 0; _cDay = -1; Array.Clear(_uDay, 0, _uDay.Length); _mf = null;''',
'''            Array.Clear(_c28, 0, _c28.Length); Array.Clear(_u28, 0, _u28.Length); _cN = 0; _cDay = -1; Array.Clear(_uDay, 0, _uDay.Length); _mf = null; _cRestart = -1;'''),
('''        private static int _cN, _cDay = -1;''',
'''        private static int _cN, _cDay = -1;
        // KS-R3 (port symulatora 17.10, runda 1): srednie zuzycia i braku od nowa w dobie konca rampy popytu (A + 28 = D + 14) - dotad srednia 28 dob z doby D+14 miala
        // w sobie 14 dob starej gospodarki przed D i 14 dob rampy (runda 3: zuzycie zboza przed D 6.4k/d Westeros i 3k/d Essos wobec 11k i 10k po rampie) - pierwsze
        // Plany sterowania ciely podaz o 7% na Plan; doba restartu w zapisie (R=)
        private static int _cRestart = -1;
        private const int CtlMinDays = 5;                 // najmniej tyle dob pelnej lambdy w srednich przed pierwsza zmiana czynnika
        private const double CtlStep = 0.10, CtlGap = 0.10; // krok czynnika na Plan i luka polek (runda 1 symulatora: 7% / 5% za wolno - zboze Essos 1.06 w oknie D+22..D+49)'''),
('''            _cDay = today;
            _cN++;
            double w = 1.0 / Math.Min(28, _cN);''',
'''            _cDay = today;
            int rampEnd = _state == StateApplied && DDay >= 0 ? DDay + (RampDemand - RampSupply) : -1;
            if (rampEnd >= 0 && today >= rampEnd && _cRestart != rampEnd) { _cN = 0; _cRestart = rampEnd; }   // tylko doby z pelnym popytem koszyka (lambda 1)
            _cN++;
            double w = 1.0 / Math.Min(28, _cN);'''),
('''            bool ctl = _state == StateApplied && DDay >= 0 && day >= DDay + 14 && _plan != null && _plan.Error == null && _plan.D != null;''',
'''            bool ctl = _state == StateApplied && DDay >= 0 && day >= DDay + 14 && _cRestart >= 0 && _cN >= CtlMinDays && _plan != null && _plan.Error == null && _plan.D != null;'''),
('''            sb.Append(PK).Append("sterowanie rynku doba ").Append(day).Append(ctl ? " | czynnik popytu targu z zuzycia i braku (28 dob)" : " | do D+14 albo bez klucza Ks Market Control: czynnik 1.03 (polki startu)");''',
'''            sb.Append(PK).Append("sterowanie rynku doba ").Append(day).Append(ctl ? " | czynnik popytu targu z zuzycia i braku (" + Math.Min(28, _cN) + " dob od konca rampy " + _cRestart + ")" : " | do D+14 + " + CtlMinDays + " dob pelnej lambdy albo bez klucza Ks Market Control: czynnik 1.03 (polki startu)");'''),
('''                        double gap = Math.Max(-0.05, Math.Min(0.05, (hs[i] - h[i]) / (28.0 * cu)));
                        double tgt = cu * (1.0 + gap) / dm;
                        k = Math.Max(_mf[i] * 0.93, Math.Min(_mf[i] * 1.07, tgt));''',
'''                        double gap = Math.Max(-CtlGap, Math.Min(CtlGap, (hs[i] - h[i]) / (28.0 * cu)));
                        double tgt = cu * (1.0 + gap) / dm;
                        k = Math.Max(_mf[i] * (1.0 - CtlStep), Math.Min(_mf[i] * (1.0 + CtlStep), tgt));'''),
('''            sb.Append("C=").Append(_cN);''',
'''            if (_cRestart >= 0) sb.Append("R=").Append(_cRestart).Append('\\n');
            sb.Append("C=").Append(_cN);'''),
('''                        case 'K': KsFoodCarts.ImportKx(v); break;''',
'''                        case 'R': { int rd; if (int.TryParse(v, out rd)) _cRestart = rd; break; }
                        case 'K': KsFoodCarts.ImportKx(v); break;'''),
])

# ---------------------------------------------------------------- KsFoodCarts.cs: NKS-22 counters once per destination x good x day (not per candidate source)
patch('KsFoodCarts.cs', [
('''                        cand.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Value.CompareTo(b.Value));
                        int used = 0;
                        foreach (var kv in cand)''',
'''                        cand.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Value.CompareTo(b.Value));
                        int used = 0;
                        // KS-R3 (port symulatora 17.10, runda 1): licznik NKS-22 raz na cel x towar x dobe - zamowione = pierwsza ilosc z kandydata, kasa celu za mala = czego
                        // z niej nie kupiono, gdy kasa zatrzymala choc jeden lot (dotad kazdy kandydat dopisywal ten sam brak jeszcze raz: 58-66% zamowionych w symulatorze)
                        long ord0 = -1, got0 = 0; bool poorHit = false;
                        foreach (var kv in cand)'''),
('''                            int n = (int)Math.Floor(q);
                            _dOrdered += n;''',
'''                            int n = (int)Math.Floor(q);
                            if (ord0 < 0) ord0 = n;'''),
('''                                if (nf < Math.Max(5.0, 0.5 * d)) { _dPoor += n; continue; }
                                _dPoor += n - nf; n = nf;
                                cost = LotPrice(_t[s], items[k], n, false); fr = (long)Math.Round(cost * tau * L);
                                if (cost + fr > budget[t] + 1) { _dPoor += n; continue; }''',
'''                                poorHit = true;
                                if (nf < Math.Max(5.0, 0.5 * d)) continue;
                                n = nf;
                                cost = LotPrice(_t[s], items[k], n, false); fr = (long)Math.Round(cost * tau * L);
                                if (cost + fr > budget[t] + 1) continue;'''),
('''                            int got = Ship(s, t, k, items[k], n, cost, fr, sea, L, today, pendFreight);
                            if (got <= 0) continue;
                            used++;''',
'''                            int got = Ship(s, t, k, items[k], n, cost, fr, sea, L, today, pendFreight);
                            if (got <= 0) continue;
                            got0 += got;
                            used++;'''),
('''                            offer[s * NZ + k] -= got; sh[s * NZ + k] -= got; Hc += got; budget[t] -= cost + fr;
                        }
                    }''',
'''                            offer[s * NZ + k] -= got; sh[s * NZ + k] -= got; Hc += got; budget[t] -= cost + fr;
                        }
                        if (ord0 > 0) { _dOrdered += ord0; if (poorHit) _dPoor += Math.Max(0, ord0 - got0); }
                    }'''),
('''            if (ro == null || dstT.Gold < cost + fr) { _dPoor += n; return 0; }''',
'''            if (ro == null || dstT.Gold < cost + fr) return 0;'''),
])
print('done')
