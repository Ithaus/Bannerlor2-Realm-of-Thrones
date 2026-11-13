using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;

namespace Armoury
{
    /// <summary>
    /// 474-P6 (Jeff 13.11, projekt docs/PROJEKT-WSIE-POBOCZNE-KARCZMY-2026-11-13.md pkt 1 "To samo okno co we wsi glownej", paczka 6): pobor z wioski pobocznej w OKNIE
    /// WERBUNKU GRY (to samo co u notabli wsi glownej) zamiast listy wyboru: wiersz "Men of X" z portretem soltysa wsi okregu i - na ziemi rodu (474-P5) - wiersz
    /// "House Y" z portretem glowy rodu; kazdy slot to jeden czlowiek z puli wioski, cena slotu = nasza cena (etap, rod), koszyk, "Recruit all", Reset jak w grze.
    /// DOWOD (dekompilacja 1.4.8): GauntletMenuRecruitVolunteersView.OnInitialize -> new RecruitmentVM() (ctor: RefreshScreen tylko gdy Settlement.CurrentSettlement
    /// != null) -> _dataSource.RefreshScreen() WPROST (iteruje Settlement.CurrentSettlement.Notables - przy wiosce pobocznej osady nie ma: NRE) -> Enabled = true;
    /// OnFrameTick: Confirm -> ExecuteDone, Exit -> ExecuteForceQuit (z koszykiem: ExecuteReset + ExecuteDone), !Enabled -> CloseRecruitVolunteers.
    /// ExecuteDone -> OnDone: OnDone ma TRZY latki (BK RecruitmentOnDonePatch - prefiks, sam werbuje, DeduceManpower osady, zwraca false; DTE RecruitmentPatch -
    /// prefiks+postfiks, komplet do zbrojowni; nasze PlayerRecruit.VolPre i PeopleBook.PlayerWinPre - Priority.First) i pisze OwnerHero.VolunteerTypes[slot] = null.
    /// MenuViewContext.AddMenuView wola OnInitialize synchronicznie (OpenRecruitVolunteers wraca z gotowym VM).
    /// LATKI (Harmony "com.jeff.armoury.474screen", raz na proces w kampanii, AccessTools.DeclaredMethod na RecruitmentVM, sprawdzenie GetPatchInfo):
    ///  - prefiks RefreshScreen: TYLKO dla VM otwartego przez nas (_scr.Vm - wiazany przy pierwszym RefreshScreen nowego VM w 3 s od naszego otwarcia) - nasze
    ///    wiersze, oryginal pominiety; kazdy inny VM (miasto, wies gry) - oryginal jak dotad;
    ///  - prefiks ExecuteDone (PRZED OnDone i jego 3 latkami - te w naszym oknie nie biegna wcale): koszyk -> SideVillageVisit.LevyDo (rod, potem chlopi) - ta sama
    ///    droga co lista wyboru (bramki, DOM, pula, brak rak, pochodzenie, komplet DTE tieru 1, zaplata rodzinom), potem Deactivate; VolunteerTypes notabli nietkniete;
    ///  - postfiks OnFinalize: koniec wiazania.
    /// Potkniecia per okno (licznik, Log.Error raz na miejsce) - nic nie gasnie; okno sie nie otworzylo - lista wyboru jak dotad. Klucz NIE - lista wyboru jak dotad.
    /// </summary>
    internal static partial class SideVillageVisit
    {
        private const string SP = "Okno werbunku wioski (474-P6): ";
        private sealed class ScrCtx
        {
            public Settlement St; public string Uid; public bool Fief; public int Tick;
            public RecruitmentVM Vm; public RecruitVolunteerVM RowV, RowH; public int NV, NH, Builds;
        }
        private static ScrCtx _scr;
        private static bool _scrTried, _scrPatched; private static string _scrHook = "nie wpiete";
        private static Harmony _scrH;
        private static MethodInfo _miOnRecruit, _miOnRemove, _miRefreshParty, _miRecruitAll;
        private static int _scrOpen, _scrDone, _scrFail, _scrSt;
        private static readonly HashSet<string> _scrErr = new HashSet<string>(StringComparer.Ordinal);

        internal static bool ScreenKey { get { var s = Settings.Current; return s != null && s.SideVillageRecruit && s.SideVillageRecruitScreen; } }

        private static void ScrStumble(string where, Exception e)
        {
            _scrSt++;
            try { if (_scrErr.Add(where)) Log.Error("SideVillageVisit.Screen." + where + " (potkniecie " + _scrSt + ")", e); } catch { }
        }

