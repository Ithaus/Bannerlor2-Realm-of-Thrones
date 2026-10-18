// E7/W5 (238, PLAN 7.9 c, Jeff 08.10 "wszystkie 8"; projekt etapu 7 v2 rozdz. 3 E7/W5, R17; WL-P rozdz. 2): proporczyk pana nad wioskami z bliska.
// Osobna encja w scenie mapy przy przednim narozniku obrysu wioski (lokalna +Y = front: droga / woda; naroznik musi stac na ladzie wedlug tej
// samej reguly co budynki obrazka - Geo.Land; zaden na ladzie = srodek obrazka), podstawa na gruncie z terenu, skala 0.32 (namiot gry 0.15-0.5).
// Siatka = choragiew, ktora gra stawia przy partiach w zamku i przy namiotach oblezenia: MobilePartyVisual.GetBannerOfCharacter(banner,
// "campaign_flag") (SandBox.View 1.4.8: kopia siatki + material z tekstura herbu z MapScreen.CharacterBannerMaterialCache; klucz = (material,
// OBIEKT Banner), Banner nie nadpisuje Equals - wiec jeden WLASNY obiekt Banner na kod herbu = jedna tekstura dla wszystkich wiosek jednego pana).
// Pan = rod wlasciciela wsi gry (Village.Bound.OwnerClan, Clan.Banner - dla rodu panujacego herb krolestwa), ten sam co dymek W2 i HeldLine.
// Jeff 16.10 (D1, VillageBannerPerHolder): herb GOSPODARZA wioski (VillageHolders: majatek BK, rycerz z lore, rycerz krolestwa, pan) - ten sam co dymek i okno okregu.
// Jeff 16.10 (D2, VillageTwoPennants; "jedna flaga wasala-rycerza i seniora jako druga flaga"): gospodarz inny niz pan = DRUGI proporczyk - pana (Village.Bound.OwnerClan,
// jak dymek W2) na drugim narozniku obrysu: najpierw drugi przedni (ten sam front, drugi koniec ulicy), potem tylne; zaden inny na ladzie - bez drugiego (licznik).
// Ten sam wybor najblizszych 80 wiosek; budowa najwyzej 12 PROPORCZYKOW na cwierc sekundy (wioska z dwoma liczona za dwa); porownanie obu kodow raz na sekunde.
// Bez fizyki i skryptow (nie lapie klikniec; dymek wioski szuka po obrysie, nie po encji). Tylko z bliska: kamera z <= 90, najblizsze 80 wiosek
// z postawionym obrazkiem (od punktu, na ktory patrzy kamera), budowa najwyzej 12 na cwierc sekundy; dalej schowane, nie usuwane.
// Raz na sekunde dla pokazanych porownanie KODU herbu (gra zmienia barwy herbu w miejscu, ta sama referencja; kazda zmiana zeruje
// Banner._bannerCode) - inny kod = przebudowa tej jednej flagi (zdobycie zamku, zmiana krolestwa, zmiana rodu panujacego bez nasluchow).
// Po misji: MapScreen.ClearGPUMemory -> SandBoxViewVisualManager.ClearVisualMemory -> nasze ClearVisualMemory (Texture.ReleaseGpuMemories
// zwalnia tekstury herbow) - pamiec obiektow Banner od nowa (nowy obiekt = nowa tekstura) i kazdy proporczyk przebudowany przy nastepnym
// pokazaniu; samo ClearVisualMemory nie rusza encji (moze przyjsc, gdy mapa nie jest na ekranie).
// Nic co klatke (praca w ticku widoku 4 razy na sekunde, encje tylko przy zmianie stanu - CLAUDE.md 7), nic do zapisu gry, wylacznik
// VillagePennants. Potkniecia liczone per wioska (3 = ta jedna wioska bez proporczyka do nastepnego wczytania), nigdy wylacznik calosci.
// Po recenzji D2 (16.10, koszt): (1) LIMIT ENCJI - plotno choragwi tyka co klatke, gdy pokazane; razem najwyzej PenEntityMax (100) proporczykow:
// pierwszy dla kazdej z najblizszych 80 wiosek, drugi (pana) tylko najblizszym w reszcie limitu (wybrane posortowane od punktu kamery); drugi poza
// limitem schowany (flagi, bez przebudowy), postawiony osobno od pierwszego (zmiana samego pana = tylko drugi od nowa). (2) LIMIT TEKSTUR - kazdy nowy
// kod herbu to tekstura 1024x1024 (GetTableauTextureLarge, ok. 4 MiB) do najblizszej misji (ClearGPUMemory); powyzej PenCodesSoft (100) kodow w pamieci
// nowy herb gospodarza dostaja tylko najblizsze PenCodesNear (30) wiosek, powyzej PenCodesHard (160) zadna - reszta herb pana (ten sam co partie,
// jeden proporczyk). (3) liczniki: wiosek bez drugiego naroznika na ladzie (rozne uid) osobno od potkniec budowy drugiego.
// sw-herby (noc 17/18.10, klucz VillagePennantSmallTexture, STAN-PRAC ### 60 "limit 160 ... po dlugim zwiedzaniu bez misji czesc wiosek pokazuje herb pana (w tescie 125)").
// Sprawdzone w dekompilacji 1.4.8: GetBannerOfCharacter -> BannerVisualExtensions.GetTableauTextureLarge -> BannerTextureCreationData (isLarge) ->
// ThumbnailCacheManager -> BannerPersistentTextureCache (klucz "Mesh_BannerTableauLarge:<kod>", licznik odwolan 1 na zawsze, OnReleaseTexture nic nie robi -
// tekstura zyje do ForceClearAllCache w MapScreen.ClearGPUMemory przed misja albo FlushCache na koniec mapy); BannerTextureCreator: 1024x1024 (duza) albo
// 512x512 (mala), innych rozmiarow gra nie daje (256 tylko z wlasnym widokiem miniatur - za duze ryzyko bez testu w grze). Limit 100/160 byl NASZ (CapCode).
// Teraz przy kluczu: (1) wlasna kopia materialu choragwi (jak GetBannerOfCharacter: kopia siatki, material.CreateCopy, tekstura w slot 1 + flaga
// use_tableau_blending) z tekstura MALA 512x512 (1 MiB zamiast 4) przez PenTexData : BannerTextureCreationData - ta sama droga gry co tarcze z herbem
// w misji (GetTableauTextureSmall, ItemCollectionElementViewExtensions), ale z WLASNYM kluczem "ArmPen_Mesh_BannerTableauSmall:<kod>" (nikt inny go nie
// zamawia, wiec zwolnienie nie zabierze tekstury dymkowi, ekwipunkowi ani choragwi partii); (2) ksiega herbow _penTex (kod -> materialy, tekstura, takt
// ostatniego uzycia); herb uzyty w ticku = kod pierwszego proporczyka kazdej wybranej wioski i kod pokazanego drugiego; (3) ponad PenTexKeep (128) herbow
// w pamieci: najdawniej uzyte (nieuzyte w tym ticku, starsze niz 1 s), najwyzej PenEvictPerTick (32) na tick - encje ich proporczykow (dalekich, schowanych)
// zdjete RAZ (wracaja przy kamerze, budzet budowy jak dotad), potem wezel pamieci gry usuniety (ThumbnailCache.RemoveThumbnailCacheNode przez refleksje -
// ta sama sciezka, ktora gra zwalnia nieuzywane miniatury: anuluje zamowienie, Texture.Release) i nasze kopie materialow ManualInvalidate. W kadrze naraz
// najwyzej PenEntityMax (100) proporczykow, wiec herbow w uzyciu < 128: herb pana zamiast gospodarza tylko jako siatka bezpieczenstwa (PenTexHard 224;
// w uzyciu <= 100 + mlode nieuzyte <= 12 nowych x 4 ticki = 148).
// Po misji (ClearVisualMemory, zaraz potem ForceClearAllCache gry zwalnia nasze tekstury natychmiast): ksiega zapomniana bez ruszania pamieci gry,
// proporczyki przebudowane przy pokazaniu jak dotad. Brak refleksji (inna wersja gry) = bez zwalniania, male tekstury do misji, limit 400/640 (ta sama
// pamiec co stare 100/160 x 4 MiB). Off = stara droga dokladnie jak dotad. Linia "Proporczyki - tekstury (E7/W5)" (start, co 2 minuty przy zmianie, przy
// zdjeciu): zamowione / zwolnione / w pamieci, tozsamosc ksiegi, klucze ArmPen_ w pamieci gry, MiB, encje, herb zastepczy (grupa nherby sprawdz_logi).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using SandBox.View.Map;
using SandBox.View.Map.Visuals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.Tableaus;
using TaleWorlds.MountAndBlade.View.Tableaus.Thumbnails;

