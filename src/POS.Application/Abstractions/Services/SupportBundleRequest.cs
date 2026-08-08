namespace POS.Application.Abstractions.Services;

public sealed record SupportBundleRequest(
    string DestinationDirectory,
    bool IncludeDatabase = false);
