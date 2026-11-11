using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 389 (Jeff 10.11 "rob caly punkt 2", czesc 2e: "Bohaterowie bez broni (382): spis pokazal 383 takich wsrod AI - na razie tylko to mierze").
    /// BOHATEROWIE AI BEZ BRONI NA MAPIE - spis z lista i "spoznione dorosniecie".
    /// Badanie (CHANGELOG 389): spis CrashScribe Z16 przy 15 wczytaniach 09.11 pokazuje stale 383 bez broni - zamknieta grupa, nie narasta. Dane ROT / gry
    /// sa czyste (kazdy lord z XML ma bron w zestawie bojowym; bez broni z danych tylko 3 wedrowcy: Varys, Kinvara, Baelish). Przyczyna wedlug KODU
    /// (bez imion z zapisu - stad lista ponizej): BannerKings przy starcie kampanii tworzy rody gentry (CreateGentryClan -> CreateFamily -> DeliverOffSpring):
    /// "dzieci" w wieku do (wiek rodzica - 18) lat, czyli czesto juz dorosle, zawsze w stroju z szablonu DZIECIECEGO (GetEquipmentForDeliveredOffspring),
    /// skopiowanym takze do zestawu BOJOWEGO (12 slotow razem z pustymi - bez broni). Doroslego w chwili utworzenia gra nie ma na liscie dorastajacych
    /// (AgingCampaignBehavior._heroesYoungerThanHeroComesOfAge), wiec OnHeroComesOfAge - jedyne miejsce dajace zestaw lorda - nigdy sie dla niego nie odpala.
    /// Brak broni NIE wplywa na symulacje bitew (DefaultCombatSimulationModel liczy z poziomu / tieru; ArmedStrength pomija bohaterow) - szkodzi w misji
    /// gracza, gdzie dziala 382 (klon). Tu naprawa TRWALA zestawu BOJOWEGO (bitwy, turnieje, okup, lup). 389b: zestaw CYWILNY zostaje, jaki byl - obdarowany w miescie / zamku
    /// dalej chodzi w dzieciecej tunice (gra przy dorastaniu daje oba zestawy; cywilny odlozony jako 389c).
    ///
    /// (A) SPIS (zawsze, tylko log): dorosli zywi bohaterowie AI bez notabli, gracza i jego rodu, umarlych Innych; linia 'Bezbronni (389): spis' po wczytaniu
    ///     i potem raz na dobe tylko przy zmianie liczby; lista imion (typ, kultura, wiek, stan, tulow) tylko po wczytaniu, do 400.
    /// (B) SPOZNIONE DOROSNIECIE (klucz AiHeroesLateComingOfAgeKit; doba, najwyzej PerDay bohaterow): Occupation.Lord z podpisem "nigdy nie dorosl":
    ///     zadnej broni w slotach 0-3 ORAZ tulow bojowy pusty / z szablonu dzieciecego albo mlodziezowego / cywilny (HeroDress382.CivilBody). Dostaje RAZ
    ///     w zyciu (rejestr StringId w zapisie, arm_389_done) dokladnie to, co 382 dalby mu w misji - HeroDress382.Fill (wlasny szablon / szablon lorda
    ///     kultury, sito ItemReq.MeetsHero, dystansowa z amunicja, bez unikatow, legend i koron), zapis przez EquipmentHelper.AssignHeroEquipmentFromEquipment
    ///     (straz unikatow UniqueSpoils i kolejka Z16 CrashScribe dzialaja). Konie i zestaw cywilny bez zmian. Nie gracz, nie jego rod, nie wedrowcy /
    ///     towarzysze, nie umarli, nie w trakcie bitwy (MapEvent). Bohater bez podpisu (ma zbroje, nie ma broni) - tylko spis; w bitwie gracza dalej 382.
    /// "Nic z niczego": to nie doplata, tylko brakujacy posag dorosniecia - kazdy inny lord dostal zestaw za darmo z danych albo z OnHeroComesOfAge,
    /// tych BK pominal bledem; raz na bohatera, kto potem straci bron (okup turniejowy, prawo unikatow), drugiego nie dostaje. Sprzet bohatera AI nie jest
    /// towarem (DTE, AiGear, ArmedStrength go nie ruszaja).
    /// Wyjatek na bohaterze = potkniecie (log 3 pierwszych), bez globalnego wylacznika. Bohater, ktoremu wzorzec nic nie dal, nie jest probowany ponownie
    /// w tej sesji (zbior w pamieci, nie w zapisie - po zmianie ustawien / umiejetnosci nowa proba po wczytaniu).
    ///
    /// 389c (Jeff 11.11 "tak, rob wszystko 2-4", pkt 3 "202 bohaterow nadal bez broni (187 kobiet) - ich wzorzec nie ma broni"). POPRAWKA NAGLOWKA: grupa
    /// NIE jest zamknieta - BK co tydzien dorabia dworzan (BKCourtierBehavior.MaintainKingdom, CreateSpecialHero bez przydzialu sprzetu), a u kobiet
    /// przyczyna jest szablon postaci "bannerkings_gentry_<kultura>_spouse_N": jego roster ma same zestawy cywilne (lady_normal), wiec wlasnego wzorca
    /// bojowego nie ma, a bron z szablonu lorda kultury odpada na sicie umiejetnosci (albo kultura nie ma zenskiego szablonu lorda). Corki gentry
    /// dziedzicza szablon matki (DeliverOffSpring: template = mother.CharacterObject), dworki dostaja go wprost.
    ///  (0) DIAGNOSTYKA (zawsze): do 20 imion na dobe, ktorym wzorzec nic nie dal - umiejetnosci, roster, zrodla, co odpadlo i dlaczego; spis liczy
    ///      "umiejetnosci bojowe wszystkie 0" oraz osobno bezbronnych poza posagiem (lord ze zbroja z danych - Varys, Baelish - i nie-lordowie, np.
    ///      ksiegarze BK Occupation.Special). Ci ZOSTAJA bez broni: to cywile z danych ROT / BK, nie blad.
    ///  (1) BRON ZAPASOWA (klucz AiHeroesSpareArms) - HeroDress382.SpareArms, wspolna z 382 w misji: szablon lorda kultury, szablon gentry BK, pula kultury.
    ///  (2) STROJ CYWILNY DOROSLY (klucz AiHeroesAdultCivilClothes; rejestr arm_389c_civ, raz w zyciu): lord AI, ktorego zestaw CYWILNY ma tulow
    ///      z szablonu dzieciecego (tylko IsChildEquipmentTemplate, bez sztuk noszonych tez przez doroslych lordow), dostaje zestaw cywilny, ktory gra
    ///      daje w wieku 18 lat (EquipmentSelectionModel.GetEquipmentForHeroComeOfAge, Civilian; zapasowo wlasny przeglad rosterow lordow kultury i plci).
    ///      BEZ sita umiejetnosci (suknia 0.4 kg ma wymog Atletyki 1 - gra daje ja kazdej damie; sito pilnuje zakladania na ekranie), bez unikatow,
    ///      sztuki nie dla tej plci odpadaja. 2b: gdy tulow BOJOWY nadal jest dzieciecy (zbroja nie wyszla), tulow / nogi / glowa z tego zestawu ida tez
    ///      do zestawu bojowego (tylko sloty, w ktorych nadal lezy sztuka dzieciecego stroju). Kto czeka jeszcze na posag (1), jest przebierany po nim.
    ///      Stary stroj dzieciecy znika, nowy powstaje - dokladnie jak przy dorosnieciu w grze (OnHeroComesOfAge), nie towar.
    /// NIE zrobione swiadomie: umiejetnosci tych bohaterow ("spoznione dorosniecie umiejetnosci") - osobna decyzja Jeffa.
    /// </summary>
    internal static class HeroArms389
    {
        private const int PerDay = 60, NamesPerLine = 40, NamesMax = 400, GiftNames = 20, ErrMax = 3;

        private static readonly HashSet<string> _done = new HashSet<string>();     // w zapisie: kto dostal posag (raz w zyciu)
        private static readonly HashSet<string> _failed = new HashSet<string>();   // sesja: wzorzec nic nie dal
        private static HashSet<string> _childBody;                                 // sesja: sztuki tulowia z szablonow dzieciecych / mlodziezowych
        private static readonly HashSet<string> _civ = new HashSet<string>();      // 389c, w zapisie: kto dostal dorosly stroj cywilny (raz w zyciu)
        private static readonly HashSet<string> _civFailed = new HashSet<string>(); // 389c, sesja: brak zestawu cywilnego dla kultury / plci
        private static HashSet<string> _childAll;                                  // 389c-p, sesja: WSZYSTKIE tulowia z rosterow dzieciecych (takze noszone przez doroslych lordow)
        private static HashSet<string> _childOnly;                                 // 389c, sesja: tulowia TYLKO dzieciece (bez noszonych przez doroslych lordow)
        private static int _lastBare = -1, _total, _errs, _stumbles, _civTotal;

        private static bool On { get { var s = Settings.Current; return s != null && s.AiHeroesLateComingOfAgeKit; } }
        private static bool CivOn { get { var s = Settings.Current; return s != null && s.AiHeroesAdultCivilClothes; } }

        internal static void Reset()
        {
            _done.Clear(); _failed.Clear(); _childBody = null;
            _civ.Clear(); _civFailed.Clear(); _childOnly = null; _childAll = null; _civTotal = 0;
            HeroDress382.ResetSpare();
            _lastBare = -1; _total = 0; _errs = 0; _stumbles = 0;
        }

        internal static string ExportCiv() { return string.Join(";", _civ); }

        internal static void ImportCiv(string s)
        {
            _civ.Clear();
            if (string.IsNullOrEmpty(s)) return;
            foreach (var p in s.Split(';')) if (p.Length > 0) _civ.Add(p);
        }

        internal static string Export() { return string.Join(";", _done); }

        internal static void Import(string s)
        {
            _done.Clear();
            if (string.IsNullOrEmpty(s)) return;
            foreach (var p in s.Split(';')) if (p.Length > 0) _done.Add(p);
        }

        /// <summary>Po wczytaniu / starcie kampanii: spis z lista imion (tylko log).</summary>
        internal static void OnSession()
        {
            try { _failed.Clear(); _childBody = null; _civFailed.Clear(); _childOnly = null; _childAll = null; HeroDress382.ResetSpare(); _lastBare = -1; Census(true); }
            catch (Exception e) { Log.Error("HeroArms389.OnSession", e); }
        }

        /// <summary>Doba: posag spoznionego dorosniecia (klucz), potem spis - linia tylko przy zmianie liczby.</summary>
        internal static void Daily()
        {
            try { if (On) Gift(); } catch (Exception e) { Log.Error("HeroArms389.Gift", e); }
            try { if (CivOn) Civil(); } catch (Exception e) { Log.Error("HeroArms389.Civil", e); }
            try { Census(false); } catch (Exception e) { Log.Error("HeroArms389.Census", e); }
        }

        // ------------------------------------------------------------ populacja i podpis
        private static bool Counted(Hero h)
        {
            if (h == null || !h.IsAlive || h.IsChild || h.IsNotable || h == Hero.MainHero) return false;
            if (h.Clan != null && h.Clan == Clan.PlayerClan) return false;
            var co = h.CharacterObject;
            if (co == null || h.BattleEquipment == null) return false;
            return !Undead.Character(co);
        }

        private static bool NoWeapon(Equipment eq)
        {
            for (int i = 0; i < 4; i++) if (!eq[(EquipmentIndex)i].IsEmpty) return false;
            return true;
        }

        /// <summary>Tulow: 0 pusty, 1 stroj dzieciecy / mlodziezowy, 2 cywilny (suknia, kaftan), 3 zbroja.</summary>
        private static int Body(Equipment eq)
        {
            var it = eq[EquipmentIndex.Body].Item;
            if (it == null) return 0;
            if (ChildBody().Contains(it.StringId ?? "")) return 1;
            return HeroDress382.CivilBody(it) != 0 ? 2 : 3;
        }

        private static HashSet<string> ChildBody()
        {
            if (_childBody != null) return _childBody;
            var set = new HashSet<string>();
            try
            {
                var rosters = MBObjectManager.Instance.GetObjectTypeList<MBEquipmentRoster>();
                if (rosters != null)
                    foreach (var ro in rosters)
                    {
                        if (ro == null) continue;
                        if ((ro.EquipmentCategories & (EquipmentCategories.IsChildEquipmentTemplate | EquipmentCategories.IsTeenagerEquipmentTemplate)) == 0) continue;
                        foreach (var e in ro.AllEquipments)
                        {
                            if (e == null) continue;
                            var it = e[EquipmentIndex.Body].Item;
                            if (it != null && it.StringId != null) set.Add(it.StringId);
                        }
                    }
            }
            catch (Exception e) { Stumble("ChildBody", null, e); }
            _childBody = set;
            return set;
        }

        /// <summary>Typ bohatera do spisu (po rodzie BK, pozycji w rodzie i zajeciu).</summary>
        private static int Kind(Hero h)
        {
            string cid = h.Clan != null ? (h.Clan.StringId ?? "") : "";
            if (cid.StartsWith("gentryClan_", StringComparison.Ordinal))
            {
                var l = h.Clan.Leader;
                if (l == h) return 2;
                if (l != null && l.Spouse == h) return 1;
                return 0;
            }
            if (cid.EndsWith("_knight_clan", StringComparison.Ordinal)) return 3;
            if (h.Occupation == Occupation.Wanderer) return h.CompanionOf != null ? 6 : 5;
            // 389b: po pochodzeniu postaci, nie po prefiksie id - lordowie ROT z danych to ROTlord_* / spc_* (594 postaci), "lord_*" ma tylko vanilla;
            // postac z XML jest oryginalna, bohater utworzony w grze (HeroCreator) ma postac skopiowana z szablonu (OriginalCharacter != null)
            if (h.Occupation == Occupation.Lord) { var co = h.CharacterObject; return co != null && co.IsOriginalCharacter ? 4 : 7; }
            return 8;
        }

        private static readonly string[] KindName = { "dziecko gentry BK", "malzonek gentry", "glowa gentry", "rycerz BK", "lord z danych", "wedrowiec", "towarzysz AI", "lord utworzony w grze", "inny" };
        private static readonly string[] BodyName = { "pusty", "stroj dzieciecy", "cywilny", "zbroja" };

        /// <summary>Stan: 0 wodz partii, 1 w partii, 2 jeniec, 3 w osadzie, 4 nieobecny (NotSpawned / zbieg / w drodze / inne).</summary>
        private static int State(Hero h)
        {
            if (h.IsPrisoner) return 2;
            var p = h.PartyBelongedTo;
            if (p != null) return p.LeaderHero == h ? 0 : 1;
            if (h.CurrentSettlement != null) return 3;
            return 4;
        }

        private static readonly string[] StateName = { "wodz partii", "w partii", "jeniec", "w osadzie", "nieobecny" };

        // ------------------------------------------------------------ (A) spis
        private static void Census(bool names)
        {
            int n = 0, bare = 0, male = 0, female = 0, sig = 0, zero = 0, lordArm = 0, notLord = 0, civChild = 0, civBare = 0, civWide = 0, civData = 0, civKept = 0;
            var kind = new int[KindName.Length];
            var state = new int[StateName.Length];
            var body = new int[BodyName.Length];
            var age = new int[3];
            var cult = new Dictionary<string, int>();
            List<string> list = names ? new List<string>() : null;
            foreach (var h in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!Counted(h)) continue;
                    n++;
                    var eq = h.BattleEquipment;
                    if (h.Occupation == Occupation.Lord && h.CivilianEquipment != null)   // 389c: miara stroju cywilnego (wszyscy lordowie AI, nie tylko bezbronni)
                    {
                        var cb = h.CivilianEquipment[EquipmentIndex.Body].Item;
                        if (cb == null) civBare++;
                        else
                        {
                            // 389c-p: ten sam podpis co w Civil() (caly zestaw, nie sam tulow); przebrani z rejestru liczeni osobno
                            bool dataC; int cs = ChildSig(h, h.CivilianEquipment, out dataC);
                            if (dataC) civData++;
                            else if (cs > 0 && _civ.Contains(h.StringId ?? "")) civKept++;
                            else if (cs == 1) civChild++;
                            else if (cs == 2) { civChild++; civWide++; }
                        }
                    }
                    if (!NoWeapon(eq)) continue;
                    bare++;
                    int k = Kind(h), st = State(h), b = Body(eq);
                    kind[k]++; state[st]++; body[b]++;
                    if (h.IsFemale) female++; else male++;
                    float a = h.Age;
                    age[a < 26f ? 0 : a < 41f ? 1 : 2]++;
                    string cu = h.Culture != null ? (h.Culture.StringId ?? "-") : "-";
                    int c; cult.TryGetValue(cu, out c); cult[cu] = c + 1;
                    if (h.Occupation == Occupation.Lord && b != 3) sig++;
                    else if (h.Occupation == Occupation.Lord) lordArm++;
                    else notLord++;
                    if (AllZero(h.CharacterObject)) zero++;
                    if (list != null && list.Count < NamesMax)
                    {
                        var bi = eq[EquipmentIndex.Body].Item;
                        list.Add(Nm(h) + " (" + h.StringId + ", " + (h.Clan != null ? h.Clan.StringId : "bez rodu") + ", " + cu + ", " + ((int)a) + ", " + (h.IsFemale ? "k" : "m")
                                 + ", " + KindName[k] + (k == 8 ? " " + h.Occupation : "") + ", " + StateName[st] + (st == 4 ? " " + h.HeroState : "")
                                 + ", tulow=" + (bi != null ? bi.StringId : "-") + (b == 1 ? " dzieciecy" : b == 2 ? " cywilny" : "") + (_done.Contains(h.StringId) ? ", po posagu" : "") + ")");
                    }
                }
                catch (Exception e) { Stumble("Census", h, e); }
            }
            if (!names && bare == _lastBare) return;
            _lastBare = bare;
            var sb = new StringBuilder();
            sb.Append("Bezbronni (389): spis").Append(names ? " po wczytaniu" : " (zmiana)").Append(" | doroslych AI ").Append(n).Append(" | bez broni ").Append(bare).Append(" | typ: ");
            for (int i = 0; i < KindName.Length; i++) sb.Append(i > 0 ? ", " : "").Append(KindName[i]).Append(' ').Append(kind[i]);
            sb.Append(" | plec m ").Append(male).Append(" / k ").Append(female);
            sb.Append(" | wiek 18-25: ").Append(age[0]).Append(", 26-40: ").Append(age[1]).Append(", 41+: ").Append(age[2]);
            sb.Append(" | stan: ");
            for (int i = 0; i < StateName.Length; i++) sb.Append(i > 0 ? ", " : "").Append(StateName[i]).Append(' ').Append(state[i]);
            sb.Append(" | tulow: ");
            for (int i = 0; i < BodyName.Length; i++) sb.Append(i > 0 ? ", " : "").Append(BodyName[i]).Append(' ').Append(body[i]);
            sb.Append(" | kultury: ");
            sb.Append(string.Join(", ", cult.OrderByDescending(x => x.Value).Take(5).Select(x => x.Key + " " + x.Value).ToArray()));
            sb.Append(" | z podpisem spoznionego dorosniecia (lord, bez broni, tulow bez zbroi) ").Append(sig).Append(", w rejestrze posagu ").Append(_done.Count);
            sb.Append(" | poza posagiem - cywile z danych, zostaja bez broni: lord w zbroi / stroju z danych ").Append(lordArm).Append(", nie-lord (ksiegarze BK itp.) ").Append(notLord);
            sb.Append(" | umiejetnosci bojowe wszystkie 0: ").Append(zero);
            sb.Append(" | 389c stroj cywilny lordow AI: tulow dzieciecy ").Append(civChild).Append(" (w tym podpis szeroki - tulow noszony tez przez doroslych, zestaw bez broni i konia: ").Append(civWide)
              .Append("), z danych w takim stroju - pominieci ").Append(civData).Append(", przebrani z takim tulowiem (rejestr) ").Append(civKept).Append(", tulow pusty ").Append(civBare).Append(", w rejestrze przebranych ").Append(_civ.Count)
              .Append(CivOn ? "" : " (AiHeroesAdultCivilClothes WYLACZONY)");
            sb.Append(On ? " | posag wlaczony (AiHeroesLateComingOfAgeKit), do " + PerDay + " na dobe." : " | posag WYLACZONY (AiHeroesLateComingOfAgeKit) - tylko spis.");
            Log.Info(sb.ToString());
            if (list == null || list.Count == 0) return;
            int lines = (list.Count + NamesPerLine - 1) / NamesPerLine;
            for (int i = 0; i < lines; i++)
                Log.Info("Bezbronni (389): lista " + (i + 1) + "/" + lines + ": " + string.Join("; ", list.Skip(i * NamesPerLine).Take(NamesPerLine).ToArray())
                         + (i == lines - 1 && bare > list.Count ? " ... (+" + (bare - list.Count) + ")" : ""));
        }

        // ------------------------------------------------------------ (B) spoznione dorosniecie
        private static void Gift()
        {
            if (Campaign.Current == null) return;
            int given = 0, sigLeft = 0, noTpl = 0, nothing = 0, stillBare = 0, st0 = _stumbles;
            var spare = new int[4];
            var who = new List<string>();
            var why = new List<string>();
            foreach (var h in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!Counted(h) || h.Occupation != Occupation.Lord) continue;
                    if (h.HeroState == Hero.CharacterStates.Disabled || h.CompanionOf != null) continue;
                    string id = h.StringId;
                    if (string.IsNullOrEmpty(id) || _done.Contains(id)) continue;
                    var src = h.BattleEquipment;
                    if (src.IsCivilian || src.IsStealth || !NoWeapon(src)) continue;
                    int b = Body(src);
                    if (b == 3) continue;                              // ma zbroje, nie ma broni - nie nasz podpis (tylko spis; w bitwie gracza 382)
                    if (_failed.Contains(id)) { sigLeft++; continue; }
                    if (given >= PerDay) { sigLeft++; continue; }
                    var p = h.PartyBelongedTo;
                    if (p != null && p.MapEvent != null) { sigLeft++; continue; }   // w bitwie - nastepnej doby
                    if (h.PartyBelongedToAsPrisoner != null && h.PartyBelongedToAsPrisoner.MapEvent != null) { sigLeft++; continue; }
                    var co = h.CharacterObject;
                    var k = HeroDress382.Fill(src, co, h, b == 0, true, b != 0, true);
                    if (k == null) { _failed.Add(id); noTpl++; sigLeft++; if (why.Count < GiftNames) why.Add(Diag(h, co, null)); continue; }
                    if (k.N <= 0 || k.Eq == null) { _failed.Add(id); nothing++; sigLeft++; if (why.Count < GiftNames) why.Add(Diag(h, co, k)); continue; }
                    var eq = k.Eq;
                    if (eq.IsCivilian || eq.IsStealth)                 // klon ma byc bojowy (AssignHeroEquipmentFromEquipment wybiera zestaw po typie)
                    {
                        var be = new Equipment(Equipment.EquipmentType.Battle);
                        be.FillFrom(eq, false);
                        eq = be;
                    }
                    global::Helpers.EquipmentHelper.AssignHeroEquipmentFromEquipment(h, eq);
                    _done.Add(id);
                    given++; _total++;
                    if (k.Spare > 0 && k.Spare < spare.Length) spare[k.Spare]++;
                    bool still = NoWeapon(h.BattleEquipment);
                    if (still && why.Count < GiftNames) why.Add(Diag(h, co, k));
                    if (still) stillBare++;
                    if (who.Count < GiftNames)
                        who.Add(Nm(h) + " (" + (h.Clan != null ? h.Clan.StringId : "bez rodu") + ", " + KindName[Kind(h)] + ", " + StateName[State(h)] + ") <- "
                                + (k.AFrom != k.From ? "bron " + k.From + ", pancerz " + k.AFrom : k.From) + " [" + string.Join(", ", k.Got) + "]" + (still ? " NADAL BEZ BRONI" : ""));
                }
                catch (Exception e) { Stumble("Gift", h, e); }
            }
            if (given == 0 && noTpl == 0 && nothing == 0 && _stumbles == st0) return;   // cicha doba
            Log.Info("Bezbronni (389): doba " + (int)CampaignTime.Now.ToDays + " - spoznione dorosniecie: obdarowano " + given + " (w tej sesji " + _total + ", rejestr " + _done.Count
                     + "), z tego nadal bez broni (sito umiejetnosci / wzorzec bez broni) " + stillBare + ", zostalo z podpisem " + sigLeft + ", bez wzorca " + noTpl
                     + ", wzorzec nic nie dal " + nothing + ", bron zapasowa (389c): szablon lorda " + spare[1] + " / szablon gentry " + spare[2] + " / pula kultury " + spare[3]
                     + ", potkniecia " + (_stumbles - st0) + (who.Count > 0 ? ": " + string.Join("; ", who) : "") + (given > who.Count ? " ... (+" + (given - who.Count) + ")" : "") + ".");
            if (why.Count > 0)
                Log.Info("Bezbronni (389c): doba " + (int)CampaignTime.Now.ToDays + " - dlaczego bez broni (do " + GiftNames + "): " + string.Join("; ", why) + ".");
        }

        // ------------------------------------------------------------ 389c (0) diagnostyka
        private static readonly SkillObject[] NoSkills = new SkillObject[0];

        private static SkillObject[] Fighting()
        {
            try { return new[] { DefaultSkills.OneHanded, DefaultSkills.TwoHanded, DefaultSkills.Polearm, DefaultSkills.Bow, DefaultSkills.Crossbow, DefaultSkills.Throwing, DefaultSkills.Athletics }; }
            catch { return NoSkills; }
        }

        private static bool AllZero(CharacterObject co)
        {
            if (co == null) return false;
            var f = Fighting();
            if (f.Length == 0) return false;
            foreach (var sk in f) if (sk != null && co.GetSkillValue(sk) > 0) return false;
            return true;
        }

        /// <summary>Jedna pozycja linii "dlaczego bez broni": imie, umiejetnosci (1H/2H/Pol/Bow/Xbow/Thr/Ath), roster postaci, zrodla wzorca, co odpadlo.</summary>
        private static string Diag(Hero h, CharacterObject co, HeroDress382.Kit k)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append(Nm(h)).Append(" (").Append(h.StringId).Append(h.IsFemale ? ", k" : ", m").Append(", um ");
                var f = Fighting();
                for (int i = 0; i < f.Length; i++) sb.Append(i > 0 ? "/" : "").Append(f[i] != null ? co.GetSkillValue(f[i]) : -1);
                sb.Append(", roster ").Append(HeroDress382.RosterId(co));
                if (k == null) sb.Append(", brak wzorca");
                else
                {
                    sb.Append(", bron z: ").Append(k.From).Append(", pancerz z: ").Append(k.AFrom);
                    if (k.Got != null && k.Got.Count > 0) sb.Append(", weszlo [").Append(string.Join(", ", k.Got)).Append(']');
                    sb.Append(", odpadlo [").Append(k.Rej != null ? string.Join(", ", k.Rej) : "nic - wzorzec bez broni").Append(']');
                }
                return sb.Append(')').ToString();
            }
            catch { return Nm(h) + " (?)"; }
        }

        // ------------------------------------------------------------ 389c (2) dorosly stroj cywilny
        /// <summary>Tulowia TYLKO dzieciece: sztuki z rosterow IsChildEquipmentTemplate, ktorych nie ma w zadnym zestawie doroslych lordow (IsLordTemplate bez
        /// dziecka / mlodziezy). Szablony mlodziezowe pominiete celowo - zawieraja dorosle suknie (battania_dress_a, vlandian_noble_woman_dress).</summary>
        private static HashSet<string> ChildOnly()
        {
            if (_childOnly != null) return _childOnly;
            var set = new HashSet<string>();
            try
            {
                var adult = new HashSet<string>();
                var rosters = MBObjectManager.Instance.GetObjectTypeList<MBEquipmentRoster>();
                if (rosters != null)
                    foreach (var ro in rosters)
                    {
                        if (ro == null) continue;
                        var c = ro.EquipmentCategories;
                        bool child = (c & EquipmentCategories.IsChildEquipmentTemplate) != 0;
                        bool grown = !child && (c & EquipmentCategories.IsLordTemplate) != 0 && (c & EquipmentCategories.IsTeenagerEquipmentTemplate) == 0;
                        if (!child && !grown) continue;
                        foreach (var e in ro.AllEquipments)
                        {
                            if (e == null) continue;
                            var it = e[EquipmentIndex.Body].Item;
                            if (it != null && it.StringId != null) (child ? set : adult).Add(it.StringId);
                        }
                    }
                _childAll = new HashSet<string>(set);
                set.ExceptWith(adult);
            }
            catch (Exception e) { Stumble("ChildOnly", null, e); }
            if (_childAll == null) _childAll = new HashSet<string>(set);
            _childOnly = set;
            return set;
        }

        /// <summary>389c-p: wszystkie tulowia z rosterow IsChildEquipmentTemplate (takze te, ktore nosza dorosli lordowie: leather_coat, fine_town_tunic,
        /// empire_dress...). Sam taki tulow NIE jest podpisem - patrz ChildSig.</summary>
        private static HashSet<string> ChildAll()
        {
            if (_childAll == null) ChildOnly();
            return _childAll ?? new HashSet<string>();
        }

        /// <summary>389c-p: podpis "stroj cywilny dzieciecy". 1 = scisly (tulow tylko dzieciecy - jak 389c); 2 = szeroki: tulow z rosteru dzieciecego noszony
        /// takze przez doroslych, ale CALY zestaw wyglada jak dzieciecy - bez broni (sloty 0-3), bez konia i rzedu (zestawy dzieciece ROT: 127, zadnego z
        /// koniem ani bronia; dorosle cywilne ROT maja konia i rzad w 422 z 423) - i postac NIE pochodzi z danych (lord z XML nosi to, co mu dano w danych:
        /// data = true, podpisu nie ma). 0 = nie.</summary>
        private static int ChildSig(Hero h, Equipment cv, out bool data)
        {
            data = false;
            var cb = cv != null ? cv[EquipmentIndex.Body].Item : null;
            if (cb == null) return 0;
            string id = cb.StringId ?? "";
            if (ChildOnly().Contains(id)) return 1;
            if (!ChildAll().Contains(id)) return 0;
            for (int i = 0; i < 4; i++) if (!cv[(EquipmentIndex)i].IsEmpty) return 0;
            if (!cv[EquipmentIndex.Horse].IsEmpty || !cv[EquipmentIndex.HorseHarness].IsEmpty) return 0;
            var co = h != null ? h.CharacterObject : null;
            if (co != null && co.IsOriginalCharacter) { data = true; return 0; }
            return 2;
        }

        private static bool GoodCivil(Equipment e, Hero h, bool strict)
        {
            if (e == null || !e.IsCivilian) return false;
            var b = e[EquipmentIndex.Body].Item;
            if (b == null || HeroDress382.Banned(b) || !HeroDress382.Sex(b, h)) return false;
            // 389c-p: strict - tulow spoza wszystkich rosterow dzieciecych (model nie "przebierze" w leather_coat); luzno - jak 389c (tylko nie scisle dzieciecy)
            return !(strict ? ChildAll() : ChildOnly()).Contains(b.StringId ?? "");
        }

        /// <summary>Zestaw cywilny doroslego lorda: jak gra w wieku 18 lat; gdy model nic nie da (albo da zestaw z unikatem / nie dla tej plci) - wlasny
        /// przeglad rosterow lordow kultury i plci (bez wladcow, dzieci, mlodziezy), wybor staly dla bohatera. Zwraca KLON (szablonu nie ruszamy).</summary>
        private static Equipment CivilSet(Hero h)
        {
            Equipment e = null;
            try { e = Campaign.Current.Models.EquipmentSelectionModel.GetEquipmentForHeroComeOfAge(h, Equipment.EquipmentType.Civilian); }
            catch (Exception ex) { Stumble("CivilSet.model", h, ex); }
            if (!GoodCivil(e, h, true))
            {
                var loose = GoodCivil(e, h, false) ? e : null;   // 389c-p: zestaw modelu z tulowiem noszonym takze przez dzieci - dopiero gdy nie ma lepszego
                var looseC = new List<Equipment>();
                e = null;
                if (h.Culture == null && loose == null) return null;
                string cid = h.Culture != null ? (h.Culture.StringId ?? "") : "";
                var cands = new List<Equipment>();
                var rosters = MBObjectManager.Instance.GetObjectTypeList<MBEquipmentRoster>();
                if (rosters == null) return null;
                foreach (var ro in rosters)
                {
                    if (ro == null || h.Culture == null || ro.EquipmentCulture == null || ro.EquipmentCulture.StringId != cid) continue;
                    var c = ro.EquipmentCategories;
                    if ((c & EquipmentCategories.IsLordTemplate) == 0) continue;
                    if ((c & (EquipmentCategories.IsKingdomRulerTemplate | EquipmentCategories.IsChildEquipmentTemplate | EquipmentCategories.IsTeenagerEquipmentTemplate)) != 0) continue;
                    if (((c & EquipmentCategories.IsFemaleTemplate) != 0) != h.IsFemale) continue;
                    foreach (var x in ro.AllEquipments) { if (GoodCivil(x, h, true)) cands.Add(x); else if (GoodCivil(x, h, false)) looseC.Add(x); }
                }
                if (cands.Count == 0)
                {
                    if (loose != null) cands.Add(loose); else cands = looseC;
                }
                if (cands.Count == 0) return null;
                e = cands[(int)((uint)(h.StringId ?? "").GetHashCode() % (uint)cands.Count)];
            }
            var cl = new Equipment(Equipment.EquipmentType.Civilian);
            cl.FillFrom(e, false);
            for (int i = 0; i < 12; i++)
            {
                var it = cl[(EquipmentIndex)i].Item;
                if (it == null) continue;
                bool armour = i >= (int)EquipmentIndex.Head && i <= (int)EquipmentIndex.Cape;
                if (HeroDress382.Banned(it) || (armour && !HeroDress382.Sex(it, h))) cl[(EquipmentIndex)i] = default(EquipmentElement);
            }
            // 389c-p: bron (sloty 0-3) i kon z zestawu cywilnego przez to samo sito co reszta drog bohatera (ItemReq.MeetsHero - umiejetnosc i Prawo Tieru);
            // suknia / stroj dalej bez sita (gra daje suknie kazdej damie). Kon odpadl - rzad tez. Przy wylaczonym HeroGearRequirements bez sita, jak 382 / 389.
            try
            {
                var set0 = Settings.Current;
                var co = h.CharacterObject;
                if (set0 != null && set0.HeroGearRequirements && co != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var it = cl[(EquipmentIndex)i].Item;
                        if (it != null && !ItemReq.MeetsHero(co, it)) cl[(EquipmentIndex)i] = default(EquipmentElement);
                    }
                    var hs = cl[EquipmentIndex.Horse].Item;
                    if (hs != null && !ItemReq.MeetsHero(co, hs)) cl[EquipmentIndex.Horse] = default(EquipmentElement);
                }
                if (cl[EquipmentIndex.Horse].IsEmpty) cl[EquipmentIndex.HorseHarness] = default(EquipmentElement);
            }
            catch (Exception ex) { Stumble("CivilSet.sito", h, ex); }
            return cl.IsCivilian && !cl[EquipmentIndex.Body].IsEmpty ? cl : null;
        }

        private static readonly EquipmentIndex[] CivToBattle = { EquipmentIndex.Body, EquipmentIndex.Leg, EquipmentIndex.Head };

        private static void Civil()
        {
            if (Campaign.Current == null) return;
            int given = 0, left = 0, wait = 0, none = 0, battle = 0, st0 = _stumbles;
            var who = new List<string>();
            var child = ChildOnly();
            var childAll = ChildAll();
            if (childAll.Count == 0) return;
            int wide = 0;
            foreach (var h in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!Counted(h) || h.Occupation != Occupation.Lord) continue;
                    if (h.HeroState == Hero.CharacterStates.Disabled || h.CompanionOf != null) continue;
                    string id = h.StringId;
                    if (string.IsNullOrEmpty(id) || _civ.Contains(id)) continue;
                    var cv = h.CivilianEquipment;
                    if (cv == null) continue;
                    var cb = cv[EquipmentIndex.Body].Item;
                    bool dataC;
                    int sig = cb != null ? ChildSig(h, cv, out dataC) : 0;                 // 389c-p: podpis po calym zestawie (ChildSig), nie po samym tulowiu
                    if (sig == 0) continue;                                               // pusty tulow / lord z danych w takim stroju - tylko miara w spisie
                    if (_civFailed.Contains(id)) { left++; continue; }
                    var be = h.BattleEquipment;
                    // najpierw posag (krok 1 / 389): kto ma jeszcze podpis 389 i nie byl probowany, czeka - 2b ma widziec zestaw bojowy PO probie zbroi
                    if (On && !_done.Contains(id) && !_failed.Contains(id) && !be.IsCivilian && !be.IsStealth && NoWeapon(be) && Body(be) != 3) { wait++; continue; }
                    if (given >= PerDay) { left++; continue; }
                    var p = h.PartyBelongedTo;
                    if (p != null && p.MapEvent != null) { left++; continue; }
                    if (h.PartyBelongedToAsPrisoner != null && h.PartyBelongedToAsPrisoner.MapEvent != null) { left++; continue; }
                    var set = CivilSet(h);
                    if (set == null) { _civFailed.Add(id); none++; left++; continue; }
                    var old = new ItemObject[12];
                    for (int i = 0; i < 12; i++) old[i] = cv[(EquipmentIndex)i].Item;
                    global::Helpers.EquipmentHelper.AssignHeroEquipmentFromEquipment(h, set);
                    _civ.Add(id);
                    given++; _civTotal++;
                    if (sig == 2) wide++;
                    // 2b: tulow bojowy nadal dzieciecy (zbroja nie wyszla) - tulow / nogi / glowa z nowego stroju tam, gdzie nadal lezy sztuka starego stroju
                    bool b2 = false;
                    var bb = be[EquipmentIndex.Body].Item;
                    // 389c-p: takze tulow "wspolny" (noszony i przez dzieci, i przez doroslych), ale tylko gdy to TA SAMA sztuka co w starym stroju cywilnym
                    if (!be.IsCivilian && !be.IsStealth && bb != null && (child.Contains(bb.StringId ?? "") || (bb == old[(int)EquipmentIndex.Body] && childAll.Contains(bb.StringId ?? ""))))
                    {
                        var nb = new Equipment(Equipment.EquipmentType.Battle);
                        nb.FillFrom(be, false);
                        foreach (var s in CivToBattle)
                        {
                            if (set[s].IsEmpty) continue;
                            var cur = nb[s].Item;
                            if (s != EquipmentIndex.Body && cur != null && cur != old[(int)s]) continue;
                            nb[s] = new EquipmentElement(set[s].Item);
                            b2 = true;
                        }
                        if (b2 && !nb.IsCivilian && !nb.IsStealth) { global::Helpers.EquipmentHelper.AssignHeroEquipmentFromEquipment(h, nb); battle++; }
                        else b2 = false;
                    }
                    if (who.Count < GiftNames)
                        who.Add(Nm(h) + " (" + (h.Clan != null ? h.Clan.StringId : "bez rodu") + (h.IsFemale ? ", k, " : ", m, ") + KindName[Kind(h)] + ") " + cb.StringId + " -> " + set[EquipmentIndex.Body].Item.StringId + (b2 ? " (takze zestaw bojowy)" : ""));
                }
                catch (Exception e) { Stumble("Civil", h, e); }
            }
            if (given == 0 && none == 0 && _stumbles == st0) return;   // cicha doba
            Log.Info("Bezbronni (389c): doba " + (int)CampaignTime.Now.ToDays + " - dorosly stroj cywilny: przebrano " + given + " (w tej sesji " + _civTotal + ", rejestr " + _civ.Count
                     + "), z tego podpis szeroki " + wide + ", takze tulow bojowy " + battle + ", zostalo " + left + ", czeka na posag " + wait + ", bez zestawu dla kultury / plci " + none
                     + ", potkniecia " + (_stumbles - st0) + (who.Count > 0 ? ": " + string.Join("; ", who) : "") + (given > who.Count ? " ... (+" + (given - who.Count) + ")" : "") + ".");
        }

        private static string Nm(Hero h)
        {
            try { return h.Name != null ? h.Name.ToString() : h.StringId; } catch { return "?"; }
        }

        private static void Stumble(string where, Hero h, Exception e)
        {
            _stumbles++;
            if (_errs >= ErrMax) return;
            _errs++;
            try { Log.Info("Bezbronni (389): potkniecie " + where + (h != null ? " (" + h.StringId + ")" : "") + " - " + e.GetType().Name + ": " + e.Message); } catch { }
        }
    }
}
