// ═══════════════════════════════════════════════════════════
// MODULES DATA
// ═══════════════════════════════════════════════════════════
const M={windows:{en:[
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="7" height="9" x="3" y="3" rx="1"/><rect width="7" height="5" x="14" y="3" rx="1"/><rect width="7" height="9" x="14" y="12" rx="1"/><rect width="7" height="5" x="3" y="16" rx="1"/></svg>',b:'rgba(59,130,246,0.1)',t:'Dashboard',d:'Real-time system overview'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z"/><path d="M3.22 12H9.5l.5-1 2 4.5 2-7 1.5 3.5h5.27"/></svg>',b:'rgba(6,214,160,0.1)',t:'System Health',d:'Full diagnostic scan'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5a3 3 0 1 0-5.997.125 4 4 0 0 0-2.526 5.77 4 4 0 0 0 .556 6.588A4 4 0 1 0 12 18Z"/><path d="M12 5a3 3 0 1 1 5.997.125 4 4 0 0 1 2.526 5.77 4 4 0 0 1-.556 6.588A4 4 0 1 1 12 18Z"/><path d="M15 13a4.5 4.5 0 0 1-3-4 4.5 4.5 0 0 1-3 4"/></svg>',b:'rgba(139,92,246,0.1)',t:'AI Recommendations',d:'Smart optimization tips'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18"/><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"/><path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"/><line x1="10" x2="10" y1="11" y2="17"/><line x1="14" x2="14" y1="11" y2="17"/></svg>',b:'rgba(16,185,129,0.1)',t:'Junk Cleaner',d:'Temp files, caches, logs'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M6 19v-3"/><path d="M10 19v-3"/><path d="M14 19v-3"/><path d="M18 19v-3"/><path d="M8 11V9"/><path d="M16 11V9"/><path d="M12 11V9"/><path d="M2 15h20"/><path d="M2 7a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v1.1a2 2 0 0 0 0 3.837V15H2v-3.063a2 2 0 0 0 0-3.837Z"/></svg>',b:'rgba(139,92,246,0.1)',t:'RAM Optimizer',d:'Memory management + leak detection'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="20" height="5" x="2" y="3" rx="1"/><path d="M4 8v11a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8"/><path d="M10 12h4"/></svg>',b:'rgba(245,158,11,0.1)',t:'Storage Compression',d:'Transparent NTFS compression'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"/></svg>',b:'rgba(236,72,153,0.1)',t:'Registry Optimizer',d:'Scan & fix orphaned entries'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m2 2 20 20"/><path d="M5 5a1 1 0 0 0-1 1v7c0 5 3.5 7.5 8 8.5a14.6 14.6 0 0 0 4-1.8"/><path d="M9.8 3.2A1 1 0 0 1 11 3h1a1 1 0 0 1 1 1v7c0 1.1.2 2.1.5 3"/></svg>',b:'rgba(239,68,68,0.1)',t:'Bloatware Removal',d:'Community-scored safety ratings'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><path d="M12 2a14.5 14.5 0 0 0 0 20 14.5 14.5 0 0 0 0-20"/><path d="M2 12h20"/></svg>',b:'rgba(59,130,246,0.1)',t:'Network Optimizer',d:'DNS optimization + speed test'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="6" x2="10" y1="11" y2="11"/><line x1="8" x2="8" y1="9" y2="13"/><line x1="15" x2="15.01" y1="12" y2="12"/><line x1="18" x2="18.01" y1="10" y2="10"/><path d="M17.32 5H6.68a4 4 0 0 0-3.978 3.59c-.006.052-.01.101-.017.152C2.604 9.416 2 14.456 2 16a3 3 0 0 0 3 3c1 0 1.5-.5 2-1l1.414-1.414A2 2 0 0 1 9.828 16h4.344a2 2 0 0 1 1.414.586L17 18c.5.5 1 1 2 1a3 3 0 0 0 3-3c0-1.545-.604-6.584-.685-7.258-.007-.05-.011-.1-.017-.151A4 4 0 0 0 17.32 5z"/></svg>',b:'rgba(236,72,153,0.1)',t:'Gaming Mode',d:'Per-game profiles + auto-detect'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="8" height="4" x="8" y="2" rx="1" ry="1"/><path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"/><path d="M12 11h4"/><path d="M12 16h4"/><path d="M8 11h.01"/><path d="M8 16h.01"/></svg>',b:'rgba(245,158,11,0.1)',t:'Context Menu',d:'Clean up right-click menu'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="12" x2="12" y1="17" y2="22"/><path d="M5 17h14v-1.76a2 2 0 0 0-1.11-1.79l-1.78-.9A2 2 0 0 1 15 10.76V6h1a2 2 0 0 0 0-4H8a2 2 0 0 0 0 4h1v4.76a2 2 0 0 1-1.11 1.79l-1.78.9A2 2 0 0 0 5 15.24Z"/></svg>',b:'rgba(6,214,160,0.1)',t:'Taskbar Tweaks',d:'Win11 taskbar customization'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="18" cy="18" r="3"/><path d="M10.3 20H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h3.9a2 2 0 0 1 1.69.9l.81 1.2a2 2 0 0 0 1.67.9H20a2 2 0 0 1 2 2v3.3"/><path d="m21.7 19.4-.9-.3"/><path d="m15.2 16.9-.9-.3"/><path d="m16.6 21.7.3-.9"/><path d="m19.1 15.2.3-.9"/><path d="m19.6 21.7-.4-1"/><path d="m16.8 15.3-.4-1"/><path d="m14.3 19.6 1-.4"/><path d="m20.7 16.8 1-.4"/></svg>',b:'rgba(139,92,246,0.1)',t:'Explorer Tweaks',d:'File Explorer privacy & behavior'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" x2="12" y1="15" y2="3"/></svg>',b:'rgba(59,130,246,0.1)',t:'App Installer',d:'WinGet bundles + custom collections'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21.21 15.89A10 10 0 1 1 8 2.83"/><path d="M22 12A10 10 0 0 0 12 2v10z"/></svg>',b:'rgba(245,158,11,0.1)',t:'Space Analyzer',d:'Drill-down treemap + file types'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="22" x2="2" y1="12" y2="12"/><path d="M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z"/><line x1="6" x2="6.01" y1="16" y2="16"/><line x1="10" x2="10.01" y1="16" y2="16"/></svg>',b:'rgba(239,68,68,0.1)',t:'Disk Health',d:'SMART diagnostics + temperature'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09z"/><path d="m12 15-3-3a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.35 22.35 0 0 1-4 2z"/><path d="M9 12H4s.55-3.03 2-4c1.62-1.08 5 0 5 0"/><path d="M12 15v5s3.03-.55 4-2c1.08-1.62 0-5 0-5"/></svg>',b:'rgba(6,214,160,0.1)',t:'Startup Optimizer',d:'Boot benchmark + toggle apps'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>',b:'rgba(236,72,153,0.1)',t:'Auto-Scheduler',d:'Background task automation'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M10 8h.01"/><path d="M12 12h.01"/><path d="M14 8h.01"/><path d="M16 12h.01"/><path d="M18 8h.01"/><path d="M6 8h.01"/><path d="M7 16h10"/><path d="M8 12h.01"/><rect width="20" height="16" x="2" y="4" rx="2"/></svg>',b:'rgba(59,130,246,0.1)',t:'Keyboard Shortcuts',d:'Navigate faster with hotkeys'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z"/><path d="M14 2v4a2 2 0 0 0 2 2h4"/><path d="M10 9H8"/><path d="M16 13H8"/><path d="M16 17H8"/></svg>',b:'rgba(139,92,246,0.1)',t:'PDF Reports',d:'Export health reports'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m5 8 6 6"/><path d="m4 14 6-6 2-3"/><path d="M2 5h12"/><path d="M7 2h1"/><path d="m22 22-5-10-5 10"/><path d="M14 18h6"/></svg>',b:'rgba(6,214,160,0.1)',t:'Multi-Language',d:'English & Turkish'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z"/><circle cx="12" cy="12" r="3"/></svg>',b:'rgba(245,158,11,0.1)',t:'Admin Panel',d:'Web-based management'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m7 21-4.3-4.3c-1-1-1-2.5 0-3.4l9.6-9.6c1-1 2.5-1 3.4 0l5.6 5.6c1 1 1 2.5 0 3.4L13 21"/><path d="M22 21H7"/><path d="m5 11 9 9"/></svg>',b:'rgba(236,72,153,0.1)',t:'Disk Cleanup Pro',d:'Deep cache & system file cleanup'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z"/><path d="m9 12 2 2 4-4"/></svg>',b:'rgba(59,130,246,0.1)',t:'Defender Manager',d:'Windows Security control panel'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="18" height="11" x="3" y="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>',b:'rgba(124,31,162,0.1)',t:'Privacy Cleaner',d:'Browser & privacy traces'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="16" height="16" x="4" y="4" rx="2"/><rect width="6" height="6" x="9" y="9" rx="1"/><path d="M15 2v2"/><path d="M15 20v2"/><path d="M2 15h2"/><path d="M2 9h2"/><path d="M20 15h2"/><path d="M20 9h2"/><path d="M9 2v2"/><path d="M9 20v2"/></svg>',b:'rgba(245,158,11,0.1)',t:'Driver Updater',d:'Scan & backup drivers'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M15 7h1a2 2 0 0 1 2 2v6a2 2 0 0 1-2 2h-2"/><path d="M6 7H4a2 2 0 0 0-2 2v6a2 2 0 0 0 2 2h1"/><path d="m11 7-3 5h4l-3 5"/><line x1="22" x2="22" y1="11" y2="13"/></svg>',b:'rgba(46,125,50,0.1)',t:'Battery Optimizer',d:'Battery health & power plans'}
],tr:[
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="7" height="9" x="3" y="3" rx="1"/><rect width="7" height="5" x="14" y="3" rx="1"/><rect width="7" height="9" x="14" y="12" rx="1"/><rect width="7" height="5" x="3" y="16" rx="1"/></svg>',b:'rgba(59,130,246,0.1)',t:'Kontrol Paneli',d:'Ger\u00e7ek zamanl\u0131 sistem g\u00f6r\u00fcn\u00fcm\u00fc'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z"/><path d="M3.22 12H9.5l.5-1 2 4.5 2-7 1.5 3.5h5.27"/></svg>',b:'rgba(6,214,160,0.1)',t:'Sistem Sağlığı',d:'Kapsamlı tanılama taraması'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5a3 3 0 1 0-5.997.125 4 4 0 0 0-2.526 5.77 4 4 0 0 0 .556 6.588A4 4 0 1 0 12 18Z"/><path d="M12 5a3 3 0 1 1 5.997.125 4 4 0 0 1 2.526 5.77 4 4 0 0 1-.556 6.588A4 4 0 1 1 12 18Z"/><path d="M15 13a4.5 4.5 0 0 1-3-4 4.5 4.5 0 0 1-3 4"/></svg>',b:'rgba(139,92,246,0.1)',t:'AI Öneriler',d:'Akıllı optimizasyon ipuçları'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18"/><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"/><path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"/><line x1="10" x2="10" y1="11" y2="17"/><line x1="14" x2="14" y1="11" y2="17"/></svg>',b:'rgba(16,185,129,0.1)',t:'Çöp Temizleyici',d:'Geçici dosyalar, önbellekler'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M6 19v-3"/><path d="M10 19v-3"/><path d="M14 19v-3"/><path d="M18 19v-3"/><path d="M8 11V9"/><path d="M16 11V9"/><path d="M12 11V9"/><path d="M2 15h20"/><path d="M2 7a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v1.1a2 2 0 0 0 0 3.837V15H2v-3.063a2 2 0 0 0 0-3.837Z"/></svg>',b:'rgba(139,92,246,0.1)',t:'RAM Optimize',d:'Bellek yönetimi + sızıntı tespiti'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="20" height="5" x="2" y="3" rx="1"/><path d="M4 8v11a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8"/><path d="M10 12h4"/></svg>',b:'rgba(245,158,11,0.1)',t:'Depolama Sıkıştırma',d:'Şeffaf NTFS sıkıştırma'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"/></svg>',b:'rgba(236,72,153,0.1)',t:'Kayıt Defteri',d:'Yetim girdileri tara ve düzelt'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m2 2 20 20"/><path d="M5 5a1 1 0 0 0-1 1v7c0 5 3.5 7.5 8 8.5a14.6 14.6 0 0 0 4-1.8"/><path d="M9.8 3.2A1 1 0 0 1 11 3h1a1 1 0 0 1 1 1v7c0 1.1.2 2.1.5 3"/></svg>',b:'rgba(239,68,68,0.1)',t:'Bloatware Kaldırma',d:'Topluluk güvenlik puanları'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><path d="M12 2a14.5 14.5 0 0 0 0 20 14.5 14.5 0 0 0 0-20"/><path d="M2 12h20"/></svg>',b:'rgba(59,130,246,0.1)',t:'Ağ Optimize',d:'DNS optimizasyonu + hız testi'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="6" x2="10" y1="11" y2="11"/><line x1="8" x2="8" y1="9" y2="13"/><line x1="15" x2="15.01" y1="12" y2="12"/><line x1="18" x2="18.01" y1="10" y2="10"/><path d="M17.32 5H6.68a4 4 0 0 0-3.978 3.59c-.006.052-.01.101-.017.152C2.604 9.416 2 14.456 2 16a3 3 0 0 0 3 3c1 0 1.5-.5 2-1l1.414-1.414A2 2 0 0 1 9.828 16h4.344a2 2 0 0 1 1.414.586L17 18c.5.5 1 1 2 1a3 3 0 0 0 3-3c0-1.545-.604-6.584-.685-7.258-.007-.05-.011-.1-.017-.151A4 4 0 0 0 17.32 5z"/></svg>',b:'rgba(236,72,153,0.1)',t:'Oyun Modu',d:'Oyun profilleri + otomatik algılama'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="8" height="4" x="8" y="2" rx="1" ry="1"/><path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"/><path d="M12 11h4"/><path d="M12 16h4"/><path d="M8 11h.01"/><path d="M8 16h.01"/></svg>',b:'rgba(245,158,11,0.1)',t:'Sağ Tık Menüsü',d:'Bağlam menüsünü temizle'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="12" x2="12" y1="17" y2="22"/><path d="M5 17h14v-1.76a2 2 0 0 0-1.11-1.79l-1.78-.9A2 2 0 0 1 15 10.76V6h1a2 2 0 0 0 0-4H8a2 2 0 0 0 0 4h1v4.76a2 2 0 0 1-1.11 1.79l-1.78.9A2 2 0 0 0 5 15.24Z"/></svg>',b:'rgba(6,214,160,0.1)',t:'Görev Çubuğu',d:'Win11 görev çubuğu özelleştirme'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="18" cy="18" r="3"/><path d="M10.3 20H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h3.9a2 2 0 0 1 1.69.9l.81 1.2a2 2 0 0 0 1.67.9H20a2 2 0 0 1 2 2v3.3"/><path d="m21.7 19.4-.9-.3"/><path d="m15.2 16.9-.9-.3"/><path d="m16.6 21.7.3-.9"/><path d="m19.1 15.2.3-.9"/><path d="m19.6 21.7-.4-1"/><path d="m16.8 15.3-.4-1"/><path d="m14.3 19.6 1-.4"/><path d="m20.7 16.8 1-.4"/></svg>',b:'rgba(139,92,246,0.1)',t:'Dosya Gezgini',d:'Gizlilik ve davranış ayarları'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" x2="12" y1="15" y2="3"/></svg>',b:'rgba(59,130,246,0.1)',t:'Uygulama Yükleyici',d:'WinGet paketleri + koleksiyonlar'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21.21 15.89A10 10 0 1 1 8 2.83"/><path d="M22 12A10 10 0 0 0 12 2v10z"/></svg>',b:'rgba(245,158,11,0.1)',t:'Alan Analizi',d:'Detaylı ağaç haritası + dosya türleri'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="22" x2="2" y1="12" y2="12"/><path d="M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z"/><line x1="6" x2="6.01" y1="16" y2="16"/><line x1="10" x2="10.01" y1="16" y2="16"/></svg>',b:'rgba(239,68,68,0.1)',t:'Disk Sağlığı',d:'SMART tanılama + sıcaklık'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09z"/><path d="m12 15-3-3a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.35 22.35 0 0 1-4 2z"/><path d="M9 12H4s.55-3.03 2-4c1.62-1.08 5 0 5 0"/><path d="M12 15v5s3.03-.55 4-2c1.08-1.62 0-5 0-5"/></svg>',b:'rgba(6,214,160,0.1)',t:'Başlangıç Yöneticisi',d:'Önyükleme benchmark + aç/kapat'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>',b:'rgba(236,72,153,0.1)',t:'Otomatik Zamanlama',d:'Arka plan görev otomasyonu'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M10 8h.01"/><path d="M12 12h.01"/><path d="M14 8h.01"/><path d="M16 12h.01"/><path d="M18 8h.01"/><path d="M6 8h.01"/><path d="M7 16h10"/><path d="M8 12h.01"/><rect width="20" height="16" x="2" y="4" rx="2"/></svg>',b:'rgba(59,130,246,0.1)',t:'Klavye Kısayolları',d:'Kısayollarla daha hızlı gezinin'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z"/><path d="M14 2v4a2 2 0 0 0 2 2h4"/><path d="M10 9H8"/><path d="M16 13H8"/><path d="M16 17H8"/></svg>',b:'rgba(139,92,246,0.1)',t:'PDF Raporlar',d:'Sağlık raporu dışa aktarma'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m5 8 6 6"/><path d="m4 14 6-6 2-3"/><path d="M2 5h12"/><path d="M7 2h1"/><path d="m22 22-5-10-5 10"/><path d="M14 18h6"/></svg>',b:'rgba(6,214,160,0.1)',t:'Çoklu Dil',d:'İngilizce ve Türkçe'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z"/><circle cx="12" cy="12" r="3"/></svg>',b:'rgba(245,158,11,0.1)',t:'Yönetici Paneli',d:'Web tabanlı yönetim'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m7 21-4.3-4.3c-1-1-1-2.5 0-3.4l9.6-9.6c1-1 2.5-1 3.4 0l5.6 5.6c1 1 1 2.5 0 3.4L13 21"/><path d="M22 21H7"/><path d="m5 11 9 9"/></svg>',b:'rgba(236,72,153,0.1)',t:'Disk Temizleme Pro',d:'Derin önbellek ve sistem dosyası temizliği'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z"/><path d="m9 12 2 2 4-4"/></svg>',b:'rgba(59,130,246,0.1)',t:'Defender Yöneticisi',d:'Windows Güvenlik kontrol paneli'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="18" height="11" x="3" y="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>',b:'rgba(124,31,162,0.1)',t:'Gizlilik Temizleyici',d:'Tarayıcı & gizlilik izleri'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="16" height="16" x="4" y="4" rx="2"/><rect width="6" height="6" x="9" y="9" rx="1"/><path d="M15 2v2"/><path d="M15 20v2"/><path d="M2 15h2"/><path d="M2 9h2"/><path d="M20 15h2"/><path d="M20 9h2"/><path d="M9 2v2"/><path d="M9 20v2"/></svg>',b:'rgba(245,158,11,0.1)',t:'Sürücü Güncelleyici',d:'Sürücüleri tara & yedekle'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M15 7h1a2 2 0 0 1 2 2v6a2 2 0 0 1-2 2h-2"/><path d="M6 7H4a2 2 0 0 0-2 2v6a2 2 0 0 0 2 2h1"/><path d="m11 7-3 5h4l-3 5"/><line x1="22" x2="22" y1="11" y2="13"/></svg>',b:'rgba(46,125,50,0.1)',t:'Batarya Optimize',d:'Batarya sa\u011fl\u0131\u011f\u0131 & g\u00fc\u00e7 planlar\u0131'}
]},
linux:{en:[
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="4 17 10 11 4 5"/><line x1="12" x2="20" y1="19" y2="19"/></svg>',b:'rgba(245,158,11,0.1)',t:'Systemd Manager',d:'Manage systemd services'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m7.5 4.27 9 5.15"/><path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z"/><path d="m3.3 7 8.7 5 8.7-5"/><path d="M12 22V12"/></svg>',b:'rgba(6,214,160,0.1)',t:'Package Cleaner',d:'APT/DNF/Pacman cache cleanup'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z"/><path d="M14 2v4a2 2 0 0 0 2 2h4"/><path d="M10 9H8"/><path d="M16 13H8"/><path d="M16 17H8"/></svg>',b:'rgba(139,92,246,0.1)',t:'Journal Cleaner',d:'Journalctl log cleanup'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="16" height="16" x="4" y="4" rx="2"/><rect width="6" height="6" x="9" y="9" rx="1"/><path d="M15 2v2"/><path d="M15 20v2"/><path d="M2 15h2"/><path d="M2 9h2"/><path d="M20 15h2"/><path d="M20 9h2"/><path d="M9 2v2"/><path d="M9 20v2"/></svg>',b:'rgba(236,72,153,0.1)',t:'Kernel Cleaner',d:'Remove old kernels safely'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18"/><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"/><path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"/><line x1="10" x2="10" y1="11" y2="17"/><line x1="14" x2="14" y1="11" y2="17"/></svg>',b:'rgba(59,130,246,0.1)',t:'Docker Cleaner',d:'Prune containers, images, volumes'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>',b:'rgba(6,214,160,0.1)',t:'Cron Manager',d:'Audit & clean cron jobs'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M6 19v-3"/><path d="M10 19v-3"/><path d="M14 19v-3"/><path d="M18 19v-3"/><path d="M8 11V9"/><path d="M16 11V9"/><path d="M12 11V9"/><path d="M2 15h20"/><path d="M2 7a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v1.1a2 2 0 0 0 0 3.837V15H2v-3.063a2 2 0 0 0 0-3.837Z"/></svg>',b:'rgba(245,158,11,0.1)',t:'Swap Optimizer',d:'Swappiness & zram tuning'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"/></svg>',b:'rgba(139,92,246,0.1)',t:'Symlink Manager',d:'Find broken symlinks'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" x2="12" y1="15" y2="3"/></svg>',b:'rgba(59,130,246,0.1)',t:'Linux App Installer',d:'141 apps in 10 bundles'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="20" height="5" x="2" y="3" rx="1"/><path d="M4 8v11a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8"/><path d="M10 12h4"/></svg>',b:'rgba(236,72,153,0.1)',t:'Snap/Flatpak Cleaner',d:'Clean unused snap revisions'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z"/><circle cx="12" cy="12" r="3"/></svg>',b:'rgba(239,68,68,0.1)',t:'GRUB Manager',d:'Bootloader configuration (Advanced)'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="7" height="9" x="3" y="3" rx="1"/><rect width="7" height="5" x="14" y="3" rx="1"/><rect width="7" height="9" x="14" y="12" rx="1"/><rect width="7" height="5" x="3" y="16" rx="1"/></svg>',b:'rgba(59,130,246,0.1)',t:'Dashboard',d:'Real-time system overview'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z"/><path d="M3.22 12H9.5l.5-1 2 4.5 2-7 1.5 3.5h5.27"/></svg>',b:'rgba(6,214,160,0.1)',t:'System Health',d:'Full diagnostic scan'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18"/><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"/><path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"/></svg>',b:'rgba(16,185,129,0.1)',t:'Junk Cleaner',d:'Temp files, caches, logs'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M6 19v-3"/><path d="M10 19v-3"/><path d="M14 19v-3"/><path d="M18 19v-3"/><path d="M8 11V9"/><path d="M16 11V9"/><path d="M12 11V9"/><path d="M2 15h20"/></svg>',b:'rgba(139,92,246,0.1)',t:'RAM Optimizer',d:'Memory management + leak detection'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z"/><circle cx="12" cy="12" r="3"/></svg>',b:'rgba(245,158,11,0.1)',t:'Process Monitor',d:'CPU, memory, disk I/O tracking'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m2 2 20 20"/><path d="M5 5a1 1 0 0 0-1 1v7c0 5 3.5 7.5 8 8.5a14.6 14.6 0 0 0 4-1.8"/><path d="M9.8 3.2A1 1 0 0 1 11 3h1a1 1 0 0 1 1 1v7c0 1.1.2 2.1.5 3"/></svg>',b:'rgba(239,68,68,0.1)',t:'File Shredder',d:'Secure file deletion'}
],tr:[
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="4 17 10 11 4 5"/><line x1="12" x2="20" y1="19" y2="19"/></svg>',b:'rgba(245,158,11,0.1)',t:'Systemd Y\u00f6neticisi',d:'Systemd servislerini y\u00f6netin'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m7.5 4.27 9 5.15"/><path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z"/><path d="m3.3 7 8.7 5 8.7-5"/><path d="M12 22V12"/></svg>',b:'rgba(6,214,160,0.1)',t:'Paket Temizleyici',d:'APT/DNF/Pacman \u00f6nbellek temizli\u011fi'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z"/><path d="M14 2v4a2 2 0 0 0 2 2h4"/><path d="M10 9H8"/><path d="M16 13H8"/><path d="M16 17H8"/></svg>',b:'rgba(139,92,246,0.1)',t:'Journal Temizleyici',d:'Journalctl log temizli\u011fi'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="16" height="16" x="4" y="4" rx="2"/><rect width="6" height="6" x="9" y="9" rx="1"/><path d="M15 2v2"/><path d="M15 20v2"/><path d="M2 15h2"/><path d="M2 9h2"/><path d="M20 15h2"/><path d="M20 9h2"/><path d="M9 2v2"/><path d="M9 20v2"/></svg>',b:'rgba(236,72,153,0.1)',t:'Kernel Temizleyici',d:'Eski kernelleri g\u00fcvenle kald\u0131r\u0131n'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18"/><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"/><path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"/><line x1="10" x2="10" y1="11" y2="17"/><line x1="14" x2="14" y1="11" y2="17"/></svg>',b:'rgba(59,130,246,0.1)',t:'Docker Temizleyici',d:'Container, image, volume temizle'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>',b:'rgba(6,214,160,0.1)',t:'Cron Y\u00f6neticisi',d:'Cron g\u00f6revlerini denetle ve temizle'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M6 19v-3"/><path d="M10 19v-3"/><path d="M14 19v-3"/><path d="M18 19v-3"/><path d="M8 11V9"/><path d="M16 11V9"/><path d="M12 11V9"/><path d="M2 15h20"/><path d="M2 7a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v1.1a2 2 0 0 0 0 3.837V15H2v-3.063a2 2 0 0 0 0-3.837Z"/></svg>',b:'rgba(245,158,11,0.1)',t:'Swap Optimize',d:'Swappiness & zram ayar\u0131'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"/></svg>',b:'rgba(139,92,246,0.1)',t:'Symlink Y\u00f6neticisi',d:'K\u0131r\u0131k sembolik ba\u011flant\u0131lar\u0131 bul'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" x2="12" y1="15" y2="3"/></svg>',b:'rgba(59,130,246,0.1)',t:'Linux Uygulama Y\u00fckleyici',d:'10 pakette 141 uygulama'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="20" height="5" x="2" y="3" rx="1"/><path d="M4 8v11a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8"/><path d="M10 12h4"/></svg>',b:'rgba(236,72,153,0.1)',t:'Snap/Flatpak Temizleyici',d:'Kullan\u0131lmayan snap s\u00fcr\u00fcmlerini temizle'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z"/><circle cx="12" cy="12" r="3"/></svg>',b:'rgba(239,68,68,0.1)',t:'GRUB Y\u00f6neticisi',d:'\u00d6ny\u00fckleyici yap\u0131land\u0131rmas\u0131 (Geli\u015fmi\u015f)'}
,
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect width="7" height="9" x="3" y="3" rx="1"/><rect width="7" height="5" x="14" y="3" rx="1"/><rect width="7" height="9" x="14" y="12" rx="1"/><rect width="7" height="5" x="3" y="16" rx="1"/></svg>',b:'rgba(59,130,246,0.1)',t:'Kontrol Paneli',d:'Gerçek zamanlı sistem'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z"/></svg>',b:'rgba(6,214,160,0.1)',t:'Sistem Sağlığı',d:'Tanılama taraması'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M3 6h18"/><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"/></svg>',b:'rgba(16,185,129,0.1)',t:'Çöp Temizleyici',d:'Geçici dosyalar'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M6 19v-3"/><path d="M10 19v-3"/><path d="M14 19v-3"/><path d="M18 19v-3"/><path d="M2 15h20"/></svg>',b:'rgba(139,92,246,0.1)',t:'RAM Optimizasyonu',d:'Bellek yönetimi'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z"/><circle cx="12" cy="12" r="3"/></svg>',b:'rgba(245,158,11,0.1)',t:'İşlem İzleyici',d:'CPU, bellek takibi'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="m2 2 20 20"/></svg>',b:'rgba(239,68,68,0.1)',t:'Dosya İmhacı',d:'Güvenli silme'}]},
macos:{en:[
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m7.5 4.27 9 5.15"/><path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z"/><path d="m3.3 7 8.7 5 8.7-5"/><path d="M12 22V12"/></svg>',b:'rgba(6,214,160,0.1)',t:'Brew Manager',d:'Homebrew cleanup & upgrade'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"/></svg>',b:'rgba(139,92,246,0.1)',t:'Xcode Cleaner',d:'DerivedData & simulator cleanup'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><path d="M12 2a14.5 14.5 0 0 0 0 20 14.5 14.5 0 0 0 0-20"/><path d="M2 12h20"/></svg>',b:'rgba(59,130,246,0.1)',t:'DNS Flusher',d:'Flush macOS DNS cache'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="22" x2="2" y1="12" y2="12"/><path d="M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z"/><line x1="6" x2="6.01" y1="16" y2="16"/><line x1="10" x2="10.01" y1="16" y2="16"/></svg>',b:'rgba(236,72,153,0.1)',t:'Purgeable Space',d:'Reclaim purgeable disk space'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09z"/><path d="m12 15-3-3a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.35 22.35 0 0 1-4 2z"/><path d="M9 12H4s.55-3.03 2-4c1.62-1.08 5 0 5 0"/><path d="M12 15v5s3.03-.55 4-2c1.08-1.62 0-5 0-5"/></svg>',b:'rgba(245,158,11,0.1)',t:'Launch Agent Manager',d:'Manage launch agents & daemons'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z"/><circle cx="12" cy="12" r="3"/></svg>',b:'rgba(6,214,160,0.1)',t:'Defaults Optimizer',d:'15 macOS system tweaks'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>',b:'rgba(139,92,246,0.1)',t:'Time Machine Manager',d:'Manage backups & snapshots'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></svg>',b:'rgba(59,130,246,0.1)',t:'Spotlight Manager',d:'Rebuild/manage Spotlight index'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" x2="12" y1="15" y2="3"/></svg>',b:'rgba(236,72,153,0.1)',t:'Mac App Installer',d:'141 brew/cask apps'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18"/><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"/><path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"/><line x1="10" x2="10" y1="11" y2="17"/><line x1="14" x2="14" y1="11" y2="17"/></svg>',b:'rgba(59,130,246,0.1)',t:'Docker Cleaner',d:'Prune containers, images, volumes'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="7" height="9" x="3" y="3" rx="1"/><rect width="7" height="5" x="14" y="3" rx="1"/><rect width="7" height="9" x="14" y="12" rx="1"/><rect width="7" height="5" x="3" y="16" rx="1"/></svg>',b:'rgba(59,130,246,0.1)',t:'Dashboard',d:'Real-time system overview'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z"/><path d="M3.22 12H9.5l.5-1 2 4.5 2-7 1.5 3.5h5.27"/></svg>',b:'rgba(6,214,160,0.1)',t:'System Health',d:'Full diagnostic scan'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18"/><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"/><path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"/></svg>',b:'rgba(16,185,129,0.1)',t:'Junk Cleaner',d:'Temp files, caches, logs'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M6 19v-3"/><path d="M10 19v-3"/><path d="M14 19v-3"/><path d="M18 19v-3"/><path d="M8 11V9"/><path d="M16 11V9"/><path d="M12 11V9"/><path d="M2 15h20"/></svg>',b:'rgba(139,92,246,0.1)',t:'RAM Optimizer',d:'Memory management + leak detection'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z"/><circle cx="12" cy="12" r="3"/></svg>',b:'rgba(245,158,11,0.1)',t:'Process Monitor',d:'CPU, memory, disk I/O tracking'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m2 2 20 20"/><path d="M5 5a1 1 0 0 0-1 1v7c0 5 3.5 7.5 8 8.5a14.6 14.6 0 0 0 4-1.8"/><path d="M9.8 3.2A1 1 0 0 1 11 3h1a1 1 0 0 1 1 1v7c0 1.1.2 2.1.5 3"/></svg>',b:'rgba(239,68,68,0.1)',t:'File Shredder',d:'Secure file deletion'}
],tr:[
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m7.5 4.27 9 5.15"/><path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z"/><path d="m3.3 7 8.7 5 8.7-5"/><path d="M12 22V12"/></svg>',b:'rgba(6,214,160,0.1)',t:'Brew Y\u00f6neticisi',d:'Homebrew temizlik & g\u00fcncelleme'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"/></svg>',b:'rgba(139,92,246,0.1)',t:'Xcode Temizleyici',d:'DerivedData & sim\u00fclat\u00f6r temizli\u011fi'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><path d="M12 2a14.5 14.5 0 0 0 0 20 14.5 14.5 0 0 0 0-20"/><path d="M2 12h20"/></svg>',b:'rgba(59,130,246,0.1)',t:'DNS Temizleyici',d:'macOS DNS \u00f6nbelle\u011fini temizle'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="22" x2="2" y1="12" y2="12"/><path d="M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z"/><line x1="6" x2="6.01" y1="16" y2="16"/><line x1="10" x2="10.01" y1="16" y2="16"/></svg>',b:'rgba(236,72,153,0.1)',t:'Temizlenebilir Alan',d:'Temizlenebilir disk alan\u0131n\u0131 geri kazan'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09z"/><path d="m12 15-3-3a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.35 22.35 0 0 1-4 2z"/><path d="M9 12H4s.55-3.03 2-4c1.62-1.08 5 0 5 0"/><path d="M12 15v5s3.03-.55 4-2c1.08-1.62 0-5 0-5"/></svg>',b:'rgba(245,158,11,0.1)',t:'Launch Agent Y\u00f6neticisi',d:'Launch agent & daemon y\u00f6netimi'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z"/><circle cx="12" cy="12" r="3"/></svg>',b:'rgba(6,214,160,0.1)',t:'Defaults Optimize',d:'15 macOS sistem ayar\u0131'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>',b:'rgba(139,92,246,0.1)',t:'Time Machine Y\u00f6neticisi',d:'Yedekleri & anl\u0131k g\u00f6r\u00fcnt\u00fcleri y\u00f6net'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></svg>',b:'rgba(59,130,246,0.1)',t:'Spotlight Y\u00f6neticisi',d:'Spotlight dizinini yeniden olu\u015ftur/y\u00f6net'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" x2="12" y1="15" y2="3"/></svg>',b:'rgba(236,72,153,0.1)',t:'Mac Uygulama Y\u00fckleyici',d:'141 brew/cask uygulama'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18"/><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"/><path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"/><line x1="10" x2="10" y1="11" y2="17"/><line x1="14" x2="14" y1="11" y2="17"/></svg>',b:'rgba(59,130,246,0.1)',t:'Docker Temizleyici',d:'Container, image, volume temizle'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect width="7" height="9" x="3" y="3" rx="1"/><rect width="7" height="5" x="14" y="3" rx="1"/><rect width="7" height="9" x="14" y="12" rx="1"/><rect width="7" height="5" x="3" y="16" rx="1"/></svg>',b:'rgba(59,130,246,0.1)',t:'Kontrol Paneli',d:'Gerçek zamanlı sistem görünümü'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z"/></svg>',b:'rgba(6,214,160,0.1)',t:'Sistem Sağlığı',d:'Tam tanılama taraması'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M3 6h18"/><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"/></svg>',b:'rgba(16,185,129,0.1)',t:'Çöp Temizleyici',d:'Geçici dosyalar, önbellekler'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M6 19v-3"/><path d="M10 19v-3"/><path d="M14 19v-3"/><path d="M18 19v-3"/><path d="M2 15h20"/></svg>',b:'rgba(139,92,246,0.1)',t:'RAM Optimizasyonu',d:'Bellek yönetimi + sızıntı tespiti'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z"/><circle cx="12" cy="12" r="3"/></svg>',b:'rgba(245,158,11,0.1)',t:'İşlem İzleyici',d:'CPU, bellek, disk I/O takibi'},
{i:'<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="m2 2 20 20"/><path d="M9.8 3.2A1 1 0 0 1 11 3h1a1 1 0 0 1 1 1v7c0 1.1.2 2.1.5 3"/></svg>',b:'rgba(239,68,68,0.1)',t:'Dosya İmhacı',d:'Güvenli dosya silme'}
]}};

