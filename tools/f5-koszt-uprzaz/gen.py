# f5-koszt (noc 17/18.10) - uprzaz bit w bit diagnozy A7: wycina z plikow zrodlowych Armoury prawdziwe ciala F4A7Old (stary rachunek f4-pomiary) i F5Prep/F5Finish
# (szybki rachunek f5-koszt) oraz Px/Income/IncomeV z Rule.cs, wkleja je w zaslepki typow gry (stub.cs.txt) i buduje program net472 (ten sam runtime co gra).
# Program porownuje linie diagnozy bez czasu na 8 losowych swiatach (NaN w L, powtorzeni kandydaci, bez Planu, bez wartosci Planu, napisy zajec startowych
# jako inne instancje / null / spoza listy), pamiec G bloku wobec IncomeV, kwantyle przez wybor wobec List.Sort (NaN, -0/+0, powtorzenia) i mierzy czas.
# Uzycie (z katalogu tego pliku): python -I gen.py ../../Armoury/src gen.cs && dotnet build -c Release -v q --nologo && bin/Release/net472/h1.exe  (kod 0 = zgodne)
import re, sys, os
SRC = sys.argv[1]
OUT = sys.argv[2]

def read(name):
    return open(os.path.join(SRC, name), encoding='utf-8').read()

def extract(text, sig):
    """Wytnij deklaracje od linii zawierajacej sig do zamykajacej klamry (z komentarzem /// przed nia pominietym)."""
    i = text.find(sig)
    if i < 0:
        raise SystemExit('brak: ' + sig)
    ls = text.rfind('\n', 0, i) + 1
    j = text.find('{', i)
    # metoda w jednej linii z => albo { ... } w tej samej linii
    depth = 0; k = j
    while True:
        c = text[k]
        if c == '{': depth += 1
        elif c == '}':
            depth -= 1
            if depth == 0: break
        k += 1
    return text[ls:k + 1] + '\n'

f4 = read('StartCalibration.F4.cs')
f5 = read('StartCalibration.F5Koszt.cs')
ru = read('StartCalibration.Rule.cs')
parts = []
parts.append(extract(f4, 'private sealed class F4Var'))
parts.append(extract(f4, 'private static string F4A7Old('))
parts.append(extract(f4, 'private static void F4Eval('))
parts.append(extract(f4, 'private static double F4PlanInc('))
parts.append(extract(f4, 'private static bool F4Start('))
parts.append(extract(f4, 'private static void F4VarText('))
parts.append(re.search(r'\n(\s*private static string Pc\(int a, int n\).*?\n)', f4).group(1) + '\n')
parts.append(extract(f4, 'private static string F4Top('))
parts.append(extract(f4, 'private static string Q3('))
parts.append(extract(f4, 'private static string Qx('))
parts.append(re.search(r'\n(\s*private static string Qs\(List<double> sorted, double q\).*?\n)', f4).group(1) + '\n')
parts.append(extract(f5, 'private static double[] F5Row('))
parts.append(extract(f5, 'private static double F5Fill('))
parts.append(extract(f5, 'private sealed class F5Var'))
parts.append(extract(f5, 'private sealed class F5Job'))
parts.append(extract(f5, 'private static F5Job F5Prep('))
parts.append(extract(f5, 'private static string F5Finish('))
parts.append(extract(f5, 'private static void F5Eval('))
parts.append(extract(f5, 'private static string F4A7Fast('))
parts.append(extract(f5, 'private static string F5Q3('))
parts.append(extract(f5, 'private static string[] F5Quant('))
parts.append(extract(f5, 'private static double F5Sel('))
parts.append(extract(f5, 'internal static double F5Kth('))
parts.append(extract(ru, 'private static double Px(double[] px, int g)'))
parts.append(extract(ru, 'private static double Income(LiveBlock b'))
parts.append(extract(ru, 'private static double IncomeV(LiveBlock b'))
parts.append(extract(f5, 'private static string F5NoTime('))
body = ''.join(parts)
stub = open(os.path.join(os.path.dirname(__file__), 'stub.cs.txt'), encoding='utf-8').read()
open(OUT, 'w', encoding='utf-8').write(stub.replace('//@@BODY@@', body))
print('ok', len(body))
