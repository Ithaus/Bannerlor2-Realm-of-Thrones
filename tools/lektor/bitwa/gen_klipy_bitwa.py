# Scenariusz nagran dzwieku bitwy (P2a, noc 25.10) -> klipy_bitwa.csv obok skryptu (albo sciezka z argumentu).
# Rozszerza gen_klipy.py z rozpoznania (te same id dla starych wierszy) o polecenia Jeffa z 25.10: przemowa przed bitwa DLA RODOW,
# motta wodzow przy szarzy, jeki rannych po bitwie roznymi glosami, kruki i ptaki, rogi Dothrakow, wiatr, deszcz, ogien, konie, szczek broni.
# Kolumny (srednik = separator, w tekstach go nie ma):
#   id;kategoria;kultura/rod;tekst;glos;uwagi;prio;model;sek;lufs;loop;zakres
#   glos  = rola z glosy_bitwa.json (L_* wodz rodu/regionu, C_* kapitan regionu, S_* szeregowy) albo SFX (ElevenLabs Sound Effects)
#   model = v3 (eleven_v3, tagi [shouting] itd. w tekscie) | v2 (eleven_multilingual_v2, tagi wycinane) | sfx
#   prio  = 1 (v1 na 12:00) | 2 (zapas: drugie linie i drugie glosy - nagrac, gdy zostana kredyty)
#   sek   = dlugosc SFX (duration_seconds); lufs = cel glosnosci (ffmpeg loudnorm, TP -1 dBTP); loop = 1 dla petli SFX
#   zakres = rod (klucz rodu z nazwy klanu, jak ROTBA HouseWordsResolver) | region (klucz grupy kultur, mapa w BattleSound) | wrog (cel wyzwiska) | - (kazdy)
# Uzycie: python -I gen_klipy_bitwa.py [sciezka_csv]
import os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
rows = []

LUFS = {'przemowa': -18, 'motto': -14, 'rozkaz': -14, 'wyzwisko': -15, 'zlamanie': -14, 'sztandar': -14, 'zwyciestwo': -15, 'jek': -20,
        'beben': -20, 'rog': -16, 'tlum': -18, 'ptaki': -22, 'wiatr': -26, 'deszcz': -24, 'ogien': -22, 'kon': -18, 'szczek': -18}


def add(cid, kat, kul, txt, glos, uw='', prio=1, model=None, sek='', loop=0, zakres='-'):
    if model is None:
        model = 'sfx' if glos == 'SFX' else 'v3'
    uw = uw.replace(';', ' |')
    cells = (cid, kat, kul, txt, glos, uw, str(prio), model, str(sek), str(LUFS[kat]), str(loop), zakres)
    for s in cells:
        assert ';' not in s and '\n' not in s, cid
    assert (glos == 'SFX') == (model == 'sfx'), cid
    if model == 'sfx':
        assert sek and 0.5 <= float(sek) <= 30, cid
    rows.append(cells)


REGIONY = ('north', 'west', 'crown', 'river', 'vale', 'reach', 'storm', 'dragonstone', 'iron', 'dorne', 'nightswatch', 'freefolk', 'skagosi',
           'dothraki', 'ghiscari', 'valyrian', 'essos', 'goldencompany')

