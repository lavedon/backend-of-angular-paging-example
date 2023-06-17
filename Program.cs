using Microsoft.Data.Sqlite;
using Dapper;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/api/data/{page}", async (int page) =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    using var connection = new SqliteConnection(connectionString);
    
    int pageSize = 50; // Set the number of records per page
    var data = await connection.QueryAsync<Data>("SELECT * FROM Data LIMIT @PageSize OFFSET @Offset", 
        new { PageSize = pageSize, Offset = page * pageSize });

    return Results.Ok(data);
});

app.MapPost("/api/populate", async () =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    using var connection = new SqliteConnection(connectionString);

    var testUsers = new Bogus.Faker<Data>()
        .RuleFor(u => u.Name, f => f.Name.FullName())
        .RuleFor(u => u.Occupation, f => f.Name.JobTitle())
        .RuleFor(u => u.Age, f => f.Random.Number(20, 70))  // Generates a random number between 20 and 70
        .RuleFor(u => u.Email, f => f.Internet.Email())
        .Generate(100000);  // Generate 100,000 rows of data

    await connection.OpenAsync();

    using var transaction = await connection.BeginTransactionAsync();

    foreach (var user in testUsers)
    {
        await connection.ExecuteAsync(
            "INSERT INTO Data (Name, Occupation, Age, Email) VALUES (@Name, @Occupation, @Age, @Email)", 
        user, transaction);
    }

    await transaction.CommitAsync();

    return Results.Ok();
});

app.Run();
