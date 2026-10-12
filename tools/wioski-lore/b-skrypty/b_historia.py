# Badanie B: szablony jednozdaniowej historii wioski (pkt 6). Pisze historia.csv (separator ';').
# Chronologia: start ROT 299 AC; nic z Wojny Pieciu Krolow (dzieje sie w grze). Daty AC/BC wedlug AWOIAF.
# Kolumny: id;region;place;needs;seats;era;text;source
#   region = klucze krain oddzielone '|'
#   place  = any | bridge | ford | crossroad | road | river | coast | lake | field
#   needs  = '' | RIVER (rzeka z nazwa przy wiosce) | RIVER=the Trident (ta konkretna rzeka)
#   seats  = '' albo nazwy siedzib pana okregu oddzielone '|' (tylko wioski tych okregow)
# Wstawki: {VILLAGE} nazwa wioski, {DISTRICT} wies gry (glowna wioska okregu), {SEAT} zamek / miasto pana okregu
# (w srodku zdania "The Twins" -> "the Twins"), {RIVER} rzeka z rodzajnikiem ("the Red Fork").
# Historia NIE uzywa obecnego wlasciciela (zmienia sie w grze) - tylko nazwy miejsc i fakty z lore.
import os
from collections import defaultdict, Counter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.dirname(HERE)

WS = "riverlands|vale|westerlands|reach|stormlands|crownlands|dragonstone"   # ziemie Andalow na poludnie od Przesmyku
ESS_VAL = "pentos|norvos|qohor|myr|lys|tyrosh|volantis|ghis|valyria"          # ziemie z drogami Valyrii
COLONY = "myr|lys|tyrosh|volantis"                                             # dawne kolonie Valyrii
MARCH = "Nightsong|Blackhaven|Harvest Hall|Stonehelm"                          # Dornish Marches (stormlands)
T = []   # (region, place, needs, seats, era, text, source)

def add(region, place, text, era, source, needs="", seats=""):
    T.append((region, place, needs, seats, era, text, source))

# ============================== WESTEROS: wspolne dla ziem Andalow ==============================
add(WS, "bridge", "The bridge was built in the Old King's day, when roads were laid across the realm; before it there was a ferry.", "Jaehaerys I", "Kingsroad (Jaehaerys I roads)")
add(WS, "bridge", "The stone bridge over {RIVER} has paid for three lords' weddings with its tolls.", "-", "[WYM]", needs="RIVER")
add(WS, "bridge", "The first bridge here was timber and burned in some forgotten war; the stone one is younger than the sept.", "-", "[WYM]")
add(WS, "ford", "Armies have crossed at this ford in every war, so the village keeps its grain in the cellars.", "-", "[WYM]")
add(WS, "ford", "Men have forded {RIVER} here since the First Men; a holdfast on the far bank still takes the toll.", "First Men", "Holdfast [STYL]", needs="RIVER")
add(WS, "crossroad", "An inn stood at this crossing before the Conquest; its sign has changed with every king.", "-", "Crossroads_Inn [STYL]")
add(WS, "crossroad", "Wandering septons, singers and hedge knights meet at the crossroads inn on their way to everywhere.", "-", "Hedge_knight; Faith_of_the_Seven")
add(WS, "road", "The village grew along the road one alehouse at a time; carters still stop here for the night.", "-", "Kingsroad (inns and villages)")
add(WS, "road", "Hedge knights ride this road looking for service, and the village feeds them for their news.", "-", "Hedge_knight")
add(WS, "river", "Spring floods once carried off half the houses; the new ones stand on the high bank.", "-", "[WYM]")
add(WS, "river", "Barges and fishing boats tie up at the landing on {RIVER}; the village has always lived off the water.", "-", "Riverlands (river trade)", needs="RIVER")
add(WS, "coast", "Its fishers have worked this shore since before the Andals; the boats are still built the old way.", "First Men", "[STYL]")
add(WS, "field", "The village lies among its fields, away from the road, and most wars have passed it by.", "-", "[WYM]")
add(WS, "field", "Its fields were cleared from the woods by younger sons who could hope for no land at home.", "-", "[STYL]")
add(WS, "lake", "Its boats fish the lake; on still days the far shore seems close enough to touch.", "-", "[WYM]")
add(WS, "any", "Broken men and outlaws come with every war, and the village has learned to hide its seed corn.", "-", "Broken men (AFFC 25)")

add(WS, "any", "Its oldest families say they were here before the Andals, and the Andals married them anyway.", "Andals", "Coming_of_the_Andals [STYL]")
add(WS, "any", "The village has paid its rents in grain and eggs to {SEAT} for as long as anyone remembers.", "-", "[WYM]")
add(WS, "any", "Floods, sickness and war have each half emptied the village, and each time it filled again.", "-", "[WYM]")

# ============================== POLNOC ==============================
R = "north"
add(R, "any", "First Men cleared these fields; the stones of their old ringfort still lie under the turf.", "First Men", "First_Men (ringforts)")
add(R, "any", "Its people say their forebears fought for the Kings of Winter long before any dragon came.", "Kings of Winter", "House_Stark")
add(R, "any", "In hard winters its people shelter behind the walls of {SEAT} and come home with the thaw.", "-", "Winter town (AGOT 37)")
add(R, "any", "The old men still say Torrhen Stark knelt to the dragon so that their forefathers would not burn.", "Conquest", "Torrhen_Stark")
add(R, "any", "Men from the village marched south in Robert's Rebellion; fewer came back from the Trident.", "282-283 AC", "Robert's_Rebellion")
add(R, "any", "Some of its men sailed with Lord Stark to the siege of Pyke in 289 AC.", "289 AC", "Greyjoy's_Rebellion")
add(R, "any", "A holdfast of the First Men stood here once; only its well and a grassy barrow remain.", "First Men", "[STYL]")
add(R, "any", "Its fields were first given to a younger son of {SEAT}, with a holdfast and the land around it.", "-", "Holdfast (AGOT 5)")
add(R, "any", "A hard winter once took half the village; the dead lie under the cairn on the hill.", "-", "Winter [WYM]")
add(R, "any", "The Andals never conquered the North, and the old men here are proud of it.", "Andals", "Coming_of_the_Andals; Moat_Cailin")   # E7/L P10: "never came north" przeczyl north_22 (statki Andalow na Weeping Water)
add(R, "road", "When the Old King's roads were laid, the cattle track past the village became a road.", "Jaehaerys I", "Kingsroad (to the Wall)")
add(R, "road", "Riders from {SEAT} change horses here; the stable is older than the stone well.", "-", "[WYM]")
add(R, "crossroad", "Two old tracks met here long before the Conquest; the inn at the crossing has had many names.", "-", "[STYL]")
add(R, "bridge", "A lord of {SEAT} built the bridge after the old ford drowned a wedding party.", "-", "[WYM]")
add(R, "ford", "The ford is passable from spring to the first snows; in winter the river is crossed on the ice.", "-", "[WYM]")
add(R, "river", "The village grew where {RIVER} could be crossed in summer and fished in every season.", "-", "[STYL]", needs="RIVER")
add(R, "river", "Its first houses stood on the high bank, above the floods of the spring thaw.", "-", "[WYM]")
add(R, "coast", "Fishers here have worked the cold sea since the First Men; the boats are still built the old way.", "First Men", "[STYL]")
add(R, "field", "Its fields were won from the wood by axe and fire; the stumps still show at the edges.", "-", "[WYM]")
add(R, "any", "The Barrow Kings once ruled here as Kings of the First Men; their mounds still dot the fields.", "Age of Heroes", "Barrow_Kings", seats="Barrowton")
add(R, "any", "Under the Red Kings of the Dreadfort its people learned to fear the flayed man; some still do.", "Age of Heroes", "Red_Kings; House_Bolton", seats="Dreadfort")
add(R, "any", "Andal ships once came up the Weeping Water; King Theon Stark and the Boltons threw them back.", "Andals", "Red_Kings (Battle of the Weeping Water)", seats="Dreadfort")
add(R, "any", "Many families here came north with the Manderlys, when the Gardener kings drove them from the Reach.", "c. 1000 BC", "House_Manderly", seats="White Harbor|Ramsgate")
add(R, "any", "No southern host has ever taken Moat Cailin, and the village has never seen an Andal king.", "Andals", "Moat_Cailin", seats="Moat Cailin")
add(R, "any", "Crannogmen trade frogs and fish here; their castle moves, they say, and no stranger finds it.", "-", "Greywater_Watch", seats="Greywater Watch")
add(R, "any", "The Starks have held this land since the Age of Heroes, and the village has never known another liege.", "Age of Heroes", "House_Stark", seats="Winterfell")
add(R, "any", "In 226 AC its men fought Raymun Redbeard's wildlings at Long Lake beside the Umbers and Lord Willam Stark.", "226 AC", "Battle_at_Long_Lake", seats="Last Hearth")
add(R, "any", "The Umbers have held back wildlings here for centuries, and every house keeps an axe by the door.", "-", "House_Umber [STYL]", seats="Last Hearth")
add(R, "any", "The Karstarks are Starks by blood, the village says, and says it proudly.", "-", "House_Karstark (Karlon Stark)", seats="Karhold")
add(R, "coast", "Ironborn reavers have harried this shore for centuries, and the boats are kept far up the beach.", "Old Way", "Stony_Shore (centuries of reaving)", seats="Flint's Finger|Deepwood Motte|Torrhen's Square|Ironrath")