# ---------------------------------------------------------------- PRZEMOWY RODOW (Jeff 01:04: "przemowe przed bitwa dla kazdego rodu"); wodz rodu, 4-8 s
HSP = [
    ('stark', 'L_STARK', "Men of Winterfell! The wolf never hunts alone. Stay with the pack, and they will not see spring.", 1),
    ('lannister', 'L_LANNISTER', "Men of the Westerlands! You are paid in gold. Today, earn it in blood. Hear me roar!", 1),
    ('tully', 'L_TULLY', "Family, duty, honor. Hold to all three today, and Riverrun will remember every one of your names.", 1),
    ('arryn', 'L_ARRYN', "Knights of the Vale, the Eyrie has never fallen. Fight as high as honor, and it never shall.", 1),
    ('tyrell', 'L_TYRELL', "The Reach feeds this realm, and today it fights for it. Lances down, and let the roses grow strong!", 1),
    ('baratheon', 'L_BARATHEON', "Stormlanders! You know the fury. You were born with it. Now let it loose on these fools!", 1),
    ('martell', 'L_MARTELL', "Dorne has outlasted dragons. These men are nothing. Unbowed, unbent, unbroken. Make them remember it.", 1),
    ('greyjoy', 'L_GREYJOY', "We are ironborn, lads! What is dead may never die. Now go and take what they think is theirs!", 1),
    ('targaryen', 'L_TARGARYEN', "We fight for the blood of the dragon. Fire and blood, and let no man forget his oath.", 1),
    ('frey', 'L_FREY', "Fight well, and you shall have my bread and salt. Run, and you shall have my rope.", 1),
    ('bolton', 'L_BOLTON', "Quietly now. Let them come. A naked man has few secrets. A flayed man has none.", 1),
    ('mormont', 'L_NORTH_OLD', "Bear Island stands with you. Here we stand, and here we hold, and not one of them passes.", 1),
    ('umber', 'L_NORTH_OLD', "Last Hearth! Break their shields, break their heads, and leave the rest for the crows!", 1),
    ('karstark', 'L_STARK', "The sun of winter rises today, lads. Let them feel how cold its light can be.", 1),
    ('manderly', 'L_NORTH_OLD', "White Harbor remembers its friends and its enemies. Today we settle accounts with these.", 1),
    ('tarly', 'L_STANNIS', "First in battle, men of Horn Hill. I want discipline, not glory. Hold your ranks, and kill.", 1),
    ('hightower', 'L_TYRELL', "Oldtown has lit the way for a thousand years. Today we light it for the realm.", 1),
    ('royce', 'L_ARRYN', "Runestone remembers the First Men. Bronze and stone, and not one step back!", 1),
    ('mallister', 'L_TULLY', "Seagard never yielded to reaver or lord. Above the rest, men! Above the rest!", 1),
    ('dayne', 'L_MARTELL', "The Sword of the Morning watches over us. Fight cleanly, fight bravely, and fight for Starfall.", 1),
    ('yronwood', 'L_MARTELL', "We guard the way. Not one of them reaches the sands alive. Not one.", 1),
    ('velaryon', 'L_TARGARYEN', "The old, the true, the brave. The lords of Driftmark do not turn their backs.", 1),
    ('westerling', 'L_ARRYN', "Honor, not honors, men of the Crag. Fight well, and let others count the gold.", 1),
    ('crakehall', 'L_LANNISTER', "None so fierce! Boars of Crakehall, root them out and trample them down!", 1),
    ('glover', 'L_STARK', "Deepwood Motte has stood against reavers for a thousand years. Show them why.", 2),
    ('tarth', 'L_BARATHEON', "Evenfall has sent its sons to every war. Stand fast, and we bring them home.", 2),
    ('harlaw', 'L_GREYJOY', "Harlaw men! Pay the iron price, and drink their wine tonight!", 2),
    ('blackwood', 'C_RIVER', "The old gods are watching from the weirwoods, men of Raventree. Let them see how we fight.", 2),
    ('bracken', 'C_STORM', "Stone Hedge does not bend. Remember who you are, and drive these dogs into the river.", 2),
]
for kul, v, t, p in HSP:
    add('arm_bs_sp_%s_01' % kul, 'przemowa', kul, t, v, 'przemowa rodu przed starciem (wodz-bohater tego rodu); 4-8 s; -18 LUFS; 3D przy dowodcy 40-300 m',
        prio=p, model='v2', zakres='rod')

# ---------------------------------------------------------------- PRZEMOWY REGIONOW (zapas, gdy rod dowodcy nie ma wlasnej) - stare id z rozpoznania
SP = [
    ('north', 'L_NORTH_OLD', ["Men of the North! They think us few and cold. Show them how the winter bites!",
                              "Stand with me, brothers. No Southron lord takes one foot of our land today."]),
    ('west', 'L_LANNISTER', ["Steady, men of the Rock. A Lannister pays his debts, and today we collect.",
                             "Look at them. Farmers with sticks. Show them what gold and steel can buy."]),
    ('crown', 'L_LANNISTER', ["For the King and for the realm! Hold your ranks and the day is ours."]),
    ('river', 'L_TULLY', ["Our fields are burned and our people hanged. Today the Riverlands answer!",
                          "Remember the villages, lads. Every one of them. Now make them pay."]),
    ('vale', 'L_ARRYN', ["Knights of the Vale! Our fathers never knelt, and neither shall we.",
                         "They came down from no mountain. We did. Show them the falcon's talons."]),
    ('reach', 'L_TYRELL', ["The Reach has fed this realm for a thousand years. Today it bleeds for it.",
                           "Banners high, lances low! Let the roses show their thorns!"]),
    ('storm', 'L_BARATHEON', ["Stormlanders! We were born in the rain and the thunder. Let them hear it!",
                              "No storm ever broke us. These men will not either. With me!"]),
    ('dragonstone', 'L_STANNIS', ["There is one true king, and we march in his name. The night is dark, but we are not afraid.",
                                  "Fight for your king, and the Lord of Light will see you home."]),
    ('iron', 'L_GREYJOY', ["We do not sow, lads. We take. And today we take everything!",
                           "The sea gave us iron. Now give them a taste of it!"]),
    ('dorne', 'L_MARTELL', ["Dornishmen do not break. Let them come, and let them learn why.",
                            "They crossed our mountains to die in our sand. Let us oblige them."]),
    ('nightswatch', 'L_WATCH', ["Brothers! We are the shield that guards the realms of men. Hold the line!",
                                "We took our vows for a night like this. Make your oath worth something."]),
    ('freefolk', 'L_WILD', ["Look at them, all in a row like sheep! Free folk, show them what free means!",
                            "No kings, no crows, no masters! Today we take what is ours!"]),
    ('skagosi', 'C_WILD', ["Skagos remembers every insult. Let us repay them all at once."]),
    ('dothraki', 'L_DOTHRAKI', ["Khalakka dothrae! The stallion rides, and the earth trembles!",
                                "These men walk on their own feet like slaves. Ride them down!"]),
    ('ghiscari', 'L_UNSULLIED', ["Unsullied! You know no fear. You know no pain. Only the spear. Forward!"]),
    ('valyrian', 'L_TARGARYEN', ["For the dragon queen! Fire and blood, and no mercy for her enemies!",
                                 "The dragons have come home. Fight as if they are watching, for they are."]),
    ('essos', 'L_ESSOS', ["You are paid for today, every one of you. Earn it, and there is more.",
                          "Hold your line and fill your purse. Run, and I will hang you myself."]),
    ('goldencompany', 'L_TULLY', ["We have waited a hundred years to come home. Beneath the gold, the bitter steel!"]),
]
assert [k for k, _, _ in SP] == list(REGIONY)
for kul, v, lines in SP:
    for i, t in enumerate(lines, 1):
        add('arm_bs_sp_%s_%02d' % (kul, i), 'przemowa', kul, t, v, 'przemowa regionu (zapas, gdy rod dowodcy nie ma swojej); 4-8 s; -18 LUFS; 3D przy dowodcy 40-300 m',
            prio=1 if i == 1 else 2, model='v2', zakres='region')

