namespace RavenfallOfficial.Core.Twitch
{
    public interface ITwitchUserStore
    {
        ITwitchUser Get(string username);
    }
}