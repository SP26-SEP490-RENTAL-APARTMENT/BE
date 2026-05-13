-- Migration: add provider-neutral columns to landlord_payouts
-- Created: 2026-05-13
-- Adds provider_name, provider_payout_id, provider_request_id, provider_trans_id,
-- provider_request_body, provider_response_body to landlord_payouts table.

START TRANSACTION;

ALTER TABLE landlord_payouts
    ADD COLUMN provider_name VARCHAR(200) NULL;

ALTER TABLE landlord_payouts
    ADD COLUMN provider_payout_id VARCHAR(100) NULL;

ALTER TABLE landlord_payouts
    ADD COLUMN provider_request_id VARCHAR(100) NULL;

ALTER TABLE landlord_payouts
    ADD COLUMN provider_trans_id VARCHAR(100) NULL;

ALTER TABLE landlord_payouts
    ADD COLUMN provider_request_body LONGTEXT NULL;

ALTER TABLE landlord_payouts
    ADD COLUMN provider_response_body LONGTEXT NULL;

COMMIT;
