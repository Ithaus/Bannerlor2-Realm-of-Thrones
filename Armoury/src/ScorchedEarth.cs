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
    /// Demografia krok 4 (Devastation): przy czynnym spustoszeniu marsz nie zdejmuje hearth w skali gry, tylko
    /// pustoszy ULAMEK okregu w ludziach (DevastationPerForagerDay na zbrojnego na dobe) - ludzie ida na konto uchodzcow
    /// i wracaja; podloge hearth zastepuje pulap spustoszenia okregu, a "+0.5 ponizej 40" nie dziala (wraca sie z konta).
    /// </summary>
    internal static class ScorchedEarth
    {
        private static readonly TextObject _txtScar = new TextObject("{=!}War scars");
        private static List<Village> _villages;
        private static int _playerForageShown = -1;

        /// <summary>Stan jednej kampanii (lista wsi to obiekty tej kampanii - po wczytaniu innej gry bez restartu bylyby martwe).</summary>
        internal static void Reset() { _villages = null; _playerForageShown = -1; }

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
                // demografia krok 4: marsz pustoszy ulamek okregu (ludzie -> uchodzcy), a nie hearth w skali gry
                bool dev = Devastation.On;
                // pierwsza doba nowej kampanii: zerowanie biegnie przed rentami i siewem puli wyrzutkow, czyli przed kalibracja
                // ludnosci - bez niej wies nie mialaby przelicznika i dostalaby stara regule (-0.8 hearth w nicosc)
                if (dev) PopulationLaw.EnsureCalibrated();

                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || !mp.IsActive || !mp.IsLordParty) continue;
                    if (mp.MemberRoster == null || mp.MemberRoster.TotalManCount < Math.Max(1, s.ForageMinMen)) continue;
                    if (mp.CurrentSettlement != null || mp.MapEvent != null) continue;
                    if (Undead.Party(mp)) continue;                    // umarli nie zeruja
                    var f = mp.MapFaction;
                    if (f == null) continue;

                    var pos = mp.GetPosition2D;
                    foreach (var v in _villages)
                    {
                        if (v == null || v.Settlement == null) continue;
                        if (!dev && v.Hearth <= floor) continue;
                        var vf = v.Settlement.MapFaction;
                        if (vf == null || vf == f || !FactionManager.IsAtWarAgainstFaction(vf, f)) continue;
                        if (pos.Distance(v.Settlement.GetPosition2D) > radius) continue;

                        // wies bez przelicznika ludzi (w ROT nie ma takiej) zostaje przy starej regule z podloga hearth
                        bool people = dev && Devastation.Covers(v);
                        if (dev && !people && v.Hearth <= floor) continue;
                        if (people)
                        {
                            // okreg spustoszony do pulapu nie ma juz czego oddac - jak dawna podloga: szukamy nastepnej wsi
                            if (!Devastation.Open(v)) continue;
                            Devastation.Forage(v, mp.MemberRoster.TotalManCount);
                        }
                        else
                        {
                            float drain = Math.Max(0.2f, s.ForageHearthPerDay * mp.MemberRoster.TotalManCount / 500f);
                            v.Hearth = Math.Max(floor, v.Hearth - drain);
                        }
                        if (grain != null && mp.ItemRoster != null)
                            mp.ItemRoster.AddToCounts(grain, 1 + mp.MemberRoster.TotalManCount / 250);

                        if (mp == MobileParty.MainParty && _playerForageShown != day)
                        {
                            _playerForageShown = day;
                            Log.Player("The men live off the enemy's land - " + v.Settlement.Name + " goes hungrier for it.", false);
                        }
                        break;   // jedna wioska dziennie na partie - marsz, nie odkurzacz
                    }
                }
            }
            catch (Exception e) { Log.Error("ScorchedEarth.OnDaily", e); }
        }

        /// <summary>
        /// Ile hearth dziennie wraca do wsi przy samym dnie ("uchodzcy wracaja", +0.5 ponizej RefugeeFloorHearth). Te sama
        /// regule stosuje blizna nizej; przy czynnym przyroscie naturalnym (PopulationLaw.GrowthPostfix, demografia krok 3)
        /// blizna nie biegnie, a przyrost pyta o powrot tutaj - jedna regula, jedno miejsce. Wies poza stanem Normal
        /// (spalona, lupiona, pod przymusem): 0 - wynik gry jest tam niedodatni i blizna tez nic nie dopisuje.
        /// Demografia krok 4: przy czynnym spustoszeniu (Devastation) regula nie dziala - do wsi wracaja uchodzcy z jej konta
        /// w ksiedze ludzi, a nie pol punktu hearth z niczego (wies Reach: +222 ludzi dziennie).
        /// </summary>
        internal static float RefugeeReturn(Village village)
        {
            var s = Settings.Current;
            if (s == null || !s.ScorchedEarthEnabled || village == null) return 0f;
            if (Devastation.Covers(village)) return 0f;
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
                // demografia krok 3: przy czynnym przyroscie naturalnym wynik gry sluzy tylko za miare premii, a odrost +4 / +1.2
                // dziennie, ktory blizna spowalniala, juz nie istnieje - blizna nie ma czego ciac, powrot +0.5 dopisuje przyrost
                if (PopulationLaw.GrowthOn) return;
                if (__result.ResultNumber <= 0f) return;
                if (village.Hearth < Math.Max(1, s.RefugeeFloorHearth))
                {
                    __result.Add(0.5f, _txtScar);      // uchodzcy wracaja - dno nie jest grobem
                    return;
                }
                if (village.Hearth < Math.Max(1, s.ScarThresholdHearth))
                {
                    float keep = MBMath.ClampFloat(Math.Max(1, s.ScarRegenPercent) / 100f, 0.05f, 1f);
                    __result.AddFactor(keep - 1f, _txtScar);
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
                         + s.ScarRegenPercent + "% ponizej " + s.ScarThresholdHearth + ") uzbrojone; przy czynnym spustoszeniu (Devastation) marsz pustoszy ulamek okregu w ludziach, podloga i blizny nie dzialaja.");
            }
            catch (Exception e) { Log.Error("ScorchedEarth.ApplyAll", e); }
        }
    }
}
