using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace ModTogetherUniversal.Services
{
    public class UpnpService
    {
        private static UpnpService? _instance;
        public static UpnpService Instance => _instance ??= new UpnpService();

        private string? _serviceUrl;
        private string? _serviceType;

        public async Task<bool> TryCreatePortMappingAsync(int port, string description = "ModTogether P2P Host")
        {
            try
            {
                if (string.IsNullOrEmpty(_serviceUrl))
                {
                    await DiscoverRouterAsync();
                }

                if (string.IsNullOrEmpty(_serviceUrl) || string.IsNullOrEmpty(_serviceType))
                {
                    return false;
                }

                string localIp = GetLocalIpAddress();
                string soapBody = $@"<?xml version=""1.0""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" s:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
  <s:Body>
    <u:AddPortMapping xmlns:u=""{_serviceType}"">
      <NewRemoteHost></NewRemoteHost>
      <NewExternalPort>{port}</NewExternalPort>
      <NewProtocol>TCP</NewProtocol>
      <NewInternalPort>{port}</NewInternalPort>
      <NewInternalClient>{localIp}</NewInternalClient>
      <NewEnabled>1</NewEnabled>
      <NewPortMappingDescription>{description}</NewPortMappingDescription>
      <NewLeaseDuration>0</NewLeaseDuration>
    </u:AddPortMapping>
  </s:Body>
</s:Envelope>";

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                var request = new HttpRequestMessage(HttpMethod.Post, _serviceUrl);
                request.Headers.Add("SOAPAction", $"\"{_serviceType}#AddPortMapping\"");
                request.Content = new StringContent(soapBody, Encoding.UTF8, "text/xml");

                var response = await client.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeletePortMappingAsync(int port)
        {
            try
            {
                if (string.IsNullOrEmpty(_serviceUrl) || string.IsNullOrEmpty(_serviceType)) return false;

                string soapBody = $@"<?xml version=""1.0""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" s:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
  <s:Body>
    <u:DeletePortMapping xmlns:u=""{_serviceType}"">
      <NewRemoteHost></NewRemoteHost>
      <NewExternalPort>{port}</NewExternalPort>
      <NewProtocol>TCP</NewProtocol>
    </u:DeletePortMapping>
  </s:Body>
</s:Envelope>";

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(3);
                var request = new HttpRequestMessage(HttpMethod.Post, _serviceUrl);
                request.Headers.Add("SOAPAction", $"\"{_serviceType}#DeletePortMapping\"");
                request.Content = new StringContent(soapBody, Encoding.UTF8, "text/xml");

                var response = await client.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task DiscoverRouterAsync()
        {
            string req = "M-SEARCH * HTTP/1.1\r\n" +
                         "HOST: 239.255.255.250:1900\r\n" +
                         "ST: urn:schemas-upnp-org:device:InternetGatewayDevice:1\r\n" +
                         "MAN: \"ssdp:discover\"\r\n" +
                         "MX: 3\r\n\r\n";

            byte[] data = Encoding.ASCII.GetBytes(req);
            using var udp = new UdpClient();
            udp.Client.ReceiveTimeout = 3000;
            var endPoint = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);

            try
            {
                await udp.SendAsync(data, data.Length, endPoint);
                var result = await Task.Run(() => udp.Receive(ref endPoint));
                string resp = Encoding.ASCII.GetString(result);

                string location = "";
                foreach (var line in resp.Split("\r\n"))
                {
                    if (line.StartsWith("LOCATION:", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("Location:", StringComparison.OrdinalIgnoreCase))
                    {
                        location = line.Substring(line.IndexOf(':') + 1).Trim();
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(location))
                {
                    await ParseXmlDescriptionAsync(location);
                }
            }
            catch { }
        }

        private async Task ParseXmlDescriptionAsync(string locationUrl)
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            string xml = await client.GetStringAsync(locationUrl);

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("u", "urn:schemas-upnp-org:device-1-0");

            var serviceList = doc.SelectNodes("//u:service", nsmgr);
            if (serviceList == null) return;

            foreach (XmlNode service in serviceList)
            {
                string? serviceType = service.SelectSingleNode("u:serviceType", nsmgr)?.InnerText;
                string? controlUrl = service.SelectSingleNode("u:controlURL", nsmgr)?.InnerText;

                if (!string.IsNullOrEmpty(serviceType) && !string.IsNullOrEmpty(controlUrl))
                {
                    if (serviceType.Contains("WANIPConnection") || serviceType.Contains("WANPPPConnection"))
                    {
                        _serviceType = serviceType;
                        if (controlUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            controlUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            _serviceUrl = controlUrl;
                        }
                        else
                        {
                            Uri baseUri = new Uri(locationUrl);
                            _serviceUrl = new Uri(baseUri, controlUrl).ToString();
                        }
                        break;
                    }
                }
            }
        }

        private string GetLocalIpAddress()
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            try
            {
                socket.Connect("8.8.8.8", 65530);
                var endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint?.Address.ToString() ?? "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }
    }
}
