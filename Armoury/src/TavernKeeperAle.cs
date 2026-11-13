using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 474-P3 (Jeff 13.11, projekt docs/PROJEKT-WSIE-POBOCZNE-KARCZMY-2026-11-13.md pkt 2 "Plotka u barmana za kufel"): w karczmie 3D miasta gracz mowi do karczmarza
    /// gry "Pour me an ale. What's the news?". To jest TEN SAM kufel co opcja menu karczmy "Buy a tankard of ale and listen to the gossip" (Rumours.Drink):
    ///  - stan i powod szarej linii - Rumours.KeeperAleState (wspolne cialo z warunkiem menu, DrinkState): ta sama cena (1/10 beczki piwa w tym miescie), ta sama
    ///    pula plotek miejsca (raz na godzine), ta sama ksiega uslyszanych (klucz = StringId miasta) - plotka uslyszana w menu nie wraca u karczmarza i odwrotnie;
    ///  - zaplata, pijanstwo, XP Uroku, okno plotek i linia logu "Kufle (m98): kufel w ..." - Rumours.KeeperAle -> DrinkConsequence, wolane PO zamknieciu rozmowy
    ///    (ConversationManager.ConversationEndOneShot - okno plotek nie wchodzi na okno rozmowy). Nic nie liczy sie podwojnie: jedna linia rozmowy = jeden kufel.
    /// Dowod (dekompilacja SandBox.dll 1.4.8, TavernEmployeesCampaignBehavior.AddDialogs): karczmarz wchodzi w "tavernkeeper_talk" ("Good day... How can I help you?"),
    /// opcje gracza z priorytetem 100, "tavernkeeper_pretalk" wraca do "tavernkeeper_talk"; tylko SandBox.dll uzywa tego tokenu (grep po Modules).
    /// Warunek: klucz InnKeeperAleLine + plotki karczm (InnGossip) + kufel (InnGossipNeedsDrink) + karczmy miast (TownInns) - bez kufla (klucz NIE) linii nie ma,
    /// bo plotki sa wtedy darmowe w menu. Klucz NIE = linii nie ma, rozmowa z karczmarzem co do linii jak przed.
    /// </summary>
    internal static class TavernKeeperAle
    {
        private const string P = "Barman (474-P3): ";
        internal static bool On
        {
            get { var s = Settings.Current; return s != null && s.InnKeeperAleLine && Rumours.On && InnDrink.On && TownInns.On; }
        }

        private static int _dShown, _dAsked, _dPoured, _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>(StringComparer.Ordinal);

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("TavernKeeperAle." + where, e); } catch { }
        }

        internal static void AddDialogs(CampaignGameStarter starter)
        {
            try
            {
                if (starter == null) return;
                starter.AddPlayerLine("arm474_tk_ale", "tavernkeeper_talk", "arm474_tk_ale_pour", "{=!}Pour me an ale. What's the news?", Cond, Asked, 105, Click);
                starter.AddDialogLine("arm474_tk_ale_pour", "arm474_tk_ale_pour", "close_window", "{=!}{ARM474_TK_POUR}", PourCond, Pour, 105);
                Log.Info(P + "linia \"Pour me an ale. What's the news?\" dodana do rozmowy z karczmarzem miasta (tavernkeeper_talk) - klucz Innkeeper Ale Line " + (On ? "TAK" : "NIE (albo brak plotek / kufla / karczm miast)") + ".");
            }
            catch (Exception e) { Stumble("AddDialogs", e); }
        }

        private static Settlement CurTown()
        {
            var st = Settlement.CurrentSettlement;
            return st != null && st.IsTown && st.Town != null ? st : null;
        }

        private static bool Cond()
        {
            try
            {
                if (!On || CurTown() == null) return false;
                var c = CharacterObject.OneToOneConversationCharacter;
                if (c == null || c.Occupation != Occupation.Tavernkeeper) return false;
                _dShown++;
                return true;
            }
            catch (Exception e) { Stumble("Cond", e); return false; }
        }

        private static bool Click(out TextObject why)
        {
            try
            {
                string tip;
                bool ok = Rumours.KeeperAleState(CurTown(), out tip);
                why = new TextObject("{=!}" + (tip ?? ""));
                return ok;
            }
            catch (Exception e) { Stumble("Click", e); why = new TextObject("{=!}Not now."); return false; }
        }

        private static void Asked() { _dAsked++; }

        /// <summary>474-P10: odpowiedz karczmarza z pliku rozmow (rodzaj "pour", region kultury miasta); klucz Tavern Talk Lines NIE - zdanie jak dotad.</summary>
        private static bool PourCond()
        {
            string t = Def;
            try
            {
                var st = CurTown();
                if (TavernLines.On && st != null)
                {
                    TavernLines.SetPlace(st.GetPosition2D, st.MapFaction);
                    t = TavernLines.Pick("pour", TavernLines.RegionOf(st.Culture != null ? st.Culture.StringId : null), Def).Replace("{INN}", st.Name.ToString());
                }
            }
            catch (Exception e) { Stumble("PourCond", e); t = Def; }
            MBTextManager.SetTextVariable("ARM474_TK_POUR", t);
            return true;
        }

        private const string Def = "There you are. Drink up, and I'll tell you what the talk has been.";

        private static void Pour()
        {
            try
            {
                var st = CurTown();
                if (st == null) return;
                string id = st.StringId;
                var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
                if (cm == null) return;
                cm.ConversationEndOneShot += delegate
                {
                    try
                    {
                        var now = CurTown();
                        if (now == null || now.StringId != id) { Log.Info(P + "kufel w " + id + " pominiety - po rozmowie gracz nie jest juz w tym miescie."); return; }
                        _dPoured++;
                        Log.Info(P + "\"Pour me an ale\" w " + now.Name + " (" + id + ") - kufel jak z menu karczmy (linia 'Kufle (m98): kufel w ...' ponizej). Warunek linii spelniony " + _dShown + "x, wybrana " + _dAsked + "x, nalane " + _dPoured + "x od startu gry.");
                        Rumours.KeeperAle(now);
                    }
                    catch (Exception e) { Stumble("Pour.End", e); }
                };
            }
            catch (Exception e) { Stumble("Pour", e); }
        }
    }
}