# ============================== DAR / NOCNA STRAZ ==============================
R = "gift"
add(R, "any", "Brandon's Gift has been farmed for thousands of years to feed the Watch; the village is one of the few left.", "Age of Heroes", "Brandon's_Gift")
add(R, "any", "Queen Alysanne asked for the New Gift in 58 AC; settlers came then, and wildling raids drove most away.", "58 AC", "New_Gift")
add(R, "any", "Wildling raiders cross the Wall every few winters; the village pays its tithe to the Watch and hopes.", "-", "Wall [STYL]")
add(R, "any", "In 226 AC Raymun Redbeard's wildlings passed through; the black brothers came too late to help.", "226 AC", "Battle_at_Long_Lake (Sleepy Jack)")
add(R, "any", "The Nightfort was abandoned in the Old King's day; the village keeps well clear of its ruins.", "Jaehaerys I", "Nightfort")
add(R, "any", "Like Mole's Town, the village lives half under the ground when winter comes.", "-", "Mole's_Town (AGOT 70)")
add(R, "any", "Every boy here has watched black brothers ride by, and some have taken the black themselves.", "-", "Night's_Watch")
add(R, "any", "The Watch had ten thousand men at the Conquest and has fewer than a thousand now; the village feels it.", "Conquest", "Night's_Watch (TWOIAF)")
add(R, "any", "Its orchards went wild when the Watch could no longer keep them; the village picks what it can.", "-", "Brandon's_Gift (nature reclaimed orchards)")
add(R, "any", "Hardhome burned six hundred years ago; old women here say its glow lit the northern sky.", "c. 300 BC", "Hardhome")
add(R, "road", "Lords send their criminals to the Wall along this road, and the village feeds them on the way.", "-", "Night's_Watch [STYL]")
add(R, "field", "Its fields were cleared by settlers of the New Gift; half of them lie fallow now.", "58 AC", "New_Gift")
add(R, "coast", "When ships come to Eastwatch, they buy salt fish here; most years they do not come.", "-", "Eastwatch [STYL]")
add(R, "any", "In the long winters the village shuts its doors and lives on what it salted in autumn.", "-", "[WYM]")
add(R, "any", "The black brothers' rangers come back fewer every year, and the village has stopped asking why.", "-", "Night's_Watch [STYL]")

# ============================== ZA MUREM ==============================
R = "beyond"
add(R, "any", "Free folk have lived here since before there was a Wall, and they kneel to no one.", "Long Night", "Free_folk")
add(R, "any", "Raymun Redbeard gathered the clans in 226 AC; the village sent men south, and few came back from Long Lake.", "226 AC", "Raymun_Redbeard")
add(R, "any", "Bael the Bard stole a Stark daughter, the songs say, and the village still sings it to annoy the crows.", "Legend", "Bael_the_Bard")
add(R, "any", "Hardhome burned six hundred years ago, and no one from the village will camp near its ruins.", "c. 300 BC", "Hardhome")
add(R, "any", "Joramun blew the Horn of Winter and woke giants from the earth, the old women say.", "Legend", "Horn_of_Winter")
add(R, "any", "Thenns come down from their hidden valley to trade bronze for furs, and keep apart.", "-", "Thenns")
add(R, "any", "Weirwoods still grow thick in the woods here; the free folk say the old gods see everything.", "-", "Haunted_forest")
add(R, "any", "Raiders from the village have crossed the Wall more than once, and come back with southron steel.", "-", "Free_folk")
add(R, "any", "Gendel and Gorne led the free folk under the Wall in the old days, the singers say.", "Legend", "Gendel; Gorne")
add(R, "any", "Its people are hunters and spearwives; the strongest chief takes what he can hold.", "-", "Free_folk")
add(R, "any", "Winters here kill more than any war; every family has lost someone to the snow.", "-", "[WYM]")
add(R, "any", "The Frostfangs loom over the village; in summer the clans come down to trade.", "-", "Frostfangs")
add(R, "any", "Mammoth bones mark the path to the village; the giants passed this way once.", "-", "Giants (mammoths)")
add(R, "river", "The village sits on {RIVER}, where the clans meet when the hunting is poor.", "-", "Milkwater [STYL]", needs="RIVER")
add(R, "coast", "Slavers from across the sea once took people from this shore, the elders say.", "-", "Hardhome (slavers tale)")

# ============================== SKAGOS ==============================
R = "skagos"
add(R, "any", "The stoneborn rose against Winterfell in the reign of Daeron II; Lord Barthogan Stark died putting them down.", "c. 200 AC", "Barthogan_Stark")
add(R, "any", "Traders say the lords of Skagos ride shaggy unicorns; the village does not deny it.", "-", "Skagosi")
add(R, "any", "Mainlanders say the Skagosi eat men in winter; the village says mainlanders talk too much.", "-", "Skagosi (rumours)")
add(R, "any", "Skagos is Stark land in name; the village has seen a Stark ship once in living memory.", "-", "Skagos (AFFC 15)")
add(R, "coast", "Ships that pass too close to this coast are said to be lured onto the rocks by false lights.", "-", "Skagosi (rumours)")
add(R, "any", "Some tales blame Skagosi cannibals for the ruin of Hardhome; the village has its own tale.", "c. 300 BC", "Hardhome")
add(R, "any", "The stoneborn clans have held these crags since the First Men, and no Andal ever came.", "First Men", "Skagosi")
add(R, "any", "The village lives in stone houses half sunk into the hill, out of the sea wind.", "-", "Skagosi (caves, fastnesses) [STYL]")
add(R, "any", "Its clan has feuded with the next valley for longer than anyone can count.", "-", "[WYM]")
add(R, "any", "Wildling ships sometimes put in here; the stoneborn trade with them, and fight them.", "-", "[STYL]")
add(R, "coast", "Seals and whales feed the village in winter, and their oil lights its halls.", "-", "[STYL]")
add(R, "any", "The village pays no tax to Winterfell and sends no levies; the Starks have stopped asking.", "-", "Skagos (in name only)")
add(R, "any", "A weirwood grove above the village is older than any house on the island.", "-", "Old_gods")
add(R, "any", "The stoneborn bury their dead in the rock, and do not speak of them to strangers.", "-", "[WYM]")
add(R, "any", "Sailors from White Harbor call this coast cursed, and steer wide of it.", "-", "Skagos [STYL]")

# ============================== DORZECZE ==============================
R = "riverlands"
add(R, "any", "First Men raised the first huts here; the Andals came later, with steel and the seven-pointed star.", "Andals", "Coming_of_the_Andals")
add(R, "any", "Under the Storm Kings, and later the ironborn kings, the village paid tribute it never forgot.", "Storm Kings; Hoare", "Arlan_III; House_Hoare")
add(R, "any", "Men from the village died in Harren's quarries, chained to the sledges that built Harrenhal.", "Hoare", "Harrenhal (forty years, captives)")
add(R, "any", "When Aegon came, Lord Edmyn Tully rose against Harren, and the men of the village rose with him.", "2 BC", "Edmyn_Tully")
add(R, "any", "In 196 AC its men marched to the Redgrass Field, some for the red dragon and some for the black.", "196 AC", "Battle_of_the_Redgrass_Field")
add(R, "any", "Its men fought on the Trident in 283 AC, though no one here agrees on which side.", "283 AC", "Battle_of_the_Trident")
add(R, "river", "After the Trident in 283 AC the village children dived for Rhaegar's rubies; none were ever found.", "283 AC", "Battle_of_the_Trident (rubies)", needs="RIVER=the Trident")
add(R, "river", "Barges have carried grain down {RIVER} from this landing since the days of the river kings.", "-", "Riverlands (river trade)", needs="RIVER")
add(R, "road", "Mummers' carts and wandering septons pass in every season, and the village never lacks for news.", "-", "Mummers (ASOS 2); septons (AFFC 25)")
add(R, "field", "Its fields lie away from the river, too poor to be fought over, and so the village has survived.", "-", "[WYM]")
add(R, "lake", "On a clear day the Isle of Faces can be seen from the shore; no one rows out there.", "-", "Isle_of_Faces")
add(R, "any", "In 129 AC Brackens and Blackwoods fought by a burning mill nearby; the feud is older than the mill.", "129 AC", "Battle_of_the_Burning_Mill", seats="Stone Hedge|Raventree Hall")
add(R, "any", "Brackens and Blackwoods have quarrelled over these fields for a thousand years, and both have burned it.", "-", "House_Blackwood; House_Bracken", seats="Stone Hedge|Raventree Hall")
add(R, "any", "When the bells of Stoney Sept rang in 283 AC, the village hid its people in the cellars.", "283 AC", "Battle_of_the_Bells", seats="Stoney Sept")
add(R, "any", "Its lords swear to Riverrun, where Aegon raised the Tullys over all the riverlands.", "1 AC", "Edmyn_Tully (Lord Paramount)", seats="Riverrun")
add(R, "any", "Every cart that crosses at the Twins pays the Freys, and the village sells them bread, not love.", "-", "The_Twins (toll)", seats="The Twins")
add(R, "coast", "In 289 AC ironborn longships came against Seagard, where Lord Jason Mallister slew Rodrik Greyjoy.", "289 AC", "Storming_of_Seagard", seats="Seagard")
add(R, "any", "The ruined towers of Harrenhal cast long shadows; folk here say the castle is cursed, and many lords agree.", "-", "Curse_of_Harrenhal", seats="Harrenhal")
add(R, "any", "Harren cut weirwoods three thousand years old for his rafters; the village still curses his name.", "Hoare", "Harrenhal (weirwood beams)", seats="Harrenhal")
add(R, "any", "Harwyn Hardhand won the riverlands at Fairmarket, and the village paid the ironborn for three generations.", "Hoare", "Harwyn_Hoare", seats="Fairmarket")
add(R, "any", "Smallfolk here will not climb High Heart at night; the ghosts of the children of the forest walk there.", "Andals", "High_Heart", seats="Acorn Hall")
add(R, "coast", "Salt has been dried in pans along this shore for as long as Saltpans has had its name.", "-", "Saltpans [STYL]", seats="Saltpans")

