// E7/W5 (238, PLAN 7.9 c, Jeff 08.10 "wszystkie 8"; projekt etapu 7 v2 rozdz. 3 E7/W5, R17; WL-P rozdz. 2): proporczyk pana nad wioskami z bliska.
// Osobna encja w scenie mapy przy przednim narozniku obrysu wioski (lokalna +Y = front: droga / woda; naroznik musi stac na ladzie wedlug tej
// samej reguly co budynki obrazka - Geo.Land; zaden na ladzie = srodek obrazka), podstawa na gruncie z terenu, skala 0.32 (namiot gry 0.15-0.5).
// Siatka = choragiew, ktora gra stawia przy partiach w zamku i przy namiotach oblezenia: MobilePartyVisual.GetBannerOfCharacter(banner,
// "campaign_flag") (SandBox.View 1.4.8: kopia siatki + material z tekstura herbu z MapScreen.CharacterBannerMaterialCache; klucz = (material,
// OBIEKT Banner), Banner nie nadpisuje Equals - wiec jeden WLASNY obiekt Banner na kod herbu = jedna tekstura dla wszystkich wiosek jednego pana).
// Pan = rod wlasciciela wsi gry (Village.Bound.OwnerClan, Clan.Banner - dla rodu panujacego herb krolestwa), ten sam co dymek W2 i HeldLine.
// Bez fizyki i skryptow (nie lapie klikniec; dymek wioski szuka po obrysie, nie po encji). Tylko z bliska: kamera z <= 90, najblizsze 80 wiosek
// z postawionym obrazkiem (od punktu, na ktory patrzy kamera), budowa najwyzej 12 na cwierc sekundy; dalej schowane, nie usuwane.
// Raz na sekunde dla pokazanych porownanie KODU herbu (gra zmienia barwy herbu w miejscu, ta sama referencja; kazda zmiana zeruje
// Banner._bannerCode) - inny kod = przebudowa tej jednej flagi (zdobycie zamku, zmiana krolestwa, zmiana rodu panujacego bez nasluchow).
// Po misji: MapScreen.ClearGPUMemory -> SandBoxViewVisualManager.ClearVisualMemory -> nasze ClearVisualMemory (Texture.ReleaseGpuMemories
// zwalnia tekstury herbow) - pamiec obiektow Banner od nowa (nowy obiekt = nowa tekstura) i kazdy proporczyk przebudowany przy nastepnym
// pokazaniu; samo ClearVisualMemory nie rusza encji (moze przyjsc, gdy mapa nie jest na ekranie).
// Nic co klatke (praca w ticku widoku 4 razy na sekunde, encje tylko przy zmianie stanu - CLAUDE.md 7), nic do zapisu gry, wylacznik
// VillagePennants. Potkniecia liczone per wioska (3 = ta jedna wioska bez proporczyka do nastepnego wczytania), nigdy wylacznik calosci.
using System;
using System.Collections.Generic;
using System.Globalization;
using SandBox.View.Map;
using SandBox.View.Map.Visuals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace Armoury
{
    public sealed partial class MapVillagesView
    {
        private const float PenCameraZ = 90f;         // proporczyki tylko przy kamerze z <= 90 (WL-P 2)
        private const int PenMax = 80;                // najblizsze 80 wiosek z obrazkiem
        private const int PenBuildPerTick = 12;       // budowa / przebudowa najwyzej 12 na cwierc sekundy
        private const float PenCheckEvery = 1f;       // porownanie kodu herbu pokazanych raz na sekunde
        private const float PenScale = 0.32f;         // skala choragwi (namiot oblezenia gry 0.15-0.5)
        private const float PenSink = 0.02f;          // drzewce lekko w gruncie (bez szpary na stoku)
        private const int PenStrikesMax = 3;          // potkniecia jednej wioski, po ktorych ta JEDNA wioska jest bez proporczyka
        private const int PenWhyNone = 0, PenWhyMission = 1, PenWhyLord = 2;   // powod przebudowy
        private static readonly string[] PenMeshes = { "campaign_flag", "map_banner" };   // zapas bez plotna (WL-P 2 [AUTOTEST])

        /// <summary>Stan proporczyka jednej wioski (nic do zapisu gry).</summary>
        private sealed class PenState
        {
            public GameEntity E;              // encja w scenie mapy (null = nie zbudowany albo zdjety)
            public string Code = "";          // kod herbu, z ktorym zbudowano
            public bool Shown, Failed;
            public int Why;                   // PenWhy*: do przebudowy (po misji / inny herb)
            public int Strikes;
            public int Stamp;                 // numer ticku, w ktorym wioska byla wsrod wybranych
        }

        private readonly List<Slot> _penShown = new List<Slot>();
        private readonly Dictionary<string, TaleWorlds.Core.Banner> _penBanners = new Dictionary<string, TaleWorlds.Core.Banner>(StringComparer.Ordinal);   // kod -> wlasny obiekt (do misji)
        private Slot[] _penCand = new Slot[256];      // bufor wyboru (bez alokacji w ticku)
        private float[] _penDist = new float[256];
        private string _penMesh;                      // null = nie sprawdzone na tej mapie; "" = brak siatki
        private bool _penOff;
        private float _penCheckAcc;
        private int _penStamp, _penShownMax, _penBuilt, _penBuildTicks, _penAfterMission, _penMissions, _penLord, _penFailed, _stPen;
        private long _penBuildTime;
        private string _penLastLine = "";

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
                // 2) wybrane: pokaz / zbuduj / przebuduj
                int built = 0;
                long t0 = 0;
                for (int i = 0; i < n; i++)
                {
                    Slot s = _penCand[i];
                    PenState p = s.Pen;
                    if (p.Shown && p.Why == PenWhyNone && !check) continue;
                    try
                    {
                        string code = LordCode(s.D);
                        if (code.Length == 0)
                        {
                            // wies bez pana (rzadkie): bez proporczyka; wroci przy pierwszym ticku z panem
                            if (p.Shown) { _penShown.Remove(s); HidePennant(s); }
                            continue;
                        }
                        if (p.E != null && p.Why == PenWhyNone && !string.Equals(code, p.Code, StringComparison.Ordinal)) p.Why = PenWhyLord;
                        if (p.E != null && p.Why == PenWhyNone)
                        {
                            if (!p.Shown) ShowPennant(s);
                            continue;
                        }
                        if (built >= PenBuildPerTick) continue;   // reszta w nastepnym ticku (Why zostaje)
                        if (built == 0) t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                        built++;
                        BuildPennant(s, code);
                    }
                    catch (Exception e) { Stumble(s, "Build", e); }
                }
                if (built > 0)
                {
                    _penBuildTicks++;
                    _penBuilt += built;
                    _penBuildTime += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
                }
                if (_penShown.Count > _penShownMax) _penShownMax = _penShown.Count;
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
            if (m > PenMax) Array.Sort(_penDist, _penCand, 0, m);
            int n = Math.Min(m, PenMax);
            for (int i = 0; i < n; i++)
            {
                Slot s = _penCand[i];
                if (s.Pen == null) s.Pen = new PenState();
                s.Pen.Stamp = _penStamp;
            }
            return n;
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
                     + " | nic do zapisu gry.");
        }

        /// <summary>Stawia (albo stawia od nowa) proporczyk jednej wioski i go pokazuje. Stara encja zdejmowana przed nowa.</summary>
        private void BuildPennant(Slot s, string code)
        {
            PenState p = s.Pen;
            int why = p.Why;
            if (p.E != null)
            {
                GameEntity old = p.E;
                p.E = null;
                if (p.Shown) { _penShown.Remove(s); p.Shown = false; }
                old.Remove(111);
            }
            Scene scene = MapScene;
            if (scene == null) throw new InvalidOperationException("brak sceny mapy");
            TaleWorlds.Core.Banner banner;
            if (!_penBanners.TryGetValue(code, out banner))
            {
                banner = new TaleWorlds.Core.Banner(code);   // wlasna kopia: klucz pamieci materialow gry (po misji nowy obiekt = nowa tekstura)
                _penBanners[code] = banner;
            }
            MetaMesh mm = MobilePartyVisual.GetBannerOfCharacter(banner, _penMesh);
            if (mm == null || !mm.IsValid) throw new InvalidOperationException("brak siatki " + _penMesh);
            float x, y, z;
            PennantSpot(s, out x, out y, out z);
            GameEntity e = GameEntity.CreateEmpty(scene, false, false, false);   // bez fizyki, bez skryptow
            if (e == (GameEntity)null) throw new InvalidOperationException("CreateEmpty zwrocil null");
            try
            {
                e.Name = "arm_mappennant_" + SafeName(s.R.Uid);   // nie id osady (gra szuka osad po nazwie encji)
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
            p.E = e;
            p.Code = code;
            p.Why = PenWhyNone;
            if (why == PenWhyMission) _penAfterMission++;
            else if (why == PenWhyLord) _penLord++;
            ShowPennant(s);
        }

        /// <summary>Miejsce drzewca: naroznik obrysu wioski z pliku (pol dlugosci x pol szerokosci) wokol srodka i obrotu POSTAWIONEGO obrazka
        /// (Cx / Cy / Deg - po przesunieciu na lad / ku wodzie / ze stoku); kolejnosc: przednie (droga / woda), potem tylne; pierwszy na ladzie.
        /// Zaden na ladzie - srodek obrazka (nie pod poziomem morza).</summary>
        private void PennantSpot(Slot s, out float x, out float y, out float z)
        {
            var g = new Geo { Ms = Campaign.Current.MapSceneWrapper, Sea = _seaLevel, Faces = _faceTest != 0 };
            double a = s.Deg * Math.PI / 180.0;
            float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
            float hl = s.R.HalfLen, hw = s.R.HalfWid;
            for (int k = 0; k < 4; k++)
            {
                float kx = (k & 1) == 0 ? hl : -hl;      // lokalna X = ulica wzdluz drogi
                float ky = k < 2 ? hw : -hw;             // lokalna +Y = front
                float wx = s.Cx + ca * kx - sa * ky, wy = s.Cy + sa * kx + ca * ky;
                if (!g.Land(wx, wy)) continue;
                x = wx; y = wy; z = g.H(wx, wy);
                return;
            }
            x = s.Cx; y = s.Cy;
            float h = g.H(x, y);
            z = g.Sea > MapVillageData.NoSea * 0.5f ? Math.Max(h, g.Sea) : h;
        }

        private void ShowPennant(Slot s)
        {
            PenState p = s.Pen;
            if (p == null || p.E == null || p.Shown) return;
            p.E.EntityFlags = p.E.EntityFlags & ~EntityFlags.DoNotTick;   // plotno choragwi tyka tylko na widoku
            p.E.SetVisibilityExcludeParents(true);   // tylko przy zmianie stanu
            p.E.CheckResources(true, false);         // scena mapy laduje tylko widoczne (jak obrazek wioski)
            p.Shown = true;
            _penShown.Add(s);
        }

        /// <summary>Chowa encje (zostaje w scenie do ponownego pokazania). Liste _penShown porzadkuje wolajacy.</summary>
        private static void HidePennant(Slot s)
        {
            PenState p = s.Pen;
            if (p == null) return;
            p.Shown = false;
            if (p.E == null) return;
            p.E.SetVisibilityExcludeParents(false);
            p.E.EntityFlags = p.E.EntityFlags | EntityFlags.DoNotTick;
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
                s.Pen = null;
            }
            _penShown.Clear();
            _penBanners.Clear();
            Array.Clear(_penCand, 0, _penCand.Length);
            return n;
        }

        /// <summary>Scena juz nie ta: same odwolania (cudzej sceny nie ruszamy - jak DropAll).</summary>
        private void DropPennants()
        {
            foreach (var s in _slots) s.Pen = null;
            _penShown.Clear();
            _penBanners.Clear();
            Array.Clear(_penCand, 0, _penCand.Length);
        }

        /// <summary>Linia logu (co 2 minuty przy zmianie, z podsumowaniem wiosek, i na koniec mapy).</summary>
        private string PennantLine()
        {
            double ms = _penBuildTicks > 0 ? _penBuildTime * 1000.0 / System.Diagnostics.Stopwatch.Frequency / _penBuildTicks : 0;
            return "Proporczyki (E7/W5): w kadrze " + _penShown.Count + " (najwiecej " + _penShownMax + "), budowa sr. "
                   + ms.ToString("0.000", CultureInfo.InvariantCulture) + " ms/klatke (klatek z budowa " + _penBuildTicks + ", zbudowane " + _penBuilt
                   + ", herbow w pamieci " + _penBanners.Count + "), przebudowy po misji " + _penAfterMission + " (misji " + _penMissions
                   + "), zmiany herbu " + _penLord + ", potkniecia " + _stPen + " (wiosek bez proporczyka " + _penFailed + ")"
                   + (Settings.Current.VillagePennants ? "" : " | WYLACZONE w MCM") + ".";
        }
    }
}
