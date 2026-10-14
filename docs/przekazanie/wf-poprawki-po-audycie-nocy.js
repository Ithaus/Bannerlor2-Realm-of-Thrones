export const meta = {
  name: 'poprawki-po-audycie-nocy',
  description: 'Poprawki DROBNE z audytu integracji nocy 13/14.10 (gracz: napisy, L3 zakupy gracza, MCM grupy, L5b-2 udzwig, W7 LDayOut, lup w ksiedze kas, start gold wies - diagnoza); autor + recenzent z poprawka',
  phases: [ { title: 'Kod' }, { title: 'Recenzja' } ],
}
const S3 = String.raw`C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad`
const LIBS = S3 + String.raw`\lancuch\libs`
const SC = String.raw`C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad`
const REPO = String.raw`C:\Users\GAME\Bannerlor2-Realm-of-Thrones`
const AUD = String.raw`C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\c36b73ca-1de0-4b1b-80ce-71bd02afe545\scratchpad\audyt`
const DZ = SC + String.raw`\zb\paud`
const BR = 'w-toku/audyt-noc-poprawki'
const BASE = '43af413'
const env = String.raw`Mody Bannerlorda 1.4.8 (Realm of Thrones + BannerKings + nasze Armoury). Przeczytaj ${REPO}\CLAUDE.md (caly). Katalog roboczy = folder gry: NIC tam nie zapisuj, gry NIE uruchamiaj (biegnie bieg dwuletni), niczego nie wgrywaj.
DRZEWO: ${DZ} (galaz ${BR}, od w-toku/zbior-e8 ${BASE} = wersja koncowa nocy). Pracuj TYLKO w nim. Raporty audytu integracji: ${AUD}\{obieg,zapis,gracz}\RAPORT.md (przeczytaj wszystkie trzy; weryfikacja obalila znaleziska WAZNE o zapisie, o koniach / zaworze i o skarbcu upadlego krolestwa - tych NIE ruszaj).
ZASADY: poprawki MALE i bezpieczne (to ostatnia paczka przed wgraniem; po niej tylko krotki test 40 dob + zapis): napisy w grze po ANGIELSKU, komentarze po polsku bez polskich znakow, .cs z CRLF, bez nowych pol zapisu, bez zmiany regul gospodarki (liczb, stawek) - tylko to, co nizej. Po Settings.cs "python tools/gen_mcm.py". Build: cd "${DZ}\Armoury" && dotnet build Armoury.csproj -c Release -v q --nologo "-p:GameLibs=${LIBS}" > build.log 2>&1; echo rc=$? (kod 0). Progi w tools/sprawdz_logi.py tylko jesli konieczne (py_compile kopii). Commit konczy sie "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>". Nie uzywaj spawn_task. ZWRACAJ KROTKO (max 25 linii).`
const zad = String.raw`POPRAW (kazde jako osobny maly commit, wpis w CHANGELOG - jeden zbiorczy wpis "Poprawki po audycie integracji nocy 13/14.10" z lista):
1. L3 a gracz: ludzie gracza nie kupuja co dobe w lupionym miescie przez okno L3 (MenPurse.OnDailyTickParty przed BuyPlayerGaps / MenUpgrade - ta sama bramka CitySack co odziez); komunikat "they spend nothing here" tylko gdy druzyna gracza brala udzial w lupie tego miasta (inaczej bez komunikatu i bez blokady dla gracza, ktory nie lupil - sprawdz, czy to zgodne z decyzja Jeffa 3 "wojsko zdobywcy"; jesli regula obejmuje cale wojsko krolestwa, zostaw blokade, popraw tylko tresc komunikatu, zeby nie twierdzil, ze gracz lupil).
2. Napisy lupu przy ZAMKU: zamiast "Merchants' coin" / "Townsfolk's coin" - "Castle treasury" / "Castle folk's coin" (CitySack.Menu.cs, CitySack.Coin.cs, okno People); jedno slowo na pieniadz w komunikatach lupu: "denars" (nie "coins"), separator tysiecy jak w grze ({0:N0}).
3. MCM: klucze lupu L0-L7 i 5b (CapitalTreasurySeized, CapitalTreasuryShare, EnemyCaravansSeized) w JEDNEJ grupie "The sack of towns and castles"; StragglersRally* w sensownej grupie (wojna / bitwy, nie "Iron Bank"); po zmianie gen_mcm. Opisy kluczy ze stanem w kampanii (StragglersRally, SackBurnsBuildings, StartGoldByPeople, GoodsRatesByPattern, VillageOutputByPeople) - jedno zdanie po angielsku, co sie stanie przy wylaczeniu w trakcie kampanii.
4. L5b-2: ladunek zajetych karawan do udzwigu wodza tak jak L7 (wolny udzwig, limit stada; nadmiar zostaje na polce zdobytej osady - nie znika).
5. W7: TownFolk.LordLevy.cs - dopisz f.LDayOut przy poborze (jak inne zdjecia: lup, konie, P2, P1), zeby wiersz osady w kiesa-ludu.csv sie bilansowal.
6. L2: MoneyLedger - nazwana pozycja "lup miast (L2)" dla kasy kupcow zdejmowanej przy lupie (MoneyLedger.Note), zeby "Przeplywy osad" nie wrzucaly lupu do "reszta - inne".
7. Zloto startowe kies wsi (PeopleMarket.cs ok. :645-697): ubytek ok. 4 000 zl w dobie 1 w kazdym biegu - DIAGNOZA: przy real != d zaloguj id wsi, d, real, Gold przed / po (jedna linia "Zloto startowe wedlug ludzi (GL2): wies niepelna ..."), b1 licz z faktycznych przelewow; jesli znajdziesz przyczyne w kodzie (cudza latka, nasza latka na ChangeGold / Gold, przyciecie) - napraw tak, zeby suma przed = po; jesli nie - zostaw diagnoze.
8. Wyjasnienia tarczowego / doplaty dla gracza (ScutagePlayer) - nie czesciej niz raz na 30 dob gry w sesji (dzis po kazdym wczytaniu).
Na koniec: build kod 0, gen_mcm, CHANGELOG, commit. NIE wypychaj.`
phase('Kod')
const code = await agent(`${env}\n\nJESTES AUTOREM.\n${zad}\n\nZwroc: HEAD, co zrobione w kazdym punkcie (1 zdanie), czego nie i dlaczego, ryzyka.`, { label: 'autor-poprawki-audytu', phase: 'Kod' })
if (code == null) return { blad: 'autor bez wyniku' }
phase('Recenzja')
const rev = await agent(`${env}\n\nJESTES RECENZENTEM Z POPRAWKA.\n${zad}\n\nDiff: "git -C ${DZ} diff ${BASE}..HEAD". Sprawdz: kazda zmiana mala i zgodna z punktem; zadna nie zmienia liczb gospodarki ani zlota (poza nazwaniem pozycji w ksiedze i zamknieciem ubytku zlota startowego); napisy po angielsku; brak nowych wyjatkow (null-e: zamek bez kasy, gracz bez druzyny, karawana bez wodza); build kod 0; gen_mcm; progi dalej dzialaja na logu D:\\Backup-Bannerlord\\logs\\2026-10-14-noc\\Armoury-2026-10-13_20-41-38.log (grupy lup, lup5b, w7, kiesa1, gl2 - uruchom; py_compile kopii). Popraw problemy (commit). Na koniec wypchnij DWOMA OSOBNYMI poleceniami: "git -C ${DZ} push origin ${BR}" i "git -C ${REPO} push origin ${BR}". Zwroc: HEAD, co poprawiles, ryzyka, co sprawdzic w tescie 40 dob.\n\nOpis autora:\n${code}`, { label: 'rec-poprawki-audytu', phase: 'Recenzja' })
return { code, rev }