namespace Armoury
{
    public sealed partial class MapVillagesView
    {
        private const float PenCameraZ = 90f;         // proporczyki tylko przy kamerze z <= 90 (WL-P 2)
        private const int PenMax = 80;                // najblizsze 80 wiosek z obrazkiem
        private const int PenEntityMax = 100;         // po recenzji D2: najwyzej 100 pokazanych proporczykow (encji z plotnem) - drugie tylko najblizszym w limicie
        private const int PenCodesSoft = 100, PenCodesHard = 160, PenCodesNear = 30;   // po recenzji D2: limit tekstur herbow (nowe kody w _penBanners)
        private const int PenBuildPerTick = 12;       // budowa / przebudowa najwyzej 12 na cwierc sekundy
        private const float PenCheckEvery = 1f;       // porownanie kodu herbu pokazanych raz na sekunde
        private const float PenScale = 0.32f;         // skala choragwi (namiot oblezenia gry 0.15-0.5)
        private const float PenSink = 0.02f;          // drzewce lekko w gruncie (bez szpary na stoku)
        private const int PenStrikesMax = 3;          // potkniecia jednej wioski, po ktorych ta JEDNA wioska jest bez proporczyka
        private const int PenWhyNone = 0, PenWhyMission = 1, PenWhyLord = 2;   // powod przebudowy
        private const int PenWhyFar = 3;              // sw-herby: encja zdjeta przy zwalnianiu tekstury dalekiego herbu (wraca przy kamerze)
        private static readonly string[] PenMeshes = { "campaign_flag", "map_banner" };   // zapas bez plotna (WL-P 2 [AUTOTEST])
        // sw-herby (VillagePennantSmallTexture): ksiega herbow na teksturach 512x512 z wlasnym kluczem i zwalnianie dalekich
        private const int PenTexKeep = 128;           // ponad tyle herbow w pamieci - zwalnianie najdawniej uzytych (w kadrze najwyzej 100 proporczykow)
        private const int PenTexHard = 224;           // siatka bezpieczenstwa: tyle herbow w pamieci i nowy herb gospodarza = herb pana (przy dzialajacym zwalnianiu nigdy: <= 100 + 48)
        private const int PenEvictPerTick = 32;       // najwyzej tyle herbow zwolnionych na cwierc sekundy (nowych najwyzej 12 - budzet budowy)
        private const int PenTexMinAge = 4;           // herb zamowiony co najmniej 4 ticki (1 s) temu - zamowienie gry dawno wyrenderowane (mlodych nieuzytych <= 48)
        private const int PenCodesSoftSmall = 400, PenCodesHardSmall = 640;   // bez zwalniania (brak refleksji): ta sama pamiec co stare 100/160 x 1024x1024
        private const int PenTexSide = 512;           // BannerTextureCreator: mala tekstura tableau 512x512 (duza 1024x1024)
        private const float PenTexEvery = 120f;       // linia tekstur w logu najwyzej co 2 minuty, tylko gdy cos sie zmienilo
        private const string PenRenderPrefix = "ArmPen_";   // wlasny klucz w pamieci gry: "ArmPen_Mesh_BannerTableauSmall:<kod>"

        /// <summary>Stan proporczyka jednej wioski (nic do zapisu gry).</summary>
        private sealed class PenState
        {
            public GameEntity E;              // encja w scenie mapy (null = nie zbudowany albo zdjety)
            public string Code = "";          // kod herbu, z ktorym zbudowano
            public GameEntity E2;             // D2: drugi proporczyk - pana (null = jeden, jeszcze nie zbudowany albo brak drugiego naroznika na ladzie)
            public string Code2 = "";         // D2: kod herbu pana, z ktorym zbudowano drugi ("" = brak drugiego)
            public string Want2 = "";         // po recenzji D2: kod herbu pana na drugi proporczyk ("" = jeden proporczyk)
            public int Corner = -1;           // po recenzji D2: naroznik pierwszego (drugi szuka innego); -1 = srodek obrazka, bez drugiego
            public bool Shown2;               // po recenzji D2: drugi pokazany (limit encji)
            public bool NoCorner2, Failed2;   // po recenzji D2: drugi bez naroznika na ladzie / blad budowy - bez ponownej proby do zmiany herbu albo przebudowy
            public bool Shown, Failed;
            public int Why;                   // PenWhy*: do przebudowy (po misji / inny herb)
            public int Strikes;
            public int Stamp;                 // numer ticku, w ktorym wioska byla wsrod wybranych
            public PenTex Tex, Tex2;          // sw-herby: wpis ksiegi herbow pierwszego / drugiego (null = stara droga albo nie zbudowany)
            public bool Capped;               // sw-herby: pierwszy zbudowany z herbem pana zamiast gospodarza (limit tekstur)
        }

        /// <summary>sw-herby: jeden herb w ksiedze tekstur proporczykow (kod -> kopie materialow choragwi z tekstura 512x512 z kluczem ArmPen_).
        /// Nic do zapisu gry; zwalniany jako calosc (encje, wezel pamieci gry, materialy).</summary>
        private sealed class PenTex
        {
            public string Code = "";
            public string RenderId;           // klucz w BannerPersistentTextureCache (z PenTexData)
            public TaleWorlds.Core.Banner Banner;
            public Texture Tex;               // tekstura z pamieci gry (do odczytu rozmiaru w logu; po zwolnieniu null)
            public readonly List<PenMat> Mats = new List<PenMat>(2);
            public int Stamp;                 // numer ticku ostatniego uzycia (wybrana wioska z tym herbem)
            public int Born;                  // numer ticku zamowienia
            public bool Evict, Released;
        }

        private sealed class PenMat
        {
            public Material Base;             // material siatki choragwi (zasob gry; porownanie po wskazniku)
            public Material Copy;             // nasza kopia z tekstura herbu
        }

        /// <summary>sw-herby: zamowienie tekstury herbu ta sama droga, co GetTableauTextureSmall gry (BannerTextureCreationData - pamiec trwala, tekstura
        /// zawsze wazna, setAction od razu), ale z wlasnym kluczem: RenderId = "ArmPen_" + "Mesh_BannerTableauSmall:" + kod.</summary>
        private sealed class PenTexData : BannerTextureCreationData
        {
            public PenTexData(TaleWorlds.Core.Banner banner, Action<Texture> setAction)
                : base(banner, setAction, null, BannerDebugInfo.CreateManual("ArmouryVillagePennant"), true, false)
            {
                RenderId = PenRenderPrefix + RenderId;
            }
        }

        private readonly List<Slot> _penShown = new List<Slot>();
        private readonly Dictionary<string, TaleWorlds.Core.Banner> _penBanners = new Dictionary<string, TaleWorlds.Core.Banner>(StringComparer.Ordinal);   // kod -> wlasny obiekt (do misji)
        private Slot[] _penCand = new Slot[256];      // bufor wyboru (bez alokacji w ticku)
        private float[] _penDist = new float[256];
        private string _penMesh;                      // null = nie sprawdzone na tej mapie; "" = brak siatki
        private bool _penOff;
        private float _penCheckAcc;
        private int _penStamp, _penShownMax, _penBuilt, _penBuildTicks, _penAfterMission, _penMissions, _penLord, _penFailed, _stPen;
        private int _penTwoBuilt, _penTwoMax, _stPen2, _penTwoHiddenMax;   // D2: zbudowane drugie proporczyki, najwiecej podwojnych w kadrze, potkniecia drugiego, najwiecej schowanych drugich (limit encji)
        private readonly HashSet<string> _penNoCorner = new HashSet<string>(StringComparer.Ordinal);   // po recenzji D2: wioski (uid) bez drugiego naroznika na ladzie
        private readonly HashSet<string> _penCapped = new HashSet<string>(StringComparer.Ordinal);     // po recenzji D2: wioski (uid) z herbem pana zamiast gospodarza (limit tekstur)
        private int _penCodesMax;                                                                       // po recenzji D2: najwiecej kodow herbow w pamieci
        private long _penBuildTime;
        private string _penLastLine = "";
        // sw-herby (VillagePennantSmallTexture): ksiega herbow, tryb mapy, liczniki linii tekstur
        private readonly Dictionary<string, PenTex> _penTex = new Dictionary<string, PenTex>(StringComparer.Ordinal);
        private readonly List<PenTex> _penEvictBuf = new List<PenTex>();
        private static readonly Comparison<PenTex> PenTexOlder = (a, b) => a.Stamp.CompareTo(b.Stamp);
        private int _penTexMode = -1;                 // -1 nie ustalony na tej mapie, 0 stara droga, 1 male tekstury z ksiega
        private bool _penRelOk;                       // zwalnianie dalekich czynne (refleksja na pamiec gry)
        private PenTex _penLastTex;                   // wpis ostatniego MakePennant (nowa droga)
        private bool _penCapNow;                      // ostatni CapCode dal herb pana zamiast gospodarza
        private float _penTexAcc;
        private string _penTexLastLine = "";
        private int _penTexNew, _penTexReq, _penTexShared, _penTexRefused, _penTexFreed, _penTexNoNode, _penTexForgot, _penTexMax;
        private int _penTexKilled, _penTexRebuilt, _penTexBlocked, _penTexEvictRuns, _stPenTex, _penTexLiveMax;
        private long _penTexEvictTime;
        private int _penTexSampleW, _penTexSampleH, _penTexSampleKiB;
        private double _penTexMiBMax;
        private static int _penRelState;              // 0 nie sprawdzone, 1 jest, -1 brak (raz na proces gry)
        private static string _penRelWhy = "";
        private static FieldInfo _penMapField;        // ThumbnailCache<BannerTextureCreationData>._map (protected)
        private static MethodInfo _penRemoveNode;     // ThumbnailCache<BannerTextureCreationData>.RemoveThumbnailCacheNode(node, bool) (protected)

