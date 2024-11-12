using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TerminalFirst
{
    /// <summary>
    /// Interaction logic for LastTrans.xaml
    /// </summary>
    public partial class LastTrans : Window
    {
        public LastTrans()
        {
            InitializeComponent();
        }

        private void GetLastTnxBtn_Click(object sender, RoutedEventArgs e)
        {
            string id = txnID.Text.ToString();
            string command = "GetLastTransaction";
            var dataObject = new { TxnId = id };
            MainWindow mw = new MainWindow();
            string checksum = mw.CalculateChecksum(command, id);

            var request = new
            {
                Command = command,
                Data = dataObject,
                Checksum = checksum
            };
            string jsonRequest = JsonConvert.SerializeObject(request);
            SetCommand(jsonRequest);
        }
        private async void SetCommand(string jsonRequest)
        {
            string response = await SendCommandToTerminal(jsonRequest);
            FullResultTextBox.Text = response;
        }
        private async System.Threading.Tasks.Task<string> SendCommandToTerminal(string jsonRequest)
        {
            string host = "192.168.100.19";
            int port = 9000;

            try
            {
                using (TcpClient client = new TcpClient(host, port))
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] requestBytes = Encoding.UTF8.GetBytes(jsonRequest);
                    await stream.WriteAsync(requestBytes, 0, requestBytes.Length);

                    byte[] responseBuffer = new byte[1024];
                    int bytesRead = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);
                    string jsonResponse = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);

                    var responseObject = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
                    if (responseObject != null)
                    {
                        if (responseObject.Command.ToString() == "GetLastTransaction")
                        {
                            string result = responseObject.Result.ToString();
                            if (result == "ACK")
                            {
                                return "Acknowledge";
                            }
                            else if (result == "NAK")
                            {
                                return "Not Acknowledge";
                            }
                            else if (result == "ESC")
                            {
                                return "Invalid Parameter";
                            }
                            else if (result == "CAN")
                            {
                                return "Cancel";
                            }
                            else
                            {
                                return "Error";
                            }
                        }
                        else
                        {
                            return "Error";
                        }

                    }
                    else
                    {
                        return "No result in response or response is invalid.";
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}
