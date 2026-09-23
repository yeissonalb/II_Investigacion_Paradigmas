import assert from "node:assert/strict";
import test from "node:test";
import { pickEnemyMove } from "./src/enemy.js";

test("el rival elige uno de sus movimientos ofensivos", () => {
  const moves = [
    { name: "ember" },
    { name: "scratch" }
  ];
  assert.equal(pickEnemyMove(moves, 1).name, "scratch");
  assert.equal(pickEnemyMove([], 0), null);
});