// ═══════════════════════════════════════════════════════════
// TRANSLATIONS
// ═══════════════════════════════════════════════════════════
const L={en:{
'nav.features':'Features','nav.iso':'ISO Builder','nav.modules':'Modules','nav.pricing':'Pricing','nav.support':'Support','nav.signin':'Sign In','nav.signup':'Create Account',
'hero.badge':'Now Available for Windows, Linux & macOS','hero.title':'Your System,<br><span class="gradient">Supercharged.</span>',
'hero.desc':'The most advanced cross-platform optimization suite. 45+ intelligent modules and 7 AI models that clean, optimize, and protect your system across Windows, Linux, and macOS.',
'hero.platforms':'Platforms',
'hero.download':'Download Free','hero.createAccount':'Create Account','hero.seeFeatures':'See Features','hero.modules':'Modules','hero.languages':'Languages','hero.freeTier':'Free Tier','hero.bloatware':'Bloatware','hero.avnote':'Windows SmartScreen may show a warning - click More info then Run anyway. This is normal for new software.',
'features.label':'Core Features','features.title':'Everything your PC needs.<br>Nothing it doesn\'t.',
'features.desc':'Every module is built for real impact — no placebo optimizations. Just real improvements you can measure.',
'f1.t':'Real-Time Dashboard','f1.d':'Live CPU, RAM, Disk monitoring with colored stat cards. AI-powered health scoring updates every 3 seconds.',
'f2.t':'Intelligent Junk Cleaner','f2.d':'Risk-rated categories. Finds temp files, caches, logs — shows exactly what it\'ll delete before touching anything.',
'f3.t':'RAM Optimizer','f3.d':'Per-process memory tracking with leak detection. Sparkline trends show which apps are memory hogs.',
'f4.t':'Gaming Mode','f4.d':'One-click optimization with per-game profiles. Auto-detects game launch, suspends background processes.',
'f5.t':'Startup Optimizer','f5.d':'Scan and toggle startup programs with impact ratings. Boot time benchmark with before/after comparison.',
'f6.t':'Bloatware Removal','f6.d':'Community-scored safety ratings for 50+ pre-installed apps. Search, filter, bulk remove with one click.',
'f7.t':'Disk Cleanup Pro','f7.d':'Deep system cleanup that finds hidden caches, shader files, crash dumps, and update leftovers that Windows tools miss.',
'f8.t':'Defender Manager','f8.d':'Full control over Windows Security - toggle protection features, run scans, view threat history, manage exclusions.',
'f9.t':'Privacy Cleaner','f9.d':'Remove browser data, recent files, jump lists, thumbnails, clipboard history, DNS cache across Chrome, Edge, and Firefox.',
'f10.t':'Driver Updater','f10.d':'Scan all system drivers, detect outdated or problematic ones, backup third-party drivers, and check Windows Update.',
'f11.t':'Battery Optimizer','f11.d':'Monitor battery health, switch power plans, analyze per-app drain, and extend laptop battery life.',
'f12.t':'System Health','f12.d':'Full diagnostic scan with health scoring. Monitors CPU, RAM, disk, GPU, battery, and startup programs across all platforms.',
'f13.t':'AI Assistant','f13.d':'Built-in AI chat powered by 7 fine-tuned models. Answers questions, diagnoses issues, and recommends optimizations — 100% local.',
'f14.t':'Process Monitor','f14.d':'Detailed process visibility with CPU, memory, disk I/O tracking. Identifies resource hogs and suggests safe actions.',
'modules.label':'All Modules','modules.title':'47 modules. <span class="gradient">Zero bloat.</span>',
'modules.desc':'Every tool a power user needs, organized into one cohesive app.',
'pricing.label':'Pricing','pricing.title':'Simple pricing.<br>Powerful at every tier.','pricing.desc':'Start free, upgrade when you need more.',
'cta.title':'Ready to <span class="gradient">supercharge</span> your system?','cta.desc':'Join thousands of users who already optimized their computing experience.','cta.btn':'Download AuraCore Pro','cta.signup':'Create Free Account',
'footer.privacy':'Privacy','footer.terms':'Terms','footer.rights':'All rights reserved.',
'p.badge':'POPULAR',
'auth.login.title':'Welcome Back','auth.login.subtitle':'Sign in to your AuraCore Pro account','auth.login.btn':'Sign In','auth.login.switch':'Don\'t have an account?','auth.login.switchLink':'Create one',
'auth.register.title':'Create Account','auth.register.subtitle':'Sign up to unlock your full potential','auth.register.btn':'Create Account','auth.register.switch':'Already have an account?','auth.register.switchLink':'Sign in',
'auth.email':'Email address','auth.password':'Password','auth.confirmPassword':'Confirm password',
'iso.label':'New Feature','iso.title':'Custom ISO Builder.<br><span class="gradient">Your Windows, your rules.</span>','iso.desc':'Build a fully customized Windows installation ISO with a 12-step wizard. Remove bloatware, pre-install apps, configure settings - all before Windows even boots.','iso.cta':'Try ISO Builder',
'cp.label':'Cross-Platform','cp.title':'One app. Three platforms.<br><span class="gradient">Zero compromises.</span>','cp.desc':'AuraCore Pro runs natively on Windows, Linux, and macOS with platform-specific optimizations tailored to each OS.',
'cp.win.t':'Windows','cp.win.d':'27 modules: Registry Optimizer, Gaming Mode, Bloatware Removal, Disk Cleanup, Storage Compression, Defender Manager, and more.',
'cp.linux.t':'Linux','cp.linux.d':'17 modules: Systemd Manager, Package Cleaner, Kernel Cleaner, Docker Cleaner, GRUB Manager, App Installer (141 apps), and more.',
'cp.mac.t':'macOS','cp.mac.d':'16 modules: Brew Manager, Xcode Cleaner, DNS Flusher, Spotlight Manager, Time Machine Manager, App Installer (141 apps), and more.',
'ai.label':'AI Powered','ai.title':'7 fine-tuned AI models.<br><span class="gradient">Your personal optimization expert.</span>','ai.desc':'Built-in AI assistant trained on 2,844 AuraCore-specific examples. Answers questions, diagnoses issues, and recommends optimizations \u2014 all running locally on your machine.',
'ai.chat.t':'AI Chat','ai.chat.d':'Ask anything about system optimization. The AI reads your system metrics in real-time and gives context-aware advice.',
'ai.rag.t':'RAG Retrieval','ai.rag.d':'AI retrieves relevant source code context from a vector database before answering \u2014 zero hallucination, maximum accuracy.',
'ai.models.t':'7 Model Options','ai.models.d':'From TinyLlama 1.1B (fast) to Qwen 32B (powerful). Choose the best model for your hardware \u2014 all run 100% locally, no cloud needed.',
},tr:{
'nav.features':'Özellikler','nav.iso':'ISO Oluşturucu','nav.modules':'Modüller','nav.pricing':'Fiyatlandırma','nav.support':'Destek','nav.signin':'Giriş Yap','nav.signup':'Hesap Oluştur',
'hero.badge':'Windows, Linux ve macOS i\u00e7in Haz\u0131r','hero.title':'Sisteminiz,<br><span class="gradient">S\u00fcper G\u00fc\u00e7l\u00fc.</span>',
'hero.desc':'En geli\u015fmi\u015f \u00e7apraz platform optimizasyon paketi. 45\'ten fazla ak\u0131ll\u0131 mod\u00fcl ve 7 AI model ile Windows, Linux ve macOS sistemlerinizi temizleyin, optimize edin ve koruyun.',
'hero.platforms':'Platform',
'hero.download':'Ücretsiz İndir','hero.createAccount':'Hesap Oluştur','hero.seeFeatures':'Özellikleri Gör','hero.modules':'Modül','hero.languages':'Dil','hero.freeTier':'Ücretsiz','hero.bloatware':'Bloatware','hero.avnote':'Windows SmartScreen uyarısı gösterebilir — Daha fazla bilgi ve ardından Yine de çalıştır seçeneklerine tıklayın. Bu yeni yazılımlar için normaldir.',
'features.label':'Temel Özellikler','features.title':'Bilgisayarınızın ihtiyacı olan her şey.<br>İhtiyacı olmayan hiçbir şey.',
'features.desc':'Her modül gerçek bir fark yaratmak için tasarlandı — sahte optimizasyon yok, sadece ölçülebilir gerçek iyileştirmeler.',
'f1.t':'Gerçek Zamanlı Panel','f1.d':'Renkli kartlarla canlı CPU, RAM, Disk izleme. AI destekli sağlık puanlama her 3 saniyede güncellenir.',
'f2.t':'Akıllı Çöp Temizleyici','f2.d':'Güvenli\'den Riskli\'ye derecelendirme. Geçici dosyaları, önbellekleri bulur — silmeden önce neyin silineceğini gösterir.',
'f3.t':'RAM Optimizasyonu','f3.d':'İşlem bazında bellek takibi ve sızıntı tespiti. Hangi uygulamaların belleği tükettiğini trend grafikleriyle görün.',
'f4.t':'Oyun Modu','f4.d':'Oyuna özel profillerle tek tıkla optimizasyon. Oyun açıldığında otomatik devreye girer, arka plan işlemlerini durdurur.',
'f5.t':'Başlangıç Yöneticisi','f5.d':'Başlangıçta açılan programları etki derecesiyle tara. Öncesi ve sonrası karşılaştırmalı açılış hızı analizi.',
'f6.t':'Gereksiz Uygulama Temizliği','f6.d':'50\'den fazla ön yüklü uygulama için topluluk güvenlik puanları. Arama, filtreleme ve tek tıkla toplu kaldırma.',
'f7.t':'Disk Temizleme Pro','f7.d':'Windows araçlarının bulamadığı gizli önbellekleri, shader dosyalarını, çökme dökümlerini ve güncelleme artıklarını bulan derin sistem temizliği.',
'f8.t':'Defender Yöneticisi','f8.d':'Windows Güvenliği üzerinde tam kontrol - koruma özelliklerini yönetin, tarama başlatın, tehdit geçmişini görüntüleyin.',
'f9.t':'Gizlilik Temizleyici','f9.d':'Chrome, Edge ve Firefox genelinde tarayıcı verileri, son dosyalar, küçük resimler, pano geçmişi ve DNS önbelleğini temizleyin.',
'f10.t':'Sürücü Güncelleyici','f10.d':'Tüm sistem sürücülerini tarayın, güncel olmayanları tespit edin, üçüncü parti sürücüleri yedekleyin.',
'f11.t':'Pil Optimizasyonu','f11.d':'Pil sağlığını izleyin, güç planlarını değiştirin, uygulama bazlı tüketimi analiz edin.',
'f12.t':'Sistem Sağlığı','f12.d':'Sağlık puanlamasıyla tam tanılama taraması. Tüm platformlarda CPU, RAM, disk, GPU, pil ve başlangıç programlarını izler.',
'f13.t':'AI Asistan','f13.d':'7 fine-tuned model ile yerleşik AI sohbet. Soruları yanıtlar, sorunları teşhis eder, optimizasyon önerileri sunar — %100 yerel.',
'f14.t':'İşlem İzleyici','f14.d':'CPU, bellek, disk I/O takibiyle detaylı işlem görünürlüğü. Kaynak tüketen işlemleri tespit eder.',
'modules.label':'T\u00fcm Mod\u00fcller','modules.title':'47 mod\u00fcl. <span class="gradient">S\u0131f\u0131r gereksizlik.</span>',
'modules.desc':'Her seviye kullan\u0131c\u0131n\u0131n ihtiya\u00e7 duydu\u011fu t\u00fcm ara\u00e7lar, tek bir uygulamada.',
'pricing.label':'Fiyatlandırma','pricing.title':'Basit fiyatlandırma.<br>Her seviyede güçlü.','pricing.desc':'Ücretsiz başlayın, ihtiyacınız olduğunda yükseltin.',
'cta.title':'Sisteminizin ger\u00e7ek potansiyelini <span class="gradient">ke\u015ffetmeye</span> haz\u0131r m\u0131s\u0131n\u0131z?','cta.desc':'Bilgisayar deneyimlerini zaten optimize etmi\u015f binlerce kullan\u0131c\u0131ya kat\u0131l\u0131n.','cta.btn':'AuraCore Pro İndir','cta.signup':'Ücretsiz Hesap Oluştur',
'footer.privacy':'Gizlilik','footer.terms':'Şartlar','footer.rights':'Tüm hakları saklıdır.',
'p.badge':'POPÜLER',
'auth.login.title':'Tekrar Hoş Geldiniz','auth.login.subtitle':'AuraCore Pro hesabınıza giriş yapın','auth.login.btn':'Giriş Yap','auth.login.switch':'Hesabınız yok mu?','auth.login.switchLink':'Oluşturun',
'auth.register.title':'Hesap Oluştur','auth.register.subtitle':'Tam potansiyelinizi açığa çıkarın','auth.register.btn':'Hesap Oluştur','auth.register.switch':'Zaten hesabınız var mı?','auth.register.switchLink':'Giriş yapın',
'auth.email':'E-posta adresi','auth.password':'Şifre','auth.confirmPassword':'Şifreyi onayla',
'iso.label':'Yeni \u00d6zellik','iso.title':'\u00d6zel ISO Olu\u015fturucu.<br><span class="gradient">Senin Windows\'un, senin kurallar\u0131n.</span>','iso.desc':'12 ad\u0131ml\u0131 sihirbaz ile tamamen \u00f6zelle\u015fmi\u015f bir Windows kurulum ISO\'su olu\u015fturun. Bloatware\'leri kald\u0131r\u0131n, uygulamalar\u0131 \u00f6n y\u00fckleyin, ayarlar\u0131 yap\u0131land\u0131r\u0131n - Windows a\u00e7\u0131lmadan \u00f6nce.','iso.cta':'ISO Builder\'\u0131 Dene',
'cp.label':'\u00c7apraz Platform','cp.title':'Tek uygulama. \u00dc\u00e7 platform.<br><span class="gradient">S\u0131f\u0131r taviz.</span>','cp.desc':'AuraCore Pro, Windows, Linux ve macOS\'ta her i\u015fletim sistemine \u00f6zel optimizasyonlarla yerel olarak \u00e7al\u0131\u015f\u0131r.',
'cp.win.t':'Windows','cp.win.d':'27 mod\u00fcl: Kay\u0131t Defteri Optimize, Oyun Modu, Bloatware Kald\u0131rma, Disk Temizleme, Depolama S\u0131k\u0131\u015ft\u0131rma, Defender Y\u00f6neticisi ve daha fazlas\u0131.',
'cp.linux.t':'Linux','cp.linux.d':'17 mod\u00fcl: Systemd Y\u00f6neticisi, Paket Temizleyici, Kernel Temizleyici, Docker Temizleyici, GRUB Y\u00f6neticisi, Uygulama Y\u00fckleyici (141 uygulama) ve daha fazlas\u0131.',
'cp.mac.t':'macOS','cp.mac.d':'16 mod\u00fcl: Brew Y\u00f6neticisi, Xcode Temizleyici, DNS Temizleyici, Spotlight Y\u00f6neticisi, Time Machine Y\u00f6neticisi, Uygulama Y\u00fckleyici (141 uygulama) ve daha fazlas\u0131.',
'ai.label':'AI Destekli','ai.title':'7 ince ayarl\u0131 AI modeli.<br><span class="gradient">Ki\u015fisel optimizasyon uzman\u0131n\u0131z.</span>','ai.desc':'2.844 AuraCore \u00f6rne\u011fiyle e\u011fitilmi\u015f yerle\u015fik AI asistan. Sorular\u0131 yan\u0131tlar, sorunlar\u0131 te\u015fhis eder ve optimizasyonlar \u00f6nerir \u2014 tamamen yerel olarak \u00e7al\u0131\u015f\u0131r.',
'ai.chat.t':'AI Sohbet','ai.chat.d':'Sistem optimizasyonu hakk\u0131nda her \u015feyi sorun. AI, sistem metriklerinizi ger\u00e7ek zamanl\u0131 okur ve ba\u011flama duyarl\u0131 \u00f6neriler sunar.',
'ai.rag.t':'RAG Getirme','ai.rag.d':'AI, yan\u0131tlamadan \u00f6nce vekt\u00f6r veritaban\u0131ndan ilgili kaynak kodu ba\u011flam\u0131n\u0131 al\u0131r \u2014 s\u0131f\u0131r hal\u00fcs\u00fcnasyon, maksimum do\u011fruluk.',
'ai.models.t':'7 Model Se\u00e7ene\u011fi','ai.models.d':'TinyLlama 1.1B\'den (h\u0131zl\u0131) Qwen 32B\'ye (g\u00fc\u00e7l\u00fc). Donan\u0131m\u0131n\u0131za en uygun modeli se\u00e7in \u2014 tamamen yerel, bulut gerektirmez.',
}};

