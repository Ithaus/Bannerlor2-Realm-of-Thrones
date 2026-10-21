using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// m105-drobne pkt 2 (T-4, test 102: "ERROR in QuestPay8.ShownReward: System.DivideByZeroException", tablica "nagroda nieczytelna 1") - SPRAWA "Gang Leader Needs
    /// Weapons" Z SENSOWNA SREDNIA CENA SIEKIER (klucz QuestGangAxesFix).
    /// Gra (dekompilacja 1.4.8, GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue):
    ///  - konstruktor :174-187: dla kazdej klasy z listy (tylko OneHandedAxe, :1238) CalculateAveragePriceForWeaponClass; klasa wchodzi do losowania tylko przy
    ///    wyniku > 0; pusta lista -> GetRandomElement = default -> _averagePriceForItem 0, _requiredWeaponClassIndex = FindIndex(Undefined) = -1;
    ///  - CalculateAveragePriceForWeaponClass :208-228: suma WARTOSCI SZTUKI po elementach polek miast (num += ItemValue - raz na element) dzielona CALKOWICIE przez
    ///    sume SZTUK (num2 += Amount) - to nie jest srednia cena. Przy cenach historycznych (siekiery 8-52 d) i pelnych polkach suma wartosci < suma sztuk -> 0,
    ///    a gdy nie zero - kilka denarow zamiast ceny siekiery. Zero daje tez brak siekier 1H na polkach;
    ///  - RequestedWeaponAmount :47-58: straznik "3 + avg == 0" (literowka gry, nigdy prawda) -> 20000 / 0; RewardGold = 500 + avg x ilosc wola ten getter
    ///    (tablica QuestPay8.ShownReward, teksty rozmowy NEEDED_AMOUNT), a indeks -1 wywraca konstruktor zadania (IndexOutOfRange przy przyjeciu, :506).
    /// Zmiana: postfiks konstruktora GangLeaderNeedsWeaponsIssue(Hero) wpisuje SREDNIA WAZONA ceny siekier 1H z polek miast (suma ItemValue x sztuki / sztuki - ta
    /// sama petla i te same przedmioty co gra, wynik >= 1); polki bez siekier 1H - srednia Value towarowych siekier 1H z definicji przedmiotow (ta sama sciezka co
    /// zapas QuestQty28.AxeRatio); indeks klasy 0 (jedyna klasa listy). Nagroda = 500 + cena x ilosc (QuestPay8: czesc stala to praca, reszta towar po cenie) - teraz
    /// "cena x ilosc" to naprawde koszt siekier; ilosc liczy dalej QuestQty28 (20000 w starej monecie / srednia sprzed przeliczenia). Sprawy z zapisu: przy starcie
    /// sesji naprawiane TYLKO zepsute (srednia <= 0 albo indeks poza lista) - sprawa poprawna zostaje jak byla (gracz mogl juz slyszec ilosc w rozmowie).
    /// Klucz wylaczony - gra bez zmian (latka wpieta, postfiks nic nie robi). Latka W KAMPANII (CLAUDE.md 7), raz na proces.
    /// Log: linia startu (wpiecie, sprawy z zapisu), linia na kazda nowa / naprawiona sprawe (najwyzej 40 na sesje).
    /// </summary>
    internal static class QuestGangAxes
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.QuestGangAxesFix; } }

        private const string Outer = "TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior", Inner = "GangLeaderNeedsWeaponsIssue";
        private const int MaxLines = 40;

        private static Harmony _h;
        private static bool _tried, _hooked;
        private static Type _t;
        private static FieldInfo _fAvg, _fIdx;
        private static WeaponClass[] _classes;
        private static string _status = "";
        private static int _new, _fixedNew, _fixedLoad, _fixedLate, _stumbles, _lines;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 5) Log.Error("QuestGangAxes." + where, e);
        }

        // ------------------------------------------------------------ srednia ceny siekier

        /// <summary>Srednia wazona ceny sztuki klasy `wc` z polek miast (ta sama petla i te same przedmioty co gra); 0 - brak takich sztuk na polkach.</summary>
        private static int ShelfAverage(WeaponClass wc, out long pieces, out int towns)
        {
            pieces = 0; towns = 0;
            double sum = 0.0;
            foreach (Settlement st in Settlement.All)
            {
                if (st == null || !st.IsTown || st.ItemRoster == null) continue;
                var ro = st.ItemRoster;
                bool any = false;
                for (int i = 0; i < ro.Count; i++)
                {
                    var el = ro.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    var w = it != null ? it.WeaponComponent : null;
                    if (w == null || w.PrimaryWeapon == null || w.PrimaryWeapon.WeaponClass != wc || el.Amount <= 0) continue;
                    sum += (double)el.EquipmentElement.ItemValue * el.Amount;
                    pieces += el.Amount;
                    any = true;
                }
                if (any) towns++;
            }
            return pieces > 0 ? Math.Max(1, (int)Math.Round(sum / pieces)) : 0;
        }

        /// <summary>Srednia Value towarowych przedmiotow klasy `wc` z definicji (zapas przy pustych polkach); 0 - brak takich przedmiotow.</summary>
        private static int DefAverage(WeaponClass wc, out int kinds)
        {
            kinds = 0;
            double sum = 0.0;
            foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
            {
                var w = it != null ? it.WeaponComponent : null;
                if (w == null || it.NotMerchandise || w.PrimaryWeapon == null || w.PrimaryWeapon.WeaponClass != wc || it.Value <= 0) continue;
                sum += it.Value; kinds++;
            }
            return kinds > 0 ? Math.Max(1, (int)Math.Round(sum / kinds)) : 0;
        }

        private static string Who(IssueBase iss)
        {
            try
            {
                var h = iss != null ? iss.IssueOwner : null;
                var st = h != null ? h.CurrentSettlement : null;
                return (h != null && h.Name != null ? h.Name.ToString() : "?") + (st != null ? " w " + st.StringId : "");
            }
            catch { return "?"; }
        }

        /// <summary>
        /// Sprawa broni gangu: `always` (nowa sprawa) - srednia gry zastapiona srednia wazona z polek; inaczej (zapis, pozny odczyt) - tylko sprawa zepsuta
        /// (srednia <= 0 albo indeks klasy poza lista). true - pola zmienione.
        /// </summary>
        private static bool Repair(object issue, bool always, string when)
        {
            if (issue == null || _fAvg == null || _fIdx == null || _classes == null || _classes.Length == 0) return false;
            if (_t != null && issue.GetType() != _t) return false;
            int avg = (int)_fAvg.GetValue(issue), idx = (int)_fIdx.GetValue(issue);
            bool broken = avg <= 0 || idx < 0 || idx >= _classes.Length;
            if (!broken && !always) return false;
            int ci = broken ? 0 : idx;
            var wc = _classes[ci];
            long pieces; int towns, kinds = 0;
            int na = ShelfAverage(wc, out pieces, out towns);
            string src = na > 0 ? "srednia wazona polek (" + pieces + " szt. w " + towns + " miastach)" : "";
            if (na <= 0) { na = DefAverage(wc, out kinds); src = na > 0 ? "polki bez siekier 1H - srednia z definicji (" + kinds + " rodzajow)" : ""; }
            if (na <= 0)
            {
                if (!broken) return false;
                na = 1; src = "brak siekier 1H na polkach i w definicjach - 1";
            }
            if (!broken && na == avg) return false;
            _fAvg.SetValue(issue, na);
            if (broken) _fIdx.SetValue(issue, ci);
            if (broken) { if (when == "zapis") _fixedLoad++; else if (when == "pozny odczyt") _fixedLate++; else _fixedNew++; }
            else _new++;
            if (_lines < MaxLines)
            {
                _lines++;
                Log.Info("Bron gangu (T-4): " + (broken ? "NAPRAWIONA sprawa" : "nowa sprawa") + " " + Who(issue as IssueBase) + " (" + when + ") - srednia ceny siekier gry " + avg
                         + (broken ? (avg <= 0 ? " (zero: dzielenie calkowite gry albo brak siekier 1H na polkach)" : "") + ", indeks klasy " + idx + " -> " + ci : "")
                         + " -> " + na + " d (" + src + ") | w sesji: nowe " + _new + ", naprawione " + (_fixedNew + _fixedLoad + _fixedLate) + " (nowe " + _fixedNew
                         + ", z zapisu " + _fixedLoad + ", przy odczycie " + _fixedLate + ").");
            }
            return true;
        }

        // ------------------------------------------------------------ latka i wolania

        /// <summary>Postfiks konstruktora GangLeaderNeedsWeaponsIssue(Hero): nowa sprawa ze srednia wazona ceny siekier (klucz).</summary>
        public static void CtorPost(object __instance)
        {
            if (!On) return;
            try { Repair(__instance, true, "nowa"); }
            catch (Exception e) { Stumble("CtorPost", e); }
        }

        /// <summary>QuestQty28.OldAvg przy sredniej <= 0 (sprawa sprzed latki albo z wylaczonym kluczem): naprawa zepsutej sprawy przed dzieleniem. true - naprawiona.</summary>
        internal static bool RepairLate(object issue)
        {
            if (!On || !_hooked) return false;
            try { return Repair(issue, false, "pozny odczyt"); }
            catch (Exception e) { Stumble("RepairLate", e); return false; }
        }

        /// <summary>Z ArmouryBehavior.OnSessionLaunched (W KAMPANII - CLAUDE.md 7): latka raz na proces, naprawa zepsutych spraw z zapisu, linia startu.</summary>
        internal static void SessionStart()
        {
            _new = _fixedNew = _fixedLoad = _fixedLate = _lines = 0;
            EnsureHooks();
            int seen = 0;
            if (_hooked && On)
            {
                try
                {
                    var im = Campaign.Current != null ? Campaign.Current.IssueManager : null;
                    if (im != null && im.Issues != null)
                        foreach (var kv in im.Issues)
                        {
                            var iss = kv.Value;
                            if (iss == null || iss.GetType() != _t) continue;
                            seen++;
                            try { Repair(iss, false, "zapis"); } catch (Exception e) { Stumble("SessionStart(sprawa)", e); }
                        }
                }
                catch (Exception e) { Stumble("SessionStart", e); }
            }
            Log.Info("Bron gangu (T-4, klucz Quest Gang Axes Fix " + (On ? "TAK" : "NIE") + "): " + _status + " | sprawy broni gangu z zapisu " + seen + ", naprawione " + _fixedLoad
                     + (_stumbles > 0 ? " | potkniecia " + _stumbles : "") + ".");
        }

        private static void EnsureHooks()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                var outer = AccessTools.TypeByName(Outer);
                _t = outer != null ? AccessTools.Inner(outer, Inner) : null;
                if (_t == null) { _status = "BRAK typu " + Inner + " - nic nie wpiete"; return; }
                _fAvg = AccessTools.Field(_t, "_averagePriceForItem");
                _fIdx = AccessTools.Field(_t, "_requiredWeaponClassIndex");
                var fl = AccessTools.Field(outer, "_canBeRequestedWeaponClassList");
                _classes = fl != null ? fl.GetValue(null) as WeaponClass[] : null;
                var ctor = AccessTools.DeclaredConstructor(_t, new[] { typeof(Hero) });
                var miss = new List<string>();
                if (_fAvg == null || _fAvg.FieldType != typeof(int)) miss.Add("pole _averagePriceForItem");
                if (_fIdx == null || _fIdx.FieldType != typeof(int)) miss.Add("pole _requiredWeaponClassIndex");
                if (_classes == null || _classes.Length == 0) miss.Add("lista klas broni");
                if (ctor == null) miss.Add("konstruktor (Hero)");
                if (miss.Count > 0) { _status = "BRAK: " + string.Join(", ", miss.ToArray()) + " - nic nie wpiete"; _fAvg = null; return; }
                if (_h == null) _h = new Harmony("com.jeff.armoury.m105gangaxes");
                _h.Patch(ctor, postfix: new HarmonyMethod(typeof(QuestGangAxes), nameof(CtorPost)));
                _hooked = true;
                _status = "latka konstruktora " + Inner + "(Hero) WPIETA (klasy broni listy: " + _classes.Length + ", pierwsza " + _classes[0] + ")";
            }
            catch (Exception e) { _status = "BLAD wpiecia (" + e.GetType().Name + ")"; Stumble("EnsureHooks", e); }
        }
    }
}
