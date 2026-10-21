using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m103-sceny (E4): TRESC 23 SCEN PROLOGU 298 (projekt PROJEKT-PROLOG-298 pkt 2, Jeff 21.10 "I zrob wydarzenia wedlug ksiazki"). Teksty EN wedlug A Game of Thrones
    /// wlasnymi slowami (rozpoznanie m103 "teksty": prolog298_sceny.json; bez cytatow z ksiazek i serialu). Kazdy napis w TextObject z wlasnym id (ARM_PRL_NN_T/U/L) -
    /// tlumaczenie mozna dolozyc plikiem jezyka bez zmiany kodu; przycisk jak w ROT ({=ROTasSnYXW5}Continue...). Pojemnosc okna (prefab EventXml z ROT-Content:
    /// Galahad 25, ok. 100 znakow na linie, srodki akapitow 120 px od siebie - razem do 9 linii): akapit gorny do 4 linii, dolny do 3, razem do 7 (linie.py).
    /// Sceny dni 118, 125, 155 bez zdan obiecujacych graczowi turniej i bandy (sceny zmieniajace swiat E5 jeszcze nie ma) - tekst zostaje przy ksiazce.
    /// Obrazek: Modules\Armoury\Images\Prolog\prolog_NN.png|jpg (StoryPictures), bez pliku - sprite ROT z ui_event_category (Fallback).
    /// Plik generowany skryptem z JSON rozpoznania - zmiany tekstu tu i w JSON razem.
    /// </summary>
    internal static class PrologueScenes
    {
        internal const string ContinueText = "{=ROTasSnYXW5}Continue...";

        internal sealed class Scene
        {
            public readonly int Day;          // dzien prologu (0 = wyjazd krola z Krolewskiej Przystani, 186 = smierc krola)
            public readonly string Stem;      // nazwa pliku obrazka bez rozszerzenia (prolog_NN)
            public readonly string Fallback;  // sprite ROT (ui_event_category), gdy pliku nie ma albo jest zly
            private readonly string _t, _u, _l;

            public Scene(int day, string stem, string fallback, string t, string u, string l)
            {
                Day = day; Stem = stem; Fallback = fallback; _t = t; _u = u; _l = l;
            }

            public string Title() { return new TextObject(_t).ToString(); }
            public string Upper() { return new TextObject(_u).ToString(); }
            public string Lower() { return new TextObject(_l).ToString(); }
        }

        internal static string Continue() { return new TextObject(ContinueText).ToString(); }

        internal static readonly Scene[] All =
        {
            new Scene(0, "prolog_01", "kingslanding",
                "{=ARM_PRL_01_T}The King Rides North",
                "{=ARM_PRL_01_U}Lord Jon Arryn, Hand of the King, is dead of a sudden fever, and his widow Lysa has fled to the Eyrie with their young son. Grieving and restless, King Robert leaves King's Landing by the Kingsroad with Queen Cersei, the royal children, the Lannister brothers and a great household, bound for Winterfell.",
                "{=ARM_PRL_01_L}The king needs a new Hand, and there is only one man he trusts: his old friend Eddard Stark. For now the realm is at peace and its great houses stand with the crown. Few suspect how quickly that peace can break."),
            new Scene(5, "prolog_02", "rickard",
                "{=ARM_PRL_02_T}Wolves in the Snow",
                "{=ARM_PRL_02_U}Near Winterfell, Lord Eddard Stark beheads Gared, a deserter of the Night's Watch half mad with terror of what he saw beyond the Wall. Riding home, his sons find a dead direwolf with a stag's antler in her throat and her litter still alive: five pups for the trueborn Stark children, and a white runt found by Jon Snow.",
                "{=ARM_PRL_02_L}The direwolf is the sigil of House Stark, and a stag killed the mother. Some call it an omen. The deserter's tale of things in the dark is dismissed as madness, yet beyond the Wall the rangers of the Watch keep vanishing."),
            new Scene(12, "prolog_03", "messenger-1",
                "{=ARM_PRL_03_T}A Khal's Bride",
                "{=ARM_PRL_03_U}Across the Narrow Sea, outside the Free City of Pentos, Viserys Targaryen gives his sister Daenerys in marriage to Khal Drogo, a Dothraki warlord who rides with tens of thousands. Magister Illyrio gifts the bride three dragon eggs turned to stone by the ages, and the exiled knight Ser Jorah Mormont joins their service.",
                "{=ARM_PRL_03_L}Viserys believes the khal will repay him with an army to win back his father's throne. The Dothraki do not cross the sea lightly, but the realm's spies take note. In King's Landing, Robert Baratheon never forgets a Targaryen."),
            new Scene(35, "prolog_04", "traveling_merchant",
                "{=ARM_PRL_04_T}The King in Winterfell",
                "{=ARM_PRL_04_U}After weeks on the Kingsroad, the royal column reaches Winterfell. Lord Eddard and Lady Catelyn welcome King Robert, his queen and their court with a great feast. In the crypts below the castle, Robert asks his old friend to be his Hand and proposes to wed Prince Joffrey to Sansa Stark.",
                "{=ARM_PRL_04_L}That night a secret letter arrives from Lysa Arryn, claiming the Lannisters murdered her husband. Ned must choose between the duty he owes his king and the safety of his home. Either way, the North will be drawn into the games of the South."),
            new Scene(40, "prolog_05", "harrenhal",
                "{=ARM_PRL_05_T}The Fall of Bran Stark",
                "{=ARM_PRL_05_U}Young Bran Stark, who has climbed the walls of Winterfell since he could walk, falls from the old broken tower while most of the court is away hunting. He lives, but lies broken and senseless, and the maester cannot say if he will ever wake.",
                "{=ARM_PRL_05_L}Lady Catelyn will not leave his bedside. Bran was the surest climber in the castle, and a careless slip seems unlike him. What he saw at the tower window is a secret that some in the queen's company would kill to keep."),
            new Scene(48, "prolog_06", "ned_stark",
                "{=ARM_PRL_06_T}The King's Hand",
                "{=ARM_PRL_06_U}Lord Eddard Stark accepts King Robert's offer and becomes Hand of the King. Sansa is promised to Prince Joffrey, and Arya will go south with her sister. Robb is to rule Winterfell as his father's heir, while Lady Catelyn stays with Bran.",
                "{=ARM_PRL_06_L}Ned takes the office not for glory, but to guard his king and learn the truth behind Jon Arryn's death. The second seat of the realm now belongs to a northerner with no taste for intrigue. In King's Landing, many will be eager to test him."),
            new Scene(55, "prolog_07", "messenger-img",
                "{=ARM_PRL_07_T}Roads South and North",
                "{=ARM_PRL_07_U}The royal column leaves Winterfell for King's Landing, with Lord Eddard, Sansa and Arya in its train. On the same morning Jon Snow rides north with his uncle Benjen to serve in the Night's Watch, and Tyrion Lannister goes with them, eager to see the Wall with his own eyes.",
                "{=ARM_PRL_07_L}Bran has still not woken. The Starks are now split between Winterfell, the Wall and the capital, and they may never be whole again. For weeks to come the Kingsroad will be crowded with the king's men."),
            new Scene(62, "prolog_08", "ramsay",
                "{=ARM_PRL_08_T}A Knife in the Night",
                "{=ARM_PRL_08_U}While a fire draws the guards to Winterfell's library tower, a hired killer slips into Bran's sickroom with a fine dagger. Lady Catelyn fights him with her bare hands until Bran's direwolf tears out the man's throat. The blade is Valyrian steel, far too costly for a common cutthroat.",
                "{=ARM_PRL_08_L}Certain that someone powerful wants her son dead, Catelyn sails south in secret with Ser Rodrik Cassel to warn her husband, leaving Robb to hold Winterfell. Soon after she is gone, Bran wakes, remembering nothing of his fall."),
            new Scene(75, "prolog_09", "event-bg",
                "{=ARM_PRL_09_T}Blood at the Trident",
                "{=ARM_PRL_09_U}Near the Ruby Ford, Prince Joffrey attacks Mycah, a butcher's boy who was sparring with Arya Stark. Arya's wolf Nymeria bites the prince, and Arya hurls his sword into the river. The queen demands blood: Nymeria has fled, so Sansa's gentle wolf Lady must die in her place, and the Hound rides down the butcher's boy.",
                "{=ARM_PRL_09_L}Lord Eddard kills Lady himself rather than leave it to another. The quarrel poisons the bond between Stark and Lannister before the court has even reached the capital. If you ride with the king's column, expect to be asked whose side you are on."),
            new Scene(80, "prolog_10", "wallfall",
                "{=ARM_PRL_10_T}A Ranger Lost",
                "{=ARM_PRL_10_U}Benjen Stark, First Ranger of the Night's Watch, rides out beyond the Wall to search for Ser Waymar Royce and the rangers who vanished with him. He does not return. Weeks pass, and no word of him reaches Castle Black.",
                "{=ARM_PRL_10_L}With their best ranger gone, the black brothers grow uneasy. Wildlings are said to be gathering in numbers no one has seen before, and old tales of the dead walking are told more often by the fires. The Wall holds, for now."),
            new Scene(95, "prolog_11", "kingslanding",
                "{=ARM_PRL_11_T}The Small Council",
                "{=ARM_PRL_11_U}Lord Eddard reaches King's Landing and takes his seat at the head of the small council, beside Varys the master of whisperers, Petyr Baelish the master of coin, Grand Maester Pycelle and Renly Baratheon. Lord Stannis has kept to Dragonstone since Jon Arryn's death.",
                "{=ARM_PRL_11_L}Ned learns that the crown owes some six million gold dragons, much of it to the Lannisters, and that the king wants a costly tourney to honour his new Hand. Every man at the table has his own purposes. In this city, trust is spent as freely as gold."),
            new Scene(100, "prolog_12", "messenger-2",
                "{=ARM_PRL_12_T}The Dragonbone Dagger",
                "{=ARM_PRL_12_U}Lady Catelyn arrives secretly in King's Landing. Petyr Baelish, who loved her in their youth, hides her in one of his brothels and studies the dagger sent against Bran. He swears it was once his own, and that he lost it in a wager to Tyrion Lannister.",
                "{=ARM_PRL_12_L}To the Starks, Littlefinger's word seems proof enough that the Imp hired the killer. Ned urges his wife to return home and vows to find the truth. From now on, every step they take in the capital is watched by Varys and his informers."),
            new Scene(110, "prolog_13", "riverlands",
                "{=ARM_PRL_13_T}The Inn at the Crossroads",
                "{=ARM_PRL_13_U}Riding home with Ser Rodrik, Lady Catelyn meets Tyrion Lannister at the inn at the crossroads, on his way back from the Wall. Before a common room full of travellers and sworn swords of the river lords, she accuses him of plotting her son's murder and calls on them to help her seize him.",
                "{=ARM_PRL_13_L}Tyrion is taken east into the Vale, to her sister Lysa at the Eyrie. When word reaches Casterly Rock, Lord Tywin will not let such an insult pass. Lannister and Tully are now one spark away from open war."),
            new Scene(118, "prolog_14", "freefolk1",
                "{=ARM_PRL_14_T}The Mountain Clans",
                "{=ARM_PRL_14_U}On the high road through the Mountains of the Moon, Lady Catelyn's party is ambushed by wild hill clansmen. Her prisoner Tyrion Lannister fights beside his captors with an axe, and a sellsword named Bronn proves the deadliest blade among them.",
                "{=ARM_PRL_14_L}The clans of the Vale answer to no lord and fall on any traveller they can catch. Several of Lady Catelyn's men die on the road before the party reaches the Bloody Gate, where her uncle Ser Brynden Tully, the Blackfish, rides out to meet them."),
            new Scene(125, "prolog_15", "kingslanding",
                "{=ARM_PRL_15_T}The Tourney of the Hand",
                "{=ARM_PRL_15_U}King's Landing honours its new Hand with a great tourney. In the lists, Ser Gregor Clegane, the Mountain, kills a young knight of the Vale with a lance through the throat. Ser Loras Tyrell, the Knight of Flowers, unhorses him, and when the Mountain draws steel on Loras, his own brother Sandor stands in the way.",
                "{=ARM_PRL_15_L}Loras yields the final to the Hound, and Thoros of Myr wins the melee. Ned notes that the dead knight, Ser Hugh of the Vale, was newly knighted and had once been Jon Arryn's squire."),
            new Scene(132, "prolog_16", "fist",
                "{=ARM_PRL_16_T}Trial at the Eyrie",
                "{=ARM_PRL_16_U}Held in a sky cell of the Eyrie, Tyrion demands a trial by combat. Lady Lysa's champion, Ser Vardis Egen, faces the sellsword Bronn, who fights for the Imp. Bronn wears down the heavily armoured knight and sends his body out through the Moon Door.",
                "{=ARM_PRL_16_L}Tyrion walks free down the mountain, where he wins over the hill clans with promises of steel and gold. Shagga and his Stone Crows follow him toward the Riverlands, with Bronn at his side. Lady Lysa has made an enemy of the Rock."),
            new Scene(138, "prolog_17", "ned_stark",
                "{=ARM_PRL_17_T}Swords in the Street",
                "{=ARM_PRL_17_U}Enraged by his brother's capture, Ser Jaime Lannister and his men waylay Lord Eddard in a street of King's Landing. Ned's guards are cut down, and Jory Cassel dies by Jaime's own hand. The Hand is left with a shattered leg when his horse falls on him.",
                "{=ARM_PRL_17_L}Jaime flees the city to join his father's host in the West, and King Robert has no patience for the quarrel. A knight of the Kingsguard has spilled the blood of the Hand's household in the open street. The peace is starting to crack."),
            new Scene(145, "prolog_18", "event-bg",
                "{=ARM_PRL_18_T}A Crown of Gold",
                "{=ARM_PRL_18_U}In Vaes Dothrak, a drunken Viserys Targaryen draws a sword in the sacred city and threatens his pregnant sister. Khal Drogo answers by giving the beggar king the crown he has demanded for so long: a pot of molten gold poured over his head.",
                "{=ARM_PRL_18_L}Daenerys watches her brother die and finds she feels nothing for him. She carries Drogo's son, and the Dothraki whisper that the boy will one day ride to the ends of the world. The Targaryen claim lives on across the Narrow Sea."),
            new Scene(155, "prolog_19", "riverlands",
                "{=ARM_PRL_19_T}The Riverlands Burn",
                "{=ARM_PRL_19_U}Raiders flying no banners burn villages and holdfasts across the Riverlands, slaughtering the smallfolk and driving off their herds. Survivors speak of a giant knight in heavy plate, and every tale points to Ser Gregor Clegane, the Mountain, bannerman of Lord Tywin.",
                "{=ARM_PRL_19_L}Lord Tywin answers the seizure of his son with fire, while pretending to have no hand in it. Refugees crowd the roads to Riverrun, and the river lords send survivors to King's Landing to beg the king's justice."),
            new Scene(160, "prolog_20", "kingslanding",
                "{=ARM_PRL_20_T}The King's Justice",
                "{=ARM_PRL_20_U}With King Robert away hunting, Lord Eddard sits the Iron Throne and hears the smallfolk of the Riverlands. He strips Ser Gregor of his lands and titles and sends Lord Beric Dondarrion with six score men, Thoros of Myr among them, to bring the Mountain to justice.",
                "{=ARM_PRL_20_L}Ned also summons Lord Tywin to court to answer for his bannerman's crimes. It is the act of an honest Hand, and it makes the Lannisters his open enemies. Beric's riders now hunt the raiders along the rivers."),
            new Scene(172, "prolog_21", "messenger-1",
                "{=ARM_PRL_21_T}A Cup of Wine",
                "{=ARM_PRL_21_U}Learning that Daenerys is with child, King Robert orders her death over the protests of his Hand. In the market of Vaes Dothrak, a wine seller offers her his finest vintage, but Ser Jorah Mormont grows suspicious of the man and the poison is exposed before she drinks.",
                "{=ARM_PRL_21_L}The attempt achieves what Viserys never could. Khal Drogo swears to lead his horse lords across the sea and win the Iron Throne for his son. In fearing the dragon, the king may have woken it."),
            new Scene(182, "prolog_22", "stannis-slain",
                "{=ARM_PRL_22_T}The Royal Hunt",
                "{=ARM_PRL_22_U}Hunting in the Kingswood, King Robert, made reckless by the strongwine his squire keeps pouring, faces a great boar on foot. The beast gores him terribly before it dies. He is carried back to the Red Keep on a litter, and the maesters can do little but ease his pain.",
                "{=ARM_PRL_22_L}On his deathbed Robert names Lord Eddard Protector of the Realm until his heir comes of age. Ned now holds the secret that killed Jon Arryn: the queen's children are not the king's. Whoever sits the Iron Throne next will decide the fate of the realm."),
            new Scene(186, "prolog_23", "joffrey",
                "{=ARM_PRL_23_T}The Fall of the Hand",
                "{=ARM_PRL_23_U}King Robert is dead, and Joffrey is proclaimed king. When Lord Eddard brings Robert's will to the throne room to claim the regency, the gold cloaks he paid for turn their spears on his men. Petyr Baelish holds a dagger to his throat, and the Hand is dragged to the black cells.",
                "{=ARM_PRL_23_L}The Stark household in the Red Keep is put to the sword. Sansa is held as a hostage, while Arya escapes as her sword master Syrio Forel holds off the Kingsguard. In the North, Robb Stark calls the banners. The realm stands on the edge of war."),
        };
    }
}
