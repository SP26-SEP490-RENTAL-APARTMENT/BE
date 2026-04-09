-- Prevent callback crashes when message/response_body are omitted on insert.
ALTER TABLE momo_transactions
    MODIFY COLUMN message VARCHAR(1024) NOT NULL DEFAULT '',
    MODIFY COLUMN response_body LONGTEXT NOT NULL DEFAULT ('');

-- Backfill any existing null message values defensively (for legacy rows).
UPDATE momo_transactions
SET message = ''
WHERE message IS NULL;
