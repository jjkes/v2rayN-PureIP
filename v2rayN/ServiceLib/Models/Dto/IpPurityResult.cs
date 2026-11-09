namespace ServiceLib.Models.Dto;

/// <summary>
/// ippure public API payload (https://my.ippure.com/v1/info)
/// </summary>
internal class IpPurityInfo
{
    public string? ip { get; set; }
    public int? asn { get; set; }
    public string? asOrganization { get; set; }
    public string? country { get; set; }
    public string? countryCode { get; set; }
    public string? city { get; set; }
    public int? fraudScore { get; set; }
    public bool? isResidential { get; set; }
    public bool? isBroadcast { get; set; }
}

public readonly record struct IpPurityResult(int Purity, string Type, string? Ip)
{
    public override string ToString()
    {
        return $"{Purity}% {Type}";
    }
}
