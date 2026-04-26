import { useState } from 'react';
import { Modal, View, Text, TextInput, Pressable } from 'react-native';

export function DenyModal({
  visible, onClose, onSubmit,
}: {
  visible: boolean;
  onClose: () => void;
  onSubmit: (reviewNote: string) => void;
}) {
  const [note, setNote] = useState('');

  return (
    <Modal visible={visible} transparent animationType="fade">
      <View className="flex-1 items-center justify-center bg-black/60 px-6">
        <View className="w-full bg-surface-900 border border-white/10 rounded-lg p-4">
          <Text className="text-white text-base font-semibold mb-2">Deny request</Text>
          <Text className="text-white/50 text-xs mb-3">
            Optional note shown to the requester explaining why.
          </Text>
          <TextInput
            value={note}
            onChangeText={setNote}
            multiline
            placeholder="Reason (optional)…"
            placeholderTextColor="rgba(255,255,255,0.3)"
            className="bg-white/[0.03] border border-white/[0.08] rounded-md p-3 text-white text-sm min-h-[80px]"
          />
          <View className="flex-row gap-2 mt-4">
            <Pressable onPress={onClose} className="flex-1 bg-white/[0.05] border border-white/10 rounded-md py-3 items-center">
              <Text className="text-white/80 text-xs">Cancel</Text>
            </Pressable>
            <Pressable
              onPress={() => { onSubmit(note); setNote(''); }}
              className="flex-1 bg-red-500/80 rounded-md py-3 items-center"
            >
              <Text className="text-white text-xs font-semibold">Deny</Text>
            </Pressable>
          </View>
        </View>
      </View>
    </Modal>
  );
}
