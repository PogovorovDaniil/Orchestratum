using Microsoft.EntityFrameworkCore;
using Orchestratum.Example.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();

// Configure HttpClient with base address for Blazor Server components
builder.Services.AddScoped(sp =>
{
    var navigationManager = sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(navigationManager.BaseUri) };
});

// Add Serilog
builder.Services.AddSerilog();

// Register application services
builder.Services.AddSingleton<OrderService>();
builder.Services.AddSingleton<PaymentService>();
builder.Services.AddSingleton<NotificationService>();

// Configure Orchestratum
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Username=root;Password=root;Database=orchestratum_example";

builder.Services.AddOchestratum(opts =>
{
    opts.InstanceKey = "example_instance";
    opts.ConfigureDbContext(db => db.UseNpgsql(connectionString));
    opts.CommandPollingInterval = TimeSpan.FromSeconds(1);

    // Register commands and handlers
    opts.RegisterCommands(typeof(Program).Assembly);
    opts.RegisterHandlers(typeof(Program).Assembly);
});

var app = builder.Build();

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<Orchestratum.Example.Components.App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

app.Run();