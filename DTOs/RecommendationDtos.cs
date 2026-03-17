namespace Recomandare_PC.DTOs;

/// <summary>
/// Sent by the client describing their current PC components.
/// </summary>
public record RecommendationRequest(
    /// <summary>
    /// List of components the user already has, e.g. [{ "category": "CPU", "name": "Intel i5-13600K" }]
    /// </summary>
    List<ExistingComponent> ExistingComponents,

    /// <summary>
    /// Optional budget constraint in RON/EUR.
    /// </summary>
    decimal? Budget,

    /// <summary>
    /// Optional usage context, e.g. "gaming 1080p", "video editing".
    /// </summary>
    string? UseCase
);

public record ExistingComponent(string Category, string Name);

/// <summary>
/// Returned to the client with LLM recommendations.
/// </summary>
public record RecommendationResponse(
    List<RecommendedItem> Recommendations,
    string Explanation
);

public record RecommendedItem(
    string Category,
    int ProductId,
    string ProductName,
    decimal Price,
    string Reason
);
