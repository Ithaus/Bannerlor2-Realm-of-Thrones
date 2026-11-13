using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace CrashScribe
{
    /// <summary>481 (13.11): Heraldry Studio 1.9.0 (Bannerlord.HeraldryStudio.CompleteCollection) na mapie ROT.
    /// (1) HeraldryStudio.BannerCodeCompatibility.TryRepairClanBanner(Clan) - wolane z BannerCompatibilityCampaignBehavior.OnSessionLaunched
    /// (kazdy klan) i OnDailyTickClan - dla klanu innego niz gracza wola RepairNonPlayerBannerCode, ktory przycina kod herbu do 33 warstw
    /// (330 liczb) i zapisuje nowy Banner w klanie. ROT ma 210 z 243 herbow klanow powyzej 33 warstw (mediana 214) - trwale zniszczenie w zapisie.
    /// (2) statyczny Dictionary&lt;int,int&gt; LegacyOnlineColorIds w BannerCodeCompatibility ORAZ w BannerBuilderScreenPatch przemalowuje kolory
    /// 37, 40, 117, 124-145 ("stare kolory online"), a w ROT to wazne kolory palety ROT (ROT-Content/ModuleData/banner_icons.xml) - 81 klanow,
    /// takze klan gracza.
    /// Naprawa: (a) oba slowniki czyszczone przy starcie procesu - NormalizeLegacyColors / NormalizeLegacyOnlineEditorColors nic nie zmieniaja,
    /// edytor herbu gracza w HS dziala dalej, tylko bez przemalowania; (b) prefiksy Priority.First (DeclaredMethod na klasie deklarujacej):
    /// TryRepairClanBanner - klan inny niz gracza: pominiete (false), gracz: oryginal (same kolory, po (a) bez zmian); RepairNonPlayerBannerCode -
    /// kod oddany bez zmian (changed=false); druga linia obrony na samych metodach zdarzen (delegaty, nie da sie ich wkompilowac):
    /// OnSessionLaunched - pominiete (po (a) nie mialo nic do zrobienia nawet dla gracza), OnDailyTickClan - pominiete dla klanu innego niz gracza.
    /// Kazde wolanie we wlasnym try/catch: potkniecie liczone, oryginal pominiety (naprawa HS nie jest potrzebna), bez globalnego wylacznika.
    /// HS nie jest referencja przy kompilacji - wszystko odbiciem; bez HS linia "HS brak - pomijam". Pliki HS nietkniete.</summary>
    internal static class HsBanners
    {
        private const string TCompat = "HeraldryStudio.BannerCodeCompatibility";
        private const string TBehavior = "HeraldryStudio.BannerCompatibilityCampaignBehavior";
        private const string TEditor = "HeraldryStudio.BannerBuilderScreenPatch";
        private const int NativeLayers = 33;

        private static bool _installed;
        private static int _skipTry, _skipRepair, _skipSession, _skipDaily, _playerPass;
        private static int _stumbles;
        private static int _sessionLines;

        internal static void Install(Harmony harmony)
        {
            if (_installed || harmony == null) return;
            _installed = true;
            try
            {
                var tCompat = AccessTools.TypeByName(TCompat);
                var tBehavior = AccessTools.TypeByName(TBehavior);
                var tEditor = AccessTools.TypeByName(TEditor);
                if (tCompat == null && tBehavior == null && tEditor == null)
                {
                    Scribe.Line("Mends: HS herby ROT - HS brak - pomijam.");
                    return;
                }

                // (a) stare kolory online - oba slowniki (osobne potkniecia, jeden nie gasi drugiego)
                string n1 = ClearColors(tCompat), n2 = ClearColors(tEditor);

                // (b) przycinanie - metody wyszukiwane na klasie deklarujacej (pulapka 15.10)
                var targets = new List<KeyValuePair<MethodInfo, string>>();
                targets.Add(new KeyValuePair<MethodInfo, string>(
                    tCompat != null ? AccessTools.DeclaredMethod(tCompat, "TryRepairClanBanner", new[] { typeof(Clan) }) : null, nameof(TryRepairPrefix)));
                targets.Add(new KeyValuePair<MethodInfo, string>(
                    tCompat != null ? AccessTools.DeclaredMethod(tCompat, "RepairNonPlayerBannerCode", new[] { typeof(string), typeof(bool).MakeByRefType() }) : null, nameof(RepairPrefix)));
                targets.Add(new KeyValuePair<MethodInfo, string>(
                    tBehavior != null ? AccessTools.DeclaredMethod(tBehavior, "OnSessionLaunched", new[] { typeof(CampaignGameStarter) }) : null, nameof(SessionPrefix)));
                targets.Add(new KeyValuePair<MethodInfo, string>(
                    tBehavior != null ? AccessTools.DeclaredMethod(tBehavior, "OnDailyTickClan", new[] { typeof(Clan) }) : null, nameof(DailyPrefix)));

                int bound = 0;
                var miss = new List<string>();
                foreach (var t in targets)
                {
                    if (t.Key == null) { miss.Add(t.Value); continue; }
                    try
                    {
                        harmony.Patch(t.Key, prefix: new HarmonyMethod(typeof(HsBanners), t.Value) { priority = Priority.First });
                        if (Ours(t.Key, harmony.Id)) bound++;
                        else miss.Add(t.Key.Name + " (brak w Harmony.GetPatchInfo)");
                    }
                    catch (Exception e)
                    {
                        _stumbles++;
                        miss.Add(t.Key.Name + " (" + e.GetType().Name + ")");
                    }
                }

                Scribe.Line("Mends: HS herby ROT - przycinanie wylaczone (prefiksy " + bound + "/" + targets.Count + "), stare kolory online wylaczone ("
                            + n1 + "/" + n2 + ")"
                            + (miss.Count > 0 ? " - UWAGA, nie wpiete: " + string.Join(", ", miss.ToArray()) : "")
                            + " [kolory: wpisy wyczyszczone w BannerCodeCompatibility / BannerBuilderScreenPatch].");
            }
            catch (Exception e) { _stumbles++; try { Scribe.Report("CrashScribe", e, "HsBanners.Install", null); } catch { } }
        }

        /// <summary>Czysci statyczny slownik LegacyOnlineColorIds w typie; zwraca liczbe wyczyszczonych wpisow albo opis braku. Nie rzuca.</summary>
        private static string ClearColors(Type t)
        {
            if (t == null) return "brak typu";
            try
            {
                var f = AccessTools.Field(t, "LegacyOnlineColorIds");
                if (f == null || !f.IsStatic) return "brak pola";
                var d = f.GetValue(null) as Dictionary<int, int>;
                if (d == null) return "pole puste";
                int n = d.Count;
                d.Clear();
                return n.ToString();
            }
            catch (Exception e) { _stumbles++; return "potkniecie " + e.GetType().Name; }
        }

        private static bool Ours(MethodBase m, string id)
        {
            try
            {
                var info = Harmony.GetPatchInfo(m);
                if (info == null) return false;
                foreach (var p in info.Prefixes) if (p.owner == id) return true;
            }
            catch { }
            return false;
        }

        private static bool IsPlayer(Clan clan)
        {
            return clan != null && clan == Clan.PlayerClan;
        }

        /// <summary>BannerCodeCompatibility.TryRepairClanBanner(Clan): klan inny niz gracza - pominiete (__result false), gracz - oryginal.</summary>
        private static bool TryRepairPrefix(Clan clan, ref bool __result)
        {
            try
            {
                if (IsPlayer(clan)) { _playerPass++; return true; }
                _skipTry++;
                __result = false;
                return false;
            }
            catch (Exception e)
            {
                Stumble(e, "TryRepairPrefix");
                __result = false;
                return false;
            }
        }

        /// <summary>BannerCodeCompatibility.RepairNonPlayerBannerCode(string, out bool): kod bez zmian, changed=false (bez przyciecia do 33 warstw).</summary>
        private static bool RepairPrefix(string bannerCode, ref bool changed, ref string __result)
        {
            try
            {
                _skipRepair++;
                changed = false;
                __result = bannerCode;
                return false;
            }
            catch (Exception e)
            {
                Stumble(e, "RepairPrefix");
                changed = false;
                __result = bannerCode;
                return false;
            }
        }

        /// <summary>BannerCompatibilityCampaignBehavior.OnSessionLaunched: pominiete; linia sesji z liczba herbow ponad 33 warstwy, ktore HS by przycial.</summary>
        private static bool SessionPrefix()
        {
            try
            {
                _skipSession++;
                if (_sessionLines < 20)
                {
                    _sessionLines++;
                    int clans = 0, over = 0, maxLayers = 0;
                    try
                    {
                        foreach (var c in Clan.All)
                        {
                            if (c == null || IsPlayer(c) || c.Banner == null) continue;
                            clans++;
                            string code = c.Banner.BannerCode;
                            if (string.IsNullOrEmpty(code)) continue;
                            int layers = (Count(code, '.') + 1) / 10;
                            if (layers > NativeLayers) over++;
                            if (layers > maxLayers) maxLayers = layers;
                        }
                    }
                    catch (Exception e) { Stumble(e, "SessionPrefix.licz"); }
                    Scribe.Line("Mends: HS herby ROT - sesja: naprawa HS pominieta; herby klanow (bez gracza) " + clans + ", ponad " + NativeLayers
                                + " warstw " + over + " (najwiecej " + maxLayers + ") - nieprzyciete; dotad pominiete TryRepair " + _skipTry
                                + ", RepairNonPlayer " + _skipRepair + ", doba " + _skipDaily + ", gracz przepuszczony " + _playerPass
                                + ", potkniecia " + _stumbles + ".");
                }
                return false;
            }
            catch (Exception e) { Stumble(e, "SessionPrefix"); return false; }
        }

        /// <summary>BannerCompatibilityCampaignBehavior.OnDailyTickClan(Clan): klan inny niz gracza - pominiete.</summary>
        private static bool DailyPrefix(Clan clan)
        {
            try
            {
                if (IsPlayer(clan)) return true;
                _skipDaily++;
                return false;
            }
            catch (Exception e) { Stumble(e, "DailyPrefix"); return false; }
        }

        private static int Count(string s, char ch)
        {
            int n = 0;
            for (int i = 0; i < s.Length; i++) if (s[i] == ch) n++;
            return n;
        }

        /// <summary>Potkniecie liczone; pelny raport tylko kilka pierwszych razy (bez zalewania logu, bez wylaczania naprawy).</summary>
        private static void Stumble(Exception e, string where)
        {
            _stumbles++;
            if (_stumbles <= 5)
            {
                try { Scribe.Report("CrashScribe", e, "HsBanners." + where, null); } catch { }
            }
        }
    }
}
