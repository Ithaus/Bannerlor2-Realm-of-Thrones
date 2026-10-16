using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// SPALONA ZIEMIA (Jeff 31.08, "rob"). Vanilla: wioska ponizej 300
    /// palenisk odrasta +4/dzien - po rabunku "jak nowa" w pare tygodni.
    /// Odtad wojna zostawia blizny:
    ///  - FORAGING: wroga armia lordowska (>= 100 ludzi) przy cudzej wiosce
    ///    zywi sie z terenu - dziennie zdejmuje troche palenisk (skala
    ///    z wielkoscia armii) i BIERZE zboze do taboru; marsz ma PODLOGE
    ///    (25 palenisk) - doglebne zlupienie wymaga prawdziwego raidu;
    ///  - BLIZNA: wioska zbita ponizej progu (150) odbudowuje sie CZTERY
    ///    razy wolniej az stanie na nogi - krater goi sie sezonami;
    ///  - ANTY-SMIERC: ponizej 40 palenisk "uchodzcy wracaja" (+0.5/dzien
    ///    flat) - region moze byc zrujnowany, ale nigdy nie umiera na stale;
    ///  - JEDNA OS KARY: niskie paleniska juz obnizaja produkcje, rekrutow
    ///    i podatki w vanilla/BK - zadnych dodatkowych kar, zero spirali;
    ///  - UMARLI NIE ZERUJA (nie jedza) - horda NK nie drenuje marszem,
    ///    ich sprawka to konwersje ROT.
    /// E4/5 (Devastation): przy czynnym spustoszeniu marsz nie zdejmuje hearth w skali gry, tylko pustoszy ULAMEK okregu w ludziach
    /// (DevastationPerForagerDay na zbrojnego na dobe) - trafieni ida przez ksiege ludzi (zabici, las, uchodzcy) i wracaja w ciagu roku;
    /// podloge hearth zastepuje pulap spustoszenia okregu, a "+0.5 ponizej 40" nie dziala dla wsi objetej (uchodzcy wracaja z konta).
    /// </summary>
    internal static class ScorchedEarth
    {
        private static readonly TextObject _txtScar = new TextObject("{=!}War scars");
        private static List<Village> _villages;
        private static int _playerForageShown = -1;

        // NW O3 (noc 16.10, klucz ScorchedEarthFastScan): kontrola R9 - co SampleEvery-ta partia liczy tez stara kolejnosc warunkow; liczniki sesji
        private const int SampleEvery = 64;
        private static long _seSeq;
        private static int _seDiffAll;

        /// <summary>E4/5: stan jednej kampanii (lista wsi to obiekty tej kampanii - po wczytaniu innej gry bez restartu bylyby martwe; 113 recenzja).</summary>
        internal static void Reset() { _villages = null; _playerForageShown = -1; _seSeq = 0; _seDiffAll = 0; }

        /// <summary>
        /// NW O3: indeks pierwszej wsi od `from`, ktora przechodzi czyste warunki wstepne furazu (wies jest, hearth ponad podloga przy starej regule,
        /// frakcja wsi w wojnie z frakcja partii, wies w promieniu). Wszystkie warunki to odczyty bez skutkow, wiec kolejnosc nie zmienia wyniku:
        /// fast = najpierw odleglosc (tania), potem pytanie o wojne; inaczej kolejnosc sprzed zmiany. Granica ta sama: Distance > radius (bez kwadratu
        /// odleglosci - zaokraglenia float na granicy). -1 = brak.
        /// </summary>
        private static int Next(Vec2 pos, IFaction f, int from, bool fast, bool dev, int floor, float radius)
        {
            for (int i = from; i < _villages.Count; i++)
            {
                var v = _villages[i];
                if (v == null || v.Settlement == null) continue;
                if (!dev && v.Hearth <= floor) continue;
                if (fast)
                {
                    if (pos.Distance(v.Settlement.GetPosition2D) > radius) continue;
                    var vf = v.Settlement.MapFaction;
                    if (vf == null || vf == f || !FactionManager.IsAtWarAgainstFaction(vf, f)) continue;
                }
                else
                {
                    var vf = v.Settlement.MapFaction;
                    if (vf == null || vf == f || !FactionManager.IsAtWarAgainstFaction(vf, f)) continue;
                    if (pos.Distance(v.Settlement.GetPosition2D) > radius) continue;
                }
                return i;
            }
            return -1;
        }

        internal static void OnDaily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.ScorchedEarthEnabled) return;
                if (_villages == null)
                {
                    _villages = new List<Village>();
                    foreach (var st in Settlement.All)
                        if (st != null && st.IsVillage && st.Village != null) _villages.Add(st.Village);
                }

                var grain = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>("grain");
                float radius = Math.Max(1f, s.ForageRadius);
                int floor = Math.Max(0, s.ForageFloor);
                int day = (int)CampaignTime.Now.ToDays;
                // E4/5: marsz pustoszy ulamek okregu (ludzie -> zabici, las, uchodzcy), a nie hearth w skali gry. Pierwsza doba nowej kampanii: zerowanie biegnie
                // przed rentami i siewem puli wyrzutkow, czyli przed kalibracja ludnosci - bez niej wies nie mialaby przelicznika i dostalaby stara regule
                bool dev = Devastation.On;
                if (dev) PopulationLaw.EnsureCalibrated();
                bool fast = s.ScorchedEarthFastScan;   // NW O3: odleglosc przed wojna (Next)
                long tSe = System.Diagnostics.Stopwatch.GetTimestamp();
                int seParties = 0, seChecks = 0, seDiff = 0;

                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || !mp.IsActive || !mp.IsLordParty) continue;
                    if (mp.MemberRoster == null || mp.MemberRoster.TotalManCount < Math.Max(1, s.ForageMinMen)) continue;
                    if (mp.CurrentSettlement != null || mp.MapEvent != null) continue;
                    if (Undead.Party(mp)) continue;                    // umarli nie zeruja
                    var f = mp.MapFaction;
                    if (f == null) continue;

                    var pos = mp.GetPosition2D;
                    seParties++;
                    bool sample = fast && ++_seSeq % SampleEvery == 0;   // NW O3 R9: ta partia liczy tez stara kolejnosc warunkow
                    int from = 0;
                    while (true)
                    {
                        // NW O3: pierwsza wies od `from` po czystych warunkach wstepnych (dotad: foreach z tymi samymi warunkami w kolejnosci wojna -> odleglosc)
                        int vi = Next(pos, f, from, fast, dev, floor, radius);
                        if (sample) { seChecks++; if (Next(pos, f, from, false, dev, floor, radius) != vi) { seDiff++; _seDiffAll++; } }
                        if (vi < 0) break;
                        from = vi + 1;
                        var v = _villages[vi];

                        // E4/5: wies bez przelicznika ludzi (w ROT nie ma takiej) zostaje przy starej regule z podloga hearth
                        float per;
                        bool people = dev && Devastation.Covers(v, out per);
                        if (dev && !people && v.Hearth <= floor) continue;
                        if (people)
                        {
                            // okreg spustoszony do pulapu nie ma juz czego oddac - jak dawna podloga: szukamy nastepnej wsi; ruch hearth liczy ksiega (MDevForage)
                            if (!Devastation.Open(v)) continue;
                            Devastation.Forage(v, mp);
                        }
                        else
                        {
                            float drain = Math.Max(0.2f, s.ForageHearthPerDay * mp.MemberRoster.TotalManCount / 500f);
                            float h0 = v.Hearth;
                            v.Hearth = Math.Max(floor, v.Hearth - drain);
                            try { Measure40.NoteHearth(Measure40.SrcForage, v.Hearth - h0, v.Settlement); } catch { }   // E4/0: zerowanie armii (tu licznika nie ma; tylko pomiar) - tylko stara sciezka
                        }
                        if (grain != null && mp.ItemRoster != null)
                        {
                            int want = 1 + mp.MemberRoster.TotalManCount / 250;
                            if (s.ForageFromVillageStock) Leaks310.Forage(v, mp, want);   // 3.10: z zapasu tej wsi, nie z niczego
                            else mp.ItemRoster.AddToCounts(grain, want);
                        }

                        if (mp == MobileParty.MainParty && _playerForageShown != day)
                        {
                            _playerForageShown = day;
                            Log.Player("The men live off the enemy's land - " + v.Settlement.Name + " goes hungrier for it.", false);
                        }
                        break;   // jedna wioska dziennie na partie - marsz, nie odkurzacz
                    }
                }
                double seMs = (System.Diagnostics.Stopwatch.GetTimestamp() - tSe) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                Log.Info("Spalona ziemia (NW): dzien " + (day - 1) + " | szybki przeglad (ScorchedEarthFastScan) " + (fast ? "[WL]" : "[wyl]") + " | partii " + seParties
                         + ", probek starej drogi " + seChecks + ", rozjazdow " + seDiff + " (od wczytania " + _seDiffAll + ") | czas "
                         + seMs.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ms.");
            }
            catch (Exception e) { Log.Error("ScorchedEarth.OnDaily", e); }
        }

        /// <summary>
        /// E4/4 (109n): ile hearth dziennie wraca do wsi przy samym dnie ("uchodzcy wracaja", +0.5 ponizej RefugeeFloorHearth). Te sama regule stosuje blizna nizej;
        /// przy czynnym przyroscie naturalnym (PopulationLaw.GrowthPostfix) blizna nie biegnie, a przyrost pyta o powrot tutaj - jedna regula, jedno miejsce.
        /// Wies poza stanem Normal (spalona, lupiona, pod przymusem): 0. W logu osobno, jako ludzie z niczego.
        /// E4/5: wies objeta spustoszeniem (Devastation) - 0: wracaja uchodzcy z jej konta w ksiedze ludzi, a nie pol punktu hearth z niczego (wies Reach: +222 ludzi dziennie).
        /// </summary>
        internal static float RefugeeReturn(Village village)
        {
            var s = Settings.Current;
            if (s == null || !s.ScorchedEarthEnabled || village == null) return 0f;
            if (Devastation.CoversQuiet(village)) return 0f;
            if (village.VillageState != Village.VillageStates.Normal) return 0f;
            return village.Hearth < Math.Max(1, s.RefugeeFloorHearth) ? 0.5f : 0f;
        }

        /// <summary>Blizna: z ruin wstaje sie wolno; przy samym dnie wracaja uchodzcy.</summary>
        public static void HearthScarPostfix(Village village, ref ExplainedNumber __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.ScorchedEarthEnabled || village == null) return;
                // E4/4: przy czynnym przyroscie naturalnym wynik gry sluzy tylko za miare premii, a odrost +4 / +1.2 dziennie, ktory blizna spowalniala, juz nie
                // istnieje - blizna nie ma czego ciac, powrot +0.5 dopisuje przyrost (RefugeeReturn)
                if (PopulationLaw.GrowthOn) return;
                if (__result.ResultNumber <= 0f) return;
                if (village.Hearth < Math.Max(1, s.RefugeeFloorHearth))
                {
                    float r0 = __result.ResultNumber;
                    __result.Add(0.5f, _txtScar);      // uchodzcy wracaja - dno nie jest grobem
                    if (Measure40.InVillageTick) Measure40.NoteScar(__result.ResultNumber - r0);   // E4/0: tylko w ticku wsi gry (tylko pomiar)
                    return;
                }
                if (village.Hearth < Math.Max(1, s.ScarThresholdHearth))
                {
                    float keep = MBMath.ClampFloat(Math.Max(1, s.ScarRegenPercent) / 100f, 0.05f, 1f);
                    float r0 = __result.ResultNumber;
                    __result.AddFactor(keep - 1f, _txtScar);
                    if (Measure40.InVillageTick) Measure40.NoteScar(__result.ResultNumber - r0);   // E4/0: tylko w ticku wsi gry (tylko pomiar)
                }
            }
            catch { }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.ScorchedEarthEnabled) { Log.Info("ScorchedEarth: wylaczone."); return; }
                var m = AccessTools.Method(typeof(DefaultSettlementProsperityModel), "CalculateHearthChange");
                if (m != null)
                    h.Patch(m, postfix: new HarmonyMethod(typeof(ScorchedEarth).GetMethod("HearthScarPostfix")) { priority = Priority.Last });
                Log.Info("ScorchedEarth: foraging (podloga " + s.ForageFloor + " palenisk) i blizny wojenne (regen "
                         + s.ScarRegenPercent + "% ponizej " + s.ScarThresholdHearth + ") uzbrojone; przy czynnym spustoszeniu (Devastation, E4/5) marsz pustoszy ulamek okregu w ludziach (podloga hearth nie dziala).");
            }
            catch (Exception e) { Log.Error("ScorchedEarth.ApplyAll", e); }
        }
    }
}
