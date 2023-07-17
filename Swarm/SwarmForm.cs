using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DataStructures;
using System.IO.Ports;
using System.Threading;
using System.Text.RegularExpressions;

namespace Swarm
{
    public partial class SwarmForm : Form
    {
        private Swarm server;

        private SerialPort _serialPort = new SerialPort();
       
     

        public SwarmForm() : this(new SwarmSettings())
        {
            

        }

        public SwarmForm(SwarmSettings settings) {
            InitializeComponent();
            _serialPort.PortName = "COM10";//Set your board COM
            _serialPort.BaudRate = 9600;
            _serialPort.Open();
            timer1.Enabled = true;
            this.server = new Swarm(this, settings);
            this.server.messageLog += addMessageLogText;
            this.propertyGrid1.SelectedObject = settings;

            

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

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void tableLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void timer1_Tick(object sender, EventArgs e)
        {
          Regex rx = new Regex(@"(\d){1,4}",
          RegexOptions.Compiled | RegexOptions.IgnoreCase);
            
            string text = _serialPort.ReadLine();

            MatchCollection matches = rx.Matches(text);
                
            if(matches.Count > 1)
            {
                string humiditynumber = matches[1].ToString();
                double humidityvalue = Convert.ToInt32(humiditynumber) / 1024.0 * 5.0*200.0/7.0 - 120.0/7.0; 
                label1.Text = "Humidity is "+humidityvalue.ToString().Substring(0,6)+"%";
            }
          
        }
    }
}