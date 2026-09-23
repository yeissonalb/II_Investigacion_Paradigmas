export function StartBattleForm({ form, onChange, onSubmit, busy }) {
  const update = (field) => (event) => onChange({ ...form, [field]: event.target.value });

  return (
    <section className="create-card">
      <div className="section-head">
        <h2>Nuevo combate</h2>
        <span className="hint">POST :3001</span>
      </div>
      <form onSubmit={onSubmit} autoComplete="off">
        <label>
          ID del combate
          <input
            value={form.battleId}
            onChange={update("battleId")}
            placeholder="battle-demo-1 (opcional)"
          />
        </label>
        <div className="form-row">
          <label>
            Héroe
            <input value={form.heroName} onChange={update("heroName")} required />
          </label>
          <label>
            HP
            <input type="number" min="1" max="500" value={form.heroHp} onChange={update("heroHp")} />
          </label>
        </div>
        <label>
          Enemigo (opcional)
          <input
            value={form.enemyName}
            onChange={update("enemyName")}
            placeholder="Nombre al azar si lo dejas vacío"
          />
        </label>
        <p className="hint">Vida, daño, curación y el momento de actuar se generan al azar.</p>
        <button type="submit" className="btn btn-gold" disabled={busy}>
          Iniciar combate
        </button>
      </form>
    </section>
  );
}

export function BattleList({ battles, selectedId, onSelect }) {
  return (
    <section>
      <div className="section-head">
        <h2>Combates</h2>
        <span className="hint">GET :3002</span>
      </div>
      <ul className="battle-list">
        {!battles.length && <li className="empty-state">Aún no hay combates proyectados.</li>}
        {battles.map((battle) => (
          <li key={battle.battleId}>
            <button
              type="button"
              className={battle.battleId === selectedId ? "active" : ""}
              onClick={() => onSelect(battle.battleId)}
            >
              <strong>{battle.hero.name} vs {battle.enemy.name}</strong>
              <span>{battle.battleId} · {battle.isFinished ? "finalizado" : "en curso"}</span>
            </button>
          </li>
        ))}
      </ul>
    </section>
  );
}
