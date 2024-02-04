﻿using DcsBios.Communicator;
using Matric.Integration;
using System.Diagnostics;

namespace DCSMATRICFeeder {
    internal class MatricDCSTranslator : IBiosTranslator {
        int MAX_VAR_LIST = 100;
        Matric.Integration.Matric matricComm;
        Dictionary<string, object> _dcsValues;
        Dictionary<string, ServerVariable> _changesBuffer;
        System.Threading.Timer _timer;
        private object locker = new object();
        public MatricDCSTranslator() {
            _dcsValues = new Dictionary<string, object>();
            _changesBuffer = new Dictionary<string, ServerVariable>();
            _timer = new System.Threading.Timer(SendUpdates, null, 100, 100);            
            matricComm = new Matric.Integration.Matric("DCS", null, 5300);
            matricComm.OnVariablesChanged += MatricComm_OnVariablesChanged;
        }

        private void MatricComm_OnVariablesChanged(object sender, ServerVariablesChangedEventArgs data) {
            //throw new NotImplementedException();
        }

        public void FromBios<T>(string biosCode, T data) {
            Debug.WriteLine($"{biosCode}     {data}");
            lock(locker) {
                object currentData = null;
                if (_dcsValues.TryGetValue(biosCode, out currentData)) {
                    if (currentData.ToString() != data.ToString()) {
                        //add or replace in changes
                        if (_changesBuffer.ContainsKey(biosCode)) {
                            _changesBuffer[biosCode].Value = data;
                        }
                        else {
                            if (data is string) {
                                _changesBuffer.Add(biosCode, new ServerVariable() { Name = biosCode, Value = data.ToString(), VariableType = ServerVariable.ServerVariableType.STRING });
                            }
                            else {
                                //int 
                                _changesBuffer.Add(biosCode, new ServerVariable() { Name = biosCode, Value = data, VariableType = ServerVariable.ServerVariableType.NUMBER });
                            }
                        }
                    }
                }
                else {
                    //add or replace in changes
                    if (_changesBuffer.ContainsKey(biosCode)) {
                        _changesBuffer[biosCode].Value = data;
                    }
                    else {
                        if (data is string) {
                            _changesBuffer.Add(biosCode, new ServerVariable() { Name = biosCode, Value = data.ToString(), VariableType = ServerVariable.ServerVariableType.STRING });
                        }
                        else {
                            //int 
                            _changesBuffer.Add(biosCode, new ServerVariable() { Name = biosCode, Value = data, VariableType = ServerVariable.ServerVariableType.NUMBER });
                        }
                    }
                }

            }

        }

        public void SendUpdates(object state) {
            Debug.WriteLine($"Changes: {_changesBuffer.Keys.Count}");
            lock(locker) {
                if (_changesBuffer.Count >= MAX_VAR_LIST) {
                    Dictionary<string, ServerVariable> sendBuffer = _changesBuffer.Take(MAX_VAR_LIST).ToDictionary();
                    foreach (string key in sendBuffer.Keys) {
                        _changesBuffer.Remove(key);
                    }
                    matricComm.SetVariables(sendBuffer.Values.ToList<ServerVariable>());
                }
                else { 
                    matricComm.SetVariables(_changesBuffer.Values.ToList<ServerVariable>());
                    _changesBuffer.Clear();            
                }
            }
        }
    }
}
