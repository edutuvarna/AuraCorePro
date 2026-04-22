# AuraCore Pro — Admin Panel

Modern web-based admin panel for AuraCore Pro. Built with Next.js 14, Tailwind CSS, TypeScript.

## Features
- 🔐 JWT-based admin authentication
- 📊 Real-time dashboard with stats and conversion funnel
- 👤 User management (search, delete, revoke)
- 💳 Payment history with pending crypto alerts
- 👑 Subscription management (grant Pro/Enterprise)
- 🔑 Password reset
- 🏥 Server health monitoring with latency check

## Setup

```bash
npm install
npm run dev        # → http://localhost:3000
```

## Environment
Create `.env.local`:
```
NEXT_PUBLIC_API_URL=http://localhost:5000
```

For production (Netlify):
```
NEXT_PUBLIC_API_URL=https://api.auracore.pro
```

## Deploy to Netlify

### Option 1: Git-based (recommended)
1. Push this folder to a GitHub repo
2. Netlify → New Site → Import from Git
3. Build command: `npm run build`
4. Publish directory: `out`
5. Set environment variable: `NEXT_PUBLIC_API_URL`

### Option 2: Manual deploy
```bash
npm run build
npx netlify deploy --prod --dir=out
```

## Tech Stack
- Next.js 14 (Static Export)
- Tailwind CSS (custom dark theme)
- TypeScript
- Lucide React (icons)
- Recharts (charts)
