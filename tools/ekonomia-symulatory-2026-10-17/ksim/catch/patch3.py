import os
HERE = os.path.dirname(os.path.abspath(__file__))
p = os.path.join(HERE, 'CatchK.cs'); s = open(p, encoding='utf-8').read()
a = '''                            if (ExcessSkip(g) || !(st.Dk[i] > 1.0)) continue;'''
b = '''                            if (ExcessSkip(g) || !(st.Dk[i] > 1.0)) continue;
                            if (st.P[i] >= PHi * 0.99 || st.P[i] <= PLo * 1.01) continue;   // brak / nadmiar strukturalny (cena na granicy) - nie jest praca do zdjecia ani oddania'''
assert s.count(a) == 1; s = s.replace(a, b)
open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('ok')