        /// <summary>Praca proporczykow w ticku widoku (po UpdateVisibility - wybor tylko sposrod pokazanych obrazkow tego samego ticku).</summary>
        private void PennantTick(MapScreen screen)
        {
            try
            {
                if (!Settings.Current.VillagePennants)
                {
                    if (!_penOff)
                    {
                        _penOff = true;
                        int removed = RemovePennants();
                        Log.Info("Proporczyki (E7/W5): wylaczone w MCM - zdjete " + removed + ".");
                    }
                    return;
                }
                if (_penOff) { _penOff = false; Log.Info("Proporczyki (E7/W5): wlaczone w MCM - wracaja przy kamerze."); }
                if (_penMesh == null) PickPennantMesh();
                if (_penMesh.Length == 0) return;
                // sw-herby: droga tekstur ustalona raz na mape; zmiana klucza w MCM = wszystkie proporczyki zdjete raz i od nowa przy kamerze
                bool small = Settings.Current.VillagePennantSmallTexture;
                if (_penTexMode < 0) PenTexStart(small);
                else if ((_penTexMode == 1) != small)
                {
                    int removed = RemovePennants();
                    Log.Info("Proporczyki - tekstury (E7/W5): VillagePennantSmallTexture zmienione w MCM na " + (small ? "tak" : "NIE") + " - zdjete " + removed
                             + " proporczykow, wracaja przy kamerze.");
                    // po recenzji sw-herby: wszystkie proporczyki stawiane od nowa w nowej drodze - zbior wiosek z herbem pana zamiast gospodarza tez od nowa
                    // (inaczej po NIE -> tak linia tekstur pokazuje stary licznik z limitu 100/160; linia "przy zdjeciu" wyzej ma jeszcze stary stan)
                    _penCapped.Clear();
                    PenTexStart(small);
                }
                if (screen == null || screen.MapCameraView == null || screen.MapCameraView.Camera == null) return;
                Camera camera = screen.MapCameraView.Camera;
                Vec3 cam = camera.Position;
                _penStamp++;
                bool check = false;
                _penCheckAcc += TickEvery;
                if (_penCheckAcc >= PenCheckEvery) { _penCheckAcc = 0f; check = true; }
                int n = cam.z <= PenCameraZ ? PickPennants(screen, camera, cam) : 0;
                // 1) schowane: wypadly z wybranych (oddalenie, kamera wyzej, obrazek schowany pod mgla / zdjety)
                for (int i = _penShown.Count - 1; i >= 0; i--)
                {
                    Slot s = _penShown[i];
                    if (s.Pen != null && s.Pen.Stamp == _penStamp) continue;
                    _penShown.RemoveAt(i);
                    try { HidePennant(s); }
                    catch (Exception e) { Stumble(s, "Hide", e); }
                }
                // 2) wybrane (od najblizszej): pokaz / zbuduj / przebuduj; drugi proporczyk (pan) najblizszym w limicie encji
                int built = 0;
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                int allow2 = Math.Max(0, PenEntityMax - n), two = 0, hidden2 = 0;
                for (int i = 0; i < n; i++)
                {
                    Slot s = _penCand[i];
                    PenState p = s.Pen;
                    try
                    {
                        if (!(p.Shown && p.Why == PenWhyNone && !check))
                        {
                            string code = LordCode(s);
                            if (code.Length == 0)
                            {
                                // wies bez pana (rzadkie): bez proporczyka; wroci przy pierwszym ticku z panem
                                if (p.Shown) { _penShown.Remove(s); HidePennant(s); }
                                continue;
                            }
                            code = CapCode(s, code, i);           // po recenzji D2: limit tekstur herbow
                            string code2 = LiegeCode(s, code);   // D2: herb pana obok herbu gospodarza ("" = jeden proporczyk)
                            if (p.E != null && p.Why == PenWhyNone && !string.Equals(code, p.Code, StringComparison.Ordinal)) p.Why = PenWhyLord;
                            if (p.E != null && p.Why == PenWhyNone)
                            {
                                // ten sam gospodarz, inny pan (albo pan zamiast drugiego) - tylko drugi od nowa
                                if (!string.Equals(code2, p.Want2, StringComparison.Ordinal)) { DropSecond(p); p.Want2 = code2; _penLord++; }
                                if (!p.Shown) ShowPennant(s);
                            }
                            else
                            {
                                if (built >= PenBuildPerTick) continue;   // reszta w nastepnym ticku (Why zostaje)
                                built += BuildPennant(s, code, code2);
                            }
                        }
                        if (p.Want2.Length > 0 && two >= allow2 && p.E2 != null) hidden2++;
                        built += SyncSecond(s, two < allow2, ref two, built);
                    }
                    catch (Exception e) { Stumble(s, "Build", e); }
                }
                if (hidden2 > _penTwoHiddenMax) _penTwoHiddenMax = hidden2;
                if (_penBanners.Count > _penCodesMax) _penCodesMax = _penBanners.Count;
                if (built > 0)
                {
                    _penBuildTicks++;
                    _penBuilt += built;
                    _penBuildTime += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
                }
                if (_penShown.Count > _penShownMax) _penShownMax = _penShown.Count;
                if (_penTexMode == 1) PenTexTouchAndEvict(n);   // sw-herby: herby uzyte w tym ticku; ponad PenTexKeep - zwalnianie dalekich (poza czasem budowy W5-2)
                if (built > 0)
                {
                    int twoShown = 0;
                    foreach (var q in _penShown) if (q.Pen != null && q.Pen.Shown2) twoShown++;
                    if (twoShown > _penTwoMax) _penTwoMax = twoShown;
                }
                // sw-herby: linia tekstur najwyzej co 2 minuty, tylko gdy cos sie zmienilo (koniec mapy / wylacznik - RemovePennants i DropPennants)
                _penTexAcc += TickEvery;
                if (_penTexAcc >= PenTexEvery)
                {
                    _penTexAcc = 0f;
                    PenTexLog(false);
                }
            }
            catch (Exception e)
            {
                _stPen++;
                if (_stPen <= 3 || _stPen % 200 == 0) Log.Error("MapVillagesView.PennantTick (potkniecie " + _stPen + ")", e);
            }
        }

        /// <summary>Najblizsze PenMax pokazanych wiosek z obrazkiem od punktu, na ktory patrzy kamera (pozycja + kierunek x odleglosc kamery od celu;
        /// zapas - punkt pod kamera). Oznacza wybrane numerem ticku; zwraca ich liczbe (pierwsze n w _penCand).</summary>
        private int PickPennants(MapScreen screen, Camera camera, Vec3 cam)
        {
            float lx = cam.x, ly = cam.y;
            try
            {
                Vec3 dir = camera.Direction;
                float dist = screen.MapCameraView.CameraDistance;
                if (dist > 0f && dist < 2000f && dir.z < 0f) { lx = cam.x + dir.x * dist; ly = cam.y + dir.y * dist; }
            }
            catch { lx = cam.x; ly = cam.y; }
            int m = 0;
            for (int i = 0; i < _shown.Count; i++)
            {
                Slot s = _shown[i];
                if (s.Root == null || !s.Placed || s.Failed || (s.Pen != null && s.Pen.Failed)) continue;
                if (m == _penCand.Length) { Array.Resize(ref _penCand, m * 2); Array.Resize(ref _penDist, m * 2); }
                float dx = s.Cx - lx, dy = s.Cy - ly;
                _penCand[m] = s;
                _penDist[m] = dx * dx + dy * dy;
                m++;
            }
            if (m > 1) Array.Sort(_penDist, _penCand, 0, m);   // po recenzji D2: zawsze od najblizszej (limit encji i tekstur liczony od kamery)
            int n = Math.Min(m, PenMax);
            for (int i = 0; i < n; i++)
            {
                Slot s = _penCand[i];
                if (s.Pen == null) s.Pen = new PenState();
                s.Pen.Stamp = _penStamp;
            }
            return n;
        }

