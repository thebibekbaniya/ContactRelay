namespace ContactRelay.Models;

public sealed record ContactSyncStateUpdate(
    Guid SourceUserObjectId,
    string ExchangeContactId,
    string FieldHash
);
