using System;
using System.IO;

namespace Heleus.ServiceHelper
{
    public enum ImageInfoResult
	{
        InvalidFormat,
        InvalidFileSize,
        Jpg,
        Png
	}

    public class ImageInfo
	{
        public static ImageInfo InvalidFormat = new ImageInfo(ImageInfoResult.InvalidFormat, 0, 0, 0);
        public static ImageInfo InvalidFileSize = new ImageInfo(ImageInfoResult.InvalidFileSize, 0, 0, 0);

        public readonly ImageInfoResult Result;
		public readonly int Width;
		public readonly int Height;

        public readonly long Filesize;

        public bool IsValid => Result != ImageInfoResult.InvalidFormat && Result != ImageInfoResult.InvalidFileSize && Width > 0 && Height > 0;
        public bool IsInvalidFormat => Result == ImageInfoResult.InvalidFormat;
        public bool IsInvalidFileSize => Result == ImageInfoResult.InvalidFileSize;

        public ImageInfo(ImageInfoResult result, int width, int height, long fileSize)
		{
			Result = result;
			Width = width;
			Height = height;
            Filesize = fileSize;
        }
	}

    public static class Image
    {
		static ushort ReadUshortBigEndian(Stream stream)
		{
			if (stream.Position == stream.Length)
				return 0;

			var a = (ushort)stream.ReadByte();
			var b = (ushort)stream.ReadByte();

			return (ushort)(b | (a << 8));
		}

		static uint ReadUintBigEndian(Stream stream)
		{
			if (stream.Position == stream.Length)
				return 0;

			var a = (uint)stream.ReadByte();
			var b = (uint)stream.ReadByte();
			var c = (uint)stream.ReadByte();
			var d = (uint)stream.ReadByte();

			return d | c << 8 | b << 16 | a << 24;
		}

		static byte ReadByte(Stream stream)
		{
			var data = stream.ReadByte();
			if (data < 0)
				return 0;

			return (byte)data;
		}

		static ImageInfo IsValidPng(Stream stream)
		{
			if (0x0d0a1a0a != ReadUintBigEndian(stream))
				goto invalid;

			// chunk size
			if (13 != ReadUintBigEndian(stream))
                goto invalid;

            // chunk IHDR
            if (0x49484452 != ReadUintBigEndian(stream))
                goto invalid;

            var width = ReadUintBigEndian(stream);
			var height = ReadUintBigEndian(stream);
			//var depth = ReadByte(stream);
			//var colorType = ReadByte(stream);

            return new ImageInfo(ImageInfoResult.Png, (int)width, (int)height, stream.Length);

        invalid:
            return ImageInfo.InvalidFormat;
		}

		static ImageInfo IsValidJpg(Stream stream)
		{
			//if (0xffe0 != ReadUshortBigEndian(stream))
            //    goto invalid;

            var headerSize = ReadUshortBigEndian(stream);

			if (0x4a464946 != ReadUintBigEndian(stream)) // "JFXX"
                goto invalid;
            if (0 != ReadByte(stream))
                goto invalid;

            var lastStartPosition = stream.Position = 2 + headerSize + 2;
			var count = 0;
			while (stream.Position < stream.Length)
			{
				var start = ReadByte(stream);
				if (start != 0xff)
                    goto invalid;

                var marker = ReadByte(stream);
				var size = ReadUshortBigEndian(stream);

				if (marker == 0xc0)
				{
					ReadByte(stream); // precision
					var width = ReadUshortBigEndian(stream);
					var height = ReadUshortBigEndian(stream);

                    return new ImageInfo(ImageInfoResult.Jpg, width, height, stream.Length);
				}

				lastStartPosition = stream.Position = lastStartPosition + 2 + size;
				count++;

				if (count > 10)
					goto invalid;
			}

        invalid:
			return ImageInfo.InvalidFormat;
		}

		public static ImageInfo IsValidImage(string filePath, long maxFileSize)
		{
			try
			{
				using (var stream = new FileStream(filePath, FileMode.Open))
				{
                    if (stream.Length > maxFileSize)
                        return ImageInfo.InvalidFileSize;

                    var magic = ReadUintBigEndian(stream);

					//if (0xffd8 == ReadUshortBigEndian(stream))
					//	return IsValidJpg(stream);
                    if(0xffd8ffe0 == magic)
                        return IsValidJpg(stream);

                    // header
                    //stream.Position = 0;
                    if (0x89504e47 == magic)
						return IsValidPng(stream);
				}
			}
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
			catch (Exception)
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
			{

			}

			return ImageInfo.InvalidFormat;
		}
	}
}
