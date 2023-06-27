using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Dapper;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        builder =>
        {
            builder.WithOrigins("http://localhost:4200")
                   .AllowAnyHeader()
                   .AllowAnyMethod();
        });
});
var app = builder.Build();

app.MapGet("/api/data/{page}/{pageSize}", async (int page, int pageSize) =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    using var connection = new SqliteConnection(connectionString);
    
    var totalCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Data");
    var data = await connection.QueryAsync<Data>("SELECT * FROM Data LIMIT @PageSize OFFSET @Offset", 
        new { PageSize = pageSize, Offset = page * pageSize });

    return Results.Ok(new { totalCount = totalCount, data = data });
});

app.MapGet("/api/data/search/{page}/{pageSize}", async (int page, int pageSize, string searchTerm) => 
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    using var connection = new SqliteConnection(connectionString);

    var totalCount = 0;
    var data = Enumerable.Empty<Data>();

    if (string.IsNullOrEmpty(searchTerm))
    {
        totalCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Data");
        data = await connection.QueryAsync<Data>("SELECT * FROM Data LIMIT @PageSize OFFSET @Offset", 
        new { PageSize = pageSize, Offset = page * pageSize });
    }
    else
    {
        totalCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Data WHERE Name LIKE @SearchTerm",
        new { SearchTerm = $"%{searchTerm}%" });

        data = await connection.QueryAsync<Data>("SELECT * FROM Data WHERE Name LIKE @SearchTerm LIMIT @PageSize OFFSET @Offset", 
        new { SearchTerm = $"%{searchTerm}%", PageSize = pageSize, Offset = page * pageSize });
    }

    return Results.Ok(new { totalCount = totalCount, data = data });
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

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseCors("AllowSpecificOrigin");
app.Run();
