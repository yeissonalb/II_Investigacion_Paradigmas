# Investigación II: Patrones y Mecanismos en Arquitecturas Distribuidas

> **Curso:** Paradigmas de Investigación / Arquitecturas de Software  
> **Tecnologías:** .NET 9, React, Vite, EventStoreDB, Docker, Docker Compose  
> **Integrantes:** Fatima Carrillo Garcia, Samir Campos Díaz, Maria del Mar Diaz Ruiz, Brayan José Pérez Balladares, Yeisson Alberto Villalobos Toruño

---

## 1. Tema Seleccionado

**Event Sourcing** (implementado junto con el patrón **CQRS — Command Query Responsibility Segregation**).

En las arquitecturas tradicionales orientadas a CRUD, las bases de datos guardan únicamente una foto del estado actual, sobreescribiendo los valores con `UPDATE` y perdiendo la historia. En **Event Sourcing**, el estado de una entidad se modela como una **secuencia inmutable y ordenada de eventos** (*append-only log*). El estado actual es el resultado de reproducir (*replay*) esos eventos a lo largo del tiempo.

> **Principio Clave:** *"La fuente de la verdad son los eventos ocurridos, no el saldo ni el estado final."*

---

## 2. Arquitectura o Diagrama

La solución está compuesta por un **frontend web**, dos microservicios autónomos desarrollados en **.NET 9** y **EventStoreDB**:

```
                               +-----------------------------+
                               |   Frontend (Puerto 8080)    |
                               |     o Cliente HTTP / cURL   |
                               +-----------------------------+
                                     |                 ^
                        1. POST      |                 | 4. GET
                       Comandos      v                 | Consultas
                     +---------------------+     +---------------------+
                     |      Service A      |     |      Service B      |
                     |   (Command Side)    |     |    (Query Side)     |
                     |     Puerto 3001     |     |     Puerto 3002     |
                     +---------------------+     +---------------------+
                                |                           ^
                      2. Valida |                           | 3. Suscripción
                      replay y  |                           | en tiempo real
                      hace      v                           | (Read Model)
                      Append  +-------------------------------+
                              |         EventStoreDB          |
                              |         (Puerto 2113)         |
                              |      Append-Only Storage      |
                              +-------------------------------+
```

### Componentes y Contrato de Integración:
- **Service A (`service-a`, Puerto 3001):** Servicio de Escritura / Comandos. Recibe peticiones `POST`, reconstruye el estado previo reproduciendo los eventos del stream para validar reglas de negocio, y almacena los nuevos eventos.
- **Service B (`service-b`, Puerto 3002):** Servicio de Lectura / Consultas. Mantiene una suscripción en segundo plano a EventStoreDB (`$all`), escucha los eventos conforme ocurren y actualiza una vista en memoria (*Read Model*) para responder peticiones `GET` en milisegundos.
- **Frontend (`frontend`, Puerto 8080):** SPA en **React + Vite**. Envía comandos a Service A y lee el estado, estadísticas e historial proyectado desde Service B. Incluye un inspector CQRS para ver cada `POST` y `GET` en vivo.
- **EventStoreDB (Puerto 2113):** Base de datos especializada en almacenamiento inmutable de eventos por stream y bus pub/sub en tiempo real.
- **Convención de Streams y Eventos:**
  - Identificador de Stream: `battle-{id}` (o `account-{id}`).
  - Eventos de Dominio: `BattleStarted`, `AttackPerformed`, `HealUsed` (y su equivalente financiero `AccountOpened`, `MoneyDeposited`, `MoneyWithdrawn`).
  - Cadena de conexión interna en Docker: `esdb://eventstore:2113?tls=false`.
  - Cadena de conexión local host: `esdb://localhost:2113?tls=false`.

*(Para el análisis teórico completo, ventajas, limitaciones y comparativa técnica EventStoreDB vs PostgreSQL, consultar [`docs/arquitectura.md`](docs/arquitectura.md) y [`docs/informe_investigacion.md`](docs/informe_investigacion.md)).*

---

## 3. Cómo Ejecutar el Proyecto

Una persona con **Git**, **Docker** y **Docker Compose** puede clonar el repositorio y levantar toda la solución, incluida EventStoreDB. No hay que instalar .NET, Node.js ni bases de datos a mano.

### Requisitos Previos:
- **Git**, **Docker** y **Docker Compose**.

### Ejecución reproducible (requerida)
1. Clonar el repositorio y situarse en la raíz:
   ```bash
   git clone <url-del-repositorio>
   cd II_Investigacion_Paradigmas
   ```
