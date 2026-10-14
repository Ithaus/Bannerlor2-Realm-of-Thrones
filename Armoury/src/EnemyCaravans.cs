using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// L5b-2 - KARAWANY WROGA W ZDOBYTYM MIESCIE (decyzja Jeffa 13.10 "5 tak, i tak": docs/lup-miast/LUP-ZDOBYTEGO-MIASTA-2026-10-13.md rozdz. 8 pyt. 5b,
    /// rozdz. 2 wiersz 9). Wylacznik EnemyCaravansSeized (wylaczony = stan a913a58: karawany wyjezdzaja jak w grze; linia pomiaru zostaje).
    ///
    /// GRA (dekompilacja 1.4.8): karawany nie bronia murow (Town.GetDefenderParties pomija IsCaravan), a przy zmianie pana PartyDiplomaticHandlerCampaignBehavior.
    /// OnSettlementOwnerChanged -> CheckSettlementSuitabilityForParties wyprowadza z osady kazda partie w wojnie z nowym panem (LeaveSettlementAction) - karawana
    /// wroga wyjezdza z calym towarem i kiesa. Dzieje sie to WEWNATRZ KingdomManager.SiegeCompleted (ApplyBySiege -> zdarzenie), wiec spis karawan bierzemy w
    /// prefiksie tej metody (ten sam warunek zdobycia co gra: szturm, wypad, wypad z blokady i wygrana), a zajecie robi postfiks - po zmianie pana i wejsciu
    /// oblegajacych do osady, gdy stan gry jest juz spojny.
    ///
    /// KTO: karawana (CaravanPartyComponent: kupcow, lordow BK, konwoje DTE, karawany gracza), ktora w chwili zdobycia stoi w osadzie (CurrentSettlement == osada)
    /// i ktorej frakcja jest w wojnie ze zdobywca PO zmianie pana - dokladnie te, ktore gra wyprowadza (obce karawany wroga: kupcow innych osad, lordow BK, gracza).
    /// Karawana kupca zdobytej osady albo jej wsi (MobileParty.MapFaction notabla = frakcja jego osady) staje sie poddana zdobywcy i zostaje - jej kupcy placa
    /// przez lup osady (L2) wedlug wyboru zdobywcy, laska zostaje laska. Pominiete: karawany ze statkami (w porcie odplywaja - zostawiamy grze),
    /// karawana w bitwie, zdobywca Inni (trup monety nie bierze - jak BeatenPurse / CasualtyLoot; karawany wyjezdzaja jak w grze).
    ///
    /// JAK (DECYZJA - pelne uprawnienia: ta sama regula co karawana rozbita w polu, 3.2 "jak rozbije karawane, to zabiera wszystko zwyciezca" - decyzja Jeffa 09.10
    /// Q3b; karawana to partia, nie moneta miasta - podzial "prawa trzecich" lupu miasta L2 jej nie dotyczy, a L2 jest w innej galezi):
    ///  1. przewodnik i kazdy bohater karawany -> niewola zdobywcy (TakePrisonerAction - jak wrog zlapany w zdobytych murach);
    ///  2. kiesa karawany (PartyTradeGold) -> wodz zdobywcow (GiveGoldAction partia -> bohater, zdarzenie gry - ksiega widzi przelew), korona 1/9 od wodza-wasala
    ///     w wojnie (Ransom178.NoteBeatenGold - ta sama podstawa co 3.2); wodza brak - kiesa jego druzyny (partia z kiesa) albo kasa zdobytej osady;
    ///  3. caly ladunek (towar, zywnosc, konie, juczne, bron) -> tabor druzyny zdobywcy (sztuka zdjeta = sztuka dopisana; juczne niosa to, co niosly) - po audycie
    ///     integracji 13/14.10 miara L7: zwierzeta do granicy stada bez kary "Herding", reszta najcenniejsze na kg do wolnego udzwigu; nadmiar na polke zdobytej osady. AI sprzedaje
    ///     to w nastepnym miescie droga gry (PartiesSellLootCampaignBehavior - zloto z kasy miasta, korona 1/9 przez Ransom178.LootPostfix), zywnosc zjada;
    ///  4. pusta karawana -> DestroyPartyAction.Apply(null, karawana): CZYNNE reguly partii rozwiazanej bez bitwy - eskorta do domu (PeopleBook.ArmsPartyGone, E4/8;
    ///     warunek: bez niszczyciela, bez bitwy i oblezenia - dlatego null), sakiewki eskorty do kasy najblizszego miasta (MenPurse, wpis 89). Kiesa (0) i dobytek
    ///     (pusty) - nic do przekazania. Perk Insurance Plans: u AI ucina SmallGold, u gracza placi gra (jego ubezpieczenie).
    /// Gracz: jego karawana zajeta - wiadomosc po angielsku (strata, przewodnik w niewoli); gracz zdobywca - wiadomosc, co wzial.
    ///
    /// OBIEG ZAMKNIETY: zloto partia -> bohater (swiat bez zmian), sztuki roster -> roster (ksiega towarow: przewoz, zero), ludzie do DOM (ksiega ludzi), bohaterowie
    /// do niewoli. Kontrola w linii: zdjete z karawan = dopisane (zloto i sztuki), karawany, ktore nie zniknely - 0.
    /// Linia "Karawany w zdobytym miescie (L5b-2): dzien" przy kazdym zdobyciu twierdzy (takze bez karawan - pomiar "ile karawan stoi w murach"). Stan tylko w pamieci.
    /// </summary>
    internal static class EnemyCaravans
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.EnemyCaravansSeized; } }

        // spis z prefiksu (jedno zdobycie naraz - SiegeCompleted nie jest zagniezdzone)
        private sealed class Snap
        {
            public Settlement St; public MobileParty Cap; public IFaction WinF;
            public readonly List<MobileParty> Enemy = new List<MobileParty>();
            public int All, Naval, Friendly, InBattle;
        }

        // liczniki doby (Last* dla "Obieg: notable i karawany")
        private static int _dCaps, _dSeized;
        private static long _dGold, _dUnits;
        internal static int LastCaps, LastSeized;
        internal static long LastGold, LastUnits;
        private static int _stumbles, _dStumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _hookTried;

        internal static void Reset()
        {
            _dCaps = _dSeized = 0; _dGold = _dUnits = 0; LastCaps = LastSeized = 0; LastGold = LastUnits = 0;
            _stumbles = _dStumbles = 0; _err.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_err.Add(where)) Log.Error("EnemyCaravans." + where, e); } catch { }
        }

        private static string Nm(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }

        /// <summary>Seal31.EnsureHooks (w kampanii, raz na proces): prefiks (spis) i postfiks (zajecie) na KingdomManager.SiegeCompleted.</summary>
        internal static void EnsureHooks()
        {
            if (_hookTried) return;
            _hookTried = true;
            try
            {
                Seal31.Patch("L5b-2: karawany wroga w zdobytym miescie (KingdomManager.SiegeCompleted)", AccessTools.Method(typeof(KingdomManager), "SiegeCompleted"),
                             typeof(EnemyCaravans), nameof(SiegePre), nameof(SiegePost), null);
            }
            catch (Exception e) { Stumble("EnsureHooks", e); }
        }

        /// <summary>Prefiks: spis karawan w osadzie przed zmiana pana (potem gra je wyprowadza). __state - spis tego wywolania (null - bez zdobycia).</summary>
        public static void SiegePre(Settlement settlement, MobileParty capturerParty, bool isWin, MapEvent.BattleTypes battleType, out object __state)
        {
            __state = null;
            try
            {
                bool capture = isWin && (battleType == MapEvent.BattleTypes.Siege || battleType == MapEvent.BattleTypes.SallyOut
                                         || battleType == MapEvent.BattleTypes.BlockadeSallyOutBattle);
                if (!capture || settlement == null || capturerParty == null || !settlement.IsFortification) return;
                var sn = new Snap { St = settlement, Cap = capturerParty, WinF = capturerParty.MapFaction };
                var list = new List<MobileParty>(settlement.Parties);
                foreach (var mp in list)
                {
                    try
                    {
                        if (mp == null || !mp.IsActive || !mp.IsCaravan) continue;
                        sn.All++;
                        var f = mp.MapFaction;
                        if (f == null || sn.WinF == null || !f.IsAtWarWith(sn.WinF)) { sn.Friendly++; continue; }
                        if (mp.Ships != null && mp.Ships.Count > 0) { sn.Naval++; continue; }   // w porcie odplywa - gra
                        if (mp.MapEvent != null) { sn.InBattle++; continue; }
                        sn.Enemy.Add(mp);
                    }
                    catch (Exception e) { Stumble("SiegePre(karawana)", e); }
                }
                __state = sn;
            }
            catch (Exception e) { Stumble("SiegePre", e); }
        }

        /// <summary>Postfiks: zdobycie potwierdzone (osada ma frakcje zdobywcy) - zajecie karawan ze spisu, linia, wiadomosci gracza.</summary>
        public static void SiegePost(object __state)
        {
            var sn = __state as Snap;
            if (sn == null) return;
            try { Seize(sn); }
            catch (Exception e) { Stumble("SiegePost", e); }
        }

        private static void Seize(Snap sn)
        {
            var s = Settings.Current;
            int day = (int)CampaignTime.Now.ToDays;
            var st = sn.St; var cap = sn.Cap;
            bool taken = st.MapFaction != null && sn.WinF != null && ReferenceEquals(st.MapFaction, sn.WinF);
            bool undead = false;
            try { undead = Undead.Party(cap); } catch { undead = false; }
            _dCaps++;
            Hero lead = cap != null && cap.IsActive ? cap.LeaderHero : null;
            int seized = 0, survived = 0, skipped = 0, subjects = 0, heroes = 0, men = 0, horses = 0, playerN = 0;
            long goldOff = 0, goldOn = 0, unitsOff = 0, unitsOn = 0, unitsShelf = 0, value = 0, menPurse = 0, playerGold = 0, playerUnits = 0;
            string why = null;
            var ex = new List<string>();
            var playerLeaders = new List<string>();
            if (!taken) why = "osada nie zmienila frakcji - bez zajecia";
            else if (undead) why = "zdobywca Inni - karawany wyjezdzaja jak w grze";
            else if (cap == null || !cap.IsActive) why = "druzyna zdobywcy nieczynna - karawany wyjezdzaja jak w grze";
            else if (!On) why = "W CIENIU (klucz wylaczony) - karawany wyjezdzaja jak w grze";
            foreach (var c in sn.Enemy)
            {
                if (why != null) { skipped++; continue; }
                try
                {
                    if (c == null || !c.IsActive || c.MapEvent != null) { skipped++; continue; }
                    // po zmianie pana: karawana kupca tej osady (i jej wsi) ma frakcje osady - jest juz poddana zdobywcy i zostaje (gra tez jej nie wyprowadza);
                    // jej pan i jej kupcy placa przez lup osady (L2) wedlug wyboru zdobywcy - laska zostaje laska
                    var fNow = c.MapFaction;
                    if (fNow == null || !fNow.IsAtWarWith(sn.WinF)) { subjects++; continue; }
                    bool player = c.ActualClan == Clan.PlayerClan || (c.Owner != null && c.Owner.Clan == Clan.PlayerClan);
                    string owner = Nm(c.Owner ?? (c.Party != null ? c.Party.Owner : null));
                    // 1. bohaterowie -> niewola zdobywcy
                    var hs = new List<Hero>();
                    var mr = c.MemberRoster;
                    if (mr != null)
                        for (int i = 0; i < mr.Count; i++)
                        {
                            var ch = mr.GetCharacterAtIndex(i);
                            if (ch != null && ch.IsHero && ch.HeroObject != null && ch.HeroObject.IsAlive && ch.HeroObject != Hero.MainHero) hs.Add(ch.HeroObject);
                        }
                    foreach (var h in hs)
                    {
                        try { TakePrisonerAction.Apply(cap.Party, h); heroes++; if (player) playerLeaders.Add(Nm(h)); }
                        catch (Exception e) { Stumble("Seize(niewola)", e); }
                    }
                    // 2. kiesa -> wodz zdobywcow (regula 3.2), korona 1/9 od wodza-wasala
                    int g = c.PartyTradeGold, gotG = 0;
                    if (g > 0)
                    {
                        goldOff += g;
                        if (lead != null && lead.IsAlive)
                        {
                            int h0 = lead.Gold;
                            GiveGoldAction.ApplyForPartyToCharacter(c.Party, lead, g, true);
                            gotG = lead.Gold - h0;
                            Ransom178.NoteBeatenGold(lead, gotG);
                        }
                        else if (cap.IsPartyTradeActive)
                        {
                            int p0 = cap.PartyTradeGold;
                            GiveGoldAction.ApplyForPartyToParty(c.Party, cap.Party, g, true);
                            gotG = cap.PartyTradeGold - p0;
                        }
                        else if (st.SettlementComponent != null)
                        {
                            int s0 = st.SettlementComponent.Gold;
                            GiveGoldAction.ApplyForPartyToSettlement(c.Party, st, g, true);
                            gotG = st.SettlementComponent.Gold - s0;
                        }
                        goldOn += gotG;
                    }
                    // 3. ladunek -> tabor wodza (sztuka po sztuce: zdjeta z karawany = dopisana wodzowi albo polce osady). Poprawka po audycie (obieg Z5, gracz 1):
                    //    ta sama miara co L7 - zwierzeta (konie, juczne, zywiec) najpierw, do granicy stada bez kary "Herding" gry; reszta najcenniejsze na kg do
                    //    wolnego udzwigu PO zwierzetach (juczne niosa). Czego wodz nie uniesie albo nie poprowadzi - na polke zdobytej osady (nie znika, nie plonie);
                    //    osada bez polki - wszystko do wodza, jak dotad
                    int u = 0, uAll = 0;
                    var pack = c.ItemRoster;
                    var shelf = st.ItemRoster;
                    if (pack != null)
                    {
                        var els = new List<ItemRosterElement>();
                        for (int i = 0; i < pack.Count; i++)
                        {
                            var el = pack.GetElementCopyAtIndex(i);
                            if (el.Amount <= 0 || el.EquipmentElement.Item == null || el.EquipmentElement.IsQuestItem) continue;
                            els.Add(el);
                        }
                        els.Sort((x, y) =>
                        {
                            bool wx = CitySack.Walks7(x.EquipmentElement.Item), wy = CitySack.Walks7(y.EquipmentElement.Item);
                            if (wx != wy) return wx ? -1 : 1;
                            return CitySack.PerKg7(y.EquipmentElement.Item).CompareTo(CitySack.PerKg7(x.EquipmentElement.Item));
                        });
                        int herd = CitySack.HerdRoom(cap);
                        double free = double.NaN;                       // wolny udzwig liczony po zwierzetach
                        foreach (var el in els)
                        {
                            var ee = el.EquipmentElement;
                            int at = pack.FindIndexOfElement(ee);
                            int n = at >= 0 ? pack.GetElementNumber(at) : 0;
                            if (n <= 0) continue;
                            bool walks = CitySack.Walks7(ee.Item);
                            double w = Math.Max(0f, ee.Item.Weight);
                            int fit;
                            if (shelf == null) fit = n;
                            else if (walks) fit = Math.Min(n, Math.Max(0, herd));
                            else
                            {
                                if (double.IsNaN(free)) free = CitySack.FreeKg(cap);
                                fit = w > 1e-6 ? (int)Math.Min(n, Math.Floor(Math.Max(0.0, free) / w)) : n;
                            }
                            int dst0 = CountOf(cap.ItemRoster, ee), sh0 = CountOf(shelf, ee);
                            pack.AddToCounts(ee, -n);
                            int at2 = pack.FindIndexOfElement(ee);
                            int off = n - (at2 >= 0 ? pack.GetElementNumber(at2) : 0);
                            if (off <= 0) continue;
                            int toCap = Math.Min(fit, off), toShelf = off - toCap;
                            if (toCap > 0) cap.ItemRoster.AddToCounts(ee, toCap);
                            if (toShelf > 0) shelf.AddToCounts(ee, toShelf);
                            int on = CountOf(cap.ItemRoster, ee) - dst0;
                            int onShelf = toShelf > 0 ? CountOf(shelf, ee) - sh0 : 0;
                            unitsOff += off; unitsOn += on + onShelf; u += on; uAll += on + onShelf; unitsShelf += onShelf;
                            value += (long)on * Math.Max(0, ee.ItemValue);
                            if (ee.Item.HasHorseComponent) horses += on;
                            if (walks) herd -= on; else if (!double.IsNaN(free)) free -= on * w;
                        }
                    }
                    // 4. pusta karawana - rozwiazana bez niszczyciela (eskorta do domu, sakiewki eskorty do kasy najblizszego miasta)
                    int m = mr != null ? mr.TotalManCount : 0;
                    int mp0 = 0;
                    try { mp0 = MenPurse.Get(c); } catch { mp0 = 0; }
                    if (c.IsActive) DestroyPartyAction.Apply(null, c);   // recenzja: partia usunieta przez cudzy nasluch (np. przy niewoli przewodnika) - bez drugiego zdarzenia zniszczenia
                    if (c.IsActive) { survived++; continue; }
                    seized++; men += m; menPurse += mp0;
                    if (player) { playerN++; playerGold += g; playerUnits += uAll; }
                    if (ex.Count < 4) ex.Add(owner + ": kiesa " + g + ", sztuk " + uAll + (uAll != u ? " (wodzowi " + u + ")" : "") + ", ludzie " + m + (hs.Count > 0 ? ", bohaterow " + hs.Count : ""));
                }
                catch (Exception e) { Stumble("Seize(karawana)", e); survived++; }
            }
            _dSeized += seized; _dGold += goldOn; _dUnits += unitsOn;
            var sb = new StringBuilder(800);
            sb.Append("Karawany w zdobytym miescie (L5b-2): dzien ").Append(day)
              .Append(" | osada ").Append(Nm(st)).Append(" (").Append(st.StringId).Append(") zdobyta przez ").Append(cap != null && cap.Name != null ? cap.Name.ToString() : "-")
              .Append(" (").Append(Nm(sn.WinF)).Append(", wodz ").Append(Nm(lead)).Append(')')
              .Append(" | karawan w murach ").Append(sn.All).Append(": wroga ").Append(sn.Enemy.Count).Append(", niewrogich ").Append(sn.Friendly)
              .Append(", ze statkami (odplywaja) ").Append(sn.Naval).Append(", w bitwie ").Append(sn.InBattle)
              .Append(" | zajete ").Append(seized).Append(", poddani nowego pana (zostaja) ").Append(subjects).Append(", pominiete ").Append(skipped).Append(why != null ? " (" + why + ")" : "")
              .Append(" | kiesy: zdjete ").Append(goldOff).Append(" - dopisane ").Append(goldOn).Append(" = ").Append(goldOff - goldOn)
              .Append(" | ladunek: zdjete ").Append(unitsOff).Append(" - dopisane ").Append(unitsOn).Append(" = ").Append(unitsOff - unitsOn)
              .Append(" szt. (wartosc ").Append(value).Append(", konie i juczne ").Append(horses).Append(')')
              .Append(" | bohaterowie do niewoli ").Append(heroes).Append(", eskorta do domu ").Append(men).Append(" (sakiewki eskorty ").Append(menPurse).Append(" do kasy najblizszego miasta)")
              .Append(" | z dopisanych na polke osady ").Append(unitsShelf).Append(" szt. (ponad udzwig i stado wodza; wartosc i konie wyzej - tylko tabor wodza)")
              .Append(" | nie zniknely ").Append(survived)
              .Append(" | gracz: karawan zajetych ").Append(playerN).Append(cap != null && cap.IsMainParty ? ", zdobywca" : "")
              .Append(ex.Count > 0 ? " | " + string.Join("; ", ex.ToArray()) : "")
              .Append(_dStumbles > 0 ? " | potkniecia " + _dStumbles + " (od wczytania " + _stumbles + ")" : "").Append('.');
            Log.Info(sb.ToString());
            try
            {
                if (playerN > 0)
                    Log.Player(Nm(st) + " has fallen to " + Nm(sn.WinF) + ": " + (playerN == 1 ? "your caravan there was" : playerN + " of your caravans there were") + " seized - "
                               + playerGold.ToString("N0", Inv) + " denars and " + playerUnits + " items of cargo and pack animals are lost"
                               + (playerLeaders.Count > 0 ? "; " + string.Join(", ", playerLeaders.ToArray()) + (playerLeaders.Count == 1 ? " is" : " are") + " now held prisoner" : "") + ".", true);
                if (cap != null && cap.IsMainParty && seized > 0)
                    Log.Player("In " + Nm(st) + " your men seized " + (seized == 1 ? "an enemy caravan" : seized + " enemy caravans") + ": " + goldOn.ToString("N0", Inv) + " denars go to your purse and "
                               + (unitsOn - unitsShelf) + " items of cargo, horses and pack animals to your baggage"
                               + (unitsShelf > 0 ? " (" + unitsShelf + " more that your men could not carry or herd are left " + (st.IsTown ? "in the market of " : "behind in ") + Nm(st) + ")" : "") + (heroes > 0 ? "; " + heroes + (heroes == 1 ? " caravan master is your prisoner" : " caravan masters are your prisoners") : "") + ".");
            }
            catch (Exception e) { Stumble("Seize(wiadomosc)", e); }
        }

        private static int CountOf(ItemRoster r, EquipmentElement ee)
        {
            if (r == null) return 0;
            int at = r.FindIndexOfElement(ee);
            return at >= 0 ? r.GetElementNumber(at) : 0;
        }

        /// <summary>Raz na dobe, przed MoneyLedger.Daily ("Obieg: notable i karawany" czyta Last*) - liczby doby. Nie rusza zlota.</summary>
        internal static void DayRoll()
        {
            LastCaps = _dCaps; LastSeized = _dSeized; LastGold = _dGold; LastUnits = _dUnits;
            _dCaps = _dSeized = 0; _dGold = _dUnits = 0; _dStumbles = 0;
        }
    }
}
