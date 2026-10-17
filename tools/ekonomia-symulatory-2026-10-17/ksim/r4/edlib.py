# edit helper: exact-anchor replacement with count == 1 assertion; LF/CRLF kept as in file
import sys
def rep(path, pairs):
    s = open(path, encoding='utf-8', newline='').read()
    crlf = '\r\n' in s
    if crlf: s = s.replace('\r\n', '\n')
    for a, b in pairs:
        n = s.count(a)
        if n != 1:
            print('ANCHOR COUNT', n, 'in', path, ':', a[:120].replace('\n', '|'))
            sys.exit(1)
        s = s.replace(a, b)
    if crlf: s = s.replace('\n', '\r\n')
    open(path, 'w', encoding='utf-8', newline='').write(s)
    print('ok', path, len(pairs))
