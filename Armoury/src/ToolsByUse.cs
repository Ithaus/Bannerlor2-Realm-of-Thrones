using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// E6/R3 (215, PLAN 6.7, projekt etapu 6 v2 rozdz. 3 "E6/R3" (a); P174 rozdz. 9 "P7 narzedzia wedlug zuzycia") - NARZEDZIA WEDLUG ZUZYCIA.
    /// Dotad linia narzedzi (kuznia "smithy" gry: ruda -> 4 narzedzia przy 1.5; ukryci rzemieslnicy BK: ruda -> 2 narzedzia przy 0.05) szla zawsze, gdy cykl
    /// sie oplacal: 32 ladunki rudy/d na narzedzia, a stos narzedzi swiata rosl +23/d (RA6 rozdz. 5, d91-120). Teraz linia z wyrobem kategorii "tools"
    /// rusza cykl tylko, gdy na polce miasta lezy mniej niz StockDays (30) dni popytu miasta na narzedzia; przy wiekszym zapasie cykl nie idzie wcale
    /// (prefiks WorkshopLaw.CyclePrefix / PlayerCyclePrefix przed gra - wsad zostaje na polce, nic nie powstaje i nic nie znika).
    /// Popyt dobowy = popyt rynku gry na kategorie narzedzi (TownMarketData ItemData.Demand - wygladzony co dobe szacunek gry
    /// GetEstimatedDemandForCategory, przy HistDemandScaling w nowej monecie) / wartosc sztuki "tools"; zapas = ItemData.InStore (sztuki kategorii na polce,
    /// gra prowadzi go przy kazdej zmianie rosteru). Jeden odczyt slownika na cykl - bez petli po osadach. Start gry (GameStarted = false) - bez bramki.
    /// Warsztat gracza - ta sama regula przy PlayerWorkshopsSameRule (jedna regula gracz/AI, Jeff 09.10). Wylacznik ToolsByUse.
    /// </summary>
    internal static class ToolsByUse
    {
        internal const float StockDays = 30f;   // projekt E6/R3 (a): "zapas miasta < 30 dni zuzycia narzedzi"

        internal static bool On { get { var s = Settings.Current; return s != null && s.ToolsByUse; } }

        private static ItemObject _tools; private static bool _looked;
        private static int _dRun, _dHeld, _dInput, _dPlayerHeld, _stumbles, _errDay = -1;
        private static readonly HashSet<Town> _dTowns = new HashSet<Town>();
        private static readonly HashSet<string> _errWhere = new HashSet<string>();

        internal static void Reset()
        {
            _tools = null; _looked = false; _dRun = _dHeld = _dInput = _dPlayerHeld = _stumbles = 0; _errDay = -1;
            _dTowns.Clear(); _errWhere.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try
            {
                int d = (int)CampaignTime.Now.ToDays;
                if (d != _errDay) { _errDay = d; _errWhere.Clear(); }
                if (_errWhere.Add(where)) Log.Error("ToolsByUse." + where, e);
            }
            catch { }
        }

        private static ItemObject Tools()
        {
            if (!_looked)
            {
                _looked = true;
                try { var om = MBObjectManager.Instance; _tools = om != null ? om.GetObject<ItemObject>("tools") : null; } catch { _tools = null; }
                if (_tools == null) _looked = false;   // menedzer obiektow jeszcze nie gotowy - nastepny cykl sprobuje znowu
            }
            return _tools;
        }

        /// <summary>Linia z wyrobem kategorii narzedzi przy zapasie miasta &gt;= StockDays dni popytu - true (cykl nie idzie). Inne linie, brak danych - false.</summary>
        internal static bool Held(WorkshopType.Production p, Workshop w, bool player = false)
        {
            if (!On || w == null) return false;
            try
            {
                if (Campaign.Current == null || !Campaign.Current.GameStarted) return false;
                if (p.Outputs == null || p.Outputs.Count == 0) return false;
                ItemCategory cat = null;
                foreach (var o in p.Outputs) if (o.Item1 != null && o.Item1.StringId == "tools") { cat = o.Item1; break; }
                if (cat == null) return false;
                var st = w.Settlement; var town = st != null ? st.Town : null;
                if (town == null || town.MarketData == null) return false;
                var tools = Tools();
                int value = tools != null ? Math.Max(1, tools.Value) : 1;
                var data = town.MarketData.GetCategoryData(cat);
                float perDay = Math.Max(0f, data.Demand) / value;
                if (data.InStore <= 0 || data.InStore < StockDays * perDay) { _dRun++; return false; }
                _dHeld++; _dTowns.Add(town);
                if (player) _dPlayerHeld++;
                if (p.Inputs != null) foreach (var i in p.Inputs) _dInput += Math.Max(0, i.Item2);
                return true;
            }
            catch (Exception e) { Stumble("Held", e); return false; }
        }

        /// <summary>Linia doby "Narzedzia wedlug zuzycia (215)" (z ArmouryBehavior, raz na dobe) - liczniki od poprzedniej linii.</summary>
        internal static void Daily()
        {
            try
            {
                int day = (int)CampaignTime.Now.ToDays;
                if (!On && _dRun + _dHeld == 0) { _stumbles = 0; return; }
                Log.Info("Narzedzia wedlug zuzycia (215): dzien " + day + " | " + (On ? "CZYNNE" : "WYLACZONE") + " | cykle linii narzedzi: puszczone " + _dRun
                         + ", wstrzymane " + _dHeld + " (zapas na polce >= " + StockDays.ToString("0", CultureInfo.InvariantCulture) + " dni popytu miasta; w tym warsztat gracza "
                         + _dPlayerHeld + ") w " + _dTowns.Count + " miastach, wsad niezuzyty " + _dInput + " szt. (zostaje na polce) | potkniecia dzis " + _stumbles + ".");
            }
            catch (Exception e) { Log.Error("ToolsByUse.Daily", e); }
            _dRun = _dHeld = _dInput = _dPlayerHeld = _stumbles = 0; _dTowns.Clear();
        }
    }
}
