import { useCallback, useEffect, useRef, useState } from "react";
import { api, describeCall } from "./api.js";
import { Arena, EventTimeline, StatsGrid } from "./components/Arena.jsx";
import { BattleList, StartBattleForm } from "./components/Sidebar.jsx";
import { decideEnemyAction, rollEnemy, rollEnemyDamage, rollEnemyDelay, rollEnemyHeal } from "./enemy.js";

const emptyForm = {
  battleId: "",
  heroName: "Guerrero",
  heroHp: 100,
  enemyName: ""
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
        : await api.attack(id, { attacker: "Enemy", damage: rollEnemyDamage() });
    } catch (error) {
      if (action !== "heal") {
        rememberCall(error.entry);
        setEnemyStatus(error.message);
        return;
      }
      try {
        action = "attack";
        result = await api.attack(id, { attacker: "Enemy", damage: rollEnemyDamage() });
      } catch (retryError) {
        rememberCall(retryError.entry);
        setEnemyStatus(retryError.message);
        return;
      }
    }

    rememberCall(result.entry);
    if (action === "attack") {
      setEnemyStatus(`${name} ataca por ${result.data.damage} de daño${result.data.isCritical ? " (crítico)" : ""}`);
    } else {
      setEnemyStatus(`${name} se cura +${result.data.healedAmount} HP`);
    }
    await waitForProjection(id, previousCount);
  }

  async function handleStart(event) {
    event.preventDefault();
    setBusy(true);
    try {
      const enemy = rollEnemy(form.enemyName);
      const payload = {
        battleId: form.battleId.trim() || undefined,
        heroName: form.heroName || "Guerrero",
        heroHp: Number(form.heroHp) || 100,
        enemyName: enemy.enemyName,
        enemyHp: enemy.enemyHp
      };
      const { data, entry } = await api.startBattle(payload);
      rememberCall(entry);
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
      rememberCall(error.entry);
      showToast(error.message);
    } finally {
      setBusy(false);
    }
  }

  async function handleAction(action) {
    if (!selectedId) return;
    setBusy(true);
    const value = Number(amount);
    const optionalAmount = Number.isFinite(value) && value > 0 ? value : undefined;
    const previousCount = history.length;

    try {
      const result = action === "attack"
        ? await api.attack(selectedId, { attacker: "Hero", damage: optionalAmount })
        : await api.heal(selectedId, { target: "Hero", amount: optionalAmount });
      rememberCall(result.entry);
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
    <>
      <div className="bg-glow" />
      <header className="topbar">
        <div className="brand">
          <span className="brand-mark" aria-hidden="true">⚔</span>
          <div>
            <h1>Event Arena</h1>
            <p>Event Sourcing + CQRS · Combate por turnos</p>
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

      <main className="layout">
        <aside className="panel sidebar">
          <BattleList
            battles={battles}
            selectedId={selectedId}
            onSelect={(id) => {
              previousHp.current = { hero: null, enemy: null };
              setEnemyStatus("");
              setSelectedId(id);
            }}
          />
          <StartBattleForm form={form} onChange={setForm} onSubmit={handleStart} busy={busy} />
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
          <EventTimeline history={history} />
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
    </>
  );
}
