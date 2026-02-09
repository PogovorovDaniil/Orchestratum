using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orchestratum.Example;
using Orchestratum.Extentions;
using Orchestratum.MediatR;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var builder = Host.CreateDefaultBuilder(args);
builder.ConfigureServices(services =>
{
    services.AddMediatR(opts => opts.RegisterServicesFromAssembly(typeof(Program).Assembly));
    services.AddSerilog();
    services.AddHostedService<Test1HostedService>();
    services.AddHostedService<Test2HostedService>();
    services.AddHostedService<Test3HostedService>();
    services.AddHostedService<Test4HostedService>();
    services.AddOchestratum((sp, opts) =>
    {
        opts.InstanceKey = "example-instance";
        opts
            .ConfigureDbContext(opts => opts.UseNpgsql("Host=localhost;Username=root;Password=root;Database=orchestratum_example"))
            .RegisterMediatR();
    });
});

builder.Build().Run();