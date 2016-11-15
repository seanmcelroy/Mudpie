namespace Mudpie.Console.Data
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Newtonsoft.Json;

    using StackExchange.Redis.Extensions.Core;

    public class ComposedObject
    {
        [JsonIgnore]
        public ObjectBase DataObject { get; private set; }

        [CanBeNull]
        [JsonIgnore]
        public ComposedObject Location { get; private set; }

        [CanBeNull]
        [JsonIgnore]
        public ReadOnlyCollection<ComposedObject> Contents { get; }

        [CanBeNull]
        [JsonIgnore]
        public ComposedObject Parent { get; private set; }

        public ComposedObject([NotNull] ICacheClient redis, [NotNull] ObjectBase dataObject)
        {
            this.DataObject = dataObject;

            var taskLocation = Task.Run(async () =>
                {
                    var composedLocation = await CacheManager.LookupOrRetrieveAsync(dataObject.Location, redis, async dbref => await Room.GetAsync(redis, dataObject.Location));
                    this.Location = composedLocation;
                });

            if (dataObject.Contents != null)
            {
                var contents = new List<ComposedObject>();
                Parallel.ForEach(
                    dataObject.Contents, 
                    async dbref =>
                    {
                        var composedContent = await CacheManager.LookupOrRetrieveAsync(dbref, redis, async d => await ObjectBase.GetAsync(redis, d));
                        if (composedContent != null)
                            contents.Add(composedContent);
                    });

                this.Contents = contents.AsReadOnly();
            }

            var taskParent = Task.Run(async () =>
            {
                var composedParent = await CacheManager.LookupOrRetrieveAsync(dataObject.Parent, redis, async dbref => await ObjectBase.GetAsync(redis, dataObject.Parent));
                this.Parent = composedParent;
            });

            Task.WaitAll(taskLocation, taskParent);
        }
    }
}
