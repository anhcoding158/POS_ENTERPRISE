namespace POS.Application.Abstractions.Serialization;

/// <summary>
/// Trừu tượng hóa việc chuyển đổi JSON.
///
/// Application không phụ thuộc trực tiếp System.Text.Json.
/// </summary>
public interface IJsonSerializer
{
    string Serialize<TValue>(TValue value);

    TValue? Deserialize<TValue>(string json);

    object? Deserialize(
        string json,
        Type targetType);
}