using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using BudgetTracker.Common.DTOs;

namespace BudgetTracker.Common.Services.Parsing;

public interface IUniversalBankParser
{
    Task<TransactionParsingResult> ParseFileAsync(
        byte[] fileData, 
        string fileName, 
        BankDetectionResult? bankInfo = null);
    
    Task<ImportPreviewDto> GeneratePreviewAsync(byte[] fileData, string fileName);
    
    Task<bool> CanProcessSynchronouslyAsync(byte[] fileData, string fileName);
}