        /// <summary>W kampanii (OnSessionLaunched), raz na proces: trzy latki na RecruitmentVM. Brak ktorejkolwiek - okno gry nieczynne (lista wyboru jak dotad).</summary>
        internal static void ScreenEnsurePatch()
        {
            if (_scrTried) { Log.Info(SP + "latki " + _scrHook + " | klucz Side Village Recruit Screen " + (ScreenKey ? "TAK" : "NIE") + " | otwarte " + _scrOpen + ", zatwierdzone " + _scrDone + ", nieudane " + _scrFail + ", potkniecia " + _scrSt + "."); return; }
            _scrTried = true;
            try
            {
                var t = typeof(RecruitmentVM);
                var mRef = AccessTools.DeclaredMethod(t, "RefreshScreen", Type.EmptyTypes);
                var mDone = AccessTools.DeclaredMethod(t, "ExecuteDone", Type.EmptyTypes);
                var mFin = AccessTools.DeclaredMethod(t, "OnFinalize", Type.EmptyTypes);
                _miOnRecruit = AccessTools.DeclaredMethod(t, "OnRecruit", new[] { typeof(RecruitVolunteerVM), typeof(RecruitVolunteerTroopVM) });
                _miOnRemove = AccessTools.DeclaredMethod(t, "OnRemoveFromCart", new[] { typeof(RecruitVolunteerVM), typeof(RecruitVolunteerTroopVM) });
                _miRefreshParty = AccessTools.DeclaredMethod(t, "RefreshPartyProperties", Type.EmptyTypes);
                _miRecruitAll = AccessTools.DeclaredMethod(t, "UpdateRecruitAllProperties", Type.EmptyTypes);
                if (mRef == null || mDone == null || mFin == null || _miOnRecruit == null || _miOnRemove == null || _miRefreshParty == null)
                {
                    _scrHook = "BRAK metod RecruitmentVM (RefreshScreen " + (mRef != null) + ", ExecuteDone " + (mDone != null) + ", OnFinalize " + (mFin != null) + ", OnRecruit " + (_miOnRecruit != null)
                               + ", OnRemoveFromCart " + (_miOnRemove != null) + ", RefreshPartyProperties " + (_miRefreshParty != null) + ") - lista wyboru jak dotad";
                }
                else
                {
                    _scrH = _scrH ?? new Harmony("com.jeff.armoury.474screen");
                    _scrH.Patch(mRef, prefix: new HarmonyMethod(typeof(SideVillageVisit), nameof(ScrRefreshPre)) { priority = Priority.First });
                    _scrH.Patch(mDone, prefix: new HarmonyMethod(typeof(SideVillageVisit), nameof(ScrDonePre)) { priority = Priority.First });
                    _scrH.Patch(mFin, postfix: new HarmonyMethod(typeof(SideVillageVisit), nameof(ScrFinPost)));
                    int ok = (ScrHas(mRef, true) ? 1 : 0) + (ScrHas(mDone, true) ? 1 : 0) + (ScrHas(mFin, false) ? 1 : 0);
                    _scrPatched = ok == 3;
                    _scrHook = _scrPatched ? "wpiete 3/3 (prefiks RecruitmentVM.RefreshScreen i ExecuteDone - przed OnDone z latkami BK, DTE i naszymi; postfiks OnFinalize)"
                                           : "NIE weszly (" + ok + "/3 po Patch) - lista wyboru jak dotad";
                }
            }
            catch (Exception e) { _scrHook = "wyjatek " + e.GetType().Name + " - lista wyboru jak dotad"; ScrStumble("EnsurePatch", e); }
            Log.Info(SP + "latki " + _scrHook + " | klucz Side Village Recruit Screen " + (ScreenKey ? "TAK" : "NIE") + ".");
        }

        private static bool ScrHas(MethodBase m, bool prefix)
        {
            var pi = Harmony.GetPatchInfo(m);
            if (pi == null) return false;
            foreach (var x in prefix ? pi.Prefixes : pi.Postfixes) if (x.PatchMethod != null && x.PatchMethod.DeclaringType == typeof(SideVillageVisit)) return true;
            return false;
        }

        /// <summary>Soltys wsi okregu (albo pierwszy zywy notabl) - wlasciciel wiersza "Men of X". null - okno gry nie do zbudowania.</summary>
        private static Hero ScrHeadman(Settlement st)
        {
            if (st == null || st.Notables == null) return null;
            foreach (var n in st.Notables) if (n != null && n.IsAlive && n.IsHeadman) return n;
            foreach (var n in st.Notables) if (n != null && n.IsAlive) return n;
            return null;
        }

