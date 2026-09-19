# Servicio B — Query Side (Persona 3)
## Dominio: Combate por Turnos con CQRS + Event Sourcing

Este servicio es el **lado de consulta**. No recibe comandos ni escribe eventos. Se suscribe a EventStoreDB, materializa una vista de cada combate y responde `GET` contra esa proyección.

`service-a` persiste hechos (`BattleStarted`, `AttackPerformed`, `HealUsed`). `service-b` los proyecta a un read model en memoria para devolver estado, estadísticas e historial sin rehacer replay en cada request.

---

### 1. Variables de Entorno
* `PORT` o `SERVICE_B_PORT`: Puerto HTTP (por defecto `3002`).
* `EVENTSTORE_URL`: Cadena de conexión hacia EventStoreDB (`esdb://eventstore:2113?tls=false` en Docker, o `esdb://localhost:2113?tls=false` en local).

---

### 2. Cómo se arma la proyección
1. Al arrancar, un `BackgroundService` se suscribe a `$all` desde el inicio.
2. Filtra streams `battle-{id}`.
3. Aplica cada evento a `BattleReadStore` (HP, ganador, daño total, curas, críticos, historial).
4. Si EventStoreDB todavía no está arriba, reintenta cada 3 segundos.
5. Los `GET` leen solo la vista materializada. No validan reglas de negocio y no hacen append.

La vista puede ir un instante atrasada respecto al último comando (consistencia eventual). En este lab suele ser imperceptible.

---

### 3. Endpoints (`GET` únicamente)

#### Salud
* **Ruta:** `GET /health`

#### Listado de combates
* **Ruta:** `GET /battles`

#### Estado actual
* **Ruta:** `GET /battles/{id}`

#### Estadísticas
* **Ruta:** `GET /battles/{id}/stats`

#### Historial de eventos
* **Ruta:** `GET /battles/{id}/history`

---

### 4. Ejemplo de consulta

Después de iniciar un combate y atacar desde `service-a`:

```bash
curl http://localhost:3002/battles/battle-demo-1/stats
```

Respuesta esperada (estructura):

```json
{
  "battleId": "battle-demo-1",
  "isFinished": false,
  "winner": null,
  "hero": { "name": "Guerrero", "hp": 100, "maxHp": 100 },
  "enemy": { "name": "Dragón", "hp": 70, "maxHp": 100 },
  "stats": {
    "turns": 1,
    "actions": 1,
    "totalDamage": 30,
    "heroDamageDealt": 30,
    "enemyDamageDealt": 0,
    "totalHealed": 0,
    "heroHealed": 0,
    "enemyHealed": 0,
    "criticalHits": 1
  }
}
```
