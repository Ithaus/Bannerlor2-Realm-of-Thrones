using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CrashScribe
{
    /// <summary>
    /// Mostek na crash RealisticBannerlord: jego szpiegowski dialog
    /// (lord_spy_fail_consequence) wola PlayerEncounter.RestartPlayerEncounter
    /// z TRZEMA argumentami - w tej wersji gry metoda ma CZTERY, wiec sam JIT
    /// tej metody konczy sie MissingMethodException i gra pada. Oryginalu nie
    /// da sie zalatac wprost (Harmony tez musialby go skompilowac), wiec
    /// przechwytujemy zdanie dialogowe o id "lord_spy_reaction_fail" i odgrywamy
    /// jego skutki sami, juz z poprawna sygnatura.
    /// </summary>
    internal static class Mends
    {
        internal static void Install(Harmony harmony)
        {
            try
            {
                // Zaciezny w bitwie przy oblezeniu (wycieczka obroncow, "continue_siege
                // _after_attack"): gracz potrafi nie miec formalnej strony w MapEvent
                // (PlayerSide = None) i vanilla GetPlayerBattleContributionRate wali
                // IndexOutOfRange - gra pada na ekranie wynikow. Wklad = 0 i gramy dalej.
                var contrib = AccessTools.Method(typeof(TaleWorlds.CampaignSystem.MapEvents.MapEvent), "GetPlayerBattleContributionRate");
                if (contrib != null)
                {
                    harmony.Patch(contrib, finalizer: new HarmonyMethod(typeof(Mends), "SafeContribution"));
                    Scribe.Line("Mends: contribution-rate crash (zaciezny przy oblezeniu) zmostkowany.");
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(contrib)", null); } catch { } }

            try
            {
                // ===== SEDNO PUSTYCH EKRANOW BANNER KINGS =====
                // BKROTPatch podmienia CALA inicjalizacje stylow zycia BK wlasnym
                // prefixem (zwraca false, wiec oryginal sie nie wykonuje). Ten prefix
                // wywraca sie w polowie na NullReference - a wtedy:
                //   DefaultLifestyles.Initialize konczy sie w polowie
                //   -> BannerKingsConfig.Initialize() nie dochodzi do konca
                //   -> menedzery BK (dwor, relacje, rekruci) zostaja niekompletne
                //   -> tysiace NullReference przy kazdym odwolaniu (u Jeffa 6000
                //      w kilkanascie minut) i PUSTE EKRANY tam, gdzie BK dokłada
                //      swoje rozszerzenia - miedzy innymi ekran REKRUTACJI
                //      (BannerKings.UI.Extensions.VolunteerRecruitmentMixin).
                // Finalizer: wywrotka lataczki -> oddajemy robote ORYGINALNEJ
                // metodzie BannerKings (__result = true). Style zycia beda bez
                // ROT-owych opisow, ale CALA reszta moda wreszcie wstanie.
                var tLif = Type.GetType("BKROTPatch.Patches.DefaultLifestylesInitializePatch, BKROTPatch")
                           ?? QuietType("DefaultLifestylesInitializePatch");
                var mLif = tLif != null ? AccessTools.Method(tLif, "Prefix") : null;
                if (mLif != null)
                {
                    // Najpierw po prostu ZDEJMUJEMY ich lataczke z metody BK - wtedy
                    // oryginal leci czysto, bez zadnego wyjatku i bez polowicznego stanu.
                    bool off = Unhook(harmony, mLif, "BannerKings.Managers.Education.Lifestyles.DefaultLifestyles", "Initialize");
                    if (off)
                        Scribe.Line("Mends: lataczka BKROTPatch zdjeta ze stylow zycia BK - inicjalizacje robi oryginal (Banner Kings wstanie caly).");
                    else
                    {
                        harmony.Patch(mLif, finalizer: new HarmonyMethod(typeof(Mends), "SafeLifestyles"));
                        Scribe.Line("Mends: inicjalizacja stylow zycia BK zabezpieczona (BKROTPatch nie zabije juz calego Banner Kings).");
                    }
                }
                else Scribe.Line("Mends: BKROTPatch/DefaultLifestylesInitializePatch nieobecny - nic do zabezpieczenia.");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(lifestyles)", null); } catch { } }

            try
            {
                // ---------------------------------------------------------------
                // BKROTPatch / ReligionGenerateClergymanPatch - CICHY ZABOJCA OSAD.
                // Ich prefix "sprawdza" kulture osady wolajac
                //     NameGenerator.GetNameListForCulture(...)  przez  Invoke(NULL, ...)
                // a ta metoda NIE JEST STATYCZNA. Kazde wywolanie konczy sie
                // TargetException ("Non-static method requires a target"), ich wlasny
                // catch zwraca false - i ORYGINALNY BannerKings.Religion.GenerateClergyman
                // NIGDY NIE LECI. Skutek lawinowy:
                //   zadna osada nie dostaje duchownego
                //   -> ReligionData.Update wywraca sie na nullu
                //   -> PopulationData.Update NIGDY nie dochodzi do konca
                //   -> dane osady BK (ludnosc, milicja, OCHOTNICY DO REKRUTACJI) stoja martwe.
                // W sesji Jeffa z 25.08: 11 487 wyjatkow i pusty ekran rekrutacji.
                // Zdejmujemy TYLKO ten jeden prefix; ich Finalizer zostaje (lapie wywrotki
                // samego generatora BK), reszta BKROTPatch nietknieta.
                // ---------------------------------------------------------------
                var tCle = Type.GetType("BKROTPatch.Patches.ReligionGenerateClergymanPatch, BKROTPatch")
                           ?? QuietType("ReligionGenerateClergymanPatch");
                var mCle = tCle != null ? AccessTools.Method(tCle, "Prefix") : null;
                if (mCle != null)
                {
                    bool off = Unhook(harmony, mCle, "BannerKings.Managers.Institutions.Religions.Religion", "GenerateClergyman");
                    Scribe.Line(off
                        ? "Mends: BKROTPatch nie dusi juz duchownych BK - dane osad (ludnosc, milicja, ochotnicy) licza sie do konca."
                        : "Mends: nie udalo sie zdjac lataczki na duchownych - probuje objazdem.");
                    if (!off)
                        harmony.Patch(mCle, prefix: new HarmonyMethod(typeof(Mends), "LetClergyBe"));
                }
                else Scribe.Line("Mends: BKROTPatch/ReligionGenerateClergymanPatch nieobecny - nic do odblokowania.");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(clergy)", null); } catch { } }

            try
            {
                // ---------------------------------------------------------------
                // OCHOTNICY SA MIEJSCOWI. Banner Kings dobiera rekrutow wedle kultury
                // NOTABLA, nie osady:
                //     GetPossibleSpawns(sellerHero.Culture, popType, settlement)
                // a duchownych BK tworzy z presetu wiary:
                //     HeroCreator.CreateSpecialHero(preset, ...)   <- kultura Z PRESETU
                // i dopiero potem wsadza ich do osady. Czyli kaplan zrodzony jako
                // wolny lud siada w polnocnej wsi i wystawia synow Thenna.
                // Jeff (sluszznie): "nie moze byc free folk w wiosce Polnocy".
                // Chlopi z Acorn Water sa Polnocnikami niezaleznie od tego, kto im
                // odprawia obrzedy - wiec pytamy o kulture OSADY, nie kaplana.
                // ---------------------------------------------------------------
                if (Config.LocalRecruits)
                {
                    var tSpawns = AccessTools.TypeByName("BannerKings.Managers.Recruits.DefaultRecruitSpawns");
                    System.Reflection.MethodInfo mSpawns = null;
                    if (tSpawns != null)
                        foreach (var mi in tSpawns.GetMethods(System.Reflection.BindingFlags.Public
                                 | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
                        {
                            if (mi.Name != "GetPossibleSpawns") continue;
                            var ps = mi.GetParameters();
                            if (ps.Length == 2 && ps[1].ParameterType == typeof(Settlement)) { mSpawns = mi; break; }
                        }
                    if (mSpawns != null)
                    {
                        harmony.Patch(mSpawns, prefix: new HarmonyMethod(typeof(Mends), "LocalRecruitsPrefix"));
                        Scribe.Line("Mends: ochotnicy ida wedle kultury OSADY, nie notabla (koniec wolnego ludu w wioskach Polnocy).");
                    }
                    else Scribe.Line("Mends: nie znalazlem BK GetPossibleSpawns - ochotnicy zostaja po staremu.");
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(recruits)", null); } catch { } }

            try
            {
                // BannerKings BKRelationsModel.CalculateModifiers wali NullReference
                // DZIESIEC RAZY NA SEKUNDE (22 tys. powtorek w pol godziny u Jeffa -
                // stad freez): jakis bohater bez kultury/klanu wywraca liczenie
                // relacji. Finalizer: wywrotka -> pusta lista modyfikatorow i gramy.
                var tRel = Type.GetType("BannerKings.Models.BKModels.BKRelationsModel, BannerKings");
                var mRel = tRel != null ? AccessTools.Method(tRel, "CalculateModifiers") : null;
                if (mRel != null)
                {
                    // slad 28.08 nazwal null po imieniu: notabl w osadzie BEZ TYTULU
                    // feudalnego (Essos, np. Tolarra) -> GetTitle(osada) daje null,
                    // a BK robi title.DeFacto = NullReference. 13 wyjatkow NA SEKUNDE
                    // (12,9 tys. w 17 minut) - kazdy lapany, ale samo rzucanie MULI gre.
                    // Brama PRZED metoda wycina znany przypadek bez wyjatku;
                    // finalizer zostaje na wszystko inne.
                    try
                    {
                        var tCfg = Type.GetType("BannerKings.BannerKingsConfig, BannerKings") ?? QuietType("BannerKingsConfig");
                        _bkCfgInstanceGet = tCfg != null ? AccessTools.PropertyGetter(tCfg, "Instance") : null;
                        _bkCfgTitleMgrGet = tCfg != null ? AccessTools.PropertyGetter(tCfg, "TitleManager") : null;
                        var tTm = _bkCfgTitleMgrGet != null ? _bkCfgTitleMgrGet.ReturnType : null;
                        _bkGetTitle = tTm != null ? AccessTools.Method(tTm, "GetTitle", new[] { typeof(Settlement) }) : null;
                    }
                    catch { _bkGetTitle = null; }
                    harmony.Patch(mRel,
                        prefix: new HarmonyMethod(typeof(Mends), "EssosTitleGate"),
                        finalizer: new HarmonyMethod(typeof(Mends), "SafeRelations"));
                    Scribe.Line("Mends: BK CalculateModifiers zabezpieczone (brama Essos przed metoda + finalizer)."
                                + (_bkGetTitle == null ? " UWAGA: GetTitle nieznaleziony - brama spi, zostal sam finalizer." : ""));
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(relations)", null); } catch { } }

            try
            {
                // ZRODLO lawiny, nie objaw: HeroRelations.UpdateRelations (DailyTickHero)
                // wola GetHeroesToUpdate, a ta dla notabla BEZ OSADY (albo z osada
                // bez wlasciciela) robi Hero.CurrentSettlement.OwnerClan.Heroes
                // i pada NullReference (HeroRelations.cs:115; sesja 26.08: 2500+
                // powtorek, wspolnie z SafeRelations 12 tys. zlapan i freez gry).
                // Notabl bez osady nie ma zadnej listy do aktualizacji - pomijamy
                // go w calosci, ZANIM cokolwiek wybuchnie.
                var tHr = Type.GetType("BannerKings.Behaviours.Relations.HeroRelations, BannerKings")
                          ?? QuietType("HeroRelations");
                var mUpd = tHr != null ? AccessTools.Method(tHr, "UpdateRelations") : null;
                if (mUpd != null)
                {
                    _hrHeroGet = AccessTools.PropertyGetter(tHr, "Hero");
                    harmony.Patch(mUpd, prefix: new HarmonyMethod(typeof(Mends), "RelationsUpdateGate"));
                    Scribe.Line("Mends: relacje BK - notable bez osady pomijani (zrodlo lawiny NullReference).");
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(relations)", null); } catch { } }

            try
            {
                // MARTWY DIALOG DUCHOWNEGO (Jeff 27.08: "click to continue i nic
                // sie nie dzieje"). Powitanie BK i WSZYSTKIE opcje preachera maja
                // warunek ReligionsManager.IsPreacher - a preacher bez wpisu
                // w menedzerze (ROT-owe osady czesto nie maja religii, wiec
                // CleanClergymen BK go nie rejestruje) wypada z wlasnego dialogu:
                // gra pokazuje cudze powitanie, po ktorym nie ma ZADNEJ opcji.
                // Tuz przed warunkiem powitania dopisujemy go do religii.
                var tRelB = Type.GetType("BannerKings.Behaviours.BKReligionsBehavior, BannerKings")
                            ?? QuietType("BKReligionsBehavior");
                var mGreet = tRelB != null ? AccessTools.Method(tRelB, "OnConditionClergymanGreeting") : null;
                if (mGreet != null)
                {
                    harmony.Patch(mGreet, prefix: new HarmonyMethod(typeof(Mends), "RegisterStrayPreacher"));
                    Scribe.Line("Mends: preacher bez rejestru religii dostaje wpis przy rozmowie (martwy dialog).");
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(preacher)", null); } catch { } }

            try
            {
                // Westeros zna tylko DLUGA zime i dlugie lato - kalendarzyk por roku
                // RealisticBannerlord (co 21 dni wiosna/jesien, zamiecie, kary do marszu,
                // zimowy glod) nie ma tu sensu, a MCM RB i tak nie zapisuje ustawien.
                // Jeff kazal wylaczyc na twardo: getter SeasonsEnabled klamie "false"
                // i caly system Seasons (predkosc, pogoda, jedzenie, morale) spi.
                var tRbSet = Type.GetType("RealisticBannerlord.Settings.RealisticSettings, RealisticBannerlord");
                var gSeasons = tRbSet != null ? AccessTools.PropertyGetter(tRbSet, "SeasonsEnabled") : null;
                if (gSeasons != null)
                {
                    harmony.Patch(gSeasons, postfix: new HarmonyMethod(typeof(Mends), "SeasonsOff"));
                    Scribe.Line("Mends: pory roku RB wylaczone na twardo (Westeros ma zime i lato, nie kalendarzyk).");
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(seasons)", null); } catch { } }

            try
            {
                // KUZNIA (crash Jeffa 2026-08-24 11:36, Two Handed Mace + Long Steel
                // Mace Handle): mody laduja elementy broni PODWOJNIE (ten sam StringId,
                // INNA instancja obiektu). GenerateCraftedItem porownuje REFERENCJE:
                // klikniety uchwyt "nie nalezy do szablonu" -> item = null -> pierwsze
                // odswiezenie UI (WeaponDesignVM.RefreshStats i spolka) wali
                // NullReference i gra pada. Naprawa: gdy item wyszedl null, podmieniamy
                // obce instancje na blizniaki z szablonu (po StringId) i skladamy znowu.
                var mSetItem = AccessTools.Method(typeof(TaleWorlds.Core.Crafting), "SetItemObject");
                if (mSetItem != null)
                {
                    harmony.Patch(mSetItem, postfix: new HarmonyMethod(typeof(Mends), "SafeSmithy"));
                    Scribe.Line("Mends: kuznia zabezpieczona (podwojnie zaladowany element nie wywraca juz projektowania).");
                }
                // pas i szelki: gdyby item MIMO TO byl null, odswiezenia UI maja
                // nie dotykac pustki zamiast rzucac NullReference w silniku Gauntleta
                var tVm = Type.GetType("TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign.WeaponDesignVM, TaleWorlds.CampaignSystem.ViewModelCollection");
                if (tVm != null)
                {
                    string[] guarded = { "DoesCurrentItemHaveSecondaryUsage", "RefreshAlternativeUsageList", "RefreshStats", "AddClassFlagsToPiece" };
                    for (int gi = 0; gi < guarded.Length; gi++)
                    {
                        var mg = AccessTools.Method(tVm, guarded[gi]);
                        if (mg != null) harmony.Patch(mg, prefix: new HarmonyMethod(typeof(Mends), "VmNeedsItem"));
                    }
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(smithy)", null); } catch { } }

            try
            {
                if (Type.GetType("RealisticBannerlord.Systems.Espionage.LordSpyBehavior, RealisticBannerlord") == null)
                    return;   // moda nie ma - nie ma czego mostkowac
                var run = AccessTools.Method(typeof(ConversationSentence), "RunConsequence");
                if (run == null) { Scribe.Line("Mends: ConversationSentence.RunConsequence not found."); return; }
                harmony.Patch(run, prefix: new HarmonyMethod(typeof(Mends), "SafeSpyFail"));
                Scribe.Line("Mends: RealisticBannerlord spy-arrest crash bridged (RestartPlayerEncounter 3->4 args).");

                // formula skutecznosci przekupstwa: baza 0, honor wyklucza, licza sie
                // wady charakteru, przyjazn, Roguery i sila klanu - nie plaski rzut 20%
                var spyType = Type.GetType("RealisticBannerlord.Systems.Espionage.LordSpyBehavior, RealisticBannerlord");
                var cond = spyType != null ? AccessTools.Method(spyType, "lord_spy_success_condition") : null;
                if (cond != null)
                {
                    harmony.Patch(cond, prefix: new HarmonyMethod(typeof(Mends), "RealSpyChance"));
                    Scribe.Line("Mends: spy bribe odds rebuilt (base 0%, honour is incorruptible).");
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install", null); } catch { } }
        }

        /// <summary>
        /// Finalizer na MapEvent.GetPlayerBattleContributionRate: gdy gracz nie ma
        /// strony w bitwie (sluzba, wycieczka przy oblezeniu), wklad = 0 zamiast crashu.
        /// </summary>
        private static int _relSaves;

        /// <summary>
        /// BK liczy modyfikatory relacji dla bohatera bez kultury/klanu i pada.
        /// Wywrotka -> pusta lista (relacja bez modyfikatorow) zamiast lawiny
        /// wyjatkow mrozacej gre. Log tylko co setny raz, zeby sie nie zapchac.
        /// </summary>
        private static bool _lifestylesSaved;

        /// <summary>
        /// Lataczka BKROTPatch na style zycia sie wywrocila - niech oryginal
        /// Banner Kings dokonczy, zamiast zostawiac pol moda w gruzach.
        /// </summary>
        public static Exception SafeLifestyles(Exception __exception, ref bool __result)
        {
            try
            {
                if (__exception == null) return null;
                __result = true;                       // TAK, wykonaj oryginalny Initialize
                if (!_lifestylesSaved)
                {
                    _lifestylesSaved = true;
                    Scribe.Report("BKROTPatch przerwal inicjalizacje Banner Kings - oddaje robote oryginalowi",
                                  __exception, "DefaultLifestylesInitializePatch.Prefix", null);
                }
                Scribe.Line("Mends: BKROTPatch wywrocil sie na stylach zycia (" + __exception.GetType().Name
                            + ") - inicjalizacje konczy oryginal BannerKings.");
                return null;                           // wyjatek polkniety
            }
            catch { return null; }
        }

        /// <summary>
        /// KULTURA BEZ IMION ZABIJA GRE. Kultura bez wpisow male_names/female_names
        /// w XML ma MaleNameList/FemaleNameList = null, a NameGenerator robi na tym
        /// IsEmpty() -> ArgumentNullException przy tworzeniu KAZDEGO bohatera tej
        /// kultury. Dowod 28.08 11:11: BK generowal duchownego (Religion.
        /// GenerateClergymanHero -> CreateSpecialHero -> GetNameListForCulture),
        /// FirstChance lecial seriami, a o 11:15 gra padla na ApplicationTick przy
        /// wejsciu Jeffa w bitwe. Raz na sesje pozyczamy listy od najbogatszej
        /// kultury - imie bedzie obce, ale gra zyje (kulture duchownego i tak
        /// zaraz prostuje nasza latka "kaplan jest stad").
        /// </summary>
        internal static void FeedNamelessCultures()
        {
            try
            {
                var mgr = TaleWorlds.ObjectSystem.MBObjectManager.Instance;
                var all = mgr != null ? mgr.GetObjectTypeList<CultureObject>() : null;
                if (all == null) return;
                var fMale = AccessTools.Field(typeof(CultureObject), "_maleNameList");
                var fFem = AccessTools.Field(typeof(CultureObject), "_femaleNameList");
                var fClan = AccessTools.Field(typeof(CultureObject), "_clanNameList");
                if (fMale == null || fFem == null || fClan == null) return;

                CultureObject donor = null; int best = 0;
                foreach (var c in all)
                {
                    if (c == null) continue;
                    int score = (c.MaleNameList != null ? c.MaleNameList.Count : 0)
                              + (c.FemaleNameList != null ? c.FemaleNameList.Count : 0);
                    if (score > best) { best = score; donor = c; }
                }
                if (donor == null) return;

                int fed = 0;
                foreach (var c in all)
                {
                    if (c == null || c == donor) continue;
                    bool hungry = false;
                    if (c.MaleNameList == null || c.MaleNameList.Count == 0) { fMale.SetValue(c, fMale.GetValue(donor)); hungry = true; }
                    if (c.FemaleNameList == null || c.FemaleNameList.Count == 0) { fFem.SetValue(c, fFem.GetValue(donor)); hungry = true; }
                    if (c.ClanNameList == null || c.ClanNameList.Count == 0) { fClan.SetValue(c, fClan.GetValue(donor)); hungry = true; }
                    if (hungry)
                    {
                        fed++;
                        Scribe.Line("Mends: kultura " + c.StringId + " nie miala list imion - pozyczone od " + donor.StringId + ".");
                    }
                }
                if (fed > 0)
                    Scribe.Line("Mends: " + fed + " kultur bez imion nakarmione (crash NameGenerator przy tworzeniu bohatera).");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.FeedNamelessCultures", null); } catch { } }
        }

        private static System.Reflection.MethodInfo _hrHeroGet;
        private static System.Reflection.MethodInfo _bkCfgInstanceGet, _bkCfgTitleMgrGet, _bkGetTitle;
        private static Type _tListRelMod;
        private static int _essosGated;

        /// <summary>
        /// Notabl w osadzie bez tytulu feudalnego (pol Essos w ROT) to w BK
        /// GWARANTOWANY NullReference (title.DeFacto na null). Oddajemy pusta
        /// liste modyfikatorow - DOKLADNIE to, co po wywrotce oddawal finalizer -
        /// tylko bez rzucania wyjatku, ktory przy 13/s mulil gre.
        /// </summary>
        public static bool EssosTitleGate(object heroRelations, Hero target, ref object __result)
        {
            try
            {
                if (_bkGetTitle == null || _hrHeroGet == null) return true;
                var hero = heroRelations != null ? _hrHeroGet.Invoke(heroRelations, null) as Hero : null;
                if (hero == null || target == null) return true;
                if (!hero.IsNotable || !target.IsLord) return true;   // pada tylko galaz notabl->lord
                var st = hero.CurrentSettlement;
                if (st != null)
                {
                    var cfg = _bkCfgInstanceGet != null ? _bkCfgInstanceGet.Invoke(null, null) : null;
                    var tm = cfg != null && _bkCfgTitleMgrGet != null ? _bkCfgTitleMgrGet.Invoke(cfg, null) : null;
                    var title = tm != null ? _bkGetTitle.Invoke(tm, new object[] { st }) : null;
                    if (title != null) return true;   // osada ma tytul - normalna droga BK
                }
                if (_tListRelMod == null)
                {
                    var t = Type.GetType("BannerKings.Managers.Skills.RelationsModifier, BannerKings")
                            ?? QuietType("RelationsModifier");
                    if (t == null) return true;
                    _tListRelMod = typeof(System.Collections.Generic.List<>).MakeGenericType(t);
                }
                __result = Activator.CreateInstance(_tListRelMod);
                _essosGated++;
                if (_essosGated == 1 || _essosGated % 2000 == 0)
                    try { Scribe.Line("Mends: relacje notabl-lord bez tytulu osady (Essos) - odciete " + _essosGated + " razy, zero wyjatkow."); } catch { }
                return false;
            }
            catch { return true; }
        }
        private static int _relSkipped;
        private static bool _relStackDumped;

        /// <summary>
        /// Brama na UpdateRelations. Niebezpieczna galaz GetHeroesToUpdate
        /// (else-if Hero.IsNotable) odpala sie tylko dla notabla bez klanu
        /// i wtedy CurrentSettlement/OwnerClan bez null-checka klada watek.
        /// Takiego bohatera pomijamy; cala reszta liczy sie normalnie.
        /// </summary>
        /// <summary>Wizytowka bohatera do diagnozy nulli: co ma, czego mu brak.</summary>
        private static string Describe(Hero h)
        {
            try
            {
                if (h == null) return "(null)";
                return h.Name + " [" + (h.IsLord ? "lord" : h.IsNotable ? "notabl" : "inny")
                       + ", kultura=" + (h.Culture != null ? h.Culture.StringId : "NULL")
                       + ", klan=" + (h.Clan != null ? h.Clan.StringId : "NULL")
                       + ", osada=" + (h.CurrentSettlement != null ? h.CurrentSettlement.StringId : "NULL") + "]";
            }
            catch { return "(blad opisu)"; }
        }

        private static int _strayPreachersFixed;

        /// <summary>
        /// Preacher-duch: notabl z zawodem kaplana, ktorego menedzer religii BK
        /// nie zna. Dopisujemy go do religii (wlasnej, a gdy brak - idealnej dla
        /// jego kultury) TUZ PRZED warunkiem powitania - dialog wstaje w tej
        /// samej rozmowie. Wszystko refleksja, zero twardej zaleznosci od BK.
        /// </summary>
        public static void RegisterStrayPreacher()
        {
            try
            {
                var hero = Hero.OneToOneConversationHero;
                if (hero == null || !hero.IsPreacher) return;
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                var cfgP = cfgT != null ? AccessTools.Property(cfgT, "Instance") : null;
                object cfg = cfgP != null ? cfgP.GetValue(null, null) : null;
                object mgr = cfg != null ? Traverse.Create(cfg).Property("ReligionsManager").GetValue() : null;
                if (mgr == null) return;
                var tr = Traverse.Create(mgr);
                if (tr.Method("IsPreacher", hero).GetValue<bool>()) return;   // zna go - nic do roboty

                object rel = tr.Method("GetHeroReligion", hero).GetValue();
                if (rel == null)
                {
                    try { rel = tr.Method("GetIdealReligion", hero.Culture).GetValue(); } catch { }
                }
                var sett = hero.CurrentSettlement;
                if (rel == null || sett == null)
                {
                    Scribe.Line("Mends: preacher " + hero.Name + " bez religii/osady - dialogu nie ozywie.");
                    return;
                }
                Traverse.Create(rel).Method("AddClergyman", sett, hero).GetValue();
                _strayPreachersFixed++;
                Scribe.Line("Mends: preacher " + hero.Name + " (" + sett.Name + ") dopisany do religii - dialog ozyl (lacznie " + _strayPreachersFixed + ").");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "RegisterStrayPreacher", null); } catch { } }
        }

        public static bool RelationsUpdateGate(object __instance)
        {
            try
            {
                if (_hrHeroGet == null || __instance == null) return true;
                var hero = _hrHeroGet.Invoke(__instance, null) as Hero;
                if (hero == null) return false;
                if (hero.IsNotable && hero.Clan == null
                    && (hero.CurrentSettlement == null || hero.CurrentSettlement.OwnerClan == null))
                {
                    if (_relSkipped++ % 500 == 0)
                        try { Scribe.Line("Mends: relacje BK - pominieto notabla bez osady/wlasciciela (" + hero.Name + "), lacznie " + _relSkipped + "."); } catch { }
                    return false;
                }
            }
            catch { }
            return true;
        }

        public static Exception SafeRelations(Exception __exception, ref object __result, object heroRelations, Hero target)
        {
            if (__exception == null) return null;
            if (__exception is NullReferenceException || __exception is ArgumentNullException
                || __exception is System.Collections.Generic.KeyNotFoundException)
            {
                // pierwszy zlapany dostaje PELNY raport ze stosem - w sesji 26.08
                // licznik doszedl do 12 tys., a w logu nie bylo ANI JEDNEGO sladu,
                // KTORA linia CalculateModifiers naprawde pada
                if (!_relStackDumped)
                {
                    _relStackDumped = true;
                    // slad 27.08 pokazal tylko ramke Patch1 (inline) - dokladamy
                    // KONTEKST bohaterow, zeby nazwac null po imieniu
                    string ctx = null;
                    try
                    {
                        Hero hero = null;
                        try { hero = _hrHeroGet != null && heroRelations != null ? _hrHeroGet.Invoke(heroRelations, null) as Hero : null; } catch { }
                        ctx = "hero=" + Describe(hero) + " | target=" + Describe(target);
                    }
                    catch { }
                    try { Scribe.Report("CrashScribe", __exception, "BKRelationsModel.CalculateModifiers - pelny slad lawiny (jednorazowo)", ctx); } catch { }
                }
                try
                {
                    var t = Type.GetType("BannerKings.Managers.Skills.RelationsModifier, BannerKings")
                            ?? QuietType("RelationsModifier");
                    var listT = typeof(System.Collections.Generic.List<>).MakeGenericType(t);
                    __result = Activator.CreateInstance(listT);
                }
                catch { }
                if (_relSaves++ % 100 == 0)
                    try { Scribe.Line("Mends: BK relations uratowane (NullReference), lacznie " + _relSaves + " razy."); } catch { }
                return null;
            }
            return __exception;
        }

        /// <summary>
        /// Zdejmuje JEDNA konkretna lataczke BKROTPatch z metody BannerKings, nie ruszajac
        /// niczego innego w ich modzie. Zwraca true, jesli metoda BK jest juz wolna.
        /// </summary>
        private static bool Unhook(Harmony harmony, System.Reflection.MethodInfo theirPatch,
                                   string bkTypeFullName, string bkMethodName)
        {
            try
            {
                if (harmony == null || theirPatch == null) return false;
                var bkType = AccessTools.TypeByName(bkTypeFullName);
                var target = bkType != null ? AccessTools.Method(bkType, bkMethodName) : null;
                if (target == null) return false;
                harmony.Unpatch(target, theirPatch);
                return true;
            }
            catch { return false; }
        }

        private static int _clergyFreed;
        private static int _localSwaps;

        /// <summary>
        /// JEDNORAZOWE SPRZATANIE PO MNIE. Zanim ochotnicy odswieza sie sami
        /// (jeden slot na dobe), w zapisie siedza juz obce oddzialy wpisane
        /// notablom - miedzy innymi wolny lud u polnocnych kaplanow, ktorych
        /// dopiero moja latka na BKROTPatch pozwolila stworzyc. Zerujemy tylko
        /// te sloty, ktorych kultura nie zgadza sie z osada; BK dobierze
        /// miejscowych przy najblizszym odswiezeniu.
        /// </summary>
        internal static void LocalLevies() { LocalLevies(null); }

        /// <summary>
        /// KAPLAN JEST STAD. Banner Kings tworzy duchownego z PRESETU WIARY:
        ///     HeroCreator.CreateSpecialHero(preset, ...)
        /// czyli jego kultura jest kultura presetu, nie osady - a wiare Starych
        /// Bogow ROT spina z wolnym ludem. Skutek widac czarno na bialym w moim
        /// wlasnym spisie notabli:
        ///     White Ranch (battania) -> [Greenseer Jarl / freefolk / Preacher]
        ///     Last River  (battania) -> [Greenseer Hali / freefolk / Preacher]
        /// a poniewaz OCHOTNIKOW dobiera sie wedle kultury NOTABLA, polnocne wsie
        /// wystawialy synow Thenna. Prostujemy u zrodla: kaplan dostaje kulture
        /// SWOJEJ osady, a obce oddzialy juz mu wpisane leca do kosza, zeby BK
        /// dobralo miejscowych. Wiara i tytul zostaja - zmienia sie tylko to,
        /// kogo ten czlowiek potrafi wystawic pod bron.
        /// </summary>
        internal static void LocalLevies(Settlement only)
        {
            try
            {
                if (!Config.LocalRecruits) return;
                int slots = 0, people = 0, cultures = 0;
                var all = only != null
                    ? (System.Collections.Generic.IEnumerable<Settlement>)new Settlement[] { only }
                    : Settlement.All;
                foreach (var st in all)
                {
                    if (st == null || st.Culture == null || st.IsHideout) continue;
                    var list = st.Notables;
                    if (list == null) continue;
                    foreach (var h in list)
                    {
                        if (h == null) continue;

                        // 1. obcy kaplan (albo inny notabl) dostaje kulture osady
                        if (h.Culture != st.Culture)
                        {
                            var was = h.Culture != null ? h.Culture.StringId : "?";
                            h.Culture = st.Culture;
                            cultures++;
                            try
                            {
                                Scribe.Line("Mends: " + h.Name + " w " + st.Name + " byl "
                                            + was + ", jest " + st.Culture.StringId
                                            + " (" + (h.CharacterObject != null ? h.CharacterObject.Occupation.ToString() : "?") + ").");
                            }
                            catch { }
                        }

                        // 2. obce oddzialy juz mu wpisane - do kosza, BK dobierze miejscowych
                        var vt = h.VolunteerTypes;
                        if (vt == null) continue;
                        bool touched = false;
                        for (int i = 0; i < vt.Length; i++)
                        {
                            var ch = vt[i];
                            if (ch == null || ch.Culture == null || ch.Culture == st.Culture) continue;
                            vt[i] = null;
                            slots++; touched = true;
                        }
                        if (touched) people++;
                    }
                }
                if (only == null || slots > 0 || cultures > 0)
                    Scribe.Line("Mends: werbunek miejscowy - poprawionych notabli " + cultures
                                + ", wyrzuconych obcych ochotnikow " + slots + " u " + people + " ludzi.");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.LocalLevies", null); } catch { } }
        }

        /// <summary>
        /// Kultura osady bije kulture notabla przy doborze ochotnikow.
        /// </summary>
        public static void LocalRecruitsPrefix(ref CultureObject __0, Settlement __1)
        {
            try
            {
                if (__1 == null || __1.Culture == null) return;
                if (__0 == __1.Culture) return;
                __0 = __1.Culture;
                if (_localSwaps == 0)
                    try { Scribe.Line("Mends: pierwszy podmieniony werbunek - ochotnicy sa juz miejscowi."); } catch { }
                _localSwaps++;
            }
            catch { }
        }



        /// <summary>
        /// Objazd, gdyby zdjecie lataczki sie nie udalo: wchodzimy PRZED ich prefix,
        /// zostawiamy ich sensowne zabezpieczenia na null, a poza tym kazemy im
        /// zwrocic true - czyli "niech duchownego zrobi oryginal BannerKings".
        /// </summary>
        public static bool LetClergyBe(TaleWorlds.CampaignSystem.Settlements.Settlement __0, ref bool __result)
        {
            try
            {
                if (__0 == null || __0.Culture == null) return true;
                __result = true;
                if (_clergyFreed == 0)
                    try { Scribe.Line("Mends: duchowni BK odblokowani objazdem - dane osad znowu licza sie do konca."); } catch { }
                _clergyFreed++;
                return false;
            }
            catch { return true; }
        }

        /// <summary>Typ RelationsModifier bywa w roznych przestrzeniach BK - szukamy po nazwie.</summary>
        private static Type QuietType(string shortName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.GetName().Name != "BannerKings") continue;
                    foreach (var t in asm.GetTypes())
                        if (t != null && t.Name == shortName) return t;
                }
                catch { }
            }
            return typeof(object);
        }

        /// <summary>Pory roku RB: zawsze "wylaczone" - w ROT zima trwa pokolenie, nie 21 dni.</summary>
        public static void SeasonsOff(ref bool __result) { __result = false; }

        // ---- kuznia: ratunek projektu z obcym (podwojnie zaladowanym) elementem ----
        private static bool _smithyFixing;
        private static int _smithySaves;

        public static void SafeSmithy(TaleWorlds.Core.Crafting __instance)
        {
            try
            {
                if (_smithyFixing || __instance == null) return;
                var fItem = AccessTools.Field(typeof(TaleWorlds.Core.Crafting), "_craftedItemObject");
                if (fItem == null || fItem.GetValue(__instance) != null) return;   // item jest - nic do ratowania

                var design = __instance.CurrentWeaponDesign;
                if (design == null || design.Template == null || design.UsedPieces == null) return;

                var used = design.UsedPieces;
                var els = new TaleWorlds.Core.WeaponDesignElement[used.Length];
                bool changed = false;
                for (int i = 0; i < used.Length; i++)
                {
                    els[i] = used[i];
                    var el = used[i];
                    if (el == null || !el.IsValid || el.CraftingPiece == null) continue;

                    bool inTemplate = false;
                    TaleWorlds.Core.CraftingPiece twin = null;
                    foreach (var p in design.Template.Pieces)
                    {
                        if (ReferenceEquals(p, el.CraftingPiece)) { inTemplate = true; break; }
                        if (twin == null && p != null && p.PieceType == el.CraftingPiece.PieceType
                            && p.StringId == el.CraftingPiece.StringId) twin = p;
                    }
                    if (inTemplate) continue;
                    if (twin == null)
                    {
                        // brak blizniaka po StringId - bierzemy pierwszy element tego samego typu
                        foreach (var p in design.Template.Pieces)
                            if (p != null && p.PieceType == el.CraftingPiece.PieceType) { twin = p; break; }
                    }
                    if (twin == null) continue;
                    els[i] = TaleWorlds.Core.WeaponDesignElement.CreateUsablePiece(twin, el.ScalePercentage);
                    changed = true;
                }
                if (!changed) return;

                var fresh = new TaleWorlds.Core.WeaponDesign(design.Template, design.WeaponName, els, null);
                var pDesign = AccessTools.Property(typeof(TaleWorlds.Core.Crafting), "CurrentWeaponDesign");
                var setDesign = pDesign != null ? pDesign.GetSetMethod(true) : null;
                if (setDesign != null) setDesign.Invoke(__instance, new object[] { fresh });
                else
                {
                    var bf = AccessTools.Field(typeof(TaleWorlds.Core.Crafting), "<CurrentWeaponDesign>k__BackingField");
                    if (bf == null) return;
                    bf.SetValue(__instance, fresh);
                }

                _smithyFixing = true;
                try
                {
                    var mSet = AccessTools.Method(typeof(TaleWorlds.Core.Crafting), "SetItemObject");
                    if (mSet != null) mSet.Invoke(__instance, new object[] { null, null });
                }
                finally { _smithyFixing = false; }

                _smithySaves++;
                if (_smithySaves == 1 || _smithySaves % 50 == 0)
                    Scribe.Line("Mends: kuznia uratowana (x" + _smithySaves + ") - obcy element podmieniony na blizniaka z szablonu.");
            }
            catch (Exception e) { _smithyFixing = false; try { Scribe.Report("CrashScribe", e, "SafeSmithy", null); } catch { } }
        }

        /// <summary>
        /// Odswiezenia UI projektanta broni nie maja czego liczyc, gdy zlozony
        /// item wyszedl null - pomijamy je, zamiast pozwolic silnikowi rzucic
        /// NullReference (crash). Zwrot false = nie wykonuj oryginalu.
        /// </summary>
        private static System.Reflection.FieldInfo _fVmCrafting;

        public static bool VmNeedsItem(object __instance)
        {
            try
            {
                if (__instance == null) return true;
                if (_fVmCrafting == null) _fVmCrafting = AccessTools.Field(__instance.GetType(), "_crafting");
                var cr = _fVmCrafting != null ? _fVmCrafting.GetValue(__instance) as TaleWorlds.Core.Crafting : null;
                if (cr == null) return true;
                return cr.GetCurrentCraftedItemObject() != null;
            }
            catch { return true; }
        }

        public static Exception SafeContribution(Exception __exception, ref float __result)
        {
            if (__exception == null) return null;
            if (__exception is IndexOutOfRangeException || __exception is NullReferenceException
                || __exception is ArgumentOutOfRangeException)
            {
                __result = 0f;
                try { Scribe.Line("Mends: contribution-rate uratowane (PlayerSide bez strony) -> wklad 0."); } catch { }
                return null;
            }
            return __exception;
        }

        /// <summary>
        /// Szansa, ze lord za 250 000 zostanie szpiegiem. Zamiast plaskich 20%:
        /// baza 0. Honorowy lord (Honor >= 1, np. Jon Snow) NIE sprzedaje sie nigdy.
        /// Dodaja: niski honor (+12/poziom), wyrachowanie (+5/poziom), przyjazn
        /// (+0.3/pkt relacji), Twoje Roguery (+0.1/pkt). Odejmuja: wrogosc
        /// (-0.5/pkt ujemnej relacji), poteha klanu (-4/tier). Sufit 35%.
        /// </summary>
        public static bool RealSpyChance(ref bool __result)
        {
            try
            {
                __result = false;
                if (Hero.MainHero.Gold < 250000) return false;
                var lord = Hero.OneToOneConversationHero;
                if (lord == null) return false;

                int honor = lord.GetTraitLevel(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultTraits.Honor);
                if (honor >= 1) return false;   // czlowiek honoru - nie ma ceny

                float chance = 0f;
                chance += Hero.MainHero.GetSkillValue(TaleWorlds.Core.DefaultSkills.Roguery) * 0.1f;
                float rel = lord.GetRelationWithPlayer();
                chance += rel > 0f ? rel * 0.3f : rel * 0.5f;
                if (honor < 0) chance += -honor * 12f;
                int calc = lord.GetTraitLevel(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultTraits.Calculating);
                if (calc > 0) chance += calc * 5f;
                if (lord.Clan != null) chance -= lord.Clan.Tier * 4f;
                if (chance < 0f) chance = 0f;
                if (chance > 35f) chance = 35f;

                __result = MBRandom.RandomFloat * 100f <= chance;
                return false;   // oryginalny rzut 20% pomijamy
            }
            catch (Exception e)
            {
                try { Scribe.Report("CrashScribe", e, "Mends.RealSpyChance", null); } catch { }
                __result = false;
                return false;
            }
        }

        public static bool SafeSpyFail(ConversationSentence __instance)
        {
            try
            {
                if (__instance == null || !"lord_spy_reaction_fail".Equals(__instance.Id)) return true;
                // odgrywamy tresc oryginalu (relacja, wojna, aresztowanie), bez feralnego wywolania
                var lord = Hero.OneToOneConversationHero;
                if (lord == null) return false;
                ChangeRelationAction.ApplyPlayerRelation(lord, -100);
                var playerFaction = Hero.MainHero.MapFaction;
                var lordFaction = lord.MapFaction;
                if (playerFaction != null && lordFaction != null && playerFaction != lordFaction &&
                    !playerFaction.IsAtWarWith(lordFaction))
                    DeclareWarAction.ApplyByDefault(playerFaction, lordFaction);
                InformationManager.DisplayMessage(new InformationMessage(
                    lord.Name + " was deeply offended! War has been declared.", Colors.Red));
                if (lord.PartyBelongedTo != null && MobileParty.MainParty != null)
                {
                    PlayerEncounter.RestartPlayerEncounter(MobileParty.MainParty.Party,
                        lord.PartyBelongedTo.Party, true, false);
                    PlayerEncounter.StartBattle();
                }
                return false;
            }
            catch (Exception e)
            {
                try { Scribe.Report("CrashScribe", e, "Mends.SafeSpyFail", null); } catch { }
                return false;   // lepiej pominac skutek niz polozyc gre
            }
        }
    }
}
