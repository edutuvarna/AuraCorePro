module.exports = function (api) {
  api.cache(true);
  return {
    presets: [
      ['babel-preset-expo', { jsxImportSource: 'nativewind' }],
      'nativewind/babel',
    ],
    // react-native-worklets/plugin must be LAST. Reanimated 4.x split its babel
    // plugin out into the worklets package; the plugin transforms function
    // bodies marked with 'worklet' (used by NativeWind v4 + react-native-
    // css-interop animations + InAppNotificationBanner translateY).
    plugins: ['react-native-worklets/plugin'],
  };
};
