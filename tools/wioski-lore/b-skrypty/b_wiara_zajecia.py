# Badanie B: wiara (pkt 5) i zajecia (pkt 4) wiosek na mapie. Pisze wiara.csv i zajecia.csv (separator ';').
# Teksty po angielsku (gra), komentarze bez polskich znakow. Zrodla: AWOIAF (strony w kolumnie source),
# [STYL] = szczegol dopisany w stylu krainy (lore nie opisuje wioski wprost), [WYM] = wymyslone w duchu lore.
import os, re

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.dirname(HERE)

# ---------------------------------------------------------------------------------------------
# WIARA: mapa kraina / siedziba -> wiara (z waga) + linie tekstu wedlug wiary i klasy miejsca
# ---------------------------------------------------------------------------------------------
# table=map: key = region:<klucz> albo seat:<nazwa siedziby> albo river:<nazwa rzeki z rodzajnikiem>@<region>;
#            faith = kod wiary; weight = udzial (suma w kluczu = 100). Siedziba bije region, rzeka bije siedzibe.
FAITH_MAP = [
    # Westeros
    ("region:north", "old_gods", 100, "Old_gods; Heart_tree"),
    ("region:gift", "old_gods", 70, "Old_gods"), ("region:gift", "gift_seven", 30, "Faith_of_the_Seven; Castle_Black"),
    ("region:beyond", "old_gods_free", 100, "Old_gods; Free_folk"),
    ("region:skagos", "old_gods_skagos", 100, "Skagos"),
    ("region:riverlands", "seven", 88, "Faith_of_the_Seven"), ("region:riverlands", "seven_weirwood", 12, "Raventree_Hall; High_Heart"),
    ("region:vale", "seven", 90, "Faith_of_the_Seven"), ("region:vale", "seven_weirwood", 10, "Vale_mountain_clans [STYL]"),
    ("region:westerlands", "seven", 100, "Faith_of_the_Seven"),
    ("region:reach", "seven", 100, "Faith_of_the_Seven; House_Gardener"),
    ("region:stormlands", "seven", 95, "Faith_of_the_Seven"), ("region:stormlands", "seven_weirwood", 5, "[STYL]"),
    ("region:crownlands", "seven", 100, "Faith_of_the_Seven"),
    ("region:dragonstone", "seven", 85, "Faith_of_the_Seven"), ("region:dragonstone", "seven_red", 15, "R'hllor; Dragonstone (298-299 AC)"),
    ("region:iron", "drowned_god", 100, "Drowned_God"),
    ("region:dorne", "seven_dorne", 100, "Faith_of_the_Seven; Dorne"),
    # wyjatki per siedziba (Westeros)
    ("seat:White Harbor", "seven_north", 75, "White_Harbor; Sept_of_the_Snows; House_Manderly"), ("seat:White Harbor", "old_gods", 25, "White_Harbor"),
    ("seat:Ramsgate", "seven_north", 60, "House_Manderly"), ("seat:Ramsgate", "old_gods", 40, "House_Manderly"),
    ("seat:Raventree Hall", "old_gods_river", 60, "Raventree_Hall; House_Blackwood"), ("seat:Raventree Hall", "seven", 40, "Raventree_Hall"),
    ("seat:Acorn Hall", "seven_weirwood", 40, "High_Heart"), ("seat:Acorn Hall", "seven", 60, "Faith_of_the_Seven"),
    ("seat:Harrenhal", "seven_weirwood", 25, "Harrenhal (weirwood beams)"), ("seat:Harrenhal", "seven", 75, "Faith_of_the_Seven"),
    ("seat:Dyre Den", "seven_weirwood", 30, "Crackclaw_Point (First Men blood) [STYL]"), ("seat:Dyre Den", "seven", 70, "Crackclaw_Point"),
    # Dorne: wioski nad Greenblood - pamiec Matki Rhoyne
    ("river:the Greenblood@dorne", "mother_rhoyne", 60, "Mother_Rhoyne; Orphans_of_the_Greenblood"), ("river:the Greenblood@dorne", "seven_dorne", 40, "Dorne"),
    # Essos
    ("region:braavos", "braavos_many", 100, "Braavos; Isle_of_the_Gods; Temple_of_the_Moonsingers"),
    ("region:pentos", "pentos_many", 100, "Pentos; Prince_of_Pentos; Red_temple"),
    ("region:norvos", "norvos_bearded", 100, "Norvos; Bearded_priests"),
    ("region:qohor", "qohor_goat", 100, "Black_Goat; Qohor"),
    ("region:lorath", "lorath_mixed", 100, "Lorath; Boash; Mazemakers"),
    ("region:myr", "myr_mixed", 100, "Myr; Red_temple"),
    ("region:lys", "lys_mixed", 100, "Lys; Weeping_Lady_of_Lys"),
    ("region:tyrosh", "tyrosh_mixed", 100, "Tyrosh; Trios; Red_temple"),
    ("region:volantis", "volantis_mixed", 100, "Temple_of_the_Lord_of_Light; Volantis"),
    ("region:valyria", "valyria_ruins", 100, "Valyria; Fourteen_Flames"),
    ("region:ghis", "ghis_gods", 100, "Ghiscari; Temple_of_the_Graces"),
    ("seat:Lhazosh", "lhazar_shepherd", 100, "Great_Shepherd; Lhazareen"), ("seat:Hesh", "lhazar_shepherd", 100, "Great_Shepherd; Lhazareen"),
    ("region:qarth", "qarth_many", 100, "Qartheen; Temple_of_Memory"),
    ("region:dothraki", "dothraki_horse", 100, "Great_Stallion; Womb_of_the_World; Godsway"),
    ("region:sarnor", "sarnor_hundred", 100, "Sarnori; Saath"),
    ("region:ibben", "ibben_gods", 100, "Ibben (Thousand with priests) [STYL]"),
    ("region:yiti", "yiti_gods", 100, "Lion_of_Night; Maiden-Made-of-Light; God-on-Earth"),
    ("region:summer", "summer_love", 100, "Summer_Isles"),
]