        /// <summary>Jeff 16.10 (D1, VillageBannerPerHolder): kod herbu GOSPODARZA tej wioski (VillageHolders.Code - ten sam gospodarz co dymek i okno okregu; pamiec
        /// okregu na dobe, wiec porownanie raz na sekunde to sam odczyt); klucz off - herb pana okregu jak dotad. "" = brak pana albo herbu.</summary>
        private static string LordCode(Slot s)
        {
            if (Settings.Current.VillageBannerPerHolder && s.D != null && s.D.S != null) return VillageHolders.Code(s.D.S, s.R.Uid);
            return LordCode(s.D);
        }

        /// <summary>Jeff 16.10 (D2, VillageTwoPennants): kod herbu PANA na drugi proporczyk - tylko przy herbach gospodarza (VillageBannerPerHolder) i gdy gospodarz ma inny
        /// herb niz pan; "" = jeden proporczyk. Sam odczyt gry (Village.Bound.OwnerClan.Banner), bez BK.</summary>
        private static string LiegeCode(Slot s, string holderCode)
        {
            var set = Settings.Current;
            if (set == null || !set.VillageTwoPennants || !set.VillageBannerPerHolder) return "";
            string lc = LordCode(s.D);
            return lc.Length > 0 && !string.Equals(lc, holderCode, StringComparison.Ordinal) ? lc : "";
        }

        /// <summary>Po recenzji D2 (limit tekstur): kod herbu gospodarza, ktorego jeszcze nie ma w pamieci, powyzej PenCodesSoft kodow tylko dla najblizszych
        /// PenCodesNear wiosek (rank = miejsce od kamery), powyzej PenCodesHard dla zadnej - inaczej herb pana (jeden proporczyk). Kod juz w pamieci - zawsze.</summary>
        private string CapCode(Slot s, string code, int rank)
        {
            _penCapNow = false;
            if (_penTexMode == 1)
            {
                // sw-herby: male tekstury z ksiega; przy zwalnianiu dalekich limit tylko jako siatka bezpieczenstwa (herbow w uzyciu < PenTexKeep),
                // bez zwalniania - limit tej samej pamieci co stare 100/160 duzych
                if (_penTex.ContainsKey(code)) return code;
                int held = _penTex.Count;
                if (_penRelOk ? held < PenTexHard : (held < PenCodesSoftSmall || (held < PenCodesHardSmall && rank < PenCodesNear))) return code;
            }
            else
            {
                if (_penBanners.ContainsKey(code)) return code;
                int have = _penBanners.Count;
                if (have < PenCodesSoft || (have < PenCodesHard && rank < PenCodesNear)) return code;
            }
            string lord = LordCode(s.D);
            if (lord.Length == 0 || string.Equals(lord, code, StringComparison.Ordinal)) return code;
            _penCapped.Add(s.R.Uid);
            _penCapNow = true;
            return lord;
        }

        /// <summary>Kod herbu pana okregu (Village.Bound.OwnerClan.Banner - jak HeldLine i dymek W2); "" = brak pana albo herbu.</summary>
        private static string LordCode(District d)
        {
            Settlement v = d != null ? d.S : null;
            if (v == null || v.Village == null) return "";
            Settlement bound = v.Village.Bound;
            Clan c = bound != null ? bound.OwnerClan : null;
            TaleWorlds.Core.Banner b = c != null ? c.Banner : null;
            string code = b != null ? b.BannerCode : null;
            return code ?? "";
        }

        /// <summary>Siatka choragwi raz na mape: campaign_flag (z plotnem, jak przy partiach gry), zapas map_banner; brak obu = bez proporczykow
        /// na tej mapie (fakt o zasobach gry, nie skutek potkniecia). Linia startowa w logu.</summary>
        private void PickPennantMesh()
        {
            _penMesh = "";
            foreach (string name in PenMeshes)
            {
                MetaMesh mm = null;
                try { mm = MetaMesh.GetCopy(name, false, true); }
                catch (Exception e)
                {
                    _stPen++;
                    if (_stPen <= 3) Log.Error("MapVillagesView.PickPennantMesh " + name + " (potkniecie " + _stPen + ")", e);
                }
                if (mm != null && mm.IsValid) { _penMesh = name; break; }
            }
            Log.Info("Proporczyki (E7/W5): start | siatka "
                     + (_penMesh.Length == 0 ? "BRAK (campaign_flag, map_banner) - proporczyki nie beda pokazane do nastepnego wczytania"
                        : _penMesh + (_penMesh == PenMeshes[0] ? " (z plotnem, jak przy partiach gry)" : " (ZAPAS - brak campaign_flag, bez plotna)"))
                     + " | kamera z <= " + PenCameraZ.ToString("0", CultureInfo.InvariantCulture) + ", najblizsze " + PenMax + ", budowa najwyzej "
                     + PenBuildPerTick + " na cwierc sekundy, kod herbu raz na sekunde, skala " + PenScale.ToString("0.00", CultureInfo.InvariantCulture)
                     + " | D2: drugi proporczyk pana przy gospodarzu innym niz pan " + (Settings.Current.VillageTwoPennants ? "tak" : "NIE (VillageTwoPennants)")
                     + " (kazdy proporczyk w limicie budowy; razem najwyzej " + PenEntityMax + " pokazanych, drugie najblizszym) | herby w pamieci: nowe powyzej "
                     + PenCodesSoft + " tylko najblizszym " + PenCodesNear + ", powyzej " + PenCodesHard + " herb pana | nic do zapisu gry.");
        }

        /// <summary>Stawia (albo stawia od nowa) PIERWSZY proporczyk jednej wioski (gospodarz) i go pokazuje; drugi (pan, code2) - SyncSecond w tym samym
        /// albo nastepnym ticku (limit encji). Stare encje zdejmowane przed nowymi. Zwraca liczbe postawionych proporczykow (budzet ticku liczy kazdy).</summary>
        private int BuildPennant(Slot s, string code, string code2)
        {
            PenState p = s.Pen;
            int why = p.Why;
            if (p.Shown) { _penShown.Remove(s); p.Shown = false; }
            if (p.E != null)
            {
                GameEntity old = p.E;
                p.E = null;
                old.Remove(111);
            }
            DropSecond(p);
            Scene scene = MapScene;
            if (scene == null) throw new InvalidOperationException("brak sceny mapy");
            int corner;
            GameEntity e = MakePennant(scene, s, code, -1, "", out corner);
            p.E = e;
            p.Code = code;
            p.Want2 = code2;
            p.Corner = corner;
            p.Why = PenWhyNone;
            p.Tex = _penLastTex;     // sw-herby: wpis ksiegi herbow (stara droga - null)
            p.Capped = _penCapNow;   // sw-herby: herb pana zamiast gospodarza (CapCode tej samej wioski w tym samym przebiegu)
            if (why == PenWhyMission) _penAfterMission++;
            else if (why == PenWhyLord) _penLord++;
            else if (why == PenWhyFar) _penTexRebuilt++;
            ShowPennant(s);
            return 1;
        }

        /// <summary>Po recenzji D2: drugi proporczyk (pan) pokazanej wioski - w limicie encji (may: miejsce wsrod najblizszych) postawiony (budzet ticku) i
        /// pokazany, poza limitem schowany. two - drugie zajmujace limit (zbudowane albo czekajace na budzet). Zwraca liczbe postawionych.</summary>
        private int SyncSecond(Slot s, bool may, ref int two, int built)
        {
            PenState p = s.Pen;
            if (p == null || p.E == null || !p.Shown) return 0;
            if (!may || p.Want2.Length == 0)
            {
                if (p.Shown2) HideSecond(p);
                return 0;
            }
            int made = 0;
            if (p.E2 == null)
            {
                if (p.NoCorner2 || p.Failed2) return 0;
                if (built >= PenBuildPerTick) { two++; return 0; }   // miejsce w limicie zajete, budowa w nastepnym ticku
                made = BuildSecond(s);
                if (made == 0) return 0;
            }
            two++;
            if (!p.Shown2)
            {
                ShowEntity(p.E2);
                p.Shown2 = true;
            }
            return made;
        }

