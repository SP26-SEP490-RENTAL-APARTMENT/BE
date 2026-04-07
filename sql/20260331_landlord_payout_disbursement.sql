-- Landlord payout + MoMo disbursement schema changes
-- Safe for MySQL 8.x

ALTER TABLE landlords
    ADD COLUMN momo_wallet_phone VARCHAR(20) NULL,
    ADD COLUMN payout_receiver_name VARCHAR(150) NULL,
    ADD COLUMN payout_personal_id VARCHAR(30) NULL,
    ADD COLUMN payout_bank_account_no VARCHAR(40) NULL,
    ADD COLUMN payout_bank_card_no VARCHAR(40) NULL,
    ADD COLUMN payout_bank_code VARCHAR(20) NULL,
    ADD COLUMN preferred_payout_method VARCHAR(20) NULL;

CREATE TABLE IF NOT EXISTS landlord_payouts (
    payout_id CHAR(36) NOT NULL,
    landlord_id CHAR(36) NOT NULL,
    amount BIGINT NOT NULL,
    fee_amount BIGINT NOT NULL DEFAULT 0,
    net_amount BIGINT NOT NULL,
    channel VARCHAR(20) NOT NULL,
    status VARCHAR(30) NOT NULL,
    momo_order_id VARCHAR(120) NOT NULL,
    momo_request_id VARCHAR(120) NOT NULL,
    momo_trans_id VARCHAR(120) NULL,
    result_code INT NULL,
    message VARCHAR(1024) NULL,
    request_body LONGTEXT NULL,
    response_body LONGTEXT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    completed_at TIMESTAMP NULL,
    failed_at TIMESTAMP NULL,
    CONSTRAINT pk_landlord_payouts PRIMARY KEY (payout_id),
    CONSTRAINT fk_landlord_payouts_landlord FOREIGN KEY (landlord_id) REFERENCES landlords (landlord_id) ON DELETE CASCADE
);

CREATE INDEX idx_landlord_payouts_landlord ON landlord_payouts (landlord_id);
CREATE INDEX idx_landlord_payouts_landlord_created ON landlord_payouts (landlord_id, created_at);
CREATE UNIQUE INDEX uk_landlord_payouts_momo_request_id ON landlord_payouts (momo_request_id);
