using FutureTech.StudentManagement.Models.Entities;
using Microsoft.Azure.Cosmos;

namespace FutureTech.StudentManagement.Services;

public class CosmosDbService
{
    private readonly Container _container;
    private readonly ILogger<CosmosDbService> _logger;

    public CosmosDbService(IConfiguration configuration, ILogger<CosmosDbService> logger)
    {
        var connectionString = configuration["CosmosDb:ConnectionString"]
            ?? throw new InvalidOperationException("CosmosDb:ConnectionString is missing.");
        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "StudentManagementDb";
        var containerName = configuration["CosmosDb:ContainerName"] ?? "Students";

        var client = new CosmosClient(connectionString, new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        });

        var database = client.CreateDatabaseIfNotExistsAsync(databaseName).GetAwaiter().GetResult();
        var container = database.Database.CreateContainerIfNotExistsAsync(new ContainerProperties(containerName, "/id")).GetAwaiter().GetResult();

        _container = container.Container;
        _logger = logger;
    }

    public async Task<Student?> GetStudentAsync(string id)
    {
        try
        {
            var response = await _container.ReadItemAsync<Student>(id, new PartitionKey(id));
            return response.Resource.IsDeleted ? null : response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Student?> GetStudentByEmailAsync(string email)
    {
        const string sql = "SELECT TOP 1 * FROM c WHERE c.isDeleted = false AND LOWER(c.email) = @email";
        var query = new QueryDefinition(sql).WithParameter("@email", email.Trim().ToLowerInvariant());
        using var iterator = _container.GetItemQueryIterator<Student>(query, requestOptions: new QueryRequestOptions { MaxItemCount = 1 });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            var student = response.FirstOrDefault();
            if (student != null)
            {
                return student;
            }
        }

        return null;
    }

    public async Task<(List<Student> Students, int TotalCount)> GetStudentsAsync(string? searchTerm, int pageNumber, int pageSize)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim().ToLowerInvariant();

        var whereClause = "WHERE c.isDeleted = false";
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            whereClause += " AND (CONTAINS(LOWER(c.firstName), @search) OR CONTAINS(LOWER(c.lastName), @search) OR CONTAINS(LOWER(c.email), @search) OR CONTAINS(LOWER(c.id), @search))";
        }

        var countQuery = new QueryDefinition($"SELECT VALUE COUNT(1) FROM c {whereClause}");
        var listQuery = new QueryDefinition($"SELECT * FROM c {whereClause} ORDER BY c.createdAt DESC OFFSET @offset LIMIT @limit")
            .WithParameter("@offset", (pageNumber - 1) * pageSize)
            .WithParameter("@limit", pageSize);

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            countQuery.WithParameter("@search", normalizedSearch);
            listQuery.WithParameter("@search", normalizedSearch);
        }

        var totalCount = 0;
        using (var countIterator = _container.GetItemQueryIterator<int>(countQuery))
        {
            while (countIterator.HasMoreResults)
            {
                var response = await countIterator.ReadNextAsync();
                totalCount = response.FirstOrDefault();
            }
        }

        var students = new List<Student>();
        using (var listIterator = _container.GetItemQueryIterator<Student>(listQuery))
        {
            while (listIterator.HasMoreResults)
            {
                var response = await listIterator.ReadNextAsync();
                students.AddRange(response);
            }
        }

        return (students, totalCount);
    }

    public async Task<Student> CreateStudentAsync(Student student)
    {
        student.CreatedAt = DateTime.UtcNow;
        var response = await _container.CreateItemAsync(student, new PartitionKey(student.Id));
        _logger.LogInformation("Student created: {StudentId}", student.Id);
        return response.Resource;
    }

    public async Task<Student> UpdateStudentAsync(Student student)
    {
        student.UpdatedAt = DateTime.UtcNow;
        var response = await _container.UpsertItemAsync(student, new PartitionKey(student.Id));
        _logger.LogInformation("Student updated: {StudentId}", student.Id);
        return response.Resource;
    }

    public async Task SoftDeleteStudentAsync(string id)
    {
        var student = await GetStudentAsync(id);
        if (student == null)
        {
            return;
        }

        student.IsDeleted = true;
        student.DeletedAt = DateTime.UtcNow;
        student.EnrolmentStatus = EnrolmentStatus.Inactive;
        await UpdateStudentAsync(student);
        _logger.LogInformation("Student soft deleted: {StudentId}", id);
    }

    public async Task HardDeleteStudentAsync(string id)
    {
        await _container.DeleteItemAsync<Student>(id, new PartitionKey(id));
        _logger.LogInformation("Student permanently deleted: {StudentId}", id);
    }
}