        /// <summary>LevyOpen (bramki juz przeszly): otwiera okno gry. false - nie da sie (klucz, latki, brak menu, brak soltysa, okno sie nie otworzylo) -
        /// LevyOpen pokazuje liste wyboru jak dotad.</summary>
        private static bool ScrTryOpen(Settlement st, string uid, bool fief)
        {
            if (!ScreenKey || !_scrPatched) return false;
            try
            {
                var c = Campaign.Current;
                var mc = c != null ? c.CurrentMenuContext : null;
                if (mc == null || ScrHeadman(st) == null) { _scrFail++; Log.Info(SP + NameOf(Rec(uid)) + " (" + uid + "): " + (mc == null ? "brak menu" : "wies okregu bez notabla") + " - lista wyboru jak dotad."); return false; }
                var ctx = new ScrCtx { St = st, Uid = uid, Fief = fief, Tick = Environment.TickCount };
                _scr = ctx;
                mc.OpenRecruitVolunteers();   // MenuViewContext.AddMenuView -> OnInitialize synchronicznie: RefreshScreen wiaze VM
                if (ctx.Vm == null)
                {
                    if (ReferenceEquals(_scr, ctx)) _scr = null;
                    _scrFail++;
                    Log.Info(SP + NameOf(Rec(uid)) + " (" + uid + "): okno gry sie nie otworzylo (menu bez widoku albo okno werbunku juz otwarte) - lista wyboru jak dotad.");
                    return false;
                }
                _scrOpen++;
                Log.Info(SP + NameOf(Rec(uid)) + " (" + uid + ", okreg " + st.StringId + "): okno gry - chlopi " + ctx.NV + ", ludzie rodu " + ctx.NH + " | otwarte " + _scrOpen + "x, potkniecia " + _scrSt + ".");
                return true;
            }
            catch (Exception e) { _scr = null; _scrFail++; ScrStumble("Open", e); return false; }
        }

        /// <summary>Prefiks RecruitmentVM.RefreshScreen: VM otwarty przez nas - nasze wiersze, oryginal pominiety (osady nie ma). Inny VM - oryginal.</summary>
        public static bool ScrRefreshPre(RecruitmentVM __instance)
        {
            var c = _scr;
            if (c == null || __instance == null) return true;
            if (c.Vm == null)
            {
                if (unchecked(Environment.TickCount - c.Tick) > 3000) { _scr = null; return true; }   // stare otwarcie - nie wiazemy cudzego okna
                c.Vm = __instance;
            }
            else if (!ReferenceEquals(c.Vm, __instance)) return true;
            try { ScrBuild(__instance, c); }
            catch (Exception e) { ScrStumble("Build", e); try { __instance.VolunteerList.Clear(); } catch { } }
            return false;
        }

        private static RecruitVolunteerVM ScrRow(RecruitmentVM vm, Hero owner, CharacterObject troop, int n, int price, string title)
        {
            var onRec = (Action<RecruitVolunteerVM, RecruitVolunteerTroopVM>)Delegate.CreateDelegate(typeof(Action<RecruitVolunteerVM, RecruitVolunteerTroopVM>), vm, _miOnRecruit);
            var onRem = (Action<RecruitVolunteerVM, RecruitVolunteerTroopVM>)Delegate.CreateDelegate(typeof(Action<RecruitVolunteerVM, RecruitVolunteerTroopVM>), vm, _miOnRemove);
            var list = new List<CharacterObject>(n);
            for (int i = 0; i < n; i++) list.Add(troop);
            var row = new RecruitVolunteerVM(owner, list, onRec, onRem);
            foreach (var t in row.Troops)
            {
                if (t == null || t.Character == null) continue;
                t.Cost = price;                   // nasza cena (etap poboru, rod) zamiast kosztu modelu gry
                t.PlayerHasEnoughRelation = true; // bramki zaufania i rodu liczy LevyGate / HouseQuote
                t.CanBeRecruited = true;
            }
            try { if (row.Owner != null) row.Owner.TitleText = title; } catch (Exception e) { ScrStumble("Title", e); }
            return row;
        }