# ---------------------------------------------------------------- MOTTA RODOW (Jeff 01:05: "niech wodzowie mowia swoje motta przy szarzy"); glos wodza rodu
MOT = [
    ('stark', 'L_STARK', ["Winter is coming!", "The North remembers!"], 'kanon (slowa rodu; The North remembers - Manderly)'),
    ('lannister', 'L_LANNISTER', ["Hear me roar!", "For Casterly Rock!"], 'kanon'),
    ('tully', 'L_TULLY', ["Family! Duty! Honor!", "For Riverrun!"], 'kanon'),
    ('arryn', 'L_ARRYN', ["As high as honor!", "For the Eyrie!"], 'kanon'),
    ('royce', 'L_ARRYN', ["We remember!"], 'kanon'),
    ('tyrell', 'L_TYRELL', ["Growing strong!", "For Highgarden!"], 'kanon'),
    ('tarly', 'L_STANNIS', ["First in battle!"], 'kanon'),
    ('hightower', 'L_TYRELL', ["We light the way!"], 'kanon'),
    ('baratheon', 'L_BARATHEON', ["Ours is the fury!", "For Storm's End!"], 'kanon'),
    ('dragonstone', 'L_STANNIS', ["For the one true king!", "For King Stannis!"], 'Stannis - drugi tylko gdy Stannis zyje (FABULA)'),
    ('martell', 'L_MARTELL', ["Unbowed! Unbent! Unbroken!", "For Sunspear!"], 'kanon'),
    ('dayne', 'L_MARTELL', ["For Starfall!"], 'bez slow rodu w kanonie'),
    ('yronwood', 'L_MARTELL', ["We guard the way!"], 'kanon'),
    ('greyjoy', 'L_GREYJOY', ["What is dead may never die!", "We do not sow!"], 'kanon (modlitwa Utopionego Boga i slowa rodu)'),
    ('harlaw', 'L_GREYJOY', ["For Harlaw and the Drowned God!"], 'bez slow rodu w kanonie'),
    ('targaryen', 'L_TARGARYEN', ["Fire and blood!", "Dracarys!"], 'kanon; Dracarys tylko przy smoku na polu (ROT-Dragon) - inaczej Fire and blood'),
    ('frey', 'L_FREY', ["For the Twins!"], 'Freyowie bez slow rodu w kanonie'),
    ('bolton', 'L_BOLTON', ["Our blades are sharp!"], 'kanon; glos zimny, cichy'),
    ('mormont', 'L_NORTH_OLD', ["Here we stand!"], 'kanon'),
    ('umber', 'L_NORTH_OLD', ["For Last Hearth!"], 'bez slow rodu w kanonie'),
    ('karstark', 'L_STARK', ["The sun of winter!"], 'kanon'),
    ('manderly', 'L_NORTH_OLD', ["For White Harbor!"], 'bez slow rodu w kanonie'),
    ('glover', 'L_STARK', ["For Deepwood Motte!"], 'bez slow rodu w kanonie'),
    ('mallister', 'L_TULLY', ["Above the rest!"], 'kanon'),
    ('blackwood', 'C_RIVER', ["For Raventree Hall!"], 'bez slow rodu w kanonie'),
    ('bracken', 'C_STORM', ["For Stone Hedge!"], 'bez slow rodu w kanonie'),
    ('velaryon', 'L_TARGARYEN', ["The old, the true, the brave!"], 'kanon'),
    ('crakehall', 'L_LANNISTER', ["None so fierce!"], 'kanon'),
    ('marbrand', 'L_LANNISTER', ["Burning bright!"], 'kanon'),
    ('westerling', 'L_ARRYN', ["Honor, not honors!"], 'kanon'),
    ('tarth', 'L_BARATHEON', ["For Evenfall!"], 'bez slow rodu w kanonie'),
    ('crownlands', 'L_LANNISTER', ["For the King!"], 'zloci plaszcze i lordowie korony'),
    ('nightswatch', 'L_WATCH', ["I am the sword in the darkness!", "For the Watch!"], 'kanon (przysiega)'),
    ('freefolk', 'L_WILD', ["Free folk!", "We kneel to no one!"], 'bez kanonicznego motta'),
    ('dothraki', 'L_DOTHRAKI', ["Hajas!", "Khalakka dothrae!"], 'dothracki kanon (Hajas = badz silny), wymowa: HA-jas, ha-LAK-ka DOTH-ra-e - SPRAWDZIC u Jeffa'),
    ('ghiscari', 'L_UNSULLIED', ["Unsullied! Spears!"], 'Nieskalani nie maja okrzyku - komenda'),
    ('goldencompany', 'L_TULLY', ["Beneath the gold, the bitter steel!"], 'kanon'),
    ('essos', 'L_ESSOS', ["Gold and glory!"], 'kompanie najemne (Second Sons, Stormcrows) - bez kanonu'),
]
for kul, v, lines, uw in MOT:
    for i, t in enumerate(lines, 1):
        add('arm_bs_mo_%s_%02d' % (kul, i), 'motto', kul, '[shouting] ' + t, v,
            uw + '; okrzyk wodza przy szarzy i zebraniu, 1-3 s; -14 LUFS; 3D 35-450 m; nie razem z ROTBA EnableHouseWords tego rodu w tej samej sekundzie',
            prio=1 if (i == 1 or kul in ('stark', 'targaryen', 'greyjoy')) else 2, zakres='region' if kul in REGIONY else 'rod')