// ═══════════════════════════════════════════════════════════
// PRICING
// ═══════════════════════════════════════════════════════════
const PRICES={
  en:{pro:{base:4.99,extra:2.00,sym:'$'},ent:{base:12.99,extra:1.50,sym:'$'}},
  tr:{pro:{base:149,extra:59,sym:'₺'},ent:{base:449,extra:39,sym:'₺'}}
};
let proDevices=1,entDevices=1;
function getPrice(tier,devices){
  const p=PRICES[lang][tier];
  const total=p.base+p.extra*Math.max(0,devices-1);
  return lang==='tr'?`${p.sym}${Math.round(total)}`:`${p.sym}${total.toFixed(2)}`;
}
function getP(){
  const pp=PRICES[lang].pro, ep=PRICES[lang].ent;
  const perTxt=lang==='en'?'/ month':'/ ay';
  const foreverTxt=lang==='en'?'/ forever':'/ süresiz';
  const extraTxt=lang==='en'?'per extra device':'ek cihaz başına';
  return {tiers:[
    {n:lang==='en'?'Free':'Ücretsiz',price:lang==='en'?'$0':'₺0',per:foreverTxt,
     desc:lang==='en'?'Everything you need to get started':'Başlamak için ihtiyacınız olan her şey',
     features:lang==='en'?['Dashboard + System Health','Junk Cleaner (basic)','RAM Optimizer','Startup Optimizer','1 Device']:['Kontrol Paneli + Sistem Sağlığı','Çöp Temizleyici (temel)','RAM Optimizasyonu','Başlangıç Yöneticisi','1 Cihaz'],
     btn:'',cls:'ghost',tier:'free'},
    {n:'Pro',price:getPrice('pro',proDevices),per:perTxt,
     desc:lang==='en'?'Full power for enthusiasts':'Tam güçlü optimizasyon deneyimi',
     features:lang==='en'?['All 47 modules unlocked','Gaming Mode + Profiles','Network Optimizer','PDF Health Reports','Auto-Scheduler',proDevices+' Device'+(proDevices>1?'s':''),'Priority support']:['T\u00fcm 47 mod\u00fcl a\u00e7\u0131k','Oyun Modu + Profiller','A\u011f Optimizasyonu','PDF Sa\u011fl\u0131k Raporlar\u0131','Otomatik Zamanlama',proDevices+' Cihaz','\u00d6ncelikli destek'],
     btn:lang==='en'?'Get Pro':'Pro Satın Al',cls:'primary',featured:true,tier:'pro',
     slider:{id:'pro',val:proDevices,extra:`+${pp.sym}${lang==='tr'?Math.round(pp.extra):pp.extra.toFixed(2)} ${extraTxt}`}},
    {n:'Enterprise',price:getPrice('ent',entDevices),per:perTxt,
     desc:lang==='en'?'For teams and organizations':'Ekipler ve kurumlar için',
     features:lang==='en'?['Everything in Pro',entDevices+' Device'+(entDevices>1?'s':''),'Admin Panel access','Fleet management','API access','Custom branding','Dedicated support']:["Pro'daki tüm özellikler",entDevices+' Cihaz','Yönetim Paneli erişimi','Cihaz filosu yönetimi','API erişimi','Kurumsal markalama','Birebir destek'],
     btn:lang==='en'?'Contact Sales':'İletişime Geç',cls:'ghost',tier:'enterprise',
     slider:{id:'ent',val:entDevices,extra:`+${ep.sym}${lang==='tr'?Math.round(ep.extra):ep.extra.toFixed(2)} ${extraTxt}`}}
  ]};
}

