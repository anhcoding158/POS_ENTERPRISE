namespace POS.Application.Abstractions.Purchasing;

/// <summary>
/// Sinh mã ứng viên cho Purchase Order. Unique index của database
/// và retry ở Application mới là authority cuối.
/// </summary>
public interface IPurchaseOrderNumberGenerator
{
    string Generate(DateTimeOffset utcNow);
}
