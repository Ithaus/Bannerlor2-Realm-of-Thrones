using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// GEARFIT - SPRZET ZGODNY Z UMIEJETNOSCIAMI I KULTURA, DLA WSZYSTKICH JEDNOSTEK (Jeff 15.10 ok. 12:15: "rekrutuje ludzi, maja uzbrojenie, ktorego
    /// nie moga uzywac, bo nie maja umiejetnosci - oddaj bron"; lista na noc 15/16.10 pkt 2: "sprzet WSZYSTKICH jednostek zgodny z umiejetnoscia
    /// i kultura (przy poborze oddaja sprzet do zbrojowni, bo nie moga uzywac, i walcza piesciami)").
    ///
    /// Log 15.10 [12:03:43] (bitwa gracza, 16 ludzi): 2 bez broni po przydziale DTE i 3x pointed_falchion_sword_t4 zabrane przy spawnie przez straz
    /// "nic z niczego" - 5 walczylo piesciami. Zrodla zlych sztuk (rozpoznanie noc 15/16.10): zakup notabla dla ochotnika (VolunteerKit - ten sam typ,
    /// tier nie nizszy, bez sprawdzenia umiejetnosci: po prawie tieru broni CrashScribe sztuka o tier wyzej to +35 wymogu), komplet przeniesiony przy
    /// zamianie rodowej (HouseLevies), weterani z karczmy i przenoszeni ludzie (GarrisonArmory.Pick - wyzszy tier bez sprawdzenia).
    ///
    /// JEDEN predykat "czy ten zolnierz tego uzyje" (ForTroop): umiejetnosc (ItemReq.Meets - bron i tarcza wedlug klasy, pancerz Atletyka, kon i rzad
    /// Jazda), bron z siodla u jezdzca (MenUpgrade.MountOk; sztuka z wlasnego wzorca zawsze przechodzi) i kultura (CultureOk). Kultura dozwolona: ta sama,
    /// przedmiot bez kultury albo neutral_culture, ta sama strona Waskiego Morza, styl KREWNY (ROT sam ubiera nim te kulture w co najmniej 3 slotach wzorcow
    /// i 2% jej slotow - liczone z ZYWYCH wzorcow, po prawach i zamianie 175 CrashScribe) albo sztuka z wlasnego wzorca. Wierzchowce - MountLaw.
    ///
    /// Gdzie dziala: zakup notabla (VolunteerKit.BuyCore), przekazanie kompletu do zbrojowni (RecruitKit.FitHandover - nieuzywalne kupione notabl
    /// sprzedaje, nieuzywalny dobytek czlowiek sprzedaje do sakiewki ludzi, w zamian rzecz z domu), komplety przenoszonych ludzi i weteranow
    /// (GarrisonArmory.PlanKits), dobytek tieru 1 z DTE (postfiks GetRecruitEquipments), kultura zakupow AI i ludzi gracza (AiGear.Lift, MenUpgrade.Pick,
    /// MenPurse.BuyPlayerGaps), straz Waskiego Morza w CrashScribe (CultureOk przez refleksje) i PODLOGA BRONI w bitwie (FloorPostfix, Priority.Last -
    /// po SkillLawWard i RealmWard): kto po strazach nie ma zadnej broni, bierze bron biala ze zbrojowni swojej partii - najpierw uzywalna dozwolonej
    /// kultury, potem uzywalna obcej; ponad umiejetnosc tylko przy GearFitFloorOverSkill (DECYZJA CLAUDE noc 15/16.10: domyslnie wylaczone - Jeff 14.09
    /// "jak nic nie pasuje, to nic nie zaklada"; ZASADA NADRZEDNA 29.08 "umiejetnosci sa swiete"). Nic z niczego: sztuka podlogi pochodzi z puli
    /// rozdzielacza DTE (ConsumeEquipmentToAssign), kazde zloto ma platnika (kasa miasta) i odbiorce (notabl, sakiewka ludzi).
    ///
    /// Poprawki po recenzji (noc 15/16.10): kto co nosi (QuartermasterLaw.MeetsFitOf = ForTroop) liczy ten sam predykat u ludzi gracza, lordow AI i zalog
    /// (nadwyzki, naprawy AI, koszyki dozbrajania, wklady do zalogi, procent gotowosci zalogi), a zakupy (AiGear.Lift, MenPurse.BuyPlayerGaps - AnyFitFor) ten sam,
    /// takze z siodlem - inaczej sztuka kupiona na brak jest nazajutrz "nieuzywalna" nadwyzka (petla kupno -> skup). Kontrola na wejsciu do zbrojowni
    /// (CheckEntry), x odrzuconych liczone w chwili decyzji, filtr kompletu t1 dopiero od startu sesji (SessionStart), obca bron w podlodze za kluczem
    /// GearFitFloorForeign.
    /// Bez zapisu w SyncData (stan sesji); Reset z konstruktora ArmouryBehavior; linie "GearFit (start sesji)" i "GearFit: dzien N" (Daily).
    /// PUNKTY STYKU dla czesci 2 (zbrojownia gracza, przekazanie pkt 15): ForTroop / CultureOk / AnyCultureOk - ten sam predykat dla wymiany sprzetu
    /// gracza z ludzmi i sprzedazy ich rzeczy; RecruitKit.FitHandover - miejsce, w ktorym rzeczy zolnierza wchodza do zbrojowni gracza (PlayerVolunteer /
    /// PlayerMerc -> PlayerRecruit.AddToStores); PlayerNotice - komunikat gracza po oknie werbunku.
    /// </summary>
    internal static class GearFit
    {
        // ------------------------------------------------------------ klucze
        internal static bool On { get { var s = Settings.Current; return s != null && s.GearFitKits; } }
        internal static bool CultureOn { get { var s = Settings.Current; return s != null && s.GearFitCulture; } }
        internal static bool FloorOn { get { var s = Settings.Current; return s != null && s.GearFitWeaponFloor; } }
        internal static bool FloorOverSkillOn { get { var s = Settings.Current; return s != null && s.GearFitFloorOverSkill; } }
        internal static bool RealmFromStoresOn { get { var s = Settings.Current; return s != null && s.GearFitRealmFromStores; } }
        // poprawki po recenzji (noc 15/16.10): drugie przejscie podlogi (bron obcej kultury) osobnym kluczem - to zasada Jeffa (16.09: obca bron zdejmowana),
        // nie decyzja techniczna; bez niego czlowiek bez innej broni moze dostac z polki te sama bron zza morza, ktora RealmWard chwile wczesniej zdjal
        internal static bool FloorForeignOn { get { var s = Settings.Current; return s != null && s.GearFitFloorForeign; } }
        // f29-arakh (Jeff 19.10): krewna para dopuszcza tylko sztuki, ktore wzorce kultury zolnierza naprawde nosza (off = cala kultura przedmiotu, jak dotad)
        internal static bool KinItemOn { get { var s = Settings.Current; return s != null && s.GearFitKinItemLevel; } }

        internal const int WhySkill = 1, WhySaddle = 2, WhyCulture = 3;
        internal const int KinMinSlots = 3;
        internal const float KinMinShare = 0.02f;
        internal const int PathAi = 0, PathPlayer = 1, PathGarrison = 2;

        private static readonly object _lock = new object();
        // pary krewne i (f29-arakh) spis sztuk w JEDNEJ niezmiennej migawce: jeden zapis w BuildKin, jeden odczyt w KinOk - watek DTE (DoAssignAsync)
        // nie zlapie par z jednej budowy, a sztuk z innej (poprawka z przegladu f29); null - jeszcze nie zbudowane
        private sealed class KinSnap
        {
            internal readonly Dictionary<string, HashSet<string>> Pairs;     // kultura wojska -> kultury przedmiotow z DRUGIEJ strony morza, ktore ROT sam daje tej kulturze
            internal readonly Dictionary<string, HashSet<ItemObject>> Items; // f29-arakh: kultura wojska -> sztuki z DRUGIEJ strony morza z jej zywych wzorcow (te same sloty)
            internal KinSnap(Dictionary<string, HashSet<string>> pairs, Dictionary<string, HashSet<ItemObject>> items) { Pairs = pairs; Items = items; }
        }
        private static volatile KinSnap _kin;
        private static readonly List<KeyValuePair<string, int>> _kinTop = new List<KeyValuePair<string, int>>();
        private static readonly Dictionary<CharacterObject, HashSet<ItemObject>> _tpl = new Dictionary<CharacterObject, HashSet<ItemObject>>();
        private static bool _census;
        // poprawki po recenzji: DTE OnGameLoaded -> OnMobilePartyCreated -> GetRecruitEquipments biegnie PRZED OnSessionLaunched, czyli przed prawami
        // CrashScribe (WeightLaw, ArmorTierLaw, WeaponTierLaw, SkillSinew) - wtedy filtr kompletu t1 nie dziala (_live), liczone w _preSkip (linia startu)
        private static bool _live;
        private static int _preSkip;
        // kontrola w miejscu wejscia do zbrojowni (CheckEntry): sztuki "bez targu" ostatniego FitHandover - dozwolone nieuzywalne (jak dotad z czlowiekiem)
        private static int _pendKeep;
        // braki ludzi gracza (MenPurse.BuyPlayerGaps): egzemplarz (id|mod) -> doba zakupu; ten sam egzemplarz wolny przy brakach albo sprzedany jako
        // nadwyzka w ciagu GapIdleDays = zakup "bez skutku" (petla kupno -> skup, recenzja noc 15/16.10)
        private static readonly Dictionary<string, int> _gapBuy = new Dictionary<string, int>();
        private static readonly List<string> _gapIdleIds = new List<string>();
        internal const int GapIdleDays = 14;

        // latki DTE
        private static FieldInfo _fPool, _fAssigns, _fParty, _fMission, _fEq;
        private static MethodInfo _consume, _setEq, _getChar;
        private static bool _wFloor;
        private static string _wiring = "BRAK (ApplyAll nie biegl)";

        // liczniki doby
        private static int _dRej, _dRejSkill, _dRejSaddle, _dRejCulture, _dNative, _dRevNoFit, _dExtraNoFit;
        private static int _dMenAi, _dMenPl, _dMenGar, _dN, _dU, _dX, _dX1, _dX2, _dX3, _dKeep, _dHome, _dCtl;
        private static long _dG1, _dG2;
        private static int _dLordN, _dEntry, _dEntryPcs, _dEntryKeep, _dGapBuy, _dGapIdle, _dAiForeign, _dGarForeign;
        private static long _dLordG;
        private static int _dPlan, _dTpl, _dDrop;
        private static int _dMis, _dBare, _dB1, _dB2, _dB3, _dNone, _dNoSlot, _dKept;
        private static int _stum, _stumAll;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        // liczniki jednej misji (dopisek linii "Bitwa gracza (sklad7b-p, start)"): Twoi ludzie (P) i wszystkie partie
        private static WeakReference _mMission;
        private static int _mBareP, _mB1P, _mB2P, _mB3P, _mNoneP, _mBare, _mGot, _mNone;
        // komunikat gracza po oknie werbunku
        private static int _plPcs, _plMen, _plMenGold, _plNotGold, _plShelf;
        // ekwipunki z bronia ponad umiejetnosc z ostatniej deski podlogi (DragonUnmount jej nie podmienia) - maska slotow 0-3
        private static readonly Dictionary<Equipment, int> _floorKept = new Dictionary<Equipment, int>(RefEq.I);

        private sealed class RefEq : IEqualityComparer<Equipment>
        {
            internal static readonly RefEq I = new RefEq();
            public bool Equals(Equipment a, Equipment b) { return ReferenceEquals(a, b); }
            public int GetHashCode(Equipment o) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o); }
        }

        internal static void Reset()
        {
            lock (_lock) { _kin = null; _kinTop.Clear(); _tpl.Clear(); _floorKept.Clear(); }
            _census = false; _stumAll = 0; _errSites.Clear(); _mMission = null;
            _live = false; _preSkip = 0; _pendKeep = 0; _gapBuy.Clear();
            ClearDay(); ClearMission();
            _plPcs = _plMen = _plMenGold = _plNotGold = _plShelf = 0;
        }

        /// <summary>Start sesji (ArmouryBehavior.OnSessionLaunched, po McmSettings.Apply): od teraz filtr kompletu t1 (FitHomeKit) czynny; pamiec wzorcow
        /// i par krewnych od nowa (zbudowane przy OnGameLoaded DTE mogly widziec swiat przed startem sesji). Kolejnosc OnSessionLaunched Armoury i CrashScribe
        /// nieznana - miedzy nimi nikt nie werbuje (werbunek i nowe partie tylko w tikach), a ItemReq.Meets czyta wymogi i umiejetnosci w chwili wolania.</summary>
        internal static void SessionStart()
        {
            lock (_lock) { _tpl.Clear(); _kin = null; _kinTop.Clear(); }
            _live = true;
        }

        private static void ClearDay()
        {
            _dRej = _dRejSkill = _dRejSaddle = _dRejCulture = _dNative = _dRevNoFit = _dExtraNoFit = 0;
            _dMenAi = _dMenPl = _dMenGar = _dN = _dU = _dX = _dX1 = _dX2 = _dX3 = _dKeep = _dHome = _dCtl = 0; _dG1 = _dG2 = 0;
            _dLordN = _dEntry = _dEntryPcs = _dEntryKeep = _dGapBuy = _dGapIdle = _dAiForeign = _dGarForeign = 0; _dLordG = 0; _gapIdleIds.Clear();
            _dPlan = _dTpl = _dDrop = 0;
            _dMis = _dBare = _dB1 = _dB2 = _dB3 = _dNone = _dNoSlot = _dKept = 0;
            _stum = 0;
        }

        private static void ClearMission() { _mBareP = _mB1P = _mB2P = _mB3P = _mNoneP = _mBare = _mGot = _mNone = 0; }

        /// <summary>Wyjatek przy jednym obiekcie: liczony (linia doby), Log.Error raz na miejsce w sesji - nic nie gasnie (CLAUDE.md pkt 7).</summary>
        private static void Stumble(string where, Exception e)
        {
            _stum++; _stumAll++;
            try { bool first; lock (_errSites) first = _errSites.Add(where); if (first) Log.Error("GearFit." + where, e); } catch { }
        }

        // ------------------------------------------------------------ predykat
        /// <summary>Lustro Mends.EssosCultures (CrashScribe) - trzymac w zgodzie: lista MountLaw.Essos + steppe_bandits (MountLaw bez zmian - rydwany i wielblady).</summary>
        internal static bool IsEssos(string c) { return c != null && (MountLaw.IsEssosCulture(c) || c == "steppe_bandits"); }

        /// <summary>Czy ten zolnierz uzyje tej sztuki. kit = sztuka kompletu (slot nieznany): u jezdzca bron musi dac sie uzyc z siodla.
        /// why: 1 umiejetnosc, 2 siodlo, 3 kultura. Bohaterowie zawsze true (wlasne sito ItemReq.MeetsHero / HeroGear). Wyjatek - true (jak ItemReq).</summary>
        internal static bool ForTroop(CharacterObject troop, ItemObject it, bool kit, out int why)
        {
            why = 0;
            if (troop == null || it == null) return true;
            try
            {
                if (troop.IsHero) return true;
                if (!ItemReq.Meets(troop, it)) { why = WhySkill; return false; }
                if (kit && troop.IsMounted && it.HasWeaponComponent && !MenUpgrade.MountOk(it) && !TemplateHas(troop, it)) { why = WhySaddle; return false; }
                if (CultureOn && !CultureOk(troop, it)) { why = WhyCulture; return false; }
                return true;
            }
            catch (Exception e) { Stumble("ForTroop", e); return true; }
        }

        internal static bool ForTroop(CharacterObject troop, ItemObject it, bool kit) { int why; return ForTroop(troop, it, kit, out why); }

        /// <summary>Kultura sztuki wobec kultury zolnierza. Przy GearFitCulture: ta sama, bez kultury, ta sama strona morza, styl krewny albo wlasny wzorzec;
        /// przy wylaczonym - tylko regula strony morza z wyjatkiem wlasnego wzorca (jak RealmWard przed GearFit). Kon - MountLaw.Allowed; rzad i proporzec
        /// zawsze. Wolane takze z CrashScribe (Mends.RealmWard) przez refleksje - sygnatury nie zmieniac.</summary>
        internal static bool CultureOk(CharacterObject troop, ItemObject it)
        {
            if (troop == null || it == null) return true;
            try
            {
                var t = it.ItemType;
                if (t == ItemObject.ItemTypeEnum.Horse) return MountLaw.Allowed(troop, it);
                if (t == ItemObject.ItemTypeEnum.HorseHarness || t == ItemObject.ItemTypeEnum.Banner) return true;
                string ic = it.Culture != null ? it.Culture.StringId : null;
                string tc = troop.Culture != null ? troop.Culture.StringId : null;
                if (string.IsNullOrEmpty(ic) || string.IsNullOrEmpty(tc) || ic == "neutral_culture" || ic == tc) return true;
                if (IsEssos(ic) == IsEssos(tc)) return true;   // w obrebie jednej strony morza domy dziela sie lupem (Jeff 16.09)
                if (CultureOn && KinOk(tc, ic, it)) return true;   // f29-arakh: przy GearFitKinItemLevel - sztuka po sztuce (wylaczony = sama para, jak dotad)
                return TemplateHas(troop, it);
            }
            catch (Exception e) { Stumble("CultureOk", e); return true; }
        }

        /// <summary>Czy ktos z tych oddzialow moze nosic sztuke tej kultury (zakupy ludzi gracza dla kilku oddzialow naraz); pusta lista - true.</summary>
        internal static bool AnyCultureOk(IEnumerable<CharacterObject> troops, ItemObject it)
        {
            if (troops == null) return true;
            bool any = false;
            foreach (var c in troops)
            {
                if (c == null) continue;
                any = true;
                if (CultureOk(c, it)) return true;
            }
            return !any;
        }

        /// <summary>Czy ktos z tych oddzialow uzyje sztuki wedlug pelnego predykatu ForTroop (umiejetnosc, siodlo przy kit, kultura) - ten sam, ktorym
        /// QuartermasterLaw.MeetsFitOf liczy, kto co nosi; pusta lista - true. Poprawka po recenzji (noc 15/16.10, uwaga krytyczna): zakupy brakow ludzi gracza
        /// (MenPurse.BuyPlayerGaps) sprawdzaly tylko kulture, a dopasowanie takze siodlo - dlugi luk dla konnego lucznika byl kupowany co dobe i sprzedawany
        /// przy wjezdzie jako nieuzywalny.</summary>
        internal static bool AnyFitFor(IEnumerable<CharacterObject> troops, ItemObject it, bool kit)
        {
            if (troops == null) return true;
            bool any = false;
            foreach (var c in troops)
            {
                if (c == null) continue;
                any = true;
                if (ForTroop(c, it, kit)) return true;
            }
            return !any;
        }

        /// <summary>Nadwyzki AI i zalog (MenPurse.SellArmorySurplus): sztuka, ktorej kultury nie nosi zaden oddzial partii (CultureOk) - tylko do licznika doby.</summary>
        internal static bool ForeignToAll(TaleWorlds.CampaignSystem.Roster.TroopRoster roster, ItemObject it)
        {
            if (!CultureOn || roster == null || it == null) return false;
            try
            {
                bool any = false;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var c = el.Character;
                    if (c == null || c.IsHero || el.Number <= 0) continue;
                    any = true;
                    if (CultureOk(c, it)) return false;
                }
                return any;
            }
            catch (Exception e) { Stumble("ForeignToAll", e); return false; }
        }

        /// <summary>0 - kultura zolnierza albo sztuka bez kultury, 1 - inna (dozwolona) kultura. Do preferencji wlasnej kultury przy zakupie notabla.</summary>
        internal static int CultureRank(CharacterObject troop, ItemObject it)
        {
            try
            {
                string ic = it != null && it.Culture != null ? it.Culture.StringId : null;
                if (string.IsNullOrEmpty(ic) || ic == "neutral_culture") return 0;
                return troop != null && troop.Culture != null && troop.Culture.StringId == ic ? 0 : 1;
            }
            catch { return 0; }
        }

        /// <summary>Czy przedmiot siedzi we wlasnym szablonie jednostki (sloty 0-11 wszystkich zestawow bojowych) - jak Mends.TemplateHas.</summary>
        internal static bool TemplateHas(CharacterObject co, ItemObject it)
        {
            if (co == null || it == null) return false;
            HashSet<ItemObject> set;
            lock (_lock)
            {
                if (!_tpl.TryGetValue(co, out set))
                {
                    set = new HashSet<ItemObject>();
                    try
                    {
                        foreach (var eq in co.BattleEquipments)
                        {
                            if (eq == null) continue;
                            for (int s = 0; s < 12; s++) { var x = eq[(EquipmentIndex)s].Item; if (x != null) set.Add(x); }
                        }
                    }
                    catch { }
                    _tpl[co] = set;
                }
            }
            return set.Contains(it);
        }

        private static bool Kin(string tc, string ic)
        {
            EnsureKin();
            var k = _kin;
            if (k == null) return true;   // przed startem gry (wzorce jeszcze bez praw CrashScribe) - bez odrzucen za kulture
            HashSet<string> s;
            return k.Pairs.TryGetValue(tc, out s) && s.Contains(ic);
        }

        /// <summary>f29-arakh (Jeff 19.10, arakh u Dornijczykow): para krewna (tc, ic), a przy GearFitKinItemLevel dodatkowo TA sztuka we wzorcach kultury tc
        /// (sloty jak pary krewne). Pary i sztuki z jednej migawki (jeden odczyt _kin). Przed zbudowaniem - true, jak Kin (bez odrzucen przed startem gry).
        /// Klucz wylaczony = dokladnie Kin(tc, ic).</summary>
        private static bool KinOk(string tc, string ic, ItemObject it)
        {
            EnsureKin();
            var k = _kin;
            if (k == null) return true;
            HashSet<string> s;
            if (!k.Pairs.TryGetValue(tc, out s) || !s.Contains(ic)) return false;
            if (!KinItemOn) return true;
            HashSet<ItemObject> hs;
            return k.Items.TryGetValue(tc, out hs) && hs.Contains(it);
        }

        private static void EnsureKin()
        {
            if (_kin != null) return;
            try { if (Campaign.Current == null || !Campaign.Current.GameStarted) return; } catch { return; }
            lock (_lock) { if (_kin == null) BuildKin(); }
        }

        private static bool Troop(CharacterObject c)
        {
            return c != null && !c.IsHero && !c.IsTemplate && c.Culture != null && !string.IsNullOrEmpty(c.Culture.StringId);
        }

        /// <summary>Pary krewne z zywych wzorcow (wolane pod _lock): sloty 0-9 bez proporca zestawow bojowych wszystkich nie-bohaterow; para (kultura wojska,
        /// kultura przedmiotu z drugiej strony morza) krewna, gdy n >= KinMinSlots i n >= KinMinShare slotow tej kultury (jak skrypt rozpoznania kin.py).</summary>
        private static void BuildKin()
        {
            var n = new Dictionary<string, Dictionary<string, int>>();
            var tot = new Dictionary<string, int>();
            var kinItems = new Dictionary<string, HashSet<ItemObject>>();   // f29-arakh
            try
            {
                foreach (var c in MBObjectManager.Instance.GetObjectTypeList<CharacterObject>())
                {
                    if (!Troop(c)) continue;
                    string tc = c.Culture.StringId;
                    foreach (var eq in c.BattleEquipments)
                    {
                        if (eq == null) continue;
                        for (int s = 0; s < 10; s++)
                        {
                            if (s == 4) continue;
                            var it = eq[(EquipmentIndex)s].Item;
                            if (it == null) continue;
                            int t; tot.TryGetValue(tc, out t); tot[tc] = t + 1;
                            string ic = it.Culture != null ? it.Culture.StringId : null;
                            if (string.IsNullOrEmpty(ic) || ic == "neutral_culture" || ic == tc) continue;
                            Dictionary<string, int> d; if (!n.TryGetValue(tc, out d)) n[tc] = d = new Dictionary<string, int>();
                            int v; d.TryGetValue(ic, out v); d[ic] = v + 1;
                            if (IsEssos(ic) != IsEssos(tc))
                            {
                                HashSet<ItemObject> hs; if (!kinItems.TryGetValue(tc, out hs)) kinItems[tc] = hs = new HashSet<ItemObject>();
                                hs.Add(it);
                            }
                        }
                    }
                }
            }
            catch (Exception e) { Stumble("BuildKin", e); }
            var kin = new Dictionary<string, HashSet<string>>();
            _kinTop.Clear();
            foreach (var kv in n)
            {
                int total; tot.TryGetValue(kv.Key, out total);
                foreach (var p in kv.Value)
                {
                    if (IsEssos(p.Key) == IsEssos(kv.Key)) continue;   // ta sama strona morza - i tak dozwolone
                    if (p.Value < KinMinSlots || p.Value < KinMinShare * total) continue;
                    HashSet<string> set; if (!kin.TryGetValue(kv.Key, out set)) kin[kv.Key] = set = new HashSet<string>();
                    set.Add(p.Key);
                    _kinTop.Add(new KeyValuePair<string, int>(kv.Key + "<-" + p.Key, p.Value));
                }
            }
            _kinTop.Sort((a, b) => b.Value.CompareTo(a.Value));
            _kin = new KinSnap(kin, kinItems);   // jeden zapis - pary i spis sztuk razem
        }

        // ------------------------------------------------------------ liczniki z innych plikow
        internal static void NoteBuyReject(int why)
        {
            _dRej++;
            if (why == WhySkill) _dRejSkill++; else if (why == WhySaddle) _dRejSaddle++; else if (why == WhyCulture) _dRejCulture++;
        }
        internal static void NoteNativePick() { _dNative++; }
        internal static void NoteRevertNoFit() { _dRevNoFit++; }
        internal static void NoteExtraNoFit() { _dExtraNoFit++; }
        internal static void NotePlanSkip() { _dPlan++; }

        /// <summary>RecruitKit.FitHandover: jeden czlowiek - sztuk n, uzywalnych u, x odrzuconych na targ (liczone w chwili decyzji, NIEZALEZNIE od wyniku
        /// sprzedazy - ksiega x = x1 + x2 + x3), kupione nieuzywalne x1 sprzedane notablowi za g1, dobytek x2 za g2 do sakiewki ludzi (zaloga bez sakiewki
        /// - panu osady, NoteOwnToLord), x3 na polke bez zaplaty, keep bez targu (jak dotad), home zamiennikow z domu. keep - dozwolone nieuzywalne dla
        /// kontroli na wejsciu do zbrojowni (CheckEntry) zaraz po tym przekazaniu.</summary>
        internal static void NoteHandover(int path, int n, int u, int x, int x1, int g1, int x2, int g2, int x3, int keep, int home)
        {
            if (path == PathPlayer) _dMenPl++; else if (path == PathGarrison) _dMenGar++; else _dMenAi++;
            _dN += n; _dU += u; _dX += x; _dX1 += x1; _dG1 += g1; _dX2 += x2; _dG2 += g2; _dX3 += x3; _dKeep += keep; _dHome += home;
            _pendKeep = keep;
            if (path == PathPlayer && x1 + x2 + x3 > 0) { _plPcs += x1 + x2 + x3; _plMen++; _plMenGold += g2; _plNotGold += g1; _plShelf += x3; }
        }

        /// <summary>RecruitKit.SellOwn: zaloga bez sakiewki (GarrisonPurseEnabled off) - zloto za nieuzywalny dobytek dostaje pan osady (regula 171, jak
        /// nadwyzki zalog w MenPurse.SellArmorySurplus); sztuka liczona dalej w x2 (zaplacona).</summary>
        internal static void NoteOwnToLord(int gold) { _dLordN++; _dLordG += Math.Max(0, gold); }

        /// <summary>
        /// Kontrola w miejscu WEJSCIA do zbrojowni (poprawka po recenzji: dotad liczona w FitHandover na sztukach, ktore przed chwila przeszly ten sam
        /// ForTroop - zawsze 0). Wolane tuz przed petla dodawania (RecruitKit.OnRecruited, RecruitKit.OnGarrisonTook, PlayerRecruit.Settle) na liscie, ktora
        /// NAPRAWDE idzie do zbrojowni: ile sztuk ten zolnierz nie uzyje, ponad sztuki "bez targu" ostatniego przekazania (_pendKeep - jak dotad idzie
        /// z czlowiekiem). Ma byc 0; lapie kazda droge obok filtra (wzorzec przy RecruitBringsWhatHeHas off, sztuki dolozone po FitHandover, dobytek t1 zalog).
        /// </summary>
        internal static void CheckEntry(CharacterObject troop, List<EquipmentElement> items)
        {
            int allow = _pendKeep;
            _pendKeep = 0;
            if (!On || troop == null || items == null) return;
            try
            {
                if (troop.IsHero) return;
                int bad = 0, pcs = 0;
                foreach (var e in items)
                {
                    if (e.Item == null) continue;
                    pcs++;
                    if (!ForTroop(troop, e.Item, true)) bad++;
                }
                _dEntry++; _dEntryPcs += pcs;
                int ok = Math.Min(bad, allow);
                _dEntryKeep += ok;
                _dCtl += bad - ok;
            }
            catch (Exception e) { Stumble("CheckEntry", e); }
        }

        private static int Today() { try { return (int)CampaignTime.Now.ToDays; } catch { return 0; } }

        /// <summary>MenPurse.BuyPlayerGaps: egzemplarz kupiony przez ludzi gracza na brak (klucz id|mod - QuartermasterLaw.KeyOf).</summary>
        internal static void NoteGapBuy(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            _gapBuy[key] = Today(); _dGapBuy++;
        }

        /// <summary>Egzemplarz kupiony na brak w ciagu GapIdleDays lezy wolny przy brakach tego typu (BuyPlayerGaps) albo idzie do kupca jako nadwyzka
        /// (SellPlayerSurplus) - zakup bez skutku (ma byc 0; po bitwie ze stratami mozliwy uczciwy przypadek - prog DECYZJA). Raz na egzemplarz.</summary>
        internal static void GapIdle(string key, int n)
        {
            int d;
            if (n <= 0 || string.IsNullOrEmpty(key) || _gapBuy.Count == 0 || !_gapBuy.TryGetValue(key, out d)) return;
            _gapBuy.Remove(key);
            if (Today() - d > GapIdleDays) return;
            _dGapIdle += n;
            if (_gapIdleIds.Count < 4 && !_gapIdleIds.Contains(key)) _gapIdleIds.Add(key);
        }

        /// <summary>MenPurse.SellArmorySurplus: sprzedane jako nadwyzka sztuki kultury, ktorej nie nosi zaden oddzial partii (od poprawki AI i zalogi licza
        /// kto co nosi przez MeetsFitOf - takie sztuki ida do kupca pierwsze).</summary>
        internal static void NoteAiForeignSold(int n, bool garrison) { if (n <= 0) return; if (garrison) _dGarForeign += n; else _dAiForeign += n; }

        /// <summary>Koniec okna werbunku / najmu gracza (PlayerRecruit.Settle): jedno zdanie w grze, gdy rekruci oddali rzeczy, ktorych nie uzyja.</summary>
        internal static void PlayerNotice()
        {
            try
            {
                if (_plPcs <= 0) return;
                var sb = new StringBuilder();
                sb.Append(_plPcs).Append(_plPcs == 1 ? " piece" : " pieces").Append(" of kit your new men cannot use (skill, saddle or people) went to the market instead of your stores");
                var paid = new List<string>();
                if (_plMenGold > 0) paid.Add(_plMenGold + " denars to the men's purse");
                if (_plNotGold > 0) paid.Add(_plNotGold + " to the notables who bought them");
                if (paid.Count > 0) sb.Append(" - ").Append(string.Join(", ", paid.ToArray()));
                if (_plShelf > 0) sb.Append(" (").Append(_plShelf).Append(" unpaid - the merchants had no coin)");
                sb.Append(". They took their own plainer gear from home where they had it.");
                Log.Player(sb.ToString());
                Log.Info("GearFit: rekruci gracza - " + _plMen + " ludzi oddalo " + _plPcs + " szt. nieuzywalnych (do sakiewki ludzi " + _plMenGold + " zl, notablom " + _plNotGold
                         + " zl, na polke bez zaplaty " + _plShelf + ").");
            }
            catch (Exception e) { Stumble("PlayerNotice", e); }
            finally { _plPcs = _plMen = _plMenGold = _plNotGold = _plShelf = 0; }
        }

        // ------------------------------------------------------------ latki DTE
        internal static void ApplyAll(Harmony h)
        {
            string rec = "BRAK", floor = "BRAK";
            try
            {
                var tRec = QuartermasterLaw.FindType("DynamicTroopEquipmentReupload.Patches.RecruitmentPatch");
                // pulapka z 15.10: metoda DECLARED w typie (inaczej Harmony wpina bez bledu, a postfiks nie jest wolany)
                var m = tRec != null ? AccessTools.DeclaredMethod(tRec, "GetRecruitEquipments", new[] { typeof(CharacterObject) }) : null;
                if (m != null) { h.Patch(m, postfix: new HarmonyMethod(typeof(GearFit), nameof(RecruitPostfix))); rec = "tak"; }
                else rec = tRec == null ? "BRAK (DTE nieobecne)" : "BRAK (zmienione DTE)";
            }
            catch (Exception e) { rec = "BRAK (wyjatek)"; Log.Error("GearFit.ApplyAll(GetRecruitEquipments)", e); }
            try
            {
                var tDist = QuartermasterLaw.FindType("DynamicTroopEquipmentReupload.PartyEquipmentDistributor");
                var tAssign = QuartermasterLaw.FindType("DynamicTroopEquipmentReupload.Assignment");
                if (tDist != null && tAssign != null)
                {
                    _fPool = AccessTools.Field(tDist, "_equipmentToAssign");
                    _fAssigns = AccessTools.Field(tDist, "Assignments");
                    _fParty = AccessTools.Field(tDist, "_party");
                    _fMission = AccessTools.Field(tDist, "_mission");
                    _consume = AccessTools.DeclaredMethod(tDist, "ConsumeEquipmentToAssign");
                    _fEq = AccessTools.Field(tAssign, "Equipment");
                    _setEq = AccessTools.DeclaredMethod(tAssign, "SetEquipment");
                    _getChar = AccessTools.PropertyGetter(tAssign, "Character");
                    var mA = AccessTools.DeclaredMethod(tDist, "DoAssignAsync");
                    if (mA != null && _fPool != null && _fAssigns != null && _fParty != null && _fMission != null && _consume != null && _fEq != null && _setEq != null && _getChar != null)
                    {
                        // Priority.Last: po strazach CrashScribe (SkillLawWard, RealmWard) - podloga widzi koncowy przydzial
                        h.Patch(mA, postfix: new HarmonyMethod(typeof(GearFit), nameof(FloorPostfix)) { priority = Priority.Last });
                        _wFloor = true; floor = "tak";
                    }
                    else floor = "BRAK (zmienione DTE)";
                }
                else floor = "BRAK (DTE nieobecne)";
            }
            catch (Exception e) { floor = "BRAK (wyjatek)"; Log.Error("GearFit.ApplyAll(DoAssignAsync)", e); }
            _wiring = "DTE GetRecruitEquipments " + rec + ", podloga broni DoAssignAsync " + floor;
            Log.Info("GearFit: latki przy starcie gry - " + _wiring + ".");
        }

        /// <summary>Postfiks DTE RecruitmentPatch.GetRecruitEquipments(character): komplet DTE rekruta (tier 1 - wlasny dobytek; zakladanie zbrojowni
        /// partii; MobilePartyExtension). Sztuka, ktorej zolnierz nie uzyje (RandomizeStartingEquipment losuje pancerz tego samego tieru, ktory po prawie
        /// wagi bywa ponad Atletyke), zastapiona sztuka tego samego typu z jego wzorca, ktora przechodzi; inaczej zdjeta (DTE nie stworzy jej z niczego).</summary>
        public static void RecruitPostfix(CharacterObject __0, ref List<EquipmentElement> __result)
        {
            if (__0 == null || __result == null || __result.Count == 0 || !On) return;
            try { FitHomeKit(__0, __result); }
            catch (Exception e) { Stumble("RecruitPostfix", e); }
        }

        /// <summary>Komplet "z domu" (tier 1, wzorzec): sztuka nieuzywalna -> sztuka wzorca tego samego typu, ktora przechodzi i jeszcze jej nie ma; inaczej zdjeta.
        /// Ta sama regula dla DTE (RecruitPostfix) i autowerbunku zalog tieru 1 (RecruitKit.OnGarrisonTook).</summary>
        internal static void FitHomeKit(CharacterObject c, List<EquipmentElement> l)
        {
            if (c == null || l == null || c.IsHero) return;
            // przed startem sesji (OnGameLoaded DTE zaklada zbrojownie partii bez wpisu) - wymogi sprzed praw CrashScribe: bez filtra, jak przed GearFit
            if (!_live) { _preSkip++; return; }
            for (int i = l.Count - 1; i >= 0; i--)
            {
                var it = l[i].Item;
                if (it == null || ForTroop(c, it, true)) continue;
                var sub = TemplateSub(c, it, l);
                if (sub != null) { l[i] = new EquipmentElement(sub); _dTpl++; }
                else { l.RemoveAt(i); _dDrop++; }
            }
        }

        private static ItemObject TemplateSub(CharacterObject c, ItemObject bad, List<EquipmentElement> have)
        {
            var sets = new List<Equipment>();
            Equipment first = null;
            try { first = c.FirstBattleEquipment; } catch { }
            if (first != null) sets.Add(first);
            try { foreach (var eq in c.BattleEquipments) if (eq != null && !ReferenceEquals(eq, first)) sets.Add(eq); } catch { }
            foreach (var eq in sets)
                for (int s = 0; s < 12; s++)
                {
                    var x = eq[(EquipmentIndex)s].Item;
                    if (x == null || x == bad || x.ItemType != bad.ItemType || !ForTroop(c, x, true)) continue;
                    bool dup = false;
                    foreach (var e in have) if (e.Item == x) { dup = true; break; }
                    if (!dup) return x;
                }
            return null;
        }

        // ------------------------------------------------------------ podloga broni w bitwie
        private sealed class Piece { internal EquipmentElement El; internal int Left; }

        private static bool IsMelee(ItemObject.ItemTypeEnum t)
        {
            return t == ItemObject.ItemTypeEnum.OneHandedWeapon || t == ItemObject.ItemTypeEnum.TwoHandedWeapon || t == ItemObject.ItemTypeEnum.Polearm;
        }

        /// <summary>Czy czlowiek ma czym walczyc: bron biala, rzucana, luk ze strzalami albo kusza z beltami (proca, pistolet, muszkiet - tez) w slotach 0-3.</summary>
        private static bool Armed(Equipment eq)
        {
            bool bow = false, xbow = false, arrows = false, bolts = false;
            for (int i = 0; i < 4; i++)
            {
                var e = eq[(EquipmentIndex)i];
                var it = e.Item;
                if (e.IsEmpty || it == null) continue;
                var t = it.ItemType;
                if (IsMelee(t) || t == ItemObject.ItemTypeEnum.Thrown || t == ItemObject.ItemTypeEnum.Sling || t == ItemObject.ItemTypeEnum.Pistol || t == ItemObject.ItemTypeEnum.Musket) return true;
                if (t == ItemObject.ItemTypeEnum.Bow) bow = true;
                else if (t == ItemObject.ItemTypeEnum.Crossbow) xbow = true;
                else if (t == ItemObject.ItemTypeEnum.Arrows) arrows = true;
                else if (t == ItemObject.ItemTypeEnum.Bolts) bolts = true;
            }
            return (bow && arrows) || (xbow && bolts);
        }

        private static void MissionFor(Mission m)
        {
            if (_mMission != null && ReferenceEquals(_mMission.Target, m)) return;
            _mMission = new WeakReference(m);
            ClearMission();
            lock (_lock) _floorKept.Clear();
            _dMis++;
        }

        /// <summary>Postfiks DTE PartyEquipmentDistributor.DoAssignAsync (Priority.Last - po SkillLawWard i RealmWard CrashScribe; takze podglad gotowosci
        /// zbrojowni na mapie i autorozstrzygniecie - ta sama regula, liczniki tylko w misji). Kazdy nie-bohater bez broni (Armed) dostaje w pierwszy pusty slot 0-3
        /// bron biala z puli rozdzielacza: (1) uzywalna (umiejetnosc i siodlo) dozwolonej kultury, (2) przy GearFitFloorForeign uzywalna obcej kultury - takze ta
        /// sama sztuka zza morza, ktora RealmWard chwile wczesniej zwrocil na polke (lepsza obca bron niz piesci; zasada Jeffa do decyzji), (3) przy
        /// GearFitFloorOverSkill najlzejsza ponad umiejetnosc (bez GearFitFloorForeign - tylko dozwolonej kultury). W kazdym przejsciu: najpierw klasa jego najwyzszej umiejetnosci, potem najwyzszy wymog. Bez unikatow, lore, sprzetu
        /// umarlych (QuartermasterLaw.BarredInBattle), legend i sprzetu olbrzymow nie dla niego. Pobranie przez ConsumeEquipmentToAssign (DTE nie da sztuki
        /// drugi raz, straz "nic z niczego" widzi pokrycie). Partie umarlych - bez zmian.</summary>
        public static void FloorPostfix(object __instance)
        {
            if (__instance == null || !_wFloor || !FloorOn) return;
            try
            {
                var mp = _fParty.GetValue(__instance) as MobileParty;
                if (mp != null && Undead.Party(mp)) return;   // sprzet umarlych poza gospodarka (jak GarrisonKit)
                var mission = _fMission.GetValue(__instance) as Mission;
                bool count = mission != null;
                if (count) MissionFor(mission);
                bool main = mp != null && mp.IsMainParty;
                var list = _fAssigns.GetValue(__instance) as IEnumerable;
                var pool = _fPool.GetValue(__instance) as IDictionary;
                if (list == null || pool == null) return;
                List<Piece> stock = null;
                // ZB-5 (klucz GearFitRealmFootInHideoutSiege): w kryjowce i oblezeniu jezdziec walczy pieszo - kon z przydzialu DTE nie zawaza o broni (ArmouryZb.FootMission)
                bool foot = mission != null && ArmouryZb.FootOn && ArmouryZb.FootMission(mission, mp);   // wypad/odsiecz: pieszo tylko strona Attacker
                foreach (var a in list)
                {
                    if (a == null) continue;
                    try { FloorOne(__instance, a, pool, ref stock, count, main, foot); }
                    catch (Exception e) { Stumble("FloorOne", e); }
                }
            }
            catch (Exception e) { Stumble("FloorPostfix", e); }
        }

        private static void FloorOne(object dist, object a, IDictionary pool, ref List<Piece> stock, bool count, bool main, bool foot)
        {
            var co = _getChar.Invoke(a, null) as CharacterObject;
            if (co == null || co.IsHero) return;
            var eq = _fEq.GetValue(a) as Equipment;
            if (eq == null || Armed(eq)) return;
            if (count) { _mBare++; _dBare++; if (main) _mBareP++; }
            int slot = -1;
            for (int i = 0; i < 4; i++) { var e = eq[(EquipmentIndex)i]; if (e.IsEmpty || e.Item == null) { slot = i; break; } }
            if (slot < 0) { if (count) _dNoSlot++; return; }   // sloty zajete czyms innym (tarcze, kolczany bez luku) - bez zmian
            // jezdziec = kon w przydziale (DTE dal mu konia z puli); bez konia idzie pieszo - wtedy kazda bron
            var hs = eq[EquipmentIndex.Horse];
            bool mounted = !hs.IsEmpty && hs.Item != null && !foot;   // ZB-5: w kryjowce i oblezeniu pieszo (klucz wylaczony - foot = false, jak dotad)
            if (stock == null) stock = Melee(pool);
            int pass;
            var pick = Best(co, stock, mounted, out pass);
            if (pick == null) { if (count) { _mNone++; _dNone++; if (main) _mNoneP++; } return; }
            _setEq.Invoke(a, new object[] { (EquipmentIndex)slot, pick.El });
            _consume.Invoke(dist, new object[] { pick.El });
            pick.Left--;
            if (pass == 3) Keep(eq, slot);
            if (!count) return;
            _mGot++;
            if (pass == 1) { _dB1++; if (main) _mB1P++; }
            else if (pass == 2) { _dB2++; if (main) _mB2P++; }
            else { _dB3++; if (main) _mB3P++; }
        }

        private static List<Piece> Melee(IDictionary pool)
        {
            var l = new List<Piece>();
            foreach (DictionaryEntry kv in pool)
            {
                int n; try { n = Convert.ToInt32(kv.Value); } catch { continue; }
                if (n <= 0 || !(kv.Key is EquipmentElement)) continue;
                var el = (EquipmentElement)kv.Key;
                var it = el.Item;
                if (it == null || !IsMelee(it.ItemType)) continue;
                if (QuartermasterLaw.BarredInBattle(it) || LegendaryLaw.IsLegend(it)) continue;
                l.Add(new Piece { El = el, Left = n });
            }
            return l;
        }

        private static Piece Best(CharacterObject co, List<Piece> stock, bool mounted, out int pass)
        {
            pass = 0;
            for (int p = 1; p <= 3; p++)
            {
                if (p == 2 && !FloorForeignOn) continue;   // obca kultura tylko za kluczem GearFitFloorForeign (zasada Jeffa, nie techniczna)
                if (p == 3 && !FloorOverSkillOn) break;
                Piece best = null; long bk = long.MinValue;
                foreach (var pc in stock)
                {
                    if (pc.Left <= 0) continue;
                    var it = pc.El.Item;
                    if (mounted && !MenUpgrade.MountOk(it)) continue;
                    if (GiantGear.Is(it) && !GiantGear.MayWear(co)) continue;
                    long key;
                    if (p < 3)
                    {
                        if (!ItemReq.Meets(co, it)) continue;
                        if (p == 1 && !CultureOk(co, it)) continue;
                        var sk = ItemReq.SkillFor(it);
                        int have = sk != null ? co.GetSkillValue(sk) : 0;
                        key = (long)have * 1000000L + (long)Math.Max(0, it.Difficulty) * 1000L + (long)Math.Max(0f, Math.Min(999f, it.Effectiveness));
                    }
                    else
                    {
                        if (!FloorForeignOn && !CultureOk(co, it)) continue;   // bez GearFitFloorForeign obca nie wraca takze z ostatniej deski
                        key = -(long)Math.Max(0, it.Difficulty);   // ostatnia deska: najlzejsza
                    }
                    if (best == null || key > bk) { best = pc; bk = key; }
                }
                if (best != null) { pass = p; return best; }
            }
            return null;
        }

        private static void Keep(Equipment eq, int slot)
        {
            lock (_lock) { int m; _floorKept.TryGetValue(eq, out m); _floorKept[eq] = m | (1 << slot); }
        }

        /// <summary>DragonUnmount przy spawnie: bron ponad umiejetnosc w tym slocie dala podloga swiadomie (ostatnia deska) - bez podmiany na wzorzec z niczego,
        /// ktory straz "nic z niczego" i tak by zdjela.</summary>
        internal static bool FloorKept(Equipment eq, int slot)
        {
            if (eq == null || slot < 0 || slot > 3) return false;
            bool kept;
            lock (_lock) { int m; kept = _floorKept.Count > 0 && _floorKept.TryGetValue(eq, out m) && (m & (1 << slot)) != 0; }
            if (kept) _dKept++;
            return kept;
        }

        /// <summary>Dopisek NA KONCU linii "Bitwa gracza (sklad7b-p, start)" (GarrisonKit.BattleNotice) - liczniki biezacej misji; komunikat gracza przy ostatniej desce.</summary>
        internal static string FloorNote()
        {
            try
            {
                if (_mB3P > 0) Log.Player(_mB3P + (_mB3P == 1 ? " of your men took a weapon" : " of your men took weapons") + " too hard for them from the stores - better than bare hands.", true);
                return " | podloga broni (GearFit" + (FloorOn ? "" : " - WYLACZONA") + "): Twoi ludzie - bez broni po strazach " + _mBareP + ", z polki " + (_mB1P + _mB2P + _mB3P)
                       + " (uzywalna " + _mB1P + ", obca kultura " + _mB2P + ", ponad umiejetnosc - ostatnia deska " + _mB3P + "), nic w polce " + _mNoneP
                       + "; wszystkie partie - bez broni po strazach " + _mBare + ", z polki " + _mGot + ", nic w polce " + _mNone + ".";
            }
            catch { return ""; }
        }

        // ------------------------------------------------------------ doba i spis
        internal static void Daily()
        {
            if (!_census) { _census = true; Census(); }
            Flush();
        }

        private static string Yes(bool b) { return b ? "tak" : "nie"; }

        /// <summary>Pierwsza doba sesji (po prawach CrashScribe, zamianie 175 i SkillSinew): pary krewne od nowa, spis wzorcow, stan wpiec i druzyny gracza.</summary>
        private static void Census()
        {
            int units = 0, slots = 0, over = 0, ownOnly = 0;
            string cs = "BRAK (stara DLL CrashScribe)";
            int men = 0, pcs = 0, unusable = 0;
            try
            {
                lock (_lock) { _kin = null; _tpl.Clear(); }
                bool started = false;
                try { started = Campaign.Current != null && Campaign.Current.GameStarted; } catch { }
                if (started) lock (_lock) BuildKin();
                foreach (var c in MBObjectManager.Instance.GetObjectTypeList<CharacterObject>())
                {
                    if (!Troop(c)) continue;
                    bool any = false;
                    try
                    {
                        foreach (var eq in c.BattleEquipments)
                        {
                            if (eq == null) continue;
                            for (int s = 0; s < 12; s++)
                            {
                                if (s == 4) continue;
                                var it = eq[(EquipmentIndex)s].Item;
                                if (it == null) continue;
                                any = true; slots++;
                                if (!ItemReq.Meets(c, it)) over++;
                                if (it.ItemType == ItemObject.ItemTypeEnum.Horse || it.ItemType == ItemObject.ItemTypeEnum.HorseHarness) continue;
                                string ic = it.Culture != null ? it.Culture.StringId : null, tc = c.Culture.StringId;
                                if (string.IsNullOrEmpty(ic) || ic == "neutral_culture" || ic == tc || IsEssos(ic) == IsEssos(tc)) continue;
                                if (!Kin(tc, ic)) ownOnly++;
                            }
                        }
                    }
                    catch (Exception e) { Stumble("Census(jednostka)", e); }
                    if (any) units++;
                }
            }
            catch (Exception e) { Stumble("Census", e); }
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name != "CrashScribe") continue;
                    var t = asm.GetType("CrashScribe.Mends");
                    cs = t != null && t.GetField("WardForeignSub", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) != null ? "tak" : "BRAK (stara DLL CrashScribe)";
                    break;
                }
            }
            catch { }
            try
            {
                var main = MobileParty.MainParty;
                if (main != null && main.MemberRoster != null) men = main.MemberRoster.TotalRegulars;
                var arm = QuartermasterLaw.DteArmory();
                if (arm != null)
                    for (int i = 0; i < arm.Count; i++)
                    {
                        var el = arm.GetElementCopyAtIndex(i);
                        if (el.EquipmentElement.Item == null || el.Amount <= 0) continue;
                        pcs += el.Amount;
                        int best; string sn;
                        if (!QuartermasterEscrow.AnyoneCanUse(el.EquipmentElement.Item, out best, out sn)) unusable += el.Amount;
                    }
            }
            catch (Exception e) { Stumble("Census(gracz)", e); }
            string kinItem = KinItemCensus();
            var ex = new List<string>();
            int pairs = 0;
            lock (_lock)
            {
                pairs = _kinTop.Count;
                for (int i = 0; i < _kinTop.Count && i < 5; i++) ex.Add(_kinTop[i].Key + " " + _kinTop[i].Value);
            }
            Log.Info("GearFit (start sesji): klucze - Gear Fit Kits " + Yes(On) + ", Gear Fit Culture " + Yes(CultureOn) + ", Gear Fit Weapon Floor " + Yes(FloorOn)
                     + ", Gear Fit Floor Foreign " + Yes(FloorForeignOn) + ", Gear Fit Floor Over Skill " + Yes(FloorOverSkillOn) + ", Gear Fit Realm From Stores " + Yes(RealmFromStoresOn)
                     + " | wpiete: " + _wiring + ", RealmWard z polki (CrashScribe) " + cs + ", start sesji " + Yes(_live)
                     + " (komplety DTE przed startem sesji bez filtra " + _preSkip + ")"
                     + " | wzorce wojsk po prawach CS: jednostek " + units + ", slotow " + slots + ", ponad umiejetnosc " + over + " (ma byc 0), dopuszczone tylko wyjatkiem wlasnego wzorca "
                     + ownOnly + " | kultury krewne (przez Waskie Morze, prog " + KinMinSlots + " sloty i " + (int)(KinMinShare * 100f) + "%): " + pairs + " par"
                     + (ex.Count > 0 ? ", np. " + string.Join(", ", ex.ToArray()) : "")
                     + " | " + kinItem
                     + " | druzyna gracza: ludzi " + men + ", sztuk zbrojowni " + pcs + ", nieuzywalnych dla kogokolwiek " + unusable + "; potkniecia " + _stumAll + ".");
        }

        /// <summary>f29-arakh: ile dopuszczen (kultura wojska x sztuka zza morza) daja pary krewne calej kultury przedmiotu, a ile sztuka po sztuce
        /// (GearFitKinItemLevel), i u ilu kultur arakh przechodzi przez pare krewna - przed i po. Raz na sesje (Census, po BuildKin).</summary>
        private static string KinItemCensus()
        {
            int pairAll = 0, itemAll = 0, arB = 0, arA = 0;
            var arWho = new List<string>();
            try
            {
                var snap = _kin;   // jedna migawka - pary i sztuki z tej samej budowy
                var k = snap != null ? snap.Pairs : null; var ki = snap != null ? snap.Items : null;
                if (k == null || ki == null) return "krewne sztuka po sztuce (Gear Fit Kin Item Level " + Yes(KinItemOn) + "): pary jeszcze nie zbudowane";
                ItemObject arakh = null;
                try { arakh = MBObjectManager.Instance.GetObject<ItemObject>("arakh"); } catch { }
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || it.Culture == null) continue;
                    var t = it.ItemType;
                    if (t == ItemObject.ItemTypeEnum.Horse || t == ItemObject.ItemTypeEnum.HorseHarness || t == ItemObject.ItemTypeEnum.Banner) continue;
                    string ic = it.Culture.StringId;
                    if (string.IsNullOrEmpty(ic) || ic == "neutral_culture") continue;
                    foreach (var kv in k)
                    {
                        if (!kv.Value.Contains(ic)) continue;
                        pairAll++;
                        HashSet<ItemObject> hs;
                        bool has = ki.TryGetValue(kv.Key, out hs) && hs.Contains(it);
                        if (has) itemAll++;
                        if (it == arakh) { arB++; if (has) arA++; arWho.Add(kv.Key); }
                    }
                }
                arWho.Sort(StringComparer.Ordinal);
            }
            catch (Exception e) { Stumble("KinItemCensus", e); }
            return "krewne sztuka po sztuce (Gear Fit Kin Item Level " + Yes(KinItemOn) + "): dopuszczen kultura x sztuka przez pare krewna " + pairAll
                   + ", z tego noszonych przez wzorce tej kultury " + itemAll + " (przy kluczu tak odpada " + (pairAll - itemAll) + ")"
                   + "; arakh przez pare krewna: kultur " + arB + (arWho.Count > 0 ? " [" + string.Join(", ", arWho.ToArray()) + "]" : "")
                   + " -> " + (KinItemOn ? arA : arB) + " (ma byc 0 przy kluczu tak)";
        }

        private static void Flush()
        {
            if (!On && !CultureOn && !FloorOn) { ClearDay(); return; }
            int day = Today();
            // poprawka po recenzji: x liczone w chwili odrzucenia (FitHandover), nie jako suma x1 + x2 + x3 - ksiega x = x1 + x2 + x3 jest prawdziwym sprawdzeniem
            int x = _dX;
            bool book = _dN == _dU + x + _dKeep;
            // pamiec zakupow brakow starsza niz GapIdleDays - bez znaczenia dla kontroli
            if (_gapBuy.Count > 0)
            {
                var old = new List<string>();
                foreach (var kv in _gapBuy) if (day - kv.Value > GapIdleDays) old.Add(kv.Key);
                foreach (var k in old) _gapBuy.Remove(k);
            }
            Log.Info("GearFit: dzien " + day + " | zakupy notabli: kandydatow odrzuconych " + _dRej + " (umiejetnosc " + _dRejSkill + ", siodlo " + _dRejSaddle + ", kultura " + _dRejCulture
                     + "), kultura wlasna zamiast tanszej " + _dNative + ", awanse cofniete przez brak uzywalnej sztuki " + _dRevNoFit + ", dodatki bez uzywalnej sztuki " + _dExtraNoFit
                     + " | przekazanie kompletow (AI " + _dMenAi + ", gracz " + _dMenPl + ", zalogi " + _dMenGar + " ludzi): szt. " + _dN + ", uzywalne " + _dU + ", nieuzywalne " + x
                     + " (kupione -> notabl " + _dX1 + " za " + _dG1 + " zl, dobytek -> sakiewka ludzi " + _dX2 + " za " + _dG2 + " zl, na polke bez zaplaty " + _dX3 + "), bez targu zostawione jak dotad " + _dKeep
                     + ", zamiennik z domu " + _dHome + " (w tym x2 zalog bez sakiewki - panu osady " + _dLordN + " za " + _dLordG + " zl)" + " | PlanKits pominiete " + _dPlan + " | dobytek tieru 1 (DTE GetRecruitEquipments, zalogi t1): podmienione na wzorzec " + _dTpl + ", usuniete " + _dDrop
                     + " | bitwy (podloga broni, misji " + _dMis + "): bez broni po strazach " + _dBare + ", z polki " + (_dB1 + _dB2 + _dB3) + " (uzywalna " + _dB1 + ", obca kultura " + _dB2
                     + ", ponad umiejetnosc " + _dB3 + "), nic w polce " + _dNone + ", bez wolnego slotu " + _dNoSlot + ", DragonUnmount pozostawione " + _dKept
                     + " | nadwyzki wedlug MeetsFitOf - sprzedane sztuki kultury, ktorej nie nosi nikt w partii: lordowie AI " + _dAiForeign + ", zalogi " + _dGarForeign
                     + " | braki ludzi gracza: kupione " + _dGapBuy + ", kupione w " + GapIdleDays + " dniach i nieuzyte (wolne przy brakach albo sprzedane jako nadwyzka) " + _dGapIdle
                     + (_gapIdleIds.Count > 0 ? " (" + string.Join(", ", _gapIdleIds.ToArray()) + ")" : "")
                     + " | kontrola: do zbrojowni nieuzywalnych " + _dCtl + " (ma byc 0) - na wejsciu, list " + _dEntry + ", sztuk " + _dEntryPcs + ", dozwolone bez targu " + _dEntryKeep
                     + ", x = x1 + x2 + x3 " + x + " = " + _dX1 + " + " + _dX2 + " + " + _dX3
                     + ", szt. = uzywalne + nieuzywalne + bez targu " + (book ? "zgodne" : "NIEZGODNE") + "; potkniecia " + _stum + " (sesja " + _stumAll + ").");
            ClearDay();
        }
    }
}
