/**
 * Configuration page — system feature flags + maintenance message.
 * Toggles for maintenance mode, new registrations, telemetry, crash reports,
 * and auto-update delivery, plus a free-form maintenance message that the
 * desktop app surfaces when the kill-switch is on.
 *
 * Lives under src/views/ rather than src/pages/ to avoid Next.js auto-detecting
 * the legacy Pages Router (sibling extractions Tasks 4-9 follow the same
 * convention).
 *
 * No shared primitives are needed here (form-only — no KPICard / EmptyState /
 * SearchBar / Pagination / StatusBadge call sites), so Strategy B copies are
 * skipped for this file.
 *
 * Phase 6.10 W2.T10 — extracted from page.tsx (originally `ConfigPage`,
 * renamed `ConfigurationPage` per task plan for clarity).
 */

'use client';

import { useState, useEffect } from 'react';
import {
    AlertTriangle, UserCheck, BarChart2, Bug, Zap,
    RefreshCw, Check,
} from 'lucide-react';
import { api } from '@/lib/api';
import { PageHeader } from '@/components/PageHeader';

export function ConfigurationPage() {
    const [config, setConfig] = useState<any>(null);
    const [saving, setSaving] = useState(false);
    const [msg, setMsg] = useState('');

    useEffect(() => { api.getConfig().then(setConfig); }, []);

    const toggleFlag = async (key: string) => {
        if (!config) return;
        const newVal = !config[key];
        setSaving(true);
        const updated = await api.updateConfig({ [key]: newVal });
        setSaving(false);
        if (updated) { setConfig(updated); setMsg(''); }
        else setMsg('Failed to update');
    };

    const saveMessage = async () => {
        if (!config) return;
        setSaving(true);
        const updated = await api.updateConfig({ maintenanceMessage: config.maintenanceMessage });
        setSaving(false);
        if (updated) { setConfig(updated); setMsg('Saved!'); setTimeout(() => setMsg(''), 2000); }
        else setMsg('Failed');
    };

    const flags = [
        { key: 'isMaintenanceMode', label: 'Maintenance Mode', desc: 'Block all user logins and API access (admin panel stays accessible)', icon: AlertTriangle, danger: true },
        { key: 'newRegistrations', label: 'New Registrations', desc: 'Allow new users to register accounts', icon: UserCheck },
        { key: 'telemetryEnabled', label: 'Telemetry Collection', desc: 'Collect usage analytics from desktop app', icon: BarChart2 },
        { key: 'crashReportsEnabled', label: 'Crash Reports', desc: 'Accept crash report submissions from clients', icon: Bug },
        { key: 'autoUpdateEnabled', label: 'Auto-Update Delivery', desc: 'Deliver update notifications to desktop app', icon: Zap },
    ];

    if (!config) return <div className="flex items-center justify-center h-64"><RefreshCw className="w-6 h-6 text-white/20 animate-spin" /></div>;

    return (
        <div className="animate-fade-in">
            <PageHeader title="Configuration" subtitle="Feature flags and system settings">
                <span className="text-xs text-white/30">Last updated: {config.lastUpdated ? new Date(config.lastUpdated).toLocaleString() : '-'}</span>
            </PageHeader>

            <div className="glass-card p-6 max-w-2xl">
                <h3 className="text-[11px] font-semibold text-white/25 uppercase tracking-widest mb-5">Feature Flags</h3>
                <div className="space-y-1">
                    {flags.map(flag => (
                        <div key={flag.key} className="flex items-center justify-between py-4 px-4 -mx-4 rounded-xl hover:bg-white/[0.02] transition-colors">
                            <div className="flex items-start gap-3">
                                <flag.icon className={`w-5 h-5 mt-0.5 ${flag.danger ? 'text-aura-amber' : 'text-white/30'}`} />
                                <div>
                                    <p className="font-medium text-sm">{flag.label}</p>
                                    <p className="text-xs text-white/35 mt-0.5">{flag.desc}</p>
                                </div>
                            </div>
                            <button onClick={() => toggleFlag(flag.key)} disabled={saving}
                                className={`relative w-12 h-6 rounded-full transition-all duration-300 ${config[flag.key] ? (flag.danger ? 'bg-aura-amber' : 'bg-accent') : 'bg-white/10'}`}>
                                <div className={`absolute top-1 w-4 h-4 bg-white rounded-full shadow transition-all duration-300 ${config[flag.key] ? 'left-7' : 'left-1'}`} />
                            </button>
                        </div>
                    ))}
                </div>

                <div className="mt-6 pt-6 border-t border-white/[0.06]">
                    <h3 className="text-[11px] font-semibold text-white/25 uppercase tracking-widest mb-3">Maintenance Message</h3>
                    <textarea value={config.maintenanceMessage || ''} onChange={e => setConfig({ ...config, maintenanceMessage: e.target.value })}
                        className="input-dark w-full h-20 resize-none mb-3" placeholder="AuraCore Pro is currently under maintenance..." />
                    <div className="flex items-center gap-3">
                        <button onClick={saveMessage} disabled={saving} className="btn-primary flex items-center gap-2">
                            {saving ? <RefreshCw className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}Save Message
                        </button>
                        {msg && <span className={`text-sm ${msg === 'Saved!' ? 'text-aura-green' : 'text-aura-red'}`}>{msg}</span>}
                    </div>
                </div>
            </div>
        </div>
    );
}
