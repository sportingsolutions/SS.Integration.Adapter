using System;
using SS.Integration.Adapter.Interface;
using SdkClients = SportingSolutions.Udapi.Sdk.Clients;

namespace SS.Integration.Adapter.Configuration
{
    internal class UdapiConfiguration : SdkClients.IConfiguration
    {
        private readonly ISettings _settings;

        public string ContentType => SdkClients.Configuration.Instance.ContentType;
        public int Timeout => SdkClients.Configuration.Instance.Timeout;
        public bool Compression => SdkClients.Configuration.Instance.Compression;
        public bool UseEchos => SdkClients.Configuration.Instance.UseEchos;
        public int MissedEchos => SdkClients.Configuration.Instance.MissedEchos;
        public int EchoWaitInterval => _settings.EchoInterval;
        public bool VerboseLogging => SdkClients.Configuration.Instance.VerboseLogging;
        public ushort AMQPMissedHeartbeat => SdkClients.Configuration.Instance.AMQPMissedHeartbeat;
        public bool AutoReconnect => _settings.AutoReconnect;
        public bool UseStreamControllerMailbox => _settings.UseStreamControllerMailbox;
        public int DisconnectionDelay => SdkClients.Configuration.Instance.DisconnectionDelay;

        public UdapiConfiguration(ISettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }
    }
}
