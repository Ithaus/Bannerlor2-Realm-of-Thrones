# Zewnetrzny straznik uprzezy: uruchamia harness.exe, jesli przez 15 s nie ma nowej linii - "ZAWIESZENIE", zabija proces, kod 3.
import subprocess, sys, threading, time, queue
exe, args, limit = sys.argv[1], sys.argv[2:], 15
p = subprocess.Popen([exe] + args, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
q = queue.Queue()
def rd():
    for line in p.stdout: q.put(line.rstrip())
    q.put(None)
threading.Thread(target=rd, daemon=True).start()
last = time.time(); lastline = ''
while True:
    try:
        line = q.get(timeout=1)
        if line is None:
            p.wait(); print('PROCES SKONCZYL kod', p.returncode, '| ostatnia:', lastline); sys.exit(p.returncode)
        lastline = line; last = time.time()
        if not line.startswith('T ') or line.split()[1].endswith('0'): print(line, flush=True)
    except queue.Empty:
        pass
    if time.time() - last > limit:
        print('ZAWIESZENIE: brak linii od %d s | ostatnia: %s' % (limit, lastline), flush=True)
        p.kill(); sys.exit(3)
