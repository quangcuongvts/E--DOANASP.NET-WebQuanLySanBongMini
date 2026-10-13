using Microsoft.AspNetCore.Identity;
using MiniFootballBooking.Services;

namespace MiniFootballBooking.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        string[] roles = ["Admin", "Manager", "Staff", "Customer"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var today = AppClock.Today;
        var admin = await EnsureUser(userManager, "admin@football.com", "Admin@123", "Quản trị hệ thống", "0901000001", "Admin", today.AddYears(-1), Gender.Male);
        var manager = await EnsureUser(userManager, "manager@football.com", "Manager@123", "Phạm Minh Đức", "0901000002", "Manager", today.AddMonths(-10), Gender.Male);
        var staff = new List<ApplicationUser>
        {
            await EnsureUser(userManager, "staff@football.com", "Staff@123", "Nguyễn Văn Khôi", "0901000003", "Staff", today.AddMonths(-8), Gender.Male),
            await EnsureUser(userManager, "staff2@football.com", "Staff@123", "Trần Thị Mai", "0901000004", "Staff", today.AddMonths(-6), Gender.Female),
            await EnsureUser(userManager, "staff3@football.com", "Staff@123", "Lê Hoàng Phúc", "0901000005", "Staff", today.AddMonths(-4), Gender.Male)
        };

        var customerSeeds = new (string Email, string Name, Gender Gender)[]
        {
            ("customer@football.com", "Nguyễn Hoàng Long", Gender.Male),
            ("customer02@football.com", "Trần Mỹ Duyên", Gender.Female),
            ("customer03@football.com", "Lê Quốc Bảo", Gender.Male),
            ("customer04@football.com", "Phạm Gia Hân", Gender.Female),
            ("customer05@football.com", "Võ Minh Khang", Gender.Male),
            ("customer06@football.com", "Đặng Thu Hà", Gender.Female),
            ("customer07@football.com", "Bùi Đức Thịnh", Gender.Male),
            ("customer08@football.com", "Ngô Thanh Tâm", Gender.Female),
            ("customer09@football.com", "Đỗ Hải Yến", Gender.Female),
            ("customer10@football.com", "Huỳnh Nhật Nam", Gender.Male),
            ("customer11@football.com", "Phan Anh Tuấn", Gender.Male),
            ("customer12@football.com", "Lý Phương Anh", Gender.Female),
            ("customer13@football.com", "Mai Tiến Đạt", Gender.Male),
            ("customer14@football.com", "Trịnh Ngọc Mai", Gender.Female),
            ("customer15@football.com", "Châu Văn Phúc", Gender.Male),
            ("customer16@football.com", "Đinh Gia Bảo", Gender.Male),
            ("customer17@football.com", "Tạ Quỳnh Chi", Gender.Female),
            ("customer18@football.com", "Lâm Hoàng Sơn", Gender.Male)
        };

        var customers = new List<ApplicationUser>();
        for (var i = 0; i < customerSeeds.Length; i++)
        {
            var created = i < 5 ? today.AddDays(-i * 3).ToDateTime(new TimeOnly(9, 0)) : today.AddMonths(-(i % 8 + 1)).ToDateTime(new TimeOnly(9, 0));
            customers.Add(await EnsureUser(userManager, customerSeeds[i].Email, "Customer@123", customerSeeds[i].Name, $"0902{i + 1:000000}", "Customer", DateOnly.FromDateTime(created), customerSeeds[i].Gender, created));
        }

        if (!await context.FieldTypes.AnyAsync())
        {
            context.FieldTypes.AddRange(
                new FieldType { Name = "Sân 5 người", Description = "Phù hợp đá giao hữu 5v5, mặt sân nhỏ và tốc độ cao.", Capacity = 10 },
                new FieldType { Name = "Sân 7 người", Description = "Sân tiêu chuẩn 7v7, cân bằng kỹ thuật và thể lực.", Capacity = 14 },
                new FieldType { Name = "Sân 11 người", Description = "Sân lớn cho trận đấu đủ người hoặc giải phong trào.", Capacity = 22 });
            await context.SaveChangesAsync();
        }

        if (!await context.FootballFields.AnyAsync())
        {
            var types = await context.FieldTypes.OrderBy(t => t.Capacity).ToListAsync();
            var images = new[]
            {
                "https://images.unsplash.com/photo-1575361204480-aadea25e6e68?auto=format&fit=crop&w=1400&q=80",
                "https://images.unsplash.com/photo-1529900748604-07564a03e7a6?auto=format&fit=crop&w=1400&q=80",
                "https://images.unsplash.com/photo-1459865264687-595d652de67e?auto=format&fit=crop&w=1400&q=80",
                "https://images.unsplash.com/photo-1431324155629-1a6deb1dec8d?auto=format&fit=crop&w=1400&q=80",
                "https://images.unsplash.com/photo-1517466787929-bc90951d0974?auto=format&fit=crop&w=1400&q=80",
                "https://images.unsplash.com/photo-1489944440615-453fc2b6a9a9?auto=format&fit=crop&w=1400&q=80",
                "https://images.unsplash.com/photo-1551958219-acbc608c6377?auto=format&fit=crop&w=1400&q=80",
                "https://images.unsplash.com/photo-1574629810360-7efbbe195018?auto=format&fit=crop&w=1400&q=80",
                "https://images.unsplash.com/photo-1508098682722-e99c43a406b2?auto=format&fit=crop&w=1400&q=80",
                "https://images.unsplash.com/photo-1517927033932-b3d18e61fb3a?auto=format&fit=crop&w=1400&q=80"
            };

            var specs = new[]
            {
                new { Code = "F01", Name = "Mini Football 01", Type = 0, Price = 300000m, Area = "Quận 1", Address = "12 Nguyễn Huệ, Quận 1, TP.HCM", Surface = "Cỏ nhân tạo", Len = 30m, Wid = 18m, Cap = 10 },
                new { Code = "F02", Name = "Mini Football 02", Type = 0, Price = 320000m, Area = "Quận 1", Address = "45 Lê Lợi, Quận 1, TP.HCM", Surface = "Cỏ nhân tạo", Len = 30m, Wid = 18m, Cap = 10 },
                new { Code = "F03", Name = "Champions Arena", Type = 1, Price = 500000m, Area = "Quận 3", Address = "88 Võ Văn Tần, Quận 3, TP.HCM", Surface = "Cỏ nhân tạo", Len = 40m, Wid = 20m, Cap = 14 },
                new { Code = "F04", Name = "Premier Arena", Type = 1, Price = 550000m, Area = "Quận 3", Address = "20 Cách Mạng Tháng 8, Quận 3, TP.HCM", Surface = "Cỏ nhân tạo", Len = 40m, Wid = 22m, Cap = 14 },
                new { Code = "F05", Name = "Old Trafford Mini", Type = 1, Price = 600000m, Area = "Quận 7", Address = "15 Nguyễn Văn Linh, Quận 7, TP.HCM", Surface = "Cỏ nhân tạo cao cấp", Len = 42m, Wid = 22m, Cap = 14 },
                new { Code = "F06", Name = "Bernabeu Arena", Type = 2, Price = 1000000m, Area = "Quận 7", Address = "99 Nguyễn Thị Thập, Quận 7, TP.HCM", Surface = "Cỏ nhân tạo", Len = 50m, Wid = 30m, Cap = 22 },
                new { Code = "F07", Name = "Camp Nou Mini", Type = 0, Price = 350000m, Area = "Thủ Đức", Address = "120 Võ Văn Ngân, TP. Thủ Đức", Surface = "Cỏ nhân tạo", Len = 32m, Wid = 18m, Cap = 10 },
                new { Code = "F08", Name = "Anfield Garden", Type = 1, Price = 480000m, Area = "Bình Thạnh", Address = "60 Xô Viết Nghệ Tĩnh, Bình Thạnh", Surface = "Cỏ nhân tạo", Len = 40m, Wid = 20m, Cap = 14 },
                new { Code = "F09", Name = "Allianz Mini", Type = 0, Price = 280000m, Area = "Tân Bình", Address = "30 Hoàng Văn Thụ, Tân Bình", Surface = "Sàn cao su", Len = 28m, Wid = 16m, Cap = 10 },
                new { Code = "F10", Name = "San Siro Night", Type = 2, Price = 900000m, Area = "Quận 2", Address = "18 Trần Não, TP. Thủ Đức", Surface = "Cỏ nhân tạo", Len = 52m, Wid = 32m, Cap = 22 }
            };

            for (var i = 0; i < specs.Length; i++)
            {
                var spec = specs[i];
                var field = new FootballField
                {
                    FieldCode = spec.Code,
                    Name = spec.Name,
                    FieldTypeId = types[spec.Type].Id,
                    Description = $"{spec.Name} là sân {types[spec.Type].Name.ToLowerInvariant()} tại {spec.Area}. Mặt sân {spec.Surface.ToLowerInvariant()}, đèn LED, lưới mới và khu vực chờ cho đội. Phù hợp đá giao hữu, giải nội bộ và thuê theo giờ.",
                    Capacity = spec.Cap,
                    Length = spec.Len,
                    Width = spec.Wid,
                    SurfaceType = spec.Surface,
                    BasePrice = spec.Price,
                    Address = spec.Address,
                    Area = spec.Area,
                    ImageUrl = images[i],
                    Status = spec.Code == "F09" ? FieldStatus.Maintenance : FieldStatus.Available,
                    HasLighting = true,
                    HasChangingRoom = i % 5 != 0,
                    HasToilet = true,
                    HasParking = i % 4 != 0,
                    HasWifi = i % 2 == 0,
                    HasWater = true,
                    IsActive = true,
                    CreatedAt = today.AddMonths(-8).ToDateTime(new TimeOnly(8, 0))
                };
                field.Images.Add(new FieldImage { ImageUrl = images[i], IsPrimary = true, SortOrder = 0 });
                field.Images.Add(new FieldImage { ImageUrl = images[(i + 3) % images.Length] + "&sat=-20", IsPrimary = false, SortOrder = 1 });
                field.Images.Add(new FieldImage { ImageUrl = images[(i + 6) % images.Length] + "&sat=10", IsPrimary = false, SortOrder = 2 });
                context.FootballFields.Add(field);
            }

            await context.SaveChangesAsync();
        }

        if (!await context.PriceRules.AnyAsync())
        {
            var fields = await context.FootballFields.ToListAsync();
            var bands = new (TimeOnly Start, TimeOnly End, int Kind, bool Peak)[]
            {
                (new TimeOnly(6, 0), new TimeOnly(16, 0), 0, false),
                (new TimeOnly(16, 0), new TimeOnly(18, 0), 1, false),
                (new TimeOnly(18, 0), new TimeOnly(22, 0), 2, true),
                (new TimeOnly(22, 0), new TimeOnly(23, 0), 3, false)
            };
            foreach (var field in fields)
            {
                for (var day = 0; day <= 6; day++)
                {
                    var weekend = day is 0 or 6;
                    foreach (var band in bands)
                    {
                        var raw = band.Kind switch
                        {
                            0 => field.BasePrice,
                            1 => field.BasePrice * 4 / 3,
                            2 => field.BasePrice * 5 / 3,
                            _ => field.BasePrice * 7 / 6
                        };
                        if (weekend)
                        {
                            raw *= 1.1m;
                        }

                        context.PriceRules.Add(new PriceRule
                        {
                            FieldId = field.Id,
                            DayOfWeek = day,
                            StartTime = band.Start,
                            EndTime = band.End,
                            PricePerHour = Money.RoundMoney(raw),
                            IsPeakHour = band.Peak,
                            EffectiveFrom = today.AddYears(-1),
                            IsActive = true
                        });
                    }
                }
            }

            await context.SaveChangesAsync();
        }

        if (!await context.ServiceItems.AnyAsync())
        {
            context.ServiceItems.AddRange(
                new ServiceItem { Name = "Thuê bóng", Description = "Bóng số 4, bơm sẵn.", Price = 30000, CreatedAt = AppClock.Now },
                new ServiceItem { Name = "Thuê áo bib", Description = "Bộ áo tập phân biệt đội.", Price = 50000, CreatedAt = AppClock.Now },
                new ServiceItem { Name = "Nước khoáng", Description = "Nước suối 500ml.", Price = 10000, CreatedAt = AppClock.Now },
                new ServiceItem { Name = "Nước tăng lực", Description = "Nước tăng lực lon.", Price = 20000, CreatedAt = AppClock.Now },
                new ServiceItem { Name = "Khăn lạnh", Description = "Khăn lạnh dùng một lần.", Price = 10000, CreatedAt = AppClock.Now },
                new ServiceItem { Name = "Thuê giày", Description = "Giày sân cỏ nhân tạo.", Price = 50000, CreatedAt = AppClock.Now },
                new ServiceItem { Name = "Thuê trọng tài", Description = "Trọng tài điều khiển trận.", Price = 200000, CreatedAt = AppClock.Now },
                new ServiceItem { Name = "Quay video trận đấu", Description = "Ghi hình và gửi file sau trận.", Price = 300000, CreatedAt = AppClock.Now });
            await context.SaveChangesAsync();
        }

        if (!await context.Vouchers.AnyAsync())
        {
            var start = today.AddDays(-30).ToDateTime(TimeOnly.MinValue);
            var end = today.AddDays(180).ToDateTime(new TimeOnly(23, 59));
            context.Vouchers.AddRange(
                new Voucher { VoucherCode = "NEWUSER50", Name = "Khách mới giảm 50.000đ", Description = "Giảm 50.000đ cho đơn từ 300.000đ.", DiscountType = DiscountType.FixedAmount, DiscountValue = 50000, MinOrderAmount = 300000, StartDate = start, EndDate = end, UsageLimit = 200, MaxUsagePerUser = 1 },
                new Voucher { VoucherCode = "FOOTBALL10", Name = "Giảm 10% mùa bóng", Description = "Giảm 10%, tối đa 100.000đ.", DiscountType = DiscountType.Percentage, DiscountValue = 10, MinOrderAmount = 200000, MaxDiscountAmount = 100000, StartDate = start, EndDate = end, UsageLimit = 300, MaxUsagePerUser = 1 },
                new Voucher { VoucherCode = "WEEKEND100", Name = "Cuối tuần giảm 100.000đ", Description = "Giảm 100.000đ cho đơn từ 500.000đ.", DiscountType = DiscountType.FixedAmount, DiscountValue = 100000, MinOrderAmount = 500000, StartDate = start, EndDate = end, UsageLimit = 150, MaxUsagePerUser = 1 },
                new Voucher { VoucherCode = "VIP20", Name = "VIP giảm 20%", Description = "Giảm 20%, tối đa 200.000đ.", DiscountType = DiscountType.Percentage, DiscountValue = 20, MinOrderAmount = 800000, MaxDiscountAmount = 200000, StartDate = start, EndDate = end, UsageLimit = 80, MaxUsagePerUser = 1 });
            await context.SaveChangesAsync();
        }

        if (!await context.SystemSettings.AnyAsync())
        {
            context.SystemSettings.AddRange(
                new SystemSetting { Key = "SiteName", Value = "Mini Football Booking", Description = "Tên hệ thống" },
                new SystemSetting { Key = "Hotline", Value = "0901 234 567", Description = "Số điện thoại" },
                new SystemSetting { Key = "Email", Value = "contact@minifootball.vn", Description = "Email liên hệ" },
                new SystemSetting { Key = "Address", Value = "12 Nguyễn Huệ, Quận 1, TP. Hồ Chí Minh", Description = "Địa chỉ" },
                new SystemSetting { Key = "OpenHour", Value = "6", Description = "Giờ mở cửa" },
                new SystemSetting { Key = "CloseHour", Value = "23", Description = "Giờ đóng cửa" },
                new SystemSetting { Key = "CancelFullRefundHours", Value = "24", Description = "Số giờ trước trận được hoàn 100%" },
                new SystemSetting { Key = "CancelHalfRefundHours", Value = "6", Description = "Số giờ trước trận được hoàn 50%" },
                new SystemSetting { Key = "BankName", Value = "Vietcombank", Description = "Ngân hàng" },
                new SystemSetting { Key = "BankAccount", Value = "0123456789", Description = "Số tài khoản" },
                new SystemSetting { Key = "BankOwner", Value = "CONG TY MINI FOOTBALL", Description = "Chủ tài khoản" });
            await context.SaveChangesAsync();
        }

        if (!await context.Bookings.AnyAsync())
        {
            await SeedBookingsAsync(context, customers, staff, admin.Id, manager.Id);
        }

        if (!await context.Posts.AnyAsync())
        {
            context.Posts.AddRange(
                PostOf("Kỹ thuật chuyền bóng một chạm cho sân 5", "Ba bài tập chuyền một chạm giúp đội giữ nhịp khi mặt sân nhỏ và đối thủ pressing.", admin.Id, today.AddDays(-12)),
                PostOf("Cách chọn giờ đá để tiết kiệm chi phí", "Khung giờ trước 16:00 thường thấp hơn giờ cao điểm. Đặt sớm trong tuần để giữ sân đẹp.", admin.Id, today.AddDays(-8)),
                PostOf("Checklist trước giờ bóng lăn", "Đến sớm 15 phút, kiểm tra giày, chia bib và thống nhất luật biên để trận đấu bắt đầu đúng giờ.", admin.Id, today.AddDays(-3)),
                PostOf("Vì sao sân cỏ nhân tạo cần bảo trì định kỳ", "Cào cát, bổ sung hạt cao su và kiểm tra đèn giúp mặt sân êm và giảm chấn thương.", admin.Id, today.AddDays(-1)));
            await context.SaveChangesAsync();
        }

        if (!await context.Banners.AnyAsync())
        {
            context.Banners.AddRange(
                new Banner { Title = "Đặt sân trong 1 phút", Subtitle = "Chọn giờ trống, giữ chỗ và thanh toán online.", ImageUrl = "https://images.unsplash.com/photo-1574629810360-7efbbe195018?auto=format&fit=crop&w=1600&q=80", LinkUrl = "/Fields", SortOrder = 1 },
                new Banner { Title = "Giờ vàng cuối tuần", Subtitle = "Ưu đãi voucher WEEKEND100 cho khung giờ tối.", ImageUrl = "https://images.unsplash.com/photo-1529900748604-07564a03e7a6?auto=format&fit=crop&w=1600&q=80", LinkUrl = "/Promotions", SortOrder = 2 });
            await context.SaveChangesAsync();
        }

        if (!await context.Contacts.AnyAsync())
        {
            context.Contacts.AddRange(
                new ContactMessage { FullName = "Nguyễn Văn An", Email = "an.nguyen@gmail.com", Phone = "0912345678", Subject = "Hỏi giá sân 7 người", Message = "Cho mình xin bảng giá sân 7 người tối thứ Bảy.", Status = ContactStatus.Processed, CreatedAt = today.AddDays(-6).ToDateTime(new TimeOnly(10, 0)) },
                new ContactMessage { FullName = "Trần Thu Trang", Email = "trang.tran@gmail.com", Phone = "0988123456", Subject = "Đặt sân cho công ty", Message = "Bên mình cần sân 11 người cho sự kiện nội bộ khoảng 20 người.", Status = ContactStatus.New, CreatedAt = today.AddDays(-1).ToDateTime(new TimeOnly(15, 30)) },
                new ContactMessage { FullName = "Lê Minh Quân", Email = "quan.le@gmail.com", Phone = "0903344556", Subject = "Góp ý ánh sáng", Message = "Sân F02 tối qua hơi tối ở góc phải.", Status = ContactStatus.New, CreatedAt = today.ToDateTime(new TimeOnly(9, 15)) });
            await context.SaveChangesAsync();
        }

        if (!await context.AuditLogs.AnyAsync())
        {
            context.AuditLogs.AddRange(
                new AuditLog { UserId = admin.Id, Action = "CREATE FIELD", EntityName = "FootballField", EntityId = "1", NewValues = "{\"name\":\"Mini Football 01\"}", IpAddress = "127.0.0.1", CreatedAt = today.AddDays(-20).ToDateTime(new TimeOnly(8, 0)) },
                new AuditLog { UserId = manager.Id, Action = "UPDATE FIELD", EntityName = "FootballField", EntityId = "3", OldValues = "{\"price\":480000}", NewValues = "{\"price\":500000}", IpAddress = "127.0.0.1", CreatedAt = today.AddDays(-10).ToDateTime(new TimeOnly(11, 0)) },
                new AuditLog { UserId = admin.Id, Action = "CHANGE ROLE", EntityName = "User", EntityId = staff[0].Id, NewValues = "{\"role\":\"Staff\"}", IpAddress = "127.0.0.1", CreatedAt = today.AddDays(-8).ToDateTime(new TimeOnly(9, 0)) });
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedBookingsAsync(ApplicationDbContext context, List<ApplicationUser> customers, List<ApplicationUser> staff, string adminId, string managerId)
    {
        var today = AppClock.Today;
        var fields = await context.FootballFields.OrderBy(f => f.FieldCode).ToListAsync();
        var services = await context.ServiceItems.OrderBy(s => s.Id).ToListAsync();
        var vouchers = await context.Vouchers.OrderBy(v => v.Id).ToListAsync();
        var pricing = new PricingService(context);
        var random = new Random(20260922);
        var taken = new List<(int FieldId, DateOnly Date, TimeOnly Start, TimeOnly End)>();
        var codes = new Dictionary<string, int>();
        var voucherUse = new HashSet<string>();
        var maintenanceField = fields.First(f => f.FieldCode == "F09").Id;
        var index = 0;

        bool Free(int fieldId, DateOnly date, int startHour, int duration)
        {
            if (startHour < 6 || startHour + duration > 23)
            {
                return false;
            }

            if (fieldId == maintenanceField && date >= today && date <= today.AddDays(2))
            {
                return false;
            }

            var start = new TimeOnly(startHour, 0);
            var end = new TimeOnly(startHour + duration, 0);
            return !taken.Any(t => t.FieldId == fieldId && t.Date == date && t.Start < end && t.End > start);
        }

        string NextCode(string kind, DateOnly date)
        {
            var prefix = kind + date.ToString("yyyyMMdd");
            codes.TryGetValue(prefix, out var seq);
            seq++;
            codes[prefix] = seq;
            return prefix + seq.ToString("D4");
        }

        void Reserve(int fieldId, DateOnly date, int startHour, int duration, BookingStatus status, PaymentStatus payStatus, bool withServices, bool allowVoucher)
        {
            var start = new TimeOnly(startHour, 0);
            var end = new TimeOnly(startHour + duration, 0);
            taken.Add((fieldId, date, start, end));
            var field = fields.First(f => f.Id == fieldId);
            var rules = context.PriceRules.Where(r => r.FieldId == fieldId && r.IsActive).ToList();
            var quote = pricing!.Calculate(field, rules, date, start, end);
            var customer = customers[index % customers.Count];
            var staffUser = staff[index % staff.Count];
            decimal serviceAmount = 0;
            var lines = new List<BookingServiceLine>();
            if (withServices)
            {
                var picks = new[] { services[index % 3], services[2] };
                foreach (var service in picks.DistinctBy(s => s.Id))
                {
                    var qty = service.Price <= 20000 ? 2 : 1;
                    var lineTotal = service.Price * qty;
                    serviceAmount += lineTotal;
                    lines.Add(new BookingServiceLine { ServiceId = service.Id, Quantity = qty, UnitPrice = service.Price, TotalPrice = lineTotal });
                }
            }

            var subtotal = quote.TotalPrice + serviceAmount;
            Voucher? voucher = null;
            decimal discount = 0;
            if (allowVoucher)
            {
                voucher = vouchers.FirstOrDefault(v => subtotal >= v.MinOrderAmount && !voucherUse.Contains(customer.Id + ":" + v.Id) && v.UsedCount < v.UsageLimit);
                if (voucher != null)
                {
                    discount = voucher.DiscountType == DiscountType.Percentage
                        ? Math.Min(voucher.MaxDiscountAmount ?? decimal.MaxValue, Math.Round(subtotal * voucher.DiscountValue / 100m, 0))
                        : voucher.DiscountValue;
                    discount = Math.Min(discount, subtotal);
                    voucher.UsedCount++;
                    voucherUse.Add(customer.Id + ":" + voucher.Id);
                }
            }

            var created = date.AddDays(-Math.Min(3, Math.Max(0, date.DayNumber - today.DayNumber > 0 ? 1 : 2))).ToDateTime(new TimeOnly(10, index % 50));
            if (created > AppClock.Now)
            {
                created = AppClock.Now.AddMinutes(-30);
            }

            var total = Math.Max(0, subtotal - discount);
            var booking = new Booking
            {
                BookingCode = NextCode("BK", DateOnly.FromDateTime(created)),
                CustomerId = customer.Id,
                FieldId = fieldId,
                BookingDate = date,
                StartTime = start,
                EndTime = end,
                OriginalAmount = quote.TotalPrice,
                ServiceAmount = serviceAmount,
                DiscountAmount = discount,
                TotalAmount = total,
                VoucherId = voucher?.Id,
                Status = status,
                PaymentStatus = payStatus,
                Note = index % 4 == 0 ? "Đá giao hữu, cần bib." : null,
                CreatedAt = created,
                StaffId = status is BookingStatus.CheckedIn or BookingStatus.Playing or BookingStatus.Completed ? staffUser.Id : null,
                CheckInTime = status is BookingStatus.CheckedIn or BookingStatus.Playing or BookingStatus.Completed ? date.ToDateTime(start).AddMinutes(5) : null,
                CheckOutTime = status == BookingStatus.Completed ? date.ToDateTime(end) : null
            };

            if (status == BookingStatus.Cancelled)
            {
                booking.CancelledAt = date.ToDateTime(start).AddHours(-10);
                booking.CancellationReason = index % 2 == 0 ? "Đội bận đột xuất" : "Thay đổi lịch công tác";
                booking.CancelledById = customer.Id;
                booking.RefundPercent = payStatus == PaymentStatus.Refunded ? 100 : 0;
                booking.RefundAmount = payStatus == PaymentStatus.Refunded ? total : 0;
            }

            foreach (var line in lines)
            {
                booking.ServiceLines.Add(line);
            }

            var payment = new Payment
            {
                PaymentCode = NextCode("PM", DateOnly.FromDateTime(created)),
                UserId = customer.Id,
                Amount = total,
                PaymentMethod = (PaymentMethod)(index % 3),
                PaymentStatus = payStatus,
                PaymentDate = payStatus is PaymentStatus.Paid or PaymentStatus.Refunded ? date.ToDateTime(start).AddHours(-2) : null,
                TransactionCode = payStatus == PaymentStatus.Unpaid ? null : "TX" + (100000 + index),
                Note = payStatus == PaymentStatus.Refunded ? "Hoàn tiền do hủy sân" : null
            };
            booking.Payments.Add(payment);
            if (voucher != null)
            {
                context.VoucherUsages.Add(new VoucherUsage { Voucher = voucher, UserId = customer.Id, Booking = booking, UsedAt = created });
            }

            context.Bookings.Add(booking);
            index++;
        }

        var dates = new List<DateOnly>();
        for (var month = 1; month <= 11; month++)
        {
            var anchor = today.AddMonths(-month);
            foreach (var day in new[] { 5, 15, 25 })
            {
                var date = new DateOnly(anchor.Year, anchor.Month, Math.Min(day, DateTime.DaysInMonth(anchor.Year, anchor.Month)));
                if (date < today)
                {
                    dates.Add(date);
                }
            }
        }

        for (var i = 1; i <= 7; i++)
        {
            dates.Add(today.AddDays(-i));
            dates.Add(today.AddDays(-i));
        }

        for (var i = 8; i <= 18; i++)
        {
            dates.Add(today.AddDays(-i));
        }

        foreach (var date in dates)
        {
            var preferred = index % fields.Count;
            var placed = false;
            for (var attempt = 0; attempt < fields.Count && !placed; attempt++)
            {
                var field = fields[(preferred + attempt) % fields.Count];
                int[] starts = [6, 8, 10, 14, 16, 18, 19, 20];
                foreach (var start in starts.OrderBy(_ => random.Next()))
                {
                    var duration = start <= 20 && random.Next(3) == 0 ? 2 : 1;
                    if (!Free(field.Id, date, start, duration))
                    {
                        continue;
                    }

                    var mod = index % 8;
                    var status = mod == 0 ? BookingStatus.Cancelled : mod == 1 ? BookingStatus.NoShow : BookingStatus.Completed;
                    var pay = status switch
                    {
                        BookingStatus.Cancelled => index % 2 == 0 ? PaymentStatus.Refunded : PaymentStatus.Unpaid,
                        BookingStatus.NoShow => PaymentStatus.Paid,
                        _ => PaymentStatus.Paid
                    };
                    Reserve(field.Id, date, start, duration, status, pay, index % 2 == 0, index % 4 == 0);
                    placed = true;
                    break;
                }
            }
        }

        var hour = AppClock.Now.Hour;
        if (hour > 9 && Free(fields[0].Id, today, 6, 2))
        {
            Reserve(fields[0].Id, today, 6, 2, BookingStatus.Completed, PaymentStatus.Paid, true, false);
        }

        if (hour > 12 && Free(fields[1].Id, today, 9, 2))
        {
            Reserve(fields[1].Id, today, 9, 2, BookingStatus.Completed, PaymentStatus.Paid, false, false);
        }

        if (hour is >= 8 and <= 22)
        {
            var current = Math.Clamp(hour, 6, 21);
            if (Free(fields[2].Id, today, current, 1))
            {
                Reserve(fields[2].Id, today, current, 1, BookingStatus.Playing, PaymentStatus.Paid, true, false);
            }

            var earlier = Math.Max(6, current - 2);
            if (earlier != current && Free(fields[3].Id, today, earlier, 1))
            {
                Reserve(fields[3].Id, today, earlier, 1, BookingStatus.CheckedIn, PaymentStatus.Paid, false, false);
            }
        }

        var futureCursor = Math.Max(hour + 1, 6);
        var futureField = 4;
        while (futureCursor <= 21 && index < 80)
        {
            var field = fields[futureField % fields.Count];
            if (field.FieldCode == "F09")
            {
                futureField++;
                field = fields[futureField % fields.Count];
            }

            if (Free(field.Id, today, futureCursor, 1))
            {
                var pending = futureCursor % 2 == 0;
                Reserve(field.Id, today, futureCursor, 1, pending ? BookingStatus.Pending : BookingStatus.Confirmed, pending ? PaymentStatus.Unpaid : PaymentStatus.Paid, false, !pending);
            }

            futureCursor += 2;
            futureField++;
        }

        for (var day = 1; day <= 12; day++)
        {
            var date = today.AddDays(day);
            var field = fields[(day + 1) % fields.Count];
            if (field.FieldCode == "F09" && day <= 2)
            {
                field = fields[0];
            }

            var start = 18 - (day % 3);
            if (Free(field.Id, date, start, 1))
            {
                var pending = day % 3 == 0;
                Reserve(field.Id, date, start, 1, pending ? BookingStatus.Pending : BookingStatus.Confirmed, pending ? PaymentStatus.Unpaid : PaymentStatus.Paid, day % 2 == 0, day % 4 == 0);
            }
        }

        context.Maintenances.AddRange(
            new Maintenance { FieldId = maintenanceField, Title = "Thay lưới và bổ sung cát", Description = "Khu vực góc trái bị thủng lưới, tạm ngưng nhận khách.", StartDateTime = today.ToDateTime(new TimeOnly(0, 0)), EndDateTime = today.AddDays(3).ToDateTime(new TimeOnly(6, 0)), Cost = 1500000, Status = MaintenanceStatus.InProgress, CreatedBy = managerId, CreatedAt = today.AddDays(-1).ToDateTime(new TimeOnly(8, 0)) },
            new Maintenance { FieldId = fields.First(f => f.FieldCode == "F03").Id, Title = "Bảo dưỡng đèn LED", Description = "Thay 4 bóng đèn cao áp.", StartDateTime = today.AddDays(-20).ToDateTime(new TimeOnly(8, 0)), EndDateTime = today.AddDays(-20).ToDateTime(new TimeOnly(12, 0)), Cost = 800000, Status = MaintenanceStatus.Completed, CreatedBy = managerId, CreatedAt = today.AddDays(-21).ToDateTime(new TimeOnly(9, 0)) },
            new Maintenance { FieldId = fields.First(f => f.FieldCode == "F06").Id, Title = "Kẻ lại đường biên", Description = "Sơn lại vạch và vòng tròn giữa sân.", StartDateTime = today.AddDays(6).ToDateTime(new TimeOnly(7, 0)), EndDateTime = today.AddDays(6).ToDateTime(new TimeOnly(11, 0)), Cost = 500000, Status = MaintenanceStatus.Scheduled, CreatedBy = adminId, CreatedAt = AppClock.Now });

        await context.SaveChangesAsync();

        var comments = new[]
        {
            "Sân đẹp, cỏ mới, đèn rất sáng.",
            "Sân tốt, nhân viên thân thiện.",
            "Đặt sân nhanh, giá hợp lý.",
            "Mặt sân êm, không bị trơn sau khi tưới.",
            "Khu để xe rộng, vào sân dễ.",
            "Giờ cao điểm hơi đông nhưng vẫn đúng giờ.",
            "Phòng thay đồ sạch, có nước uống.",
            "Lưới chắc, bóng không bay ra ngoài.",
            "Nhân viên check-in nhanh, không phải chờ.",
            "Sân 7 người rộng, đá rất đã.",
            "Đèn hơi vàng nhưng vẫn nhìn rõ bóng.",
            "Giá cuối tuần hơi cao nhưng sân đẹp.",
            "Đặt online tiện, voucher áp dụng đúng.",
            "Cỏ đều, không có vũng nước.",
            "Sẽ quay lại đá cùng công ty.",
            "Trọng tài thuê thêm rất ổn.",
            "Video trận gửi đúng hẹn.",
            "Nhân viên hỗ trợ bib nhiệt tình.",
            "Sân gần trung tâm, dễ tìm.",
            "Mình đá tối, an ninh tốt.",
            "Giữ chỗ chính xác, không bị trùng giờ.",
            "Nước khoáng lạnh, giá ổn.",
            "Sân 5 người hợp đá giao hữu.",
            "Lần sau sẽ đặt khung giờ sớm hơn.",
            "Không gian thoáng, có ghế ngồi chờ.",
            "Cỏ mới thay tuần trước, chạy rất êm.",
            "Quy trình hủy rõ ràng, hoàn tiền đúng.",
            "Sân lớn đá 11 người rất đã.",
            "App dễ dùng, lịch trống nhìn là thấy.",
            "Tổng thể xứng đáng 5 sao."
        };
        var ratings = new[] { 5, 5, 4, 5, 4, 5, 3, 5, 4, 5, 4, 5, 5, 4, 5, 4, 5, 5, 4, 3, 5, 4, 5, 5, 4, 5, 4, 5, 5, 4 };
        var completed = await context.Bookings.Where(b => b.Status == BookingStatus.Completed).OrderByDescending(b => b.BookingDate).Take(30).ToListAsync();
        for (var i = 0; i < completed.Count && i < comments.Length; i++)
        {
            context.Reviews.Add(new Review
            {
                CustomerId = completed[i].CustomerId,
                FieldId = completed[i].FieldId,
                BookingId = completed[i].Id,
                Rating = ratings[i],
                Comment = comments[i],
                CreatedAt = completed[i].BookingDate.ToDateTime(completed[i].EndTime).AddHours(1),
                IsVisible = true
            });
        }

        await context.SaveChangesAsync();
        var groups = await context.Reviews.Where(r => r.IsVisible).GroupBy(r => r.FieldId)
            .Select(g => new { g.Key, Avg = g.Average(x => (double)x.Rating) }).ToListAsync();
        var allFields = await context.FootballFields.ToListAsync();
        foreach (var group in groups)
        {
            var field = allFields.First(f => f.Id == group.Key);
            field.RatingAverage = Math.Round((decimal)group.Avg, 1);
        }

        foreach (var booking in await context.Bookings.Include(b => b.Field).Where(b => b.BookingDate >= today && b.BookingDate <= today.AddDays(2) && b.Status != BookingStatus.Cancelled).OrderBy(b => b.BookingDate).ThenBy(b => b.StartTime).Take(8).ToListAsync())
        {
            context.Notifications.Add(new Notification
            {
                UserId = booking.CustomerId,
                Title = "Sắp đến giờ đá",
                Message = $"{booking.Field?.Name} lúc {booking.StartTime:HH:mm} ngày {booking.BookingDate:dd/MM/yyyy}.",
                Type = NotificationType.UpcomingMatch,
                Link = $"/Booking/Details/{booking.Id}",
                CreatedAt = AppClock.Now.AddHours(-2)
            });
        }

        context.Notifications.Add(new Notification
        {
            UserId = adminId,
            Title = "Có booking mới",
            Message = "Hệ thống vừa ghi nhận các đơn đặt sân trong ngày.",
            Type = NotificationType.BookingCreated,
            Link = "/Admin/Bookings",
            CreatedAt = AppClock.Now.AddMinutes(-40)
        });
        context.Notifications.Add(new Notification
        {
            UserId = managerId,
            Title = "Có đánh giá mới",
            Message = "Khách hàng vừa để lại đánh giá cho sân.",
            Type = NotificationType.NewReview,
            Link = "/Admin/Reviews",
            CreatedAt = AppClock.Now.AddHours(-5)
        });
        await context.SaveChangesAsync();
    }

    private static Post PostOf(string title, string summary, string authorId, DateOnly published)
    {
        return new Post
        {
            Title = title,
            Slug = TextHelper.Slugify(title),
            Summary = summary,
            Content = $"<p>{summary}</p><p>Mini Football Booking ghi lại kinh nghiệm vận hành sân và gợi ý cho đội bóng phong trào. Hãy đặt sân sớm vào cuối tuần để giữ khung giờ đẹp, đồng thời kiểm tra dịch vụ đi kèm như bib, trọng tài hoặc quay video trước giờ bóng lăn.</p><p>Nếu cần đổi lịch, hãy thao tác trước 24 giờ để được hoàn tiền theo chính sách của sân.</p>",
            ThumbnailUrl = "https://images.unsplash.com/photo-1574629810360-7efbbe195018?auto=format&fit=crop&w=1200&q=80",
            AuthorId = authorId,
            IsPublished = true,
            PublishedAt = published.ToDateTime(new TimeOnly(8, 0))
        };
    }

    private static async Task<ApplicationUser> EnsureUser(UserManager<ApplicationUser> users, string email, string password, string fullName, string phone, string role, DateOnly createdOn, Gender gender, DateTime? createdAt = null)
    {
        var user = await users.FindByEmailAsync(email);
        if (user != null)
        {
            return user;
        }

        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            PhoneNumber = phone,
            Gender = gender,
            DateOfBirth = new DateOnly(1995, 1, 1).AddDays(Math.Abs(email.GetHashCode()) % 3000),
            Address = "TP. Hồ Chí Minh",
            IsActive = true,
            LockoutEnabled = true,
            CreatedAt = createdAt ?? createdOn.ToDateTime(new TimeOnly(8, 0))
        };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        await users.AddToRoleAsync(user, role);
        return user;
    }
}
