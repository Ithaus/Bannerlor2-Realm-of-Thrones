using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace CrashScribe
{
    /// <summary>
    /// REPARACJE W WOJNACH FABULARNYCH ROT (T9 noc 08/09.10; badanie "trybut" - krok B; Jeff 08.10: "jak znika to zamykamy").
    ///
    /// Skad (dekompilacja Diplomacy 1.4.7 - DLL ladowany dla gry 1.4.8; 1.4.5 i 1.4.6 maja te same sygnatury - oraz ROT 8.1.8):
    ///  - Zmeczenie wojna Diplomacy (WarExhaustionManager.ConsiderPeaceActions) wymusza pokoj -> KingdomPeaceAction.ApplyPeace
    ///    -> DiplomacyCostCalculator.DetermineCostForMakingPeace -> DetermineReparationsForMakingPeace (private static,
    ///    zwraca KingdomWalletCost) -> AcceptPeace -> HybridCost.ApplyCost -> GiveGoldToKingdomAction.ApplyFromWalletToWallet:
    ///    placacy splaca kwote w ok. 3 doby z kies rodow (w nicosc), odbiorca dostaje cala kwote od razu (z niczego).
    ///  - ROT ROTStorylineWars.EnforceWars (DailyTick) wypowiada od nowa kazda wojne fabularna, dla ktorej
    ///    ROTStorylineWar.Enforced == true, a strony nie sa w wojnie. ROTStorylineWars.IsWarForced(a, b) to ten sam warunek
    ///    (Enforced dla wojny tej pary, w dowolnej kolejnosci stron); Enforced sprawdza tez wyeliminowanie stron, a daty
    ///    StartDay / EndDay rozciaga nasza Fabula (x FabulaTimeScale) - przez ten sam patch dla ROT i dla nas.
    ///  - Skutek (autotest 120 dob): 19 z 21 oplat (11.5 z 12.3 mln) to "pokoje" trwajace najwyzej dobe.
    ///
    /// Latka: postfiks na DetermineReparationsForMakingPeace (oryginal liczy jak dotad - czysta funkcja, bez skutkow ubocznych;
    /// potrzebujemy jego kwoty do logu). Gdy ROT IsWarForced(kingdomMakingPeace, otherKingdom) == true i kwota != 0 - Value
    /// zwroconego KingdomWalletCost = 0 (ten sam obiekt, ten sam placacy, odbiorca i portfele; to samo, co Diplomacy zwraca
    /// w swoich galeziach "bez reparacji"). ApplyCost z kwota 0 nic nie robi (GiveGoldToKingdomAction.ApplyInternal: amount != 0).
    /// Ta sama metoda zasila ekran krolestwa (KingdomWarItemVMMixin) - gracz tez widzi 0.
    /// Kwota nie wplywa na decyzje (KingdomWalletCost.CanPayCost zawsze true; grep Diplomacy: tylko ApplyCost, UI, komunikaty),
    /// wiec AI Diplomacy i ROT decyduja o wojnie i pokoju jak dotad. Wojny niefabularne - bez zmian (paczka 165 / suwak Jeffa).
    /// Trybut dzienny (vanilla, StanceLink) - bez zmian; ponowne wypowiedzenie wojny i tak go zeruje (StanceLink.ResetStats).
    ///
    /// Log: pokoj widzimy przez zdarzenie gry MakePeace (Diplomacy AcceptPeace -> MakePeaceAction.ApplyByKingdomDecision), a kwote
    /// z postfiksu tuz przed nim (ApplyPeace liczy koszt i od razu go stosuje; przy pytaniu do gracza gra stoi). Liczenie dla ekranu
    /// krolestwa bez pokoju nie daje linii - rekord czeka tylko do konca doby.
    /// Bledy: przy wyjatku oryginalna kwota zostaje (Diplomacy placi jak dotad), liczymy potkniecia, latka dziala dalej.
    /// Wylacznik: StorylineWarNoReparations w ModuleData/CrashScribe.settings.xml. Bez zapisu w grze.
    /// </summary>
    internal static class StorylineReparations
    {
        private static bool _installed;
        private static FieldInfo _fWars;          // ROT SubModule.StorylineWars (internal static ROTStorylineWars)
        private static MethodInfo _mForced;       // ROT ROTStorylineWars.IsWarForced(IFaction, IFaction) (internal)
        private static PropertyInfo _pValue;      // Diplomacy AbstractDiplomacyCost.Value { get; init; }
        private static MethodInfo _setValue;      // jego setter init
        private static FieldInfo _fValue;         // zapas: <Value>k__BackingField
        private static PropertyInfo _pPayer, _pReceiver;   // KingdomWalletCost.PayingKingdom / ReceivingKingdom

        // koszt policzony przez Diplomacy, czeka na zdarzenie MakePeace tej pary
        private sealed class Pending
        {
            public Kingdom A, B, Payer, Receiver;
            public long Z;          // kwota Diplomacy przed latka
            public bool Forced;     // wojna fabularna ROT - wyzerowane
            public CampaignTime At;
        }
        private static readonly List<Pending> _pending = new List<Pending>();
        private const int PendingMax = 64;

        // dzis (od ostatniej linii dobowej)
        private static int _dayForced, _dayOther;
        private static long _dayForcedSum, _dayOtherSum;
        // od wczytania (sesja kampanii)
        private static int _totForced, _totOther;
        private static long _totForcedSum, _totOtherSum;
        private static int _stumbles, _stumblesDay;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_stumbles == 1) { try { Scribe.Report("CrashScribe", e, where, null); } catch { } }
        }

        internal static void Install(Harmony harmony)
        {
            try
            {
                if (!Config.StorylineWarNoReparations)
                {
                    Scribe.Line("Reparacje (T9): wylaczone (StorylineWarNoReparations = false) - reparacje Diplomacy jak w modzie, takze w wojnach fabularnych ROT.");
                    return;
                }
                var tCalc = AccessTools.TypeByName("Diplomacy.Costs.DiplomacyCostCalculator");
                if (tCalc == null) { Scribe.Line("Reparacje (T9): Diplomacy nieobecny (brak DiplomacyCostCalculator) - pominiete, wpiete 0/1."); return; }
                var tWars = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTStorylineWars");
                if (tWars == null) { Scribe.Line("Reparacje (T9): ROT nieobecny (brak ROTStorylineWars) - pominiete, wpiete 0/1."); return; }

                var missing = new List<string>();
                var tSub = tWars.Assembly.GetType("ROT.SubModule");   // ten sam DLL co ROTStorylineWars
                _fWars = tSub != null ? AccessTools.Field(tSub, "StorylineWars") : null;
                if (_fWars == null) missing.Add("ROT SubModule.StorylineWars");
                _mForced = AccessTools.Method(tWars, "IsWarForced", new[] { typeof(IFaction), typeof(IFaction) });
                if (_mForced == null || _mForced.ReturnType != typeof(bool)) missing.Add("ROT ROTStorylineWars.IsWarForced");

                var m = AccessTools.Method(tCalc, "DetermineReparationsForMakingPeace", new[] { typeof(Kingdom), typeof(Kingdom), typeof(bool) });
                if (m == null) missing.Add("Diplomacy DetermineReparationsForMakingPeace(Kingdom, Kingdom, bool)");
                else
                {
                    var tCost = m.ReturnType;   // Diplomacy.Costs.KingdomWalletCost
                    _pValue = AccessTools.Property(tCost, "Value");
                    _pPayer = AccessTools.Property(tCost, "PayingKingdom");
                    _pReceiver = AccessTools.Property(tCost, "ReceivingKingdom");
                    _setValue = _pValue != null ? _pValue.GetSetMethod(true) : null;
                    if (_setValue == null) _fValue = AccessTools.Field(tCost, "<Value>k__BackingField");
                    if (_pValue == null || _pValue.PropertyType != typeof(float) || (_setValue == null && _fValue == null))
                        missing.Add("Diplomacy KingdomWalletCost.Value");
                    if (_pPayer == null || _pReceiver == null) missing.Add("Diplomacy KingdomWalletCost.PayingKingdom/ReceivingKingdom");
                }
                if (missing.Count > 0)
                {
                    Scribe.Line("Reparacje (T9): NIEZNALEZIONE " + string.Join(", ", missing.ToArray()) + " - pominiete, wpiete 0/1 (reparacje Diplomacy jak w modzie).");
                    return;
                }

                harmony.Patch(m, postfix: new HarmonyMethod(typeof(StorylineReparations), nameof(Postfix)));
                _installed = true;
                Scribe.Line("Reparacje (T9): wpiete 1/1 - Diplomacy DetermineReparationsForMakingPeace: pokoj w wojnie fabularnej, ktora ROT wymusza"
                            + " (ROTStorylineWars.IsWarForced - ten sam warunek, z ktorym ROT EnforceWars wypowiada ja od nowa nastepnej doby), bez reparacji"
                            + " (0 dla placacego i dla odbiorcy, ekran krolestwa tez 0); pozostale pokoje i decyzje o wojnie i pokoju bez zmian.");
            }
            catch (Exception e) { Stumble("StorylineReparations.Install", e); }
        }

        /// <summary>Postfiks DiplomacyCostCalculator.DetermineReparationsForMakingPeace(kingdomMakingPeace, otherKingdom, forcePlayerCharacterCosts):
        /// wojna fabularna ROT - kwota 0.</summary>
        public static void Postfix(Kingdom __0, Kingdom __1, object __result)
        {
            if (!_installed || !Config.StorylineWarNoReparations || __result == null || __0 == null || __1 == null) return;
            try
            {
                // najpierw caly odczyt; wyjatek przed SetZero = oryginalna kwota zostaje i nie ma rekordu (zadnej falszywej linii)
                float z = (float)_pValue.GetValue(__result, null);
                var payer = _pPayer.GetValue(__result, null) as Kingdom;
                var receiver = _pReceiver.GetValue(__result, null) as Kingdom;
                bool forced = IsForced(__0, __1);
                if (forced && z != 0f) SetZero(__result);
                Remember(__0, __1, payer, receiver, z, forced);
            }
            catch (Exception e) { Stumble("StorylineReparations.Postfix", e); }
        }

        private static bool IsForced(Kingdom a, Kingdom b)
        {
            object wars = _fWars.GetValue(null);
            if (wars == null) return false;   // ROT bez zachowania wojen fabularnych w tej grze - nic nie wymusza
            object r = _mForced.Invoke(wars, new object[] { a, b });
            return r is bool && (bool)r;
        }

        private static void SetZero(object cost)
        {
            if (_setValue != null) _setValue.Invoke(cost, new object[] { 0f });
            else _fValue.SetValue(cost, 0f);
            if ((float)_pValue.GetValue(cost, null) != 0f) throw new InvalidOperationException("KingdomWalletCost.Value nie wyzerowane");
        }

        private static bool Same(Pending p, Kingdom a, Kingdom b)
        {
            return (p.A == a && p.B == b) || (p.A == b && p.B == a);
        }

        private static void Remember(Kingdom a, Kingdom b, Kingdom payer, Kingdom receiver, float z, bool forced)
        {
            _pending.RemoveAll(p => Same(p, a, b));   // najnowsze liczenie tej pary wygrywa (ApplyPeace liczy tuz przed pokojem)
            if (_pending.Count >= PendingMax) _pending.RemoveAt(0);
            _pending.Add(new Pending { A = a, B = b, Payer = payer, Receiver = receiver, Z = (long)z, Forced = forced, At = CampaignTime.Now });
        }

        /// <summary>Zdarzenie gry MakePeace: jesli Diplomacy wlasnie policzyl koszt tej pary - linia logu i liczniki.</summary>
        internal static void OnMakePeace(IFaction side1, IFaction side2, MakePeaceAction.MakePeaceDetail detail)
        {
            try
            {
                if (!_installed || !Config.StorylineWarNoReparations || _pending.Count == 0) return;
                var a = side1 as Kingdom;
                var b = side2 as Kingdom;
                if (a == null || b == null) return;
                int i = _pending.FindIndex(x => Same(x, a, b));
                if (i < 0) return;   // pokoj bez kosztu Diplomacy (nie z ApplyPeace) - nie nasza sprawa
                var p = _pending[i];
                _pending.RemoveAt(i);
                if ((CampaignTime.Now - p.At).ToHours > 24.0) return;   // stary rekord z ekranu krolestwa

                if (p.Forced)
                {
                    _dayForced++; _totForced++; _dayForcedSum += p.Z; _totForcedSum += p.Z;
                    Scribe.Line("Reparacje (T9): dzien " + Day() + " - pokoj " + N(a) + " - " + N(b) + ", wojna fabularna ROT - reparacje 0 zamiast " + p.Z
                                + (p.Z != 0 ? " (placilby " + N(p.Payer) + ", dostalby " + N(p.Receiver) + ")" : " (Diplomacy i tak nic nie naliczyl)")
                                + "; ROT wypowie te wojne od nowa najdalej nastepnej doby. Od wczytania: pokojow fabularnych " + _totForced
                                + ", reparacji nie naliczono razem " + _totForcedSum + ".");
                }
                else
                {
                    _dayOther++; _totOther++; _dayOtherSum += p.Z; _totOtherSum += p.Z;
                    Scribe.Line("Reparacje (T9): dzien " + Day() + " - pokoj " + N(a) + " - " + N(b) + ", wojna niefabularna - reparacje Diplomacy bez zmian " + p.Z
                                + (p.Z != 0 ? " (placi " + N(p.Payer) + ", dostaje " + N(p.Receiver) + ")" : "")
                                + ". Od wczytania: innych pokojow " + _totOther + ", reparacji naliczono razem " + _totOtherSum + ".");
                }
            }
            catch (Exception e) { Stumble("StorylineReparations.OnMakePeace", e); }
        }

        /// <summary>Raz na dobe: podsumowanie dnia (tylko gdy byl taki pokoj albo potkniecie); stare rekordy precz.</summary>
        internal static void DailyLine()
        {
            try
            {
                _pending.Clear();
                if (_installed && Config.StorylineWarNoReparations && (_dayForced + _dayOther + _stumblesDay) > 0)
                    Scribe.Line("Reparacje (T9): dzien " + Day() + " - dzis pokojow w wojnach fabularnych ROT " + _dayForced + " (reparacji nie naliczono " + _dayForcedSum
                                + "), innych pokojow z Diplomacy " + _dayOther + " (reparacje bez zmian " + _dayOtherSum + "); od wczytania: fabularne " + _totForced
                                + " (nie naliczono " + _totForcedSum + "), inne " + _totOther + " (naliczono " + _totOtherSum + ")."
                                + (_stumblesDay > 0 ? " Potkniecia dzis " + _stumblesDay + " (razem " + _stumbles + ", pierwsze w raporcie; przy potknieciu reparacje Diplomacy jak w modzie)." : ""));
            }
            catch (Exception e) { Stumble("StorylineReparations.DailyLine", e); }
            _dayForced = _dayOther = 0; _dayForcedSum = _dayOtherSum = 0; _stumblesDay = 0;
        }

        /// <summary>Nowa gra / wczytanie: liczniki od zera (latka zostaje - zakladana raz przy starcie gry).</summary>
        internal static void ResetSession()
        {
            _pending.Clear();
            _dayForced = _dayOther = _totForced = _totOther = 0;
            _dayForcedSum = _dayOtherSum = _totForcedSum = _totOtherSum = 0;
            _stumblesDay = 0;
        }

        private static int Day()
        {
            try { return (int)(CampaignTime.Now - Campaign.Current.Models.CampaignTimeModel.CampaignStartTime).ToDays; } catch { return 0; }
        }

        private static string N(Kingdom k)
        {
            try { return k == null ? "?" : (k.Name != null ? k.Name.ToString() : k.StringId); } catch { return "?"; }
        }
    }

    /// <summary>Zdarzenie MakePeace i linia dobowa "Reparacje (T9)" (bez zapisu w grze).</summary>
    internal sealed class StorylineReparationsBehavior : CampaignBehaviorBase
    {
        public StorylineReparationsBehavior() { StorylineReparations.ResetSession(); }

        public override void RegisterEvents()
        {
            CampaignEvents.MakePeace.AddNonSerializedListener(this, StorylineReparations.OnMakePeace);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, StorylineReparations.DailyLine);
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
