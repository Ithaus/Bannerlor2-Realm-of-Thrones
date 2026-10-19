# f26-wolni: diag/Core.cs = Armoury/src/StartCalibration.Core.cs + slad okregu ostatniej rundy kroku 8 (MoneyDebug2: alfa, budzet, koszyk, hw, V0, V0T, czesc pokryta
# przed/po kluczu Ks Home Work Trade Prices, kcal). Uruchomienie: python -I mkdiag.py (z tego katalogu), potem dotnet build h.csproj -c Release.
import os
here = os.path.dirname(os.path.abspath(__file__))
src = os.path.join(here, "..", "..", "..", "..", "Armoury", "src", "StartCalibration.Core.cs")
s = open(src, encoding="utf-8").read()
anchor = "                        lk[0] += fk; lk[1] += Math.Max(0.0, uk - hkc); lk[2] = m;\n"
assert s.count(anchor) == 1
add = anchor + '''                        if (MoneyDebug2 != null) { double pk2 = 0, okU = 0, nokU = 0; for (int z = 0; z < NZ; z++) { double kc = KsBasket.Kcal[z]; if (!(kc > 0)) continue; pk2 += paid[z] * q[z] * kc; if (v.Ok[z]) okU += u[z] * kc; else nokU += u[z] * kc; }
                            MoneyDebug2(d.Id + ";" + lnm + ";" + d.Type + ";" + v.Alpha.ToString("0.000") + ";" + budget.ToString("0") + ";" + bask.ToString("0") + ";" + pv.ToString("0") + ";" + hwc.ToString("0.000") + ";" + v.HwV.ToString("0.000") + ";" + v.G.ToString("0.000") + ";" + target.ToString("0.000") + ";" + v0.ToString("0") + ";" + raw.ToString("0") + ";" + rawF.ToString("0") + ";" + cov.ToString("0.000") + ";" + (fk/1e3).ToString("0") + ";" + (pk2/1e3).ToString("0") + ";" + (uk/1e3).ToString("0") + ";" + (okU/1e3).ToString("0") + ";" + (nokU/1e3).ToString("0") + ";" + (subK/1e3).ToString("0") + ";" + (subKeff/1e3).ToString("0") + ";" + (stK/1e3).ToString("0") + ";" + (hkc/1e3).ToString("0") + ";" + ((uk-hkc)/1e3).ToString("0") + ";" + (R.DistDietKcal[d.Id]/1e3).ToString("0") + ";" + subZ + ";" + v0T.ToString("0") + ";" + cov0.ToString("0.000") + ";" + ySub.ToString("0") + ";" + (double.IsInfinity(capSub) ? "inf" : capSub.ToString("0")) + ";" + kcSub.ToString("0") + ";" + (hkcBase/1e3).ToString("0") + ";" + hwSFin.ToString("0.000")); }
'''
s = s.replace(anchor, add)
a = "        internal static Action<int, string, string, int, double, double, double, double, double, double, double, double, double, double> MoneyDebug = null;\n"
assert s.count(a) == 1
s = s.replace(a, a + "        internal static Action<string> MoneyDebug2 = null;\n")
os.makedirs(os.path.join(here, "diag"), exist_ok=True)
open(os.path.join(here, "diag", "Core.cs"), "w", encoding="utf-8").write(s)
print("diag/Core.cs gotowy")
