import type { Metadata } from 'next'
import './globals.css'

export const metadata: Metadata = {
  title: 'AuraCore Pro — Admin',
  description: 'Admin panel for AuraCore Pro',
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  )
}
