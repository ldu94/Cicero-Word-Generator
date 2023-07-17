using System;
using System.Collections.Generic;
using System.Text;
using DataStructures;
using System.Threading;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Diagnostics;
using Renci.SshNet;



namespace Poseidon
{
    class ExampleServer : ServerCommunicator
    {

        /// <summary>
        /// To be called to add messages to the server message log. Eventually may support logging to a file
        /// as well as to the screen.
        /// </summary>
        public EventHandler<MessageEvent> messageLog;
        private PoseidonForm exampleServerForm;
        public ExampleServerSettings serverSettings;

        //SW
        public System.Diagnostics.Stopwatch sw1 = new Stopwatch();

        //Stuff copied over from Atticus:
        private SequenceData sequence;
        private SettingsData settings;
        private object remoteLockObj = new object();
        private List<HardwareChannel> myHardwareChannels;
        public List<HardwareChannel> MyHardwareChannels
        {
            get { return myHardwareChannels; }
            set { myHardwareChannels = value; }
        }
        private Dictionary<int, HardwareChannel> usedAnalogChannels;
        public ExampleServer(PoseidonForm form, ExampleServerSettings serverSettings)
        {
            this.exampleServerForm = form;
            this.serverSettings = serverSettings;
        }
        private bool clientFinishedRun;


        #region Implementation of ServerCommunicator
        public override bool armTasks(UInt32 clockID)
        {
            return true;
        }

        public override BufferGenerationStatus generateBuffers(int listIterationNumber)
        {
            lock (remoteLockObj)
            {
                clientFinishedRun = false;

                    messageLog(this, new MessageEvent("Generating buffers."));
                    if (settings == null)
                    {
                        messageLog(this, new MessageEvent("Unable to generate buffers. Null settings."));
                        return BufferGenerationStatus.Failed_Settings_Null;
                    }
                    if (sequence == null)
                    {
                        messageLog(this, new MessageEvent("Unable to generate buffers. Null sequence."));
                        return BufferGenerationStatus.Failed_Sequence_Null;
                    }
                    // This is redundant.
                    sequence.ListIterationNumber = listIterationNumber;

                    //No multithreading

                    foreach (SOCServer SOC in serverSettings.SOCList)
                    {
                        if (!SOC.Name.Contains("SOC"))
                        {
                            messageLog(this, new MessageEvent("******* You are using a NI device named " + SOC + ". This does not follow the convention of naming your devices Dev1, Dev2, Dev3, etc. Unpredictable results are possible! Not recommended! *******"));
                        }
                        generateBufferOnDevice(SOC);
                    }
                    // Try to clean up as much memory as possible so that there wont be any garbage collection
                    // during the run. Suspect that GCs during the run may be the cause of sporadic buffer underruns.
                    // Note: This is likely wrong. Most of these buffer underruns were eventually fixed with the 
                    // data transfer mechanism tweaks described in the user manual. However, no harm in doing some
                    // GC here.
                    System.GC.Collect();
                    System.GC.Collect();
                    System.GC.Collect();
                    System.GC.WaitForPendingFinalizers();

                    messageLog(this, new MessageEvent("Buffers generated succesfully."));


                    return BufferGenerationStatus.Success;
            }
        }

        public override bool generateTrigger()
        {
            return true;
        }

        public override List<HardwareChannel> getHardwareChannels()
        {
            List<HardwareChannel> ListForCicero = new List<HardwareChannel>();
            foreach(SOCServer SOC in serverSettings.SOCList)
            {
                HardwareChannel temp = new HardwareChannel(serverSettings.ServerName,SOC.Name,SOC.Name,HardwareChannel.HardwareConstants.ChannelTypes.analog);
                ListForCicero.Add(temp);
            }

            return ListForCicero;
        }

        public override string getServerName()
        {
            return serverSettings.ServerName;
        }

        public override ServerSettingsInterface getServerSettings()
        {
            return this.serverSettings;
        }

        public override void nextRunTimeStamp(DateTime timeStamp)
        {

        }

        public override bool outputGPIBGroup(GPIBGroup gpibGroup, SettingsData settings)
        {
            return true;
        }