# ---------------------------------------------------------------- ROZKAZY (kapitan formacji; komplet na glos kapitana regionu)
ORD = [('march', "Forward! March!"), ('hold', "Hold the line!"), ('shields', "Shields! Up!"), ('loose', "Nock! Draw! Loose!"),
       ('charge', "Charge!"), ('rally', "To the banner! Rally!"), ('fallback', "Fall back! Keep your ranks!")]
CAPT = [('north', 'C_NORTH'), ('west', 'C_WEST'), ('river', 'C_RIVER'), ('storm', 'C_STORM'), ('iron', 'C_IRON'), ('dorne', 'L_MARTELL'),
        ('freefolk', 'C_WILD'), ('essos', 'L_ESSOS')]
SPECIAL = {
    'iron': {'march': "Move, you sea-dogs! Forward!", 'charge': "Reave! Reave! Charge!"},
    'freefolk': {'march': "Come on then! Forward!", 'hold': "Stand! Stand, you bastards!", 'charge': "At them! Kill them all!"},
    'essos': {'charge': "Charge! No quarter!"},
}
for kul, v in CAPT:
    for o, t in ORD:
        t = SPECIAL.get(kul, {}).get(o, t)
        add('arm_bs_or_%s_%s_01' % (kul, o), 'rozkaz', kul, '[shouting] ' + t, v,
            'rozkaz kapitana; 0.8-2 s; -14 LUFS; 3D przy kapitanie 30-250 m; rozkaz ' + o, prio=2 if o == 'rally' else 1, zakres='region')
EXTRA = [('archback', "Archers! Behind the line!"), ('flank', "Horse! Round their flank!")]
for kul, v in [('north', 'C_NORTH'), ('west', 'C_WEST'), ('storm', 'C_STORM'), ('river', 'C_RIVER')]:
    for o, t in EXTRA:
        add('arm_bs_or_%s_%s_01' % (kul, o), 'rozkaz', kul, '[shouting] ' + t, v,
            'rozkaz taktyczny (lucznicy za piechote / flanka konnicy - czesc 1); -14 LUFS', zakres='region')
add('arm_bs_or_dothraki_charge_01', 'rozkaz', 'dothraki', "[shouting] Hajas! Dothras chek!", 'L_DOTHRAKI', 'dothracki kanon - SPRAWDZIC wymowe; szarza khalasaru', zakres='region')
add('arm_bs_or_ghiscari_hold_01', 'rozkaz', 'ghiscari', "Unsullied! Hold!", 'L_UNSULLIED', 'glos plaski, bez emocji (bez tagu)', zakres='region')
add('arm_bs_or_ghiscari_march_01', 'rozkaz', 'ghiscari', "Unsullied! Forward!", 'L_UNSULLIED', 'glos plaski, bez emocji (bez tagu)', zakres='region')

