namespace ContactRelay.Mapping;

/// <summary>
/// Identifiers for the hidden MAPI single-value extended properties used to store
/// ContactRelay sync metadata on managed contacts. These properties are not visible
/// in Outlook or OWA, unlike the contact's personal notes field that earlier versions
/// used. All properties live under a private property set GUID so they never collide
/// with other named properties in the mailbox.
/// </summary>
public static class ContactMetadata
{
    // Private property set GUID for ContactRelay metadata. Do not change once contacts
    // exist in production — the identifiers below would no longer match stored values.
    private const string PropertySetGuid = "{b6f3c2a1-9d4e-4f7a-8c1b-2e3d4f5a6b7c}";

    public const string ManagedMarkerPropertyId = $"String {PropertySetGuid} Name ManagedBy";
    public const string SourceUserObjectIdPropertyId = $"String {PropertySetGuid} Name SourceUserObjectId";
    public const string FieldHashPropertyId = $"String {PropertySetGuid} Name FieldHash";

    /// <summary>
    /// $expand clause that returns the ContactRelay metadata properties on a contacts query.
    /// </summary>
    public static string ExpandClause =>
        "singleValueExtendedProperties($filter=" +
        $"id eq '{ManagedMarkerPropertyId}' or " +
        $"id eq '{SourceUserObjectIdPropertyId}' or " +
        $"id eq '{FieldHashPropertyId}')";
}
