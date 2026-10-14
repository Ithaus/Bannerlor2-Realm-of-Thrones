# po scaleniu: kazda dodana linia galezi w pliku (bez komentarzy) jest w wyniku i w tej samej metodzie co w galezi
import re,subprocess,sys
f,base,br=sys.argv[1],sys.argv[2],sys.argv[3]
def meth(lines,i):
    for k in range(i,-1,-1):
        if re.match(r'\s{8}(public|private|internal|protected)[^=;]*\(',lines[k]): return lines[k].strip()[:70]
    return '?'
a=subprocess.run(['git','show',br+':'+f],capture_output=True,text=True,encoding='utf-8').stdout.splitlines()
b=open(f,encoding='utf-8').read().splitlines()
diff=subprocess.run(['git','diff',base,br,'--',f],capture_output=True,text=True,encoding='utf-8').stdout
added=[l[1:].rstrip('\r') for l in diff.splitlines() if l.startswith('+') and not l.startswith('+++') and l[1:].strip() and not l[1:].strip().startswith('//')]
bad=0
for s in added:
    ia=[i for i,l in enumerate(a) if l.rstrip('\r')==s]; ib=[i for i,l in enumerate(b) if l.rstrip('\r')==s]
    if not ib: print('BRAK:',s[:90]); bad+=1; continue
    if ia and meth(a,ia[0])!=meth(b,ib[0]): print('INNA METODA:',s[:70],'|',meth(a,ia[0]),'|',meth(b,ib[0])); bad+=1
print(f,'dodanych linii',len(added),'problemow',bad)
