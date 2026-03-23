# Aura Core Pro — Production Deployment Guide
# Ubuntu Server + Cloudflare + PostgreSQL

## Overview

This guide deploys the Aura Core Pro backend API on an Ubuntu server
accessible worldwide via Cloudflare. Architecture:

```
Users worldwide → Cloudflare (CDN + DDoS) → Your Domain (api.auracorepro.com)
    → Nginx (reverse proxy + SSL) → ASP.NET Core API (:5000)
    → PostgreSQL (local)
```

---

## Part 1: Ubuntu Server Setup

### Step 1: Install .NET 8 Runtime

```bash
# Add Microsoft package repo
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install ASP.NET Core runtime
sudo apt update
sudo apt install -y aspnetcore-runtime-8.0

# Verify
dotnet --info
```

### Step 2: Install PostgreSQL

```bash
sudo apt install -y postgresql postgresql-contrib

# Start and enable
sudo systemctl start postgresql
sudo systemctl enable postgresql

# Create database and user
sudo -u postgres psql << SQL
CREATE USER auracoreuser WITH PASSWORD 'YOUR_STRONG_DB_PASSWORD';
CREATE DATABASE auracoredb OWNER auracoreuser;
GRANT ALL PRIVILEGES ON DATABASE auracoredb TO auracoreuser;
SQL
```

### Step 3: Install Nginx

```bash
sudo apt install -y nginx
sudo systemctl start nginx
sudo systemctl enable nginx
```

### Step 4: Deploy the API

```bash
# On your dev machine — publish the API
cd AuraCorePro
dotnet publish src/Backend/AuraCore.API -c Release -o ./publish

# Copy to server (replace YOUR_SERVER_IP)
scp -r ./publish/* user@YOUR_SERVER_IP:/opt/auracore/

# On the server
sudo mkdir -p /opt/auracore
sudo chown -R www-data:www-data /opt/auracore
```

### Step 5: Configure the API

Create the production appsettings on the server:

```bash
sudo nano /opt/auracore/appsettings.Production.json
```

Content:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=auracoredb;Username=auracoreuser;Password=YOUR_STRONG_DB_PASSWORD"
  },
  "Jwt": {
    "Secret": "GENERATE_A_RANDOM_64_CHAR_STRING_HERE_USE_openssl_rand_hex_32",
    "Issuer": "AuraCorePro",
    "Audience": "AuraCorePro"
  },
  "Stripe": {
    "SecretKey": "sk_live_YOUR_STRIPE_SECRET",
    "PublishableKey": "pk_live_YOUR_STRIPE_PUBLISHABLE",
    "WebhookSecret": "whsec_YOUR_WEBHOOK_SECRET"
  },
  "Crypto": {
    "BTC": { "Address": "YOUR_BTC_WALLET" },
    "USDT_TRC20": { "Address": "YOUR_TRC20_WALLET" },
    "USDT_ERC20": { "Address": "YOUR_ERC20_WALLET" }
  },
  "Kestrel": {
    "Endpoints": {
      "Http": { "Url": "http://localhost:5000" }
    }
  }
}
```

Generate a JWT secret:
```bash
openssl rand -hex 32
```

### Step 6: Create systemd Service

```bash
sudo nano /etc/systemd/system/auracore.service
```

Content:

```ini
[Unit]
Description=Aura Core Pro API
After=network.target postgresql.service

[Service]
WorkingDirectory=/opt/auracore
ExecStart=/usr/bin/dotnet /opt/auracore/AuraCore.API.dll
Restart=always
RestartSec=5
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl start auracore
sudo systemctl enable auracore

# Check status
sudo systemctl status auracore
# Check logs
sudo journalctl -u auracore -f
```

### Step 7: Configure Nginx Reverse Proxy

```bash
sudo nano /etc/nginx/sites-available/auracore
```

Content:

```nginx
server {
    listen 80;
    server_name api.auracorepro.com;  # Your domain

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;

        # Allow large file uploads (for crash reports)
        client_max_body_size 10M;
    }
}
```

```bash
sudo ln -s /etc/nginx/sites-available/auracore /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

---

## Part 2: Domain + Cloudflare Setup

### Step 1: Buy a Domain

Buy `auracorepro.com` (or your choice) from:
- Namecheap (cheapest)
- Cloudflare Registrar (no markup)
- Google Domains

### Step 2: Add to Cloudflare

1. Go to https://dash.cloudflare.com
2. "Add a site" → enter your domain
3. Select Free plan
4. Cloudflare gives you 2 nameservers — set these at your domain registrar
5. Wait for DNS propagation (5 min to 48 hours)

### Step 3: Add DNS Records

In Cloudflare Dashboard → DNS:

| Type | Name | Content | Proxy |
|------|------|---------|-------|
| A | api | YOUR_SERVER_IP | Proxied (orange cloud) |
| A | @ | YOUR_SERVER_IP | Proxied |

### Step 4: SSL/TLS Settings

