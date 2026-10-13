export const meta = {
  name: 'sucho-85-modele',
  description: 'Gospodarka od ludzi - rachunek na sucho, czy swiat utrzyma 85 tys. wojska lordow w wojnie: 4 niezalezne modele (rody, krolestwa, pobor, garnek korony)',
  phases: [ { title: 'Modele', detail: '4 niezalezne rachunki, skrypty sparametryzowane biegiem' } ],
}
const REPO = String.raw`C:\Users\GAME\Bannerlor2-Realm-of-Thrones`
const SC = String.raw`C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad`
const INT = SC + String.raw`\zb\int`
const SD = String.raw`C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\dbc3d3d1-6a1e-4825-afe7-38583389c73c\scratchpad`
const OUT = String.raw`C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\c36b73ca-1de0-4b1b-80ce-71bd02afe545\scratchpad\sucho85`
const LOGDIR = String.raw`D:\Backup-Bannerlord\logs\2026-10-13-noc34-35`
const env = String.raw`Mody Bannerlorda 1.4.8 (Realm of Thrones + BannerKings.Redux + nasze Armoury / CrashScribe). NAJPIERW przeczytaj ${REPO}\CLAUDE.md (caly). Katalog roboczy sesji = folder gry: NIC tam nie zapisuj, gry NIE uruchamiaj (wlasnie biegnie w niej test roczny - nie ruszaj plikow w folderze gry ani w Documents), niczego nie wgrywaj, NIE commituj. To jest RACHUNEK NA PAPIERZE (python na danych z biegow), bez zmiany kodu moda.
PISZ TYLKO do ${OUT}\<twoj_podkatalog>\ (utworz). Python: "python -I skrypt.py" (na tym komputerze "python3" to atrapa); w kazdym skrypcie sys.stdout.reconfigure(encoding='utf-8'); logi maja ok. 120 MB - czytaj strumieniowo, filtruj po poczatku linii. Komentarze bez polskich znakow.
KAZDY SKRYPT PARAMETRYZOWANY: --log <Armoury-*.log> --tabele <katalog Logs\<data>> (oraz --okno 28), zeby dalo sie go puscic ponownie na nowym biegu rocznym (bedzie gotowy za ok. godzine, ten sam format plikow).
ZRODLA:
- Projekt (przeczytaj rozdz. 0, 3, 4, 5, 8, 9, 10): ${REPO}\docs\gospodarka-od-ludzi\PROJEKT-GOSPODARKA-OD-LUDZI-2026-10-13.md. Odpowiedzi Jeffa na pytania z rozdz. 10 (WIAZACE): 1 TAK (sila krolestw za ludzmi, bez hamulca; przed kodem rachunek na sucho, czy swiat utrzyma 85 tys. wojska); 2 "lup i milicja bez wyjatku" (Wyspy, Wolni Ludzie, Dothrakowie - bez wyjatku w produkcji); 3 TAK (Nocna Straz - jawny wyjatek ze stala dzisiejsza produkcja; Sarnor 0.1 -> ok. 0.25 mln ludzi); 4 zostawic Qarth i zmierzyc; 5 TAK (tylko istniejace poprawki na glowe); 6 len przez nowe symbole wsi, sol x2; 7 TAK: w NOWEJ kampanii zloto startowe kas kupcow miast, kies ludu i kies wsi wedlug ludzi + skarbce koron wedlug ludzi (ta sama suma swiata); kiesy (zloto) rodow bez zmian.
- Rachunki recenzji projektu (gotowe skrypty i wyniki - UZYJ, nie licz od zera tego, co juz jest): ${SD}\gosp-ludzie\skrypty-rec\ (r01_krolestwa.py/.json/.out.txt - ludzie, dochod, wojsko na krolestwo; r04_dochod_wojsko.py/.out.txt/.json - D dzis / pierwszy krok / granica na krolestwo; r05_miasta, r06_lud_po_zmianie - kiesy ludu). Rozpoznania: ${REPO}\docs\gospodarka-od-ludzi\robocze\.
- Raport roczny o wojsku (luka do limitu druzyn = w 71% pulap zoldu; zalogi placone pierwsze; rody na pulapie vs z luzem; "pan bez klopotu z pieniadzem trzyma ok. 60% druzyny"): ${REPO}\docs\test-zbiorczy\raport-roczny-2026-10-13\wojsko.md i wojsko-weryfikacja.md (oraz RAPORT-ROCZNY-2026-10-13.md rozdz. 0).
- Kod (galaz w-toku/zbior-e8 = to, co jest w grze, WGRANIE 43): ${INT}\Armoury\src - ClanBudget.cs (pulap zoldu: pokoj 0.28 x D, wojna 0.60 x D + 0.8 x zloto ponad rezerwe / 45; zalogi placone pierwsze), ClanIncomeBook*.cs (D), CrownRents.cs, CrownIncome.cs, KingdomTreasury*.cs (WarTax = podatek 184 i stawka daniny P1; WarAid; NeedAid = W5 doplata wedlug potrzeby; Scutage = W6 tarczowe; CrownSurplus = W5b), TownFolk.Tithe.cs / TownFolk.Savings.cs (danina P1 z oszczednosci ludu), Levy.cs, FreeHands.cs, ArmsFromPeople.cs, PopulationLaw.cs, CrownBorrow.cs.
- MIARA PROGU 85 tys. (dokladnie ta): ${INT}\tools\sprawdz_logi.py - funkcja kingdom_war_series (linie logu "Skarbce: dzien D - K (WOJNA): ... wojsko rodow N ludzi") i blok "W5-7": suma "wojsko rodow" krolestw w stanie WOJNA, srednia ostatnich 28 dob. Bieg roczny WGRANIA 37 (bez daniny P1, bez W5/W6/W5b): 79.4 tys. w d337-364, a w d93-120 95.6 tys. (rozruch na kiesach startowych).
- BIEGI (log + tabele CSV; budzet-rodow.csv ma wiersz rod x doba: D, kiesa_glowy, pulap, zold_partii, zold_zalog, ludzi_partie, ludzi_zalogi, limit_partii, na_suficie, d_ziemia, d_korona, doplata_w5, tarczowe_w6, doplata_ze_skarbca_w5b ...; balans-krolestw.csv; ludzie-regiony.csv; kiesa-ludu.csv; kasy-miast.csv):
  * bieg roczny WGRANIA 37 (364 doby, przed P1/W5/W6/W5b): ${LOGDIR}\Armoury-2026-10-13_06-24-10.log + ${LOGDIR}\Logs\2026-10-13_06-24-10\
  * biegi 120 dob (kazdy nowa kampania; wojsko lordow w wojnie d93-120 w nawiasie): P1 danina ${LOGDIR}\Armoury-2026-10-13_10-30-28.log (105.6 tys.), N3 _11-11-41 (99.9), W5 _11-46-24 (106.7), W6 _12-21-21 (101.7), N5 _12-59-58 (111.2), WGRANIE 43 = wszystko, co w grze: ${LOGDIR}\Armoury-2026-10-13_13-28-08.log + ${LOGDIR}\Logs\2026-10-13_13-28-08\ (109.0 tys.). Tabele kazdego w ${LOGDIR}\Logs\<ta sama data>\. Rozne kampanie roznia sie wojskiem o +-5 tys. - nie wyciagaj wnioskow z jednej roznicy.
SCENARIUSZE (wszyscy licza te same; wynik zawsze: wojsko lordow krolestw w wojnie, srednia 28 dob KONCA ROKU, swiat + na krolestwo, widelki nisko-wysoko, i porownanie z 85 tys.):
  S0 = dzis (kod WGRANIA 43, bez reguly) na koniec roku;
  S1 = "pierwszy krok": reguly P1-P3 projektu wlaczone na kampanii, w ktorej zloto lezy jak dzis (za ludzmi idzie od razu tylko ok. 19% D - podatki wprost od ludzi; r04 "D po 1. kroku");
  S2 = "granica": D krolestw w pelni wedlug ludzi (r04 "D po");
  S3 = NOWA kampania wedlug pyt. 7 (zloto startowe kas miast, kies ludu, kies wsi i skarbce koron wedlug ludzi) - jak blisko S2 swiat zaczyna i co z wojskiem w roku 1;
  oraz NAKLADKA P4 (pobor i milicja wedlug ludzi: szansa ochotnika x min(3, max(0.1, skala x udzial zbrojnych)); Wyspy udzial 2, Wolni Ludzie 5, Dothrakowie 3, reszta 1) na S1-S3, plus pyt. 3 (Straz stala produkcja, Sarnor 0.25 mln).
Kazde liczba oznacz [P] pomiar / [S] szacunek; czego nie da sie ustalic - napisz "nie wiem" i dlaczego. Raport prostym jezykiem po polsku w pliku RAPORT.md w twoim podkatalogu (rozdz. 0 - wynik w 10 liniach), skrypty obok. ZWRACAJ KROTKO przez StructuredOutput.`

