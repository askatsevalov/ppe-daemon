using System;

namespace daemon
{
    public class DaemonConfig
    {
        public string ReaderHost { get; set; }
        public int ReaderPort { get; set; }
        public string GatewayAddress { get; set; }
        public string ControlPointId { get; set; }
        public int TxLevel { get; set; }
    }
}