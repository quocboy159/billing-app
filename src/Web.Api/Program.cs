using Application;
using Infrastructure;
using Infrastructure.Database;
using Web.Api.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpoints(typeof(Program).Assembly);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts =>
{
    // Use the full type name so nested DTOs (e.g. GetForExport.Request, GetProfitLoss.Request)
    // get unique schema IDs instead of colliding on the simple name "Request".
    opts.CustomSchemaIds(t => t.FullName?.Replace("+", "."));
});

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();

    await app.Services.ApplyMigrationsAndSeedAsync();
}

app.UseHttpsRedirection();
app.MapEndpoints();
app.Run();

public partial class Program;
