using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wivuu.GlobalCache
{
    internal class JsonSerializationProvider : ISerializationProvider
    {
        static JsonSerializationProvider()
        {
            var options = new JsonSerializerOptions
            {
                IgnoreNullValues = true,
            };

            options.Converters.Add(new JsonStringEnumConverter());

            Options = options;
        }

        public static JsonSerializerOptions Options { get; }

        public async Task<T> DeserializeFromStreamAsync<T>(Stream input, CancellationToken cancellationToken = default) => 
            await JsonSerializer.DeserializeAsync<T>(input, Options, cancellationToken).ConfigureAwait(false);

        public async Task SerializeToStreamAsync<T>(T input, Stream output, CancellationToken cancellationToken = default)
        {
            if (input is null)
                return;

            await JsonSerializer.SerializeAsync(output, input, Options, cancellationToken);
        }
    }
}