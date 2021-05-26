BEGIN;

-- Creates participant records tables and their access controls.
-- Assumes 3 database roles, to be set via the psql -v option:
--  * cluster `superuser`
--  * database `owner`, which owns the tables, sequences
--  * database `admin`, which gets read/write access

SET search_path=piipan,public;

CREATE TABLE IF NOT EXISTS uploads(
	id serial PRIMARY KEY,
	created_at timestamp NOT NULL,
	publisher text NOT NULL
);

COMMENT ON TABLE uploads IS 'Bulk PII upload events';
COMMENT ON COLUMN uploads.created_at IS 'Date/time the records were uploaded in bulk';
COMMENT ON COLUMN uploads.publisher IS 'User or service account that performed the upload';

CREATE TABLE IF NOT EXISTS participants(
	id serial PRIMARY KEY,
	last text NOT NULL,
	first text,
	middle text,
	dob date NOT NULL,
	ssn text NOT NULL,
	exception text,
	upload_id integer REFERENCES uploads (id),
  	case_id text NOT NULL,
  	participant_id text,
	benefits_end_date date,
    	recent_benefit_months date[]
);

COMMENT ON TABLE participants IS 'Program participant Personally Identifiable Information (PII)';
COMMENT ON COLUMN participants.last IS 'Participant''s last name';
COMMENT ON COLUMN participants.first IS 'Participant''s first name';
COMMENT ON COLUMN participants.middle IS 'Participant''s middle name';
COMMENT ON COLUMN participants.dob IS 'Participant''s date of birth';
COMMENT ON COLUMN participants.ssn IS 'Participant''s Social Security Number';
COMMENT ON COLUMN participants.exception IS 'Placeholder for value indicating special processing instructions';
COMMENT ON COLUMN participants.case_id IS 'Participant''s state-specific case identifier';
COMMENT ON COLUMN participants.participant_id IS 'Participant''s state-specific identifier';
COMMENT ON COLUMN participants.benefits_end_date IS 'Participant''s ending benefits date';
COMMENT ON COLUMN participants.recent_benefit_months IS 'Participant''s recent benefit months';

CREATE INDEX IF NOT EXISTS participants_ssn_idx ON participants (ssn, upload_id);

-- "superuser" account under Azure is not so super
GRANT :owner to :superuser;

ALTER TABLE uploads OWNER TO :owner;
ALTER TABLE participants OWNER TO :owner;

ALTER SEQUENCE participants_id_seq OWNER TO :owner;
ALTER SEQUENCE uploads_id_seq OWNER TO :owner;

GRANT SELECT, INSERT, UPDATE, DELETE ON participants TO :admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON uploads TO :admin;
GRANT USAGE, SELECT, UPDATE ON participants_id_seq TO :admin;
GRANT USAGE, SELECT, UPDATE ON uploads_id_seq TO :admin;

-- restore privileges
REVOKE :owner FROM :superuser;

COMMIT;
