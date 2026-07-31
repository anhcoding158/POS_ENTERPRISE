using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace POS.Application.Common;

public static partial class PosLog
{
    private static readonly ConcurrentDictionary<LogLevel, Action<ILogger, string, Exception?>>
        Writers = new();
    private static readonly Regex Placeholder = new(
        @"\{[^{}]+\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static void Information(
        ILogger logger,
        string messageTemplate,
        params object?[] arguments) =>
        Write(logger, LogLevel.Information, null, messageTemplate, arguments);

    public static void Warning(
        ILogger logger,
        string messageTemplate,
        params object?[] arguments) =>
        Write(logger, LogLevel.Warning, null, messageTemplate, arguments);

    public static void Warning(
        ILogger logger,
        Exception exception,
        string messageTemplate,
        params object?[] arguments) =>
        Write(logger, LogLevel.Warning, exception, messageTemplate, arguments);

    public static void Error(
        ILogger logger,
        string messageTemplate,
        params object?[] arguments) =>
        Write(logger, LogLevel.Error, null, messageTemplate, arguments);

    public static void Error(
        ILogger logger,
        Exception exception,
        string messageTemplate,
        params object?[] arguments) =>
        Write(logger, LogLevel.Error, exception, messageTemplate, arguments);

    public static void Critical(
        ILogger logger,
        string messageTemplate,
        params object?[] arguments) =>
        Write(logger, LogLevel.Critical, null, messageTemplate, arguments);

    public static void Critical(
        ILogger logger,
        Exception exception,
        string messageTemplate,
        params object?[] arguments) =>
        Write(logger, LogLevel.Critical, exception, messageTemplate, arguments);

    private static void Write(
        ILogger logger,
        LogLevel level,
        Exception? exception,
        string messageTemplate,
        object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(messageTemplate);
        if (!logger.IsEnabled(level))
            return;

        var writer = Writers.GetOrAdd(
            level,
            static value => LoggerMessage.Define<string>(
                value,
                new EventId(0, "POS"),
                "{RenderedMessage}"));
        writer(logger, Render(messageTemplate, arguments), exception);
    }

    private static string Render(string template, object?[] arguments)
    {
        if (arguments.Length == 0)
            return template;

        var index = 0;
        var rendered = Placeholder.Replace(
            template,
            match => index < arguments.Length
                ? Convert.ToString(arguments[index++], CultureInfo.InvariantCulture)
                    ?? string.Empty
                : match.Value);
        if (index >= arguments.Length)
            return rendered;

        var builder = new StringBuilder(rendered);
        for (; index < arguments.Length; index++)
        {
            builder.Append(" | ");
            builder.Append(Convert.ToString(arguments[index], CultureInfo.InvariantCulture));
        }
        return builder.ToString();
    }
}
