# libs — biblioteki gry (NIE trafiaja do repozytorium)

Skopiuj tu ponizsze pliki DLL. Wszystkie sa juz na dysku, w instalacji gry i modow.
Nie commituj ich — `.gitignore` je pomija (to wlasnosc TaleWorlds i autorow modow).

## Z `Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\`

    TaleWorlds.CampaignSystem.dll
    TaleWorlds.CampaignSystem.ViewModelCollection.dll
    TaleWorlds.Core.dll
    TaleWorlds.Core.ViewModelCollection.dll
    TaleWorlds.DotNet.dll
    TaleWorlds.Engine.dll
    TaleWorlds.InputSystem.dll
    TaleWorlds.Library.dll
    TaleWorlds.Localization.dll
    TaleWorlds.MountAndBlade.dll
    TaleWorlds.ObjectSystem.dll
    TaleWorlds.SaveSystem.dll
    TaleWorlds.ScreenSystem.dll

## Z katalogow modow (`Modules\<Mod>\bin\Win64_Shipping_Client\`)

    0Harmony.dll                 <- Bannerlord.Harmony
    MCMv5.dll                    <- Bannerlord.MBOptionScreen
    Bannerlord.UIExtenderEx.dll  <- Bannerlord.UIExtenderEx
    BannerKings.dll              <- BannerKings.Redux

## Z `Modules\SandBox\bin\Win64_Shipping_Client\` (Armoury: wioski na mapie, W2)

    SandBox.dll
    SandBox.View.dll

Gdy ich tu nie ma, `Armoury.csproj` bierze je wprost z katalogu modulu SandBox gry
(wlasciwosc `SandBoxLibs`, domyslnie `C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBox/bin/Win64_Shipping_Client`).

## Z `Modules\Native\bin\Win64_Shipping_Client\` (Armoury: tekstury proporczykow wiosek, sw-herby)

    TaleWorlds.MountAndBlade.View.dll

Gdy jej tu nie ma, `Armoury.csproj` bierze ja wprost z katalogu modulu Native gry
(wlasciwosc `NativeViewLibs`, domyslnie `C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Native/bin/Win64_Shipping_Client`).

## Z `Modules\ROT-Core\bin\Win64_Shipping_Client\` i `bin\Win64_Shipping_Client\` (Armoury: okna opowiesci i obrazki z plikow, m103-sceny)

    ROT.dll                      <- ROT-Core (Realm of Thrones)
    TaleWorlds.TwoDimension.dll  <- bin\Win64_Shipping_Client gry

Gdy ich tu nie ma, `Armoury.csproj` bierze je wprost z katalogow gry (wlasciwosci `RotLibs`, domyslnie
`C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/ROT-Core/bin/Win64_Shipping_Client`, i `TwoDimLibs`,
domyslnie `C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client`). Kopii w libs/
lepiej nie robic - dysk C zeruje pliki. Armoury dziedziczy po `ROT.Events.EventBase` (okno w wygladzie wydarzen ROT), wiec po aktualizacji
ROT trzeba przebudowac Armoury i sprawdzic okno (typ ROT uzywany tylko w metodach NoInlining, zapas: zwykle okno gry z tym samym tekstem).

## Szybciej

Zamiast kopiowac, wskaz katalogi wprost przy budowaniu:

    dotnet build Armoury/Armoury.csproj -c Release -p:GameLibs="C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client"

(wtedy i tak brakuje 0Harmony/MCMv5/UIExtenderEx/BannerKings — te cztery skopiuj do libs/)
