using System.Text;
using System.Text.Json;
using Hevy.Mcp.Composite;
using Xunit;

namespace Hevy.Mcp.Tests.Composite;

public sealed class ContinuationTests
{
  [Fact]
  public void RoundTripContainsOnlyEndpointNextPageOriginalFiltersAndRemainingBudget()
  {
    var filters = new Dictionary<string, string?>(StringComparer.Ordinal)
    {
      ["equipment"] = "barbell",
      ["muscle"] = null,
      ["query"] = "bench press",
    };

    var token = Continuation.Create("exercise-templates", 4, filters, 875);
    var json = DecodeToken(token);
    var state = Continuation.Parse(token, "exercise-templates", filters);

    Assert.Equal(["endpoint", "filters", "next_page", "remaining_item_budget"], json.RootElement.EnumerateObject().Select(static property => property.Name).Order());
    Assert.Equal(4, state.NextPage);
    Assert.Equal(875, state.RemainingItemBudget);
    Assert.Equal(filters, state.Filters);
    Assert.DoesNotContain("api", json.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
  }

  [Theory]
  [InlineData("")]
  [InlineData("not-base64!")]
  [InlineData("e30")]
  public void MalformedTokensAreRejected(string token)
  {
    Assert.Throws<ArgumentException>(() => Continuation.Parse(token, "routines", new Dictionary<string, string?>()));
  }

  [Fact]
  public void EndpointOrFilterMismatchIsRejected()
  {
    var filters = new Dictionary<string, string?> { ["query"] = "squat" };
    var token = Continuation.Create("routines", 2, filters, 90);

    Assert.Throws<ArgumentException>(() => Continuation.Parse(token, "exercise-templates", filters));
    Assert.Throws<ArgumentException>(() => Continuation.Parse(token, "routines", new Dictionary<string, string?> { ["query"] = "press" }));
  }

  [Theory]
  [InlineData(0)]
  [InlineData(1001)]
  public void InvalidOrOverLimitBudgetsAreRejected(int remaining)
  {
    var token = EncodeJson($$"""{"endpoint":"routines","filters":{},"next_page":2,"remaining_item_budget":{{remaining}}}""");

    Assert.Throws<ArgumentOutOfRangeException>(() => Continuation.Parse(token, "routines", new Dictionary<string, string?>()));
  }

  [Fact]
  public void UnknownPayloadFieldsAreRejected()
  {
    var token = EncodeJson("""{"endpoint":"routines","filters":{},"next_page":2,"remaining_item_budget":10,"credential":"secret"}""");

    Assert.Throws<ArgumentException>(() => Continuation.Parse(token, "routines", new Dictionary<string, string?>()));
  }

  private static JsonDocument DecodeToken(string token) => JsonDocument.Parse(Base64UrlDecode(token));

  private static string EncodeJson(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

  private static byte[] Base64UrlDecode(string token)
  {
    var padded = token.Replace('-', '+').Replace('_', '/');
    padded += new string('=', (4 - (padded.Length % 4)) % 4);
    return Convert.FromBase64String(padded);
  }
}
