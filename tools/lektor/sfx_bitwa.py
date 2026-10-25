# Efekty dzwiekowe bitwy (P2a, noc 25.10): wiersze glos=SFX ze scenariusza bitwa/klipy_bitwa.csv przez ElevenLabs Sound Effects
# (POST /v1/sound-generation, model eleven_text_to_sound_v2, duration_seconds z kolumny sek, loop dla petli: bebny, wiatr, deszcz, ogien, kolumna koni).
# Bebny, rogi i trabki wedlug kultur, tlum, kruki i ptaki, wiatr, deszcz, ogien, konie, szczek broni. Ta sama obrobka, manifest i kontrola co nagraj_bitwa.py.
# Kontrola kosztu: 1. generacja idzie sama, koszt z roznicy subskrypcji; > 60 kredytow/s albo naruszenie rezerwy (--rezerwa 30000) = stop reszty.
# Klucz z %USERPROFILE%\.elevenlabs\key.txt - nigdy nie wypisywany. Platne wywolania TYLKO z --nagraj (po zgodzie Jeffa).
# Uzycie: python -I tools/lektor/sfx_bitwa.py --sprawdz | --nagraj --out <katalog> [--prio 1|2|all] [--kategoria rog,beben] [--id ...] [--limit-sfx 120]
import importlib.util, os, sys

_p = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'nagraj_bitwa.py')
_spec = importlib.util.spec_from_file_location('nagraj_bitwa', _p)   # jawna sciezka - dziala tez z python -I
_nb = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_nb)

if __name__ == '__main__':
    sys.exit(_nb.main('sfx'))
