internal static class Metrics
{
    public static double P95(IEnumerable<double> values)
    {
        double[] sorted = values.OrderBy(static value => value).ToArray();
        if (sorted.Length == 0) return double.NaN;
        int index = Math.Max(0, (int)Math.Ceiling(sorted.Length * 0.95) - 1);
        return sorted[index];
    }

    public static double LinearSlope(IReadOnlyList<(double X, double Y)> points)
    {
        double meanX = points.Average(static p => p.X);
        double meanY = points.Average(static p => p.Y);
        double numerator = points.Sum(p => (p.X - meanX) * (p.Y - meanY));
        double denominator = points.Sum(p => (p.X - meanX) * (p.X - meanX));
        return denominator == 0 ? 0 : numerator / denominator;
    }

    public static string MiB(long bytes) => $"{bytes / 1024d / 1024d:F2} MiB";
}
