using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenAndTokenExpiredToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "amenities",
                columns: table => new
                {
                    amenity_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    name_en = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name_vi = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.amenity_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "holidays_events",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    event_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    event_type = table.Column<string>(type: "enum('national_holiday','local_festival','international_event','school_break','major_conference','sports_event','other')", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    location_scope = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, defaultValueSql: "'Vietnam'", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_recurring = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'0'"),
                    recurrence_rule = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.event_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "nearby_attractions",
                columns: table => new
                {
                    attraction_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    name_en = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name_vi = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    type = table.Column<string>(type: "enum('restaurant','museum','park','landmark','shopping','transport')", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    location = table.Column<Point>(type: "point", nullable: false),
                    address = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    city = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.attraction_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "package_items",
                columns: table => new
                {
                    package_item_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    item_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    item_description = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    quantity = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true, defaultValueSql: "'1.00'"),
                    estimated_value = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true, defaultValueSql: "'0.00'"),
                    sort_order = table.Column<int>(type: "int", nullable: true, defaultValueSql: "'0'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.package_item_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    payment_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    related_entity_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    payment_type = table.Column<string>(type: "enum('deposit','balance','addon','refund')", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payment_purpose = table.Column<string>(type: "enum('booking_deposit','booking_balance','booking_addon_or_package','subscription_monthly','subscription_annual','subscription_trial','subscription_renewal','refund_booking','refund_subscription','other')", nullable: false, defaultValueSql: "'booking_deposit'", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    related_entity_type = table.Column<string>(type: "enum('booking','host_subscription','other')", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    method = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "enum('pending','success','failed','refunded')", nullable: true, defaultValueSql: "'pending'", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    transaction_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    paid_at = table.Column<DateTime>(type: "timestamp", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.payment_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "ReportDefinitions",
                columns: table => new
                {
                    ReportId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Type = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportDefinitions", x => x.ReportId);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "subscription_plans",
                columns: table => new
                {
                    plan_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    price_monthly = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    price_annual = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    max_apartments = table.Column<int>(type: "int", nullable: true, defaultValueSql: "'1'"),
                    features = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'1'"),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.plan_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    email = table.Column<string>(type: "varchar(255)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    password_hash = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    role = table.Column<string>(type: "enum('tenant','landlord','admin','staff')", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    full_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    identity_verified = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'0'"),
                    token = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    token_expired = table.Column<DateTime>(type: "timestamp", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.user_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "momo_transactions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    request_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    partner_code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    request_body = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    response_body = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    result_code = table.Column<int>(type: "int", nullable: true),
                    message = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", maxLength: 6, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", maxLength: 6, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn),
                    payment_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.id);
                    table.ForeignKey(
                        name: "fk_momo_transactions_payment",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "payment_id");
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "GeneratedReports",
                columns: table => new
                {
                    GeneratedReportId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ReportId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RequestedBy = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RequestedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResultSummaryJson = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResultJson = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RetentionUntil = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedReports", x => x.GeneratedReportId);
                    table.ForeignKey(
                        name: "FK_GeneratedReports_ReportDefinitions_ReportId",
                        column: x => x.ReportId,
                        principalTable: "ReportDefinitions",
                        principalColumn: "ReportId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "ReportQueryConfigs",
                columns: table => new
                {
                    ReportId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DimensionsJson = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FiltersJson = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MetricsJson = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TimeRangeJson = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportQueryConfigs", x => x.ReportId);
                    table.ForeignKey(
                        name: "FK_ReportQueryConfigs_ReportDefinitions_ReportId",
                        column: x => x.ReportId,
                        principalTable: "ReportDefinitions",
                        principalColumn: "ReportId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "ScheduledReports",
                columns: table => new
                {
                    ScheduledReportId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ReportId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Frequency = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CronExpression = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NextRunAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastRunAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeliveryChannel = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Recipients = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledReports", x => x.ScheduledReportId);
                    table.ForeignKey(
                        name: "FK_ScheduledReports_ReportDefinitions_ReportId",
                        column: x => x.ReportId,
                        principalTable: "ReportDefinitions",
                        principalColumn: "ReportId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "admin_actions",
                columns: table => new
                {
                    action_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    admin_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    action_type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    target_type = table.Column<string>(type: "enum('user','apartment','booking','review','report')", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    target_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    previous_value = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    new_value = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.action_id);
                    table.ForeignKey(
                        name: "admin_actions_ibfk_1",
                        column: x => x.admin_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "landlords",
                columns: table => new
                {
                    landlord_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    verified_business = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'0'"),
                    current_plan_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    subscription_expires_at = table.Column<DateOnly>(type: "date", nullable: true),
                    identity_verification_status = table.Column<string>(type: "enum('not_started','pending','verified','rejected')", nullable: true, defaultValueSql: "'not_started'", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_verified_at = table.Column<DateTime>(type: "timestamp", nullable: true),
                    subscription_status = table.Column<string>(type: "enum('none','active','expired','pending')", nullable: true, defaultValueSql: "'none'", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.landlord_id);
                    table.ForeignKey(
                        name: "landlords_ibfk_1",
                        column: x => x.landlord_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "landlords_ibfk_2",
                        column: x => x.current_plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "plan_id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    notification_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    type = table.Column<string>(type: "enum('booking_created','booking_confirmed','booking_cancelled','booking_upcoming','payment_success','payment_failed','identity_verified','identity_rejected','listing_approved','listing_rejected','inspection_scheduled','inspection_completed','support_ticket_created','support_ticket_update','support_ticket_resolved','review_reminder','new_message','system_announcement','other')", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    title = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    message = table.Column<string>(type: "text", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reference_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    reference_type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_read = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'0'"),
                    read_at = table.Column<DateTime>(type: "timestamp", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.notification_id);
                    table.ForeignKey(
                        name: "notifications_ibfk_1",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "support_tickets",
                columns: table => new
                {
                    ticket_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    subject = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "text", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    category = table.Column<string>(type: "enum('booking_issue','payment_problem','listing_problem','account_verification','cancellation','dispute','property_quality','other')", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    priority = table.Column<string>(type: "enum('low','medium','high','urgent')", nullable: true, defaultValueSql: "'medium'", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "enum('open','in_progress','resolved','closed','escalated')", nullable: true, defaultValueSql: "'open'", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "now()"),
                    resolved_at = table.Column<DateTime>(type: "timestamp", nullable: true),
                    resolved_by = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    resolution_notes = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.ticket_id);
                    table.ForeignKey(
                        name: "support_tickets_ibfk_1",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "support_tickets_ibfk_2",
                        column: x => x.resolved_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    passport_id = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    nationality = table.Column<string>(type: "char(2)", fixedLength: true, maxLength: 2, nullable: true, comment: "ISO 3166-1 alpha-2 code (e.g. VN, US, KR). Used for temp residence reporting", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    identity_verification_status = table.Column<string>(type: "enum('not_started','pending','verified','rejected')", nullable: true, defaultValueSql: "'not_started'", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_verified_at = table.Column<DateTime>(type: "timestamp", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.tenant_id);
                    table.ForeignKey(
                        name: "tenants_ibfk_1",
                        column: x => x.tenant_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "user_identity_documents",
                columns: table => new
                {
                    document_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    document_type = table.Column<string>(type: "enum('passport','national_id_card','drivers_license','other_government_id','selfie_with_id')", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    side = table.Column<string>(type: "enum('front','back','bio_page','other')", nullable: true, defaultValueSql: "'front'", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    file_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    file_key = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    mime_type = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    file_size = table.Column<long>(type: "bigint", nullable: true),
                    uploaded_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "now()"),
                    verified_at = table.Column<DateTime>(type: "timestamp", nullable: true),
                    verification_status = table.Column<string>(type: "enum('pending','verified','rejected','expired')", nullable: true, defaultValueSql: "'pending'", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    rejection_reason = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.document_id);
                    table.ForeignKey(
                        name: "user_identity_documents_ibfk_1",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "apartments",
                columns: table => new
                {
                    apartment_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    landlord_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    max_occupants = table.Column<sbyte>(type: "tinyint", nullable: true, defaultValueSql: "'1'"),
                    is_pet_allowed = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'0'"),
                    address = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    district = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    city = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, defaultValueSql: "'Hồ Chí Minh'", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    latitude = table.Column<decimal>(type: "decimal(10,8)", precision: 10, scale: 8, nullable: true),
                    longitude = table.Column<decimal>(type: "decimal(11,8)", precision: 11, scale: 8, nullable: true),
                    location = table.Column<Point>(type: "point", nullable: false),
                    base_price_per_night = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<string>(type: "enum('draft','pending_review','posted','blocked','archived')", nullable: true, defaultValueSql: "'draft'", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    booking_status = table.Column<string>(type: "enum('Available','ConfirmedReservation','Locked')", nullable: true, defaultValueSql: "'Available'", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.apartment_id);
                    table.ForeignKey(
                        name: "apartments_ibfk_1",
                        column: x => x.landlord_id,
                        principalTable: "landlords",
                        principalColumn: "landlord_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "landlord_subscriptions",
                columns: table => new
                {
                    subscription_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    landlord_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    plan_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    status = table.Column<string>(type: "enum('active','pending_payment','expired','cancelled','trial')", nullable: true, defaultValueSql: "'pending_payment'", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    renewal_type = table.Column<string>(type: "enum('monthly','annual','none')", nullable: true, defaultValueSql: "'monthly'", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    auto_renew = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'1'"),
                    payment_method = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_payment_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.subscription_id);
                    table.ForeignKey(
                        name: "landlord_subscriptions_ibfk_1",
                        column: x => x.landlord_id,
                        principalTable: "landlords",
                        principalColumn: "landlord_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "landlord_subscriptions_ibfk_2",
                        column: x => x.plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "plan_id");
                    table.ForeignKey(
                        name: "landlord_subscriptions_ibfk_3",
                        column: x => x.last_payment_id,
                        principalTable: "payments",
                        principalColumn: "payment_id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "landlord_wallets",
                columns: table => new
                {
                    landlord_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    pending_balance = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValueSql: "'0.00'"),
                    available_balance = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValueSql: "'0.00'"),
                    updated_at = table.Column<DateTime>(type: "timestamp", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.landlord_id);
                    table.ForeignKey(
                        name: "landlord_wallets_ibfk_1",
                        column: x => x.landlord_id,
                        principalTable: "landlords",
                        principalColumn: "landlord_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "support_ticket_assignments",
                columns: table => new
                {
                    assignment_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ticket_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    staff_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    assigned_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "now()"),
                    role_in_ticket = table.Column<string>(type: "enum('primary','collaborator')", nullable: true, defaultValueSql: "'primary'", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.assignment_id);
                    table.ForeignKey(
                        name: "support_ticket_assignments_ibfk_1",
                        column: x => x.ticket_id,
                        principalTable: "support_tickets",
                        principalColumn: "ticket_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "support_ticket_assignments_ibfk_2",
                        column: x => x.staff_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "apartment_amenities",
                columns: table => new
                {
                    apartment_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    amenity_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => new { x.apartment_id, x.amenity_id })
                        .Annotation("MySql:IndexPrefixLength", new[] { 0, 0 });
                    table.ForeignKey(
                        name: "apartment_amenities_ibfk_1",
                        column: x => x.apartment_id,
                        principalTable: "apartments",
                        principalColumn: "apartment_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "apartment_amenities_ibfk_2",
                        column: x => x.amenity_id,
                        principalTable: "amenities",
                        principalColumn: "amenity_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "apartment_availability",
                columns: table => new
                {
                    availability_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    apartment_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.availability_id);
                    table.ForeignKey(
                        name: "apartment_availability_ibfk_1",
                        column: x => x.apartment_id,
                        principalTable: "apartments",
                        principalColumn: "apartment_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "apartment_media",
                columns: table => new
                {
                    media_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    apartment_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    type = table.Column<string>(type: "enum('photo','video')", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_primary = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'0'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.media_id);
                    table.ForeignKey(
                        name: "apartment_media_ibfk_1",
                        column: x => x.apartment_id,
                        principalTable: "apartments",
                        principalColumn: "apartment_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "apartment_price_calendar",
                columns: table => new
                {
                    price_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    apartment_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    discount_percentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true, defaultValueSql: "'0.00'"),
                    is_discount = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'0'"),
                    price_type = table.Column<string>(type: "enum('base','weekend','holiday','peak_season','low_season','special_event','manual_override')", nullable: true, defaultValueSql: "'base'", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    min_nights = table.Column<int>(type: "int", nullable: true, defaultValueSql: "'1'"),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.price_id);
                    table.ForeignKey(
                        name: "apartment_price_calendar_ibfk_1",
                        column: x => x.apartment_id,
                        principalTable: "apartments",
                        principalColumn: "apartment_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "packages",
                columns: table => new
                {
                    package_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    apartment_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: true, defaultValueSql: "'VND'", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'1'"),
                    max_bookings = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.package_id);
                    table.ForeignKey(
                        name: "packages_ibfk_1",
                        column: x => x.apartment_id,
                        principalTable: "apartments",
                        principalColumn: "apartment_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "property_inspections",
                columns: table => new
                {
                    inspection_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    apartment_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    inspector_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    scheduled_date = table.Column<DateOnly>(type: "date", nullable: true),
                    completed_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "enum('pending','scheduled','in_progress','passed','failed','re_inspection_needed')", nullable: true, defaultValueSql: "'pending'", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    overall_condition = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    issues_found = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recommendations = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    approved_for_listing = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'0'"),
                    approved_at = table.Column<DateTime>(type: "timestamp", nullable: true),
                    approved_by = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.inspection_id);
                    table.ForeignKey(
                        name: "property_inspections_ibfk_1",
                        column: x => x.apartment_id,
                        principalTable: "apartments",
                        principalColumn: "apartment_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "property_inspections_ibfk_2",
                        column: x => x.inspector_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "property_inspections_ibfk_3",
                        column: x => x.approved_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "rooms",
                columns: table => new
                {
                    room_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    apartment_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    title = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    room_type = table.Column<string>(type: "enum('private_single','private_double','shared_bed','studio','other')", nullable: true, defaultValueSql: "'private_single'", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    bed_type = table.Column<string>(type: "enum('single','double','queen','king','bunk','shared')", nullable: true, defaultValueSql: "'single'", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    size_sqm = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    is_private_bathroom = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'0'"),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.room_id);
                    table.ForeignKey(
                        name: "rooms_ibfk_1",
                        column: x => x.apartment_id,
                        principalTable: "apartments",
                        principalColumn: "apartment_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "smart_pricing_history",
                columns: table => new
                {
                    pricing_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    apartment_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    suggested_price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    base_price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    multiplier = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true, defaultValueSql: "'1.00'"),
                    reason = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    occupancy_rate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    accepted_by_landlord = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'0'"),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.pricing_id);
                    table.ForeignKey(
                        name: "smart_pricing_history_ibfk_1",
                        column: x => x.apartment_id,
                        principalTable: "apartments",
                        principalColumn: "apartment_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    booking_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    tenant_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    apartment_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    check_in_date = table.Column<DateOnly>(type: "date", nullable: false),
                    check_out_date = table.Column<DateOnly>(type: "date", nullable: false),
                    nights = table.Column<int>(type: "int", nullable: false),
                    noOfAdults = table.Column<int>(type: "int", nullable: true),
                    noOfInfants = table.Column<int>(type: "int", nullable: true),
                    noOfPets = table.Column<int>(type: "int", nullable: true),
                    total_price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    package_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    package_price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true, defaultValueSql: "'0.00'"),
                    deposit_amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    deposit_paid = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'0'"),
                    balance_due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "enum('pending','negotiating','confirmed','paid','completed','cancelled','disputed')", nullable: true, defaultValueSql: "'pending'", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.booking_id);
                    table.ForeignKey(
                        name: "bookings_ibfk_1",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "bookings_ibfk_2",
                        column: x => x.apartment_id,
                        principalTable: "apartments",
                        principalColumn: "apartment_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "bookings_ibfk_3",
                        column: x => x.package_id,
                        principalTable: "packages",
                        principalColumn: "package_id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "package_packages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    package_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    package_item_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.id);
                    table.ForeignKey(
                        name: "package_packages_ibfk_1",
                        column: x => x.package_id,
                        principalTable: "packages",
                        principalColumn: "package_id");
                    table.ForeignKey(
                        name: "package_packages_ibfk_2",
                        column: x => x.package_item_id,
                        principalTable: "package_items",
                        principalColumn: "package_item_id");
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "inspection_photos",
                columns: table => new
                {
                    photo_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    inspection_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    file_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    file_key = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_issue = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'0'"),
                    uploaded_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.photo_id);
                    table.ForeignKey(
                        name: "inspection_photos_ibfk_1",
                        column: x => x.inspection_id,
                        principalTable: "property_inspections",
                        principalColumn: "inspection_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "booking_check_times",
                columns: table => new
                {
                    check_time_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    booking_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    scheduled_check_in = table.Column<DateTime>(type: "datetime", nullable: false),
                    scheduled_check_out = table.Column<DateTime>(type: "datetime", nullable: false),
                    actual_check_in = table.Column<DateTime>(type: "datetime", nullable: true),
                    actual_check_out = table.Column<DateTime>(type: "datetime", nullable: true),
                    is_late_check_out = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'0'"),
                    late_check_out_fee = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true, defaultValueSql: "'0.00'"),
                    is_early_check_in = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'0'"),
                    early_check_in_fee = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true, defaultValueSql: "'0.00'"),
                    temp_residence_reported = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'0'"),
                    reported_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    report_reference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recorded_by = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    recorded_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.check_time_id);
                    table.ForeignKey(
                        name: "booking_check_times_ibfk_1",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "booking_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "reviews",
                columns: table => new
                {
                    review_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    booking_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    reviewer_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    reviewed_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    apartment_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    rating = table.Column<sbyte>(type: "tinyint", nullable: true),
                    comment_en = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    comment_vi = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.review_id);
                    table.ForeignKey(
                        name: "reviews_ibfk_1",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "booking_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "reviews_ibfk_2",
                        column: x => x.reviewer_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "reviews_ibfk_3",
                        column: x => x.reviewed_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "reviews_ibfk_4",
                        column: x => x.apartment_id,
                        principalTable: "apartments",
                        principalColumn: "apartment_id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "temporary_residence_reports",
                columns: table => new
                {
                    report_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    booking_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    landlord_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    tenant_passport_id = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tenant_nationality = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    check_in_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reported_to_police = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'0'"),
                    report_date = table.Column<DateOnly>(type: "date", nullable: true),
                    report_number = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.report_id);
                    table.ForeignKey(
                        name: "temporary_residence_reports_ibfk_1",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "booking_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "temporary_residence_reports_ibfk_2",
                        column: x => x.landlord_id,
                        principalTable: "landlords",
                        principalColumn: "landlord_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "idx_admin",
                table: "admin_actions",
                column: "admin_id");

            migrationBuilder.CreateIndex(
                name: "idx_target",
                table: "admin_actions",
                columns: new[] { "target_type", "target_id" });

            migrationBuilder.CreateIndex(
                name: "uk_name_en",
                table: "amenities",
                column: "name_en",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uk_name_vi",
                table: "amenities",
                column: "name_vi",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "amenity_id",
                table: "apartment_amenities",
                column: "amenity_id");

            migrationBuilder.CreateIndex(
                name: "idx_apartment_availability_dates",
                table: "apartment_availability",
                columns: new[] { "apartment_id", "start_date", "end_date" });

            migrationBuilder.CreateIndex(
                name: "idx_apartment",
                table: "apartment_media",
                column: "apartment_id");

            migrationBuilder.CreateIndex(
                name: "idx_apartment_dates",
                table: "apartment_price_calendar",
                columns: new[] { "apartment_id", "start_date", "end_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_dates",
                table: "apartment_price_calendar",
                columns: new[] { "start_date", "end_date" });

            migrationBuilder.CreateIndex(
                name: "idx_booking_status",
                table: "apartments",
                column: "booking_status");

            migrationBuilder.CreateIndex(
                name: "idx_landlord",
                table: "apartments",
                column: "landlord_id");

            migrationBuilder.CreateIndex(
                name: "idx_location",
                table: "apartments",
                column: "location")
                .Annotation("MySql:SpatialIndex", true);

            migrationBuilder.CreateIndex(
                name: "idx_status",
                table: "apartments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_actual_in",
                table: "booking_check_times",
                column: "actual_check_in");

            migrationBuilder.CreateIndex(
                name: "idx_booking",
                table: "booking_check_times",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_scheduled_in",
                table: "booking_check_times",
                column: "scheduled_check_in");

            migrationBuilder.CreateIndex(
                name: "idx_apartment1",
                table: "bookings",
                column: "apartment_id");

            migrationBuilder.CreateIndex(
                name: "idx_dates1",
                table: "bookings",
                columns: new[] { "check_in_date", "check_out_date" });

            migrationBuilder.CreateIndex(
                name: "idx_package",
                table: "bookings",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "idx_status1",
                table: "bookings",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_tenant",
                table: "bookings",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedReports_ReportId",
                table: "GeneratedReports",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "idx_dates2",
                table: "holidays_events",
                columns: new[] { "start_date", "end_date" });

            migrationBuilder.CreateIndex(
                name: "idx_type_scope",
                table: "holidays_events",
                columns: new[] { "event_type", "location_scope" });

            migrationBuilder.CreateIndex(
                name: "uk_event_dates",
                table: "holidays_events",
                columns: new[] { "event_name", "start_date", "end_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_inspection",
                table: "inspection_photos",
                column: "inspection_id");

            migrationBuilder.CreateIndex(
                name: "idx_end_date",
                table: "landlord_subscriptions",
                column: "end_date");

            migrationBuilder.CreateIndex(
                name: "idx_landlord_status",
                table: "landlord_subscriptions",
                columns: new[] { "landlord_id", "status" });

            migrationBuilder.CreateIndex(
                name: "last_payment_id",
                table: "landlord_subscriptions",
                column: "last_payment_id");

            migrationBuilder.CreateIndex(
                name: "plan_id",
                table: "landlord_subscriptions",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "current_plan_id",
                table: "landlords",
                column: "current_plan_id");

            migrationBuilder.CreateIndex(
                name: "idx_subscription_status",
                table: "landlords",
                column: "subscription_status");

            migrationBuilder.CreateIndex(
                name: "idx_verification_status",
                table: "landlords",
                column: "identity_verification_status");

            migrationBuilder.CreateIndex(
                name: "ix_momo_transactions_payment_id",
                table: "momo_transactions",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_momo_transactions_request_id",
                table: "momo_transactions",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "idx_location1",
                table: "nearby_attractions",
                column: "location")
                .Annotation("MySql:SpatialIndex", true);

            migrationBuilder.CreateIndex(
                name: "idx_reference",
                table: "notifications",
                columns: new[] { "reference_type", "reference_id" });

            migrationBuilder.CreateIndex(
                name: "idx_type",
                table: "notifications",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "idx_user_created",
                table: "notifications",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_user_read",
                table: "notifications",
                columns: new[] { "user_id", "is_read" });

            migrationBuilder.CreateIndex(
                name: "package_packages_ibfk_1",
                table: "package_packages",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "package_packages_ibfk_2",
                table: "package_packages",
                column: "package_item_id");

            migrationBuilder.CreateIndex(
                name: "idx_active",
                table: "packages",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_apartment2",
                table: "packages",
                column: "apartment_id");

            migrationBuilder.CreateIndex(
                name: "idx_entity",
                table: "payments",
                columns: new[] { "related_entity_type", "related_entity_id" });

            migrationBuilder.CreateIndex(
                name: "idx_purpose",
                table: "payments",
                column: "payment_purpose");

            migrationBuilder.CreateIndex(
                name: "idx_status2",
                table: "payments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "approved_by",
                table: "property_inspections",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "idx_apartment3",
                table: "property_inspections",
                column: "apartment_id");

            migrationBuilder.CreateIndex(
                name: "idx_inspector",
                table: "property_inspections",
                column: "inspector_id");

            migrationBuilder.CreateIndex(
                name: "idx_status3",
                table: "property_inspections",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "apartment_id",
                table: "reviews",
                column: "apartment_id");

            migrationBuilder.CreateIndex(
                name: "reviewed_id",
                table: "reviews",
                column: "reviewed_id");

            migrationBuilder.CreateIndex(
                name: "reviewer_id",
                table: "reviews",
                column: "reviewer_id");

            migrationBuilder.CreateIndex(
                name: "uk_booking_review",
                table: "reviews",
                columns: new[] { "booking_id", "reviewer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_apartment4",
                table: "rooms",
                column: "apartment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledReports_ReportId",
                table: "ScheduledReports",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "idx_apartment_date",
                table: "smart_pricing_history",
                columns: new[] { "apartment_id", "date" });

            migrationBuilder.CreateIndex(
                name: "idx_active1",
                table: "subscription_plans",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "staff_id",
                table: "support_ticket_assignments",
                column: "staff_id");

            migrationBuilder.CreateIndex(
                name: "uk_assignment",
                table: "support_ticket_assignments",
                columns: new[] { "ticket_id", "staff_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_category",
                table: "support_tickets",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "idx_priority",
                table: "support_tickets",
                column: "priority");

            migrationBuilder.CreateIndex(
                name: "idx_status4",
                table: "support_tickets",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_user",
                table: "support_tickets",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "resolved_by",
                table: "support_tickets",
                column: "resolved_by");

            migrationBuilder.CreateIndex(
                name: "landlord_id",
                table: "temporary_residence_reports",
                column: "landlord_id");

            migrationBuilder.CreateIndex(
                name: "uk_booking",
                table: "temporary_residence_reports",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_verification_status1",
                table: "tenants",
                column: "identity_verification_status");

            migrationBuilder.CreateIndex(
                name: "idx_status5",
                table: "user_identity_documents",
                column: "verification_status");

            migrationBuilder.CreateIndex(
                name: "idx_type1",
                table: "user_identity_documents",
                column: "document_type");

            migrationBuilder.CreateIndex(
                name: "idx_user1",
                table: "user_identity_documents",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_role",
                table: "users",
                column: "role");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_actions");

            migrationBuilder.DropTable(
                name: "apartment_amenities");

            migrationBuilder.DropTable(
                name: "apartment_availability");

            migrationBuilder.DropTable(
                name: "apartment_media");

            migrationBuilder.DropTable(
                name: "apartment_price_calendar");

            migrationBuilder.DropTable(
                name: "booking_check_times");

            migrationBuilder.DropTable(
                name: "GeneratedReports");

            migrationBuilder.DropTable(
                name: "holidays_events");

            migrationBuilder.DropTable(
                name: "inspection_photos");

            migrationBuilder.DropTable(
                name: "landlord_subscriptions");

            migrationBuilder.DropTable(
                name: "landlord_wallets");

            migrationBuilder.DropTable(
                name: "momo_transactions");

            migrationBuilder.DropTable(
                name: "nearby_attractions");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "package_packages");

            migrationBuilder.DropTable(
                name: "ReportQueryConfigs");

            migrationBuilder.DropTable(
                name: "reviews");

            migrationBuilder.DropTable(
                name: "rooms");

            migrationBuilder.DropTable(
                name: "ScheduledReports");

            migrationBuilder.DropTable(
                name: "smart_pricing_history");

            migrationBuilder.DropTable(
                name: "support_ticket_assignments");

            migrationBuilder.DropTable(
                name: "temporary_residence_reports");

            migrationBuilder.DropTable(
                name: "user_identity_documents");

            migrationBuilder.DropTable(
                name: "amenities");

            migrationBuilder.DropTable(
                name: "property_inspections");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "package_items");

            migrationBuilder.DropTable(
                name: "ReportDefinitions");

            migrationBuilder.DropTable(
                name: "support_tickets");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropTable(
                name: "packages");

            migrationBuilder.DropTable(
                name: "apartments");

            migrationBuilder.DropTable(
                name: "landlords");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "subscription_plans");
        }
    }
}
