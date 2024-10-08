﻿using DcsBios.Communicator;
using Matric.Integration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;

namespace EXM.DBMM {
    internal class MatricDCSTranslator : IBiosTranslator, IDisposable {

        internal class TxRxNotificationEventArgs : EventArgs {
            public TxRxNotificationEventArgs(int count) {
                Count = count;
            }
            public int Count { get; set; }
        }

        int MAX_VAR_LIST = 100;
        const string COMMON_DATA = "CommonData";
        const string METADATA_START = "MetadataStart";
        const string METADATA_END = "MetadataEnd";
        const string DCS_INPUT_COMMAND = "DCS_INPUT_COMMAND";
        string _currentAircraftName = string.Empty;
        List<string> _allowedVariables = new List<string>();
        Matric.Integration.Matric matricComm;
        Dictionary<string, object> _dcsValues;
        Dictionary<string, ServerVariable> _changesBuffer;
        System.Threading.Timer _timer;
        BiosUdpClient _biosClient = null;
        ILogger _logger;
        private object locker = new object();
        public MatricDCSTranslator(int matricIntegrationPort, BiosUdpClient biosClient, ILogger logger) {
            _logger = logger;
            _biosClient = biosClient;
            _dcsValues = new Dictionary<string, object>();
            _changesBuffer = new Dictionary<string, ServerVariable>();
            _timer = new System.Threading.Timer(SendUpdates,
                                                null,
                                                100,
                                                (int)Math.Round(1000d / Program.mwSettings.UpdateFrequency)
                                                );            
            matricComm = new Matric.Integration.Matric("DCS", null, matricIntegrationPort);
            matricComm.OnVariablesChanged += MatricComm_OnVariablesChanged;
            matricComm.OnControlInteraction += MatricComm_OnControlInteraction;
            //create DCS_INPUT_COMMAND as user editable variable
            ServerVariable dcsInputVariable = new ServerVariable() {
                Name = DCS_INPUT_COMMAND,
                Value = "",
                VariableType = ServerVariable.ServerVariableType.STRING,
                IsPersistent = true,
                IsUserEditable = true
            };
            _changesBuffer.Add(dcsInputVariable.Name, dcsInputVariable);
        }

        public event EventHandler<TxRxNotificationEventArgs> UpdateSentNotification;

        public event EventHandler<TxRxNotificationEventArgs> UpdateBufferSizeNotification;

        private void MatricComm_OnVariablesChanged(object sender, ServerVariablesChangedEventArgs data) {
            //Debug.WriteLine("Got variables changed event notification");
            if (data.ChangedVariables.Contains(DCS_INPUT_COMMAND)) {
                if (data.Variables[DCS_INPUT_COMMAND].Value == null) {
                    return;
                }
                if (string.IsNullOrEmpty(data.Variables[DCS_INPUT_COMMAND].Value as string)) {
                    return;
                }
                string command = data.Variables[DCS_INPUT_COMMAND].Value.ToString();
#if DEBUG
                Debug.WriteLine($"DCS-BIOS import command: {command}");
#endif
                _biosClient?.SendRaw(command); //method takes separate biosAddress and data, but in the end it is concatenated and sent via UDP anyway
                //Reset the variable immediatelly
                matricComm.SetVariables(new List<ServerVariable>() { new ServerVariable() {
                    Name = DCS_INPUT_COMMAND,
                    Value = "",
                    VariableType = ServerVariable.ServerVariableType.STRING,
                    IsPersistent = true,
                    IsUserEditable = true
                    }
                });
            }
        }

        private void MatricComm_OnControlInteraction(object sender, object data) {
            //throw new NotImplementedException();
        }

