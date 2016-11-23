// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   The main application program loop
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console
{
    using System;
    using System.Configuration;
    using System.Diagnostics;
    using System.Linq;
    using System.Security;
    using System.Threading.Tasks;

    using Configuration;

    using JetBrains.Annotations;

    using log4net;
    using log4net.Config;

    using Mudpie.Scripting.Common;

    using Network;

    using Scripting;

    using Server.Data;

    using StackExchange.Redis.Extensions.Core;
    using StackExchange.Redis.Extensions.Newtonsoft;

    /// <summary>
    /// The main application program loop
    /// </summary>
    public sealed class Program
    {
        /// <summary>
        /// The logging utility instance to use to log events from this class
        /// </summary>
        [NotNull]
        // ReSharper disable once AssignNullToNotNullAttribute
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
                                    Properties = new[]
                                                     {
                                                         new Property
                                                             {
                                                                 Name = "_/de",
                                                                 Value = "The Creator",
                                                                 Owner = 2
                                                             }
                                                     },
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
                                                   Properties = new[]
                                                                    {
                                                                        new Property(Property.DESCRIPTION, "An infinite emptiness of nothing.", 2)
                                                                    }
                                               };
                            await voidRoom.SaveAsync(redis, server.CancellationToken);

                            // GOD
                            using (var godPassword = new SecureString())
                            {
                                foreach (var c in "god")
                                {
                                    godPassword.AppendChar(c);
                                }

                                godPlayer.SetPassword(godPassword);
                                godPassword.Clear();
                            }

                            await godPlayer.SaveAsync(redis, server.CancellationToken);

                            var nextAvailableDbRef = 3;
                            var registerProgramToVoid = new Func<string, string[], string, int, ICacheClient, Task<int>>(async (name, aliases, desc, nextDbRef, cacheClient) =>
                                                            {
                                                                var source = await SourceUtility.GetSourceCodeLinesAsync(mudpieConfigurationSection, name);
                                                                Debug.Assert(source != null, $"Unable to find source code for program {name}");
                                                                var nameProgram = new Mudpie.Server.Data.Program(name, godPlayer.DbRef, source)
                                                                {
                                                                    DbRef = nextDbRef,
                                                                    Properties = new[]
                                                                    {
                                                                        new Property(Property.DESCRIPTION, desc, godPlayer.DbRef)
                                                                    },
                                                                    Interactive = false
                                                                };
                                                                await nameProgram.SaveAsync(cacheClient, server.CancellationToken);

                                                                // LINK-NAME-ROOM
                                                                var linkName = new Link(System.IO.Path.GetFileNameWithoutExtension(name), godPlayer.DbRef)
                                                                {
                                                                    Aliases = aliases,
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
                            nextAvailableDbRef = await registerProgramToVoid.Invoke("@create.msc", new[] { "create", "make" }, "Creates new things", nextAvailableDbRef, redis);
                            nextAvailableDbRef = await registerProgramToVoid.Invoke("@describe.msc", new[] { "@desc", "describe" }, "Sets the description of an object", nextAvailableDbRef, redis);
                            nextAvailableDbRef = await registerProgramToVoid.Invoke("@dig.msc", new[] { "dig" }, "Creates new rooms", nextAvailableDbRef, redis);
                            nextAvailableDbRef = await registerProgramToVoid.Invoke("@name.msc", new[] { "rename" }, "Rename objects", nextAvailableDbRef, redis);
                            nextAvailableDbRef = await registerProgramToVoid.Invoke("inventory.msc", new[] { "i", "inv" }, "Lists the items you are carrying", nextAvailableDbRef, redis);
                            nextAvailableDbRef = await registerProgramToVoid.Invoke("look.msc", new[] { "l" }, "Displays information about your surroundings, or an object.", nextAvailableDbRef, redis);

                            await redis.Database.StringSetAsync("mudpie::dbref:counter", nextAvailableDbRef);
                        }
                        else
                        {
                            // Ensure we can read Void and God
                            var voidRoom = await Room.GetAsync(redis, 1, server.CancellationToken);
                            Debug.Assert(voidRoom != null, "voidRoom != null");

                            godPlayer = await Player.GetAsync(redis, 2, server.CancellationToken);
                            Debug.Assert(godPlayer != null, "godPlayer != null");

                            var godComposed = await CacheManager.LookupOrRetrieveAsync(2, redis, async (d, token) => await Player.GetAsync(redis, d, token), server.CancellationToken);
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

                Console.WriteLine("\r\nPress ~ to quit the server console");
                while (Console.ReadKey().Key != ConsoleKey.Oem3)
                {
                }

                Console.WriteLine("\r\nShutting down server...");
                server.Stop();
                Console.WriteLine("\r\nServer is shut down. End of line.");
            }
        }
    }
}
