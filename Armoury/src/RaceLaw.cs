using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// RASY OSOBNO (decyzja Jeffa 10.10, wiazaca): "olbrzymy moga tylko z olbrzymami, ludzie z ludzmi; nie ma zadnej ciazy ani malzenstwa olbrzyma
    /// z czlowiekiem". Rasy w danych modow: human (domyslna), giant, wight, whitewalker (ROT) - liczy sie CharacterObject.Race (numer rasy gry).
    /// Dowod: test C3c (CrashScribe session-2026-10-10_04-30-07) - silnik zawisl w dobie 43 tuz po SilentAssert w HeroCreator.DeliverOffSpring
    /// ("mother.CharacterObject.Race == father.CharacterObject.Race"), wolanym z PregnancyCampaignBehavior.CheckOffspringToDeliver.
    /// Sprawdzone w dekompilacji 1.4.8 / BK / ROT:
    /// - model malzenstwa w grze to ROTMarriageModel (dekorator: wlasne warunki, potem _previousModel), pod nim BKMarriageModel, ktory
    ///   IsCoupleSuitableForMarriage dziedziczy z DefaultMarriageModel; z modelu korzystaja sluby NPC gry (RomanceCampaignBehavior przez
    ///   NpcCoupleMarriageChance -> IsCoupleSuitableForMarriage), ROT (ROTRelationshipsBehavior), dialogi i kontrakty BK (BKMarriageBehavior);
    /// - ciaza: MakePregnantAction.Apply(matka) -> OnChildConceived -> PregnancyCampaignBehavior.ChildConceived dopisuje ciaze (matka, matka.Spouse)
    ///   do _heroPregnancies (BK wola to samo MakePregnantAction); porod: CheckOffspringToDeliver -> HeroCreator.DeliverOffSpring.
    /// Latki (wylacznik SameRaceOnly, czytany przy kazdym wywolaniu):
    /// (a) postfiks DefaultMarriageModel.IsCoupleSuitableForMarriage (i nadpisanie w modelu, ktory gra naprawde uzywa - np. ROT - wpinane przy starcie
    ///     sesji) -> false dla roznych ras. Sam slub (MarriageAction.ApplyInternal) pyta ten model i przy false nic nie robi - osobnej latki na akcje
    ///     nie trzeba;
    /// (b) prefiks MakePregnantAction.Apply i ApplyInternal - matka i jej malzonek roznych ras: brak ciazy (nic sie nie dzieje);
    /// (c) prefiks PregnancyCampaignBehavior.CheckOffspringToDeliver - ciaza roznych ras ze starego zapisu konczy sie bez porodu: wpis zdjety
    ///     z _heroPregnancies i IsPregnant = false (tak jak gra przy smierci matki i ROT przy przemianie w Innego), bez DeliverOffSpring.
    /// Malzenstwa roznych ras ze startu ROT zostaja - tylko bez ciaz. Linia logu raz na dobe, tylko gdy cos zablokowano: "Rasy: dzien N | ...".
    /// </summary>
    internal static class RaceLaw
    {
        private static FieldInfo _fList, _fMother, _fFather;
        private static readonly HashSet<long> _pairs = new HashSet<long>();   // pary zakazane przez model w tej dobie (model pytany wiele razy o te sama pare)
        private static int _preg, _births, _stumbles;
        private static int _birthSess;   // do przykladow w logu (licznik sesji - dobowe zeruje Daily)
        private static readonly HashSet<MethodBase> _patchedModels = new HashSet<MethodBase>();
        private static Harmony _h;

        /// <summary>Wylacznik SameRaceOnly (bez ustawien = wlaczony).</summary>
        internal static bool On { get { var s = Settings.Current; return s == null || s.SameRaceOnly; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3 || _stumbles % 100 == 0) Log.Error(where + " (potkniecie " + _stumbles + " w sesji)", e);
        }

        /// <summary>Rozne rasy - oba znane i rozne numery rasy; brak danych = nie blokujemy (zachowanie gry).</summary>
        internal static bool Differ(Hero a, Hero b)
        {
            if (a == null || b == null) return false;
            var ca = a.CharacterObject; var cb = b.CharacterObject;
            if (ca == null || cb == null) return false;
            return ca.Race != cb.Race;
        }

        private static string RaceName(int race)
        {
            try
            {
                var names = FaceGen.GetRaceNames();
                if (names != null && race >= 0 && race < names.Length) return names[race];
            }
            catch { }
            return "rasa " + race;
        }

        private static string Who(Hero h)
        {
            try { return h.Name + " (" + RaceName(h.CharacterObject.Race) + ")"; } catch { return "?"; }
        }

        private static long PairKey(Hero a, Hero b)
        {
            uint x = a.Id.InternalValue, y = b.Id.InternalValue;
            if (x > y) { uint t = x; x = y; y = t; }
            return ((long)x << 32) | y;
        }

        // ------------------------------------------------------------ (a) slub
        /// <summary>Postfiks IsCoupleSuitableForMarriage: rozne rasy -> false (para liczona raz na dobe).</summary>
        // argumenty po pozycji (__0, __1) - nadpisania w innych modach moga inaczej nazywac parametry; model bywa pytany z innych watkow (BK trzyma
        // pamiec modelu w ConcurrentDictionary) - zbior par pod zamkiem
        public static void CouplePostfix(Hero __0, Hero __1, ref bool __result)
        {
            try
            {
                if (!__result || !On || !Differ(__0, __1)) return;
                __result = false;
                long k = PairKey(__0, __1);
                lock (_pairs) _pairs.Add(k);
            }
            catch (Exception e) { Stumble("RaceLaw.CouplePostfix", e); }
        }

        // ------------------------------------------------------------ (b) ciaza
        /// <summary>Prefiks MakePregnantAction.Apply / ApplyInternal: matka i jej malzonek (ojciec wpisywany przez gre) roznych ras - brak ciazy.</summary>
        public static bool PregPrefix(Hero __0)
        {
            try
            {
                var mother = __0;
                if (!On || mother == null || !Differ(mother, mother.Spouse)) return true;
                _preg++;
                return false;
            }
            catch (Exception e) { Stumble("RaceLaw.PregPrefix", e); return true; }
        }

        // ------------------------------------------------------------ (c) porod
        /// <summary>
        /// Prefiks PregnancyCampaignBehavior.CheckOffspringToDeliver: ciaza roznych ras (stary zapis) konczy sie bez porodu - wpis zdjety z listy ciaz
        /// gry i IsPregnant = false, DeliverOffSpring nie jest wolany. Gra sprawdza ciaze co dobe (DailyTickHero ciezarnej) - koniec przy pierwszym ticku.
        /// </summary>
        public static bool DeliverPrefix(object __instance, object __0)
        {
            try
            {
                var pregnancy = __0;
                if (!On || pregnancy == null || _fMother == null || _fFather == null) return true;
                var mother = _fMother.GetValue(pregnancy) as Hero;
                var father = _fFather.GetValue(pregnancy) as Hero;
                if (!Differ(mother, father)) return true;
                var list = _fList != null ? _fList.GetValue(__instance) as IList : null;
                if (list != null) list.Remove(pregnancy);
                mother.IsPregnant = false;
                _births++; _birthSess++;
                if (_birthSess <= 5) Log.Info("Rasy: ciaza " + Who(mother) + " z " + Who(father) + " zakonczona bez porodu (rozne rasy) [" + _birthSess + " w sesji].");
                return false;
            }
            catch (Exception e) { Stumble("RaceLaw.DeliverPrefix", e); return true; }
        }

        // ------------------------------------------------------------ wpinanie
        internal static void ApplyAll(Harmony h)
        {
            _h = h;
            var parts = new List<string>();
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(DefaultMarriageModel), "IsCoupleSuitableForMarriage", new[] { typeof(Hero), typeof(Hero) });
                if (m != null) { h.Patch(m, postfix: new HarmonyMethod(typeof(RaceLaw), nameof(CouplePostfix))); _patchedModels.Add(m); parts.Add("model slubu gry"); }
                else parts.Add("BRAK DefaultMarriageModel.IsCoupleSuitableForMarriage");
            }
            catch (Exception e) { Log.Error("RaceLaw.ApplyAll (model)", e); parts.Add("model - WYJATEK"); }
            try
            {
                int n = 0;
                foreach (var name in new[] { "Apply", "ApplyInternal" })
                {
                    var m = AccessTools.Method(typeof(MakePregnantAction), name, new[] { typeof(Hero) });
                    if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(RaceLaw), nameof(PregPrefix))); n++; }
                }
                parts.Add(n == 2 ? "MakePregnantAction" : "MakePregnantAction " + n + "/2");
            }
            catch (Exception e) { Log.Error("RaceLaw.ApplyAll (MakePregnantAction)", e); parts.Add("MakePregnantAction - WYJATEK"); }
            try
            {
                var tB = typeof(PregnancyCampaignBehavior);
                var tP = AccessTools.Inner(tB, "Pregnancy");
                _fList = AccessTools.Field(tB, "_heroPregnancies");
                _fMother = tP != null ? AccessTools.Field(tP, "Mother") : null;
                _fFather = tP != null ? AccessTools.Field(tP, "Father") : null;
                var m = tP != null ? AccessTools.Method(tB, "CheckOffspringToDeliver", new[] { tP }) : null;
                if (m != null && _fList != null && _fMother != null && _fFather != null)
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(RaceLaw), nameof(DeliverPrefix)));
                    parts.Add("porod (CheckOffspringToDeliver)");
                }
                else parts.Add("BRAK PregnancyCampaignBehavior.CheckOffspringToDeliver / pol ciazy - porody roznych ras bez straznika");
            }
            catch (Exception e) { Log.Error("RaceLaw.ApplyAll (porod)", e); parts.Add("porod - WYJATEK"); }
            Log.Info("Rasy: latki wpiete - " + string.Join(", ", parts) + ".");
        }

        /// <summary>
        /// Start sesji: model malzenstwa, ktory gra naprawde uzywa (ROT owija BK, BK dziedziczy z gry), moze miec WLASNE IsCoupleSuitableForMarriage -
        /// wtedy postfiks idzie tez na nie (para liczona raz). Do tego linia stanu: bohaterowie wedlug rasy, malzenstwa i ciaze roznych ras.
        /// </summary>
        internal static void OnSessionLaunched()
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            string model = "?";
            try
            {
                var mm = Campaign.Current != null ? Campaign.Current.Models.MarriageModel : null;
                if (mm != null && _h != null)
                {
                    var added = new List<string>();
                    for (var t = mm.GetType(); t != null && t != typeof(DefaultMarriageModel) && t != typeof(object); t = t.BaseType)
                    {
                        var m = AccessTools.DeclaredMethod(t, "IsCoupleSuitableForMarriage", new[] { typeof(Hero), typeof(Hero) });
                        if (m == null || m.IsAbstract || _patchedModels.Contains(m)) continue;
                        _h.Patch(m, postfix: new HarmonyMethod(typeof(RaceLaw), nameof(CouplePostfix)));
                        _patchedModels.Add(m);
                        added.Add(t.FullName);
                    }
                    model = mm.GetType().FullName + (added.Count > 0 ? " (postfiks takze na: " + string.Join(", ", added) + ")" : " (bez wlasnego IsCoupleSuitableForMarriage - wystarczy model gry)");
                }
            }
            catch (Exception e) { Log.Error("RaceLaw.OnSessionLaunched (model)", e); model = "WYJATEK"; }
            try
            {
                var byRace = new SortedDictionary<int, int>();
                int mixed = 0, mixedPreg = 0;
                foreach (var h in Hero.AllAliveHeroes)
                {
                    if (h == null || h.CharacterObject == null) continue;
                    int r = h.CharacterObject.Race;
                    int c; byRace.TryGetValue(r, out c); byRace[r] = c + 1;
                    if (h.Spouse != null && h.Spouse.IsAlive && Differ(h, h.Spouse) && h.IsFemale) { mixed++; if (h.IsPregnant) mixedPreg++; }
                }
                int listMixed = 0;
                try
                {
                    var beh = Campaign.Current != null ? Campaign.Current.GetCampaignBehavior<PregnancyCampaignBehavior>() : null;
                    var list = beh != null && _fList != null ? _fList.GetValue(beh) as IList : null;
                    if (list != null && _fMother != null && _fFather != null)
                        foreach (var p in list) if (Differ(_fMother.GetValue(p) as Hero, _fFather.GetValue(p) as Hero)) listMixed++;
                }
                catch { }
                var sb = new System.Text.StringBuilder();
                foreach (var kv in byRace) { if (sb.Length > 0) sb.Append(", "); sb.Append(RaceName(kv.Key)).Append(' ').Append(kv.Value.ToString(ci)); }
                Log.Info("Rasy: start sesji - SameRaceOnly=" + On + "; model malzenstwa " + model + "; zywi bohaterowie wedlug rasy: " + sb
                         + "; malzenstwa roznych ras (zostaja, bez ciaz) " + mixed + " (w tym w ciazy " + mixedPreg + "); ciaze roznych ras na liscie gry " + listMixed
                         + (listMixed > 0 ? " - skoncza sie bez porodu przy najblizszym ticku dnia" : "") + ".");
            }
            catch (Exception e) { Log.Error("RaceLaw.OnSessionLaunched (stan)", e); }
            lock (_pairs) _pairs.Clear();
            _preg = 0; _births = 0; _birthSess = 0;
        }

        /// <summary>Linia doby - tylko gdy cos zablokowano.</summary>
        internal static void Daily()
        {
            int pairs;
            lock (_pairs) { pairs = _pairs.Count; _pairs.Clear(); }
            // sluby = rozne pary, ktore model gry uznalby za dobre, a odrzucila je rasa (model pytany wiele razy dziennie o te same pary)
            if (pairs + _preg + _births > 0)
                Log.Info("Rasy: dzien " + (int)CampaignTime.Now.ToDays + " | slubow roznych ras zablokowano " + pairs + ", ciaz " + _preg + ", porodow roznych ras przerwano " + _births);
            _preg = 0; _births = 0;
        }
    }
}
