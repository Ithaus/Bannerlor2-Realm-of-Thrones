using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace Armoury
{
    /// <summary>
    /// 471b-1 (Jeff 13.11, odpowiedz D1: "daj mniejsze zamki pozim murow 1"). W ROT 8.1.8 (ROT-Map settlements.xml) zaden zamek nie ma murow poziomu 1:
    /// 128 zamkow ma "building_castle_fortifications" level 2, Casterly Rock (castle_V5) i Red Keep (ROT_castle61) - 3. Podzial 471 "maly zamek = mury 1"
    /// dawal wiec zero malych zamkow.
    /// KRYTERIUM "mniejszy" z danych (ROT-Content spclans.xml, tier rodu-wlasciciela na starcie): rody tier 3 maja 60 zamkow, tier 4 - 55, tier 5 - 9,
    /// tier 6 - 6 (130 zamkow, kazdy zamek ma innego pana; wladcy krolestw nie maja zamkow). Maly zamek = zamek rodu tier &lt;= 3 (pomniejszy rod), nie wielka
    /// twierdza z listy 471 (SiegeWalls.IsTwierdza471), z murami dokladnie 2 - ok. polowa zamkow (60 / 130). Siedziby wielkich rodow (tier 4-6) zostaja na 2.
    /// WYKONANIE: tylko NOWA kampania (OnNewGameCreatedPartialFollowUpEnd - po calym starcie gry) - Building.LevelDown() budowli CastleFortifications
    /// (droga gry: poziom 2 -> 1, postep 0, zdarzenie OnBuildingLevelChanged; StartLevel fortyfikacji zamku = 1, cs_full.cs:166146/165548). Zapis gry
    /// trzyma poziom sam - stary zapis bez zmian (wczytanie nie wola tej metody). Kontrola: pierwsza doba sesji nowej kampanii liczy, ile z obnizonych
    /// zamkow ma dalej mury 1 (gdyby BK/ROT nadpisaly poziom przy starcie). Wyjatek per zamek = licznik, bez globalnego wylacznika.
    /// Klucz SiegeSmallCastlesWalls1 NIE = zadnej zmiany (tylko linia logu).
    /// </summary>
    internal static class SmallCastles471
    {
        internal const int MaxTier = 3;
        private static bool On { get { var s = Settings.Current; return s != null && s.SiegeSmallCastlesWalls1; } }
        private static List<string> _done;      // obnizone w tej sesji (tylko nowa kampania) - kontrola w pierwszej dobie
        private static bool _checked;

        private static string Nm(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "-"; } catch { return "?"; } }

        /// <summary>Z ArmouryBehavior (OnNewGameCreatedPartialFollowUpEndEvent): male zamki nowej kampanii dostaja mury 1. Nie rzuca.</summary>
        internal static void OnNewGameEnd()
        {
            _done = null; _checked = false;
            try
            {
                if (!On) { Log.Info("Mury malych zamkow (471b-1) start nowej kampanii: klucz Small Castles Start With Level 1 Walls NIE - mury jak na mapie."); return; }
                var low = new List<string>();
                int castles = 0, great = 0, tierHigh = 0, notTwo = 0, noOwner = 0, fail = 0;
                var tiers = new int[8];
                var sb = new StringBuilder(4000);
                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsCastle || st.Town == null) continue;
                    castles++;
                    try
                    {
                        if (SiegeWalls.IsTwierdza471(st)) { great++; continue; }
                        var c = st.OwnerClan;
                        if (c == null) { noOwner++; continue; }
                        tiers[Math.Max(0, Math.Min(7, c.Tier))]++;
                        if (c.Tier > MaxTier) { tierHigh++; continue; }
                        Building b = null;
                        foreach (var x in st.Town.Buildings) if (x != null && x.BuildingType == DefaultBuildingTypes.CastleFortifications) { b = x; break; }
                        if (b == null || b.CurrentLevel != 2) { notTwo++; continue; }
                        b.LevelDown();
                        if (b.CurrentLevel == 1 && st.Town.GetWallLevel() == 1)
                        {
                            low.Add(st.StringId);
                            if (sb.Length < 3800) sb.Append(sb.Length > 0 ? ", " : "").Append(st.StringId).Append(' ').Append(Nm(st)).Append(" (").Append(c.StringId).Append(" t").Append(c.Tier).Append(')');
                        }
                        else fail++;
                    }
                    catch (Exception e) { fail++; if (fail <= 3) Log.Error("SmallCastles471." + (st.StringId ?? "?"), e); }
                }
                _done = low;
                Log.Info("Mury malych zamkow (471b-1) start nowej kampanii: zamkow " + castles + " | mury 2 -> 1: " + low.Count + " (rod tier <= " + MaxTier + ", nie wielka twierdza)"
                         + " | bez zmian: wielkie twierdze " + great + ", rod tier > " + MaxTier + " " + tierHigh + ", mury inne niz 2 " + notTwo + ", bez pana " + noOwner + ", blad " + fail
                         + " | tiery panow zamkow: t1 " + tiers[1] + ", t2 " + tiers[2] + ", t3 " + tiers[3] + ", t4 " + tiers[4] + ", t5 " + tiers[5] + ", t6 " + tiers[6]
                         + " | lista: " + (sb.Length == 0 ? "-" : sb.ToString()) + ".");
            }
            catch (Exception e) { Log.Error("SmallCastles471.OnNewGameEnd", e); }
        }

        /// <summary>Z ArmouryBehavior (doba): raz na sesje nowej kampanii - ile obnizonych zamkow ma dalej mury 1 (kontrola, tylko log). Nie rzuca.</summary>
        internal static void Daily()
        {
            if (_checked || _done == null) return;
            _checked = true;
            try
            {
                int one = 0; var other = new List<string>();
                foreach (var id in _done)
                {
                    var st = Settlement.Find(id);
                    int l = st != null && st.Town != null ? st.Town.GetWallLevel() : -1;
                    if (l == 1) one++; else if (other.Count < 20) other.Add(id + "=" + l);
                }
                Log.Info("Mury malych zamkow (471b-1) kontrola pierwszej doby: obnizonych " + _done.Count + ", dalej mury 1: " + one
                         + (other.Count > 0 ? " | ZMIENIONE po starcie: " + string.Join(", ", other.ToArray()) : "") + ".");
            }
            catch (Exception e) { Log.Error("SmallCastles471.Daily", e); }
            _done = null;
        }
    }
}
