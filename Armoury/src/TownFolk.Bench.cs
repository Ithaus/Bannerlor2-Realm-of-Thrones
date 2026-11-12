using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 435-L8 (lancuch podazy, krok 8 - STANOWISKA WARSZTATOW; WorkshopBenches.cs): zaplata raty za stanowisko z KAPITALU WARSZTATU - ta sama droga materialu co roboty
    /// gminy i sprzet wsi (BuildFunding.DsWorksBuy: prawdziwe sztuki z polki miasta, zapasy polki i sufit ceny jak dotad, ramka ksiegi towarow FBuild), ta sama regula
    /// rzemieslnikow (TownFolk.Earn KBuildLabour: lud przyjmuje, czesc zatrzymana regula f5 zostaje w kasie miasta - "wolajacy daje kasie amt - wynik").
    /// Platnik: kapital warsztatu (Workshop.ChangeGold); odbiorcy: kasa WLASNEGO miasta (material) i kiesy ludu tego miasta (rzemieslnicy). Kiesa wlasciciela nietknieta.
    /// Brak materialu na polce - nic nie schodzi (zadnej drogi "same place"); wyjatek: `wageOnly` - koncowka ceny poziomu mniejsza od sztuki materialu, same place.
    /// Wolajacy pilnuje, zeby `matBudget` + place (1:1) miescily sie w kapitale ponad znacznik. Kapital i kasa miasta zmierzone na zywych kiesach, strona ludu to
    /// wynik Earn (435-L8-p: kontrola dowodzi zgodnosci kapital - kasa; kies ludu nie mierzy) (kontrola w linii
    /// "LP warsztaty"). Watek glowny, nie rzuca.
    /// </summary>
    internal static partial class TownFolk
    {
        internal sealed class BenchPay
        {
            public int Why = GfErr, Pcs, Drop;
            public long Mat;         // material po cenie polki -> kasa miasta
            public long WageFolk;    // rzemieslnicy - przyjete przez lud miasta
            public long WageCof;     // rzemieslnicy - czesc nieprzyjeta, zostala w kasie miasta
            public long Gap;         // kontrola: z kapitalu warsztatu - (kasa miasta + lud); ma byc 0
            public long Paid { get { return Mat + WageFolk + WageCof; } }
        }

        /// <summary>
        /// `matBudget` - zloto materialu (najwyzej), place rzemieslnikow = tyle samo co material naprawde kupiony (polowa ceny stanowiska to material, polowa praca);
        /// `items` - materialy w kolejnosci BuildFunding.DsWorksItems (null = tego materialu nie kupowac); `wageOnly` &gt; 0 - koncowka: same place, bez zakupu.
        /// </summary>
        internal static BenchPay BenchPayRun(Workshop w, Settlement st, double matBudget, ItemObject[] items, long wageOnly)
        {
            var r = new BenchPay();
            try
            {
                if (!DaWorksOn || OffMain()) { r.Why = GfRoad; return r; }
                if (w == null || st == null || st.Town == null || items == null) { r.Why = GfSmall; return r; }
                Folk mf = null;
                _f.TryGetValue(st, out mf);
                if (mf == null || mf.Kind != 0) { r.Why = GfNoFolk; return r; }
                long cap0 = w.Capital, c0 = st.Town.Gold;
                long spent = 0, wage;
                if (wageOnly > 0)
                {
                    wage = Math.Min(wageOnly, Math.Max(0L, cap0));
                    if (wage <= 0) { r.Why = GfNoGold; return r; }
                }
                else
                {
                    if (matBudget * 2.0 > cap0) matBudget = Math.Floor(cap0 / 2.0);   // warsztat ma cala kwote przed zdjeciem sztuk z polki
                    if (!(matBudget >= 1)) { r.Why = GfNoGold; return r; }
                    var pc = new int[Math.Max(BuildFunding.DsWorksMatCount, 1)];
                    int cap = 0, drop = 0; bool none = false; int sp = 0;
                    var gf = GoodsLedger.Begin(GoodsLedger.FBuild, st);            // ksiega towarow: material na rynek miasta (jak roboty gminy)
                    if (gf != null) gf.Info = "stanowiska warsztatow";
                    try { sp = BuildFunding.DsWorksBuy(st, matBudget, items, pc, out cap, out none, out drop); }
                    finally { GoodsLedger.End(gf); }
                    for (int i = 0; i < pc.Length; i++) r.Pcs += pc[i];
                    r.Drop = drop;
                    SupplySignal.NoteWorksGot(st, items, pc, SupplySignal.SWorks);  // sztuki kupione wedlug towaru (sam licznik)
                    // 435-L8-p: polka ma material ponad zapas i ponizej sufitu ceny, a nic nie kupiono - budzet materialu mniejszy od sztuki (nie "brak materialu")
                    if (sp <= 0) { r.Why = none ? GfNoShelf : (cap > 0 ? GfPriceCap : GfPiece); return r; }
                    spent = sp; wage = spent;
                    if (spent + wage > cap0) wage = Math.Max(0L, cap0 - spent);
                }
                int total = (int)Math.Min(int.MaxValue, spent + wage);
                w.ChangeGold(-total);                                              // kapital warsztatu -> kasa miasta: material i place (lud bierze swoja czesc z kasy nizej)
                st.Town.ChangeGold(total);
                // 435-L8-p (recenzje 4): przelew zrobiony - wynik zapisany OD RAZU (wyjatek nizej nie gubi raty: Book dostaje to, co zeszlo z kapitalu; place
                // nieprzyjete przez lud zostaja w kasie miasta i sa poprawiane po Earn)
                r.Mat = spent; r.WageCof = wage; r.WageFolk = 0;
                WorkshopTrade.ShiftCapEnd(w, -total);                              // to nie wyplata zysku wlascicielowi (linia "Warsztaty towarowe")
                if (spent > 0) mf.Sale[MixBuild] += spent;                         // sprzedaz kupcow miasta - podstawa robocizny (jak material robot gminy)
                long fin = 0;
                if (wage > 0)
                {
                    fin = Math.Max(0, Math.Min(wage, (long)Earn(st, (int)Math.Min(int.MaxValue, wage), KBuildLabour)));
                    if (fin > 0) st.Town.ChangeGold(-(int)fin);                    // kasa miasta -> lud (rzemieslnicy); reszta zostaje w kasie (regula f5)
                }
                long capOut = cap0 - w.Capital, cofIn = (long)st.Town.Gold - c0;
                r.Mat = spent; r.WageFolk = fin; r.WageCof = Math.Max(0, wage - fin);
                r.Gap = capOut - (cofIn + fin);
                r.Why = GfOk;
                return r;
            }
            catch (Exception e) { DaStumble("BenchPayRun", e); if (r.Mat <= 0 && r.WageFolk <= 0 && r.WageCof <= 0) r.Why = GfErr; return r; }
        }
    }
}
