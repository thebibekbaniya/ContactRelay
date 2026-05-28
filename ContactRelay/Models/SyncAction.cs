namespace ContactRelay.Models;

public enum SyncAction
{
    None = 0,
    Create = 1,
    Update = 2,
    Delete = 3,
    Skip = 4,
    Error = 5,
    Cleanup = 6
}