        /// <summary>Wiersze okna: chlopi (soltys) i ludzie rodu (glowa rodu, albo soltys gdy rod wygasly / portret sie nie zbudowal). Slotow nie wiecej niz miejsc
        /// notabla (VolunteerTypes - BK do 20, gra 6). Wiersz chlopow = gotowi minus wiersz rodu (ludzie rodu to czesc tego samego naplywu).</summary>
        private static void ScrBuild(RecruitmentVM vm, ScrCtx c)
        {
            c.Builds++;
            vm.VolunteerList.Clear();
            vm.TroopsInCart.Clear();
            c.RowV = c.RowH = null; c.NV = c.NH = 0;
            vm.InitialPartySize = PartyBase.MainParty.NumberOfAllMembers;
            _lvQ = null;
            bool hide; string code; LvQuote q;
            string why = LevyGate(c.St, c.Uid, true, out hide, out code, out q);
            var head = ScrHeadman(c.St);
            if (!hide && why == null && q != null && head != null)
            {
                int slots = head.VolunteerTypes != null ? Math.Max(6, Math.Min(20, head.VolunteerTypes.Length)) : 6;
                int nh = q.H != null && q.HouseReady > 0 ? Math.Min(q.HouseReady, slots) : 0;
                int nv = Math.Min(Math.Max(0, q.Ready - nh), slots);
                if (nh > 0)
                {
                    Hero lead = q.H.Alive != null ? q.H.Alive.Leader : null;
                    RecruitVolunteerVM rh = null;
                    if (lead != null && lead.IsAlive)
                    {
                        try { rh = ScrRow(vm, lead, q.H.Troop, nh, q.HousePrice, q.H.Name); }
                        catch (Exception e) { ScrStumble("RowHouseLord", e); rh = null; }
                    }
                    if (rh == null) rh = ScrRow(vm, head, q.H.Troop, nh, q.HousePrice, q.H.Name);
                    c.RowH = rh; c.NH = nh;
                }
                if (nv > 0) { c.RowV = ScrRow(vm, head, q.Troop, nv, q.Price, "Men of " + NameOf(Rec(c.Uid))); c.NV = nv; vm.VolunteerList.Add(c.RowV); }
                if (c.RowH != null) vm.VolunteerList.Add(c.RowH);
            }
            else if (why != null) Log.Player(why, true);
            vm.TotalWealth = Hero.MainHero != null ? Hero.MainHero.Gold : 0;
            try { _miRefreshParty.Invoke(vm, null); } catch (Exception e) { ScrStumble("RefreshParty", e); }
            try { if (_miRecruitAll != null) _miRecruitAll.Invoke(vm, null); } catch (Exception e) { ScrStumble("RecruitAll", e); }
        }

        /// <summary>Prefiks RecruitmentVM.ExecuteDone (przed OnDone i jego latkami): koszyk naszego okna -> LevyDo (rod, potem chlopi), potem Deactivate. Inny VM - oryginal.</summary>
        public static bool ScrDonePre(RecruitmentVM __instance)
        {
            var c = _scr;
            if (c == null || __instance == null || !ReferenceEquals(c.Vm, __instance)) return true;
            int nv = 0, nh = 0;
            try
            {
                foreach (var t in __instance.TroopsInCart)
                {
                    if (t == null || t.IsTroopEmpty || t.Character == null) continue;
                    if (c.RowH != null && ReferenceEquals(t.Owner, c.RowH)) nh++;
                    else if (c.RowV != null && ReferenceEquals(t.Owner, c.RowV)) nv++;
                }
                if (nh + nv > 0)
                {
                    _scrDone++;
                    string how = c.Fief ? "okno gry (wioska gracza)" : "okno gry";
                    LvDone rh = nh > 0 ? LevyDo(c.St, c.Uid, nh, how + ", rod", true) : null;
                    LvDone rv = nv > 0 ? LevyDo(c.St, c.Uid, nv, how) : null;
                    Log.Info(SP + NameOf(Rec(c.Uid)) + " (" + c.Uid + "): koszyk - ludzie rodu " + nh + " (przybylo " + (rh != null ? rh.Keep : 0) + (rh != null && rh.Why != null ? ", " + rh.Why : "")
                             + "), chlopi " + nv + " (przybylo " + (rv != null ? rv.Keep : 0) + (rv != null && rv.Why != null ? ", " + rv.Why : "") + ") | zatwierdzone " + _scrDone + "x, potkniecia " + _scrSt + ".");
                }
            }
            catch (Exception e) { ScrStumble("Done", e); }
            try { __instance.Deactivate(); } catch (Exception e) { ScrStumble("Deactivate", e); }
            return false;
        }

        /// <summary>Postfiks RecruitmentVM.OnFinalize: koniec wiazania naszego okna.</summary>
        public static void ScrFinPost(RecruitmentVM __instance)
        {
            var c = _scr;
            if (c != null && ReferenceEquals(c.Vm, __instance)) _scr = null;
        }
    }
}
