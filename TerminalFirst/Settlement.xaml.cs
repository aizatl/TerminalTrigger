using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
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
    /// Interaction logic for Settlement.xaml
    /// </summary>
    public partial class Settlement : Window
    {
        public Settlement()
        {
            InitializeComponent();
        }

        private void SettlementBtn_Click(object sender, RoutedEventArgs e)
        {
            ResponseStatusTextBox.Text = "";
            string command = "Settlement";
            string host = "";
            string hostString = (HostComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            if (hostString == "Visa/MasterCard")
                host = "1";
            else if (hostString == "Amex")
                host = "2";
            else if (hostString == "MyDebit")
                host = "3";
            else if (hostString == "China UnionPay")
                host = "4";
            else if (hostString == "Tng")
                host = "99";

            var dataObject = new { HostNo = host };
            string data = JsonConvert.SerializeObject(dataObject);
            MainWindow mw = new MainWindow();
            string checksum = mw.CalculateChecksum(command, data);

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
            ResponseStatusTextBox.Text = response;
            if (response == "Acknowledge")
            {
                string settlementDetails = await WaitForSettlementDetails();
                FullResultTextBox.Text = settlementDetails;
            }
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
                        if (responseObject.Command.ToString() == "Settlement")
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
        private async Task<string> WaitForSettlementDetailsq()
        {
            string host = "192.168.100.19";
            int port = 9000;
            int timeout = 30000; 

            try
            {
                using (TcpClient client = new TcpClient(host, port))
                using (NetworkStream stream = client.GetStream())
                {
                    var readTask = Task.Run(async () =>
                    {
                        byte[] responseBuffer = new byte[1024];
                        StringBuilder callbackResponse = new StringBuilder();
                        while (true)
                        {
                            int bytesRead = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);
                            string jsonResponse = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);

                            var responseObject = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
                            if (responseObject != null && responseObject.Command.ToString() == "Settlement")
                            {
                                return $"Host No: {responseObject.Data.HostNo.ToString()}\n" +
                                       $"Status Code:{responseObject.Data.StatusCode.ToString()}\n" +
                                       $"Batch No:{responseObject.Data.BatchNo.ToString()}\n" +
                                       $"Batch Count:{responseObject.Data.BatchCount.ToString()}\n" +
                                       $"Batch Amount:{responseObject.Data.BatchAmount.ToString()}\n" +
                                       $"Refund Count:{responseObject.Data.RefundCount.ToString()}\n" +
                                       $"Refund Amount:{responseObject.Data.RefundAmount.ToString()}\n";
                            }
                        }
                    });

                    if (await Task.WhenAny(readTask, Task.Delay(timeout)) == readTask)
                    {
                        return await readTask;
                    }
                    else
                    {
                        return "Timeout waiting for settlement details.";
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
        private async Task<string> WaitForSettlementDetails()
        {
            string host = "192.168.100.11"; 
            int port = 9000;
            int timeout = 30000;

            TcpListener listener = null;

            try
            {
                listener = new TcpListener(IPAddress.Parse(host), port);
                listener.Start();

                using (TcpClient client = await listener.AcceptTcpClientAsync()) {
                    MessageBox.Show("Terminal connected successfully.");
                    using (NetworkStream stream = client.GetStream())
                    {
                        var readTask = Task.Run(async () =>
                        {
                            byte[] responseBuffer = new byte[1024];
                            StringBuilder callbackResponse = new StringBuilder();

                            while (true)
                            {
                                int bytesRead = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);
                                if (bytesRead == 0) break;

                                string jsonResponse = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);

                                var responseObject = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
                                if (responseObject != null && responseObject.Command.ToString() == "Settlement")
                                {
                                    await SendAckToTerminal(stream);
                                    return $"Host No: {responseObject.Data[0].HostNo.ToString()}\n" +
                                       $"Status Code: {responseObject.Data[0].StatusCode.ToString()}\n" +
                                       $"Batch No: {responseObject.Data[0].BatchNo.ToString()}\n" +
                                       $"Batch Count: {responseObject.Data[0].BatchCount.ToString()}\n" +
                                       $"Batch Amount: {responseObject.Data[0].BatchAmount.ToString()}\n" +
                                       $"Refund Count: {responseObject.Data[0].RefundCount.ToString()}\n" +
                                       $"Refund Amount: {responseObject.Data[0].RefundAmount.ToString()}\n";
                                        
                                }
                            }
                            return "No settlement details received.";
                        });

                        if (await Task.WhenAny(readTask, Task.Delay(timeout)) == readTask)
                        {
                            return await readTask;
                        }
                        else
                        {
                            return "Timeout waiting for settlement details.";
                        }
                    }
                }
                    

                
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
            finally
            {
                // Stop the listener if it was started
                listener?.Stop();
            }
        }
        private async Task SendAckToTerminal(NetworkStream stream)
        {
            string command = "Settlement";
            string result = "ACK";


            string checksum = CalculateChecksum(command, result);

            var ackObject = new
            {
                Checksum = checksum,
                Command = command,
                Result = result
            };

            string jsonAck = JsonConvert.SerializeObject(ackObject);

            byte[] ackBytes = Encoding.UTF8.GetBytes(jsonAck);
            await stream.WriteAsync(ackBytes, 0, ackBytes.Length);
            MessageBox.Show("ni aizat(patut ack): " + jsonAck);

           
        }
        public string CalculateChecksum(string command, string data)
        {
            using (MD5 md5 = MD5.Create())
            {
                string rawInput = command + data;
                byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(rawInput));
                StringBuilder hashString = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    hashString.Append(b.ToString("x2"));
                }
                return hashString.ToString();
            }
        }

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mv = new MainWindow();
            mv.Show();
            this.Close();
        }
    }
}
