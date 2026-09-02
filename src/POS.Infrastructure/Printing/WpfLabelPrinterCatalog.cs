using System.Printing;
using POS.Application.Abstractions.Printing;
using POS.Application.DTOs.Printing;

namespace POS.Infrastructure.Printing;

public sealed class WpfLabelPrinterCatalog : ILabelPrinterCatalog
{
    public IReadOnlyList<LabelPrinterInfo> Discover()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Array.Empty<LabelPrinterInfo>();
        }

        try
        {
            using var server = new LocalPrintServer();
            var defaultName = server.DefaultPrintQueue?.Name;
            var queueTypes = new[]
            {
                EnumeratedPrintQueueTypes.Local,
                EnumeratedPrintQueueTypes.Connections
            };
            return server.GetPrintQueues(
                    queueTypes)
                .Select(queue =>
                {
                    try
                    {
                        queue.Refresh();
                        return new LabelPrinterInfo(
                            queue.Name,
                            !queue.IsOffline && !queue.IsNotAvailable,
                            SupportsCustomMedia(queue));
                    }
                    catch (PrintSystemException)
                    {
                        return new LabelPrinterInfo(queue.Name, false, false);
                    }
                    finally
                    {
                        queue.Dispose();
                    }
                })
                .OrderByDescending(x => string.Equals(
                    x.Name, defaultName, StringComparison.OrdinalIgnoreCase))
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (PrintSystemException)
        {
            return Array.Empty<LabelPrinterInfo>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<LabelPrinterInfo>();
        }
    }

    private static bool SupportsCustomMedia(PrintQueue queue)
    {
        try
        {
            var capabilities = queue.GetPrintCapabilities();
            return capabilities.PageMediaSizeCapability.Count > 0;
        }
        catch (PrintSystemException)
        {
            return false;
        }
    }
}