// ═══════════════════════════════════════════════════════════
// PLATFORM TABS
// ═══════════════════════════════════════════════════════════
let currentPlatform='windows';

function showPlatform(platform){
  currentPlatform=platform;
  document.querySelectorAll('.platform-tab').forEach(t=>{
    t.style.background='rgba(255,255,255,0.03)';
    t.style.color='var(--text2)';
  });
  const activeTab=document.getElementById('tab'+platform.charAt(0).toUpperCase()+platform.slice(1));
  if(activeTab){
    const colors={windows:'#3b82f6',linux:'#f59e0b',macos:'#8b5cf6'};
    const bgs={windows:'59,130,246',linux:'245,158,11',macos:'139,92,246'};
    activeTab.style.background='rgba('+bgs[platform]+',0.15)';
    activeTab.style.color=colors[platform];
  }
  const counts={windows:27,linux:17,macos:16};
  const titles={
    en:{windows:'27 Windows modules.',linux:'17 Linux modules.',macos:'16 macOS modules.'},
    tr:{windows:'27 Windows mod\u00fcl\u00fc.',linux:'17 Linux mod\u00fcl\u00fc.',macos:'16 macOS mod\u00fcl\u00fc.'}
  };
  const zeroBloat={en:'Zero bloat.',tr:'S\u0131f\u0131r gereksizlik.'};
  const titleEl=document.getElementById('modulesTitle');
  if(titleEl)titleEl.innerHTML=titles[lang][platform]+' <span class="gradient">'+zeroBloat[lang]+'</span>';
  const g=document.getElementById('modulesGrid');
  const modules=M[platform][lang];
  g.innerHTML=modules.map(m=>'<div class="module-card"><div class="module-icon" style="background:'+m.b+'">'+m.i+'</div><div><h4>'+m.t+'</h4><span>'+m.d+'</span></div></div>').join('');
}

