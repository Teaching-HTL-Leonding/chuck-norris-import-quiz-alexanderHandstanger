using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

var factory = new ChuckNorrisJokeContextFactory();
using var dbContext = factory.CreateDbContext();
using var httpClient = new HttpClient();
bool noArguments = false;

if (args.Length == 1 || args.Length == 0)
{
    int maxJokes = -1;
    if (args.Length == 0)
    {
        maxJokes = 5;
        noArguments = true;
    }

    if (!noArguments && args[0].Equals("clean"))
    {
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ChuckNorrisJokes");
        await dbContext.SaveChangesAsync();
    }
    else
    {
        if (!noArguments)
        {
            bool success = Int32.TryParse(args[0], out maxJokes);
            if (!success) throw new Exception("Input wasn't a number!");
            if (maxJokes < 1 || maxJokes > 10) throw new Exception("Wrong amount of jokes [1 - 10]!");
        }
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            for (int i = 0; i < maxJokes; i++)
            {
                var jokeData = await GetRandomJoke();
                var newJoke = new ChuckNorrisJoke { ChuckNorrisId = jokeData.Id, Url = jokeData.Url, Joke = jokeData.Value };
                await dbContext.ChuckNorrisJokes.AddAsync(newJoke);
                await dbContext.SaveChangesAsync();
            }
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Something really bad happened: {ex.Message}");
        }
    }
}
else
{
    Console.Error.WriteLine("Wrong amount of arguments!");
}


async Task<ChuckNorrisJokeData> GetRandomJoke()
{
    var conditionCounter = 0;
    var jokeData = new ChuckNorrisJokeData();

    do
    {
        if (conditionCounter == 9) throw new Exception("We already have all jokes");
        var responseBody = await httpClient.GetStreamAsync("https://api.chucknorris.io/jokes/random");
        jokeData = await JsonSerializer.DeserializeAsync<ChuckNorrisJokeData>(responseBody);
        if (jokeData == null) throw new Exception("Something really bad happened: Could not deserialize json from httpClient");
        if (jokeData.Categories.FirstOrDefault() != "explicit") break;
        conditionCounter++;
    } while (!dbContext.ChuckNorrisJokes.Select(j => j.ChuckNorrisId).Contains(jokeData.Id));

    return jokeData;
}

class ChuckNorrisJokeData
{
    [JsonPropertyName("categories")]
    public string[] Categories { get; set; } = Array.Empty<string>();

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("icon_url")]
    public string IconUrl { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}
class ChuckNorrisJoke
{
    public int Id { get; set; }

    [MaxLength(40)]
    public string ChuckNorrisId { get; set; } = String.Empty;

    [MaxLength(1024)]
    public string Url { get; set; } = string.Empty;

    public string Joke { get; set; } = string.Empty;
}

class ChuckNorrisJokeContext : DbContext
{
    public ChuckNorrisJokeContext(DbContextOptions<ChuckNorrisJokeContext> options)
        : base(options)
    { }
    public DbSet<ChuckNorrisJoke> ChuckNorrisJokes { get; set; }
}

class ChuckNorrisJokeContextFactory : IDesignTimeDbContextFactory<ChuckNorrisJokeContext>
{
    public ChuckNorrisJokeContext CreateDbContext(string[]? args = null)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

        var optionsBuilder = new DbContextOptionsBuilder<ChuckNorrisJokeContext>();
        optionsBuilder
            // Uncomment the following line if you want to print generated
            // SQL statements on the console.
            // .UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()))
            .UseSqlServer(configuration["ConnectionStrings:DefaultConnection"]);

        return new ChuckNorrisJokeContext(optionsBuilder.Options);
    }
}