# ---------------------------------------------------------------- WYZWISKA (wedlug rodu / kultury WROGA; mowi szeregowy z przodu) - 2 linie x 2 glosy
TAUNT = [
    ('vs_lannister', ('C_NORTH', 'C_RIVER'), ["Come on, lions! Is that gold or rust on your swords?", "Lannister lapdogs! Go home to your gold!"]),
    ('vs_stark', ('C_WEST', 'S_RICKY'), ["Wolf cubs! Go back to your snow!", "The North? I see nothing but frozen sheep!"]),
    ('vs_tully', ('C_WEST', 'C_IRON'), ["Gut the trout, lads!", "Fish out of water, the lot of you!"]),
    ('vs_arryn', ('C_STORM', 'S_TERRY'), ["Come down off your mountain, falcons!", "Mountain goats in shiny armour!"]),
    ('vs_tyrell', ('C_STORM', 'C_IRON'), ["Pluck the roses!", "Flowers! They brought flowers to a battle!"]),
    ('vs_baratheon', ('C_WEST', 'C_RIVER'), ["Stags for the spit!", "Antlers won't save you!"]),
    ('vs_martell', ('C_STORM', 'S_RICKY'), ["Stamp on the snakes!", "Sand and snakes! Go home!"]),
    ('vs_greyjoy', ('C_NORTH', 'C_RIVER'), ["Back to your rocks, reavers!", "Krakens out of water! Easy meat!"]),
    ('vs_targaryen', ('C_WEST', 'C_NORTH'), ["Dragonspawn! Your time is over!", "No dragon will save you today!"]),
    ('vs_frey', ('C_NORTH', 'S_TERRY'), ["Turncloaks! Oathbreakers!", "Murderers! Remember the wedding!"]),
    ('vs_bolton', ('S_TERRY', 'C_NORTH'), ["Flayers! Butchers!", "Skin them like they skin us!"]),
    ('vs_nightswatch', ('C_WILD', 'C_IRON'), ["Crows! Fly back to your Wall!", "Black crows, dead crows!"]),
    ('vs_freefolk', ('C_NORTH', 'S_TERRY'), ["Wildling savages!", "Back beyond the Wall, you animals!"]),
    ('vs_dothraki', ('S_RICKY', 'C_WEST'), ["Horse lords? Horse thieves!", "Pull them from their saddles!"]),
    ('vs_essos', ('C_STORM', 'C_NORTH'), ["Sellswords! Run back to your masters!", "Paid men die cheap!"]),
    ('vs_generic', ('S_TERRY', 'S_RICKY'), ["Come on then! Come and die!", "Is that all you have?"]),
]
for tgt, (va, vb), lines in TAUNT:
    for i, t in enumerate(lines, 1):
        v1, v2 = (va, vb) if i == 1 else (vb, va)
        uw = 'wyzwisko do wroga (rod / kultura wroga); 1-3 s; -15 LUFS; mowi zolnierz przodu; najwyzej 1 na strone na 30 s'
        add('arm_bs_tn_%s_%02d' % (tgt, i), 'wyzwisko', tgt, '[shouting, mocking] ' + t, v1, uw, prio=1 if i == 1 else 2, zakres='wrog')
        add('arm_bs_tn_%s_%02db' % (tgt, i), 'wyzwisko', tgt, '[shouting, mocking] ' + t, v2, uw + '; drugi glos', prio=2, zakres='wrog')

# ---------------------------------------------------------------- ZLAMANIE WROGA / UPADEK SZTANDARU / ZWYCIESTWO (kapitan regionu)
for kul, v in CAPT:
    add('arm_bs_br_%s_01' % kul, 'zlamanie', kul, "[triumphant shouting] They break! They break! At them!", v,
        'wroga choragiew sie lamie (>=50% ucieka albo pada sztandar/dowodca); -14 LUFS', zakres='region')
    add('arm_bs_bd_%s_01' % kul, 'zlamanie', kul, "[shouting] Their banner is down! Push! Push!", v, 'padl wrogi sztandar; -14 LUFS', zakres='region')
    add('arm_bs_ob_%s_01' % kul, 'sztandar', kul, "[urgent shouting] The banner! Save the banner! Rally to me!", v,
        'padl NASZ sztandar albo dowodca - zebranie; -14 LUFS', zakres='region')
    add('arm_bs_vi_%s_01' % kul, 'zwyciestwo', kul, "[triumphant shouting] Victory! The field is ours!", v,
        'koniec bitwy, wygrana; -15 LUFS; 3D blisko gracza 10-150 m', prio=2, zakres='region')
add('arm_bs_vi_dothraki_02', 'zwyciestwo', 'dothraki', "[triumphant] Me nem nesa!", 'L_DOTHRAKI', 'dothracki kanon (it is known) - SPRAWDZIC', prio=2, zakres='region')

