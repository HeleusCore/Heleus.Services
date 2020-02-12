using System;
using Heleus.Chain;

namespace Heleus.NoteService
{
    public static class NoteServiceInfo
    {
        public const long Version = 1;
        public const string Name = "Note Service";

        public const int ChainId = 4;
        public const uint ChainIndex = 0;
        public static readonly Uri EndPoint = new Uri("https://servicenode.heleuscore.com");

        public const string NoteFileName = "note.data";
        public const int NoteMaxFileSize = 1024 * 10;

        public static Index NoteIndex = Index.New().Add((short)10).Build();
    }
}