// ═══════════════════════════════════════════════════════════
// RENDER
// ═══════════════════════════════════════════════════════════
let lang='en';

function render(){
  const s=L[lang];
  document.querySelectorAll('[data-i18n]').forEach(e=>{const k=e.getAttribute('data-i18n');if(s[k])e.textContent=s[k]});
  document.querySelectorAll('[data-i18n-html]').forEach(e=>{const k=e.getAttribute('data-i18n-html');if(s[k])e.innerHTML=s[k]});
  document.getElementById('langBtn').textContent=lang==='en'?'TR 🇹🇷':'EN 🇬🇧';
  document.getElementById('langBtnMobile').textContent=lang==='en'?'TR 🇹🇷':'EN 🇬🇧';
  document.documentElement.lang=lang;
  // Platform tab labels
  const tabLabels={en:{windows:'Windows (27)',linux:'Linux (17)',macos:'macOS (16)'},tr:{windows:'Windows (27)',linux:'Linux (17)',macos:'macOS (16)'}};
  const twEl=document.getElementById('tabWindows');const tlEl=document.getElementById('tabLinux');const tmEl=document.getElementById('tabMacos');
  if(twEl)twEl.textContent=tabLabels[lang].windows;
  if(tlEl)tlEl.textContent=tabLabels[lang].linux;
  if(tmEl)tmEl.textContent=tabLabels[lang].macos;
  // Modules
  showPlatform(currentPlatform);
  // ISO Steps
  const isoStepsData={en:[
    {n:'1',t:'Select ISO & Auto-Detect'},{n:'2',t:'User Account Setup'},{n:'3',t:'OOBE Skip Settings'},
    {n:'4',t:'WiFi Auto-Connect'},{n:'5',t:'Win11 Bypass (TPM/RAM)'},{n:'6',t:'Bloatware Removal (30+)'},
    {n:'7',t:'Custom EXE Bundler'},{n:'8',t:'Driver Backup & Scan'},{n:'9',t:'Winget App Pre-Install'},
    {n:'10',t:'Registry Presets'},{n:'11',t:'Post-Install Actions'},{n:'12',t:'Review & Build ISO'}
  ],tr:[
    {n:'1',t:'ISO Seç & Otomatik Algılama'},{n:'2',t:'Kullanıcı Hesabı'},{n:'3',t:'OOBE Atlama Ayarları'},
    {n:'4',t:'WiFi Otomatik Bağlanma'},{n:'5',t:'Win11 Bypass (TPM/RAM)'},{n:'6',t:'Bloatware Kaldırma (30+)'},
    {n:'7',t:'Özel EXE Ekleme'},{n:'8',t:'Sürücü Yedekleme & Tarama'},{n:'9',t:'Winget Uygulama Yükleme'},
    {n:'10',t:'Kayıt Defteri Şablonları'},{n:'11',t:'Kurulum Sonrası İşlemler'},{n:'12',t:'İnceleme & ISO Oluştur'}
  ]};
  const ig=document.getElementById('isoSteps');
  if(ig)ig.innerHTML=isoStepsData[lang].map(s=>`<div class="iso-step"><div class="iso-step-num">${s.n}</div><span>${s.t}</span></div>`).join('');
  // Pricing
  const pg=document.getElementById('pricingGrid');
  const tiers=getP().tiers;
  pg.innerHTML=tiers.map(t=>{
    const sliderHtml=t.slider?`
      <div style="margin:1rem 0;padding:12px;background:rgba(255,255,255,0.03);border-radius:10px;border:1px solid rgba(255,255,255,0.05)">
        <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:8px">
          <span style="font-size:0.75rem;color:var(--text3);text-transform:uppercase;letter-spacing:1px">${lang==='en'?'Devices':'Cihaz Sayısı'}</span>
          <span style="font-family:Outfit;font-weight:700;font-size:1.1rem;color:var(--cyan)">${t.slider.val}</span>
        </div>
        <input type="range" min="1" max="10" value="${t.slider.val}"
          oninput="${t.slider.id==='pro'?'proDevices':'entDevices'}=+this.value;render()"
          style="width:100%;accent-color:var(--cyan);cursor:pointer;height:6px">
        <div style="font-size:0.7rem;color:var(--text3);margin-top:6px;text-align:center">${t.slider.extra}</div>
      </div>`:'';
    return `<div class="price-card${t.featured?' featured':''}" ${t.featured?`data-badge="${s['p.badge']||'POPULAR'}"`:''}>
    <div class="price-tier">${t.n}</div><div class="price-amount">${t.price} <span>${t.per}</span></div>
    <p class="price-desc">${t.desc}</p>${sliderHtml}<ul class="price-features">${t.features.map(f=>`<li>${f}</li>`).join('')}</ul>
    ${t.btn ? '<a href="#" class="price-btn price-btn-'+t.cls+'" onclick="handleBuy(\''+ t.tier+'\','+(t.slider?t.slider.val:1)+');return false">'+t.btn+'</a>' : ''}</div>`;
  }).join('');
  updateNavAuth();
}

