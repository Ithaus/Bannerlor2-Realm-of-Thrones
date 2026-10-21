using System;
using System.Collections.Generic;
using System.Xml;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// m101-prolog E2a - ROBERT I NED ZYJA (projekt pkt 4 E2). W ROT_heroes.xml oba sa alive="false" (ROTlord_506 Robert Baratheon, ROTlord_512 Eddard Stark).
    /// Hero.Deserialize ustawia z tego stan Dead i losowe daty urodzin/smierci, a dopiero potem wpisuje bohatera do rodu (Clan setter -> lista martwych rodu), wiec
    /// "ozywianie" po fakcie musialoby przepisywac listy rodu i menedzera obiektow. Zamiast tego w nowej kampanii z prologiem (Active, klucz PrologueKings) prefiks
    /// Hero.Deserialize na czas tego jednego wywolania zmienia atrybut alive tych dwoch wezlow XML na "true" (finalizer przywraca "false" - dokument XML zostaje jak byl
    /// dla nastepnej kampanii w procesie). Gra wczytuje ich wtedy jak kazdego zywego lorda z XML: stan NotSpawned, urodziny z wieku (Robert 50, Ned 42),
    /// zony z XML (Cersei lord_4_2, Catelyn ROTlord_513), listy zywych (InitializeOnNewGame), rozstawienie przez HeroSpawnCampaignBehavior.
    /// OnNewGameCreated (PRZED rozstawieniem lordow i druzyn w OnNewGameCreatedPartialFollowUp): Robert glowa rodu clan_nord_1 (Baratheonowie z Krolewskiej Przystani;
    /// nazwa rodu "Baratheon, Joffrey" -> "Baratheon, Robert", oryginal w zapisie, wraca przy smierci krola) i przez to wladca krolestwa korony vlandia; Ned glowa
    /// Starkow clan_battania_1 i wladca Polnocy battania. Clan.SetLeader (bez przelewu zlota i zmian relacji ChangeClanLeaderAction - to start swiata, nie sukcesja);
    /// dziedzice: Joffrey (lord_7_1) i Robb (lord_5_1) - najstarsi synowie z XML, wybierze ich gra przy smierci ojca.
    /// W prologu Robert i Ned nie umieraja "sami" (CanHeroDie = false dla zwyklych smierci: starosc, bitwa, egzekucja nie wymuszona) - smierc krola i egzekucja Neda
    /// to wymuszone smierci z E2b. Klasyczny start: latka nawet sie nie wpina (InstallRest leniwie przy pierwszej kampanii z prologiem).
    /// </summary>
    internal static partial class Prologue
    {
        internal const string RobertId = "ROTlord_506", NedId = "ROTlord_512", JoffreyId = "lord_7_1", RobbId = "lord_5_1";
        internal const string CrownClanId = "clan_nord_1", StarkClanId = "clan_battania_1", NorthId = "battania";

        private static bool KingsKey { get { var s = S; return s != null && s.PrologueKings; } }

        // stan (zapis): Robert i Ned ozywieni w tej kampanii, oryginalna nazwa rodu korony
        private static bool _kingsAlive;
        private static string _crownClanName = "";
        private static int _revivedNow;

        private static void ResetKings()
        {
            _kingsAlive = false; _crownClanName = ""; _revivedNow = 0;
        }

        private static string PackKings()
        {
            return "ka=" + (_kingsAlive ? "1" : "0") + ";cn=" + Clean(_crownClanName) + ";";
        }

        private static void UnpackKings(Dictionary<string, string> map)
        {
            ResetKings();
            string v;
            _kingsAlive = map.TryGetValue("ka", out v) && v == "1";
            _crownClanName = map.TryGetValue("cn", out v) ? Unesc(v) : "";
        }

        internal static Hero H(string id)
        {
            try { var h = Hero.Find(id); if (h != null) return h; } catch { }
            try { return Hero.FindFirst(x => x != null && x.StringId == id); } catch { return null; }
        }

        internal static Clan C(string id)
        {
            foreach (var c in Clan.All) if (c != null && c.StringId == id) return c;
            return null;
        }

        private static string Who(Hero h) { return h == null ? "brak" : h.Name + " (" + h.StringId + ")"; }

        // ---------------------------------------------------------------- latka XML

        private static string ApplyKings(Harmony h)
        {
            var m = AccessTools.DeclaredMethod(typeof(Hero), "Deserialize", new[] { typeof(MBObjectManager), typeof(XmlNode) });
            if (m == null) return "BRAK (Hero.Deserialize)";
            return h.Patch(m, prefix: new HarmonyMethod(typeof(Prologue), nameof(DeserializePrefix)),
                           finalizer: new HarmonyMethod(typeof(Prologue), nameof(DeserializeFinalizer))) != null ? "TAK" : "BRAK";
        }

        /// <summary>Prefiks Hero.Deserialize: w nowej kampanii z prologiem wezel Roberta i Neda czytany jak zywy (alive "false" -> "true" na czas wywolania).</summary>
        public static void DeserializePrefix(XmlNode node, out XmlAttribute __state)
        {
            __state = null;
            try
            {
                if (!Active || !KingsKey || node == null || node.Attributes == null) return;
                var idA = node.Attributes["id"];
                string id = idA != null ? idA.Value : null;
                if (id != RobertId && id != NedId) return;
                var al = node.Attributes["alive"];
                if (al == null || al.Value != "false") return;
                al.Value = "true";
                __state = al;
                _revivedNow++;
            }
            catch (Exception e) { Stumble("DeserializePrefix", e); }
        }

        public static Exception DeserializeFinalizer(XmlAttribute __state, Exception __exception)
        {
            try { if (__state != null) __state.Value = "false"; } catch { }
            return __exception;
        }

        // ---------------------------------------------------------------- start swiata

        /// <summary>OnNewGameCreated (przed rozstawieniem lordow): Robert glowa rodu korony i krolem, Ned glowa Starkow i wladca Polnocy.</summary>
        internal static void OnNewGameCreated()
        {
            if (!Active) return;
            var robert = H(RobertId); var ned = H(NedId);
            var crownClan = C(CrownClanId); var stark = C(StarkClanId);
            string r = "-", n = "-";
            if (robert != null && robert.IsAlive && crownClan != null && robert.Clan == crownClan)
            {
                try
                {
                    var oldLeader = crownClan.Leader;
                    if (string.IsNullOrEmpty(_crownClanName) && crownClan.Name != null) _crownClanName = crownClan.Name.Value ?? crownClan.Name.ToString();
                    crownClan.SetLeader(robert);
                    var nm = new TextObject("{=!}Baratheon, Robert");
                    crownClan.ChangeClanName(nm, nm);
                    var k = crownClan.Kingdom;
                    r = "Robert " + Who(robert) + " wiek " + robert.Age.ToString("0", Inv) + ", zona " + Who(robert.Spouse) + " - glowa rodu " + crownClan.StringId
                        + " (byl " + Who(oldLeader) + ")" + (k != null ? ", krolestwo " + k.StringId + " wladca " + Who(k.Leader) + (k.Leader == robert ? " TAK" : " NIE") : ", bez krolestwa");
                }
                catch (Exception e) { Stumble("OnNewGameCreated.Robert", e); r = "BLAD"; }
            }
            else r = "Robert nie zyje albo poza rodem " + CrownClanId + " (" + Who(robert) + ", rod " + (robert != null && robert.Clan != null ? robert.Clan.StringId : "-") + ")";
            if (ned != null && ned.IsAlive && stark != null && ned.Clan == stark)
            {
                try
                {
                    var oldLeader = stark.Leader;
                    stark.SetLeader(ned);
                    var k = stark.Kingdom;
                    n = "Ned " + Who(ned) + " wiek " + ned.Age.ToString("0", Inv) + ", zona " + Who(ned.Spouse) + " - glowa rodu " + stark.StringId
                        + " (byl " + Who(oldLeader) + ")" + (k != null ? ", krolestwo " + k.StringId + " wladca " + Who(k.Leader) + (k.Leader == ned ? " TAK" : " NIE") : ", bez krolestwa");
                }
                catch (Exception e) { Stumble("OnNewGameCreated.Ned", e); n = "BLAD"; }
            }
            else n = "Ned nie zyje albo poza rodem " + StarkClanId + " (" + Who(ned) + ")";
            _kingsAlive = robert != null && robert.IsAlive && ned != null && ned.IsAlive;
            Log.Info(Tag + "Robert i Ned (XML ozywionych " + _revivedNow + "/2, klucz Prologue Kings " + (KingsKey ? "TAK" : "NIE") + "): " + r + " | " + n + ".");
        }

        /// <summary>CanHeroDie: w prologu Robert (do smierci krola) i Ned (do egzekucji) nie umieraja zwykla smiercia; wymuszone smierci E2b ida obok (isForced).
        /// Recenzja R3: po smierci krola (_kingFallen) i po NedEvent (_nedDone) ochrony juz nie ma - egzekucja Neda w bitwie stawia tylko znak smierci, a smierc po
        /// bitwie (KillCharacterAction.ApplyByDeathMark) idzie bez wymuszenia i nasze weto zostawiloby Neda przy zyciu.</summary>
        internal static void CanHeroDie(Hero hero, KillCharacterAction.KillCharacterActionDetail detail, ref bool result)
        {
            try
            {
                if (!result || !Active || !_kingsAlive || hero == null) return;
                if (hero.StringId == RobertId && !_kingFallen && KingProtected()) result = false;
                else if (hero.StringId == NedId && !_nedDone && NedProtected()) result = false;
            }
            catch (Exception e) { Stumble("CanHeroDie", e); }
        }

        private static bool KingProtected() { return PrologueDay() < KingFallsDay + 30; }
        private static bool NedProtected() { return PrologueDay() < PrologueDays + 30; }

        /// <summary>Linia doby: krol, korona, Ned, Polnoc.</summary>
        private static string DailyKings()
        {
            if (!_kingsAlive && string.IsNullOrEmpty(_crownClanName)) return "Robert i Ned: nie ozywieni";
            var robert = H(RobertId); var ned = H(NedId);
            var crown = K(Crown); var north = K(NorthId);
            string nedWhere = ned == null ? "-" : ned.IsPrisoner && ned.PartyBelongedToAsPrisoner != null
                ? "jeniec " + (ned.PartyBelongedToAsPrisoner.IsSettlement ? ned.PartyBelongedToAsPrisoner.Settlement.StringId : ned.PartyBelongedToAsPrisoner.Id)
                : "wolny";
            return "krol Robert " + (robert != null && robert.IsAlive ? "ZYJE" : "MARTWY")
                   + " | korona: wladca " + Who(crown != null ? crown.Leader : null)
                   + " | Ned " + (ned != null && ned.IsAlive ? "ZYJE" : "MARTWY") + " (" + nedWhere + ")"
                   + " | Polnoc: wladca " + Who(north != null ? north.Leader : null);
        }
    }
}
