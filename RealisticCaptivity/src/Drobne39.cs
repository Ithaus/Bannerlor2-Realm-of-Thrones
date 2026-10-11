using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace RealisticCaptivity
{
    /// <summary>
    /// KROK 3.9 PLANU (docs/PLAN-DO-KONCA-MODA-2026-10-09.md) - DROBNE W NICOSC, czesc RealisticCaptivity (reszta - Armoury Drobne39.cs).
    /// Kazde zjawisko: platnik -> odbiorca; kazde za wlasnym wylacznikiem (domyslnie wlaczonym, wylaczony = jak dotad).
    ///  (1) LAPOWKA PRZY UCIECZCE (EscapeBribeToCaptor; AUDYT-PELNY-EKONOMIA, Patches.cs:96). Pomocnik z zewnatrz przekupywal straznika za
    ///      EscapeBribeGold (800) - GiveGoldAction(gracz -> nikt): zloto w nicosc. Straznik jest czlowiekiem partii, ktora trzyma gracza, wiec
    ///      lapowka idzie do kiesy tej partii (GiveGoldAction.ApplyForCharacterToParty: partia lorda - kiesa lorda, banda - kiesa bandy). RC nie zna
    ///      sakiewek ludzi Armoury (osobny mod, bez twardej zaleznosci) - kiesa partii to najblizszy odbiorca, jakiego widzi gra. Ucieczka z lochu
    ///      osady lapowki nie wymaga (Patches.cs - loch przepuszcza wczesniej), wiec odbiorca to zawsze partia w polu.
    ///  (2) RYNSZTUNEK GRACZA U BANDYTOW (FenceGearPaid; CaptivityBehavior.FenceGear). Bandyci "sprzedawali" zdjety z gracza rynsztunek: sztuki
    ///      ladowaly na polce najblizszego miasta, a nikt nikomu nie placil (13.09: 11 szt. za 43 196 w The Eyrie - towar za darmo dla kasy miasta).
    ///      Teraz ta sama regula co paser band w Armoury (OutlawLaw.SellStack): najblizsze nieoblezone miasto placi bandzie za kazda sztuke
    ///      cene skupu targu (TownMarketData.GetPrice ze sprzedaza, bez kupca - jak OutlawLaw.FencePrice) x udzial bandy (OutlawFenceLootShare
    ///      przy miescie, malejacy z odlegloscia bandy od miasta do OutlawFenceLootShareFar na granicy OutlawFenceRadius; reszta to dzialka
    ///      pasera i zostaje w kasie), tylko z kasy ponad rezerwe (TownWageLink.Spare - TownRentFloorGold Armoury). Sztuki, za ktore miasto nie
    ///      zaplaci (kasa przy rezerwie, legenda poza handlem, blad wyceny), zostaja w jukach bandy. Gdy partii zdobywcow juz nie ma - rynsztunek
    ///      wraca do gracza (porzucony oboz; ta sama zasada co odbicie sila w OnHeroPrisonerReleased). Bez Armoury: rezerwa 0, udzialy 0.5 / 0.25,
    ///      promien 200 (wartosci domyslne Armoury).
    /// </summary>
    internal static class Drobne39
    {
        internal static bool BribeOn { get { var s = Settings.Current; return s != null && s.EscapeBribeToCaptor; } }
        internal static bool FenceOn { get { var s = Settings.Current; return s != null && s.FenceGearPaid; } }

        private static int _stumbles;
        private static readonly HashSet<string> _errWhere = new HashSet<string>();

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errWhere.Add(where)) Log.Error("Drobne39." + where, e); } catch { }
        }

        // ------------------------------------------------------------ (1) lapowka

        /// <summary>Patches.cs (ucieczka z pomoca z zewnatrz): placi lapowke. Zwraca, ile pobrano (0 - nic: brak partii zdobywcy).</summary>
        internal static int PayBribe(int bribe)
        {
            if (!BribeOn) { GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, bribe); return bribe; }
            PartyBase captor = null;
            try { captor = PlayerCaptivity.CaptorParty; } catch { captor = null; }
            if (captor == null)
            {
                Log.Info("Lapowka (3.9): nie ma partii zdobywcy - nikt nie bierze lapowki, nic nie pobrano.");
                return 0;
            }
            GiveGoldAction.ApplyForCharacterToParty(Hero.MainHero, captor, bribe);
            Log.Info("Lapowka (3.9): " + bribe + " zl do kiesy partii zdobywcy " + captor.Name
                     + (captor.LeaderHero != null ? " (kiesa " + captor.LeaderHero.Name + ")" : (captor.IsMobile ? " (kiesa bandy)" : " (kasa osady)")) + " - przelew, nie w nicosc.");
            return bribe;
        }

        // ------------------------------------------------------------ (2) rynsztunek u bandytow

        /// <summary>CaptivityBehavior.FenceGear: sprzedaz rynsztunku `stored` ("slot|item|mod") przez bande `captor` paserowi najblizszego miasta.
        /// false - partii zdobywcow juz nie ma (rynsztunek ma wrocic do gracza; nic nie ruszone); true - kazda sztuka jest na polce miasta
        /// (zaplacona bandzie) albo w jukach bandy.</summary>
        internal static bool FenceGear(List<string> stored, PartyBase captor)
        {
            var band = captor != null && captor.IsMobile ? captor.MobileParty : null;
            if (band == null || !band.IsActive || band.ItemRoster == null)
            {
                Log.Info("Rynsztunek (3.9): partii zdobywcow juz nie ma (" + (captor != null ? captor.Name.ToString() : "brak partii") + ") - gracz zabiera swoj sprzet z porzuconego obozu.");
                return false;
            }
            Settlement best = null;
            float bestDist = float.MaxValue;
            var myPos = MobileParty.MainParty.GetPosition2D;
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsTown || st.Town == null || st.ItemRoster == null || st.IsUnderSiege) continue;   // do oblezonego miasta paser nie wjedzie (jak u band)
                float d = st.GetPosition2D.Distance(myPos);
                if (d < bestDist) { bestDist = d; best = st; }
            }
            float share = 0f;
            try { if (best != null) share = Share(band.GetPosition2D.Distance(best.GetPosition2D)); } catch (Exception e) { Stumble("Share", e); }
            double due = 0.0;
            int sold = 0, kept = 0, skipped = 0, pay = 0;
            long worth = 0;
            try
            {
                foreach (var entry in stored)
                {
                    var parts = entry.Split('|');
                    if (parts.Length < 2) { skipped++; continue; }
                    var item = MBObjectManager.Instance.GetObject<ItemObject>(parts[1]);
                    if (item == null) { skipped++; continue; }
                    ItemModifier mod = null;
                    if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2]))
                        mod = MBObjectManager.Instance.GetObject<ItemModifier>(parts[2]);
                    var el = new EquipmentElement(item, mod);
                    bool placed = false;
                    try
                    {
                        if (best != null && best.Town.MarketData != null && !item.NotMerchandise && share > 0f)
                        {
                            int price = Math.Max(1, best.Town.MarketData.GetPrice(el, null, true, best.Party));
                            double net = price * (double)share;
                            if (TownWageLink.Spare(best) >= (int)Math.Ceiling(due + net))   // tylko z kasy ponad rezerwe (zaplata dopiero w finally - kasa stoi)
                            {
                                best.ItemRoster.AddToCounts(el, 1);
                                placed = true;
                                due += net; worth += price; sold++;
                            }
                        }
                    }
                    catch (Exception e) { Stumble("FenceGear.sale", e); }
                    if (!placed)
                    {
                        try { band.ItemRoster.AddToCounts(el, 1); kept++; }
                        catch (Exception e) { Stumble("FenceGear.keep", e); skipped++; }
                    }
                }
            }
            finally
            {
                pay = (int)due;                                              // ulamek monety zostaje u pasera (jak OutlawLaw.SellStack)
                if (pay > 0 && best != null)
                {
                    try { GiveGoldAction.ApplyForSettlementToParty(best, captor, pay, true); }
                    catch (Exception e) { Stumble("FenceGear.pay", e); }
                }
                Log.Info("Rynsztunek (3.9): banda " + captor.Name + " sprzedala paserowi w " + (best != null ? best.Name.ToString() : "-") + " " + sold
                         + " szt. (cena skupu targu " + worth + ", udzial bandy " + share.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                         + ") - kasa miasta zaplacila bandzie " + pay + " zl; w jukach bandy zostalo " + kept + " szt. (miasto nie zaplacilo)"
                         + (skipped > 0 ? "; nieznanych sztuk " + skipped : "") + (_stumbles > 0 ? "; potkniecia 3.9: " + _stumbles : "") + ".");
            }
            if (sold > 0 && best != null)
                Log.Player("Your war gear has surfaced on the market in " + best.Name + ". Buy it back there - if no one beats you to it.", true);
            if (kept > 0)
                Log.Player(sold > 0 ? "The brigands kept " + kept + " pieces of your war gear - the fence would not pay for them."
                                    : "The brigands kept your war gear - no fence would pay for it.", true);
            return true;
        }

        // ------------------------------------------------------------ udzial bandy u pasera (ustawienia Armoury przez refleksje)

        private static FieldInfo _cur, _near, _far, _radius;
        private static bool _tried;

        /// <summary>Udzial bandy w cenie skupu: OutlawFenceLootShare przy miescie, liniowo do OutlawFenceLootShareFar na granicy OutlawFenceRadius
        /// (ten sam wzor co OutlawLaw.Slide). Bez Armoury - wartosci domyslne Armoury (0.5 / 0.25 / 200).</summary>
        private static float Share(float dist)
        {
            float near = Read(ref _near, "OutlawFenceLootShare", 0.5f);
            float far = Read(ref _far, "OutlawFenceLootShareFar", 0.25f);
            float radius = Read(ref _radius, "OutlawFenceRadius", 200f);
            near = MBMath.ClampFloat(near, 0f, 1f);
            far = MBMath.ClampFloat(far, 0f, near);                         // daleko nigdy lepiej niz przy miescie (jak OutlawLaw.FenceSell)
            return near + (far - near) * MBMath.ClampFloat(dist / Math.Max(1f, radius), 0f, 1f);
        }

        private static float Read(ref FieldInfo f, string name, float dflt)
        {
            try
            {
                if (!_tried)
                {
                    _tried = true;
                    foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (a.GetName().Name != "Armoury") continue;
                        var st = a.GetType("Armoury.Settings", false);
                        if (st == null) break;
                        _cur = st.GetField("Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        _near = st.GetField("OutlawFenceLootShare");
                        _far = st.GetField("OutlawFenceLootShareFar");
                        _radius = st.GetField("OutlawFenceRadius");
                        break;
                    }
                }
                var cur = _cur != null ? _cur.GetValue(null) : null;
                return cur != null && f != null ? Convert.ToSingle(f.GetValue(cur)) : dflt;
            }
            catch { return dflt; }
        }
    }
}
