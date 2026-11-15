using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 563 (Jeff 15.11): SZTABY STALI VALYRIANSKIEJ - zgodne z 177 (skonczony zasob). Klucz ValyrianIngots.
    ///  - JEDNOSTKA BILANSU to sztaba: 1 miara = IngotsPerMeasure (10) sztab. Stala w kodzie, nie w MCM (zmiana w trakcie kampanii rozjechalaby rejestr).
    ///    Stan = 10 x suma(klingi x miary) + sztaby w swiecie; Baza = 10 x BaseMeasures + R_I (rejestr sztab); Start = Baza przy pierwszym spisie 563 (zapis).
    ///    Stan == Baza == Start, poza legalnymi stratami (NoteLoss: odzysk sprzed 177 i stara droga przetopu przy ValyrianIngots NIE - "stal ginie").
    ///  - PRZETOP (kuznia gracza, kowal od ValyrianSmeltSkill): klinga schodzi z rejestru, R_I += 10 x miary, sztaby do taboru gracza; wynik metalu gry
    ///    zerowany (SmeltTab.OutputPostfix) - wegiel jak zwykle.
    ///  - KUCIE (perk Legendary Smith + ValyrianForgeSkill, kuznia gracza, nie zlecenie): projekt z klinga VS (QohorMasters.VsBlades i ich "_duel")
    ///    zamiast metalu klingi bierze 10 sztab (R_I -= 10); nowy miecz to wlasny projekt 1 miary (_own, jak wariant B Qohoru; RegistryMove nic -> id).
    ///    Bramka w UI (CraftingVM.RefreshEnableMainAction + HaveMaterialsNeeded) - przycisk szary z powodem, bez odmowy w srodku kucia (CTD).
    ///  - SPIS: sztaby z tych samych miejsc co klingi (tabory, targi, schowki, magazyn DTE gracza, magazyn wojenny Spoils, wozy kupcow); miecz na
    ///    wykonczeniu w kuzni gracza - miejsce nie do zmiany. Straznik: sztaby ponad R_I -> Castle-forged Steel (Iron6) w tym samym miejscu.
    ///  - PERK: Legendary Smith na 300 (klucz ValyrianLegendarySmithAt300, ustawiane w kampanii), opis przez PerkTexts, komunikat odblokowania raz.
    /// Kazda latka ma wlasny try/catch z licznikiem potkniec (log do 3 razy), bez globalnego wylacznika.
    /// </summary>
    internal static partial class ValyrianBlades
    {
        internal const int IngotsPerMeasure = 10;
        internal const string IngotId = "arm_valyrian_steel";
        internal const string IngotCategoryId = "arm_vs_stuff";

        internal static bool IngotsOn { get { var s = Settings.Current; return s != null && s.ValyrianIngots; } }

        private static ItemObject _ingot;
        private static object _ingotFor;
        /// <summary>Przedmiot sztaby (leniwie, na kampanie); null - XML nie wczytany.</summary>
        internal static ItemObject Ingot
        {
            get
            {
                var c = Campaign.Current;
                if (c != null && ReferenceEquals(c, _ingotFor) && _ingot != null) return _ingot;
                ItemObject it = null;
                try { it = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<ItemObject>(IngotId) : null; } catch { }
                _ingot = it; _ingotFor = c;
                return it;
            }
        }

        internal static bool IsIngot(ItemObject it) { return it != null && it.StringId == IngotId; }

        // ------------------------------------------------------------ stan 563 (zapis arm_vs_ingots)
        private static int _regIngots;          // R_I
        private static int _startUnits = -1;    // Start (sztaby); -1 = jeszcze nie ustawiony
        private static int _smeltedUnits, _forgedSwords;
        private static bool _unlockShown;
        private static int _stumbles563;
        private static int _dSmeltPieces, _dSmeltIngots, _dForged, _dGateRefusals, _dGuardIngots;
        private static string _lastGate;
        private static readonly HashSet<string> _balanceLogged = new HashSet<string>();
        private static readonly HashSet<string> _ingotLossLogged = new HashSet<string>();
        private static ItemObject _pricePending;
        private static Hero _smeltHero;
        private static int _smeltMeasures;

        private static void Stumble563(string where, Exception e)
        {
            _stumbles563++;
            if (_stumbles563 <= 3) Log.Error("ValyrianBlades563." + where + " (potkniecie " + _stumbles563 + ")", e);
        }

        private static void ResetIngots()
        {
            _regIngots = 0; _startUnits = -1; _smeltedUnits = 0; _forgedSwords = 0; _unlockShown = false; _stumbles563 = 0;
            _dSmeltPieces = _dSmeltIngots = _dForged = _dGateRefusals = _dGuardIngots = 0; _lastGate = null;
            _balanceLogged.Clear(); _ingotLossLogged.Clear(); _pricePending = null; _smeltHero = null; _smeltMeasures = 0;
            _vsPieces = null; _revealPieces = null; _vsPiecesFor = null;
            try { RestoreDesigner(); } catch { }
        }

        /// <summary>"v1|R_I|Start|przetopione sztaby|wykute miecze|komunikat odblokowania".</summary>
        internal static string ExportIngots()
        {
            return "v1|" + _regIngots.ToString(CultureInfo.InvariantCulture) + "|" + _startUnits.ToString(CultureInfo.InvariantCulture) + "|"
                   + _smeltedUnits.ToString(CultureInfo.InvariantCulture) + "|" + _forgedSwords.ToString(CultureInfo.InvariantCulture) + "|" + (_unlockShown ? "1" : "0");
        }

        internal static void ImportIngots(string s)
        {
            _regIngots = 0; _startUnits = -1; _smeltedUnits = 0; _forgedSwords = 0; _unlockShown = false;
            if (string.IsNullOrEmpty(s) || !s.StartsWith("v1|")) return;   // stary zapis bez klucza: R_I 0, Start przy pierwszym spisie
            var f = s.Split('|');
            int n;
            if (f.Length > 1 && int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) _regIngots = Math.Max(0, n);
            if (f.Length > 2 && int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) _startUnits = Math.Max(-1, n);
            if (f.Length > 3 && int.TryParse(f[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) _smeltedUnits = Math.Max(0, n);
            if (f.Length > 4 && int.TryParse(f[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) _forgedSwords = Math.Max(0, n);
            if (f.Length > 5) _unlockShown = f[5] == "1";
        }

        /// <summary>Baza w sztabach: 10 x miary rejestru klng + rejestr sztab.</summary>
        internal static int BaseUnits() { return IngotsPerMeasure * BaseMeasures() + _regIngots; }

        /// <summary>Legalna strata stali (odzysk sprzed 177, przetop "stal ginie" przy ValyrianIngots NIE): Start w dol razem z baza.</summary>
        internal static void NoteLoss(int units)
        {
            if (units <= 0 || _startUnits < 0) return;
            int was = _startUnits;
            _startUnits = Math.Max(0, _startUnits - units);
            Log.Info("Stal valyrianska 563: legalna strata " + units + " sztab (stal ginie) - start " + was + " -> " + _startUnits + ".");
        }

        // ------------------------------------------------------------ czesci VS w projektancie
        private static HashSet<CraftingPiece> _vsPieces, _revealPieces;
        private static object _vsPiecesFor;

        /// <summary>Czesci stali valyrianskiej: klingi QohorMasters.VsBlades i ich blizniaki "_duel" (zawsze), plus klingi wzorow VS (rejestr 177),
        /// o ile nie uzywa ich zaden zwykly przedmiot z danych gry (wyroby kuzni gracza pominiete - stara dziura kucia VS z Iron6 nie odbiera klingom miana).</summary>
        private static void EnsurePieces()
        {
            var c = Campaign.Current;
            if (_vsPieces != null && ReferenceEquals(c, _vsPiecesFor)) return;
            var vs = new HashSet<CraftingPiece>();
            var reveal = new HashSet<CraftingPiece>();
            try
            {
                foreach (var id0 in QohorMasters.VsBlades)
                    foreach (var id in new[] { id0, id0 + "_duel" })
                    {
                        CraftingPiece p = null;
                        try { p = MBObjectManager.Instance.GetObject<CraftingPiece>(id); } catch { }
                        if (p == null) continue;
                        vs.Add(p); reveal.Add(p);
                    }
                var common = new HashSet<CraftingPiece>();
                var vsItems = new HashSet<CraftingPiece>();
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    try
                    {
                        if (it == null || it.WeaponDesign == null || it.WeaponDesign.UsedPieces == null) continue;
                        bool isVs = Is(it);
                        if (!isVs && it.IsCraftedByPlayer) continue;
                        foreach (var el in it.WeaponDesign.UsedPieces)
                        {
                            var p = el != null ? el.CraftingPiece : null;
                            if (p == null || p.PieceType != CraftingPiece.PieceTypes.Blade) continue;
                            if (isVs) vsItems.Add(p); else common.Add(p);
                        }
                    }
                    catch (Exception e) { Stumble563("EnsurePieces(item)", e); }
                }
                foreach (var p in vsItems) if (!common.Contains(p)) vs.Add(p);
            }
            catch (Exception e) { Stumble563("EnsurePieces", e); }
            _vsPieces = vs; _revealPieces = reveal; _vsPiecesFor = c;
        }

        internal static bool IsVsPiece(CraftingPiece p)
        {
            if (p == null) return false;
            EnsurePieces();
            return _vsPieces != null && _vsPieces.Contains(p);
        }

        /// <summary>Czy projekt ma klinge ze stali valyrianskiej.</summary>
        internal static bool UsesVs(WeaponDesign d)
        {
            try
            {
                if (d == null || d.UsedPieces == null) return false;
                foreach (var el in d.UsedPieces)
                    if (el != null && el.IsValid && IsVsPiece(el.CraftingPiece)) return true;
            }
            catch (Exception e) { Stumble563("UsesVs", e); }
            return false;
        }

        /// <summary>Miary projektu: 2 - szablon dwureczny z klinga >= ValyrianGreatswordMinCm, inaczej 1 (ta sama zasada co Measures).</summary>
        internal static int DesignMeasures(WeaponDesign d)
        {
            try
            {
                var tpl = d != null ? d.Template : null;
                if (tpl == null || tpl.StringId == null || tpl.StringId.IndexOf("TwoHanded", StringComparison.Ordinal) < 0) return 1;
                float min = Settings.Current != null ? Settings.Current.ValyrianGreatswordMinCm : 105f;
                foreach (var el in d.UsedPieces)
                    if (el != null && el.IsValid && el.CraftingPiece != null && el.CraftingPiece.PieceType == CraftingPiece.PieceTypes.Blade)
                        return el.ScaledLength * 100f >= min ? 2 : 1;
            }
            catch (Exception e) { Stumble563("DesignMeasures", e); }
            return 1;
        }

        internal static int IngotsInBaggage()
        {
            try
            {
                var it = Ingot;
                var r = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
                return it != null && r != null ? r.GetItemNumber(it) : 0;
            }
            catch { return 0; }
        }

        private static int Smithing(Hero h)
        {
            try { return h != null ? h.GetSkillValue(DefaultSkills.Crafting) : 0; } catch { return 0; }
        }

        /// <summary>Kowal moze kuc stal valyrianska: perk Legendary Smith i Smithing >= ValyrianForgeSkill (jawnie - perk sprzed przeniesienia na 300 zostaje).</summary>
        internal static bool CanForge(Hero h)
        {
            try
            {
                var s = Settings.Current;
                return h != null && s != null && h.GetPerkValue(DefaultPerks.Crafting.LegendarySmith) && Smithing(h) >= s.ValyrianForgeSkill;
            }
            catch { return false; }
        }

        private static List<Hero> PartyHeroes()
        {
            var l = new List<Hero>();
            try
            {
                var mp = MobileParty.MainParty;
                if (mp == null || mp.MemberRoster == null) return l;
                foreach (var e in mp.MemberRoster.GetTroopRoster())
                    if (e.Character != null && e.Character.IsHero && e.Character.HeroObject != null) l.Add(e.Character.HeroObject);
            }
            catch { }
            return l;
        }

        // ------------------------------------------------------------ spis sztab (Gather, Daily, Summary)
        /// <summary>Gather: sztuki na wykonczeniu w kuzni gracza (projekt "van" - miecz zabrany z taboru do Forge.Deliver) - miejsce nie do zmiany.</summary>
        private static void ForgeCensus(Action<Copy> add)
        {
            try
            {
                var b = ArmouryBehavior.Instance;
                if (b == null) return;
                foreach (var line in b.ProjectLines.ToList())
                {
                    var p = Project.Parse(line);
                    if (p.Item == null || !Is(p.Item)) continue;
                    string town = p.SettlementId;
                    try { var st = TaleWorlds.CampaignSystem.Settlements.Settlement.Find(p.SettlementId ?? ""); if (st != null) town = st.Name.ToString(); } catch { }
                    for (int k = 0; k < Math.Max(1, p.Count); k++)
                        add(new Copy { Id = p.Item.StringId, Kind = K.Fixed, Pri = 4, Where = "w kuzni gracza " + town + " (wykonczenie)" });
                }
            }
            catch (Exception e) { Stumble563("ForgeCensus", e); }
        }

        /// <summary>Daily: sztaby ponad rejestr -> Castle-forged Steel w tym samym miejscu; ponizej rejestru - ERROR (raz na wartosc). Zwraca liczbe zamian.</summary>
        private static int DailyIngots(Dictionary<string, List<Copy>> copies, List<string> notes)
        {
            try
            {
                List<Copy> l; copies.TryGetValue(IngotId, out l);
                if (l == null) l = new List<Copy>();
                int have = l.Count;
                var s = Settings.Current;
                if (have > _regIngots && s != null && s.ValyrianGuard) return SurplusIngots(l, notes, false);
                if (have < _regIngots && !_recoverPending)
                {
                    if (_ingotLossLogged.Add(have + "|" + _regIngots))
                        Log.Error("ValyrianBlades.Census", new InvalidOperationException("Stal valyrianska: UBYTEK sztab -" + (_regIngots - have) + " (jest " + have + ", rejestr " + _regIngots + "), ostatnio: " + LastIngotWhere()));
                }
                else _ingotLossLogged.Clear();
            }
            catch (Exception e) { Stumble563("DailyIngots", e); }
            return 0;
        }

        private static string _lastIngotWhere = "";
        private static string LastIngotWhere() { return _lastIngotWhere.Length > 0 ? _lastIngotWhere : "nieznane"; }

        /// <summary>Nadwyzka sztab ponad R_I: kolejnosc zostawiania jak klingi (miejsca nie do zmiany, potem Pri); reszta -> Castle-forged Steel (Iron6).
        /// Wiadomosc gracza zbiorcza (jedna na wywolanie).</summary>
        private static int SurplusIngots(List<Copy> l, List<string> notes, bool now)
        {
            if (l == null || l.Count <= _regIngots) return 0;
            var iron6 = Recipes.MaterialItem(CraftingMaterials.Iron6);
            var order = l.OrderBy(c => c.Kind == K.Fixed ? 0 : 1).ThenBy(c => c.Pri).ToList();
            var perWhere = new Dictionary<string, int>();
            int n = 0, player = 0;
            for (int i = _regIngots; i < order.Count; i++)
            {
                var c = order[i];
                try
                {
                    if (c.Kind == K.Fixed || c.Kind == K.Cart) { notes.Add("sztaba nadwyzka w miejscu nie do zmiany (" + c.Where + ")"); continue; }
                    if (c.Kind == K.Roster && c.Roster != null)
                    {
                        c.Roster.AddToCounts(c.El, -1);
                        if (iron6 != null) c.Roster.AddToCounts(iron6, 1);
                    }
                    else if (c.Kind == K.Stock) StockSwap(c.Stock, IngotId, iron6);
                    else continue;
                    n++;
                    if (c.Player) player++;
                    int v; perWhere.TryGetValue(c.Where ?? "?", out v); perWhere[c.Where ?? "?"] = v + 1;
                    l.Remove(c);
                }
                catch (Exception e) { Stumble563("SurplusIngots", e); }
            }
            foreach (var kv in perWhere) notes.Add("Z NICZEGO +" + kv.Value + " sztab " + kv.Key + " -> " + (iron6 != null ? iron6.StringId : "nic"));
            if (player > 0)
                Log.Player("There is more Valyrian steel in the world than was ever forged: " + player + (player == 1 ? " ingot" : " ingots") + " of yours "
                           + (player == 1 ? "is" : "are") + " only Castle-forged Steel.", true);
            _dGuardIngots += n;
            return n;
        }

        /// <summary>Straznik sztab teraz (przy wejsciu do kuzni i przed przetopem): kopia z niczego nie przejdzie przez kucie jako prawdziwa stal.</summary>
        internal static bool GuardIngotsNow(string why)
        {
            if (_reg == null || Settings.Current == null || !Settings.Current.ValyrianGuard) return false;
            try
            {
                var copies = Gather();
                List<Copy> l; copies.TryGetValue(IngotId, out l);
                if (l == null || l.Count <= _regIngots) return false;
                var notes = new List<string>();
                int n = SurplusIngots(l, notes, true);
                if (notes.Count > 0) Log.Info("Stal valyrianska: dzien " + (int)CampaignTime.Now.ToDays + " (" + why + ") - " + string.Join(" | ", notes.ToArray()) + ".");
                return n > 0;
            }
            catch (Exception e) { Stumble563("GuardIngotsNow", e); return false; }
        }

        /// <summary>Dopisek linii spisu: sztaby i bilans. Ustawia Start przy pierwszym spisie. Rozjazd bazy ze startem = ERROR "BILANS" (raz na wartosc).</summary>
        private static string IngotSummary(Dictionary<string, List<Copy>> copies, int measures, bool start)
        {
            try
            {
                List<Copy> l; copies.TryGetValue(IngotId, out l);
                int I = l != null ? l.Count : 0, mine = 0, ai = 0, shelves = 0, stash = 0, carts = 0, other = 0;
                if (l != null)
                    foreach (var c in l)
                    {
                        if (c.Kind == K.Cart) carts++;
                        else if (c.Where != null && c.Where.StartsWith("targ")) shelves++;
                        else if (c.Where != null && (c.Where.StartsWith("schowek") || c.Where.StartsWith("magazyn"))) stash++;
                        else if (c.Where == "tabor gracza") mine++;
                        else if (c.Where != null && c.Where.StartsWith("tabor")) ai++;
                        else other++;
                    }
                if (l != null && l.Count > 0) _lastIngotWhere = string.Join("; ", l.Select(c => c.Where).Distinct().Take(6).ToArray());
                int S = IngotsPerMeasure * measures + I;
                int T = BaseUnits();
                if (_startUnits < 0 && !start)
                {
                    _startUnits = T;
                    Log.Info("Stal valyrianska 563: pierwszy spis - start bilansu " + T + " sztab (" + BaseMeasures() + " miar x " + IngotsPerMeasure + " + rejestr sztab " + _regIngots + ").");
                }
                int U = _startUnits;
                string verdict;
                if (U >= 0 && T != U)
                {
                    verdict = "ROZNICA " + (T - U);
                    if (_balanceLogged.Add(T + "|" + U))
                        Log.Error("ValyrianBlades.Census", new InvalidOperationException("Stal valyrianska: BILANS - baza " + T + " sztab, start " + U + " (roznica " + (T - U) + "); rejestr klng " + BaseMeasures() + " miar, rejestr sztab " + _regIngots));
                }
                else if (S != T) verdict = "ROZNICA " + (S - T);
                else verdict = U < 0 ? "OK (start przy pierwszym spisie)" : "OK";
                return ", sztaby " + I + " (= " + (I / (float)IngotsPerMeasure).ToString("0.#", CultureInfo.InvariantCulture) + " miar: tabor gracza " + mine + ", tabory AI " + ai
                       + ", targi " + shelves + ", schowki i magazyny " + stash + ", w drodze wozem " + carts + (other > 0 ? ", inne " + other : "") + "; rejestr sztab " + _regIngots
                       + "); bilans: stan " + S + " sztab, baza " + T + ", start " + U + " [" + verdict + "]";
            }
            catch (Exception e) { Stumble563("IngotSummary", e); return ", sztaby: blad spisu"; }
        }

        // ------------------------------------------------------------ przetop (latki z ApplyAll)
        /// <summary>Przetop dozwolony: kowal od ValyrianSmeltSkill (albo ValyrianNoSmelt NIE)?</summary>
        private static bool SmeltAllowed(Hero h)
        {
            var s = Settings.Current;
            return s != null && (!s.ValyrianNoSmelt || Smithing(h) >= s.ValyrianSmeltSkill);
        }

        /// <summary>Lista Smelt: ktos w partii moze przetopic (faktycznego kowala sprawdza prefiks DoSmelting).</summary>
        private static bool AnySmelterInParty()
        {
            foreach (var h in PartyHeroes()) if (SmeltAllowed(h)) return true;
            return false;
        }

        /// <summary>SmeltPostfix przy sztabach: klinga -> sztaby (rejestr klingi w dol, R_I w gore, sztaby do taboru gracza).</summary>
        private static void SmeltToIngots(string id, ItemObject item, int gone, ItemRoster r)
        {
            int m = Math.Max(1, _smeltMeasures > 0 ? _smeltMeasures : Measures(item));
            int units = gone * m * IngotsPerMeasure;
            int wasReg = Registered(id), wasI = _regIngots;
            for (int i = 0; i < gone; i++) RegistryMove(new[] { id }, new string[0]);
            _regIngots += units;
            var ing = Ingot;
            if (ing != null) r.AddToCounts(ing, units);
            else Log.Error("ValyrianBlades.SmeltPostfix", new InvalidOperationException("BILANS: brak przedmiotu " + IngotId + " - sztaby nie powstaly (XML arm_items_rare.xml)"));
            _smeltedUnits += units;
            _dSmeltPieces += gone; _dSmeltIngots += units;
            string hero = _smeltHero != null ? _smeltHero.Name.ToString() : "kowal";
            Log.Player((_smeltHero != null ? _smeltHero.Name.ToString() : "Your smith") + " melts down " + item.Name + ": " + units + " ingots of Valyrian steel.");
            Log.Info("Kronika unikatow: " + id + " przetopiona przez " + hero + " (Smithing " + Smithing(_smeltHero) + ") - " + (gone * m) + " miar -> " + units
                     + " sztab; rejestr klingi " + wasReg + " -> " + Registered(id) + ", sztab " + wasI + " -> " + _regIngots + ".");
        }

        /// <summary>Postfiks GetPartResearchGainForSmeltingItem: przetop klingi VS - punkty badan od wartosci wykonania (jak XP przetopu po
        /// HistoricalPrices.XpPostfix), nie od 60 tys. ceny miary (1200 pkt za jeden przetop).</summary>
        public static void ResearchSmeltPostfix(ItemObject item, ref int __result)
        {
            try
            {
                if (!IngotsOn || !Is(item) || Campaign.Current == null) return;
                int xp = Campaign.Current.Models.SmithingModel.GetSkillXpForSmelting(item);
                int cap = 1 + Math.Max(0, xp);
                if (__result > cap) __result = cap;
            }
            catch (Exception e) { Stumble563("ResearchSmeltPostfix", e); }
        }

        // ------------------------------------------------------------ kucie: bramka, koszt, rejestracja
        private static AccessTools.FieldRef<CraftingVM, Crafting> _fCrafting;
        private static bool _fLooked;

        private static WeaponDesign DesignOf(CraftingVM vm)
        {
            if (!_fLooked)
            {
                _fLooked = true;
                try { _fCrafting = AccessTools.FieldRefAccess<CraftingVM, Crafting>("_crafting"); } catch (Exception e) { _fCrafting = null; Stumble563("DesignOf", e); }
            }
            var c = _fCrafting != null && vm != null ? _fCrafting(vm) : null;
            return c != null ? c.CurrentWeaponDesign : null;
        }

        /// <summary>Powod odmowy kucia projektu z klinga VS (napis przycisku, EN) albo null - wolno.</summary>
        internal static string GateReason(Hero hero, WeaponDesign d, bool order)
        {
            var s = Settings.Current;
            if (s == null) return null;
            if (!s.ValyrianIngots) return "Only the masters of Qohor can work Valyrian steel.";
            if (order) return "Valyrian steel is not worked for hire.";
            if (!CanForge(hero)) return "Only a smith with the Legendary Smith perk and " + s.ValyrianForgeSkill + " Smithing can forge Valyrian steel.";
            int need = DesignMeasures(d);
            if (need >= 2) return "A greatsword of Valyrian steel is beyond this forge.";
            int have = IngotsInBaggage();
            if (have < IngotsPerMeasure * need) return "You need " + (IngotsPerMeasure * need) + " ingots of Valyrian steel (you have " + have + ").";
            return null;
        }

        private static string GateText(CraftingVM vm)
        {
            if (vm == null || !vm.IsInCraftingMode || Campaign.Current == null || Campaign.Current.GameMode == CampaignGameMode.Tutorial) return null;
            var d = DesignOf(vm);
            if (d == null || !UsesVs(d)) return null;
            var hero = vm.CurrentCraftingHero != null ? vm.CurrentCraftingHero.Hero : null;
            bool order = vm.WeaponDesign != null && vm.WeaponDesign.IsInOrderMode;
            return GateReason(hero, d, order);
        }

        /// <summary>Postfiks CraftingVM.RefreshEnableMainAction (Priority.Last - po ForgeBill): projekt z klinga VS bez warunku - przycisk szary z powodem.</summary>
        public static void MainActionPostfix(CraftingVM __instance)
        {
            try
            {
                string text = GateText(__instance);
                if (text == null) { _lastGate = null; return; }
                __instance.IsMainActionEnabled = false;
                if (__instance.MainActionHint != null) __instance.MainActionHint = new BasicTooltipViewModel(() => text);
                if (text != _lastGate) { _dGateRefusals++; _lastGate = text; }
            }
            catch (Exception e) { Stumble563("MainActionPostfix", e); }
        }

        /// <summary>Postfiks CraftingVM.HaveMaterialsNeeded: ExecuteMainAction sprawdza go przed kuciem (pas i szelki do przycisku).</summary>
        public static void HaveMaterialsPostfix(CraftingVM __instance, ref bool __result)
        {
            try
            {
                if (!__result) return;
                if (GateText(__instance) != null) __result = false;
            }
            catch (Exception e) { Stumble563("HaveMaterialsPostfix", e); }
        }

        /// <summary>Postfiks GetSmithingCostsForWeaponDesign czynnego modelu (Priority.Last): klinga VS nie bierze sztab zwyklego metalu - bierze sztaby
        /// stali valyrianskiej (OnCraftedVs). Koszty sa ujemne; wynik obciety do &lt;= 0. Rekojesc, jelec i glowica placa normalnie.</summary>
        public static void CostPostfix(WeaponDesign weaponDesign, ref int[] __result)
        {
            try
            {
                if (!IngotsOn || __result == null || weaponDesign == null || weaponDesign.UsedPieces == null) return;
                bool any = false;
                foreach (var el in weaponDesign.UsedPieces)
                {
                    if (el == null || !el.IsValid || !IsVsPiece(el.CraftingPiece) || el.CraftingPiece.MaterialsUsed == null) continue;
                    foreach (var m in el.CraftingPiece.MaterialsUsed)
                    {
                        int i = (int)m.Item1;
                        if (i < 1 || i > 6 || i >= __result.Length || m.Item2 <= 0) continue;
                        // SmeltTruth.CrudeCostPostfix przenosi surowke (1) na zelazo kute (2) - zwrot z tego samego miejsca
                        if (i == 1 && __result[1] == 0 && __result.Length > 2) i = 2;
                        __result[i] += m.Item2;
                        any = true;
                    }
                }
                if (!any) return;
                for (int g = 1; g <= 6 && g < __result.Length; g++) if (__result[g] > 0) __result[g] = 0;
            }
            catch (Exception e) { Stumble563("CostPostfix", e); }
        }

        /// <summary>ArmouryBehavior.OnNewItemCrafted (na poczatku, przed zabraniem na wykonczenie): wykuty projekt z klinga VS - 10 sztab z taboru, R_I w dol,
        /// nowy miecz w rejestrze jako wlasny projekt 1 miary. Cena miary dopiero po XP i badaniach gry (postfiks CreateCraftedWeaponInFreeBuildMode).</summary>
        internal static void OnCraftedVs(ItemObject item, bool isOrder)
        {
            try
            {
                if (item == null || !IngotsOn || _reg == null || !UsesVs(item.WeaponDesign)) return;
                if (isOrder)
                {
                    Log.Error("ValyrianBlades.OnCraftedVs", new InvalidOperationException("BILANS: kucie VS w trybie zlecenia mimo bramki - " + item.StringId + " bez rejestracji"));
                    return;
                }
                var r = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
                var ing = Ingot;
                int need = IngotsPerMeasure * Math.Max(1, DesignMeasures(item.WeaponDesign));
                int have = ing != null && r != null ? r.GetItemNumber(ing) : 0;
                if (r == null || ing == null || have < need || need > IngotsPerMeasure)
                {
                    Log.Error("ValyrianBlades.OnCraftedVs", new InvalidOperationException("BILANS: kucie VS bez sztab (jest " + have + ", potrzeba " + need + ") - " + item.StringId + " zostaje nierozpoznana kopia"));
                    return;
                }
                int wasI = _regIngots;
                r.AddToCounts(ing, -need);
                _regIngots = Math.Max(0, _regIngots - need);
                AddOwn(item.StringId, Hero.MainHero != null ? Hero.MainHero.StringId : null);
                RegistryMove(new string[0], new[] { item.StringId });
                _pricePending = item;
                _forgedSwords++; _dForged++;
                Hero smith = null;
                try { var cb = Campaign.Current.GetCampaignBehavior<ICraftingCampaignBehavior>(); smith = cb != null ? cb.GetActiveCraftingHero() : null; } catch { }
                Log.Info("Kronika unikatow: " + (smith != null ? smith.Name.ToString() : "kowal gracza") + " (Smithing " + Smithing(smith) + ") wykul " + item.Name + " (" + item.StringId
                         + ") ze stali valyrianskiej - " + need + " sztab; rejestr sztab " + wasI + " -> " + _regIngots + ", wlasne +1 (1 miara, razem " + OwnCount + ").");
                Log.Player((smith != null ? smith.Name.ToString() : "Your smith") + " forges " + item.Name + " from " + need + " ingots of Valyrian steel.");
            }
            catch (Exception e) { Stumble563("OnCraftedVs", e); }
        }

        /// <summary>Postfiks CraftingCampaignBehavior.CreateCraftedWeaponInFreeBuildMode: cena miary VS po XP i badaniach gry (liczonych z Value).</summary>
        public static void FreeBuildPostfix(ItemObject __result)
        {
            try
            {
                var it = _pricePending;
                _pricePending = null;
                if (it == null || !ReferenceEquals(it, __result)) return;
                HistoricalPrices.PriceValyrianOne(it);
            }
            catch (Exception e) { Stumble563("FreeBuildPostfix", e); }
        }

        // ------------------------------------------------------------ projektant: czesci VS odkryte tylko na czas ekranu kuzni
        private static readonly List<CraftingPiece> _unhid = new List<CraftingPiece>();
        private static bool _designerOpen;
        private static FieldInfo _fHide;

        /// <summary>Prefiks konstruktora CraftingVM (wejscie do kuzni): gdy ktos w partii moze kuc VS - straznik sztab, czesci VS widoczne w projektancie.</summary>
        public static void CraftingVmPrefix()
        {
            try
            {
                RestoreDesigner();
                if (!IngotsOn || Campaign.Current == null || Campaign.Current.GameMode != CampaignGameMode.Campaign) return;
                bool any = false;
                foreach (var h in PartyHeroes()) if (CanForge(h)) { any = true; break; }
                if (!any) return;
                if (IngotsInBaggage() > 0) GuardIngotsNow("przed przyjeciem w kuzni");
                EnsurePieces();
                if (_fHide == null) _fHide = AccessTools.Field(typeof(CraftingPiece), "<IsHiddenOnDesigner>k__BackingField");
                if (_fHide == null || _revealPieces == null) return;
                foreach (var p in _revealPieces)
                {
                    if (p == null || !p.IsHiddenOnDesigner) continue;
                    _fHide.SetValue(p, false);
                    _unhid.Add(p);
                }
                _designerOpen = true;
            }
            catch (Exception e) { Stumble563("CraftingVmPrefix", e); }
        }

        /// <summary>Postfiks CraftingVM.OnFinalize (wyjscie z kuzni): flagi z powrotem.</summary>
        public static void CraftingVmFinalizePostfix()
        {
            try { RestoreDesigner(); } catch (Exception e) { Stumble563("CraftingVmFinalizePostfix", e); }
        }

        private static void RestoreDesigner()
        {
            _designerOpen = false;
            if (_unhid.Count == 0) return;
            if (_fHide == null) _fHide = AccessTools.Field(typeof(CraftingPiece), "<IsHiddenOnDesigner>k__BackingField");
            foreach (var p in _unhid) try { if (p != null && _fHide != null) _fHide.SetValue(p, true); } catch { }
            _unhid.Clear();
        }

        /// <summary>Postfiks CraftingCampaignBehavior.IsOpened: czesc VS "otwarta" tylko na czas ekranu kuzni (bez zapisu w _openedPartsDictionary).</summary>
        public static void IsOpenedPostfix(CraftingPiece craftingPiece, ref bool __result)
        {
            try
            {
                if (__result || !_designerOpen || craftingPiece == null || _revealPieces == null) return;
                if (_revealPieces.Contains(craftingPiece)) __result = true;
            }
            catch (Exception e) { Stumble563("IsOpenedPostfix", e); }
        }

        // ------------------------------------------------------------ start sesji (kampania): latki, perk, linia startu
        private static Harmony _h563;
        private static readonly HashSet<MethodBase> _patched563 = new HashSet<MethodBase>();
        private static readonly Dictionary<string, string> _hooks563 = new Dictionary<string, string>();

        private static void Hook(string label, MethodBase m, HarmonyMethod pre, HarmonyMethod post, string missing)
        {
            try
            {
                if (m == null) { _hooks563[label] = "BRAK (" + missing + ")"; return; }
                if (!_patched563.Contains(m)) { _h563.Patch(m, prefix: pre, postfix: post); _patched563.Add(m); }
                _hooks563[label] = "wpieta";
            }
            catch (Exception e) { _hooks563[label] = "BLAD (" + missing + ": " + e.GetType().Name + ")"; Stumble563("Hook(" + label + ")", e); }
        }

        /// <summary>ArmouryBehavior.OnSessionLaunched (kampania, przed PerkTexts): latki 563 (raz na proces), perk Legendary Smith na 300, linia startu.</summary>
        internal static void SessionStart563()
        {
            try
            {
                if (_h563 == null) _h563 = new Harmony("com.jeff.armoury.vs563");
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.SmithingModel : null;
                var mc = model != null ? PatchTarget.OfActive(model, "GetSmithingCostsForWeaponDesign", new[] { typeof(WeaponDesign) }, "563: koszt kucia VS") : null;
                Hook("koszt-kucia", mc, null, new HarmonyMethod(typeof(ValyrianBlades), nameof(CostPostfix)) { priority = Priority.Last }, "GetSmithingCostsForWeaponDesign");
                var mr = model != null ? PatchTarget.OfActive(model, "GetPartResearchGainForSmeltingItem", new[] { typeof(ItemObject), typeof(Hero) }, "563: badania z przetopu VS") : null;
                Hook("badania-przetopu", mr, null, new HarmonyMethod(typeof(ValyrianBlades), nameof(ResearchSmeltPostfix)) { priority = Priority.Last }, "GetPartResearchGainForSmeltingItem");
                Hook("przycisk", AccessTools.DeclaredMethod(typeof(CraftingVM), "RefreshEnableMainAction"), null,
                     new HarmonyMethod(typeof(ValyrianBlades), nameof(MainActionPostfix)) { priority = Priority.Last }, "CraftingVM.RefreshEnableMainAction");
                Hook("materialy", AccessTools.DeclaredMethod(typeof(CraftingVM), "HaveMaterialsNeeded"), null,
                     new HarmonyMethod(typeof(ValyrianBlades), nameof(HaveMaterialsPostfix)) { priority = Priority.Last }, "CraftingVM.HaveMaterialsNeeded");
                Hook("IsOpened", AccessTools.DeclaredMethod(typeof(CraftingCampaignBehavior), "IsOpened", new[] { typeof(CraftingPiece), typeof(CraftingTemplate) }), null,
                     new HarmonyMethod(typeof(ValyrianBlades), nameof(IsOpenedPostfix)) { priority = Priority.Last }, "CraftingCampaignBehavior.IsOpened");
                Hook("projektant", AccessTools.GetDeclaredConstructors(typeof(CraftingVM)).FirstOrDefault(),
                     new HarmonyMethod(typeof(ValyrianBlades), nameof(CraftingVmPrefix)), null, "CraftingVM()");
                Hook("projektant-wyjscie", AccessTools.DeclaredMethod(typeof(CraftingVM), "OnFinalize"), null,
                     new HarmonyMethod(typeof(ValyrianBlades), nameof(CraftingVmFinalizePostfix)), "CraftingVM.OnFinalize");
                Hook("cena-po-kuciu", AccessTools.DeclaredMethod(typeof(CraftingCampaignBehavior), "CreateCraftedWeaponInFreeBuildMode"), null,
                     new HarmonyMethod(typeof(ValyrianBlades), nameof(FreeBuildPostfix)) { priority = Priority.Last }, "CreateCraftedWeaponInFreeBuildMode");
            }
            catch (Exception e) { Stumble563("SessionStart563(latki)", e); }

            // perk Legendary Smith na 300 (po AfterLoad - ClearChangedPerks w tej sesji nikomu perku nie zdejmie; DefaultPerks nowe w kazdej kampanii)
            string perk = "bez zmian";
            try
            {
                var s = Settings.Current;
                var p = DefaultPerks.Crafting.LegendarySmith;
                if (s != null && s.ValyrianLegendarySmithAt300 && p != null)
                {
                    var f = AccessTools.Field(typeof(PerkObject), "<RequiredSkillValue>k__BackingField");
                    if (f == null) perk = "BRAK pola RequiredSkillValue";
                    else
                    {
                        float was = p.RequiredSkillValue;
                        if (was < 300f) f.SetValue(p, 300f);
                        perk = "prog " + was.ToString("0", CultureInfo.InvariantCulture) + " -> " + p.RequiredSkillValue.ToString("0", CultureInfo.InvariantCulture);
                        Log.Info("Perk Legendary Smith: " + perk + " (563).");
                    }
                }
            }
            catch (Exception e) { perk = "BLAD"; Stumble563("SessionStart563(perk)", e); }

            try
            {
                var s = Settings.Current;
                EnsurePieces();
                var ing = Ingot;
                var hooks = string.Join(", ", _hooks563.Select(kv => kv.Key + " " + kv.Value).ToArray());
                Log.Info("Stal valyrianska 563: sztaby " + (s != null && s.ValyrianIngots ? "TAK" : "NIE") + ", przetop od " + (s != null ? s.ValyrianSmeltSkill : 0)
                         + (s != null && !s.ValyrianNoSmelt ? " (Valyrian No Smelt NIE - bez progu)" : "") + ", kucie od " + (s != null ? s.ValyrianForgeSkill : 0)
                         + " (Legendary Smith na 300: " + (s != null && s.ValyrianLegendarySmithAt300 ? "TAK" : "NIE") + ", " + perk + "), sztaba " + IngotId + " "
                         + (ing != null ? "wartosc " + ing.Value + ", kategoria " + (ing.ItemCategory != null ? ing.ItemCategory.StringId : "BRAK") : "BRAK PRZEDMIOTU")
                         + "; czesci VS w projektancie " + (_revealPieces != null ? _revealPieces.Count : 0) + " (wszystkich klng VS " + (_vsPieces != null ? _vsPieces.Count : 0)
                         + "); rejestr sztab " + _regIngots + ", start " + _startUnits + ", przetopione sztaby " + _smeltedUnits + ", wykute miecze " + _forgedSwords
                         + "; latki: przetop (ApplyAll), wynik przetopu (SmeltTab.OutputPostfix), " + hooks + ".");
            }
            catch (Exception e) { Stumble563("SessionStart563(linia)", e); }
            CheckUnlock();
        }

        /// <summary>Raz na kampanie: bohater gracza pierwszy raz spelnia perk + prog kucia - komunikat (zamiast ikony perku).</summary>
        internal static void CheckUnlock()
        {
            try
            {
                if (_unlockShown || !IngotsOn || Hero.MainHero == null || !CanForge(Hero.MainHero)) return;
                _unlockShown = true;
                Log.Player("You have mastered a lost art: you can forge Valyrian steel.");
                Log.Info("Stal valyrianska 563: " + Hero.MainHero.Name + " moze kuc stal valyrianska (Legendary Smith, Smithing " + Smithing(Hero.MainHero) + ") - komunikat odblokowania.");
            }
            catch (Exception e) { Stumble563("CheckUnlock", e); }
        }

        /// <summary>Linia doby 563 (tylko gdy cos bylo) i liczniki od zera; zalegle flagi projektanta (ekran kuzni bez OnFinalize) wracaja.</summary>
        internal static void Daily563()
        {
            try
            {
                if (_unhid.Count > 0 && !(TaleWorlds.Core.GameStateManager.Current != null && TaleWorlds.Core.GameStateManager.Current.ActiveState is CraftingState)) RestoreDesigner();
            }
            catch (Exception e) { Stumble563("Daily563(projektant)", e); }
            CheckUnlock();
            if (_dSmeltPieces + _dForged + _dGateRefusals + _dGuardIngots > 0 || _stumbles563 > 0)
                Log.Info("Stal valyrianska 563: dzien " + (int)CampaignTime.Now.ToDays + " - przetopione klingi " + _dSmeltPieces + " (sztab " + _dSmeltIngots + "), wykute miecze " + _dForged
                         + ", odmowy bramki kucia " + _dGateRefusals + ", sztaby z niczego -> Castle-forged " + _dGuardIngots + ", potkniecia " + _stumbles563 + ".");
            _dSmeltPieces = _dSmeltIngots = _dForged = _dGateRefusals = _dGuardIngots = 0;
        }
    }
}
