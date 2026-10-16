using System;
using System.Collections.Generic;

namespace Armoury
{
    /// <summary>
    /// ZBROJOWNIA GRACZA - CZYJE CO JEST (Jeff 15.10, przekazanie pkt 15; DECYZJA JEFFA 15.10 a/b/c - zastepuje pomysl z dwoma kliknieciami).
    /// Log 15.10 [12:11:15] "Wymiana: ekran zbrojowni - widoczne sztuki gracza 91 (...), zapas ludzi 52 (do kupienia)" i [12:11:41] "Sakiewka ludzi: gracz kupil
    /// od ludzi 24 szt. za 86" - na liscie lezaly razem Twoje rzeczy i zapas ludzi z lupu; co wziales z zapasu ludzi, placiles im przy zamknieciu okna.
    ///  (a) ArmouryShowsOnlyYours: okno zbrojowni DTE pokazuje TYLKO czesc gracza (Target z podzialu porzadku - QuartermasterLaw.LastSplit), do wziecia za
    ///      darmo. To, co ludzie nosza, i ich zapas z lupu sa ich: QuartermasterEscrow.HoldReserve chowa na czas okna wszystko ponad czesc gracza (HideOf)
    ///      i oddaje przy zamknieciu (ten sam depozyt _held, co dotad dla noszonego). Bez sakiewki ludzi (MenPurseEnabled) nikt nie sprzedaje ich lupu -
    ///      wtedy widok jak dotad (zapas bylby schowany na zawsze); bez ArmouryProtectUsed nic nie jest chowane (jak dotad).
    ///  (b) Ludzie sami sprzedaja swoj zapas w miescie (MenPurse.SellPlayerSurplus), przy PlayerMenSellSpareHorses takze zbedne konie (dotad konie ludzi
    ///      gracza nigdy nie szly do kupca - przy (a) bylyby martwym, niewidocznym zapasem), i dokupuja lepsze (MenUpgrade.ForPlayer - linia "bez zakupow").
    ///  (c) SwapReturnsTheirOldKit: za Twoj lepszy wklad dostajesz WYPARTA sztuke - stary sprzet zolnierza (SwapMath.Swap backDisplaced), a nie najgorsza
    ///      wolna sztuke ludzi tego typu (regula 30.08 - przy (a) bylby to smiec z ich lupu, ktorego nie widzisz); wklad w puste rece - bez zwrotu (14.09).
    /// Kto co nosi u ludzi gracza (wymiana, nadwyzki, braki, dozbrajanie) - jeden predykat GearFit (umiejetnosc, bron z siodla, kultura):
    /// QuartermasterLaw.MeetsFitOf. Bez zapisu, bez latek, bez stanu; samosprawdzenie przy starcie sesji (linia "Zbrojownia gracza (15.10): start sesji").
    /// </summary>
    internal static class PlayerArmoryView
    {
        internal static bool OnlyYours { get { var s = Settings.Current; return s != null && s.ArmouryShowsOnlyYours && s.ArmouryProtectUsed && MenPurse.On; } }
        internal static bool ReturnOld { get { var s = Settings.Current; return s != null && s.SwapReturnsTheirOldKit; } }
        internal static bool SellHorses { get { var s = Settings.Current; return s != null && s.PlayerMenSellSpareHorses; } }

        /// <summary>Ile sztuk wpisu rostera (egzemplarz z podzialu, klucz id|mod) schowac na czas okna. onlyYours - wszystko ponad czesc gracza (ownLeft:
        /// czesc gracza tego egzemplarza jeszcze do pokazania); inaczej regula sprzed 15.10 - tylko noszone (wornLeft). Jedna regula dla ekranu i samosprawdzenia.</summary>
        internal static int HideOf(bool onlyYours, int amount, string key, Dictionary<string, int> wornLeft, Dictionary<string, int> ownLeft)
        {
            if (amount <= 0 || key == null) return 0;
            int left;
            if (onlyYours)
            {
                ownLeft.TryGetValue(key, out left);
                int vis = Math.Min(amount, Math.Max(0, left));
                ownLeft[key] = left - vis;
                return amount - vis;
            }
            wornLeft.TryGetValue(key, out left);
            int hide = Math.Min(amount, Math.Max(0, left));
            wornLeft[key] = left - hide;
            return hide;
        }

