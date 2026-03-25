CREATE TABLE IF NOT EXISTS support_resistance_levels (
    id                  SERIAL PRIMARY KEY,
    pair_name           TEXT NOT NULL,
    price_level         NUMERIC NOT NULL,
    level_type          TEXT NOT NULL,
    strength            INTEGER DEFAULT 1,
    first_detected_at   TIMESTAMPTZ NOT NULL,
    last_tested_at      TIMESTAMPTZ NOT NULL,
    is_active           BOOLEAN DEFAULT TRUE,
    created_at          TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (pair_name, level_type, first_detected_at)
);

CREATE INDEX IF NOT EXISTS idx_sr_levels_pair_active
    ON support_resistance_levels (pair_name, is_active);
