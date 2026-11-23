using OopLab.Cli;
using OopLab.Cli.Rendering;
using OopLab.Cli.Strategies;
using OopLab.Core.Abstractions;
using OopLab.Core.Mapping;
using OopLab.Core.Models;
using OopLab.Core.Parsing;
using OopLab.Core.Services;
using OopLab.Core.Specifications;
using OopLab.Data.Caching;
using OopLab.Data.Persistence;

Console.OutputEncoding = System.Text.Encoding.UTF8;

void Assert(bool condition, string message)
{
    if (!condition)
        throw new Exception($"[ПЕРЕВІРКА НЕ ПРОЙДЕНА] {message}");
    Console.WriteLine($"  [PASS] {message}");
}

Console.WriteLine("=== АВТОМАТИЗОВАНА ПЕРЕВІРКА ВИМОГ ПРОЄКТУ (ПР 13-18) ===");

string tempDir = Path.Combine(Path.GetTempPath(), "OopLab_Verify_" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(tempDir);

try
{
    const string sampleJson = """
    {
      "results": [
        {
          "id": 101,
          "name": "Test Rick",
          "status": "Alive",
          "species": "Human",
          "type": "",
          "gender": "Male",
          "origin": { "name": "Earth (C-137)" },
          "location": { "name": "Citadel" },
          "image": "https://example.com/101.jpeg",
          "episode": ["1","2","3","4","5","6","7","8","9","10","11","12","13","14","15","16","17","18","19","20","21","22","23","24","25","26","27","28","29","30","31","32","33","34","35","36","37","38","39","40"]
        }
      ]
    }
    """;

    var parser = new RickAndMortyApiJsonParser();
    var parsedList = parser.ParseCharacterListPage(sampleJson);
    Assert(parsedList.Count == 1, "Парсер JSON успішно розібрав відповідь API.");
    Assert(parsedList[0].Rarity == 5, "Розрахунок рідкості (Rarity = 5 для >= 40 епізодів) коректний.");

    var mapper = new CharacterViewModelMapper();
    var vm = mapper.ToViewModel(parsedList[0]);
    Assert(vm.TierRank == "S+" && vm.PopularityScore >= 900, $"Мапінг у CharacterViewModel та формула рейтингу ({vm.PopularityScore}, {vm.TierRank}) коректні.");

    string jsonPath = Path.Combine(tempDir, "catalog_state.json");
    string cacheDir = Path.Combine(tempDir, "portraits_cache");
    var repo = new JsonCharacterRepository(jsonPath);
    var cache = new DiskImageCacheStorage(cacheDir);
    var mockRemote = new MockRemoteProvider(parsedList);
    var service = new CharacterCatalogService(repo, mockRemote, cache, mapper);

    var (synced, newlyCached) = await service.SyncWithRemoteAsync(limit: 10, downloadPortraits: true);
    Assert(synced == 1 && newlyCached == 1, "Синхронізація та запис портрета у файловий буфер виконані.");
    Assert(cache.IsCached("101"), "Портрет персонажа збережено на диск у папку буфера.");

    var repoReloaded = new JsonCharacterRepository(jsonPath);
    var reloadedChar = repoReloaded.GetByIdOrName("101");
    Assert(reloadedChar is not null && reloadedChar.Name == "Test Rick", "Постійність даних: стан каталогу відновлено після перезапуску.");

    var filtered = service.QueryCharacters(new CharacterFilterCriteria { Status = "Alive", OnlyCachedPortraits = true, SortBy = "power" });
    Assert(filtered.Count >= 1, "Специфікація фільтрації та сортування ViewModel працює коректно.");

    var renderer = new ConsoleCharacterRenderer();
    var cli = new CliFacade();
    cli.UseStrategy(new InformationCommandStrategy(service, renderer, () => cli.Strategies))
       .UseStrategy(new TransformativeCommandStrategy(service, renderer))
       .UseStrategy(new InfrastructureCommandStrategy(service, renderer));

    bool keepRunning = await cli.ExecCommandAsync("cache-info");
    Assert(keepRunning, "CLI Facade та Strategy успішно виконали команду 'cache-info'.");

    Console.WriteLine("=== УСІ ПЕРЕВІРКИ ПРОЙДЕНО УСПІШНО ===");
}
finally
{
    if (Directory.Exists(tempDir))
        Directory.Delete(tempDir, recursive: true);
}

file sealed class MockRemoteProvider : IRemoteCharacterProvider
{
    private readonly IReadOnlyList<CharacterProfile> _items;
    public MockRemoteProvider(IReadOnlyList<CharacterProfile> items) => _items = items;
    public string SourceName => "mock://rickandmortyapi";
    public Task<IReadOnlyList<CharacterProfile>> FetchRemoteCharactersAsync(int? limit = null, CancellationToken cancellationToken = default)
        => Task.FromResult(_items);
    public Task<CharacterProfile?> FetchCharacterByIdOrNameAsync(string idOrName, CancellationToken cancellationToken = default)
        => Task.FromResult(_items.FirstOrDefault());
}
