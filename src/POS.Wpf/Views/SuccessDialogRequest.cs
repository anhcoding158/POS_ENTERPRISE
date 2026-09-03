namespace POS.Wpf.Views;

public sealed record SuccessDialogLine(string Text);

public sealed record SuccessDialogRequest(
    string Title,
    string Message,
    IReadOnlyList<SuccessDialogLine> Details,
    string AcknowledgeAutomationId = "SuccessDialogAcknowledgeButton")
{
    public static SuccessDialogRequest StoreSettingsSaved() => new(
        "Lưu cài đặt thành công",
        "Cài đặt cửa hàng đã được lưu thành công.",
        [new("Các thay đổi đã sẵn sàng sử dụng.")],
        "StoreSettingsSuccessAcknowledgeButton");
}
