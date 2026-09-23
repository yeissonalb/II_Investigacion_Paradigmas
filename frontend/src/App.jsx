import { useCallback, useEffect, useRef, useState } from "react";
import { api, describeCall } from "./api.js";
import { Arena, EventTimeline, StatsGrid } from "./components/Arena.jsx";
import { BattleList, StartBattleForm } from "./components/Sidebar.jsx";
import { decideEnemyAction, pickEnemyMove, rollEnemyDamage, rollEnemyDelay, rollEnemyHeal } from "./enemy.js";
import { getPokemon, getRandomPokemon } from "./pokeApi.js";

const emptyForm = {
  battleId: "",
  pokemonQuery: ""
};

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export default function App() {
  const [health, setHealth] = useState({ a: false, b: false });
  const [battles, setBattles] = useState([]);
  const [selectedId, setSelectedId] = useState(null);
  const [detail, setDetail] = useState(null);
  const [pending, setPending] = useState(null);
  const [history, setHistory] = useState([]);
  const [stats, setStats] = useState(null);
  const [busy, setBusy] = useState(false);
  const [amount, setAmount] = useState("");
  const [form, setForm] = useState(emptyForm);
  const [toast, setToast] = useState(null);
  const [lastCommand, setLastCommand] = useState(null);
  const [lastQuery, setLastQuery] = useState(null);
  const [heroFx, setHeroFx] = useState("");
  const [enemyFx, setEnemyFx] = useState("");
  const [enemyStatus, setEnemyStatus] = useState("");
  const [player, setPlayer] = useState(null);
  const [pickerOpen, setPickerOpen] = useState(true);
  const [searchError, setSearchError] = useState("");
  const [opponentStatus, setOpponentStatus] = useState("");

  const previousHp = useRef({ hero: null, enemy: null });

  const showToast = useCallback((message, ok = false) => {
    setToast({ message, ok });
  }, []);

  useEffect(() => {
    if (!toast) return undefined;
    const timer = setTimeout(() => setToast(null), 3200);
    return () => clearTimeout(timer);
  }, [toast]);

  const rememberCall = useCallback((entry) => {
    if (!entry || entry.silent) return;
    if (entry.side === "command") setLastCommand(entry);
    else setLastQuery(entry);
  }, []);

  const flashIfNeeded = useCallback((nextDetail) => {
    const prev = previousHp.current;
    if (prev.hero != null && nextDetail?.hero) {
      if (nextDetail.hero.hp < prev.hero) setHeroFx("hit");
      if (nextDetail.hero.hp > prev.hero) setHeroFx("heal");
    }
    if (prev.enemy != null && nextDetail?.enemy) {
      if (nextDetail.enemy.hp < prev.enemy) setEnemyFx("hit");
      if (nextDetail.enemy.hp > prev.enemy) setEnemyFx("heal");
    }
    previousHp.current = {
      hero: nextDetail?.hero?.hp ?? null,
      enemy: nextDetail?.enemy?.hp ?? null
    };
  }, []);

  useEffect(() => {
    if (!heroFx && !enemyFx) return undefined;
    const timer = setTimeout(() => {
      setHeroFx("");
      setEnemyFx("");
    }, 600);
    return () => clearTimeout(timer);
  }, [heroFx, enemyFx]);

  const refreshHealth = useCallback(async () => {
    try {
      await api.healthA();
      setHealth((current) => ({ ...current, a: true }));
    } catch {
      setHealth((current) => ({ ...current, a: false }));
    }
    try {
      await api.healthB();
      setHealth((current) => ({ ...current, b: true }));
    } catch {
      setHealth((current) => ({ ...current, b: false }));
    }
  }, []);

  const refreshList = useCallback(async () => {
    try {
      const { data } = await api.listBattles();
      setBattles(Array.isArray(data) ? data : []);
    } catch {
      setBattles([]);
    }
  }, []);

  const refreshSelected = useCallback(async (id = selectedId) => {
    if (!id) {
      setDetail(null);
      setPending(null);
      setHistory([]);
      setStats(null);
      previousHp.current = { hero: null, enemy: null };
      return { detail: null, historyLength: 0 };
    }

    try {
      const [detailRes, historyRes, statsRes] = await Promise.all([
        api.getBattle(id),
        api.getHistory(id),
        api.getStats(id)
      ]);
      const nextHistory = historyRes.data?.history || [];
      rememberCall(detailRes.entry);
      setDetail(detailRes.data);
      setPending(null);
      setHistory(nextHistory);
      setStats(statsRes.data?.stats || null);
      flashIfNeeded(detailRes.data);
      return { detail: detailRes.data, historyLength: nextHistory.length };
    } catch {
      setDetail(null);
      setPending(id);
      return { detail: null, historyLength: 0 };
    }
  }, [selectedId, rememberCall, flashIfNeeded]);

  useEffect(() => {
    refreshHealth();
    refreshList();
    const healthTimer = setInterval(refreshHealth, 5000);
    const listTimer = setInterval(refreshList, 3000);
    return () => {
      clearInterval(healthTimer);
      clearInterval(listTimer);
    };
  }, [refreshHealth, refreshList]);

  useEffect(() => {
    refreshSelected();
    const timer = setInterval(() => refreshSelected(), 1500);
    return () => clearInterval(timer);
  }, [refreshSelected]);

  async function waitForProjection(id, previousCount) {
    let projected = { detail: null, historyLength: previousCount };
    for (let i = 0; i < 6; i += 1) {
      await sleep(200);
      projected = await refreshSelected(id);
      if (projected.historyLength > previousCount) break;
    }
    return projected;
  }

  async function runEnemyTurn(id, snapshot) {
    if (!snapshot || snapshot.isFinished) return;
    const name = snapshot.enemy.name;
    setEnemyStatus(`${name} está decidiendo...`);
    await sleep(rollEnemyDelay());

    const latest = (await refreshSelected(id)).detail || snapshot;
    if (!latest || latest.isFinished) {
      setEnemyStatus("");
      return;
    }

    const previousCount = (await refreshSelected(id)).historyLength;
    let action = decideEnemyAction(latest.enemy);
    let result;

    try {
      result = action === "heal"
        ? await api.heal(id, { target: "Enemy", amount: rollEnemyHeal() })
        : await api.attack(id, enemyAttackPayload(latest.enemy?.moves));
    } catch (error) {
      if (action !== "heal") {
        rememberCall(error.entry);
        setEnemyStatus(error.message);
        return;
      }
      try {
        action = "attack";
        result = await api.attack(id, enemyAttackPayload(latest.enemy?.moves));
      } catch (retryError) {
        rememberCall(retryError.entry);
        setEnemyStatus(retryError.message);
        return;
      }
    }

    rememberCall(result.entry);
    if (action === "attack") setEnemyFx("attack");
    else setEnemyFx("heal");
    if (action === "attack") {
      if (result.data.hit === false) {
        setEnemyStatus(`${name} usó ${result.data.moveName} pero falló`);
      } else if (result.data.moveName) {
        setEnemyStatus(`${name} usó ${result.data.moveName} por ${result.data.damage} de daño${result.data.isCritical ? " (crítico)" : ""}`);
      } else {
        setEnemyStatus(`${name} ataca por ${result.data.damage} de daño${result.data.isCritical ? " (crítico)" : ""}`);
      }
    } else {
      setEnemyStatus(`${name} se cura +${result.data.healedAmount} HP`);
    }
    await waitForProjection(id, previousCount);
  }

  function loadedPlayerMatchesQuery() {
    const query = form.pokemonQuery.trim().toLowerCase();
    if (!player || !query) return false;
    return player.name === query || String(player.id) === query;
  }

  async function handleSearch(identifier) {
    const query = typeof identifier === "string" || typeof identifier === "number"
      ? identifier
      : form.pokemonQuery;
    setBusy(true);
    setSearchError("");
    try {
      const found = await getPokemon(query);
      setPlayer(found);
      setForm((current) => ({ ...current, pokemonQuery: found.name }));
    } catch (error) {
      setPlayer(null);
      setSearchError(error.message);
    } finally {
      setBusy(false);
    }
  }

  async function handleStart(event) {
    event.preventDefault();
    setBusy(true);
    setSearchError("");
    setOpponentStatus("");
    try {
      const hero = loadedPlayerMatchesQuery() ? player : await getPokemon(form.pokemonQuery);
      setPlayer(hero);
      setForm((current) => ({ ...current, pokemonQuery: hero.name }));

      setOpponentStatus("Buscando oponente...");
      const enemy = await getRandomPokemon(hero.id);
      if (enemy.id === hero.id) {
        throw new Error("El oponente salió igual que tu Pokémon. Inténtalo de nuevo.");
      }

      const payload = {
        battleId: form.battleId.trim() || undefined,
        heroName: hero.name,
        heroHp: hero.hp,
        heroPokemonId: hero.id,
        heroSprite: hero.sprite,
        heroTypes: hero.types,
        heroAttack: hero.stats.attack,
        heroDefense: hero.stats.defense,
        heroSpecialAttack: hero.stats.specialAttack,
        heroSpecialDefense: hero.stats.specialDefense,
        heroSpeed: hero.stats.speed,
        heroMoves: hero.moves,
        enemyName: enemy.name,
        enemyHp: enemy.hp,
        enemyPokemonId: enemy.id,
        enemySprite: enemy.sprite,
        enemyTypes: enemy.types,
        enemyAttack: enemy.stats.attack,
        enemyDefense: enemy.stats.defense,
        enemySpecialAttack: enemy.stats.specialAttack,
        enemySpecialDefense: enemy.stats.specialDefense,
        enemySpeed: enemy.stats.speed,
        enemyMoves: enemy.moves
      };
      const { data, entry } = await api.startBattle(payload);
      rememberCall(entry);
      setOpponentStatus("");
      showToast(`Aparece ${data.enemy.name} con ${data.enemy.hp} HP`, true);
      setEnemyStatus(`${data.enemy.name} entra con ${data.enemy.hp} HP. Esperará su turno para atacar o curarse.`);
      previousHp.current = { hero: null, enemy: null };
      setSelectedId(data.battleId);
      await refreshList();
      for (let i = 0; i < 5; i += 1) {
        const projected = await refreshSelected(data.battleId);
        if (projected.detail) break;
        await sleep(250);
      }
    } catch (error) {
      setOpponentStatus("");
      if (error.entry) rememberCall(error.entry);
      else setSearchError(error.message);
      showToast(error.message);
    } finally {
      setBusy(false);
    }
  }

  function enemyAttackPayload(moves) {
    const move = pickEnemyMove(moves);
    return move
      ? { attacker: "Enemy", moveName: move.name }
      : { attacker: "Enemy", damage: rollEnemyDamage() };
  }

  async function handleAction(action, moveName) {
    if (!selectedId) return;
    setBusy(true);
    const value = Number(amount);
    const optionalAmount = Number.isFinite(value) && value > 0 ? value : undefined;
    const previousCount = history.length;
    const usesMoves = (detail?.hero?.moves?.length ?? 0) > 0;

    try {
      const result = action === "attack"
        ? await api.attack(selectedId, usesMoves
          ? { attacker: "Hero", moveName }
          : { attacker: "Hero", damage: optionalAmount })
        : await api.heal(selectedId, { target: "Hero", amount: optionalAmount });
      rememberCall(result.entry);
      if (action === "attack") setHeroFx("attack");
      else setHeroFx("heal");
      showToast("Evento enviado a EventStoreDB", true);
      const projected = await waitForProjection(selectedId, previousCount);
      if (projected.detail && !projected.detail.isFinished) {
        await runEnemyTurn(selectedId, projected.detail);
      } else {
        setEnemyStatus("");
      }
    } catch (error) {
      rememberCall(error.entry);
      showToast(error.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="app-shell">
      <div className="bg-glow" />
      <header className="topbar">
        <div className="brand">
          <span className="brand-mark" aria-hidden="true"><span className="pokeball" /></span>
          <div>
            <h1>Event Arena</h1>
            <p>Elige tu Pokémon, combate y revisa cada evento</p>
          </div>
        </div>
        <div className="health-cluster">
          <span className={`pill ${health.a ? "online" : ""}`}>
            <i /> Service A · comandos :3001
          </span>
          <span className={`pill ${health.b ? "online" : ""}`}>
            <i /> Service B · consultas :3002
          </span>
        </div>
      </header>

      <main className={`layout ${pickerOpen ? "" : "picker-closed"}`}>
        <aside className={`panel sidebar ${pickerOpen ? "" : "collapsed"}`}>
          <button
            type="button"
            className="drawer-toggle"
            aria-expanded={pickerOpen}
            onClick={() => setPickerOpen((open) => !open)}
          >
            <span className="pokeball" aria-hidden="true" />
            <span>{pickerOpen ? "Ocultar equipo" : "Equipo"}</span>
          </button>
          {pickerOpen && (
          <>
          <BattleList
            battles={battles}
            selectedId={selectedId}
            onSelect={(id) => {
              previousHp.current = { hero: null, enemy: null };
              setEnemyStatus("");
              setSelectedId(id);
            }}
          />
          <StartBattleForm
            form={form}
            onChange={setForm}
            onSearch={handleSearch}
            onSubmit={handleStart}
            busy={busy}
            player={loadedPlayerMatchesQuery() ? player : null}
            searchError={searchError}
            opponentStatus={opponentStatus}
          />
          </>
          )}
        </aside>

        <Arena
          detail={detail}
          pending={pending}
          busy={busy}
          amount={amount}
          onAmountChange={setAmount}
          onAction={handleAction}
          heroFx={heroFx}
          enemyFx={enemyFx}
          enemyStatus={enemyStatus}
        />

        <aside className="panel sidecar">
          <EventTimeline history={history} detail={detail} />
          <StatsGrid stats={stats} />
        </aside>
      </main>

      <footer className="inspector">
        <div>
          <span className="kicker">Inspector CQRS</span>
          <p>{describeCall(lastCommand, "Aún no se ha enviado ningún comando a Service A.")}</p>
        </div>
        <div>
          <span className="kicker">Última consulta</span>
          <p>{describeCall(lastQuery, "Esperando proyección de Service B…")}</p>
        </div>
      </footer>

      {toast && (
        <div className={`toast ${toast.ok ? "ok" : ""}`}>{toast.message}</div>
      )}
    </div>
  );
}
