using System.Globalization;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for the single date/time formatter (Story 10.4). Pins each token's exact output and the
/// determinism guarantee: output must be byte-identical under a non-Gregorian host culture (th-TH), the same guard
/// <see cref="GitMetrics"/> documents — a culture-sensitive format would emit Buddhist-era years and corrupt every
/// date/href. The machine token (<see cref="PortalDates.IsoDay"/>) must stay ISO regardless.</summary>
public class PortalDatesTests
{
    private static readonly DateOnly Day = new(2026, 7, 9);
    private static readonly DateTime Stamp = new(2026, 7, 9, 17, 4, 0);

    [Fact]
    public void Day_UsesTheOneMonthNameToken()
    {
        Assert.Equal("Jul 9, 2026", PortalDates.Day(Day));
        Assert.Equal("Jul 9, 2026", PortalDates.Day(Stamp));
    }

    [Fact]
    public void DayWithWeekday_PrefixesWeekdayBuiltFromTheSameToken()
    {
        Assert.Equal("Thu, Jul 9, 2026", PortalDates.DayWithWeekday(Day));
    }

    [Fact]
    public void IsoDay_StaysMachineIso()
    {
        Assert.Equal("2026-07-09", PortalDates.IsoDay(Day));
    }

    [Fact]
    public void MonthShort_IsTheHeatmapGutterToken()
    {
        Assert.Equal("Jul", PortalDates.MonthShort(Day));
    }

    [Fact]
    public void TimeOfDay_Is24Hour()
    {
        Assert.Equal("17:04", PortalDates.TimeOfDay(Stamp));
    }

    [Fact]
    public void Timestamp_JoinsDateAndTime_AppendsZoneOnlyWhenGiven()
    {
        Assert.Equal("Jul 9, 2026 at 17:04", PortalDates.Timestamp(Stamp));
        Assert.Equal("Jul 9, 2026 at 17:04 UTC-05:00", PortalDates.Timestamp(Stamp, "UTC-05:00"));
        Assert.Equal("Jul 9, 2026 at 17:04", PortalDates.Timestamp(Stamp, ""));
    }

    [Fact]
    public void ReformatAuthored_NormalizesParseableDates_LeavesFreeTextVerbatim()
    {
        Assert.Equal("Jul 5, 2026", PortalDates.ReformatAuthored("2026-07-05"));
        Assert.Equal("Jul 5, 2026", PortalDates.ReformatAuthored("July 5, 2026"));
        Assert.Equal("Draft", PortalDates.ReformatAuthored("Draft"));       // free text unchanged (NFR8)
        Assert.Equal("TBD", PortalDates.ReformatAuthored("TBD"));
    }

    [Fact]
    public void LocalZoneLabel_IsACompactUtcOffset()
    {
        var label = PortalDates.LocalZoneLabel(DateTime.Now);
        Assert.Matches(@"^UTC[+-]\d{2}:\d{2}$", label);
    }

    [Theory]
    [InlineData("th-TH")]
    [InlineData("fa-IR")]
    public void AllTokens_AreCultureInvariant(string culture)
    {
        var prior = CultureInfo.CurrentCulture;
        var priorUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            CultureInfo.CurrentUICulture = new CultureInfo(culture);

            // Non-Gregorian default calendars must NOT leak into the rendered strings (no Buddhist-era 2569, etc.).
            Assert.Equal("Jul 9, 2026", PortalDates.Day(Day));
            Assert.Equal("Thu, Jul 9, 2026", PortalDates.DayWithWeekday(Day));
            Assert.Equal("2026-07-09", PortalDates.IsoDay(Day));
            Assert.Equal("17:04", PortalDates.TimeOfDay(Stamp));
            Assert.Equal("Jul 5, 2026", PortalDates.ReformatAuthored("2026-07-05"));
        }
        finally
        {
            CultureInfo.CurrentCulture = prior;
            CultureInfo.CurrentUICulture = priorUi;
        }
    }
}
