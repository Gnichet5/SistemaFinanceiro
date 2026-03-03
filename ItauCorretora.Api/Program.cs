using Microsoft.EntityFrameworkCore;
using ItauCorretora.Infrastructure.Data;
using ItauCorretora;
using Serilog; 
using ItauCorretora.Application.Interfaces;
using ItauCorretora.Application.Services;
using ItauCorretora.Infrastructure.Parsers;
using ItauCorretora.Domain.Interfaces;
using ItauCorretora.Infrastructure.Repositories;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Iniciando a API da Itaú Corretora...");

    var builder = WebApplication.CreateBuilder(args);

    // Add Azure Key Vault configuration
    var keyVaultName = builder.Configuration["KeyVaultName"];
    if (!string.IsNullOrEmpty(keyVaultName))
    {
        var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
        builder.Configuration.AddAzureKeyVault(
            keyVaultUri,
            new DefaultAzureCredential(),
            new AzureKeyVaultConfigurationOptions
            {
                ReloadInterval = TimeSpan.FromHours(1)
            });
    }

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
            mySqlOptions => 
            {
                mySqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3, 
                    maxRetryDelay: TimeSpan.FromSeconds(5), 
                    errorNumbersToAdd: null);
            }));

   
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowNextJS", policy =>
        {
            policy.WithOrigins("http://localhost:3000")
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });
            
    builder.Services.AdicionarSistemaComprasProgramadas(builder.Configuration);

    builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
    builder.Services.AddScoped<IHistoricoCestaRepository, HistoricoCestaRepository>();

    builder.Services.AddScoped<MotorCompraService>();
    builder.Services.AddScoped<IRebalanceamentoService, RebalanceamentoService>();
    builder.Services.AddScoped<IRentabilidadeService, RentabilidadeService>();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    
    var app = builder.Build();

    app.UseCors("AllowNextJS");

    app.UseMiddleware<ItauCorretora.Api.Middleware.ErrorHandlingMiddleware>();
    app.UseSerilogRequestLogging(); 

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "A aplicação falhou ao iniciar de forma inesperada.");
}
finally
{
    Log.CloseAndFlush();
}