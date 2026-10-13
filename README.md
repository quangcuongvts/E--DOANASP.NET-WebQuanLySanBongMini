# Mini Football Booking

Hệ thống đặt sân bóng mini trực tuyến: quản lý sân, lịch, giá theo khung giờ, đặt sân, thanh toán, check-in/check-out, voucher, đánh giá và báo cáo.

## Công nghệ

- ASP.NET Core MVC 8
- Entity Framework Core 8 + SQL Server
- ASP.NET Core Identity
- Razor, Bootstrap 5, Chart.js, SweetAlert2

## Chạy project

Cài [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0) và SQL Server. Trong thư mục này:

```bash
dotnet restore
dotnet ef database update
dotnet run
```

Mở `http://localhost:5196`.

Chuỗi kết nối trong `appsettings.json` mặc định dùng `Server=localhost` và Windows Authentication. LocalDB:

```text
Server=(localdb)\MSSQLLocalDB;Database=MiniFootballBookingDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;
```

`dotnet run` cũng tự migration và seed nếu database chưa có dữ liệu.

## Tài khoản demo

| Vai trò | Email | Mật khẩu |
|---|---|---|
| Admin | admin@football.com | Admin@123 |
| Manager | manager@football.com | Manager@123 |
| Staff | staff@football.com | Staff@123 |
| Staff | staff2@football.com | Staff@123 |
| Staff | staff3@football.com | Staff@123 |
| Customer | customer@football.com | Customer@123 |

`customer02@football.com` … `customer18@football.com` dùng mật khẩu `Customer@123`.

Voucher: `NEWUSER50`, `FOOTBALL10`, `WEEKEND100`, `VIP20`. Mỗi khách dùng một mã một lần.

Hủy sân: trước 24 giờ hoàn 100%, từ 6–24 giờ hoàn 50%, dưới 6 giờ không hoàn. Quên mật khẩu hiện liên kết đặt lại ngay trên trang vì đồ án không cấu hình SMTP.
