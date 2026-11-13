using System;
using System.Collections.Generic;
using System.Globalization;
using SandBox.Objects.Usables;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 474-P9 (Jeff 13.11 "Wioski, i karczmy rob wszystko", projekt docs/PROJEKT-WSIE-POBOCZNE-KARCZMY-2026-11-13.md pkt 2 "Dosiadanie sie do plotkujacych", paczka 9):
    /// STOLY WE WNETRZU ZAJAZDU (474-P8). Bywalcy siedza przy stolach na krzeslach sceny; rozmowa z jednym = dosiadasz sie do calego stolu (rozmowa grupowa).
    /// DOWOD (dekompilacja 1.4.8): krzeslo = SandBox.Objects.Usables.Chair (UsableMachine, punkty AnimationPoint - siadanie po Agent.UseGameObject, AI);
    /// gracz sam siada na krzesle (Chair.GetActionTextForStandingPoint "Sit"). ConversationManager.AddConversationAgents(agents, true) dolacza agentow do trwajacej
    /// rozmowy (CampaignMission.OnConversationStart - animacje, AnimationPoint.OnUserConversationStart dla siedzacych); kto mowi linie - ConversationSentence.IsSpeaker
    /// (UpdateSpeakerAndListenerAgents; pole publiczne ConversationSentence.IsSpeaker ustawiane na linii zwroconej przez AddDialogLine). Linia NPC z
    /// wyjsciem na token z samymi liniami NPC - nastepna linia NPC po "dalej" (ContinueConversation -> ProcessPartnerSentence); token z liniami gracza - wybor.
    /// Stoly: punkty krzesel grupowane po odleglosci (do 1.9 m - jeden stol), stol od 2 miejsc; przy stole 2-4 bywalcow. Scenka: kwestie z pliku (rodzaj "table",
    /// region zajazdu, temat chwili - TavernLines; same teksty, bez nagran), mowione po kolei przez ludzi stolu. Na koncu: "Tell me more." (nastepna scenka; 1 za darmo
    /// na stol, kazda kolejka +1, do 3), "Let me buy this table a round (N d)." (kufel na glowe przy stole - cena kufla zajazdu; ta sama zaplata co kufel: kiesa wsi
    /// okregu, podatek pana - TownInns.DrinkPayRoad; zakaz wojny / rabunku jak kufel), "I'll leave you to your talk.".
    /// Klucz InnTableTalk (MCM "Inn Table Talk"); NIE = bywalcy stoja na npc_common jak w 474-P8, rozmowa z nimi "Not now, friend." (co do bitu jak 474-P8).
    /// Linie logu "Stoly zajazdu (474-P9): ..." - budowa (krzesla, stoly, siedzacy), kazda rozmowa (stol, ludzie, scenka), kolejka (zaplata, kiesa), wyjscie.
    /// </summary>
    internal sealed class InnTable
    {
        internal const string P = "Stoly zajazdu (474-P9): ";
        private const float Join = 1.9f;
        private const int MaxScenes = 3;
        internal static bool On { get { var s = Settings.Current; return s != null && s.InnTableTalk && RoadsideInnInterior.On; } }

        private readonly InnInteriorLogic _l;
        private readonly Dictionary<Agent, int> _tableOf = new Dictionary<Agent, int>();
        private readonly List<List<Agent>> _tables = new List<List<Agent>>();
        private readonly HashSet<string> _usedScenes = new HashSet<string>(StringComparer.Ordinal);
        private int _chairs, _seats, _tablesAll, _sitOk;
        // rozmowa (tylko na czas jednej rozmowy)
        private int _talkTable = -1, _idx, _scenesLeft;
        private List<string> _lines = new List<string>();
        private Agent _speaker;
        // liczniki wizyty
        private int _talks, _scenes, _rounds, _roundGold;

        internal InnTable(InnInteriorLogic l) { _l = l; }

        internal static InnTable Now { get { var l = InnInteriorLogic.Current; return l != null ? l.Tables : null; } }

        // ------------------------------------------------------------ budowa
        private sealed class SeatPt { public StandingPoint Sp; public Vec3 Pos; public int Group; }

        /// <summary>Sadza do want bywalcow przy stolach sceny. Zwraca liczbe posadzonych.</summary>
        internal int Seat(Mission m, int want, Random rng)
        {
            var seats = new List<SeatPt>();
            foreach (var mo in m.MissionObjects)
            {
                var ch = mo as Chair;
                if (ch == null || ch.StandingPoints == null) continue;
                _chairs++;
                foreach (var sp in ch.StandingPoints)
                {
                    if (sp == null || sp.IsDeactivated || sp.HasUser || !(sp is SandBox.Objects.AnimationPoints.AnimationPoint)) continue;
                    try { seats.Add(new SeatPt { Sp = sp, Pos = sp.GameEntity.GlobalPosition, Group = -1 }); }
                    catch (Exception e) { RoadsideInnInterior.Stumble("Table.Seat", e); }
                }
            }
            _seats = seats.Count;
            // grupy (pojedyncze wiazanie do Join metrow)
            var groups = new List<List<SeatPt>>();
            for (int i = 0; i < seats.Count; i++)
            {
                if (seats[i].Group >= 0) continue;
                var g = new List<SeatPt>();
                seats[i].Group = groups.Count;
                var q = new Queue<SeatPt>(); q.Enqueue(seats[i]);
                while (q.Count > 0)
                {
                    var s = q.Dequeue(); g.Add(s);
                    foreach (var o in seats)
                        if (o.Group < 0 && o.Pos.Distance(s.Pos) <= Join) { o.Group = s.Group; q.Enqueue(o); }
                }
                groups.Add(g);
            }
            var tables = new List<List<SeatPt>>();
            foreach (var g in groups) if (g.Count >= 2) tables.Add(g);
            _tablesAll = tables.Count;
            for (int i = tables.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var t = tables[i]; tables[i] = tables[j]; tables[j] = t; }
            int seated = 0;
            foreach (var t in tables)
            {
                if (seated >= want) break;
                int k = Math.Min(t.Count, Math.Min(want - seated, 2 + rng.Next(3)));
                if (k < 2 && seated > 0) break;
                for (int i = t.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var x = t[i]; t[i] = t[j]; t[j] = x; }
                var people = new List<Agent>();
                for (int i = 0; i < k; i++)
                {
                    var a = _l.SpawnAt(m, _l.PatronKind(), t[i].Sp.GameEntity.GetGlobalFrame(), "_villager", true, InnInteriorLogic.RolePatron);
                    if (a == null) continue;
                    if (InnInteriorLogic.UseSeat(a, t[i].Sp)) _sitOk++;
                    people.Add(a);
                    seated++;
                }
                if (people.Count == 0) continue;
                foreach (var a in people) _tableOf[a] = _tables.Count;
                _tables.Add(people);
            }
            return seated;
        }

        internal string BuildText()
        {
            return "stoly (474-P9): krzesel " + _chairs + ", miejsc " + _seats + ", stolow " + _tablesAll + ", zajete " + _tables.Count + ", siadanie " + _sitOk;
        }

        internal string ExitText()
        {
            return "stoly (474-P9): rozmowy " + _talks + ", scenki " + _scenes + ", kolejki " + _rounds + " (" + _roundGold + " d)";
        }

        internal int TableOf(Agent a) { int t; return a != null && _tableOf.TryGetValue(a, out t) ? t : -1; }

        // ------------------------------------------------------------ rozmowa
        private static Agent TalkAgent()
        {
            var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
            return cm != null ? cm.OneToOneConversationAgent as Agent : null;
        }

        internal static void AddDialogs(CampaignGameStarter starter)
        {
            try
            {
                starter.AddDialogLine("arm474_tb_sit", "start", "arm474_tb", "{=!}{ARM474_TB_SIT}", SitCond, SitCons, RoadsideInnInterior.TopPriority - 2);
                starter.AddDialogLine("arm474_tb_say", "arm474_tb", "arm474_tb", "{=!}{ARM474_TB_LINE}", SayMoreCond, SayCons, 110).IsSpeaker = IsSpeaker;
                starter.AddDialogLine("arm474_tb_last", "arm474_tb", "arm474_tb_opt", "{=!}{ARM474_TB_LINE}", SayLastCond, SayCons, 109).IsSpeaker = IsSpeaker;
                starter.AddDialogLine("arm474_tb_none", "arm474_tb", "arm474_tb_opt", "{=!}{ARM474_TB_LINE}", NoneCond, null, 100);
                starter.AddPlayerLine("arm474_tb_more", "arm474_tb_opt", "arm474_tb", "{=!}Tell me more.", MoreCond, MoreCons, 110, MoreClick);
                starter.AddPlayerLine("arm474_tb_round", "arm474_tb_opt", "arm474_tb_toast", "{=!}{ARM474_TB_ROUND}", RoundCond, RoundCons, 109, RoundClick);
                starter.AddDialogLine("arm474_tb_toast", "arm474_tb_toast", "arm474_tb_opt", "{=!}{ARM474_TB_TOAST}", ToastCond, null, 110).IsSpeaker = IsSpeaker;
                starter.AddPlayerLine("arm474_tb_leave", "arm474_tb_opt", "close_window", "{=!}I'll leave you to your talk.", null, EndCons, 100);
                Log.Info(P + "rozmowy stolow dodane | klucz Inn Table Talk " + (On ? "TAK" : "NIE") + ".");
            }
            catch (Exception e) { RoadsideInnInterior.Stumble("Table.AddDialogs", e); }
        }

        private static bool IsSpeaker(IAgent a)
        {
            var t = Now;
            return t != null && t._speaker != null && ReferenceEquals(a, t._speaker);
        }

        private static bool SitCond()
        {
            try
            {
                if (!On) return false;
                var t = Now;
                if (t == null || t.TableOf(TalkAgent()) < 0) return false;
                MBTextManager.SetTextVariable("ARM474_TB_SIT", TavernLines.Pick("sit", t._l.Region, "Pull up a stool, friend. We were just talking.").Replace("{INN}", t._l.InnName));
                return true;
            }
            catch (Exception e) { RoadsideInnInterior.Stumble("Table.SitCond", e); return false; }
        }

        /// <summary>Skutek linii powitania (raz): cala reszta stolu dolacza do rozmowy, pierwsza scenka.</summary>
        private static void SitCons()
        {
            try
            {
                var t = Now;
                var a = TalkAgent();
                if (t == null || a == null) return;
                t._talkTable = t.TableOf(a);
                t._scenesLeft = 2;   // pierwsza scenka + jedna za darmo; kazda kolejka +1 (do MaxScenes)
                t._talks++;
                var cm = Campaign.Current.ConversationManager;
                var others = new List<IAgent>();
                if (t._talkTable >= 0)
                    foreach (var o in t._tables[t._talkTable]) if (o != null && o != a && o.IsActive()) others.Add(o);
                if (others.Count > 0)
                {
                    try { cm.AddConversationAgents(others, true); }
                    catch (Exception e) { RoadsideInnInterior.Stumble("Table.Join", e); }
                }
                t.NextScene();
                Log.Info(P + "dosiadasz sie do stolu " + t._talkTable + " w " + t._l.InnName + " - ludzi przy stole " + (others.Count + 1) + ", w rozmowie " + cm.ConversationAgents.Count
                         + " | scenka " + t._lines.Count + " kwestii | rozmowa nr " + t._talks + " tej wizyty.");
            }
            catch (Exception e) { RoadsideInnInterior.Stumble("Table.SitCons", e); }
        }

        /// <summary>Nastepna scenka stolu: kwestie "table" regionu i chwili, bez powtorek w tej wizycie (wszystkie uzyte - od nowa); brak - wbudowana.</summary>
        private void NextScene()
        {
            _scenesLeft--;
            _idx = 0;
            _lines = new List<string>();
            var all = TavernLines.All("table", _l.Region);
            var fresh = new List<string>();
            foreach (var s in all) if (!_usedScenes.Contains(s)) fresh.Add(s);
            if (fresh.Count == 0 && all.Count > 0) { _usedScenes.Clear(); fresh = all; }
            string pick = fresh.Count > 0 ? fresh[MBRandom.RandomInt(fresh.Count)]
                        : "Roads are worse every year. || Roads? It's the tolls I mind. Every bridge has a lord's man on it now. || Drink up. Talking won't mend either.";
            _usedScenes.Add(pick);
            foreach (var x in pick.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries)) { var y = x.Trim(); if (y.Length > 0) _lines.Add(y.Replace("{INN}", _l.InnName)); }
            _scenes++;
        }

        private bool SetLine()
        {
            if (_idx < 0 || _idx >= _lines.Count || _talkTable < 0) return false;
            var people = _tables[_talkTable];
            _speaker = people.Count > 0 ? people[_idx % people.Count] : null;
            MBTextManager.SetTextVariable("ARM474_TB_LINE", _lines[_idx]);
            return true;
        }

        private static bool SayMoreCond() { try { var t = Now; return t != null && t._idx < t._lines.Count - 1 && t.SetLine(); } catch (Exception e) { RoadsideInnInterior.Stumble("Table.SayMore", e); return false; } }

        private static bool SayLastCond() { try { var t = Now; return t != null && t._idx == t._lines.Count - 1 && t.SetLine(); } catch (Exception e) { RoadsideInnInterior.Stumble("Table.SayLast", e); return false; } }

        private static bool NoneCond()
        {
            if (Now == null) return false;
            MBTextManager.SetTextVariable("ARM474_TB_LINE", "The talk at the table dies down.");
            return true;
        }

        private static void SayCons() { var t = Now; if (t != null) t._idx++; }

        private static bool MoreCond() { return Now != null; }

        private static bool MoreClick(out TextObject why)
        {
            var t = Now;
            if (t == null || t._scenesLeft <= 0) { why = new TextObject("{=!}They have talked themselves out - a round might loosen their tongues again."); return false; }
            why = new TextObject("{=!}They have more to tell.");
            return true;
        }

        private static void MoreCons() { try { var t = Now; if (t != null) t.NextScene(); } catch (Exception e) { RoadsideInnInterior.Stumble("Table.More", e); } }

        private int RoundPrice(out int heads)
        {
            heads = _talkTable >= 0 ? _tables[_talkTable].Count + 1 : 1;
            return Rumours.RoadTankardPrice(_l.Inn) * heads;
        }

        private static bool RoundCond()
        {
            var t = Now;
            if (t == null) return false;
            int heads, price = t.RoundPrice(out heads);
            MBTextManager.SetTextVariable("ARM474_TB_ROUND", "Let me buy this table a round (" + price + " d).");
            return true;
        }

        private static bool RoundClick(out TextObject why)
        {
            try
            {
                var t = Now;
                if (t == null || t._l.Inn == null) { why = new TextObject("{=!}Not now."); return false; }
                string barred = TownInns.RoadDrinkBarred(t._l.Inn);
                if (barred != null) { why = new TextObject("{=!}" + barred); return false; }
                int heads, price = t.RoundPrice(out heads);
                if (Hero.MainHero == null || Hero.MainHero.Gold < price) { why = new TextObject("{=!}A round for " + heads + " costs " + price + " d - you cannot pay it."); return false; }
                why = new TextObject("{=!}A tankard for everyone at the table and one for you: " + heads + " x " + (price / Math.Max(1, heads)) + " d. The coin goes to the keeper and the village whose lands the inn stands on.");
                return true;
            }
            catch (Exception e) { RoadsideInnInterior.Stumble("Table.RoundClick", e); why = new TextObject("{=!}Not now."); return false; }
        }

        private static void RoundCons()
        {
            try
            {
                var t = Now;
                var inn = t != null ? t._l.Inn : null;
                var vs = inn != null ? inn.Village : null;
                if (vs == null || vs.Village == null || Hero.MainHero == null) return;
                int heads, price = t.RoundPrice(out heads);
                int g0 = Hero.MainHero.Gold;
                long w0 = TownInns.DrinkWorldRoad(vs);
                int tax, crown;
                int paid = TownInns.DrinkPayRoad(vs, price, out tax, out crown);
                long diff = TownInns.DrinkWorldRoad(vs) + crown - w0;
                if (paid <= 0) { Log.Info(P + "kolejka w " + inn.Name + " - zaplata nie przeszla (cena " + price + ", zloto gracza " + g0 + ")."); MBTextManager.SetTextVariable("ARM474_TB_TOAST", "The keeper won't take your coin, it seems. Never mind - sit anyway."); return; }
                if (PayNotes.On) PayNotes.Paid("tankard", paid, " for a round at " + inn.Name + ".");
                t._rounds++; t._roundGold += paid;
                t._scenesLeft = Math.Min(MaxScenes, t._scenesLeft + 1);
                if (t._talkTable >= 0 && t._tables[t._talkTable].Count > 0) t._speaker = t._tables[t._talkTable][MBRandom.RandomInt(t._tables[t._talkTable].Count)];
                MBTextManager.SetTextVariable("ARM474_TB_TOAST", TavernLines.Pick("toast", t._l.Region, "Now that's a friend! Your health, stranger."));
                Log.Info(P + "kolejka w " + inn.Name + " (" + inn.Uid + ") przy stole " + t._talkTable + " - " + heads + " kufli, zaplacil " + paid + " (gracz " + g0 + " -> " + Hero.MainHero.Gold
                         + ") -> kiesa wsi " + vs.StringId + " " + (paid - tax - crown) + ", podatek pana " + tax + ", korona (DS) " + crown + " | kontrola obiegu: roznica " + diff
                         + " | kolejek tej wizyty " + t._rounds + ".");
            }
            catch (Exception e) { RoadsideInnInterior.Stumble("Table.Round", e); }
        }

        private static bool ToastCond() { var t = Now; if (t == null) return false; if (t._speaker == null && t._talkTable >= 0 && t._tables[t._talkTable].Count > 0) t._speaker = t._tables[t._talkTable][0]; return true; }

        private static void EndCons()
        {
            var t = Now;
            if (t == null) return;
            t._talkTable = -1; t._idx = 0; t._lines = new List<string>(); t._speaker = null;
        }
    }
}