// ═══════════════════════════════════════════════════════════
// MOBILE MENU
// ═══════════════════════════════════════════════════════════
function toggleMenu(){
  const h=document.getElementById('hamburger');
  const m=document.getElementById('mobileMenu');
  h.classList.toggle('open');
  m.classList.toggle('open');
  document.body.style.overflow=m.classList.contains('open')?'hidden':'';
}
function closeMenu(){
  document.getElementById('hamburger').classList.remove('open');
  document.getElementById('mobileMenu').classList.remove('open');
  document.body.style.overflow='';
}

// ═══════════════════════════════════════════════════════════
// AUTH MODAL
// ═══════════════════════════════════════════════════════════
const API_URL = 'https://api.auracore.pro';

function showAuth(mode){
  closeMenu();
  const s=L[lang];
  const overlay=document.getElementById('authOverlay');
  const content=document.getElementById('authContent');

  if(mode==='login'){
    content.innerHTML=`
      <h3 class="auth-title">${s['auth.login.title']}</h3>
      <p class="auth-subtitle">${s['auth.login.subtitle']}</p>
      <input class="auth-input" type="email" id="authEmail" placeholder="${s['auth.email']}" autocomplete="email">
      <input class="auth-input" type="password" id="authPass" placeholder="${s['auth.password']}" autocomplete="current-password">
      <div style="text-align:right;margin:-4px 0 8px"><a href="#" onclick="showAuth('forgot');return false" style="color:var(--cyan);font-size:0.78rem;text-decoration:none;opacity:0.7;transition:opacity 0.2s" onmouseover="this.style.opacity='1'" onmouseout="this.style.opacity='0.7'">${lang==='en'?'Forgot password?':'Şifremi unuttum?'}</a></div>
      <button class="auth-btn" id="authBtn" onclick="doLogin()">${s['auth.login.btn']}</button>
      <div class="auth-msg" id="authMsg"></div>
      <div class="auth-switch">${s['auth.login.switch']} <a onclick="showAuth('register')">${s['auth.login.switchLink']}</a></div>`;
  } else if(mode==='forgot') {
    content.innerHTML=`
      <h3 class="auth-title">${lang==='en'?'Reset Password':'Şifre Sıfırla'}</h3>
      <p class="auth-subtitle">${lang==='en'?'Enter your email and we\'ll send you a reset code':'E-postanızı girin, size bir sıfırlama kodu göndereceğiz'}</p>
      <input class="auth-input" type="email" id="authEmail" placeholder="${s['auth.email']}" autocomplete="email">
      <button class="auth-btn" id="authBtn" onclick="doForgot()">${lang==='en'?'Send Reset Code':'Sıfırlama Kodu Gönder'}</button>
      <div class="auth-msg" id="authMsg"></div>
      <div class="auth-switch"><a onclick="showAuth('login')">${lang==='en'?'\u2190 Back to Sign In':'\u2190 Giriş\'e Dön'}</a></div>`;
  } else if(mode==='reset') {
    content.innerHTML=`
      <h3 class="auth-title">${lang==='en'?'Enter Reset Code':'Sıfırlama Kodunu Girin'}</h3>
      <p class="auth-subtitle">${lang==='en'?'Check your email for the 6-digit code':'E-postanızda 6 haneli kodu kontrol edin'}</p>
      <input class="auth-input" type="email" id="authEmail" placeholder="${s['auth.email']}" value="${window._resetEmail||''}" readonly style="opacity:0.6">
      <input class="auth-input" type="text" id="resetCode" placeholder="${lang==='en'?'6-digit code':'6 haneli kod'}" maxlength="6" style="text-align:center;font-size:1.3rem;letter-spacing:8px;font-family:monospace">
      <input class="auth-input" type="password" id="authPass" placeholder="${lang==='en'?'New password (min 8 chars)':'Yeni şifre (min 8 karakter)'}" autocomplete="new-password">
      <input class="auth-input" type="password" id="authPass2" placeholder="${lang==='en'?'Confirm new password':'Yeni şifre tekrar'}" autocomplete="new-password">
      <button class="auth-btn" id="authBtn" onclick="doReset()">${lang==='en'?'Reset Password':'Şifreyi Sıfırla'}</button>
      <div class="auth-msg" id="authMsg"></div>
      <div class="auth-switch"><a onclick="showAuth('forgot')">${lang==='en'?'\u2190 Request new code':'\u2190 Yeni kod iste'}</a></div>`;
  } else {
    content.innerHTML=`
      <h3 class="auth-title">${s['auth.register.title']}</h3>
      <p class="auth-subtitle">${s['auth.register.subtitle']}</p>
      <input class="auth-input" type="email" id="authEmail" placeholder="${s['auth.email']}" autocomplete="email">
      <input class="auth-input" type="password" id="authPass" placeholder="${s['auth.password']}" autocomplete="new-password">
      <input class="auth-input" type="password" id="authPass2" placeholder="${s['auth.confirmPassword']}" autocomplete="new-password">
      <button class="auth-btn" id="authBtn" onclick="doRegister()">${s['auth.register.btn']}</button>
      <div class="auth-msg" id="authMsg"></div>
      <div class="auth-switch">${s['auth.register.switch']} <a onclick="showAuth('login')">${s['auth.register.switchLink']}</a></div>`;
  }
  overlay.classList.add('open');
}

function closeAuth(){
  document.getElementById('authOverlay').classList.remove('open');
}

function showMsg(text, isError){
  const el=document.getElementById('authMsg');
  el.textContent=text;
  el.className='auth-msg '+(isError?'err':'ok');
}

async function doLogin(){
  const email=document.getElementById('authEmail').value.trim();
  const pass=document.getElementById('authPass').value;
  const btn=document.getElementById('authBtn');
  if(!email||!pass){showMsg(lang==='en'?'Please fill in all fields':'Lütfen tüm alanları doldurun',true);return;}
  btn.disabled=true;btn.textContent='...';
  try{
    const res=await fetch(API_URL+'/api/auth/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({email,password:pass})});
    const data=await res.json();
    if(res.ok&&data.accessToken){
      localStorage.setItem('aura_user',JSON.stringify({email,tier:data.user?.tier||'free'}));
      showMsg(lang==='en'?'Login successful!':'Giriş başarılı!',false);
      setTimeout(()=>{closeAuth();updateNavAuth();},1500);
    } else {
      showMsg(data.error||(lang==='en'?'Invalid email or password':'Geçersiz e-posta veya şifre'),true);
    }
  }catch(e){showMsg(lang==='en'?'Connection error':'Bağlantı hatası',true);}
  btn.disabled=false;btn.textContent=L[lang]['auth.login.btn'];
}

async function doRegister(){
  const email=document.getElementById('authEmail').value.trim();
  const pass=document.getElementById('authPass').value;
  const pass2=document.getElementById('authPass2').value;
  const btn=document.getElementById('authBtn');
  if(!email||!pass||!pass2){showMsg(lang==='en'?'Please fill in all fields':'Lütfen tüm alanları doldurun',true);return;}
  if(pass!==pass2){showMsg(lang==='en'?'Passwords do not match':'Şifreler eşleşmiyor',true);return;}
  if(pass.length<8){showMsg(lang==='en'?'Password must be at least 8 characters':'Şifre en az 8 karakter olmalı',true);return;}
  btn.disabled=true;btn.textContent='...';
  try{
    const res=await fetch(API_URL+'/api/auth/register',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({email,password:pass})});
    const data=await res.json();
    if(res.ok){
      showMsg(lang==='en'?'Account created! Please sign in.':'Hesap oluşturuldu! Lütfen giriş yapın.',false);
      setTimeout(()=>showAuth('login'),2000);
    } else {
      showMsg(data.error||(lang==='en'?'Registration failed':'Kayıt başarısız'),true);
    }
  }catch(e){showMsg(lang==='en'?'Connection error':'Bağlantı hatası',true);}
  btn.disabled=false;btn.textContent=L[lang]['auth.register.btn'];
}

