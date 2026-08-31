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
    /// </summary>
    internal static class ScorchedEarth
    {
        private static readonly TextObject _txtScar = new TextObject("{=!}War scars");
        private static List<Village> _villages;
        private static int _playerForageShown = -1;

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
                        if (v.Hearth <= floor) continue;
                        var vf = v.Settlement.MapFaction;
                        if (vf == null || vf == f || !FactionManager.IsAtWarAgainstFaction(vf, f)) continue;
                        if (pos.Distance(v.Settlement.GetPosition2D) > radius) continue;

                        float drain = Math.Max(0.2f, s.ForageHearthPerDay * mp.MemberRoster.TotalManCount / 500f);
                        v.Hearth = Math.Max(floor, v.Hearth - drain);
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

        /// <summary>Blizna: z ruin wstaje sie wolno; przy samym dnie wracaja uchodzcy.</summary>
        public static void HearthScarPostfix(Village village, ref ExplainedNumber __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.ScorchedEarthEnabled || village == null) return;
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
                         + s.ScarRegenPercent + "% ponizej " + s.ScarThresholdHearth + ") uzbrojone.");
            }
            catch (Exception e) { Log.Error("ScorchedEarth.ApplyAll", e); }
        }
    }
}
