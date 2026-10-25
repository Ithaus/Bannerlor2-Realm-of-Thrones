using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using CharacterObject = TaleWorlds.CampaignSystem.CharacterObject;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P2: KTO MOWI I JAKIM GLOSEM. Kultura agenta (BasicCharacterObject.Culture.StringId, id ROT - battania = Polnoc, vlandia = Zachod, sturgia = Zelazne Wyspy,
    /// aserai = Dorne, khuzait = Dothrakowie, empire = Braavos, nie Dolina) -> region klipow:
    ///  - rozkazy / okrzyki choragwi (or, br, bd, ob): north (battania, skagosi, nightswatch), west (vlandia, crownlands), river (river, vale), storm (reach, stormlands,
    ///    dragonstone), iron (sturgia), dorne (aserai), freefolk (freefolk, wildlings, hilltribesmen), dothraki (khuzait - tylko szarza), ghiscari (march / hold, reszta
    ///    jak essos), essos (pozostale Essos);
    ///  - przemowa (sp): jak wyzej, ale osobno skagosi, nightswatch, crown, vale, reach, dragonstone, valyrian; Zlota Kompania z nazwy rodu;
    ///  - rog (hr): knight (Westeros poludniowy), north, nightswatch (jeden dzwiek - marsz; dwa - dzicy; trzy - Inni), iron, freefolk, dorne, dothraki, essos;
    ///  - beben (dr): westeros, north, iron, freefolk, dorne, unsullied (ghiscari), essos; Dothrakowie bez bebna.
    /// Inni (whitewalker / Undead) i bandyci (BasicCultureObject.IsBandit poza dzikimi) - bez glosu, rogu i bebna. Rod: nazwa i id rodu bohatera (karstark przed stark).
    /// Mowca: zywy, czlowiek, AI, nie gracz, nie kobieta, nie Inny, wazna druzyna. Kapitan szyku (Formation.Captain), a gdy go brak - najwyzszy tier w szyku
    /// (przeglad ludzi szyku najwyzej raz na 10 s na szyk, ApplyActionOnEachUnit tylko zbiera).
    /// </summary>
    internal static partial class BattleSound
    {
        private static readonly string[] Houses =
        {
            "karstark", "stark", "lannister", "tully", "arryn", "tyrell", "baratheon", "martell", "greyjoy", "targaryen", "frey", "bolton", "mormont", "umber",
            "manderly", "glover", "mallister", "blackwood", "bracken", "crakehall", "marbrand", "westerling", "royce", "hightower", "tarly", "tarth", "dayne",
            "yronwood", "harlaw", "velaryon"
        };

        private static string CulOf(BasicCharacterObject ch)
        {
            try { var c = ch != null ? ch.Culture : null; return c != null && c.StringId != null ? c.StringId.ToLowerInvariant() : null; }
            catch { return null; }
        }

        private static bool Wild(string cul) { return cul == "freefolk" || cul == "wildlings" || cul == "hilltribesmen"; }

        private static bool Westeros(string cul)
        {
            switch (cul)
            {
                case "vlandia": case "crownlands": case "river": case "vale": case "reach": case "stormlands": case "dragonstone":
                case "battania": case "skagosi": case "nightswatch": case "sturgia": case "aserai":
                    return true;
            }
            return false;
        }

        /// <summary>Bez glosu: brak kultury, Inni, bandyci (poza dzikimi).</summary>
        private static bool Silent(BasicCharacterObject ch, string cul)
        {
            if (cul == null || cul == "whitewalker") return true;
            try { if (Undead.Character(ch)) return true; } catch { }
            if (Wild(cul)) return false;
            try { var c = ch != null ? ch.Culture : null; if (c != null && c.IsBandit) return true; } catch { }
            return false;
        }

        private static string OrderRegion(string cul)
        {
            if (cul == null) return null;
            switch (cul)
            {
                case "battania": case "skagosi": case "nightswatch": return "north";
                case "vlandia": case "crownlands": return "west";
                case "river": case "vale": return "river";
                case "reach": case "stormlands": case "dragonstone": return "storm";
                case "sturgia": return "iron";
                case "aserai": return "dorne";
                case "khuzait": return "dothraki";
                case "ghiscari": return "ghiscari";
            }
            if (Wild(cul)) return "freefolk";
            return "essos";
        }

        private static string SpeechRegion(string cul)
        {
            if (cul == null) return null;
            switch (cul)
            {
                case "battania": return "north";
                case "skagosi": return "skagosi";
                case "nightswatch": return "nightswatch";
                case "vlandia": return "west";
                case "crownlands": return "crown";
                case "river": return "river";
                case "vale": return "vale";
                case "reach": return "reach";
                case "stormlands": return "storm";
                case "dragonstone": return "dragonstone";
                case "sturgia": return "iron";
                case "aserai": return "dorne";
                case "khuzait": return "dothraki";
                case "ghiscari": return "ghiscari";
                case "valyrian": return "valyrian";
            }
            if (Wild(cul)) return "freefolk";
            return "essos";
        }

        /// <summary>Motto regionu dla bohatera bez wlasnego rodu w katalogu (mo_dothraki, mo_essos...); null = bez motta.</summary>
        private static string MottoRegion(string cul)
        {
            if (cul == null) return null;
            switch (cul)
            {
                case "khuzait": return "dothraki";
                case "dragonstone": return "dragonstone";
                case "crownlands": return "crownlands";
                case "nightswatch": return "nightswatch";
                case "ghiscari": return "ghiscari";
            }
            if (Wild(cul)) return "freefolk";
            if (!Westeros(cul)) return "essos";
            return null;
        }

        private static string HornStyle(string cul)
        {
            if (cul == null) return null;
            switch (cul)
            {
                case "battania": case "skagosi": return "north";
                case "nightswatch": return "nightswatch";
                case "sturgia": return "iron";
                case "aserai": return "dorne";
                case "khuzait": return "dothraki";
            }
            if (Wild(cul)) return "freefolk";
            return Westeros(cul) ? "knight" : "essos";
        }

        private static string DrumStyle(string cul)
        {
            if (cul == null) return null;
            switch (cul)
            {
                case "battania": case "skagosi": case "nightswatch": return "north";
                case "sturgia": return "iron";
                case "aserai": return "dorne";
                case "ghiscari": return "unsullied";
                case "khuzait": return null;
            }
            if (Wild(cul)) return "freefolk";
            return Westeros(cul) ? "westeros" : "essos";
        }

        /// <summary>Rod bohatera z nazwy i id rodu (jak ROTBA HouseWordsResolver, ale karstark przed stark); Zlota Kompania osobno; null - brak.</summary>
        private static string HouseOf(Agent a)
        {
            try
            {
                var co = a != null ? a.Character as CharacterObject : null;
                var h = co != null ? co.HeroObject : null;
                var clan = h != null ? h.Clan : null;
                if (clan == null) return null;
                // 129: rody krolestwa gracza (gracz wladca) krzycza okrzyki Kardy; proba autotestu - bohaterowie druzyny gracza
                var cs = Settings.Current;
                if (cs != null && cs.OwnKingdomWarCries && Has("mo", KardaKey))
                {
                    var pk = PlayerKingdom();
                    if (pk != null && clan.Kingdom == pk) return KardaKey;
                    var m = Mission.Current;
                    if (_kardaTest && m != null && a.Team != null && ReferenceEquals(a.Team, m.PlayerTeam)) return KardaKey;
                }
                string t = ((clan.Name != null ? clan.Name.ToString() : "") + " " + (clan.StringId ?? "")).ToLowerInvariant();
                if (t.Contains("golden company") || t.Contains("golden_company") || t.Contains("goldencompany")) return "goldencompany";
                for (int i = 0; i < Houses.Length; i++) if (t.Contains(Houses[i])) return Houses[i];
            }
            catch { }
            return null;
        }

        private static bool CanSpeak(Agent a)
        {
            try
            {
                if (a == null || !a.IsActive() || !a.IsHuman || a.IsMainAgent || !a.IsAIControlled || a.IsFemale) return false;
                if (!MissionTeams.Valid(a.Team)) return false;
                return !Silent(a.Character, CulOf(a.Character));
            }
            catch { return false; }
        }

        private sealed class SpkCache { internal Agent A; internal float At = -1000f; }
        private static readonly Dictionary<Formation, SpkCache> _spk = new Dictionary<Formation, SpkCache>();
        private static readonly List<Agent> _units = new List<Agent>();
        private static readonly Action<Agent> _collectUnit = a => { if (_units.Count < 400) _units.Add(a); };

        /// <summary>Mowca szyku: kapitan, inaczej najwyzszy tier (przeglad najwyzej co 10 s na szyk). null - brak.</summary>
        private static Agent SpeakerOf(Formation f)
        {
            if (f == null) return null;
            var cap = f.Captain;
            if (CanSpeak(cap)) return cap;
            SpkCache sc;
            if (!_spk.TryGetValue(f, out sc)) { sc = new SpkCache(); _spk[f] = sc; }
            if (sc.A != null && CanSpeak(sc.A) && sc.A.Formation == f) return sc.A;
            if (_now - sc.At < 10f) return null;
            sc.At = _now;
            sc.A = null;
            _units.Clear();
            try { f.ApplyActionOnEachUnit(_collectUnit); } catch (Exception e) { _units.Clear(); RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.Mowca", e); }
            int best = -1;
            for (int i = 0; i < _units.Count; i++)
            {
                var a = _units[i];
                if (!CanSpeak(a)) continue;
                int t = BattleWind.TierOf(a.Character) + (a.IsHero ? 10 : 0);
                if (t > best) { best = t; sc.A = a; }
            }
            _units.Clear();
            return sc.A;
        }

        private sealed class CulCache { internal string Cul; internal BasicCharacterObject Ch; internal float At = -1000f; }
        private static readonly Dictionary<Formation, CulCache> _fcul = new Dictionary<Formation, CulCache>();

        /// <summary>Kultura szyku: kapitana, inaczej pierwszego czlowieka (odswiezana co 20 s). Ch - postac do Silent.</summary>
        private static string FormCul(Formation f, out BasicCharacterObject ch)
        {
            ch = null;
            if (f == null) return null;
            CulCache cc;
            if (!_fcul.TryGetValue(f, out cc)) { cc = new CulCache(); _fcul[f] = cc; }
            if (_now - cc.At < 20f) { ch = cc.Ch; return cc.Cul; }
            cc.At = _now;
            cc.Cul = null; cc.Ch = null;
            try
            {
                var a = f.Captain;
                if (a == null || !a.IsActive() || !a.IsHuman) a = f.CountOfUnits > 0 ? f.GetFirstUnit() : null;
                if (a != null && a.Character != null) { cc.Ch = a.Character; cc.Cul = CulOf(a.Character); }
            }
            catch { }
            ch = cc.Ch;
            return cc.Cul;
        }

        /// <summary>Kultura druzyny: dowodca (Leader / GeneralAgent), inaczej pierwszy czlowiek pierwszego niepustego szyku.</summary>
        private static string TeamCul(Team t, out BasicCharacterObject ch)
        {
            ch = null;
            if (!MissionTeams.Valid(t)) return null;
            try
            {
                var a = t.GeneralAgent ?? t.Leader;
                if (a != null && a.Character != null) { ch = a.Character; return CulOf(a.Character); }
                var fl = t.FormationsIncludingSpecialAndEmpty;
                for (int i = 0; fl != null && i < fl.Count; i++)
                    if (fl[i] != null && fl[i].CountOfUnits > 0) return FormCul(fl[i], out ch);
            }
            catch { }
            return null;
        }

        private static Vec3 FormPos(Formation f)
        {
            try { return f.CachedMedianPosition.GetNavMeshVec3() + new Vec3(0f, 0f, 1.5f); }
            catch { var p = f.CachedAveragePosition; return new Vec3(p.x, p.y, _lis.z); }
        }

        private static Vec3 HeadOf(Agent a) { return a.Position + new Vec3(0f, 0f, 1.6f); }

        private static float EnemyDist(Formation f)
        {
            float d2 = f.CachedClosestEnemyFormationDistanceSquared;
            return d2 >= float.MaxValue * 0.5f || d2 < 0f ? 100000f : (float)Math.Sqrt(d2);
        }
    }
}
