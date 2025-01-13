CREATE TABLE ApiKeys (
    Id NVARCHAR(50) PRIMARY KEY,
    AccountId NVARCHAR(50) NOT NULL,
    KeyHash NVARCHAR(100) NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    Environment NVARCHAR(20) NOT NULL,
    RateLimit INT NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    ExpiresAt DATETIME2 NULL,
    RevokedAt DATETIME2 NULL,
    Status INT NOT NULL,
    CONSTRAINT UQ_ApiKeys_KeyHash UNIQUE (KeyHash)
);

CREATE TABLE ApiKeyPermissions (
    ApiKeyId NVARCHAR(50) NOT NULL,
    Permission NVARCHAR(100) NOT NULL,
    CONSTRAINT PK_ApiKeyPermissions PRIMARY KEY (ApiKeyId, Permission),
    CONSTRAINT FK_ApiKeyPermissions_ApiKeys FOREIGN KEY (ApiKeyId) 
        REFERENCES ApiKeys(Id) ON DELETE CASCADE
);

CREATE INDEX IX_ApiKeys_AccountId ON ApiKeys(AccountId);
CREATE INDEX IX_ApiKeys_KeyHash ON ApiKeys(KeyHash);
CREATE INDEX IX_ApiKeys_Status ON ApiKeys(Status); 