import { useState } from 'react';
import type { InventoryItem, ItemDraft } from './types';

export default function ItemEditor({ initial, onSubmit }: { initial?: InventoryItem; onSubmit: (draft: ItemDraft) => Promise<void> }) {
  const [name, setName] = useState(initial?.name ?? '');
  const [quantity, setQuantity] = useState(initial?.quantity ?? 1);
  const [note, setNote] = useState(initial?.note ?? '');
  const [values, setValues] = useState({
    category: initial?.category ?? '', tags: initial?.tags?.join(', ') ?? '',
    brand: initial?.brand ?? '', model: initial?.model ?? '', serialNumber: initial?.serialNumber ?? '',
    purchaseDate: initial?.purchaseDate ?? '', purchasePrice: initial?.purchasePrice?.toString() ?? '',
    replacementValue: initial?.replacementValue?.toString() ?? '', warrantyExpiresOn: initial?.warrantyExpiresOn ?? '',
    warrantyNotes: initial?.warrantyNotes ?? '', condition: initial?.condition ?? '',
  });
  const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  const set = (key: keyof typeof values, value: string) => setValues((known) => ({ ...known, [key]: value }));
  const optional = (value: string) => value.trim() || null;
  return <form onSubmit={async (event) => {
    event.preventDefault(); setError(''); setBusy(true);
    try {
      await onSubmit({ name: name.trim(), quantity, note: note.trim() || undefined,
        category: optional(values.category), tags: values.tags.split(',').map((tag) => tag.trim()).filter(Boolean),
        brand: optional(values.brand), model: optional(values.model), serialNumber: optional(values.serialNumber),
        purchaseDate: values.purchaseDate || null, purchasePrice: values.purchasePrice === '' ? null : Number(values.purchasePrice),
        replacementValue: values.replacementValue === '' ? null : Number(values.replacementValue),
        warrantyExpiresOn: values.warrantyExpiresOn || null, warrantyNotes: optional(values.warrantyNotes), condition: optional(values.condition),
      });
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not save this item.'); }
    finally { setBusy(false); }
  }}>
    <label>Item name<input required autoFocus value={name} onChange={(e) => setName(e.target.value)} placeholder="What are we trying not to lose?"/></label>
    <label>Quantity<input type="number" required min="1" step="1" value={quantity} onChange={(e) => setQuantity(Number(e.target.value))}/></label>
    <label>Notes<textarea value={note} onChange={(e) => setNote(e.target.value)}/></label>
    <details className="optional-details" onInvalidCapture={(event) => { event.currentTarget.open = true; }}>
      <summary>Optional details</summary><div className="optional-fields">
        {([
          ['category', 'Category', 100], ['brand', 'Brand', 100], ['model', 'Model', 200],
          ['serialNumber', 'Serial number', 200], ['condition', 'Condition', 100],
        ] as const).map(([key, label, max]) => <label key={key}>{label}<input maxLength={max} list={key === 'condition' ? 'item-condition-examples' : undefined} value={values[key]} onChange={(e) => set(key, e.target.value)}/></label>)}
        <datalist id="item-condition-examples">{['New', 'Like new', 'Good', 'Fair', 'Poor', 'Needs repair', 'For parts'].map((condition) => <option key={condition} value={condition}/>)}</datalist>
        <label>Tags<input value={values.tags} onChange={(e) => set('tags', e.target.value)} placeholder="tools, workshop, cordless"/><small>Separate tags with commas; up to 20 tags.</small></label>
        <div className="form-row"><label>Purchase date<input type="date" value={values.purchaseDate} onChange={(e) => set('purchaseDate', e.target.value)}/></label><label>Warranty ends<input type="date" value={values.warrantyExpiresOn} onChange={(e) => set('warrantyExpiresOn', e.target.value)}/></label></div>
        <div className="form-row">{(['purchasePrice', 'replacementValue'] as const).map((key) => <label key={key}>{key === 'purchasePrice' ? 'Purchase price' : 'Replacement value'} (USD each)<input type="number" min="0" max="9999999999.99" step="0.01" value={values[key]} onChange={(e) => set(key, e.target.value)}/></label>)}</div>
        <label>Warranty notes<textarea maxLength={2000} value={values.warrantyNotes} onChange={(e) => set('warrantyNotes', e.target.value)} placeholder="Coverage, provider, reference number…"/></label>
      </div>
    </details>
    {error && <p role="alert">{error}</p>}
    <button className="submit" disabled={busy}>{busy ? 'Saving…' : initial ? 'Save changes' : 'Add item'}</button>
  </form>;
}
