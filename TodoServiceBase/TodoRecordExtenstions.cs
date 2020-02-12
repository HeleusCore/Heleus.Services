using Heleus.Network.Client.Record;

namespace Heleus.TodoService
{
    public static class TodoRecordExtenstions
    {
        public static TodoRecordTypes GetRecordType(this Record record)
        {
            return (TodoRecordTypes)record.RecordType;
        }

        public static TodoRecordTypes GetRecordType<T>(this RecordStorage<T> storage) where T : Record
        {
            return (TodoRecordTypes)storage.RecordType;
        }

        public static TodoRecordTypes GetRecordType(this IRecordStorage storage)
        {
            return (TodoRecordTypes)storage.RecordType;
        }
    }
}
