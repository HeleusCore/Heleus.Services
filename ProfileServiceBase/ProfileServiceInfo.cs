using System;
using Heleus.Chain;

namespace Heleus.ProfileService
{
    public static class ProfileServiceInfo
    {
        public const long Version = 1; 
        public const string Name = "Profile Service";

        public const int ChainId = 2;
        public const uint ChainIndex = 0;
        public static readonly Uri EndPoint = new Uri("https://heleusnode.heleuscore.com");

        public const int MinSearchLength = 4;

        public const int MinNameLength = 2;
        public const int MaxNameLength = 31;

        public const string ProfileJsonFileName = "profile.json";
        public const int ProfileJsonMaxFileSize = 1024 * 10;

        public const string ImageFileName = "profile.png";
        public const int ImageMaxFileSize = 1024 * 128;
        public const int ImageMaxDimensions = 512;

        public static Index ProfileIndex = Index.New().Add((short)10).Build();
        public static Index ImageIndex = Index.New().Add((short)11).Build();
        public static Index ProfileAndImageIndex = Index.New().Add((short)12).Build();

        public static bool IsRealNameValid(string realName)
        {
            return !string.IsNullOrEmpty(realName) && realName.Length >= MinNameLength && realName.Length <= MaxNameLength;
        }

        static bool IsValidProfileNameCharacter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
        }

        public static string ToValidProfileName(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
                return string.Empty;

            profileName = profileName.ToLower();
            var result = string.Empty;
            foreach (char c in profileName)
            {
                if (result.Length > MaxNameLength)
                    break;

                if (IsValidProfileNameCharacter(c))
                {
                    result += c;
                }
            }

            return result;
        }

        public static bool IsProfileNameValid(string profileName)
        {
            if(!string.IsNullOrEmpty(profileName) && profileName.Length >= MinNameLength && profileName.Length <= MaxNameLength)
            {
                foreach(var c in profileName)
                {
                    if (!IsValidProfileNameCharacter(c))
                        return false;
                }
                return true;
            }
            return false;
        }
    }
}
