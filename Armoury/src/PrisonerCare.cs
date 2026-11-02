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

        internal static bool On { get { var s = Settings.Current; return s != null && s.PrisonerCareXp; } }

        internal static void RegisterEvents(CampaignBehaviorBase b)
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(b, delegate (CampaignGameStarter s) { try { SessionStart(); } catch (Exception e) { Stumble("SessionStart", e); } });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(b, delegate { try { Daily(); } catch (Exception e) { Stumble("Daily", e); } });
        }

        private static void SessionStart()
        {
            // stan doby czyszczony przy kazdym starcie sesji (nowa kampania / wczytanie)
            _dHealed = 0; _dCalls = 0; _dXp = 0f; _dSurgeon = "-"; _woundedAtDay = WoundedNow();
            EnsureHooks();
            var s = Settings.Current;
            Log.Info(P + "start sesji | klucz Prisoner Care Xp " + (On ? "tak" : "nie") + ", Prisoner Care Xp Percent " + (s != null ? s.PrisonerCareXpPercent : 0)
                     + " | latki " + _wired + " | rannych jencow gracza " + Math.Max(0, _woundedAtDay) + " | jency-bohaterowie bez XP (gra nie ma haczyka dla jenca).");
        }

        private static void EnsureHooks()
        {
            if (_hooked) return;
            _hooked = true;
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
                if (partyBase == null || partyBase != PartyBase.MainParty || !On) return;
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
                if (pct <= 0) return;
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
            if (On && (_dHealed > 0 || now > 0 || before > 0))
            {
                Log.Info(P + "wyleczonych " + _dHealed + " (wywolan " + _dCalls + "), XP Medicine " + _dXp.ToString("0.#", CultureInfo.InvariantCulture)
                         + " (surowe, przed nauka) dla " + _dSurgeon + ", rannych jencow przed/po " + Math.Max(0, before) + "/" + Math.Max(0, now)
                         + (_stumbles > 0 ? " | potkniecia " + _stumbles : ""));
            }
            _dHealed = 0; _dCalls = 0; _dXp = 0f; _dSurgeon = "-";
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
