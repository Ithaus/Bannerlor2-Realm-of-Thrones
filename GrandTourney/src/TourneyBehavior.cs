using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GrandTourney
{
    public class TourneyBehavior : CampaignBehaviorBase
    {
        public static TourneyBehavior Instance;

        // stan: "townId|proclaimedDay|state|hostFee"   state: 0 = zjazd, 1 = otwarty
        private List<string> _events = new List<string>();
        private List<string> _hostCooldowns = new List<string>();   // "townId|day"
        private List<string> _adoptSilence = new List<string>();     // "townId|day" - po odwolaniu nie przejmuj od razu

        public TourneyBehavior() { Instance = this; }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("gt_events", ref _events);
                dataStore.SyncData("gt_hostCooldowns", ref _hostCooldowns);
                dataStore.SyncData("gt_adoptSilence", ref _adoptSilence);
                if (_events == null) _events = new List<string>();
                if (_hostCooldowns == null) _hostCooldowns = new List<string>();
                if (_adoptSilence == null) _adoptSilence = new List<string>();
            }
            catch (Exception e) { Log.Error("SyncData", e); }
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.TournamentStarted.AddNonSerializedListener(this, OnTournamentStarted);
            CampaignEvents.TournamentFinished.AddNonSerializedListener(this, OnTournamentFinished);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            try { HostMenu.Add(starter); Log.Info("Menu ogloszenia turnieju dodane."); }
            catch (Exception e) { Log.Error("OnSessionLaunched", e); }
        }

        // ------------------------------------------------------------ stan
        private static float Today { get { return (float)CampaignTime.Now.ToDays; } }

        /// <summary>Czy wlasciciel miasta ma pokoj ze wszystkimi. W czasie wojny nikt nie bawi sie w turnieje.</summary>
        internal static bool IsAtPeace(Town town)
        {
            try
            {
                if (town == null) return false;
                var faction = town.Settlement.MapFaction;
                if (faction == null) return false;
                foreach (var other in Kingdom.All)
                {
                    if (other == null || other == faction) continue;
                    if (FactionManager.IsAtWarAgainstFaction(faction, other)) return false;
                }
                return true;
            }
            catch (Exception e) { Log.Error("IsAtPeace", e); return true; }
        }

        /// <summary>
        /// Wojna odwoluje turniej TYLKO, gdy naprawde stoi u bram: oblezenie,
        /// rebelia albo wrogi lord z wojskiem w poblizu. Westeros zawsze gdzies
        /// wojuje - stara zasada "pokoj ze wszystkimi" gasila kazde szranki
        /// (Castle Black nie zobaczylby turnieju nigdy: Nocna Straz wiecznie walczy).
        /// </summary>
        internal static bool WarAtTheGates(Town town)
        {
            try
            {
                if (town == null || town.Settlement == null) return false;
                var st = town.Settlement;
                if (st.IsUnderSiege || town.InRebelliousState) return true;
                var faction = st.MapFaction;
                if (faction == null) return false;
                float r = Math.Max(10f, Settings.Current.WarDangerRadius);
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || mp.MapFaction == null || !mp.IsLordParty) continue;
                    if (mp.MemberRoster == null || mp.MemberRoster.TotalHealthyCount < 30) continue;
                    if (!FactionManager.IsAtWarAgainstFaction(mp.MapFaction, faction)) continue;
                    if (mp.GetPosition2D.Distance(st.GetPosition2D) <= r) return true;
                }
                return false;
            }
            catch (Exception e) { Log.Error("WarAtTheGates", e); return false; }
        }

        /// <summary>Czy wiesc dociera do gracza. Za Waskim Morzem nikt o niczym nie slyszy.</summary>
        private static bool PlayerHearsOf(Town town, int prizeGold)
        {
            try
            {
                if (town == null) return false;
                if (town.OwnerClan == Clan.PlayerClan) return true;              // twoje miasto - zawsze wiesz
                if (Hero.MainHero.CurrentSettlement == town.Settlement) return true;
                var s = Settings.Current;
                float radius = s.PlayerNoticeRadius * (1f + prizeGold * s.PrizeRadiusBonus);
                float d = MobileParty.MainParty.GetPosition2D.Distance(town.Settlement.GetPosition2D);
                return d <= radius;
            }
            catch (Exception e) { Log.Error("PlayerHearsOf", e); return false; }
        }

        internal bool IsGathering(Town town)
        {
            var line = Find(town);
            return line != null && line.Split('|')[2] == "0";
        }

        private string Find(Town town)
        {
            if (town == null) return null;
            foreach (var e in _events)
                if (e.StartsWith(town.Settlement.StringId + "|")) return e;
            return null;
        }

        private void Remove(Town town)
        {
            var line = Find(town);
            if (line != null) _events.Remove(line);
        }

        // ------------------------------------------------------------ obwieszczenie
        private void OnTournamentStarted(Town town)
        {
            try
            {
                if (!Settings.Current.Enabled || town == null) return;
                if (Find(town) != null) return;
                if (Settings.Current.PeaceOnly && WarAtTheGates(town))
                {
                    var g = Campaign.Current.TournamentManager.GetTournamentGame(town);
                    if (g != null) Campaign.Current.TournamentManager.ResolveTournament(g, town);
                    Log.Info("Turniej w " + town.Name + " odwolany od razu - wrog u bram.");
                    return;
                }
                // krolestwo wojuje (choc wrog daleko): zadnego zjazdu i wzywania
                // lordow - turniej zostaje LOKALNY, listy otwarte, nagroda skromna
                if (Settings.Current.LocalWhenAtWar && RealmAtWar(town.Settlement.MapFaction))
                {
                    var gl = Campaign.Current.TournamentManager.GetTournamentGame(town);
                    if (gl != null) SetLocalPrize(gl);
                    Log.Info("Turniej w " + town.Name + " LOKALNY - krolestwo wojuje, bez heroldow.");
                    return;
                }
                Proclaim(town, 0);
            }
            catch (Exception e) { Log.Error("OnTournamentStarted", e); }
        }

        internal void Proclaim(Town town, int hostFeePaid)
        {
            try
            {
                var s = Settings.Current;
                _events.Add(town.Settlement.StringId + "|" + Today.ToString("0.##") + "|0|" + hostFeePaid);
                int invited = SummonLords(town, hostFeePaid);
                Log.Info("Turniej ogloszony w " + town.Name + ", wezwano " + invited + " lordow.");
                if (PlayerHearsOf(town, hostFeePaid))
                    Log.Player("A tourney is proclaimed at " + town.Name + ". " + invited +
                               " lords have been called. It opens in " + s.GatherDays + " days.");
            }
            catch (Exception e) { Log.Error("Proclaim", e); }
        }

        /// <summary>Rozsyla wezwania. Wojna ma pierwszenstwo przed zabawa.</summary>
        /// <summary>Krolestwo tego lorda z KIMKOLWIEK wojuje? Wojna to sluzba - herold go nie wola.</summary>
        private static bool RealmAtWar(TaleWorlds.CampaignSystem.IFaction f)
        {
            try
            {
                if (f == null) return false;
                foreach (var k in Kingdom.All)
                {
                    if (k == null || k == f || k.IsEliminated) continue;
                    if (FactionManager.IsAtWarAgainstFaction(f, k)) return true;
                }
                return false;
            }
            catch { return false; }
        }

        // Komu herold kazal jechac na turniej - zeby moc go PUSCIC, gdy wybuchnie
        // wojna. Bez tego lord z rozkazem "jedz do miasta" lazi jak zombie wokol
        // turnieju, zamiast bic sie za krolestwo (Sansa krazaca pod Winterfell).
        private readonly HashSet<string> _summoned = new HashSet<string>();

        /// <summary>
        /// Skromna nagroda LOKALNYCH szranek: losowy porzadny przedmiot do
        /// LocalPrizeMaxValue. Wielka sakiewka czeka na Grand Tournament w pokoju.
        /// </summary>
        private static void SetLocalPrize(TournamentGame game)
        {
            try
            {
                var s = Settings.Current;
                if (game == null) return;
                var pool = new List<ItemObject>();
                foreach (var it in TaleWorlds.CampaignSystem.Extensions.Items.All)
                {
                    if (it == null || it.NotMerchandise) continue;
                    if (!it.HasWeaponComponent && !it.HasArmorComponent) continue;
                    if (it.Value < 400 || it.Value > s.LocalPrizeMaxValue) continue;
                    pool.Add(it);
                }
                if (pool.Count == 0) return;
                var best = pool[MBRandom.RandomInt(pool.Count)];
                var prop = HarmonyLib.AccessTools.Property(typeof(TournamentGame), "Prize");
                if (prop != null && prop.CanWrite) prop.SetValue(game, best, null);
                else
                {
                    var f = HarmonyLib.AccessTools.Field(typeof(TournamentGame), "<Prize>k__BackingField");
                    if (f != null) f.SetValue(game, best);
                }
                Log.Info("Nagroda lokalna: " + best.Name + " (" + best.Value + ")");
            }
            catch (Exception e) { Log.Error("SetLocalPrize", e); }
        }

        /// <summary>Wezwani lordowie, ktorych krolestwo POSZLO NA WOJNE - rozkaz zdjety od reki.</summary>
        private void ReleaseWarBound()
        {
            try
            {
                if (_summoned.Count == 0) return;
                var drop = new List<string>();
                foreach (var id in _summoned)
                {
                    MobileParty mp = null;
                    foreach (var p in MobileParty.All)
                        if (p != null && p.StringId == id) { mp = p; break; }
                    if (mp == null || !mp.IsActive) { drop.Add(id); continue; }
                    if (!Settings.Current.SummonOnlyPeacefulLords) continue;
                    if (!RealmAtWar(mp.MapFaction)) continue;
                    try { mp.SetMoveModeHold(); } catch { }
                    drop.Add(id);
                    Log.Info("Herold puszcza " + (mp.LeaderHero != null ? mp.LeaderHero.Name.ToString() : id) +
                             " - jego krolestwo wojuje, sluzba przed szrankami.");
                }
                foreach (var id in drop) _summoned.Remove(id);
            }
            catch (Exception e) { Log.Error("ReleaseWarBound", e); }
        }

        private int SummonLords(Town town, int hostFeePaid)
        {
            int invited = 0;
            try
            {
                var s = Settings.Current;
                float baseRadius = s.InviteRadius * (1f + hostFeePaid * s.PrizeRadiusBonus);
                var hostFaction = town.Settlement.MapFaction;
                var pos = town.Settlement.GetPosition2D;

                // W rzadkiej okolicy - Polnoc, pustkowia, kresy - osmiu rycerzy w zwyklym promieniu
                // po prostu nie ma. Zamiast odwolywac kazdy turniej, heroldowie jada dalej.
                float radius = baseRadius;
                var candidates = new List<MobileParty>();
                int attempt = 0;
                while (true)
                {
                    candidates.Clear();
                    foreach (var mp in MobileParty.All)
                    {
                        if (mp == null || !mp.IsActive || mp.IsMainParty) continue;
                        var lord = mp.LeaderHero;
                        if (lord == null || !lord.IsAlive || !lord.IsLord || lord.IsWounded || lord.IsPrisoner) continue;

                        if (mp.Army != null) continue;                    // zebrany pod choragwia
                        if (mp.MapEvent != null) continue;                // w bitwie
                        if (mp.SiegeEvent != null) continue;              // oblega
                        if (mp.BesiegedSettlement != null) continue;      // oblezony
                        if (FactionManager.IsAtWarAgainstFaction(mp.MapFaction, hostFaction)) continue;
                        // wojna WLASNEGO krolestwa = sluzba; herold nie odciaga lorda
                        // od frontu na zabawe (to psulo AI: turniej wygrywal z wojna)
                        if (s.SummonOnlyPeacefulLords && RealmAtWar(mp.MapFaction)) continue;
                        if (mp.GetPosition2D.Distance(pos) > radius) continue;

                        candidates.Add(mp);
                    }

                    if (!s.WidenWhenScarce) break;
                    if (candidates.Count >= s.MinLordsToHold) break;
                    if (radius >= s.MaxInviteRadius) break;
                    if (++attempt > 12) break;
                    radius = MathF.Min(s.MaxInviteRadius, radius + s.WidenStep);
                }
                if (radius > baseRadius)
                    Log.Info("Rzadka okolica przy " + town.Name + " - promien rozszerzony " +
                             (int)baseRadius + " -> " + (int)radius);

                candidates.Sort((a, b) => a.GetPosition2D.Distance(pos).CompareTo(b.GetPosition2D.Distance(pos)));
                foreach (var mp in candidates)
                {
                    if (invited >= s.MaxLordsInvited) break;
                    mp.SetMoveGoToSettlement(town.Settlement, MobileParty.NavigationType.All, false);
                    _summoned.Add(mp.StringId);
                    invited++;
                }
                Log.Info("Kandydaci: " + candidates.Count + ", wezwano: " + invited + ", promien: " + (int)radius);
            }
            catch (Exception e) { Log.Error("SummonLords", e); }
            return invited;
        }

        // ------------------------------------------------------------ codzienny tick
        private const float AdoptSilenceDays = 6f;

        private bool AdoptSilenced(Settlement st)
        {
            foreach (var c in _adoptSilence)
            {
                var p = c.Split('|');
                if (p[0] == st.StringId && Today - float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture) < AdoptSilenceDays)
                    return true;
            }
            return false;
        }

        private void SilenceAdoption(Settlement st)
        {
            _adoptSilence.RemoveAll(x => x.StartsWith(st.StringId + "|", StringComparison.Ordinal));
            _adoptSilence.Add(st.StringId + "|" + Today.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>Czy ta osada jest juz pod nasza opieka.</summary>
        private bool Known(Settlement settlement)
        {
            if (settlement == null) return false;
            foreach (var line in _events)
                if (line.StartsWith(settlement.StringId + "|", StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// Turnieje zakladane przez gre i inne mody nie przechodza przez nasze oglaszanie,
        /// wiec dotad omijaly zasade minimum rycerzy - stad areny pelne rekrutow.
        /// Bierzemy je pod te same reguly: rozsylamy wezwania i dajemy czas na zjazd,
        /// a jesli nikt znaczacy nie przyjedzie, impreza zostaje odwolana.
        /// </summary>
        private void AdoptForeignTournaments()
        {
            try
            {
                var s = Settings.Current;
                if (!s.PoliceAllTournaments) return;
                var mgr = Campaign.Current.TournamentManager;
                if (mgr == null) return;

                foreach (var town in Town.AllTowns)
                {
                    if (town == null || town.Settlement == null) continue;
                    if (Known(town.Settlement)) continue;
                    if (AdoptSilenced(town.Settlement)) continue;
                    var game = mgr.GetTournamentGame(town);
                    if (game == null) continue;

                    // Wrog u bram - cudza impreze zdejmujemy OD RAZU, bez wzywania
                    // lordow i bez wpisu; inaczej tego samego dnia sami bysmy ja
                    // odwolali, a lordowie dostaliby rozkaz jazdy donikad.
                    if (s.PeaceOnly && WarAtTheGates(town))
                    {
                        mgr.ResolveTournament(game, town);
                        SilenceAdoption(town.Settlement);
                        Log.Info("Turniej w " + town.Name + " zdjety - wrog u bram.");
                        continue;
                    }
                    // krolestwo wojuje - nie robimy z tego zjazdu; zostaje impreza
                    // lokalna ze skromna nagroda, listy otwarte od reki
                    if (s.LocalWhenAtWar && RealmAtWar(town.Settlement.MapFaction))
                    {
                        SetLocalPrize(game);
                        SilenceAdoption(town.Settlement);
                        Log.Info("Turniej w " + town.Name + " przyjety jako LOKALNY - krolestwo wojuje.");
                        continue;
                    }

                    _events.Add(town.Settlement.StringId + "|" + Today.ToString("0.##") + "|0|0");
                    int called = SummonLords(town, 0);
                    Log.Info("Przejeto cudzy turniej w " + town.Name + ", wezwano rycerzy: " + called);
                    if (PlayerHearsOf(town, 0))
                        Log.Player("Word goes out from " + town.Name + ": a tourney is proclaimed. " +
                                   "The heralds ride for the knights of the region.");
                }
            }
            catch (Exception e) { Log.Error("AdoptForeignTournaments", e); }
        }

        private void OnDailyTick()
        {
            try
            {
                if (!Settings.Current.Enabled) return;
                var s = Settings.Current;
                ReleaseWarBound();
                AdoptForeignTournaments();
                var copy = new List<string>(_events);
                foreach (var line in copy)
                {
                    var p = line.Split('|');
                    var settlement = Settlement.Find(p[0]);
                    if (settlement == null || settlement.Town == null) { _events.Remove(line); continue; }
                    var town = settlement.Town;
                    var game = Campaign.Current.TournamentManager.GetTournamentGame(town);
                    if (game == null) { _events.Remove(line); continue; }

                    float proclaimed = float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
                    int state = int.Parse(p[2]);
                    int fee = int.Parse(p[3]);
                    if (state != 0) continue;

                    // wojna doszla pod mury, zanim goscie dojechali - impreza odwolana
                    if (s.PeaceOnly && WarAtTheGates(town))
                    {
                        _events.Remove(line);
                        Campaign.Current.TournamentManager.ResolveTournament(game, town);
                        if (fee > 0)
                        {
                            int back = (int)(fee * s.CancelledFeeRefund);
                            if (back > 0) GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, back);
                        }
                        if (PlayerHearsOf(town, fee))
                            Log.Player("War has broken out. The tourney at " + town.Name +
                                       " is called off before it began.", true);
                        SilenceAdoption(town.Settlement);
                        Log.Info("Turniej w " + town.Name + " odwolany - wybuchla wojna w trakcie zjazdu.");
                        continue;
                    }

                    // wojna wybuchla GOSPODARZOWI w trakcie zjazdu: lordowie jada na
                    // front (ReleaseWarBound juz ich puscil), a impreza nie umiera -
                    // schodzi do rangi lokalnej ze skromna nagroda
                    if (s.LocalWhenAtWar && RealmAtWar(town.Settlement.MapFaction))
                    {
                        _events.Remove(line);
                        SetLocalPrize(game);
                        if (fee > 0)
                        {
                            int back2 = (int)(fee * s.CancelledFeeRefund);
                            if (back2 > 0) GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, back2);
                        }
                        if (PlayerHearsOf(town, fee))
                            Log.Player("The lords ride to war. The tourney at " + town.Name +
                                       " goes on as a local affair with a modest prize.", true);
                        SilenceAdoption(town.Settlement);
                        Log.Info("Zjazd w " + town.Name + " przerwany wojna - turniej schodzi do LOKALNEGO.");
                        continue;
                    }

                    float elapsed = Today - proclaimed;
                    if (elapsed < s.GatherDays)
                    {
                        SummonLords(town, fee);      // podtrzymuj rozkazy, AI lubi je gubic
                        continue;
                    }

                    int lords = CountLordsPresent(town);
                    if (lords >= s.MinLordsToHold)
                    {
                        _events.Remove(line);
                        _events.Add(p[0] + "|" + p[1] + "|1|" + fee);
                        game.UpdateTournamentPrize(false, true);
                        Log.Info("Turniej w " + town.Name + " otwarty, rycerzy: " + lords);
                        if (PlayerHearsOf(town, fee))
                            Log.Player("The lists at " + town.Name + " are open. " + lords +
                                       " knights have answered the call. The prize has been set accordingly.");
                    }
                    else
                    {
                        // za malo rycerzy na Grand - ale miasto nie odwoluje zabawy:
                        // szranki zostaja jako impreza LOKALNA ze skromna nagroda
                        _events.Remove(line);
                        SetLocalPrize(game);
                        if (fee > 0)
                        {
                            int refund = (int)(fee * s.CancelledFeeRefund);
                            if (refund > 0) GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, refund);
                            Log.Player("Too few knights answered at " + town.Name + ". The tourney is held as a " +
                                       "local affair with a modest prize; " + refund + " gold was recovered.", true);
                        }
                        else if (PlayerHearsOf(town, 0))
                        {
                            Log.Player("Too few knights answered at " + town.Name +
                                       ". The tourney is held as a local affair with a modest prize.", true);
                        }
                        SilenceAdoption(town.Settlement);
                        Log.Info("Turniej w " + town.Name + " LOKALNY - rycerzy tylko " + lords);
                    }
                }
            }
            catch (Exception e) { Log.Error("OnDailyTick", e); }
        }

        private int CountLordsPresent(Town town)
        {
            int n = 0;
            try
            {
                foreach (var mp in town.Settlement.Parties)
                {
                    var h = mp.LeaderHero;
                    if (h != null && h.IsLord && !h.IsWounded && h != Hero.MainHero) n++;
                }
                foreach (var h in town.Settlement.HeroesWithoutParty)
                    if (h != null && h.IsLord && !h.IsWounded && h != Hero.MainHero) n++;
            }
            catch (Exception e) { Log.Error("CountLordsPresent", e); }
            return n;
        }

        // ------------------------------------------------------------ nagrody dla gospodarza
        private void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
        {
            try
            {
                Remove(town);
                if (town == null || town.OwnerClan != Clan.PlayerClan) return;
                var s = Settings.Current;

                int lords = 0;
                foreach (var c in participants)
                    if (c != null && c.IsHero && c.HeroObject != Hero.MainHero && c.HeroObject.IsLord) lords++;
                if (lords <= 0) return;

                var clan = Clan.PlayerClan;
                clan.Renown += lords * s.HostRenownPerLord;
                if (clan.Kingdom != null) ChangeClanInfluenceAction.Apply(clan, lords * s.HostInfluencePerLord);

                foreach (var c in participants)
                    if (c != null && c.IsHero && c.HeroObject != Hero.MainHero && c.HeroObject.IsLord)
                        ChangeRelationAction.ApplyPlayerRelation(c.HeroObject, s.HostRelationPerLord);

                town.Prosperity += lords * s.HostProsperityPerLord;
                town.Loyalty = MathF.Min(100f, town.Loyalty + s.HostLoyaltyGain);
                town.Security = MathF.Max(0f, town.Security - s.HostSecurityLoss);

                int takings = lords * s.HostTakingsPerLord + (int)(town.Prosperity * s.HostTakingsProsperityFactor);
                if (takings > 0) GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, takings);

                Log.Info("Gospodarz rozliczony: lordow " + lords + ", reputacja +" + lords * s.HostRenownPerLord + ", utarg " + takings);
                Log.Player("The tourney at " + town.Name + " is ended. " + lords + " lords rode in your lists: +" +
                           (lords * s.HostRenownPerLord) + " renown, +" + (lords * s.HostProsperityPerLord) +
                           " prosperity, and " + takings + " gold in takings.");
            }
            catch (Exception e) { Log.Error("OnTournamentFinished", e); }
        }

        // ------------------------------------------------------------ ogloszenie wlasnego
        internal bool CanHost(Town town, out string reason)
        {
            reason = "";
            var s = Settings.Current;
            if (!s.PlayerHostingEnabled) { reason = "disabled"; return false; }
            if (town == null || town.OwnerClan != Clan.PlayerClan) { reason = "You do not hold this town."; return false; }
            if (Clan.PlayerClan.Renown < s.HostMinRenown) { reason = "Your name is not yet great enough (" + s.HostMinRenown + " renown needed)."; return false; }
            if (Campaign.Current.TournamentManager.GetTournamentGame(town) != null) { reason = "A tourney is already afoot here."; return false; }
            if (s.PeaceOnly && WarAtTheGates(town)) { reason = "The enemy is at the gates - no tourney while war stands this close."; return false; }
            foreach (var cd in _hostCooldowns)
            {
                var p = cd.Split('|');
                if (p[0] != town.Settlement.StringId) continue;
                float last = float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
                if (Today - last < s.HostCooldownDays)
                {
                    reason = "You held a tourney here too recently.";
                    return false;
                }
            }
            return true;
        }

        internal int HostFee(Town town)
        {
            var s = Settings.Current;
            return s.HostBaseFee + (int)(town.Prosperity * s.HostFeeProsperityFactor);
        }

        internal void HostTournament(Town town, int prizeGold)
        {
            try
            {
                int fee = HostFee(town) + prizeGold;
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, fee);

                var game = Campaign.Current.Models.TournamentModel.CreateTournament(town);
                Campaign.Current.TournamentManager.AddTournament(game);

                _hostCooldowns.RemoveAll(x => x.StartsWith(town.Settlement.StringId + "|"));
                _hostCooldowns.Add(town.Settlement.StringId + "|" + Today.ToString("0.##"));

                Remove(town);
                Proclaim(town, prizeGold);
                Log.Info("Gracz oglosil turniej w " + town.Name + ", koszt " + fee);
                Log.Player("You have proclaimed a tourney at " + town.Name + ". It cost you " + fee + " gold.");
            }
            catch (Exception e) { Log.Error("HostTournament", e); }
        }
    }
}
