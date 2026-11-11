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
    /// gracza, gdzie dziala 382 (klon). Tu naprawa TRWALA, zeby bohater byl ubrany takze poza bitwa gracza (rozmowy, turnieje, okup, lup).
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
    /// </summary>
    internal static class HeroArms389
    {
        private const int PerDay = 60, NamesPerLine = 40, NamesMax = 400, GiftNames = 20, ErrMax = 3;

        private static readonly HashSet<string> _done = new HashSet<string>();     // w zapisie: kto dostal posag (raz w zyciu)
        private static readonly HashSet<string> _failed = new HashSet<string>();   // sesja: wzorzec nic nie dal
        private static HashSet<string> _childBody;                                 // sesja: sztuki tulowia z szablonow dzieciecych / mlodziezowych
        private static int _lastBare = -1, _total, _errs, _stumbles;

        private static bool On { get { var s = Settings.Current; return s != null && s.AiHeroesLateComingOfAgeKit; } }

        internal static void Reset()
        {
            _done.Clear(); _failed.Clear(); _childBody = null;
            _lastBare = -1; _total = 0; _errs = 0; _stumbles = 0;
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
            try { _failed.Clear(); _childBody = null; _lastBare = -1; Census(true); }
            catch (Exception e) { Log.Error("HeroArms389.OnSession", e); }
        }

        /// <summary>Doba: posag spoznionego dorosniecia (klucz), potem spis - linia tylko przy zmianie liczby.</summary>
        internal static void Daily()
        {
            try { if (On) Gift(); } catch (Exception e) { Log.Error("HeroArms389.Gift", e); }
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
            if (h.Occupation == Occupation.Lord) return (h.StringId ?? "").StartsWith("lord_", StringComparison.Ordinal) ? 4 : 7;
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
            int n = 0, bare = 0, male = 0, female = 0, sig = 0;
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
            var who = new List<string>();
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
                    if (k == null) { _failed.Add(id); noTpl++; sigLeft++; continue; }
                    if (k.N <= 0 || k.Eq == null) { _failed.Add(id); nothing++; sigLeft++; continue; }
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
                    bool still = NoWeapon(h.BattleEquipment);
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
                     + ", wzorzec nic nie dal " + nothing + ", potkniecia " + (_stumbles - st0) + (who.Count > 0 ? ": " + string.Join("; ", who) : "") + (given > who.Count ? " ... (+" + (given - who.Count) + ")" : "") + ".");
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