# ============================== DOLINA ==============================
R = "vale"
add(R, "any", "The first Andal ships landed on the Fingers, and the village says its founders came with them.", "Andals", "Coming_of_the_Andals (Fingers)")
add(R, "any", "Since the Andals the Arryns have ruled the Vale, and the village has paid its dues to the Eyrie for thousands of years.", "Andals", "House_Arryn")
add(R, "any", "Clansmen from the Mountains of the Moon have raided here in living memory; the knights ride out every spring.", "-", "Vale_mountain_clans")
add(R, "any", "Its men rode under Jon Arryn's banners in 282 AC, when the Vale rose for Robert.", "282 AC", "Robert's_Rebellion")
add(R, "any", "In the Dance of the Dragons the Vale stood for Rhaenyra, and the village sent men and grain.", "129-131 AC", "Jeyne_Arryn")
add(R, "any", "The Vale shut its roads and ports in 209 AC, and the Great Spring Sickness passed the village by.", "209 AC", "Great_Spring_Sickness")
add(R, "any", "The Arryn kings built septs and septries across the Vale; the village sept is one of them.", "Andals", "Vale (learning at septs)")
add(R, "any", "In 97 AC the Stone Crows killed Lady Jeyne's father and brothers; the village has kept its spears since.", "97 AC", "Stone_Crows")
add(R, "river", "Snowmelt from the Mountains of the Moon floods the river each spring, so the village stands on the high bank.", "-", "Mountains_of_the_Moon [STYL]")
add(R, "crossroad", "The valley roads meet here; septons, wool carts and knights pass in every season.", "-", "[STYL]")
add(R, "field", "Its fields lie in a fold of the hills, out of sight of the road and of the clansmen.", "-", "Vale_mountain_clans [STYL]")
add(R, "any", "Every host that tried the Bloody Gate has broken there, and the village has fed the garrison for centuries.", "-", "Bloody_Gate", seats="Bloody Gate")
add(R, "any", "When Jon Arryn took Gulltown from the Graftons in 282 AC, the village quartered his knights.", "282 AC", "Taking_of_Gulltown", seats="Gulltown")
add(R, "any", "The Royces of Runestone are older than the Arryns, and their bronze runes are older still.", "First Men", "House_Royce", seats="Runestone")
add(R, "any", "The Corbrays won the Fingers in the Andal wars, and the village still sings of it.", "Andals", "Coming_of_the_Andals (Corbray)", seats="Heart's Home")
add(R, "any", "Its old songs are of the Battle of the Seven Stars, fought at the foot of the Giant's Lance.", "Andals", "Battle_of_the_Seven_Stars", seats="The Eyrie")
add(R, "coast", "Mainlanders call the Sistermen smugglers and wreckers; the village says only that it fishes.", "-", "Sisterton", seats="Sisterton")

# ============================== ZACHOD ==============================
R = "westerlands"
add(R, "any", "Gold and silver have come out of these hills since the First Men, and the Lannisters have taken their share since Lann.", "Age of Heroes", "Westerlands (mines); Lann_the_Clever")
add(R, "any", "The Kings of the Rock ruled here until Loren Lannister knelt after the Field of Fire.", "2 BC", "Field_of_Fire")
add(R, "any", "When the Andals came, the Lannisters took them in as wards and brides, and the first sept was raised here.", "Andals", "House_Lannister (Andals)")
add(R, "any", "In 261 AC its men marched with young Tywin Lannister against the Reynes of Castamere.", "261 AC", "Reyne-Tarbeck_revolt")
add(R, "any", "Its people still sing The Rains of Castamere, and know better than to sing it at the wrong table.", "261 AC", "The_Rains_of_Castamere")
add(R, "any", "Its men marched with Lord Tywin to King's Landing in 283 AC, and some came home richer.", "283 AC", "Sack_of_King's_Landing")
add(R, "coast", "The Red Kraken's longships harried this shore in 130 AC and carried off wives and daughters.", "130 AC", "Sack_of_Lannisport (Dalton Greyjoy)")
add(R, "river", "The stream below the village once ran yellow with mine washings, the old women say.", "-", "[WYM]")
add(R, "road", "Ore carts and wool carts have worn ruts in this road since the Kings of the Rock.", "-", "[STYL]")
add(R, "any", "In 289 AC the village watched the Lannister fleet burn in the harbour of Lannisport.", "289 AC", "Burning_of_the_Lannister_fleet", seats="Lannisport|Kayce|Casterly Rock")
add(R, "any", "The Great Spring Sickness of 209 AC struck Lannisport hard and took many in the village.", "209 AC", "Great_Spring_Sickness (Lannisport)", seats="Lannisport|Kayce|Casterly Rock")
add(R, "any", "The Golden Tooth guards the pass to the riverlands, and armies have come through it in every war.", "-", "Golden_Tooth", seats="Goldentooth")
add(R, "any", "The silver workings in the hills above the village are older than any lord's name.", "First Men", "Westerlands (mines) [STYL]", seats="Silverhill")
add(R, "coast", "The Banefort watches the sea for ironborn sails, and the village keeps a beacon on the hill.", "Old Way", "Banefort [STYL]", seats="Banefort")
add(R, "any", "The Westerlings' gold mines gave out long ago, and the village lives from the sea and the fields now.", "-", "House_Westerling (mines exhausted)", seats="The Crag")
add(R, "any", "The Cleganes won their land when a kennelman saved a Lannister from a lioness; the village remembers the dogs.", "c. 260 AC", "House_Clegane", seats="Clegane's Keep")

# ============================== REACH ==============================
R = "reach"
add(R, "any", "Garth Greenhand taught the First Men to sow here, the old songs say, and the soil has never failed since.", "Legend", "Garth_Greenhand")
add(R, "any", "The Gardener kings welcomed the Andals with marriages, not swords, and the village took the Seven in peace.", "Andals", "King_of_the_Reach (Three Sage Kings)")
add(R, "any", "Its men went to the Field of Fire with King Mern in 2 BC, and the dragons burned them there.", "2 BC", "Field_of_Fire")
add(R, "any", "When Aegon gave Highgarden to the Tyrells, the village paid its dues to new masters without a battle.", "1 AC", "House_Tyrell")
add(R, "any", "Its men spent a year at the siege of Storm's End in Robert's war, and came home with nothing but stories.", "282-283 AC", "Siege_of_Storm's_End")
add(R, "any", "A Gardener king built the first sept here; the Tyrells only gave it a new roof.", "Andals", "House_Gardener (Merle I septs)")
add(R, "road", "Carts of grain and apples roll past {VILLAGE} all autumn, bound for {SEAT}.", "-", "Reach [STYL]")
add(R, "crossroad", "Travelling knights, singers and septons meet at the inn here on the way to every tourney.", "-", "[STYL]")
add(R, "field", "Its orchards and fields lie off the road, quiet enough that no war has found it in a century.", "-", "Reach [STYL]")
add(R, "river", "Barges have carried wine and grain down {RIVER} from this landing since the Gardener kings.", "-", "Mander (river trade) [STYL]", needs="RIVER")
add(R, "coast", "Ironborn longships raided this coast in the old days, until the Gardener kings armed the Shield Islands.", "Old Way", "Shield_Islands (Garth VII)")
add(R, "any", "In 130 AC the Two Betrayers burned Tumbleton with dragonfire, and the village took in what was left.", "130 AC", "First_Battle_of_Tumbleton", seats="Tumbleton")
add(R, "any", "Ormund Hightower's host was trapped on the Honeywine in 130 AC, a day's ride from here.", "130 AC", "Battle_of_the_Honeywine", seats="Honeyholt")
add(R, "any", "Before the Field of Fire, Loren and Mern joined their hosts at Goldengrove, and the village fed both kings.", "2 BC", "Field_of_Fire (Goldengrove)", seats="Goldengrove")
add(R, "any", "At Ashford in 282 AC Lord Tarly broke Robert Baratheon's van before Mace Tyrell even arrived.", "282 AC", "Battle_of_Ashford", seats="Ashford")
add(R, "any", "The Great Spring Sickness of 209 AC was worse in Oldtown than anywhere but King's Landing.", "209 AC", "Great_Spring_Sickness", seats="Oldtown")
add(R, "any", "Acolytes of the Citadel walk out here to study the soil and the stars, and pay for their ale.", "-", "Citadel [STYL]", seats="Oldtown")
add(R, "any", "Horn Hill guards the Dornish marches, and Dornish raiders have burned the village more than once.", "-", "Dornish_Marches (Horn Hill)", seats="Hornhill")
add(R, "coast", "The Shield Islands were armed against ironborn reavers, and the village keeps its boats ready.", "Old Way", "Shield_Islands", seats="Lord Hewett's Town")
add(R, "any", "The Florents say Highgarden should have been theirs, and the village hears it at every harvest feast.", "-", "House_Florent", seats="Brightwater Keep")
add(R, "any", "The Oakhearts of Old Oak are older than the Tyrells, and the village never lets anyone forget it.", "-", "House_Oakheart", seats="Old Oak")
add(R, "any", "Its people send grain and roses to Highgarden, as they did when green-crowned kings sat there.", "-", "House_Gardener", seats="Highgarden")

