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

function mapPokemon(data) {
  const hpStat = data.stats?.find((entry) => entry.stat?.name === "hp");
  const hp = hpStat?.base_stat;
  const sprite = data.sprites?.front_default;

  if (!data.id || !data.name || !Number.isFinite(hp) || hp <= 0 || !sprite) {
    throw new Error("PokéAPI devolvió datos incompletos para este Pokémon.");
  }

  return {
    id: data.id,
    name: data.name,
    hp,
    sprite
  };
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
  return mapPokemon(data);
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