        public void FromBios<T>(string biosCode, T data) {
            if (biosCode == "_ACFT_NAME" && !data.ToString().Equals(_currentAircraftName)) {
            //get list of user selected variables. If it doesn't exist do not filter, forward everything to MATRIC
                //New aircraft, load config for aircraft
                _currentAircraftName = data.ToString();
#if DEBUG
                Debug.WriteLine($"DCS-BIOS module detected {_currentAircraftName}");
#endif
                if (!Program.mwSettings.AircraftVariables.ContainsKey(_currentAircraftName)) {
                    //Look for an alias
                    if (Program.aircraftBiosConfigurations.Any(t => t.Value.Aliases.Contains(_currentAircraftName))) {
                        _currentAircraftName = Program.aircraftBiosConfigurations.Where(t => t.Value.Aliases.Contains(_currentAircraftName)).FirstOrDefault().Key;
                    }
                }

                if (Program.mwSettings.AircraftVariables.ContainsKey(_currentAircraftName)) {
                    _allowedVariables.Clear();
                    _allowedVariables.AddRange(Program.mwSettings.AircraftVariables[_currentAircraftName]);
                    //Add common and metadata
                    if (Program.mwSettings.AircraftVariables.ContainsKey(COMMON_DATA)) {
                        _allowedVariables.AddRange(Program.mwSettings.AircraftVariables[COMMON_DATA]);
                    }
                    if (Program.mwSettings.AircraftVariables.ContainsKey(METADATA_START)) {
                        _allowedVariables.AddRange(Program.mwSettings.AircraftVariables[METADATA_START]);
                    }
                    if (Program.mwSettings.AircraftVariables.ContainsKey(METADATA_END)) {
                        _allowedVariables.AddRange(Program.mwSettings.AircraftVariables[METADATA_END]);
                    }
                }
            }

            if(string.IsNullOrEmpty(_currentAircraftName)) {
                //Do not export anything until we know the module and can load variables configuration for that module
                return;
            }

            if(_allowedVariables.Count > 0) {
                if(!_allowedVariables.Contains(biosCode)) {
                    return;
                }
            }

            string varName = $"dcs_{biosCode}";
            lock(locker) {
                object currentData = null;
                if (_dcsValues.TryGetValue(varName, out currentData)) {
                    if (currentData.ToString() != data.ToString()) {
                        //Variable has changed
                        //add or replace in changes
                        if (_changesBuffer.ContainsKey(varName)) {
                            _changesBuffer[varName].Value = data;
                        }
                        else {
                            if (data is string) {
                                _changesBuffer.Add(varName, new ServerVariable() { Name = varName, 
                                    Value = data.ToString(), 
                                    VariableType = ServerVariable.ServerVariableType.STRING });
                            }
                            else {
                                //int 
                                _changesBuffer.Add(varName, new ServerVariable() { Name = varName, 
                                    Value = data, 
                                    VariableType = ServerVariable.ServerVariableType.NUMBER });
                            }
                        }
                    }
                    _dcsValues[varName] = data;
                }
                else {
                    _dcsValues[varName] = data;
                    //add or replace in changes
                    if (_changesBuffer.ContainsKey(varName)) {
                        _changesBuffer[varName].Value = data;
                    }
                    else {
                        if (data is string) {
                            _changesBuffer.Add(varName, new ServerVariable() { Name = varName, Value = data.ToString(), VariableType = ServerVariable.ServerVariableType.STRING });
                        }
                        else {
                            //int 
                            _changesBuffer.Add(varName, new ServerVariable() { Name = varName, Value = data, VariableType = ServerVariable.ServerVariableType.NUMBER });
                        }
                    }
                }

            }
        }

        public void SendUpdates(object state) {
#if DEBUG
            Debug.WriteLine($"Sending updates, changes: {_changesBuffer.Keys.Count}");
            foreach (var key in _changesBuffer.Keys) {
                Debug.WriteLine($"{key}: {_changesBuffer[key].Value}");
            }
#endif
            int bufferSize = _changesBuffer.Count;
            Task.Run(() => {
                UpdateBufferSizeNotification?.Invoke(this, new TxRxNotificationEventArgs(Math.Min(bufferSize, 200)));
            });
            int sent = 0;
            lock(locker) {
                if (_changesBuffer.Count >= MAX_VAR_LIST) {
                    Dictionary<string, ServerVariable> sendBuffer = _changesBuffer.Take(MAX_VAR_LIST).ToDictionary();
                    foreach (string key in sendBuffer.Keys) {
                        _changesBuffer.Remove(key);
                    }
                    matricComm.SetVariables(sendBuffer.Values.ToList<ServerVariable>());
                    sent = sendBuffer.Count;
                }
                else { 
                    matricComm.SetVariables(_changesBuffer.Values.ToList<ServerVariable>());
                    sent = _changesBuffer.Count;
                    _changesBuffer.Clear();            
                }
            }
            Task.Run(() => UpdateSentNotification?.Invoke(this, new TxRxNotificationEventArgs(Math.Min(sent, 200))));
        }

        public void Dispose() {
            _timer.Dispose();
            if(matricComm != null) {
                matricComm.Stop();
                matricComm.Dispose();
            }
        }
    }
}
