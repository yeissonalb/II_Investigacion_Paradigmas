using EventStore.Client;
using ServiceA.Domain;
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
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();
app.UseCors();

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
        DateTime.UtcNow,
        request?.HeroPokemonId,
        string.IsNullOrWhiteSpace(request?.HeroSprite) ? null : request.HeroSprite,
        request?.EnemyPokemonId,
        string.IsNullOrWhiteSpace(request?.EnemySprite) ? null : request.EnemySprite,
        request?.HeroTypes,
        request?.HeroAttack,
        request?.HeroDefense,
        request?.HeroSpecialAttack,
        request?.HeroSpecialDefense,
        request?.HeroSpeed,
        request?.HeroMoves,
        request?.EnemyTypes,
        request?.EnemyAttack,
        request?.EnemyDefense,
        request?.EnemySpecialAttack,
        request?.EnemySpecialDefense,
        request?.EnemySpeed,
        request?.EnemyMoves
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
        hero = new { name = heroName, hp = heroHp, pokemonId = battleStartedEvent.HeroPokemonId, sprite = battleStartedEvent.HeroSprite },
        enemy = new { name = enemyName, hp = enemyHp, pokemonId = battleStartedEvent.EnemyPokemonId, sprite = battleStartedEvent.EnemySprite }
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
    int damage;
    bool isCritical;
    int remainingHp;
    string? moveName = null;
    string? moveType = null;
    string? damageClass = null;
    int? basePower = null;
    double? effectiveness = null;
    double? stab = null;
    bool? hit = null;

    if (!string.IsNullOrWhiteSpace(request?.MoveName))
    {
        if (!battle.TryGetMove(isAttackerHero, request.MoveName, out var move, out var moveError) || move is null)
        {
            return Results.BadRequest(new { error = moveError, battleId = id });
        }

        var outcome = MoveCombat.Resolve(
            battle.AttackerProfile(isAttackerHero),
            battle.DefenderProfile(isAttackerHero),
            move,
            CombatRoll.Create(Random.Shared));
        damage = outcome.Damage;
        isCritical = outcome.Critical;
        remainingHp = Math.Max(0, targetCurrentHp - damage);
        moveName = move.Name;
        moveType = move.Type;
        damageClass = move.DamageClass;
        basePower = move.Power;
        effectiveness = outcome.Effectiveness;
        stab = outcome.Stab;
        hit = outcome.Hit;
    }
    else if (battle.HasMoves(isAttackerHero))
    {
        return Results.BadRequest(new
        {
            error = "Debes elegir un movimiento de este Pokémon.",
            battleId = id
        });
    }
    else
    {
        damage = request?.Damage is > 0
            ? request.Damage.Value
            : isAttackerHero
                ? Random.Shared.Next(15, 30)
                : Random.Shared.Next(10, 38);
        isCritical = damage >= 25;
        remainingHp = Math.Max(0, targetCurrentHp - damage);
    }

    var attackEvent = new AttackPerformed(
        id,
        attacker,
        target,
        damage,
        remainingHp,
        isCritical,
        DateTime.UtcNow,
        moveName,
        moveType,
        damageClass,
        basePower,
        effectiveness,
        stab,
        hit
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
        winner = remainingHp <= 0 ? attacker : null,
        moveName,
        moveType,
        damageClass,
        effectiveness,
        hit
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

    int requestedHeal = request?.Amount is > 0
        ? request.Amount.Value
        : isTargetHero
            ? 20
            : Random.Shared.Next(8, 28);
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