# ---------------------------------------------------------------- JEKI RANNYCH PO BITWIE (Jeff 01:05: "rozne glosy a nie ciagle ten sam"); 8 glosow
JEK = {
    'S_TERRY': ["[groaning] Ahh... ahhh...", "[pained] Mother... Mother, where are you...", "[gasping] I can't... I can't breathe...",
                "[crying] Mum... I'm sorry, Mum...", "[weak] Is anyone there? Anyone?"],
    'S_RICKY': ["[groaning] Ngh... my arm... my arm...", "[weak, whispering] Water... please... water...", "[pained] Finish it... just finish it...",
                "[moaning] Ohh... ohhh...", "[sobbing] I want to go home... I just want to go home..."],
    'C_NORTH': ["[groaning] Gods... old gods, it hurts...", "[weak] Water...", "[pained] Mother...", "[whispers] Don't leave me here... please...",
                "[coughing] Is it over? Did we win?"],
    'C_WEST': ["[pained, shouting] Maester! Someone fetch a maester!", "[groaning] Get it out... get it out of me...", "[weak] Mother... Mother...",
               "[moaning] Ahh... no more... no more..."],
    'C_RIVER': ["[crying] I can't feel my legs...", "[weak, whispering] Water... someone... water...", "[pained] Stay with me, brother... stay with me...",
                "[groaning] Ohh... Seven save me...", "[sobbing] Mother... I want my mother..."],
    'C_STORM': ["[gasping] Help me... someone... help me...", "[groaning] Ahh... ahh...", "[weak] Tell my wife... tell her...", "[pained] Water... please..."],
    'C_IRON': ["[groaning] Drowned God... take me... take me home...", "[coughing] Ahh... the salt... I can taste the salt...", "[pained] Water... fresh water...",
               "[moaning] Ugh... ahh..."],
    'C_WILD': ["[groaning] Ahh... it's cold... so cold...", "[pained] Mother... is that you...", "[weak] Leave me... leave me for the crows...",
               "[gasping] Ngh... help... help me up..."],
}
n = 0
for v, lines in JEK.items():
    for t in lines:
        n += 1
        add('arm_bs_jk_%02d' % n, 'jek', '-', t, v, 'jek rannego PO bitwie, przy cialach rannych (agent ranny/nieprzytomny) - rozne glosy; 1-5 s; -20 LUFS; 3D 5-60 m',
            prio=1, zakres='-')
assert 30 <= n <= 40 and len(JEK) >= 6

# ---------------------------------------------------------------- SFX: BEBNY (marsz wedlug grup kultur; petla 8 s, cichnie przy starciu)
DR = [
    ('westeros', 'Medieval marching drum, a single snare and tabor playing a steady military march at 100 beats per minute, outdoors on an open field, dry, no music, no voices'),
    ('north', 'Deep slow war drum, large skin drum struck at 80 beats per minute, heavy and ominous, outdoors in cold wind, no music, no voices'),
    ('iron', 'Heavy wooden oar drum beating a steady rowing rhythm at 90 beats per minute, hollow and booming, no music, no voices'),
    ('freefolk', 'Wild tribal skin drums, several hand drums playing a loose fast rhythm, outdoors, no melody, no voices'),
    ('unsullied', 'Precise military drum cadence, single deep drum and sharp rim clicks at 110 beats per minute, disciplined and mechanical, no music, no voices'),
    ('dorne', 'Desert frame drum and small hand drum playing a quick marching rhythm at 105 beats per minute, dry and warm, no melody, no voices'),
    ('essos', 'Mercenary war drums, two kettledrums playing a steady march at 95 beats per minute, outdoors, no melody, no voices'),
]
for kul, p in DR:
    for i in (1, 2):
        add('arm_bs_dr_%s_%02d' % (kul, i), 'beben', kul, p, 'SFX', 'beben marszu; petla 8 s (loop); -20 LUFS; 3D przy formacji 20-300 m',
            prio=1 if i == 1 else 2, sek=8, loop=1, zakres='region')

# ---------------------------------------------------------------- SFX: ROGI I TRABKI (wedlug kultur; sygnaly marsz, szarza, odwrot, zebranie)
HORN = [
    ('knight', 'Medieval brass herald trumpet, ', ['two short bright calls then one long note, signal to advance', 'fast rising fanfare, urgent call to charge',
                                                   'three long falling notes, signal to retreat', 'one long steady note repeated twice, rally call']),
    ('north', 'Deep cow horn war horn, ', ['one long low blast, signal to advance', 'two long rising blasts, call to charge', 'three short low blasts, signal to fall back',
                                          'one very long mournful blast, rally call']),
    ('iron', 'Huge conch-like sea horn, ', ['one booming blast, signal to advance', 'harsh rising blast, call to attack', 'two falling blasts, signal to retreat']),
    ('freefolk', 'Rough wild horn made of animal horn, ', ['wild uneven blast, signal to advance', 'many overlapping horn blasts, call to charge', 'two short ragged blasts, retreat']),
    ('essos', 'Bronze long trumpet of an eastern army, ', ['two piercing calls, advance', 'fast urgent blaring call, charge', 'three descending calls, retreat']),
    ('dorne', 'Thin bright desert horn, ', ['short bright call, advance', 'trilling urgent call, charge', 'falling call, retreat']),
    ('dothraki', 'Curved ram horn of a nomad horse horde, ', ['one wild rising blast, the horde rides out', 'several wild overlapping blasts with distant hoofbeats, call to charge',
                                                          'two short harsh blasts, the riders wheel away']),
]
SIG = ['march', 'charge', 'retreat', 'rally']
for kul, base, sigs in HORN:
    for j, s in enumerate(sigs):
        add('arm_bs_hr_%s_%s' % (kul, SIG[j]), 'rog', kul, base + s + ', outdoors over a battlefield, distant echo, no music, no voices', 'SFX',
            'rog/trabka; 3-5 s; -16 LUFS; slychac na calym polu 50-700 m', sek=4, zakres='region')