        /// <summary>Drugi proporczyk na drugim narozniku obrysu. 1 - postawiony (schowany do SyncSecond), 0 - brak naroznika na ladzie (wioska w liczniku)
        /// albo blad (licznik potkniec drugiego, bez ponownej proby do zmiany herbu); pierwszy zostaje.</summary>
        private int BuildSecond(Slot s)
        {
            PenState p = s.Pen;
            GameEntity e2 = null;
            try
            {
                Scene scene = MapScene;
                if (scene == null) throw new InvalidOperationException("brak sceny mapy");
                int corner;
                if (p.Corner >= 0) e2 = MakePennant(scene, s, p.Want2, p.Corner, "_lord", out corner);
            }
            catch (Exception ex)
            {
                _stPen2++;
                p.Failed2 = true;
                if (_stPen2 <= 3 || _stPen2 % 200 == 0) Log.Error("MapVillagesView.PennantBuild2 " + s.R.Uid + " (potkniecie " + _stPen2 + ")", ex);
                return 0;
            }
            if (e2 == null)
            {
                p.NoCorner2 = true;
                _penNoCorner.Add(s.R.Uid);
                return 0;
            }
            p.E2 = e2;
            p.Code2 = p.Want2;
            p.Tex2 = _penLastTex;   // sw-herby: wpis ksiegi herbow drugiego (stara droga - null)
            p.Shown2 = false;
            _penTwoBuilt++;
            return 1;
        }

        /// <summary>Zdejmuje drugi proporczyk ze sceny (zmiana pana, przebudowa pierwszego); flagi drugiego od nowa.</summary>
        private static void DropSecond(PenState p)
        {
            if (p.E2 != null)
            {
                GameEntity old2 = p.E2;
                p.E2 = null;
                old2.Remove(111);
            }
            p.Code2 = "";
            p.Tex2 = null;
            p.Shown2 = p.NoCorner2 = p.Failed2 = false;
        }

        private static void HideSecond(PenState p)
        {
            p.Shown2 = false;
            if (p.E2 == null) return;
            p.E2.SetVisibilityExcludeParents(false);
            p.E2.EntityFlags = p.E2.EntityFlags | EntityFlags.DoNotTick;
        }

        /// <summary>Jedna encja proporczyka: siatka choragwi z herbem kodu, naroznik obrysu (avoid = naroznik pierwszego proporczyka albo -1). null - drugi bez
        /// naroznika na ladzie (pierwszy zawsze stoi: zaden naroznik na ladzie = srodek obrazka).</summary>
        private GameEntity MakePennant(Scene scene, Slot s, string code, int avoid, string suffix, out int corner)
        {
            float x, y, z;
            _penLastTex = null;
            corner = PennantSpot(s, avoid, out x, out y, out z);
            if (avoid >= 0 && corner < 0) return null;
            MetaMesh mm;
            if (_penTexMode == 1) mm = PenTexMesh(code);   // sw-herby: wlasne materialy, tekstura 512x512 z kluczem ArmPen_ (ustawia _penLastTex)
            else
            {
                TaleWorlds.Core.Banner banner;
                if (!_penBanners.TryGetValue(code, out banner))
                {
                    banner = new TaleWorlds.Core.Banner(code);   // wlasna kopia: klucz pamieci materialow gry (po misji nowy obiekt = nowa tekstura)
                    _penBanners[code] = banner;
                }
                mm = MobilePartyVisual.GetBannerOfCharacter(banner, _penMesh);
            }
            if (mm == null || !mm.IsValid) throw new InvalidOperationException("brak siatki " + _penMesh);
            GameEntity e = GameEntity.CreateEmpty(scene, false, false, false);   // bez fizyki, bez skryptow
            if (e == (GameEntity)null) throw new InvalidOperationException("CreateEmpty zwrocil null");
            try
            {
                e.Name = "arm_mappennant_" + SafeName(s.R.Uid) + suffix;   // nie id osady (gra szuka osad po nazwie encji)
                e.AddMultiMesh(mm, true);
                Mat3 rot = Mat3.Identity;
                rot.RotateAboutUp(s.Deg * (float)Math.PI / 180f);
                rot.ApplyScaleLocal(PenScale);
                MatrixFrame f = new MatrixFrame(in rot, new Vec3(x, y, z - PenSink, 1f));
                e.SetFrame(ref f, true);
                e.SetReadyToRender(true);
                e.SetEntityEnvMapVisibility(false);
                e.SetVisibilityExcludeParents(false);
                e.EntityFlags = e.EntityFlags | EntityFlags.DoNotTick;   // tyka tylko pokazany (ShowPennant), jak encja partii gry
            }
            catch
            {
                try { e.Remove(111); } catch { }
                throw;
            }
            return e;
        }

        /// <summary>Miejsce drzewca: naroznik obrysu wioski z pliku (pol dlugosci x pol szerokosci) wokol srodka i obrotu POSTAWIONEGO obrazka
        /// (Cx / Cy / Deg - po przesunieciu na lad / ku wodzie / ze stoku); kolejnosc: przednie (droga / woda), potem tylne; pierwszy na ladzie.
        /// Zaden na ladzie - srodek obrazka (nie pod poziomem morza), zwraca -1. D2: avoid >= 0 (drugi proporczyk, pana) - najpierw drugi przedni naroznik
        /// (avoid ^ 1: drugi koniec ulicy po tej samej stronie), potem pozostale; zaden inny na ladzie - zwraca -1 (bez drugiego).</summary>
        private int PennantSpot(Slot s, int avoid, out float x, out float y, out float z)
        {
            var g = new Geo { Ms = Campaign.Current.MapSceneWrapper, Sea = _seaLevel, Faces = _faceTest != 0 };
            double a = s.Deg * Math.PI / 180.0;
            float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
            float hl = s.R.HalfLen, hw = s.R.HalfWid;
            for (int t = 0; t < 4; t++)
            {
                int k = t;
                if (avoid >= 0)
                {
                    // kolejnosc: drugi przedni (avoid ^ 1), potem pozostale dwa (tylne albo przednie) - bez naroznika pierwszego proporczyka
                    if (t == 3) break;
                    k = t == 0 ? (avoid ^ 1) : ((avoid & 2) ^ 2) + (t - 1);
                }
                float kx = (k & 1) == 0 ? hl : -hl;      // lokalna X = ulica wzdluz drogi
                float ky = k < 2 ? hw : -hw;             // lokalna +Y = front
                float wx = s.Cx + ca * kx - sa * ky, wy = s.Cy + sa * kx + ca * ky;
                if (!g.Land(wx, wy)) continue;
                x = wx; y = wy; z = g.H(wx, wy);
                return k;
            }
            x = s.Cx; y = s.Cy;
            float h = g.H(x, y);
            z = g.Sea > MapVillageData.NoSea * 0.5f ? Math.Max(h, g.Sea) : h;
            return -1;
        }

        /// <summary>Pokazuje PIERWSZY proporczyk (drugi - SyncSecond, limit encji).</summary>
        private void ShowPennant(Slot s)
        {
            PenState p = s.Pen;
            if (p == null || p.E == null || p.Shown) return;
            ShowEntity(p.E);
            p.Shown = true;
            _penShown.Add(s);
        }

        private static void ShowEntity(GameEntity e)
        {
            e.EntityFlags = e.EntityFlags & ~EntityFlags.DoNotTick;   // plotno choragwi tyka tylko na widoku
            e.SetVisibilityExcludeParents(true);   // tylko przy zmianie stanu
            e.CheckResources(true, false);         // scena mapy laduje tylko widoczne (jak obrazek wioski)
        }

        /// <summary>Chowa encje (zostaja w scenie do ponownego pokazania). Liste _penShown porzadkuje wolajacy.</summary>
        private static void HidePennant(Slot s)
        {
            PenState p = s.Pen;
            if (p == null) return;
            p.Shown = false;
            if (p.E != null)
            {
                p.E.SetVisibilityExcludeParents(false);
                p.E.EntityFlags = p.E.EntityFlags | EntityFlags.DoNotTick;
            }
            HideSecond(p);
        }

