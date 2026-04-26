import { View, Text, Pressable } from 'react-native';
import type { PermissionRequest } from '@/hooks/usePermissionRequests';

export function PermissionRequestRow({
  req, onApprove, onDeny,
}: {
  req: PermissionRequest;
  onApprove: (id: string) => void;
  onDeny: (id: string) => void;
}) {
  return (
    <View className="bg-white/[0.03] border border-white/[0.06] rounded-lg p-3 mb-2">
      <Text className="text-white text-sm font-medium">{req.adminEmail}</Text>
      <Text className="text-accent text-xs font-mono mt-1">{req.permissionKey}</Text>
      {req.reason ? <Text className="text-white/60 text-xs mt-2">{req.reason}</Text> : null}
      <View className="flex-row gap-2 mt-3">
        <Pressable onPress={() => onApprove(req.id)} className="flex-1 bg-accent rounded-md py-2 items-center">
          <Text className="text-surface-900 text-xs font-semibold">Approve</Text>
        </Pressable>
        <Pressable onPress={() => onDeny(req.id)} className="flex-1 bg-white/[0.05] border border-white/10 rounded-md py-2 items-center">
          <Text className="text-white/80 text-xs font-semibold">Deny</Text>
        </Pressable>
      </View>
    </View>
  );
}
