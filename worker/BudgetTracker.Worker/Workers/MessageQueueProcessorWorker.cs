using BudgetTracker.Common.Services.Messaging;
using BudgetTracker.Common.DTOs.Messaging;
using BudgetTracker.Common.DTOs;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.Models;
using BudgetTracker.Common.Services;
using BudgetTracker.Common.Services.Parsing;
using BudgetTracker.Common.Services.AI;
using BudgetTracker.Common.Services.OCR;
using BudgetTracker.Common.Services.Templates;
using BudgetTracker.Common.Services.Merchants;
using BudgetTracker.Common.Services.Categories;
using BudgetTracker.Common.Services.Transactions;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Worker.Workers;

public class MessageQueueProcessorWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMessageQueueService _messageQueue;
    private readonly ILogger<MessageQueueProcessorWorker> _logger;
    private string? _importSubscriptionId;
    private string? _recurringSubscriptionId;

    public MessageQueueProcessorWorker(
        IServiceProvider serviceProvider, 
        IMessageQueueService messageQueue,
        ILogger<MessageQueueProcessorWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _messageQueue = messageQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting message queue processor worker...");

        try
        {
            // Subscribe to import processing queue
            _importSubscriptionId = await _messageQueue.SubscribeAsync<ImportProcessingMessage>(
                "import-processing", 
                ProcessImportMessageAsync, 
                stoppingToken);

            // Subscribe to recurring transaction detection queue
            _recurringSubscriptionId = await _messageQueue.SubscribeAsync<RecurringTransactionDetectionMessage>(
                "recurring-detection", 
                ProcessRecurringMessageAsync, 
                stoppingToken);

            _logger.LogInformation("Message queue subscriptions established successfully");

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                
                // Log queue status periodically
                var importQueueLength = await _messageQueue.GetQueueLengthAsync("import-processing");
                var recurringQueueLength = await _messageQueue.GetQueueLengthAsync("recurring-detection");
                
                _logger.LogDebug("Queue status - Imports: {ImportCount}, Recurring: {RecurringCount}", 
                    importQueueLength, recurringQueueLength);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in message queue processor worker");
            throw;
        }
        finally
        {
            if (_importSubscriptionId != null)
            {
                await _messageQueue.UnsubscribeAsync(_importSubscriptionId);
            }
            
            if (_recurringSubscriptionId != null)
            {
                await _messageQueue.UnsubscribeAsync(_recurringSubscriptionId);
            }
        }
    }

    private async Task<bool> ProcessImportMessageAsync(ImportProcessingMessage message)
    {
        _logger.LogInformation("Processing import message for ImportId: {ImportId}", message.ImportId);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BudgetTrackerDbContext>();
            var blobService = scope.ServiceProvider.GetRequiredService<IBlobStorageService>();

            // Get the import record
            var import = await context.ImportedFiles
                .FirstOrDefaultAsync(f => f.Id == message.ImportId);

            if (import == null)
            {
                _logger.LogWarning("Import {ImportId} not found in database", message.ImportId);
                return true; // Message processed (not found is not an error)
            }

            if (import.Status != ImportStatus.Processing)
            {
                _logger.LogInformation("Import {ImportId} is no longer in processing status: {Status}", 
                    message.ImportId, import.Status);
                return true;
            }

            _logger.LogInformation("Processing import {ImportId} - {FileName} ({FileSize} bytes)", 
                import.Id, import.FileName, import.FileSize);

            await ProcessImportFileAsync(import, context, scope.ServiceProvider, blobService);

            import.Status = ImportStatus.Completed;
            import.ProcessingCompletedAt = DateTime.UtcNow;
            import.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();

            _logger.LogInformation("✅ Import {ImportId} completed successfully", import.Id);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error processing import {ImportId}: {Error}", message.ImportId, ex.Message);
            
            // Update import status to failed
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<BudgetTrackerDbContext>();
                
                var import = await context.ImportedFiles.FirstAsync(f => f.Id == message.ImportId);
                import.Status = ImportStatus.Failed;
                import.ErrorDetails = ex.Message;
                import.UpdatedAt = DateTime.UtcNow;
                
                await context.SaveChangesAsync();
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to update import status to failed");
            }
            
            return false; // Signal processing failure
        }
    }

    private async Task<bool> ProcessRecurringMessageAsync(RecurringTransactionDetectionMessage message)
    {
        _logger.LogInformation("Processing recurring transaction detection for UserId: {UserId}", message.UserId);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BudgetTrackerDbContext>();

            await DetectRecurringTransactionsAsync(context, message.UserId);
            await UpdateRecurringSchedulesAsync(context, message.UserId);

            _logger.LogInformation("✅ Recurring transaction processing completed for UserId: {UserId}", message.UserId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error processing recurring transactions for UserId: {UserId}", message.UserId);
            return false;
        }
    }

    private async Task ProcessImportFileAsync(Common.Models.ImportedFile import, BudgetTrackerDbContext context, 
        IServiceProvider serviceProvider, IBlobStorageService blobService)
    {
        if (string.IsNullOrEmpty(import.BlobUrl))
        {
            throw new InvalidOperationException("Import file URL is missing");
        }

        _logger.LogInformation("📥 Downloading file from blob storage...");
        var fileData = await blobService.DownloadFileAsync("imports", $"{import.UserId}/{ import.Id}{ import.FileType}");
        
        _logger.LogInformation("🔍 Parsing file with universal parser...");
        var transactions = await ParseFileWithUniversalParser(import, fileData, serviceProvider);
        
        _logger.LogInformation("📊 Found {Count} transactions to process", transactions.Count);
        
        var account = await GetAccountForImport(context, import.UserId);
        _logger.LogInformation("🏦 Using account: {AccountName} ({AccountId})", account.Name, account.Id);

        import.TotalRows = transactions.Count;
        var (importedCount, duplicateCount) = await ImportTransactionsAsync(
            context, serviceProvider, import, transactions, account.Id);

        import.ImportedTransactions = importedCount;
        import.DuplicateTransactions = duplicateCount;
        import.ProcessedRows = transactions.Count;

        _logger.LogInformation("💾 Import {ImportId} saved: {Imported} imported, {Duplicates} duplicates", 
            import.Id, importedCount, duplicateCount);
    }

    private async Task<List<Common.DTOs.ParsedTransaction>> ParseFileWithUniversalParser(
        Common.Models.ImportedFile import, Stream fileData, IServiceProvider serviceProvider)
    {
        var universalParser = serviceProvider.GetService<IUniversalBankParser>();
        var aiAnalyzer = serviceProvider.GetService<IAIBankAnalyzer>();
        var templateService = serviceProvider.GetService<IBankTemplateService>();

        if (universalParser == null)
        {
            _logger.LogWarning("Universal parser not available, using legacy parsing for import {ImportId}", import.Id);
            return ParseFileDataLegacy(import, fileData);
        }

        try
        {
            var memoryStream = new MemoryStream();
            await fileData.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            // Detect bank if not already detected
            Common.DTOs.BankDetectionResult? bankInfo = null;
            if (string.IsNullOrEmpty(import.DetectedBankName) && aiAnalyzer != null)
            {
                _logger.LogInformation("🏦 Detecting bank from file content...");
                bankInfo = await aiAnalyzer.DetectBankAsync(fileBytes, import.FileName);
                
                _logger.LogInformation("🏦 Detected: {Bank} ({Country}) - Confidence: {Confidence:F2}", 
                    bankInfo.BankName, bankInfo.Country, bankInfo.Confidence);
                
                import.DetectedBankName = bankInfo.BankName;
                import.DetectedCountry = bankInfo.Country;
                if (string.IsNullOrEmpty(import.DetectedFormat))
                {
                    import.DetectedFormat = bankInfo.FileFormat;
                }
            }

            _logger.LogInformation("📋 Parsing transactions from {Format} file...", import.DetectedFormat ?? "unknown");
            
            var parseResult = await universalParser.ParseFileAsync(fileBytes, import.FileName, bankInfo);
            return parseResult.Transactions.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error parsing file with universal parser, falling back to legacy parsing");
            return ParseFileDataLegacy(import, fileData);
        }
    }

    private List<Common.DTOs.ParsedTransaction> ParseFileDataLegacy(Common.Models.ImportedFile import, Stream fileData)
    {
        var transactions = new List<Common.DTOs.ParsedTransaction>();
        
        try
        {
            fileData.Position = 0;
            using var reader = new StreamReader(fileData);
            var content = reader.ReadToEnd();
            
            // Simple CSV parsing fallback
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 1)
            {
                for (int i = 1; i < lines.Length; i++)
                {
                    var values = lines[i].Split(',');
                    if (values.Length >= 3)
                    {
                        transactions.Add(new ParsedTransaction
                        {
                            Date = DateTime.TryParse(values[0], out var date) ? date : DateTime.Now,
                            Amount = decimal.TryParse(values[1], out var amount) ? amount : 0,
                            Description = values[2]
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Legacy parsing also failed for import {ImportId}", import.Id);
        }
        
        return transactions;
    }

    private async Task<Common.Models.Account> GetAccountForImport(BudgetTrackerDbContext context, Guid userId)
    {
        var account = await context.Accounts
            .FirstOrDefaultAsync(a => a.UserId == userId && a.Type == Common.Models.AccountType.Checking);

        if (account == null)
        {
            account = await context.Accounts
                .FirstOrDefaultAsync(a => a.UserId == userId);

            if (account == null)
            {
                throw new InvalidOperationException($"No account found for user {userId}");
            }
        }

        return account;
    }

    private async Task<(int importedCount, int duplicateCount)> ImportTransactionsAsync(
        BudgetTrackerDbContext context, IServiceProvider serviceProvider, 
        Common.Models.ImportedFile import, List<Common.DTOs.ParsedTransaction> transactions, 
        Guid accountId)
    {
        var merchantService = serviceProvider.GetRequiredService<IMerchantService>();
        var categoryService = serviceProvider.GetRequiredService<ICategoryAssignmentService>();
        var batchTransactionService = serviceProvider.GetRequiredService<IBatchTransactionService>();

        // Simulate batch import - replace with actual service call when available
        var importedCount = 0;
        var duplicateCount = 0;
        
        foreach (var transaction in transactions)
        {
            try
            {
                var newTransaction = new Transaction
                {
                    Id = Guid.NewGuid(),
                    UserId = import.UserId,
                    AccountId = accountId,
                    Amount = transaction.Amount,
                    Description = transaction.Description,
                    TransactionDate = transaction.Date,
                    PostedDate = transaction.Date,
                    OriginalMerchant = transaction.Description,
                    CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow
                };
                
                context.Transactions.Add(newTransaction);
                importedCount++;
            }
            catch (Exception)
            {
                duplicateCount++;
            }
        }
        
        await context.SaveChangesAsync();
        return (importedCount, duplicateCount);
    }

    private async Task DetectRecurringTransactionsAsync(BudgetTrackerDbContext context, Guid userId)
    {
        // Implementation from RecurringTransactionWorker
        var recentTransactions = await context.Transactions
            .Where(t => t.UserId == userId && t.TransactionDate >= DateTime.UtcNow.AddMonths(-3))
            .OrderBy(t => t.NormalizedMerchant)
            .ThenBy(t => t.TransactionDate)
            .ToListAsync();

        // Group by merchant and detect recurring patterns
        var merchantGroups = recentTransactions.GroupBy(t => t.NormalizedMerchant);
        
        foreach (var group in merchantGroups)
        {
            if (group.Count() >= 3 && !string.IsNullOrEmpty(group.Key)) // At least 3 occurrences
            {
                var sortedTransactions = group.OrderBy(t => t.TransactionDate).ToList();
                var intervals = new List<TimeSpan>();
                
                for (int i = 1; i < sortedTransactions.Count; i++)
                {
                    intervals.Add(sortedTransactions[i].TransactionDate - sortedTransactions[i-1].TransactionDate);
                }
                
                // Check if intervals are approximately equal (within 2 days tolerance)
                var avgInterval = TimeSpan.FromDays(intervals.Average(d => d.TotalDays));
                
                if (intervals.All(i => Math.Abs(i.TotalDays - avgInterval.TotalDays) <= 2))
                {
                    _logger.LogInformation("Found potential recurring transaction for {Merchant} with interval {Days} days", 
                        group.Key, avgInterval.TotalDays);
                        
                    // Create or update recurring series
                    await CreateOrUpdateRecurringSeries(context, userId, group.Key, avgInterval);
                }
            }
        }
    }

    private async Task UpdateRecurringSchedulesAsync(BudgetTrackerDbContext context, Guid userId)
    {
        var recurringSeries = await context.RecurringSeries
            .Where(r => r.UserId == userId && r.IsActive)
            .ToListAsync();

        foreach (var series in recurringSeries)
        {
            var lastOccurrence = await context.Transactions
                .Where(t => t.NormalizedMerchant == series.Name && t.UserId == userId && !t.IsRecurring)
                .OrderByDescending(t => t.TransactionDate)
                .FirstOrDefaultAsync();

            if (lastOccurrence != null)
            {
                series.LastOccurrence = lastOccurrence.TransactionDate;
                series.NextExpectedDate = lastOccurrence.TransactionDate.AddDays(series.RecurrenceInterval);
            }
        }

        await context.SaveChangesAsync();
    }

    private async Task CreateOrUpdateRecurringSeries(BudgetTrackerDbContext context, Guid userId, string merchant, TimeSpan avgInterval)
    {
        var existingSeries = await context.RecurringSeries
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Name == merchant);

        if (existingSeries != null)
        {
            existingSeries.RecurrenceInterval = (int)avgInterval.TotalDays;
            existingSeries.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            context.RecurringSeries.Add(new RecurringSeries
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = merchant,
                RecurrenceInterval = (int)avgInterval.TotalDays,
                RecurrenceType = avgInterval.TotalDays <= 7 ? RecurrenceType.Weekly : 
                                  avgInterval.TotalDays <= 14 ? RecurrenceType.BiWeekly : 
                                  avgInterval.TotalDays <= 35 ? RecurrenceType.Monthly : RecurrenceType.Custom,
                ExpectedAmount = 0, // Will be calculated later
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
    }
}
