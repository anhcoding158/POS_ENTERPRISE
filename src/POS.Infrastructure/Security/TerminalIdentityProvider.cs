using POS.Application.Abstractions.Security;

namespace POS.Infrastructure.Security;

public sealed class TerminalIdentityProvider : ITerminalIdentityProvider
{
    public string TerminalId =>
        string.Equals(Environment.GetEnvironmentVariable("POS_RUNTIME_MODE"), "IsolatedTest", StringComparison.OrdinalIgnoreCase)
            ? "TERM-ISOLATED"
            : "TERM-PRODUCTION";

    public string DisplayName => "Quầy chính";
}
