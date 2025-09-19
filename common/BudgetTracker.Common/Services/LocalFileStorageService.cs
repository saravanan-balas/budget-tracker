using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BudgetTracker.Common.Services;

public class LocalFileStorageService : IBlobStorageService
{
    private readonly ILogger<LocalFileStorageService> _logger;
    private readonly string _basePath;

    public LocalFileStorageService(IConfiguration configuration, ILogger<LocalFileStorageService> logger)
    {
        _logger = logger;
        _basePath = configuration["LocalStorage:BasePath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        
        // Ensure the base directory exists
        Directory.CreateDirectory(_basePath);
        _logger.LogInformation("Local file storage initialized at: {BasePath}", _basePath);
    }

    public async Task<string> UploadFileAsync(string containerName, string fileName, Stream fileStream, string? contentType = null)
    {
        try
        {
            var containerPath = Path.Combine(_basePath, containerName);
            Directory.CreateDirectory(containerPath);

            var filePath = Path.Combine(containerPath, fileName);
            
            // Ensure directory for nested paths exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var fileStreamOutput = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            await fileStream.CopyToAsync(fileStreamOutput);

            var relativePath = Path.Combine(containerName, fileName).Replace('\\', '/');
            var fileUrl = $"file:///{relativePath}";
            
            _logger.LogInformation("File uploaded successfully: {FileName} -> {FilePath}", fileName, filePath);
            return fileUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName} to container {ContainerName}", fileName, containerName);
            throw;
        }
    }

    public Task<Stream> DownloadFileAsync(string containerName, string fileName)
    {
        try
        {
            var filePath = Path.Combine(_basePath, containerName, fileName);
            
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            _logger.LogInformation("File downloaded successfully: {FilePath}", filePath);
            return Task.FromResult<Stream>(fileStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file {FileName} from container {ContainerName}", fileName, containerName);
            throw;
        }
    }

    public Task<bool> DeleteFileAsync(string containerName, string fileName)
    {
        try
        {
            var filePath = Path.Combine(_basePath, containerName, fileName);
            
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("File deleted successfully: {FilePath}", filePath);
                return Task.FromResult(true);
            }
            
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {FileName} from container {ContainerName}", fileName, containerName);
            return Task.FromResult(false);
        }
    }

    public Task<string> GetFileUrlAsync(string containerName, string fileName)
    {
        try
        {
            var relativePath = Path.Combine(containerName, fileName).Replace('\\', '/');
            return Task.FromResult($"file:///{relativePath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file URL for {FileName} in container {ContainerName}", fileName, containerName);
            throw;
        }
    }

    public Task<bool> FileExistsAsync(string containerName, string fileName)
    {
        try
        {
            var filePath = Path.Combine(_basePath, containerName, fileName);
            return Task.FromResult(File.Exists(filePath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking file existence {FileName} in container {ContainerName}", fileName, containerName);
            return Task.FromResult(false);
        }
    }
}