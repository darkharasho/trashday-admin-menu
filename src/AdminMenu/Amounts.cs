using System.Globalization;

namespace AdminMenu
{
    /// <summary>Parses the custom money field.</summary>
    internal static class Amounts
    {
        public const float MaxMoney = 1_000_000f;

        /// <summary>Accepts "100", "$1,000" or "12.5"; refuses anything that isn't a positive amount up to <see cref="MaxMoney"/>.</summary>
        public static bool TryParseMoney(string text, out float amount)
        {
            amount = 0f;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            var cleaned = text.Trim().TrimStart('$').Replace(",", string.Empty);
            if (!float.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return false;
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f || value > MaxMoney)
                return false;
            amount = value;
            return true;
        }
    }
}
