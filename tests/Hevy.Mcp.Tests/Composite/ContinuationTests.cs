using System.Text;
using System.Text.Json;
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

    (json.RootElement.EnumerateObject().Select(static property => property.Name).Order()).Should().Equal(["endpoint", "filters", "next_page", "remaining_item_budget"]);
    (state.NextPage).Should().Be(4);
    (state.RemainingItemBudget).Should().Be(875);
    (state.Filters).Should().BeEquivalentTo(filters);
    (json.RootElement.GetRawText()).Should().NotContainEquivalentOf("api");
  }

  [Theory]
  [InlineData("")]
  [InlineData("not-base64!")]
  [InlineData("e30")]
  public void MalformedTokensAreRejected(string token)
  {
    FluentActions.Invoking(() => Continuation.Parse(token, "routines", new Dictionary<string, string?>())).Should().ThrowExactly<ArgumentException>();
  }

  [Fact]
  public void EndpointOrFilterMismatchIsRejected()
  {
    var filters = new Dictionary<string, string?> { ["query"] = "squat" };
    var token = Continuation.Create("routines", 2, filters, 90);

    FluentActions.Invoking(() => Continuation.Parse(token, "exercise-templates", filters)).Should().ThrowExactly<ArgumentException>();
    FluentActions.Invoking(() => Continuation.Parse(token, "routines", new Dictionary<string, string?> { ["query"] = "press" })).Should().ThrowExactly<ArgumentException>();
  }

  [Theory]
  [InlineData(0)]
  [InlineData(1001)]
  public void InvalidOrOverLimitBudgetsAreRejected(int remaining)
  {
    var token = EncodeJson($$"""{"endpoint":"routines","filters":{},"next_page":2,"remaining_item_budget":{{remaining}}}""");

    FluentActions.Invoking(() => Continuation.Parse(token, "routines", new Dictionary<string, string?>())).Should().ThrowExactly<ArgumentOutOfRangeException>();
  }

  [Fact]
  public void UnknownPayloadFieldsAreRejected()
  {
    var token = EncodeJson("""{"endpoint":"routines","filters":{},"next_page":2,"remaining_item_budget":10,"credential":"secret"}""");

    FluentActions.Invoking(() => Continuation.Parse(token, "routines", new Dictionary<string, string?>())).Should().ThrowExactly<ArgumentException>();
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
