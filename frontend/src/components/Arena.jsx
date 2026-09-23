import { FighterCard } from "./FighterCard.jsx";

export function Arena({
  detail,
  pending,
  busy,
  amount,
  onAmountChange,
  onAction,
  heroFx,
  enemyFx,
  enemyStatus
}) {
  const canAct = Boolean(detail) && !detail.isFinished && !busy;
  const statusClass = detail
    ? (detail.isFinished ? "done" : "live")
    : "";
  const statusText = pending
    ? "Aún no proyectado"
    : !detail
      ? "Sin combate"
      : detail.isFinished
        ? `Finalizado · ganó ${detail.winner}`
        : "Combate activo";

  return (
    <section className="panel arena">
      <div className="arena-head">
        <div>
          <p className="kicker">
            {pending
              ? pending
              : detail?.stream || (detail ? `battle-${detail.battleId}` : "Selecciona o crea un combate")}
          </p>
          <h2>
            {pending
              ? "Esperando a Service B"
              : detail
                ? `${detail.hero.name} vs ${detail.enemy.name}`
                : "Arena"}
          </h2>
        </div>
        <div className={`status-badge ${statusClass}`}>{statusText}</div>
      </div>

      <div className="fighters">
        <FighterCard side="hero" fighter={detail?.hero} effect={heroFx} />
        <div className="versus">
          <span>VS</span>
          <small>CQRS en vivo</small>
        </div>
        <FighterCard side="enemy" fighter={detail?.enemy} effect={enemyFx} />
      </div>

      <div className="actions">
        <div className="action-group">
          <p>Comandos del héroe</p>
          <div className="btn-row">
            <button className="btn btn-attack" disabled={!canAct} onClick={() => onAction("attack", "Hero")}>Atacar</button>
            <button className="btn btn-heal" disabled={!canAct} onClick={() => onAction("heal", "Hero")}>Curar</button>
          </div>
        </div>
        <div className="action-group enemy-ai">
          <p>IA del enemigo</p>
          <strong>{enemyStatus || "Espera su turno. Vida, daño y curación son aleatorios."}</strong>
        </div>
        <label className="amount-field">
          Valor opcional
          <input
            type="number"
            min="1"
            max="200"
            placeholder="Aleatorio"
            value={amount}
            onChange={(event) => onAmountChange(event.target.value)}
          />
        </label>
      </div>

      <p className="arena-note">
        Tú mandas los comandos del héroe a Service A. El enemigo responde solo, con daño, curación
        y tiempo de reacción aleatorios. El estado se lee de Service B.
      </p>
    </section>
  );
}

function formatEventDate(value) {
  if (!value) return "Sin fecha";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Sin fecha";
  return date.toLocaleString("es-CR", {
    dateStyle: "short",
    timeStyle: "medium"
  });
}

export function EventTimeline({ history }) {
  const items = [...(history || [])].reverse();

  return (
    <section>
      <div className="section-head">
        <h2>Historial inmutable</h2>
        <span className="hint">stream append-only</span>
      </div>
      <ol className="timeline">
        {!items.length && <li className="empty-state">Los eventos aparecerán aquí en orden.</li>}
        {items.map((item, index) => (
          <li
            key={`${item.eventNumber}-${item.eventType}`}
            className={`${item.eventType === "AttackPerformed" ? "attack" : ""} ${item.eventType === "HealUsed" ? "heal" : ""} ${index === 0 ? "new" : ""}`}
          >
            <strong>#{item.eventNumber} · {item.eventType}</strong>
            <em>{item.description}</em>
            <em>{formatEventDate(item.timestamp)}</em>
          </li>
        ))}
      </ol>
    </section>
  );
}

export function StatsGrid({ stats }) {
  const values = stats || {};
  const cards = [
    ["Turnos", values.turns ?? 0],
    ["Acciones", values.actions ?? 0],
    ["Daño total", values.totalDamage ?? 0],
    ["Curado", values.totalHealed ?? 0],
    ["Críticos", values.criticalHits ?? 0],
    ["Daño héroe", values.heroDamageDealt ?? 0]
  ];

  return (
    <section>
      <div className="section-head">
        <h2>Estadísticas</h2>
        <span className="hint">read model</span>
      </div>
      <div className="stats-grid">
        {cards.map(([label, value]) => (
          <article key={label}>
            <small>{label}</small>
            <strong>{value}</strong>
          </article>
        ))}
      </div>
    </section>
  );
}
