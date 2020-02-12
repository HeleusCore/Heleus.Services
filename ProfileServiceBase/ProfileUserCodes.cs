namespace Heleus.ProfileService
{
    public enum ProfileUserCodes
    {
        Ok = 100,

        InvalidTransaction = 110,
        InvalidAttachements = 120,

        InvalidImage = 121,
        InvalidImageFileSize = 122,
        InvalidImageDimensions = 123,

        InvalidProfileJsonFizeSize = 124,
        InvalidProfileJson = 125,

        InvalidRealName = 130,
        InvalidProfileName = 131,
        ProfileNameInUse = 132
    }
}
