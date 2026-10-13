using MiniFootballBooking.Data;

namespace MiniFootballBooking.Services;

public interface IVoucherService
{
    Task<VoucherResult> EvaluateAsync(string? code, decimal orderAmount, string? userId);
}

public class VoucherService : IVoucherService
{
    private readonly ApplicationDbContext _db;

    public VoucherService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<VoucherResult> EvaluateAsync(string? code, decimal orderAmount, string? userId)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new VoucherResult { Ok = true, Discount = 0 };
        }

        var normalized = code.Trim().ToUpperInvariant();
        var voucher = await _db.Vouchers.FirstOrDefaultAsync(v => v.VoucherCode == normalized);
        if (voucher == null || !voucher.IsActive)
        {
            return new VoucherResult { Ok = false, Message = "Voucher không tồn tại hoặc đã ngừng áp dụng." };
        }

        var now = AppClock.Now;
        if (now < voucher.StartDate || now > voucher.EndDate)
        {
            return new VoucherResult { Ok = false, Message = "Voucher đã hết hạn hoặc chưa đến ngày áp dụng." };
        }

        if (voucher.UsedCount >= voucher.UsageLimit)
        {
            return new VoucherResult { Ok = false, Message = "Voucher đã hết lượt sử dụng." };
        }

        if (orderAmount < voucher.MinOrderAmount)
        {
            return new VoucherResult { Ok = false, Message = $"Đơn tối thiểu để dùng voucher là {voucher.MinOrderAmount.VndText()}." };
        }

        if (!string.IsNullOrEmpty(userId))
        {
            var usedByUser = await _db.VoucherUsages.CountAsync(u => u.VoucherId == voucher.Id && u.UserId == userId);
            if (usedByUser >= voucher.MaxUsagePerUser)
            {
                return new VoucherResult { Ok = false, Message = "Bạn đã sử dụng hết lượt của voucher này." };
            }
        }

        var discount = voucher.DiscountType == DiscountType.Percentage
            ? Math.Round(orderAmount * voucher.DiscountValue / 100m, 0, MidpointRounding.AwayFromZero)
            : voucher.DiscountValue;

        if (voucher.MaxDiscountAmount is > 0)
        {
            discount = Math.Min(discount, voucher.MaxDiscountAmount.Value);
        }

        discount = Math.Min(discount, orderAmount);
        return new VoucherResult
        {
            Ok = true,
            VoucherId = voucher.Id,
            Discount = discount,
            Code = voucher.VoucherCode,
            Message = $"Áp dụng {voucher.VoucherCode}: giảm {discount.VndText()}."
        };
    }
}
