﻿//Written by Ceramicskate0
//Copyright 2020
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.Eventing.Reader;
using System.IO;

namespace SWELF
{
    internal class Read_EventLog
    {
        internal static EventRecord Windows_EventLog_from_API { get; set; }
        internal static long First_EventLogID_From_Check;
        internal static long Last_EventLogID_From_Check;
        private static bool MissingLogInFileDueToException = false;

        internal Read_EventLog()
        {

        }


        internal void READ_EventLog(string Eventlog_FullName,long PlaceKeeper_EventRecordID=1)
        {
            long EVTlog_PlaceHolder = PlaceKeeper_EventRecordID;

            try
            {
                if (EVTlog_PlaceHolder <= 1)
                {
                    EVTlog_PlaceHolder = Settings.EventLog_w_PlaceKeeper[Eventlog_FullName];
                }
            }
            catch (Exception e)
            {
                EVTlog_PlaceHolder = 1;
            }

            if (Settings.CHECK_If_EventLog_Exsits(Eventlog_FullName))
            {
                long First_EventID;
                long Last_EventID;

                try
                {
                     First_EventID = GET_First_EventRecordID_InLogFile(Eventlog_FullName);
                }
                catch
                {
                    First_EventID = -1;
                }
                try
                {
                    Last_EventID = GET_Last_EventRecordID_InLogFile(Eventlog_FullName);
                }
                catch
                {
                    Last_EventID = -1;
                }

                if (First_EventID==-1 || Last_EventID==-1)
                {
                    Error_Operation.Log_Error("READ_EventLog() GET_Last_EventRecordID_InLogFile && GET_First_EventRecordID_InLogFile", Eventlog_FullName + " EventLog is empty or null.", "", Error_Operation.LogSeverity.Informataion);
                    Settings.EventLog_w_PlaceKeeper[Eventlog_FullName] = 0;
                }
                else if (PlaceKeeper_EventRecordID > First_EventID && PlaceKeeper_EventRecordID < Last_EventID)//Normal operation placekkeeper in middle of log file
                {
                    EVTlog_PlaceHolder = PlaceKeeper_EventRecordID;
                    READ_WindowsEventLog_API(Eventlog_FullName, EVTlog_PlaceHolder);
                    Settings.EventLog_w_PlaceKeeper[Eventlog_FullName] = Last_EventID;
                }
                else if (Last_EventID == PlaceKeeper_EventRecordID)//no logs added
                {
                    EVTlog_PlaceHolder = PlaceKeeper_EventRecordID;
                }
                else if (PlaceKeeper_EventRecordID<=1)
                {
                    READ_WindowsEventLog_API(Eventlog_FullName, First_EventID);
                    EventLog_SWELF.WRITE_Warning_EventLog("Logging as EventLog Source 1st run for Eventlog named '"+ Eventlog_FullName +"' on machine named '"+ Settings.ComputerName+ "' due to PlaceKeeper_EventRecordID<=1");
                    Settings.EventLog_w_PlaceKeeper[Eventlog_FullName] = Last_EventID;
                }
                else if (First_EventID > PlaceKeeper_EventRecordID)//missed all logs and missing log files send alert for missing log files
                {
                    READ_WindowsEventLog_API(Eventlog_FullName, First_EventID);
                    EventLog_SWELF.WRITE_FailureAudit_Error_To_EventLog("Missed "+ (First_EventID-PlaceKeeper_EventRecordID) + " logs from '"+ Eventlog_FullName+"' on machine '"+Settings.ComputerName +"' the first eventlog id was older than where app left off. Possible log file cycle/overwrite between runs. First event log id number in the log is "+ First_EventID+" SWELF left off from last run at "+PlaceKeeper_EventRecordID);
                    Settings.EventLog_w_PlaceKeeper[Eventlog_FullName] = Last_EventID;
                }
                else//unknown/catch condition assume 1st run
                {
                    READ_WindowsEventLog_API(Eventlog_FullName, First_EventID);
                    EventLog_SWELF.WRITE_Warning_EventLog("ERROR: App unable to determine app reading state in event log. App starting over. App not told to reset. '"+Eventlog_FullName +"' '"+ Settings.ComputerName+ "'. unknown/catch condition assume 1st run");
                    Settings.EventLog_w_PlaceKeeper[Eventlog_FullName] = Last_EventID;
                }
            }
            else
            {
                Error_Operation.Log_Error("READ_EventLog() if (Settings.FIND_EventLog_Exsits())", Eventlog_FullName+" EventLog does not exist.","",Error_Operation.LogSeverity.Informataion);
            }
        }

