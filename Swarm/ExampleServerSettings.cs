using System;
using System.Collections.Generic;
using System.Text;
using DataStructures;

namespace Swarm
{
    public class SwarmSettings : ServerSettingsInterface
    {
        private string serverName;

        public string ServerName
        {
            get { return serverName; }
            set { serverName = value; }
        }
    }
}