        public override bool outputRS232Group(RS232Group rs232Group, SettingsData settings)
        {
            return true;
        }

        public override bool outputSingleTimestep(SettingsData settings, SingleOutputFrame output)
        {
            lock (remoteLockObj)
            {
                setSettings(settings);

                messageLog(this, new MessageEvent("Outputting timestep"));
               

                //No multithreading

                foreach (SOCServer SOC in serverSettings.SOCList)
                {
                    if (!SOC.Name.Contains("SOC"))
                    {
                        messageLog(this, new MessageEvent("******* You are using a NI device named " + SOC + ". This does not follow the convention of naming your devices Dev1, Dev2, Dev3, etc. Unpredictable results are possible! Not recommended! *******"));
                    }
                    generateSingleBufferOnDevice(SOC,output);
                }
             
                messageLog(this, new MessageEvent("Timestep outputted successfully."));

                return true;
           }
   
        }

        public override bool ping()
        {
            return true;
        }

        public override bool runSuccess()
        {
            byte[] tempBytes = BitConverter.GetBytes(66);
            foreach (SOCServer SOC in serverSettings.SOCList)
            {
                try
                {
                    SOC.Sock.Send(tempBytes.Skip(0).Take(1).ToArray());
                }
                catch
                {
                    messageLog(this, new MessageEvent("Stop failed"));
                }
            }
            return true;
        }

        public override bool setSequence(SequenceData sequence)
        {
            lock (remoteLockObj)
            {
                messageLog(this, new MessageEvent("Received sequence."));
                this.sequence = sequence;
                return true;
            }
        }

        public override bool setSettings(SettingsData settings)
        {
            lock (remoteLockObj)
            {
                try
                {
                    messageLog(this, new MessageEvent("Received settings."));
                    this.settings = settings;

                    findMyChannels();
                    return true;
                }
                catch (Exception e)
                {
                    messageLog(this, new MessageEvent("Caught exception while attempting to verify settings. " + e.Message + e.StackTrace));
                    return false;
                }
            }
        }

        public override void stop()
        {

                byte[] tempBytes = BitConverter.GetBytes(66);
                foreach (SOCServer SOC in serverSettings.SOCList)
                {
                     try
                     {
                         SOC.Sock.Send(tempBytes.Skip(0).Take(1).ToArray());
                     }
                     catch
                     {
                         messageLog(this, new MessageEvent("Stop failed"));
                     }
                }

        }


        //----------------------------------------------------
        //Begin new methods to be used by the database server
        //----------------------------------------------------
        public override bool checkIfCiceroCanRun()
        {
            return true;
        }

        public override bool waitForDatabaseUpdates(List<Variable> Variables)
        {
            return true;
        }

        public override bool writeVariablesIntoDatabase(List<Variable> Variables, string SequenceName, string SequenceDescription)
        {
            return true;
        }

        public override bool moveImageDataFromCacheToDatabase()
        {
            return true;
        }

        //----------------------------------------------------
        //End new methods to be used by the database server
        //----------------------------------------------------

        #endregion


        #region Methods called by MainVirgilForm

        public void openConnection()
        {
            messageLog(this, new MessageEvent("Attempting to open connection."));
            Thread thread = new Thread(new ThreadStart(startMarshalProc));
            thread.Start();
        }

        #endregion

        /// <summary>
        /// Determines the logical channel IDs and Hardware Channel objects for all channels in settings data
        /// which reside on this server.
        /// </summary>
        private void findMyChannels()
        {
            this.usedAnalogChannels = new Dictionary<int, HardwareChannel>();

            LogicalChannel errorChan;
            //analog
            errorChan = findChannels(settings.logicalChannelManager.ChannelCollections[HardwareChannel.HardwareConstants.ChannelTypes.analog],
                usedAnalogChannels);

            if (errorChan != null)
            {
                throw new Exception("Invalid settings data. Analog logical channel named " + errorChan.Name + " is bound to a hardware channel on this server which is disabled or does not exist.");
            }

        }

