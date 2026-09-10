namespace CryptoTracker.API.DTOs;

/// <summary>
/// Sayfalanmış liste sonucu (Görev 51). Öğelerin yanında toplam kayıt sayısı ve
/// sayfa bilgisini taşır, böylece istemci "Sayfa X / Y" ve ileri/geri gösterebilir.
/// </summary>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages
);
