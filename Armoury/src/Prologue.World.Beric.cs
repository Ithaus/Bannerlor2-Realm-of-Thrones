using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// m104-e5 pkt 7 - ODDZIAL BERICA od dnia 160 (klucz PrologueBericHunt; projekt pkt 2: "Ned jako Reka wysyla Berica Dondarriona na Gore", scena 20).
    /// W ROT Beric to bohater z wzoru ROTuniqueleader_spc_brotherhood_0 - przywodca pomniejszej frakcji "brotherhood" (Brotherhood without Banners, wyjeta spod prawa,
    /// kultura vlandia, dom Riverrun; rozpoznanie e5:B). Wedlug DefaultDiplomacyModel.IsAtConstantWar jest w stalej wojnie z krolestwem korony (ta sama kultura) - nie wjedzie
    /// do Krolewskiej Przystani, wiec rusza tam, gdzie jest (zwykle Dorzecze). Z Ludzmi Gory walczyc moze: klan bandycki kontra frakcja nie-bandycka = wojna
    /// (GetShallowDiplomaticStance). Brak Berica albo jego druzyny - glowa rodu Dondarrion (ROTclan_58, Blackhaven), gdy prowadzi druzyne. Partia sceny: rozkaz
    /// SetMoveEngageParty na najblizsza bande Ludzi Gory (co godzine od nowa), ukryta przed AI (lordowie korony i bandy nie zaczepiaja jej po drodze; gracz moze).
    /// Koniec: Ludzi Gory juz nie ma (rozbici albo w domu), druzyna stracona, dzien 188 - wraca do swojego AI. Ludzie i zloto - bez zmian (zwykla druzyna).
    /// m105-drobne pkt 1 (wedlug ksiazki: Ned wysyla Berica po skargach ze spalonych wsi): od dnia 160 wezwanie dopiero po pierwszej SPALONEJ wsi Ludzi Gory, najpozniej
    /// w dniu BericCallBy (168); test 102 - Beric ruszyl w d160 sztywno i rozbil bande w d172.6, zanim spalila pierwsza wies. Na druzyne czeka BericLate dni od wezwania
    /// (dzien wezwania w zapisie - czwarte pole wbr; stary zapis bez pola - wezwanie liczone od nowa).
    /// </summary>
    internal static partial class Prologue
    {
        private const string BScene = "Beric";
        private const string BericTpl = "ROTuniqueleader_spc_brotherhood_0", DondarrionClanId = "ROTclan_58";
        private const int BericFrom = 160, BericLate = 5, BericCallBy = 168;   // m105-drobne: wezwanie po pierwszej spalonej wsi, najpozniej d168

        // stan (zapis): -1 pominiety, 0 przed, 1 poscig, 2 koniec; bohater sceny (Beric albo glowa Dondarrionow)
        private static int _brPhase;
        private static string _brHero = "", _brWhy = "";
        private static double _brStart = -1, _brEnd = -1;
        private static double _brCall = -1;          // m105-drobne: dzien wezwania (zapis - czwarte pole wbr)
        private static string _brCallWhy = "";       // powod wezwania (tylko do linii "rusza" w tej sesji)

        private static void ResetBeric() { _brPhase = 0; _brHero = _brWhy = _brCallWhy = ""; _brStart = _brEnd = _brCall = -1; }

        private static string PackBeric()
        {
            return "wbr=" + _brPhase.ToString(Inv) + "," + D(_brStart) + "," + D(_brEnd) + "," + D(_brCall) + ";wbh=" + Clean(_brHero) + ";wbw=" + Clean(_brWhy) + ";";
        }

        private static void UnpackBeric(Dictionary<string, string> map)
        {
            ResetBeric();
            string v;
            if (map.TryGetValue("wbr", out v)) { var a = v.Split(','); _brPhase = Int(a, 0, 0); _brStart = Dbl(a, 1, -1); _brEnd = Dbl(a, 2, -1); _brCall = Dbl(a, 3, -1); }
            if (map.TryGetValue("wbh", out v)) _brHero = Unesc(v);
            if (map.TryGetValue("wbw", out v)) _brWhy = Unesc(v);
        }

        private static int BericWins() { int n = 0; if (_brHero.Length > 0) foreach (var id in _mmKillerIds) if (id == _brHero) n++; return n; }

        private static void BericEnd(int phase, string why, double day)
        {
            _brPhase = phase; _brWhy = why; _brEnd = day;
            Log.Info(WTag + "Beric: " + (phase < 0 ? "POMINIETY - " : "KONIEC - ") + why + " (dzien prologu " + D(day) + ", rozbite bandy Ludzi Gory: " + BericWins() + ").");
        }

        private static void BericStep(double day, bool daily)
        {
            var s = S;
            if (s == null || !s.PrologueBericHunt) return;
            if (_brPhase < 0 || _brPhase > 1) return;
            if (_brPhase == 0)
            {
                if (day < BericFrom) return;
                if (day >= PrologueDays) { BericEnd(-1, "koniec prologu przed poscigiem", day); return; }
                if (_wLegacy && day > BericFrom + 2) { BericEnd(-1, "zapis sprzed E5", day); return; }
                if (_brCall < 0)
                {
                    // m105-drobne: Ned wzywa Berica po pierwszej spalonej wsi (skargi z Dorzecza), najpozniej w dniu BericCallBy; bez band w polu - jak dotad (koniec nizej)
                    if (_mmBurned == 0 && day < BericCallBy && _mm.Count > 0) return;
                    _brCall = day;
                    _brCallWhy = _mmBurned > 0 ? "po spaleniu " + (_mmBurnedNames.Count > 0 ? _mmBurnedNames[0] : "wsi") : (_mm.Count > 0 ? "d" + BericCallBy + " bez spalonej wsi" : "bez band w polu");
                }
                var b = Tpl(BericTpl);
                Hero who = null; string how;
                if (Led(b) != null) { who = b; how = "Beric (brotherhood)"; }
                else
                {
                    var dc = C(DondarrionClanId);
                    var lead = dc != null ? dc.Leader : null;
                    if (Led(lead) != null) { who = lead; how = "zapas: glowa rodu Dondarrion (" + (b == null ? "brak Berica" : "Beric " + WhereS(b)) + ")"; }
                    else how = (b == null ? "brak Berica" : "Beric " + WhereS(b)) + ", glowa rodu Dondarrion " + (lead == null ? "brak" : WhereS(lead));
                }
                if (who == null) { if (day >= Math.Max(BericFrom, _brCall) + BericLate) BericEnd(-1, "bez druzyny: " + how, day); return; }
                if (_mm.Count == 0) { BericEnd(-1, "brak Ludzi Gory w polu (" + (_mmStarted ? "rozbici albo nie powstali" : "scena ludzi Gory nie ruszyla") + ")", day); return; }
                _brPhase = 1; _brHero = who.StringId; _brStart = day;
                var p0 = Led(who);
                Log.Info(WTag + "Beric: rusza na Ludzi Gory - " + WhoS(who) + " [" + how + "] druzyna " + p0.StringId + " (" + p0.MemberRoster.TotalManCount + " ludzi, "
                         + Pos(p0, null) + ", frakcja " + (p0.MapFaction != null ? p0.MapFaction.StringId : "-") + ") [wezwanie d" + D(_brCall) + ": "
                         + (_brCallWhy.Length > 0 ? _brCallWhy : "z zapisu") + ", spalone wsie Ludzi Gory " + _mmBurned + "] (dzien prologu " + D(day) + ").");
            }
            var h = H(_brHero) ?? Tpl(BericTpl);
            var bp = Led(h);
            if (bp == null) { BericEnd(2, "druzyna stracona (" + WhereS(h) + ")", day); return; }
            if (day >= PrologueDays) { BericEnd(2, "koniec prologu", day); return; }
            var target = NearestRaider(bp);
            if (target == null)
            {
                if (_mm.Count == 0) BericEnd(2, "Ludzi Gory juz nie ma w polu", day);
                return;   // bandy w bitwie z kims innym / chwilowo poza zasiegiem - nastepna godzina
            }
            Control(BScene, bp, KEngage, null, target, true);
        }

        private static MobileParty NearestRaider(MobileParty from)
        {
            if (_mm.Count == 0) return null;
            var map = BanditsById();
            MobileParty best = null; float bd = float.MaxValue;
            foreach (var r in _mm)
            {
                MobileParty mp;
                if (!map.TryGetValue(r.Id, out mp) || mp == null || !mp.IsActive) continue;
                float d = Dist(from, mp);
                if (d >= 0f && d < bd) { bd = d; best = mp; }
            }
            return best;
        }

        private static string BericLine()
        {
            string st = _brPhase < 0 ? "pominiety (" + _brWhy + ")" : _brPhase == 0 ? (_brCall >= 0 ? "wezwany d" + D(_brCall) + ", czeka na druzyne" : "przed (od d" + BericFrom + " po pierwszej spalonej wsi, najpozniej d" + BericCallBy + ")")
                        : _brPhase == 2 ? "koniec d" + D(_brEnd) + " (" + _brWhy + ")" : "poscig";
            if (_brPhase == 1)
            {
                var bp = Led(H(_brHero));
                var t = bp != null ? NearestRaider(bp) : null;
                st += " - " + WhoS(H(_brHero)) + " " + (bp == null ? "bez druzyny" : bp.MemberRoster.TotalManCount + " ludzi, " + Pos(bp, null) + (t != null ? ", cel " + t.StringId + " " + Dist(bp, t).ToString("0", Inv) + " jedn." : ", bez celu"));
            }
            return "Beric: " + st + ", rozbite bandy Ludzi Gory " + BericWins();
        }
    }
}
