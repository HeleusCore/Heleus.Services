namespace Heleus.Messages
{
    public class PushServicePingMessage : PushServiceMessage
    {
        public PushServicePingMessage() : base(PushServiceMessageTypes.Ping)
        {
        }

        public PushServicePingMessage(int chainId, int clientId) : base(PushServiceMessageTypes.Ping, chainId, clientId)
        {
        }
    }
}
