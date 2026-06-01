using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations;

/// <inheritdoc />
public partial class AddMediaTypeColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET @has_apartment_media_type := (
                SELECT COUNT(*) FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'apartment_media'
                  AND COLUMN_NAME = 'type'
            );

            SET @sql := IF(
                @has_apartment_media_type > 0,
                'ALTER TABLE apartment_media MODIFY COLUMN type ENUM(''photo'',''video'',''image'') NOT NULL',
                'SELECT 1'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;

            UPDATE apartment_media SET type = 'image' WHERE type = 'photo';

            SET @sql := IF(
                @has_apartment_media_type > 0,
                'ALTER TABLE apartment_media
                    CHANGE COLUMN type media_type ENUM(''image'',''video'') NOT NULL',
                'SELECT 1'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;

            SET @has_apartment_media_media_type := (
                SELECT COUNT(*) FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'apartment_media'
                  AND COLUMN_NAME = 'media_type'
            );

            SET @sql := IF(
                @has_apartment_media_type = 0 AND @has_apartment_media_media_type = 0,
                'ALTER TABLE apartment_media ADD COLUMN media_type ENUM(''image'',''video'') NOT NULL DEFAULT ''image'' AFTER url',
                'SELECT 1'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;

            SET @has_inspection_type := (
                SELECT COUNT(*) FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'inspection_photos'
                  AND COLUMN_NAME = 'type'
            );

            SET @has_inspection_media_type := (
                SELECT COUNT(*) FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'inspection_photos'
                  AND COLUMN_NAME = 'media_type'
            );

            SET @sql := IF(
                @has_inspection_type > 0 AND @has_inspection_media_type = 0,
                'ALTER TABLE inspection_photos CHANGE COLUMN type media_type ENUM(''image'',''video'') NULL',
                'SELECT 1'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;

            SET @sql := IF(
                @has_inspection_type = 0 AND @has_inspection_media_type = 0,
                'ALTER TABLE inspection_photos ADD COLUMN media_type ENUM(''image'',''video'') NULL AFTER file_url',
                'SELECT 1'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;

            SET @has_ticket_type := (
                SELECT COUNT(*) FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'support_ticket_attachments'
                  AND COLUMN_NAME = 'type'
            );

            SET @has_ticket_media_type := (
                SELECT COUNT(*) FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'support_ticket_attachments'
                  AND COLUMN_NAME = 'media_type'
            );

            SET @sql := IF(
                @has_ticket_type > 0 AND @has_ticket_media_type = 0,
                'ALTER TABLE support_ticket_attachments CHANGE COLUMN type media_type ENUM(''image'',''video'') NULL',
                'SELECT 1'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;

            SET @sql := IF(
                @has_ticket_type = 0 AND @has_ticket_media_type = 0,
                'ALTER TABLE support_ticket_attachments ADD COLUMN media_type ENUM(''image'',''video'') NULL AFTER file_url',
                'SELECT 1'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;

            SET @has_check_in_media_type := (
                SELECT COUNT(*) FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'booking_check_times'
                  AND COLUMN_NAME = 'check_in_media_type'
            );

            SET @sql := IF(
                @has_check_in_media_type = 0,
                'ALTER TABLE booking_check_times
                    ADD COLUMN check_in_media_type ENUM(''image'',''video'') NULL AFTER check_in_photo_url,
                    ADD COLUMN check_out_media_type ENUM(''image'',''video'') NULL AFTER check_out_photo_url',
                'SELECT 1'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET @has_apartment_media_media_type := (
                SELECT COUNT(*) FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'apartment_media'
                  AND COLUMN_NAME = 'media_type'
            );

            SET @sql := IF(
                @has_apartment_media_media_type > 0,
                'ALTER TABLE apartment_media
                    CHANGE COLUMN media_type type ENUM(''photo'',''video'') NOT NULL',
                'SELECT 1'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;

            UPDATE apartment_media SET type = 'photo' WHERE type = 'image';

            SET @has_inspection_media_type := (
                SELECT COUNT(*) FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'inspection_photos'
                  AND COLUMN_NAME = 'media_type'
            );

            SET @sql := IF(
                @has_inspection_media_type > 0,
                'ALTER TABLE inspection_photos CHANGE COLUMN media_type type ENUM(''image'',''video'') NULL',
                'SELECT 1'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;

            SET @has_ticket_media_type := (
                SELECT COUNT(*) FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'support_ticket_attachments'
                  AND COLUMN_NAME = 'media_type'
            );

            SET @sql := IF(
                @has_ticket_media_type > 0,
                'ALTER TABLE support_ticket_attachments CHANGE COLUMN media_type type ENUM(''image'',''video'') NULL',
                'SELECT 1'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;

            SET @has_check_in_media_type := (
                SELECT COUNT(*) FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'booking_check_times'
                  AND COLUMN_NAME = 'check_in_media_type'
            );

            SET @sql := IF(
                @has_check_in_media_type > 0,
                'ALTER TABLE booking_check_times
                    DROP COLUMN check_in_media_type,
                    DROP COLUMN check_out_media_type',
                'SELECT 1'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            """);
    }
}