        /// <summary>
        /// Helper function for findMyChannels. If a channel is found that is not contained in the current list of exportable channels,
        /// then that channel is returned as an error.
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="channelMap"></param>
        /// From atticus:
        private LogicalChannel findChannels(ChannelCollection collection, Dictionary<int, HardwareChannel> channelMap)
        {
            foreach (int id in collection.Channels.Keys)
            {
                LogicalChannel logical = collection.Channels[id];
                if (logical.HardwareChannel != null)
                {
                    if (!logical.HardwareChannel.isUnAssigned)
                    {
                        if (logical.HardwareChannel.ServerName == this.serverSettings.ServerName)
                        {
                            if (this.MyHardwareChannels.Contains(logical.HardwareChannel))
                            {
                                channelMap.Add(id, logical.HardwareChannel);
                            }
                            else
                            {
                                return logical;
                            }
                        }
                    }
                }
            }

            return null;
        }
        private void generateBufferOnDevice(SOCServer SOC)
        {

            if (SOC.Status == "Connected")
            {

                messageLog(this, new MessageEvent("Generating buffer for " + SOC.Name));
                createBuffer(SOC, sequence, settings, usedAnalogChannels, serverSettings);

            }

            else
            {
                messageLog(this, new MessageEvent("Skipped buffer generation for disconnected device " + SOC.Name));
            }

        }

