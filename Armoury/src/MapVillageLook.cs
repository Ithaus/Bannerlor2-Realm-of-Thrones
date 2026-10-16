// E7/W4 (237, PLAN 7.10 b, Jeff L; projekt etapu 7 v2 rozdz. 3 "E7/W4", R10, R17; WM = docs/PROJEKT-WIOSKI-NA-MAPIE-2026-10-07.md 1.4, 5.3, 5.4, 5.6):
// WYGLAD CZESCIOWO PUSTEJ I SPALONEJ WIOSKI NA MAPIE, NAPISY NAD SPALONYMI.
// Stan JEDEN - ksiega wiosek (R10): VillageBook.LookOf(uid) = osady wioski, puste osady (rabunek + glod), spalona (ta sama definicja co StateOf:
// flaga spalenia przy 50% osad, trzyma do 47%). Widok niczego nie liczy sam i niczego nie zapisuje.
//  1. Wioska maleje: poziom obrazka z pliku (1-3) o 1 nizszy od 1/3 pustych osad, o 2 nizszy od 2/3 (powrot dopiero 3 pkt proc. ponizej progu -
//     histereza jak w ksiedze, obrazek nie miga); nigdy ponizej poziomu 1. Poziom = ile domow stoi (VillageWant / HousesFor) - ta sama droga
//     Create co dotad, tylko z innym poziomem.
//  2. Spalona w ksiedze: obrazek bez detali (stog, woz, worki, studnia, szopy - RoleDetail i detale kepy), kazda encja z siatkami przyciemniona
//     GameEntity.SetFactorColor (stala barwa; gra tak przyciemnia plonacy maszt - NavalDLC SailVisual), dym map_icon_village_plunder_fx
//     (ten sam, co gra nad zlupiona wsia), dopoki wies gry okregu jest rabowana albo w czujnosci (E7/R1 wydluza czujnosc). Wioska z ogniem /
//     dymem wsi gry - jak dotad PollRaids.
//  3. Przebudowa obrazka TYLKO przy zmianie stanu (CLAUDE.md 7 - nic co klatke i nic co tick): ksiega oznacza okreg przy kazdej zmianie pustych
//     osad albo flag (LookTouch), widok 4 razy na sekunde porownuje numer zmiany (bez blokady), zabiera liste okregow, oznacza ich wioski jako
//     brudne i dla wiosek Z OBRAZKIEM czyta wyglad z kolejki (najwyzej 32 na cwierc sekundy). Obrazek przebudowany dopiero, gdy poziom albo
//     "spalona" wyszly INNE niz te, z ktorymi stoi: pokazany - od razu (najwyzej 4 na cwierc sekundy), schowany - zdjety i postawiony zwykla
//     droga przy nastepnym pokazaniu. Wioska bez obrazka czyta wyglad przy tworzeniu (TryCreate). Liczniki: zmiany stanu, przebudowy,
//     najwiecej przebudow jednej wioski.
//  4. Napisy "{VILLAGE} has been burned" (VillageTexts.VilBurned) tylko nad SPALONYMI w ksiedze, tylko z bliska (kamera z <= 90, jak
//     proporczyki), najwyzej 30 najblizszych punktu, na ktory patrzy kamera. Wlasna warstwa Gauntlet na ekranie mapy (porzadek 89 - pod
//     tabliczkami gry 90 i menu 100) z wlasnym prefabem Modules\Armoury\GUI\Prefabs\ArmBurnedVillageLabels.xml, dokladana dopiero przy pierwszym
//     napisie. BEZ SetInputRestrictions, bez fokusu, korzen prefabu DoNotAcceptEvents + DoNotPassEventsToChildren - GauntletLayer.HitTest
//     zwraca false, ScreenManager.FirstHitLayer zostaje warstwa sceny (klik i dymek mapy pod napisem dzialaja; krytyka W4-warstwa: nie wzorem
//     CraftPopup). Ukryte w menu, rozmowie na mapie i encyklopedii; w misji ekran mapy nie jest na wierzchu (gra nie wola VisualTick, warstwy
//     ekranu nieczynne). Pozycja: MBWindowManager.WorldToScreenInsideUsableArea co klatke TYLKO dla napisow w kadrze (<= 30) - samo przeliczenie
//     punktu na ekran i dwie liczby do widzetu; wybor napisow 4 razy na sekunde. Warstwa schodzi przy koncu mapy, wylaczeniu klucza i wiosek.
// Potkniecia: wioska - licznik per wioska (3 = ta jedna pominieta, jak dotad); warstwa napisow - 3 potkniecia z rzedu = napisy wylaczone do
// nastepnego wczytania mapy (obrazki bez zmian); nigdy wylacznik calosci. Wylaczniki: BurnedVillageLook, BurnedVillageLabels.
// Poza paczka (projekt pkt 4): trwaly wyglad "looted" wsi GRY przy polowie jej osad (latka na SettlementVisual.RefreshLevelMask).
// Kompilacja zdjec (-p:W4Photo=true, stala W4_PHOTO; projekt T2 "jesli spalonych 0"): pierwsza wioska kazdego okregu (order 0) pokazana jako
// spalona - TYLKO widok (obrazek, dym, napis), ksiega i gra bez zmian; w zwyklym DLL tego kodu nie ma.
using System;
using System.Collections.Generic;
using System.Globalization;
using Armoury.Villages;
using SandBox.View.Map;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>Jeden napis nad spalona wioska: tekst, pozycja na ekranie w pikselach (ScaledPositionXOffset / YOffset widzetu) i widocznosc.</summary>
    public sealed class MapBurnedLabelVM : ViewModel
    {
        private string _text = "";
        private float _x, _y;
        private bool _isShown;
        internal float Wx, Wy, Wz;        // punkt na mapie (srodek postawionego obrazka)
        internal object Owner;            // wioska, ktorej napis teraz nosi (null = wolny)

        [DataSourceProperty]
        public string Text
        {
            get { return _text; }
            set { if (value != _text) { _text = value; OnPropertyChangedWithValue(value, "Text"); } }
        }

        [DataSourceProperty]
        public float X
        {
            get { return _x; }
            set { if (value != _x) { _x = value; OnPropertyChangedWithValue(value, "X"); } }
        }

        [DataSourceProperty]
        public float Y
        {
            get { return _y; }
            set { if (value != _y) { _y = value; OnPropertyChangedWithValue(value, "Y"); } }
        }

        [DataSourceProperty]
        public bool IsShown
        {
            get { return _isShown; }
            set { if (value != _isShown) { _isShown = value; OnPropertyChangedWithValue(value, "IsShown"); } }
        }
    }

    /// <summary>Lista napisow (stala pula - widzety powstaja raz przy zaladowaniu prefabu, potem tylko zmiana tekstu, pozycji i widocznosci).</summary>
    public sealed class MapBurnedLabelsVM : ViewModel
    {
        private MBBindingList<MapBurnedLabelVM> _labels = new MBBindingList<MapBurnedLabelVM>();

        public MapBurnedLabelsVM(int count)
        {
            for (int i = 0; i < count; i++) _labels.Add(new MapBurnedLabelVM());
        }

        [DataSourceProperty]
        public MBBindingList<MapBurnedLabelVM> Labels
        {
            get { return _labels; }
            set { if (value != _labels) { _labels = value; OnPropertyChangedWithValue(value, "Labels"); } }
        }
    }

    public sealed partial class MapVillagesView
    {
        private const float LookShare1 = 1f / 3f;     // od tylu pustych osad obrazek o poziom nizszy (poziom 2 = ok. 2/3 domow, WM 5.3)
        private const float LookShare2 = 2f / 3f;     // od tylu - o dwa poziomy (poziom 1 = ok. 1/3 domow)
        private const float LookBack = 0.03f;         // powrot poziomu dopiero 3 pkt proc. ponizej progu (ksiega: spalona od 50%, trzyma do 47%)
        private const int LookPerTick = 32;           // tyle wiosek z kolejki na cwierc sekundy (odczyt tablic ksiegi)
        private const int LookRebuildPerTick = 4;     // najwyzej tyle przebudow pokazanych obrazkow na cwierc sekundy
        private const uint BurnedTint = 0xFF66605Au;  // mnoznik barwy siatek spalonej wioski (ARGB; bialy = bez zmiany): ok. 0.4, lekko cieply (sadza)
        private const float LabelCameraZ = 90f;       // napisy tylko przy kamerze z <= 90 ("z bliska" jak proporczyki E7/W5)
        private const int LabelMax = 30;              // najwyzej tyle napisow w kadrze
        private const int LabelOrder = 89;            // warstwa pod tabliczkami gry ("MapNameplateLayer" 90) i menu mapy (100)
        private const int LabelStrikesMax = 3;        // potkniecia warstwy z rzedu, po ktorych napisy sa wylaczone do nastepnego wczytania mapy
        private const string LabelMovie = "ArmBurnedVillageLabels";

        private readonly List<Slot> _lookQueue = new List<Slot>();      // wioski z obrazkiem i brudnym stanem (albo czekajace na przebudowe)
        private readonly List<string> _lookIds = new List<string>();    // bufor id okregow z ksiegi
        private bool _lookStarted, _lookKeyOn, _lblKeyOn, _lookBookOn;
        private int _lookSeenVer;
        private int _lookChanges, _lookRebuilt, _lookDropped, _lookNoPic, _lookMerged, _lookTinted, _lookDarkBuilt, _lookBuildsMax, _lookNotes, _stLook;
        private string _lookLastLine = "";
        // napisy
        private GauntletLayer _lblLayer;
        private GauntletMovieIdentifier _lblMovie;
        private MapBurnedLabelsVM _lblVm;
        private MapScreen _lblScreen;
        private MapConversationView _lblConv;
        private Slot[] _lblCand = new Slot[64];
        private float[] _lblDist = new float[64];
        private int _lblCount, _lblMax, _lblStrikes, _lblTickStrikes, _lblLayers, _lblFrames, _stLbl;
        private bool _lblBroken, _lblPrefabChecked, _lblNoPrefab;
        private long _lblFrameTicks;

        /// <summary>O ile poziomow nizej stoi obrazek przy udziale pustych osad e (0-1); prev = poprzedni wynik tej wioski (histereza powrotu).</summary>
        internal static int LookDropFor(double e, int prev)
        {
            if (double.IsNaN(e) || e < 0.0) e = 0.0;
            int d = e >= LookShare2 ? 2 : e >= LookShare1 ? 1 : 0;
            if (d < prev)
            {
                if (prev >= 2 && e >= LookShare2 - LookBack) d = 2;
                else if (e >= LookShare1 - LookBack) d = Math.Max(d, 1);
            }
            return d;
        }

        /// <summary>Detal zdejmowany ze spalonej wioski: element przepisu z rola "detal" albo detal kepy wsi (studnia, szopa); nigdy budynek
        /// glowny, element przy wodzie ani niezbedny.</summary>
        private static bool IsBurnedDetail(Kit k, Piece p)
        {
            if (p.Essential || p.Role == MapVillageData.RoleSpecial || p.Role == MapVillageData.RoleWater) return false;
            if (p.Role == MapVillageData.RoleDetail) return true;
            return p.ThinIndex >= 0 && k.ThinDetail != null && p.ThinIndex < k.ThinDetail.Length && k.ThinDetail[p.ThinIndex];
        }

        // ---------- stan z ksiegi wiosek -> wyglad (4 razy na sekunde; praca tylko przy zmianie stanu) ----------
        private void LookTick()
        {
            try
            {
                Settings st = Settings.Current;
                bool keyLook = st.BurnedVillageLook, keyLbl = st.BurnedVillageLabels, book = VillageBook.Active;
                int ver = VillageBook.LookVersion;
                bool all = false;
                if (!_lookStarted)
                {
                    _lookStarted = true;
                    _lookKeyOn = keyLook; _lblKeyOn = keyLbl; _lookBookOn = book;
                    _lookSeenVer = ver - 1;   // pierwszy tick mapy zabiera liste ksiegi
                    all = true;
                    LookStartLine(keyLook, keyLbl, book);
                }
                else if (keyLook != _lookKeyOn || keyLbl != _lblKeyOn || book != _lookBookOn)
                {
                    if (keyLook != _lookKeyOn)
                        Log.Info("Wioski - wyglad (E7/W4): wyglad malejacej i spalonej wioski " + (keyLook ? "wlaczony" : "wylaczony")
                                 + " w MCM - obrazki w innym wygladzie przebudowane raz.");
                    if (keyLbl != _lblKeyOn) Log.Info("Wioski - wyglad (E7/W4): napisy nad spalonymi " + (keyLbl ? "wlaczone" : "wylaczone") + " w MCM.");
                    if (book != _lookBookOn)
                        Log.Info("Wioski - wyglad (E7/W4): ksiega wiosek " + (book ? "czynna - wyglad z ksiegi." : "nieczynna - obrazki z pliku, bez spalonych."));
                    _lookKeyOn = keyLook; _lblKeyOn = keyLbl; _lookBookOn = book;
                    all = true;
                }
                if (ver != _lookSeenVer)
                {
                    _lookSeenVer = ver;
                    _lookIds.Clear();
                    bool bookAll;
                    VillageBook.TakeLookDirty(_lookIds, out bookAll);
                    if (bookAll) all = true;
                    else if (!all)
                        for (int i = 0; i < _lookIds.Count; i++)
                        {
                            District d;
                            if (!_distById.TryGetValue(_lookIds[i], out d)) continue;
                            for (int j = 0; j < d.ByOrder.Count; j++) MarkLook(d.ByOrder[j]);
                        }
                }
                if (all) for (int i = 0; i < _slots.Count; i++) MarkLook(_slots[i]);
                int done = 0, rebuilt = 0, idx = 0;
                while (idx < _lookQueue.Count && done < LookPerTick)
                {
                    Slot s = _lookQueue[idx];
                    bool wait = false;
                    try { wait = LookStep(s, ref rebuilt); }
                    catch (Exception e)
                    {
                        _stLook++;
                        s.Strikes++;
                        s.LookRebuild = false;
                        if (s.Root == null && s.Shown) { s.Shown = false; _shown.Remove(s); }   // obrazek zdjety w polowie przebudowy: wroci zwykla droga
                        if (s.Strikes >= StrikesPerVillage) Fail(s, "potkniecia");   // ta jedna wioska (nigdy cala warstwa)
                        if (_stLook <= 3 || _stLook % 200 == 0)
                            Log.Error("MapVillagesView.Look " + s.R.Uid + " (potkniecie wioski " + s.Strikes + "/" + StrikesPerVillage + ", razem " + _stLook + ")", e);
                    }
                    if (wait) { idx++; continue; }   // budzet przebudow tego ticku wyczerpany - zostaje w kolejce
                    s.LookQueued = false;
                    int last = _lookQueue.Count - 1;
                    _lookQueue[idx] = _lookQueue[last];
                    _lookQueue.RemoveAt(last);
                    done++;
                }
            }
            catch (Exception e)
            {
                _stLook++;
                if (_stLook <= 3 || _stLook % 200 == 0) Log.Error("MapVillagesView.LookTick (potkniecie " + _stLook + ")", e);
            }
        }

        /// <summary>Wioska do ponownego odczytu wygladu. Z obrazkiem - do kolejki widoku; bez obrazka - przeczyta przy tworzeniu (TryCreate).</summary>
        private void MarkLook(Slot s)
        {
            s.LookDirty = true;
            if (s.Root == null || s.LookQueued) return;
            s.LookQueued = true;
            _lookQueue.Add(s);
        }

        /// <summary>Jedna wioska z kolejki. true = czeka dalej (pokazany obrazek do przebudowy, budzet ticku wyczerpany).</summary>
        private bool LookStep(Slot s, ref int rebuilt)
        {
            if (s.LookDirty && RefreshLook(s) && s.Root != null) s.LookRebuild = true;
            if (!s.LookRebuild) { ApplyFx(s); return false; }   // sam dym (albo nic): jedno przelaczenie, gdy sie zmienil
            if (s.Root == null) { s.LookRebuild = false; _lookDropped++; return false; }
            if (!s.Shown || s.Failed)
            {
                // schowany: nowy obrazek stanie zwykla droga przy nastepnym pokazaniu; wioska pominieta po potknieciach: stary obrazek schodzi i nie wraca
                s.LookRebuild = false;
                if (s.Shown) { s.Shown = false; _shown.Remove(s); }
                DropPicture(s);
                _lookDropped++;
                LookNote(s, s.Failed ? "zdjety (wioska pominieta po potknieciach)" : "schowany - nowy obrazek przy nastepnym pokazaniu");
                return false;
            }
            if (rebuilt >= LookRebuildPerTick) return true;
            rebuilt++;
            s.LookRebuild = false;
            RebuildPicture(s);
            _lookRebuilt++;
            LookNote(s, s.Root != null ? "przebudowany" : "nie stanal - wroci przy kamerze");
            return false;
        }

        /// <summary>Wyglad wioski z ksiegi (odczyt tablic, nie rzuca). true = wyglad obrazka INNY niz poprzednio policzony (zmiana stanu) -
        /// tylko wtedy obrazek jest przebudowywany. Pierwszy odczyt po wczytaniu mapy nie jest zmiana.</summary>
        private bool RefreshLook(Slot s)
        {
            Settings st = Settings.Current;
            bool keyLook = st.BurnedVillageLook, keyLbl = st.BurnedVillageLabels;
            int baseLv = Math.Max(1, Math.Min(3, s.R.Level));
            int drop = 0;
            bool burned = false;
            if (keyLook || keyLbl)
            {
                double n, empty;
                bool b;
                if (VillageBook.LookOf(s.R.Uid, out n, out empty, out b))
                {
                    drop = LookDropFor(empty / n, s.LookDrop);
                    burned = b;
                }
#if W4_PHOTO
                if (s.R.Order == 0) burned = true;   // kompilacja zdjec: pierwsza wioska kazdego okregu pokazana jako spalona (sam widok)
#endif
            }
            int picLevel = keyLook ? Math.Max(1, baseLv - drop) : baseLv;
            bool picBurned = keyLook && burned;
            bool changed = s.LookKnown && (picLevel != s.PicLevel || picBurned != s.PicBurned);
            bool fx = !s.LookKnown || picBurned != s.PicBurned;
            s.LookDrop = drop;
            s.BookBurned = burned;
            s.PicLevel = picLevel;
            s.PicBurned = picBurned;
            s.LookKnown = true;
            s.LookDirty = false;
            if (!burned) s.Label = null;
            if (fx) WantBurnedFx(s);
            if (changed)
            {
                _lookChanges++;
                if (s.Root == null) _lookNoPic++;
                else if (s.LookRebuild) _lookMerged++;   // druga zmiana przed przebudowa - jedna przebudowa na obie
            }
            return changed;
        }

        /// <summary>Dym nad spalona wioska, ktora nie jest wioska z ogniem / dymem wsi gry: tylko gdy wies gry okregu jest rabowana albo w czujnosci.</summary>
        private static void WantBurnedFx(Slot s)
        {
            District d = s.D;
            if (d == null || ReferenceEquals(d.FxSlot, s)) return;   // te prowadzi PollRaids
            s.FxWanted = s.PicBurned && d.Fx != FxNone ? FxSmoke : FxNone;
        }

        /// <summary>Okreg zmienil stan (PollRaids: rabunek / czujnosc / koniec, inna wioska z ogniem): dym nad pozostalymi spalonymi wioskami okregu.</summary>
        private void BurnedFx(District d)
        {
            for (int i = 0; i < d.ByOrder.Count; i++)
            {
                Slot s = d.ByOrder[i];
                if (ReferenceEquals(d.FxSlot, s)) continue;
                int want = s.PicBurned && d.Fx != FxNone ? FxSmoke : FxNone;
                if (s.FxWanted == want) continue;
                s.FxWanted = want;
                ApplyFx(s);   // wlasny try, bez obrazka nic nie robi (dym stanie przy tworzeniu)
            }
        }

        /// <summary>Zdejmuje obrazek jednej wioski (stan pokazania zostaje u wolajacego).</summary>
        private void DropPicture(Slot s)
        {
            GameEntity old = s.Root;
            s.Root = null;
            s.FxShown = FxNone;
            s.NeedRes = false;
            if (old == (GameEntity)null) return;
            if (_created > 0) _created--;
            try { old.Remove(111); }
            catch (Exception e) { _stRemove++; if (_stRemove <= 3) Log.Error("MapVillagesView.Remove " + s.R.Uid, e); }
        }

        /// <summary>Pokazany obrazek w nowym wygladzie: stary zdjety, nowy ta sama droga co zawsze (TryCreate) i od razu pokazany. Nie stanal
        /// (potkniecie policzone w TryCreate) - wioska schodzi z pokazanych i wraca zwykla droga UpdateVisibility.</summary>
        private void RebuildPicture(Slot s)
        {
            DropPicture(s);
            s.LookBuilds++;
            if (s.LookBuilds > _lookBuildsMax) _lookBuildsMax = s.LookBuilds;
            if (TryCreate(s))
            {
                s.Root.SetVisibilityExcludeParents(true);
                s.Root.CheckResources(true, false);
                s.NeedRes = false;
                return;
            }
            s.Shown = false;
            _shown.Remove(s);
        }

        private void LookNote(Slot s, string what)
        {
            _lookNotes++;
            if (_lookNotes > 30 && _lookNotes % 100 != 0) return;
            double n, empty;
            bool b;
            string share = VillageBook.LookOf(s.R.Uid, out n, out empty, out b) ? (100.0 * empty / n).ToString("0", CultureInfo.InvariantCulture) + "%" : "-";
            Log.Info("Wioski - wyglad (E7/W4): obrazek " + s.R.Name + " (okreg " + (s.D != null && s.D.S != null ? s.D.S.Name.ToString() : "?") + ", " + F2(s.Cx) + " / " + F2(s.Cy)
                     + ") " + what + " - poziom " + Math.Max(1, Math.Min(3, s.R.Level)) + " -> " + s.PicLevel + ", spalona " + (s.PicBurned ? "tak" : "nie")
                     + ", pustych osad " + share + (_lookNotes == 30 ? " (dalej co setna)." : "."));
        }

        /// <summary>Wioski i obrazki znikaja (wylaczenie w MCM, koniec mapy): kolejka i napisy od zera; policzony wyglad zostaje (zmiana liczona dalej).</summary>
        private void LookReset()
        {
            foreach (var s in _slots)
            {
                if (s.LookRebuild) { s.LookRebuild = false; _lookDropped++; }   // obrazek i tak schodzi - nowy stanie w nowym wygladzie
                s.LookQueued = false;
                s.LookDirty = true;
            }
            _lookQueue.Clear();
            LabelsOff();
        }

        private void LookStartLine(bool keyLook, bool keyLbl, bool book)
        {
            int lv2 = 0, lv3 = 0;
            foreach (var s in _slots) { if (s.R.Level >= 3) lv3++; else if (s.R.Level == 2) lv2++; }
            Log.Info("Wioski - wyglad (E7/W4): start | wyglad malejacej i spalonej wioski " + (keyLook ? "CZYNNY" : "wylaczony (BurnedVillageLook)")
                     + ", napisy nad spalonymi " + (keyLbl ? "CZYNNE" : "wylaczone (BurnedVillageLabels)") + ", ksiega wiosek " + (book ? "czynna" : "NIECZYNNA - obrazki z pliku")
                     + " | poziom obrazka nizszy od " + (100f * LookShare1).ToString("0", CultureInfo.InvariantCulture) + "% i "
                     + (100f * LookShare2).ToString("0", CultureInfo.InvariantCulture) + "% pustych osad (powrot " + (100f * LookBack).ToString("0", CultureInfo.InvariantCulture)
                     + " pkt nizej), nigdy ponizej 1; wiosek poziomu 2 w pliku " + lv2 + ", poziomu 3 " + lv3 + " z " + _slots.Count + " (tylko one moga zmalec)"
                     + " | spalona = stan ksiegi (od 50% osad, trzyma do 47%): bez detali, barwa siatek x " + BurnedTint.ToString("X8") + ", dym w rabunku i czujnosci wsi gry"
                     + " | przebudowa obrazka tylko przy zmianie stanu: kolejka " + LookPerTick + " wiosek i najwyzej " + LookRebuildPerTick + " przebudowy na cwierc sekundy"
                     + " | napisy: kamera z <= " + LabelCameraZ.ToString("0", CultureInfo.InvariantCulture) + ", najwyzej " + LabelMax + ", prefab " + LabelMovie
                     + ", warstwa " + LabelOrder + " bez wejscia i fokusu, dokladana przy pierwszym napisie"
#if W4_PHOTO
                     + " | KOMPILACJA ZDJEC (W4_PHOTO): pierwsza wioska kazdego okregu pokazana jako spalona - sam widok, nie do gry"
#endif
                     + " | nic do zapisu gry.");
        }

        /// <summary>Linia stanu (co 2 minuty przy zmianie, z podsumowaniem wiosek, i na koniec mapy; liczniki od wczytania mapy).</summary>
        private string LookLine()
        {
            int burned = 0, dark = 0, smaller = 0, smoke = 0, wait = 0, known = 0;
            foreach (var s in _slots)
            {
                if (!s.LookKnown) continue;
                known++;
                if (s.BookBurned) burned++;
                if (s.PicBurned && s.Root != null) dark++;
                if (s.PicLevel < Math.Max(1, Math.Min(3, s.R.Level))) smaller++;
                if (s.PicBurned && s.FxWanted == FxSmoke && s.D != null && !ReferenceEquals(s.D.FxSlot, s)) smoke++;
                if (s.LookRebuild) wait++;
            }
            double ms = _lblFrames > 0 ? _lblFrameTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / _lblFrames : 0;
            return "Wioski - wyglad (E7/W4): przebudowy obrazka " + (_lookRebuilt + _lookDropped) + " (zmiany stanu " + _lookChanges + "; pokazane " + _lookRebuilt
                   + ", schowane do nastepnego pokazania " + _lookDropped + ", bez obrazka " + _lookNoPic + ", polaczone " + _lookMerged + ", czeka " + wait
                   + "; najwiecej przebudow jednej wioski " + _lookBuildsMax + ") | teraz: spalonych w ksiedze " + burned + " (ciemny obrazek stoi " + dark
                   + ", dym nad spalonymi " + smoke + "), mniejszych " + smaller + "; ciemnych obrazkow od wczytania " + _lookDarkBuilt + " (encji przyciemnionych "
                   + _lookTinted + ")" + BookBurnedText(known) + " | napisy w kadrze teraz " + _lblCount + ", maks. " + _lblMax + ", warstwa " + (_lblLayer != null ? "stoi" : "brak")
                   + " (postawiona " + _lblLayers + " razy), przeliczenie pozycji sr. " + ms.ToString("0.0000", CultureInfo.InvariantCulture) + " ms/klatke (klatek z napisami "
                   + _lblFrames + ") | potkniecia: wyglad " + _stLook + ", napisy " + _stLbl + (_lblBroken ? " - NAPISY WYLACZONE do nastepnego wczytania mapy" : "")
                   + (_lookKeyOn ? "" : " | wyglad WYLACZONY w MCM") + (_lblKeyOn ? "" : " | napisy WYLACZONE w MCM") + ".";
        }

        /// <summary>Jeff 16.10 (D4): spalone w calej ksiedze obok "spalonych w ksiedze" widoku (te liczy tylko wioski z odczytanym wygladem - postawione przy kamerze
        /// od wczytania mapy); bez tego linia wygladu pokazywala 0 przy 3 spalonych w ksiedze (log Jeffa 16.10 10:26). Nazwy bez "spalonych w ksiedze" i "mniejszych"
        /// (wzorce tools/sprawdz_logi.py etap7_w4 biora pierwsze wystapienie).</summary>
        private string BookBurnedText(int known)
        {
            int side, main;
            if (!VillageBook.BurnedCounts(out side, out main)) return " | cala ksiega: nieczynna";
            return " | cala ksiega: wioski poboczne spalone " + side + ", wsie gry spalone " + main + " (wsie gry bez obrazka Armoury); wyglad odczytany u " + known + " z "
                   + _slots.Count + " wiosek (tylko postawione przy kamerze od wczytania mapy - dalsze spalone widok liczy, gdy kamera do nich dojdzie)";
        }

        // ---------- napisy nad spalonymi ----------
        /// <summary>Wybor napisow (4 razy na sekunde, po UpdateVisibility): spalone w ksiedze sposrod pokazanych obrazkow, najblizsze punktu, na ktory
        /// patrzy kamera. Widzety puli dostaja tekst i punkt mapy; pozycje na ekranie liczy LabelFrame.</summary>
        private void LabelTick(MapScreen screen)
        {
            try
            {
                if (_lblBroken) { _lblCount = 0; return; }   // audyt 13.10: po wylaczeniu (3 potkniecia z rzedu albo brak prefabu) warstwy juz nie ma - bez wyboru napisow, zeby staly wyjatek nie lecial dalej 4/s
                if (!Settings.Current.BurnedVillageLabels)
                {
                    if (_lblLayer != null) RemoveLabelLayer(true);
                    _lblCount = 0;
                    return;
                }
                int n = 0;
                Camera camera = screen != null && screen.MapCameraView != null ? screen.MapCameraView.Camera : null;
                if (camera != null)
                {
                    Vec3 cam = camera.Position;
                    if (cam.z <= LabelCameraZ) n = PickLabels(screen, camera, cam);
                }
                if (n > 0 && _lblLayer == null && (_lblBroken || !EnsureLabelLayer(screen))) n = 0;
                MapBurnedLabelsVM vm = _lblVm;
                if (_lblLayer == null || vm == null) { _lblCount = 0; return; }
                MBBindingList<MapBurnedLabelVM> list = vm.Labels;
                if (n > list.Count) n = list.Count;
                for (int i = 0; i < n; i++)
                {
                    Slot s = _lblCand[i];
                    MapBurnedLabelVM it = list[i];
                    if (s.Label == null) s.Label = VillageTexts.Make(VillageTexts.VilBurned, "VILLAGE", s.R.Name).ToString();
                    if (!ReferenceEquals(it.Owner, s)) { it.Owner = s; it.Text = s.Label; }
                    it.Wx = s.Cx; it.Wy = s.Cy; it.Wz = s.Z;
                }
                for (int i = n; i < list.Count; i++)
                {
                    MapBurnedLabelVM it = list[i];
                    if (it.Owner == null && !it.IsShown) continue;
                    it.IsShown = false;
                    it.Owner = null;
                }
                _lblCount = n;
                _lblTickStrikes = 0;   // audyt 13.10: wlasny licznik "z rzedu" wyboru napisow (4/s) - zeruje go tylko udany wybor (patrz LabelStumble)
                if (n > _lblMax) _lblMax = n;
                if (n > 0) LabelFrame(screen);   // nowy wlasciciel widzetu od razu na swoim miejscu (bez klatki ze starym)
            }
            catch (Exception e) { _lblTickStrikes++; LabelStumble("Tick", e); }
        }

        private int PickLabels(MapScreen screen, Camera camera, Vec3 cam)
        {
            int m = 0;
            for (int i = 0; i < _shown.Count; i++)
            {
                Slot s = _shown[i];
                if (!s.BookBurned || s.Root == null || !s.Placed || s.Failed) continue;
                if (m == _lblCand.Length) { Array.Resize(ref _lblCand, m * 2); Array.Resize(ref _lblDist, m * 2); }
                _lblCand[m] = s;
                _lblDist[m] = 0f;
                m++;
            }
            if (m <= LabelMax) return m;
            // wiecej niz 30 spalonych w kadrze: najblizsze punktu, na ktory patrzy kamera (jak wybor proporczykow)
            float lx = cam.x, ly = cam.y;
            try
            {
                Vec3 dir = camera.Direction;
                float dist = screen.MapCameraView.CameraDistance;
                if (dist > 0f && dist < 2000f && dir.z < 0f) { lx = cam.x + dir.x * dist; ly = cam.y + dir.y * dist; }
            }
            catch { lx = cam.x; ly = cam.y; }
            for (int i = 0; i < m; i++)
            {
                float dx = _lblCand[i].Cx - lx, dy = _lblCand[i].Cy - ly;
                _lblDist[i] = dx * dx + dy * dy;
            }
            Array.Sort(_lblDist, _lblCand, 0, m);
            return LabelMax;
        }

        /// <summary>Pozycje napisow w kadrze (co klatke, najwyzej 30): punkt mapy -> ekran i dwie liczby do widzetu. Zadnych encji mapy.
        /// W menu, rozmowie na mapie i encyklopedii napisy schowane.</summary>
        private void LabelFrame(MapScreen screen)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                MapBurnedLabelsVM vm = _lblVm;
                if (vm == null) { _lblCount = 0; return; }
                bool hide = screen == null || screen.IsInMenu || (_lblConv != null && _lblConv.IsConversationActive)
                            || (screen.EncyclopediaScreenManager != null && screen.EncyclopediaScreenManager.IsEncyclopediaOpen);
                Camera camera = !hide && screen.MapCameraView != null ? screen.MapCameraView.Camera : null;
                if (camera == null) hide = true;
                float lift = 0f, maxX = 0f, maxY = 0f;
                if (!hide)
                {
                    lift = 0.5f + Clamp(camera.Position.z / 30f, 0f, 1f) * 1.5f;   // wyzej przy oddalonej kamerze (tabliczki wsi gry: 0.5 + do 2.5)
                    maxX = Screen.RealScreenResolutionWidth + 200f;
                    maxY = Screen.RealScreenResolutionHeight + 50f;
                }
                MBBindingList<MapBurnedLabelVM> list = vm.Labels;
                for (int i = 0; i < _lblCount && i < list.Count; i++)
                {
                    MapBurnedLabelVM it = list[i];
                    if (hide) { it.IsShown = false; continue; }
                    float x = 0f, y = 0f, w = 0f;
                    MBWindowManager.WorldToScreenInsideUsableArea(camera, new Vec3(it.Wx, it.Wy, it.Wz + lift), ref x, ref y, ref w);
                    bool on = w > 0f && x > -200f && y > -50f && x < maxX && y < maxY;
                    if (on) { it.X = x; it.Y = y; }
                    it.IsShown = on;
                }
                _lblStrikes = 0;
            }
            catch (Exception e) { LabelStumble("Frame", e); }
            _lblFrameTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            _lblFrames++;
        }

        /// <summary>Warstwa napisow na ekranie mapy (raz, przy pierwszym napisie): bez ograniczen wejscia i bez fokusu. Brak prefabu w module -
        /// napisow nie ma do nastepnego wczytania mapy (fakt o plikach, nie potkniecie).</summary>
        private bool EnsureLabelLayer(MapScreen screen)
        {
            if (screen == null) return false;
            try
            {
                if (!_lblPrefabChecked)
                {
                    _lblPrefabChecked = true;
                    string prefab = MapVillageTooltip.ModulePath("GUI", "Prefabs", LabelMovie + ".xml");
                    _lblNoPrefab = prefab == null || !System.IO.File.Exists(prefab);
                    if (_lblNoPrefab)
                        Log.Info("Wioski - wyglad (E7/W4): BRAK prefabu GUI\\Prefabs\\" + LabelMovie + ".xml w module Armoury - napisow nad spalonymi nie bedzie"
                                 + " do nastepnego wczytania mapy (obrazki bez zmian).");
                }
                if (_lblNoPrefab) { _lblBroken = true; return false; }
                var vm = new MapBurnedLabelsVM(LabelMax);
                var layer = new GauntletLayer("ArmBurnedVillageLabels", LabelOrder, false);
                screen.AddLayer(layer);
                GauntletMovieIdentifier movie;
                try { movie = layer.LoadMovie(LabelMovie, vm); }
                catch
                {
                    try { screen.RemoveLayer(layer); } catch { }
                    throw;
                }
                _lblVm = vm;
                _lblLayer = layer;
                _lblMovie = movie;
                _lblScreen = screen;
                _lblConv = screen.GetMapView<MapConversationView>();
                _lblLayers++;
                return true;
            }
            catch (Exception e) { LabelStumble("EnsureLayer", e); return false; }
        }

        /// <summary>Napisy znikaja (bez ruszania warstwy): licznik kadru 0, widzety schowane.</summary>
        private void LabelsOff()
        {
            _lblCount = 0;
            MapBurnedLabelsVM vm = _lblVm;
            if (vm == null) return;
            try
            {
                MBBindingList<MapBurnedLabelVM> list = vm.Labels;
                for (int i = 0; i < list.Count; i++) { list[i].IsShown = false; list[i].Owner = null; }
            }
            catch (Exception e) { LabelStumble("Off", e); }
        }

        /// <summary>Zdejmuje warstwe napisow z ekranu mapy (koniec mapy, wylacznik, wylaczone wioski, 3 potkniecia). Ekran juz nie ten - same odwolania.</summary>
        private void RemoveLabelLayer(bool screenAlive)
        {
            _lblCount = 0;
            GauntletLayer layer = _lblLayer;
            GauntletMovieIdentifier movie = _lblMovie;
            MapBurnedLabelsVM vm = _lblVm;
            MapScreen screen = _lblScreen;
            _lblLayer = null; _lblMovie = null; _lblVm = null; _lblScreen = null; _lblConv = null;
            if (layer == null) return;
            try
            {
                if (screenAlive && screen != null && ReferenceEquals(MapScreen.Instance, screen) && !layer.IsFinalized)
                {
                    if (movie != null) layer.ReleaseMovie(movie);
                    if (screen.HasLayer(layer)) screen.RemoveLayer(layer);
                }
                if (vm != null) vm.OnFinalize();
            }
            catch (Exception e)
            {
                _stLbl++;
                if (_stLbl <= 3) Log.Error("MapVillagesView.LabelRemove (potkniecie " + _stLbl + ")", e);
            }
        }

        /// <summary>Potkniecie warstwy napisow: liczone; 3 z rzedu = napisy wylaczone do nastepnego wczytania mapy (obrazki i reszta widoku bez zmian).
        /// Audyt 13.10: dwa liczniki "z rzedu". _lblStrikes zeruje udana klatka (LabelFrame, co klatke), _lblTickStrikes - udany wybor napisow
        /// (LabelTick, 4/s). Dotad byl jeden, zerowany co klatke: staly wyjatek w LabelTick (kilkanascie udanych klatek miedzy dwoma wyborami)
        /// nigdy nie doszedl do 3 i lecial 4 razy na sekunde do konca sesji mapy.</summary>
        private void LabelStumble(string where, Exception e)
        {
            _stLbl++;
            _lblStrikes++;
            int row = _lblStrikes > _lblTickStrikes ? _lblStrikes : _lblTickStrikes;
            if (_stLbl <= 3 || _stLbl % 500 == 0) Log.Error("MapVillagesView.Label" + where + " (potkniecie " + _stLbl + ", z rzedu " + row + "/" + LabelStrikesMax + ")", e);
            if (row < LabelStrikesMax || _lblBroken) return;
            _lblBroken = true;
            RemoveLabelLayer(true);
            Log.Info("Wioski - wyglad (E7/W4): napisy nad spalonymi WYLACZONE do nastepnego wczytania mapy po " + LabelStrikesMax + " potknieciach z rzedu (obrazki bez zmian).");
        }
    }
}
