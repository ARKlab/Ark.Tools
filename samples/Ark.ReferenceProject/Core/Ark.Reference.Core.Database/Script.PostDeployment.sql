/*
Post-Deployment Script Template							
--------------------------------------------------------------------------------------
 This file contains SQL statements that will be appended to the build script.		
 Use SQLCMD syntax to include a file in the post-deployment script.			
 Example:      :r .\myfile.sql								
 Use SQLCMD syntax to reference a variable in the post-deployment script.		
 Example:      :setvar TableName MyTable							
               SELECT * FROM [$(TableName)]					
--------------------------------------------------------------------------------------
*/

:r .\obj\ArkCompliance.Sql\CompliancePolicies.sql

IF DATABASE_PRINCIPAL_ID(N'ComplianceMaskedReader') IS NULL
    CREATE USER [ComplianceMaskedReader] WITHOUT LOGIN;

IF DATABASE_PRINCIPAL_ID(N'CompliancePrivilegedReader') IS NULL
    CREATE USER [CompliancePrivilegedReader] WITHOUT LOGIN;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.database_role_members AS drm
    INNER JOIN sys.database_principals AS role_principal
        ON role_principal.principal_id = drm.role_principal_id
    INNER JOIN sys.database_principals AS user_principal
        ON user_principal.principal_id = drm.member_principal_id
    WHERE role_principal.name = N'db_datareader'
      AND user_principal.name = N'ComplianceMaskedReader'
)
    ALTER ROLE [db_datareader] ADD MEMBER [ComplianceMaskedReader];

IF NOT EXISTS
(
    SELECT 1
    FROM sys.database_role_members AS drm
    INNER JOIN sys.database_principals AS role_principal
        ON role_principal.principal_id = drm.role_principal_id
    INNER JOIN sys.database_principals AS user_principal
        ON user_principal.principal_id = drm.member_principal_id
    WHERE role_principal.name = N'db_datareader'
      AND user_principal.name = N'CompliancePrivilegedReader'
)
    ALTER ROLE [db_datareader] ADD MEMBER [CompliancePrivilegedReader];

GRANT UNMASK TO [CompliancePrivilegedReader];

EXEC [ops].[InitConfig]