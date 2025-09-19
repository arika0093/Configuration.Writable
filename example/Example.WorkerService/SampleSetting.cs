namespace Example.WorkerService;

public record SampleSetting
{
    public string? Name { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.Now;
}
