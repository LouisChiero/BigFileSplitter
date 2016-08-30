using System;

namespace BigFileSplitter
{
    [Flags]
    public enum SplitStatusMessage
    {
        Success = 0,
        Cancelled = 1,
        IOError = 2,
        UnauthorizedAccessError = 4,
        PathTooLongError = 8,
        DirectoryNotFoundError = 16,
        NotSupportedError = 32,
        UnknownError = 64        
    }
}
