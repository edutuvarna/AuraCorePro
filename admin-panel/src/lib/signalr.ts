/*
Phase 6.11 SignalR event additions (wire listeners via `on('EventName', fn)`):

- PermissionRequested  (superadmin-only group) — { requestId, adminEmail, permissionKey, reason, requestedAt }
- PermissionApproved   (targeted via Clients.User) — { permissionKey, expiresAt? }
- PermissionDenied     (targeted) — { permissionKey, reviewNote? }
- PermissionRevoked    (targeted) — { permissionKey, reason? }
*/
import * as signalR from "@microsoft/signalr";
import { getToken } from "./api";
const API=process.env.NEXT_PUBLIC_API_URL||"https://api.auracore.pro";
let conn:signalR.HubConnection|null=null;
const L:Record<string,Function[]>={};
// Phase 6.10 Wave 4 (Task 21): backend AdminHub at /hubs/admin is live —
// re-enabled. Nginx /hubs/ location block has WebSocket upgrade headers
// (proxy_http_version 1.1 + Upgrade/Connection + 86400s read timeout) and
// CORS is handled by the backend with .AllowCredentials() (Phase 6.9 hotfix).
const SIGNALR_ENABLED = true;

export function startConnection(){
if(!SIGNALR_ENABLED)return;
if(conn?.state===signalR.HubConnectionState.Connected)return;
if(!getToken())return;
conn=new signalR.HubConnectionBuilder()
.withUrl(API+"/hubs/admin",{accessTokenFactory:()=>getToken()||""})
.withAutomaticReconnect([0,2000,5000,10000,30000])
.configureLogging(signalR.LogLevel.Warning).build();
Object.keys(L).forEach(k=>L[k].forEach(f=>conn!.on(k,f as any)));
conn.start().catch(e=>console.warn("SignalR:",e));}
export function stopConnection(){conn?.stop();conn=null;}
export function on(e:string,f:Function){if(!L[e])L[e]=[];L[e].push(f);conn?.on(e,f as any);}
export function off(e:string,f:Function){if(L[e])L[e]=L[e].filter(x=>x!==f);conn?.off(e,f as any);}
export function getConnection(){return conn;}
