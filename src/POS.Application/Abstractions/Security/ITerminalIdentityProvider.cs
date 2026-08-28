namespace POS.Application.Abstractions.Security;

public interface ITerminalIdentityProvider
{
    string TerminalId { get; }
    string DisplayName { get; }
}
