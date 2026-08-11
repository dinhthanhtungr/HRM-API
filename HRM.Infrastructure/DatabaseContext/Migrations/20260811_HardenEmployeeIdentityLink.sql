-- Hardens the Employee-to-Identity relationship and adds an explicit account lifecycle flag.
-- Run the duplicate preflight before deployment; the script aborts without creating the
-- unique index when more than one Identity user points to the same Employee.

ALTER TABLE public."AspNetUsers"
    ADD COLUMN IF NOT EXISTS "IsActive" boolean;

UPDATE public."AspNetUsers"
SET "IsActive" = TRUE
WHERE "IsActive" IS NULL;

ALTER TABLE public."AspNetUsers"
    ALTER COLUMN "IsActive" SET DEFAULT TRUE,
    ALTER COLUMN "IsActive" SET NOT NULL;

DO $$
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM public."AspNetUsers"
        WHERE "EmployeeId" IS NOT NULL
        GROUP BY "EmployeeId"
        HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION
            'Cannot create UX_AspNetUsers_EmployeeId_NotNull: duplicate EmployeeId links exist.';
    END IF;
END
$$;

CREATE UNIQUE INDEX IF NOT EXISTS "UX_AspNetUsers_EmployeeId_NotNull"
    ON public."AspNetUsers" ("EmployeeId")
    WHERE "EmployeeId" IS NOT NULL;
