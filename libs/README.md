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

## Szybciej

Zamiast kopiowac, wskaz katalogi wprost przy budowaniu:

    dotnet build Armoury/Armoury.csproj -c Release -p:GameLibs="C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client"

(wtedy i tak brakuje 0Harmony/MCMv5/UIExtenderEx/BannerKings — te cztery skopiuj do libs/)
