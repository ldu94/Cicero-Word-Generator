using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Poseidon
{
    public class SOCServer
    {

        private string ipAddress;
        public string IPAddress
        {
            get { return ipAddress;  }
            set { ipAddress = value; }
        }
        private int port;
        public int Port
        {
            get { return port; }
            set { port = value; }
        }
        private string name;
        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        private string status;
        public string Status
        {
            get { return status; }
            set { status = value; }
            
        }

     
        private Socket sock;
        [XmlIgnore]
        public Socket Sock
        {
            get { return sock; }
            set { sock = value; }
        }


        public SOCServer()
        {
            name = "SOC";
            IPAddress = "192.168.2.1";
            status = "Disconnected";
            port = 2300;
        }
    }
}
