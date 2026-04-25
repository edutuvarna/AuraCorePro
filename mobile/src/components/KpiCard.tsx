import { View, Text } from 'react-native';

export function KpiCard({ label, value, accent }: { label: string; value: string; accent?: 'cyan' | 'purple' | 'green' | 'amber' }) {
  const accentBorder =
    accent === 'cyan' ? 'border-l-accent'
    : accent === 'purple' ? 'border-l-accent-secondary'
    : accent === 'green' ? 'border-l-aura-green'
    : accent === 'amber' ? 'border-l-aura-amber'
    : 'border-l-white/30';
  return (
    <View className={`bg-white/[0.03] border border-white/[0.06] rounded-lg p-3 border-l-2 ${accentBorder}`}>
      <Text className="text-[10px] text-white/40 uppercase tracking-widest font-mono">{label}</Text>
      <Text className="text-2xl font-bold text-white mt-1">{value}</Text>
    </View>
  );
}