# ============================== ZIEMIE BURZY ==============================
R = "stormlands"
add(R, "any", "The Storm Kings ruled here from Storm's End for thousands of years; its walls, they say, were raised by Bran the Builder.", "Age of Heroes", "House_Durrandon; Storm's_End")
add(R, "any", "Its men followed Argilac the Arrogant to the Last Storm, where Orys Baratheon slew him.", "2 BC", "House_Durrandon (Last Storm)")
add(R, "any", "In 282 AC Robert won three battles in one day at Summerhall, and men from here fought in all three.", "282 AC", "Battles_at_Summerhall")
add(R, "any", "It rains here more than anywhere in the realm, the villagers boast, and their mossy roofs agree.", "-", "Stormlands (TWOIAF)")
add(R, "any", "In the Dance of the Dragons the storm lords followed Lord Borros Baratheon for Aegon II.", "129-131 AC", "Borros_Baratheon")
add(R, "any", "When Lyonel Baratheon, the Laughing Storm, crowned himself Storm King, the village sent him its sons.", "c. 239 AC", "Lyonel_Baratheon")
add(R, "any", "Arlan III led the storm lords north and took the riverlands, and the village's forefathers went with him.", "Storm Kings", "Arlan_III_Durrandon")
add(R, "river", "{RIVER} floods with the autumn rains, and the village has learned to build on the high ground.", "-", "[WYM]", needs="RIVER")
add(R, "coast", "Storms off the narrow sea wreck ships every autumn, and the village salvages what the sea gives.", "-", "Shipbreaker_Bay [STYL]")
add(R, "road", "The Kingswood Brotherhood robbed travellers on this road until the Kingsguard broke them in 281 AC.", "281 AC", "Kingswood_Brotherhood", seats="Bronzegate|Haystack Hall|Storm's End")
add(R, "any", "The Last Storm was fought near Bronzegate in 2 BC, and ploughs here still turn up rusted helms.", "2 BC", "House_Durrandon (Last Storm near Bronzegate)", seats="Bronzegate")
add(R, "any", "During the year-long siege of Storm's End the Reach host ate the village bare.", "282-283 AC", "Siege_of_Storm's_End", seats="Storm's End")
add(R, "any", "Dornish raiders came over the mountains for a thousand years, and every house here has a hiding cellar.", "-", "Dornish_Marches", seats=MARCH)
add(R, "any", "Summerhall burned in 259 AC and the king died with it; the village saw the glow from its fields.", "259 AC", "Tragedy_at_Summerhall", seats=MARCH)
add(R, "any", "In the First Dornish War Lord Fowler came over the mountains and burned Nightsong.", "4-13 AC", "Dornish_Marches (Nightsong burned)", seats="Nightsong")
add(R, "any", "The Selmys of Harvest Hall gave the realm Ser Barristan the Bold, and every boy here has fought him with a stick.", "-", "House_Selmy", seats="Harvest Hall")
add(R, "any", "The Rainwood begins behind the last field, and outlaws and ghosts are both said to live there.", "-", "Rainwood [STYL]", seats="Rain House")

# ============================== ZIEMIE KORONY ==============================
R = "crownlands"
add(R, "any", "Aegon landed at the mouth of the Blackwater in 2 BC, and the lords hereabouts were among the first to kneel.", "2 BC", "Aegon's_Landing")
add(R, "any", "Its people have seen Targaryen and Baratheon kings ride past, and paid the same taxes to both.", "-", "[WYM]")
add(R, "any", "In the Dance of the Dragons the city changed hands more than once, and the village fed whoever held it.", "129-131 AC", "Dance_of_the_Dragons [STYL]")
add(R, "road", "The Old King's roads were begun in 62 AC, and the village grew up beside one of them.", "62 AC", "Kingsroad (begun 62 AC)")
add(R, "road", "The Kingswood Brotherhood robbed travellers on this road until the Kingsguard broke them in 281 AC.", "281 AC", "Kingswood_Brotherhood")
add(R, "river", "Barges come down the Blackwater Rush to the city, and the village sells them bread and eels.", "-", "Blackwater_Rush [STYL]", needs="RIVER=the Blackwater Rush")
add(R, "coast", "Its fishers sell crabs and eels in the city's markets, and the walk there takes all night.", "-", "[STYL]")
add(R, "field", "Its fields have fed the capital since Aegon's day, and the tax-men know every furrow.", "-", "[WYM]")
add(R, "any", "Queen Rhaenys flew to Rosby in the Conquest, and Rosby yielded without a fight.", "2 BC", "Aegon's_Conquest (Rosby, Stokeworth)", seats="Rosby")
add(R, "any", "In 277 AC King Aerys was held captive in Duskendale for half a year, and the village paid for the Defiance.", "277 AC", "Defiance_of_Duskendale", seats="Duskendale")
add(R, "any", "The Darklyns once ruled from Duskendale as kings; after the Defiance not one of them was left.", "277 AC", "House_Darklyn", seats="Duskendale")
add(R, "any", "The Great Spring Sickness of 209 AC killed four in ten in King's Landing; the village buried its dead in the fields.", "209 AC", "Great_Spring_Sickness", seats="King's Landing|Red Keep")
add(R, "any", "When the Lannisters sacked King's Landing in 283 AC, refugees filled the village for a month.", "283 AC", "Sack_of_King's_Landing", seats="King's Landing|Red Keep")
add(R, "any", "In 130 AC a mob stormed the Dragonpit, and the village saw the fires from its fields.", "130 AC", "Storming_of_the_Dragonpit", seats="King's Landing|Red Keep")
add(R, "any", "The lords hereabouts have sent their sons to court for three hundred years, and their debts home.", "-", "[WYM]")

# ============================== SMOCZA SKALA I ZATOKA ==============================
R = "dragonstone"
add(R, "any", "The Targaryens came to Dragonstone before the Doom, and these lands have served dragons ever since.", "c. 114 BC", "House_Targaryen (Dragonstone)")
add(R, "any", "Dragons flew over this coast in the Dance of the Dragons, and the village still tells of fire in the sky.", "129-131 AC", "Dance_of_the_Dragons [STYL]")
add(R, "any", "After Robert's war the last Targaryen ships left Dragonstone, and Stannis Baratheon came to rule it.", "284 AC", "Assault_on_Dragonstone")
add(R, "coast", "In 130 AC the Triarchy's fleet sacked Spicetown on Driftmark, and the village still fears sails from the east.", "130 AC", "Spicetown; Battle_of_the_Gullet")
add(R, "coast", "Storms off the narrow sea take a boat from the village every year; the widows keep the count.", "-", "[WYM]")
add(R, "road", "Riders bound for King's Landing water their horses here, and leave their news behind.", "-", "[STYL]")
add(R, "any", "The Crackclaws are First Men blood; no Andal king ever conquered them, and they remind visitors of it.", "Andals", "Crackclaw_Point", seats="Dyre Den")
add(R, "any", "Blood feuds run deep here; two families of the village have not spoken since before the Conquest.", "-", "Crackclaw_Point (blood feuds)", seats="Dyre Den")
add(R, "any", "In 129 AC Princess Rhaenys and her dragon Meleys fell in battle at Rook's Rest, close by.", "129 AC", "Battle_of_Rook's_Rest", seats="Rook's Rest")
add(R, "any", "Lord Fell rode to Summerhall in 282 AC and died there by Robert's hand; the village mourned him.", "282 AC", "Battles_at_Summerhall (Lord Fell)", seats="Fellwood")
add(R, "any", "The Celtigars came from Valyria with the Targaryens, and the village serves them as it always has.", "c. 114 BC", "House_Celtigar", seats="Celtigar Keep")
add(R, "any", "A smuggler from this coast slipped onions past the siege of Storm's End in 283 AC; the village still tells it.", "283 AC", "Davos_Seaworth", seats="Cape Wrath")   # E7/L P10: bez "its lord"

# ============================== ZELAZNE WYSPY ==============================
R = "iron"
add(R, "any", "The drowned men say the Grey King himself once walked this shore.", "Age of Heroes", "Grey_King")
add(R, "any", "In the Old Way its men paid the iron price on every coast of the Sunset Sea.", "Old Way", "Iron_Islands (Old Way)")
add(R, "any", "Thralls taken in raids tilled this thin soil while the ironborn went reaving.", "Old Way", "Iron_Islands (thralls)")
add(R, "any", "Its longships sailed with Harwyn Hardhand when the ironborn won the riverlands.", "Hoare", "Harwyn_Hoare")
add(R, "any", "When Harrenhal burned in 2 BC, the village lost sons who had gone east with Harren.", "2 BC", "Burning_of_Harrenhal")
add(R, "any", "After Harren burned, the ironborn chose Vickon Greyjoy to rule them, and the village gave him its oars.", "1 AC", "House_Greyjoy")
add(R, "any", "In 130 AC its longships followed the Red Kraken to sack Lannisport.", "130 AC", "Sack_of_Lannisport")
add(R, "any", "The dragon kings forbade the Old Way; the village grumbled and went fishing, mostly.", "AC", "Iron_Islands (Old Way suppressed)")
add(R, "coast", "Most families here fish; the rest go reaving when the longships sail.", "-", "[STYL]")   # E7/L P9: bez Haerega ("seven families in ten")
add(R, "field", "The fields are thin and stony; what grows here grows because thralls broke their backs over it.", "-", "Iron_Islands (thin soil)")
add(R, "any", "Each island once had a rock king and a salt king; the village remembers serving both.", "Age of Heroes", "Iron_Islands (rock and salt kings)")
add(R, "any", "Its people despise the gold price; what a man has not taken with iron is not truly his.", "Old Way", "Iron_Islands (iron price)")
add(R, "any", "The Iron Fleet took men from the village every summer of Balon's rebellion in 289 AC.", "289 AC", "Greyjoy's_Rebellion")
add(R, "any", "Driftwood shelters of the drowned men stand on the beach below the village.", "-", "Priest_of_the_Drowned_God (driftwood)")
add(R, "any", "In 289 AC King Robert's knights landed on these islands to end Balon's rebellion.", "289 AC", "Greyjoy's_Rebellion", seats="Ten Towers|Pebbleton")
add(R, "any", "Lonely Light lies far out in the western sea; the Farwynds, men say, can take the shape of seals.", "-", "House_Farwynd", seats="Lonely Light")
add(R, "coast", "No ironborn of the village was ever buried on land; they are all given back to the sea.", "-", "Drowned_God")
add(R, "any", "Its men still tell of the kingsmoot on Old Wyk, where the first High King was chosen.", "Age of Heroes", "Iron_Islands (Urras Greyiron)")

