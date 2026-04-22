-- T3.2: admin@auracore.pro user had license.Tier='free'. Promote to enterprise
-- since admin operations need full access.

BEGIN;

UPDATE licenses
SET "Tier" = 'enterprise',
    "MaxDevices" = 10
WHERE "UserId" IN (
    SELECT "Id" FROM users WHERE "Email" = 'admin@auracore.pro'
)
AND "Status" = 'active'
AND "Tier" = 'free'
RETURNING "Id", "UserId", "Tier", "MaxDevices", "ExpiresAt";

COMMIT;
