using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m96-wies (Jeff 20.10: "You paid w miescie 2 - powinno byc, za co: za nocleg w karczmie") - KOMUNIKATY PLATNOSCI GRACZA Z POWODEM.
    /// Przelew gry (GiveGoldAction z disableNotification = false) z graczem po jednej stronie pokazuje gola informacje "You paid N" / "You received N"
    /// (SandBox DefaultNotificationsCampaignBehavior.OnHeroOrPartyTradedGold: str_gold_removed_with_icon / str_you_received_gold_with_icon, dzwiek monet).
    /// Przy kluczu PlayerPaymentReasons nasze miejsca, w ktorych placi albo dostaje gracz, wolaja przelew z disableNotification = true (Quiet) i pokazuja JEDEN
    /// wlasny komunikat z powodem i rozbiciem (Paid / Got) - ten sam wyglad co komunikat gry (bialy, ikona monety, ten sam dzwiek). Kwoty, platnicy i odbiorcy
    /// bez zmian - zmienia sie tylko napis. AI bez komunikatow (Quiet zmienia cos tylko tam, gdzie gra i tak pokazalaby komunikat graczowi).
    /// Wylaczony klucz = komunikat gry jak dotad (od razu, bez wczytania). Bez stanu w zapisie, bez latek Harmony. Liczniki tylko do logu: linia startu sesji
    /// i linia przy kazdej podmianie ("Komunikaty platnosci (m96-wies): ...").
    /// Zwiad (tylko odczyt): nasluch zdarzenia gry HeroOrPartyTradedGold - gdy gra MIMO TO pokazuje graczowi gola informacje (showNotification, gracz po jednej
    /// stronie), linia logu z miejscem wywolania (stos bez ramek gry), zeby znalezc reszte zrodel "You paid N" (Jeff 20.10: w logach dnia nie bylo noclegu ani
    /// oplaty E8/D2 - zrodla "You paid 2" nie dalo sie potwierdzic). Koszt: tylko przy komunikacie gry graczowi (pierwszy warunek - flaga zdarzenia).
    /// </summary>
    internal static class PayNotes
    {
        private const string Head = "Komunikaty platnosci (m96-wies): ";
        private static readonly Dictionary<string, int> _byKind = new Dictionary<string, int>();
        private static int _n, _paidN, _gotN, _st;
        private static long _paidGold, _gotGold;
        // zwiad: komunikaty gry graczowi, ktorych nie podmienilismy (wg miejsca wywolania)
        private static readonly Dictionary<string, int> _gameBy = new Dictionary<string, int>();
        private static int _gameN, _gameSt;
        private static readonly object Owner = new object();
        private static WeakReference<Campaign> _hookFor;

        /// <summary>Klucz PlayerPaymentReasons (czytany przy kazdym przelewie - zmiana w MCM dziala od razu).</summary>
        internal static bool On
        {
            get { try { var s = Settings.Current; return s != null && s.PlayerPaymentReasons; } catch { return false; } }
        }

        /// <summary>disableNotification przelewu, w ktorym placi albo dostaje gracz: przy wlaczonym kluczu true (zamiast komunikatu gry nasz, z powodem),
        /// inaczej jak dotad (asBefore).</summary>
        internal static bool Quiet(bool asBefore) { return asBefore || On; }

        /// <summary>Gracz zaplacil: "You paid N{ikona} " + powod (np. "for your night at a common room in Seagard (bed 1, horse 1)."). Kwota 0 - nic.</summary>
        internal static void Paid(string kind, int amount, string why) { Show(kind, amount, why, false); }

        /// <summary>Gracz dostal: "You received N{ikona}" + powod (np. ": ransom of horse and armour (Ser X)."). Kwota 0 - nic.</summary>
        internal static void Got(string kind, int amount, string why) { Show(kind, amount, why, true); }

        private static void Show(string kind, int amount, string why, bool got)
        {
            if (amount <= 0) return;
            try
            {
                var t = new TextObject(got ? "{=!}You received {ARM_PN_GOLD}{GOLD_ICON}" : "{=!}You paid {ARM_PN_GOLD}{GOLD_ICON}");
                t.SetTextVariable("ARM_PN_GOLD", amount);
                string text = t.ToString() + (why ?? ".");    // powod doklejony jako zwykly napis (bez zmiennych {..} w srodku)
                InformationManager.DisplayMessage(new InformationMessage(text, got ? "event:/ui/notification/coins_positive" : "event:/ui/notification/coins_negative"));
                _n++;
                if (got) { _gotN++; _gotGold += amount; } else { _paidN++; _paidGold += amount; }
                int k; _byKind.TryGetValue(kind ?? "?", out k); _byKind[kind ?? "?"] = k + 1;
                Log.Info(Head + (got ? "wplyw " : "zaplata ") + (kind ?? "?") + " " + amount + " | podmienione w sesji " + _n + " (zaplaty " + _paidN + " za " + _paidGold
                         + ", wplywy " + _gotN + " za " + _gotGold + ") | wg powodu: " + Kinds());
            }
            catch (Exception e)
            {
                _st++;
                if (_st <= 3 || _st % 200 == 0) Log.Error("PayNotes.Show (potkniecie " + _st + ")", e);
            }
        }

        private static string Kinds()
        {
            var sb = new StringBuilder();
            foreach (var kv in _byKind) sb.Append(sb.Length > 0 ? ", " : "").Append(kv.Key).Append(' ').Append(kv.Value);
            return sb.Length > 0 ? sb.ToString() : "-";
        }

        /// <summary>SubModuleMain.OnAfterGameInitializationFinished (kazde wczytanie i nowa kampania): liczniki od zera, nasluch zwiadu (raz na kampanie),
        /// linia startu ze stanem klucza.</summary>
        internal static void StartLine()
        {
            _byKind.Clear(); _n = _paidN = _gotN = _st = 0; _paidGold = _gotGold = 0;
            _gameBy.Clear(); _gameN = _gameSt = 0;
            string hook = "nasluch zwiadu: ";
            try
            {
                var c = Campaign.Current;
                Campaign done;
                if (c == null) hook += "BRAK kampanii";
                else if (_hookFor != null && _hookFor.TryGetTarget(out done) && ReferenceEquals(done, c)) hook += "juz wpiety w tej kampanii";
                else
                {
                    CampaignEvents.HeroOrPartyTradedGold.AddNonSerializedListener(Owner, OnTraded);   // zdarzenia nowej kampanii - nowy obiekt CampaignEvents
                    _hookFor = new WeakReference<Campaign>(c);
                    hook += "wpiety";
                }
            }
            catch (Exception e) { hook += "BLAD (" + e.Message + ")"; }
            Log.Info(Head + "start | " + (On ? "WLACZONY (Player Payment Reasons) - przy przelewach gracza z Armoury komunikat gry wylaczony, wlasny z powodem" : "WYLACZONY w MCM (Player Payment Reasons) - komunikaty gry jak dotad")
                     + " | miejsca: nocleg w karczmie (inn), w zajezdzie (roadinn), kuznia na dobe (forgeday), ksiazki (book, bkbook), okup gracza (ransom), zlecenia lordow (order),"
                     + " sprzedaz warsztatu (workshop), okup wsi (tribute), wplata karawany (caravan), sakiewka wiesniakow (villagers), oplaty E8/D2 (bribe, prisonbreak, barber,"
                     + " claninfo, round, drink) | " + hook + " (komunikat gry graczowi bez naszego powodu - linia 'komunikat gry bez powodu' z miejscem wywolania).");
        }

        /// <summary>Zwiad: zdarzenie gry po kazdym GiveGoldAction. Tylko gdy gra pokazuje graczowi komunikat (showNotification) - linia logu z miejscem wywolania
        /// (pierwsze 3 z kazdego miejsca, potem co 50.). Nic nie zmienia.</summary>
        private static void OnTraded((Hero, PartyBase) giver, (Hero, PartyBase) recipient, (int, string) amount, bool showNotification)
        {
            if (!showNotification) return;
            try
            {
                var me = Hero.MainHero;
                var mine = PartyBase.MainParty;
                bool fromMe = (me != null && giver.Item1 == me) || (mine != null && giver.Item2 == mine);
                bool toMe = (me != null && recipient.Item1 == me) || (mine != null && recipient.Item2 == mine);
                int n = amount.Item1;
                if ((!fromMe && !toMe) || n == 0) return;
                bool paid = (fromMe && n > 0) || (toMe && n < 0);
                string where = Caller();
                _gameN++;
                int k; _gameBy.TryGetValue(where, out k); k++; _gameBy[where] = k;
                if (k <= 3 || k % 50 == 0)
                    Log.Info(Head + "komunikat gry bez powodu (" + (paid ? "You paid " : "You received ") + Math.Abs(n) + ") | " + Side(giver) + " -> " + Side(recipient)
                             + (string.IsNullOrEmpty(amount.Item2) ? "" : " | id " + amount.Item2) + " | wolajacy: " + where
                             + " | takich w sesji " + _gameN + " (z tego miejsca " + k + ")");
            }
            catch (Exception e)
            {
                _gameSt++;
                if (_gameSt <= 3 || _gameSt % 200 == 0) Log.Error("PayNotes.OnTraded (potkniecie " + _gameSt + ")", e);
            }
        }

        private static string Side((Hero, PartyBase) s)
        {
            try
            {
                if (s.Item1 != null) return s.Item1 == Hero.MainHero ? "gracz" : "bohater " + s.Item1.StringId;
                if (s.Item2 != null) return s.Item2 == PartyBase.MainParty ? "partia gracza" : (s.Item2.IsSettlement ? "osada " : "partia ") + (s.Item2.Id ?? "?");
            }
            catch { }
            return "nikt";
        }

        /// <summary>Miejsce wywolania: do 5 ramek stosu poza akcja i zdarzeniami gry (GiveGoldAction, CampaignEvents, MbEvent) i poza ta klasa.</summary>
        private static string Caller()
        {
            try
            {
                var frames = new System.Diagnostics.StackTrace(2, false).GetFrames();
                if (frames == null) return "?";
                var parts = new List<string>();
                foreach (var f in frames)
                {
                    var m = f != null ? f.GetMethod() : null;
                    if (m == null) continue;
                    string t = m.DeclaringType != null ? m.DeclaringType.FullName ?? m.DeclaringType.Name : "?";
                    if (t.StartsWith("TaleWorlds.CampaignSystem.Actions.GiveGoldAction", StringComparison.Ordinal) || t.StartsWith("TaleWorlds.CampaignSystem.CampaignEvent", StringComparison.Ordinal)
                        || t.StartsWith("TaleWorlds.CampaignSystem.MbEvent", StringComparison.Ordinal) || t.StartsWith("Armoury.PayNotes", StringComparison.Ordinal)) continue;
                    parts.Add(t + "." + m.Name);
                    if (parts.Count >= 5) break;
                }
                return parts.Count > 0 ? string.Join(" <- ", parts.ToArray()) : "?";
            }
            catch { return "?"; }
        }
    }
}
