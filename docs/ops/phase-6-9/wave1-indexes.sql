-- Phase 6.9 Wave 1: CTP-9 prod DB index catch-up.
-- Idempotent — safe to re-run. Does NOT create an EF migration; these are
-- schema-level query optimizations that were declared in the EF model but
-- never reached prod (the DB was raw-DDL-bootstrapped).

BEGIN;

-- crash_reports: audit T2.20 — CreatedAt index missing
CREATE INDEX IF NOT EXISTS idx_crash_reports_createdat
    ON crash_reports ("CreatedAt" DESC);

-- telemetry_events: audit T2.22 — CreatedAt + EventType indexes missing
CREATE INDEX IF NOT EXISTS idx_telemetry_events_createdat
    ON telemetry_events ("CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS idx_telemetry_events_eventtype
    ON telemetry_events ("EventType");

-- login_attempts: audit T2.23 — composite indexes missing
CREATE INDEX IF NOT EXISTS idx_login_attempts_email_createdat
    ON login_attempts ("Email", "CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS idx_login_attempts_ip_createdat
    ON login_attempts ("IpAddress", "CreatedAt" DESC);

-- payments: audit T2.13 — ExternalId should be UNIQUE (DB-level duplicate prevention)
-- Check if unique index already exists (Phase 6.6 may have added it) before creating
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE tablename = 'payments'
          AND indexname = 'uq_payments_externalid'
    ) THEN
        -- DELETE any duplicate ExternalId rows first (keep earliest)
        -- then create unique index. This is intentionally destructive for
        -- historically-duplicate rows (Stripe webhook pre-idempotency-guard).
        EXECUTE $del$
            DELETE FROM payments a USING payments b
            WHERE a."ExternalId" = b."ExternalId"
              AND a."ExternalId" IS NOT NULL
              AND a."ExternalId" <> ''
              AND a."CreatedAt" > b."CreatedAt"
        $del$;
        CREATE UNIQUE INDEX uq_payments_externalid
            ON payments ("ExternalId")
            WHERE "ExternalId" IS NOT NULL AND "ExternalId" <> '';
    END IF;
END $$;

-- devices: Phase 6.8 already has unique (LicenseId, HardwareFingerprint).
-- Add lookup index on LastSeenAt DESC for "recently active" queries.
CREATE INDEX IF NOT EXISTS idx_devices_lastseenat
    ON devices ("LastSeenAt" DESC);

-- T1.26: singleton enforcement on app_configs. Add a check constraint so
-- future INSERTs with id != 1 fail at DB level. Do NOT delete existing
-- extra rows automatically (avoid destructive auto-cleanup).
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.constraint_column_usage
        WHERE table_name = 'app_configs' AND constraint_name = 'chk_app_configs_singleton'
    ) THEN
        ALTER TABLE app_configs
            ADD CONSTRAINT chk_app_configs_singleton CHECK ("Id" = 1);
    END IF;
END $$;

-- ip_whitelists already has unique IpAddress index from Phase 6.8 DDL.
-- audit_log already has 3 indexes from Phase 6.8.

COMMIT;

-- Post-apply verification
\echo ''
\echo '=== new indexes present ==='
SELECT indexname, tablename
FROM pg_indexes
WHERE indexname IN (
    'idx_crash_reports_createdat',
    'idx_telemetry_events_createdat',
    'idx_telemetry_events_eventtype',
    'idx_login_attempts_email_createdat',
    'idx_login_attempts_ip_createdat',
    'uq_payments_externalid',
    'idx_devices_lastseenat'
)
ORDER BY tablename, indexname;
\echo '=== singleton constraint present? ==='
SELECT constraint_name FROM information_schema.table_constraints
WHERE table_name = 'app_configs' AND constraint_name = 'chk_app_configs_singleton';
\echo '=== duplicate ExternalId payments should now be 0 ==='
SELECT "ExternalId", COUNT(*) AS cnt
FROM payments
WHERE "ExternalId" IS NOT NULL AND "ExternalId" <> ''
GROUP BY "ExternalId"
HAVING COUNT(*) > 1;
