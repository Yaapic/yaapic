namespace Yaapic.ViewModels.Responses;

/// <summary>One phase of the response timings, ready for a waterfall bar.</summary>
/// <param name="Label">The phase name, such as <c>DNS lookup</c>.</param>
/// <param name="DurationText">The duration, such as <c>12 ms</c>, or the "not available" text when <paramref name="IsMeasured"/> is false.</param>
/// <param name="Fraction">The share of the total duration for the bar width, in 0..1.</param>
/// <param name="OffsetFraction">The sum of the previous rows' fractions, where the bar starts, in 0..1.</param>
/// <param name="IsMeasured">Whether the phase was measured.</param>
public sealed record TimingRow(string Label, string DurationText, double Fraction, double OffsetFraction, bool IsMeasured);
