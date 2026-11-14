using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// ZACIAG RODOWY (Jeff 02.09: "tak samo jak na Polnocy i w Nocnej Strazy
    /// czasami pojawiaja sie rekruci elitarni" -> "szansa wszedzie taka sama,
    /// daj ta sama zasade"). W danych ROT z osad wychodzi TYLKO drzewo
    /// kultury (zwykle + szlacheckie); linie rodowe (Blackwood -> Ravens'
    /// Teeth, Karstark, Bolton, Lannister...) zyja w szablonach klanow: lord
    /// werbuje zwyklego rekruta, a ROT (ROTTroopRecruiter) podmienia mu go
    /// na czlowieka rodu - gracz jest z tego wylaczony. Nocna Straz rodow
    /// nie ma, wiec tam cale drzewo jest do zwerbowania.
    /// TA SAMA ZASADA CO DLA ELIT: sami nic nie losujemy. Gra (vanilla/BK)
    /// wystawia u notabli rekrutow linii SZLACHECKIEJ wedle wlasnych regul
    /// (moc notabla, relacje) - i to jest czestotliwosc "elit" znana z Polnocy.
    /// My tylko mowimy: na ziemi rodu z wlasnym szablonem szlachecki ochotnik
    /// jest czlowiekiem TEGO rodu tego samego tieru (ziemia Blackwoodow rodzi
    /// ludzi Blackwoodow). Zwykle sloty zostaja zwykle. Przeglad raz dziennie
    /// per osada + natychmiast, gdy gracz wjezdza do osady.
    /// 536 (Jeff 15.11: "w wioskach glownych tez dawaj blackwood recruit, nie tylko riverland recruit, oczywiscie w wioskach blackwooda, bo w innych
    /// jesli nie ma wojska rodowego to nie, to wtedy general recruit"): ZWYKLY ochotnik tier 1 (BasicTroop kultury osady, tylko on - nie jego awanse)
    /// u notabli tej samej ziemi rodu (te same warunki co wyzej) z szansa HouseRecruitShare (domyslnie 25%) jest rekrutem rodu tier 1 (pieszy tier &lt;= 1
    /// z szablonu rodu - SideVillageVisit.HouseT1Of, ta sama lista co 531 we wsiach pobocznych). Los RAZ na czlowieka, gdy sie pojawia: pamietamy, ilu
    /// zwyklych notabl ma juz "rozstrzygnietych" (_decided, w zapisie), i losujemy tylko nadwyzke; werbunek zwyklego (OnTroopRecruited) zmniejsza
    /// liczbe rozstrzygnietych, zeby nowy w tym samym slocie dostal swoj los. Rod bez pieszego tier 1 w szablonie / osada nie rodu - zwykly rekrut.
    /// </summary>
    internal sealed class HouseLevies : CampaignBehaviorBase
    {
        private readonly Dictionary<Clan, List<CharacterObject>> _trees = new Dictionary<Clan, List<CharacterObject>>();
        private readonly Dictionary<CharacterObject, bool> _eliteCache = new Dictionary<CharacterObject, bool>();
        private int _swappedToday;
        private int _lastLogDay = -1;
        // 536: notabl (StringId) -> ilu zwyklych tier 1 w jego slotach ma juz swoj los (zostali zwykli); tylko nadwyzka losowana. W zapisie (arm_houselev_basic).
        private readonly Dictionary<string, int> _decided = new Dictionary<string, int>(StringComparer.Ordinal);
        private int _bRolled, _bSwapped, _bAbsorbed, _bRecruited, _st, _stDay;
        private readonly List<string> _bEx = new List<string>();

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, DayProfile.TownDay("HouseLevies.Convert", st => Convert(st)));   // E8/0: opakowanie pomiaru (tylko log)
            CampaignEvents.OnTroopRecruitedEvent.AddNonSerializedListener(this, OnRecruited);   // 536
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, DayProfile.Settle("HouseLevies.Convert", (party, st, hero) =>
            {
                if (party != null && party == MobileParty.MainParty) Convert(st);
            }));
        }

        public override void SyncData(IDataStore dataStore)
        {
            // 536: brak klucza = stary zapis - zwykli w slotach dostana los raz przy pierwszym przegladzie
            try
            {
                string v = dataStore.IsSaving ? Export() : null;
                SaveText.Sync(dataStore, "arm_houselev_basic", ref v);
                if (dataStore.IsLoading) Import(v);
            }
            catch (Exception e) { Log.Error("HouseLevies.SyncData", e); }
        }

        private const int MaxSlots = 64;   // recenzja 536: bezpiecznik liczby slotow notabla (gra 6, BK do 20)

        private string Export()
        {
            // recenzja 536: tylko zywi notable osad - umarli / zastapieni nie rosna w zapisie bez konca (gdy spisu nie da sie zrobic - wszystko jak bylo)
            HashSet<string> live = null;
            try
            {
                live = new HashSet<string>(StringComparer.Ordinal);
                foreach (var st in Settlement.All)
                    if (st != null && st.Notables != null)
                        foreach (var h in st.Notables) if (h != null && h.IsAlive && h.StringId != null) live.Add(h.StringId);
            }
            catch (Exception e) { Stumble("Export", e); live = null; }
            var sb = new System.Text.StringBuilder(_decided.Count * 24);
            foreach (var kv in _decided)
            {
                if (kv.Value <= 0 || string.IsNullOrEmpty(kv.Key) || kv.Key.IndexOf(';') >= 0 || kv.Key.IndexOf('=') >= 0) continue;
                if (live != null && live.Count > 0 && !live.Contains(kv.Key)) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(kv.Key).Append('=').Append(kv.Value);
            }
            return sb.ToString();
        }

        private void Import(string v)
        {
            _decided.Clear();
            if (string.IsNullOrEmpty(v)) return;
            foreach (var part in v.Split(';'))
            {
                int eq = part.LastIndexOf('='); int n;
                if (eq <= 0 || !int.TryParse(part.Substring(eq + 1), out n) || n <= 0) continue;
                _decided[part.Substring(0, eq)] = Math.Min(MaxSlots, n);   // recenzja 536: BK trzyma do 20 slotow - przyciecie do 6 losowalo od nowa ludzi juz rozstrzygnietych po kazdym wczytaniu
            }
        }

        /// <summary>536: werbunek z puli notabla - zabrany zwykly tier 1 byl rozstrzygniety (slot pusty, nowy czlowiek w nim dostanie swoj los).</summary>
        private void OnRecruited(Hero recruiter, Settlement settlement, Hero source, CharacterObject troop, int count)
        {
            try
            {
                if (source == null || troop == null || source.StringId == null) return;
                // recenzja 536: kultura osady NOTABLA (tam liczy Basics), nie miejsca werbunku - zaloga miasta bierze tez z notabli wsi innej kultury
                var home = source.CurrentSettlement ?? source.HomeSettlement;
                var cul = home != null && home.Culture != null ? home.Culture : (settlement != null && settlement.Culture != null ? settlement.Culture : troop.Culture);
                if (cul == null || troop != cul.BasicTroop) return;
                int d;
                if (!_decided.TryGetValue(source.StringId, out d) || d <= 0) return;
                d -= Math.Max(1, Math.Min(MaxSlots, count));
                if (d > 0) _decided[source.StringId] = d; else _decided.Remove(source.StringId);
                _bRecruited++;
            }
            catch (Exception e) { Stumble("OnRecruited", e); }
        }

        private void Stumble(string where, Exception e)
        {
            _st++; _stDay++;
            if (_st <= 5) Log.Error("HouseLevies." + where, e);
        }

        /// <summary>536: zwykli tier 1 notabla. Osada rodu (houseT1 niepusta) - nadwyzka ponad rozstrzygnietych losowana (HouseRecruitShare -> rekrut rodu);
        /// inaczej tylko pamiec (wszyscy obecni rozstrzygnieci - zmiana pana nie zamienia ludzi juz siedzacych w slotach). Nie rzuca.</summary>
        private void Basics(Settlement settlement, Hero notable, CharacterObject basic, List<CharacterObject> houseT1, float pct)
        {
            try
            {
                if (notable == null || notable.VolunteerTypes == null || notable.StringId == null) return;
                var slots = notable.VolunteerTypes;
                int c = 0;
                if (basic != null) for (int i = 0; i < slots.Length; i++) if (slots[i] == basic) c++;
                int d;
                _decided.TryGetValue(notable.StringId, out d);
                if (c > d && houseT1 != null && houseT1.Count > 0 && pct > 0f)
                {
                    int fresh = c - d;
                    for (int i = slots.Length - 1; i >= 0 && fresh > 0; i--)
                    {
                        if (slots[i] != basic) continue;
                        fresh--;
                        _bRolled++;
                        if (!(MBRandom.RandomFloat * 100f < pct)) continue;
                        var repl = houseT1[MBRandom.RandomInt(houseT1.Count)];
                        if (repl == null || repl == basic) continue;
                        try { RecruitKit.OnSwap(notable, basic, repl); } catch (Exception e) { RecruitKit.Stumble("OnSwap", e); }
                        slots[i] = repl;
                        c--;
                        _bSwapped++;
                        if (_bEx.Count < 3) _bEx.Add((settlement.Name != null ? settlement.Name.ToString() : settlement.StringId) + ": " + basic.StringId + " -> " + repl.StringId);
                    }
                }
                else if (c > d) _bAbsorbed += c - d;
                if (c > 0) _decided[notable.StringId] = c; else _decided.Remove(notable.StringId);
            }
            catch (Exception e) { Stumble("Basics", e); }
        }

        private void Convert(Settlement settlement)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.HouseLeviesEnabled) return;
                if (settlement == null || settlement.Notables == null) return;
                LogDay();
                var owner = settlement.OwnerClan;
                var basic = settlement.Culture != null ? settlement.Culture.BasicTroop : null;
                // TYLKO WLASNA ZIEMIA (02.09, screen Jeffa: puste, zamkniete sloty):
                // ludzie rodu maja kulture rodu, a Mends.LocalLevies wyrzuca
                // ochotnikow obcej kultury niz osada - dom z Westerlands trzymajacy
                // wioske Riverlands wstawial swoich, latka ich kasowala i zostawal
                // pusty slot. Ziemia rodzi ludzi rodu tylko tam, gdzie rod jest u siebie.
                bool land = owner != null && owner != Clan.PlayerClan && owner.DefaultPartyTemplate != null
                            && settlement.Culture != null && owner.Culture == settlement.Culture;
                var tree = land ? TreeOf(owner) : null;
                // 536: zwykli tier 1 - tylko pieszy tier <= 1 szablonu rodu (jak 531 we wsiach pobocznych); bez niego (albo osada nie rodu) zwykly rekrut bez zmian
                List<CharacterObject> houseT1 = null;
                if (land) { try { houseT1 = SideVillageVisit.HouseT1Of(owner, settlement.Culture); } catch (Exception e) { Stumble("HouseT1Of", e); } }
                float pct = Math.Max(0f, Math.Min(100f, s.HouseRecruitShare));
                foreach (var n in settlement.Notables) Basics(settlement, n, basic, houseT1, pct);
                if (tree == null || tree.Count == 0) return;

                foreach (var notable in settlement.Notables)
                {
                    if (notable == null || notable.VolunteerTypes == null) continue;
                    var slots = notable.VolunteerTypes;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var troop = slots[i];
                        if (troop == null || troop.IsHero || tree.Contains(troop)) continue;
                        if (!IsElite(troop)) continue;                    // tylko szlacheckie sloty - ta sama zasada co elity
                        var repl = Pick(tree, troop.Tier, settlement.Culture);
                        if (repl == null || repl == troop) continue;
                        // 171 A2: ten sam czlowiek w barwach rodu - zapis jego kompletu przechodzi z X na Y (dotad sierota, a Y bral wzorzec z niczego)
                        try { RecruitKit.OnSwap(notable, troop, repl); } catch (Exception e) { RecruitKit.Stumble("OnSwap", e); }
                        slots[i] = repl;
                        _swappedToday++;
                    }
                }
            }
            catch (Exception e) { Stumble("Convert", e); }
        }

        /// <summary>Linia doby (za poprzednia dobe, przy pierwszym przegladzie nowej): elity -> rod, 536 zwykli (losowani / zamienieni / bez losu / werbunki), przyklady.</summary>
        private void LogDay()
        {
            int day = (int)CampaignTime.Now.ToDays;
            if (day == _lastLogDay) return;
            if (_lastLogDay >= 0 && (_swappedToday > 0 || _bRolled > 0 || _stDay > 0))
                Log.Info("HouseLevies: dzien " + _lastLogDay + " - " + _swappedToday + " szlacheckich ochotnikow na ziemiach rodow to teraz ludzie rodu"
                         + " | zwykli tier 1 (536, House Recruit Share " + (Settings.Current != null ? Settings.Current.HouseRecruitShare.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) : "?") + "%): nowych z losem " + _bRolled
                         + ", na rekruta rodu " + _bSwapped + ", bez losu (osada nie rodu / rod bez tier 1) " + _bAbsorbed + ", zwerbowanych rozstrzygnietych " + _bRecruited
                         + ", notabli w pamieci " + _decided.Count + (_bEx.Count > 0 ? " [" + string.Join("; ", _bEx.ToArray()) + "]" : "")
                         + " | potkniecia doby " + _stDay + ", sesji " + _st + ".");
            _lastLogDay = day; _swappedToday = 0;
            _bRolled = _bSwapped = _bAbsorbed = _bRecruited = _stDay = 0; _bEx.Clear();
        }

        /// <summary>Cale drzewo szablonu rodu (stacki + wszystkie awanse), jak ROT TraverseTree.</summary>
        private List<CharacterObject> TreeOf(Clan clan)
        {
            List<CharacterObject> tree;
            if (_trees.TryGetValue(clan, out tree)) return tree;
            tree = new List<CharacterObject>();
            try
            {
                var tpl = clan.DefaultPartyTemplate;
                if (tpl != null && tpl.Stacks != null)
                {
                    var stack = new Stack<CharacterObject>();
                    foreach (var st in tpl.Stacks) if (st.Character != null) stack.Push(st.Character);
                    while (stack.Count > 0)
                    {
                        var c = stack.Pop();
                        if (c == null || tree.Contains(c)) continue;
                        tree.Add(c);
                        if (c.UpgradeTargets != null) foreach (var u in c.UpgradeTargets) stack.Push(u);
                    }
                }
            }
            catch (Exception e) { Log.Error("HouseLevies.TreeOf", e); }
            _trees[clan] = tree;
            return tree;
        }

        /// <summary>Szlachecki = siedzi w drzewie elite_basic_troop swojej kultury (jak ROT IsEliteTroop).</summary>
        private bool IsElite(CharacterObject troop)
        {
            bool v;
            if (_eliteCache.TryGetValue(troop, out v)) return v;
            v = false;
            try
            {
                var cult = troop.Culture;
                if (cult != null && cult.EliteBasicTroop != null)
                {
                    var stack = new Stack<CharacterObject>(); var seen = new HashSet<CharacterObject>();
                    stack.Push(cult.EliteBasicTroop);
                    while (stack.Count > 0)
                    {
                        var c = stack.Pop();
                        if (c == null || !seen.Add(c)) continue;
                        if (c == troop) { v = true; break; }
                        if (c.UpgradeTargets != null) foreach (var u in c.UpgradeTargets) stack.Push(u);
                    }
                }
            }
            catch { }
            _eliteCache[troop] = v;
            return v;
        }

        /// <summary>Czlowiek rodu tego samego tieru (albo o jeden nizej/wyzej), losowo z pasujacych.</summary>
        private static CharacterObject Pick(List<CharacterObject> tree, int tier, CultureObject culture)
        {
            for (int spread = 0; spread <= 1; spread++)
            {
                var cands = new List<CharacterObject>();
                foreach (var c in tree)
                    if (!c.IsHero && c.Culture == culture && Math.Abs(c.Tier - tier) == spread) cands.Add(c);
                if (cands.Count > 0) return cands[MBRandom.RandomInt(cands.Count)];
            }
            return null;
        }
    }
}
