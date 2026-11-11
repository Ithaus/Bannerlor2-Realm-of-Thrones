using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>403 (Jeff 11.11 "rob 4" - rany i blizny bohaterow; PLAN-396 commit 403, rozdz. 0 pkt 5) - JEDNO API SMIERCI I OKALECZENIA BOHATERA Z NASZEGO KODU.
    /// Uzywaja go rany (403: przesilenie w HeroWounds), od 404 loch, od 409 wyrok gracza. Wspolne bramki w jednym miejscu.
    ///
    /// TryKill - kolejnosc: zywy i bez znaku smierci -> StoryGuard.MayDie (straz fabuly + CanHeroDie gry i cudzych sluchaczy) -> dziecko -> (gdy to nie wybor
    /// gracza) wladca przy Hero Fate Rulers Can Die NIE, ostatni dorosly zywy lord rodu przy Hero Fate Spare Last Of House TAK (warunek gry z
    /// KillCharacterAction.ApplyInternal: bez innego doroslego lorda rod ginie - DestroyClanAction.ApplyByClanLeaderDeath), roczny sufit LordToll ->
    /// odroczenie (Busy: partia bohatera albo pojmujacego w bitwie, partia bohatera oblega, misja, spotkanie POLOWE gracza, rozmowa gracza z nim, menu lochu
    /// tej osady; 403-p: sam pobyt gracza w osadzie ani oblezona osada nie odraczaja - gra zabija tam ze starosci tak samo) ->
    /// wykonanie akcja gry (ApplyByWounds / ApplyByMurder / ApplyByExecution). NIGDY przez znak smierci: w warunkach, w ktorych gra postawilaby znak
    /// (PartyBelongedTo w MapEvent / SiegeEvent), odraczamy - wiec akcja gry zabija od razu albo wcale. Po smierci gra sama: zloto do glowy rodu, partia -
    /// nowy wodz albo rozwiazanie, jeniec znika z listy, armia rozwiazana, wybor wladcy (ale wladce domyslnie oszczedzamy).
    ///
    /// TryMaim - trwale okaleczenie (tabela raportu ran): Scarred face 40 (Charm -10) / Lame leg 25 (Athletics -60, Riding -20, Endurance -1) /
    /// Lost an eye 20 (Bow -40, Crossbow -40, Throwing -30, Scouting -20) / Maimed hand 15 (TwoHanded -60, Bow -60, Polearm -30, Vigor -1); liczby = wagi.
    /// Jeden typ raz, najwyzej 2 na bohatera; chroniony fabula nigdy (StoryGuard.MayBeMaimed). Umiejetnosc HeroDeveloper.SetInitialSkillLevel (dol 0),
    /// atrybut HeroDeveloper.RemoveAttribute (dol 1). FAKTYCZNE delty w zapisie arm_heromaims_v1 "id|typ|dzien|skill:delta,..|attr:delta" (da sie cofnac
    /// recznie); perki zostaja; wyglad bez zmian. Wpisy zostaja po smierci bohatera (encyklopedia). Stary zapis = pusta ksiega.
    /// Statyki: Reset w konstruktorze ArmouryBehavior i w OnGameEnd. Potkniecia liczone per miejsce - nic sie nie gasi.</summary>
    internal static class HeroFate
    {
        private const string Tag = "HeroFate: ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal enum Fate { Killed, Refused, Deferred }

        internal sealed class Maim
        {
            public string Id = "", Type = "", Skills = "", Attr = "";   // Skills: "Bow:-40,Crossbow:-12"; Attr: "Vigor:-1" albo ""
            public int Day;
        }

        private static readonly Dictionary<string, List<Maim>> _maims = new Dictionary<string, List<Maim>>(StringComparer.Ordinal);
        private static int _maimRows;
        private static int _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>(StringComparer.Ordinal);

        internal const string Scar = "scar", Eye = "eye", Hand = "hand", Leg = "leg";
        private static readonly string[] Types = { Scar, Leg, Eye, Hand };
        private static readonly int[] Weights = { 40, 25, 20, 15 };
        // 422: wagi typu okaleczenia wedlug czesci ciala ciosu, ktory zadal rane (kolejnosc jak Types: blizna, noga, oko, dlon)
        private static readonly int[] WeightsHead = { 60, 0, 40, 0 }, WeightsShoulder = { 40, 0, 0, 60 }, WeightsArm = { 0, 0, 0, 100 }, WeightsLeg = { 0, 100, 0, 0 };
        internal const int MaxMaims = 2;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errSites.Add(where)) Log.Error("HeroFate." + where, e); } catch { }
        }

        internal static int Stumbles { get { return _stumbles; } }
        internal static int MaimRows { get { return _maimRows; } }
        private static string Nm(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "?"; } catch { return "?"; } }

        // ---------------------------------------------------------------- cykl zycia / zapis
        internal static void Reset() { _maims.Clear(); _maimRows = 0; _stumbles = 0; _errSites.Clear(); }

        internal static string Export()
        {
            if (_maimRows == 0) return "";
            var sb = new StringBuilder(48 * _maimRows);
            foreach (var kv in _maims)
                foreach (var m in kv.Value)
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(m.Id).Append('|').Append(m.Type).Append('|').Append(m.Day.ToString(Inv)).Append('|').Append(m.Skills).Append('|').Append(m.Attr);
                }
            return sb.ToString();
        }

        internal static void Import(string text)
        {
            _maims.Clear(); _maimRows = 0;
            if (string.IsNullOrEmpty(text)) return;
            foreach (var line in text.Split('\n'))
            {
                try
                {
                    var p = line.Split('|');
                    if (p.Length < 3 || p[0].Length == 0 || Array.IndexOf(Types, p[1]) < 0) continue;
                    var m = new Maim { Id = p[0], Type = p[1], Day = int.Parse(p[2], Inv), Skills = p.Length > 3 ? p[3] : "", Attr = p.Length > 4 ? p[4] : "" };
                    Add(m);
                }
                catch (Exception e) { Stumble("Import", e); }
            }
        }

        private static void Add(Maim m)
        {
            List<Maim> l;
            if (!_maims.TryGetValue(m.Id, out l)) _maims[m.Id] = l = new List<Maim>(2);
            l.Add(m); _maimRows++;
        }

        // ---------------------------------------------------------------- bramki wspolne
        /// <summary>Wladca krolestwa (smierc = wybory krola / zmiana rodu panujacego).</summary>
        internal static bool IsRuler(Hero h)
        {
            try { return h != null && (h.IsKingdomLeader || (h.Clan != null && h.Clan.Kingdom != null && h.Clan.Kingdom.Leader == h)); }
            catch { return false; }
        }

        /// <summary>Ostatni z rodu - smierc tego bohatera konczy rod albo zostawia go bez doroslego lorda. Bohater bez rodu - NIE.
        /// 403-p: GLOWA rodu liczona tak, jak gra wybiera nastepce (KillCharacterAction.ApplyInternal: jest inny dorosly zywy lord ORAZ
        /// Clan.GetHeirApparents() niepuste - dziedzic bez znaku smierci, nie Disabled / NotSpawned, nie wedrowiec; inaczej glowa zostaje i
        /// DestroyClanAction.ApplyByClanLeaderDeath usuwa caly rod). Pozostali: brak innego doroslego lorda zdolnego dziedziczyc.</summary>
        internal static bool LastOfHouse(Hero h)
        {
            try
            {
                var c = h != null ? h.Clan : null;
                if (c == null) return false;
                bool other = false;
                foreach (var x in c.Heroes)
                    if (x != null && x != h && x.IsAlive && !x.IsChild && x.IsLord && x.DeathMark == KillCharacterAction.KillCharacterActionDetail.None
                        && !x.IsDisabled && !x.IsNotSpawned) { other = true; break; }
                if (!other) return true;
                if (c.Leader != h) return false;
                var heirs = c.GetHeirApparents();
                return heirs == null || heirs.Count == 0;
            }
            catch (Exception e) { Stumble("LastOfHouse", e); return true; }   // blad = oszczedzamy (ostroznie)
        }

        /// <summary>Teraz nie wolno zabijac (gra postawilaby znak smierci albo gracz wlasnie ma z tym bohaterem do czynienia) - odroczyc o dobe. what: opis (PL).</summary>
        internal static bool Busy(Hero h, out string what)
        {
            what = null;
            try
            {
                if (h == null) return false;
                try { if (TaleWorlds.MountAndBlade.Mission.Current != null) { what = "misja"; return true; } } catch { }
                var mp = h.PartyBelongedTo;
                if (mp != null)
                {
                    if (mp.MapEvent != null) { what = "bitwa"; return true; }
                    if (mp.SiegeEvent != null) { what = "oblezenie"; return true; }   // partia OBLEGA (MobileParty.SiegeEvent = BesiegerCamp) - gra postawilaby znak smierci
                }
                // 403-p: bohater w OBLEZONEJ osadzie nie odracza - gra nie stawia tam znaku smierci (warunek to tylko MapEvent / SiegeEvent partii), a loze
                // rannego obroncy trwaloby cale oblezenie
                var cap = h.PartyBelongedToAsPrisoner;
                if (cap != null && cap.MapEvent != null) { what = "bitwa pojmujacego"; return true; }
                bool playerSide = h == Hero.MainHero || (mp != null && mp == MobileParty.MainParty) || (cap != null && cap == PartyBase.MainParty);
                // 403-p: tylko spotkanie POLOWE albo bitwa. Sam pobyt gracza w miescie / zamku / wsi trzyma PlayerEncounter przez caly czas (gra zabija tam
                // towarzyszy ze starosci) - odraczanie zamrazalo przesilenie i loze gracza, ktory odpoczywal w murach
                if (playerSide)
                {
                    try { if (PlayerEncounter.Current != null && (PlayerEncounter.Battle != null || PlayerEncounter.EncounterSettlement == null)) { what = "spotkanie gracza"; return true; } }
                    catch { }
                }
                try
                {
                    var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
                    if (cm != null && cm.IsConversationInProgress && cm.OneToOneConversationHero == h) { what = "rozmowa gracza"; return true; }
                }
                catch { }
                if (cap != null && cap.IsSettlement && cap.Settlement != null && cap.Settlement == Settlement.CurrentSettlement)
                {
                    try
                    {
                        var ctx = Campaign.Current != null ? Campaign.Current.CurrentMenuContext : null;
                        string menu = ctx != null && ctx.GameMenu != null ? ctx.GameMenu.StringId : null;
                        if (menu == "town_keep_dungeon" || menu == "castle_dungeon") { what = "gracz w menu lochu"; return true; }
                    }
                    catch { }
                }
                return false;
            }
            catch (Exception e) { Stumble("Busy", e); what = "blad oceny"; return true; }   // blad = odroczenie (ostroznie)
        }

        // ---------------------------------------------------------------- smierc
        /// <summary>Jedyna droga, ktora nasz kod zabija bohatera. detail: WoundedInBattle (rany), Murdered (loch), Executed (wyrok). playerChoice: decyzja
        /// gracza - bez oszczedzania wladcy / ostatniego z rodu i bez sufitu roku. why: powod odmowy / odroczenia albo opis wykonania (PL, do logu).</summary>
        internal static Fate TryKill(Hero h, KillCharacterAction.KillCharacterActionDetail detail, string source, Hero killer, bool playerChoice, out string why)
        {
            why = null;
            try
            {
                if (h == null || Campaign.Current == null) { why = "brak bohatera"; return Fate.Refused; }
                if (!h.IsAlive) { why = "nie zyje"; return Fate.Refused; }
                if (h.DeathMark != KillCharacterAction.KillCharacterActionDetail.None) { why = "ma juz znak smierci " + h.DeathMark; return Fate.Refused; }
                if (detail != KillCharacterAction.KillCharacterActionDetail.WoundedInBattle && detail != KillCharacterAction.KillCharacterActionDetail.Murdered
                    && detail != KillCharacterAction.KillCharacterActionDetail.Executed)
                { why = "nieobslugiwana przyczyna " + detail; return Fate.Refused; }
                if (!StoryGuard.MayDie(h, detail, source, out why)) return Fate.Refused;
                if (h.IsChild) { why = "dziecko"; return Fate.Refused; }
                var s = Settings.Current;
                if (s == null) { why = "brak ustawien"; return Fate.Refused; }
                bool last = h != Hero.MainHero && LastOfHouse(h);
                // 403-p: smierc glowy bez dziedzica niszczy rod, a gra usuwa wtedy WSZYSTKICH jego zywych czlonkow (ApplyByRemove, wymuszone - omija CanHeroDie).
                // Jesli w rodzie zyje postac fabuly, odmawiamy zawsze - takze wyrokowi gracza i przy kluczu Hero Fate Spare Last Of House NIE.
                if (last && h.Clan != null && h.Clan.Leader == h)
                {
                    string who;
                    if (StoryGuard.ClanHoldsStory(h.Clan, h, out who)) { why = "straz fabuly (rod zginalby z glowa, a zyje w nim " + who + ")"; return Fate.Refused; }
                }
                if (!playerChoice)
                {
                    if (!s.HeroFateRulersCanDie && IsRuler(h)) { why = "oszczedzony (wladca)"; return Fate.Refused; }
                    if (s.HeroFateSpareLastOfHouse && last) { why = "oszczedzony (ostatni z rodu)"; return Fate.Refused; }
                    if (!LordToll.Allow(source, out why)) return Fate.Refused;
                }
                string what;
                if (Busy(h, out what)) { why = "odroczone (" + what + ")"; return Fate.Deferred; }

                bool player = h == Hero.MainHero;
                if (detail == KillCharacterAction.KillCharacterActionDetail.WoundedInBattle) KillCharacterAction.ApplyByWounds(h, true);
                else if (detail == KillCharacterAction.KillCharacterActionDetail.Murdered) KillCharacterAction.ApplyByMurder(h, killer, false);
                else KillCharacterAction.ApplyByExecution(h, killer ?? Hero.MainHero, true, false);

                if (player)
                {
                    // gra nie zabija gracza od razu: OnBeforeMainCharacterDied (wybor dziedzica albo koniec gry) - dalej prowadzi gra.
                    // 403-p: konto roku tylko przy potwierdzonej smierci (sluchacz wyboru dziedzica jest poza naszym kodem - NIETESTOWANE w tym zestawie modow)
                    if (!h.IsAlive) { LordToll.Note(source); why = "gracz - wykonane (" + detail + ")"; }
                    else why = "gracz - gra przejela (wybor dziedzica; zgon nie potwierdzony, poza kontem roku)";
                    return Fate.Killed;
                }
                if (!h.IsAlive) { LordToll.Note(source); why = "wykonane (" + detail + ")"; return Fate.Killed; }
                if (h.DeathMark != KillCharacterAction.KillCharacterActionDetail.None)
                {
                    // nie powinno sie zdarzyc (Busy odracza warunki znaku) - liczymy jako zgon, gra wykona znak po bitwie / w dobie starzenia
                    LordToll.Note(source);
                    Log.Info(Tag + "UWAGA " + Nm(h) + " (" + h.StringId + ") dostal znak smierci " + h.DeathMark + " zamiast smierci od razu (zrodlo " + source + ") - gra wykona go pozniej.");
                    why = "znak smierci " + h.DeathMark + " (gra wykona pozniej)";
                    return Fate.Killed;
                }
                why = "gra odmowila (bohater zyje po akcji " + detail + ")";
                return Fate.Refused;
            }
            catch (Exception e)
            {
                Stumble("TryKill", e);
                bool dead = false;
                try { dead = h != null && !h.IsAlive; } catch { }
                if (dead) { try { LordToll.Note(source); } catch { } why = "wykonane (z bledem po smierci - patrz log bledow)"; return Fate.Killed; }
                why = "blad";
                return Fate.Refused;
            }
        }

        // ---------------------------------------------------------------- okaleczenie
        internal static int MaimCount(Hero h)
        {
            List<Maim> l;
            return _maimRows != 0 && h != null && h.StringId != null && _maims.TryGetValue(h.StringId, out l) ? l.Count : 0;
        }

        internal static string Label(string type)
        {
            return type == Eye ? "Lost an eye" : type == Hand ? "Maimed hand" : type == Leg ? "Lame leg" : "Scarred face";
        }

        /// <summary>"Lost an eye, Lame leg" do encyklopedii / dymka; null gdy bohater nie ma okaleczen.</summary>
        internal static string Labels(Hero h)
        {
            List<Maim> l;
            if (_maimRows == 0 || h == null || h.StringId == null || !_maims.TryGetValue(h.StringId, out l) || l.Count == 0) return null;
            if (l.Count == 1) return Label(l[0].Type);
            var sb = new StringBuilder();
            foreach (var m in l) { if (sb.Length > 0) sb.Append(", "); sb.Append(Label(m.Type)); }
            return sb.ToString();
        }

        /// <summary>Komunikat dla gracza o okaleczeniu (EN).</summary>
        internal static string News(Hero h, string type)
        {
            bool me = h == Hero.MainHero;
            string n = Nm(h);
            if (type == Eye) return me ? "You have lost an eye." : n + " has lost an eye.";
            if (type == Hand) return me ? "Your sword hand is maimed for good." : n + "'s sword hand is maimed for good.";
            if (type == Leg) return me ? "You will walk with a limp for the rest of your days." : n + " will walk with a limp for the rest of " + (h.IsFemale ? "her" : "his") + " days.";
            return me ? "You will carry the scars of this battle." : n + " will carry the scars of this battle.";
        }

        private static bool Has(Hero h, string type)
        {
            List<Maim> l;
            if (!_maims.TryGetValue(h.StringId, out l)) return false;
            foreach (var m in l) if (m.Type == type) return true;
            return false;
        }

        /// <summary>Trwale okaleczenie bohatera (losowy typ wg wag sposrod tych, ktorych jeszcze nie ma). false: bramka albo brak wolnego typu (why - PL).
        /// type: kod typu; detail: opis zmian do logu ("Bow 120->80, Vigor 4->3"). Wolac poza misja i poza seterem HP (doba).</summary>
        internal static bool TryMaim(Hero h, string source, out string type, out string detail, int part = -1)
        {
            type = null; detail = null;
            try
            {
                if (h == null || Campaign.Current == null || !h.IsAlive || h.StringId == null) { detail = "brak bohatera"; return false; }
                if (h.IsChild) { detail = "dziecko"; return false; }
                if (h.DeathMark != KillCharacterAction.KillCharacterActionDetail.None) { detail = "znak smierci"; return false; }
                if (h.HeroDeveloper == null) { detail = "brak HeroDeveloper"; return false; }
                if (MaimCount(h) >= MaxMaims) { detail = "ma juz " + MaxMaims + " okaleczenia"; return false; }
                if (!StoryGuard.MayBeMaimed(h, source)) { detail = "straz fabuly / CanHeroDie"; return false; }
                // 422: czesc ciala ciosu (0 glowa / szyja, 2 barki, 3 rece, 4 nogi) zaweza typ; tulow, brak danych (-1) albo typy z czesci juz zajete = stara tabela
                int[] w = part == 0 ? WeightsHead : part == 2 ? WeightsShoulder : part == 3 ? WeightsArm : part == 4 ? WeightsLeg : Weights;
                int sum = 0;
                for (int i = 0; i < Types.Length; i++) if (!Has(h, Types[i])) sum += w[i];
                if (sum <= 0 && w != Weights)
                {
                    w = Weights;
                    for (int i = 0; i < Types.Length; i++) if (!Has(h, Types[i])) sum += w[i];
                }
                if (sum <= 0) { detail = "brak wolnego typu"; return false; }
                int roll = MBRandom.RandomInt(sum);
                string pick = null;
                for (int i = 0; i < Types.Length; i++)
                {
                    if (Has(h, Types[i])) continue;
                    if (roll < w[i]) { pick = Types[i]; break; }
                    roll -= w[i];
                }
                if (pick == null) { detail = "brak wolnego typu"; return false; }

                var skills = new StringBuilder();
                var log = new StringBuilder();
                string attr = "";
                if (pick == Scar) Skill(h, DefaultSkills.Charm, 10, skills, log);
                else if (pick == Eye)
                {
                    Skill(h, DefaultSkills.Bow, 40, skills, log); Skill(h, DefaultSkills.Crossbow, 40, skills, log);
                    Skill(h, DefaultSkills.Throwing, 30, skills, log); Skill(h, DefaultSkills.Scouting, 20, skills, log);
                }
                else if (pick == Hand)
                {
                    Skill(h, DefaultSkills.TwoHanded, 60, skills, log); Skill(h, DefaultSkills.Bow, 60, skills, log); Skill(h, DefaultSkills.Polearm, 30, skills, log);
                    attr = Attribute(h, DefaultCharacterAttributes.Vigor, log);
                }
                else
                {
                    Skill(h, DefaultSkills.Athletics, 60, skills, log); Skill(h, DefaultSkills.Riding, 20, skills, log);
                    attr = Attribute(h, DefaultCharacterAttributes.Endurance, log);
                }
                Add(new Maim { Id = h.StringId, Type = pick, Day = (int)CampaignTime.Now.ToDays, Skills = skills.ToString(), Attr = attr });
                type = pick;
                detail = Label(pick) + " (" + (log.Length > 0 ? log.ToString() : "bez zmian liczb - juz na dnie") + ")"
                         + (part >= 0 ? " [422: cios: " + HeroBlow422.PartName(part) + (w == Weights ? ", stara tabela" : "") + "]" : "");
                return true;
            }
            catch (Exception e) { Stumble("TryMaim", e); detail = "blad"; return false; }
        }

        /// <summary>Umiejetnosc w dol o cut (dol 0); faktyczna delta do zapisu i logu. Blad jednej umiejetnosci nie zatrzymuje reszty.</summary>
        private static void Skill(Hero h, SkillObject sk, int cut, StringBuilder save, StringBuilder log)
        {
            try
            {
                if (sk == null) return;
                int cur = h.GetSkillValue(sk);
                int nw = Math.Max(0, cur - cut);
                if (nw == cur) return;
                h.HeroDeveloper.SetInitialSkillLevel(sk, nw);
                int now = h.GetSkillValue(sk);
                if (now == cur) return;
                if (save.Length > 0) save.Append(',');
                save.Append(sk.StringId).Append(':').Append((now - cur).ToString(Inv));
                if (log.Length > 0) log.Append(", ");
                log.Append(sk.StringId).Append(' ').Append(cur).Append("->").Append(now);
            }
            catch (Exception e) { Stumble("Skill", e); }
        }

        /// <summary>Atrybut -1 (dol 1); zwraca wpis do zapisu ("Vigor:-1") albo "".</summary>
        private static string Attribute(Hero h, CharacterAttribute at, StringBuilder log)
        {
            try
            {
                if (at == null) return "";
                int cur = h.GetAttributeValue(at);
                if (cur <= 1) return "";
                h.HeroDeveloper.RemoveAttribute(at, 1);
                int now = h.GetAttributeValue(at);
                if (now == cur) return "";
                if (log.Length > 0) log.Append(", ");
                log.Append(at.StringId).Append(' ').Append(cur).Append("->").Append(now);
                return at.StringId + ":" + (now - cur).ToString(Inv);
            }
            catch (Exception e) { Stumble("Attribute", e); return ""; }
        }
    }
}