add('arm_bs_hr_nightswatch_two', 'rog', 'nightswatch', "Deep war horn of the Night's Watch, two long low blasts from the Wall, cold wind, no music, no voices", 'SFX',
    "kanon: dwa dzwieki = dzicy (bitwa z freefolk/wildlings)", sek=6, zakres='region')
add('arm_bs_hr_nightswatch_three', 'rog', 'nightswatch', "Deep war horn of the Night's Watch, three long low blasts, ominous, cold wind, no music, no voices", 'SFX',
    'kanon: trzy dzwieki = Inni (bitwa z whitewalker)', sek=8, zakres='region')
add('arm_bs_hr_nightswatch_one', 'rog', 'nightswatch', "Deep war horn of the Night's Watch, one long low blast, cold wind, no music, no voices", 'SFX',
    'kanon: jeden dzwiek = wracaja zwiadowcy (marsz)', prio=2, sek=4, zakres='region')

# ---------------------------------------------------------------- SFX: TLUM (ryk szarzy, Dothrakowie, wiwat, stukanie w tarcze)
CR = [
    ('roar_westeros', 'Large medieval army of hundreds of men roaring a battle cry as they charge, outdoors, no words', 1),
    ('roar_westeros', 'Hundreds of soldiers shouting and cheering before a charge, distant, outdoors, no words', 2),
    ('whoop_dothraki', 'Horde of horsemen whooping and ululating while galloping, outdoors, no words', 1),
    ('cheer_victory', 'Victorious medieval army cheering after battle, swords raised, outdoors, no words', 1),
    ('shields_bang', 'Many soldiers rhythmically banging swords on wooden shields as a taunt, slow steady rhythm, outdoors', 1),
    ('unsullied_stamp', 'Disciplined spearmen stamping and striking spear butts on the ground in perfect unison, three times, outdoors', 1),
]
cnt = {}
for kul, p, pr in CR:
    cnt[kul] = cnt.get(kul, 0) + 1
    add('arm_bs_cr_%s_%02d' % (kul, cnt[kul]), 'tlum', kul, p, 'SFX', 'tlum; 3-6 s; -18 LUFS; 3D przy formacji 50-400 m', prio=pr, sek=5, zakres='-')

# ---------------------------------------------------------------- SFX: KRUKI I PTAKI PO BITWIE (Jeff 01:06)
PT = [
    ('crows', 'A flock of crows cawing over a silent battlefield after the fighting, several birds, outdoors, light wind, no voices', 6, 0),
    ('raven', 'A single large raven croaking slowly, close by, on a quiet field, no voices', 3, 0),
    ('wings', 'Ravens taking off together, heavy wing flapping and a few croaks, outdoors, no voices', 4, 0),
    ('squabble', 'Carrion crows squabbling and cawing over food on the ground, outdoors, no voices', 5, 0),
    ('distant', 'Distant crows calling over open fields with soft wind, sparse and mournful, no voices', 8, 1),
    ('forest', 'Calm forest birdsong after a storm, small birds and a distant cuckoo, peaceful, outdoors, no voices', 10, 1),
    ('gulls', 'Seagulls crying over a stony shore with gentle waves, outdoors, no voices', 8, 1),
]
for k, p, s, lp in PT:
    add('arm_bs_pt_%s_01' % k, 'ptaki', '-', p, 'SFX', 'po bitwie: kruki przy cialach / ptaki lasu / mewy przy brzegu; -22 LUFS', sek=s, loop=lp, zakres='-')

# ---------------------------------------------------------------- SFX: WIATR I DESZCZ NA POLU (Jeff 01:06) - petle tla
WI = [
    ('steppe', 'Dry wind blowing steadily across an open grass steppe, grasses rustling, wide and empty, no voices, no music'),
    ('mountain', 'Strong gusting wind on a high mountain pass, whistling over rocks, no voices, no music'),
    ('winter', 'Icy winter wind with blowing snow hissing over frozen ground, bleak and cold, no voices, no music'),
    ('gentle', 'Gentle breeze over a summer field, soft rustling leaves and grass, calm, no voices, no music'),
]
for k, p in WI:
    add('arm_bs_wi_%s_01' % k, 'wiatr', k, p, 'SFX', 'tlo pola wedlug terenu/pory roku; petla 12 s; -26 LUFS; 2D przy sluchaczu', sek=12, loop=1, zakres='-')