# ============================== DORNE ==============================
R = "dorne"
add(R, "any", "Nymeria's Rhoynar came ashore about seven hundred years before the Conquest, and their blood runs in the village.", "c. 700 BC", "Ten_thousand_ships")
add(R, "any", "When Nymeria burned her ships, the Rhoynar stayed; their customs still rule inheritance here.", "c. 700 BC", "Nymeria's_War; Dorne (equal inheritance)")
add(R, "any", "Dorne never knelt to Aegon; in the First Dornish War the village hid in the hills while dragons burned the castles.", "4-13 AC", "Conquest_of_Dorne (First Dornish War)")
add(R, "any", "The Young Dragon conquered Dorne in 158 AC and lost it within three years; the village saw both.", "157-161 AC", "Daeron_I_Targaryen")
add(R, "any", "Dorne joined the realm by marriage in 187 AC, not by the sword, and the village is proud of it.", "187 AC", "Wedding_of_Maron_Martell_and_Daenerys_Targaryen")
add(R, "any", "Prince Lewyn Martell led Dornish spears to the Trident in 283 AC, and few came back to the village.", "283 AC", "Battle_of_the_Trident")
add(R, "any", "In Dorne water is worth more than gold, and the lords of the wells fought over this one for centuries.", "-", "Dorne (lords of the wells)")
add(R, "any", "Caravans cross the deep sand to the village wells, and the well-keeper is the richest man here.", "-", "Dorne (caravans) [STYL]")
add(R, "river", "Everything green here lives off {RIVER}; the wells were dug and guarded before any castle.", "-", "Dorne (rivers)", needs="RIVER")
add(R, "river", "Orphans of the Greenblood pole their rafts past the village, still mourning Mother Rhoyne.", "-", "Orphans_of_the_Greenblood", needs="RIVER=the Greenblood")
add(R, "coast", "The salt Dornishmen of this coast trade with Lys and Tyrosh as readily as with the realm.", "-", "Dorne (salt, sand, stony)")
add(R, "bridge", "The bridge spans a river half dry in summer; travellers mock it until the floods come.", "-", "[WYM]")
add(R, "road", "The road follows the old caravan track from well to well, and the village is one of the wells.", "-", "Dorne (caravans) [STYL]")
add(R, "field", "Its fields are watered by little canals; whoever cuts a neighbour's channel answers for it with blood.", "-", "Dorne (irrigation, AFFC 22) [STYL]")
add(R, "any", "Andal adventurers carved out petty kingdoms here long before Nymeria; the village served three of them.", "Andals", "Dorne (Andal petty kingdoms)")
add(R, "any", "The sun is the village's oldest enemy; work stops at noon and begins again at dusk.", "-", "Dorne [STYL]")
add(R, "any", "Dornishmen avoid pitched battle; when armies came, the village went into the hills and waited.", "-", "Conquest_of_Dorne (Dornish tactics)")
add(R, "any", "Queen Rhaenys and her dragon Meraxes fell at Hellholt in 10 AC; the village still tells it as a victory.", "10 AC", "Conquest_of_Dorne (Hellholt)", seats="Hellholt")
add(R, "any", "In the First Dornish War Lord Fowler rode out of Skyreach and burned Nightsong across the mountains.", "4-13 AC", "Dornish_Marches (Nightsong burned)", seats="Skyreach")
add(R, "any", "Dornish raiders rode north through the passes from here for centuries, and stormlanders rode south to repay them.", "-", "Dornish_Marches; Prince's_Pass", seats="Skyreach|Wyl|Kingsgrave|Yronwood|Blackmont")
add(R, "any", "The Martells have ruled here since Mors Martell wed Nymeria, and the village has known no other princes.", "c. 700 BC", "Nymeria's_War", seats="Sunspear|Planky Town")
add(R, "coast", "Nymeria's fleet made landfall at the mouth of the Greenblood, within sight of this shore.", "c. 700 BC", "Ten_thousand_ships (Greenblood mouth)", seats="Planky Town|Sunspear")
add(R, "any", "The Daynes of Starfall keep a sword forged from a fallen star, the village swears, and so do the singers.", "Age of Heroes", "House_Dayne (Dawn)", seats="Starfall")
add(R, "any", "The Yronwoods were kings before the Martells, and the village has not quite forgotten it.", "Rhoynar", "House_Yronwood", seats="Yronwood")
add(R, "any", "The lemon trees of Lemonwood drink from canals older than the Conquest.", "-", "Lemonwood [STYL]", seats="Lemonwood")

# ============================== ESSOS: wspolne ==============================
add(ESS_VAL, "road", "The road past the village is Valyrian, fused stone that has not cracked in a thousand years.", "Freehold", "Valyrian roads")
add(ESS_VAL, "crossroad", "An old Valyrian milestone stands at the crossroads; the distances on it are to cities long gone.", "Freehold", "[STYL]")
add(ESS_VAL, "bridge", "The bridge is Valyrian work, older than the Doom, and no one here knows how it was made.", "Freehold", "Valyrian stonework [STYL]")
add("pentos|norvos|qohor|myr|lys|tyrosh|volantis|braavos|lorath", "bridge", "A toll-keeper of {SEAT} sits at the bridge; even beggars pay to cross.", "-", "[WYM]")
add("pentos|norvos|qohor|myr|lys|tyrosh|volantis|braavos|lorath|ghis", "ford", "Caravans cross {RIVER} at this ford in the dry season; in the floods they wait, and spend.", "-", "[STYL]", needs="RIVER")
add("pentos|norvos|qohor|myr|lys|tyrosh|volantis|braavos|lorath|ghis", "crossroad", "Caravans from three cities meet at this crossroads, and the village lives by their thirst.", "-", "[STYL]")
add("pentos|norvos|qohor|myr|lys|tyrosh|volantis|braavos|lorath", "road", "Sellswords on the march stop here for water and wine, and the village keeps its daughters indoors.", "-", "Free companies [STYL]")
add("pentos|norvos|qohor|myr|lys|tyrosh|volantis|braavos|lorath|ghis", "river", "River galleys and poleboats pass the village landing every day, bound for the sea.", "-", "[STYL]")
add("pentos|norvos|qohor|myr|lys|tyrosh|volantis|lorath|ghis", "river", "The river floods every spring; the fields are richer for it, and the houses stand on mounds.", "-", "[WYM]")
add("pentos|myr|lys|tyrosh|volantis|braavos|lorath", "coast", "Its fishers and smugglers know every cove on this shore; the customs men of {SEAT} know a few.", "-", "[WYM]")
add("pentos|norvos|qohor|myr|lys|tyrosh|volantis|braavos|lorath", "field", "The village lies off the road among its fields; tax-men are its only regular visitors.", "-", "[WYM]")
add(COLONY, "any", "The village paid its dues to Valyria before the Doom and pays {SEAT} now; the sum is about the same.", "Freehold", "Free_Cities (colonies)")
add("volantis|lys|myr|tyrosh|qohor|norvos|pentos", "any", "In the Century of Blood after the Doom, wars and khalasars swept these lands, and the village was burned and built again.", "c. 100-1 BC", "Century_of_Blood")
add("myr|lys|tyrosh", "any", "In 96 AC Myr, Lys and Tyrosh joined as the Kingdom of the Three Daughters to clear the Stepstones of pirates.", "96 AC", "Kingdom_of_the_Three_Daughters")
add("myr|lys|tyrosh", "any", "The Golden Company has fought over these fields in many wars since its founding in 212 AC.", "212 AC", "Golden_Company; Disputed_Lands")
add("myr|lys|tyrosh", "any", "Sellswords of the free companies winter here between wars; the village feeds them and counts the spoons.", "-", "Disputed_Lands (free companies)")
add("myr|lys|tyrosh", "any", "Slaves work these fields, three for every free man, as everywhere in the Quarrelsome Daughters.", "-", "Lys; Myr; Tyrosh (3:1)")
add("braavos|pentos|norvos|lorath", "any", "Andals once tilled these lands, before they took ship for Westeros; their old stones still stand.", "Andals", "Hills_of_Andalos")

# ============================== BRAAVOS ==============================
R = "braavos"
add(R, "any", "Braavos was founded by slaves who fled their Valyrian masters and hid in the fog of the lagoon.", "Freehold", "Uncloaking_of_Uthero (founding)")
add(R, "any", "Its families still keep the masks and the ten days of the Uncloaking of Uthero every year.", "-", "Uncloaking_of_Uthero (festival)")
add(R, "any", "No man may be a slave in the lands of Braavos, and the village is proud that free hands work its fields.", "-", "Braavos (slavery forbidden)")
add(R, "any", "Braavos and Pentos fought six wars in two hundred years, and the village sent oarsmen to every one.", "c. 0-209 AC", "Pentos (six wars with Braavos)")
add(R, "any", "Moonsingers led the founders to the lagoon, and the village shrine still sings their hymns.", "Freehold", "Temple_of_the_Moonsingers")
add(R, "any", "Its young men go to the city to learn the water dance, and come back with scars and stories.", "-", "Braavos (bravos, water dancers)")
add(R, "any", "Pine from the hills above the village went to build the war galleys of the Arsenal.", "-", "Braavos (Arsenal, pine hills)")
add(R, "any", "The village has never seen a dragon; the fog that hid Braavos hid it too.", "Freehold", "Braavos (Secret City)")
add(R, "coast", "Its fishers row their catch to the fishmongers of Braavos before dawn.", "-", "Braavos [STYL]")
add(R, "any", "The Iron Bank will have its due, the villagers say, and so will the Sealord's tax-farmers.", "-", "Iron_Bank")
add(R, "any", "On a still night the shepherds swear they hear the Titan of Braavos roar from the sea.", "-", "Titan_of_Braavos [STYL]")
add(R, "any", "Silver from the hills behind the village paid for many Braavosi ships.", "-", "[STYL]")

