using MiniFootballBooking.Data;

namespace MiniFootballBooking.Services;

public interface IPricingService
{
    Task<PriceBreakdown> CalculateAsync(int fieldId, DateOnly date, TimeOnly start, TimeOnly end);
    PriceBreakdown Calculate(FootballField field, IReadOnlyList<PriceRule> rules, DateOnly date, TimeOnly start, TimeOnly end);
}

public class PricingService : IPricingService
{
    private readonly ApplicationDbContext _db;

    public PricingService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PriceBreakdown> CalculateAsync(int fieldId, DateOnly date, TimeOnly start, TimeOnly end)
    {
        var field = await _db.FootballFields.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fieldId)
                    ?? throw new InvalidOperationException("Không tìm thấy sân.");
        var rules = await _db.PriceRules.AsNoTracking()
            .Where(r => r.FieldId == fieldId && r.IsActive)
            .ToListAsync();
        return Calculate(field, rules, date, start, end);
    }

    public PriceBreakdown Calculate(FootballField field, IReadOnlyList<PriceRule> rules, DateOnly date, TimeOnly start, TimeOnly end)
    {
        var result = new PriceBreakdown();
        if (end <= start)
        {
            return result;
        }

        var cursor = start;
        while (cursor < end)
        {
            var next = cursor.AddHours(1);
            if (next > end)
            {
                next = end;
            }

            var portion = (decimal)(next - cursor).TotalMinutes / 60m;
            var (applied, peak, weekendExtra, peakExtra, basePart) = ResolveHour(rules, field, date, cursor, cursor.AddHours(1));
            var scale = portion;
            result.Lines.Add(new PriceHourLine
            {
                Start = cursor.ToString("HH:mm"),
                End = next.ToString("HH:mm"),
                Price = Money.RoundMoney(applied * scale),
                IsPeak = peak,
                IsWeekend = AppClock.IsWeekend(date)
            });
            result.BasePrice += Money.RoundMoney(basePart * scale);
            result.PeakHourCharge += Money.RoundMoney(peakExtra * scale);
            result.WeekendCharge += Money.RoundMoney(weekendExtra * scale);
            result.TotalHours += scale;
            cursor = next;
        }

        result.TotalPrice = result.BasePrice + result.PeakHourCharge + result.WeekendCharge;
        return result;
    }

    /// <summary>
    /// Giá một giờ = giá gốc + phụ thu cao điểm + phụ thu cuối tuần.
    /// Cuối tuần lấy chênh lệch so với bảng giá thứ Hai cùng khung giờ.
    /// </summary>
    private static (decimal Applied, bool Peak, decimal WeekendExtra, decimal PeakExtra, decimal BasePart) ResolveHour(
        IReadOnlyList<PriceRule> rules, FootballField field, DateOnly date, TimeOnly start, TimeOnly end)
    {
        var appliedRule = Match(rules, (int)date.DayOfWeek, date, start, end);
        var applied = appliedRule?.PricePerHour ?? field.BasePrice;
        var peak = appliedRule?.IsPeakHour ?? false;
        var weekend = AppClock.IsWeekend(date);
        var weekdayPrice = applied;
        if (weekend)
        {
            var mondayRule = Match(rules, (int)DayOfWeek.Monday, date, start, end);
            weekdayPrice = mondayRule?.PricePerHour ?? Money.RoundMoney(applied / 1.1m);
        }

        var weekendExtra = weekend ? Math.Max(0, applied - weekdayPrice) : 0;
        var core = weekend ? weekdayPrice : applied;
        var peakExtra = peak ? Math.Max(0, core - field.BasePrice) : 0;
        var basePart = core - peakExtra;
        return (applied, peak, weekendExtra, peakExtra, basePart);
    }

    private static PriceRule? Match(IReadOnlyList<PriceRule> rules, int dayOfWeek, DateOnly date, TimeOnly start, TimeOnly end)
    {
        return rules
            .Where(r => r.DayOfWeek == dayOfWeek && r.StartTime <= start && r.EndTime >= end)
            .Where(r => (r.EffectiveFrom == null || r.EffectiveFrom <= date) && (r.EffectiveTo == null || r.EffectiveTo >= date))
            .OrderByDescending(r => r.EffectiveFrom ?? DateOnly.MinValue)
            .FirstOrDefault();
    }
}