// ═══════════════════════════════════════════════════════════
// PAYMENT
// ═══════════════════════════════════════════════════════════
async function handleBuy(tier, devices) {
  if (tier === 'free') { showAuth('register'); return; }
  showPaymentModal(tier, parseInt(devices) || 1);
}

function showPaymentModal(tier, devices) {
  const existing = document.getElementById('payModal');
  if (existing) existing.remove();
  const prices = lang === 'tr'
    ? (tier === 'enterprise' ? {base:449,extra:39,sym:'₺',cur:'TRY'} : {base:149,extra:59,sym:'₺',cur:'TRY'})
    : (tier === 'enterprise' ? {base:12.99,extra:1.50,sym:'$',cur:'USD'} : {base:4.99,extra:2.00,sym:'$',cur:'USD'});
  const total = prices.base + prices.extra * Math.max(0, devices - 1);
  const tierName = tier === 'enterprise' ? 'Enterprise' : 'Pro';
  const fmt = lang==='tr'?`${prices.sym}${Math.round(total)}`:`${prices.sym}${total.toFixed(2)}`;
  const yearlyTotal = lang==='tr'?`${prices.sym}${Math.round(total*10)}`:`${prices.sym}${(total*10).toFixed(2)}`;

  const modal = document.createElement('div');
  modal.id = 'payModal';
  modal.innerHTML = `
    <div style="position:fixed;inset:0;background:rgba(0,0,0,0.85);z-index:9999;display:flex;align-items:center;justify-content:center;padding:1rem" onclick="if(event.target===this)this.remove()">
      <div style="background:#0f1629;border:1px solid rgba(255,255,255,0.08);border-radius:16px;padding:2rem;max-width:420px;width:100%;position:relative">
        <button onclick="this.closest('#payModal').remove()" style="position:absolute;top:12px;right:12px;background:none;border:none;color:#94a3b8;font-size:1.4rem;cursor:pointer;width:32px;height:32px">&times;</button>
        <h3 style="font-family:Outfit;font-size:1.3rem;margin-bottom:0.5rem;color:#e8eaf6">${lang==='en'?'Complete Your Purchase':'Satın Alma'}</h3>
        <p style="color:#94a3b8;font-size:0.85rem;margin-bottom:1.5rem">${tierName} — ${devices} ${lang==='en'?'device(s)':'cihaz'} — <strong style="color:#06d6a0">${fmt}/${lang==='en'?'mo':'ay'}</strong></p>
        <button onclick="stripeCheckout('${tier}','monthly',${devices},'${prices.cur}')" style="width:100%;padding:14px;border-radius:10px;border:none;background:linear-gradient(135deg,#06d6a0,#0891b2);color:#050811;font-weight:700;font-size:0.95rem;cursor:pointer;font-family:DM Sans;margin-bottom:8px;display:flex;align-items:center;justify-content:center;gap:8px">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><rect x="1" y="4" width="22" height="16" rx="2"/><line x1="1" y1="10" x2="23" y2="10"/></svg>
          ${lang==='en'?'Pay Monthly':'Aylık Öde'}
        </button>
        <button onclick="stripeCheckout('${tier}','yearly',${devices},'${prices.cur}')" style="width:100%;padding:12px;border-radius:10px;border:1px solid rgba(139,92,246,0.3);background:rgba(139,92,246,0.1);color:#8b5cf6;font-weight:600;font-size:0.85rem;cursor:pointer;font-family:DM Sans;margin-bottom:8px">
          ${lang==='en'?'Yearly':'Yıllık'} — ${lang==='en'?'Save 20%':'%20 Tasarruf'} (${yearlyTotal}/${lang==='en'?'yr':'yıl'})
        </button>
        <div style="text-align:center;margin-top:4px">
          <a href="#" onclick="showCryptoInfo('${tier}',${devices});return false" style="font-size:0.78rem;color:#94a3b8;text-decoration:none;display:inline-flex;align-items:center;gap:6px;padding:6px 12px;border-radius:6px;transition:all 0.2s;border:1px solid rgba(255,255,255,0.05);background:rgba(255,255,255,0.02)" onmouseover="this.style.background='rgba(255,255,255,0.05)';this.style.borderColor='rgba(255,255,255,0.1)'" onmouseout="this.style.background='rgba(255,255,255,0.02)';this.style.borderColor='rgba(255,255,255,0.05)'">${lang==='en'?'\u20bf Pay with BTC / USDT':'\u20bf BTC / USDT ile \u00d6de'}</a>
        </div>
        <div id="payStatus" style="margin-top:12px;text-align:center;font-size:0.8rem;color:#94a3b8;display:none"></div>
        <div style="margin-top:1rem;text-align:center;font-size:0.65rem;color:#475569">${lang==='en'?'Secure payment via Stripe':'Stripe ile güvenli ödeme'}</div>
      </div>
    </div>`;
  document.body.appendChild(modal);
}

async function stripeCheckout(tier, plan, devices, currency) {
  const status = document.getElementById('payStatus');
  if (status) { status.style.display = 'block'; status.textContent = lang==='en'?'Redirecting to Stripe...':'Stripe\'a yönlendiriliyor...'; status.style.color='#94a3b8'; }
  const token = localStorage.getItem('aura_token');
  const headers = {'Content-Type': 'application/json'};
  if (token) headers['Authorization'] = 'Bearer ' + token;
  const endpoint = token ? '/api/payment/stripe/create-session' : '/api/payment/stripe/guest-checkout';
  try {
    const res = await fetch(API_URL + endpoint, {
      method: 'POST', headers,
      body: JSON.stringify({tier, plan, deviceCount: devices, currency: currency || 'USD'})
    });
    const data = await res.json();
    if (data.url) { window.location.href = data.url; }
    else { if (status) { status.style.color = '#ef4444'; status.textContent = data.error || (lang==='en'?'Payment error':'Ödeme hatası'); } }
  } catch (e) {
    if (status) { status.style.color = '#ef4444'; status.textContent = lang==='en'?'Connection error. Try again.':'Bağlantı hatası. Tekrar deneyin.'; }
  }
}

// ═══════════════════════════════════════════════════════════
// CRYPTO MODAL
// ═══════════════════════════════════════════════════════════
const CM_ADDR = {btc:'bc1q99jlwen6d8rh7uwculh9h0acwzc8serugsvf5s', usdt:'0x29985B1e41557871A8266D03f271B6836E2fbe78'};
let cmQrDone = false, cmLastTier = '', cmLastDevices = 1;

function cmGenQr() {
  if (cmQrDone || typeof QRCode === 'undefined') return;
  const o = {width:152, height:152, colorDark:'#0f1629', colorLight:'#ffffff', correctLevel:QRCode.CorrectLevel.M};
  new QRCode(document.getElementById('cmQrBtc'), {...o, text:'bitcoin:'+CM_ADDR.btc});
  new QRCode(document.getElementById('cmQrUsdt'), {...o, text:CM_ADDR.usdt});
  cmQrDone = true;
}

function showCryptoInfo(tier, devices) {
  cmLastTier = tier; cmLastDevices = devices;
  const cp = lang === 'tr'
    ? (tier==='enterprise' ? {base:449,extra:39,sym:'₺'} : {base:149,extra:59,sym:'₺'})
    : (tier==='enterprise' ? {base:12.99,extra:1.50,sym:'$'} : {base:4.99,extra:2.00,sym:'$'});
  const total = cp.base + cp.extra * Math.max(0, devices-1);
  const tierName = tier==='enterprise' ? 'Enterprise' : 'Pro';
  const perTxt = lang==='en' ? '/mo' : '/ay';
  const cfmt = lang==='tr' ? `${cp.sym}${Math.round(total)}` : `${cp.sym}${total.toFixed(2)}`;
  const pm = document.getElementById('payModal'); if (pm) pm.remove();

  document.getElementById('cmTitle').textContent = lang==='en' ? 'Pay with Crypto' : 'Kripto ile \u00d6de';
  document.getElementById('cmPN').textContent = tierName + ' \u2014 ' + devices + ' ' + (lang==='en'?'device'+(devices>1?'s':''):'cihaz');
  document.getElementById('cmPP').innerHTML = cfmt + ' <span>' + perTxt + '</span>';
  document.getElementById('cmQrLabelBtc').textContent = lang==='en' ? 'Scan to pay with Bitcoin' : 'Bitcoin ile \u00f6demek i\u00e7in taray\u0131n';
  document.getElementById('cmQrLabelUsdt').textContent = lang==='en' ? 'Scan to pay with USDT' : 'USDT ile \u00f6demek i\u00e7in taray\u0131n';
  document.getElementById('cmBackBtn').innerHTML = '&#8592; ' + (lang==='en' ? 'Back to Card Payment' : 'Kart \u00d6demesine D\u00f6n');
  document.getElementById('cmWarnText').innerHTML = lang==='en'
    ? '<strong>BTC:</strong> Only send via the <strong>Bitcoin network</strong>. Do NOT use BEP20 or other chains.<br><strong>USDT:</strong> Only send via <strong>Ethereum (ERC-20)</strong>. Do NOT use TRC-20 or BEP20.<br>Sending on the wrong network will result in <strong>permanent loss of funds</strong>.'
    : '<strong>BTC:</strong> Yaln\u0131zca <strong>Bitcoin a\u011f\u0131</strong> \u00fczerinden g\u00f6nderin. BEP20 veya di\u011fer zincirler KULLANMAYIN.<br><strong>USDT:</strong> Yaln\u0131zca <strong>Ethereum (ERC-20)</strong> \u00fczerinden g\u00f6nderin. TRC-20 veya BEP20 KULLANMAYIN.<br>Yanl\u0131\u015f a\u011fdan g\u00f6nderim <strong>kal\u0131c\u0131 fon kayb\u0131na</strong> neden olur.';
  document.getElementById('cmInfoText').innerHTML = lang==='en'
    ? 'Send the <strong style="color:var(--text)">exact amount</strong> shown above. After payment, email your <strong style="color:var(--amber)">transaction ID</strong> to <a href="mailto:support@auracore.pro">support@auracore.pro</a> \u2014 your license will be activated within 24 hours.'
    : 'Yukar\u0131daki <strong style="color:var(--text)">tam tutar\u0131</strong> g\u00f6nderin. \u00d6deme sonras\u0131 <strong style="color:var(--amber)">i\u015flem ID\'nizi</strong> <a href="mailto:support@auracore.pro">support@auracore.pro</a> adresine g\u00f6nderin \u2014 lisans\u0131n\u0131z 24 saat i\u00e7inde aktifle\u015ftirilir.';
  cmTab('btc');
  document.getElementById('cmOverlay').classList.add('open');
  document.body.style.overflow = 'hidden';
  cmGenQr();
}

function cmClose() {
  document.getElementById('cmOverlay').classList.remove('open');
  document.body.style.overflow = '';
}

function cmBackToCard() {
  if (cmLastTier) showPaymentModal(cmLastTier, cmLastDevices);
}

function cmTab(t) {
  document.getElementById('cmTB').classList.toggle('active', t==='btc');
  document.getElementById('cmTU').classList.toggle('active', t==='usdt');
  document.getElementById('cmPaneBtc').classList.toggle('active', t==='btc');
  document.getElementById('cmPaneUsdt').classList.toggle('active', t==='usdt');
}

