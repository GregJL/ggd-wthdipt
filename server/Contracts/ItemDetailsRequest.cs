using System.Text.Json.Serialization;
using Ggd.Api.Models;

namespace Ggd.Api.Contracts;

public abstract record ItemDetailsRequest
{
    [JsonIgnore] public HashSet<string> ProvidedFields { get; } = [];
    private string? category;
    public string? Category { get => category; init { category = value; ProvidedFields.Add(nameof(Category)); } }
    private string[]? tags;
    public string[]? Tags { get => tags; init { tags = value; ProvidedFields.Add(nameof(Tags)); } }
    private string? brand;
    public string? Brand { get => brand; init { brand = value; ProvidedFields.Add(nameof(Brand)); } }
    private string? model;
    public string? Model { get => model; init { model = value; ProvidedFields.Add(nameof(Model)); } }
    private string? serialNumber;
    public string? SerialNumber { get => serialNumber; init { serialNumber = value; ProvidedFields.Add(nameof(SerialNumber)); } }
    private DateOnly? purchaseDate;
    public DateOnly? PurchaseDate { get => purchaseDate; init { purchaseDate = value; ProvidedFields.Add(nameof(PurchaseDate)); } }
    private decimal? purchasePrice;
    public decimal? PurchasePrice { get => purchasePrice; init { purchasePrice = value; ProvidedFields.Add(nameof(PurchasePrice)); } }
    private decimal? replacementValue;
    public decimal? ReplacementValue { get => replacementValue; init { replacementValue = value; ProvidedFields.Add(nameof(ReplacementValue)); } }
    private DateOnly? warrantyExpiresOn;
    public DateOnly? WarrantyExpiresOn { get => warrantyExpiresOn; init { warrantyExpiresOn = value; ProvidedFields.Add(nameof(WarrantyExpiresOn)); } }
    private string? warrantyNotes;
    public string? WarrantyNotes { get => warrantyNotes; init { warrantyNotes = value; ProvidedFields.Add(nameof(WarrantyNotes)); } }
    private string? condition;
    public string? Condition { get => condition; init { condition = value; ProvidedFields.Add(nameof(Condition)); } }
}

public static class ItemDetailsValidation
{
    public static string? Error(ItemDetailsRequest request)
    {
        if (request.Category?.Trim().Length > 100) return "Category is too long (maximum 100 characters).";
        if (request.Brand?.Trim().Length > 100) return "Brand is too long (maximum 100 characters).";
        if (request.Model?.Trim().Length > 200) return "Model is too long (maximum 200 characters).";
        if (request.SerialNumber?.Trim().Length > 200) return "SerialNumber is too long (maximum 200 characters).";
        if (request.WarrantyNotes?.Trim().Length > 2000) return "WarrantyNotes is too long (maximum 2000 characters).";
        if (request.Condition?.Trim().Length > 100) return "Condition is too long (maximum 100 characters).";
        if (request.Tags is { Length: > 20 } || request.Tags?.Any(tag => tag is null || tag.Trim().Length > 50) == true)
            return "Use up to 20 tags, each up to 50 characters.";
        foreach (var value in new[] { request.PurchasePrice, request.ReplacementValue })
            if (value is decimal amount && (amount < 0 || amount > 9_999_999_999.99m || decimal.Round(amount, 2) != amount))
                return "Prices must be nonnegative, no more than 9,999,999,999.99, and have at most two decimal places.";
        return null;
    }

    public static void Apply(InventoryItem item, ItemDetailsRequest request)
    {
        static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (request.ProvidedFields.Contains(nameof(request.Category))) item.Category = Clean(request.Category);
        if (request.ProvidedFields.Contains(nameof(request.Tags))) item.Tags = (request.Tags ?? []).Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (request.ProvidedFields.Contains(nameof(request.Brand))) item.Brand = Clean(request.Brand);
        if (request.ProvidedFields.Contains(nameof(request.Model))) item.Model = Clean(request.Model);
        if (request.ProvidedFields.Contains(nameof(request.SerialNumber))) item.SerialNumber = Clean(request.SerialNumber);
        if (request.ProvidedFields.Contains(nameof(request.PurchaseDate))) item.PurchaseDate = request.PurchaseDate;
        if (request.ProvidedFields.Contains(nameof(request.PurchasePrice))) item.PurchasePrice = request.PurchasePrice;
        if (request.ProvidedFields.Contains(nameof(request.ReplacementValue))) item.ReplacementValue = request.ReplacementValue;
        if (request.ProvidedFields.Contains(nameof(request.WarrantyExpiresOn))) item.WarrantyExpiresOn = request.WarrantyExpiresOn;
        if (request.ProvidedFields.Contains(nameof(request.WarrantyNotes))) item.WarrantyNotes = Clean(request.WarrantyNotes);
        if (request.ProvidedFields.Contains(nameof(request.Condition))) item.Condition = Clean(request.Condition);
    }
}
