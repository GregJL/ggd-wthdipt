import type { InventoryItem } from './types';

export default function ItemDetails({ item }: { item: InventoryItem }) {
  const date = (value?: string | null) => value ? new Date(`${value}T12:00:00`).toLocaleDateString() : null;
  const money = (value?: number | null) => value == null ? null : new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(value);
  const rows = [
    ['Category', item.category], ['Tags', item.tags?.join(', ')], ['Brand', item.brand], ['Model', item.model],
    ['Serial number', item.serialNumber], ['Condition', item.condition], ['Purchased', date(item.purchaseDate)],
    ['Purchase price (each)', money(item.purchasePrice)], ['Replacement value (each)', money(item.replacementValue)],
    ['Warranty ends', date(item.warrantyExpiresOn)], ['Warranty notes', item.warrantyNotes],
  ].filter(([, value]) => value != null && value !== '');
  if (!rows.length) return null;
  return <details className="item-details"><summary>Details</summary><dl>{rows.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}</dl></details>;
}