# table=line: faith, cls (H wezel / R przy linii / F ubocze / * kazda), region ('' = kazdy), text, source
# {DISTRICT} = wies gry (glowna wioska okregu), {SEAT} = zamek / miasto pana okregu.
FAITH_LINES = [
    # --- starzy bogowie (Polnoc) ---
    ("old_gods", "H", "", "Faith: the old gods. A weirwood with a carved face stands on the village green.", "Old_gods (godswoods in villages)"),
    ("old_gods", "H", "", "Faith: the old gods. Weddings are made before the heart tree in the village grove.", "Heart_tree"),
    ("old_gods", "R", "", "Faith: the old gods. They pray before a heart tree in the wood above the fields.", "Heart_tree"),
    ("old_gods", "R", "", "Faith: the old gods. The heart tree here weeps red sap, and no one cuts wood near it.", "Weirwood"),
    ("old_gods", "R", "", "Faith: the old gods. No priests and no temple, only an old weirwood by the spring.", "Old_gods (no priests)"),
    ("old_gods", "F", "", "Faith: the old gods. The nearest heart tree is a morning's walk away, in the lord's godswood.", "Godswood"),
    ("old_gods", "F", "", "Faith: the old gods. A carved weirwood stump marks where the village prays.", "Old_gods [STYL]"),
    # --- Polnoc, Manderly: Siedmiu ---
    ("seven_north", "H", "", "Faith: the Seven, as in White Harbor. A sept of white stone, small but proud.", "White_Harbor; Sept_of_the_Snows [STYL]"),
    ("seven_north", "R", "", "Faith: the Seven, brought north by the Manderlys. A few old families still keep a heart tree.", "House_Manderly; White_Harbor"),
    ("seven_north", "F", "", "Faith: the Seven. A septon from {DISTRICT} comes twice a year, when the roads allow.", "Faith_of_the_Seven (wandering septons)"),
    # --- Dorzecze, Blackwood: starzy bogowie ---
    ("old_gods_river", "*", "", "Faith: the old gods, like the Blackwoods of Raventree. A weirwood stands at the village edge.", "Raventree_Hall; House_Blackwood"),
    ("old_gods_river", "*", "", "Faith: the old gods. Ravens roost in the village weirwood, as they do at Raventree.", "Godswood_of_Raventree_Hall [STYL]"),
    # --- Dar / Nocna Straz ---
    ("gift_seven", "*", "", "Faith: the Seven, for the southron settlers. The black brothers pray at the sept of Castle Black.", "Castle_Black [STYL]"),
    ("gift_seven", "*", "", "Faith: the Seven and the old gods side by side; a weirwood and a wooden sept share the green.", "New_Gift [STYL]"),
    # --- Za Murem, Skagos ---
    ("old_gods_free", "*", "", "Faith: the old gods. The free folk kneel to no king, only to the weirwoods of the haunted forest.", "Free_folk; Old_gods"),
    ("old_gods_free", "*", "", "Faith: the old gods. They burn their dead, so that nothing walks again.", "Free_folk"),
    ("old_gods_skagos", "*", "", "Faith: the old gods. Mainlanders whisper that the stoneborn still give blood to their weirwoods.", "Skagosi"),
    ("old_gods_skagos", "*", "", "Faith: the old gods, kept the oldest way. A weirwood grove watches the bay.", "Skagos [STYL]"),
    # --- Siedmiu (poludnie Westeros) ---
    ("seven", "H", "", "Faith: the Seven. A seven-sided sept of stone, with its own septon and a bell.", "Faith_of_the_Seven; Sept"),
    ("seven", "H", "", "Faith: the Seven. A stone sept with seven windows, the pride of the village.", "Sept [STYL]"),
    ("seven", "H", "riverlands|reach|vale", "Faith: the Seven. A septry of brown brothers nearby keeps bees, a mill and a brewhouse.", "Septry (ASOS 39)"),
    ("seven", "R", "", "Faith: the Seven. A small wooden sept; a septon from {DISTRICT} comes on holy days.", "Faith_of_the_Seven"),
    ("seven", "R", "", "Faith: the Seven. A roadside shrine bears the seven-pointed star; the sept is in {DISTRICT}.", "Seven-pointed star [STYL]"),
    ("seven", "R", "", "Faith: the Seven. Begging brothers pass through and are fed at the sept door.", "Holy_brother"),
    ("seven", "F", "", "Faith: the Seven. No septon of its own: a wandering septon comes twice a year to marry and bless.", "Faith_of_the_Seven (twice a year)"),
    ("seven", "F", "", "Faith: the Seven. The faithful walk to the sept in {DISTRICT} on holy days.", "Faith_of_the_Seven"),
    ("seven_weirwood", "*", "", "Faith: the Seven, though an old weirwood still stands nearby and some leave gifts there.", "Raventree_Hall; High_Heart"),
    ("seven_weirwood", "*", "", "Faith: the Seven. A few old families still pray at a weirwood in the woods, as the First Men did.", "Old_gods (south of the Neck)"),
    ("seven_red", "*", "", "Faith: the Seven. Lately men from Dragonstone speak of a red god, and the septon frowns.", "R'hllor; Melisandre at Dragonstone"),
    # --- Zelazne Wyspy ---
    ("drowned_god", "H", "", "Faith: the Drowned God. No temple: the drowned men baptise in the surf below the village.", "Drowned_God (no temples)"),
    ("drowned_god", "R", "", "Faith: the Drowned God. When a drowned priest comes with his driftwood cudgel, he is fed and housed.", "Priest_of_the_Drowned_God"),
    ("drowned_god", "F", "", "Faith: the Drowned God. They give their dead to the sea and keep no idols.", "Drowned_God"),
    # --- Dorne ---
    ("seven_dorne", "H", "", "Faith: the Seven. A domed sept of sandstone stands beside the village well.", "Dorne [STYL]"),
    ("seven_dorne", "R", "", "Faith: the Seven. A small sept of sun-dried brick; a septon comes from {DISTRICT}.", "Faith_of_the_Seven"),
    ("seven_dorne", "F", "", "Faith: the Seven. A wandering septon comes with the caravans, when they come.", "Faith_of_the_Seven [STYL]"),
    ("mother_rhoyne", "*", "", "Faith: the Seven, but the river folk still leave offerings to Mother Rhoyne.", "Mother_Rhoyne; Orphans_of_the_Greenblood"),
    ("mother_rhoyne", "*", "", "Faith: the Seven. Orphans of the Greenblood moor here and sing to Mother Rhoyne at dusk.", "Orphans_of_the_Greenblood"),
    # --- Braavos ---
    ("braavos_many", "H", "", "Faith: many gods, as in Braavos. A shrine of the Moonsingers, who led the founders to the lagoon.", "Temple_of_the_Moonsingers"),
    ("braavos_many", "R", "", "Faith: many gods. Some pray at the Moonsingers' shrine, others at the red priests' night fire.", "Braavos; Red_temple"),
    ("braavos_many", "F", "", "Faith: many gods. In Braavos every god has a temple on the Isle of the Gods; here there is one shrine.", "Isle_of_the_Gods"),
    # --- Pentos ---
    ("pentos_many", "*", "", "Faith: the many gods of Pentos. A field shrine; when harvests fail, they say the Prince will pay for it.", "Prince_of_Pentos"),
    ("pentos_many", "*", "", "Faith: many gods. A red priest lights a night fire in {DISTRICT} for those who will listen.", "Pentos; Red_temple"),
    # --- Norvos ---
    ("norvos_bearded", "H", "", "Faith: the god of the bearded priests, whose name only initiates know. A bell calls the hours of prayer.", "Bearded_priests; Norvos (three bells) [STYL]"),
    ("norvos_bearded", "R", "", "Faith: the nameless god of Norvos. A bearded priest in a hair shirt comes on holy days.", "Bearded_priests"),
    ("norvos_bearded", "F", "", "Faith: the god of the bearded priests. The village rises and rests by the bells of Norvos, as all must.", "Norvos (three bells)"),
    # --- Qohor ---
    ("qohor_goat", "H", "", "Faith: the Black Goat of Qohor. A calf or a kid is bled on the stone altar each holy day.", "Black_Goat"),
    ("qohor_goat", "R", "", "Faith: the Black Goat. The village sends a bullock to the temple when the priests ask.", "Black_Goat"),
    ("qohor_goat", "F", "", "Faith: the Black Goat. A blackened altar stone stands at the edge of the fields.", "Black_Goat [STYL]"),
    # --- Lorath ---
    ("lorath_mixed", "*", "", "Faith: many gods, brought by freedmen from every land. The old maze-stones belong to none of them.", "Lorath; Mazemakers"),
    ("lorath_mixed", "*", "", "Faith: many gods. The cult of the Blind God is long dead, but its stone maze still stands.", "Boash; Blind_God"),
    # --- Myr ---
    ("myr_mixed", "*", "", "Faith: no single faith, as in Myr. Shrines to many gods line the road; a red priest comes on market days.", "Myr; Red_temple"),
    ("myr_mixed", "*", "", "Faith: many gods. The red temple of Myr takes a boy from the village now and then.", "Thoros_of_Myr"),
    # --- Lys ---
    ("lys_mixed", "*", "", "Faith: many gods. The old women keep a shrine to the Weeping Lady of Lys.", "Weeping_Lady_of_Lys"),
    ("lys_mixed", "*", "", "Faith: many gods. The love goddess of the Lyseni coins is honoured at every wedding.", "Lys (love goddess on coinage) [STYL]"),
    # --- Tyrosh ---
    ("tyrosh_mixed", "*", "", "Faith: many gods. A shrine of three-headed Trios stands at the edge of the village.", "Trios; Temple_of_Trios"),
    ("tyrosh_mixed", "*", "", "Faith: many gods. Red priests from Tyrosh preach in the market square.", "Red_temple"),
    # --- Volantis ---
    ("volantis_mixed", "*", "", "Faith: many gods. The slaves turn to R'hllor, whose temple in Volantis is the greatest in the world.", "Temple_of_the_Lord_of_Light"),
    ("volantis_mixed", "*", "", "Faith: many gods. A red priest, himself a slave of the temple, tends the night fire.", "Temple_of_the_Lord_of_Light (slave priests)"),
    # --- Valyria ---
    ("valyria_ruins", "*", "", "Faith: many gods. Men here fear the Fourteen Flames more than any of them.", "Fourteen_Flames"),
    # --- Ghis, Lhazar ---
    ("ghis_gods", "*", "", "Faith: the gods of Ghis, whose Graces keep the temples of the cities. The slaves pray in silence.", "Ghiscari; Graces"),
    ("ghis_gods", "*", "", "Faith: the gods of Ghis. A harpy carved over the shrine door watches who comes in.", "Ghiscari (harpy emblem) [STYL]"),
    ("lhazar_shepherd", "H", "", "Faith: the Great Shepherd. A mud-walled temple with a domed roof, sheepskins on the floor.", "Great_Shepherd (temple)"),
    ("lhazar_shepherd", "*", "", "Faith: the Great Shepherd. All men are one flock, the godswife teaches.", "Great_Shepherd; Lhazareen"),
    # --- Qarth ---
    ("qarth_many", "*", "", "Faith: many strange gods. Their temples crowd the waterfront of Qarth; here there is only a shrine.", "Qartheen"),
    # --- Dothrakowie ---
    ("dothraki_horse", "*", "", "Faith: the horse god of the Dothraki, for the riders. The village folk keep their own gods quietly.", "Great_Stallion (book: horse god)"),
    ("dothraki_horse", "*", "", "Faith: the horse god. The riders say the first man came out of the Womb of the World.", "Womb_of_the_World"),
    # --- Sarnor, Ibben, Yi Ti, Wyspy Letnie ---
    ("sarnor_hundred", "*", "", "Faith: the hundred gods of the Tall Men, honoured now only here and in Saath.", "Sarnori"),
    ("ibben_gods", "*", "", "Faith: the gods of Ibben, whose priests sit among the Thousand.", "Ibben (Shadow Council, Thousand) [STYL]"),
    ("yiti_gods", "*", "", "Faith: the Lion of Night and the Maiden-Made-of-Light, parents of the God-on-Earth.", "Lion_of_Night; Maiden-Made-of-Light"),
    ("summer_love", "*", "", "Faith: the god and goddess of love, beauty and fertility. The village keeps a temple of love.", "Summer_Isles"),
]
# trzecie warianty, zeby duze krainy Essos i wyjatki nie mialy jednego zdania na sto wiosek
FAITH_LINES += [
    ("lhazar_shepherd", "*", "", "Faith: the Great Shepherd. The temple is mud-walled and dark, with a blue-veined altar stone.", "Great_Shepherd (temple)"),
    ("lhazar_shepherd", "R", "", "Faith: the Great Shepherd. A godswife tends the village temple and the sick alike.", "Great_Shepherd (godswife)"),
    ("seven_north", "R", "", "Faith: the Seven. A small wooden sept with a white door, paid for by Manderly silver.", "House_Manderly [STYL]"),
    ("pentos_many", "*", "", "Faith: many gods. The magisters' gods have temples in Pentos; the village makes do with a roadside altar.", "Pentos [STYL]"),
    ("myr_mixed", "*", "", "Faith: many gods. Every trader brings his own god to Myr, and some leave one behind in the village.", "Myr (mixed) [WYM]"),
    ("lys_mixed", "*", "", "Faith: many gods. A red priest and a priestess of love share the village square on feast days.", "Lys; Red_temple [STYL]"),
    ("tyrosh_mixed", "*", "", "Faith: many gods. The first head of Trios devours the dying, the village says, and the third gives them back.", "Trios"),
    ("volantis_mixed", "*", "", "Faith: many gods. The free keep the gods of their Valyrian fathers; the slaves keep the fire.", "Volantis [STYL]"),
    ("ghis_gods", "*", "", "Faith: the gods of Ghis. The masters' household gods sit by the door, and the slaves bow as they pass.", "Ghiscari [STYL]"),
    ("lorath_mixed", "*", "", "Faith: many gods. Freedmen from every land each brought a god, and none of them quarrels.", "Lorath (refuge) [WYM]"),
    # klasa R to 84% wiosek - wiecej wariantow dla duzych wiar
    ("seven", "R", "", "Faith: the Seven. A sept of timber and daub, its seven-pointed star painted fresh every spring.", "Sept [STYL]"),
    ("seven", "R", "", "Faith: the Seven. Their septon is old and half deaf, and preaches mostly about the Stranger.", "Faith_of_the_Seven [WYM]"),
    ("seven", "*", "", "Faith: the Seven. Father, Mother, Warrior, Smith, Maiden, Crone and Stranger each have a candle in the sept.", "Faith_of_the_Seven (seven aspects)"),
    ("seven", "R", "", "Faith: the Seven. Women pray to the Mother in the sept, men to the Warrior and the Smith.", "Faith_of_the_Seven [STYL]"),
    ("old_gods", "R", "", "Faith: the old gods. Oaths are sworn before the heart tree, and no one breaks them lightly.", "Heart_tree [STYL]"),
    ("old_gods", "*", "", "Faith: the old gods of the forest, nameless and countless, who watch through the weirwoods' eyes.", "Old_gods"),
    ("norvos_bearded", "*", "", "Faith: the nameless god of Norvos. At the hour of prayer the whole village stops work and kneels.", "Norvos (three bells) [STYL]"),
    ("norvos_bearded", "*", "", "Faith: the god of the bearded priests. Men here go clean-shaven, for only priests may wear a beard.", "Bearded_priests"),
    ("qohor_goat", "*", "", "Faith: the Black Goat. On holy days the god is given condemned men, and the village keeps out of trouble.", "Black_Goat (holy days)"),
    ("qohor_goat", "*", "", "Faith: the Black Goat of Qohor, who must have blood every day.", "Black_Goat (daily sacrifice)"),
    ("braavos_many", "*", "", "Faith: many gods. Some here honour the Father of Waters, whose temple is rebuilt for each new bride.", "Isle_of_the_Gods (Father of Waters)"),
    ("braavos_many", "*", "", "Faith: many gods. A Westerosi sailor's widow keeps a little shrine of the Seven by the road.", "Sept-Beyond-the-Sea [WYM]"),
    ("seven_dorne", "R", "", "Faith: the Seven. The sept is cool and dim inside, and the Mother's statue is veiled against the sun.", "Dorne [WYM]"),
    ("seven_dorne", "*", "", "Faith: the Seven. As everywhere in Dorne, daughters inherit as sons do, and the septon blesses both.", "Dorne (equal inheritance)"),
    ("ghis_gods", "*", "", "Faith: the gods of Ghis. On holy days the masters ride to the temple of the Graces in the city.", "Temple_of_the_Graces [STYL]"),
    ("volantis_mixed", "*", "", "Faith: many gods. At dusk the village gathers around the red priests' night fire to hear of the Lord of Light.", "Red_priest (nightfires)"),
]

