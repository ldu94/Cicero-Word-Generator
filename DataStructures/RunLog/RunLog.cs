using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace DataStructures
{
    [Serializable, TypeConverter(typeof(ExpandableObjectConverter))]
    public class RunLog
    {

        private string sequenceFileName;

        public string SequenceFileName
        {
            get { return sequenceFileName; }
            set { sequenceFileName = value; }
        }

        private string settingsFileName;

        public string SettingsFileName
        {
            get { return settingsFileName; }
            set { settingsFileName = value; }
        }

        private DateTime runTime;

        private DateTime listStartTime;

        public DateTime ListStartTime
        {
            get {
            
                return listStartTime; 
            }
            set { listStartTime = value; }
        }

        public DateTime RunTime
        {
            get { return runTime; }
            set { runTime = value; }
        }
        private SequenceData runSequence;

        public SequenceData RunSequence
        {
            get { return runSequence; }
            set { runSequence = value; }
        }
        private SettingsData runSettings;

        public SettingsData RunSettings
        {
            get { return runSettings; }
            set { runSettings = value; }
        }

        private RunLogAnnotation annotation;

        public RunLogAnnotation Annotation
        {
            get
            {
                if (annotation == null)
                {
                    annotation = new RunLogAnnotation();
                }
                return annotation;
            }
            set { annotation = value; }
        }

        public override string ToString()
        {
          
            return runSequence.ToString();
        }

        public RunLog(DateTime runTime, DateTime listStartTime, SequenceData sequence, SettingsData settings, string sequenceFileName, string settingsFileName)
        {
            this.runTime = runTime;
            this.listStartTime = listStartTime;
            this.runSequence = sequence;
            this.runSettings = settings;
            this.SequenceFileName = sequenceFileName;
            this.SettingsFileName = settingsFileName;
        }

        /// <summary>
        /// Writes the runlog to a timestamped file in the RunLogs directory. Returns the filename that the log was written to. Returns null if
        /// no log was written (for instance, if one already exists with the same stamp).
        /// </summary>
        /// <returns></returns>
        public string WriteLogFile()
        {
            BinaryFormatter b = new BinaryFormatter();
            //runTime.ToString("s") returns a date time string which is formatted to make it string sortable by time
            // and suitable for file names (once the : are replaced with -)

            string fileStamp = "RunLog-" + CiceroUtilityFunctions.getTimeStampString(runTime);
            string fileExt = ".clg";

            string fileDirectory = AppDomain.CurrentDomain.BaseDirectory + "RunLogs\\";

            
            if (!Directory.Exists(fileDirectory))
            {
                Directory.CreateDirectory(fileDirectory);
            }

            string fullFileName = fileDirectory + fileStamp + fileExt;

            if (File.Exists(fullFileName))
            {
                return null;
            }

            FileStream fs = new FileStream(fullFileName, FileMode.Create);
            b.Serialize(fs, this);
            fs.Close();
            return fullFileName;
        }
    }
}
