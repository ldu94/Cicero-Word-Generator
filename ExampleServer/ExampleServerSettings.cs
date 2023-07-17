using System;
using System.Collections.Generic;
using System.Text;
using DataStructures;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.ComponentModel;
using Renci.SshNet;


namespace Poseidon
{
    [Serializable, TypeConverter(typeof(ExpandableObjectConverter))]
    public class ExampleServerSettings : ServerSettingsInterface
    {
        private string serverName;
         [Description("Sets the name of this Poseidon server"), Category("General")]
        public string ServerName
        {
            get { return serverName; }
            set { serverName = value; }
        }

        private int packetSize;
        [Description("OBSOLETE // Sets the number of bytes to send to a SOC server in each packet"), Category("Ethernet")]
        public int PacketSize
        {
            get { return packetSize; }
            set { packetSize = value; }
        }

        private List<SOCServer> socList;
        [Description("List of SOC servers. Add channels here!"), Category("Ethernet")]
        public List<SOCServer> SOCList
        {
            get { return socList; }
            set { socList = value; }
        }




    }
}
