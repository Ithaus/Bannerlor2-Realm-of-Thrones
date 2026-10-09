using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// WOJSKO SAMO LATA SWOJ SPRZET (Jeff 29.08: "dostaja zold i czesc lupow,
    /// niech za to naprawiaja; jak im nie starczy, to ja moge"). Kazdego dnia
    /// w MIESCIE (jest kowal) zolnierze oddaja do naprawy najgorsze sztuki
    /// z magazynu kwatermistrza - placa ze swojego zoldu, gracz nie wydaje
    /// ani grosza. Po przerobce 30.08 (Jeff: "10 szt./dzien to bez sensu,
    /// niech naprawiaja PROCENT uszkodzen") dzienna robota to PROCENT calej
    /// puli zuzytych sztuk (min. 3, zeby ogon nie wisial wiecznie) - pelny
    /// remont trwa ~100/procent dni postoju NIEZALEZNIE od wielkosci armii.
    /// Kto sie spieszy, placi kowalowi (Send the men's worn gear...).
    /// </summary>
    internal static class TroopSelfMend
    {
        // ------------------------------------------------------------ wpis 84: godzinowo, z sakiewki ludzi
        // Jeff 05.10: "maja kase, to niech naprawiaja, na ile ich stac; a co jak wyjde z miasta?". Ludzie oddaja kowalom miasta
        // najgorsze sztuki; kowale maja tyle godzin, ile rak (rece rzemieslnikow x udzial platnerzy i miecznikow), w nocy spia.
        // Gotowa sztuka - zaplacona z sakiewki ludzi do kasy miasta. Wyjazd: to, co na warsztacie, ludzie zabieraja nienaprawione
        // i nikt za to nie placi (postep sztuki przepada, nic wiecej). Wraki (<10%) - tylko kowal z materialem albo przetop.
        private static float _bench;
        private static string _benchTown;

        internal static void LeftTown() { _bench = 0f; _benchTown = null; }

        internal static int UnitCost(EquipmentElement ee)   // internal: robocizna kowali miasta - ta sama stawka u kwatermistrza Spoils (SpoilsSeal)
        {
            return UnitCost(ee, Settlement.CurrentSettlement);
        }

        /// <summary>Robota kowali miasta st za jedna sztuke: przy regule kowali miasta (MendMaterial.RuleOn) ulamek wykonania po dniowce
        /// mistrza w tym miescie (MendMaterial.Labor - ta sama co na lawie); inaczej jak dotad 25% utraconej wartosci.</summary>
        internal static int UnitCost(EquipmentElement ee, Settlement st)
        {
            int old = Math.Max(1, (int)(ee.Item.Value * (1f - ee.ItemModifier.PriceMultiplier) * 0.25f));
            return MendMaterial.RuleOn ? MendMaterial.Labor(ee.Item, MendMaterial.Share(ee), st, old) : old;
        }

        private static bool Mendable(ItemRosterElement el)
        {
            var mod = el.EquipmentElement.ItemModifier;
            return el.Amount > 0 && el.EquipmentElement.Item != null && mod != null && mod.PriceMultiplier < 1f && !LootPrices.IsWreck(mod)   // wpis 97
                   && !ArmouryBehavior.IsBeast(el.EquipmentElement.Item);   // poprawka po recenzji 159: kon i zwierze zachowuja stan - kowal nie "leczy" kulawego konia (RBM lame_horse 0.5)
        }

        /// <summary>K1 (przeglad): obite sztuki LUDZI do naprawy za ich pieniadze. Ksiega wkladow gracza jest per id i obejmuje NAJGORSZE
        /// egzemplarze (ta sama regula co SwapMath.AllocateOwn w QuartermasterLaw.KitPieces: wrak, potem gorszy stan), wiec kowale brali
        /// dotad od najgorszej sztuki - czyli najpierw Twoja - a placila sakiewka ludzi; rezerwa na te naprawy zjadala im budzet na braki
        /// i lepszy sprzet. Teraz Twoja czesc kazdego id jest pomijana (naprawiasz ja sam u kowala).</summary>
        internal static List<ItemRosterElement> MenWorn(ItemRoster armory)
        {
            var worn = new List<ItemRosterElement>();
            if (armory == null) return worn;
            var mine = new Dictionary<string, List<ItemRosterElement>>();
            for (int i = 0; i < armory.Count; i++)
            {
                var el = armory.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (el.Amount <= 0 || it == null || it.StringId == null) continue;
                if (ArmouryBehavior.StockOf(it.StringId) <= 0) { if (Mendable(el)) worn.Add(el); continue; }
                List<ItemRosterElement> l;
                if (!mine.TryGetValue(it.StringId, out l)) mine[it.StringId] = l = new List<ItemRosterElement>();
                l.Add(el);
            }
            foreach (var kv in mine)
            {
                var l = kv.Value;
                l.Sort((a, b) =>
                {
                    var ma = a.EquipmentElement.ItemModifier; var mb = b.EquipmentElement.ItemModifier;
                    int d = (mb != null && LootPrices.IsWreck(mb) ? 1 : 0).CompareTo(ma != null && LootPrices.IsWreck(ma) ? 1 : 0); if (d != 0) return d;
                    d = (ma != null ? ma.PriceMultiplier : 1f).CompareTo(mb != null ? mb.PriceMultiplier : 1f); if (d != 0) return d;
                    return string.CompareOrdinal(ma != null ? ma.StringId ?? "" : "", mb != null ? mb.StringId ?? "" : "");
                });
                int left = ArmouryBehavior.StockOf(kv.Key);
                foreach (var el in l)
                {
                    int own = Math.Min(el.Amount, Math.Max(0, left)); left -= own;
                    int men = el.Amount - own;
                    if (men > 0 && Mendable(el)) worn.Add(new ItemRosterElement(el.EquipmentElement, men));
                }
            }
            return worn;
        }

        /// <summary>Ile kosztowalyby wszystkie zalegle naprawy (bez wrakow) - tyle ludzie trzymaja w sakiewce.</summary>
        internal static int OutstandingCost() { return OutstandingCost(null); }

        /// <summary>To samo; przy naprawach z materialem (MendMaterial.MenAndLordsOn) i podanym miescie - robocizna + szacunek materialu
        /// z polki tego miasta (Bench.Estimate - ta sama cena co na lawie), sztuki bez receptury kowala pominiete (kowale ich nie naprawia).
        /// Robocizna jak dotad (UnitCost(ee) - stawka biezacej osady).</summary>
        internal static int OutstandingCost(Settlement st)
        {
            int sum = 0;
            try
            {
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) return 0;
                MendMaterial.Bench bench = MendMaterial.MenAndLordsOn && st != null && st.IsTown ? new MendMaterial.Bench(st) : null;
                foreach (var el in MenWorn(armory))   // K1 (przeglad): bez Twojej czesci
                {
                    int unit = UnitCost(el.EquipmentElement);
                    if (bench != null && bench.Ok)
                    {
                        var need = MendMaterial.Needs(el.EquipmentElement);
                        if (need == null) continue;
                        unit += MendMaterial.Gold(bench.Estimate(need));
                    }
                    sum += unit * el.Amount;
                }
            }
            catch { }
            return sum;
        }

        internal static void Hourly()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.TroopSelfMendEnabled || !MenPurse.On) return;
                var main = MobileParty.MainParty;
                var st = main != null ? main.CurrentSettlement : null;
                if (st == null || !st.IsTown || st.Town == null) { _bench = 0f; _benchTown = null; return; }
                if (_benchTown != st.StringId) { _bench = 0f; _benchTown = st.StringId; }
                if (s.WorkshopNightRest) { int hh = TaleWorlds.CampaignSystem.CampaignTime.Now.GetHourOfDay; if (hh >= 23 || hh < 5) return; }
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) return;
                var worn = MenWorn(armory);   // K1 (przeglad): tylko sztuki ludzi - Twoja czesc kazdego id (najgorsze egzemplarze) pomijana
                if (worn.Count == 0) { _bench = 0f; return; }
                worn.Sort((a, b) => a.EquipmentElement.ItemModifier.PriceMultiplier.CompareTo(b.EquipmentElement.ItemModifier.PriceMultiplier));
                // godziny kowali na godzine: rece miasta x (platnerze + miecznicy) / wszystkie cechy
                // wpis 91: godziny z WSPOLNEJ puli kowali miasta (dzienny zapas rozlozony na 18 godzin pracy)
                float hourShare = Math.Min(SmithHours.Available(st.Town), SmithHours.Capacity(st.Town) / Math.Max(1f, s.WorkHoursPerManDay));   // wpis 93: dzien pracy kowala rozlozony na jego godziny
                _bench += hourShare;
                float per = Math.Max(0.05f, s.MendLootHoursPerPiece);
                if (MendMaterial.MenAndLordsOn) { HourlyWithMaterial(main, st, armory, worn, hourShare, per); return; }   // poprawka po audycie TOWARY 3
                int mended = 0, paid = 0;
                bool broke = false;
                foreach (var el in worn)
                {
                    int left = el.Amount, fixedN = 0;
                    while (left > 0 && _bench >= per)
                    {
                        int unit = UnitCost(el.EquipmentElement, st);
                        if (MenPurse.Get(main) < unit) { broke = true; break; }
                        MenPurse.Take(main, unit); st.Town.ChangeGold(unit);
                        MoneyLedger.Note169(MoneyLedger.N169Repair, st, unit);   // paczka 169: linia kas (tylko licznik)
                        _bench -= per; left--; fixedN++; paid += unit;
                    }
                    if (fixedN > 0)
                    {
                        armory.AddToCounts(el.EquipmentElement, -fixedN);
                        armory.AddToCounts(new EquipmentElement(el.EquipmentElement.Item), fixedN);
                        mended += fixedN;
                    }
                    if (broke || _bench < per) break;
                }
                if (broke) _bench = Math.Min(_bench, per);   // nie ma czym zaplacic - kowale nie trzymaja godzin na zapas
                SmithHours.Use(st.Town, broke ? mended * per : hourShare);   // godziny kowali tej godziny (bez zaplaty - tylko gotowe)
                _hourMended += mended; _hourPaid += paid;
                if (_hourMended > 0 && (TaleWorlds.CampaignSystem.CampaignTime.Now.GetHourOfDay == 22 || broke))
                {
                    Log.Player("The smiths of " + st.Name + " mended " + _hourMended + " pieces of your men's kit today for " + _hourPaid
                               + " denars from the men's purse." + (broke ? " The men's purse is empty - the rest waits (or pay the smith yourself)." : ""), true);
                    Log.Info("TroopSelfMend: " + st.Name + " - naprawiono " + _hourMended + " szt. za " + _hourPaid + " z sakiewki ludzi" + (broke ? " (sakiewka pusta)" : "") + ".");
                    _hourMended = 0; _hourPaid = 0;
                }
            }
            catch (Exception e) { Log.Error("TroopSelfMend.Hourly", e); }
        }
        private static int _hourMended, _hourPaid;

        // ------------------------------------------------------------ poprawka po audycie TOWARY 3 (krok 139 planu K13): naprawy ludzi z materialem
        // Do 135 kazda naprawa u kowali miasta byla sama robocizna; 135 dal lawie (i kwatermistrzowi) material z polki, a ludzie dalej naprawiali
        // ta sama sztuke w tej samej kuzni bez grama materialu - dwie ceny jednej naprawy i material z niczego. Teraz ta sama regula co na lawie:
        // MendMaterial.Order (material wedle stanu z polki miasta / zapasu kowali, po cenie targu; brak - sztuka czeka; wraki i sztuki bez
        // receptury kowala - nie). Godziny kowali jak dotad (gdy robota stanela - tylko za gotowe sztuki). Zaplata (robota + material w calych
        // pensach) z sakiewki ludzi do kasy miasta - za to, co naprawde zrobione (najpierw plan na kopii, potem Commit i zaplata).
        private static int _hourMat;
        private static readonly float[] _hourKg = new float[MendMaterial.Kinds];

        private static void HourlyWithMaterial(MobileParty main, Settlement st, ItemRoster armory, List<ItemRosterElement> worn, float hourShare, float per)
        {
            var o = new MendMaterial.Order(st);
            int purse = MenPurse.Get(main);
            bool broke = false;
            foreach (var el in worn)
            {
                if (_bench < per) break;
                var ee = el.EquipmentElement;
                int n = Math.Min(el.Amount, (int)Math.Floor(_bench / per + 0.0001f));   // tyle sztuk tego stosu, na ile starczy godzin kowali
                if (n <= 0) break;
                int poor0 = o.Poor;
                int planned = o.AddLot(ee, MendMaterial.Needs(ee), UnitCost(ee, st), n, purse, int.MaxValue);   // brak materialu: reszta stosu czeka, nastepny stos dalej
                _bench -= planned * per;
                if (o.Poor > poor0) { broke = true; break; }
            }
            int mended = 0, paid = 0;
            if (o.Pieces > 0)
            {
                o.Bench.Commit();
                foreach (var j in o.Jobs)
                {
                    armory.AddToCounts(j.El, -j.N);
                    armory.AddToCounts(new EquipmentElement(j.El.Item), j.N);
                    mended += j.N;
                }
                paid = MenPurse.Take(main, o.Total);
                st.Town.ChangeGold(paid);
                MoneyLedger.Note169(MoneyLedger.N169Repair, st, paid);   // paczka 169: linia kas (tylko licznik)
                for (int k = 0; k < MendMaterial.Kinds; k++) _hourKg[k] += o.Bench.UsedKg[k];
                _hourMat += o.MatGold;
            }
            // kowale stoja (pusta sakiewka, brak materialu, nic do roboty): godziny tylko za gotowe sztuki, bez trzymania na zapas
            bool idle = broke || _bench >= per;
            if (idle) _bench = Math.Min(_bench, per);
            SmithHours.Use(st.Town, idle ? mended * per : hourShare);
            _hourMended += mended; _hourPaid += paid;
            int hour = TaleWorlds.CampaignSystem.CampaignTime.Now.GetHourOfDay;
            if ((_hourMended > 0 || o.Wait > 0) && (hour == 22 || (broke && _hourMended > 0)))
            {
                string wait = o.Wait > 0 ? " " + o.Wait + (o.Wait == 1 ? " piece waits" : " pieces wait") + " for materials - the market of " + st.Name + " has not enough "
                                           + MendMaterial.KindsEn(o.WaitMask) + "." : "";
                if (_hourMended > 0)
                    Log.Player("The smiths of " + st.Name + " mended " + _hourMended + " pieces of your men's kit today for " + _hourPaid
                               + " denars from the men's purse (work and materials)." + (broke ? " The men's purse is empty - the rest waits (or pay the smith yourself)." : "") + wait, true);
                else Log.Player("The smiths of " + st.Name + " could not mend your men's kit today." + wait, true);
                Log.Info("TroopSelfMend: " + st.Name + " - naprawiono " + _hourMended + " szt. za " + _hourPaid + " z sakiewki ludzi (w tym material " + _hourMat
                         + " zl; zuzyto kg: metal " + F(_hourKg[MendMaterial.Metal]) + ", drewno " + F(_hourKg[MendMaterial.Wood]) + ", skora " + F(_hourKg[MendMaterial.Leather])
                         + ", plotno " + F(_hourKg[MendMaterial.Cloth]) + ")" + (broke ? " (sakiewka pusta)" : "") + "; czeka na material " + o.Wait
                         + " (brak: " + MendMaterial.KindsPl(o.WaitMask) + "), nie robota kowala " + o.NoSmith + ".");
                _hourMended = 0; _hourPaid = 0; _hourMat = 0;
                for (int k = 0; k < MendMaterial.Kinds; k++) _hourKg[k] = 0f;
            }
        }

        private static string F(float v) { return v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture); }

        internal static void Run(Settlement st)
        {
            if (MenPurse.On) return;   // wpis 84: naprawy godzinowe z sakiewki ludzi (Hourly)
            try
            {
                var s = Settings.Current;
                if (s == null || !s.TroopSelfMendEnabled || s.TroopSelfMendPercentPerDay <= 0) return;
                var main = MobileParty.MainParty;
                if (main == null || st == null || main.CurrentSettlement != st) return;
                if (!st.IsTown) return;   // naprawa wymaga kowala z prawdziwym warsztatem

                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) return;

                // najgorsze sztuki na wierzch - je lataja najpierw
                var worn = new List<ItemRosterElement>();
                for (int i = 0; i < armory.Count; i++)
                {
                    var el = armory.GetElementCopyAtIndex(i);
                    var mod = el.EquipmentElement.ItemModifier;
                    if (el.Amount <= 0 || mod == null || mod.PriceMultiplier >= 1f) continue;
                    if (LootPrices.IsWreck(mod)) continue;   // audyt pelny K2: wrak - tylko kowal z materialem albo przetop
                    if (el.EquipmentElement.Item == null) continue;
                    if (ArmouryBehavior.IsBeast(el.EquipmentElement.Item)) continue;   // poprawka po recenzji 159: kon i zwierze zachowuja stan
                    worn.Add(el);
                }
                if (worn.Count == 0) return;
                worn.Sort((a, b) => a.EquipmentElement.ItemModifier.PriceMultiplier
                    .CompareTo(b.EquipmentElement.ItemModifier.PriceMultiplier));

                int wornTotal = 0;
                foreach (var el in worn) wornTotal += el.Amount;
                int budget = Math.Max(3, (int)Math.Round(wornTotal * s.TroopSelfMendPercentPerDay / 100.0));
                if (budget > wornTotal) budget = wornTotal;
                if (MendMaterial.MenAndLordsOn) { RunWithMaterial(st, armory, worn, budget); return; }   // poprawka po audycie TOWARY 3: material jak na lawie
                int mended = 0, paidAll = 0;
                foreach (var el in worn)
                {
                    if (budget <= 0) break;
                    // audyt pelny K2: naprawa PLATNA miastu (robocizna jak u kowala: 25% utraconej wartosci) - wczesniej za darmo
                    int unit = UnitCost(el.EquipmentElement, st);   // ta sama robota co w Hourly i na lawie
                    int afford = Math.Max(0, TaleWorlds.CampaignSystem.Hero.MainHero.Gold - paidAll) / unit;
                    int take = Math.Min(Math.Min(budget, el.Amount), afford);
                    if (take <= 0) break;
                    paidAll += unit * take;
                    armory.AddToCounts(el.EquipmentElement, -take);
                    armory.AddToCounts(new EquipmentElement(el.EquipmentElement.Item), take);   // czysty stan
                    mended += take; budget -= take;
                }
                if (paidAll > 0) Pay.ToSettlement(paidAll);
                if (mended > 0)
                {
                    Log.Info("TroopSelfMend: wojsko naprawilo " + mended + " sztuk w " + st.Name + " za " + paidAll + " zl do kasy miasta.");
                    Log.Player("The men see to their own kit at " + st.Name + " - " + mended
                               + " pieces mended by the town smiths for " + paidAll + " gold.", true);
                }
            }
            catch (Exception e) { Log.Error("TroopSelfMend.Run", e); }
        }

        /// <summary>Run przy naprawach z materialem: ten sam plan co Hourly (MendMaterial.Order), zaplata z kiesy gracza do kasy miasta.</summary>
        private static void RunWithMaterial(Settlement st, ItemRoster armory, List<ItemRosterElement> worn, int budget)
        {
            var o = new MendMaterial.Order(st);
            long gold = Math.Max(0, TaleWorlds.CampaignSystem.Hero.MainHero.Gold);
            foreach (var el in worn)
            {
                if (o.Pieces >= budget) break;
                var ee = el.EquipmentElement;
                o.AddLot(ee, MendMaterial.Needs(ee), UnitCost(ee, st), el.Amount, gold, budget);
                if (o.Poor > 0) break;
            }
            if (o.Pieces <= 0)
            {
                if (o.Wait > 0) Log.Info("TroopSelfMend: " + st.Name + " - nic nie naprawiono, czeka na material " + o.Wait + " (brak: " + MendMaterial.KindsPl(o.WaitMask) + ").");
                return;
            }
            o.Bench.Commit();
            int mended = 0;
            foreach (var j in o.Jobs)
            {
                armory.AddToCounts(j.El, -j.N);
                armory.AddToCounts(new EquipmentElement(j.El.Item), j.N);
                mended += j.N;
            }
            int paid = o.Total;
            Pay.ToSettlement(paid);
            Log.Info("TroopSelfMend: wojsko naprawilo " + mended + " sztuk w " + st.Name + " - " + o.LogPl());
            Log.Player("The men see to their own kit at " + st.Name + " - " + mended + " pieces mended by the town smiths for " + paid + " gold (work and materials)."
                       + o.LeftEn(st.Name.ToString()), true);
        }
    }
}
