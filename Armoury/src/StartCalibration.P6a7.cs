namespace Armoury
{
    internal static partial class StartCalibration
    {
        /// <summary>p6-a7 (TownHire): regula A7 czynna (A7RuleOn) i w ostatnim przegladzie RuleDaily byla co najmniej jedna przebudowa zajecia w toku (_rActive) - wtedy
        /// wsie naprawde placa miastom targowym za przebudowe (CraftsRebuildPay). Tylko odczyt, watek glowny (tick dobowy, po StartCalibration.Daily).</summary>
        internal static bool A7Rebuilding { get { return A7RuleOn && _rActive > 0; } }
    }
}
