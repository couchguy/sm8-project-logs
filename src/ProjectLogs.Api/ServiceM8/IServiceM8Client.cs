namespace ProjectLogs.Api.ServiceM8;

public interface IServiceM8Client
{
    Task<List<Sm8JobMaterial>> GetJobMaterialsAsync(string accessToken, string jobUuid);
    Task<string> CreateNoteAsync(string accessToken, string jobUuid, string noteText);
    Task<Sm8Job?> GetJobAsync(string accessToken, string jobUuid);
}
