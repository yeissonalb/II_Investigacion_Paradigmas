const ENEMY_NAMES = [
  "Dragón",
  "Orco Brutal",
  "Liche",
  "Gólem de piedra",
  "Hidra",
  "Quimera",
  "Sombra",
  "Troll"
];

export function randomInt(min, max) {
  return Math.floor(Math.random() * (max - min + 1)) + min;
}

export function rollEnemy(preferredName) {
  return {
    enemyName: preferredName?.trim() || ENEMY_NAMES[randomInt(0, ENEMY_NAMES.length - 1)],
    enemyHp: randomInt(80, 180)
  };
}

export function rollEnemyDamage() {
  return randomInt(10, 36);
}

export function rollEnemyHeal() {
  return randomInt(8, 28);
}

export function rollEnemyDelay() {
  return randomInt(700, 1600);
}

export function decideEnemyAction(enemy) {
  const ratio = enemy?.maxHp ? enemy.hp / enemy.maxHp : 1;
  if (ratio >= 0.98) return "attack";
  if (ratio <= 0.32) return Math.random() < 0.75 ? "heal" : "attack";
  return Math.random() < 0.68 ? "attack" : "heal";
}
