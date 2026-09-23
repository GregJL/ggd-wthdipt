import type { Location } from '../types';

export const WORKSPACE_ID = '00000000-0000-0000-0000-000000000001';
const loc = (id: string, parentId: string | null, name: string, type: Location['type'], status: Location['status'] = 'active', note?: string): Location => ({ id, workspaceId: WORKSPACE_ID, parentId, name, type, status, isMoveLocked: type === 'floor' || type === 'room', isEditLocked: false, note });

export const seededLocations: Location[] = [
  loc('ground', null, 'Ground Floor', 'floor'),
  loc('basement', null, 'Basement', 'floor'),
  ...['Master Bedroom', 'Master Bath', 'Kitchen', 'Dining Room', 'Living Room', 'Front Bedroom', 'Rear Bedroom', 'Guest Bathroom', 'Sunroom', 'Garage']
    .map((name, i) => loc(`ground-${i + 1}`, 'ground', name, 'room')),
  loc('heat-press', 'basement', 'North-Side Heat Press Area', 'area'),
  loc('workshop', 'basement', 'South-Side Workshop', 'area'),
  loc('gym', 'basement', 'South-Side Home Gym', 'area'),
  loc('man-cave', 'basement', 'South-Side Man Cave', 'area'),
  loc('office', 'basement', 'South-Side Office Nook', 'area'),
  loc('under-stairs', 'basement', 'Under the Stairs', 'area'),
  loc('pellet-storage', 'basement', 'Pellet Storage', 'storage'),
  loc('bar', 'basement', 'South-Side Bar', 'area', 'planned', 'Do not inventory the bar before it is built.'),
  loc('workbench', 'workshop', 'Workbench', 'storage'),
  loc('overflow-table', 'workshop', 'Overflow Table', 'storage', 'temporary'),
  loc('tool-wall', 'workshop', 'Pegboard Tool Wall', 'storage'),
  loc('red-toolbox', 'workshop', 'Red Toolbox', 'storage'),
  loc('parts-organizers', 'workshop', 'Parts Organizers', 'storage'),
  loc('office-divider', 'office', 'Divider Cabinets', 'storage'),
  loc('office-wire-shelves', 'office', 'Wire Shelving', 'storage'),
  loc('heat-wire-shelves', 'heat-press', 'Wire Shelving', 'storage'),
  loc('heat-cabinets', 'heat-press', 'Tall Cabinets', 'storage'),
];
