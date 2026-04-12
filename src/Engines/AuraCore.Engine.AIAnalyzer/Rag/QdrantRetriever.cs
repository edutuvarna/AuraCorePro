using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuraCore.Engine.AIAnalyzer.Rag;

/// <summary>
/// Calls the Python RAG retrieval API to fetch the most relevant source-code
/// chunks from the Qdrant vector database for a given natural-language query.
/// <para>
/// Default endpoint: <c>http://localhost:5000/api/retrieve</c>.
/// Override via constructor or <see cref="EndpointUrl"/>.
/// </para>
/// </summary>
public sealed class QdrantRetriever : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    /// <summary>Base URL of the retrieval API (no trailing slash).</summary>
    public string EndpointUrl { get; set; } = "http://localhost:5000/api/retrieve";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // -----------------------------------------------------------------------
    //  Constructors
    // -----------------------------------------------------------------------

    /// <summary>Create a retriever with a default <see cref="HttpClient"/>.</summary>
    public QdrantRetriever(string? endpointUrl = null)
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _ownsClient = true;
        if (!string.IsNullOrWhiteSpace(endpointUrl))
            EndpointUrl = endpointUrl;
    }

    /// <summary>Create a retriever using an externally-managed <see cref="HttpClient"/>
    /// (e.g. from <c>IHttpClientFactory</c>).</summary>
    public QdrantRetriever(HttpClient httpClient, string? endpointUrl = null)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ownsClient = false;
        if (!string.IsNullOrWhiteSpace(endpointUrl))
            EndpointUrl = endpointUrl;
    }

    // -----------------------------------------------------------------------
    //  Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Retrieve the top-<paramref name="topK"/> most relevant code/doc chunks
    /// for the given <paramref name="query"/>.
    /// </summary>
    /// <returns>
    /// A list of raw text chunks ordered by relevance, or an empty list if the
    /// retrieval service is unreachable.
    /// </returns>
    public async Task<List<string>> RetrieveContextAsync(
        string query, int topK = 3, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        try
        {
            var payload = new { query, top_k = topK };

            using var response = await _http.PostAsJsonAsync(EndpointUrl, payload, ct)
                                            .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return [];

            var body = await response.Content
                                     .ReadFromJsonAsync<RetrievalResponse>(_jsonOptions, ct)
                                     .ConfigureAwait(false);

            if (body?.Results is null)
                return [];

            return body.Results
                       .Where(r => !string.IsNullOrWhiteSpace(r.Text))
                       .Select(r => r.Text!)
                       .ToList();
        }
        catch (Exception)
        {
            // Connection refused, timeout, deserialization error, etc.
            // Gracefully degrade: the caller can proceed without RAG context.
            return [];
        }
    }

    // -----------------------------------------------------------------------
    //  Disposal
    // -----------------------------------------------------------------------

    public void Dispose()
    {
        if (_ownsClient)
            _http.Dispose();
    }

    // -----------------------------------------------------------------------
    //  DTOs (internal, mirrors the Python API response shape)
    // -----------------------------------------------------------------------

    private sealed class RetrievalResponse
    {
        [JsonPropertyName("query")]
        public string? Query { get; set; }

        [JsonPropertyName("results")]
        public List<ChunkResult>? Results { get; set; }
    }

    private sealed class ChunkResult
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("module_name")]
        public string? ModuleName { get; set; }

        [JsonPropertyName("file_path")]
        public string? FilePath { get; set; }

        [JsonPropertyName("chunk_type")]
        public string? ChunkType { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }
    }
}
