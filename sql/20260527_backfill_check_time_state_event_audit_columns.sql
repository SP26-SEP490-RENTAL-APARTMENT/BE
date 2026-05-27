-- Backfill best-effort audit metadata for existing check_time_state_events rows
-- This uses event type, actor presence, and structured event payloads where available.

UPDATE `check_time_state_events`
SET
  `trigger_source` = CASE
    WHEN `trigger_source` IS NOT NULL AND `trigger_source` <> '' THEN `trigger_source`
    WHEN `event_type` IN ('automation_run_started', 'automation_run_completed', 'long_claim_queue', 'claim_locked', 'no_show_warning_sent', 'no_show_auto_confirmed', 'missing_checkout_auto_closed', 'automation_error') THEN 'automation'
    WHEN `event_type` IN ('payment_settled', 'payment_failed') THEN 'integration'
    WHEN `created_by` IS NULL THEN 'integration'
    ELSE 'manual'
  END,
  `trigger_reason` = CASE
    WHEN `trigger_reason` IS NOT NULL AND `trigger_reason` <> '' THEN `trigger_reason`
    WHEN `event_type` = 'no_show_marked' THEN 'manual_no_show'
    WHEN `event_type` = 'missing_checkout_closed' THEN 'manual_close_missing_checkout'
    WHEN `event_type` = 'automation_run_started' THEN 'automation_cycle_started'
    WHEN `event_type` = 'automation_run_completed' THEN 'automation_cycle_completed'
    WHEN `event_type` = 'long_claim_queue' THEN 'long_claim_threshold_reached'
    WHEN `event_type` = 'claim_locked' THEN 'claim_expired'
    WHEN `event_type` = 'no_show_warning_sent' THEN 'no_show_grace_window_elapsed'
    WHEN `event_type` = 'no_show_auto_confirmed' THEN 'no_show_warning_window_expired'
    WHEN `event_type` = 'missing_checkout_auto_closed' THEN 'missing_checkout_grace_elapsed'
    WHEN `event_type` = 'payment_settled' THEN 'stripe_checkout_session_completed'
    WHEN `event_type` = 'payment_failed' THEN 'payment_failed_or_expired'
    ELSE `event_type`
  END,
  `correlation_id` = CASE
    WHEN `correlation_id` IS NOT NULL AND `correlation_id` <> '' THEN `correlation_id`
    WHEN `event_type` IN ('automation_run_started', 'automation_run_completed', 'long_claim_queue') THEN JSON_UNQUOTE(JSON_EXTRACT(`event_data`, '$.RunId'))
    WHEN `event_type` = 'payment_settled' THEN JSON_UNQUOTE(JSON_EXTRACT(`event_data`, '$.TransactionId'))
    WHEN `event_type` = 'payment_failed' THEN JSON_UNQUOTE(JSON_EXTRACT(`event_data`, '$.TransactionId'))
    ELSE NULL
  END
WHERE
  (`trigger_source` IS NULL OR `trigger_source` = '')
  OR (`trigger_reason` IS NULL OR `trigger_reason` = '')
  OR (`correlation_id` IS NULL OR `correlation_id` = '');
