-- Soft-delete rollout for all base tables in current database.
-- Adds `is_deleted` (soft delete flag) and `deleted_at` (deletion timestamp)
-- to every table except __EFMigrationsHistory.

SET SQL_SAFE_UPDATES = 0;

DROP PROCEDURE IF EXISTS add_soft_delete_columns_to_all_tables;
DELIMITER $$
CREATE PROCEDURE add_soft_delete_columns_to_all_tables()
BEGIN
    DECLARE done INT DEFAULT 0;
    DECLARE table_name_var VARCHAR(255);

    DECLARE table_cursor CURSOR FOR
        SELECT t.TABLE_NAME
        FROM information_schema.TABLES t
        WHERE t.TABLE_SCHEMA = DATABASE()
          AND t.TABLE_TYPE = 'BASE TABLE'
          AND t.TABLE_NAME <> '__EFMigrationsHistory';

    DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = 1;

    OPEN table_cursor;

    table_loop: LOOP
        FETCH table_cursor INTO table_name_var;
        IF done = 1 THEN
            LEAVE table_loop;
        END IF;

        IF NOT EXISTS (
            SELECT 1
            FROM information_schema.COLUMNS c
            WHERE c.TABLE_SCHEMA = DATABASE()
              AND c.TABLE_NAME = table_name_var
              AND c.COLUMN_NAME = 'is_deleted'
        ) THEN
            SET @sql_flag = CONCAT(
                'ALTER TABLE `', table_name_var,
                '` ADD COLUMN `is_deleted` TINYINT(1) NOT NULL DEFAULT 0'
            );
            PREPARE stmt_flag FROM @sql_flag;
            EXECUTE stmt_flag;
            DEALLOCATE PREPARE stmt_flag;
        END IF;

        IF NOT EXISTS (
            SELECT 1
            FROM information_schema.COLUMNS c
            WHERE c.TABLE_SCHEMA = DATABASE()
              AND c.TABLE_NAME = table_name_var
              AND c.COLUMN_NAME = 'deleted_at'
        ) THEN
            SET @sql_deleted_at = CONCAT(
                'ALTER TABLE `', table_name_var,
                '` ADD COLUMN `deleted_at` DATETIME NULL'
            );
            PREPARE stmt_deleted_at FROM @sql_deleted_at;
            EXECUTE stmt_deleted_at;
            DEALLOCATE PREPARE stmt_deleted_at;
        END IF;
    END LOOP;

    CLOSE table_cursor;
END $$
DELIMITER ;

CALL add_soft_delete_columns_to_all_tables();
DROP PROCEDURE IF EXISTS add_soft_delete_columns_to_all_tables;

SET SQL_SAFE_UPDATES = 1;
