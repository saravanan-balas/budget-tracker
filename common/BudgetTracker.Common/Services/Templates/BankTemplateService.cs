using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.DTOs;
using BudgetTracker.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Common.Services.Templates;

public class BankTemplateService : IBankTemplateService
{
    private readonly BudgetTrackerDbContext _context;
    private readonly ILogger<BankTemplateService> _logger;

    public BankTemplateService(
        BudgetTrackerDbContext context,
        ILogger<BankTemplateService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BankTemplate?> FindTemplateAsync(string bankName, string country, string format)
    {
        var template = await _context.BankTemplates
            .Where(t => t.BankName.ToLower() == bankName.ToLower() &&
                       t.Country.ToLower() == country.ToLower() &&
                       t.FileFormat.ToLower() == format.ToLower())
            .OrderByDescending(t => t.ConfidenceScore)
            .ThenByDescending(t => t.LastUsed)
            .FirstOrDefaultAsync();

        if (template != null)
        {
            // Update last used timestamp
            template.LastUsed = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Found template for {BankName} ({Country}) with confidence {Confidence}",
                bankName, country, template.ConfidenceScore);
        }

        return template;
    }

    public async Task<BankTemplate> SaveTemplateAsync(BankDetectionResult bankInfo, TransactionParsingResult parseResult)
    {
        var templatePattern = new
        {
            BankInfo = bankInfo,
            ParsePattern = new
            {
                Success = parseResult.IsSuccessful,
                TransactionCount = parseResult.Transactions.Count,
                SampleTransactions = parseResult.Transactions.Take(3).ToList(),
                ProcessingTime = parseResult.ProcessingTime.TotalSeconds
            }
        };

        var template = new BankTemplate
        {
            Id = Guid.NewGuid(),
            BankName = bankInfo.BankName,
            Country = bankInfo.Country,
            FileFormat = bankInfo.FileFormat,
            TemplatePattern = JsonSerializer.Serialize(templatePattern),
            SuccessCount = parseResult.IsSuccessful ? 1 : 0,
            FailureCount = parseResult.IsSuccessful ? 0 : 1,
            ConfidenceScore = bankInfo.Confidence,
            LastUsed = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.BankTemplates.Add(template);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Saved new template for {BankName} ({Country}) - {Format}",
            bankInfo.BankName, bankInfo.Country, bankInfo.FileFormat);

        return template;
    }

    public async Task<bool> UpdateTemplateSuccessAsync(Guid templateId, bool success)
    {
        var template = await _context.BankTemplates.FindAsync(templateId);
        if (template == null) return false;

        if (success)
        {
            template.SuccessCount++;
            template.ConfidenceScore = Math.Min(1.0, template.ConfidenceScore + 0.1);
        }
        else
        {
            template.FailureCount++;
            template.ConfidenceScore = Math.Max(0.0, template.ConfidenceScore - 0.1);
        }

        template.LastUsed = DateTime.UtcNow;
        template.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated template {TemplateId}: Success={Success}, New Confidence={Confidence}",
            templateId, success, template.ConfidenceScore);

        return true;
    }

    public async Task<IEnumerable<BankTemplate>> GetKnownBanksAsync()
    {
        var templates = await _context.BankTemplates
            .Where(t => t.ConfidenceScore > 0.5) // Only return reliable templates
            .OrderBy(t => t.Country)
            .ThenBy(t => t.BankName)
            .ToListAsync();

        return templates;
    }

    public async Task<BankTemplate?> GetTemplateByIdAsync(Guid templateId)
    {
        return await _context.BankTemplates.FindAsync(templateId);
    }
}