# ---------------------------------------------------------------------------------------------
# ZAJECIA: grupa krain, rodzaj obrazka (kind v4), typ wsi gry (village_type), miejsce
# ---------------------------------------------------------------------------------------------
GROUP = {
    "north": "N", "gift": "N", "skagos": "N", "beyond": "N",
    "riverlands": "W", "vale": "W", "westerlands": "W", "crownlands": "W", "stormlands": "W", "dragonstone": "W",
    "reach": "R", "dorne": "D", "iron": "I",
    "braavos": "E1", "lorath": "E1", "ibben": "E1",
    "pentos": "E2", "norvos": "E2", "qohor": "E2",
    "myr": "E3", "lys": "E3", "tyrosh": "E3", "volantis": "E3", "valyria": "E3",
    "ghis": "G", "qarth": "X", "dothraki": "X", "sarnor": "X", "yiti": "X", "summer": "X",
}
CROPS = {"N": "barley, oats and turnips", "W": "wheat, barley and oats", "R": "wheat, barley and apples",
         "D": "wheat and barley on watered land", "I": "barley and oats on thin, stony soil",
         "E1": "barley, oats and cabbages", "E2": "wheat, barley and beans", "E3": "wheat, barley and figs",
         "G": "wheat and barley on irrigated land", "X": "barley and beans"}
