/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './app/**/*.{js,jsx,ts,tsx}',
    './src/**/*.{js,jsx,ts,tsx}',
  ],
  presets: [require('nativewind/preset')],
  theme: {
    extend: {
      colors: {
        surface: { 700: '#15151a', 800: '#0d0d12', 900: '#08080c', 950: '#060911' },
        accent: {
          DEFAULT: '#22d3ee',
          light: '#22D3EE',
          dark: '#0891B2',
          secondary: '#a78bfa',
        },
        aura: {
          cyan: '#22d3ee',
          purple: '#a78bfa',
          green: '#34d399',
          red: '#f87171',
          amber: '#fbbf24',
        },
      },
    },
  },
  plugins: [],
};