# ============================== PENTOS ==============================
R = "pentos"
add(R, "any", "Braavos forbade Pentos its slaves, but the village's bond servants wear collars all the same.", "c. 200 AC", "Pentos (free bond servants)")
add(R, "any", "When khalasars come west, the magisters pay them off, and the village pays the magisters.", "-", "Pentos (tribute to khals)")
add(R, "any", "In the war that ended in 209 AC, Pentos sacrificed four princes in one year, and the village lost its sons.", "209 AC", "Prince_of_Pentos")
add(R, "any", "Braavosi raiders burned the village in one of the six wars, and it was built again in Pentoshi stone.", "c. 0-209 AC", "Pentos (wars with Braavos) [STYL]")
add(R, "any", "The magisters' estates swallowed the village fields long ago; most here work another man's land.", "-", "Pentos (magisters' estates)")
add(R, "any", "Khal Drogo's khalasar camped outside the city in 298 AC, and the village hid its horses.", "298 AC", "Khal Drogo (AGOT)", seats="Pentos")
add(R, "any", "The ruins of Ghoyan Drohe on the Little Rhoyne, burned by Valyrian dragons, lie a day's walk away.", "c. 700 BC", "Rhoynish_Wars (Ghoyan Drohe)", seats="Ghoyan Drohe")
add(R, "any", "Its cheeses and wine go to Pentos by cart, and the magisters' tax-men count every wheel.", "-", "[WYM]")
add(R, "any", "Pentos may keep no army by Braavosi treaty, so the village keeps its own spears.", "c. 200 AC", "Pentos (military restrictions)")

# ============================== NORVOS ==============================
R = "norvos"
add(R, "any", "Norvos was founded by bearded priests who left Valyria to keep their faith, and the village keeps it too.", "Freehold", "Bearded_priests (Norvos founding)")
add(R, "any", "The three bells of Norvos rule the day; the village rises and rests by them, as the priests command.", "-", "Norvos (three bells)")
add(R, "any", "Its terraced fields climb the hills, as they have since the city was young.", "-", "Norvos (AGOT 3)")
add(R, "any", "Dothraki riders have come into the hills of Norvos before, and the village walls remember them.", "-", "Norvos (walled villages) [STYL]")
add(R, "river", "The Noyne carries the village's wine down to the Rhoyne and on to Volantis.", "-", "Noyne", needs="RIVER=the Noyne")
add(R, "any", "The bearded priests choose the magisters, and the magisters choose the tax-men; the village pays them all.", "-", "Bearded_priests (council)")
add(R, "any", "Its sons go to the city to learn the long axe; one of them, they say, now guards a prince of Dorne.", "-", "Areo_Hotah")
add(R, "any", "The ruins of Ny Sar, Nymeria's city, destroyed by the dragonlords, lie close by.", "c. 700 BC", "Rhoynish_Wars (Ny Sar)", seats="Ny Sar")

# ============================== QOHOR ==============================
R = "qohor"
add(R, "any", "Four centuries ago three thousand Unsullied held off Khal Temmo's riders; without them there would be no village.", "c. 100 BC", "Three_Thousand_of_Qohor")
add(R, "any", "Qohorik smiths can rework Valyrian steel, and the village sells them charcoal and iron.", "-", "Qohor (smiths)")
add(R, "any", "The forest of Qohor begins at the village fields, and its woodsmen fell timber for the city.", "-", "Qohor (forest)")
add(R, "any", "Every day a beast is given to the Black Goat; in hard years the village gives its best bullock.", "-", "Black_Goat [STYL]")
add(R, "any", "Khalasars still ride to the edge of the forest; the village watches the eastern grass every summer.", "-", "Qohor (Dothraki) [STYL]")
add(R, "river", "Rafts of timber float down {RIVER} from the village to the Rhoyne.", "-", "[STYL]", needs="RIVER")
add(R, "any", "The ruins of Ar Noy, a Rhoynish city burned by Valyria, lie upriver of the village.", "c. 700 BC", "Rhoynish_Wars (Ar Noy)", seats="Ar Noy")
add(R, "any", "When Qohor is threatened, its nobles give their own children to the Black Goat; the village gives its prayers.", "-", "Black_Goat (crisis sacrifice)")

# ============================== LORATH ==============================
R = "lorath"
add(R, "any", "Mazemakers raised the stone mazes here long before men knew their name, and left no word of why.", "Dawn Age", "Mazemakers")
add(R, "any", "Followers of the Blind God settled Lorath from Valyria; their cult is long dead, but their maze-towns remain.", "Freehold", "Boash")
add(R, "any", "Lorath is the poorest of the Free Cities; the village is poorer still, and does not complain.", "-", "Lorath (TWOIAF)")
add(R, "any", "Freed and runaway slaves have settled here for centuries, and no one asks a newcomer whose he was.", "-", "Lorath (refuge)")
add(R, "coast", "The Shivering Sea is cold and full of fish, and the village has never gone hungry in summer.", "-", "[STYL]")
add(R, "coast", "Ibbenese whalers put in here for water and trouble, and the village sells them both.", "-", "Ibben (whalers) [WYM]")
add(R, "any", "Refugees from fallen Sarnor passed through in the Century of Blood, the old songs say.", "c. 100 BC", "Century_of_Blood [STYL]")
add(R, "any", "The followers of Boash held all life equal, lord and beggar, man and beast; the village has not forgotten.", "Freehold", "Boash")
add(R, "any", "The Axe stretches north of the village, and once it was the land of the Andals.", "Andals", "Hills_of_Andalos (Axe)", seats="Axe Castle")

# ============================== MYR ==============================
R = "myr"
add(R, "any", "Myr has fought Lys and Tyrosh over the Disputed Lands for centuries, and the village has changed hands often.", "-", "Disputed_Lands")
add(R, "any", "In the Daughters' War the Myrmen lost the most, and the village paid for it in sons and grain.", "131-134 AC", "Daughters'_War")
add(R, "any", "Myrish lace and lenses are made in the city; the flax and the thread come from villages like this.", "-", "Myr (lace, lenses)")
add(R, "any", "The red temple of Myr takes boys from villages like this; Thoros of Myr was one of them.", "-", "Thoros_of_Myr")
add(R, "any", "The Sorrows and drowned Chroyane lie near; men who breathe the mist there, they say, turn to stone.", "c. 700 BC", "Chroyane; Sorrows", seats="Chroyane")
add(R, "any", "Prince Garin called down his curse on the Valyrians at Chroyane, and the village still makes a sign against it.", "c. 700 BC", "Rhoynish_Wars (Garin)", seats="Chroyane")

# ============================== TYROSH ==============================
R = "tyrosh"
add(R, "coast", "Tyrosh grew rich on a sea snail whose dye the dragonlords loved, and the snails are gathered on these shores.", "Freehold", "Tyrosh (sea snail dye)")
add(R, "any", "Tyrosh was a Valyrian military outpost before it was a city, and the village still musters spears for the Archon.", "Freehold", "Tyrosh [STYL]")
add(R, "any", "Its pear orchards press the brandy Tyrosh sells across the world.", "-", "Tyroshi_pear_brandy")
add(R, "any", "Tyroshi dye their beards in bright colours, and in the village even old men wear green or blue.", "-", "Tyroshi (dyed beards)")
add(R, "any", "Tyrosh has fought Myr and Lys over the Disputed Lands, and the village sits where their borders shift.", "-", "Disputed_Lands")
add(R, "any", "The Archon's tax-men come twice a year, and the village hides its brandy once.", "-", "[WYM]")
add(R, "coast", "Pirates of the Stepstones have raided this shore since the Three Daughters failed to keep them down.", "c. 130 AC", "Daughters'_War (Stepstones)")

# ============================== LYS ==============================
R = "lys"
add(R, "any", "Lys was a pleasure isle of the dragonlords before the Doom, and its masters still live as if it were.", "Freehold", "Lys (Valyrian pleasure isle)")
add(R, "any", "Here too many have the pale hair and violet eyes of old Valyria.", "Freehold", "Lyseni (Valyrian looks)")
add(R, "any", "In the Century of Blood the tigers of Volantis ruled Lys for two generations, and the village paid them.", "c. 100 BC", "Century_of_Blood (Volantis ruled Lys and Myr)")
add(R, "any", "The tears of Lys are a poison; the village grows nothing but grapes and olives, everyone insists.", "-", "Tears of Lys")
add(R, "coast", "Lyseni galleys put in at the village for water on their way to the Summer Sea.", "-", "[STYL]")
add(R, "any", "Lys fought Myr and Tyrosh over the Disputed Lands, and the village sent spears and grain.", "-", "Disputed_Lands")
add(R, "coast", "Pleasure barges from Lys drift past the village coast on summer nights.", "-", "Lys (pleasure houses) [STYL]")

# ============================== VOLANTIS ==============================
R = "volantis"
add(R, "any", "Volantis was the First Daughter of Valyria, and the village has served the Old Blood since before the Doom.", "Freehold", "Century_of_Blood (First Daughter)")
add(R, "any", "After the Doom the tigers of Volantis went to war for Valyria's empire, and many of the village's men never came home.", "c. 100 BC", "Tigers_(Volantis)")
add(R, "any", "For centuries now the elephants have ruled Volantis, and the village sells to merchants, not to armies.", "-", "Elephants_(Volantis)")
add(R, "any", "Five slaves to every free man work these fields, and their faces bear the tattoos of their trade.", "-", "Volantis (5:1, tattoos)")
add(R, "any", "Volantis lost a whole fleet in the Smoking Sea trying to reclaim Valyria; the widows' songs are still sung here.", "c. 100 BC", "Century_of_Blood (fleet lost)")
add(R, "any", "The Black Walls of Volantis keep the Old Blood in and everyone else out; no one from the village has been inside.", "-", "Black_Walls")
add(R, "river", "The Rhoyne carries the village's grain to Volantis, and river galleys pass every day.", "-", "Rhoyne", needs="RIVER=the Rhoyne")
add(R, "any", "Prince Garin of Chroyane won his first battles near here, a thousand years ago, before the dragons crushed him.", "c. 700 BC", "Rhoynish_Wars (Selhorys, Valysar, Volon Therys)", seats="Selhorys|Valysar|Volon Therys")
add(R, "any", "Rhoynish Sar Mell stood here before the dragonlords burned it; its stones are in every village wall.", "c. 700 BC", "Rhoynish_Wars (Sar Mell)", seats="Sar Mell")

