using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using BudgetTracker.Common.DTOs;

namespace BudgetTracker.Common.Services.AI;

public interface IAIBankAnalyzer
{
    Task<BankDetectionResult> DetectBankAsync(byte[] fileData, string fileName);
    Task<TransactionParsingResult> ParseTransactionsWithAIAsync(
        byte[] fileData, 
        string fileName,
        BankDetectionResult bankInfo);
    Task<decimal> EstimateAICostAsync(int fileSize, string fileType);
    Task<bool> IsAIProcessingRequired(byte[] fileData, string fileName);
}