        internal void READ_EVTX_File(string FilePath)
        {
            using (var reader = new EventLogReader(FilePath, PathType.FilePath))
            {
                while ((Windows_EventLog_from_API = reader.ReadEvent()) != null)
                {
                    try
                    {
                        EventLog_Entry Eventlog = new EventLog_Entry();
                        using (Windows_EventLog_from_API)
                        {
                            Eventlog.EventLog_Seq_num = Windows_EventLog_from_API.RecordId.Value;
                            Eventlog.ComputerName = Windows_EventLog_from_API.MachineName;
                            Eventlog.EventID = Windows_EventLog_from_API.Id;
                            Eventlog.CreatedTime = Windows_EventLog_from_API.TimeCreated.Value;
                            try
                            {
                                Eventlog.LogName = Windows_EventLog_from_API.LogName;
                            }
                            catch
                            {
                                Eventlog.LogName = Settings.SWELF_EventLog_Name;
                            }
                            try
                            {
                                Eventlog.Severity = Windows_EventLog_from_API.LevelDisplayName;
                            }
                            catch (Exception e)
                            {
                                try
                                {
                                    Eventlog.Severity = Windows_EventLog_from_API.OpcodeDisplayName;
                                }
                                catch
                                {
                                    Eventlog.Severity = Windows_EventLog_from_API.Level.Value.ToString();//if this doesnt work we have issues that we cant fix
                                }
                            }
                            try
                            {
                                Eventlog.TaskDisplayName = Windows_EventLog_from_API.TaskDisplayName;
                            }
                            catch
                            {
                                Eventlog.TaskDisplayName = Windows_EventLog_from_API.ProviderName;
                            }
                            try
                            {
                                Eventlog.EventData = Windows_EventLog_from_API.FormatDescription().ToLower();
                                Eventlog.GET_FileHash();
                                Eventlog.GET_IP_FromLogFile();
                                Eventlog.GET_XML_of_Log = Windows_EventLog_from_API.ToXml();
                            }
                            catch
                            {
                                Eventlog.GET_XML_of_Log = Windows_EventLog_from_API.ToXml();
                                Eventlog.EventData = Windows_EventLog_from_API.ToXml();
                            }

                        }
                        Data_Store.EVTX_File_Logs.Enqueue(Eventlog);
                    }
                    catch (Exception e)
                    {
                        Error_Operation.Log_Error("READ_EVTX_File()", e.Message.ToString() +"Event Log Missing due to improper format. Possible tampering or invalid format.", e.StackTrace.ToString(), Error_Operation.LogSeverity.FailureAudit);
                    }
                }
            }
        }

        internal void READ_EVTX_Folder(string Folder_Path)
        {
            Settings.Evtx_Files = Directory.GetFiles(Folder_Path, "*.evtx").ToList();

            for (int x=0;x< Settings.Evtx_Files.Count;++x)
            {
                READ_EVTX_File(Settings.Evtx_Files.ElementAt(x));
            }
        }

