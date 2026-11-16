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
        private const int T581Rec = 20, T581Rng = 10, T581Cav = 10, T581DropEvery = 3, T581DropMax = 8, T581DropPcs = 2, T581Deadline = 30, T581T6 = 3, T581OneWait = 12;
        private const string T581P = "Musztra (autotest 581): ";
        private static bool _t581Set, _t581Off;
        private static int _t581Day0 = -1, _t581NextDrop, _t581Drops, _t581DropN, _t581Pcs, _t581Grain, _t581Gold, _t581Men0, _t581Rk = -1;
        private static long _t581HardSkip, _t581HardBack, _t581Swap, _t581SwapOut, _t581SwapIn, _t581SwapOutV, _t581SwapInV, _t581CovDrop, _t581Bad, _t581Taken, _t581TakenBook, _t581BalBad;
        private static int _t581OneDay = -1, _t581OnePole, _t581CavFull, _t581T6Max;   // recenzja 581 (1, 4): 1H po kopiach, doby z pelnym 1H jazdy, T6 w zapasie
        private static ItemObject _t581Hard;
        // 583 (P2): "za trudne" tylko dla sztuk testu (hard[0], hard[1]) - pominiete przy braniu Z LISTY GRACZA i oddane; rozne sztuki, nie proby
        private static HashSet<ItemObject> _t581HardIds, _t581HardSeen;
        private static long _t581HardIdSkip, _t581HardIdBack, _t581HardIdKeep;   // recenzja 583 (2): Keep - sztuka testu na liscie gracza zablokowana (Avail 0)
        private static bool _t581OneWaitSaid, _t581OneCavOk;   // recenzja 583 (1): przy dostawie 1H kopie F1H byly u jazdy   // 583 (P3): dostawa 1H czeka na kopie F1H w zapasie - linia raz
        private static List<ItemObject> _t581Items;

        private static void T581Reset()
        {
            _t581Set = _t581Off = false; _t581Day0 = -1; _t581NextDrop = _t581Drops = _t581DropN = _t581Pcs = _t581Grain = _t581Gold = _t581Men0 = 0; _t581Rk = -1;
            _t581HardSkip = _t581HardBack = _t581Swap = _t581SwapOut = _t581SwapIn = _t581SwapOutV = _t581SwapInV = _t581CovDrop = _t581Bad = _t581Taken = _t581TakenBook = _t581BalBad = 0;
            _t581OneDay = -1; _t581OnePole = _t581CavFull = _t581T6Max = 0;
            _t581Hard = null; _t581Items = null;
            _t581HardIds = _t581HardSeen = null; _t581HardIdSkip = _t581HardIdBack = _t581HardIdKeep = 0; _t581OneWaitSaid = _t581OneCavOk = false;
        }

        /// <summary>583 (P2): DrillKit - sztuka "za trudna" oddana z zapasu (back) albo pominieta przy braniu z listy gracza. Liczy tylko sztuki testu 581
        /// (_t581HardIds); poza autotestem _t581HardIds == null - nic.</summary>
        private static void T581HardNote(ItemObject it, long n, bool back)
        {
            if (_t581HardIds == null || it == null || n <= 0 || !_t581HardIds.Contains(it)) return;
            if (back) _t581HardIdBack += n; else _t581HardIdSkip += n;
            _t581HardSeen.Add(it);
        }

        /// <summary>Recenzja 583 (2): DrillKit.TakePlayer - sztuka testu z listy gracza ma brak do pokrycia, ale Avail 0 (Keep nastepnej bitwy / brak w zbrojowni):
        /// 574 przy niej nie bylo sprawdzane. Poza autotestem _t581HardIds == null - nic.</summary>
        private static void T581HardKeepNote(ItemObject it)
        {
            if (_t581HardIds == null || it == null || !_t581HardIds.Contains(it)) return;
            _t581HardIdKeep++;
        }

        /// <summary>Recenzja 583 (1): ile miejsc drzewc typow JAZDY zajmuja kopie / wlocznie F1H z zapasu (FitMatch na samych drzewcach F1H - Claim daje
        /// najpierw typom z najnizsza umiejetnoscia, wiec kopia moze pojsc do rekruta, a nie do jazdy). Bez 574 (Skill) -1. Zmienia m.Resid / m.Fit.</summary>
        private static int T581CavLance(KitM m)
        {
            if (m == null || !m.Skill) return -1;
            var pcs = new Dictionary<ItemObject, int>();
            for (int i = 0; i < _stock.Count; i++)
            {
                var el = _stock.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (el.Amount <= 0 || it == null || KindOf(it) != KPole || (KindInfo(it) & F1H) == 0) continue;
                int v; pcs.TryGetValue(it, out v); pcs[it] = v + el.Amount;
            }
            FitMatch(m, pcs);
            int n = 0;
            for (int t = 0; t < m.Types.Count; t++)
                if (RoleClass(m.Types[t].Mask) == RCav && (m.Types[t].Mask & (1 << KPole)) != 0) n += m.Types[t].Cap - m.Resid[KPole][t];
            return n;
        }

        /// <summary>583 (P4): najtansze drzewce z uzyciem 1H (F1H - bron biala bez NotUsableWithOneHand: kopia jednoreczna z couch, wlocznia 1H) z wlasnych
        /// zestawow bitewnych typu co (T581Lance wybral go wlasnie po takim drzewcu) - wlasny wzorzec przechodzi MountOk i kulture (TemplateHas).</summary>
        private static ItemObject T581OwnLance(CharacterObject co)
        {
            ItemObject best = null;
            if (co == null) return null;
            foreach (var eq in co.BattleEquipments)
            {
                if (eq == null) continue;
                for (int i = 0; i < 4; i++)
                {
                    var it = eq[(EquipmentIndex)i].Item;
                    if (it == null || it.ItemType != ItemObject.ItemTypeEnum.Polearm || (KindInfo(it) & F1H) == 0 || it.Value <= 0 || !Eligible(it)) continue;
                    if (best == null || it.Value < best.Value || (it.Value == best.Value && string.CompareOrdinal(it.StringId, best.StringId) < 0)) best = it;
                }
            }
            return best;
        }

        /// <summary>Drill.Daily, od doby 2, PRZED AutotestFeed: dokladka (raz), utrzymanie oddzialu, tansze sztuki co T581DropEvery dob. Poza autotestem nic.</summary>
        private static void T581Feed(int day)
        {
            if (_t581Off || !AutotestActive()) return;
            var main = MobileParty.MainParty;
            if (main == null || Hero.MainHero == null) return;
            if (!_t581Set) { T581Setup(day, main); return; }
            T581Upkeep(main);
            if (_t581OneDay < 0 && day >= _t581Day0 + T581DropEvery) T581One(day);   // recenzja 581 (1): 1H dopiero, gdy kopie sa juz w zapasie (583: czeka na nie najdluzej do T581OneWait)
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
            if (room < 6) { _t581Off = true; Log.Info(T581P + "miejsce w druzynie " + room + " (< 6) - bez dokladki, test musztry gracza pominiety."); return; }   // recenzja 581 (5): ponad limit gra zwolni ludzi
            float f = room >= want ? 1f : room / (float)want;
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
                if (k == K1H) { sb.Append(KindPl[k]).Append(": po kopiach (doba +").Append(T581DropEvery).Append("); "); continue; }   // recenzja 581 (1)
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
                    _t581HardIds = new HashSet<ItemObject> { hard[0] }; if (hard.Count > 1) _t581HardIds.Add(hard[1]);   // 583 (P2)
                    _t581HardSeen = new HashSet<ItemObject>();
                    var sk = ItemReq.SkillFor(hard[0]);
                    int sv = 0; try { if (sk != null) sv = rng.GetSkillValue(sk); } catch (Exception e) { Stumble("T581Skill", e); }
                    hardNote = KindPl[k] + " " + T581Name(hard[0]) + " x" + a1 + (hard.Count > 1 ? " i " + T581Name(hard[1]) + " x" + (n2 - a1) : "") + ", wymog " + hard[0].Difficulty
                               + " " + (sk != null ? sk.StringId : "?") + " wobec " + sv + " u " + rng.StringId;
                    continue;
                }
                var l = T581ByKind(m, k);
                string poleNote = "";
                if (k == KPole)
                {
                    var l1 = l.FindAll(x => (KindInfo(x) & F1H) != 0);   // jazda: kopia / wlocznia z uzyciem 1H
                    if (l1.Count > 0) l = l1;
                    else
                    {
                        // 583 (P4): at-268b - zadne drzewce F1H nie przeszlo dla wszystkich, dokladka dala widly 2H i kopii w zapasie nie bylo nigdy
                        var own = T581OwnLance(cav);
                        if (own != null) { l = new List<ItemObject> { own }; poleNote = " (kopia z wlasnego wzorca " + cav.StringId + " - zadne drzewce F1H nie dla wszystkich)"; }
                        else poleNote = " (BRAK drzewca F1H - 1H u jazdy przy kopiach nie do sprawdzenia)";
                    }
                }
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
                sb.Append(KindPl[k]).Append(' ').Append(T581Name(pick)).Append(" x").Append(cnt).Append(poleNote);
                if (k == KBody) T581AddT6(k, l, add, sb);
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

        /// <summary>Recenzja 581 (4): 3 sztuki T6 rodzaju k - najpierw takie, ktore udzwigna wszyscy (l), inaczej najtansza T6 rodzaju (z dopiskiem; do zapasu
        /// nie wejdzie - nikt jej nie udzwignie - ale lezy na liscie gracza jak drogi sprzet gracza).</summary>
        private static void T581AddT6(int k, List<ItemObject> l, List<KeyValuePair<ItemObject, int>> add, StringBuilder sb)
        {
            var t6 = l.Find(x => TierIx(x) == 6);
            string note = "";
            if (t6 == null)
            {
                foreach (var it in _t581Items) if (KindOf(it) == k && TierIx(it) == 6 && (t6 == null || it.Value < t6.Value || (it.Value == t6.Value && string.CompareOrdinal(it.StringId, t6.StringId) < 0))) t6 = it;
                note = " - nie dla wszystkich";
            }
            if (t6 == null) { sb.Append(" + T6: brak"); return; }
            add.Add(new KeyValuePair<ItemObject, int>(t6, T581T6));
            sb.Append(" + ").Append(T581Name(t6)).Append(" x").Append(T581T6).Append(note);
        }

        /// <summary>Recenzja 581 (1): bron 1H (2 x popyt + 2 najtanszych, ktore udzwigna wszyscy, i 3 x T6) na liste gracza dopiero po T581DropEvery dobach -
        /// kopie F1H jazdy leza juz w zapasie, wiec pobor mieczy idzie przy zajetych miejscach drzewc (scenariusz bledu recenzji 574).</summary>
        private static void T581One(int day)
        {
            _t581OnePole = 0;
            for (int i = 0; i < _stock.Count; i++)
            {
                var el = _stock.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (el.Amount > 0 && it != null && KindOf(it) == KPole && (KindInfo(it) & F1H) != 0) _t581OnePole += el.Amount;
            }
            // 583 (P3): 1H dopiero przy kopiach F1H w zapasie (scenariusz recenzji 574); bez nich czeka do doby +T581OneWait, potem dostawa z dopiskiem
            // recenzja 583 (1): kopie maja byc U JAZDY (FitMatch drzewc F1H), nie tylko w zapasie - bez 574 (Skill) jak dotad zapas
            var mc = MobileParty.MainParty != null && Settings.Current != null ? Groups(MobileParty.MainParty, Settings.Current, true) : null;
            int cavL = T581CavLance(mc);
            bool ready = cavL >= 0 ? cavL > 0 : _t581OnePole > 0;
            if (!ready && day < _t581Day0 + T581OneWait)
            {
                if (!_t581OneWaitSaid) { _t581OneWaitSaid = true; Log.Info(T581P + "1H po kopiach (doba " + (day - _t581Day0) + ") - kopii / wloczni F1H w zapasie " + _t581OnePole + ", u jazdy " + cavL + ", dostawa 1H czeka (najdluzej do doby +" + T581OneWait + ")."); }
                return;
            }
            _t581OneCavOk = ready;
            _t581OneDay = day;
            var arm = QuartermasterLaw.DteArmory();
            var main = MobileParty.MainParty;
            if (arm == null || main == null || _t581Items == null) { Log.Info(T581P + "1H - brak zbrojowni DTE / druzyny, bez dostawy."); return; }
            var m = Groups(main, Settings.Current, true);
            var sb = new StringBuilder();
            var add = new List<KeyValuePair<ItemObject, int>>();
            var l = T581ByKind(m, K1H);
            if (m.Need[K1H] <= 0 || l.Count == 0) sb.Append("brak popytu albo sztuki, ktora udzwigna wszyscy");
            else
            {
                int cnt = 2 * m.Need[K1H] + 2;
                add.Add(new KeyValuePair<ItemObject, int>(l[0], cnt));
                sb.Append(T581Name(l[0])).Append(" x").Append(cnt);
                T581AddT6(K1H, l, add, sb);
            }
            int n = 0;
            foreach (var kv in add)
            {
                var el = new EquipmentElement(kv.Key);
                arm.AddToCounts(el, kv.Value);
                ArmouryBehavior.StockDepositKey(el, kv.Value);   // lista gracza (jak 576)
                n += kv.Value;
            }
            if (n > 0) { ArmouryBehavior.StockVersion++; _hudDirty = true; _t581Pcs += n; }
            Log.Info(T581P + "1H po kopiach (doba " + (day - _t581Day0) + ") - " + n + " szt. na liste gracza: " + sb + "; kopie / wlocznie F1H w zapasie " + _t581OnePole + ", u jazdy " + cavL + (cavL < 0 ? " (bez 574)" : "")
                     + (!ready ? " - SCENARIUSZ KOPII NIE ZASZEDL (dostawa bez kopii u jazdy po " + T581OneWait + " dobach)" : "") + ".");
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
            _t581HardSkip += _pHardSkip; _t581HardBack += _pBackHard; _t581Swap += _pSwapN; _t581CovDrop += _pSwapCovDrop; _t581Bad += _settleBad;   // _t581BalBad liczy Daily (bilans zapasu NIEZGODNY)
            _t581Taken += _pTaken; _t581TakenBook += _pTakenBook; _t581SwapOutV += _pSwapOutV; _t581SwapInV += _pSwapInV;
            for (int i = 1; i <= 6; i++) { _t581SwapOut += _pSwapOutT[i]; _t581SwapIn += _pSwapInT[i]; }   // tylko opis - rosna razem z _pSwapN (recenzja 581 (2))
            var main = MobileParty.MainParty;
            var s = Settings.Current;
            if (main == null || s == null) return;
            // recenzja 581 (1): 1H u jazdy - FitMatch na CALYM zapasie (jedna pula jak ClaimTake: kopie najpierw do drzewc, potem miecze do 1H); miejsca 1H typow
            // jazdy pelne po dostawie 1H przy kopiach w zapasie (blad recenzji 574 - kopia zajmuje miejsce 1H i pobor mieczy staje - zostawilby je puste)
            var mm = Groups(main, s, true);
            var pcs = new Dictionary<ItemObject, int>();
            int sw = 0, lance = 0, t6 = 0, cavNeed = 0, cav1 = 0, men = mm.Men;
            for (int i = 0; i < _stock.Count; i++)
            {
                var el = _stock.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (el.Amount <= 0 || it == null) continue;
                int k = KindOf(it);
                if (k < 0) continue;
                int v; pcs.TryGetValue(it, out v); pcs[it] = v + el.Amount;
                if (k == K1H) sw += el.Amount;
                else if (k == KPole && (KindInfo(it) & F1H) != 0) lance += el.Amount;
                if (TierIx(it) == 6) t6 += el.Amount;
            }
            _t581T6Max = Math.Max(_t581T6Max, t6);
            for (int t = 0; t < mm.Types.Count; t++) if (RoleClass(mm.Types[t].Mask) == RCav && (mm.Types[t].Mask & (1 << K1H)) != 0) cavNeed += mm.Types[t].Cap;
            int cavLance = T581CavLance(mm);   // recenzja 583 (1): kopie F1H u jazdy (przed pelnym FitMatch - ten liczy Resid od nowa); -1 bez 574
            if (cavLance < 0) cavLance = lance;
            if (mm.Skill)
            {
                FitMatch(mm, pcs);
                for (int t = 0; t < mm.Types.Count; t++)
                    if (RoleClass(mm.Types[t].Mask) == RCav && (mm.Types[t].Mask & (1 << K1H)) != 0) cav1 += mm.Types[t].Cap - mm.Resid[K1H][t];
            }
            else cav1 = Math.Min(sw, cavNeed);   // bez 574 nie ma dopasowania do typow - przyblizenie
            if (_t581OneDay >= 0 && day > _t581OneDay && cavNeed > 0 && cav1 >= cavNeed && sw > 0 && cavLance > 0) _t581CavFull++;   // recenzja 583 (1): kopie u jazdy, nie w zapasie
            long hard = _t581HardIdSkip + _t581HardIdBack;   // 583 (P2): tylko sztuki testu - pominiete z listy gracza albo oddane (nie globalne proby)
            bool pvOk = KitPreview.CsQuiet;   // 583 (P1): bez podgladu (CrashScribe testu sprzed 133a) lista gracza i 575 martwe - nie do sprawdzenia
            bool noLance = _t581OneDay >= 0 && !_t581OneCavOk && cavLance <= 0;   // 583 (P3), recenzja (1): dostawa 1H poszla bez kopii u jazdy i dalej ich tam nie ma
            bool bad = _t581Bad > 0 || _t581BalBad > 0 || _t581CovDrop > 0 || (_t581Swap > 0 && _t581SwapInV >= _t581SwapOutV);   // recenzja 581 (2): bilans sztuk z Daily zamiast tautologii liczb wymian
            var miss = new List<string>();
            bool hardKeep = _t581Hard != null && hard <= 0 && _t581HardIdKeep > 0;   // recenzja 583 (2): sztuki testu tylko blokowane (Keep / brak w zbrojowni) - 574 nie sprawdzane
            if (_t581Hard != null && hard <= 0 && !hardKeep) miss.Add("za trudne");
            if (_t581Swap <= 0) miss.Add("wymiana na tansze");
            if (cavNeed > 0 && _t581CavFull <= 0 && !noLance) miss.Add("1H u jazdy (pelne przy kopiach w zapasie)");
            string skip = (_t581Hard == null ? " (za trudne nie do sprawdzenia - brak sztuk ponad umiejetnosc)" : "") + (hardKeep ? " (za trudne nie do sprawdzenia - sztuki testu zablokowane Keep bitwy / brak w zbrojowni x" + _t581HardIdKeep + ")" : "") + (cavNeed <= 0 ? " (1H u jazdy nie do sprawdzenia - brak jazdy)" : "")
                          + (cavNeed > 0 && _t581CavFull <= 0 && noLance ? " (1H u jazdy nie do sprawdzenia - kopii F1H w zapasie nie bylo)" : "");
            string verdict = bad ? "NIEZGODNE (kontrola sztuk / bilans zapasu / pokrycie / wartosc wymian)"
                : !pvOk && miss.Count > 0 ? "nie do sprawdzenia (CrashScribe testu bez podgladu) - brak: " + string.Join(", ", miss.ToArray()) + "; kontrola sztuk i bilans zapasu ZGODNE" + skip
                : miss.Count == 0 ? "ZGODNE" + skip
                : (day - _t581Day0 >= T581Deadline ? "NIEZGODNE (po " + T581Deadline + " dobach brak: " : "CZEKA (brak: ") + string.Join(", ", miss.ToArray()) + ")" + skip;
            Log.Info(T581P + "dzien " + day + " (doba " + (day - _t581Day0) + " od dokladki), ludzi " + men + "/" + _t581Men0 + " | 574 za trudne sztuk testu " + hard + " (pominiete z listy gracza " + _t581HardIdSkip + ", oddane " + _t581HardIdBack + ", rozne sztuki "
                     + (_t581HardSeen != null ? _t581HardSeen.Count : 0) + "/" + (_t581HardIds != null ? _t581HardIds.Count : 0) + ", zablokowane Keep " + _t581HardIdKeep + "; wszystkie proby: pominiete przy braniu " + _t581HardSkip
                     + ", oddane " + _t581HardBack + ") | 575 wymieniono na tansze " + _t581Swap + " (oddane " + _t581SwapOut + ", wziete " + _t581SwapIn
                     + "; wartosc oddanych " + _t581SwapOutV + " d, wzietych " + _t581SwapInV + " d; spadek pokrycia " + _t581CovDrop + ") | 1H w zapasie " + sw + (_t581OneDay < 0 ? " (dostawa 1H po kopiach jeszcze nie)" : "")  + ", kopie F1H " + lance + " (u jazdy " + cavLance + "), 1H u jazdy " + cav1 + "/" + cavNeed + " (pelne przy kopiach " + _t581CavFull + " dob)"
                     + (mm.Skill ? "" : " (bez 574 - przyblizenie)") + " | wzieto ze zbrojowni " + _t581Taken + " (z listy gracza " + _t581TakenBook + "), kontrola sztuk zbrojownia + zapas "
                     + (_t581Bad == 0 ? "ZGODNE" : "NIEZGODNE x" + _t581Bad) + ", bilans zapasu " + (_t581BalBad == 0 ? "ZGODNY" : "NIEZGODNY x" + _t581BalBad) + " | T6 w zapasie " + t6 + " (najwiecej " + _t581T6Max + ") | test dolozyl: sztuk " + _t581Pcs + " (tansze " + _t581Drops + "x, " + _t581DropN + " szt.), zboza " + _t581Grain + ", zlota " + _t581Gold
                     + (pvOk ? "" : " | podglad bitwy BRAK (CrashScribe testu sprzed 133a)") + " | " + verdict + ".");
        }
    }
}
