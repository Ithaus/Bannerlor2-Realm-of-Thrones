using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;

namespace RealisticCaptivity
{
    /// <summary>
    /// POZIOM PLAC MIASTA z Armoury (Jeff 07.10: "wszelkie koszty w danym miescie powinny byc zalezne od dobrobytu i stawek
    /// historycznych"; "wszystko, co dotyczy placenia, musi byc spojne"). Jeden wzor zyje w Armoury (Armoury.TownWage.Index:
    /// dobrobyt / TownWageRefProsperity, 0.5 - 1.5) - tu tylko odczyt przez refleksje (Armoury nie jest zaleznoscia tego modu);
    /// bez Armoury - 1 (zwykla stawka). Wies: miasto targowe wsi (TradeBound), a gdy go brak - osada nadrzedna.
    /// </summary>
    internal static class TownWageLink
    {
        private static Func<Settlement, float> _index;
        private static bool _tried, _fail;

        internal static bool Linked { get { Resolve(); return _index != null; } }

        internal static float Index(Settlement s)
        {
            Resolve();
            if (_index == null || s == null) return 1f;
            var market = s.IsVillage && s.Village != null ? (s.Village.TradeBound ?? s.Village.Bound ?? s) : s;
            try { return _index(market); }
            catch (Exception e) { if (!_fail) { _fail = true; Log.Error("TownWageLink", e); } return 1f; }
        }

        // ------------------------------------------------------------ rezerwa miasta (ta sama co w Armoury)

        private static System.Reflection.FieldInfo _floorField, _curField;

        private static bool _floorTried;



        /// <summary>

        /// Ile osada moze wydac, gdy KUPUJE od gracza albo dzieli sie z nim utargiem: miasto - kasa ponad rezerwe na renty

        /// (Armoury TownRentFloorGold, 20 000 - ponizej Banner Kings odbiera miastu dobrobyt; ta sama regula co paser, przetop i karawany

        /// w Armoury); wies - cala kasa; bez Armoury - cala kasa.

        /// </summary>

        internal static int Spare(Settlement s)

        {

            var sc = s != null ? s.SettlementComponent : null;

            if (sc == null) return 0;

            int gold = Math.Max(0, sc.Gold);

            if (!s.IsTown) return gold;

            return Math.Max(0, gold - (int)Math.Ceiling(Floor()));

        }



        private static float Floor()

        {

            try

            {

                if (!_floorTried)

                {

                    _floorTried = true;

                    foreach (var a in AppDomain.CurrentDomain.GetAssemblies())

                    {

                        if (a.GetName().Name != "Armoury") continue;

                        var st = a.GetType("Armoury.Settings", false);

                        if (st == null) break;

                        _curField = st.GetField("Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                        _floorField = st.GetField("TownRentFloorGold");

                        break;

                    }

                }

                var cur = _curField != null ? _curField.GetValue(null) : null;

                return cur != null && _floorField != null ? Math.Max(0f, Convert.ToSingle(_floorField.GetValue(cur))) : 0f;

            }

            catch { return 0f; }

        }


        private static void Resolve()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                Type t = null;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (a.GetName().Name != "Armoury") continue;
                    t = a.GetType("Armoury.TownWage", false);
                    if (t != null) break;
                }
                var m = t != null ? t.GetMethod("Index", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Settlement) }, null) : null;
                if (m != null) _index = (Func<Settlement, float>)Delegate.CreateDelegate(typeof(Func<Settlement, float>), m);
                Log.Info("TownWageLink: poziom plac miasta z Armoury - " + (_index != null ? "PODPIETY (Armoury.TownWage.Index)" : "BRAK (zwykla stawka wszedzie)"));
            }
            catch (Exception e) { Log.Error("TownWageLink.Resolve", e); }
        }
    }
}