        private static void READ_WindowsEventLog_API(string Eventlog_FullName, long RecordID_From_Last_Read)
        {
            try
            {
                EventLogQuery eventsQuery = new EventLogQuery(Eventlog_FullName, PathType.LogName);
                EventLogReader EventLogtoReader = new EventLogReader(eventsQuery);

                EventLog_Entry SWELF_Eventlog;

                while (GET_EventLogEntry_From_API(EventLogtoReader) != null)
                {
                    try
                    {
                        SWELF_Eventlog = new EventLog_Entry();
                        if (Windows_EventLog_from_API.RecordId.Value > RecordID_From_Last_Read)
                        {
                            SWELF_Eventlog.CreatedTime = Windows_EventLog_from_API.TimeCreated.Value;//if this doesnt work we have issues that we cant fix
                            SWELF_Eventlog.EventLog_Seq_num = Windows_EventLog_from_API.RecordId.Value;//if this doesnt work we have issues that we cant fix
                            SWELF_Eventlog.EventID = Windows_EventLog_from_API.Id; //if this doesnt work we have issues that we cant fix
                            SWELF_Eventlog.LogName = Windows_EventLog_from_API.LogName;

                            try
                            {
                                SWELF_Eventlog.ComputerName = Windows_EventLog_from_API.MachineName;
                            }
                            catch (Exception e)
                            {
                                SWELF_Eventlog.ComputerName = Settings.ComputerName;
                            }

                            try
                            {
                                SWELF_Eventlog.Severity = Windows_EventLog_from_API.LevelDisplayName;
                            }
                            catch (Exception e)
                            {
                                try
                                {
                                    SWELF_Eventlog.Severity = Windows_EventLog_from_API.OpcodeDisplayName;
                                }
                                catch
                                {
                                    SWELF_Eventlog.Severity = Windows_EventLog_from_API.Level.Value.ToString();//if this doesnt work we have issues that we cant fix
                                }
                            }

                            try
                            {
                                SWELF_Eventlog.TaskDisplayName = Windows_EventLog_from_API.TaskDisplayName;
                            }
                            catch (Exception e)
                            {
                                SWELF_Eventlog.TaskDisplayName = Windows_EventLog_from_API.ProviderName;//if this doesnt work we have issues that we cant fix
                            }

                            try
                            {
                                if (Settings.AppConfig_File_Args.ContainsKey(Settings.SWELF_AppConfig_Args[16]))
                                {
                                    SWELF_Eventlog.EventData = "CreationDate="+SWELF_Eventlog.CreatedTime + "\r\nEventLog_Seq_Number=" + SWELF_Eventlog.EventLog_Seq_num + "\r\nEventID=" + SWELF_Eventlog.EventID + "\r\nSeverity=" + SWELF_Eventlog.Severity + "\r\nEventLogName=" + SWELF_Eventlog.LogName + "\r\n\r\n" + Windows_EventLog_from_API.FormatDescription().ToLower();
                                }
                                else
                                {
                                    SWELF_Eventlog.EventData = Windows_EventLog_from_API.FormatDescription().ToLower();
                                }
                            }
                            catch (Exception e)
                            {
                                if (Settings.AppConfig_File_Args.ContainsKey(Settings.SWELF_AppConfig_Args[16]))
                                {
                                    SWELF_Eventlog.EventData = "CreationDate=" + SWELF_Eventlog.CreatedTime + "\r\nEventLog_Seq_Number=" + SWELF_Eventlog.EventLog_Seq_num + "\r\nEventID=" + SWELF_Eventlog.EventID + "\r\nSeverity=" + SWELF_Eventlog.Severity + "\r\nEventLogName=" + SWELF_Eventlog.LogName + "\r\n\r\n" + Windows_EventLog_from_API.ToXml();
                                }
                                else
                                {
                                    SWELF_Eventlog.EventData = Windows_EventLog_from_API.ToXml();//if this doesnt work we have issues that we cant fix
                                }
                            }

                            try
                            {
                                SWELF_Eventlog.GET_XML_of_Log = Windows_EventLog_from_API.ToXml();
                                if (string.IsNullOrEmpty(SWELF_Eventlog.GET_XML_of_Log))
                                {
                                    SWELF_Eventlog.GET_XML_of_Log = "ERROR READING. Windows_EventLog_from_API.ToXml()";
                                }
                            }
                            catch (Exception e)
                            {
                                SWELF_Eventlog.GET_XML_of_Log = "ERROR READING. Windows_EventLog_from_API.ToXml() Exception Thrown";
                            }

                            try
                            {
                                SWELF_Eventlog.GET_FileHash();
                            }
                            catch (Exception e)
                            {
                                //unable to get file hashs from log
                            }
                            try
                            {
                                SWELF_Eventlog.GET_IP_FromLogFile();
                            }
                            catch (Exception e)
                            {
                                //unable to get IP values from log
                            }
                            //try
                            //{
                            //    EventLogName.EventlogMissing = Sec_Checks.CHECK_If_EventLog_Missing(EventLogName, SWELF_Eventlog);
                            //}
                            //catch (Exception e)
                            //{
                            //    EventLogName.EventlogMissing = true;
                            //}

                            //try
                            //{
                            //    EventLogName.ID_Number_Of_Individual_log_Entry_EVENTLOG = Windows_EventLog_from_API.RecordId.Value;
                            //}
                            //catch (Exception e)
                            //{
                            //    EventLogName.ID_Number_Of_Individual_log_Entry_EVENTLOG = 0;
                            //}
                            Data_Store.contents_of_EventLog.Enqueue(SWELF_Eventlog);
                        }
                    }
                    catch (Exception e)
                    {
                        Error_Operation.Log_Error("INDEX_Record_FROM_API() Missing Event Log(s) Due To Exception with log format while reading in eventlogs.", "EventLog='" + Eventlog_FullName + "' " + e.Message.ToString(), e.StackTrace.ToString(), Error_Operation.LogSeverity.Warning);
                        MissingLogInFileDueToException = true;
                    }
                }

                try
                {
                    if (Settings.AppConfig_File_Args.ContainsKey(Settings.SWELF_AppConfig_Args[12]) || Settings.AppConfig_File_Args.ContainsKey(Settings.SWELF_AppConfig_Args[11]))
                    {
                        Settings.IP_List_EVT_Logs.AddRange(Settings.IP_List_EVT_Logs.Distinct().ToList());
                        Settings.Hashs_From_EVT_Logs.AddRange(Settings.Hashs_From_EVT_Logs.Distinct().ToList());
                    }
                }
                catch (Exception e)
                {
                    Error_Operation.Log_Error("Settings.IP_List_EVT_Logs.AddRange() OR Settings.Hashs_From_EVT_Logs.AddRange()", e.Message.ToString(), e.StackTrace.ToString(), Error_Operation.LogSeverity.Warning);
                }
                MissingLogInFileDueToException = false;
            }
            catch (Exception e)
            {
                Error_Operation.Log_Error("READ_WindowsEventLog_API() Missing All Event Log(s) Due To Exception. ", "EventLog='" + Eventlog_FullName + "' " + e.Message.ToString() + " " + Eventlog_FullName + " " + RecordID_From_Last_Read , e.StackTrace.ToString(), Error_Operation.LogSeverity.FailureAudit);
                MissingLogInFileDueToException = true;
            }
            
        }

