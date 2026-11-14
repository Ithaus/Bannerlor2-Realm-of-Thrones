using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace Armoury
{
    /// <summary>
    /// PERKI XP Z BITWY (503 = P2 audytu perkow 14.11; Jeff 14.11 "78-105 ok, rob po kolei"). RBM (RBMCombat.CampaignChanges+GetXpFromHitPatch,
    /// prefiks na DefaultCombatXpModel.GetXpFromHit, zwraca false dla Battle / PracticeFight / Tournament / SimulationBattle) liczy XP za cios sam
    /// i premie perkow chce dodac odbiciem: method.Invoke(__instance, new object[3] { attackerParty, val, attackerTroop }). ExplainedNumber to
    /// struktura - w tablicy object[] jedzie jej pudelko; metoda gry (GetBattleXpBonusFromPerks, parametr ref) dopisuje czynniki do pudelka
    /// (args[1]), ktorego RBM juz nie czyta, a do __result idzie stara kopia val. Wszystkie perki tej metody gry przepadaja:
    /// OneHanded Trainer, LeadByExample, CorpsACorps; TwoHanded BaptisedInBlood; Throwing Resourceful; Crossbow MountedCrossbowman; Bow BullsEye;
    /// Roguery NoRestForTheWicked (perki wodza partii) oraz TwoHanded ProjectileDeflection i Polearm Guards (gubernator - XP zalogi).
    /// Leadership InspiringLeader (kapitan) RBM dolicza sam - ta czesc dziala.
    /// NAPRAWA: postfiks (Priority.VeryLow - po zwyklych cudzych postfiksach, PRZED FairXp, ktory na tej metodzie stoi z Priority.Last) na
    /// DefaultCombatXpModel.GetXpFromHit (DeclaredMethod), tylko gdy oryginal NIE biegl (__runOriginal == false - inaczej gra dodala perki sama)
    /// i tylko dla Battle / SimulationBattle (XP wojska z bitwy; arena i turniej bez zmian - tam stoja TXP i Gladiator BK). Liczy premie ta sama
    /// metoda gry (delegat na prywatne GetBattleXpBonusFromPerks - warunki morza, roli, gubernatora jak w grze) na probce ExplainedNumber(1)
    /// i dopisuje jej czynniki (i ewentualne Add) do wyniku. Bez podwojenia: gdy suma czynnikow wyniku ma juz InspiringLeader RBM + premie perkow
    /// (RBM kiedys naprawi Invoke) - nic nie dopisujemy i liczymy "juz w wyniku".
    /// Klucz PerkXpFix (TAK; NIE = jak dotad). Bledy liczone (potkniecia), bez wylacznika. Stan tylko liczniki doby, bez zapisu.
    /// Linie: "Perki XP (503): start sesji ..." (OnSessionLaunched) i "Perki XP (503): dzien N ..." (doba, XP doliczone per perk).
    /// </summary>
    internal static class PerkXp503
    {
        internal const string P = "Perki XP (503): ";
        private const string Owner = "com.jeff.armoury.perkxp503";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private delegate void BonusFn(PartyBase party, ref ExplainedNumber xp, CharacterObject troop);

        private static Harmony _h;
        private static MethodBase _patched;
        private static BonusFn _bonus;
        private static string _hook = "nie wpieta", _bonusHook = "nie szukana", _rbm = "?";
        private static int _stumbles;

        // liczniki doby
        private static int _dCalls, _dAdded, _dAlready, _dNone;
        private static double _dBase, _dXp;
        private static readonly Dictionary<string, double> _dPerk = new Dictionary<string, double>();
        // rozbicie na perki (tylko do logu): raz na dobe na pare (partia, oddzial) - opisy perkow kosztuja, wiec nie przy kazdym ciosie
        private static readonly Dictionary<(PartyBase, CharacterObject), KeyValuePair<string, float>[]> _split = new Dictionary<(PartyBase, CharacterObject), KeyValuePair<string, float>[]>();
        private const int SplitCap = 20000;

        private static string F1(double v) { return v.ToString("0.#", Inv); }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3 || _stumbles % 100 == 0) Log.Error("PerkXp503." + where + " (potkniecie " + _stumbles + ")", e);
        }

        private static void ResetDay()
        {
            _dCalls = 0; _dAdded = 0; _dAlready = 0; _dNone = 0; _dBase = 0; _dXp = 0;
            _dPerk.Clear(); _split.Clear();
        }

        /// <summary>OnSessionLaunched: wpiecie postfiksu (raz na proces), delegat na metode gry, zerowanie licznikow, linia startu.</summary>
        internal static void EnsureHooks()
        {
            ResetDay();
            int others = -1;
            try
            {
                if (_bonus == null)
                {
                    var b = AccessTools.DeclaredMethod(typeof(DefaultCombatXpModel), "GetBattleXpBonusFromPerks",
                        new[] { typeof(PartyBase), typeof(ExplainedNumber).MakeByRefType(), typeof(CharacterObject) });
                    _bonus = b != null ? AccessTools.MethodDelegate<BonusFn>((MethodInfo)b) : null;
                    _bonusHook = _bonus != null ? "TAK" : "BRAK (latka nic nie doliczy)";
                }
                if (_h == null) _h = new Harmony(Owner);
                var m = AccessTools.DeclaredMethod(typeof(DefaultCombatXpModel), "GetXpFromHit");
                if (m == null) _hook = "BRAK celu";
                else
                {
                    if (_patched == null)
                    {
                        _h.Patch(m, postfix: new HarmonyMethod(typeof(PerkXp503), nameof(XpPostfix)) { priority = Priority.VeryLow });   // przed FairXp (Last)
                        _patched = m;
                    }
                    _hook = "wpieta";
                    try
                    {
                        var info = Harmony.GetPatchInfo(m);
                        if (info != null)
                        {
                            bool ours = false, rbm = false;
                            others = 0;
                            foreach (var p in info.Prefixes)
                            {
                                if (p.owner == Owner) { ours = true; continue; }
                                others++;
                                var dt = p.PatchMethod != null ? p.PatchMethod.DeclaringType : null;
                                if (dt != null && dt.FullName != null && dt.FullName.StartsWith("RBMCombat", StringComparison.Ordinal)) rbm = true;
                            }
                            foreach (var p in info.Postfixes) { if (p.owner == Owner) ours = true; else others++; }
                            foreach (var p in info.Transpilers) if (p.owner != Owner) others++;
                            foreach (var p in info.Finalizers) if (p.owner != Owner) others++;
                            if (!ours) _hook = "NIE widac w GetPatchInfo";
                            _rbm = rbm ? "TAK (prefiks RBMCombat gubi perki)" : "NIE (gra liczy perki sama - latka nic nie doda)";
                        }
                    }
                    catch { others = -1; }
                }
            }
            catch (Exception e) { _hook = "wyjatek " + e.GetType().Name; Stumble("EnsureHooks", e); }

            string perks = "?";
            try
            {
                perks = string.Join(", ", Array.ConvertAll(new[]
                {
                    DefaultPerks.OneHanded.Trainer, DefaultPerks.OneHanded.LeadByExample, DefaultPerks.OneHanded.CorpsACorps,
                    DefaultPerks.TwoHanded.BaptisedInBlood, DefaultPerks.Throwing.Resourceful, DefaultPerks.Crossbow.MountedCrossbowman,
                    DefaultPerks.Bow.BullsEye, DefaultPerks.Roguery.NoRestForTheWicked, DefaultPerks.TwoHanded.ProjectileDeflection, DefaultPerks.Polearm.Guards
                }, x => x != null ? x.StringId : "null"));
            }
            catch { }
            var s = Settings.Current;
            Log.Info(P + "start sesji | klucz PerkXpFix " + (s != null && s.PerkXpFix ? "TAK" : "NIE")
                     + " | postfiks DefaultCombatXpModel.GetXpFromHit " + _hook + " (Priority.VeryLow, przed FairXp)"
                     + " | metoda gry GetBattleXpBonusFromPerks " + _bonusHook + " | RBM na celu " + _rbm
                     + " | inne latki na celu " + (others >= 0 ? others.ToString(Inv) : "?")
                     + " | perki (bitwa i symulacja): " + perks + " | potkniecia " + _stumbles + ".");
        }

        /// <summary>Postfiks DefaultCombatXpModel.GetXpFromHit: gdy prefiks RBM zdjal metode gry, doliczenie premii perkow metoda gry.</summary>
        public static void XpPostfix(CharacterObject attackerTroop, CharacterObject captain, PartyBase attackerParty,
            CombatXpModel.MissionTypeEnum missionType, ref ExplainedNumber __result, bool __runOriginal)
        {
            if (__runOriginal) return;   // oryginal biegl - perki juz w wyniku
            if (missionType != CombatXpModel.MissionTypeEnum.Battle && missionType != CombatXpModel.MissionTypeEnum.SimulationBattle) return;
            if (attackerParty == null || attackerTroop == null || _bonus == null) return;
            var s = Settings.Current;
            if (s == null || !s.PerkXpFix) return;
            try
            {
                float baseXp = __result.BaseNumber;
                if (!(baseXp > 0f)) return;
                _dCalls++;
                var probe = new ExplainedNumber(1f, false, null);
                _bonus(attackerParty, ref probe, attackerTroop);
                float f = probe.SumOfFactors, add = probe.BaseNumber - 1f;
                if (f < 1e-4f && f > -1e-4f && add < 1e-4f && add > -1e-4f) { _dNone++; return; }

                // bez podwojenia: RBM dolicza tylko InspiringLeader kapitana (ten sam warunek co w RBM); jesli suma czynnikow ma juz i perki - nic
                float insp = 0f;
                if (captain != null && captain.IsHero && captain.GetPerkValue(DefaultPerks.Leadership.InspiringLeader))
                    insp = DefaultPerks.Leadership.InspiringLeader.SecondaryBonus;
                if (f > 1e-4f && __result.SumOfFactors >= insp + f - 1e-4f) { _dAlready++; return; }

                float before = __result.ResultNumber;
                if (add > 1e-4f || add < -1e-4f) __result.Add(add);
                if (f > 1e-4f || f < -1e-4f) __result.AddFactor(f);
                float gained = __result.ResultNumber - before;
                _dAdded++; _dBase += before; _dXp += gained;
                Attribute(attackerParty, attackerTroop, gained);
            }
            catch (Exception e) { Stumble("XpPostfix", e); }
        }

        /// <summary>Tylko log: rozbicie doliczonego XP na perki (udzial wedlug czynnika perka w probce z opisami).</summary>
        private static void Attribute(PartyBase party, CharacterObject troop, float gained)
        {
            try
            {
                KeyValuePair<string, float>[] lines;
                var key = (party, troop);
                if (!_split.TryGetValue(key, out lines))
                {
                    if (_split.Count >= SplitCap) { Bucket("(rozbicie pominiete - limit)", gained); return; }
                    var d = new ExplainedNumber(1f, true, null);
                    _bonus(party, ref d, troop);
                    var all = d.GetLines();
                    var list = new List<KeyValuePair<string, float>>();
                    for (int i = 1; i < all.Count; i++) list.Add(new KeyValuePair<string, float>(all[i].name, Math.Abs(all[i].number)));   // [0] = linia bazy (1)
                    lines = list.ToArray();
                    _split[key] = lines;
                }
                float sum = 0f;
                foreach (var l in lines) sum += l.Value;
                if (!(sum > 0f)) { Bucket("?", gained); return; }
                foreach (var l in lines) Bucket(l.Key, gained * l.Value / sum);
            }
            catch (Exception e) { Stumble("Attribute", e); }
        }

        private static void Bucket(string name, double xp)
        {
            double v;
            _dPerk.TryGetValue(name ?? "?", out v);
            _dPerk[name ?? "?"] = v + xp;
        }

        /// <summary>ArmouryBehavior.OnDailyTickBodyInner: linia doby (gdy cos sie dzialo albo sa potkniecia) i zerowanie.</summary>
        internal static void Daily(int day)
        {
            try
            {
                if (_dCalls > 0 || _stumbles > 0)
                {
                    var parts = new List<string>();
                    foreach (var kv in _dPerk) parts.Add(kv.Key + " +" + F1(kv.Value));
                    parts.Sort(StringComparer.Ordinal);
                    Log.Info(P + "dzien " + day + " | ciosy RBM (bitwa, symulacja) " + _dCalls + ", z premia perkow doliczona " + _dAdded
                             + ", bez perkow " + _dNone + ", perki juz w wyniku (bez doliczania) " + _dAlready
                             + " | XP przed / doliczone " + F1(_dBase) + " / +" + F1(_dXp)
                             + " | per perk: " + (parts.Count > 0 ? string.Join(", ", parts) : "-")
                             + " | latka " + _hook + ", potkniecia " + _stumbles + ".");
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            ResetDay();
        }
    }
}
