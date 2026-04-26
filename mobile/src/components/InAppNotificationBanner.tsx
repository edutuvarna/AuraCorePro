import { useEffect, useRef } from 'react';
import { View, Text, Pressable, Animated } from 'react-native';

export function InAppNotificationBanner({
  visible,
  title,
  body,
  onDismiss,
  onTap,
}: {
  visible: boolean;
  title: string;
  body: string;
  onDismiss: () => void;
  onTap?: () => void;
}) {
  const slide = useRef(new Animated.Value(-80)).current;

  useEffect(() => {
    if (visible) {
      Animated.timing(slide, { toValue: 0, duration: 250, useNativeDriver: true }).start();
      const t = setTimeout(onDismiss, 5000);
      return () => clearTimeout(t);
    } else {
      Animated.timing(slide, { toValue: -80, duration: 200, useNativeDriver: true }).start();
    }
  }, [visible]);

  if (!visible) return null;

  return (
    <Animated.View
      style={{ transform: [{ translateY: slide }] }}
      className="absolute top-12 left-3 right-3 z-50"
    >
      <Pressable
        onPress={onTap}
        className="bg-surface-900/95 border border-white/10 border-l-2 border-l-accent rounded-lg p-3"
      >
        <Text className="text-accent text-[10px] font-mono uppercase tracking-widest">{title}</Text>
        <Text className="text-white text-sm mt-1">{body}</Text>
      </Pressable>
    </Animated.View>
  );
}