        /// <summary>Potkniecie jednej wioski: schowana; po PenStrikesMax ta jedna wioska bez proporczyka (encja zdjeta), reszta dalej.</summary>
        private void Stumble(Slot s, string where, Exception e)
        {
            _stPen++;
            PenState p = s.Pen;
            if (p != null)
            {
                p.Strikes++;
                _penShown.Remove(s);
                try { HidePennant(s); } catch { }
                if (p.Strikes >= PenStrikesMax && !p.Failed)
                {
                    p.Failed = true;
                    _penFailed++;
                    if (p.E != null) { try { p.E.Remove(111); } catch { } p.E = null; }
                    if (p.E2 != null) { try { p.E2.Remove(111); } catch { } p.E2 = null; }
                    p.Shown2 = false;
                }
            }
            if (_stPen <= 3 || _stPen % 200 == 0)
                Log.Error("MapVillagesView.Pennant" + where + " " + s.R.Uid + " (potkniecie wioski " + (p != null ? p.Strikes : 0) + "/" + PenStrikesMax + ", razem " + _stPen + ")", e);
        }

        /// <summary>Po ClearGPUMemory (misja): pamiec obiektow Banner od nowa, kazdy postawiony proporczyk do przebudowy przy pokazaniu. Bez ruszania encji.</summary>
        private void PennantsClearMemory()
        {
            try
            {
                _penBanners.Clear();
                PenTexForgetAll();   // sw-herby: ForceClearAllCache gry (zaraz po ClearVisualMemory) zwalnia nasze tekstury natychmiast - ksiega od nowa, pamieci gry nie ruszamy
                int n = 0;
                foreach (var s in _slots)
                {
                    if (s.Pen == null || s.Pen.E == null) continue;
                    s.Pen.Why = PenWhyMission;
                    n++;
                }
                if (n > 0) _penMissions++;
            }
            catch (Exception e)
            {
                _stPen++;
                if (_stPen <= 3 || _stPen % 200 == 0) Log.Error("MapVillagesView.PennantsClearMemory (potkniecie " + _stPen + ")", e);
            }
        }

        /// <summary>Zdejmuje wszystkie proporczyki ze sceny (wylacznik, wylaczone wioski, koniec mapy przy zywej scenie). Zwraca liczbe zdjetych.</summary>
        private int RemovePennants()
        {
            int n = 0;
            PenTexLog(true);   // sw-herby: stan tekstur przed zdjeciem (koniec mapy, wylacznik, zmiana klucza)
            foreach (var s in _slots)
            {
                PenState p = s.Pen;
                if (p == null) continue;
                if (p.E != null)
                {
                    try { p.E.Remove(111); n++; }
                    catch (Exception e)
                    {
                        _stPen++;
                        if (_stPen <= 3) Log.Error("MapVillagesView.RemovePennants " + s.R.Uid + " (potkniecie " + _stPen + ")", e);
                    }
                }
                if (p.E2 != null)
                {
                    try { p.E2.Remove(111); n++; }
                    catch (Exception e)
                    {
                        _stPen++;
                        if (_stPen <= 3) Log.Error("MapVillagesView.RemovePennants " + s.R.Uid + " pan (potkniecie " + _stPen + ")", e);
                    }
                }
                s.Pen = null;
            }
            _penShown.Clear();
            _penBanners.Clear();
            PenTexFreeAll();   // sw-herby: encje zdjete wyzej - tekstury herbow i materialy zwolnione (stara droga: ksiega pusta)
            Array.Clear(_penCand, 0, _penCand.Length);
            return n;
        }

        /// <summary>Scena juz nie ta: same odwolania (cudzej sceny nie ruszamy - jak DropAll).</summary>
        private void DropPennants()
        {
            PenTexLog(true);   // sw-herby: stan tekstur przed zrzuceniem odwolan
            PenTexForgetAll(); // sw-herby: pamiec gry oprozni FlushCache na koniec mapy - tu same odwolania
            foreach (var s in _slots) s.Pen = null;
            _penShown.Clear();
            _penBanners.Clear();
            Array.Clear(_penCand, 0, _penCand.Length);
        }

        /// <summary>Linia logu (co 2 minuty przy zmianie, z podsumowaniem wiosek, i na koniec mapy).</summary>
        private string PennantLine()
        {
            double ms = _penBuildTicks > 0 ? _penBuildTime * 1000.0 / System.Diagnostics.Stopwatch.Frequency / _penBuildTicks : 0;
            int two = 0;
            foreach (var q in _penShown) if (q.Pen != null && q.Pen.Shown2) two++;
            // sw-herby: przy malych teksturach "herbow w pamieci" = ksiega _penTex, limit = cel zwalniania / siatka (stara droga - jak dotad)
            bool tex = _penTexMode == 1;
            int have = tex ? _penTex.Count : _penBanners.Count, haveMax = tex ? _penTexMax : _penCodesMax;
            string lim = !tex ? PenCodesSoft + "/" + PenCodesHard : (_penRelOk ? PenTexKeep + "/" + PenTexHard : PenCodesSoftSmall + "/" + PenCodesHardSmall);
            return "Proporczyki (E7/W5): w kadrze " + _penShown.Count + " wiosek (najwiecej " + _penShownMax + "), z dwoma proporczykami (gospodarz i pan, D2 "
                   + (Settings.Current.VillageTwoPennants ? "tak" : "NIE") + ") " + two + " (najwiecej " + _penTwoMax + "; pokazanych proporczykow najwyzej " + PenEntityMax
                   + ", drugich schowanych w limicie najwiecej " + _penTwoHiddenMax + "), drugich zbudowanych " + _penTwoBuilt
                   + ", wiosek bez drugiego naroznika na ladzie " + _penNoCorner.Count + ", potkniecia drugiego " + _stPen2 + " | budowa sr. "
                   + ms.ToString("0.000", CultureInfo.InvariantCulture) + " ms/klatke (klatek z budowa " + _penBuildTicks + ", zbudowane " + _penBuilt
                   + ", herbow w pamieci " + have + ", najwiecej " + haveMax + ", limit " + lim
                   + ", wiosek z herbem pana zamiast gospodarza " + _penCapped.Count + "), przebudowy po misji " + _penAfterMission + " (misji " + _penMissions
                   + "), zmiany herbu " + _penLord + ", potkniecia " + _stPen + " (wiosek bez proporczyka " + _penFailed + ")"
                   + (Settings.Current.VillagePennants ? "" : " | WYLACZONE w MCM") + ".";
        }

        // ---------- sw-herby (VillagePennantSmallTexture): male tekstury herbow z wlasna ksiega i zwalnianie dalekich ----------

        /// <summary>Droga tekstur na tej mapie (raz, i od nowa przy zmianie klucza - po zdjeciu wszystkich): 1 - male tekstury z ksiega (zwalnianie dalekich,
        /// gdy jest refleksja na pamiec gry), 0 - stara droga GetBannerOfCharacter. Linia startowa w logu.</summary>
        private void PenTexStart(bool small)
        {
            _penTexMode = small ? 1 : 0;
            _penRelOk = small && PenTexResolve();
            string body;
            if (!small)
                body = "VillagePennantSmallTexture NIE | stara droga: MobilePartyVisual.GetBannerOfCharacter gry, tekstura 1024x1024 do najblizszej misji, nowe herby powyzej "
                       + PenCodesSoft + " tylko najblizszym " + PenCodesNear + ", powyzej " + PenCodesHard + " herb pana";
            else
                body = "VillagePennantSmallTexture tak | tekstury " + PenTexSide + "x" + PenTexSide + " (droga GetTableauTextureSmall gry, wlasny klucz " + PenRenderPrefix
                       + "Mesh_BannerTableauSmall:<kod>) | zwalnianie dalekich "
                       + (_penRelOk
                          ? "CZYNNE (powyzej " + PenTexKeep + " herbow w pamieci najdawniej uzyte, najwyzej " + PenEvictPerTick + " na cwierc sekundy, nieuzyte w ticku i starsze niz "
                            + (PenTexMinAge / 4) + " s; siatka bezpieczenstwa " + PenTexHard + " - herb pana)"
                          : "BRAK (" + _penRelWhy + ") - tekstury do najblizszej misji, nowe herby powyzej " + PenCodesSoftSmall + " tylko najblizszym " + PenCodesNear
                            + ", powyzej " + PenCodesHardSmall + " herb pana");
            Log.Info("Proporczyki - tekstury (E7/W5): start | " + body + " | nic do zapisu gry.");
        }

