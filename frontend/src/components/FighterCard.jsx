function hpPercent(fighter) {
  if (!fighter?.maxHp) return 0;
  return Math.max(0, Math.min(100, (fighter.hp / fighter.maxHp) * 100));
}

export function FighterCard({ side, fighter, effect }) {
  const isHero = side === "hero";
  const percent = hpPercent(fighter);
  const tone = percent <= 25 ? "low" : percent <= 50 ? "mid" : "";

  return (
    <article className={`fighter ${isHero ? "hero-side" : "enemy-side"} ${effect || ""}`}>
      <div className="nameplate">
        <h3>{fighter?.name || (isHero ? "Tu Pokémon" : "Rival")}</h3>
        <div className="hp">
          <span>HP</span>
          <div className="hp-track">
            <div
              className={`hp-fill ${tone}`}
              style={{ width: `${percent}%` }}
            />
          </div>
          <p>{fighter ? `${fighter.hp} / ${fighter.maxHp}` : "— / —"}</p>
        </div>
      </div>
      <div className={`platform ${isHero ? "hero-portrait" : "enemy-portrait"}`}>
        <div className="portrait">
          {fighter?.sprite
            ? <img src={fighter.sprite} alt={fighter.name || (isHero ? "Tu Pokémon" : "Rival")} />
            : <span className="pokeball" aria-hidden="true" />}
        </div>
      </div>
    </article>
  );
}