        private static EventRecord GET_EventLogEntry_From_API(EventLogReader EventLogtoReader)
        { 
            try
            {
                return Windows_EventLog_from_API = EventLogtoReader.ReadEvent();
            }
            catch
            {
                return Windows_EventLog_from_API;
            }
        }

        private static long GET_Last_EventRecordID_InLogFile(string Eventlog_FullName)
        {
            TimeSpan Timeout = new TimeSpan(0, 30, 0);
            EventLogReader EventLogtoReader = new EventLogReader(Eventlog_FullName, PathType.LogName);
            EventLogtoReader.BatchSize = 100;
            EventRecord Windows_EventLog_API = EventLogtoReader.ReadEvent();

            First_EventLogID_From_Check = Windows_EventLog_API.RecordId.Value;

            while ((Windows_EventLog_API = EventLogtoReader.ReadEvent(Timeout)) != null)
            {
                Last_EventLogID_From_Check = Windows_EventLog_API.RecordId.Value;
            }
            return Last_EventLogID_From_Check;
        }

        private static long GET_First_EventRecordID_InLogFile(string Eventlog_FullName)
        {
            EventLogReader EventLogtoReader = new EventLogReader(Eventlog_FullName, PathType.LogName);
            EventLogtoReader.BatchSize = 100;
            EventRecord Windows_EventLog_API = EventLogtoReader.ReadEvent();
            EventLog_Entry Eventlog = new EventLog_Entry();
            return Windows_EventLog_API.RecordId.Value;
        }
    }
}
