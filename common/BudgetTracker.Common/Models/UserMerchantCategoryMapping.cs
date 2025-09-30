using System.ComponentModel.DataAnnotations;

namespace BudgetTracker.Common.Models;

public class UserMerchantCategoryMapping
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    public Guid UserId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string MerchantName { get; set; } = string.Empty;
    
    [Required]
    public Guid CategoryId { get; set; }
    
    /// <summary>
    /// Confidence score for this mapping (0.0 to 1.0+)
    /// Increases each time user confirms this category for this merchant
    /// </summary>
    public decimal ConfidenceScore { get; set; } = 1.0m;
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Category Category { get; set; } = null!;
}