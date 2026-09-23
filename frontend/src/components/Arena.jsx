import { LuClock } from "react-icons/lu";
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
                : "Campo de batalla"}
          </h2>
        </div>
        <div className={`status-badge ${statusClass}`}>{statusText}</div>
      </div>

      <div className="fighters">
        <FighterCard side="hero" fighter={detail?.hero} effect={heroFx} />
        <FighterCard side="enemy" fighter={detail?.enemy} effect={enemyFx} />
      </div>

      <div className="actions">
        <div className="action-group">
          <p>Tu turno</p>
          <div className="btn-row">
            <button className="btn btn-attack" disabled={!canAct} onClick={() => onAction("attack", "Hero")}>Atacar</button>
            <button className="btn btn-heal" disabled={!canAct} onClick={() => onAction("heal", "Hero")}>Curar</button>
          </div>
        </div>
        <div className="action-group enemy-ai">
          <p>Rival</p>
          <strong>{enemyStatus || "Espera su turno y responde solo."}</strong>
        </div>
        <label className="amount-field">
          Daño o cura
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
        Atacar y curar se envían a Service A. El rival responde solo. El marcador y los eventos salen de Service B.
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

function matchFighter(detail, name) {
  const key = name?.trim().toLowerCase();
  if (!key || !detail) return null;
  if (detail.hero?.name?.toLowerCase() === key) return detail.hero;
  if (detail.enemy?.name?.toLowerCase() === key) return detail.enemy;
  return null;
}

function presentEvent(item, detail) {
  const text = item.description || "";
  const attack = text.match(/^(.*?) hizo (\d+) de daño a (.*?) \(HP restante: (\d+)\)/);
  if (item.eventType === "AttackPerformed" && attack) {
    return {
      tone: "attack",
      fighter: matchFighter(detail, attack[1]),
      lead: attack[1],
      verb: " hizo ",
      amount: attack[2],
      tail: ` de daño a ${attack[3]}.`,
      chip: "HP restante:",
      value: attack[4],
      critical: text.includes("CRÍTICO")
    };
  }

  const heal = text.match(/^(.*?) se curó \+(\d+) HP \(HP actual: (\d+)\)/);
  if (item.eventType === "HealUsed" && heal) {
    return {
      tone: "heal",
      fighter: matchFighter(detail, heal[1]),
      lead: heal[1],
      verb: " se curó ",
      amount: `+${heal[2]}`,
      tail: " PS.",
      chip: "HP actual:",
      value: heal[3]
    };
  }

  return {
    tone: "start",
    fighter: detail?.hero?.sprite ? detail.hero : null,
    lead: "",
    verb: "",
    amount: "",
    tail: text,
    chip: "",
    value: ""
  };
}

function EventMark({ tone }) {
  if (tone === "heal") {
    return (
      <svg className="event-mark heal" viewBox="0 0 64 64" aria-hidden="true">
        <path fill="#2fbe62" d="M28 8h8v16h16v8H36v16h-8V32H12v-8h16z" />
        <path fill="#7ee0a2" d="M8 18h6v4H8zm4-4h4v4h-4zM50 40h6v4h-6zm4-4h4v4h-4zM10 44h5v3h-5zm3-3h3v3h-3zM48 14h5v3h-5zm3-3h3v3h-3z" />
      </svg>
    );
  }
  if (tone === "attack") {
    return (
      <svg className="event-mark attack" viewBox="0 0 64 64" aria-hidden="true">
        <path fill="#ff8a1e" d="M32 2l6 16 16-6-6 16 16 6-16 6 6 16-16-6-6 16-6-16-16 6 6-16L4 34l16-6-6-16 16 6z" />
        <path fill="#ffe14a" d="M32 18l3.2 8.2L44 29l-8.8 2.8L32 40l-3.2-8.2L20 29l8.8-2.8z" />
      </svg>
    );
  }
  return <span className="pokeball event-mark-ball" aria-hidden="true" />;
}

export function EventTimeline({ history, detail }) {
  const items = [...(history || [])].reverse();

  return (
    <section className="events-panel">
      <div className="events-head">
        <div className="events-title">
          <span className="pokeball" aria-hidden="true" />
          <h2>Eventos</h2>
          <span className="hint">battle log</span>
        </div>
        <p>Registro de acciones en tiempo real del combate.</p>
      </div>
      <ol className="timeline">
        {!items.length && <li className="empty-state">Aquí verás BattleStarted, AttackPerformed y HealUsed.</li>}
        {items.map((item, index) => {
          const view = presentEvent(item, detail);
          return (
            <li
              key={`${item.eventNumber}-${item.eventType}`}
              className={`event-card ${view.tone} ${index === 0 ? "new" : ""}`}
            >
              <div className="event-side">
                <span className="event-index">#{item.eventNumber}</span>
                {view.tone !== "start" && <EventMark tone={view.tone} />}
                <span className="event-sprite">
                  {view.fighter?.sprite
                    ? <img src={view.fighter.sprite} alt={view.fighter.name} />
                    : <span className="pokeball event-fallback" aria-hidden="true" />}
                </span>
              </div>
              <div className="event-body">
                <span className="event-watermark pokeball" aria-hidden="true" />
                <strong>{item.eventType}</strong>
                <p>
                  <span className="event-name">{view.lead}</span>
                  {view.verb}
                  {view.amount && <b>{view.amount}</b>}
                  {view.tail}
                  {view.critical ? " Crítico." : ""}
                </p>
                {view.chip && (
                  <span className="event-chip">{view.chip} <b>{view.value}</b></span>
                )}
                <time><LuClock aria-hidden="true" /> {formatEventDate(item.timestamp)}</time>
              </div>
            </li>
          );
        })}
      </ol>
      <p className="events-foot">¡Que continúe el combate!</p>
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
    <section className="stats-panel">
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
