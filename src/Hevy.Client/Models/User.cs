namespace Hevy.Client.Models;

public sealed record UserInfo(string Id, string Name, string Url);

public sealed record UserInfoResponse(UserInfo Data);
