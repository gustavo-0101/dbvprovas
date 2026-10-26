namespace Dbvprovas.Api.Modules.Identity;

// Idade na data (RN-ID-002): calculada, nunca guardada. O aniversário de 29/02 conta em 01/03 nos anos não bissextos.
public static class AgeCalculator
{
    public static int AgeOn(DateOnly birthDate, DateOnly today)
    {
        var age = today.Year - birthDate.Year;
        return today < Birthday(birthDate, today.Year) ? age - 1 : age;
    }

    private static DateOnly Birthday(DateOnly birthDate, int year) =>
        birthDate is { Month: 2, Day: 29 } && !DateTime.IsLeapYear(year)
            ? new DateOnly(year, 3, 1)
            : new DateOnly(year, birthDate.Month, birthDate.Day);
}

// O "hoje" do cálculo de idade é a data em Brasília (PC-12).
public static class BrasiliaCalendar
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Zone).DateTime);
}
