CREATE TABLE IF NOT EXISTS password_setup_token (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES app_user(id) ON DELETE CASCADE,
    token_hash TEXT NOT NULL UNIQUE,
    expires_at TIMESTAMP NOT NULL,
    used_at TIMESTAMP NULL,
    created_at TIMESTAMP NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_password_setup_token_user_id ON password_setup_token(user_id);
CREATE INDEX IF NOT EXISTS idx_password_setup_token_expires_at ON password_setup_token(expires_at);
