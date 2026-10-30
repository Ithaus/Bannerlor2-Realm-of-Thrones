using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// PACZKA 300b - TARG GRACZA (Jeff 29.10 ok. 21:05, gdy uslyszal, ze lup i sprzet sprzedaje za ok. 8% wartosci przez kare handlowa BK x5: "dajmy startowo np. 30% ale
    /// ceny zalezne sa od persuazion i trade im wiecej trade i persuwazion tym mam lepsze ceny, tak aby to mechnik aograc a nie na sztywno").
    ///
    /// Regula (klucz PlayerHaggle): dla STRONY GRACZA (gracz, partie jego rodu z przywodca-bohaterem, nie karawany; nie w zbrojowni QuartermasterEscrow) i tylko dla
    /// sprzetu (SupplyDemand.Equipmentish: bron, zbroje, amunicja, tarcze, konie, uprzaz) kara handlowa gry (DefaultTradeItemPriceFactorModel.GetTradePenalty 1.4.8:
    /// (0.06 + 1.5 + 0.25 x max(0, Tierf - 1)) [+0.5 wojna, +1 wies, +1 kupiec-karawana] x PartyTradeModel.GetTradePenaltyFactor = 1 / (1 + 0.002 x Handel), potem perki
    /// AddFactor) liczy sie tak, jakby perswazja przywodcy (Persuasion.Measure - ta sama miara co przesluchanie i kufel) byla dopisana do Handlu punkt za punkt:
    ///   h = 1 / (1 / tfT + E(P)), E = DefaultSkillEffects.TradePenaltyReduction (stawka gry 0.002 za punkt, czytana z obiektu), tfT = czynnik aktywnego modelu;
    ///   wynik = wynik lancucha x k / M, k = h / tfT, M = mnoznik BK (BkPenaltyOnce.LastMult, zmierzony na starcie sesji; dzis x5) tylko przy SPRZEDAZY broni,
    ///   zbroi i uprzezy (te same warunki co postfiks BK: HasWeaponComponent / HasArmorComponent / HasSaddleComponent).
    /// Wynik gry jest liniowy w num (AddFactor perkow, mnozenia BK, AddFactor blogoslawienstw BKROT), wiec to dokladnie wzor gry z perswazja w umiejetnosci i bez x5 BK;
    /// zostaja: zamek x3 i Gladiator BK, wojna, wies, perki, blogoslawienstwa ROT. AI i karawany - wczesne wyjscie, zero zmian.
    /// Marza kupca przy kupnie sprzetu w miescie (RetailFromWorth) dla strony gracza = RetailMarkupPercent x h (MarkupPct) - gra mnozy kare w obie strony tym samym
    /// czynnikiem umiejetnosci. Zacisk "kupno-1" i ksiega odkupu (RebuyLedger, klucz PlayerRebuyCap) - w SupplyDemand.PricePostfix.
    /// Klucz wylaczony = postfiks wychodzi od razu, marza i sufit jak dotad (137 + 299b bit w bit).
    /// </summary>
    internal static class PlayerHaggle
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        [ThreadStatic] private static int _depth;
        [ThreadStatic] internal static bool Raw;        // postfiks nic nie zmienia: wynik lancucha (log, samotest)
        [ThreadStatic] internal static bool Off;        // samotest: jak przy kluczu wylaczonym
        [ThreadStatic] private static int _forceS1;     // sonda przy zadanym S (S + 1; 0 = S przywodcy)
        private static readonly List<string> _models = new List<string>();
        private static readonly Dictionary<MobileParty, Terms> _cache = new Dictionary<MobileParty, Terms>();
        // 300b po recenzji: mnoznik BK na typ modelu, ktory robi zewnetrzne wywolanie (aktywny = BkPenaltyOnce.LastMult; inny typ - pomiar na nim, raz na sesje)
        private static readonly Dictionary<Type, float> _mbkType = new Dictionary<Type, float>();
        private static readonly object _lock = new object();
        private static int _errors, _dayErrors;

        internal sealed class Terms
        {
            internal int T, S, Hour;
            internal Persuasion.Parts P;
            internal float TfT, H, K;
            internal CultureObject Culture;
        }

        internal static bool On { get { if (Off) return false; var s = Settings.Current; return s != null && s.PlayerHaggle; } }
        internal static bool RebuyOn { get { if (Off) return false; var s = Settings.Current; return s != null && s.PlayerHaggle && s.PlayerRebuyCap; } }

        /// <summary>Sonda: S = Handel + perswazja przyjete na czas wyceny (-1 = prawdziwe S przywodcy).</summary>
        internal static int ForceS { get { return _forceS1 - 1; } set { _forceS1 = value < 0 ? 0 : value + 1; } }

        internal static void Stumble(string where, Exception e)
        {
            _dayErrors++;
            if (++_errors <= 5 || _errors % 100 == 0) Log.Error("PlayerHaggle." + where + " (potkniecie " + _errors + ")", e);
        }

        /// <summary>Strona gracza: gracz albo partia jego rodu z przywodca-bohaterem (ten sam targujacy co w grze - PartyRole.PartyLeader), nie karawana.</summary>
        internal static bool Side(MobileParty p)
        {
            if (p == null) return false;
            if (p.IsMainParty) return true;
            if (p.IsCaravan || p.LeaderHero == null) return false;
            var c = p.ActualClan;
            return c != null && c == Clan.PlayerClan;
        }

        /// <summary>Rzeczy, ktore postfiks BK mnozy x5 (BKEconomyLayerInstaller.GetTradePenaltyPostfix).</summary>
        internal static bool BkScope(ItemObject it)
        {
            return it != null && (it.HasWeaponComponent || it.HasArmorComponent || it.HasSaddleComponent);
        }

        /// <summary>Mnoznik BK do zdjecia przy sprzedazy strony gracza (pomiar BkPenaltyOnce.Sync; brak pomiaru albo liczba poza 0.5..50 = 1).</summary>
        internal static float Mbk { get { float m = BkPenaltyOnce.LastMult; return m >= 0.5f && m <= 50f ? m : 1f; } }

        /// <summary>300b po recenzji: mnoznik BK do zdjecia dla typu modelu, ktory liczy zewnetrzne wywolanie. Aktywny model = Mbk (pomiar Sync); inny typ
        /// (np. wlasna instancja Default w cudzym modelu) - BkPenaltyOnce.Measure na tej instancji raz na sesje (przy BkTradePenaltyOnce = false aktywny BKROT
        /// mierzy x25, a goly Default tylko x5 - jeden globalny mnoznik dzielilby go przez 25). Pomiar poza 0.5..50 = Mbk.</summary>
        private static float MbkFor(TradeItemPriceFactorModel inst)
        {
            float g = Mbk;
            if (inst == null || !BkPenaltyOnce.Measured) return g;
            var cm = Campaign.Current != null ? Campaign.Current.Models : null;
            var ty = inst.GetType();
            if (cm != null && cm.TradeItemPriceFactorModel != null && cm.TradeItemPriceFactorModel.GetType() == ty) return g;
            lock (_lock) { float v; if (_mbkType.TryGetValue(ty, out v)) return v; }
            float m = g;
            var a = ArmourNear(1f);
            if (a != null)
            {
                // wewnetrzne wywolanie: licznik zagniezdzenia > 1 i brak partii - postfiks 300b nic nie zmienia, wynik = sam lancuch tego modelu
                float r = BkPenaltyOnce.Measure(inst, a);
                if (!float.IsNaN(r) && r >= 0.5f && r <= 50f) m = r;
            }
            lock (_lock) _mbkType[ty] = m;
            Log.Info("Targ gracza (300b): model " + ty.FullName + " (nie aktywny) - BK x" + m.ToString("0.0", Inv) + " zmierzone na nim (aktywny x" + g.ToString("0.0", Inv) + ").");
            return m;
        }

        private static float E(int points)
        {
            var se = DefaultSkillEffects.TradePenaltyReduction;
            return se != null ? se.GetSkillEffectValue(points) : 0.002f * points;
        }

        internal static CultureObject CultureOf(PartyBase merchant)
        {
            if (merchant == null) return null;
            try
            {
                if (merchant.Settlement != null) return merchant.Settlement.Culture;
                var mp = merchant.MobileParty;
                if (mp != null && mp.LeaderHero != null) return mp.LeaderHero.Culture;
                return merchant.MapFaction != null ? merchant.MapFaction.Culture : null;
            }
            catch { return null; }
        }

        private static Terms Compute(MobileParty party, CultureObject cul, int fs)
        {
            var hero = party.LeaderHero;
            var m = Campaign.Current != null ? Campaign.Current.Models : null;
            if (hero == null || m == null || m.PartyTradeModel == null) return null;
            var t = new Terms { Culture = cul };
            t.T = hero.GetSkillValue(DefaultSkills.Trade);
            t.P = Persuasion.Measure(hero, cul);
            float tf = m.PartyTradeModel.GetTradePenaltyFactor(party);
            if (float.IsNaN(tf) || float.IsInfinity(tf) || tf <= 0f) tf = 1f;
            t.TfT = tf;
            int pe = fs >= 0 ? fs - t.T : t.P.Total;   // sonda: perswazja = S - Handel (E liniowe, wiec 1 / tfT + E(S - T) = 1 + E(S) w wanilii)
            t.S = fs >= 0 ? fs : t.T + t.P.Total;
            float inv = 1f / tf + E(pe);
            if (float.IsNaN(inv) || inv < 0.05f) inv = 0.05f;
            t.H = 1f / inv;
            t.K = t.H / tf;
            return t;
        }

        /// <summary>Warunki targu przywodcy party wobec kupca (kultura osady kupca; pamiec na godzine gry). null = brak przywodcy - bez zmian.</summary>
        internal static Terms Get(MobileParty party, PartyBase merchant) { return GetFor(party, CultureOf(merchant), ForceS); }

        /// <summary>139b: warunki targu przy zadanej kulturze rozmowcy (karawany: kultura osady, gdy kupca nie ma) i S sondy (fs; -1 = prawdziwe S - z pamiecia).</summary>
        internal static Terms GetFor(MobileParty party, CultureObject cul, int fs)
        {
            if (party == null) return null;
            int hour = (int)CampaignTime.Now.ToHours;
            if (fs < 0)
                lock (_lock) { Terms c; if (_cache.TryGetValue(party, out c) && c.Hour == hour && c.Culture == cul) return c; }
            var x = Compute(party, cul, fs);
            if (x != null && fs < 0)
            {
                x.Hour = hour;
                lock (_lock) { if (_cache.Count > 64) _cache.Clear(); _cache[party] = x; }
            }
            return x;
        }

        public static void Prefix() { _depth++; }
        // finalizer biegnie zawsze (takze po wyjatku) - licznik nie zostanie zawyzony
        public static Exception Finalizer(Exception __exception) { if (_depth > 0) _depth--; return __exception; }

        /// <summary>Postfiks GetTradePenalty (Priority.Last - po BK); tylko zewnetrzne wywolanie (BKROT -> base Default liczy raz).</summary>
        public static void Postfix(TradeItemPriceFactorModel __instance, ItemObject __0, MobileParty __1, PartyBase __2, bool __3, ref float __result)
        {
            if (_depth > 1 || Raw || __1 == null || __0 == null) return;
            try
            {
                // 139b: karawana - kara wedlug umiejetnosci dowodcy (CaravanLeader139b; bez dowodcy wczesne wyjscie, jak dotad); strona gracza karawan nie obejmuje
                if (__1.IsCaravan) { CaravanLeader139b.Penalty(__instance, __0, __1, __2, __3, ref __result); return; }
                if (!On || !Side(__1) || QuartermasterEscrow.Active || !SupplyDemand.Equipmentish(__0)) return;
                var t = Get(__1, __2);
                if (t == null) return;
                float r = __result * t.K;
                if (__3 && BkScope(__0)) r /= MbkFor(__instance);
                if (!float.IsNaN(r) && !float.IsInfinity(r)) __result = r;
            }
            catch (Exception e) { Stumble("Postfix", e); }
        }

        /// <summary>Marza kupca przy kupnie sprzetu (RetailFromWorth) w %: strona gracza RetailMarkupPercent x h, reszta jak dotad.</summary>
        internal static float MarkupPct(MobileParty client, PartyBase merchant)
        {
            var s = Settings.Current;
            float b = s != null ? Math.Max(0f, s.RetailMarkupPercent) : 10f;
            try
            {
                if (!On || !Side(client) || QuartermasterEscrow.Active) return b;
                var t = Get(client, merchant);
                return t != null ? b * t.H : b;
            }
            catch (Exception e) { Stumble("MarkupPct", e); return b; }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var seen = new HashSet<MethodBase>();
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                    catch { continue; }
                    foreach (var t in types)
                    {
                        try
                        {
                            if (t == null || t.IsAbstract || !typeof(TradeItemPriceFactorModel).IsAssignableFrom(t)) continue;
                            var m = AccessTools.DeclaredMethod(t, "GetTradePenalty");
                            if (m == null || m.IsAbstract || !seen.Add(m)) continue;
                            h.Patch(m, prefix: new HarmonyMethod(typeof(PlayerHaggle), "Prefix") { priority = Priority.First },
                                       postfix: new HarmonyMethod(typeof(PlayerHaggle), "Postfix") { priority = Priority.Last },
                                       finalizer: new HarmonyMethod(typeof(PlayerHaggle), "Finalizer"));
                            _models.Add(t.FullName);
                        }
                        catch (Exception e) { if (++_errors <= 3) Log.Error("PlayerHaggle.ApplyAll(" + (t != null ? t.FullName : "?") + ")", e); }
                    }
                }
                Log.Info("Targ gracza (300b): GetTradePenalty wpiete w " + _models.Count + " modelach (" + Models() + ") - postfiks po BK (Priority.Last), tylko strona gracza i sprzet.");
            }
            catch (Exception e) { Log.Error("PlayerHaggle.ApplyAll", e); }
        }

        internal static int ModelCount { get { return _models.Count; } }   // 139b: linia startowa karawan

        private static string Models() { return _models.Count == 0 ? "-" : string.Join(", ", _models); }

        // ------------------------------------------------------------ odczyty do logu i sondy
        /// <summary>Kara aktywnego modelu (Raw = wynik lancucha bez 300b; fs >= 0 - sonda przy S).</summary>
        internal static float Pen(TradeItemPriceFactorModel model, ItemObject it, MobileParty client, PartyBase merchant, bool selling, float ins, float sup, float dem, bool raw, int fs)
        {
            bool r0 = Raw; int f0 = _forceS1;
            try { Raw = raw; ForceS = fs; return model.GetTradePenalty(it, client, merchant, selling, ins, sup, dem); }
            finally { Raw = r0; _forceS1 = f0; }
        }

        /// <summary>Ulamek gry przy sprzedazy (przed polka): GetBasePriceFactor / (1 + kara), te same argumenty co GetPrice.</summary>
        internal static float Fraction(TradeItemPriceFactorModel model, ItemObject it, MobileParty client, PartyBase merchant, float ins, float sup, float dem, bool raw, int fs)
        {
            float bpf = model.GetBasePriceFactor(it.GetItemCategory(), ins, sup, dem, true, it.Value);
            return bpf / (1f + Pen(model, it, client, merchant, true, ins, sup, dem, raw, fs));
        }

        /// <summary>Ulamek gry przy sprzedazy w osadzie st (dane kategorii rynku osady) - do handel.log i linii doby.</summary>
        internal static float FractionAt(Settlement st, ItemObject it, MobileParty client, bool raw, int fs)
        {
            var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.TradeItemPriceFactorModel : null;
            if (model == null || it == null) return float.NaN;
            float ins = 0f, sup = 0f, dem = 0f;
            if (st != null && st.Town != null && st.Town.MarketData != null)
            {
                var d = st.Town.MarketData.GetCategoryData(it.GetItemCategory());
                ins = d.InStoreValue; sup = d.Supply; dem = d.Demand;
            }
            return Fraction(model, it, client, st != null ? st.Party : null, ins, sup, dem, raw, fs);
        }

        /// <summary>Dopisek linii PodazPopyt gracza (raz na godzine na koszyk).</summary>
        internal static string Explain(TradeItemPriceFactorModel model, EquipmentElement el, MobileParty client, PartyBase merchant, bool selling, float ins, float sup, float dem, int askP)
        {
            try
            {
                var t = Get(client, merchant);
                if (t == null || el.Item == null) return ", targ gracza (300b): brak przywodcy - bez zmian";
                float raw = Pen(model, el.Item, client, merchant, selling, ins, sup, dem, true, -1);
                float now = Pen(model, el.Item, client, merchant, selling, ins, sup, dem, false, -1);
                bool lift = selling && BkScope(el.Item);
                var sb = new StringBuilder();
                sb.Append(", targ gracza (300b): Handel ").Append(t.T).Append(" + perswazja ").Append(t.P.Total).Append(" (").Append(Persuasion.LogText(t.P))
                  .Append(") -> czynnik kary ").Append(t.H.ToString("0.000", Inv)).Append(" (sam Handel ").Append(t.TfT.ToString("0.000", Inv)).Append("), kara gry ")
                  .Append(raw.ToString("0.00", Inv)).Append(" -> ").Append(now.ToString("0.00", Inv))
                  .Append(lift ? " (BK x" + Mbk.ToString("0.0", Inv) + " zdjete przy sprzedazy)" : selling ? " (bez kary BK)" : " (BK zostaje przy kupnie)");
                if (selling)
                {
                    float bpf = model.GetBasePriceFactor(el.Item.GetItemCategory(), ins, sup, dem, true, el.Item.Value);
                    sb.Append(", ulamek sprzedazy ").Append((bpf / (1f + now)).ToString("0.000", Inv)).Append(" przed polka");
                    if (askP > 0) sb.Append(", kupno-1 ").Append(askP);
                }
                sb.Append(", narzut kupca ").Append(MarkupPct(client, merchant).ToString("0.0", Inv)).Append('%');
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Explain", e); return ", targ gracza (300b): blad opisu"; }
        }

        /// <summary>Dopisek linii SPRZEDAZ GRACZA w handel.log.</summary>
        internal static string SaleNote(Settlement st, EquipmentElement el, int atRebuy, int atBuy, out float frac)
        {
            frac = float.NaN;
            try
            {
                var main = MobileParty.MainParty;
                var t = Get(main, st != null ? st.Party : null);
                if (t == null) return "; targ 300b: brak przywodcy";
                frac = FractionAt(st, el.Item, main, false, -1);
                return "; targ 300b: Handel " + t.T + ", perswazja " + t.P.Total + ", czynnik " + t.H.ToString("0.000", Inv) + ", ulamek gry "
                       + (float.IsNaN(frac) ? "-" : frac.ToString("0.000", Inv)) + (BkScope(el.Item) ? " (BK x" + Mbk.ToString("0.0", Inv) + " zdjete)" : " (bez kary BK)")
                       + ", na suficie odkupu " + atRebuy + ", przy cenie kupna " + atBuy;
            }
            catch (Exception e) { Stumble("SaleNote", e); return "; targ 300b: blad opisu"; }
        }

        // ------------------------------------------------------------ linia startowa i samotest
        private static readonly int[] CurveS = { 0, 36, 200, 400, 700 };
        private static readonly float[] CurveTier = { 1f, 4f, 6f };

        private static ItemObject ArmourNear(float tierf)
        {
            ItemObject best = null; float bd = float.MaxValue;
            try
            {
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || it.ItemType != ItemObject.ItemTypeEnum.BodyArmor || !it.HasArmorComponent || it.IsTradeGood || it.IsCraftedByPlayer || it.Value <= 0) continue;
                    float d = Math.Abs(Math.Max(1f, it.Tierf) - tierf);
                    if (d < bd) { bd = d; best = it; }
                }
            }
            catch { }
            return best;
        }

        private static IEnumerable<Settlement> ProbeTowns(int max)
        {
            var list = new List<Settlement>();
            var cur = MobileParty.MainParty != null ? MobileParty.MainParty.CurrentSettlement : null;
            if (cur != null && cur.Town != null && cur.ItemRoster != null && cur.IsTown) list.Add(cur);
            foreach (var st in Settlement.All)
            {
                if (list.Count >= max) break;
                if (st == null || !st.IsTown || st.Town == null || st.ItemRoster == null || list.Contains(st)) continue;
                list.Add(st);
            }
            return list;
        }

        private static List<EquipmentElement> ShelfSample(Settlement st, int max)
        {
            var l = new List<EquipmentElement>();
            var seen = new HashSet<EquipmentElement>();
            var r = st.ItemRoster;
            for (int i = 0; i < r.Count && l.Count < max; i++)
            {
                var e = r.GetElementCopyAtIndex(i);
                if (e.Amount <= 0 || e.EquipmentElement.Item == null || !SupplyDemand.Equipmentish(e.EquipmentElement.Item)) continue;
                if (seen.Add(e.EquipmentElement)) l.Add(e.EquipmentElement);
            }
            return l;
        }

        /// <summary>Naruszenie arbitrazu: sprzedaz > kupno albo sprzedaz = kupno ponad minimum gry (1 = 1 zl przy minimum ceny to nie arbitraz - kupno i sprzedaz po 1 zl, zysk 0).</summary>
        internal static bool Violates(int buy, int sell) { return sell > buy || (sell == buy && buy > 1); }

        /// <summary>Naruszenia "sprzedaz >= kupno" cenami gracza (ProbeBuySell) przy S (fs) na probce polek (bez 1 = 1 zl - Violates).</summary>
        private static void Violations(IEnumerable<Settlement> towns, int perTown, int fs, ref int bad, ref int n)
        {
            int f0 = _forceS1;
            try
            {
                ForceS = fs;
                foreach (var st in towns)
                    foreach (var el in ShelfSample(st, perTown))
                    {
                        int buy, sell;
                        if (!SupplyDemand.ProbeBuySell(st, el, false, out buy, out sell)) continue;
                        n++;
                        if (Violates(buy, sell)) bad++;
                    }
            }
            finally { _forceS1 = f0; }
        }

        /// <summary>Start sesji (po SellByCondition.OnSessionLaunched - po pomiarze BK): linia "Targ gracza (300b): klucz ..." z krzywa i samotestem. Tylko odczyt.</summary>
        internal static void StartLine()
        {
            try
            {
                var s = Settings.Current;
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.TradeItemPriceFactorModel : null;
                var main = MobileParty.MainParty;
                if (s == null || model == null || main == null) { Log.Info("Targ gracza (300b): linia startowa niemozliwa (brak modelu cen albo partii gracza)."); return; }
                lock (_lock) { _cache.Clear(); _mbkType.Clear(); }   // po BkPenaltyOnce.Sync - pomiar BK na typ od nowa
                var sb = new StringBuilder();
                sb.Append("Targ gracza (300b): klucz Player Haggle ").Append(s.PlayerHaggle ? "TAK" : "NIE").Append(", Player Rebuy Cap ").Append(s.PlayerRebuyCap ? "TAK" : "NIE")
                  .Append(" | GetTradePenalty wpiete w ").Append(_models.Count).Append(" modelach (").Append(Models()).Append(')')
                  .Append(" | BK x").Append(Mbk.ToString("0.0", Inv)).Append(BkPenaltyOnce.Measured ? " (zmierzone)" : " (bez pomiaru - nic do zdjecia)");
                var t = Get(main, null);
                int sNow = 0;
                if (t != null)
                {
                    sNow = t.S;
                    sb.Append(" | gracz: Handel ").Append(t.T).Append(", perswazja ").Append(t.P.Total).Append(" (").Append(Persuasion.LogText(t.P)).Append(") -> czynnik kary ")
                      .Append(t.H.ToString("0.000", Inv)).Append(" (sam Handel ").Append(t.TfT.ToString("0.000", Inv)).Append("), narzut ")
                      .Append(MarkupPct(main, null).ToString("0.0", Inv)).Append('%');
                }
                else sb.Append(" | gracz: brak przywodcy");
                // krzywa: ulamek przed polka przy bpf 0.8 (czynnik gry sprzedazy), zbroja najblizsza Tierf 1 / 4 / 6, partia gracza, bez kupca
                var items = new ItemObject[CurveTier.Length];
                for (int i = 0; i < items.Length; i++) items[i] = ArmourNear(CurveTier[i]);
                sb.Append(" | krzywa (ulamek przed polka, bpf 0.8) Tierf ");
                for (int i = 0; i < items.Length; i++) { if (i > 0) sb.Append('/'); sb.Append(items[i] != null ? Math.Max(1f, items[i].Tierf).ToString("0.00", Inv) : "-"); }
                sb.Append(" (");
                for (int i = 0; i < items.Length; i++) { if (i > 0) sb.Append('/'); sb.Append(items[i] != null ? items[i].StringId : "-"); }
                sb.Append(')');
                // 300b po recenzji: perki i modyfikatory partii gracza w krzywej (Appraiser, Gladiator BK, Aserai, blogoslawienstwa) - kara przy S = 0 / wzor gry bez perkow
                // (0.06 + 1.5 + 0.25 x max(0, Tierf - 1)); sprawdz_logi L300-4 liczy z tego krzywa oczekiwana (wynik gry liniowy w num, wiec jeden mnoznik na krzywa)
                sb.Append(", perki x");
                if (items[0] != null)
                {
                    float b0 = 1.56f + 0.25f * Math.Max(0f, items[0].Tierf - 1f);
                    float p0 = Pen(model, items[0], main, null, true, 0f, 0f, 0f, false, 0);
                    float pf = b0 > 0f ? p0 / b0 : float.NaN;
                    sb.Append(float.IsNaN(pf) || float.IsInfinity(pf) ? "-" : pf.ToString("0.000", Inv));
                }
                else sb.Append('-');
                foreach (int S in CurveS)
                {
                    sb.Append(", S=").Append(S).Append(": ");
                    for (int i = 0; i < items.Length; i++)
                    {
                        if (i > 0) sb.Append('/');
                        if (items[i] == null) { sb.Append('-'); continue; }
                        float tp = Pen(model, items[i], main, null, true, 0f, 0f, 0f, false, S);
                        sb.Append((0.8f / (1f + tp)).ToString("0.000", Inv));
                    }
                }
                // samotest (a): AI bez zmian - kara z postfiksem 300b = wynik lancucha
                int aOk = 0, aN = 0, bOk = 0, bN = 0;
                Settlement town = null;
                foreach (var st in Settlement.All) if (st != null && st.IsTown && st.Town != null) { town = st; break; }
                var probe = new List<ItemObject>();
                foreach (var it in items) if (it != null) probe.Add(it);
                try
                {
                    foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                    {
                        if (it == null || it.IsTradeGood || it.IsCraftedByPlayer) continue;
                        if ((it.ItemType == ItemObject.ItemTypeEnum.Crossbow || it.ItemType == ItemObject.ItemTypeEnum.Horse || it.ItemType == ItemObject.ItemTypeEnum.HorseHarness)
                            && !probe.Exists(x => x.ItemType == it.ItemType)) probe.Add(it);
                    }
                }
                catch { }
                var parties = new List<MobileParty>();
                foreach (var mp in MobileParty.AllLordParties)
                {
                    if (parties.Count >= 20) break;
                    if (mp != null && !mp.IsMainParty && mp.LeaderHero != null && mp.ActualClan != Clan.PlayerClan) parties.Add(mp);
                }
                var merchants = new PartyBase[] { town != null ? town.Party : null, null };
                foreach (var mp in parties)
                    foreach (var it in probe)
                        foreach (var mer in merchants)
                            for (int sell = 0; sell < 2; sell++)
                            {
                                aN++;
                                float x = Pen(model, it, mp, mer, sell == 1, 0f, 0f, 0f, false, -1), y = Pen(model, it, mp, mer, sell == 1, 0f, 0f, 0f, true, -1);
                                if (x == y) aOk++;
                            }
                // samotest (b): bez klucza = 137 - partia gracza przy Off: kara = wynik lancucha, marza = RetailMarkupPercent
                bool off0 = Off;
                try
                {
                    Off = true;
                    float baseMk = Math.Max(0f, s.RetailMarkupPercent);
                    foreach (var it in probe)
                        foreach (var mer in merchants)
                            for (int sell = 0; sell < 2; sell++)
                            {
                                bN++;
                                float x = Pen(model, it, main, mer, sell == 1, 0f, 0f, 0f, false, -1), y = Pen(model, it, main, mer, sell == 1, 0f, 0f, 0f, true, -1);
                                if (x == y && MarkupPct(main, mer) == baseMk) bOk++;
                            }
                }
                finally { Off = off0; }
                // samotest (c): sprzedaz < kupno cenami gracza przy S teraz i S = 700
                int vb = 0, vn = 0;
                var towns = ProbeTowns(3);
                Violations(towns, 60, sNow, ref vb, ref vn);
                Violations(towns, 60, 700, ref vb, ref vn);
                sb.Append(" | samotest: AI bez zmian ").Append(aOk).Append('/').Append(aN).Append(", bez klucza = 137 ").Append(bOk).Append('/').Append(bN)
                  .Append(", sprzedaz < kupno przy S teraz i S=700: naruszen ").Append(vb).Append(" z ").Append(vn)
                  .Append(" | ksiega odkupu: wczytano ").Append(RebuyLedger.Loaded).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("StartLine", e); }
        }

        // ------------------------------------------------------------ linia doby pomiaru
        private static float Med(List<float> l) { return l.Count == 0 ? float.NaN : PriceWalk299.Q(l, 0.5); }
        private static string F3(float v) { return float.IsNaN(v) ? "-" : v.ToString("0.000", Inv); }
        private static string P1(float v) { return float.IsNaN(v) ? "-" : (100f * v).ToString("0.0", Inv); }

        private static ItemModifier Wreck(ItemObject it)
        {
            try
            {
                if (it == null || it.ItemComponent == null || it.ItemComponent.ItemModifierGroup == null) return null;
                ItemModifier w = null;
                foreach (var m in it.ItemComponent.ItemModifierGroup.ItemModifiers)
                    if (m != null && m.PriceMultiplier > 0f && m.PriceMultiplier < 1f && (w == null || m.PriceMultiplier < w.PriceMultiplier)) w = m;
                return w;
            }
            catch { return null; }
        }

        /// <summary>Doba (po SellByCondition.Daily): w dobie pomiaru (LogGate.Lines) linia "Targ gracza (300b): dzien N"; zawsze zeruje liczniki doby. Tylko odczyt.</summary>
        internal static void Daily()
        {
            int day = (int)CampaignTime.Now.ToDays - 1;
            try
            {
                RebuyLedger.Expire();   // 300b po recenzji: wpisy ksiegi starsze niz pora roku wypadaja (falszywe sufity nie zostaja na zawsze)
                var s = Settings.Current;
                var main = MobileParty.MainParty;
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.TradeItemPriceFactorModel : null;
                if (s == null || main == null || model == null) return;
                if (!LogGate.Lines)
                {
                    if (_dayErrors > 0) Log.Info("Targ gracza (300b): dzien " + day + " | potkniecia " + _dayErrors + ".");
                    return;
                }
                var t = Get(main, null);
                int sNow = t != null ? t.S : 0;
                var gNow = new List<float>(); var g700 = new List<float>(); var wNow = new List<float>(); var w700 = new List<float>();
                var lNow = new List<float>(); var l700 = new List<float>();
                int vbN = 0, vnN = 0, vb7 = 0, vn7 = 0, towns = 0, samples = 0;
                float share = Math.Max(0f, s.MendMaterialMaxShare);
                int f0 = _forceS1;
                try
                {
                    foreach (var st in Settlement.All)
                    {
                        if (st == null || !st.IsTown || st.Town == null || st.ItemRoster == null) continue;
                        var els = ShelfSample(st, 15);
                        if (els.Count == 0) continue;
                        towns++;
                        foreach (var el in els)
                        {
                            samples++;
                            for (int k = 0; k < 2; k++)
                            {
                                int fs = k == 0 ? sNow : 700;
                                ForceS = fs;
                                float g = FractionAt(st, el.Item, main, false, fs);
                                int buy, sell;
                                if (!SupplyDemand.ProbeBuySell(st, el, false, out buy, out sell)) continue;
                                if (k == 0) { if (!float.IsNaN(g)) gNow.Add(g); vnN++; if (Violates(buy, sell)) vbN++; } else { if (!float.IsNaN(g)) g700.Add(g); vn7++; if (Violates(buy, sell)) vb7++; }
                                if (el.ItemValue > 0) (k == 0 ? wNow : w700).Add((float)sell / el.ItemValue);
                                // petla wrak + material -> czysta w tym samym miescie bez ksiegi odkupu (gorna granica: towar przywieziony z innego miasta)
                                if (el.Item.ItemType == ItemObject.ItemTypeEnum.Horse) continue;
                                var wm = Wreck(el.Item);
                                if (wm == null) continue;
                                int wb = SupplyDemand.ProbeEquipAt(st, new EquipmentElement(el.Item, wm), 0, false, false);
                                int cs = SupplyDemand.ProbeEquipAt(st, new EquipmentElement(el.Item), 0, true, false);
                                float mat = share * (1f - wm.PriceMultiplier) * ArmsPricing.BaseOf(el.Item);
                                if (wb > 0 && cs > 0) (k == 0 ? lNow : l700).Add(cs / (wb + mat) - 1f);
                            }
                        }
                    }
                }
                finally { _forceS1 = f0; }
                int clan = 0, clanHero = 0;
                foreach (var mp in MobileParty.AllLordParties)
                    if (mp != null && !mp.IsMainParty && mp.ActualClan == Clan.PlayerClan && !mp.IsCaravan) { clan++; if (mp.LeaderHero != null) clanHero++; }
                var sb = new StringBuilder();
                sb.Append("Targ gracza (300b): dzien ").Append(day).Append(" | klucz ").Append(s.PlayerHaggle ? "TAK" : "NIE").Append(", ksiega ").Append(s.PlayerRebuyCap ? "TAK" : "NIE")
                  .Append(" | gracz Handel ").Append(t != null ? t.T : 0).Append(", perswazja ").Append(t != null ? t.P.Total : 0).Append(", S ").Append(sNow)
                  .Append(", czynnik ").Append(t != null ? F3(t.H) : "-")
                  .Append(" | probki ").Append(samples).Append(" w ").Append(towns).Append(" miastach")
                  .Append(" | ulamek gry przed polka mediana S teraz ").Append(F3(Med(gNow))).Append(", S=700 ").Append(F3(Med(g700)))
                  .Append(" | sprzedaz/wartosc ze stanem mediana S teraz ").Append(F3(Med(wNow))).Append(", S=700 ").Append(F3(Med(w700)))
                  .Append(" | naruszen sprzedaz >= kupno: S teraz ").Append(vbN).Append(" z ").Append(vnN).Append(", S=700 ").Append(vb7).Append(" z ").Append(vn7)
                  .Append(" | petla wrak + material -> czysta tu (bez ksiegi, INFO): S teraz mediana ").Append(P1(Med(lNow))).Append("% max ")
                  .Append(P1(lNow.Count > 0 ? PriceWalk299.Q(lNow, 1.0) : float.NaN)).Append("% (").Append(lNow.Count).Append("), S=700 mediana ").Append(P1(Med(l700)))
                  .Append("% max ").Append(P1(l700.Count > 0 ? PriceWalk299.Q(l700, 1.0) : float.NaN)).Append("% (").Append(l700.Count).Append(')')
                  .Append(" | ksiega odkupu: ").Append(RebuyLedger.Brief())
                  .Append(" | rod gracza: partii ").Append(clan).Append(", z przywodca ").Append(clanHero)
                  .Append(" | potkniecia ").Append(_dayErrors).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { _dayErrors = 0; RebuyLedger.DayReset(); }
        }
    }

    /// <summary>
    /// PACZKA 300b - KSIEGA ODKUPU (klucz PlayerRebuyCap, tylko przy PlayerHaggle): zastepuje dla strony gracza sufit SellByCondition.Cap (1/10 ceny nowej sztuki,
    /// decyzja Jeffa 07.10 - chronil przed petla "kup wrak, napraw, sprzedaj w tym samym miescie"). Ksiega zamyka wlasnie te petle i nic wiecej:
    ///  - zamkniecie ekranu handlu gracza w miescie albo zamku (SellByCondition.OnPlayerExchange) zapisuje kupione sztuki sprzetu w stanie < 1 (bez koni i towarow):
    ///    (osada, przedmiot) -> [mnoznik stanu, zaplacone za sztuke];
    ///  - wycena sprzedazy strony gracza sztuki tego samego przedmiotu w LEPSZYM stanie w tej osadzie: cena <= najnizsze zaplacone (wiaze: "odkup") - petla traci
    ///    co najmniej material i robocizne; dziala tez na strzaly czyszczone za darmo (CleanseAmmo);
    ///  - zatwierdzona sprzedaz zuzywa wpisy (najpierw ten sam stan - sztuka oddana jak kupiona, potem najtansze) przy zamknieciu ekranu i przy sprzedazy nadwyzki
    ///    zbrojowni gracza (MenPurse.SellPlayerSurplus). W trakcie ekranu kazda sztuka idzie na sufit, poki jest wpis (ostroznie: sprzedaz, cofniecie i ponowna
    ///    sprzedaz w jednym ekranie nie zdejmuje sufitu);
    ///  - sufit tylko dla partii gracza (to jego zakupy); partie rodu bez sufitu;
    ///  - wpis zyje jedna pore roku (CampaignTimeModel: DaysInWeek x WeeksInSeason, u Jeffa 91 dob) - sztuki kupione zuzyte dla ludzi i nigdy nieoddane nie trzymaja
    ///    falszywego sufitu na czysty lup tej rzeczy na zawsze; po porze roku petla w tym samym miescie jest tylko wolniejsza odmiana handlu miedzy miastami (decyzja 3).
    /// Zapis arm_lupy300 (SaveText.Sync), najwyzej 50 wpisow na (osada, przedmiot) i 5000 razem (najstarsze wypadaja). Reset w konstruktorze ArmouryBehavior.
    /// </summary>
    internal static class RebuyLedger
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int PerKey = 50, Total = 5000;
        private sealed class Entry { internal float Pm; internal int Paid; internal long Seq; internal int Day; }
        private static readonly Dictionary<string, List<Entry>> _book = new Dictionary<string, List<Entry>>();
        private static int _total, _loaded, _notedDay, _usedDay, _cappedDay, _expiredDay;
        private static long _seq;

        internal static int Loaded { get { return _loaded; } }

        internal static void Reset() { _book.Clear(); _total = _loaded = _notedDay = _usedDay = _cappedDay = _expiredDay = 0; _seq = 0; }
        internal static void DayReset() { _notedDay = _usedDay = _cappedDay = _expiredDay = 0; }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }

        /// <summary>Zycie wpisu: pora roku z ZYWEGO modelu czasu (jak LandValue.DaysInSeason; u Jeffa 7 x 13 = 91).</summary>
        private static int Life()
        {
            try { var m = Campaign.Current.Models.CampaignTimeModel; return Math.Max(7, m.DaysInWeek * m.WeeksInSeason); }
            catch { return 91; }
        }

        /// <summary>Raz na dobe (PlayerHaggle.Daily): wpisy starsze niz pora roku wypadaja.</summary>
        internal static void Expire()
        {
            try
            {
                if (_total == 0) return;
                int cut = Today() - Life();
                List<string> empty = null;
                foreach (var kv in _book)
                {
                    int n = kv.Value.RemoveAll(e => e.Day < cut);
                    if (n > 0) { _total -= n; _expiredDay += n; }
                    if (kv.Value.Count == 0) { if (empty == null) empty = new List<string>(); empty.Add(kv.Key); }
                }
                if (empty != null) foreach (var k in empty) _book.Remove(k);
            }
            catch (Exception e) { PlayerHaggle.Stumble("RebuyLedger.Expire", e); }
        }

        private static string Key(Settlement st, ItemObject it) { return st.StringId + "|" + it.StringId; }
        private static float Pm(EquipmentElement el) { return el.ItemModifier != null ? el.ItemModifier.PriceMultiplier : 1f; }

        private static bool Covered(Settlement st, ItemObject it)
        {
            return st != null && (st.IsTown || st.IsCastle) && it != null && SupplyDemand.Equipmentish(it) && !ArmouryBehavior.IsBeast(it) && !ArmouryBehavior.IsGoods(it)
                   && it.ItemType != ItemObject.ItemTypeEnum.Horse;
        }

        private static void Add(string key, float pm, int paid, int day)
        {
            List<Entry> l;
            if (!_book.TryGetValue(key, out l)) _book[key] = l = new List<Entry>();
            l.Add(new Entry { Pm = pm, Paid = paid, Seq = ++_seq, Day = day });
            _total++;
            if (l.Count > PerKey) { l.RemoveAt(0); _total--; }
            while (_total > Total)
            {
                // najstarszy wpis w calej ksiedze (rzadko - tylko ponad limit)
                string ok = null; long os = long.MaxValue;
                foreach (var kv in _book) if (kv.Value.Count > 0 && kv.Value[0].Seq < os) { os = kv.Value[0].Seq; ok = kv.Key; }
                if (ok == null) break;
                var ol = _book[ok]; ol.RemoveAt(0); _total--;
                if (ol.Count == 0) _book.Remove(ok);
            }
        }

        /// <summary>Kupno gracza w osadzie (zamkniecie ekranu): n sztuk po unit; tylko sprzet w stanie < 1.</summary>
        internal static void Note(Settlement st, EquipmentElement el, int n, int unit)
        {
            try
            {
                if (!PlayerHaggle.RebuyOn || n <= 0 || !Covered(st, el.Item)) return;
                float pm = Pm(el);
                if (pm >= 1f) return;
                string key = Key(st, el.Item);
                int today = Today();
                for (int i = 0; i < n; i++) Add(key, pm, Math.Max(1, unit), today);
                _notedDay += n;
            }
            catch (Exception e) { PlayerHaggle.Stumble("RebuyLedger.Note", e); }
        }

        /// <summary>Sufit odkupu sprzedazy sztuki el w osadzie st: najnizsze zaplacone za gorsza sztuke tego przedmiotu (0 = bez sufitu).</summary>
        internal static int Cap(Settlement st, EquipmentElement el)
        {
            try
            {
                if (!PlayerHaggle.RebuyOn || _total == 0 || !Covered(st, el.Item)) return 0;
                List<Entry> l;
                if (!_book.TryGetValue(Key(st, el.Item), out l)) return 0;
                float pm = Pm(el) - 1e-4f;
                int best = 0;
                foreach (var e in l) if (e.Pm < pm && (best == 0 || e.Paid < best)) best = e.Paid;
                return best;
            }
            catch (Exception e) { PlayerHaggle.Stumble("RebuyLedger.Cap", e); return 0; }
        }

        /// <summary>Zatwierdzona sprzedaz n sztuk el w osadzie st: zuzywa wpisy o stanie <= stan sprzedanej (ten sam stan najpierw, potem najtansze). Zwraca ile.</summary>
        internal static int Commit(Settlement st, EquipmentElement el, int n)
        {
            int used = 0;
            try
            {
                if (!PlayerHaggle.RebuyOn || n <= 0 || _total == 0 || !Covered(st, el.Item)) return 0;
                string key = Key(st, el.Item);
                List<Entry> l;
                if (!_book.TryGetValue(key, out l)) return 0;
                float pm = Pm(el);
                while (used < n && l.Count > 0)
                {
                    int bi = -1;
                    for (int i = 0; i < l.Count; i++)
                    {
                        var e = l[i];
                        if (e.Pm > pm + 1e-4f) continue;
                        if (bi < 0) { bi = i; continue; }
                        var b = l[bi];
                        bool eSame = Math.Abs(e.Pm - pm) <= 1e-4f, bSame = Math.Abs(b.Pm - pm) <= 1e-4f;
                        if ((eSame && !bSame) || (eSame == bSame && e.Paid < b.Paid)) bi = i;
                    }
                    if (bi < 0) break;
                    l.RemoveAt(bi); _total--; used++;
                }
                if (l.Count == 0) _book.Remove(key);
                _usedDay += used;
            }
            catch (Exception e) { PlayerHaggle.Stumble("RebuyLedger.Commit", e); }
            return used;
        }

        internal static void NoteCapped(int n) { if (n > 0) _cappedDay += n; }

        internal static string Brief()
        {
            var towns = new HashSet<string>();
            foreach (var k in _book.Keys) { int i = k.IndexOf('|'); towns.Add(i > 0 ? k.Substring(0, i) : k); }
            return "wpisow " + _total + ", miast " + towns.Count + ", zapisano dzis " + _notedDay + ", zuzyto dzis " + _usedDay + ", sufitow dzis " + _cappedDay + ", wygaslo dzis " + _expiredDay;
        }

        internal static string SkupBrief()
        {
            var towns = new HashSet<string>();
            foreach (var k in _book.Keys) { int i = k.IndexOf('|'); towns.Add(i > 0 ? k.Substring(0, i) : k); }
            return "ksiega odkupu: wpisow " + _total + ", miast " + towns.Count + ", zuzytych dzis " + _usedDay;
        }

        /// <summary>Zapis: "osada|przedmiot=pm:zaplacone:dzien,pm:zaplacone:dzien;..." (kolejnosc wpisow = kolejnosc kupna). Kontrola: odczyt napisu daje te sama liczbe wpisow.</summary>
        internal static string Export()
        {
            var sb = new StringBuilder();
            int n = 0;
            foreach (var kv in _book)
            {
                if (kv.Value.Count == 0) continue;
                sb.Append(kv.Key).Append('=');
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(kv.Value[i].Pm.ToString("0.####", Inv)).Append(':').Append(kv.Value[i].Paid).Append(':').Append(kv.Value[i].Day);
                    n++;
                }
                sb.Append(';');
            }
            string s = sb.ToString();
            int back = Parse(s, null);
            Log.Info("Ksiega odkupu (300b): zapis " + n + " wpisow (kluczy " + _book.Count + "), dlugosc " + s.Length + ", odczyt kontrolny " + back + ".");
            return s;
        }

        private static int Parse(string s, Dictionary<string, List<Entry>> into)
        {
            int n = 0;
            if (string.IsNullOrEmpty(s)) return 0;
            int today = into != null ? Today() : 0;
            foreach (var part in s.Split(';'))
            {
                if (part.Length == 0) continue;
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string key = part.Substring(0, eq);
                foreach (var e in part.Substring(eq + 1).Split(','))
                {
                    var f = e.Split(':');
                    float pm; int paid, day;
                    if (f.Length < 2 || f.Length > 3 || !float.TryParse(f[0], NumberStyles.Float, Inv, out pm) || !int.TryParse(f[1], NumberStyles.Integer, Inv, out paid)) continue;
                    if (f.Length < 3 || !int.TryParse(f[2], NumberStyles.Integer, Inv, out day)) day = today;   // wpis bez dnia - od dnia wczytania
                    n++;
                    if (into != null)
                    {
                        List<Entry> l;
                        if (!into.TryGetValue(key, out l)) into[key] = l = new List<Entry>();
                        l.Add(new Entry { Pm = pm, Paid = paid, Day = day });
                    }
                }
            }
            return n;
        }

        internal static void Import(string s)
        {
            try
            {
                Reset();
                var tmp = new Dictionary<string, List<Entry>>();
                Parse(s, tmp);
                foreach (var kv in tmp) foreach (var e in kv.Value) Add(kv.Key, e.Pm, e.Paid, e.Day);
                _loaded = _total;
            }
            catch (Exception e) { PlayerHaggle.Stumble("RebuyLedger.Import", e); }
        }
    }
}
