using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 474-P5 (Jeff 13.11, decyzja 2: "Ludzie rodow tylko tam gdzie sa ziemie rodow, nawet jak ktos inny je podbije np. ja to nadal sa to ludzie z tej kultury
    /// i z tego rodu i nadal moge ich rekrutowac"). TABELA SIEDZIB RODOW ROT - jedno zrodlo prawdy, czytane z ROT (nie przepisane recznie):
    /// DOWOD (dekompilacja ROT, ROT.CampaignBehaviors.ROTHouseTroopsBehavior): prywatny slownik _castellans (Settlement -> ROTHouseTroopsConfig) wypelniany w
    /// OnSessionLaunched - 35 siedzib (np. RaventreeHall -> "blackwood_levy" u kasztelana "blackwoodcast", ROTClans.Blackwood; Stonehedge -> "bracken_levy";
    /// Winterfell -> "stark_levy"); konfiguracja: BasicTroopID (string), CastellanID (string), Clans (Clan[]), MinimumRelation (int, domyslnie 30).
    /// Red Keep ("kingsguard_page", Clans pusta) to Gwardia Krolewska, nie rod - pominieta.
    /// ZIEMIA RODU = siedziba z tej tabeli i jej okreg (wsie przypisane do siedziby w danych mapy: Village.Bound). Liczy sie POCHODZENIE ziemi, nie obecny pan -
    /// po podboju ludzie tej ziemi nadal sa ludzmi tego rodu.
    /// ZGODA RODU (ta sama zasada co kasztelan ROT, house_troops_castellan_buy - warunek klikniecia): siedziba albo wies okregu gracza; rod wygasly ("Although
    /// House X is no more, I still train men"); rod gracza; inaczej relacja glowy pierwszego zywego klanu rodu z graczem co najmniej MinimumRelation.
    /// Odczyt odbiciem raz na kampanie (po OnSessionLaunched ROT - pusty slownik nie jest zapamietany, nastepne pytanie czyta znowu). Nie rzuca.
    /// </summary>
    internal static class RotHouses
    {
        internal sealed class House
        {
            public Settlement Seat;
            public CharacterObject Troop;
            public string TroopId;
            public Clan[] Clans;
            public int MinRel;

            /// <summary>Pierwszy zywy klan rodu (jak ROT: FirstOrDefault(!IsEliminated)); null - rod wygasly albo bez klanow.</summary>
            public Clan Alive
            {
                get
                {
                    if (Clans == null) return null;
                    foreach (var c in Clans) if (c != null && !c.IsEliminated) return c;
                    return null;
                }
            }

            /// <summary>Nazwa rodu po angielsku ("House Blackwood"); bez klanow - nazwa siedziby.</summary>
            public string Name
            {
                get
                {
                    try
                    {
                        var c = Clans != null && Clans.Length > 0 ? Clans[0] : null;
                        if (c != null && c.Name != null) return "House " + c.Name.ToString();
                        return Seat != null && Seat.Name != null ? Seat.Name.ToString() : "?";
                    }
                    catch { return "?"; }
                }
            }
        }

        private static Dictionary<Settlement, House> _bySeat;
        private static object _from;
        private static int _stumbles, _reads;
        private static readonly HashSet<string> _err = new HashSet<string>(StringComparer.Ordinal);

        internal static int Stumbles { get { return _stumbles; } }
        internal static int Count { get { var d = _bySeat; return d != null ? d.Count : 0; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("RotHouses." + where, e); } catch { }
        }

        /// <summary>Tabela siedzib tej kampanii (odczyt przy pierwszym pytaniu). Pusta - ROT nieobecny albo jeszcze nie wypelnil slownika.</summary>
        private static Dictionary<Settlement, House> Table()
        {
            var c = Campaign.Current;
            if (c == null) return null;
            if (_bySeat != null && ReferenceEquals(_from, c)) return _bySeat;
            var d = new Dictionary<Settlement, House>();
            int skipped = 0;
            try
            {
                _reads++;
                var behT = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTHouseTroopsBehavior");
                if (behT == null) { if (_reads == 1) Log.Info("Levy rodow (474-P5): ROT ROTHouseTroopsBehavior nie znaleziony - levy rodow nieczynne."); return null; }
                var get = typeof(Campaign).GetMethod("GetCampaignBehavior", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                object beh = get != null ? get.MakeGenericMethod(behT).Invoke(c, null) : null;
                var fCast = AccessTools.Field(behT, "_castellans");
                var dict = beh != null && fCast != null ? fCast.GetValue(beh) as IDictionary : null;
                if (dict == null || dict.Count == 0) return null;   // ROT jeszcze nie wypelnil - nie zapamietane
                FieldInfo fId = null, fClans = null, fRel = null;
                foreach (DictionaryEntry e in dict)
                {
                    var st = e.Key as Settlement; var cfg = e.Value;
                    if (st == null || cfg == null) { skipped++; continue; }
                    if (fId == null)
                    {
                        var ct = cfg.GetType();
                        fId = AccessTools.Field(ct, "BasicTroopID"); fClans = AccessTools.Field(ct, "Clans"); fRel = AccessTools.Field(ct, "MinimumRelation");
                        if (fId == null || fClans == null) { Log.Info("Levy rodow (474-P5): ROTHouseTroopsConfig bez pol BasicTroopID / Clans - levy rodow nieczynne."); return null; }
                    }
                    var clans = fClans.GetValue(cfg) as Clan[];
                    string tid = fId.GetValue(cfg) as string;
                    var troop = string.IsNullOrEmpty(tid) ? null : CharacterObject.Find(tid);
                    if (clans == null || clans.Length == 0 || troop == null || troop.IsHero) { skipped++; continue; }   // Red Keep (Gwardia) i braki danych
                    int rel = 30;
                    try { if (fRel != null) rel = (int)fRel.GetValue(cfg); } catch { rel = 30; }
                    d[st] = new House { Seat = st, Troop = troop, TroopId = tid, Clans = clans, MinRel = rel };
                }
            }
            catch (Exception e) { Stumble("Table", e); return null; }
            _bySeat = d; _from = c;
            var sb = new System.Text.StringBuilder(400);
            int n = 0;
            foreach (var h in d.Values) { if (n++ < 6) sb.Append(n > 1 ? ", " : "").Append(h.Seat.StringId).Append(" -> ").Append(h.TroopId); }
            Log.Info("Levy rodow (474-P5): tabela siedzib rodow z ROT (ROTHouseTroopsBehavior._castellans) - " + d.Count + " siedzib, pominiete " + skipped
                     + " (Gwardia Krolewska / braki danych); np. " + sb + (d.Count > 6 ? ", ..." : "") + ".");
            return d;
        }

        /// <summary>Rod siedziby seat (sama siedziba z tabeli ROT). Nie rzuca.</summary>
        internal static House OfSeat(Settlement seat)
        {
            try { House h; var t = Table(); return seat != null && t != null && t.TryGetValue(seat, out h) ? h : null; }
            catch (Exception e) { Stumble("OfSeat", e); return null; }
        }

        /// <summary>Rod ziemi wsi okregu v: wies przypisana do siedziby rodu (Village.Bound - dane mapy, nie obecny pan). Nie rzuca.</summary>
        internal static House OfVillage(Settlement v)
        {
            try
            {
                if (v == null || v.Village == null) return null;
                return OfSeat(v.Village.Bound);
            }
            catch (Exception e) { Stumble("OfVillage", e); return null; }
        }

        /// <summary>Zgoda rodu na pobor jego ludzi przez gracza (zasada kasztelana ROT). null - zgoda; inaczej powod po angielsku. land - wies okregu
        /// (albo sama siedziba): jej pan albo pan siedziby = rod gracza - zgoda.</summary>
        internal static string Refusal(House h, Settlement land)
        {
            try
            {
                if (h == null) return "No house holds this land.";
                var pc = Clan.PlayerClan;
                if (pc != null && (ReferenceEquals(land != null ? land.OwnerClan : null, pc) || ReferenceEquals(h.Seat != null ? h.Seat.OwnerClan : null, pc))) return null;
                if (pc != null && h.Clans != null) foreach (var c in h.Clans) if (ReferenceEquals(c, pc)) return null;
                var alive = h.Alive;
                if (alive == null) return null;   // rod wygasly - ludzie ziemi ida za kazdym, kto zaplaci
                var lead = alive.Leader;
                if (lead == null || Hero.MainHero == null) return null;
                int rel = lead.GetRelation(Hero.MainHero);
                if (rel >= h.MinRel) return null;
                return h.Name + " does not know you well enough to let its men follow you - " + lead.Name + " must think well of you (" + h.MinRel + ", now " + rel + ").";
            }
            catch (Exception e) { Stumble("Refusal", e); return "The house will not say."; }
        }
    }
}
