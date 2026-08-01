using Hevy.Core.Models;

namespace Hevy.Mcp.Tools;

internal sealed record MutationData<TPayload, TResult>(
    TPayload? Payload = default,
    TResult? Result = default)
    where TPayload : class
    where TResult : class;
