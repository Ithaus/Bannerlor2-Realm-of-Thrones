using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// DLUGI MARSZ, DLUGIE RACJE (Jeff 15.09: "ruch obnizylismy, wiec wszystko
    /// porusza sie wolniej, a jedzenie zostalo na starym torze - obniz zuzycie
    /// jedzenia dla gracza i AI o 40%").
    ///
    /// Nasze prawa predkosci (WorldPace, MarchPace, TerrainEase) i dlugi rok
    /// (Calendar: 168 dni) rozciagnely KAZDA droge na wiecej dni, a jedzenie
    /// schodzi NA DZIEN - ta sama trasa kosztuje teraz kilka razy wiecej zapasu.
    /// Stad glodujace partie w polu, a za nimi kaskada ran (SlowHealing.StarvePostfix).
    /// Tniemy zuzycie o suwak (dom. 40%) - rowno graczowi i AI.
    ///
    /// GDZIE: MobileParty.FoodChange wola
    /// Campaign.Current.Models.MobilePartyFoodConsumptionModel.CalculateDailyFoodConsumptionf -
    /// to jest JEDYNA brama, przez ktora przechodzi dzienne zuzycie partii (baza,
    /// perki, zwierzeta, zima). Nie ruszamy zapasow osad ani produkcji wiosek.
    ///
    /// KTORY MODEL: BannerKings podstawia swoj (BKPartyConsumptionModel dziedziczy
    /// po DefaultMobilePartyFoodConsumptionModel i NADPISUJE te metode, nie wolajac
    /// base) - latka na klasie vanilli byla by martwa. Skanujemy wiec typy jak
    /// HungerLaw, ale bierzemy tylko LISCIE hierarchii: typ, ktory deklaruje metode
    /// i po ktorym nie dziedziczy zaden inny deklarujacy. Gdyby czyjs model jednak
    /// wolal base, ciecie nie nalozy sie dwa razy.
    /// </summary>
    internal static class Rations
    {
        private static readonly TextObject _text = new TextObject("{=armRations}Rations stretched", null);

        private static System.Reflection.MethodInfo Declared(Type t)
        {
            return t.GetMethod("CalculateDailyFoodConsumptionf",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
        }

        /// <summary>Priority.Last - po BK i po wszystkich, co dokladaja swoje.</summary>
        public static void Postfix(ref ExplainedNumber __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                int cut = s.FoodConsumptionCutPercent;
                if (cut <= 0) return;
                if (cut > 90) cut = 90;                       // zawsze cos zjedza
                if (__result.ResultNumber >= 0f) return;      // dodatnie to nie zuzycie
                // ExplainedNumber liczy: wynik = Base * (1 + SumOfFactors), a Add()
                // zmienia Base. Chcemy wynik dokladnie k-krotny (k = 1 - cut/100)
                // i NIE chcemy stracic rozpiski w dymku, wiec dokladamy czynnik
                // -(cut/100) * (1 + SumOfFactors):
                //   Base * (1 + F - (cut/100)(1+F)) = k * Base * (1 + F).
                // Sufit z modelu (vanilla LimitMax -0.01, BK 0) dziala dalej.
                __result.AddFactor(-(cut / 100f) * (1f + __result.SumOfFactors), _text);
            }
            catch { }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || s.FoodConsumptionCutPercent <= 0)
                { Log.Info("Rations: zuzycie jedzenia bez zmian (suwak 0)."); return; }

                var declaring = new List<Type>();
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (System.Reflection.ReflectionTypeLoadException r) { types = r.Types; }
                    catch { continue; }
                    foreach (var t in types)
                    {
                        if (t == null || !typeof(MobilePartyFoodConsumptionModel).IsAssignableFrom(t)) continue;
                        System.Reflection.MethodInfo m;
                        try { m = Declared(t); } catch { continue; }
                        if (m == null || m.IsAbstract) continue;
                        declaring.Add(t);
                    }
                }

                int done = 0; string names = "";
                var post = new HarmonyMethod(typeof(Rations), "Postfix") { priority = Priority.Last };
                foreach (var t in declaring)
                {
                    bool leaf = true;
                    foreach (var o in declaring) if (o != t && t.IsAssignableFrom(o)) { leaf = false; break; }
                    if (!leaf) continue;                       // po nim dziedziczy inny model - ten jest w uzyciu
                    // kazda latka osobno - egzotyczny model nie moze polozyc reszty
                    try { h.Patch(Declared(t), postfix: post); done++; names += (names.Length > 0 ? ", " : "") + t.Name; }
                    catch (Exception pe) { Log.Error("Rations.Patch(" + t.Name + ")", pe); }
                }
                Log.Info("Rations: zuzycie jedzenia -" + s.FoodConsumptionCutPercent
                         + "% dla gracza i AI (dlugi marsz, dlugi rok) - zalatane modele: "
                         + (done > 0 ? names : "BRAK - zuzycie bez zmian") + ".");
            }
            catch (Exception e) { Log.Error("Rations.ApplyAll", e); }
        }
    }
}
