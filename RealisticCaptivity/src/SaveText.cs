using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace RealisticCaptivity
{
    /// <summary>
    /// n20-niewola: napis do zapisu kampanii bez ryzyka limitu 32 KB (gra zapisuje dlugosc napisu na 2 bajtach - dluzszy psuje wczytanie; Armoury 161
    /// SaveText.Sync). RC nie zalezy od Armoury, wiec tu ta sama droga zapisu (bez ratunku przy wczytaniu i bez pakowania - to robi Armoury dla calej
    /// gry): napis dluzszy niz Part znakow idzie jako lista kawalkow pod kluczem + "_parts", sam klucz dostaje "". Krotki napis - zwykly SyncData.
    /// Stary zapis bez klucza - value null (wolajacy traktuje jak pusty).
    /// </summary>
    internal static class SaveText
    {
        private const int Part = 8000;               // UTF-8 najwyzej 3 B na znak -> 8000 znakow < 32763 B
        private const string PartsSuffix = "_parts";

        internal static void Sync(IDataStore ds, string key, ref string value)
        {
            if (ds.IsSaving)
            {
                string v = value ?? "";
                if (v.Length <= Part) { ds.SyncData(key, ref v); return; }
                var parts = new List<string>(v.Length / Part + 1);
                int i = 0;
                while (i < v.Length)
                {
                    int n = Math.Min(Part, v.Length - i);
                    if (n < v.Length - i && char.IsHighSurrogate(v[i + n - 1])) n--;   // nie tniemy pary zastepczej
                    parts.Add(v.Substring(i, n));
                    i += n;
                }
                string head = "";
                ds.SyncData(key, ref head);
                ds.SyncData(key + PartsSuffix, ref parts);
                return;
            }
            if (ds.IsLoading)
            {
                string head = null;
                ds.SyncData(key, ref head);
                List<string> got = null;
                ds.SyncData(key + PartsSuffix, ref got);
                value = got != null && got.Count > 0 ? string.Concat(got) : head;
            }
        }
    }
}
