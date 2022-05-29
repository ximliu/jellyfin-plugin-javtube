using Jellyfin.Plugin.JavTube.Extensions;
using Jellyfin.Plugin.JavTube.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
#if __EMBY__
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;

#else
using Microsoft.Extensions.Logging;
#endif

namespace Jellyfin.Plugin.JavTube.Providers;

public class ActorProvider : BaseProvider, IRemoteMetadataProvider<Person, PersonLookupInfo>, IHasOrder
{
#if __EMBY__
    public ActorProvider(IHttpClient httpClient, ILogManager logManager) : base(
        httpClient,
        logManager.CreateLogger<ActorProvider>())
#else
        public ActorProvider(IHttpClientFactory httpClientFactory, ILogger<ActorProvider> logger) : base(
            httpClientFactory, logger)
#endif
    {
        // Init
    }

    public int Order => 1;

    public string Name => Constant.JavTube;

    public async Task<MetadataResult<Person>> GetMetadata(PersonLookupInfo info,
        CancellationToken cancellationToken)
    {
        var pid = info.GetProviderIdModel(Name);
        if (string.IsNullOrWhiteSpace(pid.Id) || string.IsNullOrWhiteSpace(pid.Provider))
        {
            var searchResults = (await GetSearchResults(info, cancellationToken)).ToList();
            if (searchResults.Any())
            {
                var firstResult = searchResults.First();
                pid = firstResult.GetProviderIdModel(Name);
            }
        }

        LogInfo("Get actor info: {0}", pid.Id);

        var m = await ApiClient.GetActorInfo(pid.Id, pid.Provider, cancellationToken);

        var result = new MetadataResult<Person>
        {
            Item = new Person
            {
                Name = m.Name,
                PremiereDate = m.Birthday.ValidDateTime(),
                ProductionYear = m.Birthday.ValidDateTime()?.Year,
                Overview = FormatOverview(m)
            },
            HasMetadata = true
        };

        // Set ProviderIdModel.
        result.Item.SetProviderIdModel(Name, new ProviderIdModel
        {
            Provider = m.Provider,
            Id = m.Id
        });

        // Set actor nationality.
        if (!string.IsNullOrWhiteSpace(m.Nationality))
            result.Item.ProductionLocations = new[] { m.Nationality };

        return result;
    }

    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(
        PersonLookupInfo info, CancellationToken cancellationToken)
    {
        var pid = info.GetProviderIdModel(Name);
        if (string.IsNullOrWhiteSpace(pid.Id))
        {
            // Search actor by name.
            pid.Id = info.Name;
        }

        LogInfo("Search for actor: {0}", pid.Id);

        var results = new List<RemoteSearchResult>();

        var searchResults = await ApiClient.SearchActor(pid.Id, pid.Provider, cancellationToken);
        if (!searchResults.Any())
        {
            LogInfo("Actor not found: {0}", pid.Id);
            return results;
        }

        foreach (var m in searchResults)
        {
            var result = new RemoteSearchResult
            {
                Name = m.Name,
                SearchProviderName = Name,
                ImageUrl = m.Images.Length > 0
                    ? ApiClient.GetPrimaryImageApiUrl(m.Id, m.Provider, m.Images[0], 0.5, true)
                    : string.Empty
            };
            result.SetProviderIdModel(Name, new ProviderIdModel
            {
                Provider = m.Provider,
                Id = m.Id
            });
            results.Add(result);
        }

        return results;
    }

    private static string FormatOverview(ActorInfoModel a)
    {
        string G(string k, string v)
        {
            return !string.IsNullOrWhiteSpace(v) ? $"{k}: {v}\n" : string.Empty;
        }

        var overview = string.Empty;
        overview += G("身高", $"{a.Height}cm");
        overview += G("血型", a.BloodType);
        overview += G("罩杯", a.CupSize);
        overview += G("三围", a.Measurements);
        overview += G("爱好", a.Hobby);
        return overview;
    }
}