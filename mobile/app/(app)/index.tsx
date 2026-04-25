import { View, Text, ScrollView, RefreshControl, ActivityIndicator } from 'react-native';
import { useQuery } from '@tanstack/react-query';
import { useState, useCallback } from 'react';
import { api } from '@/lib/api';
import { KpiCard } from '@/components/KpiCard';

function formatNumber(n: number | undefined): string {
  if (n == null) return '–';
  return n.toLocaleString('en-US');
}

function formatCurrency(n: number | undefined): string {
  if (n == null) return '–';
  return `$${(n / 1000).toFixed(1)}K`;
}

export default function Dashboard() {
  const [refreshing, setRefreshing] = useState(false);
  const { data, refetch, isLoading } = useQuery({
    queryKey: ['dashboardStats'],
    queryFn: () => api.getStats(),
  });

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    await refetch();
    setRefreshing(false);
  }, [refetch]);

  return (
    <ScrollView
      className="flex-1 bg-surface-900"
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor="#22d3ee" />}
    >
      <View className="p-4 pt-12">
        <Text className="text-xs text-white/40 uppercase tracking-widest font-mono mb-3">Overview</Text>
        {isLoading ? (
          <ActivityIndicator color="#22d3ee" />
        ) : (
          <View className="gap-2">
            <KpiCard label="Active users" value={formatNumber(data?.activeUsers)} accent="cyan" />
            <KpiCard label="Subscriptions" value={formatNumber(data?.activeSubscriptions)} accent="purple" />
            <KpiCard label="MRR" value={formatCurrency(data?.mrr)} accent="green" />
            <KpiCard label="Recent payments" value={formatNumber(data?.recentPaymentsCount)} accent="amber" />
          </View>
        )}
      </View>
    </ScrollView>
  );
}
