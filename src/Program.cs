using System.Text;
using LLMChatbotApi.Config;
using LLMChatbotApi.exceptions;
using LLMChatbotApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using MySqlConnector;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);


builder.Services.Configure<Settings>(
    builder.Configuration.GetSection("Settings")
);

// Configuração da autenticação JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.ASCII.GetBytes(builder.Configuration["Settings:TokenPrivateKey"]!)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var redisService = context.HttpContext.RequestServices.GetRequiredService<DatabaseRedisService>();

                var token = context.HttpContext.Request.Headers["Authorization"]
                    .ToString()
                    .Replace("Bearer ", "");

                if (string.IsNullOrEmpty(token))
                {
                    context.Fail("Token não encontrado");
                    return;
                }

                var db = redisService.GetDatabase();
                var exists = await db.KeyExistsAsync($"session:{token}");

                if (!exists)
                {
                    context.Fail("Token revogado ou sessão expirada");
                }
            }
        };
    }); ;

// Configura a conexão com o serviço do MySQL
builder.Services.AddScoped(provider =>
    new MySqlConnection(builder.Configuration.GetConnectionString("MySQL")));

builder.Services.AddScoped<DatabaseMySQLService>();

// Configura a conexão com o serviço do MonogDB
var mongoConnection = builder.Configuration.GetConnectionString("MongoDB");
//O erro ta no "MongoDatabase"
var mongoDatabase = builder.Configuration.GetConnectionString("MongoDatabase");

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnection));
builder.Services.AddScoped(provider =>
    provider.GetRequiredService<IMongoClient>().GetDatabase(mongoDatabase));
builder.Services.AddScoped<DatabaseMongoDBService>();
builder.Services.AddScoped<MessageManagementService>();
// Configura a conexão com o serviço do Redis
builder.Services.AddScoped<DatabaseRedisService>(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var logger = provider.GetRequiredService<ILogger<DatabaseRedisService>>();
    var connectionString = config.GetConnectionString("Redis")
        ?? throw new ArgumentNullException("ConnectionStrings:Redis");
    return new DatabaseRedisService(connectionString, logger);
});


builder.Services.AddScoped<TokenService>();
// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Irá mostrar se conectou com os Bancos
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var mysqlService = scope.ServiceProvider.GetRequiredService<DatabaseMySQLService>();
    var mongoService = scope.ServiceProvider.GetRequiredService<DatabaseMongoDBService>();
    var redisService = scope.ServiceProvider.GetRequiredService<DatabaseRedisService>();

    try
    {
        await mysqlService.VerifyConnection();
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Falha crítica na conexão com o MySQL");
        throw new DatabaseConnectionException("Erro MySQL: ", ex);
    }

    try
    {
        await mongoService.VerifyConnection();
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Falha crítica na conexão com o MongoDB");
        throw new DatabaseConnectionException("Erro MongoDB: ", ex);
    }

    try
    {
        await redisService.VerifyConnection();
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
