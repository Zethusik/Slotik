using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Slotik.Data;
using Slotik.Services;
using System.Text;
using System.Text.Json.Serialization;

namespace Slotik;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var connectionString =
            builder.Configuration.GetConnectionString("DefaultConnection");

        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        // JWT
        var jwtSettings =
            builder.Configuration.GetSection("JwtSettings");

        var secretKey =
            jwtSettings["SecretKey"]!;

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme =
                    JwtBearerDefaults.AuthenticationScheme;

                options.DefaultChallengeScheme =
                    JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters =
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,

                        ValidIssuer =
                            jwtSettings["Issuer"],

                        ValidAudience =
                            jwtSettings["Audience"],

                        IssuerSigningKey =
                            new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(secretKey))
                    };
            });

        // Controllers
        builder.Services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.ReferenceHandler =
                    ReferenceHandler.IgnoreCycles;
            });

        builder.Services.AddEndpointsApiExplorer();

        // Swagger
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(
                "v1",
                new OpenApiInfo
                {
                    Title = "Slotik API",
                    Version = "v2"
                });

            options.AddSecurityDefinition(
                "Bearer",
                new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter JWT token"
                });

            options.AddSecurityRequirement(
                new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference =
                                new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                        },
                        Array.Empty<string>()
                    }
                });
        });

        // Services
        builder.Services.AddScoped<TokenService>();

        builder.Services.AddCors(options =>
            options.AddPolicy(
                "front",
                policy =>
                    policy
                        .SetIsOriginAllowed(origin =>
                            origin.StartsWith("http://localhost:")
                            || origin.EndsWith(".vercel.app")
                            || origin.Contains("slotik"))
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials()));

        builder.Services.Configure<SmtpSettings>(
            builder.Configuration.GetSection("SmtpSettings"));

        builder.Services.AddScoped<SmtpSettings>();
        builder.Services.AddScoped<EmailService>();

        builder.Services.Configure<CloudinarySettings>(
            builder.Configuration.GetSection("CloudinarySettings"));

        builder.Services.AddScoped<IPhotoService, PhotoService>();

        // LiqPay
        builder.Services
            .AddOptions<LiqPaySettings>()
            .Bind(builder.Configuration.GetSection("LiqPay"))
            .Validate(
                settings =>
                    !string.IsNullOrWhiteSpace(settings.PublicKey)
                    && !string.IsNullOrWhiteSpace(settings.PrivateKey),
                "LiqPay PublicKey and PrivateKey are required.")
            .Validate(
                settings =>
                    Uri.TryCreate(
                        settings.ServerUrl,
                        UriKind.Absolute,
                        out var serverUri)
                    && serverUri.Scheme == Uri.UriSchemeHttps,
                "LiqPay ServerUrl must be a valid HTTPS URL.")
            .Validate(
                settings =>
                    Uri.TryCreate(
                        settings.ResultUrl,
                        UriKind.Absolute,
                        out var resultUri)
                    && (
                        resultUri.Scheme == Uri.UriSchemeHttp
                        || resultUri.Scheme == Uri.UriSchemeHttps
                    ),
                "LiqPay ResultUrl must be a valid HTTP/HTTPS URL.")
            .Validate(
                settings =>
                    settings.BasicPriceUah > 0
                    && settings.ProPriceUah > 0,
                "LiqPay tariff prices must be greater than zero.")
            .ValidateOnStart();

        builder.Services
            .AddHttpClient<LiqPayService>(client =>
            {
                client.BaseAddress =
                    new Uri("https://www.liqpay.ua/");

                client.Timeout =
                    TimeSpan.FromSeconds(30);
            });

        var app = builder.Build();

        // Database
        using (var scope = app.Services.CreateScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<AppDbContext>();

            await context.Database.MigrateAsync();
            await DbInitializer.SeedDataAsync(context);
        }

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        /*
         * Для локального ngrok пока выключено
         */

        // app.UseHttpsRedirection();

        app.UseAuthentication();
        app.UseCors("front");
        app.UseAuthorization();

        app.MapControllers();

        await app.RunAsync();
    }
}