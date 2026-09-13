using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
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
    /// dzialala tylko w dymku). Perki zwiadowcy (ForestKin, DesertBorn,
    /// featy kulturowe) zmniejszaja kare vanilla - takim partiom oddajemy
    /// pelny procent, wiec wychodza odrobine na plus; swiadomie. Biegnie
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

                if (tt == TerrainType.Forest)
                    Swap(ref __result, 0.3f, _uForest, c.ForestSpeedPenalty, _aForest);
                else if (!atSea && (tt == TerrainType.Water || tt == TerrainType.River || tt == TerrainType.UnderBridge
                                    || tt == TerrainType.Bridge || tt == TerrainType.Fording))
                    Swap(ref __result, 0.3f, _uFord, c.FordSpeedPenalty, _aFord);
                else if (tt == TerrainType.Desert || tt == TerrainType.Dune)
                    Swap(ref __result, 0.1f, _uDesert, c.DesertSpeedPenalty, _aDesert);
                // BAGNO (Jeff 13.09: "bagna 0.3, jesli sa bagna"). Vanilla nie liczy
                // bagnu ZADNEJ kary - w DefaultPartySpeedCalculatingModel branch terenu
                // obsluguje tylko Forest, brody i Desert/Dune, reszta nie dostaje nic.
                // Nie ma wiec czego zwracac: sam Add, bez Swap.
                else if (tt == TerrainType.Swamp)
                {
                    if (c.SwampSpeedPenalty > 0.0005f) __result.Add(-c.SwampSpeedPenalty, _aSwamp);
                }

                try
                {
                    var w = Campaign.Current.Models.MapWeatherModel.GetWeatherEventInPosition(mp.Position.ToVec2());
                    if (w == MapWeatherModel.WeatherEvent.Snowy || w == MapWeatherModel.WeatherEvent.Blizzard)
                        Swap(ref __result, 0.1f, _uSnow, c.SnowSpeedPenalty, _aSnow);
                }
                catch { }

                if (!atSea && Campaign.Current.IsNight)
                    Swap(ref __result, 0.25f, _uNight, c.NightSpeedPenalty, _aNight);

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
                         + " (" + done + " modeli); audyt predkosci " + (c.SpeedAuditEnabled ? "raz na dzien" : "wylaczony") + ".");
            }
            catch (Exception e) { Log.Error("TerrainEase.ApplyAll", e); }
        }
    }
}
