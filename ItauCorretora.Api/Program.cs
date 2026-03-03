using Microsoft.EntityFrameworkCore;
using ItauCorretora.Infrastructure.Data;
using ItauCorretora;
using Serilog; 
// Novos usings para a Rentabilidade e o Parser
using ItauCorretora.Application.Interfaces;
using ItauCorretora.Application.Services;
using ItauCorretora.Infrastructure.Parsers;

// 1. Configuração do Serilog para capturar até os erros de inicialização (Bootstrap Logger)
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Iniciando a API da Itaú Corretora...");

    var builder = WebApplication.CreateBuilder(args);

    // 2. Substitui o Logger padrão do .NET pelo Serilog
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    
    // 3. Configuração do Banco de Dados COM Resiliência (Tolerância a Falhas)
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
            mySqlOptions => 
            {
                mySqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3, 
                    maxRetryDelay: TimeSpan.FromSeconds(5), 
                    errorNumbersToAdd: null);
            }));
            
    // Injeção dos serviços antigos do motor
    builder.Services.AdicionarSistemaComprasProgramadas(builder.Configuration);

    builder.Services.AddScoped<MotorCompraService>();
    builder.Services.AddScoped<IRebalanceamentoService, RebalanceamentoService>();
    // 4. Injeção de Dependência dos NOVOS serviços de Rentabilidade
    
    builder.Services.AddScoped<IRentabilidadeService, RentabilidadeService>();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    
    var app = builder.Build();
    
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