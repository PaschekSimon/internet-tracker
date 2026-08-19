using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using RouterTaskbarMonitor;

class TestLogin
{
    static void Main()
    {
        string routerUrl = "http://192.168.0.1";
        string rawPwd = EnvLoader.Get("ROUTER_PASSWORD");
        if (string.IsNullOrEmpty(rawPwd))
        {
            Console.WriteLine("ROUTER_PASSWORD not found in .env file.");
            return;
        }

        string hashedPwd = ComputePasswordHash(rawPwd);
        string postData = "isTest=false&goformId=LOGIN&password=" + hashedPwd;
        byte[] data = Encoding.UTF8.GetBytes(postData);

        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(routerUrl + "/goform/goform_set_cmd_process");
        req.Method = "POST";
        req.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
        req.Referer = routerUrl + "/index.html";
        req.Headers.Add("Origin", routerUrl);
        req.Headers.Add("X-Requested-With", "XMLHttpRequest");
        req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36";

        string cookieHeader = null;

        using (Stream s = req.GetRequestStream())
        {
            s.Write(data, 0, data.Length);
        }

        using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
        {
            using (StreamReader r = new StreamReader(resp.GetResponseStream()))
            {
                Console.WriteLine("LOGIN RESPONSE: " + r.ReadToEnd());
            }

            string setCookie = resp.Headers["Set-Cookie"];
            Console.WriteLine("Raw Set-Cookie Header: " + setCookie);
            if (!string.IsNullOrEmpty(setCookie))
            {
                // Format cookie header: e.g. zwsd="4fa91c19..."
                string[] parts = setCookie.Split(';');
                cookieHeader = parts[0].Trim();
                Console.WriteLine("Parsed Cookie Header: " + cookieHeader);
            }
        }

        if (cookieHeader != null)
        {
            // Test Signal Info Request with manual Cookie header
            string sigUrl = routerUrl + "/goform/goform_get_cmd_process?isTest=false&cmd=network_type,rssi,rscp,lte_rsrp,Z_rssi,Z_rscp,Z_rsrp,Z_rsrq,wan_lte_ca,lte_ca_pcell_bandwidth,lte_ca_pcell_band,lte_ca_scell_bandwidth,lte_ca_scell_band,ZCELLINFO_band&multi_data=1&_=" + DateTime.Now.Ticks;

            HttpWebRequest reqSig = (HttpWebRequest)WebRequest.Create(sigUrl);
            reqSig.Method = "GET";
            reqSig.Referer = routerUrl + "/index.html";
            reqSig.Headers.Add("X-Requested-With", "XMLHttpRequest");
            reqSig.Headers.Add("Cookie", cookieHeader);
            reqSig.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36";

            using (HttpWebResponse respSig = (HttpWebResponse)reqSig.GetResponse())
            using (StreamReader rSig = new StreamReader(respSig.GetResponseStream()))
            {
                Console.WriteLine("\nMANUAL COOKIE SIGNAL RESPONSE:\n" + rSig.ReadToEnd());
            }

            // Test Speed Info Request with manual Cookie header
            string speedUrl = routerUrl + "/goform/goform_get_cmd_process?multi_data=1&isTest=false&sms_received_flag_flag=0&sts_received_flag_flag=0&cmd=modem_main_state,pin_status,opms_wan_mode,loginfo,new_version_state,current_upgrade_state,is_mandatory,wan_ipaddr,static_wan_ipaddr,ipv6_wan_ipaddr,signalbar,network_type,network_provider,ppp_status,EX_SSID1,sta_ip_status,EX_wifi_profile,guest_switch,wifi_ap_mode,SSID1,m_SSID,wifi_onoff_state,wifi_chip1_ssid1_ssid,wifi_chip2_ssid1_ssid,wifi_chip1_ssid1_access_sta_num,wifi_chip2_ssid1_access_sta_num,wifi_chip1_ssid2_access_sta_num,wifi_chip2_ssid2_access_sta_num,wifi_lbd_enable,simcard_roam,lan_ipaddr,wifi_access_sta_num,station_mac,battery_charging,battery_vol_percent,battery_pers,spn_name_data,spn_b1_flag,spn_b2_flag,wifi_chip1_ssid1_switch_onoff,wifi_chip2_ssid1_switch_onoff,realtime_tx_bytes,realtime_rx_bytes,realtime_time,realtime_tx_thrpt,realtime_rx_thrpt,monthly_rx_bytes,monthly_tx_bytes,monthly_time,date_month,data_volume_limit_switch,data_volume_limit_size,data_volume_alert_percent,data_volume_limit_unit,roam_setting_option,upg_roam_switch,ssid,wifi_enable,dial_mode,hmnc,hmcc,mnc,mcc,wlan_process,wifi_dfs_status,privacy_read_flag,wifi_mesh_sta_access_num,wifi_mesh_lan_access_num,wifi_mesh_deployed,wifi_start_mode,wifi_mesh_re_num,wan_lte_ca,pppoe_status,dhcp_wan_status,static_wan_status,sms_received_flag,sts_received_flag,sms_unread_num&_=" + DateTime.Now.Ticks;

            HttpWebRequest reqSpd = (HttpWebRequest)WebRequest.Create(speedUrl);
            reqSpd.Method = "GET";
            reqSpd.Referer = routerUrl + "/index.html";
            reqSpd.Headers.Add("X-Requested-With", "XMLHttpRequest");
            reqSpd.Headers.Add("Cookie", cookieHeader);
            reqSpd.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36";

            using (HttpWebResponse respSpd = (HttpWebResponse)reqSpd.GetResponse())
            using (StreamReader rSpd = new StreamReader(respSpd.GetResponseStream()))
            {
                Console.WriteLine("\nMANUAL COOKIE SPEED RESPONSE:\n" + rSpd.ReadToEnd());
            }
        }
    }

    private static string ComputePasswordHash(string rawPassword)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(rawPassword);
        string base64 = Convert.ToBase64String(bytes);

        using (SHA256 sha = SHA256.Create())
        {
            byte[] hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(base64));
            StringBuilder sb = new StringBuilder();
            foreach (byte b in hashBytes) sb.Append(b.ToString("X2"));
            return sb.ToString();
        }
    }
}
