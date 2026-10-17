import os
HERE = os.path.dirname(os.path.abspath(__file__))
p = os.path.join(HERE, 'ProgramK.cs'); s = open(p, encoding='utf-8').read()
a = '''        KI.NE = ci.Count; KI.CI = ci.ToArray();'''
b = '''        { var tn = Environment.GetEnvironmentVariable("KS_TOPN"); int topn; if (!string.IsNullOrEmpty(tn) && int.TryParse(tn, out topn) && topn > 0)
          {
            var keepIx = new List<int>();
            for (int e = 0; e < ci.Count; e++) if (ct[e] == 0) keepIx.Add(e);
            var byJ = new Dictionary<int, List<int>>();
            for (int e = 0; e < ci.Count; e++) if (ct[e] != 0) { List<int> l; if (!byJ.TryGetValue(cjj[e], out l)) { l = new List<int>(); byJ[cjj[e]] = l; } l.Add(e); }
            foreach (var kv in byJ) { kv.Value.Sort((a1, b1) => cdist[a1].CompareTo(cdist[b1])); for (int q = 0; q < Math.Min(topn, kv.Value.Count); q++) keepIx.Add(kv.Value[q]); }
            keepIx.Sort();
            ci = keepIx.Select(e => ci[e]).ToList(); cjj = keepIx.Select(e => cjj[e]).ToList(); ct = keepIx.Select(e => ct[e]).ToList(); cd = keepIx.Select(e => cd[e]).ToList();
            cdist = keepIx.Select(e => cdist[e]).ToList(); cr = keepIx.Select(e => cr[e]).ToList(); ca = keepIx.Select(e => ca[e]).ToList();
          } }
        KI.NE = ci.Count; KI.CI = ci.ToArray();'''
assert s.count(a) == 1; s = s.replace(a, b)
open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('ok')
