using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Armoury.Villages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// E7/W3 (234, PLAN 7.9 b, R13; projekt etapu 7 v2 rozdz. 3 "E7/W3") - MENU OKREGU "LOOK OVER THE DISTRICT".
    /// Pozycja w menu "village" i "village_looted" (zwykle menu, nie menu oczekiwania - bez GameMenu.SwitchToMenu, CLAUDE.md 7, TECH 3.1) otwiera okno
    /// MBInformationManager.ShowMultiSelectionInquiry z wioskami okregu: wiersze z ksiegi wiosek (VillageBook.DistrictView - ten sam stan StateAt i ta sama
    /// funkcja wiersza RowLine co sekcja okregu w "People of this land"), w kazdym wierszu herb rycerza (InquiryElement z BannerImageIdentifier): posiadacz
    /// tytulu wsi z BK (R12, MapVillageLore.BkHolder) - herb jego rodu, inaczej knight_banner z lore, inaczej herb pana (Village.Bound.OwnerClan - jak dymek W2).
    /// Dymek wiersza: pelny wiersz menu z udzialem w plonie, rycerz, zajecie, pamiec wojny; "Hear more" - pelny opis wioski (ShowInquiry: wiersz, rycerz, wiara BK,
    /// zajecie, swiete miejsce, historia, godlo rycerza z lore, pamiec wojny) z "Back to the villages" (okno od nowa) i "Leave".
    /// "People of this land" zostaje z naglowkiem i podsumowaniem okregu, bez wierszy wiosek (VillageBook.MenuSection pyta DistrictMenu.On).
    /// Koszt: tylko po kliknieciu (odczyt ksiegi pod Gate, jeden odczyt tytulu BK na okno, wiara BK przy "Hear more"). Bez stanu w zapisie (stary zapis bez zmian).
    /// Bledy liczone (licznik potkniec, pierwsze 3 w logu), nic sie nie gasi. Klucz DistrictMenuEnabled (off = brak pozycji, sekcja okregu z wierszami jak dotad).
    /// </summary>
    internal static class DistrictMenu
    {
        internal const string OptVillage = "arm_district_village", OptLooted = "arm_district_village_looted";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static bool _wired, _startLogged;
        private static int _st;                                                              // potkniecia (sesja)
        private static int _dOpen, _dMore, _dRowsMax, _dSt, _dBk, _dLore, _dLord, _dNone;    // doba
        private static int _dEst, _dRealm;                                                   // Jeff 16.10 (D1): herby wierszy z majatku BK i od rycerzy krolestwa
        private static int _dCanon, _dWords;                                                 // Jeff 16.10 (D2): herby wierszy rodow kanonu, "Hear more" z dewiza

        /// <summary>Wiersz okna: wioska z ksiegi, wiersz lore (albo null), gotowa linia rycerza (albo null) i gospodarz (D1; null - klucz off, jak dotad).</summary>
        private sealed class Item { public VillageBook.DistRow R; public MapVillageLore.Row Lore; public string Knight; public VillageHolders.Holder H; }

        /// <summary>Pozycja wpieta i klucz wlaczony - wtedy "People of this land" pokazuje okreg bez wierszy wiosek.</summary>
        internal static bool On { get { var s = Settings.Current; return _wired && s != null && s.DistrictMenuEnabled; } }

        /// <summary>Konstruktor ArmouryBehavior (przed OnSessionLaunched): stan sesji.</summary>
        internal static void Reset() { _wired = _startLogged = false; _st = 0; DayReset(); }

        private static void DayReset() { _dOpen = _dMore = _dRowsMax = _dSt = _dBk = _dLore = _dLord = _dNone = _dEst = _dRealm = _dCanon = _dWords = 0; }

        private static void Stumble(string where, Exception e)
        {
            _st++; _dSt++;
            if (_st <= 3 || _st % 200 == 0) Log.Error("DistrictMenu." + where + " (potkniecie " + _st + ")", e);
        }

        /// <summary>ArmouryBehavior.OnSessionLaunched zaraz po PeopleMenu.AddMenus: pozycja za "People of this land" (village index 6 -> 7; village_looted index 0 -> 1,
        /// nad "Leave..."). Gra (OnAfterSessionLaunched) i BK dokladaja swoje opcje - prawdziwy index podaje linia startowa (pierwsza doba sesji).</summary>
        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                starter.AddGameMenuOption("village", OptVillage, VillageTexts.DistOption, Condition, Consequence, false, 7);
                starter.AddGameMenuOption("village_looted", OptLooted, VillageTexts.DistOption, Condition, Consequence, false, 1);
                _wired = true;
            }
            catch (Exception e) { _wired = false; Log.Error("DistrictMenu.AddMenus", e); }
        }

        private static bool Condition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                if (!On || !VillageBook.Active) return false;
                var st = Settlement.CurrentSettlement;
                return st != null && st.IsVillage;
            }
            catch { return false; }
        }

        private static void Consequence(MenuCallbackArgs args) { Open(Settlement.CurrentSettlement); }

        /// <summary>Okno okregu: tytul "Villages of the X district", naglowek z podsumowaniem, wiersz na wioske (wybor jednej - "Hear more").</summary>
        private static void Open(Settlement st)
        {
            try
            {
                if (st == null || !st.IsVillage) return;
                var rows = new List<VillageBook.DistRow>();
                string name, head;
                if (!VillageBook.DistrictView(st, out name, out head, rows)) return;     // ksiega nieczynna (pozycja i tak ukryta) albo blad (licznik ksiegi)
                // ZC2: w naglowku sklad stanow i Dostatek okregu (tylko odczyt; klucz DailyLifeEstates off - null)
                try { string zc = DailyLife.DistrictLine(st); if (!string.IsNullOrEmpty(zc)) head = (head ?? "") + "\n" + zc; }
                catch (Exception e) { Stumble("Estates " + st.StringId, e); }
                // inwest (ui_text 4): produkcja okregu wedlug zajec, glowny towar i prace gracza (null - nic)
                try { string po = DistrictWorks.HeadText(st); if (!string.IsNullOrEmpty(po)) head = (head ?? "") + "\n" + po; }
                catch (Exception e) { Stumble("Works " + st.StringId, e); }
                _dOpen++;
                if (rows.Count > _dRowsMax) _dRowsMax = rows.Count;
                string title = VillageTexts.Make(VillageTexts.DistTitle, "DISTRICT", name ?? "?").ToString();
                string leave = VillageTexts.Make(VillageTexts.DistClose).ToString();
                Clan lord = st.Village != null && st.Village.Bound != null ? st.Village.Bound.OwnerClan : null;   // pan jak dymek W2 (HeldLine)
                Hero holder = MapVillageLore.BkHolder(st, lord);                                                  // jeden odczyt tytulu BK na okno (tytul jest na wies gry; D1 off)
                var list = new List<InquiryElement>(rows.Count);
                foreach (var r in rows) list.Add(Element(st, r, lord, holder));
                if (list.Count == 0)
                {
                    InformationManager.ShowInquiry(new InquiryData(title, head ?? "", true, false, leave, null, delegate { }, null), true);
                    return;
                }
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(title, (head ?? "") + "\n" + VillageTexts.Make(VillageTexts.DistPick).ToString(),
                    list, true, 1, 1, VillageTexts.Make(VillageTexts.DistHearMore).ToString(), leave,
                    delegate (List<InquiryElement> picked)
                    {
                        if (picked != null && picked.Count > 0) More(st, picked[0].Identifier as Item, holder);
                    }, null), false);
            }
            catch (Exception e) { Stumble("Open " + (st != null ? st.StringId : "?"), e); }
        }

        /// <summary>Wiersz okna: krotki tytul (stan z ksiegi), herb rycerza, dymek (pelny wiersz menu z udzialem w plonie, rycerz, zajecie, pamiec wojny).</summary>
        private static InquiryElement Element(Settlement st, VillageBook.DistRow r, Clan lord, Hero holder)
        {
            var it = new Item { R = r };
            Banner b = null;
            var hint = new List<string>(4) { r.Line + " " + r.Share };
            try
            {
                it.Lore = r.Uid != null ? MapVillageLore.Of(r.Uid) : null;
                // Jeff 16.10 (D1, VillageBannerPerHolder): gospodarz TEJ wioski (VillageHolders - ten sam co proporczyk i dymek; wies gry - tytul BK albo pan)
                it.H = Settings.Current.VillageBannerPerHolder ? VillageHolders.Of(st, r.Main ? null : r.Uid) : null;
                if (it.H != null)
                {
                    it.Knight = VillageHolders.Line(it.H);
                    b = it.H.Banner;
                    if (b != null)
                        switch (it.H.Rule)
                        {
                            case VillageHolders.RuleTitle: _dBk++; break;
                            case VillageHolders.RuleEstate: _dEst++; break;
                            case VillageHolders.RuleLore: _dLore++; break;
                            case VillageHolders.RuleRealmEstate: case VillageHolders.RuleRealmHouse: _dRealm++; break;
                            case VillageHolders.RuleCanon: _dCanon++; break;
                            default: _dLord++; break;
                        }
                }
                else
                {
                    it.Knight = MapVillageLore.KnightLine(it.Lore, lord, holder);
                    if (holder != null && holder.Clan != null) { b = holder.Clan.Banner; if (b != null) _dBk++; }
                    else if (it.Lore != null && it.Lore.KnightBanner != null) { b = it.Lore.KnightBanner; _dLore++; }
                    else if (lord != null) { b = lord.Banner; if (b != null) _dLord++; }
                }
                if (it.Knight != null) hint.Add(it.Knight);
                string est = DailyLife.DistrictRowCounts(st, r.Uid, r.Main);   // ZC2: ludzie wioski wedlug stanow okregu (null - wies gry, klucz off)
                if (est != null) hint.Add(est);
                if (it.Lore != null && it.Lore.Livelihood.Length > 0) hint.Add(VineOlive.Livelihood(st, GoodsRates.Livelihood(st, r.Uid, it.Lore.Livelihood)));   // GL-5 (P5): wies lniana - zdanie lnu
                string mem = Memory(r, it.Lore);
                if (mem != null) hint.Add(mem);
                hint.AddRange(DistrictWorks.PartLines(st, r.Main ? null : r.Uid, false));   // inwest (ui_text 4): zajecie, udzial w produkcji okregu, prace
            }
            catch (Exception e) { Stumble("wiersz " + (r.Uid ?? r.Name ?? "?"), e); }
            if (b == null) _dNone++;
            return new InquiryElement(it, r.Title ?? r.Name ?? "?", b != null ? new BannerImageIdentifier(b, false) : null, true, string.Join("\n", hint.ToArray()));
        }

        /// <summary>Pamiec wojny wioski (W1, napastnik na zywo) albo war_lore z lore; wies gry - bez pamieci po uid.</summary>
        private static string Memory(VillageBook.DistRow r, MapVillageLore.Row row)
        {
            string mem = r.Uid != null ? VillageNews.MemoryText(r.Uid) : null;
            if (mem == null && row != null && row.WarLore.Length > 0) mem = row.WarLore;
            return mem;
        }

        /// <summary>"Hear more": pelny opis wioski - wiersz z udzialem, rycerz, wiara BK; zajecie, swiete miejsce (faith_bk = dominujaca wiara BK, jak dymek W2),
        /// historia, godlo rycerza z lore (tylko gdy rycerz z lore), pamiec wojny. "Back to the villages" - okno okregu od nowa.</summary>
        private static void More(Settlement st, Item it, Hero holder)
        {
            try
            {
                if (it == null || it.R == null) return;
                _dMore++;
                var sb = new StringBuilder(900);
                sb.Append(it.R.Line).Append(' ').Append(it.R.Share);
                if (it.Knight != null) sb.Append('\n').Append(it.Knight);
                string bkId;
                string faith = MapVillageLore.FaithLine(st, out bkId);
                if (faith != null) sb.Append('\n').Append(faith);
                var row = it.Lore;
                var more = new List<string>(5);
                if (row != null)
                {
                    if (row.Livelihood.Length > 0) more.Add(VineOlive.Livelihood(st, GoodsRates.Livelihood(st, it.R.Uid, row.Livelihood)));   // GL-5 (P5): wies lniana - zdanie lnu
                    if (faith != null && row.Faith.Length > 0 && row.FaithMatches(bkId)) more.Add(row.Faith);
                    if (row.History.Length > 0) more.Add(row.History);
                    // godlo rycerza z lore - gdy wioske trzyma on (D1: regula lore albo kasztelan pana z lore; klucz off - brak tytulu BK, jak dotad)
                    bool loreKnight = it.H != null ? (it.H.Rule == VillageHolders.RuleLore || it.H.Rule == VillageHolders.RuleLord) : holder == null;
                    if (loreKnight && row.Knight.Length > 0 && row.House.Length > 0 && row.Arms.Length > 0)
                        more.Add(VillageTexts.Make(VillageTexts.VilArms, "HOUSE", row.House, "ARMS", Lower(row.Arms)).ToString());
                }
                // Jeff 16.10 (D2): rod kanonu - herb slowami z kanonu i dewiza (gdy znane); nazwa rodu z siedziba ("House Florent of Brightwater Keep")
                var ch = it.H != null && it.H.Rule == VillageHolders.RuleCanon ? it.H.Canon : null;
                if (ch != null)
                {
                    if (ch.ArmsText.Length > 0) more.Add(VillageTexts.Make(VillageTexts.VilArms, "HOUSE", ch.Name, "ARMS", Lower(ch.ArmsText)).ToString());
                    if (ch.Words.Length > 0) { more.Add(VillageTexts.Make(VillageTexts.VilWords, "HOUSE", ch.Name, "WORDS", ch.Words).ToString()); _dWords++; }
                }
                string mem = Memory(it.R, row);
                if (mem != null) more.Add(mem);
                more.AddRange(DistrictWorks.PartLines(st, it.R.Main ? null : it.R.Uid, false));   // inwest (ui_text 4): zajecie i prace w "Hear more"
                if (more.Count > 0) sb.Append("\n\n").Append(string.Join("\n", more.ToArray()));
                InformationManager.ShowInquiry(new InquiryData(it.R.Name ?? "?", sb.ToString(), true, true, VillageTexts.Make(ShortLabels.Pick(VillageTexts.DistBackShort, VillageTexts.DistBack)).ToString(),   // n20-napisy
                    VillageTexts.Make(VillageTexts.DistClose).ToString(), delegate { Open(st); }, null), true);
            }
            catch (Exception e) { Stumble("HearMore " + (it != null && it.R != null ? (it.R.Uid ?? it.R.Name) : "?"), e); }
        }

        /// <summary>"A green rook on gold" -> "a green rook on gold" (godlo w srodku zdania); skroty i nazwy z dwiema wielkimi literami zostaja.</summary>
        private static string Lower(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 2 || !char.IsUpper(s[0]) || char.IsUpper(s[1])) return s;
            return char.ToLowerInvariant(s[0]) + s.Substring(1);
        }

        // ------------------------------------------------------------ log
        /// <summary>ArmouryBehavior.OnDailyTick (po MapVillageLore.Daily): w pierwszej dobie sesji linia startowa (wszystkie mody i gra - takze OnAfterSessionLaunched -
        /// dopisaly juz swoje opcje, wiec index jest prawdziwy); potem linia doby tylko, gdy okno otwarto albo bylo potkniecie.</summary>
        internal static void Daily()
        {
            if (!_startLogged) { _startLogged = true; StartLine(); }
            if (_dOpen > 0 || _dSt > 0)
                Log.Info("Menu okregu (E7/W3): dzien " + ((int)CampaignTime.Now.ToDays).ToString(Inv) + " | okien " + _dOpen + " (wiosek w oknie najwiecej " + _dRowsMax + "), Hear more " + _dMore
                         + " | herby wierszy: rod posiadacza tytulu BK " + _dBk + ", rycerz z lore " + _dLore + ", pan " + _dLord + ", bez herbu " + _dNone
                         + ", majatek BK " + _dEst + ", rycerz krolestwa " + _dRealm + ", rod kanonu " + _dCanon + " (Hear more z dewiza " + _dWords + ")"
                         + " | potkniecia dzis " + _dSt + " (sesja " + _st + ")");
            DayReset();
        }

        private static void StartLine()
        {
            try
            {
                var s = Settings.Current;
                const string head = "Menu okregu (E7/W3): start | ";
                if (s == null || !s.DistrictMenuEnabled) { Log.Info(head + "WYLACZONY w MCM (DistrictMenuEnabled) - brak pozycji, People of this land z wierszami wiosek jak dotad."); return; }
                if (!_wired) { Log.Info(head + "pozycja NIEWPIETA (blad DistrictMenu.AddMenus wyzej w logu) - People of this land z wierszami wiosek jak dotad."); return; }
                var mgr = Campaign.Current != null ? Campaign.Current.GameMenuManager : null;
                Log.Info(head + "\"Look over the district\" wpieta: " + Where(mgr, "village", OptVillage) + "; " + Where(mgr, "village_looted", OptLooted)
                         + " | People of this land we wsi: naglowek i podsumowanie okregu bez wierszy wiosek | ksiega wiosek " + (VillageBook.Active ? "czynna" : "NIECZYNNA - pozycja ukryta")
                         + " | koszt: tylko po kliknieciu.");
            }
            catch (Exception e) { Stumble("StartLine", e); }
        }

        /// <summary>"village index 8 z 12 (przed: arm_people_village, po: disembark)" - pozycja w menu po wszystkich modach.</summary>
        private static string Where(GameMenuManager mgr, string menuId, string opt)
        {
            var menu = mgr != null ? mgr.GetGameMenu(menuId) : null;
            if (menu == null) return menuId + " BRAK MENU";
            var ids = new List<string>();
            foreach (var o in menu.MenuOptions) ids.Add(o != null ? o.IdString : "?");
            int at = ids.IndexOf(opt);
            if (at < 0) return menuId + " BRAK pozycji (" + ids.Count + " opcji)";
            return menuId + " index " + at + " z " + ids.Count + " (przed: " + (at > 0 ? ids[at - 1] : "-") + ", po: " + (at + 1 < ids.Count ? ids[at + 1] : "-") + ")";
        }
    }
}