        /// <summary>Refleksja na chronione skladowe pamieci herbow gry (raz na proces): ThumbnailCache&lt;BannerTextureCreationData&gt;._map i
        /// RemoveThumbnailCacheNode(node, releaseTexture) - ta sama sciezka, ktora ThumbnailCache.Tick zwalnia nieuzywane miniatury. Brak = bez zwalniania.</summary>
        private static bool PenTexResolve()
        {
            if (_penRelState != 0) return _penRelState > 0;
            _penRelState = -1;
            try
            {
                Type t = typeof(ThumbnailCache<BannerTextureCreationData>);
                _penMapField = t.GetField("_map", BindingFlags.Instance | BindingFlags.NonPublic);
                _penRemoveNode = t.GetMethod("RemoveThumbnailCacheNode", BindingFlags.Instance | BindingFlags.NonPublic, null,
                                             new[] { typeof(ThumbnailCacheNode), typeof(bool) }, null);
                if (_penMapField == null) _penRelWhy = "brak pola ThumbnailCache._map";
                else if (_penMapField.FieldType != typeof(Dictionary<string, ThumbnailCacheNode>)) _penRelWhy = "inny typ pola _map: " + _penMapField.FieldType.Name;
                else if (_penRemoveNode == null) _penRelWhy = "brak metody ThumbnailCache.RemoveThumbnailCacheNode";
                else _penRelState = 1;
            }
            catch (Exception e) { _penRelWhy = "wyjatek " + e.GetType().Name; }
            if (_penRelState < 0) { _penMapField = null; _penRemoveNode = null; }
            return _penRelState > 0;
        }

        /// <summary>Siatka choragwi z materialami herbu z ksiegi (jak GetBannerOfCharacter gry, ale tekstura 512x512 z kluczem ArmPen_); ustawia _penLastTex.
        /// Nowy herb = jeden wpis ksiegi i jedno zamowienie tekstury na material siatki (te same dla wszystkich wiosek z tym herbem).</summary>
        private MetaMesh PenTexMesh(string code)
        {
            if (ThumbnailCacheManager.Current == null) throw new InvalidOperationException("brak ThumbnailCacheManager");
            PenTex t;
            if (!_penTex.TryGetValue(code, out t))
            {
                t = new PenTex { Code = code, Banner = new TaleWorlds.Core.Banner(code), Born = _penStamp };
                _penTex[code] = t;
                _penTexNew++;
                if (_penTex.Count > _penTexMax) _penTexMax = _penTex.Count;
            }
            t.Stamp = _penStamp;
            MetaMesh mm = MetaMesh.GetCopy(_penMesh, true, false);
            if (mm == null || !mm.IsValid) throw new InvalidOperationException("brak siatki " + _penMesh);
            for (int i = 0; i < mm.MeshCount; i++)
            {
                Mesh mesh = mm.GetMeshAtIndex(i);
                if (mesh.HasTag("dont_use_tableau")) continue;
                mesh.SetMaterial(PenTexMaterial(t, mesh.GetMaterial()));
            }
            _penLastTex = t;
            return mm;
        }

        /// <summary>Kopia materialu siatki z tekstura herbu (raz na herb i material). Zamowienie: PenTexData -> ThumbnailCacheManager -> BannerPersistentTextureCache
        /// (nowa tekstura 512x512 renderowana przez gre; setAction od razu, jak w GetBannerOfCharacter; drugi material tego herbu dostaje te sama teksture).</summary>
        private Material PenTexMaterial(PenTex t, Material baseMat)
        {
            for (int i = 0; i < t.Mats.Count; i++)
                if (t.Mats[i].Base == baseMat) return t.Mats[i].Copy;
            Material copy = baseMat.CreateCopy();
            Action<Texture> set = delegate (Texture tex)
            {
                // jak GetBannerOfCharacter gry: tekstura herbu w slot 1 i flaga mieszania tableau
                copy.SetTexture(Material.MBTextureType.DiffuseMap2, tex);   // (MBTextureType)1 jak w grze
                uint mask = (uint)copy.GetShader().GetMaterialShaderFlagMask("use_tableau_blending", true);
                ulong flags = copy.GetShaderFlags();
                copy.SetShaderFlags(flags | mask);
            };
            t.Mats.Add(new PenMat { Base = baseMat, Copy = copy });
            var data = new PenTexData(t.Banner, set);
            TextureCreationInfo info = ThumbnailCacheManager.Current.CreateTexture(data);
            t.RenderId = data.RenderId;
            if (info.CreatedNewTexture) { _penTexReq++; t.Tex = info.Texture; }
            else if (info.UsingExistingTexture) { _penTexShared++; if (t.Tex == null) t.Tex = info.Texture; }
            else _penTexRefused++;   // zadna pamiec gry nie przyjela zamowienia - proporczyk bez herbu (licznik)
            return copy;
        }

        /// <summary>Herby uzyte w tym ticku (pierwszy proporczyk kazdej wybranej wioski, pokazany drugi); ponad PenTexKeep herbow w pamieci - najdawniej uzyte
        /// (nieuzyte w tym ticku, starsze niz PenTexMinAge), najwyzej PenEvictPerTick: encje ich proporczykow zdjete raz (sa daleko i schowane - wroca przy kamerze),
        /// potem tekstura i materialy zwolnione (PenTexFree). Jeden przebieg po wioskach tylko w ticku ze zwalnianiem.</summary>
        private void PenTexTouchAndEvict(int n)
        {
            for (int i = 0; i < n; i++)
            {
                PenState q = _penCand[i].Pen;
                if (q == null) continue;
                if (q.Tex != null && q.E != null) q.Tex.Stamp = _penStamp;
                if (q.Tex2 != null && q.E2 != null && q.Shown2) q.Tex2.Stamp = _penStamp;
            }
            if (!_penRelOk || _penTex.Count <= PenTexKeep) return;
            _penEvictBuf.Clear();
            foreach (var t in _penTex.Values)
                if (t.Stamp != _penStamp && t.Born + PenTexMinAge <= _penStamp) _penEvictBuf.Add(t);
            int k = Math.Min(Math.Min(_penTex.Count - PenTexKeep, PenEvictPerTick), _penEvictBuf.Count);
            if (k <= 0) { _penEvictBuf.Clear(); return; }
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                _penEvictBuf.Sort(PenTexOlder);   // najdawniej uzyte najpierw
                for (int i = 0; i < k; i++) _penEvictBuf[i].Evict = true;
                foreach (var s in _slots)
                {
                    PenState p = s.Pen;
                    if (p == null) continue;
                    try
                    {
                        bool sel = p.Stamp == _penStamp;   // wioska wybrana w tym ticku - jej proporczykow nie zdejmujemy (herb wtedy zostaje)
                        if (p.E != null && p.Tex != null && p.Tex.Evict)
                        {
                            if (sel) { p.Tex.Evict = false; _penTexBlocked++; continue; }
                            if (p.Shown) { _penShown.Remove(s); p.Shown = false; }
                            if (p.E2 != null) _penTexKilled++;
                            DropSecond(p);                 // drugi wisi na pierwszym (SyncSecond postawi go po przebudowie pierwszego)
                            GameEntity old = p.E;
                            p.E = null;
                            p.Code = "";
                            p.Tex = null;
                            p.Capped = false;
                            p.Why = PenWhyFar;             // przebudowa przy nastepnym wyborze (budzet budowy jak dotad)
                            old.Remove(111);
                            _penTexKilled++;
                        }
                        else if (p.E2 != null && p.Tex2 != null && p.Tex2.Evict)
                        {
                            if (sel && p.Shown2) { p.Tex2.Evict = false; _penTexBlocked++; continue; }
                            DropSecond(p);
                            _penTexKilled++;
                        }
                    }
                    catch (Exception e)
                    {
                        _stPenTex++;
                        // po recenzji sw-herby: wywrotka w polowie zdejmowania (np. Remove drugiej encji w DropSecond) - encja, ktora dalej stoi, zatrzymuje swoj herb
                        // (bez zwalniania tekstury i materialow pod zywa encja: ksiega zgodna z tym, co naprawde jest w scenie; herb wypadnie w kolejnym przebiegu)
                        if (p.E != null && p.Tex != null && p.Tex.Evict) { p.Tex.Evict = false; _penTexBlocked++; }
                        if (p.E2 != null && p.Tex2 != null && p.Tex2.Evict) { p.Tex2.Evict = false; _penTexBlocked++; }
                        if (_stPenTex <= 3 || _stPenTex % 200 == 0) Log.Error("MapVillagesView.PennantTexEvict " + s.R.Uid + " (potkniecie " + _stPenTex + ")", e);
                    }
                }
                for (int i = 0; i < k; i++)
                {
                    PenTex t = _penEvictBuf[i];
                    if (!t.Evict) continue;
                    t.Evict = false;
                    PenTexFree(t);
                }
                _penTexEvictRuns++;
            }
            finally
            {
                for (int i = 0; i < _penEvictBuf.Count; i++) _penEvictBuf[i].Evict = false;
                _penEvictBuf.Clear();
                _penTexEvictTime += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            }
        }

