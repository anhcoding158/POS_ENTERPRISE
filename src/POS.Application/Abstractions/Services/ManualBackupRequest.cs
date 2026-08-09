namespace POS.Application.Abstractions.Services;

public sealed record ManualBackupRequest(
    string DestinationDirectory);
