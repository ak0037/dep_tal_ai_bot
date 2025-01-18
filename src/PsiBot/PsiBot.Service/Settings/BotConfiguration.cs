
using PsiBot.Model.Constants;
using Microsoft.Skype.Bots.Media;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace PsiBot.Service.Settings
{
    public class BotConfiguration
    {
        /// <summary>
        /// Gets or sets the name of the bot.
        /// </summary>
        /// <value>The name of the bot.</value>
        public string BotName { get; set; }

        /// <summary>
        /// Gets or sets the name of the service DNS.
        /// </summary>
        /// <value>The name of the service DNS.</value>
        public string ServiceDnsName { get; set; }

        /// <summary>
        /// Gets or sets the service cname.
        /// </summary>
        /// <value>The service cname.</value>
        public string ServiceCname { get; set; }

        /// <summary>
        /// Gets or sets the certificate thumbprint.
        /// </summary>
        /// <value>The certificate thumbprint.</value>
        public string CertificateThumbprint { get; set; }

        /// <summary>
        /// Gets or sets the call control listening urls.
        /// </summary>
        /// <value>The call control listening urls.</value>
        public IEnumerable<string> CallControlListeningUrls { get; set; }

        /// <summary>
        /// Gets or sets the call control base URL.
        /// </summary>
        /// <value>The call control base URL.</value>
        public Uri CallControlBaseUrl { get; set; }

        /// <summary>
        /// Gets or sets the place call endpoint URL.
        /// </summary>
        /// <value>The place call endpoint URL.</value>
        public Uri PlaceCallEndpointUrl { get; set; }

        /// <summary>
        /// Gets the media platform settings.
        /// </summary>
        /// <value>The media platform settings.</value>
        public MediaPlatformSettings MediaPlatformSettings { get; private set; }

        /// <summary>
        /// Gets or sets the aad application identifier.
        /// </summary>
        /// <value>The aad application identifier.</value>
        public string AadAppId { get; set; }

        /// <summary>
        /// Gets or sets the aad application secret.
        /// </summary>
        /// <value>The aad application secret.</value>
        public string AadAppSecret { get; set; }

        /// <summary>
        /// Gets or sets the instance public port.
        /// </summary>
        /// <value>The instance public port.</value>
        public int InstancePublicPort { get; set; }

        /// <summary>
        /// Gets or sets the instance internal port.
        /// </summary>
        /// <value>The instance internal port.</value>
        public int InstanceInternalPort { get; set; }

        /// <summary>
        /// Gets or sets the call signaling port.
        /// </summary>
        /// <value>The call signaling port.</value>
        public int CallSignalingPort { get; set; }

        /// <summary>
        /// Gets or sets the media folder.
        /// </summary>
        /// <value>The media folder.</value>
        public string MediaServiceFQDN { get; set; }

        // New properties for external ports
        public int BotInstanceExternalPort { get; set; } = 443; // Default to 443
        public int MediaInstanceExternalPort { get; set; } = 443; // Default to 443

        public bool UseLocalDevSettings { get; set; }

        // /// <summary>
        // /// Gets or sets whether to use local development settings.
        // /// </summary>
        // public bool UseLocalDevSettings { get; set; }

        /// <summary>
        /// Gets or sets the directory location for the Psi store.
        /// </summary>
        public string PsiStoreDirectory { get; set; }

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        public void Initialize()
        {
            if (string.IsNullOrWhiteSpace(ServiceCname))
            {
                ServiceCname = ServiceDnsName;
            }

            X509Certificate2 defaultCertificate = this.GetCertificateFromStore();

            List<string> controlListenUris = new List<string>();
            var baseDomain = "+";
            int publicPort;
            if (string.IsNullOrEmpty(ServiceCname))
            {
                throw new InvalidOperationException("ServiceCname is not set in the configuration.");
            }

            Console.WriteLine($"Initializing with UseLocalDevSettings: {UseLocalDevSettings}");

            if (UseLocalDevSettings)
            {
                // Local development settings
                this.CallControlBaseUrl = new Uri($"https://{this.ServiceCname}/{HttpRouteConstants.CallSignalingRoutePrefix}");
                publicPort = InstancePublicPort;
                Console.WriteLine("Local Development Settings:");
                Console.WriteLine($"  CallControlBaseUrl: {this.CallControlBaseUrl}");
                Console.WriteLine($"  publicPort: {publicPort}");
            }
            else
            {
                // Production settings
                this.CallControlBaseUrl = new Uri($"https://{this.ServiceCname}:{this.BotInstanceExternalPort}/{HttpRouteConstants.CallSignalingRoutePrefix}");
                publicPort = MediaInstanceExternalPort;
                Console.WriteLine("Production Settings:");
                Console.WriteLine($"  CallControlBaseUrl: {this.CallControlBaseUrl}");
                Console.WriteLine($"  publicPort: {publicPort}");
            }

            // Keep internal listening URLs as they were
            controlListenUris.Add($"https://{baseDomain}:{CallSignalingPort}/");
            controlListenUris.Add($"http://{baseDomain}:{CallSignalingPort + 1}/");

            this.CallControlListeningUrls = controlListenUris;

            this.MediaPlatformSettings = new MediaPlatformSettings()
            {
                MediaPlatformInstanceSettings = new MediaPlatformInstanceSettings()
                {
                    CertificateThumbprint = defaultCertificate.Thumbprint,
                    InstanceInternalPort = InstanceInternalPort,
                    InstancePublicIPAddress = IPAddress.Any,
                    InstancePublicPort = publicPort,
                    ServiceFqdn = this.MediaServiceFQDN
                },
                ApplicationId = this.AadAppId,
            };

            Console.WriteLine("MediaPlatformSettings:");
            Console.WriteLine($"  InstancePublicPort: {this.MediaPlatformSettings.MediaPlatformInstanceSettings.InstancePublicPort}");
            Console.WriteLine($"  ServiceFqdn: {this.MediaPlatformSettings.MediaPlatformInstanceSettings.ServiceFqdn}");
        }

        /// <summary>
        /// Helper to search the certificate store by its thumbprint.
        /// </summary>
        /// <returns>Certificate if found.</returns>
        /// <exception cref="Exception">No certificate with thumbprint {CertificateThumbprint} was found in the machine store.</exception>
        public X509Certificate2 GetCertificateFromStore()
        {

            X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            try
            {
                X509Certificate2Collection certs = store.Certificates.Find(X509FindType.FindByThumbprint, CertificateThumbprint, validOnly: false);

                if (certs.Count != 1)
                {
                    throw new Exception($"No certificate with thumbprint {CertificateThumbprint} was found in the machine store.");
                }

                return certs[0];
            }
            finally
            {
                store.Close();
            }
        }

        private IPAddress GetInstancePublicIpAddress(string publicFqdn)
        {
            string instanceHostName = publicFqdn;
            IPAddress[] instanceAddresses = Dns.GetHostEntry(instanceHostName).AddressList;
            if (instanceAddresses.Length == 0)
            {
                throw new InvalidOperationException("Could not resolve the PIP hostname. Please make sure that PIP is properly configured for the service");
            }

            return instanceAddresses[0];
        }
    }
}
