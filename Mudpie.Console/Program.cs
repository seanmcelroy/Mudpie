namespace Mudpie.Console
{
    using System;
    using System.Configuration;
    using System.Diagnostics;
    using System.Linq;
    using System.Security;
    using System.Threading.Tasks;

    using Configuration;

    using Data;

    using log4net;
    using log4net.Config;

    using Network;

    using Scripting;

    using StackExchange.Redis.Extensions.Core;
    using StackExchange.Redis.Extensions.Newtonsoft;

    public sealed class Program
    {
        /// <summary>
        /// The logging utility instance to use to log events from this class
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Program));

        /// <summary>
        /// Entry point of the application
        /// </summary>
        public static void Main()
        {
            // Setup LOG4NET
            XmlConfigurator.Configure();

            // Load configuration
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var mudpieConfigurationSection = (MudpieConfigurationSection)config.GetSection("mudpie");
            Debug.Assert(mudpieConfigurationSection != null, "mudpieConfigurationSection != null");
            Logger.InfoFormat("Loaded configuration from {0}", config.FilePath);

            var godPlayer = new Player
                                {
                                    Aliases = new[] { "Jehovah", "Yahweh", "Allah" },
                                    DbRef = 2,
                                    Description = "The Creator",
                                    InternalId = new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2).ToString("N"),
                                    Name = "God",
                                    Username = "god",
                                    Location = 1
                                };

            using (var redis = new StackExchangeRedisCacheClient(new NewtonsoftSerializer()))
            {
                Debug.Assert(redis != null, "redis != null");

                // Seed data
                Task.Run(async () =>
                    {
                        if (!await redis.ExistsAsync("mudpie::dbref:counter"))
                        {
                            Console.WriteLine("Redis database is not seeded with any data.  Creating seed data...");

                            await redis.Database.StringSetAsync("mudpie::dbref:counter", 4);

                            // VOID
                            var voidRoom = new Room
                                               {
                                                   DbRef = 1,
                                                   InternalId = new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1).ToString("N"),
                                                   Name = "The Void",
                                                   Description = "An infinite emptiness of nothing."
                                               };
                            await voidRoom.SaveAsync(redis);

                            // GOD
                            var godPassword = new SecureString();
                            foreach (var c in "god")
                                godPassword.AppendChar(c);
                            godPlayer.SetPassword(godPassword);
                            await godPlayer.SaveAsync(redis);

                            // LOOK
                            var lookProgramSource = await Data.Program.GetSourceCodeLinesAsync(mudpieConfigurationSection, "look.mcs");
                            Debug.Assert(lookProgramSource != null, "lookProgramSource != null");
                            var lookProgram = new Data.Program("look.msc", lookProgramSource)
                                                  {
                                                      DbRef = 3,
                                                      Description = "A program used to observe your surroundings",
                                                      Name = "look.msc",
                                                      Interactive = false
                                                  };
                            await lookProgram.SaveAsync(redis);

                            // LINK-LOOK-ROOM
                            var linkLook = new Link
                                               {
                                                    DbRef = 4,
                                                   Name = "look",
                                                   Aliases = new[] { "l" },
                                                   Location = voidRoom.DbRef,
                                                   Target = 3
                                               };
                            await linkLook.MoveAsync(voidRoom.DbRef, redis);
                        }
                        else
                        {
                            // Ensure we can read Void and God
                            var voidRoom = await Room.GetAsync(redis, 1);
                            Debug.Assert(voidRoom != null, "voidRoom != null");

                            godPlayer = await Player.GetAsync(redis, 2);
                            Debug.Assert(godPlayer != null, "godPlayer != null");

                            var godComposed = await CacheManager.LookupOrRetrieveAsync(2, redis, async d => await Player.GetAsync(redis, d));
                            Debug.Assert(godComposed != null, "godComposed != null");
                        }

                        /*else
                        {
                            Console.WriteLine("Redis database is seeded with data.  BLOWING IT AWAY DATA...");

                            System.Console.WriteLine("Deleting ROOMS...");
                            foreach (var roomId in await redis.SetMembersAsync<string>("mudpie::rooms"))
                                await redis.RemoveAsync($"mudpie::room:{roomId}");
                            await redis.RemoveAsync("mudpie::rooms");

                            System.Console.WriteLine("Deleting USERS...");
                            foreach (var playerId in await redis.SetMembersAsync<string>("mudpie::players"))
                                await redis.RemoveAsync($"mudpie::player:{playerId}");
                            await redis.RemoveAsync("mudpie::players");
                        }*/

                        return 0;
                    }).Wait();

                // Setup scripting engine
                var scriptingEngine = new Engine(redis);

                // Listen for connections
                Debug.Assert(mudpieConfigurationSection.Ports != null, "mudpieConfigurationSection.Ports != null");
                var server = new Server(mudpieConfigurationSection.Ports.Select(p => p.Port).ToArray(), scriptingEngine)
                                 {
                                     ShowBytes = true,
                                     ShowCommands = false,
                                     ShowData = true
                                 };
                server.Start();

                Console.ReadLine();
            }
        }
    }
}
