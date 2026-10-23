using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m97-szkolenie (Jeff 20.10 18:27): "szkolenie wojska nie odbywa sie w marszu, w marszu jest marsz; na ekranie obozu mam opcje szkolenie wojska
    /// i szybkosc szkolenia zalezy od moich umiejetnosci leadership [...]; szkolenie z ekranu obozu daj, ze jest o 50% wydajniejsze niz bylo przedtem;
    /// zobacz, czy mozna przerobic perki leadership, aby pasowaly".
    /// ZASADA (klucz DrillCampOnly, domyslnie TAK; wzor musztry Drill.cs bez zmian poza D, L i P):
    ///  - marsz nie uczy: D liczy tylko godziny cwiczen, godzina ruchu = 0;
    ///  - gracz cwiczy TYLKO z menu obozu (arm_camp_wait -> "Train the men", menu oczekiwania arm_camp_drill_wait), tylko za dnia (poza noca snu
    ///    NightRest.MenuNight - przy obozie 21-3 cwiczenia 3:00-17:59), najwyzej FullHours (8) godzin na dobe od switu do switu; godziny czekaja na
    ///    najblizszy tick treningu druzyny gracza (tam liczy je Drill.Shape) - wzor jest liniowy w godzinach, wiec chwila ticku nie zmienia sumy;
    ///  - strona gracza (rod gracza, lordowie w armii gracza - Z14a) i AI przy DrillCampOnlyAi: godziny postoju za dnia z masek Drill (RestHour:
    ///    osada, oboz oblezenia, postoj), najwyzej 8 w oknie 24 h ticku;
    ///  - tempo: pelny dzien (8 h) D = 1.5 (dawny postoj) x 1.5 (Jeff: "o 50% wydajniejsze") = 2.25, godzina = 1/8 dnia;
    ///  - L: Przywodztwo dowodcy + polowa Przywodztwa mistrza musztry (najlepszy inny bohater druzyny; klucz DrillMaster) / 170, w 0.5-1.5;
    ///  - P: perki treningu gry i BK (BKPartyTrainningModel.GetEffectiveDailyExperience) liczone wlasna tabela: warunek oddzialu (tier, piechota,
    ///    strzelcy, piesi) i stanu partii (morale, przeciazenie) jak w grze, warunek marszu / postoju w osadzie zastapiony cwiczeniem; wartosc gry
    ///    x 1.5 za pelny dzien cwiczen, czesc dnia - czesc. Opisy perkow podmienione (PrimaryDescription / SecondaryDescription, id bez zmian).
    /// Glod albo dlug snu o swicie - jak dotad: dzien bez cwiczen. Gracz: kare pilnuje menu w chwili cwiczen (WhyNot, DrillTick: glod, dlug o swicie);
    /// godziny juz zrobione NIE przepadaja przez kare z chwili ticku (recenzja 1: tick bywa po nastepnym swicie). Strona gracza i AI - kara z ticku jak dotad.
    /// Recenzja 2 (klucz DrillSiegeArmy): gracz w obozie oblezenia albo w armii cudzego wodza (menu obozu niedostepne) - ludzie cwicza sami w godzinach
    /// postoju za dnia, jak lordowie obok (AutoHour z Drill.Observe), w tym samym limicie 8 h na dobe. Godzina cwiczen z menu nie jest odpoczynkiem
    /// w ksiedze snu (NightRest, DrilledLastHour) - jak stamina (recenzja 4).
    /// </summary>
    internal static class CampDrill
    {
        internal const string P = "Szkolenie w obozie (m97): ";
        internal const float FullHours = 8f;               // pelny dzien cwiczen
        internal const float Boost = 1.5f;                 // Jeff: "o 50% wydajniejsze niz bylo przedtem"
        internal const float DayFull = 1.5f * Boost;       // dawny dzien postoju Drill.RestDay 1.5 -> 2.25 za pelny dzien
        private const float MasterWeight = 0.5f;
        private const string Menu = "arm_camp_drill_wait";

        // ------------------------------------------------------------ stan gracza (zapis arm_campdrill)
        private static float _dayHours;                    // godziny cwiczen w biezacej dobie (od switu), najwyzej FullHours
        private static long _dayIdx = -1;                  // doba switu, do ktorej naleza _dayHours
        private static float _owed;                        // godziny cwiczen czekajace na tick treningu druzyny gracza
        private static bool _leave;
        private static float _session; private static double _sessionStart;
        private static double _lastDrillAt = -1e9;         // chwila ostatniej godziny cwiczen z menu (ksiega snu - NightRest), bez zapisu
        private static double _auto;                       // godziny samoczynne (oboz oblezenia, cudza armia) od ostatniej linii gracza
        // m109-musztra (TownStay): postoj gracza w osadzie - licznik postoju (TownStay.Stay: poczatek, poranki, godziny w polu), godziny w osadzie od switu (czesc
        // _dayHours), najwyzszy limit osady w tej dobie od switu (kontrola), godziny w osadzie czekajace na tick (czesc _owed), od ostatniej linii gracza
        private static readonly TownStay.Stay _stay = new TownStay.Stay();
        private static float _daySet, _owedSet, _dayCapMax;
        private static double _setLine;
        private static int _stumbles, _sessions;
        private static readonly HashSet<string> _err = new HashSet<string>();

        // ------------------------------------------------------------ pomiar doby (linia AI i strony gracza)
        private static int _aParties, _aH0, _aH8, _aMarchXpParties, _sParties, _sMarchXpParties;
        private static double _aH, _aOld, _aCamp, _aMen, _aNow, _sOld, _sCamp, _sNow, _sH;
        private static double _pSession;                   // godziny z menu obozu od ostatniej linii gracza (kontrola "tylko z obozu")

        internal static void Reset()
        {
            _dayHours = 0f; _dayIdx = -1; _owed = 0f; _leave = false; _session = 0f; _stumbles = 0; _sessions = 0; _pSession = 0; _auto = 0; _lastDrillAt = -1e9;
            _stay.Clear(); _daySet = _owedSet = _dayCapMax = 0f; _setLine = 0;   // m109-musztra
            _err.Clear(); ClearDay();
            TownStay.Reset();
        }

        private static void ClearDay()
        {
            _aParties = _aH0 = _aH8 = _aMarchXpParties = _sParties = _sMarchXpParties = 0;
            _aH = _aOld = _aCamp = _aMen = _aNow = _sOld = _sCamp = _sNow = _sH = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_err.Add(where)) { try { Log.Error("CampDrill." + where, e); } catch { } }
        }

        private static string F1(double v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }
        private static string F2(double v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }

        // ------------------------------------------------------------ zasada
        /// <summary>Gracz i strona gracza (Z14a) przy DrillCampOnly (i DrillLaw), AI przy DrillCampOnly + DrillCampOnlyAi (i DrillLawAi - bez wzoru AI cwiczy
        /// plasko jak w grze).</summary>
        internal static bool RuleFor(bool main, bool noBase, Settings s)
        {
            if (s == null || !s.DrillCampOnly) return false;
            if (main || noBase) return s.DrillLaw;
            return s.DrillCampOnlyAi && s.DrillLawAi;
        }

        private static bool PlayerOn(Settings s) { return s != null && s.DrillCampOnly && s.DrillLaw; }

        /// <summary>Godzina cwiczen: poza noca snu gracza (ta sama noc co sen w menu i karczma - NightRest.MenuNight).</summary>
        internal static bool DayHour(int h) { return !NightRest.MenuNight(h); }

        private static long DawnIdx()
        {
            return (long)Math.Floor((CampaignTime.Now.ToHours - NightRest.PlayerDawn) / 24.0);
        }

        private static void Roll()
        {
            long d = DawnIdx();
            if (d != _dayIdx) { _dayIdx = d; _dayHours = 0f; _daySet = 0f; _dayCapMax = 0f; }
        }

        /// <summary>Godziny cwiczen gracza czekajace na tick treningu (Drill.CtxOf druzyny gracza).</summary>
        internal static float PlayerOwed { get { return _owed; } }

        /// <summary>m109-musztra: czesc PlayerOwed zrobiona na postoju w osadzie (napis dnia w grze - Drill.DayMessage).</summary>
        internal static float PlayerOwedTown { get { return _owedSet; } }

        /// <summary>m109-musztra: stan postoju gracza (TownStay - linie, komunikat, autotest; tylko odczyt): licznik postoju i godziny w osadzie od switu.</summary>
        internal static void PlayerStay(out TownStay.Stay stay, out float daySet) { Roll(); stay = _stay; daySet = _daySet; }

        /// <summary>m109-musztra: najwyzszy limit osady w biezacej dobie od switu (0 - bez godziny w osadzie) - kontrola "godziny w osadzie <= limit" (MUSZT-1).</summary>
        internal static float PlayerDayCap { get { Roll(); return _dayCapMax; } }

        /// <summary>m109-musztra: godziny cwiczen gracza od switu (wspolny limit 8 h - autotest).</summary>
        internal static float PlayerDayHours { get { Roll(); return _dayHours; } }

        /// <summary>m109-musztra: zasada gracza czynna (Drill Camp Only i Drill Law) - postoj dotyczy gracza tylko wtedy.</summary>
        internal static bool PlayerRuleOn { get { return PlayerOn(Settings.Current); } }

        /// <summary>Recenzja 4: w ostatniej godzinie byla godzina cwiczen z menu - ksiega snu gracza (NightRest) nie liczy jej jako odpoczynku za dnia
        /// (cwiczenia to praca, jak w staminie: menu cwiczen nie jest w InCampMenu).</summary>
        internal static bool DrilledLastHour { get { return CampaignTime.Now.ToHours - _lastDrillAt < 0.999; } }

        /// <summary>Recenzja 2 (klucz DrillSiegeArmy): gracz w obozie oblezenia albo w armii cudzego wodza - menu obozu niedostepne (klawisz O tylko na czystej
        /// mapie), o postoju decyduje oblezenie albo wodz. Ludzie cwicza sami w godzinie postoju za dnia, jak lordowie obok (strona gracza: Drill.DayRestHours) -
        /// ta sama doba od switu i ten sam limit 8 h co menu, ta sama kara (glod, dlug o swicie) w chwili godziny. Wolane z Drill.Observe raz na godzine
        /// dla druzyny gracza (rest - godzina postoju wedlug Drill.RestHour, now - pelna godzina konca obserwowanej godziny). Sluzba ROT - bez zmian (partia
        /// nieaktywna: Observe jej nie widzi, Drill.Shape daje 0 jak przed m97).
        /// m109-musztra (klucz DrillTownStay, TownStay): postoj w osadzie (miasto, zamek, wies; takze doczepiony do armii w osadzie - gra przenosi CurrentSettlement
        /// na partie doczepione) - ludzie cwicza sami w godzinach cwiczen, z limitem godzin w osadzie na dobe postoju (TownStay.Cap) w tym samym limicie 8 h od switu;
        /// licznik postoju co godzine (takze noca i w karze; missed - godziny przegapione przez Observe, poza osada). Oblezona twierdza - jak oboz oblezenia.
        /// Klucz wylaczony - kind 0, zasada jak dotad.</summary>
        internal static void AutoHour(MobileParty mp, bool rest, long now, int missed)
        {
            try
            {
                var s = Settings.Current;
                if (mp == null || !PlayerOn(s)) return;
                var st = mp.CurrentSettlement;
                int kind = s.DrillTownStay ? TownStay.Kind(st) : TownStay.KNone;
                TownStay.Step(_stay, kind != TownStay.KNone, now, missed);   // m109: licznik postoju (klucz wylaczony - zawsze "poza", licznik pusty)
                if (!rest) return;
                if (kind == TownStay.KNone)
                {
                    if (!s.DrillSiegeArmy) return;
                    if (mp.BesiegerCamp == null && mp.AttachedTo == null && !(s.DrillTownStay && TownStay.Besieged(st))) return;
                }
                if (mp.MapEvent != null || mp.IsCurrentlyAtSea || NightRest.EnlistedNow) return;
                if (!DayHour(Mod24((int)(now % 24) - 1))) return;
                if (mp.MemberRoster == null || mp.MemberRoster.TotalRegulars <= 0) return;
                if (mp.Party != null && mp.Party.IsStarving) return;
                if (NightRest.DawnDebtOf(mp) >= 1) return;
                Roll();
                float room = FullHours - _dayHours;
                int cap = 0;
                if (kind != TownStay.KNone)
                {
                    cap = TownStay.Cap(kind, TownStay.DayNow(_stay));   // doba tego poranka (Step policzyl go wyzej)
                    if (cap > _dayCapMax) _dayCapMax = cap;
                    room = Math.Min(room, cap - _daySet);
                }
                float take = Math.Max(0f, Math.Min(1f, room));
                if (take <= 0f) return;
                _dayHours += take; _owed += take;
                if (kind != TownStay.KNone) { _daySet += take; _owedSet += take; _setLine += take; }
                else _auto += take;
            }
            catch (Exception e) { Stumble("AutoHour", e); }
        }

        /// <summary>Po ticku treningu druzyny gracza (Drill.TickPostfix): godziny zuzyte i linia doby gracza. Recenzja 1: kara z chwili ticku (glod, dlug o swicie)
        /// nie kasuje godzin juz zrobionych - pilnuje jej menu w chwili cwiczen (Drill.CtxOf: gracz pod zasada obozu bez c.Off).</summary>
        internal static void AfterPlayerTick(Drill.Ctx c, long accepted, double men, double perks, double armed)
        {
            try
            {
                var s = Settings.Current;
                if (c == null || !c.Camp) return;
                float h = _owed;
                _owed = 0f; _owedSet = 0f;   // m109-musztra: czesc w osadzie zuzyta tym samym tickiem
                if (s == null || !s.DrillLog) { _pSession = 0; _auto = 0; _setLine = 0; return; }
                Roll();
                bool stay = s.DrillTownStay;   // m109-musztra: dopiski postoju tylko przy kluczu (NIE - linia jak dotad)
                Log.Info(P + "gracz dzien " + (int)CampaignTime.Now.ToDays + " | godzin cwiczen " + F2(h) + " (z menu obozu od ostatniej linii " + F2(_pSession)
                         + ", samoczynnie w obozie oblezenia albo w cudzej armii " + F2(_auto) + (stay ? ", w osadzie (m109) " + F2(_setLine) : "") + "; dzis od switu "
                         + F2(_dayHours) + " z " + (int)FullHours + ") -> dzien x" + F2(c.D) + " (pelny x" + F2(DayFull) + "), godzin ruchu w dobie " + c.Moved
                         + " (dawna regula x" + F2(c.Dold) + ") | XP przyjete " + Math.Max(0L, accepted) + " (na glowe " + F1(men > 0 ? Math.Max(0L, accepted) / men : 0) + ", ludzi " + (int)men
                         + "), perki +" + F1(men > 0 ? perks / men : 0) + " na glowe | Przywodztwo x" + F2(c.L) + " (dowodca " + c.Lead + ", mistrz musztry "
                         + (c.MasterName != null ? c.MasterName + " " + c.Master : "brak") + "; bez mistrza x" + F2(c.Lold) + ") | zapas x" + F2(c.StockOn ? c.S : 1f)
                         + " | uzbrojeni " + (int)Math.Round(100 * armed) + "% | kara w chwili ticku: " + (c.Hungry ? "glod" : c.Sleepless ? "dlug snu o swicie" : "brak")
                         + ((c.Hungry || c.Sleepless) && h > 0f ? " - godziny zrobione przed kara policzone" : "") + (c.Off ? " - KARA ZASTOSOWANA" : "")
                         + (stay ? " | " + StayNote() : "")
                         + " | wejsc w Train the men " + _sessions + ", potkniecia " + _stumbles + ".");
                _pSession = 0; _sessions = 0; _auto = 0; _setLine = 0;
            }
            catch (Exception e) { Stumble("AfterPlayerTick", e); }
        }

        /// <summary>m109-musztra: dopisek linii gracza - postoj teraz (rodzaj, osada, doba, limit) albo wypad poza osade, godziny w osadzie od switu.</summary>
        private static string StayNote()
        {
            var mp = MobileParty.MainParty;
            var st = mp != null ? mp.CurrentSettlement : null;
            int kind = TownStay.Kind(st);
            if (_stay.From < 0) return "postoj (m109): brak (w polu albo osada bez postoju), dzis w osadzie " + F2(_daySet) + " h";
            int d = TownStay.DayNow(_stay);
            if (kind != TownStay.KNone)
                return "postoj (m109): " + TownStay.KindName(kind) + " " + st.Name + " doba " + d + ", limit " + TownStay.Cap(kind, d) + " h, dzis w osadzie " + F2(_daySet) + " h";
            return "postoj (m109): poza osada, w polu " + _stay.Field + " h (doba postoju " + d + " trwa do " + TownStay.FieldResetHours + " h w polu), dzis w osadzie "
                   + F2(_daySet) + " h";
        }

        // ------------------------------------------------------------ mistrz musztry
        /// <summary>L zasady obozu: (Przywodztwo dowodcy + polowa Przywodztwa najlepszego innego bohatera druzyny) / 170 w 0.5-1.5. Bez dowodcy 0.5 jak dotad.</summary>
        internal static float Lead(MobileParty mp, int lead, float lOld, Settings s, out int master, out string name)
        {
            master = -1; name = null;
            if (lead < 0) return lOld;
            if (s != null && s.DrillMaster)
            {
                try
                {
                    var lh = mp.LeaderHero;
                    var r = mp.MemberRoster;
                    for (int i = 0; r != null && i < r.Count; i++)
                    {
                        var ch = r.GetCharacterAtIndex(i);
                        if (ch == null || !ch.IsHero) continue;
                        var h = ch.HeroObject;
                        if (h == null || h == lh || !h.IsAlive || h.IsPrisoner) continue;
                        int v = h.GetSkillValue(DefaultSkills.Leadership);
                        if (v > master) { master = v; name = h.Name != null ? h.Name.ToString() : h.StringId; }
                    }
                }
                catch (Exception e) { Stumble("Lead", e); master = -1; name = null; }
            }
            float sum = lead + (master > 0 ? MasterWeight * master : 0f);
            float l = sum / 170f;
            return l < 0.5f ? 0.5f : (l > 1.5f ? 1.5f : l);
        }

        // ------------------------------------------------------------ perki
        private enum Troop { All, LowTier, Infantry, Foot, Ranged, HighTier }
        private enum Cond { None, Morale75, Overburdened }
        private sealed class PerkRule
        {
            internal string Name; internal Func<PerkObject> Get; internal bool Secondary; internal Troop Who; internal Cond When; internal string Text;
            internal PerkObject Perk;
        }

        private static readonly PerkRule[] Rules =
        {
            new PerkRule { Name = "Leadership Combat Tips", Get = () => DefaultPerks.Leadership.CombatTips, Secondary = false, Who = Troop.All, Text = "every man" },
            new PerkRule { Name = "Leadership Raise The Meek", Get = () => DefaultPerks.Leadership.RaiseTheMeek, Secondary = false, Who = Troop.LowTier, Text = "tier 1 and 2 men" },
            new PerkRule { Name = "Polearm Drills", Get = () => DefaultPerks.Polearm.Drills, Secondary = true, Who = Troop.All, Text = "every man" },
            new PerkRule { Name = "One Handed Military Tradition", Get = () => DefaultPerks.OneHanded.MilitaryTradition, Secondary = false, Who = Troop.Infantry, Text = "infantry" },
            new PerkRule { Name = "Athletics Walk It Off", Get = () => DefaultPerks.Athletics.WalkItOff, Secondary = true, Who = Troop.Foot, Text = "foot troops" },
            new PerkRule { Name = "Throwing Saddlebags", Get = () => DefaultPerks.Throwing.Saddlebags, Secondary = true, Who = Troop.Infantry, Text = "infantry" },
            new PerkRule { Name = "Athletics A Good Days Rest", Get = () => DefaultPerks.Athletics.AGoodDaysRest, Secondary = true, Who = Troop.Foot, Text = "foot troops" },
            new PerkRule { Name = "Bow Trainer", Get = () => DefaultPerks.Bow.Trainer, Secondary = true, Who = Troop.Ranged, Text = "archers and crossbowmen" },
            new PerkRule { Name = "Crossbow Renowned Marksmen", Get = () => DefaultPerks.Crossbow.RenownMarksmen, Secondary = false, Who = Troop.Ranged, Text = "ranged troops" },
            new PerkRule { Name = "Scouting Forced March", Get = () => DefaultPerks.Scouting.ForcedMarch, Secondary = true, Who = Troop.All, When = Cond.Morale75, Text = "every man" },
            new PerkRule { Name = "Scouting Unburdened", Get = () => DefaultPerks.Scouting.Unburdened, Secondary = true, Who = Troop.All, When = Cond.Overburdened, Text = "every man" },
            new PerkRule { Name = "Steward Seven Veterans", Get = () => DefaultPerks.Steward.SevenVeterans, Secondary = false, Who = Troop.HighTier, Text = "tier 4 and higher men" },
            new PerkRule { Name = "Steward Drill Sergeant", Get = () => DefaultPerks.Steward.DrillSergant, Secondary = false, Who = Troop.All, Text = "every man" },
        };
        private static PerkObject _wicked;                 // Roguery No Rest for the Wicked: +20% (AddFactor) dla oddzialow kultury bandytow - jak w grze

        private static void Bind()
        {
            for (int i = 0; i < Rules.Length; i++) { try { Rules[i].Perk = Rules[i].Get(); } catch (Exception e) { Rules[i].Perk = null; Stumble("Bind." + Rules[i].Name, e); } }
            try { _wicked = DefaultPerks.Roguery.NoRestForTheWicked; } catch (Exception e) { _wicked = null; Stumble("Bind.Wicked", e); }
        }

        private static float Value(PerkRule r)
        {
            var p = r.Perk;
            if (p == null) return 0f;
            return (float)Math.Round(r.Secondary ? p.SecondaryBonus : p.PrimaryBonus);   // jak BKPartyTrainningModel.GetPerkExperiencesForTroops (MathF.Round)
        }

        /// <summary>Wartosci perkow partii (raz na partie w Drill.CtxOf): XP gry na dobe, 0 gdy partia perka nie ma albo nie spelnia warunku stanu. Ostatni
        /// element - czynnik bandytow (0 albo premia No Rest for the Wicked).</summary>
        internal static float[] PartyPerks(MobileParty mp)
        {
            var v = new float[Rules.Length + 1];
            try
            {
                if (Rules[0].Perk == null) Bind();
                bool morale = mp.Morale > 75f;
                bool over = mp.TotalWeightCarried > (float)mp.InventoryCapacity;
                for (int i = 0; i < Rules.Length; i++)
                {
                    var r = Rules[i];
                    if (r.Perk == null) continue;
                    if (r.When == Cond.Morale75 && !morale) continue;
                    if (r.When == Cond.Overburdened && !over) continue;
                    if (mp.HasPerk(r.Perk, r.Secondary)) v[i] = Value(r);
                }
                if (_wicked != null && mp.HasPerk(_wicked, false)) v[Rules.Length] = _wicked.PrimaryBonus;
            }
            catch (Exception e) { Stumble("PartyPerks", e); }
            return v;
        }

        /// <summary>Perki oddzialu (XP gry na dobe) z wartosci partii i warunku oddzialu; fac - czynnik bandytow (1 bez perka).</summary>
        internal static float TroopPerks(float[] v, CharacterObject ch, out float fac)
        {
            fac = 1f;
            if (v == null || ch == null) return 0f;
            float sum = 0f;
            int tier = ch.Tier;
            for (int i = 0; i < Rules.Length; i++)
            {
                if (v[i] == 0f) continue;
                bool ok;
                switch (Rules[i].Who)
                {
                    case Troop.LowTier: ok = tier < 3; break;
                    case Troop.HighTier: ok = tier >= 4; break;
                    case Troop.Infantry: ok = ch.IsInfantry; break;
                    case Troop.Foot: ok = !ch.IsMounted; break;
                    case Troop.Ranged: ok = ch.IsRanged; break;
                    default: ok = true; break;
                }
                if (ok) sum += v[i];
            }
            if (v[Rules.Length] > 0f && ch.Culture != null && ch.Culture.IsBandit) fac = 1f + v[Rules.Length];
            return sum;
        }

        private static string NewText(PerkRule r)
        {
            string v = (Value(r) * Boost).ToString("0.#", CultureInfo.InvariantCulture);
            string cond = r.When == Cond.Morale75 ? " while the party's morale is above 75" : r.When == Cond.Overburdened ? " while the party carries more than it can" : "";
            return "+" + v + " experience to " + r.Text + " for a full day of drill in camp" + cond
                   + " (Train the men, 8 hours by day; a shorter drill gives its share). Marching teaches nothing.";
        }

        /// <summary>Opisy perkow: czesc z doswiadczeniem (Primary albo Secondary) i zmienna STR1/STR2 opisu calosci; pierwotny opis do logu.</summary>
        private static string PerkTexts(out int n)
        {
            n = 0;
            var names = new StringBuilder();
            var pPrim = HarmonyLib.AccessTools.Property(typeof(PerkObject), "PrimaryDescription");
            var pSec = HarmonyLib.AccessTools.Property(typeof(PerkObject), "SecondaryDescription");
            Bind();
            for (int i = 0; i < Rules.Length; i++)
            {
                var r = Rules[i];
                try
                {
                    if (r.Perk == null) { names.Append(names.Length > 0 ? ", " : "").Append(r.Name).Append(" BRAK"); continue; }
                    // wartosc gry po zaokragleniu 0 (Polearm Drills: drugi bonus 0.1 -> gra i BK daja 0 XP) - opis bez zmian, perk bez XP jak w grze
                    if (Value(r) <= 0f)
                    {
                        names.Append(names.Length > 0 ? ", " : "").Append(r.Name).Append(" 0 (gra daje 0 - opis bez zmian)");
                        Log.Info(P + "perk " + r.Name + " - wartosc gry po zaokragleniu 0 (bonus " + F2(r.Secondary ? r.Perk.SecondaryBonus : r.Perk.PrimaryBonus)
                                 + ") - gra i BK nie daja z niego XP; opis bez zmian.");
                        continue;
                    }
                    var prop = r.Secondary ? pSec : pPrim;
                    if (prop == null) continue;
                    var old = prop.GetValue(r.Perk, null) as TextObject;
                    var t = new TextObject("{=!}" + NewText(r));
                    prop.SetValue(r.Perk, t, null);
                    if (r.Perk.Description != null) r.Perk.Description.SetTextVariable(r.Secondary ? "STR2" : "STR1", t);
                    n++;
                    names.Append(names.Length > 0 ? ", " : "").Append(r.Name).Append(" ").Append(F1(Value(r))).Append(" -> ").Append(F1(Value(r) * Boost)).Append(" za pelny dzien");
                    Log.Info(P + "perk " + r.Name + " (" + (r.Secondary ? "druga" : "pierwsza") + " czesc opisu) - opis gry: '" + (old != null ? old.ToString() : "-") + "' -> nowy: '" + t.ToString() + "'.");
                }
                catch (Exception e) { Stumble("PerkTexts." + r.Name, e); }
            }
            return names.ToString();
        }

        // ------------------------------------------------------------ start sesji
        internal static void SessionStart()
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                int n = 0;
                string perks = "nie (klucz Drill Camp Only albo Drill Law wylaczony - perki jak w grze)";
                if (PlayerOn(s)) perks = PerkTexts(out n);
                else Bind();
                Roll();
                Log.Info(P + "start sesji | zasada: marsz nie uczy, gracz tylko z menu obozu (Train the men) " + (PlayerOn(s) ? "TAK" : "NIE") + " (Drill Camp Only "
                         + (s.DrillCampOnly ? "TAK" : "nie") + ", Drill Law " + (s.DrillLaw ? "TAK" : "nie") + "), strona gracza (rod, armia gracza) godziny postoju za dnia "
                         + (PlayerOn(s) ? "TAK" : "NIE") + ", AI " + (RuleFor(false, false, s) ? "TAK" : "NIE") + " (Drill Camp Only Ai " + (s.DrillCampOnlyAi ? "TAK" : "nie")
                         + ", Drill Law Ai " + (s.DrillLawAi ? "TAK" : "nie") + ") | tempo: pelny dzien " + (int)FullHours + " h = x" + F2(DayFull) + " (dawny postoj x1.50 x " + F2(Boost)
                         + "), godzina x" + F2(DayFull / FullHours) + ", dawny marsz x0.90 -> 0 | godziny cwiczen " + NightRest.PlayerDawn + ":00-" + Mod24(NightRest.SleepNightStart - 1)
                         + ":59 (noc snu " + NightRest.SleepNightStart + ":00-" + Mod24(NightRest.PlayerDawn - 1) + ":59 - ludzie spia) | mistrz musztry " + (s.DrillMaster ? "TAK" : "nie")
                         + " (polowa Przywodztwa najlepszego innego bohatera druzyny) | gracz w obozie oblezenia albo w cudzej armii - cwiczy sam w godzinach postoju za dnia "
                         + (s.DrillSiegeArmy ? "TAK" : "nie") + " (Drill Siege Army) | kara gracza w chwili cwiczen (godziny zrobione nie przepadaja) | godzina cwiczen z menu"
                         + " to nie odpoczynek w ksiedze snu | perki przerobione " + n + "/" + Rules.Length + ": " + perks
                         + " | stan gracza: dzis " + F2(_dayHours) + " h, do policzenia " + F2(_owed) + " h.");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
            TownStay.SessionStart();   // m109-musztra: linia startu postoju w osadzie (wlasny try)
        }

        private static int Mod24(int h) { return ((h % 24) + 24) % 24; }

        // ------------------------------------------------------------ menu
        internal static void RegisterEvents(CampaignBehaviorBase b)
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(b, delegate (CampaignGameStarter st) { try { _leave = false; if (st != null) AddMenus(st); } catch (Exception e) { Stumble("RegisterEvents", e); } });
            TownStay.RegisterEvents(b);   // m109-musztra: takt autotestu postoju (tylko przy pliku armoury-postoj.flag w autotescie; w grze od razu wyjscie)
        }

        private static void AddMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption("arm_camp_wait", "arm_camp_drill", "{=!}Train the men",
                delegate (MenuCallbackArgs a)
                {
                    if (!PlayerOn(Settings.Current)) return false;
                    a.optionLeaveType = GameMenuOption.LeaveType.Wait;
                    string why = WhyNot();
                    if (why != null) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); }
                    else a.Tooltip = new TextObject("{=!}Drill by day: a full day (8 hours) teaches 50% more than a day of rest did; the pace follows your Leadership"
                                                    + " and your best drill master's. Marching teaches nothing.");
                    return true;
                },
                delegate (MenuCallbackArgs a) { GameMenu.SwitchToMenu(Menu); }, false, 2);

            starter.AddWaitGameMenu(Menu, "{=!}{ARM_DRILL_TEXT}", DrillInit, delegate (MenuCallbackArgs a) { return true; }, null, DrillTick,
                GameMenu.MenuAndOptionType.WaitMenuShowProgressAndHoursOption);
            starter.AddGameMenuOption(Menu, "arm_camp_drill_stop", "{=!}Stop the drill",
                delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                // ZAKAZ SwitchToMenu z opcji menu oczekiwania (CTD, CLAUDE.md): opcja podnosi flage, DrillTick przelacza (wzorzec SmithMenu "Step away");
                // zegar ruszamy sami - ratunek po wczytaniu w srodku menu oczekiwania
                delegate (MenuCallbackArgs a)
                {
                    _leave = true;
                    try { Campaign.Current.TimeControlMode = CampaignTimeControlMode.StoppablePlay; a.MenuContext.GameMenu.StartWait(); } catch { }
                }, true, 9);
            Log.Info(P + "opcja \"Train the men\" w menu obozu (arm_camp_wait) i menu cwiczen " + Menu + " | klucz Drill Camp Only " + (PlayerOn(Settings.Current) ? "TAK" : "NIE") + ".");
        }

        /// <summary>Powod, dla ktorego teraz nie ma cwiczen (null = mozna).</summary>
        private static string WhyNot()
        {
            try
            {
                var mp = MobileParty.MainParty;
                if (Campaign.Current == null || mp == null) return "No campaign is running.";
                if (mp.MapEvent != null || mp.SiegeEvent != null || PlayerEncounter.Current != null) return "Not while in a battle or siege.";
                if (mp.CurrentSettlement != null || mp.IsCurrentlyAtSea || mp.AttachedTo != null) return "Only in your own camp on land.";
                if (mp.MemberRoster == null || mp.MemberRoster.TotalRegulars <= 0) return "You have no men to drill.";
                Roll();
                if (!DayHour(CampaignTime.Now.GetHourOfDay))
                    return "The men are asleep. The drill runs by day, from " + NightRest.PlayerDawn + ":00 to " + NightRest.SleepNightStart + ":00.";
                if (mp.Party != null && mp.Party.IsStarving) return "The men are too hungry to drill.";
                if (NightRest.DawnDebtOf(mp) >= 1) return "The men met the dawn short of sleep - no drill until the next dawn.";
                if (_dayHours >= FullHours - 0.01f) return "The men have drilled a full day (8 hours) - they can learn no more today.";
            }
            catch (Exception e) { Stumble("WhyNot", e); return "Not now."; }
            return null;
        }

        private static void DrillInit(MenuCallbackArgs args)
        {
            try
            {
                NightRest.SetCampBackground(args);
                var mp = MobileParty.MainParty;
                if (mp != null) mp.SetMoveModeHold();      // cwiczenia = postoj
                _leave = false;
                // kazde wejscie (takze ponowne po wczytaniu albo po spotkaniu, ktore gra przelaczyla sama) - nowa sesja cwiczen do linii "koniec cwiczen"
                _session = 0f; _sessionStart = CampaignTime.Now.ToHours; _sessions++;
                Roll();
                SetText();
                args.MenuContext.GameMenu.StartWait();
            }
            catch (Exception e) { Stumble("DrillInit", e); }
        }

        private static void SetText()
        {
            MBTextManager.SetTextVariable("ARM_DRILL_TEXT", "The men drill in camp - sword and shield, the spear wall, volleys at the butts, marching in step. Today: "
                + F1(_dayHours) + " of " + (int)FullHours + " hours. What they learn is counted with their daily drill.");
        }

        private static void Leave(string why, string msg)
        {
            try
            {
                if (msg != null) Log.Player(msg);
                var s = Settings.Current;
                if (s != null && s.DrillLog)
                    Log.Info(P + "koniec cwiczen | " + F2(_session) + " h cwiczen (zegara " + F1(CampaignTime.Now.ToHours - _sessionStart) + " h) | dzis " + F2(_dayHours) + " z "
                             + (int)FullHours + ", do policzenia " + F2(_owed) + " | powod: " + why + ".");
            }
            catch (Exception e) { Stumble("Leave", e); }
            GameMenu.SwitchToMenu("arm_camp_wait");
        }

        private static void DrillTick(MenuCallbackArgs args, CampaignTime dt)
        {
            try
            {
                var mp = MobileParty.MainParty;
                // bitwa i spotkanie przelacza gra sama - tu nic
                if (mp == null || mp.MapEvent != null || PlayerEncounter.Current != null) return;
                if (_leave) { _leave = false; Leave("gracz", null); return; }
                Roll();
                if (!DayHour(CampaignTime.Now.GetHourOfDay)) { Leave("noc", "Dusk - the drill is over for today; the men eat and bed down."); return; }
                if (mp.Party != null && mp.Party.IsStarving) { Leave("glod", "The men are too hungry to drill."); return; }
                // recenzja 1: kare pilnuje menu w chwili cwiczen (tick treningu juz jej nie stosuje do godzin zrobionych) - dlug o swicie takze tu, nie tylko przy wejsciu
                if (NightRest.DawnDebtOf(mp) >= 1) { Leave("dlug snu", "The men met the dawn short of sleep - no drill until the next dawn."); return; }
                if (mp.CurrentSettlement != null || mp.AttachedTo != null || mp.BesiegerCamp != null || mp.SiegeEvent != null || mp.IsMoving) { Leave("ruch", null); return; }
                float add = (float)dt.ToHours;
                float take = Math.Max(0f, Math.Min(add, FullHours - _dayHours));
                _dayHours += take; _owed += take; _session += take; _pSession += take;
                if (take > 0f) _lastDrillAt = CampaignTime.Now.ToHours;   // ksiega snu: ta godzina to praca, nie odpoczynek (recenzja 4)
                if (_dayHours >= FullHours - 0.001f)
                {
                    Leave("pelny dzien", "A full day of drill (8 hours) - the men can learn no more today.");
                    return;
                }
                SetText();
                args.MenuContext.GameMenu.SetProgressOfWaitingInMenu(Math.Min(1f, _dayHours / FullHours));
            }
            catch (Exception e) { Stumble("DrillTick", e); }
        }

        // ------------------------------------------------------------ pomiar (Drill.Done) i linia AI
        /// <summary>Raz na element oddzialu: XP po broni dawna regula i zasada obozu (oba z kara glodu i snu), XP naprawde dane. Partia - raz (first).</summary>
        internal static void Count(Drill.Ctx c, bool first, int n, double oldXp, double campXp, double nowXp)
        {
            try
            {
                if (c == null || c.Main) return;
                TownStay.Count(c, first, campXp);   // m109-musztra: koszyki dob postoju, XP bez limitu i z limitem (wlasny try)
                if (c.NoBase)
                {
                    if (first) { _sParties++; _sH += c.H; }
                    if (c.Camp && c.H <= 0f && nowXp > 0.5) _sMarchXpParties++;   // oddzialy (elementy) z XP bez godziny postoju za dnia - ma byc 0
                    _sOld += oldXp; _sCamp += campXp; _sNow += nowXp;
                    return;
                }
                if (first)
                {
                    _aParties++; _aH += c.H;
                    if (c.H <= 0f) _aH0++; else if (c.H >= FullHours) _aH8++;
                }
                if (c.Camp && c.H <= 0f && nowXp > 0.5) _aMarchXpParties++;
                _aOld += oldXp; _aCamp += campXp; _aNow += nowXp; _aMen += n;
            }
            catch (Exception e) { Stumble("Count", e); }
        }

        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.DrillLog) { ClearDay(); return; }
                int day = (int)CampaignTime.Now.ToDays;
                double men = _aMen > 0 ? _aMen : 1;
                Log.Info(P + "AI dzien " + day + " | zasada AI " + (RuleFor(false, false, s) ? "TAK" : "NIE (AI jak dotad, ponizej pomiar)") + " | partii " + _aParties
                         + " (ludzio-dni " + (int)_aMen + ") | godzin postoju za dnia (najwyzej " + (int)FullHours + ") srednio " + F2(_aParties > 0 ? _aH / _aParties : 0)
                         + ": 0 h " + _aH0 + ", 1-7 h " + (_aParties - _aH0 - _aH8) + ", 8 h " + _aH8 + " | XP po broni: dawna regula " + (long)_aOld + " -> zasada obozu " + (long)_aCamp
                         + " (x" + F2(_aOld > 0 ? _aCamp / _aOld : 0) + "), na glowe na dobe " + F2(_aOld / men) + " -> " + F2(_aCamp / men) + ", dane naprawde " + (long)_aNow
                         + " | XP z marszu (0 h postoju za dnia, XP > 0) przy czynnej zasadzie: oddzialow " + _aMarchXpParties
                         + " | strona gracza (rod, armia): partii " + _sParties + ", godzin srednio " + F2(_sParties > 0 ? _sH / _sParties : 0) + ", XP dawna " + (long)_sOld
                         + " -> obozu " + (long)_sCamp + ", dane " + (long)_sNow + ", z marszu oddzialow " + _sMarchXpParties + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { TownStay.DrillLine(); ClearDay(); }   // m109-musztra: linia musztry postoju po linii AI (wlasny try, pod DrillLog; zeruje swoje liczniki)
        }

        // ------------------------------------------------------------ zapis
        // m109-musztra: pola 5-12 "|postoj od|godzin w polu|w osadzie od switu|w osadzie do policzenia|limit osady od switu|poranki|doba od switu poranka|godzin
        // w osadzie bez przerwy" (wersja zostaje "1": stara DLL czyta 4 pierwsze pola, nowa DLL ze starego zapisu - postoj od nowa; zapis 8-9 pol z wczesnej wersji
        // galezi, nigdy w grze - godziny w osadzie od switu tak, postoj od nowa)
        internal static string Export()
        {
            var ci = CultureInfo.InvariantCulture;
            return "1|" + _dayHours.ToString("0.###", ci) + "|" + _dayIdx.ToString(ci) + "|" + _owed.ToString("0.###", ci)
                   + "|" + _stay.From.ToString(ci) + "|" + _stay.Field.ToString(ci) + "|" + _daySet.ToString("0.###", ci) + "|" + _owedSet.ToString("0.###", ci)
                   + "|" + _dayCapMax.ToString("0.###", ci) + "|" + _stay.Day.ToString(ci) + "|" + _stay.Dawn.ToString(ci) + "|" + _stay.Run.ToString(ci);
        }

        internal static void Import(string v)
        {
            _dayHours = 0f; _dayIdx = -1; _owed = 0f;
            _stay.Clear(); _daySet = _owedSet = _dayCapMax = 0f;   // m109-musztra: stary zapis - bez postoju
            try
            {
                if (string.IsNullOrEmpty(v)) return;
                var p = v.Split('|');
                if (p.Length < 4 || p[0] != "1") return;
                float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out _dayHours);
                long.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _dayIdx);
                float.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out _owed);
                _dayHours = Math.Max(0f, Math.Min(FullHours, _dayHours));
                _owed = Math.Max(0f, Math.Min(3f * FullHours, _owed));
                if (p.Length >= 8)
                {
                    float.TryParse(p[6], NumberStyles.Float, CultureInfo.InvariantCulture, out _daySet);
                    float.TryParse(p[7], NumberStyles.Float, CultureInfo.InvariantCulture, out _owedSet);
                    if (p.Length >= 9) float.TryParse(p[8], NumberStyles.Float, CultureInfo.InvariantCulture, out _dayCapMax);
                    _daySet = Math.Max(0f, Math.Min(_dayHours, _daySet));
                    _owedSet = Math.Max(0f, Math.Min(_owed, _owedSet));
                    _dayCapMax = Math.Max(0f, Math.Min(FullHours, _dayCapMax));
                }
                if (p.Length >= 12)
                {
                    long from, dawn; int field, day, run;
                    if (long.TryParse(p[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out from) && from >= 0
                        && int.TryParse(p[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out field) && int.TryParse(p[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out day)
                        && long.TryParse(p[10], NumberStyles.Integer, CultureInfo.InvariantCulture, out dawn) && int.TryParse(p[11], NumberStyles.Integer, CultureInfo.InvariantCulture, out run)
                        && field >= 0 && field < TownStay.FieldResetHours && day >= 0 && run >= 0 && (day == 0 || dawn != long.MinValue))
                    { _stay.From = from; _stay.Field = field; _stay.Day = day; _stay.Dawn = day > 0 ? dawn : long.MinValue; _stay.Run = run; }
                }
            }
            catch (Exception e) { Stumble("Import", e); _dayHours = 0f; _dayIdx = -1; _owed = 0f; _stay.Clear(); _daySet = _owedSet = _dayCapMax = 0f; }
        }
    }
}
