using System.Globalization;
using Hevy.Core.Models;
using Hevy.Mcp.Caching;

namespace Hevy.Mcp.Composite;

internal sealed record RoutineSearchItem(string Id, string Title, long? FolderId);