# kto pracuje w polu (lore: Volantis 5:1, Lys/Myr/Tyrosh 3:1, Zatoka Niewolnicza; Pentos "wolni sludzy"; thralle)
FARMERS = {"N": "Farmers", "W": "Farmers", "R": "Farmers", "D": "Farmers", "I": "Thralls and farmers",
           "E1": "Free farmers", "E2": "Farmers and bond servants", "E3": "Field slaves", "G": "Field slaves", "X": "Farmers"}

# table=main: kind;vtype;group;text  ('*' = kazdy). Najbardziej szczegolowy wiersz wygrywa (kind > vtype > group).
# {CROPS} {FARMERS} {SEAT} {RIVER_OR} (rzeka z nazwa albo "the river")
MAIN = [
    # rodzaje obrazka v4 (kind) - bez wzgledu na typ wsi
    ("mill", "*", "*", "{FARMERS} and millers: {CROPS}. The mill wheel turns on {RIVER_OR}.", "PROJEKT v4 (mill = water mill) [STYL]"),
    ("mill", "*", "E3", "Field slaves and a miller: {CROPS} for the masters of {SEAT}. The mill turns on {RIVER_OR}.", "Volantis/Lys/Myr/Tyrosh slavery"),
    ("mill", "*", "G", "Field slaves and a miller: {CROPS} for the masters of {SEAT}. The mill turns on {RIVER_OR}.", "Slaver's_Bay"),
    ("windmill", "*", "*", "{FARMERS}: {CROPS}. A windmill on the rise grinds for the whole district.", "PROJEKT v4 (windmill) [STYL]"),
    ("granary", "*", "*", "{FARMERS} and carters: {CROPS}. The great barn here holds the grain of the district.", "PROJEKT v4 (granary) [STYL]"),
    ("granary", "*", "E3", "Field slaves and overseers: {CROPS}. The masters' granary holds the grain of the district.", "Slavery (Essos)"),
    ("granary", "*", "G", "Field slaves and overseers: {CROPS}. The masters' granary holds the grain of the district.", "Slaver's_Bay"),
    ("fishing", "*", "*", "Fishers and net-makers: {FISH}.", "Fisherman"),
    ("fishing", "*", "I", "Fishers, as everywhere on the isles: {FISH}.", "[STYL]"),   # E7/L P9: bez Haerega
    ("farm", "*", "*", "Crofters and herdsmen: {CROPS}, a few cows and pigs.", "PROJEKT v4 (farm) [STYL]"),
    ("farm", "*", "E3", "Slaves on a master's estate: {CROPS}, goats and pigs.", "Slavery (Essos)"),
    ("farm", "*", "G", "Slaves on a master's estate: {CROPS}, goats and sheep.", "Slaver's_Bay"),
    # typy wsi gry (dla kind=village i dla farm, gdy typ to hodowla)
    ("*", "wheat_farm", "*", "{FARMERS}: {CROPS}.", "settlements.xml wheat_farm"),
    ("*", "wheat_farm", "E3", "Field slaves: {CROPS} for the masters of {SEAT}.", "Slavery (Essos)"),
    ("*", "wheat_farm", "G", "Field slaves: {CROPS} for the masters of {SEAT}.", "Slaver's_Bay"),
    ("*", "fisherman", "*", "Fishers and net-makers: {FISH}.", "fisherman"),
    ("*", "lumberjack", "*", "Woodcutters and charcoal burners.", "lumberjack"),
    ("*", "lumberjack", "N", "Woodsmen: pine, oak and sentinel trees felled for the lords' halls.", "North (sentinel trees) [STYL]"),
    ("*", "lumberjack", "E1", "Woodcutters: pine from the hills for the shipwrights of the Arsenal.", "Braavos (Arsenal, pine hills)"),
    ("*", "lumberjack", "E2", "Woodsmen: timber from the great forest for the city.", "Qohor (forest of Qohor)"),
    ("*", "lumberjack", "D", "Woodcutters in the hills: timber is scarce in Dorne and dearly sold.", "Dorne [STYL]"),
    ("*", "silver_mine", "*", "Silver miners and smelters.", "silver_mine"),
    ("*", "silver_mine", "W", "Miners in the silver workings of the hills.", "Westerlands (mines)"),
    ("*", "iron_mine", "*", "Iron miners and smiths.", "iron_mine"),
    ("*", "iron_mine", "I", "Thralls in the iron mines, and smiths who sell to the longships.", "Iron_Islands (thralls in mines)"),
    ("*", "iron_mine", "E2", "Iron miners: ore for the smiths of Qohor, the best in the world.", "Qohor (smiths)"),
    ("*", "clay_mine", "*", "Potters and brickmakers: clay pits by the water.", "clay_mine"),
    ("*", "clay_mine", "G", "Slave brickmakers: the red and yellow bricks of the slaver cities.", "Astapor (red brick), Yunkai (yellow)"),
    ("*", "salt_mine", "*", "Salt-makers: salt pans along the shore.", "Saltpans (riverlands) [STYL]"),
    ("*", "vineyard", "*", "Vintners: vines on the slopes and a wine press.", "vineyard"),
    ("*", "vineyard", "R", "Vintners: the vines of the Reach and a wine press.", "Reach (wine)"),
    ("*", "vineyard", "D", "Vintners: the vines that make Dornish red.", "Dornish red"),
    ("*", "vineyard", "N", "Brewers and beekeepers: barley ale and heather mead.", "North (no vines) [STYL]"),
    ("*", "vineyard", "E3", "Slaves in the vineyards and a master's press.", "Slavery (Essos)"),
    ("*", "cattle_farm", "*", "Herdsmen and cheesemakers: cattle on the common grazing.", "cattle_farm"),
    ("*", "sheep_farm", "*", "Shepherds and weavers: sheep and wool.", "sheep_farm"),
    ("*", "sheep_farm", "G", "Shepherds: sheep and wool for the looms of the slaver cities.", "Slaver's_Bay"),
    ("*", "swine_farm", "*", "Swineherds: pigs fattened in the oak woods.", "swine_farm"),
    ("*", "flax_plant", "*", "Flax growers and linen weavers.", "flax_plant"),
    ("*", "flax_plant", "E3", "Slaves in the flax fields: thread for the lacemakers of Myr.", "Myr (lace)"),
    ("*", "olive_trees", "*", "Olive growers: groves and an oil press.", "olive_trees"),
    ("*", "date_farm", "*", "Date growers and well-keepers.", "date_farm"),
    ("*", "date_farm", "D", "Date palms around a guarded well, worth more than gold.", "Dorne (wells)"),
    ("*", "silk_plant", "*", "Dyers and weavers of fine cloth.", "silk_plant (ROT) [STYL]"),
    ("*", "silk_plant", "E3", "Dyers and weavers of fine cloth, slaves to a master in {SEAT}.", "Slavery (Essos)"),
    ("*", "silk_plant", "X", "Silk growers and weavers.", "Yi Ti [STYL]"),
    ("*", "trapper", "*", "Trappers and furriers.", "trapper"),
    ("*", "trapper", "N", "Trappers: fox, marten and beaver furs for the winter.", "North [STYL]"),
    ("*", "europe_horse_ranch", "*", "Horse breeders: palfreys and plough horses.", "europe_horse_ranch"),
    ("*", "vlandian_horse_ranch", "*", "Horse breeders: coursers and destriers for knights.", "vlandian_horse_ranch"),
    ("*", "vlandian_horse_ranch", "W", "Horse breeders: destriers for the knights of the west.", "Westerlands [STYL]"),
    ("*", "battanian_horse_ranch", "*", "Horse breeders: shaggy garrons and riding horses.", "garron (North)"),
    ("*", "sturgian_horse_ranch", "*", "Horse breeders: hardy garrons for hill and moor.", "garron"),
    ("*", "desert_horse_ranch", "*", "Horse breeders: swift horses of the south.", "desert_horse_ranch"),
    ("*", "desert_horse_ranch", "D", "Breeders of sand steeds, the swift and tireless horses of Dorne.", "Sand_steed"),
    ("*", "steppe_horse_ranch", "*", "Horse herders on the open grass, selling to khalasars and sellswords.", "steppe_horse_ranch"),
    ("*", "whaler", "*", "Whalers and oil-boilers.", "whaler"),
    ("*", "whaler", "E1", "Whalers: whale oil for the lamps of Ibben and the markets beyond.", "Port_of_Ibben (whale-oil lamps)"),
    ("*", "walrus_hunter", "*", "Walrus and seal hunters.", "walrus_hunter"),
    ("*", "*", "*", "{FARMERS}: {CROPS}.", "fallback"),
    # warianty (rowna szczegolowosc -> wybor z hasha)
    ("*", "wheat_farm", "*", "Ploughmen and reapers: {CROPS}.", "wheat_farm [STYL]"),
    ("*", "wheat_farm", "*", "Farmers and beekeepers: {CROPS}.", "wheat_farm [STYL]"),
    ("*", "vineyard", "*", "Vintners and coopers: grapes, casks and a wine press.", "vineyard [STYL]"),
    ("*", "flax_plant", "*", "Linen weavers: flax retted in the ponds and spun through the winter.", "flax_plant [STYL]"),
    ("*", "cattle_farm", "*", "Herdsmen: cattle, butter and hides for the tanners.", "cattle_farm [STYL]"),
    ("*", "desert_horse_ranch", "*", "Horse breeders: lean, swift horses for the caravans.", "desert_horse_ranch [STYL]"),
    ("*", "sheep_farm", "*", "Shepherds and shearers: wool, mutton and cheese.", "sheep_farm [STYL]"),
    ("*", "lumberjack", "*", "Woodcutters and sawyers: timber, charcoal and bark for the tanners.", "lumberjack [STYL]"),
    ("*", "clay_mine", "*", "Potters and tile-makers, with kilns that smoke day and night.", "clay_mine [STYL]"),
]
# ryby wedlug miejsca i grupy (FISH)
FISH = [
    ("coast", "N", "cod, herring and seal"), ("coast", "I", "cod, herring and crabs"), ("coast", "E1", "cod, herring and eels"),
    ("coast", "D", "crabs, octopus and tuna"), ("coast", "E3", "crabs, octopus and tuna"), ("coast", "G", "crabs and shellfish"),
    ("coast", "*", "crabs, eels and mackerel"),
    ("river", "*", "eels, pike and trout"), ("lake", "*", "lake trout and eels"), ("*", "*", "eels and river fish"),
]
# table=place: miejsce;group;text - drugie zdanie (zajecie od miejsca). Pomijane, gdy kind=fishing i miejsce=coast.
PLACE = [
    ("bridge", "*", "Bridge tolls, and a smithy for passing riders.", "Bridge (toll) [STYL]"),
    ("bridge", "E1|E2|E3|G", "Bridge tolls for {SEAT}, paid in coin or in kind.", "[STYL]"),
    ("ford", "*", "Ox teams help carts across the ford, for a coin.", "[STYL]"),
    ("crossroad", "*", "An inn at the crossroads, and a market on feast days.", "Crossroads_Inn"),
    ("crossroad", "N", "An inn at the crossroads, busiest before winter.", "Kingsroad (inns rarer in the north)"),
    ("crossroad", "E1|E2|E3|G|X", "Caravans rest at the crossroads market.", "[STYL]"),
    ("road", "*", "An alehouse on the road serves carters and riders.", "Kingsroad (inns and villages)"),
    ("road", "D|E3|G|X", "A well and a stable for the caravans on the road.", "Dorne (wells) [STYL]"),
    ("river", "*", "Ferrymen and eel traps on {RIVER_OR}.", "Riverlands (river trade)"),
    ("coast", "*", "Boats drawn up on the shingle, and salt fish for the markets.", "[STYL]"),
    ("lake", "*", "Reed-cutters and boats on the lake.", "[STYL]"),
    ("field", "*", "", "-"),
    # warianty
    ("road", "*", "A smithy on the road shoes the horses of passing riders.", "[STYL]"),
    ("road", "*", "A waystation on the road sells bread and ale.", "[STYL]"),
    ("road", "D|E3|G|X", "Caravan drivers water their beasts at the village well.", "[STYL]"),
    ("river", "*", "A landing on {RIVER_OR} for barges and boats.", "[STYL]"),
    ("coast", "*", "Salt fish and seaweed are dried along the shore.", "[STYL]"),
    ("crossroad", "*", "Carters and drovers meet at the crossroads inn.", "[STYL]"),
    ("bridge", "*", "A smithy and an alehouse by the bridge.", "[STYL]"),
]

