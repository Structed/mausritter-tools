using MausritterTools.Core.Data;

namespace MausritterTools.Web.Services;

/// <summary>Reads the data files that ship in <c>wwwroot/data</c> over HTTP.</summary>
public sealed class HttpDataFileReader(HttpClient httpClient) : IDataFileReader
{
    private readonly HttpClient _httpClient =
        httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<Stream> OpenAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.GetAsync(
            $"data/{relativePath}", cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }
}
