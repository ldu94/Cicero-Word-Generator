using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DataStructures;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using Renci.SshNet;

namespace Poseidon
{
    public partial class PoseidonForm : Form
    {
        private ExampleServer server;


        public PoseidonForm() : this(new ExampleServerSettings())
        {
        }

        public PoseidonForm(ExampleServerSettings settings) {
            InitializeComponent();
            
            this.server = new ExampleServer(this, settings);
            this.server.messageLog += addMessageLogText;
            this.server.serverSettings.SOCList = new List<SOCServer>();
            this.server.serverSettings.SOCList.Add(new SOCServer());
 

            if (File.Exists("./PoseidonSettings.psf"))
            {
                server.serverSettings = DeSerializeObject<ExampleServerSettings>("./PoseidonSettings.psf");
                MessageBox.Show("Settings were loaded from the file 'PoseidonSettings.psf in the directory with the Poseidon executable.");
            }
            else
            {
                MessageBox.Show("Settings were not automatically loaded. Save or place a settings file named 'PoseidonSettings.psf' in the directory containing the Poseidon executable in order to enable automatic loading of settings.");
            }

            this.propertyGrid1.SelectedObject = server.serverSettings;

        }

        private void connectButton_Click(object sender, EventArgs e)
        {
            connectButton.Enabled = false;
            server.openConnection();

        }


        public void reenableConnectButton()
        {
            Action enableConnectButton = () => this.connectButton.Enabled = true;
            BeginInvoke(enableConnectButton);
        }



        public void addMessageLogText(object sender, MessageEvent e)
        {
            Action addText = () => this.textBox1.AppendText(e.MyTime.ToString() + " " + sender.ToString() + ": " + e.ToString() + "\r\n");
            BeginInvoke(addText);
        }

