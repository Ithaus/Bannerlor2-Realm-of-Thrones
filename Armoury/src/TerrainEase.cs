using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// KARY TERENU PO NASZEMU (Jeff 02.09: "za duze kary mam w lesie -1.45,
    /// a mialo byc -0.25; pustynia -0.5, snieg -0.5"). Vanilla liczy teren
    /// PROCENTEM od bazy (las -30%, brod/rzeka -30%, snieg -10%, pustynia -10%,
    /// noc -25%), wiec przy bazie ~4.8 las zabiera 1.45 - a Jeff chce liczb
    /// BEZWZGLEDNYCH, w jednostkach mapy. Postfix na kazdy CalculateFinalSpeed:
    /// liczymy teren TAK SAMO jak vanilla (typ pola nawigacji, pogoda, noc),
    /// ODDAJEMY caly vanillowy procent (AddFactor +0.3 itd., wpis "(vanilla
    /// undone)") i dokladamy nasza plaska kare (Add -0.25, wpis "Forest").
    /// Liczymy sami, nie z rozpiski (GetLines) - gra liczy ruch BEZ opisow
    /// i rozpiska poza tooltipem jest pusta (pierwsza wersja 02.09 rano
    /// dzialala tylko w dymku). E7/T (225, PLAN 7.4 - dawny zapis "swiadomie na plus" to byl BLAD:
    /// Polnoc i 7 kultur ROT z cecha lasu jechaly w lesie o ok. 12% SZYBCIEJ niz po rowninie, Dorne na
    /// pustyni o ok. 5%): przy TerrainEaseExact oddajemy DOKLADNIE to, co gra nalozyla netto (ForestKin
    /// zwiadowcy przy >= 75% pieszych, cecha lasu, cecha pustyni - te same warunki co gra) i kladziemy
    /// plaska kare Jeffa w tym samym ulamku, ktory gra zostawila (cecha zmniejsza kare, nigdy nie daje
    /// premii); DesertBorn, Pathfinder, NightRunner, DayTraveler - osobne premie gry, zostaja. Biegnie
    /// PRZED sufitem kolumny (MarchPace ma Priority.Last), czapka trzyma.
    /// Kara 0 w MCM = teren tego typu w ogole nie spowalnia.
    /// AUDYT (Jeff: "zaudytuj poruszanie sie"): raz na dzien gry pelna rozpiska
    /// predkosci glownej partii do logu - ale z DZIENNEGO TICKU (DailyAudit nizej),
    /// nie stad. Wolany z postfixa klamal przez dwa tygodnie, patrz komentarz tam.
    /// </summary>
    internal static class TerrainEase
    {
        // NAZWY JAK U VANILLI (Jeff 02.09: "po co tyle pozycji"). ZASTRZEZENIE
        // z 13.09: ExplainedNumber scala wpisy tylko wtedy, gdy zgadza sie NAZWA
        // *I* RODZAJ dzialania - a my cofamy procentem (AddFactor) i dokladamy
        // plasko (Add), wiec te dwa wpisy nigdy sie nie zleja. W dymku beda DWIE
        // pozycje "Forest": zwrot ~+0% i nasza plaska kara. To poprawne
        // i jest DOWODEM, ze zamiana zadziala - jedna pozycja "Forest -1.08"
        // oznaczalaby, ze nasz postfix w ogole nie wszedl.
        private static readonly TextObject _uForest = new TextObject("{=!}Forest");
        private static readonly TextObject _uFord = new TextObject("{=!}Fording");
        private static readonly TextObject _uDesert = new TextObject("{=!}Desert");
        private static readonly TextObject _uSnow = new TextObject("{=!}Snow");
        private static readonly TextObject _uNight = new TextObject("{=!}Night");
        private static readonly TextObject _aForest = new TextObject("{=!}Forest");
        private static readonly TextObject _aFord = new TextObject("{=!}Fording");
        private static readonly TextObject _aDesert = new TextObject("{=!}Desert");
        private static readonly TextObject _aSnow = new TextObject("{=!}Snow");
        private static readonly TextObject _aNight = new TextObject("{=!}Night");
        private static readonly TextObject _aSwamp = new TextObject("{=!}Swamp");   // vanilla nie karze bagna - nie ma _uSwamp, bo nie ma czego cofac
        private static int _lastAuditDay = -1;
        private const float GameForestFull = 0.3f;   // G DefaultPartySpeedCalculatingModel MovingAtForestEffect (stala prywatna gry)
        private const float GameDesertFull = 0.1f;   // G MovingInDesertEffect

        private static void Swap(ref ExplainedNumber r, float vanillaFactor, TextObject undo, float ours, TextObject name)
        {
            r.AddFactor(vanillaFactor, undo);                 // oddaj procent vanilla
            if (ours > 0.0005f) r.Add(-ours, name);           // nasza plaska kara
        }

        public static void SpeedPostfix(MobileParty __0, ref ExplainedNumber __result)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.TerrainEaseEnabled) return;
                var mp = __0;
                if (mp == null || Campaign.Current == null) return;
                // LANCUCH MODELI (ROT -> RealisticBannerlord -> vanilla): latamy kazdy,
                // wiec bez tego bezpiecznika kara szla 2-3 razy ("Night -1" u Jeffa)
                if (!SpeedDepth.OutermostFinal) return;

                bool atSea = mp.IsCurrentlyAtSea;
                TerrainType tt = TerrainType.Plain;
                try { tt = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(mp.CurrentNavigationFace); } catch { }
                bool forest = tt == TerrainType.Forest, desert = tt == TerrainType.Desert || tt == TerrainType.Dune;

                // E7/T probka z rozpiski gry (tylko w wywolaniu z Probe, watek glowny, ta jedna partia): wpisy GRY dla terenu
                // PRZED naszym zwrotem, potem to, co nasz zwrot i nasza kara naprawde zmienily w liczbie
                bool probe = _probeOn && !_pGot && mp == _probeParty && (forest || desert);
                float sf0 = 0f, bn0 = 0f;
                if (probe) { sf0 = __result.SumOfFactors; bn0 = __result.BaseNumber; _pGame = GameLinesFactor(ref __result, forest); }
                float scoutEase = 0f;   // R3-10a (NightScout): czesc kary lasu i nocy zdjeta przez zwiadowce - jedna pozycja "Scout" na koncu

                if (forest)
                {
                    if (c.TerrainEaseExact)
                    {
                        // E7/T: zwrot = netto gry (przy cesze i ForestKin razem 0), nasza kara w ulamku, ktory gra zostawila
                        bool kin, feat;
                        float net = GameForestNet(mp, out kin, out feat);
                        float fp = c.ForestSpeedPenalty * Math.Max(0f, net) / GameForestFull;
                        Swap(ref __result, net, _uForest, fp, _aForest);
                        scoutEase += NightScout.Ease(mp, fp);
                    }
                    else { Swap(ref __result, GameForestFull, _uForest, c.ForestSpeedPenalty, _aForest); scoutEase += NightScout.Ease(mp, c.ForestSpeedPenalty); }
                }
                else if (!atSea && (tt == TerrainType.Water || tt == TerrainType.River || tt == TerrainType.UnderBridge
                                    || tt == TerrainType.Bridge || tt == TerrainType.Fording))
                    Swap(ref __result, 0.3f, _uFord, c.FordSpeedPenalty, _aFord);
                else if (desert)
                {
                    if (c.TerrainEaseExact)
                    {
                        // E7/T: cecha pustyni - gra nic nie naklada, wiec ani zwrotu, ani naszej kary ("No speed penalty on desert")
                        bool feat;
                        float net = GameDesertNet(mp, out feat);
                        Swap(ref __result, net, _uDesert, c.DesertSpeedPenalty * net / GameDesertFull, _aDesert);
                    }
                    else Swap(ref __result, GameDesertFull, _uDesert, c.DesertSpeedPenalty, _aDesert);
                }
                // BAGNO (Jeff 13.09: "bagna 0.3, jesli sa bagna"). Vanilla nie liczy
                // bagnu ZADNEJ kary - w DefaultPartySpeedCalculatingModel branch terenu
                // obsluguje tylko Forest, brody i Desert/Dune, reszta nie dostaje nic.
                // Nie ma wiec czego zwracac: sam Add, bez Swap.
                else if (tt == TerrainType.Swamp)
                {
                    if (c.SwampSpeedPenalty > 0.0005f) __result.Add(-c.SwampSpeedPenalty, _aSwamp);
                }
                if (probe)
                {
                    _pRefund = __result.SumOfFactors - sf0;   // nasz zwrot (ulamek) - zmiana sumy wspolczynnikow
                    _pFlat = bn0 - __result.BaseNumber;       // nasza plaska kara (jedn.) - zmiana bazy
                    _pBase = bn0; _pForest = forest; _pGot = true;
                }

                try
                {
                    var w = Campaign.Current.Models.MapWeatherModel.GetWeatherEventInPosition(mp.Position.ToVec2());
                    if (w == MapWeatherModel.WeatherEvent.Snowy || w == MapWeatherModel.WeatherEvent.Blizzard)
                        Swap(ref __result, 0.1f, _uSnow, c.SnowSpeedPenalty, _aSnow);
                }
                catch { }

                if (!atSea && Campaign.Current.IsNight)
                {
                    Swap(ref __result, 0.25f, _uNight, c.NightSpeedPenalty, _aNight);
                    scoutEase += NightScout.Ease(mp, c.NightSpeedPenalty);
                }
                if (scoutEase > 0.0005f) __result.Add(scoutEase, NightScout.SpeedLabel);   // R3-10a: przed sufitem kolumny (MarchPace Last)

            }
            catch { }
        }

        /// <summary>
        /// AUDYT PREDKOSCI - RAZ DZIENNIE, Z DZIENNEGO TICKU (nie z postfixa!).
        /// Jeff 13.09: "caly czas zle jest pokazywana predkosc marszu" - i mial
        /// racje, tyle ze klamal POMIAR, nie mechanika. Audyt siedzial w srodku
        /// SpeedPostfix i wolal MobileParty.SpeedExplained, ktore NIE czyta cache,
        /// tylko PONOWNIE przepuszcza partie przez caly lancuch modeli. To wejscie
        /// zaczynalo sie o poziom glebiej (_final == 2, a w vanilli 3), wiec KAZDY
        /// poziom audytowanego przebiegu wypadal na bezpieczniku z linii 69
        /// i nie zdazyl podmienic kar terenu. W logu ladowaly wiec gole liczby
        /// vanilli: "Forest -1.08" to rowno 30% z bazy 3.60, "Snow -0.36" to 10%.
        /// Nasza zamiana dzialala w prawdziwym przebiegu (glebokosc 1), ale nikt
        /// jej nie widzial. Przy okazji SpeedExplained ZAPISUJE wynik z powrotem
        /// do MobileParty._lastCalculatedSpeed, wiec raz na dobe zatruwalo cache
        /// predkosci glownej partii wartoscia bez naszych ulg.
        /// Z dziennego ticku zaden model nie stoi na stosie (_final == 0), wiec
        /// rozpiska pokazuje to, co gracz naprawde ma - i cache dostaje to samo.
        /// </summary>
        internal static void DailyAudit()
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.SpeedAuditEnabled) return;
                var mp = MobileParty.MainParty;
                if (mp == null || Campaign.Current == null) return;
                int day = (int)CampaignTime.Now.ToDays;
                if (day == _lastAuditDay) return;
                _lastAuditDay = day;

                TerrainType tt = TerrainType.Plain;
                try { tt = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(mp.CurrentNavigationFace); } catch { }

                var ex = mp.SpeedExplained;
                var lines = ex.GetLines();
                var sb = new System.Text.StringBuilder("Audyt predkosci (dzien " + day + ", teren " + tt + "): ");
                for (int i = 0; i < lines.Count; i++)
                {
                    if (i > 0) sb.Append(" | ");
                    sb.Append(lines[i].name).Append(' ').Append(lines[i].number.ToString("+0.00;-0.00"));
                }
                sb.Append(" => ").Append(ex.ResultNumber.ToString("0.00"));
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("TerrainEase.DailyAudit", e); }
        }

        // ================================================================ E7/T (225, PLAN 7.4, projekt etapu 7 v2 rozdz. 3 "E7/T", R2)
        // Netto kary lasu, ktora gra nalozyla (ulamek, dodatni = kara): te same warunki co G DefaultPartySpeedCalculatingModel.CalculateFinalSpeed
        // :264-283 - ForestKin zwiadowcy (EffectiveScout) przy >= 75% pieszych w MemberRoster (ta sama arytmetyka float co gra, takze 0/0 = NaN = bez perku)
        // i cecha BattanianForestSpeedFeat (PartyBaseHelper.HasFeat - wodz, a bez wodza kultura partii, jak gra). Liczby gry czytane w czasie gry
        // (PrimaryBonus -0.5, EffectBonus 0.5), nie wpisane na stale: bez niczego 0.3, perk albo cecha 0.15, oba 0.
        internal static float GameForestNet(MobileParty mp, out bool kin, out bool feat)
        {
            kin = false;
            float vf = GameForestFull;
            var scout = mp.EffectiveScout;
            if (scout != null && scout.GetPerkValue(DefaultPerks.Scouting.ForestKin))
            {
                var r = mp.MemberRoster;
                float foot = 0f;
                for (int i = 0; i < r.Count; i++)
                    if (!r.GetCharacterAtIndex(i).IsMounted) foot += (float)r.GetElementNumber(i);
                if (foot / (float)r.TotalManCount >= 0.75f)
                {
                    vf = GameForestFull * (0f - DefaultPerks.Scouting.ForestKin.PrimaryBonus);
                    kin = true;
                }
            }
            feat = PartyBaseHelper.HasFeat(mp.Party, DefaultCulturalFeats.BattanianForestSpeedFeat);
            return feat ? vf - DefaultCulturalFeats.BattanianForestSpeedFeat.EffectBonus * GameForestFull : vf;
        }

        // Netto kary pustyni gry: 0.1, a przy cesze AseraiDesertFeat 0 (G :291-295). DesertBorn to osobna premia gry - zostaje.
        internal static float GameDesertNet(MobileParty mp, out bool feat)
        {
            feat = PartyBaseHelper.HasFeat(mp.Party, DefaultCulturalFeats.AseraiDesertFeat);
            return feat ? 0f : GameDesertFull;
        }

        // ---- probka godzinna (tylko z Stage7MeasureLog; dziala tez przy TerrainEaseExact off = "na sucho"). Stan sesji, czyszczony w Reset.
        private const int ProbesPerHour = 8;   // projekt pkt 6: najwyzej 8 rozpisek gry na godzine
        // probka z rozpiski: flaga watku (CalculateFinalSpeed bywa wolane z watkow roboczych gry - tam flaga zawsze false)
        [ThreadStatic] private static bool _probeOn;
        // SW-DROGI: probka wola model wprost (bez zapisu predkosci do pamieci partii) - premia drogi nie zapisuje wtedy stanu drogi partii (MapRoads.SpeedRoad)
        internal static bool ProbeActive { get { return _probeOn; } }
        private static MobileParty _probeParty;
        private static bool _pGot, _pForest;
        private static float _pGame, _pRefund, _pFlat, _pBase;
        private static string _gForest, _gCulture, _gDesert;   // nazwy wpisow GRY w rozpisce (te same teksty co G :47, :55, :59)
        private static readonly Random _rng = new Random(225);   // wlasny los - MBRandom gry zostaje nietkniety
        private static readonly List<MobileParty> _cand = new List<MobileParty>();
        // partio-godziny
        private static int _hF, _hFFeat, _hFKin, _hD, _hDFeat;
        // zwrot ponad netto gry (ok., jedn. - ulamek x LastCalculatedBaseSpeed): stara regula / biezaca
        private static double _exOld, _exNow;
        // predkosc partii w ruchu: 0-3 rody (Polnoc las, Polnoc rownina, Dorne pustynia, Dorne rownina), 4-7 to samo dla karawan i wozow wsi (MarchPace ich nie tnie)
        private static readonly double[] _sSum = new double[8];
        private static readonly int[] _sN = new int[8];
        // rozpiska gry: probek, z cecha/perkiem, na suficie MarchPace, zwrot > kary gry, kara Jeffa poza ulamkiem gry
        private static int _rN, _rFeat, _rCap, _rViol, _rMis;
        private static double _rSumU, _rMaxU;
        private static long _ticks;
        private static int _stumbles;

        internal static void Reset()
        {
            ZeroSample();
            _stumbles = 0; _probeParty = null; _pGot = false; _gForest = null;
        }

        /// <summary>E8/S (246, PLAN 8.11): dzien ostatniego audytu predkosci gracza od zera przy nowej kampanii albo wczytaniu (konstruktor ArmouryBehavior
        /// przez StateHygiene.ResetAll; osobno od Reset - pod kluczem StateHygieneEnabled). Dotad DailyAudit pomijal pierwsza dobe drugiej kampanii,
        /// gdy miala ten sam numer dnia co ostatni audyt poprzedniej (kazda nowa gra startuje w tym samym dniu). Zwraca 1, gdy znacznik byl ustawiony.</summary>
        internal static int ResetAudit()
        {
            int had = _lastAuditDay >= 0 ? 1 : 0;
            _lastAuditDay = -1;
            return had;
        }

        private static void ZeroSample()
        {
            _hF = _hFFeat = _hFKin = _hD = _hDFeat = 0;
            _exOld = _exNow = 0;
            for (int i = 0; i < 8; i++) { _sSum[i] = 0; _sN[i] = 0; }
            _rN = _rFeat = _rCap = _rViol = _rMis = 0;
            _rSumU = _rMaxU = 0;
            _ticks = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            // potkniecia liczone, probka dziala dalej (CLAUDE.md 7 - bez globalnego wylacznika)
            _stumbles++;
            if (_stumbles <= 3) Log.Error("TerrainEase." + where, e);
        }

        // suma wpisow GRY dla terenu (las: "Forest" + "Culture" cechy lasu; pustynia: "Desert") jako ulamek - wpis typu Multiply
        // w GetLines to BaseNumber x procent (G ExplainedNumber.GetLines), wiec dzielimy przez baze
        private static float GameLinesFactor(ref ExplainedNumber r, bool forest)
        {
            float b = r.BaseNumber;
            if (Math.Abs(b) < 0.0001f) return 0f;
            if (_gForest == null)
            {
                _gDesert = new TextObject("{=ecUwABe2}Desert").ToString();
                var ct = GameTexts.FindText("str_culture");
                _gCulture = ct != null ? ct.ToString() : "Culture";
                _gForest = new TextObject("{=rTFaZCdY}Forest").ToString();
            }
            float sum = 0f;
            foreach (var l in r.GetLines())
            {
                string n = l.Item1;
                if (forest ? (n == _gForest || n == _gCulture) : n == _gDesert) sum += l.Item2;
            }
            return sum / b;
        }

        internal static void Hourly()
        {
            var s = Settings.Current;
            if (s == null || !s.Stage7MeasureLog || Campaign.Current == null || !LogGate.Today) return;   // NW O4: sondy tylko w dobie pomiaru (klucz Stage7MeasureLog nietkniety - pilnuje tez WorldClock7)
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                _cand.Clear();
                Scan(MobileParty.AllLordParties, false, s);
                Scan(MobileParty.AllCaravanParties, true, s);
                Scan(MobileParty.AllVillagerParties, true, s);
                int k = Math.Min(ProbesPerHour, _cand.Count);
                for (int i = 0; i < k; i++)
                {
                    int j = i + _rng.Next(_cand.Count - i);   // losowo bez powtorzen
                    var tmp = _cand[i]; _cand[i] = _cand[j]; _cand[j] = tmp;
                    try { Probe(_cand[i], s); } catch (Exception e) { Stumble("Probe", e); }
                }
            }
            catch (Exception e) { Stumble("Hourly", e); }
            finally { _cand.Clear(); _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        // partie AI w ruchu na ladzie (bez gracza i bez doczepionych do armii - jada predkoscia wodza): teren pod partia jak postfiks
        private static void Scan(MBReadOnlyList<MobileParty> list, bool uncapped, Settings s)
        {
            if (list == null) return;
            var main = MobileParty.MainParty;
            for (int i = 0; i < list.Count; i++)
            {
                var mp = list[i];
                try
                {
                    if (mp == null || mp == main || !mp.IsActive || mp.AttachedTo != null || mp.IsCurrentlyAtSea || !mp.IsMoving) continue;
                    if (!uncapped && mp.LeaderHero == null) continue;
                    TerrainType tt = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(mp.CurrentNavigationFace);
                    bool forest = tt == TerrainType.Forest, desert = tt == TerrainType.Desert || tt == TerrainType.Dune;
                    if (!forest && !desert && tt != TerrainType.Plain) continue;
                    var cul = mp.LeaderHero != null ? mp.LeaderHero.Culture : mp.Party.Culture;   // jak PartyBaseHelper.HasFeat
                    string cid = cul != null ? cul.StringId : null;
                    bool north = cid == "battania", dorne = cid == "aserai";   // ROT: battania = Polnoc, aserai = Dorne
                    int g = uncapped ? 4 : 0;
                    if (forest)
                    {
                        bool kin, feat;
                        float net = GameForestNet(mp, out kin, out feat);
                        _hF++; if (feat) _hFFeat++; if (kin) _hFKin++;
                        Excess(mp, GameForestFull - net, s);
                        if (north) Note(g, mp.Speed);
                        _cand.Add(mp);
                    }
                    else if (desert)
                    {
                        bool feat;
                        float net = GameDesertNet(mp, out feat);
                        _hD++; if (feat) _hDFeat++;
                        Excess(mp, GameDesertFull - net, s);
                        if (dorne) Note(g + 2, mp.Speed);
                        _cand.Add(mp);
                    }
                    else if (north) Note(g + 1, mp.Speed);
                    else if (dorne) Note(g + 3, mp.Speed);
                }
                catch (Exception e) { Stumble("Scan", e); }
            }
        }

        private static void Note(int k, float speed) { _sSum[k] += speed; _sN[k]++; }

        // stara regula oddawala pelny procent gry; nadmiar ponad to, co gra nalozyla netto (ok. w jedn. - na bazie predkosci partii)
        private static void Excess(MobileParty mp, float exFactor, Settings s)
        {
            if (exFactor <= 0.0005f) return;
            double u = exFactor * mp.LastCalculatedBaseSpeed;
            _exOld += u;
            if (s.TerrainEaseEnabled && !s.TerrainEaseExact) _exNow += u;
        }

        // rozpiska gry na glebokosci 0 (jak DailyAudit): postfiks zapisuje wpisy gry przed zwrotem i zmiany od naszego zwrotu i kary;
        // tu - netto (gra + zwrot) i to, czy partia stoi na suficie MarchPace (wpis "Marching column"). Model wolany wprost, NIE
        // mp.SpeedExplained: ten zapisuje wynik i baze z opisami do pamieci partii (G MobileParty :400-410) - kazde kolejne przeliczenie
        // predkosci tej partii budowaloby wtedy opisy (koszt) az do uniewaznienia bazy; tu pamiec partii zostaje nietknieta
        private static void Probe(MobileParty mp, Settings s)
        {
            var model = Campaign.Current.Models.PartySpeedCalculatingModel;
            if (model == null) return;
            _probeParty = mp; _pGot = false; _probeOn = true;
            ExplainedNumber en;
            try { en = model.CalculateFinalSpeed(mp, model.CalculateBaseSpeed(mp, true)); }
            finally { _probeOn = false; _probeParty = null; }
            if (!_pGot) return;   // postfiks nie wpiety (TerrainEaseEnabled off przy starcie) albo wylaczony
            _pGot = false;
            bool cap = false;
            foreach (var l in en.GetLines())
                if (l.Item1 != null && l.Item1.StartsWith("Marching column", StringComparison.Ordinal)) { cap = true; break; }
            float full = _pForest ? GameForestFull : GameDesertFull;
            float u = (_pGame + _pRefund) * _pBase;   // netto teren gry + nasz zwrot, jedn. (> 0 = zwrot wiekszy niz kara gry)
            _rN++;
            if (cap) _rCap++;
            if (-_pGame < full - 0.0005f) _rFeat++;
            _rSumU += u;
            if (Math.Abs(u) > _rMaxU) _rMaxU = Math.Abs(u);
            if (u > 0.001f) _rViol++;
            if (s.TerrainEaseEnabled && s.TerrainEaseExact)
            {
                float pen = _pForest ? s.ForestSpeedPenalty : s.DesertSpeedPenalty;
                float want = pen * Math.Max(0f, -_pGame) / full;   // kara Jeffa w ulamku, ktory zostawila gra - z wpisow GRY, nie z naszego wzoru
                if (want <= 0.0005f) want = 0f;                    // Swap nie dopisuje kary < 0.0005
                if (Math.Abs(_pFlat - want) > 0.001f) _rMis++;
            }
        }

        private static string Avg(int k)
        {
            return _sN[k] > 0 ? (_sSum[k] / _sN[k]).ToString("0.000", CultureInfo.InvariantCulture) : "-";
        }

        // linia dobowa (po ksiedze pieniadza, obok E7/0): dzien = doba ksiegi (dzien gry - 1, jak "Pieniadz swiata")
        internal static void Stage7Daily()
        {
            var s = Settings.Current;
            if (s == null || !s.Stage7MeasureLog || !LogGate.Lines) { ZeroSample(); return; }   // NW O4: linia za dobe pomiaru, w innej dobie samo zerowanie
            int day = (int)CampaignTime.Now.ToDays - 1;
            try
            {
                var ci = CultureInfo.InvariantCulture;
                int fd = _hF + _hD;
                double ms = _ticks * 1000.0 / Stopwatch.Frequency;
                var sb = new StringBuilder(640);
                sb.Append("Kary terenu (E7/T): dzien ").Append(day)
                  .Append(" (TerrainEaseExact ").Append(s.TerrainEaseExact ? "on" : "off").Append(", TerrainEaseEnabled ").Append(s.TerrainEaseEnabled ? "on" : "off").Append(')')
                  .Append(" | probki AI (partio-godziny): las ").Append(_hF).Append(" (cecha lasu ").Append(_hFFeat).Append(", ForestKin ").Append(_hFKin)
                  .Append("), pustynia ").Append(_hD).Append(" (cecha pustyni ").Append(_hDFeat).Append(')')
                  .Append(" | zwrot ponad netto gry (ok., na partio-godzine w lesie/na pustyni): stara regula ").Append((fd > 0 ? _exOld / fd : 0).ToString("0.0000", ci))
                  .Append(" jedn., teraz ").Append((fd > 0 ? _exNow / fd : 0).ToString("0.0000", ci)).Append(" jedn.")
                  .Append(" | Polnoc las/rownina ").Append(Avg(0)).Append('/').Append(Avg(1)).Append(" (n ").Append(_sN[0]).Append('/').Append(_sN[1]).Append(')')
                  .Append(", Dorne pustynia/rownina ").Append(Avg(2)).Append('/').Append(Avg(3)).Append(" (n ").Append(_sN[2]).Append('/').Append(_sN[3]).Append(')')
                  .Append(" | koszt ").Append(ms.ToString("0.000", ci)).Append(" ms")
                  .Append(" | z rozpiski gry: probek ").Append(_rN).Append(" (z cecha/perkiem ").Append(_rFeat).Append(')')
                  .Append(", netto teren (gra + zwrot) sr. ").Append((_rN > 0 ? _rSumU / _rN : 0).ToString("0.0000", ci))
                  .Append(" (maks. |u| ").Append(_rMaxU.ToString("0.0000", ci)).Append(')')
                  .Append(", zwrot > kary gry ").Append(_rViol).Append(", kara Jeffa poza ulamkiem gry ").Append(_rMis)
                  .Append(", na suficie MarchPace ").Append((_rN > 0 ? 100.0 * _rCap / _rN : 0).ToString("0.0", ci)).Append('%')
                  .Append(" | ponizej sufitu (karawany, wozy wsi): Polnoc las/rownina ").Append(Avg(4)).Append('/').Append(Avg(5))
                  .Append(" (n ").Append(_sN[4]).Append('/').Append(_sN[5]).Append(')')
                  .Append(", Dorne pustynia/rownina ").Append(Avg(6)).Append('/').Append(Avg(7)).Append(" (n ").Append(_sN[6]).Append('/').Append(_sN[7]).Append(')')
                  .Append(" | potkniecia ").Append(_stumbles);
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Stage7Daily", e); }
            ZeroSample();
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.TerrainEaseEnabled) { Log.Info("Kary terenu: vanilla (TerrainEase wylaczony)."); return; }
                var post = new HarmonyMethod(typeof(TerrainEase).GetMethod("SpeedPostfix",
                    BindingFlags.Public | BindingFlags.Static)) { priority = Priority.Normal };
                int done = 0;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                    catch { continue; }
                    foreach (var t in types)
                    {
                        if (t == null || t.IsAbstract || !typeof(PartySpeedModel).IsAssignableFrom(t)) continue;
                        try
                        {
                            var m = t.GetMethod("CalculateFinalSpeed", BindingFlags.Public | BindingFlags.NonPublic |
                                                                        BindingFlags.Instance | BindingFlags.DeclaredOnly);
                            if (m == null || m.IsAbstract) continue;
                            h.Patch(m, postfix: post);
                            done++;
                        }
                        catch { }
                    }
                }
                Log.Info("Kary terenu po naszemu: las -" + c.ForestSpeedPenalty + ", pustynia -" + c.DesertSpeedPenalty
                         + ", snieg -" + c.SnowSpeedPenalty + ", brod -" + c.FordSpeedPenalty + ", noc -" + c.NightSpeedPenalty
                         + " (" + done + " modeli); audyt predkosci " + (c.SpeedAuditEnabled ? "raz na dzien" : "wylaczony")
                         + "; las i pustynia " + (c.TerrainEaseExact ? "dokladnie jak gra (E7/T: cecha kultury i ForestKin zmniejszaja tez nasza kare)" : "z pelnym zwrotem kary gry (TerrainEaseExact off)") + ".");
            }
            catch (Exception e) { Log.Error("TerrainEase.ApplyAll", e); }
        }
    }
}
