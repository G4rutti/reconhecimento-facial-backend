using FaceAuth.API.Application.Interfaces;
using FaceAuth.API.Infrastructure.Data;
using FaceAuth.API.Infrastructure.Repositories;
using FaceAuth.API.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configurar porta dinâmica (Railway injeta a variável PORT)
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// ============================================
// Configuração dos Serviços (Dependency Injection)
// ============================================

// Configuração do Banco de Dados para Railway e Local
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
var pgHost = Environment.GetEnvironmentVariable("PGHOST");

if (!string.IsNullOrEmpty(databaseUrl))
{
    var isUri = Uri.TryCreate(databaseUrl, UriKind.Absolute, out var databaseUri);
    if (isUri && databaseUri != null)
    {
        var userInfo = databaseUri.UserInfo.Split(':');
        connectionString = $"Host={databaseUri.Host};Port={databaseUri.Port};Database={databaseUri.LocalPath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SslMode=Require;TrustServerCertificate=True;";
        Console.WriteLine($"[DB] Conectando via DATABASE_URL ({databaseUri.Host})");
    }
}
else if (!string.IsNullOrEmpty(pgHost))
{
    var pgPort = Environment.GetEnvironmentVariable("PGPORT") ?? "5432";
    var pgUser = Environment.GetEnvironmentVariable("PGUSER");
    var pgPass = Environment.GetEnvironmentVariable("PGPASSWORD");
    var pgDb = Environment.GetEnvironmentVariable("PGDATABASE");

    connectionString = $"Host={pgHost};Port={pgPort};Database={pgDb};Username={pgUser};Password={pgPass};SslMode=Require;TrustServerCertificate=True;";
    Console.WriteLine($"[DB] Conectando via variáveis PG* ({pgHost}:{pgPort})");
}
else
{
    // TESTE
    Console.WriteLine("[DB] ALERTA: Nenhuma variável do Railway encontrada. Usando config local (localhost). Se estiver na nuvem, lembre-se de linkar o banco ao serviço!");
}

// Configurar PostgreSQL com Entity Framework Core
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Registrar repositórios
builder.Services.AddScoped<UserRepository>();

// Registrar serviços de reconhecimento facial (Singleton para evitar recarregar modelos)
builder.Services.AddSingleton<IFaceService, FaceService>();

// Registrar serviço de usuários
builder.Services.AddScoped<IUserService, UserService>();

// Configurar controllers
builder.Services.AddControllers();

// Configurar Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configurar CORS (permitir acesso do frontend)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ============================================
// Aplicar migrations automaticamente ao iniciar
// ============================================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    Console.WriteLine("✅ Migrations aplicadas com sucesso!");
}

// ============================================
// Configuração do Pipeline HTTP
// ============================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.MapControllers();

Console.WriteLine("🚀 FaceAuth API rodando!");
Console.WriteLine("📡 Endpoints disponíveis:");
Console.WriteLine("   POST /api/auth/register     → Cadastro de usuário");
Console.WriteLine("   POST /api/auth/authenticate  → Autenticação facial");

app.Run();
