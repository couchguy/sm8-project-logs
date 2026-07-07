using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ProjectLogs.Api.ServiceM8;

public class ServiceM8Client(HttpClient http) : IServiceM8Client
{
    private const string BaseUrl = "https://api.servicem8.com/api_1.0";

    public async Task<List<Sm8JobMaterial>> GetJobMaterialsAsync(string accessToken, string jobUuid)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{BaseUrl}/jobmaterial.json?%24filter=job_uuid eq '{jobUuid}'");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<Sm8JobMaterial>>() ?? [];
    }

    public async Task<string> CreateNoteAsync(string accessToken, string jobUuid, string noteText)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/Note.json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new
        {
            related_object = "job",
            related_object_uuid = jobUuid,
            note = noteText
        });

        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return response.Headers.TryGetValues("x-record-uuid", out var values)
            ? values.First()
            : "";
    }

    public async Task<Sm8Job?> GetJobAsync(string accessToken, string jobUuid)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{BaseUrl}/job/{jobUuid}.json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        return await response.Content.ReadFromJsonAsync<Sm8Job>();
    }
}
