using POS.Application.Common;
using POS.Application.DTOs.Printing;

namespace POS.Application.Abstractions.Printing;

public interface ILabelPrinterCatalog
{
    IReadOnlyList<LabelPrinterInfo> Discover();
}

public interface ILabelPrintSettingsStore
{
    LabelPrintSettings Current { get; }
    void Save(LabelPrintSettings settings);
}

/// <summary>
/// Boundary gửi job. Automated tests thay thế interface này bằng recording fake.
/// </summary>
public interface ILabelPrintDispatcher
{
    Task<Result> DispatchAsync(
        LabelPrintRequest request,
        CancellationToken cancellationToken = default);
}

public interface ILabelPrintingService
{
    Task<Result> PrintAsync(
        LabelPrintRequest request,
        CancellationToken cancellationToken = default);
}
