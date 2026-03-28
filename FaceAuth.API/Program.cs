using FaceAuth.API.Application.Interfaces;
using FaceAuth.API.Infrastructure.Data;
using FaceAuth.API.Infrastructure.Repositories;
using FaceAuth.API.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// Configuração dos Serviços (Dependency Injection)
// ============================================

// Configurar PostgreSQL com Entity Framework Core
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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
builder.Services.AddOpenApi();

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
    app.MapOpenApi();
}

app.UseCors("AllowAll");
app.MapControllers();

Console.WriteLine("🚀 FaceAuth API rodando!");
Console.WriteLine("📡 Endpoints disponíveis:");
Console.WriteLine("   POST /api/auth/register     → Cadastro de usuário");
Console.WriteLine("   POST /api/auth/authenticate  → Autenticação facial");

app.Run();
