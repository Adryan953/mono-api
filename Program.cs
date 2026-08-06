using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Mono.Api.Data;

// Fix Npgsql: evita InvalidCastException entre timestamptz e DateTime sem fuso
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Configuração do WebHost para escutar na porta 8080 externamente
builder.WebHost.UseUrls("http://0.0.0.0:8080");

// Configuração do CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        b => b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configuração do OpenAPI
builder.Services.AddOpenApi();

// Serviços de Integração
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<Mono.Api.Services.IGeminiService, Mono.Api.Services.GeminiService>();

// Banco de Dados (EF Core com PostgreSQL)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), 
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure()));

// Autenticação JWT (Supabase)
var supabaseSettings = builder.Configuration.GetSection("Supabase");
var secretKey = supabaseSettings["JwtSecret"] ?? "your-jwt-secret-your-jwt-secret-your-jwt";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

var app = builder.Build();

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => "Mono API Online!");

app.Run();
