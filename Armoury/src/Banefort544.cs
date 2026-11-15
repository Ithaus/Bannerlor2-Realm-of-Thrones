using System;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 544 - JEDNORAZOWA NAPRAWA ZAPISU: Banefort wraca do korony (Jeff, czat 15.11: "oddaj po zaladowaniu gry zamek koronie ten banefort ... do jego prawowitego
    /// wlasciciela, bo zakladamy ze to byl cheat - zamek bronil sie i nie powinien pasc").
    /// DOWOD: Armoury-2026-11-15_04-07-23.log l.10459-10462, dzien 109026 - "Czas oblezenia (471b) kapitulacja: ... ROT_castle20 (Banefort) | droga a - termin R minal",
    /// jedzenie zalogi 144 dob, 4 szturmy odparte; pan ROTclan_59 (House Banefort) -> clan_battania_1 (Polnoc). Zamek padl TYLKO z reguly 471b (zmiana 542 zdejmuje
    /// kapitulacje z terminu na przyszlosc; ta zmiana naprawia raz biezaca kampanie). Klucz North Conquest Return (326b) u Jeffa NIE - 326b go nie zwroci.
    /// KIEDY: raz na kampanie, flaga w zapisie "arm_544_banefort" ("" czeka, "done:..." zrobione, "new" - nowa kampania: nic do naprawy). Proba co godzine gry
    /// (HourlyTickEvent - mapa, nie wnetrze menu), tylko gdy gracz stoi na mapie (BookHoldings.PlayerOnMapFor: nie w osadzie, menu, spotkaniu, bitwie, oblezeniu,
    /// armii, rozmowie, niewoli; nigdy GameMenu.SwitchToMenu). Warunki niespelnione - nic, linia "czeka" raz na dobe i ponowna proba (takze po nastepnym wczytaniu).
    /// WARUNKI: korona = krolestwo "vlandia" (House Baratheon of King's Landing) zywe; Banefort (ROT_castle20) u rodu Polnocy (krolestwo "battania"; inny pan - koniec bez zmian, recenzja 544);
    /// pan nie jest rodem gracza (wtedy koniec bez zmian - decyzja gracza). Odbiorca: House Banefort (ROTclan_59) - zywy, z zywa glowa, w koronie, nie najemnik,
    /// nie rod gracza; glowa w niewoli (poza lochem Banefortu) - czekamy do 15 dob, potem jak brak rodu; brak rodu - rod panujacy korony (domena krola, jak 326b).
    /// JAK: BookHoldings.TransferFor (ta sama droga co 326b i f19-kanon-b): oblezenie / bitwa w osadzie, zalodze, wsiach i przy bramie - czekamy; bohaterowie bez
    /// druzyny, ktorych gra wzielaby do niewoli - do najblizszej wlasnej twierdzy; zaloga Polnocy (ludzie, ranni, ksiega ludzi, sprzet zbrojowni) - do partii
    /// lordow i zalog Polnocy od najblizszej, reszta do druzyny glowy rodu, bez odbiorcy - do domu droga W4c (nic nie znika, nic nie powstaje); roszczenie SCA
    /// Polnocy jak przed zmiana; ChangeOwnerOfSettlementAction.ApplyByDefault (NIE BySiege: bez niszczenia zalogi, kar relacji, wplywu, glosowania o lenno,
    /// naszych sluchaczy zdobycia; NIE ByGift/ByBarter: BKTitleBehavior nie rusza tytulow - tytul BK zostaje de iure przy rodzie, jak byl); wrogie partie w murach
    /// wyprowadza gra (PartyDiplomaticHandler przy zmianie pana); wsie Banefortu ida za zamkiem (pan wsi = pan twierdzy w grze); zwykli jency - zolnierze
    /// korony - do zalogi (f23, klucz BookHoldingsFreeOwnPrisoners); BEZ obsady zalogi z innych partii (326x, Jeff 05.11: zaloga z werbunku pana, na ile go stac).
    /// Po zmianie: roszczenie SCA korony na Banefort zdjete (osada wrocila). Jedna wiadomosc dla gracza po angielsku. Potkniecia liczone, bez wylacznika.
    /// </summary>
    internal static class Banefort544
    {
        internal const string Tag = "Banefort (544): ";
        private const string StId = "ROT_castle20", ClanId = "ROTclan_59", CrownId = "vlandia", NorthId = "battania";
        private const int PrisonerWaitDays = 15;
        private const int MaxErrors = 3;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // zapis
        private static string _state = "";     // "" czeka, "new" nowa kampania, "done:dzien:wynik"
        private static int _firstPrisonerDay = -1;   // doba pierwszego czekania na glowe rodu w niewoli (w zapisie)

        // sesja
        private static int _errors, _waitDay = -1;
        private static string _waitText;
        private static bool _errLogged;

        internal static void Reset() { _state = ""; _firstPrisonerDay = -1; _errors = 0; _waitDay = -1; _waitText = null; _errLogged = false; }

        /// <summary>555: zwrot jeszcze czeka (flaga pusta) - dar zalogi 555 czeka na niego.</summary>
        internal static bool Pending { get { return _state.Length == 0; } }

        internal static string Export() { return "b1|" + _state + "|" + _firstPrisonerDay.ToString(Inv); }

        internal static void Import(string v)
        {
            Reset();
            if (string.IsNullOrEmpty(v)) return;   // zapis sprzed 544 - naprawa czeka
            try
            {
                var p = v.Split('|');
                if (p.Length < 3 || p[0] != "b1") { _state = "done:?:nieznany-format"; return; }
                _state = p[1];
                int d; _firstPrisonerDay = int.TryParse(p[2], NumberStyles.Integer, Inv, out d) ? d : -1;
            }
            catch { _state = "done:?:blad-odczytu"; }
        }

        internal static void OnNewGame() { _state = "new"; }

        internal static void SessionStart()
        {
            try
            {
                Log.Info(Tag + "start sesji | stan zapisu: " + (_state.Length == 0 ? "czeka (naprawa w pierwszej godzinie na mapie)" : _state == "new" ? "nowa kampania - nic do naprawy" : "zrobione (" + _state + ")") + ".");
            }
            catch { }
        }

        private static Kingdom FindKingdom(string id) { foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k; return null; }
        private static Clan FindClan(string id) { foreach (var c in Clan.All) if (c != null && c.StringId == id) return c; return null; }
        private static string Nm(object o) { try { return o == null ? "-" : (o is Clan ? ((Clan)o).Name.ToString() : o is Kingdom ? ((Kingdom)o).Name.ToString() : o is Hero ? ((Hero)o).Name.ToString() : o.ToString()); } catch { return "?"; } }

        private static void Done(int day, string res, string text)
        {
            _state = "done:" + day.ToString(Inv) + ":" + res;
            Log.Info(Tag + "dzien " + day + " | " + text + " | flaga w zapisie: zrobione (nie powtorzy sie).");
        }

        private static void Wait(int day, string why)
        {
            _errors = 0;   // recenzja 544: przebieg bez wyjatku zeruje licznik ("z rzedu")
            if (day == _waitDay && why == _waitText) return;
            if (day == _waitDay && why != null && _waitText != null && why.StartsWith("gracz", StringComparison.Ordinal) && _waitText.StartsWith("gracz", StringComparison.Ordinal)) return;
            _waitDay = day; _waitText = why;
            Log.Info(Tag + "czeka - dzien " + day + " | " + why + " (proba co godzine, tez po nastepnym wczytaniu).");
        }

        /// <summary>Rod House Banefort, gdy moze przyjac zamek; inaczej null i powod.</summary>
        private static Clan Rightful(Kingdom crown, Settlement st, int day, out string why, out bool waitPrisoner)
        {
            why = null; waitPrisoner = false;
            var c = FindClan(ClanId);
            if (c == null || c.IsEliminated) { why = "House Banefort (" + ClanId + ") nie istnieje albo wyeliminowany"; return null; }
            var l = c.Leader;
            if (l == null || !l.IsAlive) { why = Nm(c) + " bez zywej glowy"; return null; }
            if (c == Clan.PlayerClan) { why = Nm(c) + " to rod gracza"; return null; }
            if (c.Kingdom != crown) { why = Nm(c) + " poza korona (w " + (c.Kingdom != null ? Nm(c.Kingdom) : "-") + ")"; return null; }
            if (c.IsUnderMercenaryService) { why = Nm(c) + " najemnikiem"; return null; }
            if (l.IsPrisoner)
            {
                var pp = l.PartyBelongedToAsPrisoner;
                if (pp != null && pp == st.Party) return c;   // w lochu Banefortu - gra wypusci go przy zmianie pana
                if (_firstPrisonerDay < 0) _firstPrisonerDay = day;
                if (day - _firstPrisonerDay < PrisonerWaitDays) { waitPrisoner = true; why = "glowa rodu " + Nm(l) + " w niewoli (czekam " + (day - _firstPrisonerDay) + "/" + PrisonerWaitDays + " dob)"; return null; }
                why = "glowa rodu " + Nm(l) + " w niewoli ponad " + PrisonerWaitDays + " dob";
                return null;
            }
            _firstPrisonerDay = -1;   // recenzja 544: glowa na wolnosci - nowa niewola liczy 15 dob od nowa
            return c;
        }

        /// <summary>HourlyTickEvent. Nie rzuca.</summary>
        internal static void Hourly()
        {
            if (_state.Length > 0) return;
            int day = -1;
            try
            {
                var camp = Campaign.Current;
                if (camp == null || !camp.GameStarted) return;
                day = (int)CampaignTime.Now.ToDays;
                var st = Settlement.Find(StId);
                if (st == null || !st.IsFortification || st.Town == null) { Done(day, "brak-osady", "osady " + StId + " nie ma w swiecie - nic do naprawy"); return; }
                var crown = FindKingdom(CrownId);
                if (crown == null || crown.IsEliminated) { Wait(day, "korony (" + CrownId + ") brak albo zniszczona"); return; }
                var owner = st.OwnerClan;
                if (owner == null) { Wait(day, "Banefort bez pana"); return; }
                if (owner.Kingdom == crown) { Done(day, "juz-w-koronie", "Banefort juz w koronie (pan " + Nm(owner) + ") - bez zmian"); return; }
                if (owner == Clan.PlayerClan) { Done(day, "gracz", "Banefort nalezy do rodu gracza - nie zabieram (decyzja gracza)"); return; }
                // recenzja 544: naprawiamy tylko skutek kapitulacji z dnia 109026 - pan z Polnocy (dowolny rod krolestwa "battania": lenno moglo
                // pojsc glosowaniem do innego rodu Polnocy). Inny pan (np. zdobyl normalnym oblezeniem ktos trzeci) - zdobycz uczciwa, bez zmian.
                if (owner.Kingdom == null || owner.Kingdom.StringId != NorthId)
                { Done(day, "inny-pan", "Banefort u " + Nm(owner) + " (" + (owner.Kingdom != null ? Nm(owner.Kingdom) : "-") + "), nie u Polnocy - to nie skutek kapitulacji 471b, bez zmian"); return; }
                string why;
                if (!BookHoldings.PlayerOnMapFor(out why)) { Wait(day, why); return; }

                bool waitPrisoner;
                string rwhy;
                var to = Rightful(crown, st, day, out rwhy, out waitPrisoner);
                string toWhy = "prawowity pan";
                if (to == null)
                {
                    if (waitPrisoner) { Wait(day, rwhy); return; }
                    var rc = crown.RulingClan;
                    if (rc == null || rc.IsEliminated || rc.Leader == null || !rc.Leader.IsAlive || rc.Leader.IsPrisoner || rc == Clan.PlayerClan)
                    { Wait(day, rwhy + "; rod panujacy korony nie moze przyjac (brak, bez glowy, glowa w niewoli albo rod gracza)"); return; }
                    to = rc;
                    toWhy = rwhy + " - rod panujacy korony (domena krola)";
                }

                string wait;
                string res = BookHoldings.TransferFor(Tag, StId, null, to, day, out wait, prisonerHereOk: true, donorRadius: 0f, noFill: true);   // 326x: bez obsady
                if (res == null) { Wait(day, wait ?? "?"); return; }
                _errors = 0;
                if (res.StartsWith("ok", StringComparison.Ordinal))
                {
                    string sca = BookHoldings.ScaDropClaim(crown, StId);
                    Done(day, "ok", "ZWROT Banefort [" + StId + "] " + Nm(owner) + " (" + (owner.Kingdom != null ? Nm(owner.Kingdom) : "-") + ") -> " + Nm(to) + " (" + Nm(crown) + "; " + toWhy
                         + ") | zamek padl tylko z kapitulacji z terminu 471b (dzien 109026) - naprawa raz na kampanie | " + sca + " | szczegoly w linii wyzej");
                    try { Log.Player("Banefort is returned to House " + Nm(to) + ": the castle held out and should not have surrendered."); } catch { }
                }
                else Done(day, res, "bez zwrotu: " + res + " (linia wyzej)");
            }
            catch (Exception e)
            {
                _errors++;
                if (!_errLogged) { _errLogged = true; try { Log.Error("Banefort544.Hourly", e); } catch { } }
                if (_errors >= MaxErrors) { try { Done(day, "blad", MaxErrors + " wyjatki z rzedu - naprawa porzucona (blad w logu wyzej)"); } catch { _state = "done:?:blad"; } }
            }
        }
    }

    /// <summary>544: jednorazowy zwrot Banefortu koronie (flaga arm_544_banefort w zapisie).</summary>
    internal sealed class Banefort544Behavior : CampaignBehaviorBase
    {
        public Banefort544Behavior() { Banefort544.Reset(); }

        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => Banefort544.OnNewGame());
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => Banefort544.SessionStart());
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, Banefort544.Hourly);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                string v = dataStore.IsSaving ? Banefort544.Export() : null;
                SaveText.Sync(dataStore, "arm_544_banefort", ref v);
                if (dataStore.IsLoading) Banefort544.Import(v);
            }
            catch (Exception e) { Log.Error("SyncData.arm_544_banefort", e); }
        }
    }
}
