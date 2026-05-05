-- Add support_ticket_attachments table for storing evidence images
CREATE TABLE support_ticket_attachments (
    attachment_id char(36) NOT NULL,
    ticket_id char(36) NOT NULL,
    file_url varchar(1000) NOT NULL,
    mime_type varchar(100),
    file_size bigint,
    uploaded_at timestamp NULL DEFAULT current_timestamp(),
    uploaded_by char(36) NOT NULL,
    caption varchar(500),
    is_evidence tinyint(1) DEFAULT 1,
    is_deleted tinyint(1) DEFAULT 0,
    deleted_at datetime DEFAULT NULL,
    PRIMARY KEY (attachment_id),
    FOREIGN KEY (ticket_id) REFERENCES support_tickets(ticket_id),
    FOREIGN KEY (uploaded_by) REFERENCES users(user_id),
    INDEX idx_ticket (ticket_id),
    INDEX idx_uploaded_by (uploaded_by),
    INDEX idx_is_evidence (is_evidence)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;