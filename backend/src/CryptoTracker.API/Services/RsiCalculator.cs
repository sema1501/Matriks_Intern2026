namespace CryptoTracker.API.Services;

public static class RsiCalculator
{
    public static decimal Calculate(
        IReadOnlyList<decimal> closingPrices,
        int period = 14)
    {
        if (closingPrices is null)
            throw new ArgumentNullException(nameof(closingPrices));

        if (period <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(period),
                "RSI periyodu sıfırdan büyük olmalıdır.");

        if (closingPrices.Count < period + 1)
            throw new ArgumentException(
                $"RSI hesaplamak için en az {period + 1} kapanış fiyatı gereklidir.",
                nameof(closingPrices));

        decimal totalGain = 0;
        decimal totalLoss = 0;

        for (var i = 1; i <= period; i++)
        {
            var difference = closingPrices[i] - closingPrices[i - 1];

            if (difference > 0)
                totalGain += difference;
            else
                totalLoss += Math.Abs(difference);
        }

        var averageGain = totalGain / period;
        var averageLoss = totalLoss / period;

        for (var i = period + 1; i < closingPrices.Count; i++)
        {
            var difference = closingPrices[i] - closingPrices[i - 1];
            var gain = difference > 0 ? difference : 0;
            var loss = difference < 0 ? Math.Abs(difference) : 0;

            averageGain =
                ((averageGain * (period - 1)) + gain) / period;

            averageLoss =
                ((averageLoss * (period - 1)) + loss) / period;
        }

        if (averageLoss == 0)
            return averageGain == 0 ? 50m : 100m;

        if (averageGain == 0)
            return 0m;

        var relativeStrength = averageGain / averageLoss;
        var rsi = 100m - (100m / (1m + relativeStrength));

        return Math.Round(rsi, 2);
    }
}