        /// <summary>Samosprawdzenie wymiany (c): 1 czlowiek nosi t2 ludzi, w zapasie ludzi t1, Twoj wklad t4. Zwraca id zwrotu ("nic" - bez zwrotu).</summary>
        private static string SwapProbe(bool backDisplaced, out int ownWorn, out int x, out int xDisp, out bool sane)
        {
            var d = new SwapMath.Piece { Id = "test_t2", Tier = 2, Power = 20, Total = 1 };
            var j = new SwapMath.Piece { Id = "test_t1", Tier = 1, Power = 5, Total = 1 };
            var a = new SwapMath.Piece { Id = "test_t4", Tier = 4, Power = 40, Total = 1, Own = 1 };
            var list = new List<SwapMath.Piece> { d, j, a };
            var r = SwapMath.Swap(new[] { 0 }, 1, (g, p) => true, list, true, backDisplaced);
            ownWorn = a.OwnWorn; x = r.X; xDisp = r.XDisp; sane = SwapMath.TargetsSane(list);
            return d.Back > 0 ? d.Id : j.Back > 0 ? j.Id : "nic";
        }

        /// <summary>Start sesji (po McmSettings.Apply - klucze z MCM): stan kluczy i samosprawdzenie obu regul na liczbach bez typow gry (autotest NS-10..NS-12).</summary>
        internal static void SessionStart()
        {
            var s = Settings.Current;
            if (s == null) return;
            bool only = OnlyYours, old = ReturnOld;
            // (a) egzemplarz 5 szt.: noszone 2, Twoje 1, zapas ludzi 2
            const string k = "test_t3|";
            int vis = 5 - HideOf(only, 5, k, new Dictionary<string, int> { { k, 2 } }, new Dictionary<string, int> { { k, 1 } });
            int visOld = 5 - HideOf(false, 5, k, new Dictionary<string, int> { { k, 2 } }, new Dictionary<string, int> { { k, 1 } });
            int visWant = only ? 1 : 3;
            // (c) wymiana
            int worn, x, xd, worn0, x0, xd0; bool sane, sane0;
            string back = SwapProbe(old, out worn, out x, out xd, out sane);
            string back0 = SwapProbe(false, out worn0, out x0, out xd0, out sane0);
            string backWant = old ? "test_t2" : "test_t1";
            string why = only ? "tak" : (s.ArmouryShowsOnlyYours ? "NIE - wymaga Men Purse Enabled i Armoury Protect Used" : "nie (klucz wylaczony)");
            Log.Info("Zbrojownia gracza (15.10): start sesji | widok tylko Twoje " + (s.ArmouryShowsOnlyYours ? "tak" : "nie") + " (czynny " + why + ")"
                     + " | zwrot starego sprzetu " + (old ? "tak" : "nie") + " | ludzie sprzedaja zbedne konie " + (SellHorses ? "tak" : "nie")
                     + " | kto co nosi wedlug GearFit: umiejetnosc tak, siodlo " + (GearFit.On ? "tak" : "nie") + ", kultura " + (GearFit.CultureOn ? "tak" : "nie")
                     + " | samosprawdzenie widoku: egzemplarz 5 szt. (noszone 2, Twoje 1, zapas ludzi 2) -> widoczne " + vis + " (oczekiwane " + visWant
                     + "; regula sprzed 15.10: " + visOld + ")"
                     + " | samosprawdzenie wymiany: zwrot " + back + " (oczekiwane " + backWant + "; regula sprzed 15.10: " + back0 + "), z wypartych " + xd + " z " + x
                     + ", noszony wklad " + worn + " (oczekiwane 1), czesci gracza w granicach " + (sane && sane0 ? "tak" : "NIE") + ".");
        }
    }
}