RN = [
    ('light', 'Light steady rain falling on a field, pattering on leather, steel armour and wet grass, no thunder, no voices', 1),
    ('heavy', 'Heavy downpour on an open field with distant rolling thunder, water splashing in mud, no voices', 1),
    ('tents', 'Rain drumming on canvas tents in a military camp, dripping, no voices', 2),
]
for k, p, pr in RN:
    add('arm_bs_rn_%s_01' % k, 'deszcz', k, p, 'SFX', 'tlo przy deszczu na scenie; petla 12 s; -24 LUFS; 2D przy sluchaczu', prio=pr, sek=12, loop=1, zakres='-')

# ---------------------------------------------------------------- SFX: OGIEN PRZY OBLEZENIU (dopisek Jeffa)
FI = [
    ('tents', 'Canvas tents and wooden carts burning in a military camp, crackling flames and snapping timber, no voices', 10, 1),
    ('palisade', 'Large wooden palisade wall on fire, roaring flames, timber cracking and groaning, embers popping, no voices', 10, 1),
    ('engine', 'Wooden siege engine burning, flames roaring, heavy beams cracking and collapsing, no voices', 8, 0),
    ('camp', 'Distant burning camp at night, low roar of many fires, occasional collapse of timbers, no voices', 12, 1),
]
for k, p, s, lp in FI:
    add('arm_bs_fi_%s_01' % k, 'ogien', k, p, 'SFX', 'oblezenie: plonace namioty/palisada/machina; -22 LUFS; 3D 10-200 m', sek=s, loop=lp, zakres='-')

# ---------------------------------------------------------------- SFX: KONIE I SZCZEK BRONI (Jeff 01:06)
KO = [
    ('neigh', 'A warhorse neighing loudly, outdoors, no voices', 3, 0, 1),
    ('snort', 'Restless horses snorting and stamping, bridles and tack jingling, outdoors, no voices', 4, 0, 1),
    ('charge', 'Heavy cavalry charge, many horses galloping hard, thundering hooves and jingling armour, outdoors, no voices', 8, 0, 1),
    ('wounded', 'A wounded horse squealing in pain and thrashing, outdoors, no voices', 3, 0, 1),
    ('column', 'A column of many horses walking on a dirt road, steady hooves, tack and armour jingling, outdoors, no voices', 10, 1, 1),
    ('neigh', 'Several horses neighing and whinnying in alarm, outdoors, no voices', 4, 0, 2),
]
kc = {}
for k, p, s, lp, pr in KO:
    kc[k] = kc.get(k, 0) + 1
    add('arm_bs_ko_%s_%02d' % (k, kc[k]), 'kon', k, p, 'SFX', 'konie: kolumna/szarza/ranny kon; -18 LUFS; 3D 10-300 m', prio=pr, sek=s, loop=lp, zakres='-')
SZ = [
    ('melee', 'Large medieval battle melee, hundreds of swords clashing on shields and armour, distant shouting crowd without words, chaotic, outdoors', 10, 1),
    ('close', 'A few swords clashing and scraping close by, steel on steel, grunts without words, outdoors', 4, 0),
    ('press', 'Shield wall pressing together, wooden shields grinding and cracking, spears knocking, heavy breathing without words, outdoors', 6, 0),
]
for k, p, s, lp in SZ:
    add('arm_bs_sz_%s_01' % k, 'szczek', k, p, 'SFX', 'szczek broni przy zwarciu tlumu; -18 LUFS; 3D przy starciu formacji 10-250 m', sek=s, loop=lp, zakres='-')

# ---------------------------------------------------------------- zapis i kontrola
out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, 'klipy_bitwa.csv')
ids = [r[0] for r in rows]
assert len(ids) == len(set(ids)), [i for i in ids if ids.count(i) > 1]
with open(out, 'w', encoding='utf-8', newline='\n') as f:
    f.write('id;kategoria;kultura/rod;tekst;glos;uwagi;prio;model;sek;lufs;loop;zakres\n')
    for r in rows:
        f.write(';'.join(r) + '\n')


def spoken(t):
    return re.sub(r'\[[^\]]*\]\s*', '', t).strip()


by = {}
for r in rows:
    k = (r[1], r[6])
    by[k] = by.get(k, 0) + 1
kats = sorted(set(r[1] for r in rows), key=lambda k: list(LUFS).index(k))
print('klipow: %d (prio1 %d, prio2 %d)' % (len(rows), sum(1 for r in rows if r[6] == '1'), sum(1 for r in rows if r[6] == '2')))
for k in kats:
    print('  %-10s prio1 %3d  prio2 %3d' % (k, by.get((k, '1'), 0), by.get((k, '2'), 0)))
for p in ('1', '2'):
    tts = [r for r in rows if r[4] != 'SFX' and r[6] == p]
    sfx = [r for r in rows if r[4] == 'SFX' and r[6] == p]
    print('prio%s: TTS %d klipow, %d znakow (z tagami v3; mowionych %d) | SFX %d generacji, %d s' % (
        p, len(tts), sum(len(r[3]) for r in tts), sum(len(spoken(r[3])) for r in tts), len(sfx), sum(float(r[8]) for r in sfx)))
print('glosow (rol):', len(set(r[4] for r in rows if r[4] != 'SFX')))
print('zapis:', out)
