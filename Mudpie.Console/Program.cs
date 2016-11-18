namespace Mudpie.Console
{
    using System;
    using System.Configuration;
    using System.Diagnostics;
    using System.Linq;
    using System.Security;
    using System.Threading.Tasks;

    using Configuration;

    using log4net;
    using log4net.Config;

    using Network;

    using Scripting;

    using Server.Data;

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

            var godPlayer = new Player("God", 2, "god")
                                {
                                    Aliases = new[] { "Jehovah", "Yahweh", "Allah" },
                                    DbRef = 2,
                                    Description = "The Creator",
                                    Location = 1
                                };
            
            using (var redis = new StackExchangeRedisCacheClient(new NewtonsoftSerializer()))
            {
                Debug.Assert(redis != null, "redis != null");
                
                // Setup scripting engine
                var scriptingEngine = new Engine(redis);

                // Setup server process
                Debug.Assert(mudpieConfigurationSection.Ports != null, "mudpieConfigurationSection.Ports != null");
                var server = new Server(mudpieConfigurationSection.Ports.Select(p => p.Port).ToArray(), scriptingEngine)
                {
                    ShowBytes = true,
                    ShowCommands = false,
                    ShowData = true
                };

                // Seed data
                Task.Run(async () =>
                    {
                        if (!await redis.ExistsAsync("mudpie::dbref:counter"))
                        {
                            Console.WriteLine("Redis database is not seeded with any data.  Creating seed data...");

                            // VOID
                            var voidRoom = new Room("The Void", godPlayer.DbRef)
                                               {
                                                   DbRef = 1,
                                                   Description = "An infinite emptiness of nothing."
                                               };
                            await voidRoom.SaveAsync(redis, server.CancellationToken);

                            // GOD
                            var godPassword = new SecureString();
                            foreach (var c in "god")
                            {
                                godPassword.AppendChar(c);
                            }

                            godPlayer.SetPassword(godPassword);
                            await godPlayer.SaveAsync(redis, server.CancellationToken);

                            // LOOK
                            {
                                var lookProgramSource = await SourceUtility.GetSourceCodeLinesAsync(mudpieConfigurationSection, "look.mcs");
                                Debug.Assert(lookProgramSource != null, "lookProgramSource != null");
                                var lookProgram = new Mudpie.Server.Data.Program("look.msc", godPlayer.DbRef, lookProgramSource)
                                                      {
                                                          DbRef = 3,
                                                          Description = "A program used to observe your surroundings",
                                                          Interactive = false
                                                      };
                                await lookProgram.SaveAsync(redis, server.CancellationToken);

                                // LINK-LOOK-ROOM
                                var linkLook = new Link("look", godPlayer.DbRef)
                                                   {
                                                       DbRef = 4,
                                                       Aliases = new[] { "l" },
                                                       Target = 3
                                                   };
                                await linkLook.MoveAsync(voidRoom.DbRef, redis, server.CancellationToken);
                                var void1 = ObjectBase.GetAsync(redis, voidRoom.DbRef, server.CancellationToken).Result;
                                if (void1 == null)
                                {
                                    throw new InvalidOperationException("void1 cannot be null");
                                }

                                Debug.Assert(void1.Contents?.Length == 1, "After reparenting, VOID should have 1 content");
                            }

                            var nextAvailableDbRef = 5;
                            var registerProgramToVoid = new Func<string, string, int, ICacheClient, Task<int>>(async (name, desc, nextDbRef, cacheClient) =>
                                                            {
                                                                var source = await SourceUtility.GetSourceCodeLinesAsync(mudpieConfigurationSection, name);
                                                                Debug.Assert(source != null, "nameProgramSource != null");
                                                                var nameProgram = new Mudpie.Server.Data.Program(name, godPlayer.DbRef, source)
                                                                {
                                                                    DbRef = nextDbRef,
                                                                    Description = "A program used to rename objects",
                                                                    Interactive = false
                                                                };
                                                                await nameProgram.SaveAsync(cacheClient, server.CancellationToken);

                                                                // LINK-NAME-ROOM
                                                                var linkName = new Link(System.IO.Path.GetFileNameWithoutExtension(name), godPlayer.DbRef)
                                                                {
                                                                    DbRef = nextDbRef + 1,
                                                                    Target = nextDbRef
                                                                };
                                                                await linkName.MoveAsync(voidRoom.DbRef, cacheClient, server.CancellationToken);
                                                                var void2 = ObjectBase.GetAsync(cacheClient, voidRoom.DbRef, server.CancellationToken).Result;
                                                                if (void2 == null)
                                                                {
                                                                    throw new InvalidOperationException("void2 cannot be null");
                                                                }

                                                                return nextDbRef + 2;
                                                            });

                            // @NAME
                            nextAvailableDbRef = await registerProgramToVoid.Invoke("@dig.msc", "Creates new rooms", nextAvailableDbRef, redis);
                            nextAvailableDbRef = await registerProgramToVoid.Invoke("@name.msc", "Rename objects", nextAvailableDbRef, redis);

                            await redis.Database.StringSetAsync("mudpie::dbref:counter", nextAvailableDbRef);
                        }
                        else
                        {
                            // Ensure we can read Void and God
                            var voidRoom = await Room.GetAsync(redis, 1, server.CancellationToken);
                            Debug.Assert(voidRoom != null, "voidRoom != null");

                            godPlayer = await Player.GetAsync(redis, 2, server.CancellationToken);
                            Debug.Assert(godPlayer != null, "godPlayer != null");

                            var godComposed = await CacheManager.LookupOrRetrieveAsync<Player>(2, redis, async (d, token) => await Player.GetAsync(redis, d, token), server.CancellationToken);
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

                // Listen for connections
                server.Start();

                Console.ReadLine();
            }
        }
    }
}
