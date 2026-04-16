using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Scaffolding.Internal;

namespace DAL.Data;

public partial class AppDbContext : DbContext
{
    private const string SoftDeleteFlagColumn = "is_deleted";
    private const string SoftDeleteTimestampColumn = "deleted_at";
    private static readonly MethodInfo SetSoftDeleteFilterMethod = typeof(AppDbContext)
        .GetMethod(nameof(SetSoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Static)!;

    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AdminAction> AdminActions { get; set; }

    public virtual DbSet<Amenity> Amenities { get; set; }

    public virtual DbSet<Apartment> Apartments { get; set; }

    public virtual DbSet<ApartmentMedium> ApartmentMedia { get; set; }

    public virtual DbSet<ApartmentAvailability> ApartmentAvailabilities { get; set; }

    public virtual DbSet<ApartmentPriceCalendar> ApartmentPriceCalendars { get; set; }

    public virtual DbSet<Booking> Bookings { get; set; }

    public virtual DbSet<BookingOffer> BookingOffers { get; set; }

    public virtual DbSet<BookingOccupant> BookingOccupants { get; set; }

    public virtual DbSet<BookingCheckTime> BookingCheckTimes { get; set; }

    public virtual DbSet<HolidaysEvent> HolidaysEvents { get; set; }

    public virtual DbSet<InspectionPhoto> InspectionPhotos { get; set; }

    public virtual DbSet<Landlord> Landlords { get; set; }

    public virtual DbSet<LandlordPayout> LandlordPayouts { get; set; }

    public virtual DbSet<LandlordSubscription> LandlordSubscriptions { get; set; }

    public virtual DbSet<MomoTransaction> MomoTransactions { get; set; }

    public virtual DbSet<NearbyAttraction> NearbyAttractions { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Package> Packages { get; set; }

    public virtual DbSet<PackageItem> PackageItems { get; set; }

    public virtual DbSet<PackagePackage> PackagePackages { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PropertyInspection> PropertyInspections { get; set; }

    public virtual DbSet<Review> Reviews { get; set; }

    public virtual DbSet<Room> Rooms { get; set; }

    public virtual DbSet<SmartPricingHistory> SmartPricingHistories { get; set; }

    public virtual DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }

    public virtual DbSet<SupportTicket> SupportTickets { get; set; }

    public virtual DbSet<SupportTicketAssignment> SupportTicketAssignments { get; set; }

    public virtual DbSet<TemporaryResidenceReport> TemporaryResidenceReports { get; set; }

    public virtual DbSet<Tenant> Tenants { get; set; }

    public virtual DbSet<TenantWishlist> TenantWishlists { get; set; }

    public virtual DbSet<WishlistCollection> WishlistCollections { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserIdentityDocument> UserIdentityDocuments { get; set; }

    public virtual DbSet<ReportDefinition> ReportDefinitions { get; set; }

    public virtual DbSet<ReportQueryConfig> ReportQueryConfigs { get; set; }

    public virtual DbSet<ScheduledReport> ScheduledReports { get; set; }

    public virtual DbSet<GeneratedReport> GeneratedReports { get; set; }

    public virtual DbSet<LandlordWallet> LandlordWallets { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<AdminAction>(entity =>
        {
            entity.HasKey(e => e.ActionId).HasName("PRIMARY");

            entity.ToTable("admin_actions");

            entity.HasIndex(e => e.AdminId, "idx_admin");

            entity.HasIndex(e => new { e.TargetType, e.TargetId }, "idx_target");

            entity.Property(e => e.ActionId).HasColumnName("action_id");
            entity.Property(e => e.ActionType)
                .HasMaxLength(50)
                .HasColumnName("action_type");
            entity.Property(e => e.AdminId).HasColumnName("admin_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .HasColumnName("description");
            entity.Property(e => e.NewValue)
                .HasColumnType("text")
                .HasColumnName("new_value");
            entity.Property(e => e.PreviousValue)
                .HasColumnType("text")
                .HasColumnName("previous_value");
            entity.Property(e => e.TargetId).HasColumnName("target_id");
            entity.Property(e => e.TargetType)
                .HasColumnType("enum('user','apartment','booking','review','report')")
                .HasColumnName("target_type");

            entity.HasOne(d => d.Admin).WithMany(p => p.AdminActions)
                .HasForeignKey(d => d.AdminId)
                .HasConstraintName("admin_actions_ibfk_1");
        });

        modelBuilder.Entity<Amenity>(entity =>
        {
            entity.HasKey(e => e.AmenityId).HasName("PRIMARY");

            entity.ToTable("amenities");

            entity.HasIndex(e => e.NameEn, "uk_name_en").IsUnique();

            entity.HasIndex(e => e.NameVi, "uk_name_vi").IsUnique();

            entity.Property(e => e.AmenityId).HasColumnName("amenity_id");
            entity.Property(e => e.NameEn)
                .HasMaxLength(100)
                .HasColumnName("name_en");
            entity.Property(e => e.NameVi)
                .HasMaxLength(100)
                .HasColumnName("name_vi");
        });

        modelBuilder.Entity<Apartment>(entity =>
        {
            entity.HasKey(e => e.ApartmentId).HasName("PRIMARY");

            entity.ToTable("apartments");

            entity.HasIndex(e => e.LandlordId, "idx_landlord");

            entity.HasIndex(e => e.Location, "idx_location")
                .HasAnnotation("MySql:SpatialIndex", true);

            entity.HasIndex(e => e.Status, "idx_status");

            entity.HasIndex(e => e.BookingStatus, "idx_booking_status");

            entity.Property(e => e.ApartmentId).HasColumnName("apartment_id");
            entity.Property(e => e.Address)
                .HasMaxLength(255)
                .HasColumnName("address");
            entity.Property(e => e.BasePricePerNight)
                .HasPrecision(12, 2)
                .HasColumnName("base_price_per_night");
            entity.Property(e => e.BookingStatus)
                .HasDefaultValueSql("'available'")
                .HasColumnType("enum('available','confirmed','locked')")
                .HasColumnName("booking_status");
            entity.Property(e => e.City)
                .HasMaxLength(100)
                .HasDefaultValueSql("'Hồ Chí Minh'")
                .HasColumnName("city");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.District)
                .HasMaxLength(100)
                .HasColumnName("district");
            entity.Property(e => e.IsPetAllowed)
                .HasDefaultValueSql("'0'")
                .HasColumnName("is_pet_allowed");
            entity.Property(e => e.LandlordId).HasColumnName("landlord_id");
            entity.Property(e => e.Latitude)
                .HasPrecision(10, 8)
                .HasColumnName("latitude");
            entity.Property(e => e.Location).HasColumnName("location");
            entity.Property(e => e.Longitude)
                .HasPrecision(11, 8)
                .HasColumnName("longitude");
            entity.Property(e => e.MaxOccupants)
                .HasDefaultValueSql("'1'")
                .HasColumnName("max_occupants");
            entity.Property(e => e.MaxPets).HasColumnName("max_pets");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'draft'")
                .HasColumnType("enum('draft','pending_review','posted','blocked','archived')")
                .HasColumnName("status");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");

            entity.HasOne(d => d.Landlord).WithMany(p => p.Apartments)
                .HasForeignKey(d => d.LandlordId)
                .HasConstraintName("apartments_ibfk_1");

            entity.HasMany(d => d.Amenities).WithMany(p => p.Apartments)
                .UsingEntity<Dictionary<string, object>>(
                    "ApartmentAmenity",
                    r => r.HasOne<Amenity>().WithMany()
                        .HasForeignKey("AmenityId")
                        .HasConstraintName("apartment_amenities_ibfk_2"),
                    l => l.HasOne<Apartment>().WithMany()
                        .HasForeignKey("ApartmentId")
                        .HasConstraintName("apartment_amenities_ibfk_1"),
                    j =>
                    {
                        j.HasKey("ApartmentId", "AmenityId")
                            .HasName("PRIMARY")
                            .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });
                        j.ToTable("apartment_amenities");
                        j.HasIndex(new[] { "AmenityId" }, "amenity_id");
                        j.IndexerProperty<Guid>("ApartmentId").HasColumnName("apartment_id");
                        j.IndexerProperty<Guid>("AmenityId").HasColumnName("amenity_id");
                    });
        });

        modelBuilder.Entity<ApartmentMedium>(entity =>
        {
            entity.HasKey(e => e.MediaId).HasName("PRIMARY");

            entity.ToTable("apartment_media");

            entity.HasIndex(e => e.ApartmentId, "idx_apartment");

            entity.Property(e => e.MediaId).HasColumnName("media_id");
            entity.Property(e => e.ApartmentId).HasColumnName("apartment_id");
            entity.Property(e => e.IsPrimary)
                .HasDefaultValueSql("'0'")
                .HasColumnName("is_primary");
            entity.Property(e => e.Type)
                .HasColumnType("enum('photo','video')")
                .HasColumnName("type");
            entity.Property(e => e.Url)
                .HasMaxLength(500)
                .HasColumnName("url");

            entity.HasOne(d => d.Apartment).WithMany(p => p.ApartmentMedia)
                .HasForeignKey(d => d.ApartmentId)
                .HasConstraintName("apartment_media_ibfk_1");
        });

        modelBuilder.Entity<ApartmentAvailability>(entity =>
        {
            entity.HasKey(e => e.AvailabilityId).HasName("PRIMARY");

            entity.ToTable("apartment_availability");

            entity.HasIndex(e => new { e.ApartmentId, e.StartDate, e.EndDate }, "idx_apartment_availability_dates");

            entity.Property(e => e.AvailabilityId).HasColumnName("availability_id");
            entity.Property(e => e.ApartmentId).HasColumnName("apartment_id");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.Reason)
                .HasMaxLength(200)
                .HasColumnName("reason");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Apartment).WithMany(p => p.ApartmentAvailabilities)
                .HasForeignKey(d => d.ApartmentId)
                .HasConstraintName("apartment_availability_ibfk_1");
        });

