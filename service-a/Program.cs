using EventStore.Client;
using ServiceA.Models;
using ServiceA.Services;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "3001";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var eventStoreUrl = Environment.GetEnvironmentVariable("EVENTSTORE_URL") 
    ?? builder.Configuration["EventStore:ConnectionString"] 
    ?? "esdb://localhost:2113?tls=false";

Console.WriteLine("=================================================");
Console.WriteLine(" [Service-A] INICIANDO SERVICIO A (COMMAND SIDE)");
Console.WriteLine($" [Service-A] Puerto de escucha: {port}");
Console.WriteLine($" [Service-A] EventStoreDB URL:  {eventStoreUrl}");
Console.WriteLine("=================================================");

var esSettings = EventStoreClientSettings.Create(eventStoreUrl);
var esClient = new EventStoreClient(esSettings);

builder.Services.AddSingleton(esClient);
builder.Services.AddScoped<EventStoreService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "service-a", port }));

app.MapPost("/battles/start", async (StartBattleRequest? request, EventStoreService esService) =>
{
    var battleId = string.IsNullOrWhiteSpace(request?.BattleId) 
        ? $"battle-{Guid.NewGuid().ToString()[..8]}" 
        : request.BattleId;

    var streamName = EventStoreService.GetStreamName(battleId);

    var existingBattle = await esService.ReplayBattleAsync(battleId);
    if (existingBattle.Exists)
    {
        return Results.Conflict(new { error = $"La batalla '{battleId}' ya fue iniciada previamente." });
    }

    var heroName = string.IsNullOrWhiteSpace(request?.HeroName) ? "Guerrero" : request.HeroName;
    var heroHp = request?.HeroHp is > 0 ? request.HeroHp.Value : 100;
    var enemyName = string.IsNullOrWhiteSpace(request?.EnemyName) ? "Dragón" : request.EnemyName;
    var enemyHp = request?.EnemyHp is > 0 ? request.EnemyHp.Value : 100;

    var battleStartedEvent = new BattleStarted(
        battleId,
        heroName,
        heroHp,
        enemyName,
        enemyHp,
        DateTime.UtcNow
    );

    await esService.AppendEventAsync(battleId, nameof(BattleStarted), battleStartedEvent);

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[Service-A] Append BattleStarted {streamName} {heroName} ({heroHp} HP) vs {enemyName} ({enemyHp} HP)");
    Console.ResetColor();

    return Results.Created($"/battles/{battleId}", new
    {
        message = "Combate iniciado exitosamente",
        battleId,
        stream = streamName,
        hero = new { name = heroName, hp = heroHp },
        enemy = new { name = enemyName, hp = enemyHp }
    });
});

app.MapPost("/battles/{id}/attack", async (string id, AttackRequest? request, EventStoreService esService) =>
{
    var streamName = EventStoreService.GetStreamName(id);

    var battle = await esService.ReplayBattleAsync(id);

    var attacker = string.IsNullOrWhiteSpace(request?.Attacker) ? "Hero" : request.Attacker;
    var isAttackerHero = attacker.Equals("Hero", StringComparison.OrdinalIgnoreCase) || attacker.Equals(battle.HeroName, StringComparison.OrdinalIgnoreCase);
    var target = isAttackerHero ? battle.EnemyName : battle.HeroName;

    if (!battle.CanAttack(attacker, target, out var validationError))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[Service-A] REJECTED Attack on {streamName}: {validationError}");
        Console.ResetColor();

        return Results.BadRequest(new
        {
            error = validationError,
            battleId = id,
            isFinished = battle.IsFinished,
            winner = battle.Winner
        });
    }

    var targetCurrentHp = isAttackerHero ? battle.EnemyCurrentHp : battle.HeroCurrentHp;
    var targetMaxHp = isAttackerHero ? battle.EnemyMaxHp : battle.HeroMaxHp;
    var damage = request?.Damage is > 0 ? request.Damage.Value : Random.Shared.Next(15, 30);
    var isCritical = damage >= 25;
    var remainingHp = Math.Max(0, targetCurrentHp - damage);

    var attackEvent = new AttackPerformed(
        id,
        attacker,
        target,
        damage,
        remainingHp,
        isCritical,
        DateTime.UtcNow
    );

    await esService.AppendEventAsync(id, nameof(AttackPerformed), attackEvent);

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"[Service-A] Append AttackPerformed {streamName} {attacker} dealt {damage} damage to {target} (Remaining HP: {remainingHp}/{targetMaxHp}){(isCritical ? " [¡CRÍTICO!]" : "")}");
    if (remainingHp <= 0)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[Service-A] *** ¡COMBATE FINALIZADO! {attacker} ha derrotado a {target}! ***");
    }
    Console.ResetColor();

    return Results.Ok(new
    {
        message = $"Ataque ejecutado exitosamente por {attacker}",
        battleId = id,
        attacker,
        target,
        damage,
        isCritical,
        targetRemainingHp = remainingHp,
        targetMaxHp,
        battleFinished = remainingHp <= 0,
        winner = remainingHp <= 0 ? attacker : null
    });
});

app.MapPost("/battles/{id}/heal", async (string id, HealRequest? request, EventStoreService esService) =>
{
    var streamName = EventStoreService.GetStreamName(id);

    var battle = await esService.ReplayBattleAsync(id);

    var target = string.IsNullOrWhiteSpace(request?.Target) ? "Hero" : request.Target;

    if (!battle.CanHeal(target, out var validationError))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[Service-A] REJECTED Heal on {streamName}: {validationError}");
        Console.ResetColor();

        return Results.BadRequest(new
        {
            error = validationError,
            battleId = id,
            isFinished = battle.IsFinished
        });
    }

    bool isTargetHero = !target.Equals("Enemy", StringComparison.OrdinalIgnoreCase) && !target.Equals(battle.EnemyName, StringComparison.OrdinalIgnoreCase);
    int currentHp = isTargetHero ? battle.HeroCurrentHp : battle.EnemyCurrentHp;
    int maxHp = isTargetHero ? battle.HeroMaxHp : battle.EnemyMaxHp;
    string targetName = isTargetHero ? battle.HeroName : battle.EnemyName;

    int requestedHeal = request?.Amount is > 0 ? request.Amount.Value : 20;
    int effectiveHeal = Math.Min(requestedHeal, maxHp - currentHp);
    int newHp = currentHp + effectiveHeal;

    var healEvent = new HealUsed(
        id,
        targetName,
        effectiveHeal,
        newHp,
        DateTime.UtcNow
    );

    await esService.AppendEventAsync(id, nameof(HealUsed), healEvent);

    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.WriteLine($"[Service-A] Append HealUsed {streamName} {targetName} healed +{effectiveHeal} HP (Current HP: {newHp}/{maxHp})");
    Console.ResetColor();

    return Results.Ok(new
    {
        message = $"{targetName} se ha curado +{effectiveHeal} HP",
        battleId = id,
        target = targetName,
        healedAmount = effectiveHeal,
        currentHp = newHp,
        maxHp
    });
});

app.Run();
