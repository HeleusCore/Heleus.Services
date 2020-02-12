namespace Heleus.Messages
{
    public class PushServicePongMessage : PushServiceMessage
    {
        public PushServicePongMessage() : base(PushServiceMessageTypes.Pong)
        {
        }

        public PushServicePongMessage(int chainId, int clientId) : base(PushServiceMessageTypes.Pong, chainId, clientId)
        {
        }
    }
}
