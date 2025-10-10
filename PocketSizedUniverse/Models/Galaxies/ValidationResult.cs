namespace PocketSizedUniverse.Models.Galaxies;

public class ValidationResult
{
    public bool Valid { get; set; }
    public string? Message { get; set; }
    public static ValidationResult Success() => new() { Valid = true };
    public static ValidationResult Fail(string message) => new() { Valid = false, Message = message };
}