        modelBuilder.Entity<ApartmentPriceCalendar>(entity =>
        {
            entity.HasKey(e => e.PriceId).HasName("PRIMARY");

            entity.ToTable("apartment_price_calendar");

            entity.HasIndex(e => new { e.ApartmentId, e.StartDate, e.EndDate }, "idx_apartment_dates").IsUnique();

            entity.HasIndex(e => new { e.StartDate, e.EndDate }, "idx_dates");

            entity.Property(e => e.PriceId).HasColumnName("price_id");
            entity.Property(e => e.ApartmentId).HasColumnName("apartment_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");
            entity.Property(e => e.DiscountPercentage)
                .HasPrecision(5, 2)
                .HasDefaultValueSql("'0.00'")
                .HasColumnName("discount_percentage");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.IsDiscount)
                .HasDefaultValueSql("'0'")
                .HasColumnName("is_discount");
            entity.Property(e => e.MinNights)
                .HasDefaultValueSql("'1'")
                .HasColumnName("min_nights");
            entity.Property(e => e.PriceType)
                .HasDefaultValueSql("'base'")
                .HasColumnType("enum('base','weekend','holiday','peak_season','low_season','special_event','manual_override')")
                .HasColumnName("price_type");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Apartment).WithMany(p => p.ApartmentPriceCalendars)
                .HasForeignKey(d => d.ApartmentId)
                .HasConstraintName("apartment_price_calendar_ibfk_1");
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.BookingId).HasName("PRIMARY");

            entity.ToTable("bookings");

            entity.HasIndex(e => e.ApartmentId, "idx_apartment");

            entity.HasIndex(e => new { e.CheckInDate, e.CheckOutDate }, "idx_dates");

            entity.HasIndex(e => e.PackageId, "idx_package");

            entity.HasIndex(e => e.Status, "idx_status");

            entity.HasIndex(e => e.TenantId, "idx_tenant");

            entity.Property(e => e.BookingId).HasColumnName("booking_id");
            entity.Property(e => e.ApartmentId).HasColumnName("apartment_id");
            entity.Property(e => e.BalanceDueDate).HasColumnName("balance_due_date");
            entity.Property(e => e.CheckInDate).HasColumnName("check_in_date");
            entity.Property(e => e.CheckOutDate).HasColumnName("check_out_date");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");
            entity.Property(e => e.DepositAmount)
                .HasPrecision(12, 2)
                .HasColumnName("deposit_amount");
            entity.Property(e => e.UpfrontPaymentAmount)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("'0.00'")
                .HasColumnName("upfront_payment_amount");
            entity.Property(e => e.DepositPaid)
                .HasDefaultValueSql("'0'")
                .HasColumnName("deposit_paid");
            entity.Property(e => e.PaymentMode)
                .HasDefaultValueSql("'partial'")
                .HasColumnType("enum('partial','full')")
                .HasColumnName("payment_mode");
            entity.Property(e => e.Nights).HasColumnName("nights");
            entity.Property(e => e.NoOfAdults).HasColumnName("noOfAdults");
            entity.Property(e => e.NoOfInfants).HasColumnName("noOfInfants");
            entity.Property(e => e.NoOfPets).HasColumnName("noOfPets");
            entity.Property(e => e.PackageId).HasColumnName("package_id");
            entity.Property(e => e.PackagePrice)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("'0.00'")
                .HasColumnName("package_price");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'pending'")
                .HasColumnType("enum('pending','negotiating','confirmed','paid','completed','cancelled','disputed')")
                .HasColumnName("status");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.TotalPrice)
                .HasPrecision(12, 2)
                .HasColumnName("total_price");

            entity.HasOne(d => d.Apartment).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.ApartmentId)
                .HasConstraintName("bookings_ibfk_2");

            entity.HasOne(d => d.Package).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.PackageId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("bookings_ibfk_3");

            entity.HasOne(d => d.Tenant).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("bookings_ibfk_1");
        });

        modelBuilder.Entity<BookingOccupant>(entity =>
        {
            entity.HasKey(e => e.OccupantId).HasName("PRIMARY");

            entity.ToTable("booking_occupants");

            entity.HasIndex(e => e.BookingId, "idx_booking");

            entity.HasIndex(e => new { e.BookingId, e.OccupantOrder }, "uk_booking_order").IsUnique();

            entity.HasIndex(e => new { e.BookingId, e.IsPrimary }, "idx_booking_primary");

            entity.Property(e => e.OccupantId).HasColumnName("occupant_id");
            entity.Property(e => e.BookingId).HasColumnName("booking_id");
            entity.Property(e => e.OccupantOrder).HasColumnName("occupant_order");
            entity.Property(e => e.IsPrimary)
                .HasDefaultValueSql("'0'")
                .HasColumnName("is_primary");
            entity.Property(e => e.FullName)
                .HasMaxLength(150)
                .HasColumnName("full_name");
            entity.Property(e => e.PassportId)
                .HasMaxLength(50)
                .HasColumnName("passport_id");
            entity.Property(e => e.DateOfBirth)
                .HasColumnType("date")
                .HasColumnName("date_of_birth");
            entity.Property(e => e.NationalIdCardNumber)
                .HasMaxLength(20)
                .HasColumnName("national_id_card_number");
            entity.Property(e => e.Nationality)
                .HasMaxLength(2)
                .IsFixedLength()
                .HasColumnName("nationality");
            entity.Property(e => e.Sex)
                .HasMaxLength(20)
                .HasColumnName("sex");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .HasColumnName("phone");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.ProofPhotoUrl)
                .HasMaxLength(1000)
                .HasColumnName("proof_photo_url");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");

            entity.HasOne(d => d.Booking).WithMany(p => p.BookingOccupants)
                .HasForeignKey(d => d.BookingId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("booking_occupants_ibfk_1");
        });

        modelBuilder.Entity<BookingOffer>(entity =>
        {
            entity.HasKey(e => e.OfferId).HasName("PRIMARY");

            entity.ToTable("booking_offers");

            entity.HasIndex(e => e.OriginalBookingId, "idx_original_booking");

            entity.HasIndex(e => e.AlternativeApartmentId, "idx_alternative_apartment");

            entity.HasIndex(e => e.TenantId, "idx_tenant");

            entity.HasIndex(e => e.Status, "idx_status");

            entity.HasIndex(e => e.ExpiresAt, "idx_expires_at");

            entity.Property(e => e.OfferId).HasColumnName("offer_id");
            entity.Property(e => e.AlternativeApartmentId).HasColumnName("alternative_apartment_id");
            entity.Property(e => e.AlternativePrice)
                .HasPrecision(12, 2)
                .HasColumnName("alternative_price");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedByStaffId).HasColumnName("created_by_staff_id");
            entity.Property(e => e.ExpiresAt)
                .HasColumnType("timestamp")
                .HasColumnName("expires_at");
            entity.Property(e => e.OriginalBookingId).HasColumnName("original_booking_id");
            entity.Property(e => e.OriginalPrice)
                .HasPrecision(12, 2)
                .HasColumnName("original_price");
            entity.Property(e => e.PriceDifference)
                .HasPrecision(12, 2)
                .HasColumnName("price_difference");
            entity.Property(e => e.Reason)
                .HasMaxLength(100)
                .HasColumnName("reason");
            entity.Property(e => e.RespondedAt)
                .HasColumnType("timestamp")
                .HasColumnName("responded_at");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'pending'")
                .HasColumnType("enum('pending','accepted','rejected','expired','cancelled')")
                .HasColumnName("status");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.TenantResponseNotes)
                .HasColumnType("text")
                .HasColumnName("tenant_response_notes");

            entity.HasOne(d => d.AlternativeApartment).WithMany(p => p.BookingOffers)
                .HasForeignKey(d => d.AlternativeApartmentId)
                .HasConstraintName("booking_offers_ibfk_2");

            entity.HasOne(d => d.CreatedByStaff).WithMany(p => p.BookingOfferCreatedByStaffNavigations)
                .HasForeignKey(d => d.CreatedByStaffId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("booking_offers_ibfk_4");

            entity.HasOne(d => d.OriginalBooking).WithMany(p => p.BookingOffers)
                .HasForeignKey(d => d.OriginalBookingId)
                .HasConstraintName("booking_offers_ibfk_1");

            entity.HasOne(d => d.Tenant).WithMany(p => p.BookingOffers)
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("booking_offers_ibfk_3");
        });

        modelBuilder.Entity<BookingCheckTime>(entity =>
        {
            entity.HasKey(e => e.CheckTimeId).HasName("PRIMARY");

            entity.ToTable("booking_check_times");

            entity.HasIndex(e => e.ActualCheckIn, "idx_actual_in");

            entity.HasIndex(e => e.BookingId, "idx_booking").IsUnique();

            entity.HasIndex(e => e.ScheduledCheckIn, "idx_scheduled_in");

            entity.Property(e => e.CheckTimeId).HasColumnName("check_time_id");
            entity.Property(e => e.ActualCheckIn)
                .HasColumnType("datetime")
                .HasColumnName("actual_check_in");
            entity.Property(e => e.ActualCheckOut)
                .HasColumnType("datetime")
                .HasColumnName("actual_check_out");
            entity.Property(e => e.BookingId).HasColumnName("booking_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");
            entity.Property(e => e.DisputeResolutionNotes)
                .HasColumnType("text")
                .HasColumnName("dispute_resolution_notes");
            entity.Property(e => e.DisputeResolutionStatus)
                .HasMaxLength(80)
                .HasColumnName("dispute_resolution_status");
            entity.Property(e => e.DisputeResolvedAt)
                .HasColumnType("datetime")
                .HasColumnName("dispute_resolved_at");
            entity.Property(e => e.DisputeResolvedBy).HasColumnName("dispute_resolved_by");
            entity.Property(e => e.EarlyCheckInFee)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("'0.00'")
                .HasColumnName("early_check_in_fee");
            entity.Property(e => e.FeeDueAt)
                .HasColumnType("datetime")
                .HasColumnName("fee_due_at");
            entity.Property(e => e.FeeSettlementNotes)
                .HasColumnType("text")
                .HasColumnName("fee_settlement_notes");
            entity.Property(e => e.FeeSettlementStatus)
                .HasMaxLength(30)
                .HasDefaultValue("none")
                .HasColumnName("fee_settlement_status");
            entity.Property(e => e.FeeSettledAt)
                .HasColumnType("datetime")
                .HasColumnName("fee_settled_at");
            entity.Property(e => e.IsEarlyCheckIn)
                .HasDefaultValueSql("'0'")
                .HasColumnName("is_early_check_in");
            entity.Property(e => e.IsLateCheckOut)
                .HasDefaultValueSql("'0'")
                .HasColumnName("is_late_check_out");
            entity.Property(e => e.LateCheckOutFee)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("'0.00'")
                .HasColumnName("late_check_out_fee");
            entity.Property(e => e.Notes)
                .HasColumnType("text")
                .HasColumnName("notes");
            entity.Property(e => e.RecordedAt)
                .HasColumnType("datetime")
                .HasColumnName("recorded_at");
            entity.Property(e => e.RecordedBy).HasColumnName("recorded_by");
            entity.Property(e => e.ReportReference)
                .HasMaxLength(100)
                .HasColumnName("report_reference");
            entity.Property(e => e.ReportedAt)
                .HasColumnType("datetime")
                .HasColumnName("reported_at");
            entity.Property(e => e.ScheduledCheckIn)
                .HasColumnType("datetime")
                .HasColumnName("scheduled_check_in");
            entity.Property(e => e.ScheduledCheckOut)
                .HasColumnType("datetime")
                .HasColumnName("scheduled_check_out");
            entity.Property(e => e.TenantDisputeNotes)
                .HasColumnType("text")
                .HasColumnName("tenant_dispute_notes");
            entity.Property(e => e.TenantDisputeReason)
                .HasMaxLength(300)
                .HasColumnName("tenant_dispute_reason");
            entity.Property(e => e.TenantRespondedAt)
                .HasColumnType("datetime")
                .HasColumnName("tenant_responded_at");
            entity.Property(e => e.TenantRespondedBy).HasColumnName("tenant_responded_by");
            entity.Property(e => e.TenantResponseStatus)
                .HasMaxLength(50)
                .HasColumnName("tenant_response_status");
            entity.Property(e => e.TempResidenceReported)
                .HasDefaultValueSql("'0'")
                .HasColumnName("temp_residence_reported");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Booking).WithOne(p => p.BookingCheckTime)
                .HasForeignKey<BookingCheckTime>(d => d.BookingId)
                .HasConstraintName("booking_check_times_ibfk_1");
        });

        modelBuilder.Entity<HolidaysEvent>(entity =>
        {
            entity.HasKey(e => e.EventId).HasName("PRIMARY");

            entity.ToTable("holidays_events");

            entity.HasIndex(e => new { e.StartDate, e.EndDate }, "idx_dates");

            entity.HasIndex(e => new { e.EventType, e.LocationScope }, "idx_type_scope");

            entity.HasIndex(e => new { e.EventName, e.StartDate, e.EndDate }, "uk_event_dates").IsUnique();

            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.EventName)
                .HasMaxLength(150)
                .HasColumnName("event_name");
            entity.Property(e => e.EventType)
                .HasColumnType("enum('national_holiday','local_festival','international_event','school_break','major_conference','sports_event','other')")
                .HasColumnName("event_type");
            entity.Property(e => e.IsRecurring)
                .HasDefaultValueSql("'0'")
                .HasColumnName("is_recurring");
            entity.Property(e => e.LocationScope)
                .HasMaxLength(100)
                .HasDefaultValueSql("'Vietnam'")
                .HasColumnName("location_scope");
            entity.Property(e => e.RecurrenceRule)
                .HasMaxLength(255)
                .HasColumnName("recurrence_rule");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<InspectionPhoto>(entity =>
        {
            entity.HasKey(e => e.PhotoId).HasName("PRIMARY");

            entity.ToTable("inspection_photos");

            entity.HasIndex(e => e.InspectionId, "idx_inspection");

            entity.Property(e => e.PhotoId).HasColumnName("photo_id");
            entity.Property(e => e.Description)
                .HasMaxLength(200)
                .HasColumnName("description");
            entity.Property(e => e.FileKey)
                .HasMaxLength(255)
                .HasColumnName("file_key");
            entity.Property(e => e.FileUrl)
                .HasMaxLength(500)
                .HasColumnName("file_url");
            entity.Property(e => e.InspectionId).HasColumnName("inspection_id");
            entity.Property(e => e.IsIssue)
                .HasDefaultValueSql("'0'")
                .HasColumnName("is_issue");
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("uploaded_at");

            entity.HasOne(d => d.Inspection).WithMany(p => p.InspectionPhotos)
                .HasForeignKey(d => d.InspectionId)
                .HasConstraintName("inspection_photos_ibfk_1");
        });

        modelBuilder.Entity<Landlord>(entity =>
        {
            entity.HasKey(e => e.LandlordId).HasName("PRIMARY");

            entity.ToTable("landlords");

            entity.HasIndex(e => e.CurrentPlanId, "current_plan_id");

            entity.HasIndex(e => e.SubscriptionStatus, "idx_subscription_status");

            entity.HasIndex(e => e.IdentityVerificationStatus, "idx_verification_status");

            entity.Property(e => e.LandlordId)
                .ValueGeneratedOnAdd()
                .HasColumnName("landlord_id");
            entity.Property(e => e.CurrentPlanId).HasColumnName("current_plan_id");
            entity.Property(e => e.IdentityVerificationStatus)
                .HasDefaultValueSql("'not_started'")
                .HasColumnType("enum('not_started','pending','verified','rejected')")
                .HasColumnName("identity_verification_status");
            entity.Property(e => e.LastVerifiedAt)
                .HasColumnType("timestamp")
                .HasColumnName("last_verified_at");
            entity.Property(e => e.MomoWalletPhone)
                .HasMaxLength(20)
                .HasColumnName("momo_wallet_phone");
            entity.Property(e => e.PayoutBankAccountNo)
                .HasMaxLength(40)
                .HasColumnName("payout_bank_account_no");
            entity.Property(e => e.PayoutBankCardNo)
                .HasMaxLength(40)
                .HasColumnName("payout_bank_card_no");
            entity.Property(e => e.PayoutBankCode)
                .HasMaxLength(20)
                .HasColumnName("payout_bank_code");
            entity.Property(e => e.PayoutPersonalId)
                .HasMaxLength(30)
                .HasColumnName("payout_personal_id");
            entity.Property(e => e.PayoutReceiverName)
                .HasMaxLength(150)
                .HasColumnName("payout_receiver_name");
            entity.Property(e => e.PreferredPayoutMethod)
                .HasMaxLength(20)
                .HasColumnName("preferred_payout_method");
            entity.Property(e => e.SubscriptionExpiresAt).HasColumnName("subscription_expires_at");
            entity.Property(e => e.SubscriptionStatus)
                .HasDefaultValueSql("'none'")
                .HasColumnType("enum('none','active','expired','pending')")
                .HasColumnName("subscription_status");
            entity.Property(e => e.VerifiedBusiness)
                .HasDefaultValueSql("'0'")
                .HasColumnName("verified_business");

            entity.HasOne(d => d.CurrentPlan).WithMany(p => p.Landlords)
                .HasForeignKey(d => d.CurrentPlanId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("landlords_ibfk_2");

            entity.HasOne(d => d.LandlordNavigation).WithOne(p => p.Landlord)
                .HasForeignKey<Landlord>(d => d.LandlordId)
                .HasConstraintName("landlords_ibfk_1");
        });

        modelBuilder.Entity<LandlordPayout>(entity =>
        {
            entity.HasKey(e => e.PayoutId).HasName("PRIMARY");

            entity.ToTable("landlord_payouts");

            entity.HasIndex(e => e.LandlordId, "idx_landlord");

            entity.HasIndex(e => e.MomoRequestId, "uk_momo_request_id").IsUnique();

            entity.HasIndex(e => new { e.LandlordId, e.CreatedAt }, "idx_landlord_created");

            entity.Property(e => e.PayoutId).HasColumnName("payout_id");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.Channel)
                .HasMaxLength(20)
                .HasColumnName("channel");
            entity.Property(e => e.CompletedAt)
                .HasColumnType("timestamp")
                .HasColumnName("completed_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");
            entity.Property(e => e.FailedAt)
                .HasColumnType("timestamp")
                .HasColumnName("failed_at");
            entity.Property(e => e.FeeAmount)
                .HasDefaultValueSql("'0'")
                .HasColumnName("fee_amount");
            entity.Property(e => e.LandlordId).HasColumnName("landlord_id");
            entity.Property(e => e.Message)
                .HasMaxLength(1024)
                .HasColumnName("message");
            entity.Property(e => e.MomoOrderId)
                .HasMaxLength(120)
                .HasColumnName("momo_order_id");
            entity.Property(e => e.MomoRequestId)
                .HasMaxLength(120)
                .HasColumnName("momo_request_id");
            entity.Property(e => e.MomoTransId)
                .HasMaxLength(120)
                .HasColumnName("momo_trans_id");
            entity.Property(e => e.NetAmount).HasColumnName("net_amount");
            entity.Property(e => e.RequestBody).HasColumnName("request_body");
            entity.Property(e => e.ResponseBody).HasColumnName("response_body");
            entity.Property(e => e.ResultCode).HasColumnName("result_code");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Landlord).WithMany(p => p.LandlordPayouts)
                .HasForeignKey(d => d.LandlordId)
                .HasConstraintName("landlord_payouts_ibfk_1");
        });

        modelBuilder.Entity<LandlordSubscription>(entity =>
        {
            entity.HasKey(e => e.SubscriptionId).HasName("PRIMARY");

            entity.ToTable("landlord_subscriptions");

            entity.HasIndex(e => e.EndDate, "idx_end_date");

            entity.HasIndex(e => new { e.LandlordId, e.Status }, "idx_landlord_status");

            entity.HasIndex(e => e.LastPaymentId, "last_payment_id");

            entity.HasIndex(e => e.PlanId, "plan_id");

            entity.Property(e => e.SubscriptionId).HasColumnName("subscription_id");
            entity.Property(e => e.AutoRenew)
                .HasDefaultValueSql("'1'")
                .HasColumnName("auto_renew");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.LandlordId).HasColumnName("landlord_id");
            entity.Property(e => e.LastPaymentId).HasColumnName("last_payment_id");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(50)
                .HasColumnName("payment_method");
            entity.Property(e => e.PlanId).HasColumnName("plan_id");
            entity.Property(e => e.RenewalType)
                .HasDefaultValueSql("'monthly'")
                .HasColumnType("enum('monthly','annual','none')")
                .HasColumnName("renewal_type");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'pending_payment'")
                .HasColumnType("enum('active','pending_payment','expired','cancelled','trial')")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Landlord).WithMany(p => p.LandlordSubscriptions)
                .HasForeignKey(d => d.LandlordId)
                .HasConstraintName("landlord_subscriptions_ibfk_1");

            entity.HasOne(d => d.LastPayment).WithMany(p => p.LandlordSubscriptions)
                .HasForeignKey(d => d.LastPaymentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("landlord_subscriptions_ibfk_3");

            entity.HasOne(d => d.Plan).WithMany(p => p.LandlordSubscriptions)
                .HasForeignKey(d => d.PlanId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("landlord_subscriptions_ibfk_2");
        });

        modelBuilder.Entity<MomoTransaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("momo_transactions");

            entity.HasIndex(e => e.PaymentId, "ix_momo_transactions_payment_id");

            entity.HasIndex(e => e.RequestId, "ix_momo_transactions_request_id");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.CreatedAt)
                .HasMaxLength(6)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                .HasColumnName("created_at");
            entity.Property(e => e.Message)
                .HasMaxLength(1024)
                .HasColumnName("message");
            entity.Property(e => e.PartnerCode)
                .HasMaxLength(50)
                .HasColumnName("partner_code");
            entity.Property(e => e.PaymentId).HasColumnName("payment_id");
            entity.Property(e => e.RequestBody).HasColumnName("request_body");
            entity.Property(e => e.RequestId)
                .HasMaxLength(100)
                .HasColumnName("request_id");
            entity.Property(e => e.ResponseBody).HasColumnName("response_body");
            entity.Property(e => e.ResultCode).HasColumnName("result_code");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .HasColumnName("type");
            entity.Property(e => e.UpdatedAt)
                .HasMaxLength(6)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Payment).WithMany(p => p.MomoTransactions)
                .HasForeignKey(d => d.PaymentId)
                .HasConstraintName("fk_momo_transactions_payment");
        });

        modelBuilder.Entity<NearbyAttraction>(entity =>
        {
            entity.HasKey(e => e.AttractionId).HasName("PRIMARY");

            entity.ToTable("nearby_attractions");

            entity.HasIndex(e => e.Location, "idx_location")
                .HasAnnotation("MySql:SpatialIndex", true);

            entity.Property(e => e.AttractionId).HasColumnName("attraction_id");
            entity.Property(e => e.Address)
                .HasMaxLength(500)
                .HasColumnName("address");
            entity.Property(e => e.City)
                .HasMaxLength(100)
                .HasColumnName("city");
            entity.Property(e => e.Location).HasColumnName("location");
            entity.Property(e => e.NameEn)
                .HasMaxLength(200)
                .HasColumnName("name_en");
            entity.Property(e => e.NameVi)
                .HasMaxLength(200)
                .HasColumnName("name_vi");
            entity.Property(e => e.Type)
                .HasColumnType("enum('restaurant','museum','park','landmark','shopping','transport')")
                .HasColumnName("type");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PRIMARY");

            entity.ToTable("notifications");

            entity.HasIndex(e => new { e.ReferenceType, e.ReferenceId }, "idx_reference");

            entity.HasIndex(e => e.Type, "idx_type");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "idx_user_created");

            entity.HasIndex(e => new { e.UserId, e.IsRead }, "idx_user_read");

            entity.Property(e => e.NotificationId).HasColumnName("notification_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");
            entity.Property(e => e.IsRead)
                .HasDefaultValueSql("'0'")
                .HasColumnName("is_read");
            entity.Property(e => e.Message)
                .HasColumnType("text")
                .HasColumnName("message");
            entity.Property(e => e.ReadAt)
                .HasColumnType("timestamp")
                .HasColumnName("read_at");
            entity.Property(e => e.ReferenceId).HasColumnName("reference_id");
            entity.Property(e => e.ReferenceType)
                .HasMaxLength(50)
                .HasColumnName("reference_type");
            entity.Property(e => e.Title)
                .HasMaxLength(150)
                .HasColumnName("title");
            entity.Property(e => e.Type)
                .HasColumnType("enum('booking_created','booking_confirmed','booking_cancelled','booking_upcoming','payment_success','payment_failed','identity_verified','identity_rejected','listing_approved','listing_rejected','inspection_scheduled','inspection_completed','support_ticket_created','support_ticket_update','support_ticket_resolved','review_reminder','new_message','system_announcement','other')")
                .HasColumnName("type");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("notifications_ibfk_1");
        });

        modelBuilder.Entity<Package>(entity =>
        {
            entity.HasKey(e => e.PackageId).HasName("PRIMARY");

            entity.ToTable("packages");

            entity.HasIndex(e => e.IsActive, "idx_active");

            entity.HasIndex(e => e.ApartmentId, "idx_apartment");

            entity.Property(e => e.PackageId).HasColumnName("package_id");
            entity.Property(e => e.ApartmentId).HasColumnName("apartment_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");
            entity.Property(e => e.Currency)
                .HasMaxLength(3)
                .HasDefaultValueSql("'VND'")
                .HasColumnName("currency");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
            entity.Property(e => e.MaxBookings).HasColumnName("max_bookings");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.Price)
                .HasPrecision(12, 2)
                .HasColumnName("price");

            entity.HasOne(d => d.Apartment).WithMany(p => p.Packages)
                .HasForeignKey(d => d.ApartmentId)
                .HasConstraintName("packages_ibfk_1");
        });

        modelBuilder.Entity<PackageItem>(entity =>
        {
            entity.HasKey(e => e.PackageItemId).HasName("PRIMARY");

            entity.ToTable("package_items");

            entity.Property(e => e.PackageItemId).HasColumnName("package_item_id");
            entity.Property(e => e.EstimatedValue)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("'0.00'")
                .HasColumnName("estimated_value");
            entity.Property(e => e.ItemDescription)
                .HasColumnType("text")
                .HasColumnName("item_description");
            entity.Property(e => e.ItemName)
                .HasMaxLength(150)
                .HasColumnName("item_name");
            entity.Property(e => e.Quantity)
                .HasPrecision(10, 2)
                .HasDefaultValueSql("'1.00'")
                .HasColumnName("quantity");
            entity.Property(e => e.SortOrder)
                .HasDefaultValueSql("'0'")
                .HasColumnName("sort_order");
        });

        modelBuilder.Entity<PackagePackage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("package_packages");

            entity.HasIndex(e => e.PackageId, "package_packages_ibfk_1");

            entity.HasIndex(e => e.PackageItemId, "package_packages_ibfk_2");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PackageId).HasColumnName("package_id");
            entity.Property(e => e.PackageItemId).HasColumnName("package_item_id");

            entity.HasOne(d => d.Package).WithMany(p => p.PackagePackages)
                .HasForeignKey(d => d.PackageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("package_packages_ibfk_1");

            entity.HasOne(d => d.PackageItem).WithMany(p => p.PackagePackages)
                .HasForeignKey(d => d.PackageItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("package_packages_ibfk_2");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("PRIMARY");

            entity.ToTable("payments");

            entity.HasIndex(e => new { e.RelatedEntityType, e.RelatedEntityId }, "idx_entity");

            entity.HasIndex(e => e.PaymentPurpose, "idx_purpose");

            entity.HasIndex(e => e.Status, "idx_status");

            entity.Property(e => e.PaymentId).HasColumnName("payment_id");
            entity.Property(e => e.Amount)
                .HasPrecision(12, 2)
                .HasColumnName("amount");
            entity.Property(e => e.Method)
                .HasMaxLength(50)
                .HasColumnName("method");
            entity.Property(e => e.PaidAt)
                .HasColumnType("timestamp")
                .HasColumnName("paid_at");
            entity.Property(e => e.PaymentPurpose)
                .HasDefaultValueSql("'booking_deposit'")
                .HasColumnType("enum('booking_deposit','booking_balance','booking_full_payment','booking_addon_or_package','subscription_monthly','subscription_annual','subscription_trial','subscription_renewal','refund_booking','refund_subscription','other')")
                .HasColumnName("payment_purpose");
            entity.Property(e => e.PaymentType)
                .HasColumnType("enum('deposit','balance','addon','refund','upfront')")
                .HasColumnName("payment_type");
            entity.Property(e => e.RelatedEntityId).HasColumnName("related_entity_id");
            entity.Property(e => e.RelatedEntityType)
                .HasColumnType("enum('booking','host_subscription','other')")
                .HasColumnName("related_entity_type");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'pending'")
                .HasColumnType("enum('pending','success','failed','refunded')")
                .HasColumnName("status");
            entity.Property(e => e.TransactionId)
                .HasMaxLength(100)
                .HasColumnName("transaction_id");
            entity.Property(e => e.LandlordId).HasColumnName("landlord_id");
            entity.Property(e => e.LandlordAmount)
                .HasPrecision(12, 2)
                .HasColumnName("landlord_amount");
            entity.Property(e => e.PlatformFee)
                .HasPrecision(12, 2)
                .HasColumnName("platform_fee");
            entity.Property(e => e.SettlementStatus)
                .HasMaxLength(20)
                .HasColumnName("settlement_status");
        });

        modelBuilder.Entity<PropertyInspection>(entity =>
        {
            entity.HasKey(e => e.InspectionId).HasName("PRIMARY");

            entity.ToTable("property_inspections");

            entity.HasIndex(e => e.ApprovedBy, "approved_by");

            entity.HasIndex(e => e.ApartmentId, "idx_apartment");

            entity.HasIndex(e => e.InspectorId, "idx_inspector");

            entity.HasIndex(e => e.Status, "idx_status");

            entity.Property(e => e.InspectionId).HasColumnName("inspection_id");
            entity.Property(e => e.ApartmentId).HasColumnName("apartment_id");
            entity.Property(e => e.ApprovedAt)
                .HasColumnType("timestamp")
                .HasColumnName("approved_at");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.ApprovedForListing)
                .HasDefaultValueSql("'0'")
                .HasColumnName("approved_for_listing");
            entity.Property(e => e.CompletedDate).HasColumnName("completed_date");
            entity.Property(e => e.InspectorId).HasColumnName("inspector_id");
            entity.Property(e => e.IssuesFound)
                .HasColumnType("text")
                .HasColumnName("issues_found");
            entity.Property(e => e.OverallCondition)
                .HasColumnType("text")
                .HasColumnName("overall_condition");
            entity.Property(e => e.Recommendations)
                .HasColumnType("text")
                .HasColumnName("recommendations");
            entity.Property(e => e.ScheduledDate).HasColumnName("scheduled_date");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'pending'")
                .HasColumnType("enum('pending','scheduled','in_progress','passed','failed','re_inspection_needed')")
                .HasColumnName("status");

            entity.HasOne(d => d.Apartment).WithMany(p => p.PropertyInspections)
                .HasForeignKey(d => d.ApartmentId)
                .HasConstraintName("property_inspections_ibfk_1");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.PropertyInspectionApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("property_inspections_ibfk_3");

            entity.HasOne(d => d.Inspector).WithMany(p => p.PropertyInspectionInspectors)
                .HasForeignKey(d => d.InspectorId)
                .HasConstraintName("property_inspections_ibfk_2");
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.ReviewId).HasName("PRIMARY");

            entity.ToTable("reviews");

            entity.HasIndex(e => e.ApartmentId, "apartment_id");

            entity.HasIndex(e => e.ReviewedId, "reviewed_id");

            entity.HasIndex(e => e.ReviewerId, "reviewer_id");

            entity.HasIndex(e => new { e.BookingId, e.ReviewerId }, "uk_booking_review").IsUnique();

            entity.Property(e => e.ReviewId).HasColumnName("review_id");
            entity.Property(e => e.ApartmentId).HasColumnName("apartment_id");
            entity.Property(e => e.BookingId).HasColumnName("booking_id");
            entity.Property(e => e.CommentEn)
                .HasColumnType("text")
                .HasColumnName("comment_en");
            entity.Property(e => e.CommentVi)
                .HasColumnType("text")
                .HasColumnName("comment_vi");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.ReviewedId).HasColumnName("reviewed_id");
            entity.Property(e => e.ReviewerId).HasColumnName("reviewer_id");

            entity.HasOne(d => d.Apartment).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.ApartmentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("reviews_ibfk_4");

            entity.HasOne(d => d.Booking).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.BookingId)
                .HasConstraintName("reviews_ibfk_1");

            entity.HasOne(d => d.Reviewed).WithMany(p => p.ReviewRevieweds)
                .HasForeignKey(d => d.ReviewedId)
                .HasConstraintName("reviews_ibfk_3");

            entity.HasOne(d => d.Reviewer).WithMany(p => p.ReviewReviewers)
                .HasForeignKey(d => d.ReviewerId)
                .HasConstraintName("reviews_ibfk_2");
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.RoomId).HasName("PRIMARY");

            entity.ToTable("rooms");

            entity.HasIndex(e => e.ApartmentId, "idx_apartment").IsUnique();

            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.ApartmentId).HasColumnName("apartment_id");
            entity.Property(e => e.BedType)
                .HasDefaultValueSql("'single'")
                .HasColumnType("enum('single','double','queen','king','bunk','shared')")
                .HasColumnName("bed_type");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.IsPrivateBathroom)
                .HasDefaultValueSql("'0'")
                .HasColumnName("is_private_bathroom");
            entity.Property(e => e.RoomType)
                .HasDefaultValueSql("'private_single'")
                .HasColumnType("enum('private_single','private_double','shared_bed','studio','other')")
                .HasColumnName("room_type");
            entity.Property(e => e.SizeSqm)
                .HasPrecision(5, 2)
                .HasColumnName("size_sqm");
            entity.Property(e => e.Title)
                .HasMaxLength(150)
                .HasColumnName("title");

            entity.HasOne(d => d.Apartment).WithOne(p => p.Room)
                .HasForeignKey<Room>(d => d.ApartmentId)
                .HasConstraintName("rooms_ibfk_1");
        });

        modelBuilder.Entity<SmartPricingHistory>(entity =>
        {
            entity.HasKey(e => e.PricingId).HasName("PRIMARY");

            entity.ToTable("smart_pricing_history");

            entity.HasIndex(e => new { e.ApartmentId, e.Date }, "idx_apartment_date");

            entity.Property(e => e.PricingId).HasColumnName("pricing_id");
            entity.Property(e => e.AcceptedByLandlord)
                .HasDefaultValueSql("'0'")
                .HasColumnName("accepted_by_landlord");
            entity.Property(e => e.ApartmentId).HasColumnName("apartment_id");
            entity.Property(e => e.BasePrice)
                .HasPrecision(12, 2)
                .HasColumnName("base_price");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");
            entity.Property(e => e.Date).HasColumnName("date");
            entity.Property(e => e.Multiplier)
                .HasPrecision(5, 2)
                .HasDefaultValueSql("'1.00'")
                .HasColumnName("multiplier");
            entity.Property(e => e.OccupancyRate)
                .HasPrecision(5, 2)
                .HasColumnName("occupancy_rate");
            entity.Property(e => e.Reason)
                .HasMaxLength(255)
                .HasColumnName("reason");
            entity.Property(e => e.SuggestedPrice)
                .HasPrecision(12, 2)
                .HasColumnName("suggested_price");

            entity.HasOne(d => d.Apartment).WithMany(p => p.SmartPricingHistories)
                .HasForeignKey(d => d.ApartmentId)
                .HasConstraintName("smart_pricing_history_ibfk_1");
        });

        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.HasKey(e => e.PlanId).HasName("PRIMARY");

            entity.ToTable("subscription_plans");

            entity.HasIndex(e => e.IsActive, "idx_active");

            entity.Property(e => e.PlanId).HasColumnName("plan_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.Features)
                .HasColumnType("text")
                .HasColumnName("features");
            entity.Property(e => e.IsActive)
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
            entity.Property(e => e.MaxApartments)
                .HasDefaultValueSql("'1'")
                .HasColumnName("max_apartments");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.PriceAnnual)
                .HasPrecision(12, 2)
                .HasColumnName("price_annual");
            entity.Property(e => e.PriceMonthly)
                .HasPrecision(12, 2)
                .HasColumnName("price_monthly");
        });

        modelBuilder.Entity<SupportTicket>(entity =>
        {
            entity.HasKey(e => e.TicketId).HasName("PRIMARY");

            entity.ToTable("support_tickets");

            entity.HasIndex(e => e.Category, "idx_category");

            entity.HasIndex(e => e.Priority, "idx_priority");

            entity.HasIndex(e => e.Status, "idx_status");

            entity.HasIndex(e => e.UserId, "idx_user");

            entity.HasIndex(e => e.ResolvedBy, "resolved_by");

            entity.Property(e => e.TicketId).HasColumnName("ticket_id");
            entity.Property(e => e.Category)
                .HasColumnType("enum('booking_issue','payment_problem','listing_problem','account_verification','cancellation','dispute','property_quality','other')")
                .HasColumnName("category");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.Priority)
                .HasDefaultValueSql("'medium'")
                .HasColumnType("enum('low','medium','high','urgent')")
                .HasColumnName("priority");
            entity.Property(e => e.ResolutionNotes)
                .HasColumnType("text")
                .HasColumnName("resolution_notes");
            entity.Property(e => e.ResolvedAt)
                .HasColumnType("timestamp")
                .HasColumnName("resolved_at");
            entity.Property(e => e.ResolvedBy).HasColumnName("resolved_by");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'open'")
                .HasColumnType("enum('open','in_progress','resolved','closed','escalated')")
                .HasColumnName("status");
            entity.Property(e => e.Subject)
                .HasMaxLength(200)
                .HasColumnName("subject");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.ResolvedByNavigation).WithMany(p => p.SupportTicketResolvedByNavigations)
                .HasForeignKey(d => d.ResolvedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("support_tickets_ibfk_2");

            entity.HasOne(d => d.User).WithMany(p => p.SupportTicketUsers)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("support_tickets_ibfk_1");
        });

        modelBuilder.Entity<SupportTicketAssignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentId).HasName("PRIMARY");

            entity.ToTable("support_ticket_assignments");

            entity.HasIndex(e => e.StaffId, "staff_id");

            entity.HasIndex(e => new { e.TicketId, e.StaffId }, "uk_assignment").IsUnique();

            entity.Property(e => e.AssignmentId).HasColumnName("assignment_id");
            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("assigned_at");
            entity.Property(e => e.RoleInTicket)
                .HasDefaultValueSql("'primary'")
                .HasColumnType("enum('primary','collaborator')")
                .HasColumnName("role_in_ticket");
            entity.Property(e => e.StaffId).HasColumnName("staff_id");
            entity.Property(e => e.TicketId).HasColumnName("ticket_id");

            entity.HasOne(d => d.Staff).WithMany(p => p.SupportTicketAssignments)
                .HasForeignKey(d => d.StaffId)
                .HasConstraintName("support_ticket_assignments_ibfk_2");

            entity.HasOne(d => d.Ticket).WithMany(p => p.SupportTicketAssignments)
                .HasForeignKey(d => d.TicketId)
                .HasConstraintName("support_ticket_assignments_ibfk_1");
        });

        modelBuilder.Entity<TemporaryResidenceReport>(entity =>
        {
            entity.HasKey(e => e.ReportId).HasName("PRIMARY");

            entity.ToTable("temporary_residence_reports");

            entity.HasIndex(e => e.LandlordId, "landlord_id");

            entity.HasIndex(e => e.BookingId, "uk_booking").IsUnique();

            entity.Property(e => e.ReportId).HasColumnName("report_id");
            entity.Property(e => e.BookingId).HasColumnName("booking_id");
            entity.Property(e => e.CheckInDate).HasColumnName("check_in_date");
            entity.Property(e => e.LandlordId).HasColumnName("landlord_id");
            entity.Property(e => e.ReportDate).HasColumnName("report_date");
            entity.Property(e => e.ReportNumber)
                .HasMaxLength(100)
                .HasColumnName("report_number");
            entity.Property(e => e.ReportedToPolice)
                .HasDefaultValueSql("'0'")
                .HasColumnName("reported_to_police");
            entity.Property(e => e.TenantNationality)
                .HasMaxLength(100)
                .HasColumnName("tenant_nationality");
            entity.Property(e => e.TenantPassportId)
                .HasMaxLength(50)
                .HasColumnName("tenant_passport_id");

            entity.HasOne(d => d.Booking).WithOne(p => p.TemporaryResidenceReport)
                .HasForeignKey<TemporaryResidenceReport>(d => d.BookingId)
                .HasConstraintName("temporary_residence_reports_ibfk_1");

            entity.HasOne(d => d.Landlord).WithMany(p => p.TemporaryResidenceReports)
                .HasForeignKey(d => d.LandlordId)
                .HasConstraintName("temporary_residence_reports_ibfk_2");
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.TenantId).HasName("PRIMARY");

            entity.ToTable("tenants");

            entity.HasIndex(e => e.IdentityVerificationStatus, "idx_verification_status");

            entity.Property(e => e.TenantId)
                .ValueGeneratedOnAdd()
                .HasColumnName("tenant_id");
            entity.Property(e => e.IdentityVerificationStatus)
                .HasDefaultValueSql("'not_started'")
                .HasColumnType("enum('not_started','pending','verified','rejected')")
                .HasColumnName("identity_verification_status");
            entity.Property(e => e.LastVerifiedAt)
                .HasColumnType("timestamp")
                .HasColumnName("last_verified_at");
            entity.Property(e => e.PassportId)
                .HasMaxLength(50)
                .HasColumnName("passport_id");

            entity.HasOne(d => d.TenantNavigation).WithOne(p => p.Tenant)
                .HasForeignKey<Tenant>(d => d.TenantId)
                .HasConstraintName("tenants_ibfk_1");
        });

        modelBuilder.Entity<TenantWishlist>(entity =>
        {
            entity.HasKey(e => e.WishlistId).HasName("PRIMARY");

            entity.ToTable("tenant_wishlists");

            entity.HasIndex(e => e.TenantId, "idx_tenant_id");

            entity.HasIndex(e => e.CollectionId, "idx_collection_id");

            entity.HasIndex(e => new { e.CollectionId, e.ApartmentId }, "uk_collection_apartment").IsUnique();

            entity.Property(e => e.WishlistId)
                .ValueGeneratedOnAdd()
                .HasColumnName("wishlist_id");

            entity.Property(e => e.TenantId)
                .HasColumnName("tenant_id");

            entity.Property(e => e.ApartmentId)
                .HasColumnName("apartment_id");

            entity.Property(e => e.CollectionId)
                .HasColumnName("collection_id");

            entity.Property(e => e.IsFavorite)
                .HasDefaultValue(false)
                .HasColumnName("is_favorite");

            entity.Property(e => e.Notes)
                .HasMaxLength(500)
                .HasColumnName("notes");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");

            entity.HasOne(d => d.Tenant).WithMany(p => p.Wishlists)
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("tenant_wishlists_ibfk_1");

            entity.HasOne(d => d.Apartment).WithMany(p => p.TenantWishlists)
                .HasForeignKey(d => d.ApartmentId)
                .HasConstraintName("tenant_wishlists_ibfk_2");

            entity.HasOne(d => d.Collection).WithMany(p => p.WishlistItems)
                .HasForeignKey(d => d.CollectionId)
                .HasConstraintName("tenant_wishlists_ibfk_3");
        });

        modelBuilder.Entity<WishlistCollection>(entity =>
        {
            entity.HasKey(e => e.CollectionId).HasName("PRIMARY");

            entity.ToTable("wishlist_collections");

            entity.HasIndex(e => e.TenantId, "idx_wishlist_collections_tenant_id");

            entity.HasIndex(e => new { e.TenantId, e.Name }, "uk_wishlist_collections_tenant_name").IsUnique();

            entity.Property(e => e.CollectionId)
                .ValueGeneratedOnAdd()
                .HasColumnName("collection_id");

            entity.Property(e => e.TenantId)
                .HasColumnName("tenant_id");

            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");

            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .HasColumnName("description");

            entity.Property(e => e.IsDefault)
                .HasDefaultValue(false)
                .HasColumnName("is_default");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Tenant).WithMany(p => p.WishlistCollections)
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("wishlist_collections_ibfk_1");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PRIMARY");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "email").IsUnique();

            entity.HasIndex(e => e.Role, "idx_role");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");
            entity.Property(e => e.Birthday).HasColumnName("birthday");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.FullName)
                .HasMaxLength(100)
                .HasColumnName("full_name");
            entity.Property(e => e.IdentityVerified)
                .HasDefaultValueSql("'0'")
                .HasColumnName("identity_verified");
            entity.Property(e => e.NationalIdCardNumber)
                .HasMaxLength(12)
                .HasColumnName("national_id_card_number");
            entity.Property(e => e.Nationality)
                .HasMaxLength(2)
                .IsFixedLength()
                .HasComment("ISO 3166-1 alpha-2 code (e.g. VN, US, KR). Used for temp residence reporting")
                .HasColumnName("nationality");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .HasColumnName("phone");
            entity.Property(e => e.Sex)
                .HasMaxLength(20)
                .HasColumnName("sex");
            entity.Property(e => e.Token)
                .HasMaxLength(500)
                .HasColumnName("token");
            entity.Property(e => e.TokenExpired)
                .HasColumnType("timestamp")
                .HasColumnName("token_expired");
            entity.Property(e => e.Role)
                .HasColumnType("enum('tenant','landlord','admin','staff')")
                .HasColumnName("role");
        });

        modelBuilder.Entity<UserIdentityDocument>(entity =>
        {
            entity.HasKey(e => e.DocumentId).HasName("PRIMARY");

            entity.ToTable("user_identity_documents");

            entity.HasIndex(e => e.VerificationStatus, "idx_status");

            entity.HasIndex(e => e.DocumentType, "idx_type");

            entity.HasIndex(e => e.UserId, "idx_user");

            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.DocumentType)
                .HasColumnType("enum('passport','national_id_card','drivers_license','other_government_id','selfie_with_id')")
                .HasColumnName("document_type");
            entity.Property(e => e.FileKey)
                .HasMaxLength(255)
                .HasColumnName("file_key");
            entity.Property(e => e.FileSize).HasColumnName("file_size");
            entity.Property(e => e.FileUrl)
                .HasMaxLength(500)
                .HasColumnName("file_url");
            entity.Property(e => e.MimeType)
                .HasMaxLength(100)
                .HasColumnName("mime_type");
            entity.Property(e => e.Notes)
                .HasColumnType("text")
                .HasColumnName("notes");
            entity.Property(e => e.RejectionReason)
                .HasColumnType("text")
                .HasColumnName("rejection_reason");
            entity.Property(e => e.Side)
                .HasDefaultValueSql("'front'")
                .HasColumnType("enum('front','back','bio_page','other')")
                .HasColumnName("side");
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp")
                .HasColumnName("uploaded_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.VerificationStatus)
                .HasDefaultValueSql("'pending'")
                .HasColumnType("enum('pending','verified','rejected','expired')")
                .HasColumnName("verification_status");
            entity.Property(e => e.VerifiedAt)
                .HasColumnType("timestamp")
                .HasColumnName("verified_at");

            entity.HasOne(d => d.User).WithMany(p => p.UserIdentityDocuments)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("user_identity_documents_ibfk_1");
        });

        modelBuilder.Entity<LandlordWallet>(entity =>
        {
            entity.HasKey(e => e.LandlordId).HasName("PRIMARY");

            entity.ToTable("landlord_wallets");

            entity.Property(e => e.LandlordId).HasColumnName("landlord_id");
            entity.Property(e => e.PendingBalance)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("'0.00'")
                .HasColumnName("pending_balance");
            entity.Property(e => e.AvailableBalance)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("'0.00'")
                .HasColumnName("available_balance");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Landlord).WithOne(p => p.LandlordWallet)
                .HasForeignKey<LandlordWallet>(d => d.LandlordId)
                .HasConstraintName("landlord_wallets_ibfk_1");
        });

        ApplySoftDeleteModelConfiguration(modelBuilder);
        OnModelCreatingPartial(modelBuilder);
    }

    public override int SaveChanges()
    {
        ApplySoftDeleteInterception();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplySoftDeleteInterception();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplySoftDeleteInterception();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplySoftDeleteInterception();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplySoftDeleteInterception()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Deleted))
        {
            if (entry.Metadata.FindProperty(SoftDeleteFlagColumn) == null)
            {
                continue;
            }

            entry.State = EntityState.Modified;
            entry.CurrentValues[SoftDeleteFlagColumn] = true;

            if (entry.Metadata.FindProperty(SoftDeleteTimestampColumn) != null)
            {
                entry.CurrentValues[SoftDeleteTimestampColumn] = now;
            }
        }
    }

    private void ApplySoftDeleteModelConfiguration(ModelBuilder modelBuilder)
    {
        var entityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(t => !t.IsOwned() && t.ClrType != null && t.ClrType != typeof(Dictionary<string, object>))
            .ToList();

        foreach (var entityType in entityTypes)
        {
            modelBuilder.Entity(entityType.ClrType)
                .Property<bool>(SoftDeleteFlagColumn)
                .HasColumnName(SoftDeleteFlagColumn)
                .HasDefaultValue(false);

            modelBuilder.Entity(entityType.ClrType)
                .Property<DateTime?>(SoftDeleteTimestampColumn)
                .HasColumnName(SoftDeleteTimestampColumn)
                .HasColumnType("datetime");

            var setFilterMethod = SetSoftDeleteFilterMethod.MakeGenericMethod(entityType.ClrType);
            setFilterMethod.Invoke(null, new object[] { modelBuilder });
        }
    }

    private static void SetSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(entity => !EF.Property<bool>(entity, SoftDeleteFlagColumn));
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
