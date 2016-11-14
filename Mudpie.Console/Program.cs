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

    using Mudpie.Scripting.Common;

    using Network;

    using Scripting;

    using StackExchange.Redis.Extensions.Core;
    using StackExchange.Redis.Extensions.Newtonsoft;

    public sealed class Program
    {
        /// <summary>
        /// The logging utility instance to use to log events from this class
        /// </summary>
        private static readonly ILog _Logger = LogManager.GetLogger(typeof(Program));

        /// <summary>
        /// Entry point of the application
        /// </summary>
        /// <param name="args">Command line arguments for the console application</param>
        public static void Main(string[] args)
        {
            // Setup LOG4NET
            XmlConfigurator.Configure();

            // Load configuration
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var mudpieConfigurationSection = (MudpieConfigurationSection)config.GetSection("mudpie");
            _Logger.InfoFormat("Loaded configuration from {0}", config.FilePath);

            var godPlayer = new Player
                                {
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

                            await redis.Database.StringSetAsync("mudpie::dbref:counter", 1);

                            var voidId = new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1).ToString("N");
                            var voidRoom = new Room
                                               {
                                                   DbRef = 1,
                                                   InternalId = voidId,
                                                   Name = "The Void",
                                                   Description = "An infinite emptiness of nothing."
                                               };

                            await redis.SetAddAsync<string>("mudpie::rooms", voidRoom.DbRef);
                            await redis.AddAsync($"mudpie::room:{voidRoom.DbRef}", voidRoom);

                            var godPassword = new SecureString();
                            foreach (var c in "god")
                                godPassword.AppendChar(c);
                            godPlayer.SetPassword(godPassword);
                            await redis.SetAddAsync<string>("mudpie::players", godPlayer.DbRef);
                            await redis.AddAsync($"mudpie::player:{godPlayer.DbRef}", godPlayer);

                            await redis.HashSetAsync("mudpie::usernames", godPlayer.Username.ToLowerInvariant(), godPlayer.DbRef);
                        }
                        else
                        {
                            // Ensure we can read Void and God
                            var voidRoom = redis.Get<Room>($"mudpie::room:{(DbRef)1}");
                            Debug.Assert(voidRoom != null);

                            godPlayer = Player.Get(redis, 2);
                            Debug.Assert(godPlayer != null);
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
                var scriptEngineTask = Task.Run(async () =>
                    {
                        Debug.Assert(mudpieConfigurationSection != null, "mudpieConfigurationSection != null");
                        foreach (var dir in mudpieConfigurationSection.Directories)
                        foreach (var file in System.IO.Directory.GetFiles(dir.Directory))
                        {
                            Debug.Assert(file != null, "file != null");
                            using (var sr = new System.IO.StreamReader(file))
                            {
                                var contents = await sr.ReadToEndAsync();
                                var interactive = contents.IndexOf("// MUDPIE::INTERACTIVE", StringComparison.InvariantCultureIgnoreCase) > -1;
                                await scriptingEngine.SaveProgramAsync(new Data.Program(System.IO.Path.GetFileNameWithoutExtension(file), contents, dir.Unauthenticated)
                                                                           {
                                                                               Interactive = interactive
                                                                           });
                                sr.Close();
                            }
                        }

                        //await engine.SaveProgramAsync(new Data.Program("test", "int x = 1;int y = 2;return x + y;"));
                    //var result = await engine.RunProgramAsync<int>("test", godPlayer);
                    //Console.WriteLine(result.ReturnValue);
                    });
                scriptEngineTask.Wait();

                // Listen for connections
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
