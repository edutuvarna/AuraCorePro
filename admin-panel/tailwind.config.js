/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      colors: {
        surface: {
          50: '#f0f4ff',
          100: '#e0e7ff',
          800: '#141927',
          900: '#0B0F1A',
          950: '#060911',
        },
        accent: {
          DEFAULT: '#06B6D4',
          light: '#22D3EE',
          dark: '#0891B2',
          glow: 'rgba(6, 182, 212, 0.15)',
        },
        aura: {
          cyan: '#06B6D4',
          blue: '#3B82F6',
          purple: '#8B5CF6',
          pink: '#EC4899',
          amber: '#F59E0B',
          green: '#10B981',
          red: '#EF4444',
        }
      },
      fontFamily: {
        display: ['Outfit', 'sans-serif'],
        body: ['DM Sans', 'sans-serif'],
        mono: ['JetBrains Mono', 'monospace'],
      },
      animation: {
        'fade-in': 'fadeIn 0.5s ease-out',
        'slide-up': 'slideUp 0.4s ease-out',
        'pulse-glow': 'pulseGlow 2s ease-in-out infinite',
      },
      keyframes: {
        fadeIn: { '0%': { opacity: '0' }, '100%': { opacity: '1' } },
        slideUp: { '0%': { opacity: '0', transform: 'translateY(12px)' }, '100%': { opacity: '1', transform: 'translateY(0)' } },
        pulseGlow: { '0%, 100%': { boxShadow: '0 0 20px rgba(6,182,212,0.1)' }, '50%': { boxShadow: '0 0 30px rgba(6,182,212,0.25)' } },
      },
    },
  },
  plugins: [],
}
