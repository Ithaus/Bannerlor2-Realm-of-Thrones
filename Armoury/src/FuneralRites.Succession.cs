using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>410 (PLAN-396 commit 410, Jeff 11.11 "rob 5" - pogrzeby i sukcesja) - SPORNA SUKCESJA + WOJNY O TRON (312c). Zero latek, zero zlota, zadnych
    /// nowych rodow ani zmian krolestw; nikogo nie zabijamy i nie okaleczamy.
    ///
    /// SPOR (klucz Succession Disputes) - oceniany RAZ, przy smierci glowy waznego rodu, gdy powstal wiersz pogrzebu (pelny albo cichy):
    ///  H = glowa rodu PO smierci; R = najwyzszy w migawce linii dziedziczenia BK (FuneralRites.BkLine - 4 pierwszych, zdjeta w BeforeHeroKilled), rozny od H,
    ///  zywy, dorosly, w tym rodzie. Jedna z regul:
    ///   "brat"      - H i R oboje dziecmi zmarlego albo oboje jego rodzenstwem, R starszy od H albo mlodszy najwyzej o Succession Max Age Gap lat;
    ///   "stryj"     - H ma mniej niz 21 lat, R jest rodzenstwem zmarlego i ma co najmniej 30 lat;
    ///   "pominiety" - R byl PIERWSZY w migawce, a glowa zostal ktos inny.
    ///  ORAZ wszystkie: R wolny i prowadzi wlasna partie; relacja osobista R-H ponizej 20; Honor R nie wyzszy niz 0; rzut Succession Dispute Chance.
    ///  NIGDY: rod gracza, Nocna Straz, Wolni Ludzie, Inni (FuneralRites.Eligible); rod zniszczony / bez dziedzica / bez twierdzy (brak wiersza pogrzebu);
    ///  rod z kimkolwiek pod straza fabuly (StoryGuard.ClanLocked - obejmuje H i R); zgon albo zmiana glowy z wydarzenia ROT (ROT.Events na stosie sluchacza -
    ///  sprawdzane raz na zgon glowy i raz na zmiane glowy); republika (ustroj BK tytulu suwerena krolestwa, odczyt odbiciem; brak odczytu = nie republika);
    ///  brak migawki linii BK (wykrycie glowy droga "zmiana glowy" / "migawka" albo brak BK).
    /// SKUTKI: wiersz (rod, H, R, doba, rozstrzygniecie = koniec pogrzebu, termin D+91); relacja osobista R-H -20 (ten sam rod: gra i BK licza ja miedzy
    ///  samymi bohaterami - cs_full.cs:58646, bk.cs:38123). Po koncu pogrzebu rzut pojednania 50% (+20 pp, gdy gracz stanal przy dziedzicu, -20 pp przy rywalu):
    ///  pojednanie = relacja +10 i wiersz zamkniety; inaczej uraza do D+91 (bez dalszych skutkow poza premia 312c). Wiersz przepada, gdy H albo R nie zyje,
    ///  R odszedl z rodu, glowa rodu sie zmienila albo rod zniszczony.
    /// GRACZ (klucz Succession Player Support): po "Attend the funeral" w siedzibie, przy otwartym sporze - pytanie (raz na spor): "Stand with {HEIR}" /
    ///  "Speak for {RIVAL}" / "Stay out of it": +8 z poparta strona, -8 z druga. Bez zlota.
    /// 312c (CanonThroneWars.Compute): bramka "zaloba" - krolestwo, ktorego wladca zmarl mniej niz Funeral Days temu, ma czlon 0 (takze przy pogrzebie cichym
    ///  i samej wiesci; 410-p: wlasny klucz Iron Throne Mourning, NIE = 312c jak przed 410); cel ze sporem w rodzie PANUJACYM - otwartym ALBO w stanie urazy
    ///  (do 91 dni; swiadome rozszerzenie planu, decyzja 21 "rod panujacy w sporze"): czlon x (1 + Iron Throne Contested Bonus). Klucze czytane na zywo.
    ///  410-p: klucz Succession Disputes / Funerals wylaczony w trakcie = wiersze sporow zdjete w dobie bez rzutu i bez zmiany relacji.
    ///  Ocena wojny bywa liczona poza
    ///  watkiem glownym - czyta NIEZMIENNA migawke (_succ), podmieniana w calosci w watku glownym; pamiec doby czlonu kasowana przy smierci wladcy i przy
    ///  otwarciu / zamknieciu sporu (CanonThroneWars.Invalidate).
    /// Zapis: arm_funeral v1 + wiersze "S|..." (spor) i "K|krolestwo|doba" (zaloba); stary zapis = pusto; starsza wersja moda pominie nowe wiersze jako bledne.
    /// Wyjatki per zdarzenie liczone (Stumble), nic sie nie gasi. Reset razem z FuneralRites.Reset.</summary>
    internal static partial class FuneralRites
    {
        private const int SOpen = 0, SGrudge = 1;
        private const int GrudgeDays = 91, RivalHit = -20, MakeUp = 10, PlayerSide = 8, SideShift = 20, ReconcileBase = 50, RelationGate = 20;
        private const int MournKeepDays = 30;   // wiersz zaloby zyje dluzej niz najdluzszy pogrzeb (Funeral Days do 14)

        private sealed class Dispute
        {
            public string Clan, Heir, Rival, Name, Rule;   // rod, dziedzic (glowa), rywal, imie zmarlego, regula
            public int Day, Decide, Until, State, Support; // doba otwarcia, rozstrzygniecia (koniec pogrzebu), terminu; stan; gracz: +1 dziedzic, -1 rywal, 0 nikt
            public bool Asked, Told;                       // gracz zapytany; gracz wie o sporze (komunikaty o wyniku)
        }

        private sealed class SuccMap
        {
            public readonly HashSet<string> Contested = new HashSet<string>(StringComparer.Ordinal);            // krolestwa z rodem panujacym w sporze
            public readonly Dictionary<string, int> Mourn = new Dictionary<string, int>(StringComparer.Ordinal); // krolestwo -> doba smierci wladcy
            public int Days; public float Bonus;
        }

        private static readonly Dictionary<string, Dispute> _disputes = new Dictionary<string, Dispute>(StringComparer.Ordinal);   // rod -> spor
        private static readonly Dictionary<string, int> _kingDied = new Dictionary<string, int>(StringComparer.Ordinal);            // krolestwo -> doba smierci wladcy
        private static volatile SuccMap _succ;   // null = brak zaloby i sporow w rodach panujacych (szybkie wyjscie 312c)
        private static int _sOpened, _sNone, _sRecon, _sGrudge, _sLapsed, _sAsked, _sMourn;
        // BK ustroj (odbicie, raz na proces): BannerKingsConfig.Instance.TitleManager.GetSovereignTitle(Kingdom).Contract.Government.StringId
        private static bool _govTried, _govOk;
        private static PropertyInfo _govManager, _govContract, _govGovernment, _govId;
        private static MethodInfo _govSovereign;

        private static bool SuccKey { get { var s = Settings.Current; return s != null && s.Funerals && s.SuccessionDisputes; } }
        private static bool SuccAny { get { return _disputes.Count > 0 || _kingDied.Count > 0; } }

        private static void SuccReset()
        {
            _disputes.Clear(); _kingDied.Clear(); _succ = null;
            _sOpened = _sNone = _sRecon = _sGrudge = _sLapsed = _sAsked = _sMourn = 0;
        }

        // ---------------------------------------------------------------- 312c: odczyt z oceny wojny (dowolny watek, bez blokad)

        /// <summary>410: krolestwo w zalobie - wladca zmarl mniej niz Funeral Days temu. Czyta niezmienna migawke. Nie rzuca.</summary>
        internal static bool Mourning(Kingdom k, int day)
        {
            var m = _succ;
            if (m == null || k == null || m.Mourn.Count == 0) return false;
            int d;
            if (!m.Mourn.TryGetValue(k.StringId, out d) || day < d || day - d >= m.Days) return false;
            var s = Settings.Current;   // 410-p: klucze na zywo (migawka zyje do najblizszej doby)
            return s != null && s.Funerals && s.IronThroneMourning;
        }

        /// <summary>410: premia czlonu 312c za cel, ktorego rod panujacy ma otwarty spor o dziedzictwo (0 = brak). Czyta niezmienna migawke. Nie rzuca.</summary>
        internal static float ContestedBonus(Kingdom k)
        {
            var m = _succ;
            if (m == null || k == null || m.Contested.Count == 0) return 0f;
            if (!m.Contested.Contains(k.StringId)) return 0f;
            var s = Settings.Current;   // 410-p: klucze na zywo (migawka zyje do najblizszej doby)
            return s != null && s.Funerals && s.SuccessionDisputes ? m.Bonus : 0f;
        }

        /// <summary>Watek glowny: migawka dla 312c od nowa (smierc wladcy, otwarcie / zamkniecie sporu, doba, start sesji). Nie rzuca.</summary>
        private static void SuccRebuild()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.Funerals || !SuccAny) { _succ = null; return; }
                var map = new SuccMap { Days = Days };
                float b = s.IronThroneContestedBonus;
                map.Bonus = b < 0f ? 0f : (b > 1f ? 1f : b);
                if (s.IronThroneMourning) foreach (var kv in _kingDied) map.Mourn[kv.Key] = kv.Value;   // 410-p: zaloba ma wlasny klucz (NIE = 312c jak przed 410)
                if (s.SuccessionDisputes && map.Bonus > 0f)
                    foreach (var d in _disputes.Values)
                    {
                        var cl = FindClan(d.Clan);
                        var k = cl != null && !cl.IsEliminated ? cl.Kingdom : null;
                        if (k != null && !k.IsEliminated && k.RulingClan == cl && k.StringId != null) map.Contested.Add(k.StringId);
                    }
                _succ = map.Mourn.Count == 0 && map.Contested.Count == 0 ? null : map;
            }
            catch (Exception e) { _succ = null; Stumble("SuccRebuild", e); }
        }

        /// <summary>Smierc glowy rodu PANUJACEGO (z FuneralRites.Head, kazdy tryb pogrzebu): zaloba krolestwa od dzis.</summary>
        private static void KingDied(string kingdomId, Hero victim)
        {
            try
            {
                if (string.IsNullOrEmpty(kingdomId) || !Ok(kingdomId)) return;
                int today = Today();
                _kingDied[kingdomId] = today;
                _sMourn++;
                SuccRebuild();
                CanonThroneWars.Invalidate();
                var s = Settings.Current;
                bool on = s != null && s.IronThroneMourning, amb = s != null && s.IronThroneAmbition;
                Info("ZALOBA krolestwa " + kingdomId + " po wladcy " + Nm(victim) + ": " + (!on ? "klucz Iron Throne Mourning NIE - bez skutku dla 312c (data zapamietana)."
                     : !amb ? "klucz Iron Throne Ambition NIE - czlonu 312c nie ma (data zapamietana)."
                     : "czlon 312c tego krolestwa = 0 do dnia " + (today + Days - 1) + " (bramka \"zaloba\")."));
            }
            catch (Exception e) { Stumble("KingDied", e); }
        }

        // ---------------------------------------------------------------- ROT na stosie, ustroj BK

        /// <summary>Wydarzenie ROT (przestrzen ROT.Events) na stosie biezacego sluchacza - glowa / zgon z fabuly. Wolane raz na zgon glowy i raz na zmiane glowy.</summary>
        private static bool RotOnStack()
        {
            try
            {
                var st = new StackTrace(2, false);
                for (int i = 0; i < st.FrameCount && i < 80; i++)
                {
                    var f = st.GetFrame(i);
                    var mm = f != null ? f.GetMethod() : null;
                    if (mm == null) continue;
                    string n = (mm.DeclaringType != null ? mm.DeclaringType.FullName + "." : "") + (mm.Name ?? "");   // metoda zalatana (DMD) - nazwa zawiera typ
                    if (n.IndexOf("ROT.Events", StringComparison.Ordinal) >= 0) return true;
                }
            }
            catch (Exception e) { Stumble("RotOnStack", e); }
            return false;
        }

        private static bool GovReady()
        {
            if (_govTried) return _govOk;
            _govTried = true;
            try
            {
                if (!BkReady() && _bkInstance == null) return _govOk = false;
                var cfg = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                _govManager = cfg != null ? AccessTools.Property(cfg, "TitleManager") : null;
                var tm = _govManager != null ? _govManager.PropertyType : null;
                _govSovereign = tm != null ? AccessTools.Method(tm, "GetSovereignTitle", new[] { typeof(Kingdom) }) : null;
                var title = _govSovereign != null ? _govSovereign.ReturnType : null;
                _govContract = title != null ? AccessTools.Property(title, "Contract") : null;
                _govGovernment = _govContract != null ? AccessTools.Property(_govContract.PropertyType, "Government") : null;
                _govId = _govGovernment != null ? AccessTools.Property(_govGovernment.PropertyType, "StringId") : null;
                _govOk = _bkInstance != null && _govManager != null && _govSovereign != null && _govContract != null && _govGovernment != null && _govId != null;
            }
            catch (Exception e) { _govOk = false; Stumble("GovReady", e); }
            return _govOk;
        }

        /// <summary>Krolestwo o ustroju republiki BK (bk.cs:56253 "Republic"). Brak BK / odczytu / tytulu = false.</summary>
        private static bool Republic(Kingdom k)
        {
            if (k == null || !GovReady()) return false;
            try
            {
                var cfg = _bkInstance.GetValue(null, null);
                var mgr = cfg != null ? _govManager.GetValue(cfg, null) : null;
                var title = mgr != null ? _govSovereign.Invoke(mgr, new object[] { k }) : null;
                var contract = title != null ? _govContract.GetValue(title, null) : null;
                var gov = contract != null ? _govGovernment.GetValue(contract, null) : null;
                var id = gov != null ? _govId.GetValue(gov, null) as string : null;
                return id != null && string.Equals(id, "Republic", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception e) { Stumble("Republic", e); return false; }
        }

        // ---------------------------------------------------------------- ocena sporu (raz, przy smierci glowy)

        private static Hero HeroIn(Clan cl, string id)
        {
            if (cl == null || string.IsNullOrEmpty(id)) return null;
            foreach (var h in cl.Heroes) if (h != null && h.StringId == id) return h;
            return null;
        }

        private static bool ChildOf(Hero x, Hero dead) { return x != null && dead != null && (x.Father == dead || x.Mother == dead); }

        private static bool SiblingOf(Hero x, Hero dead)
        {
            if (x == null || dead == null || x == dead) return false;
            return (x.Father != null && x.Father == dead.Father) || (x.Mother != null && x.Mother == dead.Mother);
        }

        /// <summary>R: najwyzszy w migawce linii, rozny od H i od zmarlego, zywy, dorosly, w tym rodzie. first: R stal na PIERWSZYM miejscu migawki.</summary>
        private static Hero RivalOf(Clan cl, Hero victim, Hero heir, string line, out bool first)
        {
            first = false;
            var ids = line.Split(',');
            for (int i = 0; i < ids.Length; i++)
            {
                var h = HeroIn(cl, ids[i]);
                if (h == null || h == heir || h == victim || !h.IsAlive || h.IsChild || h.Clan != cl) continue;
                first = i == 0;
                return h;
            }
            return null;
        }

        private static string RuleOf(Hero victim, Hero heir, Hero rival, bool first, int gap)
        {
            bool sibR = SiblingOf(rival, victim);
            if ((ChildOf(heir, victim) && ChildOf(rival, victim)) || (SiblingOf(heir, victim) && sibR))
                if (rival.Age >= heir.Age - gap) return "brat";
            if (heir.Age < 21f && sibR && rival.Age >= 30f) return "stryj";
            if (first) return "pominiety";
            return null;
        }

        /// <summary>Z FuneralRites.Head po utworzeniu wiersza pogrzebu: jedna ocena sporu na zgon glowy. Linia logu zawsze (wynik albo powod braku sporu).</summary>
        private static void SuccessionCheck(Clan cl, Hero victim, Hero heir, Row row, string line, bool rot, bool told)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.SuccessionDisputes || cl == null || heir == null || row == null || victim == null || cl.StringId == null) return;
                string head = "SPOR " + cl.StringId + ": dziedzic " + Nm(heir) + " (" + heir.StringId + ")";
                Hero rival = null; string rule = null, no = null; bool first = false;
                int gap = s.SuccessionMaxAgeGap < 0 ? 0 : (s.SuccessionMaxAgeGap > 15 ? 15 : s.SuccessionMaxAgeGap);
                Dispute old;
                if (_disputes.TryGetValue(cl.StringId, out old))
                {
                    // 410-p: stary spor z martwym dziedzicem (zmarly wlasnie byl tym dziedzicem) przepada od razu - nie blokuje oceny nowej smierci glowy do doby
                    var oh = HeroIn(cl, old.Heir);
                    if (old.Heir == victim.StringId || oh == null || !oh.IsAlive)
                    {
                        _disputes.Remove(cl.StringId); _sLapsed++;
                        Info("SPOR " + old.Clan + " (dziedzic " + old.Heir + ", rywal " + old.Rival + ", regula " + old.Rule + ") PRZEPADL: dziedzic nie zyje (nowa smierc glowy rodu).");
                        SuccRebuild();
                        CanonThroneWars.Invalidate();
                    }
                }
                if (_disputes.ContainsKey(cl.StringId)) no = "spor w rodzie juz trwa";
                else if (cl == Clan.PlayerClan || cl.IsEliminated || cl.Leader != heir) no = "rod poza regula";
                else if (rot) no = "zgon albo zmiana glowy z wydarzenia ROT";
                else if (string.IsNullOrEmpty(line)) no = "brak migawki linii dziedziczenia BK";
                else if (Republic(cl.Kingdom)) no = "republika";
                else if (StoryGuard.ClanLocked(cl)) no = "rod pod straza fabuly";
                else
                {
                    rival = RivalOf(cl, victim, heir, line, out first);
                    if (rival == null) no = "brak rywala w linii";
                    else if (StoryGuard.Needed(heir) || StoryGuard.Needed(rival)) no = "strona pod straza fabuly";
                    else if ((rule = RuleOf(victim, heir, rival, first, gap)) == null) no = "zadna regula (brat / stryj / pominiety)";
                    else if (rival.IsPrisoner) no = "rywal w niewoli";
                    else if (rival.PartyBelongedTo == null || rival.PartyBelongedTo.LeaderHero != rival) no = "rywal bez wlasnej partii";
                    else
                    {
                        int rel = CharacterRelationManager.GetHeroRelation(rival, heir);
                        int honor = rival.GetTraitLevel(DefaultTraits.Honor);
                        if (rel >= RelationGate) no = "relacja rywal-dziedzic " + rel + " (prog " + RelationGate + ")";
                        else if (honor > 0) no = "rywal honorowy (Honor " + honor + ")";
                    }
                }
                if (rival != null)
                    head += ", rywal " + Nm(rival) + " (" + rival.StringId + ", wiek " + ((int)rival.Age) + " wobec " + ((int)heir.Age) + (first ? ", pierwszy w linii BK" : "") + ")"
                            + (rule != null ? ", regula " + rule : "");
                if (no != null) { _sNone++; Info(head + " | bez sporu: " + no + "."); return; }
                int chance = s.SuccessionDisputeChance < 0 ? 0 : (s.SuccessionDisputeChance > 100 ? 100 : s.SuccessionDisputeChance);
                int roll = MBRandom.RandomInt(100);
                if (roll >= chance) { _sNone++; Info(head + " | rzut " + roll + "/" + chance + " -> bez sporu."); return; }
                int today = Today();
                var d = new Dispute
                {
                    Clan = cl.StringId, Heir = heir.StringId, Rival = rival.StringId, Name = row.Name, Rule = rule,
                    Day = today, Decide = row.End, Until = today + GrudgeDays, State = SOpen, Told = told
                };
                _disputes[cl.StringId] = d;
                _sOpened++;
                try { ChangeRelationAction.ApplyRelationChangeBetweenHeroes(rival, heir, RivalHit, false); } catch (Exception e) { Stumble("Succession.relation", e); }
                SuccRebuild();
                CanonThroneWars.Invalidate();
                Info(head + " | rzut " + roll + "/" + chance + " -> SPOR OTWARTY: relacja rywal-dziedzic " + RivalHit + " (teraz " + CharacterRelationManager.GetHeroRelation(rival, heir)
                     + "), rozstrzygniecie po dniu " + d.Decide + ", termin urazy dzien " + d.Until + (Ruling(cl) ? " | rod PANUJACY - cel 312c x" + (1f + (_succ != null ? _succ.Bonus : 0f)).ToString("0.00", Inv) : "") + ".");
                if (told) Log.Player(Nm(rival) + " disputes the succession of " + Nm(cl) + ": " + (rival.IsFemale ? "she" : "he") + " will not bow to " + Nm(heir) + ".");
            }
            catch (Exception e) { Stumble("SuccessionCheck", e); }
        }

        // ---------------------------------------------------------------- doba

        /// <summary>Z FuneralRites.Daily: rozstrzygniecia po koncu pogrzebu, wygasle urazy, spory przepadle, stare zaloby; migawka 312c od nowa.</summary>
        private static void SuccDaily()
        {
            if (!SuccAny) { if (_succ != null) { _succ = null; CanonThroneWars.Invalidate(); } return; }
            bool changed = false;
            int today = Today();
            try
            {
                if (!SuccKey && _disputes.Count > 0)
                {
                    // 410-p: klucz Funerals / Succession Disputes wylaczony w trakcie - wiersze zdjete bez rzutu, bez zmiany relacji i bez komunikatow
                    foreach (var d in _disputes.Values) { _sLapsed++; Info("SPOR " + d.Clan + " (dziedzic " + d.Heir + ", rywal " + d.Rival + ", regula " + d.Rule + ") PRZEPADL: klucz wylaczony."); }
                    _disputes.Clear(); changed = true;
                }
                _tmp.Clear();
                foreach (var kv in _disputes)
                {
                    try { if (DisputeDay(kv.Value, today)) _tmp.Add(kv.Key); }
                    catch (Exception e) { Stumble("DisputeDay", e); }
                }
                foreach (var id in _tmp) { _disputes.Remove(id); changed = true; }
                _tmp.Clear();
                foreach (var kv in _kingDied) if (today - kv.Value > MournKeepDays || today < kv.Value) _tmp.Add(kv.Key);
                foreach (var id in _tmp) _kingDied.Remove(id);
                _tmp.Clear();
            }
            catch (Exception e) { Stumble("SuccDaily", e); }
            SuccRebuild();
            if (changed) CanonThroneWars.Invalidate();
        }

        /// <summary>Jeden spor w dobie. true = wiersz do zdjecia.</summary>
        private static bool DisputeDay(Dispute d, int today)
        {
            var cl = FindClan(d.Clan);
            Hero heir = HeroIn(cl, d.Heir), rival = HeroIn(cl, d.Rival);
            string gone = cl == null || cl.IsEliminated ? "rod zniszczony"
                        : heir == null || !heir.IsAlive ? "dziedzic nie zyje"
                        : rival == null || !rival.IsAlive ? "rywal nie zyje"
                        : rival.Clan != cl ? "rywal odszedl z rodu"
                        : cl.Leader != heir ? "glowa rodu sie zmienila" : null;
            if (gone != null)
            {
                _sLapsed++;
                Info("SPOR " + d.Clan + " (dziedzic " + d.Heir + ", rywal " + d.Rival + ", regula " + d.Rule + ") PRZEPADL: " + gone + ".");
                return true;
            }
            if (d.State == SOpen)
            {
                if (today <= d.Decide) return false;
                int p = ReconcileBase + SideShift * d.Support;
                p = p < 0 ? 0 : (p > 100 ? 100 : p);
                int roll = MBRandom.RandomInt(100);
                string side = d.Support > 0 ? "dziedzica" : (d.Support < 0 ? "rywala" : (d.Asked ? "nikogo" : "nikogo (nie pytany)"));
                if (roll < p)
                {
                    _sRecon++;
                    try { ChangeRelationAction.ApplyRelationChangeBetweenHeroes(rival, heir, MakeUp, false); } catch (Exception e) { Stumble("Succession.makeup", e); }
                    Info("SPOR " + d.Clan + ": dziedzic " + Nm(heir) + ", rywal " + Nm(rival) + ", regula " + d.Rule + " | rzut " + roll + "/" + p + " -> POJEDNANIE (relacja +" + MakeUp + ", teraz "
                         + CharacterRelationManager.GetHeroRelation(rival, heir) + ") | gracz poparl " + side + ".");
                    if (d.Told) Log.Player(Nm(rival) + " has made " + (rival.IsFemale ? "her" : "his") + " peace with " + Nm(heir) + ".");
                    return true;
                }
                d.State = SGrudge;
                _sGrudge++;
                Info("SPOR " + d.Clan + ": dziedzic " + Nm(heir) + ", rywal " + Nm(rival) + ", regula " + d.Rule + " | rzut " + roll + "/" + p + " -> URAZA do dnia " + d.Until + " | gracz poparl " + side + ".");
                if (d.Told) Log.Player(Nm(rival) + " leaves the funeral of " + d.Name + " unreconciled.");
                return false;
            }
            if (today > d.Until)
            {
                Info("SPOR " + d.Clan + ": uraza rywala " + Nm(rival) + " wobec dziedzica " + Nm(heir) + " wygasla (termin dnia " + d.Until + ").");
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- gracz na pogrzebie

        /// <summary>Z AttendConsequence (menu siedziby, gracz wlasnie stanal nad grobem): pytanie o strone sporu - raz na spor. Okno gry, bez przelaczania menu.</summary>
        private static void SuccAsk(Clan cl, Hero heir)
        {
            try
            {
                var s = Settings.Current;
                Dispute d;
                if (s == null || !s.SuccessionDisputes || !s.SuccessionPlayerSupport || cl == null || heir == null) return;
                if (!_disputes.TryGetValue(cl.StringId, out d) || d.State != SOpen || d.Asked || d.Heir != heir.StringId) return;
                var rival = HeroIn(cl, d.Rival);
                if (rival == null || !rival.IsAlive || rival == Hero.MainHero) return;
                d.Asked = true; d.Told = true;
                _sAsked++;
                string hn = Nm(heir), rn = Nm(rival), cn = Nm(cl);
                var rows = new List<InquiryElement>
                {
                    new InquiryElement("heir", "Stand with " + hn, null, true, hn + " will think better of you and " + rn + " worse. The house is more likely to make its peace."),
                    new InquiryElement("rival", "Speak for " + rn, null, true, rn + " will think better of you and " + hn + " worse. The house is less likely to make its peace."),
                    new InquiryElement("none", "Stay out of it", null, true, "You take no side.")
                };
                string clanId = cl.StringId;
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("A house divided",
                    "The house of " + cn + " is divided over the succession. " + rn + " will not bow to " + hn + ".", rows, false, 1, 1, "Decide", null,
                    delegate (List<InquiryElement> picked)
                    {
                        try { SuccAnswer(clanId, d, picked != null && picked.Count > 0 ? picked[0].Identifier as string : null); }
                        catch (Exception e) { Stumble("SuccAnswer", e); }
                    }, null), true);
            }
            catch (Exception e) { Stumble("SuccAsk", e); }
        }

        private static void SuccAnswer(string clanId, Dispute asked, string pick)
        {
            Dispute d;
            if (!_disputes.TryGetValue(clanId, out d) || !ReferenceEquals(d, asked) || d.State != SOpen || d.Support != 0) return;   // inna kampania / spor juz rozstrzygniety
            var cl = FindClan(clanId);
            Hero heir = HeroIn(cl, d.Heir), rival = HeroIn(cl, d.Rival);
            if (heir == null || rival == null || !heir.IsAlive || !rival.IsAlive || Hero.MainHero == null) return;
            if (pick == "heir" || pick == "rival")
            {
                bool forHeir = pick == "heir";
                d.Support = forHeir ? 1 : -1;
                ChangeRelationAction.ApplyPlayerRelation(forHeir ? heir : rival, PlayerSide);
                ChangeRelationAction.ApplyPlayerRelation(forHeir ? rival : heir, -PlayerSide);
            }
            Info("SPOR " + clanId + ": gracz na pogrzebie poparl " + (d.Support > 0 ? "dziedzica " + Nm(heir) : (d.Support < 0 ? "rywala " + Nm(rival) : "nikogo"))
                 + (d.Support != 0 ? " (+" + PlayerSide + " / -" + PlayerSide + ", szansa pojednania " + (ReconcileBase + SideShift * d.Support) + "%; relacja gracza teraz: dziedzic "
                    + CharacterRelationManager.GetHeroRelation(Hero.MainHero, heir) + ", rywal " + CharacterRelationManager.GetHeroRelation(Hero.MainHero, rival) + ")" : "") + ".");   // 410-p: faktyczny skutek (zalezy od czynnego modelu dyplomacji)
        }

        // ---------------------------------------------------------------- start, zapis

        /// <summary>Z AddMenus (po wczytaniu): migawka 312c i linia startu.</summary>
        private static void SuccStart()
        {
            SuccRebuild();
            try
            {
                var s = Settings.Current;
                int grudge = 0;
                foreach (var d in _disputes.Values) if (d.State == SGrudge) grudge++;
                Log.Info(Tag + " (410): sukcesja start | klucze: spor " + YN(s != null && s.SuccessionDisputes) + ", roznica wieku " + (s != null ? s.SuccessionMaxAgeGap : 0) + ", szansa "
                         + (s != null ? s.SuccessionDisputeChance : 0) + "%, gracz popiera " + YN(s != null && s.SuccessionPlayerSupport) + ", premia 312c "
                         + (s != null ? s.IronThroneContestedBonus : 0f).ToString("0.00", Inv) + ", zaloba 312c " + YN(s != null && s.IronThroneMourning) + " | BK linia dziedziczenia " + YN(BkReady()) + ", ustroj BK (republiki) " + YN(GovReady())
                         + " | w zapisie: sporow " + _disputes.Count + " (uraza " + grudge + "), zalob " + _kingDied.Count
                         + " | migawka 312c: krolestw w zalobie " + (_succ != null ? _succ.Mourn.Count : 0) + ", z rodem panujacym w sporze " + (_succ != null ? _succ.Contested.Count : 0) + ".");
            }
            catch (Exception e) { Stumble("SuccStart", e); }
        }

        /// <summary>Dopisek do linii KONIEC pogrzebu (liczniki sesji).</summary>
        private static string SuccEndText()
        {
            return " | spory (sesja): otwarte " + _sOpened + ", bez sporu " + _sNone + ", pojednania " + _sRecon + ", urazy " + _sGrudge + ", przepadle " + _sLapsed + ", pytan do gracza " + _sAsked
                   + ", trwa " + _disputes.Count + "; zaloby krolestw " + _sMourn;
        }

        /// <summary>Wiersze "S|rod|dziedzic|rywal|imie|regula|doba|rozstrzygniecie|termin|stan|poparcie|pytany|wie" i "K|krolestwo|doba".</summary>
        private static void SuccExport(StringBuilder sb)
        {
            foreach (var d in _disputes.Values)
            {
                if (!Ok(d.Clan) || !Ok(d.Heir) || !Ok(d.Rival)) continue;
                sb.Append('\n').Append("S|").Append(d.Clan).Append('|').Append(d.Heir).Append('|').Append(d.Rival).Append('|').Append(Clean(d.Name)).Append('|').Append(Clean(d.Rule))
                  .Append('|').Append(d.Day.ToString(Inv)).Append('|').Append(d.Decide.ToString(Inv)).Append('|').Append(d.Until.ToString(Inv)).Append('|').Append(d.State.ToString(Inv))
                  .Append('|').Append(d.Support.ToString(Inv)).Append('|').Append(d.Asked ? '1' : '0').Append('|').Append(d.Told ? '1' : '0');
            }
            foreach (var kv in _kingDied)
                if (Ok(kv.Key)) sb.Append('\n').Append("K|").Append(kv.Key).Append('|').Append(kv.Value.ToString(Inv));
        }

        /// <summary>Wiersz "S" albo "K" z zapisu. false = bledny (pominiety). Rzuca przy zlej liczbie - lapie Import.</summary>
        private static bool SuccImportLine(string[] p)
        {
            if (p[0] == "K")
            {
                if (p.Length < 3 || p[1].Length == 0) return false;
                _kingDied[p[1]] = int.Parse(p[2], Inv);
                return true;
            }
            if (p.Length < 13 || p[1].Length == 0 || p[2].Length == 0 || p[3].Length == 0) return false;
            int sup = int.Parse(p[10], Inv);
            _disputes[p[1]] = new Dispute
            {
                Clan = p[1], Heir = p[2], Rival = p[3], Name = p[4], Rule = p[5], Day = int.Parse(p[6], Inv), Decide = int.Parse(p[7], Inv), Until = int.Parse(p[8], Inv),
                State = p[9] == "1" ? SGrudge : SOpen, Support = sup > 0 ? 1 : (sup < 0 ? -1 : 0), Asked = p[11] == "1", Told = p[12] == "1"
            };
            return true;
        }
    }
}
