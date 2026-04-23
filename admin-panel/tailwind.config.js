/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      colors: {
        surface: {
          50: '#f0f4ff',
          100: '#e0e7ff',
          // Phase 6.10 W3 hybrid Glass + Terminal additions
          700: '#15151a',
          800: '#0d0d12',
          900: '#08080c',
          950: '#060911',
        },
        accent: {
          // Existing accent palette (preserved — sidebar-item, btn-primary,
          // accent-glow, input-dark, LoginScreen all consume these)
          DEFAULT: '#22d3ee', // bumped to plan's hybrid cyan (was #06B6D4)
          light: '#22D3EE',
          dark: '#0891B2',
          glow: 'rgba(6, 182, 212, 0.15)',
          // Phase 6.10 W3 addition
          secondary: '#a78bfa',
        },
        aura: {
          // Phase 6.10 W3 hybrid Glass + Terminal palette
          cyan: '#22d3ee',
          purple: '#a78bfa',
          green: '#34d399',
          red: '#f87171',
          amber: '#fbbf24',
          // Preserved from earlier phases (still in use by badge-blue +
          // semantic helpers across pages)
          blue: '#3B82F6',
          pink: '#EC4899',
        }
      },
      fontFamily: {
        // Phase 6.10 W3 hybrid: Outfit body+display, JetBrains Mono mono
        display: ['Outfit', 'ui-sans-serif', 'system-ui', 'sans-serif'],
        body: ['Outfit', 'ui-sans-serif', 'system-ui', 'sans-serif'],
        mono: ['JetBrains Mono', 'ui-monospace', 'monospace'],
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
