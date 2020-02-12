using Heleus.Base;
using Heleus.Service.Push;

namespace Heleus.Messages
{
    public class PushServiceTokenRemoveMessage : PushServiceMessage
    {
        public PushTokenInfo TokenInfo { get; private set; }

        public PushServiceTokenRemoveMessage() : base(PushServiceMessageTypes.PushTokenRemove)
        {
        }

        public PushServiceTokenRemoveMessage(PushTokenInfo pushToken, long requestCode, int chainId, int clientId) : base(PushServiceMessageTypes.PushTokenRemove, chainId, clientId)
        {
            TokenInfo = pushToken;
            SetRequestCode(requestCode);
        }

        protected override void Pack(Packer packer)
        {
            base.Pack(packer);
            packer.Pack(TokenInfo);
        }

        protected override void Unpack(Unpacker unpacker)
        {
            base.Unpack(unpacker);
            TokenInfo = new PushTokenInfo(unpacker);
        }
    }
}
