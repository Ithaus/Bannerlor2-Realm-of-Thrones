using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// R3-01c - POSAG PRZY SLUBACH AI (POMYSLY-35 nr 1, Jeff 20.10 "tak"; audyt 05 D2, L9). Klucze AiDowry (+ AiDowryWealthShare, AiDowryIncomeDays) i AiDowryMatchTilt.
    ///  - KIEDY: zdarzenie gry BeforeHeroesMarried (MarriageAction.ApplyInternal - po ustaleniu rodu po slubie, PRZED zmiana rodu; gra zamienia kolejnosc tak, ze
    ///    pierwszy bohater jest w rodzie po slubie, drugi odchodzi). Wszystkie sluby AI z AI (ROT CheckNPCMarriages i nasza droga CrashScribe MarriageFast koncza na
    ///    MarriageAction.Apply). Pominiete: slub z udzialem rodu gracza (BK rozlicza posag w kontrakcie z rozmowy - BKMarriageBehavior.ApplyMarriageContract ->
    ///    GiveGoldAction) i kazdy slub wewnatrz ApplyMarriageContract (flaga z prefiksu/finalizera - drugi bezpiecznik, zeby posag nie byl placony dwa razy).
    ///  - KTO PLACI: rod panny mlodej, gdy to ona odchodzi do rodu meza (pan mlody przechodzacy do rodu zony - bez posagu; ten sam rod - bez posagu).
    ///  - ILE: min(wycena BK GetDowryValue(panna), AiDowryWealthShare x G, AiDowryIncomeDays x D) i najwyzej G - R (tylko ponad rezerwe rodu 166). G = min(zloto
    ///    rodziny z budzetu, kiesy rodziny dzis); R i warunek budzetu - ClanBudget.ChestOf (dluznik, rod w zajeciu, Straz bez zoldu, najemnik, rod bez budzetu -
    ///    bez posagu); D - DebtLadder.DebtD (D budzetu). Brak nadwyzki = slub bez posagu.
    ///  - SKAD -> DOKAD: kiesy rodziny panny (glowa, potem dorosli; kazdy do podlogi FamilyPurseFloor) -> kiesa glowy rodu meza (ClanIncomeBook KRansom -
    ///    jednorazowe od prawdziwego platnika, nie wchodzi w D staly). Zadnego zlota z niczego.
    ///  - PRZECHYL SWATOW (AiDowryMatchTilt): postfiks NpcCoupleMarriageChance modelu na wierzchu (ROT owija BK; wpinany przy starcie sesji, DeclaredMethod na klasie
    ///    deklarujacej): ten sam krolestwo, kawaler z rodu biednego (G &lt; R miary potrzeby) i panna z bogatego (G &gt; 60 D) - szansa x1.5; odwrotnie (bogaty kawaler,
    ///    biedna panna) - x0.8. Szansa 0 zostaje 0 (skrot CrashScribe dla zonatych dalej wazny - CrashScribe MarriageFast.Foreign przepuszcza ten postfiks).
    ///    Regulator CrashScribe (MarriageTargetPerYear 48) sumuje te same szanse, wiec liczba slubow w roku sie nie zmienia.
    /// Bez zapisu (same liczniki doby). Linia doby "Posagi AI (R3-01c)".
    /// </summary>
    internal static class AiDowry
    {
        private const string HarmonyId = "com.jeff.armoury";   // ten sam identyfikator co SubModuleMain.HarmonyId
        private const float TiltUp = 1.5f, TiltDown = 0.8f, RichDays = 60f;
        private static readonly Type[] H2 = { typeof(Hero), typeof(Hero) };

        internal static bool On { get { var s = Settings.Current; return s != null && s.AiDowry && ClanBudget.On; } }
        private static bool TiltOn { get { var s = Settings.Current; return s != null && s.AiDowry && s.AiDowryMatchTilt && ClanBudget.On; } }

        private static bool _inContract, _contractTried, _bkTried;
        private static readonly HashSet<MethodBase> _patchedChance = new HashSet<MethodBase>();
        private static string _wired = "-";
        private static MethodInfo _mDowry;
        private static PropertyInfo _pInst, _pModel;

        // liczniki doby
        private static int _dWedN, _dBrideN, _dGroomN, _dSameN, _dPaidN, _dPoorN, _dNoBudgetN, _dSkipN, _dOtherN, _dTiltUp, _dTiltDown, _stumbles;
        private static long _dPaid, _dBkSum;
        private static string _dMax; private static long _dMaxV;
        private static readonly HashSet<string> _err = new HashSet<string>();

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("AiDowry." + where, e); } catch { }
        }

        private static string Name(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Name(Clan c) { try { return c != null && c.Name != null ? c.Name.ToString() : "-"; } catch { return "?"; } }

        private static void ZeroDay()
        {
            _dWedN = _dBrideN = _dGroomN = _dSameN = _dPaidN = _dPoorN = _dNoBudgetN = _dSkipN = _dOtherN = _dTiltUp = _dTiltDown = 0;
            _dPaid = _dBkSum = 0; _dMax = null; _dMaxV = 0;
        }

        internal static void RegisterEvents(CampaignBehaviorBase owner)
        {
            CampaignEvents.BeforeHeroesMarried.AddNonSerializedListener(owner, OnBeforeMarried);
        }

        // ------------------------------------------------------------ start sesji: latki
        internal static void OnSessionLaunched()
        {
            ZeroDay(); _inContract = false;
            var parts = new List<string>();
            Harmony h = null;
            try { h = new Harmony(HarmonyId); } catch (Exception e) { Stumble("Harmony", e); }
            // kontrakt BK (raz na proces)
            try
            {
                if (!_contractTried && h != null)
                {
                    _contractTried = true;
                    var t = AccessTools.TypeByName("BannerKings.Behaviours.Marriage.BKMarriageBehavior");
                    var m = t != null ? AccessTools.DeclaredMethod(t, "ApplyMarriageContract", Type.EmptyTypes) : null;
                    if (m != null)
                    {
                        h.Patch(m, prefix: new HarmonyMethod(typeof(AiDowry), nameof(ContractPrefix)), finalizer: new HarmonyMethod(typeof(AiDowry), nameof(ContractFinalizer)));
                        _wired = "kontrakt BK (ApplyMarriageContract)";
                    }
                    else _wired = "BRAK BKMarriageBehavior.ApplyMarriageContract (sluby rodu gracza i tak pominiete)";
                }
            }
            catch (Exception e) { Stumble("ContractPatch", e); _wired = "kontrakt BK - blad latki"; }
            parts.Add(_wired);
            // przechyl swatow: model, ktorego gra naprawde uzywa (ROT owija BK) - metoda najbardziej pochodna, latka na klasie deklarujacej
            try
            {
                var mm = Campaign.Current != null ? Campaign.Current.Models.MarriageModel : null;
                var res = mm != null ? AccessTools.Method(mm.GetType(), "NpcCoupleMarriageChance", H2) : null;
                var m = res != null && res.DeclaringType != null ? AccessTools.DeclaredMethod(res.DeclaringType, "NpcCoupleMarriageChance", H2) : null;
                if (m == null) parts.Add("BRAK NpcCoupleMarriageChance modelu slubu (" + (mm != null ? mm.GetType().FullName : "brak modelu") + ") - bez przechylu swatow");
                else if (_patchedChance.Contains(m)) parts.Add("przechyl swatow na " + m.DeclaringType.FullName + " (juz wpiety)");
                else if (h != null)
                {
                    h.Patch(m, postfix: new HarmonyMethod(typeof(AiDowry), nameof(ChancePostfix)) { priority = Priority.Last });
                    _patchedChance.Add(m);
                    parts.Add("przechyl swatow na " + m.DeclaringType.FullName);
                }
            }
            catch (Exception e) { Stumble("ChancePatch", e); parts.Add("przechyl swatow - blad latki"); }
            try { Log.Info("Posagi AI (R3-01c): start sesji | latki: " + string.Join(", ", parts.ToArray()) + (On ? "" : " (posag wylaczony)") + (TiltOn ? "" : " (przechyl swatow wylaczony)") + "."); } catch { }
        }

        public static void ContractPrefix() { _inContract = true; }
        public static void ContractFinalizer() { _inContract = false; }

        // ------------------------------------------------------------ wycena BK
        /// <summary>BannerKingsConfig.Instance.MarriageModel.GetDowryValue(panna, false, false).ResultNumber (refleksja); -1 - brak BK.</summary>
        private static double BkDowry(Hero bride)
        {
            try
            {
                if (!_bkTried)
                {
                    _bkTried = true;
                    var cfg = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                    _pInst = cfg != null ? AccessTools.Property(cfg, "Instance") : null;
                    _pModel = cfg != null ? AccessTools.Property(cfg, "MarriageModel") : null;
                    var mt = AccessTools.TypeByName("BannerKings.Models.Vanilla.Abstract.MarriageModel");
                    _mDowry = mt != null ? AccessTools.DeclaredMethod(mt, "GetDowryValue", new[] { typeof(Hero), typeof(bool), typeof(bool) }) : null;
                }
                if (_pInst == null || _pModel == null || _mDowry == null) return -1;
                var inst = _pInst.GetValue(null, null);
                var model = inst != null ? _pModel.GetValue(inst, null) : null;
                if (model == null) return -1;
                object en = _mDowry.Invoke(model, new object[] { bride, false, false });
                if (!(en is ExplainedNumber)) return -1;
                return Math.Max(0f, ((ExplainedNumber)en).ResultNumber);
            }
            catch (Exception e) { Stumble("BkDowry", e); return -1; }
        }

        // ------------------------------------------------------------ posag
        private static void OnBeforeMarried(Hero a, Hero b, bool show)
        {
            try
            {
                if (!On || a == null || b == null) return;
                var ca = a.Clan; var cb = b.Clan;
                if (ca == null || cb == null) return;
                if (_inContract || ca == Clan.PlayerClan || cb == Clan.PlayerClan || a == Hero.MainHero || b == Hero.MainHero) { _dSkipN++; return; }
                _dWedN++;
                if (ca == cb) { _dSameN++; return; }
                // gra: pierwszy bohater jest juz w rodzie po slubie, drugi odchodzi - sprawdzamy tym samym modelem (inny wynik = nietypowy slub, bez posagu)
                Clan after = null;
                try { after = Campaign.Current.Models.MarriageModel.GetClanAfterMarriage(a, b); } catch { after = null; }
                if (after != ca) { _dOtherN++; return; }
                if (!b.IsFemale || a.IsFemale) { _dGroomN++; return; }
                _dBrideN++;
                var bride = b; var payer = cb; var recv = ca;
                var to = recv.Leader;
                if (payer.IsBanditFaction || recv.IsBanditFaction || to == null || !to.IsAlive || to == Hero.MainHero) { _dOtherN++; return; }
                double G, R; bool war;
                if (!ClanBudget.ChestOf(payer, out G, out R, out war)) { _dNoBudgetN++; return; }
                double g = Math.Min(G, ClanIncomeBook.FamilyGoldOf(payer));
                double above = g - R;
                if (above <= 0) { _dPoorN++; return; }
                var s = Settings.Current;
                double D = DebtLadder.DebtD(payer);
                double byWealth = Math.Max(0f, Math.Min(1f, s.AiDowryWealthShare)) * g;
                double byIncome = Math.Max(0f, s.AiDowryIncomeDays) * D;
                double bk = BkDowry(bride);
                double dowry = Math.Min(byWealth, byIncome);
                if (bk >= 0) { dowry = Math.Min(dowry, bk); _dBkSum += (long)bk; }
                long want = (long)Math.Min(dowry, above);
                if (want <= 0) { _dPoorN++; return; }
                long got = VassalRansomAid.TakeFromFamily(payer, want, Math.Max(0, s.FamilyPurseFloor));
                if (got <= 0) { _dPoorN++; return; }
                int ig = (int)Math.Min(int.MaxValue, got);
                to.ChangeHeroGold(ig);
                CirculationWindows.NoteHeroGold(to, got);
                ClanIncomeBook.NoteInflow(to, ig, ClanIncomeBook.KRansom);   // jednorazowe od prawdziwego platnika (jak okup) - nie wchodzi w D staly
                _dPaidN++; _dPaid += got;
                if (got > _dMaxV) { _dMaxV = got; _dMax = Name(payer) + " -> " + Name(recv) + " " + got + " (" + Name(bride) + ")"; }
                Log.Info("Posag (R3-01c): " + Name(bride) + " (" + Name(payer) + ") wychodzi za " + Name(a) + " (" + Name(recv) + ") - posag " + got + " zl do " + Name(to)
                         + " (BK " + (bk >= 0 ? Math.Round(bk).ToString() : "-") + ", " + Math.Round(s.AiDowryWealthShare * 100) + "% zlota rodu " + Math.Round(byWealth) + ", "
                         + Math.Round(s.AiDowryIncomeDays) + " dni D " + Math.Round(byIncome) + ", ponad rezerwe " + Math.Round(above) + ").");
            }
            catch (Exception e) { Stumble("OnBeforeMarried", e); }
        }

        // ------------------------------------------------------------ przechyl swatow
        /// <summary>Postfiks NpcCoupleMarriageChance modelu na wierzchu: x1.5 biedny kawaler + bogata panna (ten sam krolestwo), x0.8 odwrotnie; 0 zostaje 0.</summary>
        public static void ChancePostfix(Hero __0, Hero __1, ref float __result)
        {
            if (!(__result > 0f) || __0 == null || __1 == null) return;
            try
            {
                if (!TiltOn || __0.IsFemale == __1.IsFemale) return;
                var man = __0.IsFemale ? __1 : __0; var woman = __0.IsFemale ? __0 : __1;
                var cm = man.Clan; var cw = woman.Clan;
                if (cm == null || cw == null || cm == cw || cm == Clan.PlayerClan || cw == Clan.PlayerClan) return;
                var k = cm.Kingdom;
                if (k == null || k != cw.Kingdom) return;
                double gm, rm, gw, rw;
                if (!ClanBudget.NeedChestAnyOf(cm, out gm, out rm) || !ClanBudget.NeedChestAnyOf(cw, out gw, out rw)) return;
                double dm = ClanBudget.DLast(cm), dw = ClanBudget.DLast(cw);
                if (gm < rm && dw > 0 && gw > RichDays * dw) { __result *= TiltUp; _dTiltUp++; }
                else if (gw < rw && dm > 0 && gm > RichDays * dm) { __result *= TiltDown; _dTiltDown++; }
            }
            catch (Exception e) { Stumble("ChancePostfix", e); }
        }

        // ------------------------------------------------------------ linia doby
        internal static void Report()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.LogEnabled || Campaign.Current == null) return;
                if (!On && _dWedN == 0 && _dTiltUp == 0 && _dTiltDown == 0) return;
                Log.Info("Posagi AI (R3-01c): dzien " + (int)CampaignTime.Now.ToDays + " | slubow AI-AI " + _dWedN + ": panna przechodzi do rodu meza " + _dBrideN + ", pan mlody do rodu zony "
                         + _dGroomN + ", ten sam rod " + _dSameN + ", inne (rod po slubie poza para, brak glowy rodu meza) " + _dOtherN
                         + " | posagow " + _dPaidN + " razem " + _dPaid + " zl (najwiekszy: " + (_dMax ?? "-") + "; wycena BK tych panien razem " + _dBkSum + ")"
                         + " | bez posagu: rod panny bez nadwyzki ponad rezerwe " + _dPoorN + ", rod panny bez budzetu (dluznik, zajecie, Straz, najemnik, bez krolestwa) " + _dNoBudgetN
                         + " | pominiete: rod gracza albo kontrakt BK " + _dSkipN
                         + " | swaty - ocen pary z przechylem: x" + TiltUp.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " " + _dTiltUp + ", x" + TiltDown.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " " + _dTiltDown
                         + " | latki: " + _wired + (_patchedChance.Count > 0 ? ", przechyl swatow" : ", BRAK przechylu swatow")
                         + (On ? "" : " (posag wylaczony)") + (TiltOn ? "" : " (przechyl wylaczony)") + (_stumbles > 0 ? " | potkniecia " + _stumbles : "") + ".");
            }
            catch (Exception e) { Stumble("Report", e); }
            finally { ZeroDay(); }
        }
    }
}
