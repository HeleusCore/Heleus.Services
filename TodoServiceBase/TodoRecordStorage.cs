using Heleus.Base;
using Heleus.Network.Client.Record;

namespace Heleus.TodoService
{
    public class TodoRecordStorage<T> : RecordStorage<T> where T : Record
    {
        public readonly long TargetedTransactionId;
        public readonly long GroupId;

        public TodoRecordStorage(T record, long transactionId, long accountId, long timestamp, long targetedTransactionId, long groupId) : base(record, transactionId, accountId, timestamp)
        {
            TargetedTransactionId = targetedTransactionId;
            GroupId = groupId;
        }

        public TodoRecordStorage(Unpacker unpacker) : base(unpacker)
        {
            unpacker.Unpack(out TargetedTransactionId);
            unpacker.Unpack(out GroupId);
        }

        public override void Pack(Packer packer)
        {
            base.Pack(packer);

            packer.Pack(TargetedTransactionId);
            packer.Pack(GroupId);
        }
    }
}
