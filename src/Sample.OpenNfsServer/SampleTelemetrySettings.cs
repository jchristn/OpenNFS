namespace Sample.OpenNfsServer
{
    using System;

    /// <summary>
    /// Telemetry settings for the sample server, modeled on Pneuma's <c>TelemetrySettings</c>. Bound from the
    /// <c>telemetry</c> object of the JSON configuration file and overridable from the command line.
    /// </summary>
    /// <remarks>
    /// Loopback defaults use <c>127.0.0.1</c> rather than <c>localhost</c> so Windows hosts do not stall resolving
    /// IPv6 <c>::1</c> first. This type is not thread safe; it is read once at startup.
    /// </remarks>
    internal sealed class SampleTelemetrySettings
    {
        private string _ServiceName = "opennfs-sample-server";
        private string _OtlpEndpoint = "http://127.0.0.1:4317";
        private string _OtlpProtocol = "grpc";
        private string _PrometheusHostname = "127.0.0.1";
        private int _PrometheusPort = 9464;
        private double _SamplingRatio = 1.0;

        /// <summary>
        /// Gets or sets a value indicating whether the Radiant telemetry host starts at all. Default <c>true</c>.
        /// When <c>false</c>, OpenNFS still emits through its meters and activity sources but nothing exports them.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the <c>service.name</c> resource attribute. Default <c>opennfs-sample-server</c>.
        /// </summary>
        public string ServiceName
        {
            get => _ServiceName;
            set => _ServiceName = string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("The telemetry service name must be non-empty.", nameof(value))
                : value;
        }

        /// <summary>
        /// Gets or sets the OTLP collector endpoint for traces and metrics. Default <c>http://127.0.0.1:4317</c>.
        /// Must be an absolute URI.
        /// </summary>
        public string OtlpEndpoint
        {
            get => _OtlpEndpoint;
            set => _OtlpEndpoint = Uri.TryCreate(value, UriKind.Absolute, out _)
                ? value
                : throw new ArgumentException("The OTLP endpoint must be an absolute URI.", nameof(value));
        }

        /// <summary>
        /// Gets or sets the OTLP protocol: <c>grpc</c> (default, port 4317) or <c>http/protobuf</c> (port 4318).
        /// </summary>
        public string OtlpProtocol
        {
            get => _OtlpProtocol;
            set => _OtlpProtocol = string.Equals(value, "grpc", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "http/protobuf", StringComparison.OrdinalIgnoreCase)
                    ? value
                    : throw new ArgumentException("The OTLP protocol must be 'grpc' or 'http/protobuf'.", nameof(value));
        }

        /// <summary>
        /// Gets or sets a value indicating whether the in-process Prometheus scrape endpoint is served. Default
        /// <c>false</c>, because only one process can bind a given port and the sample is often launched several
        /// times side by side; the Docker stack enables it.
        /// </summary>
        public bool PrometheusEnabled { get; set; }

        /// <summary>
        /// Gets or sets the hostname the Prometheus endpoint binds. Default <c>127.0.0.1</c>. Use <c>+</c> or
        /// <c>*</c> to bind every interface (for example inside a container).
        /// </summary>
        public string PrometheusHostname
        {
            get => _PrometheusHostname;
            set => _PrometheusHostname = string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("The Prometheus hostname must be non-empty.", nameof(value))
                : value;
        }

        /// <summary>
        /// Gets or sets the Prometheus endpoint port. Default 9464. Minimum 1, maximum 65535.
        /// </summary>
        public int PrometheusPort
        {
            get => _PrometheusPort;
            set => _PrometheusPort = value < 1 || value > 65535
                ? throw new ArgumentOutOfRangeException(nameof(value), value, "The Prometheus port must be between 1 and 65535.")
                : value;
        }

        /// <summary>
        /// Gets or sets the head-based trace sampling ratio. Default 1.0 (sample every root trace). Minimum 0.0,
        /// maximum 1.0.
        /// </summary>
        public double SamplingRatio
        {
            get => _SamplingRatio;
            set => _SamplingRatio = value < 0.0 || value > 1.0
                ? throw new ArgumentOutOfRangeException(nameof(value), value, "The sampling ratio must be between 0.0 and 1.0.")
                : value;
        }
    }
}
