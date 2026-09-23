import { POKEMON_ROSTER, spriteUrl } from "../pokeApi.js";

export function StartBattleForm({
  form,
  onChange,
  onSearch,
  onSubmit,
  busy,
  player,
  searchError,
  opponentStatus
}) {
  const update = (field) => (event) => onChange({ ...form, [field]: event.target.value });

  return (
    <section className="create-card">
      <div className="section-head">
        <h2>Elige tu Pokémon</h2>
        <span className="hint">PokéAPI</span>
      </div>
      <form onSubmit={onSubmit} autoComplete="off">
        <div className="roster" role="listbox" aria-label="Pokémon para combatir">
          {POKEMON_ROSTER.map((entry) => (
            <button
              key={entry.id}
              type="button"
              className={`roster-card ${player?.id === entry.id ? "selected" : ""}`}
              onClick={() => onSearch(entry.id)}
              disabled={busy}
              aria-pressed={player?.id === entry.id}
            >
              <img src={spriteUrl(entry.id)} alt="" />
              <span>{entry.name}</span>
            </button>
          ))}
        </div>
        <label>
          Buscar otro
          <span className="search-row">
            <input
              value={form.pokemonQuery}
              onChange={update("pokemonQuery")}
              placeholder="eevee, snorlax o 25"
            />
            <button type="button" className="btn btn-quiet" onClick={() => onSearch()} disabled={busy}>
              Buscar
            </button>
          </span>
        </label>
        {player && (
          <div className="pokemon-preview">
            <img src={player.sprite} alt={player.name} />
            <div>
              <strong>{player.name}</strong>
              <span>{player.hp} HP · #{player.id}</span>
            </div>
          </div>
        )}
        {searchError && <p className="form-error">{searchError}</p>}
        {opponentStatus && (
          <p className="hint opponent-status">
            <span className="pokeball spin" aria-hidden="true" />
            {opponentStatus}
          </p>
        )}
        <button type="submit" className="btn btn-gold" disabled={busy || !player}>
          ¡A combatir!
        </button>
        <label>
          Código del combate
          <input
            value={form.battleId}
            onChange={update("battleId")}
            placeholder="Opcional"
          />
        </label>
        <p className="hint">El rival aparece solo. Tú eliges atacar o curar; cada acción queda en los eventos.</p>
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
        {!battles.length && <li className="empty-state">Todavía no hay combates. Elige un Pokémon para empezar.</li>}
        {battles.map((battle) => (
          <li key={battle.battleId}>
            <button
              type="button"
              className={battle.battleId === selectedId ? "active" : ""}
              onClick={() => onSelect(battle.battleId)}
            >
              <span className="battle-faces" aria-hidden="true">
                {battle.hero.sprite ? <img src={battle.hero.sprite} alt="" /> : <i />}
                {battle.enemy.sprite ? <img src={battle.enemy.sprite} alt="" /> : <i />}
              </span>
              <span className="battle-copy">
                <strong>{battle.hero.name} vs {battle.enemy.name}</strong>
                <span>{battle.battleId} · {battle.isFinished ? "finalizado" : "en curso"}</span>
              </span>
            </button>
          </li>
        ))}
      </ul>
    </section>
  );
}