# ============================== VALYRIA ==============================
R = "valyria"
add(R, "any", "The Doom fell on Valyria four hundred years ago, and ash still turns up under the plough.", "c. 100 BC", "Doom_of_Valyria")
add(R, "any", "The old Valyrian roads still run straight through the village, though no dragonlord has used them in centuries.", "Freehold", "Valyrian roads")
add(R, "any", "Sailors say the Smoking Sea is cursed, and no one here will sail toward the old Freehold.", "-", "Smoking Sea")
add(R, "coast", "Dragonglass still washes up on the shore after storms, and the village sells it to the traders.", "c. 100 BC", "Doom_of_Valyria (dragonglass rain)")
add(R, "any", "Dragonlords once ruled from here; now Volantenes, Ghiscari and outlaws fight over what is left.", "-", "Valyria [STYL]")
add(R, "any", "The Fourteen Flames still smoke on clear days, and the village prays they stay asleep.", "-", "Fourteen_Flames")
add(R, "any", "Its people say they descend from the dragonlords' slaves, the only ones who were far enough away.", "c. 100 BC", "[WYM]")
add(R, "any", "Treasure-seekers come through every year bound for the ruins; few come back.", "-", "Valyria (TWOIAF) [STYL]")
add(R, "road", "The demon road runs near here; travellers who take it rarely come back the same.", "-", "Demon_road")
add(R, "coast", "Fishers here will not take their boats past the headland, where the water steams.", "-", "Smoking Sea [STYL]")
add(R, "field", "The soil here is black and rich from the old ash, and strange things grow in it.", "-", "[WYM]")
add(R, "any", "The village is built of Valyrian fused stone, cut from the ruins nearby.", "-", "[STYL]")
add(R, "any", "Before the Doom the Freehold ruled half the world; the village now rules a field and a well.", "Freehold", "[WYM]")

# ============================== GHIS / ZATOKA NIEWOLNICZA ==============================
R = "ghis"
add(R, "any", "Old Ghis fell to the dragonlords five thousand years ago, and the Valyrians sowed its fields with salt.", "c. 4700 BC", "Ghiscari_wars")
add(R, "any", "Slaves work the estates here for masters in {SEAT}; few free men own any land.", "-", "Slaver's_Bay")
add(R, "any", "Its masters trace their blood to Old Ghis, and wear tokars to prove it.", "-", "Ghiscari (tokar)")
add(R, "any", "The harpy of Ghis is carved over the village gate, as over every slaver city.", "-", "Ghiscari (harpy) [STYL]")
add(R, "any", "Dothraki sell their captives in the markets of Slaver's Bay, and the road past the village carries them.", "-", "Slaver's_Bay (Dothraki captives)")
add(R, "river", "The brown Skahazadhan feeds the village fields before it reaches Meereen.", "-", "Skahazadhan", needs="RIVER=the Skahazadhan")
add(R, "any", "The slaver cities were Ghiscari colonies long before Valyria took them, and the village fields have known masters ever since.", "Freehold", "Slaver's_Bay [STYL]")
add(R, "any", "Overseers with whips walk the village fields; the slaves sing to keep the rhythm.", "-", "Slaver's_Bay [STYL]")
add(R, "any", "The Good Masters of the cities quarrel endlessly, and the village pays whoever wins.", "-", "[WYM]")
add(R, "any", "Astapor trains its Unsullied from boyhood, and the village has sold boys to the Good Masters.", "-", "Unsullied", seats="Astapor")
add(R, "any", "Yunkai's Wise Masters trade in slaves of every kind, and the village grows their grain.", "-", "Yunkai", seats="Yunkai")
add(R, "any", "The pyramids of Meereen can be seen from the hills, and the fighting pits are the village's only holiday.", "-", "Meereen", seats="Meereen")
add(R, "any", "Mantarys lies at the end of the demon road; travellers from the west arrive frightened or not at all.", "-", "Demon_road; Mantarys", seats="Mantarys")
add(R, "any", "The Lamb Men of Lhazar herd sheep here, and every few years the Dothraki come to take slaves.", "-", "Lhazareen", seats="Lhazosh|Hesh")
add(R, "any", "The Lhazareen say all men are one flock; the Dothraki do not agree.", "-", "Great_Shepherd", seats="Lhazosh|Hesh")

# ============================== QARTH ==============================
R = "qarth"
add(R, "any", "Qarth is the greatest city that ever was or will be, the Qartheen say, and the village sells it fruit and flowers.", "-", "Qartheen")
add(R, "any", "The village wells were dug by order of the ancient kings of Qarth, and are still guarded.", "-", "[WYM]")
add(R, "any", "The red waste lies west of here, and whoever crosses it is lucky to see the village.", "-", "Qarth (red waste)")
add(R, "any", "The warlocks of the House of the Undying are feared here more than any lord.", "-", "House_of_the_Undying")
add(R, "coast", "Qarth sits on the Jade Gates between two seas, and ships from Yi Ti pass the village coast.", "-", "Qarth (Jade Gates)")
add(R, "any", "The Pureborn trace their blood to the ancient kings of Qarth, and the village pays their taxes.", "-", "Pureborn")
add(R, "any", "The Thirteen buy the village's harvest before it is grown, and sell it twice over.", "-", "Thirteen [STYL]")
add(R, "any", "Spice caravans from the east rest here before the last day's march to the city.", "-", "Ancient Guild of Spicers [STYL]")
add(R, "any", "The warlocks drink shade of the evening, and their lips are blue; the village children run from them.", "-", "Warlocks (shade of the evening)")
add(R, "coast", "Its fishers keep to the coast; the open sea beyond the Jade Gates belongs to the great ships.", "-", "[STYL]")
add(R, "road", "Every traveller on this road is going to Qarth or coming back poorer.", "-", "[WYM]")
add(R, "any", "The Qartheen weep easily, the traders say, but the village tax-men never do.", "-", "Qartheen (weeping) [STYL]")
add(R, "any", "Water is carried from the city cisterns to the village in dry years, at a price.", "-", "[WYM]")
add(R, "any", "Dothraki never come so far; it is the desert the village fears, not the khals.", "-", "Qarth (red waste) [STYL]")
add(R, "any", "The Tourmaline Brotherhood and the Thirteen quarrel in the city; the village keeps its head down.", "-", "Thirteen; Tourmaline Brotherhood")
add(R, "field", "Its gardens are watered by hand, a bucket at a time, as they have been for a thousand years.", "-", "[WYM]")

# ============================== DOTHRAKOWIE ==============================
R = "dothraki"
add(R, "any", "Khalasars ride past each summer, and the village pays in horses and grain, or burns.", "-", "Dothraki [STYL]")
add(R, "any", "Vaes Dothrak lies far to the east beneath the Mother of Mountains, and the riders go there to honour the horse god.", "-", "Mother_of_Mountains")
add(R, "any", "The village stands on the stones of a town the Dothraki burned in the Century of Blood.", "c. 100 BC", "Century_of_Blood (Sarnor)")
add(R, "any", "Its people are slaves and freedmen who keep horses for a khal; they belong to no khalasar.", "-", "[STYL]")
add(R, "any", "The Dothraki take what they want and call it a gift; the village has learned to give first.", "-", "Dothraki [STYL]")
add(R, "any", "Stolen gods line the godsway of Vaes Dothrak; one of them, the old men say, came from this village.", "-", "Godsway")
add(R, "any", "The dosh khaleen of Vaes Dothrak send word through the grass, and every khal listens.", "-", "Dosh_khaleen")
add(R, "river", "The riders water their horses at {RIVER} below the village, and leave when the grass is gone.", "-", "[STYL]", needs="RIVER")
add(R, "any", "No Dothraki will cross the poison water of the sea, and the village is glad of it.", "-", "Dothraki (poison water)")
add(R, "any", "Twice in living memory two khalasars met here and fought; the bones are still in the grass.", "-", "[WYM]")
add(R, "any", "The stallion who mounts the world will come one day, the riders say; the village hopes he passes by.", "-", "Stallion_who_mounts_the_world")
add(R, "any", "The grass sea runs to the edge of sight in every direction, and the village is a stone in it.", "-", "Dothraki_sea [STYL]")
add(R, "any", "Lhazareen captives built the first houses here, and their children still herd sheep on the hill.", "-", "Lhazareen [STYL]")
add(R, "any", "Traders from the Free Cities come with gifts for the khal, and buy what the village has.", "-", "Dothraki (gifts) [STYL]")
add(R, "field", "Its fields are small and fenced with stone, so the horses of the khalasars do not graze them bare.", "-", "[WYM]")

