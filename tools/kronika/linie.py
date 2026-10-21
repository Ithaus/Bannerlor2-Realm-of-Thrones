# Szacunek linii tekstu w oknie wydarzen ROT (ROT-Content EventXml: Galahad 25, szerokosc ~1004 px)
import re, sys, json
FNT = r'C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/GUI/GauntletUI/Fonts/Galahad/Galahad.fnt'
src = open(FNT, encoding='utf-8').read()
adv = {int(m.group(1)): int(m.group(2)) for m in re.finditer(r'<char id="(\d+)"[^>]*xadvance="(-?\d+)"', src)}
SIZE, FONT_SIZE, BASE = 32, 25, 29
SC = FONT_SIZE / SIZE
LINE_H = (BASE + 5) * SC
WIDTH = float(sys.argv[2]) if len(sys.argv) > 2 else 1004.0

def cw(c):
    return (adv.get(ord(c), adv[0]) + 0.5) * SC

def lines(text, width=WIDTH):
    words = text.split(' ')
    n, cur = 1, 0.0
    sp = cw(' ')
    for w in words:
        ww = sum(cw(c) for c in w)
        if cur == 0:
            cur = ww
        elif cur + sp + ww > width:
            n += 1; cur = ww
        else:
            cur += sp + ww
    return n, cur

def report(items):
    out = []
    for p in items:
        u = p.get('UpperText', ''); l = p.get('LowerText', '')
        nu, _ = lines(u); nl, _ = lines(l)
        out.append((nu + nl, nu, nl, len(u), len(l), p.get('type', p.get('id', '')), p.get('Title', '')))
    return out

if __name__ == '__main__':
    data = json.load(open(sys.argv[1], encoding='utf-8'))
    if isinstance(data, dict) and 'scenes' in data:
        data = [dict(type=s['id'], Title=s['Title'], UpperText=s['UpperText'], LowerText=s['LowerText']) for s in data['scenes']]
    sample = ' '.join(p.get('UpperText', '') + ' ' + p.get('LowerText', '') for p in data)
    avg = sum(cw(c) for c in sample) / max(1, len(sample))
    print(f'line height {LINE_H:.2f}px, avg char {avg:.2f}px -> ~{WIDTH/avg:.0f} chars/line at width {WIDTH}')
    for r in sorted(report(data), reverse=True):
        print(f'tot{r[0]:>2} U{r[1]} L{r[2]}  chars U{r[3]:>4} L{r[4]:>4}  {r[5]} | {r[6]}')
