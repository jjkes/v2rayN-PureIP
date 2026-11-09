namespace ServiceLib.Models.Dto;

public class SpeedTestResult
{
    public string? IndexId { get; set; }

    public string? Delay { get; set; }

    public string? Speed { get; set; }

    public string? IpInfo { get; set; }

    public int? IpPurity { get; set; }

    public string? IpPurityInfo { get; set; }
}
