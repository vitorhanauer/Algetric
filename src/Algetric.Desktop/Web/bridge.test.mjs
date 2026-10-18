import { test } from 'node:test';
import assert from 'node:assert/strict';
import { desiredMode, acceptState } from './bridge.mjs';
test('zoom transition has hysteresis', () => {
  assert.equal(desiredMode('3D', 50_000), '2D');
  assert.equal(desiredMode('2D', 70_000), '2D');
  assert.equal(desiredMode('3D', 70_000), '3D');
  assert.equal(desiredMode('2D', 100_000), '3D');
  assert.equal(desiredMode('2D', NaN), '2D');
});
test('late snapshots do not overwrite new revisions', () => {
  assert.equal(acceptState({session:'a', revision:3}, {session:'a', revision:2}), false);
  assert.equal(acceptState({session:'a', revision:3}, {session:'b', revision:0}), true);
});
