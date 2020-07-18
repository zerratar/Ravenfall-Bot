using TwitchLib.Client.Models;

namespace RavenfallOfficial.Core
{
    public interface ITwitchCredentialsProvider
    {
        ConnectionCredentials Get();
    }
}