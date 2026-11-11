using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace Armoury
{
    /// <summary>397 (Jeff 11.11, decyzja 3 planu: "przycisk egzekucji bedzie szary z wyjasnieniem, a po bitwie zamiast sciecia wezmiesz go do niewoli") - STRAZ FABULY:
    /// kat gracza odprawiony. Sluchacz CanHeroDie z 396 nie wystarcza na dwoch drogach gracza:
    ///  (1) ekran druzyny: PartyScreenLogic.ExecuteTroop NAJPIERW zdejmuje jenca z listy, potem wola KillCharacterAction.ApplyByExecution (niewymuszone) - weto
    ///      po kliknieciu zostawia jenca-ducha (zywy, IsPrisoner, w zadnej liscie). Dlatego odmowa PRZED kliknieciem: postfiks IsExecutable (pyta o to wiersz
    ///      ekranu, PartyVM.ExecuteExecution, ValidateCommand i prefiks ROT PartyCharacterVMPatch) + postfiks GetExecutableReasonString (podpowiedz przycisku);
    ///  (2) rozmowa po bitwie "I care not. Prepare to die!": HeroExecutionSceneNotificationData.CreateForInformingPlayer zabija droga WYMUSZONA (isForced) -
    ///      sluchacz nie jest pytany. Prefiks konsekwencji robi dokladnie to, co odpowiedz "Fine then. You are my prisoner now." (ta sama klasa, konsekwencja
    ///      conversation_talk_lord_defeat_to_lord_capture_on_consequence) i pomija oryginal.
    /// Latki wpinane w kampanii (OnSessionLaunched), raz na proces, cel przez DeclaredMethod na klasie deklarujacej; klucze Story Guard i Story Guard Executions
    /// czytane przy kazdym wywolaniu (zmiana w MCM dziala od razu). Niewpieta latka = linia BLAD, straz dalej dziala sluchaczem 396. Bez stanu w zapisie.</summary>
    internal static class StoryGuardHeadsman
    {
        private const string Tag = "StoryGuard: ";
        internal const string RefusedHint = "This lord still has a part to play in the story of the realm.";

        private static Harmony _harmony;
        private static bool _tried;
        private static int _wired;
        private static MethodInfo _mIsExec, _mReason, _mKill;
        private static readonly HashSet<string> _said = new HashSet<string>(StringComparer.Ordinal);   // id bohaterow z linia odmowy "ekran druzyny" w tej sesji
        private static readonly HashSet<string> _errSites = new HashSet<string>(StringComparer.Ordinal);
        private static int _stumbles;

        internal static void SetHarmony(Harmony h) { _harmony = h; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errSites.Add(where)) Log.Error("StoryGuardHeadsman." + where, e); } catch { }
        }

        private static bool On
        {
            get { var s = Settings.Current; return s != null && s.StoryGuard && s.StoryGuardExecutions; }
        }

        /// <summary>OnSessionLaunched (po StoryGuard.SessionStart): pamiec linii od zera, latki raz na proces, linia startu z liczba wpietych.</summary>
        internal static void SessionStart()
        {
            _said.Clear(); _errSites.Clear(); _stumbles = 0;
            if (!_tried && _harmony != null && Campaign.Current != null)
            {
                _tried = true;
                // wszystkie trzy metody zadeklarowane w podanych klasach (DeclaredMethod - pulapka 15.10 nie dotyczy)
                _mIsExec = Wire("PartyScreenLogic.IsExecutable", () => AccessTools.DeclaredMethod(typeof(PartyScreenLogic), "IsExecutable",
                    new[] { typeof(PartyScreenLogic.TroopType), typeof(CharacterObject), typeof(PartyScreenLogic.PartyRosterSide) }), null, nameof(IsExecutablePostfix));
                _mReason = Wire("PartyScreenLogic.GetExecutableReasonString", () => AccessTools.DeclaredMethod(typeof(PartyScreenLogic), "GetExecutableReasonString",
                    new[] { typeof(CharacterObject), typeof(bool) }), null, nameof(ReasonPostfix));
                _mKill = Wire("LordConversationsCampaignBehavior.conversation_talk_lord_defeat_to_lord_capture_and_kill_on_consequence",
                    () => AccessTools.DeclaredMethod(typeof(LordConversationsCampaignBehavior), "conversation_talk_lord_defeat_to_lord_capture_and_kill_on_consequence", Type.EmptyTypes),
                    nameof(CaptureAndKillPrefix), null);
            }
            var s = Settings.Current;
            Log.Info(Tag + "kat gracza (397) - latki " + _wired + "/3" + (_wired < 3 ? " BLAD (brak: " + Missing() + "; straz dalej dziala sluchaczem CanHeroDie)" : "")
                     + " | klucze: Story Guard " + (s != null && s.StoryGuard ? "TAK" : "NIE") + ", Story Guard Executions " + (s != null && s.StoryGuardExecutions ? "TAK" : "NIE")
                     + " | poza zasiegiem (tylko linia UTRATA): egzekucja ROT ze smoka/wilka (CustomHeroExecution, wymuszona), sciecie jencow rodu buntownikow przez gre, "
                     + "DestroyClan/Lost, ROT 'send to the Wall', zamiana jenca w Bialego Wedrowca.");
        }

        private static string Missing()
        {
            string m = "";
            if (_mIsExec == null) m += "IsExecutable ";
            if (_mReason == null) m += "GetExecutableReasonString ";
            if (_mKill == null) m += "capture_and_kill_on_consequence ";
            return m.Trim();
        }

        /// <summary>Zwraca MethodInfo tylko, gdy latka weszla (null = brak celu albo blad Harmony).</summary>
        private static MethodInfo Wire(string label, Func<MethodInfo> find, string pre, string post)
        {
            try
            {
                var m = find();
                if (m == null) { Log.Info(Tag + "BLAD latki 397: nie ma metody " + label + "."); return null; }
                _harmony.Patch(m, prefix: pre != null ? new HarmonyMethod(typeof(StoryGuardHeadsman), pre) { priority = Priority.First } : null,
                                  postfix: post != null ? new HarmonyMethod(typeof(StoryGuardHeadsman), post) { priority = Priority.Last } : null);
                _wired++;
                return m;
            }
            catch (Exception e) { Log.Error("StoryGuardHeadsman.Wire " + label, e); return null; }
        }

        /// <summary>Ekran druzyny: chroniony jeniec nie jest "do sciecia" (przycisk wyszarzony, polecenie egzekucji nie przechodzi ValidateCommand).</summary>
        private static void IsExecutablePostfix(CharacterObject character, ref bool __result)
        {
            try
            {
                if (!__result || character == null || !character.IsHero || !On) return;
                var h = character.HeroObject;
                if (h == null || !StoryGuard.Needed(h)) return;
                __result = false;
                if (h.StringId != null && _said.Add(h.StringId))
                    Log.Info(Tag + "EGZEKUCJA GRACZA odmowiona " + (h.Name != null ? h.Name.ToString() : "?") + " (" + h.StringId + ") (ekran druzyny) - jeszcze przed "
                             + (StoryGuard.Why(h) ?? "?") + ".");
            }
            catch (Exception e) { Stumble("IsExecutable", e); }
        }

        /// <summary>Podpowiedz wyszarzonego przycisku egzekucji: dla chronionego jenca nasz powod zamiast "cannot execute hero".</summary>
        private static void ReasonPostfix(CharacterObject character, bool isExecutable, ref string __result)
        {
            try
            {
                if (isExecutable || character == null || !character.IsHero || !On) return;
                var h = character.HeroObject;
                if (h == null || !h.IsPrisoner || !StoryGuard.Needed(h)) return;
                __result = RefusedHint;
            }
            catch (Exception e) { Stumble("Reason", e); }
        }

        /// <summary>Rozmowa po bitwie, "I care not. Prepare to die!": chroniony idzie do niewoli jak po odpowiedzi "Fine then. You are my prisoner now."
        /// (LordConversationsCampaignBehavior.conversation_talk_lord_defeat_to_lord_capture_on_consequence), oryginal (scena egzekucji, smierc wymuszona) pominiety.</summary>
        private static bool CaptureAndKillPrefix()
        {
            Hero h;
            try
            {
                if (!On || Campaign.Current == null) return true;
                h = Hero.OneToOneConversationHero;
                if (h == null || !StoryGuard.Needed(h)) return true;
            }
            catch (Exception e) { Stumble("CaptureAndKill.ocena", e); return true; }
            try
            {
                string name = h.Name != null ? h.Name.ToString() : "?";
                Log.Info(Tag + "EGZEKUCJA GRACZA odmowiona " + name + " (" + h.StringId + ") (rozmowa po bitwie) - niewola zamiast sciecia; jeszcze przed " + (StoryGuard.Why(h) ?? "?") + ".");
                Campaign.Current.CurrentConversationContext = ConversationContext.Default;
                TakePrisonerAction.Apply(Campaign.Current.MainParty.Party, h);
                Log.Player(name + "'s part in the tale is not yet played out. Fate stays your hand - you take " + name + " prisoner instead.");
            }
            catch (Exception e) { Stumble("CaptureAndKill.niewola", e); }
            return false;
        }
    }
}
