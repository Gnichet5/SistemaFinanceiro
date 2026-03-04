using Microsoft.EntityFrameworkCore;
using ItauCorretora.Infrastructure.Data;
using ItauCorretora;
using Serilog; 
// Novos usings para a Rentabilidade e o Parser
using ItauCorretora.Application.Interfaces;
using ItauCorretora.Application.Services;
using ItauCorretora.Infrastructure.Parsers;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Iniciando a API da Itaú Corretora...");

    var builder = WebApplication.CreateBuilder(args);
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
            
    builder.Services.AdicionarSistemaComprasProgramadas(builder.Configuration);

    builder.Services.AddScoped<MotorCompraService>();
    builder.Services.AddScoped<IRebalanceamentoService, RebalanceamentoService>();
    
    builder.Services.AddScoped<IRentabilidadeService, RentabilidadeService>();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend",
            policy =>
            {
                policy.WithOrigins("http://localhost:3000") 
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
    });
    var app = builder.Build();
    
    app.UseSerilogRequestLogging(); 

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseCors("AllowFrontend");
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