2. Ejecutar el script:
   - Windows: `.\run.ps1`
   - Linux / macOS: `chmod +x run.sh && ./run.sh`
3. El script internamente hace `docker compose pull`, `docker compose build` y `docker compose up -d`, y espera a que Service A y Service B respondan.
4. Abrir el frontend: [http://localhost:8080](http://localhost:8080).

Para detener todo: `docker compose down`.

---

## 4. Cómo Probar el Escenario

A continuación se detalla el paso a paso para ejecutar las pruebas, observar quién inicia la acción, qué se transmite, qué servicio lo procesa y la evidencia de la comunicación:

### Paso 0: Usar el frontend (recomendado para la demo)
1. Abrir [http://localhost:8080](http://localhost:8080).
2. Crear un combate (por ejemplo `battle-demo-1`, charizard vs blastoise).
3. Atacar o curar desde la arena: cada botón hace `POST` a Service A (`:3001`).
4. Observar cómo Service B (`:3002`) proyecta el estado, las estadísticas y el historial append-only.
5. El inspector inferior muestra el último comando y la última consulta, evidenciando CQRS.

Si se prefiere probar a mano con cURL:

### Paso 1: Comprobar el estado de salud de ambos servicios
```bash
curl http://localhost:3001/health
curl http://localhost:3002/health
```

### Paso 2: Iniciar una entidad (Comando en Service A)
Enviamos un comando para inicializar el combate `battle-demo-1`:
```bash
curl -X POST http://localhost:3001/battles/start \
  -H "Content-Type: application/json" \
  -d "{\"battleId\":\"battle-demo-1\",\"heroName\":\"charizard\",\"heroHp\":100,\"enemyName\":\"blastoise\",\"enemyHp\":100}"
```
- **Quién inicia:** El cliente envía la petición a Service A.
- **Qué ocurre:** Service A valida que el ID no exista y hace append del evento `BattleStarted` en el stream `battle-demo-1`.

### Paso 3: Ejecutar acciones sobre la entidad (Comandos en Service A)
Realizamos un ataque que inflige 35 de daño:
```bash
curl -X POST http://localhost:3001/battles/battle-demo-1/attack \
  -H "Content-Type: application/json" \
  -d "{\"attacker\":\"Hero\",\"damage\":35}"
```
- **Quién procesa:** Service A lee los eventos previos del stream, valida que el combate sigue activo y emite el evento `AttackPerformed`.

Ejecutamos una acción de curación:
```bash
curl -X POST http://localhost:3001/battles/battle-demo-1/heal \
  -H "Content-Type: application/json" \
  -d "{\"target\":\"Hero\",\"amount\":10}"
```

### Paso 4: Comprobar la proyección reactiva en Service B (Consultas)
Consultar el estado actual consolidado:
```bash
curl http://localhost:3002/battles/battle-demo-1
```

Consultar las estadísticas acumuladas generadas por la proyección:
```bash
curl http://localhost:3002/battles/battle-demo-1/stats
```

Consultar el historial completo de eventos proyectados:
```bash
curl http://localhost:3002/battles/battle-demo-1/history
```

### Paso 5: Evidencia de la comunicación en EventStoreDB UI
1. Abrir en el navegador web: [http://localhost:2113](http://localhost:2113).
2. Ir a la pestaña **Stream Browser**.
3. Buscar el stream `battle-demo-1`.
4. Se observará la secuencia ordenada de eventos inmutables:
   - Evento 0: `BattleStarted`
   - Evento 1: `AttackPerformed`
   - Evento 2: `HealUsed`
5. Al hacer clic en cada uno se puede auditar el payload exacto, fecha y metadatos, demostrando que la comunicación y persistencia ocurrieron correctamente.

---

## 5. Breve Explicación del Flujo de Comunicación

1. **Desacoplamiento total:** `Service A` (escritura) no se comunica por HTTP con `Service B` (lectura); ambos se comunican de forma asíncrona a través de `EventStoreDB`.
2. **Orden cronológico inmutable:** Cada evento emitido por `Service A` se almacena al final del stream con un número de versión secuencial (`0, 1, 2...`), imposibilitando pérdidas o modificaciones arbitrarias.
3. **Proyecciones reactivas y consistencia eventual:** `Service B` consume los eventos en tiempo real y actualiza su *Read Model* en memoria. Si `Service B` se reinicia, vuelve a leer los eventos desde el inicio del stream y reconstruye el estado idéntico.
4. **Verdad en los eventos:** El estado expuesto por `Service B` (la vida restante o el saldo) es solo una interpretación momentánea; la verdadera fuente de la verdad son los eventos persistidos en `EventStoreDB`.
