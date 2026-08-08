using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace POS.Application.Common;

public static partial class PosLog
{
    private static readonly ConcurrentDictionary<LogLevel, Action<ILogger, string, Exception?>>
        Writers = new();
    public const string Redacted = SafeDiagnosticPolicy.Redacted;
    private static readonly Regex Placeholder = new(
        @"\{(?<name>[^{}:,]+)(?:[^{}]*)\}",
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
        writer(logger, Render(messageTemplate, arguments, exception), null);
    }

    private static string Render(
        string template,
        object?[] arguments,
        Exception? exception)
    {
        var index = 0;
        var rendered = Placeholder.Replace(
            template,
            match => index < arguments.Length
                ? SafeDiagnosticPolicy.Sanitize(match.Groups["name"].Value, arguments[index++])
                : match.Value);

        var builder = new StringBuilder(rendered);
        for (; index < arguments.Length; index++)
        {
            builder.Append(" | ");
            builder.Append(Redacted);
        }

        if (exception is not null)
        {
            builder.Append(" | ExceptionType=");
            builder.Append(exception.GetType().FullName ?? exception.GetType().Name);
            AppendNumericExceptionProperty(builder, exception, "SqliteErrorCode");
            AppendNumericExceptionProperty(builder, exception, "SqliteExtendedErrorCode");
        }

        return builder.ToString();
    }

    private static void AppendNumericExceptionProperty(
        StringBuilder builder,
        Exception exception,
        string propertyName)
    {
        try
        {
            var property = exception.GetType().GetProperty(propertyName);
            if (property?.PropertyType == typeof(int) && property.GetValue(exception) is int value)
            {
                builder.Append(" | ");
                builder.Append(propertyName);
                builder.Append('=');
                builder.Append(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        catch
        {
            // Exception inspection is optional and must never affect the caller.
        }
    }
}
