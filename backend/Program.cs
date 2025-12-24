using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.FileProviders;
using System.Text;
using System.Text.RegularExpressions;
using InvoiceExpenseSystem.Data;
using InvoiceExpenseSystem.Services;
using DotNetEnv;

// Load environment variables from .env file (if it exists)
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialize enums as strings instead of numbers
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Configure Forwarded Headers for Render (fixes Secure cookie issues)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Production Priority: Use Render's environment variables if available
var dbConnectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");

if (string.IsNullOrEmpty(dbConnectionString))
{
    // Fallback to local config only if Render variable is missing
    dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    Console.WriteLine("ℹ Using fallback/local connection string");
}
else
{
    // Log a masked version for debugging to confirm we are using the Render variable
    var maskedConnectionString = Regex.Replace(dbConnectionString, @"Password=[^;]+", "Password=****");
    Console.WriteLine($"ℹ Using PRODUCTION connection string: {maskedConnectionString}");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(dbConnectionString));

// JWT Authentication - read from environment variables or config
var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") 
    ?? builder.Configuration["Jwt:Key"] 
    ?? "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!";
var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") 
    ?? builder.Configuration["Jwt:Issuer"] 
    ?? "InvoiceExpenseSystem";
var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") 
    ?? builder.Configuration["Jwt:Audience"] 
    ?? "InvoiceExpenseSystem";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        // Read token from cookie if not in Authorization header
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // First try to get token from Authorization header
                var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
                
                // If not in header, try to get from cookie
                if (string.IsNullOrEmpty(token))
                {
                    token = context.Request.Cookies["authToken"];
                }

                if (!string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = new List<string> 
        { 
            "http://localhost:3000", 
            "https://expenses-home-assignment.onrender.com",
            "https://expenses-home-assignment-1.onrender.com" // Added this one
        };
        
        // Also allow adding an origin via environment variable for flexibility
        var envOrigin = Environment.GetEnvironmentVariable("FRONTEND_URL");
        if (!string.IsNullOrEmpty(envOrigin))
        {
            allowedOrigins.Add(envOrigin);
        }

        policy.WithOrigins(allowedOrigins.ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();

var app = builder.Build();

// Ensure database is created and migrations are applied (only for development)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        dbContext.Database.EnsureCreated();
        
        // Try to add DocumentType column if it doesn't exist
        try
        {
            var connection = dbContext.Database.GetDbConnection();
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            
            // Check for DocumentType column
            command.CommandText = "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'Expenses' AND column_name = 'DocumentType';";
            var columnExists = Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
            
            if (!columnExists)
            {
                Console.WriteLine("Adding DocumentType column to Expenses table...");
                command.CommandText = @"
                    ALTER TABLE ""Expenses"" 
                    ADD COLUMN ""DocumentType"" text DEFAULT 'Receipt';
                    
                    UPDATE ""Expenses"" 
                    SET ""DocumentType"" = CASE 
                        WHEN ""IsReceipt"" = true THEN 'Receipt'
                        ELSE 'TaxInvoice'
                    END
                    WHERE ""DocumentType"" IS NULL;
                ";
                await command.ExecuteNonQueryAsync();
                Console.WriteLine("✓ DocumentType column added successfully!");
            }
            else
            {
                Console.WriteLine("✓ DocumentType column already exists");
            }

            // Check for FileName column
            command.CommandText = "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'Expenses' AND column_name = 'FileName';";
            var fileColumnExists = Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
            
            if (!fileColumnExists)
            {
                Console.WriteLine("Adding FileName column to Expenses table...");
                command.CommandText = @"ALTER TABLE ""Expenses"" ADD COLUMN ""FileName"" text;";
                await command.ExecuteNonQueryAsync();
                Console.WriteLine("✓ FileName column added successfully!");
            }
            else
            {
                Console.WriteLine("✓ FileName column already exists");
            }
            
            await connection.CloseAsync();
            Console.WriteLine("✓ Database schema is up to date!");
        }
        catch (Exception colEx)
        {
            // Column might already exist, or there's another issue
            Console.WriteLine($"⚠ Schema update error: {colEx.Message}");
        }
        
        Console.WriteLine("✓ Database and tables are ready!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Database error: {ex.Message}");
        Console.WriteLine("\nTroubleshooting:");
        Console.WriteLine("1. Make sure PostgreSQL is running");
        Console.WriteLine("2. Verify username and password in appsettings.json");
        Console.WriteLine("3. Create the database manually:");
        Console.WriteLine("   - Open pgAdmin or psql");
        Console.WriteLine("   - Run: CREATE DATABASE invoiceexpensesystem;");
        Console.WriteLine("4. Add DocumentType column manually:");
        Console.WriteLine("   ALTER TABLE \"Expenses\" ADD COLUMN \"DocumentType\" text DEFAULT 'Receipt';");
        Console.WriteLine("5. Then restart the server");
    }
}

// Ensure Uploads directory exists
var uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "Uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

// Configure the HTTP request pipeline
// Enable Swagger in all environments
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Invoice Expense System API V1");
    c.RoutePrefix = "swagger";
});

// Serve uploaded files
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/api/uploads"
});

app.UseForwardedHeaders();

// Skip HTTPS redirection for now (causes issues when only HTTP is available)
// app.UseHttpsRedirection();

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

