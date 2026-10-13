using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace MiniFootballBooking.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<FieldType> FieldTypes => Set<FieldType>();
    public DbSet<FootballField> FootballFields => Set<FootballField>();
    public DbSet<FieldImage> FieldImages => Set<FieldImage>();
    public DbSet<PriceRule> PriceRules => Set<PriceRule>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingServiceLine> BookingServiceLines => Set<BookingServiceLine>();
    public DbSet<ServiceItem> ServiceItems => Set<ServiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<VoucherUsage> VoucherUsages => Set<VoucherUsage>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Maintenance> Maintenances => Set<Maintenance>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ContactMessage> Contacts => Set<ContactMessage>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        foreach (var property in builder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(18);
            property.SetScale(0);
        }

        builder.Entity<FootballField>(e =>
        {
            e.HasIndex(x => x.FieldCode).IsUnique();
            e.Property(x => x.RatingAverage).HasPrecision(4, 1);
            e.Property(x => x.Length).HasPrecision(6, 1);
            e.Property(x => x.Width).HasPrecision(6, 1);
            e.HasOne(x => x.FieldType).WithMany(x => x.Fields).HasForeignKey(x => x.FieldTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FieldImage>(e =>
        {
            e.HasOne(x => x.Field).WithMany(x => x.Images).HasForeignKey(x => x.FieldId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PriceRule>(e =>
        {
            e.HasOne(x => x.Field).WithMany(x => x.PriceRules).HasForeignKey(x => x.FieldId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Booking>(e =>
        {
            e.HasIndex(x => x.BookingCode).IsUnique();
            e.HasIndex(x => new { x.FieldId, x.BookingDate });
            e.HasOne(x => x.Customer).WithMany(x => x.Bookings).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Field).WithMany(x => x.Bookings).HasForeignKey(x => x.FieldId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Voucher).WithMany(x => x.Bookings).HasForeignKey(x => x.VoucherId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CancelledBy).WithMany().HasForeignKey(x => x.CancelledById).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BookingServiceLine>(e =>
        {
            e.HasOne(x => x.Booking).WithMany(x => x.ServiceLines).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Service).WithMany(x => x.BookingLines).HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Payment>(e =>
        {
            e.HasIndex(x => x.PaymentCode).IsUnique();
            e.HasOne(x => x.Booking).WithMany(x => x.Payments).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Voucher>(e =>
        {
            e.HasIndex(x => x.VoucherCode).IsUnique();
        });

        builder.Entity<VoucherUsage>(e =>
        {
            e.HasIndex(x => new { x.VoucherId, x.UserId, x.BookingId }).IsUnique();
            e.HasOne(x => x.Voucher).WithMany(x => x.Usages).HasForeignKey(x => x.VoucherId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Review>(e =>
        {
            e.HasIndex(x => x.BookingId).IsUnique();
            e.HasOne(x => x.Customer).WithMany(x => x.Reviews).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Field).WithMany(x => x.Reviews).HasForeignKey(x => x.FieldId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Booking).WithMany(x => x.Reviews).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Maintenance>(e =>
        {
            e.HasOne(x => x.Field).WithMany(x => x.Maintenances).HasForeignKey(x => x.FieldId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Creator).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Notification>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.IsRead });
            e.HasOne(x => x.User).WithMany(x => x.Notifications).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuditLog>(e =>
        {
            e.Property(x => x.OldValues).HasColumnType("nvarchar(max)");
            e.Property(x => x.NewValues).HasColumnType("nvarchar(max)");
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Post>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Content).HasColumnType("nvarchar(max)");
            e.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SystemSetting>(e =>
        {
            e.HasIndex(x => x.Key).IsUnique();
        });
    }
}
