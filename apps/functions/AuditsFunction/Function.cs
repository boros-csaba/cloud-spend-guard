using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace CloudSpendGuard.Functions.Audits;

public record AuditsResponse([property: JsonPropertyName("result")] string Result);

public class Function
{
    public AuditsResponse Handler() => new("it works!");
}
