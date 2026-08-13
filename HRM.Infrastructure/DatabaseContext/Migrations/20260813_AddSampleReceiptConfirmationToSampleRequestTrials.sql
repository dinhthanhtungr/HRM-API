-- Adds Sale's receipt confirmation to each delivered sample trial.
-- Safe to run repeatedly in environments where the trial table already exists.

ALTER TABLE "SampleRequests"."SampleRequestSampleTrials"
    ADD COLUMN IF NOT EXISTS "SampleReceivedDate" timestamp without time zone NULL,
    ADD COLUMN IF NOT EXISTS "SampleReceivedByEmployeeId" uuid NULL,
    ADD COLUMN IF NOT EXISTS "SampleReceiptConfirmedAt" timestamp without time zone NULL;

CREATE INDEX IF NOT EXISTS "IX_SampleRequestSampleTrials_SampleReceivedDate"
    ON "SampleRequests"."SampleRequestSampleTrials" ("SampleReceivedDate");
