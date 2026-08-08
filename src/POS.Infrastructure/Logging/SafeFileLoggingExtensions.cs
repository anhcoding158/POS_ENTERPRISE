using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace POS.Infrastructure.Logging;

public static class SafeFileLoggingExtensions
{
    public static ILoggingBuilder AddPosSafeFile(
        this ILoggingBuilder builder,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        if (builder.Services.Any(descriptor =>
                descriptor.ImplementationType == typeof(SafeFileLoggerProvider)))
        {
            return builder;
        }

        var options = new SafeFileLoggerOptions();
        configuration.GetSection(SafeFileLoggerOptions.SectionName).Bind(options);
        options.Validate();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<ILoggerProvider, SafeFileLoggerProvider>();
        return builder;
    }
}
