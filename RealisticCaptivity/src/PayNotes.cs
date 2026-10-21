using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace RealisticCaptivity
{
    /// <summary>
    /// m96-wies (Jeff 20.10: "You paid w miescie 2 - powinno byc, za co") - KOMUNIKATY PLATNOSCI GRACZA Z POWODEM, ten sam wzor co Armoury PayNotes.
    /// Przelew gry (GiveGoldAction z disableNotification = false) z graczem po jednej stronie pokazuje gola informacje "You paid N" / "You received N"
    /// (SandBox DefaultNotificationsCampaignBehavior). Przy kluczu PlayerPaymentReasons nasze miejsca przelewaja z disableNotification = true (On) i zostaje
    /// JEDEN komunikat z powodem: nasz dotychczasowy (Silent - tylko licznik) albo nowy (Paid / Got - wyglad i dzwiek jak komunikat gry). Kwoty, platnicy
    /// i odbiorcy bez zmian. Wylaczony klucz = komunikat gry jak dotad (MCM tego moda wchodzi po wczytaniu zapisu). Bez stanu w zapisie, bez latek Harmony.
    /// Linia logu przy kazdej podmianie.
    /// </summary>
    internal static class PayNotes
    {
        private const string Head = "Komunikaty platnosci (m96-wies): ";
        private static readonly Dictionary<string, int> _byKind = new Dictionary<string, int>();
        private static int _n, _st;

        /// <summary>Klucz PlayerPaymentReasons; true = przelew gracza bez komunikatu gry (disableNotification), komunikat z powodem nasz.</summary>
        internal static bool On
        {
            get { try { var s = Settings.Current; return s != null && s.PlayerPaymentReasons; } catch { return false; } }
        }

        /// <summary>m96-wies (recenzja): start sesji (OnSessionLaunched) - liczniki od zera, jak StartLine w Armoury; drugi zapis w tym samym
        /// uruchomieniu gry nie dolicza do pierwszego. Stan klucza "teraz" (MCM tego moda wchodzi po wczytaniu zapisu).</summary>
        internal static void Reset()
        {
            try
            {
                _byKind.Clear(); _n = 0; _st = 0;
                Log.Info(Head + "nowa sesja | liczniki od zera | klucz PlayerPaymentReasons teraz: " + (On ? "TAK" : "NIE"));   // nie "start" - linia startu PN-0 jest w Armoury
            }
            catch { }
        }

        /// <summary>Gracz zaplacil: "You paid N{ikona}" + powod. Kwota 0 - nic.</summary>
        internal static void Paid(string kind, int amount, string why) { Show(kind, amount, why, 1); }

        /// <summary>Gracz dostal: "You received N{ikona}" + powod. Kwota 0 - nic.</summary>
        internal static void Got(string kind, int amount, string why) { Show(kind, amount, why, 2); }

        /// <summary>Komunikat gry wylaczony, a powod mowi juz nasz dotychczasowy komunikat - tylko licznik i linia logu.</summary>
        internal static void Silent(string kind, int amount) { Show(kind, amount, null, 0); }

        private static void Show(string kind, int amount, string why, int how)
        {
            if (amount <= 0) return;
            try
            {
                if (how != 0)
                {
                    var t = new TextObject(how == 2 ? "{=!}You received {ARM_PN_GOLD}{GOLD_ICON}" : "{=!}You paid {ARM_PN_GOLD}{GOLD_ICON}");
                    t.SetTextVariable("ARM_PN_GOLD", amount);
                    InformationManager.DisplayMessage(new InformationMessage(t.ToString() + (why ?? "."),
                        how == 2 ? "event:/ui/notification/coins_positive" : "event:/ui/notification/coins_negative"));
                }
                _n++;
                string k = kind ?? "?";
                int c; _byKind.TryGetValue(k, out c); _byKind[k] = c + 1;
                var sb = new StringBuilder();
                foreach (var kv in _byKind) sb.Append(sb.Length > 0 ? ", " : "").Append(kv.Key).Append(' ').Append(kv.Value);
                Log.Info(Head + (how == 2 ? "wplyw " : how == 1 ? "zaplata " : "komunikat gry wylaczony (nasz mowi powod) ") + k + " " + amount
                         + " | podmienione w sesji " + _n + " | wg powodu: " + sb);
            }
            catch (Exception e)
            {
                _st++;
                if (_st <= 3 || _st % 200 == 0) Log.Error("PayNotes.Show (potkniecie " + _st + ")", e);
            }
        }
    }
}
