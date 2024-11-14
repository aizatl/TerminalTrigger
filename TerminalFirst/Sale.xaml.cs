using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TerminalFirst
{
    /// <summary>
    /// Interaction logic for Sale.xaml
    /// </summary>
    public partial class Sale : Window
    {
        public Sale()
        {
            InitializeComponent();
        }

        private void SaleBtn_Click(object sender, RoutedEventArgs e)
        {
            int amount;
            if (!int.TryParse(AmountTextBox.Text, out amount))
            {
                MessageBox.Show("Please enter a valid amount in cents.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string txnID = TxnIDTextBox.Text;
            string reference = ReferenceTextBox.Text;
            string reserver = ReserverTextBox.Text;
            string command = "Sale";
            var dataObject = new
            {
                Amount = amount,
                TxnID = txnID,
                Reference = reference,
                Reserver = reserver
            };
            MainWindow mw = new MainWindow();
            string checksum = mw.CalculateChecksum(command, JsonConvert.SerializeObject(dataObject));

            var request = new
            {
                Command = command,
                Data = dataObject,
                Checksum = checksum
            };
            string jsonRequest = JsonConvert.SerializeObject(request);
            SetCommand(jsonRequest);
        }
        string ProcessObject(dynamic obj)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var property in obj)
            {
                string key = property.Name;
                var value = property.Value;

                // If the value is a nested object, recursively process it
                if (value is JObject)
                {
                    sb.AppendLine($"{key}:");
                    sb.AppendLine(ProcessNestedObject(value));
                }
                else
                {
                    sb.AppendLine($"{key}: {value}");
                }
            }

            return sb.ToString();
        }

        // Helper function to process nested objects recursively and return a formatted string
        string ProcessNestedObject(dynamic nestedObject)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var subProperty in nestedObject)
            {
                string subKey = subProperty.Name;
                var subValue = subProperty.Value;

                // If subValue is a nested object, recursively process it
                if (subValue is JObject)
                {
                    sb.AppendLine($"{subKey}:");
                    sb.AppendLine(ProcessNestedObject(subValue));
                }
                else
                {
                    sb.AppendLine($"{subKey}: {subValue}");
                }
            }

            return sb.ToString();
        }
        private async void SetCommand(string jsonRequest)
        {
            string response = await SendCommandToTerminal(jsonRequest);
            ResponseStatusTextBox.Text = response;
            if (response == "Acknowledge")
            {
                string cardDetails = await WaitForCardDetails();
                getStatusResponse.Text = cardDetails;
                
            }
        }
        private async Task<string> qGetCallbackResult() {
            string host = "192.168.100.11";
            int port = 9000;
            int timeout = 10000;

            TcpListener listener = null;
            try
            {
                listener = new TcpListener(IPAddress.Parse(host), port);
                listener.Start();

                using (TcpClient client = await listener.AcceptTcpClientAsync())
                {
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
                                if (responseObject != null && responseObject.Command.ToString() == "Sale")
                                {
                                    return $"Card Type: {responseObject.Data.CardType.ToString()}\n" +
                                         $"Card No:{responseObject.Data.StatusCode.ToString()}\n" +
                                         $"Hashed PAN:{responseObject.Data.HashedPAN.ToString()}\n" +
                                         $"Card Status:{responseObject.Data.CardStatus.ToString()}\n" +
                                         $"Card UID:{responseObject.Data.CardUID.ToString()}\n";
                                }
                                else 
                                {
                                    return "salah";
                                }
                            }
                            return "No sales details received.";
                        });

                        if (await Task.WhenAny(readTask, Task.Delay(timeout)) == readTask)
                        {
                            return await readTask;
                        }
                        else
                        {
                            return "Timeout waiting for sale details.";
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
        private async Task<string> mGetCallbackResult(NetworkStream streamq)
        {
            string host = "192.168.100.11";
            int port = 9000;
            int timeout = 600000;

            TcpListener listener = null;
            try
            {
                listener = new TcpListener(IPAddress.Parse(host), port);
                listener.Start();

                using (TcpClient client = await listener.AcceptTcpClientAsync())
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("Terminal connected successfully.");
                    });
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

                                if (!string.IsNullOrWhiteSpace(jsonResponse))
                                {
                                    try
                                    {
                                        var responseObject = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
                                        if (responseObject != null && responseObject.Command.ToString() == "Sale")
                                        {
                                            string result = ProcessObject(responseObject);
                                            return result; // return the accumulated result
                                        }
                                        else
                                        {
                                            return "Incorrect command or format.";
                                        }
                                    }
                                    catch (JsonException)
                                    {
                                        return "Invalid JSON format.";
                                    }
                                }
                                else
                                {
                                    return "Received empty response.";
                                }
                            }
                            return "No sales details received.";
                        });

                        if (await Task.WhenAny(readTask, Task.Delay(timeout)) == readTask)
                        {
                            return await readTask;
                        }
                        else
                        {
                            return "Timeout waiting for sale details.";
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
                listener?.Stop();
            }
        }
        private async Task<string> aGetCallbackResult()
        {
            string host = "192.168.100.11";
            int port = 9000;

            TcpListener listener = null;
            try
            {
                listener = new TcpListener(IPAddress.Parse(host), port);
                listener.Start();

                using (TcpClient client = await listener.AcceptTcpClientAsync())
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] responseBuffer = new byte[1024];
                    StringBuilder callbackResponse = new StringBuilder();

                    while (true)
                    {
                        int bytesRead = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);
                        if (bytesRead == 0) break;

                        string jsonResponse = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);

                        if (!string.IsNullOrWhiteSpace(jsonResponse))
                        {
                            try
                            {
                                var responseObject = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
                                if (responseObject != null && responseObject.Command.ToString() == "Sale")
                                {
                                    return $"Card Type: {responseObject.Data.CardType}\n" +
                                           $"Card No: {responseObject.Data.StatusCode}\n" +
                                           $"Hashed PAN: {responseObject.Data.HashedPAN}\n" +
                                           $"Card Status: {responseObject.Data.CardStatus}\n" +
                                           $"Card UID: {responseObject.Data.CardUID}\n";
                                }
                                else
                                {
                                    return "Incorrect command or format.";
                                }
                            }
                            catch (JsonException)
                            {
                                return "Invalid JSON format.";
                            }
                        }
                        else
                        {
                            return "Received empty response.";
                        }
                    }
                    return "No sales details received.";
                }
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
            finally
            {
                listener?.Stop();
            }
        }
        private async Task<string> GetCallbackResult(NetworkStream stream)
        {
            byte[] responseBuffer = new byte[1024];
            StringBuilder callbackResponse = new StringBuilder();

            while (true)
            {
                // Read data from the stream
                int bytesRead = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);

                // If no data was read, the stream is closed
                if (bytesRead == 0) break;

                // Convert the bytes to a string and append it to the response
                string jsonResponse = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);
                callbackResponse.Append(jsonResponse);
            }

            // After the loop ends (stream closed), return the accumulated response
            return callbackResponse.Length > 0 ? callbackResponse.ToString() : "No callback result received.";
        }



        private async Task<string> WaitForCardDetails()
        {
            string host = "192.168.100.11";
            int port = 9000;
            int timeout = 333330000;

            TcpListener listener = null;

            try
            {
                listener = new TcpListener(IPAddress.Parse(host), port);
                listener.Start();

                using (TcpClient client = await listener.AcceptTcpClientAsync())
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("Terminal connected successfully.");
                    });
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
                                if (responseObject != null && responseObject.Command.ToString() == "Sale")
                                {
                                    return $"Card Type: {responseObject.Data.CardType.ToString()}\n" +
                                         $"Card No:{responseObject.Data.StatusCode.ToString()}\n" +
                                         $"Hashed PAN:{responseObject.Data.HashedPAN.ToString()}\n" +
                                         $"Card Status:{responseObject.Data.CardStatus.ToString()}\n" +
                                         $"Card UID:{responseObject.Data.CardUID.ToString()}\n";
                                }
                                else if (responseObject != null && responseObject.Command.ToString() == "GetStatus") {
                                    //string ack = await SendAckToTerminal();
                                    await SendAckWithJson(stream);
                                    string resultFull = await GetCallbackResult(stream);
                                    FullResultTextBox.Text = resultFull;
                                    string cardPresentJson = responseObject.Data.CardPresent.ToString();

                                    string cardPresent = "";
                                    if (cardPresentJson == "1")
                                        cardPresent = "Card Present";
                                    else if (cardPresentJson == "0")
                                        cardPresent = "Card Not Present";

                                    string stageJson = responseObject.Data.Stage.ToString();
                                    string stage = "";
                                    if (stageJson == "0")
                                        stage = "Idle";
                                    else if (stageJson == "10")
                                        stage = "Waiting for card";
                                    else if (stageJson == "20")
                                        stage = "Chip card reading";
                                    else if (stageJson == "21")
                                        stage = "Contactless card reading";
                                    else if (stageJson == "40")
                                        stage = "PIN entry";
                                    else if (stageJson == "50")
                                        stage = "Bank Host authentication";
                                    else if (stageJson == "60")
                                        stage = "See Phone";
                                    else if (stageJson == "70")
                                        stage = "Tap again";
                                    else if (stageJson == "71")
                                        stage = "Present 1 card";
                                    else if (stageJson == "90")
                                        stage = "Busy";
                                    return $"Card Present: {cardPresent}\n" +
                                    $"Stage: {stage}";
                                }
                            }
                            return "No sales details received.";
                        });

                        if (await Task.WhenAny(readTask, Task.Delay(timeout)) == readTask)
                        {
                            return await readTask;
                        }
                        else
                        {
                            return "Timeout waiting for sale details.";
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
        private async Task SendAckWithJson(NetworkStream stream)
        {
            string command = "GetStatus";
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
             ackBytes = Encoding.UTF8.GetBytes(jsonAck);
            await stream.WriteAsync(ackBytes, 0, ackBytes.Length);
            MessageBox.Show("ni aizat(patut ack): " + jsonAck);

            //try
            //{
            //    byte[] responseBuffer = new byte[1024];
            //    int bytesRead = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);

            //    if (bytesRead > 0)
            //    {
            //        string ackJsonResult = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);
            //        // Handle the ackJsonResult, e.g., parse it, log it, etc.
            //    }
            //    else
            //    {
            //        // Handle the case where no data was received
            //        MessageBox.Show("No data received.");
            //    }
            //}
            //catch (SocketException ex)
            //{
            //    // Handle the socket-related error here (e.g., connection reset)
            //    MessageBox.Show($"SocketException: {ex.Message}");
            //}
            //catch (IOException ex)
            //{
            //    // Handle I/O exceptions here (e.g., connection abortion)
            //    MessageBox.Show($"IOException: {ex.Message}");
            //}
            //catch (Exception ex)
            //{
            //    // Handle any other unforeseen errors
            //    MessageBox.Show($"Unexpected error: {ex.Message}");
            //}
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
                        if (responseObject.Command.ToString() == "Sale")
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
        private async System.Threading.Tasks.Task<string> SendAckToTerminal()
        {
            string host = "192.168.100.19";
            int port = 9000;

            try
            {
                using (TcpClient client = new TcpClient(host, port))
                using (NetworkStream stream = client.GetStream())
                {
                    string command = "GetStatus";
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
                    MessageBox.Show("ACK sent successfully.");
                    return jsonAck;
                }
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mw = new MainWindow();
            mw.Show();
            this.Close();
        }
    }
}
