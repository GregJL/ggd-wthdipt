import assert from 'node:assert/strict';
import { filterInventory, emptyFilters } from '../src/searchFilters.ts';
const location = { id: 'shelf', parentId: null, name: 'Shelf', status: 'active', isEditLocked: false, isMoveLocked: false };
const item = { id: 'drill', locationId: 'shelf', name: 'Drill', quantity: 1, isEditLocked: true, isMoveLocked: false,
  category: 'Tools', tags: ['Cordless'], brand: 'Greenworks', model: 'Example-model', serialNumber: 'TEST-123',
  condition: 'Needs repair', warrantyNotes: 'Coverage reference', purchaseDate: '2026-01-15', purchasePrice: 0 };
for (const query of ['tools', 'CORDLESS', 'Greenworks', 'example-model', 'test-123', 'needs repair', 'coverage', '2026-01-15', '0']) {
  assert.deepEqual(filterInventory([item], [location], query, emptyFilters, null).items.map(x => x.id), ['drill']);
}
assert.equal(filterInventory([item], [location], 'Greenworks', { ...emptyFilters, editLock: 'yes' }, null).items.length, 1);
assert.equal(filterInventory([item], [location], 'Greenworks', { ...emptyFilters, editLock: 'no' }, null).items.length, 0);
assert.equal(filterInventory([{ id: 'legacy', name: 'Old item', quantity: 1, locationId: 'shelf' }], [location], 'old', emptyFilters, null).items.length, 1);
console.log('PASS: richer item search, filter composition, and legacy items.');
