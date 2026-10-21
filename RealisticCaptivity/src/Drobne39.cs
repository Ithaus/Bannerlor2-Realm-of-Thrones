using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace RealisticCaptivity
{
    /// <summary>
    /// KROK 3.9 PLANU (docs/PLAN-DO-KONCA-MODA-2026-10-09.md) - DROBNE W NICOSC, czesc RealisticCaptivity (reszta - Armoury Drobne39.cs).
    /// Kazde zjawisko: platnik -> odbiorca; kazde za wlasnym wylacznikiem (domyslnie wlaczonym, wylaczony = jak dotad).
    ///  (1) LAPOWKA PRZY UCIECZCE (EscapeBribeToCaptor; AUDYT-PELNY-EKONOMIA, Patches.cs). Pomocnik z zewnatrz przekupywal straznika za
    ///      EscapeBribeGold (800) - GiveGoldAction(gracz -> nikt): zloto w nicosc. Straznik jest czlowiekiem partii, ktora trzyma gracza, wiec
    ///      lapowka idzie do kiesy tej partii: partia lorda - kiesa lorda (GiveGoldAction.ApplyBetweenCharacters: zdarzenie niesie bohatera, wiec
    ///      ksiega rodu Armoury liczy to jako wplyw spoza rodu - recenzja 3.9; z partia jako odbiorca ClanIncomeBook.OnEvent konczyl sie na rh == null),
    ///      inna partia (banda) - kiesa partii (ApplyForCharacterToParty). RC nie zna sakiewek ludzi Armoury (osobny mod, bez twardej zaleznosci) -
    ///      kiesa partii to najblizszy odbiorca, jakiego widzi gra. Ucieczka z lochu osady lapowki nie wymaga (Patches.cs - loch przepuszcza
    ///      wczesniej), wiec odbiorca to zawsze partia w polu. Gracz na parolu placi dopiero, gdy naprawde ucieka ("I run") - recenzja 3.9:
    ///      dotad placil przed pytaniem o parol, takze gdy dotrzymal slowa (Patches.cs).
    ///  (2) RYNSZTUNEK GRACZA U BANDYTOW (GearToCaptorPacks; CaptivityBehavior.FenceGear). Bandyci "sprzedawali" zdjety z gracza rynsztunek:
    ///      sztuki ladowaly na polce najblizszego miasta, a nikt nikomu nie placil (13.09: 11 szt. za 43 196 w The Eyrie - towar za darmo dla
    ///      kasy miasta). Teraz JEDNA REGULA z kazdym lupem band (recenzja 3.9 - pierwsza wersja kazala miastu kupic kazda sztuke, czego paser band
    ///      nigdy nie robi): rynsztunek to lup partii, ktora trzymala gracza, i trafia do jej jukow (ItemRoster). Dalej robi z nim to samo, co z kazdym
    ///      lupem bandy, prawo band Armoury (OutlawLaw): paser bierze tylko ladunek i zwierzeta ponad potrzebe (konie ponad liczbe pieszych - z kontrola
    ///      egzotykow MountLaw, juczne ponad nosnosc), zbroja na korpus czeka na awanse (Equip), bron, helmy i tarcze zostaja w jukach. Gracz odzyskuje
    ///      swoje, rozbijajac te partie (gra rozdaje juki pokonanej partii zwyciezcom - MapEvent.LootDefeatedPartyItems). Gdy partii zdobywcow juz
    ///      nie ma - rynsztunek wraca do gracza (porzucony oboz; ta sama zasada co odbicie sila w OnHeroPrisonerReleased); sztuka, ktorej nie udalo
    ///      sie wlozyc do jukow, tez wraca do gracza (nic nie ginie). Bez Armoury rynsztunek po prostu lezy w jukach partii.
    /// </summary>
    internal static class Drobne39
    {
        internal static bool BribeOn { get { var s = Settings.Current; return s != null && s.EscapeBribeToCaptor; } }
        internal static bool PacksOn { get { var s = Settings.Current; return s != null && s.GearToCaptorPacks; } }

        private static int _stumbles;
        private static readonly HashSet<string> _errWhere = new HashSet<string>();

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errWhere.Add(where)) Log.Error("Drobne39." + where, e); } catch { }
        }

        // ------------------------------------------------------------ (1) lapowka

        /// <summary>Patches.cs (ucieczka z pomoca z zewnatrz - w chwili, gdy gracz naprawde ucieka): placi lapowke. Zwraca, ile pobrano (0 - nic: brak
        /// partii zdobywcy).</summary>
        internal static int PayBribe(int bribe)
        {
            bool pn = PayNotes.On;   // m96-wies: komunikat gry wylaczony - powod mowi "X bribed a guard for you" (Patches.Bribe)
            if (!BribeOn) { GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, bribe, pn); if (pn) PayNotes.Silent("bribe", bribe); return bribe; }
            PartyBase captor = null;
            try { captor = PlayerCaptivity.CaptorParty; } catch { captor = null; }
            if (captor == null)
            {
                Log.Info("Lapowka (3.9): nie ma partii zdobywcy - nikt nie bierze lapowki, nic nie pobrano.");
                return 0;
            }
            // partia lorda: kiesa partii = zloto wodza (MobileParty.PartyTradeGold), ale tylko przelew do bohatera niesie go w zdarzeniu gry
            Hero lord = captor.IsMobile && captor.MobileParty != null && captor.MobileParty.IsLordParty ? captor.LeaderHero : null;
            if (lord != null) GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, lord, bribe, pn);
            else GiveGoldAction.ApplyForCharacterToParty(Hero.MainHero, captor, bribe, pn);
            if (pn) PayNotes.Silent("bribe", bribe);
            Log.Info("Lapowka (3.9): " + bribe + " zl do kiesy partii zdobywcy " + captor.Name
                     + (lord != null ? " (kiesa " + lord.Name + ")" : (captor.IsMobile ? " (kiesa partii)" : " (kasa osady)")) + " - przelew, nie w nicosc.");
            return bribe;
        }

        // ------------------------------------------------------------ (2) rynsztunek u bandytow

        /// <summary>CaptivityBehavior.FenceGear: rynsztunek `stored` ("slot|item|mod") do jukow partii `captor`, ktora trzymala gracza.
        /// false - partii zdobywcow juz nie ma (rynsztunek ma wrocic do gracza; nic nie ruszone); true - sztuki sa w jukach partii, a wpisy,
        /// ktorych nie udalo sie wlozyc, sa w `back` (wracaja do gracza).</summary>
        internal static bool GearToPacks(List<string> stored, PartyBase captor, List<string> back)
        {
            var band = captor != null && captor.IsMobile ? captor.MobileParty : null;
            if (band == null || !band.IsActive || band.ItemRoster == null)
            {
                Log.Info("Rynsztunek (3.9): partii zdobywcow juz nie ma (" + (captor != null ? captor.Name.ToString() : "brak partii") + ") - gracz zabiera swoj sprzet z porzuconego obozu.");
                return false;
            }
            int put = 0, horses = 0, armour = 0, unknown = 0;
            long worth = 0;
            foreach (var entry in stored)
            {
                var parts = entry.Split('|');
                ItemObject item = null;
                ItemModifier mod = null;
                try
                {
                    if (parts.Length >= 2) item = MBObjectManager.Instance.GetObject<ItemObject>(parts[1]);
                    if (item != null && parts.Length > 2 && !string.IsNullOrEmpty(parts[2])) mod = MBObjectManager.Instance.GetObject<ItemModifier>(parts[2]);
                }
                catch (Exception e) { Stumble("GearToPacks.find", e); item = null; }
                if (item == null) { unknown++; continue; }                 // sztuki, ktorej gra nie zna, nie przywroci tez RestoreGear
                try
                {
                    var el = new EquipmentElement(item, mod);
                    band.ItemRoster.AddToCounts(el, 1);
                    put++; worth += el.ItemValue;
                    if (item.ItemType == ItemObject.ItemTypeEnum.Horse) horses++;
                    else if (item.ItemType == ItemObject.ItemTypeEnum.BodyArmor) armour++;
                }
                catch (Exception e) { Stumble("GearToPacks.put", e); back.Add(entry); }   // nie weszla do jukow - wraca do gracza
            }
            Log.Info("Rynsztunek (3.9): " + put + " szt. (wartosc " + worth + " zl; konie " + horses + ", zbroje na korpus " + armour + ") do jukow partii zdobywcow "
                     + captor.Name + (band.IsBandit ? " (banda)" : "") + " - lup jak kazdy inny, dalej prawo band Armoury (paser: konie ponad potrzebe; zbroja na awanse;"
                     + " reszta zostaje w jukach)" + (back.Count > 0 ? "; wraca do gracza " + back.Count + " szt. (nie weszly do jukow)" : "")
                     + (unknown > 0 ? "; nieznanych sztuk " + unknown : "") + (_stumbles > 0 ? "; potkniecia 3.9: " + _stumbles : "") + ".");
            if (put > 0)
                Log.Player("Your captors keep your war gear in their packs. Run them down if you want it back.", true);
            return true;
        }
    }
}
