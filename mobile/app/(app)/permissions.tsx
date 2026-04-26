import { useState } from 'react';
import { View, Text, ScrollView, ActivityIndicator, RefreshControl } from 'react-native';
import { usePermissionRequests, useApproveRequest, useDenyRequest } from '@/hooks/usePermissionRequests';
import { PermissionRequestRow } from '@/components/PermissionRequestRow';
import { DenyModal } from '@/components/DenyModal';

export default function Permissions() {
  const { data, isLoading, refetch, isRefetching } = usePermissionRequests();
  const approve = useApproveRequest();
  const deny = useDenyRequest();
  const [denyTarget, setDenyTarget] = useState<string | null>(null);

  const onDenySubmit = async (reviewNote: string) => {
    if (denyTarget) {
      await deny.mutateAsync({ id: denyTarget, reviewNote });
      setDenyTarget(null);
    }
  };

  return (
    <ScrollView
      className="flex-1 bg-surface-900"
      refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor="#22d3ee" />}
    >
      <View className="p-4 pt-12">
        <Text className="text-xs text-white/40 uppercase tracking-widest font-mono mb-3">Permission requests</Text>
        {isLoading ? (
          <ActivityIndicator color="#22d3ee" />
        ) : data?.items?.length ? (
          data.items.map(req => (
            <PermissionRequestRow
              key={req.id}
              req={req}
              onApprove={(id) => approve.mutate({ id })}
              onDeny={(id) => setDenyTarget(id)}
            />
          ))
        ) : (
          <Text className="text-white/40 text-sm text-center mt-12">No pending requests.</Text>
        )}
      </View>
      <DenyModal
        visible={!!denyTarget}
        onClose={() => setDenyTarget(null)}
        onSubmit={onDenySubmit}
      />
    </ScrollView>
  );
}