        /// <summary>Zwalnia jeden herb: wpis z ksiegi, wezel pamieci gry z kluczem ArmPen_ (RemoveThumbnailCacheNode: anuluje zamowienie, Texture.Release, Count--),
        /// nasze kopie materialow (ManualInvalidate - ich encje juz zdjete). Potkniecie liczone, herb i tak wypada z ksiegi.</summary>
        private void PenTexFree(PenTex t)
        {
            _penTex.Remove(t.Code);
            t.Released = true;
            bool node = false;
            try
            {
                BannerPersistentTextureCache cache = BannerPersistentTextureCache.Current;
                if (cache != null && t.RenderId != null && _penMapField != null && _penRemoveNode != null)
                {
                    var map = _penMapField.GetValue(cache) as Dictionary<string, ThumbnailCacheNode>;
                    ThumbnailCacheNode nd;
                    if (map != null && map.TryGetValue(t.RenderId, out nd) && nd != null)
                    {
                        _penRemoveNode.Invoke(cache, new object[] { nd, true });
                        node = true;
                    }
                }
            }
            catch (Exception e)
            {
                _stPenTex++;
                if (_stPenTex <= 3 || _stPenTex % 200 == 0) Log.Error("MapVillagesView.PennantTexFree (potkniecie " + _stPenTex + ")", e);
            }
            _penTexFreed++;
            if (!node) _penTexNoNode++;
            for (int i = 0; i < t.Mats.Count; i++)
            {
                try { t.Mats[i].Copy.ManualInvalidate(); } catch { }
            }
            t.Mats.Clear();
            t.Tex = null;
        }

        /// <summary>Wszystkie herby zwolnione (po zdjeciu wszystkich encji - RemovePennants).</summary>
        private void PenTexFreeAll()
        {
            if (_penTex.Count == 0) return;
            var all = new List<PenTex>(_penTex.Values);
            for (int i = 0; i < all.Count; i++) PenTexFree(all[i]);
        }

        /// <summary>Ksiega herbow od nowa bez ruszania pamieci gry (misja: ForceClearAllCache gry zwalnia tekstury zaraz po nas; cudza scena: FlushCache
        /// na koniec mapy). Encje zostaja z dotychczasowymi materialami do przebudowy (jak stara droga).</summary>
        private void PenTexForgetAll()
        {
            if (_penTex.Count == 0) return;
            foreach (var t in _penTex.Values) { t.Released = true; t.Mats.Clear(); t.Tex = null; }
            _penTexForgot += _penTex.Count;
            _penTex.Clear();
        }

        /// <summary>Linia tekstur w logu: co 2 minuty, gdy sie zmienila (final = false), i zawsze przy zdjeciu / zrzuceniu (final = true). Tylko gdy droga ustalona.</summary>
        private void PenTexLog(bool final)
        {
            if (_penTexMode < 0) return;
            try
            {
                string body = PenTexBody();
                if (!final && body == _penTexLastLine) return;
                _penTexLastLine = body;
                Log.Info("Proporczyki - tekstury (E7/W5): " + (final ? "przy zdjeciu" : "stan") + " | " + body);
            }
            catch (Exception e)
            {
                _stPenTex++;
                if (_stPenTex <= 3 || _stPenTex % 200 == 0) Log.Error("MapVillagesView.PennantTexLog (potkniecie " + _stPenTex + ")", e);
            }
        }

        /// <summary>Tresc linii tekstur (liczby dla grupy nherby sprawdz_logi). Tozsamosc ksiegi: nowe - zwolnione - zapomniane - w pamieci = 0; pamiec gry:
        /// klucze ArmPen_ w BannerPersistentTextureCache = w pamieci ksiegi (roznica 0 - zwalnianie naprawde oddaje wezly).</summary>
        private string PenTexBody()
        {
            var inv = CultureInfo.InvariantCulture;
            int live = 0, cappedNow = 0;
            foreach (var s in _slots)
            {
                PenState p = s.Pen;
                if (p == null) continue;
                if (p.E != null) live++;
                if (p.E2 != null) live++;
            }
            foreach (var s in _penShown) if (s.Pen != null && s.Pen.Capped) cappedNow++;
            if (live > _penTexLiveMax) _penTexLiveMax = live;
            if (_penTexMode != 1)
                return "VillagePennantSmallTexture NIE | rozmiar 1024x1024 (stara droga, do misji) | herbow w pamieci " + _penBanners.Count + " (najwiecej " + _penCodesMax
                       + ", limit " + PenCodesSoft + "/" + PenCodesHard + ") | encje: zywe " + live + " (najwiecej " + _penTexLiveMax + ") | herb zastepczy (pana zamiast gospodarza): wiosek "
                       + _penCapped.Count + ", teraz w kadrze " + cappedNow + " | potkniecia " + _stPenTex + ".";
            // probka rozmiaru tekstury z gry (raz, z herbu w pamieci)
            if (_penTexSampleW == 0)
            {
                foreach (var t in _penTex.Values)
                {
                    if (t.Tex == null || t.Tex.IsReleased) continue;
                    try
                    {
                        int w = t.Tex.Width, h = t.Tex.Height, b = t.Tex.MemorySize;
                        if (w > 0 && h > 0) { _penTexSampleW = w; _penTexSampleH = h; _penTexSampleKiB = b > 0 ? b / 1024 : 0; }
                    }
                    catch { }
                    break;
                }
            }
            int keys = -1, all = -1;
            try
            {
                BannerPersistentTextureCache cache = BannerPersistentTextureCache.Current;
                if (cache != null)
                {
                    all = cache.Count;
                    var map = _penMapField != null ? _penMapField.GetValue(cache) as Dictionary<string, ThumbnailCacheNode> : null;
                    if (map != null)
                    {
                        keys = 0;
                        foreach (var key in map.Keys) if (key != null && key.StartsWith(PenRenderPrefix, StringComparison.Ordinal)) keys++;
                    }
                }
            }
            catch { keys = -1; }
            double perTex = _penTexSampleKiB > 0 ? _penTexSampleKiB / 1024.0 : PenTexSide * (double)PenTexSide * 4.0 / (1024.0 * 1024.0);
            double mib = (keys >= 0 ? keys : _penTex.Count) * perTex;
            if (mib > _penTexMiBMax) _penTexMiBMax = mib;
            int diff = _penTexNew - _penTexFreed - _penTexForgot - _penTex.Count;
            double ems = _penTexEvictRuns > 0 ? _penTexEvictTime * 1000.0 / System.Diagnostics.Stopwatch.Frequency / _penTexEvictRuns : 0;
            return "VillagePennantSmallTexture tak | rozmiar " + PenTexSide + "x" + PenTexSide + " (gra: "
                   + (_penTexSampleW > 0 ? _penTexSampleW + "x" + _penTexSampleH + ", " + _penTexSampleKiB + " KiB" : "nie odczytano") + ")"
                   + " | herby: nowe " + _penTexNew + ", tekstury zamowione " + _penTexReq + " (wspolne " + _penTexShared + ", nieprzyjete " + _penTexRefused + "), zwolnione "
                   + _penTexFreed + " (bez klucza w pamieci gry " + _penTexNoNode + "), zapomniane po misji " + _penTexForgot + ", w pamieci " + _penTex.Count + " (najwiecej "
                   + _penTexMax + ", cel " + PenTexKeep + ", siatka " + PenTexHard + "), tozsamosc nowe - zwolnione - zapomniane - w pamieci = " + diff
                   + " | pamiec gry: klucze " + PenRenderPrefix + " " + (keys >= 0 ? keys.ToString(inv) : "nie odczytano")
                   + " (roznica z ksiega " + (keys >= 0 ? (keys - _penTex.Count).ToString(inv) : "?") + "), herbow gry razem " + all + " (pojemnosc 1000), ok. "
                   + mib.ToString("0.0", inv) + " MiB (najwiecej " + _penTexMiBMax.ToString("0.0", inv) + " MiB)"
                   + " | encje: zywe " + live + " (najwiecej " + _penTexLiveMax + "), zdjete przy zwalnianiu " + _penTexKilled + ", przebudowane po zwolnieniu " + _penTexRebuilt
                   + ", zablokowane " + _penTexBlocked
                   + " | herb zastepczy (pana zamiast gospodarza): wiosek " + _penCapped.Count + ", teraz w kadrze " + cappedNow
                   + " | zwalnianie " + (_penRelOk ? "CZYNNE" : "BRAK (" + _penRelWhy + ")") + ", przebiegow " + _penTexEvictRuns + ", sr. " + ems.ToString("0.000", inv) + " ms"
                   + " | potkniecia " + _stPenTex + ".";
        }
    }
}
