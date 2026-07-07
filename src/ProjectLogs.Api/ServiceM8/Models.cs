using System.Text.Json.Serialization;

namespace ProjectLogs.Api.ServiceM8;

/// <summary>ServiceM8 JobMaterial API record.</summary>
public class Sm8JobMaterial
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";

    [JsonPropertyName("job_uuid")]
    public string JobUuid { get; set; } = "";

    [JsonPropertyName("material_uuid")]
    public string? MaterialUuid { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("quantity")]
    public string? Quantity { get; set; }

    [JsonPropertyName("price")]
    public string? Price { get; set; }

    [JsonPropertyName("cost")]
    public string? Cost { get; set; }

    [JsonPropertyName("tax_rate_uuid")]
    public string? TaxRateUuid { get; set; }

    [JsonPropertyName("sort_order")]
    public string? SortOrder { get; set; }

    [JsonPropertyName("active")]
    public int Active { get; set; } = 1;
}

/// <summary>ServiceM8 Job API record (subset of fields we need).</summary>
public class Sm8Job
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("generated_job_id")]
    public string? GeneratedJobId { get; set; }

    [JsonPropertyName("job_description")]
    public string? JobDescription { get; set; }
}
