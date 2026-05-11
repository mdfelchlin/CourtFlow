using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CourtFlow.Application.Scheduling;
using CourtFlow.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace CourtFlow.Infrastructure.Scheduling;

public sealed class ScheduleApplyTokenIssuer : IScheduleApplyTokenIssuer
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly byte[] _key;

    public ScheduleApplyTokenIssuer(IOptions<ScheduleVersionsOptions> opts)
    {
        var raw = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(opts.Value.SigningKey)
            ? "courtflow-dev-schedule-signing-key-do-not-use"
            : opts.Value.SigningKey);
        using var sha = SHA256.Create();
        _key = sha.ComputeHash(raw);
    }

    public string Sign(ScheduleApplyPayload payload)
    {
        var payloadJson = JsonSerializer.Serialize(payload, JsonOpts);
        var payloadUtf8 = Encoding.UTF8.GetBytes(payloadJson);
        var sig = Convert.ToHexString(HMACSHA256.HashData(_key, payloadUtf8));
        var b64 = Convert.ToBase64String(payloadUtf8);
        return $"{b64}:{sig}";
    }

    public bool TryVerify(string token, out ScheduleApplyPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var parts = token.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return false;

        byte[] utf8bytes;
        try
        {
            utf8bytes = Convert.FromBase64String(parts[0]);
        }
        catch
        {
            return false;
        }

        var expectedSig = Convert.ToHexString(HMACSHA256.HashData(_key, utf8bytes));
        var actualSig = parts[1].Trim();
        if (!string.Equals(actualSig, expectedSig, StringComparison.Ordinal))
            return false;

        try
        {
            payload = JsonSerializer.Deserialize<ScheduleApplyPayload>(utf8bytes, JsonOpts);
            return payload is not null;
        }
        catch
        {
            payload = null;
            return false;
        }
    }
}
