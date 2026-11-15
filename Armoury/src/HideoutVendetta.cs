using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 570 D: POSCIG ZA SKARBEM (Jeff, czat 15.11, "ABCDE tak rob" - punkt D: "poscig za skarbem: tylko bandy tego samego klanu/kryjowki, przez kilka dni
    /// gdy niesiesz skarb, promien rosnie, slabsi sledza i atakuja gdy oslabniesz (po bitwie, noca w obozie), bardzo slabi odpuszczaja; nic z niczego,
    /// ochrona bohaterow, przerwa po poscigu").
    ///
    /// Start: gracz PRZESZUKAL kryjowke (HideoutPurge.DoSearch) i zabral skarb (zloto kasy kryjowki + towar magazynu 568, wartosc bazowa) co najmniej
    /// HideoutVendettaMinGold. Bez poscigu: klucz off, skarb ponizej progu, trwa inny poscig, przerwa po poprzednim (HideoutVendettaCooldownDays),
    /// nieznany klan i kryjowka. Odwet z bitwy (HideoutPurge.Reprisal) przechodzi w poscig (HandOverReprisal) - jeden zarzadca band.
    ///
    /// Co godzine (tylko gdy poscig trwa - inaczej sam test flagi): bandy klanu kryjowki albo z ta kryjowka jako domem (HideoutPurge.Kin) w promieniu
    /// od gracza HideoutVendettaRadius + HideoutVendettaRadiusPerDay x pelne doby poscigu; bez band w osadach, bitwach, zadaniach, nieumarlych.
    /// Przewodnik = najwieksza banda bez bohatera (HideoutPurge.Mergeable); bandy bez bohatera, ktore go dogonily (MergeDist), scalaja sie w niego
    /// (HideoutPurge.MergeInto - ludzie, jency, towar, kiesa, sakiewka ludzi; nic nie powstaje), reszta idzie za nim (eskorta). Bandy z bohaterem
    /// (BK) i eskorty bohatera: nigdy scalane, bez naszych rozkazow (ochrona bohaterow, ida wlasnym rozumem) i poza rachunkiem sil (recenzja 570).
    /// Decyzja (sila szacunkowa gry, PartyBase.EstimatedStrength): gracz = jego partia + partie jego frakcji w AllyRange; oslabiony (bitwa w ostatnich
    /// WeakHours h, ranni >= WeakWounded skladu, oboz / sen NightRest) - liczy sie x HideoutVendettaWeakFactor.
    ///  - wszyscy razem / gracz < HideoutVendettaGiveUpOdds -> odpuszczaja (koniec);
    ///  - zebrani przy przewodniku (GatherDist) / gracz >= HideoutVendettaAttackOdds -> atak (SetMoveEngageParty na gracza);
    ///  - inaczej sledza: przewodnik trzyma sie 3-6 jednostek od gracza (ShadowMin/Max), reszta sciaga do niego.
    /// Gracz w osadzie, przy lordzie (zaciag / armia), na morzu - bez ataku (sledza). Gracz w niewoli - koniec.
    /// Wygrana hordy z graczem: zabieraja najwyzej min(skarb, zloto gracza przed bitwa) MINUS to, co gracz juz stracil w tej bitwie (lup gry
    /// MapEvent.CalculatePlunderedAndLostGoldAmounts -> MapEventParty.CommitGoldChanges: GoldLost = 5% zlota wodza; A6 BattlePlunder; zabor przy
    /// pojmaniu RealisticCaptivity) - roznica zlota gracza od startu bitwy (MapEventStarted) do jej konca (MapEventEnded), wiec nic dwa razy.
    /// Przelew GiveGoldAction gracz -> zwycieska banda (kiesa). Potem koniec poscigu. Koniec (czas, odpuszczenie, odzyskanie, niewola) -> przerwa.
    /// Zapis: klucz "arm_570_vendetta" (SaveText.Sync, ArmouryBehavior). Stan statyczny czyszczony w Reset (konstruktor ArmouryBehavior).
    /// </summary>
    internal static class HideoutVendetta
    {
        private const float MergeDist = 1.5f;      // banda, ktora dogonila przewodnika, scala sie w niego
        private const float GatherDist = 4f;       // zebrani przy przewodniku - ta sila decyduje o ataku
        private const float ShadowMin = 3f, ShadowMax = 6f, ShadowAt = 4.5f;
        private const float AllyRange = 6f;        // sojusznicy gracza liczeni do jego sily
        private const double WeakHours = 12.0;     // po bitwie gracz jest oslabiony tyle godzin
        private const float WeakWounded = 0.3f;    // ranni / sklad
        private const float AttackHold = 0.8f;     // recenzja 570: trwajacy atak przerywany dopiero ponizej AttackOdds x AttackHold (bez migotania co godzine)

        private static bool _active;
        private static string _clanId = "", _homeId = "", _homeName = "";
        private static Clan _clan;
        private static Settlement _home;
        private static double _startH, _untilH, _coolH = -1, _lastBattleH = -1;
        private static int _stash;
        private static string _leadId = "";
        private static MobileParty _lead;
        private static int _phase;   // 0 nic, 1 sledza, 2 atak
        private static bool _shadowSaid;
        private static double _attackMsgH = -1;   // nie zapisywane - po wczytaniu komunikat moze pasc raz wiecej
        private static readonly HashSet<MobileParty> _ordered = new HashSet<MobileParty>();
        private static int _goldAtBattle = -1;
        private static MapEvent _battle;
        // liczniki poscigu (linia konca)
        private static int _hours, _maxBands, _merges, _mergedMen, _attacks, _won, _lost, _stumbles;
        private static int _paidBack;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        private static Settings S { get { return Settings.Current; } }
        private static double NowH { get { return CampaignTime.Now.ToHours; } }

        internal static void Reset()
        {
            _active = false; _clanId = ""; _homeId = ""; _homeName = ""; _clan = null; _home = null;
            _startH = _untilH = 0; _coolH = -1; _lastBattleH = -1; _stash = 0; _leadId = ""; _lead = null; _phase = 0; _shadowSaid = false; _attackMsgH = -1;
            _ordered.Clear(); _goldAtBattle = -1; _battle = null;
            _hours = _maxBands = _merges = _mergedMen = _attacks = _won = _lost = _stumbles = 0; _paidBack = 0;
            _errSites.Clear();
        }

        /// <summary>Banda dostala w tym poscigu rozkaz (NightRest.AiBanditRest - nie kladzie jej na drzemke).</summary>
        internal static bool Hunting(MobileParty mp) { return _active && mp != null && _ordered.Contains(mp); }

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errSites.Add(where)) Log.Error("HideoutVendetta." + where, e); } catch { }
        }

        // ------------------------------------------------------------ start

        /// <summary>HideoutPurge.DoSearch: gracz zabral skarb kryjowki (zloto + wartosc towaru).</summary>
        internal static void Start(Settlement hideout, Clan clan, int gold, long goods)
        {
            var s = S;
            if (s == null || !s.HideoutVendetta) return;
            long stash = Math.Max(0, gold) + Math.Max(0L, goods);
            string name = hideout != null && hideout.Name != null ? hideout.Name.ToString() : "-";
            if (stash < Math.Max(0, s.HideoutVendettaMinGold))
            {
                Log.Info("Poscig za skarbem (570): " + name + " - skarb " + stash + " zl ponizej progu " + s.HideoutVendettaMinGold + " - bez poscigu.");
                return;
            }
            if (_active) { Log.Info("Poscig za skarbem (570): " + name + " - trwa juz poscig band " + _homeName + " - bez drugiego."); return; }
            if (_coolH >= 0 && NowH < _coolH)
            {
                Log.Info("Poscig za skarbem (570): " + name + " - przerwa po poprzednim poscigu jeszcze " + (_coolH - NowH).ToString("0", CultureInfo.InvariantCulture) + " h - bez poscigu.");
                return;
            }
            if (clan == null && hideout == null) { Log.Info("Poscig za skarbem (570): nieznany klan i kryjowka - bez poscigu."); return; }
            _active = true;
            _clan = clan; _clanId = clan != null ? clan.StringId : "";
            _home = hideout; _homeId = hideout != null ? hideout.StringId : ""; _homeName = name;
            _startH = NowH;
            _untilH = _startH + 24.0 * Math.Max(0.25f, s.HideoutVendettaDays);
            _stash = (int)Math.Min(int.MaxValue, stash);
            _lead = null; _leadId = ""; _phase = 0; _shadowSaid = false; _attackMsgH = -1; _ordered.Clear(); _goldAtBattle = -1; _battle = null;
            _hours = _maxBands = _merges = _mergedMen = _attacks = _won = _lost = _stumbles = 0; _paidBack = 0;
            int taken = 0;
            try { taken = HideoutPurge.HandOverReprisal(); } catch (Exception e) { Stumble("HandOver", e); }
            Log.Player("Word of the plundered den runs ahead of you - its bands will hunt their hoard for days.", true);
            Log.Info("Poscig za skarbem (570): start - kryjowka " + name + " (" + _homeId + "), klan " + (_clanId.Length > 0 ? _clanId : "-")
                     + ", skarb " + _stash + " zl (zloto " + gold + " + towar " + goods + "), " + s.HideoutVendettaDays.ToString("0.#", CultureInfo.InvariantCulture)
                     + " dni, promien " + s.HideoutVendettaRadius.ToString("0", CultureInfo.InvariantCulture) + " + " + s.HideoutVendettaRadiusPerDay.ToString("0", CultureInfo.InvariantCulture)
                     + " na dobe, atak od " + s.HideoutVendettaAttackOdds.ToString("0.##", CultureInfo.InvariantCulture) + ":1, odpuszczaja ponizej "
                     + s.HideoutVendettaGiveUpOdds.ToString("0.##", CultureInfo.InvariantCulture) + ":1; odwet przejety: " + taken + " partii.");
        }

        // ------------------------------------------------------------ godzina

        internal static void Hourly()
        {
            if (!_active) return;
            try { HourlyCore(); }
            catch (Exception e) { Stumble("Hourly", e); }
        }

        private static void HourlyCore()
        {
            var s = S;
            if (s == null || !s.HideoutVendetta) { End("klucz HideoutVendetta wylaczony", null); return; }
            var hero = Hero.MainHero;
            var me = MobileParty.MainParty;
            if (hero == null || !hero.IsAlive || hero.IsPrisoner || me == null || !me.IsActive) { End("gracz w niewoli albo poza mapa", null); return; }
            if (NowH >= _untilH) { End("czas minal", "The hunt for the den's hoard has gone cold."); return; }
            if (me.MapEvent != null) return;   // bitwa trwa - rozliczy MapEventEnded
            Resolve();
            _hours++;
            float radius = Math.Max(1f, s.HideoutVendettaRadius) + Math.Max(0f, s.HideoutVendettaRadiusPerDay) * (float)Math.Floor((NowH - _startH) / 24.0);
            Vec2 at = me.GetPosition2D;
            var bands = new List<MobileParty>();
            float allies = 0f;
            foreach (var mp in MobileParty.All)
            {
                try
                {
                    if (mp == null || !mp.IsActive || mp.IsMainParty) continue;
                    float d = mp.GetPosition2D.Distance(at);
                    if (mp.IsBandit)
                    {
                        if (d > radius || mp.CurrentSettlement != null || mp.MapEvent != null) continue;
                        if (mp.IsCurrentlyUsedByAQuest || Undead.Party(mp)) continue;
                        if (mp.MemberRoster == null || mp.MemberRoster.TotalManCount <= 0) continue;
                        if (!HideoutPurge.Kin(mp, _clan, _home)) continue;
                        if (HideoutPurge.InReprisal(mp)) continue;   // horda odwetu innej kryjowki - prowadzi ja odwet
                        bands.Add(mp);
                    }
                    // recenzja 570: tylko partie wojskowe (lordowie / druzyny klanu) - bez karawan i chlopow przejezdzajacych obok
                    else if (d <= AllyRange && mp.IsLordParty && mp.MapEvent == null && mp.CurrentSettlement == null && me.MapFaction != null && mp.MapFaction == me.MapFaction)
                        allies += mp.Party.EstimatedStrength;
                }
                catch (Exception e) { Stumble("Scan", e); }
            }
            if (bands.Count > _maxBands) _maxBands = bands.Count;
            // przewodnik: dotychczasowy, jesli nadal w gronie i bez bohatera; inaczej najwieksza banda bez bohatera
            if (_lead != null && (!bands.Contains(_lead) || !HideoutPurge.Mergeable(_lead))) _lead = null;
            if (_lead == null)
                foreach (var mp in bands)
                    if (HideoutPurge.Mergeable(mp) && (_lead == null || mp.MemberRoster.TotalManCount > _lead.MemberRoster.TotalManCount)) _lead = mp;
            _leadId = _lead != null ? _lead.StringId : "";
            if (_lead == null)
            {
                // nikogo do prowadzenia (brak band albo same z bohaterem) - bohaterow nie rzucamy, bandy bez rozkazu wracaja do swoich spraw
                Release();
                if (_phase != 0) { _phase = 0; Log.Info("Poscig za skarbem (570): godzina " + _hours + " - w promieniu " + radius.ToString("0", CultureInfo.InvariantCulture) + " brak przewodnika (band " + bands.Count + ")."); }
                return;
            }
            // scalenie band, ktore dogonily przewodnika
            for (int i = bands.Count - 1; i >= 0; i--)
            {
                var mp = bands[i];
                if (mp == _lead || !HideoutPurge.Mergeable(mp)) continue;
                if (mp.GetPosition2D.Distance(_lead.GetPosition2D) > MergeDist) continue;
                try
                {
                    int men = HideoutPurge.MergeInto(_lead, mp);
                    _merges++; _mergedMen += men;
                    _ordered.Remove(mp);
                    bands.RemoveAt(i);
                }
                catch (Exception e) { Stumble("Merge", e); }
            }
            // sily
            // recenzja 570: bohaterowie i ich eskorty nigdy nie gonia (bez rozkazow) - poza rachunkiem "odpuszczaja" i ataku, tylko do logu
            float sAll = 0f, sNear = 0f, sHero = 0f;
            Vec2 lp = _lead.GetPosition2D;
            foreach (var mp in bands)
            {
                float st = 0f;
                try { st = mp.Party.EstimatedStrength; } catch { }
                if (mp.LeaderHero != null || HideoutPurge.HeroEscort(mp)) { sHero += st; continue; }
                sAll += st;
                if (mp.GetPosition2D.Distance(lp) <= GatherDist) sNear += st;
            }
            float mine = 0f;
            try { mine = me.Party.EstimatedStrength; } catch { }
            float player = mine + allies;
            string weakWhy = WeakWhy(me);
            float pe = Math.Max(1f, player * (weakWhy != null ? Math.Max(0.05f, s.HideoutVendettaWeakFactor) : 1f));
            float all = sAll / pe, near = sNear / pe;
            if (all < Math.Max(0f, s.HideoutVendettaGiveUpOdds))
            {
                End("odpuszczaja - sila wszystkich " + (int)sAll + " vs gracz " + (int)pe, "The bands of the plundered den lose heart and scatter - the hoard is yours.");
                return;
            }
            bool canHit = me.CurrentSettlement == null && me.AttachedTo == null && !me.IsCurrentlyAtSea && !NightRest.EnlistedNow;
            float odds = Math.Max(0.1f, s.HideoutVendettaAttackOdds);
            bool attack = canHit && near >= (_phase == 2 ? odds * AttackHold : odds);   // histereza: wejscie od odds, wyjscie ponizej odds x AttackHold
            int phase = attack ? 2 : 1;
            foreach (var mp in bands)
            {
                try
                {
                    // bohater (BK) i jego eskorta: sojusznicy w sile poscigu, bez naszych rozkazow - BK SetFollow kaze bandom przy bohaterze isc za nim,
                    // rozkaz "bohater za przewodnikiem" dawalby wzajemna eskorte (stoja w miejscu); bohatera nigdy nie rzucamy do ataku (ochrona bohaterow)
                    if (mp.LeaderHero != null || HideoutPurge.HeroEscort(mp)) continue;
                    if (mp == _lead) { if (attack) Engage(mp, me); else Shadow(mp, me); continue; }
                    if (attack && mp.GetPosition2D.Distance(lp) <= GatherDist) Engage(mp, me);
                    else Escort(mp, _lead);
                }
                catch (Exception e) { Stumble("Order", e); }
            }
            // bandy z rozkazem, ktore wypadly z grona (poza promieniem, w osadzie, bohater je przejal, rozbite) - wracaja do swoich spraw
            if (_ordered.Count > 0)
            {
                var drop = new List<MobileParty>();
                foreach (var mp in _ordered)
                    if (mp == null || !mp.IsActive || !bands.Contains(mp) || (mp != _lead && (mp.LeaderHero != null || HideoutPurge.HeroEscort(mp)))) drop.Add(mp);
                foreach (var mp in drop)
                {
                    _ordered.Remove(mp);
                    try { if (mp != null && mp.IsActive && !HideoutPurge.HeroEscort(mp)) { mp.Ai.SetInitiative(1f, 1f, 1f); mp.Ai.RethinkAtNextHourlyTick = true; } } catch { }
                }
            }
            if (phase != _phase)
            {
                if (phase == 2)
                {
                    _attacks++;
                    // komunikat najwyzej co 12 h gry (faza moze skakac atak <-> sledzenie z godziny na godzine)
                    if (_attackMsgH < 0 || NowH - _attackMsgH >= 12.0) { _attackMsgH = NowH; Log.Player("The bands of the plundered den close in - they mean to take their hoard back!", true); }
                }
                else if (!_shadowSaid)
                {
                    _shadowSaid = true;
                    Log.Player("Riders of the plundered den keep pace at a distance - waiting for you to falter.", true);
                }
                Log.Info("Poscig za skarbem (570): godzina " + _hours + " - " + (phase == 2 ? "ATAK" : "sledza") + "; promien " + radius.ToString("0", CultureInfo.InvariantCulture)
                         + ", band " + bands.Count + ", przewodnik " + _lead.StringId + " (" + _lead.MemberRoster.TotalManCount + " ludzi, " + _lead.GetPosition2D.Distance(at).ToString("0.0", CultureInfo.InvariantCulture)
                         + " od gracza); sila zebranych " + (int)sNear + ", wszystkich " + (int)sAll + " (bohaterowie / eskorty poza rachunkiem " + (int)sHero + ") vs gracz " + (int)mine + " + sojusznicy " + (int)allies
                         + (weakWhy != null ? " (oslabiony: " + weakWhy + ", liczony " + (int)pe + ")" : "") + (canHit ? "" : " (gracz poza zasiegiem ataku)")
                         + "; scalen dotad " + _merges + ".");
                _phase = phase;
            }
        }

        /// <summary>Powod oslabienia gracza albo null: bitwa w ostatnich WeakHours h, ranni, oboz / sen.</summary>
        private static string WeakWhy(MobileParty me)
        {
            try
            {
                if (_lastBattleH >= 0 && NowH - _lastBattleH < WeakHours) return "po bitwie";
                var r = me.MemberRoster;
                if (r != null && r.TotalManCount > 0 && r.TotalWounded >= WeakWounded * r.TotalManCount) return "ranni " + r.TotalWounded + "/" + r.TotalManCount;
                if (NightRest.PlayerCamped || NightRest.SleepingNow) return "oboz";
            }
            catch { }
            return null;
        }

        private static void Engage(MobileParty mp, MobileParty me)
        {
            mp.Ai.SetInitiative(2f, 0.05f, 2f);
            mp.SetMoveEngageParty(me, MobileParty.NavigationType.Default);
            _ordered.Add(mp);
        }

        private static void Escort(MobileParty mp, MobileParty lead)
        {
            if (mp == lead) return;
            mp.Ai.SetInitiative(0.3f, 1f, 2f);
            mp.SetMoveEscortParty(lead, MobileParty.NavigationType.Default, false);
            _ordered.Add(mp);
        }

        /// <summary>Przewodnik trzyma sie ShadowMin-ShadowMax od gracza: za daleko - podchodzi do punktu ShadowAt od gracza po swojej stronie,
        /// za blisko - odchodzi do tego punktu (punkt poza siatka mapy - stoi), w pasie - stoi. Inicjatywa ataku niska (nie zaczepia), unikania zwykla
        /// (AI walczy o zycie - ucieka, gdy gracz rusza na niego).</summary>
        private static void Shadow(MobileParty mp, MobileParty me)
        {
            mp.Ai.SetInitiative(0.1f, 1f, 2f);
            _ordered.Add(mp);
            // recenzja 570: gracz na morzu - ladowej bandy nie wysylamy na punkt na wodzie, czeka na brzegu
            if (me.IsCurrentlyAtSea) { mp.SetMoveModeHold(); return; }
            Vec2 a = me.GetPosition2D, b = mp.GetPosition2D;
            float d = b.Distance(a);
            if (d >= ShadowMin && d <= ShadowMax) { mp.SetMoveModeHold(); return; }
            Vec2 dir = d > 0.01f ? (b - a) * (1f / d) : new Vec2(1f, 0f);
            var pt = new CampaignVec2(a + dir * ShadowAt, true);
            bool ok = false;
            try { ok = pt.Face.IsValid(); } catch { }
            if (ok) mp.SetMoveGoToPoint(pt, MobileParty.NavigationType.Default);
            else if (d > ShadowMax) mp.SetMoveGoToPoint(me.Position, MobileParty.NavigationType.Default);
            else mp.SetMoveModeHold();
        }

        private static void Release()
        {
            foreach (var mp in _ordered)
                try { if (mp != null && mp.IsActive) { mp.Ai.SetInitiative(1f, 1f, 1f); mp.Ai.RethinkAtNextHourlyTick = true; } } catch { }
            _ordered.Clear();
        }

        private static void End(string why, string msg)
        {
            Release();
            _active = false;
            var s = S;
            _coolH = NowH + 24.0 * Math.Max(0f, s != null ? s.HideoutVendettaCooldownDays : 10f);
            if (msg != null) Log.Player(msg);
            Log.Info("Poscig za skarbem (570): koniec (" + why + ") - kryjowka " + _homeName + ", skarb " + _stash + " zl; godzin " + _hours
                     + ", najwiecej band " + _maxBands + ", scalen " + _merges + " (" + _mergedMen + " ludzi), atakow " + _attacks
                     + ", bitwy z graczem: wygrane " + _won + " / przegrane " + _lost + ", odebrali " + _paidBack + " zl; potkniecia " + _stumbles
                     + "; przerwa do " + (s != null ? s.HideoutVendettaCooldownDays.ToString("0.#", CultureInfo.InvariantCulture) : "10") + " dni.");
            _lead = null; _leadId = ""; _phase = 0; _goldAtBattle = -1; _battle = null;
        }

        /// <summary>Po wczytaniu zapisu klan, kryjowka i przewodnik sa napisami - odnajdywane leniwie przy pierwszej godzinie.</summary>
        private static void Resolve()
        {
            if (_clan == null && _clanId.Length > 0)
                foreach (var c in Clan.All) if (c != null && c.StringId == _clanId) { _clan = c; break; }
            if (_home == null && _homeId.Length > 0) _home = Settlement.Find(_homeId);
            if (_lead == null && _leadId.Length > 0)
                foreach (var mp in MobileParty.All) if (mp != null && mp.StringId == _leadId) { _lead = mp; break; }
        }

        // ------------------------------------------------------------ bitwy gracza

        /// <summary>MapEventStarted: zloto gracza na poczatku jego bitwy (do odebrania skarbu bez podwojenia lupu gry).</summary>
        internal static void OnMapEventStarted(MapEvent ev, PartyBase attacker, PartyBase defender)
        {
            if (!_active) return;
            try
            {
                if (attacker != PartyBase.MainParty && defender != PartyBase.MainParty) return;
                _battle = ev;
                _goldAtBattle = Hero.MainHero != null ? Hero.MainHero.Gold : -1;
            }
            catch (Exception e) { Stumble("Started", e); }
        }

        /// <summary>MapEventEnded: bitwa gracza. Oslabienie (zegar), wynik z banda poscigu; wygrana hordy - odebranie skarbu i koniec.</summary>
        internal static void OnMapEventEnded(MapEvent ev)
        {
            if (!_active) return;
            try
            {
                if (ev == null || !ev.IsPlayerMapEvent) return;
                _lastBattleH = NowH;
                bool ours = ReferenceEquals(ev, _battle);
                int g0 = ours ? _goldAtBattle : -1;
                _battle = null; _goldAtBattle = -1;
                if (!ev.HasWinner) return;
                MobileParty winner = null;
                bool vsHorde = false;
                foreach (var side in new[] { BattleSideEnum.Attacker, BattleSideEnum.Defender })
                {
                    if (side == ev.PlayerSide) continue;
                    var ms = ev.GetMapEventSide(side);
                    if (ms == null || ms.Parties == null) continue;
                    foreach (var p in ms.Parties)
                    {
                        var mp = p != null && p.Party != null ? p.Party.MobileParty : null;
                        if (mp == null || !mp.IsBandit || !HideoutPurge.Kin(mp, _clan, _home)) continue;
                        vsHorde = true;
                        if (winner == null && mp.IsActive && side == ev.WinningSide) winner = mp;
                    }
                }
                if (!vsHorde) return;
                if (ev.WinningSide == ev.PlayerSide)
                {
                    _won++;
                    Log.Info("Poscig za skarbem (570): gracz rozbil bande poscigu (bitwa " + (_won + _lost) + ").");
                    return;
                }
                _lost++;
                int took = 0, lost = 0, want = 0;
                var hero = Hero.MainHero;
                if (g0 >= 0 && hero != null && winner != null)
                {
                    lost = Math.Max(0, g0 - hero.Gold);                 // lup gry (GoldLost), A6, zabor przy pojmaniu - juz zeszlo
                    want = Math.Min(_stash, g0);
                    int extra = Math.Min(hero.Gold, want - lost);
                    if (extra > 0)
                    {
                        GiveGoldAction.ApplyForCharacterToParty(hero, winner.Party, extra, true);
                        took = extra;
                        _paidBack += extra;
                        Log.Player("The bandits strip your purse of " + extra + " denars - their hoard, taken back.", true);
                    }
                }
                Log.Info("Poscig za skarbem (570): horda wygrala z graczem - skarb " + _stash + ", zloto gracza przed bitwa " + g0 + ", stracil w bitwie " + lost
                         + ", do odebrania min(skarb, zloto) " + want + " -> odebrali dodatkowo " + took + " zl do " + (winner != null ? winner.StringId : "-")
                         + (g0 < 0 ? " (bez migawki zlota - gracz dolaczyl do cudzej bitwy, nic dodatkowo)" : "") + ".");
                End("odzyskali skarb", null);
            }
            catch (Exception e) { Stumble("Ended", e); }
        }

        // ------------------------------------------------------------ zapis

        /// <summary>"v1|aktywny|klan|kryjowka|nazwa|startH|doH|skarb|przewodnik|przerwaDoH|ostatniaBitwaH|faza|komunikat".</summary>
        internal static string Export()
        {
            var ci = CultureInfo.InvariantCulture;
            if (!_active && _coolH < 0) return null;
            return "v1|" + (_active ? "1" : "0") + "|" + _clanId + "|" + _homeId + "|" + (_homeName ?? "").Replace("|", "/") + "|" + _startH.ToString("R", ci) + "|" + _untilH.ToString("R", ci)
                   + "|" + _stash.ToString(ci) + "|" + (_lead != null ? _lead.StringId : _leadId) + "|" + _coolH.ToString("R", ci) + "|" + _lastBattleH.ToString("R", ci)
                   + "|" + _phase.ToString(ci) + "|" + (_shadowSaid ? "1" : "0");
        }

        internal static void Import(string v)
        {
            try
            {
                if (string.IsNullOrEmpty(v)) return;
                var f = v.Split('|');
                if (f.Length < 13 || f[0] != "v1") return;
                var ci = CultureInfo.InvariantCulture;
                _active = f[1] == "1";
                _clanId = f[2]; _homeId = f[3]; _homeName = f[4];
                _startH = double.Parse(f[5], ci); _untilH = double.Parse(f[6], ci);
                _stash = int.Parse(f[7], ci);
                _leadId = f[8];
                _coolH = double.Parse(f[9], ci); _lastBattleH = double.Parse(f[10], ci);
                _phase = int.Parse(f[11], ci); _shadowSaid = f[12] == "1";
                _clan = null; _home = null; _lead = null;
            }
            catch (Exception e) { Stumble("Import", e); Reset(); }
        }
    }
}
