using Dbvprovas.Api.Modules.Identity;
using Microsoft.Extensions.Time.Testing;

namespace Dbvprovas.Api.Tests.Shared;

public sealed class AgeCalculatorTests
{
    // RN-ID-002: o aniversário de 29/02 conta em 01/03 nos anos não bissextos.
    [Fact]
    public void Age_counts_leap_day_birthday_on_march_first()
    {
        var birth = new DateOnly(2008, 2, 29);

        Assert.Equal(17, AgeCalculator.AgeOn(birth, new DateOnly(2026, 2, 28)));
        Assert.Equal(18, AgeCalculator.AgeOn(birth, new DateOnly(2026, 3, 1)));
        Assert.Equal(15, AgeCalculator.AgeOn(birth, new DateOnly(2024, 2, 28)));
        Assert.Equal(16, AgeCalculator.AgeOn(birth, new DateOnly(2024, 2, 29)));
    }

    // RN-ID-002, PC-12: 18 anos completos no dia do aniversário, com o "hoje" de Brasília.
    [Fact]
    public void Age_turns_on_birthday_in_brasilia_time()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 5, 10, 2, 30, 0, TimeSpan.Zero)); // 23:30 de 09/05 em Brasília
        var birth = new DateOnly(2008, 5, 10);

        Assert.Equal(new DateOnly(2026, 5, 9), BrasiliaCalendar.Today(clock));
        Assert.Equal(17, AgeCalculator.AgeOn(birth, BrasiliaCalendar.Today(clock)));
        clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(18, AgeCalculator.AgeOn(birth, BrasiliaCalendar.Today(clock)));
    }
}