        private void generateSingleBufferOnDevice(SOCServer SOC, SingleOutputFrame output)
        {

            if (SOC.Status == "Connected")
            {

                messageLog(this, new MessageEvent("Outputting timstep for " + SOC.Name));
                createSingleBuffer(SOC, sequence, settings, usedAnalogChannels, serverSettings,output);

            }

            else
            {
                messageLog(this, new MessageEvent("Skipped timestep output for disconnected device " + SOC.Name));
            }

        }
        public void createBuffer(SOCServer SOC, SequenceData sequence,
           SettingsData settings, Dictionary<int, HardwareChannel> usedAnalogChannels,
           ExampleServerSettings serverSettings)
        {

            // figure out which of the analog and digital channels belong on this device. Add them here and index by 
            // logical ID#
            Dictionary<int, HardwareChannel> analogsUnsorted = getChannelsOnDevice(usedAnalogChannels, SOC.Name);


            // sort the lists by ID
            List<int> analogIDs = new List<int>();
            List<HardwareChannel> analogs = new List<HardwareChannel>();
            sortDicionaryByID(analogIDs, analogs, analogsUnsorted);

            //Bug fix 1/29/2019
            //This code was adapted from the code that generates buffers on multiple Channels on a single device. Now, we have the case that one device = one channel
            //Adding a check to make sure that the channel is used before generating a buffer. Without this fix, all SOCs added in Poseidon must be assigned in the sequence,
            //or else an exception is thrown
            if (analogIDs.Count > 0)
            {
                int analogID = analogIDs[0];

                double[] singleChannelBuffer;
                double timeStepSize = Common.getPeriodFromFrequency(1000000);

                TimestepTimebaseSegmentCollection timebaseSegments =
                    sequence.generateVariableTimebaseSegments(SequenceData.VariableTimebaseTypes.AnalogGroupControlledVariableFrequencyClock,
                                            timeStepSize);

                int nBaseSamples = timebaseSegments.nSegmentSamples();
                nBaseSamples++; // add one sample for the dwell sample at the end of the buffer
                                // for reasons that are utterly stupid and frustrating, the DAQmx libraries seem to prefer sample
                                // buffers with lengths that are a multiple of 4. (otherwise they, on occasion, depending on the parity of the 
                                // number of channels, throw exceptions complaining.
                                // thus we add a few filler samples at the end of the sequence which parrot back the last sample.
                int nFillerSamples = 4 - nBaseSamples % 4;
                if (nFillerSamples == 4)
                    nFillerSamples = 0;

                int nSamples = nBaseSamples + nFillerSamples;
                try
                {
                    singleChannelBuffer = new double[nSamples];
                }
                catch (Exception e)
                {
                    throw new Exception("Unable to allocate analog buffer for device " + SOC.Name + ". Reason: " + e.Message + "\n" + e.StackTrace);
                }

                if (settings.logicalChannelManager.Analogs[analogID].TogglingChannel)
                {
                    getAnalogTogglingBuffer(singleChannelBuffer);
                }
                else if (settings.logicalChannelManager.Analogs[analogID].overridden)
                {
                    for (int j = 0; j < singleChannelBuffer.Length; j++)
                    {
                        singleChannelBuffer[j] = settings.logicalChannelManager.Analogs[analogID].analogOverrideValue;
                    }
                }
                else
                {
                    sequence.computeAnalogBuffer(analogIDs[0], timeStepSize, singleChannelBuffer, timebaseSegments);
                }

                for (int j = nBaseSamples; j < nSamples; j++)
                {
                    singleChannelBuffer[j] = singleChannelBuffer[j - 1];
                }

                
                #region Sending Buffer to all NIOS's
                int STATE = 1; //obsolete
                bool notExited = true; //obsolete
                int remainingNumberOfBytes = 3 * nSamples; //obsolete
                int lastPacket = 0; //0 false, 1 true
                int packetCounter = 0; //obsolete
                int bytesReceived = 0;
                byte[] tcpBuffer = new byte[6];
                int packetsize = serverSettings.PacketSize; //obsolete
                int numberOfPackets = (int)Math.Ceiling((3.0 * nSamples) / (1.0 * packetsize)); //obsolete


                Socket mySock = SOC.Sock;
                #region NEW SOC CODE

                //Tell the SOC how many bytes are coming 
                byte[] tempBytes = BitConverter.GetBytes(nSamples * 3);
                mySock.Send(tempBytes.Skip(0).Take(4).ToArray());
                byte[] writeBuffer = new byte[nSamples * 3];
                //Send the buffer over the socket
                for (int j = 0; j < nSamples * 3; j += 3) //j counts BYTES
                {
                    double temp = singleChannelBuffer[j / 3] * (524287) / 10.0;
                    byte[] tempBytes1 = BitConverter.GetBytes((int)temp);
                    byte[] tempBytes2 = tempBytes1.Skip(0).Take(3).ToArray();
                    writeBuffer[j] = tempBytes2[0];
                    writeBuffer[j + 1] = tempBytes2[1];
                    writeBuffer[j + 2] = tempBytes2[2];

                }

                mySock.Send(writeBuffer);

                bytesReceived = mySock.Receive(tcpBuffer);

                messageLog(this, new MessageEvent(SOC.Name + "has reported receipt of the buffer."));
                #endregion
                

                #region OLD NIOS CODE
                /*
                    //-------------------------------
                    //BEGIN STATE MACHINE
                    //-------------------------------
                    while (notExited)
                    {
                        switch (STATE)
                        {
                            case 1:// See if "ready1" is in the buffer (every time except the first time)

                                if (mySock.Available != 6)
                                {
                                    messageLog(this, new MessageEvent(NIOS.Name + " did not report ready1. This is normal for first run."));
                                }
                                else
                                {
                                    bytesReceived = mySock.Receive(tcpBuffer);
                                    System.Text.UTF8Encoding enc = new System.Text.UTF8Encoding();
                                    string s = enc.GetString(tcpBuffer);
                                    if (s == "ready1")
                                    {
                                        messageLog(this, new MessageEvent(NIOS.Name + " reported ready1."));
                                    }
                                    else
                                    {
                                        notExited = false; //We must be out of sync
                                    }
                                }

                                STATE = 2;
                                //Tell the server, with a two-byte word, how many packets are coming;
                                mySock.Send(BitConverter.GetBytes(Convert.ToInt16(numberOfPackets)));
                                break;

                            case 2://Wait for "ready0"
                                if (mySock.Available < 6)
                                {
                                    //Do nothing
                                }
                                else
                                {
                                    bytesReceived = mySock.Receive(tcpBuffer);
                                    System.Text.UTF8Encoding enc = new System.Text.UTF8Encoding();
                                    string s = enc.GetString(tcpBuffer);
                                    if (s == "ready0")
                                    {
                                        messageLog(this, new MessageEvent(NIOS.Name + " has confirmed receipt of the number of packets it will receive."));
                                        STATE = 3;
                                    }
                                    else
                                    {
                                        notExited = false;
                                    }
                                }

                                break;

                            case 3://Report how many bytes are in the next packet

                                //First determine if there are least PACKETSIZE (defined in NIOS) bytes left
                                if (remainingNumberOfBytes >= packetsize) lastPacket = 0;
                                else lastPacket = 1;
                                //Now tell the server, with a two-byte word, how many bytes are coming
                                if (lastPacket == 0) mySock.Send(BitConverter.GetBytes(Convert.ToInt16(packetsize)));
                                else mySock.Send(BitConverter.GetBytes(Convert.ToInt16(remainingNumberOfBytes)));

                                STATE = 4;


                                break;

                            case 4: //Wait for "ready2"

                                if (mySock.Available < 6)
                                {
                                    //Do nothing
                                }
                                else
                                {
                                    bytesReceived = mySock.Receive(tcpBuffer);
                                    System.Text.UTF8Encoding enc = new System.Text.UTF8Encoding();
                                    string s = enc.GetString(tcpBuffer);
                                    if (s == "ready2")
                                    {
                                        STATE = 5;
                                    }
                                    else
                                    {
                                        notExited = false;
                                    }
                                }

                                break;

                            case 5: //Write out a packet.

                                int bytesToWrite = packetsize;
                                if (remainingNumberOfBytes < packetsize) bytesToWrite = remainingNumberOfBytes;
                                sw1.Start();
                                for (int j = 0; j < bytesToWrite; j += 3)
                                {
                                    double temp = singleChannelBuffer[packetCounter * packetsize / 3 + j / 3] * (524287) / 10.0;
                                    byte[] tempBytes1 = BitConverter.GetBytes((int)temp);
                                    byte[] tempBytes2 = tempBytes1.Skip(0).Take(3).ToArray();
                                    mySock.Send(tempBytes2);
                                }
                                sw1.Stop();


                                //if (lastPacket == 0) messageLog(this, new MessageEvent(NIOS.Name + "has been sent a packet with " + packetsize.ToString() + " bytes."));
                                //else messageLog(this, new MessageEvent(NIOS.Name + "has been sent a packet with " + remainingNumberOfBytes.ToString() + " bytes."));

                                packetCounter++;
                                remainingNumberOfBytes -= packetsize;

                                if (packetCounter == numberOfPackets)
                                {
                                    STATE = 7;
                                }
                                else
                                {
                                    STATE = 6;
                                }
                                break;


                            case 6: //Wait for ready3
                                if (mySock.Available < 6)
                                {
                                    //Do nothing
                                }
                                else
                                {
                                    bytesReceived = mySock.Receive(tcpBuffer);
                                    System.Text.UTF8Encoding enc = new System.Text.UTF8Encoding();
                                    string s = enc.GetString(tcpBuffer);
                                    if (s == "ready3")
                                    {
                                        STATE = 3;
                                    }
                                    else
                                    {
                                        notExited = false;
                                    }
                                }
                                break;


                            case 7: //wait for done

                                if (mySock.Available < 4)
                                {
                                    //Do nothing
                                }
                                else
                                {
                                    bytesReceived = mySock.Receive(tcpBuffer);
                                    System.Text.UTF8Encoding enc = new System.Text.UTF8Encoding();
                                    string s = enc.GetString(tcpBuffer);
                                    if (s == "doney2")
                                    {
                                        notExited = false;
                                        messageLog(this, new MessageEvent(NIOS.Name + "is now ready for clock signals."));
                                        messageLog(this, new MessageEvent(sw1.ElapsedMilliseconds.ToString()));
                                    }
                                    else
                                    {
                                        notExited = false;
                                    }
                                }
                                break;




                        }



                    }
                    //-------------------------------
                    //END STATE MACHINE
                    //-------------------------------

    */
                #endregion

                #endregion
                
                System.GC.Collect();

            }

            else
            {

            }

                }



