# Optional item details

Add/edit forms retain name, quantity, and notes at the top. Expand **Optional details**
to enter category, comma-separated tags, brand, model, serial number, condition,
purchase date/price, replacement value, warranty end date, or warranty notes.
Each field is optional; existing items start blank with an empty tag list.

Prices and replacement values are USD **per item**, not totals for the quantity.
Zero is valid. Dates are date-only values. Tags are trimmed and deduplicated without
case sensitivity; up to 20 tags of 50 characters each are accepted. Other field
lengths and price ranges are validated by the server as well as the form.

An item's **Details** expander shows populated values. Global search includes the
new fields, and activity history records readable changes. Edit locks still apply.

API updates preserve metadata fields omitted from a request, so older clients do
not accidentally erase them. Send an explicit null (or an empty array for tags) to
clear a field. New create requests can omit every metadata field.

## Install after merge

From the existing project directory:

```powershell
git pull
.\stop-background-ggd.cmd
dotnet ef database update --project server
.\publish-ggd.cmd
.\start-background-ggd.cmd
```

Apply the included AddOptionalItemDetails migration before starting this version.
It adds columns to Items and retains the existing inventory and migration history.
Reload the app on each device to pick up the new forms.

## Checks

```powershell
dotnet run --project tests/Ggd.AuthTests
node --experimental-strip-types client/tests/item-search.mjs
```

Endpoint checks cover optional creates, metadata round trips, date/money values,
legacy and partial updates, normalization, limits, edit locks, explicit clearing,
and activity records. The search checks cover metadata and existing filters.
