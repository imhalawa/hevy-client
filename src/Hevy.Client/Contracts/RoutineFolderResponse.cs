using System;
using System.Text.Json.Serialization;

namespace Hevy.Client.Contracts;

public sealed record RoutineFolderResponse([property: JsonRequired] long Id, [property: JsonRequired] int Index, [property: JsonRequired] string Title, [property: JsonRequired] DateTimeOffset UpdatedAt, [property: JsonRequired] DateTimeOffset CreatedAt);
