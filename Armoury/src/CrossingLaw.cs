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
    /// Etap 1 celowo tylko gracz: wypychanie WROGICH ARMII AI grozi
    /// zapetleniem ich pathfindingu (armia klinuje sie o strefe) - do
    /// decyzji Jeffa po testach.
    /// </summary>
    internal static class CrossingLaw
    {
        // przeprawy-warownie: id osad ROT (The Twins = ROT_town3)
        private static readonly string[] CrossingIds = { "ROT_town3" };

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
            }
            catch (Exception e) { Log.Error("CrossingLaw.OnTick", e); }
        }
    }
}
