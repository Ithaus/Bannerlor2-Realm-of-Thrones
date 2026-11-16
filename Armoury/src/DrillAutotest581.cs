using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 581 (at-268 P-A1, analiza A + weryfikacja 16.11): AUTOTEST MUSZTRY GRACZA. Gracz nowej kampanii testu nie ma zolnierzy (ludzie dokladani do bitew
    /// wracaja po walce), wiec AutotestFeed (562) wychodzil przy men &lt;= 0 przez 220 dob i strona gracza 562/571/574/575 nie byla sprawdzona ani razu.
    /// TYLKO w autotescie (ten sam warunek co AutotestFeed: AutotestActive - CrashScribe.Autotest.Active, ustawiane tylko przy pliku autotest.json; w grze
    /// Jeffa false), od doby 2, gdy druzyna nie ma zolnierzy:
    ///  - na stale mieszany oddzial kultury gracza: rekruci (BasicTroop), kusznicy (albo lucznicy) najnizszego tieru, jazda najnizszego tieru z drzewcem
    ///    1H (kopia / wlocznia F1H) - przyciete do wolnego miejsca w druzynie (gra zwalnia ludzi ponad limit); zasilenie 562 wylaczone (_fed);
    ///  - do zbrojowni DTE na LISTE GRACZA (ta sama para co 576: AddToCounts + StockDepositKey) na kazdy rodzaj zestawu: 2 x popyt + 2 sztuk najtanszych,
    ///    ktore udzwigna wszyscy z ta rola (bitwa wydaje polowe - KeepFor - druga polowa idzie do zapasu musztry); helmy - sztuka z gornej tercji ceny
    ///    (drogie w zapasie do wymiany 575); korpus i 1H - po 3 sztuki T6; bron dystansowa oddzialu - TYLKO sztuki z wymogiem umiejetnosci ponad umiejetnosc
    ///    typu (574 "za trudne");
    ///  - co 3 doby (najwyzej 8 razy) 2 sztuki tanszego egzemplarza tej samej klasy (KindInfo, FitsSuper), co najdrozsza sztuka zapasu, ktora ma tansze
    ///    zastepstwo - wymiana 575;
    ///  - utrzymanie oddzialu: zboze do 10 dni jedzenia przy zapasie ponizej 5 dni, zloto 7 x zold przy kiesie ponizej 3 x zold (inaczej glod i zold
    ///    rozpedzaja oddzial przed koncem testu).
    /// Wszystko powyzej powstaje z niczego - swiadomie, tylko w tescie (jak dokladka ludzi AutotestBattle); sumy w linii doby.
    /// Linia doby "Musztra (autotest 581)" (po "Musztra (gracz)", przed zerowaniem licznikow): narastajaco 574 za trudne (pominiete przy braniu + oddane),
    /// 575 wymiany (oddane = wziete = wymian, wartosc zapasu spada, bez spadku pokrycia), kontrola sztuk zbrojownia + zapas, 1H w zapasie dopasowane do
    /// jazdy (FitMatch na samych sztukach 1H - miejsca 1H typow jazdy); wynik ZGODNE / CZEKA / NIEZGODNE (po 30 dobach od dokladki brak ktoregos - NIEZGODNE).
    /// Nic nie trafia do zapisu poza tym, co i tak zapisuje gra (ludzie, sztuki) i flaga F= (zasilenie 562 zakonczone).
    /// </summary>
    internal static partial class Drill
    {
        private const int T581Rec = 20, T581Rng = 10, T581Cav = 10, T581DropEvery = 3, T581DropMax = 8, T581DropPcs = 2, T581Deadline = 30, T581T6 = 3;
        private const string T581P = "Musztra (autotest 581): ";
        private static bool _t581Set, _t581Off;
        private static int _t581Day0 = -1, _t581NextDrop, _t581Drops, _t581DropN, _t581Pcs, _t581Grain, _t581Gold, _t581Men0, _t581Rk = -1;
        private static long _t581HardSkip, _t581HardBack, _t581Swap, _t581SwapOut, _t581SwapIn, _t581SwapOutV, _t581SwapInV, _t581CovDrop, _t581Bad, _t581Taken, _t581TakenBook;
        private static ItemObject _t581Hard;
        private static List<ItemObject> _t581Items;

        private static void T581Reset()
        {
            _t581Set = _t581Off = false; _t581Day0 = -1; _t581NextDrop = _t581Drops = _t581DropN = _t581Pcs = _t581Grain = _t581Gold = _t581Men0 = 0; _t581Rk = -1;
            _t581HardSkip = _t581HardBack = _t581Swap = _t581SwapOut = _t581SwapIn = _t581SwapOutV = _t581SwapInV = _t581CovDrop = _t581Bad = _t581Taken = _t581TakenBook = 0;
            _t581Hard = null; _t581Items = null;
        }

        /// <summary>Drill.Daily, od doby 2, PRZED AutotestFeed: dokladka (raz), utrzymanie oddzialu, tansze sztuki co T581DropEvery dob. Poza autotestem nic.</summary>
        private static void T581Feed(int day)
        {
            if (_t581Off || !AutotestActive()) return;
            var main = MobileParty.MainParty;
            if (main == null || Hero.MainHero == null) return;
            if (!_t581Set) { T581Setup(day, main); return; }
            T581Upkeep(main);
            if (day >= _t581NextDrop && _t581Drops < T581DropMax) T581Drop(day);
        }

        private static bool T581Soldier(CharacterObject co)
        {
            if (co == null || co.IsHero || co.IsTemplate || co.StringId == null || co.Occupation != Occupation.Soldier || co.Culture == null || co.Culture.IsBandit) return false;
            try { if (PeopleBook.IsWight(co)) return false; } catch (Exception e) { Stumble("T581Soldier", e); return false; }
            return co.BattleEquipments != null;
        }

        private static bool T581Lance(CharacterObject co)
        {
            foreach (var eq in co.BattleEquipments)
            {
                if (eq == null) continue;
                for (int i = 0; i < 4; i++) { var it = eq[(EquipmentIndex)i].Item; if (it != null && it.ItemType == ItemObject.ItemTypeEnum.Polearm && (KindInfo(it) & F1H) != 0) return true; }
            }
            return false;
        }

        /// <summary>Najnizszy tier typu kultury cul (null - kazdej) w roli role; jazda tylko z drzewcem 1H w zestawie bitewnym.</summary>
        private static CharacterObject T581Troop(CultureObject cul, int role)
        {
            CharacterObject best = null;
            foreach (var co in CharacterObject.All)
            {
                if (!T581Soldier(co) || (cul != null && co.Culture != cul)) continue;
                if (RoleClass(RoleOf(co)) != role || (role == RCav && !T581Lance(co))) continue;
                if (best == null || co.Tier < best.Tier || (co.Tier == best.Tier && string.CompareOrdinal(co.StringId, best.StringId) < 0)) best = co;
            }
            return best;
        }

        /// <summary>Kazdy szeregowy typ z rodzajem k udzwignie sztuke (GearFit.ForTroop jak Fits); bez takiego typu - false.</summary>
        private static bool T581FitsAll(KitM m, int k, ItemObject it)
        {
            bool any = false;
            foreach (var t in m.Types)
            {
                if (t.Hero || (t.Mask & (1 << k)) == 0) continue;
                any = true;
                if (!GearFit.ForTroop(t.Ch, it, GearFit.On)) return false;
            }
            return any;
        }

        private static List<ItemObject> T581ByKind(KitM m, int k)
        {
            var l = new List<ItemObject>();
            foreach (var it in _t581Items) if (KindOf(it) == k && T581FitsAll(m, k, it)) l.Add(it);
            l.Sort((a, b) => { int d = a.Value.CompareTo(b.Value); return d != 0 ? d : string.CompareOrdinal(a.StringId, b.StringId); });
            return l;
        }

        private static string T581Name(ItemObject it) { return it.StringId + " (T" + TierIx(it) + ", " + it.Value + " d)"; }

        private static void T581Setup(int day, MobileParty main)
        {
            if (!StockOn) { _t581Off = true; Log.Info(T581P + "zapas musztry wylaczony (Drill Stock / Drill Law / Donation Xp Off) - test musztry gracza pominiety."); return; }
            if (Hero.MainHero.IsPrisoner || main.MapEvent != null || TaleWorlds.MountAndBlade.Mission.Current != null || QuartermasterEscrow.Active) return;   // nastepna doba
            var arm = QuartermasterLaw.DteArmory();
            if (arm == null) { _t581Off = true; Log.Info(T581P + "brak zbrojowni DTE gracza - test musztry gracza pominiety."); return; }
            var s = Settings.Current;
            var m0 = Groups(main, s, true);
            if (m0.Men > 0) { _t581Off = true; Log.Info(T581P + "druzyna ma " + m0.Men + " zolnierzy - bez dokladki (zasilenie 562 jak dotad)."); return; }
            var cul = Hero.MainHero.Culture;
            var rec = cul != null && T581Soldier(cul.BasicTroop) ? cul.BasicTroop : null;
            CharacterObject rng = T581Troop(cul, RXbow), cav = T581Troop(cul, RCav);
            if (rng == null) rng = T581Troop(cul, RBow);
            if (rec == null) rec = T581Troop(cul, RSword);
            string fall = "";
            if (rng == null) { rng = T581Troop(null, RXbow) ?? T581Troop(null, RBow); fall += " dystans spoza kultury gracza;"; }
            if (cav == null) { cav = T581Troop(null, RCav); fall += " jazda spoza kultury gracza;"; }
            int room = main.Party.PartySizeLimit - main.Party.NumberOfAllMembers, want = T581Rec + T581Rng + T581Cav;
            float f = room >= want ? 1f : Math.Max(6, room) / (float)want;
            int nR = rec != null ? Math.Max(2, (int)(T581Rec * f)) : 0, nX = rng != null ? Math.Max(2, (int)(T581Rng * f)) : 0, nC = cav != null ? Math.Max(2, (int)(T581Cav * f)) : 0;
            if (nR + nX + nC == 0) { _t581Off = true; Log.Info(T581P + "brak typow zolnierzy do dokladki - test pominiety."); return; }
            if (nR > 0) main.MemberRoster.AddToCounts(rec, nR);
            if (nX > 0) main.MemberRoster.AddToCounts(rng, nX);
            if (nC > 0) main.MemberRoster.AddToCounts(cav, nC);
            _t581Set = true; _t581Day0 = day; _t581NextDrop = day + T581DropEvery; _t581Men0 = nR + nX + nC;
            _fed = true;   // zasilenie 562 (zakupy z polki za zloto gracza) zastapione dokladka 581 - nie miesza sztuk testu
            // sztuki
            _t581Items = new List<ItemObject>();
            foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>()) if (it != null && it.Value > 0 && Eligible(it)) _t581Items.Add(it);
            var m = Groups(main, s, true);
            _t581Rk = rng == null ? -1 : (RoleClass(RoleOf(rng)) == RXbow ? KXbow : KBow);
            var add = new List<KeyValuePair<ItemObject, int>>();
            var sb = new StringBuilder();
            string hardNote = "brak oddzialu dystansowego";
            for (int k = 0; k < NK; k++)
            {
                if (k == KCape || m.Need[k] <= 0) continue;
                int need = m.Need[k];
                if (k == _t581Rk)
                {
                    // 574: tylko sztuki z wymogiem ponad umiejetnosc typu (GearFit WhySkill) - najtansze dwie
                    var hard = new List<ItemObject>();
                    foreach (var it in _t581Items)
                    {
                        if (KindOf(it) != k) continue;
                        int why; if (!GearFit.ForTroop(rng, it, GearFit.On, out why) && why == GearFit.WhySkill) hard.Add(it);
                    }
                    hard.Sort((a, b) => { int d = a.Value.CompareTo(b.Value); return d != 0 ? d : string.CompareOrdinal(a.StringId, b.StringId); });
                    if (hard.Count == 0) { hardNote = "brak sztuk " + KindPl[k] + " ponad umiejetnosc " + rng.StringId; continue; }
                    int n2 = need + 2, a1 = hard.Count > 1 ? (n2 + 1) / 2 : n2;
                    add.Add(new KeyValuePair<ItemObject, int>(hard[0], a1));
                    if (hard.Count > 1) add.Add(new KeyValuePair<ItemObject, int>(hard[1], n2 - a1));
                    _t581Hard = hard[0];
                    var sk = ItemReq.SkillFor(hard[0]);
                    int sv = 0; try { if (sk != null) sv = rng.GetSkillValue(sk); } catch (Exception e) { Stumble("T581Skill", e); }
                    hardNote = KindPl[k] + " " + T581Name(hard[0]) + " x" + a1 + (hard.Count > 1 ? " i " + T581Name(hard[1]) + " x" + (n2 - a1) : "") + ", wymog " + hard[0].Difficulty
                               + " " + (sk != null ? sk.StringId : "?") + " wobec " + sv + " u " + rng.StringId;
                    continue;
                }
                var l = T581ByKind(m, k);
                if (k == KPole) { var l1 = l.FindAll(x => (KindInfo(x) & F1H) != 0); if (l1.Count > 0) l = l1; }   // jazda: kopia / wlocznia z uzyciem 1H
                if (l.Count == 0) { sb.Append(KindPl[k]).Append(": brak sztuki, ktora udzwigna wszyscy; "); continue; }
                ItemObject pick = l[0];
                int cnt = 2 * need + 2;
                if (k == KHead && l.Count >= 2)
                {
                    // 575: drogie w zapasie - sztuka z gornej tercji ceny (bez T6), tansze przyjda co kilka dob
                    for (int i = Math.Min(l.Count - 1, l.Count * 2 / 3); i >= 1; i--) if (TierIx(l[i]) < 6) { pick = l[i]; break; }
                    cnt = 2 * need;
                }
                add.Add(new KeyValuePair<ItemObject, int>(pick, cnt));
                sb.Append(KindPl[k]).Append(' ').Append(T581Name(pick)).Append(" x").Append(cnt);
                if (k == KBody || k == K1H)
                {
                    var t6 = l.Find(x => TierIx(x) == 6);
                    if (t6 != null) { add.Add(new KeyValuePair<ItemObject, int>(t6, T581T6)); sb.Append(" + ").Append(T581Name(t6)).Append(" x").Append(T581T6); }
                }
                sb.Append("; ");
            }
            foreach (var kv in add)
            {
                var el = new EquipmentElement(kv.Key);
                arm.AddToCounts(el, kv.Value);
                ArmouryBehavior.StockDepositKey(el, kv.Value);   // lista gracza (jak 576)
                _t581Pcs += kv.Value;
            }
            ArmouryBehavior.StockVersion++;
            _hudDirty = true;
            Log.Info(T581P + "dokladka na stale (tylko autotest) - " + nR + " x " + (rec != null ? rec.StringId + " T" + rec.Tier : "-") + ", " + nX + " x " + (rng != null ? rng.StringId + " T" + rng.Tier : "-")
                     + ", " + nC + " x " + (cav != null ? cav.StringId + " T" + cav.Tier : "-") + " (kultura gracza " + (cul != null ? cul.StringId : "-") + ";" + fall + " miejsce w druzynie " + room
                     + "); do zbrojowni DTE na liste gracza " + _t581Pcs + " szt.: " + sb + "za trudne (574): " + hardNote + "; zasilenie 562 wylaczone; klucze Skill Check "
                     + (SkillOn ? "tak" : "nie") + ", Cheapest Swap " + (CheapOn ? "tak" : "nie") + ", Takes Player Stash " + (BookOn ? "tak" : "nie") + ", ludzi na sztuke " + MenPerPiece + ".");
        }

        /// <summary>Jedzenie i zold oddzialu testu (bez tego glod i niezaplacony zold obnizaja morale - DefaultPartyDesertionModel zwalnia ludzi).</summary>
        private static void T581Upkeep(MobileParty main)
        {
            try
            {
                float cons = Math.Max(1f, -main.FoodChange);
                if (main.Food < 5f * cons && DefaultItems.Grain != null)
                {
                    int n = (int)Math.Ceiling(10f * cons - main.Food);
                    if (n > 0) { main.ItemRoster.AddToCounts(DefaultItems.Grain, n); _t581Grain += n; }
                }
                int wage = main.TotalWage;
                if (wage > 0 && Hero.MainHero.Gold < 3 * wage) { GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, 7 * wage, true); _t581Gold += 7 * wage; }
            }
            catch (Exception e) { Stumble("T581Upkeep", e); }
        }

        /// <summary>575: najdrozsza sztuka zapasu, ktora ma tansze zastepstwo tej samej klasy (KindInfo; FitsSuper - kto udzwignie drozsza, udzwignie tansza),
        /// dostaje T581DropPcs najtanszych takich sztuk w zbrojowni na liscie gracza.</summary>
        private static void T581Drop(int day)
        {
            _t581NextDrop = day + T581DropEvery;
            var arm = QuartermasterLaw.DteArmory();
            if (arm == null || _t581Items == null) return;
            var m = MeasurePlayer();
            var els = new List<ItemRosterElementCopy>();
            for (int i = 0; i < _stock.Count; i++)
            {
                var el = _stock.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                int k = KindOf(it);
                if (el.Amount <= 0 || k < 0 || k == KCape || (m.Skill && !UsableAny(m, it))) continue;
                els.Add(new ItemRosterElementCopy { It = it, V = el.EquipmentElement.ItemValue, K = k });
            }
            els.Sort((a, b) => b.V.CompareTo(a.V));
            foreach (var d in els)
            {
                int info = KindInfo(d.It);
                ItemObject best = null;
                foreach (var it in _t581Items)
                {
                    if (it == d.It || KindInfo(it) != info || it.Value >= d.V || (best != null && it.Value >= best.Value)) continue;
                    if (!FitsSuper(m, it, d.It) || !FitsKind(m, d.K, it)) continue;
                    best = it;
                }
                if (best == null) continue;
                var e = new EquipmentElement(best);
                arm.AddToCounts(e, T581DropPcs);
                ArmouryBehavior.StockDepositKey(e, T581DropPcs);
                ArmouryBehavior.StockVersion++;
                _t581Drops++; _t581DropN += T581DropPcs; _t581Pcs += T581DropPcs;
                Log.Info(T581P + "tansza sztuka " + _t581Drops + "/" + T581DropMax + " - " + T581DropPcs + " x " + T581Name(best) + " do zbrojowni na liste gracza; w zapasie drozsza "
                         + T581Name(d.It) + " (" + KindPl[d.K] + ") - jutro wymiana 575.");
                return;
            }
            Log.Info(T581P + "tansza sztuka - zadna sztuka zapasu nie ma tanszego zastepstwa (zapas " + els.Count + " pozycji).");
        }

        private sealed class ItemRosterElementCopy { internal ItemObject It; internal int V, K; }

        /// <summary>Drill.Daily po liniach doby, przed ClearDay: liczniki doby narastajaco i aserty. Poza autotestem / bez dokladki nic.</summary>
        private static void T581Line(int day)
        {
            if (!_t581Set || !AutotestActive()) return;
            _t581HardSkip += _pHardSkip; _t581HardBack += _pBackHard; _t581Swap += _pSwapN; _t581CovDrop += _pSwapCovDrop; _t581Bad += _settleBad;
            _t581Taken += _pTaken; _t581TakenBook += _pTakenBook; _t581SwapOutV += _pSwapOutV; _t581SwapInV += _pSwapInV;
            for (int i = 1; i <= 6; i++) { _t581SwapOut += _pSwapOutT[i]; _t581SwapIn += _pSwapInT[i]; }
            var main = MobileParty.MainParty;
            var s = Settings.Current;
            if (main == null || s == null) return;
            // 1H u jazdy: FitMatch na samych sztukach 1H zapasu - miejsca 1H typow jazdy zajete prawdziwa bronia 1H (nie drzewcem jako zastepstwem)
            var mm = Groups(main, s, true);
            var pcs = new Dictionary<ItemObject, int>();
            int sw = 0, cavNeed = 0, cav1 = 0, men = mm.Men;
            for (int i = 0; i < _stock.Count; i++)
            {
                var el = _stock.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (el.Amount <= 0 || it == null || KindOf(it) != K1H) continue;
                int v; pcs.TryGetValue(it, out v); pcs[it] = v + el.Amount; sw += el.Amount;
            }
            for (int t = 0; t < mm.Types.Count; t++) if (RoleClass(mm.Types[t].Mask) == RCav && (mm.Types[t].Mask & (1 << K1H)) != 0) cavNeed += mm.Types[t].Cap;
            if (mm.Skill)
            {
                FitMatch(mm, pcs);
                for (int t = 0; t < mm.Types.Count; t++)
                    if (RoleClass(mm.Types[t].Mask) == RCav && (mm.Types[t].Mask & (1 << K1H)) != 0) cav1 += mm.Types[t].Cap - mm.Resid[K1H][t];
            }
            else cav1 = Math.Min(sw, cavNeed);   // bez 574 nie ma dopasowania do typow - przyblizenie
            long hard = _t581HardSkip + _t581HardBack;
            bool bad = _t581Bad > 0 || _t581CovDrop > 0 || _t581SwapOut != _t581Swap || _t581SwapIn != _t581Swap || (_t581Swap > 0 && _t581SwapInV >= _t581SwapOutV);
            var miss = new List<string>();
            if (_t581Hard != null && hard <= 0) miss.Add("za trudne");
            if (_t581Swap <= 0) miss.Add("wymiana na tansze");
            if (cavNeed > 0 && cav1 <= 0) miss.Add("1H u jazdy");
            string skip = (_t581Hard == null ? " (za trudne nie do sprawdzenia - brak sztuk ponad umiejetnosc)" : "") + (cavNeed <= 0 ? " (1H u jazdy nie do sprawdzenia - brak jazdy)" : "");
            string verdict = bad ? "NIEZGODNE (kontrola sztuk / pokrycie / bilans wymian)"
                : miss.Count == 0 ? "ZGODNE" + skip
                : (day - _t581Day0 >= T581Deadline ? "NIEZGODNE (po " + T581Deadline + " dobach brak: " : "CZEKA (brak: ") + string.Join(", ", miss.ToArray()) + ")" + skip;
            Log.Info(T581P + "dzien " + day + " (doba " + (day - _t581Day0) + " od dokladki), ludzi " + men + "/" + _t581Men0 + " | 574 za trudne " + hard + " (pominiete przy braniu " + _t581HardSkip
                     + ", oddane " + _t581HardBack + ") | 575 wymieniono na tansze " + _t581Swap + " (oddane " + _t581SwapOut + ", wziete " + _t581SwapIn + " - bilans 1:1 " + (_t581SwapOut == _t581SwapIn && _t581SwapIn == _t581Swap ? "TAK" : "NIE")
                     + "; wartosc oddanych " + _t581SwapOutV + " d, wzietych " + _t581SwapInV + " d; spadek pokrycia " + _t581CovDrop + ") | 1H w zapasie " + sw + ", u jazdy " + cav1 + "/" + cavNeed
                     + (mm.Skill ? "" : " (bez 574 - przyblizenie)") + " | wzieto ze zbrojowni " + _t581Taken + " (z listy gracza " + _t581TakenBook + "), kontrola sztuk zbrojownia + zapas "
                     + (_t581Bad == 0 ? "ZGODNE" : "NIEZGODNE x" + _t581Bad) + " | test dolozyl: sztuk " + _t581Pcs + " (tansze " + _t581Drops + "x, " + _t581DropN + " szt.), zboza " + _t581Grain + ", zlota " + _t581Gold
                     + " | " + verdict + ".");
        }
    }
}
