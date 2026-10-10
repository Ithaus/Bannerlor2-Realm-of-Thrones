using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// MNIEJSZE SAKWY AI (Jeff 28.08: "tyle ile do napraw, zadnych setek").
    /// Banner Kings kaze kazdej partii AI trzymac 10 DNI zapasu kazdego dobra
    /// per zolnierz - setki partii x 10 dni = wykupione targi (stad "nigdzie
    /// nie ma alkoholu ani skory"). Dwa ciecia popytu U ZRODLA:
    ///  - PostInitialize: AI trzyma BkSupplyDaysCap dni (domyslnie JEDEN) -
    ///    kupuja na biezaco, jak braknie, jada do miasta;
    ///  - czapka na kazdy Calculate*Need: zapas zadnego dobra nie przekroczy
    ///    BkSupplyMaxPieces sztuk, chocby armia miala 300 ludzi.
    /// Gracz kupuje recznie - jego nie ruszamy. Dosypki podazy (browar,
    /// garbarnia) COFNIETE na zadanie Jeffa 28.08 - po scieciu popytu
    /// vanillowa produkcja ma wystarczyc.
    /// </summary>
    internal static class BkSupplyTemper
    {
        private static int _tempered;

        public static void PostInitPostfix(object __instance)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || c.BkSupplyDaysCap <= 0) return;
                var tr = HarmonyLib.Traverse.Create(__instance);
                bool auto = false;
                try { auto = tr.Property("AutoBuying").GetValue<bool>(); } catch { }
                if (!auto) return;   // gracz zaopatruje sie sam
                int days = tr.Property("DaysOfProvision").GetValue<int>();
                int capNow = WinterBite.SupplyDaysCapNow();
                if (days <= capNow) return;
                tr.Property("DaysOfProvision").SetValue(capNow);
                if (++_tempered == 1)
                    Log.Info("BkSupplyTemper: zapasy AI sciete do " + capNow + " dni (BK chcial " + days + "; jesienia cap rosnie).");
            }
            catch { }
        }

        /// <summary>
        /// SUFIT NA SZTUKI (Jeff 28.08: "NIE na glowe - po co im SETKI tego?
        /// tyle, ile potrzebuja do napraw"). BK liczy potrzeby per zolnierz
        /// (300 ludzi = 300 stawek dziennie) - czapka na WYNIK kazdego modelu
        /// potrzeb: dzienna potrzeba partii AI nie przekroczy
        /// BkSupplyMaxPieces / BkSupplyDaysCap, wiec CALY zapas nigdy nie
        /// przekroczy BkSupplyMaxPieces sztuk danego dobra. Gracz nietkniety.
        /// </summary>
        public static void NeedCapPostfix(object __0, ref TaleWorlds.CampaignSystem.ExplainedNumber __result)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || c.BkSupplyMaxPieces <= 0) return;
                bool auto = false;
                try { auto = HarmonyLib.Traverse.Create(__0).Property("AutoBuying").GetValue<bool>(); } catch { }
                if (!auto) return;   // gracz kupuje recznie - bez czapki
                float perDay = (float)c.BkSupplyMaxPieces / Math.Max(1, c.BkSupplyDaysCap > 0 ? c.BkSupplyDaysCap : 4);
                __result.LimitMax(perDay);
            }
            catch { }
        }

        // ------------------------------------------------------------ 150: tekstylia zaopatrzenia BK = 0 (jedna regula odziezy wojska)
        // BK PartySupplies (dekompilacja BannerKings.dll): potrzeba tekstyliow (kategorie welna, plotno, len) rosnie co dobe o wynik
        // BKPartyNeedsModel.CalculateClothNeed (0.01 sztuki na zolnierza x PartySuppliesFactor), zapisana w ClothNeed (w zapisie gry,
        // do 0.3 x ludzi); BuyItems kupuje ja w osadzie z kiesy lorda (zloto w nicosc - osada nic nie dostaje), ConsumeItems zjada
        // z jukow partii, takze gracza. Przy odziezy wojska (ArmyClothing) wynik = 0 dla KAZDEJ partii, a zapisana potrzeba (stary
        // zapis) zerowana w tej samej chwili - inaczej BK kupilby i zjadl ja jeszcze raz obok naszej reguly.
        internal static bool ClothHooked, ClothResetReady;
        private static System.Reflection.MethodInfo _clothGet, _clothSet;

        public static void ClothZeroPostfix(object __0, ref TaleWorlds.CampaignSystem.ExplainedNumber __result)
        {
            try
            {
                if (!ArmyClothing.On) return;
                __result.LimitMin(0f);
                __result.LimitMax(0f);
                if (__0 == null || _clothGet == null || _clothSet == null) return;
                float v = (float)_clothGet.Invoke(__0, null);
                if (v == 0f) return;
                _clothSet.Invoke(__0, new object[] { 0f });
                ArmyClothing.BkClothZeroed++;
            }
            catch (Exception e) { ArmyClothing.Stumble("BkSupplyTemper.ClothZeroPostfix", e); }
        }

        // ------------------------------------------------------------ 172: strzaly zaopatrzenia BK = 0 dla partii AI (jedna droga amunicji)
        // BK PartySupplies: ArrowsNeed rosnie co dobe o BKPartyNeedsModel.CalculateArrowsNeed (0.003 x PartySuppliesFactor snopa na lucznika),
        // BuyItems kupuje kategorie "arrows" z polki osady za zloto lorda (zloto w nicosc), ConsumeItems(ArrowsNeed x 2) niszczy z jukow.
        // Amunicje partii AI liczy Armoury: zakupy AiGear wedlug wzorcow, zuzycie w bitwie i odzysk. Przy czynnych strzelarzach (TownFletchers)
        // i zakupach AI wynik = 0 i zapisana potrzeba zerowana (jak tekstylia 150); partia gracza - bez zmian (BK jak dotad).
        internal static bool ArrowsHooked, ArrowsResetReady;
        private static System.Reflection.MethodInfo _arrowsGet, _arrowsSet, _partyGet;

        public static void ArrowsZeroPostfix(object __0, ref TaleWorlds.CampaignSystem.ExplainedNumber __result)
        {
            try
            {
                if (__0 == null) return;
                var party = _partyGet != null ? _partyGet.Invoke(__0, null) as TaleWorlds.CampaignSystem.Party.MobileParty : null;
                if (party == null || !ArrowsClosedFor(party)) return;
                __result.LimitMin(0f);
                __result.LimitMax(0f);
                bool reset = false;
                if (ArrowsResetReady)
                {
                    float v = (float)_arrowsGet.Invoke(__0, null);
                    if (v != 0f) { _arrowsSet.Invoke(__0, new object[] { 0f }); reset = true; }
                }
                TownFletchers.NoteBk(reset);
            }
            catch (Exception e) { TownFletchers.Stumble("BkSupplyTemper.ArrowsZeroPostfix", e); }
        }

        /// <summary>172 + 174.0: strzaly BK zamkniete dla tej partii - AI przy czynnych strzelarzach i zakupach AI; gracz tylko przy BkSuppliesNoArmsPlayer (Q5).</summary>
        private static bool ArrowsClosedFor(TaleWorlds.CampaignSystem.Party.MobileParty party)
        {
            if (party == TaleWorlds.CampaignSystem.Party.MobileParty.MainParty) { var s = Settings.Current; return s != null && s.BkSuppliesNoArmsPlayer; }
            return TownFletchers.BkArrowsClosed;
        }

        // ------------------------------------------------------------ 174.0: bron i tarcze zaopatrzenia BK = 0 (jedna regula zuzycia broni armii AI)
        // BK PartySupplies: WeaponsNeed i ShieldsNeed rosna co dobe o CalculateWeaponsNeed / CalculateShieldsNeed (0.006 / 0.003 na zolnierza),
        // BuyItems kupuje kategorie MeleeWeapons2/3 i Shield2/3 z polki osady za zloto lorda (zloto w nicosc), ConsumeItems(potrzeba x 2) niszczy z jukow.
        // Zuzycie broni armii AI liczy Armoury (AiWear, naprawy kowali, wraki) - przy BkSuppliesNoArms i zakupach AI wynik = 0, zapisana potrzeba zerowana
        // (wzor strzal 172). Partia gracza: osobny wylacznik BkSuppliesNoArmsPlayer (pytanie 5 - domyslnie wylaczony, czeka na Jeffa).
        internal static bool ArmsHooked, ArmsResetReady, NeedsBuyHooked;
        private static System.Reflection.MethodInfo _weapGet, _weapSet, _shieldGet, _shieldSet;

        private static bool ArmsClosedFor(TaleWorlds.CampaignSystem.Party.MobileParty party)
        {
            var s = Settings.Current;
            if (s == null || party == null) return false;
            if (party == TaleWorlds.CampaignSystem.Party.MobileParty.MainParty) return s.BkSuppliesNoArmsPlayer;
            return s.BkSuppliesNoArms && AiGear.On;
        }

        public static void WeaponsZeroPostfix(object __0, ref TaleWorlds.CampaignSystem.ExplainedNumber __result) { ArmsZero(__0, ref __result, _weapGet, _weapSet); }
        public static void ShieldsZeroPostfix(object __0, ref TaleWorlds.CampaignSystem.ExplainedNumber __result) { ArmsZero(__0, ref __result, _shieldGet, _shieldSet); }

        private static void ArmsZero(object sup, ref TaleWorlds.CampaignSystem.ExplainedNumber result, System.Reflection.MethodInfo get, System.Reflection.MethodInfo set)
        {
            try
            {
                if (sup == null || _partyGet == null) return;
                var party = _partyGet.Invoke(sup, null) as TaleWorlds.CampaignSystem.Party.MobileParty;
                if (!ArmsClosedFor(party)) return;
                result.LimitMin(0f);
                result.LimitMax(0f);
                ArmsLeaks.BkZeroCalls++;
                if (ArmsResetReady && get != null && set != null)
                {
                    float v = (float)get.Invoke(sup, null);
                    if (v != 0f) { set.Invoke(sup, new object[] { 0f }); ArmsLeaks.BkReset++; }
                }
            }
            catch (Exception e) { ArmsLeaks.Stumble("BkSupplyTemper.ArmsZero", e); }   // recenzja 174: raz w logu, reszta w liczniku linii "Uzbrojenie (ujscia 174)"
        }

        /// <summary>Recenzja 172: prefiks PartySupplies.BuyItems() (Tick i wejscie do osady). Partie z ludzmi ponizej MinimumSoldiersThreshold
        /// nie wolaja CalculateArrowsNeed, a po wczytaniu BuyItems przy wejsciu do osady moze isc przed pierwszym Tick - stara ArrowsNeed
        /// (zapis sprzed 172) kupilaby strzaly za zloto lorda w nicosc. Zerowana tu, przed zakupem, dla partii AI przy czynnych strzelarzach.</summary>
        // 174.0: ten sam prefiks zeruje tez zapisane WeaponsNeed i ShieldsNeed (NeedsBuyPrefix) - partie ponizej progu ludzi nie przeliczaja potrzeb.
        public static void NeedsBuyPrefix(object __instance)
        {
            if (__instance == null || _partyGet == null) return;
            TaleWorlds.CampaignSystem.Party.MobileParty party = null;
            try { party = _partyGet.Invoke(__instance, null) as TaleWorlds.CampaignSystem.Party.MobileParty; } catch { }
            if (party == null) return;
            try
            {
                if (ArrowsResetReady && ArrowsClosedFor(party))
                {
                    float v = (float)_arrowsGet.Invoke(__instance, null);
                    if (v != 0f) { _arrowsSet.Invoke(__instance, new object[] { 0f }); TownFletchers.NoteBkBuyReset(); }
                }
            }
            catch (Exception e) { TownFletchers.Stumble("BkSupplyTemper.NeedsBuyPrefix(strzaly)", e); }
            try
            {
                if (ArmsResetReady && ArmsClosedFor(party))
                {
                    float w = (float)_weapGet.Invoke(__instance, null);
                    if (w != 0f) { _weapSet.Invoke(__instance, new object[] { 0f }); ArmsLeaks.BkReset++; }
                    float sh = (float)_shieldGet.Invoke(__instance, null);
                    if (sh != 0f) { _shieldSet.Invoke(__instance, new object[] { 0f }); ArmsLeaks.BkReset++; }
                }
            }
            catch (Exception e) { ArmsLeaks.Stumble("BkSupplyTemper.NeedsBuyPrefix(bron)", e); }
        }

        // ------------------------------------------------------------ W1 (noc 10/11.10): kara morale zaopatrzenia BK = dni braku
        // BK (BannerKings.Patches.VanillaModelTweakPatches.BKPartyMoraleTweakPatches - postfiks na DefaultPartyMoraleModel.GetEffectivePartyMorale,
        // brama LeaderHero i TotalManCount > MinimumSoldiersThreshold): dla alkoholu, produktow zwierzecych, tekstyliow i drewna
        // kara = -min(Need / max(Get*CurrentNeed, 1), Need). Get*CurrentNeed to model potrzeb BK: w miescie/zamku 0 (czynnik "In a town or castle" -1),
        // u AI z nasza czapka NeedCapPostfix (3/d), u malej partii < 1. Dzielnik 1 daje wtedy CALA narosla potrzebe (160 ludzi w twierdzy: -40 alkohol
        // -48 drewno), a czapka podnosi kare duzych partii ponad zamierzone max BK (L15 183d: "Alcohol supplies -35.3; Wood supplies -33.6").
        // Tutaj Get*CurrentNeed zwraca inny wynik TYLKO wewnatrz liczenia morale (SpeedDepth.InMorale - tam czyta je wylacznie kara BK; dialog
        // kwatermistrza BKPartyNeedsBehavior i podpowiedzi BK UIHelper biegna poza morale i widza model jak dotad): prawdziwa dzienna potrzebe partii
        // z jej ludzi (ludzie x stawka BK x PartySuppliesFactor - bez czynnika osady, bez kwatermistrza i bez naszej czapki), a gdy zapisana potrzeba
        // przekracza klamre BK dla dzisiejszych ludzi (k = 0.25 alkohol, 0.25 zwierzece, 0.3 tekstylia, 0.3 drewno) - dzielnik powiekszony tak, ze BK
        // liczy od potrzeby przycietej do klamry. Kara = min(Need, k x ludzi) / max(dzienna, 1): dni braku, najwyzej k / (stawka x factor) =
        // 20 / 20 / 60 / 30 przy factor 0.5 (tekstylia przy odziezy wojska 150 i tak 0); mala partia (dzienna < 1) - jak w BK, potrzeba / 1, czyli
        // najwyzej k x ludzi. Nigdy wiecej niz wzor BK (dzielnik >= dzielnik BK), z jednym wyjatkiem: kwatermistrz, ktory w BK PODNOSI dzienna potrzebe.
        // Nadwyzka (Need < 0): premia BK bez zmian (+|Need| - wzor BK nie zalezy wtedy od dzielnika). Przyrost potrzeby (Tick liczy model wprost) -
        // bez zmian; zakupy (Need x DaysOfProvision) i zuzycie (Need x 2) BK ida za przycieta potrzeba (klamra nizej przed BuyItems/ConsumeItems) -
        // inaczej niz w BK tylko u partii z zamknieta brama stosow (przy otwartej BK przycina sam przed zakupem), zawsze mniej, nigdy wiecej.
        // Gracz tak samo (jedna regula).
        // Klamra (TickClampPrefix, prefiks PartySupplies.Tick): zapisane Alcohol/AnimalProducts/Cloth/WoodNeed do +-k x dzisiejszych ludzi przy kazdym
        // ticku dobowym, przed przyrostem BK - BK przycina tylko przy przyroscie, za brama MemberRoster.Count (liczba STOSOW, nie ludzi) > progu, wiec
        // potrzeba partii, ktora sie skurczyla albo ma malo rodzajow jednostek, zamarzala ponad klamra.
        internal static bool W1Hooked, W1ClampHooked;
        private static readonly string[] W1Need = { "AlcoholNeed", "AnimalProductsNeed", "ClothNeed", "WoodNeed" };
        private static readonly string[] W1Get = { "GetAlcoholCurrentNeed", "GetAnimalProductsCurrentNeed", "GetTextileCurrentNeed", "GetWoodCurrentNeed" };
        private static readonly string[] W1Rate = { "AlcoholPerSoldier", "AnimalProductsPerSoldier", "ClothPerSoldier", "WoodPerSoldier" };
        private static readonly string[] W1Name = { "alkohol", "produkty zwierzece", "tekstylia", "drewno" };
        private static readonly float[] W1K = { 0.25f, 0.25f, 0.3f, 0.3f };                       // klamry BK (PartySupplies.Tick)
        private static readonly float[] W1RateDefault = { 0.025f, 0.01f, 0.01f, 0.02f };          // stawki BK (BKPartyNeedsModel)
        private static float[] _w1Rate = (float[])W1RateDefault.Clone();
        private static float _w1Factor = 0.5f;
        private static int _w1Day = int.MinValue;
        private static bool _w1FromBk;                                                             // stawki i factor odczytane z BK (false - domyslne BK)
        private static Type _w1TCfg, _w1TSet;                                                      // typy BK (szukane raz - FindType przechodzi wszystkie zestawy)
        private static Func<object, float>[] _w1NeedGet;
        private static System.Reflection.MethodInfo[] _w1NeedSet, _w1GetM;
        private static Func<object, TaleWorlds.CampaignSystem.Party.MobileParty> _w1Party;
        private static System.Reflection.PropertyInfo _w1Threshold;
        // liczniki doby (linia "Zaopatrzenie BK - kara morale (W1)")
        private static int _w1Swaps, _w1ClampN, _w1Stumbles;
        private static float _w1ClampMass;
        private static readonly System.Collections.Generic.HashSet<string> _w1Err = new System.Collections.Generic.HashSet<string>();

        private static void W1Stumble(string where, Exception e)
        {
            _w1Stumbles++;
            try { if (_w1Err.Add(where)) Log.Error("BkSupplyTemper." + where, e); } catch { }
        }

        private static Func<object, T> Getter<T>(Type t, string prop)
        {
            var pi = t != null ? t.GetProperty(prop, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) : null;
            if (pi == null || pi.PropertyType != typeof(T) || pi.GetGetMethod() == null) return null;
            var o = System.Linq.Expressions.Expression.Parameter(typeof(object), "o");
            var body = System.Linq.Expressions.Expression.Property(System.Linq.Expressions.Expression.Convert(o, t), pi);
            return System.Linq.Expressions.Expression.Lambda<Func<object, T>>(body, o).Compile();
        }

        /// <summary>W1: stawki BK (model potrzeb z BannerKingsConfig) i PartySuppliesFactor (MCM BK) - raz na dobe; blad - domyslne BK (0.5).</summary>
        private static void W1Refresh()
        {
            int day;
            try { day = (int)CampaignTime.Now.ToDays; } catch { day = 0; }
            if (day == _w1Day) return;
            _w1Day = day;
            bool ok = true;
            var rate = (float[])W1RateDefault.Clone();
            float factor = 0.5f;
            try
            {
                var tCfg = _w1TCfg ?? (_w1TCfg = QuartermasterLaw.FindType("BannerKings.BannerKingsConfig"));
                var cfg = tCfg != null ? HarmonyLib.AccessTools.Property(tCfg, "Instance")?.GetValue(null, null) : null;
                var model = cfg != null ? HarmonyLib.AccessTools.Property(tCfg, "PartyNeedsModel")?.GetValue(cfg, null) : null;
                if (model == null) ok = false;
                else
                    for (int c = 0; c < 4; c++)
                    {
                        var p = model.GetType().GetProperty(W1Rate[c], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (p != null && p.PropertyType == typeof(float)) { float v = (float)p.GetValue(model, null); if (v > 0f && v < 1f) rate[c] = v; else ok = false; }
                        else ok = false;
                    }
            }
            catch (Exception e) { ok = false; W1Stumble("W1Refresh(stawki)", e); }
            try
            {
                var tSet = _w1TSet ?? (_w1TSet = QuartermasterLaw.FindType("BannerKings.Settings.BannerKingsSettings"));
                object inst = null;
                for (var bt = tSet; bt != null && inst == null; bt = bt.BaseType)
                {
                    var ip = bt.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly);
                    if (ip != null) inst = ip.GetValue(null, null);
                }
                var fp = tSet != null ? tSet.GetProperty("PartySuppliesFactor", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) : null;
                if (inst != null && fp != null && fp.PropertyType == typeof(float)) { float f = (float)fp.GetValue(inst, null); if (f > 0f) factor = f; else ok = false; }
                else ok = false;
            }
            catch (Exception e) { ok = false; W1Stumble("W1Refresh(factor)", e); }
            _w1Rate = rate; _w1Factor = factor; _w1FromBk = ok;
        }

        /// <summary>W1: dzienna potrzeba partii z jej ludzi (bez osady, kwatermistrza i czapki), najmniej 1 - jak dzielnik BK.</summary>
        private static float W1Daily(int c, float men) { return Math.Max(1f, men * _w1Rate[c] * _w1Factor); }

        /// <summary>W1: kara (dodatnia liczba punktow) z naszego wzoru - Need przycieta do klamry dzisiejszych ludzi / max(dzienna, 1); nadwyzka 0.</summary>
        private static float W1Penalty(int c, float need, float men)
        {
            if (need <= 0f || men <= 0f) return 0f;
            return Math.Min(need, W1K[c] * men) / W1Daily(c, men);
        }

        public static void AlcoholNeedPostfix(object __instance, ref TaleWorlds.CampaignSystem.ExplainedNumber __result) { W1Divisor(__instance, 0, ref __result); }
        public static void AnimalNeedPostfix(object __instance, ref TaleWorlds.CampaignSystem.ExplainedNumber __result) { W1Divisor(__instance, 1, ref __result); }
        public static void TextileNeedPostfix(object __instance, ref TaleWorlds.CampaignSystem.ExplainedNumber __result) { W1Divisor(__instance, 2, ref __result); }
        public static void WoodNeedPostfix(object __instance, ref TaleWorlds.CampaignSystem.ExplainedNumber __result) { W1Divisor(__instance, 3, ref __result); }

        private static void W1Divisor(object sup, int c, ref TaleWorlds.CampaignSystem.ExplainedNumber result)
        {
            if (!SpeedDepth.InMorale) return;                  // poza liczeniem morale (dialog, podpowiedzi BK) - model BK jak dotad
            var s = Settings.Current;
            if (s == null || !s.BkSupplyMoraleByDays || sup == null || _w1Party == null || _w1NeedGet == null) return;
            try
            {
                var party = _w1Party(sup);
                if (party == null || party.MemberRoster == null) return;
                float men = party.MemberRoster.TotalManCount;
                if (men <= 0f) return;
                W1Refresh();
                float need = _w1NeedGet[c](sup);
                if (need <= 0f) return;                         // nadwyzka albo zero - wzor BK nie zalezy od dzielnika
                float den = W1Daily(c, men);
                float lim = W1K[c] * men;
                if (need > lim && lim > 0f) den = den * need / lim;   // BK: Need / den = min(Need, k x ludzi) / max(dzienna, 1)
                result = new TaleWorlds.CampaignSystem.ExplainedNumber(den, false, null);
                _w1Swaps++;
            }
            catch (Exception e) { W1Stumble("W1Divisor", e); }
        }

        /// <summary>W1: prefiks PartySupplies.Tick - zapisane potrzeby do +-k x dzisiejszych ludzi (kazda partia BK, takze gracz i brama stosow zamknieta).</summary>
        public static void TickClampPrefix(object __instance)
        {
            var s = Settings.Current;
            if (s == null || !s.BkSupplyMoraleByDays || __instance == null || _w1Party == null || _w1NeedGet == null || _w1NeedSet == null) return;
            try
            {
                var party = _w1Party(__instance);
                if (party == null || party.MemberRoster == null) return;
                float men = Math.Max(0, party.MemberRoster.TotalManCount);
                for (int c = 0; c < 4; c++)
                {
                    float v = _w1NeedGet[c](__instance);
                    float lim = W1K[c] * men;
                    float nv = v > lim ? lim : (v < -lim ? -lim : v);
                    if (nv == v) continue;
                    _w1NeedSet[c].Invoke(__instance, new object[] { nv });
                    _w1ClampN++; _w1ClampMass += Math.Abs(v - nv);
                }
            }
            catch (Exception e) { W1Stumble("TickClampPrefix", e); }
        }

        /// <summary>W1: linia doby (z WarLedger.Line183) - partie lordow AI z kara zaopatrzenia BK: ile, srednia i max na kategorie, nasz wzor
        /// i wzor BK bez poprawki (ten sam stan partii; Get*CurrentNeed poza liczeniem morale = model BK jak dotad). Tylko odczyt.</summary>
        internal static string MoraleDigest()
        {
            var s = Settings.Current;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            int day = (int)CampaignTime.Now.ToDays;
            var sb = new System.Text.StringBuilder(600);
            sb.Append("Zaopatrzenie BK - kara morale (W1): dzien ").Append(day).Append(" | dni braku zamiast wzoru BK: ")
              .Append(s != null && s.BkSupplyMoraleByDays ? (W1Hooked ? "TAK" : "BRAK latki") : "NIE (wylaczone)");
            try
            {
                W1Refresh();
                sb.Append(" (factor BK ").Append(_w1Factor.ToString("0.00", inv)).Append(", stawki na czlowieka ");
                for (int c = 0; c < 4; c++) sb.Append(c > 0 ? "/" : "").Append(_w1Rate[c].ToString("0.000", inv));
                sb.Append(_w1FromBk ? " z BK" : " - domyslne BK, odczyt z BK nieudany").Append(")");
                object beh = null; System.Reflection.MethodInfo getSup = null;
                var tBeh = QuartermasterLaw.FindType("BannerKings.Behaviours.PartyNeeds.BKPartyNeedsBehavior");
                if (tBeh != null && Campaign.Current != null)
                {
                    var gm = typeof(Campaign).GetMethod("GetCampaignBehavior", Type.EmptyTypes);
                    beh = gm != null ? gm.MakeGenericMethod(tBeh).Invoke(Campaign.Current, null) : null;
                    getSup = HarmonyLib.AccessTools.Method(tBeh, "GetPartySupplies", new[] { typeof(TaleWorlds.CampaignSystem.Party.MobileParty) });
                }
                if (beh == null || getSup == null || _w1NeedGet == null || _w1GetM == null || _w1Threshold == null) { sb.Append(" | BK zaopatrzenie nieczytelne."); return sb.ToString(); }
                int parties = 0, withPen = 0, inFort = 0;
                var n = new int[4]; var sum = new float[4]; var max = new float[4]; var bkSum = new float[4]; var bkMax = new float[4];
                foreach (var mp in TaleWorlds.CampaignSystem.Party.MobileParty.AllLordParties)
                {
                    try
                    {
                        if (mp == null || !mp.IsActive || mp.IsMainParty || mp.ActualClan == Clan.PlayerClan || Undead.Party(mp) || mp.LeaderHero == null) continue;
                        var sup = getSup.Invoke(beh, new object[] { mp });
                        if (sup == null) continue;
                        float men = mp.MemberRoster.TotalManCount;
                        if (men <= (int)_w1Threshold.GetValue(sup, null)) continue;     // brama kary BK
                        parties++;
                        bool any = false;
                        for (int c = 0; c < 4; c++)
                        {
                            var en = (TaleWorlds.CampaignSystem.ExplainedNumber)_w1GetM[c].Invoke(sup, new object[] { false });   // model BK (poza morale - bez naszej podmiany)
                            float need = _w1NeedGet[c](sup);                                                                         // po wywolaniu - jak BK (tekstylia: zerowanie 150)
                            if (need <= 0f) continue;
                            float bk = Math.Min(need / Math.Max(en.ResultNumber, 1f), need);
                            float ours = W1Penalty(c, need, men);
                            if (bk < 0.05f && ours < 0.05f) continue;
                            n[c]++; sum[c] += ours; bkSum[c] += bk;
                            if (ours > max[c]) max[c] = ours;
                            if (bk > bkMax[c]) bkMax[c] = bk;
                            any = true;
                        }
                        if (any) { withPen++; if (mp.CurrentSettlement != null && mp.CurrentSettlement.Town != null) inFort++; }
                    }
                    catch (Exception e) { W1Stumble("MoraleDigest(partia)", e); }
                }
                sb.Append(" | partie lordow AI z zaopatrzeniem BK powyzej progu ").Append(parties).Append(", z kara ").Append(withPen).Append(" (w miescie lub zamku ").Append(inFort).Append(")");
                for (int c = 0; c < 4; c++)
                {
                    sb.Append(" | ").Append(W1Name[c]).Append(": partii ").Append(n[c]);
                    if (n[c] > 0)
                        sb.Append(", kara srednio ").Append((sum[c] / n[c]).ToString("0.0", inv)).Append(" max ").Append(max[c].ToString("0.0", inv))
                          .Append(" (wzor BK bez poprawki: srednio ").Append((bkSum[c] / n[c]).ToString("0.0", inv)).Append(" max ").Append(bkMax[c].ToString("0.0", inv)).Append(")");
                }
            }
            catch (Exception e) { W1Stumble("MoraleDigest", e); sb.Append(" | blad odczytu"); }
            sb.Append(" | klamra potrzeb przy ticku BK od wczoraj: przycietych ").Append(_w1ClampN).Append(" (zdjete razem ").Append(_w1ClampMass.ToString("0.0", inv)).Append(")")
              .Append(", podmiany dzielnika w liczeniu morale ").Append(_w1Swaps).Append(", potkniecia ").Append(_w1Stumbles).Append('.');
            _w1Swaps = _w1ClampN = _w1Stumbles = 0; _w1ClampMass = 0f;
            return sb.ToString();
        }

        /// <summary>W1: wpiecie (z ApplyAll, BK obecne). Postfiks na Get*CurrentNeed (Priority.Last - po czapce i zerowaniu modelu nic nie zmienia,
        /// bo te dzialaja na modelu, nie na Get*) i prefiks na Tick.</summary>
        private static void ApplyW1(HarmonyLib.Harmony h, Type t)
        {
            try
            {
                _w1Party = Getter<TaleWorlds.CampaignSystem.Party.MobileParty>(t, "Party");
                var get = new Func<object, float>[4]; var set = new System.Reflection.MethodInfo[4]; var gm = new System.Reflection.MethodInfo[4];
                bool ok = _w1Party != null;
                for (int c = 0; c < 4 && ok; c++)
                {
                    get[c] = Getter<float>(t, W1Need[c]);
                    set[c] = HarmonyLib.AccessTools.PropertySetter(t, W1Need[c]);
                    gm[c] = HarmonyLib.AccessTools.Method(t, W1Get[c], new[] { typeof(bool) });
                    if (get[c] == null || set[c] == null || gm[c] == null || gm[c].ReturnType != typeof(TaleWorlds.CampaignSystem.ExplainedNumber)) ok = false;
                }
                _w1Threshold = HarmonyLib.AccessTools.Property(t, "MinimumSoldiersThreshold");
                if (!ok) { Log.Info("BkSupplyTemper: W1 (kara zaopatrzenia BK = dni braku) - BRAK skladowych PartySupplies, kara BK jak dotad."); return; }
                _w1NeedGet = get; _w1NeedSet = set; _w1GetM = gm;
                if (!SpeedDepth.MoraleOnDefault) { Log.Info("BkSupplyTemper: W1 - licznik liczenia morale (SpeedDepth) nie wpiety na DefaultPartyMoraleModel - kara BK jak dotad."); }
                else
                {
                    string[] post = { nameof(AlcoholNeedPostfix), nameof(AnimalNeedPostfix), nameof(TextileNeedPostfix), nameof(WoodNeedPostfix) };
                    for (int c = 0; c < 4; c++)
                        h.Patch(gm[c], postfix: new HarmonyLib.HarmonyMethod(typeof(BkSupplyTemper), post[c]) { priority = HarmonyLib.Priority.Last });
                    W1Hooked = true;
                }
                var tick = HarmonyLib.AccessTools.Method(t, "Tick", Type.EmptyTypes);
                if (tick != null) { h.Patch(tick, prefix: new HarmonyLib.HarmonyMethod(typeof(BkSupplyTemper), nameof(TickClampPrefix))); W1ClampHooked = true; }
            }
            catch (Exception e) { Log.Error("BkSupplyTemper.ApplyW1", e); }
            Log.Info("BkSupplyTemper: W1 kara morale zaopatrzenia BK = dni braku (MCM Bk Supply Morale By Days) - dzielnik w liczeniu morale "
                     + (W1Hooked ? "wpiety" : "BRAK") + ", klamra potrzeb przy ticku BK " + (W1ClampHooked ? "wpieta" : "BRAK") + ".");
        }

        internal static void ApplyAll(HarmonyLib.Harmony h)
        {
            try
            {
                var t = QuartermasterLaw.FindType("BannerKings.Behaviours.PartyNeeds.PartySupplies");
                var m = t != null ? HarmonyLib.AccessTools.Method(t, "PostInitialize") : null;
                if (m == null) { Log.Info("BkSupplyTemper: BK PartySupplies nieobecne."); return; }
                h.Patch(m, postfix: new HarmonyLib.HarmonyMethod(typeof(BkSupplyTemper), "PostInitPostfix"));
                ApplyW1(h, t);   // W1: kara morale zaopatrzenia BK = dni braku (dzielnik w liczeniu morale + klamra przy ticku)

                // czapka na kazdy model potrzeb BK (Calculate*Need)
                int capped = 0;
                var tModel = QuartermasterLaw.FindType("BannerKings.Models.BKModels.BKPartyNeedsModel");
                if (tModel != null)
                {
                    var capPost = new HarmonyLib.HarmonyMethod(typeof(BkSupplyTemper), "NeedCapPostfix");
                    foreach (var mm in tModel.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
                    {
                        if (!mm.Name.StartsWith("Calculate") || !mm.Name.EndsWith("Need")) continue;
                        if (mm.ReturnType != typeof(TaleWorlds.CampaignSystem.ExplainedNumber)) continue;
                        try { h.Patch(mm, postfix: capPost); capped++; } catch { }
                    }
                    // 150: tekstylia = 0 przy odziezy wojska - po czapce (Priority.Last), takze dla partii gracza
                    try
                    {
                        var cloth = HarmonyLib.AccessTools.Method(tModel, "CalculateClothNeed");
                        if (cloth != null && cloth.ReturnType == typeof(TaleWorlds.CampaignSystem.ExplainedNumber))
                        {
                            h.Patch(cloth, postfix: new HarmonyLib.HarmonyMethod(typeof(BkSupplyTemper), "ClothZeroPostfix") { priority = HarmonyLib.Priority.Last });
                            ClothHooked = true;
                        }
                        _clothGet = HarmonyLib.AccessTools.PropertyGetter(t, "ClothNeed");
                        _clothSet = HarmonyLib.AccessTools.PropertySetter(t, "ClothNeed");
                        ClothResetReady = _clothGet != null && _clothSet != null && _clothGet.ReturnType == typeof(float);
                        if (!ClothResetReady) { _clothGet = null; _clothSet = null; }
                    }
                    catch (Exception e) { Log.Error("BkSupplyTemper.ApplyAll(CalculateClothNeed)", e); }
                    // 172: strzaly = 0 dla partii AI przy czynnych strzelarzach - po czapce (Priority.Last)
                    try
                    {
                        var arrows = HarmonyLib.AccessTools.Method(tModel, "CalculateArrowsNeed");
                        _partyGet = HarmonyLib.AccessTools.PropertyGetter(t, "Party");
                        if (arrows != null && arrows.ReturnType == typeof(TaleWorlds.CampaignSystem.ExplainedNumber) && _partyGet != null)
                        {
                            h.Patch(arrows, postfix: new HarmonyLib.HarmonyMethod(typeof(BkSupplyTemper), "ArrowsZeroPostfix") { priority = HarmonyLib.Priority.Last });
                            ArrowsHooked = true;
                        }
                        _arrowsGet = HarmonyLib.AccessTools.PropertyGetter(t, "ArrowsNeed");
                        _arrowsSet = HarmonyLib.AccessTools.PropertySetter(t, "ArrowsNeed");
                        ArrowsResetReady = _arrowsGet != null && _arrowsSet != null && _arrowsGet.ReturnType == typeof(float);
                        // recenzja 172: zerowanie zapisanej ArrowsNeed takze przed zakupem (BuyItems() bez parametrow - wola go Tick i wejscie do osady)
                    }
                    catch (Exception e) { Log.Error("BkSupplyTemper.ApplyAll(CalculateArrowsNeed)", e); }
                    // 174.0: bron i tarcze = 0 dla partii AI (BkSuppliesNoArms) i gracza (BkSuppliesNoArmsPlayer) - po czapce (Priority.Last)
                    try
                    {
                        if (_partyGet == null) _partyGet = HarmonyLib.AccessTools.PropertyGetter(t, "Party");
                        var weap = HarmonyLib.AccessTools.Method(tModel, "CalculateWeaponsNeed");
                        var shld = HarmonyLib.AccessTools.Method(tModel, "CalculateShieldsNeed");
                        if (weap != null && shld != null && weap.ReturnType == typeof(TaleWorlds.CampaignSystem.ExplainedNumber) && shld.ReturnType == typeof(TaleWorlds.CampaignSystem.ExplainedNumber) && _partyGet != null)
                        {
                            h.Patch(weap, postfix: new HarmonyLib.HarmonyMethod(typeof(BkSupplyTemper), "WeaponsZeroPostfix") { priority = HarmonyLib.Priority.Last });
                            h.Patch(shld, postfix: new HarmonyLib.HarmonyMethod(typeof(BkSupplyTemper), "ShieldsZeroPostfix") { priority = HarmonyLib.Priority.Last });
                            ArmsHooked = true;
                        }
                        _weapGet = HarmonyLib.AccessTools.PropertyGetter(t, "WeaponsNeed");
                        _weapSet = HarmonyLib.AccessTools.PropertySetter(t, "WeaponsNeed");
                        _shieldGet = HarmonyLib.AccessTools.PropertyGetter(t, "ShieldsNeed");
                        _shieldSet = HarmonyLib.AccessTools.PropertySetter(t, "ShieldsNeed");
                        ArmsResetReady = _weapGet != null && _weapSet != null && _shieldGet != null && _shieldSet != null && _weapGet.ReturnType == typeof(float) && _shieldGet.ReturnType == typeof(float);
                    }
                    catch (Exception e) { Log.Error("BkSupplyTemper.ApplyAll(CalculateWeaponsNeed)", e); }
                    // recenzja 172 + 174.0: zerowanie zapisanych potrzeb takze przed zakupem (BuyItems() bez parametrow - wola go Tick i wejscie do osady)
                    try
                    {
                        var buy = HarmonyLib.AccessTools.Method(t, "BuyItems", Type.EmptyTypes);
                        if (buy != null && (ArrowsResetReady || ArmsResetReady) && _partyGet != null)
                        {
                            h.Patch(buy, prefix: new HarmonyLib.HarmonyMethod(typeof(BkSupplyTemper), "NeedsBuyPrefix"));
                            NeedsBuyHooked = true;
                        }
                    }
                    catch (Exception e) { Log.Error("BkSupplyTemper.ApplyAll(BuyItems)", e); }
                }
                Log.Info("BkSupplyTemper: tekstylia zaopatrzenia BK (CalculateClothNeed) = 0 przy odziezy wojska (150, MCM Army Clothing Enabled) - "
                         + (ClothHooked ? "wpiete" : "BRAK latki (BK kupi tekstylia jak dotad)") + ", zerowanie zapisanej potrzeby ClothNeed " + (ClothResetReady ? "wpiete" : "BRAK") + ".");
                Log.Info("BkSupplyTemper: strzaly zaopatrzenia BK (CalculateArrowsNeed) = 0 dla partii AI przy czynnych strzelarzach (172) - "
                         + (ArrowsHooked ? "wpiete" : "BRAK latki (BK kupi i zuzyje strzaly jak dotad)") + ", zerowanie zapisanej potrzeby ArrowsNeed " + (ArrowsResetReady ? "wpiete" : "BRAK") + " (przed zakupem BuyItems " + (NeedsBuyHooked ? "wpiete" : "BRAK") + ").");
                Log.Info("BkSupplyTemper: bron i tarcze zaopatrzenia BK (CalculateWeaponsNeed, CalculateShieldsNeed) = 0 (174.0; partie AI przy BkSuppliesNoArms i zakupach AI, gracz przy BkSuppliesNoArmsPlayer) - "
                         + (ArmsHooked ? "wpiete" : "BRAK latki (BK kupi i zuzyje bron i tarcze jak dotad)") + ", zerowanie zapisanych WeaponsNeed i ShieldsNeed " + (ArmsResetReady ? "wpiete" : "BRAK") + ".");
                Log.Info("BkSupplyTemper: sakwy AI ograniczone (dni=" + (Settings.Current != null ? Settings.Current.BkSupplyDaysCap : 4)
                         + ", sufit sztuk=" + (Settings.Current != null ? Settings.Current.BkSupplyMaxPieces : 15)
                         + ", czapka w " + capped + " modelach potrzeb).");
            }
            catch (Exception e) { Log.Error("BkSupplyTemper.ApplyAll", e); }
        }
    }
}

