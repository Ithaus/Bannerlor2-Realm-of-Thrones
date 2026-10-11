using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// KROK 3.10 - DROBNE Z NICZEGO (PLAN-DO-KONCA-MODA, etap 3). Z pieciu podejrzanych trzy sa prawdziwe i tu sie zamykaja:
    ///  A. FURAZ (ScorchedEarth): armia przy wrogiej wsi brala 1 + ludzie/250 zboza z niczego (bieg 457 dob: 2215 szt., srednio 4.8 na
    ///     dobe, najwiecej 36). Teraz bierze z zapasu tej wsi (Settlement.ItemRoster): najpierw zboze, potem inna zywnosc; pusta wies
    ///     nie karmi nikogo. Przewoz wies -> partia (lup wojenny bez zaplaty, jak rabunek), nic nie powstaje.
    ///  B. HERSZT KRYJOWKI (gra, BanditSpawnCampaignBehavior.CheckForSpawningBanditBoss): przy wejsciu bandy do wypatrzonej kryjowki gra
    ///     dopisuje partii herszta jednego BanditBoss z niczego, gdy go tam nie ma (sklad partii herszta idzie z puli wyrzutkow
    ///     OutlawLaw, a w puli herszta nie ma). Teraz hersztem zostaje najlepszy z jej wlasnych ludzi (jeden mniej z najwyzszego tieru);
    ///     partia bez ludzi herszta nie dostaje (zostaje pusta - OutlawLaw usuwa ja w ciagu godziny).
    ///  C. ZOLD ZACIAGU ROT (ROT.Models.ROTClanFinanceModel.CalculateClanGoldChange): w sluzbie lorda ROT dopisywal rodowi gracza
    ///     zold GRACZA za kazdego bohatera w jego partii (n x zold gracza) - z niczego, nikt nie placil. Teraz ten wpis jest zdejmowany
    ///     z rozliczenia rodu, a raz na dobe pan, u ktorego sluzysz, placi ze swojej kiesy kazdemu bohaterowi jego WLASNY zold
    ///     (tyle, ile ma). Ta sama regula co u AI: partie lorda oplaca jej wodz.
    /// Nieprawdziwe (opis w CHANGELOG): kopalnie BK (wpis 88 - polowa do pana przez podatek, polowa wraca do miasta; ruda to praca
    /// gornikow, wpis 50) i start BK "Mariner" 10 000 zl (jednorazowe wyposazenie startowe gracza, nie przeplyw - pytanie do Jeffa).
    /// Kazdy kawalek za wlasnym wylacznikiem (domyslnie wlaczony). Wyjatki liczone (potkniecia), mechanizm nigdy sie nie gasi.
    /// </summary>
    internal static class Leaks310
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static Harmony _harmony;
        private static bool _bossWired, _rotTried, _rotHooked, _rotFields;

        // liczniki doby (zerowane po linii logu)
        private static int _fParties, _fWant, _fGrain, _fOther, _fShort, _fStumbles;
        private static int _bPromoted, _bNoMen, _bStumbles;
        private static long _pRemoved; private static int _pRemovedN;
        private static long _pDue, _pPaid; private static int _pHeroes; private static string _pLord; private static int _pStumbles;
        private static bool _errF, _errB, _errP;

        private static Settings S { get { return Settings.Current; } }

        // ------------------------------------------------------------ A. furaz z zapasu wsi
        /// <summary>ScorchedEarth.OnDaily: partia bierze z zapasu wsi do `want` sztuk zywnosci (najpierw zboze). Zwraca, ile wziela.</summary>
        internal static int Forage(Village v, MobileParty mp, int want)
        {
            int got = 0;
            try
            {
                if (v == null || v.Settlement == null || mp == null || mp.ItemRoster == null || want <= 0) return 0;
                _fParties++; _fWant += want;
                var stock = v.Settlement.ItemRoster;
                if (stock != null)
                {
                    // kopia elementow przed zmianami rosteru; zboze na poczatek listy
                    var food = new List<EquipmentElement>();
                    var counts = new List<int>();
                    int firstOther = 0;
                    for (int i = 0; i < stock.Count; i++)
                    {
                        var el = stock.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (it == null || el.Amount <= 0 || !it.IsFood) continue;
                        if (it.StringId == "grain") { food.Insert(firstOther, el.EquipmentElement); counts.Insert(firstOther, el.Amount); firstOther++; }
                        else { food.Add(el.EquipmentElement); counts.Add(el.Amount); }
                    }
                    for (int i = 0; i < food.Count && got < want; i++)
                    {
                        int n = Math.Min(want - got, counts[i]);
                        if (n <= 0) continue;
                        stock.AddToCounts(food[i], -n);
                        mp.ItemRoster.AddToCounts(food[i], n);
                        got += n;
                        if (i < firstOther) _fGrain += n; else _fOther += n;
                    }
                }
                if (got < want) _fShort += want - got;
            }
            catch (Exception e)
            {
                _fStumbles++;
                if (!_errF) { _errF = true; Log.Error("Leaks310.Forage", e); }
            }
            return got;
        }

        // ------------------------------------------------------------ B. herszt kryjowki z wlasnych ludzi
        public struct BossState { public MobileParty Party; public CharacterObject Boss; public bool Had; }

        /// <summary>Prefiks CheckForSpawningBanditBoss: partia herszta w tej kryjowce i czy herszt juz w niej jest.</summary>
        public static void BossPrefix(Settlement __0, out BossState __state)
        {
            __state = default(BossState);
            try
            {
                var s = S;
                if (s == null || !s.HideoutBossFromOwnMen || __0 == null || !__0.IsHideout || __0.Culture == null) return;
                var boss = __0.Culture.BanditBoss;
                if (boss == null) return;
                MobileParty bp = null;
                foreach (var p in __0.Parties) if (p != null && p.IsBanditBossParty) { bp = p; break; }
                if (bp == null || bp.MemberRoster == null) return;   // partii jeszcze nie ma: gra ja tworzy, herszta dopisuje wywolanie zagniezdzone
                __state.Party = bp; __state.Boss = boss; __state.Had = bp.MemberRoster.Contains(boss);
            }
            catch (Exception e) { BossStumble(e); }
        }

        /// <summary>Postfiks: gra dopisala herszta z niczego - zdejmujemy jednego z jej wlasnych ludzi (najwyzszy tier), ten zostaje hersztem.</summary>
        public static void BossPostfix(BossState __state)
        {
            if (__state.Party == null || __state.Had || __state.Boss == null) return;
            try
            {
                var r = __state.Party.MemberRoster;
                if (r == null || !r.Contains(__state.Boss)) return;   // gra nic nie dopisala
                int best = -1, bestTier = int.MinValue;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var ch = el.Character;
                    if (ch == null || ch.IsHero || ch == __state.Boss || el.Number <= 0) continue;
                    int t = ch.Tier;
                    if (t > bestTier) { bestTier = t; best = i; }
                }
                if (best >= 0)
                {
                    var el = r.GetElementCopyAtIndex(best);
                    bool wounded = el.WoundedNumber >= el.Number;          // sami ranni - herszt tez ranny
                    r.AddToCounts(el.Character, -1, false, wounded ? -1 : 0);
                    if (wounded) r.AddToCounts(__state.Boss, 0, false, 1);
                    _bPromoted++;
                }
                else
                {
                    r.AddToCounts(__state.Boss, -1);                       // nikogo do awansu - herszta nie ma
                    _bNoMen++;
                    MarkEmpty(__state.Party);
                }
            }
            catch (Exception e) { BossStumble(e); }
        }

        private static FieldInfo _fEmpty; private static bool _emptyResolved;
        /// <summary>Partia herszta bez ludzi: do listy pustych band OutlawLaw (OutlawLaw.Hourly usuwa je, gdy dalej puste).</summary>
        private static void MarkEmpty(MobileParty p)
        {
            try
            {
                if (p == null || p.MemberRoster == null || p.MemberRoster.TotalManCount > 0) return;
                if (!_emptyResolved) { _emptyResolved = true; _fEmpty = AccessTools.Field(typeof(OutlawLaw), "_empty"); }
                var set = _fEmpty != null ? _fEmpty.GetValue(null) as HashSet<MobileParty> : null;
                if (set != null) set.Add(p);
            }
            catch (Exception e) { BossStumble(e); }
        }

        private static void BossStumble(Exception e)
        {
            _bStumbles++;
            if (!_errB) { _errB = true; Log.Error("Leaks310.Boss", e); }
        }

        // ------------------------------------------------------------ C. zold zaciagu ROT
        private static FieldInfo _fBeh, _fIsEnl, _fHero;
        private static readonly TextObject _txtPay = new TextObject("{=!}Enlisted pay is handed over by your lord each day, each hero his own wage");

        private static bool ResolveRot()
        {
            if (_rotFields) return true;
            try
            {
                var sub = AccessTools.TypeByName("ROT.SubModule");
                _fBeh = sub != null ? AccessTools.Field(sub, "EnlistmentBehavior") : null;
                var bt = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTEnlistmentBehavior");
                _fIsEnl = bt != null ? AccessTools.Field(bt, "IsEnlisted") : null;
                _fHero = bt != null ? AccessTools.Field(bt, "EnlistedHero") : null;
                _rotFields = _fBeh != null && _fIsEnl != null && _fHero != null && _fIsEnl.FieldType == typeof(bool) && _fHero.FieldType == typeof(Hero);
            }
            catch { _rotFields = false; }
            return _rotFields;
        }

        /// <summary>Gracz w sluzbie lorda ROT, a ROT dopisuje mu zold (te same warunki co ROTClanFinanceModel): pan, liczba bohaterow
        /// w partii gracza, suma ROT (n x zold gracza) i nalezne (kazdy bohater swoj zold).</summary>
        private static bool Company(out Hero lord, out int heroes, out long rotSum, out long due)
        {
            lord = null; heroes = 0; rotSum = 0; due = 0;
            if (!ResolveRot() || Campaign.Current == null) return false;
            var beh = _fBeh.GetValue(null);
            if (beh == null || !(bool)_fIsEnl.GetValue(beh)) return false;
            lord = _fHero.GetValue(beh) as Hero;
            if (lord == null || lord.Clan == null) return false;
            // ROT: u Wolnych Ludzi i u Innych zoldu nie ma
            Kingdom ff = null;
            foreach (var k in Campaign.Current.Kingdoms) if (k != null && k.StringId == "freefolk") { ff = k; break; }
            if (lord.Clan.Kingdom == ff) return false;
            if (lord.Clan.Culture != null && lord.Clan.Culture.StringId == "whitewalker") return false;
            var main = Hero.MainHero; var mp = MobileParty.MainParty; var pc = Clan.PlayerClan;
            if (main == null || mp == null || pc == null || main.CharacterObject == null) return false;
            int w = main.CharacterObject.TroopWage;
            var seen = new HashSet<Hero>();
            foreach (var h in pc.Heroes) Count(h, mp, w, seen, ref heroes, ref rotSum, ref due);
            foreach (var h in pc.Companions) Count(h, mp, w, seen, ref heroes, ref rotSum, ref due);
            return heroes > 0;
        }

        private static void Count(Hero h, MobileParty mp, int w, HashSet<Hero> seen, ref int heroes, ref long rotSum, ref long due)
        {
            if (h == null || !seen.Add(h) || h.PartyBelongedTo != mp || h.CharacterObject == null) return;
            heroes++; rotSum += w; due += h.CharacterObject.TroopWage;
        }

        /// <summary>Postfiks ROTClanFinanceModel.CalculateClanGoldChange: zold zaciagu ROT (z niczego) zdjety tym samym wpisem - placi pan w Daily.</summary>
        public static void RotPayPostfix(Clan __0, bool __2, ref ExplainedNumber __result)
        {
            try
            {
                var s = S;
                if (s == null || !s.EnlistedPayFromLord || __0 == null || __0 != Clan.PlayerClan) return;
                Hero lord; int heroes; long rotSum, due;
                if (!Company(out lord, out heroes, out rotSum, out due) || rotSum <= 0) return;
                __result.Add(-rotSum, _txtPay);
                if (__2) { _pRemoved += rotSum; _pRemovedN++; }
            }
            catch (Exception e) { PayStumble(e); }
        }

        private static void PayStumble(Exception e)
        {
            _pStumbles++;
            if (!_errP) { _errP = true; Log.Error("Leaks310.Pay", e); }
        }

        /// <summary>Raz na dobe: pan placi bohaterom w partii gracza ich zold (tylko przy wpietej latce - inaczej ROT dalej placi sam).</summary>
        private static void PayDaily()
        {
            try
            {
                var s = S;
                if (s == null || !s.EnlistedPayFromLord || !_rotHooked) return;
                Hero lord; int heroes; long rotSum, due;
                if (!Company(out lord, out heroes, out rotSum, out due) || due <= 0) return;
                int pay = (int)Math.Min(due, (long)Math.Max(0, lord.Gold));
                if (pay > 0) GiveGoldAction.ApplyBetweenCharacters(lord, Hero.MainHero, pay, true);
                _pDue += due; _pPaid += pay; _pHeroes = heroes; _pLord = lord.Name != null ? lord.Name.ToString() : lord.StringId;
                if (pay < due)
                    Log.Player(_pLord + " could not pay your company in full today: " + pay + " of " + due + " coins.", true);
            }
            catch (Exception e) { PayStumble(e); }
        }

        // ------------------------------------------------------------ wpiecia
        /// <summary>Start gry (SubModuleMain): latka herszta kryjowki (metoda gry, bez modeli).</summary>
        internal static void ApplyAll(Harmony h)
        {
            _harmony = h;
            try
            {
                var t = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.BanditSpawnCampaignBehavior");
                var m = t != null ? AccessTools.Method(t, "CheckForSpawningBanditBoss", new[] { typeof(Settlement), typeof(MobileParty) }) : null;
                if (m != null)
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(Leaks310), nameof(BossPrefix)), postfix: new HarmonyMethod(typeof(Leaks310), nameof(BossPostfix)));
                    _bossWired = true;
                }
            }
            catch (Exception e) { Log.Error("Leaks310.ApplyAll", e); }
            var s = S;
            Log.Info("Drobne z niczego (3.10): furaz z zapasu wsi " + (s != null && s.ForageFromVillageStock ? "CZYNNE" : "wylaczone")
                     + "; herszt kryjowki z wlasnych ludzi " + (_bossWired ? "wpiety" : "BRAK (CheckForSpawningBanditBoss)") + (s != null && s.HideoutBossFromOwnMen ? "" : " (wylaczony w MCM)")
                     + "; zold zaciagu ROT od pana - latka modelu finansow w kampanii" + (s != null && s.EnlistedPayFromLord ? "" : " (wylaczony w MCM)") + ".");
        }

        /// <summary>Kampania (OnSessionLaunched): postfiks na zold zaciagu ROT - raz na proces (model ROT; latki modeli tylko w kampanii).</summary>
        internal static void EnsureHooks()
        {
            ZeroDay();
            if (_rotTried || _harmony == null || Campaign.Current == null) return;
            _rotTried = true;
            string where = "BRAK (brak ROT.Models.ROTClanFinanceModel)";
            try
            {
                var t = AccessTools.TypeByName("ROT.Models.ROTClanFinanceModel");
                var m = t != null ? t.GetMethod("CalculateClanGoldChange", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null,
                                                new[] { typeof(Clan), typeof(bool), typeof(bool), typeof(bool) }, null) : null;
                if (m != null && ResolveRot())
                {
                    _harmony.Patch(m, postfix: new HarmonyMethod(typeof(Leaks310), nameof(RotPayPostfix)));   // Normal - przed SoldierPay i ksiega (Last)
                    _rotHooked = true;
                    where = "wpiete";
                }
                else if (m != null) where = "BRAK (pola ROT: EnlistmentBehavior, IsEnlisted, EnlistedHero)";
            }
            catch (Exception e) { where = "BRAK (blad: " + e.Message + ")"; Log.Error("Leaks310.EnsureHooks", e); }
            Log.Info("Drobne z niczego (3.10): zold zaciagu ROT (ROTClanFinanceModel.CalculateClanGoldChange) " + where + ".");
        }

        private static void ZeroDay()
        {
            _fParties = _fWant = _fGrain = _fOther = _fShort = _fStumbles = 0;
            _bPromoted = _bNoMen = _bStumbles = 0;
            _pRemoved = 0; _pRemovedN = 0; _pDue = 0; _pPaid = 0; _pHeroes = 0; _pLord = null; _pStumbles = 0;
        }

        /// <summary>Doba (ArmouryBehavior, zaraz po furazu): zold zaciagu od pana i linia logu.</summary>
        internal static void Daily()
        {
            PayDaily();
            try
            {
                var s = S;
                if (s != null && s.LogEnabled)
                {
                    var sb = new StringBuilder(400);
                    sb.Append("Drobne z niczego (3.10): dzien ").Append(((int)CampaignTime.Now.ToDays).ToString(Inv));
                    sb.Append(" | furaz armii z zapasu wsi").Append(s.ForageFromVillageStock ? "" : " (wylaczony - zboze z niczego)")
                      .Append(": partii ").Append(_fParties).Append(", chcialy ").Append(_fWant).Append(" szt., wziely ").Append(_fGrain + _fOther)
                      .Append(" (zboze ").Append(_fGrain).Append(", inna zywnosc ").Append(_fOther).Append("), zabraklo ").Append(_fShort).Append(" (wies bez zywnosci)");
                    sb.Append(" | herszt kryjowki z wlasnych ludzi").Append(s.HideoutBossFromOwnMen ? "" : " (wylaczony)")
                      .Append(": awansowanych ").Append(_bPromoted).Append(", bez ludzi do awansu ").Append(_bNoMen).Append(" (herszt zdjety)");
                    sb.Append(" | zold zaciagu ROT").Append(s.EnlistedPayFromLord ? "" : " (wylaczony - ROT placi z niczego)");
                    if (_pLord != null || _pRemovedN > 0)
                        sb.Append(": w sluzbie u ").Append(_pLord ?? "?").Append(" - bohaterow ").Append(_pHeroes)
                          .Append(", ROT dopisal z niczego ").Append(_pRemoved).Append(" zl (").Append(_pRemovedN).Append(" rozliczen rodu) - zdjete")
                          .Append("; nalezne ").Append(_pDue).Append(" zl (kazdy swoj zold), pan zaplacil ").Append(_pPaid).Append(" zl ze swojej kiesy, nie zaplacil ").Append(_pDue - _pPaid);
                    else sb.Append(": nie w sluzbie");
                    if (!_rotHooked) sb.Append(" (latka ROT nie wpieta)");
                    if (_fStumbles + _bStumbles + _pStumbles > 0)
                        sb.Append(" | potkniecia: furaz ").Append(_fStumbles).Append(", herszt ").Append(_bStumbles).Append(", zold ").Append(_pStumbles);
                    sb.Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Log.Error("Leaks310.Daily", e); }
            ZeroDay();
        }
    }
}
