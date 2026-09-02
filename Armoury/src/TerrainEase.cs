using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// ULGA TERENU (Jeff 02.09: "ograniczenia podrozy typu forest, snow,
    /// desert sa za duze - daj -0.5, a nie jak teraz -1; przeciez zmniejszylismy
    /// szybkosc i trzeba dostosowac"). Vanilla liczy teren PROCENTEM od bazy
    /// (las -30%, snieg -10%, pustynia -10%, noc -25%, brod -30%), wiec po
    /// WorldPace 50% kara w liczbach zmalala o polowe, ale w PROPORCJI zostala
    /// taka sama - a Jeff chce, zeby teren gryzl slabiej. Postfix na kazdy
    /// CalculateFinalSpeed: czytamy gotowe pozycje rozpiski (GetLines), dla
    /// kazdej pozycji terenu/nocy oddajemy (100 - TerrainPenaltyPercent)%
    /// jej wartosci jako osobny, NAZWANY wpis "Forest (eased)" - gracz widzi
    /// w tooltipie, ile odzyskal. Biegnie PRZED sufitem kolumny (MarchPace ma
    /// Priority.Last), wiec czapka dalej trzyma.
    /// AUDYT (Jeff: "zaudytuj poruszanie sie"): raz na dzien gry pelna
    /// rozpiska predkosci glownej partii idzie do Armoury.log.
    /// </summary>
    internal static class TerrainEase
    {
        private static readonly string[] Names = { "Forest", "Snow", "Desert", "Night", "Fording" };
        private static int _lastAuditDay = -1;

        public static void SpeedPostfix(MobileParty __0, ref ExplainedNumber __result)
        {
            try
            {
                var c = Settings.Current;
                if (c == null) return;
                var mp = __0;
                if (mp == null) return;

                int pct = Math.Max(0, Math.Min(100, c.TerrainPenaltyPercent));
                List<(string name, float number)> lines = null;
                if (pct < 100 || (c.SpeedAuditEnabled && mp == MobileParty.MainParty))
                {
                    try { lines = __result.GetLines(); } catch { lines = null; }
                }

                if (pct < 100 && lines != null)
                {
                    float give = (100 - pct) / 100f;
                    foreach (var ln in lines)
                    {
                        if (ln.number >= 0f || string.IsNullOrEmpty(ln.name)) continue;
                        string nm = ln.name.Trim();
                        for (int i = 0; i < Names.Length; i++)
                        {
                            if (!string.Equals(nm, Names[i], StringComparison.OrdinalIgnoreCase)) continue;
                            float back = -ln.number * give;
                            if (back > 0.001f)
                                __result.Add(back, new TextObject("{=!}" + Names[i] + " (eased)"));
                            break;
                        }
                    }
                }

                // audyt raz na dzien gry - tylko glowna partia
                if (c.SpeedAuditEnabled && mp == MobileParty.MainParty && lines != null)
                {
                    int day = (int)CampaignTime.Now.ToDays;
                    if (day != _lastAuditDay)
                    {
                        _lastAuditDay = day;
                        var sb = new System.Text.StringBuilder("Audyt predkosci (dzien " + day + "): ");
                        try
                        {
                            var fresh = __result.GetLines();
                            for (int i = 0; i < fresh.Count; i++)
                            {
                                if (i > 0) sb.Append(" | ");
                                sb.Append(fresh[i].name).Append(' ').Append(fresh[i].number.ToString("+0.00;-0.00"));
                            }
                        }
                        catch { sb.Append("(brak rozpiski)"); }
                        sb.Append(" => ").Append(__result.ResultNumber.ToString("0.00"))
                          .Append(" (przed sufitem kolumny i kara snu; teren x").Append(pct).Append("%)");
                        Log.Info(sb.ToString());
                    }
                }
            }
            catch { }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
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
                Log.Info("Ulga terenu: kary terenu i nocy x" + Settings.Current.TerrainPenaltyPercent + "% (" + done
                         + " modeli); audyt predkosci " + (Settings.Current.SpeedAuditEnabled ? "raz na dzien" : "wylaczony") + ".");
            }
            catch (Exception e) { Log.Error("TerrainEase.ApplyAll", e); }
        }
    }
}
