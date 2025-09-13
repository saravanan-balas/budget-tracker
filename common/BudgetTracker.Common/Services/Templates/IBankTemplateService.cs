using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using BudgetTracker.Common.DTOs;
using BudgetTracker.Common.Models;

namespace BudgetTracker.Common.Services.Templates;

public interface IBankTemplateService
{
    Task<BankTemplate?> FindTemplateAsync(string bankName, string country, string format);
    Task<BankTemplate> SaveTemplateAsync(BankDetectionResult bankInfo, TransactionParsingResult parseResult);
    Task<bool> UpdateTemplateSuccessAsync(Guid templateId, bool success);
    Task<IEnumerable<BankTemplate>> GetKnownBanksAsync();
    Task<BankTemplate?> GetTemplateByIdAsync(Guid templateId);
}