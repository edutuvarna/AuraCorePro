export const TIER1_KEYS = [
  'tab:configuration',
  'tab:ipwhitelist',
  'tab:updates',
  'tab:rolechange',
] as const;

export const TIER2_KEYS = [
  'action:users.delete',
  'action:users.ban',
  'action:subscriptions.grant',
  'action:subscriptions.revoke',
  'action:payments.approveCrypto',
  'action:payments.rejectCrypto',
] as const;

export type PermissionKey = typeof TIER1_KEYS[number] | typeof TIER2_KEYS[number];

export const PERMISSION_KEYS: readonly PermissionKey[] = [...TIER1_KEYS, ...TIER2_KEYS];

export const PERMISSION_LABELS: Record<PermissionKey, string> = {
  'tab:configuration':          'Configuration tab',
  'tab:ipwhitelist':            'IP Whitelist tab',
  'tab:updates':                'Updates tab',
  'tab:rolechange':             'Role Change tab',
  'action:users.delete':        'Delete a user',
  'action:users.ban':           'Ban a user',
  'action:subscriptions.grant': 'Grant a subscription',
  'action:subscriptions.revoke':'Revoke a subscription',
  'action:payments.approveCrypto':'Approve a crypto payment',
  'action:payments.rejectCrypto': 'Reject a crypto payment',
};

export function isTabKey(k: string): boolean { return k.startsWith('tab:'); }
export function isActionKey(k: string): boolean { return k.startsWith('action:'); }
