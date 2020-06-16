namespace Nick.InferenceEngine.Net
{
    public class CoreVersions
    {
        public string? DeviceName { get; }
        public string? Description { get; }
        public long Major { get; }
        public long Minor { get; }
        public string? BuildNumber { get; }

        public CoreVersions(string? deviceName, string? description, long major, long minor, string? buildNumber)
        {
            DeviceName = deviceName;
            Description = description;
            Major = major;
            Minor = minor;
            BuildNumber = buildNumber;
        }
    }
}