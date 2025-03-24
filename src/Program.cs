using LLMChatbotApi.exceptions;
using LLMChatbotApi.Services;
using MongoDB.Driver;
using MySqlConnector;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Configura a conexão com o serviço do MySQL
builder.Services.AddScoped(provider => 
    new MySqlConnection(builder.Configuration.GetConnectionString("MySQL")));

builder.Services.AddScoped<DatabaseMySQLService>();

// Configura a conexão com o serviço do MonogDB
var mongoConnection = builder.Configuration.GetConnectionString("MongoDB");
var mongoDatabase = builder.Configuration.GetConnectionString("MongoDatabase");

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnection));
builder.Services.AddScoped(provider => 
    provider.GetRequiredService<IMongoClient>().GetDatabase(mongoDatabase));
builder.Services.AddScoped<DatabaseMongoDBService>();

// Configura a conexão com o serviço do Redis
builder.Services.AddScoped<DatabaseRedisService>(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var logger = provider.GetRequiredService<ILogger<DatabaseRedisService>>();
    var connectionString = config.GetConnectionString("Redis") 
        ?? throw new ArgumentNullException("ConnectionStrings:Redis");
    return new DatabaseRedisService(connectionString, logger);
});

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Irá mostrar se conectou com os Bancos
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var mysqlService = scope.ServiceProvider.GetRequiredService<DatabaseMySQLService>();
    var mongoService = scope.ServiceProvider.GetRequiredService<DatabaseMongoDBService>();
    var redisService = scope.ServiceProvider.GetRequiredService<DatabaseRedisService>();

    try
    {
        await mysqlService.VerifyConnectionSQL();
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Falha crítica na conexão com o MySQL");
        throw new DatabaseConnectionException("Erro MySQL: ", ex);
    }

    try
    {
        await  mongoService.VerifyNoSQLCOnnection();
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Falha crítica na conexão com o MongoDB");
        throw new DatabaseConnectionException("Erro MongoDB: ", ex);
    }

    try
    {
        await redisService.VerifyConnectionRedis();
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Falha Crítica na conexão com o Redis");
        throw new DatabaseConnectionException("Erro Redis: ", ex);
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUi(options =>
    {
        options.DocumentPath = "/openapi/v1.json";
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
