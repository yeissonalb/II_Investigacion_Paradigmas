export function HeroPortrait() {
  return (
    <svg viewBox="0 0 120 120" role="img" aria-label="Héroe">
      <defs>
        <radialGradient id="heroGlow" cx="50%" cy="40%" r="60%">
          <stop offset="0%" stopColor="#7ec8ff" />
          <stop offset="100%" stopColor="#1d4e89" />
        </radialGradient>
      </defs>
      <circle cx="60" cy="60" r="54" fill="url(#heroGlow)" />
      <path d="M60 28 L88 42 V62 C88 86 60 98 60 98 C60 98 32 86 32 62 V42 Z" fill="#d7e7f7" opacity=".9" />
      <path d="M60 34 L80 44 V61 C80 80 60 90 60 90 C60 90 40 80 40 61 V44 Z" fill="#3a7ebd" />
      <circle cx="60" cy="48" r="10" fill="#f3d7b0" />
      <rect x="54" y="58" width="12" height="18" rx="3" fill="#f3d7b0" />
      <path d="M48 92 H72 L66 104 H54 Z" fill="#2c5f93" />
    </svg>
  );
}

export function EnemyPortrait() {
  return (
    <svg viewBox="0 0 120 120" role="img" aria-label="Enemigo">
      <defs>
        <radialGradient id="enemyGlow" cx="50%" cy="40%" r="60%">
          <stop offset="0%" stopColor="#ff8a65" />
          <stop offset="100%" stopColor="#7a1f16" />
        </radialGradient>
      </defs>
      <circle cx="60" cy="60" r="54" fill="url(#enemyGlow)" />
      <path d="M28 58 C28 38 46 26 60 26 C74 26 92 38 92 58 C92 84 74 98 60 98 C46 98 28 84 28 58 Z" fill="#8b1e14" />
      <path d="M38 40 L22 28 L34 48 Z" fill="#c44536" />
      <path d="M82 40 L98 28 L86 48 Z" fill="#c44536" />
      <circle cx="48" cy="56" r="6" fill="#f7e27c" />
      <circle cx="72" cy="56" r="6" fill="#f7e27c" />
      <path d="M46 74 C54 82 66 82 74 74" stroke="#2b0b08" strokeWidth="4" fill="none" />
    </svg>
  );
}

function hpPercent(fighter) {
  if (!fighter?.maxHp) return 0;
  return Math.max(0, Math.min(100, (fighter.hp / fighter.maxHp) * 100));
}

export function FighterCard({ side, fighter, effect }) {
  const isHero = side === "hero";
  const percent = hpPercent(fighter);

  return (
    <article className={`fighter ${effect || ""}`}>
      <div className={`portrait ${isHero ? "hero-portrait" : "enemy-portrait"}`}>
        {isHero ? <HeroPortrait /> : <EnemyPortrait />}
      </div>
      <h3>{fighter?.name || (isHero ? "Héroe" : "Enemigo")}</h3>
      <div className="hp">
        <div className="hp-track">
          <div
            className={`hp-fill ${isHero ? "" : "enemy"} ${percent <= 30 ? "low" : ""}`}
            style={{ width: `${percent}%` }}
          />
        </div>
        <p>{fighter ? `${fighter.hp} / ${fighter.maxHp}` : "— / —"}</p>
      </div>
    </article>
  );
}
