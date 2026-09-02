using POS.Application.DTOs.Printing;
using ZXing;
using ZXing.Common;

namespace POS.Infrastructure.Printing;

/// <summary>
/// Mã hóa Code 128 tại runtime thành BitMatrix; renderer chỉ vẽ các ô đen
/// bằng vector rectangle, không tạo hoặc kéo giãn ảnh barcode.
/// </summary>
public static class LabelBarcodeEncoder
{
    public static BitMatrix Encode(string value, int width = 400, int height = 60)
    {
        var error = LabelBarcodeValidator.GetError(value);
        if (!string.IsNullOrEmpty(error)) throw new ArgumentException(error, nameof(value));
        return new MultiFormatWriter().encode(
            value!.Trim(),
            BarcodeFormat.CODE_128,
            Math.Max(1, width),
            Math.Max(1, height),
            new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = 0 });
    }
}
