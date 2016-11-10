namespace Mudpie.Console
{
    using System;
    using System.Configuration;
    using System.Linq;
    using System.Security;
    using System.Threading.Tasks;

    using log4net.Config;

    using Configuration;
    using Data;

    using log4net;

    using Mudpie.Console.Scripting;

    using Network;

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
        /// <param name="args">Command line arguments for the console application</param>
        public static void Main(string[] args)
        {
            // Setup LOG4NET
            XmlConfigurator.Configure();

            // Load configuration
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var mudpieConfigurationSection = (MudpieConfigurationSection)config.GetSection("mudpie");
            Logger.InfoFormat("Loaded configuration from {0}", config.FilePath);
            
            var godId = new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1).ToString("N");
            var godPlayer = new Player
            {
                Id = godId,
                Name = "God",
                Username = "god"
            };

            using (var redis = new StackExchangeRedisCacheClient(new NewtonsoftSerializer()))
            {
                // Seed data
                Task.Run(async () =>
                    {
                        if (!await redis.ExistsAsync("mudpie::rooms"))
                        {
                            Console.WriteLine("Redis database is not seeded with any data.  Creating seed data...");
                            var voidId = Guid.Empty.ToString("N");
                            var voidRoom = new Room
                            {
                                Id = voidId,
                                Name = "The Void",
                                Description = "An infinite emptiness of nothing."
                            };

                            await redis.SetAddAsync("mudpie::rooms", voidId);
                            await redis.AddAsync($"mudpie::room:{voidId}", voidRoom);

                            var godPassword = new SecureString();
                            foreach (var c in "god")
                                godPassword.AppendChar(c);
                            godPlayer.SetPassword(godPassword);
                            await redis.SetAddAsync("mudpie::players", godId);
                            await redis.AddAsync($"mudpie::player:{godId}", godPlayer);
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
                    foreach (var dir in mudpieConfigurationSection.Directories)
                        foreach (var file in System.IO.Directory.GetFiles(dir.Directory))
                            using (var sr = new System.IO.StreamReader(file))
                            {
                                var contents = await sr.ReadToEndAsync();
                                await scriptingEngine.SaveProgramAsync(new Data.Program(System.IO.Path.GetFileNameWithoutExtension(file), contents, dir.Unauthenticated));
                                sr.Close();
                            }
                    
                    //await engine.SaveProgramAsync(new Data.Program("test", "int x = 1;int y = 2;return x + y;"));
                    //var result = await engine.RunProgramAsync<int>("test", godPlayer);
                    //Console.WriteLine(result.ReturnValue);
                });
                scriptEngineTask.Wait();

                // Listen for connections
                var server = new Server(mudpieConfigurationSection.Ports.Select(p => p.Port).ToArray(), scriptingEngine);
                server.Start();


                System.Console.ReadLine();
            }
        }
    }
}
