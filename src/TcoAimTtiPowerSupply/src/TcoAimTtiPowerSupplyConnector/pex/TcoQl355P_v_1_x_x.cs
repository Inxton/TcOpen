using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Vortex.Connector;

namespace TcoAimTtiPowerSupply
{
    public partial class TcoQl355P_v_1_x_x

    {

        public void Initialize()
        {
            _getCommandTask.InitializeExclusively(InvokeGetCommand);
            _setCommandTask.InitializeExclusively(InvokeSetCommand);

        }

        // Exceptions are intentionally not caught here: TcoRemoteTask reports them to the PLC (_hasException)
        // and to the logger, and does not signal Done.
        internal void InvokeSetCommand()
        {
            this.Read();

            var cmd = FindCommand(eQlCommandType.SetCommand, Enum.GetName(typeof(eTcoQWlSeriesSupplySetCommands_v_1_x_x), _setCommandTask._command.Command.LastValue));
            var message = cmd.Syntax.Replace("<N>", _setCommandTask._command.NValue.LastValue.ToString()).Replace("<NRF>", _setCommandTask._command.NrfValue.LastValue.ToString(" 00.00", System.Globalization.CultureInfo.InvariantCulture));

            using (TcpClient client = new TcpClient(_config.IpAddress.LastValue, _config.Port.LastValue))
            {
                client.SendTimeout = _config.Timeout.LastValue;
                client.ReceiveTimeout = _config.Timeout.LastValue;

                using (NetworkStream stream = client.GetStream())
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    // Set commands have no response.
                    writer.WriteLine(message);
                    writer.Flush(); // Flush the buffer to ensure data is sent immediately
                }
            }
        }

        internal void InvokeGetCommand()
        {
            this.Read();

            var cmd = FindCommand(eQlCommandType.GetCommand, Enum.GetName(typeof(eTcoQWlSeriesSupplyGetCommands_v_1_x_x), _getCommandTask._command.Command.LastValue));
            var message = cmd.Syntax.Replace("<N>", _getCommandTask._command.NValue.LastValue.ToString());

            using (TcpClient client = new TcpClient(_config.IpAddress.LastValue, _config.Port.LastValue))
            {
                client.SendTimeout = _config.Timeout.LastValue;
                client.ReceiveTimeout = _config.Timeout.LastValue;

                using (NetworkStream stream = client.GetStream())
                using (StreamWriter writer = new StreamWriter(stream))
                using (StreamReader reader = new StreamReader(stream))
                {
                    writer.WriteLine(message);
                    writer.Flush(); // Flush the buffer to ensure data is sent immediately

                    // Receive a response from the server
                    var response = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(response))
                    {
                        throw new IOException($"No response from power supply {_config.IpAddress.LastValue}:{_config.Port.LastValue} to '{message.Trim()}'.");
                    }

                    _status.State.Nr1Value.Cyclic = 0;
                    _status.State.Nr2Value.Cyclic = 0;
                    _status.State.NValue.Cyclic = 0;
                    _status.State.ConvertMessage.Cyclic = string.Empty;
                    _status.State.Resposne.Cyclic = response;

                    if (!string.IsNullOrEmpty(cmd.ResponseSyntax))
                    {
                        if (cmd.ResponseSyntax == "<NR1><RMT>")
                        {
                            short result;
                            if (short.TryParse(response, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
                                _status.State.Nr1Value.Cyclic = result;
                            else
                                _status.State.ConvertMessage.Cyclic = "Unable to convert " + response;
                        }

                        else if (cmd.ResponseSyntax == "<NR2>V<RMT>" || cmd.ResponseSyntax == "<NR2>A<RMT>")
                        {
                            float result;
                            var splitted = response.Split('A', 'V');
                            if (float.TryParse(splitted[0], NumberStyles.Any, CultureInfo.InvariantCulture, out result))
                                _status.State.Nr2Value.Cyclic = result;
                            else
                                _status.State.ConvertMessage.Cyclic = "Unable to convert " + splitted[0];
                        }

                        //"V<N> <NR2><RMT>"
                        else
                        {
                            var splitted = response.Split(' ');
                            short nResult;
                            string nValue = new String(splitted[0].Where(Char.IsDigit).ToArray());
                            if (short.TryParse(nValue, NumberStyles.Any, CultureInfo.InvariantCulture, out nResult))
                                _status.State.NValue.Cyclic = (byte)nResult;
                            else
                                _status.State.ConvertMessage.Cyclic = "Unable to convert " + splitted[0];

                            float result;
                            if (splitted.Length > 1 && float.TryParse(splitted[1], NumberStyles.Any, CultureInfo.InvariantCulture, out result))
                                _status.State.Nr2Value.Cyclic = result;
                            else
                                _status.State.ConvertMessage.Cyclic = "Unable to convert " + response;
                        }
                    }

                    _status.State.Write();

                    if (!string.IsNullOrEmpty(_status.State.ConvertMessage.Cyclic))
                    {
                        throw new FormatException($"Unexpected response '{response}' to '{message.Trim()}': {_status.State.ConvertMessage.Cyclic}");
                    }
                }
            }
        }

        private static QlSeriesCommand FindCommand(eQlCommandType type, string key)
        {
            var cmd = QlSeriesCommands.Commands.FirstOrDefault(p => p.Type == type && p.Key == key);
            if (cmd == null)
            {
                throw new InvalidOperationException($"Command '{key}' of type {type} is not defined in {nameof(QlSeriesCommands)}.");
            }
            return cmd;
        }
    }
    
}