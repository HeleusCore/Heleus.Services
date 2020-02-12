using Heleus.Network.Client.Record;

namespace Heleus.MessageService
{
    public static class MessageRecordExtenstions
    {
        public static MessageRecordTypes GetMessageRecordType(this Record record)
        {
            return (MessageRecordTypes)record.RecordType;
        }

        public static MessageRecordTypes GetMessageRecordType<T>(this RecordStorage<T> storage) where T : Record
        {
            return (MessageRecordTypes)storage.RecordType;
        }

        public static MessageRecordTypes GetMessageRecordType(this IRecordStorage storage)
        {
            return (MessageRecordTypes)storage.RecordType;
        }
    }
}
