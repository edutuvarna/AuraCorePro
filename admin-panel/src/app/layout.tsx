import type { Metadata, Viewport } from 'next'
import './globals.css'

export const metadata: Metadata = {
  title: 'AuraCore Pro — Admin',
  description: 'Admin panel for AuraCore Pro',
  manifest: '/manifest.json',
  appleWebApp: {
    capable: true,
    statusBarStyle: 'black-translucent',
    title: 'AuraCore',
  },
  robots: { index: false, follow: false },
}

export const viewport: Viewport = {
  themeColor: '#22d3ee',
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  )
}
