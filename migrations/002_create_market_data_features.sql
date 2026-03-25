CREATE TABLE IF NOT EXISTS market_data_features (
    pair_name            TEXT NOT NULL,
    candle_open_time     TIMESTAMPTZ NOT NULL,
    timeframe            TEXT NOT NULL,
    -- OHLCV
    open_price           NUMERIC NOT NULL,
    high_price           NUMERIC NOT NULL,
    low_price            NUMERIC NOT NULL,
    close_price          NUMERIC NOT NULL,
    volume               NUMERIC NOT NULL,
    -- Momentum
    rsi_14               NUMERIC,
    rsi_7                NUMERIC,
    stoch_rsi_k          NUMERIC,
    stoch_rsi_d          NUMERIC,
    roc_14               NUMERIC,
    -- Trend
    ema_9                NUMERIC,
    ema_21               NUMERIC,
    ema_50               NUMERIC,
    ema_200              NUMERIC,
    macd_line            NUMERIC,
    macd_signal          NUMERIC,
    macd_histogram       NUMERIC,
    adx_14               NUMERIC,
    -- Volatility
    bb_upper             NUMERIC,
    bb_middle            NUMERIC,
    bb_lower             NUMERIC,
    bb_pctb              NUMERIC,
    atr_14               NUMERIC,
    -- Volume
    obv                  NUMERIC,
    vwap                 NUMERIC,
    volume_sma_20        NUMERIC,
    cmf_20               NUMERIC,
    -- Custom
    price_ema9_ratio     NUMERIC,
    price_ema21_ratio    NUMERIC,
    macd_hist_slope      NUMERIC,
    -- Support/Resistance
    nearest_support      NUMERIC,
    nearest_resistance   NUMERIC,
    dist_support_pct     NUMERIC,
    dist_resistance_pct  NUMERIC,
    support_strength     INTEGER,
    resistance_strength  INTEGER,
    sr_zone_position     NUMERIC,
    num_sr_within_1pct   INTEGER,
    -- Labels
    buy_signal           BOOLEAN,
    sell_signal          BOOLEAN,
    target_pct           NUMERIC,
    drawdown_pct         NUMERIC,
    -- Meta
    inserted_at          TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (pair_name, candle_open_time, timeframe)
);