        private void propertyGrid1_Click(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {



            foreach (SOCServer NIOS in server.serverSettings.SOCList)
            {

                //establish SSH connection
                NIOS.Status = "Attempting";

                try { NIOS.Sock.Dispose(); }
                catch { }

                using (var client = new SshClient(NIOS.IPAddress,22, "root", "w0lfg4ng"))
                {
                    try
                    {
                        client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(0.5);
                        client.Connect();
                        client.RunCommand("cd /home/root");
                        client.RunCommand("rm foo.*");
                        client.RunCommand("killall -9 fifo_test");
                        client.RunCommand("nohup /home/root/fifo_test " + NIOS.Port.ToString() + " > foo.out 2> foo.err < /dev/null &");
                        client.Disconnect();
                        client.Dispose();
                    }
                    catch
                    {
                        try
                        {
                            NIOS.Port++;
                            client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(0.5);
                            client.Connect();
                            client.RunCommand("cd /home/root");
                            client.RunCommand("rm foo.*");
                            client.RunCommand("killall -9 fifo_test");
                            client.RunCommand("nohup /home/root/fifo_test " + NIOS.Port.ToString() + " > foo.out 2> foo.err < /dev/null &");
                            client.Disconnect();
                            client.Dispose();
                            
                        }
                        catch
                        {
                            MessageBox.Show("Failed to start socket server over SSH on " + NIOS.Name);
                            NIOS.Status = "Failed";
                        }

                    }
                }


                if (NIOS.Status != "Failed")
                {
                    //Establish socket connection details
                    IPAddress address = IPAddress.Parse(NIOS.IPAddress);
                    IPEndPoint remoteEP = new IPEndPoint(address, NIOS.Port);
                    // Create a TCP/IP  socket.
                    Socket mySock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    // Disable the Nagle Algorithm for this tcp socket.
                    mySock.NoDelay = true;
                    //Connect
                    try
                    {
                        mySock.Connect(remoteEP);
                        NIOS.Sock = mySock;
                    }
                    catch
                    {
                        MessageBox.Show("Failed to establish socket connection to " + NIOS.Name + ". If you suspect an orphaned socket, power cycle the SOC.");
                        NIOS.Status = "Failed";
                    }
                }
            }
            foreach (SOCServer NIOS in server.serverSettings.SOCList)
            {
               
                    byte[] tcpBuffer = new byte[6];
                    if (NIOS.Sock != null && NIOS.Status != "Failed")
                    {
                        int bytesReceived = NIOS.Sock.Receive(tcpBuffer);
                        System.Text.UTF8Encoding enc = new System.Text.UTF8Encoding();
                        string s = enc.GetString(tcpBuffer);
                        if (s == "ready1")
                        {
                            NIOS.Status = "Connected";
                        }
                        else
                        {
                            NIOS.Status = "Disconnected";
                        }
                    }
                
            }

            saveToolStripMenuItem.Enabled = false;
            loadToolStripMenuItem.Enabled = false;

           
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            //Clear the table
            dataGridView1.Rows.Clear();
            //Populate the table
            foreach (SOCServer NIOS in server.serverSettings.SOCList)
            {
                dataGridView1.Rows.Add(NIOS.Name,NIOS.IPAddress,NIOS.Port,NIOS.Status);
            }
            //Deselect the first cell
            dataGridView1.CurrentCell = null;

            //Update the server's list of hardware channels
            server.MyHardwareChannels = new List<HardwareChannel>();
            foreach (SOCServer NIOS in server.serverSettings.SOCList)
            {
                HardwareChannel temp = new HardwareChannel(server.serverSettings.ServerName,NIOS.Name,NIOS.Name,"",HardwareChannel.HardwareConstants.ChannelTypes.analog);
                server.MyHardwareChannels.Add(temp);
            }
        }

        private void propertyGrid1_Click_1(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            int counter = 0;
            int deletedCounter = 0;
            List<string> IPlist = new List<string>();
            List<SOCServer> temp = new List<SOCServer>();

            foreach (SOCServer SOC in server.serverSettings.SOCList)
            {
                SOC.Name = "SOC" + counter++.ToString();
                if (IPlist.Contains(SOC.IPAddress))
                {
                    deletedCounter++;
                    
                }
                else
                {
                    IPlist.Add(SOC.IPAddress);
                    temp.Add(SOC);
                }
                
            }
            server.serverSettings.SOCList = temp;
            MessageBox.Show(deletedCounter.ToString() + " channels deleted due to IP address redundancy");
        }

        public void SerializeObject<T>(T serializableObject, string fileName)
        {
            if (serializableObject == null) { return; }

            try
            {
                XmlDocument xmlDocument = new XmlDocument();
                XmlSerializer serializer = new XmlSerializer(serializableObject.GetType());
                using (MemoryStream stream = new MemoryStream())
                {
                    serializer.Serialize(stream, serializableObject);
                    stream.Position = 0;
                    xmlDocument.Load(stream);
                    xmlDocument.Save(fileName);
                    stream.Close();
                }
            }
            catch (Exception ex)
            {
                //Log exception here
            }
        }

        /// <summary>
        /// Deserializes an xml file into an object list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public T DeSerializeObject<T>(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) { return default(T); }

            T objectOut = default(T);

            try
            {
                string attributeXml = string.Empty;

                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.Load(fileName);
                string xmlString = xmlDocument.OuterXml;

                using (StringReader read = new StringReader(xmlString))
                {
                    Type outType = typeof(T);

                    XmlSerializer serializer = new XmlSerializer(outType);
                    using (XmlReader reader = new XmlTextReader(read))
                    {
                        objectOut = (T)serializer.Deserialize(reader);
                        reader.Close();
                    }

                    read.Close();
                }
            }
            catch (Exception ex)
            {
                //Log exception here
            }

            return objectOut;
        }


        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Process input if the user clicked OK.
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                String savepath = saveFileDialog1.FileName;
                SerializeObject(server.serverSettings, savepath);
            }
        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Process input if the user clicked OK.
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                String loadpath = openFileDialog1.FileName;
                server.serverSettings = DeSerializeObject<ExampleServerSettings>(loadpath);
            }
            propertyGrid1.SelectedObject = server.serverSettings;

        }

        private void textBox1_TextChanged_1(object sender, EventArgs e)
        {

        }
    }
}