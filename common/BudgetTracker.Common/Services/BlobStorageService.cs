using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace BudgetTracker.Common.Services;

public interface IBlobStorageService
{
    Task<string> UploadFileAsync(string containerName, string fileName, Stream fileStream, string? contentType = null);
    Task<Stream> DownloadFileAsync(string containerName, string fileName);
    Task<bool> DeleteFileAsync(string containerName, string fileName);
    Task<string> GetFileUrlAsync(string containerName, string fileName);
    Task<bool> FileExistsAsync(string containerName, string fileName);
}

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IConfiguration _configuration;

    public BlobStorageService(IConfiguration configuration)
    {
        _configuration = configuration;
        var connectionString = configuration.GetConnectionString("AzureStorage");
        _blobServiceClient = new BlobServiceClient(connectionString);
    }

    public async Task<string> UploadFileAsync(string containerName, string fileName, Stream fileStream, string? contentType = null)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

        var blobClient = containerClient.GetBlobClient(fileName);
        
        var options = new BlobHttpHeaders();
        if (!string.IsNullOrEmpty(contentType))
        {
            options.ContentType = contentType;
        }

        await blobClient.UploadAsync(fileStream, new BlobUploadOptions
        {
            HttpHeaders = options
        });

        return blobClient.Uri.ToString();
    }

    public async Task<Stream> DownloadFileAsync(string containerName, string fileName)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(fileName);

        if (!await blobClient.ExistsAsync())
        {
            throw new FileNotFoundException($"File {fileName} not found in container {containerName}");
        }

        var response = await blobClient.DownloadAsync();
        return response.Value.Content;
    }

    public async Task<bool> DeleteFileAsync(string containerName, string fileName)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(fileName);

        var response = await blobClient.DeleteIfExistsAsync();
        return response.Value;
    }

    public async Task<string> GetFileUrlAsync(string containerName, string fileName)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(fileName);

        if (!await blobClient.ExistsAsync())
        {
            throw new FileNotFoundException($"File {fileName} not found in container {containerName}");
        }

        return blobClient.Uri.ToString();
    }

    public async Task<bool> FileExistsAsync(string containerName, string fileName)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(fileName);

        var response = await blobClient.ExistsAsync();
        return response.Value;
    }
}