        public void createSingleBuffer(SOCServer SOC, SequenceData sequence,
          SettingsData settings, Dictionary<int, HardwareChannel> usedAnalogChannels,
          ExampleServerSettings serverSettings,SingleOutputFrame output)
        {
            //Get the correct analogID for this SOC (see normal buffer gen code for more info)
            Dictionary<int, HardwareChannel> analogsUnsorted = getChannelsOnDevice(usedAnalogChannels, SOC.Name);
            List<int> analogIDs = new List<int>();
            List<HardwareChannel> analogs = new List<HardwareChannel>();
            sortDicionaryByID(analogIDs, analogs, analogsUnsorted);

            //Bug fix 1/29/2019
            //This code was adapted from the code that generates buffers on multiple Channels on a single device. Now, we have the case that one device = one channel
            //Adding a check to make sure that the channel is used before generating a buffer. Without this fix, all SOCs added in Poseidon must be assigned in the sequence,
            //or else an exception is thrown
            if (analogIDs.Count > 0)
            {
                int analogID = analogIDs[0];
                double singleChannelBuffer;


                int bytesReceived = 0;
                byte[] tcpBuffer = new byte[6];


                singleChannelBuffer = output.analogValues[analogID];

                Socket mySock = SOC.Sock;


                //Tell the SOC how many bytes are coming (just 3 for output now)
                byte[] tempBytes = BitConverter.GetBytes(3);
                mySock.Send(tempBytes.Skip(0).Take(4).ToArray());
                byte[] writeBuffer = new byte[3];

                //Send the buffer over the socket
                double temp = singleChannelBuffer * (524287) / 10.0;
                byte[] tempBytes1 = BitConverter.GetBytes((int)temp);
                byte[] tempBytes2 = tempBytes1.Skip(0).Take(3).ToArray();
                writeBuffer[0] = tempBytes2[0];
                writeBuffer[1] = tempBytes2[1];
                writeBuffer[2] = tempBytes2[2];

                mySock.Send(writeBuffer);

                bytesReceived = mySock.Receive(tcpBuffer);

                tempBytes = BitConverter.GetBytes(66);


                try
                {
                    SOC.Sock.Send(tempBytes.Skip(0).Take(1).ToArray());
                }
                catch
                {
                    messageLog(this, new MessageEvent("Stop failed"));
                }


                messageLog(this, new MessageEvent(SOC.Name + "has reported receipt of the buffer."));



                System.GC.Collect();

            }

            else
            {

            }

        }

