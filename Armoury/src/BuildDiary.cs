using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// DZIENNIK BUDOW (etap 0 z docs/AUDYT-BUDOWY-SKARBIEC.md; Jeff 05.10: "najpierw logi"). Tylko log - nic nie zmienia.
    /// Codziennie dla kazdego miasta i zamku: co jest w budowie, postep / koszt w punktach, dzienna moc budowy (model),
    /// ile dni do konca przy tej mocy, i czy budowa stoi (postep bez zmian od wczoraj). Szczegoly do Logs/<sesja>/budowy.log,
    /// podsumowanie dnia w glownym logu (ile budow idzie, ile stoi, srednia moc, ukonczone poziomy).
    /// </summary>
    internal static class BuildDiary
    {
        private static readonly Dictionary<string, float> _last = new Dictionary<string, float>();
        private static readonly Dictionary<string, int> _stuckDays = new Dictionary<string, int>();
        private static readonly Dictionary<string, string> _sig = new Dictionary<string, string>();

        internal static void Reset() { _last.Clear(); _stuckDays.Clear(); _sig.Clear(); }

        internal static void Daily()
        {
            try
            {
                if (!Settings.Current.BuildDiaryEnabled) return;
                int day = (int)CampaignTime.Now.ToDays;
                int going = 0, stuck = 0, done = 0, idle = 0, towns = 0;
                double power = 0;
                var lines = new List<string>();
                foreach (var st in Settlement.All)
                {
                    if (st == null || st.Town == null || (!st.IsTown && !st.IsCastle)) continue;
                    var t = st.Town;
                    towns++;
                    float pw = 0f;
                    try { pw = t.Construction; } catch { }
                    var b = t.BuildingsInProgress != null && t.BuildingsInProgress.Count > 0 ? t.BuildingsInProgress.Peek() : null;
                    if (b == null) { idle++; continue; }
                    string key = st.StringId;
                    int cost = 0; try { cost = b.GetConstructionCost(); } catch { }
                    float prog = b.BuildingProgress;
                    string sig = b.BuildingType.StringId + "|" + b.CurrentLevel;
                    string prevSig; float prev = 0f;
                    bool had = _sig.TryGetValue(key, out prevSig);
                    if (had) _last.TryGetValue(key, out prev);
                    string state;
                    if (had && prevSig != sig) { done++; _stuckDays.Remove(key); state = "UKONCZONO poprzedni poziom, nowy w toku"; }
                    else if (had && Math.Abs(prog - prev) < 0.01f)
                    {
                        int sd; _stuckDays.TryGetValue(key, out sd); _stuckDays[key] = sd + 1; stuck++;
                        state = "STOI " + (sd + 1) + " dni";
                    }
                    else { _stuckDays.Remove(key); going++; state = "idzie +" + (had ? (prog - prev).ToString("0.#") : "?"); }
                    _sig[key] = sig; _last[key] = prog;
                    power += pw;
                    int daysLeft = pw > 0.01f && cost > 0 ? (int)Math.Ceiling((cost - prog) / pw) : -1;
                    lines.Add((st.IsCastle ? "zamek " : "miasto ") + st.Name + ": " + b.Name + " poz." + b.CurrentLevel + "->" + (b.CurrentLevel + 1)
                              + " " + (int)prog + "/" + cost + " pkt, moc " + pw.ToString("0.#") + "/dzien, " + (daysLeft >= 0 ? "zostalo ~" + daysLeft + " dni" : "bez mocy") + " - " + state);
                }
                foreach (var l in lines) Log.Info("Budowa: dzien " + day + " - " + l);
                Log.Info("Budowy: dzien " + day + " - osad " + towns + ": w budowie " + (going + stuck + done) + " (idzie " + going + ", stoi " + stuck + ", ukonczonych poziomow " + done
                         + "), bez budowy " + idle + "; srednia moc " + (going + stuck + done > 0 ? (power / (going + stuck + done)).ToString("0.#") : "-") + " pkt/dzien.");
            }
            catch (Exception e) { Log.Error("BuildDiary", e); }
        }
    }
}