# ============================== SARNOR ==============================
R = "sarnor"
add(R, "any", "The Tall Men ruled this land until the Dothraki burned their cities in the Century of Blood.", "c. 100 BC", "Kingdom_of_Sarnor")
add(R, "any", "At the Field of Crows the Sarnori host died, and the village was founded by the few who fled.", "c. 100 BC", "Field_of_Crows")
add(R, "any", "Saath is all that is left of Sarnor; the village pays it taxes and honours its hundred gods.", "-", "Saath")
add(R, "any", "Its people still call themselves Tagaez Fen, the Tall Men, though few are tall.", "-", "Tall_Men")
add(R, "any", "Ruins of a Sarnori city stand on the plain, and the village takes its stone from them.", "c. 100 BC", "Kingdom_of_Sarnor (ruins)")
add(R, "any", "Sarnath was put to the torch by Khal Loso; the village keeps one of its carved stones over the door.", "c. 100 BC", "Field_of_Crows (Sarnath)")
add(R, "any", "Once Sarnor ruled all the lands of the Sarne; now the village rules a well and a few fields.", "-", "Kingdom_of_Sarnor")
add(R, "river", "The Sarne still runs past the village as it did when Sarnor had kings.", "-", "Sarne", needs="RIVER=the Sarne")
add(R, "any", "The Dothraki ride past and burn what is left every few years; the village rebuilds lower each time.", "-", "[STYL]")
add(R, "any", "Old men here still remember the names of the Sarnori kings, a hundred names and more.", "-", "[WYM]")
add(R, "any", "The lakes here were once the Silver Sea, the old songs say.", "-", "Kingdom_of_Sarnor (Silver Sea)")
add(R, "any", "Scythed chariots once rode from here against the khals; their wheels lie rusting in the barns.", "c. 100 BC", "Kingdom_of_Sarnor (chariots) [STYL]")
add(R, "any", "Traders from Lorath buy the village furs and sell it salt.", "-", "[WYM]")
add(R, "field", "Its fields are small and walled with ruins; the rest of the plain belongs to the grass.", "-", "[WYM]")
add(R, "any", "No khal has ever kept a promise to the village, and the village has stopped asking for one.", "-", "[WYM]")

# ============================== IBBEN ==============================
R = "ibben"
add(R, "coast", "Ibbenese whalers sail from here into the Shivering Sea, and the village smells of whale oil all year.", "-", "Ibben (whalers)")
add(R, "any", "Ibbenese traders say their islands were old when Valyria was young, and no one here argues.", "-", "Ibben [STYL]")
add(R, "any", "Ibben was ruled by a God-King until the Doom; now the Shadow Council and the Thousand rule.", "c. 100 BC", "God-King_of_Ib")
add(R, "any", "Its people are short, broad and hairy, and proud of all three.", "-", "Ibbenese")
add(R, "any", "Whale-oil lamps hang on iron chains in the village street, as in the Port of Ibben.", "-", "Port_of_Ibben")
add(R, "any", "The Thousand chose the village's lord, and the village paid for the choosing.", "-", "Ibben (Thousand)")
add(R, "coast", "Fat-bellied whalers with tarred hulls are built on the beach below the village.", "-", "Ibben (whalers)")
add(R, "any", "Ibbenese settlers came over the sea to these mainland hills, and the forest has fought them ever since.", "-", "Ibben (mainland colonies) [STYL]")
add(R, "any", "Its men go to sea for a year at a time, and the women run the village.", "-", "[WYM]")
add(R, "any", "Dothraki once rode up to the village palisade, looked at it, and rode away.", "-", "[WYM]")
add(R, "any", "The forests here are full of pigs and wolves; the village eats the first and fears the second.", "-", "[WYM]")
add(R, "any", "Lorathi and Braavosi traders buy the village's oil and amber.", "-", "[STYL]")
add(R, "any", "Winter comes early here; the village lays in salt meat before the first frost.", "-", "[WYM]")
add(R, "field", "Its fields are cleared from the forest, and the stumps are older than the village.", "-", "[WYM]")
add(R, "road", "The only road runs from the village to the harbour, and everything that matters travels on it.", "-", "[WYM]")
add(R, "any", "The Port of Ibben is grey and gloomy, the villagers say, and they visit it as rarely as they can.", "-", "Port_of_Ibben")

# ============================== YI TI ==============================
R = "yiti"
add(R, "any", "The God-on-Earth ruled the Great Empire of the Dawn for ten thousand years, the priests of Yin say.", "Legend", "God-on-Earth")
add(R, "any", "Yi Ti's emperors come and go; the village pays its taxes to whichever one is reigning.", "-", "Yi Ti [STYL]")
add(R, "any", "The Bloodstone Emperor brought the Long Night, the Yi Tish tales say, and the village lights lamps at dusk.", "Legend", "Bloodstone Emperor")
add(R, "any", "The priestly scribes of Yin keep the village in their tax rolls, and have for a thousand years.", "-", "Yi Ti (priestly scribes)")
add(R, "any", "Traders from the Jade Sea buy the village's timber and furs for silk.", "-", "[STYL]")
add(R, "any", "The forest here is old and dark, and the village keeps to its edge.", "-", "[WYM]")
add(R, "any", "Its people burn incense to the Maiden-Made-of-Light every dawn.", "-", "Maiden-Made-of-Light [STYL]")
add(R, "any", "Bandits from the hills raid the village roads, and the emperor's soldiers come too late.", "-", "Yi Ti (bandits) [STYL]")
add(R, "any", "The village is far from any emperor, and likes it that way.", "-", "[WYM]")
add(R, "any", "Traders say the Church of Starry Wisdom began in Yi Ti; the village says nothing about it.", "-", "Church of Starry Wisdom")
add(R, "any", "Its people have seen men from the west only twice, and both times they were lost.", "-", "[WYM]")
add(R, "road", "The emperor's road passes the village, but the emperor never has.", "-", "[WYM]")
add(R, "river", "Timber rafts float down the river from the village to the sea.", "-", "[STYL]")
add(R, "field", "Its fields are terraced up the hillside, a step for every generation.", "-", "[WYM]")
add(R, "any", "The Lion of Night is feared here as much as he is worshipped.", "-", "Lion_of_Night")

# ============================== WYSPY LETNIE ==============================
R = "summer"
add(R, "any", "Swan ships from the village have sailed as far as Oldtown, and come back with tales of snow.", "-", "Swan_ship [STYL]")
add(R, "any", "Summer Islanders sail swan ships across the world, and the village sends its sons as archers.", "-", "Swan_ship")
add(R, "any", "Their laws are written on the talking trees, and the village keeps its own beneath one.", "-", "Summer_Isles (talking trees)")
add(R, "any", "Goldenheart bows are the best in the world, and the village cuts the wood for them.", "-", "Goldenheart")
add(R, "any", "Every islander serves a time in the temples of love, and the village is proud of its priests.", "-", "Summer_Isles (temples of love)")
add(R, "coast", "Swan ships with white sails put in at the village bay on their way to Oldtown and Qarth.", "-", "Swan_ship [STYL]")
add(R, "any", "Silver from the hills here buys the feathers and fruit of the other islands.", "-", "[STYL]")
add(R, "any", "Islanders seldom fight in war; when they must, they meet at a holy place and settle it there.", "-", "Summer_Isles (ritual war) [STYL]")
add(R, "any", "The village wears feathered cloaks on feast days, as its grandmothers did.", "-", "Summer_Isles (feathered cloaks)")
add(R, "any", "Slavers have come to these shores before, and the village keeps its archers ready.", "-", "[STYL]")
add(R, "any", "Its people trade spices and fruit for western cloth and steel.", "-", "[STYL]")
add(R, "coast", "The village bay is blue and deep, and swan ships anchor there in the storm season.", "-", "[WYM]")
add(R, "any", "Red archers of the swan ships learn to shoot here, before they ever see a sail.", "-", "Goldenheart (red archers)")
add(R, "any", "Its elders say the islands were born of the sea, and will go back to it one day.", "-", "[WYM]")
add(R, "field", "Its gardens grow fruit that the west has no names for.", "-", "[WYM]")
add(R, "any", "The village honours the god and goddess of love before every voyage.", "-", "Summer_Isles (gods of love)")

REGIONS = ["north", "gift", "beyond", "skagos", "riverlands", "vale", "westerlands", "reach", "stormlands", "crownlands",
           "dragonstone", "iron", "dorne", "braavos", "pentos", "norvos", "qohor", "lorath", "myr", "tyrosh", "lys", "volantis",
           "valyria", "ghis", "qarth", "dothraki", "sarnor", "ibben", "yiti", "summer"]

def rows():
    out = []; n = Counter()
    for region, place, needs, seats, era, text, src in T:
        first = region.split("|")[0] if "|" not in region else "shared"
        n[first] += 1
        out.append(("%s_%02d" % (first, n[first]), region, place, needs, seats, era, text, src))
    return out

def check(s):
    # CSV RFC 4180 (przecinek, cudzyslow tylko gdy trzeba); w polach bez cudzyslowow, tabulatorow i nowych linii
    assert '"' not in s and "\t" not in s and "\n" not in s, s
    assert all(ord(c) < 128 for c in s), s
    assert "hamlet" not in s.lower(), s

def main():
    import csv
    rr = rows()
    with open(os.path.join(OUT, "historia.csv"), "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f, lineterminator="\n")
        w.writerow(["id", "region", "place", "needs", "seats", "era", "text", "source"])
        for r in rr:
            for x in r: check(x)
            w.writerow(r)
    per = Counter(); gen = Counter()
    for _, region, place, needs, seats, *_ in rr:
        for k in region.split("|"):
            per[k] += 1
            if not seats: gen[k] += 1
    print("szablonow", len(rr))
    for k in REGIONS: print("  %-12s ogolne %2d  + siedzib %2d" % (k, gen[k], per[k] - gen[k]))
    bad = [k for k in REGIONS if not 15 <= gen[k] <= 30]
    print("ogolne poza 15-30:", bad)
    long_ = [(r[0], len(r[6])) for r in rr if len(r[6]) > 125]
    print("dluzsze niz 125 znakow:", long_)

if __name__ == "__main__":
    main()
