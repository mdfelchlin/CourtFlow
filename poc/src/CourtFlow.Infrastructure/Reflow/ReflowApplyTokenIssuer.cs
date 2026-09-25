using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CourtFlow.Application.Reflow;
using CourtFlow.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace CourtFlow.Infrastructure.Reflow;

public sealed class ReflowApplyTokenIssuer : IReflowApplyTokenIssuer
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly byte[] _key;

    public ReflowApplyTokenIssuer(IOptions<ScheduleVersionsOptions> opts)
    {
        var raw = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(opts.Value.SigningKey)
            ? "courtflow-dev-schedule-signing-key-do-not-use"
            : opts.Value.SigningKey);
        using var sha = SHA256.Create();
        _key = sha.ComputeHash(raw);
    }

    public string Sign(ReflowApplyPayload payload)
    {
        var payloadJson = JsonSerializer.Serialize(payload, JsonOpts);
        var payloadUtf8 = Encoding.UTF8.GetBytes(payloadJson);
        var sig = Convert.ToHexString(HMACSHA256.HashData(_key, payloadUtf8));
        var b64 = Convert.ToBase64String(payloadUtf8);
        return $"{b64}:{sig}";
    }

    public bool TryVerify(string token, out ReflowApplyPayload? payload)
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
        if (!string.Equals(parts[1].Trim(), expectedSig, StringComparison.Ordinal))
            return false;

        try
        {
            payload = JsonSerializer.Deserialize<ReflowApplyPayload>(utf8bytes, JsonOpts);
            return payload is not null;
        }
        catch
        {
            payload = null;
            return false;
        }
    }
}