In Cloudflare Dashboard → SSL/TLS:
- Set mode to **Full (Strict)**
- Enable **Always Use HTTPS**
- Enable **Automatic HTTPS Rewrites**

Install origin certificate on your server:

```bash
# Install Certbot for Let's Encrypt
sudo apt install -y certbot python3-certbot-nginx

# Get certificate
sudo certbot --nginx -d api.auracorepro.com

# Auto-renewal
sudo certbot renew --dry-run
```

### Step 5: Security Settings

In Cloudflare Dashboard:
- **WAF** → Enable managed rules
- **DDoS** → Auto-enabled
- **Bot Fight Mode** → On
- **Rate Limiting** → Create rule:
  - URL: `api.auracorepro.com/api/auth/*`
  - Rate: 20 requests per minute
  - Action: Block

---

## Part 3: Connecting Payment Systems

### Stripe Setup

1. Go to https://dashboard.stripe.com
2. Get your API keys (Settings → API keys)
3. Create a webhook endpoint:
   - URL: `https://api.auracorepro.com/api/payment/stripe/webhook`
   - Events: `checkout.session.completed`, `invoice.paid`, `customer.subscription.deleted`
4. Copy the webhook signing secret
5. Update `appsettings.Production.json` with all three keys

### Bitcoin (BTC) Setup

Option A — Manual (simplest):
1. Create a Bitcoin wallet (Electrum, Ledger, or exchange wallet)
2. Put your receive address in `appsettings.Production.json`
3. Users send BTC, submit TX hash, you verify manually in admin panel

Option B — Automated (BTCPay Server):
1. Install BTCPay Server on your server or use hosted version
2. BTCPay generates unique addresses per payment
3. Webhook notifies your API when payment confirmed
4. See: https://btcpayserver.org

### USDT Setup

Option A — Manual:
1. Create a Tron wallet for TRC-20 USDT (TronLink, Ledger)
2. Create an Ethereum wallet for ERC-20 USDT (MetaMask, Ledger)
3. Put addresses in appsettings
4. Users send USDT, submit TX hash, you verify on blockchain explorer

Option B — Automated (NOWPayments / CoinGate):
1. Sign up at https://nowpayments.io or https://coingate.com
2. Get API key
3. Their API generates unique payment pages per transaction
4. Webhook callback when payment confirms
5. Supports BTC, USDT, ETH, and 100+ cryptocurrencies

---

## Part 4: Update Desktop App Server URL

In the desktop app's Login screen, users enter your server URL:

```
https://api.auracorepro.com
```

Or hardcode it as the default in `LoginWindow.xaml.cs`:

```csharp
public static string? ApiBaseUrl { get; private set; } = "https://api.auracorepro.com";
```

And in the XAML:
```xml
<TextBox x:Name="ServerUrl" Text="https://api.auracorepro.com" ... />
```

---

## Part 5: Database Migration on Production

```bash
# On your dev machine — generate migration
cd AuraCorePro
dotnet ef migrations add Production \
  --project src/Backend/AuraCore.API.Infrastructure \
  --startup-project src/Backend/AuraCore.API \
  --output-dir Data/Migrations

# Apply on server
ssh user@YOUR_SERVER
cd /opt/auracore
ASPNETCORE_ENVIRONMENT=Production dotnet AuraCore.API.dll --migrate
```

Or set auto-migrate in Program.cs (already enabled for Development — change to also run in Production for first deploy).

---

## Part 6: Monitoring + Maintenance

### View Logs
```bash
sudo journalctl -u auracore -f          # Live logs
sudo journalctl -u auracore --since today # Today's logs
```

### Restart API
```bash
sudo systemctl restart auracore
```

### Update Deployment
```bash
# On dev machine
dotnet publish src/Backend/AuraCore.API -c Release -o ./publish
scp -r ./publish/* user@SERVER:/opt/auracore/

# On server
sudo systemctl restart auracore
```

### Database Backup (daily cronjob)
```bash
# Add to crontab
sudo crontab -e

# Add this line (runs at 3 AM daily)
0 3 * * * pg_dump -U auracoreuser auracoredb | gzip > /opt/backups/auracore_$(date +\%Y\%m\%d).sql.gz
```

### Firewall
```bash
sudo ufw allow 22    # SSH
sudo ufw allow 80    # HTTP
sudo ufw allow 443   # HTTPS
sudo ufw enable
```

---

## Checklist Before Going Live

- [ ] Domain purchased and pointed to Cloudflare
- [ ] Cloudflare SSL set to Full (Strict)
- [ ] Server firewall configured (only 22, 80, 443 open)
- [ ] PostgreSQL password is strong (not "postgres")
- [ ] JWT secret is a random 64-character string
- [ ] Stripe API keys are LIVE keys (not test)
- [ ] Crypto wallet addresses are correct
- [ ] Database auto-backup cronjob is set
- [ ] API is accessible at https://api.auracorepro.com/health
- [ ] Rate limiting on auth endpoints
- [ ] Desktop app default URL points to production server
- [ ] Admin account created and secured
- [ ] Test payment flow end-to-end (Stripe test mode first)