const SCHEMA = {
  type: 'object',
  properties: {
    katalog: { type: 'string' },
    skrypty: { type: 'array', items: { type: 'string' }, description: 'pelne sciezki skryptow + jak je uruchomic' },
    s0_tys: { type: 'string', description: 'S0 wojsko lordow w wojnie koniec roku, tys. (widelki)' },
    s1_tys: { type: 'string' },
    s2_tys: { type: 'string' },
    s3_tys: { type: 'string' },
    p4_wplyw: { type: 'string', description: 'ile zmienia nakladka P4 w S1/S2/S3' },
    wniosek: { type: 'string', description: 'max 8 zdan: czy swiat utrzyma 85 tys.; ktore krolestwa traca / zyskuja; czego nie wiem' },
    zalozenia_krytyczne: { type: 'array', items: { type: 'string' }, description: 'zalozenia, od ktorych wynik zalezy najbardziej' },
  },
  required: ['katalog', 'skrypty', 's0_tys', 's1_tys', 's2_tys', 's3_tys', 'p4_wplyw', 'wniosek', 'zalozenia_krytyczne'],
}

const TASKS = [
  { key: 'rody', label: 'model-rodow', prompt: String.raw`TWOJ PODKATALOG: rody. ZADANIE: MODEL OD DOLU, ROD PO RODZIE.
1. Odtworz z kodu (ClanBudget.cs i to, co go karmi - D z ClanIncomeBook, rezerwa, skrzynia wojenna, kolejnosc: zalogi pierwsze, potem druzyny; limit druzyn; krok werbunku; zwolnienia) DOKLADNY wzor pulapu zoldu rodu i tego, ile ludzi w druzynach z niego wychodzi. Wypisz plik:linia.
2. KALIBRACJA: na budzet-rodow.csv biegu rocznego W37 (ostatnie 28 dob) i biegu W43 120 dob (d93-120) policz z modelu ludzi w druzynach kazdego rodu z jego D, kiesy, zoldu zalog, limitu i zoldu na glowe (zmierz zold na glowe druzyny rodu: zold_partii / ludzi_partie) - porownaj z pomiarem (ludzi_partie): blad na swiat i na krolestwo. Wyjasnij zjawisko "pan bez klopotu z pieniadzem trzyma ok. 60% druzyny" (raport) - albo przyjmij je jako zmierzony sufit wypelnienia rodu z luzem i powiedz, skad liczba. Dodaj W5/W5b/W6 (kolumny doplata_w5, podniesienie_pulapu_w5, tarczowe_w6, doplata_ze_skarbca_w5b) tak, jak dzialaja w kodzie.
3. SCENARIUSZE S0-S3: zmien D rodow wedlug krolestwa (czynnik krolestwa z r04: pierwszy krok i granica; w obrebie krolestwa podziel zmiane D wedlug skladnikow d_ziemia / d_korona rodu tak, jak dziala kod rent i podatkow - opisz zalozenie), skrzynie (kiesy rodow) bez zmian (pyt. 7), zalogi bez zmian (normy na twierdze); policz ludzi w druzynach kazdego rodu, sumuj krolestwa w wojnie (stan wojny jak w biegu - zalozenie). Dla S0 (bez reguly) przenies wynik roczny W37 na kod W43: oszacuj, ile W5/W5b/W6/P1 dodaja na koniec roku (z biegu W43 120 dob wobec W37 d93-120 i z mechaniki), z widelkami.
4. Wrazliwosc: ktore 5 krolestw decyduje o sumie; co jesli wypelnienie rodow z luzem jest 0.55 / 0.64 / 0.75; co jesli pierwszy krok trwa caly rok, a co jesli swiat jest w granicy.` },
  { key: 'krolestwa', label: 'model-krolestw', prompt: String.raw`TWOJ PODKATALOG: krolestwa. ZADANIE: MODEL OD GORY, NIEZALEZNY OD MODELU ROD PO RODZIE (inny zespol liczy rod po rodzie - nie kopiuj jego metody).
1. Zmierz EMPIRYCZNIE, ile ludzi w druzynach daje 1 zl/d stalego dochodu (D) i 1 zl/d doplaty, osobno dla krolestw "na pulapie" (druzyny daleko od limitu) i "na limicie" (druzyny blisko limitu): (a) przekroj 29 krolestw na koniec biegu rocznego W37 (wojsko krolestwa vs D, zold zalog, limit druzyn, zloto rodow), (b) szereg czasowy w biegu rocznym (jak wojsko krolestwa idzie za jego D z opoznieniem - regula budzetu uczy sie D z 28 dob), (c) roznica biegow 120 dob przed i po W5 / W6 / W5b (ile tys. ludzi daly doplaty na zl/d wyplacone - uwaga na rozrzut kampanii +-5 tys.).
2. S0: oszacuj wojsko lordow w wojnie na KONIEC ROKU z kodem WGRANIA 43 (W37 rok: 79.4 tys. d337-364 i 95.6 d93-120; W43 120 dob: 109.0 d93-120) - jak sie zmienia od d120 do d364 i dlaczego (skrzynie startowe topnieja, skarbce koron spadaja z 58 do 27 mln, wojny); widelki.
3. S1, S2, S3: zastosuj elastycznosci do zmian D krolestw z r04 (pierwszy krok, granica), z sufitem limitu druzyn krolestwa i z faktem, ze zalogi sa placone pierwsze (zold zalog krolestwa vs nowe D - w ilu krolestwach pulap nie pokryje nawet zalog). Krolestwa w wojnie - jak w biegu (zalozenie) oraz wariant: najbiedniejsze krolestwa po zmianie czesciej przegrywaja i traca twierdze (oszacuj, czy to podnosi czy obniza sume wojska lordow w wojnie).
4. Napisz, czym twoj wynik rozni sie od tabeli projektu ("wojsko w granicy 131-167 tys." - to druzyny + ZALOGI razem; prog 85 tys. to same druzyny lordow krolestw w wojnie).` },
  { key: 'pobor', label: 'pobor-i-milicja', prompt: String.raw`TWOJ PODKATALOG: pobor. ZADANIE: CZY PO REGULE WOJSKO KROLESTW W OGOLE ZDAZY SIE ZWERBOWAC (nakladka P4) - i co robi milicja.
1. Odtworz z kodu droge werbunku lorda AI: ochotnicy u notabli (BK, szansa pustego miejsca - Levy.cs ok. :98-141), hamulec wolnych rak (FreeHands.cs: 20% mezczyzn krainy, Wyspy 40%, Wolni Ludzie bez hamulca), pobor z ksiegi ludzi (ArmsFromPeople.cs), krok werbunku z pulapu (ClanBudget / NeedAid "krok werbunku 10% limitu/d"), koszt rekruta. Plik:linia.
2. ZMIERZ w biegu rocznym W37 i biegu W43 120 dob: rekrutow na dobe na krolestwo (lordowie AI), straty druzyn na dobe (zabici, jency, dezercja, rozbici) na krolestwo, odmowy hamulca wolnych rak (Sarnor ok. 328/d), puste miejsca u notabli, ile miejsc ochotnikow zostaje niewykorzystanych (czy dzis werbunek wiaze, czy pieniadz).
3. Z P4 (szansa x min(3, max(0.1, u_v x udzial zbrojnych)); skale wsi u_v i rynku m_t na krolestwo z r04/r01 i z projektu rozdz. 3.2) policz, w ktorych krolestwach tempo werbunku spadnie ponizej strat wojny (druzyny nie odbuduja sie mimo pieniedzy) - ile tys. ludzi lordow w wojnie to kosztuje na koniec roku w S1/S2/S3; a w ktorych wzrosnie (pieniadz wtedy wiaze). Milicja: przyrost x skala (ubytek 2.5% stanu) - ile milicji ma kazde krolestwo przed i po (milicja broni twierdz, ale NIE liczy sie do progu 85 tys. - napisz, jak zmieni wynik oblezen w pustych krolestwach).
4. Pyt. 3: Sarnor 0.25 mln - ile odmow hamulca zniknie; Nocna Straz - ludzie przychodza przez WatchLaw z 10 krain Westeros - czy P4 ich dotyka.` },
  { key: 'garnek', label: 'garnek-korony', prompt: String.raw`TWOJ PODKATALOG: garnek. ZADANIE: PIENIADZ KORON I DOPLATY PO REGULE (danina P1, W5, W5b, W6, skarbce, Zelazny Bank 186) - czy doplaty przesuna sie tam, gdzie po regule brakuje wojska.
1. Z kodu (KingdomTreasury*.cs, TownFolk.Tithe.cs / TownFolk.Savings.cs, CrownBorrow.cs, CrownRents.cs) opisz krotko kazdy strumien do garnka korony i z garnka do rodow (plik:linia), z biegu W43 120 dob zmierz go na krolestwo (zl/d, d93-120), a z biegu rocznego W37 - skarbce i podatek 184 na koniec roku.
2. Po regule (projekt rozdz. 5 i r06_lud_po_zmianie): lud ludnych miast (Reach, Qarth, Braavos, Volantis, Pentos) wyda oszczednosci na wiekszy koszyk -> danina P1 tam spada; lud pustych krain (Polnoc, Wyspy, Dorne, Wolni Ludzie) zarabia 2-6 x wiecej, niz potrzebuje -> oszczednosci i danina rosna. Policz na krolestwo zmiane daniny P1 i garnka W5 w S1 i S2 (zl/d) oraz ile to ludzi w druzynach (zold na glowe z budzet-rodow.csv) - czy garnek korony pustych krolestw zlagodzi spadek ich D.
3. S3 (nowa kampania, skarbce koron wedlug ludzi przy tej samej sumie 57.9 mln w dobie 1, z podloga dla najmniejszych - zaproponuj podloge): ile kazde krolestwo dostaje na start, jak szybko skarbiec schodzi do zapasu wojny (W5b doklada 1/30 nadwyzki dziennie, zwrot 1/360 zapasu), czy biedne krolestwa beda pozyczac w Banku (186) i z jakim skutkiem dla wojska w roku 1.
4. Zapas kupcow x m_t (projekt rozdz. 5: puste miasta oddadza jednorazowo 2.28 mln panom, ludne zatrzymaja 2.56 mln) - jak to przesuwa D i wojsko w pierwszych miesiacach S1, a jak w S3 (kasy startowe wedlug ludzi).` },
]

phase('Modele')
const res = await parallel(TASKS.map(t => () => agent(env + '\n\n' + t.prompt, { label: t.label, phase: 'Modele', schema: SCHEMA })))
return TASKS.map((t, i) => ({ key: t.key, wynik: res[i] }))
