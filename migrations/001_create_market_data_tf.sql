CREATE TABLE IF NOT EXISTS market_data_tf (
    pair_name           TEXT NOT NULL,
    candle_open_time    TIMESTAMPTZ NOT NULL,
    timeframe           TEXT NOT NULL,
    open_price          NUMERIC NOT NULL,
    high_price          NUMERIC NOT NULL,
    low_price           NUMERIC NOT NULL,
    close_price         NUMERIC NOT NULL,
    volume              NUMERIC NOT NULL,
    candle_close_time   TIMESTAMPTZ NOT NULL,
    buy_signal          BOOLEAN,
    sell_signal         BOOLEAN,
    sequence_label      TEXT,
    inserted_at         TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (pair_name, candle_open_time, timeframe)
);

CREATE INDEX IF NOT EXISTS idx_market_data_tf_pair_time
    ON market_data_tf (pair_name, timeframe, candle_close_time);
