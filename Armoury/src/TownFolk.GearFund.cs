using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 435-L7 (lancuch podazy, krok 7 - FUNDATORZY; VillageGear.Fund.cs): zaplata obcego fundatora za sprzet podstawowy wsi - TA SAMA droga materialu co roboty gminy
    /// i przebudowa zajec (BuildFunding.DsWorksBuy: prawdziwe sztuki z polki miasta targowego, zapasy polki i sufit ceny jak dotad, ramka ksiegi towarow FBuild), ta sama
    /// regula rzemieslnikow (TownFolk.Earn KBuildLabour: lud przyjmuje, czesc zatrzymana regula f5 zostaje w kasie targu - "wolajacy daje kasie amt - wynik").
    /// Platnik: bohater (pan wsi AI albo gracz) albo - `payer` null - KASA miasta targowego (kupcy): material to wtedy wlasny towar kupcow (sztuki schodza z polki,
    /// moneta sie nie rusza), z kasy schodza tylko place rzemieslnikow i praca wsi, i tylko z kasy ponad zapas kupcow (TownPurse.Reserve).
    /// Brak materialu na polce - nic nie schodzi (zadnej drogi "same place", krytyka B2). Platnik musi miec cala kwote PRZED zdjeciem sztuk z polki.
    /// Wszystkie strony zmierzone na zywych kiesach (kontrola w linii "LP czynsz"). Watek glowny, nie rzuca.
    /// </summary>
    internal static partial class TownFolk
    {
        internal const int GfOk = 0, GfRoad = 1, GfNoFolk = 2, GfSmall = 3, GfNoShelf = 4, GfPriceCap = 5, GfNoGold = 6, GfErr = 7, GfKinds = 8;

        internal sealed class GearPay
        {
            public int Why = GfErr, Pcs, Drop;
            public long Paid;        // wklad fundatora (wierzytelnosc): material + place + praca wsi
            public long Mat;         // material po cenie polki (miasto-fundator: wartosc wlasnego towaru, bez monety)
            public long WageFolk;    // rzemieslnicy - przyjete przez lud targu
            public long WageCof;     // rzemieslnicy - czesc nieprzyjeta, zostala w kasie targu (bohater-fundator)
            public long Work;        // praca wsi - do kiesy wsi
            public long Gap;         // kontrola: z kiesy platnika - (kasa targu + lud + kiesa wsi); ma byc 0
        }

        /// <summary>
        /// `matBudget` - zloto materialu (najwyzej), `k` - rzemieslnicy na zlotowke materialu, `workPerMat` - praca wsi na zlotowke materialu, `outsideTick` - zakup
        /// gracza z menu (poza blokiem doby: przelew kasa -> lud nazwany w ksiedze przeplywow).
        /// </summary>
        internal static GearPay GearFundPay(Hero payer, Village vil, Settlement mk, double matBudget, double k, double workPerMat, bool outsideTick)
        {
            var r = new GearPay();
            bool gotMat = false; int pay0 = 0; Hero payerRef = null;
            try
            {
                if (!DaWorksOn || OffMain()) { r.Why = GfRoad; return r; }
                if (vil == null || vil.Settlement == null || mk == null || mk.Town == null || !(matBudget >= 1)) { r.Why = GfSmall; return r; }
                Folk mf = null;
                _f.TryGetValue(mk, out mf);
                if (mf == null || mf.Kind > 1 || (mf.Kind == 1 && !CastleLive)) { r.Why = GfNoFolk; return r; }
                if (double.IsNaN(k) || k < 0) k = 0; if (k > 3.0) k = 3.0;
                if (double.IsNaN(workPerMat) || workPerMat < 0) workPerMat = 0; if (workPerMat > 20.0) workPerMat = 20.0;
                // platnik ma cala kwote przed zakupem (sztuki zdjete z polki sa zawsze zaplacone)
                long have = payer != null ? Math.Max(0, payer.Gold) : Math.Max(0L, (long)mk.Town.Gold - TownPurse.Reserve(mk.Town));
                double perGold = payer != null ? 1.0 + k + workPerMat : k + workPerMat;
                if (perGold > 0 && matBudget * perGold > have) matBudget = Math.Floor(have / perGold);
                if (!(matBudget >= 1)) { r.Why = GfNoGold; return r; }
                var items = BuildFunding.DsWorksItems();
                var pc = new int[Math.Max(BuildFunding.DsWorksMatCount, 1)];
                int spent = 0, cap = 0, drop = 0; bool none = false;
                var gf = GoodsLedger.Begin(GoodsLedger.FBuild, mk);            // ksiega towarow: material na rynek osady targu (jak roboty gminy)
                if (gf != null) gf.Info = "sprzet wsi (fundator)";
                try { spent = BuildFunding.DsWorksBuy(mk, matBudget, items, pc, out cap, out none, out drop); }
                finally { GoodsLedger.End(gf); }
                for (int i = 0; i < pc.Length; i++) r.Pcs += pc[i];
                r.Drop = drop;
                SupplySignal.NoteWorksGot(mk, items, pc, SupplySignal.SWorks);  // sztuki kupione wedlug towaru (sam licznik; brak liczy juz zlecenie gminy tej wsi)
                if (spent <= 0) { r.Why = !none && cap > 0 ? GfPriceCap : GfNoShelf; return r; }
                long wage = (long)Math.Floor(k * spent), work = (long)Math.Floor(workPerMat * spent);
                int c0 = mk.Town.Gold, v0 = vil.Gold, p0 = payer != null ? payer.Gold : 0;
                // 435-L7-p (recenzje N7 / stab. 4): sztuki juz zeszly z polki - material zapisany OD RAZU; wyjatek dalej = Why GfErr z materialem i zaplata zmierzona
                // w catch na zywej kiesie (wolajacy przyznaje jednostki za zaplacony material - nic w proznie)
                r.Mat = spent; gotMat = true; pay0 = p0; payerRef = payer;
                if (payer != null)
                {
                    // bohater -> kasa targu: material i place rzemieslnikow (lud bierze swoja czesc z kasy nizej)
                    double car0 = mf.CarBuy;
                    GiveGoldAction.ApplyForCharacterToSettlement(payer, mk, (int)Math.Min(int.MaxValue, spent + wage), true);
                    // 435-L7-p (recenzje N1 / stab. 2): zakup GRACZA z menu nasluch zlota liczy juz jako zakup w miescie (CarBuy = material + place) - bez drugiego
                    // dopisania do sprzedazy kupcow; place rzemieslnikow ida do ludu wprost (Earn nizej), wiec schodza z tej podstawy. Pan AI - jak dotad (Sale).
                    double carAdd = mf.CarBuy - car0;
                    if (carAdd > 0) mf.CarBuy -= Math.Min(carAdd, (double)wage);
                    else mf.Sale[MixBuild] += spent;                            // sprzedaz kupcow targu - podstawa robocizny (jak material robot gminy)
                }
                long fin = 0;
                if (wage > 0)
                {
                    fin = Math.Max(0, Math.Min(wage, (long)Earn(mk, (int)Math.Min(int.MaxValue, wage), KBuildLabour)));
                    if (fin > 0)
                    {
                        mk.Town.ChangeGold(-(int)fin);                           // kasa targu -> lud (rzemieslnicy); reszta zostaje w kasie (regula f5)
                        if (outsideTick) MoneyLedger.Note169(MoneyLedger.N169Other, mk, -(int)fin);
                    }
                }
                if (work > 0)
                {
                    if (payer != null) GiveGoldAction.ApplyForCharacterToSettlement(payer, vil.Settlement, (int)work, true);
                    else { mk.Town.ChangeGold(-(int)work); vil.ChangeGold((int)work); }
                }
                long cofIn = (long)mk.Town.Gold - c0, vilIn = (long)vil.Gold - v0;
                r.Mat = spent; r.WageFolk = fin; r.Work = Math.Max(0, vilIn);
                if (payer != null)
                {
                    long outP = (long)p0 - payer.Gold;
                    r.WageCof = Math.Max(0, wage - fin);
                    r.Paid = Math.Max(0, outP);
                    r.Gap = outP - (cofIn + fin + vilIn);
                }
                else
                {
                    r.Paid = spent + fin + Math.Max(0, vilIn);
                    r.Gap = -cofIn - (fin + vilIn);
                }
                r.Why = GfOk;
                return r;
            }
            catch (Exception e)
            {
                DaStumble("GearFundPay", e);
                r.Why = GfErr;
                if (gotMat)
                {
                    // sztuki zeszly: wklad = to, co naprawde wyszlo z kiesy bohatera (miasto-fundator: wartosc wlasnego towaru)
                    try { r.Paid = payerRef != null ? Math.Max(0L, (long)pay0 - payerRef.Gold) : r.Mat; } catch { r.Paid = 0; }
                }
                return r;
            }
        }
    }
}
