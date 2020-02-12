using System;
using System.Collections.Generic;
using System.IO;
using Heleus.Base;
using Heleus.Cryptography;

namespace Heleus.VerifyService
{
    public class VerifyJson
    {
        public string description;
        public string link;
        public List<VerifyFileJson> files;
    }

    public class VerifyFileJson
    {
        public string name;
        public long length;
        public string hash;
        public string hashtype;
        public string link;

        public Hash GetHash()
        {
            return GetHash(hash);
        }

        public static Hash GetHash(string hash)
        {
            // meh
            var data = Hex.FromString(hash);
            var hashdata = new byte[Hash.GetHashBytes(HashTypes.Sha512)];
            var padding = Hash.PADDING_BYTES;
            Buffer.BlockCopy(data, 0, hashdata, padding, data.Length);
            hashdata[0] = (byte)HashTypes.Sha512;

            return Hash.Restore(new ArraySegment<byte>(hashdata));
        }

        public static (Hash, long) GenerateHash(Stream stream)
        {
            try
            {
                var length = 0L;
                Hash hash = null;

                stream.Position = 0;
                length = stream.Length;
                hash = Hash.Generate(HashTypes.Sha512, stream);

                stream.Position = 0;
                var hash2 = Hash.Generate(HashTypes.Sha512, stream);
                if (hash == hash2)
                {
                    return (hash, length);
                }
            }
            catch { }

            return (null, 0L);
        }

        public static (Hash, long, string) GenerateHash(string filepath)
        {
            try
            {
                var length = 0L;
                Hash hash = null;

                using (var stream = File.OpenRead(filepath))
                {
                    length = stream.Length;
                    hash = Hash.Generate(HashTypes.Sha512, stream);
                }

                // hash two times
                using (var stream = File.OpenRead(filepath))
                {
                    var hash2 = Hash.Generate(HashTypes.Sha512, stream);
                    if (hash == hash2)
                    {
                        return (hash, length, Path.GetFileName(filepath));
                    }
                }
            }
            catch { }

            return (null, 0L, null);
        }

        public static bool CheckHash(string filepath, Hash hash, long lenth)
        {
            try
            {
                using (var stream = File.OpenRead(filepath))
                {
                    if (stream.Length != lenth)
                        return false;

                    var filehash = Hash.Generate(HashTypes.Sha512, stream);
                    if (hash == filehash)
                        return true;
                }

                // two tries
                using (var stream = File.OpenRead(filepath))
                {
                    var filehash = Hash.Generate(HashTypes.Sha512, stream);
                    if (hash == filehash)
                        return true;
                }
            }
            catch { }

            return false;
        }

        public static bool CheckHash(Stream stream, Hash hash, long length)
        {
            try
            {
                if (stream.Length != length)
                    return false;

                var filehash = Hash.Generate(HashTypes.Sha512, stream);
                if (hash == filehash)
                    return true;

                stream.Position = 0;
                // two tries
                filehash = Hash.Generate(HashTypes.Sha512, stream);
                if (hash == filehash)
                    return true;
            }
            catch { }

            return false;
        }
    }
}
