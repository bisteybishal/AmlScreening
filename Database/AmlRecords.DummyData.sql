/* Development-only dummy records. Names and references are fictional. */
SET NOCOUNT ON;

INSERT INTO dbo.AmlRecords
    (RecordType, FullName, DateOfBirth, Country, Nationality, ReferenceNo, SourceName, Remarks, IsActive)
SELECT v.RecordType, v.FullName, v.DateOfBirth, v.Country, v.Nationality,
       v.ReferenceNo, v.SourceName, v.Remarks, v.IsActive
FROM (VALUES
    (N'Individual', N'Arun Dev Sharma',   CONVERT(date, '1965-04-12'), N'Nepal',            N'Nepali',          N'DEMO-PEP-001', N'PEP',          N'Fictional politically exposed person record for local testing.', CONVERT(bit, 1)),
    (N'Individual', N'Maya Koirala',      CONVERT(date, '1972-09-23'), N'Nepal',            N'Nepali',          N'DEMO-PEP-002', N'PEP',          N'Fictional close associate of a PEP for local testing.',          CONVERT(bit, 1)),
    (N'Entity',     N'Himalaya Civic Fund', CONVERT(date, '2004-06-15'), N'Nepal',          N'Nepali',          N'DEMO-PEP-003', N'PEP',          N'Fictional entity linked to a PEP for local testing.',             CONVERT(bit, 0)),
    (N'Individual', N'Elena Markovic',    CONVERT(date, '1980-02-08'), N'Serbia',           N'Serbian',         N'DEMO-EU-001',  N'EU Sanctions', N'Fictional individual sanctions record for local testing.',       CONVERT(bit, 1)),
    (N'Entity',     N'Baltic Meridian LLC', CONVERT(date, '2011-11-02'), N'Latvia',         N'Latvian',         N'DEMO-EU-002',  N'EU Sanctions', N'Fictional organization sanctions record for local testing.',     CONVERT(bit, 1)),
    (N'Vessel',     N'MV Northern Star',  NULL,                        N'Malta',            N'Maltese',         N'DEMO-EU-003',  N'EU Sanctions', N'Fictional vessel sanctions record for local testing.',           CONVERT(bit, 0)),
    (N'Individual', N'Karim Al Noor',     CONVERT(date, '1976-07-19'), N'Jordan',           N'Jordanian',       N'DEMO-UN-001',  N'UN Sanctions', N'Fictional individual sanctions record for local testing.',       CONVERT(bit, 1)),
    (N'Entity',     N'Global Horizon Trading', CONVERT(date, '2009-03-27'), N'United Arab Emirates', N'Emirati', N'DEMO-UN-002',  N'UN Sanctions', N'Fictional organization sanctions record for local testing.',     CONVERT(bit, 1)),
    (N'Vessel',     N'MV Ocean Crest',    NULL,                        N'Panama',           N'Panamanian',      N'DEMO-UN-003',  N'UN Sanctions', N'Fictional vessel sanctions record for local testing.',           CONVERT(bit, 1))
) v (RecordType, FullName, DateOfBirth, Country, Nationality, ReferenceNo, SourceName, Remarks, IsActive)
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.AmlRecords existing
    WHERE existing.ReferenceNo = v.ReferenceNo
);

SELECT Id, ReferenceNo, FullName, RecordType, SourceName, IsActive
FROM dbo.AmlRecords
WHERE ReferenceNo LIKE N'DEMO-%'
ORDER BY SourceName, ReferenceNo;
