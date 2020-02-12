using System;
using Heleus.Base;
using Heleus.Network.Client.Record;

namespace Heleus.NoteService
{
    public enum NoteRecordTypes
    {
        Note
    }

    public class NoteRecord : Record
    {
        public readonly string Note;

        public NoteRecord(string note) : base((ushort)NoteRecordTypes.Note)
        {
            Note = note;
        }

        public NoteRecord(Unpacker unpacker) : this(unpacker.UnpackString())
        {

        }

        public override void Pack(Packer packer)
        {
            packer.Pack(Note);
        }
    }

    public static class NoteRecordExtenstions
    {
        public static NoteRecordTypes GetMessageRecordType(this Record record)
        {
            return (NoteRecordTypes)record.RecordType;
        }

        public static NoteRecordTypes GetMessageRecordType<T>(this RecordStorage<T> storage) where T : Record
        {
            return (NoteRecordTypes)storage.RecordType;
        }

        public static NoteRecordTypes GetMessageRecordType(this IRecordStorage storage)
        {
            return (NoteRecordTypes)storage.RecordType;
        }
    }
}
