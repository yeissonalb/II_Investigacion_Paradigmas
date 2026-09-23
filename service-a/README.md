# Servicio A — Command Side (Persona 2)
## Dominio: Combate por Turnos con Event Sourcing

Este servicio es el encargado exclusivo del flujo de **escritura / comandos** en la arquitectura Event Sourcing del proyecto. No gestiona vistas materializadas ni consultas (`GET`); en su lugar, recibe comandos, reconstruye el estado actual de la entidad mediante **replaying** de eventos para validar reglas de negocio, y registra nuevos eventos inmutables en **EventStoreDB**.

---

### 1. Variables de Entorno
* `PORT`: Puerto HTTP donde corre el servicio (por defecto `3001`).
* `EVENTSTORE_URL`: Cadena de conexión hacia EventStoreDB (por defecto `esdb://localhost:2113?tls=false` o `esdb://eventstore:2113?tls=false` en Docker).

---

### 2. Contrato de Eventos y Streams
* **Stream ID:** `battle-{id}` (ej. `battle-demo-1`).
* **Eventos generados:**
  * `BattleStarted`: Inicializa el combate con los dos Pokémon y sus vidas máximas.
  * `AttackPerformed`: Registra el daño infligido, vida restante y si fue golpe crítico.
  * `HealUsed`: Registra la cantidad de puntos de vida recuperados.

---

### 3. Endpoints del Servicio A (`POST` únicamente)

#### A. Iniciar Combate
* **Ruta:** `POST /battles/start`
* **Body:**
  ```json
  {
    "battleId": "battle-1",
    "heroName": "charizard",
    "heroHp": 100,
    "enemyName": "blastoise",
    "enemyHp": 100
  }
  ```

#### B. Ejecutar Ataque (Con Replay & Validación)
* **Ruta:** `POST /battles/{id}/attack`
* **Body:**
  ```json
  {
    "attacker": "Hero",
    "damage": 25
  }
  ```
  *(Si el objetivo ya tiene 0 HP o la batalla culminó, el replay lo detecta y rechaza la petición con código 400).*

#### C. Curar / Recuperar Vida (Con Replay & Validación)
* **Ruta:** `POST /battles/{id}/heal`
* **Body:**
  ```json
  {
    "target": "Hero",
    "amount": 20
  }
  ```
  *(Si la vida ya está al máximo o la batalla terminó, el replay lo detecta y rechaza la petición).*

---

### 4. Guion para tu Presentación en Vivo (Persona 2)

#### Parte 1: Explicar el Problema que Resuelve Event Sourcing (Oral)
> *"En sistemas tradicionales, guardaríamos una fila en una base de datos relacional con `vida_actual = 60`. Si alguien altera ese valor o hay un bug, perdemos la trazabilidad de cómo llegó a ese estado. En cambio, con **Event Sourcing**, el estado actual no se almacena: **la única fuente de verdad son los eventos inmutables ocurridos en el tiempo** (`BattleStarted`, `AttackPerformed`, `HealUsed`). Esto nos permite reproducir partidas (replay), auditar cada turno, evitar trampas y validar reglas de negocio en el momento exacto."*

#### Parte 2: Ejecutar los comandos de la Demo en Clase
Abre una terminal y ejecuta las siguientes solicitudes:

1. **Iniciar combate:**
   ```bash
   curl -X POST http://localhost:3001/battles/start \
     -H "Content-Type: application/json" \
     -d "{\"battleId\":\"battle-demo-1\",\"heroName\":\"charizard\",\"enemyName\":\"blastoise\"}"
   ```
   *Observar en tu consola el log:*  
   `[Service-A] Append BattleStarted battle-demo-1 charizard (100 HP) vs blastoise (100 HP)`

2. **Atacar al Pokémon rival:**
   ```bash
   curl -X POST http://localhost:3001/battles/battle-demo-1/attack \
     -H "Content-Type: application/json" \
     -d "{\"attacker\":\"Hero\",\"damage\":30}"
   ```
   *Observar en tu consola el log:*  
   `[Service-A] Append AttackPerformed battle-demo-1 Hero dealt 30 damage to blastoise (Remaining HP: 70/100)`

3. **Curar a tu Pokémon (o recibir contraataque y curarse):**
   ```bash
   # El Pokémon rival contraataca
   curl -X POST http://localhost:3001/battles/battle-demo-1/attack \
     -H "Content-Type: application/json" \
     -d "{\"attacker\":\"Enemy\",\"damage\":25}"

   # Tu Pokémon se cura
   curl -X POST http://localhost:3001/battles/battle-demo-1/heal \
     -H "Content-Type: application/json" \
     -d "{\"target\":\"Hero\",\"amount\":20}"
   ```
   *Observar en tu consola el log:*  
   `[Service-A] Append HealUsed battle-demo-1 charizard healed +20 HP (Current HP: 95/100)`

4. **Demostración de la regla de negocio con Replay (Ataque tras derrota):**
   *(Ejecutar ataques masivos hasta que la vida llegue a 0)*
   ```bash
   curl -X POST http://localhost:3001/battles/battle-demo-1/attack \
     -H "Content-Type: application/json" \
     -d "{\"attacker\":\"Hero\",\"damage\":80}"
   ```
   *Intento de ataque adicional después de la muerte:*
   ```bash
   curl -X POST http://localhost:3001/battles/battle-demo-1/attack \
     -H "Content-Type: application/json" \
     -d "{\"attacker\":\"Hero\",\"damage\":10}"
   ```
   *Respuesta esperada:* Error `400 Bad Request` con mensaje:  
   `"No se puede atacar. La batalla ya culminó. Ganador: 'charizard', derrotado: 'blastoise'."`
