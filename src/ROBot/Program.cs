using System;
using RavenfallOfficial.Core;
using RavenfallOfficial.Core.Handlers;
using RavenfallOfficial.Core.Twitch;
using ROBot;

namespace RavenfallOfficial
{
    class Program
    {
        static void Main(string[] args)
        {
            var ioc = new IoC();

            ioc.RegisterCustomShared<IoC>(() => ioc);
            ioc.RegisterCustomShared<IAppSettings>(() => new AppSettingsProvider().Get());
            ioc.RegisterShared<ILogger, ConsoleLogger>();
            ioc.RegisterShared<IKernel, Kernel>();
            ioc.RegisterShared<IApplication, App>();

            ioc.RegisterShared<IMessageBus, MessageBus>();

            // Twitch stuff
            ioc.RegisterShared<ITwitchUserStore, TwitchUserStore>();
            ioc.RegisterShared<ITwitchCredentialsProvider, TwitchCredentialsProvider>();
            ioc.RegisterShared<ITwitchCommandController, TwitchCommandController>();
            ioc.RegisterShared<ITwitchCommandClient, TwitchCommandClient>();

            // YouTube live stuff
            // ... to be added :)

            var app = ioc.Resolve<IApplication>();
            {
                app.Run();
                while (true)
                {
                    if (Console.ReadKey().Key == ConsoleKey.Q)
                    {
                        break;
                    }
                }
                app.Shutdown();
            }
        }
    }
}