        private static Dictionary<int, HardwareChannel> getChannelsOnDevice(Dictionary<int, HardwareChannel> channels, string deviceName)
        {
            Dictionary<int, HardwareChannel> ans = new Dictionary<int,HardwareChannel>();
            foreach (int id in channels.Keys)
            {
                if (channels[id].DeviceName == deviceName)
                {
                    ans.Add(id, channels[id]);
                }
            }

            return ans;
        }



        private static void sortDicionaryByID(List<int> ids, List<HardwareChannel> hc, Dictionary<int, HardwareChannel> dict)
        {
            ids.Clear();
            hc.Clear();
            ids.AddRange(dict.Keys);
            ids.Sort();
            for (int i = 0; i < ids.Count; i++)
            {
                hc.Add(dict[ids[i]]);
            }
        }

        public static void getAnalogTogglingBuffer(double[] buffer)
        {
            for (int i = 0; i < buffer.Length; i += 2)
            {
                buffer[i] = 5;
                buffer[i + 1] = 0;
            }
        }

        

        #region Thread procedures

        private object marshalLock = new object();
        private TcpChannel tcpChannel;
        private ObjRef objRef;

        /// <summary>
        /// Adapted from corresponding procedure in AtticusServerRuntime
        /// </summary>
        private void startMarshalProc()
        {
            try
            {
                lock (marshalLock)
                {
                    tcpChannel = new TcpChannel(5681);
                    ChannelServices.RegisterChannel(tcpChannel, false);
                    objRef = RemotingServices.Marshal(this, "serverCommunicator");
                }
                messageLog(this, new MessageEvent("Connection suceeded."));
            }
            catch (Exception e)
            {
                messageLog(this, new MessageEvent("Unable to start Marshal due to exception: " + e.Message + e.StackTrace));
                exampleServerForm.reenableConnectButton();
            }
        }



        #endregion
    }
}
