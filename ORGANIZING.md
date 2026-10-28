# Help Me Fucking Organize This Shit

Open **Organize this shit** in the header for the organizing dashboard.

## First steps

1. Create or edit a physical location and tick **Staging area for sorting and
   organizing**. Its nested boxes/shelves also count as staging.
2. Use an item's **Organize** action to flag **Needs sorting** and choose an optional
   planned destination. Saving a plan does not move the item.
3. Physically relocate the item, then use **Move to plan** to update its recorded
   location. The original item ID and normal movement history are preserved.
4. Use **Mark sorted / clear plan** when ready. This changes the sorting flag and
   clears the plan; it does not move the item. An item physically left in a staging
   location remains visible in the staging queue.

The dashboard shows active inventory entries needing sorting, entries in staging,
entries with a plan, and completed tasks out of the current task list. It does not
claim that older unflagged items are already organized. Counts are inventory entries,
not the sum of quantities. Archived locations and their contents are excluded from
the item queues. Restore them to bring their flags/plans back into those queues.

## Tasks and photos

Create, edit, complete, reopen, or delete organization tasks. Link a task to an item
or location, or leave it household-wide. Task completion does not silently move or
mark linked items sorted. Linked task names are retained if inventory is deleted.
Task changes appear in activity history with the signed-in user.

Photo upload/edit supports **General**, **Before organizing**, and **After organizing**
labels. Filter galleries by label to review progress. Existing photos remain General
until labeled. Both items and locations support before/after photos.

## Locks and destination safety

Organization flags/plans respect edit locks. Actual relocation respects move locks.
The actions remain independent, so a move never silently clears an edit-locked plan.
Plans require an available destination in the same household. If a destination is
later archived, choose another destination or clear the plan. Permanent location
deletion is blocked while items still plan to go there.

## Install after merge

From the project directory:

```powershell
git pull
.\stop-background-ggd.cmd
dotnet ef database update --project server
.\publish-ggd.cmd
.\start-background-ggd.cmd
```

The AddOrganizationWorkflow migration adds staging/sorting flags, a nullable planned
destination, photo labels, and a task table. Existing inventory/photo IDs, hierarchy,
metadata, and history are retained. Reload the app on each device after publishing.

## Checks

```powershell
dotnet run --project tests/Ggd.AuthTests
node --experimental-strip-types client/tests/organization.mjs
```

Endpoint checks cover staging, planning without moving, independent locks, archived
and foreign-household destination rejection, safe deletion, task lifecycle/retained
references, before/after labels, and readable attributed history. Frontend data checks
cover nested staging, archived descendants, and queue counts.
