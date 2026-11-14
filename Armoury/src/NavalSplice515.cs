using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 515 (perk 102 audytu perkow 14.11; Jeff 14.11 "78-105 ok ... 102 podlaczyc DLC pod modele BK"): modele NavalDLC zoldu, jedzenia, milicji i treningu
    /// pod modelami BannerKings.
    ///
    /// DOWOD: NavalDLC laduje sie PRZED BannerKings (log 14.11 13:09 linia 665: "zold: BKROTPartyWageModel -> ROTPartyWageModel -> BEE_PartyWageModel ->
    /// BKPartyWageModel"; linia 1217: "teraz: RealisticFoodConsumptionModel -> ROTMobilePartyFoodConsumptionModel -> BEE_MobilePartyFoodConsumptionModel ->
    /// BKPartyConsumptionModel"; ArmsDrill: czynny trening "BannerKings.Models.Vanilla.BKPartyTrainningModel"). NavalDLC dodaje swoje modele przez
    /// AddModel&lt;T&gt; (BaseModel = model gry Default*), potem BK dodaje BKPartyWageModel / BKPartyConsumptionModel / BKMilitiaModel / BKPartyTrainningModel -
    /// klasy po Default*, ktore wolaja base, a nie BaseModel - wiec model NavalDLC zostaje pod spodem i nie biegnie nigdy. Ginie: zold na morzu -20%, konwoj
    /// -80%, perki bosmana (NavalHorde, Optimization, PopularCaptain - takze koszt werbunku); jedzenie na morzu -20% i SmoothOperator; milicja: AccuracyTraining,
    /// NavalFightingTraining (weterani), polityka BolsterTheFyrd (+25% przyrostu); trening: XP z bitwy Mariner.Arr (marynarze) i PirateHunter (szeregowi).
    ///
    /// ZMIANA (raz na kampanie, OnSessionLaunched; klucz NavalModelsUnderBk, zmiana dziala od wczytania):
    ///  (A) zold, jedzenie, milicja - model BK jest dnem lancucha owijek (BEE _baseModel, ROT _previousModel): model NavalDLC dostaje BaseModel = obiekt BK
    ///      (MBGameModel.Initialize, publiczna metoda gry), a pola owijek, ktore wskazywaly obiekt BK, wskazuja model NavalDLC. Lancuch: ... -> BEE -> NavalDLC
    ///      -> BK (zold, jedzenie), ROT -> NavalDLC -> BK (milicja). Kazdy model biegnie raz; latki na BK (CompanionWage, MountedWage, Rations - licznik
    ///      zagniezdzenia) licza sie raz jak dotad. NavalDLC tylko przekazuje inne metody do BaseModel (GetCharacterWage, DoesPartyConsumeFood, ...).
    ///  (B) trening - BK jest modelem CZYNNYM (nikt go nie owija), a CalculateXpGainFromBattles dziedziczy po DefaultPartyTrainingModel: prefiks na metodzie
    ///      klasy deklarujacej, tylko dla obiektu BK, oddaje wynik modelu NavalDLC (BaseModel = obiekt BK; jego wywolanie bazy przechodzi przez znacznik watku
    ///      do metody gry). Wpinany tylko, gdy na tej metodzie nie ma cudzych latek (inaczej liczylyby sie 2 razy) - inaczej BRAK w linii startu.
    /// Zold: straznik NaN / ujemnego zoldu z NavalDLC (Optimization dzieli przez baze zoldu) zostaje w BkrotChains.WageTotal; klucz FixBkrotWageSeaCut dalej
    /// wylacza obnizke morska (NavalWagePrefix), a linia doby P4 "bez obnizki morskiej" pokazuje w koncu roznice. Zamknieta ekonomia: mniejszy zold = mniej
    /// zlota wyplacanego (platnik ten sam), mniej jedzenia = mniej zjedzonego z zapasu; milicja z ludzi (PeopleLevy / ArmsFromPeople) - polityka i perki
    /// zmieniaja przyrost, ludzie dalej ida z DOM.
    /// Linie: "Modele NavalDLC pod BK (515): start sesji ..." (co wpiete, lancuch) i "Modele NavalDLC pod BK (515): dzien N" (przekierowania treningu, potkniecia).
    /// Bledy liczone (potkniecia), bez globalnego wylacznika; wyjatek przy wpinaniu jednego rodzaju zostawia go jak dotad.
    /// </summary>
    internal static class NavalSplice515
    {
        internal const string P = "Modele NavalDLC pod BK (515): ";
        private const string Owner = "com.jeff.armoury.navalsplice515";

        private sealed class Kind
        {
            public string Label, Naval, Bk;
            public Type Abs;
            public string State = "-";
        }

        private static readonly Kind[] Kinds =
        {
            new Kind { Label = "zold", Abs = typeof(PartyWageModel), Naval = "NavalDLC.GameComponents.NavalDLCPartyWageModel", Bk = "BannerKings.Models.Vanilla.BKPartyWageModel" },
            new Kind { Label = "jedzenie", Abs = typeof(MobilePartyFoodConsumptionModel), Naval = "NavalDLC.GameComponents.NavalDLCMobilePartyFoodConsumptionModel", Bk = "BannerKings.Models.Vanilla.BKPartyConsumptionModel" },
            new Kind { Label = "milicja", Abs = typeof(SettlementMilitiaModel), Naval = "NavalDLC.GameComponents.NavalDLCSettlementMilitiaModel", Bk = "BannerKings.Models.Vanilla.BKMilitiaModel" },
            new Kind { Label = "trening", Abs = typeof(PartyTrainingModel), Naval = "NavalDLC.GameComponents.NavalDLCPartyTrainingModel", Bk = "BannerKings.Models.Vanilla.BKPartyTrainningModel" },
        };

        private static Harmony _h;
        private static object _camp;
        private static string _trainHook = "nie wpieta";
        private static MethodBase _trainTarget;

        // trening (B): obiekty biezacej kampanii
        private static volatile object _tBk;
        private static volatile PartyTrainingModel _tNaval;
        [ThreadStatic] private static bool _tIn;

        private static long _dTrain;
        private static int _stumbles, _dStumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        internal static void Reset()
        {
            _camp = null; _tBk = null; _tNaval = null;
            foreach (var k in Kinds) k.State = "-";
            ResetDay();
            _stumbles = 0;
            lock (_errSites) _errSites.Clear();
        }

        private static void ResetDay() { Interlocked.Exchange(ref _dTrain, 0); _dStumbles = 0; }

        private static void Stumble(string where, Exception e)
        {
            int n = Interlocked.Increment(ref _stumbles);
            Interlocked.Increment(ref _dStumbles);
            bool first; lock (_errSites) first = _errSites.Add(where);
            if (first || n % 500 == 0) Log.Error("NavalSplice515." + where + " (potkniecie " + n + ")", e);
        }

        /// <summary>OnSessionLaunched: wpiecie (raz na kampanie - obiekty modeli sa nowe po kazdym wczytaniu) i linia startu.</summary>
        internal static void EnsureHooks()
        {
            var cur = Campaign.Current;
            if (cur == null || cur.Models == null) return;
            var s = Settings.Current;
            if (ReferenceEquals(_camp, cur)) return;
            _camp = cur;
            _tBk = null; _tNaval = null;
            var sb = new StringBuilder();
            if (s == null || !s.NavalModelsUnderBk)
            {
                Log.Info(P + "start sesji | klucz Naval Models Under Bk NIE - modele NavalDLC zoldu, jedzenia, milicji i treningu dalej nie biegna (jak dotad).");
                return;
            }
            IList<GameModel> models = null;
            try { models = cur.Models.GetGameModels(); } catch (Exception e) { Stumble("GetGameModels", e); }
            if (models == null) { Log.Info(P + "start sesji | brak listy modeli kampanii - bez zmian."); return; }
            foreach (var k in Kinds)
            {
                try { k.State = Splice(k, models); }
                catch (Exception e) { k.State = "BRAK - wyjatek " + e.GetType().Name + " (jak dotad)"; Stumble("Splice(" + k.Label + ")", e); }
                sb.Append(sb.Length > 0 ? "; " : "").Append(k.Label).Append(": ").Append(k.State);
            }
            Log.Info(P + "start sesji | klucz TAK | " + sb + " | potkniecia " + _stumbles + ".");
        }

        private static GameModel Find(IList<GameModel> models, string fullName)
        {
            for (int i = 0; i < models.Count; i++) { var m = models[i]; if (m != null && m.GetType().FullName == fullName) return m; }
            return null;
        }

        private static GameModel Active(IList<GameModel> models, Type abs)
        {
            for (int i = models.Count - 1; i >= 0; i--) if (models[i] != null && abs.IsInstanceOfType(models[i])) return models[i];
            return null;
        }

        /// <summary>Pola owijek (klasa wprost po typie abstrakcyjnym, takze pole wlasciwosci BaseModel gry) wskazujace obiekt target.</summary>
        private static List<KeyValuePair<object, FieldInfo>> FieldsPointingTo(IList<GameModel> models, Type abs, object target, object skip)
        {
            var r = new List<KeyValuePair<object, FieldInfo>>();
            foreach (var m in models)
            {
                if (m == null || ReferenceEquals(m, skip) || ReferenceEquals(m, target) || !abs.IsInstanceOfType(m) || m.GetType().BaseType != abs) continue;
                for (var t = m.GetType(); t != null && t != typeof(object); t = t.BaseType)
                    foreach (var fi in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (!abs.IsAssignableFrom(fi.FieldType)) continue;
                        if (ReferenceEquals(fi.GetValue(m), target)) r.Add(new KeyValuePair<object, FieldInfo>(m, fi));
                    }
            }
            return r;
        }

        private static string Splice(Kind k, IList<GameModel> models)
        {
            var naval = Find(models, k.Naval);
            if (naval == null) return "brak modelu NavalDLC - nic do zrobienia";
            var bk = Find(models, k.Bk);
            if (bk == null) return "brak modelu BK - nic do zrobienia";
            var init = typeof(MBGameModel<>).MakeGenericType(k.Abs).GetMethod("Initialize", BindingFlags.Instance | BindingFlags.Public);
            if (init == null) return "BRAK - brak MBGameModel.Initialize (jak dotad)";
            if (FieldsPointingTo(models, k.Abs, naval, naval).Count > 0) return "NavalDLC juz w lancuchu (owijka go wskazuje) - bez zmian";
            var parents = FieldsPointingTo(models, k.Abs, bk, naval);
            var active = Active(models, k.Abs);
            if (parents.Count > 0)
            {
                // (A) owijki nad BK wskazuja NavalDLC, NavalDLC wskazuje BK
                init.Invoke(naval, new object[] { bk });
                var names = new List<string>();
                foreach (var p in parents)
                {
                    p.Value.SetValue(p.Key, naval);
                    names.Add(p.Key.GetType().Name + "." + p.Value.Name);
                }
                return "TAK - " + string.Join(", ", names.ToArray()) + " -> " + naval.GetType().Name + " -> " + bk.GetType().Name
                       + " (czynny " + (active != null ? active.GetType().Name : "?") + ")";
            }
            if (k.Abs == typeof(PartyTrainingModel) && ReferenceEquals(active, bk))
            {
                // (B) BK czynny, metoda XP z bitwy dziedziczona po grze - przekierowanie prefiksem tylko dla obiektu BK
                if (!WireTraining(bk)) return "BRAK - " + _trainHook + " (jak dotad)";
                init.Invoke(naval, new object[] { bk });
                _tNaval = naval as PartyTrainingModel;
                _tBk = bk;
                return "TAK - " + bk.GetType().Name + " (czynny).CalculateXpGainFromBattles -> " + naval.GetType().Name + " -> " + bk.GetType().Name + " [" + _trainHook + "]";
            }
            return "BRAK - nikt nie owija " + bk.GetType().Name + " i nie jest czynny (czynny " + (active != null ? active.GetType().Name : "?") + ") - bez zmian";
        }

        private static bool WireTraining(object bk)
        {
            if (_trainTarget != null) return true;   // raz na proces (metoda gry, wspolna dla kampanii)
            var m = AccessTools.Method(bk.GetType(), "CalculateXpGainFromBattles", new[] { typeof(FlattenedTroopRosterElement), typeof(PartyBase) });
            if (m == null) { _trainHook = "brak metody CalculateXpGainFromBattles"; return false; }
            var decl = AccessTools.DeclaredMethod(m.DeclaringType, "CalculateXpGainFromBattles", new[] { typeof(FlattenedTroopRosterElement), typeof(PartyBase) });
            if (decl == null) { _trainHook = "brak metody klasy deklarujacej " + m.DeclaringType.Name; return false; }
            var info = Harmony.GetPatchInfo(decl);
            if (info != null)
            {
                var bad = new List<string>();
                foreach (var p in info.Prefixes) if (p != null && p.owner != Owner) bad.Add(p.owner);
                foreach (var p in info.Postfixes) if (p != null && p.owner != Owner) bad.Add(p.owner);
                foreach (var p in info.Transpilers) if (p != null && p.owner != Owner) bad.Add(p.owner);
                foreach (var p in info.Finalizers) if (p != null && p.owner != Owner) bad.Add(p.owner);
                if (bad.Count > 0) { _trainHook = "cudze latki na " + decl.DeclaringType.Name + "." + decl.Name + ": " + string.Join(", ", bad.ToArray()); return false; }
            }
            if (_h == null) _h = new Harmony(Owner);
            _h.Patch(decl, prefix: new HarmonyMethod(typeof(NavalSplice515), nameof(TrainPrefix)) { priority = Priority.First });
            _trainTarget = decl;
            _trainHook = "prefiks na " + decl.DeclaringType.Name + "." + decl.Name;
            return true;
        }

        public static bool TrainPrefix(object __instance, FlattenedTroopRosterElement __0, PartyBase __1, ref ExplainedNumber __result)
        {
            if (_tIn) return true;
            var nav = _tNaval;
            if (nav == null || __instance == null || !ReferenceEquals(__instance, _tBk)) return true;
            var s = Settings.Current;
            if (s == null || !s.NavalModelsUnderBk) return true;
            _tIn = true;
            try
            {
                var r = nav.CalculateXpGainFromBattles(__0, __1);
                Interlocked.Increment(ref _dTrain);
                __result = r;
                return false;
            }
            catch (Exception e) { Stumble("TrainPrefix", e); return true; }
            finally { _tIn = false; }
        }

        internal static void Daily(int day)
        {
            try
            {
                if (_camp == null) { ResetDay(); return; }
                var sb = new StringBuilder();
                foreach (var k in Kinds) sb.Append(sb.Length > 0 ? "; " : "").Append(k.Label).Append(" ").Append(k.State.StartsWith("TAK", StringComparison.Ordinal) ? "TAK" : "NIE");
                Log.Info(P + "dzien " + day + " | wpiete: " + sb + " | XP z bitwy przez NavalDLC: " + Interlocked.Read(ref _dTrain) + " wywolan (Arr / PirateHunter wodza) (zold na morzu - linia P4 'bez obnizki morskiej') | potkniecia doby " + _dStumbles + " (od startu " + _stumbles + ").");
            }
            catch (Exception e) { Stumble("Daily", e); }
            ResetDay();
        }
    }
}