function cmCopy(coin, btn) {
  const addr = CM_ADDR[coin==='btc'?'btc':'usdt'];
  const lbl = btn.querySelector('span');
  const ok = () => { lbl.textContent=lang==='en'?'Copied!':'Kopyaland\u0131!'; btn.classList.add('copied'); setTimeout(()=>{lbl.textContent='Copy';btn.classList.remove('copied')},2200); };
  if (navigator.clipboard && navigator.clipboard.writeText) {
    navigator.clipboard.writeText(addr).then(ok).catch(fb);
  } else fb();
  function fb() { const ta=document.createElement('textarea');ta.value=addr;ta.style.cssText='position:fixed;left:-9999px';document.body.appendChild(ta);ta.select();document.execCommand('copy');document.body.removeChild(ta);ok(); }
}


// ═══════════════════════════════════════════════════════════
// FORGOT PASSWORD
// ═══════════════════════════════════════════════════════════
window._resetEmail = '';

async function doForgot() {
  const email = document.getElementById('authEmail').value.trim();
  const btn = document.getElementById('authBtn');
  if (!email) { showMsg(lang==='en'?'Please enter your email':'Lütfen e-postanızı girin', true); return; }
  btn.disabled = true; btn.textContent = '...';
  try {
    const res = await fetch(API_URL+'/api/auth/password/forgot', {
      method:'POST', headers:{'Content-Type':'application/json'},
      body: JSON.stringify({email})
    });
    const data = await res.json();
    if (res.ok) {
      window._resetEmail = email;
      showMsg(lang==='en'?'Reset code sent! Check your email.':'Sıfırlama kodu gönderildi! E-postanızı kontrol edin.', false);
      setTimeout(() => showAuth('reset'), 2000);
    } else {
      showMsg(data.error || (lang==='en'?'Something went wrong':'Bir hata oluştu'), true);
    }
  } catch(e) { showMsg(lang==='en'?'Connection error':'Bağlantı hatası', true); }
  btn.disabled = false; btn.textContent = lang==='en'?'Send Reset Code':'Sıfırlama Kodu Gönder';
}

async function doReset() {
  const email = document.getElementById('authEmail').value.trim();
  const code = document.getElementById('resetCode').value.trim();
  const pass = document.getElementById('authPass').value;
  const pass2 = document.getElementById('authPass2').value;
  const btn = document.getElementById('authBtn');
  if (!email || !code || !pass) { showMsg(lang==='en'?'Please fill in all fields':'Lütfen tüm alanları doldurun', true); return; }
  if (code.length !== 6) { showMsg(lang==='en'?'Code must be 6 digits':'Kod 6 haneli olmalı', true); return; }
  if (pass.length < 8) { showMsg(lang==='en'?'Password must be at least 8 characters':'Şifre en az 8 karakter olmalı', true); return; }
  if (pass !== pass2) { showMsg(lang==='en'?'Passwords do not match':'Şifreler eşleşmiyor', true); return; }
  btn.disabled = true; btn.textContent = '...';
  try {
    const res = await fetch(API_URL+'/api/auth/password/reset', {
      method:'POST', headers:{'Content-Type':'application/json'},
      body: JSON.stringify({email, code, newPassword: pass})
    });
    const data = await res.json();
    if (res.ok) {
      showMsg(lang==='en'?'Password reset! You can now sign in.':'Şifre sıfırlandı! Artık giriş yapabilirsiniz.', false);
      window._resetEmail = '';
      setTimeout(() => showAuth('login'), 2500);
    } else {
      showMsg(data.error || (lang==='en'?'Invalid or expired code':'Geçersiz veya süresi dolmuş kod'), true);
    }
  } catch(e) { showMsg(lang==='en'?'Connection error':'Bağlantı hatası', true); }
  btn.disabled = false; btn.textContent = lang==='en'?'Reset Password':'Şifreyi Sıfırla';
}

// Payment success banner
if (window.location.search.includes('payment=success')) {
  setTimeout(() => {
    const banner = document.createElement('div');
    banner.style.cssText = 'position:fixed;top:0;left:0;right:0;background:linear-gradient(135deg,#06d6a0,#0891b2);color:#050811;text-align:center;padding:14px;font-weight:700;font-family:Outfit;z-index:10000;font-size:0.95rem';
    banner.innerHTML = (lang==='en'?'Payment successful! Check your email for details.':'Ödeme başarılı! Detaylar için e-postanızı kontrol edin.') + ' <a href="/" style="color:#050811;margin-left:10px;text-decoration:underline">✕</a>';
    document.body.prepend(banner);
  }, 500);
}

// ═══════════════════════════════════════════════════════════
// NAV AUTH STATE
// ═══════════════════════════════════════════════════════════
function updateNavAuth(){
  const user=JSON.parse(localStorage.getItem('aura_user')||'null');
  const desktop=document.getElementById('navAuthBtn');
  const mobile=document.querySelector('.nav-mobile-actions .nav-cta');
  if(user&&user.email){
    const short=user.email.length>18?user.email.substring(0,16)+'…':user.email;
    const tierBadge=user.tier&&user.tier!=='free'?' <span style="background:linear-gradient(135deg,var(--cyan),var(--blue));color:var(--bg);padding:2px 6px;border-radius:4px;font-size:0.6rem;font-weight:700;margin-left:4px;vertical-align:middle">'+user.tier.toUpperCase()+'</span>':'';
    if(desktop){desktop.innerHTML=short+tierBadge;desktop.onclick=e=>{e.preventDefault();showUserMenu()};}
    if(mobile){mobile.innerHTML='<span style="font-size:0.7rem">'+user.email.split('@')[0]+'</span>';mobile.onclick=e=>{e.preventDefault();showUserMenu()};}
  } else {
    if(desktop){desktop.textContent=L[lang]['nav.signin']||'Sign In';desktop.onclick=e=>{e.preventDefault();showAuth('login')};}
    if(mobile){mobile.textContent=L[lang]['nav.signin']||'Sign In';mobile.onclick=e=>{e.preventDefault();showAuth('login')};}
  }
}

function showUserMenu(){
  const existing=document.getElementById('userMenu');
  if(existing){existing.remove();return;}
  const user=JSON.parse(localStorage.getItem('aura_user')||'{}');
  const menu=document.createElement('div');
  menu.id='userMenu';
  menu.innerHTML=`
    <div style="position:fixed;inset:0;z-index:9998" onclick="document.getElementById('userMenu').remove()"></div>
    <div style="position:fixed;top:60px;right:2rem;background:var(--surface);border:1px solid rgba(255,255,255,0.08);border-radius:12px;padding:1rem;min-width:220px;z-index:9999;box-shadow:0 12px 40px rgba(0,0,0,0.5)">
      <div style="font-size:0.8rem;color:var(--text2);margin-bottom:4px">${lang==='en'?'Signed in as':'Giriş yapan'}</div>
      <div style="font-size:0.9rem;font-weight:600;color:var(--text);margin-bottom:12px;word-break:break-all">${user.email||''}</div>
      <div style="border-top:1px solid rgba(255,255,255,0.06);padding-top:10px;display:flex;flex-direction:column;gap:6px">
        <a href="#pricing" onclick="document.getElementById('userMenu').remove()" style="color:var(--text2);text-decoration:none;font-size:0.82rem;padding:6px 8px;border-radius:6px;transition:background 0.2s" onmouseover="this.style.background='rgba(255,255,255,0.04)'" onmouseout="this.style.background='none'">${lang==='en'?'Upgrade Plan':'Plan Yükselt'}</a>
        <a href="#" onclick="doLogout();return false" style="color:var(--red);text-decoration:none;font-size:0.82rem;padding:6px 8px;border-radius:6px;transition:background 0.2s" onmouseover="this.style.background='rgba(239,68,68,0.06)'" onmouseout="this.style.background='none'">${lang==='en'?'Sign Out':'Çıkış Yap'}</a>
      </div>
    </div>`;
  document.body.appendChild(menu);
}

function doLogout(){
  localStorage.removeItem('aura_user');
  const um=document.getElementById('userMenu');if(um)um.remove();
  updateNavAuth();
}

// ═══════════════════════════════════════════════════════════
// INIT
// ═══════════════════════════════════════════════════════════
function toggleLang(){lang=lang==='en'?'tr':'en';localStorage.setItem('aura_lang',lang);render()}
const sv=localStorage.getItem('aura_lang');if(sv)lang=sv;render();

const nav=document.getElementById('navbar');
window.addEventListener('scroll',()=>nav.classList.toggle('scrolled',window.scrollY>50));
const reveals=document.querySelectorAll('.reveal');
const obs=new IntersectionObserver(e=>{e.forEach((en,i)=>{if(en.isIntersecting){setTimeout(()=>en.target.classList.add('visible'),i*80);obs.unobserve(en.target)}})},{threshold:0.1,rootMargin:'0px 0px -50px 0px'});
reveals.forEach(e=>obs.observe(e));
updateNavAuth();
document.querySelectorAll('a[href^="#"]').forEach(a=>{a.addEventListener('click',e=>{if(a.getAttribute('href')==='#')return;e.preventDefault();const t=document.querySelector(a.getAttribute('href'));if(t)t.scrollIntoView({behavior:'smooth'})})});
// ESC to close modals
document.addEventListener('keydown',e=>{if(e.key==='Escape'){closeAuth();const pm=document.getElementById('payModal');if(pm)pm.remove();cmClose();const um=document.getElementById('userMenu');if(um)um.remove();}});


// ============================================================================
// Release pipeline integration (added 2026-04-21, Phase 6.6)
// Fetches latest version from backend and updates primary Download CTAs.
// Falls back silently to hardcoded v1.6.0 GitHub link if API unavailable.
// ============================================================================
(function() {
  'use strict';

  function detectOS() {
    var p = (navigator.platform || '').toLowerCase();
    var ua = (navigator.userAgent || '').toLowerCase();
    if (p.indexOf('win') >= 0 || ua.indexOf('windows') >= 0) return 'windows';
    if (p.indexOf('linux') >= 0 || ua.indexOf('linux') >= 0) return 'linux';
    if (p.indexOf('mac') >= 0 || ua.indexOf('macintosh') >= 0) return 'macos';
    return 'windows';
  }

  function displayOS(os) {
    return { windows: 'Windows', linux: 'Linux', macos: 'macOS' }[os] || 'Windows';
  }

  async function fetchPlatformRelease(platform) {
    try {
      var r = await fetch('/api/updates/check?currentVersion=0.0.0&platform=' + platform);
      if (!r.ok) return null;
      var j = await r.json();
      return j.updateAvailable ? j : null;
    } catch (e) { return null; }
  }

  async function loadLatestRelease() {
    var os = detectOS();
    var jobs = [fetchPlatformRelease(os)];
    if (os !== 'linux') jobs.push(fetchPlatformRelease('linux'));
    else jobs.push(fetchPlatformRelease('windows'));

    var results = await Promise.all(jobs);
    var primary = results[0];
    var other = results[1];

    if (primary) {
      document.querySelectorAll('.download-link').forEach(function(a) {
        a.href = primary.downloadUrl;
      });
      document.querySelectorAll('.download-version').forEach(function(el) {
        el.textContent = 'v' + primary.version;
      });
      document.querySelectorAll('.download-platform').forEach(function(el) {
        el.textContent = displayOS(os);
      });
    }

    var dropdown = document.getElementById('otherPlatformsList');
    if (dropdown) {
      dropdown.innerHTML = '';
      var add = function(label, url, comingSoon) {
        var li = document.createElement('li');
        if (comingSoon) {
          li.innerHTML = '<span class="disabled">' + label + ' — Coming Soon</span>';
        } else {
          li.innerHTML = '<a href="' + url + '">' + label + '</a>';
        }
        dropdown.appendChild(li);
      };
      if (other && os === 'windows')  add('Linux',   other.downloadUrl, false);
      if (other && os === 'linux')    add('Windows', other.downloadUrl, false);
      if (os === 'macos')             { if (other) add('Windows', other.downloadUrl, false); add('macOS', '', true); }
      else                            add('macOS',   '', true);
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', loadLatestRelease);
  } else {
    loadLatestRelease();
  }
})();
