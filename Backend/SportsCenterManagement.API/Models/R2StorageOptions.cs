namespace SportsCenterManagement.API.Models;

public sealed class R2StorageOptions
{
    public const string SectionName = "R2Storage";

    public string ServiceUrl { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string PublicDomain { get; set; } = string.Empty;
}
