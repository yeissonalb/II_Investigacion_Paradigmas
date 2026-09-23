const POKEAPI = "https://pokeapi.co/api/v2";

export const POKEMON_ROSTER = [
  { id: 1, name: "bulbasaur" },
  { id: 4, name: "charmander" },
  { id: 7, name: "squirtle" },
  { id: 25, name: "pikachu" },
  { id: 39, name: "jigglypuff" },
  { id: 52, name: "meowth" },
  { id: 133, name: "eevee" },
  { id: 143, name: "snorlax" },
  { id: 6, name: "charizard" },
  { id: 94, name: "gengar" },
  { id: 149, name: "dragonite" },
  { id: 150, name: "mewtwo" }
];

export function spriteUrl(id) {
  return `https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/${id}.png`;
}

let speciesCount = null;

async function readJson(response, fallbackMessage) {
  if (response.status === 404) {
    return null;
  }
  if (!response.ok) {
    throw new Error(fallbackMessage);
  }
  return response.json();
}

async function getSpeciesCount() {
  if (speciesCount) return speciesCount;

  let response;
  try {
    response = await fetch(`${POKEAPI}/pokemon-species?limit=1`);
  } catch {
    throw new Error("No se pudo conectar con PokéAPI para elegir un oponente.");
  }

  const data = await readJson(response, "PokéAPI no respondió al preparar el oponente. Inténtalo de nuevo.");
  if (!data?.count) {
    throw new Error("PokéAPI no devolvió la lista de Pokémon para elegir un oponente.");
  }

  speciesCount = data.count;
  return speciesCount;
}

function statValue(data, name) {
  const value = data.stats?.find((entry) => entry.stat?.name === name)?.base_stat;
  return Number.isFinite(value) ? value : null;
}

function mapPokemon(data) {
  const hp = statValue(data, "hp");
  const attack = statValue(data, "attack");
  const defense = statValue(data, "defense");
  const specialAttack = statValue(data, "special-attack");
  const specialDefense = statValue(data, "special-defense");
  const speed = statValue(data, "speed");
  const sprite = data.sprites?.front_default;
  const types = [...(data.types || [])]
    .sort((left, right) => left.slot - right.slot)
    .map((entry) => entry.type?.name)
    .filter(Boolean);

  if (!data.id || !data.name || !hp || hp <= 0 || !sprite || !attack || !defense || !specialAttack || !specialDefense || !speed || !types.length) {
    throw new Error("PokéAPI devolvió datos incompletos para este Pokémon.");
  }

  return {
    id: data.id,
    name: data.name,
    hp,
    sprite,
    types,
    stats: { hp, attack, defense, specialAttack, specialDefense, speed }
  };
}

function learnLevel(entry) {
  const levels = (entry.version_group_details || [])
    .map((detail) => detail.level_learned_at)
    .filter((level) => level > 0);
  return levels.length ? Math.min(...levels) : 1000;
}

export async function getMoveDetails(name) {
  let response;
  try {
    response = await fetch(`${POKEAPI}/move/${encodeURIComponent(name)}`);
  } catch {
    return null;
  }
  if (!response.ok) return null;
  const data = await response.json();
  const damageClass = data.damage_class?.name;
  if (data.power == null || (damageClass !== "physical" && damageClass !== "special") || !data.type?.name) {
    return null;
  }
  return {
    name: data.name,
    type: data.type.name,
    power: data.power,
    accuracy: data.accuracy,
    priority: data.priority ?? 0,
    damageClass
  };
}

export async function getOffensiveMoves(pokemon) {
  const ordered = [...(pokemon.moves || [])].sort((left, right) => learnLevel(left) - learnLevel(right));
  const selected = [];
  let requests = 0;
  for (const entry of ordered) {
    if (selected.length >= 4 || requests >= 12) break;
    requests += 1;
    const move = await getMoveDetails(entry.move?.name);
    if (move) selected.push(move);
  }
  if (!selected.length) {
    throw new Error(`No se encontraron movimientos ofensivos para ${pokemon.name}.`);
  }
  return selected;
}

export async function getPokemon(identifier) {
  const query = String(identifier ?? "").trim().toLowerCase();
  if (!query) {
    throw new Error("Escribe el nombre o el número de un Pokémon.");
  }

  let response;
  try {
    response = await fetch(`${POKEAPI}/pokemon/${encodeURIComponent(query)}`);
  } catch {
    throw new Error("No se pudo conectar con PokéAPI. Revisa tu conexión e inténtalo de nuevo.");
  }

  if (response.status === 404) {
    throw new Error(`No se encontró el Pokémon "${identifier}".`);
  }

  const data = await readJson(response, "PokéAPI no respondió correctamente. Inténtalo de nuevo.");
  const pokemon = mapPokemon(data);
  pokemon.moves = await getOffensiveMoves(data);
  return pokemon;
}

export async function getRandomPokemon(excludeId) {
  const count = await getSpeciesCount();

  for (let attempt = 0; attempt < 8; attempt += 1) {
    const candidateId = Math.floor(Math.random() * count) + 1;
    if (excludeId != null && candidateId === excludeId) continue;

    try {
      const pokemon = await getPokemon(candidateId);
      if (excludeId != null && pokemon.id === excludeId) continue;
      return pokemon;
    } catch {
      if (attempt === 7) {
        throw new Error("No se pudo generar un Pokémon enemigo. Inténtalo de nuevo.");
      }
    }
  }

  throw new Error("No se pudo generar un Pokémon enemigo. Inténtalo de nuevo.");
}
