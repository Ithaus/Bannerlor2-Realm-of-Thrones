using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace Armoury
{
    /// <summary>
    /// m104-wojny (2) - REACH I DORNE Z KORONA JAK W KSIAZKACH (klucze CanonReachJoinsCrown i CanonDorneJoinsCrown; kazda kampania - klasyczna i z prologiem).
    ///
    /// REACH (po smierci Renly'ego: bohater ROT lord_1_9 martwy albo RenlyDeathEvent zakonczone - ta sama regula co CrashScribe CanonAlliances.RenlyGone): Reach rzadzony
    /// przez Tyrellow (clan_empire_west_1), bez sojuszu ze Smocza Skala, zadna strona bez wladzy gracza. Kolejnosc ma znaczenie - StartAlliance gry (dekompilacja 1.4.8)
    /// nie pyta o CanMakeAlliance ani o wojny, ale od razu sklada wnioski wezwan do wojny (ProposeCallToWarAgreementDecision) dla kazdej wojny jednej strony, w ktorej
    /// druga nie walczy:
    ///   1. Reach w wojnie z Dorne: pokoj (MakePeaceAction.Apply - Diplomacy go nie latuje; WorldClock7 widzi wolajacego). Wojna fabularna ROT Reach-Dorne (5/100, po
    ///      rozciagnieciu dni 22-433) w chwili smierci Renly'ego (~520-534) juz nie jest wymuszana; gdyby byla (inna skala osi) - jej wpis ROT dostaje IsEnabled = false
    ///      (ten sam przelacznik co okno ROT "Edit Storyline Wars"; zapisuje sie w zapisie ROT), dopiero potem pokoj.
    ///   2. Korona w wojnie z Reach (wojna ROT FIVE_KINGS_WESTERLANDS_REACH wylaczona przez ROT OnRenlysDeath, ale moze trwac): pokoj, gdy ROT jej nie wymusza.
    ///   3. SOJUSZ gry korona + Reach (IAllianceCampaignBehavior.StartAlliance przez interfejs - latki CrownCallToWar / CanonAlliances widza wszystko jak przy sojuszu AI).
    ///      CrashScribe CanonAlliances dopuszcza Korona + Reach po smierci Renly'ego (nie zerwie go przy wczytaniu).
    ///   4. Koniec sojuszu (gra: 84 dni) przesuwany na +364 dni, gdy zostalo mniej niz 182 (prywatne UpdateAllianceEndTime - tylko przedluza; jak Prologue.Peace).
    ///   5. Pakt o nieagresji Diplomacy Reach-Dorne (FormNonAggressionPactAction bez kosztow i pytania gracza, do ~250 dni po dniu najazdu Daenerys) - trzyma pokoj
    ///      Reach-Dorne przed decyzjami AI o wojnie (Diplomacy blokuje nim tylko DeclareWarConditions, nie DeclareWarAction - wezwania i ROT go omijaja; dlatego tez
    ///      krok 1 co dobe).
    ///   Pilnowane co dobe (brak sojuszu - od nowa, linia ODNOWIONY; wojna Reach-Dorne - pokoj od nowa) az do najazdu Daenerys.
    /// DORNE (po zareczynach Myrcelli z Trystanem): w ksiazce Tyrion zareczyl Myrcelle na poczatku "Starcia kroli", ona odplywa przed smiercia Renly'ego. Na osi Fabuli
    ///   (CrashScribe Fabula.Timeline, dzien ROT przed rozciagnieciem) Dorzecze wchodzi do wojny w dniu 25, Renly ginie w 120 - wybrany dzien 70 x skala osi
    ///   (CrashScribe Config.FabulaTimeScale odbiciem, zapas rok/84), przy 4.33 ok. dzien opowiesci 303 (Kronika nr 19: dni 215-390). Warunek: Myrcella (lord_4_10)
    ///   zywa, Dorne rzadzone przez Martellow (clan_aserai_1), bez wladzy gracza. Wedlug kanonu Dorne formalnie stoi przy koronie, ale nie walczy: pokoj (gdy ROT nie
    ///   wymusza wojny) i PAKT O NIEAGRESJI Diplomacy korona-Dorne - nie sojusz (CanonAlliances nie ma Korona + Dorne na liscie - zerwalby go przy wczytaniu; StartAlliance
    ///   wciagnalby Dorne wnioskami wezwan do wojen korony). Pilnowany co dobe.
    /// KONIEC: najazd Daenerys (DanyInvasionEvent ruszyl - ROT wlacza wojne DANY_INVASION_DORNE_WESTERLANDS): nasze pakty Dorne (korona-Dorne, Reach-Dorne) wygaszone
    ///   (Expire Diplomacy), sojusz Reach-korona bez pilnowania (trwa do swojego terminu, potem wedlug gry). Raz (stan w zapisie).
    /// Klucz wylaczony: nic nie zakladamy; nasze pakty tej czesci wygaszone raz (dlugie terminy); sojusz zostaje do swojego terminu.
    /// Bez moda Diplomacy: bez paktow (sam pokoj i sojusz).
    /// </summary>
    internal static partial class Prologue
    {
        private const string ReachId = "empire_w", DorneId = "aserai", DragonstoneId = "dragonstone";
        private const string TyrellClanId = "clan_empire_west_1", MartellClanId = "clan_aserai_1";
        private const string RenlyHeroId = "lord_1_9", MyrcellaHeroId = "lord_4_10";
        private const float MyrcellaRotDay = 70f;        // dzien osi ROT (przed rozciagnieciem): miedzy wejsciem Dorzecza do wojny (25) a smiercia Renly'ego (120)
        private const float DanyInvasionRotDay = 450f;   // CrashScribe Fabula.Timeline: DanyInvasionEvent
        private const float AllianceKeepDays = 364f, AllianceRenewBelow = 182f;
        private const float NapAfterInvasionDays = 250f, NapMinDays = 364f;

        private static bool ReachKey { get { var s = S; return s != null && s.CanonReachJoinsCrown; } }
        private static bool DorneKey { get { var s = S; return s != null && s.CanonDorneJoinsCrown; } }

        // stan (zapis arm_m104)
        private static bool _rcHeld, _rnOurs, _cdOurs, _crownEnded;
        private static int _rcDay, _cdDay, _crownEndDay;
        private const int NoDay = -99999;
        // doba (bez zapisu)
        private static string _reachState = "-", _dorneState = "-";

        private static void ResetCrown()
        {
            _rcHeld = false; _rnOurs = false; _cdOurs = false; _crownEnded = false;
            _rcDay = NoDay; _cdDay = NoDay; _crownEndDay = NoDay;
            _reachState = "-"; _dorneState = "-";
        }

        private static string PackCrown()
        {
            return "rc=" + (_rcHeld ? "1" : "0") + ";rcd=" + _rcDay.ToString(Inv) + ";rn=" + (_rnOurs ? "1" : "0") + ";cd=" + (_cdOurs ? "1" : "0")
                   + ";cdd=" + _cdDay.ToString(Inv) + ";ce=" + (_crownEnded ? "1" : "0") + ";ced=" + _crownEndDay.ToString(Inv) + ";";
        }

        private static void UnpackCrown(Dictionary<string, string> map)
        {
            ResetCrown();
            string v;
            _rcHeld = map.TryGetValue("rc", out v) && v == "1";
            _rnOurs = map.TryGetValue("rn", out v) && v == "1";
            _cdOurs = map.TryGetValue("cd", out v) && v == "1";
            _crownEnded = map.TryGetValue("ce", out v) && v == "1";
            _rcDay = ParseInt(map, "rcd", NoDay);
            _cdDay = ParseInt(map, "cdd", NoDay);
            _crownEndDay = ParseInt(map, "ced", NoDay);
        }

        // ---------------------------------------------------------------- os opowiesci

        private static bool _scaleTried;
        private static FieldInfo _fScale;

        /// <summary>Skala osi Fabuli (CrashScribe Config.FabulaTimeScale odbiciem, przycieta 0.25-6 jak Fabula.ScaleK); bez CrashScribe - rok kalendarza / 84.</summary>
        internal static float StoryScale()
        {
            float k;
            try
            {
                if (!_scaleTried)
                {
                    _scaleTried = true;
                    var t = AccessTools.TypeByName("CrashScribe.Config");
                    _fScale = t != null ? AccessTools.Field(t, "FabulaTimeScale") : null;
                }
                k = _fScale != null ? Convert.ToSingle(_fScale.GetValue(null), Inv) : CampaignTime.DaysInYear / 84f;
            }
            catch { k = 4.33f; }
            if (float.IsNaN(k) || float.IsInfinity(k)) k = 4.33f;
            return Math.Max(0.25f, Math.Min(6f, k));
        }

        /// <summary>Dzien opowiesci najazdu Daenerys na osi Fabuli (450 x skala; przy 4.33 - 1948.5).</summary>
        internal static float DanyInvasionDay() { return DanyInvasionRotDay * StoryScale(); }

        /// <summary>Dzien opowiesci zareczyn Myrcelli (70 x skala; przy 4.33 - 303).</summary>
        private static float MyrcellaDay() { return MyrcellaRotDay * StoryScale(); }

        // ---------------------------------------------------------------- wydarzenia ROT (stan bez pamieci doby)

        private static bool RotEventsReady()
        {
            if (_tEvBeh == null)
            {
                _tEvBeh = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTEventBehavior");
                var tBase = AccessTools.TypeByName("ROT.Events.EventBase");
                if (_tEvBeh == null || tBase == null) { _tEvBeh = null; return false; }
                _fEvents = AccessTools.Field(_tEvBeh, "_events");
                _pTotal = AccessTools.Property(tBase, "TotalEvents");
                _pIsActive = AccessTools.Property(tBase, "IsActive");
            }
            return _fEvents != null && _pTotal != null && _pIsActive != null;
        }

        /// <summary>Stan wydarzenia ROT po nazwie typu: -1 = nie widac, 0 = czeka, 1 = trwa, 2 = zakonczone (jak CrashScribe Fabula.EventState).</summary>
        internal static int RotEventState(string typeName)
        {
            try
            {
                if (Campaign.Current == null || !RotEventsReady()) return -1;
                var mi = typeof(Campaign).GetMethod("GetCampaignBehavior");
                var beh = mi != null ? mi.MakeGenericMethod(_tEvBeh).Invoke(Campaign.Current, null) : null;
                var list = beh != null ? _fEvents.GetValue(beh) as System.Collections.IEnumerable : null;
                if (list == null) return -1;
                foreach (var ev in list)
                {
                    if (ev == null || ev.GetType().Name != typeName) continue;
                    bool act = Convert.ToBoolean(_pIsActive.GetValue(ev, null), Inv);
                    int total = Convert.ToInt32(_pTotal.GetValue(ev, null), Inv);
                    return act ? 1 : (total > 0 ? 2 : 0);
                }
                return -1;
            }
            catch (Exception e) { WStumble("RotEventState", e); return -1; }
        }

        private static Hero HeroAny(string id)
        {
            try { return Hero.FindFirst(x => x != null && x.StringId == id); } catch { return null; }
        }

        /// <summary>Renly nie zyje: bohater lord_1_9 martwy; bez bohatera - RenlyDeathEvent zakonczone (CrashScribe CanonAlliances.RenlyGone).</summary>
        private static bool RenlyGone()
        {
            var h = HeroAny(RenlyHeroId);
            return h != null ? !h.IsAlive : RotEventState("RenlyDeathEvent") == 2;
        }

        private static bool DanyInvaded() { return RotEventState("DanyInvasionEvent") >= 1; }

        // ---------------------------------------------------------------- pomocnicze

        private static bool _updTried;

        /// <summary>Prywatne AllianceCampaignBehavior.UpdateAllianceEndTime (tylko przedluza) - w kampanii klasycznej ApplyPeace sie nie wpina, wiec leniwie.</summary>
        private static MethodInfo UpdateEnd()
        {
            if (_mUpdateEnd == null && !_updTried)
            {
                _updTried = true;
                try { _mUpdateEnd = AccessTools.DeclaredMethod(typeof(AllianceCampaignBehavior), "UpdateAllianceEndTime", new[] { typeof(Kingdom), typeof(Kingdom), typeof(CampaignTime) }); }
                catch { _mUpdateEnd = null; }
            }
            return _mUpdateEnd;
        }

        private static bool Alive(Kingdom k) { return k != null && !k.IsEliminated; }

        private static string Ruler(Kingdom k) { try { return k != null && k.RulingClan != null ? k.RulingClan.StringId ?? "" : ""; } catch { return ""; } }

        /// <summary>Wpis ROT wojny fabularnej pary (a, b), ktory teraz wymusza wojne - IsEnabled = false. Liczba wylaczonych.</summary>
        private static int DisableRotWar(IFaction a, IFaction b)
        {
            int n = 0;
            if (_pWEnabled == null) return 0;
            foreach (var war in RotWars())
            {
                try
                {
                    var f1 = WF1(war); var f2 = WF2(war);
                    if (!((f1 == a && f2 == b) || (f1 == b && f2 == a))) continue;
                    if (_pWCustom != null && (bool)_pWCustom.GetValue(war, null)) continue;
                    if (!(bool)_mWEnforced.Invoke(war, new object[] { null })) continue;
                    _pWEnabled.SetValue(war, false, null);
                    n++;
                }
                catch (Exception e) { WStumble("DisableRotWar", e); }
            }
            return n;
        }

        private static float NapDays(double sd) { return (float)Math.Max(NapMinDays, DanyInvasionDay() + NapAfterInvasionDays - sd); }

        /// <summary>Pakt o nieagresji Diplomacy a-b (bez kosztow, bez pytania gracza). true = jest po wywolaniu.</summary>
        private static bool MakeNap(Kingdom a, Kingdom b, double sd)
        {
            if (!DipReady()) return false;
            if (NapOf(a, b) != null) return true;
            _mNapApply.Invoke(null, new object[] { a, b, false, true, (float?)NapDays(sd), false });
            return NapOf(a, b) != null;
        }

        private static int ExpireNap(Kingdom a, Kingdom b)
        {
            if (a == null || b == null || !DipReady() || _mExpire == null) return 0;
            var p = NapOf(a, b);
            if (p == null) return 0;
            _mExpire.Invoke(p, null);
            return 1;
        }

        private static string Did(List<string> did) { return did.Count > 0 ? " [" + string.Join("; ", did.ToArray()) + "]" : ""; }

        // ---------------------------------------------------------------- doba

        /// <summary>Doba (CanonWarsBehavior.OnDaily): Reach i Dorne z korona - zalozenie i pilnowanie, koniec przy najezdzie Daenerys, wylaczony klucz.</summary>
        private static void CrownDaily()
        {
            double sd = StoryDays();
            // klucz wylaczony - nasze pakty tej czesci wygaszone (raz)
            if (!ReachKey && _rnOurs) { int n = 0; try { n = ExpireNap(K(ReachId), K(DorneId)); } catch (Exception e) { WStumble("ExpireNap Reach", e); } _rnOurs = false; _rcHeld = false; _warsEventDay = true;
                Log.Info(WTag + "klucz Canon Reach Joins Crown NIE - nasz pakt Reach-Dorne wygaszony " + n + ", sojusz Reach-korona bez pilnowania (trwa do swojego terminu)."); }
            if (!DorneKey && _cdOurs) { int n = 0; try { n = ExpireNap(K(Crown), K(DorneId)); } catch (Exception e) { WStumble("ExpireNap Dorne", e); } _cdOurs = false; _warsEventDay = true;
                Log.Info(WTag + "klucz Canon Dorne Joins Crown NIE - nasz pakt korona-Dorne wygaszony " + n + "."); }
            if (!ReachKey && !DorneKey) { _reachState = "klucz NIE"; _dorneState = "klucz NIE"; return; }
            if (_crownEnded) { _reachState = _dorneState = "zakonczone (najazd Daenerys, dzien opowiesci " + _crownEndDay.ToString(Inv) + ")"; return; }
            if (DanyInvaded()) { EndCrown("DanyInvasionEvent ruszyl", sd); return; }
            var crown = K(Crown); var reach = K(ReachId); var dorne = K(DorneId);
            var beh = Campaign.Current != null ? Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>() : null;

            // REACH
            try
            {
                if (!ReachKey) _reachState = "klucz NIE";
                else if (!Alive(crown) || !Alive(reach)) _reachState = "brak krolestwa (korona " + (Alive(crown) ? "jest" : "BRAK") + ", Reach " + (Alive(reach) ? "jest" : "BRAK") + ")";
                else if (!RenlyGone()) _reachState = "czeka (Renly zyje)";
                else if (PlayerRules(crown) || PlayerRules(reach)) _reachState = "krolestwo gracza - poza regula";
                else if (Ruler(reach) != TyrellClanId) _reachState = "czeka (Reach bez Tyrellow: " + Ruler(reach) + ")";
                else if (beh != null && Alive(K(DragonstoneId)) && beh.IsAllyWithKingdom(reach, K(DragonstoneId))) _reachState = "czeka (Reach w sojuszu ze Smocza Skala)";
                else _reachState = HoldReach(crown, reach, dorne, beh, sd);
            }
            catch (Exception e) { WStumble("HoldReach", e); _reachState = "BLAD (" + e.GetType().Name + ")"; }

            // DORNE
            try
            {
                float md = MyrcellaDay();
                var myr = HeroAny(MyrcellaHeroId);
                if (!DorneKey) _dorneState = "klucz NIE";
                else if (!Alive(crown) || !Alive(dorne)) _dorneState = "brak krolestwa (korona " + (Alive(crown) ? "jest" : "BRAK") + ", Dorne " + (Alive(dorne) ? "jest" : "BRAK") + ")";
                else if (sd < md && !_cdOurs) _dorneState = "czeka (zareczyny Myrcelli w dniu opowiesci " + ((int)md).ToString(Inv) + ")";
                else if (!_cdOurs && (myr == null || !myr.IsAlive)) _dorneState = "nie bedzie (Myrcella " + (myr == null ? "nieobecna" : "nie zyje") + " - bez zareczyn)";
                else if (PlayerRules(crown) || PlayerRules(dorne)) _dorneState = "krolestwo gracza - poza regula";
                else if (Ruler(dorne) != MartellClanId) _dorneState = "czeka (Dorne bez Martellow: " + Ruler(dorne) + ")";
                else _dorneState = HoldDorne(crown, dorne, beh, sd);
            }
            catch (Exception e) { WStumble("HoldDorne", e); _dorneState = "BLAD (" + e.GetType().Name + ")"; }
        }

        private static string HoldReach(Kingdom crown, Kingdom reach, Kingdom dorne, IAllianceCampaignBehavior beh, double sd)
        {
            var did = new List<string>();
            int day = (int)Math.Floor(sd);
            // 1. pokoj Reach-Dorne PRZED sojuszem - inaczej StartAlliance zlozy wniosek wezwania korony przeciw Dorne
            if (Alive(dorne) && reach.IsAtWarWith(dorne))
            {
                if (WarForced(reach, dorne)) did.Add("wojna ROT Reach-Dorne wymuszona - wpisow ROT wylaczonych " + DisableRotWar(reach, dorne));
                if (!WarForced(reach, dorne))
                {
                    MakePeaceAction.Apply(reach, dorne);
                    did.Add(reach.IsAtWarWith(dorne) ? "pokoj Reach-Dorne NIE wszedl" : (_rcHeld ? "ponowny pokoj Reach-Dorne" : "pokoj Reach-Dorne"));
                }
            }
            // 2. pokoj korona-Reach (ROT OnRenlysDeath wylacza swoja wojne korona-Reach, ale stan wojny moze trwac)
            if (crown.IsAtWarWith(reach))
            {
                if (WarForced(crown, reach)) { if (did.Count > 0) _warsEventDay = true; return "czeka (wojna ROT korona-Reach wymuszona)" + Did(did); }
                MakePeaceAction.Apply(crown, reach);
                did.Add("pokoj korona-Reach");
                if (crown.IsAtWarWith(reach)) { _warsEventDay = true; return "czeka (pokoj korona-Reach nie wszedl)" + Did(did); }
            }
            if (beh == null) return "brak AllianceCampaignBehavior" + Did(did);
            // 3. sojusz
            bool newA = false;
            if (!beh.IsAllyWithKingdom(crown, reach))
            {
                beh.StartAlliance(crown, reach);
                newA = beh.IsAllyWithKingdom(crown, reach);
                if (!newA) { _warsEventDay = true; Log.Info(WTag + "Reach z korona: SOJUSZ NIE wszedl (StartAlliance bez skutku)" + Did(did) + "."); return "SOJUSZ NIE wszedl" + Did(did); }
            }
            // 4. koniec sojuszu
            string end = "";
            try
            {
                double left = (beh.GetAllianceEndDate(crown, reach) - CampaignTime.Now).ToDays;
                if (left < AllianceRenewBelow && UpdateEnd() != null && beh is AllianceCampaignBehavior)
                {
                    _mUpdateEnd.Invoke(beh, new object[] { crown, reach, CampaignTime.Now + CampaignTime.Days(AllianceKeepDays) });
                    double left2 = (beh.GetAllianceEndDate(crown, reach) - CampaignTime.Now).ToDays;
                    if (!newA) did.Add("koniec sojuszu przesuniety z " + left.ToString("0", Inv) + " na " + left2.ToString("0", Inv) + " dni");
                    left = left2;
                }
                end = "do dnia opowiesci " + (sd + left).ToString("0", Inv);
            }
            catch (Exception e) { WStumble("AllianceEnd", e); end = "koniec ?"; }
            // 5. pakt Reach-Dorne (trzyma pokoj przed decyzjami AI)
            string nap = "-";
            if (Alive(dorne))
            {
                if (reach.IsAtWarWith(dorne)) nap = "wojna - bez paktu";
                else if (beh.IsAllyWithKingdom(reach, dorne)) nap = "sojusz (AI) - pakt zbedny";
                else if (!DipReady()) nap = "brak moda Diplomacy";
                else if (NapOf(reach, dorne) != null) nap = "jest";
                else if (MakeNap(reach, dorne, sd)) { _rnOurs = true; nap = "ZALOZONY"; did.Add("pakt Reach-Dorne"); }
                else nap = "NIE wszedl";
            }
            if (newA)
            {
                _warsEventDay = true;
                if (!_rcHeld)
                {
                    _rcDay = day;
                    Log.Info(WTag + "REACH Z KORONA (dzien opowiesci " + sd.ToString("0.0", Inv) + ", po smierci Renly'ego): SOJUSZ korona-Reach ZALOZONY (" + end + ")"
                             + " | pakt Reach-Dorne " + nap + Did(did) + " | wnioski wezwan do wojny sklada gra (StartAlliance) - korona w wojnie z " + Wars(crown) + "; Reach w wojnie z " + Wars(reach) + ".");
                }
                else Log.Info(WTag + "Reach z korona: sojusz ODNOWIONY (dzien opowiesci " + sd.ToString("0.0", Inv) + ", " + end + ")" + Did(did) + ".");
            }
            else if (did.Count > 0)
            {
                _warsEventDay = true;
                Log.Info(WTag + "Reach z korona: pilnowanie (dzien opowiesci " + sd.ToString("0.0", Inv) + ")" + Did(did) + " | pakt Reach-Dorne " + nap + ".");
            }
            _rcHeld = true;
            if (_rcDay == NoDay) _rcDay = day;   // sojusz zastany (np. AI po smierci Renly'ego) - pilnowany od dzis
            return "SOJUSZ TAK od dnia " + (_rcDay != NoDay ? _rcDay.ToString(Inv) : "?") + " (" + end + "), pakt Reach-Dorne " + nap + ", Reach-Dorne " + (Alive(dorne) && reach.IsAtWarWith(dorne) ? "WOJNA" : "pokoj");
        }

        private static string HoldDorne(Kingdom crown, Kingdom dorne, IAllianceCampaignBehavior beh, double sd)
        {
            var did = new List<string>();
            int day = (int)Math.Floor(sd);
            if (crown.IsAtWarWith(dorne))
            {
                if (WarForced(crown, dorne)) return "czeka (wojna ROT korona-Dorne wymuszona)";
                MakePeaceAction.Apply(crown, dorne);
                did.Add(_cdOurs ? "ponowny pokoj korona-Dorne" : "pokoj korona-Dorne");
                if (crown.IsAtWarWith(dorne)) { _warsEventDay = true; return "czeka (pokoj korona-Dorne nie wszedl)" + Did(did); }
            }
            string nap;
            bool made = false;
            if (beh != null && beh.IsAllyWithKingdom(crown, dorne)) nap = "sojusz (AI) - pakt zbedny";
            else if (!DipReady()) nap = "brak moda Diplomacy - bez paktu";
            else if (NapOf(crown, dorne) != null) nap = "jest";
            else if (MakeNap(crown, dorne, sd)) { made = true; nap = "ZALOZONY"; }
            else nap = "NIE wszedl";
            bool first = made && !_cdOurs;
            if (made) _cdOurs = true;
            if (first || (_cdOurs && _cdDay == NoDay))
            {
                _cdDay = day;
                _warsEventDay = true;
                Log.Info(WTag + "DORNE Z KORONA (dzien opowiesci " + sd.ToString("0.0", Inv) + ", zareczyny Myrcelli z Trystanem - dzien osi " + ((int)MyrcellaDay()).ToString(Inv)
                         + "): PAKT o nieagresji korona-Dorne " + nap + " (Diplomacy, " + NapDays(sd).ToString("0", Inv) + " dni)" + Did(did) + " | Dorne w wojnie z " + Wars(dorne) + ".");
            }
            else if (made || did.Count > 0)
            {
                _warsEventDay = true;
                Log.Info(WTag + "Dorne z korona: pilnowanie (dzien opowiesci " + sd.ToString("0.0", Inv) + ") - pakt " + nap + Did(did) + ".");
            }
            return "PAKT " + nap + (_cdDay != NoDay ? " (od dnia " + _cdDay.ToString(Inv) + ")" : "") + ", korona-Dorne " + (crown.IsAtWarWith(dorne) ? "WOJNA" : "pokoj");
        }

        private static void EndCrown(string why, double sd)
        {
            int n = 0;
            try
            {
                if (_cdOurs) n += ExpireNap(K(Crown), K(DorneId));
                if (_rnOurs) n += ExpireNap(K(ReachId), K(DorneId));
            }
            catch (Exception e) { WStumble("EndCrown", e); }
            _crownEnded = true; _crownEndDay = (int)Math.Floor(sd);
            _cdOurs = false; _rnOurs = false; _rcHeld = false;
            _warsEventDay = true;
            _reachState = _dorneState = "zakonczone (najazd Daenerys, dzien opowiesci " + _crownEndDay.ToString(Inv) + ")";
            Log.Info(WTag + "NAJAZD DAENERYS (" + why + ", dzien opowiesci " + sd.ToString("0.0", Inv) + ") - nasze pakty Dorne wygaszone " + n
                     + ", sojusz Reach-korona bez pilnowania (trwa do swojego terminu, potem wedlug gry).");
        }

        private static string Wars(Kingdom k)
        {
            try
            {
                var l = new List<string>();
                foreach (var f in k.FactionsAtWarWith) if (f != null && f.IsKingdomFaction && l.Count < 8) l.Add(f.StringId);
                return l.Count == 0 ? "nikim" : string.Join(", ", l.ToArray());
            }
            catch { return "?"; }
        }

        /// <summary>Opis (2) do linii startu sesji (bez dzialania).</summary>
        private static string CrownStartText()
        {
            double sd = StoryDays();
            string dip = DipReady() ? "TAK" : "BRAK";
            return "klucze Reach " + (ReachKey ? "TAK" : "NIE") + ", Dorne " + (DorneKey ? "TAK" : "NIE") + " | Diplomacy " + dip + " | koniec sojuszu (UpdateAllianceEndTime) "
                   + (UpdateEnd() != null ? "TAK" : "BRAK") + " | Renly " + (RenlyGone() ? "NIE ZYJE" : "zyje")
                   + " | Reach z korona: " + (_crownEnded ? "zakonczone (dzien " + _crownEndDay.ToString(Inv) + ")" : _rcHeld ? "TAK od dnia " + _rcDay.ToString(Inv) : "czeka")
                   + (_rnOurs ? ", nasz pakt Reach-Dorne" : "")
                   + " | Dorne z korona: " + (_crownEnded ? "zakonczone" : _cdOurs ? "PAKT od dnia " + _cdDay.ToString(Inv) : "czeka (zareczyny Myrcelli w dniu " + ((int)MyrcellaDay()).ToString(Inv) + ")")
                   + " | najazd Daenerys na osi dnia " + ((int)DanyInvasionDay()).ToString(Inv) + (DanyInvaded() ? " (RUSZYL)" : "")
                   + " | dzien opowiesci " + sd.ToString("0.0", Inv);
        }
    }
}
