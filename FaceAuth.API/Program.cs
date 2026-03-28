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

var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL") 
               ?? Environment.GetEnvironmentVariable("CONNECTION_STRING");

var pgHost = Environment.GetEnvironmentVariable("PGHOST") 
          ?? Environment.GetEnvironmentVariable("POSTGRES_HOST");

if (!string.IsNullOrEmpty(databaseUrl) && databaseUrl.StartsWith("postgres"))
{
    var isUri = Uri.TryCreate(databaseUrl, UriKind.Absolute, out var databaseUri);
    if (isUri && databaseUri != null)
    {
        var userInfo = databaseUri.UserInfo.Split(':');
        connectionString = $"Host={databaseUri.Host};Port={databaseUri.Port};Database={databaseUri.LocalPath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SslMode=Require;TrustServerCertificate=True;";
        Console.WriteLine($"[DB] Conectando via URL ({databaseUri.Host})");
    }
}
else if (!string.IsNullOrEmpty(pgHost))
{
    var pgPort = Environment.GetEnvironmentVariable("PGPORT") ?? Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
    var pgUser = Environment.GetEnvironmentVariable("PGUSER") ?? Environment.GetEnvironmentVariable("POSTGRES_USER");
    var pgPass = Environment.GetEnvironmentVariable("PGPASSWORD") ?? Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
    var pgDb = Environment.GetEnvironmentVariable("PGDATABASE") ?? Environment.GetEnvironmentVariable("POSTGRES_DB");

    connectionString = $"Host={pgHost};Port={pgPort};Database={pgDb};Username={pgUser};Password={pgPass};SslMode=Require;TrustServerCertificate=True;";
    Console.WriteLine($"[DB] Conectando via Variáveis Manuais ({pgHost}:{pgPort})");
}
else
{
    // TESTE
    Console.WriteLine("[DB] ALERTA: Nenhuma variável de banco encontrada. Usando config local (localhost).");
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

// Habilitar Swagger em todos os ambientes (inclusive no Railway)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.RoutePrefix = "docs"; // Muda a interface visual do /swagger para o /docs
});

// Redirecionar a rota principal ("/") direto para o painel do Swagger
app.MapGet("/", () => Results.Redirect("/docs"));

app.UseCors("AllowAll");
app.MapControllers();

Console.WriteLine("🚀 FaceAuth API rodando!");
Console.WriteLine("📡 Endpoints disponíveis:");
Console.WriteLine("   POST /api/auth/register     → Cadastro de usuário");
Console.WriteLine("   POST /api/auth/authenticate  → Autenticação facial");

app.Run();
