namespace Yaapic.ViewModels.LoadTesting;

/// <summary>A point of a chart, both coordinates normalized to 0..1.</summary>
/// <param name="X">The horizontal position, 0 at the start of the test and 1 at its end.</param>
/// <param name="Y">The vertical position, 1 at the largest value of the series.</param>
public readonly record struct ChartPoint(double X, double Y);