def check(s):
    # CSV RFC 4180 (przecinek, cudzyslow tylko gdy trzeba); w polach bez cudzyslowow, tabulatorow i nowych linii
    assert '"' not in s and "\t" not in s and "\n" not in s, s
    assert all(ord(c) < 128 for c in s), s
    assert "hamlet" not in s.lower(), s

def write(path, header, rows):
    import csv
    with open(path, "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f, lineterminator="\n")
        w.writerow(header)
        for r in rows:
            r = [str(x) for x in r]
            for x in r: check(x)
            w.writerow(r)

def main():
    rows = []
    for key, faith, w, src in FAITH_MAP:
        rows.append(("map", key, faith, w, "", "", "", src))
    for faith, cls, reg, text, src in FAITH_LINES:
        rows.append(("line", "", faith, "", cls, reg, text, src))
    write(os.path.join(OUT, "wiara.csv"), ["table", "key", "faith", "weight", "cls", "region", "text", "source"], rows)

    rows = []
    for g, crops in CROPS.items(): rows.append(("crops", "", "", g, "", crops, "Lore (regions) [STYL]"))
    for g, who in FARMERS.items(): rows.append(("farmers", "", "", g, "", who, "Slavery / thralls"))
    for place, g, fish in FISH: rows.append(("fish", "", "", g, place, fish, "[STYL]"))
    for kind, vt, g, text, src in MAIN: rows.append(("main", kind, vt, g, "", text, src))
    for place, g, text, src in PLACE: rows.append(("place", "", "", g, place, text, src))
    rows.append(("group", "", "", "", "", " ".join("%s=%s" % kv for kv in GROUP.items()), "region -> group"))
    write(os.path.join(OUT, "zajecia.csv"), ["table", "kind", "vtype", "group", "place", "text", "source"], rows)
    # sprawdzenie map wiary: suma wag = 100 w kazdym kluczu, kazda wiara ma linie
    from collections import defaultdict
    s = defaultdict(int)
    for key, faith, w, src in FAITH_MAP: s[key] += w
    bad = {k: v for k, v in s.items() if v != 100}
    faiths = {f for _, f, _, _ in FAITH_MAP}
    lines = {f for f, *_ in FAITH_LINES}
    print("wiara: kluczy", len(s), "zle sumy", bad, "wiary bez linii", faiths - lines, "linii", len(FAITH_LINES))
    print("zajecia: main", len(MAIN), "place", len(PLACE))

if __name__ == "__main__":
    main()
