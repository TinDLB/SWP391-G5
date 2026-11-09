-- Run once on an existing SQL Server database that was created before Gender was added.
IF COL_LENGTH('dbo.Users', 'Gender') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD Gender NVARCHAR(20) NULL;
END;
GO
