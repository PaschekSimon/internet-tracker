# Implementation Plan - Router Monitoring Application

Analyze network packets and API endpoints of the ZTE MF282 / DreiTube Router to capture RSRP, RSRQ, download/upload speed, uptime, and connected device details, and outline an architectural roadmap for a custom monitoring app.

## User Review Required

> [!IMPORTANT]
> The target router IP address active on your network is **`192.168.0.1`** (the router default gateway address), whereas `192.168.1.1` timed out. Router credentials are stored locally in `.env` (see `.env.example`: `ROUTER_PASSWORD=<your-router-password>`).

> [!NOTE]
> All technical details, exact API endpoints, request payloads, response schemas, and authentication hashing details have been documented in [`router_api_analysis_and_strategy.md`](file:///C:/Users/simon/.gemini/antigravity/brain/ae1febfc-8f9a-4451-bf82-137e8e17e113/router_api_analysis_and_strategy.md).

## Open Questions

> [!IMPORTANT]
> **Which platform/format would you prefer for the monitoring application?**
> 1. **Windows System Tray App / Taskbar Bar** (C# .NET / Tauri / Rust) for lightweight status on your PC taskbar.
> 2. **Modern Web Dashboard** (Node.js / Python backend + React/Vite frontend) accessible from any browser in your home network.
> 3. **Mobile App** (Flutter / React Native) to monitor your router on Android / iOS.
> 4. **Multi-platform Hybrid** (Lightweight Node.js service backend + modular frontends for both PC & Mobile).

## Proposed Architecture & Strategy

Detailed documentation created in:
- [router_api_analysis_and_strategy.md](file:///C:/Users/simon/.gemini/antigravity/brain/ae1febfc-8f9a-4451-bf82-137e8e17e113/router_api_analysis_and_strategy.md)

### Key Discovered API Specifications

1. **Authentication**:
   - `POST http://192.168.0.1/goform/goform_set_cmd_process`
   - Password Encoded: `SHA256(Base64(password))`
   - Session Cookie: `zwsd="..."`

2. **Signal Quality (RSRP & RSRQ)**:
   - `GET http://192.168.0.1/goform/goform_get_cmd_process?isTest=false&cmd=network_type,rssi,rscp,lte_rsrp,Z_rssi,Z_rscp,Z_rsrp,Z_rsrq,wan_lte_ca,lte_ca_pcell_bandwidth,lte_ca_pcell_band,lte_ca_scell_bandwidth,lte_ca_scell_band,ZCELLINFO_band&multi_data=1`
   - Yields: `lte_rsrp` (-106 dBm), `Z_rsrq` (-17.0 dB), `Z_rssi` (-70 dBm), `ZCELLINFO_band` (LTE B3).

3. **Traffic Speeds, Uptime & Device Counts**:
   - `GET http://192.168.0.1/goform/goform_get_cmd_process?multi_data=1&isTest=false&cmd=...realtime_tx_thrpt,realtime_rx_thrpt,realtime_time,realtime_tx_bytes,realtime_rx_bytes...`
   - Yields: `realtime_rx_thrpt` (Download speed B/s), `realtime_tx_thrpt` (Upload speed B/s), `realtime_time` (Session uptime in seconds), total downloaded/uploaded bytes.

4. **Connected Clients List**:
   - `GET http://192.168.0.1/goform/goform_get_cmd_process?isTest=false&cmd=lan_station_list`
   - Yields JSON array of connected stations with `hostname`, `ip_addr`, `mac_addr`.

## Verification Plan

### Automated Tests
- Test API authentication script using Python / Node.js with the password hashing logic to verify automated login & session token retrieval.

### Manual Verification
- Verify that live router metrics match the router GUI in real-time when polling the `goform` endpoints.
