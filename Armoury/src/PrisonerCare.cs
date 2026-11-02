using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace Armoury
{
    /// <summary>
    /// 318b (Jeff 02.11): "kto leczy jencow ... to tez powinno zwiekszac medicine tego, kto zajmuje sie rannymi ... moje medicine powinno rosnac".
    ///
    /// GRA (dekompilacja 1.4.8, PartyHealCampaignBehavior): ranni jency szeregowi zdrowieja w ManageHealingOfPrisonerRegulars (ryczalt modelu
    /// GetDailyHealingForRegulars(isPrisoners) = 1 na dobe), ale bez wywolania SkillLevelingManager - nikt nie dostaje Medicine XP. Wlasni ranni
    /// (HealMemberRegulars) daja OnRegularTroopHealedWhileWaiting(partia, n, sredni tier) = 5 * n * tier (x2 w miescie/wsi), XP dla EffectiveSurgeon
    /// (przypisany chirurg albo wodz partii).
    ///
    /// ZMIANA (klucz PrisonerCareXp; NIE = gra jak dotad): prefiks/postfiks na prywatnej ManageHealingOfPrisonerRegulars (DeclaredMethod), tylko partia
    /// gracza. Prefiks zapamietuje rannych per typ jenca, postfiks liczy wyleczonych i wazony tier i wola te sama metode gry co przy wlasnych rannych,
    /// z tierem razy PrisonerCareXpPercent/100 - XP idzie do tego samego chirurga, ten sam mnoznik w osadzie, SkillLedger liczy to jako "leczenie".
    /// Tempo leczenia jencow bez zmian (krok C rozpoznania 317 - osobna decyzja Jeffa).
    ///
    /// Jency-bohaterowie (ManageHealingOfPrisonerHeroes): POMINIETE. Haczyk gry OnHeroHealedWhileWaiting liczy XP z hero.PartyBelongedTo, a jeniec ma
    /// tylko PartyBelongedToAsPrisoner - gra dalaby 0. Wlasna kopia wzoru wymagalaby wolania OnPartySkillExercised poza menedzerem (poza ksiega
    /// SkillLedger), a bohater-jeniec leczy sie tylko do WoundedHealthLimit (kilka-kilkanascie HP jednorazowo) - XP rzedu kilkudziesieciu na lorda.
    /// Linie: "Opieka nad jencami (318b): start sesji ..." i doba "Opieka nad jencami (318b): wyleczonych N, ...".
    ///
    /// 319a (Jeff 02.11: "tak szybciej tak jak przy moich zolnierzach"; klucz PrisonersHealLikeYourMen, NIE = ryczalt gry 1/dobe jak w 318b):
    /// ranni jency szeregowi PARTII GRACZA zdrowieja w tym samym dobowym tempie co jego wlasni ranni szeregowi. Prefiks na prywatnej
    /// PartyHealCampaignBehavior.TryToHealOrWoundPrisoners (DeclaredMethod) - gra wola ja w TryHealOrWoundParty zaraz po leczeniu wlasnych, tylko gdy
    /// partia IsActive i MapEvent == null (warunek gry dziedziczony, bez kopii). Prefiks dopisuje do prywatnego slownika gry
    /// _overflowedHealingForPrisonerRegulars[partia] dodatek = max(0, swoi - jency) / healFrequencyPerDay, gdzie swoi = partyBase.HealingRateForMemberRegulars
    /// (czynny model z calym lancuchem BKROT/Naval/BK, SlowHealing 50%, Medicine, perki, +10 w twierdzy - ta sama wartosc, ktora gra wlasnie policzyla dla
    /// wlasnych w TryToHealOrWoundMembers), a jency = GetDailyHealingForRegulars(partia, isPrisoners true) (ryczalt 1 + blogoslawienstwa BKROT + Naval).
    /// Oba wywolania z zewnatrz modelu, nie zagniezdzone (straznik BKROT/BkrotChains.HealRegulars bezpieczny). Gra w tym samym wywolaniu dolicza swoj
    /// ryczalt, losuje wiersz i leczy (ManageHealingOfPrisonerRegulars) - XP liczy haczyk 318b, raz za kazdego wyleczonego (sami nie leczymy). Slownik
    /// zapisuje gra (SyncData), czysci przy zniszczeniu partii. Glod (swoi <= 0) albo kara dla jencow (jency < 0) - bez dodatku. Bohaterowie-jency bez zmian.
    /// Partie AI bez zmian: Jeff prosil o "moich"; szybsze leczenie jencow AI nie daje nikomu XP (haczyk 318b tylko gracz), a zmienialoby obraz jencow
    /// lordow (PrisonerLaw/niewolnicy, przesluchania po odbiciu) bez pomiaru; dodatkowo 2 wywolania modelu na partie co 6 h dla setek partii.
    /// </summary>
    internal static class PrisonerCare
    {
        private const string P = "Opieka nad jencami (318b): ";

        private static bool _hooked;
        private static string _wired = "-";
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();

        // doba (tylko partia gracza, watek gry)
        private static int _dHealed, _dCalls;
        private static float _dXp;
        private static string _dSurgeon = "-";
        private static int _woundedAtDay = -1;
        // 319a doba: sumy godzinowe (na dobe) tempa swoich i jencow oraz dopisanego dodatku i ryczaltu gry (do podzialu wyleczonych)
        private static int _lTicks;
        private static float _lMemberSum, _lPrisSum, _lExtraSum, _lBaseSum;
        private static AccessTools.FieldRef<PartyHealCampaignBehavior, Dictionary<PartyBase, float>> _accRef;

        internal static bool On { get { var s = Settings.Current; return s != null && s.PrisonerCareXp; } }
        internal static bool Like { get { var s = Settings.Current; return s != null && s.PrisonersHealLikeYourMen; } }

        internal static void RegisterEvents(CampaignBehaviorBase b)
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(b, delegate (CampaignGameStarter s) { try { SessionStart(); } catch (Exception e) { Stumble("SessionStart", e); } });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(b, delegate { try { Daily(); } catch (Exception e) { Stumble("Daily", e); } });
        }

        private static void SessionStart()
        {
            // stan doby czyszczony przy kazdym starcie sesji (nowa kampania / wczytanie)
            _dHealed = 0; _dCalls = 0; _dXp = 0f; _dSurgeon = "-"; _woundedAtDay = WoundedNow();
            ResetLike();
            EnsureHooks();
            var s = Settings.Current;
            Log.Info(P + "start sesji | klucz Prisoner Care Xp " + (On ? "tak" : "nie") + ", Prisoner Care Xp Percent " + (s != null ? s.PrisonerCareXpPercent : 0)
                     + " | latki " + _wired + " | rannych jencow gracza " + Math.Max(0, _woundedAtDay) + " | jency-bohaterowie bez XP (gra nie ma haczyka dla jenca).");
            Log.Info("Jency jak swoi (319a): start sesji | klucz Prisoners Heal Like Your Men " + (Like ? "tak" : "nie") + " | latka " + _wiredLike
                     + " | slownik gry " + (_accRef != null ? "tak" : "BRAK") + " | tylko partia gracza, AI bez zmian.");
        }

        private static string _wiredLike = "-";

        private static void ResetLike() { _lTicks = 0; _lMemberSum = 0f; _lPrisSum = 0f; _lExtraSum = 0f; _lBaseSum = 0f; }

        /// <summary>Wpiecie 319a: prefiks na TryToHealOrWoundPrisoners + dostep do slownika gry. Zawsze wpiete (klucz czytany przy kazdym wywolaniu).</summary>
        private static void HookLike(Harmony h)
        {
            string target = "PartyHealCampaignBehavior.TryToHealOrWoundPrisoners";
            try
            {
                var fld = AccessTools.DeclaredField(typeof(PartyHealCampaignBehavior), "_overflowedHealingForPrisonerRegulars");
                if (fld == null || fld.FieldType != typeof(Dictionary<PartyBase, float>)) { _wiredLike = "0/1 (BRAK pola _overflowedHealingForPrisonerRegulars)"; return; }
                _accRef = AccessTools.FieldRefAccess<PartyHealCampaignBehavior, Dictionary<PartyBase, float>>(fld);
                var m = AccessTools.DeclaredMethod(typeof(PartyHealCampaignBehavior), "TryToHealOrWoundPrisoners", new[] { typeof(PartyBase), typeof(float) });
                if (m == null) { _wiredLike = "0/1 (BRAK: " + target + ")"; return; }
                h.Patch(m, prefix: new HarmonyMethod(typeof(PrisonerCare), nameof(LikePrefix)));
                var pi = Harmony.GetPatchInfo(m);
                bool pre = false;
                var owners = new List<string>();
                if (pi != null)
                {
                    foreach (var p in pi.Prefixes) { if (p.owner == h.Id) pre = true; else owners.Add(p.owner); }
                    foreach (var p in pi.Postfixes) owners.Add(p.owner);
                    foreach (var p in pi.Transpilers) owners.Add(p.owner);
                }
                _wiredLike = (pre ? "1/1" : "0/1 (GetPatchInfo: prefiks NIE)") + " " + target
                             + (owners.Count > 0 ? " | inne latki tej metody: " + string.Join(", ", owners) : " | innych latek brak");
            }
            catch (Exception e) { _wiredLike = "0/1 (" + target + ": " + e.GetType().Name + ")"; Stumble("HookLike", e); }
        }

        /// <summary>319a: przed akumulacja jencow gry - dopisz do akumulatora gry roznice tempa swoich i jencow (na ten tick). __0 = partyBase, __1 = healFrequencyPerDay.</summary>
        public static void LikePrefix(PartyHealCampaignBehavior __instance, PartyBase __0, float __1)
        {
            try
            {
                if (__0 == null || __0 != PartyBase.MainParty || __instance == null || _accRef == null || !Like) return;
                if (__1 <= 0f) return;
                var r = __0.PrisonRoster;
                if (r == null || r.TotalWoundedRegulars <= 0) return;
                var model = Campaign.Current != null ? Campaign.Current.Models.PartyHealingModel : null;
                if (model == null) return;
                float member = __0.HealingRateForMemberRegulars;                                       // to samo co gra dla wlasnych (czynny model)
                float pris = model.GetDailyHealingForRegulars(__0, true, false).ResultNumber;        // to samo, co gra zaraz policzy dla jencow
                _lTicks++; _lMemberSum += member; _lPrisSum += pris;
                if (member <= 0f || pris < 0f) return;          // glod swoich / kara dla jencow - bez dodatku (nie lagodzic kar)
                float extraDay = member - pris;
                if (extraDay <= 0f) return;
                float add = extraDay / __1;
                var acc = _accRef(__instance);
                if (acc == null) return;
                float v;
                acc.TryGetValue(__0, out v);
                acc[__0] = v + add;
                _lExtraSum += add; _lBaseSum += pris / __1;
            }
            catch (Exception e) { Stumble("LikePrefix", e); }
        }

        private static void EnsureHooks()
        {
            if (_hooked) return;
            _hooked = true;
            HookLike(new Harmony("com.jeff.armoury.prisonercare"));   // 319a osobno - wczesniejsze return 318b go nie omija
            string target = "PartyHealCampaignBehavior.ManageHealingOfPrisonerRegulars";
            try
            {
                var h = new Harmony("com.jeff.armoury.prisonercare");
                var m = AccessTools.DeclaredMethod(typeof(PartyHealCampaignBehavior), "ManageHealingOfPrisonerRegulars",
                                                   new[] { typeof(PartyBase), typeof(float).MakeByRefType() });
                if (m == null) { _wired = "0/1 (BRAK: " + target + ")"; return; }
                h.Patch(m, prefix: new HarmonyMethod(typeof(PrisonerCare), nameof(HealPrefix)),
                           postfix: new HarmonyMethod(typeof(PrisonerCare), nameof(HealPostfix)));
                // weryfikacja (CLAUDE.md 7: latka moze byc po cichu polknieta)
                var pi = Harmony.GetPatchInfo(m);
                bool pre = false, post = false;
                var owners = new List<string>();
                if (pi != null)
                {
                    foreach (var p in pi.Prefixes) { if (p.owner == h.Id) pre = true; else owners.Add(p.owner); }
                    foreach (var p in pi.Postfixes) { if (p.owner == h.Id) post = true; else owners.Add(p.owner); }
                    foreach (var p in pi.Transpilers) owners.Add(p.owner);
                }
                _wired = (pre && post ? "1/1" : "0/1 (GetPatchInfo: prefiks " + (pre ? "tak" : "NIE") + ", postfiks " + (post ? "tak" : "NIE") + ")")
                         + " " + target + (owners.Count > 0 ? " | inne latki tej metody: " + string.Join(", ", owners) : " | innych latek brak");
            }
            catch (Exception e) { _wired = "0/1 (" + target + ": " + e.GetType().Name + ")"; Stumble("EnsureHooks", e); }
        }

        /// <summary>Stan przed leczeniem: typy jencow szeregowych z rannymi i ich ranni.</summary>
        internal sealed class Snap
        {
            internal readonly List<CharacterObject> Troops = new List<CharacterObject>();
            internal readonly List<int> Wounded = new List<int>();
        }

        public static void HealPrefix(PartyBase partyBase, out object __state)
        {
            __state = null;
            try
            {
                if (partyBase == null || partyBase != PartyBase.MainParty || (!On && !Like)) return;   // 319a: licznik wyleczonych tez bez XP
                var r = partyBase.PrisonRoster;
                if (r == null || r.TotalWoundedRegulars <= 0) return;
                var st = new Snap();
                for (int i = 0; i < r.Count; i++)
                {
                    var c = r.GetCharacterAtIndex(i);
                    int w = r.GetElementWoundedNumber(i);
                    if (c != null && c.IsRegular && w > 0) { st.Troops.Add(c); st.Wounded.Add(w); }
                }
                if (st.Troops.Count > 0) __state = st;
            }
            catch (Exception e) { __state = null; Stumble("HealPrefix", e); }
        }

        public static void HealPostfix(PartyBase partyBase, object __state)
        {
            var st0 = __state as Snap;
            if (st0 == null) return;
            try
            {
                var mp = partyBase != null ? partyBase.MobileParty : null;
                var r = partyBase != null ? partyBase.PrisonRoster : null;
                if (mp == null || r == null) return;
                int n = 0; float tiers = 0f;
                for (int k = 0; k < st0.Troops.Count; k++)
                {
                    var c = st0.Troops[k];
                    int idx = r.FindIndexOfTroop(c);
                    int now = idx >= 0 ? r.GetElementWoundedNumber(idx) : 0;
                    int healed = st0.Wounded[k] - now;
                    if (healed <= 0) continue;
                    n += healed;
                    tiers += (float)c.Tier * healed;   // jak gra w HealMemberRegulars: CharacterObject.Tier
                }
                if (n <= 0) return;
                var s = Settings.Current;
                int pct = s != null ? Math.Max(0, Math.Min(200, s.PrisonerCareXpPercent)) : 0;
                _dCalls++; _dHealed += n;
                if (!On || pct <= 0) return;   // 319a bez 318b: liczymy wyleczonych, XP nie
                float avgTier = tiers / n * (pct / 100f);
                if (avgTier <= 0f) return;   // same rekruty tier 0 - jak u wlasnych: 0 XP
                SkillLevelingManager.OnRegularTroopHealedWhileWaiting(mp, n, avgTier);
                // do linii doby: surowe XP wzorem gry (DefaultSkillLevelingManager.OnRegularTroopHealedWhileWaiting), przed nauka bohatera
                float raw = Campaign.Current.Models.PartyHealingModel.GetSkillXpFromHealingTroop(partyBase) * n * avgTier;
                if (mp.CurrentSettlement != null && !mp.CurrentSettlement.IsCastle) raw *= 2f;
                _dXp += raw;
                var surgeon = mp.EffectiveSurgeon;
                _dSurgeon = surgeon != null ? surgeon.Name + (surgeon == Hero.MainHero ? " (gracz)" : "") : "brak chirurga";
            }
            catch (Exception e) { Stumble("HealPostfix", e); }
        }

        private static int WoundedNow()
        {
            var mp = MobileParty.MainParty;
            var r = mp != null && mp.Party != null ? mp.Party.PrisonRoster : null;
            return r != null ? r.TotalWoundedRegulars : -1;
        }

        private static void Daily()
        {
            int now = WoundedNow();
            int before = _woundedAtDay;
            if ((On || Like) && (_dHealed > 0 || now > 0 || before > 0))
            {
                var ci = CultureInfo.InvariantCulture;
                // 319a: udzial dodatku w wyleczonych = proporcja dopisanego dodatku do (dodatek + ryczalt gry) w godzinach z dodatkiem (przyblizenie - gra losuje jeden akumulator)
                int extraN = 0;
                if (_lExtraSum > 0f && _dHealed > 0) extraN = (int)Math.Round(_dHealed * (_lExtraSum / (_lExtraSum + Math.Max(0f, _lBaseSum))));
                string like = !Like ? " | 319a wylaczone (ryczalt gry)"
                    : " | z tego dodatek 319a " + extraN + " | tempo/dobe: swoi " + (_lTicks > 0 ? (_lMemberSum / _lTicks).ToString("0.##", ci) : "-")
                      + ", jency gra " + (_lTicks > 0 ? (_lPrisSum / _lTicks).ToString("0.##", ci) : "-")
                      + ", dodatek dopisany " + _lExtraSum.ToString("0.##", ci) + " (godzin z pomiarem " + _lTicks + ")";
                Log.Info(P + "wyleczonych " + _dHealed + " (wywolan " + _dCalls + "), XP Medicine " + _dXp.ToString("0.#", ci)
                         + " (surowe, przed nauka) dla " + _dSurgeon + ", rannych jencow przed/po " + Math.Max(0, before) + "/" + Math.Max(0, now)
                         + like + (_stumbles > 0 ? " | potkniecia " + _stumbles : ""));
            }
            _dHealed = 0; _dCalls = 0; _dXp = 0f; _dSurgeon = "-";
            ResetLike();
            _woundedAtDay = now;
        }

        private static void Stumble(string where, Exception e)
        {
            Interlocked.Increment(ref _stumbles);
            bool first;
            lock (_err) first = _err.Add(where);
            if (first) { try { Log.Error("PrisonerCare." + where, e); } catch { } }
        }
    }
}
