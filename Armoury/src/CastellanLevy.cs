using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 474-P5b (Jeff 13.11, decyzja 3 "Tak" na pytanie "ROT dzis rekrutuje levy rodow u kasztelana z niczego: zloto znika, ludzi nigdzie nie ubywa. Czy to latac?").
    /// DOWOD (dekompilacja ROT, ROT.CampaignBehaviors.ROTHouseTroopsBehavior.OnSessionLaunched): linia gracza "house_troops_castellan_buy" ("[Recruit Household Troops]")
    /// otwiera dwa okna (oddzial, potem 1/5/10/25/50/100), a wybor robi GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, -koszt x n) - zloto gracza
    /// znika (odbiorca null) - i MobileParty.MainParty.AddElementToMemberRoster(oddzial, n) - ludzie z niczego (zadna osada nie traci ludzi). Koszt: wlasciwosc
    /// RecruitmentCost (200, King's Landing 500). Warunek klikniecia (relacja z glowa rodu, siedziba gracza, oblezenie, sluzba) - bez zmian.
    /// ZMIANA (klucz CastellanLevyFromPool): skutek tej linii (ConversationSentence.OnConsequence - pole publiczne; podmiana raz na kampanie po
    /// OnAfterSessionLaunched, bez Harmony na lambdach ROT) - ludzie rodu ida z DOM wsi siedziby (Settlement.BoundVillages, PeopleBook.TakeLevyHere - ta sama
    /// droga i podloga 50 hearth co pobor z wioski pobocznej; pule wiosek pobocznych tych wsi traca swoj udzial - LevyNoteDistrictDraw), ile wsie moga dac;
    /// placisz tylko za przybylych, ta sama cena co ROT, a zloto idzie do kies tych wsi (rodziny) w proporcji do ich ludzi - GiveGoldAction gracz -> osada.
    /// Pochodzenie ludzi - PeopleBook.GiveNew (pobor gracza). Siedziba spoza tabeli rodow (Red Keep - Gwardia Krolewska) albo ksiega pochodzenia nieczynna
    /// (PeopleBook.LevyLive) - skutek ROT jak dotad. Klucz NIE - skutek ROT co do bitu (wolany oryginalny delegat).
    /// </summary>
    internal static class CastellanLevy
    {
        private const string P = "Kasztelan (474-P5b): ";
        private const string LineId = "house_troops_castellan_buy";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static ConversationSentence.OnConsequenceDelegate _orig;
        private static ConversationSentence.OnConsequenceDelegate _mine;
        private static int _stumbles, _rec, _men, _orgCalls, _poolNotes;
        private static long _gold;
        private static readonly HashSet<string> _err = new HashSet<string>(StringComparer.Ordinal);

        internal static bool Key { get { var s = Settings.Current; return s != null && s.CastellanLevyFromPool; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("CastellanLevy." + where, e); } catch { }
        }

        /// <summary>OnAfterSessionLaunched: podmiana skutku linii ROT (po OnSessionLaunched wszystkich zachowan). Nie rzuca.</summary>
        internal static void Hook(CampaignGameStarter starter)
        {
            try
            {
                _rec = _men = _orgCalls = _poolNotes = 0; _gold = 0;
                var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
                var f = AccessTools.Field(typeof(ConversationManager), "_sentences");
                var list = cm != null && f != null ? f.GetValue(cm) as IList : null;
                if (list == null) { Log.Info(P + "ConversationManager._sentences nieczytelne - skutek ROT bez zmian."); return; }
                int found = 0, hooked = 0;
                if (_mine == null) _mine = Consequence;
                foreach (var o in list)
                {
                    var s = o as ConversationSentence;
                    if (s == null || s.Id != LineId) continue;
                    found++;
                    if (s.OnConsequence == null) continue;
                    if (ReferenceEquals(s.OnConsequence, _mine)) { hooked++; continue; }   // ta sama kampania - juz podmienione
                    _orig = s.OnConsequence;
                    s.OnConsequence = _mine;
                    hooked++;
                }
                Log.Info(P + (hooked > 0 ? "skutek linii ROT \"" + LineId + "\" przejety (" + hooked + " z " + found + ")" : "linia ROT \"" + LineId + "\" nie znaleziona (" + found + ") - skutek ROT bez zmian")
                         + " | klucz Castellan Levy From Pool " + (Key ? "TAK" : "NIE") + " - ludzie rodu z DOM wsi siedziby, zloto do kies tych wsi; tabela rodow " + RotHouses.Count + " siedzib.");
            }
            catch (Exception e) { Stumble("Hook", e); }
        }

        private static void CallOrig(string why)
        {
            _orgCalls++;
            if (why != null) Log.Info(P + "skutek ROT jak dotad (" + why + ").");
            _orig?.Invoke();
        }

        /// <summary>Skutek linii "[Recruit Household Troops]".</summary>
        private static void Consequence()
        {
            Settlement seat = null;
            try
            {
                seat = Settlement.CurrentSettlement;
                if (!Key) { CallOrig(null); return; }
                var h = RotHouses.OfSeat(seat);
                if (h == null || h.Troop == null) { CallOrig("siedziba " + (seat != null ? seat.StringId : "-") + " spoza tabeli rodow"); return; }
                if (!PeopleBook.LevyLive) { CallOrig("ksiega pochodzenia nieczynna"); return; }
                Open(seat, h);
            }
            catch (Exception e) { Stumble("Consequence", e); }
        }

        private static int Cost(Settlement seat)
        {
            try
            {
                var behT = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTHouseTroopsBehavior");
                var get = typeof(Campaign).GetMethod("GetCampaignBehavior", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                object beh = behT != null && get != null ? get.MakeGenericMethod(behT).Invoke(Campaign.Current, null) : null;
                var pr = behT != null ? AccessTools.Property(behT, "RecruitmentCost") : null;
                if (beh != null && pr != null) return Math.Max(1, (int)pr.GetValue(beh, null));
            }
            catch (Exception e) { Stumble("Cost", e); }
            return 200;   // ROT: 200 (King's Landing 500)
        }

        /// <summary>Wsie siedziby z miejscem ponad podloga 50 hearth (PeopleBook.LevyRoomHere), od najwiekszego zapasu.</summary>
        private static List<KeyValuePair<Settlement, int>> Rooms(Settlement seat, out int all)
        {
            all = 0;
            var l = new List<KeyValuePair<Settlement, int>>();
            if (seat == null || seat.BoundVillages == null) return l;
            foreach (var v in seat.BoundVillages)
            {
                if (v == null || v.Settlement == null) continue;
                int r = PeopleBook.LevyRoomHere(v.Settlement);
                if (r > 0) { l.Add(new KeyValuePair<Settlement, int>(v.Settlement, r)); all += r; }
            }
            l.Sort((a, b) => b.Value.CompareTo(a.Value));
            return l;
        }

        private static void Open(Settlement seat, RotHouses.House h)
        {
            int cost = Cost(seat), room;
            Rooms(seat, out room);
            var troop = h.Troop;
            int gold = Hero.MainHero != null ? Hero.MainHero.Gold : 0;
            int canPay = gold / Math.Max(1, cost);
            ImageIdentifier img = null;
            try { img = new CharacterImageIdentifier(CharacterCode.CreateFrom(troop)); } catch { img = null; }
            var els = new List<InquiryElement>();
            foreach (int i in new[] { 1, 5, 10, 25, 50, 100 })
            {
                bool ok = i <= room && i <= canPay;
                string hint = "{GOLD_ICON}" + (cost * i);
                if (i > room) hint = "The villages of " + seat.Name + " cannot spare so many men.";
                else if (i > canPay) hint = "You cannot pay for so many.";
                els.Add(new InquiryElement(i, i.ToString(Inv), img, ok, new TextObject("{=!}" + hint).ToString()));
            }
            string title = new TextObject("{=ROT4861O2g7}Which troops would you like to recruit?").ToString();
            string desc = Txt(troop.Name) + " of " + h.Name + ", " + cost + " denars each. They are sons of the villages of " + Txt(seat.Name) + " - about "
                          + room.ToString("N0", Inv) + " more could still be spared from their homesteads. The money goes to their families, and every man you take leaves those fields.";
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(title, desc, els, true, 1, 1, GameTexts.FindText("str_done").ToString(),
                GameTexts.FindText("str_cancel").ToString(), delegate (List<InquiryElement> sel)
                {
                    try
                    {
                        if (sel == null || sel.Count == 0 || !(sel[0].Identifier is int)) return;
                        Recruit(seat, h, troop, (int)sel[0].Identifier, cost);
                    }
                    catch (Exception e) { Stumble("Pick", e); }
                }, null));
        }

        /// <summary>Pobor n ludzi rodu z wsi siedziby: DOM kazdej wsi (TakeLevyHere) -> ludzie -> pochodzenie -> zaplata kazdej wsi za jej ludzi.</summary>
        private static void Recruit(Settlement seat, RotHouses.House h, CharacterObject troop, int n, int cost)
        {
            var hero = Hero.MainHero; var mp = MobileParty.MainParty;
            if (hero == null || mp == null || seat == null || troop == null || n < 1) return;
            n = Math.Min(n, hero.Gold / Math.Max(1, cost));
            int room; var rooms = Rooms(seat, out room);
            n = Math.Min(n, room);
            if (n < 1) { Log.Player("The villages of " + Txt(seat.Name) + " have no men left to spare.", true); return; }
            int gold0 = hero.Gold, left = n, keepAll = 0; double gotAll = 0.0;
            var paid = new List<string>();
            foreach (var kv in rooms)
            {
                if (left <= 0) break;
                int ask = Math.Min(left, kv.Value);
                Settlement region;
                double got = 0.0;
                try { got = PeopleBook.TakeLevyHere(kv.Key, ask, PeopleBook.MLevyVillage, out region); }
                catch (Exception e) { Stumble("Take", e); continue; }
                // 474-P5b-p (recenzja B1): TakeLevyHere idzie z notePool=false (wlasny pobor wioski pobocznej) - tu bierzemy z DOM wsi siedziby, wiec pule mlodych
                // wiosek pobocznych tej wsi traca swoj udzial tak samo jak przy poborze gry z DOM (FromVillage notePool) - bez tego ci sami ludzie liczeni dwa razy.
                if (got > 0.0 && kv.Key.Village != null) { try { SideVillageVisit.LevyNoteDistrictDraw(kv.Key.Village, got); _poolNotes++; } catch (Exception e) { Stumble("PoolNote", e); } }
                int keep = Math.Min(ask, (int)Math.Ceiling(got - 1e-6));
                if (keep <= 0) continue;
                try
                {
                    mp.MemberRoster.AddToCounts(troop, keep);
                    PeopleBook.GiveNew(mp.MemberRoster, region, got, PeopleBook.FLevyPlayer);
                    if (keep - got > 1e-6) PeopleBook.GiveNone(mp.MemberRoster, keep - got, PeopleBook.FNoneFloor);
                }
                catch (Exception e) { Stumble("Men", e); }
                int pay = keep * cost;
                try { GiveGoldAction.ApplyForCharacterToSettlement(hero, kv.Key, pay, true); }
                catch (Exception e) { Stumble("Pay", e); }
                paid.Add(Txt(kv.Key.Name) + " " + keep + " (" + pay + ")");
                left -= keep; keepAll += keep; gotAll += got;
            }
            int total = gold0 - hero.Gold;
            _rec++; _men += keepAll; _gold += total;
            Log.Info(P + Txt(seat.Name) + " (" + seat.StringId + ", " + h.Name + ", pan " + (seat.OwnerClan != null ? Txt(seat.OwnerClan.Name) : "-") + "): " + keepAll + " x " + troop.StringId
                     + " z prosby " + n + ", cena ROT " + cost + " | gracz " + gold0 + " -> " + hero.Gold + " (zaplacone " + total + ") | z DOM wsi " + gotAll.ToString("0.##", Inv)
                     + " (wolne przed " + room + "): " + (paid.Count > 0 ? string.Join(", ", paid.ToArray()) : "-") + " | od startu gry: werbunki " + _rec + ", ludzi " + _men
                     + ", zlota " + _gold + ", pule wiosek pobocznych pomniejszone (474-P5b-p) " + _poolNotes + "x, skutek ROT bez zmian " + _orgCalls + "x, potkniecia " + _stumbles + ".");
            if (keepAll > 0)
            {
                Log.Player((keepAll == 1 ? "One man of " + h.Name + " joins" : keepAll + " men of " + h.Name + " join") + " your party. Their families in the villages of "
                           + Txt(seat.Name) + " got " + total.ToString("N0", Inv) + " denars.");
                if (total > 0)
                {
                    MBTextManager.SetTextVariable("GOLD_AMOUNT", total);
                    InformationManager.DisplayMessage(new InformationMessage(GameTexts.FindText("str_gold_removed_with_icon").ToString(), "event:/ui/notification/coins_negative"));
                }
            }
            else Log.Player("The villages of " + Txt(seat.Name) + " have no men left to spare.", true);
        }

        private static string Txt(TextObject t) { try { return t != null ? t.ToString() : "?"; } catch { return "?"; } }
    }
}
