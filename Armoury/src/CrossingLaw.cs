using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// PRAWO PRZEPRAWY (Jeff 30.08, screen spod Twins: "jak to mozliwe, ze
    /// jako wroga armia moge przejsc przez Twins?! Ten, kto ma zamek,
    /// przepuszcza sojusznikow, a wrogow nie!"). Bliznaki to JEDYNY most
    /// przez Zielona Galaz - Freyowie zyli z bramkowania przeprawy, a gra
    /// traktuje most jak zwykly teren. Odtad: partia GRACZA w stanie WOJNY
    /// z wlascicielem przeprawy nie wjedzie w jej strefe - straz mostu
    /// wypycha ja tam, skad przyszla (SetMoveGoToPoint na ostatnia
    /// bezpieczna pozycje - zero teleportow, naturalny ruch). Sojusznicy
    /// i neutralni przechodza. Chcesz na druga strone? Zdobadz zamek,
    /// zawrzyj pokoj albo plyn morzem.
    /// ETAP 2 (Jeff 31.08): takze WROGIE PARTIE AI. Zabezpieczenie przed
    /// zapetleniem pathfindingu: odbita partia dostaje CEL-ODWROT (punkt po
    /// swojej stronie strefy) i osobisty COOLDOWN 3 h - w tym czasie straz
    /// jej nie tyka, wiec AI dochodzi do celu i podejmuje wlasna decyzje
    /// zamiast wibrowac na granicy. Oblezenie przeprawy jest LEGALNE
    /// (BesiegerCamp = skip) - wroga droga na druga strone to zdobycie
    /// warowni, pokoj albo morze. Eskorty armii ida za wodzem (skip
    /// doczepionych; odbijamy tylko partie prowadzace).
    /// </summary>
    internal static class CrossingLaw
    {
        // przeprawy-warownie: id osad ROT (Jeff 31.08: "Crossing law - robimy")
        // The Twins = ROT_town3, Moat Cailin = castle_B1, Bloody Gate = castle_EN2
        private static readonly string[] CrossingIds = { "ROT_town3", "castle_B1", "castle_EN2" };

        private static float _acc;
        private static Vec2 _lastSafe;
        private static bool _haveSafe;
        private static DateTime _lastShout = DateTime.MinValue;

        internal static void OnTick(float dt)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.CrossingLawEnabled) return;
                _acc += dt;
                if (_acc < 0.5f) return;
                _acc = 0f;

                var main = MobileParty.MainParty;
                if (main == null || !main.IsActive) return;
                if (main.CurrentSettlement != null || main.MapEvent != null) return;
                if (main.AttachedTo != null) return;   // w cudzej armii decyduje jej dowodca

                Vec2 pos = main.Position.ToVec2();
                bool inHostileZone = false;
                Settlement zone = null;

                foreach (var id in CrossingIds)
                {
                    var st = Settlement.Find(id);
                    if (st == null) continue;
                    float d = pos.Distance(st.GetPosition2D);
                    if (d > s.CrossingRadius) continue;
                    var owner = st.MapFaction;
                    var mine = main.MapFaction;
                    if (owner == null || mine == null || owner == mine) continue;
                    if (!FactionManager.IsAtWarAgainstFaction(owner, mine)) continue;
                    inHostileZone = true; zone = st;
                    break;
                }

                if (!inHostileZone)
                {
                    _lastSafe = pos; _haveSafe = true;
                    return;
                }

                // straz mostu wypycha: wracaj, skad przyszedles; po wczytaniu
                // save'a W strefie - odepchnij prosto od zamku
                Vec2 back;
                if (_haveSafe) back = _lastSafe;
                else
                {
                    Vec2 away = pos - zone.GetPosition2D;
                    if (away.LengthSquared < 0.01f) away = new Vec2(1f, 0f);
                    away.Normalize();
                    back = zone.GetPosition2D + away * (s.CrossingRadius + 1f);
                }
                main.SetMoveGoToPoint(new CampaignVec2(back, false), MobileParty.NavigationType.Default);

                if ((DateTime.Now - _lastShout).TotalSeconds > 5)
                {
                    _lastShout = DateTime.Now;
                    Log.Player("The " + zone.Name + " bar the crossing - no enemy of " + (zone.OwnerClan != null ? zone.OwnerClan.Name.ToString() : "the holders")
                               + " rides this bridge. Take the castle, make peace, or go by sea.", true);
                    Log.Info("CrossingLaw: gracz odepchniety od przeprawy " + zone.StringId + ".");
                }

                AiTick(s);
            }
            catch (Exception e) { Log.Error("CrossingLaw.OnTick", e); }
        }

        // ---- etap 2: straz mostu zawraca wrogie partie AI ----
        private static readonly System.Collections.Generic.Dictionary<MobileParty, CampaignTime> _aiCooldown =
            new System.Collections.Generic.Dictionary<MobileParty, CampaignTime>();
        private static float _aiAcc;
        private static int _aiShoutStock = 3;   // max meldunki na dobe, zeby nie zalewac logu gracza

        private static void AiTick(Settings s)
        {
            _aiAcc += 0.5f;
            if (_aiAcc < 2f) return;   // AI co ~2 s realne wystarczy
            _aiAcc = 0f;
            try
            {
                if (!s.CrossingLawAi) return;
                foreach (var id in CrossingIds)
                {
                    var st = Settlement.Find(id);
                    if (st == null) continue;
                    var owner = st.MapFaction;
                    if (owner == null) continue;
                    Vec2 gate = st.GetPosition2D;

                    foreach (var mp in MobileParty.All)
                    {
                        if (mp == null || !mp.IsActive || mp == MobileParty.MainParty) continue;
                        if (!mp.IsLordParty) continue;                       // strzezemy przed wojskiem, nie chlopstwem
                        if (mp.CurrentSettlement != null || mp.MapEvent != null) continue;
                        if (mp.BesiegerCamp != null) continue;               // oblega przeprawe - legalna droga
                        if (mp.AttachedTo != null) continue;                 // eskorta idzie za wodzem
                        var f = mp.MapFaction;
                        if (f == null || f == owner || !FactionManager.IsAtWarAgainstFaction(owner, f)) continue;
                        if (gate.Distance(mp.GetPosition2D) > s.CrossingRadius) continue;

                        CampaignTime until;
                        if (_aiCooldown.TryGetValue(mp, out until) && until > CampaignTime.Now) continue;

                        // cel-odwrot: punkt po stronie, z ktorej partia przyszla
                        Vec2 away = mp.GetPosition2D - gate;
                        if (away.LengthSquared < 0.01f) away = new Vec2(1f, 0f);
                        away.Normalize();
                        Vec2 back = gate + away * (s.CrossingRadius + 2f);
                        mp.SetMoveGoToPoint(new CampaignVec2(back, false), MobileParty.NavigationType.Default);
                        _aiCooldown[mp] = CampaignTime.HoursFromNow(3f);

                        if (_aiShoutStock > 0)
                        {
                            _aiShoutStock--;
                            Log.Player("The " + st.Name + " turn back " + mp.Name + " - the crossing is barred to enemies.", false);
                        }
                        Log.Info("CrossingLaw: AI " + mp.StringId + " zawrocone od " + st.StringId + " (cooldown 3h).");
                    }
                }
                // odswiez pule meldunkow raz na dobe i przytnij slownik z martwych partii
                if (CampaignTime.Now.GetHourOfDay == 12) _aiShoutStock = 3;
                if (_aiCooldown.Count > 200)
                {
                    var dead = new System.Collections.Generic.List<MobileParty>();
                    foreach (var kv in _aiCooldown)
                        if (kv.Key == null || !kv.Key.IsActive || kv.Value <= CampaignTime.Now) dead.Add(kv.Key);
                    foreach (var k in dead) _aiCooldown.Remove(k);
                }
            }
            catch (Exception e) { Log.Error("CrossingLaw.AiTick", e); }
        }
    }
}
