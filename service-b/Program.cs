using EventStore.Client;
using ServiceB.Services;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT")
    ?? Environment.GetEnvironmentVariable("SERVICE_B_PORT")
    ?? "3002";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var eventStoreUrl = Environment.GetEnvironmentVariable("EVENTSTORE_URL")
    ?? builder.Configuration["EventStore:ConnectionString"]
    ?? "esdb://localhost:2113?tls=false";

Console.WriteLine("=================================================");
Console.WriteLine(" [Service-B] INICIANDO SERVICIO B (QUERY SIDE)");
Console.WriteLine($" [Service-B] Puerto de escucha: {port}");
Console.WriteLine($" [Service-B] EventStoreDB URL:  {eventStoreUrl}");
Console.WriteLine("=================================================");

var esSettings = EventStoreClientSettings.Create(eventStoreUrl);
var esClient = new EventStoreClient(esSettings);

builder.Services.AddSingleton(esClient);
builder.Services.AddSingleton<BattleReadStore>();
builder.Services.AddHostedService<BattleProjectionService>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();
app.UseCors();

app.MapGet("/health", (BattleReadStore store) => Results.Ok(new
{
    status = "healthy",
    service = "service-b",
    port,
    projectedBattles = store.Count
}));

app.MapGet("/battles", (BattleReadStore store) => Results.Ok(store.GetAll()));

app.MapGet("/battles/{id}", (string id, BattleReadStore store) =>
{
    var battle = store.GetDetail(id);
    return battle is null
        ? Results.NotFound(new { error = $"La batalla '{id}' no existe o aún no ha sido proyectada." })
        : Results.Ok(battle);
});

app.MapGet("/battles/{id}/stats", (string id, BattleReadStore store) =>
{
    var stats = store.GetStats(id);
    return stats is null
        ? Results.NotFound(new { error = $"No hay estadísticas para la batalla '{id}'." })
        : Results.Ok(stats);
});

app.MapGet("/battles/{id}/history", (string id, BattleReadStore store) =>
{
    var history = store.GetHistory(id);
    return history is null
        ? Results.NotFound(new { error = $"No hay historial para la batalla '{id}'." })
        : Results.Ok(history);
});

app.Run();
