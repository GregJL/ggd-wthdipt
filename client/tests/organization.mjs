import assert from 'node:assert/strict';
import { organizationView } from '../src/organization.ts';
const locations = [
  { id: 'root', parentId: null, status: 'active', name: 'Home' },
  { id: 'stage', parentId: 'root', status: 'temporary', name: 'Staging', isStaging: true },
  { id: 'box', parentId: 'stage', status: 'active', name: 'Box' },
  { id: 'archive', parentId: 'stage', status: 'inactive', name: 'Archived box' },
];
const items = [
  { id: 'staged', locationId: 'box', name: 'Staged item' },
  { id: 'flagged', locationId: 'root', needsSorting: true },
  { id: 'planned', locationId: 'root', plannedLocationId: 'box' },
  { id: 'legacy', locationId: 'root' },
  { id: 'hidden', locationId: 'archive', needsSorting: true },
];
const view = organizationView(items, locations);
assert.deepEqual(view.staged.map(x => x.id), ['staged']);
assert.deepEqual(view.needsSorting.map(x => x.id), ['flagged']);
assert.deepEqual(view.planned.map(x => x.id), ['planned']);
assert.deepEqual(view.attention.map(x => x.id), ['staged', 'flagged', 'planned']);
assert.equal(view.staging.has('archive'), false);
console.log('PASS: nested staging, archived contents, attention counts, and neutral legacy items.');
