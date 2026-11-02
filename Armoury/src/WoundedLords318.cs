using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 318a (Jeff 02.11 "wygralem ja i nie mam w prisoner dowodcow, z jednym stoczylem pojedynek i wygralem wiec tez powinien byc w jencach";
    /// rozp317.md "# jency" INV + CRIT "Poprawiona rekomendacja"). Klucz DefeatedLordsStayWounded.
    ///
    /// Przyczyna (gra 1.4.8, dekompilacja dec306b/cs_full): PlayerEncounter.DoApplyMapEventResults (PlayerEncounter.cs:1520-1523; tak samo ROT patch49
    /// przy zaciagu - EnlistmentPatches.cs:871-874) wola NAJPIERW OnPlayerBattleEnd -> PartyHealCampaignBehavior.OnBattleEndCheckPerkEffects (leczy
    /// perkami Medicine KAZDA partie bitwy, takze pokonane: Preventive Medicine 30% utraconego HP, Walk It Off +10 atakujacym, Naval Resilience 30%),
    /// a DOPIERO POTEM CalculateAndCommitMapEventResults -> CaptureDefeatedPartyMembers (MapEvent.cs:1955-2010), ktore wybiera szanse wedlug
    /// IsWounded (HitPoints <= WoundedHealthLimit = 20): ranny 1.0, zdrowy 0.25 (DefaultBattleRewardModel.cs:231-255); nietrafiony - MakeHeroFugitiveAction.
    /// Ranny lord (HP 1 po MakeWounded - takze przegrany pojedynku ROT) po leczeniu PM / Resilience mial > 20 HP i w 75% "zbiegl". W bitwach AI to samo
    /// leczenie idzie z MapEventEnded - po pojmaniu (stad "wodz: niewola" we wszystkich bitwach AI z lordem w 312f).
    ///
    /// Zmiana: prefiks (Priority.First) + finalizer na PartyHealCampaignBehavior.OnBattleEndCheckPerkEffects (DeclaredMethod - prywatna, klasa deklarujaca):
    /// tylko bitwa gracza z wynikiem, ktora gracz WYGRAL (me.IsPlayerMapEvent && HasWinner && WinningSide == PartyBase.MainParty.Side) - znacznik
    /// [ThreadStatic] i spis bohaterow strony pokonanej (bez Hero.MainHero). Prefiks na Hero.Heal: przy znaczniku pomija leczenie bohatera ze spisu,
    /// ktory JEST ranny (HitPoints <= WoundedHealthLimit) - HP sie nie zmienia, zadnych zdarzen gry (OnHeroWounded) ani przeskokow rostera; zdrowych
    /// lordow, zwyciezcow, gracza, przegranej gracza i bitew AI nie rusza. Finalizer zawsze gasi znacznik (takze po wyjatku).
    /// Pomiar (zawsze, bez wzgledu na klucz): finalizer MapEvent.CaptureDefeatedPartyMembers zapisuje los kazdego bohatera po pojmaniu, MapEventEnded
    /// pisze jedna linie "Ranni lordowie (318a): bitwa gracza ..." (HP przed/po, perki PM/WIO/Res, leczenie pominiete tak/nie, los po pojmaniu i na koncu).
    /// Potkniecia liczone (pierwszy blad miejsca do logu) - bez globalnego wylacznika. Latki raz na proces, sprawdzone Harmony.GetPatchInfo.
    /// </summary>
    internal static class WoundedLords318
    {
        private const string HarmonyId = "com.jeff.armoury.woundedlords318";
        internal const string P = "Ranni lordowie (318a): ";

        private sealed class HeroRec
        {
            public Hero H; public string Name, Party; public int Hp0, Max, Lim, Asked, Hp1 = -1; public bool Wnd0, PM, WIO, Res, Blocked;
            public string CapFate = "-";
        }

        private sealed class Win
        {
            public MapEvent Me; public bool Won, Block, CapSeen, CapRetreat; public int Day;
            public readonly List<HeroRec> L = new List<HeroRec>();
            public readonly Dictionary<Hero, HeroRec> By = new Dictionary<Hero, HeroRec>();
        }

        [ThreadStatic] private static bool _inHeal;
        [ThreadStatic] private static Win _cur;
        private static Win _last;                                  // ostatnia bitwa gracza z bohaterami pokonanych (jedna naraz)
        private static int _lines, _blocked, _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static bool _hookTried;
        private static string _hookNote = "-";
        private static PerkObject _resilience; private static bool _resTried;

        internal static void Reset() { _inHeal = false; _cur = null; _last = null; _lines = 0; _blocked = 0; _stumbles = 0; _errSites.Clear(); }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errSites.Add(where)) Log.Error("WoundedLords318." + where, e); } catch { }
        }

        // ------------------------------------------------------------ latki (raz na proces, w kampanii)
        internal static void EnsureHooks()
        {
            if (_hookTried) { Log.Info(P + "start sesji - latki " + _hookNote + ", klucz Defeated Lords Stay Wounded " + (Settings.Current.DefeatedLordsStayWounded ? "wlaczony" : "WYLACZONY") + "."); return; }
            _hookTried = true;
            var h = new Harmony(HarmonyId);
            var notes = new List<string>(3);
            notes.Add("leczenie po bitwie " + Wire(h, AccessTools.DeclaredMethod(typeof(PartyHealCampaignBehavior), "OnBattleEndCheckPerkEffects", new[] { typeof(MapEvent) }),
                                                   "PartyHealCampaignBehavior.OnBattleEndCheckPerkEffects", nameof(WinPrefix), nameof(WinFinalizer)));
            notes.Add("Hero.Heal " + Wire(h, AccessTools.DeclaredMethod(typeof(Hero), "Heal", new[] { typeof(int), typeof(bool) }), "Hero.Heal", nameof(HealPrefix), null));
            notes.Add("pojmanie " + Wire(h, AccessTools.DeclaredMethod(typeof(MapEvent), "CaptureDefeatedPartyMembers"), "MapEvent.CaptureDefeatedPartyMembers", null, nameof(CaptureFinalizer)));
            _hookNote = string.Join("; ", notes);
            Log.Info(P + "start sesji - latki " + _hookNote + ", klucz Defeated Lords Stay Wounded " + (Settings.Current.DefeatedLordsStayWounded ? "wlaczony" : "WYLACZONY") + ".");
        }

        private static string Wire(Harmony h, System.Reflection.MethodInfo m, string name, string prefix, string finalizer)
        {
            try
            {
                if (m == null) return "BRAK (" + name + ")";
                var pre = prefix == null ? null : new HarmonyMethod(typeof(WoundedLords318), prefix) { priority = Priority.First };
                var fin = finalizer == null ? null : new HarmonyMethod(typeof(WoundedLords318), finalizer) { priority = Priority.Last };
                h.Patch(m, prefix: pre, finalizer: fin);
                var info = Harmony.GetPatchInfo(m);
                bool okP = prefix == null, okF = finalizer == null;
                int others = 0;
                if (info != null)
                {
                    foreach (var p in info.Prefixes) { if (p == null) continue; if (p.owner == HarmonyId) okP = true; else others++; }
                    foreach (var p in info.Finalizers) { if (p == null) continue; if (p.owner == HarmonyId) okF = true; else others++; }
                    foreach (var p in info.Postfixes) if (p != null && p.owner != HarmonyId) others++;
                    foreach (var p in info.Transpilers) if (p != null && p.owner != HarmonyId) others++;
                }
                return (okP && okF ? "wpieta" : "BRAK (latka nie weszla - slad w BKROTLog.txt)") + (others > 0 ? " (cudzych latek " + others + ")" : "");
            }
            catch (Exception e) { Log.Error("WoundedLords318.Wire(" + name + ")", e); return "BRAK (" + e.GetType().Name + ")"; }
        }

        private static bool HasRes(Hero h)
        {
            if (!_resTried)
            {
                _resTried = true;
                try { _resilience = MBObjectManager.Instance?.GetObject<PerkObject>("Resilience"); } catch { _resilience = null; }
            }
            return _resilience != null && h.GetPerkValue(_resilience);
        }

        // ------------------------------------------------------------ okno leczenia po bitwie gracza
        public static void WinPrefix(MapEvent __0)
        {
            _inHeal = false; _cur = null;
            try
            {
                var me = __0;
                if (me == null || !me.IsPlayerMapEvent || !me.HasWinner) return;
                var mainSide = PartyBase.MainParty != null ? PartyBase.MainParty.Side : BattleSideEnum.None;
                var w = new Win { Me = me, Won = mainSide != BattleSideEnum.None && me.WinningSide == mainSide, Day = (int)CampaignTime.Now.ToDays };
                w.Block = w.Won && Settings.Current.DefeatedLordsStayWounded;
                var side = me.GetMapEventSide(me.DefeatedSide);
                if (side == null) return;
                foreach (var mep in side.Parties)
                {
                    try
                    {
                        var pb = mep?.Party;
                        if (pb == null || pb.MemberRoster.TotalHeroes <= 0) continue;
                        foreach (TroopRosterElement el in pb.MemberRoster.GetTroopRoster())
                        {
                            var c = el.Character;
                            if (c == null || !c.IsHero || c.HeroObject == null || c.HeroObject == Hero.MainHero) continue;
                            var hero = c.HeroObject;
                            if (w.By.ContainsKey(hero)) continue;
                            var r = new HeroRec
                            {
                                H = hero, Name = hero.Name?.ToString() ?? hero.StringId, Party = pb.Name?.ToString() ?? "?",
                                Hp0 = hero.HitPoints, Max = hero.MaxHitPoints, Lim = hero.WoundedHealthLimit
                            };
                            r.Wnd0 = r.Hp0 <= r.Lim;
                            try { r.PM = hero.GetPerkValue(DefaultPerks.Medicine.PreventiveMedicine); r.WIO = hero.GetPerkValue(DefaultPerks.Medicine.WalkItOff); r.Res = HasRes(hero); }
                            catch (Exception e) { Stumble("WinPrefix(perki)", e); }
                            w.L.Add(r); w.By[hero] = r;
                        }
                    }
                    catch (Exception e) { Stumble("WinPrefix(partia)", e); }
                }
                if (w.L.Count == 0) return;
                _last = w; _cur = w; _inHeal = true;
            }
            catch (Exception e) { _inHeal = false; _cur = null; Stumble("WinPrefix", e); }
        }

        /// <summary>Finalizer okna - zawsze gasi znacznik, zapisuje HP po leczeniu; wyjatku gry nie zmienia.</summary>
        public static void WinFinalizer()
        {
            var w = _cur;
            _inHeal = false; _cur = null;
            if (w == null) return;
            try { foreach (var r in w.L) { try { r.Hp1 = r.H.HitPoints; } catch (Exception e) { Stumble("WinFinalizer(bohater)", e); } } }
            catch (Exception e) { Stumble("WinFinalizer", e); }
        }

        /// <summary>Prefiks Hero.Heal (goracy - pierwsze sprawdzenie to znacznik [ThreadStatic]). false = leczenie pominiete.</summary>
        public static bool HealPrefix(Hero __instance, int __0)
        {
            if (!_inHeal) return true;
            try
            {
                var w = _cur;
                HeroRec r;
                if (w == null || __instance == null || !w.By.TryGetValue(__instance, out r)) return true;
                r.Asked += __0;
                if (!w.Block || __instance == Hero.MainHero) return true;
                if (__instance.HitPoints > __instance.WoundedHealthLimit) return true;
                r.Blocked = true; _blocked++;
                return false;
            }
            catch (Exception e) { Stumble("HealPrefix", e); return true; }
        }

        // ------------------------------------------------------------ los po pojmaniu
        public static void CaptureFinalizer(MapEvent __instance)
        {
            try
            {
                var w = _last;
                if (w == null || !ReferenceEquals(w.Me, __instance) || w.CapSeen) return;
                w.CapSeen = true;
                w.CapRetreat = __instance.RetreatingSide != BattleSideEnum.None;   // gra wychodzi wtedy od razu (MapEvent.cs:1957)
                foreach (var r in w.L)
                {
                    try { r.CapFate = FateNow(r.H, true); } catch (Exception e) { r.CapFate = "?"; Stumble("CaptureFinalizer(bohater)", e); }
                }
            }
            catch (Exception e) { Stumble("CaptureFinalizer", e); }
        }

        private static string FateNow(Hero h, bool atCapture)
        {
            if (h.IsDead || h.DeathMark != KillCharacterAction.KillCharacterActionDetail.None) return "zginal";
            if (h.IsPrisoner)
            {
                var pp = h.PartyBelongedToAsPrisoner;
                if (pp == PartyBase.MainParty) return "jeniec gracza";
                return "jeniec " + (pp?.Name?.ToString() ?? "?");
            }
            if (h.IsFugitive) return "zbiegl";
            if (atCapture)
            {
                // gracz: CaptureDefeatedPartyMembers kladzie bohatera do rostera lupu (OwnerParty null) i zdejmuje go z druzyny - jencem zostaje w DoCaptureHeroes
                var pb = h.PartyBelongedTo?.Party;
                if (pb == null || pb.MemberRoster.FindIndexOfTroop(h.CharacterObject) < 0) return "do lupu gracza";
                return "zostal w druzynie";
            }
            return "wolny";
        }

        internal static void OnMapEventEnded(MapEvent me)
        {
            try
            {
                var w = _last;
                if (w == null || me == null || !ReferenceEquals(w.Me, me)) return;
                _last = null;
                var sb = new StringBuilder(160 + 160 * w.L.Count);
                sb.Append(P).Append("bitwa gracza dzien ").Append(w.Day).Append(" | gracz ").Append(w.Won ? "wygral" : "przegral")
                  .Append(" | klucz ").Append(Settings.Current.DefeatedLordsStayWounded ? "wlaczony" : "WYLACZONY")
                  .Append(" | pojmanie ").Append(!w.CapSeen ? "NIE" : w.CapRetreat ? "NIE (odwrot)" : "TAK")
                  .Append(" | bohaterowie pokonanych ").Append(w.L.Count).Append(':');
                int blocked = 0, prisoners = 0, fled = 0;
                foreach (var r in w.L)
                {
                    string end;
                    try { end = FateNow(r.H, false); } catch (Exception e) { end = "?"; Stumble("OnMapEventEnded(bohater)", e); }
                    if (r.Blocked) blocked++;
                    if (end == "jeniec gracza") prisoners++;
                    if (end == "zbiegl") fled++;
                    sb.Append(" [").Append(r.Name).Append(" (").Append(r.Party).Append(") HP ").Append(r.Hp0).Append('/').Append(r.Max)
                      .Append(r.Wnd0 ? " ranny" : " zdrowy").Append(" -> ").Append(r.Hp1 < 0 ? "?" : r.Hp1.ToString())
                      .Append(", perki PM ").Append(r.PM ? "tak" : "nie").Append(" WIO ").Append(r.WIO ? "tak" : "nie").Append(" Res ").Append(r.Res ? "tak" : "nie")
                      .Append(", leczenie ").Append(r.Asked).Append(" pominiete ").Append(r.Blocked ? "TAK" : "nie")
                      .Append(", po pojmaniu: ").Append(r.CapFate).Append(", na koniec: ").Append(end).Append(']');
                }
                sb.Append(" | pominiete ").Append(blocked).Append(", jency gracza ").Append(prisoners).Append(", zbiegli ").Append(fled)
                  .Append(" | razem pominietych w sesji ").Append(_blocked).Append(", potkniecia ").Append(_stumbles).Append('.');
                _lines++;
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("OnMapEventEnded", e); }
        }
    }
}
