namespace Heleus.StatusService
{
    public enum ServiceUserCodes
    {
        None = 0,

        InvalidTransaction = 100,
        InvalidAttachementItems = 101,

        InvalidStatusJson = 110,
        InvalidStatusMessageLength = 111,
        InvalidStatusLink = 112,

        InvalidImageFormat = 120,
        InvalidImageDimensions = 121,
        InvalidImageFileSize = 122,
    }
}
